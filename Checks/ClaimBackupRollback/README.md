# Explicit backup rollback after an uncertain order claim

One scoped Windows/NTFS physical case. It links the accepted shared `../MilestoneStorage/AuditedStorage.cs` and reuses the two exact `../OptionalOrderStorage/fixtures/` snapshots. The accepted reservation/commit normal-reopen cases are not run or modified.

From an explicit source-only X: checkout, with per-process TEMP/TMP onX:

```powershell
dotnet build Checks/ClaimBackupRollback/ClaimBackupRollbackChecks.csproj -c Release --disable-build-servers -nodeReuse:false
.work/claim-backup-rollback-checks/bin/Release/net10.0/ClaimBackupRollbackChecks.exe --case claim-backup-rollback --workspace <absolute-source-only-X-checkout>
```

The selector accepts only this one case. Private output is `.work/claim-backup-rollback/run-<guid>`; existing Windows/X/cwd/path/reparse/file/byte/run-retention guards remain. The source-only checkout must include current50 pure sources, shared physical wrapper and accepted fixture files.

Initial valid primary is ready sorting1062coins/sequence43/seed2371448660/instance27/no retained claim; initial valid older backup is690/26/no order. A normal claim reservation durably replaces primary and rolls backup forward to the previous ready1062/43 primary. A deny-delete read handle then makes actual final File.Replace fail with sharing32, leaving valid pending1062 primary, ready1062 backup and fully applied1182 temp. The ACTUAL chosen backup after this journal boundary is1062, not the overwritten initial690; the receipt records both inventories.

Three blocked OptionalAchievements queries must reject with zero storage reads/writes/preserves/restores and exact unchanged state/error/files. After releasing the owned handle, call GameSession.RestoreBackup on this SAME blocked session BEFORE any normal Open. Otherwise Open would recover the pending claim instead of exercising intentional rollback.

Expected confirmed state is byte-exact validated chosen ready1062/43 snapshot, no retained claim/optional accomplishments/new120 or initial37 grant. Recovery-primary preserves the valid pending primary; recovery-backup preserves the chosen backup; recovery-pending preserves applied1182 temp. Normal temp, interrupted slot and restore temp must be absent. This establishes API/file rollback and separate-preservation semantics for a valid current file; it is not another UI-warning/corrupt-primary fixture.

Healthy repeat RestoreBackup must refuse. Three later Opens/passive queries keep exact chosen state and all evidence, without resurrecting staged44/reward. No durable claim is issued after rollback. One PURE/non-writing admissibility probe demonstrates that the identical44 claim can be a new proposal after history rollback; the schema does not retain lifetime anti-replay history outside the chosen snapshot. Do not label that a new durable replay grant or claim exactly-once across intentional rollback.

Only a new regression is added; no production codec/journal/Flush(true)/Replace/history/reward/permission or recovery rule changes. No deliberate corruption, process kill, Android, Inspector or Unity build is needed. Runtime receipt binds actual50 source hashes, fixtures, harness and executed assembly plus full filename/hash/mtime inventories. Preserve first failures and receipts; no automatic rerun/cleanup. Retain evidence, with disposable exact-root bin/obj/TEMP removable only after review while skipping locked/reparse files. Windows sharing simulation is not power-loss/device acceptance.