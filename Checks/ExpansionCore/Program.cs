using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;

static class Program
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static string Repository,Output;
    sealed class Storage:ISnapshotStorage
    {
        public string Primary,Backup;public int Reads,Writes,FailAt=-1;public bool LandThenThrow;
        public string ReadPrimary(){Reads++;return Primary;}public string ReadBackup(){Reads++;return Backup;}
        public void Write(string raw)
        {
            Writes++;if(Writes==FailAt&&!LandThenThrow)throw new IOException("Injected private write-before fault");
            Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("Injected private write-after fault");
        }
    }
    static void Need(bool b,string why){if(!b)throw new Exception(why);}
    static void Reject(Action action)
    {
        try{action();}catch(InvalidOperationException){return;}catch(ArgumentException){return;}catch(FormatException){return;}catch(OverflowException){return;}
        throw new Exception("Expected refusal");
    }
    static ModelState Input(string path){return Codec.Decode(File.ReadAllText(Path.Combine(Repository,path)));}
    static Storage Stored(ModelState s){return new Storage{Primary=Codec.Encode(s)};}
    static GameSession Open(Storage storage)
    {var game=GameSession.Open(new GameStore(storage,Codec),1);Need(!game.RecoveryRequired,"Open failed: "+game.Error);return game;}
    static void NoIo(Storage storage,GameSession game,Action action)
    {
        string before=storage.Primary,backup=storage.Backup,state=Codec.Encode(game.State);int reads=storage.Reads,writes=storage.Writes;
        Reject(action);Need(before==storage.Primary&&backup==storage.Backup&&reads==storage.Reads&&writes==storage.Writes&&state==Codec.Encode(game.State)&&!game.RecoveryRequired,"Refusal changed state/IO/health");
    }
    static ModelState Spin(ModelState s){return GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1));}
    static void Spin(GameSession game){game.ExecuteChapterSpin(game.State.sequence+1);}
    static ModelState Legacy600()
    {var s=ModelState.Fresh(1);s.coins=535;s.houseStage=1;s.autoHintSeen=1;s.selectedStage=2;GameLogic.Validate(s);return s;}
    static ModelState Funded(ModelState source=null)
    {
        // Deliberately synthetic resource balances for bounded semantic tests, not pacing.
        var s=source?.Copy()??Spin(ModelState.Fresh(1));
        if(s.expansion==null)
        {
            var q=ExpansionEconomy.Quote(s,"speed","speed.fast");s=GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,q));
        }
        var d=new ExpansionDraft(s.expansion){Parts=200,Potatoes=200,Energy=20};s.expansion=d.Freeze();GameLogic.Validate(s);return s;
    }
    static ModelState Complete(){return Input("Checks/OrderVariety/fixtures/potatoes-ready12.json");}
    static ModelState Command(ModelState s,string action,string id,int slot=0)
    {return GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,ExpansionEconomy.Quote(s,action,id,slot)));}
    static string Lower(ModelState s){return Codec.Encode(ExpansionEconomy.EconomicShadow(s));}
    static void Export(string name,ModelState s)
    {Directory.CreateDirectory(Output);string raw=Codec.Encode(s);Need(Codec.Encode(Codec.Decode(raw))==raw,"Export codec changed bytes");File.WriteAllText(Path.Combine(Output,name+".json"),raw);}
    static void OldCodecs()
    {
        foreach(string relative in new[]{"Checks/Milestones/fixtures/v2-selected540.json","Checks/EraMigration/R8/Fixtures/owned-delivery-2.json","Checks/EraMigration/R8/Fixtures/all-skills-owned.json","Checks/EraMigration/R8/Fixtures/pending-canonical-spin.json","Checks/EraMigration/R8/Fixtures/pending-universal-2.json"})
        {
            string raw=File.ReadAllText(Path.Combine(Repository,relative)).Trim();var s=Codec.Decode(raw);
            Need(s.expansion==null&&ExpansionEconomy.Project(s).Parts==0&&!ExpansionEconomy.Project(s).IsAdopted,"Old read invented resources/adoption");
            Need(Codec.Encode(s)==raw,"Old canonical codec bytes changed: "+relative);
            var storage=Stored(s);var game=Open(storage);
            Need(game.State.expansion==null&&game.Expansion.ForegroundSpins==0,"Old pending recovery granted expansion resources");
            if(s.pending!=null)
            {
                var expected=GameLogic.ApplyPending(s);Need(Codec.Encode(game.State)==Codec.Encode(expected)&&storage.Writes==1,"Old pending result/recovery changed");
                string committed=storage.Primary;Open(storage);Need(storage.Primary==committed&&storage.Writes==1,"Old pending repeated");
                Spin(game);Need(game.Expansion.IsAdopted&&game.Expansion.ForegroundSpins==1,"Next genuinely new spin failed adoption");
            }
        }
        var v1=Legacy600();string old=Codec.Encode(v1);var shop=Open(Stored(v1));
        Need(shop.Expansion.Parts==0&&shop.Expansion.SocketCount==1&&shop.Expansion.SpeedId=="speed.normal"&&Codec.Encode(shop.State)==old,"Legacy query mutated");
        Reject(()=>shop.GetExpansionOffer("module.salvage"));Need(Codec.Encode(shop.State)==old,"Unaffordable quote adopted");
    }
    static void Adoption()
    {
        var s=ModelState.Fresh(1);var control=s.Copy();int[][] trace={new[]{0,0,2},new[]{0,2,0},new[]{0,0,1},new[]{2,2,1},new[]{2,0,0},new[]{3,0,3},new[]{0,3,2},new[]{3,1,3},new[]{0,1,2},new[]{0,0,2}};
        int[] parts={1,2,3,6,7,7,8,9,11,12},potatoes={2,4,6,6,8,9,10,10,11,13};
        for(int i=0;i<10;i++)
        {
            s=Spin(s);control=GameLogic.ApplyPending(GameLogic.ReserveHistorical(control,"spin",0,control.sequence+1));
            Need(s.lastSpin.symbols.SequenceEqual(trace[i])&&s.seed==control.seed&&s.coins==control.coins&&Lower(s)==Codec.Encode(control),"Three-symbol RNG/payout/lower transaction changed");
            Need(s.expansion.Parts==parts[i]&&s.expansion.Potatoes==potatoes[i]&&s.expansion.Energy==i+1&&s.expansion.ForegroundSpins==i+1,"Literal seed1 resources changed");
            Need(s.expansion.AdoptedSequence==1&&s.lastSpin.expansion.Sequence==s.sequence,"Atomic adoption/receipt identity mismatch");
            s=Codec.Decode(Codec.Encode(s));
        }
        Need(s.expansion.ModuleAt(1)==null&&s.expansion.ThemeId=="theme.neon"&&s.expansion.BonusRolls==0,"Implicit purchase/equip/bonus");
        Export("future-ten-spins",s);
    }
    static void ShopLoadout()
    {
        var storage=Stored(Funded());var game=Open(storage);long coins=game.State.coins;
        string[] ids={"module.salvage","module.packing","module.harvest","module.combo","module.dynamo"};
        int[] parts={188,176,168,148,136},potatoes={200,200,192,182,174};
        for(int i=0;i<ids.Length;i++)
        {
            var q=game.GetExpansionOffer(ids[i]);Need(q.CoinsCost==0&&q.ExpectedSequence==game.State.sequence,"Quote identity/currency drift");game.BuyExpansion(q);
            Need(game.Expansion.OwnsModule(ids[i])&&game.Expansion.Parts==parts[i]&&game.Expansion.Potatoes==potatoes[i]&&game.State.coins==coins&&game.Expansion.ModuleAt(1)==null,"Ownership cost or implicit equip drift");
            string raw=storage.Primary;int writes=storage.Writes;game.BuyExpansion(q);Need(raw==storage.Primary&&writes==storage.Writes,"Exact retained buy replay wrote");
            NoIo(storage,game,()=>game.GetExpansionOffer(ids[i]));
        }
        NoIo(storage,game,()=>game.EquipExpansionModule(2,"module.salvage",game.State.sequence));
        game.BuyExpansion(game.GetExpansionOffer("socket.2"));game.BuyExpansion(game.GetExpansionOffer("socket.3"));
        Need(game.Expansion.SocketCount==3&&game.Expansion.Parts==46&&game.Expansion.Potatoes==144,"Socket costs drift");
        game.EquipExpansionModule(1,"module.combo",game.State.sequence);game.EquipExpansionModule(2,"module.dynamo",game.State.sequence);game.EquipExpansionModule(3,"module.harvest",game.State.sequence);
        NoIo(storage,game,()=>game.EquipExpansionModule(3,"module.combo",game.State.sequence));
        game.EquipExpansionModule(1,"module.none",game.State.sequence);game.EquipExpansionModule(3,"module.combo",game.State.sequence);
        Need(game.Expansion.ModuleAt(1)==null&&game.Expansion.ModuleAt(3)=="module.combo","Explicit clear/equip failed");
        game.BuyExpansion(game.GetExpansionOffer("theme.amber"));game.SelectExpansionTheme("theme.amber",game.State.sequence);
        Need(game.Expansion.ThemeId=="theme.amber"&&game.Expansion.Parts==31&&game.Expansion.Potatoes==139,"Theme ownership/price/selection drift");
        NoIo(storage,game,()=>game.SelectExpansionTheme("theme.violet",game.State.sequence));
        NoIo(storage,game,()=>game.EquipExpansionModule(4,"module.salvage",game.State.sequence));
        var stale=game.GetExpansionOffer("theme.mint");Spin(game);NoIo(storage,game,()=>game.BuyExpansion(stale));
        Export("five-modules-three-sockets",game.State);
    }
    static void Equipment()
    {
        var lockedStorage=Stored(Funded());var locked=Open(lockedStorage);
        foreach(string family in FamilyIds.All)NoIo(lockedStorage,locked,()=>locked.GetExpansionOffer("equipment."+family+".1"));
        var s=Funded(Complete());int completed=s.chapter.Progress.CompletedUpgradeIds.Count;string lower=Lower(s);
        foreach(string family in FamilyIds.All)
        {
            s=Funded(s);var game=Open(Stored(s));
            game.BuyExpansion(game.GetExpansionOffer("equipment."+family+".1"));Need(game.Expansion.Parts==(family=="workshop"?175:180)&&game.Expansion.Potatoes==(family=="workshop"?185:190)&&game.Expansion.EquipmentLevel(family)==1,"EquipmentI literal price/level");
            game.BuyExpansion(game.GetExpansionOffer("equipment."+family+".2"));Need(game.Expansion.Parts==(family=="workshop"?135:140)&&game.Expansion.Potatoes==(family=="workshop"?165:170)&&game.Expansion.EquipmentLevel(family)==2,"EquipmentII literal price/level");
            Need(game.Chapter.Progress.CompletedUpgradeIds.Count==completed&&completed==24&&HouseGoal.Complete(game.State),"Equipment entered chapter completion");s=game.State;
        }
        Need(s.expansion.PartsCap==300&&s.expansion.PotatoesCap==300&&s.expansion.EnergyCap==40,"Equipment caps drift");
        var outcome=ExpansionEconomy.Observe(s.expansion,s.sequence+1,new[]{0,1,2});
        Need(outcome.LastResourceReceipt.Parts==8&&outcome.LastResourceReceipt.Potatoes==1&&outcome.LastResourceReceipt.Energy==3,"Six-family equipment effects drift");
        s=Funded(s);s=Command(s,"buy","potato.yield.1");Need(s.expansion.Parts==185&&s.expansion.Potatoes==185,"YieldI price drift");
        s=Command(s,"buy","potato.yield.2");Need(s.expansion.Parts==155&&s.expansion.Potatoes==155&&s.expansion.PotatoYieldLevel==2,"YieldII price drift");
        var d=new ExpansionDraft(s.expansion){ModuleMask=4};d.Modules[0]="module.harvest";var enhanced=d.Freeze();
        var potato=ExpansionEconomy.Observe(enhanced,s.sequence+1,new[]{0,0,0});Need(potato.LastResourceReceipt.Potatoes==12,"Harvest/yield triple must give12");
        Export("all-equipment-yield",s);
    }
    static void ModuleMetrics()
    {
        var baseState=Funded();var d=new ExpansionDraft(baseState.expansion){Parts=0,Potatoes=0,Energy=0,ModuleMask=31,SocketCount=3};
        d.Modules[0]="module.combo";d.Modules[1]="module.dynamo";d.Modules[2]="module.salvage";
        var loadout=d.Freeze();
        string frozen=ExpansionCodec.EncodeState(loadout);
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(loadout)).SequenceEqual(new[]{1,0,0,1,1,1}),"Independent installed module coefficients");
        var prospective=new[]{"module.combo","module.dynamo","module.packing"};
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(loadout,prospective)).SequenceEqual(new[]{0,1,0,1,1,1}),"Trash and Box capabilities were collapsed");
        prospective[2]="module.harvest";
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(loadout,prospective)).SequenceEqual(new[]{0,0,1,1,1,1})&&ExpansionCodec.EncodeState(loadout)==frozen,"Projection mutated snapshot or mixed trigger classes");
        Reject(()=>ExpansionEconomy.ModuleBonuses(loadout,new[]{"module.combo","module.combo",(string)null}));
        Reject(()=>ExpansionEconomy.ModuleBonuses(loadout,new[]{"module.combo"}));
        var full=new ExpansionDraft(loadout){Parts=200,Potatoes=200,Energy=30};
        var unrelated=ExpansionEconomy.Observe(full.Freeze(),baseState.sequence+1,new[]{0,1,2});
        Need(unrelated.LastResourceReceipt.Parts==0&&unrelated.LastResourceReceipt.Potatoes==0&&unrelated.LastResourceReceipt.Energy==0&&ModuleVector(ExpansionEconomy.ModuleBonuses(unrelated)).SequenceEqual(new[]{1,0,0,1,1,1}),"Full stocks or unrelated spin erased structural capabilities");
        var triple=ExpansionEconomy.Observe(loadout,baseState.sequence+1,new[]{3,3,3});
        Need(triple.LastResourceReceipt.Parts==1&&triple.LastResourceReceipt.Potatoes==1&&triple.LastResourceReceipt.Energy==2,"Combo/dynamo triple must grant once, not once per matching pair/pigeon");
        var different=ExpansionEconomy.Observe(loadout,baseState.sequence+1,new[]{0,1,2});
        Need(different.LastResourceReceipt.Parts==3&&different.LastResourceReceipt.Potatoes==1&&different.LastResourceReceipt.Energy==1,"Combo triggered on distinct symbols or specialization drift");
        var pair=ExpansionEconomy.Observe(loadout,baseState.sequence+1,new[]{2,2,3});
        Need(pair.LastResourceReceipt.Parts==5&&pair.LastResourceReceipt.Potatoes==1&&pair.LastResourceReceipt.Energy==2,"Pair/pigeon specialization literal mismatch");
        var pack=new ExpansionDraft(loadout);pack.Modules[2]="module.packing";var box=ExpansionEconomy.Observe(pack.Freeze(),baseState.sequence+1,new[]{1,1,1});
        Need(box.LastResourceReceipt.Parts==7&&box.LastResourceReceipt.Potatoes==1&&box.LastResourceReceipt.Energy==1,"Packing triple must be6+combo1");
        var trash=ExpansionEconomy.Observe(loadout,baseState.sequence+1,new[]{2,2,2});
        Need(trash.LastResourceReceipt.Parts==7&&trash.LastResourceReceipt.Potatoes==1&&trash.LastResourceReceipt.Energy==1,"Salvage repeated symbols or Combo triple drift");
        var harvest=new ExpansionDraft(loadout);harvest.Modules[2]="module.harvest";
        var potatoes=ExpansionEconomy.Observe(harvest.Freeze(),baseState.sequence+1,new[]{0,0,0});
        Need(potatoes.LastResourceReceipt.Parts==1&&potatoes.LastResourceReceipt.Potatoes==7&&potatoes.LastResourceReceipt.Energy==1,"Harvest repeated symbols or Combo triple drift");
        var twoPigeons=ExpansionEconomy.Observe(loadout,baseState.sequence+1,new[]{3,3,0});
        Need(twoPigeons.LastResourceReceipt.Parts==1&&twoPigeons.LastResourceReceipt.Potatoes==2&&twoPigeons.LastResourceReceipt.Energy==2,"Dynamo must apply once to multiple Pigeons");
        var storage=Stored(Funded());var game=Open(storage);
        foreach(string id in new[]{"module.salvage","module.combo","socket.2"})game.BuyExpansion(game.GetExpansionOffer(id));
        game.EquipExpansionModule(1,"module.salvage",game.State.sequence);game.EquipExpansionModule(2,"module.combo",game.State.sequence);
        var before=game.State;var q=game.GetExpansionAction("preset","preset.energy");int writes=storage.Writes;
        game.ExecuteExpansion(q);
        Need(Enumerable.Range(1,3).Select(game.Expansion.ModuleAt).SequenceEqual(new[]{"module.combo","module.salvage",null})&&game.State.sequence==q.Sequence&&storage.Writes==writes+2,"Same-set rearrangement suppressed or not atomic");
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(game.Expansion)).SequenceEqual(new[]{1,0,0,1,1,0})&&game.Expansion.Parts==before.expansion.Parts&&game.Expansion.Potatoes==before.expansion.Potatoes&&game.Expansion.Energy==before.expansion.Energy,"Rearrangement changed effects/balances");
        string applied=storage.Primary;writes=storage.Writes;game.ApplyExpansionPreset("preset.energy",game.State.sequence);game.ExecuteExpansion(q);
        Need(storage.Primary==applied&&storage.Writes==writes,"Exact vector/replay wrote again");
        storage=Stored(Funded());game=Open(storage);
        foreach(string id in new[]{"module.salvage","module.harvest"})game.BuyExpansion(game.GetExpansionOffer(id));
        game.EquipExpansionModule(1,"module.harvest",game.State.sequence);var view=game.Expansion;var fallback=ExpansionEconomy.PreviewPreset(view,"preset.energy");
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(view)).SequenceEqual(new[]{0,0,1,0,0,0})&&ModuleVector(ExpansionEconomy.ModuleBonuses(view,fallback)).SequenceEqual(new[]{1,0,0,0,0,0}),"One-socket Energy fallback is not a gained Trash/lost Potato tradeoff");
        Reject(()=>ExpansionEconomy.ModuleBonuses(view,new[]{"module.dynamo",null,null}));Reject(()=>ExpansionEconomy.ModuleBonuses(view,new[]{"module.salvage","module.harvest",null}));
        game.ApplyExpansionPreset("preset.energy",game.State.sequence);
        Need(game.Expansion.ModuleAt(1)=="module.salvage"&&ModuleVector(ExpansionEconomy.ModuleBonuses(game.Expansion)).SequenceEqual(new[]{1,0,0,0,0,0}),"Applied fallback diverges from preview");
        game.EquipExpansionModule(1,"module.none",game.State.sequence);
        Need(ModuleVector(ExpansionEconomy.ModuleBonuses(game.Expansion)).SequenceEqual(new[]{0,0,0,0,0,0})&&game.Expansion.OwnsModule("module.salvage"),"Removal lost ownership or retained active contribution");
    }
    static int[] ModuleVector(ExpansionModuleBonuses b)=>new[]{b.PartsPerTrash,b.PartsPerBox,b.PotatoesPerPotato,b.PartsPerMatchingSpin,b.PotatoesPerMatchingSpin,b.EnergyPerPigeonSpin};
    static ModelState BonusReady()
    {
        var s=Funded();s=Command(s,"buy","bonus.unlock");s=Command(s,"boost","boost.next-bonus");
        for(int i=0;i<4;i++)s=Spin(s);
        Need(s.expansion.BonusProgress==4&&s.expansion.BoostArmed&&s.expansion.BonusRolls==0,"Prepared next bonus context drift");return s;
    }
    static void Bonus()
    {
        var boundary=Command(Funded(),"buy","bonus.unlock");boundary.expansion=new ExpansionDraft(boundary.expansion){Energy=14}.Freeze();
        var boundaryStorage=Stored(boundary);var boundaryGame=Open(boundaryStorage);
        NoIo(boundaryStorage,boundaryGame,()=>boundaryGame.ArmExpansionBoost(boundaryGame.State.sequence));
        var s=BonusReady();var game=Open(Stored(s));Need(game.Expansion.Energy==9,"Boost15 debit plus four foreground energy mismatch");
        var before=game.State;var pending=JourneyEconomy.ReserveChapter(before,"spin",null,before.sequence+1);var frozen=pending.pending.expansion;
        Need(frozen.After.LastResourceReceipt.BonusSymbol==0&&frozen.After.BonusSeed==686983322U&&frozen.After.BonusRolls==1&&frozen.After.LastResourceReceipt.BonusParts==10&&frozen.After.LastResourceReceipt.BonusPotatoes==0&&frozen.After.LastResourceReceipt.Boosted&&!frozen.After.BoostArmed,"Independent first bonus/boost outcome drift");
        var lower=GameLogic.ApplyPending(ExpansionEconomy.EconomicShadow(pending));var after=GameLogic.ApplyPending(pending);
        Need(Lower(after)==Codec.Encode(lower)&&after.seed==lower.seed,"Bonus consumed primary RNG/changed lower payout");
        Need(Codec.Encode(GameLogic.ApplyPending(after))==Codec.Encode(after),"Already committed pending replay granted twice");
        var unboosted=new ExpansionDraft(s.expansion){BoostArmed=false};s.expansion=unboosted.Freeze();var normal=Spin(s);
        Need(normal.expansion.LastResourceReceipt.BonusParts==5&&!normal.expansion.LastResourceReceipt.Boosted,"Unboosted first bonus reward drift");
        Export("boosted-bonus-pending",pending);Export("boosted-bonus-committed",after);
    }
    static void Trades()
    {
        foreach(var source in new[]{Legacy600(),Input("Checks/Milestones/fixtures/v2-selected540.json")})
        {
            var s=Funded(source);var storage=Stored(s);var game=Open(storage);long price=ChapterEconomy.SelectedProjectPrice(game.Chapter);var oldChapter=game.Chapter;string receipt=game.State.lastSpin==null?"-":Lower(game.State);
            var q=game.GetExpansionOffer("trade.parts");Need(q.PartsCost==20&&q.CoinsReward==20,"Trade quote literal mismatch");
            game.BuyExpansion(q);Need(game.State.coins==555&&game.Expansion.Parts==180&&game.Chapter.SelectedProjectId=="house.improve"&&ChapterEconomy.SelectedProjectPrice(game.Chapter)==price&&ChapterEconomy.Same(oldChapter,game.Chapter),"Trade bought/repriced old goal or wrong credit");
            var expectedLower=ExpansionEconomy.EconomicShadow(s);expectedLower.sequence=game.State.sequence;expectedLower.revision=game.State.revision;expectedLower.coins=555;
            Need(Lower(game.State)==Codec.Encode(expectedLower),"Trade changed receipts/selection/carry/guidance/RNG");
            string raw=storage.Primary;int writes=storage.Writes;game.BuyExpansion(q);Need(raw==storage.Primary&&writes==storage.Writes,"Trade replay paid twice");
            game.BuyExpansion(game.GetExpansionOffer("trade.potatoes"));Need(game.State.coins==575&&game.Expansion.Potatoes==180,"Potato exchange not useful/literal");
            NoIo(storage,game,()=>game.BuyExpansion(q));
            if(price==540)Export("trade555-saved540",Codec.Decode(raw));else Export("trade555-saved600",Codec.Decode(raw));
        }
        var full=Funded();full.coins=long.MaxValue-19;var st=Stored(full);var g=Open(st);NoIo(st,g,()=>g.GetExpansionOffer("trade.parts"));
    }
    static void WriteFailures(bool bonus)
    {
        var original=bonus?BonusReady():Funded(Legacy600());
        var quote=bonus?null:ExpansionEconomy.Quote(original,"buy","trade.parts");
        var expectedPending=bonus?JourneyEconomy.ReserveChapter(original,"spin",null,original.sequence+1,manualSpin:true):ExpansionEconomy.Reserve(original,quote);
        var expected=GameLogic.ApplyPending(expectedPending);string expectedRaw=Codec.Encode(expected),beforeRaw=Codec.Encode(original);
        foreach(int phase in new[]{1,2})foreach(bool lands in new[]{false,true})
        {
            var storage=Stored(original);var game=Open(storage);storage.FailAt=phase;storage.LandThenThrow=lands;
            try{if(bonus)Spin(game);else game.BuyExpansion(quote);throw new Exception("Injected write fault did not occur");}catch(IOException){}
            Need(game.RecoveryRequired,"Uncertain write failed to block");Reject(()=>{var x=game.Expansion;});
            storage.FailAt=-1;int beforeRecovery=storage.Writes;game=Open(storage);
            bool committed=phase==2||lands;
            Need(storage.Primary==(committed?expectedRaw:beforeRaw),"Frozen recovery changed/rerolled/partially applied wallet/resources");
            int expectedWrites=phase==1&&!lands||phase==2&&lands?0:1;
            Need(storage.Writes-beforeRecovery==expectedWrites,"Unexpected recovery write count");
            string recovered=storage.Primary;int writes=storage.Writes;Open(storage);Need(storage.Primary==recovered&&writes==storage.Writes,"Recovery repeated grant");
            if(committed&&!bonus){game.BuyExpansion(quote);Need(storage.Primary==recovered&&writes==storage.Writes,"Recovered trade replay paid again");}
            if(bonus)Need(game.Expansion.BoostArmed==!committed&&game.Expansion.BonusRolls==(committed?1:0),"Booster consumption split from frozen spin");
        }
    }
    static void Isolation()
    {
        var s=Funded(Complete());s=Command(s,"buy","bonus.unlock");s=Command(s,"boost","boost.next-bonus");
        var storage=Stored(s);var game=Open(storage);string expansion=ExpansionCodec.EncodeState(game.Expansion);uint seed=game.State.seed;
        game.Checkpoint(1000);game.SettleOffline(1600);
        Need(game.State.lastSettlement.coins==10&&ExpansionCodec.EncodeState(game.Expansion)==expansion&&game.State.seed==seed,"Offline/clock generated resources or consumed boost");
        var skill=ChapterEconomy.QuoteSkill(game.State,"slot.coin-boost");game.BuySkill(skill.Request);
        Need(ExpansionCodec.EncodeState(game.Expansion)==expansion,"Skill changed expansion resources");
        // Existing ready order can explicitly claim120 without manufacturing resources.
        game.ClaimOrder("order.gather-potatoes",27,game.State.sequence+1);
        Need(ExpansionCodec.EncodeState(game.Expansion)==expansion,"Order claim changed expansion resources");
        var copy=game.State;copy.coins=0;copy.pending=new PendingOperation();if(copy.lastSpin!=null)copy.lastSpin.symbols[0]=3;
        Need(game.State.coins!=0&&game.State.pending==null,"Detached snapshot mutation reached store");
        var capped=new ExpansionDraft(game.Expansion){Parts=200,Potatoes=200,Energy=30};var max=capped.Freeze();
        var observed=ExpansionEconomy.Observe(max,game.State.sequence+1,new[]{0,1,2});Need(observed.LastResourceReceipt.Parts==0&&observed.LastResourceReceipt.Potatoes==0&&observed.LastResourceReceipt.Energy==0,"Capped resources overflowed/claimed uncredited gain");
        var zero=new ExpansionDraft(game.Expansion){Energy=0};var zeroState=game.State;zeroState.expansion=zero.Freeze();var free=Spin(zeroState);
        Need(free.expansion.Energy==1&&free.lastSpin.symbols.Length==3,"Zero energy blocked/charged base spin");
    }
    static void Tamper()
    {
        var s=Funded();string raw=Codec.Encode(s);
        string fresh=Codec.Encode(ModelState.Fresh(1));
        Reject(()=>Codec.Decode(fresh.Substring(0,fresh.Length-1)+",\"expansion\":null}"));
        Reject(()=>Codec.Decode(fresh.Substring(0,fresh.Length-1)+",\"expansion\":\"\"}"));
        Reject(()=>Codec.Decode(raw.Replace("expansion.state.v1","expansion.state.v2")));
        var p=GameLogic.Reserve(s,"spin",0,s.sequence+1);var missing=p.Copy();missing.pending.expansion=null;Reject(()=>GameLogic.Validate(missing));
        var changed=p.Copy();changed.pending.symbols[0]=(changed.pending.symbols[0]+1)%4;Reject(()=>GameLogic.Validate(changed));
        var orphan=s.Copy();orphan.expansion=null;Reject(()=>GameLogic.Validate(orphan));
        var q=ExpansionEconomy.Quote(s,"buy","module.salvage");var altered=s.Copy();altered.coins++;Reject(()=>ExpansionEconomy.Reserve(altered,q));
        var unowned=new ExpansionDraft(s.expansion);unowned.Modules[0]="module.combo";Reject(()=>unowned.Freeze());
        var duplicate=new ExpansionDraft(s.expansion){ModuleMask=31,SocketCount=3};duplicate.Modules[0]="module.combo";duplicate.Modules[1]="module.combo";Reject(()=>duplicate.Freeze());
        Reject(()=>ExpansionCodec.DecodeState(ExpansionCodec.EncodeState(s.expansion)+"|extra"));
        string operation=ExpansionCodec.EncodeOperation(p.pending.expansion);
        string downgraded=operation.Replace(p.pending.expansion.RuleRevision,"expansion.rules.v1");
        Need(p.pending.expansion.RuleRevision=="expansion.rules.v3"&&downgraded!=operation,"Tamper fixture must actually change the current stored revision");
        Reject(()=>ExpansionCodec.DecodeOperation(downgraded));
        var st=Stored(s);var game=Open(st);NoIo(st,game,()=>game.GetExpansionOffer("trade.unknown"));NoIo(st,game,()=>game.GetExpansionAction("buy","module.salvage",1));
        NoIo(st,game,()=>game.EquipExpansionModule(0,"module.salvage",game.State.sequence));
        NoIo(st,game,()=>game.GetExpansionAction("trade","trade.parts"));
        NoIo(st,game,()=>game.SelectExpansionSpeed("speed.fast",-1));
        NoIo(st,game,()=>game.EquipExpansionModule(1,"module.salvage",-1));
        var owned=Command(s,"buy","module.salvage");owned=Spin(owned);var regressed=owned.Copy();var loss=new ExpansionDraft(regressed.expansion){ModuleMask=0};loss.LastCommand=null;regressed.expansion=loss.Freeze();Reject(()=>GameLogic.Validate(regressed));
        var transition=Input("Checks/Guidance/fixtures/late-repair-era-pending.json");var transitionStorage=Stored(transition);var blocked=Open(transitionStorage);
        blocked.EnterWorkshopYard(blocked.State.sequence+1);Need(blocked.Chapter.TransitionPending,"Expected a real unacknowledged era entry");
        NoIo(transitionStorage,blocked,()=>blocked.GetExpansionAction("speed","speed.fast"));
    }
    static void PublicRoutes()
    {
        var s=Funded(Complete());
        foreach(var pending in new[]{GameLogic.Reserve(s,"clock",0,s.sequence+1,1000),ChapterEconomy.Reserve(s,"clock",null,s.sequence+1,1000),JourneyEconomy.ReserveChapter(s,"clock",null,s.sequence+1,1000),JourneyEconomy.Reserve(s,"order-claim","order.gather-potatoes",27,s.sequence+1)})
        {
            Need(pending.pending.expansion!=null,"Public reserve missed outer aggregate");
            foreach(var after in new[]{GameLogic.ApplyPending(pending),ChapterEconomy.ApplyPending(pending),JourneyEconomy.ApplyPending(pending)})
                Need(ExpansionCodec.EncodeState(after.expansion)==ExpansionCodec.EncodeState(s.expansion),"Public apply lost/mutated aggregate");
        }
    }
    static void Speed()
    {
        var storage=Stored(ModelState.Fresh(1));var game=Open(storage);string original=storage.Primary;
        Need(game.Expansion.SpeedId=="speed.normal"&&game.Expansion.SpinDurationSeconds==3.5f,"Default normal speed changed");
        game.SelectExpansionSpeed("speed.normal",0);Need(storage.Writes==0&&storage.Primary==original&&game.State.expansion==null,"Already selected speed adopted/wrote");
        var q=game.GetExpansionAction("speed","speed.fast");game.ExecuteExpansion(q);
        Need(game.Expansion.SpeedId=="speed.fast"&&game.Expansion.SpinDurationSeconds==1.5f&&game.Expansion.Parts==0&&game.Expansion.ForegroundSpins==0&&game.State.coins==37&&game.State.seed==1&&storage.Writes==2,"Free fast speed changed economy");
        string raw=storage.Primary;int writes=storage.Writes;game.ExecuteExpansion(q);Need(raw==storage.Primary&&writes==storage.Writes,"Repeated speed callback wrote");
        game.SelectExpansionSpeed("speed.normal",game.State.sequence);Need(game.Expansion.SpinDurationSeconds==3.5f,"Normal speed did not restore");
        Spin(game);Need(game.State.coins==45&&game.Expansion.Parts==1&&game.Expansion.Potatoes==2&&game.Expansion.Energy==1&&game.State.seed==2647435461U,"Speed altered first spin result");
        NoIo(storage,game,()=>game.SelectExpansionSpeed("speed.fast",0));
        Export("speed-normal-confirmed",game.State);
    }
    static int Main(string[] args)
    {
        try
        {
            Repository=args.Length>0?Path.GetFullPath(args[0]):Directory.GetCurrentDirectory();Output=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(Repository,".work/five-hour-core/fixtures");
            var groups=new Dictionary<string,Action>{{"old-codecs",OldCodecs},{"adoption",Adoption},{"shop-loadout",ShopLoadout},{"equipment",Equipment},{"module-metrics",ModuleMetrics},{"bonus",Bonus},{"trades",Trades},
                {"trade-write-failures",()=>WriteFailures(false)},{"bonus-write-failures",()=>WriteFailures(true)},{"isolation",Isolation},{"tamper",Tamper},{"public-routes",PublicRoutes},{"speed",Speed}};
            foreach(var group in NextExpansionChecks.Groups(Repository,args.Length>2?Output:null))groups.Add(group.Key,group.Value);
            int count=0;foreach(var group in groups){if(args.Length>1&&args[1]!=group.Key)continue;group.Value();count++;Console.WriteLine("PASS "+group.Key);}
            Need(count>0,"No selected group");Console.WriteLine("PASS "+count+" scoped expansion groups; private memory faults, synthetic funding, no Unity/ordinary saves");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
