# Compatible house540 construction pricing

Pure implementation on source-only branch `codex/construction-pricing-r11`, based on primary commit1b15ed91078480a9bdcdf3883c99d90d6cb42327. The qualified0ca71b4 proposal is implemented here, not merged into the primary Editor checkout. Owner02 retains GameSession/performance, Runtime, scene and integration checks. No32-session timing batch, Candidate A or Python was repeated.

## Behavior and API

`ConstructionPricing` adds independent `construction.prices.v1`/`construction.prices.v2`. V1 resolves the literal ChapterData catalog, including house600. V2 changes only `house.improve` to540. The24-project totals are9460/9400. Repair40, initial37, all other23 prices, effects, source IDs, RNG, shared delivery carry, all skill revisions, orders and offline rules remain unchanged. Content references only Contracts; no Core dependency or assembly-definition change is introduced.

- `ChapterEconomy.ProjectPrice(string id)` quotes a **new current offer**. Runtime selection/confirmation should use it.
- `ChapterEconomy.SelectedProjectPrice(ChapterState state)` reads the **active frozen term**, and throws when no target is selected. Runtime active-goal progress should use it.
- `ChapterState.ConstructionPriceRevision` is immutable, defaults to V1 in the existing constructor, and participates in state identity. The optional strict JSON field `pricing` is emitted only for V2. Legacy V1 canonical bytes stay unchanged; present null, non-string, unknown and duplicate fields reject.

Every new modern `select`, including explicit reselection and immediate completion, adopts V2 in its after-state. Frozen selection validation resolves the **after-state** price revision; this distinguishes new540 selection from an old600 immediate purchase even when both clear selection. A selected legacy target remains V1 until completion or explicit reselection/replacement. A spin resolves the **before-state** price revision. Skill, clock, offline, transition and acknowledgement commands preserve that revision. Non-selection revision changes/downgrades reject, as does revision regression after a retained V2 spin. No eager migration, new grant, refund, price compensation or extra write is added.

The unchanged legacy integer-target protocol and its literal r6/r7 pending operations continue to recover under their original600 rules. The active modern construction API uses the new offers. Older executables do not understand V2 fields; downgrade compatibility is not claimed.

Missing revision cannot distinguish a genuine legacy selection from a damaged new leaf snapshot whose only pricing field was removed. Such an unprovable case preserves legacy600; the loader never invents V2 history or silently discounts it. Where frozen receipts/pending state retain V2 evidence, missing/conflicting/downgraded state fails validation. A clear explicit reselection creates a new540 term.

## Producer verification

The new focused check project runs the actual pure C# modules, strict codec and journal. Final result:307 assertions PASS. Cases include old550+8 remaining below600, explicit/new immediate540 debit, old600 immediate pending select, four old-spin write-fault positions, exact replay/no extra IO, a real private Windows-file backup recovery, new selected and immediate pending540 recovery, preservation across skill/clock/offline, retained old600 receipt after a new V2 offer, and strict malformed/stale revision rejection. It loads54 immutable r6/r7/r8 snapshots plus the separate literal skillV2 module payload and checks all18 V1/V2/V3 skill prices. A module payload is not represented as a full save.

The existing ContentChecks project also passes. It compiles Content and Contracts without Core, checking the assembly boundary that the first build exposed. Its25-action catalog graph fixture retains the historical9460 catalog total and540 ending balance: this is deliberately V1 data, not an active V2 gameplay wallet.

```powershell
$env:TEMP='X:/tmp/jtn-house-pricing03-r11/.work/temp'; $env:TMP=$env:TEMP
dotnet run --project Checks/ConstructionPricing/ConstructionPricingChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-house-pricing03-r11/.work/obj/ -p:BaseOutputPath=X:/tmp/jtn-house-pricing03-r11/.work/bin/ -p:UseSharedCompilation=false -- X:/tmp/jtn-house-pricing03-r11/.work/new-private-cases X:/tmp/jtn-house-pricing03-r11
```

Use a new exact test directory; the entry point refuses an existing one. `ConstructionPricingRegression.Run(privateRoot, repositoryRoot)` returns the result groups for integration. Both roots are explicit to avoid normal saves or broad path discovery.

Actual failures are preserved with the [evidence](../../Checks/ConstructionPricing/evidence/qualification.json): the first build found a shadowed local and an invalid Content→Core reference; both were corrected. A later test extension treated the existing V2 ownership-only payload as a full save and failed strict shape validation. It was corrected to use SkillOwnershipCodec without changing production behavior. Earlier successful277/285 cases remain under `.work`; the expanded final307-case result is frozen separately. No failed verdict was relabelled.

## Integration boundary for02

Existing CoreRegression/CoreRegressionUnity/CoreChecks wiring was deliberately left untouched by agreement. Their new-command expectations still describe9460 and must be updated by the integrator, while literal old receipts retain600. Required full CoreChecks, Unity compile/EditMode/UI and any later APK acceptance remain pending integration. The producer does not claim native or complete release acceptance.

Affected current-command arithmetic in ChapterEconomyRegression: Junkyard3120→3060 and6880→6940; total9460→9400 and final540→600; full skills9930→9990; following29-coin spin9959→10019. Update the corresponding expected fault results and malformed frozen-wallet fragment9959/9960→10019/10020. JourneyEconomyRegression's newly constructed Completed/Accept endpoint changes540→600. Its literal old rich3569 case must remain unchanged. No blind replacement of the number540 is appropriate: it also occurs as an old skill price and in historical receipt fixtures.

Next action: owner02 completes the already running performance A/B on its unchanged baseline, then integrates this immutable commit, uses the two pricing APIs in Runtime, updates owned current-command test expectations, wires the new cases and runs the required affected integration checks once. No primary files, ordinary saves, Unity import or APK were touched here. Disposable `.work` output has manual seven-day retention in this exact checkout, skipping links/locked files; source and committed receipts are preserved. No automatic cleanup is claimed.

## Primary integration acceptance

Integrated as bde1f54 with connected adapter/source checkpoint bd8fbe8. Final307 assertions ran again against integrated bounded-reader/performance source, preserving54 literal saves. Current Core49 and Unity EditMode11 pass; actual focused PlayMode proves200/540 current goal/skill text,550/600 legacy goal/skill text before explicit pointer reselection, then one540 debit to10 without RNG. Independent07 closed the Runtime blocker. Exact receipts and remaining native/pacing limits are in docs/evidence/unity/integration-r11/verification.json.
