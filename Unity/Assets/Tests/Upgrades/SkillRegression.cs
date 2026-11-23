using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests.Upgrades
{
    public static class SkillRegression
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Fails<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        static SkillContext Context(string era, int house, int workshop, int recycler, int pigeon, int cart, int power, string selected = null) =>
            new SkillContext(new FamilyStages(era, new[] { new FamilyStage(FamilyIds.House, house), new FamilyStage(FamilyIds.Workshop, workshop),
                new FamilyStage(FamilyIds.Recycler, recycler), new FamilyStage(FamilyIds.PigeonLoft, pigeon), new FamilyStage(FamilyIds.Cart, cart),
                new FamilyStage(FamilyIds.PowerSource, power) }), selected);
        static SkillContext Era1(int house = 2, int recycler = 2, int pigeon = 2, string selected = null) => Context(EraIds.Junkyard, house, 2, recycler, pigeon, 2, 2, selected);
        static SkillContext Era2(int house = 4, int recycler = 4, int pigeon = 4, string selected = null) => Context(EraIds.WorkshopYard, house, 4, recycler, pigeon, 4, 4, selected);
        static SkillPurchaseResult Buy(string node, long sequence, long wallet, SkillContext context, SkillOwnership state, string operation = null)
        {
            var quote = SkillTree.Quote(node, operation ?? "test.purchase-" + sequence, sequence, wallet, context, state);
            return SkillTree.Propose(sequence - 1, wallet, context, state, quote.Request);
        }
        public static IReadOnlyList<string> Run()
        {
            var groups = new List<string>();
            var empty = SkillOwnership.Empty;
            string emptyJson = SkillOwnershipCodec.Encode(empty);
            string[] ids = { GameIds.SlotCoinBoost, SkillTree.UniversalTier2, SkillTree.RecyclingTier1,
                SkillTree.RecyclingTier2, SkillTree.DeliveryTier1, SkillTree.DeliveryTier2 };
            long[] prices = { 120, 160, 160, 90, 60, 20 };
            Check(SkillTree.Nodes.Count == 6, "Exactly six nodes");
            for (int i = 0; i < 6; i++)
            {
                var node = SkillTree.Find(ids[i]);
                Check(node.Price == prices[i] && node.LevelCap == 1 && node.Provisional && node.CurrencyId == "coins", "Explicit offer metadata");
                var quote = SkillTree.Quote(ids[i], "preview.test", 1, 10000, Era2(), empty);
                Check(quote.CurrentEffects.UniversalCoins == 0 && quote.CurrentEffects.CoinsPerTrash == 0 && quote.CurrentEffects.CoinsPerDelivery == 0, "Preview current zero");
                Check(quote.Status == (i % 2 == 0 ? OfferStatus.Available : OfferStatus.Locked), "Tier prerequisites");
                Check(!quote.Owned && emptyJson == SkillOwnershipCodec.Encode(empty), "No preview mutation");
                if (i % 2 == 0) Check(quote.NextUnlocks.Count == 1 && quote.NextUnlocks[0].Node.Id == ids[i + 1] &&
                    quote.NextUnlocks[0].PrerequisitesMetAfterPurchase, "Era2 quote exposes next tier prerequisites");
                if (i == 0) Check(quote.NextEffects.UniversalCoins == 1 && quote.NextEffects.CoinsPerTrash == 0 && quote.NextEffects.CoinsPerDelivery == 0, "Universal quote next scalar");
                if (i == 2) Check(quote.NextEffects.UniversalCoins == 0 && quote.NextEffects.CoinsPerTrash == 1 && quote.NextEffects.CoinsPerDelivery == 0, "Recycling quote next scalar");
                if (i == 4) Check(quote.NextEffects.UniversalCoins == 0 && quote.NextEffects.CoinsPerTrash == 0 && quote.NextEffects.CoinsPerDelivery == 1, "Delivery quote next scalar");
            }
            groups.Add("six registered immutable quotes, independent prices and no-write preview");

            var state = empty; long wallet = 10000; long sequence = 10;
            SkillPurchaseProposal first = null;
            for (int i = 0; i < 6; i++)
            {
                var context = i == 0 ? Era1() : Era2();
                var quote = SkillTree.Quote(ids[i], "buy.node-" + i, sequence, wallet, context, state);
                var result = SkillTree.Propose(sequence - 1, wallet, context, state, quote.Request);
                Check(result.Status == PurchaseStatus.Proposed, "Every node purchasable");
                var p = result.Proposal;
                Check(p.FrozenPrice == prices[i] && p.WalletAfter == wallet - prices[i] && p.OwnershipAfter.Revision == i + 1 && p.OperationFormat == 2 && p.RuleRevision == "chapter.rules.v1", "Independent wallet and cap");
                Check(p.MatchesPreconditions(sequence - 1, wallet, context, state) && !p.MatchesPreconditions(sequence, wallet, context, state), "Frozen preconditions");
                Check(!p.MatchesPreconditions(sequence - 1, wallet + 1, context, state), "Wallet precondition");
                Check(!p.MatchesPreconditions(sequence - 1, wallet, i == 0 ? Era1(selected: "house.improve") : Era2(selected: "house.workshop-yard.upgrade-1"), state), "Selection precondition");
                if (first == null) first = p;
                state = p.OwnershipAfter; wallet = p.WalletAfter;
                Check(Buy(ids[i], sequence + 1, wallet, context, state).Status == PurchaseStatus.Maxed, "Every node capped");
                sequence += 7;
            }
            Check(wallet == 9390 && state.Receipts.Count == 6 && empty.Revision == 0, "Six purchases total610 with interleaved commands");
            var full = SkillTree.EvaluateModifiers(Era2(), state);
            Check(full.UniversalCoins == 2 && full.CoinsPerTrash == 2 && full.CoinsPerDelivery == 2, "Final additive branch scalars");
            groups.Add("each node purchase, frozen debit, six caps and interleaved global sequences");

            foreach (var receipt in state.Receipts)
            {
                var replay = SkillTree.Propose(1000, 3, Era2(5, 5, 5), state, receipt.Request);
                Check(replay.Status == PurchaseStatus.Replay && replay.Proposal == null, "No replacement wallet on replay");
                var r = receipt.Request;
                var changed = new[] {
                    new SkillPurchaseRequest(r.NodeId, r.OperationId, r.Sequence, r.ExpectedWallet + 1, r.ExpectedOwnership, r.Context),
                    new SkillPurchaseRequest(r.NodeId, "changed.operation", r.Sequence, r.ExpectedWallet, r.ExpectedOwnership, r.Context),
                    new SkillPurchaseRequest(r.NodeId, r.OperationId, r.Sequence + 1, r.ExpectedWallet, r.ExpectedOwnership, r.Context),
                    new SkillPurchaseRequest(r.NodeId, r.OperationId, r.Sequence, r.ExpectedWallet, "empty", Era2(selected: "house.workshop-yard.upgrade-1")),
                    new SkillPurchaseRequest(r.NodeId == ids[0] ? ids[2] : ids[0], r.OperationId, r.Sequence, r.ExpectedWallet, r.ExpectedOwnership, r.Context) };
                foreach (var request in changed) Check(SkillTree.Propose(1000, 3, Era2(), state, request).Status == PurchaseStatus.Conflict, "Changed replay conflicts");
            }
            var staleQuote = SkillTree.Quote(ids[0], "stale.quote", 100, 157, Era1(), empty);
            Check(SkillTree.Propose(100, 157, Era1(), empty, staleQuote.Request).Status == PurchaseStatus.Stale, "Stale sequence");
            Check(SkillTree.Propose(90, 157, Era1(), empty, staleQuote.Request).Status == PurchaseStatus.Conflict, "Sequence gap");
            Check(SkillTree.Propose(99, 158, Era1(), empty, staleQuote.Request).Status == PurchaseStatus.Conflict, "Changed wallet");
            Check(SkillTree.Propose(99, 157, Era1(selected: "house.improve"), empty, staleQuote.Request).Status == PurchaseStatus.Conflict, "Changed selected construction");
            Check(SkillTree.Propose(99, 157, Era1(recycler: 1), empty, staleQuote.Request).Status == PurchaseStatus.Conflict, "Changed unrelated building");
            Check(SkillTree.Propose(1000, 1, Era2(), first.OwnershipAfter, state.Receipts[1].Request).Status == PurchaseStatus.Stale, "Old request with missing receipt never reexecutes");
            groups.Add("all receipts replay without writes; changed requests, selections and stale inputs fail closed");

            Check(Buy(ids[0], 1, 157, Era1(house: 0), empty).Status == PurchaseStatus.Locked, "Universal actual house prerequisite");
            Check(Buy(ids[2], 1, 1000, Era1(recycler: 0), empty).Status == PurchaseStatus.Locked, "Recycler prerequisite");
            Check(Buy(ids[4], 1, 1000, Era1(pigeon: 0), empty).Status == PurchaseStatus.Locked, "Loft prerequisite");
            for (int i = 0; i < 6; i += 2)
            {
                var tier1 = Buy(ids[i], 1, 1000, Era1(), empty).Proposal.OwnershipAfter;
                Check(Buy(ids[i + 1], 2, 1000, Era1(), tier1).Status == PurchaseStatus.Locked, "Tier2 requires era2");
                var low = i == 0 ? Era2(house: 3) : i == 2 ? Era2(recycler: 3) : Era2(pigeon: 3);
                Check(Buy(ids[i + 1], 2, 1000, low, tier1).Status == PurchaseStatus.Locked, "Tier2 actual era2local1");
                Check(Buy(ids[i], 1, prices[i] - 1, Era1(), empty).Status == PurchaseStatus.InsufficientFunds, "Tier1 insufficient");
                Check(Buy(ids[i + 1], 2, prices[i + 1] - 1, Era2(), tier1).Status == PurchaseStatus.InsufficientFunds, "Tier2 insufficient");
                Check(Buy(ids[i + 1], 2, prices[i + 1], Era2(), tier1).Proposal.WalletAfter == 0, "Exact tier2 funds");
            }
            Check(Buy(ids[0], 1, 157, Era1(), empty).Proposal.WalletAfter == 37, "Legacy quote157to37");
            Check(Buy(ids[0], long.MaxValue, long.MaxValue, Era1(), empty).Proposal.WalletAfter == long.MaxValue - 120, "Checked long wallet");
            Check(SkillTree.Propose(long.MaxValue, 1000, Era2(), empty, staleQuote.Request).Status == PurchaseStatus.Overflow, "Sequence exhaustion");
            Check(SkillTree.Propose(-1, 1000, Era2(), empty, staleQuote.Request).Status == PurchaseStatus.InvalidRequest, "Invalid sequence");
            Fails<ArgumentException>(() => SkillTree.Quote(ids[0], "x", 1, -1, Era1(), empty));
            Fails<ArgumentException>(() => Context("unknown.era", 0, 0, 0, 0, 0, 0));
            Fails<ArgumentException>(() => Era1(house: 6));
            Fails<ArgumentException>(() => Era1(house: 3));
            Fails<ArgumentException>(() => Era2(house: 2));
            Fails<ArgumentException>(() => Era1(selected: "unknown.project"));
            Fails<ArgumentException>(() => Era2(selected: "house.improve"));
            groups.Add("every branch unlock, era and actual-stage gates, funds and long boundaries");

            var universal = Buy(ids[0], 1, 1000, Era1(), empty).Proposal.OwnershipAfter;
            var recycle = Buy(ids[2], 1, 1000, Era1(), empty).Proposal.OwnershipAfter;
            var delivery = Buy(ids[4], 1, 1000, Era1(), empty).Proposal.OwnershipAfter;
            Fails<ArgumentException>(() => SkillTree.EvaluateModifiers(Era1(recycler: 0), recycle));
            Fails<ArgumentException>(() => SkillTree.ValidateOwnership(Era1(pigeon: 0), delivery));
            Fails<ArgumentException>(() => SkillTree.ValidateOwnership(Era1(house: 0), universal));
            var eraEntry = Context(EraIds.WorkshopYard, 3, 3, 3, 3, 3, 3);
            Check(SkillTree.EvaluateModifiers(eraEntry, recycle).CoinsPerTrash == 1 &&
                SkillTree.EvaluateModifiers(eraEntry, delivery).CoinsPerDelivery == 1 &&
                SkillTree.EvaluateModifiers(eraEntry, universal).UniversalCoins == 1, "Era entry local0 preserves actual owned buildings/skills");
            Check(SkillTree.Quote(ids[1], "era.entry.quote", 2, 1000, eraEntry, universal).Status == OfferStatus.Locked, "Tier2 remains gated until actuallocal1");
            Check(!SkillTree.Quote(ids[0], "era.one.quote", 1, 1000, Era1(), empty).NextUnlocks[0].PrerequisitesMetAfterPurchase, "Quote retains unmet era gate");
            for (int left = 0; left < 4; left++) for (int middle = 0; middle < 4; middle++) for (int right = 0; right < 4; right++)
            {
                var outcome = new SpinOutcome(left, middle, right);
                var u = SkillTree.EvaluateModifiers(Era2(), universal); var r = SkillTree.EvaluateModifiers(Era2(), recycle); var d = SkillTree.EvaluateModifiers(Era2(), delivery);
                int expectedTrash = (left == 2 ? 1 : 0) + (middle == 2 ? 1 : 0) + (right == 2 ? 1 : 0);
                Check(u.UniversalCoins == 1 && u.CoinsPerTrash == 0 && u.CoinsPerDelivery == 0, "Uniform scalar");
                Check(r.UniversalCoins == 0 && r.CoinsPerTrash * outcome.Count(2) == expectedTrash && r.CoinsPerDelivery == 0, "Qualifying trash only");
                Check(d.UniversalCoins == 0 && d.CoinsPerTrash == 0 && d.CoinsPerDelivery == 1, "Delivery scalar separate from appearances");
                Check(SkillTree.EvaluateModifiers(Era2(), empty).UniversalCoins == 0, "Acquisition spin uses before ownership");
            }
            Check(checked(full.CoinsPerDelivery * 0) == 0 && checked(full.CoinsPerDelivery * 2) == 4, "Zero vs two completed deliveries");
            Fails<OverflowException>(() => { long value = checked(full.CoinsPerDelivery * long.MaxValue); });
            groups.Add("all64 frozen outcomes, separate qualifying branches and checked delivery arithmetic");

            string encoded = SkillOwnershipCodec.Encode(state);
            var decoded = SkillOwnershipCodec.DecodeOrDefault(encoded);
            Check(SkillOwnershipCodec.Encode(decoded) == encoded && decoded.Token == state.Token, "Six-receipt roundtrip");
            foreach (var receipt in state.Receipts) Check(SkillTree.Propose(1000, 0, Era2(), decoded, receipt.Request).Status == PurchaseStatus.Replay, "Serialized replay");
            Check(SkillOwnershipCodec.DecodeOrDefault(null).Revision == 0 && SkillOwnershipCodec.Encode(empty) == emptyJson, "Missing optional field no write/grant");
            string[] invalid = { "", "null", "{}", "{\"version\":1,\"receipts\":[]}", "{\"version\":2,\"receipts\":[],\"extra\":0}",
                "{\"version\":2,\"version\":2,\"receipts\":[]}", emptyJson + "x", emptyJson.Replace(":2", ":02"), emptyJson.Replace(":2", ":2.0"),
                emptyJson.Replace("{", "{\u00a0"), encoded.Replace("\"price\":120", "\"price\":119"), encoded.Replace("\"legacy\":0", "\"legacy\":2"),
                encoded.Replace("\"node\":\"slot.coin-boost\"", "\"node\":\"unknown.node\""), encoded.Replace("\"sequence\":10", "\"sequence\":0"),
                encoded.Replace("\"wallet\":10000", "\"wallet\":-1"), encoded.Replace("\"ownership\":\"empty\"", "\"ownership\":\"changed\""),
                new string(' ', SkillOwnershipCodec.MaximumJsonLength + 1) };
            foreach (string json in invalid) Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(json));
            string one = SkillOwnershipCodec.Encode(first.OwnershipAfter);
            Check(SkillOwnershipCodec.DecodeOrDefault("{\"receipts\":[],\"version\":2}").Revision == 0, "Root field order is independent");
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(one.Replace("\"house\":2", "\"house\":0")));
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(one.Replace("\"selected\":\"-\"", "\"selected\":\"unknown.project\"")));
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(one.Replace("\"price\":120", "\"price\":120,\"price\":120")));
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(one.Replace("\"node\":\"slot.coin-boost\"", "\"node\":\"skill.universal.2\"").Replace("\"price\":120", "\"price\":360")));
            string receiptJson = one.Substring(one.IndexOf('[') + 1); receiptJson = receiptJson.Substring(0, receiptJson.Length - 2);
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault("{\"version\":2,\"receipts\":[" + string.Join(",", new[] {receiptJson,receiptJson,receiptJson,receiptJson,receiptJson,receiptJson,receiptJson}) + "]}"));
            groups.Add("strict bounded six-receipt codec, malformed state rejection and missing legacy defaults");

            var oldRequest = new UpgradePurchaseRequest(GameIds.SlotCoinBoost, 42, 0, 157, 1);
            var old = CoinBoostShop.Propose(41, 157, 1, UpgradeOwnership.Empty, oldRequest).Proposal.OwnershipAfter;
            var migrated = SkillOwnership.Migrate(old);
            Check(migrated.Revision == 1 && migrated.Owns(ids[0]) && !migrated.Owns(ids[1]) && migrated.Receipts[0].Legacy, "Migration preserves paid node only");
            Check(SkillTree.ReplayLegacy(900, 999, 2, migrated, oldRequest) == PurchaseStatus.Replay, "Original old replay survives migration");
            Check(SkillTree.ReplayLegacy(900, 999, 2, migrated, new UpgradePurchaseRequest(ids[0], 42, 0, 158, 1)) == PurchaseStatus.Conflict, "Changed legacy request conflicts");
            Check(Buy(ids[0], 901, 999, Era2(), migrated).Status == PurchaseStatus.Maxed, "No recharge");
            var upgrade2 = Buy(ids[1], 901, 999, Era2(), migrated).Proposal.OwnershipAfter;
            var reloadedMigration = SkillOwnershipCodec.DecodeOrDefault(SkillOwnershipCodec.Encode(upgrade2));
            Check(SkillTree.ReplayLegacy(902, 0, 2, reloadedMigration, oldRequest) == PurchaseStatus.Replay &&
                SkillTree.EvaluateModifiers(Era2(), reloadedMigration).UniversalCoins == 2, "Old receipt retained after new purchase/reload");
            Check(SkillOwnership.Migrate(UpgradeOwnership.Empty).Revision == 0, "Unowned migration no grant");
            Check(SkillOwnershipCodec.DecodeOrMigrate(null, old).Token == migrated.Token, "Missing skill state explicitly migrates paid legacy slot");
            Check(SkillOwnershipCodec.DecodeOrMigrate(SkillOwnershipCodec.Encode(reloadedMigration), old).Token == reloadedMigration.Token, "Present state preserves old receipt exactly");
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrMigrate(emptyJson, old));
            Check(migrated.Receipts[0].Request.Context == null && migrated.Receipts[0].LegacyHouseStage == 1, "Migration does not invent original era/selection/context");
            Check(SkillTree.ReplayLegacy(50, 0, 2, reloadedMigration, oldRequest) == PurchaseStatus.Conflict, "Legacy replay rejects aggregate sequence behind later receipt");
            Fails<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(SkillOwnershipCodec.Encode(migrated).Replace("\"era\":0", "\"era\":1")));
            groups.Add("explicit old ownership migration preserves exact receipt, replay and no-recharge cap");

            // Independent construction fixture: the domain observes completion; it never requires skill ownership to build.
            var allConstructionComplete = Context(EraIds.WorkshopYard, 5, 5, 5, 5, 5, 5);
            foreach (var node in SkillTree.Nodes)
                SkillTree.Quote(node.Id, "completion.preview", 1, 10000, allConstructionComplete, empty);
            Check(empty.Revision == 0 && SkillOwnershipCodec.Encode(empty) == emptyJson && allConstructionComplete.HouseStage == 5 &&
                allConstructionComplete.RecyclerLevel == 5 && allConstructionComplete.PowerSourceStage == 5, "No implicit purchase or chapter gate");
            groups.Add("completed two-era construction fixture remains skill-free and unchanged; chapter logic integration-owned");
            return groups;
        }
    }
}
