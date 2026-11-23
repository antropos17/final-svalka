using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Contracts;

namespace JunkToNeon.Orders
{
    public enum OrderMetric { TrashAppearances = 0, CompletedDeliveries = 1, PotatoAppearances = 2, BoxAppearances = 3 }
    public enum OrderAction { Accept, Cancel, Observe, Claim }
    public static class OrderIds
    {
        public const string TrashSort = "order.sort-salvage", PigeonDeliveries = "order.neighborhood-supply";
        public const string GatherPotatoes = "order.gather-potatoes", PackBoxes = "order.pack-boxes";
        public const string RuleRevision = "orders.rules.v1";
        public const string VarietyRuleRevision = "orders.rules.v2";

        // V1 remains the exact original two-ID protocol. Only operations whose
        // identity/checkpoints contain an additive ID use V2; old hashes stay exact.
        internal static string RevisionFor(string id, OrderState before, OrderState after)
        {
            return IsVariety(id) || HasVariety(before) || HasVariety(after) ? VarietyRuleRevision : RuleRevision;
        }
        static bool IsVariety(string id) { return id == GatherPotatoes || id == PackBoxes; }
        static bool HasVariety(OrderState state)
        { return state != null && (IsVariety(state.ActiveOrderId) || IsVariety(state.LatestClaim?.OrderId)); }
    }
    public sealed class OrderDefinition
    {
        public string Id { get; }
        public string Label { get; }
        public OrderMetric Metric { get; }
        public int Target { get; }
        public long RewardCoins { get; }
        internal OrderDefinition(string id, string label, OrderMetric metric, int target, long reward)
        { Id = id; Label = label; Metric = metric; Target = target; RewardCoins = reward; }
    }
    public static class OrderCatalog
    {
        static readonly ReadOnlyCollection<OrderDefinition> choices = Array.AsReadOnly(new[] {
            new OrderDefinition(OrderIds.TrashSort, "Sort twelve trash finds", OrderMetric.TrashAppearances, 12, 120),
            new OrderDefinition(OrderIds.PigeonDeliveries, "Complete three pigeon deliveries", OrderMetric.CompletedDeliveries, 3, 180),
            new OrderDefinition(OrderIds.GatherPotatoes, "Gather twelve potato appearances", OrderMetric.PotatoAppearances, 12, 120),
            new OrderDefinition(OrderIds.PackBoxes, "Pack twelve box appearances", OrderMetric.BoxAppearances, 12, 120) });
        public static IReadOnlyList<OrderDefinition> Choices { get { return choices; } }
        public static OrderDefinition Get(string id)
        {
            foreach (var choice in choices) if (choice.Id == id) return choice;
            throw new ArgumentException("Unknown order ID.", nameof(id));
        }
    }
    public sealed class OrderContext
    {
        public FamilyStages Stages { get; }
        public OrderContext(FamilyStages stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            Stages = stages;
        }
        public bool Unlocked
        {
            get
            {
                if (Stages.EraId != EraIds.WorkshopYard) return false;
                foreach (var stage in Stages.Entries) if (stage.AbsoluteLevel != 5) return false;
                return true;
            }
        }
        internal bool Same(OrderContext other)
        {
            if (other == null || Stages.EraId != other.Stages.EraId) return false;
            foreach (var stage in Stages.Entries) if (other.Stages.AbsoluteLevel(stage.FamilyId) != stage.AbsoluteLevel) return false;
            return true;
        }
    }
    // Source facts, not inferred from coin amount or a presentation animation. The Core
    // owner obtains them from its validated frozen foreground spin/effects operation and
    // commits the order delta in THAT SAME transaction. Offline/animation are not sources.
    public sealed class OrderEvent
    {
        public long OperationSequence { get; }
        public long ExpectedCursor { get; }
        public SpinOutcome Outcome { get; }
        public int CompletedDeliveries { get; }
        public OrderEvent(long operationSequence, long expectedCursor, SpinOutcome outcome, int completedDeliveries)
        {
            if (operationSequence < 1 || operationSequence > OrderState.MaximumSequence || expectedCursor < 0 || expectedCursor >= operationSequence ||
                completedDeliveries < 0 || completedDeliveries > 1 || outcome.Count(SymbolIds.Pigeon) == 0 && completedDeliveries != 0 ||
                outcome.Count(SymbolIds.Pigeon) == 3 && completedDeliveries != 1)
                throw new ArgumentException("Invalid frozen foreground event.");
            OperationSequence = operationSequence; ExpectedCursor = expectedCursor; Outcome = outcome; CompletedDeliveries = completedDeliveries;
        }
        internal bool Same(OrderEvent other)
        { return other != null && OperationSequence == other.OperationSequence && ExpectedCursor == other.ExpectedCursor && Outcome.Left == other.Outcome.Left && Outcome.Middle == other.Outcome.Middle && Outcome.Right == other.Outcome.Right && CompletedDeliveries == other.CompletedDeliveries; }
    }
}
