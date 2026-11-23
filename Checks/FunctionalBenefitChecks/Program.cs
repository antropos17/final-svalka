// Pin:1b15ed91078480a9bdcdf3883c99d90d6cb42327. Run on X with TEMP/TMP,
// BaseIntermediateOutputPath and OutputPath under .work/functional-benefits.
// Executes unchanged extracted copy methods against production pure domains.
// Text/control stand-ins capture values only: no Canvas, Unity or input acceptance.
// Original pin:178/197 passed,19 failures: generic "+1 за символ" completion
// omitted the required symbol on all19 symbol-coin upgrades. Candidate changes
// only that condition's noun; production primary is owned by02.
using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace FunctionalBenefitChecks
{
    sealed class TMP_Text { public string text; }
    struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
    static class Mathf { public static float Min(float a,float b)=>Math.Min(a,b); }
    sealed class RectTransform { public Vector2 anchorMin,anchorMax; }
    sealed class Control
    {
        public readonly TMP_Text Label=new TMP_Text(); public readonly RectTransform Rect=new RectTransform();
        public bool activeSelf; public bool activeInHierarchy=>activeSelf; public Control gameObject=>this;
        public void SetActive(bool value){activeSelf=value;}
        public T GetComponentInChildren<T>() where T:class=>Label as T;
        public T GetComponent<T>() where T:class=>Rect as T;
    }
    sealed class JourneyFixture { public OrderState Orders=OrderState.Empty; }
    sealed class SessionFixture
    {
        public ModelState State; public ChapterState Chapter=>ChapterEconomy.Project(State);
        public readonly JourneyFixture Journey=new JourneyFixture();
    }
    sealed partial class CopyProbe
    {
        const int ChapterFamily=4,ChapterSkill=5,JourneyOrders=20,JourneyOrder=21;
        public readonly SessionFixture Session=new SessionFixture();
        readonly Control panel=new Control(),selectButton=new Control(),reloadButton=new Control(),closeButton=new Control(),chapterPrevious=new Control();
        readonly TMP_Text panelBody=new TMP_Text();
        readonly Vector2 orderSelectMin=new Vector2(0,0),orderSelectMax=new Vector2(1,1);
        string chapterFamily,chapterProject,shownOrder; long shownOrderInstance,journeyQuoteSequence; SkillQuote chapterQuote;
        public string Body=>panelBody.text; public string Action=>selectButton.Label.text; public bool ActionVisible=>selectButton.activeSelf;
        public string[] Choices;
        bool CanOpenChapterPanel()=>true;
        bool ShowChapterTransition()=>false;
        void BeginChapterPanel(int mode,string title){panelBody.text="";selectButton.SetActive(false);}
        void ObservePreviewRequest(){}
        static void FitChapterLabel(Control control){}
        void ChoiceLabels(params string[] values){Choices=values;}
        public static string Description(UpgradeDefinition value)=>Benefit(value);
        public static string Completion(string id)=>InlineCompletion(id);
        public static long Price(string id)=>ProjectPrice(id);
        public void Skill(string id)=>ShowChapterSkill(id);
        public void Order(string id)=>ShowOrder(id);
        public void OrderMenu()=>ShowOrders();
    }
    static class Program
    {
        static readonly List<string> failures=new List<string>(); static int cases;
        static void Check(bool value,string name){cases++;if(!value)failures.Add(name);}
        static void Contains(string value,string expected,string name)=>Check(value.Contains(expected,StringComparison.Ordinal),name+"; expected: "+expected+"; actual: "+value);
        static ModelState Apply(ModelState state,string kind,string id=null,SkillPurchaseRequest request=null)
            =>ChapterEconomy.ApplyPending(ChapterEconomy.Reserve(state,kind,id,state.sequence+1,0,request));
        static ModelState Fresh(){var s=ModelState.Fresh(1);s.coins=100000;return s;}
        static void Buildings()
        {
            // Independent reviewed table: rows house/workshop/recycler/loft/cart/power,
            // columns build/improve/era2-upgrade1/era2-upgrade2. Current new-selection V2 offer; historical catalog remains V1.
            long[,] prices={{40,540,420,680},{120,260,380,620},{180,320,400,640},{240,360,440,660},{160,300,360,580},{200,340,460,700}};
            string[] units={"картошку","коробку","мусор","голубя","коробку","картошку"};
            int[] symbol={0,1,2,3,1,0}; int count=0;
            foreach(string era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})
            for(int f=0;f<6;f++)for(int level=1;level<=2;level++)
            {
                string id=ChapterIds.Project(era,FamilyIds.All[f],level);ChapterCatalog.Rules.TryGetUpgrade(id,out var u);count++;
                int column=(era==EraIds.Junkyard?0:2)+level-1;
                Check(u!=null&&u.Price==(f==0&&column==1?600:prices[f,column])&&CopyProbe.Price(id)==prices[f,column],id+" current price");
                bool special=era==EraIds.Junkyard&&(f==0||level==1&&f<4);
                if(!special)
                {
                    Check(u.Effects.Count==1&&u.Effects[0].Kind==ContentEffectKind.SymbolCoins&&u.Effects[0].SymbolId==symbol[f],id+" registered unit");
                    Contains(u.BenefitSummary,"+1 coin per "+new[]{"potato","box","trash","pigeon"}[symbol[f]],id+" registered description");
                    Contains(CopyProbe.Description(u),"+1 за "+units[f]+" на следующих спинах",id+" preview unit/acquisition");
                    Contains(CopyProbe.Completion(id),"+1 за "+units[f],id+" completion must name qualifying symbol");
                }
                else if(f==0&&level==2)
                {
                    Check(u.Effects.Count==0,id+" milestone has no income");
                    Check(!CopyProbe.Description(u).Contains("+1")&&!CopyProbe.Completion(id).Contains("+1"),id+" milestone copy has no income");
                }
                else Contains(CopyProbe.Description(u),f==0?"АВТО":f==1?"следующие спины":f==2?"мусор на следующих спинах":"три появления голубя; перенос 0–2",id+" baseline unit");
            }
            Check(count==24,"all24 buildings checked");
        }
        static ModelState CompleteChapter()
        {
            var s=Fresh();
            foreach(string era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})
            {
                if(era==EraIds.WorkshopYard){s=Apply(s,"enter-era");s=Apply(s,"era-seen");}
                for(int level=1;level<=2;level++)foreach(string family in FamilyIds.All)s=Apply(s,"select",ChapterIds.Project(era,family,level));
            }
            return s;
        }
        static void Skills()
        {
            var s=CompleteChapter();var probe=new CopyProbe();
            long[] price={120,160,160,90,60,20};string[] units={"за спин","за спин","за мусор","за мусор","за доставку (3 голубя)","за доставку (3 голубя)"};
            for(int i=0;i<6;i++)
            {
                var node=SkillTree.Nodes[i];probe.Session.State=s;probe.Skill(node.Id);
                Check(node.Price==price[i],node.Id+" V3 price");Contains(probe.Body,"Цена: "+price[i],node.Id+" price copy");
                Contains(probe.Body,"+"+(i%2)+" → +"+(i%2+1)+" "+units[i],node.Id+" independent effect unit");
                Check(node.RequiredProjectLevel==(i%2==0?1:4)&&node.RequiredFamilyId==FamilyIds.All[i/2==0?0:i/2==1?2:3],node.Id+" required family/stage");
                Contains(probe.Body,"Шансы не меняются",node.Id+" unchanged odds");Check(probe.ActionVisible,node.Id+" available quote");
                var q=ChapterEconomy.QuoteSkill(s,node.Id);s=Apply(s,"skill",node.Id,q.Request);
                probe.Session.State=s;probe.Skill(node.Id);Contains(probe.Body,"уже куплен",node.Id+" owned quote");Check(!probe.ActionVisible,node.Id+" no second purchase button");
            }
            // Locked tier1 and tier2 copy is generated from the actual quote status.
            probe.Session.State=Fresh();foreach(var node in SkillTree.Nodes){probe.Skill(node.Id);Check(!probe.ActionVisible,node.Id+" locked button");Contains(probe.Body,node.Tier==1?"Нужно построить":"Нужны I уровень",node.Id+" prerequisite copy");}
        }
        static void Orders()
        {
            var probe=new CopyProbe();probe.Session.State=CompleteChapter();probe.OrderMenu();
            var context=new OrderContext(new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select(id=>new FamilyStage(id,5))));
            foreach(var def in OrderCatalog.Choices)
            {
                bool trash=def.Id==OrderIds.TrashSort;long reward=trash?120:180;int target=trash?12:3;
                Check(def.Target==target&&def.RewardCoins==reward&&def.Metric==(trash?OrderMetric.TrashAppearances:OrderMetric.CompletedDeliveries),def.Id+" independent target/reward");
                Contains(probe.Choices[trash?0:1],"+"+reward,def.Id+" menu reward");
                probe.Session.Journey.Orders=OrderState.Empty;probe.Order(def.Id);
                Contains(probe.Body,trash?"12 появлений мусора":"3 завершённые доставки по 3 голубя",def.Id+" target units");Check(probe.Action=="Взять заказ"&&probe.ActionVisible,def.Id+" accept");
                var state=OrderEngine.PrepareAccept(context,OrderState.Empty,OrderEngine.QuoteAccept(context,OrderState.Empty,def.Id),10,7).After;
                for(int n=1;n<=(trash?4:3);n++)
                {
                    probe.Session.Journey.Orders=state;probe.Order(def.Id);Check(!probe.ActionVisible,def.Id+" not-ready cannot claim");
                    var ev=new OrderEvent(10+n,state.Cursor,trash?new SpinOutcome(2,2,2):new SpinOutcome(3,3,3),trash?0:1);
                    state=OrderEngine.PrepareObserve(context,state,ev,10+n,7).After;
                }
                probe.Session.Journey.Orders=state;probe.Order(def.Id);Contains(probe.Body,"Награда готова",def.Id+" ready copy");Check(probe.Action=="Получить награду"&&probe.ActionVisible,def.Id+" ready claim button");
                var claim=OrderEngine.PrepareClaim(context,state,def.Id,10,20,7);var applied=OrderEngine.Apply(context,state,7,claim);
                Check(applied.Coins==7+reward&&!applied.State.Ready,def.Id+" claim exact amount");
                var replay=OrderEngine.Apply(context,applied.State,applied.Coins,claim);Check(!replay.Applied&&replay.Coins==applied.Coins,def.Id+" retained claim replay no payout");
                probe.Session.Journey.Orders=replay.State;probe.Order(def.Id);Check(probe.Action=="Взять заказ"&&probe.ActionVisible&&!probe.Body.Contains("Награда готова"),def.Id+" claimed/replay copy offers new instance");
            }
        }
        static void SelectionAndAcquisition()
        {
            var s=Fresh();var probe=new CopyProbe();probe.Session.State=s;probe.ShowChapterFamily(FamilyIds.House);
            Contains(probe.Body,"в том числе сразу", "affordable preview promises immediate purchase");
            s=Apply(s,"select",GameIds.HouseRepair);Check(s.houseStage==1&&ChapterEconomy.Project(s).SelectedProjectId==null,"affordable select purchases, no outstanding selection");
            Contains(CopyProbe.Completion(GameIds.HouseRepair),"Готово:","immediate selection completion copy");
            s.coins=119;s=Apply(s,"select",GameIds.WorkshopBuild);s=Apply(s,"spin");
            Check(s.workshopBuilt==1&&s.lastSpin.bonus==0,"acquisition spin gets no retroactive workshop effect");
            s=Apply(s,"spin");Check(s.lastSpin.bonus==1,"subsequent spin effect matches preview");
            s.coins=200;s=Apply(s,"select",GameIds.HouseImprove);probe.Session.State=s;
            probe.Skill(SkillIds.Universal1);Contains(probe.Body,"200/540", "current selected540 target in skill preview");
            Check(ChapterEconomy.Project(s).SelectedProjectId==GameIds.HouseImprove,"skill preview preserves outstanding target");
            var c=ChapterEconomy.Project(s);s.chapter=new ChapterState(ChapterState.FormatVersion,c.RuleRevision,c.Progress,c.Skills,c.SelectedProjectId,c.EntrySequence,c.TransitionSeen);probe.Session.State=s;
            probe.Skill(SkillIds.Universal1);Contains(probe.Body,"200/600","legacy selected600 remains pinned in skill preview");
            s.coins=550;probe.ShowChapterFamily(FamilyIds.House);
            Contains(probe.Body,"Цена: 540", "new offer is540 beside pinned600 target");
            Check(probe.ActionVisible&&probe.Action=="Перевыбрать за 540","legacy underfunded selection exposes explicit540 reselection");
        }
        static int Main()
        {
            Buildings();Skills();Orders();SelectionAndAcquisition();
            foreach(var failure in failures)Console.Error.WriteLine("FAIL "+failure);
            Console.WriteLine($"{cases-failures.Count}/{cases} pure functional-copy assertions passed; {failures.Count} failed. No Unity/UI acceptance.");
            return failures.Count==0?0:1;
        }
    }
}
