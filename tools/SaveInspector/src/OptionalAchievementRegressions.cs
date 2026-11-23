using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Contracts;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    // Independent literal expectations. Reuse the main harness's existing funded
    // completed chapter; real pure commands create receipts, never fabricated flags.
    internal static class OptionalAchievementRegressions
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        internal static List<Obj> Run(string repo,string workspace,string completeInput)
        {
            string root=SelfTest.PrepareWorkspace(repo,workspace);var cases=new List<Obj>();int calls=0;
            var completeBytes=File.ReadAllBytes(completeInput);string completeHash=InputGuard.Hash(completeBytes);
            var complete=Codec.Decode(Encoding.UTF8.GetString(completeBytes));
            Need(complete.coins==690&&complete.chapter.Skills.Receipts.Count==0&&complete.journey.Orders.LatestClaim==null,"Existing funded9310 chapter fixture differs");
            string Save(string name,ModelState state){string path=Path.Combine(root,name);File.WriteAllText(path,Codec.Encode(state),new UTF8Encoding(false));return path;}
            JsonDocument Run(string command,string[] options,int expected=0){
                calls++;var args=new List<string>{command,"--workspace",root,"--format","json"};args.AddRange(options);
                var before=Directory.GetFiles(root).ToDictionary(path=>path,path=>InputGuard.Hash(File.ReadAllBytes(path))+":"+File.GetLastWriteTimeUtc(path).Ticks);
                using(var output=new MemoryStream()){
                    int exit=Cli.Run(args.ToArray(),output);
                    Need(Directory.GetFiles(root).Length==before.Count&&before.All(p=>File.Exists(p.Key)&&InputGuard.Hash(File.ReadAllBytes(p.Key))+":"+File.GetLastWriteTimeUtc(p.Key).Ticks==p.Value),"Passive CLI changed private bytes/inventory/mtime");
                    Need(exit==expected,"Unexpected CLI exit "+exit);return JsonDocument.Parse(output.ToArray());
                }
            }
            void Case(string id,Action action){var result=new Obj().Add("id",id);int start=calls;
                try{action();result.Add("passed",true);}catch(Exception error)when(!(error is OutOfMemoryException)){result.Add("passed",false).Add("failure",error.GetType().Name+": "+Report.Ascii(error.Message));}
                result.Add("cliCalls",calls-start);cases.Add(result);
            }
            string original=Save("complete-no-skills-no-claim.json",complete);
            var ready=Commit(JourneyEconomy.Reserve(complete,"order-accept","order.sort-salvage",0,complete.sequence+1));
            for(int spin=0;spin<80&&!ready.journey.Orders.Ready;spin++)ready=Commit(JourneyEconomy.ReserveChapter(ready,"spin",null,ready.sequence+1));
            Need(ready.journey.Orders.Ready&&ready.journey.Orders.LatestClaim==null,"Finite real foreground fixture did not reach first ready order");
            string readyPath=Save("ready-never-claimed.json",ready);
            var claimPending=JourneyEconomy.Reserve(ready,"order-claim","order.sort-salvage",ready.journey.Orders.InstanceSequence,ready.sequence+1);
            string claimPendingPath=Save("claim-pending.json",claimPending);
            var claimed=Commit(claimPending);string claimedPath=Save("claimed120.json",claimed);
            Need(claimed.coins==checked(ready.coins+120),"Explicit first claim must add literal120 once");
            var five=complete;
            string[] skills={SkillIds.Universal1,SkillIds.Universal2,SkillIds.Recycling1,SkillIds.Recycling2,SkillIds.Delivery1,SkillIds.Delivery2};
            foreach(string id in skills.Take(5))five=Buy(five,id);
            Need(five.coins==100&&five.chapter.Skills.Receipts.Count==5,"Literal first-five cost590 from690 differs");
            string fivePath=Save("five-skills.json",five);
            var sixthPending=ChapterEconomy.Reserve(five,"skill",SkillIds.Delivery2,five.sequence+1,skillRequest:ChapterEconomy.QuoteSkill(five,SkillIds.Delivery2).Request);
            string sixthPendingPath=Save("sixth-skill-pending.json",sixthPending);
            var six=Commit(sixthPending);string sixPath=Save("all-six-skills.json",six);
            Need(six.coins==80,"All six current skill purchases must cost literal610 from690");

            Case("optional-base-and-ready-do-not-invent-earnings",()=>{
                foreach(string path in new[]{original,readyPath})using(var r=Run("inspect",new[]{"--input",path})){
                    var f=r.RootElement.GetProperty("facts");Facts(f.GetProperty("optionalAchievements"),false,false);
                    Need(f.GetProperty("milestones").GetProperty("constructionCount").GetInt32()==24&&f.GetProperty("milestones").GetProperty("reachedCount").GetInt32()==4,"Optional facts altered chapter24/four milestone meaning");
                }
            });
            Case("optional-five-to-six-current-and-literal-legacy-skills",()=>{
                using(var r=Run("inspect",new[]{"--input",fivePath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),false,false);
                using(var r=Run("preview-pending",new[]{"--input",sixthPendingPath})){
                    var f=r.RootElement.GetProperty("optionalAchievements");Facts(f.GetProperty("before"),false,false);Facts(f.GetProperty("computedAfter"),true,false);
                    Need(r.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64()==80,"Computed sixth skill debit must leave80");
                }
                using(var r=Run("inspect",new[]{"--input",sixPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,false);
                string literal=Path.Combine(root,"literal-old-six-skills.json");File.Copy(Path.Combine(repo,"Checks/EraMigration/R8/Fixtures/all-skills-owned.json"),literal);
                using(var r=Run("inspect",new[]{"--input",literal})){
                    Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,false);
                    Need(r.RootElement.GetProperty("facts").GetProperty("wallet").GetProperty("coins").GetInt64()==8160,"Literal old six paid receipts were repriced/refunded");
                }
            });
            Case("optional-pending-claim-is-computed-not-already-earned",()=>{
                using(var r=Run("inspect",new[]{"--input",claimPendingPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),false,false);
                using(var r=Run("preview-pending",new[]{"--input",claimPendingPath})){
                    var f=r.RootElement.GetProperty("optionalAchievements");Facts(f.GetProperty("before"),false,false);Facts(f.GetProperty("computedAfter"),false,true);
                    Need(r.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64()==ready.coins+120,"Computed claim must add120 once");
                }
                using(var r=Run("inspect",new[]{"--input",claimedPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),false,true);
                using(var r=Run("preview-pending",new[]{"--input",claimedPath},5))Need(r.RootElement.GetProperty("status").GetString()=="not-applicable","Paid claim became a new preview grant");
            });
            ModelState both=claimed;
            Case("optional-mixed-new-active-ready-and-cancel-retain-real-claim",()=>{
                foreach(string id in skills.Take(5))both=Buy(both,id);
                string mixedPath=Save("mixed-five-skills-real-claim.json",both);
                using(var r=Run("inspect",new[]{"--input",mixedPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),false,true);
                both=Buy(both,SkillIds.Delivery2);string bothPath=Save("both-current-facts.json",both);
                using(var r=Run("inspect",new[]{"--input",bothPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,true);
                long priorClaim=both.journey.Orders.LatestClaim.ClaimSequence;
                var newer=Commit(JourneyEconomy.Reserve(both,"order-accept","order.sort-salvage",0,both.sequence+1));
                string activePath=Save("new-active-with-old-claim.json",newer);
                using(var r=Run("inspect",new[]{"--input",activePath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,true);
                for(int spin=0;spin<80&&!newer.journey.Orders.Ready;spin++)newer=Commit(JourneyEconomy.ReserveChapter(newer,"spin",null,newer.sequence+1));
                Need(newer.journey.Orders.Ready&&newer.journey.Orders.LatestClaim.ClaimSequence==priorClaim,"New instance fabricated/replaced prior claim");
                string newReadyPath=Save("new-ready-with-old-claim.json",newer);
                using(var r=Run("inspect",new[]{"--input",newReadyPath}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,true);
                var canceled=Commit(JourneyEconomy.Reserve(newer,"order-cancel","order.sort-salvage",newer.journey.Orders.InstanceSequence,newer.sequence+1));
                Need(canceled.coins==newer.coins&&canceled.journey.Orders.LatestClaim.ClaimSequence==priorClaim,"Cancellation rewarded or lost retained claim");
                using(var r=Run("inspect",new[]{"--input",Save("cancel-preserves-old-claim.json",canceled)}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),true,true);
            });
            Case("optional-earlier-snapshot-drops-current-facts-and-diff-groups",()=>{
                string bothPath=Save("restore-before-both.json",both);
                using(var r=Run("diff",new[]{"--before",bothPath,"--after",original})){
                    var changes=r.RootElement.GetProperty("changes").EnumerateArray().ToArray();
                    foreach(int index in new[]{0,1})Need(changes.Any(c=>c.GetProperty("group").GetString()=="optionalAchievements"&&c.GetProperty("path").GetString()=="optionalAchievements.achievements["+index+"].reached"&&c.GetProperty("before").GetString()=="true"&&c.GetProperty("after").GetString()=="false"),"Earlier snapshot retained an invented lifetime optional flag");
                    Need(r.RootElement.GetProperty("groupsChanged").EnumerateArray().Any(g=>g.GetString()=="optionalAchievements"),"Optional changes omitted from semantic grouping");
                }
                using(var r=Run("inspect",new[]{"--input",original}))Facts(r.RootElement.GetProperty("facts").GetProperty("optionalAchievements"),false,false);
            });
            Case("optional-canonical-clock-only-and-invalid-preview-safety",()=>{
                var clock=Commit(JourneyEconomy.ReserveChapter(complete,"clock",null,complete.sequence+1,1000));
                using(var r=Run("diff",new[]{"--before",original,"--after",Save("clock-only.json",clock)})){
                    var groups=r.RootElement.GetProperty("groupsChanged").EnumerateArray().ToArray();Need(groups.Length==1&&groups[0].GetString()=="clockSequence","Derived optional facts broke clock-only coverage");
                    Need(!r.RootElement.GetProperty("equal").GetBoolean(),"Clock metadata became canonical equality");
                }
                using(var r=Run("diff",new[]{"--before",sixPath,"--after",sixPath}))Need(r.RootElement.GetProperty("equal").GetBoolean()&&r.RootElement.GetProperty("changes").GetArrayLength()==0,"Derived optional facts broke canonical equality");
                string raw=Codec.Encode(claimPending),bad=raw.Replace("\"reward\":120,\"composed\":0","\"reward\":121,\"composed\":0");
                Need(raw!=bad,"Private malformed pending setup did not target its actual dedicated reward");
                string path=Path.Combine(root,"invalid-pending-reward.json");File.WriteAllText(path,bad,new UTF8Encoding(false));
                using(var r=Run("preview-pending",new[]{"--input",path},4))Need(r.RootElement.GetProperty("status").GetString()=="snapshot-rejected"&&!r.RootElement.TryGetProperty("optionalAchievements",out _),"Rejected pending exposed successful optional facts");
            });
            Need(InputGuard.Hash(File.ReadAllBytes(completeInput))==completeHash,"Main harness's reused input changed");
            return cases;
        }
        static ModelState Buy(ModelState state,string id){var quote=ChapterEconomy.QuoteSkill(state,id);return Commit(ChapterEconomy.Reserve(state,"skill",id,state.sequence+1,skillRequest:quote.Request));}
        static ModelState Commit(ModelState state){return Codec.Decode(Codec.Encode(GameLogic.ApplyPending(state)));}
        static void Facts(JsonElement facts,bool skills,bool claim){
            Need(facts.GetProperty("source").GetString()=="derived"&&facts.GetProperty("scope").GetString()=="current-save-facts","Wrong optional source/scope");
            Need(facts.EnumerateObject().Count()==3,"Unexpected optional counter/reward/time/history field");
            var entries=facts.GetProperty("achievements").EnumerateArray().ToArray();Need(entries.Length==2,"Wrong optional identity cardinality");
            Need(entries[0].GetProperty("id").GetString()=="optional.all-skills-owned"&&entries[0].GetProperty("reached").GetBoolean()==skills,"Wrong current six-skill fact");
            Need(entries[1].GetProperty("id").GetString()=="optional.retained-order-claim"&&entries[1].GetProperty("reached").GetBoolean()==claim,"Wrong retained-collected-order fact");
            foreach(var entry in entries)Need(entry.EnumerateObject().Count()==2,"Unexpected optional reward/lifetime/locked label");
        }
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    }
}