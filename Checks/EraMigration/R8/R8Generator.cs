using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Contracts;

// Compiled ONLY against the actual pinned r8 source. No future codec is used here.
internal static class R8Generator
{
    sealed class Storage:ISnapshotStorage
    {public string raw;public string ReadPrimary()=>raw;public string ReadBackup()=>null;public void Write(string next){raw=next;}}
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string output;
    static readonly Dictionary<string,string> tokens=new Dictionary<string,string>();
    static GameSession Open(ModelState state)=>GameSession.Open(new GameStore(new Storage{raw=Codec.Encode(state)},Codec),1);
    static void Save(string name,ModelState state)
    {
        string raw=Codec.Encode(state);Codec.Decode(raw);File.WriteAllText(Path.Combine(output,name+".json"),raw+"\n");
        tokens[name]=ChapterEconomy.Project(state).Skills.Token;
        if(state.pending?.chapter!=null)tokens[name+"/after"]=state.pending.chapter.After.Skills.Token;
        Console.WriteLine(name+": coins="+state.coins+", seq="+state.sequence+", seed="+state.seed+", carry="+WaveProgress.Of(state).Delivery.PigeonProgress);
    }
    static void BuyEra(GameSession game,string era)
    {foreach(string family in FamilyIds.All)for(int local=1;local<=2;local++){string id=ChapterIds.Project(era,family,local);if(ChapterCatalog.Rules.AvailableUpgrades(game.Chapter.Progress).Contains(id))game.SelectChapterProject(id,game.State.sequence+1,2000);}}
    static GameSession Ready(ModelState source)
    {
        source.coins=20000;var game=Open(source);BuyEra(game,EraIds.Junkyard);
        game.EnterWorkshopYard(game.State.sequence+1,2000);game.AcknowledgeEraTransition(game.State.sequence+1,2000);
        foreach(string family in new[]{FamilyIds.House,FamilyIds.Recycler,FamilyIds.PigeonLoft,FamilyIds.PowerSource})game.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard,family,1),game.State.sequence+1,2000);
        var low=game.State;low.coins=699;game=Open(low);
        game.SelectChapterProject("power-source.workshop-yard.upgrade-2",game.State.sequence+1,2000);
        var funded=game.State;funded.coins=10000;return Open(funded); // Private test funding, not an economic command/grant.
    }
    static void Branch(string name,string tier1,string tier2,ModelState start)
    {
        var game=Open(start);var request=ChapterEconomy.QuoteSkill(game.State,tier1).Request;
        Save("pending-"+name+"-1",ChapterEconomy.Reserve(game.State,"skill",tier1,request.Sequence,2000,request));
        game.BuySkill(request,2000);Save("owned-"+name+"-1",game.State);
        request=ChapterEconomy.QuoteSkill(game.State,tier2).Request;
        Save("pending-"+name+"-2",ChapterEconomy.Reserve(game.State,"skill",tier2,request.Sequence,2000,request));
        game.BuySkill(request,2000);Save("owned-"+name+"-2",game.State);
    }
    static void BuyAll(GameSession game)
    {foreach(string id in new[]{SkillIds.Universal1,SkillIds.Universal2,SkillIds.Recycling1,SkillIds.Recycling2,SkillIds.Delivery1,SkillIds.Delivery2})if(!game.Chapter.Skills.Owns(id))game.BuySkill(ChapterEconomy.QuoteSkill(game.State,id).Request,2000);}
    public static void Main(string[] args)
    {
        output=args[0];Directory.CreateDirectory(output);
        var clean=Ready(ModelState.Fresh(1));Save("ready-no-skills",clean.State);
        Branch("universal",SkillIds.Universal1,SkillIds.Universal2,clean.State);
        Branch("recycling",SkillIds.Recycling1,SkillIds.Recycling2,clean.State);
        Branch("delivery",SkillIds.Delivery1,SkillIds.Delivery2,clean.State);
        BuyAll(clean);Save("all-skills-owned",clean.State);
        Save("pending-all-skills-spin",ChapterEconomy.Reserve(clean.State,"spin",null,clean.State.sequence+1,2000));
        clean.ExecuteChapterSpin(clean.State.sequence+1,2000);Save("committed-all-skills-spin",clean.State);
        var legacy=Ready(Codec.Decode(File.ReadAllText(args[1])));Save("canonical-boost-owned",legacy.State);
        var upgrade=ChapterEconomy.QuoteSkill(legacy.State,SkillIds.Universal2).Request;
        Save("pending-canonical-universal-2",ChapterEconomy.Reserve(legacy.State,"skill",SkillIds.Universal2,upgrade.Sequence,2000,upgrade));
        BuyAll(legacy);Save("canonical-all-skills-owned",legacy.State);
        Save("pending-canonical-spin",ChapterEconomy.Reserve(legacy.State,"spin",null,legacy.State.sequence+1,2000));
        legacy.ExecuteChapterSpin(legacy.State.sequence+1,2000);Save("committed-canonical-spin",legacy.State);
        File.WriteAllText(Path.Combine(output,"codec-facts.json"),JsonSerializer.Serialize(tokens)+"\n");
    }
}
