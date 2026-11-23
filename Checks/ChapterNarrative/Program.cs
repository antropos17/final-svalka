using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

internal static class Program
{
    const long At=1700000000;
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    // Independent contract IDs, not generated through the production ID helper.
    static readonly string[] Ids={
        "house.repair","house.improve","workshop.build","workshop.improve","recycler.build","recycler.improve",
        "pigeon-loft.build","pigeon-loft.improve","cart.build","cart.improve","power-source.build","power-source.improve",
        "house.workshop-yard.upgrade-1","house.workshop-yard.upgrade-2","workshop.workshop-yard.upgrade-1","workshop.workshop-yard.upgrade-2",
        "recycler.workshop-yard.upgrade-1","recycler.workshop-yard.upgrade-2","pigeon-loft.workshop-yard.upgrade-1","pigeon-loft.workshop-yard.upgrade-2",
        "cart.workshop-yard.upgrade-1","cart.workshop-yard.upgrade-2","power-source.workshop-yard.upgrade-1","power-source.workshop-yard.upgrade-2"};
    static int assertions,readChecks,actualSpins;static readonly List<string> groups=new List<string>();
    static readonly Dictionary<string,string> inputHashes=new Dictionary<string,string>();
    sealed class Storage:ISnapshotStorage
    {public string raw;public int writes;public string ReadPrimary()=>raw;public string ReadBackup()=>null;public void Write(string text){raw=text;writes++;}}
    static void Need(bool value,string why){assertions++;if(!value)throw new InvalidOperationException(why);}
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static string Fixture(string root,string path)
    {string full=Path.Combine(root,path);inputHashes[path]=Hash(full);return File.ReadAllText(full).TrimEnd('\r','\n');}
    static GameSession Open(Storage storage,bool fresh=false)
    {var g=GameSession.Open(new GameStore(storage,Codec),1,initializeJourney:fresh);Need(!g.RecoveryRequired,g.Error);return g;}
    static void Pass(string name){groups.Add(name);Console.WriteLine("PASS "+name);}
    static void RejectWithoutWrite(Storage s,GameSession g,Action action,string why)
    {
        string before=s.raw,encoded=Codec.Encode(g.State);int writes=s.writes;bool rejected=false;
        try{action();}catch(InvalidOperationException error){rejected=error.Message.Contains("Stale completion acknowledgement");}
        Need(rejected,why);Need(s.raw==before&&s.writes==writes&&Codec.Encode(g.State)==encoded&&!g.RecoveryRequired,why+": refusal changed snapshot");
    }
    static void ReadOnly(Storage s,GameSession g,string expectedId=null)
    {
        string raw=s.raw,encoded=Codec.Encode(g.State);int writes=s.writes;
        Need(!string.IsNullOrWhiteSpace(ChapterNarrative.Opening)&&!string.IsNullOrWhiteSpace(ChapterNarrative.EraReady)&&
            !string.IsNullOrWhiteSpace(ChapterNarrative.EraEntry)&&!string.IsNullOrWhiteSpace(ChapterNarrative.ChapterEnd),"Missing global text");
        foreach(string id in Ids)Need(ChapterNarrative.TryCompletion(id,out _),"Missing lookup during persisted read");
        foreach(string id in new[]{"order.sort-salvage","order.neighborhood-supply"})Need(ChapterNarrative.OrderIntroduction(id)!=null,"Missing order introduction");
        var completion=g.Guidance.LastCompletion;
        if(expectedId!=null)
        {
            Need(completion!=null&&completion.ProjectId==expectedId,"Latest persisted completion identity differs");
            Need(ChapterNarrative.TryCompletion(completion.ProjectId,out var beat)&&beat.ProjectId==expectedId,"Real completion cannot resolve story");
        }
        else if(completion!=null)Need(ChapterNarrative.TryCompletion(completion.ProjectId,out _),"Unmapped real completion");
        Need(s.raw==raw&&s.writes==writes&&Codec.Encode(g.State)==encoded,"Narrative read wrote/granted/repriced/advanced state");readChecks++;
    }
    static void Coverage()
    {
        var catalog=ChapterCatalog.Rules.Definition.Eras.SelectMany(e=>e.Catalog.Upgrades.Select(u=>u.Id)).ToArray();
        Need(catalog.Length==24&&Ids.Distinct().Count()==24&&new HashSet<string>(catalog).SetEquals(Ids),"Literal24 IDs differ from actual catalog");
        foreach(string id in Ids)
        {
            Need(ChapterNarrative.TryCompletion(id,out var beat)&&beat.ProjectId==id,"Wrong completion lookup: "+id);
            Need(beat.Title.Length>0&&beat.Title.Length<=48&&beat.Body.Length>0&&beat.Body.Length<=150,"Unbounded existing-label copy: "+id);
            Need(!beat.Body.Contains("Глава завершена")&&!beat.Body.Contains("Неон"),"Stage reaction claims a global ending: "+id);
        }
        foreach(string id in new[]{null,"","HOUSE.REPAIR","skill.delivery.1","era.workshop-yard.unlock","house.workshop-yard.upgrade-3"})
        {Need(!ChapterNarrative.TryCompletion(id,out var beat)&&beat==null,"Unknown completion invented a story");Need(ChapterNarrative.OrderIntroduction(id)==null,"Unknown order invented a story");}
        Need(typeof(NarrativeBeat).IsSealed&&typeof(NarrativeBeat).GetProperties().All(p=>p.SetMethod==null),"Beat exposes mutable properties");
        Need(typeof(ChapterNarrative).GetFields(BindingFlags.Public|BindingFlags.Static).Length==0&&typeof(ChapterNarrative).GetProperties().All(p=>p.PropertyType==typeof(string)),"Narrative exposes mutable content collection");
        Need(!ChapterNarrative.TryCompletion("house.improve",out var unused)||!unused.Body.Contains("Остальные постройки"),"House-last ordering has a false unfinished-family claim");
        Pass("literal24 catalog coverage, immutable API, unknown/null fallbacks and bounded own-stage copy");
    }
    static GameSession Funded(out Storage storage)
    {
        var state=ModelState.Fresh(1);state.coins=10000;state.journey=JourneyState.Fresh();
        storage=new Storage{raw=Codec.Encode(state)};return Open(storage);
    }
    static void TwoOrders(bool houseLast)
    {
        var game=Funded(out var storage);long spent=0,previousCompletion=0;int purchases=0;
        var order=houseLast?new[]{10,8,6,4,2,0}:new[]{0,2,4,6,8,10};
        foreach(int era in new[]{0,1})
        {
            var indices=new List<int>();
            if(era==0&&houseLast)indices.Add(0); // The only cross-family prerequisite is first repair.
            foreach(int pair in order)for(int n=0;n<2;n++)if(!(era==0&&houseLast&&pair+n==0))indices.Add(era*12+pair+n);
            foreach(int index in indices)
            {
                string id=Ids[index];Need(ChapterCatalog.Rules.AvailableUpgrades(game.Chapter.Progress).Contains(id),"Declared legal order unavailable: "+id);
                long before=game.State.coins,sequence=game.State.sequence+1,price=ChapterEconomy.ProjectPrice(id);
                game.SelectChapterProject(id,sequence,At);spent+=price;purchases++;
                Need(game.State.coins==before-price&&game.Chapter.SelectedProjectId==null,"Funded immediate purchase changed");
                Need(game.Journey.Guidance.LastCompletion.Sequence==sequence,"Completion not recorded on immediate selection");
                ReadOnly(storage,game,id);
                int writes=storage.writes;game=Open(storage);Need(storage.writes==writes,"Completed snapshot eagerly rewrote on reload");ReadOnly(storage,game,id);
                if(previousCompletion!=0)RejectWithoutWrite(storage,game,()=>game.AcknowledgeGuidance(GuideIds.Completion,game.State.sequence+1,previousCompletion,At),"Old completion ACK dismissed latest");
                if(purchases%3==0)
                {
                    string economic=EconomicIdentity(game);long ack=game.State.sequence+1;
                    game.AcknowledgeGuidance(GuideIds.Completion,ack,sequence,At);
                    Need(game.Journey.Guidance.CompletionAcknowledgedSequence==sequence&&EconomicIdentity(game)==economic,"Completion ACK changed economy");
                    writes=storage.writes;game.AcknowledgeGuidance(GuideIds.Completion,ack,sequence,At);Need(storage.writes==writes,"Exact ACK replay wrote again");
                    ReadOnly(storage,game,id);
                }
                previousCompletion=sequence;
                if(id=="house.repair")game.AcknowledgeAutoHint(game.State.sequence+1,At);
                Need(ChapterCatalog.Rules.IsChapterComplete(game.Chapter.Progress)==(purchases==24),"One family ended chapter prematurely");
                if(era==0)Need(ChapterCatalog.Rules.CanUnlockWorkshopYard(game.Chapter.Progress)==(purchases==12),"Era-ready fact before all twelve projects");
            }
            if(era==0)
            {
                long entry=game.State.sequence+1;
                game.EnterWorkshopYard(entry,At);Need(game.Chapter.TransitionPending&&game.State.coins==10000-spent,"Era entry charged or skipped presentation");
                int writes=storage.writes;game=Open(storage);Need(game.Chapter.TransitionPending&&storage.writes==writes,"Unseen transition changed on reload");ReadOnly(storage,game);
                uint seed=game.State.seed;long coins=game.State.coins,ack=game.State.sequence+1;
                game.AcknowledgeEraTransition(ack,At);Need(!game.Chapter.TransitionPending&&game.State.coins==coins&&game.State.seed==seed,"Skip/ACK changed economic terms");
                writes=storage.writes;game.AcknowledgeEraTransition(ack,At);Need(storage.writes==writes,"Exact transition ACK replay wrote");
            }
        }
        Need(purchases==24&&spent==9310&&game.State.coins==690&&game.State.seed==1&&game.Chapter.Skills.Receipts.Count==0,"Private24-purchase fixture altered price/RNG/skills");
        Need(JourneyEconomy.GuideContext(game.Chapter,game.Journey).ChapterComplete&&game.Journey.Orders.ActiveOrderId==null,"Final story fact created a mandatory order");
        foreach(string id in new[]{"order.sort-salvage","order.neighborhood-supply"})
        {
            long coins=game.State.coins,instance=game.State.sequence+1;game.AcceptOrder(id,instance,At);ReadOnly(storage,game);
            Need(game.Journey.Orders.Progress==0&&!game.Journey.Orders.Ready&&game.State.coins==coins,"Order introduction granted or advanced progress");
            game.CancelOrder(id,instance,game.State.sequence+1,At);Need(game.State.coins==coins&&game.Journey.Orders.ActiveOrderId==null,"Optional cancel paid a story reward");
        }
        Need(OrderCatalog.Get("order.sort-salvage").Target==12&&OrderCatalog.Get("order.sort-salvage").RewardCoins==120&&
            OrderCatalog.Get("order.neighborhood-supply").Target==3&&OrderCatalog.Get("order.neighborhood-supply").RewardCoins==180,"Narrative altered optional order terms");
        Pass((houseLast?"house-last/reversed":"house-first/forward")+" legal24 purchases; persisted completion/reload/ACK/stale guard; both boundaries and optional order read/cancel");
    }
    static string EconomicIdentity(GameSession g)=>g.State.coins+"/"+g.State.seed+"/"+g.Chapter.SelectedProjectId+"/"+g.Chapter.Skills.Token+"/"+WaveProgress.Of(g.State).Delivery.PigeonProgress;
    static void FreshSpinAndCurrentSkill()
    {
        var s=new Storage();var g=Open(s,true);Need(g.State.coins==37&&s.writes==1&&g.Journey.Guidance.Origin==GuideOrigin.Fresh,"Fresh grant/origin changed");ReadOnly(s,g);
        g.SelectChapterProject("house.repair",1,At);Need(g.State.coins==37&&g.Journey.Guidance.LastCompletion==null,"Unaffordable selection invented completion");ReadOnly(s,g);
        g.ExecuteChapterSpin(2,At);actualSpins++;Need(g.State.coins==5&&g.State.seed==2647435461&&g.Journey.Guidance.LastCompletion.Sequence==2,"Literal37+8-40 first repair failed");ReadOnly(s,g,"house.repair");
        g.AcknowledgeAutoHint(g.State.sequence+1,At);g.ExecuteChapterSpin(g.State.sequence+1,At,manualSpin:false);actualSpins++;
        Need(g.Journey.Guidance.LastCompletion.Sequence==2,"Later AUTO erased retained completion");ReadOnly(s,g,"house.repair");
        int writes=s.writes;g=Open(s);Need(s.writes==writes,"Reload repeated fresh grant or AUTO completion");ReadOnly(s,g,"house.repair");
        g=Funded(out s);g.SelectChapterProject("house.repair",1,At);
        var quote=ChapterEconomy.QuoteSkill(g.State,"slot.coin-boost");g.BuySkill(quote.Request,At);
        Need(g.State.coins==9840&&g.Chapter.Skills.Receipts.Single().FrozenPrice==120&&g.Chapter.Skills.Receipts.Single().Request.PricingRevision=="skill.prices.v3","Current skill term changed");ReadOnly(s,g,"house.repair");
        writes=s.writes;g.BuySkill(quote.Request,At);Need(s.writes==writes&&g.State.coins==9840,"Current skill exact replay paid again");
        Pass("real fresh grant once, spin completion, retained AUTO/read/reload and current V3 owned skill");
    }
    static void Historical(string root)
    {
        var samples=new[]{
            new{path="Checks/EraMigration/Fixtures/r6/pending-finish-house-spin.json",coins=4L,writes=1,latest=(string)null},
            new{path="Checks/EraMigration/Fixtures/r6/committed-finish-house-spin.json",coins=4L,writes=0,latest=(string)null},
            new{path="Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-pending-spin540.json",coins=3L,writes=1,latest="house.improve"},
            new{path="Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-paid540.json",coins=3L,writes=0,latest="house.improve"},
            new{path="Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-retained-v1-spin600.json",coins=3L,writes=0,latest="house.improve"},
            new{path="Checks/EraMigration/R8/Fixtures/pending-delivery-2.json",coins=9280L,writes=1,latest=(string)null},
            new{path="Checks/EraMigration/R8/Fixtures/owned-delivery-2.json",coins=9280L,writes=0,latest=(string)null}};
        foreach(var sample in samples)
        {
            string raw=Fixture(root,sample.path);var old=Codec.Decode(raw);var request=old.pending?.chapter?.SkillRequest;
            var s=new Storage{raw=raw};var g=Open(s);Need(g.State.coins==sample.coins&&s.writes==sample.writes,"Frozen debit/eager write changed: "+sample.path);
            if(sample.writes==0)Need(s.raw==raw,"Nonpending historical snapshot rewritten");
            if(sample.path.Contains("delivery-2"))Need(g.Chapter.Skills.Receipts.Last().FrozenPrice==540&&g.Chapter.Skills.Receipts.Last().Request.PricingRevision=="skill.prices.v1","Legacy skill540 repriced");
            if(sample.path.Contains("v2-pending")||sample.path.Contains("v2-paid"))Need(g.State.lastSpin.chapter.Price==540,"Frozen construction540 repriced");
            if(sample.path.Contains("retained-v1"))Need(g.State.lastSpin.chapter.Price==600,"Retained construction600 repriced");
            if(sample.latest==null)Need(g.Journey.Guidance.LastCompletion==null,"Narrative invented pre-Journey history");
            ReadOnly(s,g,sample.latest);int writes=s.writes;g=Open(s);Need(s.writes==writes&&g.State.coins==sample.coins,"Historical reopen replayed debit/grant");ReadOnly(s,g,sample.latest);
            if(request!=null){g.BuySkill(request,At);Need(s.writes==writes&&g.State.coins==9280,"Historical540 skill replay charged again");}
        }
        string v2=Fixture(root,"Checks/EraMigration/R8/V2Pricing/delivery2-price5-owned.json");
        var owned=SkillOwnershipCodec.DecodeOrDefault(v2);Need(owned.Receipts[0].FrozenPrice==180&&owned.Receipts[1].FrozenPrice==5&&owned.Receipts[1].Request.PricingRevision=="skill.prices.v2","Pinned historical V1/V2 module terms changed");
        Need(SkillOwnershipCodec.Encode(owned)==v2,"Reading pinned V2 module changed bytes");
        ChapterNarrative.TryCompletion("pigeon-loft.workshop-yard.upgrade-1",out _);Need(SkillOwnershipCodec.Encode(owned)==v2,"Story lookup repriced historical module");
        Pass("seven genuine legacy/current historical snapshots: pending/paid600/540 and V1 skill540; separate pinned V2 module180/5 round-trip");
    }
    static int Main(string[] args)
    {
        try
        {
            Need(args.Length==2,"Provide exact repository root and new evidence output path");string root=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Need(!File.Exists(output),"Preserve first receipt: new output required");
            var timer=Stopwatch.StartNew();Coverage();TwoOrders(false);TwoOrders(true);FreshSpinAndCurrentSkill();Historical(root);
            Need(actualSpins==2&&timer.Elapsed.TotalSeconds<60,"Bounded two-spin/60-second scope exceeded");
            foreach(var input in inputHashes)Need(Hash(Path.Combine(root,input.Key))==input.Value,"Frozen input changed during check");
            string json=JsonSerializer.Serialize(new{scope="Pure narrative + actual Core/private in-memory save behavior; no Unity/UI/native claim",groups,assertions,readChecks,actualSpins,privateFundedChapterPaths=2,naturalBalanceCampaigns=0,inputHashes,wallSeconds=timer.Elapsed.TotalSeconds},new JsonSerializerOptions{WriteIndented=true});
            Need(System.Text.Encoding.UTF8.GetByteCount(json)<131072,"128KiB receipt cap");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json+"\n");Console.WriteLine("PASS narrative groups="+groups.Count+" assertions="+assertions+" readChecks="+readChecks+" actualSpins="+actualSpins);return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL narrative "+error);return 1;}
    }
}
