# One pre-frame hitch presentation check

Prepared during the finite29 October2026 window; hard stop13:30:12Z /17:30:12 Asia/Baku. Parent source is the test-only expected-refusal increment, preserving its first0/2 path-guard failure and correction. Own only NEW `ForegroundHitchPresentationTests.cs` (+meta) and this handoff. No Runtime/Core/scene or old test change, compile/import/Editor/APK/process setting change, or source mutation campaign is included.

## Observable result and exact timing boundary

The sole UnityTest is `JunkToNeon.Tests.PlayMode.ForegroundHitchPresentationTests.PrecedingRealFrameHitchCannotConsumeNewSpinPresentation`.

It uses the existing qualified private-save/additive-scene factory and a legitimate r11 selected540 house goal: seed1,160 coins, repaired house, no workshop, no skills. The case yields a real frame, sleeps4000ms on the test-runner/main thread once, then yields the next actual frame. It records measured hitch wall time, `Time.unscaledDeltaTime` immediately before Spin and the actual frame numbers. No ScriptEval start or artificial delta/time-scale/RNG alteration is used.

A fresh Stopwatch begins **before invoking the actual public `JunkGame.Spin` handler**, continuing until Busy becomes false, bounded by8s. The pointer helper is intentionally not called at this precise point because it yields another frame, which would discard the hitch-frame delta being tested. The whole case is bounded at18s including setup/4s hitch/presentation; expected wall time is approximately8s plus scene/setup overhead.

Independent outcome checks run before the timing assertion: exactly one pair8 payout (160->168), `[0,0,2]`, seed2647435461, one sequence increment, no bonus/pending, house1 and selected540 unchanged, full actual private primary equals encoded live state, AUTO/background false and observed frame progress. The required handler-through-Busy=false wall time is **>=3.45s**, allowing50ms tolerance on the3.5s presentation contract. The authorized hitch itself must measure>=3.8s. An unexpectedly backgrounded case fails its assumption rather than claiming a foreground timing defect.

## Why this is new and what is not established

Ordinary measured manual presentations near3.51s and the earlier retained0.124s eval-frame anomaly do not establish behavior after a preceding real4s frame hitch. Source `Present` currently adds the frame's entire `unscaledDeltaTime` on its initial StartCoroutine step; this is a hypothesis to test, not an already reproduced gameplay defect. The new test provides one actual controlled reproducer with its start boundary measured independently of ScriptEval.

No heavy compilation or PlayMode execution was performed by05 for this source preparation, per02's instruction. **There is no passing or failing runtime verdict yet.**02 owns the existing Editor and runs only this new class once after restoring normal preview and protecting it again with the established private-save workflow. Do not rerun unrelated timing/Core/UI suites. Record the first result and measured deltas. If it demonstrates a defect, agree exact Runtime ownership, freeze the smallest fix for independent07 review and run only directly affected checks; do not weaken3.5s, persistence or sequence requirements.

Inherited setup verifies every actual storage path belongs to its unique X: fixture. Failed fixtures are preserved; inherited teardown still unloads only its additive scene and restores save/time overrides. Normal progress is not restored or rewritten. Outputs stay in bounded X: task directories with manual7-day/8MiB inspection and receipt preservation; no automatic retention or native Android timing/lifecycle acceptance is claimed.

## Actual hitch finding and one no-frame-background extension

02 ran the original single case once: **1/1 failed in4.63s**, with actual before-Spin-through-Busy=false wall0.043789s. All independently fixed pair8/168 coins/seed/sequence/target540/full-primary assertions passed before the timing assertion. This reproduces a production presentation-duration defect, unlike the earlier eval-frame-only observation.02 owns the clock fix in `JunkGame.cs` and its frozen independent07 review;05 has not changed Runtime or rerun the case.

One additional test, `BackgroundWithoutFramesCannotConsumeRemainingForegroundPresentation`, qualifies the pause anchor around that fix. It starts a real Spin, calls OnApplicationPause(true), sleeps4000ms without yielding, calls OnApplicationPause(false) in the SAME engine frame, then yields the next real frame. It asserts Busy is still true and total foreground time from before Spin through Busy=false (excluding the measured background sleep) is>=3.45s. Both callbacks retain the same injected UTC1000, so the entire committed economic snapshot must remain identical across the absence. The independently expected pair8/168/seed2647435461/target540/one sequence/full-primary invariants are checked again. No production clock implementation is duplicated in the test, and no platform time/delta is fabricated.

This new test is prepared source only, with no heavy compile/import/Editor run by05.02 will execute the original hitch, this one added no-frame case, and only affected CombinedLifecycle tests after the reviewed fix. The original single-case request and first failed result remain preserved; the class now contains exactly TWO UnityTests for that specifically demonstrated clock issue. This models callback/frame absence, not native Android execution.

## Connected qualification

The original single real-frame hitch case failed at0.043789s before the new foreground presentation completed, while all independent economic/primary assertions passed. Frozen Runtime fix76a379f is accepted by07 after read-only source and receipt review. Post-fix two-case class passes2/2 in15.79s, including a4s no-frame background interval resumed in the same frame; both enforce foreground>=3.45s. Affected combined class4/4 passes13.83s; Core49/EditMode11/compile0/0 pass. Runtime source is unchanged from the reviewed freeze. Evidence: docs/evidence/unity/overnight-first-wave-20261029/verification.json. No physical-device timing/lifecycle qualification follows.
