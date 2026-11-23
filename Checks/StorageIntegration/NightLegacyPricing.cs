using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Contracts;

internal static class NightLegacyPricing
{
    static readonly DateTimeOffset Deadline = new DateTimeOffset(2026, 10, 29, 13, 30, 12, TimeSpan.Zero);
    internal static void CheckDeadline() { if (DateTimeOffset.UtcNow >= Deadline) throw new InvalidOperationException("Overnight hard deadline reached; no new case may start"); }
    internal static void Run(string workspace)
    {
        CheckDeadline();
        var files = new PrivateFiles(workspace); // Dedicated NEW night worktree, never the full old producer directory.
        var codec = new JsonStateCodec();
        var results = new List<object>();
        var sourceHashes = new Dictionary<string, string>();
        foreach (string module in new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance" })
            foreach (string path in Directory.GetFiles(Path.Combine(files.Workspace, "Unity/Assets/Scripts", module), "*.cs"))
            {
                PrivateFiles.RejectReparse(path);
                sourceHashes.Add(Path.GetRelativePath(files.Workspace, path).Replace('\\', '/'), Hash(File.ReadAllBytes(path)));
            }
        string corpus = Path.Combine(files.Workspace, "Checks/EraMigration/Fixtures/r7");
        string pendingPath = Path.Combine(corpus, "pending-finish-house-spin.json");
        string paidPath = Path.Combine(corpus, "committed-finish-house-spin.json");
        PrivateFiles.RejectReparse(pendingPath); PrivateFiles.RejectReparse(paidPath);
        byte[] pendingBytes = File.ReadAllBytes(pendingPath), paidBytes = File.ReadAllBytes(paidPath);
        var pending = codec.Decode(System.Text.Encoding.UTF8.GetString(pendingBytes));
        Need(pending.coins == 595 && pending.sequence == 1 && pending.selectedStage == 2 && pending.pending.wave.Price == 600, "Literal selected/pending600 premise changed");
        // A deliberately conflicting new-offer branch is PRIVATE evidence only:
        // an explicit current reselection would pay540 from595 and leave55,
        // without spinning. Recovery must never choose that orphan over backup.
        var beforePending = pending.Copy(); beforePending.pending = null; beforePending.revision = 2;
        var offered = JourneyEconomy.ReserveChapter(beforePending, "select", GameIds.HouseImprove, 2);
        var currentCandidate = GameLogic.ApplyPending(offered);
        Need(currentCandidate.coins == 55 && currentCandidate.seed == 1 && currentCandidate.houseStage == 2 && currentCandidate.chapter.ConstructionPriceRevision == ConstructionPricing.RevisionV2, "Independent595-540=55 new-offer premise");
        byte[] candidateBytes = System.Text.Encoding.UTF8.GetBytes(codec.Encode(currentCandidate));
        string activeCase = "before-first-case";
        try
        {
            for (int caseIndex = 0; caseIndex < 2; caseIndex++)
            {
                CheckDeadline();
                bool pendingBackup = caseIndex == 0;
                activeCase = pendingBackup ? "selected-pending-sixhundred" : "already-paid-sixhundred";
                string directory = files.Case(activeCase);
                byte[] corrupt = System.Text.Encoding.UTF8.GetBytes("{private-night-corrupt-" + caseIndex);
                byte[] backup = pendingBackup ? pendingBytes : paidBytes;
                File.WriteAllBytes(files.PathAt(directory, 0), corrupt);
                File.WriteAllBytes(files.PathAt(directory, 1), backup);
                File.WriteAllBytes(files.PathAt(directory, 2), candidateBytes);
                using (var storage = new AuditedStorage(files, directory))
                {
                    var original = files.Inventory(directory);
                    var game = Open(storage, codec);
                    Need(game.RecoveryRequired && game.BackupAvailable && ChapterEconomy.ProjectPrice(GameIds.HouseImprove) == 540, "Restore not explicit or current offer no longer540");
                    // Cancel is no RestoreBackup call: read flags and reload only.
                    game = Open(storage, codec);
                    Need(game.RecoveryRequired && storage.WriteAttempts == 0 && storage.RestoreAttempts == 0 && Same(original, files.Inventory(directory)), "Cancel/retry-load changed original bytes or wrote state");
                    var restored = game.RestoreBackup();
                    CheckPaid(restored);
                    Need(!game.RecoveryRequired && storage.RestoreAttempts == 1 && storage.WriteAttempts == 0, "Explicit restore not isolated from command writes");
                    Need(Bytes(files.PathAt(directory, 4), corrupt) && Bytes(files.PathAt(directory, 5), backup) && Bytes(files.PathAt(directory, 6), candidateBytes), "Original primary/backup/new540 orphan bytes were not all preserved");
                    Need(Bytes(files.PathAt(directory, 1), backup) && !File.Exists(files.PathAt(directory, 2)), "Backup changed or conflicting new540 orphan stayed live");
                    var afterRestore = files.Inventory(directory);
                    for (int reopen = 0; reopen < 3; reopen++)
                    {
                        game = Open(storage, codec); CheckPaid(game.State);
                        game.ExecuteChapterSpin(2); CheckPaid(game.State);
                    }
                    Need(storage.WriteAttempts == 0 && Same(afterRestore, files.Inventory(directory)), "Restored paid600 replay rewrote/recharged/refunded state");
                    Need(game.Chapter.ConstructionPriceRevision == ConstructionPricing.LegacyRevision && game.Chapter.SelectedProjectId == null, "Paid history silently adopted new540 terms");
                    if (!pendingBackup)
                    {
                        // A genuinely NEW different project uses current offers,
                        // but may not reinterpret the old600 receipt or spend4.
                        game.SelectChapterProject(GameIds.RecyclerBuild, 3);
                        Need(game.State.coins == 4 && game.State.houseStage == 2 && game.Chapter.SelectedProjectId == GameIds.RecyclerBuild && game.Chapter.ConstructionPriceRevision == ConstructionPricing.RevisionV2, "New offer changed paid600 wallet/history");
                        Need(game.State.lastSpin.wave.Price == 600 && game.State.lastSpin.sequence == 2, "New price term replaced retained600 receipt");
                        int writes = storage.WriteAttempts; var afterNewOffer = files.Inventory(directory);
                        game = Open(storage, codec); game.ExecuteChapterSpin(2);
                        Need(game.State.coins == 4 && writes == storage.WriteAttempts && Same(afterNewOffer, files.Inventory(directory)), "Old sequence replay after new V2 selection changed wallet/files");
                    }
                    Need(Bytes(files.PathAt(directory, 4), corrupt) && Bytes(files.PathAt(directory, 5), backup) && Bytes(files.PathAt(directory, 6), candidateBytes), "Later legitimate journal writes modified preserved originals");
                    results.Add(new { name = activeCase, passed = true, originalSelectedPrice = 600, currentOffer = 540,
                        beforeCoins = 595, frozenBaseReward = 8, frozenWorkshopReward = 1, frozenDebit = 600, paidCoins = 4,
                        conflictingNew540CandidateCoins = 55, cancelWrites = 0, cancelRestoreCalls = 0, explicitRestoreCalls = storage.RestoreAttempts,
                        replayReopens = 3, replayWrites = 0, laterNewOffer = !pendingBackup, finalSequence = game.State.sequence, finalCoins = game.State.coins,
                        sourceBackupSha256 = Hash(backup), preservedPendingSha256 = Hash(candidateBytes), inventory = files.Inventory(directory) });
                }
            }
            foreach (var source in sourceHashes) Need(Hash(File.ReadAllBytes(Path.Combine(files.Workspace, source.Key))) == source.Value, "Production source changed during new cases");
            Need(Bytes(pendingPath, pendingBytes) && Bytes(paidPath, paidBytes), "Immutable corpus fixture changed");
            var receipt = new { status = "PASS", sourceBase = "c583236", deadlineUtc = Deadline.ToString("O"), privateRoot = files.Root,
                filesystem = new DriveInfo(Path.GetPathRoot(files.Workspace)).DriveFormat, sourceHashes,
                fixtureHashes = new { pending = Hash(pendingBytes), paid = Hash(paidBytes) }, fixtureBytes = files.CheckBudget(), cases = results,
                limits = "Two new Windows real-file cases only; not UI cancellation or Android/power-loss qualification" };
            File.WriteAllText(files.Inside(Path.Combine(files.Root, "receipt.json")), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
            foreach (object result in results) Console.WriteLine("PASS " + JsonSerializer.Serialize(result));
            Console.WriteLine("PASS exactly2 new legacy-pricing file cases; receipt " + Path.Combine(files.Root, "receipt.json"));
        }
        catch (Exception error)
        {
            File.WriteAllText(files.Inside(Path.Combine(files.Root, "receipt.json")), JsonSerializer.Serialize(new { status = "FAIL", activeCase, type = error.GetType().Name, error.Message, cases = results }));
            throw;
        }
    }
    static GameSession Open(AuditedStorage storage, JsonStateCodec codec) { return GameSession.Open(new GameStore(storage, codec), 99); }
    static void CheckPaid(ModelState state)
    {
        Need(state.coins == 4 && state.sequence == 2 && state.houseStage == 2 && state.selectedStage == 0 && state.pending == null && state.seed == 2647435461u, "Independent595+8+1-600=4 paid checkpoint changed");
        Need(state.lastSpin.sequence == 2 && state.lastSpin.reward == 8 && state.lastSpin.bonus == 1 && state.lastSpin.wave.Price == 600 && state.lastSpin.symbols.SequenceEqual(new[] { 0, 0, 2 }), "Frozen original600 debit/bonus/symbols changed");
    }
    static void Need(bool value, string detail) { if (!value) throw new Exception(detail); }
    static string Hash(byte[] bytes) { return Convert.ToHexString(SHA256.HashData(bytes)); }
    static bool Bytes(string path, byte[] bytes) { return File.ReadAllBytes(path).SequenceEqual(bytes); }
    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) { return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value); }
}
