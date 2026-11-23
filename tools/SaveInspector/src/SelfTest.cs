using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace JunkToNeon.SaveInspector
{
    // Regression cases with independent expected outcomes. Repository fixtures are copied
    // into a private workspace; current snapshots are generated in memory with production pure
    // Reserve/ApplyPending functions (no storage implementation) and written there.
    // Every CLI run is executed twice for byte determinism and wrapped in an independent
    // hash/listing check of the whole input workspace.
    public static class SelfTest
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();

        // Independent oracles copied from GAME_RULES.md and JourneyEconomyRegression.cs, not
        // from this tool's formatter: seed1 foreground symbols after a funded no-skill chapter
        // and cumulative trash-order progress after each of those spins.
        static readonly int[][] SeedOne = {
            new[] {0,0,2},new[] {0,2,0},new[] {0,0,1},new[] {2,2,1},new[] {2,0,0},new[] {3,0,3},
            new[] {0,3,2},new[] {3,1,3},new[] {0,1,2},new[] {0,0,2},new[] {0,3,0},new[] {2,1,0},
            new[] {3,1,0},new[] {1,1,2},new[] {1,2,0},new[] {2,3,2} };
        static readonly int[] TrashProgress = { 1, 2, 2, 4, 5, 5, 6, 6, 7, 8, 8, 9, 9, 10, 11, 12 };
        const long ChapterConstructionTotal = 9310, TrashTarget = 12, TrashReward = 120;
        const long V1Universal2 = 360, V2Delivery2 = 5, V3Universal1 = 120, V3Recycling1 = 160;

        static readonly string[] CopiedFixtures = {
            "Checks/EraMigration/Fixtures/r6/fresh.json",
            "Checks/EraMigration/Fixtures/r6/pending-finish-house-spin.json",
            "Checks/EraMigration/Fixtures/r6/committed-finish-house-spin.json",
            "Checks/EraMigration/Fixtures/r7/old-complete.json",
            "Checks/EraMigration/Fixtures/r7/pending-effects-spin.json",
            "Checks/EraMigration/R8/Fixtures/owned-universal-2.json",
            "Checks/EraMigration/R8/Fixtures/pending-universal-2.json",
            "Checks/EraMigration/R8/Fixtures/pending-all-skills-spin.json",
            "Checks/EraMigration/R8/Fixtures/committed-all-skills-spin.json",
            "Checks/EraMigration/R8/V2Pricing/delivery2-price5-owned.json" };

        sealed class Context
        {
            public string Workspace, Inputs;
            public readonly List<object> Cases = new List<object>();
            public readonly List<string> Failures = new List<string>();
            public int Runs;
        }

        sealed class Result
        {
            public int Code;
            public byte[] Bytes;
            public JsonDocument Json;
            public string Text { get { return Encoding.ASCII.GetString(Bytes); } }
        }

        public static int Run(string repo, string workspace, string format, Stream stdout)
        {
            string repoRoot = Path.GetFullPath(repo).TrimEnd('\\', '/');
            string root = PrepareWorkspace(repoRoot, workspace);
            var ctx = new Context { Workspace = root, Inputs = Path.Combine(root, "inputs") };
            InputGuard.ValidateWorkspacePath(ctx.Inputs);
            Directory.CreateDirectory(ctx.Inputs);
            InputGuard.ResolveWorkspace(ctx.Inputs);

            var originals = new List<object>();
            var originalHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string relative in CopiedFixtures)
            {
                string source = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                byte[] bytes = File.ReadAllBytes(source);
                originalHashes[relative] = InputGuard.Hash(bytes);
                string name = CopyName(relative);
                File.WriteAllBytes(Path.Combine(ctx.Inputs, name), bytes);
                originals.Add(new Obj().Add("fixture", relative).Add("copy", "inputs/" + name).Add("bytes", (long)bytes.Length).Add("sha256", originalHashes[relative]));
            }

            var generated = new List<object>();
            try { Generate(ctx, generated); }
            catch (Exception error) when (!(error is OutOfMemoryException))
            { ctx.Failures.Add("generation: " + error.GetType().Name + " " + Report.Ascii(error.Message)); }

            if (ctx.Failures.Count == 0)
            {
                Cases(ctx);
                foreach (var regression in FixRegressions.Run(repoRoot, Path.Combine(root, "fix-regressions")))
                {
                    ctx.Cases.Add(regression);
                    if (!(bool)regression.Get("passed")) ctx.Failures.Add((string)regression.Get("id") + ": focused fix regression failed");
                }
                foreach (var regression in CurrentRegressions.Run(repoRoot, Path.Combine(root, "current-regressions")))
                {
                    ctx.Cases.Add(regression);
                    if (!(bool)regression.Get("passed")) ctx.Failures.Add((string)regression.Get("id") + ": current-source regression failed");
                }
                foreach (var regression in MilestoneRegressions.Run(repoRoot, Path.Combine(root, "milestone-regressions")))
                {
                    ctx.Cases.Add(regression);
                    if (!(bool)regression.Get("passed")) ctx.Failures.Add((string)regression.Get("id") + ": milestone regression failed");
                }
                foreach (var regression in OptionalAchievementRegressions.Run(repoRoot, Path.Combine(root, "optional-regressions"), Input(ctx, "r11-complete.json")))
                {
                    ctx.Cases.Add(regression);
                    if (!(bool)regression.Get("passed")) ctx.Failures.Add((string)regression.Get("id") + ": optional achievement regression failed");
                }
            }

            bool originalsUnchanged = true;
            foreach (string relative in CopiedFixtures)
                if (InputGuard.Hash(File.ReadAllBytes(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)))) != originalHashes[relative]) originalsUnchanged = false;
            if (!originalsUnchanged) ctx.Failures.Add("repository fixture originals changed");

            var failures = new List<object>();
            foreach (string failure in ctx.Failures) failures.Add(failure);
            var report = Cli.Envelope("self-test", ctx.Failures.Count == 0 ? "passed" : "failed")
                .Add("caseCount", ctx.Cases.Count).Add("cliRuns", ctx.Runs).Add("failures", failures)
                .Add("repositoryOriginalsUnchanged", originalsUnchanged)
                .Add("copiedFixtures", originals).Add("generatedInputs", generated).Add("cases", ctx.Cases);
            Cli.Emit(stdout, format, report);
            return ctx.Failures.Count == 0 ? ExitCodes.Ok : ExitCodes.SelfTestFailed;
        }

        internal static string PrepareWorkspace(string repo, string workspace)
        {
            string repoRoot = Path.GetFullPath(repo).TrimEnd('\\', '/');
            string root = InputGuard.ValidateWorkspacePath(workspace);
            if (root.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(root, repoRoot, StringComparison.OrdinalIgnoreCase))
                throw new InputRefused("workspace-in-repository", "Self-test workspace must be outside the repository");
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
                throw new InputRefused("workspace-not-empty", "Self-test workspace must be empty or absent");
            Directory.CreateDirectory(root);
            InputGuard.ResolveWorkspace(root);
            if (Directory.EnumerateFileSystemEntries(root).Any())
                throw new InputRefused("workspace-not-empty", "Created self-test workspace is not empty");
            return root;
        }

        static string CopyName(string relative)
        {
            string[] parts = relative.Split('/');
            return parts[parts.Length - 2].ToLowerInvariant() + "-" + parts[parts.Length - 1];
        }

        static string Input(Context ctx, string name) { return Path.Combine(ctx.Inputs, name); }

        // ---------------------------------------------------------------- generation

        static ModelState Persist(ModelState s)
        {
            GameLogic.Validate(s);
            var back = Codec.Decode(Codec.Encode(s));
            GameLogic.Validate(back);
            return back;
        }
        static ModelState Commit(ModelState reserved) { return Persist(GameLogic.ApplyPending(Persist(reserved))); }

        static void Save(Context ctx, List<object> generated, string name, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(Input(ctx, name), bytes);
            generated.Add(new Obj().Add("copy", "inputs/" + name).Add("bytes", (long)bytes.Length).Add("sha256", InputGuard.Hash(bytes)));
        }
        static void Save(Context ctx, List<object> generated, string name, ModelState state) { Save(ctx, generated, name, Codec.Encode(state)); }

        static void Require(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); }

        static ModelState BuildEra(ModelState s, string era)
        {
            for (int level = 1; level <= 2; level++) foreach (string family in FamilyIds.All)
                s = Commit(JourneyEconomy.ReserveChapter(s, "select", ChapterIds.Project(era, family, level), s.sequence + 1));
            return s;
        }

        static void Generate(Context ctx, List<object> generated)
        {
            // Current constructionV3 chapter completion from a funded seed1 save, through the production command path.
            var s = ModelState.Fresh(1); s.coins = 10000; s = Persist(s);
            s = BuildEra(s, EraIds.Junkyard);
            s = Commit(JourneyEconomy.ReserveChapter(s, "enter-era", null, s.sequence + 1));
            s = Commit(JourneyEconomy.ReserveChapter(s, "era-seen", null, s.sequence + 1));
            s = BuildEra(s, EraIds.WorkshopYard);
            Require(s.coins == 10000 - ChapterConstructionTotal, "funded current chapter traversal did not cost 9310");
            var complete = s;
            Save(ctx, generated, "r11-complete.json", complete);

            // Current V3 skill purchase from the completed chapter.
            var quote = ChapterEconomy.QuoteSkill(complete, SkillIds.Universal1);
            var skillPending = Persist(JourneyEconomy.ReserveChapter(complete, "skill", SkillIds.Universal1, complete.sequence + 1, request: quote.Request));
            Save(ctx, generated, "r11-skill-v3-pending.json", skillPending);
            Save(ctx, generated, "r11-skill-v3-committed.json", Persist(GameLogic.ApplyPending(skillPending)));

            // Trash order: accept, 16 foreground spins to 12/12, claim, re-accept.
            long firstInstance = complete.sequence + 1;
            s = Commit(JourneyEconomy.Reserve(complete, "order-accept", OrderIds.TrashSort, 0, firstInstance));
            Save(ctx, generated, "r11-order-accepted.json", s);
            for (int i = 0; i < SeedOne.Length; i++)
            {
                s = Commit(JourneyEconomy.ReserveChapter(s, "spin", null, s.sequence + 1, manualSpin: true));
                Require(s.lastSpin.symbols.SequenceEqual(SeedOne[i]), "seed1 trace diverged at spin " + i);
                Require(s.journey.Orders.Progress == TrashProgress[i], "trash progress oracle diverged at spin " + i);
            }
            var ready = s;
            Save(ctx, generated, "r11-order-ready.json", ready);
            var claimPending = Persist(JourneyEconomy.Reserve(ready, "order-claim", OrderIds.TrashSort, firstInstance, ready.sequence + 1));
            Save(ctx, generated, "r11-claim-pending.json", claimPending);
            var claimed = Persist(GameLogic.ApplyPending(claimPending));
            Save(ctx, generated, "r11-claimed.json", claimed);
            var reaccepted = Commit(JourneyEconomy.Reserve(claimed, "order-accept", OrderIds.TrashSort, 0, claimed.sequence + 1));
            Save(ctx, generated, "r11-reaccepted.json", reaccepted);
            var cancelled = Commit(JourneyEconomy.Reserve(reaccepted, "order-cancel", OrderIds.TrashSort, reaccepted.journey.Orders.InstanceSequence, reaccepted.sequence + 1));
            Save(ctx, generated, "r11-cancelled.json", cancelled);
            var clocked = Commit(JourneyEconomy.ReserveChapter(reaccepted, "clock", null, reaccepted.sequence + 1, at: 1000));
            Save(ctx, generated, "r11-clock-only.json", clocked);

            // Mixed V1 + V3 ownership: the literal r8 V1 save buys a current V3 node.
            var u2 = Codec.Decode(File.ReadAllText(Input(ctx, "fixtures-owned-universal-2.json")));
            var mixedQuote = ChapterEconomy.QuoteSkill(u2, SkillIds.Recycling1);
            var mixedPending = Persist(JourneyEconomy.ReserveChapter(u2, "skill", SkillIds.Recycling1, u2.sequence + 1, request: mixedQuote.Request));
            Save(ctx, generated, "r8-mixed-v3-pending.json", mixedPending);
            Save(ctx, generated, "r8-mixed-v3-committed.json", Persist(GameLogic.ApplyPending(mixedPending)));

            // Synthetic full snapshot carrying the literal V2 ownership payload (production constructors validate it).
            var v2Skills = SkillOwnershipCodec.DecodeOrDefault(File.ReadAllText(Input(ctx, "v2pricing-delivery2-price5-owned.json")));
            var v2 = u2.Copy(); var c = u2.chapter; var w = WaveProgress.Of(u2);
            v2.chapter = new ChapterState(2, c.RuleRevision, c.Progress, v2Skills, c.SelectedProjectId, c.EntrySequence, c.TransitionSeen);
            v2.wave = new WaveProgress(w.RecyclerBuilt, w.PigeonLoftBuilt, w.Delivery, ChapterEconomy.LegacySlot(v2Skills));
            v2.coins = 10000 - 180 - V2Delivery2;
            Save(ctx, generated, "synthetic-v2-owned.json", Persist(v2));

            // Synthetic malformed or reformatted copies.
            string fresh = File.ReadAllText(Input(ctx, "r6-fresh.json"));
            string pending = File.ReadAllText(Input(ctx, "r6-pending-finish-house-spin.json"));
            Save(ctx, generated, "bad-truncated.json", fresh.Substring(0, fresh.Length - 5));
            Save(ctx, generated, "bad-duplicate-field.json", Change(fresh, "\"coins\":37,", "\"coins\":37,\"coins\":37,"));
            Save(ctx, generated, "bad-unknown-marker.json", Change(fresh, "{\"schemaVersion\"", "{\"zzMarkerQ7X\":\"ZZ-MARKER-Q7X-PAYLOAD\",\"schemaVersion\""));
            string modern = Codec.Encode(reaccepted);
            int top = modern.LastIndexOf("\"journey\":{\"format\":1,", StringComparison.Ordinal);
            Save(ctx, generated, "bad-unknown-modern-field.json", modern.Substring(0, top) + "\"journey\":{\"extra\":1,\"format\":1," + modern.Substring(top + "\"journey\":{\"format\":1,".Length));
            Save(ctx, generated, "bad-duplicate-modern-field.json", modern.Substring(0, top) + "\"journey\":{\"format\":1,\"format\":1," + modern.Substring(top + "\"journey\":{\"format\":1,".Length));
            Save(ctx, generated, "bad-duplicate-skill-payload-field.json", Change(File.ReadAllText(Input(ctx, "r11-skill-v3-committed.json")), "\\\"version\\\":3,", "\\\"version\\\":3,\\\"version\\\":3,"));
            Save(ctx, generated, "bad-over-character-limit.json", "{" + new string(' ', InputGuard.ProductionMaximumCharacters) + fresh.Substring(1));
            Save(ctx, generated, "bad-frozen-legacy-spin.json", Change(pending, "\"symbols\":[0,0,2]", "\"symbols\":[1,1,1]"));
            Save(ctx, generated, "bad-frozen-claim-reward.json", Change(Codec.Encode(claimPending), "\"reward\":120", "\"reward\":999"));
            Save(ctx, generated, "fmt-bom-fresh.json", "\uFEFF" + fresh);
            Save(ctx, generated, "fmt-pretty-effects-spin.json", Pretty(File.ReadAllText(Input(ctx, "r7-pending-effects-spin.json"))));
            var big = new byte[InputGuard.MaximumInputBytes + 1];
            for (int i = 0; i < big.Length; i++) big[i] = (byte)' ';
            File.WriteAllBytes(Input(ctx, "bad-over-byte-bound.json"), big);
            generated.Add(new Obj().Add("copy", "inputs/bad-over-byte-bound.json").Add("bytes", (long)big.Length).Add("sha256", InputGuard.Hash(big)));
            Directory.CreateDirectory(Input(ctx, "a-directory.json"));
        }

        static string Change(string raw, string from, string to)
        {
            int index = raw.IndexOf(from, StringComparison.Ordinal);
            Require(index >= 0 && raw.IndexOf(from, index + 1, StringComparison.Ordinal) < 0, "fixture fragment not unique");
            return raw.Substring(0, index) + to + raw.Substring(index + from.Length);
        }

        // Insignificant whitespace outside strings, which production's reader accepts.
        static string Pretty(string raw)
        {
            var b = new StringBuilder(); bool quoted = false, escaped = false;
            foreach (char ch in raw)
            {
                b.Append(ch);
                if (quoted) { if (escaped) escaped = false; else if (ch == '\\') escaped = true; else if (ch == '"') quoted = false; continue; }
                if (ch == '"') quoted = true;
                else if (ch == ',') b.Append("\n  ");
                else if (ch == ':') b.Append(' ');
                else if (ch == '{') b.Append("\r\n\t");
            }
            return b.ToString();
        }

        // ---------------------------------------------------------------- harness

        static Dictionary<string, string> Snapshot(Context ctx)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in new DirectoryInfo(ctx.Workspace).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                map[entry.FullName.Substring(ctx.Workspace.Length)] = entry is FileInfo f ? f.Length + ":" + InputGuard.Hash(File.ReadAllBytes(f.FullName)) : "dir";
            return map;
        }

        static Result Cli2(Context ctx, string caseId, params string[] args)
        {
            var before = Snapshot(ctx);
            byte[] first, second; int code1, code2;
            using (var a = new MemoryStream()) { code1 = Cli.Run(args, a); first = a.ToArray(); }
            using (var b = new MemoryStream()) { code2 = Cli.Run(args, b); second = b.ToArray(); }
            ctx.Runs += 2;
            var after = Snapshot(ctx);
            if (code1 != code2 || !first.SequenceEqual(second)) ctx.Failures.Add(caseId + ": nondeterministic output or exit code");
            if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var v) || v != p.Value))
                ctx.Failures.Add(caseId + ": workspace bytes or entries changed during the command");
            foreach (byte value in first) if (value > 127 || (value < 32 && value != 10)) { ctx.Failures.Add(caseId + ": non-ASCII or control byte in output"); break; }
            var result = new Result { Code = code1, Bytes = first };
            if (Array.IndexOf(args, "json") >= 0)
            {
                try { result.Json = JsonDocument.Parse(first); }
                catch (JsonException) { ctx.Failures.Add(caseId + ": JSON output does not parse"); }
            }
            return result;
        }

        static Result Json(Context ctx, string caseId, string command, params string[] pairs)
        {
            var args = new List<string> { command, "--workspace", ctx.Workspace };
            args.AddRange(pairs); args.Add("--format"); args.Add("json");
            var json = Cli2(ctx, caseId, args.ToArray());
            args[args.Count - 1] = "text";
            var text = Cli2(ctx, caseId + "/text", args.ToArray());
            if (text.Code != json.Code) ctx.Failures.Add(caseId + ": text and json exit codes differ");
            return json;
        }

        static JsonElement? At(Result r, string path)
        {
            if (r.Json == null) return null;
            JsonElement cursor = r.Json.RootElement;
            foreach (string raw in path.Split('.'))
            {
                string part = raw; int index = -1;
                int bracket = part.IndexOf('[');
                if (bracket >= 0) { index = int.Parse(part.Substring(bracket + 1, part.Length - bracket - 2)); part = part.Substring(0, bracket); }
                if (cursor.ValueKind != JsonValueKind.Object || !cursor.TryGetProperty(part, out var next)) return null;
                cursor = next;
                if (index >= 0)
                {
                    if (cursor.ValueKind != JsonValueKind.Array || index >= cursor.GetArrayLength()) return null;
                    cursor = cursor[index];
                }
            }
            return cursor;
        }

        static string S(Result r, string path)
        {
            var e = At(r, path);
            if (e == null) return "<missing>";
            var v = e.Value;
            if (v.ValueKind == JsonValueKind.String) return v.GetString();
            if (v.ValueKind == JsonValueKind.Null) return "null";
            return JsonSerializer.Serialize(v);
        }

        sealed class Case
        {
            readonly Context ctx; readonly string id; readonly string purpose; readonly List<object> checks = new List<object>(); bool passed = true;
            public Case(Context ctx, string id, string purpose) { this.ctx = ctx; this.id = id; this.purpose = purpose; }
            public Case Expect(string what, bool condition, string observed = null)
            {
                checks.Add(new Obj().Add("expect", what).Add("passed", condition).Add("observed", observed));
                if (!condition) { passed = false; ctx.Failures.Add(id + ": " + what + (observed == null ? "" : " (observed " + Report.Ascii(observed) + ")")); }
                return this;
            }
            public Case Eq(Result r, string path, string expected) { string v = S(r, path); return Expect(path + " == " + expected, v == expected, v); }
            public Case Code(Result r, int expected) { return Expect("exit " + expected, r.Code == expected, r.Code.ToString()); }
            public Case Intact(Result r, string prefix = "input")
            {
                return Eq(r, prefix + ".integrity.unchanged", "true").Eq(r, prefix + ".integrity.adjacentEntriesUnchanged", "true");
            }
            public void Done() { ctx.Cases.Add(new Obj().Add("id", id).Add("purpose", purpose).Add("passed", passed).Add("checks", checks)); }
        }

        // ---------------------------------------------------------------- cases

        static void Cases(Context ctx)
        {
            string In(string name) { return Input(ctx, name); }

            // 1. Older valid snapshot and a modern r11 snapshot.
            var r = Json(ctx, "c01-legacy-fresh", "inspect", "--input", In("r6-fresh.json"));
            new Case(ctx, "c01-legacy-fresh", "pre-wave r6 snapshot inspected without migration or grant")
                .Code(r, 0).Eq(r, "facts.format.layer", "legacy-pre-wave").Eq(r, "facts.wallet.coins", "37")
                .Eq(r, "facts.format.journeyStored", "false").Eq(r, "facts.format.chapterStored", "false")
                .Eq(r, "facts.guidance.source", Facts.Projected).Eq(r, "facts.guidance.origin", "Existing")
                .Eq(r, "facts.guidance.acknowledged", "[]").Eq(r, "facts.guidance.tutorialSteps", "[]")
                .Eq(r, "facts.orders.active", "null").Eq(r, "facts.orders.latestClaim", "null").Eq(r, "facts.orders.journeyMarker.adoptedSequence", "null")
                .Eq(r, "facts.pending.status", "absent").Eq(r, "facts.clockSequence.sequence", "0").Intact(r).Done();

            r = Json(ctx, "c02-modern-r11", "inspect", "--input", In("r11-complete.json"));
            new Case(ctx, "c02-modern-r11", "current journey snapshot: completed chapter, stored journey, no order")
                .Code(r, 0).Eq(r, "facts.format.layer", "journey-r9").Eq(r, "facts.format.journeyStored", "true")
                .Eq(r, "facts.wallet.coins", (10000 - ChapterConstructionTotal).ToString())
                .Eq(r, "facts.progression.completeUnderChapterRule", "true").Eq(r, "facts.progression.era", "workshop-yard")
                .Eq(r, "facts.orders.unlocked", "true").Eq(r, "facts.orders.active", "null").Eq(r, "facts.guidance.source", Facts.Stored).Intact(r).Done();

            // 10. No-op inspection of an old-complete r7 save: no coins, settlement, migration or acknowledgement invented.
            r = Json(ctx, "c03-legacy-old-complete", "inspect", "--input", In("r7-old-complete.json"));
            new Case(ctx, "c03-legacy-old-complete", "old completed-house r7 save: no chapter completion, no settlement, no acknowledgement")
                .Code(r, 0).Eq(r, "facts.wallet.coins", "42").Eq(r, "facts.progression.source", Facts.Projected)
                .Eq(r, "facts.progression.completeUnderChapterRule", "false").Eq(r, "facts.progression.legacyFields.completeUnderR7Rule", "false")
                .Eq(r, "facts.clockSequence.lastSettledAt", "0").Eq(r, "facts.clockSequence.lastSettlement", "null")
                .Eq(r, "facts.guidance.acknowledged", "[]").Eq(r, "facts.guidance.origin", "Existing")
                .Eq(r, "facts.rngCarry.deliveryCarrySource", "default-zero-absent-field").Eq(r, "facts.format.chapterStored", "false").Intact(r).Done();

            // 2. Valid pending and already committed operations with exact wallet differences.
            var committed = Json(ctx, "c04-r6-committed-inspect", "inspect", "--input", In("r6-committed-finish-house-spin.json"));
            r = Json(ctx, "c04-r6-pending-preview", "preview-pending", "--input", In("r6-pending-finish-house-spin.json"));
            new Case(ctx, "c04-r6-pending-preview", "pair 8 + workshop 1 lifts 595 to 604, then house improvement 600 is bought: 4")
                .Code(r, 0).Eq(r, "status", "replayed-in-memory").Eq(r, "operation.coinsBefore", "595").Eq(r, "operation.coinsAfter", "4")
                .Eq(r, "operation.seedAfter", "2647435461")
                .Expect("replayed canonical hash equals literal committed fixture", S(r, "afterCanonicalSha256") == S(committed, "input.canonicalSha256"), S(r, "afterCanonicalSha256"))
                .Intact(r).Done();

            r = Json(ctx, "c05-r6-committed-preview", "preview-pending", "--input", In("r6-committed-finish-house-spin.json"));
            new Case(ctx, "c05-r6-committed-preview", "committed snapshot is not previewed as another payable grant")
                .Code(r, ExitCodes.PreviewNotApplicable).Eq(r, "status", "not-applicable").Expect("no operation block", At(r, "operation") == null).Intact(r).Done();

            r = Json(ctx, "c06-r7-effects-preview", "preview-pending", "--input", In("r7-pending-effects-spin.json"));
            new Case(ctx, "c06-r7-effects-preview", "base8+workshop1+slot1+recycler1 = 351 (<600, no purchase); two pigeons carry 0->2")
                .Code(r, 0).Eq(r, "operation.coinsAfter", "351").Eq(r, "operation.walletDelta", "11")
                .Eq(r, "operation.deliveryCarryBefore", "0").Eq(r, "operation.deliveryCarryAfter", "2").Intact(r).Done();

            var ownedU2 = Json(ctx, "c07-r8-v1-owned", "inspect", "--input", In("fixtures-owned-universal-2.json"));
            r = Json(ctx, "c07-r8-v1-pending", "preview-pending", "--input", In("fixtures-pending-universal-2.json"));
            new Case(ctx, "c07-r8-v1-pending", "frozen V1 universal2 purchase debits exactly 360 and equals the literal owned fixture")
                .Code(r, 0).Eq(r, "operation.walletDelta", (-V1Universal2).ToString()).Eq(r, "operation.coinsAfter", "9520")
                .Eq(r, "operation.frozenChapterMatches", "true")
                .Expect("replayed canonical hash equals literal owned-universal-2 fixture", S(r, "afterCanonicalSha256") == S(ownedU2, "input.canonicalSha256"))
                .Intact(r).Done();

            var claimReady = Json(ctx, "c08-claim-ready", "inspect", "--input", In("r11-order-ready.json"));
            r = Json(ctx, "c08-claim-preview", "preview-pending", "--input", In("r11-claim-pending.json"));
            new Case(ctx, "c08-claim-preview", "pending explicit claim credits exactly the trash order reward 120")
                .Code(r, 0).Eq(r, "operation.kind", "order-claim").Eq(r, "operation.walletDelta", TrashReward.ToString())
                .Eq(r, "operation.frozenJourneyMatches", "true").Eq(r, "operation.seedBefore", S(r, "operation.seedAfter"))
                .Eq(claimReady, "facts.orders.active.ready", "true").Eq(claimReady, "facts.orders.active.claimed", "false")
                .Eq(claimReady, "facts.orders.active.progress", TrashTarget.ToString()).Intact(r).Done();

            // 3. Historical price revision retention and unchanged ownership tokens.
            new Case(ctx, "c09-v1-receipts", "V1 receipts keep 120/360 under codec version2")
                .Eq(ownedU2, "facts.skills.ownershipCodecVersion", "2").Eq(ownedU2, "facts.skills.receipts[1].node", SkillIds.Universal2)
                .Eq(ownedU2, "facts.skills.receipts[1].priceRevision", SkillPricing.RevisionV1).Eq(ownedU2, "facts.skills.receipts[1].frozenPrice", V1Universal2.ToString())
                .Eq(ownedU2, "facts.skills.receipts[0].frozenPrice", "120").Done();

            var mixed = Json(ctx, "c10-mixed-committed", "inspect", "--input", In("r8-mixed-v3-committed.json"));
            r = Json(ctx, "c10-mixed-preview", "preview-pending", "--input", In("r8-mixed-v3-pending.json"));
            new Case(ctx, "c10-mixed-v1-v3", "new V3 recycling1 costs 160; old V1 receipts stay 120/360; purchase binds the prior ownership token")
                .Code(r, 0).Eq(r, "operation.walletDelta", (-V3Recycling1).ToString())
                .Eq(mixed, "facts.skills.ownershipCodecVersion", "3")
                .Eq(mixed, "facts.skills.receipts[0].priceRevision", SkillPricing.RevisionV1).Eq(mixed, "facts.skills.receipts[1].frozenPrice", V1Universal2.ToString())
                .Eq(mixed, "facts.skills.receipts[1].priceRevision", SkillPricing.RevisionV1)
                .Eq(mixed, "facts.skills.receipts[2].priceRevision", SkillPricing.RevisionV3).Eq(mixed, "facts.skills.receipts[2].frozenPrice", V3Recycling1.ToString())
                .Eq(mixed, "facts.skills.receipts[0].sequence", S(ownedU2, "facts.skills.receipts[0].sequence"))
                .Eq(mixed, "facts.skills.receipts[1].operationId", S(ownedU2, "facts.skills.receipts[1].operationId"))
                .Expect("new receipt's expected ownership equals the pre-purchase token", S(mixed, "facts.skills.receipts[2].expectedOwnershipSha256") == S(ownedU2, "facts.skills.ownershipTokenSha256"))
                .Intact(r).Done();

            var v2 = Json(ctx, "c11-v2-owned", "inspect", "--input", In("synthetic-v2-owned.json"));
            new Case(ctx, "c11-v2-owned", "literal V2 payload: delivery2 keeps price 5 under skill.prices.v2; delivery1 keeps V1 180")
                .Code(v2, 0).Eq(v2, "facts.skills.ownershipCodecVersion", "3")
                .Eq(v2, "facts.skills.receipts[1].priceRevision", SkillPricing.RevisionV2).Eq(v2, "facts.skills.receipts[1].frozenPrice", V2Delivery2.ToString())
                .Eq(v2, "facts.skills.receipts[0].priceRevision", SkillPricing.RevisionV1).Eq(v2, "facts.skills.receipts[0].frozenPrice", "180").Intact(v2).Done();

            var v3 = Json(ctx, "c12-v3-skill", "preview-pending", "--input", In("r11-skill-v3-pending.json"));
            var v3c = Json(ctx, "c12-v3-committed", "inspect", "--input", In("r11-skill-v3-committed.json"));
            new Case(ctx, "c12-v3-skill", "current V3 universal1 quote debits 120 from 690")
                .Code(v3, 0).Eq(v3, "operation.coinsAfter", (10000 - ChapterConstructionTotal - V3Universal1).ToString())
                .Eq(v3c, "facts.skills.receipts[0].priceRevision", SkillPricing.RevisionV3).Eq(v3c, "facts.skills.ownershipCodecVersion", "3")
                .Expect("replayed canonical hash equals committed snapshot", S(v3, "afterCanonicalSha256") == S(v3c, "input.canonicalSha256")).Done();

            var allSkills = Json(ctx, "c13-all-skills-committed", "inspect", "--input", In("fixtures-committed-all-skills-spin.json"));
            r = Json(ctx, "c13-all-skills-spin", "preview-pending", "--input", In("fixtures-pending-all-skills-spin.json"));
            var allBefore = Json(ctx, "c13-all-skills-pending", "inspect", "--input", In("fixtures-pending-all-skills-spin.json"));
            new Case(ctx, "c13-all-skills-spin", "a spin never changes skill ownership; replay equals the literal committed fixture")
                .Code(r, 0).Expect("no skills group change", !S(r, "groupsChanged").Contains("skills"), S(r, "groupsChanged"))
                .Eq(allSkills, "facts.skills.ownershipTokenSha256", S(allBefore, "facts.skills.ownershipTokenSha256"))
                .Expect("replayed canonical hash equals literal committed fixture", S(r, "afterCanonicalSha256") == S(allSkills, "input.canonicalSha256")).Intact(r).Done();

            // 4. Repeated order with a new instance and a retained older claim.
            var claimed = Json(ctx, "c14-claimed", "inspect", "--input", In("r11-claimed.json"));
            var re = Json(ctx, "c14-reaccepted", "inspect", "--input", In("r11-reaccepted.json"));
            long claimSequence = long.Parse(S(claimed, "facts.clockSequence.sequence"));
            long firstInstance = long.Parse(S(claimReady, "facts.orders.active.instanceSequence"));
            new Case(ctx, "c14-repeated-order", "new instance starts at 0/12 while the old claim stays a retained, already credited receipt")
                .Code(re, 0).Eq(re, "facts.orders.active.orderId", OrderIds.TrashSort).Eq(re, "facts.orders.active.progress", "0")
                .Eq(re, "facts.orders.active.target", TrashTarget.ToString()).Eq(re, "facts.orders.active.ready", "false")
                .Eq(re, "facts.orders.active.instanceSequence", (claimSequence + 1).ToString())
                .Eq(re, "facts.orders.latestClaim.instanceSequence", firstInstance.ToString())
                .Eq(re, "facts.orders.latestClaim.claimSequence", claimSequence.ToString())
                .Eq(re, "facts.orders.latestClaim.rewardCoins", TrashReward.ToString())
                .Eq(re, "facts.orders.latestClaim.status", "retained-historical-already-credited")
                .Eq(re, "facts.wallet.coins", S(claimed, "facts.wallet.coins")).Done();

            r = Json(ctx, "c15-diff-claim", "diff", "--before", In("r11-order-ready.json"), "--after", In("r11-claimed.json"));
            new Case(ctx, "c15-diff-claim", "ready -> claimed: wallet +120, claim inferred and computed equal to reward")
                .Code(r, 0).Eq(r, "equal", "false").Eq(r, "computed[0].check", "walletDeltaEqualsNewClaimReward").Eq(r, "computed[0].result", "true")
                .Expect("inferred mentions claim", S(r, "inferred").Contains("explicit order claim")).Intact(r, "inputs[0]").Intact(r, "inputs[1]").Done();

            r = Json(ctx, "c16-diff-reaccept", "diff", "--before", In("r11-claimed.json"), "--after", In("r11-reaccepted.json"));
            new Case(ctx, "c16-diff-reaccept", "claimed -> reaccepted: no wallet change, new instance inferred, claim not re-credited")
                .Code(r, 0).Expect("no wallet group", !S(r, "groupsChanged").Contains("wallet"), S(r, "groupsChanged"))
                .Expect("new instance inferred", S(r, "inferred").Contains("new order instance"))
                .Expect("no claim inferred", !S(r, "inferred").Contains("explicit order claim")).Done();

            r = Json(ctx, "c16b-diff-cancel", "diff", "--before", In("r11-reaccepted.json"), "--after", In("r11-cancelled.json"));
            var cancelledFacts = Json(ctx, "c16b-cancelled", "inspect", "--input", In("r11-cancelled.json"));
            new Case(ctx, "c16b-diff-cancel", "cancellation forfeits progress without reward; the older claim stays retained unchanged")
                .Code(r, 0).Expect("no wallet group", !S(r, "groupsChanged").Contains("wallet"), S(r, "groupsChanged"))
                .Expect("cancellation inferred", S(r, "inferred").Contains("consistent with cancellation"))
                .Expect("no claim inferred", !S(r, "inferred").Contains("explicit order claim"))
                .Eq(cancelledFacts, "facts.orders.active", "null")
                .Eq(cancelledFacts, "facts.orders.latestClaim.instanceSequence", firstInstance.ToString())
                .Eq(cancelledFacts, "facts.orders.latestClaim.claimSequence", claimSequence.ToString())
                .Eq(cancelledFacts, "facts.wallet.coins", S(re, "facts.wallet.coins")).Done();

            // 5. Semantic equality despite formatting production accepts.
            r = Json(ctx, "c17-format-equal", "diff", "--before", In("r7-pending-effects-spin.json"), "--after", In("fmt-pretty-effects-spin.json"));
            new Case(ctx, "c17-format-equal", "whitespace/newline reformatting is semantically identical")
                .Code(r, 0).Eq(r, "equal", "true").Eq(r, "changeCount", "0")
                .Expect("raw hashes differ", S(r, "inputs[0].sha256") != S(r, "inputs[1].sha256")).Done();
            r = Json(ctx, "c18-bom-equal", "diff", "--before", In("r6-fresh.json"), "--after", In("fmt-bom-fresh.json"));
            new Case(ctx, "c18-bom-equal", "UTF-8 BOM is accepted exactly as File.ReadAllText accepts it")
                .Code(r, 0).Eq(r, "equal", "true").Done();

            // 6. Malformed, duplicate/unknown, over-limit and invalid frozen operation.
            Rejected(ctx, "c19-truncated", "bad-truncated.json", "json-format-or-shape");
            Rejected(ctx, "c20-duplicate", "bad-duplicate-field.json", "json-format-or-shape");
            Rejected(ctx, "c21-unknown-modern", "bad-unknown-modern-field.json", "json-format-or-shape");
            Rejected(ctx, "c21b-duplicate-modern", "bad-duplicate-modern-field.json", "json-format-or-shape");
            Rejected(ctx, "c21c-duplicate-skill-payload", "bad-duplicate-skill-payload-field.json", "json-format-or-shape");
            Rejected(ctx, "c22-over-characters", "bad-over-character-limit.json", "json-format-or-shape");
            var frozen = Rejected(ctx, "c23-invalid-frozen-legacy", "bad-frozen-legacy-spin.json", "production-validation");
            new Case(ctx, "c23-invalid-frozen-legacy-hint", "invalid frozen spin is reported as rejected with a pending hint, never repaired")
                .Eq(frozen, "pendingFieldPresentHint", "true").Expect("no facts", At(frozen, "facts") == null).Done();
            var claimBad = Rejected(ctx, "c24-invalid-frozen-claim", "bad-frozen-claim-reward.json", "production-validation");
            var p = Json(ctx, "c24-preview-refused", "preview-pending", "--input", In("bad-frozen-claim-reward.json"));
            new Case(ctx, "c24-preview-refused", "preview refuses an invalid frozen claim without a fabricated state")
                .Code(p, ExitCodes.SnapshotRejected).Expect("no afterCanonicalSha256", At(p, "afterCanonicalSha256") == null).Intact(p).Done();
            var marker = Rejected(ctx, "c25-marker", "bad-unknown-marker.json", "json-format-or-shape");
            var markerText = Cli2(ctx, "c25-marker-text", "inspect", "--workspace", ctx.Workspace, "--input", In("bad-unknown-marker.json"), "--format", "text");
            new Case(ctx, "c25-marker-not-echoed", "unknown field names/values from the payload never reach the report")
                .Expect("JSON omits marker", !Encoding.ASCII.GetString(marker.Bytes).Contains("MARKER") && !Encoding.ASCII.GetString(marker.Bytes).Contains("zzMarker"))
                .Expect("text omits marker", !markerText.Text.Contains("MARKER") && !markerText.Text.Contains("zzMarker")).Done();

            r = Json(ctx, "c26-over-bytes", "inspect", "--input", In("bad-over-byte-bound.json"));
            new Case(ctx, "c26-over-bytes", "input above the derived byte bound is refused before reading or decoding")
                .Code(r, ExitCodes.InputRefused).Eq(r, "error.category", "too-large").Done();

            // 7. Boundary refusals.
            Refused(ctx, "c27-directory", In("a-directory.json"), "not-a-file");
            Refused(ctx, "c28-outside", Path.Combine(Path.GetDirectoryName(ctx.Workspace), "outside.json"), "outside-workspace");
            Refused(ctx, "c29-stream", In("r6-fresh.json") + ":hidden", "alternate-stream");
            Refused(ctx, "c30-device", Path.Combine(ctx.Inputs, "NUL.json"), "device-path");
            Refused(ctx, "c31-unc", "\\\\?\\" + In("r6-fresh.json"), "device-path");
            Refused(ctx, "c32-missing", In("absent.json"), "missing");
            new Case(ctx, "c33-protected-location", "normal game data location (AppData\\LocalLow) is refused (string check)")
                .Expect("LocalLow path protected", InputGuard.IsProtectedLocation("C:\\Users\\someone\\AppData\\LocalLow\\Company\\Junk\\save.json", "C:\\Users\\other"))
                .Expect("profile-relative LocalLow protected", InputGuard.IsProtectedLocation("C:\\Users\\other\\AppData\\LocalLow", "C:\\Users\\other"))
                .Expect("task workspace not protected", !InputGuard.IsProtectedLocation("X:\\tmp\\task\\inputs\\a.json", "C:\\Users\\other")).Done();
            LinkCase(ctx);

            // 9. Clock/sequence-only diff never claims income.
            r = Json(ctx, "c35-clock-only", "diff", "--before", In("r11-reaccepted.json"), "--after", In("r11-clock-only.json"));
            new Case(ctx, "c35-clock-only", "zero-income clock checkpoint changes only clock/sequence metadata")
                .Code(r, 0).Eq(r, "groupsChanged", "[\"clockSequence\"]").Expect("clock-only inference", S(r, "inferred").Contains("Only clock/sequence metadata changed"))
                .Expect("no income/claim wording", !S(r, "observed").Contains("wallet delta") && !S(r, "inferred").Contains("claim")).Done();

            // Pending -> committed computed replay equality, and projected metadata becoming stored.
            r = Json(ctx, "c36-diff-commit", "diff", "--before", In("r6-pending-finish-house-spin.json"), "--after", In("r6-committed-finish-house-spin.json"));
            new Case(ctx, "c36-diff-commit", "literal pending -> committed pair matches the in-memory replay")
                .Code(r, 0).Eq(r, "computed[0].check", "afterEqualsInMemoryReplayOfBeforePending").Eq(r, "computed[0].result", "true").Done();
            r = Json(ctx, "c37-diff-adoption", "diff", "--before", In("fixtures-owned-universal-2.json"), "--after", In("r8-mixed-v3-pending.json"));
            new Case(ctx, "c37-diff-adoption", "first journey-era command stores previously projected journey metadata")
                .Code(r, 0).Expect("journey adoption inferred", S(r, "inferred").Contains("journey aggregate is now stored")).Done();

            // 8. Usage errors are stable.
            var usage = Cli2(ctx, "c38-usage", "inspect", "--input", In("r6-fresh.json"));
            var unknown = Cli2(ctx, "c38-usage-unknown", "inspect", "--workspace", ctx.Workspace, "--input", In("r6-fresh.json"), "--repair", "yes");
            new Case(ctx, "c38-usage", "missing workspace or unknown option is a usage error")
                .Code(usage, ExitCodes.Usage).Code(unknown, ExitCodes.Usage).Done();

            IntegrityDetector(ctx);
        }

        // The detector itself must fire: a changed byte and a new adjacent file are both reported.
        static void IntegrityDetector(Context ctx)
        {
            string dir = Path.Combine(ctx.Workspace, "integrity-probe");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "probe.json");
            File.Copy(Input(ctx, "r6-fresh.json"), file);
            var loaded = InputGuard.Load("input", ctx.Workspace, file);
            var clean = InputGuard.Integrity(loaded, out bool cleanIntact);
            File.WriteAllText(Path.Combine(dir, "jtn.recovery-primary.v1.json"), "{}");
            InputGuard.Integrity(loaded, out bool afterNeighbour);
            File.Delete(Path.Combine(dir, "jtn.recovery-primary.v1.json"));
            File.AppendAllText(file, " ");
            var changed = InputGuard.Integrity(loaded, out bool afterByte);
            new Case(ctx, "c39-integrity-detector", "integrity check reports an untouched input, then detects an adjacent file and a changed byte")
                .Expect("clean input intact", cleanIntact && (bool)clean.Get("unchanged"))
                .Expect("adjacent file detected", !afterNeighbour)
                .Expect("changed byte detected", !afterByte && !(bool)changed.Get("unchanged")).Done();
            Directory.Delete(dir, true);
        }

        static Result Rejected(Context ctx, string id, string name, string category)
        {
            var r = Json(ctx, id, "inspect", "--input", Input(ctx, name));
            new Case(ctx, id, "production rejects " + name).Code(r, ExitCodes.SnapshotRejected).Eq(r, "status", "snapshot-rejected")
                .Eq(r, "error.category", category).Expect("no facts", At(r, "facts") == null).Intact(r).Done();
            return r;
        }

        static void Refused(Context ctx, string id, string path, string category)
        {
            var r = Json(ctx, id, "inspect", "--input", path);
            new Case(ctx, id, "boundary refuses " + category).Code(r, ExitCodes.InputRefused).Eq(r, "error.category", category).Done();
        }

        static void LinkCase(Context ctx)
        {
            string link = Input(ctx, "zz-link.json");
            try { File.CreateSymbolicLink(link, Input(ctx, "r6-fresh.json")); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is PlatformNotSupportedException)
            {
                ctx.Cases.Add(new Obj().Add("id", "c34-reparse-point").Add("purpose", "symbolic link input is refused")
                    .Add("passed", null).Add("skipped", "symbolic link creation not permitted for this account; refusal path not exercised"));
                return;
            }
            try
            {
                var r = Json(ctx, "c34-reparse-point", "inspect", "--input", link);
                new Case(ctx, "c34-reparse-point", "symbolic link input is refused").Code(r, ExitCodes.InputRefused).Eq(r, "error.category", "reparse-point").Done();
            }
            finally { File.Delete(link); }
        }
    }
}
