using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public static class ChapterValidator
    {
        public static ReadOnlyCollection<CatalogIssue> Validate(ChapterDefinition chapter, IEnumerable<string> availableAssetKeys)
        {
            var issues = new List<CatalogIssue>();
            if (chapter == null) { issues.Add(new CatalogIssue("chapter-null", "chapter", "Chapter is required.")); return issues.AsReadOnly(); }
            if (chapter.Id != ChapterContentIds.Catalog) issues.Add(new CatalogIssue("chapter-version", chapter.Id, "Unsupported chapter rules revision."));
            if (chapter.Eras.Count != 2) { issues.Add(new CatalogIssue("era-count", chapter.Id, "This chapter has exactly two eras.")); return issues.AsReadOnly(); }
            // Snapshot the supplied registry once: single-pass inputs must behave like arrays.
            var assets = new HashSet<string>(availableAssetKeys ?? new string[0], StringComparer.Ordinal);
            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var upgradeIds = new HashSet<string>(StringComparer.Ordinal);
            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < 2; index++)
            {
                var era = chapter.Eras[index];
                string expected = index == 0 ? EraIds.Junkyard : EraIds.WorkshopYard;
                if (era == null || era.Id != expected || era.Catalog == null)
                { issues.Add(new CatalogIssue("era-identity", expected, "Missing or incorrectly ordered era.")); continue; }
                var data = era.Catalog;
                issues.AddRange(CatalogValidator.Validate(data, assets));
                if (data.Proposed || data.Families.Count != 6 || !data.Families.Where(f => f != null).Select(f => f.Id).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(FamilyIds.All.OrderBy(x => x, StringComparer.Ordinal)))
                    issues.Add(new CatalogIssue("chapter-families", era.Id, "Both active eras retain the same six registered objects."));
                if (data.Stages.Count != 18 || data.Upgrades.Count != 12)
                    issues.Add(new CatalogIssue("chapter-size", era.Id, "Each era requires eighteen states and twelve paid transitions."));
                foreach (var stage in data.Stages)
                {
                    if (stage == null) continue;
                    if (!stageIds.Add(stage.Id ?? "")) issues.Add(new CatalogIssue("chapter-stage-duplicate", stage.Id, "Stage IDs must be globally unique."));
                    if (!FamilyIds.All.Contains(stage.FamilyId) || stage.Level < 0 || stage.Level > 2 ||
                        stage.Id != ChapterIds.Stage(era.Id, stage.FamilyId, stage.Level))
                        issues.Add(new CatalogIssue("chapter-stage-id", stage.Id, "Stage does not match the registered era/family/level."));
                }
                foreach (var upgrade in data.Upgrades)
                {
                    if (upgrade == null) continue;
                    if (!upgradeIds.Add(upgrade.Id ?? "")) issues.Add(new CatalogIssue("chapter-upgrade-duplicate", upgrade.Id, "Upgrade IDs must never be reused across eras."));
                    if (!upgrade.Enabled || !upgrade.Price.HasValue || upgrade.BenefitPending || string.IsNullOrWhiteSpace(upgrade.BenefitSummary) || upgrade.BenefitSummary.Length > 256)
                        issues.Add(new CatalogIssue("chapter-untuned", upgrade.Id, "Every active transition needs a price and explicit resolved benefit."));
                    var target = data.Stages.FirstOrDefault(s => s != null && s.Id == upgrade.ToStageId);
                    if (target == null || target.Level < 1 || target.Level > 2 || !FamilyIds.All.Contains(target.FamilyId) ||
                        upgrade.Id != ChapterIds.Project(era.Id, target.FamilyId, target.Level))
                        issues.Add(new CatalogIssue("chapter-upgrade-id", upgrade.Id, "Upgrade must use its registered persistent identity."));
                    int symbol;
                    if (ChapterContentIds.TrySymbolEffect(ChapterContentIds.SymbolEffect(upgrade.Id), out symbol) &&
                        (upgrade.Effects.Count != 1 || upgrade.Effects[0] == null || upgrade.Effects[0].Kind != ContentEffectKind.SymbolCoins ||
                         upgrade.Effects[0].EffectId != ChapterContentIds.SymbolEffect(upgrade.Id) || upgrade.Effects[0].Amount != 1 || upgrade.Effects[0].SymbolId != symbol))
                        issues.Add(new CatalogIssue("chapter-benefit", upgrade.Id, "Registered chapter improvement requires its concrete +1 symbol benefit."));
                    foreach (var effect in upgrade.Effects)
                        if (effect != null && !effectIds.Add(effect.EffectId ?? "")) issues.Add(new CatalogIssue("chapter-effect-duplicate", effect.EffectId, "Effect source ID collision across eras."));
                }
            }
            // Preserve existing r7 saved-project meaning, including deliberately unchanged benefits.
            var first = chapter.Eras[0]?.Catalog;
            if (first != null)
            {
                RequireLegacy(first, GameIds.HouseRepair, 40, ContentIds.UnlockAuto, ContentEffectKind.UnlockAuto, 0, null, issues);
                RequireLegacy(first, GameIds.HouseImprove, 600, null, ContentEffectKind.UnlockAuto, 0, null, issues);
                RequireLegacy(first, GameIds.WorkshopBuild, 120, GameIds.WorkshopSpinCoins, ContentEffectKind.SpinCoins, 1, null, issues);
                RequireLegacy(first, GameIds.RecyclerBuild, 180, GameIds.RecyclerTrashCoins, ContentEffectKind.TrashSymbolCoins, 1, null, issues);
                RequireLegacy(first, GameIds.PigeonLoftBuild, 240, GameIds.PigeonDeliveryCoins, ContentEffectKind.PigeonDeliveryCoins, 3, 3, issues);
            }
            return issues.AsReadOnly();
        }
        static void RequireLegacy(CatalogDefinition data, string id, long price, string effectId, ContentEffectKind kind,
            long amount, int? threshold, List<CatalogIssue> issues)
        {
            var u = data.Upgrades.FirstOrDefault(x => x != null && x.Id == id);
            bool valid = u != null && u.Price == price;
            if (valid && effectId == null) valid = u.Effects.Count == 0;
            else if (valid) valid = u.Effects.Count == 1 && u.Effects[0] != null && u.Effects[0].EffectId == effectId &&
                u.Effects[0].Kind == kind && u.Effects[0].Amount == amount && u.Effects[0].Threshold == threshold && u.Effects[0].SymbolId == null;
            if (valid) valid = id == GameIds.HouseRepair ? u.Prerequisites.Count == 0 : u.Prerequisites.SequenceEqual(new[] { GameIds.HouseRepair });
            if (!valid) issues.Add(new CatalogIssue("chapter-r7-meaning", id, "Existing price, prerequisite or effect meaning changed."));
        }
    }
}
