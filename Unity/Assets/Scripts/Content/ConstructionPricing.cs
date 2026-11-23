using System;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    // Price history is independent of chapter effects and their persistent source IDs.
    // The content catalog keeps literal V1 prices for old saves and frozen operations.
    public static class ConstructionPricing
    {
        public const string LegacyRevision = "construction.prices.v1";
        public const string RevisionV2 = "construction.prices.v2";
        public const string RevisionV3 = "construction.prices.v3";
        public const string CurrentRevision = RevisionV3;
        static readonly ChapterDefinition legacyCatalog = ChapterData.TwoEras();

        public static bool IsKnown(string revision)
        { return revision == LegacyRevision || revision == RevisionV2 || revision == RevisionV3; }

        // Historical validation only. New selections still use CurrentRevision.
        public static bool CanAdvance(string fromRevision, string toRevision)
        {
            if (!IsKnown(fromRevision) || !IsKnown(toRevision)) return false;
            int from = fromRevision == LegacyRevision ? 1 : fromRevision == RevisionV2 ? 2 : 3;
            int to = toRevision == LegacyRevision ? 1 : toRevision == RevisionV2 ? 2 : 3;
            return from <= to;
        }

        public static long Price(string projectId, string revision)
        {
            if (!IsKnown(revision)) throw new ArgumentException("Unknown construction price revision", nameof(revision));
            foreach (var era in legacyCatalog.Eras) foreach (var upgrade in era.Catalog.Upgrades)
                if (upgrade.Id == projectId && upgrade.Price.HasValue)
                {
                    if (revision == RevisionV3)
                    {
                        if (projectId == GameIds.HouseImprove) return 480;
                        if (projectId == "cart.build") return 140;
                        if (projectId == "power-source.build") return 190;
                    }
                    return projectId == GameIds.HouseImprove && revision != LegacyRevision ? 540 : upgrade.Price.Value;
                }
            throw new ArgumentException("Unknown construction project", nameof(projectId));
        }
    }
}
