#if JOURNEY_CHECKS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;
using JunkToNeon.Orders;
using JunkToNeon.Guidance;

internal static class JourneyCompatibilityChecks
{
    sealed class Storage:ISnapshotStorage
    {public string raw,backup;public int writes;public string ReadPrimary()=>raw;public string ReadBackup()=>backup;public void Write(string next){writes++;backup=raw;raw=next;}}
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static int assertions,groups;
    static void Check(bool value,string why){assertions++;if(!value)throw new Exception(why);}
    static void Reject(Action action,string why){assertions++;try{action();}catch{return;}throw new Exception(why);}
    static void Pass(string name){groups++;Console.WriteLine("PASS Journey/pricing compatibility: "+name);}
    static string Raw(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name+".json")).TrimEnd('\r','\n');
    static GameSession Open(Storage storage,bool fresh=false){var g=GameSession.Open(new GameStore(storage,Codec),1,initializeJourney:fresh);Check(!g.RecoveryRequired,g.Error);return g;}
    static string BeforePending(string name,out SkillPurchaseRequest request)
    {var s=Codec.Decode(Raw(name));request=s.pending.chapter.SkillRequest;s.pending=null;s.revision=s.sequence*2;return Codec.Encode(s);}
    static void NoWrite(Storage storage,GameSession game,Action action,string why)
    {string before=storage.raw;int writes=storage.writes;Reject(action,why);Check(storage.raw==before&&storage.writes==writes&&!game.RecoveryRequired,why+": refusal wrote or blocked healthy state");}
    static void CurrentPricesAndOldRequests()
    {
        string[] names={"universal-1","universal-2","recycling-1","recycling-2","delivery-1","delivery-2"};
        long[] oldPrices={120,360,160,480,180,540},newPrices={120,160,160,90,60,20};
        for(int i=0;i<names.Length;i++)
        {
            string before=BeforePending("pending-"+names[i],out var oldRequest);var store=new Storage{raw=before};var game=Open(store);
            Check(oldRequest.PricingRevision=="skill.prices.v1","Missing old request revision did not mean V1");
            NoWrite(store,game,()=>game.BuySkill(oldRequest,2000),"Fresh old V1 quote must refuse while durable V1 pending remains recoverable");
            var quote=ChapterEconomy.QuoteSkill(game.State,oldRequest.NodeId);var request=quote.Request;
            Check(request.PricingRevision=="skill.prices.v3"&&quote.Node.Price==newPrices[i]&&SkillPricing.Price(request.NodeId,"skill.prices.v1")==oldPrices[i],"Independent current/old price table differs");
            long wallet=game.State.coins;uint seed=game.State.seed;string token=game.Chapter.Skills.Token;int count=game.Chapter.Skills.Receipts.Count;
            string selected=game.Chapter.SelectedProjectId;string checkpoint=OrderStateCodec.Encode(game.Journey.Orders);
            ReplayFaultMatrix.Verify("current-price-"+names[i],before,(g,q)=>g.BuySkill(request,2000),
                s=>Check(s.coins==wallet&&s.seed==seed,"Current-price before mismatch"),s=>{
                    var c=ChapterEconomy.Project(s);Check(s.coins==wallet-newPrices[i]&&s.seed==seed&&c.SelectedProjectId==selected&&c.Skills.Receipts.Count==count+1,"Current-price debit changed RNG/target/old ownership");
                    Check(c.Skills.Receipts.Last().FrozenPrice==newPrices[i]&&c.Skills.Receipts.Last().Request.PricingRevision=="skill.prices.v3","Current receipt did not freeze V3");
                    Check(count==0||c.Skills.Token.StartsWith(token+"|",StringComparison.Ordinal),"Mixed history rewrote old token prefix");
                });
            game.BuySkill(request,2000);
            Check(OrderStateCodec.Encode(game.Journey.Orders)==checkpoint,"Skill purchase advanced order state");
            var payload=JsonNode.Parse(SkillOwnershipCodec.Encode(game.Chapter.Skills));Check((int)payload["version"]==3,"Current/mixed ownership did not use version3");
            Check(payload["receipts"].AsArray().All(r=>r["priceRevision"]!=null),"Mixed ownership omitted a receipt revision");
            game.SettleOffline(2060);long live=game.State.coins;int writes=store.writes;game.BuySkill(request,2060);
            Check(game.State.coins==live&&live==wallet-newPrices[i]+1&&store.writes==writes,"Replay restored old wallet or bought deferred construction");
        }
        var staleStore=new Storage{raw=Raw("owned-delivery-1")};var staleGame=Open(staleStore);var staleQuote=ChapterEconomy.QuoteSkill(staleGame.State,SkillIds.Delivery2).Request;
        staleGame.Checkpoint(2001);NoWrite(staleStore,staleGame,()=>staleGame.BuySkill(staleQuote,2001),"Stale current quote after protocol command");
        Pass("V1 frozen goldens versus refused fresh V1 requests; all six actual V3 quotes/purchases, mixed token prefixes, four-write fault positions and live-wallet replay");
    }
    static void V2ProposalAndOwnedReceipt()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"V2Pricing","delivery2-price5-proposal.json")));
        var f=data.RootElement;var before=SkillOwnershipCodec.DecodeOrDefault(f.GetProperty("beforeOwnership").GetString());
        var after=SkillOwnershipCodec.DecodeOrDefault(f.GetProperty("afterOwnership").GetString());int[] levels={4,3,4,4,3,4};
        var context=new SkillContext(new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select((id,i)=>new FamilyStage(id,levels[i]))),"power-source.workshop-yard.upgrade-2");
        var request=new SkillPurchaseRequest("skill.delivery.2","skillbuy.21",21,9820,f.GetProperty("expectedOwnership").GetString(),context,"skill.prices.v2");
        var frozen=SkillTree.RestorePurchase(20,9820,context,before,request,5);
        Check(frozen.WalletAfter==9815&&frozen.FrozenPrice==5&&frozen.OwnershipAfter.Token==f.GetProperty("afterToken").GetString(),"V3 reinterpreted frozen V2 debit5");
        Check(SkillOwnershipCodec.Encode(after)==f.GetProperty("afterOwnership").GetString()&&after.Token==f.GetProperty("afterToken").GetString(),"V2 mixed ownership/token repriced or refunded");
        Check(after.Receipts[0].FrozenPrice==180&&after.Receipts[1].FrozenPrice==5&&after.Receipts[1].Request.PricingRevision=="skill.prices.v2","V1/V2 receipt prices not preserved");
        // This is explicitly a reconstructed CURRENT Core journal backed by the pinned
        // historical module proposal. It is not labelled as literal historical Core JSON.
        var original=Codec.Decode(Raw("owned-delivery-1"));var chapter=original.chapter;
        var afterChapter=new ChapterState(2,chapter.RuleRevision,chapter.Progress,after,chapter.SelectedProjectId,chapter.EntrySequence,chapter.TransitionSeen);
        original.pending=new PendingOperation{kind="skill",sequence=21,at=2000,chapter=new ChapterOperation(2,"skill",21,2000,SkillIds.Delivery2,5,9820,9815,1,1,null,0,0,chapter,afterChapter,null,request,null)};
        original.revision=41;var frozenStore=new Storage{raw=Codec.Encode(original)};var recovered=Open(frozenStore);
        Check(recovered.State.coins==9815&&recovered.State.sequence==21&&recovered.State.seed==1&&recovered.Chapter.Skills.Token==f.GetProperty("afterToken").GetString()&&recovered.State.journey==null,"Actual Core journal did not recover frozen V2 price5 before Journey adoption");
        int writes=frozenStore.writes;recovered=Open(frozenStore);Check(frozenStore.writes==writes&&recovered.State.coins==9815,"Recovered V2 Core journal replayed");
        Check(SkillTree.Propose(20,9820,context,before,request).Status==PurchaseStatus.Stale,"Unreserved old V2 quote accepted under V3");
        Check(SkillTree.Propose(21,9815,context,after,request).Status==PurchaseStatus.Replay,"Exact already-owned V2 purchase did not replay without another debit");
        var unknown=new SkillPurchaseRequest(request.NodeId,request.OperationId,request.Sequence,request.ExpectedWallet,request.ExpectedOwnership,request.Context,"skill.prices.v99");
        Reject(()=>SkillTree.RestorePurchase(20,9820,context,before,unknown,5),"Unknown revision restored");
        Pass("pinned bff8b5 V2 module payload/proposal preserves D1=180 and D2=5 under V3=20; this fixture is not a historical full Core pending save");
    }
    static void GuidanceIdentity()
    {
        var oldStore=new Storage{raw=Raw("ready-no-skills")};var existing=Open(oldStore,true);string initial=oldStore.raw;
        Check(existing.Journey.Guidance.Origin==GuideOrigin.Existing&&existing.State.journey==null&&existing.Guidance.Suggestion?.HintId==GuideIds.Skills&&oldStore.writes==0&&oldStore.raw==initial,"Existing r8 was relabelled Fresh/LegacyExpanded or eagerly written");
        ReplayFaultMatrix.Verify("guide-ack-with-funded-deferred-power",initial,(g,q)=>g.AcknowledgeGuidance(GuideIds.Skills,q,at:2000),
            s=>Check(s.coins==10000&&s.seed==1&&s.selectedStage==0,"Guide before"),
            s=>Check(s.coins==10000&&s.seed==1&&s.chapter.SelectedProjectId=="power-source.workshop-yard.upgrade-2"&&s.chapter.Skills.Receipts.Count==0&&WaveProgress.Of(s).Delivery.PigeonProgress==0&&(s.journey.Guidance.AcknowledgementBits&8)!=0,"Guide ACK granted/debited/bought or lost acknowledgement"));
        existing.SelectChapterProject("power-source.workshop-yard.upgrade-2",20,2000);
        Check(existing.State.coins==9300&&existing.Journey.Guidance.LastObservedSequence==20&&existing.Journey.Guidance.LastCompletion?.Sequence==20&&existing.Journey.Guidance.LastCompletion.ProjectId=="power-source.workshop-yard.upgrade-2","One selection event dropped its immediate completion");
        existing.SelectChapterProject("workshop.workshop-yard.upgrade-1",21,2000);
        NoWrite(oldStore,existing,()=>existing.AcknowledgeGuidance(GuideIds.Completion,22,20,2000),"Late old completion ACK dismissed the newer completion");
        existing.ExecuteChapterSpin(22,2000,manualSpin:false);
        Check(existing.State.coins==8942&&existing.Journey.Guidance.LastCompletion.Sequence==21&&existing.Journey.Guidance.LastCompletion.ProjectId=="workshop.workshop-yard.upgrade-1","Later AUTO result replaced latest completion or changed independent22-coin reward");
        existing=Open(oldStore);string afterAuto=oldStore.raw;
        ReplayFaultMatrix.Verify("late-ACK-then-AUTO-current-completion",afterAuto,(g,q)=>g.AcknowledgeGuidance(GuideIds.Completion,q,21,2000),
            s=>Check(s.coins==8942&&s.journey.Guidance.LastCompletion.Sequence==21,"Completion ACK before"),
            s=>Check(s.coins==8942&&s.seed==2647435461&&s.journey.Guidance.LastCompletion.Sequence==21&&s.journey.Guidance.CompletionAcknowledgedSequence==21&&WaveProgress.Of(s).Delivery.PigeonProgress==0,"Completion ACK changed economy or identity"));
        existing.AcknowledgeGuidance(GuideIds.Completion,23,21,2000);existing.SelectChapterProject("workshop.workshop-yard.upgrade-2",24,2000);
        Check(existing.Journey.Guidance.LastCompletion.Sequence==24&&existing.Journey.Guidance.CompletionAcknowledgedSequence==0,"New completion inherited old dismissal");
        NoWrite(oldStore,existing,()=>existing.AcknowledgeGuidance(GuideIds.Completion,25,21,2000),"Old ACK paid/dismissed after a new completion");
        var freshStore=new Storage();var fresh=Open(freshStore,true);Check(fresh.State.coins==37&&fresh.Journey.Guidance.Origin==GuideOrigin.Fresh&&freshStore.writes==1,"Actual fresh creation did not persist factual Fresh origin once");
        var r7=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"OlderFixtures","r7","owned-offline-seen.json")));
        r7["houseStage"]=2;r7["selectedStage"]=0;r7["coins"]=0;var expandedStore=new Storage{raw=r7.ToJsonString()};var expanded=Open(expandedStore);
        Check(expanded.Journey.Guidance.Origin==GuideOrigin.LegacyExpanded&&expanded.Guidance.Suggestion?.HintId==GuideIds.ExpandedR7&&expandedStore.writes==0,"Real prechapter r7 completion did not retain expanded provenance purely");
        expanded.AcknowledgeGuidance(GuideIds.ExpandedR7,9,at:1600);expanded=Open(expandedStore);
        Check(expanded.State.coins==0&&expanded.State.seed==2451568673&&WaveProgress.Of(expanded.State).Delivery.PigeonProgress==2&&(expanded.Journey.Guidance.AcknowledgementBits&32)!=0,"Expanded hint ACK reset/granted or resurrected");
        Pass("factual origins, no eager guidance writes/grants, deferred target ACK, combined selection/completion, AUTO retention and stale completion-identity rejection");
    }
    static string CompleteOldR8()
    {
        var store=new Storage{raw=Raw("canonical-all-skills-owned")};var game=Open(store);
        foreach(string family in FamilyIds.All)for(int level=1;level<=2;level++){string id=ChapterIds.Project(EraIds.WorkshopYard,family,level);if(ChapterCatalog.Rules.AvailableUpgrades(game.Chapter.Progress).Contains(id))game.SelectChapterProject(id,game.State.sequence+1,2000);}
        Check(game.State.coins==3660&&game.State.sequence==36&&game.State.seed==2451568673&&WaveProgress.Of(game.State).Delivery.PigeonProgress==2&&ChapterCatalog.Rules.IsChapterComplete(game.Chapter.Progress),"Independent remaining4620 chapter costs changed");
        Check(game.Chapter.Skills.Receipts.Count==6&&game.Chapter.Skills.Receipts.Last().FrozenPrice==540&&game.Journey.Guidance.LastCompletion.Sequence==36,"Completing old r8 repriced old skills or lost completion");return store.raw;
    }
    static void SpinOrder(GameSession game)
    {
        long before=game.State.coins;int carry=WaveProgress.Of(game.State).Delivery.PigeonProgress;var order=game.Journey.Orders;int progress=order.Progress;
        game.ExecuteChapterSpin(game.State.sequence+1,2000,manualSpin:false);var s=game.State;var symbols=s.lastSpin.symbols;
        int potato=symbols.Count(x=>x==0),box=symbols.Count(x=>x==1),trash=symbols.Count(x=>x==2),pigeon=symbols.Count(x=>x==3);
        int baseCoins=potato==3||box==3||trash==3||pigeon==3?23:potato==2||box==2||trash==2||pigeon==2?8:3;
        int deliveries=(carry+pigeon)/3;long expected=baseCoins+3+6*potato+7*box+6*trash+3*pigeon+5*deliveries;
        Check(s.coins==before+expected&&WaveProgress.Of(s).Delivery.PigeonProgress==(carry+pigeon)%3,"Order observation granted automatic reward or counted delivery coins instead of completions");
        int target=order.ActiveOrderId=="order.sort-salvage"?12:3;int delta=order.ActiveOrderId=="order.sort-salvage"?trash:deliveries;
        Check(game.Journey.Orders.Progress==Math.Min(target,progress+delta)&&game.Journey.Orders.Cursor==s.sequence&&s.lastSpin.chapter.Sequence==s.sequence&&s.lastSpin.journey.Sequence==s.sequence,"Order progress not composed atomically in the SAME spin");
        Check(game.Journey.Guidance.LastCompletion.Sequence==36,"AUTO order spin erased latest building completion");
    }
    static void FillOrder(GameSession game)
    {int spins=0;while(!game.Journey.Orders.Ready&&spins++<80)SpinOrder(game);Check(game.Journey.Orders.Ready,"Seeded order did not become ready within80 foreground spins");}
    static void OrdersIdentity()
    {
        string completed=CompleteOldR8();var store=new Storage{raw=completed};var game=Open(store);game.AcceptOrder("order.sort-salvage",37,2000);
        Check(game.State.coins==3660&&game.Journey.Orders.InstanceSequence==37&&game.Journey.Orders.Progress==0,"Accept counted old spins/paid fee");
        string accepted=store.raw;
        ReplayFaultMatrix.Verify("old-r8-composed-order-spin",accepted,(g,q)=>g.ExecuteChapterSpin(q,2000,manualSpin:false),
            s=>Check(s.coins==3660&&s.journey.Orders.InstanceSequence==37,"Composed spin before"),
            s=>Check(s.coins==3686&&s.seed==2793601271&&WaveProgress.Of(s).Delivery.PigeonProgress==0&&s.journey.Orders.Progress==1&&s.journey.Orders.Cursor==38,"Old-skill/order same-spin reward26 or progress1 changed"));
        var protocolStore=new Storage{raw=accepted};var protocols=Open(protocolStore);string checkpoint=OrderStateCodec.Encode(protocols.Journey.Orders);
        protocols.Checkpoint(2060);protocols.SettleOffline(2120);Check(protocols.State.coins==3661&&protocols.State.seed==2451568673&&WaveProgress.Of(protocols.State).Delivery.PigeonProgress==2&&OrderStateCodec.Encode(protocols.Journey.Orders)==checkpoint,"Clock/offline advanced order progress or purchased anything");
        FillOrder(game);long firstInstance=37,firstClaim=game.State.sequence+1,coins=game.State.coins;uint seed=game.State.seed;int carry=WaveProgress.Of(game.State).Delivery.PigeonProgress;
        game.ClaimOrder("order.sort-salvage",firstInstance,firstClaim,2000);Check(game.State.coins==coins+120&&game.State.seed==seed&&WaveProgress.Of(game.State).Delivery.PigeonProgress==carry,"First claim changed RNG/carry or reward120");
        long secondInstance=game.State.sequence+1;game.AcceptOrder("order.sort-salvage",secondInstance,2000);Check(game.Journey.Orders.Progress==0&&game.Journey.Orders.InstanceSequence==secondInstance,"New repeated instance inherited old progress");
        int writes=store.writes;long live=game.State.coins;game.ClaimOrder("order.sort-salvage",firstInstance,firstClaim,2000);
        Check(store.writes==writes&&game.State.coins==live&&game.Journey.Orders.InstanceSequence==secondInstance,"Latest exact old claim replay paid/rewound new instance");
        NoWrite(store,game,()=>game.ClaimOrder("order.sort-salvage",firstInstance,game.State.sequence+1,2000),"Old instance claimed against newer active instance");
        FillOrder(game);string readyAgain=store.raw;long secondClaim=game.State.sequence+1;coins=game.State.coins;seed=game.State.seed;carry=WaveProgress.Of(game.State).Delivery.PigeonProgress;
        ReplayFaultMatrix.Verify("repeat-instance-claim-after-old-replay",readyAgain,(g,q)=>g.ClaimOrder("order.sort-salvage",secondInstance,q,2000),
            s=>Check(s.coins==coins&&s.journey.Orders.InstanceSequence==secondInstance&&s.journey.Orders.Ready,"Second claim before"),
            s=>Check(s.coins==coins+120&&s.seed==seed&&WaveProgress.Of(s).Delivery.PigeonProgress==carry&&s.journey.Orders.ActiveOrderId==null&&s.journey.Orders.LatestClaim.InstanceSequence==secondInstance,"Repeated claim paid wrong instance/reward or changed RNG/carry"));
        game.ClaimOrder("order.sort-salvage",secondInstance,secondClaim,2000);game=Open(store);
        NoWrite(store,game,()=>game.ClaimOrder("order.sort-salvage",firstInstance,firstClaim,2000),"Older-than-latest exact claim accepted");
        long thirdInstance=game.State.sequence+1;game.AcceptOrder("order.sort-salvage",thirdInstance,2000);int bounded=0;while(game.Journey.Orders.Progress==0&&bounded++<20)SpinOrder(game);
        Check(game.Journey.Orders.Progress>0&&!game.Journey.Orders.Ready,"Cancellation fixture must forfeit positive incomplete progress");coins=game.State.coins;seed=game.State.seed;carry=WaveProgress.Of(game.State).Delivery.PigeonProgress;
        string toCancel=store.raw;
        ReplayFaultMatrix.Verify("cancel-new-repeat-forfeits-progress",toCancel,(g,q)=>g.CancelOrder("order.sort-salvage",thirdInstance,q,2000),
            s=>Check(s.journey.Orders.Progress>0,"Cancel before"),s=>Check(s.coins==coins&&s.seed==seed&&WaveProgress.Of(s).Delivery.PigeonProgress==carry&&s.journey.Orders.ActiveOrderId==null&&s.journey.Orders.Progress==0,"Cancel rewarded or changed carry/history"));
        game.CancelOrder("order.sort-salvage",thirdInstance,game.State.sequence+1,2000);
        long deliveryInstance=game.State.sequence+1;game.AcceptOrder("order.neighborhood-supply",deliveryInstance,2000);Check(game.Journey.Orders.Progress==0,"Delivery accepted retroactive carry/completions");
        FillOrder(game);coins=game.State.coins;seed=game.State.seed;carry=WaveProgress.Of(game.State).Delivery.PigeonProgress;long deliveryClaim=game.State.sequence+1;
        game.ClaimOrder("order.neighborhood-supply",deliveryInstance,deliveryClaim,2000);game=Open(store);
        Check(game.State.coins==coins+180&&game.State.seed==seed&&WaveProgress.Of(game.State).Delivery.PigeonProgress==carry&&game.Journey.Orders.LatestClaim.RewardCoins==180,"Three completed deliveries did not claim180 once");
        writes=store.writes;game.ClaimOrder("order.neighborhood-supply",deliveryInstance,deliveryClaim,2000);Check(store.writes==writes&&game.State.coins==coins+180,"Delivery latest replay paid again");
        Check(store.raw.Length<65536&&OrderStateCodec.Encode(game.Journey.Orders).Length<=4096&&GuideStateCodec.Encode(game.Journey.Guidance).Length<=4096,"Repeated instances accumulated unbounded state");
        Pass("old-r8 composed reward/progress, clock non-events, repeated-instance claims, stale/old replay isolation, cancel forfeit and delivery-completion reward180");
    }
    static void StrictNewDescriptors()
    {
        var s=Codec.Decode(Raw("owned-delivery-1"));var request=ChapterEconomy.QuoteSkill(s,SkillIds.Delivery2).Request;
        var pending=JourneyEconomy.ReserveChapter(s,"skill",request.NodeId,request.Sequence,2000,request);
        string valid=Codec.Encode(pending);Codec.Decode(valid);
        var bad=JsonNode.Parse(valid);bad["pending"].AsObject().Remove("journey");Reject(()=>Codec.Decode(bad.ToJsonString()),"Composed Journey descriptor missing but modern aggregate accepted");
        bad=JsonNode.Parse(valid);bad["journey"]=null;Reject(()=>Codec.Decode(bad.ToJsonString()),"Present null Journey silently defaulted");
        bad=JsonNode.Parse(valid);bad["pending"]["journey"]["format"]=99;Reject(()=>Codec.Decode(bad.ToJsonString()),"Unknown Journey format accepted");
        bad=JsonNode.Parse(valid);bad["pending"]["chapter"]["skillRequest"]["priceRevision"]="skill.prices.v99";Reject(()=>Codec.Decode(bad.ToJsonString()),"Actual Core accepted unknown frozen pricing revision");
        var ownership=JsonNode.Parse(SkillOwnershipCodec.Encode(pending.pending.chapter.After.Skills));
        ownership["receipts"][0].AsObject().Remove("priceRevision");Reject(()=>SkillOwnershipCodec.DecodeOrDefault(ownership.ToJsonString()),"Mixed version3 accepted a missing receipt revision");
        Pass("strict present Journey/composed descriptors and explicit unknown/missing pricing revisions reject without legacy fallback");
    }
    public static void Run()
    {
        OlderCorpusChecks.Run();CurrentPricesAndOldRequests();V2ProposalAndOwnedReceipt();GuidanceIdentity();OrdersIdentity();StrictNewDescriptors();
        Console.WriteLine("PASS "+groups+" new-wave compatibility groups / "+assertions+" counted assertions plus focused journal fault matrices");
    }
}
#endif
