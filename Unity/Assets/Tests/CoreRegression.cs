using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class CoreRegression
    {
        sealed class MemoryStorage : ISnapshotStorage
        {
            public string primary, backup; public int writes, failAt;
            public string ReadPrimary() { return primary; }
            public string ReadBackup() { return backup; }
            public void Write(string raw) { writes++; if (writes == failAt) throw new IOException("Simulated interrupted write"); backup = primary; primary = raw; }
        }
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Fails(Action action, string message) { try { action(); } catch { return; } throw new Exception(message); }
        static GameSession Open(MemoryStorage storage) { return GameSession.Open(new GameStore(storage, Codec), 123456u); }
        public static IReadOnlyList<string> Run(string fileTestDirectory)
        {
            var passed = new List<string>();
            var counts = new Dictionary<int, int> { { 23, 0 }, { 8, 0 }, { 3, 0 } };
            foreach (var a in EconomyRules.Symbols) foreach (var b in EconomyRules.Symbols) foreach (var c in EconomyRules.Symbols) counts[GameLogic.Payout(new[] { a, b, c })]++;
            Require(counts[23] == 4 && counts[8] == 36 && counts[3] == 24, "64-outcome payout distribution");
            Fails(() => GameLogic.Payout(new[] { 0, 0, 9 }), "Unknown symbol accepted");
            passed.Add("64 outcomes: 4 triples, 36 pairs, 24 different; exclusive payouts");

            var storage = new MemoryStorage(); var game = Open(storage);
            Require(!game.RecoveryRequired && game.State.coins == 37 && storage.writes == 1, "Fresh save grant");
            game = Open(storage); Require(game.State.coins == 37 && storage.writes == 1, "Grant repeated on restart");
            var selection = game.SelectProject(1, 1);
            Require(selection.coins == 37 && selection.houseStage == 0 && selection.selectedStage == 1, "Selection spent before funds");
            var result = game.ExecuteSpin(2); var reward = result.lastSpin.reward;
            Require(result.coins == 37 + reward - 40 && result.houseStage == 1 && result.selectedStage == 0, "First spin repair or surplus wrong");
            var writes = storage.writes; var before = Codec.Encode(result);
            Require(Codec.Encode(game.ExecuteSpin(2)) == before && Codec.Encode(game.SelectProject(2, 1)) == before && storage.writes == writes, "Duplicate/stale command changed state");
            Fails(() => game.ExecuteSpin(4), "Sequence gap accepted");
            Require(!game.RecoveryRequired && storage.writes == writes, "Rejected pre-write command changed store");
            result.coins = 9999; Require(game.State.coins != 9999, "State exposed mutable reference");
            passed.Add("Fresh grant once, explicit first repair, surplus, duplicate/stale/gap commands, defensive state");

            foreach (var balance in new[] { 40L, 57L, 600L, 617L })
            {
                var state = ModelState.Fresh(7); state.coins = balance;
                if (balance >= 600) state.houseStage = 1;
                var target = state.houseStage + 1;
                var purchased = GameLogic.ApplyPending(GameLogic.Reserve(state, "select", target, 1));
                Require(purchased.coins == (balance == 40 || balance == 600 ? 0 : 17) && purchased.houseStage == target && purchased.selectedStage == 0, "Exact/surplus project purchase");
            }
            var unselected = ModelState.Fresh(7); unselected.coins = 1000;
            var spinOnly = GameLogic.ApplyPending(GameLogic.Reserve(unselected, "spin", 0, 1));
            Require(spinOnly.houseStage == 0 && spinOnly.coins > 1000, "Project auto-selected");
            passed.Add("Both purchases with exact funds/overflow; no automatic project/successor");

            storage = new MemoryStorage(); game = Open(storage); storage.failAt = 3;
            Fails(() => game.ExecuteSpin(1), "Injected interruption ignored");
            Require(game.RecoveryRequired && game.State.pending != null && game.State.coins == 37, "Failed save presented reward");
            var frozen = Codec.Decode(storage.primary).pending; var frozenReward = GameLogic.Payout(frozen.symbols);
            Fails(() => game.ExecuteSpin(1), "Blocked session retried mutation");
            storage.failAt = 0; game = Open(storage);
            Require(!game.RecoveryRequired && game.State.pending == null && game.State.coins == 37 + frozenReward && game.State.seed == frozen.seedAfter, "Pending result not recovered once");
            var recoveredRaw = storage.primary; game = Open(storage);
            Require(storage.primary == recoveredRaw && game.State.coins == 37 + frozenReward, "Restart repeated applied reward");
            game.ExecuteSpin(1); Require(storage.primary == recoveredRaw, "Recovered sequence paid again");
            passed.Add("Interruption between reserve/apply, frozen RNG recovery, failure blocks retry, restart after commit");

            storage = new MemoryStorage(); game = Open(storage); game.SelectProject(1, 1); storage.failAt = 5;
            Fails(() => game.ExecuteSpin(2), "Repair interruption ignored"); storage.failAt = 0; game = Open(storage);
            Require(game.State.houseStage == 1 && game.State.coins == 37 + game.State.lastSpin.reward - 40, "Repair recovery wrong");
            var once = storage.primary; game = Open(storage); Require(storage.primary == once, "Repair repeated");
            storage = new MemoryStorage(); game = Open(storage); storage.failAt = 3;
            Fails(() => game.SelectProject(1, 1), "Selection interruption ignored"); storage.failAt = 0; game = Open(storage);
            Require(game.State.sequence == 1 && game.State.selectedStage == 1 && game.State.coins == 37, "Pending selection recovery wrong");
            storage = new MemoryStorage(); game = Open(storage);
            for (var i = 1; i <= 1000; i++) game.ExecuteSpin(i);
            writes = storage.writes; before = storage.primary; game.ExecuteSpin(1);
            Require(writes == storage.writes && before == storage.primary, "Old operation paid after receipt history");
            passed.Add("Purchase recovered once; monotonic duplicate suppression after 1000 operations");

            var clean = Codec.Encode(ModelState.Fresh(9));
            var corruptions = new[] {
                clean.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                clean.Replace("\"contentVersion\":1", "\"contentVersion\":2"),
                clean.Replace("\"coins\":37", "\"coins\":-1"),
                clean.Replace("\"revision\":0", "\"revision\":3"),
                clean.Replace("\"coins\":37,", ""),
                clean.Replace("\"seed\":9", "\"seed\":0"),
                clean.Replace("\"coins\":37", "\"coins\":37,\"coins\":37"),
                clean.Replace("\"coins\":37", "\"coins\":37.5"), clean + " trailing", "{}", "null"
            };
            foreach (var bad in corruptions)
            {
                storage = new MemoryStorage { primary = bad, backup = clean }; game = Open(storage);
                Require(game.RecoveryRequired && storage.primary == bad && storage.backup == clean && storage.writes == 0 && game.BackupAvailable, "Malformed save mutated or accepted");
            }
            var pendingState = GameLogic.Reserve(ModelState.Fresh(9), "spin", 0, 1);
            var validPending = Codec.Encode(pendingState);
            storage = new MemoryStorage { primary = validPending.Replace("\"seedAfter\":" + pendingState.pending.seedAfter, "\"seedAfter\":1"), backup = clean };
            game = Open(storage); Require(game.RecoveryRequired && storage.writes == 0, "Forged pending accepted");
            storage = new MemoryStorage { primary = validPending.Replace("\"sequence\":1", "\"sequence\":2"), backup = clean };
            game = Open(storage); Require(game.RecoveryRequired && storage.writes == 0, "Pending sequence gap accepted");
            var appliedSpin = GameLogic.ApplyPending(pendingState);
            storage = new MemoryStorage { primary = Codec.Encode(appliedSpin).Replace("\"reward\":" + appliedSpin.lastSpin.reward, "\"reward\":999"), backup = clean };
            game = Open(storage); Require(game.RecoveryRequired && storage.writes == 0, "Forged reward receipt accepted");
            storage = new MemoryStorage { primary = clean.Replace("\"selectedStage\":0", "\"selectedStage\":2"), backup = clean };
            game = Open(storage); Require(game.RecoveryRequired && storage.writes == 0, "Invalid selected stage accepted");
            storage = new MemoryStorage { backup = clean }; game = Open(storage);
            Require(game.RecoveryRequired && storage.primary == null && storage.writes == 0, "Missing primary created fresh grant despite backup");
            var max = ModelState.Fresh(7); max.coins = long.MaxValue;
            Fails(() => GameLogic.ApplyPending(GameLogic.Reserve(max, "spin", 0, 1)), "Wallet overflow accepted");
            passed.Add("Malformed schema/content/wallet/shape/sequences/results preserved; missing primary and overflow fail closed");

            var goalState = ModelState.Fresh(12);
            Require(HouseGoal.Marker(goalState) == "SELECT: REPAIR · 37 / 40", "Fresh goal marker");
            goalState = GameLogic.ApplyPending(GameLogic.Reserve(goalState, "select", 1, 1));
            Require(HouseGoal.Marker(Codec.Decode(Codec.Encode(goalState))) == "IN PROGRESS: REPAIR · 37 / 40", "Selected marker reload");
            goalState = GameLogic.ApplyPending(GameLogic.Reserve(goalState, "spin", 0, 2));
            Require(goalState.houseStage == 1 && goalState.selectedStage == 0 && HouseGoal.Marker(goalState).StartsWith("SELECT: HOUSE UPGRADE · "), "First purchase silently selected successor");
            goalState.coins = 617;
            Require(HouseGoal.Marker(goalState) == "SELECT: HOUSE UPGRADE · 600 / 600", "Affordable goal marker");
            goalState = GameLogic.ApplyPending(GameLogic.Reserve(goalState, "select", 2, 3));
            goalState = Codec.Decode(Codec.Encode(goalState));
            Require(goalState.coins == 17 && goalState.selectedStage == 0 && !HouseGoal.Complete(goalState) && HouseGoal.Marker(goalState) == "HOUSE COMPLETE · CHOOSE ANOTHER BUILDING", "House completion reload or overflow");
            Fails(() => GameLogic.Reserve(goalState, "select", 6, 4), "Unknown successor accepted");
            passed.Add("Goal markers follow explicit selection, both purchases and reload; persistent completion with no successor");

            var legacy = Codec.Encode(ModelState.Fresh(9)).Replace(",\"autoHintSeen\":0", "");
            var legacyStageOne = legacy.Replace("\"houseStage\":0", "\"houseStage\":1");
            Require(!HouseGoal.NeedsAutoHint(Codec.Decode(legacy)), "Hint before first repair");
            storage = new MemoryStorage { primary = legacyStageOne }; game = Open(storage);
            Require(HouseGoal.NeedsAutoHint(game.State) && storage.writes == 0 && storage.primary == legacyStageOne, "Legacy save rewritten or hint lost");
            var hintResult = game.AcknowledgeAutoHint(1);
            Require(!HouseGoal.NeedsAutoHint(hintResult) && hintResult.coins == 37 && hintResult.seed == 9 && hintResult.houseStage == 1 && hintResult.selectedStage == 0 && hintResult.lastSpin == null, "Hint changed economy");
            writes = storage.writes; game.AcknowledgeAutoHint(1);
            Require(storage.writes == writes, "Duplicate hint wrote again");
            game = Open(storage); Require(!HouseGoal.NeedsAutoHint(game.State), "Acknowledged hint repeated on reload");
            Fails(() => game.AcknowledgeAutoHint(2), "Hint acknowledged twice with new sequence");
            storage = new MemoryStorage { primary = legacyStageOne, failAt = 2 }; game = Open(storage);
            Fails(() => game.AcknowledgeAutoHint(1), "Failed hint save accepted");
            Require(game.RecoveryRequired, "Hint storage failure did not block commands");
            game = Open(storage); Require(!game.RecoveryRequired && game.State.sequence == 1 && !HouseGoal.NeedsAutoHint(game.State) && game.State.coins == 37, "Frozen hint recovery failed");
            foreach(var badHint in new[]{"null", "2", "-1", "\"yes\""}) Fails(() => Codec.Decode(legacyStageOne.Replace("\"seed\":9", "\"seed\":9,\"autoHintSeen\":" + badHint)), "Invalid hint accepted");
            Fails(() => Codec.Decode(legacy.Replace("\"seed\":9", "\"seed\":9,\"autoHintSeen\":1")), "Stage-zero acknowledgement accepted");
            passed.Add("Legacy saves, one-time AUTO acknowledgement, duplicate protection, failed-write recovery and strict optional field");

            Directory.CreateDirectory(fileTestDirectory);
            var files = new FileSnapshotStorage(fileTestDirectory);
            game = GameSession.Open(new GameStore(files, Codec), 44);
            Require(!game.RecoveryRequired && game.State.coins == 37, "File initial snapshot");
            game.ExecuteSpin(1); var saved = Codec.Encode(game.State);
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(!game.RecoveryRequired && Codec.Encode(game.State) == saved && File.Exists(Path.Combine(fileTestDirectory, "jtn.backup.v1.json")), "File save reload/backup");
            var primaryPath = Path.Combine(fileTestDirectory, "jtn.save.v1.json");
            var temporaryPath = Path.Combine(fileTestDirectory, "jtn.pending-write.v1.json");
            var interruptedPath = Path.Combine(fileTestDirectory, "jtn.interrupted-write.v1.json");
            passed.Add("Real file flush/replace/backup/reload");
            File.WriteAllText(temporaryPath, "uncertain interrupted write");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(!game.RecoveryRequired && File.ReadAllText(primaryPath) == saved && !File.Exists(temporaryPath) && File.ReadAllText(interruptedPath) == "uncertain interrupted write", "Committed primary orphan was not preserved");
            File.WriteAllText(temporaryPath, "uncertain interrupted write");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(!game.RecoveryRequired && !File.Exists(temporaryPath), "Identical orphan failed bounded deduplication");
            File.WriteAllText(temporaryPath, "different uncertain write");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(game.RecoveryRequired && File.ReadAllText(primaryPath) == saved && File.ReadAllText(temporaryPath) == "different uncertain write" && File.ReadAllText(interruptedPath) == "uncertain interrupted write", "Distinct preservation conflict overwrote evidence");
            File.Delete(temporaryPath); File.Delete(interruptedPath);
            var reservedOnDisk = GameLogic.Reserve(ModelState.Fresh(44), "spin", 0, 1);
            File.WriteAllText(primaryPath, Codec.Encode(reservedOnDisk));
            File.WriteAllText(temporaryPath, "{partial applied snapshot");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(!game.RecoveryRequired && game.State.sequence == 1 && game.State.coins == 37 + GameLogic.Payout(reservedOnDisk.pending.symbols) && game.State.seed == reservedOnDisk.pending.seedAfter && game.State.pending == null && File.ReadAllText(interruptedPath) == "{partial applied snapshot", "Pending primary plus partial applied write did not recover frozen result");
            var recoveredOnDisk = File.ReadAllText(primaryPath);
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(!game.RecoveryRequired && File.ReadAllText(primaryPath) == recoveredOnDisk, "Quarantined pending result applied twice");
            File.Delete(interruptedPath); File.Delete(primaryPath);
            File.WriteAllText(temporaryPath, "orphan without primary");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(game.RecoveryRequired && !File.Exists(primaryPath) && File.ReadAllText(temporaryPath) == "orphan without primary" && !File.Exists(interruptedPath), "Missing primary orphan permitted a fresh grant");
            File.WriteAllText(primaryPath, "malformed primary");
            game = GameSession.Open(new GameStore(new FileSnapshotStorage(fileTestDirectory), Codec), 99);
            Require(game.RecoveryRequired && File.ReadAllText(primaryPath) == "malformed primary" && File.ReadAllText(temporaryPath) == "orphan without primary" && !File.Exists(interruptedPath), "Malformed primary orphan was mutated");
            passed.Add("Validated-primary orphan quarantine, frozen pending recovery, bounded conflicts and malformed/missing-primary preservation");
            passed.AddRange(RecoveryRegression.Run(fileTestDirectory + "-recovery"));
            passed.AddRange(WorkshopRegression.Run());
            passed.AddRange(OfflineRegression.Run());
            passed.AddRange(WaveCatalogRegression.Run());
            passed.AddRange(WaveEconomyRegression.Run());
            passed.AddRange(ChapterEconomyRegression.Run());
            passed.AddRange(JourneyEconomyRegression.Run());
            passed.AddRange(StoreCodecBoundaryRegression.Run());
            passed.AddRange(BoundedStorageRegression.Run(Path.Combine(fileTestDirectory,"bounded-reader")));
            return passed;
        }
    }
}
