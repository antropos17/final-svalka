using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Upgrades;
using JunkToNeon.Orders;
using JunkToNeon.Guidance;

namespace CommandSequenceLab;

sealed class CheckFailure(string signature, string detail) : Exception(detail)
{
    public string Signature { get; } = signature;
}
sealed class MemoryStore : ISnapshotStorage
{
    public string Primary, Backup;
    public int Writes;
    public string ReadPrimary() => Primary;
    public string ReadBackup() => Backup;
    public void Write(string text)
    {
        if (text.Length > 65536) throw new CheckFailure("harness.storage.bound", "Snapshot exceeded production size bound");
        Backup = Primary; Primary = text; Writes++;
    }
}
sealed record Command(string Id, string Kind, string Arg, long Sequence, long Instance = 0,
    long Completion = 0, string Expected = "commit");
sealed class Trace
{
    public int Schema { get; set; } = 1;
    public string SourceManifestHash { get; set; }
    public string Fixture { get; set; }
    public uint GameSeed { get; set; }
    public uint GenerationSeed { get; set; }
    public string InitialSnapshot { get; set; }
    public List<Command> Commands { get; set; } = new();
    public Dictionary<string, string> AfterHashes { get; set; } = new();
    public string FailureSignature { get; set; }
    public string FailureDetail { get; set; }
    public string BeforeSnapshot { get; set; }
    public string AfterSnapshot { get; set; }
}
sealed class Coverage
{
    public SortedDictionary<string, int> Categories { get; } = new();
    public SortedDictionary<string, int> Commands { get; } = new();
    public HashSet<string> States { get; } = new();
    public long Assertions;
    public void Hit(string key) { Categories[key] = Categories.GetValueOrDefault(key) + 1; }
    public void Count(string key) { Commands[key] = Commands.GetValueOrDefault(key) + 1; }
}
// Fixed algorithm rather than System.Random: generation is independent of game RNG and SDK versions.
sealed class Generator(uint seed)
{
    uint value = seed == 0 ? 1 : seed;
    public int Next(int max) { value = unchecked(value * 1664525u + 1013904223u); return (int)((value >> 8) % (uint)max); }
}

sealed class Lab
{
    public static readonly JsonStateCodec Codec = new();
    public static readonly string[] Skills = { SkillIds.Universal1, SkillIds.Universal2,
        SkillIds.Recycling1, SkillIds.Recycling2, SkillIds.Delivery1, SkillIds.Delivery2 };
    // Deliberately literal oracle tables; do not replace with production catalog/effect calculations.
    static readonly long[,] Prices = { {40,600,420,680}, {120,260,380,620}, {180,320,400,640},
        {240,360,440,660}, {160,300,360,580}, {200,340,460,700} };
    static readonly int[] FamilySymbols = {0,1,2,3,1,0};
    readonly MemoryStore storage;
    readonly Dictionary<string, SkillPurchaseRequest> quotes = new();
    public GameSession Session { get; private set; }
    public Coverage Coverage { get; }
    public Trace Trace { get; }
    long expectedWallet;
    public Lab(Trace trace, Coverage coverage)
    {
        Trace = trace; Coverage = coverage;
        storage = new MemoryStore { Primary = trace.InitialSnapshot };
        Session = Open(); expectedWallet = Session.State.coins;
    }
    GameSession Open()
    {
        var s = GameSession.Open(new GameStore(storage, Codec), Trace.GameSeed, initializeJourney: true);
        if (s.RecoveryRequired) throw new CheckFailure("harness.open", s.Error);
        return s;
    }
    void Need(bool condition, string signature, string detail)
    { Coverage.Assertions++; if (!condition) throw new CheckFailure(signature, detail); }
    public static long ProjectPrice(string id, string revision = "construction.prices.v2")
    {
        if (revision != "construction.prices.v1" && revision != "construction.prices.v2")
            throw new CheckFailure("harness.construction-revision", "Unknown independently supported revision: " + revision);
        for (int era = 0; era < 2; era++) for (int f = 0; f < 6; f++) for (int l = 1; l <= 2; l++)
            if (id == ChapterIds.Project(era == 0 ? EraIds.Junkyard : EraIds.WorkshopYard, FamilyIds.All[f], l))
                return id == GameIds.HouseImprove && revision == "construction.prices.v2" ? 540 : Prices[f, era * 2 + l - 1];
        throw new CheckFailure("harness.project", "No independent project price for " + id);
    }
    public static long SkillPrice(string id) => new long[] {120,160,160,90,60,20}[Array.IndexOf(Skills, id)];
    public static int[] Levels(ChapterState c) => FamilyIds.All.Select(f => ChapterCatalog.Rules.Stages(c.Progress).AbsoluteLevel(f)).ToArray();
    static int Base(int[] symbols) => symbols.Distinct().Count() switch { 1 => 23, 2 => 8, _ => 3 };
    public static uint SeedAfter(uint seed)
    {
        for (int i = 0; i < 3; i++) { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; }
        return seed;
    }
    public void Add(string kind, string arg = null, string expected = "commit", long? seq = null, long instance = 0, long completion = 0)
    {
        if (Trace.Commands.Count >= 150) throw new CheckFailure("harness.command.bound", "150-command bound");
        var c = new Command("c" + Trace.Commands.Count.ToString("D3"), kind, arg, seq ?? Session.State.sequence + 1, instance, completion, expected);
        Trace.Commands.Add(c); Apply(c);
    }
    public void Apply(Command c)
    {
        var before = Session.State; string beforeText = Codec.Encode(before), bytes = storage.Primary;
        int writes = storage.Writes;
        var chapter = Session.Chapter; var journey = Session.Journey;
        try
        {
            Exception refusal = null;
            try
            {
                switch (c.Kind)
                {
                    case "query":
                        _ = Session.Chapter; _ = Session.Journey; _ = Session.Guidance;
                        _ = ChapterCatalog.Rules.AvailableUpgrades(chapter.Progress).ToArray();
                        foreach (string id in Skills) _ = ChapterEconomy.QuoteSkill(before, id);
                        break;
                    case "quote": quotes[c.Id] = ChapterEconomy.QuoteSkill(before, c.Arg).Request; break;
                    case "buy":
                        if (!quotes.TryGetValue(c.Arg, out var request)) throw new CheckFailure("harness.quote.dependency", c.Arg);
                        Session.BuySkill(request, 1000); break;
                    case "spin": Session.ExecuteChapterSpin(c.Sequence, 1000); break;
                    case "select": Session.SelectChapterProject(c.Arg, c.Sequence, 1000); break;
                    case "enter": Session.EnterWorkshopYard(c.Sequence, 1000); break;
                    case "era-ack": Session.AcknowledgeEraTransition(c.Sequence, 1000); break;
                    case "accept": Session.AcceptOrder(c.Arg, c.Sequence, 1000); break;
                    case "cancel": Session.CancelOrder(c.Arg, c.Instance, c.Sequence, 1000); break;
                    case "claim": Session.ClaimOrder(c.Arg, c.Instance, c.Sequence, 1000); break;
                    case "ack": Session.AcknowledgeGuidance(c.Arg, c.Sequence, c.Completion, 1000); break;
                    case "reopen":
                        Need(Codec.Encode(Codec.Decode(bytes)) == beforeText, "codec.roundtrip", "Canonical snapshot changed");
                        Session = Open(); break;
                    default: throw new CheckFailure("harness.command", c.Kind);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException or OverflowException) { refusal = e; }
            var after = Session.State; string afterText = Codec.Encode(after);
            string result = refusal != null ? "refuse" : afterText == beforeText ? "unchanged" : "commit";
            Coverage.Count(c.Kind + ":" + result);
            Need(!Session.RecoveryRequired, "session.blocked", Session.Error ?? "Unexpected blocked session");
            // Even an unexpected refusal is checked for unauthorized writes before classification.
            if (result != "commit")
                Need(afterText == beforeText && storage.Primary == bytes && storage.Writes == writes,
                    "state.nonmutation", c.Kind + " " + result + " modified validated state/storage");
            Need(result == c.Expected, "classification." + c.Kind, "Expected " + c.Expected + ", got " + result + ": " + refusal?.Message);
            if (result == "commit")
            {
                Need(after.sequence == before.sequence + 1 && storage.Writes == writes + 2 && after.pending == null,
                    "journal.commit", "Successful command did not commit exactly one two-write operation");
                CheckCommit(c, before, after, chapter, journey);
            }
            Need(after.coins == expectedWallet, "wallet.history", "Expected independently accumulated wallet " + expectedWallet + ", actual " + after.coins);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(afterText))).ToLowerInvariant();
            if (Trace.AfterHashes.TryGetValue(c.Id, out string recorded))
                Need(hash == recorded, "replay.state-hash", "Exact replay diverged at " + c.Id);
            else Trace.AfterHashes[c.Id] = hash;
            if (c.Kind != "spin" || result != "commit")
                Need(before.seed == after.seed, "rng.nonspin", "Non-spin advanced RNG");
            if (result != "commit") Coverage.Hit(c.Kind == "reopen" ? "reopen.roundtrip" : c.Kind == "query" || c.Kind == "quote" ? "query.nonmutation" : result == "refuse" ? "refusal.nonmutation" : "replay.nonmutation");
            var levels = Levels(Session.Chapter);
            Coverage.States.Add(Session.Chapter.Progress.EraId + ":" + string.Join(",", levels) + ":skills" + Session.Chapter.Skills.Receipts.Count +
                ":target" + (Session.Chapter.SelectedProjectId ?? "none") + ":order" + (Session.Journey.Orders.ActiveOrderId ?? "none") + ":ready" + Session.Journey.Orders.Ready);
            if (levels.All(l => l == 5)) Coverage.Hit(Session.Chapter.Skills.Receipts.Count == 0 ? "chapter.complete.no-skills" : "chapter.complete.with-skills");
            if (Session.Chapter.TransitionPending) Coverage.Hit("era.pending");
        }
        catch (CheckFailure e)
        {
            Trace.FailureSignature = e.Signature + "@" + c.Id; Trace.FailureDetail = c.Id + " " + c.Kind + ": " + e.Message;
            Trace.BeforeSnapshot = beforeText; Trace.AfterSnapshot = Codec.Encode(Session.State);
            throw new CheckFailure(Trace.FailureSignature, Trace.FailureDetail);
        }
        catch (Exception e)
        {
            Trace.FailureSignature = "unexpected." + e.GetType().Name + "@" + c.Id;
            Trace.FailureDetail = c.Id + " " + c.Kind + ": " + e.Message;
            Trace.BeforeSnapshot = beforeText; Trace.AfterSnapshot = Codec.Encode(Session.State);
            throw new CheckFailure(Trace.FailureSignature, Trace.FailureDetail);
        }
    }
    void CheckCommit(Command c, ModelState before, ModelState after, ChapterState chapter, JourneyState journey)
    {
        var next = Session.Chapter; var orders = journey.Orders; var now = Session.Journey.Orders;
        var levels = Levels(chapter); var nextLevels = Levels(next);
        var expectedProjects = chapter.Progress.CompletedUpgradeIds.ToHashSet();
        var expectedSkills = chapter.Skills.Receipts.Select(r => r.Identity).ToHashSet();
        string selected = chapter.SelectedProjectId, bought = null;
        int deliveries = 0;
        long income = 0, debit = 0;
        if (c.Kind == "spin")
        {
            int[] symbols = after.lastSpin.symbols;
            Need(symbols.Length == 3 && symbols.All(s => s >= 0 && s < 4), "spin.symbols", "Invalid symbols");
            int[] counts = Enumerable.Range(0, 4).Select(s => symbols.Count(x => x == s)).ToArray();
            int carry = WaveProgress.Of(before).Delivery.PigeonProgress;
            int pigeons = levels[3] >= 1 ? counts[3] : 0;
            deliveries = (carry + pigeons) / 3;
            Need(WaveProgress.Of(after).Delivery.PigeonProgress == (carry + pigeons) % 3, "delivery.carry", "Carry not conserved");
            var owned = chapter.Skills.Receipts.Select(r => r.Request.NodeId).ToHashSet();
            int universal = owned.Count(s => s == Skills[0] || s == Skills[1]);
            int recycling = owned.Count(s => s == Skills[2] || s == Skills[3]);
            int delivery = owned.Count(s => s == Skills[4] || s == Skills[5]);
            income = Base(symbols) + (levels[1] >= 1 ? 1 : 0) + (levels[2] >= 1 ? counts[2] : 0) +
                deliveries * 3 + universal + recycling * counts[2] + delivery * deliveries;
            for (int f = 0; f < 6; f++)
            {
                // House first two levels have no symbol source; W/R/L first level is baseline.
                int sources = f == 0 ? Math.Max(0, levels[f] - 3) : f < 4 ? Math.Max(0, levels[f] - 1 - (levels[f] >= 3 ? 1 : 0)) : levels[f] - (levels[f] >= 3 ? 1 : 0);
                income += sources * counts[FamilySymbols[f]];
            }
            Need(after.lastSpin.reward == Base(symbols), "spin.base", "Exclusive payout mismatch");
            Need(after.seed == SeedAfter(before.seed), "rng.spin", "Spin did not advance exactly three xorshift draws");
            Coverage.Hit(selected == null ? "spin.no-target" : "spin.target");
            Coverage.Hit("spin.preowned.skills" + owned.Count);
            if (selected != null && before.coins + income >= ProjectPrice(selected, chapter.ConstructionPriceRevision)) bought = selected;
            if (selected == GameIds.HouseImprove && chapter.ConstructionPriceRevision == "construction.prices.v1")
                Coverage.Hit(bought == null ? "pricing.retained600.waiting" : "pricing.retained600.purchased");
        }
        if (c.Kind == "select")
        {
            selected = c.Arg;
            if (before.coins >= ProjectPrice(selected)) bought = selected;
            Coverage.Hit(bought == null ? "selection.insufficient" : "selection.immediate-purchase");
            if (chapter.SelectedProjectId != null && chapter.SelectedProjectId != selected) Coverage.Hit("selection.replace");
        }
        if (bought != null)
        {
            debit = ProjectPrice(bought, c.Kind == "spin" ? chapter.ConstructionPriceRevision : "construction.prices.v2");
            expectedProjects.Add(bought); selected = null;
            if (bought == GameIds.HouseImprove && c.Kind == "select") Coverage.Hit("pricing.current540.purchased");
            Need(Session.Journey.Guidance.LastCompletion?.ProjectId == bought && Session.Journey.Guidance.LastCompletion.Sequence == after.sequence,
                "guidance.completion", "Completion not bound to actual purchase sequence");
            if (c.Kind == "spin")
            {
                Coverage.Hit("purchase.acquisition-spin");
                if (bought == GameIds.WorkshopBuild || bought == GameIds.RecyclerBuild || bought == GameIds.PigeonLoftBuild)
                    Coverage.Hit("purchase.preowned-only." + bought);
            }
        }
        if (c.Kind == "buy")
        {
            var request = quotes[c.Arg]; debit = SkillPrice(request.NodeId);
            Need(next.Skills.Receipts.Count == chapter.Skills.Receipts.Count + 1 && next.Skills.Receipts.Last().Request.NodeId == request.NodeId && next.Skills.Receipts.Last().FrozenPrice == debit,
                "skill.receipt", "Wrong new skill debit/identity");
            expectedSkills.Add(next.Skills.Receipts.Last().Identity); Coverage.Hit("skill.purchase");
        }
        if (c.Kind == "enter")
        {
            Need(levels.All(l => l == 2) && nextLevels.All(l => l == 3) && next.TransitionPending,
                "era.entry", "Entry did not advance exactly2->3 with acknowledgement required");
            Coverage.Hit("era.entry");
        }
        else if (bought == null) Need(levels.SequenceEqual(nextLevels), "ownership.levels", "Unrequested level change");
        Need(expectedProjects.SetEquals(next.Progress.CompletedUpgradeIds), "ownership.projects", "Unexpected hidden/missing project");
        Need(expectedSkills.SetEquals(next.Skills.Receipts.Select(r => r.Identity)), "ownership.skills", "Unexpected skill ownership change");
        Need(next.SelectedProjectId == selected, "selection.identity", "Hidden successor or changed target");
        if (c.Kind != "enter") Need(chapter.Progress.EraId == next.Progress.EraId, "era.automatic", "Unrequested era change");
        Need(next.ConstructionPriceRevision == (c.Kind == "select" ? "construction.prices.v2" : chapter.ConstructionPriceRevision),
            "pricing.revision", "Only explicit selection may adopt current construction terms");
        if (c.Kind != "spin") Need(ChapterEconomy.SameDelivery(WaveProgress.Of(before).Delivery, WaveProgress.Of(after).Delivery), "delivery.nonspin", "Non-spin changed complete carry checkpoint");
        long reward = c.Kind == "claim" ? c.Arg == OrderIds.TrashSort ? 120 : 180 : 0;
        expectedWallet = checked(expectedWallet + income - debit + reward);
        if (c.Kind == "accept")
        {
            Need(orders.ActiveOrderId == null && now.ActiveOrderId == c.Arg && now.InstanceSequence == after.sequence && now.Progress == 0,
                "order.accept", "New instance must start zero with explicit acceptance identity"); Coverage.Hit("order.accept");
        }
        else if (c.Kind == "claim" || c.Kind == "cancel")
        {
            Need(now.ActiveOrderId == null && now.Progress == 0, "order.clear", "Order not cleared"); Coverage.Hit("order." + c.Kind);
        }
        else
        {
            int progress = orders.Progress;
            if (c.Kind == "spin" && orders.ActiveOrderId != null)
                progress = Math.Min(orders.ActiveOrderId == OrderIds.TrashSort ? 12 : 3,
                    progress + (orders.ActiveOrderId == OrderIds.TrashSort ? after.lastSpin.symbols.Count(s => s == 2) : deliveries));
            Need(now.ActiveOrderId == orders.ActiveOrderId && now.InstanceSequence == orders.InstanceSequence && now.Progress == progress,
                "order.progress", "Order advanced outside qualifying subsequent spin or wrong progress");
            if (c.Kind != "spin") Need(now.Cursor == orders.Cursor, "order.cursor", "Non-event advanced cursor");
            if (now.Ready) Coverage.Hit("order.ready." + now.ActiveOrderId);
        }
        if (c.Kind == "ack" && c.Arg == GuideIds.Completion)
            Need(Session.Journey.Guidance.CompletionAcknowledgedSequence == c.Completion,
                "guidance.ack", "Completion acknowledgement sequence mismatch");
        if (c.Kind == "era-ack") Coverage.Hit("era.acknowledged");
    }
}

static class Program
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    const int ReportLimit = 2 * 1024 * 1024;
    static string Root, Tool, ManifestHash;
    static readonly Stopwatch Timer = Stopwatch.StartNew();
    static readonly string[] Required = { "query.nonmutation", "selection.insufficient", "selection.immediate-purchase", "selection.replace",
        "spin.no-target", "spin.target", "purchase.acquisition-spin", "skill.purchase", "skill.stale-refused", "skill.retained-replay",
        "era.entry", "era.pending", "era.acknowledged", "era.blocked-spin", "era.blocked-select", "era.blocked-skill",
        "chapter.complete.no-skills", "chapter.complete.with-skills", "order.accept", "order.cancel", "order.claim", "order.old-claim-new-instance",
        "order.ready." + OrderIds.TrashSort, "order.ready." + OrderIds.PigeonDeliveries, "guidance.old-ack-refused", "reopen.roundtrip",
        "purchase.preowned-only." + GameIds.WorkshopBuild, "purchase.preowned-only." + GameIds.RecyclerBuild,
        "purchase.preowned-only." + GameIds.PigeonLoftBuild };
    static readonly string[] PricingRequired = { "pricing.current540.purchased", "pricing.retained600.waiting",
        "pricing.retained600.purchased", "pricing.explicit-reselection540", "pricing.paid600.replay" };
    static void Assert(bool condition, string message) { if (!condition) throw new CheckFailure("harness.selfcheck", message); }
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    static void WriteBounded(string dir, string name, object value)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        string reportRoot = Path.Combine(Tool, "artifacts"); Directory.CreateDirectory(reportRoot);
        long existing = Directory.EnumerateFiles(reportRoot, "*", SearchOption.AllDirectories).Where(p => p != path).Sum(p => new FileInfo(p).Length);
        if (name.Contains("failure") && Directory.EnumerateFiles(reportRoot, "*failure*.json", SearchOption.AllDirectories).Count(p => p != path) >= 2)
            throw new CheckFailure("harness.failure-retention.bound", "Two failing traces already retained; preserve them before another campaign");
        if (bytes.Length + existing > ReportLimit) throw new CheckFailure("harness.report.bound", "2MiB report budget exceeded; use an empty owned output directory");
        File.WriteAllBytes(path, bytes);
    }
    static void VerifySources()
    {
        string text = File.ReadAllText(Path.Combine(Tool, "source-manifest.json")); ManifestHash = Hash(text);
        using var doc = JsonDocument.Parse(text);
        foreach (var f in doc.RootElement.GetProperty("files").EnumerateObject())
            Assert(FileHash(Path.Combine(Root, f.Name)) == f.Value.GetString(), "Linked source drift: " + f.Name);
    }
    static Trace Start(string fixture, uint seed, uint generation)
    {
        if (fixture.StartsWith("legacy600")) seed = 1;
        var initial = ModelState.Fresh(seed);
        if (fixture != "fresh") initial.coins = 20000; // Explicit synthetic grant, outside history ledger.
        if (fixture.StartsWith("acquisition:")) initial.coins = 40 + Lab.ProjectPrice(fixture[12..]) - 1;
        if (fixture.StartsWith("legacy600"))
        {
            // Literal pre-chapter envelope: absent revision projects V1, never the new540 offer.
            initial.coins = fixture == "legacy600-paid" ? 595 : 550;
            initial.houseStage = 1; initial.selectedStage = 2; initial.autoHintSeen = 1;
        }
        return new Trace { Fixture = fixture, GameSeed = seed, GenerationSeed = generation, SourceManifestHash = ManifestHash,
            InitialSnapshot = Lab.Codec.Encode(initial) };
    }
    static string Quote(Lab lab, string node)
    { lab.Add("quote", node, "unchanged"); return lab.Trace.Commands.Last().Id; }
    static void Buy(Lab lab, string node)
    {
        var q = ChapterEconomy.QuoteSkill(lab.Session.State, node);
        if (q.Status == OfferStatus.Available) { string key = Quote(lab, node); lab.Add("buy", key); }
    }
    static void BuildEra(Lab lab, string era, bool skills)
    {
        for (int level = 1; level <= 2; level++) foreach (string family in FamilyIds.All)
            lab.Add("select", ChapterIds.Project(era, family, level));
        if (skills) foreach (var s in Lab.Skills) Buy(lab, s);
    }
    static void Prefix(Lab lab, string fixture)
    {
        lab.Add("query", expected: "unchanged"); lab.Add("reopen", expected: "unchanged");
        if (fixture.StartsWith("legacy600"))
        {
            Assert(lab.Session.Chapter.ConstructionPriceRevision == "construction.prices.v1", "Absent revision must project V1");
            lab.Add("spin"); lab.Add("query", expected: "unchanged"); lab.Add("reopen", expected: "unchanged");
            if (fixture == "legacy600-paid")
            {
                Assert(lab.Session.State.coins == 3 && lab.Session.State.lastSpin.chapter.Price == 600 && lab.Session.State.houseStage == 2,
                    "Literal595+8-600 must retain old paid terms");
                lab.Add("spin", expected: "unchanged", seq: 1); lab.Coverage.Hit("pricing.paid600.replay");
            }
            else
            {
                Assert(lab.Session.State.coins == 558 && lab.Session.Chapter.SelectedProjectId == GameIds.HouseImprove &&
                    lab.Session.Chapter.ConstructionPriceRevision == "construction.prices.v1", "Literal550+8 must keep600 target");
                lab.Add("select", GameIds.HouseImprove);
                Assert(lab.Session.State.coins == 18 && lab.Session.Chapter.SelectedProjectId == null, "Explicit558-540 reselection must leave18");
                lab.Coverage.Hit("pricing.explicit-reselection540");
            }
            lab.Add("reopen", expected: "unchanged"); return;
        }
        if (fixture.StartsWith("acquisition:"))
        {
            lab.Add("select", GameIds.HouseRepair);
            lab.Add("select", fixture[12..]); lab.Add("spin"); lab.Add("spin");
            lab.Add("reopen", expected: "unchanged"); return;
        }
        if (fixture == "fresh")
        {
            lab.Add("select", GameIds.HouseRepair); lab.Add("spin");
            lab.Add("select", GameIds.HouseImprove);
            lab.Add("select", GameIds.WorkshopBuild); // Replace active unaffordable target.
            lab.Add("spin"); return;
        }
        // Scripted public commands are part of the <=150 history, not hidden state edits.
        lab.Add("select", GameIds.HouseRepair);
        string stale = Quote(lab, SkillIds.Universal1); lab.Add("spin");
        lab.Add("buy", stale, "refuse"); lab.Coverage.Hit("skill.stale-refused");
        if (fixture == "funded")
        {
            string key = Quote(lab, SkillIds.Universal1); lab.Add("buy", key);
            lab.Add("spin"); lab.Add("buy", key, "unchanged"); lab.Coverage.Hit("skill.retained-replay");
            return;
        }
        // Finish the first era, already owning house1.
        foreach (string f in FamilyIds.All.Skip(1)) lab.Add("select", ChapterIds.Project(EraIds.Junkyard, f, 1));
        foreach (string f in FamilyIds.All) lab.Add("select", ChapterIds.Project(EraIds.Junkyard, f, 2));
        bool skills = fixture == "complete-skills";
        if (skills) foreach (string id in Lab.Skills) Buy(lab, id);
        long completion = lab.Session.Journey.Guidance.LastCompletion.Sequence;
        lab.Add("enter");
        lab.Add("spin", expected: "refuse"); lab.Coverage.Hit("era.blocked-spin");
        lab.Add("select", ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.House, 1), "refuse"); lab.Coverage.Hit("era.blocked-select");
        string pending = Quote(lab, skills ? SkillIds.Universal2 : SkillIds.Universal1);
        lab.Add("buy", pending, "refuse"); lab.Coverage.Hit("era.blocked-skill");
        lab.Add("reopen", expected: "unchanged"); lab.Add("era-ack");
        lab.Add("select", ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.House, 1));
        lab.Add("ack", GuideIds.Completion, "refuse", completion: completion); lab.Coverage.Hit("guidance.old-ack-refused");
        lab.Add("ack", GuideIds.Completion, completion: lab.Session.Journey.Guidance.LastCompletion.Sequence);
        foreach (string f in FamilyIds.All.Skip(1)) lab.Add("select", ChapterIds.Project(EraIds.WorkshopYard, f, 1));
        foreach (string f in FamilyIds.All) lab.Add("select", ChapterIds.Project(EraIds.WorkshopYard, f, 2));
        if (skills) foreach (string id in Lab.Skills) Buy(lab, id);
        lab.Add("reopen", expected: "unchanged");
        string order = lab.Trace.GenerationSeed % 2 == 0 ? OrderIds.TrashSort : OrderIds.PigeonDeliveries;
        lab.Add("accept", order); long instance = lab.Session.Journey.Orders.InstanceSequence;
        lab.Add("claim", order, "refuse", instance: instance);
        lab.Add("accept", order, "refuse");
        // Cancel/reaccept once, then allow bounded qualifying spin progress.
        lab.Add("spin"); lab.Add("cancel", order, instance: instance);
        lab.Add("accept", order); instance = lab.Session.Journey.Orders.InstanceSequence;
        int spins = 0;
        while (!lab.Session.Journey.Orders.Ready && spins++ < 45) lab.Add("spin");
        if (!lab.Session.Journey.Orders.Ready) { lab.Coverage.Hit("unreached.order-ready-within45"); return; }
        long claim = lab.Session.State.sequence + 1;
        lab.Add("claim", order, instance: instance);
        lab.Add("accept", order); lab.Add("spin");
        lab.Add("claim", order, "unchanged", seq: claim, instance: instance);
        lab.Coverage.Hit("order.old-claim-new-instance"); lab.Add("reopen", expected: "unchanged");
    }
    static void Generate(Lab lab, Generator random)
    {
        var session = lab.Session; var s = session.State; var c = session.Chapter; var o = session.Journey.Orders;
        int choice = random.Next(12);
        if (choice == 0) { lab.Add("query", expected: "unchanged"); return; }
        if (choice == 1) { lab.Add("reopen", expected: "unchanged"); return; }
        if (choice == 2) { lab.Add("select", "project.invalid", "refuse"); return; }
        if (choice == 3) { lab.Add("spin", expected: "refuse", seq: s.sequence + 2); return; }
        if (c.TransitionPending)
        { lab.Add(choice < 7 ? "spin" : "era-ack", expected: choice < 7 ? "refuse" : "commit"); return; }
        if (choice == 4)
        {
            string node = Lab.Skills[random.Next(6)]; var q = ChapterEconomy.QuoteSkill(s, node);
            string key = Quote(lab, node); lab.Add("buy", key, q.Status == OfferStatus.Available ? "commit" : "refuse"); return;
        }
        if (choice == 5 && session.Journey.Guidance.LastCompletion != null)
        {
            long completion = session.Journey.Guidance.LastCompletion.Sequence;
            bool old = random.Next(2) == 0;
            lab.Add("ack", GuideIds.Completion, old ? "refuse" : "commit",
                completion: old ? completion - 1 : completion); return;
        }
        if (choice == 6 && ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress)) { lab.Add("enter"); return; }
        if (choice == 7 || choice == 8)
        {
            var available = ChapterCatalog.Rules.AvailableUpgrades(c.Progress).ToArray();
            if (available.Length > 0) { lab.Add("select", available[random.Next(available.Length)]); return; }
        }
        if (choice == 9 && Lab.Levels(c).All(x => x == 5))
        {
            if (o.ActiveOrderId == null) lab.Add("accept", random.Next(2) == 0 ? OrderIds.TrashSort : OrderIds.PigeonDeliveries);
            else lab.Add(o.Ready ? "claim" : "cancel", o.ActiveOrderId, instance: o.InstanceSequence);
            return;
        }
        if (choice == 10 && o.ActiveOrderId != null)
        { lab.Add("cancel", o.ActiveOrderId, "refuse", instance: o.InstanceSequence + 1); return; }
        lab.Add("spin");
    }
    static CheckFailure Replay(Trace trace, Coverage coverage = null)
    {
        try
        {
            Assert(trace.Schema == 1 && trace.SourceManifestHash == ManifestHash, "Trace schema/source mismatch");
            Assert(trace.Commands.Count <= 150 && trace.InitialSnapshot.Length <= 65536, "Trace input bound");
            Assert(trace.Commands.Select(c => c.Id).Distinct().Count() == trace.Commands.Count, "Duplicate command ID");
            var lab = new Lab(trace, coverage ?? new());
            foreach (var c in trace.Commands) lab.Apply(c);
            return null;
        }
        catch (CheckFailure failure) { return failure; }
    }
    // Greedy deletion, stable IDs and explicit sequences retained; never repairs the program under test.
    static List<T> Reduce<T>(List<T> input, Func<List<T>, bool> preserves, int attempts, Stopwatch timer)
    {
        var current = input.ToList(); int tried = 0;
        for (int width = Math.Max(1, current.Count / 2); width >= 1; width /= 2)
        {
            for (int i = 0; i < current.Count && tried < attempts && timer.Elapsed.TotalSeconds < 20;)
            {
                var candidate = current.Take(i).Concat(current.Skip(i + width)).ToList(); tried++;
                if (preserves(candidate)) current = candidate; else i += width;
            }
            if (width == 1) break;
        }
        return current;
    }
    static void SelfCheck()
    {
        int[] totals = new int[4];
        for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
        {
            var symbols = new[] {a,b,c}; int distinct = symbols.ToHashSet().Count();
            int expected = distinct == 1 ? 23 : distinct == 2 ? 8 : 3;
            Assert(GameLogic.Payout(symbols) == expected, "64-outcome payout"); totals[distinct]++;
        }
        Assert(totals[1] == 4 && totals[2] == 36 && totals[3] == 24, "Outcome partition4/36/24");
        long legacyTotal = 0, currentTotal = 0;
        foreach (string era in new[] { EraIds.Junkyard, EraIds.WorkshopYard }) foreach (string family in FamilyIds.All) for (int level = 1; level <= 2; level++)
        {
            string id = ChapterIds.Project(era, family, level);
            long legacyPrice = Lab.ProjectPrice(id, "construction.prices.v1"), current = Lab.ProjectPrice(id);
            Assert(ConstructionPricing.Price(id, "construction.prices.v1") == legacyPrice && ConstructionPricing.Price(id, "construction.prices.v2") == current,
                "Independent construction revision prices: " + id);
            legacyTotal += legacyPrice; currentTotal += current;
        }
        Assert(legacyTotal == 9460 && currentTotal == 9400 && Lab.ProjectPrice(GameIds.HouseImprove) == 540 &&
            Lab.ProjectPrice(GameIds.HouseImprove, "construction.prices.v1") == 600, "Construction offer/retained totals");
        var reduced = Reduce(new List<int> {1,2,3,4,5}, xs => xs.Contains(2) && xs.Contains(4), 40, Stopwatch.StartNew());
        Assert(reduced.SequenceEqual(new[] {2,4}), "Synthetic reducer");
        long[][] historical = { new long[] {120,360,160,480,180,540}, new long[] {120,160,160,90,60,5} };
        for (int revision = 0; revision < 2; revision++) for (int i = 0; i < 6; i++)
            Assert(SkillPricing.Price(Lab.Skills[i], revision == 0 ? SkillPricing.RevisionV1 : SkillPricing.RevisionV2) == historical[revision][i], "Historical price revision");
        // Literal already-paid V1 slot receipt. No pending recovery or compatibility campaign.
        var old = ModelState.Fresh(1); old.coins = 17; old.houseStage = 1; old.sequence = 2; old.revision = 4;
        old.wave = new WaveProgress(0, 0, new JunkToNeon.Effects.DeliveryState(0, 0, 3, null),
            UpgradeOwnershipCodec.DecodeOrDefault("{\"version\":1,\"level\":1,\"revision\":1,\"sequence\":2,\"walletBefore\":137,\"houseStage\":1}"));
        string raw = Lab.Codec.Encode(old); var decoded = Lab.Codec.Decode(raw);
        Assert(Lab.Codec.Encode(decoded) == raw && ChapterEconomy.Project(decoded).Skills.Receipts.Single().FrozenPrice == 120,
            "Historical receipt roundtrip");
        string fixture = Path.Combine(Root, "Checks/EraMigration/R8/V2Pricing/delivery2-price5-owned.json");
        Assert(FileHash(fixture) == "340a309c274398a86cd47a550d1e79967b11196c64db7bce0010417bc792ca6d", "Qualified historical fixture hash");
        string receiptBytes = File.ReadAllText(fixture).Trim();
        var receipts = SkillOwnershipCodec.DecodeOrDefault(receiptBytes);
        Assert(SkillOwnershipCodec.Encode(receipts) == receiptBytes && receipts.Receipts[0].FrozenPrice == 180 &&
            receipts.Receipts[0].PricingRevision == SkillPricing.RevisionV1 && receipts.Receipts[1].FrozenPrice == 5 &&
            receipts.Receipts[1].PricingRevision == SkillPricing.RevisionV2, "V1/V2 receipt prices must not become current60/20");
    }
    static int Main(string[] args)
    {
        try
        {
            Tool = Directory.GetCurrentDirectory(); Root = Path.GetFullPath(Path.Combine(Tool, "../.."));
            VerifySources();
            string mode = args.FirstOrDefault() ?? "check";
            if (mode == "check") { SelfCheck(); Console.WriteLine("PASS harness:64 outcomes (4/36/24), construction V1/V2 totals9460/9400 and600/540 terms, historical skill prices/receipts, synthetic reducer. Sources verified."); return 0; }
            if (mode == "replay")
            {
                Assert(args.Length == 2 && new FileInfo(args[1]).Length <= 512 * 1024, "Replay requires one bounded trace file");
                var trace = JsonSerializer.Deserialize<Trace>(File.ReadAllText(args[1]), Json);
                string expectedFailure = trace.FailureSignature; var result = Replay(trace);
                Console.WriteLine(JsonSerializer.Serialize(new { mode, commands = trace.Commands.Count, expectedFailure, actualFailure = result?.Signature,
                    reproduced = expectedFailure == result?.Signature, detail = result?.Message }));
                return expectedFailure == result?.Signature ? 0 : 1;
            }
            Assert(mode == "smoke" || mode == "explore" || mode == "compat", "Use check, smoke, explore, compat or replay <trace.json>");
            int seeds = mode == "smoke" ? 1 : mode == "compat" ? 5 : 32, steps = mode == "smoke" ? 120 : mode == "compat" ? 110 : 140;
            string output = mode;
            for (int i = 1; i < args.Length; i += 2)
            {
                Assert(i + 1 < args.Length, "Missing option value");
                if (args[i] == "--output")
                {
                    output = args[i + 1]; Assert(output.Length <= 40 && output.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'), "Output must be one short directory name"); continue;
                }
                int value = int.Parse(args[i + 1]);
                if (args[i] == "--seeds") seeds = value; else if (args[i] == "--steps") steps = value; else Assert(false, "Unknown option");
            }
            Assert(seeds >= 1 && seeds <= 64 && steps >= 110 && steps <= 150, "Limits:1..64 seeds,110..150 steps");
            Assert(mode != "compat" || seeds == 5, "Compatibility profile has exactly five declared histories");
            var coverage = new Coverage(); string dir = Path.Combine(Tool, "artifacts", output);
            Assert(!Directory.Exists(dir) || !Directory.EnumerateFiles(dir).Any(), "Output exists; preserve prior evidence and select a fresh copy for retries");
            var failures = new List<object>(); int completed = 0; bool timedOut = false;
            for (int i = 0; i < seeds; i++)
            {
                if (Timer.Elapsed.TotalSeconds >= 280) { timedOut = true; break; }
                string fixture = mode == "smoke" ? "complete-skills" : mode == "compat" ? new[] { "fresh", "complete", "complete-skills", "legacy600", "legacy600-paid" }[i] : new[] {"fresh","funded","complete","complete-skills",
                    "acquisition:" + GameIds.WorkshopBuild, "acquisition:" + GameIds.RecyclerBuild, "acquisition:" + GameIds.PigeonLoftBuild}[i % 7];
                var trace = Start(fixture, (uint)(i + 1), (uint)(1001 + i));
                try
                {
                    var lab = new Lab(trace, coverage); Prefix(lab, fixture); var random = new Generator(trace.GenerationSeed);
                    while (trace.Commands.Count < steps - 2 && Timer.Elapsed.TotalSeconds < 280) Generate(lab, random);
                    lab.Add("reopen", expected: "unchanged");
                    if (mode == "smoke" || i == 0 || i == 3) WriteBounded(dir, "passing-trace-" + i + ".json", trace);
                    completed++;
                }
                catch (CheckFailure failure)
                {
                    trace.FailureSignature ??= failure.Signature; trace.FailureDetail ??= failure.Message;
                    WriteBounded(dir, "first-failure.json", trace); // Retain before any attempts to reduce.
                    string signature = trace.FailureSignature;
                    bool reducible = !signature.StartsWith("harness.") && !signature.StartsWith("classification.") && !signature.StartsWith("unexpected.");
                    var reductionTimer = Stopwatch.StartNew();
                    var reduced = !reducible ? trace.Commands : Reduce(trace.Commands, commands =>
                    {
                        var candidate = Start(trace.Fixture, trace.GameSeed, trace.GenerationSeed);
                        candidate.Commands = commands;
                        return Replay(candidate)?.Signature == signature;
                    }, 80, reductionTimer);
                    var minimal = Start(trace.Fixture, trace.GameSeed, trace.GenerationSeed); minimal.Commands = reduced;
                    if (reducible) { Replay(minimal); WriteBounded(dir, "reduced-failure.json", minimal); }
                    failures.Add(new { signature, detail = trace.FailureDetail, triage = "unclassified:production-or-harness-candidate",
                        originalCommands = trace.Commands.Count, reducedCommands = reduced.Count });
                    break; // One retained failure, never silently continue or rerun.
                }
            }
            var summary = new { schema = 1, mode, sourceManifestHash = ManifestHash, seedsRequested = seeds, completed, stepsCeiling = steps,
                secondsCeiling = 300, elapsedSeconds = Timer.Elapsed.TotalSeconds, timedOut,
                assertions = coverage.Assertions, commands = coverage.Commands, categories = coverage.Categories,
                uniqueStates = coverage.States.Count, missingRequired = (mode == "compat" ? PricingRequired : Required).Where(r => !coverage.Categories.ContainsKey(r)).ToArray(), failures,
                limits = new[] {"Synthetic funded histories; not natural pacing", "Sequential in-memory storage; no native durability/concurrency", "No exhaustive correctness or fun claim"} };
            WriteBounded(dir, "summary.json", summary);
            Console.WriteLine(JsonSerializer.Serialize(summary, Json)); return failures.Count == 0 && !timedOut ? 0 : 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 2; }
    }
}
