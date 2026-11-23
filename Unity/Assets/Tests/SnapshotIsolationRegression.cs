using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Effects;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    // Caller-owned data mutations only: no production-source edits or write faults.
    // Integrated constructionV3/current skillV3 rules: construction9310, six V3 skills610, final rates6/7/3/3.
    public static class SnapshotIsolationRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string primary, backup;
            public int writes;
            public string ReadPrimary() { return primary; }
            public string ReadBackup() { return backup; }
            public void Write(string raw) { backup = primary; primary = raw; writes++; }
        }
        sealed class Fixture
        {
            public Storage storage;
            public GameStore store;
            public GameSession session;
        }
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static readonly string[] Skills = { SkillIds.Universal1, SkillIds.Recycling1, SkillIds.Delivery1,
            SkillIds.Universal2, SkillIds.Recycling2, SkillIds.Delivery2 };
        static void Need(bool value, string why) { if (!value) throw new Exception("Snapshot isolation: " + why); }
        static void Symbols(int[] values, int a, int b, int c, string why)
        { Need(values != null && values.SequenceEqual(new[] { a, b, c }), why); }
        static void ReadOnly<T>(IReadOnlyList<T> values, T replacement, string why)
        {
            Need(values != null && values.Count > 0, why + " fixture is empty");
            // Immutable collections may safely share references and need not expose IList.
            var list = values as IList<T>;
            if (list == null) return;
            bool refused = false;
            try { list[0] = replacement; } catch (NotSupportedException) { refused = true; }
            Need(refused, why + " accepted replacement through IList");
            refused = false;
            try { list.Clear(); } catch (NotSupportedException) { refused = true; }
            Need(refused, why + " accepted Clear through IList");
        }
        static Fixture Late(bool skills)
        {
            var initial = ModelState.Fresh(1); initial.coins = 20000;
            var storage = new Storage { primary = Codec.Encode(initial) };
            var store = new GameStore(storage, Codec);
            var session = GameSession.Open(store, 1);
            for (int era = 0; era < 2; era++)
            {
                if (era == 1)
                {
                    session.EnterWorkshopYard(session.State.sequence + 1);
                    session.AcknowledgeEraTransition(session.State.sequence + 1);
                }
                for (int level = 1; level <= 2; level++) foreach (string family in FamilyIds.All)
                    session.SelectChapterProject(ChapterIds.Project(era == 0 ? EraIds.Junkyard : EraIds.WorkshopYard, family, level), session.State.sequence + 1);
            }
            if (skills) foreach (string id in Skills) session.BuySkill(ChapterEconomy.QuoteSkill(session.State, id).Request);
            Need(!session.RecoveryRequired && session.State.coins == (skills ? 10080 : 10690) && session.State.seed == 1,
                "late public-command fixture wallet/seed");
            Need(session.Chapter.Progress.CompletedUpgradeIds.Count == 24 && session.Chapter.Skills.Receipts.Count == (skills ? 6 : 0),
                "late fixture ownership");
            return new Fixture { storage = storage, store = store, session = session };
        }
        static void Original(Fixture f, string raw, int writes, long wallet, uint seed, string why)
        {
            Need(Codec.Encode(f.session.State) == raw && Codec.Encode(f.store.State) == raw && f.storage.primary == raw && f.storage.writes == writes,
                why + " changed complete authoritative snapshot or storage");
            Need(f.session.State.coins == wallet && f.session.State.seed == seed && f.session.State.pending == null &&
                f.session.Chapter.Progress.CompletedUpgradeIds.Count == 24 && f.session.Chapter.SelectedProjectId == null,
                why + " changed original wallet/seed/ownership/selection");
        }
        static ChapterOperation Reconstruct(ChapterOperation op, int[] input, OfflineReceipt receipt)
        {
            return new ChapterOperation(2, op.Kind, op.Sequence, op.At, op.ActionId, op.Price,
                op.CoinsBefore, op.CoinsAfter, op.SeedBefore, op.SeedAfter, input, op.BaseCoins,
                op.WorkshopCoins, op.Before, op.After, op.Effects, op.SkillRequest, receipt);
        }
        static void Damage(ModelState copy)
        {
            copy.coins = 777; copy.seed = 99; copy.sequence = 999; copy.revision = 1998;
            copy.houseStage = 0; copy.workshopBuilt = 0; copy.selectedStage = 1;
            copy.lastSettledAt = 0; copy.lastSettlementSeen = 0;
            if (copy.lastSettlement != null) { copy.lastSettlement.coins = 999; copy.lastSettlement.from = 0; }
            if (copy.lastSpin != null)
            {
                copy.lastSpin.symbols[0] = 3; copy.lastSpin.reward = 999; copy.lastSpin.sequence = 999;
                copy.lastSpin.chapter = null; copy.lastSpin.journey = null; copy.lastSpin.wave = null;
            }
            if (copy.pending != null)
            {
                if (copy.pending.symbols != null) copy.pending.symbols[0] = 3;
                copy.pending.sequence = 999; copy.pending.kind = "select"; copy.pending.target = 1;
                copy.pending.chapter = null; copy.pending.journey = null; copy.pending.wave = null;
            }
            copy.chapter = null; copy.wave = null; copy.journey = null;
        }
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();

            var legacy = GameLogic.Reserve(ModelState.Fresh(1), "spin", 0, 1);
            string legacyRaw = Codec.Encode(legacy);
            var legacyCopy = legacy.Copy(); Damage(legacyCopy);
            Need(Codec.Encode(legacy) == legacyRaw && legacy.coins == 37 && legacy.seed == 1 && legacy.pending.sequence == 1,
                "ModelState.Copy aliases legacy root/pending");
            Symbols(legacy.pending.symbols, 0, 0, 2, "legacy pending array leaked");
            var legacyApplied = GameLogic.ApplyPending(legacy);
            Need(legacyApplied.coins == 45 && legacyApplied.seed == 2647435461u && legacyApplied.sequence == 1,
                "copy mutation changed independent legacy37+8 outcome");
            var spinCopy = legacyApplied.lastSpin.Copy(); spinCopy.symbols[1] = 3; spinCopy.bonus = 999;
            Symbols(legacyApplied.lastSpin.symbols, 0, 0, 2, "SpinReceipt.Copy aliases symbols");
            passed.Add("Legacy root/pending/SpinReceipt copies isolate caller arrays; original37+8=45 and seed2647435461 remain exact");

            var f = Late(true);
            f.session.Checkpoint(1000); f.session.SettleOffline(1060);
            f.session.AcceptOrder(OrderIds.TrashSort, f.session.State.sequence + 1);
            var returned = f.session.ExecuteChapterSpin(f.session.State.sequence + 1);
            Need(returned.coins == 10110 && returned.seed == 2647435461u && returned.lastSettlement.coins == 1 &&
                f.session.Journey.Orders.Progress == 1, "full late fixture10080+offline1+spin29");
            string fullRaw = Codec.Encode(f.session.State); int writes = f.storage.writes;
            Damage(returned); Damage(f.session.State); Damage(f.store.State);
            Original(f, fullRaw, writes, 10110, 2647435461u, "State/command-return mutation");
            Need(f.session.Chapter.Skills.Receipts.Count == 6 && f.session.Journey.Orders.Progress == 1, "getter changed skills/order");
            passed.Add("Session.State, GameStore.State and command result isolate roots, lastSpin arrays, receipt and optional aggregate assignments");

            var pending = JourneyEconomy.ReserveChapter(f.session.State, "spin", null, f.session.State.sequence + 1, 1060, manualSpin: true);
            string pendingRaw = Codec.Encode(pending);
            var modernCopy = pending.Copy(); Damage(modernCopy);
            Need(Codec.Encode(pending) == pendingRaw && pending.coins == 10110 && pending.seed == 2647435461u &&
                pending.lastSettlement.coins == 1, "modern Copy aliases mutable receipt/root/pending");
            Symbols(pending.pending.symbols, 0, 2, 0, "modern pending symbols leaked");
            Symbols(pending.lastSpin.symbols, 0, 0, 2, "modern retained spin symbols leaked");
            var applied = GameLogic.ApplyPending(pending);
            Need(applied.coins == 10139 && applied.seed == 745495504u && applied.journey.Orders.Progress == 2 && applied.chapter.Skills.Receipts.Count == 6,
                "copy mutation changed second-spin29/seed/order outcome");
            Original(f, fullRaw, writes, 10110, 2647435461u, "pure pending-copy test");
            passed.Add("Modern Copy isolates pending and retained-spin arrays plus offline receipt; applying untouched pending yields10139/order2");

            var op = pending.pending.chapter;
            int[] constructorSymbols = op.Symbols;
            var reconstructed = Reconstruct(op, constructorSymbols, null);
            constructorSymbols[0] = 3;
            int[] getterSymbols = reconstructed.Symbols; getterSymbols[1] = 3;
            Symbols(reconstructed.Symbols, 0, 2, 0, "ChapterOperation retained constructor/getter array");
            Symbols(op.Symbols, 0, 2, 0, "shared original ChapterOperation symbols leaked");
            var settlement = JourneyEconomy.ReserveChapter(f.session.State, "settle", null, f.session.State.sequence + 1, 1120);
            string settlementRaw = Codec.Encode(settlement);
            var settlementOp = settlement.pending.chapter;
            var constructorReceipt = settlementOp.Settlement;
            var reconstructedSettlement = Reconstruct(settlementOp, null, constructorReceipt);
            constructorReceipt.coins = 999; constructorReceipt.to = 0;
            var getterReceipt = reconstructedSettlement.Settlement; getterReceipt.coins = 888; getterReceipt.from = 0;
            Need(reconstructedSettlement.Settlement.coins == 1 && reconstructedSettlement.Settlement.from == 1060 &&
                reconstructedSettlement.Settlement.to == 1120 && Codec.Encode(settlement) == settlementRaw,
                "ChapterOperation retained constructor/getter OfflineReceipt");
            Need(GameLogic.ApplyPending(settlement).coins == 10111 && GameLogic.ApplyPending(settlement).seed == 2647435461u,
                "receipt input mutation changed frozen1-coin settlement");
            Original(f, fullRaw, writes, 10110, 2647435461u, "frozen descriptor input/getter test");
            passed.Add("ChapterOperation Symbols and Settlement defensively capture inputs and return mutable copies; frozen settlement remains1coin");

            var snapshot = f.session.State;
            var projectInput = snapshot.chapter.Progress.CompletedUpgradeIds.ToList();
            var progress = new ChapterProgress(EraIds.WorkshopYard, projectInput); projectInput.Clear();
            Need(progress.CompletedUpgradeIds.Count == 24, "ChapterProgress retained caller list");
            var stagesInput = FamilyIds.All.Select(id => new FamilyStage(id, 5)).ToList();
            var stages = new FamilyStages(EraIds.WorkshopYard, stagesInput); stagesInput.Clear();
            Need(stages.Entries.Count == 6 && stages.Entries.All(s => s.AbsoluteLevel == 5), "FamilyStages retained caller list");
            var sources = snapshot.lastSpin.chapter.Effects.Modifiers.Sources;
            var sourceInput = sources.ToArray();
            var modifiers = new ChapterSymbolModifiers(sourceInput);
            sourceInput[0] = new SymbolCoinSource("test.caller-source", 0, 999);
            Need(modifiers.Sources.Count == 19 && modifiers.CoinsPerSymbol(0) == 6 && modifiers.CoinsPerSymbol(1) == 7 &&
                modifiers.CoinsPerSymbol(2) == 3 && modifiers.CoinsPerSymbol(3) == 3, "source constructor retained input array");
            ReadOnly(snapshot.chapter.Progress.CompletedUpgradeIds, "test.caller-project", "progress collection");
            ReadOnly(snapshot.chapter.Skills.Receipts, snapshot.chapter.Skills.Receipts[1], "skill receipt collection");
            ReadOnly(snapshot.chapter.Skills.Receipts[0].Request.Context.Stages.Entries, new FamilyStage(FamilyIds.House, 0), "skill context stages");
            ReadOnly(sources, sourceInput[0], "registered source collection");
            ReadOnly(snapshot.lastSpin.chapter.Effects.Contributions, new EffectContribution("test.caller-credit", 999), "effect contribution collection");
            ReadOnly(f.session.Chapter.Progress.CompletedUpgradeIds, "test.caller-project", "Chapter getter progress");
            Original(f, fullRaw, writes, 10110, 2647435461u, "immutable collection access");
            // Independently known seed1 second symbols0/2/0: base8+workshop1+potato12+trash3+recycler1+universal2+skillTrash2=29.
            var next = f.session.ExecuteChapterSpin(f.session.State.sequence + 1);
            Need(next.coins == 10139 && next.seed == 745495504u && next.chapter.Skills.Receipts.Count == 6 && next.journey.Orders.Progress == 2,
                "public getter/input mutation changed later actual store outcome");
            Symbols(next.lastSpin.symbols, 0, 2, 0, "later store symbols changed");
            passed.Add("Shared progression, ownership/context and19 source collections stay immutable; caller-list changes cannot alter next actual spin10139/order2");

            var plain = Late(false); string plainRaw = Codec.Encode(plain.session.State); int plainWrites = plain.storage.writes;
            var plainCopy = plain.session.State.Copy(); Damage(plainCopy);
            Original(plain, plainRaw, plainWrites, 10690, 1, "no-skills late copy");
            var plainSpin = plain.session.ExecuteChapterSpin(plain.session.State.sequence + 1);
            Need(plainSpin.coins == 10715 && plainSpin.seed == 2647435461u && plainSpin.chapter.Skills.Receipts.Count == 0 &&
                plainSpin.chapter.Progress.CompletedUpgradeIds.Count == 24 && plainSpin.chapter.SelectedProjectId == null,
                "no-skills late fixture gained hidden ownership or changed independent25-coin spin");
            passed.Add("Separate completed chapter without optional skills preserves10690 and yields10715/seed2647435461 with no hidden purchase");
            return passed;
        }
    }
}
