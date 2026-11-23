using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public sealed class ChapterRules
    {
        public ChapterDefinition Definition { get; }
        readonly Dictionary<string, UpgradeDefinition> upgrades;
        readonly Dictionary<string, EraDefinition> upgradeEras;
        public ChapterRules(ChapterDefinition definition, IEnumerable<string> availableAssetKeys)
        {
            var issues = ChapterValidator.Validate(definition, availableAssetKeys);
            if (issues.Count != 0) throw new ArgumentException(string.Join("\n", issues.Select(i => i.ToString())), nameof(definition));
            Definition = definition;
            upgrades = definition.Eras.SelectMany(e => e.Catalog.Upgrades).ToDictionary(u => u.Id, StringComparer.Ordinal);
            upgradeEras = new Dictionary<string, EraDefinition>(StringComparer.Ordinal);
            foreach (var era in definition.Eras) foreach (var upgrade in era.Catalog.Upgrades) upgradeEras.Add(upgrade.Id, era);
        }
        public bool TryGetUpgrade(string id, out UpgradeDefinition upgrade)
        { upgrade = null; return id != null && upgrades.TryGetValue(id, out upgrade); }
        public void ValidateProgress(ChapterProgress progress) { Owned(progress); }
        public ReadOnlyCollection<string> AvailableUpgrades(ChapterProgress progress)
        {
            var owned = Owned(progress);
            return Current(progress).Catalog.Upgrades.Where(u => !owned.Contains(u.Id) && u.Prerequisites.All(owned.Contains)).Select(u => u.Id).ToList().AsReadOnly();
        }
        public bool CanUnlockWorkshopYard(ChapterProgress progress)
        {
            var owned = Owned(progress);
            return progress.EraId == EraIds.Junkyard && Definition.Eras[0].Catalog.CompletionUpgradeIds.All(owned.Contains);
        }
        public bool IsChapterComplete(ChapterProgress progress)
        {
            var owned = Owned(progress);
            return progress.EraId == EraIds.WorkshopYard && Definition.Eras[1].Catalog.CompletionUpgradeIds.All(owned.Contains);
        }
        public ReadOnlyCollection<string> RemainingEraRequirements(ChapterProgress progress)
        {
            var owned = Owned(progress);
            return Current(progress).Catalog.CompletionUpgradeIds.Where(id => !owned.Contains(id)).ToList().AsReadOnly();
        }
        public ChapterStageView FamilyStage(ChapterProgress progress, string family)
        {
            var owned = Owned(progress);
            return FamilyStageFromOwned(progress, family, owned);
        }
        ChapterStageView FamilyStageFromOwned(ChapterProgress progress, string family, HashSet<string> owned)
        {
            if (!FamilyIds.All.Contains(family)) throw new ArgumentException("Unknown family.", nameof(family));
            int level = owned.Contains(ChapterIds.Project(progress.EraId, family, 2)) ? 2 : owned.Contains(ChapterIds.Project(progress.EraId, family, 1)) ? 1 : 0;
            var stage = Current(progress).Catalog.Stages.Single(s => s.FamilyId == family && s.Level == level);
            return new ChapterStageView(progress.EraId, stage, (progress.EraId == EraIds.Junkyard ? 0 : 3) + level);
        }
        public FamilyStages Stages(ChapterProgress progress)
        {
            // This immutable progress is fully validated once for this projection.
            // The local set never survives the synchronous FamilyStages constructor.
            var owned = Owned(progress);
            return new FamilyStages(progress.EraId, FamilyIds.All.Select(id => FamilyStageFromOwned(progress, id, owned).Progress));
        }
        public ChapterProgress FromStages(FamilyStages stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var owned = new List<string>();
            if (stages.EraId == EraIds.WorkshopYard)
                foreach (string family in FamilyIds.All) for (int level = 1; level <= 2; level++) owned.Add(ChapterIds.Project(EraIds.Junkyard, family, level));
            foreach (string family in FamilyIds.All) for (int level = 1; level <= stages.LocalLevel(family); level++) owned.Add(ChapterIds.Project(stages.EraId, family, level));
            var result = new ChapterProgress(stages.EraId, owned); Owned(result); return result;
        }
        public ChapterSymbolModifiers SymbolModifiers(ChapterProgress progress)
        {
            return new ChapterSymbolModifiers(OwnedBenefits(progress).Where(e => e.Kind == ContentEffectKind.SymbolCoins)
                .Select(e => new SymbolCoinSource(e.EffectId, e.SymbolId.Value, e.Amount.Value)));
        }
        public void ValidateSymbolModifiers(ChapterProgress preCommandProgress, string ruleRevision, ChapterSymbolModifiers frozen)
        {
            if (ruleRevision != ChapterContentIds.RuleRevision) throw new ArgumentException("Unsupported chapter rule revision.", nameof(ruleRevision));
            if (frozen == null) throw new ArgumentNullException(nameof(frozen));
            var expected = SymbolModifiers(preCommandProgress);
            if (expected.Sources.Count != frozen.Sources.Count) throw new ArgumentException("Missing or unowned symbol sources.", nameof(frozen));
            for (int i = 0; i < expected.Sources.Count; i++)
            {
                var left = expected.Sources[i]; var right = frozen.Sources[i];
                if (left.SourceId != right.SourceId || left.SymbolId != right.SymbolId || left.CoinsPerSymbol != right.CoinsPerSymbol)
                    throw new ArgumentException("Symbol sources do not match registered pre-command ownership and pinned rules.", nameof(frozen));
            }
        }
        public ReadOnlyCollection<ContentEffect> OwnedBenefits(ChapterProgress progress)
        {
            var owned = Owned(progress);
            // These are source descriptors, not a receipt/contribution array. The Effects owner
            // aggregates SymbolCoins by symbol into at most four registered contributions.
            return Definition.Eras.SelectMany(e => e.Catalog.Upgrades).Where(u => owned.Contains(u.Id)).SelectMany(u => u.Effects).ToList().AsReadOnly();
        }
        public ChapterProposal ProposePurchase(ChapterProgress before, string upgradeId, long coins)
        {
            Owned(before);
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            UpgradeDefinition upgrade;
            if (upgradeId == null || !upgrades.TryGetValue(upgradeId, out upgrade)) throw new ArgumentException("Unknown upgrade.", nameof(upgradeId));
            if (!AvailableUpgrades(before).Contains(upgradeId)) throw new InvalidOperationException("Upgrade already owned or locked.");
            if (coins < upgrade.Price.Value) throw new InvalidOperationException("Insufficient coins.");
            var after = new ChapterProgress(before.EraId, before.CompletedUpgradeIds.Concat(new[] { upgradeId }));
            Owned(after);
            return new ChapterProposal(Definition.Id, upgradeId, before, after, coins, upgrade.Price.Value);
        }
        public ChapterProposal ProposeUnlockWorkshopYard(ChapterProgress before, long coins)
        {
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            if (!CanUnlockWorkshopYard(before)) throw new InvalidOperationException("Workshop Yard is locked or already unlocked.");
            var after = new ChapterProgress(EraIds.WorkshopYard, before.CompletedUpgradeIds);
            Owned(after);
            return new ChapterProposal(Definition.Id, ChapterIds.UnlockWorkshopYard, before, after, coins, 0);
        }
        public ChapterLegacyProjection ProjectR7(int houseStage, int workshopBuilt, int recyclerBuilt, int pigeonLoftBuilt,
            int selectedStage = 0, int legacyContentVersion = 1)
        {
            var legacy = LegacyProgress.Project(houseStage, workshopBuilt, 0, legacyContentVersion);
            if (recyclerBuilt < 0 || recyclerBuilt > 1 || pigeonLoftBuilt < 0 || pigeonLoftBuilt > 1 ||
                houseStage == 0 && (recyclerBuilt != 0 || pigeonLoftBuilt != 0)) throw new ArgumentException("Invalid r7 progress.");
            var owned = new List<string>(legacy.CompletedUpgradeIds);
            if (recyclerBuilt == 1) owned.Add(GameIds.RecyclerBuild);
            if (pigeonLoftBuilt == 1) owned.Add(GameIds.PigeonLoftBuild);
            var progress = new ChapterProgress(EraIds.Junkyard, owned); Owned(progress);
            string selected;
            switch (selectedStage)
            {
                case 0: selected = null; break;
                case 1: selected = GameIds.HouseRepair; break;
                case 2: selected = GameIds.HouseImprove; break;
                case 3: selected = GameIds.WorkshopBuild; break;
                case 4: selected = GameIds.RecyclerBuild; break;
                case 5: selected = GameIds.PigeonLoftBuild; break;
                default: throw new ArgumentException("Unknown r7 selected target.");
            }
            if (selected != null && !AvailableUpgrades(progress).Contains(selected)) throw new ArgumentException("Unavailable r7 selected target.");
            return new ChapterLegacyProjection(progress, selected);
        }
        EraDefinition Current(ChapterProgress progress) { return Definition.Eras[progress.EraId == EraIds.Junkyard ? 0 : 1]; }
        HashSet<string> Owned(ChapterProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (progress.EraId != EraIds.Junkyard && progress.EraId != EraIds.WorkshopYard || progress.CompletedUpgradeIds.Count > 24)
                throw new ArgumentException("Unsupported era or oversized progress.", nameof(progress));
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in progress.CompletedUpgradeIds)
            {
                if (id == null || !upgrades.ContainsKey(id) || !owned.Add(id)) throw new ArgumentException("Unknown or duplicate owned upgrade.", nameof(progress));
                if (progress.EraId == EraIds.Junkyard && upgradeEras[id].Id != EraIds.Junkyard) throw new ArgumentException("Future-era ownership before unlock.", nameof(progress));
            }
            foreach (var id in owned) if (!upgrades[id].Prerequisites.All(owned.Contains)) throw new ArgumentException("Owned upgrade lacks prerequisites.", nameof(progress));
            if (progress.EraId == EraIds.WorkshopYard && !Definition.Eras[0].Catalog.CompletionUpgradeIds.All(owned.Contains))
                throw new ArgumentException("Workshop Yard lacks completed Junkyard ownership.", nameof(progress));
            return owned;
        }
    }
}
