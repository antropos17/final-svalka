#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed class PrototypeEndToEndUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string name)=>(T)Game.GetType().GetField(name,Hidden).GetValue(Game);
        bool Flag(string name)=>(bool)Game.GetType().GetProperty(name).GetValue(Game);
        IEnumerator Harness(string name,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name,Hidden).Invoke(ui,args);
        string State=>codec.Encode(Session.State);
        int Count=>Session.State.chapter?.Progress.CompletedUpgradeIds.Count??0;
        int spins,actions,records;long lastSpin;double started;string output;
        [Serializable]sealed class Progress
        {public string phase;public int frame,spins,actions,buildings,tickets;public double seconds;public long coins,diamonds;public uint seed;}
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Same(string state,Dictionary<string,string> files){Assert.That(State,Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));}
        void Bound()
        {
            Assert.That(Time.realtimeSinceStartupAsDouble-started,Is.LessThan(2040));Assert.That(spins,Is.LessThanOrEqualTo(1000));Assert.That(actions,Is.LessThanOrEqualTo(1200));
            Assert.That(Session.RecoveryRequired,Is.False,Session.Error);Assert.That(Session.State.coins,Is.GreaterThanOrEqualTo(0));
        }
        void Record(string phase)
        {
            Bound();Assert.That(++records,Is.LessThan(256));
            string line=JsonUtility.ToJson(new Progress{phase=phase,frame=Time.frameCount,seconds=Time.realtimeSinceStartupAsDouble-started,spins=spins,actions=actions,buildings=Count,coins=Session.State.coins,seed=Session.State.seed,tickets=Session.Arcade.Tickets,diamonds=Session.Arcade.Diamonds});
            File.AppendAllText(Path.Combine(output,"trace.jsonl"),line+"\n");File.WriteAllText(Path.Combine(output,"status.json"),line);
            if(phase!="spinning")foreach(string file in Directory.GetFiles(Folder,"jtn.*.json"))File.Copy(file,Path.Combine(output,"checkpoint-"+Path.GetFileName(file)),true);
        }
        void Observe()
        {
            Bound();var spin=Session.State.lastSpin;if(spin==null||spin.sequence==lastSpin)return;lastSpin=spin.sequence;spins++;
            var e=Session.Expansion;Assert.That(e.Parts,Is.InRange(0,e.PartsCap));Assert.That(e.Potatoes,Is.InRange(0,e.PotatoesCap));Assert.That(e.Energy,Is.InRange(0,e.EnergyCap));
            if(spins%16==0)Record("spinning");
        }
        IEnumerator Tap(string name,int index=-1)
        {
            Bound();Assert.That(++actions,Is.LessThanOrEqualTo(1200));
            var button=index<0?Field<Button>(name):Field<Button[]>(name)[index];float deadline=Time.realtimeSinceStartup+5;
            while((!button.isActiveAndEnabled||!button.IsInteractable())&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return Harness("PointerClick",name,index);yield return null;Observe();
        }
        IEnumerator Idle()
        {
            float deadline=Time.realtimeSinceStartup+10;while(Flag("Busy")&&Time.realtimeSinceStartup<deadline){Observe();yield return null;}
            Assert.That(Flag("Busy"),Is.False);
            while((int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game)>0&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;Observe();
        }
        IEnumerator Root()
        {
            yield return Idle();
            if(Field<bool>("autoHintPanel")||Field<bool>("offlineSummaryPanel"))yield return Tap("closeButton");
            if(Session.Chapter.TransitionPending)yield return Tap("selectButton");
            yield return Tap("productTabs",1);
        }
        IEnumerator ManualSpin(){yield return Root();yield return Tap("spinButton");yield return Idle();}
        IEnumerator Earn(long price){while(Session.State.coins<price)yield return ManualSpin();}
        long Price()
        {
            string text=Regex.Replace(Field<TMP_Text>("panelBody").text,"<[^>]+>","");var match=Regex.Match(text,@"Price:\s*(\d+)");
            Assert.That(match.Success,Is.True,text);return long.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;
        }
        IEnumerator ExpansionCard(string id)
        {
            for(int page=0;page<6;page++)
            {
                int index=Array.IndexOf(Field<string[]>("expansionCardIds"),id);
                if(index>=0){yield return Tap("expansionCards",index);yield break;}
                Assert.That(Field<Button>("expansionNextButton").gameObject.activeInHierarchy,Is.True,"Offer not reachable: "+id);yield return Tap("expansionNextButton");
            }
            Assert.Fail("Offer page bound exceeded: "+id);
        }
        [SetUp]public void Isolate(){ui.IsolateSave();Assert.That(Path.GetFullPath(Folder).Replace('\\','/'),Does.StartWith("X:/PROJECTS/FINAL-SVALKA/.work/"));}
        [UnityTearDown]public IEnumerator Restore(){yield return ui.RestoreAndRemoveOnlyPrivateFixtures();}

        [UnityTest,Category("ExplicitEndToEnd"),Timeout(2100000)]
        public IEnumerator FreshTwoEraPrototypeThroughVisibleUiAndColdReloads(){yield return Journey(false);}
        [UnityTest,Category("ExplicitEndToEnd"),Timeout(2100000)]
        public IEnumerator ResumeEarnedPrototypeCheckpointThroughVisibleUiAndReloads(){yield return Journey(true);}
        IEnumerator Journey(bool resume)
        {
            if(Environment.GetEnvironmentVariable("JTN_RUN_FULL_E2E")!="1")Assert.Ignore("Opt-in real-time full prototype journey; set JTN_RUN_FULL_E2E=1.");
            started=Time.realtimeSinceStartupAsDouble;spins=actions=records=0;lastSpin=0;
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/maintenance-wave2-20261114/e2e"));
            Assert.That(Directory.Exists(output),Is.False,"Keep the first run visible; move it to a named retained run before an explained retry");Directory.CreateDirectory(output);
            if(resume)
            {
                string previous=Path.Combine(Path.GetDirectoryName(output),"e2e-interrupted-at19");
                foreach(string name in new[]{"jtn.save.v1.json","jtn.backup.v1.json"})File.Copy(Path.Combine(previous,"checkpoint-"+name),Path.Combine(Folder,name));
                yield return Harness("LoadGame",false);Assert.That(Count,Is.EqualTo(19));Assert.That(Session.State.coins,Is.EqualTo(18));Assert.That(Session.State.seed,Is.EqualTo(866700034u));Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));lastSpin=Session.State.lastSpin.sequence;Record("resume-earned19-after-editor-exit");
            }
            else{yield return Harness("LoadGame",false);Assert.That(Session.State.coins,Is.EqualTo(37));Assert.That(Count,Is.Zero);Record("fresh37");}
            for(int era=0;era<2;era++)
            {
                string eraId=era==0?EraIds.Junkyard:EraIds.WorkshopYard;
                for(int level=1;level<=2;level++)for(int family=0;family<6;family++)
                {
                    int ordinal=era*12+(level-1)*6+family;if(Count>ordinal)continue;Assert.That(Count,Is.EqualTo(ordinal));
                    yield return Root();string before=State;var files=Files();
                    yield return Tap("productTabs",0);yield return Tap("productCards",0);yield return Tap("navigationCards",family);Same(before,files);
                    string id=ChapterIds.Project(eraId,FamilyIds.All[family],level);Assert.That(Field<string>("chapterProject"),Is.EqualTo(id));
                    long price=Price(),wallet=Session.State.coins;uint seed=Session.State.seed;int count=Count;
                    yield return Tap("selectButton");
                    if(wallet>=price){Assert.That(Count,Is.EqualTo(count+1));Assert.That(Session.State.coins,Is.EqualTo(wallet-price));Assert.That(Session.State.seed,Is.EqualTo(seed));}
                    else
                    {
                        Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(id));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(price));Assert.That(Session.State.coins,Is.EqualTo(wallet));
                        yield return Root();yield return Tap(Session.State.houseStage==0?"spinButton":"autoButton");
                        while(Count==count){Observe();Assert.That(Flag("Busy")||Flag("AutoRunning"),Is.True,"Selected goal stalled");yield return null;}
                        yield return Idle();Assert.That(Session.State.lastSpin.chapter.ActionId,Is.EqualTo(id));Assert.That(Session.State.lastSpin.chapter.Price,Is.EqualTo(price));
                    }
                    Assert.That(Count,Is.EqualTo(count+1));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Flag("AutoRunning"),Is.False,"Paid goals pause at their reveal");
                    Record("built:"+id);yield return Root();
                    if(count==0)
                    {
                        var symbols=Session.State.lastSpin.symbols;int expected=symbols.Distinct().Count()==1?23:symbols.Distinct().Count()==2?8:3;Assert.That(Session.State.lastSpin.reward,Is.EqualTo(expected));Assert.That(Session.State.coins,Is.EqualTo(37+expected-40));yield return Capture("01-house");
                        yield return Tap("productSettings");
                        if(!(bool)Game.GetType().GetProperty("RewardFeedbackReduced").GetValue(Game))yield return Tap("productCards",0);
                        yield return Tap("productCards",4);long unchanged=Session.State.coins;uint rng=Session.State.seed;
                        yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));Assert.That(Session.State.coins,Is.EqualTo(unchanged));Assert.That(Session.State.seed,Is.EqualTo(rng));yield return Root();Record("explicit-fast-and-reduced");
                    }
                    if(count==1)yield return Capture("02-workshop");
                }
                if(era==0&&Session.Chapter.Progress.EraId==EraIds.Junkyard)
                {
                    long wallet=Session.State.coins;uint seed=Session.State.seed;Assert.That(Count,Is.EqualTo(12));
                    yield return Tap("productTabs",0);yield return Tap("productCards",0);yield return Tap("navigationEraButton");yield return Tap("selectButton");
                    if(Session.Chapter.TransitionPending)yield return Tap("selectButton");yield return Root();
                    Assert.That(Session.Chapter.Progress.EraId,Is.EqualTo(EraIds.WorkshopYard));Assert.That(Count,Is.EqualTo(12));Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.State.seed,Is.EqualTo(seed));Record("free-era-entry");yield return Capture("03-era-entry");
                }
            }
            Assert.That(Count,Is.EqualTo(24));Assert.That(Field<Button>("autoButton").interactable,Is.False);Record("chapter24");yield return Capture("04-chapter-complete");
            for(int skill=0;skill<6;skill++)
            {
                yield return Root();yield return Tap("productTabs",4);yield return Tap("productCards",3);yield return Tap("navigationCards",skill);long price=Price();
                if(Session.State.coins<price){yield return Earn(price);yield return Tap("productTabs",4);yield return Tap("productCards",3);yield return Tap("navigationCards",skill);Assert.That(Price(),Is.EqualTo(price));}
                long wallet=Session.State.coins;uint seed=Session.State.seed;yield return Tap("selectButton");Assert.That(Session.State.coins,Is.EqualTo(wallet-price));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(skill+1));Record("skill:"+skill);
            }
            yield return Root();yield return Tap("productTabs",2);yield return Tap("productCards",2);yield return Tap("navigationCards",0);yield return Tap("selectButton");
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo("order.sort-salvage"));int orderSpins=spins;
            while(!Session.Journey.Orders.Ready){Assert.That(spins-orderSpins,Is.LessThan(80));yield return ManualSpin();}
            long beforeClaim=Session.State.coins;uint beforeClaimSeed=Session.State.seed;
            yield return Root();yield return Tap("productTabs",2);yield return Tap("productCards",2);yield return Tap("navigationCards",0);var oldOrder=Field<Button>("selectButton").onClick;yield return Tap("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(beforeClaim+120));Assert.That(Session.State.seed,Is.EqualTo(beforeClaimSeed));long paid=Session.State.coins;oldOrder.Invoke();Assert.That(Session.State.coins,Is.EqualTo(paid));Record("order-claim120");
            for(int game=0;game<3;game++)
            {
                yield return Root();int tickets=Session.Arcade.Tickets;long diamonds=Session.Arcade.Diamonds;uint seed=Session.State.seed;
                yield return Tap("productTabs",2);yield return Tap("productCards",game==0?0:game==1?1:5);yield return Tap("arcadePrimaryButton");Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));
                int reward;
                if(game==0)
                {
                    yield return Tap("arcadeCards",0);long instance=Session.Arcade.ActiveRun.InstanceSequence;long cash=Session.State.coins;
                    yield return Root();yield return Harness("ReenterScene");yield return Root();Assert.That(Session.State.coins,Is.EqualTo(cash));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));
                    yield return Tap("productTabs",2);yield return Tap("productCards",0);Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));
                    var known=new Dictionary<int,int>();int clicks=0;
                    while(!Session.Arcade.ActiveRun.Ready)
                    {
                        Assert.That(++clicks,Is.LessThan(32));var run=Session.Arcade.ActiveRun;
                        for(int i=0;i<6;i++)if(run.MemoryFaces[i]>=0)known[i]=run.MemoryFaces[i];
                        if(run.Phase=="mismatch"){yield return Tap("arcadePrimaryButton");continue;}
                        var available=Enumerable.Range(0,6).Where(i=>(run.MatchedMask&(1<<i))==0&&run.MemoryFaces[i]<0).ToArray();int pick=-1;
                        if(run.Phase=="second")
                        {int face=Enumerable.Range(0,6).Where(i=>(run.MatchedMask&(1<<i))==0&&run.MemoryFaces[i]>=0).Select(i=>run.MemoryFaces[i]).Single();pick=available.Where(i=>known.ContainsKey(i)&&known[i]==face).DefaultIfEmpty(-1).First();}
                        else{var pair=available.Where(known.ContainsKey).GroupBy(i=>known[i]).FirstOrDefault(group=>group.Count()==2);if(pair!=null)pick=pair.First();}
                        if(pick<0)pick=available.Where(i=>!known.ContainsKey(i)).DefaultIfEmpty(available[0]).First();yield return Tap("arcadeCards",pick);
                    }
                    var solved=Session.Arcade.ActiveRun;Assert.That(solved.MatchedPairs,Is.EqualTo(3));reward=solved.Attempts<=4?3:solved.Attempts<=6?2:1;
                }
                else if(game==1){for(int i=0;i<8;i++)yield return Tap("arcadeCards",Session.Arcade.ActiveRun.CurrentSortingSymbol);Assert.That(Session.Arcade.ActiveRun.Correct,Is.EqualTo(8));reward=3;}
                else
                {
                    Assert.That(Session.Parcels.ActiveRun.BoardId,Is.EqualTo("parcels.box-priority"));int[] tokens={0,3,1,2};
                    for(int slot=0;slot<4;slot++){yield return Tap("parcelsTokenButtons",tokens[slot]);yield return Tap("parcelsSlotButtons",slot);}
                    Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.EqualTo(2));yield return Tap("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun.ReadyReward,Is.EqualTo(3));reward=3;
                }
                Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds));var oldClaim=Field<Button>("arcadePrimaryButton").onClick;yield return Tap("arcadePrimaryButton");Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds+reward));Assert.That(Session.State.seed,Is.EqualTo(seed));
                oldClaim.Invoke();Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds+reward));Record("mini-game-claimed:"+game);
            }
            yield return Root();yield return Tap("productTabs",3);yield return Tap("productCards",0);yield return ExpansionCard("module.salvage");int parts=Session.Expansion.Parts;long coins=Session.State.coins;
            yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.OwnsModule("module.salvage"),Is.True);Assert.That(Session.Expansion.ModuleAt(1),Is.Null);Assert.That(Session.Expansion.Parts,Is.EqualTo(parts-12));Assert.That(Session.State.coins,Is.EqualTo(coins));
            yield return Root();yield return Tap("productTabs",3);yield return Tap("productCards",1);yield return Tap("expansionCards",0);yield return Tap("expansionCards",0);yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.ModuleAt(1),Is.EqualTo("module.salvage"));Record("module-bought-then-installed");
            var equipment=ExpansionCatalog.Offers.Single(o=>o.Kind=="equipment"&&o.FamilyId==FamilyIds.House&&o.Level==1);
            yield return Root();yield return Tap("productTabs",4);yield return Tap("productCards",1);yield return ExpansionCard(equipment.Id);
            parts=Session.Expansion.Parts;int oldCap=Session.Expansion.PartsCap;yield return Tap("expansionPrimaryButton");
            Assert.That(Session.Expansion.EquipmentLevel(FamilyIds.House),Is.EqualTo(1));Assert.That(Session.Expansion.Parts,Is.EqualTo(parts-equipment.PartsCost));Assert.That(Session.Expansion.PartsCap,Is.GreaterThan(oldCap));Record("equipment-capacity-without-refill");
            yield return Root();yield return Tap("productTabs",3);yield return Tap("productCards",4);yield return ExpansionCard("trade.potatoes");int potatoes=Session.Expansion.Potatoes;coins=Session.State.coins;yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.Potatoes,Is.EqualTo(potatoes-20));Assert.That(Session.State.coins,Is.EqualTo(coins+20));Record("explicit-trade20");
            yield return Root();yield return Tap("productEnergy");yield return Tap("productCards",2);yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.BonusUnlocked,Is.True);
            yield return Root();yield return Tap("productEnergy");yield return Tap("productCards",0);int energy=Session.Expansion.Energy;yield return Tap("expansionPrimaryButton");Assert.That(Session.Expansion.BoostArmed,Is.True);Assert.That(Session.Expansion.Energy,Is.EqualTo(energy-EconomyRules.ExpansionBoostCost));
            energy=Session.Expansion.Energy;coins=Session.State.coins;long rolls=Session.Expansion.BonusRolls;yield return Root();yield return Harness("ReenterScene");yield return Root();Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.Expansion.Energy,Is.EqualTo(energy));Assert.That(Session.Expansion.BoostArmed,Is.True);Assert.That(Session.Expansion.ModuleAt(1),Is.EqualTo("module.salvage"));
            int bonusSpins=spins;while(Session.Expansion.BoostArmed){Assert.That(spins-bonusSpins,Is.LessThan(8));yield return ManualSpin();}
            Assert.That(Session.Expansion.BonusRolls,Is.EqualTo(rolls+1));Assert.That(Session.Expansion.LastResourceReceipt.Boosted,Is.True);Record("saved-boost-consumed-on-bonus");
            yield return Root();yield return Tap("productTabs",4);yield return Tap("productCards",0);int slotParts=Session.Expansion.Parts;long slotDiamonds=Session.Arcade.Diamonds;yield return Tap("arcadeCards",0);yield return Tap("arcadePrimaryButton");Assert.That(Session.Arcade.SlotLevel,Is.EqualTo(1));Assert.That(Session.Arcade.Diamonds,Is.EqualTo(slotDiamonds-2));Assert.That(Session.Expansion.Parts,Is.EqualTo(slotParts-20));
            yield return Root();Assert.That(Count,Is.EqualTo(24));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));Assert.That(Session.Parcels.ActiveRun,Is.Null);Assert.That(Session.Arcade.ActiveRun,Is.Null);Assert.That(Flag("AutoRunning"),Is.False);Record("complete");yield return Capture("05-final");
        }
    }
}
#endif
