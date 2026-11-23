using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal ChapterMilestoneSnapshot ReadMilestones()
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Milestones require a confirmed healthy snapshot");
                return ChapterMilestones.Project(ChapterEconomy.Project(state));
            }
        }
    }
    public sealed partial class GameSession
    {
        public ChapterMilestoneSnapshot Milestones { get { return store.ReadMilestones(); } }
    }
}
