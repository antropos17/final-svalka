using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class BoundedStorageRegression
    {
        sealed class EmptyAcceptingCodec : IStateCodec
        {
            readonly JsonStateCodec strict=new JsonStateCodec();
            public string Encode(ModelState state) { return strict.Encode(state); }
            public ModelState Decode(string raw) { return raw==string.Empty?ModelState.Fresh(1):strict.Decode(raw); }
        }
        static void Need(bool value,string message) { if(!value)throw new Exception(message); }
        public static IReadOnlyList<string> Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var codec=new JsonStateCodec();
            string valid=codec.Encode(ModelState.Fresh(1)), large=new string(' ',65537);
            string primary=Path.Combine(directory,"jtn.save.v1.json"),backup=Path.Combine(directory,"jtn.backup.v1.json");
            File.WriteAllText(primary,valid);File.WriteAllText(backup,large);
            var store=new GameStore(new FileSnapshotStorage(directory),codec);store.Open(1);
            Need(!store.RecoveryRequired&&!store.BackupAvailable&&store.State.coins==37,"Valid primary must survive an oversized invalid backup");
            Need(File.ReadAllText(primary)==valid&&File.ReadAllText(backup)==large,"Opening healthy primary changed oversized evidence");
            var generic=new GameStore(new FileSnapshotStorage(directory),new EmptyAcceptingCodec());generic.Open(1);
            Need(!generic.RecoveryRequired&&!generic.BackupAvailable,"Generic codec accepting empty input must never advertise an unreadable oversized backup");
            File.Delete(primary);store=new GameStore(new FileSnapshotStorage(directory),codec);store.Open(1);
            Need(store.RecoveryRequired&&store.State==null&&!File.Exists(primary)&&File.ReadAllText(backup)==large,"Missing primary plus oversized backup must never create fresh37");
            File.WriteAllText(primary,large);File.WriteAllText(backup,valid);
            store=new GameStore(new FileSnapshotStorage(directory),codec);store.Open(1);
            Need(store.RecoveryRequired&&store.BackupAvailable&&File.ReadAllText(primary)==large,"Oversized primary must block without mutation and retain valid backup");
            File.WriteAllText(primary,valid);File.WriteAllText(backup,large);
            bool rejected=false;
            try { new FileSnapshotStorage(directory).RestoreBackup(large,valid); }
            catch(SnapshotSizeLimitException) { rejected=true; }
            Need(rejected&&Directory.GetFiles(directory).Length==2&&File.ReadAllText(primary)==valid&&File.ReadAllText(backup)==large,"Exclusive recovery must reject oversized input before preservation or promotion");
            File.Delete(backup);string maximum=valid+new string(' ',65536-valid.Length);File.WriteAllText(primary,maximum);
            store=new GameStore(new FileSnapshotStorage(directory),codec);store.Open(1);
            Need(!store.RecoveryRequired&&store.State.coins==37&&File.ReadAllText(primary)==maximum,"Exact65536-character valid primary must remain readable without rewrite");
            File.Delete(primary);Directory.Delete(directory);
            return new[]{"Connected bounded FileSnapshotStorage: healthy primary/oversized backup, missing-primary no-grant, blocked oversized primary, exclusive pre-preservation rejection, exact65536 valid boundary"};
        }
    }
}
