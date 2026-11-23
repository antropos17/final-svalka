using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

namespace ExpansionPacing
{
    // Opt-in research only. No ExpansionDriver, policy forks, Unity dependency,
    // hidden challenge information or production rule implementation.
    internal static class DecisionCadence
    {
        const long Epoch = 1700000000;
        static readonly string[] Policies = { "construction-baseline", "affordable-skills-first", "one-u1-sensitivity" };
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static readonly Stopwatch Clock = new Stopwatch();
        static readonly List<Case> Cases = new List<Case>();
        static readonly List<string> Draws = new List<string>();
        static string root, output, budgetPath;
        static Budget budget = new Budget();
        static double priorWall;
        static int assertions, quoteReads;
        static Choice chosen;
        sealed class Limit : Exception { public Limit(string why) : base(why) { } }
        sealed class Budget { public int ForegroundCalls { get; set; } public double WallSeconds { get; set; } }
        sealed class Storage : ISnapshotStorage
        {
            public string Raw; public int Writes;
            public string ReadPrimary() => Raw; public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; Writes++; }
        }
        sealed class ActionRow
        {
            public string Kind { get; set; } public string Id { get; set; } public int Spin { get; set; }
            public long BeforeSequence { get; set; } public long Sequence { get; set; } public long At { get; set; }
            public long BeforeCoins { get; set; } public long Coins { get; set; } public long Income { get; set; } public long Debit { get; set; }
            public string Selected { get; set; } public long SelectedPrice { get; set; } public uint Seed { get; set; }
            public string BeforeSelected { get; set; } public long BeforeSelectedPrice { get; set; } public string QuoteOperation { get; set; }
            public int Parts { get; set; } public int Potatoes { get; set; } public int Energy { get; set; }
            public int PartsCap { get; set; } public int PotatoesCap { get; set; } public int EnergyCap { get; set; }
            public string Era { get; set; } public int Owned { get; set; } public int Skills { get; set; }
            public int Tickets { get; set; } public string StateHash { get; set; }
        }
        sealed class Interval
        {
            public string Id { get; set; } public int StartSpin { get; set; } public int EndSpin { get; set; } public int Gap { get; set; }
            public long Price { get; set; } public string CommitKind { get; set; }
            public int StartCommand { get; set; } public int EndCommand { get; set; }
            public string StartHash { get; set; } public string EndHash { get; set; }
        }
        sealed class Opportunity
        {
            public string Key { get; set; } public string Terms { get; set; }
            public int StartSpin { get; set; } public long StartSequence { get; set; }
            public int? EndSpin { get; set; } public long? EndSequence { get; set; }
            public bool OpenAtStop { get; set; } public string Disposition { get; set; }
        }
        sealed class Case
        {
            public string Policy { get; set; } public string Status { get; set; } = "unexecuted";
            public string StopReason { get; set; } public int Spins { get; set; } public long Income { get; set; }
            public long ConstructionSpend { get; set; } public long SkillSpend { get; set; }
            public int Skills { get; set; } public long Wallet { get; set; } public int Constructions { get; set; }
            public int ReadOnlyObservations { get; set; } public int PolicyActions { get; set; } public int Acknowledgements { get; set; }
            public int DeferralOnsets { get; set; } public int MaximumGap { get; set; }
            public double NormalManualProjectionSeconds { get; set; } public double FastManualProjectionSeconds { get; set; }
            public double NormalAutoAnimationProjectionSeconds { get; set; } public double FastAutoAnimationProjectionSeconds { get; set; }
            public List<ActionRow> Actions { get; set; } = new List<ActionRow>();
            public List<Interval> Intervals { get; set; } = new List<Interval>();
            public List<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
            public object FinalResources { get; set; }
        }
        sealed class Choice
        {
            public string Policy { get; set; } public Interval Interval { get; set; }
            public string StartRaw { get; set; } public string EndRaw { get; set; }
            public List<ActionRow> Prefix { get; set; } public List<ActionRow> Commands { get; set; }
        }
        static void Need(bool yes, string why) { assertions++; if (!yes) throw new InvalidOperationException(why); }
        static string Hash(byte[] raw) => Convert.ToHexString(SHA256.HashData(raw));
        static string Hash(string raw) => Hash(Encoding.UTF8.GetBytes(raw));
        static double Elapsed => priorWall + Clock.Elapsed.TotalSeconds;
        static void Guard()
        {
            // Reserve five seconds of the original90s for bounded partial flush.
            if (Elapsed >= 85) throw new Limit("original90s process allowance: flush reserve reached");
            if (budget.ForegroundCalls >= 2400) throw new Limit("original2400 foreground/auxiliary command cap");
        }
        static void SaveBudget()
        {
            budget.WallSeconds = Elapsed;
            string temp = budgetPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(budget)); File.Move(temp, budgetPath, true);
        }
        static void AccountForeground() { Guard(); budget.ForegroundCalls++; SaveBudget(); }
        static SortedDictionary<string, string> Inputs()
        {
            var paths = new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance", "Expansion" }
                .Select(x => Path.Combine(root, "Unity/Assets/Scripts", x)).Where(Directory.Exists)
                .SelectMany(x => Directory.GetFiles(x, "*.cs"));
            paths = paths.Concat(Directory.GetFiles(Path.Combine(root, "Checks/ExpansionPacing"), "*.cs"));
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string p in paths) result.Add(Path.GetRelativePath(root, p).Replace('\\', '/'), Hash(File.ReadAllBytes(p)));
            return result;
        }
        static void Execute(Case result)
        {
            result.Status = "running";
            var store = new Storage(); var game = GameSession.Open(new GameStore(store, Codec), 1, initializeJourney: true);
            Need(!game.RecoveryRequired && game.State.coins == 37 && game.State.seed == 1, "Fresh37 seed1 required");
            int previousSpin = 0, startCommand = 0; string startRaw = Codec.Encode(game.State);
            uint primary = 1;
            var active = new Dictionary<string, Opportunity>();
            long Now() => Epoch + result.Spins * 4L;
            void Balance()
            {
                var s = game.State;
                Need(!game.RecoveryRequired && s.pending == null && s.coins == 37 + result.Income - result.ConstructionSpend - result.SkillSpend,
                    "Independent gross-income/debit wallet ledger");
                Need(s.seed == primary, "Non-spin changed primary RNG");
            }
            void Observe()
            {
                Guard(); var s = game.State; string raw = Codec.Encode(s); int writes = store.Writes;
                var c = ChapterEconomy.Project(s); var e = ExpansionEconomy.Project(s); var a = ArcadeEconomy.Project(s);
                var current = new Dictionary<string, string>();
                foreach (string id in ChapterCatalog.Rules.AvailableUpgrades(c.Progress))
                {
                    long price = ChapterEconomy.ProjectPrice(id); current["construction-select/" + id] = "legal selection; price=" + price;
                    if (s.coins >= price) current["construction-pay/" + id] = "affordable immediate select; coins=" + price;
                }
                foreach (var node in SkillTree.Nodes)
                {
                    var q = ChapterEconomy.QuoteSkill(s, node.Id); quoteReads++;
                    if (q.Status == OfferStatus.Available || q.Status == OfferStatus.InsufficientFunds) current["skill-inspect/" + node.Id] = "unlocked; coins=" + node.Price;
                    if (q.Status == OfferStatus.Available) current["skill-buy/" + node.Id] = "affordable; coins=" + node.Price;
                }
                foreach (var offer in ExpansionCatalog.Offers)
                {
                    if (e.Parts < offer.PartsCost || e.Potatoes < offer.PotatoesCost) continue;
                    try { var q = ExpansionEconomy.Quote(s, "buy", offer.Id); quoteReads++;
                        current["resource-buy/" + offer.Id] = $"parts={q.PartsCost};potatoes={q.PotatoesCost};energy={q.EnergyCost};coinsDebit={q.CoinsCost};coinsCredit={q.CoinsReward};onsetQuote={q.Key}"; }
                    catch (InvalidOperationException) { quoteReads++; }
                }
                foreach (var offer in ArcadeCatalog.Offers)
                {
                    if (a.Diamonds < offer.DiamondsCost || e.Parts < offer.PartsCost || e.Potatoes < offer.PotatoesCost) continue;
                    try { var q = ArcadeEconomy.Quote(s, "buy", offer.Id); quoteReads++;
                        current["arcade-buy/" + offer.Id] = $"parts={q.PartsCost};potatoes={q.PotatoesCost};diamonds={q.DiamondsCost};onsetQuote={q.Key}"; }
                    catch (InvalidOperationException) { quoteReads++; }
                }
                foreach (string id in new[] { ArcadeCatalog.Memory, ArcadeCatalog.Sorting })
                    try { var q = ArcadeEconomy.Quote(s, "start", id); quoteReads++; current["arcade-start/" + id] = "tickets=" + q.TicketsCost + ";onsetQuote=" + q.Key; }
                    catch (InvalidOperationException) { quoteReads++; }
                // Free presentation speed is an optional mutation; exact current
                // identity is excluded. No resource/Arcade option is exercised.
                if (e.SpeedId != "speed.fast")
                    try { var q = ExpansionEconomy.Quote(s, "speed", "speed.fast"); quoteReads++; current["speed/speed.fast"] = "free; current=" + e.SpeedId + ";onsetQuote=" + q.Key; }
                    catch (InvalidOperationException) { quoteReads++; }
                try { var q = ExpansionEconomy.Quote(s, "boost", ExpansionCatalog.NextBonusBoost); quoteReads++; current["energy-boost"] = "energy=" + q.EnergyCost + ";onsetQuote=" + q.Key; }
                catch (InvalidOperationException) { quoteReads++; }
                foreach (string key in active.Keys.Where(k => !current.ContainsKey(k)).ToArray())
                { active[key].EndSpin = result.Spins; active[key].EndSequence = s.sequence; active.Remove(key); }
                foreach (var pair in current)
                    if (!active.ContainsKey(pair.Key))
                    {
                        var op = new Opportunity { Key = pair.Key, Terms = pair.Value, StartSpin = result.Spins, StartSequence = s.sequence,
                            Disposition = pair.Key.StartsWith("construction-") || pair.Key.StartsWith("skill-") ? "policy-contingent; see command ledger" : "deferred; usefulness/attention unobserved" };
                        active.Add(pair.Key, op); result.Opportunities.Add(op);
                        if (op.Disposition.StartsWith("deferred")) result.DeferralOnsets++;
                    }
                Need(store.Writes == writes && Codec.Encode(game.State) == raw, "Observer mutated state/time/RNG/journal"); result.ReadOnlyObservations++;
            }
            void Command(string kind, string id, Action action, long skillCost = 0, string quoteOperation = null)
            {
                Guard(); var before = game.State; var cBefore = game.Chapter;
                int owned = cBefore.Progress.CompletedUpgradeIds.Count; string selected = cBefore.SelectedProjectId;
                long selectedPrice = selected == null ? 0 : ChapterEconomy.SelectedProjectPrice(cBefore);
                if (kind == "spin") { if (result.Spins >= 800) throw new Limit("per-case800 foreground cap"); AccountForeground(); result.Spins++; }
                action(); var after = game.State; var chapter = game.Chapter; var expansion = game.Expansion; var arcade = game.Arcade;
                long income = 0, debit = skillCost;
                if (kind == "spin")
                {
                    var spin = after.lastSpin; var draw = GameLogic.Draw(primary, before.sequence + 1); primary = draw.seedAfter;
                    Need(spin.symbols.SequenceEqual(draw.symbols) && after.seed == primary && spin.reward == GameLogic.Payout(draw.symbols), "Primary outcome identity");
                    string identity = primary + ":" + string.Join("", spin.symbols) + ":" + spin.reward;
                    if (result.Spins <= Draws.Count) Need(Draws[result.Spins - 1] == identity, "Matched-spin primary draw drift across policies"); else Draws.Add(identity);
                    income = spin.reward + spin.bonus + spin.chapter.Effects.TotalCoins;
                    Need(arcade.LastResourceReceipt != null && arcade.LastResourceReceipt.Sequence == after.sequence && arcade.LastResourceReceipt.CoinIncome == income, "Actual gross receipt differs");
                    result.Income += income;
                }
                int count = chapter.Progress.CompletedUpgradeIds.Count;
                Need(count == owned || count == owned + 1, "One construction per command");
                if (count == owned + 1)
                {
                    string built = kind == "spin" ? selected : id; long price = kind == "spin" ? selectedPrice : ChapterEconomy.ProjectPrice(id);
                    Need(built != null && chapter.Progress.CompletedUpgradeIds.Contains(built), "Actual owned construction missing");
                    if (kind == "spin") Need(after.lastSpin.chapter.Price == price && arcade.LastResourceReceipt.ConstructionDebit == price, "Saved price/debit not frozen");
                    debit += price; result.ConstructionSpend += price;
                }
                if (kind == "skill")
                {
                    result.SkillSpend += skillCost;
                    Need(chapter.SelectedProjectId == selected && (selected == null || ChapterEconomy.SelectedProjectPrice(chapter) == selectedPrice), "Skill changed selected quote");
                }
                Need(after.coins == before.coins + income - debit, "Actual per-command debit/credit ledger");
                string encoded = Codec.Encode(after);
                result.Actions.Add(new ActionRow { Kind = kind, Id = id, Spin = result.Spins, BeforeSequence = before.sequence, Sequence = after.sequence,
                    At = Now(), BeforeCoins = before.coins, Coins = after.coins, Income = income, Debit = debit, Seed = after.seed,
                    Selected = chapter.SelectedProjectId, SelectedPrice = chapter.SelectedProjectId == null ? 0 : ChapterEconomy.SelectedProjectPrice(chapter),
                    BeforeSelected = selected, BeforeSelectedPrice = selectedPrice, QuoteOperation = quoteOperation,
                    Parts = expansion.Parts, Potatoes = expansion.Potatoes, Energy = expansion.Energy, PartsCap = expansion.PartsCap, PotatoesCap = expansion.PotatoesCap, EnergyCap = expansion.EnergyCap,
                    Era = chapter.Progress.EraId, Owned = count, Skills = chapter.Skills.Receipts.Count, Tickets = arcade.Tickets, StateHash = Hash(encoded) });
                if (kind == "era-ack") result.Acknowledgements++; else if (kind != "spin") result.PolicyActions++;
                Balance();
                if (count == owned + 1)
                {
                    var interval = new Interval { Id = kind == "spin" ? selected : id, StartSpin = previousSpin, EndSpin = result.Spins,
                        Gap = result.Spins - previousSpin, Price = debit, CommitKind = kind, StartCommand = startCommand,
                        EndCommand = result.Actions.Count, StartHash = Hash(startRaw), EndHash = Hash(encoded) };
                    result.Intervals.Add(interval); result.MaximumGap = Math.Max(result.MaximumGap, interval.Gap);
                    // Predeclared maximum gap; ties keep declared policy order,
                    // then first encountered interval. Never select a new seed.
                    if (chosen == null || interval.Gap > chosen.Interval.Gap)
                        chosen = new Choice { Policy = result.Policy, Interval = interval, StartRaw = startRaw, EndRaw = encoded,
                            Prefix = result.Actions.Take(startCommand).ToList(), Commands = result.Actions.Skip(startCommand).ToList() };
                    previousSpin = result.Spins; startCommand = result.Actions.Count; startRaw = encoded;
                }
                Observe();
            }
            try
            {
                Observe();
                while (!ChapterCatalog.Rules.IsChapterComplete(game.Chapter.Progress))
                {
                    Guard(); var chapter = game.Chapter;
                    if (chapter.TransitionPending) { Command("era-ack", null, () => game.AcknowledgeEraTransition(game.State.sequence + 1, Now())); continue; }
                    if (ChapterCatalog.Rules.CanUnlockWorkshopYard(chapter.Progress)) { Command("enter-era", null, () => game.EnterWorkshopYard(game.State.sequence + 1, Now())); continue; }
                    SkillQuote skill = null;
                    if (result.Policy == Policies[1]) skill = SkillTree.Nodes.Select(n => ChapterEconomy.QuoteSkill(game.State, n.Id)).Where(q => q.Status == OfferStatus.Available).OrderBy(q => q.Node.Price).ThenBy(q => q.Node.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (result.Policy == Policies[2] && ChapterCatalog.Rules.Stages(chapter.Progress).AbsoluteLevel(FamilyIds.Workshop) >= 1)
                    { var q = ChapterEconomy.QuoteSkill(game.State, GameIds.SlotCoinBoost); if (q.Status == OfferStatus.Available) skill = q; }
                    if (skill != null) { var q = skill; Command("skill", q.Node.Id, () => game.BuySkill(q.Request, Now()), q.Node.Price, q.Request.OperationId); continue; }
                    if (chapter.SelectedProjectId == null)
                    {
                        string id = ChapterCatalog.Rules.AvailableUpgrades(chapter.Progress).OrderBy(ChapterEconomy.ProjectPrice).ThenBy(x => x, StringComparer.Ordinal).First();
                        Command("select", id, () => game.SelectChapterProject(id, game.State.sequence + 1, Now()));
                        if (game.Chapter.SelectedProjectId == null) continue;
                    }
                    Command("spin", null, () => game.ExecuteChapterSpin(game.State.sequence + 1, Now(), manualSpin: true));
                }
                Need(game.Chapter.Progress.CompletedUpgradeIds.Count == 24 && result.ConstructionSpend == 9310, "Current24 construction terms");
                Need(result.Policy != Policies[0] || result.SkillSpend == 0, "Baseline skill spend");
                Need(result.Policy != Policies[2] || result.SkillSpend == (game.Chapter.Skills.Receipts.Count == 0 ? 0 : 120) && game.Chapter.Skills.Receipts.Count <= 1, "Single-U1 spend");
                Need(game.Chapter.Skills.Receipts.Count != 6 || result.SkillSpend == 610, "Six actual skills total"); result.Status = "complete";
            }
            catch (Limit stop) { result.Status = "censored"; result.StopReason = stop.Message; }
            finally
            {
                foreach (var op in active.Values) op.OpenAtStop = true;
                result.Constructions = game.Chapter.Progress.CompletedUpgradeIds.Count; result.Skills = game.Chapter.Skills.Receipts.Count; result.Wallet = game.State.coins;
                var e = game.Expansion; var a = game.Arcade;
                result.FinalResources = new { e.Parts, e.PartsCap, e.Potatoes, e.PotatoesCap, e.Energy, e.EnergyCap, e.ForegroundSpins, e.BonusProgress, e.BonusUnlocked, e.SpeedId, a.Diamonds, a.Tickets, a.TicketProgress, a.SlotLevel, activeRun = a.ActiveRun?.Id };
                result.NormalManualProjectionSeconds = result.Spins * 3.5; result.FastManualProjectionSeconds = result.Spins * 1.5;
                // Animation plus nominal inter-spin gaps only; menus, era stops,
                // manual choices and restart dispatches are deliberately excluded.
                result.NormalAutoAnimationProjectionSeconds = result.Spins * 3.5 + Math.Max(0, result.Spins - 1) * .25;
                result.FastAutoAnimationProjectionSeconds = result.Spins * 1.5 + Math.Max(0, result.Spins - 1) * .25;
            }
        }
        internal static int Run(string[] args)
        {
            string error = null; object checkpoint = null; SortedDictionary<string, string> inputs = null; string commit = null, manifestHash = null;
            int exit = 0; Clock.Restart();
            try
            {
                Need(args.Length == 8 && args[0] == "--source-root" && args[2] == "--source-commit" && args[4] == "--input-manifest" && args[6] == "--output", "Use --decision-cadence --source-root <frozen> --source-commit <commit> --input-manifest <build manifest> --output <new prefix>");
                root = Path.GetFullPath(args[1]); commit = args[3]; output = Path.GetFullPath(args[7]);
                Need(!File.Exists(output + ".json") && !File.Exists(output + ".csv"), "Never overwrite a prior screen"); Directory.CreateDirectory(Path.GetDirectoryName(output));
                budgetPath = Path.Combine(Path.GetDirectoryName(output), "original-budget.json");
                if (File.Exists(budgetPath)) budget = JsonSerializer.Deserialize<Budget>(File.ReadAllText(budgetPath)); priorWall = budget.WallSeconds;
                inputs = Inputs(); using (var manifest = JsonDocument.Parse(File.ReadAllText(args[5])))
                {
                    var m = manifest.RootElement; Need(m.GetProperty("assemblySha256").GetString() == Hash(File.ReadAllBytes(typeof(DecisionCadence).Assembly.Location)), "Built assembly hash differs");
                    foreach (var p in m.GetProperty("compiledInputs").EnumerateObject()) Need(inputs.ContainsKey(p.Name) && inputs[p.Name] == p.Value.GetString(), "Actual compile input differs: " + p.Name);
                    Need(m.GetProperty("compiledInputs").EnumerateObject().Count() == inputs.Count, "Compile input set differs");
                }
                manifestHash = Hash(File.ReadAllBytes(args[5]));
                foreach (string policy in Policies) Cases.Add(new Case { Policy = policy });
                foreach (var run in Cases)
                { if (Elapsed >= 85 || budget.ForegroundCalls >= 2400) { run.StopReason = "Original budget exhausted before case"; continue; } Execute(run); }
                if (chosen != null)
                {
                    string start = output + ".interval-start.json", end = output + ".interval-end.json";
                    Need(Hash(Codec.Encode(Codec.Decode(chosen.StartRaw))) == chosen.Interval.StartHash && Hash(Codec.Encode(Codec.Decode(chosen.EndRaw))) == chosen.Interval.EndHash, "Checkpoint roundtrip differs");
                    File.WriteAllText(start, chosen.StartRaw); File.WriteAllText(end, chosen.EndRaw);
                    string replay = "not applicable to an immediate transaction checkpoint"; var state = Codec.Decode(chosen.StartRaw);
                    if (state.lastSpin != null && state.lastSpin.sequence == state.sequence)
                    {
                        var storage = new Storage { Raw = chosen.StartRaw }; var g = GameSession.Open(new GameStore(storage, Codec), 1, initializeJourney: true);
                        int writes = storage.Writes; AccountForeground(); g.ExecuteChapterSpin(state.sequence, state.lastSettledAt, manualSpin: true);
                        Need(storage.Writes == writes && Codec.Encode(g.State) == chosen.StartRaw, "Exact retained spin replay mutated checkpoint"); replay = "same retained public spin request returns identical state with0IO; one auxiliary call counted";
                    }
                    checkpoint = new { chosen.Policy, chosen.Interval, startPath = start, endPath = end, prefix = chosen.Prefix, commands = chosen.Commands,
                        prefixHash = Hash(JsonSerializer.Serialize(chosen.Prefix)), commandHash = Hash(JsonSerializer.Serialize(chosen.Commands)), replay };
                }
                Need(JsonSerializer.Serialize(inputs) == JsonSerializer.Serialize(Inputs()), "Frozen source drifted during screen");
            }
            catch (Exception failure) { error = failure.ToString(); exit = 1; }
            finally
            {
                if (budgetPath != null) SaveBudget();
                if (output != null)
                {
                    var report = new { schema = 1, diagnostic = "decision cadence, seed1 only", sourceCommit = commit, sourceInputs = inputs, buildManifestHash = manifestHash,
                        assemblyHash = Hash(File.ReadAllBytes(typeof(DecisionCadence).Assembly.Location)), bounds = new { perCaseSpins = 800, totalForegroundIncludingAuxiliary = 2400, wallSeconds = 90, jsonBytes = 2097152, csvBytes = 524288 },
                        budget, assertions, quoteReads, error, policies = Policies, timing = "Nominal animation projections only, not measured play/reading time. AUTO gaps exclude menus, choices and explicit era/chapter stops/restarts.",
                        scope = "Resource/Arcade spending, mini-game starts/claims and free speed changes deferred identically. Persistent quote onset/expiry is opportunity, not usefulness/attention. No human observations.",
                        opportunitySemantics = "construction-select=legal selection; construction-pay=affordable immediate selection; skill-inspect=unlocked; *buy/start=current legal affordable quote. Speed is free optional mutation. Repeated sequence/key refreshes do not create new onset.",
                        primaryDrawIdentitiesBySpin = Draws, cases = Cases, selectedUiInterval = checkpoint };
                    string json = JsonSerializer.Serialize(report); var csv = new StringBuilder("policy,id,start_spin,end_spin,gap,price,commit_kind\n");
                    foreach (var c in Cases) foreach (var i in c.Intervals) csv.Append(c.Policy).Append(',').Append(i.Id).Append(',').Append(i.StartSpin).Append(',').Append(i.EndSpin).Append(',').Append(i.Gap).Append(',').Append(i.Price).Append(',').Append(i.CommitKind).Append('\n');
                    if (Encoding.UTF8.GetByteCount(json) > 2097152 || Encoding.UTF8.GetByteCount(csv.ToString()) > 524288)
                    { json = JsonSerializer.Serialize(new { error = "Output cap; full result withheld", budget, cases = Cases.Select(c => new { c.Policy, c.Status, c.Spins, c.Constructions }) }); exit = 1; }
                    File.WriteAllText(output + ".json", json); File.WriteAllText(output + ".csv", csv.ToString());
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { exit, calls = budget.ForegroundCalls, wallSeconds = budget.WallSeconds, cases = Cases.Select(c => new { c.Policy, c.Status, c.Spins, c.Constructions, c.SkillSpend, c.MaximumGap }), error }));
            return exit;
        }
    }
}
