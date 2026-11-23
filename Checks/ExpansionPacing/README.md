# Expansion pacing utility

The opt-in `--decision-cadence` mode is a separate current-source research screen:
seed1, three declared policies, no resource/Arcade spending or payback forks.
It dispatches before the historical parser/driver. See the
[2026-11-02 report](../../docs/development/decision-cadence-20261102.md) for the
frozen source, exact command, limits, first-run receipts and one private UI
interval. The historical baseline/candidate descriptions below retain their
original scope and are not current-game acceptance.

This source-only tool executes the actual frozen production GameStore and strict
JsonStateCodec with private in-memory snapshots. It never uses ordinary saves,
Unity, an emulator or a filesystem durability simulation. `ACCEPTANCE.md` was
frozen before the baseline run; keep its first missed targets visible.

Baseline: three declared seeds × three policies,24 constructions each, no additional
order grind. Candidate adapter: the same construction/skill policies plus salvage
reinvestment, potato investment/exchange and a no-purchase resource-holding control.
All modules/equipment come from actual earned purchases; no free ownership fixture.
Source/data/assembly hashes, every purchase,10/25/50/60/100/120 checkpoints and the
first120 candidate spin deltas bind the result. A shop acquisition counts as useful
by50 only after an equipped/usable effect is observed. Cap effects require actual
overflow prevention; merely increasing a number is not measured production.

The candidate performs one paired40-spin purchase-vs-defer check for the first
reachable potato-yield purchase in each harvesting run. Both forks use production
commands and identical primary outcomes; these240 diagnostic spins count against
the same6,000-spin/180s total cap. Report marginal potato-cost recovery separately
from the full Parts+Potatoes opportunity cost. This is not a broad balance search.

## Reproduce a frozen packet

Run PowerShell in this worktree. Use a new output directory per immutable source
revision; never overwrite first execution/failure receipts. For a candidate, export
the parent's supplied immutable commit's `Unity/Assets/Scripts` tree under a fresh
`.work/pacing/frozen-<commit>` directory with `git archive` and `Expand-Archive`.
Do not compile against an actively edited producer/primary source tree.

```powershell
$env:TEMP = "$PWD/.work/pacing/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
New-Item -ItemType Directory -Force $env:TEMP | Out-Null
# Baseline: omit ExpansionCandidate and ProductionRoot overrides.
dotnet build Checks/ExpansionPacing/ExpansionPacing.csproj -c Release --artifacts-path .work/pacing/baseline/artifacts --nologo
dotnet .work/pacing/baseline/artifacts/bin/ExpansionPacing/release/ExpansionPacing.dll --source-root . --source-commit <baseline-commit> --output .work/pacing/baseline/first
# Candidate: <frozen-root> is the exported immutable production tree.
dotnet build Checks/ExpansionPacing/ExpansionPacing.csproj -c Release -p:ExpansionCandidate=true -p:ProductionRoot=<frozen-root> --artifacts-path .work/pacing/candidate/artifacts --nologo
dotnet .work/pacing/candidate/artifacts/bin/ExpansionPacing/release/ExpansionPacing.dll --source-root <frozen-root> --source-commit <candidate-commit> --output .work/pacing/candidate/first
./Checks/ExpansionPacing/Compare.ps1 -Baseline .work/pacing/baseline/first.json -Candidate .work/pacing/candidate/first.json -Output .work/pacing/candidate/comparison.json
```

JSON/CSV report spin counts and separate normal3.5s/fast1.5s arithmetic wait
projections. AUTO columns add a0.25s post-repair gap assumption; replace this label
only if actual implemented fast gap differs. These times exclude decisions, menus,
IO and human reaction. Runner wall time is only a cost/bound diagnostic, never game
pacing or runtime FPS. Changing presentation speed does not rerun economic RNG.

`Compare.ps1` derives tradeoff rows without another gameplay run. Retain the first
candidate; at most one root-authorized small NEW-catalog alternative may be compared
after a concrete target miss. Do not change historical construction/skill prices or
search many profiles/seeds. A missed target is a result, not authorization to change
the criteria.

The separate `--boost-priority` mode resolves the later root-authorized purchase-
priority question on the exact chosen alternative, without another nine-run screen:

```powershell
dotnet <candidate-dll> --boost-priority --source-root <same-alternative-root> --source-commit <same-report-identity> --prior-report <5334-spin-alternative-report.json> --output <new-supplement.json>
```

It checks the prior source hashes, executes only the same three seeds, saves all
Parts/Potatoes for actual bonus-unlock quotes, arms at first affordable public quote,
and stops at arming or30spins/seed. No other purchases or free funds are used. Its
at-most90 spins and wall time combine with the prior report under6000spins/180s;
the original nine MISS rows are never rewritten. This path diagnoses priority versus
resource gating; it does not establish a globally optimal investment policy.

The later workbench slice has its own `--workbench` mode and pre-execution policies
in `WorkbenchAcceptance.md`. Compile only the immutable next03 Core packet:

```powershell
dotnet build Checks/ExpansionPacing/ExpansionPacing.csproj -c Release -p:ExpansionCandidate=true -p:WorkbenchCandidate=true -p:ProductionRoot=<next-frozen-root> --artifacts-path .work/next-pacing/artifacts --nologo
dotnet .work/next-pacing/artifacts/bin/ExpansionPacing/release/ExpansionPacing.dll --workbench --source-root <next-frozen-root> --source-commit <next-commit> --output .work/next-pacing/first
```

It runs no existing nine-case/chapter mode. Natural earned benchI acquisition is
separate from synthetic no-bench/I/II marginal and focus controls. Actual paid setup
receipts precede disclosed40/40/30 headroom equalization. II's70-unit screen is
relative to owningI with the same balanced focus/bonus/boost trace, excluding I's
cost. Uncapped, one-resource-full and both-full focus cases retain their actual
outcomes. The amended1797-call worst case,60s/512KiB caps and all misses remain
explicit; conserved raw units are not a guarantee of conserved credited income.

Disposable closed diagnostics live only under this exact worktree's `.work/pacing`
and `.work/dotnet`, with a32MiB combined stream budget and seven-day cleanup
eligibility. Preserve source/fixture/failure/execution receipts; skip locked files
and reparse points in later cleanup. No cleanup/rotation service is installed.
Check free disk/output growth before and after each meaningful batch. Existing
`.work/probe` belongs to the previous completed task and remains preserved.
