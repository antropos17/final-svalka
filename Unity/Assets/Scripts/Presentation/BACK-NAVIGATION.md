# Back navigation adapter handoff

Base: primary `0f7a80072cdfbca93c842c6978ba4e04b2ba1c2b`, inspected on 2026-10-29 (Asia/Baku). Previous layout/test history remains on `codex/jtn-layout-completed`; this clean warmed worktree was switched to the verified primary base without resetting/cleaning it.

## Demonstrated gap and scope

The existing `JunkGame.Update` Escape branch calls `ClosePanel()` whenever the panel is active. With a corrupt primary and a valid backup, `ClosePanel` returns immediately because `failed` is true. Thus a restore confirmation stays open. Its safe Cancel action already exists in `Reload`: when `restoreConfirmation` is true it returns to `Error(Session?.Error)` without opening the store or writing snapshots. The new real-scene test reproduces the ignored Close action, then routes Back to that existing Cancel and checks every fixture file byte-for-byte.

`JtnBackNavigation` is a small callback adapter with four contexts: root, dismissible panel, restore confirmation and blocking error. It consumes at most one request per frame, stops AUTO through the supplied callback, closes only a dismissible panel, cancels only a restore confirmation and leaves blocking recovery UI intact. Root may request the existing exit confirmation; the adapter never calls Quit, Spin, SelectProject, RestoreBackup, save APIs or a backend. It uses the already-installed Input System Escape path, without new packages or legacy Input Manager settings. A separate small assembly allows focused tests to reference it without restructuring Assembly-CSharp.

**Original worker handoff:** the adapter was not wired in that isolated commit. The primary integration described below now connects it; the following snippet records the integration contract.

## Minimal integration owned by chat02

At the end of `JunkGame.Awake`, after `OpenSession()`, configure the component once. The callbacks have access to existing private fields/methods because this snippet belongs inside `JunkGame`:

```csharp
if (!TryGetComponent<JunkToNeon.Presentation.JtnBackNavigation>(out var back))
    back = gameObject.AddComponent<JunkToNeon.Presentation.JtnBackNavigation>();
back.Configure(
    () => failed
        ? (restoreConfirmation
            ? JunkToNeon.Presentation.BackContext.RestoreConfirmation
            : JunkToNeon.Presentation.BackContext.BlockingError)
        : (panel.activeSelf
            ? JunkToNeon.Presentation.BackContext.DismissiblePanel
            : JunkToNeon.Presentation.BackContext.Root),
    ClosePanel,
    Reload,
    () => {
        exitPanel = true;
        panelTitle.text = "Выйти из игры?";
        panelBody.text = "Результаты уже сохранены.";
        selectButton.gameObject.SetActive(true);
        selectButton.GetComponentInChildren<TMP_Text>().text = "Выйти";
        reloadButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(true);
        panel.SetActive(true);
    },
    StopAuto);
```

## Primary integration verification (r6)

The primary runtime now configures the adapter in Awake and uses ShowExitConfirmation for its root callback. The previous Escape poll in JunkGame.Update was removed; only the adapter polls that key. Existing panel cancellation and recovery callbacks remain authoritative.

Primary PlayMode passed 9/9 in 14.29 seconds, including ConnectedEscapeRoutesRootAndRestoreCancelWithoutDuplicateHandling. That case queues a private Input System Keyboard state while the Game View is focused and reaches the production adapter Update and actual Awake callbacks: root opens once, idle frames do not repeat, the next Escape closes, and restore cancellation returns to the blocking error without changing any owned fixture bytes. No real OS Escape or Input System settings change was used. The earlier isolated-worker input limitation and failures below remain historical evidence; queued Editor input is now qualified. Physical Android Back delivery remains unverified. Receipts: docs/evidence/unity/back-connected-primary-r6.json and integration-r6-playmode.json.

Remove only the old `if (Keyboard.current?.escapeKey.wasPressedThisFrame...)` block from `JunkGame.Update` (the current source uses the fully qualified Keyboard name). Keep frame counting, safe-area refresh and other Update behavior. **Do not leave both Escape polls active:** otherwise the game and adapter may handle the same key independently. No scene edit is necessary with this Awake wiring. Keep the recovery stream's current field/method names and equivalent root confirmation behavior if its concurrent changes require adjusting the snippet. ClosePanel continues to acknowledge an AUTO hint through the existing economic protocol; the adapter does not invent another hint path. Exit confirmation still requires its explicit button action; Back itself only opens/closes it.

After integrating, verify ordinary preview → Back closes without a command; restore warning → Back cancels to the blocked error with no file writes; blocked error remains blocked; root → Back opens the exit prompt and another Back closes it. Do not synthesize a real OS Escape with computer automation. Use adapter requests or queued Input System events in a private Editor fixture.

## Input evidence and limits

Inspected installed `com.unity.inputsystem` 1.20.0, its `Documentation~/query-keyboards.md`/keyboard controls and the current `activeInputHandler: 1` (new Input System). Existing runtime polls `Keyboard.current.escapeKey.wasPressedThisFrame`; this adapter preserves that input contract. Two attempts to deliver a private queued Keyboard event in the unfocused batch Editor did not establish the player key state. The installed InputManager selects an Editor update buffer when the game lacks focus; this is a plausible scheduling cause, not a qualified native-input diagnosis. No Input System settings were changed to force delivery. Final tests call `RequestBack()` (explicitly allowed for this task) and verify routing, same-frame deduplication, idle frames and a disabled component. The automatic Escape poll is source-reviewed but remains unqualified by these tests. `RequestBack()` also remains available for an existing platform/UI callback if one is integrated later.

Unity documents separate Android Back/gesture behavior in [UUM-139263](https://issuetracker.unity.com/issues/10034/the-side-back-gesture-does-not-exit-the-player-when-using-gesture-navigation-mode). No legacy `Input.backButtonLeavesApp`, Active Input Handling change, Java activity hook, plugin or package was added. If a platform backend does not deliver Back as Escape, this adapter cannot manufacture that event. Physical Android delivery/lifecycle remains unverified; no phone/emulator installation or APK build was attempted.

## Verification and first failures

See the focused test folder's `verification.json` for actual final counts and source hashes. Tests use the real saved scene for preview/recovery cases, private fixture directories and no production debug hook. The cancel fixture contains exactly the corrupt primary and a valid 91-coin backup: no preservation/promotion/reset files may appear. Root/preview retains 37 coins, sequence 0, no selection and no spin receipt. Same-frame duplicate and later harmless requests are covered. The existing pointer/lifecycle tests were included in the first run.

The first seven-case run was 5 passed / 2 failed: the queued-key case did not reach the adapter, and the old repeated-SPIN case passed its behavioral assertions but failed a whole-ordinary-directory teardown comparison. A second focused run was 1 passed / 2 failed: the preview's ordinary-directory guard observed a concurrent pending-write file, and explicit queued-key processing still did not establish the player key state. Both original verdicts are retained under `.work/back/playmode-first-verdict.json` and `playmode-second-verdict.json`. The integration owner then confirmed the primary Editor was legitimately running AUTO against the ordinary directory. No normal save was restored/overwritten; that Editor was not operated or stopped here.

The corrected invariant verifies every actual private string path in each loaded `FileSnapshotStorage` (all eight normal/recovery paths), including the authored-scene clone, and the old UI tests compare the complete committed private snapshot against their session state. The Back cases additionally require byte-identical owned fixture files and no extra recovery-write files. Ordinary-directory changes remain a read-only, race-tolerant logged observation rather than an invalid assertion about another process. Wrong session paths or mismatched owned snapshots still fail. The final full PlayMode run passed **7/7** (three Back plus four existing UI cases, 9.65 seconds); this rerun was warranted by the changed storage assertions. Compilation passed **0 errors/0 warnings** and EditMode **1/1**. Actual Editor process audit confirmed Path.GetTempPath, TEMP/TMP and Application.temporaryCachePath all resolve to the task's X: tree; a unique `back-editor-41400` baseline override guarded even idle/transition time before that final run. No recovery/core/scene code changed.

The existing task Library/Logs and process Temp remain under unique `X:/tmp/jtn-ui-lifecycle-20261029`; no SDK/toolchain was copied. Before reuse, inspect the exact task folder; closed disposable logs/Temp have a seven-day retention and 8 MiB log budget. Keep verification receipts and skip locks/reparse points. This is a bounded task policy, not installed automatic rotation. Final storage and shutdown observations are in the receipt.
