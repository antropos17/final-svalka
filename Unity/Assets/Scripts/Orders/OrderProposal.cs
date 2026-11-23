namespace JunkToNeon.Orders
{
    public sealed class OrderProposal
    {
        public int FormatVersion { get { return 1; } }
        public string RuleRevision { get { return OrderIds.RevisionFor(OrderId, Before, After); } }
        public OrderAction Action { get; }
        public long Sequence { get; }
        public string OrderId { get; }
        public long ExpectedInstanceSequence { get; }
        public OrderContext Context { get; }
        public OrderState Before { get; }
        public OrderState After { get; }
        public long CoinsBefore { get; }
        public long CoinsAfter { get; }
        public long RewardCoins { get; }
        public OrderEvent Event { get; }
        public string Key { get; }
        public bool NoOp { get; }
        internal OrderProposal(OrderAction action, long sequence, string orderId, long expectedInstanceSequence, OrderContext context,
            OrderState before, OrderState after, long coinsBefore, long coinsAfter, long rewardCoins, OrderEvent sourceEvent, string key, bool noOp)
        {
            Action = action; Sequence = sequence; OrderId = orderId; ExpectedInstanceSequence = expectedInstanceSequence; Context = context;
            Before = before; After = after; CoinsBefore = coinsBefore; CoinsAfter = coinsAfter; RewardCoins = rewardCoins; Event = sourceEvent; Key = key; NoOp = noOp;
        }
    }
}
