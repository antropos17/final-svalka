using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JunkToNeon.Core
{
    public sealed class OptionalAchievementDefinition
    {
        public string Id { get; }
        internal OptionalAchievementDefinition(string id) { Id=id; }
    }

    public sealed class OptionalAchievementProgress
    {
        public OptionalAchievementDefinition Definition { get; }
        public bool Reached { get; }
        internal OptionalAchievementProgress(OptionalAchievementDefinition definition,bool reached)
        { Definition=definition;Reached=reached; }
    }

    // Current saved facts only. This is separate from the four chapter milestones.
    public sealed class OptionalAchievementSnapshot
    {
        public ReadOnlyCollection<OptionalAchievementProgress> Achievements { get; }
        public int ReachedCount { get; }
        internal OptionalAchievementSnapshot(OptionalAchievementProgress[] achievements)
        {
            Achievements=Array.AsReadOnly(achievements);
            int reached=0;foreach(var achievement in achievements)if(achievement.Reached)reached++;ReachedCount=reached;
        }
    }

    public static class OptionalAchievements
    {
        public const string AllSkillsOwned="optional.all-skills-owned", RetainedOrderClaim="optional.retained-order-claim";
        static readonly ReadOnlyCollection<OptionalAchievementDefinition> catalog=Array.AsReadOnly(new[] {
            new OptionalAchievementDefinition(AllSkillsOwned),new OptionalAchievementDefinition(RetainedOrderClaim)
        });
        public static IReadOnlyList<OptionalAchievementDefinition> Catalog { get { return catalog; } }

        // The two immutable aggregates must come from the same validated snapshot.
        public static OptionalAchievementSnapshot Project(ChapterState chapter,JourneyState journey)
        {
            if(chapter==null)throw new ArgumentNullException(nameof(chapter));
            if(journey==null)throw new ArgumentNullException(nameof(journey));
            var context=JourneyEconomy.GuideContext(chapter,journey);
            bool claim=journey.Orders.LatestClaim!=null;
            if(claim&&!context.ChapterComplete)throw new ArgumentException("A retained order claim requires the completed chapter");
            return new OptionalAchievementSnapshot(new[] {
                new OptionalAchievementProgress(catalog[0],chapter.Skills.Receipts.Count==6),
                new OptionalAchievementProgress(catalog[1],claim)
            });
        }
    }
}
