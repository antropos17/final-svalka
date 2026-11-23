# Qualification findings

## Current r11 compatibility scope

The active source pin is bd8fbe8e140627f0bbbd0763b174c8245457705d, with45
linked source hashes. The source contract changed: current house improvement
offers540, while a retained V1 target/receipt still uses600. The laboratory now
accounts for these separately and checks that only explicit selection adoptsV2.
Current receipts belong in evidence/r11/; all sections below describe the accepted
r9 qualification, preserved without relabeling it as current-source evidence.

The corrected r11 build passed with0 errors/0 warnings. The single compatibility
campaign passed5 histories/545 commands/5589 checks in3.08s, with75 distinct state
categories:295 commits,118 expected refusals and132 unchanged operations. All five
declared price categories were reached. The fresh and legacy600 representative
traces each replayed109 commands with every recorded state hash matching.
The preserved price examples are550+8=558 while600 stays selected, explicit
558-540=18 reselection, and595+8-600=3 followed by an unchanged retained replay.
Both full-chapter fixtures still reach order claims and era acknowledgements.

No production defect was found in this bounded check. No game reduction ran.
The prior broad campaign, physical storage cases and pacing comparisons were not
repeated. Broader original categories are not the acceptance target of this small
source-contract compatibility profile; missing retained-request aging boundaries,
native durability and exhaustive correctness remain unproven.

Independent reviewer07 accepted frozen26e71c6 against acceptedfd3d201 without
blocking findings. The reviewer checked all45 source blobs, manifest and evidence
hashes, price-revision distinctions, unchanged generator and both109-command trace
identities. This was an independent source/evidence review; executable/build/Unity
checks were not rerun. See evidence/r11/review.json. It does not expand game acceptance.

The first r11 build stopped before any game execution with harness CS0136
(new local old collided with the historical-receipt variable). Renaming that new
variable to legacyPrice is the sole reason for the scoped retry. The failure is
retained in evidence/r11/first-build-failure.txt. No production mutation or reducer
campaign is authorized or required here.

## Historical r9 qualification

## Production defects

None found under the tested bounded histories. This is not proof of absence.
No production edits, source mutation, write-fault campaign or game reducer run.

## Harness defects and corrections

The first build failed with CS0117 at then-Program.cs:471: `DeliveryState` has no
`Empty` member. Replaced that harness call with the real zero-checkpoint constructor.
The first compiler failure was not a game execution failure. Final builds have
zero errors and warnings.

Post-campaign inspection found correlation in low LCG bits (initial campaign
only exercised skill counts0/1/6). Generation now uses upper24 bits. Inspection
also corrected an unexercised harness expectation: a new-sequence acknowledgement
of the current completion is allowed even if it was already acknowledged.
These are harness/coverage corrections, not production bugs. The final8-history
follow-up specifically checks the changed generator/ack behavior and reaches
skill counts0/1/2/3/4/6. The original32-history campaign was not rerun.

The initial32-history receipt predates these harness corrections and has no frozen
harness SHA recorded. Treat it as historical exploration, not final-version proof.
The43 production inputs are identical across every run. The final source hashes,
separate final8-history receipt and exact sample replay receipts are retained.

## Executed evidence

| Run | Histories/commands | Assertions | Outcome |
| --- | --- | --- | --- |
| Initial smoke | 1/120 | 1277 | Passed, then exact command replay passed |
| Initial bounded exploration | 32/4448 | 41678 | Passed in15.77s;192 state categories; all declared cases reached |
| Final targeted coverage follow-up | 8/1112 | 10697 | Passed in6.34s;121 state categories; no missing declared category |
| Final exact trace replay | fresh139 + full-skills139 commands | Per-command hashes and invariants | Both passed |
| Harness checks | All64 outcomes;4/36/24 partition; V1/V2 price/receipt checks; synthetic reducer | Focused | Passed |

The final run has584 committed commands,245 expected refusals and283 unchanged
queries/replays/reopens. This includes both ready order types and two claims,
old-claim/new-instance replay twice, five acquisition-spin category observations
across workshop/recycler/loft, stale quotes, retained purchases, completion notices,
blocked era commands and chapter completion with/without optional skills.
Counts and full command breakdowns in evidence/final/summary.json are authoritative.

The retained historical receipt fixture is reused read-only from
`Checks/EraMigration/R8/V2Pricing/delivery2-price5-owned.json`, SHA256
`340a309c274398a86cd47a550d1e79967b11196c64db7bce0010417bc792ca6d`.
Its delivery1 V1 price180 and delivery2 V2 price5 remain distinct from current60/20.
No old-save corpus or unrelated full suite was repeated.

## Design observations and coverage limits

No balance recommendation follows from synthetic funding or these histories.
The final sample does not reach five simultaneously owned skills, every possible
construction order, all stale-identity aging boundaries, overflow wallets or every
old-save variant. Catalog availability is used for state-aware generation, so its
completeness is not independently proved. Presentation flags are never fabricated.
The37-coin fixture opens an existing serialized envelope; actual Fresh-origin
creation and its initial welcome flow reuse qualified evidence rather than being
counted as generated coverage.
Offline-time event suppression reuses existing qualified coverage; no clock campaign
was introduced. Physical devices, Unity rendering, native files, lifecycle crashes,
concurrency and player enjoyment remain outside scope.

Resources stayed bounded on X:. No normal player save, shared source/configuration,
Editor or Library was opened. The first SDK invocation printed certificate setup
boilerplate; read-only inspection found only the already-existing localhost
development certificate dated2026-07-31. Later invocations explicitly suppress
certificate generation and telemetry. No certificate/store change was requested.

The bounded laboratory is complete. Production issues, if found by a later replay,
belong to the integration owner; this tool's ownership remains its own new path.
