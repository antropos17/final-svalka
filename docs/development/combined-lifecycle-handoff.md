# Combined gameplay/lifecycle regression handoff

Source-only baseline `1b15ed91078480a9bdcdf3883c99d90d6cb42327`, isolated `X:/tmp/jtn-gameplay-lifecycle05`. Own only NEW `Unity/Assets/Tests/PlayMode/CombinedLifecycleTests.cs` (+meta) and this handoff. No existing tests, Runtime/Core/Chapter files, scene, Editor, Library, APK, personal progress or settings changed. The existing PlayMode assembly and private-save/uGUI input approach are reused. Project `unity-verify` guides the integration-only warm Editor run; no additional Editor may be opened.

## Coverage gap and observable results

Existing `ReelUiLifecycleTests` separately covers repeated spin input, ordinary final AUTO completion, idle offline return, idle pause/quit, order claim/reload and order-panel Back. `BackNavigationTests` covers actual queued Escape and recovery-warning cancellation. Those cases are not repeated here. The two new scenarios combine a still-busy committed presentation with return income and panel eligibility.

| Filtered method | Independent expected result |
| --- | --- |
| `AutoPresentationReturnDefersAffordableConstructionAndCannotRestartItself` | Modern setup selects house improvement with wallet=current catalog price minus10. AUTO seed1 commits pair8+workshop1 once, leaving price-1. Explicit pause callbacks keep presentation Busy for more than its full3.5s duration while frames advance and AUTO stops. Resume after600 simulated seconds grants10 once, reaching price+9, without buying the now-affordable selected house. Return summary waits for presentation. Reentrant Spin/ToggleAuto and repeated resume commit nothing. Back acknowledges only the return receipt; after the AUTO gap no queued spin, purchase, exit confirmation or restart occurs. |
| `ReadyOrderDuringPresentationReturnAndBackNeverClaimsItsReward` | All24 actual construction selections use current catalog prices. Fifteen seed1 spins yield11 trash, carry1, seed82514103 and352 cumulative coins under unchanged chapter effects. The visible manual spin16 is `[trash,pigeon,trash]`, pays20, advances seed2371448660, caps order progress11->12 and carry1->2. Pause/resume adds10 without more spin/order/carry progress. Return summary has priority; Back acknowledges it. The now-ready order is inspected, then production Back closes it without claiming120 or cancelling. Wallet stays before-spin+30, lastSpin remains the one committed spin, AUTO/Busy/panel end false. |

The second case is deliberately manual: AUTO is unavailable after chapter completion, and an active post-chapter order cannot legally coexist with an unfinished chapter. No impossible AUTO/order fixture is manufactured. Era-transition and recovery-specific Back cases are outside these two additions; their unchanged existing evidence remains separate.

Price is queried from `ChapterCatalog.Rules`, not hard-coded600. The fixture uses explicit current selections before the UI action; no old selected600 quote is silently converted. If03 introduces a distinct active-catalog API,02 should adapt only this fixture's price helper to that authoritative API when merging, preserving independent +9/+10 expectations. Whole-chapter funding sums actual project definitions, so600->540 does not falsify fixed completion-wallet constants. Qualifying outcomes/effect amounts remain explicit independently expected literals.

## Control-flow review and isolation

`Spin` rejects Busy/background/panel state and sets Busy before starting its sole Present coroutine. `Present` pauses elapsed time in background, clears Busy once, then shows the pending return summary before considering AUTO. Pause stops AUTO before any protocol call. The cases assert the observable consequence (one committed spin, frozen result, no second queued spin), not a fabricated internal coroutine-count API. No concrete Runtime defect was established by source inspection, so no speculative Runtime patch is proposed.

The setup requires a neutral scene, backs up process-local save/clock overrides, loads only its additive game scene and asserts **every actual FileSnapshotStorage path** equals the unique project-local X: fixture. It never reads or rewrites ordinary progress. Teardown unloads only that scene and restores both overrides, even after setup assertions. Failed fixtures are preserved. Successful cleanup deletes only four exact known regular private files, skips reparse targets, and removes the empty directory non-recursively. Disposable task output has a manual7-day/8MiB inspection budget; no automatic cleanup/rotation is claimed.

## Evidence and smallest integration run

Compiler-only verification used installed Unity6000.3.21f1's Roslyn and the existing warm project's PlayMode reference list, reading references without copying/importing Library. Final compilation passed0 errors/0 warnings. The first command produced only CS2023 because `-noconfig` was placed inside its response file; moving that compiler option to the command line removed it. Raw first/final logs, response and input hashes remain under `.work/source-check/` in the isolated checkout (about57KiB). This is source compilation, **not Unity test execution or live lifecycle acceptance**. No PlayMode case was run by05.

Test source SHA256: `86A9906A3D024FAEFC130BD11289A8239C238E7EF40D80C42343EB6C1A5EAD38`.

Pinned Runtime source SHA256 (from the isolated source baseline):

| File | SHA256 |
| --- | --- |
| JunkGame.cs | D448C5ACEF91719A9143A60ABAEEAB047E5B62E012E65E3A6BC15E9DB0305EB1 |
| JunkGame.Chapter.cs | BE8F6BC0E050ADD922DD3215904CD4ED0A3274701D4A3C758F46CB7E1E6B701B |
| JunkGame.Journey.cs | 78C82409BB28CE12726F68FF3F8573F411658892CB36142857BB1871C536E750 |
| JunkGame.Wave.cs | 4360B45481A66BD1BA520B28C8B4B58C17492BC44E608F7A1D76E7CF465733B6 |
| JunkGame.Diagnostics.cs | E5BE3150339E78CF2C4EA8CE71E4FD4A264880E453816F2019882EC3362E29D5 |

After02's performance benchmark freeze and intended source merges, import the new test source into the **existing primary Editor** and run only filter `JunkToNeon.Tests.PlayMode.CombinedLifecycleTests` (two UnityTests) from its neutral test scene. Use the existing per-process private-save protection and normal-save backup/hash loop. Preserve the first actual verdict; change a fixture or Runtime only for a demonstrated failure, with Runtime ownership agreed with02. Record exact integrated source hashes and results separately. Do not rerun the unchanged full PlayMode/Core suite for this test-only addition.

Remaining: primary PlayMode execution and any demonstrated corrective follow-up. Callback simulation, pointer dispatch and a production Back adapter call do not establish Android OS backgrounding or physical input acceptance.

## Primary integration acceptance

Integrated as09d391b, then fixture current-price lookup adapted to ChapterEconomy.ProjectPrice at bd8fbe8. Both original combined lifecycle cases pass in the primary warm Editor. Two additional affected UI cases pass: pinned600/current540 explicit reselection and qualifying-box completion heading. One focused class run passes4/4 in13.69s; no unrelated PlayMode suite was repeated. Core49, EditMode11 and307 pricing assertions separately pass. Private saves and simulated uGUI pointer/programmatic pause/Back do not qualify physical Android lifecycle/input. Evidence: docs/evidence/unity/integration-r11/verification.json.
