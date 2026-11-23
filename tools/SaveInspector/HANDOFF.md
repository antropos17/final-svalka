# Save Inspector handoff (2026-10-29)

## What runs

`junk-save-inspector` (net10.0 console app, `tools/SaveInspector/`) has four commands: `inspect`, `diff`, `preview-pending` and `self-test`. The first three work on explicit copies inside a `--workspace`. Startup:

```
dotnet build tools/SaveInspector/SaveInspector.csproj -c Release --disable-build-servers -nodeReuse:false
.work/save-inspector/bin/Release/net10.0/junk-save-inspector.exe self-test --repo <repo-root> --workspace <empty-dir-outside-repo>
```

It is not integrated into Unity, the runtime, diagnostics, or any other stream.

## Source pin

- Branch: `tools/save-inspector`, in the source-only sparse worktree `X:/tmp/jtn-save-inspector`.
  - The task started when the main checkout's HEAD was `8787e1f`.
  - By the time the worktree was created, someone else had integrated `5343928` and `a7ba454` (connected diagnostics).
  - The branch base is therefore `a7ba454eacd72d5b46bf3d714f1305547c0aed07`. Main later moved on to `563b437`; this branch was not rebased.
- `git diff c0863958b1bcf8d9fbdcc18957a5371192756733 a7ba454` over the seven linked directories is empty (re-checked after the commit). The 43 linked production files are therefore byte-identical to the r9 gameplay source pin `c086395` (packaging/evidence `a1f9bff`).
- Commits after the pin changed docs/evidence, `Checks/Diagnostics`, `Scripts/Diagnostics`, `Scripts/Runtime`, PlayMode tests and one `Resources/Editable` asset. The tool links none of these.
- Combined SHA-256 over the per-file `sha256sum` listing of those 43 files, in directory order Core, Contracts, Content, Effects, Upgrades, Orders, Guidance: `aa0cdfdd014eeb3941ce431c26d0250bfd1c4b8be83e8a1fdf10ec0ad25fe718`.
- The main checkout has someone else's work in progress, which this branch does not link, stage or touch:
  - modified: `Runtime/JunkGame*.cs`, PlayMode tests, `EditorPipelineConfig.json`
  - untracked: `JunkGame.Diagnostics.cs`, `outputs/`, `research/*`, `PLAN HERE.html`

## Private fixture identities (SHA-256 prefix, bytes)

Copied read-only from the repository. The originals' hashes were unchanged after the run.

| Fixture | Bytes | SHA-256 |
|---|---|---|
| r6/fresh | 184 | A8D9D1FE4C3B5E8D |
| r6/pending-finish-house-spin | 261 | 624C6BF2E0315DD3 |
| r6/committed-finish-house-spin | 241 | 24C135FE2B60D194 |
| r7/old-complete | 249 | 8FF3EE1172E75673 |
| r7/pending-effects-spin | 881 | B23A9D43F1EE4200 |
| R8/owned-universal-2 | 1321 | B17B5ABFC7D9DD1B |
| R8/pending-universal-2 | 2973 | D7461E38F5B80686 |
| R8/pending-all-skills-spin | 11655 | 829C64E63E0367D0 |
| R8/committed-all-skills-spin | 11667 | 6AA2FD4B3F47004A |
| V2Pricing/delivery2-price5-owned | 677 | 340A309C274398A8 |

The generated r9 inputs (production pure functions, seed 1, deterministic) were byte-identical across two separate self-test runs:

| Input | Bytes | SHA-256 |
|---|---|---|
| r9-complete | 957 | 557DDFFACADD3813 |
| r9-order-ready | 4791 | 3C7070C773E2BB99 |
| r9-claim-pending | 6130 | 8CC7C6E371CFE107 |
| r9-claimed | 4801 | A2277C816C6E35E2 |
| r9-reaccepted | 4806 | 61D31D78C9F67808 |
| r9-clock-only | 4809 | 1DFAF1C3F410ABFC |
| r8-mixed-v3-committed | 2200 | 193BF67859E4F1E8 |
| synthetic-v2-owned | 1390 | 814CFF890FB4A0A0 |

The rest are listed in the self-test JSON. None is a tracked fixture, and nothing was written to the repository's fixture tree.

## Checks run and results

1. **Build.** Release, 0 errors, 0 warnings.
2. **Self-test, first run** (`X:/tmp/jtn-save-inspector-work/out/selftest-run1.json`): 41/41 cases, 182 CLI invocations, exit 0, originals unchanged.
3. **Negative control.** Two oracles were deliberately set wrong: claim reward 121 and r7 coins 352. Self-test exited 7 with exactly three failures, `c06`, `c08` and `c14`, each showing the observed 351/120. Reverted afterwards (`selftest-negative-control.json`).
4. **Integrity detector.** Case `c39` was added because a self-test that passes first time must show its integrity check can fire. It shows an untouched input reported intact, then a new adjacent `jtn.recovery-primary.v1.json` and a changed byte both detected.
5. **Self-test, second run** (`selftest-run2.json`): 42/42 cases, 182 invocations, exit 0. The symbolic-link refusal case ran; it was not skipped.
6. **Review fix.** Independent review found that the diff's cancellation explanation compared two report objects by reference. It therefore never fired when an older claim was retained. It now compares claim sequences. Three cases were added:
   - `c16b`: cancelling instance B forfeits progress, adds no wallet change, keeps the older claim unchanged, and produces the cancellation inference.
   - `c21b`: a duplicate key inside the stored modern `journey` object.
   - `c21c`: a duplicate key inside the nested skill-ownership payload, which exercises `SkillOwnershipCodec`'s separate reader.
7. **Self-test, final run** (`selftest-run3.json`, fresh workspace `ws3`): 45/45 cases, 198 invocations, exit 0, originals unchanged. `dotnet run --project ... -- preview-pending` was also checked once: exit 0, claim walletDelta 120.
8. **Out-of-process exit codes from the built exe:**

   | Run | Exit |
   |---|---|
   | inspect | 0 |
   | inspect again | 0, identical output hash |
   | diff | 0 |
   | preview | 0 |
   | preview of a committed snapshot | 5 |
   | malformed snapshot | 4 |
   | over-bound input | 3 |
   | repository original path outside the workspace | 3 |
   | no arguments | 2 |
   | self-test into a non-empty workspace | 3 |

9. **Source grep.** The tool sources have no `DateTime`, `Stopwatch` or `TickCount`. `GameStore`, `GameSession`, `FileSnapshotStorage` and `ISnapshotStorage` appear only in comments and notes.

What the cases cover:

- **Legacy and modern.** An r6/r7 legacy save and an r9 journey save.
- **Pending versus committed, with exact wallets:**
  - r6: 595→4, and the replay hash equals the committed fixture.
  - r7: 340→351, carry 0→2.
  - r8 V1 universal2: −360, equal to the literal owned fixture.
  - r9 claim: +120.
  - V3 universal1: 540→420.
  - A committed snapshot gets exit 5, with no grant.
- **Price revisions.** V1 120/360 under codec 2. Mixed V1+V3: recycling1 at 160 under codec 3, with the old receipts unchanged and bound to the prior token. Literal V2 delivery2 at price 5. An all-skills spin leaves the token unchanged and equals its committed fixture.
- **Repeated order.** A new instance at 0/12 with the retained claim of 120; cancelling that new instance keeps the older claim. Wallet 1032 matches the STATUS claim observation 912→1032.
- **Formatting.** Pretty-printed and BOM-prefixed copies are semantically equal.
- **Rejections.** Truncated, duplicate legacy field, duplicate modern field (journey object and nested skills payload), unknown modern field, over-65536-character and invalid frozen legacy-spin/claim snapshots are rejected with no facts. A marker string planted in a rejected payload is never echoed.
- **Boundary refusals.** Directory, outside-workspace, alternate stream, device name, `\\?\`, missing file, symbolic link, over-byte-bound, and an `AppData\LocalLow` string check.
- **Clock-only diff.** It changes only `clockSequence`, with no income wording.
- **Diff checks.** A literal pending→committed diff passes the computed replay equality. Journey adoption is reported as metadata now stored.

## Disk and output

| Check | Value |
|---|---|
| Free space at start | C: 9.2G, X: 87G |
| Free space at end | C: 8.5G, X: 87G (C: dipped to 8.4G mid-session) |
| `.work/save-inspector` (obj/bin) | 1.3M |
| Task directory `X:/tmp/jtn-save-inspector-work` (workspaces, outputs, caches) | 0.53M |
| Sparse worktree | 27M |

TEMP, TMP, DOTNET_CLI_HOME and NUGET_PACKAGES were configured process-local on X:, and the build ran with `--disable-build-servers`. The C: drift is not attributed to any cause, and it was not investigated. No global setting was changed and no cleanup was done.

**Shared repository side effect.** The shared `X:/PROJECTS/FINAL-SVALKA/.git/config` now has `extensions.worktreeConfig=true`. It was most likely set by `git sparse-checkout init` in this worktree; the earlier value cannot be confirmed. It was not reverted, because the sparse worktree's `config.worktree` depends on it. The main checkout's `core.sparseCheckout` is unset, so the main checkout is not sparse.

**Cleanup**, when the branch is no longer needed and after merging or discarding it:

```
git -C X:/PROJECTS/FINAL-SVALKA worktree remove X:/tmp/jtn-save-inspector
git -C X:/PROJECTS/FINAL-SVALKA branch -D tools/save-inspector
rm -rf X:/tmp/jtn-save-inspector-work
```

## First failures (preserved)

- **First smoke run**, before the self-test existed:
  - The diff reported a spurious `rngCarry.lastSpin null -> null` entry whenever a null became an object. Fixed: descendant-aware flattening.
  - The text output joined long note strings with commas on one line. Fixed: long or comma-containing strings are listed one per line.
- **Pin statement.** The first handoff wrongly said that only docs/diagnostics changed after the pin, and named the base as `8787e1f`. It was corrected after `git diff --name-only` and the actual branch parent were checked.
- **Cancellation inference.** It silently failed when a claim was retained (reference comparison), as described above. The first self-test passed because no case covered it.

## Limitations and unsupported cases

- `preview-pending` is supported for every pending kind that production `GameLogic.ApplyPending` accepts.
- Recovery semantics beyond that replay are not modelled: backup validation, interrupted-write quarantine and recovery-evidence blocking. The replay does not establish Android filesystem durability or real recovery success.
- Hard links are not detectable cheaply and are not refused.
- No literal r9 journey snapshot exists in the repository corpus. The r9 cases use snapshots generated in memory by production pure functions, not historical device saves.
- No physical-device or Unity Editor evidence is involved.

## Next action

Optional: if this tool should be kept, have the coordinator review and merge branch `tools/save-inspector` (three commits, `tools/SaveInspector/` only). Nothing was pushed, merged or published.
