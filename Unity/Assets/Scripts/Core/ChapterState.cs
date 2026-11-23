using System;
using JunkToNeon.Content;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    public sealed class ChapterState
    {
        public const int FormatVersion = 2;
        public string RuleRevision { get; }
        public string ConstructionPriceRevision { get; }
        public ChapterProgress Progress { get; }
        public SkillOwnership Skills { get; }
        public string SelectedProjectId { get; }
        public long EntrySequence { get; }
        public long TransitionSeen { get; }
        public bool TransitionPending { get { return EntrySequence > TransitionSeen; } }
        public ChapterState(int format, string ruleRevision, ChapterProgress progress, SkillOwnership skills,
            string selectedProjectId, long entrySequence, long transitionSeen, string constructionPriceRevision = ConstructionPricing.LegacyRevision)
        {
            if (format != FormatVersion || ruleRevision != ChapterContentIds.RuleRevision || progress == null || skills == null || !ConstructionPricing.IsKnown(constructionPriceRevision))
                throw new InvalidOperationException("Unsupported chapter state");
            ChapterCatalog.Rules.ValidateProgress(progress);
            if (selectedProjectId != null && !ChapterCatalog.Rules.AvailableUpgrades(progress).Contains(selectedProjectId))
                throw new InvalidOperationException("Unavailable chapter selection");
            if (progress.EraId == EraIds.Junkyard ? entrySequence != 0 || transitionSeen != 0
                : entrySequence < 1 || (transitionSeen != 0 && transitionSeen != entrySequence))
                throw new InvalidOperationException("Invalid era transition marker");
            SkillTree.ValidateOwnership(new SkillContext(ChapterCatalog.Rules.Stages(progress), selectedProjectId), skills);
            RuleRevision = ruleRevision; Progress = progress; Skills = skills; SelectedProjectId = selectedProjectId;
            ConstructionPriceRevision = constructionPriceRevision;
            EntrySequence = entrySequence; TransitionSeen = transitionSeen;
        }
    }

    // Complete immutable pending delta, also retained by a spin receipt. Only the
    // existing GameStore writes it, alongside the complete pre-command snapshot.
    public sealed class ChapterOperation
    {
        readonly int[] symbols;
        readonly OfflineReceipt settlement;
        public const int FormatVersion = 2;
        public string Kind { get; }
        public long Sequence { get; }
        public long At { get; }
        public string ActionId { get; }
        public long Price { get; }
        public long CoinsBefore { get; }
        public long CoinsAfter { get; }
        public uint SeedBefore { get; }
        public uint SeedAfter { get; }
        public int[] Symbols { get { return symbols == null ? null : (int[])symbols.Clone(); } }
        public int BaseCoins { get; }
        public int WorkshopCoins { get; }
        public ChapterState Before { get; }
        public ChapterState After { get; }
        public FrozenSpinEffectsV2 Effects { get; }
        public SkillPurchaseRequest SkillRequest { get; }
        public OfflineReceipt Settlement { get { return settlement?.Copy(); } }
        public ChapterOperation(int format, string kind, long sequence, long at, string actionId, long price,
            long coinsBefore, long coinsAfter, uint seedBefore, uint seedAfter, int[] frozenSymbols,
            int baseCoins, int workshopCoins, ChapterState before, ChapterState after, FrozenSpinEffectsV2 effects,
            SkillPurchaseRequest skillRequest, OfflineReceipt frozenSettlement)
        {
            if (format != 2 || sequence < 1 || at < 0 || at > OfflineIncome.MaxUtcSeconds || price < 0 ||
                coinsBefore < 0 || coinsAfter < 0 || seedBefore == 0 || seedAfter == 0 || before == null || after == null ||
                baseCoins < 0 || workshopCoins < 0 || workshopCoins > 1)
                throw new InvalidOperationException("Invalid frozen chapter operation");
            if (kind != "spin" && kind != "select" && kind != "skill" && kind != "enter-era" && kind != "era-seen" &&
                kind != "clock" && kind != "settle" && kind != "hint" && kind != "seen" && kind != ClearGoalRequest.Kind)
                throw new InvalidOperationException("Unknown chapter command");
            Kind = kind; Sequence = sequence; At = at; ActionId = actionId; Price = price; CoinsBefore = coinsBefore; CoinsAfter = coinsAfter;
            SeedBefore = seedBefore; SeedAfter = seedAfter; symbols = frozenSymbols == null ? null : (int[])frozenSymbols.Clone();
            BaseCoins = baseCoins; WorkshopCoins = workshopCoins; Before = before; After = after; Effects = effects;
            SkillRequest = skillRequest; settlement = frozenSettlement?.Copy();
        }
    }
}
