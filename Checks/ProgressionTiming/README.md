# Bounded production-Core progression timing

## R12 bounded cadence comparison: no price adoption

Historical first-stage checkpoint:704e03d froze the original screening without adoption. Coordinator00 subsequently selected the exact already-executed B tradeoff; see `Checks/ConstructionPricing/evidence/cadence-r12/selection.md` and the second producer commit for active V3 source/390 compatibility assertions. The first-stage verdicts and artifacts below remain unchanged. No candidate was rerun.

The post-night producer at base d914da2 reuses the accepted76a379f Runtime route and the qualified540 records. The archived two experimental overlays are **not active production code**. Both candidates failed the predeclared local-gap criterion, so CurrentRevision staysV2 and all Core/Content files remain identical to the base. See `evidence/cadence-r12/decision.md` for actual intervals, the12 fixed cases, rejection scope and replay recipe. The two prior historical modes below remain tied to their documented source revisions.

## Two-case actual r11 equivalence checkpoint

The opt-in `-p:R11Equivalence=true` build uses actual r11 current-offer and pinned-target pricing APIs. It runs only seed1 with the two existing policies, without offline or order simulation, against the byte-exact house540 input from commit0ca71b4 (`evidence/tuning-r10/house-540.json` in that commit). The historical default mode remains available for its pinned old Core; no old campaign was repeated.

```powershell
dotnet run --project Checks/ProgressionTiming/ProgressionTiming.csproj -p:R11Equivalence=true -p:BaseIntermediateOutputPath=X:/tmp/jtn-economic-equivalence03-r11/.work/obj/ -p:BaseOutputPath=X:/tmp/jtn-economic-equivalence03-r11/.work/bin/ -p:UseSharedCompilation=false -- --qualified X:/tmp/jtn-economic-equivalence03-r11/.work/qualified-house-540.json --output X:/tmp/jtn-economic-equivalence03-r11/.work/new-equivalence.json
```

The input SHA256 is pinned; use a new output filename. The mode caps input at2MiB, output at256KiB, each case at2000 spins and asserts a60-second budget at case boundaries. Before an autonomous run, check the coordinator's hard deadline and acquire a heavy-process slot. Set per-process TEMP/TMP to the task's X: `.work/temp`. Disposable closed `.work` output has manual seven-day retention, skipping links/locked files; durable evidence is preserved, with no automatic cleanup claim.

At production source c583236 (pure source identical to bd8fbe8), construction-first matches chapter603/all-skills626/income9978/final wallet5; affordable-skills-first matches569/569/9998/25. Both have24 constructions9400 and six skills610; independently37+income−9400−610 equals the actual wallet. Every purchase/milestone/economic field matches the corresponding qualified experiment; journal writes also match1321/1207. The terminal seeded stream and delivery carry are independently checked from the qualified seed/spin count/loft acquisition point, using unchanged actual Draw code:392404096/carry2 and197992699/carry1. This checks intended RNG identity without translating the economy into another model.

Expected differences are explicit construction price revision/frozen before-after metadata and computational cost/managed heap. SerializedCharacters is the aggregate number of UTF16 characters passed to the in-memory storage, not disk bytes or retained log size:6922448→7106688 and13845923→14014203. Execution took about11.05 seconds of computation, with no player-time/performance or native claim. The first and only two-case invocation passed; its single CS0414 warning says historical Profile is unused in this opt-in mode. That warning is preserved; there was no test retry or extra campaign.

The mixed old600-selected550+8→558 then explicit540 reselection→18 boundary already exists in the307-assertion producer suite and was qualified in r11 integration. It is reused, not duplicated. Evidence and source/hash identity are under `evidence/equivalence-r11/`. No production source, ordinary save, Editor, import, APK or price was changed; no wider acceptance follows from two cases.

## Historical r9 runner

This runner links the actual r9 Core/Content/Upgrades/Effects/Orders/Guidance C# sources. It uses public GameSession commands, the real two-write GameStore and strict JSON codec, with an in-memory storage implementation. It never uses a Unity Editor, normal save path or the Python economy model. Source pin: c086395; workflow/report baseline3079397. No production source/data is changed.

## Reproduce current V3

Run from this checkout root, using a new output filename to preserve receipts:

```powershell
$env:TEMP='X:/tmp/jtn-progression-timing03/temp'; $env:TMP=$env:TEMP
dotnet run --project Checks/ProgressionTiming/ProgressionTiming.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-progression-timing03/obj/ -p:BaseOutputPath=X:/tmp/jtn-progression-timing03/bin/ -p:UseSharedCompilation=false -- --samples 16 --offline-samples 4 --order-repeats 3 --output X:/tmp/jtn-progression-timing03/new-production-r9.json
./Checks/ProgressionTiming/Summarize.ps1 -InputPath X:/tmp/jtn-progression-timing03/new-production-r9.json -OutputPath X:/tmp/jtn-progression-timing03/new-analysis-r9.json
```

The fixed16 seeds are literal in Program.cs and copied into each output. The40 main runs are16 seeds ×2 policies plus4 paired early-return seeds ×2 policies. A2000-spin run cap,100-spin order cap,600-second batch cap and2MiB JSON cap bound execution/output. The main observed maximum was638 spins; no run stalled. Each run finishes24 construction purchases and all six skills, then forks its exact endpoint for each order type. Main uses three sequential instances of each type (240 instances total,192 in the continuous subset). Do not treat within-seed instances as independent samples.

Construction-first selects the cheapest available construction, price then ordinal stable-ID tie-break, and defers all skills until chapter end. Affordable-skills-first buys the cheapest currently affordable unlocked skill before each next spin; otherwise it continues the existing selected construction or selects the same cheapest construction. It does not wait without selecting solely to save for an unaffordable skill. Both enter/ack the era as soon as eligible. This is a two-policy sample, not an optimizer over all legal orders/insertion positions.

First repair is manual. During the chapter the projection uses AUTO where available (3.5+.25 seconds); after final completion AUTO is disabled, so postchapter skill gathering/order spins use manual3.5. Decisions/confirmation/restart/reading latency is zero in the basic projection. A separately labelled2-second-per-explicit-decision sensitivity is an assumption, not observation. Orders fork from the all-skill endpoint with no further purchases or offline returns; clock is held constant while counts are converted to manual-cycle projections. Runner wall time is C# validation/computation time, never gameplay time.

The40-run source/result checkpoint is1785a57. The final runner adds explicit profile selection; its current-V3 seed1 parity check matches the original spin count, wallet, spending, income and gap. No40-run repeat was needed for that CLI-only extension. The initial four-run pilot preceded correction of the postchapter timing interpretation and is excluded from final timing statistics.

The final scoped attributes preserve generated evidence bytes, including original CRLF where emitted. The first checkpoint's default Git text handling normalized some JSON line endings; the final byte-preservation correction changes no parsed result values. Review hashes against the final checkout/receipt; whitespace-insensitive comparison shows the original current-run data are unchanged.

## Reproduce the separate proposed Candidate A

```powershell
./Checks/ProgressionTiming/PrepareCandidateA.ps1 -OutputRoot X:/tmp/jtn-progression-timing03/new-candidate-a-source
dotnet run --project Checks/ProgressionTiming/ProgressionTiming.csproj -p:ProductionRoot=X:/tmp/jtn-progression-timing03/new-candidate-a-source -p:BaseIntermediateOutputPath=X:/tmp/jtn-progression-timing03/candidate-obj/ -p:BaseOutputPath=X:/tmp/jtn-progression-timing03/candidate-bin/ -p:UseSharedCompilation=false -- --profile candidate-a --policy affordable-skills-first --samples 16 --offline-samples 0 --order-repeats 1 --output X:/tmp/jtn-progression-timing03/new-candidate-a.json
```

PrepareCandidateA verifies the exact three proposed deltas, exports c086395's pure source to a new X: directory and changes only SkillPricing.cs/SkillTree.cs there: a proposal-only V4 table/resolver, preserving V1/V2/V3 prices. Core/effects/constructors/codec are unchanged. These are real C# session runs against that isolated proposal, not a live configuration change or release/migration qualification. Candidate B is excluded. The input manifest records all original Git blobs and experimental source SHA256.

The explicit Candidate A prices sum to500, not the brief's claimed540:120+120+160+40+40+20. The first guarded run refused the inconsistent540 expectation before simulating; only the expected total was corrected, with the proposed prices untouched. No Python lab rerun occurred. No old receipt corpus/release acceptance for hypothetical V4 is claimed.

## Evidence and limits

See docs/development/production-balance-r9.md. evidence/production-inputs.json binds the initial runner/core/result hashes; evidence/final-qualification.json binds the candidate and final dispatch/checks. The owner-measured Editor timing receipt is copied with its original byte hash and exact commit. Quantiles use nearest rank; with n16, p95 is the sample maximum, not a population-tail guarantee. Core construction commits are proxies for visible world changes, not observed rendering times.

Only Checks/ProgressionTiming, the requested report and scoped evidence are owned. Preserve other writers, earlier fixtures, user progress and research. Temporary generated source/obj/bin/Temp stay under X:/tmp/jtn-progression-timing03. Disposable closed output has manual7-day retention in that exact directory, skipping links/locked files; no automatic rotation/cleanup is installed. Committed receipts are durable evidence. No install, APK, heavy Unity import or global setting change belongs to this runner.
