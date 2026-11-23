using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;
using ExpansionPacing;

internal static class Program
{
    const long Epoch = 1700000000;
    const int RunCap = 800, TotalSpinCap = 6000, WallCapSeconds = 180;
    static readonly uint[] Seeds = { 1, 12305, 305419896 };
    static readonly string[] Policies = { "cheapest-builder", "skills-first", "house-first" };
    static readonly JsonStateCodec Codec = new JsonStateCodec();
    static readonly Stopwatch Total = Stopwatch.StartNew();
    static int assertions, totalSpins;
    static readonly List<RunResult> Runs = new List<RunResult>();
    internal static void AccountDiagnosticSpin()
    {
        Need(totalSpins < TotalSpinCap && Total.Elapsed.TotalSeconds < WallCapSeconds, "Declared total diagnostic bound reached");
        totalSpins++;
    }
    static void Need(bool condition, string why)
    { assertions++; if (!condition) throw new InvalidOperationException(why); }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    sealed class Storage : ISnapshotStorage
    {
        public string Raw;
        public int Writes;
        public string ReadPrimary() => Raw;
        public string ReadBackup() => null;
        public void Write(string value) { Raw = value; Writes++; }
    }
    static int Priority(string project)
    {
        if (project.StartsWith("house.", StringComparison.Ordinal)) return 0;
        if (project.StartsWith("workshop.", StringComparison.Ordinal)) return 1;
        if (project.StartsWith("recycler.", StringComparison.Ordinal)) return 2;
        if (project.StartsWith("pigeon-loft.", StringComparison.Ordinal)) return 3;
        if (project.StartsWith("cart.", StringComparison.Ordinal)) return 4;
        return 5;
    }
    static RunResult Run(uint seed, string policy)
    {
        var result = new RunResult { Seed = seed, Policy = policy };
        Runs.Add(result);
        var storage = new Storage(); var driver = ExpansionDriver.Create();
        var game = GameSession.Open(new GameStore(storage, Codec), seed, initializeJourney: true);
        Need(!game.RecoveryRequired && game.State.coins == 37, "Private Fresh opening failed");
        var timer = Stopwatch.StartNew(); int previousConstructionSpin = 0;
        uint expectedSeed = seed; long quarterSeconds = 0;
        long Now() => Epoch + quarterSeconds / 4;
        void Balance()
        {
            var state = game.State;
            Need(!game.RecoveryRequired && state.pending == null, "Private operation did not commit");
            Need(state.coins == 37 + result.SpinIncome + result.ExchangeIncome - result.ConstructionSpend - result.SkillSpend,
                "Independent wallet ledger differs: " + policy + "/" + seed + "/" + result.Spins);
            Need(state.seed == expectedSeed, "Only a spin may advance the primary RNG");
        }
        void Built(string id, long price)
        {
            result.ConstructionCount++; result.ConstructionSpend += price;
            if (id == "house.repair") result.FirstRepairSpin = result.Spins;
            int gap = result.Spins - previousConstructionSpin; previousConstructionSpin = result.Spins;
            result.MaximumConstructionGap = Math.Max(result.MaximumConstructionGap, gap);
            result.Purchases.Add(new Purchase { Kind = "construction", Id = id, Spin = result.Spins,
                Price = price, Wallet = game.State.coins, Gap = gap });
        }
        void Checkpoint()
        {
            var state = game.State; var chapter = game.Chapter;
            result.Checkpoints.Add(new Checkpoint { Spin = result.Spins, Wallet = state.coins,
                Constructions = result.ConstructionCount, Skills = chapter.Skills.Receipts.Count,
                Selected = chapter.SelectedProjectId, SelectedPrice = chapter.SelectedProjectId == null ? 0 : ChapterEconomy.SelectedProjectPrice(chapter),
                SpinIncome = result.SpinIncome, ExchangeIncome = result.ExchangeIncome,
                Expansion = driver.Snapshot(game) });
        }
        Checkpoint();
        while (true)
        {
            Need(Total.Elapsed.TotalSeconds < WallCapSeconds && totalSpins <= TotalSpinCap, "Declared total execution bound reached");
            var chapter = game.Chapter;
            if (ChapterCatalog.Rules.IsChapterComplete(chapter.Progress)) break;
            if (chapter.TransitionPending)
            { game.AcknowledgeEraTransition(game.State.sequence + 1, Now()); Balance(); continue; }
            if (ChapterCatalog.Rules.CanUnlockWorkshopYard(chapter.Progress))
            {
                result.EraReadySpin = result.Spins;
                game.EnterWorkshopYard(game.State.sequence + 1, Now());
                result.EraEnteredSpin = result.Spins; Balance(); continue;
            }
            driver.Decisions(game, result, Now()); Balance();
            if (policy == "skills-first")
            {
                var quote = SkillTree.Nodes.Select(n => ChapterEconomy.QuoteSkill(game.State, n.Id))
                    .Where(q => q.Status == OfferStatus.Available).OrderBy(q => q.Node.Price)
                    .ThenBy(q => q.Node.Id, StringComparer.Ordinal).FirstOrDefault();
                if (quote != null)
                {
                    game.BuySkill(quote.Request, Now()); result.SkillSpend += quote.Node.Price;
                    if (result.FirstOptionalSkillSpin < 0) result.FirstOptionalSkillSpin = result.Spins;
                    result.Purchases.Add(new Purchase { Kind = "skill", Id = quote.Node.Id, Spin = result.Spins,
                        Price = quote.Node.Price, Wallet = game.State.coins });
                    Balance(); continue;
                }
            }
            if (chapter.SelectedProjectId == null)
            {
                var choices = ChapterCatalog.Rules.AvailableUpgrades(chapter.Progress);
                string id = (policy == "house-first" ? choices.OrderBy(Priority).ThenBy(ChapterEconomy.ProjectPrice)
                    : choices.OrderBy(ChapterEconomy.ProjectPrice).ThenBy(x => x, StringComparer.Ordinal)).First();
                long price = ChapterEconomy.ProjectPrice(id);
                game.SelectChapterProject(id, game.State.sequence + 1, Now());
                if (game.Chapter.SelectedProjectId == null) { Built(id, price); Balance(); continue; }
            }
            Need(result.Spins < RunCap && totalSpins < TotalSpinCap, "Declared spin bound reached");
            var before = game.State; string selected = game.Chapter.SelectedProjectId;
            long selectedPrice = ChapterEconomy.SelectedProjectPrice(game.Chapter);
            quarterSeconds += before.houseStage == 0 ? 14 : 15;
            result.Spins++; totalSpins++;
            driver.BeforeSpin(game, result);
            game.ExecuteChapterSpin(before.sequence + 1, Now(), manualSpin: true);
            var after = game.State; var receipt = after.lastSpin;
            // Independent primary draw count/identity; no simulated or rerolled outcomes.
            var draw = GameLogic.Draw(expectedSeed, before.sequence + 1); expectedSeed = draw.seedAfter;
            Need(receipt.symbols.SequenceEqual(draw.symbols) && receipt.reward == GameLogic.Payout(draw.symbols), "Primary spin outcome/payout differs");
            long income = receipt.reward + receipt.bonus + receipt.chapter.Effects.TotalCoins;
            result.SpinIncome += income;
            if (selected != null && game.Chapter.SelectedProjectId == null)
            {
                Need(receipt.chapter.Price == selectedPrice, "Selected construction debit was not frozen"); Built(selected, selectedPrice);
            }
            driver.AfterSpin(game, result); Balance();
            if (result.Spins == 10 || result.Spins == 25 || result.Spins == 50 || result.Spins == 60 || result.Spins == 100 || result.Spins == 120) Checkpoint();
        }
        result.ChapterSpin = result.Spins; result.Wallet = game.State.coins;
        result.ProjectedNormalManualSeconds = result.Spins * 3.5;
        result.ProjectedFastManualSeconds = result.Spins * 1.5;
        result.ProjectedNormalAutoSeconds = result.Spins * 3.5 + (result.Spins - result.FirstRepairSpin) * .25;
        result.ProjectedFastAutoSeconds = result.Spins * 1.5 + (result.Spins - result.FirstRepairSpin) * .25;
        result.Skills = game.Chapter.Skills.Receipts.Count; result.Writes = storage.Writes;
        result.FinalSeed = game.State.seed; result.FinalStateHash = Hash(Encoding.UTF8.GetBytes(Codec.Encode(game.State)));
        result.Expansion = driver.Snapshot(game); result.WallSeconds = timer.Elapsed.TotalSeconds;
        Need(result.ConstructionCount == 24 && result.ConstructionSpend == 9310, "Current construction offer total/ending differs");
        Need(policy != "skills-first" || result.SkillSpend == 610 && result.Skills == 6, "Current skill terms differ");
        Balance(); result.Status = "complete";
        Console.WriteLine("DONE " + policy + " seed=" + seed + " chapter=" + result.Spins + " maxGap=" + result.MaximumConstructionGap);
        return result;
    }
    static string Csv()
    {
        var text = new StringBuilder("seed,policy,status,chapter_spins,era_ready_spins,max_construction_gap,first_optional_skill_spin,construction_spend,skill_spend,wallet,exchange_income,first_useful_shop_affordability_spin,first_useful_shop_purchase_spin,useful_shop_purchases_by50,first_potato_spend_spin,first_boost_affordability_spin,boost_arms,boost_consumptions,normal_manual_seconds,fast_manual_seconds,normal_auto_seconds,fast_auto_seconds\n");
        foreach (var run in Runs) text.Append(run.Seed).Append(',').Append(run.Policy).Append(',').Append(run.Status).Append(',')
            .Append(run.ChapterSpin).Append(',').Append(run.EraReadySpin).Append(',').Append(run.MaximumConstructionGap).Append(',')
            .Append(run.FirstOptionalSkillSpin).Append(',').Append(run.ConstructionSpend).Append(',').Append(run.SkillSpend).Append(',')
            .Append(run.Wallet).Append(',').Append(run.ExchangeIncome).Append(',').Append(run.FirstUsefulShopAffordabilitySpin).Append(',')
            .Append(run.FirstUsefulShopPurchaseSpin).Append(',').Append(run.UsefulShopPurchasesBy50).Append(',').Append(run.FirstPotatoSpendSpin).Append(',')
            .Append(run.FirstBoostAffordabilitySpin).Append(',').Append(run.BoostArms).Append(',').Append(run.BoostConsumptions).Append(',')
            .Append(run.ProjectedNormalManualSeconds.ToString(CultureInfo.InvariantCulture)).Append(',').Append(run.ProjectedFastManualSeconds.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(run.ProjectedNormalAutoSeconds.ToString(CultureInfo.InvariantCulture)).Append(',').Append(run.ProjectedFastAutoSeconds.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--decision-cadence") return DecisionCadence.Run(args.Skip(1).ToArray());
#if WORKBENCH_CANDIDATE
        if (args.Length > 0 && args[0] == "--workbench") return WorkbenchProbe.Run(args.Skip(1).ToArray());
#endif
#if EXPANSION_CANDIDATE
        if (args.Length > 0 && args[0] == "--boost-priority") return BoostPriority.Run(args.Skip(1).ToArray());
#endif
        string output = null; object identities = null; string sourceCommit = null;
        try
        {
            Need(args.Length == 6 && args[0] == "--source-root" && args[2] == "--source-commit" && args[4] == "--output", "Use --source-root <frozen root> --source-commit <commit> --output <new prefix>");
            string sourceRoot = Path.GetFullPath(args[1]); sourceCommit = args[3]; output = Path.GetFullPath(args[5]);
            Need(!File.Exists(output + ".json") && !File.Exists(output + ".csv") && !File.Exists(output + ".failure.json"), "Never overwrite first receipts");
            identities = new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance", "Expansion" }
                .Select(g => Path.Combine(sourceRoot, "Unity/Assets/Scripts", g)).Where(Directory.Exists)
                .SelectMany(g => Directory.GetFiles(g, "*.cs", SearchOption.TopDirectoryOnly)).OrderBy(x => x, StringComparer.Ordinal)
                .ToDictionary(x => Path.GetRelativePath(sourceRoot, x).Replace('\\', '/'), x => Hash(File.ReadAllBytes(x)));
            Need(SkillTree.Nodes.Sum(n => n.Price) == 610 && ChapterEconomy.ProjectPrice("house.improve") == 480,
                "This utility expects actual current V3 baseline terms");
            // Literal independent exhaustive primary payout expectation:4 triples,
            //36 pairs and24 different outcomes sum452 coins over64 outcomes.
            long total = 0; var histogram = new Dictionary<int, int>();
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
            { int payout = GameLogic.Payout(new[] { a, b, c }); total += payout; histogram[payout] = histogram.TryGetValue(payout, out int count) ? count + 1 : 1; }
            Need(total == 452 && histogram[23] == 4 && histogram[8] == 36 && histogram[3] == 24, "Independent primary payout contract differs");
            foreach (string policy in Policies) foreach (uint seed in Seeds) Run(seed, policy);
            var report = new { schema = 1, sourceCommit, sourceInputs = identities, assembly = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
                driver = ExpansionDriver.Create().Label, assertions, totalSpins, wallSeconds = Total.Elapsed.TotalSeconds,
                bounds = new { seeds = Seeds, policies = Policies, perRunSpins = RunCap, totalSpins = TotalSpinCap, wallSeconds = WallCapSeconds },
                timingProjection = new { normalPresentationSeconds = 3.5, fastPresentationSeconds = 1.5, autoGapSeconds = .25,
                    qualification = "Arithmetic projections from actual spin counts; no menus, decisions, IO, human reaction or measured runtime speed. Normal remains default; timing does not change these simulated economic commands." },
                scope = "Production GameStore/strict Codec in private memory; no filesystem durability, Unity, human pacing or Android claim", runs = Runs };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), csv = Csv();
            Need(Encoding.UTF8.GetByteCount(json) <= 2 * 1024 * 1024 && Encoding.UTF8.GetByteCount(csv) <= 512 * 1024, "Declared output bound reached");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".json", json); File.WriteAllText(output + ".csv", csv);
            Console.WriteLine("PASS " + assertions + " assertions (JSON excludes final output-size check); " + Runs.Count + " bounded runs; " + totalSpins + " spins"); return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (output != null && !File.Exists(output + ".failure.json"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output + ".failure.json", JsonSerializer.Serialize(new { sourceCommit, sourceInputs = identities,
                    error = error.ToString(), assertions, totalSpins, wallSeconds = Total.Elapsed.TotalSeconds, partialRuns = Runs }, new JsonSerializerOptions { WriteIndented = true }));
            }
            return 1;
        }
    }
}
