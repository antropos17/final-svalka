using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;
using ProductionTiming;

internal static partial class Program
{
    static readonly string[] PayoffIds={SkillIds.Universal1,SkillIds.Universal2,SkillIds.Recycling1,SkillIds.Recycling2,SkillIds.Delivery1,SkillIds.Delivery2};
    static readonly long[] PayoffPrices={120,160,160,90,60,20};
    static int[] PayoffDraw(ref uint seed)
    {
        var result=new int[3];
        for(int i=0;i<3;i++){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;result[i]=(int)(seed>>30);}
        return result;
    }
    static string PayoffHash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static GameSession PayoffOpen(ModelState state,out Storage storage)
    {storage=new Storage{raw=Codec.Encode(state)};return GameSession.Open(new GameStore(storage,Codec),1);}
    static ModelState PayoffFundedChapter()
    {
        var state=ModelState.Fresh(1);state.coins=10000;
        var game=PayoffOpen(state,out _);long spent=0;
        foreach(string era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})
        {
            if(era==EraIds.WorkshopYard){game.EnterWorkshopYard(game.State.sequence+1,Epoch);game.AcknowledgeEraTransition(game.State.sequence+1,Epoch);}
            foreach(string family in FamilyIds.All)for(int level=1;level<=2;level++)
            {
                string id=ChapterIds.Project(era,family,level);long price=ChapterEconomy.ProjectPrice(id);spent+=price;
                long before=game.State.coins;game.SelectChapterProject(id,game.State.sequence+1,Epoch);
                Need(game.State.coins==before-price&&game.Chapter.SelectedProjectId==null,"Funded actual-Core purchase mismatch");
                if(id==GameIds.HouseRepair)game.AcknowledgeAutoHint(game.State.sequence+1,Epoch);
            }
        }
        Need(spent==9310&&game.State.coins==690&&game.State.seed==1&&game.Chapter.Skills.Receipts.Count==0,"Current B funded fixture differs");
        return game.State;
    }
    static object PayoffOrderPair(ModelState fixture,string order)
    {
        var plain=PayoffOpen(fixture,out var plainStorage);var skilled=PayoffOpen(fixture,out var skilledStorage);
        long spent=0;
        for(int i=0;i<PayoffIds.Length;i++)
        {
            var q=ChapterEconomy.QuoteSkill(skilled.State,PayoffIds[i]);
            Need(q.Status==OfferStatus.Available&&q.Node.Price==PayoffPrices[i],"Actual current skill quote differs");
            skilled.BuySkill(q.Request,Epoch);spent+=q.Node.Price;
        }
        Need(spent==610&&plain.State.coins==690&&skilled.State.coins==80,"Private bundle cost differs");
        long plainStart=plain.State.coins,skilledStart=skilled.State.coins;
        long plainInstance=plain.State.sequence+1,skilledInstance=skilled.State.sequence+1;
        plain.AcceptOrder(order,plainInstance,Epoch);skilled.AcceptOrder(order,skilledInstance,Epoch);
        uint expectedSeed=1;int spins=0,trash=0,carry=0,deliveries=0;long baseIncome=0,bonus=0;
        while(!plain.Journey.Orders.Ready)
        {
            Need(++spins<=32,"Focused postchapter order exceeded32-spin cap");
            int[] symbols=PayoffDraw(ref expectedSeed);int t=symbols.Count(x=>x==2),p=symbols.Count(x=>x==3);
            int delivered=(carry+p)/3;carry=(carry+p)%3;trash+=t;deliveries+=delivered;
            int reward=symbols[0]==symbols[1]&&symbols[1]==symbols[2]?23:symbols[0]==symbols[1]||symbols[0]==symbols[2]||symbols[1]==symbols[2]?8:3;
            long expectedPlain=reward+1+t+3*delivered+symbols.Sum(x=>new[]{6,7,3,3}[x]);
            long expectedExtra=2*(1+t+delivered);baseIncome+=expectedPlain;bonus+=expectedExtra;
            plain.ExecuteChapterSpin(plain.State.sequence+1,Epoch);skilled.ExecuteChapterSpin(skilled.State.sequence+1,Epoch);
            Need(plain.State.seed==expectedSeed&&skilled.State.seed==expectedSeed&&plain.State.lastSpin.symbols.SequenceEqual(symbols)&&skilled.State.lastSpin.symbols.SequenceEqual(symbols),"Skill changed independent RNG outcome");
            Need(plain.State.coins==plainStart+baseIncome&&skilled.State.coins==skilledStart+baseIncome+bonus,"Actual per-unit skill income differs or order auto-paid");
            int progress=order=="order.sort-salvage"?Math.Min(12,trash):Math.Min(3,deliveries);
            Need(plain.Journey.Orders.Progress==progress&&skilled.Journey.Orders.Progress==progress&&plain.Journey.Orders.Ready==skilled.Journey.Orders.Ready,"Skill changed qualification/order timing");
            Need(WaveProgress.Of(plain.State).Delivery.PigeonProgress==carry&&WaveProgress.Of(skilled.State).Delivery.PigeonProgress==carry,"Skill changed cadence/carry");
        }
        long rewardCoins=order=="order.sort-salvage"?120:180;
        bool trashOrder=order=="order.sort-salvage";
        Need(spins==(trashOrder?16:18)&&trash==(trashOrder?13:15)&&deliveries==(trashOrder?2:3)&&
            baseIncome==(trashOrder?372:420)&&bonus==(trashOrder?62:72),"Frozen seed1 independent example differs");
        long pc=plain.State.sequence+1,sc=skilled.State.sequence+1;
        plain.ClaimOrder(order,plainInstance,pc,Epoch);skilled.ClaimOrder(order,skilledInstance,sc,Epoch);
        Need(plain.State.coins==plainStart+baseIncome+rewardCoins&&skilled.State.coins==skilledStart+baseIncome+bonus+rewardCoins,"Skill multiplied or altered fixed order claim");
        int pw=plainStorage.writes,sw=skilledStorage.writes;
        plain.ClaimOrder(order,plainInstance,pc,Epoch);skilled.ClaimOrder(order,skilledInstance,sc,Epoch);
        Need(plainStorage.writes==pw&&skilledStorage.writes==sw,"Exact claim replay wrote or paid again");
        var nodeRows=PayoffIds.Select((id,i)=>new{id,price=PayoffPrices[i],qualifyingUnits=i<2?spins:i<4?trash:deliveries,
            incrementalCoins=i<2?spins:i<4?trash:deliveries,netAfterOwnNodeCost=(i<2?spins:i<4?trash:deliveries)-PayoffPrices[i]}).ToArray();
        return new{order,spins,trashAppearances=trash,completedDeliveries=deliveries,carry,fixedClaimCoins=rewardCoins,
            plainWalletBeforeClaim=plainStart+baseIncome,skilledWalletBeforeClaim=skilledStart+baseIncome+bonus,
            plainFinalWallet=plain.State.coins,skilledFinalWallet=skilled.State.coins,
            extraBundleSpinCoins=bonus,bundlePrice=spent,netBundleAfterFirstOrder=bonus-spent,
            sameCompletionSpin=true,claimUnaffected=true,eachNode=nodeRows,
            scope="One seed1 actual-Core pair; private funded chapter, no skills vs all six; no human timing/order-policy inference"};
    }
    static int SkillPayoff(string[] args)
    {
        try
        {
            Need(args.Length==6&&args[0]=="--captured"&&args[2]=="--inputs"&&args[4]=="--output","Use --skill-payoff --captured <B JSON> --inputs <source-only JSON> --output <new path>");
            string captured=Path.GetFullPath(args[1]),inputs=Path.GetFullPath(args[3]),output=Path.GetFullPath(args[5]);
            Need(!File.Exists(output)&&!output.Equals(captured,StringComparison.OrdinalIgnoreCase)&&!output.Equals(inputs,StringComparison.OrdinalIgnoreCase),"New non-input output path required");
            var timer=Stopwatch.StartNew();using var meta=JsonDocument.Parse(File.ReadAllText(inputs));
            string capturedHash=PayoffHash(captured),inputsHash=PayoffHash(inputs);
            Need(capturedHash==meta.RootElement.GetProperty("capturedBWorkingSha256").GetString(),"Frozen B capture hash differs");
            Need(SkillPricing.CurrentRevision==SkillPricing.RevisionV3&&ChapterEconomy.ProjectPrice(GameIds.HouseImprove)==480&&ChapterEconomy.ProjectPrice("cart.build")==140&&ChapterEconomy.ProjectPrice("power-source.build")==190,"Expected unchanged B/V3 production source");
            for(int i=0;i<6;i++)Need(SkillPricing.Price(PayoffIds[i],SkillPricing.CurrentRevision)==PayoffPrices[i],"No price candidate is authorized in this baseline proof");
            using var doc=JsonDocument.Parse(File.ReadAllText(captured));
            var captures=doc.RootElement.GetProperty("rows").EnumerateArray().Select(row=>JsonSerializer.Deserialize<RunResult>(row.GetProperty("actual").GetRawText())).ToArray();
            Need(captures.Length==6&&captures.All(r=>r.ConstructionSpend==9310&&r.SkillSpend==610&&r.Orders.Count==0),"Expected six captured continuous B cases");
            var windows=new List<object>();
            foreach(var run in captures)
            {
                int chapterEnd=run.Milestones["chapter-end"].Spins;
                int loft=run.Purchases.Single(p=>p.Kind=="construction"&&p.Id==GameIds.PigeonLoftBuild).Spin;
                var trash=new int[chapterEnd+1];var deliveries=new int[chapterEnd+1];uint seed=run.Seed;int carry=0;
                for(int spin=1;spin<=chapterEnd;spin++)
                {
                    int[] symbols=PayoffDraw(ref seed);trash[spin]=trash[spin-1]+symbols.Count(x=>x==2);
                    int delivered=0;if(spin>loft){int total=carry+symbols.Count(x=>x==3);delivered=total/3;carry=total%3;}
                    deliveries[spin]=deliveries[spin-1]+delivered;
                }
                foreach(var purchase in run.Purchases.Where(p=>p.Kind=="skill"))
                {
                    int index=Array.IndexOf(PayoffIds,purchase.Id);Need(index>=0&&purchase.Price==PayoffPrices[index],"Captured skill identity/price differs");
                    int at=Math.Min(purchase.Spin,chapterEnd);
                    int units=index<2?chapterEnd-at:index<4?trash[chapterEnd]-trash[at]:deliveries[chapterEnd]-deliveries[at];
                    int? recovered=null;for(int spin=at+1;spin<=chapterEnd;spin++)
                    {int n=index<2?spin-at:index<4?trash[spin]-trash[at]:deliveries[spin]-deliveries[at];if(n>=purchase.Price){recovered=spin;break;}}
                    int eventIndex=run.Purchases.IndexOf(purchase);
                    var next=run.Purchases.Skip(eventIndex+1).FirstOrDefault(p=>p.Kind=="construction");
                    var parent=SkillTree.Find(purchase.Id).RequiredSkillId;
                    windows.Add(new{run.Seed,run.Policy,id=purchase.Id,observedPurchaseSpin=purchase.Spin,price=purchase.Price,
                        affordableWalletBefore=purchase.Wallet+purchase.Price,walletAfter=purchase.Wallet,
                        chapterEndSpin=chapterEnd,remainingChapterSpins=chapterEnd-at,qualifyingUnitsAfterPurchase=units,
                        recoveredAtSpin=recovered,incrementalCoinsBeforeEnd=units,netAfterOwnNodePrice=units-purchase.Price,
                        branchPriceFromUnowned=purchase.Price+(parent==null?0:SkillPricing.Price(parent,SkillPricing.CurrentRevision)),
                        nextConstruction=next==null?null:new{id=next.Id,price=next.Price,observedCompletionSpin=next.Spin,
                            coinsShortBeforeSkill=Math.Max(0,next.Price-(purchase.Wallet+purchase.Price)),coinsShortAfterSkill=Math.Max(0,next.Price-purchase.Wallet)},
                        scope="Observed captured-policy window, acquisition spin excluded; per-node accounting is not a causal chapter-speed comparison"});
                }
            }
            var delays=new List<object>();
            foreach(uint seed in captures.Select(r=>r.Seed).Distinct())foreach(string id in new[]{"cart.build","power-source.build"})
            {
                var early=captures.Single(r=>r.Seed==seed&&r.Policy=="affordable-skills-first");
                var construction=captures.Single(r=>r.Seed==seed&&r.Policy=="construction-first");
                delays.Add(new{seed,project=id,constructionFirstSpin=construction.Purchases.Single(p=>p.Id==id).Spin,
                    skillsFirstSpin=early.Purchases.Single(p=>p.Id==id).Spin,scope="Whole-policy observed opportunity cost; not attributable to a single node"});
            }
            Need(timer.Elapsed.TotalSeconds<60,"Predeclared60-second budget before focused actual-Core examples");
            var fixture=PayoffFundedChapter();var orders=new[]{PayoffOrderPair(fixture,"order.sort-salvage"),PayoffOrderPair(fixture,"order.neighborhood-supply")};
            Need(timer.Elapsed.TotalSeconds<60,"Predeclared60-second total budget");
            Need(PayoffHash(captured)==capturedHash&&PayoffHash(inputs)==inputsHash,"Frozen inputs changed during proof");
            var result=new{schema="skill-payoff-r12/1",scope="Source-only B/V3 baseline, no candidate or connected acceptance; six captured paths reused, no chapter campaign rerun",
                capturedHash,inputsHash,currentConstructionSpend=9310,currentSkillSpend=610,currentSkillRevision=SkillPricing.CurrentRevision,
                literalUnitPayback=PayoffIds.Select((id,i)=>new{id,price=PayoffPrices[i],units=PayoffPrices[i],unit=i<2?"foreground spins":i<4?"trash appearances":"completed deliveries"}),
                windows,observedPolicyOpportunityCosts=delays,postchapterActualCore=orders,wallSeconds=timer.Elapsed.TotalSeconds,
                limits=new{naturalChapterReruns=0,privateFundedChapterFixtures=1,pairedOrderExamples=2,perPairSpinCap=32,actualPerSessionSpinCap=32,outputByteCap=524288,
                    noWeaknessInferredFromConstructionFirstZeroHorizon=true,noCausalNodeSpeedClaim=true,noHumanOrNativeClaim=true}};
            string json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});Need(System.Text.Encoding.UTF8.GetByteCount(json)<=524288,"512KiB output cap");
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json+"\n");
            Console.WriteLine("PASS36 captured skill windows +2 bounded actual-Core postchapter pairs; no price change; output="+output);return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL skill-payoff "+error);return 1;}
    }
}
