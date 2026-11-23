# Repeatable post-chapter orders — stream04

Date: 2026-10-29 (Asia/Baku). Baseline042fb52/r8. Worktree/branch:
`X:/tmp/jtn-content-wave04`, `codex/content-catalog-wave04`.
Previous completed Content commits are preserved; a clean merge incorporated r8 without reset,
cleanup or Library sharing. Apply only this final owned-path Orders commit, not the baseline merge.

## Scope and resolved contract

Original task30 (dependency task29) was read from the original HTML JSON at
`docs/reference/junk-to-neon-plan-astra (2).html:1055`. It calls for two short event-driven choices,
one selected order, known rewards, clear replacement and duplicate-safe completion in the existing
journal. It does not supply final numeric tuning or authorize changing final AUTO behavior.

The coordinator and02 resolved crossed early messages: original REPEATABLE manual instances are
authoritative; the temporary once-only/max300 interpretation was withdrawn. The corrected
`docs/development/journey-pricing-contract.md` was reread. The final choices are:

| Stable type ID | Qualifying foreground facts | Target | Provisional coin reward |
| --- | --- | ---: | ---: |
| order.sort-salvage | Actual trash appearances in frozen three-reel outcomes | 12 | 120 |
| order.neighborhood-supply | Completed pigeon deliveries, not appearances or delivery coin credits | 3 | 180 |

Actual shared FamilyStages must be WorkshopYard with every absolute level5. Skills are not inputs
or prerequisites. No fees, inventory, new currency, clock, rare triple requirement, ads or automatic
accept/reward loop. Free preview shows the target and reward. One order is active; progress caps.
Reacceptance after claim/cancellation requires a new explicit action and fresh qualifying events.

## State, behavior and bounded storage

Namespace/assembly `JunkToNeon.Orders` references Contracts only. No Core/Content/Upgrades/Effects
dependency, Unity API, filesystem, RNG or wall clock. SHA256 provides a bounded deterministic
operation identity, not an authentication/signature or anti-tamper payment guarantee.

`OrderState.Empty` has no order/history. InstanceSequence is the GLOBAL acceptance sequence,
not a catalog position or reusable counter. Acceptance anchors Cursor to that sequence, clears
LastEvent and starts progress0. Same active type cannot be reselected to reset it. A quoted switch
before readiness can discard partial progress and create a distinct instance; its quote exposes
DiscardedProgress. Integration must present that loss explicitly. Alternatively it may require
Cancel then Accept. A ready order must be claimed or explicitly canceled/forfeited before replacement.

QuoteCancel exposes DiscardedProgress and ForfeitedRewardCoins, including120/180 when ready.
Discarding either quote performs no change. Explicit cancellation clears progress with no reward;
this remains possible when a ready claim would overflow, so it cannot trap the order layer.
Claim binds orderID + acceptanceInstanceSequence + new globalClaimSequence, requires readiness,
freezes the exact reward, clears active and retains ONE latest claim record. Nothing auto-selects
the next order. No list of all historical instances grows.

Cursor and LastCommandSequence are monotonic. Cursor>=InstanceSequence; an older instance's
claim sequence must precede a newer acceptance. LastEvent retains the complete latest input.
Only an exact retained event duplicate is a no-op. Conflicting same-sequence events reject;
all events older than Cursor reject as stale/unverifiable (global journal handles older duplicates).
An acceptance/cancellation cursor anchor is not a spin and has no replayable event. ExpectedCursor
must equal the previous state cursor for every new foreground event. Non-spin global gaps are valid.

LatestClaim stores only OrderId, InstanceSequence, ClaimSequence, RewardCoins. The latest exact
claim identity can replay as a no-op even after a new acceptance; a different claim sequence or
older overwritten claim rejects. Pure Apply preserves the LIVE wallet on no-op/replay, including
interleaved credits, rather than replacing it with the historical reward result.

## Exact API

- `OrderContext(FamilyStages stages)` exposes Stages/Unlocked; it checks actual completion.
- `OrderEvent(long operationSequence,long expectedCursor,SpinOutcome outcome,int completedDeliveries)`
  exposes OperationSequence/ExpectedCursor/Outcome/CompletedDeliveries. Counts0..1; no pigeon
  means0, three pigeons means1 under the existing owned-loft cadence3. Core verifies the exact
  delivery count against its frozen pre-owned checkpoint; plausible shape alone is not proof.
- `OrderState(long instanceSequence,string activeOrderId,int progress,long cursor,OrderEvent lastEvent,
  long lastCommandSequence,string lastOperationKey,OrderClaimRecord latestClaim)` exposes those
  immutable properties plus Ready. MaximumSequence=(long.MaxValue-2)/2. Parent must also require
  LastCommandSequence<=its actual global sequence (the other stored sequences are bounded by it).
- `OrderClaimRecord(string orderId,long instanceSequence,long claimSequence,long rewardCoins)`
  validates registered identity, temporal order and the fixed reward.
- `OrderCatalog.Choices/Get(id)` expose immutable target/metric/reward/label data. Querying is free.

Static OrderEngine:

```text
QuoteAccept(context,before,id) -> OrderSelectionQuote{Order,Context,Before,DiscardedProgress}
PrepareAccept(context,before,quote,globalSequence,wallet) -> OrderProposal
QuoteCancel(context,before) -> OrderCancelQuote{Context,Before,DiscardedProgress,ForfeitedRewardCoins}
PrepareCancel(context,before,quote,globalSequence,wallet) -> OrderProposal
PrepareObserve(context,before,sourceEvent,globalSequence,wallet) -> OrderProposal
PrepareClaim(context,before,id,expectedInstanceSequence,globalSequence,wallet) -> OrderProposal
Apply(context,liveState,liveWallet,proposal) -> OrderApplyResult{State,Coins,Applied}
```

OrderProposal exposes FormatVersion1, RuleRevision=orders.rules.v1, Action (Accept/Cancel/Observe/
Claim), Sequence, OrderId, ExpectedInstanceSequence, Context, Before, After, CoinsBefore, CoinsAfter,
RewardCoins, Event, Key (64 lowercase hex characters), NoOp. It contains immutable values only.
Non-claim operations grant0. Quotes bind the complete expected order state and shared context;
stale quotes/applications fail. Overflow rejects before any state or wallet is exposed for writing.

`RestoreProposal(int formatVersion,string ruleRevision,OrderAction action,long sequence,string id,
long expectedInstanceSequence,OrderContext context,OrderState before,OrderState after,long coinsBefore,
long coinsAfter,long rewardCoins,OrderEvent sourceEvent,string key,bool noOp)` validates the entire
frozen result against this pinned revision, including canonical identity and exact after-state/reward.
No future live catalog or current wallet is used to reinterpret a frozen operation.

`OrderStateCodec.Encode(state)` / `DecodeOrDefault(string)` use a canonical versioned payload
with maximum4096 characters. Fixed nine top-level fields, at most one event and one claim;
canonical decimal integers, exact registered IDs, strict shape/version/range checks. The token
format is `orders-state.v1|instance|active-or--|progress|cursor|lastCommand|key-or--|event-or--|claim-or--`.
An event has six comma-separated scalars; a claim four. This payload is an integration string,
never a side save. Null input means ABSENT field only; Core must reject PRESENT null before calling
DecodeOrDefault. Empty/whitespace/extra/unknown/noncanonical/malformed present data fails closed.

## Atomic integration by02

Do NOT enqueue an observe command after a spin has already been saved. PrepareObserve requires
event.OperationSequence==the SAME reserved global spin sequence and freezes order progress inside
the source-spin pending/applied snapshots. Supply only the trusted actual frozen outcome and
completedDelivery COUNT. No offline/hint/inspection/shop/project/ack/presentation event is synthesized.
The expected cursor catches ordering mismatches; atomic Core composition prevents a crash gap
or lost intermediate spin. Module inputs cannot independently prove that a global event exists.

Wallet phase must be consistent: Observe has zero delta but binds its supplied wallet. Prepare/apply
the nested order operation against the SOURCE-SPIN AFTER wallet, or validate/use its exact before/
after order state within Core's complete composed operation. Applying a proposal prepared with
spin-after coins against spin-before coins is invalid. Claim is a separate journal command that
credits its frozen reward once, never spins, advances carry, purchases construction or changes skills.
Prepare/Restore/Apply are pure; the existing GameStore remains the sole writer/sequence owner.

Missing old journey/orders state projects Empty without eager writes or a grant. Core validates
context eligibility for nonempty stored state, future sequences, complete frozen source facts and
root aggregate/descriptor presence. Preserve literal r8 pending replay; do not add order progress
retroactively. Older global replays are suppressed by the existing queue before this bounded module.
The final AUTO stop policy stays unchanged unless separately authorized; manual foreground spins
remain sufficient to complete an accepted order. No retention timer or automatic rotation exists.

## Verification and retained issues

Twelve standalone Orders groups pass: both orders and repeats, exact120/180 reward oracles,
chapter lock, free previews, quoted switching/cancel/ready forfeiture, idle and pre-accept facts,
duplicate/latest-conflicting/historical/missing-cursor events, non-spin gaps, immutable stale requests,
ready overflow, strict codec, full frozen restore/tampering and replay after interleaved wallet credits.
64 legitimate scrap cycles grant exactly7680 and keep only one claim/event checkpoint; encoded
state remains below512 characters in that fixture, within the hard4096 bound.

Existing unchanged r8 Core harness:39 groups passed. Installed Unity6000.3.21f1 Roslyn compiled
production Orders + Contracts against its NetStandard2.1 reference, C#9, /nostdlib+ /warnaserror+:
exit0/no diagnostics. No Editor/import/EditMode runner/APK or connected runtime acceptance ran.
The new Orders EditMode wrapper is ready for02's warm integration check.

Independent read-only review found and confirmed fixes for two gaps that initial tests missed:
unverifiable older events had been treated as no-ops, and constructors accepted impossible cursor/
claim/accept temporal order. Exact regressions now reject both constructor and encoded forms.
No executed test or C# build failed. Early contract once-only wording was explicitly withdrawn;
repeatable semantics and the corrected source were confirmed before freezing this module.

Commands (from this X: worktree, with process TEMP/TMP=.work/temp):

```powershell
dotnet build Checks/OrdersChecks/OrdersChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-content-wave04/.work/orders-obj/ -p:OutputPath=X:/tmp/jtn-content-wave04/.work/orders-bin/
dotnet .work/orders-bin/OrdersChecks.dll
dotnet build Checks/CoreChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-content-wave04/.work/orders-core-obj/ -p:OutputPath=X:/tmp/jtn-content-wave04/.work/orders-core-bin/
dotnet .work/orders-core-bin/CoreChecks.dll
```

Resource sample before C:9.92GiB/X:88.95GiB; after C:9.91GiB/X:88.94GiB, including concurrent
machine activity. Orders bin/obj554313bytes; Core bin/obj1122414bytes; all retained task logs26426bytes,
process temp11286bytes. No Library/SDK duplicate, Editor, global setting, external account/service,
other module or canceled audio draft changed. Eleven new asset metadata files passed GUID checks.
Keep receipts; disposable closed logs/temp have seven-day retention and8MiB budget before reuse.
This is a bounded manual reuse policy, not installed automatic rotation.

Next action:02 integrates only this new-module/test/handoff commit, connects the frozen order delta,
claim/cancel UI and root codec, then verifies source-spin/claim write-failure positions, reload,
ordinary no-event commands and exact replay before the combined warm Editor/UI/APK checkpoint.
The domain is delivered; connected feature acceptance remains with that integration.
