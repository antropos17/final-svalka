using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;

namespace JunkToNeon.Tests
{
    public static class JourneyEconomyRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string primary, backup;
            public int writes, failAt;
            public bool commitBeforeFailure;
            public string ReadPrimary() { return primary; }
            public string ReadBackup() { return backup; }
            public void Write(string raw)
            {
                writes++;
                if (writes == failAt && !commitBeforeFailure) throw new IOException("Private journey write failed before commit");
                backup = primary; primary = raw;
                if (writes == failAt) throw new IOException("Private journey write committed before reporting failure");
            }
        }
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static readonly int[] TrashProgress = { 1, 2, 2, 4, 5, 5, 6, 6, 7, 8, 8, 9, 9, 10, 11, 12 };
        static readonly int[] DeliveryProgress = { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3 };
        static readonly int[][] SeedOne = {
            new[] {0,0,2},new[] {0,2,0},new[] {0,0,1},new[] {2,2,1},new[] {2,0,0},new[] {3,0,3},
            new[] {0,3,2},new[] {3,1,3},new[] {0,1,2},new[] {0,0,2},new[] {0,3,0},new[] {2,1,0},
            new[] {3,1,0},new[] {1,1,2},new[] {1,2,0},new[] {2,3,2},new[] {2,2,1},new[] {3,0,3} };
        static void Need(bool condition, string why) { if (!condition) throw new Exception(why); }
        static void Rejects(Action action, string why)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            catch (ArgumentException) { return; }
            catch (FormatException) { return; }
            catch (OverflowException) { return; }
            throw new Exception(why);
        }
        static GameSession Open(Storage storage, bool initialize = false)
        {
            var session = GameSession.Open(new GameStore(storage, Codec), 1, initializeJourney: initialize);
            Need(!session.RecoveryRequired, "Private journey fixture failed to open: " + session.Error);
            return session;
        }
        static Storage Stored(ModelState state) { return new Storage { primary = Codec.Encode(state) }; }
        static ModelState Funded(long coins)
        { var state = ModelState.Fresh(1); state.coins = coins; return state; }
        static void BuildEra(GameSession session, string era)
        {
            for (int level = 1; level <= 2; level++) foreach (string family in FamilyIds.All)
                session.SelectChapterProject(ChapterIds.Project(era, family, level), session.State.sequence + 1);
        }
        static ModelState Completed()
        {
            var storage = Stored(Funded(10000)); var session = Open(storage);
            BuildEra(session, EraIds.Junkyard); session.EnterWorkshopYard(session.State.sequence + 1);
            session.AcknowledgeEraTransition(session.State.sequence + 1); BuildEra(session, EraIds.WorkshopYard);
            Need(session.State.coins == 690 && HouseGoal.Complete(session.State) && session.Chapter.Skills.Receipts.Count == 0, "Private no-skills chapter cost9310 changed");
            return session.State;
        }
        static void Spin(GameSession session, int traceIndex, bool manual = true)
        {
            session.ExecuteChapterSpin(session.State.sequence + 1, manualSpin: manual);
            Need(session.State.lastSpin.symbols.SequenceEqual(SeedOne[traceIndex]), "Independent seed1 foreground trace changed");
        }
        static void Refuse(Storage storage, GameSession session, Action action, string why)
        {
            string raw = storage.primary; int writes = storage.writes;
            Rejects(action, why);
            Need(!session.RecoveryRequired && storage.writes == writes && storage.primary == raw, "Refused operation wrote state or blocked a healthy session: " + why);
        }
        static string Fixture(string relative)
        {
            foreach (string root in new[] { Directory.GetCurrentDirectory(), Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")) })
            {
                string path = Path.Combine(root, relative);
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new Exception("Immutable repository fixture not found: " + relative);
        }
        static int ValueEnd(string raw, int start)
        {
            bool quoted = false, escaped = false; int depth = 0;
            for (int index = start; index < raw.Length; index++)
            {
                char c = raw[index];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') { quoted = false; if (depth == 0) return index + 1; }
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { if (--depth == 0) return index + 1; }
            }
            throw new Exception("Malformed test value boundary");
        }
        static string RemoveObject(string raw, string key, int from = 0, bool last = false)
        {
            string field = "\"" + key + "\":";
            int start = last ? raw.LastIndexOf(field, StringComparison.Ordinal) : raw.IndexOf(field, from, StringComparison.Ordinal);
            Need(start > 0 && raw[start - 1] == ',' && raw[start + field.Length] == '{', "Expected removable fixture object");
            return raw.Remove(start - 1, ValueEnd(raw, start + field.Length) - start + 1);
        }
        static string Change(string raw, string from, string to, bool last = false)
        {
            int index = last ? raw.LastIndexOf(from, StringComparison.Ordinal) : raw.IndexOf(from, StringComparison.Ordinal);
            Need(index >= 0, "Missing malformed fixture fragment: " + from);
            return raw.Substring(0, index) + to + raw.Substring(index + from.Length);
        }

        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>(); var complete = Completed();
            var trashStorage = Stored(complete); var trash = Open(trashStorage);
            long firstInstance = trash.State.sequence + 1; trash.AcceptOrder(OrderIds.TrashSort, firstInstance);
            Need(trash.Journey.Orders.Progress == 0 && trash.State.coins == 690, "Acceptance counted prior events or charged coins");
            for (int index = 0; index < TrashProgress.Length; index++)
            {
                Spin(trash, index);
                Need(trash.Journey.Orders.Progress == TrashProgress[index] && trash.Journey.Orders.Cursor == trash.State.sequence &&
                    trash.Journey.Orders.LastEvent.OperationSequence == trash.State.lastSpin.sequence && trash.State.lastSpin.journey.Sequence == trash.State.lastSpin.chapter.Sequence,
                    "Order progress does not share the exact frozen foreground spin");
            }
            Need(trash.Journey.Orders.Ready && trash.State.coins == 1062 && trash.Journey.Orders.LatestClaim == null,
                "Trash12 should cap without automatic120 reward (690+372=1062)");
            var readyTrash = trash.State;
            var beforeClaim = trash.State;
            long claimSequence = trash.State.sequence + 1; trash.ClaimOrder(OrderIds.TrashSort, firstInstance, claimSequence);
            Need(trash.State.coins == 1182 && trash.Journey.Orders.ActiveOrderId == null && trash.Journey.Orders.LatestClaim.RewardCoins == 120,
                "Explicit trash claim did not grant120 once");
            Need(ChapterEconomy.Same(beforeClaim.chapter, trash.State.chapter) && beforeClaim.seed == trash.State.seed &&
                beforeClaim.lastSpin.sequence == trash.State.lastSpin.sequence && ChapterEconomy.SameDelivery(WaveProgress.Of(beforeClaim).Delivery, WaveProgress.Of(trash.State).Delivery),
                "Claim drew reels, advanced carry, changed ownership/selection or bought a hidden project");
            long repeatedInstance = trash.State.sequence + 1; trash.AcceptOrder(OrderIds.TrashSort, repeatedInstance);
            Need(repeatedInstance != firstInstance && trash.Journey.Orders.Progress == 0 && trash.State.coins == 1182, "Repeat did not create a clean explicit instance");
            Spin(trash, 16); Need(trash.State.coins == 1206 && trash.Journey.Orders.Progress == 2, "New instance did not observe only its future event");
            trash = Open(trashStorage); int writes = trashStorage.writes; string snapshot = trashStorage.primary;
            trash.ClaimOrder(OrderIds.TrashSort, firstInstance, claimSequence);
            Need(trashStorage.writes == writes && trashStorage.primary == snapshot && trash.State.coins == 1206 && trash.Journey.Orders.InstanceSequence == repeatedInstance && trash.Journey.Orders.Progress == 2,
                "Exact old claim replay replaced the live wallet/new active instance");
            passed.Add("Trash12 caps on seed1 spin16 at1062 without auto reward; explicit120 claim, repeat instance and old-claim replay preserve newer wallet1206/progress2");

            var pigeonStorage = Stored(complete); var pigeon = Open(pigeonStorage);
            long pigeonInstance = pigeon.State.sequence + 1; pigeon.AcceptOrder(OrderIds.PigeonDeliveries, pigeonInstance);
            for (int index = 0; index < DeliveryProgress.Length; index++)
            {
                Spin(pigeon, index, manual: false);
                Need(pigeon.Journey.Orders.Progress == DeliveryProgress[index], "Pigeon order counted appearances/coin bonus instead of frozen completed deliveries");
                Need(pigeon.Journey.Orders.LastEvent.CompletedDeliveries == pigeon.State.lastSpin.chapter.Effects.Baseline.Deliveries, "Order delivery count diverged from the same frozen effect");
            }
            Need(pigeon.State.coins == 1110 && pigeon.Journey.Orders.Ready && pigeon.Journey.Orders.LatestClaim == null, "Delivery3 auto-paid or wrong independent spin wallet690+420");
            pigeon.ClaimOrder(OrderIds.PigeonDeliveries, pigeonInstance, pigeon.State.sequence + 1);
            Need(pigeon.State.coins == 1290 && pigeon.Journey.Orders.LatestClaim.RewardCoins == 180 && pigeon.Journey.Orders.ActiveOrderId == null,
                "Explicit delivery3 reward must be180 once");
            passed.Add("Pigeon order counts completed deliveries at seed1 spins7/11/18 (not appearances);1110 ready wallet becomes1290 only after explicit180 claim");

            var lockedStorage = Stored(Funded(10000)); var locked = Open(lockedStorage);
            Refuse(lockedStorage, locked, () => locked.AcceptOrder(OrderIds.TrashSort, 1), "Order unlocked before allsixabsolute5");
            var passiveStorage = Stored(complete); var passive = Open(passiveStorage);
            Spin(passive, 0); passive.AcceptOrder(OrderIds.TrashSort, passive.State.sequence + 1);
            Need(passive.Journey.Orders.Progress == 0, "Acceptance retroactively counted a pre-accept trash");
            long passiveInstance = passive.Journey.Orders.InstanceSequence;
            Refuse(passiveStorage, passive, () => passive.AcceptOrder(OrderIds.TrashSort, passive.State.sequence + 1), "Reselecting active order reset it");
            Refuse(passiveStorage, passive, () => passive.AcceptOrder(OrderIds.PigeonDeliveries, passive.State.sequence + 1), "Second active order silently replaced the first");
            Refuse(passiveStorage, passive, () => passive.ClaimOrder(OrderIds.TrashSort, passiveInstance, passive.State.sequence + 1), "Unready order could be claimed");
            Refuse(passiveStorage, passive, () => passive.CancelOrder(OrderIds.TrashSort, passiveInstance + 1, passive.State.sequence + 1), "Conflicting order instance canceled current progress");
            long cursor = passive.Journey.Orders.Cursor;
            passive.Checkpoint(1000); passive.SettleOffline(1600);
            Need(passive.Journey.Orders.Progress == 0 && passive.Journey.Orders.Cursor == cursor && passive.State.lastSettlement.coins == 10, "Clock/offline synthesized order events");
            var skill = ChapterEconomy.QuoteSkill(passive.State, SkillIds.Universal1); long skillWallet = passive.State.coins;
            passive.BuySkill(skill.Request);
            Need(passive.State.coins == skillWallet - skill.Node.Price && passive.Journey.Orders.Progress == 0 && passive.Journey.Orders.Cursor == cursor,
                "Skill purchase synthesized order events");
            passive.AcknowledgeGuidance(passive.Guidance.Suggestion.HintId, passive.State.sequence + 1);
            Need(passive.Journey.Orders.Cursor == cursor && passive.Journey.Orders.Progress == 0, "Guide acknowledgement synthesized order events");
            Spin(passive, 1); Need(passive.Journey.Orders.Progress == 1, "First post-accept foreground trash was lost across non-spin sequence gaps");
            writes = passiveStorage.writes; snapshot = passiveStorage.primary; long duplicateSpin = passive.State.sequence;
            passive.ExecuteChapterSpin(duplicateSpin);
            Need(passiveStorage.writes == writes && passiveStorage.primary == snapshot, "Duplicate foreground command progressed order twice");
            Refuse(trashStorage, trash, () => trash.ClaimOrder(OrderIds.PigeonDeliveries, firstInstance, claimSequence), "Conflicting old claim identity accepted");
            Refuse(trashStorage, trash, () => trash.ClaimOrder(OrderIds.TrashSort, firstInstance, claimSequence + 1), "Old instance paid through a different claim sequence");
            passed.Add("Orders stay locked/one-active; no retroactive, clock, offline, skill or ACK progress; stale instances/refusals and duplicate spins cause no IO or healthy-session block");

            trash.CancelOrder(OrderIds.TrashSort, repeatedInstance, trash.State.sequence + 1);
            Need(trash.State.coins == 1206 && trash.Journey.Orders.ActiveOrderId == null && trash.Journey.Orders.Progress == 0 &&
                trash.Journey.Orders.LatestClaim.ClaimSequence == claimSequence, "Partial cancellation paid/refunded progress or erased latest claim identity");
            var cancelStorage = Stored(readyTrash); var cancel = Open(cancelStorage);
            cancel.CancelOrder(OrderIds.TrashSort, firstInstance, cancel.State.sequence + 1);
            Need(cancel.State.coins == 1062 && cancel.Journey.Orders.ActiveOrderId == null && cancel.Journey.Orders.Progress == 0 && cancel.Journey.Orders.LatestClaim == null,
                "Canceling ready order did not forfeit its120 reward");
            Refuse(cancelStorage, cancel, () => cancel.ClaimOrder(OrderIds.TrashSort, firstInstance, cancel.State.sequence + 1), "Canceled instance could be claimed");
            cancel.AcceptOrder(OrderIds.PigeonDeliveries, cancel.State.sequence + 1); Need(cancel.Journey.Orders.Progress == 0, "Cancellation reused prior order progress");
            var overflowFixture = readyTrash.Copy(); overflowFixture.coins = long.MaxValue - 119;
            var overflowStorage = Stored(overflowFixture); var overflow = Open(overflowStorage); string overflowRaw = overflowStorage.primary;
            try { overflow.ClaimOrder(OrderIds.TrashSort, firstInstance, overflow.State.sequence + 1); throw new Exception("Overflowing120 claim accepted"); }
            catch (OverflowException) { }
            Need(!overflow.RecoveryRequired && overflowStorage.writes == 0 && overflowStorage.primary == overflowRaw, "Claim overflow reached IO or blocked a healthy session");
            overflow.CancelOrder(OrderIds.TrashSort, firstInstance, overflow.State.sequence + 1);
            Need(overflow.State.coins == long.MaxValue - 119 && overflow.Journey.Orders.ActiveOrderId == null, "Overflow trapped a ready order instead of allowing explicit forfeiture");
            passed.Add("Ready cancellation forfeits120 and resets future progress; overflowing claim refuses beforeIO without blocking, and ready cancellation still works");

            var freshStorage = new Storage(); var fresh = Open(freshStorage, initialize: true);
            var freshBaseline = fresh.State;
            Need(fresh.State.coins == 37 && freshStorage.writes == 1 && fresh.Journey.Guidance.Origin == GuideOrigin.Fresh && fresh.Guidance.Suggestion.HintId == GuideIds.Welcome && !fresh.Guidance.Suggestion.BlocksActions,
                "Actual fresh creation did not establish nonblocking factual Fresh origin/one initial grant");
            Refuse(freshStorage, fresh, () => fresh.ExecuteChapterSpin(1, manualSpin: false, tutorialBits: (int)TutorialSteps.VisibleImprovement), "Visible improvement invented before any improvement");
            Refuse(freshStorage, fresh, () => fresh.ExecuteChapterSpin(1, manualSpin: false, tutorialBits: (int)TutorialSteps.ManualSpin), "Manual tutorial step invented for nonmanual spin");
            fresh.AcknowledgeGuidance(GuideIds.Welcome, 1);
            fresh.SelectChapterProject(GameIds.HouseRepair, 2, tutorialBits: (int)TutorialSteps.Previewed);
            long freshWallet = fresh.State.coins; uint freshSeed = fresh.State.seed;
            fresh.AcknowledgeGuidance(GuideIds.ChooseProject, 3);
            Need(fresh.State.coins == freshWallet && fresh.State.seed == freshSeed && fresh.Chapter.SelectedProjectId == GameIds.HouseRepair && fresh.State.houseStage == 0,
                "Guide ACK changed wallet/RNG or selected project");
            fresh.ExecuteChapterSpin(4, manualSpin: true);
            Need(fresh.State.coins == 5 && fresh.Journey.Guidance.LastCompletion.ProjectId == GameIds.HouseRepair && fresh.Journey.Guidance.LastCompletion.Sequence == 4 &&
                (fresh.Journey.Guidance.TutorialBits & (int)TutorialSteps.ManualSpin) != 0 && (fresh.Journey.Guidance.TutorialBits & (int)TutorialSteps.VisibleImprovement) == 0,
                "Committed manual repair lost its completion or invented visible presentation");
            fresh.AcknowledgeAutoHint(5);
            fresh.ExecuteChapterSpin(6, manualSpin: false, tutorialBits: (int)TutorialSteps.VisibleImprovement);
            Need(fresh.Journey.Guidance.TutorialBits == 31 && fresh.Journey.Guidance.LastCompletion.Sequence == 4, "Supplied visibility/manual/AUTO facts or last completion did not persist");
            fresh.AcknowledgeGuidance(GuideIds.Completion, 7, 4);
            fresh.ExecuteChapterSpin(8, manualSpin: false); fresh = Open(freshStorage);
            Need(fresh.Journey.Guidance.TutorialBits == 31 && fresh.Journey.Guidance.LastCompletion.Sequence == 4 &&
                fresh.Journey.Guidance.CompletionAcknowledgedSequence == 4 && fresh.Guidance.LastCompletion.ProjectId == GameIds.HouseRepair,
                "Later nonmanual spins/reload resurrected completion ACK or lost finite tutorial facts");
            Refuse(freshStorage, fresh, () => fresh.AcknowledgeGuidance(GuideIds.Completion, fresh.State.sequence + 1, 3), "Stale completion identity dismissed current completion");
            var immediateStorage = new Storage(); var immediate = Open(immediateStorage, initialize: true);
            immediate.ExecuteChapterSpin(1);
            Need(immediate.State.coins == 45 && immediate.State.houseStage == 0, "Unselected first manual spin did not retain its8 coins");
            immediate.SelectChapterProject(GameIds.HouseRepair, 2);
            Need(immediate.State.coins == 5 && immediate.State.sequence == 2 && immediate.State.houseStage == 1 && immediate.Journey.Guidance.LastCompletion.Sequence == 2 &&
                (immediate.Journey.Guidance.TutorialBits & ((int)TutorialSteps.Selected | (int)TutorialSteps.ManualSpin)) == ((int)TutorialSteps.Selected | (int)TutorialSteps.ManualSpin),
                "Same selection/purchase command lost completion or selection facts through sequence deduplication");
            passed.Add("Actual Fresh creation grants37 once; metadata ACK preserves target/RNG/wallet, factual manual/visible/AUTO bits and latest completion survive nonmanual spins/reload without inferred visibility");

            string r8Raw = Fixture(Path.Combine("Checks", "EraMigration", "R8", "Fixtures", "all-skills-owned.json"));
            var r8Storage = new Storage { primary = r8Raw }; var existing = Open(r8Storage, initialize: true);
            Need(r8Storage.writes == 0 && r8Storage.primary == r8Raw && existing.Journey.Guidance.Origin == GuideOrigin.Existing && existing.Guidance.Suggestion?.HintId != GuideIds.ExpandedR7,
                "Actual existing r8 data was mislabeled Fresh/LegacyExpanded or eagerly rewritten");
            var oldR7 = Funded(42); oldR7.houseStage = 2; oldR7.workshopBuilt = 1; oldR7.autoHintSeen = 1;
            oldR7.wave = new WaveProgress(1, 1, WaveProgress.Empty.Delivery, WaveProgress.Empty.Slot);
            var r7Storage = Stored(oldR7); string r7Raw = r7Storage.primary; var expanded = Open(r7Storage, initialize: true);
            Need(r7Storage.writes == 0 && r7Storage.primary == r7Raw && expanded.Journey.Guidance.Origin == GuideOrigin.LegacyExpanded && expanded.Guidance.Suggestion.HintId == GuideIds.ExpandedR7,
                "Observed pre-chapter r7 completion lost its factual expansion notice");
            expanded.AcknowledgeGuidance(GuideIds.ExpandedR7, 1); expanded = Open(r7Storage);
            Need(expanded.State.coins == 42 && expanded.Journey.Guidance.Origin == GuideOrigin.LegacyExpanded && expanded.Journey.Guidance.IsAcknowledged(GuideIds.ExpandedR7) && expanded.Guidance.Suggestion?.HintId != GuideIds.ExpandedR7,
                "Expanded notice ACK granted coins, lost origin or resurrected after reload");
            passed.Add("Observed existing r8 staysExisting with no eager rewrite; actual pre-chapter four-family r7 completion getsLegacyExpanded once, persisted ACK preserves42 and provenance");

            var acceptBaseline = complete; var progressStorage = Stored(complete); var progressSession = Open(progressStorage);
            progressSession.AcceptOrder(OrderIds.TrashSort, progressSession.State.sequence + 1); var progressBaseline = progressSession.State;
            for (int operation = 0; operation < 5; operation++)
            {
                var baseline = operation == 0 ? acceptBaseline : operation == 1 ? progressBaseline : operation < 4 ? readyTrash : freshBaseline;
                long sequence = baseline.sequence + 1;
                Action<GameSession> invoke = session => {
                    if (operation == 0) session.AcceptOrder(OrderIds.TrashSort, sequence);
                    else if (operation == 1) session.ExecuteChapterSpin(sequence);
                    else if (operation == 2) session.ClaimOrder(OrderIds.TrashSort, firstInstance, sequence);
                    else if (operation == 3) session.CancelOrder(OrderIds.TrashSort, firstInstance, sequence);
                    else session.AcknowledgeGuidance(GuideIds.Welcome, sequence);
                };
                foreach (bool committed in new[] { false, true }) foreach (int failedWrite in new[] { 1, 2 })
                {
                    var failureStorage = Stored(baseline); failureStorage.failAt = failedWrite; failureStorage.commitBeforeFailure = committed;
                    var failure = Open(failureStorage);
                    try { invoke(failure); throw new Exception("Injected journey write failure was not observed"); }
                    catch (IOException) { }
                    Need(failure.RecoveryRequired, "Uncertain journey save allowed mutation");
                    var blocked = failure; Rejects(() => blocked.ExecuteChapterSpin(sequence), "Uncertain journey save accepted another foreground event");
                    failureStorage.failAt = 0; failure = Open(failureStorage); invoke(failure);
                    long expected = operation == 0 ? 690 : operation == 1 ? 715 : operation == 2 ? 1182 : operation == 3 ? 1062 : 37;
                    Need(failure.State.coins == expected && failure.State.sequence == sequence && failure.State.pending == null, "Interrupted journey command duplicated/lost wallet/sequence");
                    if (operation == 0) Need(failure.Journey.Orders.ActiveOrderId == OrderIds.TrashSort && failure.Journey.Orders.Progress == 0, "Recovered accept invented progress");
                    if (operation == 1) Need(failure.Journey.Orders.Progress == 1 && failure.Journey.Orders.LastEvent.OperationSequence == sequence, "Spin/order observation was not atomic");
                    if (operation == 2) Need(failure.Journey.Orders.LatestClaim.ClaimSequence == sequence && failure.Journey.Orders.ActiveOrderId == null, "Recovered claim identity changed");
                    if (operation == 3) Need(failure.Journey.Orders.ActiveOrderId == null && failure.Journey.Orders.LatestClaim == null, "Recovered cancel paid a reward");
                    if (operation == 4) Need(failure.Journey.Guidance.IsAcknowledged(GuideIds.Welcome), "Recovered ACK was lost");
                    failure = Open(failureStorage); writes = failureStorage.writes; snapshot = failureStorage.primary; invoke(failure);
                    Need(failureStorage.writes == writes && failureStorage.primary == snapshot && failure.State.coins == expected, "Recovered exact journey replay wrote or rewarded twice");
                }
            }
            passed.Add("20 accept/source-spin-progress/claim/cancel/guideACK before/after reserve/applied-write failures block uncertainty and atomically recover once");

            var richStorage = new Storage { primary = r8Raw }; var rich = Open(richStorage);
            foreach (string family in FamilyIds.All)
                while (ChapterEconomy.Context(rich.Chapter).Stages.AbsoluteLevel(family) < 5)
                {
                    int next = ChapterEconomy.Context(rich.Chapter).Stages.LocalLevel(family) + 1;
                    rich.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard, family, next), rich.State.sequence + 1);
                }
            Need(rich.State.coins == 3540 && rich.Chapter.Skills.Receipts.Count == 6 && ChapterCatalog.Rules.SymbolModifiers(rich.Chapter.Progress).Sources.Count == 19,
                "Immutable v1 fixture completion changed8160−4620 or lost6receipts/19sources");
            rich.AcceptOrder(OrderIds.TrashSort, rich.State.sequence + 1); rich.ExecuteChapterSpin(rich.State.sequence + 1);
            Need(rich.State.coins == 3569 && rich.Journey.Orders.Progress == 1 && rich.State.lastSpin.chapter.Effects.TotalCoins == 20, "Rich v1 ownership lost independent29 spin or order observation");
            var richState = rich.State; var richPending = JourneyEconomy.ReserveChapter(richState, "spin", null, richState.sequence + 1, manualSpin: false);
            string richRaw = Codec.Encode(richState), pendingRaw = Codec.Encode(richPending);
            Need(richRaw.Length < 65536 && pendingRaw.Length < 65536 && Codec.Encode(Codec.Decode(richRaw)) == richRaw && Codec.Encode(Codec.Decode(pendingRaw)) == pendingRaw,
                "Rich root/receipt/pending journey payload violates depth8/65536 round-trip bounds");
            Rejects(() => Codec.Decode(RemoveObject(pendingRaw, "journey", pendingRaw.IndexOf("\"pending\":{", StringComparison.Ordinal))), "Missing pending journey descriptor fell back to economic-only spin");
            Rejects(() => Codec.Decode(RemoveObject(richRaw, "journey")), "Missing spin journey descriptor accepted after adoption");
            Rejects(() => Codec.Decode(RemoveObject(richRaw, "journey", last: true)), "Missing root journey aggregate accepted with modern receipts");
            Rejects(() => Codec.Decode(Change(richRaw, "\"journey\":{", "\"journey\":null,\"unexpected\":{", last: true)), "Present null root journey accepted");
            Rejects(() => Codec.Decode(Change(richRaw, "\"rule\":\"journey.rules.v1\"", "\"rule\":\"journey.rules.v404\"", last: true)), "Unknown root journey revision accepted");
            Rejects(() => Codec.Decode(Change(richRaw, "\"commandSequence\":" + richState.journey.CommandSequence, "\"commandSequence\":" + (richState.sequence + 1), last: true)), "Future root command marker accepted");
            Rejects(() => Codec.Decode(Change(pendingRaw, "\"orderKey\":\"", "\"orderKey\":\"0")), "Corrupt frozen order proposal identity accepted");
            int ordersField = richRaw.LastIndexOf("\"orders\":", StringComparison.Ordinal) + "\"orders\":".Length;
            string nullOrders = richRaw.Substring(0, ordersField) + "null" + richRaw.Substring(ValueEnd(richRaw, ordersField));
            Rejects(() => Codec.Decode(nullOrders), "Present null order payload treated as absent default");
            passed.Add("Immutable price-v1 ownership survives19sources/6receipts withjourney root+spin+pending below65536 (" + richRaw.Length + "/" + pendingRaw.Length + "); strict missing/null/unknown/future/corrupt descriptors reject");
            return passed.AsReadOnly();
        }
    }
}
