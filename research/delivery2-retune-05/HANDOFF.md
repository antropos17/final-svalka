# Focused delivery Tier2 retune

Base: `bff8b564bc55eeec24376a7cfa6c08631e4d27b0`. Only `skill.delivery.2` changes from5 to20 for **new** quotes under `skill.prices.v3`. All other v2 prices, effects (+1 per completed delivery), prerequisites, construction/offline rules and symbol probabilities remain fixed. Original research and the preceding price study are unchanged.

## Before/after proof

Deterministic phase DP reuses the exact previous external model inputs, pinned by the six SHA256 values in both result files and checked against the previous committed study before and after calculation. It propagates floating-point mass over rational64-outcome probabilities and wallet/carry states; no Monte Carlo. Foreground only, initial37, three fixed construction policies, each with Delivery1 at its earliest and best legal insertion. The two parent conditionings happen to coincide here; this is six cases, not six independent strategies or all legal orders.

Delta means expected chapter-completion spins with the optional Delivery2 purchase minus the identical parent-conditioned plan without it. Negative favors buying; positive favors saving. Rows below apply to both tested parent conditionings. Every late point is **before** the last construction, not after completion.

| Policy | Old5 best | Old5 late | New20 best | New20 late | New beneficial/saving positions |
| --- | ---: | ---: | ---: | ---: | --- |
| workshop-first | -2.142947 | -0.098952 | -1.345630 | +0.544558 | 5/2 |
| building-first | -1.699939 | -0.098942 | -0.933197 | +0.544496 | 4/2 |
| family-order | -1.187421 | -0.098952 | -0.448995 | +0.544558 | 2/3 |

At20, the best workshop-first/building-first insertion is before `power-source.workshop-yard.upgrade-1`; family-order is before `pigeon-loft.workshop-yard.upgrade-2`. The later saving point is before `power-source.workshop-yard.upgrade-2` in every case. Plan legality is checked by the unchanged owning model. Both endpoints were cross-checked against its unmemoized evaluator within1e-9 spins.

The result removes the observed all-position dominance at5. The tradeoff is **modest**: worst-case early benefit0.449 spin and smallest late loss0.544 spin. It does not meet the preceding study's >=1-spin cushion and is not evidence of meaningful player strategy, fun or natural pacing.20 is the round candidate balancing these two directions;19 sensitivity yields approximately0.498 benefit/0.502 late loss, which does not justify precision to a single coin in this provisional balance.

## Bounded candidates and limitations

| Price | Worst-policy best delta | Smallest late delta |
| --- | ---: | ---: |
|5|-1.187421|-0.098952|
|10|-0.941152|+0.115601|
|15|-0.695055|+0.330153|
|20|-0.448995|+0.544496|
|25|-0.202783|+0.758966|
|30|+0.043447|+0.973481|
|40|+0.535639|+1.402475|
|60|+1.520280|+2.260387|

The requested eight-point coarse sweep took9.882s. Four neighboring checks7/8/19/29 took4.963s. At7 at least one late point still favors buying (-0.013131); at8 all tested late points favor saving (+0.029779 minimum).29 still has a negative best delta in every case (-0.005790 worst), while30 loses that property. These are tested neighboring boundary brackets; intervening integers and other construction orders were not exhaustively searched. No stronger integer/global optimality claim follows.

`results.json` and `boundaries.json` retain every pre-completion legal insertion and exact unrounded deltas. There are no offline credits, joint all-skill optimization, seeded percentile simulations, UI, device or player observations here.

## Compatibility and reproduction

The immutable prior profiles stay available: v1 Delivery2=540, v2 Delivery2=5. V3 Delivery2=20 is the new CurrentRevision; the six-node total is610. `SkillTree.Find` explicitly resolves all three profiles, avoiding a two-profile fallback that could misread v2 as v1. Version3 ownership format already carries per-receipt pricing; its shape/bounds and old identity suffixes do not change. Old v2 quotes are stale for new foreground purchases; exact applied replay and RestorePurchase retain the frozen5 debit. No refund, recharge, migration write or grant is introduced.

Before changing the profile, unmodified bff8b5 code emitted seven literal v2 ownership snapshots. `V2SkillFixtures.cs` retains their exact bytes and expected tokens, including the5-coin last purchase and9405 final wallet. Focused tests verify these historical pending results/replays, mixed v2/v3 ownership,20-coin new purchases, wrong-price/retag rejection and identical effects. Existing broader22-group/8081-assertion evidence remains dated v2 evidence; it was not relabeled as a v3 full run. Only the six affected pricing compatibility groups plus three new focused groups were executed for this follow-up; all passed on the first attempt. New Unity wrapper was not run.

```powershell
$env:TEMP='X:/tmp/jtn-d2-retune05/Temp'; $env:TMP=$env:TEMP
$env:PYTHONDONTWRITEBYTECODE='1'
python -B research/delivery2-retune-05/scan.py --lab X:/PROJECTS/FINAL-SVALKA/research/progression-lab/2026-10-29-claude-opus --output X:/tmp/jtn-d2-retune05/reproduced.json
python -B research/delivery2-retune-05/check_proof.py
dotnet run --project Checks/SkillsChecks/SkillsChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-d2-retune05/obj/ -p:MSBuildProjectExtensionsPath=X:/tmp/jtn-d2-retune05/obj/ -p:BaseOutputPath=X:/tmp/jtn-d2-retune05/bin/ -- --pricing-only
```

02 acknowledged V3/20 before activation. It owns ordinary-save audit, request codec/pinned recovery, new quotes, runtime/UI and connected qualification. The stream did not touch Core, user saves, Editor, APK or settings. Exact X outputs are task-owned; inspect closed disposable files after7 days/8MiB while preserving receipts. No automatic retention was installed.
