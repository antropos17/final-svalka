using System;
using System.IO;
using JunkToNeon.Core;
#if LEGACY_R7
using JunkToNeon.Contracts;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;
#endif

// Runs only against source extracted from pinned historical Git revisions.
// Never link this generator to the current production serializer to refresh old fixtures.
internal static class LegacyGenerator
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string directory;
    static ModelState Apply(ModelState state,string kind,int target=0,long at=0)
    {
#if LEGACY_R6
        return GameLogic.ApplyPending(GameLogic.Reserve(state,kind,target,state.sequence+1));
#else
        return GameLogic.ApplyPending(GameLogic.Reserve(state,kind,target,state.sequence+1,at));
#endif
    }
    static ModelState Pending(ModelState state,string kind,int target=0,long at=0)
    {
#if LEGACY_R6
        return GameLogic.Reserve(state,kind,target,state.sequence+1);
#else
        return GameLogic.Reserve(state,kind,target,state.sequence+1,at);
#endif
    }
    static void Save(string name,ModelState state)
    {
        var raw=Codec.Encode(state);
        Codec.Decode(raw);
        File.WriteAllText(Path.Combine(directory,name+".json"),raw+"\n");
        Console.WriteLine(name+": wallet="+state.coins+", seed="+state.seed+", seq="+state.sequence+", target="+state.selectedStage);
    }
    static ModelState Repaired(long coins,uint seed=1)
    { var s=ModelState.Fresh(seed);s.coins=coins;s.houseStage=1;s.autoHintSeen=1;return s; }
    public static void Main(string[] args)
    {
        directory=args[0];Directory.CreateDirectory(directory);
        Save("fresh",ModelState.Fresh(1));
        var workshop=Repaired(119);workshop=Apply(workshop,"select",3);
        Save("selected-workshop",workshop);
        Save("pending-acquire-workshop-spin",Pending(workshop,"spin"));
        Save("committed-acquire-workshop-spin",Apply(workshop,"spin"));
        var house=Repaired(595);house.workshopBuilt=1;house=Apply(house,"select",2);
        Save("pending-finish-house-spin",Pending(house,"spin"));
        Save("committed-finish-house-spin",Apply(house,"spin"));
        Save("pending-select-workshop",Pending(Repaired(120),"select",3));
        var complete=Repaired(42);complete.houseStage=2;complete.workshopBuilt=1;complete.sequence=42;complete.revision=84;
        Save("old-complete",complete);
#if LEGACY_R7
        uint seed=1;
        for(;seed<1000000;seed++) { var p=GameLogic.Draw(seed,1);if(p.symbols[0]==3&&p.symbols[1]==3&&p.symbols[2]==2)break; }
        if(seed==1000000)throw new Exception("Deterministic seed not found");
        Console.WriteLine("Independent expected symbol fixture seed="+seed+" => [3,3,2]");
        var owned=Repaired(1000,seed);owned.lastSettledAt=1000;
        owned=Apply(owned,"select",3,1000);owned=Apply(owned,"select",4,1000);owned=Apply(owned,"select",5,1000);
        Save("pending-slot-purchase",Pending(owned,"upgrade",0,1000));
        owned=Apply(owned,"upgrade",0,1000);owned=Apply(owned,"select",2,1000);
        Save("owned-selected-house",owned);
        Save("pending-effects-spin",Pending(owned,"spin",0,1000));
        owned=Apply(owned,"spin",0,1000);
        Save("owned-carry-two",owned);
        Save("pending-offline",Pending(owned,"settle",0,1600));
        owned=Apply(owned,"settle",0,1600);Save("owned-offline-unseen",owned);
        owned=Apply(owned,"seen",0,1600);Save("owned-offline-seen",owned);
        Save("pending-carry-spin",Pending(owned,"spin",0,1600));
        Save("committed-carry-spin",Apply(owned,"spin",0,1600));
        var pigeon=Repaired(239);pigeon.workshopBuilt=1;pigeon.lastSettledAt=1000;
        pigeon=Apply(pigeon,"select",5,1000);Save("selected-pigeon",pigeon);
        pigeon=Apply(pigeon,"settle",0,1600);Save("offline-funded-pigeon",pigeon);
        Save("pending-acquire-pigeon-spin",Pending(pigeon,"spin",0,1600));
        Save("committed-acquire-pigeon-spin",Apply(pigeon,"spin",0,1600));
        // Each old integer is paired with its actual historical meaning before migration.
        var repair=ModelState.Fresh(1);Save("pending-select-house-repair",Pending(repair,"select",1));
        Save("pending-select-house-improve",Pending(Repaired(600),"select",2));
        Save("pending-select-recycler",Pending(Repaired(180),"select",4));
        Save("pending-select-pigeon",Pending(Repaired(240),"select",5));
#endif
    }
}
