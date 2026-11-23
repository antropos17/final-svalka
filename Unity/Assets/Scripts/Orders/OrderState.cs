using System;

namespace JunkToNeon.Orders
{
    public sealed class OrderClaimRecord
    {
        public string OrderId { get; }
        public long InstanceSequence { get; }
        public long ClaimSequence { get; }
        public long RewardCoins { get; }
        public OrderClaimRecord(string orderId, long instanceSequence, long claimSequence, long rewardCoins)
        {
            if (instanceSequence < 1 || claimSequence <= instanceSequence || claimSequence > OrderState.MaximumSequence || rewardCoins != OrderCatalog.Get(orderId).RewardCoins)
                throw new ArgumentException("Invalid latest claim identity/reward.");
            OrderId = orderId; InstanceSequence = instanceSequence; ClaimSequence = claimSequence; RewardCoins = rewardCoins;
        }
    }
    // One bounded checkpoint, not a growing event/reward history. GameStore owns persistence.
    public sealed class OrderState
    {
        public const long MaximumSequence = (long.MaxValue - 2) / 2;
        public long InstanceSequence { get; }
        public string ActiveOrderId { get; }
        public int Progress { get; }
        public long Cursor { get; }
        public OrderEvent LastEvent { get; }
        public long LastCommandSequence { get; }
        public string LastOperationKey { get; }
        public OrderClaimRecord LatestClaim { get; }
        public bool Ready { get { return ActiveOrderId != null && Progress == OrderCatalog.Get(ActiveOrderId).Target; } }
        public static OrderState Empty { get; } = new OrderState(0, null, 0, 0, null, 0, null, null);
        public OrderState(long instanceSequence, string activeOrderId, int progress, long cursor,
            OrderEvent lastEvent, long lastCommandSequence, string lastOperationKey, OrderClaimRecord latestClaim)
        {
            if (instanceSequence < 0 || instanceSequence > MaximumSequence || cursor < 0 || cursor > MaximumSequence ||
                lastCommandSequence < 0 || lastCommandSequence > MaximumSequence || cursor > lastCommandSequence || cursor < instanceSequence || instanceSequence > lastCommandSequence ||
                (lastCommandSequence == 0 ? lastOperationKey != null : !IsKey(lastOperationKey))) throw new ArgumentException("Invalid order checkpoint identity.");
            if (activeOrderId == null ? progress != 0 : instanceSequence == 0 || progress < 0 || progress > OrderCatalog.Get(activeOrderId).Target)
                throw new ArgumentException("Invalid active order progress.");
            if (latestClaim != null && (latestClaim.InstanceSequence > instanceSequence || latestClaim.ClaimSequence > lastCommandSequence ||
                latestClaim.InstanceSequence < instanceSequence && latestClaim.ClaimSequence >= instanceSequence ||
                activeOrderId != null && latestClaim.InstanceSequence >= instanceSequence)) throw new ArgumentException("Invalid latest claim checkpoint.");
            if (lastEvent != null && (lastEvent.OperationSequence != cursor || activeOrderId != null && lastEvent.ExpectedCursor < instanceSequence))
                throw new ArgumentException("Last event disagrees with cursor/acceptance.");
            if (progress > 0 && lastEvent == null) throw new ArgumentException("Progress lacks a foreground event checkpoint.");
            if (lastCommandSequence == 0 && (instanceSequence != 0 || activeOrderId != null || latestClaim != null || lastEvent != null))
                throw new ArgumentException("Empty state contains history.");
            InstanceSequence = instanceSequence; ActiveOrderId = activeOrderId; Progress = progress; Cursor = cursor;
            LastEvent = lastEvent; LastCommandSequence = lastCommandSequence; LastOperationKey = lastOperationKey; LatestClaim = latestClaim;
        }
        internal static bool IsKey(string key)
        {
            if (key == null || key.Length != 64) return false;
            foreach (char c in key) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
        internal bool Same(OrderState other)
        {
            return other != null && InstanceSequence == other.InstanceSequence && ActiveOrderId == other.ActiveOrderId && Progress == other.Progress &&
                Cursor == other.Cursor && LastCommandSequence == other.LastCommandSequence && LastOperationKey == other.LastOperationKey &&
                (LastEvent == null ? other.LastEvent == null : LastEvent.Same(other.LastEvent)) &&
                (LatestClaim == null ? other.LatestClaim == null : other.LatestClaim != null && LatestClaim.OrderId == other.LatestClaim.OrderId &&
                    LatestClaim.InstanceSequence == other.LatestClaim.InstanceSequence && LatestClaim.ClaimSequence == other.LatestClaim.ClaimSequence && LatestClaim.RewardCoins == other.LatestClaim.RewardCoins);
        }
    }
    public sealed class OrderSelectionQuote
    {
        public OrderDefinition Order { get; }
        public OrderContext Context { get; }
        public OrderState Before { get; }
        public int DiscardedProgress { get { return Before.Progress; } }
        internal OrderSelectionQuote(OrderDefinition order, OrderContext context, OrderState before)
        { Order = order; Context = context; Before = before; }
    }
    public sealed class OrderCancelQuote
    {
        public OrderContext Context { get; }
        public OrderState Before { get; }
        public int DiscardedProgress { get { return Before.Progress; } }
        public long ForfeitedRewardCoins { get { return Before.Ready ? OrderCatalog.Get(Before.ActiveOrderId).RewardCoins : 0; } }
        internal OrderCancelQuote(OrderContext context, OrderState before) { Context = context; Before = before; }
    }
    public sealed class OrderApplyResult
    {
        public OrderState State { get; }
        public long Coins { get; }
        public bool Applied { get; }
        internal OrderApplyResult(OrderState state, long coins, bool applied) { State = state; Coins = coins; Applied = applied; }
    }
}
