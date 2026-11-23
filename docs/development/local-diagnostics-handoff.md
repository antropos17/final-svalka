# Minimal local diagnostics handoff

Date: 2026-10-29 (Asia/Baku). Stream06, branch `codex/local-diagnostics-wave10`,
source-only worktree `X:/tmp/jtn-effects-wave06`. Baseline `3079397` records actual
r9 runtime source `c086395` and packaging `a1f9bff`. Earlier Effects and Guidance
branches/commits are preserved. The new module is not activated in this checkout.

## Implemented boundary

Original task34 requests a small analytics boundary, bounded local diagnostics,
named gameplay events and distinct error categories. For active Unity these are
managed/Unity runtime, missing required resources and persistence; JavaScript and
Capacitor are historical. 02 confirmed the exact Runtime-only integration API.

New namespace/assembly `JunkToNeon.Diagnostics` references Contracts only, with
`noEngineReferences:true`. It has no Core, Unity, wallet, save, clock, filesystem,
network, SDK, background worker or external analytics configuration. It cannot
select/purchase/grant or change RNG. Existing Core/runtime/scene/module sources
are untouched. Runtime activation and connected acceptance belong to02.

```csharp
IAnalyticsAdapter.Record(LocalEvent value);
NoOpAnalytics.Instance;
AnalyticsBoundary.TryRecord(IAnalyticsAdapter adapter, LocalEvent value); // bool
new BuildIdentity("unity-r10-local-diagnostics");
new LocalDiagnosticRing(BuildIdentity build, int capacity = 64);
ring.Record(value);       // also implements IAnalyticsAdapter
ring.Snapshot();          // IReadOnlyList<DiagnosticRecord>, detached/read-only
ring.Clear();
```

`Count`, `Capacity`, `DroppedRecords` are read-only. `DiagnosticRecord` contains
immutable `Build`, `Event` and `Occurrences`. All records share the constructor's
immutable build constant. No build/version/path discovery is performed. The
accepted release-label family is `unity-r<1..6 ASCII digits>-local-diagnostics`,
or explicit `unknown`, bounded to48 characters. The supplied r10 label identifies
the intended integration, not an APK built by this source-only task.

## Exact event meanings

| Factory / name | Emit only for | Subject and operation identity |
| --- | --- | --- |
| `TutorialCompleted(sequence,beforeBits,afterBits)` / `tutorial.completed` | A new factual incomplete-to-all-five transition (after31, before0..30). Loaded-already-complete and economic commit alone are not completion. | Fixed `tutorial.first-loop`, successful global sequence. Core supplies factual Fresh-origin guide bits. |
| `ProjectSelected(sequence,projectId)` / `project.selected` | Successful explicit selection. Use the requested ID even when immediately affordable and completed. | One of24 known Chapter project IDs. |
| `ProjectCompleted(sequence,projectId)` / `project.completed` | A NEW immutable Guidance.LastCompletion identity after successful commit; never inferred visual presentation. | Exact completion project ID and sequence. Selection/completion may share a sequence but remain separate event kinds. |
| `EraEntered(sequence,eraId)` / `era.entered` | Successful actual Junkyard-to-WorkshopYard entry. | `workshop-yard`, entry command sequence. |
| `OrderClaimed(claimSequence,orderId,instanceSequence)` / `order.claimed` | A NEW retained committed claim, excluding replay or mere ready progress. | Two fixed order IDs, claim sequence and prior acceptance-instance sequence. Repeat instances remain distinguishable. |

All operation/instance sequences are bounded by `(long.MaxValue-2)/2`. Success
requires a positive operation sequence; zero means absent only for errors. A
claim's instance is positive and strictly older than its claim. Project IDs use
the exact Contracts registry, not arbitrary syntactically plausible strings.
No event exists for each spin, preview, cancellation, clock tick or order progress.
The module defines payload meanings; it cannot prove an economic commit itself.

Error factories accept only fixed enum codes, plus a bounded operation sequence:

- `RuntimeError(sequence, RuntimeErrorCode)` accepts UnityError, ManagedException
  or UnexpectedRuntimeState. It reports category Runtime / name `error.runtime`.
- `MissingResource(sequence, ResourceKind)` accepts Font, HouseSprite, ReelSprite,
  SceneBinding or OtherRequiredResource. Category MissingResource /
  `error.resource`; no resource filename/path is retained.
- `PersistenceError(sequence, PersistenceErrorCode)` accepts ReadFailed,
  ReservationWriteFailed, CommitWriteFailed or RecoveryBlocked. Category
  Persistence / `error.persistence`. Error reporting never authorizes a retry.

There is deliberately no parameter for raw logs, exception messages, stack
traces, paths, user identifiers, command lines, arbitrary properties or secrets.
Invalid labels/IDs/codes reject without echoing their supplied values. This is
privacy by closed payloads, not a general-purpose redactor for arbitrary text.

## Bounded retention and non-interference

Capacity is1..256, default64. FIFO eviction retains the most recent distinct
consecutive records, in insertion order. Identical adjacent records coalesce and
their occurrence count saturates at65,535. Nonadjacent repeats remain separate
bounded records. `DroppedRecords` counts evictions, saturating at `int.MaxValue`.
Occurrences count observer calls, not independently verified rewards or players.

The ring is session-memory-only: no age timer, disk writer, persistence queue or
automatic export exists. `Clear` removes retained records and resets counters;
dropping the ring ends its lifetime. Locking supports error callbacks from
different threads without creating any thread. Snapshots allocate detached arrays
of at most Capacity immutable entries; consumers must not retain an unbounded
history of snapshots themselves. The ring is neither an economic ledger nor
durable/deduplicated telemetry across eviction/restart/backup rollback.

`AnalyticsBoundary.TryRecord` catches a throwing adapter and returns false without
forwarding the exception to another log sink. Null input/adapter returns false.
NoOp is an explicit discard sink. Runtime must guard factory construction as well
as observer invocation, and never let optional diagnostics exceptions escape its
observer boundary. Do not recursively log a sink failure through the same Unity
error callback. Failure is not permission to retry a game command.

## Small02 integration plan

1. Construct one bounded local ring with the checked-in build label; no external
   adapter is configured. Expose only a detached snapshot for read-only QA.
2. Compare successfully returned before/after Session results. Initial save open,
   projection/recovery synchronization and already-committed replay synchronize
   silently. Failed/uncertain journal calls emit no success events. Capture both
   immediate selection and new completion from the same command when applicable.
3. Tutorial completion requires actual guide visibility/preview/AUTO facts,
   not a staged purchase or loaded31 bits. New order claims compare retained claim
   and instance identities. No extra economic command or save field is added.
4. Known resource lookups produce fixed ResourceKind records. Actual Session
   read/write/recovery errors produce persistence codes at their known boundary;
   do not mislabel every runtime exception as storage failure.
5. A Unity error/exception/assert adapter discards all received raw strings and
   emits only the fixed Runtime code. Keep handler subscription/unsubscription
   bounded to the owned runtime lifetime. No process-wide setting is changed by
   this module.

## Native Android evidence is separate

This managed ring cannot guarantee capture of a native Android/IL2CPP crash,
process termination or OS failure before a managed callback. It is not a native
crash reporter. Existing Unity Android tooling remains the separate evidence path.
The installed embedded adb file was verified by filesystem lookup at
`X:/Programs/Unity/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`.
No adb command, daemon, device/emulator connection or installation was started.

If separately authorized native investigation is needed, use bounded Android
crash/logcat evidence with the exact APK identity and matching native symbols,
stored privately under an exact project-local `.work` directory. A finite crash
buffer dump is distinct from an unbounded live logcat stream. Native logs may
contain information outside this module's closed fields; they must not be fed
unfiltered into this ring or treated as its privacy guarantee. No physical/native
crash capture or symbolication acceptance is claimed here.

## Verification and storage

`dotnet run --project Checks/Diagnostics/DiagnosticsChecks.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true` passed 8 groups:

- Latest 3 of 1,000 records retained, 997 evictions, capacity/default bounds.
- 70,000 identical errors retain one record with count 65,535; intervening contexts
  prevent aggregation across them.
- Detached immutable snapshot, build identity, append and Clear isolation.
- All24 project IDs and five success meanings, same-command selection/completion,
  incomplete-to31 tutorial transition, era restriction and repeat-order identities.
- Intentionally thrown fixture managed/persistence exceptions and missing fixture
  resource map to three distinct fixed-code categories with no raw content.
- Hostile path/URL/token/email/newline/oversized labels and IDs reject without
  echo; enum/operation/instance limits, including exact maximum global sequence.
- Explicit no-op and hostile throwing/null observers do not escape the safe
  boundary or interrupt subsequent test work.
- Concurrent append from four harness workers retains only8 of4,000 records
  with3,992 evictions. The module itself creates no workers.

No assertion or build failure occurred. Final stdout is
`.work/diagnostics-checks/final.txt` (977 bytes). Only the new harness ran; no old
Core/full suite, Unity import/Editor/build or live save was touched.

Process TEMP/TMP, private DOTNET_CLI_HOME and all obj/bin outputs use X: `.work`;
certificate generation and CLI telemetry were disabled before dotnet. MSBuild
Directory.Build.props redirects obj/bin before SDK imports. Diagnostic task output
measured25 files /506,797 bytes. Initial free C9,505,726,464 / X94,036,090,880 bytes;
post-check sample C9,841,971,200 / X93,752,627,200 bytes; final pre-commit sample
C9,795,461,120 / X93,188,775,936 bytes, with unchanged506,797-byte task output.
Free physical memory sampled12,429,476→12,131,428 KiB (total32,945,340 KiB).
Other streams are active, so these
samples do not attribute all machine changes to this small task.

Disposable exact task `.work/diagnostics-checks/temp` diagnostics have a seven-day /
8 MiB budget before reuse. Inspect before another batch; remove only confirmed
closed disposable regular files there, skipping locks/reparse points. Preserve
source/handoff/receipts and ordinary saves; no system cache cleanup, background
rotation or guarantee of unrelated log behavior is claimed.

Remaining scope:02 Runtime adapter and actual subscribed callback/resource/Session
observations, Unity compilation and connected private-save failure/success smoke
checks. Standalone fixtures do not establish live telemetry or native acceptance.
