using System;
using System.Globalization;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Guidance
{
    public enum GuideOrigin { Fresh = 0, Existing = 1, LegacyExpanded = 2 }
    [Flags] public enum TutorialSteps { None = 0, Previewed = 1, Selected = 2, ManualSpin = 4, VisibleImprovement = 8, AutoInformed = 16 }
    public enum GuideOperationKind { ForegroundSpin = 0, ProjectSelection = 1, SkillPurchase = 2, EraEntry = 3 }

    public static class GuideIds
    {
        public const string Welcome = "guide.welcome", ChooseProject = "guide.choose-project", FirstRepair = "guide.first-repair", Skills = "guide.skills";
        public const string EraReady = "guide.era-ready", ExpandedR7 = "guide.expanded-r7", ChapterEnd = "guide.chapter-end", Orders = "guide.orders", Completion = "guide.completion";
        static readonly string[] ids = { Welcome, ChooseProject, FirstRepair, Skills, EraReady, ExpandedR7, ChapterEnd, Orders };
        internal static int Bit(string id)
        {
            for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return 1 << i;
            throw new ArgumentException("Unknown guide ID", nameof(id));
        }
        internal static bool IsKnownAck(string id)
        {
            if (id == Completion) return true;
            foreach (string known in ids) if (known == id) return true;
            return false;
        }
    }

    public sealed class GuideContext
    {
        public FamilyStages Stages { get; }
        public string SelectedProjectId { get; }
        public int SkillCount { get; }
        public bool LegacyExpanded { get; }
        // Available means orders are unlocked, including while an order is active.
        public bool OrdersAvailable { get; }
        public bool OrderActive { get; }
        public bool OrderReady { get; }
        public bool ChapterComplete { get { return AllAt(5); } }
        public bool EraReady { get { return Stages.EraId == EraIds.Junkyard && AllAt(2); } }
        public GuideContext(FamilyStages stages, string selectedProjectId, int skillCount, bool legacyExpanded, bool ordersAvailable, bool orderActive, bool orderReady)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            if (selectedProjectId != null && !ChapterIds.IsProject(selectedProjectId, stages.EraId)) throw new ArgumentException("Unknown selected project", nameof(selectedProjectId));
            if (selectedProjectId != null)
                foreach (string family in FamilyIds.All) for (int level = 1; level <= 2; level++)
                    if (ChapterIds.Project(stages.EraId, family, level) == selectedProjectId && stages.AbsoluteLevel(family) != (stages.EraId == EraIds.Junkyard ? level - 1 : level + 2))
                        throw new ArgumentException("Selected project is not the next family stage");
            if (skillCount < 0 || skillCount > 6) throw new ArgumentOutOfRangeException(nameof(skillCount));
            Stages = stages; SelectedProjectId = selectedProjectId; SkillCount = skillCount; LegacyExpanded = legacyExpanded;
            OrdersAvailable = ordersAvailable; OrderActive = orderActive; OrderReady = orderReady;
            if ((ordersAvailable || orderActive || orderReady) && !ChapterComplete) throw new ArgumentException("Orders require the completed chapter");
            if (orderReady && !orderActive || orderActive && !ordersAvailable) throw new ArgumentException("Inconsistent order facts");
        }
        bool AllAt(int level)
        {
            foreach (var stage in Stages.Entries) if (stage.AbsoluteLevel != level) return false;
            return true;
        }
        internal string Identity()
        {
            var key = new StringBuilder(Stages.EraId);
            foreach (var stage in Stages.Entries) key.Append('/').Append(stage.FamilyId).Append(':').Append(stage.AbsoluteLevel.ToString(CultureInfo.InvariantCulture));
            return key.Append('|').Append(SelectedProjectId ?? "-").Append('|').Append(SkillCount.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(LegacyExpanded ? '1' : '0').Append(OrdersAvailable ? '1' : '0').Append(OrderActive ? '1' : '0').Append(OrderReady ? '1' : '0').ToString();
        }
    }

    public sealed class ProjectCompletion
    {
        public string ProjectId { get; }
        public long Sequence { get; }
        public string EffectInfoKey { get { return ProjectId + ".effect"; } }
        public ProjectCompletion(string projectId, long sequence)
        {
            if (!(ChapterIds.IsProject(projectId, EraIds.Junkyard) || ChapterIds.IsProject(projectId, EraIds.WorkshopYard)) || sequence < 1) throw new ArgumentException("Invalid completed project identity");
            ProjectId = projectId; Sequence = sequence;
        }
        internal bool Same(ProjectCompletion other) { return other != null && ProjectId == other.ProjectId && Sequence == other.Sequence; }
    }

    // Origin is supplied from factual Core creation/migration provenance. It is
    // never inferred from wallet37, sequence0, stages or a remembered checkpoint.
    public sealed class GuideState
    {
        public const int Version = 1;
        public GuideOrigin Origin { get; }
        public int AcknowledgementBits { get; }
        public int TutorialBits { get; }
        public long LastObservedSequence { get; }
        public string LastObservationKey { get; }
        public ProjectCompletion LastCompletion { get; }
        public long LastAckSequence { get; }
        public string LastAckHintId { get; }
        public long CompletionAcknowledgedSequence { get; }
        public GuideState(GuideOrigin origin, int acknowledgementBits, int tutorialBits, long lastObservedSequence, string lastObservationKey,
            ProjectCompletion lastCompletion, long lastAckSequence, string lastAckHintId, long completionAcknowledgedSequence = 0)
        {
            if (origin < GuideOrigin.Fresh || origin > GuideOrigin.LegacyExpanded || acknowledgementBits < 0 || acknowledgementBits > 255 || tutorialBits < 0 || tutorialBits > 31)
                throw new ArgumentException("Invalid finite guide state");
            if (origin != GuideOrigin.Fresh && tutorialBits != 0) throw new ArgumentException("Existing players do not acquire invented tutorial history");
            if (lastObservedSequence < 0 || lastAckSequence < 0 || (lastObservedSequence != 0 && lastObservedSequence == lastAckSequence)) throw new ArgumentException("Invalid guide sequence");
            if (acknowledgementBits != 0 && lastAckSequence == 0) throw new ArgumentException("Acknowledgement history missing");
            if (lastObservedSequence == 0 ? lastObservationKey != null : !IsKey(lastObservationKey)) throw new ArgumentException("Invalid observation identity");
            if (lastCompletion != null && lastCompletion.Sequence > lastObservedSequence) throw new ArgumentException("Completion references a future observation");
            if (completionAcknowledgedSequence != 0 && (lastCompletion == null || completionAcknowledgedSequence != lastCompletion.Sequence || lastAckSequence <= lastCompletion.Sequence)) throw new ArgumentException("Invalid completion acknowledgement");
            if (lastAckSequence == 0 ? lastAckHintId != null : !GuideIds.IsKnownAck(lastAckHintId)) throw new ArgumentException("Invalid acknowledgement identity");
            if (lastAckHintId != null && lastAckHintId != GuideIds.Completion && (acknowledgementBits & GuideIds.Bit(lastAckHintId)) == 0) throw new ArgumentException("Last acknowledgement bit missing");
            if (lastAckHintId == GuideIds.Completion && completionAcknowledgedSequence == 0 && (lastCompletion == null || lastAckSequence >= lastCompletion.Sequence)) throw new ArgumentException("Completion acknowledgement missing");
            Origin = origin; AcknowledgementBits = acknowledgementBits; TutorialBits = tutorialBits; LastObservedSequence = lastObservedSequence;
            LastObservationKey = lastObservationKey; LastCompletion = lastCompletion; LastAckSequence = lastAckSequence; LastAckHintId = lastAckHintId;
            CompletionAcknowledgedSequence = completionAcknowledgedSequence;
        }
        static bool IsKey(string key)
        {
            if (key == null || key.Length != 64) return false;
            foreach (char c in key) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
        public static GuideState Create(GuideOrigin origin) { return new GuideState(origin, 0, 0, 0, null, null, 0, null); }
        public bool IsAcknowledged(string hintId) { return (AcknowledgementBits & GuideIds.Bit(hintId)) != 0; }
        internal long HighWater { get { return Math.Max(LastObservedSequence, LastAckSequence); } }
        internal bool Same(GuideState other)
        {
            return other != null && Origin == other.Origin && AcknowledgementBits == other.AcknowledgementBits && TutorialBits == other.TutorialBits &&
                LastObservedSequence == other.LastObservedSequence && LastObservationKey == other.LastObservationKey && LastAckSequence == other.LastAckSequence && LastAckHintId == other.LastAckHintId &&
                CompletionAcknowledgedSequence == other.CompletionAcknowledgedSequence && (LastCompletion == null ? other.LastCompletion == null : LastCompletion.Same(other.LastCompletion));
        }
    }

    public sealed class GuideObservation
    {
        public long Sequence { get; }
        public GuideOperationKind Kind { get; }
        public GuideContext BeforeContext { get; }
        public GuideContext AfterContext { get; }
        public string ActionId { get; }
        public string CompletedProjectId { get; }
        public bool ManualSpin { get; }
        public int ObservedTutorialBits { get; }
        public GuideObservation(long sequence, GuideOperationKind kind, GuideContext beforeContext, GuideContext afterContext, string actionId,
            string completedProjectId, bool manualSpin = false, int observedTutorialBits = 0)
        {
            if (sequence < 1 || kind < GuideOperationKind.ForegroundSpin || kind > GuideOperationKind.EraEntry || beforeContext == null || afterContext == null || !StableId.IsValid(actionId)) throw new ArgumentException("Invalid guide observation");
            if (observedTutorialBits < 0 || observedTutorialBits > 31 || manualSpin && kind != GuideOperationKind.ForegroundSpin) throw new ArgumentException("Invalid tutorial observation");
            Sequence = sequence; Kind = kind; BeforeContext = beforeContext; AfterContext = afterContext; ActionId = actionId;
            CompletedProjectId = completedProjectId; ManualSpin = manualSpin; ObservedTutorialBits = observedTutorialBits;
        }
    }

    public sealed class GuideSuggestion
    {
        public string HintId { get; }
        public string Title { get; }
        public string Body { get; }
        public bool BlocksActions { get { return false; } }
        internal GuideSuggestion(string hintId, string title, string body) { HintId = hintId; Title = title; Body = body; }
    }
    public sealed class GuideEvaluation
    {
        public GuideSuggestion Suggestion { get; }
        public ProjectCompletion LastCompletion { get; }
        internal GuideEvaluation(GuideSuggestion suggestion, ProjectCompletion lastCompletion) { Suggestion = suggestion; LastCompletion = lastCompletion; }
    }
    public sealed class GuideProposal
    {
        public GuideState Before { get; }
        public GuideState After { get; }
        public long Sequence { get; }
        public bool Changed { get { return !Before.Same(After); } }
        internal GuideProposal(GuideState before, GuideState after, long sequence) { Before = before; After = after; Sequence = sequence; }
        public GuideState Apply(GuideState current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (current.Same(After)) return current;
            if (!current.Same(Before)) throw new InvalidOperationException("Stale guide proposal");
            return After;
        }
    }
}
