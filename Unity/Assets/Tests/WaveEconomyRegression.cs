using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class WaveEconomyRegression
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
                if (writes == failAt && !commitBeforeFailure) throw new IOException("Private wave write failed before commit");
                backup = primary; primary = raw;
                if (writes == failAt) throw new IOException("Private wave write committed before reporting failure");
            }
        }

        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void Rejects(Action action, string message)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            catch (ArgumentException) { return; }
            catch (FormatException) { return; }
            catch (OverflowException) { return; }
            throw new Exception(message);
        }
        static GameSession Open(Storage storage)
        {
            var game = GameSession.Open(new GameStore(storage, Codec), 77);
            Require(!game.RecoveryRequired, "Private wave fixture failed to open: " + game.Error);
            return game;
        }
        static ModelState Repaired(long coins, int workshop = 0, uint seed = 1)
        {
            var state = ModelState.Fresh(seed);
            state.houseStage = 1; state.autoHintSeen = 1; state.coins = coins; state.workshopBuilt = workshop;
            return state;
        }
        static Storage Stored(ModelState state) { return new Storage { primary = Codec.Encode(state) }; }
        static void Symbols(SpinReceipt receipt, int first, int middle, int last)
        {
            Require(receipt.symbols[0] == first && receipt.symbols[1] == middle && receipt.symbols[2] == last, "Independent frozen outcome changed");
        }
        static string Change(string raw, string from, string to)
        {
            int start = raw.IndexOf(from, StringComparison.Ordinal);
            Require(start >= 0, "Invalid regression mutation fixture: " + from);
            return raw.Substring(0, start) + to + raw.Substring(start + from.Length);
        }

        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            foreach (int target in new[] { 4, 5 })
            {
                long beforeCoins = target == 4 ? 179 : 239;
                var fixture = Repaired(beforeCoins, seed: target == 4 ? 1u : 5u);
                fixture.selectedStage = target;
                var game = Open(Stored(fixture));
                var after = game.ExecuteSpin(1);
                Symbols(after.lastSpin, 0, 0, target == 4 ? 2 : 3);
                var wave = WaveProgress.Of(after);
                Require(after.coins == 7 && after.selectedStage == 0 && after.houseStage == 1 && after.workshopBuilt == 0,
                    "Acquisition spin charged another project or received a retroactive effect");
                Require(wave.RecyclerBuilt == (target == 4 ? 1 : 0) && wave.PigeonLoftBuilt == (target == 5 ? 1 : 0), "Wrong explicit project acquired");
                Require(after.lastSpin.reward == 8 && after.lastSpin.bonus == 0 && after.lastSpin.wave.Effects.TotalCoins == 0 &&
                    wave.Delivery.PigeonProgress == 0, "Acquisition outcome granted new income or accumulated old pigeons");
            }
            passed.Add("Explicit recycler180/loft240 acquisition from frozen pair8 leaves7; acquisition spin grants no new effect or carry");

            var shop = Open(Stored(Repaired(1000, 1)));
            var ownedFixture = shop.BuySlotUpgrade(1);
            ownedFixture.coins = 10;
            ownedFixture.wave = new WaveProgress(1, 0, WaveProgress.Of(ownedFixture).Delivery, WaveProgress.Of(ownedFixture).Slot);
            var storage = Stored(ownedFixture);
            var session = Open(storage);
            var result = session.ExecuteSpin(2);
            Symbols(result.lastSpin, 0, 0, 2);
            var receipt = result.lastSpin.wave;
            Require(result.coins == 21 && result.lastSpin.reward == 8 && result.lastSpin.bonus == 1 &&
                receipt.BaseCoins == 8 && receipt.WorkshopCoins == 1 && receipt.SlotCoins == 1 &&
                receipt.Effects.RecyclerCoins == 1 && receipt.Effects.DeliveryCoins == 0, "Base8/workshop1/slot1/recycler1 were not separate additions");
            session = Open(storage);
            Require(session.State.coins == 21 && session.State.lastSpin.wave.Effects.RecyclerCoins == 1 &&
                session.State.lastSpin.wave.SlotCoins == 1, "Separated credits lost on reload");
            passed.Add("Known seed1 outcome[0,0,2]:10+base8+workshop1+slot1+recycler1=21, with separated receipts retained on reload");

            // A valid independently authored prior receipt supplies carry2. Seed5's
            // next frozen outcome is[0,0,3], giving one delivery rather than another spin.
            var previous = SpinEffects.Prepare(new SpinOutcome(3, 3, 0), new EffectOwnership(0, 1),
                DeliveryState.Fresh(ProgressionCatalog.Rules), 1, ProgressionCatalog.Rules);
            var deliveryFixture = Repaired(10, seed: 5);
            deliveryFixture.sequence = 1; deliveryFixture.revision = 2;
            deliveryFixture.wave = new WaveProgress(0, 1, previous.After, UpgradeOwnership.Empty);
            deliveryFixture.lastSpin = new SpinReceipt { sequence = 1, symbols = new[] { 3, 3, 0 }, reward = 8, bonus = 0,
                wave = new WaveOperation(1, 0, 0, 8, 0, 0, previous, null) };
            storage = Stored(deliveryFixture); session = Open(storage);
            result = session.ExecuteSpin(2);
            Symbols(result.lastSpin, 0, 0, 3);
            Require(result.coins == 21 && result.lastSpin.reward == 8 && result.lastSpin.wave.Effects.DeliveryCoins == 3 &&
                result.lastSpin.wave.Effects.Deliveries == 1 && WaveProgress.Of(result).Delivery.PigeonProgress == 0, "Carry2+one pigeon did not grant3 once and reset carry");
            session = Open(storage); int writes = storage.writes; session.ExecuteSpin(2);
            Require(storage.writes == writes && session.State.coins == 21 && WaveProgress.Of(session.State).Delivery.PigeonProgress == 0,
                "Delivery replayed or lost its counter on reload");
            passed.Add("Persisted pigeoncarry2 plus one seed5 pigeon grants3 once, leavescarry0 and survives duplicate/reload");

            var shopFixture = Repaired(300); shopFixture.selectedStage = 4;
            storage = Stored(shopFixture); session = Open(storage);
            string previewBefore = storage.primary;
            var preview = CoinBoostShop.Preview(session.State.coins, session.State.houseStage, WaveProgress.Of(session.State).Slot);
            Require(preview.Status == OfferStatus.Available && storage.primary == previewBefore && storage.writes == 0, "Shop inspection spent or wrote state");
            result = session.BuySlotUpgrade(1);
            Require(result.coins == 180 && result.selectedStage == 4 && WaveProgress.Of(result).RecyclerBuilt == 0 &&
                WaveProgress.Of(result).Slot.Level == 1 && result.lastSpin == null, "Shop purchase changed target, bought an affordable project or invented a spin");
            session = Open(storage); writes = storage.writes; session.BuySlotUpgrade(1);
            Require(storage.writes == writes && session.State.coins == 180 && session.State.selectedStage == 4, "Shop duplicate debited twice or lost selected target");
            storage = Stored(Repaired(119)); session = Open(storage);
            Rejects(() => session.BuySlotUpgrade(1), "Insufficient shop purchase accepted");
            Require(storage.writes == 0 && session.State.coins == 119, "Rejected shop purchase wrote state");
            storage=Stored(Repaired(300));session=Open(storage);
            var staleQuote=new UpgradePurchaseRequest(GameIds.SlotCoinBoost,1,0,300,1);
            session.Checkpoint(1000);writes=storage.writes;
            Rejects(()=>session.BuySlotUpgrade(staleQuote),"Stale integrated quote accepted");
            Require(storage.writes==writes&&!session.RecoveryRequired&&session.State.coins==300&&WaveProgress.Of(session.State).Slot.Level==0,"Stale quote mutated or blocked valid state");
            var freshQuote=new UpgradePurchaseRequest(GameIds.SlotCoinBoost,2,0,300,1);
            session.BuySlotUpgrade(freshQuote);session.Checkpoint(1001);writes=storage.writes;
            session.BuySlotUpgrade(freshQuote);
            Require(storage.writes==writes&&session.State.coins==180&&session.State.sequence==3,"Interleaved quoted replay replaced wallet or sequence");
            passed.Add("Explicit shop120 debit300→180 preserves target; stale quotes reject without writes/blocking, replay after interleaved commands preserves live wallet");

            foreach (bool upgrade in new[] { false, true })
                foreach (bool committed in new[] { false, true })
                    foreach (int failedWrite in new[] { 1, 2 })
                    {
                        var fixture = upgrade ? Repaired(300) : Repaired(10, 1);
                        if (upgrade) fixture.selectedStage = 4;
                        else fixture.wave = new WaveProgress(1, 0, DeliveryState.Fresh(ProgressionCatalog.Rules), UpgradeOwnership.Empty);
                        storage = Stored(fixture); storage.failAt = failedWrite; storage.commitBeforeFailure = committed;
                        session = Open(storage);
                        try
                        {
                            if (upgrade) session.BuySlotUpgrade(1); else session.ExecuteSpin(1);
                            throw new Exception("Injected wave write failure was not observed");
                        }
                        catch (IOException) { }
                        Require(session.RecoveryRequired, "Uncertain wave write allowed more commands");
                        var blockedSession = session;
                        Rejects(() => blockedSession.ExecuteSpin(1), "Blocked session accepted a command");
                        storage.failAt = 0; session = Open(storage);
                        if (upgrade) session.BuySlotUpgrade(1); else session.ExecuteSpin(1);
                        result = session.State;
                        Require(result.sequence == 1 && result.pending == null && result.coins == (upgrade ? 180 : 20), "Interrupted reserve/apply duplicated or lost a wave operation");
                        Require(upgrade ? WaveProgress.Of(result).Slot.Level == 1 && result.selectedStage == 4 && WaveProgress.Of(result).RecyclerBuilt == 0
                            : result.lastSpin.reward == 8 && result.lastSpin.bonus == 1 && result.lastSpin.wave.Effects.RecyclerCoins == 1,
                            "Recovered wave operation changed its frozen components");
                        session = Open(storage); writes = storage.writes;
                        if (upgrade) session.BuySlotUpgrade(1); else session.ExecuteSpin(1);
                        Require(storage.writes == writes && session.State.coins == (upgrade ? 180 : 20), "Recovered operation replayed on another reload");
                    }
            passed.Add("Spin/shop reserve and applied writes, before/after-commit failures: block uncertainty and recover each operation exactly once");

            var strictFixture = Repaired(10, 1);
            strictFixture.wave = new WaveProgress(1, 1, DeliveryState.Fresh(ProgressionCatalog.Rules), UpgradeOwnership.Empty);
            string raw = Codec.Encode(GameLogic.Reserve(strictFixture, "spin", 0, 1));
            foreach (string invalid in new[] {
                Change(raw, "\"wave\":{", "\"wave\":{\"unknown\":1,"),
                Change(raw, "\"format\":1,", ""),
                Change(raw, "\"format\":1", "\"format\":2"),
                Change(raw, "\"baseCoins\":8", "\"baseCoins\":9"),
                Change(raw, "\"recyclerCoins\":1", "\"recyclerCoins\":2"),
                Change(raw, "\"carryAfter\":0", "\"carryAfter\":3"),
                Change(raw, "\"price\":0", "\"price\":1"),
                Change(raw, "\"pigeonOwned\":1", "\"pigeonOwned\":0") })
                Rejects(() => Codec.Decode(invalid), "Malformed or inconsistent frozen wave accepted");
            string plain = Codec.Encode(Repaired(10));
            Rejects(() => Codec.Decode(plain.Substring(0, plain.Length - 1) + ",\"wave\":null}"), "Present null wave treated as an absent legacy field");
            passed.Add("Strict wave codec rejects unknown/null/missing-format/version, wrong frozen credits/price/ownership and invalid carry");

            const string legacy = "{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":0,\"sequence\":0,\"coins\":10,\"houseStage\":1,\"selectedStage\":0,\"seed\":1,\"autoHintSeen\":1,\"workshopBuilt\":1,\"lastSpin\":null,\"pending\":null}";
            storage = new Storage { primary = legacy }; session = Open(storage);
            Require(storage.writes == 0 && storage.primary == legacy && session.State.coins == 10 && session.State.wave == null,
                "Legacy committed save received an eager write or initial grant");
            var modernSelect=Repaired(150);modernSelect.wave=WaveProgress.Empty;
            const string pendingSelect="{\"sequence\":1,\"kind\":\"select\",\"symbols\":null,\"seedAfter\":0,\"target\":3}";
            string missingSelectDescriptor=Codec.Encode(modernSelect).Replace("\"revision\":0","\"revision\":1").Replace("\"pending\":null","\"pending\":"+pendingSelect);
            Rejects(()=>Codec.Decode(missingSelectDescriptor),"Modern selected purchase accepted missing frozen descriptor");
            const string literalLegacySelect="{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":1,\"sequence\":0,\"coins\":150,\"houseStage\":1,\"selectedStage\":0,\"seed\":1,\"autoHintSeen\":1,\"workshopBuilt\":0,\"lastSpin\":null,\"pending\":{\"sequence\":1,\"kind\":\"select\",\"symbols\":null,\"seedAfter\":0,\"target\":3}}";
            var legacySelectStore=new Storage{primary=literalLegacySelect};var legacySelectSession=Open(legacySelectStore);
            Require(legacySelectSession.State.coins==30&&legacySelectSession.State.workshopBuilt==1&&legacySelectSession.State.wave==null&&legacySelectStore.writes==1,"Literal r6 pending select no longer restores once");
            const string legacyPending = "{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":1,\"sequence\":0,\"coins\":10,\"houseStage\":1,\"selectedStage\":0,\"seed\":1,\"autoHintSeen\":1,\"workshopBuilt\":1,\"lastSpin\":null,\"pending\":{\"sequence\":1,\"kind\":\"spin\",\"symbols\":[0,0,2],\"seedAfter\":2647435461,\"target\":0}}";
            storage = new Storage { primary = legacyPending }; session = Open(storage);
            Require(storage.writes == 1 && session.State.coins == 19 && session.State.sequence == 1 && session.State.wave == null &&
                session.State.lastSpin.reward == 8 && session.State.lastSpin.bonus == 1 && session.State.lastSpin.wave == null,
                "Old r6 pending spin received new effects or lost its existing workshop reward");
            session = Open(storage); writes = storage.writes; session.ExecuteSpin(1);
            Require(storage.writes == writes && session.State.coins == 19, "Legacy pending result replayed after migration");
            passed.Add("Literal oldr6 committed save stays byte-identical; old frozen spin recovers8+existingworkshop1 with zero new effects/grant");

            foreach (int[] order in new[] { new[] { 2, 3, 4, 5 }, new[] { 5, 4, 3, 2 } })
            {
                storage = Stored(Repaired(1140)); session = Open(storage);
                for (int index = 0; index < order.Length; index++)
                {
                    result = session.SelectProject(order[index], index + 1);
                    Require(result.selectedStage == 0 && HouseGoal.Complete(result) == (index == order.Length - 1), "Completion omitted a required family or chained a target");
                }
                session = Open(storage); result = session.State;
                Require(result.coins == 0 && result.houseStage == 2 && result.workshopBuilt == 1 && WaveProgress.Of(result).RecyclerBuilt == 1 &&
                    WaveProgress.Of(result).PigeonLoftBuilt == 1 && WaveProgress.Of(result).Slot.Level == 0 && HouseGoal.Complete(result),
                    "Four-family completion requires optional shop upgrade or lost completed projects");
                result = session.ExecuteSpin(5);
                Require(result.coins == 10 && HouseGoal.Complete(result) && result.selectedStage == 0, "Completed state invents a successor or changes manual rewards");
            }
            passed.Add("Both four-project build orders debit1140 exactly; all four families required, slotoptional, manual completion spin8+1+1 remains10");
            return passed.AsReadOnly();
        }
    }
}
