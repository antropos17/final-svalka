using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests.Upgrades
{
    public static class PricingRegression
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        static SkillContext Context(int stage = 4, string selected = "house.workshop-yard.upgrade-2")
        {
            var entries = new List<FamilyStage>();
            foreach (string id in FamilyIds.All) entries.Add(new FamilyStage(id, stage));
            return new SkillContext(new FamilyStages(EraIds.WorkshopYard, entries), selected);
        }
        public static IReadOnlyList<string> Run()
        {
            var groups = new List<string>();
            string[] ids = { SkillIds.Universal1, SkillIds.Universal2, SkillIds.Recycling1,
                SkillIds.Recycling2, SkillIds.Delivery1, SkillIds.Delivery2 };
            long[] oldPrices = { 120, 360, 160, 480, 180, 540 };
            long[] newPrices = { 120, 160, 160, 90, 60, 20 };
            var context = Context();
            long oldWallet = 10000;
            for (int i = 0; i < R8SkillFixtures.Ownership.Length; i++)
            {
                var state = SkillOwnershipCodec.DecodeOrDefault(R8SkillFixtures.Ownership[i]);
                Check(state.Version == 2 && state.Revision == i && SkillOwnershipCodec.Encode(state) == R8SkillFixtures.Ownership[i], "Literal r8 bytes unchanged");
                if (i == 0) continue;
                var receipt = state.Receipts[i - 1];
                var before = SkillOwnershipCodec.DecodeOrDefault(R8SkillFixtures.Ownership[i - 1]);
                Check(receipt.PricingRevision == SkillPricing.LegacyRevision && receipt.FrozenPrice == oldPrices[i - 1], "Literal original debit retained");
                Check(receipt.Request.ExpectedOwnership == before.Token, "Exact old ownership token remains the next request precondition");
                var restored = SkillTree.RestorePurchase(i - 1, oldWallet, context, before, receipt.Request, oldPrices[i - 1]);
                Check(restored.WalletAfter == oldWallet - oldPrices[i - 1] && SkillOwnershipCodec.Encode(restored.OwnershipAfter) == R8SkillFixtures.Ownership[i], "Old pending result restored at original price");
                Check(SkillTree.Propose(i - 1, oldWallet, context, before, receipt.Request).Status == PurchaseStatus.Stale, "Unapplied old quote is stale for new commands");
                var replay = SkillTree.Propose(100, 3, Context(5, null), state, receipt.Request);
                Check(replay.Status == PurchaseStatus.Replay && replay.Proposal == null, "Applied historical replay cannot replace live wallet");
                oldWallet -= oldPrices[i - 1];
            }
            Check(oldWallet == 8160, "Historical six purchases still cost1840, no refund");
            groups.Add("seven literal r8 snapshots preserve bytes/tokens/debits, frozen v1 recovery and no-op replay");

            var current = SkillOwnership.Empty; long wallet = 10000;
            for (int i = 0; i < ids.Length; i++)
            {
                Check(SkillPricing.Price(ids[i], SkillPricing.LegacyRevision) == oldPrices[i] &&
                    SkillPricing.Price(ids[i], SkillPricing.CurrentRevision) == newPrices[i], "Independent fixed price tables");
                var quote = SkillTree.Quote(ids[i], "pricing.buy-" + i, i + 1, wallet, context, current);
                Check(quote.Node.Price == newPrices[i] && quote.Node.PricingRevision == SkillPricing.CurrentRevision &&
                    quote.Request.PricingRevision == SkillPricing.CurrentRevision, "New quote explicitly pins current prices");
                Check(SkillTree.Find(ids[i], SkillPricing.LegacyRevision).Price == oldPrices[i], "Historical preview lookup");
                var result = SkillTree.Propose(i, wallet, context, current, quote.Request);
                var proposal = result.Proposal;
                Check(result.Status == PurchaseStatus.Proposed && proposal.PricingRevision == SkillPricing.CurrentRevision &&
                    proposal.FrozenPrice == newPrices[i] && proposal.RuleRevision == "chapter.rules.v1", "Price changes do not change effect revision");
                var restored = SkillTree.RestorePurchase(i, wallet, context, current, quote.Request, newPrices[i]);
                Check(restored.OwnershipAfter.Token == proposal.OwnershipAfter.Token, "Current frozen restore equals original proposal");
                current = proposal.OwnershipAfter; wallet = proposal.WalletAfter;
                var r = quote.Request;
                var changedRevision = new SkillPurchaseRequest(r.NodeId, r.OperationId, r.Sequence, r.ExpectedWallet, r.ExpectedOwnership, r.Context, SkillPricing.LegacyRevision);
                Check(SkillTree.Propose(100, 0, context, current, changedRevision).Status == PurchaseStatus.Conflict, "Revision participates in replay even when node price is unchanged");
            }
            Check(wallet == 9390 && current.Version == 3, "Current six prices total610");
            var modifiers = SkillTree.EvaluateModifiers(context, current);
            Check(modifiers.UniversalCoins == 2 && modifiers.CoinsPerTrash == 2 && modifiers.CoinsPerDelivery == 2, "All effects remain2, no extra benefit");
            string encoded = SkillOwnershipCodec.Encode(current);
            Check(SkillOwnershipCodec.Encode(SkillOwnershipCodec.DecodeOrDefault(encoded)) == encoded, "Current version3 roundtrip");
            groups.Add("six current quotes/debits and restored operations pin the active price revision while effects stay unchanged");

            var legacySlot = UpgradeOwnershipCodec.DecodeOrDefault("{\"version\":1,\"level\":1,\"revision\":1,\"sequence\":42,\"walletBefore\":157,\"houseStage\":1}");
            var migrated = SkillOwnership.Migrate(legacySlot);
            string oldToken = migrated.Token;
            var next = SkillTree.Quote(SkillIds.Recycling1, "mixed.buy", 50, 1000, context, migrated).Request;
            var mixed = SkillTree.Propose(49, 1000, context, migrated, next).Proposal.OwnershipAfter;
            Check(mixed.Version == 3 && mixed.Receipts[0].PricingRevision == SkillPricing.LegacyRevision && mixed.Receipts[1].PricingRevision == SkillPricing.CurrentRevision,
                "Mixed history keeps per-receipt revision");
            Check(mixed.Receipts[1].Request.ExpectedOwnership == oldToken, "Historical token never rewritten in mixed chain");
            string mixedJson = SkillOwnershipCodec.Encode(mixed);
            var mixedReload = SkillOwnershipCodec.DecodeOrMigrate(mixedJson, legacySlot);
            Check(mixedReload.Token == mixed.Token && mixedReload.Receipts[0].FrozenPrice == 120 && next.ExpectedWallet == 1000, "Migration adds no refund/debit/grant");
            Check(SkillTree.ReplayLegacy(100, 0, 2, mixedReload, new UpgradePurchaseRequest(SkillIds.Universal1, 42, 0, 157, 1)) == PurchaseStatus.Replay, "r7 boost replay survives mixed reload");
            Check(SkillTree.Propose(100, 0, context, mixedReload, next).Status == PurchaseStatus.Replay, "Mixed current replay survives reload");
            groups.Add("r7 legacy boost plus new purchase preserves exact old identity, mixed receipts and replay");

            var oldBeforeDelivery = SkillOwnershipCodec.DecodeOrDefault(R8SkillFixtures.Ownership[4]);
            var oldAfterDelivery = SkillOwnershipCodec.DecodeOrDefault(R8SkillFixtures.Ownership[5]);
            var oldRequest = oldAfterDelivery.Receipts[4].Request;
            Reject<ArgumentException>(() => SkillTree.RestorePurchase(4, oldRequest.ExpectedWallet, context, oldBeforeDelivery, oldRequest, 60));
            var originalConstructor = new SkillPurchaseRequest(oldRequest.NodeId, oldRequest.OperationId, oldRequest.Sequence,
                oldRequest.ExpectedWallet, oldRequest.ExpectedOwnership, oldRequest.Context);
            Check(originalConstructor.PricingRevision == SkillPricing.LegacyRevision, "Existing constructor means historical pricing, never retagged");
            Check(SkillTree.Propose(4, oldRequest.ExpectedWallet, context, oldBeforeDelivery, originalConstructor).Status == PurchaseStatus.Stale, "Stale original request cannot be silently repriced");
            Reject<ArgumentException>(() => SkillPricing.Price(SkillIds.Delivery1, "skill.prices.unknown"));
            Reject<ArgumentException>(() => SkillPricing.Price("unknown.node", SkillPricing.CurrentRevision));
            var unknown = new SkillPurchaseRequest(SkillIds.Universal1, "unknown.price", 1, 1000, "empty", context, "skill.prices.unknown");
            Check(SkillTree.Propose(0, 1000, context, SkillOwnership.Empty, unknown).Status == PurchaseStatus.InvalidRequest, "Unknown pricing fails closed");
            Reject<ArgumentException>(() => SkillTree.RestorePurchase(0, 1000, context, SkillOwnership.Empty, unknown, 120));
            Reject<ArgumentException>(() => SkillTree.RestorePurchase(49, 1000, Context(4, null), migrated, next, 160));
            groups.Add("stale v1 quote rejection, pinned historical price and full frozen context validation");

            string revision = ",\"priceRevision\":\"skill.prices.v3\"";
            var one = SkillTree.Propose(0, 1000, context, SkillOwnership.Empty,
                SkillTree.Quote(SkillIds.Universal1, "one.current", 1, 1000, context, SkillOwnership.Empty).Request).Proposal.OwnershipAfter;
            string oneJson = SkillOwnershipCodec.Encode(one);
            foreach (string invalid in new[] {
                oneJson.Replace(revision, ""), oneJson.Replace("skill.prices.v3", "skill.prices.unknown"),
                oneJson.Replace(revision, revision + revision), oneJson.Replace("\"version\":3", "\"version\":2"),
                oneJson.Replace("skill.prices.v3", "skill.prices.v1"), mixedJson.Replace(",\"priceRevision\":\"skill.prices.v1\"", ""),
                R8SkillFixtures.Ownership[1].Replace("\"version\":2", "\"version\":3"),
                oneJson.Replace("\"price\":120", "\"price\":119") })
                Reject<FormatException>(() => SkillOwnershipCodec.DecodeOrDefault(invalid));
            Check(SkillOwnershipCodec.Encode(SkillOwnershipCodec.DecodeOrDefault(null)) == R8SkillFixtures.Ownership[0], "Missing ownership stays empty version2 without grant");
            groups.Add("required bounded per-receipt revisions, strict old/current/mixed schema and malformed-state rejection");

            var maximal = SkillOwnership.Empty; long maxWallet = long.MaxValue;
            for (int i = 0; i < ids.Length; i++)
            {
                string operation = "p" + new string('a', 62) + i;
                var request = SkillTree.Quote(ids[i], operation, i + 1, maxWallet, context, maximal).Request;
                var proposal = SkillTree.Propose(i, maxWallet, context, maximal, request).Proposal;
                maximal = proposal.OwnershipAfter; maxWallet = proposal.WalletAfter;
            }
            string largest = SkillOwnershipCodec.Encode(maximal);
            Check(maximal.Token.Length <= 2048 && largest.Length <= SkillOwnershipCodec.MaximumJsonLength &&
                SkillOwnershipCodec.DecodeOrDefault(largest).Token == maximal.Token, "Maximum operation IDs and long wallets fit bounded codec");
            Check(maxWallet == long.MaxValue - 610 && context.SelectedConstruction == "house.workshop-yard.upgrade-2", "Checked subtraction preserves selected construction");
            groups.Add("six maximum-size revisioned receipts fit codec/token bounds with checked long wallets");
            return groups;
        }
    }
}
