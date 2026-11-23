using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal OptionalAchievementSnapshot ReadOptionalAchievements()
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Optional achievements require a confirmed healthy snapshot");
                return OptionalAchievements.Project(ChapterEconomy.Project(state),JourneyEconomy.Project(state));
            }
        }
    }
    public sealed partial class GameSession
    {
        public OptionalAchievementSnapshot OptionalAchievements { get { return store.ReadOptionalAchievements(); } }
    }
}
