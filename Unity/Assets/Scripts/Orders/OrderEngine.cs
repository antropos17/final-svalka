using System;
using System.Security.Cryptography;
using System.Text;

namespace JunkToNeon.Orders
{
    public static class OrderEngine
    {
        const string PlaceholderKey = "0000000000000000000000000000000000000000000000000000000000000000";
        public static OrderSelectionQuote QuoteAccept(OrderContext context, OrderState before, string id)
        {
            Guard(context, before); var order = OrderCatalog.Get(id);
            if (before.ActiveOrderId == id) throw new InvalidOperationException("This instance is already active; reselecting cannot reset it.");
            if (before.Ready) throw new InvalidOperationException("Claim the ready reward or explicitly cancel/forfeit it first.");
            return new OrderSelectionQuote(order, context, before);
        }
        public static OrderCancelQuote QuoteCancel(OrderContext context, OrderState before)
        {
            Guard(context, before);
            if (before.ActiveOrderId == null) throw new InvalidOperationException("No active order to cancel.");
            return new OrderCancelQuote(context, before);
        }
        public static OrderProposal PrepareAccept(OrderContext context, OrderState before, OrderSelectionQuote quote, long sequence, long wallet)
        {
            if (quote == null || !quote.Context.Same(context) || !quote.Before.Same(before)) throw new ArgumentException("Stale or conflicting selection quote.");
            return Prepare(OrderAction.Accept, sequence, quote.Order.Id, before.InstanceSequence, context, before, wallet, null);
        }
        public static OrderProposal PrepareCancel(OrderContext context, OrderState before, OrderCancelQuote quote, long sequence, long wallet)
        {
            if (quote == null || !quote.Context.Same(context) || !quote.Before.Same(before)) throw new ArgumentException("Stale or conflicting cancel quote.");
            return Prepare(OrderAction.Cancel, sequence, before.ActiveOrderId, before.InstanceSequence, context, before, wallet, null);
        }
        public static OrderProposal PrepareObserve(OrderContext context, OrderState before, OrderEvent sourceEvent, long sequence, long wallet)
        { return Prepare(OrderAction.Observe, sequence, null, before?.InstanceSequence ?? 0, context, before, wallet, sourceEvent); }
        public static OrderProposal PrepareClaim(OrderContext context, OrderState before, string id, long expectedInstanceSequence, long sequence, long wallet)
        { return Prepare(OrderAction.Claim, sequence, id, expectedInstanceSequence, context, before, wallet, null); }

        // Validates the frozen operation against this pinned revision, not a live wallet or
        // a newly selected catalog. Core additionally binds its full journal sequence/state.
        public static OrderProposal RestoreProposal(int formatVersion, string ruleRevision, OrderAction action, long sequence, string id,
            long expectedInstanceSequence, OrderContext context, OrderState before, OrderState after, long coinsBefore, long coinsAfter,
            long rewardCoins, OrderEvent sourceEvent, string key, bool noOp)
        {
            if (formatVersion != 1 || ruleRevision != OrderIds.RevisionFor(id, before, after)) throw new ArgumentException("Unsupported frozen order rules/format.");
            var expected = Prepare(action, sequence, id, expectedInstanceSequence, context, before, coinsBefore, sourceEvent);
            if (!expected.After.Same(after) || expected.CoinsAfter != coinsAfter || expected.RewardCoins != rewardCoins || expected.Key != key || expected.NoOp != noOp)
                throw new ArgumentException("Frozen order result disagrees with its complete pinned inputs.");
            return expected;
        }
        // This returns new values only. It neither writes a save nor owns the global queue.
        public static OrderApplyResult Apply(OrderContext context, OrderState liveState, long liveWallet, OrderProposal proposal)
        {
            Guard(context, liveState);
            if (proposal == null || liveWallet < 0 || !proposal.Context.Same(context)) throw new ArgumentException("Invalid live order application context.");
            if (proposal.NoOp) return new OrderApplyResult(liveState, liveWallet, false);
            if (liveState.LastCommandSequence == proposal.Sequence)
            {
                if (liveState.LastOperationKey != proposal.Key || !liveState.Same(proposal.After)) throw new ArgumentException("Conflicting order replay checkpoint.");
                // Interleaved non-order credits remain untouched on duplicate application.
                return new OrderApplyResult(liveState, liveWallet, false);
            }
            if (liveState.LastCommandSequence > proposal.Sequence || !liveState.Same(proposal.Before) || liveWallet != proposal.CoinsBefore)
                throw new ArgumentException("Stale or conflicting frozen order application.");
            return new OrderApplyResult(proposal.After, proposal.CoinsAfter, true);
        }
        static OrderProposal Prepare(OrderAction action, long sequence, string id, long expectedInstance, OrderContext context,
            OrderState before, long wallet, OrderEvent sourceEvent)
        {
            Guard(context, before);
            if (sequence < 1 || sequence > OrderState.MaximumSequence || wallet < 0 || expectedInstance < 0 || expectedInstance > OrderState.MaximumSequence ||
                !Enum.IsDefined(typeof(OrderAction), action)) throw new ArgumentException("Invalid operation identity/wallet/action.");
            if (action != OrderAction.Observe && sourceEvent != null) throw new ArgumentException("Only foreground observe takes an event.");
            if (action == OrderAction.Observe)
            {
                if (sourceEvent == null || sourceEvent.OperationSequence != sequence || id != null || expectedInstance != before.InstanceSequence)
                    throw new ArgumentException("Order progress must share its source spin's exact reserved operation sequence.");
                if (sequence <= before.Cursor)
                {
                    if (sequence < before.Cursor || before.LastEvent == null) throw new ArgumentException("Stale event has no retained identity; the global journal must suppress it.");
                    if (!before.LastEvent.Same(sourceEvent)) throw new ArgumentException("Conflicting same-sequence foreground event.");
                    return Proposal(action, sequence, id, expectedInstance, context, before, before, wallet, wallet, 0, sourceEvent, true);
                }
                if (sourceEvent.ExpectedCursor != before.Cursor) throw new ArgumentException("Missing/out-of-order foreground event cursor.");
            }
            else
            {
                OrderCatalog.Get(id);
                var claim = before.LatestClaim;
                if (action == OrderAction.Claim && claim != null && claim.OrderId == id && claim.InstanceSequence == expectedInstance && claim.ClaimSequence == sequence)
                    return Proposal(action, sequence, id, expectedInstance, context, before, before, wallet, wallet, 0, null, true);
            }
            if (sequence <= before.LastCommandSequence) throw new ArgumentException("Stale order command sequence.");
            long instance = before.InstanceSequence, cursor = before.Cursor, reward = 0;
            string active = before.ActiveOrderId; int progress = before.Progress;
            var lastEvent = before.LastEvent; var latestClaim = before.LatestClaim;
            switch (action)
            {
                case OrderAction.Accept:
                    if (expectedInstance != before.InstanceSequence) throw new ArgumentException("Stale acceptance instance.");
                    QuoteAccept(context, before, id);
                    instance = sequence; active = id; progress = 0; cursor = sequence; lastEvent = null;
                    break;
                case OrderAction.Cancel:
                    if (active == null || active != id || instance != expectedInstance) throw new InvalidOperationException("Stale or absent cancel target.");
                    active = null; progress = 0; cursor = sequence; lastEvent = null;
                    break;
                case OrderAction.Observe:
                    cursor = sequence; lastEvent = sourceEvent;
                    if (active != null)
                    {
                        var order = OrderCatalog.Get(active);
                        int delta = ProgressDelta(order.Metric, sourceEvent);
                        progress = Math.Min(order.Target, checked(progress + delta));
                    }
                    break;
                case OrderAction.Claim:
                    if (active != id || instance != expectedInstance || !before.Ready) throw new InvalidOperationException("Order instance is absent, stale or not ready.");
                    reward = OrderCatalog.Get(id).RewardCoins;
                    if (wallet > long.MaxValue - reward) throw new OverflowException("Order reward would overflow wallet.");
                    latestClaim = new OrderClaimRecord(id, instance, sequence, reward);
                    active = null; progress = 0;
                    break;
            }
            var provisional = new OrderState(instance, active, progress, cursor, lastEvent, sequence, PlaceholderKey, latestClaim);
            var frozen = Proposal(action, sequence, id, expectedInstance, context, before, provisional, wallet, checked(wallet + reward), reward, sourceEvent, false);
            var after = new OrderState(instance, active, progress, cursor, lastEvent, sequence, frozen.Key, latestClaim);
            return new OrderProposal(action, sequence, id, expectedInstance, context, before, after, wallet, checked(wallet + reward), reward, sourceEvent, frozen.Key, false);
        }
        static OrderProposal Proposal(OrderAction action, long sequence, string id, long instance, OrderContext context, OrderState before,
            OrderState after, long coinsBefore, long coinsAfter, long reward, OrderEvent sourceEvent, bool noOp)
        {
            var data = new StringBuilder(OrderIds.RevisionFor(id, before, after)).Append('|').Append(OrderStateCodec.N((int)action)).Append('|').Append(OrderStateCodec.N(sequence)).Append('|')
                .Append(id ?? "-").Append('|').Append(OrderStateCodec.N(instance)).Append('|').Append(OrderStateCodec.Encode(before)).Append('|').Append(OrderStateCodec.Encode(after)).Append('|')
                .Append(OrderStateCodec.N(coinsBefore)).Append('|').Append(OrderStateCodec.N(coinsAfter)).Append('|').Append(OrderStateCodec.N(reward)).Append('|')
                .Append(OrderStateCodec.EventToken(sourceEvent)).Append('|').Append(noOp ? '1' : '0').Append('|').Append(context.Stages.EraId);
            foreach (var stage in context.Stages.Entries) data.Append('|').Append(stage.FamilyId).Append(':').Append(OrderStateCodec.N(stage.AbsoluteLevel));
            byte[] digest; using (var sha = SHA256.Create()) digest = sha.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()));
            var key = new StringBuilder(64); foreach (byte value in digest) key.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return new OrderProposal(action, sequence, id, instance, context, before, after, coinsBefore, coinsAfter, reward, sourceEvent, key.ToString(), noOp);
        }
        static int ProgressDelta(OrderMetric metric, OrderEvent sourceEvent)
        {
            switch (metric)
            {
                case OrderMetric.TrashAppearances: return sourceEvent.Outcome.Count(JunkToNeon.Contracts.SymbolIds.Trash);
                case OrderMetric.CompletedDeliveries: return sourceEvent.CompletedDeliveries;
                case OrderMetric.PotatoAppearances: return sourceEvent.Outcome.Count(JunkToNeon.Contracts.SymbolIds.Potato);
                case OrderMetric.BoxAppearances: return sourceEvent.Outcome.Count(JunkToNeon.Contracts.SymbolIds.Box);
                default: throw new ArgumentException("Unknown order metric.");
            }
        }
        static void Guard(OrderContext context, OrderState state)
        {
            if (context == null || state == null) throw new ArgumentNullException(context == null ? nameof(context) : nameof(state));
            if (!context.Unlocked) throw new InvalidOperationException("Orders require actual completion of all six Workshop Yard objects.");
        }
    }
}
