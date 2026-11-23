using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class WorkshopRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string primary, backup; public int writes, failAt, reads;
            public string ReadPrimary() { reads++; return primary; }
            public string ReadBackup() { reads++; return backup; }
            public void Write(string raw) { writes++; if(writes==failAt)throw new IOException("Private interrupted workshop write");backup=primary;primary=raw; }
        }
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static void Check(bool ok,string why) { if(!ok)throw new Exception(why); }
        static void Fails(Action act) { try{act();}catch{return;}throw new Exception("Invalid workshop command accepted"); }
        static ModelState Repaired(long coins) { var s=ModelState.Fresh(1);s.houseStage=1;s.autoHintSeen=1;s.coins=coins;return s; }
        static GameSession Open(Storage s) { return GameSession.Open(new GameStore(s,Codec),99); }
        static ModelState Select(ModelState s,int target) { return GameLogic.ApplyPending(GameLogic.Reserve(s,"select",target,s.sequence+1)); }
        public static IReadOnlyList<string> Run()
        {
            var legacy="{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":2,\"sequence\":1,\"coins\":45,\"houseStage\":0,\"selectedStage\":0,\"seed\":2647435461,\"lastSpin\":{\"sequence\":1,\"symbols\":[0,0,2],\"reward\":8},\"pending\":null}";
            var storage=new Storage{primary=legacy};var game=Open(storage);
            Check(!game.RecoveryRequired&&game.State.workshopBuilt==0&&game.State.lastSpin.bonus==0&&game.State.coins==45&&storage.writes==0&&storage.primary==legacy,"Old save/receipt compatibility or eager rewrite");
            var oldComplete=Codec.Decode(legacy.Replace("\"houseStage\":0","\"houseStage\":2"));
            Check(!HouseGoal.Complete(oldComplete)&&GameLogic.Available(oldComplete,3),"Old completed house hides new choice");
            var oldPending=Codec.Encode(GameLogic.Reserve(Repaired(600),"select",2,1)).Replace(",\"workshopBuilt\":0","");
            var legacyPending=Open(new Storage{primary=oldPending});
            Check(!legacyPending.RecoveryRequired&&legacyPending.State.houseStage==2&&legacyPending.State.coins==0&&legacyPending.State.workshopBuilt==0,"Old pending house purchase changed during migration");
            Fails(()=>Select(ModelState.Fresh(1),3));
            foreach(var value in new[]{"null","-1","2","\"yes\""})Fails(()=>Codec.Decode(Codec.Encode(Repaired(10)).Replace("\"workshopBuilt\":0","\"workshopBuilt\":"+value)));
            Fails(()=>Codec.Decode(Codec.Encode(ModelState.Fresh(1)).Replace("\"workshopBuilt\":0","\"workshopBuilt\":1")));

            var s=Select(Repaired(119),2);s=Select(s,3);
            Check(s.selectedStage==3&&s.coins==119&&s.workshopBuilt==0,"Switching charged unavailable funds");
            s=Select(s,2);s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1));
            Check(s.coins==127&&s.selectedStage==2&&s.workshopBuilt==0&&s.houseStage==1,"Affordable unselected workshop purchased");
            s=Select(s,3);Check(s.coins==7&&s.workshopBuilt==1&&s.selectedStage==0&&s.houseStage==1,"Explicit switch purchase or overflow");
            s=Codec.Decode(Codec.Encode(s));s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1));
            Check(s.coins==16&&s.lastSpin.reward==8&&s.lastSpin.bonus==1&&s.selectedStage==0,"Separate workshop bonus after reload");
            Fails(()=>Select(s,3));

            foreach(bool workshopFirst in new[]{true,false})
            {
                s=Select(Repaired(720),workshopFirst?3:2);
                Check(s.coins==(workshopFirst?600:120)&&!HouseGoal.Complete(s)&&s.selectedStage==0,"First choice bought successor or ended early");
                s=Select(s,workshopFirst?2:3);s=Codec.Decode(Codec.Encode(s));
                Check(s.coins==0&&s.houseStage==2&&s.workshopBuilt==1&&s.selectedStage==0&&!HouseGoal.Complete(s),"Both orders/subset persistence");
                Fails(()=>Select(s,3));Fails(()=>Select(s,2));
            }
            var early=Select(Repaired(720),3);early=GameLogic.ApplyPending(GameLogic.Reserve(early,"spin",0,2));early=Select(early,2);
            var late=Select(Repaired(720),2);late=GameLogic.ApplyPending(GameLogic.Reserve(late,"spin",0,2));late=Select(late,3);
            Check(early.coins==9&&late.coins==8&&!HouseGoal.Complete(early)&&!HouseGoal.Complete(late),"Purchase order did not expose earlier workshop benefit");
            s=Select(Repaired(119),3);s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,2));
            Check(s.coins==7&&s.workshopBuilt==1&&s.lastSpin.reward==8&&s.lastSpin.bonus==0,"Purchase spin received retroactive bonus");
            Fails(()=>Codec.Decode(Codec.Encode(s).Replace("\"bonus\":0","\"bonus\":2")));

            storage=new Storage{primary=Codec.Encode(Repaired(120)),failAt=2};game=Open(storage);
            Fails(()=>game.SelectProject(3,1));Check(game.RecoveryRequired,"Failed purchase continued mutation");
            game=Open(storage);Check(!game.RecoveryRequired&&game.State.coins==0&&game.State.workshopBuilt==1&&game.State.sequence==1,"Pending workshop purchase recovered twice/wrong");
            int writes=storage.writes;game.SelectProject(3,1);Check(storage.writes==writes&&game.State.coins==0,"Duplicate purchase applied");
            storage.failAt=storage.writes+2;Fails(()=>game.ExecuteSpin(2));Check(game.RecoveryRequired,"Failed bonus spin continued mutation");
            game=Open(storage);Check(!game.RecoveryRequired&&game.State.coins==9&&game.State.sequence==2&&game.State.lastSpin.reward==8&&game.State.lastSpin.bonus==1,"Frozen bonus recovery duplicated or rerolled");
            writes=storage.writes;game.ExecuteSpin(2);game=Open(storage);Check(game.State.coins==9&&storage.writes==writes,"Committed bonus replayed after reload");
            s=Repaired(long.MaxValue-8);s.workshopBuilt=1;storage=new Storage{primary=Codec.Encode(s)};game=Open(storage);
            string confirmed=storage.primary;int reads=storage.reads;bool overflow=false;
            try{game.ExecuteSpin(1);}catch(OverflowException){overflow=true;}
            Check(overflow&&!game.RecoveryRequired&&storage.reads==reads&&storage.writes==0&&storage.primary==confirmed&&storage.backup==null&&
                Codec.Encode(game.State)==confirmed&&game.State.pending==null&&game.State.expansion==null,
                "NEW V3 bonus overflow did not refuse before IO without adoption");
            game=Open(storage);Check(!game.RecoveryRequired&&storage.writes==0&&storage.primary==confirmed&&storage.backup==null&&Codec.Encode(game.State)==confirmed,
                "NEW V3 preflight refusal changed confirmed state across reopen");

            const string oldOverflow="{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":1,\"sequence\":0,\"coins\":9223372036854775799,\"houseStage\":1,\"selectedStage\":0,\"seed\":1,\"autoHintSeen\":1,\"workshopBuilt\":1,\"lastSettledAt\":0,\"lastSettlementSeen\":0,\"lastSettlement\":null,\"lastSpin\":null,\"pending\":{\"sequence\":1,\"kind\":\"spin\",\"symbols\":[0,0,2],\"seedAfter\":2647435461,\"target\":0,\"at\":0}}";
            Check(Codec.Encode(Codec.Decode(oldOverflow))==oldOverflow,"Literal historical overflow pending canonical bytes changed");
            storage=new Storage{primary=oldOverflow};
            for(int reopen=0;reopen<2;reopen++)
            {
                game=Open(storage);
                Check(game.RecoveryRequired&&storage.writes==0&&storage.primary==oldOverflow&&storage.backup==null&&Codec.Encode(game.State)==oldOverflow&&
                    game.State.pending!=null&&game.State.expansion==null,
                    "OLD durable bonus overflow must remain blocked without writes or adoption");
            }
            s=Repaired(long.MaxValue-9);s.workshopBuilt=1;storage=new Storage{primary=Codec.Encode(s)};game=Open(storage);game.ExecuteSpin(1);
            Check(!game.RecoveryRequired&&storage.writes==2&&game.State.coins==long.MaxValue&&game.Arcade.LastEvent.CoinIncome==9&&game.Arcade.LastEvent.ConstructionDebit==0,
                "Representable adjacent bonus boundary lost income or refused");
            return new[]{"Legacy workshop-absent saves/receipts, strict fields and explicit target switching without unselected purchases",
                "Both construction orders, exact costs/overflow, separate non-retroactive bonus and two-project subset persistence",
                "Interrupted workshop purchase/bonus, duplicate/reload protection and checked bonus overflow"};
        }
    }
}
