using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JunkToNeon.Contracts;
using JunkToNeon.Diagnostics;

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected rejection: " + typeof(T).Name);
}
var passed = new List<string>();
var build = new BuildIdentity("unity-r10-local-diagnostics");
var ring = new LocalDiagnosticRing(build, 3);
for (int i = 1; i <= 1000; i++) ring.Record(LocalEvent.ProjectSelected(i, GameIds.HouseRepair));
var retained = ring.Snapshot();
Require(ring.Count == 3 && ring.DroppedRecords == 997 && retained[0].Event.OperationSequence == 998 && retained[2].Event.OperationSequence == 1000, "FIFO capacity/retention differs from latest3");
Reject<ArgumentOutOfRangeException>(() => new LocalDiagnosticRing(build, 0));
Reject<ArgumentOutOfRangeException>(() => new LocalDiagnosticRing(build, 257));
Require(new LocalDiagnosticRing(build).Capacity == 64, "Default capacity changed");
passed.Add("FIFO retains latest3 of1000, counts997 evictions, validates1..256 capacity/default64");

ring.Clear();
var error = LocalEvent.RuntimeError(0, RuntimeErrorCode.UnityError);
for (int i = 0; i < 70000; i++) ring.Record(error);
Require(ring.Count == 1 && ring.Snapshot()[0].Occurrences == 65535 && ring.DroppedRecords == 0, "Repeated errors grew history/counter without bound");
ring.Record(LocalEvent.MissingResource(0, ResourceKind.Font)); ring.Record(error);
Require(ring.Count == 3 && ring.Snapshot()[2].Occurrences == 1, "Aggregation crossed distinct intervening context");
passed.Add("Adjacent identical errors coalesce and saturate65535; distinct contexts retain separate entries");

var snapshot = ring.Snapshot();
var list = snapshot as IList<DiagnosticRecord>;
Require(list != null && list.IsReadOnly && snapshot[0].Build.ReleaseLabel == "unity-r10-local-diagnostics", "Snapshot/build identity is not immutable");
Reject<NotSupportedException>(() => list[0] = snapshot[1]);
ring.Clear();
Require(ring.Count == 0 && ring.DroppedRecords == 0 && ring.Snapshot().Count == 0 && snapshot.Count == 3 && snapshot[0].Occurrences == 65535, "Clear mutated an older detached snapshot");
ring.Record(LocalEvent.RuntimeError(1, RuntimeErrorCode.ManagedException));
Require(snapshot[0].Event.OperationSequence == 0 && ring.Snapshot()[0].Event.OperationSequence == 1, "New append mutated snapshot");
passed.Add("Detached read-only snapshots preserve build/events after append and explicit Clear");

int projects = 0;
foreach (string era in new[] { EraIds.Junkyard, EraIds.WorkshopYard })
    foreach (string family in FamilyIds.All)
        for (int stage = 1; stage <= 2; stage++)
        {
            string id = ChapterIds.Project(era, family, stage);
            var selected = LocalEvent.ProjectSelected(4, id); var completed = LocalEvent.ProjectCompleted(4, id);
            Require(selected.Name == "project.selected" && completed.Name == "project.completed" && selected.SubjectId == id && completed.OperationSequence == 4, "Selected/completed event identity changed");
            projects++;
        }
Require(projects == 24, "Project whitelist is not exactly24");
var tutorial = LocalEvent.TutorialCompleted(5, 23, 31);
Require(tutorial.Name == "tutorial.completed" && tutorial.SubjectId == LocalEvent.TutorialId, "Tutorial event mapping");
Reject<ArgumentException>(() => LocalEvent.TutorialCompleted(5, 31, 31));
Reject<ArgumentException>(() => LocalEvent.TutorialCompleted(5, 0, 30));
Require(LocalEvent.EraEntered(6, EraIds.WorkshopYard).Name == "era.entered", "Era event mapping");
Reject<ArgumentException>(() => LocalEvent.EraEntered(6, EraIds.Junkyard));
var firstClaim = LocalEvent.OrderClaimed(10, "order.sort-salvage", 7);
var secondClaim = LocalEvent.OrderClaimed(20, "order.sort-salvage", 17);
Require(firstClaim.Name == "order.claimed" && firstClaim.InstanceSequence == 7 && secondClaim.InstanceSequence == 17 && secondClaim.OperationSequence == 20, "Repeat order instance identity lost");
Require(LocalEvent.OrderClaimed(25, "order.neighborhood-supply", 21).SubjectId == "order.neighborhood-supply", "Second order type not recognized");
ring.Clear(); ring.Record(LocalEvent.ProjectSelected(4, GameIds.HouseRepair)); ring.Record(LocalEvent.ProjectCompleted(4, GameIds.HouseRepair));
Require(ring.Count == 2 && ring.Snapshot()[0].Event.Kind != ring.Snapshot()[1].Event.Kind, "Immediate selection/completion collapsed to one meaning");
Require(!Enum.IsDefined(typeof(LocalEventKind), "Spin"), "Per-spin history was added");
passed.Add("Five success meanings, all24 projects, factual tutorial transition, actual era entry, repeated order instances and same-op selection/completion");

ring.Clear();
try { throw new InvalidOperationException("fixture-secret-runtime"); }
catch (InvalidOperationException) { ring.Record(LocalEvent.RuntimeError(0, RuntimeErrorCode.ManagedException)); }
var fixtureAssets = new Dictionary<string, object>();
if (!fixtureAssets.ContainsKey("missing-font")) ring.Record(LocalEvent.MissingResource(0, ResourceKind.Font));
try { throw new IOException("fixture-secret-save-path"); }
catch (IOException) { ring.Record(LocalEvent.PersistenceError(11, PersistenceErrorCode.CommitWriteFailed)); }
var categorized = ring.Snapshot();
Require(categorized[0].Event.Category == DiagnosticCategory.Runtime && categorized[0].Event.RuntimeCode == RuntimeErrorCode.ManagedException, "Managed fault category");
Require(categorized[1].Event.Category == DiagnosticCategory.MissingResource && categorized[1].Event.Resource == ResourceKind.Font, "Missing resource category");
Require(categorized[2].Event.Category == DiagnosticCategory.Persistence && categorized[2].Event.PersistenceCode == PersistenceErrorCode.CommitWriteFailed, "Persistence fault category");
foreach (var record in categorized) Require(record.Event.SubjectId == null && !record.Event.Name.Contains("fixture-secret"), "Fault payload retained raw input");
passed.Add("Intentionally triggered fixture runtime/resource/persistence faults become separate fixed-code categories without raw exception content");

foreach (string hostile in new[] { "C:/Users/private/save.json", "https://host/?token=fixture-secret", "token=fixture-secret", "user@example.org", "unity-r1234567-local-diagnostics", "unity-r10-local-diagnostics\n", new string('a', 4096) })
{
    try { new BuildIdentity(hostile); throw new Exception("Hostile label accepted"); }
    catch (ArgumentException failure) { Require(!failure.Message.Contains(hostile), "Rejected input echoed into exception"); }
    Reject<ArgumentException>(() => LocalEvent.ProjectSelected(1, hostile));
    Reject<ArgumentException>(() => LocalEvent.OrderClaimed(3, hostile, 1));
}
Reject<ArgumentException>(() => new BuildIdentity(null));
Require(BuildIdentity.Unknown.ReleaseLabel == "unknown", "Unknown build fallback identity changed");
Reject<ArgumentOutOfRangeException>(() => LocalEvent.RuntimeError(0, (RuntimeErrorCode)99));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.MissingResource(0, ResourceKind.None));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.PersistenceError(0, PersistenceErrorCode.None));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.ProjectSelected(0, GameIds.HouseRepair));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.RuntimeError(-1, RuntimeErrorCode.UnityError));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.RuntimeError(LocalEvent.MaximumOperationSequence + 1, RuntimeErrorCode.UnityError));
Reject<ArgumentOutOfRangeException>(() => LocalEvent.OrderClaimed(3, "order.sort-salvage", 3));
Require(LocalEvent.ProjectCompleted(LocalEvent.MaximumOperationSequence, GameIds.HouseImprove).OperationSequence == LocalEvent.MaximumOperationSequence, "Maximum global operation rejected");
passed.Add("Privacy by closed payloads: hostile labels/IDs rejected without echo, no arbitrary error text, strict enums and operation/instance bounds");

Reject<ArgumentNullException>(() => ring.Record(null));
Require(AnalyticsBoundary.TryRecord(NoOpAnalytics.Instance, tutorial), "No-op adapter failed");
Require(!AnalyticsBoundary.TryRecord(new ThrowingAdapter(), tutorial) && !AnalyticsBoundary.TryRecord(null, tutorial) && !AnalyticsBoundary.TryRecord(ring, null), "Analytics failure escaped boundary");
int gameplayContinued = 1;
AnalyticsBoundary.TryRecord(new ThrowingAdapter(), LocalEvent.ProjectCompleted(3, GameIds.HouseRepair));
gameplayContinued++;
Require(gameplayContinued == 2 && AnalyticsBoundary.TryRecord(ring, tutorial), "Optional analytics interrupted following work");
passed.Add("Explicit no-op adapter and failure-isolating boundary keep throwing/null observers out of gameplay control flow");

var concurrent = new LocalDiagnosticRing(build, 8);
Parallel.For(0, 4, worker =>
{
    for (int i = 1; i <= 1000; i++) concurrent.Record(LocalEvent.RuntimeError(worker * 1000 + i, RuntimeErrorCode.UnityError));
});
Require(concurrent.Count == 8 && concurrent.DroppedRecords == 3992 && concurrent.Snapshot().Count == 8, "Concurrent callback records violated capacity");
foreach (var entry in concurrent.Snapshot()) Require(entry.Event.OperationSequence > 0 && entry.Occurrences == 1, "Concurrent record corrupted");
passed.Add("Concurrent callback append remains bounded and snapshot-consistent, without module-owned threads");

foreach (string result in passed) Console.WriteLine("PASS " + result);
Console.WriteLine("PASS " + passed.Count + " diagnostics regression groups");

public sealed class ThrowingAdapter : IAnalyticsAdapter
{
    public void Record(LocalEvent value) { throw new InvalidOperationException("fixture-secret-adapter"); }
}
