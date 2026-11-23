using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Content;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    // Format2 uses this pinned data revision. EconomyRules/ProgressionCatalog remain
    // the literal r7 rules for pending format1 recovery, not the active era2 catalog.
    public static class ChapterCatalog
    {
        static readonly ChapterDefinition definition = ChapterData.TwoEras();
        static readonly ReadOnlyCollection<string> assetKeys = FindAssetKeys();
        static readonly ChapterRules rules = new ChapterRules(definition, assetKeys);
        public static ChapterRules Rules { get { return rules; } }
        public static IReadOnlyList<string> RequiredAssetKeys { get { return assetKeys; } }
        public static void ValidateResources(IEnumerable<string> actualKeys) { new ChapterRules(definition, actualKeys); }
        public static ChapterLegacyProjection ProjectR7(ModelState state)
        {
            var wave = WaveProgress.Of(state);
            return rules.ProjectR7(state.houseStage, state.workshopBuilt, wave.RecyclerBuilt, wave.PigeonLoftBuilt, state.selectedStage, state.contentVersion);
        }
        public static string LegacyProject(int target)
        {
            switch (target)
            {
                case 0: return null;
                case 1: return GameIds.HouseRepair;
                case 2: return GameIds.HouseImprove;
                case 3: return GameIds.WorkshopBuild;
                case 4: return GameIds.RecyclerBuild;
                case 5: return GameIds.PigeonLoftBuild;
                default: throw new ArgumentOutOfRangeException(nameof(target));
            }
        }
        public static int LegacyTarget(string eraId, string projectId)
        {
            if (eraId != EraIds.Junkyard || projectId == null) return 0;
            for (int target = 1; target <= 5; target++) if (LegacyProject(target) == projectId) return target;
            return 0;
        }
        static ReadOnlyCollection<string> FindAssetKeys()
        {
            var keys = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var era in definition.Eras) foreach (var stage in era.Catalog.Stages)
                if (!string.IsNullOrEmpty(stage.AssetKey) && seen.Add(stage.AssetKey)) keys.Add(stage.AssetKey);
            return keys.AsReadOnly();
        }
    }
}
