# Codex tooling and gameplay skills

## CLI-first workflow decision

The human requested Unity CLI as the primary development interface on 2026-11-01. The authoritative execution rules are in [AGENTS.md](../../AGENTS.md#unity-operation-and-acceptance) and the existing [verification skill](../../.agents/skills/unity-verify/SKILL.md). Use the pinned beta.14 against the exact project, preserving the current Editor, private verification saves and source-bound receipts. The supplied beta.12 help transcript is reference material, not authorization to replace the installed CLI or change analytics/account settings. Routine CLI work no longer needs an MCP discovery preflight; a fallback requires an actual capability gap or blocking dialog. This instruction change does not establish a fresh game/test/build result.

Research and local adaptation, 2026-11-01 (Asia/Baku). Scope: Codex workflows for this Unity project. These are authored project skills, not an installation of the external frameworks, a balance change or a measured speedup.

## Automatic orchestration

The user's follow-up explicitly requests automatic use of all applicable refined skills. [AGENTS.md](../../AGENTS.md) now owns one routing table, applied to ordinary project requests without requiring skill names. It selects by intended result and task mode, then adds supporting skills at the stage where their condition holds. If the initial picker omits a local skill, the agent reads its existing `.agents/skills/<name>/SKILL.md` directly. CODING_STANDARDS points to the same table rather than maintaining a competing router. No additional catch-all skill, background service or global hook was introduced.

All six project skills now have `agents/openai.yaml` with explicit `policy.allow_implicit_invocation: true`. This makes the intended policy inspectable; implicit use was already the Codex default. The substantive change is the routing/composition instruction, not a claim that these YAML files execute anything themselves. [Official Codex documentation](https://learn.chatgpt.com/docs/build-skills) documents implicit matching, repo discovery and the policy field. The local fallback also covers a catalogue that has not refreshed. Global configuration and other projects' skills remain untouched.

For mixed implementation work, the same facts pass through the workflow: story trigger and intended meaning, source-bound economic values, UI presentation, focused invariant review where state/consent changes, then relevant Unity verification. Supporting skills do not restart discovery, broaden the scope, rerun matching checks or add an approval step. A proposal remains a proposal; review remains read-only unless fixes are requested. Skill orchestration is not authorization for subagents, external AI CLIs or publication.

Author-reviewed representative routing cases (design validation, not independent model executions):

| Ordinary request | Expected route / scope |
|---|---|
| Explain why an existing goal still costs 600 | Economy; inspect saved/current quote distinction, no edit or Editor run |
| Propose a more engaging opening without changing code | First-session; economy for numerical pacing, story for proposed beats; no implementation gates |
| Implement an opening revision with new prices and neighbor lines | First-session + economy + story; Unity UI where presentation changes; focused logic review + Unity verification |
| Rewrite a neighbor's line without changing its reward | Story; relevant integration verification only if source is edited, no balance campaign |
| Make the displayed win number larger | Unity UI/uGUI + relevant verification; no economy retuning |
| Increase the actual win credit | Economy + changed-invariant review + Unity verification |
| Fix a modal flicker shown in a recording | UI debug; Canvas skill if editing layout, relevant verification |
| Investigate a reward animation that appears twice | Start UI debug; add logic review only if actual credit/state is duplicated |
| Fix an actual duplicate claim after reload | Logic review/fix + relevant Unity verification; economy only if arithmetic changes |
| Review a diff and do not change it | Logic review; evidence gathering as needed, no automatic edits or build |
| Build an updated development APK | Unity verification + installed Unity CLI, including packaging gates |
| Update a README or these skill instructions | Relevant authoring skill; document/metadata checks, no gameplay campaign |

Validation: all six SKILL.md files pass the installed validator; all six invocation-policy YAML files parse as the expected boolean policy and match the skill identities. All six project skill paths and seven local workflow/skill Markdown links resolve. The scoped diff check passes. This establishes configuration and internally consistent routing instructions, not guaranteed future selection, measured speed gains or fresh game acceptance. No subagent/AI CLI, Editor, game test or build was launched for this configuration-only task.

## Recheck: project evidence and community advice

The follow-up review covers all five new skills, the existing `unity-verify`, AGENTS/CODING_STANDARDS, the latest applicable gameplay handoff, the existing C# pacing/check entrypoints and six opened Reddit discussions. It is not a whole-game correctness audit, a global plugin uninstall review or new Unity acceptance. The checkout has concurrent Runtime/scene/font/test edits; none belong to this tooling revision.

### Corrections made after review

1. **Existing C# utilities were under-specified.** The initial economy skill said to reuse labs without identifying `Checks/ProgressionTiming`, `Checks/ExpansionPacing` and `Checks/ArcadePacing`. These compile production C# but have historical modes and different contracts. ArcadePacing requires `FrozenRoot` and explicitly pins `fa9f3f20df6fe71de64e6045ad7e09cb97d0b3cc`; a new run against arbitrary current files would not be valid. The skill now routes to their READMEs, preserves historical receipts and requires bounded current-source work when a new analysis is authorized. Dedicated ExpansionCore/ArcadeCore checks are also named; the baseline CoreChecks project is not every subsystem's suite.
2. **Optional analysis could expand a small task.** The opening/UI skills previously enumerated several routes and policies too broadly. They now distinguish a local label/beat/bug from a whole-opening comparison. One primary skill is loaded, supporting skills only for concrete dependencies. A complete numerical simulation gets explicit state/spin/time/output bounds; a local arithmetic question can be answered directly.
3. **An older verification phrase was ambiguous.** `unity-verify` ended with "normal save restored" despite the newer verified STATUS explicitly preserving newer human progress without raw rollback. The revised wording resumes the preview against the current normal save, clears task-owned save/clock overrides, and investigates normal-file drift instead of overwriting it with a backup. Integration ownership is coordinated before mutating a shared Editor. No save or Editor operation was performed in this review.

### What Reddit contributes, and what it cannot prove

| Discussion | Useful signal | Application / limit |
|---|---|---|
| [Codex + Superpowers overhead](https://www.reddit.com/r/codex/comments/1utg8yn/has_superpowers_made_codex_gpt56_slower_for_you/) | Reports of simple changes expanding into planning/review loops; one reply favors a simplified custom workflow | Check task expansion and repeated work. These anecdotes do not establish a general speed ratio or a failure of all skills. |
| [Superpowers versus ECC](https://www.reddit.com/r/ClaudeAI/comments/1un4puf/superpowers_vs_everything_claude_code_ecc/) | Positive feedback for guided design when starting out; selective adoption favored for an established workflow | Keep useful procedures. The comment's Claude-specific metadata/token calculation is not a measurement of this Codex app. |
| [Are skills useful?](https://www.reddit.com/r/ClaudeCode/comments/1x53emo/are_skills_really_that_useful_if_you_know_what/) | Both complaints about overly broad triggers and examples of useful repeatable environment/verification skills | Tighten selection boundaries; retain our project-specific `unity-verify`. No automatic deletion based on missing usage data. |
| [Experienced developer using Unity CLI](https://www.reddit.com/r/gamedev/comments/1wo5asm/ai_models_have_caught_up_with_unity_dev_my/) | An author reports faster work with an Editor-connected workflow while retaining architectural oversight | Supports investigating the feedback loop, not adopting the claimed 5x as our expected improvement. No controlled benchmark is supplied. |
| [Unity AI or Codex + MCP](https://www.reddit.com/r/aigamedev/comments/1wdojlt/unity_ai_or_codex_with_mcp/) | Conflicting tool preferences, including CLI and MCP; some replies promote their own guides | Keep our working Unity integration; compare actual repair effort before adding another layer. CLI/MCP reliability rankings are unverified opinions. |
| [Incremental games requiring one exact build order](https://www.reddit.com/r/incremental_games/comments/1w8la69/growing_tired_of_guideslop_incremental_games_what/) | Players describe abandoning progressions that punish experimentation or require external guides | Include a plausible non-optimal purchase policy and show tradeoffs in-game. This motivates a local hypothesis, not a retention statistic. |

The current [official Codex skill documentation](https://learn.chatgpt.com/docs/build-skills) says the initial list contains names, descriptions and paths; full bodies load when selected. It documents a default catalogue budget of at most 2% of the model context, or 8,000 characters if context size is unknown, with shortening/omission for larger collections. This is documented behavior, not measured usage of this desktop session. Accordingly, Reddit claims that every installed skill's full text necessarily loads on every turn, or that a Claude token estimate transfers directly to Codex, are not a sound basis for removing unrelated tools. No global catalogue budget, invocation policy or model setting was changed.

### Highest-value next optimizations

| Priority | Concrete project action | Observable result |
|---|---|---|
| 1: Reduce repeated investigation/integration | Use the targeted source/check routes and one shared-Editor integration owner; finish one connected action, then run its applicable checks | Fewer repeated searches, truncated reads, duplicate imports and repeated unchanged tests per completed task |
| 2: Make balance comparisons cheap and trustworthy | Reuse a matching source-bound C# receipt, or adapt the narrowest existing pacing mode to an explicitly frozen current input set; compare baseline and one candidate on the same policies/seeds | Purchase/unlock times, no-decision gaps, completed/censored runs and opportunity costs, with no second hand-maintained economy |
| 3: Optimize the opening that players actually experience | Observe one current private first-session route; prioritize goal comprehension, visible payoff and a viable ordinary purchase choice before changing reward magnitude | First action/repair, recognition of the upgrade effect, next-goal clarity, interruption count and the penalty for a reasonable suboptimal choice |

These are priorities for the next relevant task, not newly started simulations or playtests. The source recheck found 17 `.asmdef` files and Core's `noEngineReferences=true`: the project already has a useful separation for C# checks. Adding assemblies blindly is not a demonstrated improvement. Runtime remains in the predefined assembly; splitting it is a candidate only if a compile profile identifies it as a bottleneck. [Unity's assembly guidance](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/script-compilation/assembly-definition-files/assembly-definitions-intro) explains the dependency/recompile tradeoff.

`EditorSettings.asset` currently records Enter Play Mode options enabled with value 0 (reloads retained). Disabling domain reload is a possible experiment, not an adopted optimization: [Unity 6.3 documentation](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/code-reload-serialization/code-reloading-editor/domain-reloading) notes persistent static state/subscribers and that script refresh still triggers reload. Given this project's lifecycle/private-save checks and no measured Play-entry bottleneck, changing that setting now would add qualification work without a demonstrated return. Hindsight, another Unity bridge, a dialogue engine and paid review CI likewise remain deferred until they resolve a measured problem.

### Recheck verification

All six project skills pass the installed skill-creator validator. Six local links in the workflow/skills and seven added runner/suite paths resolve; the scoped whitespace check passes. Manual contract review additionally checked: a label-only change avoids a whole-opening campaign; a current Arcade pacing request respects the frozen source pin; a newer normal save is preserved; and existing price/replay boundaries remain. This is author review, not independent skill execution or proof of implicit invocation. Initial broad tool output was truncated and narrowed; two guessed source paths were absent and replaced by symbol/directory searches. No game/check suite was executed, no files were deleted, and no global setting or external tool was installed. Actual elapsed-time/token gains remain unmeasured.

## Implemented setup

Five discoverable project skills live in `.agents/skills`: `jtn-story`, `jtn-economy`, `jtn-first-session`, `jtn-ui-debug` and `jtn-logic-review`. The existing `unity-verify` remains the integration procedure. Each skill serves the current agent; delegation remains subject to the user's instruction. [CODING_STANDARDS](../../CODING_STANDARDS.md) provides source routes and the small-change workflow. AGENTS points to these only when relevant.

Ordinary requests now use the automatic routing above. Explicit `$jtn-first-session`, `$jtn-economy`, `$jtn-story`, `$jtn-ui-debug`, `$jtn-logic-review` or `$unity-verify` mentions remain optional ways to select a workflow; they are not shell commands. A stale picker does not require the user to name or reload the skill before work: AGENTS directs the agent to its existing local file. Native catalogue refresh and future implicit selections have not been independently observed in this session.

## What the research changes

| Source | Useful contribution | Decision for this project |
|---|---|---|
| [Superpowers](https://github.com/obra/superpowers/blob/main/README.md) | Codex App/CLI support; [root-cause debugging](https://github.com/obra/superpowers/blob/main/skills/systematic-debugging/SKILL.md) and evidence-based completion | Adapt the useful principles locally. The full method introduces design sign-off and agent orchestration; its [verification skill](https://github.com/obra/superpowers/blob/main/skills/verification-before-completion/SKILL.md) demands fresh full runs and illustrates reverting a fix. Those defaults conflict with this project's scoped evidence reuse and no mutation-campaign rule. |
| [ECC](https://github.com/affaan-m/ECC/blob/main/README.md) | Native Codex plugin path; [iterative retrieval](https://github.com/affaan-m/ECC/blob/main/skills/iterative-retrieval/SKILL.md), [evaluation criteria](https://github.com/affaan-m/ECC/blob/main/skills/eval-harness/SKILL.md), [skill inventory review](https://github.com/affaan-m/ECC/blob/main/skills/skill-stocktake/SKILL.md) | Use selective context and observable outcomes. Do not copy `.claude` paths, Bash/jq inventory scripts, automatic subagent batches or stock harness configuration into Windows Codex. The old repository URL did not resolve through the connector; the current `affaan-m/ECC` source did. |
| [Matt Pocock skills](https://github.com/mattpocock/skills/blob/main/README.md) | Small composable skills; writing guidance is already installed locally | Apply the existing `writing-for-agents`; avoid a duplicate installation and routine interviews for already clear requests. |
| [Scrawl](https://github.com/kitlangton/scrawl/blob/main/README.md) and [agent skill](https://github.com/kitlangton/scrawl/blob/main/skills/scrawl/SKILL.md) | Timestamped contact sheet, transcript, keyframes and original media; Codex can inspect a supplied bundle | Support the bundle in `jtn-ui-debug`. Windows is an unsigned preview without auto-update; the skill still describes a Mac-only CLI path. Reading bundles is portable; recording/zoom on this Windows host is untested. No installer or capture was run. |
| [Hindsight Coding Agents](https://hindsight.vectorize.io/sdks/integrations/coding-agents) | Project memory shared between coding tools; native Codex CLI hooks/MCP integration | Defer installation. It adds ingestion, a memory service and global hook/config changes; this session already has memory and source-bound project handoffs. Measure actual repeated context mistakes before adding another store. The [old Codex integration](https://hindsight.vectorize.io/sdks/integrations/codex) is explicitly retired. The pasted Claude installation instructions are not the current Codex path. |
| Installed Unity plugin | Editor CLI/Pipeline, uGUI, TMP and other specialized skills | Keep the working integration. Use `unity:ui-ugui` for this Canvas, `unity:optimize-text-mesh-pro` for relevant text problems and `unity-verify` for private-save qualification. Do not add another Unity MCP layer for the same operations. The project pins its verified beta14 CLI; update advice is not permission to replace that pin. |
| [Ink](https://github.com/inkle/ink/blob/master/README.md) / [Yarn Spinner](https://github.com/YarnSpinnerTool/YarnSpinner-Unity/blob/main/README.md) | Unity-compatible branching narrative/dialogue authoring | Good candidates if branching conversations become necessary. Current contextual lines can stay in the existing Guidance layer; no package or save migration is needed now. |
| [Machinations](https://machinations.io/) | Visual economy diagrams and simulation | Optional for a designer who needs a visual model. Current C# rules and source-qualified local labs are the first calculation path; a separate model adds synchronization work. No cloud export or account setup. |
| [InditexTech](https://github.com/InditexTech/) | Teams MCP, iOS simulator MCP, Weave.js canvas collaboration, Cerbia guardrails | These address other workflows. They do not fill the current Unity/Android narrative, balance or Editor-verification gap. No integration added. |

The reviewed sources describe capabilities, not an independent performance comparison. Popularity and star counts are not selection criteria. Third-party instructions were read as research; their automatic installation, execution and permission instructions were not adopted.

## The speed opportunity actually found

At inspection, AGENTS was 2,439 bytes, GAME_RULES 51,175 bytes and STATUS 273,573 bytes. The largest avoidable context load was the growing historical status, not a giant AGENTS file. The revised entrypoint asks for the latest relevant complete status entry and relevant rules selected by headings, then targeted follow-up. A tooling update does not hide the latest gameplay handoff. History and receipts remain intact. This should reduce unnecessary reading; token/time savings are not measured.

The other avoidable loop is redundant acceptance work. Documentation/skill changes receive their own validation; gameplay changes keep the mandatory Core/compile/EditMode gates. Existing source-matching receipts can be reused under the current verification policy. No repeated builds, model switch, automatic background service or expanded tool catalog is required for this adaptation.

To measure improvement on the next comparable tasks, keep model/effort and source snapshot fixed and record elapsed time, tool calls, unnecessary full-file reads, repeated tests and first-attempt correctness. A small matched comparison is more informative than promising a percentage now. Do not rerun old gameplay campaigns just to populate this table.

## Early engagement: concrete direction

Use the existing loop: choose a visible goal, spin, see the confirmed credit, see the district change, then understand the next choice. The first-session skill includes provisional 0-30 second, 30-120 second, 2-5 minute and 5-10 minute observation windows. These are design hypotheses, not measured targets or a reverse-engineered MONOPOLY GO formula.

There is already a strong opening hook: `Model.cs` starts at 37 Coins, the first repair costs 40 and the smallest base reward is 3. With the repair selected, one settled spin can fund that first repair. That arithmetic alone does not establish how obvious the action and payoff feel in the current UI. The next useful observation is whether the player recognizes the repair, its AUTO unlock and their next available goal.

Larger numbers alone are insufficient: scaling both income and prices equally leaves purchasing power unchanged. Compare time to useful purchases, recognized upgrade effects, no-decision stretches and clear world changes. Preserve actual reward receipts; a pending mini-game result is not owned Diamonds. The current project has two eras, 24 construction nodes, optional resources and Arcade, so the old two-building `research/first-ten-minutes/2026-10-29-claude-opus` results cannot qualify its present opening.

[Unity's economy guide](https://activation.unity3d.com/how-to/design-balanced-in-game-economy-guide-part-3) supports planning sources/sinks, player differences and generous early progression followed by gradual difficulty. [Scopely's MONOPOLY GO retrospective](https://www.scopely.com/en/news/reflecting-on-the-journey-of-monopoly-go) emphasizes extensive iteration on the core loop and its wins/losses. Neither supplies a transferable payout schedule or proves our retention. The local skills focus on observable enjoyment and clarity; simulations cannot measure dopamine or establish D1/D7 retention.

## Review and cleanup claims in the pasted posts

The proposed toxic reviewer prompt supplies tone rather than correctness criteria. `jtn-logic-review` replaces it with reproducible, severity-ranked findings about the actual game's failure boundaries. The action's [README](https://github.com/anc95/ChatGPT-CodeReview/blob/main/README.md) documents `PROMPT`, whereas the pasted snippet uses `CUSTOM_PROMPT`. YAML parsing alone would not establish working configuration or useful review. A future CI adoption should pin a reviewed action commit, scope permissions and separate untrusted PR text from instructions. No workflow, token, paid API usage or public comment was added.

The [OpenAI maintenance article](https://developers.openai.com/blog/maintaining-software-with-the-gpt-6-family) does describe over 1,400 maintenance PRs, over 800,000 removed lines and measured container/CI improvements in its own repositories. The transferable lesson is source/runtime evidence and small reviewed changes. It does not establish that deleting code here would help. Unity serialized references, reflection and saved-operation history make text-search-only deletion unsafe; the preserved Phaser baseline remains intentional.

The pasted `/claude-api-prompt-audit` reference gives neither an inspectable skill source nor a Codex-specific contract. It was not installed or treated as an official Codex command. This local adaptation uses the installed authoring skills and [official Codex customization guidance](https://learn.chatgpt.com/docs/customization/overview), which documents repo skills and progressive disclosure.

## Initial validation and limits (76cf059)

The installed skill-creator validator passes for all five new SKILL.md files. Seven local Markdown links and twelve source/procedure routes resolve. The skills are 339-484 words each; their bodies and the research report are loaded only when relevant. A manual routing/contract review covered the following representative requests; this is an author review, not an independent agent execution benchmark:

| Request | Route and required distinction |
|---|---|
| Make the opening feel more rewarding | `jtn-first-session`: actual rewards/world changes, existing mechanics, measurable hypotheses |
| Compare a new construction price with an old selected 600-Coin goal | `jtn-economy`: current offers versus preserved selected quote, no silent repricing |
| Rewrite a neighbor's completion line | `jtn-story`: completed versus claimed state; no invented reward or next era |
| A modal flickers around 0:12 in a recording | `jtn-ui-debug`: temporal evidence and property ownership; one still is insufficient |
| Review a duplicate-claim fix | `jtn-logic-review`: reachable reproduction, historical replay and newer-run preservation |
| Change only these skill descriptions | Documentation validation; no Core run, Editor import, APK or external agent |

The initial lookup for a same-named `EconomyRules.cs` found no file; symbol search located the partial class in `Model.cs` and other Core files. The resulting route explicitly prevents that assumption. A short-context wording patch matched no complete line and changed nothing; the exact paragraph patch then applied. The old ECC repository URL returned a connector metadata error; the canonical ECC URL provided the inspected source. No game/test failure occurred in this documentation task.

No game code, scene, save, global configuration, third-party service or installed skill package is changed by this setup. Preexisting dirty Runtime, project settings and product-UI handoff files retained their recorded SHA-256 values at the preservation check; additional concurrent gameplay edits are outside this commit. Runtime performance, real-player engagement, external-tool operation on this host and automatic skill selection remain separate evidence boundaries. The next useful application is a source-current first-session observation, when gameplay work is requested.
