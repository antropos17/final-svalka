using System;
using JunkToNeon.Contracts;

namespace JunkToNeon.Upgrades
{
    // Pricing revisions are independent of the unchanged chapter/effect rule revision.
    public static class SkillPricing
    {
        public const string LegacyRevision = "skill.prices.v1";
        public const string RevisionV1 = LegacyRevision, RevisionV2 = "skill.prices.v2", RevisionV3 = "skill.prices.v3";
        public const string CurrentRevision = RevisionV3;
        public static bool IsKnown(string revision) => revision == LegacyRevision || revision == RevisionV2 || revision == RevisionV3;
        public static long Price(string nodeId, string revision)
        {
            if (!IsKnown(revision)) throw new ArgumentException("Unknown skill pricing revision", nameof(revision));
            bool old = revision == LegacyRevision;
            switch (nodeId)
            {
                case SkillIds.Universal1: return 120;
                case SkillIds.Universal2: return old ? 360 : 160;
                case SkillIds.Recycling1: return 160;
                case SkillIds.Recycling2: return old ? 480 : 90;
                case SkillIds.Delivery1: return old ? 180 : 60;
                case SkillIds.Delivery2: return old ? 540 : revision == RevisionV2 ? 5 : 20;
                default: throw new ArgumentException("Unknown skill node", nameof(nodeId));
            }
        }
    }
}
