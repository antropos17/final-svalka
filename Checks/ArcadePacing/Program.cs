using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Content;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

internal static partial class Probe
{
    static void Catalog()
    {
        Need(ArcadeCatalog.Offers.Count == 6 && ExpansionCatalog.Offers.Count == 31, "Separate six-offer Arcade/original31 catalog boundary changed");
        string[] ids = { "arcade.slot.1", "arcade.slot.2", "arcade.slot.3", "arcade.pack.parts", "arcade.pack.potatoes", "arcade.boost.next-bonus" };
        int[] parts = { 20, 0, 30, 0, 0, 0 }, potatoes = { 0, 20, 30, 0, 0, 0 }, diamonds = { 2, 4, 7, 1, 1, 1 };
        for (int i = 0; i < 6; i++)
        {
            var o = ArcadeCatalog.Offers[i];
            Need(o.Id == ids[i] && o.PartsCost == parts[i] && o.PotatoesCost == potatoes[i] && o.DiamondsCost == diamonds[i] &&
                o.PartsCredit == (i == 3 ? 5 : 0) && o.PotatoesCredit == (i == 4 ? 5 : 0) && o.ArmsBonusBoost == (i == 5), "Literal Arcade catalog mismatch");
        }
        Need(EconomyRules.ArcadeTicketCadence == 8 && EconomyRules.ArcadeTicketCap == 3 && EconomyRules.ArcadeSortingItems == 8 && EconomyRules.ArcadeMemoryAttempts == 9 &&
            EconomyRules.ExpansionBoostCost == 15 && EconomyRules.ExpansionEnergyCap == 30 &&
            ConstructionPricing.Price("house.improve", ConstructionPricing.LegacyRevision) == 600 && ConstructionPricing.Price("house.improve", ConstructionPricing.RevisionV2) == 540 &&
            ChapterEconomy.ProjectPrice("house.improve") == 480 && SkillTree.Nodes.Sum(n => n.Price) == 610, "Retained rule/price literals changed");
    }
    static int Main(string[] args)
    {
        string output = null; object sourceInputs = null;
        try
        {
            Need(args.Length == 6 && args[0] == "--source-root" && args[2] == "--source-commit" && args[4] == "--output", "Use --source-root <immutable tree> --source-commit <commit> --output <new prefix>");
            string root = Path.GetFullPath(args[1]); output = Path.GetFullPath(args[5]);
            Need(args[3] == CoreCommit && !File.Exists(output + ".json") && !File.Exists(output + ".csv") && !File.Exists(output + ".failure.json"), "Wrong source identity or existing first receipt");
            sourceInputs = new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance" }.Select(g => Path.Combine(root, "Unity/Assets/Scripts", g))
                .SelectMany(g => Directory.GetFiles(g, "*.cs")).OrderBy(p => p, StringComparer.Ordinal)
                .ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), p => Hash(File.ReadAllBytes(p)));
            Catalog();
            foreach (bool perfect in new[] { false, true }) foreach (uint seed in Seeds) Natural(seed, perfect);
            foreach (uint seed in Seeds) TicketControl(seed);
            foreach (uint seed in Seeds) for (int level = 1; level <= 3; level++) Pair(seed, level);
            CapControls(); Limit();
            var report = new { schema = 1, sourceCommit = CoreCommit, sourceInputs, assemblySha256 = Hash(File.ReadAllBytes(typeof(Probe).Assembly.Location)),
                Checks, ForegroundCalls, GameActions, OtherCommands, wallSeconds = Timer.Elapsed.TotalSeconds,
                policies = "Frozen policies.md: low1Diamond Sorting first5Parts pack then levels; visible perfect Sorting upper income; hold37 Memory then3; prepared40-spin exact-cost marginal levels",
                scope = "Actual production private-memory GameStore/strict Codec; no whole chapter, profile sweep, Unity/UI, physical durability, Android, FPS or human enjoyment acceptance",
                natural = NaturalRows, ticketMemoryControl = ControlRows, marginal = MarginalRows, caps = CapRows,
                producerEvidenceReused = "fa9f3f2 ArcadeCore11 groups for full scoring boundary/fault/replay/historical matrices and literal triple/single-wallet arithmetic; not rerun or relabeled as this utility's cases" };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), csv = string.Join("\n", Csv) + "\n";
            Need(Encoding.UTF8.GetByteCount(json) + Encoding.UTF8.GetByteCount(csv) <= 512 * 1024, "Combined512KiB output cap exceeded");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".json", json); File.WriteAllText(output + ".csv", csv);
            Console.WriteLine("PASS " + Checks + " checks (JSON excludes final size assertion); " + ForegroundCalls + " foreground, " + GameActions + " game actions");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (output != null && !File.Exists(output + ".failure.json"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".failure.json", JsonSerializer.Serialize(new {
                    error = error.ToString(), sourceInputs, Checks, ForegroundCalls, GameActions, OtherCommands, wallSeconds = Timer.Elapsed.TotalSeconds,
                    lastPrivateSnapshot = Active?.Raw, natural = NaturalRows, ticketMemoryControl = ControlRows, marginal = MarginalRows, caps = CapRows }, new JsonSerializerOptions { WriteIndented = true }));
            }
            return 1;
        }
    }
}
