using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JunkToNeon.Content
{
    public enum FallbackVisual { None, TextAndBlock }
    public enum ContentEffectKind { UnlockAuto, SpinCoins, TrashSymbolCoins, PigeonDeliveryCoins, SymbolCoins }

    // Definitions deliberately accept invalid input so importers receive structured diagnostics.
    // Collections are defensive snapshots; validated catalogs never retain caller-owned arrays.
    public sealed class StageDefinition
    {
        public string Id { get; }
        public string FamilyId { get; }
        public int Level { get; }
        public string AssetKey { get; }
        public FallbackVisual Fallback { get; }
        public StageDefinition(string id, string familyId, int level, string assetKey = null,
            FallbackVisual fallback = FallbackVisual.TextAndBlock)
        { Id = id; FamilyId = familyId; Level = level; AssetKey = assetKey; Fallback = fallback; }
    }

    public sealed class FamilyDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FamilyDefinition(string id, string displayName) { Id = id; DisplayName = displayName; }
    }

    // Describes a known effect; it does not evaluate triggers or mutate balances.
    // Null amount means not tuned. The Effects stream owns execution semantics.
    public sealed class ContentEffect
    {
        public string EffectId { get; }
        public ContentEffectKind Kind { get; }
        public long? Amount { get; }
        public int? Threshold { get; }
        public int? SymbolId { get; }
        public bool Provisional { get; }
        public ContentEffect(string effectId, ContentEffectKind kind, long? amount, bool provisional, int? threshold = null, int? symbolId = null)
        { EffectId = effectId; Kind = kind; Amount = amount; Provisional = provisional; Threshold = threshold; SymbolId = symbolId; }
    }

    public sealed class UpgradeDefinition
    {
        public string Id { get; }
        public string FromStageId { get; }
        public string ToStageId { get; }
        public string CurrencyId { get; }
        public long? Price { get; }
        public bool Enabled { get; }
        public bool Provisional { get; }
        public bool BenefitPending { get; }
        public string BenefitSummary { get; }
        public ReadOnlyCollection<string> Prerequisites { get; }
        public ReadOnlyCollection<ContentEffect> Effects { get; }
        public UpgradeDefinition(string id, string fromStageId, string toStageId, long? price,
            bool enabled, bool provisional, IEnumerable<string> prerequisites = null,
            IEnumerable<ContentEffect> effects = null, string currencyId = "coins", bool benefitPending = false, string benefitSummary = null)
        {
            Id = id; FromStageId = fromStageId; ToStageId = toStageId; Price = price;
            Enabled = enabled; Provisional = provisional; CurrencyId = currencyId; BenefitPending = benefitPending; BenefitSummary = benefitSummary;
            Prerequisites = Freeze(prerequisites); Effects = Freeze(effects);
        }
        internal static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values)
        {
            var snapshot = new List<T>();
            if (values != null) foreach (var value in values)
            {
                snapshot.Add(value);
                // Keep one extra entry so validation rejects excess instead of truncating to valid data.
                if (snapshot.Count > CatalogValidator.MaximumEntries) break;
            }
            return snapshot.AsReadOnly();
        }
    }

    public sealed class CatalogDefinition
    {
        public string Id { get; }
        public int FormatVersion { get; }
        public bool Proposed { get; }
        public ReadOnlyCollection<FamilyDefinition> Families { get; }
        public ReadOnlyCollection<StageDefinition> Stages { get; }
        public ReadOnlyCollection<UpgradeDefinition> Upgrades { get; }
        public ReadOnlyCollection<string> CompletionUpgradeIds { get; }
        public CatalogDefinition(string id, bool proposed, IEnumerable<FamilyDefinition> families,
            IEnumerable<StageDefinition> stages, IEnumerable<UpgradeDefinition> upgrades,
            IEnumerable<string> completionUpgradeIds, int formatVersion = 1)
        {
            Id = id; FormatVersion = formatVersion; Proposed = proposed; Families = UpgradeDefinition.Freeze(families);
            Stages = UpgradeDefinition.Freeze(stages); Upgrades = UpgradeDefinition.Freeze(upgrades);
            CompletionUpgradeIds = UpgradeDefinition.Freeze(completionUpgradeIds);
        }
    }

    public sealed class CatalogIssue
    {
        public string Code { get; }
        public string Subject { get; }
        public string Detail { get; }
        public CatalogIssue(string code, string subject, string detail)
        { Code = code; Subject = subject; Detail = detail; }
        public override string ToString() { return Code + ": " + Subject + " — " + Detail; }
    }
}
