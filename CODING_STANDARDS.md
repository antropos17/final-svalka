# Junk to Neon implementation workflow

## Start with the affected behavior

State the observable result and identify its owning code before editing. Inspect the working diff and preserve unrelated work. A routine, authorized fix proceeds directly; a design request produces a proposal unless implementation is also requested. Keep the user's selected model, access settings and delegation boundaries.

Read the latest relevant complete STATUS entry, then use GAME_RULES headings to select the applicable contracts. A tooling-only status entry does not replace the latest gameplay handoff. A date, an old passing count or a research recommendation is not proof of current behavior. Resolve disagreements against the owning source and source-bound evidence; retain the discrepancy in the existing task handoff when unresolved.

## Source routes

| Work | Start here; follow actual calls when needed |
|---|---|
| Base spin arithmetic | `Unity/Assets/Scripts/Core/Model.cs` (`EconomyRules`) and its other partial declarations |
| Construction price / chapter effect | `Unity/Assets/Scripts/Content/ConstructionPricing.cs`, `ChapterData.cs`, `ChapterRules.cs`; selected quotes in Core |
| Optional resources / skills / orders | Relevant `Wave*`, `Chapter*`, `Journey*`, `Expansion*` files in Core |
| Tickets / Diamonds / mini-games | `ArcadeRules.cs`, `ArcadeEconomy.cs`, `ArcadeSession.cs` in Core |
| Narrative and guidance | `Unity/Assets/Scripts/Guidance/ChapterNarrative.cs`, `GuideFlow.cs`, affected Runtime callsite |
| HUD / navigation / reward effects | Relevant `Unity/Assets/Scripts/Runtime/JunkGame.*.cs` partial and serialized Canvas in the live Editor |
| Existing checks | `Checks/Program.cs`, `Unity/Assets/Tests/`; search for the affected behavior before adding a case |
| Progression / resource / Arcade pacing | Start with the README in `Checks/ProgressionTiming`, `Checks/ExpansionPacing` or `Checks/ArcadePacing`; these have different historical pins, policies and run arguments |
| Expansion / Arcade Core regressions | `Checks/ExpansionCore/ExpansionCoreChecks.csproj`, `Checks/ArcadeCore/ArcadeCoreChecks.csproj`; read their entrypoint/README before running |

Search by symbol: a class need not have a same-named file. Distinguish current offers from historical prices, immutable paid receipts and already selected goals. A new price must not silently rewrite an existing quote. Preserve legacy protocol/replay behavior and explicit purchase/claim boundaries. Read-only inspection must remain free of economic side effects.

## Keep the feedback loop short

Reproduce a reported defect with its actual trigger, form one testable cause, patch the owning layer, then verify the same trigger. For a new feature, complete one connected player action through state, persistence and presentation before expanding it. Add behavioral regression coverage where state, arithmetic or lifecycle correctness needs it; use an independently expected result. Do not create wording-matching tests for documentation.

Follow the automatic skill routing in [AGENTS.md](AGENTS.md); it is the single routing table. Start with the primary skill and add supporting skills only when their conditions apply. For a mixed task, keep findings together: narrative names the beat/trigger, economy supplies source-bound values, UI binds that same result, and verification checks the connected behavior. Reuse these facts and matching evidence instead of restarting discovery for each skill. If the cause changes from a display issue to duplicate state mutation, activate the state-review route at that point. No extra report or planning gate is required.

Scale optional analysis to the request: one label needs its caller and display check; a price needs its affected policy/compatibility cases; a whole opening needs a journey comparison. Search filenames/headings before bounded reads; if output truncates, narrow the query instead of requesting the entire archive again. Stop expanding once the requested result and applicable checks are established.

Before a simulation, inspect the existing runner's source pin, budgets and output contract. In particular, ArcadePacing requires an immutable `FrozenRoot` and its hardcoded Core identity; pointing it at today's checkout is not a valid current-source measurement. Reuse matching receipts or adapt a bounded existing mode when authorized. Preserve the original historical runner/receipt. Coordinate one integration owner for a shared warm Editor; read-only analysis can proceed without competing scene/Play/save-override mutations.

Use existing Core checks and the warm-Editor/private-save loop in [unity-verify](.agents/skills/unity-verify/SKILL.md) for relevant game changes. Reuse evidence only when its input identities and relevant conditions still match. Record the first failure and explain any retry. A successful unit check does not establish UI, native or human-play acceptance. Documentation/skill-only edits need frontmatter, links, routing and diff checks, not Unity imports, gameplay tests or an APK rebuild.

Keep captures and temporary outputs task-local under `.work/`. Set a budget before recording or profiling; stop capture at the budget, retain the useful receipt, and remove only confirmed disposable closed files in a validated directory. A suggested retention period is not an installed cleanup mechanism. Preserve normal saves and the existing Editor layout through the verification skill.

Report the changed behavior, scoped evidence and material limits. Use the existing STATUS entry/handoff for results rather than creating a second progress ledger. Historical research is optional input: check its source pin and modeled features before reusing any output.
