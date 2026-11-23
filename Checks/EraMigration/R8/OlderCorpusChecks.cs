#if JOURNEY_CHECKS
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;

internal static class OlderCorpusChecks
{
    sealed class Storage:ISnapshotStorage
    {public string raw;public int writes;public string ReadPrimary()=>raw;public string ReadBackup()=>null;public void Write(string next){writes++;raw=next;}}
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static int assertions;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static (long,uint,long) Pending(string name)
    {
        switch(name){
            case "pending-acquire-workshop-spin":return (7,2647435461,2);
            case "pending-finish-house-spin":return (4,2647435461,2);
            case "pending-select-workshop":return (0,1,1);
            case "pending-slot-purchase":return (340,12305,4);
            case "pending-effects-spin":return (351,2451568673,6);
            case "pending-offline":return (361,2451568673,7);
            case "pending-carry-spin":return (370,2793601271,9);
            case "pending-acquire-pigeon-spin":return (18,2647435461,3);
            case "pending-select-house-repair":return (37,1,1);
            case "pending-select-house-improve":case "pending-select-recycler":case "pending-select-pigeon":return (0,1,1);
            default:throw new Exception("Unknown literal old pending name: "+name);
        }
    }
    public static void Run()
    {
        int count=0;
        foreach(string era in new[]{"r6","r7"})
        {
            string folder=Path.Combine(AppContext.BaseDirectory,"OlderFixtures",era);
            using var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"provenance.json")));
            foreach(var file in Directory.GetFiles(folder,"*.json").Where(p=>Path.GetFileName(p)!="provenance.json"))
            {
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))==provenance.RootElement.GetProperty("fixtureSha256").GetProperty(Path.GetFileName(file)).GetString(),"Old r6/r7 bytes changed");
                string raw=File.ReadAllText(file).TrimEnd('\r','\n');using var json=JsonDocument.Parse(raw);var root=json.RootElement;
                bool pending=root.GetProperty("pending").ValueKind!=JsonValueKind.Null;
                var expected=pending?Pending(Path.GetFileNameWithoutExtension(file)):(root.GetProperty("coins").GetInt64(),root.GetProperty("seed").GetUInt32(),root.GetProperty("sequence").GetInt64());
                var storage=new Storage{raw=raw};var game=GameSession.Open(new GameStore(storage,Codec),987);
                Check(!game.RecoveryRequired,file+": "+game.Error);Check(game.State.coins==expected.Item1&&game.State.seed==expected.Item2&&game.State.sequence==expected.Item3,"Old pending debit/reward/RNG or grant changed");
                Check(game.State.journey==null&&game.State.chapter==null&&storage.writes==(pending?1:0),"New Journey adopted/eagerly promoted an old literal operation");
                if(!pending)Check(storage.raw==raw,"Old committed bytes rewritten");
                string committed=Codec.Encode(game.State);int writes=storage.writes;
                var query=game.Journey;Check(storage.writes==writes&&Codec.Encode(game.State)==committed,"Old Journey query wrote or granted");
                game=GameSession.Open(new GameStore(storage,Codec),987);Check(!game.RecoveryRequired&&storage.writes==writes&&Codec.Encode(game.State)==committed,"Old reopen replayed/fresh-granted");count++;
            }
        }
        Check(count==33,"Old literal corpus lost a fixture");Console.WriteLine("PASS all33 preserved r6/r7 literals under actual V3/Journey: "+assertions+" counted assertions");
    }
}
#endif
