#if EXPANSION_CANDIDATE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JunkToNeon.Core;

internal static class BoostPriority
{
    const int SpinCap = 30;
    static readonly uint[] Seeds = { 1, 12305, 305419896 };
    static readonly JsonStateCodec Codec = new JsonStateCodec();
    static readonly Stopwatch Timer = Stopwatch.StartNew();
    static int checks, spins;
    static long priorSpins;
    static double priorSeconds;
    static readonly List<Row> Rows = new List<Row>();
    sealed class Memory : ISnapshotStorage
    {
        public string Raw;
        public int Writes;
        public string ReadPrimary() => Raw;
        public string ReadBackup() => null;
        public void Write(string raw) { Raw = raw; Writes++; }
    }
    sealed class Row
    {
        public uint Seed { get; set; }
        public int Spins { get; set; }
        public int EarliestBonusUnlockAffordabilitySpin { get; set; } = -1;
        public int EarliestEnergyChargeSpin { get; set; } = -1;
        public int FirstArmedSpin { get; set; } = -1;
        public int ActualBoostDebit { get; set; }
        public int ActualUnlockPartsDebit { get; set; }
        public int ActualUnlockPotatoesDebit { get; set; }
        public string Status { get; set; } = "running";
        public List<object> Trace { get; set; } = new List<object>();
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static void Need(bool ok, string why) { checks++; if (!ok) throw new InvalidOperationException(why); }
    static ExpansionQuote Query(Memory storage, Func<ExpansionQuote> query)
    {
        string raw = storage.Raw; int writes = storage.Writes; ExpansionQuote quote = null;
        try { quote = query(); }
        catch (InvalidOperationException error)
        {
            Need(error.Message == "Insufficient expansion resources", "Unexpected quote refusal: " + error.Message);
        }
        Need(storage.Raw == raw && storage.Writes == writes, "Quote changed private storage");
        return quote;
    }
    static void Execute(uint seed)
    {
        var row = new Row { Seed = seed }; Rows.Add(row);
        var storage = new Memory();
        var game = GameSession.Open(new GameStore(storage, Codec), seed, initializeJourney: true);
        Need(!game.RecoveryRequired && game.State.coins == 37 && storage.Writes == 1 && !game.Expansion.IsAdopted,
            "Fresh37/absent expansion boundary differs");
        uint expectedSeed = seed; long coinIncome = 0, parts = 0, potatoes = 0, energy = 0;
        for (int i = 1; i <= SpinCap; i++)
        {
            Need(priorSpins + spins < 6000 && priorSeconds + Timer.Elapsed.TotalSeconds < 180, "Combined declared budget reached");
            var before = game.State; int writes = storage.Writes; long at = 1700000000 + i * 4;
            game.ExecuteChapterSpin(before.sequence + 1, at, manualSpin: true); row.Spins = i; spins++;
            var state = game.State; var current = game.Expansion; var receipt = current.LastResourceReceipt;
            var draw = GameLogic.Draw(expectedSeed, before.sequence + 1); expectedSeed = draw.seedAfter;
            Need(state.seed == expectedSeed && state.lastSpin.symbols.SequenceEqual(draw.symbols), "Primary RNG/outcome mismatch");
            int expectedPayout = GameLogic.Payout(draw.symbols); coinIncome += expectedPayout;
            Need(state.lastSpin.reward == expectedPayout && state.lastSpin.bonus == 0 && state.lastSpin.chapter.Effects.TotalCoins == 0 &&
                state.coins == 37 + coinIncome, "Independent free-spin coin ledger differs");
            Need(storage.Writes == writes + 2 && receipt.Sequence == state.lastSpin.sequence, "Foreground journal/receipt boundary differs");
            parts += receipt.Parts; potatoes += receipt.Potatoes; energy += receipt.Energy;
            if (current.Energy >= EconomyRules.ExpansionBoostCost && row.EarliestEnergyChargeSpin < 0) row.EarliestEnergyChargeSpin = i;
            ExpansionQuote unlock = current.BonusUnlocked ? null : Query(storage, () => game.GetExpansionOffer("bonus.unlock"));
            if (unlock != null)
            {
                row.EarliestBonusUnlockAffordabilitySpin = i;
                row.ActualUnlockPartsDebit = unlock.PartsCost; row.ActualUnlockPotatoesDebit = unlock.PotatoesCost;
                uint rng = game.State.seed; long coins = game.State.coins; writes = storage.Writes;
                game.BuyExpansion(unlock, at); parts -= unlock.PartsCost; potatoes -= unlock.PotatoesCost;
                Need(storage.Writes == writes + 2 && game.State.seed == rng && game.State.coins == coins, "Unlock changed primary ledger/RNG or journal phases");
            }
            ExpansionQuote boost = game.Expansion.BonusUnlocked ? Query(storage, () => game.GetExpansionAction("boost", "boost.next-bonus")) : null;
            if (boost != null)
            {
                uint rng = game.State.seed; long coins = game.State.coins; writes = storage.Writes;
                game.ExecuteExpansion(boost, at); energy -= boost.EnergyCost;
                row.ActualBoostDebit = boost.EnergyCost; row.FirstArmedSpin = i;
                Need(storage.Writes == writes + 2 && game.Expansion.BoostArmed && game.State.seed == rng && game.State.coins == coins,
                    "First arming changed primary ledger/RNG or journal phases");
            }
            current = game.Expansion;
            Need(!game.RecoveryRequired && game.State.pending == null && current.Parts == parts && current.Potatoes == potatoes && current.Energy == energy,
                "Independent credited-resources-minus-quoted-debits ledger differs");
            row.Trace.Add(new { spin = i, symbols = string.Join("", state.lastSpin.symbols), expectedSeed, wallet = game.State.coins,
                current.Parts, current.Potatoes, current.Energy,
                creditedParts = receipt.Parts, creditedPotatoes = receipt.Potatoes, creditedEnergy = receipt.Energy,
                bonusUnlocked = current.BonusUnlocked, current.BoostArmed, writes = storage.Writes });
            if (boost != null) { row.Status = "armed"; break; }
        }
        if (row.Status == "running") row.Status = "capped-at30";
        Console.WriteLine("DONE energy-first seed=" + seed + " unlock=" + row.EarliestBonusUnlockAffordabilitySpin +
            " charge=" + row.EarliestEnergyChargeSpin + " armed=" + row.FirstArmedSpin + " spins=" + row.Spins);
    }
    internal static int Run(string[] args)
    {
        string output = null; object inputs = null;
        try
        {
            Need(args.Length == 8 && args[0] == "--source-root" && args[2] == "--source-commit" && args[4] == "--prior-report" && args[6] == "--output",
                "Use --boost-priority --source-root <same alternative> --source-commit <identity> --prior-report <5334-spin report> --output <new JSON path>");
            string root = Path.GetFullPath(args[1]), priorPath = Path.GetFullPath(args[5]); output = Path.GetFullPath(args[7]);
            Need(!File.Exists(output) && !File.Exists(output + ".failure.json"), "Preserve first supplement receipts");
            byte[] priorBytes = File.ReadAllBytes(priorPath); string priorHash = Hash(priorBytes);
            using var prior = JsonDocument.Parse(priorBytes); var priorRoot = prior.RootElement;
            priorSpins = priorRoot.GetProperty("totalSpins").GetInt64(); priorSeconds = priorRoot.GetProperty("wallSeconds").GetDouble();
            Need(priorSpins == 5334 && priorRoot.GetProperty("sourceCommit").GetString() == args[3], "Not the selected alternative report");
            var sourceInputs = priorRoot.GetProperty("sourceInputs").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()); inputs = sourceInputs;
            foreach (var source in sourceInputs) Need(Hash(File.ReadAllBytes(Path.Combine(root, source.Key))) == source.Value, "Frozen production source drift: " + source.Key);
            Need(EconomyRules.ExpansionBoostCost == 15 && EconomyRules.ExpansionEnergyCap == 30 &&
                ExpansionCatalog.Get("equipment.workshop.1").PartsCost == 25 && ExpansionCatalog.Get("equipment.workshop.1").PotatoesCost == 15,
                "Supplement is not compiled against the selected exact alternative");
            foreach (uint seed in Seeds) Execute(seed);
            Need(spins <= 90 && priorSpins + spins <= 6000 && priorSeconds + Timer.Elapsed.TotalSeconds < 180, "Combined final budget exceeded");
            Need(Hash(File.ReadAllBytes(priorPath)) == priorHash, "Original nine-run alternative report changed");
            var report = new { schema = 1, diagnostic = "energy-first unlock-and-arm only", sourceCommit = args[3], sourceInputs,
                priorPath, priorSha256 = priorHash, priorSpins, priorWallSeconds = priorSeconds,
                initialRules = new { boostEnergyCost = EconomyRules.ExpansionBoostCost, energyCap = EconomyRules.ExpansionEnergyCap,
                    bonusUnlockParts = ExpansionCatalog.Get("bonus.unlock").PartsCost, bonusUnlockPotatoes = ExpansionCatalog.Get("bonus.unlock").PotatoesCost },
                checks, supplementalSpins = spins, combinedSpins = priorSpins + spins, supplementalWallSeconds = Timer.Elapsed.TotalSeconds,
                assemblySha256 = Hash(File.ReadAllBytes(typeof(BoostPriority).Assembly.Location)),
                boundary = "Same three seeds and frozen alternative; naturally earned resources; no module/equipment/construction/skill buys, free funding or numeric changes; stop on first arming or30. Original nine policy MISS rows remain unchanged. Earliest affordability is for this no-prior-resource-spending path, not proof of global optimality across every possible investment path.", rows = Rows };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            Need(Encoding.UTF8.GetByteCount(json) <= 128 * 1024, "128KiB supplement output cap exceeded");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, json);
            Console.WriteLine("PASS " + checks + " checks (JSON excludes final output-size check); " + spins + " supplemental spins; " + (priorSpins + spins) + " combined");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (output != null && !File.Exists(output + ".failure.json"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".failure.json",
                    JsonSerializer.Serialize(new { error = error.ToString(), inputs, checks, spins, rows = Rows }, new JsonSerializerOptions { WriteIndented = true }));
            }
            return 1;
        }
    }
}
#endif
