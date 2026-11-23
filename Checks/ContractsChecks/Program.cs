using System;
using JunkToNeon.Contracts;

static void Require(bool result, string message) { if (!result) throw new Exception(message); }
static void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

// Independent expected distribution over all64 unchanged three-symbol outcomes.
int totalTrash = 0, totalPigeons = 0;
for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) for (int c = 0; c < 4; c++)
{
    var outcome = new SpinOutcome(a, b, c);
    Require(outcome.Left == a && outcome.Middle == b && outcome.Right == c, "Outcome order changed");
    totalTrash += outcome.Count(SymbolIds.Trash); totalPigeons += outcome.Count(SymbolIds.Pigeon);
}
Require(totalTrash == 48 && totalPigeons == 48, "Symbol counting changed");
Reject<ArgumentOutOfRangeException>(() => new SpinOutcome(0, 1, 4));
Reject<ArgumentOutOfRangeException>(() => new SpinOutcome(-1, 0, 0));
Reject<ArgumentOutOfRangeException>(() => new SpinOutcome(0, 0, 0).Count(4));
Console.WriteLine("PASS all64 outcomes preserve order and independently expected symbol counts");

Require(CoinEffects.Total(new[] { new EffectContribution(GameIds.RecyclerTrashCoins, 3), new EffectContribution(GameIds.PigeonDeliveryCoins, 6) }) == 9, "Additive effects changed");
Require(CoinEffects.Total(Array.Empty<EffectContribution>()) == 0, "Empty effects grant coins");
Reject<OverflowException>(() => CoinEffects.Total(new[] { new EffectContribution("a", long.MaxValue), new EffectContribution("b", 1) }));
Reject<ArgumentOutOfRangeException>(() => new EffectContribution("a", -1));
Console.WriteLine("PASS additive totals reject negative amounts and overflow");

Reject<ArgumentException>(() => CoinEffects.Total(new[] { new EffectContribution("same", 1), new EffectContribution("same", 2) }));
Reject<ArgumentException>(() => CoinEffects.Total(new EffectContribution[] { null }));
Reject<ArgumentNullException>(() => CoinEffects.Total(null));
Reject<ArgumentException>(() => CoinEffects.Total(new EffectContribution[17]));
foreach (var invalid in new[] { "", "0bad", "Coin", "bad name", "x/y", new string('a', 65), "тест" })
    Reject<ArgumentException>(() => new EffectContribution(invalid, 0));
Console.WriteLine("PASS duplicate, unknown-shape and unbounded contribution inputs fail closed");
var projectIds=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
foreach(var era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})foreach(var family in FamilyIds.All)for(int level=1;level<=2;level++)
{
    string id=ChapterIds.Project(era,family,level);
    Require(projectIds.Add(id)&&StableId.IsValid(id)&&ChapterIds.IsProject(id,era),"Colliding/invalid chapter project ID");
}
Require(projectIds.Count==24&&ChapterIds.Project(EraIds.Junkyard,FamilyIds.House,1)==GameIds.HouseRepair&&
    ChapterIds.Stage(EraIds.WorkshopYard,FamilyIds.Recycler,2)=="recycler.workshop-yard.stage.2"&&SkillIds.Universal1==GameIds.SlotCoinBoost,"Legacy/chapter identity drift");
var input=new System.Collections.Generic.List<FamilyStage>();foreach(var family in FamilyIds.All)input.Add(new FamilyStage(family,3));
var stageSnapshot=new FamilyStages(EraIds.WorkshopYard,input);input.Clear();
Require(stageSnapshot.Entries.Count==6&&stageSnapshot.AbsoluteLevel(FamilyIds.House)==3&&stageSnapshot.LocalLevel(FamilyIds.House)==0,"Stage snapshot changed or era entry reset lifetime level");
Reject<ArgumentException>(()=>new FamilyStages(EraIds.Junkyard,stageSnapshot.Entries));
Reject<ArgumentException>(()=>new FamilyStages(EraIds.Junkyard,Array.Empty<FamilyStage>()));
Reject<ArgumentException>(()=>new FamilyStage("unknown",0));
Console.WriteLine("PASS 24 unique era projects, preserved legacy IDs and immutable six-family era stage bounds");

var sources=new System.Collections.Generic.List<SymbolCoinSource>();for(int i=0;i<19;i++)sources.Add(new SymbolCoinSource("source"+i+".symbol-coins",i%4,1));
var modifiers=new ChapterSymbolModifiers(sources);sources.Clear();
Require(modifiers.Sources.Count==19&&modifiers.CoinsPerSymbol(0)==5&&modifiers.CoinsPerSymbol(1)==5&&modifiers.CoinsPerSymbol(2)==5&&modifiers.CoinsPerSymbol(3)==4,"Nineteen sources do not aggregate to independent four-symbol totals");
Reject<ArgumentException>(()=>new ChapterSymbolModifiers(new[]{new SymbolCoinSource("same",0,1),new SymbolCoinSource("same",1,1)}));
Reject<ArgumentException>(()=>new SymbolCoinSource("bad",4,1));
Reject<ArgumentException>(()=>new SymbolCoinSource("bad",0,0));
var oversized=new System.Collections.Generic.List<SymbolCoinSource>();for(int i=0;i<33;i++)oversized.Add(new SymbolCoinSource("id"+i,0,1));
Reject<ArgumentException>(()=>new ChapterSymbolModifiers(oversized));
Require(9<=CoinEffects.MaximumContributions,"New aggregate categories exceed the retained contribution bound");
Console.WriteLine("PASS 19 immutable sources aggregate into four checked rates; duplicate/invalid/33-source inputs reject");
Console.WriteLine("PASS 5 contract regression groups");
