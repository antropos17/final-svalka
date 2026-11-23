# Save Inspector P2 fixes — stream04

Date: 2026-10-29 (Asia/Baku). Immutable base: `7133a3def23003bd4fd45e2fad99ceee941a25f3`
on the original tools/save-inspector branch. This fix uses separate source-only worktree
`X:/tmp/jtn-save-inspector-fixes04`, branch `codex/save-inspector-fixes04`.
The original `X:/tmp/jtn-save-inspector` checkout was only inspected, never edited or driven.
All patch paths are under tools/SaveInspector. No production sources/semantics, normal saves,
Unity, SDK, global settings or shared Git configuration changed; no worktree was removed.
Historical cleanup/publication suggestions in HANDOFF.md were not executed.

## Changes

1. SelfTest.PrepareWorkspace calls InputGuard.ValidateWorkspacePath before the first directory
   creation. The guard walks existing ancestors root-first, including reparse/link entries, even
   when the requested leaf does not exist. It retains textual/protected-location/repository/empty
   checks, then validates the newly created root and inputs directory before fixture writes.
2. ChapterSpinFacts includes the retained frozen At timestamp. SemanticDiff adds an explicit
   hash-only format.unexplainedCanonicalDifference marker when canonical inequality has no
   displayed explanation, or when apparent clock-only facts omit a non-clock canonical change.
   The clock-only inference compares all non-clock root canonical fields rather than assuming
   the projected Facts are exhaustive. No raw opaque payload is added to reports.
3. Integrity treats length mismatch as sufficient evidence without opening/reading that file.
   If metadata matches, it verifies the reopened handle length, bounds reads to original bytes
   plus one sentinel and rechecks length after reading. Large replacements and racing growth
   cannot cause an unbounded final hash. Final ancestor/file link checks also refuse verification.
   Same-length changes still receive a bounded byte hash. Unverifiable input gives intact=false;
   sha256After remains null when length/read/link verification refuses. Public CLI API is unchanged.

ReadBounded is shared by initial and final reads. An internal opener seam permits deterministic
replacement/growth regression cases; ordinary callers always use the normal read-only FileStream.
This is bounded checking of explicit copies, not an atomic promise against arbitrary future
filesystem changes. Windows FileShare.Read is retained; the tool does not follow live save stores.

## Verification

The original45-case receipt remains in HANDOFF.md and was reused as prior unchanged-check
evidence. No separate pre-fix baseline run, Core acceptance campaign or source mutation campaign was started.
The fix test project references the actual tool assembly; its friend access is internal-only.
Obj/bin for the test project have a distinct X: output path, avoiding collisions with tool builds.

Focused run: **5/5 passed**, no skips:

- A missing multi-level workspace beneath a real private directory symlink refuses before any
  target creation/fixture write. A sentinel/target listing remains unchanged. A normal absent
  workspace is created and validated. The target substitutes for a protected/repository location;
  no real repository target or LocalLow directory is used for a write test.
- Both timestamp-only snapshots pass the unchanged production decoder/validator; the independent
  literal r8 fixture has lastSpin.chapter.at2000, and the changed valid fixture has2001. Diff is
  equal=false/changeCount1 at rngCarry.lastSpin.components.at, with no clock-only inference.
  Sparse projection fixtures separately exercise hash fallback and hidden non-clock coverage.
- Oversized and truncated changed files cause zero reopen calls and no final hash.
- Replacement after metadata checking causes zero byte reads; simulated racing growth reads
  exactly originalLength+1, never the entire larger stream.
- Same-length changed bytes are detected; an input replaced by a real private file symlink is
  not opened, and its private target stays unchanged.

One final integrated self-test: **50/50 cases passed, 198 CLI runs, zero failures/skips**,
repositoryOriginalsUnchanged=true. This is the only integrated self-test performed for this fix;
it includes the original45 cases and the new5. Build:0 errors/0 warnings (.NET10.0.302).

Commands from the new worktree:

```powershell
dotnet build tools/SaveInspector/tests/FixChecks.csproj -c Release --disable-build-servers -nodeReuse:false
dotnet .work/save-inspector-fix-checks/bin/Release/net10.0/SaveInspector.FixChecks.dll X:/tmp/jtn-save-inspector-fixes04 X:/tmp/jtn-save-inspector-fix-evidence04/focused-r1
dotnet .work/save-inspector/bin/Release/net10.0/junk-save-inspector.dll self-test --repo X:/tmp/jtn-save-inspector-fixes04 --workspace X:/tmp/jtn-save-inspector-fix-evidence04/integrated-final --format json
```

Process-local TEMP/TMP, DOTNET_CLI_HOME and NUGET_PACKAGES point into this worktree's .work on X:.
The ASP.NET first-run certificate step was disabled for this process; no certificate/config change
was requested. No build server was started. Receipts: .work/logs/focused-r1.json and integrated-final.json.
First failures: no build/test assertion failed. Initial source reads used the wrong directory level
before src was located; the thread inventory initially requested a limit above the tool's maximum
and was corrected. These did not mutate source or trigger extra test runs.

## Storage and frozen review

Before C:8.40GiB/X:86.26GiB; after integrated checks C:6.60GiB/X:86.10GiB. These are machine-wide
concurrent measurements. This task's outputs are on X:: tool obj/bin1,255,332bytes; focused-test
obj/bin990,948; logs50,910; process temp9,186; dotnet home4,645. External private fixture evidence:
focused208bytes, integrated422,895bytes. No uncontrolled task diagnostic growth was observed.
The C: decrease is not attributed to this task or diagnosed here; no further heavy batch is planned.
The shared repository config timestamp remained2026-10-28T22:55:17Z; no config command was issued.

Retain receipts and sentinel fixtures. Disposable closed task logs/temp have a seven-day retention
and8MiB log budget before reuse; this is a manual bounded policy, not installed automatic rotation.
No cleanup is needed for this delivery. All private test links were explicitly unlinked without
recursive traversal of their targets.

Next action is independent07 review of the immutable fix commit, then the coordinator's deliberate
tool integration decision. Nothing is merged, pushed, published or integrated into the game here.
