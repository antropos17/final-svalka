using System;
using System.Collections.Generic;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class StoreCodecBoundaryRegression
    {
        sealed class EmptyStorage : ISnapshotStorage
        {
            public int Writes;
            public string ReadPrimary() { return null; }
            public string ReadBackup() { return null; }
            public void Write(string value) { Writes++; }
        }
        sealed class NonValidatingCodec : IStateCodec
        {
            public int Encodes, Decodes;
            public string Encode(ModelState state) { Encodes++; return "not a canonical codec"; }
            public ModelState Decode(string raw) { Decodes++; var state=ModelState.Fresh(1);state.coins=-1;return state; }
        }
        static void Need(bool value,string detail) { if(!value)throw new Exception(detail); }
        static void Reject(Action action)
        {
            try { action(); } catch(InvalidOperationException) { return; }
            throw new Exception("Invalid state escaped the codec validation contract");
        }
        public static IReadOnlyList<string> Run()
        {
            var canonical=new JsonStateCodec();var invalid=ModelState.Fresh(1);invalid.coins=-1;
            Reject(()=>canonical.Encode(invalid));
            string valid=canonical.Encode(ModelState.Fresh(1));
            string bad=valid.Replace("\"coins\":37","\"coins\":-1");
            Need(bad!=valid,"Independent invalid decoded wallet fixture missing");
            Reject(()=>canonical.Decode(bad));
            var storage=new EmptyStorage();var custom=new NonValidatingCodec();var game=new GameStore(storage,custom);
            game.Open(1);
            Need(game.RecoveryRequired&&game.State==null&&storage.Writes==0,"Custom codec bypassed decoded validation or accepted a fresh grant");
            Need(custom.Encodes==1&&custom.Decodes==1&&!string.IsNullOrEmpty(game.Error),"Custom codec rejection did not exercise the full roundtrip boundary");
            Reject(()=>game.Execute("spin",0,1));
            Need(storage.Writes==0,"Blocked custom-codec session wrote on retry");
            return new[]{"Canonical Encode/Decode reject invalid wallets; a nonvalidating custom codec is blocked before any initial write/grant and remains blocked on retry"};
        }
    }
}
