# Era migration fixtures and replay checks

03 owns this directory only; production Core/runtime/codec and other check projects are untouched. Fixture inputs are literal historical JSON, not current-serializer reconstructions. No Unity Editor, OS lifecycle, real progress or primary test output is used.

## Run the executable historical gate

```powershell
$env:TEMP='X:/tmp/jtn-era-migration03/temp'; $env:TMP=$env:TEMP
dotnet run --project Checks/EraMigration/EraMigrationChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-era-migration03/checks-obj/ -p:BaseOutputPath=X:/tmp/jtn-era-migration03/checks-bin/
```

The five groups check all33 historical fixtures, independent numeric outcomes, failed recovery before/after durable write, strict r7 descriptor completeness and exact carry/boost/offline checkpoints. Pending operations are actually opened through GameStore and ISnapshotStorage faults. Expected numeric values are hand-calculated constants; they are not produced by ApplyPending in the test. The separate round-trip group is structural evidence, not its own expected outcome. Fixtures are synthetic private states emitted by the actual historical codec, not copied player progress.

To check02's new shared DTO/identity contract, pass `-p:ChapterContractPath=<absolute path to immutable ChapterContracts.cs>`. That file must not already be included by ProductionRoot's Contracts wildcard. This optional check validates only independent expected projection data and the published shared types; it deliberately prints that actual migration is untested. `ProjectionContractChecks.AssertProjection` is a reusable assertion for the real API adapter once02 publishes it. ProjectionExpectations.json never specifies the future save's JSON shape.

For a frozen later production source snapshot use `-p:ProductionRoot=<snapshot root>`; preserve baseline fixture JSON/provenance. If the new model removes old public fields, add a NEW adapter in this owned directory after reading the actual API. Do not weaken assertions or rewrite old fixtures to make the new model pass. No Reflection fallback or guessed future state format is used.

## Reproduce historical bytes

```powershell
./Checks/EraMigration/GenerateLegacy.ps1
```

The generator extracts pinned r6=8903237 and r7=f5bfc34 pure source from Git to a unique X: run folder, including historical codec and dependencies. Each pinned executable emits its own snapshots. Provenance records exact revisions, source Git blob IDs, generator SHA256 and fixture byte SHA256. Do not regenerate by linking current production source or by serializing a migrated model.

Disposable generation/build output stays below X:/tmp/jtn-era-migration03. Retain for seven days or until integration receipts are accepted, then delete only closed files in this exact owned directory after checking absolute paths; skip links and locked files. No automatic cleanup/rotation is installed. Committed fixture inputs and receipts are durable evidence and are not disposable. Existing shared Unity Library/Logs are not opened or changed by this task.

## Pending actual integration cases

Historical phase1 status below is superseded by the actual Core qualification described in the final section. Keep the old fixture/provenance bytes unchanged.

New era unlock, era-stage purchase and skill purchase require02's actual operation API and strict format2 codec. Replay must be checked at the existing two-write boundary: failure before first write leaves the old state; durable first write freezes one result; failure before/after second write blocks then recovers that result once. Repeat reopen, duplicate sequence and clock/foreground checks after each. A modern format2 command missing its descriptor must fail closed. Old r6/r7 pending results remain pinned even when the migrated state owns new sources or skill tiers. Chapter completion remains possible with all optional skills unowned. These cases are NOT passing until exercised against that implementation.

ReplayFaultMatrix.Verify accepts the real `Action<GameSession,long>` and independently authored before/after assertions. Its four write-fault positions and duplicate/reload assertions are executable now and verified against BuySlotUpgrade; reuse this driver with02's actual SelectChapterProject/BuySkill/EnterWorkshopYard/AcknowledgeEraTransition methods. It never derives expected reward or ownership from the command result. A temporary JSON-node pre-command view clears only pending/revision from the literal slot fixture; the committed old fixture remains byte-identical.

## Optional actual Content projection check

ChapterContentMigrationChecks exercises04's actual ChapterRules.ProjectR7, Stages/FromStages and era proposal API. It passed ten literal-fixture projection cases against a read-only, SHA256-frozen Content snapshot, with Contracts fixed at6fc9ee2. All tested Content C# hashes were then verified identical to04's immutable284babd commit. This establishes the pure Content projection, not02's pending save/skill/replay integration. Its exact module source hashes and commit are in verification.json.

```powershell
dotnet run --project Checks/EraMigration/EraMigrationChecks.csproj -p:EnableChapterContentChecks=true -p:ContentSourceRoot=X:/tmp/jtn-era-migration03/content-snapshot -p:ChapterContractPath=X:/tmp/jtn-era-migration03/published-contract/ChapterContracts.cs -p:ChapterSymbolContractPath=X:/tmp/jtn-era-migration03/published-contract/ChapterSymbolModifiers.cs -p:BaseIntermediateOutputPath=X:/tmp/jtn-era-migration03/content-checks-obj/ -p:BaseOutputPath=X:/tmp/jtn-era-migration03/content-checks-bin/
```

After those modules are integrated, omit external contract paths and ContentSourceRoot; set EnableChapterContentChecks=true with ProductionRoot pointing at the frozen integrated source. That selects exact shared DTOs already present there and avoids duplicate definitions. Do not run against changing live source or write outputs into another owner's checkout.

## Actual Core qualification (phase2)

FreezeProduction.ps1 makes a read-only input snapshot of the five pure C# modules, checking the file set and SHA256 before/after copying. It never edits primary. Use a new exact X: output directory for every snapshot. Core source is qualified by the manifest's hashes and dirty-input list, not by HEAD alone.

```powershell
./Checks/EraMigration/FreezeProduction.ps1 -OutputRoot X:/tmp/jtn-era-migration03/phase2-source-2
dotnet run --project Checks/EraMigration/EraMigrationChecks.csproj -p:ProductionRoot=X:/tmp/jtn-era-migration03/phase2-source-2 -p:EnableChapterCoreChecks=true -p:EnableChapterContentChecks=true -p:BaseIntermediateOutputPath=X:/tmp/jtn-era-migration03/phase2-checks-obj/ -p:BaseOutputPath=X:/tmp/jtn-era-migration03/phase2-checks-bin/
```

Final result: five historical groups /439 counted assertions plus four actual modern Core groups /426 counted assertions. Portable replay-driver assertions are additional, not included in those counters. All33 literal fixtures use the actual Core Chapter projection, and the10 independent expected projections are also checked. Six modern commands each exercise four write-fault positions: spin, explicit construction purchase, skill purchase, era entry, transition acknowledgement and era2 stage purchase. These use the public GameSession methods, frozen ISnapshotStorage snapshots and independently specified wallet/RNG/stage/receipt expectations.

Checks include no eager promotion/grant, exact legacy boost purchase context without an invented historical SkillContext, one universal+1 on modern spins, selected offline-funded target preservation, carry2 through entry/ack, stale quote without writes, blocked foreground actions during transition, offline protocol during transition, resume of the committed presentation marker, idempotent acknowledgement, strict missing/null/unknown modern descriptors,19 frozen sources below65k, exact-three reels and unknown positive source rejection. A funded private fixture buys all24 stages through the real session, ends at540 from10000 without skills, then a manual spin yields565; no natural pacing claim follows.

Literal r7 four-family completion remains true before promotion. The same save promoted with cart/power unfinished is not modern chapter completion or automatic era entry. This negative case uses the owner's HouseGoal correction in source snapshot2. The first narrower source1 verdict is retained separately; it did not cover that predicate.

Limit: pre-confirmation cancellation is checked at the economic boundary by inspecting the pure offer and issuing no command. Skip/resume is checked through actual saved markers and AcknowledgeEraTransition. No UI button/coroutine, Unity Editor, OS backgrounding or physical-device acceptance is established by these standalone checks. Those remain02's integration scope. phase2-verification.json records exact frozen production inputs and final verdict.
