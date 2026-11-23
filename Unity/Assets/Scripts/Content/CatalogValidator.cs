using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public static class CatalogValidator
    {
        public const int MaximumEntries = 256, MaximumPrerequisites = 32;
        public const long MaximumPrice = 1000000000000, MaximumEffectAmount = 1000000;

        // Asset/effect registries must come from the integration owner, not guessed file names.
        public static ReadOnlyCollection<CatalogIssue> Validate(CatalogDefinition data,
            IEnumerable<string> availableAssetKeys)
        {
            var issues = new List<CatalogIssue>();
            Action<string, string, string> error = (code, subject, detail) =>
                issues.Add(new CatalogIssue(code, subject ?? "<null>", detail));
            if (data == null) { error("catalog-null", "catalog", "A catalog is required."); return issues.AsReadOnly(); }
            var assets = new HashSet<string>(availableAssetKeys ?? new string[0], StringComparer.Ordinal);
            if (data.FormatVersion != 1) error("catalog-version", data.Id, "Unsupported catalog format version.");
            if (data.Families.Count > MaximumEntries || data.Stages.Count > MaximumEntries || data.Upgrades.Count > MaximumEntries || data.CompletionUpgradeIds.Count > MaximumEntries)
            { error("catalog-size", data.Id, "Catalog exceeds the bounded definition count."); return issues.AsReadOnly(); }
            if (!Id(data.Id)) error("invalid-id", data.Id, "Catalog ID must be stable lowercase ASCII.");
            var families = Index(data.Families, x => x.Id, "family", error);
            var stages = Index(data.Stages, x => x.Id, "stage", error);
            var upgrades = Index(data.Upgrades, x => x.Id, "upgrade", error);
            foreach (var u in upgrades.Values)
                if (u.Prerequisites.Count > MaximumPrerequisites || u.Effects.Count > CoinEffects.MaximumContributions)
                { error("upgrade-size", u.Id, "Upgrade exceeds bounded prerequisite/effect counts."); return issues.AsReadOnly(); }
            if (families.Count == 0) error("families-empty", data.Id, "At least one family is required.");
            foreach (var f in families.Values)
            {
                if (string.IsNullOrWhiteSpace(f.DisplayName)) error("family-name", f.Id, "Display name is required.");
                var levels = stages.Values.Where(s => s.FamilyId == f.Id).Select(s => s.Level).OrderBy(x => x).ToArray();
                if (levels.Length == 0 || !levels.SequenceEqual(Enumerable.Range(0, levels.Length)))
                    error("stage-levels", f.Id, "Stages must have unique contiguous levels starting at zero.");
            }
            foreach (var s in stages.Values)
            {
                if (s.FamilyId == null || !families.ContainsKey(s.FamilyId)) error("dangling-family", s.Id, "Unknown family.");
                if (s.Level < 0) error("stage-level", s.Id, "Level cannot be negative.");
                if (!Enum.IsDefined(typeof(FallbackVisual), s.Fallback)) error("fallback-unknown", s.Id, "Unknown visual fallback.");
                if (!string.IsNullOrEmpty(s.AssetKey) && !assets.Contains(s.AssetKey)) error("missing-asset", s.Id, "Asset key is absent from the supplied registry.");
                if (string.IsNullOrEmpty(s.AssetKey) && s.Fallback == FallbackVisual.None) error("visual-missing", s.Id, "Provide an existing asset or explicit placeholder.");
            }
            var incoming = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);
            var effectSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in upgrades.Values)
            {
                if (u.CurrencyId != GameIds.Coins) error("currency-unknown", u.Id, "Only coins is supported.");
                if (u.Price.HasValue ? u.Price.Value <= 0 || u.Price.Value > MaximumPrice : u.Enabled || !u.Provisional)
                    error("price-invalid", u.Id, "Positive price required; only disabled provisional data may be untuned.");
                if (data.Proposed && u.Enabled) error("proposed-enabled", u.Id, "Proposed content must remain disabled.");
                if (u.BenefitPending && (u.Enabled || !u.Provisional)) error("benefit-pending", u.Id, "An unspecified benefit is allowed only in disabled provisional content.");
                StageDefinition from, to;
                bool hasFrom = u.FromStageId != null && stages.TryGetValue(u.FromStageId, out from);
                bool hasTo = u.ToStageId != null && stages.TryGetValue(u.ToStageId, out to);
                if (!hasFrom || !hasTo) error("dangling-stage", u.Id, "Unknown source or destination stage.");
                else
                {
                    from = stages[u.FromStageId]; to = stages[u.ToStageId];
                    if (from.FamilyId != to.FamilyId || (long)from.Level + 1 != to.Level)
                        error("stage-transition", u.Id, "Upgrade must advance exactly one level of the same family.");
                    if (incoming.ContainsKey(to.Id)) error("duplicate-stage-entry", u.Id, "Only one upgrade may enter a stage.");
                    else incoming.Add(to.Id, u);
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var prerequisite in u.Prerequisites)
                {
                    if (prerequisite == null || !upgrades.ContainsKey(prerequisite)) error("dangling-prerequisite", u.Id, "Unknown prerequisite: " + prerequisite);
                    else if (u.Enabled && !upgrades[prerequisite].Enabled) error("disabled-prerequisite", u.Id, "Enabled content depends on disabled content.");
                    if (!seen.Add(prerequisite ?? "")) error("duplicate-prerequisite", u.Id, "Repeated prerequisite.");
                }
                var seenEffects = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in u.Effects)
                {
                    if (e == null) { error("effect-null", u.Id, "Null effect descriptor."); continue; }
                    if (!KnownEffect(e)) error("effect-unknown", u.Id, "Unknown effect ID/kind pair: " + e.EffectId);
                    if (!seenEffects.Add(e.EffectId ?? "")) error("duplicate-effect", u.Id, "Repeated effect ID.");
                    if (!effectSources.Add(e.EffectId ?? "")) error("duplicate-effect-source", u.Id, "Contribution ID is already used by another descriptor.");
                    if (e.Amount.HasValue ? e.Amount.Value < 0 || e.Amount.Value > MaximumEffectAmount : u.Enabled || !e.Provisional)
                        error("effect-amount", u.Id, "Nonnegative amount required; only disabled provisional effects may be untuned.");
                    if (e.Kind == ContentEffectKind.PigeonDeliveryCoins ? !e.Threshold.HasValue || e.Threshold < 1 || e.Threshold > 1000 : e.Threshold.HasValue)
                        error("effect-threshold", u.Id, "Only pigeon delivery takes a bounded positive appearance threshold.");
                    if (e.Kind == ContentEffectKind.UnlockAuto && e.Amount != 0) error("auto-amount", u.Id, "AUTO unlock grants no coins.");
                    if (e.Kind == ContentEffectKind.SymbolCoins ? !e.SymbolId.HasValue || !SymbolIds.IsValid(e.SymbolId.Value) || e.Amount <= 0 : e.SymbolId.HasValue)
                        error("effect-symbol", u.Id, "Only registered symbol bonuses take a valid existing symbol and positive amount.");
                }
            }
            // Requiring the previous stage's upgrade makes progress projection unambiguous.
            foreach (var u in upgrades.Values)
            {
                StageDefinition from; UpgradeDefinition prior;
                if (u.FromStageId != null && stages.TryGetValue(u.FromStageId, out from) && from.Level > 0 &&
                    incoming.TryGetValue(from.Id, out prior) && !u.Prerequisites.Contains(prior.Id))
                    error("stage-prerequisite", u.Id, "Previous stage upgrade must be an explicit prerequisite.");
            }
            foreach (var s in stages.Values)
                if (s.Level > 0 && !incoming.ContainsKey(s.Id)) error("unreachable-stage", s.Id, "No upgrade enters this stage.");

            // Fixed-point traversal includes source-stage access, not only explicit prerequisite edges.
            var reachedStages = new HashSet<string>(stages.Values.Where(s => s.Level == 0).Select(s => s.Id), StringComparer.Ordinal);
            var reachedUpgrades = new HashSet<string>(StringComparer.Ordinal);
            bool changed;
            do
            {
                changed = false;
                foreach (var u in upgrades.Values)
                    if (!reachedUpgrades.Contains(u.Id) && u.FromStageId != null && reachedStages.Contains(u.FromStageId) &&
                        u.ToStageId != null && stages.ContainsKey(u.ToStageId) && u.Prerequisites.All(p => p != null && reachedUpgrades.Contains(p)))
                    { reachedUpgrades.Add(u.Id); reachedStages.Add(u.ToStageId); changed = true; }
            } while (changed);
            foreach (var u in upgrades.Values)
                if (!reachedUpgrades.Contains(u.Id)) error("unreachable-upgrade", u.Id, "Cannot be reached from initial stages.");
            // Iterative cycle detection avoids recursion/stack overflow for imported catalogs.
            var pending = new HashSet<string>(upgrades.Keys, StringComparer.Ordinal);
            do
            {
                var removable = pending.Where(id => upgrades[id].Prerequisites.All(p => p == null || !pending.Contains(p))).ToArray();
                changed = removable.Length > 0;
                foreach (var id in removable) pending.Remove(id);
            } while (changed);
            if (pending.Count > 0) error("prerequisite-cycle", data.Id, "Cycle or cycle-dependent upgrades: " + string.Join(", ", pending.OrderBy(x => x, StringComparer.Ordinal)));

            var completion = new HashSet<string>(StringComparer.Ordinal);
            if (data.CompletionUpgradeIds.Count == 0) error("completion-empty", data.Id, "An explicit completion predicate is required.");
            foreach (var id in data.CompletionUpgradeIds)
            {
                if (id == null || !upgrades.ContainsKey(id)) error("completion-dangling", data.Id, "Unknown completion upgrade: " + id);
                else if (!reachedUpgrades.Contains(id)) error("completion-unreachable", id, "Chapter completion is unreachable.");
                else if (!data.Proposed && !upgrades[id].Enabled) error("completion-disabled", id, "Active chapter requires a disabled upgrade.");
                if (!completion.Add(id ?? "")) error("completion-duplicate", data.Id, "Repeated completion upgrade.");
            }
            foreach (var f in families.Values)
            {
                var finalStage = stages.Values.Where(s => s.FamilyId == f.Id).OrderByDescending(s => s.Level).FirstOrDefault();
                UpgradeDefinition finalUpgrade;
                if (finalStage != null && finalStage.Level > 0 && incoming.TryGetValue(finalStage.Id, out finalUpgrade) && !completion.Contains(finalUpgrade.Id))
                    error("completion-omits-family", f.Id, "Completion must explicitly require the family's final upgrade.");
            }
            return issues.AsReadOnly();
        }
        static bool Id(string value) { return StableId.IsValid(value); }
        static bool KnownEffect(ContentEffect effect)
        {
            switch (effect.Kind)
            {
                case ContentEffectKind.UnlockAuto: return effect.EffectId == ContentIds.UnlockAuto;
                case ContentEffectKind.SpinCoins: return effect.EffectId == GameIds.WorkshopSpinCoins;
                case ContentEffectKind.TrashSymbolCoins: return effect.EffectId == GameIds.RecyclerTrashCoins;
                case ContentEffectKind.PigeonDeliveryCoins: return effect.EffectId == GameIds.PigeonDeliveryCoins;
                case ContentEffectKind.SymbolCoins:
                    int symbol;
                    return ChapterContentIds.TrySymbolEffect(effect.EffectId, out symbol) && effect.SymbolId == symbol;
                default: return false;
            }
        }
        static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key, string kind,
            Action<string, string, string> error) where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null) { error(kind + "-null", kind, "Null entry."); continue; }
                string id = key(item);
                if (!Id(id)) { error("invalid-id", id, "Invalid " + kind + " ID."); continue; }
                if (result.ContainsKey(id)) error("duplicate-" + kind, id, "Repeated stable ID.");
                else result.Add(id, item);
            }
            return result;
        }
    }
}
