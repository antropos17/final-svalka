# Explicit selection of captured profile B

After evidence-only commit704e03d3fbeb304f6a271e90a4a875448c218c14, coordinator00 explicitly selected the **unchanged captured B** for review and affected acceptance: house improvement480, cart build140, power-source build190;24 construction purchases total9310, skills610, repair40 and grant37 unchanged. The original strict screening remains A4/6 and B5/6. That screening was a producer threshold, not a human requirement, and is not relabelled as PASS.

The selected tradeoff reduces the maximum gap by2–5 spins across the six matched cases, while some individual gaps grow1–2 spins, including unchanged-price later projects. `tradeoffs.json` in the first freeze lists every such change across all24 construction IDs. Three-seed maximum-gap medians change37→32 construction-first and37→35 skills-first; chapter savings are7–8 spins. Construction order, first repair/workshop and early skill spending/opportunity windows remain. This supports a bounded cadence adjustment, not a player-enjoyment claim.

## Exact source and historical contract

The two active production files are byte-identical to the already-executed B overlays:

- Content/ConstructionPricing.cs SHA256:361D90AAE651C2D67CC034B0E0E5B38ABF2220F46F224C22490047E301C4786B.
- Core/ChapterEconomy.cs SHA256:27643E9B3F9F926FD8ECD304B9512A99CB57034CDE3C678774AFC2B66C9BC5A3.

V1/V2 retain every original24 price. V3 changes only the three selected IDs. ChapterCatalog/ChapterData/effects, all skill-price revisions, RNG, carry, orders and offline income are unchanged. Existing `ProjectPrice(string)` quotes CurrentV3, while `SelectedProjectPrice(ChapterState)` retains the saved revision. No Runtime API or state/codec format changes are introduced. Older executables do not understand V3; downgrade compatibility is not claimed.

`CanAdvance` accepts only known same/newer revisions and is used at exactly two historical validation sites: frozen selection after-state versus before-state, and current state versus retained spin after-state. This preserves genuine V1→V2 pending selections and a V2 state retaining a V1 receipt when Current becomesV3. It does not permit new selections or skill quotes to request an old revision. Reserve/quote code remains Current-only; non-selection commands cannot change their saved revision. There is no eager rewrite, refund, compensation or new grant.

## Selected-revision qualification

One new, first-invocation compatibility run passes390 assertions. No capture, baseline or candidate progression was repeated. It checks the literal nine-pair history matrix plus unknown/backward refusals; independent all24 V1/V2/V3 price vectors and9460/9400/9310 totals; the six authentic snapshots captured before Current changed; exact old pending/paid/replay amounts and no eager IO; selected535+8 still paying old540; explicit535−480=55; V1 selected550+8 remaining558/600 until explicit480 reselection leaves78; non-selection revision tamper rejection; fresh37+8−40=5; stale historical skill quote refusal without writes or poisoning;54 immutable old r6/r7/r8 saves; and two actual private-file backup restores at old540/600 with exact replay/reopen identity. The original capture manifests and bytes remain unchanged.

```powershell
$env:TEMP='X:/tmp/jtn-economic-equivalence03-r11/.work/cadence-temp'; $env:TMP=$env:TEMP
dotnet run --project Checks/ConstructionPricing/ConstructionPricingChecks.csproj -p:R12Boundary=true -p:BaseIntermediateOutputPath=X:/tmp/jtn-economic-equivalence03-r11/.work/selected-obj/ -p:BaseOutputPath=X:/tmp/jtn-economic-equivalence03-r11/.work/selected-bin/ -p:UseSharedCompilation=false -- --selected-r12 X:/tmp/jtn-economic-equivalence03-r11/Checks/ConstructionPricing/evidence/cadence-r12/v2 X:/tmp/jtn-economic-equivalence03-r11 X:/tmp/jtn-economic-equivalence03-r11/.work/new-selected-cases
```

Use a new exact private root. The original selected run output and source/input hashes are preserved. Existing ConstructionPricingRegression.cs and full Core/Unity/Runtime acceptance remain owned by02; their fresh-current expected values need explicit adaptation while literal600/540 goldens remain unchanged. No primary writes, ordinary saves, Editor, import, APK or visual/audio changes occurred here. This producer is ready for07's frozen review, not a claim of integrated release acceptance.
