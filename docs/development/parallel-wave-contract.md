# Parallel gameplay wave contract

Status: integration contract v1, 2026-10-29. Source baseline: `8903237` (r6). This contract defines integration boundaries, not acceptance of unfinished modules or final balance. Logic and temporary assets are in scope; polished visuals, audio, external services and real payments are not.

Primary activation: the approved bounded wave is now connected at `61ef2a5` and packaged as r7. Active projects are house/workshop/recycler/pigeon loft; slot.coin-boost is an explicit optional shop purchase. Cart/power/extra eras remain disabled. The standalone Upgrades offer's LiveEconomyEnabled=false documents its isolated default; the primary adapter deliberately activates its explicit transaction path after connected checks. Final source/input/receipt/APK identities are in `docs/evidence/unity/android-build-parallel-wave-r7.json`. This does not establish final balance, retention or physical-device acceptance.

## Ownership

| Stream | Owned work | Shared files excluded until handoff |
| --- | --- | --- |
| 02 | This contract, Contracts module, primary integration, Core/codec/runtime/scene merges, primary Editor and eventual combined APK | Other workers' isolated source changes; coordinator's dirty ROADMAP update |
| 03 | Offline income in its isolated existing Core/codec/runtime copy | Primary checkout and other new modules |
| 04 | New Content catalog and validator | Existing Model/codec/runtime/scene |
| 05 | New Upgrades soft-currency shop/slot-upgrade domain | Existing Model/codec/runtime/scene and Content/Effects |
| 06 | New Effects recycler/pigeon domain | Existing Model/codec/runtime/scene and Content/Upgrades |

Workers provide complete tested commits and narrow evidence; 02 integrates them sequentially. No worker publishes to primary or builds an APK. Only 02 drives the existing primary Editor. New domain modules need no Unity instance, Library copy or import. Use at most one active child per chat, sequentially; inherited model/settings stay unchanged. Nobody reverts another owner's work. Existing user research and unrelated files remain untouched.

## Compiled shared API

`Unity/Assets/Scripts/Contracts/GameplayContracts.cs`, namespace/assembly `JunkToNeon.Contracts`, is owned by 02. It has no engine, persistence or catalog dependency. Other domain assemblies reference only this assembly, with `noEngineReferences:true`; they never reference Core or one another. Runtime/Core integration may reference modules in the forward direction. Standalone check projects must explicitly include the Contracts sources; the old Core check wildcard does not include sibling folders.

- `SymbolIds`: Potato=0, Box=1, Trash=2, Pigeon=3. These values and existing draw order never change.
- `SpinOutcome(int left, int middle, int right)`: immutable validated three-symbol value, properties Left/Middle/Right and Count(int symbol). No mutable array is retained or exposed.
- `EffectContribution(string effectId, long coins)`: immutable stable ID and nonnegative coin amount. It describes a result and never grants it.
- `CoinEffects.Total(IReadOnlyList<EffectContribution>)`: checked sum, at most16 entries, no nulls or duplicate effect IDs. Multiple matching symbols are summed by that effect into one contribution. Empty list means zero.
- `StableId.IsValid`: ordinal lowercase ASCII identifiers, first character a letter, remaining characters letters/digits/dot/hyphen, maximum64 characters. Shape validation is not registration: the catalog/integrator also rejects unknown IDs.

## Stable identities and current adapter

| Concept | Stable ID | Existing persisted identity |
| --- | --- | --- |
| Currency | coins | ModelState.coins |
| First house repair | house.repair | target1 / houseStage1 |
| House improvement | house.improve | target2 / houseStage2 |
| Workshop | workshop.build | target3 / workshopBuilt1 |
| Recycler | recycler.build | New; no legacy integer may be reused |
| Pigeon loft | pigeon-loft.build | New; no legacy integer may be reused |
| First uniform slot upgrade | slot.coin-boost | New; no existing save field is repurposed |
| Workshop per-spin effect | workshop.spin-coins | Existing lastSpin.bonus, still workshop-only |
| Recycler effect | recycler.trash-coins | New separate contribution |
| Pigeon delivery effect | pigeon.delivery-coins | New separate contribution |

Use these literals through GameIds. Other IDs must be unique, registered and documented by the owning handoff. Display labels, array order and asset filenames are never persistent identities. Content may use separate building IDs house/workshop/recycler/pigeon-loft, but project IDs above identify a purchasable stage. Renaming a saved ID requires explicit migration; never map an unknown ID to a default.

The current hard-coded adapter remains authoritative until deliberately integrated: house40/600, workshop120/+1 (provisional), initial37 once, equal independent symbols and exclusive base23/8/3. Content's baseline definitions must match it exactly. Catalog loading alone must not activate new content or rewrite saves. A catalog revision is not automatically the save's current contentVersion; 02 owns that compatibility decision.

## Domain results and content data

Effects evaluate the immutable outcome and **pre-command** owned levels/counters. Newly purchased upgrades/buildings never affect the spin that buys them. Base reward, workshop bonus and each new contribution remain distinguishable in receipts and presentation. Coins add with checked arithmetic; no effect changes symbol probabilities, consumes extra RNG, invents a hidden spin or multiplies the exclusive base payout. Slot coin-boost is an explicitly priced soft-currency upgrade; its first bounded effect is uniform additive coins, not altered odds. All new tuning remains provisional and exposed as data.

Domain modules are pure: no filesystem, Unity API, clock access, session mutation, automatic purchase, wall-clock callback or global mutable counters. Each evaluator receives all needed inputs. If recycler/pigeon logic uses progress/counters, its module returns explicit typed next-state data alongside contributions; the integrator freezes/persists that transition. Delivery is a deterministic result of the frozen foreground outcome, not an untracked background timer. Each worker documents its exact public evaluator/quote/state types and numeric bounds in its handoff; do not duplicate shared DTOs under another namespace.

Effects06's current provisional tuning is recycler +1 per trash symbol and pigeon +3 per three pigeon appearances, carrying0..2 appearances across spins. Counts include all three frozen symbols. For an already owned pigeon effect: total=beforeCarry+outcome.Count(Pigeon), deliveryCoins=3*(total/3), afterCarry=total%3. No ownership means no contribution or accumulated carry; persisted carry must be0 when unowned. Acquisition starts that effect at zero carry, excludes the acquisition spin, and begins observing only subsequent spins. Preview, reload, offline settlement and presentation never advance those counters. Freeze both coin amounts and before/after carry on a new spin operation; recovery applies the frozen transition once. Old frozen operations neither gain new contributions nor advance a newly introduced carry.

Content describes stable IDs, integer stage/level bounds, integer costs, explicit prerequisite IDs and levels, and a closed whitelist of effect kinds with bounded integer parameters. No arbitrary expressions, script names, reflection, executable callbacks or string evaluation. Reject duplicates, unknown effects/IDs, missing references, prerequisite cycles, invalid/overflowing numbers and unsupported versions. Definitions and validated outputs must be defensively immutable. Catalog validation never spends coins. An effect kind is distinct from a contribution ID; multiple source IDs must not collide.

Shop inspection returns a quote/availability result without spending. Only an explicit foreground purchase command may debit coins and advance one registered level. Insufficient funds, locked/maxed level, stale quote/state or overflow leaves the entire state unchanged. Repeated command sequence must not purchase twice. Modules do not write state themselves; Core integration supplies that command and receipt.

Project auto-completion is an explicit command allowlist, not a generic post-command side effect. Normal foreground spin and explicit project selection may complete that one selected target. A deferred affordable target requires an explicit foreground confirmation/resolution (the integration may reuse selection of that target). Offline settlement, hint acknowledgement, shop purchase and unrelated commands must not silently buy it. Preserve the deferred target through reload until that authorized foreground action.

## Transaction and compatibility boundary

GameSession/GameStore remain the only mutation/save owner. Existing order is reserve full pending snapshot → flush/write it → apply frozen result → flush/write committed snapshot → animate/show. Unknown outcome after a write blocks all later economic commands until the existing reload/recovery path resolves it. No separate feature file, second wallet or side save is allowed.

New commands freeze all replay-sensitive inputs/results before the first write: command sequence, stable IDs/level changes, prices, contributions and counter transitions, and any offline interval/cursor/amount. Recovery applies those exact values once; it must not rerun a changed catalog, redraw symbols or read the current clock. Validate frozen results against the pinned compatible rules or an explicit migration. Never loosen unknown/duplicate JSON rejection to accept new fields accidentally. Bound every persisted collection and receipt history. Include every new field in deep Copy, strict decode/encode, validation and interrupted-operation cases.

The current strict JSON Reader has a10-item array bound; CoinEffects'16-entry API is not yet a persisted schema. Before writing a contribution array, 02 must deliberately align the codec's field-specific bound and validator with maximum16 and preserve exact-three reel validation. Do not serialize16 entries into the unchanged reader or remove bounded parsing globally. Long-valued effect amounts also need deliberate codec handling; the old workshop int field stays separate.

Old v1 snapshots and pending operations retain their previous interpretation. Missing optional new ownership/counters/effects default to a documented zero state without an eager write or initial grant. Keep lastSpin.bonus workshop-only (currently0..1); new effects use separate validated receipt fields. Old frozen spins receive no newly invented effects on recovery. Older executables may reject newly written fields; downgrade compatibility is not promised. Existing explicit backup rollback limits still apply.

New frozen-effect operations require an explicit operation-format discriminator (or equally strict presence/version rule). Missing discriminator/fields selects the legacy operation format, never current-catalog reevaluation. Validate registered effect IDs, pinned rule revision, pre-owned levels/carry and expected frozen deltas together before recovery applies them; a nonnegative, duplicate-free arbitrary payout is still invalid.

Offline03 owns its rate/cap/cursor/settlement implementation. It grants only bounded coins from explicitly owned upgrades, no hidden spin/target selection/purchase. **Integration conflict to resolve explicitly:** current Validate rejects selected targets that have become affordable, and ApplyPending purchases an affordable selected target on every command. Offline settlement must therefore introduce an explicit validated deferred/foreground rule, preserving the player's target until visible foreground resolution. It cannot be integrated as just a new kind plus a credit. 02 will review that handoff before any merge.

## Handoff and combined acceptance

Each worker returns an immutable commit, owned paths, exact public API, provisional values, bounded state shape, passing commands/counts, original failures and remaining limits. Worker source checks do not establish runtime integration. 02 first verifies source/dependencies and private-save regressions, then integrates shared Model/codec/runtime/scene sequentially, verifies connected UI/reload/recovery and safe Back in the warm primary Editor, and builds one combined APK checkpoint after integration-ready commits. Preserve ordinary progress and earlier APK receipts. No per-stream APK or physical-device work is requested.
