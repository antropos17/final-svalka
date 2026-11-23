using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Content.Tests
{
    public static class ChapterRegression
    {
        static readonly string[] Assets = { "Editable/HouseStage0", "Editable/HouseStage1", "Editable/HouseStage2" };
        static readonly long[,] ExpectedPrices = {
            {40,600,420,680}, {120,260,380,620}, {180,320,400,640},
            {240,360,440,660}, {160,300,360,580}, {200,340,460,700}
        };
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
            throw new Exception("Expected rejection.");
        }
        static void RejectArgument(Action action, string parameter)
        {
            try { action(); } catch (ArgumentException error) { Check(error.ParamName == parameter, "Wrong rejection precedence/parameter: " + error.ParamName); return; }
            throw new Exception("Expected argument rejection: " + parameter);
        }
        static ChapterRules Rules() { return new ChapterRules(ChapterData.TwoEras(), Assets); }
        static string Key(ChapterProgress state) { return state.EraId + ":" + string.Join(",", state.CompletedUpgradeIds.OrderBy(x => x, StringComparer.Ordinal)); }
        static ChapterDefinition Alter(int eraIndex, Func<CatalogDefinition, CatalogDefinition> edit)
        {
            var d = ChapterData.TwoEras(); var eras = d.Eras.ToArray(); eras[eraIndex] = new EraDefinition(eras[eraIndex].Id, edit(eras[eraIndex].Catalog));
            return new ChapterDefinition(d.Id, eras);
        }
        static CatalogDefinition Copy(CatalogDefinition d, IEnumerable<StageDefinition> stages = null, IEnumerable<UpgradeDefinition> upgrades = null)
        { return new CatalogDefinition(d.Id, d.Proposed, d.Families, stages ?? d.Stages, upgrades ?? d.Upgrades, d.CompletionUpgradeIds); }
        static UpgradeDefinition Change(UpgradeDefinition u, long? price, IEnumerable<string> prerequisites = null, IEnumerable<ContentEffect> effects = null, string benefit = null)
        { return new UpgradeDefinition(u.Id, u.FromStageId, u.ToStageId, price, u.Enabled, u.Provisional, prerequisites ?? u.Prerequisites, effects ?? u.Effects, benefitSummary: benefit ?? u.BenefitSummary); }
        static void Issue(ChapterDefinition data, string code)
        { Check(ChapterValidator.Validate(data, Assets).Any(i => i.Code == code), "Missing diagnostic: " + code); }
        static ChapterProgress FirstEraFinished(ChapterRules rules)
        {
            var progress = ChapterProgress.Fresh();
            while (rules.AvailableUpgrades(progress).Count != 0) progress = rules.ProposePurchase(progress, rules.AvailableUpgrades(progress)[0], 10000).After;
            return progress;
        }

        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>(); Action<string, Action> run = (name, action) => { action(); passed.Add(name); };
            run("two active eras define the same six objects, all 36 states and 24 exact prices", () => {
                var definition = ChapterData.TwoEras(); Check(ChapterValidator.Validate(definition, Assets).Count == 0, "Invalid chapter");
                Check(definition.Eras.SelectMany(e => e.Catalog.Stages).Select(s => s.Id).Distinct().Count() == 36, "State reuse");
                long total = 0;
                for (int era = 0; era < 2; era++) for (int family = 0; family < 6; family++) for (int level = 1; level <= 2; level++)
                {
                    var id = ChapterIds.Project(definition.Eras[era].Id, FamilyIds.All[family], level);
                    var u = definition.Eras[era].Catalog.Upgrades.Single(x => x.Id == id);
                    Check(u.Price == ExpectedPrices[family, era * 2 + level - 1] && u.Enabled && !u.BenefitPending && !string.IsNullOrEmpty(u.BenefitSummary), "Incomplete or wrong tuning: " + id);
                    total += u.Price.Value;
                }
                Check(total == 9460, "Independent total price oracle");
            });
            run("full seeded traversal visits 36 states and finishes with 540 coins", () => {
                string firstPath = null;
                foreach (uint initialSeed in new uint[] { 17, 9047, 20261029 })
                {
                    var fixture = Traverse(initialSeed);
                    Check(fixture.Coins == 540 && fixture.Progress.CompletedUpgradeIds.Count == 24 && fixture.VisitedStageIds.Count == 36 && fixture.Trace.Count == 25, "Wrong fixture final state");
                    Check(fixture.Progress.EraId == "workshop-yard" && Rules().IsChapterComplete(fixture.Progress), "Wrong era/completion");
                    foreach (var family in FamilyIds.All) Check(Rules().FamilyStage(fixture.Progress, family).AbsoluteLevel == 5, "Not all objects complete");
                    string path = string.Join(",", fixture.Trace);
                    if (firstPath == null) firstPath = path; else Check(path != firstPath, "Seeded order did not vary");
                }
            });
            run("all 1216 reachable ownership states have a valid next action or finite end", () => {
                var rules = Rules(); var queue = new Queue<ChapterProgress>(); var visited = new HashSet<string>();
                queue.Enqueue(ChapterProgress.Fresh()); int edges = 0, completed = 0;
                while (queue.Count > 0)
                {
                    var state = queue.Dequeue(); if (!visited.Add(Key(state))) continue;
                    Check(Key(rules.FromStages(rules.Stages(state))) == Key(state), "Shared FamilyStages roundtrip lost ownership");
                    var snapshot = rules.Stages(state);
                    Check(snapshot.Entries.Select(e => e.FamilyId).SequenceEqual(FamilyIds.All), "Projection reordered the six world objects");
                    foreach (string family in FamilyIds.All)
                    {
                        // Independent ownership-count oracle, not another stage projection.
                        string first = family == "house" ? "house.repair" : family + ".build";
                        string second = family == "house" ? "house.improve" : family + ".improve";
                        int expected = 0;
                        if (state.EraId == "workshop-yard")
                        { expected = 3; first = family + ".workshop-yard.upgrade-1"; second = family + ".workshop-yard.upgrade-2"; }
                        expected += state.CompletedUpgradeIds.Contains(first) ? 1 : 0;
                        expected += state.CompletedUpgradeIds.Contains(second) ? 1 : 0;
                        Check(snapshot.AbsoluteLevel(family) == expected && rules.FamilyStage(state, family).AbsoluteLevel == expected, "Wrong independent family level: " + Key(state) + "/" + family);
                    }
                    var available = rules.AvailableUpgrades(state);
                    if (available.Count == 0)
                    {
                        if (rules.CanUnlockWorkshopYard(state)) { queue.Enqueue(rules.ProposeUnlockWorkshopYard(state, 0).After); edges++; }
                        else { Check(rules.IsChapterComplete(state), "Progression trap: " + Key(state)); completed++; }
                    }
                    foreach (string id in available)
                    {
                        var p = rules.ProposePurchase(state, id, 10000); Check(p.After.CompletedUpgradeIds.Count == state.CompletedUpgradeIds.Count + 1, "Hidden purchases");
                        queue.Enqueue(p.After); edges++;
                    }
                }
                Check(visited.Count == 1216 && completed == 1 && edges == 4781, "Unexpected reachable graph size: " + visited.Count + "/" + edges);
            });
            run("era unlock is explicit, once per committed state, preserves ownership and pays nothing", () => {
                var rules = Rules(); Reject(() => rules.ProposeUnlockWorkshopYard(ChapterProgress.Fresh(), 17));
                var first = FirstEraFinished(rules); Check(first.EraId == "junkyard" && !rules.IsChapterComplete(first) && rules.CanUnlockWorkshopYard(first), "Implicit era transition");
                var unlock = rules.ProposeUnlockWorkshopYard(first, 123);
                Check(unlock.OperationId == "era.workshop-yard.unlock" && unlock.Price == 0 && unlock.CoinsBefore == 123 && unlock.CoinsAfter == 123 && first.EraId == "junkyard", "Unlock mutated/granted value");
                Check(unlock.After.CompletedUpgradeIds.SequenceEqual(first.CompletedUpgradeIds), "Ownership reset");
                foreach (string family in FamilyIds.All) Check(rules.FamilyStage(first, family).AbsoluteLevel == 2 && rules.FamilyStage(unlock.After, family).AbsoluteLevel == 3, "Object regressed at era boundary");
                Reject(() => rules.ProposeUnlockWorkshopYard(unlock.After, 123)); Check(!rules.CanUnlockWorkshopYard(unlock.After), "Repeated unlock");
            });
            run("purchases are immutable proposals and insufficient/duplicate/locked actions change nothing", () => {
                var rules = Rules(); var fresh = ChapterProgress.Fresh(); string before = Key(fresh);
                Reject(() => rules.ProposePurchase(fresh, "house.repair", 39)); Reject(() => rules.ProposePurchase(fresh, "workshop.build", 10000));
                Reject(() => rules.ProposePurchase(fresh, "house.workshop-yard.upgrade-1", 10000)); Reject(() => rules.ProposePurchase(fresh, "unknown", 10000));
                var p = rules.ProposePurchase(fresh, "house.repair", 40); Check(p.CoinsAfter == 0 && p.Price == 40 && p.CatalogId == ChapterContentIds.Catalog && Key(fresh) == before, "Mutation or cost error");
                Reject(() => rules.ProposePurchase(p.After, "house.repair", 10000)); Reject(() => rules.ProposePurchase(fresh, "house.repair", -1));
                Check(rules.AvailableUpgrades(p.After).Count == 6, "Alternative branches missing");
                Reject(() => rules.ProposePurchase(fresh, GameIds.SlotCoinBoost, 10000));
            });
            run("all 17 valid r7 ownership combinations and every selected target project exactly", () => {
                var rules = Rules(); int combinations = 0;
                for (int house = 0; house <= 2; house++) for (int workshop = 0; workshop <= 1; workshop++) for (int recycler = 0; recycler <= 1; recycler++) for (int pigeon = 0; pigeon <= 1; pigeon++)
                {
                    if (house == 0 && workshop + recycler + pigeon != 0) { Reject(() => rules.ProjectR7(house, workshop, recycler, pigeon)); continue; }
                    combinations++; var projected = rules.ProjectR7(house, workshop, recycler, pigeon);
                    var expected = new List<string>(); if (house >= 1) expected.Add("house.repair"); if (house == 2) expected.Add("house.improve");
                    if (workshop == 1) expected.Add("workshop.build"); if (recycler == 1) expected.Add("recycler.build"); if (pigeon == 1) expected.Add("pigeon-loft.build");
                    Check(projected.Progress.EraId == "junkyard" && projected.Progress.CompletedUpgradeIds.SequenceEqual(expected), "Projection changed r7 ownership");
                    for (int target = 1; target <= 5; target++)
                    {
                        bool eligible = target == 1 ? house == 0 : target == 2 ? house == 1 : house >= 1 && (target == 3 ? workshop == 0 : target == 4 ? recycler == 0 : pigeon == 0);
                        if (eligible) Check(rules.ProjectR7(house, workshop, recycler, pigeon, target).SelectedUpgradeId == new[] { "house.repair", "house.improve", "workshop.build", "recycler.build", "pigeon-loft.build" }[target - 1], "Target identity changed");
                        else Reject(() => rules.ProjectR7(house, workshop, recycler, pigeon, target));
                    }
                    Check(rules.FamilyStage(projected.Progress, "cart").Stage.Level == 0 && rules.FamilyStage(projected.Progress, "power-source").Stage.Level == 0, "Invented old ownership");
                }
                Check(combinations == 17, "Missing legacy fixture");
                var completeR7 = rules.ProjectR7(2, 1, 1, 1).Progress; Check(!rules.CanUnlockWorkshopYard(completeR7) && !rules.IsChapterComplete(completeR7), "r7 completion skipped new chapter content");
                Reject(() => rules.ProjectR7(2, 1, 1, 1, 6)); Reject(() => rules.ProjectR7(2, 1, 1, 1, 0, 2));
            });
            run("r7 effect meaning and carry cadence stay exact while symbol benefits are concrete", () => {
                var rules = Rules(); var r7 = rules.OwnedBenefits(rules.ProjectR7(2, 1, 1, 1).Progress);
                Check(r7.Count == 4 && r7.All(e => e.Kind != ContentEffectKind.SymbolCoins), "Retroactive new effect on old ownership");
                Check(r7.Single(e => e.EffectId == "workshop.spin-coins").Amount == 1 && r7.Single(e => e.EffectId == "recycler.trash-coins").Amount == 1, "r7 rates changed");
                var delivery = r7.Single(e => e.EffectId == "pigeon.delivery-coins"); Check(delivery.Amount == 3 && delivery.Threshold == 3, "Carry interpretation changed");
                var final = Traverse(17).Progress; var all = rules.OwnedBenefits(final); var symbol = all.Where(e => e.Kind == ContentEffectKind.SymbolCoins).ToArray();
                Check(symbol.Length == 19 && all.Count == 23 && symbol.Select(e => e.EffectId).Distinct().Count() == 19, "Source descriptor collision");
                Check(symbol.Where(e => e.SymbolId == 0).Sum(e => e.Amount.Value) == 6 && symbol.Where(e => e.SymbolId == 1).Sum(e => e.Amount.Value) == 7 && symbol.Where(e => e.SymbolId == 2).Sum(e => e.Amount.Value) == 3 && symbol.Where(e => e.SymbolId == 3).Sum(e => e.Amount.Value) == 3, "Wrong independent symbol totals");
                var modifiers = rules.SymbolModifiers(final); Check(modifiers.Sources.Count == 19 && modifiers.CoinsPerSymbol(0) == 6 && modifiers.CoinsPerSymbol(1) == 7 && modifiers.CoinsPerSymbol(2) == 3 && modifiers.CoinsPerSymbol(3) == 3, "Shared symbol modifiers mismatch");
                Check(rules.SymbolModifiers(rules.ProjectR7(2, 1, 1, 1).Progress).Sources.Count == 0, "Old r7 gained symbol modifiers");
                rules.ValidateSymbolModifiers(final, "chapter.rules.v1", modifiers);
                Reject(() => rules.ValidateSymbolModifiers(final, "chapter.rules.v2", modifiers));
                Reject(() => rules.ValidateSymbolModifiers(final, "chapter.rules.v1", ChapterSymbolModifiers.Empty));
                Reject(() => rules.ValidateSymbolModifiers(ChapterProgress.Fresh(), "chapter.rules.v1", modifiers));
                var wrong = modifiers.Sources.ToArray(); wrong[0] = new SymbolCoinSource(wrong[0].SourceId, wrong[0].SymbolId, 2);
                Reject(() => rules.ValidateSymbolModifiers(final, "chapter.rules.v1", new ChapterSymbolModifiers(wrong)));
            });
            run("invalid era/ownership/cross-era snapshots fail closed without optional skills", () => {
                var rules = Rules(); Reject(() => rules.ValidateProgress(new ChapterProgress("third-era", new string[0])));
                Reject(() => rules.ValidateProgress(new ChapterProgress("workshop-yard", new string[0])));
                Reject(() => rules.ValidateProgress(new ChapterProgress("junkyard", new[] { "house.repair", "house.repair" })));
                Reject(() => rules.ValidateProgress(new ChapterProgress("junkyard", new[] { "workshop.improve" })));
                Reject(() => rules.ValidateProgress(new ChapterProgress("junkyard", new[] { "house.workshop-yard.upgrade-1" })));
                Reject(() => rules.ValidateProgress(new ChapterProgress("junkyard", new[] { "slot.coin-boost" })));
                Reject(() => rules.ValidateProgress(null));
                Check(rules.IsChapterComplete(Traverse(17).Progress), "Optional skills required for completion");
            });
            run("stage projections reject invalid ownership and keep each immutable result independent", () => {
                var rules = Rules();
                foreach (var invalid in new[] {
                    new ChapterProgress("third-era", new string[0]), new ChapterProgress("workshop-yard", new string[0]),
                    new ChapterProgress("junkyard", new[] { "house.repair", "house.repair" }),
                    new ChapterProgress("junkyard", new[] { "workshop.improve" }),
                    new ChapterProgress("junkyard", new[] { "house.workshop-yard.upgrade-1" }),
                    new ChapterProgress("junkyard", new[] { "unknown" }), new ChapterProgress("junkyard", new string[] { null }),
                    new ChapterProgress("junkyard", Enumerable.Repeat("house.repair", 25)), null })
                { RejectArgument(() => rules.Stages(invalid), "progress"); RejectArgument(() => rules.FamilyStage(invalid, "house"), "progress"); RejectArgument(() => rules.FamilyStage(invalid, "unknown"), "progress"); }
                RejectArgument(() => rules.FamilyStage(ChapterProgress.Fresh(), "unknown"), "family");
                var input = new List<string> { "house.repair" }; var before = new ChapterProgress("junkyard", input);
                var early = rules.Stages(before); input.Add("house.improve");
                var late = rules.Stages(Traverse(17).Progress);
                Check(early.EraId == "junkyard" && early.AbsoluteLevel("house") == 1 && early.AbsoluteLevel("workshop") == 0, "Later input/projection mutated an earlier result");
                Check(late.EraId == "workshop-yard" && FamilyIds.All.All(id => late.AbsoluteLevel(id) == 5), "Later projection reused earlier ownership");
                Check(before.CompletedUpgradeIds.Count == 1 && rules.Stages(before).AbsoluteLevel("house") == 1, "Projection retained caller-owned mutable state");
            });
            run("chapter rejects broken graphs, missing assets and altered r7 meaning", () => {
                Issue(Alter(0, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], 41); return Copy(d, upgrades: u); }), "chapter-r7-meaning");
                Issue(Alter(0, d => { var u = d.Upgrades.ToArray(); u[4] = Change(u[4], u[4].Price, new[] { "missing" }); return Copy(d, upgrades: u); }), "dangling-prerequisite");
                Issue(Alter(1, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], u[0].Price, new[] { u[1].Id }); return Copy(d, upgrades: u); }), "prerequisite-cycle");
                Issue(Alter(1, d => Copy(d, stages: d.Stages.Take(17))), "chapter-size");
                Check(ChapterValidator.Validate(ChapterData.TwoEras(), new string[0]).Count(i => i.Code == "missing-asset") == 3, "Missing asset accepted");
                Issue(Alter(1, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], null); return Copy(d, upgrades: u); }), "chapter-untuned");
            });
            run("unknown symbol sources, wrong symbol kinds and missing concrete benefits are rejected", () => {
                foreach (var e in new[] { new ContentEffect("unknown.symbol-coins", ContentEffectKind.SymbolCoins, 1, true, symbolId: 0), new ContentEffect("house.workshop-yard.upgrade-1.symbol-coins", ContentEffectKind.SymbolCoins, 1, true, symbolId: 99) })
                    Issue(Alter(1, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], u[0].Price, effects: new[] { e }); return Copy(d, upgrades: u); }), "effect-unknown");
                Issue(Alter(1, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], u[0].Price, effects: new ContentEffect[0]); return Copy(d, upgrades: u); }), "chapter-benefit");
                Issue(Alter(1, d => { var u = d.Upgrades.ToArray(); u[0] = Change(u[0], u[0].Price, benefit: " "); return Copy(d, upgrades: u); }), "chapter-untuned");
            });
            return passed.AsReadOnly();
        }

        // Fully executable deterministic traversal fixture for integration. Pure proposals only:
        // fixture starts with10000 coins (not a save grant), purchases24 times and unlocks once.
        // It does not run spins, skills, offline time, Core writes or Effects evaluation.
        public static ChapterTraversalFixture Traverse(uint seed)
        {
            var rules = Rules(); var progress = ChapterProgress.Fresh(); long coins = 10000;
            var trace = new List<string>(); var stages = new HashSet<string>();
            Action observe = () => { foreach (string family in FamilyIds.All) stages.Add(rules.FamilyStage(progress, family).Stage.Id); };
            observe();
            for (int action = 0; action < 25; action++)
            {
                ChapterProposal proposal;
                if (rules.CanUnlockWorkshopYard(progress)) proposal = rules.ProposeUnlockWorkshopYard(progress, coins);
                else
                {
                    var available = rules.AvailableUpgrades(progress).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    Check(available.Length > 0, "Traversal stopped before chapter end");
                    seed = unchecked(seed * 1664525u + 1013904223u);
                    proposal = rules.ProposePurchase(progress, available[seed % (uint)available.Length], coins);
                }
                progress = proposal.After; coins = proposal.CoinsAfter; trace.Add(proposal.OperationId); observe();
            }
            return new ChapterTraversalFixture(progress, coins, trace, stages.OrderBy(x => x, StringComparer.Ordinal));
        }
    }
    public sealed class ChapterTraversalFixture
    {
        public ChapterProgress Progress { get; }
        public long Coins { get; }
        public IReadOnlyList<string> Trace { get; }
        public IReadOnlyList<string> VisitedStageIds { get; }
        internal ChapterTraversalFixture(ChapterProgress progress, long coins, IEnumerable<string> trace, IEnumerable<string> stages)
        { Progress = progress; Coins = coins; Trace = new List<string>(trace).AsReadOnly(); VisitedStageIds = new List<string>(stages).AsReadOnly(); }
    }
}
