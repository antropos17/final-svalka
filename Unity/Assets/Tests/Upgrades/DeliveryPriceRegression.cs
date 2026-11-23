using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;
namespace JunkToNeon.Tests.Upgrades
{
    public static class DeliveryPriceRegression
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        static SkillContext Context()
        {
            var entries = new List<FamilyStage>();
            foreach (var id in FamilyIds.All) entries.Add(new FamilyStage(id, 4));
            return new SkillContext(new FamilyStages(EraIds.WorkshopYard, entries), "house.workshop-yard.upgrade-2");
        }
        public static IReadOnlyList<string> Run()
        {
            var groups = new List<string>(); var context = Context();
            long[] v2 = {120,160,160,90,60,5}; long wallet = 10000;
            for (int i = 0; i < V2SkillFixtures.Ownership.Length; i++)
            {
                var after = SkillOwnershipCodec.DecodeOrDefault(V2SkillFixtures.Ownership[i]);
                Check(SkillOwnershipCodec.Encode(after) == V2SkillFixtures.Ownership[i], "Literal bff8b5 v2 JSON and tokens unchanged");
                if (i == 0) continue;
                var before = SkillOwnershipCodec.DecodeOrDefault(V2SkillFixtures.Ownership[i-1]);
                var receipt = after.Receipts[i-1];
                Check(receipt.PricingRevision == SkillPricing.RevisionV2 && receipt.FrozenPrice == v2[i-1], "Every v2 frozen price remains pinned");
                var restored = SkillTree.RestorePurchase(i-1,wallet,context,before,receipt.Request,v2[i-1]);
                Check(SkillOwnershipCodec.Encode(restored.OwnershipAfter) == V2SkillFixtures.Ownership[i] && restored.WalletAfter == wallet-v2[i-1], "Original pending result restored exactly");
                Check(SkillTree.Propose(i-1,wallet,context,before,receipt.Request).Status == PurchaseStatus.Stale, "Unapplied v2 quote rejected after activation");
                var replay = SkillTree.Propose(100,0,context,after,receipt.Request);
                Check(replay.Status == PurchaseStatus.Replay && replay.Proposal == null, "Exact v2 applied replay grants/spends nothing");
                wallet -= v2[i-1];
            }
            Check(wallet == 9405, "Old profile still spent595, no refund or reprice");
            groups.Add("seven literal v2 snapshots preserve original debits/tokens, pending recovery and replay");

            string[] ids = {SkillIds.Universal1,SkillIds.Universal2,SkillIds.Recycling1,SkillIds.Recycling2,SkillIds.Delivery1,SkillIds.Delivery2};
            for (int i = 0; i < ids.Length; i++)
            {
                Check(SkillTree.Find(ids[i],SkillPricing.RevisionV2).Price == v2[i], "Explicit v2 lookup never falls back to v1");
                Check(SkillPricing.Price(ids[i],SkillPricing.RevisionV3) == (i == 5 ? 20 : v2[i]), "Only delivery2 changes");
            }
            Check(SkillPricing.CurrentRevision == "skill.prices.v3" && SkillPricing.Price(SkillIds.Delivery2,SkillPricing.RevisionV1) == 540,
                "All three immutable revisions remain distinct");
            var five = SkillOwnershipCodec.DecodeOrDefault(V2SkillFixtures.Ownership[5]);
            var oldSix = SkillOwnershipCodec.DecodeOrDefault(V2SkillFixtures.Ownership[6]);
            var oldRequest = oldSix.Receipts[5].Request;
            var quote = SkillTree.Quote(SkillIds.Delivery2,"retune.delivery2",6,9410,context,five);
            Check(quote.Node.Price == 20 && quote.Request.PricingRevision == SkillPricing.RevisionV3, "New quote explicitly pins20/v3");
            var current = SkillTree.Propose(5,9410,context,five,quote.Request).Proposal;
            Check(current != null && current.WalletAfter == 9390 && current.OwnershipAfter.Receipts[5].FrozenPrice == 20,
                "New purchase debits20 once, other five purchases untouched");
            Check(current.OwnershipAfter.Receipts[4].PricingRevision == SkillPricing.RevisionV2 && current.OwnershipAfter.Receipts[5].PricingRevision == SkillPricing.RevisionV3,
                "Mixed v2/v3 history is explicit");
            var reload = SkillOwnershipCodec.DecodeOrDefault(SkillOwnershipCodec.Encode(current.OwnershipAfter));
            Check(reload.Token == current.OwnershipAfter.Token && SkillTree.Propose(100,0,context,reload,quote.Request).Status == PurchaseStatus.Replay,
                "Current replay survives mixed-history reload");
            Check(SkillTree.Quote(SkillIds.Delivery2,"owned.preview",101,0,context,oldSix).Status == OfferStatus.Maxed,
                "Previously owned5-coin node never recharges");
            groups.Add("revision3 changes only delivery2 to20 and preserves mixed v2/v3 ownership");

            Reject<ArgumentException>(()=>SkillTree.RestorePurchase(5,9410,context,five,oldRequest,20));
            Reject<ArgumentException>(()=>SkillTree.RestorePurchase(5,9410,context,five,quote.Request,5));
            var retagged = new SkillPurchaseRequest(oldRequest.NodeId,oldRequest.OperationId,oldRequest.Sequence,oldRequest.ExpectedWallet,
                oldRequest.ExpectedOwnership,oldRequest.Context,SkillPricing.RevisionV3);
            Check(SkillTree.Propose(100,0,context,oldSix,retagged).Status == PurchaseStatus.Conflict, "Revision change cannot masquerade as historical replay");
            Reject<FormatException>(()=>SkillOwnershipCodec.DecodeOrDefault(V2SkillFixtures.Ownership[6].Replace("skill.prices.v2","skill.prices.v3")));
            var oldEffects = SkillTree.EvaluateModifiers(context,oldSix); var newEffects = SkillTree.EvaluateModifiers(context,reload);
            Check(oldEffects.UniversalCoins == 2 && oldEffects.CoinsPerTrash == 2 && oldEffects.CoinsPerDelivery == 2 &&
                newEffects.UniversalCoins == 2 && newEffects.CoinsPerTrash == 2 && newEffects.CoinsPerDelivery == 2 &&
                current.Request.Context.SelectedConstruction == context.SelectedConstruction, "Identical effects and preserved selected construction");
            groups.Add("retagged or repriced frozen data fail closed; effects and selected construction stay unchanged");
            return groups;
        }
    }
}
