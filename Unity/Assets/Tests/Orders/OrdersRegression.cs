using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Orders.Tests
{
    public static class OrdersRegression
    {
        static OrderContext Context(int house = 5, int other = 5, string era = EraIds.WorkshopYard)
        { return new OrderContext(new FamilyStages(era, FamilyIds.All.Select(id => new FamilyStage(id, id == FamilyIds.House ? house : other)))); }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } catch (FormatException) { return; } catch (OverflowException) { return; }
            throw new Exception("Expected refusal.");
        }
        static OrderProposal Accept(OrderState state, string id, long sequence, long coins = 7)
        { var context = Context(); return OrderEngine.PrepareAccept(context, state, OrderEngine.QuoteAccept(context, state, id), sequence, coins); }
        static OrderProposal Observe(OrderState state, long sequence, SpinOutcome outcome, int deliveries = 0, long coins = 7)
        { return OrderEngine.PrepareObserve(Context(), state, new OrderEvent(sequence, state.Cursor, outcome, deliveries), sequence, coins); }
        static OrderState Reload(OrderState state) { return OrderStateCodec.DecodeOrDefault(OrderStateCodec.Encode(state)); }
        static OrderState Ready(string id, long accepted = 10)
        {
            var state = Accept(OrderState.Empty, id, accepted).After;
            int events = id == OrderIds.TrashSort ? 4 : 3;
            for (int i = 1; i <= events; i++) state = id == OrderIds.TrashSort ? Observe(state, accepted + i, new SpinOutcome(2, 2, 2)).After : Observe(state, accepted + i, new SpinOutcome(3, 3, 3), 1).After;
            return state;
        }
        static OrderProposal Restore(OrderProposal p, OrderState after = null, long? afterCoins = null, long? reward = null, string key = null,
            string revision = OrderIds.RuleRevision, int version = 1)
        {
            return OrderEngine.RestoreProposal(version, revision, p.Action, p.Sequence, p.OrderId, p.ExpectedInstanceSequence, p.Context,
                Reload(p.Before), after ?? Reload(p.After), p.CoinsBefore, afterCoins ?? p.CoinsAfter, reward ?? p.RewardCoins, p.Event, key ?? p.Key, p.NoOp);
        }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>(); Action<string, Action> run = (name, action) => { action(); passed.Add(name); };
            run("four known free-preview choices require actual six-family chapter completion", () => {
                Check(OrderCatalog.Choices.Count == 4 && OrderCatalog.Get("order.sort-salvage").Target == 12 && OrderCatalog.Get("order.sort-salvage").RewardCoins == 120 && OrderCatalog.Get("order.neighborhood-supply").Target == 3 && OrderCatalog.Get("order.neighborhood-supply").RewardCoins == 180 && OrderCatalog.Get("order.gather-potatoes").Target == 12 && OrderCatalog.Get("order.gather-potatoes").RewardCoins == 120 && OrderCatalog.Get("order.pack-boxes").Target == 12 && OrderCatalog.Get("order.pack-boxes").RewardCoins == 120, "Wrong independent catalog oracle");
                string empty = OrderStateCodec.Encode(OrderState.Empty);
                var preview = OrderEngine.QuoteAccept(Context(), OrderState.Empty, OrderIds.TrashSort);
                Check(preview.Order.RewardCoins == 120 && preview.DiscardedProgress == 0 && empty == OrderStateCodec.Encode(OrderState.Empty), "Preview mutated state");
                Reject(() => OrderEngine.QuoteAccept(Context(4), OrderState.Empty, OrderIds.TrashSort));
                Reject(() => OrderEngine.QuoteAccept(Context(2, 2, EraIds.Junkyard), OrderState.Empty, OrderIds.TrashSort));
                Reject(() => OrderCatalog.Get("unknown"));
            });
            run("trash events cap at twelve and explicit claim pays120 once without automatic replacement", () => {
                var state = Accept(OrderState.Empty, OrderIds.TrashSort, 10, 5).After;
                Check(state.InstanceSequence == 10 && state.Cursor == 10 && state.Progress == 0, "Wrong acceptance anchor");
                for (int i = 1; i <= 4; i++) { var p = Observe(state, 10 + i, new SpinOutcome(2, 2, 2), coins: 5); Check(p.RewardCoins == 0 && p.CoinsAfter == 5, "Auto reward"); state = Reload(p.After); Check(state.Progress == i * 3, "Wrong trash progress"); }
                var ready = Observe(state, 15, new SpinOutcome(2, 2, 2), coins: 5).After; Check(ready.Ready && ready.Progress == 12, "Unbounded progress");
                var claim = OrderEngine.PrepareClaim(Context(), ready, OrderIds.TrashSort, 10, 16, 5);
                Check(claim.RewardCoins == 120 && claim.CoinsAfter == 125 && claim.After.ActiveOrderId == null && claim.After.Progress == 0 && claim.After.LatestClaim.InstanceSequence == 10, "Claim result");
                var first = OrderEngine.Apply(Context(), ready, 5, Restore(claim)); Check(first.Applied && first.Coins == 125, "First pure application");
                var replay = OrderEngine.Apply(Context(), Reload(first.State), 999, claim); Check(!replay.Applied && replay.Coins == 999, "Replay rewound wallet or paid again");
            });
            run("delivery order counts completed deliveries rather than symbols or reward coins", () => {
                var state = Accept(OrderState.Empty, OrderIds.PigeonDeliveries, 20, 125).After;
                state = Observe(state, 21, new SpinOutcome(3, 3, 0), 0, 125).After; Check(state.Progress == 0, "Counted mere pigeon appearances");
                state = Observe(state, 22, new SpinOutcome(3, 0, 0), 1, 125).After; Check(state.Progress == 1, "Missed completed carry delivery");
                state = Observe(state, 23, new SpinOutcome(3, 3, 3), 1, 125).After;
                state = Observe(state, 24, new SpinOutcome(3, 3, 3), 1, 125).After;
                var claim = OrderEngine.PrepareClaim(Context(), Reload(state), OrderIds.PigeonDeliveries, 20, 25, 125);
                Check(claim.RewardCoins == 180 && claim.CoinsAfter == 305, "Wrong delivery reward");
                Reject(() => new OrderEvent(26, 24, new SpinOutcome(0, 0, 0), 1));
                Reject(() => new OrderEvent(26, 24, new SpinOutcome(3, 3, 3), 0));
                Reject(() => new OrderEvent(26, 24, new SpinOutcome(3, 3, 3), 2));
            });
            run("manual repeat uses a new acceptance instance and old claims/events never fund it", () => {
                var ready = Ready(OrderIds.PigeonDeliveries, 20);
                var claim = OrderEngine.PrepareClaim(Context(), ready, OrderIds.PigeonDeliveries, 20, 24, 7);
                var next = Accept(Reload(claim.After), OrderIds.PigeonDeliveries, 26, 187).After;
                Check(next.InstanceSequence == 26 && next.Progress == 0 && next.Cursor == 26, "Instance reused");
                var oldEvent = new OrderEvent(21, 20, new SpinOutcome(3, 3, 3), 1);
                Reject(() => OrderEngine.PrepareObserve(Context(), next, oldEvent, 21, 187)); Check(next.Progress == 0, "Retroactive repeated progress");
                var oldClaim = OrderEngine.PrepareClaim(Context(), next, OrderIds.PigeonDeliveries, 20, 24, 187);
                Check(oldClaim.NoOp && OrderEngine.Apply(Context(), next, 200, oldClaim).Coins == 200, "Historical exact replay paid");
                Reject(() => OrderEngine.PrepareClaim(Context(), next, OrderIds.PigeonDeliveries, 20, 27, 187));
                for (int sequence = 27; sequence <= 29; sequence++) next = Observe(next, sequence, new SpinOutcome(3, 3, 3), 1, 187).After;
                var second = OrderEngine.PrepareClaim(Context(), next, OrderIds.PigeonDeliveries, 26, 30, 187);
                Check(second.CoinsAfter == 367 && second.After.LatestClaim.InstanceSequence == 26, "Legitimate repeat failed");
                Reject(() => OrderEngine.PrepareClaim(Context(), second.After, OrderIds.PigeonDeliveries, 20, 24, 367));
            });
            run("replacement and cancellation require explicit quotes and forfeit without reward", () => {
                var state = Observe(Accept(OrderState.Empty, OrderIds.TrashSort, 10).After, 11, new SpinOutcome(2, 0, 0)).After;
                var quote = OrderEngine.QuoteAccept(Context(), state, OrderIds.PigeonDeliveries);
                Check(quote.DiscardedProgress == 1 && state.ActiveOrderId == OrderIds.TrashSort, "Replacement not reviewable");
                var changed = OrderEngine.PrepareAccept(Context(), state, quote, 12, 7); Check(changed.After.Progress == 0 && changed.After.InstanceSequence == 12 && changed.CoinsAfter == 7, "Switch awarded or retained progress");
                Reject(() => OrderEngine.QuoteAccept(Context(), changed.After, OrderIds.PigeonDeliveries));
                var ready = Ready(OrderIds.TrashSort); Reject(() => OrderEngine.QuoteAccept(Context(), ready, OrderIds.PigeonDeliveries));
                var cancelQuote = OrderEngine.QuoteCancel(Context(), ready); Check(cancelQuote.ForfeitedRewardCoins == 120 && cancelQuote.DiscardedProgress == 12, "Missing forfeiture quote");
                var canceled = OrderEngine.PrepareCancel(Context(), ready, cancelQuote, 20, 7);
                Check(canceled.CoinsAfter == 7 && canceled.RewardCoins == 0 && canceled.After.ActiveOrderId == null, "Cancel granted reward");
                Check(Accept(Reload(canceled.After), OrderIds.TrashSort, 21).After.Progress == 0, "Cancel enabled retained progress");
                Reject(() => OrderEngine.PrepareClaim(Context(), canceled.After, OrderIds.TrashSort, 10, 22, 7));
            });
            run("new foreground events require cursor continuity and the same reserved spin sequence", () => {
                var accepted = Accept(OrderState.Empty, OrderIds.TrashSort, 100).After;
                var source = new OrderEvent(110, 100, new SpinOutcome(2, 0, 0), 0);
                var observed = OrderEngine.PrepareObserve(Context(), accepted, source, 110, 7);
                Check(observed.After.Progress == 1 && observed.After.Cursor == 110, "Non-spin sequence gap incorrectly rejected");
                var duplicate = OrderEngine.PrepareObserve(Context(), Reload(observed.After), source, 110, 7); Check(duplicate.NoOp && duplicate.After.Progress == 1, "Duplicate advanced progress");
                Reject(() => OrderEngine.PrepareObserve(Context(), observed.After, new OrderEvent(110, 100, new SpinOutcome(2, 2, 0), 0), 110, 7));
                Reject(() => OrderEngine.PrepareObserve(Context(), observed.After, new OrderEvent(120, 111, new SpinOutcome(2, 0, 0), 0), 120, 7));
                Reject(() => OrderEngine.PrepareObserve(Context(), accepted, source, 111, 7));
                Reject(() => OrderEngine.PrepareObserve(Context(), accepted, new OrderEvent(99, 0, new SpinOutcome(2, 2, 2), 0), 99, 7));
                var later = Observe(observed.After, 120, new SpinOutcome(2, 0, 0));
                Reject(() => OrderEngine.PrepareObserve(Context(), later.After, source, 110, 7));
                Reject(() => OrderEngine.PrepareObserve(Context(), later.After, new OrderEvent(110, 100, new SpinOutcome(0, 0, 0), 0), 110, 7));
            });
            run("inspection and unrelated operations do not produce implicit order events", () => {
                var state = Accept(OrderState.Empty, OrderIds.TrashSort, 10).After; string before = OrderStateCodec.Encode(state);
                for (int i = 0; i < 10; i++) { OrderCatalog.Get(OrderIds.TrashSort); OrderEngine.QuoteCancel(Context(), state); OrderEngine.QuoteAccept(Context(), state, OrderIds.PigeonDeliveries); Reload(state); }
                Check(before == OrderStateCodec.Encode(state) && state.Progress == 0, "Pure inspection/reload advanced orders");
                var idle = Observe(OrderState.Empty, 1, new SpinOutcome(2, 2, 2)).After;
                var accepted = Accept(idle, OrderIds.TrashSort, 20).After; Check(accepted.Progress == 0 && accepted.LastEvent == null, "Retroactive idle progress");
            });
            run("codec is canonical bounded and rejects malformed present data", () => {
                Check(ReferenceEquals(OrderStateCodec.DecodeOrDefault(null), OrderState.Empty), "Absent field wrote/defaulted incorrectly");
                var sample = Ready(OrderIds.TrashSort); var payload = OrderStateCodec.Encode(sample);
                Check(payload.Length < 4096 && OrderStateCodec.Encode(Reload(sample)) == payload, "State roundtrip");
                foreach (string invalid in new[] { "", " ", payload + "|unknown", payload.Replace("orders-state.v1", "orders-state.v2"), payload.Replace("order.sort-salvage", "unknown"), payload.Replace("|10|", "|010|"), payload.Replace("|12|", "|-1|"), new string('x', 4097) }) Reject(() => OrderStateCodec.DecodeOrDefault(invalid));
                var claimed = OrderEngine.PrepareClaim(Context(), sample, OrderIds.TrashSort, 10, 15, 7).After;
                Check(OrderStateCodec.Encode(Reload(claimed)) == OrderStateCodec.Encode(claimed), "Claim record reload");
                Reject(() => OrderStateCodec.DecodeOrDefault(OrderStateCodec.Encode(claimed).Replace(",120", ",121")));
                var acceptedAgain = Accept(claimed, OrderIds.TrashSort, 20).After;
                var fields = OrderStateCodec.Encode(acceptedAgain).Split('|'); fields[4] = "0";
                Reject(() => OrderStateCodec.DecodeOrDefault(string.Join("|", fields)));
                fields = OrderStateCodec.Encode(acceptedAgain).Split('|'); fields[5] = "25"; fields[8] = "order.sort-salvage,10,25,120";
                Reject(() => OrderStateCodec.DecodeOrDefault(string.Join("|", fields)));
            });
            run("frozen proposals restore exactly and reject reward/state/rule tampering", () => {
                var accept = Accept(OrderState.Empty, OrderIds.TrashSort, 10); Check(Restore(accept).Key == accept.Key, "Accept restore");
                var observed = Observe(accept.After, 11, new SpinOutcome(2, 2, 2)); Check(Restore(observed).After.Progress == 3, "Observe restore");
                var ready = Ready(OrderIds.TrashSort); var claim = OrderEngine.PrepareClaim(Context(), ready, OrderIds.TrashSort, 10, 15, 7);
                Check(Restore(claim).CoinsAfter == 127, "Claim restore");
                var cancel = OrderEngine.PrepareCancel(Context(), observed.After, OrderEngine.QuoteCancel(Context(), observed.After), 12, 7); Check(Restore(cancel).After.ActiveOrderId == null, "Cancel restore");
                Reject(() => Restore(claim, reward: 121)); Reject(() => Restore(claim, afterCoins: 128)); Reject(() => Restore(claim, after: ready));
                Reject(() => Restore(claim, key: new string('f', 64))); Reject(() => Restore(claim, revision: "orders.rules.v2")); Reject(() => Restore(claim, version: 2));
                var duplicate = OrderEngine.PrepareObserve(Context(), observed.After, observed.Event, 11, 7); Check(Restore(duplicate).NoOp, "No-op restore");
            });
            run("stale quotes/applications and conflicting replay never overwrite newer state or wallet", () => {
                var state = Accept(OrderState.Empty, OrderIds.TrashSort, 10).After;
                var quote = OrderEngine.QuoteAccept(Context(), state, OrderIds.PigeonDeliveries);
                var changed = Observe(state, 11, new SpinOutcome(2, 0, 0));
                Reject(() => OrderEngine.PrepareAccept(Context(), changed.After, quote, 12, 7));
                Reject(() => OrderEngine.Apply(Context(), state, 8, changed));
                var later = Observe(changed.After, 12, new SpinOutcome(2, 0, 0)); Reject(() => OrderEngine.Apply(Context(), later.After, 7, changed));
                var conflicting = new OrderState(changed.After.InstanceSequence, changed.After.ActiveOrderId, changed.After.Progress, changed.After.Cursor, changed.After.LastEvent, changed.After.LastCommandSequence, new string('f', 64), changed.After.LatestClaim);
                Reject(() => OrderEngine.Apply(Context(), conflicting, 7, changed));
            });
            run("reward/sequence overflow and forged checkpoint shape fail without losing ready progress", () => {
                var ready = Ready(OrderIds.TrashSort); string before = OrderStateCodec.Encode(ready);
                Reject(() => OrderEngine.PrepareClaim(Context(), ready, OrderIds.TrashSort, 10, 15, long.MaxValue));
                Check(ready.Ready && before == OrderStateCodec.Encode(ready), "Overflow consumed ready reward");
                Reject(() => Accept(OrderState.Empty, OrderIds.TrashSort, OrderState.MaximumSequence + 1));
                Reject(() => new OrderState(10, OrderIds.TrashSort, 13, 10, null, 10, new string('a', 64), null));
                Reject(() => new OrderState(10, OrderIds.TrashSort, 1, 10, null, 10, new string('a', 64), null));
                Reject(() => new OrderClaimRecord(OrderIds.TrashSort, 10, 11, 121));
                Reject(() => new OrderState(20, OrderIds.TrashSort, 0, 20, null, 25, new string('a', 64), new OrderClaimRecord(OrderIds.PigeonDeliveries, 10, 25, 180)));
                Reject(() => new OrderState(20, OrderIds.TrashSort, 0, 0, null, 20, new string('a', 64), null));
                Reject(() => OrderEngine.PrepareClaim(Context(), ready, OrderIds.TrashSort, 9, 15, 7));
                var canceled = OrderEngine.PrepareCancel(Context(), ready, OrderEngine.QuoteCancel(Context(), ready), 15, long.MaxValue); Check(canceled.CoinsAfter == long.MaxValue, "Overflow prevents safe forfeit");
            });
            run("repeated legitimate cycles retain only one claim/event checkpoint and bounded payload", () => {
                var state = OrderState.Empty; long sequence = 1, wallet = 0; int largest = 0;
                for (int cycle = 0; cycle < 64; cycle++)
                {
                    long instance = sequence; state = Accept(state, OrderIds.TrashSort, sequence++, wallet).After;
                    for (int spin = 0; spin < 4; spin++) state = Observe(state, sequence++, new SpinOutcome(2, 2, 2), coins: wallet).After;
                    var claim = OrderEngine.PrepareClaim(Context(), state, OrderIds.TrashSort, instance, sequence++, wallet);
                    var applied = OrderEngine.Apply(Context(), state, wallet, Restore(claim)); state = Reload(applied.State); wallet = applied.Coins;
                    largest = Math.Max(largest, OrderStateCodec.Encode(state).Length);
                }
                Check(wallet == 64 * 120 && state.LatestClaim.InstanceSequence == 379 && sequence == 385 && largest < 512, "Cycle reward/retention oracle");
            });
            return passed.AsReadOnly();
        }
    }
}
