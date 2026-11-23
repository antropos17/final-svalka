using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JunkToNeon.Core;

static class NextExpansionChecks
{
    static string Repository,Output;
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    sealed class Storage:ISnapshotStorage
    {
        internal string Primary,Backup;internal int Reads,Writes,FailAt=-1;internal bool Lands;
        public string ReadPrimary(){Reads++;return Primary;}public string ReadBackup(){Reads++;return Backup;}
        public void Write(string raw){Writes++;if(Writes==FailAt&&!Lands)throw new IOException("Private next-write before fault");Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("Private next-write after fault");}
    }
    static void Need(bool value,string why){if(!value)throw new Exception(why);}
    static void Reject(Action action)
    {try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}catch(FormatException){return;}catch(OverflowException){return;}throw new Exception("Expected next-expansion refusal");}
    static Storage Stored(ModelState state){return new Storage{Primary=Codec.Encode(state)};}
    static GameSession Open(Storage storage){var game=GameSession.Open(new GameStore(storage,Codec),1);Need(!game.RecoveryRequired,"Private open failed: "+game.Error);return game;}
    static string FixtureRaw(string name){return File.ReadAllText(Path.Combine(Repository,"Checks/ExpansionCore/Fixtures/v1-"+name+".json"));}
    static ModelState Rich(){return Codec.Decode(FixtureRaw("rich-owned-loadout"));}
    static ModelState Command(ModelState state,string action,string id)
    {return GameLogic.ApplyPending(ExpansionEconomy.Reserve(state,ExpansionEconomy.Quote(state,action,id)));}
    static ModelState Bench(int level)
    {
        var state=Command(Rich(),"buy","bonus.unlock");
        for(int i=1;i<=level;i++)state=Command(state,"buy","workbench.bonus."+i);
        return state;
    }
    static void NoIo(Storage storage,GameSession game,Action action)
    {
        string raw=storage.Primary,backup=storage.Backup,state=Codec.Encode(game.State);int reads=storage.Reads,writes=storage.Writes;
        Reject(action);Need(raw==storage.Primary&&backup==storage.Backup&&reads==storage.Reads&&writes==storage.Writes&&state==Codec.Encode(game.State)&&!game.RecoveryRequired,"Refusal changed state/IO/health");
    }
    static void Export(string name,ModelState state)
    {
        string raw=Codec.Encode(state);Need(Codec.Encode(Codec.Decode(raw))==raw,"Next fixture roundtrip mismatch");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,name+".json"),raw);
    }
    static void V1()
    {
        var hashes=new Dictionary<string,string>{
            {"rich-owned-loadout","10994BD4F7866C4CDBE13F8C3703396D3698259FE02A9B8E869DF8C199B53C49"},
            {"trade535-saved540-pending","99527DCDEA403D935D32F5082CF0E56DD7BEB4DD34FAA4E44D609A7F3B452A44"},
            {"trade555-saved540-committed","12D7A9C0F0E180BBD7A937B797DCAFFA23BDD9855CC5E0089D0F2BB8C1D61CD6"},
            {"boosted-bonus-pending","6583DC986EE19B626C734043947A8BB423303D481C9B256B595009F79D2EA63B"},
            {"boosted-bonus-committed","CEFF3317536DF4B947FBC37C450FDC63E8DD7E2C27698F765D31BD14BD013125"},
            {"equipment-workshop-pending","A6624EA70AF7AAB38E09A74FEE59713930814185ADBDED7114AA217767E7D7F4"},
            {"equipment-workshop-committed","A0D90C43F16CFF61D53A114662DDC620D9A2B67C492C77C2BC310C37AE2E1367"}};
        foreach(var item in hashes)
        {
            string raw=FixtureRaw(item.Key);using(var sha=SHA256.Create())Need(Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw)))==item.Value,"Frozen V1 fixture changed");
            var state=Codec.Decode(raw);Need(Codec.Encode(state)==raw&&state.expansion.FormatVersion==1&&state.expansion.BonusWorkbenchLevel==0&&state.expansion.BonusFocusId=="focus.balanced","V1 canonical bytes/defaults changed");
            if(state.pending!=null)
            {
                var p=state.pending.expansion;Need(p.RuleRevision=="expansion.rules.v1"&&ExpansionCodec.EncodeOperation(p).StartsWith("expansion.op.v1~expansion.rules.v1~"),"Old pending revision changed");
                string committedName=item.Key.Replace("trade535","trade555").Replace("pending","committed");
                var result=GameLogic.ApplyPending(state);Need(Codec.Encode(result)==FixtureRaw(committedName),"Frozen original pending result changed");
                var storage=Stored(state);var game=Open(storage);Need(storage.Writes==1&&Codec.Encode(game.State)==FixtureRaw(committedName),"V1 storage recovery changed");
                Open(storage);Need(storage.Writes==1,"V1 recovery repeated");
            }
        }
        var rich=Rich();var q=ExpansionEconomy.Quote(rich,"buy","theme.mint");Need(q.RuleRevision=="expansion.rules.v1","All-old command globally upgraded");
        var after=GameLogic.ApplyPending(ExpansionEconomy.Reserve(rich,q));Need(after.expansion.FormatVersion==1,"Old command rewrote V1 format");
        var spin=GameLogic.ApplyPending(GameLogic.Reserve(rich,"spin",0,rich.sequence+1));Need(spin.expansion.FormatVersion==3&&spin.lastSpin.expansion.RuleRevision=="expansion.rules.v3"&&ArcadeEconomy.Project(spin).TicketProgress==1,"New foreground spin must explicitly adopt V3 and count only itself");
        string[] original={"module.salvage:12:0","module.packing:12:0","module.harvest:8:8","module.combo:20:10","module.dynamo:12:8","socket.2:30:0","socket.3:60:30","theme.amber:15:5","theme.mint:15:5","theme.violet:15:5","bonus.unlock:20:10","potato.yield.1:15:15","potato.yield.2:30:30","trade.parts:20:0","trade.potatoes:0:20",
            "equipment.house.1:20:10","equipment.house.2:40:20","equipment.workshop.1:25:15","equipment.workshop.2:40:20","equipment.recycler.1:20:10","equipment.recycler.2:40:20","equipment.pigeon-loft.1:20:10","equipment.pigeon-loft.2:40:20","equipment.cart.1:20:10","equipment.cart.2:40:20","equipment.power-source.1:20:10","equipment.power-source.2:40:20"};
        Need(ExpansionCatalog.Offers.Count==31&&ExpansionCatalog.Offers.Take(27).Select(o=>o.Id+":"+o.PartsCost+":"+o.PotatoesCost).SequenceEqual(original),"Original27 offers/order/prices changed");
    }
    static void Workbench()
    {
        var storage=Stored(Rich());var game=Open(storage);var initial=game.State;
        NoIo(storage,game,()=>game.GetExpansionOffer("workbench.bonus.1"));NoIo(storage,game,()=>game.GetExpansionAction("focus","focus.parts"));
        game.BuyExpansion(game.GetExpansionOffer("bonus.unlock"));NoIo(storage,game,()=>game.GetExpansionOffer("workbench.bonus.2"));
        var before=game.State;var q=game.GetExpansionOffer("workbench.bonus.1");Need(q.PartsCost==30&&q.PotatoesCost==20&&q.RuleRevision=="expansion.rules.v2","WorkbenchI quote drift");
        var pending=ExpansionEconomy.Reserve(before,q);Need(pending.pending.expansion.Before.FormatVersion==1&&pending.pending.expansion.After.FormatVersion==2,"Explicit mixed upgrade missing");Export("workbench1-pending",pending);
        game.BuyExpansion(q);Need(game.Expansion.BonusWorkbenchLevel==1&&game.Expansion.FormatVersion==2&&game.Expansion.Parts==150&&game.Expansion.Potatoes==170,"WorkbenchI cost/level drift");
        Need(game.State.coins==initial.coins&&game.State.seed==initial.seed&&ExpansionCodec.EncodeOperation(game.State.lastSpin.expansion)==ExpansionCodec.EncodeOperation(initial.lastSpin.expansion),"Workbench rewrote old spin/wallet/RNG");
        string raw=storage.Primary;int writes=storage.Writes;game.SelectExpansionFocus("focus.balanced",game.State.sequence);Need(raw==storage.Primary&&writes==storage.Writes,"Same focus wrote");
        var stale=game.GetExpansionAction("focus","focus.parts");game.SelectExpansionFocus("focus.potatoes",game.State.sequence);NoIo(storage,game,()=>game.ExecuteExpansion(stale));
        var q2=game.GetExpansionOffer("workbench.bonus.2");game.BuyExpansion(q2);Need(game.Expansion.BonusWorkbenchLevel==2&&game.Expansion.Parts==110&&game.Expansion.Potatoes==140&&game.Expansion.BonusFocusId=="focus.potatoes","WorkbenchII cost/retained focus drift");
        NoIo(storage,game,()=>game.GetExpansionOffer("workbench.bonus.2"));
        Need(game.Expansion.LastResourceReceipt.Sequence==1&&game.Expansion.BonusRolls==0&&game.Expansion.BonusProgress==0,"Command granted retroactive resource/bonus");
        var oldIdQuote=game.GetExpansionOffer("trade.parts");Need(oldIdQuote.RuleRevision=="expansion.rules.v2","V2 context mislabeled old rule");
        Export("workbench2-potatoes",game.State);
    }
    static ExpansionState ArithmeticState(uint seed,int level,string focus,bool boost,int parts=0,int potatoes=0)
    {
        return new ExpansionDraft {FormatVersion=2,AdoptedSequence=1,ForegroundSpins=4,BonusUnlocked=true,BonusWorkbenchLevel=level,BonusFocusId=focus,BonusSeed=seed,
            BonusProgress=4,BoostArmed=boost,Parts=parts,Potatoes=potatoes,LastResourceReceipt=new ExpansionResourceReceipt(4,0,0,1,-1,0,0,false)}.Freeze();
    }
    static void Arithmetic()
    {
        uint[] seeds={285214720U,1426073600U,2566932480U,3707791360U},afters={268435456U,1342177280U,2415919104U,3489660928U};
        string[] focuses={"focus.balanced","focus.parts","focus.potatoes"};
        // Literal independent pairs indexed by level,focus,symbol; no production bonus table oracle.
        int[][][] expected={
            new[]{new[]{5,0,0,5,3,3,8,2},new[]{5,0,5,0,6,0,10,0},new[]{0,5,0,5,0,6,0,10}},
            new[]{new[]{6,1,1,6,4,4,9,3},new[]{7,0,7,0,8,0,12,0},new[]{0,7,0,7,0,8,0,12}}};
        for(int level=1;level<=2;level++)for(int focus=0;focus<3;focus++)for(int symbol=0;symbol<4;symbol++)foreach(bool boost in new[]{false,true})
        {
            var before=ArithmeticState(seeds[symbol],level,focuses[focus],boost);var after=ExpansionEconomy.Observe(before,5,new[]{3,3,3});int mult=boost?2:1;
            Need(after.LastResourceReceipt.BonusParts==expected[level-1][focus][symbol*2]*mult&&after.LastResourceReceipt.BonusPotatoes==expected[level-1][focus][symbol*2+1]*mult,"Independent focus/level/boost arithmetic changed");
            Need(after.BonusSeed==afters[symbol]&&after.LastResourceReceipt.BonusSymbol==symbol&&after.BonusRolls==1&&after.BonusProgress==0&&!after.BoostArmed,"Focus changed bonus RNG/cadence/consumption");
        }
        var capped=ExpansionEconomy.Observe(ArithmeticState(seeds[0],2,"focus.parts",true,198,100),5,new[]{0,1,2});
        Need(capped.Parts==200&&capped.Potatoes==101&&capped.LastResourceReceipt.BonusParts==0&&capped.LastResourceReceipt.BonusPotatoes==0&&!capped.BoostArmed,"Focused overflow redirected or ignored base-first caps");
        var split=ExpansionEconomy.Observe(ArithmeticState(seeds[0],2,"focus.balanced",true,198,199),5,new[]{3,3,3});
        Need(split.LastResourceReceipt.BonusParts==2&&split.LastResourceReceipt.BonusPotatoes==1,"Independent partial caps changed");
    }
    static void Presets()
    {
        var rich=Rich();var preview=ExpansionEconomy.PreviewPreset(rich.expansion,"preset.parts");Need(preview.SequenceEqual(new[]{"module.salvage","module.packing","module.combo"}),"Parts preview priority drift");
        bool immutable=false;try{((IList<string>)preview)[0]="module.dynamo";}catch(NotSupportedException){immutable=true;}Need(immutable&&rich.expansion.ModuleAt(1)=="module.combo","Preview mutates frozen/current slots");
        var storage=Stored(rich);var game=Open(storage);var q=game.GetExpansionAction("preset","preset.parts");game.ExecuteExpansion(q);
        Need(Enumerable.Range(1,3).Select(game.Expansion.ModuleAt).SequenceEqual(preview)&&game.Expansion.FormatVersion==2&&game.State.coins==rich.coins&&game.Expansion.Parts==200&&game.Expansion.Potatoes==200&&game.Expansion.Energy==20&&storage.Writes==2,"Preset not atomic/free/exact");
        string raw=storage.Primary;int writes=storage.Writes;game.ApplyExpansionPreset("preset.parts",game.State.sequence);game.ExecuteExpansion(q);Need(raw==storage.Primary&&writes==storage.Writes,"Same/replayed preset wrote");
        var stale=game.GetExpansionAction("preset","preset.energy");game.ApplyExpansionPreset("preset.harvest",game.State.sequence);NoIo(storage,game,()=>game.ExecuteExpansion(stale));
        Need(Enumerable.Range(1,3).Select(game.Expansion.ModuleAt).SequenceEqual(new[]{"module.harvest","module.combo","module.dynamo"}),"Harvest priority drift");
        game.ApplyExpansionPreset("preset.energy",game.State.sequence);Need(Enumerable.Range(1,3).Select(game.Expansion.ModuleAt).SequenceEqual(new[]{"module.dynamo","module.combo","module.salvage"}),"Energy priority drift");
        var subset=new ExpansionDraft(rich.expansion){ModuleMask=9,SocketCount=2};subset.Modules=new[]{"module.salvage","module.combo",null};
        Need(ExpansionEconomy.PreviewPreset(subset.Freeze(),"preset.energy").SequenceEqual(new[]{"module.combo","module.salvage",null}),"Preset equipped unowned module/locked slot");
        var emptyStorage=Stored(ModelState.Fresh(1));var empty=Open(emptyStorage);foreach(string id in new[]{"preset.parts","preset.harvest","preset.energy"})empty.ApplyExpansionPreset(id,0);
        Need(emptyStorage.Writes==0&&empty.State.expansion==null&&empty.State.sequence==0,"Empty preset adopted/wrote history");
        NoIo(storage,game,()=>game.ApplyExpansionPreset("preset.parts",-1));Export("preset-energy",game.State);
    }
    static void Themes()
    {
        var storage=Stored(Rich());var game=Open(storage);var before=game.State;
        NoIo(storage,game,()=>game.SelectExpansionTheme("theme.frost",game.State.sequence));
        game.BuyExpansion(game.GetExpansionOffer("theme.frost"));game.SelectExpansionTheme("theme.frost",game.State.sequence);
        Need(game.Expansion.OwnsTheme("theme.frost")&&game.Expansion.ThemeId=="theme.frost"&&game.Expansion.Parts==185&&game.Expansion.Potatoes==195,"Frost price/ownership/selection drift");
        game.BuyExpansion(game.GetExpansionOffer("theme.rose"));game.SelectExpansionTheme("theme.rose",game.State.sequence);
        Need(game.Expansion.OwnsTheme("theme.rose")&&game.Expansion.ThemeId=="theme.rose"&&game.Expansion.Parts==170&&game.Expansion.Potatoes==190&&game.Expansion.Energy==before.expansion.Energy&&game.State.coins==before.coins&&game.State.seed==before.seed,"Rose economics drift");
        Need(Codec.Encode(Codec.Decode(storage.Primary))==storage.Primary&&game.Expansion.FormatVersion==2,"New themes codec lost bits/version");Export("rose-owned",game.State);
    }
    static ModelState BonusReady()
    {
        var state=Command(Bench(2),"focus","focus.potatoes");state=Command(state,"boost","boost.next-bonus");
        for(int i=0;i<4;i++)state=GameLogic.ApplyPending(JourneyEconomy.ReserveChapter(state,"spin",null,state.sequence+1,manualSpin:true));
        Need(state.expansion.BonusProgress==4&&state.expansion.BoostArmed,"V2 next-bonus fixture drift");return state;
    }
    static void Atomic()
    {
        foreach(string kind in new[]{"purchase","focus","preset","bonus"})
        {
            var original=kind=="purchase"?Bench(0):kind=="focus"?Bench(1):kind=="preset"?Rich():BonusReady();
            var quote=kind=="bonus"?null:ExpansionEconomy.Quote(original,kind=="purchase"?"buy":kind=="focus"?"focus":"preset",kind=="purchase"?"workbench.bonus.1":kind=="focus"?"focus.potatoes":"preset.parts");
            var pending=kind=="bonus"?JourneyEconomy.ReserveChapter(original,"spin",null,original.sequence+1,manualSpin:true):ExpansionEconomy.Reserve(original,quote);
            var expected=GameLogic.ApplyPending(pending);string expectedRaw=Codec.Encode(expected),before=Codec.Encode(original);
            if(kind=="bonus")
            {
                Need(expected.lastSpin.expansion.RuleRevision=="expansion.rules.v3"&&expected.expansion.LastResourceReceipt.BonusParts==0&&expected.expansion.LastResourceReceipt.BonusPotatoes==14&&expected.expansion.BonusSeed==686983322U&&expected.coins==85&&expected.seed==3323190024U,"New V3 focused boosted bonus literal mismatch");
                var lower=GameLogic.ApplyPending(ExpansionEconomy.EconomicShadow(pending));Need(Codec.Encode(ExpansionEconomy.EconomicShadow(expected))==Codec.Encode(lower),"Workbench/focus changed primary transaction");
                Export("focused-boost-pending",pending);Export("focused-boost-committed",expected);
            }
            foreach(int phase in new[]{1,2})foreach(bool lands in new[]{false,true})
            {
                var storage=Stored(original);var game=Open(storage);storage.FailAt=phase;storage.Lands=lands;
                try{if(kind=="bonus")game.ExecuteChapterSpin(game.State.sequence+1);else game.ExecuteExpansion(quote);throw new Exception("Expected private write fault");}catch(IOException){}
                Need(game.RecoveryRequired,"Write uncertainty did not block");Reject(()=>{var x=game.Expansion;});storage.FailAt=-1;
                int writes=storage.Writes;game=Open(storage);bool committed=phase==2||lands;
                Need(storage.Primary==(committed?expectedRaw:before),"V2 recovery split/rerolled/repriced state");
                Need(storage.Writes-writes==(phase==1&&!lands||phase==2&&lands?0:1),"V2 recovery write count mismatch");
                string raw=storage.Primary;writes=storage.Writes;Open(storage);Need(raw==storage.Primary&&writes==storage.Writes,"V2 recovery repeated");
                if(committed&&quote!=null){game.ExecuteExpansion(quote);Need(raw==storage.Primary&&writes==storage.Writes,"Recovered V2 callback mutated");}
            }
        }
    }
    static void Invalid()
    {
        var v1=Rich().expansion;var newMask=new ExpansionDraft(v1){ThemeMask=19};Reject(()=>newMask.Freeze());
        var wrongBench=new ExpansionDraft(v1){BonusWorkbenchLevel=1,BonusUnlocked=true};Reject(()=>wrongBench.Freeze());
        var locked=new ExpansionDraft(v1){FormatVersion=2,BonusFocusId="focus.parts"};Reject(()=>locked.Freeze());
        var unknown=new ExpansionDraft(v1){FormatVersion=2,BonusUnlocked=true,BonusWorkbenchLevel=1,BonusFocusId="focus.unknown"};Reject(()=>unknown.Freeze());
        var state=Command(Rich(),"preset","preset.parts");var newer=Command(state,"buy","theme.mint");
        var q=ExpansionEconomy.Quote(newer,"preset","preset.energy");var pending=ExpansionEconomy.Reserve(newer,q);string token=ExpansionCodec.EncodeOperation(pending.pending.expansion);
        Reject(()=>ExpansionCodec.DecodeOperation(token.Replace("expansion.rules.v2","expansion.rules.v1")));
        var downgraded=Codec.Decode(Codec.Encode(newer));downgraded=GameLogic.ApplyPending(GameLogic.Reserve(downgraded,"spin",0,downgraded.sequence+1));
        var loss=new ExpansionDraft(downgraded.expansion){FormatVersion=1};Reject(()=>{downgraded.expansion=loss.Freeze();GameLogic.Validate(downgraded);});
        var storage=Stored(newer);var game=Open(storage);NoIo(storage,game,()=>game.GetExpansionAction("preset","preset.unknown"));
        NoIo(storage,game,()=>game.GetExpansionAction("preset","preset.parts",1));NoIo(storage,game,()=>game.GetExpansionAction("focus","focus.parts",1));
        var stale=game.GetExpansionAction("preset","preset.energy");game.ExecuteChapterSpin(game.State.sequence+1);NoIo(storage,game,()=>game.ExecuteExpansion(stale));
        var detached=game.State;detached.coins=0;Need(game.State.coins!=0,"V2 snapshot mutation reached store");
    }
    internal static IEnumerable<KeyValuePair<string,Action>> Groups(string repository,string output=null)
    {
        Repository=repository;Output=output??Path.Combine(Repository,".work/next-expansion-core/fixtures");return new Dictionary<string,Action>{{"next-v1",V1},{"next-workbench",Workbench},{"next-arithmetic",Arithmetic},{"next-presets",Presets},{"next-themes",Themes},{"next-atomic",Atomic},{"next-invalid",Invalid}};
    }
}
