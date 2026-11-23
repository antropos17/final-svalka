using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content.Tests
{
    public static class ContentRegression
    {
        static readonly string[] Assets = { "Editable/HouseStage0", "Editable/HouseStage1", "Editable/HouseStage2" };
        static CatalogDefinition Baseline() { return CatalogData.CurrentPrototype(40, 600, 120, 1); }
        static CatalogDefinition Change(CatalogDefinition d, IEnumerable<FamilyDefinition> families = null,
            IEnumerable<StageDefinition> stages = null, IEnumerable<UpgradeDefinition> upgrades = null,
            IEnumerable<string> completion = null, int version = 1)
        { return new CatalogDefinition(d.Id, d.Proposed, families ?? d.Families, stages ?? d.Stages, upgrades ?? d.Upgrades, completion ?? d.CompletionUpgradeIds, version); }
        static UpgradeDefinition ChangeUpgrade(UpgradeDefinition u, long? price, IEnumerable<string> prerequisites = null,
            IEnumerable<ContentEffect> effects = null, bool? enabled = null, bool? provisional = null)
        { return new UpgradeDefinition(u.Id, u.FromStageId, u.ToStageId, price, enabled ?? u.Enabled, provisional ?? u.Provisional, prerequisites ?? u.Prerequisites, effects ?? u.Effects); }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Equal(IEnumerable<string> actual, params string[] expected)
        { Check(actual.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal)), "Unexpected IDs: " + string.Join(",", actual)); }
        static void Reject(Action operation)
        { try { operation(); } catch (ArgumentException) { return; } throw new Exception("Expected explicit rejection."); }
        static void Issue(CatalogDefinition d, string code)
        { Check(CatalogValidator.Validate(d, Assets).Any(i => i.Code == code), "Missing diagnostic: " + code); }

        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            Action<string, Action> run = (name, action) => { action(); passed.Add(name); };
            run("current prices, stages and effects match the existing prototype", () => {
                var d = Baseline(); Check(CatalogValidator.Validate(d, Assets).Count == 0, "Invalid baseline");
                Check(d.Families.Count == 2 && d.Stages.Count == 5 && d.Upgrades.Count == 3, "Unexpected baseline size");
                Check(d.Upgrades.Single(u => u.Id == "house.repair").Price == 40, "Repair price");
                Check(d.Upgrades.Single(u => u.Id == "house.improve").Price == 600, "Improvement price");
                var workshop = d.Upgrades.Single(u => u.Id == "workshop.build");
                Check(workshop.Price == 120 && workshop.Provisional && workshop.Effects.Single().Amount == 1 && workshop.Effects.Single().EffectId == "workshop.spin-coins", "Workshop tuning");
                Check(d.Upgrades.Single(u => u.Id == "house.repair").Effects.Single().Amount == 0, "AUTO must not grant coins");
            });
            run("both purchase orders remain available and completion requires both projects", () => {
                var c = new ValidatedCatalog(Baseline(), Assets);
                Equal(c.AvailableUpgrades(new string[0]), "house.repair");
                Equal(c.AvailableUpgrades(new[] { "house.repair" }), "house.improve", "workshop.build");
                foreach (var order in new[] { new[] { "house.improve", "workshop.build" }, new[] { "workshop.build", "house.improve" } })
                {
                    var owned = new List<string> { "house.repair" };
                    Check(!c.IsChapterComplete(owned), "Early completion");
                    foreach (string id in order) { Check(c.AvailableUpgrades(owned).Contains(id), "Locked valid order"); owned.Add(id); }
                    Check(c.IsChapterComplete(owned) && c.AvailableUpgrades(owned).Count == 0 && c.RemainingCompletionUpgrades(owned).Count == 0, "Final completion");
                }
                Check(!c.IsChapterComplete(new[] { "house.repair", "house.improve" }), "House alone is not completion");
            });
            run("six sourced families have eighteen stages and stay disabled", () => {
                var d = CatalogData.ProposedFirstEra(); Check(CatalogValidator.Validate(d, Assets).Count == 0, "Invalid draft");
                Equal(d.Families.Select(f => f.Id), "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source");
                Check(d.Proposed && d.Stages.Count == 18 && d.Upgrades.Count == 12 && d.Upgrades.All(u => !u.Enabled && u.Provisional), "Draft enabled");
                Check(d.Upgrades.Where(u => u.Id != "house.repair" && u.Id != "house.improve" && u.Id != "workshop.build").All(u => !u.Price.HasValue), "Invented final prices");
                Check(d.Upgrades.Single(u => u.Id == "cart.build").BenefitPending && d.Upgrades.Single(u => u.Id == "power-source.build").BenefitPending, "Undecided benefits hidden");
                var c = new ValidatedCatalog(d, Assets); Check(c.AvailableUpgrades(new string[0]).Count == 0 && !c.IsChapterComplete(new string[0]), "Draft activated");
            });
            run("draft traversal reaches every final stage without purchase-order traps", () => {
                var d = CatalogData.ProposedFirstEra();
                foreach (var reverse in new[] { false, true })
                {
                    var done = new HashSet<string>(); var current = d.Families.ToDictionary(f => f.Id, f => f.Id + ".stage.0");
                    for (int step = 0; step < 12; step++)
                    {
                        var choices = d.Upgrades.Where(u => !done.Contains(u.Id) && u.Prerequisites.All(done.Contains) && current.Values.Contains(u.FromStageId)).OrderBy(u => u.Id, StringComparer.Ordinal).ToArray();
                        Check(choices.Length > 0, "Traversal trap"); var chosen = reverse ? choices.Last() : choices.First();
                        done.Add(chosen.Id); var family = d.Stages.Single(s => s.Id == chosen.ToStageId).FamilyId; current[family] = chosen.ToStageId;
                    }
                    Check(d.CompletionUpgradeIds.All(done.Contains) && current.Values.All(s => s.EndsWith(".2", StringComparison.Ordinal)), "Incomplete draft traversal");
                }
            });
            run("input arrays and exposed collections cannot mutate a catalog", () => {
                var prerequisites = new[] { "house.repair" }; var effects = new[] { new ContentEffect("workshop.spin-coins", ContentEffectKind.SpinCoins, 1, true) };
                var u = new UpgradeDefinition("workshop.build", "workshop.stage.0", "workshop.stage.1", 120, true, true, prerequisites, effects);
                prerequisites[0] = "unknown"; effects[0] = null;
                Check(u.Prerequisites[0] == "house.repair" && u.Effects[0] != null, "Retained input array");
                bool blocked = false; try { ((IList<string>)u.Prerequisites)[0] = "unknown"; } catch (NotSupportedException) { blocked = true; }
                Check(blocked, "Mutable result");
                var families = Baseline().Families.ToArray(); var d = Change(Baseline(), families: families); families[0] = null;
                Check(d.Families[0] != null, "Retained family array");
            });
            run("duplicate stable IDs fail in every namespace", () => {
                var d = Baseline(); Issue(Change(d, families: d.Families.Concat(new[] { d.Families[0] })), "duplicate-family");
                Issue(Change(d, stages: d.Stages.Concat(new[] { d.Stages[0] })), "duplicate-stage");
                Issue(Change(d, upgrades: d.Upgrades.Concat(new[] { d.Upgrades[0] })), "duplicate-upgrade");
            });
            run("dangling references and orphaned stages fail", () => {
                var d = Baseline(); var a = d.Upgrades.ToArray(); a[0] = new UpgradeDefinition("house.repair", "missing.stage.0", "house.stage.1", 40, true, false);
                Issue(Change(d, upgrades: a), "dangling-stage");
                Issue(Change(d, upgrades: d.Upgrades.Where(u => u.Id != "workshop.build")), "unreachable-stage");
                a = d.Upgrades.ToArray(); a[2] = ChangeUpgrade(a[2], 120, new[] { "missing.upgrade" }); Issue(Change(d, upgrades: a), "dangling-prerequisite");
                Issue(Change(d, stages: d.Stages.Concat(new[] { new StageDefinition("unknown.stage.0", "unknown", 0) })), "dangling-family");
            });
            run("cycles and completion blocked by cycles fail", () => {
                var d = Baseline(); var a = d.Upgrades.ToArray(); a[0] = ChangeUpgrade(a[0], 40, new[] { "workshop.build" });
                var broken = Change(d, upgrades: a); Issue(broken, "prerequisite-cycle"); Issue(broken, "unreachable-upgrade"); Issue(broken, "completion-unreachable");
            });
            run("invalid, unknown and oversized IDs and versions fail closed", () => {
                var d = Baseline(); Issue(Change(d, version: 2), "catalog-version");
                foreach (string id in new[] { null, "", "House", "a/b", new string('a', 65) })
                    Issue(Change(d, families: new[] { new FamilyDefinition(id, "Invalid") }), "invalid-id");
                var c = new ValidatedCatalog(d, Assets); UpgradeDefinition ignored; Check(!c.TryGetUpgrade(null, out ignored) && !c.TryGetUpgrade("missing", out ignored), "Unknown lookup");
            });
            run("prices reject zero, negative, overflow-range and untuned live values", () => {
                var d = Baseline(); foreach (long? value in new long?[] { 0, -1, long.MaxValue, null })
                { var a = d.Upgrades.ToArray(); a[0] = ChangeUpgrade(a[0], value); Issue(Change(d, upgrades: a), "price-invalid"); }
            });
            run("missing assets are errors even when placeholder fallback exists", () => {
                var d = Baseline(); Check(CatalogValidator.Validate(d, new string[0]).Count(i => i.Code == "missing-asset") == 3, "Missing asset silently accepted");
                var stages = d.Stages.ToArray(); stages[3] = new StageDefinition("workshop.stage.0", "workshop", 0, fallback: FallbackVisual.None);
                Issue(Change(d, stages: stages), "visual-missing");
                stages[3] = new StageDefinition("workshop.stage.0", "workshop", 0, fallback: (FallbackVisual)99); Issue(Change(d, stages: stages), "fallback-unknown");
            });
            run("closed effect whitelist rejects unknown IDs and kind mismatches", () => {
                var d = Baseline(); foreach (var e in new[] { new ContentEffect("unknown.effect", ContentEffectKind.SpinCoins, 1, true), new ContentEffect("workshop.spin-coins", ContentEffectKind.TrashSymbolCoins, 1, true), new ContentEffect("workshop.spin-coins", (ContentEffectKind)99, 1, true) })
                { var a = d.Upgrades.ToArray(); a[2] = ChangeUpgrade(a[2], 120, effects: new[] { e }); Issue(Change(d, upgrades: a), "effect-unknown"); }
            });
            run("effect amounts, thresholds, AUTO rewards and contribution collisions fail", () => {
                var d = Baseline(); foreach (long? amount in new long?[] { -1, long.MaxValue, null })
                { var a = d.Upgrades.ToArray(); a[2] = ChangeUpgrade(a[2], 120, effects: new[] { new ContentEffect("workshop.spin-coins", ContentEffectKind.SpinCoins, amount, true) }); Issue(Change(d, upgrades: a), "effect-amount"); }
                var b = d.Upgrades.ToArray(); b[2] = ChangeUpgrade(b[2], 120, effects: new[] { new ContentEffect("pigeon.delivery-coins", ContentEffectKind.PigeonDeliveryCoins, 3, true, 0) }); Issue(Change(d, upgrades: b), "effect-threshold");
                b = d.Upgrades.ToArray(); b[0] = ChangeUpgrade(b[0], 40, effects: new[] { new ContentEffect("house.unlock-auto", ContentEffectKind.UnlockAuto, 1, false) }); Issue(Change(d, upgrades: b), "auto-amount");
                b = d.Upgrades.ToArray(); b[1] = ChangeUpgrade(b[1], 600, effects: b[2].Effects); Issue(Change(d, upgrades: b), "duplicate-effect-source");
            });
            run("completion predicate rejects omissions, unknowns, duplicates and emptiness", () => {
                var d = Baseline(); Issue(Change(d, completion: new string[0]), "completion-empty");
                Issue(Change(d, completion: new[] { "house.improve" }), "completion-omits-family");
                Issue(Change(d, completion: new[] { "unknown" }), "completion-dangling");
                Issue(Change(d, completion: new[] { "house.improve", "workshop.build", "workshop.build" }), "completion-duplicate");
                var upgrades = d.Upgrades.ToArray(); upgrades[2] = ChangeUpgrade(upgrades[2], 120, enabled: false);
                Issue(Change(d, upgrades: upgrades), "completion-disabled");
            });
            run("stage skips, duplicate entries and missing previous-stage prerequisites fail", () => {
                var d = Baseline(); var a = d.Upgrades.ToArray(); a[1] = new UpgradeDefinition("house.improve", "house.stage.0", "house.stage.2", 600, true, false, new[] { "house.repair" }); Issue(Change(d, upgrades: a), "stage-transition");
                a = d.Upgrades.ToArray(); a[1] = ChangeUpgrade(a[1], 600, new string[0]); Issue(Change(d, upgrades: a), "stage-prerequisite");
                Issue(Change(d, upgrades: d.Upgrades.Concat(new[] { new UpgradeDefinition("house.other", "house.stage.0", "house.stage.1", 40, true, false) })), "duplicate-stage-entry");
                var stages = d.Stages.ToArray(); stages[2] = new StageDefinition("house.stage.2", "house", 4); Issue(Change(d, stages: stages), "stage-levels");
            });
            run("invalid progress snapshots never unlock or complete content", () => {
                var c = new ValidatedCatalog(Baseline(), Assets);
                Reject(() => c.IsChapterComplete(new[] { "house.improve", "workshop.build" }));
                Reject(() => c.AvailableUpgrades(new[] { "unknown" })); Reject(() => c.AvailableUpgrades(new[] { "house.repair", "house.repair" }));
                Reject(() => c.AvailableUpgrades(null));
                var draft = new ValidatedCatalog(CatalogData.ProposedFirstEra(), Assets); Reject(() => draft.IsChapterComplete(new[] { "house.repair" }));
                var d = Baseline(); var a = d.Upgrades.ToArray(); a[0] = ChangeUpgrade(a[0], 40, enabled: false); Issue(Change(d, upgrades: a), "disabled-prerequisite");
            });
            run("legacy projection preserves house stages, absent workshop and target IDs", () => {
                Equal(LegacyProgress.Project(0).CompletedUpgradeIds);
                Equal(LegacyProgress.Project(1).CompletedUpgradeIds, "house.repair");
                Equal(LegacyProgress.Project(2).CompletedUpgradeIds, "house.repair", "house.improve");
                Equal(LegacyProgress.Project(2, 1).CompletedUpgradeIds, "house.repair", "house.improve", "workshop.build");
                Check(LegacyProgress.Project(0, 0, 1).SelectedUpgradeId == "house.repair" && LegacyProgress.Project(1, 0, 2).SelectedUpgradeId == "house.improve" && LegacyProgress.Project(2, 0, 3).SelectedUpgradeId == "workshop.build", "Legacy target changed");
                Check(!new ValidatedCatalog(Baseline(), Assets).IsChapterComplete(LegacyProgress.Project(2).CompletedUpgradeIds), "Old house save gained workshop");
                Reject(() => LegacyProgress.Project(0, 1)); Reject(() => LegacyProgress.Project(3)); Reject(() => LegacyProgress.Project(1, 0, 99));
                Reject(() => LegacyProgress.Project(2, 1, 3)); Reject(() => LegacyProgress.Project(1, 0, 0, 2));
            });
            run("null data and excessive catalogs yield diagnostics without crashing", () => {
                Check(CatalogValidator.Validate(null, null).Single().Code == "catalog-null", "Null catalog");
                var d = Baseline(); Issue(Change(d, families: d.Families.Concat(new FamilyDefinition[] { null })), "family-null");
                Issue(Change(d, stages: d.Stages.Concat(new StageDefinition[] { null })), "stage-null");
                Issue(Change(d, upgrades: d.Upgrades.Concat(new UpgradeDefinition[] { null })), "upgrade-null");
                Issue(Change(d, families: Enumerable.Range(0, 257).Select(i => new FamilyDefinition("family" + i, "Family"))), "catalog-size");
            });
            run("bounded snapshots reject excess instead of accepting truncated definitions", () => {
                int consumed = 0;
                var families = Enumerable.Range(0, 1000000).Select(i => { consumed++; return new FamilyDefinition("family" + i, "Family"); });
                var oversized = Change(Baseline(), families: families); Check(consumed == 257, "Unbounded enumeration"); Issue(oversized, "catalog-size");
                var d = Baseline(); var upgrades = d.Upgrades.ToArray();
                upgrades[2] = ChangeUpgrade(upgrades[2], 120, Enumerable.Repeat("house.repair", 33)); Issue(Change(d, upgrades: upgrades), "upgrade-size");
                upgrades = d.Upgrades.ToArray(); upgrades[2] = ChangeUpgrade(upgrades[2], 120, effects: Enumerable.Repeat(upgrades[2].Effects[0], 17)); Issue(Change(d, upgrades: upgrades), "upgrade-size");
                upgrades = d.Upgrades.ToArray(); upgrades[2] = new UpgradeDefinition("workshop.build", "workshop.stage.0", "workshop.stage.1", 120, true, true, new[] { "house.repair" }, benefitPending: true);
                Issue(Change(d, upgrades: upgrades), "benefit-pending");
            });
            return passed.AsReadOnly();
        }
    }
}
