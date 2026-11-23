# Independent prototype audit and bounded parallel plan

Date: 2026-11-14 (Asia/Baku). Scope: read-only review of the existing Unity prototype; implementation and agent launch are not authorized by this audit task.

## Result and decision

No new critical gameplay, reward duplication, save corruption or purchase-consent defect was established in the inspected slices. This is not an exhaustive correctness certificate. Prioritize three modest work packages: navigation clarity, root-screen visual hierarchy, and measured render work. Use a fourth role for integration and acceptance. Do not invent ten assignments or begin another economy redesign.

One low-priority unreachable code branch is confirmed. Repeated snapshot copies are source-confirmed unnecessary work, but their player-visible cost is unmeasured. The UI observations below are design recommendations, not arithmetic bugs or evidence of failed touch input.

## Baseline, provenance and limitations

- Branch: `codex/maintenance-integration-20261114`; HEAD `33f5e62` at audit. The checkout has unrelated policy, font and PlayerSettings edits, deleted art/evidence, recovery assets and untracked work. None was modified. `runInBackground` differs from HEAD; no settings restoration is part of this task.
- Read current AGENTS/CODING_STANDARDS and complete STATUS entries for maintenance waves 2 and 3 through `scripts/read-project-context.ps1`. Applied `jtn-logic-review`, `jtn-ui-debug`, `unity:ui-ugui` and `unity-verify` in review mode. Read the construction vocabulary and Arcade contract sections of GAME_RULES.
- Recomputed hashes for the union of `sourceInputs` in the wave-2 and wave-3 receipts, taking the newer entry for duplicate paths: **93/93 unique inputs match**. A naive comparison against both generations finds two expected old Runtime hashes superseded by wave 3; they are not unexplained drift. This does not hash every asset or prove current live behavior.
- Reused historical evidence: [wave 3 receipt](../evidence/unity/maintenance-wave3-20261114/verification.json) reports compile 0/0, EditMode 16/16 and one focused passing PlayMode case; [wave 2 receipt](../evidence/unity/maintenance-wave2-20261114/verification.json) supplies 49 Core groups and a two-segment journey. Its Editor exit and checkpoint continuation remain explicit limitations. No tests were rerun for this document.
- Actually viewed: wave-3 [selected regular](../evidence/unity/maintenance-wave3-20261114/selected-goal-regular.png), [selected short](../evidence/unity/maintenance-wave3-20261114/selected-goal-short.png), [Skills short](../evidence/unity/maintenance-wave3-20261114/skills-short.png), wave-2 [first House](../evidence/unity/maintenance-wave2-20261114/e2e-first-segment/01-house.png), and [final yard](../evidence/unity/maintenance-wave2-20261114/e2e-continuation/05-final.png). These establish captured layouts, not animation quality, physical target sizes, interaction success or today's live screen.
- Inspected serialized scene references: `Unity/Assets/Scenes/JunkToNeon.unity:20437` has DirectNavigation, `:36365` uses scaled Canvas and `:36385` overlay rendering. House sprite references at `:21053` resolve to the three `Resources/Editable/HouseStage*.asset.meta` GUIDs. Deleted `house-stages.png` alone is therefore not proof of broken art. Runtime also has a placeholder path at `JunkGame.Yard.cs:61`.
- The MAIN chat retains sole ownership of the shared Editor. This audit made no Editor, Play, scene, profile, save, process or service calls. The handed-off PID, coins, sequence and AUTO state were not reasserted as current facts. No APK, device, native durability, FPS or user comprehension claim is made.

## Findings with triggers and expected outcomes

### F1 — P3 confirmed unreachable completion branch

Owner: `Unity/Assets/Scripts/Runtime/JunkGame.Chapter.cs:212-219` (`ShowChapterEra`).

Trigger: invoke the era view with a completed chapter. At line 214 the method opens Orders and returns. Its later completed-chapter block at line 217 cannot display its completion text. For an incomplete chapter, the intervening `BeginChapterPanel` at line 76 stops AUTO and updates UI; it does not complete a construction or mutate the chapter. Thus neither state reaches that inner completed block.

Expected maintenance outcome: one intentional completed route with no misleading dead fallback. Actual: a second completion route exists in source but cannot run. Player impact: no demonstrated malfunction; maintainers could edit text that players never see. Remove only the inner branch if retaining the current Orders route. Do not remove the method, legacy entry routes, serialized fields or compatibility code. A private branch inside a live method is not independently targeted by Unity serialization or reflection; deletion of whole methods would require a separate reference audit.

Smallest check: compare era-open behavior at 11/12, 12/12, and 24/24 using existing fixtures; final route stays Orders and no state is changed by inspection. No new test that merely checks the deleted source text.

### F2 — P2 candidate: repeated snapshot copies during yard binding

Owner: `Unity/Assets/Scripts/Runtime/JunkGame.Yard.cs:112-116`; copy semantics: `Core/GameSession.cs:30`, `Core/Model.cs:84`.

Trigger: render the yard after a new committed sequence, with the same healthy Session. The binding guard reads `Session.State.sequence`, then assignment reads it again, then each of six listeners captures another fresh `Session.State.sequence`. Each property read copies ModelState and retained spin/settlement objects. On the same-session changed-sequence path this is eight State reads before later callback execution. Expected: a single call-local snapshot/sequence for this synchronous binding pass, while callbacks still check current authority when actually invoked.

Consequence: avoidable allocation/work per rebuild is established by source; a visible hitch or meaningful frame-budget violation is **not** established. It is not an every-frame deep clone of every immutable subsystem. Measure at fresh, mid-chapter and completed snapshots before deciding to patch. Do not cache mutable state across frames or remove current callback/session/pointer checks.

Smallest check: byte/time comparison for the same bounded yard rebind cases, plus saved callback refusal after a new spin, session replacement and pause/reopen. Fresh clicks must still inspect the correct family. Canonical snapshot before/after inspection must match.

### R1 — P2 design recommendation: name the construction counter consistently

Owner: `Runtime/JunkGame.Milestones.cs:16-22`; related product wording in `Runtime/JunkGame.ProductUi.cs:97,268` and the Upgrades hub.

Trigger: a player with one paid construction opens District. The recorded/current source string is `11 upgrades until Workshop Yard.` Yet Upgrades is a separate tab whose hub explicitly says District construction is counted separately. Expected comprehension: the remaining count clearly means paid building stages. Actual arithmetic is correct; vocabulary is overloaded. Prefer `11 building stages until Workshop Yard` or equally concise construction wording. Preserve 12 paid stages per era and 24 overall; do not count skills/equipment or change the counter.

Acceptance: 0, 1 and 11 remaining stages, free era entry and 24/24 use unambiguous singular/plural wording and fit both portraits. This is copy work, not a balance fix.

### R2 — P2 design recommendation: explain tier-II prerequisites and retained prices

Owner: `Runtime/JunkGame.Navigation.cs:159-168,193-203`; authoritative details at `Runtime/JunkGame.Chapter.cs:191-210` and `Upgrades/SkillTree.cs:62`.

Observed in Skills short: all three tier-II cards say `I + Yard + building I`, wrapping over multiple lines and not naming the required family. The detail screen correctly names House, Recycler or Pigeon Loft; there is no demonstrated illegal purchase. Use compact family-specific requirements or a clear `Inspect requirements` summary, retaining the full authoritative detail screen and inspection access for locked cards. Avoid shrinking already small text to solve verbosity.

Observed in selected-goal portraits: `Goal 540 · need 5` and `New selection: 480` are both correctly shown with a 535 wallet. Preserve this crucial historical-price distinction. Suggested clarification is a bounded `Saved goal` / `Reselect price` vocabulary, subject to fit; never silently replace the saved quote. Opening and Back must not select or buy anything.

Acceptance: each tier-II branch is distinguishable; locked, affordable, owned and historical selected states fit 1080x2340/1080x2280. The 540 goal remains 540 until explicit reselection; 480 remains the new offer. Keep selected outline and Skills style reset.

### R3 — P2 design recommendation: preserve one obvious next action in the root view

Owners: `Runtime/JunkGame.ProductUi.cs` and `Runtime/JunkGame.InnerUi.cs`.

In the first-House capture, the repair outcome, next Workshop card, ticket countdown, Map/Guide/Event shortcuts, Bonus locked, reels and five footer tabs compete for attention. The next Workshop card and large SPIN control are already useful; retain them. The final-yard capture clearly announces completion and optional continuation, but the long construction benefit sentence spans almost the full width. These are density/hierarchy observations, not confirmed clipping, failed hit targets or user confusion.

Improve only local emphasis and copy presentation: keep repair result readable, make the next explicit inspection visually primary, keep optional ticket/minigame information secondary, and preserve a clear saved/ready-result cue. Do not auto-open a minigame, add another modal, remove navigation, change spin duration or introduce mechanics. Inspect the existing Events screen in the coordinator's private session before changing it; this audit did not view that screen. The source at `ProductUi.cs:290-303` already distinguishes start/resume/claim and exposes Parcels; do not replace that with a generic CTA that hides a pending result.

Acceptance: first repair -> Workshop inspection -> explicit goal selection -> spin -> optional Events/return is legible and uses existing actions. Include saved positive and zero reward states if event emphasis changes. Visual judgment requires actual before/after captures, not only text-fit checks.

### H1 — P2 investigation only: synchronous durable write latency

Route: `Runtime/JunkGame.cs:213-218` calls `ExecuteChapterSpin` before setting Busy/presenting; `Core/ChapterSession.cs:44` writes pending and applied snapshots; `Core/GameSession.cs:32-42,267-273` validates/encodes/decodes and flushes/replaces each snapshot.

This is a credible input-to-feedback latency source on slower storage, but no new timing measurement supports calling it a defect. Two durable boundaries are intentional crash/replay protection. Do not remove flushes, skip validation, combine commits, move IO to a background worker or alter save format as a speculative optimization. A bounded private measurement may determine whether it merits a later scoped task. Editor results must remain distinct from native storage performance. No phone work is authorized here.

## Invariant review and what should remain intact

Inspected GameStore open/write/recovery, Chapter execution, Arcade retained claim replay, UI quote dispatch and existing regression coverage. `ArcadeEconomy.cs:53-63` recognizes exact retained claims before rejecting stale new commands; `ArcadeSession.cs:17-23` persists through the common pending/applied boundary. `GameSession.cs:48-80` blocks uncertain missing-primary cases and resolves stored pending results rather than rerolling. `ChapterSession.cs:29-42` validates goal-clear identity, sequence and saved price. `JunkGame.Chapter.cs:191-210` retains goal cost while explaining optional skill spending.

No supported new violation was found here. The existing ChapterEconomy regressions cover historical prices, conflicting replay, stale wallet/context and recovery; their presence and matching receipt inputs support reuse but are not a fresh execution. Parcels implementation, all overflow paths, every old protocol, native filesystem semantics and every interaction route were not exhaustively re-audited. Do not interpret this limited review as permission to delete old APIs or weaken validation.

## Work packages and exact ownership

All paths below are repository-relative. Four roles are sufficient. MAIN counts as one of the four available concurrent slots, so at most three producers run together in its agent tree. Ten simultaneous agents are not supported by the current tree; there is no need for ten roles. If later evidence justifies more roles, queue them in waves of three producers with MAIN, or use separately supported user-requested chats within actual limits. Do not change model, limits, permissions or settings.

### A — Navigation comprehension (first wave, highest priority)

Own only `Unity/Assets/Scripts/Runtime/JunkGame.Navigation.cs`, `JunkGame.Milestones.cs`, `JunkGame.Chapter.cs` in that same directory. Deliver R1/R2; optionally remove F1 in the same small reviewable patch. No Core/Upgrades catalog/price/scene/font/asset edits. No ProductUi or Yard edits. Read-only review of their callers is allowed.

Dependencies: current baseline only. Freeze wording before integration. Acceptance and minimal checks are those in R1/R2/F1, including historical540/current480 and Skills reset. Reuse existing pure evidence for display-only changes; MAIN performs compile/EditMode and the affected UI case. Do not create a second overlapping test harness.

Delegation prompt: "Implement audit R1/R2 within Navigation, Milestones and Chapter only; optionally remove F1 without changing its final Orders route. You are not alone in the codebase: preserve others' work and do not revert it. Preserve quotes, payments, consent, styles and hit rectangles. Do not operate Unity or normal saves. Return a frozen commit, changed-file list, exact before/after copy and acceptance cases for MAIN. A lack of fit is a reason to shorten copy, not to weaken historical-price clarity."

### B — Root visual hierarchy (first wave)

Own only `Unity/Assets/Scripts/Runtime/JunkGame.ProductUi.cs` and `JunkGame.InnerUi.cs`. Deliver the narrow R3 polish. No navigation-card, Yard, Core, scene, font or art changes. Scene requests belong to MAIN; do not edit YAML or generate replacement graphics.

Dependencies: current baseline; coordinate wording with A through MAIN without editing A's files. Start with a concrete proposed local diff, using the existing first-House/final captures. MAIN supplies any additional private capture required to resolve layout uncertainty. If no meaningful improvement survives review, return no patch instead of cosmetic churn.

Acceptance: regular/short portraits at first repair, selected goal and completed chapter; clear next action and optionality, no hidden ticket/saved-result cue, no overlap or tiny autosized text. If touching event labels, include active round, Ready0, Ready-positive and no-ticket cases plus Back. MAIN runs affected pointer/lifetime checks. No broad replay campaign for styling alone.

Delegation prompt: "Implement only the smallest R3 hierarchy improvement in ProductUi/InnerUi. You are not alone; preserve others' edits. Keep existing routes and explicit actions, repair/receipt visibility and optional continuation. No new mechanics, assets, scene changes or Editor use. Return a frozen commit and specific before/after capture requests; do not claim screenshots prove touch or comprehension."

### C — Measured yard render work (first wave)

Own only `Unity/Assets/Scripts/Runtime/JunkGame.Yard.cs` and bounded task diagnostics under `.work/audit-polish-perf/`. Production Core, GameSession, Navigation, ProductUi and all serialization are read-only. Diagnostic artifacts to retain go to a uniquely owned `docs/evidence/unity/audit-polish-perf-20261114/` directory, only if measurements justify them.

Address F2. First measure call-local snapshot work; then, only if supported, reuse a local state/sequence within a render/bind pass. No cross-frame cache, stale callback bypass or callback authority snapshot reuse. H1 is an optional read-only timing investigation supplied by MAIN; it does not authorize a storage rewrite. Do not spawn an Editor or run tests against the shared one.

Acceptance: lower allocations for the same binding work, same family action and exact refusal for stale callback/session/lifecycle, unchanged canonical model. Report bytes per rebind and measured context, never infer FPS. MAIN owns live measurement and final tests; pure benchmark results may be prepared independently with X: process-local temp, bounded output and no background service.

Delegation prompt: "Measure and, if meaningful, address F2 in Yard.cs only. You are not alone; preserve others' edits. State copies remain defensive at API boundaries; callbacks must read fresh authority. No Core/storage rewrite. Return a frozen commit or a measured no-change result, benchmark inputs/limits and exact callback cases for MAIN."

### D — MAIN integration and acceptance (serial owner)

Own shared Editor, `Unity/Assets/Tests/` changes if actually needed, existing shared Checks entrypoints, scene changes if justified, `docs/STATUS.md` and final scoped receipts. No producer edits these shared files. Review A/B/C independently before cherry-pick; MAIN is the only writer in the primary checkout during integration. F1 can be omitted as low value; it does not justify a standalone agent.

Acceptance: requested visible changes exist, required focused checks pass, no quote/replay/consent regressions, normal saves protected, normal preview restored, unrelated dirty files untouched. Report limitations honestly; no APK or device acceptance.

Delegation prompt: "Integrate A/B/C into codex/maintenance-integration-20261114 in order A, C, B. Keep sole Editor ownership and current user progress. Review each frozen diff and test need; preserve the first failure and retry only after a concrete correction. Verify the combined final source, capture both portraits, update existing STATUS and commit only scoped source/evidence. Do not launch additional roles merely to fill capacity."

## Branching, scheduling and serial acceptance

1. On later implementation authorization, recheck HEAD, status and policy revisions. Use existing producer chats/worktrees only after assigning the new non-overlapping ownership; preserve their branches. Otherwise create producer worktrees/`codex/audit-nav-20261114`, `codex/audit-root-ui-20261114`, `codex/audit-yard-perf-20261114` from the verified integration commit. Do not switch/reset the shared checkout or copy its unrelated dirty files into worktrees. No extra Unity Editors/Libraries are needed for source work.
2. Wave 1: A/B/C prepare independent patches while MAIN remains the fourth role. Only MAIN can perform live observations. Queue capture requests; agent concurrency never implies concurrent Editor commands. No tests/imports while another patch is being applied.
3. Freeze each producer revision. Integrate A, then C, then B into the existing branch. Dependencies are ownership/capture dependencies rather than a mandate to refactor shared APIs. If a patch needs another owner's file, stop that overlap, transfer ownership through MAIN and serialize it. Shared tests/Checks/STATUS are merged only by MAIN.
4. Before heavy work, inspect free space and diagnostic growth; use owned X: temp and bounded `.work` output. Suggested disposable retention is seven days with a 128 MiB inspection threshold, not a claim of automatic rotation. Preserve verification receipts; skip locked/reparse files and never clear system Temp.
5. Before private Play: coordinate with the current MAIN owner, verify exact Editor/project and effective X: temp, stop Play coherently, then hash/back up the latest normal save. Use a unique private override and confirm normal files remain unchanged throughout the private interval. Never restore an older normal save over newer progress.
6. At the frozen integrated revision, apply unity-verify: relevant Core cases or documented source-matching reuse, zero Unity compile errors, explicit passing JunkToNeon EditMode summary. Run the narrow existing UI/lifetime regressions and observe frames plus relevant pointer actions. If adding an independent regression is necessary, MAIN owns it; avoid tests that mirror copy strings or removed branches only.
7. Inspect before/after regular and short captures. The connected acceptance route is repair -> next construction inspection/selection -> spin/reveal/AUTO stop -> optional minigame navigation and return. Use private fixtures for historical-price and saved-result states; distinguish fixture checks from fresh progression. No need to rerun all 654 historical spins unless a changed invariant warrants it. The old two-segment journey remains two-segment evidence.
8. Clear task save/clock overrides, restore normal preview without manufacturing progress, preserve Scene/Hierarchy, separate Canvas Inspector and portrait Simulator. Record final source identity, failures/corrections, scoped results and remaining limits in STATUS. Commit scoped source and receipts; do not publish or include unrelated font/settings/deletions.

## Audit delivery check

This task creates only this document. No source fixes, agent launches, test/build runs, cleanup, publication or Editor mutations occurred. Source/receipt hash comparison and actual image inspection support the findings. First next action, if implementation is requested: start A/B/C with MAIN as the sole integration owner; do not expand scope to ten agents.

## Authorized implementation outcome (2026-11-14)

The user subsequently authorized three agents and MAIN integration. Packages A/B/C were completed in isolated X: worktrees and integrated in the planned order; MAIN performed private-save acceptance and returned the normal preview. Final price-card text is `Saved 540` / `Reselect: 480` after actual screenshot review rejected an awkward line wrap. The scoped results, preserved initial failures and limits are recorded in [STATUS](../STATUS.md) and the [verification receipt](../evidence/unity/audit-polish-20261114/verification.json). No remaining agent patch, APK or publication is part of this delivery.
