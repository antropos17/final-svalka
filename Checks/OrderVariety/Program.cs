using System;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Orders;

static class Program
{
    static readonly JsonStateCodec Codec = new JsonStateCodec();
    static readonly string[] NewIds = { "order.gather-potatoes", "order.pack-boxes" };
    // Literal seed1 trace and counts, independent of the implementation's metric dispatch.
    static readonly int[][] Trace = {
        new[] {0,0,2},new[] {0,2,0},new[] {0,0,1},new[] {2,2,1},new[] {2,0,0},
        new[] {3,0,3},new[] {0,3,2},new[] {3,1,3},new[] {0,1,2},new[] {0,0,2},
        new[] {0,3,0},new[] {2,1,0},new[] {3,1,0},new[] {1,1,2},new[] {1,2,0},
        new[] {2,3,2},new[] {2,2,1},new[] {3,0,3},new[] {1,0,0},new[] {1,0,3} };
    sealed class Storage : ISnapshotStorage
    {
        public string Primary, Backup;
        public int Writes;
        public string ReadPrimary() { return Primary; }
        public string ReadBackup() { return Backup; }
        public void Write(string raw) { Writes++; Backup = Primary; Primary = raw; }
    }
    static void Need(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (InvalidOperationException) { return; }
        catch (FormatException) { return; }
        catch (OverflowException) { return; }
        throw new Exception("Expected refusal");
    }
    static OrderContext Context(int house = 5)
    { return new OrderContext(new FamilyStages("workshop-yard", FamilyIds.All.Select(id => new FamilyStage(id, id == "house" ? house : 5)))); }
    static OrderProposal Accept(OrderState before, string id, long sequence)
    { return OrderEngine.PrepareAccept(Context(), before, OrderEngine.QuoteAccept(Context(), before, id), sequence, 7); }
    static OrderProposal Observe(OrderState before, long sequence, int a, int b, int c, int deliveries = 0)
    { return OrderEngine.PrepareObserve(Context(), before, new OrderEvent(sequence, before.Cursor, new SpinOutcome(a,b,c), deliveries), sequence, 7); }
    static OrderState Reload(OrderState state)
    {
        string raw = OrderStateCodec.Encode(state);
        var restored = OrderStateCodec.DecodeOrDefault(raw);
        Need(OrderStateCodec.Encode(restored) == raw, "Order codec changed canonical bytes");
        return restored;
    }
    static OrderProposal Restore(OrderProposal p, string revision = null)
    {
        return OrderEngine.RestoreProposal(1, revision ?? p.RuleRevision, p.Action, p.Sequence, p.OrderId, p.ExpectedInstanceSequence,
            p.Context, Reload(p.Before), Reload(p.After), p.CoinsBefore, p.CoinsAfter, p.RewardCoins, p.Event, p.Key, p.NoOp);
    }
    static OrderState Ready(string id, int symbol)
    {
        var state = Accept(OrderState.Empty, id, 10).After;
        for (int sequence = 11; sequence <= 14; sequence++) state = Observe(state, sequence, symbol, symbol, symbol).After;
        return Reload(state);
    }
    static Storage Stored(ModelState state) { return new Storage { Primary = Codec.Encode(state) }; }
    static GameSession Open(Storage storage)
    {
        var session = GameSession.Open(new GameStore(storage, Codec), 1);
        Need(!session.RecoveryRequired, "Fixture/recovery failed: " + session.Error);
        return session;
    }
    static void Refuse(Storage storage, GameSession session, Action action)
    {
        string raw = storage.Primary; int writes = storage.Writes;
        Reject(action);
        Need(storage.Primary == raw && storage.Writes == writes && !session.RecoveryRequired, "Refusal changed save/IO/session");
    }
    static ModelState CompleteFixture()
    {
        // Synthetic funded fixture, no spin grind and no ordinary save access.
        var state = ModelState.Fresh(1); state.coins = 10000;
        var session = Open(Stored(state));
        foreach (string era in new[] { "junkyard", "workshop-yard" })
        {
            if (era == "workshop-yard")
            {
                session.EnterWorkshopYard(session.State.sequence + 1);
                session.AcknowledgeEraTransition(session.State.sequence + 1);
            }
            for (int level = 1; level <= 2; level++) foreach (string family in FamilyIds.All)
                session.SelectChapterProject(ChapterIds.Project(era, family, level), session.State.sequence + 1);
        }
        Need(session.State.coins == 690 && session.Chapter.Progress.CompletedUpgradeIds.Count == 24 && session.Chapter.Skills.Receipts.Count == 0, "Completed fixture drift");
        return session.State;
    }
    static void EconomicSame(ModelState a, ModelState b)
    {
        var da = WaveProgress.Of(a).Delivery; var db = WaveProgress.Of(b).Delivery;
        Need(a.seed == b.seed && a.coins == b.coins && da.PigeonProgress == db.PigeonProgress && da.Cadence == db.Cadence &&
            ChapterEconomy.Same(a.chapter, b.chapter), "Order altered RNG/wallet/carry/skills/selection/construction");
    }
    static void Catalog()
    {
        Need((int)OrderMetric.TrashAppearances == 0 && (int)OrderMetric.CompletedDeliveries == 1, "Old metric identities changed");
        Need(OrderCatalog.Choices.Select(o => o.Id).SequenceEqual(new[] {"order.sort-salvage","order.neighborhood-supply","order.gather-potatoes","order.pack-boxes"}), "Catalog IDs/order drift");
        var trash = OrderCatalog.Get("order.sort-salvage"); var pigeon = OrderCatalog.Get("order.neighborhood-supply");
        Need(trash.Target == 12 && trash.RewardCoins == 120 && trash.Label == "Sort twelve trash finds" &&
            pigeon.Target == 3 && pigeon.RewardCoins == 180 && pigeon.Label == "Complete three pigeon deliveries", "Legacy definitions changed");
        Need(OrderIds.GatherPotatoes == NewIds[0] && OrderIds.PackBoxes == NewIds[1], "New public IDs drift");
        foreach (string id in NewIds)
        {
            var order = OrderCatalog.Get(id); Need(order.Target == 12 && order.RewardCoins == 120, "New tuning drift");
            string empty = OrderStateCodec.Encode(OrderState.Empty);
            OrderEngine.QuoteAccept(Context(), OrderState.Empty, id);
            Need(empty == OrderStateCodec.Encode(OrderState.Empty), "Preview mutated state");
            Reject(() => OrderEngine.QuoteAccept(Context(4), OrderState.Empty, id));
        }
        Reject(() => OrderCatalog.Get("order.unknown"));
    }
    static void Metrics()
    {
        string[] ids = {NewIds[0],NewIds[1],"order.sort-salvage","order.neighborhood-supply"};
        int[] first = {2,1,0,0}, second = {2,1,0,1};
        for (int i = 0; i < ids.Length; i++)
        {
            var state = Accept(OrderState.Empty, ids[i], 10).After;
            var p = Observe(state, 11, 0,0,1);
            Need(p.After.Progress == first[i] && p.CoinsAfter == 7 && p.RewardCoins == 0, "001 metric/reward drift");
            p = Observe(p.After, 12, 3,3,3,1);
            Need(p.After.Progress == second[i], "Pigeon completion crossed into another metric");
            Need(Restore(p).Key == p.Key, "Observe frozen restore drift");
        }
        foreach (string id in NewIds)
        {
            int symbol = id == NewIds[0] ? 0 : 1;
            var state = Accept(OrderState.Empty, id, 10).After;
            for (int sequence = 11; sequence <= 13; sequence++) state = Observe(state, sequence, symbol,symbol,symbol).After;
            state = Observe(state, 14, symbol,symbol,2).After; Need(state.Progress == 11, "Expected eleven literal appearances");
            var cap = Observe(state, 15, symbol,symbol,symbol);
            Need(cap.After.Progress == 12 && cap.After.Ready && cap.CoinsAfter == 7 && cap.RewardCoins == 0, "11+3 failed cap/no-auto-reward");
            Need(Observe(cap.After, 16, symbol,symbol,symbol).After.Progress == 12, "Ready progress exceeded target");
        }
    }
    static void Lifecycle()
    {
        foreach (string id in NewIds)
        {
            int symbol = id == NewIds[0] ? 0 : 1;
            var ready = Ready(id, symbol);
            var claim = OrderEngine.PrepareClaim(Context(), ready, id, 10, 15, 7);
            Need(claim.RuleRevision == "orders.rules.v2" && claim.CoinsAfter == 127 && claim.RewardCoins == 120 && claim.After.ActiveOrderId == null, "Explicit new claim drift");
            var applied = OrderEngine.Apply(Context(), ready, 7, Restore(claim));
            Need(applied.Applied && applied.Coins == 127, "New claim failed application");
            Need(!OrderEngine.Apply(Context(), Reload(applied.State), 999, claim).Applied && OrderEngine.Apply(Context(), applied.State, 999, claim).Coins == 999, "Replay paid/rewound wallet");
            var next = Accept(Reload(claim.After), id, 20).After;
            Need(next.InstanceSequence == 20 && next.Progress == 0 && next.Cursor == 20 && next.LastEvent == null, "New instance reused old progress");
            Reject(() => OrderEngine.PrepareObserve(Context(), next, ready.LastEvent, 14, 7));
            var replay = OrderEngine.PrepareClaim(Context(), next, id, 10, 15, 999);
            Need(replay.NoOp && OrderEngine.Apply(Context(), next, 999, Restore(replay)).Coins == 999, "Retained old claim funded new instance");
            Reject(() => OrderEngine.PrepareClaim(Context(), next, id, 10, 21, 7));
            Reject(() => Restore(claim, "orders.rules.v1"));
            var forfeiture = OrderEngine.QuoteCancel(Context(), ready);
            Need(forfeiture.ForfeitedRewardCoins == 120 && forfeiture.DiscardedProgress == 12, "Missing new forfeiture disclosure");
            var canceled = OrderEngine.PrepareCancel(Context(), ready, forfeiture, 15, 7);
            Need(Restore(canceled).CoinsAfter == 7 && canceled.After.ActiveOrderId == null && canceled.After.LatestClaim == null, "Cancel granted reward");
            Reject(() => OrderEngine.PrepareClaim(Context(), canceled.After, id, 10, 16, 7));
            Need(Accept(canceled.After, id, 20).After.Progress == 0, "Forfeited reward returned");
            var mixed = Accept(claim.After, "order.sort-salvage", 20);
            Need(mixed.RuleRevision == "orders.rules.v2" && Restore(mixed).After.LatestClaim.OrderId == id, "Mixed retained new history mislabeled V1");
            Reject(() => Restore(mixed, "orders.rules.v1"));
            Reject(() => OrderEngine.PrepareClaim(Context(), ready, id, 10, 15, long.MaxValue - 119));
        }
    }
    static void LegacyFrozen()
    {
        // Independently calculated from literal original V1 canonical inputs, including
        // its provisional zero key and ordered six-family context. Do not regenerate
        // these hashes from the current engine when a regression fails.
        string[][] hashes = {
            new[] {"59eb7890455295f95b54235ea716cb04f7f27477a52742342e285fdf48da0985","5b0d8f84135abefad823bae640f0eca4adf89d6f663f1a7a33b91b097de8f673","5a5b51a77b9099c55e2853637a0de486421d9fe1402190b8f44347c030e320e5","3dc274b53dfd9fc2de70c2776a7b7f8534a45bf3d21ad8ace509c3144f69823d"},
            new[] {"66e94ceae27046f96ec8b83c5c9ac16a2aeb6afda9aaaf32907d6823d3d407f4","078c86778ada90e1906654eef3016265bc01f7cca111f9b919756c098ccd0dd8","e922edaa98891a670655ba993c5e4970b041e79e5f5e0e79e3386e9268078812","0f54393136ba72a54c0103c70c9c047ccdbcbb9f1bc62d109be0ba4efef701b0"} };
        for (int i = 0; i < 2; i++)
        {
            string id = i == 0 ? "order.sort-salvage" : "order.neighborhood-supply"; int symbol = i == 0 ? 2 : 3;
            var accept = Accept(OrderState.Empty, id, 10);
            var observed = Observe(accept.After, 11, symbol,symbol,symbol,i);
            var cancel = OrderEngine.PrepareCancel(Context(), observed.After, OrderEngine.QuoteCancel(Context(), observed.After), 12, 7);
            var ready = accept.After; int events = i == 0 ? 4 : 3;
            for (int step = 1; step <= events; step++) ready = Observe(ready, 10 + step, symbol,symbol,symbol,i).After;
            var claim = OrderEngine.PrepareClaim(Context(), ready, id, 10, 11 + events, 7);
            var proposals = new[] {accept,observed,cancel,claim};
            for (int j = 0; j < proposals.Length; j++)
            {
                Need(proposals[j].RuleRevision == "orders.rules.v1" && proposals[j].Key == hashes[i][j], "Frozen original V1 key changed: " + id + "/" + j);
                Need(Restore(proposals[j], "orders.rules.v1").Key == hashes[i][j], "Original frozen descriptor failed restore");
                Reject(() => Restore(proposals[j], "orders.rules.v2"));
            }
            Need(claim.CoinsAfter == (i == 0 ? 127 : 187), "Old reward changed");
        }
    }
    static void CoreLifecycle(ModelState complete)
    {
        for (int kind = 0; kind < 2; kind++)
        {
            string id = NewIds[kind]; var storage = Stored(complete); var session = Open(storage); var control = Open(Stored(complete));
            session.ExecuteChapterSpin(session.State.sequence + 1); control.ExecuteChapterSpin(control.State.sequence + 1);
            long instance = session.State.sequence + 1; session.AcceptOrder(id, instance);
            Need(session.Journey.Orders.Progress == 0, "Acceptance counted earlier seed1 appearances");
            Refuse(storage, session, () => session.AcceptOrder(NewIds[1-kind], session.State.sequence + 1));
            Refuse(storage, session, () => session.ClaimOrder(id, instance, session.State.sequence + 1));
            // Trace begins at seed1 spin2 because one actual foreground spin preceded acceptance.
            int[] expected = kind == 0 ? new[] {2,4,4,6,7,8,8,9,11,12} : new[] {0,1,2,2,2,2,3,4,4,4,5,6,8,9,9,10,10,11,12};
            for (int index = 0; index < expected.Length; index++)
            {
                session.ExecuteChapterSpin(session.State.sequence + 1, manualSpin: index % 2 == 0);
                control.ExecuteChapterSpin(control.State.sequence + 1, manualSpin: index % 2 == 0);
                Need(session.State.lastSpin.symbols.SequenceEqual(Trace[index+1]) && session.Journey.Orders.Progress == expected[index], "Core new metric/seed literal trace drift");
                EconomicSame(session.State, control.State);
                session = Open(storage); Need(session.Journey.Orders.Progress == expected[index], "Core reload lost progress");
            }
            Need(session.Journey.Orders.Ready && session.Journey.Orders.LatestClaim == null, "Ready auto-claimed");
            var ready = session.State; long wallet = ready.coins; long claimSequence = ready.sequence + 1;
            session.ClaimOrder(id, instance, claimSequence);
            Need(session.State.coins == wallet + 120 && session.Journey.Orders.ActiveOrderId == null && session.Journey.Orders.LatestClaim.OrderId == id, "Core explicit reward drift");
            Need(ready.seed == session.State.seed && ChapterEconomy.Same(ready.chapter, session.State.chapter) && ChapterEconomy.SameDelivery(WaveProgress.Of(ready).Delivery, WaveProgress.Of(session.State).Delivery), "Claim altered economic state");
            long next = session.State.sequence + 1; session.AcceptOrder(id, next);
            string raw = storage.Primary; int writes = storage.Writes;
            session.ClaimOrder(id, instance, claimSequence);
            Need(storage.Primary == raw && storage.Writes == writes && session.Journey.Orders.Progress == 0 && session.Journey.Orders.InstanceSequence == next, "Core old claim replaced new instance/wrote");
            Refuse(storage, session, () => session.ClaimOrder(id, instance, session.State.sequence + 1));
            var cancelStorage = Stored(ready); var cancel = Open(cancelStorage);
            cancel.CancelOrder(id, instance, cancel.State.sequence + 1);
            Need(cancel.State.coins == wallet && cancel.Journey.Orders.LatestClaim == null && cancel.Journey.Orders.ActiveOrderId == null, "Core ready cancel awarded reward");
            Refuse(cancelStorage, cancel, () => cancel.ClaimOrder(id, instance, cancel.State.sequence + 1));
        }
    }
    static void NonSpinIsolation(ModelState complete)
    {
        foreach (string id in NewIds)
        {
            var fresh = ModelState.Fresh(1); fresh.coins = 10000; var lockedStorage = Stored(fresh); var locked = Open(lockedStorage);
            Refuse(lockedStorage, locked, () => locked.AcceptOrder(id, 1));
            var storage = Stored(complete); var session = Open(storage);
            session.AcceptOrder(id, session.State.sequence + 1);
            string order = OrderStateCodec.Encode(session.Journey.Orders);
            session.Checkpoint(1000); session.SettleOffline(1600);
            Need(session.State.lastSettlement.coins == 10, "Independent offline credit changed");
            var skill = ChapterEconomy.QuoteSkill(session.State, "slot.coin-boost"); session.BuySkill(skill.Request);
            session = Open(storage);
            Need(OrderStateCodec.Encode(session.Journey.Orders) == order, "Clock/offline/skill/reload synthesized progress");
            Need(session.State.seed == 1 && session.Chapter.SelectedProjectId == null && session.Chapter.Skills.Receipts.Count == 1, "Unrelated command changed RNG/selection/skills");
            session.ExecuteChapterSpin(session.State.sequence + 1);
            Need(session.Journey.Orders.Progress == (id == NewIds[0] ? 2 : 0), "First subsequent foreground lost after non-spin gaps");
            string raw = storage.Primary; int writes = storage.Writes;
            session.ExecuteChapterSpin(session.State.sequence);
            Need(raw == storage.Primary && writes == storage.Writes, "Duplicate foreground progressed/wrote twice");
        }
    }
    static void PendingRoundtrip(ModelState complete)
    {
        foreach (string id in new[] {"order.sort-salvage","order.neighborhood-supply",NewIds[0],NewIds[1]})
        {
            var accepted = JourneyEconomy.Reserve(complete, "order-accept", id, 0, complete.sequence + 1);
            string raw = Codec.Encode(accepted); Need(Codec.Encode(Codec.Decode(raw)) == raw, "Pending accept codec changed bytes");
            var storage = new Storage { Primary = raw }; var session = Open(storage);
            Need(storage.Writes == 1 && session.Journey.Orders.ActiveOrderId == id && session.Journey.Orders.Progress == 0 && session.State.coins == 690, "Pending accept recovery drift");
            long instance = session.Journey.Orders.InstanceSequence;
            int symbol = id == "order.sort-salvage" ? 2 : id == "order.neighborhood-supply" ? 3 : id == NewIds[0] ? 0 : 1;
            int count = symbol == 3 ? 3 : 4;
            // Pure events make a valid bounded ready checkpoint; the Core fixture has
            // no lastSpin, and these order sequence identities are explicit fixture data.
            var order = session.Journey.Orders;
            for (int step = 1; step <= count; step++) order = OrderEngine.PrepareObserve(Context(), order,
                new OrderEvent(instance+step, order.Cursor, new SpinOutcome(symbol,symbol,symbol), symbol == 3 ? 1 : 0), instance+step, 690).After;
            var ready = session.State; ready.sequence = order.LastCommandSequence; ready.revision = ready.sequence * 2;
            ready.journey = new JourneyState(order, ready.journey.Guidance, ready.journey.AdoptedSequence);
            GameLogic.Validate(ready);
            var pending = JourneyEconomy.Reserve(ready, "order-claim", id, instance, ready.sequence+1);
            string frozen = Codec.Encode(pending); Need(Codec.Encode(Codec.Decode(frozen)) == frozen, "Pending claim codec changed bytes");
            var claimStorage = new Storage { Primary = frozen }; var recovered = Open(claimStorage);
            Need(recovered.State.coins == (symbol == 3 ? 870 : 810) && claimStorage.Writes == 1 && recovered.Journey.Orders.ActiveOrderId == null, "Frozen claim recovery wrong reward/count");
            string committed = claimStorage.Primary; Open(claimStorage);
            Need(claimStorage.Primary == committed && claimStorage.Writes == 1, "Reload repeated recovered reward");
            recovered.ClaimOrder(id, instance, pending.pending.sequence);
            Need(claimStorage.Primary == committed && claimStorage.Writes == 1, "Exact recovered-claim replay wrote");
        }
    }
    static int Main()
    {
        try
        {
            Catalog(); Console.WriteLine("PASS catalog literals, old identities and completion gate");
            Metrics(); Console.WriteLine("PASS four distinct metrics, literal001/333 deltas and11+3 cap");
            Lifecycle(); Console.WriteLine("PASS new explicit claims, replay/new-instance isolation, forfeiture and revision boundary");
            LegacyFrozen(); Console.WriteLine("PASS eight literal original V1 operation hashes and frozen restores");
            var complete = CompleteFixture();
            CoreLifecycle(complete); Console.WriteLine("PASS both Core seeded lifecycles, one-active, claim replay, cancel and unchanged economics");
            NonSpinIsolation(complete); Console.WriteLine("PASS locked/preaccept/offline/skill/reload/duplicate-spin isolation");
            PendingRoundtrip(complete); Console.WriteLine("PASS four IDs pending accept/claim codec recovery and exact replay");
            Console.WriteLine("PASS7 focused order-variety groups; private memory storage only");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
