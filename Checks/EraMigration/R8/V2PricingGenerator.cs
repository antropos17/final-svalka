using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

// ONLY the actual pinned bff8b564 Contracts/Upgrades modules are compiled here.
internal static class V2PricingGenerator
{
    public static void Main(string[] args)
    {
        using var raw=JsonDocument.Parse(File.ReadAllText(args[1]));
        string oldPayload=raw.RootElement.GetProperty("chapter").GetProperty("skills").GetString();
        var before=SkillOwnershipCodec.DecodeOrDefault(oldPayload);
        int[] levels={4,3,4,4,3,4};var stages=new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select((id,i)=>new FamilyStage(id,levels[i])));
        var context=new SkillContext(stages,"power-source.workshop-yard.upgrade-2");
        var quote=SkillTree.Quote(SkillIds.Delivery2,"skillbuy.21",21,9820,context,before);
        if(quote.Request.PricingRevision!="skill.prices.v2"||quote.Node.Price!=5)throw new Exception("Pinned V2 price was not5");
        var result=SkillTree.Propose(20,9820,context,before,quote.Request);
        if(result.Status!=PurchaseStatus.Proposed||result.Proposal.WalletAfter!=9815)throw new Exception("Pinned V2 proposal differs");
        var proposal=result.Proposal;
        var fixture=new{
            fixtureKind="pure-module-v2-frozen-proposal",pricingRevision=quote.Request.PricingRevision,node=quote.Request.NodeId,
            operation=quote.Request.OperationId,sequence=21,coinsBefore=9820,coinsAfter=9815,frozenPrice=5,
            expectedOwnership=quote.Request.ExpectedOwnership,era="workshop-yard",stages=levels,selected="power-source.workshop-yard.upgrade-2",
            beforeOwnership=SkillOwnershipCodec.Encode(before),afterOwnership=SkillOwnershipCodec.Encode(proposal.OwnershipAfter),
            beforeToken=before.Token,afterToken=proposal.OwnershipAfter.Token,
            externalR8Checkpoint=new{globalSequence=20,seed=1,carry=0,lastSettledAt=2000,entrySequence=13,transitionSeen=13}};
        File.WriteAllText(Path.Combine(args[0],"delivery2-price5-proposal.json"),JsonSerializer.Serialize(fixture)+"\n");
        File.WriteAllText(Path.Combine(args[0],"delivery2-price5-owned.json"),SkillOwnershipCodec.Encode(proposal.OwnershipAfter)+"\n");
        Console.WriteLine("PINNED V2 module proposal: old D1 receipt180 unchanged; D2 debit5;9820->9815; exact mixed V1/V2 token retained. Not a full Core save.");
    }
}
