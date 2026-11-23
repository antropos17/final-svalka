# Scene UI and lifecycle regressions

Verified on 2026-10-29 (Asia/Baku), based on primary commit `d4cf353144e55b9e580ee11ef8c5b04ddf62f3f7`. Isolated worktree: `C:/Users/murtu/.codex/worktrees/jtn-ui-lifecycle/FINAL-SVALKA`. The previous art worktree/commit is preserved. Unity 6000.3.21f1; existing pinned CLI 1.0.0-beta.14 from the primary `.tooling` folder. No package, runtime, scene, audio, safe-area or persistence implementation changed.

## Coverage

The existing EditMode wrapper runs ten pure core regression groups; prior gameplay evidence exercised button delegates manually. These three tests add the missing automated saved-scene/pointer boundary:

1. Seven SPIN pointer clicks during one presentation commit exactly one operation: seed 1 gives potato/potato/trash, reward 8, balance 45, sequence 1. Frames advance; after presentation and another 0.5 seconds, no queued spin appears.
2. An explicitly affordable 640-coin fixture remains unchanged when preview is opened/closed. Selecting repair spends 40 once, leaving 600 at sequence 1. The hint remains after scene re-entry until its explicit acknowledgement (sequence 2), then stays acknowledged on another re-entry. No successor is selected despite affordable funds.
3. A valid private stage-1 fixture with 595 coins and stage 2 already selected receives the fixed 8-coin pair from AUTO: 595 + 8 - 600 = 3, stage 2, sequence 1. AUTO stops, its disabled button cannot advance state, and no purchase button is exposed. Scene unload/reload restores the exact receipt and reel sprites, acknowledged hint and AUTO-off state; optional manual spin remains available.

The real serialized scene is loaded additively through `EditorSceneManager.LoadSceneAsyncInPlayMode`. Pointer targets must be the first UI raycast hit, then receive uGUI pointer-down/up/click events. Each action waits one frame so newly activated panel graphics can participate. This is simulated uGUI pointer dispatch, not OS input or physical touch. Tests unload/reload the game scene within Play Mode; they do not simulate a process crash or Android suspend/kill. No production test hooks were added: narrow reflection accesses the existing runtime assembly's fields/properties because `JunkGame` belongs to Assembly-CSharp.

The test assembly and implementation require `UNITY_EDITOR` and `UNITY_INCLUDE_TESTS`; they are not production debug interfaces. Each case validates/writes one private fixture under the Editor's process Temp directory before scene load, sets the existing Editor-only `JTN_EDITOR_SAVE_DIRECTORY`, and restores it after unloading. Normal `jtn.*.json` files are compared byte-for-byte before/after; private snapshots are removed by exact filename. All final teardown checks passed and no private fixture directories remained.

## Actual verification

Final PlayMode: **3 passed, 0 failed, 0 skipped**, runner duration 9.27 seconds. Existing JunkToNeon EditMode: **1 passed, 0 failed**, wrapping all ten core groups. Standalone `dotnet run --project Checks/CoreChecks.csproj`: **10 groups passed**. Final Unity recompilation: **0 errors, 0 warnings** (`up_to_date` after automatic compilation). `verification.json` preserves the structured first/final verdicts and final-source SHA-256.

Failures are retained, not counted as product failures or hidden:

- Initial readiness windows expired while the same fresh import continued progressing; the existing process subsequently became ready. No duplicate Editor was opened.
- The first synchronous PlayMode request was rejected because HTTP cannot survive the Play Mode domain reload. It ran no tests. Async dispatch then ran the suite.
- First actual verdict: 1 passed / 2 failed. Both failures were `closeButton must be reachable by a UI raycast`: the test clicked a newly activated panel in the same frame. Waiting one frame between pointer actions fixed the test timing; the next full three-case run passed. No game behavior was bypassed or directly invoked to make it pass.
- One recompilation request during the runner's post-test transition returned HTTP 400. After readiness, recompilation returned zero errors/warnings. No assertion retry occurred on unchanged failing code.

## Run and integrate

Cherry-pick this scoped test commit onto the integrated primary; no scene regeneration or runtime merge is needed. Use a neutral test scene (the setup intentionally refuses an already-loaded JunkToNeon scene), stop ordinary Play Mode, and target the exact checkout's Editor. With the existing warm Pipeline:

```powershell
& <existing-unity-cli> command test run --caller plugin --skill unity-cli --project-path <exact-checkout>/Unity --format json -- --mode playmode --filter JunkToNeon.Tests.PlayMode --async_tests true --timeout 90
& <existing-unity-cli> command test status --caller plugin --skill unity-cli --project-path <exact-checkout>/Unity --format json
```

Wait for the completed status and inspect its nested summary; transport success alone is not a passing verdict. Run EditMode separately with `--mode editor --filter JunkToNeon`. Do not start a second Editor against a checkout already open elsewhere. The tested runtime/scene match the named base commit; later audio/recovery changes need their own relevant integration check.

## Storage and boundaries

The task Editor (PID 57248) was opened once in batch mode against this worktree and closed gracefully after checks. Its `Library` and `Logs` junctions target unique `X:/tmp/jtn-ui-lifecycle-20261029/Library` and `Logs`; process TEMP/TMP target the sibling Temp. Actual final sizes: Library 1,414,217,918 bytes; Logs 1,240,028; process Temp 1,829; project Unity/Temp absent/0. Incidental Editor font/settings serialization was restored to the base; only new test files/metadata/evidence remain in the commit.

Free space initially C: 10,674,544,640 / X: 98,910,392,320 bytes; final C: 10,620,731,392 / X: 96,030,801,920. These are machine-wide concurrent measurements, not exclusive attribution to this task. No build/toolchain copy was made. Before any future reuse, inspect this exact task folder; disposable closed logs/Temp have a 7-day retention and 8 MiB log budget. Preserve receipts; skip locked files/reparse points. This is a stopped bounded session, not installed automatic rotation. The retained Library is reusable until the worktree is retired.

No primary Editor/save, Android APK, device installation, OS emulator, global setting or external service was used or changed. Android OS lifecycle, physical input, device safe areas and full-session pacing remain outside this evidence.

## Authored portrait layout increment — 2026-10-29

Based on test commit `d26d30224b05611b9c9bc9192db6e325bbddf1fe`. Edited the existing scene through this worktree's live Editor: compact title/era with right-aligned autosizing currency, a goal backplate near the house, one visible goal counter, more room for the Project control, and a padded modal card with a dimmed backdrop, visible cost explanation and distinct primary Start/secondary Close actions. Existing names, runtime serialized references, 60/40 safe-area split, prices and explicit selection remain. Three visual-only children were added: Goal Readout, Modal Backdrop and Project Card. The duplicate `projectPrice` TMP component is disabled but its serialized object/reference is retained; cost remains visible in the goal and preview text. No runtime/core/recovery code changed.

The scope follows source-plan chapters 08–09 and chat01's completed reference analysis: [Hooked Inc compact HUD](https://www.gamedexy.com/screens/220d6f1a-84f6-413e-84cc-b0f33e38c4ef), [Eatventure grouped upgrade information](https://www.gamedexy.com/screens/cdee7c47-c965-4880-94e4-f4907040436a). These are composition principles, without copied artwork or new mechanics. Dynamic target name and cost remain in the existing title/body fields; a separate dynamic price/state binding was not introduced.

Actual final checks: PlayMode **3/3** (9.15 seconds), now also asserting visible TMP overflow, button bounds and 60/40 in all existing state checkpoints; EditMode **1/1**; Unity compilation **0 errors/0 warnings**; existing safe-area harness **4000 samples**, padding/resume/single fitter passed. Reused the unchanged core implementation's earlier standalone ten-group evidence. The 540x960 Game View and standard non-foldable Punch Hole Left 1080x2340 Device Simulator were rendered; screenshots and button/text measurements passed. A preliminary short viewport was 540x989 before its window was sized to 16:9. A private `long.MaxValue` balance rendered its exact 19-digit text without overflow (autosized font 10.7); no spin was performed on that boundary fixture. Long preview opening retained 37 coins and sequence 0, with advancing frames 63 → 1101 → 1160. Two normal snapshots remained byte-identical and the private-save override was cleared. See `docs/evidence/unity/layout-verification.json` and `layout-{before,after,project-short,project-long}.png`.

**Integration correction still owned by chat02:** `Runtime/JunkGame.cs`, `Awake`, unconditionally assigns `stageLabel.rectTransform.anchorMin=(.06,.51)` and `anchorMax=(.95,.56)`. This scene deliberately saves those same anchors with zero offsets, and its actual Play rendering was checked. Arbitrary authored Inspector placement is still overridden: do not claim that defect is fixed. The minimal correction is to apply those two fallback assignments only when `!sceneUiAuthored`, preserving authored anchors/offsets. Retain the independent reviewer's finding and verify the merged runtime separately. The visual layout itself needs no conflicting runtime rewrite; cherry-pick this scene/test increment while preserving the recovery stream, then exercise its added recovery controls against the merged card.

Actual operational failures: the first header edit script could not find the era object because its literal name contains `/`, which `Transform.Find` treats as a path; it failed before edits, then exact-name lookup succeeded. A native floating-tab drag was rejected by window targeting; only the task Editor's own docking API was subsequently used. One local patch was rejected before any file change. After test compilation, two immediate Pipeline requests arrived during domain reload and were rejected before execution; readiness restored and the test dispatch succeeded. No final test assertion failed. No global Game View resolution preset was added: the short viewport used only the task window's size/target. The close request timed out after 20 seconds in Cleanup mono/abort_threads; that same task process subsequently exited by itself, without force or a duplicate Editor. Generated font/settings serialization was excluded. The existing X: cache/log locations and seven-day/8 MiB reuse retention remain; measured final sizes/free space are in the receipt. These are Editor/fixture checks, not device dp/touch or Android OS acceptance; no installation was attempted.


## Primary authored-placement correction

Primary integration858c0b1 removes the unconditional Awake marker-anchor reset identified on frozenfa7835640dd282dfe34d67ccf5ce89250aea3b10. The fourth case, AuthoredGoalPlacementSurvivesAwake, sets independent anchors/padding on an inactive serialized Canvas clone before its first Awake. It failed on the original runtime and passes after the narrow correction. The later combined primary layout/recovery suite passes4/4; see docs/evidence/unity/integration-r5-final-playmode.json. The isolated-layout warning above describes its older baseline, not the corrected primary runtime.

## Parallel save-path audit — Back increment

The Back increment based on primary `0f7a800` replaces the invalid whole-ordinary-directory equality guard with assertions on every actual FileSnapshotStorage path (including the authored clone) and the full committed private snapshot. Concurrent ordinary-save changes remain a read-only logged observation: the integration owner confirmed primary AUTO writes there legitimately. No ordinary save was restored or overwritten. Process Temp, temporaryCache and per-case GUID folders were verified on the task-owned X: tree. See `../../Scripts/Presentation/BACK-NAVIGATION.md` for the adapter, first failures, integration steps and Android-input limits.
