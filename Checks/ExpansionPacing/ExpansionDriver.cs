using JunkToNeon.Core;

namespace ExpansionPacing
{
    // A frozen candidate-specific adapter may replace these no-op baseline hooks.
    // Never approximate production accrual with a separate economic model.
    internal class ExpansionDriver
    {
        public static ExpansionDriver Create()
        {
#if EXPANSION_CANDIDATE
            return new CandidateExpansionDriver();
#else
            return new ExpansionDriver();
#endif
        }
        public virtual string Label => "baseline-no-resource-shop";
        public virtual void BeforeSpin(GameSession game, RunResult result) { }
        public virtual void AfterSpin(GameSession game, RunResult result) { }
        public virtual void Decisions(GameSession game, RunResult result, long at) { }
        public virtual object Snapshot(GameSession game) => new
        {
            available = false,
            firstUsefulShopPurchaseSpin = (int?)null,
            parts = (long?)null, potatoes = (long?)null, energy = (long?)null,
            reason = "This frozen baseline has optional skills and symbol orders; no earned-resource shop, energy or bonus reel exists."
        };
    }
}
