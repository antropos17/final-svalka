#if WORKBENCH_CANDIDATE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JunkToNeon.Content;
using JunkToNeon.Core;

internal static class WorkbenchProbe
{
    const long Epoch = 1700000000;
    const int CallCap = 1800, WallCap = 60;
    static readonly uint[] Seeds = { 1, 12305, 305419896 };
    static readonly JsonStateCodec Codec = new JsonStateCodec();
    static readonly Stopwatch Timer = Stopwatch.StartNew();
    static readonly List<object> NaturalRows = new List<object>(), MarginalRows = new List<object>(), FocusRows = new List<object>();
    static readonly StringBuilder SummaryCsv = new StringBuilder("phase,seed,case,spins,target_pass,baseline_bonus_units,parts_focus_bonus_units,potato_focus_bonus_units,marginal_parts,marginal_potatoes,bench_purchase_spin\n");
    static Memory activeStorage;
    static int calls, checks;
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static string StateHash(ModelState state) => Hash(Encoding.UTF8.GetBytes(Codec.Encode(state)));
    static void Need(bool ok, string detail) { checks++; if (!ok) throw new InvalidOperationException(detail); }
    sealed class Memory : ISnapshotStorage
    {
        public string Raw;
        public int Writes;
        public string ReadPrimary() => Raw;
        public string ReadBackup() => null;
        public void Write(string raw) { Raw = raw; Writes++; }
    }
    sealed class Branch
    {
        public readonly Memory Storage;
        public readonly GameSession Game;
        public readonly string StartingHash;
        public long CreditedParts, CreditedPotatoes, CreditedEnergy, SpentParts, SpentPotatoes, SpentEnergy, TradeCoins, SpinCoins;
        public int Spins, BoostArms, BoostConsumptions;
        readonly long initialParts, initialPotatoes, initialEnergy, initialCoins;
        uint expectedSeed;
        public readonly StringBuilder PrimaryTrace = new StringBuilder(), BonusTrace = new StringBuilder();
        public Branch(uint seed, ModelState state = null)
        {
            Storage = new Memory { Raw = state == null ? null : Codec.Encode(state) };
            Game = GameSession.Open(new GameStore(Storage, Codec), seed, initializeJourney: true);
            Need(!Game.RecoveryRequired, "Private branch cannot open: " + Game.Error);
            var current = Game.Expansion; initialParts = current.Parts; initialPotatoes = current.Potatoes;
            initialEnergy = current.Energy; initialCoins = Game.State.coins; expectedSeed = Game.State.seed;
            StartingHash = StateHash(Game.State);
            if (state == null) Need(initialCoins == 37 && !current.IsAdopted, "Natural branch must be genuine Fresh37");
        }
        public ExpansionQuote Offer(string id)
        {
            string raw = Storage.Raw; int writes = Storage.Writes;
            ExpansionQuote quote = null;
            try { quote = Game.GetExpansionOffer(id); }
            catch (InvalidOperationException error)
            { Need(error.Message == "Insufficient expansion resources", "Unexpected offer refusal: " + error.Message); }
            Need(Storage.Raw == raw && Storage.Writes == writes, "Read-only offer wrote storage");
            return quote;
        }
        public void Command(ExpansionQuote quote)
        {
            Need(quote != null, "Required prepared command is not affordable");
            activeStorage = Storage;
            int writes = Storage.Writes; uint seed = Game.State.seed; long bonusRolls = Game.Expansion.BonusRolls; uint bonusSeed = Game.Expansion.BonusSeed;
            var after = quote.Action == "buy" ? Game.BuyExpansion(quote, Epoch + calls * 4) : Game.ExecuteExpansion(quote, Epoch + calls * 4);
            SpentParts += quote.PartsCost; SpentPotatoes += quote.PotatoesCost; SpentEnergy += quote.EnergyCost; TradeCoins += quote.CoinsReward;
            Need(quote.CoinsCost == 0 && Storage.Writes == writes + 2 && after.seed == seed && Game.Expansion.BonusRolls == bonusRolls && Game.Expansion.BonusSeed == bonusSeed,
                "Command changed RNG/cadence/coin price or journal phases");
            if (quote.Action == "boost") BoostArms++;
            Ledger();
        }
        public void BoostWhenReady()
        {
            var state = Game.Expansion;
            if (state.BonusUnlocked && !state.BoostArmed && state.Energy >= EconomyRules.ExpansionBoostCost)
                Command(Game.GetExpansionAction("boost", "boost.next-bonus"));
        }
        public void Drain()
        {
            while (Game.Expansion.Parts >= 60) Command(Offer("trade.parts"));
            while (Game.Expansion.Potatoes >= 60) Command(Offer("trade.potatoes"));
        }
        public ExpansionResourceReceipt Spin()
        {
            Need(calls < CallCap && Timer.Elapsed.TotalSeconds < WallCap, "Declared foreground/wall bound reached");
            activeStorage = Storage;
            var before = Game.State; int writes = Storage.Writes;
            Game.ExecuteChapterSpin(before.sequence + 1, Epoch + (calls + 1) * 4, manualSpin: true); calls++; Spins++;
            var after = Game.State; var spin = after.lastSpin; var resource = Game.Expansion.LastResourceReceipt;
            var draw = GameLogic.Draw(expectedSeed, before.sequence + 1); expectedSeed = draw.seedAfter;
            int distinct = spin.symbols.Distinct().Count(), payout = distinct == 1 ? 23 : distinct == 2 ? 8 : 3;
            Need(after.seed == expectedSeed && spin.symbols.SequenceEqual(draw.symbols) && spin.reward == payout && spin.bonus == 0 && spin.chapter.Effects.TotalCoins == 0,
                "Primary outcome/payout or absent-construction/skill effects drift");
            Need(Storage.Writes == writes + 2 && resource.Sequence == spin.sequence, "Foreground journal/receipt mismatch");
            SpinCoins += payout; CreditedParts += resource.Parts; CreditedPotatoes += resource.Potatoes; CreditedEnergy += resource.Energy;
            if (resource.Boosted) BoostConsumptions++;
            PrimaryTrace.Append(string.Join("", spin.symbols)).Append('/').Append(payout).Append('/').Append(after.seed).Append('|');
            if (resource.BonusSymbol >= 0) BonusTrace.Append(resource.BonusSymbol).Append('/').Append(Game.Expansion.BonusSeed).Append('/').Append(resource.Boosted).Append('|');
            Ledger(); return resource;
        }
        public void Ledger()
        {
            var state = Game.Expansion;
            Need(!Game.RecoveryRequired && Game.State.pending == null && Game.State.coins == initialCoins + SpinCoins + TradeCoins &&
                state.Parts == initialParts + CreditedParts - SpentParts && state.Potatoes == initialPotatoes + CreditedPotatoes - SpentPotatoes &&
                state.Energy == initialEnergy + CreditedEnergy - SpentEnergy, "Independent private coin/resource ledger differs");
        }
        public object Facts() => new { StartingHash, finalHash = StateHash(Game.State), Spins, CreditedParts, CreditedPotatoes, CreditedEnergy,
            SpentParts, SpentPotatoes, SpentEnergy, TradeCoins, SpinCoins, BoostArms, BoostConsumptions,
            primaryTraceSha256 = Hash(Encoding.UTF8.GetBytes(PrimaryTrace.ToString())), bonusTraceSha256 = Hash(Encoding.UTF8.GetBytes(BonusTrace.ToString())) };
    }
    static ModelState Synthetic(ModelState source, int parts, int potatoes, int energy, bool? armed = null)
    {
        var state = source.Copy(); var draft = new ExpansionDraft(state.expansion) { Parts = parts, Potatoes = potatoes, Energy = energy };
        if (armed.HasValue) draft.BoostArmed = armed.Value;
        state.expansion = draft.Freeze(); GameLogic.Validate(state);
        // Validation and canonical decode are real production; only balances and
        // explicitly disclosed focus-case armed flag are prepared, never ownership.
        return Codec.Decode(Codec.Encode(state));
    }
    static ModelState Natural(uint seed)
    {
        var branch = new Branch(seed); var trace = new List<object>(); var purchases = new List<object>();
        int unlockSpin = -1, benchSpin = -1; ModelState beforeBench = null;
        for (int i = 1; i <= 80; i++)
        {
            var receipt = branch.Spin();
            if (!branch.Game.Expansion.BonusUnlocked)
            {
                var quote = branch.Offer("bonus.unlock");
                if (quote != null)
                { branch.Command(quote); unlockSpin = i; purchases.Add(new { spin = i, quote.Id, quote.PartsCost, quote.PotatoesCost, quote.RuleRevision }); }
            }
            branch.BoostWhenReady();
            if (branch.Game.Expansion.BonusUnlocked)
            {
                var quote = branch.Offer("workbench.bonus.1");
                if (quote != null)
                {
                    beforeBench = branch.Game.State; branch.Command(quote); benchSpin = i;
                    Need(branch.Game.Expansion.BonusWorkbenchLevel == 1 && branch.Game.Expansion.BonusFocusId == "focus.balanced", "BenchI did not commit balanced ownership");
                    purchases.Add(new { spin = i, quote.Id, quote.PartsCost, quote.PotatoesCost, quote.RuleRevision });
                }
            }
            var state = branch.Game.Expansion;
            trace.Add(new { spin = i, symbols = string.Join("", branch.Game.State.lastSpin.symbols), state.Parts, state.Potatoes, state.Energy,
                receipt.BonusSymbol, receipt.BonusParts, receipt.BonusPotatoes, receipt.Boosted, state.BoostArmed });
            if (beforeBench != null) break;
        }
        NaturalRows.Add(new { seed, population = "naturally earned", unlockSpin, benchSpin, targetBy80 = benchSpin >= 0 && benchSpin <= 80,
            purchases, trace, facts = branch.Facts() });
        SummaryCsv.Append("natural,").Append(seed).Append(",benchI,").Append(branch.Spins).Append(',').Append(benchSpin >= 0 && benchSpin <= 80)
            .Append(",,,,,,").Append(benchSpin).Append('\n');
        Console.WriteLine("DONE natural seed=" + seed + " unlock=" + unlockSpin + " benchI=" + benchSpin);
        return beforeBench;
    }
    static void SameTrace(Branch control, Branch other, ExpansionResourceReceipt a, ExpansionResourceReceipt b)
    {
        Need(control.Game.State.seed == other.Game.State.seed && control.Game.State.lastSpin.symbols.SequenceEqual(other.Game.State.lastSpin.symbols) &&
            control.Game.State.lastSpin.reward == other.Game.State.lastSpin.reward && a.BonusSymbol == b.BonusSymbol && a.Boosted == b.Boosted &&
            control.Game.Expansion.BonusSeed == other.Game.Expansion.BonusSeed && control.Game.Expansion.BonusProgress == other.Game.Expansion.BonusProgress,
            "Paired primary/bonus trace changed");
    }
    static void Marginal(uint seed, ModelState source)
    {
        var funded = Synthetic(source, 200, 200, 30);
        var none = new Branch(seed, funded); var one = new Branch(seed, funded); var two = new Branch(seed, funded);
        one.Command(one.Offer("workbench.bonus.1")); two.Command(two.Offer("workbench.bonus.1"));
        var secondQuote = two.Offer("workbench.bonus.2"); two.Command(secondQuote);
        Need(secondQuote.PartsCost == 40 && secondQuote.PotatoesCost == 30, "II marginal cost is not70");
        var paidSetup = new { noBench = none.Facts(), benchI = one.Facts(), benchII = two.Facts(),
            incrementalQuote = new { secondQuote.Id, secondQuote.PartsCost, secondQuote.PotatoesCost, secondQuote.RuleRevision, secondQuote.Key } };
        // Equal measured headroom after actual payments; the70-unit debit's
        // freed storage is explicitly removed as a source of apparent ROI.
        none = new Branch(seed, Synthetic(none.Game.State, 40, 40, 30));
        one = new Branch(seed, Synthetic(one.Game.State, 40, 40, 30));
        two = new Branch(seed, Synthetic(two.Game.State, 40, 40, 30));
        int recovered = -1; var bonusRows = new List<object>(); long extraParts = 0, extraPotatoes = 0;
        for (int i = 1; i <= 158; i++)
        {
            foreach (var branch in new[] { none, one, two }) { branch.Drain(); branch.BoostWhenReady(); }
            var a = none.Spin(); var b = one.Spin(); var c = two.Spin(); SameTrace(none, one, a, b); SameTrace(none, two, a, c);
            Need(a.Parts == b.Parts && a.Potatoes == b.Potatoes && a.Energy == b.Energy, "BalancedI changed no-bench production");
            int multiplier = c.Boosted ? 2 : 1, expected = c.BonusSymbol >= 0 ? multiplier : 0;
            Need(c.Parts - b.Parts == expected && c.Potatoes - b.Potatoes == expected && c.Energy == b.Energy,
                "BenchII marginal is not exactly1+1 before boost/caps");
            extraParts = two.CreditedParts - one.CreditedParts; extraPotatoes = two.CreditedPotatoes - one.CreditedPotatoes;
            if (c.BonusSymbol >= 0) bonusRows.Add(new { futureSpin = i, c.BonusSymbol, c.Boosted, extraParts, extraPotatoes,
                totalMarginalUnits = extraParts + extraPotatoes });
            if (extraParts + extraPotatoes >= 70) { recovered = i; break; }
        }
        MarginalRows.Add(new { seed, population = "synthetic funded paired production", fundedStartHash = StateHash(funded),
            recoverySpin = recovered, target60to200 = recovered >= 60 && recovered <= 200, censoredAt158 = recovered < 0,
            extraParts, extraPotatoes, bothCurrencyCostsRecovered = extraParts >= 40 && extraPotatoes >= 30,
            opportunityBoundary = "70 combined resource units is the declared analytical screen; it does not mean the40Parts/30Potatoes basket is separately recovered or that currencies can be exchanged into one another.",
            paidSetup, syntheticPostPurchaseBalances = "all three40Parts/40Potatoes/30Energy; actual ownership and primary/bonus history retained",
            noBench = none.Facts(), benchI = one.Facts(), benchII = two.Facts(), bonusRows });
        SummaryCsv.Append("marginal,").Append(seed).Append(",benchII-vs-I,").Append(two.Spins).Append(',').Append(recovered >= 60 && recovered <= 200)
            .Append(",,,,").Append(extraParts).Append(',').Append(extraPotatoes).Append(",\n");
        Console.WriteLine("DONE marginal seed=" + seed + " recovery=" + recovered + " units=" + (extraParts + extraPotatoes));
    }
    static void Focus(uint seed, ModelState source, bool partsCapped, bool bothCapped = false)
    {
        var funded = Synthetic(source, 200, 200, 30, false);
        var branches = new[] { new Branch(seed, funded), new Branch(seed, funded), new Branch(seed, funded) };
        string[] focus = { "none-balanced", "focus.parts", "focus.potatoes" };
        for (int i = 1; i < 3; i++)
        {
            branches[i].Command(branches[i].Offer("workbench.bonus.1"));
            branches[i].Command(branches[i].Game.GetExpansionAction("focus", focus[i]));
        }
        // New validated private branches reset only the disclosed balances/armed
        // state after paying real quotes, retaining actual ownership and history.
        for (int i = 0; i < 3; i++) branches[i] = new Branch(seed, Synthetic(branches[i].Game.State, partsCapped ? 200 : 0, bothCapped ? 200 : 0, 0, false));
        var rows = new List<object>(); int[] creditedUnits = null; bool? conserved = null, capAvoided = null;
        for (int future = 1; future <= 5; future++)
        {
            var receipts = branches.Select(b => b.Spin()).ToArray();
            SameTrace(branches[0], branches[1], receipts[0], receipts[1]); SameTrace(branches[0], branches[2], receipts[0], receipts[2]);
            rows.Add(new { futureSpin = future, bonusSymbol = receipts[0].BonusSymbol,
                credited = receipts.Select((r, i) => new { focus = focus[i], r.BonusParts, r.BonusPotatoes, r.Parts, r.Potatoes, r.Boosted }).ToArray() });
            if (receipts[0].BonusSymbol < 0) continue;
            creditedUnits = receipts.Select(r => r.BonusParts + r.BonusPotatoes).ToArray();
            Need(receipts.All(r => !r.Boosted), "Prepared no-boost focus control consumed a boost");
            if (!partsCapped)
            {
                conserved = creditedUnits[0] == creditedUnits[1] && creditedUnits[0] == creditedUnits[2];
                Need(conserved.Value && receipts[1].BonusPotatoes == 0 && receipts[2].BonusParts == 0, "Uncapped focus lost raw units or misrouted them");
            }
            else
            {
                capAvoided = creditedUnits[2] > creditedUnits[0];
                if (bothCapped) Need(creditedUnits.All(n => n == 0), "Both-full control unexpectedly credited bonus units");
            }
            break;
        }
        FocusRows.Add(new { seed, population = "synthetic prepared focus control", caseId = bothCapped ? "both-resources-full" : partsCapped ? "full-parts-zero-potatoes" : "uncapped-zero-balances",
            creditedUnits, uncappedTotalConserved = conserved, potatoFocusAvoidedActualCapLoss = capAvoided,
            startingHashes = branches.Select(b => b.StartingHash).ToArray(), rows, finalFacts = branches.Select(b => b.Facts()).ToArray() });
        SummaryCsv.Append("focus,").Append(seed).Append(',').Append(bothCapped ? "both-full" : partsCapped ? "full-parts" : "uncapped").Append(',').Append(branches[0].Spins)
            .Append(',').Append(partsCapped ? capAvoided?.ToString() : conserved?.ToString()).Append(',')
            .Append(creditedUnits == null ? "" : string.Join(",", creditedUnits)).Append(",,,\n");
        Console.WriteLine("DONE focus seed=" + seed + " capped=" + partsCapped + " avoided=" + capAvoided + " conserved=" + conserved);
    }
    static void Catalog()
    {
        void Offer(string id, string kind, int parts, int potatoes, long coins = 0)
        {
            var offer = ExpansionCatalog.Get(id);
            Need(offer.Kind == kind && offer.PartsCost == parts && offer.PotatoesCost == potatoes && offer.CoinsReward == coins &&
                offer.CoinsCost == 0 && offer.EnergyCost == 0, "Literal old/new offer changed: " + id);
        }
        Offer("module.salvage", "module", 12, 0); Offer("module.packing", "module", 12, 0); Offer("module.harvest", "module", 8, 8);
        Offer("module.combo", "module", 20, 10); Offer("module.dynamo", "module", 12, 8);
        Offer("socket.2", "socket", 30, 0); Offer("socket.3", "socket", 60, 30);
        foreach (string id in new[] { "theme.amber", "theme.mint", "theme.violet" }) Offer(id, "theme", 15, 5);
        Offer("bonus.unlock", "bonus", 20, 10); Offer("potato.yield.1", "yield", 15, 15); Offer("potato.yield.2", "yield", 30, 30);
        Offer("trade.parts", "trade", 20, 0, 20); Offer("trade.potatoes", "trade", 0, 20, 20);
        foreach (string family in new[] { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" })
        { Offer("equipment." + family + ".1", "equipment", family == "workshop" ? 25 : 20, family == "workshop" ? 15 : 10); Offer("equipment." + family + ".2", "equipment", 40, 20); }
        Offer("workbench.bonus.1", "workbench", 30, 20); Offer("workbench.bonus.2", "workbench", 40, 30);
        Need(EconomyRules.ExpansionEnergyCap == 30 && EconomyRules.ExpansionBoostCost == 15 && EconomyRules.ExpansionBonusCadence == 5 &&
            ConstructionPricing.Price("house.improve", ConstructionPricing.LegacyRevision) == 600 &&
            ConstructionPricing.Price("house.improve", ConstructionPricing.RevisionV2) == 540 && ChapterEconomy.ProjectPrice("house.improve") == 480,
            "Old r13/historical construction terms changed");
        int[] parts = { 5, 0, 3, 8 }, potatoes = { 0, 5, 3, 2 };
        for (int i = 0; i < 4; i++) Need(EconomyRules.ExpansionBonusPartsFor(i) == parts[i] && EconomyRules.ExpansionBonusPotatoesFor(i) == potatoes[i], "Old bonus table changed");
    }
    internal static int Run(string[] args)
    {
        string output = null; object sourceInputs = null;
        try
        {
            Need(args.Length == 6 && args[0] == "--source-root" && args[2] == "--source-commit" && args[4] == "--output", "Use --workbench --source-root <immutable Core> --source-commit <commit> --output <new prefix>");
            string root = Path.GetFullPath(args[1]); output = Path.GetFullPath(args[5]);
            Need(!File.Exists(output + ".json") && !File.Exists(output + ".csv") && !File.Exists(output + ".failure.json"), "Preserve first workbench receipts");
            sourceInputs = new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance" }
                .Select(g => Path.Combine(root, "Unity/Assets/Scripts", g)).SelectMany(g => Directory.GetFiles(g, "*.cs"))
                .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), p => Hash(File.ReadAllBytes(p)));
            Catalog();
            foreach (uint seed in Seeds)
            {
                var source = Natural(seed);
                if (source == null) continue; // Retain natural target MISS; do not inject an earned purchase.
                Marginal(seed, source); Focus(seed, source, false); Focus(seed, source, true); Focus(seed, source, true, true);
            }
            Need(calls <= CallCap && Timer.Elapsed.TotalSeconds < WallCap, "Final declared workbench bound exceeded");
            var report = new { schema = 1, sourceCommit = args[3], sourceInputs, utilityAssemblySha256 = Hash(File.ReadAllBytes(typeof(WorkbenchProbe).Assembly.Location)),
                checks, actualForegroundCalls = calls, wallSeconds = Timer.Elapsed.TotalSeconds,
                bounds = new { maximumCalls = CallCap, seconds = WallCap, jsonAndCsvBytes = 512 * 1024 },
                scope = "One fixed workbench candidate; actual production GameStore/strict Codec in memory; earned natural path separated from synthetic prepared controls; no Unity/native/human-play claim",
                natural = NaturalRows, marginal = MarginalRows, focus = FocusRows };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            string csv = SummaryCsv.ToString();
            Need(Encoding.UTF8.GetByteCount(json) + Encoding.UTF8.GetByteCount(csv) <= 512 * 1024, "512KiB combined output bound exceeded");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".json", json); File.WriteAllText(output + ".csv", csv);
            Console.WriteLine("PASS " + checks + " checks (JSON excludes final output-size check); " + calls + " foreground calls"); return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (output != null && !File.Exists(output + ".failure.json"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output + ".failure.json", JsonSerializer.Serialize(new {
                    error = error.ToString(), sourceInputs, checks, calls, lastPrivateSnapshot = activeStorage?.Raw,
                    natural = NaturalRows, marginal = MarginalRows, focus = FocusRows }, new JsonSerializerOptions { WriteIndented = true }));
            }
            return 1;
        }
    }
}
#endif
