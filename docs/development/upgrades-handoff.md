# Soft-currency upgrades handoff

## Delivery Tier2 follow-up (2026-10-29)

Only Delivery2 is retuned **5->20 for new quotes**, under new `SkillPricing.RevisionV3="skill.prices.v3"` / CurrentRevision. V1 and V2 remain immutable, including V2 Delivery2=5; `Find` explicitly resolves all three profiles. Other prices/effects/prerequisites and codec shapes remain unchanged. New skill total610. Existing request constructor still defaults to legacy V1. Fresh V2 quotes fail stale; applied V2 replay and frozen RestorePurchase retain5. Old V2 token suffixes and literal ownership bytes are preserved; mixed V2/V3 uses the existing Version3 receipt schema. No refund/recharge or rewrite occurs.

02 acknowledged20/V3 and owns activation. `research/delivery2-retune-05/HANDOFF.md` has the small before/after proof: all six policy/parent cases have a useful pre-end window (worst best -0.448995 spin) and a later reachable saving window (smallest purchase cost +0.544496 spin). These are modest sub-one-spin margins, not a meaningful-strategy claim. Three named policies only; no MC/all-orders claim. Model sources are hash-pinned against the previous study; old research is unchanged. The coarse sweep took9.882s and neighboring boundary checks4.963s.

Nine affected groups passed: six updated pricing cases plus three new delivery-retune cases, using seven literal snapshots emitted by unmodified bff8b5 before editing its5-coin table. Earlier22-group/8081-assertion evidence below is retained as dated V2 evidence, not a new full V3 run. Run `Checks/SkillsChecks` with `-- --pricing-only` for the focused cases. No Core/runtime, ordinary saves, Unity/Editor or APK work was performed here. Primary connected price/replay acceptance remains02-owned.

## Price-only revision (2026-10-29, r8 baseline)

This increment starts at `042fb527037cf1aea8412a5c4b7dcce303e7d529` / r8 and follows the coordinator's `journey-pricing-contract.md` (`2764c29`). Only four skill prices change. All six node identities, +1 effects, prerequisites, level caps, construction prices, four equal symbols, base23/8/3 and delivery cadence remain unchanged. Existing offline workshop1/full minute/cap480 is unchanged and excluded from the foreground price model. Core/runtime activation belongs to 02; no Editor or APK was run by this stream.

| Canonical node | `skill.prices.v1` (retained) | `skill.prices.v2` (new quotes) |
| --- | ---: | ---: |
| slot.coin-boost | 120 | 120 |
| skill.universal.2 | 360 | 160 |
| skill.recycling.1 | 160 | 160 |
| skill.recycling.2 | 480 | 90 |
| skill.delivery.1 | 180 | 60 |
| skill.delivery.2 | 540 | 5 |

The selected table totals595 instead of1840. The tiny delivery Tier2 price reflects its late finite-chapter unlock and +0.25 long-run expected coins/spin, not a larger effect. It is provisional economy tuning, not proof of fun or natural pacing. Saving remains faster at documented late insertion points. Quantitative method, independent payoff checks, two numeric variants and scoped outputs are in `research/skill-price-rebalance-05/`; the external Opus lab was read only and not overwritten. The bounded exact-mean phase sweep uses floating-point propagation of rational 64-outcome probabilities, not Monte Carlo and not Fraction-exact final answers. Scope is three named fixed construction policies with stated Tier1 conditioning, not all legal orders or the earlier120 samples.

### Pricing API and strict compatibility

- `SkillPricing.LegacyRevision` / `RevisionV1` = `skill.prices.v1`; `CurrentRevision` / `RevisionV2` = `skill.prices.v2`. `IsKnown(string revision)` and `Price(string nodeId, string revision)` expose the closed immutable tables; unknown nodes/revisions reject. The separate effect revision stays `chapter.rules.v1`.
- `SkillTree.Find(nodeId, pricingRevision = SkillPricing.CurrentRevision)` and `Quote(..., context, ownership, pricingRevision = SkillPricing.CurrentRevision)` resolve preview prices under that revision. `SkillNode.PricingRevision`, `SkillPurchaseRequest.PricingRevision`, `SkillReceipt.PricingRevision` and `SkillPurchaseProposal.PricingRevision` identify it explicitly. `NextUnlocks.Node` uses the same table as the quote.
- The optional FINAL `SkillPurchaseRequest` constructor argument defaults to **legacy**, preserving old constructor/frozen-descriptor meaning. Normal Quote explicitly supplies current. `Propose` checks exact already-applied replay first (no proposal or replacement wallet), then returns Stale for an unapplied legacy quote. It never silently converts that request to a new price. Changed revision conflicts even when the numeric price happens to be unchanged.
- `SkillTree.RestorePurchase(long currentSequence, long wallet, SkillContext context, SkillOwnership before, SkillPurchaseRequest request, long frozenPrice)` is a pure frozen-validation adapter returning an immutable proposal. It checks the request's pinned revision/price, full before-token/context/selection, sequence, operation identity, funds and prerequisites. It accepts historical prices for recovery; it is not the foreground purchase entrypoint and performs no apply/write. Core must validate the enclosing operation format/rule and use its sole frozen-result journal.
- Old-only ownership stays Version2 with exact old JSON shape and tokens. Mixed/current ownership is Version3 with **required `priceRevision` on every receipt**, including retained v1 entries. V2 rejects unexpected revision fields; V3 rejects missing, duplicate, unknown or inconsistent revisions. Each receipt validates its frozen debit against its own table. Historical tokens remain byte-identical; only current receipt identity appends its pricing revision. Existing expected-before tokens are preserved, never recomputed under a new history format. The6-receipt/2048-string/24576-payload bounds remain.

### Required Core integration and evidence

02 must persist and compare request PricingRevision, decode a missing historical request field as v1, resolve frozen prices with `SkillPricing.Price(request.NodeId, request.PricingRevision)` and retain old pending recovery under its original price. Modern legacy fresh requests must fail as stale; historical exact replay and literal r7 pending120 remain valid. Do not retag old requests to current, refund ownership, grant replacement income, alter wallet/selection or eagerly rewrite saves. Canonical Universal1 still costs120, satisfying the existing `wave.Slot` projection. Continue counting the total universal scalar exactly once.

Before editing the module, unmodified r8 code generated seven literal ownership snapshots from empty through all six purchases. `R8SkillFixtures.cs` retains these bytes; tests restore each pending purchase with its original price, compare exact tokens/encoded bytes, preserve1840 total spent, and replay after unrelated live wallet/sequence changes. New tests cover current595 total, mixed r7/current histories, stale pricing, wrong historical debit, strict schema/revision rejection and maximum-size receipts/long wallets. First module run passed **8 legacy +8 tree +6 pricing groups**; raw output is `X:/tmp/jtn-price05/module/first-run.txt`. New thin Unity wrapper is supplied but unexecuted.

Sequential quantitative review completed the25-trial search in21.563s and four independent arithmetic/result groups. Its first10-coin-minimum Delivery2 attempt failed the chosen one-spin cushion (not strict profitability); `research/skill-price-rebalance-05/first-failure.log` preserves it. Final Delivery2 grid5 resolved that declared search criterion. Worst-policy best deltas for D1/U2/R2/D2 are -1.305441/-1.362251/-1.389311/-1.187421 expected spins. Saving is faster at late pre-chapter insertion points for the other five node types; Delivery2=5 remains beneficial at every tested pre-chapter position. No claim of a saving window for that particular node is made.

The subsequent independent compatibility review found no blockers: **22 supplied groups plus8,081 assertions across all64 pricing-revision combinations** passed, including literal old snapshots, mixed histories, frozen v1/v2 prices, stale quotes, revision conflicts and replay after later purchases. Its largest tested payload/token were4527/991 characters. Evidence: `X:/tmp/jtn-price05/compat-review/results.txt` and `payoff-results.txt`; the parent also ran the four-group payoff checker after adding explicit stationary-payback assertions. No domain source changed after this review. No Core, runtime, scene, package, user save or global setting was changed. Primary persistence/UI and connected/native acceptance remain02-owned. Disk was checked before/after; exact X task outputs were about1.8MiB, with manual closed-output retention7days/8MiB and verification receipts preserved. Existing historical sections below remain dated evidence, not current price instructions.

## Two-era six-node addition (2026-10-29)

This addition targets `f5bfc34` / r7 and the coordinator's `docs/development/two-era-skill-contract.md` v2. The original sections below describe the preserved first-wave implementation. `CoinBoostShop`, `UpgradeOwnership`, their codec and all original regression source are unchanged. New source is `SkillTree.cs`, `SkillOwnershipCodec.cs`, two new regression files and `Checks/SkillsChecks/`; the original UpgradesChecks project also links the new contract dependency. The domain still references only Contracts, with no engine, Core, Content, Effects, I/O, RNG or clock dependency. No live activation or chapter-completion implementation is included here.

Requires coordinator shared release `6fc9ee2266da35091dd64e843a8158593a01fdd9`. Its released `ChapterContracts.cs` SHA256 is `6B15AE9C725BEEE596EC8886DC76D271F43551D3CC399AA9D07B3E863A64BD56`, as recorded in `chapter-contract-v2.json`. Initial implementation used its preliminary source hash `ECE7E80E97F643AE7CD0CC4FA7E5C0BB50F2C09D5E9D701C0FAD9ED6ACA30F5C`; independent review verifies the released dependency. Existing `GameplayContracts.cs` remains SHA256 `AB26AFFF86CBDCEE9AE53DE5A9823C04127B649A698EFE9DBE66557A73B26E3A`. The new check project links exact sources; no copy is vendored into this pinned checkout. The coordinator must integrate its shared contract before compiling these files in Unity. This module does not need `ChapterSymbolModifiers.cs` because its effect boundary is three primitive scalars.

| Branch | Tier 1 / price | Tier 2 / price | Each owned node |
| --- | --- | --- | --- |
| Universal | `slot.coin-boost` /120 | `skill.universal.2` /360 | +1 per foreground spin |
| Recycling | `skill.recycling.1` /160 | `skill.recycling.2` /480 | +1 per qualifying trash symbol |
| Delivery | `skill.delivery.1` /180 | `skill.delivery.2` /540 | +1 per completed delivery |

All six nodes have cap1 and provisional tuning. IDs come from shared `SkillIds`. Tier1 requires house/recycler/pigeon-loft respectively at absolute>=1. Tier2 requires its own Tier1, `workshop-yard`, and that family at absolute>=4. Each immutable `SkillNode` exposes `RequiredEraId`, `RequiredEraUnlockId`, `RequiredFamilyId`, `RequiredProjectId`, `RequiredProjectLevel`, `RequiredSkillId`, `Price`, `CurrencyId`, `LevelCap`, `AdditiveEffect`, `Branch` and `Tier`. `RequiredProjectLevel` is an absolute level. Era entry absolute2->3 retains Tier1 effects while Tier2 remains locked until absolute4.

### New public API

Namespace remains `JunkToNeon.Upgrades`.

- `SkillContext(FamilyStages stages, string selectedConstruction = null)` uses the exact immutable shared six-family snapshot. Selection must be null or `ChapterIds.IsProject(id, stages.EraId)`. The context exposes `Stages`, `SelectedConstruction`, `EraId` and absolute-stage convenience properties. No lookalike stage DTO is defined.
- `SkillTree.Nodes` and `SkillTree.Find(string nodeId)` expose the immutable six-node catalog. Unknown `Find` returns null; unknown quote IDs throw.
- `SkillTree.Quote(string nodeId, string operationId, long nextSequence, long wallet, SkillContext context, SkillOwnership ownership)` returns `SkillQuote`: Node, Status, Owned, CurrentEffects, NextEffects, Unlocks, NextUnlocks, Request. Status reuses Locked/Available/InsufficientFunds/Maxed. NextEffects is hypothetical after acquiring the requested node even when currently locked; a maxed node does not add again. Unlocks lists dependent IDs; NextUnlocks also reports each dependent node's remaining era/building prerequisites via `PrerequisitesMetAfterPurchase`. Neither property authorizes purchase or construction.
- `SkillPurchaseRequest(string nodeId, string operationId, long sequence, long expectedWallet, string expectedOwnership, SkillContext context)` binds a stable operation ID, exact next global sequence, wallet, complete ownership identity and complete context including selection. `ExpectedOwnership` is `SkillOwnership.Token`, a deterministic bounded literal identity of all immutable receipts, including their historical wallet and context; it also binds revision/receipt count. The token excludes recursively nested earlier tokens.
- `SkillTree.Propose(long currentSequence, long wallet, SkillContext context, SkillOwnership ownership, SkillPurchaseRequest request)` returns `SkillPurchaseResult`: Status and optional Proposal. Fresh proposals require next global sequence, exact quote context/wallet/ownership, affordability and prerequisites. Other commands may interleave arbitrarily. Applied exact request returns Replay and **null Proposal**, even after the live wallet changes. Reusing its sequence or operation ID with changed arguments conflicts. Stale, unknown, invalid, locked, insufficient, capped and sequence-exhaustion requests grant nothing. Invalid caller context or impossible existing ownership throws; malformed request returns InvalidRequest.
- `SkillPurchaseProposal` exposes OperationFormat2, RuleRevision=`chapter.rules.v1`, Request, OwnershipBefore/After, WalletBefore/After, FrozenPrice, EffectsBefore/After. `MatchesPreconditions(long currentSequence, long wallet, SkillContext context, SkillOwnership ownership)` must pass immediately before queue reservation. These immutable objects never spend or activate anything themselves.
- `SkillTree.ValidateOwnership(SkillContext context, SkillOwnership ownership)` rejects owned skills without their required actual building/era/branch, and contexts regressing below recorded purchase stages. `SkillTree.EvaluateModifiers(SkillContext preCommandContext, SkillOwnership preCommandOwnership)` calls this validator and returns immutable `SkillModifiers`: UniversalCoins, CoinsPerTrash, CoinsPerDelivery, each0..2. Supply **pre-command** inputs. There is no public context-free effect evaluator.
- `SkillOwnership` exposes Version2, Revision=receipt count, immutable Receipts, Token, Empty, `Owns(string nodeId)` and `Migrate(UpgradeOwnership legacy)`. At most six unique node receipts and six unique operation IDs exist. Receipts are strictly increasing in global sequence; context and prerequisite chains are validated. Each `SkillReceipt` contains Request, Legacy, FrozenPrice and LegacyHouseStage.
- `SkillOwnershipCodec.Encode(SkillOwnership)` and `DecodeOrDefault(string)` roundtrip the bounded version2 payload. Null alone means a missing new field and defaults to Empty without writes. `DecodeOrMigrate(string json, UpgradeOwnership legacy)` explicitly migrates null from old ownership; when both states exist it rejects losing/conflicting old paid receipts. Unknown/duplicate/missing fields, unsupported versions, invalid JSON integers, noncanonical string escapes, oversized input, impossible receipts/prerequisites, unregistered node/selection IDs, decreasing stages and malformed legacy tags fail closed. Maximum payload24576 characters, each string2048 characters, receipt array6. Property order is independent. No filesystem persistence is provided.
- `SkillTree.ReplayLegacy(long currentSequence, long currentWallet, int currentHouseStage, SkillOwnership ownership, UpgradePurchaseRequest request)` preserves old sequence-based replay after migration and later skill purchases. It returns only a status, never a wallet/proposal. Legacy migration stores original sequence, wallet and house stage exactly with a tagged receipt and null historical Context; it does not invent prior selection/era/building context. Its deterministic `legacy.slot-<sequence>` marker is a tagged adapter identity, not a claim that the old operation recorded a string ID.

### Integration obligations

Recover any r7 pending format1 operation **before** migration. Missing skill state must use explicit `DecodeOrMigrate` when old boost ownership is present; do not replace paid ownership with Empty. Migration is a query projection until the next authorized saved command; it never charges, grants or writes. Native new purchases and preserved legacy ownership must share one canonical skill state rather than maintaining a second active boost.

`ReplayLegacy` takes the lifetime legacy house projection `min(absoluteHouse, 2)`, not the Workshop Yard absolute level. The canonical Universal1 node must agree with the compatibility `wave.Slot` projection. Core separately blocks skill purchases while the committed era transition awaits acknowledgement; `SkillContext` intentionally has no transition marker or clock state.

For new format2 spins, the universal scalar is the **total**, including the migrated old slot tier. It replaces the old separate slot credit; adding both doubles income. Pass the three validated pre-command scalars to Effects06's `ChapterSpinEffects.Prepare(..., ruleRevision, chapter, universalCoinsPerSpin, extraCoinsPerTrash, extraCoinsPerDelivery)`. Keep recycler1/trash, pigeon3/delivery and workshop1 as their existing separate baseline effects. Recycling/delivery skills require pre-owned recycler/loft. Skills change no cadence, carry, probabilities, base23/8/3 or symbol IDs. Effects06 freezes qualifying outcome/carry/deliveries; Skills never advances a counter.

GameSession/GameStore alone atomically freeze/reserve/commit wallet plus ownership/receipt through the existing queue. Persist RuleRevision=`chapter.rules.v1`, OperationFormat2, the complete request/context and before/after ownership, frozen price/wallet delta and before/after scalars before the first write. This module's codec serializes **ownership**, not a complete pending operation. Core owns the pending DTO/codec, exact-before validation, registered rule revision and frozen result validation; this module introduces no second journal. Recovery must validate/apply the recorded pinned descriptor, not call Quote/Propose against current tuning. Shop purchase never auto-completes selected construction. Skills never enter the chapter completion predicate.

### Scoped verification and limits

New harness passed **8 unchanged legacy +8 new skill regression groups**. New groups cover six independently expected prices/debits/caps; current->next quote effects/unlocks; exact replay vs changed requests/operation identity; interleaved sequences; all branch/building/era gates; insufficient/exact funds and long boundaries; all64 outcomes with separate qualification; strict malformed/bounded roundtrips; exact legacy migration/replay/no recharge; and an independently completed six-family/two-era context that remains skill-free. That last fixture proves the module adds no implicit purchase or chapter predicate; it does not establish connected chapter completion.

The first run passed all eight old groups, then failed in the new fixture with `Owned skill lacks its construction, era or branch prerequisite`: that fixture returned to era1 after buying Tier2. The validator correctly rejected the impossible context; the fixture was corrected to monotonic progression. Captured first output is `X:/tmp/jtn-skills-05/first-failure-captured.txt`. Later runs followed changes to shared-context binding, legacy tags, strict state/context validation and their regression cases; the final result is `X:/tmp/jtn-skills-05/results.txt`.

Command (PowerShell, from this checkout):

```powershell
$env:TEMP='X:/tmp/jtn-skills-05/Temp'
$env:TMP=$env:TEMP
dotnet run --project Checks/SkillsChecks/SkillsChecks.csproj `
  -p:ChapterContractsSource=X:/PROJECTS/FINAL-SVALKA/Unity/Assets/Scripts/Contracts/ChapterContracts.cs `
  -p:BaseIntermediateOutputPath=X:/tmp/jtn-skills-05/obj/ `
  -p:MSBuildProjectExtensionsPath=X:/tmp/jtn-skills-05/obj/ `
  -p:BaseOutputPath=X:/tmp/jtn-skills-05/bin/
```

Independent review against the released contract passed both sets of eight groups and **5,411 additional assertions across all 90 legal purchase orders**, including receipt tampering, quote preconditions, replay, legacy migration and terminal sequences. Evidence: `X:/tmp/jtn-skills-05/review/results.txt`. It found one tooling regression: the old UpgradesChecks wildcard included SkillTree without linking ChapterContracts, producing CS0246/CS0103 (`review/old-harness-failure.txt`). The parent added the same explicit `ChapterContractsSource` dependency to that project; its original eight groups then passed (`legacy-harness-fix/results.txt`). No domain source changed after review.

Both check projects now require the released ChapterContracts source; the override above works for either project before integration. Thin NUnit wrapper is supplied but not executed. No Unity launch/import, Library copy, APK, device, live save or Core/runtime integration was performed. Disposable output is confined to exact `X:/tmp/jtn-skills-05/`; retain the small verification receipts, and remove only closed task-owned build/temp output after 7 days or if it exceeds 8MiB. Disk was checked before and after; no automatic retention mechanism was installed. The next step is primary atomic persistence/UI integration and connected acceptance owned by 02.

## Preserved first-wave handoff (historical r6 evidence)

The following API and evidence describe the original module, now preserved unchanged in r7. Use the combined SkillsChecks command above for the new source and contract dependency.

Source baseline: `8903237ab90cc5b870c00bfc157fbaf0066eff87` (r6). This isolated stream added only the Upgrades domain, new checks and this handoff. No existing economy, save, runtime, scene or package files changed.

## Dependency and provisional offer

Requires coordinator-owned `JunkToNeon.Contracts` from `d100d75722b1a8f77caf5e851df567a90589954f`. The checked `GameplayContracts.cs` SHA256 was `AB26AFFF86CBDCEE9AE53DE5A9823C04127B649A698EFE9DBE66557A73B26E3A`. The standalone harness links that exact source; this stream does not vendor Contracts. The domain assembly references only Contracts and has `noEngineReferences:true`.

`CoinBoostShop.Offer` exposes `slot.coin-boost`, currency `coins`, prerequisite `house.repair` / house stage >=1, price120, cap1, and uniform additive +1 coin per subsequent spin. `Provisional` is true and `LiveEconomyEnabled` is false. There is no live purchase UI or activation in this change. These values are separate from the existing workshop offer and contribution. Symbol probabilities and exclusive base23/8/3 remain unchanged.

## Public API and bounded state

Namespace: `JunkToNeon.Upgrades`.

- `CoinBoostShop.Preview(wallet, houseStage, ownership)` returns immutable `UpgradePreview`: Offer, Status, Level, Owned, Maxed. Status is Locked, Available, InsufficientFunds or Maxed. Inspection never spends or mutates.
- `UpgradePurchaseRequest(offerId, sequence, expectedOwnershipRevision, expectedWallet, expectedHouseStage)` identifies an explicit foreground command. `CoinBoostShop.Propose(currentSequence, wallet, houseStage, ownership, request)` returns `UpgradePurchaseResult` with Status and an optional immutable Proposal.
- Purchase statuses: Proposed, Replay, Locked, InsufficientFunds, Maxed, Stale, Conflict, InvalidRequest, UnknownOffer, Overflow. Invalid caller context (null ownership, negative wallet or house stage outside0..2) throws; malformed request values return InvalidRequest. Rejections leave all inputs unchanged.
- `UpgradePurchaseProposal` exposes Request, ExpectedGlobalSequence, OwnershipBefore/After, WalletBefore/After, FrozenPrice, LevelBefore/After and UpgradeId. `MatchesPreconditions(sequence, wallet, houseStage, ownership)` must pass again immediately before reserving the transaction. A proposal itself grants nothing.
- `CoinBoostShop.Evaluate(SpinOutcome frozenOutcome, UpgradeOwnership preCommandOwnership)` returns the shared `EffectContribution` under `GameIds.SlotCoinBoost`:0 when unowned,1 when owned, for all64 outcomes. Always supply ownership from before the command; a proposed purchase cannot activate an acquisition-spin effect.
- `UpgradeOwnership` is immutable, with Version1, Level0..1, Revision equal to Level, PurchaseSequence, PurchaseWalletBefore and PurchaseHouseStage. Empty state has all five numeric state values zero. Owned state requires sequence1..long.MaxValue, original wallet120..long.MaxValue and original house stage1..2. Only one purchase receipt is retained; there is no growing history.
- `UpgradeOwnershipCodec.Encode` writes exactly `version`, `level`, `revision`, `sequence`, `walletBefore`, `houseStage`. `DecodeOrDefault(null)` alone represents a missing legacy field and returns unowned state without a write. Present state must contain all six integer fields exactly once; unknown fields, unsupported versions, malformed/trailing data, invalid bounds and overflow fail closed. Maximum input length is512 characters. This codec is an integration helper, not a separate save file or Unity JsonUtility contract.

An exact already-applied request returns Replay with **no Proposal and no replacement wallet**, even after unrelated global commands change the current sequence/wallet. Changed original request arguments conflict; an unknown offer is rejected. Fresh commands require the next global sequence and matching wallet, house stage and ownership revision. Sequence exhaustion returns Overflow. Recovery must apply frozen committed transaction data, never call Propose against a changed offer.

## Required primary integration

1. Extend the existing aggregate, deep Copy, strict codec and validation with optional bounded upgrade ownership. Missing legacy ownership means Empty without an eager write. Preserve legacy pending-operation interpretation; older frozen spins receive no new effect.
2. Add the explicit foreground purchase to the existing reserve/write/apply/write queue. Recheck proposal preconditions, then freeze sequence, stable ID, rule version, price, wallet delta, ownership transition and receipt **before the first write**. Debit and ownership/receipt must commit atomically in the existing save. Reuse current uncertain-write blocking/reload recovery. Do not create a second wallet or feature save, automatically buy a selected project, or rerun current tuning during replay.
3. For new spins, evaluate pre-command ownership and freeze the separate contribution alongside other effects. Sum using checked arithmetic. Keep `lastSpin.bonus` workshop-only and align the shared codec's contribution bounds deliberately before enabling the UI. Validate registered IDs and pinned frozen deltas rather than trusting arbitrary nonnegative amounts. Enable purchase presentation only after private-save/reload/interruption regressions pass in primary integration.

No billing, ads, backend, timers, random rewards, additional currencies or new RNG consumption are included.

## Verification and limits

The implementation child completed before the independent review child started. Standalone checks passed 8/8 regression groups: offer/preview immutability; explicit 157->37 purchase/preconditions; replay and changed request; cap/stale/conflict behavior; unlock/funds/long boundaries; all 64 outcomes and distinct workshop/checked-fold overflow; strict bounded codec/migration; roundtripped replay and interleaved global commands. No initial standalone failure was observed. Independent read-only review found no blockers; all eight groups and 60 additional assertions passed, including terminal sequence, interleaved replay and malformed JSON boundaries. Its disposable harness is at `X:/tmp/jtn-upgrades-2366/review/`.

Reproduce from the worktree with installed .NET10; use a fresh disposable output directory when repeating:

```powershell
$upgradeOutput = 'X:/tmp/jtn-upgrades-2366/implementation'
$env:TEMP = "$upgradeOutput/Temp"
$env:TMP = $env:TEMP
dotnet run --project Checks/UpgradesChecks/UpgradesChecks.csproj `
  -p:ContractsSource=X:/PROJECTS/FINAL-SVALKA/Unity/Assets/Scripts/Contracts/GameplayContracts.cs `
  -p:BaseIntermediateOutputPath="$upgradeOutput/obj/" `
  -p:BaseOutputPath="$upgradeOutput/bin/"
```

Results are in `X:/tmp/jtn-upgrades-2366/implementation/results.txt`. Unity's thin NUnit wrapper is provided but was not executed. No Editor/import/Library copy, live UI/save integration, APK or physical-device acceptance is claimed. This module check does not replace primary connected acceptance. Provisional balance still needs gameplay review.

Disposable outputs remain on X:, approximately0.5MiB for the implementation run. Manually retain closed task diagnostics for at most7days or8MiB, preserving verification receipts and unrelated files; no automatic cleanup mechanism was installed. Disk was inspected before and after checks.
