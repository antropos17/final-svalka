using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content
{
    public sealed class EraDefinition
    {
        public string Id { get; }
        public CatalogDefinition Catalog { get; }
        public EraDefinition(string id, CatalogDefinition catalog) { Id = id; Catalog = catalog; }
    }

    public sealed class ChapterDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<EraDefinition> Eras { get; }
        public ChapterDefinition(string id, IEnumerable<EraDefinition> eras)
        { Id = id; Eras = UpgradeDefinition.Freeze(eras); }
    }

    // Contains only chapter ownership, not wallet/sequence/selection/skills/carry or timestamps.
    public sealed class ChapterProgress
    {
        public string EraId { get; }
        public ReadOnlyCollection<string> CompletedUpgradeIds { get; }
        public ChapterProgress(string eraId, IEnumerable<string> completedUpgradeIds)
        {
            if (completedUpgradeIds == null) throw new ArgumentNullException(nameof(completedUpgradeIds));
            EraId = eraId; CompletedUpgradeIds = UpgradeDefinition.Freeze(completedUpgradeIds);
        }
        public static ChapterProgress Fresh() { return new ChapterProgress(EraIds.Junkyard, new string[0]); }
    }

    public sealed class ChapterStageView
    {
        public string EraId { get; }
        public StageDefinition Stage { get; }
        public FamilyStage Progress { get; }
        public int AbsoluteLevel { get { return Progress.AbsoluteLevel; } }
        internal ChapterStageView(string eraId, StageDefinition stage, int absoluteLevel)
        { EraId = eraId; Stage = stage; Progress = new FamilyStage(stage.FamilyId, absoluteLevel); }
    }

    // An immutable transaction proposal, not a commit or replay authorization. The sole
    // session owner freezes these values with its sequence/revision before any write.
    public sealed class ChapterProposal
    {
        public string CatalogId { get; }
        public string RuleRevision { get; }
        public string OperationId { get; }
        public ChapterProgress Before { get; }
        public ChapterProgress After { get; }
        public long CoinsBefore { get; }
        public long CoinsAfter { get; }
        public long Price { get; }
        internal ChapterProposal(string catalogId, string operationId, ChapterProgress before,
            ChapterProgress after, long coinsBefore, long price)
        {
            CatalogId = catalogId; RuleRevision = ChapterContentIds.RuleRevision; OperationId = operationId; Before = before; After = after;
            CoinsBefore = coinsBefore; Price = price; CoinsAfter = checked(coinsBefore - price);
        }
    }

    public sealed class ChapterLegacyProjection
    {
        public ChapterProgress Progress { get; }
        public string SelectedUpgradeId { get; }
        internal ChapterLegacyProjection(ChapterProgress progress, string selected)
        { Progress = progress; SelectedUpgradeId = selected; }
    }
}
