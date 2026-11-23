# Command Sequence Lab

Bounded deterministic command histories against the actual production pure C# game.
No packages, Unity import, server, player saves, file faults or native acceptance.

Installed SDK: **10.0.302**, target **net10.0**. Run from PowerShell:

```powershell
Set-Location X:/tmp/jtn-command-sequence-lab/tools/CommandSequenceLab
./run.ps1 build
./run.ps1 check
./run.ps1 compat --output my-r11-compat
./run.ps1 replay evidence/r11/passing-trace-3.json
```

The script sets process-local TEMP/TMP, DOTNET_CLI_HOME and NUGET_PACKAGES on X:.
It disables CLI telemetry and development-certificate generation. Local NuGet.Config
clears package sources; builds use installed framework packs. It does not install an SDK.
Run `build` before the other modes. Direct DLL invocation requires this working directory.

Current r11 gameplay pin: `bd8fbe8e140627f0bbbd0763b174c8245457705d`.
The isolated branch uses that exact checkout as its source baseline.
All **45** linked files in Core, Contracts, Content, Effects, Upgrades, Orders and
Guidance match the qualified pin. Every invocation verifies source-manifest.json.
The accepted r9 tool at fd3d201 and its evidence are preserved: evidence/final/,
evidence/initial/ and evidence/qualification.json are historical r9 receipts, not
r11 claims. Its original manifest is evidence/r9/source-manifest.json. Old traces
must be replayed on their original tool/source pin; the r11 source guard deliberately
rejects a mismatched trace rather than rewriting its identity or expectations.
The clone has its own Git configuration; no Library, Editor, normal saves or
concurrent worker paths were used. Existing qualified regression/native evidence
was read and reused; existing full suites were not rerun.

## Inputs and command semantics

The explicit vocabulary is query, quote, buy, select, spin, enter, era-ack,
accept, cancel, claim, ack and reopen. `query` exercises read-only chapter,
guidance, available-project and skill quotation APIs. A quote command retains
the actual immutable request under its stable command ID; buy references that ID.
Retained spin/claim/skill requests and stale confirmations use explicit sequences.

Game seeds start at1; generation seeds at1001. A separately seeded LCG using
its upper24 bits chooses among legal actions and targeted refusals from current
state. Game RNG is never replaced. Main commands use fixed timestamp1000.
The starting snapshot is explicit; no offline-time model is introduced.

The r11 `compat` profile has exactly five histories of at most110 commands by
default: fresh37, full chapter without/with skills, and two legacy600 starts.
It reuses the unchanged generator algorithm and separate seeds. The600 starts
are explicit legacy envelopes at550 or595 coins, house1/selected2/seed1, with
no construction revision. The independent expectations are550+8=558 with the
old600 target still selected; explicit reselection then558-540=18; and595+8-600=3
with exact paid-spin replay unchanged. Query/reopen never silently adopt540.
New selections use V2; spin debits use the revision saved with the target.
The old catalog's600 remains intentional historical data. Actual file recovery
and pacing are owned by other streams. Existing smoke/explore modes remain
available but were not repeated for this update.

Seven fixture categories rotate: fresh37; funded20000; full chapter without
skills; full chapter with skills; and acquisition-spin starts for workshop,
recycler and loft. The latter three start with `40 + buildingPrice - 1` coins,
buy house40 through the public API, select the unaffordable building and spin.
`fresh37` names the starting economy only: it is a serialized existing envelope,
so the documented read-only projection has Existing origin. Actual fresh-creation
provenance is covered by the existing qualified regressions, not this campaign.
Funded fixtures are **synthetic**, not natural player progression. Later milestones
are built by public commands inside the recorded history, not by editing ownership.

Every command declares `commit`, `refuse` or `unchanged`. A return value alone
does not imply commitment. Expected refusals are not defects; unexpected
classification is a contract/harness candidate needing triage. Refusals and
retained replays compare the entire validated snapshot, storage bytes and write count.
See [ORACLES.md](ORACLES.md) for independent expectations and their limits.

## Budgets, replay and evidence

- Hard bounds:1..64 histories,110..150 commands each (default32/140),280-second
  exploration deadline plus at most20 seconds of failure reduction. Some state
  actions emit two commands; generation reserves capacity for them and final reopen.
- All generated reports together are capped at2MiB under artifacts/. At most two
  failing trace files are retained (first full trace and its reduced counterpart).
  A nonempty run directory refuses overwrite; `--output` selects a fresh short name.
  No automatic deletion or repeated campaigns occur.
- A single storage instance retains only primary and backup snapshots, each bounded
  by the strict codec's65536-character limit. A trace has at most150 commands.
- Build/temp output is ignored and disposable after the process exits. Retain at
  most7 days/64MiB of `.temp`, bin and obj diagnostics manually in this exact tool
  directory, skipping locked files/reparse points. Do not delete evidence/ or Git
  history. This is a manual policy, not automatic rotation.

Trace schema1 contains initial snapshot, fixture, both seeds, source-manifest hash,
stable command IDs/arguments/sequences/expected classifications, and per-command
canonical after-state SHA256 hashes. Final sample traces demonstrate exact replay.
Failure traces additionally preserve signature including the failing command ID,
detail and complete before/after snapshots. Replay input is capped at512KiB.
Replay returns0 when the recorded success or exact failure signature is reproduced;
it returns1 for divergence and2 for input/setup failures. The trace's explicit
commands, rather than the current generator version, define replay.

First failures are written before reduction. Only real state/accounting invariant
candidates are reduced, with at most80 deletion attempts. Stable IDs/sequences
remain unchanged; a candidate must reach the same failing command and signature.
Classification/setup/unexpected-exception candidates are preserved for triage
without automatic reduction. No failure occurred in qualification, so **no game
reduction ran**. A small independent integer-list predicate tests the reducer.

Summary schema1 is described in result.schema.json. The compat profile reports
five required construction-price categories; broad explore reports its original
gameplay categories. Counts are observations, not
exhaustive transition proofs. `missingRequired` enumerates declared categories not
reached in that run; it does not imply all possible behavior was tested. States
are distinct combinations of era, six levels, skill count, selected target and
active/ready order, not all unique serialized snapshots.

See [FINDINGS.md](FINDINGS.md) and evidence/r11/ for current results; the original
evidence/qualification.json remains the r9 receipt. Each scope retains its first
harness failure, source/output hashes and resource measurements. This tool
does not establish exhaustive correctness, Android durability, thread concurrency,
natural player behavior, probability fairness or fun.
