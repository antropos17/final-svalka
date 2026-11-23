# B/V3 skill payoff decision

Decision: retain `skill.prices.v3` unchanged. No concrete weak node was established in this bounded assessment, so no candidate profile or V4 registry change is justified. This is a source-only decision; B connected Unity qualification remains a separate prerequisite for primary integration.

## Evidence and interpretation

The runner reuses all six captured B paths from `../cadence-r12/wider.json`, SHA256 `C0D1A626FC961BC7B68A4264391E6602075B59A5D9B39146B92E0C4E494B084E`. It does not rerun a natural chapter. `inputs.json` preserves the integrator's source-only input. `source-freeze.json` pins all 49 compiled source/project files at detached source base `6b0b35e` plus the scoped runner changes.

For each purchase, independent xorshift arithmetic counts qualifying units strictly after acquisition and before the captured chapter end. Delivery carry starts after the first loft purchase and persists across era entry. The 36 rows include observed affordable wallets, the next observed construction's cash shortfall, and branch cost from unowned. Tier-two net values below treat the prerequisite as already owned; branch totals are U280/R250/D80.

All three affordable-skills-first paths recover every node's own price before chapter end:

| Node | Own price / qualifying unit | Remaining qualifying units | Gross minus own price |
| --- | --- | --- | --- |
| Universal 1 | 120 / foreground spin | 511..534 | +391..414 |
| Universal 2 | 160 / foreground spin | 190..199 | +30..39 |
| Recycling 1 | 160 / trash appearance | 329..342 | +169..182 |
| Recycling 2 | 90 / trash appearance | 152..165 | +62..75 |
| Delivery 1 | 60 / completed delivery | 98..117 | +38..57 |
| Delivery 2 | 20 / completed delivery | 34..47 | +14..27 |

Construction-first purchases occur after chapter completion. Their zero prechapter horizon is a property of that policy and does not establish a weak skill. Positive conditional net on captured paths also does not prove a node's causal effect on chapter speed.

Purchase opportunity has a real cash cost. For seed1, U1 is affordable at spin33 with125 coins while the140 cart still lacks15; purchasing U1 leaves5 and increases that cash gap to135. R1 at spin84 has161 coins while190 power lacks29; buying R1 leaves1 and a189 gap. Observed cart completion changes35->51 and power76->100 between whole policies; these delays cannot be assigned to one skill. Across the three seeds the whole-policy cart shift is13..16 spins and power23..24.

Two independent expected-outcome examples execute actual Core through private in-memory storage and the strict codec. A single artificial funded chapter fixture spends9310 from10000, leaving690. Its skilled companion buys all six for610, leaving80. Each order pair starts separately from seed1/carry0.

| Order | Spins | Actual trash | Deliveries / carry | Extra skill income | Final wallet plain / skilled |
| --- | --- | --- | --- | --- | --- |
| Sort salvage | 16 | 13 | 2 / 2 | 62 | 1182 / 634 |
| Neighborhood supply | 18 | 15 | 3 / 1 | 72 | 1290 / 752 |

The first order visibly caps progress at12 but its final spin credits the13th actual trash. Per-node ledgers account for all qualifying units. Fixed claims remain120/180; skills do not change RNG, delivery cadence, order progress or completion spin. Exact claim replay adds no write/payment. One short order does not repay the610 bundle (net -548/-538); this short horizon does not demonstrate that an individual optional node is weak.

## Verification and bounds

First attempt: PASS, exit0, process6.58s; runner1.275s. No retry. Six captured paths,36 purchase windows, two paired orders,68 actual spins total, no natural chapter reruns. First stdout/stderr/exit and before/after resource readings are preserved. Output JSON is below512KiB; new scratch growth was1,288,527 bytes and evidence about53KiB, below32MiB. C free after22,464,233,472 bytes; available RAM17,080,888KiB.

Run from the isolated source root with X-based per-process TEMP/TMP and output paths:

```powershell
dotnet run --project Checks/ProgressionTiming/ProgressionTiming.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-gameplay-lifecycle05/.work/skill-payoff-r12/obj/ -p:BaseOutputPath=X:/tmp/jtn-gameplay-lifecycle05/.work/skill-payoff-r12/bin/ -- --skill-payoff --captured Checks/ProgressionTiming/evidence/cadence-r12/wider.json --inputs Checks/ProgressionTiming/evidence/skill-payoff-r12/inputs.json --output <new-output-path>
```

The runner rejects an existing output and verifies frozen inputs before and after execution. It caps each actual session at32 spins and checks a60-second arithmetic/Core budget; the first process also had an external60-second timeout. No Unity, native device, human-play, offline timing or general balance acceptance is claimed. The original B cadence capture's stricter failed criterion is preserved and is not relabeled by this payoff check.
