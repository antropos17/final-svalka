using System;
using JunkToNeon.Core;
using JunkToNeon.Guidance;

namespace JunkToNeon
{
    // One synchronous render's live snapshot, never the pre-spin display state.
    // Retain only immutable projections; do not cache this view across renders.
    internal readonly struct JtnJourneyView
    {
        public GuideState Progress { get; }
        public GuideEvaluation Evaluation { get; }
        public bool TransitionPending { get; }

        JtnJourneyView(GuideState progress, GuideEvaluation evaluation, bool transitionPending)
        { Progress = progress; Evaluation = evaluation; TransitionPending = transitionPending; }

        public static JtnJourneyView FromSnapshot(ModelState snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var chapter = ChapterEconomy.Project(snapshot);
            var journey = JourneyEconomy.Project(snapshot);
            return new JtnJourneyView(journey.Guidance,
                GuideFlow.Query(journey.Guidance, JourneyEconomy.GuideContext(chapter, journey)),
                chapter.TransitionPending);
        }
    }
}
