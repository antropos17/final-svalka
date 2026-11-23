using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JunkToNeon.Core;

// Bounded producer regressions. Synthetic balances are labelled and are not pacing evidence.
static class Program
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string Repository,Output;
    static readonly List<string> Passed=new List<string>();
    sealed class Storage:ISnapshotStorage
    {
        internal string Primary,Backup;internal int Reads,Writes,FailAt=-1;internal bool Lands;
        public string ReadPrimary(){Reads++;return Primary;}public string ReadBackup(){Reads++;return Backup;}
        public void Write(string raw){Writes++;if(Writes==FailAt&&!Lands)throw new IOException("Private write-before fault");Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("Private write-after fault");}
    }
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}catch(ArgumentException){return;}catch(FormatException){return;}catch(OverflowException){return;}catch(NotSupportedException){return;}throw new Exception("Expected refusal");}
    static string Raw(ModelState s){return Codec.Encode(s);}
    static ModelState Input(string path){return Codec.Decode(File.ReadAllText(Path.Combine(Repository,path)));}
    static Storage Stored(ModelState s){return new Storage{Primary=Raw(s)};}
    static GameSession Open(Storage storage){var g=GameSession.Open(new GameStore(storage,Codec),1);Need(!g.RecoveryRequired,"Private open failed: "+g.Error);return g;}
    static void NoIo(Storage s,GameSession g,Action action)
    {string p=s.Primary,b=s.Backup,r=Raw(g.State);int reads=s.Reads,writes=s.Writes;Reject(action);Need(p==s.Primary&&b==s.Backup&&reads==s.Reads&&writes==s.Writes&&r==Raw(g.State)&&!g.RecoveryRequired,"Refusal changed state/IO/health");}
    static ModelState Spin(ModelState s){return GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1));}
    static ModelState Spins(int count,ModelState s=null){s=s??ModelState.Fresh(1);for(int i=0;i<count;i++)s=Spin(s);return s;}
    static ModelState Arcade(ModelState s,string action,string id,long instance=0,int target=-1)
    {return GameLogic.ApplyPending(ArcadeEconomy.Reserve(s,ArcadeEconomy.Quote(s,action,id,instance,target)));}
    static ModelState Expansion(ModelState s,string action,string id,int slot=0)
    {return GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,ExpansionEconomy.Quote(s,action,id,slot)));}
    static void Do(GameSession g,string action,string id,long instance=0,int target=-1)
    {g.ExecuteArcade(g.GetArcadeQuote(action,id,g.Arcade.Sequence,instance,target));}
    static ModelState Synthetic(ModelState s=null,long diamonds=30,int parts=100,int potatoes=100,int energy=20)
    {
        // Only affordability/overflow/cap fixtures; real earning is tested separately.
        s=s??Spins(24);var e=new ExpansionDraft(s.expansion);var a=new ArcadeDraft(e.ArcadeData){Diamonds=diamonds};
        e.ArcadeData=a.Freeze();e.Parts=parts;e.Potatoes=potatoes;e.Energy=energy;s.expansion=e.Freeze();GameLogic.Validate(s);return s;
    }
    static ModelState Start(ModelState s,string id){return Arcade(s,"start",id);}
    static ModelState Move(ModelState s,string action,int target=-1)
    {var run=ArcadeEconomy.Project(s).ActiveRun;return Arcade(s,action,run.Id,run.InstanceSequence,target);}
    static ModelState ReadySorting(ModelState s,int right)
    {s=Start(s,ArcadeCatalog.Sorting);for(int i=0;i<8;i++){int symbol=ArcadeEconomy.Project(s).ActiveRun.CurrentSortingSymbol;s=Move(s,"sort",i<right?symbol:(symbol+1)%4);}return s;}
    static ModelState ReadyMemory(ModelState s,int attempts,bool solved=true)
    {
        s=Start(s,ArcadeCatalog.Memory);var run=s.expansion.ArcadeData.Run;
        // Use the frozen private board as fixture input only; expected rewards below are literal.
        int left=0,other=Enumerable.Range(1,5).First(x=>run.Symbol(x)!=run.Symbol(left));
        int misses=solved?attempts-3:attempts;
        for(int i=0;i<misses;i++){s=Move(s,"pick",left);s=Move(s,"pick",other);if(i<misses-1||solved)s=Move(s,"continue");}
        if(solved)for(int symbol=0;symbol<3;symbol++){int[] pair=Enumerable.Range(0,6).Where(x=>run.Symbol(x)==symbol).ToArray();s=Move(s,"pick",pair[0]);s=Move(s,"pick",pair[1]);}
        return s;
    }
    static string Hash(string raw){using(var h=SHA256.Create())return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(raw)));}
    static void Export(string name,ModelState s)
    {
        string raw=Raw(s);Need(Raw(Codec.Decode(raw))==raw,"Export roundtrip");
        string pinned=Path.Combine(Repository,"Checks/ArcadeCore/Evidence",name+".json");
        if(File.Exists(pinned))Need(File.ReadAllText(pinned)==raw,"Frozen pre-projection canonical bytes changed: "+name);
        File.WriteAllText(Path.Combine(Output,name+".json"),raw);
    }

    static void Compatibility()
    {
        string[] old={"v1-boosted-bonus","v1-equipment-workshop","v1-trade535-saved540"};
        foreach(string name in old)
        {
            string dir="Checks/ExpansionCore/Fixtures/",pending=File.ReadAllText(Path.Combine(Repository,dir+name+"-pending.json"));
            string committed=File.ReadAllText(Path.Combine(Repository,dir+name.Replace("trade535","trade555")+"-committed.json"));
            var p=Codec.Decode(pending);Need(Raw(p)==pending,"V1 pending bytes changed");Need(Raw(GameLogic.ApplyPending(p))==committed,"V1 frozen result changed");
            var store=Stored(p);var game=Open(store);Need(store.Writes==1&&store.Primary==committed&&!game.Arcade.IsAdopted,"V1 recovery earned/adopted Arcade");
            Open(store);Need(store.Writes==1,"V1 repeated recovery");game.ExecuteChapterSpin(game.State.sequence+1);
            Need(game.State.expansion.FormatVersion==3&&game.Arcade.ForegroundSpins==1&&game.Arcade.Tickets==0,"NEW spin after V1 did not opt V3");
        }
        string v2p=File.ReadAllText(Path.Combine(Repository,"Checks/ArcadeCore/Fixtures/v2-focused-boost-pending.json"));
        string v2c=File.ReadAllText(Path.Combine(Repository,"Checks/ArcadeCore/Fixtures/v2-focused-boost-committed.json"));
        Need(Hash(v2p)=="F8EE8917794986D8F65F355776614F773EC22B2F486F66D9F5AD3AB77444EF38"&&Hash(v2c)=="B50D311E7C2AD7D6AFED095F07B99C2603BB91787CBD3FCA9ED976A711E24965","Pinned V2 fixture bytes changed");
        var old2=Codec.Decode(v2p);Need(Raw(old2)==v2p&&Raw(GameLogic.ApplyPending(old2))==v2c,"V2 frozen bytes/result changed");
        var st=Stored(old2);var g=Open(st);Need(st.Writes==1&&!g.Arcade.IsAdopted,"V2 replay adopted Arcade");g.ExecuteChapterSpin(g.State.sequence+1);
        Need(g.Arcade.ForegroundSpins==1&&g.State.lastSpin.expansion.RuleRevision=="expansion.rules.v3","V2 next NEW spin did not opt V3");
        foreach(string path in new[]{"Checks/Milestones/fixtures/v2-selected540.json","Checks/EraMigration/R8/Fixtures/pending-canonical-spin.json","Checks/EraMigration/R8/Fixtures/pending-universal-2.json"})
        {string raw=File.ReadAllText(Path.Combine(Repository,path)).Trim();var s=Codec.Decode(raw);Need(Raw(s)==raw&&!ArcadeEconomy.Project(s).IsAdopted,"Older literal route changed");var storage=Stored(s);var session=Open(storage);Need(!session.Arcade.IsAdopted,"Pre-expansion recovery earned tickets");}
        Need(ExpansionCatalog.Offers.Count==31&&ArcadeCatalog.Offers.Count==6,"Catalogs replaced/merged");
    }
    static void Tickets()
    {
        var initial=ModelState.Fresh(1);var storage=Stored(initial);var game=Open(storage);string before=storage.Primary;
        for(int i=0;i<3;i++){Need(!game.Arcade.IsAdopted&&game.Arcade.Diamonds==0&&game.Arcade.Tickets==0,"Read adopted/earned");}
        Need(storage.Primary==before&&storage.Writes==0,"Read wrote");NoIo(storage,game,()=>game.GetArcadeQuote("start",ArcadeCatalog.Memory,game.Arcade.Sequence));
        ModelState s=initial,control=initial;
        for(int i=1;i<=40;i++)
        {
            s=Spin(s);control=GameLogic.ApplyPending(GameLogic.ReserveHistorical(control,"spin",0,control.sequence+1));
            var a=ArcadeEconomy.Project(s);Need(a.ForegroundSpins==i&&a.TicketProgress==i%8&&a.Tickets==Math.Min(3,i/8)&&a.Diamonds==0,"Future cadence/cap/zero Diamonds");
            Need(s.seed==control.seed&&s.coins==control.coins&&s.lastSpin.symbols.SequenceEqual(control.lastSpin.symbols),"Arcade changed base RNG/income");
            Need(a.LastResourceReceipt.TicketsAdded==(i%8==0&&i<=24?1:0),"Ticket receipt credited discarded overflow");
        }
        s=Start(s,ArcadeCatalog.Memory);Need(ArcadeEconomy.Project(s).Tickets==2,"Start ticket debit");
        s=Spins(7,s);Need(ArcadeEconomy.Project(s).Tickets==2&&ArcadeEconomy.Project(s).TicketProgress==7,"Cap bank leaked tickets");
        s=Spin(s);Need(ArcadeEconomy.Project(s).Tickets==3&&ArcadeEconomy.Project(s).ActiveRun!=null,"Next full future cycle/optional base play");
        uint challenge=s.expansion.ArcadeData.ChallengeSeed;long fg=s.expansion.ArcadeData.ForegroundSpins;
        s=GameLogic.ApplyPending(GameLogic.Reserve(s,"clock",0,s.sequence+1,100));Need(s.expansion.ArcadeData.ChallengeSeed==challenge&&s.expansion.ArcadeData.ForegroundSpins==fg,"Clock generated Arcade");
        var zero=Spins(8);var e=new ExpansionDraft(zero.expansion){Energy=0};zero.expansion=e.Freeze();zero=Spin(zero);Need(zero.expansion.Energy==1,"Zero energy gated base spin");
        Export("cap-active-run",s);
    }
    static void Memory()
    {
        var st=Stored(Spins(8));var g=Open(st);Do(g,"start",ArcadeCatalog.Memory);var r=g.Arcade.ActiveRun;long id=r.InstanceSequence;
        Need(r.Phase=="first"&&r.Attempts==0&&r.Cursor==0&&r.MemoryFaces.All(x=>x==-1)&&g.Arcade.Tickets==0,"Memory initial masked state");
        Need(g.State.expansion.ArcadeData.Run.BoardToken=="122001"&&g.State.expansion.ArcadeData.ChallengeSeed==265159372U,"Pinned first Memory shuffle");
        NoIo(st,g,()=>g.GetArcadeQuote("claim",r.Id,g.Arcade.Sequence,id));NoIo(st,g,()=>g.GetArcadeQuote("start",ArcadeCatalog.Sorting,g.Arcade.Sequence));
        Do(g,"pick",r.Id,id,0);r=g.Arcade.ActiveRun;Need(r.Phase=="second"&&r.Attempts==0&&r.MemoryFaces.SequenceEqual(new[]{1,-1,-1,-1,-1,-1}),"First card consumed attempt/revealed hidden cards");
        NoIo(st,g,()=>g.GetArcadeQuote("pick",r.Id,g.Arcade.Sequence,id,0));g=Open(st);Need(g.Arcade.ActiveRun.Phase=="second","First card reload lost");
        Do(g,"pick",r.Id,id,1);r=g.Arcade.ActiveRun;Need(r.Phase=="mismatch"&&r.Attempts==1&&r.MemoryFaces.SequenceEqual(new[]{1,2,-1,-1,-1,-1}),"Mismatch not held");
        NoIo(st,g,()=>g.GetArcadeQuote("pick",r.Id,g.Arcade.Sequence,id,2));g=Open(st);Need(g.Arcade.ActiveRun.Phase=="mismatch","Mismatch reload cleared");
        Do(g,"continue",r.Id,id);Need(g.Arcade.ActiveRun.Attempts==1&&g.Arcade.ActiveRun.Cursor==3&&g.Arcade.ActiveRun.MemoryFaces.All(x=>x==-1),"Continue attempt/mask drift");
        Do(g,"pick",r.Id,id,1);Do(g,"pick",r.Id,id,2);Need(g.Arcade.ActiveRun.MatchedMask==6&&g.Arcade.ActiveRun.MatchedPairs==1,"Matched pair missing");
        NoIo(st,g,()=>g.GetArcadeQuote("pick",r.Id,g.Arcade.Sequence,id,1));Export("memory-partial",g.State);
        foreach(var pair in new[]{new[]{3,3},new[]{4,3},new[]{5,2},new[]{6,2},new[]{7,1},new[]{9,1}})
        {
            var ready=ReadyMemory(Spins(8),pair[0]);var view=ArcadeEconomy.Project(ready);
            Need(view.ActiveRun.Ready&&view.ActiveRun.Attempts==pair[0]&&view.ActiveRun.ReadyReward==pair[1]&&view.Diamonds==0,"Memory literal scoring/Ready payment");
            var committed=Move(ready,"claim");Need(ArcadeEconomy.Project(committed).Diamonds==pair[1]&&ArcadeEconomy.Project(committed).ActiveRun==null,"Memory claim");
        }
        var lose=ReadyMemory(Spins(8),9,false);Need(ArcadeEconomy.Project(lose).ActiveRun.ReadyReward==0&&ArcadeEconomy.Project(lose).ActiveRun.Attempts==9,"Memory attempt9 unsolved");
        Export("memory-zero-ready",lose);
    }
    static void Sorting()
    {
        var first=Start(Spins(8),ArcadeCatalog.Sorting);Need(first.expansion.ArcadeData.Run.BoardToken=="21103203"&&first.expansion.ArcadeData.ChallengeSeed==4012273292U,"Pinned first Sorting shuffle");
        var v=ArcadeEconomy.Project(first).ActiveRun;Need(v.MemoryFaces.Count==0&&v.CurrentSortingSymbol==2&&v.Attempts==0,"Sorting public projection");
        first=Move(first,"sort",0);Need(ArcadeEconomy.Project(first).ActiveRun.Attempts==1&&ArcadeEconomy.Project(first).ActiveRun.Correct==0&&ArcadeEconomy.Project(first).ActiveRun.CurrentSortingSymbol==1,"Wrong bin did not consume one");
        first=Codec.Decode(Raw(first));Need(ArcadeEconomy.Project(first).ActiveRun.Attempts==1,"Sorting cursor reload");
        for(int right=0;right<=8;right++)
        {var s=ReadySorting(Spins(8),right);var r=ArcadeEconomy.Project(s).ActiveRun;int expected=new[]{0,0,0,1,1,2,2,3,3}[right];Need(r.Ready&&r.Attempts==8&&r.Correct==right&&r.ReadyReward==expected&&r.CurrentSortingSymbol==-1,"Sorting literal score bands");Need(ArcadeEconomy.Project(s).Diamonds==0,"Sorting auto-paid");}
        Export("sorting-ready-three",ReadySorting(Spins(24),8));
    }
    static void ClaimsAndStale()
    {
        foreach(int right in new[]{0,8})
        {
            var s=ReadySorting(Spins(24),right);var st=Stored(s);var g=Open(st);var r=g.Arcade.ActiveRun;
            var q=g.GetArcadeQuote("claim",r.Id,g.Arcade.Sequence,r.InstanceSequence);int writes=st.Writes;g.ExecuteArcade(q);
            Need(st.Writes==writes+2&&g.Arcade.LastClaim.Key==q.Key&&g.Arcade.LastClaim.Diamonds==(right==0?0:3)&&g.Arcade.ActiveRun==null,"Zero/nonzero explicit claim not retained");
            var claim=g.Arcade.LastClaim;Do(g,"start",ArcadeCatalog.Memory);string newer=st.Primary;writes=st.Writes;g.ExecuteArcade(q);
            Need(newer==st.Primary&&writes==st.Writes&&g.Arcade.ActiveRun!=null&&g.Arcade.LastClaim.Key==claim.Key,"Old exact claim affected later run");
            Do(g,"abandon",ArcadeCatalog.Memory,g.Arcade.ActiveRun.InstanceSequence);Need(g.Arcade.Tickets==1&&g.Arcade.Diamonds==(right==0?0:3),"Abandon refunded/paid");
            Do(g,"start",ArcadeCatalog.Sorting);while(!g.Arcade.ActiveRun.Ready){var run=g.Arcade.ActiveRun;Do(g,"sort",run.Id,run.InstanceSequence,run.CurrentSortingSymbol);}
            var latest=g.Arcade.ActiveRun;Do(g,"claim",latest.Id,latest.InstanceSequence);NoIo(st,g,()=>g.ExecuteArcade(q));
        }
        var storage=Stored(Spins(16));var game=Open(storage);long displayed=game.Arcade.Sequence;var start=game.GetArcadeQuote("start",ArcadeCatalog.Memory,displayed);
        game.ExecuteChapterSpin(game.State.sequence+1);NoIo(storage,game,()=>game.GetArcadeQuote("start",ArcadeCatalog.Memory,displayed));NoIo(storage,game,()=>game.ExecuteArcade(start));
        Do(game,"start",ArcadeCatalog.Memory);var active=game.Arcade.ActiveRun;var pick=game.GetArcadeQuote("pick",active.Id,game.Arcade.Sequence,active.InstanceSequence,0);
        uint seed=game.State.expansion.ArcadeData.ChallengeSeed;game.ExecuteChapterSpin(game.State.sequence+1);
        Need(game.Arcade.ActiveRun.Cursor==0&&game.State.expansion.ArcadeData.ChallengeSeed==seed,"Foreground play rerolled active run");NoIo(storage,game,()=>game.ExecuteArcade(pick));
        Do(game,"pick",active.Id,active.InstanceSequence,0);string raw=storage.Primary;int count=storage.Writes;var current=game.State.expansion.ArcadeData.LastCommand;game.ExecuteArcade(current);Need(raw==storage.Primary&&count==storage.Writes,"Retained exact input replay wrote");
    }
    static void OffersAndBoost()
    {
        var s=Synthetic();long coins=s.coins;uint primary=s.seed,challenge=s.expansion.ArcadeData.ChallengeSeed;
        s=Arcade(s,"buy",ArcadeCatalog.Slot1);Need(s.expansion.Parts==80&&s.expansion.Potatoes==100&&ArcadeEconomy.Project(s).Diamonds==28&&ArcadeEconomy.Project(s).SlotLevel==1,"LevelI exact cost");
        s=Arcade(s,"buy",ArcadeCatalog.Slot2);Need(s.expansion.Parts==80&&s.expansion.Potatoes==80&&ArcadeEconomy.Project(s).Diamonds==24,"LevelII exact cost");
        Reject(()=>ArcadeEconomy.Quote(s,"buy",ArcadeCatalog.Slot3));s=Expansion(s,"buy","bonus.unlock");s=Arcade(s,"buy",ArcadeCatalog.Slot3);
        Need(s.expansion.Parts==30&&s.expansion.Potatoes==40&&ArcadeEconomy.Project(s).Diamonds==17&&ArcadeEconomy.Project(s).SlotLevel==3,"LevelIII exact cost/gate");
        s=Arcade(s,"buy",ArcadeCatalog.PartsPack);s=Arcade(s,"buy",ArcadeCatalog.PotatoesPack);Need(s.expansion.Parts==35&&s.expansion.Potatoes==45&&ArcadeEconomy.Project(s).Diamonds==15,"Packs exact amount");
        int energy=s.expansion.Energy;s=Arcade(s,"buy",ArcadeCatalog.BonusBoost);Need(s.expansion.BoostArmed&&s.expansion.Energy==energy&&ArcadeEconomy.Project(s).Diamonds==14,"Diamond boost charged Energy/second authority");
        Reject(()=>ExpansionEconomy.Quote(s,"boost",ExpansionCatalog.NextBonusBoost));Reject(()=>ArcadeEconomy.Quote(s,"buy",ArcadeCatalog.BonusBoost));
        Need(s.coins==coins&&s.seed==primary&&s.expansion.ArcadeData.ChallengeSeed==challenge,"Shop changed coins/RNG");
        var old=Expansion(Synthetic(),"buy","bonus.unlock");old=Expansion(old,"boost",ExpansionCatalog.NextBonusBoost);Need(old.expansion.Energy==5&&ArcadeEconomy.Project(old).Diamonds==30,"Old15Energy boost drift");Reject(()=>ArcadeEconomy.Quote(old,"buy",ArcadeCatalog.BonusBoost));
        foreach(string id in new[]{ArcadeCatalog.PartsPack,ArcadeCatalog.PotatoesPack})
        {var full=Synthetic(parts:id==ArcadeCatalog.PartsPack?196:100,potatoes:id==ArcadeCatalog.PotatoesPack?196:100);var st=Stored(full);var g=Open(st);NoIo(st,g,()=>g.GetArcadeQuote("buy",id,g.Arcade.Sequence));}
        var exact=Synthetic(parts:195,potatoes:195);exact=Arcade(exact,"buy",ArcadeCatalog.PartsPack);exact=Arcade(exact,"buy",ArcadeCatalog.PotatoesPack);Need(exact.expansion.Parts==200&&exact.expansion.Potatoes==200,"Exact pack headroom refused");
        var storage=Stored(Synthetic());var game=Open(storage);var a=game.GetArcadeQuote("buy",ArcadeCatalog.PartsPack,game.Arcade.Sequence);var b=game.GetArcadeQuote("buy",ArcadeCatalog.PotatoesPack,game.Arcade.Sequence);game.ExecuteArcade(a);NoIo(storage,game,()=>game.ExecuteArcade(b));
        Export("slot-three-boost-armed",s);
    }
    static ModelState Slot(ModelState s,int level)
    {s=Synthetic(s);if(level>=1)s=Arcade(s,"buy",ArcadeCatalog.Slot1);if(level>=2)s=Arcade(s,"buy",ArcadeCatalog.Slot2);if(level==3){s=Expansion(s,"buy","bonus.unlock");s=Arcade(s,"buy",ArcadeCatalog.Slot3);}return s;}
    static void SlotAttributeProjection()
    {
        int[][] matrix={new[]{0,0,0,0},new[]{1,0,0,0},new[]{1,1,0,0},new[]{1,1,1,1}};
        int[][] parts={new[]{2,2,3,0,5},new[]{2,3,4,0,5},new[]{2,3,4,0,5},new[]{2,3,4,0,6}};
        int[][] potatoes={new[]{0,0,0,1,2},new[]{0,0,0,1,2},new[]{0,0,0,2,3},new[]{0,0,0,2,4}};
        int[][] symbols={new[]{3,2,2},new[]{1,3,2},new[]{1,2,2},new[]{3,3,0},new[]{0,3,0}};
        var storage=Stored(Spins(24));var game=Open(storage);string original=Raw(game.State);int reads=storage.Reads,writes=storage.Writes;
        var originalQuote=game.GetArcadeQuote("start",ArcadeCatalog.Memory,game.Arcade.Sequence);
        for(int level=0;level<=3;level++)
        {
            var a=ArcadeCatalog.SlotAttributes(level);
            Need(a.Level==level&&new[]{a.BoxPresencePartBonus,a.PotatoPresencePotatoBonus,a.RawBonusPartsAddition,a.RawBonusPotatoesAddition}.SequenceEqual(matrix[level]),"Literal slot attribute matrix");
            Need(ReferenceEquals(a,ArcadeCatalog.SlotAttributes(level)),"Slot projection is not cached immutable data");
            var s=Slot(Spins(24),level);if(!s.expansion.BonusUnlocked)s=Expansion(s,"buy","bonus.unlock");
            var e=new ExpansionDraft(s.expansion){Parts=0,Potatoes=0,Energy=0};s.expansion=e.Freeze();GameLogic.Validate(s);
            var v=ArcadeEconomy.Project(s);Need(v.SlotLevel==level&&new[]{v.BoxPresencePartBonus,v.PotatoPresencePotatoBonus,v.RawBonusPartsAddition,v.RawBonusPotatoesAddition}.SequenceEqual(matrix[level]),"Current view does not match independent attribute matrix");
            for(int spin=0;spin<5;spin++)
            {
                if(spin==1||spin==4)
                {
                    // Actual production foreground spin at base-presence and raw-bonus cap boundaries.
                    var cap=s.Copy();e=new ExpansionDraft(cap.expansion){Parts=199,Potatoes=199};cap.expansion=e.Freeze();cap=Spin(cap);
                    Need(cap.lastSpin.symbols.SequenceEqual(symbols[spin])&&cap.expansion.LastResourceReceipt.Parts==1&&cap.expansion.LastResourceReceipt.Potatoes==(spin==4?1:0),"Literal capped production slot output");
                    var receipt=cap.expansion.ArcadeData.LastResourceReceipt;Need(receipt.SlotPartsIncluded==0&&receipt.SlotPotatoesIncluded==0,"Capped slot projection became another grant");
                }
                s=Spin(s);var r=s.expansion.LastResourceReceipt;
                Need(s.lastSpin.symbols.SequenceEqual(symbols[spin])&&r.Parts==parts[level][spin]&&r.Potatoes==potatoes[level][spin]&&r.Energy==1,"Independent five-spin production slot output");
            }
        }
        foreach(int level in new[]{-1,4,int.MinValue,int.MaxValue})
        {bool refused=false;try{ArcadeCatalog.SlotAttributes(level);}catch(ArgumentOutOfRangeException){refused=true;}Need(refused,"Out-of-range slot projection accepted");}
        foreach(var property in typeof(ArcadeSlotAttributes).GetProperties(BindingFlags.Instance|BindingFlags.Public))Need(property.SetMethod==null||!property.SetMethod.IsPublic,"Slot attributes expose a mutable setter");
        Need(typeof(ArcadeSlotAttributes).IsSealed&&typeof(ArcadeSlotAttributes).GetConstructors().Length==0,"Slot attributes expose mutable/substitutable construction");
        Need(Raw(game.State)==original&&storage.Primary==original&&storage.Reads==reads&&storage.Writes==writes&&game.GetArcadeQuote("start",ArcadeCatalog.Memory,game.Arcade.Sequence).Key==originalQuote.Key,"Read-only slot projection changed state/IO/quote identity");
        File.WriteAllText(Path.Combine(Output,"slot-attributes.txt"),"PASS literal level0..3 matrix, cached immutable projection, invalid bounds, unchanged full-state quote/IO;20 actual production spins and8 cap boundaries with independent fixed expected outputs.\n");
    }
    static void ResourceArithmetic()
    {
        // Literal seed1: first symbols [0,0,2], then [0,2,0], then [0,0,1].
        var basis=Synthetic();var one=Slot(basis,1);var two=Slot(basis,2);
        foreach(var tuple in new[]{new[]{0,0,0},new[]{0,0,1},new[]{1,1,1},new[]{2,2,2},new[]{0,1,3}})
        {
            var a=ExpansionEconomy.Observe(one.expansion,one.sequence+1,tuple);var b=ExpansionEconomy.Observe(two.expansion,two.sequence+1,tuple);
            int baseParts=tuple.Count(x=>x==1||x==2),basePotatoes=tuple.Count(x=>x==0);
            Need(a.LastResourceReceipt.Parts==baseParts+(tuple.Contains(1)?1:0)&&a.LastResourceReceipt.Potatoes==basePotatoes,"SlotI is per-symbol rather than once per presence");
            Need(b.LastResourceReceipt.Parts==baseParts+(tuple.Contains(1)?1:0)&&b.LastResourceReceipt.Potatoes==basePotatoes+(tuple.Contains(0)?1:0),"SlotII presence effects");
        }
        var triple=Slot(Spins(24),3);triple=Expansion(triple,"buy","workbench.bonus.1");triple=Synthetic(triple);triple=Expansion(triple,"buy","workbench.bonus.2");triple=Expansion(triple,"focus","focus.parts");triple=Arcade(triple,"buy",ArcadeCatalog.BonusBoost);
        // Synthetic boundary fixture: upcoming bonus symbol0 is 5P; +bench(1,1)+slot(1,1), Parts focus=>9P, boost=>18P.
        triple=Spins(4,triple);var e=new ExpansionDraft(triple.expansion){Parts=0,Potatoes=0};triple.expansion=e.Freeze();GameLogic.Validate(triple);
        var result=ExpansionEconomy.Observe(triple.expansion,triple.sequence+1,new[]{3,3,3});
        Need(result.LastResourceReceipt.BonusSymbol==0&&result.LastResourceReceipt.BonusParts==18&&result.LastResourceReceipt.BonusPotatoes==0&&!result.BoostArmed,"SlotIII/workbench/focus/shared boost order");
        Need(result.ArcadeData.LastResourceReceipt.SlotPartsIncluded==4&&result.ArcadeData.LastResourceReceipt.PartsAdded==18,"SlotIII marginal double-count");
        e=new ExpansionDraft(triple.expansion){Parts=199,Potatoes=200};var capped=ExpansionEconomy.Observe(e.Freeze(),triple.sequence+1,new[]{1,1,1});
        Need(capped.LastResourceReceipt.Parts==1&&capped.ArcadeData.LastResourceReceipt.SlotPartsIncluded==0&&capped.LastResourceReceipt.Potatoes==0,"Base-first caps/slot included credit");
        triple=Spin(triple);Need(triple.expansion.ArcadeData.LastResourceReceipt.PartsAdded==triple.expansion.LastResourceReceipt.Parts,"Production spin second resource authority");Export("slot-three-bonus",triple);
    }
    static void MoneyAndOldFeatures()
    {
        foreach(string path in new[]{"Checks/Milestones/fixtures/v2-selected540.json","Checks/ExpansionCore/Fixtures/v1-trade555-saved540-committed.json","Checks/EraMigration/R8/Fixtures/owned-delivery-2.json"})
        {
            var before=Input(path);var s=Spin(before);var r=s.expansion.ArcadeData.LastResourceReceipt;
            Need(before.coins+r.CoinIncome-r.ConstructionDebit==s.coins,"Gross/debit wallet bridge");Need(r.CoinIncome>=s.lastSpin.reward&&r.DiamondsAdded==0,"Income omitted construction effects/paid Diamond");
            Need(Raw(Codec.Decode(Raw(s)))==Raw(s),"Money event roundtrip");
        }
        var funded=Input("Checks/Milestones/fixtures/v2-selected540.json");funded.coins=535;GameLogic.Validate(funded);var paid=Spin(funded);
        Need(paid.expansion.ArcadeData.LastResourceReceipt.ConstructionDebit==540,"Frozen selected540 construction debit repriced");
        var game=Open(Stored(Synthetic()));
        foreach(var command in new[]{new[]{"buy","module.salvage"},new[]{"equip","module.salvage"},new[]{"buy","theme.amber"},new[]{"theme","theme.amber"},new[]{"speed","speed.fast"},new[]{"preset","preset.parts"}})
        {var q=game.GetExpansionAction(command[0],command[1],command[0]=="equip"?1:0);Need(q.RuleRevision=="expansion.rules.v3","Old-feature quote ignored full V3");game.ExecuteExpansion(q);}
        Need(game.Expansion.OwnsModule("module.salvage")&&game.Expansion.ThemeId=="theme.amber"&&game.Expansion.SpeedId=="speed.fast","Old-feature V3 routes failed");
        var stale=game.GetExpansionOffer("theme.mint");Do(game,"start",ArcadeCatalog.Memory);var st=Stored(game.State);var reopened=Open(st);NoIo(st,reopened,()=>reopened.BuyExpansion(stale));
        Export("v3-old-feature-commands",game.State);Export("selected540-spin",paid);
    }
    static void OverflowAndMalformed()
    {
        var ready=ReadySorting(Spins(8),8);var rich=Synthetic(ready,long.MaxValue);var st=Stored(rich);var g=Open(st);NoIo(st,g,()=>g.GetArcadeQuote("claim",g.Arcade.ActiveRun.Id,g.Arcade.Sequence,g.Arcade.ActiveRun.InstanceSequence));Need(g.Arcade.ActiveRun.Ready,"Overflow lost Ready");
        var zero=ReadySorting(Spins(8),0);zero=Synthetic(zero,long.MaxValue);zero=Move(zero,"claim");Need(ArcadeEconomy.Project(zero).Diamonds==long.MaxValue&&ArcadeEconomy.Project(zero).LastClaim.Diamonds==0,"Zero claim at numeric max");
        var state=Start(Spins(16),ArcadeCatalog.Memory);string raw=Raw(state),token=ExpansionCodec.EncodeState(state.expansion);
        Reject(()=>ExpansionCodec.DecodeState(token.Replace("expansion.state.v3","expansion.state.v2")));Reject(()=>ExpansionCodec.DecodeState(token.Substring(0,token.LastIndexOf('|'))));
        Reject(()=>ExpansionCodec.DecodeState(token.Substring(0,token.LastIndexOf('|')+1)+"-"));Reject(()=>ExpansionCodec.DecodeState(token+"|extra"));
        var stripped=state.Copy();stripped.expansion=null;Reject(()=>GameLogic.Validate(stripped));
        stripped=state.Copy();stripped.lastSpin.expansion=null;Reject(()=>GameLogic.Validate(stripped));
        var q=ArcadeEconomy.Quote(state,"pick",ArcadeCatalog.Memory,state.expansion.ArcadeData.Run.Instance,0);var p=ArcadeEconomy.Reserve(state,q);string op=ExpansionCodec.EncodeOperation(p.pending.expansion);
        Reject(()=>ExpansionCodec.DecodeOperation(op.Replace("expansion.rules.v3","expansion.rules.v2")));stripped=p.Copy();stripped.pending.expansion=null;Reject(()=>GameLogic.Validate(stripped));
        string[] f=Encoding.UTF8.GetString(Convert.FromBase64String(ArcadeCodec.Encode(state.expansion.ArcadeData))).Split(';');
        foreach(int field in new[]{8,9,11,12}){var copy=(string[])f.Clone();copy[field]="-";Reject(()=>ArcadeCodec.Decode(Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";",copy)))));}
        var board=(string[])f.Clone();board[8]=board[8].Replace("122001","012201");Reject(()=>ArcadeCodec.Decode(Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";",board)))));
        Reject(()=>ArcadeCodec.Decode(new string('A',3073)));Reject(()=>ExpansionCodec.DecodeState(new string('A',4097)));Reject(()=>ExpansionCodec.DecodeOperation(new string('A',12289)));
        Need(Raw(state)==raw,"Malformed checks changed source fixture");
    }
    static void Atomic()
    {
        var start=Spins(16);var first=Move(Start(start,ArcadeCatalog.Memory),"pick",0);var mismatch=Move(first,"pick",1);
        var ready=ReadySorting(Spins(16),8);var zero=ReadySorting(Spins(16),0);var shop=Synthetic();
        var cases=new[]{(start,"start",ArcadeCatalog.Memory,-1),(first,"pick",ArcadeCatalog.Memory,1),(mismatch,"continue",ArcadeCatalog.Memory,-1),
            (ready,"claim",ArcadeCatalog.Sorting,-1),(zero,"claim",ArcadeCatalog.Sorting,-1),(mismatch,"abandon",ArcadeCatalog.Memory,-1),(shop,"buy",ArcadeCatalog.PartsPack,-1)};
        foreach(var test in cases)
        {
            var run=ArcadeEconomy.Project(test.Item1).ActiveRun;long instance=test.Item2=="start"||test.Item2=="buy"?0:run.InstanceSequence;
            var q=ArcadeEconomy.Quote(test.Item1,test.Item2,test.Item3,instance,test.Item4);var pending=ArcadeEconomy.Reserve(test.Item1,q);string expected=Raw(GameLogic.ApplyPending(pending)),before=Raw(test.Item1);
            Need(Raw(Codec.Decode(Raw(pending)))==Raw(pending),"Arcade pending codec");
            foreach(int phase in new[]{1,2})foreach(bool lands in new[]{false,true})
            {
                var storage=Stored(test.Item1);var game=Open(storage);storage.FailAt=phase;storage.Lands=lands;
                try{game.ExecuteArcade(q);throw new Exception("Expected injected fault");}catch(IOException){}
                Need(game.RecoveryRequired,"Write uncertainty unblocked");Reject(()=>{var view=game.Arcade;});Reject(()=>game.GetArcadeQuote(q.Action,q.Id,q.ExpectedSequence,q.ExpectedInstance,q.Target));
                storage.FailAt=-1;int writes=storage.Writes;game=Open(storage);bool committed=phase==2||lands;
                Need(storage.Primary==(committed?expected:before),"Arcade split/rerolled/double-paid after recovery: "+test.Item2);
                Need(storage.Writes-writes==(phase==1&&!lands||phase==2&&lands?0:1),"Recovery write count");string raw=storage.Primary;writes=storage.Writes;Open(storage);Need(raw==storage.Primary&&writes==storage.Writes,"Repeated recovery");
                if(committed){game.ExecuteArcade(q);Need(raw==storage.Primary&&writes==storage.Writes,"Recovered callback paid/wrote");}
            }
            if(test.Item2=="claim"&&q.DiamondsCredit==3){Export("claim-pending",pending);Export("claim-committed",GameLogic.ApplyPending(pending));}
        }
        var spinBase=Slot(Spins(24),3);var spinPending=GameLogic.Reserve(spinBase,"spin",0,spinBase.sequence+1);string spinExpected=Raw(GameLogic.ApplyPending(spinPending));
        foreach(int phase in new[]{1,2})foreach(bool lands in new[]{false,true})
        {
            var storage=Stored(spinBase);var g=Open(storage);storage.FailAt=phase;storage.Lands=lands;try{g.ExecuteSpin(spinBase.sequence+1);throw new Exception("Expected spin fault");}catch(IOException){}
            Need(g.RecoveryRequired,"V3 spin uncertainty unblocked");storage.FailAt=-1;g=Open(storage);Need(storage.Primary==(phase==1&&!lands?Raw(spinBase):spinExpected),"V3 spin recovery bridge changed");
        }
    }
    static void PublicBoundaryAndBounds()
    {
        foreach(Type type in new[]{typeof(ArcadeView),typeof(ArcadeRunView),typeof(ArcadeQuote),typeof(ArcadeClaimView)})
        foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance))
        {Need(property.SetMethod==null||!property.SetMethod.IsPublic,"Mutable public Arcade property");Need(!property.Name.Contains("Seed")&&!property.Name.Contains("Board")&&property.Name!="After"&&property.Name!="Transcript","Hidden challenge leaked");}
        var state=Start(Spins(24),ArcadeCatalog.Memory);var view=ArcadeEconomy.Project(state);var list=view.ActiveRun.MemoryFaces as IList<int>;Reject(()=>list[0]=99);Need(ArcadeEconomy.Project(state).ActiveRun.MemoryFaces.All(x=>x==-1),"Masked array alias");
        int largestState=0,largestOperation=0,largestJson=0;
        foreach(string file in Directory.GetFiles(Output,"*.json"))
        {var s=Codec.Decode(File.ReadAllText(file));largestJson=Math.Max(largestJson,Raw(s).Length);if(s.expansion!=null)largestState=Math.Max(largestState,ExpansionCodec.EncodeState(s.expansion).Length);foreach(var op in new[]{s.pending?.expansion,s.lastSpin?.expansion})if(op!=null)largestOperation=Math.Max(largestOperation,ExpansionCodec.EncodeOperation(op).Length);}
        Need(largestState<4096&&largestOperation<12288&&largestJson<65536,"Existing codec bounds exceeded");
        File.WriteAllText(Path.Combine(Output,"sizes.txt"),"Largest exported canonical state="+largestState+"; operation="+largestOperation+"; JSON="+largestJson+". This measures scoped fixtures, not all theoretical states.\n");
    }
    static int Main(string[] args)
    {
        Repository=Path.GetFullPath(args[0]);Output=Path.GetFullPath(args[1]);Directory.CreateDirectory(Output);
        var groups=new Dictionary<string,Action>{{"old-protocol-compatibility",Compatibility},{"future-tickets",Tickets},{"memory-phases-and-reward-bands",Memory},{"sorting-literal-bands",Sorting},{"claims-stale-and-abandon",ClaimsAndStale},{"offers-shared-boost-headroom",OffersAndBoost},{"slot-attribute-projection-production-caps",SlotAttributeProjection},{"single-wallet-attribute-arithmetic",ResourceArithmetic},{"gross-income-debit-old-v3-commands",MoneyAndOldFeatures},{"overflow-malformed-stripped",OverflowAndMalformed},{"atomic-write-recovery",Atomic},{"masked-public-boundary-and-bounds",PublicBoundaryAndBounds}};
        foreach(var item in groups){try{item.Value();Passed.Add(item.Key);Console.WriteLine("PASS "+item.Key);}catch(Exception e){Console.WriteLine("FAIL "+item.Key+": "+e);return 1;}}
        File.WriteAllText(Path.Combine(Output,"acceptance.txt"),string.Join("\n",Passed.Select(x=>"PASS "+x))+"\n");Console.WriteLine("PASS "+Passed.Count+" bounded Arcade producer groups; no Editor/runtime/device acceptance.");return 0;
    }
}
