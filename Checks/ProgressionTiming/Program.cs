using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;
using ProductionTiming;

internal static partial class Program
{
    const long Epoch=1700000000;
    const int MaximumSpins=2000;
    static readonly uint[] Seeds={1,12305,0x12345678,0x9e3779b9,0xa341316c,0xc8013ea4,0xad90777d,0x7e95761e,0xdeadbeef,0x6d2b79f5,0xf00dcafe,0x13579bdf,0x2468ace1,0x85ebca6b,0xc2b2ae35,0x27d4eb2f};
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string Profile="current-v3";
    static long ExpectedSkillSpend=610;
#if R12_CADENCE
    static long CadenceSpend;
#endif
    static long ExpectedConstructionSpend =>
#if R12_CADENCE
        CadenceSpend;
#elif R11_EQUIVALENCE
        9400;
#else
        9460;
#endif
    static long OfferPrice(UpgradeDefinition upgrade)
    {
#if R11_EQUIVALENCE
        return ChapterEconomy.ProjectPrice(upgrade.Id);
#else
        return upgrade.Price.Value;
#endif
    }
    sealed class Storage:ISnapshotStorage
    {
        public string raw;public int writes;public long characters;
        public string ReadPrimary()=>raw;public string ReadBackup()=>null;
        public void Write(string value){raw=value;writes++;characters+=value.Length;}
    }
    sealed class Simulation
    {
        public readonly RunResult Result;
        public readonly Storage Storage=new Storage();
        public GameSession Game;
        readonly bool earlySkills,offline;
        long quarterSeconds;int decisions,lastConstructionSpin;double lastConstructionSeconds;
        bool returned;
        public Simulation(uint seed,bool earlySkills,bool offline)
        {
            this.earlySkills=earlySkills;this.offline=offline;
            Result=new RunResult{Seed=seed,Policy=earlySkills?"affordable-skills-first":"construction-first",Scenario=offline?"one-2h-return-after-workshop":"continuous",FreshGrant=37};
            Game=GameSession.Open(new GameStore(Storage,Codec),seed,initializeJourney:true);Need(!Game.RecoveryRequired,Game.Error);Need(Game.State.coins==37,"Fresh grant differs");Game.Checkpoint(Now);
        }
        long Now=>Epoch+Result.AbsenceSeconds+quarterSeconds/4;
        void CheckWallet(){Need(Game.State.coins==37+Result.SpinIncome+Result.OfflineIncome-Result.ConstructionSpend-Result.SkillSpend,"Independent wallet accounting failed");Need(!Game.RecoveryRequired,Game.Error);}
        void Stamp(string name)
        {if(!Result.Milestones.ContainsKey(name))Result.Milestones.Add(name,new Milestone{Spins=Result.Spins,ManualSeconds=Result.Spins*3.5,NominalAutoSeconds=quarterSeconds/4.0,ExplicitDecisions=decisions,AutoPlusTwoSecondsPerDecision=quarterSeconds/4.0+2*decisions,Wallet=Game.State.coins});}
        void Construction(string id,long price)
        {
            Result.ConstructionSpend+=price;Result.ConstructionCount++;
            int gap=Result.Spins-lastConstructionSpin;double seconds=quarterSeconds/4.0-lastConstructionSeconds;
            Result.LongestConstructionGapSpins=Math.Max(Result.LongestConstructionGapSpins,gap);Result.LongestConstructionGapNominalAutoSeconds=Math.Max(Result.LongestConstructionGapNominalAutoSeconds,seconds);
            lastConstructionSpin=Result.Spins;lastConstructionSeconds=quarterSeconds/4.0;
            Result.Purchases.Add(new Event{Kind="construction",Id=id,Price=price,Spin=Result.Spins,Wallet=Game.State.coins,NominalAutoSeconds=quarterSeconds/4.0});
            if(id==GameIds.HouseRepair){Stamp("first-repair");if(Game.State.autoHintSeen==0){Game.AcknowledgeAutoHint(Game.State.sequence+1,Now);decisions++;}}
            if(id==GameIds.WorkshopBuild)
            {
                Stamp("workshop");
                if(offline&&!returned)
                {
                    returned=true;long coins=Game.State.coins;uint seed=Game.State.seed;int carry=WaveProgress.Of(Game.State).Delivery.PigeonProgress;
                    Game.Checkpoint(Now);Result.AbsenceSeconds+=7200;Game.SettleOffline(Now);
                    Need(Game.State.coins-coins==120&&Game.State.seed==seed&&WaveProgress.Of(Game.State).Delivery.PigeonProgress==carry,"2h return changed non-income state");
                    Result.OfflineIncome+=120;Game.AcknowledgeOffline(Game.State.sequence+1,Now);decisions++;Stamp("after-offline-return");
                }
            }
        }
        bool BuySkillIfPossible()
        {
            var quotes=SkillTree.Nodes.Select(n=>ChapterEconomy.QuoteSkill(Game.State,n.Id)).Where(q=>q.Status==OfferStatus.Available).OrderBy(q=>q.Node.Price).ThenBy(q=>q.Node.Id,StringComparer.Ordinal).ToArray();
            if(quotes.Length==0)return false;var quote=quotes[0];long before=Game.State.coins;string selected=Game.Chapter.SelectedProjectId;
            Game.BuySkill(quote.Request,Now);decisions++;
            Need(Game.State.coins==before-quote.Node.Price&&Game.Chapter.SelectedProjectId==selected,"Skill bought selected construction or wrong price");
            Result.SkillSpend+=quote.Node.Price;Result.Purchases.Add(new Event{Kind="skill",Id=quote.Node.Id,Price=quote.Node.Price,Spin=Result.Spins,Wallet=Game.State.coins,NominalAutoSeconds=quarterSeconds/4.0});CheckWallet();return true;
        }
        void Spin()
        {
            if(Result.Spins>=MaximumSpins)throw new InvalidOperationException("Predeclared2000-spin cap: "+Result.Policy+"/"+Result.Seed);
            bool unlocked=Game.State.houseStage>=1&&!ChapterCatalog.Rules.IsChapterComplete(Game.Chapter.Progress);quarterSeconds+=unlocked?15:14;Result.Spins++;
            var before=Game.State;string selected=Game.Chapter.SelectedProjectId;
            Game.ExecuteChapterSpin(before.sequence+1,Now,manualSpin:!unlocked);
            var s=Game.State;var receipt=s.lastSpin;long income=receipt.reward+receipt.bonus+receipt.chapter.Effects.TotalCoins;
            Result.SpinIncome+=income;
            if(selected!=null&&Game.Chapter.SelectedProjectId==null)
            {Need(ChapterCatalog.Rules.TryGetUpgrade(selected,out var completed),"Missing selected project");
#if R11_EQUIVALENCE
                Need(receipt.chapter.Price==ChapterEconomy.SelectedProjectPrice(before.chapter),"Pinned construction debit differs from active target");Construction(selected,receipt.chapter.Price);
#else
                Construction(selected,completed.Price.Value);
#endif
            }
            else Need(Game.State.coins==before.coins+income,"Unexpected spin debit/grant");
            CheckWallet();Result.PeakManagedBytes=Math.Max(Result.PeakManagedBytes,GC.GetTotalMemory(false));
        }
        public RunResult Run(int orderRepeats)
        {
            var timer=Stopwatch.StartNew();Stamp("fresh");
            while(!ChapterCatalog.Rules.IsChapterComplete(Game.Chapter.Progress))
            {
                if(Game.Chapter.TransitionPending){Game.AcknowledgeEraTransition(Game.State.sequence+1,Now);decisions++;continue;}
                if(ChapterCatalog.Rules.CanUnlockWorkshopYard(Game.Chapter.Progress))
                {Stamp("era-ready");Game.EnterWorkshopYard(Game.State.sequence+1,Now);decisions++;Stamp("era-entered");continue;}
                if(earlySkills&&BuySkillIfPossible())continue;
                if(Game.Chapter.SelectedProjectId==null)
                {
                    var available=ChapterCatalog.Rules.AvailableUpgrades(Game.Chapter.Progress).Select(id=>{Need(ChapterCatalog.Rules.TryGetUpgrade(id,out var u),"Unknown available project");return u;}).OrderBy(OfferPrice).ThenBy(u=>u.Id,StringComparer.Ordinal).First();
                    long coins=Game.State.coins;Game.SelectChapterProject(available.Id,Game.State.sequence+1,Now);decisions++;
                    if(Game.Chapter.SelectedProjectId==null){Need(Game.State.coins==coins-OfferPrice(available),"Immediate project wrong price");Construction(available.Id,OfferPrice(available));CheckWallet();continue;}
                }
                Spin();
            }
            Stamp("chapter-end");Result.SkillsOwnedAtChapterEnd=Game.Chapter.Skills.Receipts.Count;Result.SkillSpendAtChapterEnd=Result.SkillSpend;
            while(Game.Chapter.Skills.Receipts.Count<6){if(!BuySkillIfPossible())Spin();}
            Stamp("all-skills-owned");Result.WalletAfterSkills=Game.State.coins;
            Need(Result.ConstructionCount==24&&Result.ConstructionSpend==ExpectedConstructionSpend&&Result.SkillSpend==ExpectedSkillSpend,"Production totals/ownership do not match declared profile data");CheckWallet();
            string end=Codec.Encode(Game.State);foreach(string id in new[]{"order.sort-salvage","order.neighborhood-supply"})Orders(end,id,orderRepeats);
            Result.Writes=Storage.writes;Result.SerializedCharacters=Storage.characters;Result.Status="complete";Result.RunnerWallSeconds=timer.Elapsed.TotalSeconds;return Result;
        }
        void Orders(string end,string id,int repeats)
        {
            var storage=new Storage{raw=end};var game=GameSession.Open(new GameStore(storage,Codec),Result.Seed);
            for(int repeat=1;repeat<=repeats;repeat++)
            {
                long start=game.State.coins;int carryBefore=WaveProgress.Of(game.State).Delivery.PigeonProgress;int spins=0;long spinIncome=0;
                long instance=game.State.sequence+1;game.AcceptOrder(id,instance,Now);
                Need(game.State.coins==start&&game.Journey.Orders.Progress==0,"Order fee/retroactive progress");
                while(!game.Journey.Orders.Ready)
                {
                    if(++spins>100)throw new InvalidOperationException("Predeclared100-spin order cap");long coins=game.State.coins;
                    game.ExecuteChapterSpin(game.State.sequence+1,Now,manualSpin:true);var r=game.State.lastSpin;long income=r.reward+r.bonus+r.chapter.Effects.TotalCoins;spinIncome+=income;
                    Need(game.State.coins==coins+income,"Order auto-credited before claim");
                }
                long reward=id=="order.sort-salvage"?120:180;long claim=game.State.sequence+1;game.ClaimOrder(id,instance,claim,Now);
                Need(game.State.coins==start+spinIncome+reward,"Order independent accounting failed");int writes=storage.writes;game.ClaimOrder(id,instance,claim,Now);Need(storage.writes==writes,"Order exact replay paid again");
                Result.Orders.Add(new OrderResult{Id=id,Instance=repeat,Spins=spins,NominalManualSeconds=spins*3.5,SpinIncome=spinIncome,ClaimIncome=reward,CarryBefore=carryBefore,CarryAfter=WaveProgress.Of(game.State).Delivery.PigeonProgress});
            }
        }
    }
    static void Need(bool condition,string why){if(!condition)throw new Exception(why);}
    static object Checks()
    {
        var histogram=new Dictionary<int,int>();long total=0;for(int a=0;a<4;a++)for(int b=0;b<4;b++)for(int c=0;c<4;c++){int r=GameLogic.Payout(new[]{a,b,c});total+=r;if(!histogram.ContainsKey(r))histogram[r]=0;histogram[r]++;}
        Need(histogram[23]==4&&histogram[8]==36&&histogram[3]==24&&total==452,"Independent64-outcome baseline");
        var storage=new Storage();var game=GameSession.Open(new GameStore(storage,Codec),1,initializeJourney:true);Need(game.State.coins==37&&storage.writes==1,"Fresh37 once");game=GameSession.Open(new GameStore(storage,Codec),1,initializeJourney:true);Need(game.State.coins==37&&storage.writes==1,"Reload repeated grant");
        game.Checkpoint(Epoch);game.SelectChapterProject(GameIds.HouseRepair,game.State.sequence+1,Epoch);game.ExecuteChapterSpin(game.State.sequence+1,Epoch+3);
        Need(game.State.coins==5&&game.State.houseStage==1&&game.State.lastSpin.reward==8&&game.State.lastSpin.bonus==0,"Seed1 independent first repair37+8-40=5");
        var fixture=game.State;fixture.coins=120;game=GameSession.Open(new GameStore(new Storage{raw=Codec.Encode(fixture)},Codec),1);game.SelectChapterProject(GameIds.WorkshopBuild,game.State.sequence+1,Epoch+3);
        game.Checkpoint(Epoch+3);long coins=game.State.coins;game.SettleOffline(Epoch+36003);Need(game.State.coins==coins+480,"Actual cap8h=480");long once=game.State.coins;game.SettleOffline(Epoch+36003);game.SettleOffline(Epoch+30003);Need(game.State.coins==once,"Offline replay/rollback");
        return new{outcomes=64,totalBaseCoins=452,meanBaseCoins=452.0/64,nominalManualCoinsPerMinute=(452.0/64)*60/3.5,nominalAutoCoinsPerMinute=(452.0/64)*60/3.75,firstRepairSeed1Wallet=5,actualOfflineCapProbe=480};
    }
    static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--skill-payoff")return SkillPayoff(args.Skip(1).ToArray());
#if R12_CADENCE
        return Cadence(args);
#elif R11_EQUIVALENCE
        return Equivalence(args);
#else
        try
        {
            int samples=16,offlineSamples=4,repeats=3;string output=null,policy="both";
            for(int i=0;i<args.Length;i++){switch(args[i]){case "--samples":samples=int.Parse(args[++i]);break;case "--offline-samples":offlineSamples=int.Parse(args[++i]);break;case "--order-repeats":repeats=int.Parse(args[++i]);break;case "--output":output=Path.GetFullPath(args[++i]);break;case "--profile":Profile=args[++i];break;case "--policy":policy=args[++i];break;default:throw new ArgumentException("Unknown argument");}}
            Need(samples>=1&&samples<=Seeds.Length&&offlineSamples>=0&&offlineSamples<=samples&&repeats>=1&&repeats<=5&&output!=null,"Bounded arguments/output required");
            Need(Profile=="current-v3"||Profile=="candidate-a","Unknown profile");Need(policy=="both"||policy=="affordable-skills-first","Unknown policy subset");
            ExpectedSkillSpend=Profile=="candidate-a"?500:610;
            Need(SkillTree.Nodes.Sum(n=>n.Price)==ExpectedSkillSpend&&SkillPricing.CurrentRevision==(Profile=="candidate-a"?"skill.prices.v4":"skill.prices.v3"),"Compiled source does not match explicit profile label");
            var checks=Checks();var runs=new List<RunResult>();var timer=Stopwatch.StartNew();
            foreach(bool offline in new[]{false,true})foreach(bool early in new[]{false,true})foreach(uint seed in Seeds.Take(offline?offlineSamples:samples))
            {
                if(policy=="affordable-skills-first"&&!early)continue;
                if(timer.Elapsed.TotalSeconds>600)throw new InvalidOperationException("Predeclared600-second total cap");
                var r=new Simulation(seed,early,offline).Run(repeats);runs.Add(r);Console.WriteLine("DONE "+r.Policy+" "+r.Scenario+" seed="+seed+" chapterSpins="+r.Milestones["chapter-end"].Spins+" allSkills="+r.Spins+" cpuWall="+r.RunnerWallSeconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));GC.Collect();
            }
            var summaries=runs.GroupBy(r=>new{r.Policy,r.Scenario}).Select(g=>new{g.Key.Policy,g.Key.Scenario,samples=g.Count(),chapterSpins=Distribution.Of(g.Select(r=>(double)r.Milestones["chapter-end"].Spins)),chapterNominalAutoSeconds=Distribution.Of(g.Select(r=>r.Milestones["chapter-end"].NominalAutoSeconds)),longestGapSeconds=Distribution.Of(g.Select(r=>r.LongestConstructionGapNominalAutoSeconds)),allSkillSpins=Distribution.Of(g.Select(r=>(double)r.Spins)),skillsAtChapterEnd=Distribution.Of(g.Select(r=>(double)r.SkillsOwnedAtChapterEnd))}).ToArray();
            var data=new{schema=2,profile=Profile,productionSource=Profile=="candidate-a"?"c086395 Core with isolated proposal-only V4 price resolver; NOT production/release qualification":"c086395 (r9); source manifest recorded alongside result",seeds=Seeds.Take(samples),offlineSeeds=Seeds.Take(offlineSamples),checks,assumptions=new{nominalManualSeconds=3.5,nominalAutoSeconds=3.75,autoEligible="only after first repair and before chapter end; postchapter skill gathering and orders use manual",extraDecisionSeconds=2,zeroTimeDecisions=true,firstRepairIsManual=true,offlineAbsenceSeconds=7200,perRunSpinCap=MaximumSpins,perOrderSpinCap=100,totalSecondsCap=600,quantile="nearest rank; p95 at n16 is sample maximum, not a population estimate",storage="real GameStore/strict Codec in memory; no filesystem/Unity execution"},runnerWallSeconds=timer.Elapsed.TotalSeconds,summaries,runs};
            string json=JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true});Need(json.Length<2*1024*1024,"Bounded2MiB output limit");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json+"\n");Console.WriteLine("PASS "+runs.Count+" completed runs;24 projects9460+6 skills"+ExpectedSkillSpend+" each; output="+output);return 0;
        }
        catch(Exception e){Console.Error.WriteLine("FAIL "+e);return 1;}
#endif
    }

#if R11_EQUIVALENCE
    static Dictionary<string,int> GapSpins(RunResult r)
    {
        var result=new Dictionary<string,int>();int last=0;
        foreach(var p in r.Purchases.Where(p=>p.Kind=="construction")){result.Add(p.Id,p.Spin-last);last=p.Spin;}return result;
    }
    static long SkillSpendBefore(RunResult r,string project)
    {long spend=0;foreach(var p in r.Purchases){if(p.Kind=="construction"&&p.Id==project)return spend;if(p.Kind=="skill")spend+=p.Price;}throw new Exception("Missing project");}
#if R12_CADENCE
    static int Cadence(string[] args)
    {
        try
        {
            Need(args.Length==6&&args[0]=="--candidate"&&args[2]=="--qualified"&&args[4]=="--output","Use --candidate small|wider --qualified <house540 input> --output <new path>");
            string candidate=args[1],input=Path.GetFullPath(args[3]),output=Path.GetFullPath(args[5]);
            Need(candidate=="small"||candidate=="wider","Only two predeclared profiles");
            CadenceSpend=candidate=="small"?9340:9310;
            Need(!File.Exists(output)&&new FileInfo(input).Length<2*1024*1024,"Preserve receipts and bound input");
            string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input)));
            Need(hash=="5AB11FDB1201835F39BB7BCF9BB94867865E23A0420439C33E253D1151E43943","Qualified540 input drift");
            Need(ChapterEconomy.ProjectPrice(GameIds.HouseImprove)==(candidate=="small"?500:480)&&ChapterEconomy.ProjectPrice("cart.build")== (candidate=="small"?150:140)&&ChapterEconomy.ProjectPrice("power-source.build")==190&&SkillTree.Nodes.Sum(n=>n.Price)==610,"Compiled candidate does not match declared fields");
            using var data=JsonDocument.Parse(File.ReadAllText(input));var timer=Stopwatch.StartNew();var rows=new List<object>();var actualRuns=new List<RunResult>();
            bool eligible=true;
            foreach(bool early in new[]{false,true})foreach(uint seed in Seeds.Take(3))
            {
                string policy=early?"affordable-skills-first":"construction-first";
                var old=data.RootElement.GetProperty("runs").EnumerateArray().Single(r=>r.GetProperty("Seed").GetUInt32()==seed&&r.GetProperty("Policy").GetString()==policy).Deserialize<RunResult>();
                Need(old.ConstructionSpend==9400&&old.SkillSpend==610&&old.FreshGrant==37&&old.OfflineIncome==0,"Unmatched540 baseline");
                var sim=new Simulation(seed,early,false);var actual=sim.Run(0);actualRuns.Add(actual);
                Need(sim.Game.State.coins==37+actual.SpinIncome-CadenceSpend-610&&actual.ConstructionCount==24,"Independent wallet/construction identity");
                uint expectedSeed=seed;for(int spin=1;spin<=actual.Spins;spin++)expectedSeed=GameLogic.Draw(expectedSeed,spin).seedAfter;
                Need(sim.Game.State.seed==expectedSeed,"Price change drew extra RNG");
                var oldGaps=GapSpins(old);var gaps=GapSpins(actual);
                bool reducedWorst=actual.LongestConstructionGapNominalAutoSeconds<=old.LongestConstructionGapNominalAutoSeconds-3.75;
                bool targets=new[]{"cart.build","power-source.build",GameIds.HouseImprove}.All(id=>gaps[id]<=oldGaps[id]);
                bool earlySame=new[]{"first-repair","workshop"}.All(id=>actual.Milestones[id].Spins==old.Milestones[id].Spins&&actual.Milestones[id].Wallet==old.Milestones[id].Wallet);
                bool order=actual.Purchases.Where(p=>p.Kind=="construction").Select(p=>p.Id).SequenceEqual(old.Purchases.Where(p=>p.Kind=="construction").Select(p=>p.Id));
                bool opportunity=!early||SkillSpendBefore(actual,"cart.build")==120&&SkillSpendBefore(actual,"power-source.build")==280;
                bool local=reducedWorst&&targets&&earlySame&&order&&opportunity;eligible&=local;
                rows.Add(new{seed,policy,criteria=new{reducedWorst,targetsNotWorse=targets,earlyRepairWorkshopUnchanged=earlySame,constructionOrderUnchanged=order,earlySkillSpendWindowPreserved=opportunity,eligible=local},oldLongest=old.LongestConstructionGapNominalAutoSeconds,newLongest=actual.LongestConstructionGapNominalAutoSeconds,oldGaps,newGaps=gaps,chapterSpinsSaved=old.Milestones["chapter-end"].Spins-actual.Milestones["chapter-end"].Spins,baseline=old,actual,terminalSeed=sim.Game.State.seed,carry=WaveProgress.Of(sim.Game.State).Delivery.PigeonProgress});
                Console.WriteLine("PASS "+candidate+" seed="+seed+" "+policy+" chapter="+actual.Milestones["chapter-end"].Spins+" longest="+actual.LongestConstructionGapNominalAutoSeconds+" eligible="+local);
                Need(timer.Elapsed.TotalSeconds<180,"180-second budget at case boundaries");GC.Collect();
            }
            foreach(uint seed in Seeds.Take(3))foreach(string id in new[]{"cart.build","power-source.build"})
            {
                var early=actualRuns.Single(r=>r.Seed==seed&&r.Policy=="affordable-skills-first");var deferred=actualRuns.Single(r=>r.Seed==seed&&r.Policy=="construction-first");
                bool delayed=early.Purchases.Single(p=>p.Id==id).Spin>deferred.Purchases.Single(p=>p.Id==id).Spin;eligible&=delayed;Need(delayed,"Optional early skills lost their construction opportunity cost");
            }
            string json=JsonSerializer.Serialize(new{scope="Six actual-Core runs, two predeclared price-only cadence candidates maximum; baseline reused",candidate,constructionSpend=CadenceSpend,qualifiedSha256=hash,eligible,criterion="Every matched worst gap improves >=1spin; no target gap worsens; same early repair/workshop, construction order, and skill spend/positive opportunity window",runnerWallSeconds=timer.Elapsed.TotalSeconds,rows},new JsonSerializerOptions{WriteIndented=true});
            Need(json.Length<1024*1024,"1MiB output cap");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json+"\n");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("FAIL "+e);return 1;}
    }
#endif
    // Two fixed fresh cases only; the historical runner and old-source builds stay available.
    static int Equivalence(string[] args)
    {
        try
        {
            Need(args.Length==4&&args[0]=="--qualified"&&args[2]=="--output","Use --qualified <frozen house540 JSON> --output <new JSON>");
            string input=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[3]);
            Need(!File.Exists(output)&&new FileInfo(input).Length<2*1024*1024,"Preserve receipts and bound input");
            string inputHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input)));
            Need(inputHash=="5AB11FDB1201835F39BB7BCF9BB94867865E23A0420439C33E253D1151E43943","Qualified immutable house540 input changed");
            using var qualified=JsonDocument.Parse(File.ReadAllText(input));
            Need(qualified.RootElement.GetProperty("profile").GetString()=="house-540","Wrong qualified profile");
            Need(ChapterEconomy.ProjectPrice(GameIds.HouseImprove)==540&&SkillTree.Nodes.Sum(n=>n.Price)==610,"Expected actual r11 construction/skill offers");
            var cases=new List<object>();var timer=Stopwatch.StartNew();
            foreach(bool early in new[]{false,true})
            {
                string policy=early?"affordable-skills-first":"construction-first";
                var expected=qualified.RootElement.GetProperty("runs").EnumerateArray().Single(r=>r.GetProperty("Seed").GetUInt32()==1&&r.GetProperty("Policy").GetString()==policy).Deserialize<RunResult>();
                var simulation=new Simulation(1,early,false);var actual=simulation.Run(0);
                string expectedFacts=JsonSerializer.Serialize(Facts(expected)),actualFacts=JsonSerializer.Serialize(Facts(actual));
                Need(expectedFacts==actualFacts,"Economic trace/milestone mismatch for "+policy);
                Need(actual.Writes==expected.Writes,"Journal write count changed for "+policy);
                var state=simulation.Game.State;
                Need(state.coins==37+actual.SpinIncome-9400-610&&actual.OfflineIncome==0&&actual.Milestones["first-repair"].Spins==1&&actual.Milestones["first-repair"].Wallet==5,"Independent wallet/repair invariant");
                uint seed=1;int pigeons=0;int loft=expected.Purchases.Single(p=>p.Id==GameIds.PigeonLoftBuild).Spin;
                for(int spin=1;spin<=expected.Spins;spin++){var draw=GameLogic.Draw(seed,spin);seed=draw.seedAfter;if(spin>loft)pigeons+=draw.symbols.Count(s=>s==3);}
                int carry=WaveProgress.Of(state).Delivery.PigeonProgress;
                Need(state.seed==seed&&carry==pigeons%3,"Intended seeded stream/delivery carry mismatch");
                Need(simulation.Game.Chapter.ConstructionPriceRevision==ConstructionPricing.CurrentRevision,"R11 did not retain explicit current price identity");
                cases.Add(new{seed=1,policy,status="equivalent",economicTraceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(actualFacts))),expected,actual,terminalSeed=state.seed,expectedTerminalSeed=seed,terminalCarry=carry,expectedTerminalCarry=pigeons%3,constructionPricing=simulation.Game.Chapter.ConstructionPriceRevision,journalWritesEqual=true,metadata=new{qualifiedSerializedCharacters=expected.SerializedCharacters,r11SerializedCharacters=actual.SerializedCharacters,explanation="R11 explicit construction price revision and frozen before/after identity change JSON bytes; runner timing/managed heap are not economics"}});
                Console.WriteLine("PASS seed1 "+policy+" chapter="+actual.Milestones["chapter-end"].Spins+" allSkills="+actual.Spins+" wallet="+state.coins+" seed="+state.seed+" carry="+carry);
                Need(timer.Elapsed.TotalSeconds<60,"Bounded60-second two-case run exceeded");
            }
            string json=JsonSerializer.Serialize(new{scope="Two fixed-seed actual-r11 fresh cases versus qualified house540; no production changes",qualifiedCommit="0ca71b4c962c62408a36ed224a75aeb44e1bab9f",qualifiedSha256=inputHash,runnerWallSeconds=timer.Elapsed.TotalSeconds,cases},new JsonSerializerOptions{WriteIndented=true});
            Need(json.Length<256*1024,"Bounded256KiB receipt exceeded");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json+"\n");return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}
    }
    static object Facts(RunResult r)=>new{r.Seed,r.Policy,r.Scenario,r.Status,r.Spins,r.FreshGrant,r.SpinIncome,r.OfflineIncome,r.AbsenceSeconds,r.ConstructionSpend,r.SkillSpend,r.WalletAfterSkills,r.SkillsOwnedAtChapterEnd,r.SkillSpendAtChapterEnd,r.ConstructionCount,r.LongestConstructionGapSpins,r.LongestConstructionGapNominalAutoSeconds,r.Milestones,r.Purchases};
#endif
}
