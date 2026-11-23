# Junk to Neon

Unity is the active implementation by explicit user decision on 2026-10-29. Work in `Unity/` with installed Unity 6000.3.21f1. This supersedes the ancestor Phaser/Capacitor stack and npm acceptance commands for active development. Root TypeScript/Android files remain the preserved Phaser baseline at commit `bfd5544`; preserve them and the historical HTML.

Before gameplay work, read the latest relevant complete entry in `docs/STATUS.md` and the task-relevant sections of `docs/GAME_RULES.md`. Use `rg -n '^## ' <file>` to locate sections; expand into older entries only for an affected contract, failure or evidence link. These files contain substantial history, not one current specification. Keep this prototype small; all graphics are temporary. Runtime code lives in `Unity/Assets/Scripts/Runtime`; pure C# economy and persistence in `Unity/Assets/Scripts/Core`. `EconomyRules` is partial across Core files; current construction quotes live in `Unity/Assets/Scripts/Content/ConstructionPricing.cs`, separately from historical catalog prices.

For implementation/review, read `CODING_STANDARDS.md`. Automatically apply the skill routing below to each project request, including ordinary Russian requests without skill names. Read each selected `.agents/skills/<name>/SKILL.md` before the relevant part of the work; if the picker omits a project skill, use that existing file directly. Reuse instructions already read in this task. These are workflows for the current Codex agent, not permission to start subagents or external AI CLIs. Tool research is in `docs/development/codex-tooling-20261101.md`; read it only when changing the tool setup.

## Automatic skill routing

Classify the requested outcome and intent (explain, propose, implement, review or verify). Start with the matching primary skill; add every supporting skill whose condition actually applies, at the relevant stage. Re-evaluate routing if the cause or scope changes. Skill selection needs no separate confirmation and does not turn a proposal/review into permission to implement.

| Requested work | Primary route | Conditional additions |
|---|---|---|
| Narrative, neighbor lines, chapter meaning or guidance copy | `jtn-story` | `jtn-economy` if actual benefits/costs change; Unity UI skill if layout changes |
| Prices, rewards, resource flows or upgrade value | `jtn-economy` | `jtn-first-session` if opening pacing is in scope; `jtn-story` if narrative is part of the request |
| Onboarding, early wins, first-session engagement | `jtn-first-session` | `jtn-economy` for numerical pacing/tradeoffs; `jtn-story` for narrative beats; Unity UI skill for implemented presentation |
| Flicker, displaced controls, broken navigation or a recorded UI defect | `jtn-ui-debug` | `jtn-logic-review` if investigation reaches state/consent bugs; installed `unity:ui-ugui` for Canvas edits, TMP skill only for a text-system issue |
| Gameplay/save bug or requested code review | `jtn-logic-review` | `jtn-economy` if arithmetic/balance itself changes; `jtn-ui-debug` if the defect also requires interaction evidence |
| New scene/HUD/layout or visual polish without a reported bug | Installed `unity:ui` -> `unity:ui-ugui` for this Canvas | Story/economy skills only when their behavior also changes; do not invent a UI defect to activate debugging |
| Game integration, regression verification or APK packaging | `unity-verify` | Installed `unity:unity-cli` for Editor command discovery; APK only when packaging is in scope |
| Documentation, tooling, status questions or unrelated work | Matching authoring/tool skill or ordinary reasoning | Gameplay skills only for a genuine gameplay dependency; no automatic Editor/test/build loop |

Before finalizing a change to payouts, quotes, persistence, replay or action consent, use `jtn-logic-review` for a focused check of the changed invariants. An already completed matching review satisfies this step. For implemented game C#/scene/UI changes, route verification through `unity-verify` and the mandatory checks below; read-only design/explanation does not start those checks. A larger displayed reward number is a UI change unless the actual credit changes. This routing coordinates needed skills, not a fixed sequence that runs all of them.

## Unity operation and acceptance

Human workflow decision (2026-11-01): Unity CLI is the primary interface for Unity development and automation. Use the project-pinned `X:/PROJECTS/FINAL-SVALKA/.tooling/unity-cli/bin/unity.exe` (beta.14), always targeting `X:/PROJECTS/FINAL-SVALKA/Unity`. Discover commands with `commands --grep <term>` or `command --query <term>` and prefer JSON output. Drive the existing Editor through CLI/Pipeline for scene/asset authoring, Play, captures, compilation and scoped tests. Pipeline connects the Editor; it does not require MCP. Do not add MCP discovery to routine CLI work. Use MCP or GUI automation only for a concrete CLI capability gap or blocking dialog, and record the reason. Keep the warm-Editor/private-save loop; do not start a second batch Editor against an already open project. A pasted CLI help/version transcript does not authorize a downgrade, self-update, analytics change or account/configuration change.

Use the Unity CLI/Pipeline against this project's exact absolute `Unity/` path, preserving other open projects. Create/edit scenes and settings through the live Editor. Keep the official Device Simulator visible in the right dock with a regular non-foldable Android portrait definition; preserve Scene/Hierarchy and a separate Inspector for editing the serialized Canvas. Use existing Unity tools; an Android OS emulator or custom preview tool is not required. Editor preview is separate from Android/device acceptance. Preserve existing progress when verifying; tests use private folders. A frame counter and observed UI action establish live behavior; screenshots alone do not.

Verify relevant C# regressions with `Checks/CoreChecks.csproj`, Unity compilation and the `JunkToNeon` EditMode test. Rebuild the Android development APK when validating native packaging, with Unity's embedded tools. Changes to archived Phaser do not belong to routine Unity work. Record actual failures and one next action in STATUS; commit scoped source/evidence together. Never claim physical-device acceptance from editor or automated evidence.

For Unity integration checks and APK checkpoints, follow `.agents/skills/unity-verify/SKILL.md` for the established warm-Editor/private-save verification loop.

Keep logs/caches under project-local `.work`, `Unity/Library` and `Unity/Logs` where supported; inspect disk before/after imports and builds. Preserve user data, other projects and signing credentials. No publication, ads, purchases or extra mechanics in this milestone.

Install or test on a physical phone only when the human directly requests it. Continue authorized Editor checks/APK builds locally; do not repeatedly suggest phone installation or install an Android OS emulator.
