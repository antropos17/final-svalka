# Desktop storage integration checks (r10)

## Overnight r11 legacy-price recovery: two new real-file cases

Window2026-10-29T05:30:12Z–13:30:12Z; source base `c583236`, isolated branch
`codex/night-legacy-storage06`, NEW source-only `X:/tmp/jtn-night-storage06`.
The older dirty `X:/tmp/jtn-effects-wave06` copy and all three retained old runs
were preserved. Shared Program/PrivateFiles and all production source are unchanged.
The nested NightLegacyPricing entry links the existing audited production storage;
no fake memory storage, Editor, ordinary save path or additional source mutation.

Exactly two new NTFS cases passed in one invocation under02's granted heavy slot:

- An immutable r7 selected/pending600 spin backup (595 + base8 + workshop1 −600 =4)
  is restored beside a corrupt primary and a complete, conflicting NEW540 explicit
  reselection candidate (595−540=55) in the normal-write temp. Cancel/inspect/reload
  makes zero writes or Restore calls. Explicit confirmation restores exactly4,
  sequence2, original `[0,0,2]`, seed2647435461, original600 receipt and house2.
  The55 candidate is preserved as evidence and is never selected as recovery state.
- The immutable already-paid r7 counterpart restores4 without refunding60 or
  charging540/600 again. A later explicit recycler selection adopts V2 current
  offers without changing wallet4 or the retained historical600 spin receipt.
  The original backup remains byte-identical in its fixed recovery evidence slot,
  even after normal journal backup rotation for that legitimate new selection.

Both cases preserve all original primary/backup/conflicting-temp bytes in the
three fixed recovery slots. Three reopens and old-sequence replays per case cause
zero writes and no additional credit/debit. Current house offer540 is checked
independently against retained600 history. Cancellation here is deliberately the
no-Restore API boundary, not another UI/pointer acceptance claim.

The selected/pending/paid r7 inputs are existing literal fixture bytes, used only
for these new physical combinations. The existing ConstructionPricing.FileBackup
already covers a different modern595+8−600=3 case; its result, the old four NTFS
groups, eight bounded-reader groups and54-snapshot corpus are reused unchanged.
No broad old suite was repeated. No production defect was reproduced or fixed.

Command (only after a02 test-slot grant, with X: TEMP/TMP/CLI-home and certificate
generation disabled):

```powershell
dotnet run --project Checks/StorageIntegration/NightLegacyPricing/NightLegacyPricing.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -- --workspace X:/tmp/jtn-night-storage06
```

The harness checks the hard deadline before setup and each of its two cases. It
uses the old exact path/reparse/file/size guards in its dedicated NEW night
worktree; it does not bypass the old producer's three-run retention. Receipt and
stdout are committed under NightLegacyPricing, with45 actual production source
hashes and literal-fixture/evidence hashes. Private originals remain under
`.work/storage-integration-r10/run-1e34226394dc4985a8ac9eee82770ac5`.

No build/assertion failure occurred. Fixture bytes9,241; new build/temp/stdout
output30 files /1,179,313 bytes. Before run C9,003,188,224 / X92,331,364,352 bytes,
free RAM9,362,640 KiB; afterward C9,002,524,672 / X92,330,078,208 and9,268,432 KiB.
These samples include concurrent authorized work. Existing manual seven-day /
8 MiB disposable-temp inspection policy applies; originals and receipts remain.

This proves only the stated Windows/NTFS recovery combinations, not Android,
power-loss or UI cancellation.02 owns integration and STATUS. Frozen independent
review is requested before integration; producer PASS is not an independent rerun.

## Follow-up: bounded production snapshot reader

Source base `1b15ed9`, branch `codex/bounded-snapshot-reader`. The NEW production
`Core/BoundedSnapshotReader.cs` provides `ReadFile(path)` and
`ReadStream(existingStream, leaveOpen=true)`, plus specific
`SnapshotSizeLimitException : FormatException`. `GameSession.cs` and the existing
storage harness `Program.cs` remain byte-identical to that base;02 owns their
separate performance/wiring edits. The original four-file-I/O receipts below are
preserved and were not rerun for this helper-only change.

The helper enforces65,536 decoded UTF-16 units, matching the codec's existing
`string.Length` check. A1024-character decoder buffer reads at most one extra
character, and retained content never exceeds the cap. A separate streaming byte
budget is262,148 =4*65,536+4: UTF-32 BMP is the worst supported bytes-per-unit and
the longest detected BOM is four bytes. Only one further byte may be probed for
overflow. This bound does not trust `Length`/`FileInfo` and still applies to a
growing stream. Source streams may have their own fixed read-ahead buffers; the
byte budget counts bytes delivered through the supplied Stream, not underlying
storage-controller I/O. No whole over-limit file is allocated.

Default UTF-8, BOM detection for UTF-8/UTF-16LE/BE/UTF-32LE/BE, replacement fallback,
missing-file null behavior and FileShare.Read match the old readers. Other IO
errors propagate. Existing stream position is consumed without an implicit rewind;
leaveOpen controls ownership on success and rejection. The helper never writes,
truncates, deletes, resets or repairs evidence.

Agreed02 wiring: primary/backup reads use ReadFile. Open catches ONLY the specific
limit exception from backup reading and retains a nonnull invalid candidate
(empty string), never null. Thus a valid primary still opens with BackupAvailable
false; missing primary plus an oversized present backup blocks fresh37. Primary
overflow fails closed. Explicit RestoreBackup compares ReadStream on its already
exclusive source BEFORE any preservation/promotion; existing Preserve rewinds and
copies raw binary bytes unchanged. The worker does not claim those production
call sites have been wired by its commit.

Eight focused groups passed using real private files, maximum262,152 bytes each:
five encoded65,536/65,537-unit boundaries (UTF-32 overflow consumes262,149 bytes),
real growth1024→262,152 rejected after66,560 delivered bytes, baseline codec/backup
presence, and sharing/malformed-byte/ownership compatibility. Four malformed BOM
or UTF-8 inputs match File.ReadAllText replacement behavior. A65,536-unit surrogate
string is accepted; Windows deny-delete/exclusive locks preserve sharing error32.
The existing unbounded ReadPrimary was reproduced returning65,537 characters from
a small fixture; the new helper rejects the same bytes. The agreed-wiring adapter
(not a production GameSession edit) opens a healthy primary with oversized backup
and blocks absent primary without a grant, with zero writes in both cases.

Run ONLY the new boundary, avoiding both the old four cases and the54 corpus:

```powershell
dotnet run --project Checks/StorageIntegration/ReaderFocused/ReaderChecks.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -- --workspace X:/tmp/jtn-effects-wave06
```

Use the same X: TEMP/TMP/private CLI-home and certificate-generation flags documented
below. The tiny focused project remains within the existing StorageIntegration
harness and links its path guard. Optional guard limits for this named reader run
allow at most4 MiB fixtures and262,152 bytes/file; original defaults stay unchanged.
`reader-verification.json`, `reader-results.txt` preserve the successful receipt;
fixtures remain at `.work/bounded-reader-r10/run-8a2e635f59d1475c9a8a6f75d342261e`.
The first compile failed CS0136 on a shadowed test-local variable; its literal
output is retained in `reader-first-compile.txt`. Renaming the test variable fixed
it before any reader filesystem case ran. The subsequent focused run passed.
Helper SHA-256: `B481D1BC37119349FEEB08C8E7F987939311599D40F109302D5397880359BDD3`.

Initial C9,010,876,416 / X92,563,808,256 bytes free, RAM6,745,644 KiB; post-run
C8,996,864,000 / X92,454,125,568, RAM5,568,824 KiB. Fixtures2,490,455 bytes
(2,492,437 including receipt); existing harness output including earlier runs now
2,345,395 bytes. Outputs/obj/bin remain under X: `.work`; no normal saves, global
configuration, Editor/import/APK or process were touched. Separate02 integration
and its targeted actual-GameStore tests remain required. Windows evidence does
not establish Android decoding/runtime or power-loss durability.

Date: 2026-10-29 (Asia/Baku). Source baseline `061ad0e`, isolated branch
`codex/storage-integration-r10`, source-only worktree `X:/tmp/jtn-effects-wave06`.
Only NEW `Checks/StorageIntegration/` and this handoff are owned. No production
code, normal save, Unity Editor/import/build, process, permission or global setting
was changed. No server/cloud service was installed or contacted.

## What was added and reused

The harness compiles the actual production `FileSnapshotStorage`, `GameStore`,
`GameSession`, `JsonStateCodec` and their existing pure dependencies directly.
`AuditedStorage` delegates every read, flush/write, replace, quarantine and restore
to production storage; it is not an in-memory substitute. It records completed
write contents and can hold a PRIVATE Windows read handle denying deletion to
make one real replacement fail. The two controlled sharing failures do not modify
source, kill a process or simulate a storage acknowledgement in memory.

Existing `Unity/Assets/Tests/RecoveryRegression.cs` already covers malformed and
missing candidates, binary/BOM preservation, occupied evidence slots, conflicting
restore temp, changed backup, inaccessible backup/primary, locked restore promotion
and private hard-link alias refusal. `CoreRegression.cs` already covers basic file
reload, bounded orphan duplicate/conflict handling and missing-primary protection.
Their matching r9 Core evidence is `docs/evidence/unity/journey-core-r9.txt`
(47 groups, including the three RecoveryRegression groups), linked by
`journey-verification-r9.json`. The existing 54 literal compatibility fixtures /
1,910 counted assertions and memory-fault qualification remain separate reused
evidence. None of these whole suites was rerun here.

The new four groups target exact backup lineage for modern Journey journals and
the real Windows replacement failures before and after durable reservation, plus
explicit modern backup recovery/cancellation. This is additional desktop file-I/O
evidence, not a replacement for compatibility or native qualification.

## Actual results and independent expectations

All four groups passed on **Windows / X: NTFS**, using explicit private files.
Fixed seed1 independently expects symbols `[0,0,2]` (potato/potato/trash), exclusive
pair reward8 and resulting RNG seed2647435461. No expectation calls the production
payout function to derive its own oracle.

| Case | Independent expected behavior | Observed |
| --- | --- | --- |
| Modern journal and backup lineage | Initial37 once; explicit repair selection then pair8 pays40, leaving5 at sequence2. Every successful replace backs up the exact previous full snapshot. | Five writes total (fresh move, two reserve/commit pairs). All four replacement backups byte-match the previous written primary; final backup is frozen modern pending spin2. Three committed reopens cause no writes or content/mtime changes. |
| Reservation replacement denied | Unpromoted pending result must not pay or advance RNG. Further actions block until reload. | OS sharing violation32; old primary remains37/sequence0. Fully written pending temp is preserved in the fixed interrupted slot on reopen, while wallet/seed/history remain unchanged. Only a new explicit spin pays8, reaching45 once. Blocked retry makes no write. |
| Applied-result replacement denied | A durable reservation must recover the same frozen result once, even with a complete applied temp left over. | OS sharing violation32 after successful reserve. Primary stays frozen pending1. Reopen quarantines the complete applied temp, writes that exact committed result, and retains reservation as backup:45/sequence1/reward8. Three reopens plus same-sequence command replay leave all files and write counts unchanged. |
| Cancel, then explicit modern restore | Loading a malformed primary must not restore/reset automatically. Cancel issues no restore command. Explicit restore resolves the frozen modern purchase once and preserves original evidence. | Inspection/cancel/retry-load produce zero application Write/RestoreBackup calls and identical file inventory/hash/last-write times. Explicit restore reaches5/house1/sequence2 and the exact Guidance completion. Malformed primary, pending backup and torn normal-write bytes remain in their three fixed recovery slots; backup is unchanged. Three reopens/replays write nothing. |

Both actual replacement failures were `IOException`, HRESULT `-2147024864`,
Win32 `ERROR_SHARING_VIOLATION`32. No permissions were changed. The final complete
candidate from the failed applied replacement is byte-identical to the recovered
primary. The unpromoted reservation was preserved but never interpreted as a new
economic command. These are different orphan states and were tested separately.

Cancellation here is deliberately the non-UI boundary: inspect flags and issue
NO `RestoreBackup` call. It proves no application save write on that path; it does
not newly exercise a pointer/Back/confirmation button. Existing UI evidence is
reused for those controls. File reads may have OS access-metadata effects; zero
writes refers to save mutation calls/content/last-write metadata, not every NTFS
internal operation.

## Receipts and first failure

Committed `Checks/StorageIntegration/verification.json` contains OS/filesystem,
43 actual linked production source SHA-256 values, each case's assertions/results,
expected OS failures and final file hash/last-write inventories. Production source
hashes were checked again after the run and remained unchanged.
`results.txt` preserves stdout. The synthetic fixture files remain only under:

`X:/tmp/jtn-effects-wave06/.work/storage-integration-r10/run-d09f959a8a104318b2ed0ca8299ec68f`

They total30,203 bytes, with38,884 bytes including the receipt. There is no ordinary
Unity persistent-data path lookup anywhere in the harness.

The first run failed **before constructing any production storage case** because
the harness normalized a candidate path but retained mixed separators in its own
run-root string. Its catch receipt hit the same containment guard. That empty
first-run folder is preserved as
`run-48e175a19c1a4f94886ec569f5717592`; `first-failure.txt` records the failure and
the smallest fix. Only the harness root was changed to `Path.GetFullPath(...)`.
That concrete correction justified the one rerun. All four actual storage groups
then passed. No reproducible production storage bug or shared-source fix was
found or attempted.

## Run and path/output guards

From the exact isolated source worktree:

```powershell
$env:TEMP = "$PWD/.work/storage-integration-checks/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet-home"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project Checks/StorageIntegration/StorageIntegrationChecks.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -- --workspace X:/tmp/jtn-effects-wave06
```

The harness requires an explicit absolute X: workspace equal to the working
directory, an isolated Git worktree pointer file and this check project. Existing
workspace/fixture ancestors and target files are checked for reparse attributes;
reparse targets and paths escaping the exact canonical private run are rejected.
No symlink/reparse-target campaign or permission relaxation was performed. The
guard is a test-scope boundary, not a claim of an adversarial filesystem sandbox.

Each run permits at most eight flat case directories, eight exact known save
filenames per case,128 KiB per file,2 MiB fixture content and64 KiB receipt.
Every delegated call is surrounded by bounded real-file inventory checks. Only
three preserved run directories are allowed before another run refuses and asks
for exact-folder retention inspection. The harness performs no recursive cleanup.
Keep failed/passing receipts and original evidence. Disposable task Temp has a
seven-day /8 MiB inspection budget; cleanup, if later needed, applies only to
confirmed closed disposable regular files in that exact directory, skipping
locks/reparse points. No automatic rotation is installed.

MSBuild obj/bin, process TEMP/TMP and private CLI home remain under X: `.work`.
Harness build/temp/stdout output measured27 files /1,176,403 bytes, in addition to
the small private fixtures. Initial free space was C9,023,467,520 /
X92,870,770,688 bytes; final sample C8,980,180,992 / X92,763,455,488 bytes. Free RAM
sampled5,335,428→5,044,324 KiB around the scoped compile/run. Other authorized
streams were active, so whole-drive/RAM changes are not attributed to this task.
No unbounded task output growth was observed.

## Limits and ownership

The observer adds file reads/hashing and is unsuitable for latency conclusions;
02's measured phase/performance work remains separate. This run observes complete
snapshots and sharing-failure outcomes, not every concurrent-reader interleaving.
It does not prove power-loss durability, storage-controller behavior, process-kill
recovery, Android filesystem/IL2CPP behavior, physical-device acceptance or
exactly-once history across explicit backup rollback. `Flush(true)` and successful
desktop replacement are not a substitute for that evidence.

No additional Core/Unity suite or APK is required for this Checks/docs-only
handoff. 02 alone integrates shared production changes if separately justified;
this result proposes none. Desktop file-I/O qualification is complete within the
four stated cases, with prior matching cases explicitly reused.

## Connected primary r11 acceptance

Helper f4eebae is integrated as991054d; actual GameSession/FileSnapshotStorage wiring and generic-codec readability fix are committed at bd8fbe8. Independent05 source review is closed. New actual-file Core regression covers oversized backup with healthy primary, missing-primary no-grant, oversized-primary block, exclusive recovery rejection before preservation, exact65536 boundary, and a custom codec accepting empty text. Final Core49 passes. The four original real NTFS journal/recovery groups ran once against the exact final seven-domain source copy in the reserved06 worktree; all pass, both deny-delete cases require Win32=32. The new receipt explicitly identifies changed sources against061ad0e; it does not overwrite the old receipt or claim unchanged baseline source. Every tested production source SHA matches primary. Three preserved runs now occupy that isolated harness retention limit; inspect them before any further run. The prior eight pure reader groups reuse their unchanged helper evidence. New source/evidence: docs/evidence/unity/integration-r11/verification.json. No physical device or power-loss qualification follows.
