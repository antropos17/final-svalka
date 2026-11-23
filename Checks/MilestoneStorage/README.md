# Milestone queries at the private journal failure boundary

This narrowly scoped Windows/NTFS check uses the accepted late23/skills6 fixture and actual FileSnapshotStorage/GameStore/GameSession/JsonStateCodec. It adds only two cases: denied final700 reservation replacement, and successful reservation followed by denied final commit replacement. It does not rerun the four older physical storage groups.

Run from an explicit source-only X: checkout with current48 pure sources and `Checks/Milestones/fixtures/late23-skills6.json`. No Library, Editor, ordinary save directory or Android build is needed. Per-process TEMP/TMP must point to an owned X: task directory. Bin/obj go to `.work/milestone-storage-checks/`.

```powershell
dotnet build Checks/MilestoneStorage/MilestoneStorageChecks.csproj -c Release --disable-build-servers -nodeReuse:false
.work/milestone-storage-checks/bin/Release/net10.0/MilestoneStorageChecks.exe --workspace <absolute-source-only-X-checkout>
```

The executable requires its current working directory to equal that explicit checkout. Never pass the ordinary game-save directory. Private case output is generated only in `.work/milestone-storage/run-<guid>`. Reparse points, unexpected filenames, excessive bytes and more than three retained runs are refused. No cleanup is automatic: retain first/final receipts; disposable bin/obj/TEMP may be removed only after review, in the validated exact owned root. There is no continuing/background producer.

AuditedStorage reuses the existing physical wrapper and deny-delete read-handle pattern from StorageIntegration. All actual read/write/quarantine/flush/replace behavior delegates to production FileSnapshotStorage. The only added observation counts read calls, permitting explicit zero-read/zero-write assertions around blocked Milestones getters. PrivateFiles is copied with a new scoped run group/project marker and permits a source-only .git directory or worktree .git file; existing bounds/path/reparse guards remain.

Independent fixture arithmetic:10657 coins/sequence34/seed2647435461,23 purchases, six skills. The final power-source purchase costs700, so only a committed/recovered operation may yield9957/sequence35/24 purchases and all four current facts. Reservation rejection preserves10657/34/23. Seed, old spin34 and six skill receipts remain. No wall clock is called and no historical fixture field is rewritten.

Both uncertain sessions must reject three Milestones queries with zero storage read/write/preserve delta and unchanged error, full encoded state and file inventory. A normal reopen preserves interrupted output through the existing supported quarantine. Committed recovery must match exact applied temp bytes, retain the frozen reservation as backup and preserve interrupted evidence. Three further recovered reopens/nine passive queries cannot charge/write or change any history again.

The receipt records actual source/fixture/harness/executed-assembly hashes, exact filename/hash/mtime inventories, sharing error32, two case results and original failures if any. Windows sharing-failure simulation does not establish Android, power-loss, OS termination, physical-device or filesystem durability acceptance.