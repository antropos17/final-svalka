using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    internal static class CurrentRegressions
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        internal static List<Obj> Run(string repo, string workspace)
        {
            string root = SelfTest.PrepareWorkspace(repo, workspace);
            var cases = new List<Obj>();
            void Case(string id, Action action)
            {
                var result = new Obj().Add("id", id);
                try { action(); result.Add("passed", true); }
                catch (Exception error) when (!(error is OutOfMemoryException))
                { result.Add("passed", false).Add("failure", error.GetType().Name + ": " + Report.Ascii(error.Message)); }
                cases.Add(result);
            }
            string literal = Path.Combine(repo, "Checks/EraMigration/Fixtures/r7/owned-selected-house.json");
            byte[] original = File.ReadAllBytes(literal);
            string oldPath = Path.Combine(root, "literal-selected600.json"); File.WriteAllBytes(oldPath, original);
            var old = Codec.Decode(Encoding.UTF8.GetString(original));
            // Synthetic wallet only: no normal save is read or repaired. Production commands
            // choose the offer and freeze its operation. Independent oracles below are literals.
            var funded = old.Copy(); funded.coins = 470; GameLogic.Validate(funded);
            string fundedPath = Save(root, "legacy470.json", funded);
            var selected = Commit(JourneyEconomy.ReserveChapter(funded, "select", GameIds.HouseImprove, funded.sequence + 1));
            string selectedPath = Save(root, "current-selected480.json", selected);
            var enough = old.Copy(); enough.coins = 550; GameLogic.Validate(enough);
            var pending = JourneyEconomy.ReserveChapter(enough, "select", GameIds.HouseImprove, enough.sequence + 1);
            string pendingPath = Save(root, "current-pending480.json", pending);
            string paidPath = Save(root, "current-paid480.json", Commit(pending));

            Case("current-funded500-pays480-leaves20", () => {
                var wallet500 = old.Copy(); wallet500.coins = 500; GameLogic.Validate(wallet500);
                var paid = Commit(JourneyEconomy.ReserveChapter(wallet500, "select", GameIds.HouseImprove, wallet500.sequence + 1));
                Need(paid.coins == 20 && paid.houseStage == 2 && paid.chapter.SelectedProjectId == null &&
                    paid.chapter.ConstructionPriceRevision == "construction.prices.v3" && paid.seed == wallet500.seed,
                    "Explicit current selection must pay480 once, leave20 and preserve RNG");
                string path = Save(root, "current-paid-from500.json", paid);
                using (var report = Run(root, "inspect", "--input", path))
                    Need(report.RootElement.GetProperty("facts").GetProperty("wallet").GetProperty("coins").GetInt64() == 20,
                        "Inspector lost the funded current result");
                using (var report = Run(root, "preview-pending", "--input", path, expected: 5))
                    Need(report.RootElement.GetProperty("status").GetString() == "not-applicable", "Funded result replayed");
            });

            Case("historical-v2-selected540-pending10-and-paid", () => {
                // Literal r11 artifacts are copied byte-for-byte, never regenerated or repriced.
                string Copy(string name) {
                    string source = Path.Combine(repo, "tools/SaveInspector/evidence/r11", name);
                    string destination = Path.Combine(root, "historical-v2-" + name);
                    File.Copy(source, destination); return destination;
                }
                string selectedV2 = Copy("current-selected540.json");
                string pendingV2 = Copy("current-pending540.json");
                string paidV2 = Copy("current-paid540.json");
                using (var report = Run(root, "inspect", "--input", selectedV2)) {
                    var selection = report.RootElement.GetProperty("facts").GetProperty("selection");
                    Need(selection.GetProperty("selectedPrice").GetInt64() == 540 &&
                        selection.GetProperty("constructionPriceRevision").GetString() == "construction.prices.v2" &&
                        selection.GetProperty("currentReselectionOfferPrice").GetInt64() == 480,
                        "Literal V2 selected540 must survive alongside current480");
                }
                using (var report = Run(root, "preview-pending", "--input", pendingV2))
                    Need(report.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64() == 10,
                        "Literal V2 frozen550-540 must remain10");
                using (var report = Run(root, "preview-pending", "--input", paidV2, expected: 5))
                    Need(report.RootElement.GetProperty("status").GetString() == "not-applicable", "Literal V2 paid state replayed");
            });

            Case("current-build-source-identity", () => {
                // Independently enumerate the seven exact production directories in the
                // project declaration. Compare disk bytes with the embedded build manifest.
                var lines = new List<string>(); int count = 0;
                foreach (string dir in new[] { "Core", "Contracts", "Content", "Effects", "Upgrades", "Orders", "Guidance" })
                    foreach (string file in Directory.GetFiles(Path.Combine(repo, "Unity/Assets/Scripts", dir), "*.cs").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    { lines.Add("../../Unity/Assets/Scripts/" + dir + "/" + Path.GetFileName(file) + "=" + InputGuard.Hash(File.ReadAllBytes(file)).ToUpperInvariant()); count++; }
                string manifest = string.Join("\n", lines) + "\n";
                using (var report = Run(root, "inspect", "--input", oldPath))
                {
                    var identity = report.RootElement.GetProperty("tool").GetProperty("productionSources");
                    Need(identity.GetProperty("fileCount").GetInt32() == count, "Linked source count mismatch");
                    Need(identity.GetProperty("sha256").GetString() == InputGuard.HashText(manifest), "Linked source fingerprint mismatch");
                    Need(!report.RootElement.GetProperty("tool").TryGetProperty("productionPin", out _), "Stale Git pin remains");
                }
            });
            Case("current-literal600-is-preserved", () => {
                using (var report = Run(root, "inspect", "--input", oldPath))
                {
                    var selection = report.RootElement.GetProperty("facts").GetProperty("selection");
                    Need(selection.GetProperty("selectedPrice").GetInt64() == 600, "Literal legacy promise changed");
                    Need(selection.GetProperty("constructionPriceRevision").GetString() == "construction.prices.v1", "Legacy revision lost");
                    Need(selection.GetProperty("constructionPriceRevisionSource").GetString() == Facts.Projected, "Absent aggregate presented as stored");
                    Need(selection.GetProperty("currentReselectionOfferPrice").GetInt64() == 480, "Current offer missing");
                }
            });
            Case("current-selected480-and-semantic-diff", () => {
                using (var report = Run(root, "inspect", "--input", selectedPath))
                {
                    var facts = report.RootElement.GetProperty("facts"); var selection = facts.GetProperty("selection");
                    Need(facts.GetProperty("wallet").GetProperty("coins").GetInt64() == 470, "Unfunded selection debited wallet");
                    Need(selection.GetProperty("selectedPrice").GetInt64() == 480 && !selection.GetProperty("affordableNow").GetBoolean(), "Wrong current selection price/affordability");
                    Need(selection.GetProperty("constructionPriceRevision").GetString() == "construction.prices.v3", "Current revision missing");
                    Need(selection.GetProperty("constructionPriceRevisionSource").GetString() == Facts.Stored, "Stored revision presented as projected");
                }
                using (var diff = Run(root, "diff", "--before", fundedPath, "--after", selectedPath))
                {
                    var changes = diff.RootElement.GetProperty("changes").EnumerateArray().ToArray();
                    Need(!diff.RootElement.GetProperty("equal").GetBoolean(), "Repricing presented as equal");
                    Need(changes.Any(c => c.GetProperty("path").GetString() == "selection.selectedPrice" && c.GetProperty("before").GetString() == "600" && c.GetProperty("after").GetString() == "480"), "Missing independent 600->480 price diff");
                    Need(changes.Any(c => c.GetProperty("path").GetString() == "selection.constructionPriceRevision" &&
                        c.GetProperty("before").GetString() == "construction.prices.v1" &&
                        c.GetProperty("after").GetString() == "construction.prices.v3"), "Missing independent V1->V3 revision diff");
                }
            });
            Case("current-pending480-replays-to70-once", () => {
                using (var inspected = Run(root, "inspect", "--input", pendingPath))
                {
                    var frozen = inspected.RootElement.GetProperty("facts").GetProperty("pending").GetProperty("frozenChapter");
                    Need(frozen.GetProperty("price").GetInt64() == 480, "Frozen current debit is not 480");
                    Need(frozen.GetProperty("constructionPriceRevisionBefore").GetString() == "construction.prices.v1" && frozen.GetProperty("constructionPriceRevisionAfter").GetString() == "construction.prices.v3", "Frozen revision transition lost");
                }
                using (var preview = Run(root, "preview-pending", "--input", pendingPath))
                    Need(preview.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64() == 70, "550-480 must leave 70");
                using (var diff = Run(root, "diff", "--before", pendingPath, "--after", paidPath))
                    Need(diff.RootElement.GetProperty("changes").GetArrayLength() > 0, "Pending commit lost");
                using (var noReplay = Run(root, "preview-pending", "--input", paidPath, expected: 5))
                    Need(noReplay.RootElement.GetProperty("status").GetString() == "not-applicable", "Paid state replayed twice");
            });
            Case("current-literal600-pending-replay-still4", () => {
                string copy = Path.Combine(root, "literal-pending600.json");
                File.Copy(Path.Combine(repo, "Checks/EraMigration/Fixtures/r6/pending-finish-house-spin.json"), copy);
                using (var preview = Run(root, "preview-pending", "--input", copy))
                    Need(preview.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64() == 4, "Literal 595+9-600 must remain 4");
            });
            Need(File.ReadAllBytes(literal).SequenceEqual(original), "Original literal changed");
            return cases;
        }
        static ModelState Commit(ModelState pending) { return Codec.Decode(Codec.Encode(GameLogic.ApplyPending(pending))); }
        static string Save(string root, string name, ModelState state)
        { GameLogic.Validate(state); string path = Path.Combine(root, name); File.WriteAllText(path, Codec.Encode(state), new UTF8Encoding(false)); return path; }
        static JsonDocument Run(string root, string command, string key, string value, string key2 = null, string value2 = null, int expected = 0)
        {
            var args = new List<string> { command, "--workspace", root, key, value, "--format", "json" };
            if (key2 != null) { args.Add(key2); args.Add(value2); }
            var before = Directory.GetFiles(root).ToDictionary(p => p, p => InputGuard.Hash(File.ReadAllBytes(p)));
            using (var stream = new MemoryStream())
            {
                int code = Cli.Run(args.ToArray(), stream);
                Need(code == expected, "Unexpected CLI exit " + code);
                Need(Directory.GetFiles(root).Length == before.Count && before.All(p => File.Exists(p.Key) && InputGuard.Hash(File.ReadAllBytes(p.Key)) == p.Value), "CLI changed private inputs");
                return JsonDocument.Parse(stream.ToArray());
            }
        }
        static void Need(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
