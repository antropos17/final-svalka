using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;

namespace JunkToNeon.Diagnostics
{
    public enum LocalEventKind { TutorialCompleted, ProjectSelected, ProjectCompleted, EraEntered, OrderClaimed, RuntimeError, MissingResource, PersistenceError }
    public enum DiagnosticCategory { Gameplay, Runtime, MissingResource, Persistence }
    public enum RuntimeErrorCode { None, UnityError, ManagedException, UnexpectedRuntimeState }
    public enum ResourceKind { None, Font, HouseSprite, ReelSprite, SceneBinding, OtherRequiredResource }
    public enum PersistenceErrorCode { None, ReadFailed, ReservationWriteFailed, CommitWriteFailed, RecoveryBlocked }

    // Only a checked-in release label belongs here, never a discovered path,
    // command line, user identifier, exception text or arbitrary configuration.
    public sealed class BuildIdentity
    {
        public const int MaximumLength = 48;
        public string ReleaseLabel { get; }
        public static BuildIdentity Unknown { get; } = new BuildIdentity("unknown");
        public BuildIdentity(string releaseLabel)
        {
            if (!Valid(releaseLabel)) throw new ArgumentException("Invalid diagnostic release label", nameof(releaseLabel));
            ReleaseLabel = releaseLabel;
        }
        static bool Valid(string value)
        {
            if (value == "unknown") return true;
            const string prefix = "unity-r", suffix = "-local-diagnostics";
            if (value == null || value.Length > MaximumLength || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(suffix, StringComparison.Ordinal)) return false;
            int digits = value.Length - prefix.Length - suffix.Length;
            if (digits < 1 || digits > 6) return false;
            for (int i = prefix.Length; i < prefix.Length + digits; i++) if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }
        public override string ToString() { return ReleaseLabel; }
    }

    // Closed factories exclude free-form messages and properties. The caller
    // supplies facts from successfully committed before/after results only.
    public sealed class LocalEvent
    {
        public const long MaximumOperationSequence = (long.MaxValue - 2) / 2;
        public const int TutorialBitsMask = 31;
        public const string TutorialId = "tutorial.first-loop";
        public LocalEventKind Kind { get; }
        public DiagnosticCategory Category { get; }
        public long OperationSequence { get; }
        public long InstanceSequence { get; }
        public string SubjectId { get; }
        public RuntimeErrorCode RuntimeCode { get; }
        public ResourceKind Resource { get; }
        public PersistenceErrorCode PersistenceCode { get; }
        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case LocalEventKind.TutorialCompleted: return "tutorial.completed";
                    case LocalEventKind.ProjectSelected: return "project.selected";
                    case LocalEventKind.ProjectCompleted: return "project.completed";
                    case LocalEventKind.EraEntered: return "era.entered";
                    case LocalEventKind.OrderClaimed: return "order.claimed";
                    case LocalEventKind.RuntimeError: return "error.runtime";
                    case LocalEventKind.MissingResource: return "error.resource";
                    default: return "error.persistence";
                }
            }
        }
        LocalEvent(LocalEventKind kind, DiagnosticCategory category, long sequence, string subject = null,
            long instance = 0, RuntimeErrorCode runtime = RuntimeErrorCode.None, ResourceKind resource = ResourceKind.None,
            PersistenceErrorCode persistence = PersistenceErrorCode.None)
        {
            if (sequence < (category == DiagnosticCategory.Gameplay ? 1 : 0) || sequence > MaximumOperationSequence) throw new ArgumentOutOfRangeException(nameof(sequence));
            Kind = kind; Category = category; OperationSequence = sequence; SubjectId = subject; InstanceSequence = instance;
            RuntimeCode = runtime; Resource = resource; PersistenceCode = persistence;
        }
        public static LocalEvent TutorialCompleted(long sequence, int beforeBits, int afterBits)
        {
            if (beforeBits < 0 || beforeBits >= TutorialBitsMask || afterBits != TutorialBitsMask) throw new ArgumentException("Tutorial requires a new factual incomplete-to-complete transition");
            return new LocalEvent(LocalEventKind.TutorialCompleted, DiagnosticCategory.Gameplay, sequence, TutorialId);
        }
        public static LocalEvent ProjectSelected(long sequence, string projectId) { return Project(LocalEventKind.ProjectSelected, sequence, projectId); }
        public static LocalEvent ProjectCompleted(long sequence, string projectId) { return Project(LocalEventKind.ProjectCompleted, sequence, projectId); }
        static LocalEvent Project(LocalEventKind kind, long sequence, string id)
        {
            if (!(ChapterIds.IsProject(id, EraIds.Junkyard) || ChapterIds.IsProject(id, EraIds.WorkshopYard))) throw new ArgumentException("Unknown diagnostic project ID", nameof(id));
            return new LocalEvent(kind, DiagnosticCategory.Gameplay, sequence, id);
        }
        public static LocalEvent EraEntered(long sequence, string eraId)
        {
            if (eraId != EraIds.WorkshopYard) throw new ArgumentException("Unknown entered era", nameof(eraId));
            return new LocalEvent(LocalEventKind.EraEntered, DiagnosticCategory.Gameplay, sequence, eraId);
        }
        public static LocalEvent OrderClaimed(long claimSequence, string orderId, long instanceSequence)
        {
            if (orderId != "order.sort-salvage" && orderId != "order.neighborhood-supply") throw new ArgumentException("Unknown diagnostic order ID", nameof(orderId));
            if (instanceSequence < 1 || instanceSequence >= claimSequence || instanceSequence > MaximumOperationSequence) throw new ArgumentOutOfRangeException(nameof(instanceSequence));
            return new LocalEvent(LocalEventKind.OrderClaimed, DiagnosticCategory.Gameplay, claimSequence, orderId, instanceSequence);
        }
        public static LocalEvent RuntimeError(long sequence, RuntimeErrorCode code)
        {
            if (code < RuntimeErrorCode.UnityError || code > RuntimeErrorCode.UnexpectedRuntimeState) throw new ArgumentOutOfRangeException(nameof(code));
            return new LocalEvent(LocalEventKind.RuntimeError, DiagnosticCategory.Runtime, sequence, runtime: code);
        }
        public static LocalEvent MissingResource(long sequence, ResourceKind resource)
        {
            if (resource < ResourceKind.Font || resource > ResourceKind.OtherRequiredResource) throw new ArgumentOutOfRangeException(nameof(resource));
            return new LocalEvent(LocalEventKind.MissingResource, DiagnosticCategory.MissingResource, sequence, resource: resource);
        }
        public static LocalEvent PersistenceError(long sequence, PersistenceErrorCode code)
        {
            if (code < PersistenceErrorCode.ReadFailed || code > PersistenceErrorCode.RecoveryBlocked) throw new ArgumentOutOfRangeException(nameof(code));
            return new LocalEvent(LocalEventKind.PersistenceError, DiagnosticCategory.Persistence, sequence, persistence: code);
        }
        internal bool Same(LocalEvent other)
        {
            return other != null && Kind == other.Kind && OperationSequence == other.OperationSequence && InstanceSequence == other.InstanceSequence &&
                SubjectId == other.SubjectId && RuntimeCode == other.RuntimeCode && Resource == other.Resource && PersistenceCode == other.PersistenceCode;
        }
    }

    public interface IAnalyticsAdapter { void Record(LocalEvent value); }
    public sealed class NoOpAnalytics : IAnalyticsAdapter
    {
        public static NoOpAnalytics Instance { get; } = new NoOpAnalytics();
        NoOpAnalytics() { }
        public void Record(LocalEvent value) { }
    }
    public static class AnalyticsBoundary
    {
        // An optional observer cannot undo or prevent a gameplay operation. This
        // boundary never forwards an adapter exception to another logging sink.
        public static bool TryRecord(IAnalyticsAdapter adapter, LocalEvent value)
        {
            if (adapter == null || value == null) return false;
            try { adapter.Record(value); return true; }
            catch (Exception) { return false; }
        }
    }
    public sealed class DiagnosticRecord
    {
        public BuildIdentity Build { get; }
        public LocalEvent Event { get; }
        public int Occurrences { get; }
        internal DiagnosticRecord(BuildIdentity build, LocalEvent value, int occurrences) { Build = build; Event = value; Occurrences = occurrences; }
    }

    // Session-only retention: latest Capacity distinct consecutive entries.
    // No timers, disk writer, export loop, per-spin event or background thread.
    public sealed class LocalDiagnosticRing : IAnalyticsAdapter
    {
        public const int MaximumCapacity = 256, MaximumOccurrences = 65535;
        readonly object gate = new object();
        readonly BuildIdentity build;
        readonly DiagnosticRecord[] entries;
        int start, count, dropped;
        public int Capacity { get { return entries.Length; } }
        public int Count { get { lock (gate) return count; } }
        public int DroppedRecords { get { lock (gate) return dropped; } }
        public LocalDiagnosticRing(BuildIdentity build, int capacity = 64)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.build = build; entries = new DiagnosticRecord[capacity];
        }
        public void Record(LocalEvent value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            lock (gate)
            {
                int last = (start + count - 1 + Capacity) % Capacity;
                if (count != 0 && entries[last].Event.Same(value))
                {
                    int occurrences = entries[last].Occurrences;
                    if (occurrences < MaximumOccurrences) entries[last] = new DiagnosticRecord(build, value, occurrences + 1);
                    return;
                }
                if (count == Capacity)
                {
                    entries[start] = new DiagnosticRecord(build, value, 1); start = (start + 1) % Capacity;
                    if (dropped < int.MaxValue) dropped++;
                }
                else { entries[(start + count) % Capacity] = new DiagnosticRecord(build, value, 1); count++; }
            }
        }
        public IReadOnlyList<DiagnosticRecord> Snapshot()
        {
            lock (gate)
            {
                var result = new DiagnosticRecord[count];
                for (int i = 0; i < count; i++) result[i] = entries[(start + i) % Capacity];
                return Array.AsReadOnly(result);
            }
        }
        public void Clear()
        {
            lock (gate) { Array.Clear(entries, 0, entries.Length); start = count = dropped = 0; }
        }
    }
}
