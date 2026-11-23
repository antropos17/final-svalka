# Two-era content catalog handoff — stream 04

Date: 2026-10-29 (Asia/Baku). Source baseline: f5bfc34/r7. Worktree/branch:
`X:/tmp/jtn-content-wave04`, `codex/content-catalog-wave04`.
The previous d76abf6 commit is preserved; a clean merge brought r7 into this worktree without
resetting or cleaning anything. No Library or Editor was created. Deliver only the final owned-path
commit, not the baseline merge commit. Required02 Contracts release:
`6fc9ee2266da35091dd64e843a8158593a01fdd9`.

## Delivered, and integration boundary

`ChapterData.TwoEras()` supplies an ACTIVE chapter: six permanent families, eighteen states per era,
36 globally unique states, twelve paid upgrades per era, explicit Workshop Yard entry, finite end.
Every price/benefit is resolved; all24 upgrades are enabled. Existing CurrentPrototype and the old
ProposedFirstEra sample remain compatible historical APIs; integration should select TwoEras.
The active chapter is not another disabled proposal or an assertion of connected runtime acceptance.

Production code is only in Content. It references Contracts only, never Core/Effects/Upgrades or
Unity/IO/clock/RNG. Existing shared runtime/codec/scenes/packages/STATUS and primary Editor were
not edited. Python toolkit, sound drafts and other streams were untouched. Two sequential child
agents extracted source requirements and independently reviewed the final implementation.

Original tasks26/28 and the six identities in25/27 were read from the original HTML JSON at
`docs/reference/junk-to-neon-plan-astra (2).html:1055`. Expansion prices/dependency details are
provisional design choices, not values supplied by that plan. Current r7 IDs/prices/effects stay exact.
The latest two-era-skill-contract supersedes the previous wave's disabled-expansion limit.

## Data and semantics

| Family | Junkyard0->1 | Junkyard1->2 | Workshop Yard0->1 | Workshop Yard1->2 |
| --- | ---: | ---: | ---: | ---: |
| House | 40 | 600 | 420 | 680 |
| Workshop | 120 | 260 | 380 | 620 |
| Recycler | 180 | 320 | 400 | 640 |
| Aviary / pigeon-loft | 240 | 360 | 440 | 660 |
| Cart | 160 | 300 | 360 | 580 |
| Power source | 200 | 340 | 460 | 700 |

Total paid cost: **9460 coins**, excluding optional skills. Individually chosen bounded prices;
no blind exponential multiplier or natural-pacing/session-length acceptance is claimed.
All Junkyard build branches open after house.repair; each improvement requires its own build.
House.improve keeps its prior milestone-only meaning. Workshop Yard each family's upgrade2
requires its own upgrade1. Both per-era graphs permit alternative orders without traps.

Baseline benefits remain workshop+1/later spin, recycler+1/trash symbol, loft+3/three pigeon
appearances with the same carry0..2. Repair unlocks AUTO. No old owned project gains a new bonus.
New improvements, Cart/Power and all Workshop Yard upgrades add exactly+1 per assigned symbol:
house/power-source=potato0, workshop/cart=box1, recycler=trash2, pigeon-loft=pigeon3.
Every new source is `<upgradeId>.symbol-coins` with Kind=SymbolCoins, Amount1 and explicit SymbolId.
No symbol odds/base23/8/3/cadence/clock changes. Acquisition effects start on subsequent spins.
Final nineteen symbol sources aggregate to potato6, box7, trash3, pigeon3 coins per symbol.

Era/stage/project identities and FamilyStages/FamilyStage/SymbolCoinSource/ChapterSymbolModifiers
are the exact shared Contracts types, not local copies. Content-owned ChapterContentIds contains
only data ID `chapter.two-eras.v1`, pinned rule revision `chapter.rules.v1` and effect-source registry.
The original five r7 project aliases, all first-era stage IDs and the same six object IDs persist.
Era2 uses shared `<family>.workshop-yard.stage.0/1/2` and `.upgrade-1/-2` IDs.

All six Junkyard local2 are required for explicit `era.workshop-yard.unlock`, which is a separate
zero-cost action. It retains all twelve first-era ownership IDs and advances absolute2->3 for all
six objects; local0 is an era appearance, not lost lifetime progress. It grants no coins or initial37.
Final chapter completion requires all six absolute5 and no optional skill. There is no third era.
AUTO behavior, selected-target retention and presentation/transition acknowledgement belong to02.

Only house Junkyard stages use existing Resources keys Editable/HouseStage0/1/2. All other art
explicitly uses TextAndBlock fallback. A missing keyed resource remains an error even with fallback.
The integration owner supplies an actually observed resource registry; no invented art key is used.

## Exact public interface

Construct `new ChapterRules(ChapterData.TwoEras(), observedAssetKeys)`; all definition errors throw.
`ChapterValidator.Validate` also exposes immutable structured diagnostics. It composes existing
catalog validation with exact six-family/two-era/registered-stage/project checks, fully enabled
priced benefits, global ID/source uniqueness and preservation of literal r7 meanings.
Symbol source IDs/kind/symbol/amount are tied to their registered owning upgrade.

- `ChapterProgress(eraId, completedUpgradeIds)` is an immutable QUERY snapshot, bounded24; it is
  not a new save schema. `Fresh()` is Junkyard/no purchases and grants nothing.
- `ValidateProgress`, `AvailableUpgrades`, `RemainingEraRequirements`, `CanUnlockWorkshopYard`,
  `IsChapterComplete`, `TryGetUpgrade` validate known ownership/prerequisites/era. Invalid/duplicate
  IDs, future-era ownership and Workshop Yard without completed Junkyard are rejected.
- `Stages(progress)` returns shared FamilyStages. `FromStages(FamilyStages)` expands the exact
  implied prior/current purchased IDs and validates prerequisites. All1216 reachable snapshots
  roundtrip. `FamilyStage(progress, family)` returns a metadata view containing the shared
  FamilyStage plus StageDefinition and absolute level; it does not invent a second stage DTO.
- `ProjectR7(houseStage, workshopBuilt, recyclerBuilt, pigeonLoftBuilt, selectedStage=0,
  legacyContentVersion=1)` returns ChapterLegacyProjection{Progress,SelectedUpgradeId}.
  All17 valid r7 combinations and targets1..5 retain exact meaning. New Cart/Power are unowned,
  existing one-stage buildings remain local1. Wallet/RNG/sequence/carry/skills/offline cursors and
  old pending receipts are not inputs and cannot be overwritten by this pure projection.
- `ProposePurchase(before, upgradeId, coins)` checks known/unowned/current/available/affordable
  target and returns immutable ChapterProposal{CatalogId,RuleRevision,OperationId,Before,After,
  CoinsBefore,CoinsAfter,Price}. It buys exactly one upgrade in the proposal and mutates nothing.
- `ProposeUnlockWorkshopYard(before, coins)` produces the same proposal type, price0/samewallet,
  preserving all owned IDs. It rejects early entry and entry from an already committed Yard state.
- `OwnedBenefits(progress)` returns source descriptors, not CoinEffects credits (up to23 including
  AUTO). Do not put that list directly into the at-most16 receipt array.
- `SymbolModifiers(progress)` returns shared ChapterSymbolModifiers, max19 sorted registered
  sources aggregated to four rates. `ValidateSymbolModifiers(preCommandProgress, ruleRevision,
  frozen)` rejects unknown revision, missing/unowned/unknown IDs, wrong symbols/rates and compares
  the full canonical source list. Supply FROZEN pre-command ownership under pinned compatible
  rules, never live post-purchase ownership or current tuning during old pending recovery.

There is no commit/apply/retry method here.02 binds proposals to full state/revision/global sequence,
freezes and strictly validates the complete operation before saving, and is the sole mutation owner.
Cancelling the offer/discarding a proposal changes nothing. After committed era entry, presentation
skip/reload/ack must not undo the era or issue the transition again; marker/ack persistence is02's
responsibility. No eager migration write or new bonus may be added to literal r7 format1 recovery.
All new source registration/rates must be validated against this pinned rule revision.

## Executable fixtures and checks

**19 existing Content groups +10 Chapter groups passed; 30 unchanged r7 Core groups passed.**
Chapter tests exhaust all1216 reachable snapshots and4781 edges, with one terminal chapter state,
plus both-era multiple orders, all36 stage visits, every legacy shape/target, zero-reward once-only
entry relative to committed state, immutable proposals, insufficient/locked/duplicate purchases,
malformed graphs/assets/benefits and forged source validation. Optional skills are excluded.
Independent read-only review found no concrete correctness issue in the final revision.

`ChapterRegression.Traverse(uint seed)` is an executable funded private domain fixture (no save,
spin, Effects evaluation or pacing claim). Sorted eligible choices use its local deterministic LCG;
three seeds17/9047/20261029 take different valid orders. Expected result for seed20261029:

```text
initial coins=10000; purchases=24; explicit era entries=1; actions=25
paid coins=9460; remaining coins=540; era=workshop-yard
owned upgrades=24; unique visited stage IDs=36; all six absolute levels=5
chapter complete=true; optional skills required=0
symbol sources=19; rates potato/box/trash/pigeon=6/7/3/3
```

The standalone Program prints the full25-action trace. Its entry occurs after action12 and leaves
first-era ownership intact. This fixture deliberately funds the whole chapter; integrated seeded
spin/purchase/reload/presentation tests remain02/03's acceptance work.

Production Content plus exact02 Contracts compiled with installed Unity6000.3.21f1 Roslyn,
its NetStandard2.1 reference and C#9 `/nostdlib+ /warnaserror+`: exit0/no diagnostics.
This is Unity-profile source compilation, not an Editor import or EditMode runner verdict.
New EditMode method `TwoEraChapterProgression` runs the ten groups after integration.

Commands from this worktree (TEMP/TMP point to its X: .work/temp):

```powershell
dotnet build Checks/ContentChecks/ContentChecks.csproj -p:ContractsDirectory=X:/PROJECTS/FINAL-SVALKA/Unity/Assets/Scripts/Contracts -p:BaseIntermediateOutputPath=X:/tmp/jtn-content-wave04/.work/chapter-obj/ -p:OutputPath=X:/tmp/jtn-content-wave04/.work/chapter-bin/
dotnet .work/chapter-bin/ContentChecks.dll
```

Core30 used the unchanged Checks/CoreChecks.csproj with the two new exact Contracts files linked
via task-local `.work/chapter-contracts.targets` and `CustomAfterMicrosoftCommonTargets`, plus
X: chapter-core-obj/bin. No Core harness or shared source edit was made. After the02 dependency
release is applied normally, no extra Contracts link is needed. In this source-only checkout,
use the explicit primary Contracts override until dependency files are integrated; do not open it
in Unity without them. Shared sources were verified unchanged against release6fc9ee2.

First actual failure is preserved in .work/logs/chapter-checks-first.txt: the independent draft
price-total oracle said9520, but the explicit table sums9460. Corrected the arithmetic oracle and
expected residual540; prices were not changed to fit the bad expectation. All subsequent checks
passed. The first patch attempt to replace the temporary local identity registry was rejected
before application; the final code delegates all shared identities/types to Contracts.

## Resource and next integration action

Initial free C:9.98GiB/X:89.05GiB; final sample C:9.95GiB/X:89.04GiB (machine-wide concurrent
activity included). New chapter bin/obj ~678KiB, chapter Core bin/obj ~887KiB; all task logs,
including earlier retained receipts, total18431bytes, temp8049bytes. Heavy output stays on X:.
No Library/import/SDK duplicate/background service/APK was created. Retain receipts; disposable
closed task logs/temp have seven-day retention and8MiB budget before reuse. This is a manual
bounded reuse policy, not installed automatic rotation.

Next action belongs to02: integrate this owned-path commit after shared Contracts6fc9ee2; use
shared FamilyStages for the modern aggregate, map legacy r7 without writes, feed validated
pre-command SymbolModifiers into06 format2, and bind these pure proposals into the sole Core
transaction/save protocol. Then run the supplied EditMode test, connected traversal/migration/
transition cancellation/acknowledgement/recovery checks and one combined APK. No connected
runtime, skill tree, save migration, Editor/UI or packaging acceptance is claimed by this stream.
