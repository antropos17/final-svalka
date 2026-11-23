using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.SaveInspector;

static class ArcadeChecks
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static long Number(JsonElement e,string key)=>e.GetProperty(key).GetInt64();
    internal static List<Obj> Run(string repo,string workspace)
    {
        string root=SelfTest.PrepareWorkspace(repo,workspace);var cases=new List<Obj>();int calls=0;
        string Copy(string relative){string path=Path.Combine(root,Path.GetFileName(relative));File.Copy(Path.Combine(repo,relative),path,false);return path;}
        var inputs=new Dictionary<string,string>();
        foreach(string name in new[]{"memory-partial","memory-zero-ready","sorting-ready-three","claim-pending","claim-committed","slot-three-boost-armed","slot-three-bonus","selected540-spin","v3-old-feature-commands"})
            inputs.Add(name,Copy("Checks/ArcadeCore/Evidence/"+name+".json"));
        string v1=Copy("Checks/ExpansionCore/Fixtures/v1-rich-owned-loadout.json"),v2=Copy("Checks/ArcadeCore/Fixtures/v2-focused-boost-committed.json");
        // A genuine NEW public reservation, never a fabricated descriptor.
        var old=Codec.Decode(File.ReadAllText(Path.Combine(repo,"Checks/Milestones/fixtures/v2-selected540.json")));
        var spin=GameLogic.Reserve(old,"spin",0,old.sequence+1);
        string spinPath=Path.Combine(root,"new-v3-spin-pending.json");File.WriteAllText(spinPath,Codec.Encode(spin),new UTF8Encoding(false));
        JsonDocument Run(string command,string input,string after=null){
            calls++;var args=new List<string>{command,"--workspace",root,"--format","json"};
            if(after==null){args.Add("--input");args.Add(input);}else{args.Add("--before");args.Add(input);args.Add("--after");args.Add(after);}
            var before=Directory.GetFiles(root).ToDictionary(p=>p,p=>InputGuard.Hash(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
            using(var output=new MemoryStream()){
                int exit=Cli.Run(args.ToArray(),output);
                Need(exit==0,"Arcade Inspector CLI refused "+command);
                Need(Directory.GetFiles(root).Length==before.Count&&before.All(p=>File.Exists(p.Key)&&InputGuard.Hash(File.ReadAllBytes(p.Key))+"|"+File.GetLastWriteTimeUtc(p.Key).Ticks==p.Value),"Inspector changed copied bytes/inventory/mtime");
                var doc=JsonDocument.Parse(output.ToArray());Need(doc.RootElement.GetProperty("tool").GetProperty("version").GetString()=="1.6.0","Tool version not current");return doc;
            }
        }
        void Pass(string name){cases.Add(new Obj().Add("case",name).Add("passed",true));}
        foreach(var pair in inputs){using(var doc=Run("inspect",pair.Value)){
            var facts=doc.RootElement.GetProperty("facts");var a=facts.GetProperty("arcade");
            Need(facts.GetProperty("format").GetProperty("layer").GetString()=="expansion-r15"&&a.GetProperty("source").GetString()=="stored","Stored V3 mislabeled");
            Need(Number(a,"ticketCap")==3&&Number(a,"ticketCadence")==8,"Ticket constants drift");
            string raw=a.GetRawText();Need(!raw.Contains("challengeSeed")&&!raw.Contains("boardToken")&&!raw.Contains("transcript"),"Hidden challenge state exported");
            var run=a.GetProperty("activeRun");
            if(pair.Key=="memory-partial"){
                Need(run.GetProperty("phase").GetString()=="first"&&Number(run,"attempts")==2&&Number(run,"cursor")==5&&Number(run,"matchedPairs")==1,"Memory counters drift");
                Need(run.GetProperty("memoryFaces").EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(new[]{-1,2,2,-1,-1,-1})&&run.GetProperty("readyReward").ValueKind==JsonValueKind.Null,"Unrevealed Memory faces/reward leaked");
            }
            if(pair.Key=="memory-zero-ready")Need(run.GetProperty("status").GetString()=="ready-unpaid"&&Number(run,"attempts")==9&&Number(run,"readyReward")==0&&a.GetProperty("latestClaim").ValueKind==JsonValueKind.Null,"Zero Ready relabeled claimed");
            if(pair.Key=="sorting-ready-three")Need(Number(run,"attempts")==8&&Number(run,"correct")==8&&Number(run,"readyReward")==3&&Number(run,"currentSortingSymbol")==-1&&run.GetProperty("memoryFaces").GetArrayLength()==0,"Sorting Ready leaks future item or wrong score");
            if(pair.Key=="slot-three-boost-armed")Need(Number(a,"slotLevel")==3&&Number(a,"diamonds")==14&&Number(a,"boxPresencePartBonus")==1&&Number(a,"potatoPresencePotatoBonus")==1&&Number(a,"rawBonusPartsAddition")==1&&Number(a,"rawBonusPotatoesAddition")==1,"Actual cumulative slot attributes drift");
            if(pair.Key=="selected540-spin")Need(Number(a.GetProperty("lastResourceReceipt"),"constructionDebit")==540&&Number(a.GetProperty("lastResourceReceipt"),"grossCoinIncome")>0,"Saved540 debit collapsed into net income");
            if(pair.Key=="claim-committed")Need(a.GetProperty("activeRun").ValueKind==JsonValueKind.Null&&a.GetProperty("latestClaim").GetProperty("status").GetString()=="retained-historical-already-credited"&&Number(a.GetProperty("latestClaim"),"diamonds")==3,"Retained claim relabeled payable");
            Need(doc.RootElement.GetProperty("input").GetProperty("canonicalSha256").GetString()==InputGuard.HashText(Codec.Encode(Codec.Decode(File.ReadAllText(pair.Value)))),"Canonical input changed");
            Pass("copied-facts-"+pair.Key);
        }}
        foreach(var item in new[]{Tuple.Create(v1,"expansion-r13"),Tuple.Create(v2,"expansion-r14")})using(var doc=Run("inspect",item.Item1)){
            var f=doc.RootElement.GetProperty("facts");var a=f.GetProperty("arcade");
            Need(f.GetProperty("format").GetProperty("layer").GetString()==item.Item2&&a.GetProperty("source").GetString()=="projected-not-persisted"&&Number(a,"diamonds")==0&&Number(a,"tickets")==0&&a.GetProperty("activeRun").ValueKind==JsonValueKind.Null,"Old input retroactively adopted or credited");
            Pass("old-no-adoption-"+item.Item2);
        }
        using(var doc=Run("preview-pending",inputs["claim-pending"])){
            var r=doc.RootElement;var op=r.GetProperty("operation");var a=r.GetProperty("arcade");
            Need(op.GetProperty("expansionWalletAuthority").GetString()=="dedicated-frozen-arcade-command"&&op.GetProperty("arcadeAction").GetString()=="claim"&&Number(op,"walletDelta")==0,"Dedicated Arcade wallet authority wrong");
            Need(a.GetProperty("before").GetProperty("activeRun").GetProperty("status").GetString()=="ready-unpaid"&&a.GetProperty("computedAfter").GetProperty("activeRun").ValueKind==JsonValueKind.Null&&Number(a.GetProperty("computedAfter").GetProperty("latestClaim"),"diamonds")==3,"Claim preview failed unpaid-to-retained distinction");
            Need(r.GetProperty("afterCanonicalSha256").GetString()==InputGuard.HashText(Codec.Encode(Codec.Decode(File.ReadAllText(inputs["claim-committed"]))))&&r.GetProperty("checks").GetProperty("resultMatchesFrozenArcade").GetBoolean(),"Claim preview canonical result mismatch");
            Pass("genuine-claim-preview-once-only-facts");
        }
        using(var doc=Run("preview-pending",spinPath)){
            var r=doc.RootElement;var a=r.GetProperty("arcade");
            Need(a.GetProperty("before").GetProperty("source").GetString()=="projected-not-persisted"&&a.GetProperty("computedAfter").GetProperty("source").GetString()=="stored"&&Number(a.GetProperty("computedAfter"),"ticketProgress")==1,"Pending adoption treated as already stored or retroactive ticket");
            Need(r.GetProperty("operation").GetProperty("expansionWalletAuthority").GetString()=="lower-frozen-command"&&r.GetProperty("operation").GetProperty("frozenExpansionCoinsAfter").ValueKind==JsonValueKind.Null&&r.GetProperty("checks").GetProperty("resultMatchesFrozenArcade").GetBoolean(),"Composed lower wallet mislabeled dedicated");
            Pass("new-spin-pending-before-and-computed-after");
        }
        using(var doc=Run("diff",inputs["claim-pending"],inputs["claim-committed"])){
            Need(doc.RootElement.GetProperty("groupsChanged").EnumerateArray().Any(x=>x.GetString()=="arcade"),"Arcade change omitted from semantic groups");
            Pass("claim-diff-visible-arcade-group");
        }
        cases.Add(new Obj().Add("case","bounded-read-only-summary").Add("passed",true).Add("cliCalls",calls).Add("copiedInputs",inputs.Count+2).Add("publicReservationInputs",1));
        return cases;
    }
}
