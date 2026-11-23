using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;

if (args.Length != 2 || args[0] != "--workspace") throw new ArgumentException("Use --workspace with the explicit isolated absolute X: checkout");
var files = new PrivateFiles(args[1]);
var codec = new JsonStateCodec();
var cases = new List<object>();
string currentCase = "initialization";
var sourceHashes = new Dictionary<string, string>();
foreach (string module in new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance" })
    foreach (string source in Directory.GetFiles(Path.Combine(files.Workspace, "Unity/Assets/Scripts", module), "*.cs"))
    {
        PrivateFiles.RejectReparse(source);
        sourceHashes.Add(Path.GetRelativePath(files.Workspace, source).Replace('\\', '/'), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
    }
string baselinePath=Path.Combine(files.Workspace,"Checks/StorageIntegration/verification.json");
PrivateFiles.RejectReparse(baselinePath);
using var baseline=JsonDocument.Parse(File.ReadAllText(baselinePath));
var baselineHashes=baseline.RootElement.GetProperty("sourceHashes");
var changedSourceFiles=sourceHashes.Where(pair=>!baselineHashes.TryGetProperty(pair.Key,out var original)||original.GetString()!=pair.Value).Select(pair=>pair.Key).OrderBy(value=>value).ToArray();
var harnessHashes=new Dictionary<string,string>();
foreach(string name in new[]{"Program.cs","AuditedStorage.cs","PrivateFiles.cs","StorageIntegrationChecks.csproj"})
    harnessHashes.Add(name,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(files.Workspace,"Checks/StorageIntegration",name)))));
void Need(bool value, string description) { if (!value) throw new Exception(description); }
string Raw(string directory, int index) { return File.ReadAllText(files.PathAt(directory, index)); }
bool Exists(string directory, int index) { return File.Exists(files.PathAt(directory, index)); }
bool SameInventory(Dictionary<string, string> a, Dictionary<string, string> b) { return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value); }
GameSession Open(AuditedStorage storage) { return GameSession.Open(new GameStore(storage, codec), 1, initializeJourney: true); }
object ExpectFileFailure(Action action)
{
    try { action(); }
    catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
    {
        Need(failure is IOException&&(failure.HResult&65535)==32,"Expected specifically private deny-delete sharing violation32, not another IO/access failure");
        return new { type = failure.GetType().Name, hresult = failure.HResult, win32Code = failure.HResult & 65535 };
    }
    throw new Exception("Expected private Windows deny-delete replacement failure");
}
void ExpectBlocked(Action action)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Blocked session accepted a second command");
}
void CheckSpin(ModelState state, long sequence, long coins, int house)
{
    Need(state.sequence == sequence && state.coins == coins && state.houseStage == house && state.pending == null, "Unexpected frozen spin/wallet/stage result");
    Need(state.seed == 2647435461u && state.lastSpin.sequence == sequence && state.lastSpin.reward == 8 && state.lastSpin.symbols.SequenceEqual(new[] { 0, 0, 2 }), "Independent seed1 expected [potato,potato,trash], pair8, seed2647435461");
    Need(state.journey != null && state.journey.Guidance.Origin == GuideOrigin.Fresh, "Modern Journey provenance lost");
}
void Receipt(bool passed, Exception failure = null)
{
    long fixtureBytes = files.CheckBudget();
    var receipt = new { status = passed ? "PASS" : "FAIL", sourceCommit = changedSourceFiles.Length==0?"061ad0eed23b54304dc05dc8b861d4d3ae949268":null,
        sourceBaselineCommit="061ad0eed23b54304dc05dc8b861d4d3ae949268",changedSourceFiles,harnessHashes,
        sourceIdentity=changedSourceFiles.Length==0?"Exact baseline source hashes":"Baseline plus explicit changed sources; exact sourceHashes below identify tested dirty source, not an unchanged-baseline claim",workspace = files.Workspace,
        privateRoot = files.Root, os = Environment.OSVersion.VersionString, filesystem = new DriveInfo(Path.GetPathRoot(files.Workspace)).DriveFormat,
        sourceHashes, cases, fixtureBytes, currentCase,
        failure = failure == null ? null : new { type = failure.GetType().Name, failure.HResult, description = failure.Message },
        scope = "Actual Windows FileSnapshotStorage/GameStore/JsonStateCodec; no process termination, power loss, Android or ordinary-save claim" };
    string raw = JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true });
    if (System.Text.Encoding.UTF8.GetByteCount(raw) > 65536) throw new Exception("Receipt budget exceeded");
    File.WriteAllText(files.Inside(Path.Combine(files.Root, "receipt.json")), raw);
}
try
{
    currentCase = "modern-journal-backup";
    string directory = files.Case(currentCase);
    using (var storage = new AuditedStorage(files, directory))
    {
        var game = Open(storage);
        Need(!game.RecoveryRequired && game.State.coins == 37 && game.State.sequence == 0, "Fresh private creation");
        game.SelectChapterProject(GameIds.HouseRepair, 1);
        game.ExecuteChapterSpin(2);
        CheckSpin(game.State, 2, 5, 1);
        Need(storage.Writes.Count == 5 && storage.WriteAttempts == 5 && storage.Writes[0].Backup == null, "Expected initial move plus two two-write commands");
        for (int i = 1; i < storage.Writes.Count; i++) Need(storage.Writes[i].Backup == storage.Writes[i - 1].Primary, "File.Replace backup not exact previous flushed snapshot");
        var backup = codec.Decode(Raw(directory, 1));
        Need(backup.sequence == 1 && backup.pending?.sequence == 2 && backup.pending.journey != null, "Final backup is not frozen modern pending spin2");
        Need(game.State.journey.Guidance.LastCompletion.ProjectId == GameIds.HouseRepair && game.State.journey.Guidance.LastCompletion.Sequence == 2, "Construction completion metadata lost");
        var before = files.Inventory(directory); int writes = storage.WriteAttempts;
        for (int i = 0; i < 3; i++) Need(!Open(storage).RecoveryRequired, "Committed reopen failed");
        Need(writes == storage.WriteAttempts && SameInventory(before, files.Inventory(directory)), "Repeated committed opens wrote or replaced files");
        cases.Add(new { name = currentCase, passed = true, writes = 5, finalCoins = 5, sequence = 2, exactPreviousBackups = true, inventory = before });
    }

    currentCase = "reservation-replace-denied";
    directory = files.Case(currentCase);
    using (var storage = new AuditedStorage(files, directory))
    {
        var game = Open(storage); string original = Raw(directory, 0);
        storage.HoldPrimary();
        object failure = ExpectFileFailure(() => game.ExecuteChapterSpin(1));
        Need(game.RecoveryRequired && Raw(directory, 0) == original && Exists(directory, 2), "Failed reservation modified primary or lost flushed temp");
        Need(!Exists(directory, 1) || Raw(directory, 1) == original, "Failed replacement produced unrelated backup");
        string reservation = Raw(directory, 2); var frozen = codec.Decode(reservation);
        Need(frozen.sequence == 0 && frozen.coins == 37 && frozen.pending?.sequence == 1 && frozen.pending.journey != null, "Expected fully flushed but unpromoted reservation");
        var blockedInventory = files.Inventory(directory); int attempts = storage.WriteAttempts;
        ExpectBlocked(() => game.ExecuteChapterSpin(1));
        Need(attempts == storage.WriteAttempts && SameInventory(blockedInventory, files.Inventory(directory)), "Blocked retry wrote another snapshot");
        storage.Release(); game = Open(storage);
        Need(!game.RecoveryRequired && game.State.coins == 37 && game.State.sequence == 0 && game.State.seed == 1 && game.State.lastSpin == null, "Unpromoted reservation was paid or RNG advanced on reopen");
        Need(!Exists(directory, 2) && Raw(directory, 3) == reservation, "Unpromoted reservation not quarantined byte-identically");
        game.ExecuteChapterSpin(1); CheckSpin(game.State, 1, 45, 0);
        cases.Add(new { name = currentCase, passed = true, expectedFailure = failure, blockedRetryWrites = 0, reopenCoins = 37, explicitNewSpinCoins = 45, unpromotedReservationPaid = false, inventory = files.Inventory(directory) });
    }

    currentCase = "commit-replace-denied";
    directory = files.Case(currentCase);
    using (var storage = new AuditedStorage(files, directory))
    {
        var game = Open(storage); storage.LockAfterNextWrite = true;
        object failure = ExpectFileFailure(() => game.ExecuteChapterSpin(1));
        Need(game.RecoveryRequired && storage.Writes.Count == 2, "Expected fresh plus successful reservation, failed applied promotion");
        string reservation = storage.Writes[1].Primary;
        Need(Raw(directory, 0) == reservation && codec.Decode(reservation).pending?.sequence == 1, "Applied replacement changed frozen reservation");
        string appliedTemp = Raw(directory, 2); CheckSpin(codec.Decode(appliedTemp), 1, 45, 0);
        var blockedInventory = files.Inventory(directory); int attempts = storage.WriteAttempts;
        ExpectBlocked(() => game.ExecuteChapterSpin(1));
        Need(attempts == storage.WriteAttempts && SameInventory(blockedInventory, files.Inventory(directory)), "Uncertain applied-write retry mutated files");
        storage.Release(); game = Open(storage); CheckSpin(game.State, 1, 45, 0);
        Need(!game.RecoveryRequired && Raw(directory, 3) == appliedTemp && Raw(directory, 0) == appliedTemp && Raw(directory, 1) == reservation, "Recovery changed frozen result/quarantine/backup bytes");
        var afterRecovery = files.Inventory(directory); int recoveredWrites = storage.WriteAttempts;
        for (int i = 0; i < 3; i++)
        {
            game = Open(storage); CheckSpin(game.State, 1, 45, 0);
            game.ExecuteChapterSpin(1); CheckSpin(game.State, 1, 45, 0);
        }
        Need(recoveredWrites == storage.WriteAttempts && SameInventory(afterRecovery, files.Inventory(directory)), "Repeated journal recovery/replay wrote or paid again");
        cases.Add(new { name = currentCase, passed = true, expectedFailure = failure, finalCoins = 45, reward = 8, repeatedReopens = 3, replayWrites = 0, appliedOrphanPreserved = true, inventory = afterRecovery });
    }

    currentCase = "cancel-then-explicit-modern-restore";
    directory = files.Case(currentCase);
    using (var storage = new AuditedStorage(files, directory))
    {
        var setup = Open(storage); setup.SelectChapterProject(GameIds.HouseRepair, 1);
        var pending = JourneyEconomy.ReserveChapter(setup.State, "spin", null, 2, 0, null, true, 0);
        string backup = codec.Encode(pending);
        files.Put(directory, 0, "{private malformed primary"); files.Put(directory, 1, backup); files.Put(directory, 2, "{private interrupted write");
        var before = files.Inventory(directory); int writes = storage.WriteAttempts, restores = storage.RestoreAttempts;
        var game = Open(storage);
        Need(game.RecoveryRequired && game.BackupAvailable, "Malformed primary did not expose validated modern backup");
        // A cancel is intentionally NO RestoreBackup call. Only pure recovery
        // flags are inspected here; actual pointer/Back cancellation is reused UI evidence.
        Need(game.BackupAvailable && game.RecoveryRequired, "Recovery query changed availability");
        game = Open(storage);
        Need(game.RecoveryRequired && SameInventory(before, files.Inventory(directory)) && writes == storage.WriteAttempts && restores == storage.RestoreAttempts, "Inspection/cancellation or retry-load wrote files");
        var restored = game.RestoreBackup(); CheckSpin(restored, 2, 5, 1);
        Need(!game.RecoveryRequired && Raw(directory, 4) == "{private malformed primary" && Raw(directory, 5) == backup && Raw(directory, 6) == "{private interrupted write", "Explicit modern restore lost original evidence");
        Need(Raw(directory, 1) == backup && !Exists(directory, 2) && !Exists(directory, 7), "Restore modified backup or left a live temp");
        Need(restored.journey.Guidance.LastCompletion.ProjectId == GameIds.HouseRepair && restored.journey.Guidance.LastCompletion.Sequence == 2, "Pending modern guidance completion not restored");
        var after = files.Inventory(directory); int afterWrites = storage.WriteAttempts;
        for (int i = 0; i < 3; i++) { game = Open(storage); game.ExecuteChapterSpin(2); CheckSpin(game.State, 2, 5, 1); }
        Need(afterWrites == storage.WriteAttempts && SameInventory(after, files.Inventory(directory)), "Restored backup repeated pending spin across reload");
        cases.Add(new { name = currentCase, passed = true, cancelWrites = 0, cancelRestoreCalls = 0, explicitRestoreCalls = 1, finalCoins = 5, sequence = 2, originalEvidencePreserved = true, inventory = after });
    }

    currentCase = "complete";
    foreach (var source in sourceHashes)
        Need(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(files.Workspace, source.Key)))) == source.Value, "Production source changed during scoped run");
    Receipt(true);
    foreach (object result in cases) Console.WriteLine("PASS " + JsonSerializer.Serialize(result));
    Console.WriteLine("PASS 4 real-file storage groups; private receipt " + Path.Combine(files.Root, "receipt.json"));
}
catch (Exception failure)
{
    Receipt(false, failure); Console.Error.WriteLine("FAIL " + currentCase + ": " + failure.Message + "; originals preserved at " + files.Root); throw;
}
