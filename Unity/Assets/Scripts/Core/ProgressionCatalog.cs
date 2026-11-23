using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Effects;

namespace JunkToNeon.Core
{
    // The active four-family adapter. Proposed eras/cart/power and the optional
    // slot upgrade are deliberately absent from this completion predicate.
    public static class ProgressionCatalog
    {
        static readonly CatalogDefinition definition = CreateDefinition();
        static readonly ReadOnlyCollection<string> requiredAssetKeys = AssetKeys(definition);
        // Declared keys establish structural validity only. The runtime/Editor
        // separately supplies an observed registry to ValidateResources.
        static readonly ValidatedCatalog catalog = new ValidatedCatalog(definition, requiredAssetKeys);
        static readonly EffectRules rules = ReadEffectRules();

        public static IReadOnlyList<string> RequiredAssetKeys { get { return requiredAssetKeys; } }
        public static EffectRules Rules { get { return rules; } }

        public static string Id(int target)
        {
            var id = TryId(target);
            return id ?? throw new ArgumentOutOfRangeException(nameof(target), "Unknown project target");
        }

        static string TryId(int target)
        {
            switch (target)
            {
                case 1: return GameIds.HouseRepair;
                case 2: return GameIds.HouseImprove;
                case EconomyRules.WorkshopTarget: return GameIds.WorkshopBuild;
                case EconomyRules.RecyclerTarget: return GameIds.RecyclerBuild;
                case EconomyRules.PigeonLoftTarget: return GameIds.PigeonLoftBuild;
                default: return null;
            }
        }

        public static long Price(int target)
        {
            UpgradeDefinition upgrade;
            if (!catalog.TryGetUpgrade(Id(target), out upgrade) || !upgrade.Enabled || !upgrade.Price.HasValue)
                throw new InvalidOperationException("Active project is missing its price");
            return upgrade.Price.Value;
        }

        public static bool Available(int target, int houseStage, int workshopBuilt, int recyclerBuilt, int pigeonLoftBuilt)
        {
            var completed = Completed(houseStage, workshopBuilt, recyclerBuilt, pigeonLoftBuilt);
            var id = TryId(target);
            if (id == null) return false;
            foreach (var available in catalog.AvailableUpgrades(completed))
                if (available == id) return true;
            return false;
        }

        public static bool Complete(int houseStage, int workshopBuilt, int recyclerBuilt, int pigeonLoftBuilt)
        {
            return catalog.IsChapterComplete(Completed(houseStage, workshopBuilt, recyclerBuilt, pigeonLoftBuilt));
        }

        public static IReadOnlyList<string> Completed(int houseStage, int workshopBuilt, int recyclerBuilt, int pigeonLoftBuilt)
        {
            var legacy = LegacyProgress.Project(houseStage, workshopBuilt);
            if (recyclerBuilt < 0 || recyclerBuilt > 1 || pigeonLoftBuilt < 0 || pigeonLoftBuilt > 1 ||
                (houseStage == 0 && (recyclerBuilt != 0 || pigeonLoftBuilt != 0)))
                throw new ArgumentException("Invalid active project progress");
            var result = new List<string>(legacy.CompletedUpgradeIds);
            if (recyclerBuilt == 1) result.Add(GameIds.RecyclerBuild);
            if (pigeonLoftBuilt == 1) result.Add(GameIds.PigeonLoftBuild);
            return result.AsReadOnly();
        }

        public static void ValidateResources(IEnumerable<string> actualKeys)
        {
            if (actualKeys == null) throw new ArgumentNullException(nameof(actualKeys));
            // A successful call establishes registry membership, not image quality
            // or visual correctness. The caller owns observing the actual resources.
            new ValidatedCatalog(definition, actualKeys);
        }

        static CatalogDefinition CreateDefinition()
        {
            var baseline = CatalogData.CurrentPrototype(EconomyRules.Price(0), EconomyRules.Price(1),
                EconomyRules.WorkshopCost, EconomyRules.WorkshopBonus);
            var families = new List<FamilyDefinition>(baseline.Families)
            {
                new FamilyDefinition(ContentIds.Recycler, "Recycler"),
                new FamilyDefinition(ContentIds.PigeonLoft, "Pigeon loft")
            };
            var stages = new List<StageDefinition>(baseline.Stages);
            foreach (var family in new[] { ContentIds.Recycler, ContentIds.PigeonLoft })
                for (int level = 0; level <= 1; level++)
                    stages.Add(new StageDefinition(ContentIds.Stage(family, level), family, level,
                        fallback: FallbackVisual.TextAndBlock));
            var provisional = EffectRules.Provisional;
            var upgrades = new List<UpgradeDefinition>(baseline.Upgrades)
            {
                new UpgradeDefinition(GameIds.RecyclerBuild, ContentIds.Stage(ContentIds.Recycler, 0),
                    ContentIds.Stage(ContentIds.Recycler, 1), EconomyRules.RecyclerCost, true, true,
                    new[] { GameIds.HouseRepair }, new[] {
                        new ContentEffect(GameIds.RecyclerTrashCoins, ContentEffectKind.TrashSymbolCoins,
                            provisional.RecyclerCoinsPerTrash, true) }),
                new UpgradeDefinition(GameIds.PigeonLoftBuild, ContentIds.Stage(ContentIds.PigeonLoft, 0),
                    ContentIds.Stage(ContentIds.PigeonLoft, 1), EconomyRules.PigeonLoftCost, true, true,
                    new[] { GameIds.HouseRepair }, new[] {
                        new ContentEffect(GameIds.PigeonDeliveryCoins, ContentEffectKind.PigeonDeliveryCoins,
                            provisional.CoinsPerDelivery, true, provisional.PigeonsPerDelivery) })
            };
            return new CatalogDefinition("prototype.wave1", false, families, stages, upgrades,
                new[] { GameIds.HouseImprove, GameIds.WorkshopBuild, GameIds.RecyclerBuild, GameIds.PigeonLoftBuild });
        }

        static ReadOnlyCollection<string> AssetKeys(CatalogDefinition data)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in data.Stages)
                if (!string.IsNullOrEmpty(stage.AssetKey) && seen.Add(stage.AssetKey)) keys.Add(stage.AssetKey);
            return keys.AsReadOnly();
        }

        static EffectRules ReadEffectRules()
        {
            var recycler = Effect(GameIds.RecyclerBuild, GameIds.RecyclerTrashCoins, ContentEffectKind.TrashSymbolCoins);
            var delivery = Effect(GameIds.PigeonLoftBuild, GameIds.PigeonDeliveryCoins, ContentEffectKind.PigeonDeliveryCoins);
            if (!recycler.Amount.HasValue || !delivery.Amount.HasValue || !delivery.Threshold.HasValue)
                throw new InvalidOperationException("Active effect is not configured");
            return new EffectRules(recycler.Amount.Value, delivery.Threshold.Value, delivery.Amount.Value);
        }

        static ContentEffect Effect(string upgradeId, string effectId, ContentEffectKind kind)
        {
            UpgradeDefinition upgrade;
            if (catalog.TryGetUpgrade(upgradeId, out upgrade) && upgrade.Enabled)
                foreach (var effect in upgrade.Effects)
                    if (effect.EffectId == effectId && effect.Kind == kind) return effect;
            throw new InvalidOperationException("Active catalog lacks its approved effect");
        }
    }
}
