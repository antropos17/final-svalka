using System;
using System.IO;
using JunkToNeon.Core;

// Portable actual-API driver. A caller supplies the real economic command and
// independent before/after assertions; this helper never computes expected rewards.
internal static class ReplayFaultMatrix
{
    sealed class Storage : ISnapshotStorage
    {
        public string raw,backup;public int writes,failAt;public bool afterCommit;
        public string ReadPrimary()=>raw;
        public string ReadBackup()=>backup;
        public void Write(string next)
        {
            writes++;
            if(writes==failAt&&!afterCommit)throw new IOException("Replay matrix: before durable write");
            backup=raw;raw=next;
            if(writes==failAt)throw new IOException("Replay matrix: after durable write");
        }
    }
    static void Require(bool condition,string why) { if(!condition)throw new Exception(why); }
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static GameSession Open(Storage storage)=>GameSession.Open(new GameStore(storage,Codec),123456789);
    internal static void Verify(string caseName,string initialRaw,Action<GameSession,long> command,
        Action<ModelState> assertBefore,Action<ModelState> assertAfter)
    {
        var successStore=new Storage{raw=initialRaw};var success=Open(successStore);
        Require(!success.RecoveryRequired&&successStore.writes==0,caseName+": opening pre-command state wrote or failed");
        assertBefore(success.State);long sequence=success.State.sequence+1;
        command(success,sequence);assertAfter(success.State);
        Require(success.State.sequence==sequence&&successStore.writes==2,caseName+": command is not one two-write operation");
        string expectedCommitted=Codec.Encode(success.State);
        foreach(int write in new[]{1,2})foreach(bool after in new[]{false,true})
        {
            var storage=new Storage{raw=initialRaw,failAt=write,afterCommit=after};var game=Open(storage);
            bool threw=false;try{command(game,sequence);}catch{threw=true;}
            Require(threw&&game.RecoveryRequired,caseName+": uncertain command did not block");
            string frozen=storage.raw;int writes=storage.writes;
            try{command(game,sequence+1);}catch{ }
            Require(storage.writes==writes&&storage.raw==frozen,caseName+": blocked session wrote again");
            storage.failAt=0;game=Open(storage);Require(!game.RecoveryRequired,caseName+": recovery failed: "+game.Error);
            if(write==1&&!after)
            {
                Require(storage.raw==initialRaw,caseName+": failed first write changed saved bytes");
                assertBefore(game.State);
                command(game,sequence); // Nothing reserved: only a fresh explicit action may now apply it.
            }
            assertAfter(game.State);
            Require(Codec.Encode(game.State)==expectedCommitted,caseName+": interrupted result differs from immutable successful result");
            writes=storage.writes;
            game=Open(storage);command(game,sequence);game=Open(storage);
            Require(storage.writes==writes&&Codec.Encode(game.State)==expectedCommitted,caseName+": reopen/duplicate command replayed");
            assertAfter(game.State);
        }
    }
}
