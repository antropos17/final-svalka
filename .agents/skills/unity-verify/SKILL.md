---
name: unity-verify
description: Verify changes to this project's Unity scene, runtime UI or game logic using the warm Editor, scoped checks and private saves before APK packaging.
---

# Unity verification

Use the installed `unity-cli` skill for command discovery. This project has a verified local beta14 CLI at `X:/PROJECTS/FINAL-SVALKA/.tooling/unity-cli/bin/unity.exe`; keep the selected version unless an actual compatibility issue requires a change. Run from the checkout root. For an isolated worktree, substitute its own exact Unity project path on every call and use its separate Library/Editor.

## Windows process readiness

For an Editor launch, inspect the intended child environment; after launch or when diagnosing Android SDK discovery, inspect the actual Editor process environment. `cmd.exe` ignores an inherited `PATH` longer than 8,191 characters; merely prepending System32 does not repair an oversized value. Use a short, validated process-only path containing the existing Windows system tools and required Unity/JDK tools, retaining the original values. Never change the machine/user PATH, reinstall an SDK or bypass its Java check to mask this failure. A zero SDK-manager exit code is insufficient: require actual installed-package rows; `findstr` errors or a Java-version warning without those rows are failures.

Before private UI checks, require the live Editor's `System.IO.Path.GetTempPath()` to resolve to the owned X: directory. Set `TEMP`, `TMP` and `TMPDIR` before Editor startup; their displayed values alone do not prove the effective path. If a warm Editor retains its earlier C: path after an observed environment correction, preserve scene/layout/saves and stop active operations before one coordinated close/reopen of the exact project with the corrected startup environment. Verify the old process exited, the new instance is ready and the effective temp path is X: before retrying. Preserve the first setup failure; do not weaken the private-path assertion or manipulate internal temp caches. Restore task-owned environment overrides at handoff.

## Warm Editor loop

Unity CLI is the primary execution interface by the human's 2026-11-01 instruction. Use the pinned CLI commands below directly through Pipeline; do not add MCP discovery before routine checks. A concrete CLI gap or blocking native dialog may justify a bounded fallback, recorded separately from verification. Keep beta.14; an older pasted help transcript is not a version-change instruction. Use warm-Editor test/build entrypoints when the project is already open rather than starting a second batch Editor.

1. Inspect `git status --short`, free space and the active Editor log before imports/builds. Preserve normal saves and user files. Reuse matching evidence when source and relevant conditions are unchanged.
2. Target the primary explicitly; keep one warm Editor for integration:

   ```powershell
   & .\.tooling\unity-cli\bin\unity.exe status --until-ready --timeout 45 --project-path X:/PROJECTS/FINAL-SVALKA/Unity --format json
   & .\.tooling\unity-cli\bin\unity.exe recompile --project-path X:/PROJECTS/FINAL-SVALKA/Unity --format json
   & .\.tooling\unity-cli\bin\unity.exe status --until-ready --timeout 45 --project-path X:/PROJECTS/FINAL-SVALKA/Unity --format json
   ```

   Read the returned error code and diagnostics. Import/domain reload can temporarily disconnect Pipeline. A modal or Safe Mode needs inspection; an empty catalog alone does not justify opening a duplicate Editor. Inspect the actual process/project before reopening a closed Editor. Keep the first failure visible; retry only after its cause or readiness changed.
3. Run the directly affected checks. For economy/persistence edits use `dotnet run --project Checks/CoreChecks.csproj`; for integrated game changes the warm Editor test command is:

   ```powershell
   & .\.tooling\unity-cli\bin\unity.exe command test run --caller plugin --skill unity-cli --project-path X:/PROJECTS/FINAL-SVALKA/Unity --format json -- --mode editor --filter JunkToNeon
   ```

   Require zero compilation errors and an explicit passing test summary. For safe-area wiring, run `command script eval_file` with `--file X:/PROJECTS/FINAL-SVALKA/Checks/UnityPresentationChecks.cs` in Edit Mode and inspect `passed=true`; a successful transport alone is insufficient. Reuse unchanged core evidence for art-only changes.
4. Before Play checks, coordinate the selected Editor's integration ownership, stop Play and preserve the latest resulting normal progress. Back up/hash normal `jtn.*.json` files, then set `JTN_EDITOR_SAVE_DIRECTORY` inside that Editor process through `script eval` to a unique `.work/<task>/private-save` folder. This override exists only in Editor builds. Verify normal files remain identical during the private interval. Clear task-owned save and `JTN_EDITOR_UTC_SECONDS` overrides before returning to the normal preview. Fixtures and failure cases belong only in private storage. A changed normal file requires investigation, not copying an older backup over newer human progress.
5. Enter Play using `command editor.playmode start`, then wait for readiness with the bounded status command above. Enable `command editor.playmode set_run_in_background ... -- --enable true` only for Editor preview; it preserves the build's background setting. Confirm advancing `Time.frameCount` across separate observations and perform the relevant UI action with an independently expected state/result. Distinguish programmatic button handlers, simulated pointer input and physical-device input.
6. Keep Scene/Hierarchy on the left and the official non-foldable portrait Device Simulator on the right. After Game/Simulator switches, inspect `Screen.width`, `Screen.height`, safe anchors and visible content before capturing. Capture overlay UI through `script eval` with `UnityEngine.ScreenCapture.CaptureScreenshot("<absolute evidence PNG>");`, then inspect the saved image after a frame. Camera-only captures omit the Canvas; screenshot existence is not proof of layout or live behavior.

## Packaging checkpoint

Build only when the task calls for an updated APK, after source/scene are saved and checks resolve. Preserve the previous APK/receipt. Stop Play and set the Editor background override off before packaging. The established build entry point is `JunkToNeon.Editor.ProjectSetup.BuildAndroid()` through `command script eval`; pass `--timeout 600` after `--` to the Pipeline command. Keep shell waits short enough for progress updates.

If a request times out while a build may still run, inspect the existing Editor log, `BuildPipeline.isBuildingPlayer`, output timestamp/hash and final receipt. Wait for that operation; do not invoke another build merely because its request timed out. Record actual APK length/SHA256, source commit/input hashes and dirty state, result/errors and recorded duration. BuildReport aggregate output bytes are not the APK length.

Finish by resuming the preview against the current normal save, with task-owned overrides cleared, a scoped commit and current artifact identity. Do not raw-restore an earlier save or spin normal progress for verification. Actual save recovery is a separate explicitly requested operation. Editor/unit/build evidence does not establish Android touch, native pause/resume, Back, force termination or filesystem durability. Record those limits without launching another campaign.
