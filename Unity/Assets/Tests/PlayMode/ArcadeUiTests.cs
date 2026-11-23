#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    // Actual-scene regression cases use private X-drive saves and independent
    // literal expected outcomes. No test hook changes the live economic inputs.
    [Category("Arcade10h")]
    public sealed class ArcadeUiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const string ScenePath = "Assets/Scenes/JunkToNeon.unity";
        const string SaveOverride = "JTN_EDITOR_SAVE_DIRECTORY", ClockOverride = "JTN_EDITOR_UTC_SECONDS";
        readonly JsonStateCodec codec = new JsonStateCodec();
        string folder, previousSave, previousClock;
        Scene scene, previousScene;
        Component game;
        Type gameType;
        GameSession Session => (GameSession)gameType.GetProperty("Session").GetValue(game);
        bool Flag(string name) => (bool)gameType.GetProperty(name).GetValue(game);
        T Field<T>(string name) => (T)gameType.GetField(name, Hidden).GetValue(game);
        void Call(string name, params object[] args) => gameType.GetMethod(name, Hidden | BindingFlags.Public).Invoke(game, args);
        string State => codec.Encode(Session.State);
        Dictionary<string, string> Files()
        {
            Assert.That(Directory.GetDirectories(folder), Is.Empty);
            var paths = Directory.GetFiles(folder); Assert.That(paths.Length, Is.LessThanOrEqualTo(8));
            foreach (string path in paths)
            {
                Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True);
                Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(262148));
            }
            return paths.ToDictionary(Path.GetFileName,
                path => Convert.ToBase64String(File.ReadAllBytes(path)) + "|" + File.GetLastWriteTimeUtc(path).Ticks);
        }
        void Unchanged(string expectedState, Dictionary<string, string> expectedFiles)
        {
            Assert.That(State, Is.EqualTo(expectedState)); Assert.That(Files(), Is.EquivalentTo(expectedFiles));
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
        }
        void Saved()
        {
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            Assert.That(Session.State.pending, Is.Null);
            Assert.That(File.ReadAllText(Path.Combine(folder, "jtn.save.v1.json")), Is.EqualTo(State));
        }
        IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 7;
            while (Flag("Busy") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Flag("Busy"), Is.False, "The one committed presentation did not finish");
            Assert.That(Flag("AutoRunning"), Is.False); Saved();
        }
        IEnumerator PauseAndResume()
        {
            Call("OnApplicationPause", true); Assert.That(Flag("InBackground"), Is.True);
            yield return null; Call("OnApplicationPause", false); yield return null;
            Assert.That(Flag("InBackground"), Is.False); Saved();
        }
        IEnumerator Reload()
        {
            Call("Reload"); yield return null; Canvas.ForceUpdateCanvases(); Saved();
        }
        sealed class MemoryStorage : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw;
            public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; }
        }
        GameSession Fixture(ModelState state) => GameSession.Open(
            new GameStore(new MemoryStorage { Raw = codec.Encode(state) }, codec), 1, initializeJourney: true);
        ModelState LegacySelected(long price)
        {
            ModelState state;
            if (price == 540)
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-selected540.json"));
                using (var sha = SHA256.Create())
                    Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""),
                        Is.EqualTo("CAD546964362B38ECCD057F98248F40FF43E6920839DC9125EEF5758F0CE678E"));
                state = codec.Decode(File.ReadAllText(path));
            }
            else
            {
                Assert.That(price, Is.EqualTo(600)); state = ModelState.Fresh(1);
                state.houseStage = 1; state.selectedStage = 2; state.autoHintSeen = 1;
            }
            // Declared synthetic initial boundary; no paid, pending or spin
            // receipt is fabricated, and no subsequent result is rewritten.
            state.coins = price - 5; state.lastSettledAt = 1000; GameLogic.Validate(state);
            var setup = Fixture(state); Assert.That(setup.RecoveryRequired, Is.False, setup.Error);
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(price));
            return setup.State;
        }
        void OldGoal(long price, long coins)
        {
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.houseStage, Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(price));
        }
        // Synthetic INITIAL balances only. Ticket/slot/run/seed/history fields
        // are never set: tickets below are earned by genuine public foreground
        // commands; ownership and all tested transitions use production APIs.
        ModelState FundFixture(ModelState initial, long diamonds, int parts, int potatoes, int energy)
        {
            var state = initial.Copy(); Assert.That(state.pending, Is.Null);
            var f = ExpansionCodec.EncodeState(state.expansion).Split('|');
            Assert.That(f.Length, Is.EqualTo(24)); Assert.That(f[0], Is.EqualTo("expansion.state.v3"));
            var a = Encoding.UTF8.GetString(Convert.FromBase64String(f[23])).Split(';');
            Assert.That(a.Length, Is.EqualTo(13)); Assert.That(a[0], Is.EqualTo("arcade.state.v1"));
            Assert.That(a[8], Is.EqualTo("-"), "Only the initial no-run fixture can be funded");
            f[1] = parts.ToString(); f[2] = potatoes.ToString(); f[3] = energy.ToString();
            a[3] = diamonds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            f[23] = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";", a)));
            state.expansion = ExpansionCodec.DecodeState(string.Join("|", f)); GameLogic.Validate(state); return state;
        }
        ModelState Prepared(long price, long diamonds = 0, int parts = 80, int potatoes = 80, int energy = 30, bool bonus = false, int spins = 8)
        {
            var state = LegacySelected(price); state.coins = 0; var setup = Fixture(state);
            // At most31 small pure fixture operations, not an Editor grind or
            // a fabricated historical ticket/retained resource receipt.
            for (int i = 0; i < spins; i++) setup.ExecuteChapterSpin(setup.State.sequence + 1, 1000);
            Assert.That(new[] { 8, 16, 31 }, Does.Contain(spins));
            Assert.That(setup.State.coins, Is.EqualTo(spins == 8 ? 59 : spins == 16 ? 103 : 218), "Independent seed1 payout ledger");
            Assert.That(setup.State.seed, Is.EqualTo(spins == 8 ? 3652395599u : spins == 16 ? 2371448660u : 3636105407u));
            Assert.That(setup.Arcade.Tickets, Is.EqualTo(spins == 8 ? 1 : spins == 16 ? 2 : 3));
            Assert.That(setup.Arcade.TicketProgress, Is.EqualTo(spins == 31 ? 7 : 0));
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(price));
            setup.SelectExpansionSpeed("speed.fast", setup.State.sequence, 1000);
            if (bonus)
            {
                setup = Fixture(FundFixture(setup.State, 0, 200, 200, 30));
                setup.BuyExpansion(setup.GetExpansionOffer("bonus.unlock"), 1000);
                Assert.That(setup.Expansion.BonusUnlocked, Is.True);
            }
            return FundFixture(setup.State, diamonds, parts, potatoes, energy);
        }
        ModelState Lower(ModelState state)
        {
            var copy = state.Copy(); Assert.That(copy.pending, Is.Null); copy.expansion = null;
            if (copy.lastSpin != null) copy.lastSpin.expansion = null; return copy;
        }
        void LowerUnchanged(ModelState initial, int commands)
        {
            var expected = Lower(initial); expected.sequence += commands; expected.revision = expected.sequence * 2;
            Assert.That(codec.Encode(Lower(Session.State)), Is.EqualTo(codec.Encode(expected)),
                "Arcade changes only its own state and the normal global sequence/revision"); Saved();
        }
        Button Primary => Field<Button>("arcadePrimaryButton");
        Button Secondary => Field<Button>("arcadeSecondaryButton");
        Button Card(int index) => Field<Button[]>("arcadeCards")[index];
        IEnumerator Home()
        {
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            yield return Click(Field<Button>("productAvatar"));
            yield return Click(Field<Button[]>("productCards")[2]);
            yield return Click(Field<Button>("arcadeEntryButton"));
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); ArcadeFits();
        }
        IEnumerator BackToGame()
        {
            int previous = -1;
            for (int i = 0; i < 5 && Field<GameObject>("panel").activeSelf; i++)
            {
                int mode = Field<int>("wavePanel");
                Assert.That(mode == 20 || mode == 75 || mode >= 60 && mode <= 65, Is.True, "Unexpected acknowledgement/modal");
                if (mode == 75)
                {
                    Assert.That(previous, Is.EqualTo(20), "Profile is valid here only as the Sections return origin");
                    Assert.That(Field<TMP_Text>("panelTitle").text, Is.EqualTo("My Character"));
                }
                previous = mode;
                yield return Back();
                if (mode == 60) Assert.That(Field<int>("wavePanel"), Is.EqualTo(20), "Arcade Home returns to Sections");
                if (mode == 20) Assert.That(Field<int>("wavePanel"), Is.EqualTo(75), "Sections returns to its Product Profile origin");
                if (mode == 75) Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Profile returns to the game");
            }
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
        }
        IEnumerator Offer(int category, int card)
        {
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); yield return Click(Card(category));
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(61)); yield return Click(Card(card));
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(62)); ArcadeFits();
        }
        IEnumerator GamePreview(int card, string id)
        {
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); yield return Click(Card(card));
            var quote = Field<ArcadeQuote>("arcadeQuote"); Assert.That(quote, Is.Not.Null, "The funded visible game must retain its start quote");
            Assert.That(quote.Id, Is.EqualTo(id)); ArcadeFits();
        }
        ArcadeQuote Quote(string action, string id, long diamonds = 0, int parts = 0, int potatoes = 0, int tickets = 0, int reward = 0)
        {
            var q = Field<ArcadeQuote>("arcadeQuote"); Assert.That(q, Is.Not.Null);
            Assert.That(q.Action, Is.EqualTo(action)); Assert.That(q.Id, Is.EqualTo(id));
            Assert.That(q.ExpectedSequence, Is.EqualTo(Session.State.sequence));
            Assert.That(new[] { q.DiamondsCost, q.PartsCost, q.PotatoesCost, q.TicketsCost, q.DiamondsCredit },
                Is.EqualTo(new long[] { diamonds, parts, potatoes, tickets, reward })); return q;
        }
        void ArcadeFits()
        {
            foreach (var text in Field<RectTransform>("arcadeRoot").GetComponentsInChildren<TMP_Text>()) Fits(text);
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy, Is.False);
        }
        void Run(string id, string phase, int cursor, int attempts, int correct, int reward = -1)
        {
            var run = Session.Arcade.ActiveRun; Assert.That(run, Is.Not.Null); Assert.That(run.Id, Is.EqualTo(id));
            Assert.That(run.Phase, Is.EqualTo(phase)); Assert.That(run.Cursor, Is.EqualTo(cursor));
            Assert.That(run.Attempts, Is.EqualTo(attempts)); Assert.That(run.Correct, Is.EqualTo(correct));
            Assert.That(run.ReadyReward, Is.EqualTo(reward)); Assert.That(run.Ready, Is.EqualTo(phase == "ready")); Saved();
        }
        string[] Flyers() => Field<TMP_Text[]>("rewardFeedbackLabels").Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
        void NoFlyers() { Assert.That(Flyers(), Is.Empty); Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game), Is.Zero); }
        void FeedbackDoesNotIntercept(Button target)
        {
            var root = Field<RectTransform>("rewardFeedbackRoot"); var labels = Field<TMP_Text[]>("rewardFeedbackLabels");
            Assert.That(labels.Length, Is.EqualTo(8)); Assert.That(root.GetComponentsInChildren<Graphic>(true).Length, Is.EqualTo(8));
            Assert.That(root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
            { Assert.That(group.blocksRaycasts, Is.False); Assert.That(group.interactable, Is.False); }
            Target(target);
        }
        IEnumerator FeedbackEnds()
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (Flyers().Length > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            NoFlyers();
        }
        void OptionalSnapshot(string name, string provenance)
        {
            string target = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/optional-progression-delivery-20261101/root", "produced-" + name + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "jtn.save.v1.json"), State, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target, "arcade-body.txt"), ArcadeBody, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target, "arcade-primary.txt"), Caption(Primary), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target, "arcade-secondary.txt"), Caption(Secondary), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target, "provenance.txt"), provenance + " Sequence " + Session.State.sequence + ". Text records the current UI; reload does not replay a prior result.", new UTF8Encoding(false));
            TestContext.WriteLine("Private produced snapshot: " + target);
        }
        void ReturnArcadeSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/return-session-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),State,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel.txt"),Field<TMP_Text>("panelBody").text,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"arcade-body.txt"),ArcadeBody,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"events-card-3.txt"),Field<Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>(true).text,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+" Clock="+Environment.GetEnvironmentVariable(ClockOverride)+"; sequence="+Session.State.sequence+". UI text is captured, not an economic event.",new UTF8Encoding(false));
            TestContext.WriteLine("Private produced saved-round snapshot: "+target);
        }
        ModelState OptionalReadyMemory(int ownedLevels, bool bonusUnlocked)
        {
            long initialDiamonds = ownedLevels == 0 ? 0 : ownedLevels == 1 ? 2 : ownedLevels == 2 ? 6 : 13;
            // Only the final 0-level scenario needs a second earned ticket for a newer run.
            var setup = Fixture(Prepared(540, initialDiamonds, 100, 100, bonus: bonusUnlocked, spins: ownedLevels == 0 ? 16 : 8));
            for (int level = 1; level <= ownedLevels; level++)
            {
                string id = "arcade.slot." + level;
                setup.ExecuteArcade(setup.GetArcadeQuote("buy", id, setup.Arcade.Sequence), 1000);
            }
            Assert.That(setup.Arcade.SlotLevel, Is.EqualTo(ownedLevels)); Assert.That(setup.Arcade.Diamonds, Is.Zero);
            setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.memory", setup.Arcade.Sequence), 1000);
            long instance = setup.Arcade.ActiveRun.InstanceSequence;
            // Existing independently known first board [1,2,2,0,0,1]. Each pick is a public committed command.
            foreach (int card in new[] { 0, 5, 1, 2, 3, 4 })
                setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, instance, card), 1000);
            Assert.That(setup.Arcade.ActiveRun.ReadyReward, Is.EqualTo(3));
            Assert.That(setup.Arcade.ActiveRun.Ready, Is.True); Assert.That(setup.Arcade.Diamonds, Is.Zero);
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(540));
            return setup.State;
        }
        IEnumerator OptionalOpenReadyThroughEvents()
        {
            yield return Click(Field<Button[]>("productTabs")[2]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(71));
            yield return Click(Field<Button[]>("productCards")[3]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64));
            Quote("claim", "arcade.memory", reward: 3);
        }
        [UnityTest] public IEnumerator PositiveClaimContinuationUsesFreshInspectionAndPreservesReturnContext()
        {
            var ready = OptionalReadyMemory(1, false); yield return Load(ready);
            Assert.That(Session.Arcade.Diamonds, Is.Zero); Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(1));
            string before = State; var files = Files(); yield return OptionalOpenReadyThroughEvents(); Unchanged(before, files);
            var claim = Quote("claim", "arcade.memory", reward: 3); long instance = claim.ExpectedInstance;
            var staleClaim = Primary.onClick; yield return Click(Primary);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(claim.Key));
            Assert.That(Session.Arcade.LastClaim.InstanceSequence, Is.EqualTo(instance)); Assert.That(Session.Arcade.ActiveRun, Is.Null);
            Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(1)); LowerUnchanged(ready, 1); OldGoal(540, ready.coins);
            OptionalSnapshot("positive-claim-next2", "Actual positive Memory claim through Product Events; three pairs were publicly committed in a private fixture after eight actual Core spins and a public Slot I purchase. Funding was synthetic.");
            before = State; files = Files(); staleClaim.Invoke(); Unchanged(before, files);
            Assert.That(Primary.isActiveAndEnabled && Primary.IsInteractable(), Is.True);
            Assert.That(Caption(Primary), Does.Contain("Inspect").And.Contain("Slot Level II"));
            Assert.That(Caption(Secondary), Is.EqualTo("To game")); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            Assert.That(Secondary.isActiveAndEnabled && Secondary.IsInteractable(), Is.True); PolishView();
            var staleContinuation = Primary.onClick; var held = Down(Primary, 801); yield return Click(Primary, 802);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(62)); Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Slot Level II"));
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Primary.interactable, Is.False);
            Assert.That(ArcadeBody, Does.Contain("4 diamonds").And.Contain("Not enough")); Unchanged(before, files);
            staleContinuation.Invoke(); Unchanged(before, files);
            yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(61)); staleContinuation.Invoke(); Unchanged(before, files);
            yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(60));
            yield return Offer(1, 0); Quote("buy", "arcade.pack.parts", 1); Release(held, Primary); staleContinuation.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(60));
            yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(71)); Unchanged(before, files);

            ready = OptionalReadyMemory(2, false); yield return ReplacePrivateFixture(ready); yield return OptionalOpenReadyThroughEvents();
            claim = Quote("claim", "arcade.memory", reward: 3); yield return Click(Primary);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(claim.Key));
            Assert.That(Caption(Primary), Does.Contain("Slot Level III"));
            before = State; files = Files(); yield return Click(Primary);
            Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Slot Level III"));
            Assert.That(Session.Expansion.BonusUnlocked, Is.False); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            Assert.That(Primary.interactable, Is.False); Assert.That(ArcadeBody, Does.Contain("An unlocked bonus reel is required.").And.Contain("Not enough diamonds")); Unchanged(before, files);
            yield return Back(); yield return Back(); yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(71)); Unchanged(before, files);

            ready = OptionalReadyMemory(3, true); yield return ReplacePrivateFixture(ready); yield return OptionalOpenReadyThroughEvents();
            OptionalSnapshot("ready-slot3", "Public fixture: eight foreground spins, explicit bonus unlock and three slot purchases, one Memory start and six public pair picks; synthetic initial funds. Ready3 remains unpaid.");
            yield return Click(Primary); Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(3)); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3));
            Assert.That(Primary.gameObject.activeSelf && Primary.interactable, Is.False, "Level III must not offer a nonexistent next slot");
            Assert.That(ArcadeBody, Does.Contain("All three slot levels are unlocked").And.Not.Contain("IV")); before = State; files = Files();
            Assert.That(Caption(Secondary), Is.EqualTo("To game")); yield return Click(Secondary);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False); Assert.That(Flag("AutoRunning"), Is.False); Unchanged(before, files);
            yield return Reload(); Unchanged(before, files); NoFlyers();

            ready = OptionalReadyMemory(0, false); yield return ReplacePrivateFixture(ready); yield return OptionalOpenReadyThroughEvents();
            OptionalSnapshot("ready-slot0", "Public fixture: sixteen foreground spins, one Memory start and six public pair picks; synthetic initial funds. Ready3 remains unpaid and Slot I is not owned.");
            claim = Quote("claim", "arcade.memory", reward: 3);
            yield return Click(Primary); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3));
            Assert.That(Caption(Primary), Does.Contain("Slot Level I"));
            before = State; files = Files(); var old = Primary.onClick; held = Down(Primary, 803);
            yield return Click(Primary); Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Slot Level I"));
            var next = Quote("buy", "arcade.slot.1", 2, 20); Assert.That(next.Key, Is.Not.EqualTo(claim.Key));
            Assert.That(Session.Arcade.SlotLevel, Is.Zero); old.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Back(); yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(71));
            yield return Reload(); old.Invoke(); Unchanged(before, files); Assert.That(Flag("AutoRunning"), Is.False);
            yield return Click(Field<Button[]>("productTabs")[2]); yield return Click(Field<Button[]>("productCards")[1]);
            Quote("start", "arcade.sorting", tickets: 1); old.Invoke(); Unchanged(before, files); yield return Click(Primary);
            Assert.That(Session.Arcade.ActiveRun.Id, Is.EqualTo("arcade.sorting"));
            before = State; files = Files(); Release(held, Card(0)); old.Invoke(); Unchanged(before, files);
            yield return PauseAndResume(); old.Invoke(); Unchanged(before, files);
            yield return Reload(); old.Invoke(); Unchanged(before, files); Assert.That(Flag("AutoRunning"), Is.False);
        }

        [UnityTest] public IEnumerator SlotLevelsKeepSaved540PriceAndRequireFreshConsentAcrossOffers()
        {
            void Preview(int level, int box, int potato, int rawParts, int rawPotatoes, string target)
            {
                var current = Session.Arcade; Assert.That(current.SlotLevel, Is.EqualTo(level));
                Assert.That(new[] { current.BoxPresencePartBonus, current.PotatoPresencePotatoBonus,
                    current.RawBonusPartsAddition, current.RawBonusPotatoesAddition }, Is.EqualTo(new[] { box, potato, rawParts, rawPotatoes }));
                Assert.That(ArcadeBody, Does.Contain($"Current: level {level}/3.\nBox: +{box} parts; potato: +{potato} potatoes.\nBase bonus: +{rawParts} parts, +{rawPotatoes} potatoes."));
                if (target != null) Assert.That(ArcadeBody, Does.Contain(target));
                PolishView(); Assert.That(Field<TMP_Text>("arcadeBody").fontSize, Is.GreaterThanOrEqualTo(10));
            }
            const string targetI = "Viewed level: 1.\nPer spin with a Box: Parts +1; with a Potato: Potatoes +0.\nRaw bonus: Parts +0, Potatoes +0.\nOn presence; bonus before focus and booster, then storage caps.";
            const string targetII = "Viewed level: 2.\nPer spin with a Box: Parts +1; with a Potato: Potatoes +1.\nRaw bonus: Parts +0, Potatoes +0.\nOn presence; bonus before focus and booster, then storage caps.";
            const string targetIII = "Viewed level: 3.\nPer spin with a Box: Parts +1; with a Potato: Potatoes +1.\nRaw bonus: Parts +1, Potatoes +1.\nOn presence; bonus before focus and booster, then storage caps.";
            var initial = Prepared(540, 20, 100, 100, bonus: true); yield return Load(initial);
            long coins = initial.coins; uint seed = initial.seed; string before = State; var files = Files();
            yield return Home(); yield return Offer(0, 2); Preview(0, 0, 0, 0, 0, targetIII);
            Assert.That(ArcadeBody, Does.Contain("The previous slot level is required. Its cost is separate.").And.Contain("Cost: 7 diamonds + 30 parts + 30 potatoes (this level only).").And.Not.Contain(targetI));
            Assert.That(ArcadeBody, Does.Contain("Increase from level 2 to 3: per bonus: base +1 parts, +1 potatoes; before focus, boost and limits."));
            Assert.That(ArcadeBody, Does.Not.Contain("After purchase:"), "Locked future levels have no legal captured purchase quote");
            OptionalSnapshot("slot0-locked3-preview", "Untouched prepared slot0 state: synthetic initial funding and genuine earlier bonus unlock/eight public fixture spins; locked Slot III inspected before any slot purchase, historical 540 goal preserved.");
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Primary.interactable, Is.False);
            Primary.onClick.Invoke(); Unchanged(before, files); OldGoal(540, coins);
            yield return Back(); Unchanged(before, files); yield return BackToGame(); Unchanged(before, files);
            yield return Home(); yield return Offer(0, 0); var first = Quote("buy", "arcade.slot.1", 2, 20);
            Preview(0, 0, 0, 0, 0, targetI);
            Assert.That(ArcadeBody, Does.Contain("Increase from level 0 to 1: parts +1 once per spin with a box."));
            Assert.That(ArcadeBody, Does.Contain("After purchase: 18 diamonds; 80 parts; 100 potatoes."));
            Unchanged(before, files); var stale = Primary.onClick; var held = Down(Primary, 711);
            yield return Back(); yield return Back(); yield return Offer(1, 0);
            Quote("buy", "arcade.pack.parts", 1); Release(held, Primary); stale.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Back(); yield return Offer(0, 0);
            Assert.That(Quote("buy", "arcade.slot.1", 2, 20).Key, Is.EqualTo(first.Key), "New visible consent is required even for an otherwise identical quote");
            Preview(0, 0, 0, 0, 0, targetI); Unchanged(before, files);
            Submit(Primary); yield return null;
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new long[] { 18, 80, 100 }));
            Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(1)); LowerUnchanged(initial, 1); NoFlyers();
            Assert.That(ArcadeBody,Does.Contain("Slot Parts: +0 → +1 once per future spin with a Box; caps apply."));
            yield return PurchaseChoicePortrait.Capture(game,"slot-I-result","arcadeBody");
            OptionalSnapshot("slot1-purchased", "Actual UI purchase of Slot I; synthetic initial funding, genuine earlier bonus unlock and eight foreground fixture spins; selected historical 540 goal preserved.");
            before = State; files = Files(); stale.Invoke(); stale.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Offer(0, 1); Quote("buy", "arcade.slot.2", 4, potatoes: 20);
            Preview(1, 1, 0, 0, 0, targetII); Unchanged(before, files);
            Assert.That(ArcadeBody, Does.Contain("Increase from level 1 to 2: potatoes +1 once per spin with a potato."));
            Assert.That(ArcadeBody, Does.Contain("After purchase: 14 diamonds; 80 parts; 80 potatoes."));
            var second = Primary.onClick; yield return Click(Primary);
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new long[] { 14, 80, 80 }));
            Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(2)); LowerUnchanged(initial, 2);
            Assert.That(ArcadeBody,Does.Contain("Slot Potatoes: +0 → +1 once per future spin with a Potato; caps apply."));
            OptionalSnapshot("slot2-purchased", "Actual UI purchase of Slot II after an actual UI Slot I purchase; synthetic initial funding and selected historical 540 goal preserved.");
            before = State; files = Files(); second.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Offer(0, 2); Quote("buy", "arcade.slot.3", 7, 30, 30);
            Preview(2, 1, 1, 0, 0, targetIII); Unchanged(before, files);
            Assert.That(ArcadeBody, Does.Contain("Increase from level 2 to 3: per bonus: base +1 parts, +1 potatoes; before focus, boost and limits."));
            Assert.That(ArcadeBody, Does.Contain("After purchase: 7 diamonds; 50 parts; 50 potatoes."));
            yield return Click(Primary);
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new long[] { 7, 50, 50 }));
            Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(3));
            Assert.That(ArcadeBody,Does.Contain("Slot raw addition: +0/+0 → +1/+1 Parts/Potatoes per future bonus before focus, boost and caps."));
            yield return PurchaseChoicePortrait.Capture(game,"slot-III-result","arcadeBody");
            Assert.That(new[] { Session.Arcade.BoxPresencePartBonus, Session.Arcade.PotatoPresencePotatoBonus,
                Session.Arcade.RawBonusPartsAddition, Session.Arcade.RawBonusPotatoesAddition }, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            OldGoal(540, coins); Assert.That(Session.State.seed, Is.EqualTo(seed)); LowerUnchanged(initial, 3); NoFlyers();
            OptionalSnapshot("slot3-purchased", "Actual UI purchase of Slot III after I and II, with the bonus previously unlocked; synthetic initial funding and selected historical 540 goal preserved.");
            yield return Back(); yield return Click(Secondary); ArcadeFits();
            Assert.That(Field<TMP_Text>("arcadeBody").text, Does.Contain("Slot level: 3/3").And.Contain("Parts: +1 once per spin with a Box.").And.Contain("Base bonus: +1"));
            before = State; files = Files(); yield return Back(); yield return Offer(0, 0); Preview(3, 1, 1, 1, 1, null);
            Assert.That(ArcadeBody, Does.Contain("Unlocked level. Current slot effects:").And.Contain("This level is already unlocked. It cannot be purchased again.")
                .And.Not.Contain("Viewed level:").And.Not.Contain("After purchase:").And.Not.Contain("Cost:"));
            Assert.That(ArcadeBody, Does.Contain("Increase from level 0 to 1: parts +1 once per spin with a box."));
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Primary.interactable, Is.False); Assert.That(Caption(Primary), Is.EqualTo("Already unlocked"));
            Primary.onClick.Invoke(); Unchanged(before, files); OldGoal(540, coins); LowerUnchanged(initial, 3); NoFlyers();
            yield return Back(); Unchanged(before, files); yield return Back(); yield return Click(Secondary); ArcadeFits(); Unchanged(before, files);
            yield return BackToGame(); yield return Click(Field<Button>("spinButton")); NoFlyers(); yield return Finish();
            // Literal ninth primary draw from seed1 is Potato/Box/Trash:3Coins,
            // base2Parts/1Potato plus INCLUDED once-per-presence slot1/1.
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 1, 2 })); Assert.That(Session.State.seed, Is.EqualTo(2440533925u));
            Assert.That(new[] { Session.Arcade.LastEvent.PartsAdded, Session.Arcade.LastEvent.PotatoesAdded,
                Session.Arcade.LastEvent.SlotPartsIncluded, Session.Arcade.LastEvent.SlotPotatoesIncluded }, Is.EqualTo(new[] { 3, 2, 1, 1 }));
            Assert.That(Flyers(), Is.EquivalentTo(new[] { "Coins +3", "Parts +3", "Potatoes +2" }));
            FeedbackDoesNotIntercept(Field<Button>("spinButton")); OldGoal(540, coins + 3);
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new long[] { 7, 53, 52 }));
            TestContext.WriteLine("Saved540 unchanged; three explicit purchases20D->18->14->7, Parts100->80->80->50, Potatoes100->100->80->50. Held/rebound old offer cannot authorize an affordable pack. One actual ninth draw credits3Coins/3Parts/2Potatoes; INCLUDED slot1/1 is not added to feedback twice.");
            // Explicit synthetic full stocks precede a genuine current UI purchase; the 20-Part debit creates real headroom.
            var full = Prepared(540, 2, 200, 200); yield return ReplacePrivateFixture(full);
            Assert.That(Session.Expansion.Parts, Is.EqualTo(Session.Expansion.PartsCap)); Assert.That(Session.Expansion.Potatoes, Is.EqualTo(Session.Expansion.PotatoesCap));
            before = State; files = Files(); yield return Home(); yield return Offer(0, 0);
            Quote("buy", "arcade.slot.1", 2, 20); Unchanged(before, files);
            Assert.That(ArcadeBody, Does.Contain("After purchase: 0 diamonds; 180 parts; 200 potatoes.").And.Not.Contain("Storage full: no room."));
            OptionalSnapshot("slot1-full-stock-preview", "Untouched synthetic full-stock starting state after eight public fixture spins; current legal Slot I preview, before the explicit purchase; selected historical 540 goal preserved.");
            yield return Click(Primary); Assert.That(Session.Arcade.SlotLevel, Is.EqualTo(1));
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new long[] { 0, 180, 200 }));
            OldGoal(540, full.coins); LowerUnchanged(full, 1);
            OptionalSnapshot("slot1-full-stock-paid", "Actual UI Slot I purchase; synthetic starting full Parts/Potatoes and two Diamonds, eight publicly committed fixture spins, selected historical 540 goal. Payment creates 20 Parts of headroom.");
        }

        [UnityTest] public IEnumerator MemoryMismatchSurvivesBackPauseReloadAndClaimCannotFundANewerRun()
        {
            var initial = Prepared(600, 4, spins: 16); yield return Load(initial);
            string before = State; var files = Files(); yield return Home(); yield return GamePreview(3, "arcade.memory");
            var start = Quote("start", "arcade.memory", tickets: 1); var oldStart = Primary.onClick; Unchanged(before, files);
            yield return Click(Primary); long instance = start.Sequence;
            Assert.That(Session.Arcade.Tickets, Is.EqualTo(1)); Run("arcade.memory", "first", 0, 0, 0);
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { -1, -1, -1, -1, -1, -1 }));
            var oldSecond = Card(1).onClick; yield return Click(Card(0)); Run("arcade.memory", "second", 1, 0, 0);
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, -1, -1, -1, -1, -1 }));
            before = State; files = Files(); oldSecond.Invoke(); oldStart.Invoke(); Unchanged(before, files);
            ReturnArcadeSnapshot("saved-memory-active","Actual UI start and first card in a private seeded Memory round; initial funding synthetic, tickets earned by public fixture spins.");
            var heldBeforePause=Down(Card(1),790);var staleBeforePause=Card(1).onClick;
            yield return PauseAndResume(); oldSecond.Invoke(); Unchanged(before, files);
            oldStart.Invoke(); Unchanged(before, files);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(Field<RectTransform>("arcadeRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<bool>("arcadeContextValid"), Is.False); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            Assert.That(Field<Button[]>("arcadeCards").All(button => !button.interactable), Is.True);
            Assert.That(Primary.interactable || Secondary.interactable, Is.False);
            var coverTitle = Field<TMP_Text>("panelTitle"); var coverBody = Field<TMP_Text>("panelBody");
            Assert.That(coverTitle.isActiveAndEnabled && coverBody.isActiveAndEnabled, Is.True);
            Assert.That(coverTitle.text, Is.EqualTo("Mini-games"));
            Assert.That(coverBody.text, Is.EqualTo("This window must be reopened. Tap “Close”, then open “Events” in the bottom menu."));
            ReturnArcadeSnapshot("saved-memory-invalidated","Actual short pause invalidated the same saved Memory instance; no new reward, ticket or automatic reopening.");
            var coverClose = Field<Button>("closeButton"); Assert.That(Caption(coverClose), Is.EqualTo("Close"));
            Fits(coverTitle); Fits(coverBody); Fits(coverClose.GetComponentInChildren<TMP_Text>()); Target(coverClose);
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Pause invalidates the return context; Back must dismiss the covered modal"); Unchanged(before, files);
            yield return Reload(); NoFlyers(); yield return Click(Field<Button[]>("productTabs")[2]);yield return Click(Field<Button[]>("productCards")[3]);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(instance)); Run("arcade.memory", "second", 1, 0, 0);
            Release(heldBeforePause,Card(1));staleBeforePause.Invoke();Unchanged(before,files);
            yield return Click(Card(1)); Run("arcade.memory", "mismatch", 2, 1, 0);
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, 2, -1, -1, -1, -1 }));
            before = State; files = Files(); yield return Back(); yield return Click(Card(3)); Unchanged(before, files);
            yield return Reload(); Unchanged(before, files); NoFlyers(); yield return Home(); yield return Click(Card(3));
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(instance)); Run("arcade.memory", "mismatch", 2, 1, 0);
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, 2, -1, -1, -1, -1 }));
            yield return Click(Primary); Run("arcade.memory", "first", 3, 1, 0);
            // Independent literal vector for initial seed1831565813:
            // board[1,2,2,0,0,1], mismatch01 then pairs05,12,34.
            foreach (int card in new[] { 0, 5, 1, 2, 3, 4 }) yield return Click(Card(card));
            Run("arcade.memory", "ready", 9, 4, 3, 3); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(4)); NoFlyers();
            before = State; files = Files(); yield return Back(); yield return Click(Card(3)); Unchanged(before, files);
            var claim = Quote("claim", "arcade.memory", reward: 3); Assert.That(claim.ExpectedInstance, Is.EqualTo(instance));
            var oldClaim = Primary.onClick; var heldClaim = Down(Primary, 721); yield return Click(Primary, 722);
            Assert.That(Session.Arcade.ActiveRun, Is.Null); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(7));
            Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(claim.Key)); Assert.That(Flyers(), Is.EqualTo(new[] { "Diamonds +3" }));
            Assert.That(Field<RectTransform>("rewardFeedbackDiamondTarget"), Is.SameAs(Field<TMP_Text>("productDiamonds").rectTransform));
            Assert.That(Field<RectTransform>("rewardFeedbackDiamondTarget"), Is.Not.SameAs(Field<RectTransform>("rewardFeedbackCoinTarget")));
            Assert.That(Field<RectTransform>("rewardFeedbackDiamondTarget").gameObject.activeInHierarchy, Is.True);
            FeedbackDoesNotIntercept(Field<Button>("closeButton"));
            before = State; files = Files(); oldClaim.Invoke(); Unchanged(before, files);
            yield return Back(); yield return GamePreview(4, "arcade.sorting"); yield return Click(Primary);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.GreaterThan(instance)); Assert.That(Session.Arcade.Tickets, Is.Zero);
            Run("arcade.sorting", "sorting", 0, 0, 0); Assert.That(Session.Arcade.ActiveRun.CurrentSortingSymbol, Is.EqualTo(2));
            before = State; files = Files(); Release(heldClaim, Card(0)); oldClaim.Invoke(); Unchanged(before, files);
            Submit(Card(2)); yield return null; Run("arcade.sorting", "sorting", 1, 1, 1);
            OldGoal(600, initial.coins); LowerUnchanged(initial, 13);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(7));
            TestContext.WriteLine("Literal Memory mismatch+three pairs costs1ticket, four attempts leaves Ready3 unpaid; explicit claim4D->7D once. Pause/Reload retains first card and instance; retained old claim/held pointer cannot clear or fund newer Sorting, fresh Submit still works.");
        }

        [UnityTest] public IEnumerator SortingEightWrongBinsResumeAndRequireExplicitZeroClaim()
        {
            // Two genuinely produced tickets also permit a separate newer run
            // after Finish, for the stale old-Finish identity challenge.
            var initial = Prepared(540, 2, spins:16); yield return Load(initial); yield return Home(); yield return GamePreview(4, "arcade.sorting");
            Quote("start", "arcade.sorting", tickets: 1); yield return Click(Primary);
            long instance = Session.Arcade.ActiveRun.InstanceSequence;
            // Independent initial-seed shuffle[2,1,1,0,3,2,0,3].
            // Literal wrong bins[3,2,2,1,0,3,1,0] give zero correct.
            var old = Card(3).onClick; var held = Down(Card(3), 731); yield return Click(Card(3), 732);
            Run("arcade.sorting", "sorting", 1, 1, 0); Assert.That(Session.Arcade.ActiveRun.CurrentSortingSymbol, Is.EqualTo(1));
            string before = State; var files = Files(); Release(held, Card(3)); old.Invoke(); Unchanged(before, files);
            Submit(Card(2)); yield return null; Run("arcade.sorting", "sorting", 2, 2, 0);
            before = State; files = Files(); yield return Back(); yield return Click(Card(4)); Unchanged(before, files);
            yield return PauseAndResume(); old.Invoke(); Unchanged(before, files);
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "A paused round must not trap Back over its hidden controls"); Unchanged(before, files);
            yield return Reload(); NoFlyers();
            yield return Home(); yield return Click(Card(4)); Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(instance));
            int[] symbols = { 1, 0, 3, 2, 0, 3 }, bins = { 2, 1, 0, 3, 1, 0 };
            for (int i = 0; i < bins.Length; i++)
            {
                Assert.That(Session.Arcade.ActiveRun.CurrentSortingSymbol, Is.EqualTo(symbols[i]));
                Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.Empty); ArcadeFits(); yield return Click(Card(bins[i]));
                if(i==3)
                {
                    Assert.That(Field<TMP_Text>("arcadeBody").text,Does.Contain("On completion: 0 diamonds · finish separately.").And.Not.Contain("claim separately"));
                    Assert.That(Primary.gameObject.activeInHierarchy,Is.False);
                    yield return PurchaseChoicePortrait.Capture(game,"sorting-fixed-zero-active","arcadeBody");
                }
            }
            Run("arcade.sorting", "ready", 8, 8, 0, 0); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2)); NoFlyers();
            before=State;files=Files();yield return BackToGame();yield return Click(Field<Button[]>("productTabs")[2]);
            Assert.That(Field<Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>().text,Does.Contain("Finish · 0 Diamonds").And.Not.Contain("To claim:"));
            Fits(Field<Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>());
            yield return PurchaseChoicePortrait.Capture(game,"sorting-events-zero","productContext");Unchanged(before,files);
            yield return Click(Field<Button[]>("productCards")[3]);ZeroReady();
            before = State; files = Files(); yield return Back(); yield return Click(Card(4)); Unchanged(before, files);
            ZeroReady();yield return PurchaseChoicePortrait.Capture(game,"sorting-zero-ready","arcadeBody");
            var claim = Quote("claim", "arcade.sorting"); Assert.That(Primary.GetComponentInChildren<TMP_Text>().text, Does.Contain("0 diamonds"));
            var oldClaim = Primary.onClick; Submit(Primary); yield return null;
            Assert.That(Session.Arcade.ActiveRun, Is.Null); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(claim.Key));
            Assert.That(Session.Arcade.LastClaim.Diamonds, Is.Zero); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2)); NoFlyers();
            Assert.That(Primary.gameObject.activeSelf && Primary.interactable, Is.False, "A zero claim must not create the positive-reward continuation");
            ZeroFinished();yield return PurchaseChoicePortrait.Capture(game,"sorting-zero-finished","arcadeBody");
            before = State; files = Files(); oldClaim.Invoke(); Unchanged(before, files); yield return Reload(); Unchanged(before, files); NoFlyers();
            OldGoal(540, initial.coins); LowerUnchanged(initial, 10);
            yield return Home();Assert.That(Card(4).GetComponentInChildren<TMP_Text>().text,Does.Contain("Previous round finished without Diamonds.").And.Not.Contain("Previously received: 0"));ArcadeFits();Unchanged(before,files);
            yield return PurchaseChoicePortrait.Capture(game,"sorting-zero-history","arcadeTitle");
            yield return GamePreview(4,"arcade.sorting");yield return Click(Primary);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.GreaterThan(instance));Assert.That(Session.Arcade.Tickets,Is.Zero);
            before=State;files=Files();oldClaim.Invoke();Unchanged(before,files);Assert.That(Session.Arcade.ActiveRun.Ready,Is.False);NoFlyers();
            TestContext.WriteLine("Sorting8items/4named bins, all eight literal wrong placements ->Ready0 unpaid, explicit zero claim clears once; stale card/held contact cannot consume another item; Reload resumes same cursor.");
        }
        void ZeroReady()
        {
            Assert.That(Field<TMP_Text>("arcadeResultCaption").text,Is.EqualTo("ROUND COMPLETE"));
            Assert.That(Field<TMP_Text>("arcadeResultValue").text,Is.EqualTo("No Diamonds earned"));
            Assert.That(Field<TMP_Text>("arcadeBody").text,Does.Contain("No Diamonds were earned.").And.Not.Contain("Ready to claim:"));
            Assert.That(Primary.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Finish · 0 diamonds"));ArcadeFits();NoFlyers();
        }
        void ZeroFinished()
        {
            Assert.That(Field<TMP_Text>("arcadeResultCaption").text,Is.EqualTo("ROUND FINISHED"));
            Assert.That(Field<TMP_Text>("arcadeResultValue").text,Is.EqualTo("No Diamonds earned"));
            Assert.That(Field<TMP_Text>("arcadeBody").text,Does.Contain("Round finished. No Diamonds earned.").And.Not.Contain("Credited: 0"));ArcadeFits();NoFlyers();
        }
        [UnityTest] public IEnumerator MemoryZeroUsesFinishAcrossSavedResultAndStaticHistory()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/ArcadeCore/Evidence/memory-zero-ready.json"));
            var initial=codec.Decode(File.ReadAllText(path));GameLogic.Validate(initial);yield return Load(initial);
            var run=Session.Arcade.ActiveRun;Assert.That(run.Id,Is.EqualTo("arcade.memory"));Assert.That(run.Attempts,Is.EqualTo(9));
            Assert.That(run.MatchedPairs,Is.LessThan(3));Assert.That(run.ReadyReward,Is.Zero);long instance=run.InstanceSequence;
            long diamonds=Session.Arcade.Diamonds;int tickets=Session.Arcade.Tickets;string before=State;var files=Files();
            yield return Click(Field<Button[]>("productTabs")[2]);Assert.That(Field<Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>().text,Does.Contain("Finish · 0 Diamonds"));
            yield return Click(Field<Button[]>("productCards")[3]);ZeroReady();Unchanged(before,files);
            yield return PurchaseChoicePortrait.Capture(game,"memory-zero-ready","arcadeBody");
            yield return Back();Assert.That(Card(3).GetComponentInChildren<TMP_Text>().text,Does.Contain("Finish · 0 diamonds"));
            yield return PurchaseChoicePortrait.Capture(game,"memory-zero-saved-home","arcadeTitle");Unchanged(before,files);
            yield return Click(Card(3));var q=Quote("claim","arcade.memory");var stale=Primary.onClick;yield return Click(Primary);
            Assert.That(Session.Arcade.ActiveRun,Is.Null);Assert.That(Session.Arcade.LastClaim.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Arcade.LastClaim.Key,Is.EqualTo(q.Key));
            Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds));Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets));
            Assert.That(Primary.gameObject.activeInHierarchy,Is.False);ZeroFinished();yield return PurchaseChoicePortrait.Capture(game,"memory-zero-finished","arcadeBody");
            before=State;files=Files();stale.Invoke();Unchanged(before,files);yield return Reload();Unchanged(before,files);NoFlyers();
            yield return Home();Assert.That(Card(3).GetComponentInChildren<TMP_Text>().text,Does.Contain("Previous round finished without Diamonds.").And.Not.Contain("Previously received: 0"));
            ArcadeFits();yield return PurchaseChoicePortrait.Capture(game,"memory-zero-history","arcadeTitle");Unchanged(before,files);
        }

        [UnityTest] public IEnumerator PacksRequireFullHeadroomAndDiamondBoostSharesChargeWithoutTicketOverflowBank()
        {
            var initial = Prepared(600, 5, 196, 195, bonus: true, spins: 31); yield return Load(initial);
            Assert.That(Session.Arcade.Tickets, Is.EqualTo(3)); Assert.That(Session.Arcade.TicketProgress, Is.EqualTo(7));
            yield return Home(); string before = State; var files = Files(); yield return Offer(1, 0);
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Primary.interactable, Is.False);
            Assert.That(Field<TMP_Text>("arcadeBody").text, Does.Contain("Enough space is needed for the entire purchase."));
            Primary.onClick.Invoke(); Unchanged(before, files); NoFlyers();
            yield return Back(); yield return Click(Card(1)); Quote("buy", "arcade.pack.potatoes", 1);
            var oldPack = Primary.onClick; yield return Click(Primary);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(4)); Assert.That(Session.Expansion.Potatoes, Is.EqualTo(200));
            Assert.That(Session.Expansion.Parts, Is.EqualTo(196)); Assert.That(Flyers(), Is.EqualTo(new[] { "Potatoes +5" }));
            FeedbackDoesNotIntercept(Field<Button>("closeButton")); LowerUnchanged(initial, 1);
            before = State; files = Files(); oldPack.Invoke(); Unchanged(before, files); yield return FeedbackEnds(); Unchanged(before, files);
            yield return Back(); yield return Offer(2, 0); Quote("buy", "arcade.boost.next-bonus", 1);
            var energyOffer = Session.GetExpansionAction("boost", "boost.next-bonus"); Assert.That(energyOffer.EnergyCost, Is.EqualTo(15));
            var oldBoost = Primary.onClick; Submit(Primary); yield return null;
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Expansion.Energy, Is.EqualTo(30));
            Assert.That(Session.Expansion.BoostArmed, Is.True); LowerUnchanged(initial, 2); NoFlyers();
            before = State; files = Files();
            Assert.Throws<InvalidOperationException>(() => Session.ExecuteExpansion(energyOffer, 1000));
            oldBoost.Invoke(); Unchanged(before, files);
            yield return Back(); yield return Offer(2, 0);
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Primary.interactable, Is.False);
            Assert.That(Field<TMP_Text>("arcadeBody").text, Does.Contain("Boost already active."));
            Primary.onClick.Invoke(); Unchanged(before, files); yield return BackToGame();
            yield return Click(Field<Button>("spinButton")); NoFlyers(); yield return Finish();
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 3, 1, 1 }));
            Assert.That(Session.State.seed, Is.EqualTo(1511609050u));
            Assert.That(Session.Arcade.Tickets, Is.EqualTo(3)); Assert.That(Session.Arcade.TicketProgress, Is.Zero);
            Assert.That(Session.Arcade.LastEvent.TicketsAdded, Is.Zero); Assert.That(Flyers(), Does.Not.Contain("Tickets +1"));
            Assert.That(Session.Expansion.Potatoes, Is.EqualTo(200)); Assert.That(Session.Expansion.Energy, Is.EqualTo(30));
            Assert.That(Session.Arcade.LastEvent.PotatoesAdded, Is.Zero); Assert.That(Session.Arcade.LastEvent.EnergyAdded, Is.Zero);
            Assert.That(Session.Expansion.BoostArmed, Is.True, "Only the future resource bonus consumes the shared charge");
            OldGoal(600, initial.coins + 8); var afterSpin = Session.State;
            yield return Home(); yield return GamePreview(4, "arcade.sorting"); yield return Click(Primary);
            Assert.That(Session.Arcade.Tickets, Is.EqualTo(2)); Assert.That(Session.Arcade.TicketProgress, Is.Zero);
            Assert.That(Session.Arcade.ForegroundSpins, Is.EqualTo(32)); LowerUnchanged(afterSpin, 1);
            TestContext.WriteLine("4units Parts headroom refuses whole5pack0IO; exact5Potatoes costs1D. Diamond boost costs1D/0Energy and invalidates retained15Energy offer; armed repeat refuses.32nd FG cycle at ticket cap discards overflow, then start spends3->2 with progress0/no bank.");
        }

        [UnityTest] public IEnumerator CoinFlyerUsesGrossIncomeAfterSaved600AutoDebitAndClearsAcrossLifecycle()
        {
            var setup = Fixture(LegacySelected(600)); setup.SelectExpansionSpeed("speed.fast", setup.State.sequence, 1000);
            var initial = setup.State; var f = ExpansionCodec.EncodeState(initial.expansion).Split('|');
            Assert.That(f.Length, Is.EqualTo(21)); f[1] = "200"; f[2] = "200"; f[3] = "30";
            initial.expansion = ExpansionCodec.DecodeState(string.Join("|", f)); GameLogic.Validate(initial);
            yield return Load(initial); NoFlyers(); yield return Click(Field<Button>("spinButton"));
            Assert.That(Flag("Busy"), Is.True); NoFlyers();
            Assert.That(Session.State.coins, Is.EqualTo(3)); Assert.That(Session.State.houseStage, Is.EqualTo(2));
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.Arcade.LastEvent.CoinIncome, Is.EqualTo(8)); Assert.That(Session.Arcade.LastEvent.ConstructionDebit, Is.EqualTo(600));
            Assert.That(new[] { Session.Arcade.LastEvent.PartsAdded, Session.Arcade.LastEvent.PotatoesAdded, Session.Arcade.LastEvent.EnergyAdded,
                Session.Arcade.LastEvent.TicketsAdded, Session.Arcade.LastEvent.DiamondsAdded }, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
            yield return Finish(); Assert.That(Flyers(), Is.EqualTo(new[] { "Coins +8" })); FeedbackDoesNotIntercept(Field<Button>("spinButton"));
            Assert.That(Field<TMP_Text>("productCoins").text, Does.StartWith("0\n"), "Same-spin construction debits cannot expose a negative wallet");
            string before = State; var files = Files(); var pool = Field<TMP_Text[]>("rewardFeedbackLabels").Select(t => t.GetInstanceID()).ToArray();
            Call("Render", null, null); Call("RevealRewardFeedback", Session.State); Call("Render", null, null);
            Assert.That(Flyers(), Is.EqualTo(new[] { "Coins +8" }));
            Assert.That(Field<TMP_Text[]>("rewardFeedbackLabels").Select(t => t.GetInstanceID()), Is.EqualTo(pool)); Unchanged(before, files);
            yield return PauseAndResume(); NoFlyers(); Unchanged(before, files); yield return Reload(); NoFlyers(); Unchanged(before, files);
            Assert.That(Field<TMP_Text>("productCoins").text, Does.StartWith("3\n"), "Cancellation and reload restore the exact committed wallet");
            yield return Click(Field<Button>("productSettings")); yield return Click(Field<Button[]>("productCards")[0]);
            Assert.That((bool)gameType.GetProperty("RewardFeedbackReduced").GetValue(game), Is.True); Unchanged(before, files);
            yield return Click(Field<Button>("closeButton")); Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            yield return Click(Field<Button>("spinButton")); Assert.That(Flag("Busy"), Is.True); NoFlyers();
            yield return Finish(); NoFlyers(); FeedbackDoesNotIntercept(Field<Button>("spinButton"));
            Assert.That(Session.State.sequence, Is.EqualTo(initial.sequence + 2));
            TestContext.WriteLine("First primary pair earns8 while frozen old600 goal debits600:595->3, one +8Coin flyer only after normal reveal, all capped resources add0. Re-render cannot duplicate; pause/Reload clear with0IO; session reduced preference costs0IO and suppresses next optional effect.");
        }

        [UnityTest] public IEnumerator GenuinePendingMemoryClaimRecoversOnceWithoutFlyersOrClearingNewRun()
        {
            var setup = Fixture(Prepared(600, 7, spins: 16));
            var start = setup.GetArcadeQuote("start", "arcade.memory", setup.Arcade.Sequence); setup.ExecuteArcade(start, 1000);
            long instance = setup.Arcade.ActiveRun.InstanceSequence;
            foreach (int card in new[] { 0, 5, 1, 2, 3, 4 })
                setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, instance, card), 1000);
            Assert.That(setup.Arcade.ActiveRun.Attempts, Is.EqualTo(3)); Assert.That(setup.Arcade.ActiveRun.ReadyReward, Is.EqualTo(3));
            Assert.That(setup.Arcade.Diamonds, Is.EqualTo(7));
            var claim = setup.GetArcadeQuote("claim", "arcade.memory", setup.Arcade.Sequence, instance);
            var pending = ArcadeEconomy.Reserve(setup.State, claim, 1000);
            Assert.That(pending.pending, Is.Not.Null); Assert.That(pending.sequence, Is.EqualTo(claim.ExpectedSequence));
            // Public reservation produces a genuine frozen pending descriptor.
            // No fake After/result, torn write or storage fault is injected.
            yield return Load(pending);
            Assert.That(Session.State.sequence, Is.EqualTo(claim.Sequence)); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(10));
            Assert.That(Session.Arcade.ActiveRun, Is.Null); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(claim.Key));
            Assert.That(Session.Arcade.LastClaim.InstanceSequence, Is.EqualTo(instance)); NoFlyers(); Saved();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Recovery must not synthesize a fresh claim result or continuation");
            string before = State; var files = Files(); yield return Reload(); Unchanged(before, files); NoFlyers();
            Session.ExecuteArcade(claim, 1000); Unchanged(before, files); NoFlyers();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Exact replay must not open a claim continuation");
            yield return Home(); yield return GamePreview(4, "arcade.sorting"); yield return Click(Primary);
            Assert.That(Session.Arcade.Tickets, Is.Zero); Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.GreaterThan(instance));
            Assert.That(Session.Arcade.ActiveRun.CurrentSortingSymbol, Is.EqualTo(2));
            before = State; files = Files(); Session.ExecuteArcade(claim, 1000); Session.ExecuteArcade(claim, 1000);
            Unchanged(before, files); NoFlyers(); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(10)); OldGoal(600, pending.coins);
            TestContext.WriteLine("Genuine public reserve of Ready3 claim recovers7D->10D once with no reveal/flyer. Reload and exact retained claim replay0IO; same old claim against a newer Sorting run preserves that instance/ticket debit and cannot clear/reward it.");
        }
        static string Caption(Button button) => button.GetComponentInChildren<TMP_Text>(true).text;
        string ArcadeBody => Field<TMP_Text>("arcadeBody").text;
        void PolishView()
        {
            ArcadeFits();
            foreach (var button in Field<RectTransform>("arcadeRoot").GetComponentsInChildren<Button>())
            {
                var rect = ((RectTransform)button.transform).rect;
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name);
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name);
                if (button.isActiveAndEnabled && button.IsInteractable()) Target(button);
            }
            Target(Field<Button>("closeButton"));
        }
        static void Palette(Button button, string hex)
        {
            Assert.That(ColorUtility.TryParseHtmlString("#" + hex, out var expected), Is.True);
            Assert.That(button.targetGraphic.color, Is.EqualTo(expected), button.name);
        }
        static void OfferAccent(Button button, string hex)
        {
            Palette(button, "263C50");
            var marker = button.transform.Find("Arcade Card Icon").GetComponent<Graphic>();
            Assert.That(marker.isActiveAndEnabled, Is.True);
            Assert.That(marker.raycastTarget, Is.False);
            Assert.That(ColorUtility.TryParseHtmlString("#" + hex, out var expected), Is.True);
            Assert.That(marker.color, Is.EqualTo(expected), button.name);
        }
        IEnumerator ReplacePrivateFixture(ModelState state)
        {
            Assert.That(Flag("Busy") || Flag("AutoRunning"), Is.False);
            GameLogic.Validate(state); File.WriteAllText(Path.Combine(folder, "jtn.save.v1.json"), codec.Encode(state));
            yield return Reload();
        }

        [UnityTest, Category("ArcadePolish10h")]
        public IEnumerator ShopClarityKeepsTicketCyclesAndFreshConfirmationAcrossCategoryReturn()
        {
            var zero = LegacySelected(540); zero.coins = 533; yield return Load(zero);
            string before = State; var files = Files(); yield return Home(); PolishView();
            Assert.That(Field<TMP_Text>("arcadeResources").text, Is.EqualTo(
                "Diamonds: 0 · Tickets: 0/3\nParts: 0/200 · Potatoes: 0/200\nCycle: 0/8 · 8 new spins left\nLimit 3: extra tickets do not accumulate."));
            Assert.That(Caption(Card(0)), Is.EqualTo("Slot 0/3 · next 1\n2 diamonds + 20 parts\nMissing: 2 diamonds, 20 parts"));
            Assert.That(Caption(Card(3)), Does.Contain("No ticket: 8 spins left"));
            yield return Click(Secondary); PolishView(); Assert.That(ArcadeBody, Does.Contain("Tickets: 0/3. New spins until a Ticket: 8."));
            Assert.That(ArcadeBody, Does.Contain("Slot level: 0/3. Current effects:").And.Contain("Parts: +0 once per spin with a Box.").And.Contain("Next Level: Slot Level I."));
            Unchanged(before, files); yield return Back();
            yield return Click(Card(3)); PolishView(); Assert.That(Primary.interactable, Is.False);
            Assert.That(Caption(Secondary), Is.EqualTo("To game")); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            yield return Click(Secondary); Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            Unchanged(before, files); OldGoal(540, 533); Assert.That(Session.State.seed, Is.EqualTo(1u));
            Assert.That(Session.Arcade.ForegroundSpins, Is.Zero, "The no-ticket shortcut must not Spin");
            yield return Home(); yield return Click(Card(3)); yield return Back();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); PolishView(); Unchanged(before, files);

            // A separate declared initial wallet plus three genuine foreground
            // commands produce the partial cycle; no ticket/progress is set.
            var partial = LegacySelected(600); partial.coins = 0; var setup = Fixture(partial);
            for (int i = 0; i < 3; i++) setup.ExecuteChapterSpin(setup.State.sequence + 1, 1000);
            Assert.That(setup.State.coins, Is.EqualTo(24)); Assert.That(setup.State.seed, Is.EqualTo(2005365029u));
            yield return ReplacePrivateFixture(setup.State); before = State; files = Files(); yield return Home(); PolishView();
            Assert.That(Field<TMP_Text>("arcadeResources").text, Is.EqualTo(
                "Diamonds: 0 · Tickets: 0/3\nParts: 3/200 · Potatoes: 6/200\nCycle: 3/8 · 5 new spins left\nLimit 3: extra tickets do not accumulate."));
            Assert.That(Caption(Card(4)), Does.Contain("No ticket: 5 spins left"));
            yield return Click(Card(4)); PolishView(); Assert.That(Caption(Secondary), Is.EqualTo("To game"));
            yield return Click(Secondary); Unchanged(before, files); OldGoal(600, 24);

            var initial = Prepared(540, 5, 196, 195, bonus: true, spins: 31);
            yield return ReplacePrivateFixture(initial); before = State; files = Files(); yield return Home(); PolishView();
            Assert.That(Field<TMP_Text>("arcadeResources").text, Does.Contain("Tickets: 3/3").And.Contain("Cycle: 7/8 · 1 new spins left").And.Contain("Storage full: the cycle ticket will not be added."));
            Assert.That(Caption(Card(3)), Does.Contain("Ready to start"));
            yield return Click(Card(1)); PolishView();
            Assert.That(Caption(Card(0)), Does.Contain("Parts space: 4/5")); OfferAccent(Card(0), "D7E0C5");
            Assert.That(Caption(Card(1)), Does.Contain("Available to buy")); OfferAccent(Card(1), "F4B85C");
            yield return Click(Card(0)); PolishView(); Assert.That(Primary.interactable, Is.False);
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Unchanged(before, files);
            yield return Click(Secondary); yield return Back(); yield return Click(Card(0)); PolishView();
            Assert.That(Caption(Card(0)), Does.Contain("Available to buy")); OfferAccent(Card(0), "F4B85C");
            Assert.That(Caption(Card(1)), Does.Contain("Level 1 required first")); OfferAccent(Card(1), "D7E0C5");
            yield return Click(Card(0)); PolishView(); Quote("buy", "arcade.slot.1", 2, 20);
            var oldConfirm = Primary.onClick; var held = Down(Primary, 811); yield return Click(Primary, 812);
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts }, Is.EqualTo(new long[] { 3, 176 }));
            Assert.That(Caption(Secondary), Is.EqualTo("To category")); PolishView(); LowerUnchanged(initial, 1);
            before = State; files = Files(); yield return Click(Secondary);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(61)); Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Slot levels"));
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); PolishView(); Unchanged(before, files);
            Assert.That(Caption(Card(0)), Does.Contain("Already unlocked · inspect effects").And.Not.Contain("2 diamonds").And.Not.Contain("20 parts")); OfferAccent(Card(0), "B9CCBB");
            Assert.That(Caption(Card(1)), Does.Contain("Missing: 1 diamonds"));
            yield return Click(Card(0)); PolishView(); Assert.That(Primary.interactable, Is.False);
            Assert.That(ArcadeBody, Does.Contain("Current: level 1/3.").And.Contain("Box: +1 parts; potato: +0 potatoes.").And.Not.Contain("Cost:").And.Not.Contain("Not enough"));
            Unchanged(before, files); yield return Click(Secondary); yield return Click(Secondary); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(65)); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            Assert.That(ArcadeBody, Does.Contain("Slot level: 1/3. Current effects:").And.Contain("Parts: +1 once per spin with a Box.").And.Contain("Potatoes: +0 once per spin with a Potato.").And.Contain("Base bonus: +0 parts, +0 potatoes.").And.Contain("Next Level: Slot Level II."));
            Unchanged(before, files); yield return Back(); yield return Offer(1, 0); PolishView();
            var pack = Quote("buy", "arcade.pack.parts", 1);
            Assert.That(ArcadeBody, Does.Contain("After purchase: 2 diamonds. Level 2: 4 diamonds."));
            Release(held, Primary); oldConfirm.Invoke(); Unchanged(before, files);
            Assert.That(Field<ArcadeQuote>("arcadeQuote").Key, Is.EqualTo(pack.Key), "The affordable new offer still requires its own fresh confirmation");
            yield return Click(Primary, 813);
            Assert.That(new[] { Session.Arcade.Diamonds, Session.Expansion.Parts }, Is.EqualTo(new long[] { 2, 181 })); LowerUnchanged(initial, 2);
            before = State; files = Files(); Assert.That(Caption(Secondary), Is.EqualTo("To category")); yield return Click(Secondary);
            Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Diamond shop")); PolishView(); Unchanged(before, files);
            yield return Back(); yield return Offer(2, 0); PolishView(); Quote("buy", "arcade.boost.next-bonus", 1);
            Assert.That(ArcadeBody, Does.Contain("After purchase: 1 diamonds. Level 2: 4 diamonds.").And.Contain("Energy: 30/15 · a separate boost is available without diamonds.").And.Contain("Until bonus: 5 spins."));
            yield return Click(Primary, 814); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(1)); Assert.That(Session.Expansion.BoostArmed, Is.True);
            Assert.That(Session.Expansion.Energy, Is.EqualTo(30)); LowerUnchanged(initial, 3);
            before = State; files = Files(); yield return Click(Secondary); PolishView();
            Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Bonus boost")); Assert.That(Caption(Card(0)), Does.Contain("Boost already active"));
            yield return Click(Card(0)); PolishView(); Assert.That(Primary.interactable, Is.False); Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null);
            Unchanged(before, files); OldGoal(540, 218); Assert.That(Session.State.seed, Is.EqualTo(initial.seed));
            TestContext.WriteLine("Clarity:0/8 and3/8 empty tickets return directly to Game without Spin; old533/540 and600 goal terms survive. Cap3/progress7 names discard; owned/next/headroom/shortfall/shared-boost facts are visible. Result secondary browses same category0IO; old held confirmation cannot buy an affordable new pack; fresh purchase is separate.");
        }

        [UnityTest, Category("ArcadePolish10h")]
        public IEnumerator RoundClarityUsesMaskedFacesReachableRewardsAndTransientSortingFeedback()
        {
            var setup = Fixture(Prepared(600));
            setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.memory", setup.Arcade.Sequence), 1000);
            long memoryId = setup.Arcade.ActiveRun.InstanceSequence;
            for (int miss = 0; miss < 3; miss++)
            {
                setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, memoryId, 0), 1000);
                setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, memoryId, 1), 1000);
                setup.ExecuteArcade(setup.GetArcadeQuote("continue", "arcade.memory", setup.Arcade.Sequence, memoryId), 1000);
            }
            foreach (int card in new[] { 0, 5 }) setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, memoryId, card), 1000);
            var memory = setup.State; yield return Load(memory); string before = State; var files = Files(); yield return Home();
            Assert.That(Caption(Card(3)), Does.Contain("Continue round").And.Contain("Pairs: 1/3 · attempts: 4/9"));
            yield return Click(Card(3)); PolishView(); Run("arcade.memory", "first", 11, 4, 1);
            Assert.That(ArcadeBody, Is.EqualTo("Pairs: 1/3 · attempts: 4/9\nTurn over a card.\nFor all pairs: up to 2 diamonds; otherwise 0."));
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, -1, -1, -1, -1, 1 }));
            Assert.That(Caption(Card(0)), Is.EqualTo("1. Box · pair")); Assert.That(Card(0).interactable, Is.False); Palette(Card(0), "B9CCBB");
            Assert.That(Caption(Card(1)), Is.EqualTo("2. Face down")); Palette(Card(1), "ABD2D8"); Unchanged(before, files);
            var symbols = Field<Sprite[]>("reelSymbols");
            Assert.That(Card(0).transform.Find("Arcade Card Symbol").GetComponent<Image>().sprite, Is.SameAs(symbols[1]));
            var closedImage = Card(1).transform.Find("Arcade Card Symbol").GetComponent<Image>();
            Assert.That(closedImage.sprite, Is.Null); Assert.That(closedImage.isActiveAndEnabled, Is.False);
            Assert.That(Card(1).transform.Find("Arcade Card Icon").gameObject.activeInHierarchy, Is.True);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/inner-ui-20261101/root/memory-round-tested.png"))); yield return null;
            yield return Click(Card(1)); PolishView(); Run("arcade.memory", "second", 12, 4, 1);
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, 2, -1, -1, -1, 1 }));
            Assert.That(Caption(Card(1)), Is.EqualTo("2. Trash · face up")); Assert.That(Card(1).interactable, Is.False); Palette(Card(1), "F4B85C");
            var openImage = Card(1).transform.Find("Arcade Card Symbol").GetComponent<Image>();
            Assert.That(openImage.sprite, Is.SameAs(symbols[2])); Assert.That(openImage.isActiveAndEnabled, Is.True); Assert.That(openImage.raycastTarget, Is.False);
            Assert.That(Card(1).transform.Find("Arcade Card Icon").gameObject.activeInHierarchy, Is.False);
            Assert.That(Card(1).colors.disabledColor, Is.EqualTo(Color.white)); Assert.That(Caption(Card(2)), Is.EqualTo("3. Face down"));
            before = State; files = Files(); yield return Back(); yield return Click(Card(3)); PolishView(); Unchanged(before, files);
            yield return Reload(); Unchanged(before, files); yield return Home(); yield return Click(Card(3)); PolishView(); Unchanged(before, files);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(memoryId)); Assert.That(Session.State.seed, Is.EqualTo(memory.seed));
            yield return Click(Card(2)); PolishView(); Run("arcade.memory", "first", 13, 5, 2);
            Assert.That(ArcadeBody, Is.EqualTo("Pairs: 2/3 · attempts: 5/9\nTurn over a card.\nFor all pairs: up to 2 diamonds; otherwise 0."));
            Assert.That(Caption(Card(2)), Is.EqualTo("3. Trash · pair")); Palette(Card(2), "B9CCBB");
            Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.EqualTo(new[] { 1, 2, 2, -1, -1, 1 }));
            Assert.That(Session.Arcade.Diamonds, Is.Zero); NoFlyers(); yield return Click(Card(3)); PolishView();
            Assert.That(Caption(Card(3)), Is.EqualTo("4. Potato · face up")); Assert.That(Caption(Card(4)), Is.EqualTo("5. Face down"));
            yield return Click(Card(4)); PolishView(); Run("arcade.memory", "ready", 15, 6, 3, 2);
            Assert.That(Session.Arcade.Diamonds, Is.Zero); NoFlyers(); Quote("claim", "arcade.memory", reward: 2);
            yield return Click(Primary); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2)); Assert.That(Session.Arcade.LastClaim.Diamonds, Is.EqualTo(2));
            LowerUnchanged(memory, 5); OldGoal(600, memory.coins);

            setup = Fixture(Prepared(540)); setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.sorting", setup.Arcade.Sequence), 1000);
            long sortingId = setup.Arcade.ActiveRun.InstanceSequence;
            // Literal first queue[2,1,1,0,3,2,0,3]; no future item is read.
            foreach (int bin in new[] { 2, 1, 1, 0, 3 }) setup.ExecuteArcade(setup.GetArcadeQuote("sort", "arcade.sorting", setup.Arcade.Sequence, sortingId, bin), 1000);
            var sorting = setup.State; yield return ReplacePrivateFixture(sorting); NoFlyers(); before = State; files = Files(); yield return Home();
            Assert.That(Caption(Card(4)), Does.Contain("Continue round").And.Contain("Completed: 5/8 · correct: 5"));
            yield return Click(Card(4)); PolishView(); Unchanged(before, files); Assert.That(Session.Arcade.ActiveRun.CurrentSortingSymbol, Is.EqualTo(2));
            Assert.That(new[] { Caption(Card(0)), Caption(Card(1)), Caption(Card(2)), Caption(Card(3)) }, Is.EqualTo(new[] { "Potato", "Box", "Trash", "Pigeon" }));
            for (int bin = 0; bin < 4; bin++)
            {
                var binImage = Card(bin).transform.Find("Arcade Card Symbol").GetComponent<Image>();
                Assert.That(binImage.sprite, Is.SameAs(symbols[bin])); Assert.That(binImage.isActiveAndEnabled, Is.True); Assert.That(binImage.raycastTarget, Is.False);
            }
            Assert.That(Field<RectTransform>("arcadeItemBand").Find("Arcade Item Symbol").GetComponent<Image>().sprite, Is.SameAs(symbols[2]));
            yield return Click(Card(2)); PolishView(); Run("arcade.sorting", "sorting", 6, 6, 6);
            Assert.That(ArcadeBody, Is.EqualTo("Item 7/8 · correct: 6\nNow: Potato\nOn completion: 2-3 diamonds; 1 more correct for 3.\nCorrect: score +1."));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/inner-ui-20261101/root/sorting-round-tested.png"))); yield return null;
            Assert.That(Session.Arcade.Diamonds, Is.Zero); Assert.That(Session.Arcade.ActiveRun.MemoryFaces, Is.Empty); NoFlyers();
            before = State; files = Files(); yield return Back(); yield return Click(Card(4)); PolishView(); Unchanged(before, files);
            Assert.That(ArcadeBody, Is.EqualTo("Item 7/8 · correct: 6\nNow: Potato\nOn completion: 2-3 diamonds; 1 more correct for 3."));
            yield return Click(Card(1)); PolishView(); Run("arcade.sorting", "sorting", 7, 7, 6);
            Assert.That(ArcadeBody, Is.EqualTo("Item 8/8 · correct: 6\nNow: Pigeon\nOn completion: 2-3 diamonds; 1 more correct for 3.\nIncorrect: item passed without a point."));
            before = State; files = Files(); yield return Reload(); Unchanged(before, files); yield return Home(); yield return Click(Card(4)); PolishView(); Unchanged(before, files);
            Assert.That(ArcadeBody, Is.EqualTo("Item 8/8 · correct: 6\nNow: Pigeon\nOn completion: 2-3 diamonds; 1 more correct for 3."));
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(sortingId)); Assert.That(Session.State.seed, Is.EqualTo(sorting.seed));
            yield return Click(Card(3)); PolishView(); Run("arcade.sorting", "ready", 8, 8, 7, 3);
            Assert.That(ArcadeBody, Does.Contain("Ready to claim: 3 diamonds").And.Contain("Correct: score +1.")); Assert.That(Session.Arcade.Diamonds, Is.Zero); NoFlyers();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/inner-ui-20261101/root/sorting-ready-tested.png"))); yield return null;
            before = State; files = Files(); yield return PauseAndResume(); Unchanged(before, files);
            Assert.That(Field<RectTransform>("arcadeRoot").gameObject.activeInHierarchy, Is.False); yield return Back();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False); yield return Home(); PolishView(); Unchanged(before, files);
            Assert.That(Caption(Card(4)), Does.Contain("Ready to claim: 3 diamonds").And.Contain("Confirm result").And.Contain("Correct: 7/8"));
            yield return Click(Card(4)); PolishView(); Unchanged(before, files);
            Assert.That(ArcadeBody, Does.Not.Contain("Correct: score +1.").And.Not.Contain("Incorrect: item passed without a point."));
            Quote("claim", "arcade.sorting", reward: 3); yield return Click(Primary);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Arcade.LastClaim.Diamonds, Is.EqualTo(3));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/inner-ui-20261101/root/sorting-claimed-tested.png"))); yield return null;
            LowerUnchanged(sorting, 4); OldGoal(540, sorting.coins);
            TestContext.WriteLine("Masked Memory matched/open/closed states persist without reroll; four/five used attempts honestly show best2, sixth complete attempt leaves Ready2 unpaid. Sorting6correct/2remaining names conditional2-3 and1needed; fresh correct/wrong is transient, Back/Reload/pause remove it without consuming the run, last correct reaches Ready3 and only explicit claim credits3.");
        }

        void WholeLoopSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/whole-loop-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),State,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"message.txt"),Field<TMP_Text>("message").text,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"event-badge.txt"),Field<TMP_Text>("productEventBadge").text,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"expansion-body.txt"),Field<TMP_Text>("expansionBody").text,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; sequence="+Session.State.sequence+"; Busy="+Flag("Busy")+". UI text is from this live checkpoint, not recreated by loading its save.",new UTF8Encoding(false));
            TestContext.WriteLine("Private whole-loop snapshot: "+target);
        }
        [UnityTest]
        public IEnumerator WholeLoopReadyResultsKeepActualSpinReceiptAfterConstructionReveal()
        {
            foreach(bool positive in new[]{true,false})
            {
                // Declared synthetic opening wallet; eight real seed1 spins earn
                // 59 Coins and one ticket without reaching the saved600 price.
                var seed=LegacySelected(600);seed.coins=538;var setup=Fixture(seed);
                for(int i=0;i<8;i++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
                Assert.That(setup.State.coins,Is.EqualTo(597));Assert.That(setup.Arcade.Tickets,Is.EqualTo(1));
                setup.SelectExpansionSpeed("speed.fast",setup.State.sequence,1000);
                string gameId=positive?"arcade.memory":"arcade.sorting";
                setup.ExecuteArcade(setup.GetArcadeQuote("start",gameId,setup.Arcade.Sequence),1000);
                long instance=setup.Arcade.ActiveRun.InstanceSequence;
                if(positive)
                    foreach(int card in new[]{0,5,1,2,3,4})setup.ExecuteArcade(setup.GetArcadeQuote("pick",gameId,setup.Arcade.Sequence,instance,card),1000);
                else
                    foreach(int bin in new[]{3,2,2,1,0,3,1,0})setup.ExecuteArcade(setup.GetArcadeQuote("sort",gameId,setup.Arcade.Sequence,instance,bin),1000);
                Assert.That(setup.Arcade.ActiveRun.Ready,Is.True);Assert.That(setup.Arcade.ActiveRun.ReadyReward,Is.EqualTo(positive?3:0));
                if(game==null)yield return Load(setup.State);else yield return ReplacePrivateFixture(setup.State);
                Assert.That(Session.State.coins,Is.EqualTo(597));Assert.That(Session.Arcade.Diamonds,Is.Zero);
                WholeLoopSnapshot(positive?"ready3-before-spin":"ready0-before-spin","Synthetic starting wallet538, historical selected600; eight actual seed1 Core spins earn59 and ticket, public mini-game commands leave saved Ready unpaid");
                yield return Click(Field<Button>("spinButton"));Assert.That(Flag("Busy"),Is.True);
                Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,1,2}));Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(3));
                Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.EqualTo(600));Assert.That(Session.State.coins,Is.Zero);
                Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Arcade.Diamonds,Is.Zero);
                yield return Finish();
                var message=Field<TMP_Text>("message");Assert.That(message.isActiveAndEnabled&&message.gameObject.activeInHierarchy,Is.True);
                Assert.That(message.text,Does.Contain("+3").And.Contain("600"));Fits(message);
                var badge=Field<TMP_Text>("productEventBadge");Assert.That(badge.isActiveAndEnabled&&badge.gameObject.activeInHierarchy,Is.True);Fits(badge);
                string messageCue=message.text.ToUpperInvariant(),badgeCue=badge.text.ToUpperInvariant();
                Assert.That(messageCue.Contains("RESULT READY")||badgeCue.Contains("READY")||badgeCue.Contains("CLAIM")||badgeCue.Contains("FINISH"),Is.True,"Saved Ready needs a separate visible cue");
                Assert.That(badgeCue,positive?Does.Contain("CLAIM").And.Contain("3"):Does.Contain("FINISH").And.Contain("0"),"Visible Ready action must name the actual result");
                Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Arcade.Diamonds,Is.Zero);
                WholeLoopSnapshot(positive?"ready3-revealed":"ready0-revealed","Actual UI spin9 reveals independent +3 income and historical600 debit; same Ready instance still unpaid");
                string before=State;var files=Files();yield return Click(Field<Button[]>("productTabs")[2]);
                Assert.That(Caption(Field<Button[]>("productCards")[3]),positive?Does.Contain("Result ready").And.Contain("3 Diamonds"):Does.Contain("Finish · 0 Diamonds"));
                yield return Click(Field<Button[]>("productCards")[3]);Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(instance));Unchanged(before,files);
                Assert.That(Primary.isActiveAndEnabled&&Primary.IsInteractable(),Is.True);
                Assert.That(Caption(Primary),Is.EqualTo(positive?"Claim 3 diamonds":"Finish · 0 diamonds"));
                yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);
            }
            TestContext.WriteLine("Private seed1 Ready3/Ready0, eight spins earn ticket and59: UI ninth spin reveals3 Coins and pays saved600 once (597->0) while same run remains unpaid and explicit completion remains available.");
        }

        IEnumerator WholeLoopResourceInformation(string total,string included)
        {
            string before=State;var files=Files();NoFlyers();
            yield return Click(Field<Button>("productEnergy"));Assert.That(Field<int>("wavePanel"),Is.EqualTo(74));
            yield return Click(Field<Button[]>("productCards")[4]);
            var body=Field<TMP_Text>("expansionBody");Assert.That(body.isActiveAndEnabled&&body.gameObject.activeInHierarchy,Is.True);
            if(total==null)Assert.That(body.text,Does.Not.Contain("Last spin · credited"));
            else {Assert.That(body.text,Does.Contain(total).And.Contain(included).And.Contain("Already counted"));}
            Fits(body);Unchanged(before,files);NoFlyers();
            WholeLoopSnapshot("slot-info-"+(total==null?"legacy":total.Contains("+3 parts")?"included1":"included0"),"Read-only actual resource-information panel over the declared private spin fixture");
            yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);NoFlyers();
        }
        [UnityTest]
        public IEnumerator WholeLoopHistoricalSlotAttributionUsesFrozenSpinAndCaps()
        {
            foreach(string mode in new[]{"slot1","capped","level0","missing"})
            {
                ModelState state;
                if(mode=="missing")
                {
                    string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-retained-v1-spin600.json"));
                    state=codec.Decode(File.ReadAllText(path));Assert.That(state.lastSpin,Is.Not.Null);Assert.That(state.expansion,Is.Null);
                }
                else
                {
                    var setup=Fixture(Prepared(600,2,80,80));
                    if(mode!="level0")
                    {
                        setup.ExecuteArcade(setup.GetArcadeQuote("buy","arcade.slot.1",setup.Arcade.Sequence),1000);
                        Assert.That(setup.Arcade.SlotLevel,Is.EqualTo(1));
                        if(mode=="capped")setup=Fixture(FundFixture(setup.State,0,199,80,30)); // Declared synthetic headroom1 after the paid purchase.
                    }
                    setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
                    Assert.That(setup.State.lastSpin.symbols,Is.EqualTo(new[]{0,1,2}));
                    var receipt=setup.Arcade.LastResourceReceipt;Assert.That(receipt.Sequence,Is.EqualTo(setup.State.lastSpin.sequence));
                    var expected=mode=="slot1"?new[]{3,1,1,0}:mode=="capped"?new[]{1,1,0,0}:new[]{2,1,0,0};
                    Assert.That(new[]{receipt.PartsAdded,receipt.PotatoesAdded,receipt.SlotPartsIncluded,receipt.SlotPotatoesIncluded},Is.EqualTo(expected));
                    state=setup.State;
                }
                if(game==null)yield return Load(state);else yield return ReplacePrivateFixture(state);
                if(mode=="missing"){Assert.That(Session.State.lastSpin,Is.Not.Null);Assert.That(Session.Arcade.LastResourceReceipt,Is.Null);}
                string total=mode=="slot1"?"Last spin · credited: +3 parts, +1 potatoes.":mode=="capped"?"Last spin · credited: +1 parts, +1 potatoes.":mode=="level0"?"Last spin · credited: +2 parts, +1 potatoes.":null;
                string included=mode=="slot1"?"Including slot: +1 parts, +0 potatoes.":"Including slot: +0 parts, +0 potatoes.";
                yield return WholeLoopResourceInformation(total,included);
                if(mode=="level0")
                {
                    long spinSequence=Session.State.lastSpin.sequence;var frozen=Session.Arcade.LastResourceReceipt;
                    Session.ExecuteArcade(Session.GetArcadeQuote("buy","arcade.slot.1",Session.Arcade.Sequence),1000);
                    Assert.That(Session.Arcade.SlotLevel,Is.EqualTo(1));Assert.That(Session.State.sequence,Is.GreaterThan(spinSequence));
                    Session.ExecuteArcade(Session.GetArcadeQuote("start","arcade.memory",Session.Arcade.Sequence),1000);
                    long instance=Session.Arcade.ActiveRun.InstanceSequence;
                    foreach(int card in new[]{0,5,1,2,3,4})Session.ExecuteArcade(Session.GetArcadeQuote("pick","arcade.memory",Session.Arcade.Sequence,instance,card),1000);
                    Session.ExecuteArcade(Session.GetArcadeQuote("claim","arcade.memory",Session.Arcade.Sequence,instance),1000);
                    Assert.That(Session.Arcade.LastClaim,Is.Not.Null);Assert.That(Session.Arcade.LastResourceReceipt.Sequence,Is.EqualTo(frozen.Sequence));
                    Assert.That(Session.Arcade.LastResourceReceipt.SlotPartsIncluded,Is.Zero);Call("Render",null,null);NoFlyers();
                    yield return WholeLoopResourceInformation(total,included);yield return Reload();NoFlyers();yield return WholeLoopResourceInformation(total,included);
                }
            }
            TestContext.WriteLine("Independent ninth draw Potato/Box/Trash: SlotI credits3Parts/1Potato including1/0; cap headroom1 credits1/1 including0/0; level0 credits2/1 including0/0. Later slot purchase and Memory Claim preserve the earlier spin receipt across Reload; legacy saved spin without Arcade attribution omits the block. No inspection grants.");
        }

        void HudCue(long diamonds, int tickets, string badgeText, string runCue, Rect diamondBounds, Rect badgeBounds, RectTransform coinTarget)
        {
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Observe the real Game HUD before opening another modal");
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(diamonds)); Assert.That(Session.Arcade.Tickets, Is.EqualTo(tickets));
            if (runCue == null) Assert.That(Session.Arcade.ActiveRun, Is.Null);
            else Assert.That(Session.Arcade.ActiveRun, Is.Not.Null);
            var diamond = Field<TMP_Text>("productDiamonds"); var badge = Field<TMP_Text>("productEventBadge");
            Assert.That(diamond.isActiveAndEnabled && diamond.gameObject.activeInHierarchy, Is.True);
            Assert.That(badge.isActiveAndEnabled && badge.gameObject.activeInHierarchy, Is.True);
            Assert.That(diamond.text, Is.EqualTo(diamonds + "\n<size=65%>DIAMONDS</size>"));
            Assert.That(badge.text, Is.EqualTo(badgeText)); Fits(diamond); Fits(badge);
            if (runCue != null)
            {
                var message = Field<TMP_Text>("message");
                Assert.That(message.isActiveAndEnabled && message.gameObject.activeInHierarchy, Is.True);
                Assert.That(message.text, Does.Contain(runCue)); Fits(message);
            }
            Assert.That(diamond.rectTransform.rect, Is.EqualTo(diamondBounds), "Status must not resize the diamond wallet");
            Assert.That(badge.rectTransform.rect, Is.EqualTo(badgeBounds), "Status must not resize the event badge");
            foreach (var label in new[] { diamond, badge })
            {
                Assert.That(label.raycastTarget, Is.False); Assert.That(label.GetComponent<Button>(), Is.Null);
                Assert.That(label.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
                foreach (var graphic in label.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            }
            Assert.That(Field<RectTransform>("rewardFeedbackDiamondTarget"), Is.SameAs(diamond.rectTransform));
            Assert.That(Field<RectTransform>("rewardFeedbackTicketTarget"), Is.SameAs(badge.rectTransform));
            Assert.That(Field<RectTransform>("rewardFeedbackCoinTarget"), Is.SameAs(coinTarget));
            Assert.That(coinTarget, Is.SameAs(Field<TMP_Text>("productCoins").rectTransform));
            foreach (var graphic in coinTarget.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            Target(Field<Button>("spinButton")); // Observe five rays; never press Spin.
        }

        [UnityTest, Category("ArcadeHud10h")]
        public IEnumerator GameHudKeepsSavedAndReadyCuesUntilExplicitCompletionWithoutGrantingOnRead()
        {
            var initial = Prepared(600); yield return Load(initial);
            var diamondBounds = Field<TMP_Text>("productDiamonds").rectTransform.rect;
            var badgeBounds = Field<TMP_Text>("productEventBadge").rectTransform.rect;
            var coinTarget = Field<RectTransform>("rewardFeedbackCoinTarget");
            HudCue(0, 1, "1 TICKET\nEVENTS →", null, diamondBounds, badgeBounds, coinTarget);
            var statusTarget=(RectTransform)Field<Button>("productEventStatus").transform;
            Assert.That(statusTarget.rect.width,Is.GreaterThanOrEqualTo(48));Assert.That(statusTarget.rect.height,Is.GreaterThanOrEqualTo(48));
            string entryBefore=State;var entryFiles=Files();
            yield return Click(Field<Button>("productEventStatus"));
            foreach(int card in new[]{0,1})Assert.That(Caption(Field<Button[]>("productCards")[card]),Does.Contain("Start · 1 Ticket"));
            Unchanged(entryBefore,entryFiles);yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(entryBefore,entryFiles);
            yield return Home(); yield return GamePreview(3, "arcade.memory"); Quote("start", "arcade.memory", tickets: 1);
            yield return Click(Primary); LowerUnchanged(initial, 1); yield return BackToGame();
            HudCue(0, 0, "ROUND SAVED", "Round saved", diamondBounds, badgeBounds, coinTarget); NoFlyers();
            var active = Session.State; long activeId = Session.Arcade.ActiveRun.InstanceSequence;
            string before = State; var files = Files();
            yield return Click(Field<Button>("productEventStatus"));
            Assert.That(Caption(Field<Button[]>("productCards")[3]),Does.Contain("Resume round"));
            Assert.That(Caption(Field<Button[]>("productCards")[0]),Does.Contain("Resume free").And.Not.Contain("Up to 3 Diamonds"));
            Assert.That(Caption(Field<Button[]>("productCards")[1]),Does.Contain("Other round saved"));
            ReturnArcadeSnapshot("events-memory-active","Saved active Memory was started by real UI after publicly earned ticket; Events inspection does not consume another ticket.");
            yield return Click(Field<Button[]>("productCards")[3]);Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(activeId));Unchanged(before,files);
            yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);
            yield return Home(); yield return Click(Card(3)); Run("arcade.memory", "first", 0, 0, 0); yield return BackToGame();
            Unchanged(before, files); HudCue(0, 0, "ROUND SAVED", "Round saved", diamondBounds, badgeBounds, coinTarget);
            yield return Reload(); Unchanged(before, files); NoFlyers();
            HudCue(0, 0, "ROUND SAVED", "Round saved", diamondBounds, badgeBounds, coinTarget);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(activeId)); Assert.That(Session.State.seed, Is.EqualTo(active.seed));
            yield return Home(); yield return Click(Card(3)); yield return Click(Secondary);
            Quote("abandon", "arcade.memory"); Unchanged(before, files); yield return Click(Primary); yield return BackToGame();
            Assert.That(Session.Arcade.ActiveRun, Is.Null); Assert.That(Session.Arcade.Tickets, Is.Zero); LowerUnchanged(active, 1); NoFlyers();
            HudCue(0, 0, "TICKET IN\n8 SPINS", null, diamondBounds, badgeBounds, coinTarget);
            before=State;files=Files();yield return Click(Field<Button[]>("productTabs")[2]);
            foreach(int card in new[]{0,1})Assert.That(Caption(Field<Button[]>("productCards")[card]),Does.Contain("No Ticket · inspect rules"));
            Unchanged(before,files);yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);

            var setup = Fixture(Prepared(540)); setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.memory", setup.Arcade.Sequence), 1000);
            long positiveId = setup.Arcade.ActiveRun.InstanceSequence;
            foreach (int card in new[] { 0, 5, 1, 2, 3, 4 })
                setup.ExecuteArcade(setup.GetArcadeQuote("pick", "arcade.memory", setup.Arcade.Sequence, positiveId, card), 1000);
            var positive = setup.State; yield return ReplacePrivateFixture(positive);
            Run("arcade.memory", "ready", 6, 3, 3, 3); Assert.That(Session.Arcade.Diamonds, Is.Zero); NoFlyers();
            HudCue(0, 0, "CLAIM 3 DIAM.\nEVENTS →", "Result ready: 3 Diamonds", diamondBounds, badgeBounds, coinTarget);
            before = State; files = Files();yield return Click(Field<Button>("productEventStatus"));
            Assert.That(Caption(Field<Button[]>("productCards")[3]),Does.Contain("Result ready").And.Contain("3 Diamonds"));
            Assert.That(Caption(Field<Button[]>("productCards")[0]),Does.Contain("Ready · 3 Diamonds").And.Not.Contain("Up to 3 Diamonds"));
            ReturnArcadeSnapshot("events-memory-ready3","Actual publicly completed Memory Ready3 remains unpaid; Events card only inspects the same instance.");
            yield return Click(Field<Button[]>("productCards")[3]);Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(positiveId));Unchanged(before,files);
            yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);
            yield return Home(); yield return Click(Card(3)); yield return BackToGame();
            Unchanged(before, files); HudCue(0, 0, "CLAIM 3 DIAM.\nEVENTS →", "Result ready: 3 Diamonds", diamondBounds, badgeBounds, coinTarget);
            yield return Reload(); Unchanged(before, files); NoFlyers();
            HudCue(0, 0, "CLAIM 3 DIAM.\nEVENTS →", "Result ready: 3 Diamonds", diamondBounds, badgeBounds, coinTarget);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(positiveId)); Assert.That(Session.State.seed, Is.EqualTo(positive.seed));
            yield return Home(); yield return Click(Card(3)); Quote("claim", "arcade.memory", reward: 3); Unchanged(before, files);
            yield return Click(Primary); yield return BackToGame(); Assert.That(Session.Arcade.ActiveRun, Is.Null);
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Arcade.LastClaim.Diamonds, Is.EqualTo(3)); LowerUnchanged(positive, 1);
            HudCue(3, 0, "TICKET IN\n8 SPINS", null, diamondBounds, badgeBounds, coinTarget);
            before = State; files = Files(); yield return Reload(); Unchanged(before, files); NoFlyers();
            HudCue(3, 0, "TICKET IN\n8 SPINS", null, diamondBounds, badgeBounds, coinTarget);

            setup = Fixture(Prepared(600)); setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.sorting", setup.Arcade.Sequence), 1000);
            long zeroId = setup.Arcade.ActiveRun.InstanceSequence;
            foreach (int bin in new[] { 3, 2, 2, 1, 0, 3, 1, 0 })
                setup.ExecuteArcade(setup.GetArcadeQuote("sort", "arcade.sorting", setup.Arcade.Sequence, zeroId, bin), 1000);
            var zero = setup.State; yield return ReplacePrivateFixture(zero);
            Run("arcade.sorting", "ready", 8, 8, 0, 0); Assert.That(Session.Arcade.Diamonds, Is.Zero); NoFlyers();
            HudCue(0, 0, "FINISH · 0\nEVENTS →", "Zero result: finish in Events.", diamondBounds, badgeBounds, coinTarget);
            before = State; files = Files();yield return Click(Field<Button>("productEventStatus"));
            Assert.That(Caption(Field<Button[]>("productCards")[3]),Does.Contain("Finish · 0 Diamonds").And.Contain("Round complete · finish explicitly"));
            Assert.That(Caption(Field<Button[]>("productCards")[1]),Does.Contain("Finish · 0 Diamonds").And.Not.Contain("Up to 3 Diamonds"));
            ReturnArcadeSnapshot("events-sorting-ready0","Actual public Sorting run reached Ready0 without a claim; Events inspection preserves instance and ticket balance.");
            yield return Click(Field<Button[]>("productCards")[3]);Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(zeroId));Unchanged(before,files);
            yield return Click(Field<Button[]>("productTabs")[1]);Unchanged(before,files);
            yield return Home(); yield return Click(Card(4)); yield return BackToGame();
            Unchanged(before, files); HudCue(0, 0, "FINISH · 0\nEVENTS →", "Zero result: finish in Events.", diamondBounds, badgeBounds, coinTarget);
            yield return Reload(); Unchanged(before, files); NoFlyers();
            HudCue(0, 0, "FINISH · 0\nEVENTS →", "Zero result: finish in Events.", diamondBounds, badgeBounds, coinTarget);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(zeroId)); Assert.That(Session.State.seed, Is.EqualTo(zero.seed));
            yield return Home(); yield return Click(Card(4)); Quote("claim", "arcade.sorting"); Unchanged(before, files);
            yield return Click(Primary); yield return BackToGame(); Assert.That(Session.Arcade.ActiveRun, Is.Null);
            Assert.That(Session.Arcade.Diamonds, Is.Zero); Assert.That(Session.Arcade.LastClaim.Diamonds, Is.Zero); LowerUnchanged(zero, 1); NoFlyers();
            HudCue(0, 0, "TICKET IN\n8 SPINS", null, diamondBounds, badgeBounds, coinTarget);
            OldGoal(600, zero.coins);
            TestContext.WriteLine("Product HUD: active/Ready3/Ready0 survive modal Back+Reload0IO with the same run/seed and no read-time grant/flyer. Explicit Abandon/positive Claim/zero Claim remove the cue; retained claim never becomes a payable HUD cue. Visible diamond and event badge text, run message, fitting, unchanged rects, nonraycast reward targets and five Spin rays are checked without pressing Spin. Large-balance fixture not invented.");
        }

        [UnityTest]
        public IEnumerator AbandonConfirmationCancelsToTheSameRoundAndNamesOnlyTheUnpaidLoss()
        {
            var paid = codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../Checks/ArcadeCore/Evidence/claim-committed.json")))); paid.lastSettledAt = 1000;
            var setup = Fixture(paid); string retainedClaim = setup.Arcade.LastClaim.Key;
            Assert.That(setup.Arcade.Diamonds, Is.EqualTo(3));
            setup.ExecuteArcade(setup.GetArcadeQuote("buy", "arcade.pack.parts", setup.Arcade.Sequence), 1000);
            setup.ExecuteArcade(setup.GetArcadeQuote("start", "arcade.sorting", setup.Arcade.Sequence), 1000);
            var initialActive = setup.State; yield return Load(initialActive);
            long instance = Session.Arcade.ActiveRun.InstanceSequence; uint seed = Session.State.seed;
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2)); Assert.That(Session.Arcade.Tickets, Is.Zero);
            string before = State; var files = Files(); yield return Home(); yield return Click(Card(4));
            yield return Click(Secondary); PolishView(); Quote("abandon", "arcade.sorting");
            Assert.That(Caption(Primary), Is.EqualTo("Abandon without reward")); Assert.That(Caption(Secondary), Is.EqualTo("Continue round"));
            Assert.That(Caption(Field<Button>("closeButton")), Is.EqualTo("Cancel · to round"));
            Assert.That(ArcadeBody, Does.Not.Contain("Unclaimed reward:")); Unchanged(before, files);
            var oldActiveConfirm = Primary.onClick; yield return Back(); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(63)); Run("arcade.sorting", "sorting", 0, 0, 0);
            oldActiveConfirm.Invoke(); Unchanged(before, files);
            yield return Click(Secondary); yield return Click(Secondary); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(63)); Unchanged(before, files);
            yield return Click(Secondary); yield return Click(Field<Button>("closeButton")); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(63)); Unchanged(before, files);
            yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); Unchanged(before, files);
            yield return Click(Card(4)); int visibleBin = Session.Arcade.ActiveRun.CurrentSortingSymbol;
            yield return Click(Card(visibleBin)); Run("arcade.sorting", "sorting", 1, 1, 1);
            before = State; files = Files(); oldActiveConfirm.Invoke(); Unchanged(before, files);
            yield return Click(Secondary); var interruptedConfirm = Primary.onClick; Unchanged(before, files);
            yield return PauseAndResume(); interruptedConfirm.Invoke(); Unchanged(before, files);
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            yield return Reload(); Unchanged(before, files); yield return Home(); yield return Click(Card(4));
            Run("arcade.sorting", "sorting", 1, 1, 1); Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(instance));
            Assert.That(Session.State.seed, Is.EqualTo(seed)); yield return BackToGame();

            setup = Fixture(Session.State);
            for (int move = 0; move < 7; move++)
            {
                var current = setup.Arcade.ActiveRun;
                // Only the public, currently visible symbol is used as input;
                // no board, queue, seed setter or reward solver is accessed.
                setup.ExecuteArcade(setup.GetArcadeQuote("sort", "arcade.sorting", setup.Arcade.Sequence, instance, current.CurrentSortingSymbol), 1000);
            }
            var positive = setup.State; yield return ReplacePrivateFixture(positive);
            Run("arcade.sorting", "ready", 8, 8, 8, 3); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2));
            before = State; files = Files(); yield return Home(); yield return Click(Card(4)); yield return Click(Secondary); PolishView();
            Assert.That(ArcadeBody, Is.EqualTo("Unclaimed reward: 3 diamonds.\nAbandoning forfeits it. Diamonds in your wallet are not deducted; the ticket is not returned."));
            Assert.That(Caption(Primary), Is.EqualTo("Forfeit 3 diamonds")); Assert.That(Caption(Secondary), Is.EqualTo("To result"));
            Assert.That(Caption(Field<Button>("closeButton")), Is.EqualTo("Cancel · to result")); Unchanged(before, files);
            var oldLossConfirm = Primary.onClick; yield return Back(); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64)); Quote("claim", "arcade.sorting", reward: 3);
            oldLossConfirm.Invoke(); Unchanged(before, files);
            yield return Click(Secondary); var held = Down(Primary, 951); yield return Click(Secondary); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64)); Release(held, Primary); oldLossConfirm.Invoke(); Unchanged(before, files);
            yield return Click(Secondary); yield return Click(Field<Button>("closeButton")); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64)); Unchanged(before, files);
            yield return Back(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(60)); Unchanged(before, files);
            yield return Click(Card(4)); yield return Click(Secondary); var abandon = Quote("abandon", "arcade.sorting");
            Assert.That(abandon.ExpectedInstance, Is.EqualTo(instance)); yield return Click(Primary);
            Assert.That(Session.Arcade.ActiveRun, Is.Null); Assert.That(Session.State.sequence, Is.EqualTo(abandon.Sequence));
            Assert.That(Session.Arcade.LastEvent.Sequence, Is.EqualTo(abandon.Sequence));
            Assert.That(Session.Arcade.LastEvent.Kind, Is.EqualTo("abandon")); Assert.That(Session.Arcade.LastEvent.InstanceSequence, Is.EqualTo(instance));
            Assert.That(Session.Arcade.LastEvent.DiamondsAdded, Is.Zero); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2));
            Assert.That(Session.Arcade.Tickets, Is.Zero); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(retainedClaim));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 27, 18, 16 }));
            Assert.That(Session.Arcade.LastClaim.Diamonds, Is.EqualTo(3)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            LowerUnchanged(positive, 1); NoFlyers();

            setup = Fixture(initialActive);
            for (int move = 0; move < 8; move++)
            {
                var current = setup.Arcade.ActiveRun;
                setup.ExecuteArcade(setup.GetArcadeQuote("sort", "arcade.sorting", setup.Arcade.Sequence, instance, (current.CurrentSortingSymbol + 1) % 4), 1000);
            }
            yield return ReplacePrivateFixture(setup.State); Run("arcade.sorting", "ready", 8, 8, 0, 0);
            before = State; files = Files(); yield return Home(); yield return Click(Card(4)); yield return Click(Secondary); PolishView();
            Assert.That(ArcadeBody, Is.EqualTo("Reward for this result: 0 diamonds; the ticket is not returned.\nThe result will close without a reward."));
            Assert.That(Caption(Primary), Is.EqualTo("Abandon without reward")); Assert.That(Caption(Secondary), Is.EqualTo("To result"));
            Assert.That(Caption(Field<Button>("closeButton")), Is.EqualTo("Cancel · to result"));
            oldLossConfirm.Invoke(); Unchanged(before, files); yield return Back(); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64)); Quote("claim", "arcade.sorting"); Unchanged(before, files);
            yield return Click(Secondary); yield return Click(Secondary); PolishView();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(64)); Unchanged(before, files);
            Assert.That(Session.Arcade.ActiveRun.InstanceSequence, Is.EqualTo(instance)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(2)); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(retainedClaim)); NoFlyers();
            TestContext.WriteLine("Active/Ready3/Ready0 cancellation returns the exact same round/result0IO by Back/secondary/sharedClose; normal round Back still Home. Old canceled/held/lifecycle confirmations cannot abandon or claim newer state. Ready3 preview names unpaid3 separately from wallet2; fresh explicit abandon removes only that run, refunds/grants0 and preserves actual paid claim/seed/lower state. Ready0 remains unpaid and resumable.");
        }
        [SetUp] public void Isolate()
        {
            previousSave = Environment.GetEnvironmentVariable(SaveOverride);
            previousClock = Environment.GetEnvironmentVariable(ClockOverride);
            previousScene = SceneManager.GetActiveScene(); scene = default; game = null; folder = null;
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "Use the neutral scene, never the user's running game");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/ten-hour-expansion-20261031/06-input/private"));
            Assert.That(Path.GetPathRoot(root), Is.EqualTo("X:\\").IgnoreCase);
            folder = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable(SaveOverride, folder); Environment.SetEnvironmentVariable(ClockOverride, "1000");
            gameType = Type.GetType("JunkToNeon.JunkGame, Assembly-CSharp", true);
            TestContext.WriteLine("Owned ten-hour Arcade fixture: " + folder);
        }
        IEnumerator Load(ModelState state)
        {
            state.lastSettledAt = 1000; GameLogic.Validate(state);
            File.WriteAllText(Path.Combine(folder, "jtn.save.v1.json"), codec.Encode(state));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(scene);
            game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(gameType, true)).Single();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            var store = typeof(GameSession).GetField("store", Hidden).GetValue(Session);
            var storage = (FileSnapshotStorage)typeof(GameStore).GetField("storage", Hidden).GetValue(store);
            foreach (var field in typeof(FileSnapshotStorage).GetFields(Hidden).Where(field => field.FieldType == typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))), Is.EqualTo(folder), field.Name + " escaped private X storage");
            yield return null;
        }
        static Vector2 Center(Button button) => ((RectTransform)button.transform).rect.center;
        static string HierarchyPath(GameObject target)
        {
            if (target == null) return "<null>";
            var names = new List<string>();
            for (var parent = target.transform; parent != null; parent = parent.parent) names.Add(parent.name);
            names.Reverse(); return target.scene.name + ":/" + string.Join("/", names);
        }
        static string RaycastFailure(Button button, Vector2 local, Vector2 screen, Canvas canvas, List<RaycastResult> hits)
        {
            // Failure-only, bounded diagnostics; no extra raycast or UI mutation.
            var rect = (RectTransform)button.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var message = new StringBuilder(button.name + " is intercepted; target=" + HierarchyPath(button.gameObject));
            message.Append("; local=").Append(local.ToString("F2")).Append("; screen=").Append(screen.ToString("F2"))
                .Append("; buttonRect=").Append(rect.rect).Append("; anchors=").Append(rect.anchorMin).Append("..").Append(rect.anchorMax)
                .Append("; pivot=").Append(rect.pivot).Append("; anchoredPosition=").Append(rect.anchoredPosition).Append("; sizeDelta=").Append(rect.sizeDelta)
                .Append("; screenCorners=").Append(string.Join(",", corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera, c).ToString("F2"))))
                .Append("; Screen=").Append(Screen.width).Append('x').Append(Screen.height).Append("; safeArea=").Append(Screen.safeArea)
                .Append("; Canvas=").Append(HierarchyPath(canvas.gameObject)).Append("; renderMode=").Append(canvas.renderMode)
                .Append("; pixelRect=").Append(canvas.pixelRect).Append("; canvasRect=").Append(((RectTransform)canvas.transform).rect)
                .Append("; scaleFactor=").Append(canvas.scaleFactor).Append("; lossyScale=").Append(canvas.transform.lossyScale)
                .Append("; camera=").Append(camera == null ? "<overlay/null>" : HierarchyPath(camera.gameObject));
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) message.Append("; scaler=").Append(scaler.uiScaleMode).Append("; referenceResolution=").Append(scaler.referenceResolution).Append("; match=").Append(scaler.matchWidthOrHeight);
            message.Append("; hitCount=").Append(hits.Count);
            for (int i = 0; i < Math.Min(hits.Count, 3); i++)
            {
                var hit = hits[i]; var target = hit.gameObject; var graphic = target == null ? null : target.GetComponent<Graphic>();
                message.Append("; hit[").Append(i).Append("]=").Append(HierarchyPath(target))
                    .Append("; types=").Append(target == null ? "<null>" : string.Join(",", target.GetComponents<Component>().Take(8).Select(c => c == null ? "<missing>" : c.GetType().FullName)))
                    .Append("; clickHandler=").Append(HierarchyPath(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)))
                    .Append("; depth/sortingOrder=").Append(hit.depth).Append('/').Append(hit.sortingOrder)
                    .Append("; graphicRaycast=").Append(graphic == null ? "<none>" : graphic.raycastTarget.ToString());
            }
            return message.Length <= 8192 ? message.ToString() : message.ToString(0, 8192) + " [diagnostic truncated]";
        }
        static RaycastResult Top(Button button, Vector2 local)
        {
            Canvas.ForceUpdateCanvases(); var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, button.transform.TransformPoint(local)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), hits.Count == 0 ? RaycastFailure(button, local, pointer.position, canvas, hits) : button.name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.EqualTo(button.gameObject), handler == button.gameObject ? button.name : RaycastFailure(button, local, pointer.position, canvas, hits));
            return hits[0];
        }
        static void Target(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name); Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name);
            foreach (var p in new[] { rect.center, rect.min + Vector2.one * 4, rect.max - Vector2.one * 4,
                new Vector2(rect.xMin + 4, rect.yMax - 4), new Vector2(rect.xMax - 4, rect.yMin + 4) }) Top(button, p);
        }
        static PointerEventData Down(Button button, int id)
        {
            Target(button); var hit = Top(button, Center(button));
            var data = new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left,
                eligibleForClick = true, pointerCurrentRaycast = hit, position = hit.screenPosition };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = button.gameObject; data.pointerClick = button.gameObject; return data;
        }
        static void Release(PointerEventData data, Button current)
        {
            data.pointerCurrentRaycast = Top(current, Center(current)); data.position = data.pointerCurrentRaycast.screenPosition;
            bool click = data.eligibleForClick && data.pointerClick == ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (click) ExecuteEvents.Execute(data.pointerClick, data, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Click(Button button, int id = 701)
        {
            yield return null;
            int inputFrame = Time.frameCount;
            Release(Down(button, id), button);
            // Newly enabled graphics acquire native Canvas depth on a later
            // frame. Keep Down/Up/Click synchronous, then expose the new view.
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(inputFrame), "A new view must receive a frame before the next input");
        }
        static void Submit(Button button)
        { Target(button); ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); }
        IEnumerator Back()
        {
            yield return null;
            var back = game.GetComponents<Component>().Single(c => c.GetType().FullName == "JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back, null), Is.True); yield return null;
        }
        static void Fits(TMP_Text text)
        {
            Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2), text.name);
            Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Environment.SetEnvironmentVariable(SaveOverride, previousSave); Environment.SetEnvironmentVariable(ClockOverride, previousClock);
            if (folder == null) yield break;
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            { TestContext.WriteLine("Preserved first failed Arcade private fixture: " + folder); yield break; }
            foreach (string name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) File.Delete(path);
            }
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder, false);
        }
    }
}
#endif
