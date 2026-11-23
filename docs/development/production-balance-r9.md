# Production economy and timing, r9 / TASK31

The actual r9 C# Core completes both tested policies without stalls. Buying affordable skills during construction reaches the chapter sooner than deferring all skills, but delays some early world changes. The exact proposed Candidate A saves only about six additional spins versus current V3 and does not remove long construction gaps. No production price, effect, save schema or runtime change is recommended or included by this measurement alone.

## Source, execution and sample

Production source is c0863958b1bcf8d9fbdcc18957a5371192756733, packaged at a1f9bff;3079397 supplies the completed-wave roadmap. The runner calls real GameSession/GameStore and the strict codec, with memory storage. It does not copy the Python economy or write ordinary saves. The43 pure-source SHA256 values, exact commands, outputs and runner versions are in [production inputs](../../Checks/ProgressionTiming/evidence/production-inputs.json) and [final qualification](../../Checks/ProgressionTiming/evidence/final-qualification.json). Reproduction is in [the runner README](../../Checks/ProgressionTiming/README.md).

Main sample:16 fixed uint seeds ×2 explicit policies, plus the first4 seeds ×2 policies with one two-hour return after workshop purchase:40 complete fresh sessions. Every session makes24 construction purchases (9460 coins), owns all6 current skills (610 coins) and forks the endpoint into both postchapter order types, three instances each. Candidate A adds16 paired fresh sessions for the skill-first policy only, one order instance/type; it is a separate experiment. A single final-dispatch parity case reuses seed1 and matches the main result. No new seed/policy sweep, Python rerun, Editor or APK was started.

The seed list is1,12305,0x12345678,0x9e3779b9,0xa341316c,0xc8013ea4,0xad90777d,0x7e95761e,0xdeadbeef,0x6d2b79f5,0xf00dcafe,0x13579bdf,0x2468ace1,0x85ebca6b,0xc2b2ae35,0x27d4eb2f. These are deterministic probes, not a random population sample. Quantiles use nearest rank; n16 p95 equals the sample maximum. Medians of integer spin counts may end in.5. Upper tails are observed sample tails, not promised playtime or confidence bounds.

## Policies and clock interpretation

Both policies choose the cheapest available construction when no target is selected, sorting by price then ordinal stable ID, preserve that target until completion, and enter/ack the era immediately. Construction-first defers every skill until chapter end. Affordable-skills-first buys the cheapest unlocked skill only when it is already affordable before the next spin; it otherwise continues building. It does not idle indefinitely to save for an unaffordable skill. Full policy details and all purchases are in [raw current results](../../Checks/ProgressionTiming/evidence/production-r9.json).

The exact64 Core outcomes are4 triples/36 pairs/24 different, total452 coins: **7.0625 base coins/spin**. At uninterrupted3.5 seconds this is121.0714 coins/minute; configured AUTO adds.25 seconds, giving113.0 before upgrades. These are mathematical rates with no decision, storage or frame overhead. The37-coin fresh grant is recorded separately from earned income: it ensures the first40-coin repair after one spin even at the minimum3 reward. It is not recurring income. Seed1 independently verifies37+8−40=5. Wallet accounting separates spin income, grant, offline income, construction/skill spending and later explicit order rewards.

Core itself has no animation timer. Projection uses manual3.5 seconds for the first repair, AUTO3.75 during eligible chapter play, and manual3.5 after completion because AUTO is then disabled. Basic times assume zero reading, target selection, menu/confirmation, resume and AUTO-restart delay. The optional2-second-per-explicit-decision column is a sensitivity assumption, not measured human latency. Construction gaps mean time between committed upgrades during active chapter play; no rendering/visibility observation is claimed. Postchapter activity intentionally has no further construction goal.

Owner02 separately measured the warm Editor on the same game source, receipt commit c824c067cae4ea00335544b91b539539456769c5: [raw scope and values](../../Checks/ProgressionTiming/evidence/foreground-timing-r10.json). Three stable manual cycles were3.5098949/3.5150866/3.5154286 seconds; median3.5150866. Six AUTO intervals ranged3.7475977..3.7741419; median3.7636855. The observer has one Editor.update-interval precision. An initial0.124-second eval-frame anomaly is preserved and excluded; starts were then made after at least two stable frames. These are programmatic handler observations on a1080×2340 Simulator, early repaired-house/no-workshop state, not physical input or a full chapter. The measured source hashes match the pin; the helper's receipt hash matches UTF8/LF content while this checkout uses CRLF. Later unrelated primary instrumentation is outside the pin.

Using those observed medians as a simple calibration moves the current chapter medians from37.65→37.79 minutes (construction-first) and35.40→35.53 (skills-first). This conversion does not establish late-chapter file-IO performance. It is smaller than the unmeasured human/decision delays. No controlled device/session timing exists here.

## Current V3 results: continuous play, n16 per policy

| Milestone | Construction-first median / p90 / max spins | Affordable-skills-first median / p90 / max spins |
| --- | ---: | ---: |
| First repair | 1 /1 /1 | 1 /1 /1 |
| Workshop | 18 /21 /21 | 18 /21 /21 |
| Junkyard ready | 270.5 /276 /285 | 271.5 /278 /287 |
| Chapter complete | 602.5 /612 /613 | 566.5 /577 /578 |
| All six skills owned | 626.5 /634 /638 | 566.5 /577 /578 |

At chapter end construction-first has0 skills/0 skill spend; skills-first has6/610. Nominal AUTO-eligible chapter medians are37.65 and35.40 minutes; manual-only cycle floors are35.15 and33.05. Adding the illustrative2 seconds per explicit decision gives38.55 and36.50 minutes. Construction-first's all-skill endpoint is39.05 minutes with manual postchapter gathering. The next-era/ack panels and every purchase are legal actual Core commands; their duration is assumed, not skipped in the save state.

Early skills are not free pacing: the median first-era boundary is slightly later and cart/power waits grow. The longest per-run active construction gap has median151.875 seconds, p90/max165 for construction-first; median138.75, p90142.5/max146.25 for skills-first. The bottleneck target is usually house.improve. These two cheapest-construction policies do not test all legal orders; the lab's longer family-order waits are neither reproduced nor disproved.

| Gap ending at project | Construction-first median / max seconds | Skills-first median / max seconds |
| --- | ---: | ---: |
| cart.build | 75 /90 | 123.75 /142.5 |
| power-source.build | 78.75 /90 | 121.875 /135 |
| house.improve | 151.875 /165 | 135 /146.25 |

Thus aggregate chapter speed and early unchanged-world gaps are different objectives. This is evidence of a timing tradeoff, not evidence that players enjoy or understand it.

## Separate return-income probes

Eight paired probes leave immediately after workshop purchase and return after7200 logical seconds. Actual Core grants120 once, without RNG/carry change or hidden construction. They save13–16 foreground spins (48.75–60 nominal active seconds), while total elapsed time increases by7140–7151.25 seconds. Do not describe the two-hour absence as a faster wall-clock completion. Skills do not increase offline income. A separate real-Core boundary check grants the8-hour cap480 for a10-hour return and verifies same-clock/rollback do not pay again. This is a small paired scenario probe, not a population-tail estimate.

## Repeatable orders after chapter and all skills

Orders use manual cycles after chapter end. For each seed/policy, both types fork the same all-skill wallet/RNG/carry checkpoint; three sequential instances preserve carry and require explicit acceptance/claim. Offline and further purchases are excluded. The comparison below uses the32 continuous sessions only:16 seeds/policy,48 instances/type/policy. Seed-level aggregates prevent treating the three dependent instances as separate independent samples.

| Policy / order | Three-instance median / p90 / max spins | Median nominal manual seconds | Median extra claim coins/minute |
| --- | ---: | ---: | ---: |
| Construction-first /12 trash→120 | 46 /58 /60 | 161 | 134.22 |
| Construction-first /3 deliveries→180 | 32.5 /39 /40 | 113.75 | 284.90 |
| Skills-first /12 trash→120 | 48 /58 /62 | 168 | 128.63 |
| Skills-first /3 deliveries→180 | 34.5 /40 /46 | 120.75 | 268.38 |

Claim rates exclude ordinary spin income and accept/claim interaction delay. Delivery yields higher extra claim income per nominal minute in all32 paired endpoints and fewer spins in31/32. This documents current optional-order payout asymmetry; it is not a guaranteed pathwise result or a justification for adding a mandatory spending sink. With all skills owned, these coins have no further mandatory goal. No order reward/target change is proposed for integration by this report.

## Candidate A: marginal actual-Core comparison

The user-supplied [proposal](../../Checks/ProgressionTiming/evidence/candidate-a-proposal.json) is price-only: U2 160→120, R2 90→40, D1 60→40; other prices/effects remain. **Its explicit prices total500, not the handoff's540**:120+120+160+40+40+20. The reduction from current610 is110. The first candidate guard failed before simulating because of that inconsistent declared total; [the failure note](../../Checks/ProgressionTiming/evidence/candidate-first-failure.txt) is retained. We corrected the runner's expected total and left the proposed fields untouched.

The experiment exports actual c086395 C# source to X: and adds only the proposed V4 price table/resolver in two Upgrades files there, preserving old revisions. Core/effects/schema and the production checkout remain unchanged. It uses fresh sessions only; it is not a release or old-save migration qualification for hypothetical V4. Candidate B is excluded. Python outputs remain supporting research; no Python run or economy translation was used. Exact candidate inputs/results are linked in [final qualification](../../Checks/ProgressionTiming/evidence/final-qualification.json).

| Same16 seeds, affordable-skills-first | Current V3 | Exact Candidate A |
| --- | ---: | ---: |
| Six-skill cost | 610 | 500 |
| Chapter median / p90 / max spins | 566.5 /577 /578 | 561 /571 /572 |
| Nominal chapter median minutes | 35.40 | 35.06 |
| Median longest construction gap seconds | 138.75 | 136.875 |
| Maximum construction gap seconds | 146.25 | 142.5 |

The paired saving is median6 spins/22.5 nominal seconds, range5–8 spins/18.75–30 seconds; mean6.0625 spins. This is roughly1.1% over current V3. **599.586→559.0 in the Opus report compares no skills with A-buy-all, not V3-buy-all with A-buy-all.** The current production comparison above isolates the marginal effect. It does not qualify the lab's per-node insertion/window criteria, late-purchase policies, branch diversity or all legal construction orders. Under construction-first, chapter timing is logically unchanged because no skill is bought before completion; that candidate/policy combination was not rerun.

For02: the evidence does not justify adopting A solely to shorten this chapter. It buys a modest~23-second median improvement and leaves long world gaps; deciding for per-node utility needs the separate insertion/choice evidence, whose numeric total must first be corrected. If A is selected later, use a new price revision, preserve old V1/V2/V3 receipts and pending debits, and run release/migration acceptance. This commit changes no production prices and does not inflate construction costs to manufacture content.

## Verification, bounded output and limits

All40 current runs and16 candidate runs complete without the2000-spin cap; the maximum current main run is638 and order instance26 spins (cap100). A single current-profile dispatch parity check matches frozen seed1 economics after adding candidate CLI selection. Scoped checks also cover all64 baseline outcomes, fresh grant once/reload, independent seed1 first repair, actual capped/replay-safe offline income, exact9460 construction and610/500 skill spend, wallet conservation, order reward only on Claim and exact-claim dedup. No duplicate full old compatibility/Unity suite was run.

The main actual-Core batch took331.36 seconds of runner wall time; Candidate A137.06. These are computation costs, not simulated or measured player time. Generated source/bin/obj/Temp and temporary JSON stay under X:/tmp/jtn-progression-timing03; each JSON is capped at2MiB, with40/16 fixed cases and100/2000-spin limits. No new Editor, import, APK, ordinary save, service stop or global setting change occurred. The observed task directory was under5MiB before final receipt copies; global drive changes include concurrent work and are not attributed to this runner. Disposable closed output has manual7-day exact-directory retention; no automatic cleanup is claimed. Committed fixtures/receipts remain evidence.
