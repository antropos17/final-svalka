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
    // Literal expectations from the independent milestone oracle; production commands
    // create valid fixtures, never the expected milestone/count values.
    internal static class MilestoneRegressions
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static readonly string[] Ids = { "milestone.house-repaired", "milestone.workshop-built", "milestone.junkyard-restored", "milestone.chapter-restored" };
        internal static List<Obj> Run(string repo, string workspace)
        {
            string root = SelfTest.PrepareWorkspace(repo, workspace);
            var cases = new List<Obj>();
            void Case(string id, Action action) {
                var result = new Obj().Add("id", id);
                try { action(); result.Add("passed", true); }
                catch(Exception error) when (!(error is OutOfMemoryException)) { result.Add("passed", false).Add("failure", error.GetType().Name + ": " + Report.Ascii(error.Message)); }
                cases.Add(result);
            }
            string Save(string name, ModelState state) {
                string path = Path.Combine(root, name); File.WriteAllText(path, Codec.Encode(state), new UTF8Encoding(false)); return path;
            }
            var legacy = ModelState.Fresh(1); legacy.coins = 20000; legacy.houseStage = 2;
            string houseOnly = Save("synthetic-legacy-house2.json", legacy);
            var s = ModelState.Fresh(1); s.coins = 20000;
            for(int level=1;level<=2;level++) foreach(string family in FamilyIds.All)
                s = Commit(JourneyEconomy.ReserveChapter(s, "select", ChapterIds.Project(EraIds.Junkyard,family,level),s.sequence+1));
            string twelve = Save("twelve.json", s);
            var entered = Commit(JourneyEconomy.ReserveChapter(s,"enter-era",null,s.sequence+1));
            string entry = Save("entry-unseen.json", entered);
            s = Commit(JourneyEconomy.ReserveChapter(entered,"era-seen",null,entered.sequence+1));
            string acknowledged = Save("entry-acknowledged.json", s);
            for(int level=1;level<=2;level++) foreach(string family in FamilyIds.All) {
                if(level==2 && family==FamilyIds.PowerSource) continue;
                s = Commit(JourneyEconomy.ReserveChapter(s,"select",ChapterIds.Project(EraIds.WorkshopYard,family,level),s.sequence+1));
            }
            string twentyThree = Save("twenty-three.json", s);
            var pending = JourneyEconomy.ReserveChapter(s,"select",ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.PowerSource,2),s.sequence+1);
            string pendingFinal = Save("pending-final.json", pending);
            string final = Save("final.json", Commit(pending));

            Case("milestones-legacy-house-only-is-not-full-chapter", () => {
                using(var r=Run(root,"inspect",new[]{"--input",houseOnly}))
                    Facts(r.RootElement.GetProperty("facts").GetProperty("milestones"),2,new[]{true,false,false,false},"ChooseConstruction");
            });
            Case("milestones-free-entry-stays-twelve", () => {
                foreach(var pair in new[]{(twelve,"EnterWorkshopYard"),(entry,"ContinueEraEntry"),(acknowledged,"ChooseConstruction")})
                    using(var r=Run(root,"inspect",new[]{"--input",pair.Item1}))
                        Facts(r.RootElement.GetProperty("facts").GetProperty("milestones"),12,new[]{true,true,true,false},pair.Item2);
            });
            Case("milestones-final-command-preview-and-diff", () => {
                using(var r=Run(root,"inspect",new[]{"--input",twentyThree}))
                    Facts(r.RootElement.GetProperty("facts").GetProperty("milestones"),23,new[]{true,true,true,false},"ChooseConstruction");
                using(var r=Run(root,"preview-pending",new[]{"--input",pendingFinal})) {
                    var m=r.RootElement.GetProperty("milestones");
                    Facts(m.GetProperty("before"),23,new[]{true,true,true,false},"ChooseConstruction");
                    Facts(m.GetProperty("after"),24,new[]{true,true,true,true},"FinishOrContinueOptionally");
                    Need(r.RootElement.GetProperty("operation").GetProperty("coinsAfter").GetInt64()==10690,"Final chapter must spend9310 from20000");
                }
                using(var r=Run(root,"diff",new[]{"--before",twentyThree,"--after",final})) {
                    var changes=r.RootElement.GetProperty("changes").EnumerateArray().ToArray();
                    Need(changes.Any(c=>c.GetProperty("group").GetString()=="milestones" && c.GetProperty("path").GetString()=="milestones.constructionCount" && c.GetProperty("before").GetString()=="23" && c.GetProperty("after").GetString()=="24"),"Missing grouped23->24");
                    Need(r.RootElement.GetProperty("groupsChanged").EnumerateArray().Any(g=>g.GetString()=="milestones"),"Milestone group omitted");
                }
                using(var r=Run(root,"preview-pending",new[]{"--input",final},5)) Need(r.RootElement.GetProperty("status").GetString()=="not-applicable","Paid final replayed");
            });
            Case("milestones-old540-selection-is-not-ready-at480", () => {
                string source=Path.Combine(repo,"tools/SaveInspector/evidence/r11/current-selected540.json");
                string path=Path.Combine(root,"literal-v2-selected540.json"); File.Copy(source,path);
                using(var r=Run(root,"inspect",new[]{"--input",path})) {
                    var f=r.RootElement.GetProperty("facts"); var selection=f.GetProperty("selection");
                    Need(selection.GetProperty("selectedPrice").GetInt64()==540 && selection.GetProperty("currentReselectionOfferPrice").GetInt64()==480 && !selection.GetProperty("affordableNow").GetBoolean(),"Read repriced or invented affordability");
                    Facts(f.GetProperty("milestones"),4,new[]{true,true,false,false},"InspectSelectedProject");
                    Need(f.GetProperty("milestones").GetProperty("selectedProjectId").GetString()=="house.improve","Lost selected identity");
                }
            });
            Case("milestones-earlier-snapshot-honestly-loses-current-facts", () => {
                using(var r=Run(root,"diff",new[]{"--before",final,"--after",houseOnly})) {
                    var changes=r.RootElement.GetProperty("changes").EnumerateArray().ToArray();
                    Need(changes.Any(c=>c.GetProperty("path").GetString()=="milestones.milestones[3].reached" && c.GetProperty("before").GetString()=="true" && c.GetProperty("after").GetString()=="false"),"Restored earlier facts retained final badge");
                }
                using(var r=Run(root,"inspect",new[]{"--input",houseOnly}))
                    Facts(r.RootElement.GetProperty("facts").GetProperty("milestones"),2,new[]{true,false,false,false},"ChooseConstruction");
            });
            Case("milestones-do-not-change-canonical-equality-or-clock-only-diff", () => {
                string clock=Save("legacy-clock.json",Commit(GameLogic.Reserve(legacy,"clock",0,legacy.sequence+1,1000)));
                using(var r=Run(root,"diff",new[]{"--before",houseOnly,"--after",clock})) {
                    var groups=r.RootElement.GetProperty("groupsChanged").EnumerateArray().ToArray();
                    Need(groups.Length==1 && groups[0].GetString()=="clockSequence","Passive derived facts changed on clock only");
                    Need(!r.RootElement.GetProperty("equal").GetBoolean(),"Clock delta called canonical equality");
                }
                using(var r=Run(root,"diff",new[]{"--before",final,"--after",final}))
                    Need(r.RootElement.GetProperty("equal").GetBoolean() && r.RootElement.GetProperty("changes").GetArrayLength()==0,"Derived group changed equal canonical result");
            });
            return cases;
        }
        static ModelState Commit(ModelState state) { return Codec.Decode(Codec.Encode(GameLogic.ApplyPending(state))); }
        static void Facts(JsonElement facts,int count,bool[] flags,string action) {
            Need(facts.GetProperty("source").GetString()=="derived" && facts.GetProperty("scope").GetString()=="current-save-facts","Wrong milestone source/scope");
            Need(facts.GetProperty("constructionCount").GetInt32()==count && facts.GetProperty("totalConstructions").GetInt32()==24,"Wrong literal construction progress");
            Need(facts.GetProperty("reachedCount").GetInt32()==flags.Count(x=>x),"Wrong reached count");
            var entries=facts.GetProperty("milestones").EnumerateArray().ToArray(); Need(entries.Length==4,"Wrong milestone cardinality");
            for(int i=0;i<4;i++) Need(entries[i].GetProperty("id").GetString()==Ids[i] && entries[i].GetProperty("reached").GetBoolean()==flags[i],"Wrong stable milestone identity/fact");
            Need(facts.GetProperty("nextAction").GetString()==action,"Wrong next action identity");
        }
        static JsonDocument Run(string root,string command,string[] options,int expected=0) {
            var args=new List<string>{command,"--workspace",root,"--format","json"};args.AddRange(options);
            var before=Directory.GetFiles(root).ToDictionary(p=>p,p=>InputGuard.Hash(File.ReadAllBytes(p)));
            using(var output=new MemoryStream()) {
                int exit=Cli.Run(args.ToArray(),output);
                Need(Directory.GetFiles(root).Length==before.Count && before.All(p=>File.Exists(p.Key)&&InputGuard.Hash(File.ReadAllBytes(p.Key))==p.Value),"Passive CLI changed private input bytes/inventory");
                Need(exit==expected,"Unexpected CLI exit"+exit); return JsonDocument.Parse(output.ToArray());
            }
        }
        static void Need(bool value,string error) { if(!value)throw new InvalidOperationException(error); }
    }
}