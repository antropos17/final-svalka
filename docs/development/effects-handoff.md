# Effects: era and skill extension handoff

## Current wave: two eras and three skill branches

Date: 2026-10-29 (Asia/Baku). Source baseline `f5bfc34`, branch
`codex/effects-era-skills-wave08`, source-only worktree
`X:/tmp/jtn-effects-wave06`. The original `codex/effects-wave06` branch and
`4a03846` commit are preserved. The earlier implementation below was subsequently
integrated and packaged as r7; its original inactive-module status is historical.

This wave extends pure Effects only. Content owns the two-era/six-family chapter,
Upgrades owns the three two-tier skill branches, and 02 owns shared Contracts and
all game integration. No existing Core/codec/runtime/scene/settings files are
changed here. New code does not activate itself.

### Compatibility and approved semantics

- Keep `SpinEffects.cs` and the literal r7 `FrozenSpinEffects` format-1 behavior
  unchanged. Baseline Git blob: `18247819f0363ee8ce47af46992eed9b24df2736`.
  A new explicit version handles extended frozen descriptors; it must never
  reinterpret a version-1 pending spin using new content or skills.
- Base 23/8/3 and workshop stay caller-owned and separate.
  New chapter symbol credits include only their assigned new construction
  benefits; do not include the existing recycler, delivery, workshop or slot
  credit in that aggregation a second time.
- Skills supply three additive scalars, each +1 per owned tier, at most two tiers:
  universal coins per spin, extra coins per trash symbol, and extra coins per
  COMPLETED pigeon delivery. Universal is independent; trash skill requires a
  pre-owned recycler; delivery skill requires a pre-owned loft. Skill branches
  enhance their corresponding building rather than substituting for it.
  Nonzero recycling/delivery scalars in an unowned-building context are invalid
  and fail closed; callers must not silently suppress invalid saved ownership.
- `SkillIds.Universal1` is the original `slot.coin-boost`, not a new ownership
  identity. The format-2 universal scalar is the TOTAL universal skill credit,
  including this first tier, and REPLACES a separate legacy slot credit. Format-1
  replay still uses the literal old slot accounting. The two paths must not be
  combined or charge/grant the canonical first tier twice.
- Only pre-command ownership and frozen symbols participate. Acquisition spins
  do not benefit from the building/skill just purchased. Preview, offline
  settlement, presentation, reload and non-spin commands do not advance carry.
- Cadence stays three appearances and carry stays 0..2. Coin-rate changes preserve
  that carry exactly. No cadence change, reset, modulo migration or elapsed-time
  behavior is authorized in this wave. Any future change requires a separately
  frozen explicit migration; unknown cadence must fail closed.
- GameStore remains the single atomic owner of wallet, frozen proposal,
  contributions, checkpoint and global operation sequence. Recovery must use
  frozen compatible rules, never a changed current catalog.

### Exact shared dependency and bounded state

02 supplies `ChapterContracts.cs` and `ChapterSymbolModifiers.cs` in the existing
`JunkToNeon.Contracts` assembly. Effects uses those exact types, without depending
on Content or Upgrades. `ChapterSymbolModifiers` freezes at most 32 unique
`SymbolCoinSource` descriptors, sorted ordinally by source ID; each source has a
validated symbol 0..3 and an integer amount 1..1,000,000. The evaluator retains the
complete source list in its frozen proposal, while combining it into exactly four
named chapter contribution categories. The three additional skill categories and
two existing building categories keep the complete credit list at most nine.

Source ID/ownership registration and compatibility with the pinned content rules
remain Core's responsibility. A syntactically valid source with a positive amount
does not prove that its building was owned. During reserve, pass only modifiers
derived from the same pre-command snapshot; on restore, validate frozen source
IDs, amounts and ownership using the compatible pinned revision, not the current
catalog. Acquisition/era-unlock commands must not evaluate a spin or advance carry.

The production codec's old generic 10-item array limit cannot directly hold all
chapter sources. Integration must define a field-specific source-list bound of
32 while preserving exact-three reel validation and other bounded shapes. Nine
coin contributions do not remove the separate source-list parsing requirement.

### Exact format-2 boundary and accounting example

The final owning contract is `docs/development/two-era-skill-contract.md` from 02.
Its method boundary is:

```csharp
ChapterSpinEffects.Prepare(
    SpinOutcome outcome, EffectOwnership preOwned, DeliveryState before,
    long operationSequence, EffectRules frozenBaselineRules, string ruleRevision,
    ChapterSymbolModifiers chapter, long universalCoinsPerSpin,
    long extraCoinsPerTrash, long extraCoinsPerDelivery)
```

Baseline rules stay 1 coin/trash, 3 appearances/delivery, 3 coins/delivery. The
primary registered revision is `chapter.rules.v1`; the module validates its stable
ID shape and freezes it, while Core validates that it is a supported pinned
revision. All three skill scalars are 0..2. Sources and revision must be frozen
alongside exact amounts, baseline rules, pre-owned flags and full checkpoints.

Example independent expectation: wallet 10, both buildings already owned, carry 2,
outcome trash/trash/pigeon, all skill scalars 2, chapter symbol rates
potato/box/trash/pigeon = 1/2/3/4. Baseline contributions are recycler 2 + delivery 3;
skills are universal 2 + recycling 4 + delivery 2; chapter contributes trash 6 +
pigeon 4. Module total is 23, carry becomes 0. Core freezes base pair 8 + workshop
1 as external credit; final wallet is 42. Adding the old slot +1 again would be an
integration error. A matching committed replay must leave 42 unchanged.

### Restore mapping and integration adapter

The result is immutable `FrozenSpinEffectsV2`, with `FormatVersion = 2`.
Its restored constructor takes, in order:

```csharp
new FrozenSpinEffectsV2(
    int formatVersion, FrozenSpinEffects baseline, string ruleRevision,
    ChapterSymbolModifiers modifiers, long universalCoinsPerSpin,
    long extraCoinsPerTrash, long extraCoinsPerDelivery,
    long universalCoins, long recyclingSkillCoins, long deliverySkillCoins,
    long potatoCoins, long boxCoins, long trashCoins, long pigeonCoins)
```

Persist the complete literal format-1 `Baseline` as a child, with its frozen
outcome/ownership/rules/before checkpoint/counter transition and original amounts.
Persist `RuleRevision`, every sorted `Modifiers.Sources` descriptor, the three
scalar inputs and all seven named amounts above. The constructor validates those
amounts, rebuilds the bounded contribution collection and reconstructs `After`.
If the codec also persists an after checkpoint, compare every field to this
reconstructed value; never trust a detached after key/carry. Do not drop full
source identities just because aggregate coin totals match.

The format-2 replay key is a deterministic SHA-256 digest over length-prefixed
canonical frozen fields, including revision, full source identities/rates,
baseline identity, full prior identity, skill scalars and named amounts. It is
75 characters (`effects.v2/` plus 64 hex characters), below the existing 256 limit.
This is a replay identity, not save authentication. Reordered identical sources
canonicalize to the same key; changing a source ID or revision changes the key
even when the coin amounts happen to match.

Call `proposal.Apply(currentDelivery, wallet, frozenExternalCredit)` once, where
the external credit is only the SAME operation's frozen exclusive base and
workshop bonus. The return is the existing immutable `EffectCommit` with suggested
wallet, delivery checkpoint and `Applied` flag. Persist **proposal.After / the
returned Delivery**, not `proposal.Baseline.After`: that child retains the literal
version-1 identity and is not the version-2 replay checkpoint. Do not also invoke
the child's Apply or add its recycler/delivery credit through the old Core path.

The format-2 proposal accepts an exact matching prior version-1 checkpoint, so
the next new spin continues the existing carry without reset. The next proposal
likewise accepts a prior version-2 checkpoint. Duplicate committed replay is a
no-op; changed-source/revision conflicts, stale checkpoints and unapproved
cadence/rule changes fail closed. Core still binds base credit, wallet, source
registration, pre-owned levels and the global sequence to the same full pending
snapshot. Backup rollback limitations remain unchanged.

### Current verification and task storage

Shared dependency: immutable Contracts release
`6fc9ee2266da35091dd64e843a8158593a01fdd9`. Test copies of its exact
GameplayContracts/ChapterContracts/ChapterSymbolModifiers sources are under
`.work/chapter-effects/contracts`; they are not duplicated into this commit.
The first approved snapshot differed from the released ChapterContracts only in
invariant integer formatting for IDs. After inspecting that difference, the
snapshot was updated to the release and a final scoped run passed.

```powershell
$env:TEMP = "$PWD/.work/effects-checks/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet-home"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project Checks/Effects/EffectsChecks.csproj -p:ContractsDirectory=X:/tmp/jtn-effects-wave06/.work/chapter-effects/contracts -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

Final result: **11 unchanged format-1 groups, 10 new groups, and 2 JSON roundtrips
passed**. The independent outcome grid covers three source profiles, all 64
outcomes, all 27 skill-scalar combinations and eight valid building/carry states:
23,040 accepted calculations and 18,432 expected prerequisite rejections.
Additional cases cover source sorting and identity, no duplicate canonical slot
income, acquisition inputs, frozen restore after catalog changes, changed
source/revision replay conflicts, invalid format/rates/skills/cadence, literal
v1-to-v2 carry continuation, 32 sources each at 1,000,000 coins per symbol,
maximum nine contributions, exact `long` wallet boundary and overflow, and pure
immutable inspection. The v2 JSON fixture restores all 19 full source descriptors
plus skills/revision/baseline/after checkpoint, reaches wallet 182 and replays
without another credit. Harness JSON is not production Core codec coverage.

No build or assertion failure occurred. Certificate generation was disabled
before every invocation in this wave; no global configuration or Editor was
used. Existing `SpinEffects.cs` has no diff from `f5bfc34`; its SHA-256 remains
`CBE74D7E07F45381B9765ED3315B70420F7127D1296288462E7E48F18C13185F`.

The second sequential read-only reviewer found no blocking issue. It independently
checked literal v1 compatibility, complete v2 replay identity, bounded source and
contribution collections, one cadence-3 carry, checked arithmetic and the coverage
above. It did not rerun tests. Its remaining boundaries are Core's registered
source/revision validation, base-plus-workshop-only external credit and atomic
production persistence; those are explicit integration responsibilities here.

Initial free space C 10,701,541,376 / X 95,614,894,080 bytes; final worker sample
C 10,688,299,008 / X 95,604,240,384 bytes. Task `.work` measured 40 files /
630,029 bytes, including a 2,265-byte final stdout receipt at
`.work/effects-checks/wave08-final.txt`. All obj/bin/temp/CLI-home/output remain
on X:. Concurrent streams also use these drives; these are samples, not an
attribution of every drive change to this task. The existing exact-directory
seven-day / 8 MiB disposable diagnostic retention policy below still applies.

The new Unity test wrapper is supplied but not run in an Editor. Core strict
save migration, live integration, source registration, connected UI and final
APK checks remain 02-owned. This module commit is not full two-era acceptance.

## Preserved first-wave handoff (historical)

Date: 2026-10-29 (Asia/Baku). Stream 06, isolated source-only worktree
`X:/tmp/jtn-effects-wave06`, branch `codex/effects-wave06`, source baseline
`8903237` (r6). This module is not activated in the game.

## Scope and authority

Original `PLAN HERE.html`, task 14, requires a recycler improving the trash-symbol
result and a pigeon-related delivery benefit, with exact effects and cadence
defined as data and benefits issued once. It supplies no exact amounts or cadence.
The provisional defaults here are +1 coin per trash symbol and +3 coins per three
pigeon symbol appearances after ownership. These are implementation fixtures and
prototype tuning, not final or measured balance. Recycler/loft prices,
prerequisites, selection and purchase UI belong to Content and primary integration.

The module uses the 02-owned `JunkToNeon.Contracts` assembly and its exact
`SpinOutcome`, `EffectContribution`, `GameIds`, and checked contribution total.
The authoritative integration boundary is
`docs/development/parallel-wave-contract.md` supplied by 02. Effects has no Core,
Unity, filesystem, clock, Content or Upgrades dependency and does not activate
itself. Base exclusive rewards 23/8/3 and the existing workshop bonus stay separate.

## Trigger and atomicity requirements

- Evaluate only the three already-frozen foreground spin symbols and ownership
  from before the economic command. The spin purchasing a recycler or pigeon loft
  receives none of that new building's effect. Preview and non-spin commands do
  not advance delivery progress.
- Each eligible pigeon symbol advances delivery progress once. With the default
  cadence 3, the first eligible spin can deliver if it has three pigeons; otherwise
  progress carries into later eligible spins. Counting is by symbol occurrence,
  not elapsed time, number of commands, AUTO frames or animation events.
- There is one wallet. Each effect returns its own named additive coin
  contribution; no symbols are replaced and no RNG is consumed.
- GameStore must freeze the proposal, its operation sequence, all contributions
  and before/after delivery state in the existing full pending snapshot, then
  atomically commit wallet and delivery state before presentation. Recovery applies
  that frozen proposal, without consulting current catalog tuning.
- Duplicate protection applies to the same committed history. An explicit backup
  rollback also rolls back operation history; cross-rollback exactly-once
  guarantees are not claimed.

## Integration limits

This stream does not edit ModelState, its codec or copy/validation paths,
GameSession/GameStore, runtime/scene, settings, existing tests or the primary
checkout. Existing saves and pending operations therefore have no new effect until
02 deliberately integrates ownership, counters and frozen receipts. Old frozen
spins must retain zero new contributions and must not retroactively advance
delivery progress. Missing legacy fields default to the documented zero state,
with no eager write or fresh grant. Unknown and malformed data must remain blocked.

Pure source checks are not connected game, Unity compilation/EditMode, APK or
Android persistence acceptance. These integration checks remain owned by 02.

## Public API and save mapping

Namespace and assembly: `JunkToNeon.Effects`. `SpinEffects.Prepare(SpinOutcome,
EffectOwnership, DeliveryState, long operationSequence, EffectRules)` returns an
immutable `FrozenSpinEffects`. The constructor for this proposal accepts restored
fields and validates their consistency against its own frozen rules, without
accessing a live catalog. The integrator must additionally check those rules
against the pinned compatible content version; self-consistency is not save
authentication or permission to accept arbitrary catalog values.

| Value | Inputs / persisted fields | Bounds and purpose |
| --- | --- | --- |
| `EffectRules` | `RecyclerCoinsPerTrash`, `PigeonsPerDelivery`, `CoinsPerDelivery` | Coin amounts nonnegative `long`; cadence 1..1,000,000. `Provisional` returns 1/3/3. All products/sums checked. |
| `EffectOwnership` | `RecyclerLevel`, `PigeonLoftLevel` | Each 0 or 1, mapped from the PRE-command levels of `recycler.build` and `pigeon-loft.build`. |
| `DeliveryState` | `LastOperationSequence`, `PigeonProgress`, `Cadence`, `LastProposalKey` | One checkpoint, no history list. Sequence nonnegative; carry 0..cadence-1; replay key at most 256 characters. Fresh is sequence 0, carry 0, null key. |
| Frozen proposal | Format version 1; command sequence; exact outcome; ownership; frozen rules; full before checkpoint; after carry; delivery count; recycler/delivery coins | Constructor reconstructs contributions and after checkpoint, rejecting inconsistent values. Counter cadence changes require explicit migration. |
| `EffectCommit` | `Delivery`, `Wallet`, `Applied` | Pure suggested result of `proposal.Apply(currentDelivery, wallet, baseReward)`; it writes nothing. |

`Contributions` is an immutable collection with at most two immutable entries,
using `GameIds.RecyclerTrashCoins` and `GameIds.PigeonDeliveryCoins`. Unowned
effects are absent; owned effects may explicitly contribute zero. Multiple trash
symbols are combined into one recycler entry; multiple deliveries into one
delivery entry. `TotalCoins` is a checked sum. `Deliveries` gives presentation a
countable delivery event after commit, without an animation-owned reward trigger.

The checkpoint advances for every foreground spin processed by this module,
including spins with no owned effects. Global economic command IDs may have gaps
between spins because hint, selection, offline settlement or purchase commands do
not advance the delivery checkpoint. An unowned loft requires zero carry.

### Suggested integration order

1. Reserve the normal frozen spin outcome using the unchanged Core RNG. Read
   recycler/loft ownership and the delivery checkpoint from the same pre-command
   snapshot. Call `Prepare` with the global command sequence. Freeze every proposal
   field listed above in the existing pending snapshot; do not persist a second
   file or a side ledger. Freeze the base payout and other contributions alongside
   it, keeping workshop-only `lastSpin.bonus` compatible.
2. Validate/load the frozen proposal from that pending snapshot. `Apply` checks
   the exact before checkpoint, including its replay key, and returns a checked
   wallet plus next checkpoint. Its `baseReward` argument must be the credit
   frozen in that SAME Core pending operation, never a newly evaluated catalog
   result. Account for workshop/slot bonuses exactly once: either pass the checked
   sum of all other frozen credits as this argument or perform one checked
   combined contribution calculation in Core. Do not then add the same base or
   effects again through the old reward path.
3. Resolve only the already selected project under the integration's foreground
   purchase rule. Ownership acquired here affects the next spin. Atomically save
   the resulting wallet, ownership, checkpoint, separate contributions and global
   command sequence in one committed snapshot before effects are displayed.
4. On interrupted pending recovery, rebuild from persisted frozen fields and use
   `Apply`; no live `Prepare`, clock or catalog re-evaluation. A matching committed
   after checkpoint returns `Applied=false` without another credit; a mismatched
   checkpoint, conflicting same-sequence proposal or older proposal fails closed.
   GameStore's global sequence remains the overall command replay boundary.

No existing `selectedStage` integer is assigned here. 02 owns any new persisted
targets and schema migration. Strict copy/codec/validation and whole-transaction
failure/recovery tests must accompany that integration.

## Verification and storage evidence

The implementation worker ran the standalone harness against the exact Contracts
release `d100d75722b1a8f77caf5e851df567a90589954f`, extracted with `git show` to
`.work/effects-checks/contracts/GameplayContracts.cs`. The dependency is not
duplicated in this commit. Integrate that Contracts release before using the
default checkout-relative source path.

```powershell
$env:TEMP = "$PWD/.work/effects-checks/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet-home"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project Checks/Effects/EffectsChecks.csproj -p:ContractsSource=X:/tmp/jtn-effects-wave06/.work/effects-checks/contracts/GameplayContracts.cs
```

Result: **11 behavioral regression groups and 1 JSON roundtrip passed**. Coverage
includes all 64 outcomes with independent expected counts; no ownership; recycler
alone; delivery alone; both effects; acquisition-spin exclusion; first eligible
triple-pigeon delivery; sparse global command IDs; cadence 1 with multiple
deliveries; frozen restore with changed live tuning; duplicate/stale/conflicting
replay; invalid restored deltas/carry/version; immutable result collection;
checked multiplication, aggregate credit and wallet overflow; exact `long.MaxValue`
boundaries. The in-memory JSON fixture restores both pending fields and a committed
checkpoint, obtains base 8 + recycler 2 + delivery 3, wallet 23 from 10, and then
replays without another credit. This harness-only JSON mapping does not exercise
the production strict Core codec or filesystem.

No assertion or build failure occurred. The first .NET invocation announced
automatic development HTTPS certificate setup. No manual certificate/configuration
change or cleanup was performed; whether a certificate was newly installed was not
independently verified. The final invocation disabled automatic certificate
generation. This incidental first-use behavior is not needed by the module.

The second, sequential read-only reviewer found no blocking correctness issue in
the module or its standalone coverage. The review specifically checked replay,
frozen-rule restoration, bounded carry, immutable state and checked arithmetic.
It did not rerun tests. Its remaining integration concern is the caller boundary:
wallet/base credit and the checkpoint must come from and commit with the same
GameStore pending snapshot, as specified above.

An EditMode wrapper for the same 11 groups is provided in its separate test
assembly, but was not run here; no Unity Editor, import, APK build or live save was
used. The unchanged existing Core suite was not repeated for this inactive module.

Initial free space: C 10,783,903,744 bytes; X 95,802,273,792 bytes. Final worker
sample: C 10,740,322,304 bytes; X 95,777,787,904 bytes. A parent sample shortly after
was C 10,739,261,440 / X 95,777,173,504; concurrent streams also use these drives.
All task outputs under this worktree's X: `.work` measured 32 files / 508,661 bytes,
including obj/bin, private CLI home, pinned dependency and bounded diagnostic
logs. `Directory.Build.props` puts MSBuild obj/bin there before SDK imports.

Retention: retain the commit/handoff; the exact task `.work/effects-checks/temp`
diagnostic folder has a 7-day / 8 MiB budget before reuse. Only confirmed closed
disposable regular files in that exact folder may be removed, skipping locks and
reparse points. Inspect before another batch; no automatic rotation is claimed.

Next integration action: 02 integrates the Contracts dependency and this module,
then owns strict saved-field wiring, foreground purchase semantics and connected
private-save recovery/Unity checks before any combined APK checkpoint.
