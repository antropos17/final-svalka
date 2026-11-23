# Junk to Neon Save Inspector

Read-only command-line tool that explains a **copied** game snapshot, compares two copies, and replays a pending frozen operation in memory. It links the unchanged production pure C# sources (`Unity/Assets/Scripts/{Core,Contracts,Content,Effects,Upgrades,Orders,Guidance}`) the same way `Checks/CoreChecks.csproj` does. Decoding, validation, projections and replay all use the production `JsonStateCodec.Decode`, `GameLogic.Validate`, `ChapterEconomy`/`JourneyEconomy` projections and `GameLogic.ApplyPending`. The tool has no second decoder and no copied economy formulas. It needs no Unity, NuGet packages or other third-party dependencies.

## Build and run

Installed .NET SDK 10. obj/bin go to the ignored `.work/save-inspector/` (see `Directory.Build.props`).

```
dotnet build tools/SaveInspector/SaveInspector.csproj -c Release --disable-build-servers -nodeReuse:false
.work/save-inspector/bin/Release/net10.0/junk-save-inspector.exe <command> ...
```

`dotnet run --project tools/SaveInspector/SaveInspector.csproj -c Release -- <command> ...` also works.

## Commands

Every command is noninteractive and writes only to stdout. `--format` is `text` (default) or `json`.

```
inspect          --workspace <dir> --input <copied-snapshot>                [--format text|json]
diff             --workspace <dir> --before <copy-a> --after <copy-b>       [--format text|json]
preview-pending  --workspace <dir> --input <copied-snapshot>                [--format text|json]
self-test        --repo <repository-root> --workspace <empty-private-dir>   [--format text|json]
```

Examples, after copying the snapshots into a private task folder:

```
junk-save-inspector inspect --workspace X:/tmp/task/ws --input X:/tmp/task/ws/copy.json --format json
junk-save-inspector diff --workspace X:/tmp/task/ws --before a.json --after b.json
junk-save-inspector preview-pending --workspace X:/tmp/task/ws --input pending.json
junk-save-inspector self-test --repo X:/PROJECTS/FINAL-SVALKA --workspace X:/tmp/task/selftest-ws
```

A relative `--input` is resolved against `--workspace`.

## Input boundary

- **Location.** The input must resolve inside the explicit `--workspace`. The tool never discovers files and never opens `Application.persistentDataPath`. Paths under `AppData\LocalLow` are refused.
- **Refused paths.** UNC paths, `\\?\` and `\\.\` device paths, alternate data streams (`:` after the drive), reserved device names (`CON`, `NUL`, `COMn`, `LPTn`, ...), segments with a trailing dot or space, directories, and reparse points or links (on the file or any directory up to the workspace).
- **Size.** Inputs larger than `4 × 65536 + 4` bytes are refused before reading. That is the largest byte form (UTF-32 plus BOM) of a 65536-character text, so the pre-check never refuses anything production could accept. Production's Reader stays the authoritative 65536-character, depth-8 and array bound. No production bound is raised.
- **Reading.** The file is opened read-only (`FileShare.Read`) and read once with a byte cap plus one sentinel byte. That buffer is hashed and then decoded exactly like `File.ReadAllText` in `FileSnapshotStorage` (UTF-8 default, BOM detection).
- **Integrity.** After analysis, a changed length is an integrity violation without reopening or hashing the changed file. Otherwise the reopened handle is length-checked and reads at most the original byte count plus one sentinel; replacement/growth refuses verification. Same-length bytes are rehashed and directory entry names/sizes are compared. A change sets `integrityViolation` and exit 8; `sha256After` is null when bounded verification is refused.
- **No storage path.** The tool never calls `GameStore`, `GameSession`, `FileSnapshotStorage` or any `ISnapshotStorage`. It performs no recovery promotion, quarantine, normalisation or fresh-save creation.
- **Bounded errors.** Errors are stable categories. Production exception text is cut at the first `:` or `(` and ASCII-filtered, so input-derived keys, ids and payload fragments never reach the report. Inputs are identified by role, size and hash, never by echoing content. Reports contain no file names or absolute paths.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (`diff` returns 0 for both equal and different; see `equal`) |
| 1 | Tool-internal error (type name only, no stack trace) |
| 2 | Usage error (missing/unknown/duplicate option) |
| 3 | Input refused at the boundary (`error.category`: `outside-workspace`, `device-path`, `alternate-stream`, `not-a-file`, `missing`, `empty`, `too-large`, `reparse-point`, `protected-location`, `workspace-missing`, `workspace-not-empty`, `workspace-in-repository`) |
| 4 | Snapshot rejected by production decode/validation (`json-format-or-shape`, `numeric-range`, `production-validation`, `production-invariant`) |
| 5 | `preview-pending`: no pending operation (committed snapshot; nothing is replayed) |
| 6 | `preview-pending`: replay refused or inconsistent |
| 7 | `self-test` failed |
| 8 | Integrity violation: the input changed, or adjacent directory entries changed, during the command |

## Output schema

Every report starts with `tool {name, version, productionSources}`, `command` and `status`.
Version1.1 replaces the stale hardcoded r9 `productionPin` with a build-time manifest
of actual linked pure C# bytes, including dirty source. `productionSources` reports its
SHA-256, file count, algorithm and qualification limit. The embedded UTF-8 manifest
contains `relative/path=UPPERCASE_SOURCE_SHA256` lines in project compile order; its
hash normalizes separators to `/` and line endings to LF and excludes the BOM.
Inspection reads the embedded resource, never Git or source files. This identifies
build inputs; it does not establish runtime, native or test qualification. Historical
HANDOFF/FIX-HANDOFF receipts remain evidence of their original r9 builds only.

### `inspect` (`status: ok`)

Version1.6 adds the optional `arcade` group to1.5's expansion/workbench/focus facts. V1 uses layer `expansion-r13`; V2 uses `expansion-r14`; a confirmed V3 aggregate uses `expansion-r15`. The layer describes the current aggregate: a pending operation's future V3 adoption is shown separately. `customizationSource` distinguishes projected V1 defaults from actual stored V2/V3 fields. All six known palettes are reported. Pending facts identify their exact frozen rule/before-after formats without treating future ownership as already committed. Consumers should tolerate additive groups. The four chapter `milestones` and construction0..24 retain their exact meaning. The same extractor feeds `diff` and `preview-pending`. Each group's `source` is one of:

- `stored`: present in the snapshot.
- `projected-not-persisted`: production's in-memory projection of an absent aggregate. Inspection never adopts it.
- `derived`: computed from the other facts.

| Group | Contents |
|---|---|
| `format` | `schemaVersion`, `contentVersion`, `layer` (`legacy-pre-wave`, `legacy-wave-r7`, `chapter-r8`, `journey-r9`, `expansion-r13`, `expansion-r14`, `expansion-r15`), `waveStored`, `chapterStored`, `journeyStored`, `expansionStored` |
| `wallet` | `coins` |
| `progression` | `era`, `ruleRevision`, six `families {family, absolute, local}`, `completedProjects`, era entry/transition markers, `completeUnderChapterRule`. A separate `legacyFields` block holds house/workshop/recycler/loft and `completeUnderR7Rule`, so an old save is never called complete under the new chapter rule. |
| `milestones` | Derived current-save facts from production `ChapterMilestones.Project`: `source: derived`, `scope: current-save-facts`, `constructionCount`, `totalConstructions: 24`, `reachedCount`, four stable `{id, reached}` entries, `nextAction` enum identity and preserved `selectedProjectId`. No lifetime history, dates or rewards. Earlier valid snapshots truthfully show earlier facts. |
| `optionalAchievements` | `source: derived`, `scope: current-save-facts`, two fixed `{id, reached}` facts from production `OptionalAchievements.Project`: `optional.all-skills-owned` and `optional.retained-order-claim`. False means absent in this current snapshot, not a locked UI label. No new count, reward, time, lifetime history or permanent badge. Ready-only/unclaimed orders do not establish collection; an earlier restored snapshot can lose either fact. |
| `selection` | `selectedProjectId`, saved-revision `selectedPrice`, `constructionPriceRevision` and its source, `currentOfferRevision`, `currentReselectionOfferPrice`, `affordableNow` (informational; nothing is bought), `legacySelectedStage` |
| `skills` | `ownershipCodecVersion` (2 = all-V1, 3 = explicit revisions), `receiptCount`, `ownershipTokenSha256`, and `receipts [{node, legacyReceipt, priceRevision, frozenPrice, sequence, operationId, expectedWallet, expectedOwnershipSha256}]`. Tokens are shown as 16-hex SHA-256 prefixes. |
| `orders` | `unlocked`; `active {orderId, metric, instanceSequence, progress, target, ready, rewardOnExplicitClaim, claimed:false}` or null; `cursor`; `lastCommandSequence`; `lastEventSequence`; `latestClaim {status: retained-historical-already-credited, ...}` or null; `journeyMarker` (adoption is reported only when stored) |
| `guidance` | `origin` (`Fresh`/`Existing`/`LegacyExpanded`), `acknowledged` hint ids, `tutorialSteps`, `lastObservedSequence`, `latestCompletedProject`, `completionAcknowledgedSequence`, `lastAcknowledgement`, `autoHintSeen` |
| `expansion` | Current Parts/Potatoes/Energy and caps, zero base-spin energy cost, five module ownership facts, three sockets, six equipment tracks, potato yield, six owned/applied palette facts, stored presentation speed, bonus progress/rolls/boost, payloadFormatVersion, customizationSource, bonusWorkbenchLevel, bonusFocus and retained resource/command receipts. Absent expansion and V1 customization project defaults without inventing history or adopting a field. State hashes identify canonical production encoding. |
| `arcade` | Current earned Diamonds/tickets/cadence/cap/progress, foreground spins, slot level and actual cumulative resource attributes from production's public `ArcadeView`. Absent V3 projects zero defaults without adopting or granting. `activeRun` distinguishes active-saved from ready-unpaid, including zero, and exposes only public masked Memory faces/current Sorting item. Hidden faces/current-item absence use-1; a nonready reward is null. No challenge seed, full board, transcript or future Sorting item is exported. `latestClaim` is retained historical already-credited, never payable again. `lastResourceReceipt`/`lastEvent` show gross Coin income separately from construction debit, actual total resource/currency credits, and slot components explicitly included in those totals. |
| `rngCarry` | `seed`, `deliveryCarry` with its source (stored or `default-zero-absent-field`), and `lastSpin {status: retained-historical-receipt, sequence, symbols, baseReward, workshopBonus, components, journey}`. `components` depend on the descriptor: legacy reward/bonus only; r7 wave base/workshop/slot/recycler/delivery/carry; or r8+ chapter base/workshop, baseline recycler/delivery, the three skills, four symbol sources, carry, selected price and completed project. |
| `pending` | `status`: `absent` or `present-validated-by-production`. When present: `kind`, `sequence`, `at`, `target`, `symbols`, `seedAfter`, `descriptors`, and the frozen chapter (price, coins before/after, skill request with its price revision, settlement, spin components), journey (kind, composed, instance, reward, coins) or wave context. A legacy descriptor states that its wallet effect is computed only by replay. |
| `clockSequence` | `sequence`, `revision`, `lastSettledAt`, `lastSettlementSeen` and the retained `lastSettlement`, as stored. Elapsed time is never settled against the current wall clock. |

`input {role, bytes, sha256, canonicalSha256, integrity {bytesAfter, sha256After, unchanged, adjacentEntriesUnchanged}}`. `canonicalSha256` hashes production's `Encode` of the decoded state; the encoding itself is never printed.

**Pending states.**

- *Absent*: `pending.status: absent`.
- *Valid*: `present-validated-by-production`.
- *Invalid*: production rejects the whole snapshot. The report has `status: snapshot-rejected`, exit 4, a bounded `error` and a `pendingFieldPresentHint` (true, false or unknown, from the raw document). By design it carries no partial facts.

Dedicated Arcade pending operations expose their frozen action/id/sequence/instance/cursor/phase/target and costs/credits, without the hidden challenge board. Their wallet authority is `dedicated-frozen-arcade-command`, with their actual frozen unchanged Coin wallet. Dedicated resource-shop commands retain `dedicated-frozen-expansion-command`; composed spins retain `lower-frozen-command` and null wallet placeholders. The current `arcade` group never substitutes the pending after-state for confirmed facts.

### `diff` (`status: ok`)

| Field | Meaning |
|---|---|
| `equal` | Canonical production encodings are identical. Formatting, whitespace, BOM and escapes that production accepts do not count. |
| `changeCount`, `groupsChanged` | Size and groups of the change set |
| `observed` | Sequence and wallet deltas |
| `inferred` | Explanations: clock-only, order claim, new order instance, cancellation, pending commit, aggregate adoption, sequence regression |
| `computed` | Checks such as `walletDeltaEqualsNewClaimReward` and `afterEqualsInMemoryReplayOfBeforePending` |
| `changes [{group, path, before, after}]` | Individual changes |
| `caveats` | Limits of the inferences |
| `inputs` | Two input blocks |

A clock/sequence-only change produces `groupsChanged: [clockSequence]` and no income wording. Inferences are labelled as such: two snapshots prove neither the UI action nor an unmodified history. The tool never merges, repairs or rolls back.

Retained modern spin facts include the frozen chapter `at` timestamp. If canonical state differs
but the displayed projection cannot explain it, a `format.unexplainedCanonicalDifference` hash
entry makes the difference explicit. The clock-only inference is additionally checked against
the complete canonical root fields, so an omitted receipt difference cannot be labelled clock-only.

Self-test validates every existing workspace ancestor before creating directories, then validates
the created workspace before writing fixtures. A missing leaf beneath a junction/symlink is refused.
The five focused fix regressions can run independently without rerunning the original45-case suite:

```powershell
dotnet build tools/SaveInspector/tests/FixChecks.csproj -c Release --disable-build-servers -nodeReuse:false
dotnet .work/save-inspector-fix-checks/bin/Release/net10.0/SaveInspector.FixChecks.dll <repo-root> <new-private-workspace-outside-repo>
```

The standard self-test also includes those five groups plus five current-source groups (55 cases total). They use private link,
replacement and growth fixtures only; no normal game save or production source is modified.

The focused1.6 Arcade checks use the same test executable without rerunning those historical campaigns:

```powershell
dotnet build Tools/SaveInspector/tests/FixChecks.csproj -c Release --disable-build-servers -nodeReuse:false
dotnet .work/save-inspector-fix-checks/bin/Release/net10.0/SaveInspector.FixChecks.dll X:/PROJECTS/FINAL-SVALKA X:/tmp/new-empty-inspector-arcade-workspace arcade
```

This mode checks nine committed V3 producer exports, V1/V2 defaults and one genuine NEW public spin reservation. Fourteen inspect/preview/diff calls require exact copied-file hashes, modification ticks and inventory. It asserts masked cards, unpaid zero/positive Ready, retained claims, actual slot attributes, the saved540 debit, dedicated versus composed wallet authority and canonical replay equality. It does not replay the game UI, perform filesystem recovery or qualify a device. The workspace must be new and outside the repository.

### `preview-pending`

Version1.6 adds `arcade {before, computedAfter}` using the same public masked extractor. The latter is only an in-memory projection, not a committed grant. `resultMatchesFrozenArcade` is null for historical non-V3 operations; for V3 it reports the existing full frozen Expansion-after comparison, which includes Arcade. No second replay, reward formula, storage call or clock is introduced. Retained claims are explicitly already credited; recovery output never becomes a new reward presentation.

`status: replayed-in-memory` (exit 0) reports:

- `operation`: kind, sequence, coins before/after, `walletDelta`, seed before/after, delivery carry before/after, and whether the frozen chapter/journey `coinsAfter` matches.
- `checks`:
  - `resultPassesProductionPersistValidation`: the in-memory mirror of `GameStore.Write`, i.e. validate, encode, decode, validate.
  - `decodedBeforeStateUnchanged`
  - `resultHasNoPendingAndCommitsFrozenSequence`
- `milestones {before, after}`: the same derived current-save group for the decoded pending snapshot and its in-memory committed result.
- `optionalAchievements {before, computedAfter}`: facts from the decoded pending before-state and the explicitly computed in-memory result. `computedAfter` is hypothetical until real journal recovery commits it; a valid pending snapshot does not mark its unpaid claim/unowned skill as already earned. Invalid pending snapshots still reject before any successful facts.
- `afterCanonicalSha256`, `groupsChanged`, `changes`, `notes`.

No replacement save is produced. The other outcomes are:

- `not-applicable` (exit 5): the snapshot is committed, and its `lastSpin` is described as a retained receipt, not a grant.
- `replay-refused` or `replay-inconsistent` (exit 6).
- `snapshot-rejected` (exit 4): an invalid frozen operation. It is never repaired.

Version1.4 also reports resource balances before/after pending recovery and checks the exact frozen expansion state and primary seed. A dedicated expansion command owns its frozen wallet; a composed spin delegates wallet authority to its lower frozen chapter/journey command. Expansion's placeholder wallet values on composed operations are reported as null, never as an additional debit or payout. A retained bonus receipt is historical and its resource totals already include its bonus breakdown. Previewing never grants or writes those resources.

Version1.5 adds frozen rule/format/workbench/focus before-after metadata. The full canonical After comparison includes all V2 fields; V1 pending replays retain their original canonical hashes. Inspection does not itself upgradeV1, execute a module set or choose a focus. In-memory preview is still separate from real journal recovery and native filesystem durability.

## Limitations

- An in-memory replay of `GameLogic.ApplyPending` does not establish Android filesystem durability or real recovery success. `GameStore.Open` does more that is not modelled: backup validation, interrupted-write quarantine and recovery-evidence blocking.
- An invalid snapshot yields no partial facts. Production rejects the whole document, and the tool does not salvage it.
- Hard links to a file elsewhere cannot be detected cheaply and are not refused. Symbolic links and junctions are refused.
- Inferred diff explanations are not proof of a UI action or of an untampered history.
- Facts are only what the linked production decoder validates. Skill price V1/V2/V3 and construction price V1/V2/V3 are separate histories; unsupported revisions are rejected by production.
- The tool is not integrated into Unity, the runtime or the diagnostics module.

## Self-test

`self-test` copies 10 literal repository fixtures into `<workspace>/inputs` and hashes the originals before and after. It generates snapshots with current production pure `Reserve`/`ApplyPending` functions (no storage implementation), writes synthetic malformed/reformatted copies, and runs45 original cases,5 boundary/diff fix groups and7 current-source groups plus6 milestone groups and6 optional-fact groups (69 total). Current constructionV3 totals9310, so the independently funded10000 chapter leaves690; the historical9460/540 and9400/600 results remain in their original receipts. Literal constructionV1/600 andV2/540 fixtures are unchanged. Expected values are literal rule/fixture arithmetic and the seed-1 trace/trash progression in `JourneyEconomyRegression.cs`; they are never derived from current production price queries or this tool's output.

The seven current groups can run alone using the existing focused test executable:

```
dotnet build tools/SaveInspector/tests/FixChecks.csproj -c Release --disable-build-servers -nodeReuse:false
.work/save-inspector-fix-checks/bin/Release/net10.0/SaveInspector.FixChecks.exe <repository-root> <new-private-workspace> r11
```

The historical `r11` argument remains compatible and now selects the current-source suite. The groups verify the independently reconstructed build fingerprint; literal600 versus current480; an unfunded470 selection and semantic600->480/V1->V3 diff; funded500 immediate payment leaving20; frozen550-480=70 and committed refusal; unchanged literalV2 selected540, frozen550-540=10 and paid refusal; and literal595+9-600=4. Original generated `r11-*` filenames in the main harness remain stable for compatibility, while their facts and arithmetic reflect the linked current source. Format `journey-r9` names the journey aggregate schema, not the construction price or build revision.

Every original harness CLI invocation runs twice in both formats and is checked in three ways:

- byte-identical output and exit code
- ASCII-only output
- every file in the workspace keeps the same bytes, and no file is added or removed

The workspace must be empty or absent, and outside the repository.

The seven current-source groups run fourteen additional CLI calls per execution with independent expectations and before/after input hashes. They are outside the original `cliRuns` counter, which remains198 for the deterministic harness. `evidence/r11/` remains historical evidence of its original source and literal540 snapshots; a current run does not relabel that evidence.

Milestone facts are independent of wallet and current offer prices. Old selected600/540 still use their saved price and affordability in selection; InspectSelectedProject merely identifies the next action. A free era entry remains12/24. The6 milestone groups cover house-only legacy state, free entry/acknowledgement, final23->24 real command/preview/diff, old540 affordability, earlier-snapshot regression and unchanged canonical/clock-only semantics. Their13 passive CLI calls verify input hashes/inventory and are outside the original198 counter. The inspector calls the pure helper directly; it does not instantiate storage/session or mutate/repair snapshots.

The six optional-fact groups reuse the original main harness's existing funded completed-chapter input. Actual pure purchase/accept/spin/claim/cancel commands create legitimate five/six-skill, ready/paid/new-instance and mixed states; no achievement or receipt field is fabricated. They cover historical six paid skill receipts, pending-before versus computed-after labels, old snapshot loss, canonical equality/clock-only grouping and one strict private malformed-pending rejection. Every passive command checks full private input bytes/inventory/mtime. These calls are reported per optional group and are outside the original198 deterministic-call counter. No GameStore/storage session is instantiated by the added projection/tests.
