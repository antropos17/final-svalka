using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class OfflineRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string primary, backup; public int writes, failAt; public bool commitBeforeFailure;
            public string ReadPrimary() => primary;
            public string ReadBackup() => backup;
            public void Write(string raw)
            {
                writes++;
                if(writes==failAt&&!commitBeforeFailure)throw new IOException("Interrupted offline write");
                backup=primary;primary=raw;
                if(writes==failAt)throw new IOException("Committed offline write reported failure");
            }
        }
        static readonly JsonStateCodec Codec=new JsonStateCodec();
        static void Check(bool ok,string why) { if(!ok)throw new Exception(why); }
        static void Fails(Action act) { try{act();}catch{return;}throw new Exception("Invalid offline operation accepted"); }
        static ModelState Fixture() { var s=ModelState.Fresh(1);s.houseStage=1;s.workshopBuilt=1;s.autoHintSeen=1;s.lastSettledAt=1000;s.coins=595;s.selectedStage=2;return s; }
        static GameSession Open(Storage storage) => GameSession.Open(new GameStore(storage,Codec),1);
        public static IReadOnlyList<string> Run()
        {
            Check(OfflineIncome.Coins(1000,999,1)==0&&OfflineIncome.Coins(1000,1059,1)==0&&OfflineIncome.Coins(1000,1060,1)==1,"Negative/full minute rounding");
            Check(OfflineIncome.Coins(1000,OfflineIncome.MaxUtcSeconds,1)==480&&OfflineIncome.Coins(1000,4600,0)==0,"Cap or unowned income");
            var legacy=Codec.Encode(ModelState.Fresh(1)).Replace(",\"lastSettledAt\":0","").Replace(",\"lastSettlementSeen\":0","").Replace(",\"lastSettlement\":null","");
            var storage=new Storage{primary=legacy};var game=Open(storage);
            Check(storage.writes==0&&game.State.lastSettledAt==0,"Legacy eagerly rewritten");
            game.SettleOffline(4600);Check(game.State.coins==37&&game.State.lastSettledAt==4600&&game.State.lastSettlement==null,"Legacy retrospective grant");
            Fails(()=>game.SettleOffline(-1));Fails(()=>game.SettleOffline(OfflineIncome.MaxUtcSeconds+1));

            storage=new Storage{primary=Codec.Encode(Fixture())};game=Open(storage);var result=game.SettleOffline(1600);
            Check(result.coins==605&&result.houseStage==1&&result.selectedStage==2&&result.lastSettlement.coins==10&&result.lastSettledAt==1600,"Settlement bought selected target or wrong amount");
            game=Open(storage);int writes=storage.writes;game.SettleOffline(1500);game.SettleOffline(1600);
            Check(storage.writes==writes&&game.State.coins==605,"Rollback/reload replay");
            result.lastSettlement.coins=999;Check(game.State.lastSettlement.coins==10,"Receipt reference escaped");
            game.AcknowledgeOffline(2,1610);Check(game.State.coins==605&&game.State.houseStage==1&&game.State.lastSettlementSeen==1,"Acknowledgement bought target");
            game.Checkpoint(1620);Check(game.State.lastSettlement.coins==10&&game.State.lastSettlement.to==1600,"Foreground checkpoint changed receipt");
            game.ExecuteSpin(4,1620);Check(game.State.coins==14&&game.State.houseStage==2&&game.State.lastSpin.reward==8&&game.State.lastSpin.bonus==1,"Foreground purchase changed payout");
            game=Open(storage);Check(game.State.lastSettlement.coins==10&&game.State.lastSettlementSeen==1,"Receipt/seen lost on reload");
            var unowned=Fixture();unowned.workshopBuilt=0;unowned.selectedStage=3;unowned.coins=120;
            storage=new Storage{primary=Codec.Encode(unowned)};game=Open(storage);game.SettleOffline(4600);game=Open(storage);
            Check(game.State.coins==120&&game.State.workshopBuilt==0&&game.State.selectedStage==3,"Unbuilt workshop earned or checkpoint bought it");
            game.SelectProject(3,2,4600);Check(game.State.coins==0&&game.State.workshopBuilt==1,"Visible workshop purchase failed");
            game.SettleOffline(4660);Check(game.State.coins==1,"Workshop received pre-purchase time");

            foreach(bool afterCommit in new[]{false,true})foreach(int failAt in new[]{1,2})
            {
                storage=new Storage{primary=Codec.Encode(Fixture()),failAt=failAt,commitBeforeFailure=afterCommit};game=Open(storage);
                Fails(()=>game.SettleOffline(1600));Check(game.RecoveryRequired,"Failed settlement allowed mutation");
                Fails(()=>game.SettleOffline(1600));storage.failAt=0;game=Open(storage);game.SettleOffline(1600);
                Check(!game.RecoveryRequired&&game.State.coins==605&&game.State.lastSettlement.coins==10&&game.State.lastSettlementSeen==0,"Interrupted/ambiguous settlement duplicated or lost receipt");
                game=Open(storage);writes=storage.writes;game.SettleOffline(1600);Check(storage.writes==writes&&game.State.coins==605,"Recovered settlement replayed");
            }
            storage=new Storage{primary=Codec.Encode(Fixture())};game=Open(storage);game.SettleOffline(100000);
            Check(game.State.coins==1075&&game.State.lastSettledAt==100000&&game.State.lastSettlement.seconds==28800,"Cap did not advance full observed time");
            game.SettleOffline(90000);game.SettleOffline(100059);Check(game.State.coins==1075&&game.State.lastSettlement.coins==480,"Rollback or zero-income return changed reward/receipt");
            game.SettleOffline(100119);Check(game.State.coins==1076&&game.State.lastSettlement.coins==1,"Complete-minute per-absence policy");
            var raw=storage.primary;Fails(()=>Codec.Decode(raw.Replace("\"coins\":1,\"workshop\":1","\"coins\":2,\"workshop\":1")));
            Fails(()=>Codec.Decode(raw.Replace("\"lastSettlementSeen\":0","\"lastSettlementSeen\":99999")));
            for(int i=0;i<100;i++)game.SettleOffline(100179+i*60);
            Check(storage.primary.Length<1000&&game.State.lastSettlement.coins==1,"Unbounded settlement history");
            var overflow=Fixture();overflow.coins=long.MaxValue-5;overflow.selectedStage=0;
            storage=new Storage{primary=Codec.Encode(overflow)};game=Open(storage);Fails(()=>game.SettleOffline(1600));game=Open(storage);
            Check(game.RecoveryRequired&&game.State.coins==long.MaxValue-5,"Offline overflow wrapped or replayed");
            return new[]{"Offline legacy clock initialization, bounded pure rate, negative time and strict timestamp rejection",
                "Offline-funded house defers purchase, receipt/ack persistence and non-retroactive workshop income",
                "Offline interrupted/ambiguous writes, replay/rollback/cap, immutable bounded receipt and checked overflow"};
        }
    }
}
