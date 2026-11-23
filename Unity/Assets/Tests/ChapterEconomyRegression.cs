using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class ChapterEconomyRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string primary, backup;
            public int writes, failAt;
            public bool committedFailure;
            public string ReadPrimary() { return primary; }
            public string ReadBackup() { return backup; }
            public void Write(string raw)
            {
                writes++;
                if (writes == failAt && !committedFailure) throw new IOException("Private chapter write failed before commit");
                backup = primary; primary = raw;
                if (writes == failAt) throw new IOException("Private chapter write committed before reporting failure");
            }
        }
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static void Need(bool value, string why) { if (!value) throw new Exception(why); }
        static void Rejects(Action action, string why)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            catch (ArgumentException) { return; }
            catch (FormatException) { return; }
            catch (OverflowException) { return; }
            throw new Exception(why);
        }
        static GameSession Open(Storage storage)
        {
            var session = GameSession.Open(new GameStore(storage, Codec), 91);
            Need(!session.RecoveryRequired, "Private chapter fixture failed to open: " + session.Error);
            return session;
        }
        static Storage Stored(ModelState state) { return new Storage { primary = Codec.Encode(state) }; }
        static ModelState Funded(long coins)
        {
            var state = ModelState.Fresh(1); state.coins = coins; return state;
        }
        static ModelState Repaired(long coins, int workshop = 0)
        {
            var state = Funded(coins); state.houseStage = 1; state.autoHintSeen = 1; state.workshopBuilt = workshop; return state;
        }
        static FamilyStages Stages(GameSession session) { return ChapterEconomy.Context(session.Chapter).Stages; }
        static void AllStages(GameSession session, string era, int absolute)
        {
            var stages = Stages(session);
            Need(stages.EraId == era && FamilyIds.All.All(f => stages.AbsoluteLevel(f) == absolute), "Incorrect six-family absolute stages");
        }
        static void BuyProject(GameSession session, string era, string family, int local)
        {
            var before = Stages(session); long sequence = session.State.sequence;
            string id = ChapterIds.Project(era, family, local);
            var after = session.SelectChapterProject(id, sequence + 1);
            var current = Stages(session);
            Need(after.sequence == sequence + 1 && after.pending == null && session.Chapter.SelectedProjectId == null,
                "Explicit project purchase selected a hidden successor");
            foreach (string item in FamilyIds.All)
                Need(current.AbsoluteLevel(item) == before.AbsoluteLevel(item) + (item == family ? 1 : 0), "One project purchase changed another family's stage");
        }
        static void BuildLevel(GameSession session, string era, int local, bool reverse = false)
        {
            var order = reverse ? FamilyIds.All.Reverse().ToArray() : FamilyIds.All.ToArray();
            if (era == EraIds.Junkyard && local == 1 && reverse)
                order = new[] { FamilyIds.House }.Concat(order.Where(f => f != FamilyIds.House)).ToArray();
            foreach (string family in order) BuyProject(session, era, family, local);
        }
        static void EnterAndAcknowledge(GameSession session)
        {
            long coins = session.State.coins; uint seed = session.State.seed;
            session.EnterWorkshopYard(session.State.sequence + 1);
            AllStages(session, EraIds.WorkshopYard, 3);
            Need(session.State.coins == coins && session.State.seed == seed && session.Chapter.TransitionPending, "Era entry changed wallet/RNG or lost presentation");
            session.AcknowledgeEraTransition(session.State.sequence + 1);
            Need(!session.Chapter.TransitionPending && session.State.coins == coins, "Era acknowledgement changed economy");
        }
        static string Change(string raw, string from, string to)
        {
            int start = raw.IndexOf(from, StringComparison.Ordinal);
            Need(start >= 0, "Missing malformed fixture fragment: " + from);
            return raw.Substring(0, start) + to + raw.Substring(start + from.Length);
        }
        static int ValueEnd(string raw, int start)
        {
            int depth = 0; bool quoted = false, escaped = false;
            for (int index = start; index < raw.Length; index++)
            {
                char c = raw[index];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { if (--depth == 0) return index + 1; }
            }
            throw new Exception("Malformed test object boundary");
        }
        static string RemoveObjectProperty(string raw, string key)
        {
            int name = raw.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            Need(name > 0 && raw[name - 1] == ',', "Missing removable test property");
            int value = name + key.Length + 3;
            Need(raw[value] == '{', "Expected object test property");
            return raw.Remove(name - 1, ValueEnd(raw, value) - name + 1);
        }
        static int RoundTrip(ModelState state)
        {
            string raw = Codec.Encode(state);
            Need(raw.Length < 65536 && Codec.Encode(Codec.Decode(raw)) == raw, "Actual chapter snapshot exceeds bounds or fails exact round-trip");
            return raw.Length;
        }
        static void Outcome(ModelState state, int a, int b, int c)
        {
            var symbols = state.lastSpin.symbols;
            Need(symbols.Length == 3 && symbols[0] == a && symbols[1] == b && symbols[2] == c, "Independent seeded outcome changed");
        }

        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            ModelState firstEraComplete = null;
            foreach (bool reverse in new[] { false, true })
            {
                var storage = Stored(Funded(10000)); var session = Open(storage);
                BuildLevel(session, EraIds.Junkyard, 1, reverse); BuildLevel(session, EraIds.Junkyard, 2, reverse);
                AllStages(session, EraIds.Junkyard, 2);
                Need(session.State.coins == 7030, "Independent Junkyard cost2970 changed");
                Need(!HouseGoal.Complete(session.State), "Junkyard incorrectly ends the two-era chapter");
                if (!reverse) firstEraComplete = session.State;
                string offered = storage.primary; int writes = storage.writes;
                Need(ChapterCatalog.Rules.CanUnlockWorkshopYard(session.Chapter.Progress), "Era offer unavailable after all12 projects");
                Need(storage.primary == offered && storage.writes == writes, "Inspecting/cancelling era offer wrote state");
                long entry = session.State.sequence + 1;
                session.EnterWorkshopYard(entry);
                session = Open(storage); AllStages(session, EraIds.WorkshopYard, 3);
                Need(session.Chapter.EntrySequence == entry && session.Chapter.TransitionPending && session.State.coins == 7030,
                    "Reload reset committed era or repeated an entry reward");
                writes = storage.writes;
                var gated = session;
                Rejects(() => gated.ExecuteChapterSpin(gated.State.sequence + 1), "Foreground spin bypassed pending transition");
                Rejects(() => gated.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard, FamilyIds.House, 1), gated.State.sequence + 1), "Project bypassed pending transition");
                Rejects(() => gated.BuySkill(ChapterEconomy.QuoteSkill(gated.State, SkillIds.Universal1).Request), "Skill bypassed pending transition");
                Need(storage.writes == writes && session.State.coins == 7030, "Rejected transition commands wrote state");
                session.AcknowledgeEraTransition(session.State.sequence + 1);
                session = Open(storage); Need(!session.Chapter.TransitionPending, "Acknowledged transition replayed after reload");
                BuildLevel(session, EraIds.WorkshopYard, 1, reverse); BuildLevel(session, EraIds.WorkshopYard, 2, reverse);
                AllStages(session, EraIds.WorkshopYard, 5);
                Need(session.State.coins == 690 && HouseGoal.Complete(session.State) && session.Chapter.Skills.Receipts.Count == 0,
                    "Independent24-project cost9310 or optional-skill exclusion changed");
                writes = storage.writes; session.EnterWorkshopYard(entry); session.AcknowledgeEraTransition(session.State.sequence + 1);
                Need(storage.writes == writes && session.State.coins == 690 && HouseGoal.Complete(session.State), "Era/ack replay mutated completed chapter");
            }
            passed.Add("Two build orders:24 explicit projects cost9310 (10000→690); offer cancel writes nothing, committed era reloads/gates untilACK, allsixabsolute5 without skills");

            var skillStorage = Stored(Funded(20000)); var skillsSession = Open(skillStorage);
            BuildLevel(skillsSession, EraIds.Junkyard, 1);
            string[] firstSkills = { SkillIds.Universal1, SkillIds.Recycling1, SkillIds.Delivery1 };
            string[] secondSkills = { SkillIds.Universal2, SkillIds.Recycling2, SkillIds.Delivery2 };
            long[] firstPrices = { 120, 160, 60 }, secondPrices = { 160, 90, 20 };
            for (int index = 0; index < firstSkills.Length; index++)
            {
                Need(ChapterEconomy.QuoteSkill(skillsSession.State, secondSkills[index]).Status == OfferStatus.Locked, "Tier2 unlocked in Junkyard");
                var quote = ChapterEconomy.QuoteSkill(skillsSession.State, firstSkills[index]);
                Need(quote.Status == OfferStatus.Available && quote.Node.Price == firstPrices[index], "Tier1 prerequisite/independent price changed");
                var before = Stages(skillsSession); long coins = skillsSession.State.coins;
                skillsSession.BuySkill(quote.Request);
                Need(skillsSession.State.coins == coins - firstPrices[index] && FamilyIds.All.All(f => before.AbsoluteLevel(f) == Stages(skillsSession).AbsoluteLevel(f)), "Skill bought a hidden project or charged wrong amount");
                Need(ChapterEconomy.QuoteSkill(skillsSession.State, secondSkills[index]).Status == OfferStatus.Locked, "Tier2 ignored era prerequisite");
            }
            BuildLevel(skillsSession, EraIds.Junkyard, 2); EnterAndAcknowledge(skillsSession);
            foreach (string node in secondSkills)
                Need(ChapterEconomy.QuoteSkill(skillsSession.State, node).Status == OfferStatus.Locked, "Tier2 ignored era-local family upgrade");
            BuildLevel(skillsSession, EraIds.WorkshopYard, 1);
            for (int index = 0; index < secondSkills.Length; index++)
            {
                var quote = ChapterEconomy.QuoteSkill(skillsSession.State, secondSkills[index]);
                Need(quote.Status == OfferStatus.Available && quote.Node.Price == secondPrices[index], "Tier2 prerequisite/independent price changed");
                skillsSession.BuySkill(quote.Request);
            }
            BuildLevel(skillsSession, EraIds.WorkshopYard, 2);
            ModelState fullSkills = skillsSession.State;
            Need(fullSkills.coins == 10080 && skillsSession.Chapter.Skills.Receipts.Count == 6 && HouseGoal.Complete(fullSkills), "Independent projects9310+skills610 or canonical receipts changed");
            foreach (string node in firstSkills.Concat(secondSkills))
                Need(ChapterEconomy.QuoteSkill(fullSkills, node).Status == OfferStatus.Maxed && skillsSession.Chapter.Skills.Owns(node), "Purchased skill does not inspect as owned/maxed");
            passed.Add("Six skills obey branch/era/local-tier prerequisites, cost610 without hidden construction (20000−9310−610=10080), and inspect as owned");

            var effectStorage = Stored(fullSkills); var effectSession = Open(effectStorage);
            var credit = effectSession.ExecuteChapterSpin(effectSession.State.sequence + 1); Outcome(credit, 0, 0, 2);
            var effects = credit.lastSpin.chapter.Effects;
            Need(credit.coins == 10109 && credit.lastSpin.reward == 8 && credit.lastSpin.bonus == 1 && effects.TotalCoins == 20 &&
                effects.UniversalCoins == 2 && effects.RecyclingSkillCoins == 2 && effects.DeliverySkillCoins == 0 &&
                effects.PotatoCoins == 12 && effects.BoxCoins == 0 && effects.TrashCoins == 3 && effects.PigeonCoins == 0 &&
                effects.Baseline.RecyclerCoins == 1 && effects.Modifiers.Sources.Count == 19 && effects.Contributions.Count == 9,
                "Fully owned seed1 base8+workshop1+effects20 must equal29 without double-counting migrated slot/baselines");
            int committedLength = RoundTrip(credit);
            var maximumPending = ChapterEconomy.Reserve(credit, "spin", null, credit.sequence + 1);
            int pendingLength = RoundTrip(maximumPending);
            Need(Codec.Decode(Codec.Encode(maximumPending)).chapter.Skills.Receipts.Count == 6, "Six skill receipts lost through bounded JSON-string payload");
            var pigeonFixture = fullSkills.Copy(); pigeonFixture.seed = 5;
            var pigeonStorage = Stored(pigeonFixture); var pigeonSession = Open(pigeonStorage);
            pigeonSession.ExecuteChapterSpin(pigeonSession.State.sequence + 1);
            Need(WaveProgress.Of(pigeonSession.State).Delivery.PigeonProgress == 1, "First seed5 pigeon did not persist carry1");
            pigeonSession = Open(pigeonStorage); pigeonSession.ExecuteChapterSpin(pigeonSession.State.sequence + 1);
            Need(WaveProgress.Of(pigeonSession.State).Delivery.PigeonProgress == 2, "Second seed5 pigeon did not persist carry2");
            long beforeDelivery = pigeonSession.State.coins;
            credit = pigeonSession.ExecuteChapterSpin(pigeonSession.State.sequence + 1); Outcome(credit, 3, 1, 0);
            Need(credit.coins - beforeDelivery == 27 && credit.lastSpin.chapter.Effects.Baseline.DeliveryCoins == 3 &&
                credit.lastSpin.chapter.Effects.DeliverySkillCoins == 2 && WaveProgress.Of(credit).Delivery.PigeonProgress == 0,
                "One completed delivery must grant baseline3+skill2 once, using the single shared carry");
            passed.Add("Source19/receipt6/contribution9 snapshots round-trip within65536 chars (committed" + committedLength + ",pending" + pendingLength + "); seed1 grants29, seed5 delivery grants3+skill2 with shared carry");

            var oldStorage = Stored(Repaired(1000)); var oldSession = Open(oldStorage);
            oldSession.BuySlotUpgrade(1);
            Need(oldSession.State.chapter == null && oldSession.State.coins == 880, "Private legacy API fixture was not literal format1");
            int oldWrites = oldStorage.writes; string oldRaw = oldStorage.primary;
            var projected = oldSession.Chapter;
            Need(oldStorage.writes == oldWrites && oldStorage.primary == oldRaw && projected.Skills.Receipts.Count == 1 &&
                projected.Skills.Receipts[0].Legacy && projected.Skills.Receipts[0].Request.Context == null,
                "Reading legacy boost invented context or eagerly wrote migration");
            oldSession.SelectChapterProject(GameIds.WorkshopBuild, 2);
            Need(oldSession.State.coins == 760 && oldSession.Chapter.Skills.Receipts[0].Request.Sequence == 1 &&
                oldSession.Chapter.Skills.Receipts[0].Request.ExpectedWallet == 1000 && oldSession.Chapter.Skills.Receipts[0].LegacyHouseStage == 1,
                "Migrating boost recharged/refunded it or changed known purchase receipt");
            oldSession.ExecuteChapterSpin(3);
            Need(oldSession.State.coins == 770 && oldSession.State.lastSpin.chapter.Effects.UniversalCoins == 1 && oldSession.State.lastSpin.chapter.Effects.TotalCoins == 1,
                "Migrated boost doubled universal/legacy slot income");
            oldSession = Open(oldStorage); oldWrites = oldStorage.writes;
            oldSession.BuySlotUpgrade(new UpgradePurchaseRequest(GameIds.SlotCoinBoost, 1, 0, 1000, 1));
            Need(oldStorage.writes == oldWrites && oldSession.State.coins == 770, "Known legacy purchase replay debited again");
            var legacyReplaySession = oldSession;
            Rejects(() => legacyReplaySession.BuySlotUpgrade(new UpgradePurchaseRequest(GameIds.SlotCoinBoost, 1, 0, 999, 1)), "Conflicting legacy receipt replay accepted");
            passed.Add("Public r7 boost maps once without eager write/debit/refund or fabricated context; original receipt/replay retained and new universal income stays+1");

            var selectedFixture = Repaired(475, 1); selectedFixture.lastSettledAt = 1000;
            var selectedStorage = Stored(selectedFixture); var selectedSession = Open(selectedStorage);
            selectedSession.SelectChapterProject(GameIds.HouseImprove, 1);
            selectedSession.SettleOffline(1600);
            Need(selectedSession.State.coins == 485 && selectedSession.Chapter.SelectedProjectId == GameIds.HouseImprove && selectedSession.State.houseStage == 1,
                "Offline settlement cleared selection or bought an unrequested project");
            selectedSession.BuySkill(ChapterEconomy.QuoteSkill(selectedSession.State, SkillIds.Universal1).Request);
            selectedSession = Open(selectedStorage);
            Need(selectedSession.State.coins == 365 && selectedSession.Chapter.SelectedProjectId == GameIds.HouseImprove &&
                selectedSession.State.selectedStage == 2 && selectedSession.State.lastSettlement.coins == 10 && selectedSession.State.houseStage == 1,
                "Skill/reload lost selected project or settlement receipt");
            passed.Add("Offline1000→1600 funds the selected house480 yet preserves it; skill120/reload retain selection and receipt (475→485→365), without hidden purchase");

            var acquisitionStorage = Stored(Repaired(189)); var acquisition = Open(acquisitionStorage);
            string power = ChapterIds.Project(EraIds.Junkyard, FamilyIds.PowerSource, 1);
            acquisition.SelectChapterProject(power, 1); credit = acquisition.ExecuteChapterSpin(2); Outcome(credit, 0, 0, 2);
            Need(credit.coins == 7 && Stages(acquisition).AbsoluteLevel(FamilyIds.PowerSource) == 1 && acquisition.Chapter.SelectedProjectId == null &&
                credit.lastSpin.chapter.Effects.Modifiers.Sources.Count == 0 && credit.lastSpin.chapter.Effects.PotatoCoins == 0,
                "Power acquisition spin received its own new potato bonus");
            acquisition = Open(acquisitionStorage); credit = acquisition.ExecuteChapterSpin(3); Outcome(credit, 0, 2, 0);
            Need(credit.coins == 17 && credit.lastSpin.reward == 8 && credit.lastSpin.chapter.Effects.PotatoCoins == 2,
                "Owned power benefit did not start on the following spin");
            passed.Add("Explicit power190 acquisition:189+pair8−190=7 with no retroactive potatoes; next[0,2,0] givesbase8+potatoes2=17 after reload");

            var simpleStorage = Stored(Repaired(1000)); var simpleSession = Open(simpleStorage);
            simpleSession.SelectChapterProject(GameIds.WorkshopBuild, 1);
            var simple = simpleSession.State;
            var transitionStorage = Stored(firstEraComplete); var transitionSession = Open(transitionStorage);
            transitionSession.EnterWorkshopYard(transitionSession.State.sequence + 1);
            var entered = transitionSession.State;
            for (int operation = 0; operation < 5; operation++)
            {
                var baseline = operation == 0 ? fullSkills : operation <= 2 ? simple : operation == 3 ? firstEraComplete : entered;
                long sequence = baseline.sequence + 1;
                var request = operation == 2 ? ChapterEconomy.QuoteSkill(baseline, SkillIds.Universal1).Request : null;
                Action<GameSession> invoke = game => {
                    if (operation == 0) game.ExecuteChapterSpin(sequence);
                    else if (operation == 1) game.SelectChapterProject(GameIds.RecyclerBuild, sequence);
                    else if (operation == 2) game.BuySkill(request);
                    else if (operation == 3) game.EnterWorkshopYard(sequence);
                    else game.AcknowledgeEraTransition(sequence);
                };
                foreach (bool committed in new[] { false, true }) foreach (int failedWrite in new[] { 1, 2 })
                {
                    var failureStorage = Stored(baseline); failureStorage.failAt = failedWrite; failureStorage.committedFailure = committed;
                    var failureSession = Open(failureStorage);
                    try { invoke(failureSession); throw new Exception("Injected chapter write failure was not observed"); }
                    catch (IOException) { }
                    Need(failureSession.RecoveryRequired, "Uncertain chapter operation did not block commands");
                    var blocked = failureSession;
                    Rejects(() => blocked.ExecuteChapterSpin(sequence), "Ambiguous chapter operation allowed a foreground spin");
                    failureStorage.failAt = 0; failureSession = Open(failureStorage); invoke(failureSession);
                    long expectedCoins = operation == 0 ? 10109 : operation == 1 ? 700 : operation == 2 ? 760 : 7030;
                    Need(failureSession.State.sequence == sequence && failureSession.State.pending == null && failureSession.State.coins == expectedCoins,
                        "Interrupted spin/project/skill/entry/ACK lost or duplicated its atomic delta");
                    if (operation == 0) Need(failureSession.State.lastSpin.chapter.Effects.Modifiers.Sources.Count == 19, "Recovered spin lost frozen sources");
                    if (operation == 1) Need(Stages(failureSession).AbsoluteLevel(FamilyIds.Recycler) == 1, "Recovered project not built once");
                    if (operation == 2) Need(failureSession.Chapter.Skills.Receipts.Count == 1, "Recovered skill receipt duplicated");
                    if (operation >= 3) Need(failureSession.Chapter.TransitionPending == (operation == 3), "Recovered transition marker changed");
                    failureSession = Open(failureStorage); int writes = failureStorage.writes; string snapshot = failureStorage.primary; invoke(failureSession);
                    Need(failureStorage.writes == writes && failureStorage.primary == snapshot && failureSession.State.coins == expectedCoins,
                        "Recovered chapter command replay wrote or mutated again");
                }
            }
            passed.Add("20 spin/project/skill/era-entry/ACK before/after reserve/applied-write failure cases block uncertainty and recover exactly once");

            var staleStorage = Stored(simple); var staleSession = Open(staleStorage);
            var staleQuote = ChapterEconomy.QuoteSkill(staleSession.State, SkillIds.Universal1).Request;
            staleSession.SelectChapterProject(GameIds.RecyclerBuild, staleSession.State.sequence + 1);
            int staleWrites = staleStorage.writes; string staleRaw = staleStorage.primary;
            Rejects(() => staleSession.BuySkill(staleQuote), "Stale global-sequence/wallet skill quote accepted");
            var validQuote = ChapterEconomy.QuoteSkill(staleSession.State, SkillIds.Universal1).Request;
            var wrongWallet = new SkillPurchaseRequest(validQuote.NodeId, validQuote.OperationId, validQuote.Sequence, validQuote.ExpectedWallet + 1, validQuote.ExpectedOwnership, validQuote.Context,validQuote.PricingRevision);
            Rejects(() => staleSession.BuySkill(wrongWallet), "Wrong-wallet skill quote accepted");
            var wrongContext = new SkillPurchaseRequest(validQuote.NodeId, validQuote.OperationId, validQuote.Sequence, validQuote.ExpectedWallet, validQuote.ExpectedOwnership,
                new SkillContext(validQuote.Context.Stages, GameIds.HouseImprove),validQuote.PricingRevision);
            Rejects(() => staleSession.BuySkill(wrongContext), "Wrong complete-context skill quote accepted");
            Rejects(() => staleSession.BuySkill(null), "Null skill quote accepted");
            Need(staleStorage.writes == staleWrites && staleStorage.primary == staleRaw, "Rejected skill requests changed the complete snapshot");
            staleSession.BuySkill(validQuote); staleSession = Open(staleStorage); staleWrites = staleStorage.writes;
            staleSession.BuySkill(validQuote);
            Need(staleStorage.writes == staleWrites, "Exact skill request replay wrote again");
            var adapterStorage=Stored(simple);var adapter=Open(adapterStorage);var adapterBefore=adapter.State;
            var adapterRequest=new UpgradePurchaseRequest(GameIds.SlotCoinBoost,adapterBefore.sequence+1,0,adapterBefore.coins,adapterBefore.houseStage);
            foreach(var bad in new[]{
                new UpgradePurchaseRequest("unknown.offer",adapterRequest.Sequence,0,adapterRequest.ExpectedWallet,adapterRequest.ExpectedHouseStage),
                new UpgradePurchaseRequest(null,adapterRequest.Sequence,0,adapterRequest.ExpectedWallet,adapterRequest.ExpectedHouseStage),
                new UpgradePurchaseRequest(GameIds.SlotCoinBoost,adapterRequest.Sequence,1,adapterRequest.ExpectedWallet,adapterRequest.ExpectedHouseStage),
                new UpgradePurchaseRequest(GameIds.SlotCoinBoost,adapterRequest.Sequence,0,adapterRequest.ExpectedWallet+1,adapterRequest.ExpectedHouseStage),
                new UpgradePurchaseRequest(GameIds.SlotCoinBoost,adapterRequest.Sequence,0,adapterRequest.ExpectedWallet,2),
                new UpgradePurchaseRequest(GameIds.SlotCoinBoost,adapterRequest.Sequence+1,0,adapterRequest.ExpectedWallet,adapterRequest.ExpectedHouseStage),null})
                Rejects(()=>adapter.BuySlotUpgrade(bad),"Modern legacy adapter canonicalized an invalid request");
            Need(adapterStorage.writes==0&&Codec.Encode(adapter.State)==Codec.Encode(adapterBefore),"Rejected legacy adapter request changed state/storage");
            Rejects(()=>adapter.BuySlotUpgrade(adapterRequest),"Fresh legacy price quote was retagged as current");
            Need(adapterStorage.writes==0&&Codec.Encode(adapter.State)==Codec.Encode(adapterBefore),"Stale legacy price quote changed state");
            adapter.BuySkill(ChapterEconomy.QuoteSkill(adapter.State,SkillIds.Universal1).Request);Need(adapter.State.coins==adapterBefore.coins-120,"Current canonical first skill changed price");
            adapter=Open(adapterStorage);int adapterWrites=adapterStorage.writes;string adapterRaw=adapterStorage.primary;
            adapter.BuySlotUpgrade(adapterRequest);
            Rejects(()=>adapter.BuySlotUpgrade(new UpgradePurchaseRequest("unknown.offer",adapterRequest.Sequence,0,adapterRequest.ExpectedWallet,adapterRequest.ExpectedHouseStage)),"Wrong identity replay accepted");
            Need(adapterStorage.writes==adapterWrites&&adapterStorage.primary==adapterRaw,"Legacy adapter replay wrote/debited again");
            passed.Add("Stale sequence/wallet/full-context/null skill quotes leave the complete snapshot unchanged; exact committed request replay writes nothing");

            string pendingRaw = Codec.Encode(ChapterEconomy.Reserve(simple, "spin", null, simple.sequence + 1));
            Rejects(() => Codec.Decode(RemoveObjectProperty(pendingRaw, "chapter")), "Missing modern pending descriptor fell back to legacy");
            Rejects(() => Codec.Decode(Change(pendingRaw, "\"selected\":null,", "")), "Missing modern selector field accepted");
            Rejects(() => Codec.Decode(Change(pendingRaw, "\"format\":2", "\"format\":3")), "Unknown modern format accepted");
            string richPending = Codec.Encode(ChapterEconomy.Reserve(fullSkills, "spin", null, fullSkills.sequence + 1));
            Rejects(() => Codec.Decode(Change(richPending, "\"coinsAfter\":10109", "\"coinsAfter\":10110")), "Wrong frozen wallet delta accepted");
            string registered = ChapterCatalog.Rules.SymbolModifiers(fullSkills.chapter.Progress).Sources[0].SourceId;
            Rejects(() => Codec.Decode(Change(richPending, "\"id\":\"" + registered + "\"", "\"id\":\"unregistered.symbol-coins\"")), "Unregistered positive frozen source accepted");
            Rejects(() => Codec.Decode(Change(richPending, "\"houseStage\":2", "\"houseStage\":1")), "Conflicting legacy projection accepted");
            Rejects(() => Codec.Decode(Change(richPending, "\"base\":8", "\"base\":9")), "Wrong exclusive frozen base accepted");
            string fullRaw = Codec.Encode(fullSkills);
            Rejects(() => Codec.Decode(Change(fullRaw, "\"chapter\":{", "\"chapter\":null,\"unexpected\":{")), "Malformed null/unknown modern aggregate accepted");
            var historicalStorage=Stored(Funded(1000));var historical=Open(historicalStorage);
            historical.ExecuteSpin(1); // Literal pre-chapter receipt, then migrate without a new spin.
            historical.SelectChapterProject(GameIds.HouseRepair,2);
            foreach(int malformed in new[]{0,1,2,3,4})
            {
                var invalid=historical.State;
                if(malformed==0)invalid.lastSpin.sequence=0;
                if(malformed==1)invalid.lastSpin.sequence=invalid.sequence+1;
                if(malformed==2)invalid.lastSpin.symbols=new[]{0,0,9};
                if(malformed==3)invalid.lastSpin.reward++;
                if(malformed==4)invalid.lastSpin.bonus=2;
                Rejects(()=>GameLogic.Validate(invalid),"Migration weakened literal old spin receipt validation");
            }
            passed.Add("Strict modern codec rejects missing descriptor/selector, unknown format, wrong frozen wallet/base/source, legacy projection conflict and malformed aggregate");
            return passed.AsReadOnly();
        }
    }
}
