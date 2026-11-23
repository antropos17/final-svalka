# Cadence comparison: preserve current540

Source-only base d914da2, with accepted production76a379f. **No candidate is selected by this producer checkpoint; the clarified coordinator tradeoff decision is separate.** Exactly two declared profiles ran through actual Core, three fixed seeds1/12305/305419896 and both existing policies per profile:12 new fresh sessions. No baseline campaign, Python, orders, offline, Unity or APK run occurred. Every economic/accounting check passed; profile eligibility is a separate design criterion, not a Core correctness verdict.

## Current observed intervals

The accepted Runtime result records a real normal-speed automated Editor route. Its24 construction events contain zero skill purchases between constructions; six optional skills follow chapter completion603. These are not observations of human boredom. Intervals include pointer/menu actions, frame and storage overhead. Nominal spin projections below are separate; the initial fresh-to-repair commit is not a full animation-cycle measurement.

| Construction reached | Recorded interval seconds | Foreground spins | Pointer events | Skill purchases |
| --- | ---: | ---: | ---: | ---: |
| house.improve | 141.8047681 | 37 | 6 | 0 |
| power-source.workshop-yard.upgrade-2 | 123.3231532 | 32 | 7 | 0 |
| cart.workshop-yard.upgrade-2 | 123.2233797 | 32 | 7 | 0 |
| house.workshop-yard.upgrade-2 | 119.3420079 | 31 | 6 | 0 |
| recycler.workshop-yard.upgrade-2 | 119.2636969 | 31 | 6 | 0 |

All24 intervals and the Runtime artifact hash are in the ConstructionPricing cadence evidence `runtime-gaps.json`. The existing design cross-check supplies no universal correct wait threshold. The model's skills-first policy has different early cart/power gaps, so reducing only the house price would not test both concrete concerns.

## Declared candidates and acceptance criterion

| Current offer | Current V2 | Small A | Wider B |
| --- | ---: | ---: | ---: |
| house.improve | 540 | 500 | 480 |
| cart.build | 160 | 150 | 140 |
| power-source.build | 200 | 190 | 190 |
| All24 construction purchases | 9400 | 9340 | 9310 |

Other21 prices and all effects, odds, income, skills610, grant37 and repair40 remain unchanged. The cuts are60/90 coins across the chapter, below1% of construction spending. The existing cheapest construction order remains intact. Cart still costs more than the120 universal skill, and power more than the160 recycler skill: the tested early-skill spending windows remain120 before cart and280 before power, with positive construction opportunity costs.

Before execution, the producer and02 accepted this local criterion: improve the longest construction gap by at least one spin in **every** matched case; do not worsen the gap ending at cart, power or house; preserve first repair/workshop, construction order and early-skill opportunity windows. Prefer A if both pass; adopt neither if neither passes. This intentionally conservative criterion is not a universal standard for a good economy.

## Actual Core comparison with reused540 baseline

| Profile/policy, three seeds | Longest gap median/max nominal seconds | Chapter median spins | Paired chapter-spin saving median/range | Eligible cases |
| --- | ---: | ---: | ---: | ---: |
| Current540/construction-first | 138.75/138.75 | 603 | reference | reference |
| Small A/construction-first | 120/135 | 598 | 5/5–5 | 3/3 |
| Wider B/construction-first | 120/127.5 | 595 | 8/7–8 | 3/3 |
| Current540/skills-first | 138.75/142.5 | 569 | reference | reference |
| Small A/skills-first | 131.25/135 | 563 | 5/4–6 | 1/3 |
| Wider B/skills-first | 131.25/131.25 | 562 | 7/7–7 | 2/3 |

The maximum gap improves in all12 cases, and saving/choice windows remain. However, A increases the power interval from32 to33 spins for skills-first seeds1 and305419896; B does so for seed1. This is3.75 nominal seconds. The absolute power purchase is not later: A keeps those endpoints at101/104 spins, and B reaches seed1 power at100 instead of101. The preceding construction became earlier. Thus this is a local interval redistribution, not a claim of worse total progress, a runtime defect or observed dissatisfaction. It nevertheless fails the declared no-worsened-target criterion; the producer does not relax that criterion after seeing results.

After the captured results, coordinator00 clarified that zero local regressions was a producer screening threshold, not a human requirement. Original A4/6 and B5/6 screening verdicts remain unchanged. `tradeoffs.json` separately lists every positive interval delta across all24 IDs, not only the original three screening targets, and gives all six baseline/A/B cases plus policy/house aggregates. Other later gaps grow by one or two spins even in cases passing the original three-target screen. No third profile, executable rerun or post-hoc PASS is added. Current540 stays authoritative pending an explicit tradeoff/adoption decision; these numbers do not establish natural player enjoyment.

## Source, compatibility and replay boundaries

The six baseline records are extracted from the immutable0ca71b4 house540 experiment, hash5AB11FDB1201835F39BB7BCF9BB94867865E23A0420439C33E253D1151E43943. Their seed, policy,9400 construction/610 skills,37 grant and zero offline income are checked. Actual r11/e53a22b matches both seed1 policies, and the accepted full Runtime matches construction-first seed1. Those are not live-UI claims for the other seeds. Source proof binds the unchanged RNG/effect/skill contracts and all actual producer inputs; raw baseline records are retained in each candidate result without re-simulation.

Every new run checks wallet conservation after operations, exact24 construction count and declared9340/9310 total,610 skill spending and the intended RNG draw count. Result JSON retains all purchases, milestones, per-project spin gaps, eligibility predicates and terminal seed/carry. There are no stalls at the2000-spin cap. Both first invocations passed economic checks; the inherited CS0414 unused historical Profile warning is preserved. No failed eligibility result is relabelled as a qualifying candidate.

The experimental V3 prototype also exposed a necessary historical-validation contract: a Current-only test would reject a genuine old V1-before/V2-after pending selection540, or a V2 state retaining a V1 spin600. A monotonic CanAdvance helper plus exactly two ChapterEconomy validation expressions was used in the temporary overlays. New selections still require Current; no skill/new-quote path was relaxed. Before Current changed, six genuine V2 JSON snapshots were captured once through the unmodified r11 Core/codec/journal, using explicitly funded private legacy fixtures. They cover selected535, V1→V2 pending selection, instant550→10, pending/paid535+8−540 and V2 state retaining a paid600 spin. Capture inputs/hashes and raw literals are preserved under ConstructionPricing cadence evidence.

Because no profile is adopted, the producer restored both Content and those two Core expressions exactly to the base. The separate prospective R12 boundary-verification code in the archived capture driver was compiled out during capture and **was not run**; no historical V3 release/migration qualification is claimed. A future selected revision still needs its literal matrix and combined history checks plus independent review. Original V1/V2 prices, user progress and historical receipts are unchanged.

To reproduce only a frozen candidate in a fresh private source copy of d914da2, overlay `overlays/ChapterEconomy.cs` and the corresponding `ConstructionPricing-small.cs` or `ConstructionPricing-wider.cs` into their declared Core/Content paths. Compile the existing ProgressionTiming project with `-p:CadenceR12=true`, X: intermediate/output/TEMP paths, and `--candidate small|wider --qualified <byte-exact old house540 JSON> --output <new JSON>`. The input can be recovered with `git show 0ca71b4:Checks/ProgressionTiming/evidence/tuning-r10/house-540.json`; preserve native bytes. The mode refuses mismatched price fields/input hashes, fixes three seeds/two policies, uses zero orders/offline, bounds output to1MiB, and checks a180-second budget at case boundaries. Do not apply experimental overlays to the primary project. The active V2 source intentionally fails the candidate-price guard rather than silently testing another table.

Only02 may integrate and07 reviews the frozen evidence. This commit needs no primary production or APK change. Generated artifacts remain in the exact X: `.work` directory with manual seven-day retention of closed disposable files, skipping links/locked files; durable receipts and source are preserved. No automatic cleanup is claimed.

## Same-data full24 tradeoff table

Numbers are baseline/A/B spins. Any positive gap change, including unchanged-price later projects, is retained in the following lists.

| Policy / seed | Maximum gap | House gap | Chapter | All skills |
| --- | ---: | ---: | ---: | ---: |
| construction-first / 1 | 37/32/32 | 37/32/31 | 603/598/595 | 626/621/618 |
| construction-first / 12305 | 37/36/34 | 37/36/34 | 606/601/598 | 629/624/622 |
| construction-first / 305419896 | 35/32/32 | 35/32/32 | 585/580/578 | 610/605/603 |
| affordable-skills-first / 1 | 35/33/33 | 32/29/28 | 569/563/562 | 569/563/562 |
| affordable-skills-first / 12305 | 38/36/35 | 32/29/30 | 573/568/566 | 573/568/566 |
| affordable-skills-first / 305419896 | 37/35/35 | 30/28/26 | 553/549/546 | 553/549/546 |

- construction-first seed1:
  smallA: pigeon-loft.build:22→24(+2); recycler.improve:24→25(+1); power-source.improve:26→27(+1); cart.workshop-yard.upgrade-1:26→28(+2); pigeon-loft.workshop-yard.upgrade-1:24→25(+1); workshop.workshop-yard.upgrade-2:29→30(+1); pigeon-loft.workshop-yard.upgrade-2:30→31(+1).
  widerB: recycler.build:21→22(+1); pigeon-loft.build:22→24(+2); power-source.improve:26→27(+1); cart.workshop-yard.upgrade-1:26→28(+2); pigeon-loft.workshop-yard.upgrade-1:24→25(+1); workshop.workshop-yard.upgrade-2:29→30(+1).

- construction-first seed12305:
  smallA: recycler.build:21→22(+1); workshop.improve:22→23(+1); power-source.improve:22→23(+1); recycler.workshop-yard.upgrade-1:27→28(+1); pigeon-loft.workshop-yard.upgrade-1:24→25(+1); cart.workshop-yard.upgrade-2:31→32(+1); recycler.workshop-yard.upgrade-2:31→32(+1).
  widerB: recycler.build:21→23(+2); workshop.improve:22→24(+2); power-source.improve:22→23(+1); house.workshop-yard.upgrade-1:27→28(+1); recycler.workshop-yard.upgrade-2:31→32(+1).

- construction-first seed305419896:
  smallA: pigeon-loft.build:22→23(+1); pigeon-loft.improve:25→27(+2); cart.workshop-yard.upgrade-1:24→25(+1); pigeon-loft.workshop-yard.upgrade-1:24→26(+2); cart.workshop-yard.upgrade-2:29→30(+1); workshop.workshop-yard.upgrade-2:29→30(+1); pigeon-loft.workshop-yard.upgrade-2:28→29(+1).
  widerB: pigeon-loft.build:22→24(+2); pigeon-loft.improve:25→26(+1); cart.workshop-yard.upgrade-2:29→30(+1); pigeon-loft.workshop-yard.upgrade-2:28→29(+1).

- affordable-skills-first seed1:
  smallA: recycler.build:15→16(+1); power-source.build:32→33(+1); power-source.improve:24→25(+1); cart.workshop-yard.upgrade-1:23→24(+1); workshop.workshop-yard.upgrade-1:21→22(+1); pigeon-loft.workshop-yard.upgrade-1:28→29(+1).
  widerB: recycler.build:15→16(+1); power-source.build:32→33(+1); power-source.improve:24→25(+1); cart.workshop-yard.upgrade-1:23→24(+1); pigeon-loft.workshop-yard.upgrade-1:28→29(+1).

- affordable-skills-first seed12305:
  smallA: recycler.build:18→19(+1); pigeon-loft.build:19→21(+2); recycler.improve:22→23(+1); pigeon-loft.improve:24→26(+2); house.workshop-yard.upgrade-1:28→29(+1); power-source.workshop-yard.upgrade-1:20→21(+1); workshop.workshop-yard.upgrade-2:26→27(+1); pigeon-loft.workshop-yard.upgrade-2:24→25(+1).
  widerB: recycler.build:18→19(+1); pigeon-loft.build:19→21(+2); pigeon-loft.improve:24→25(+1); workshop.workshop-yard.upgrade-1:22→23(+1); power-source.workshop-yard.upgrade-1:20→21(+1); workshop.workshop-yard.upgrade-2:26→27(+1); pigeon-loft.workshop-yard.upgrade-2:24→26(+2).

- affordable-skills-first seed305419896:
  smallA: recycler.build:19→20(+1); power-source.build:32→33(+1); cart.improve:20→21(+1); power-source.improve:22→23(+1); cart.workshop-yard.upgrade-1:21→22(+1); house.workshop-yard.upgrade-1:24→25(+1); cart.workshop-yard.upgrade-2:25→26(+1); house.workshop-yard.upgrade-2:24→25(+1).
  widerB: cart.improve:20→21(+1); power-source.improve:22→23(+1); cart.workshop-yard.upgrade-1:21→22(+1); house.workshop-yard.upgrade-1:24→25(+1); house.workshop-yard.upgrade-2:24→25(+1).
