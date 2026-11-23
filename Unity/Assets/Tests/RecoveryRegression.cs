using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using JunkToNeon.Core;

namespace JunkToNeon.Tests
{
    public static class RecoveryRegression
    {
        static readonly JsonStateCodec Codec = new JsonStateCodec();
        static readonly string[] Files = { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json",
            "jtn.recovery-primary.v1.json", "jtn.recovery-backup.v1.json", "jtn.recovery-pending.v1.json", "jtn.recovery-write.v1.json" };
        static void Require(bool ok, string detail) { if (!ok) throw new Exception(detail); }
        static void Fails(Action action) { try { action(); } catch { return; } throw new Exception("Recovery unexpectedly succeeded"); }
        static string P(string root, int index) { return Path.Combine(root, Files[index]); }
        static GameSession Open(string root) { return GameSession.Open(new GameStore(new FileSnapshotStorage(root), Codec), 99); }
        static void Case(string root, string name, Action<string> run)
        {
            var folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
            try { run(folder); }
            finally { foreach (var file in Files) { var path = Path.Combine(folder, file); if (File.Exists(path)) File.Delete(path); } Directory.Delete(folder, false); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string newPath, string existingPath, IntPtr security);

        public static IReadOnlyList<string> Run(string root)
        {
            Directory.CreateDirectory(root);
            var selected = GameLogic.ApplyPending(GameLogic.Reserve(ModelState.Fresh(1), "select", 1, 1));
            var frozen = GameLogic.Reserve(selected, "spin", 0, 2);
            var backup = Codec.Encode(frozen);
            var committed = Codec.Encode(GameLogic.ApplyPending(frozen));
            try
            {
                Case(root, "frozen", d =>
                {
                    File.WriteAllText(P(d,0), "{corrupt primary"); File.WriteAllText(P(d,1), backup); File.WriteAllText(P(d,2), "{torn normal write");
                    var game = Open(d);
                    Require(game.RecoveryRequired && game.BackupAvailable && File.ReadAllText(P(d,0)) == "{corrupt primary", "Load silently restored/reset");
                    Fails(() => game.ExecuteSpin(1));
                    var s = game.RestoreBackup();
                    Require(!game.RecoveryRequired && s.coins == 5 && s.houseStage == 1 && s.sequence == 2 && s.selectedStage == 0 && s.pending == null && s.seed == 2647435461u, "Frozen spin/purchase recovery result");
                    Require(File.ReadAllText(P(d,4)) == "{corrupt primary" && File.ReadAllText(P(d,5)) == backup && File.ReadAllText(P(d,6)) == "{torn normal write" && !File.Exists(P(d,2)) && File.ReadAllText(P(d,1)) == backup, "Recovery lost original bytes");
                    Fails(() => game.RestoreBackup());
                    Require(Codec.Encode(game.ExecuteSpin(2)) == committed, "Duplicate restored spin rewarded twice");
                    game = Open(d); Require(!game.RecoveryRequired && Codec.Encode(game.State) == committed, "Restore/restart replayed pending operation");
                });
                Case(root, "missing-primary", d =>
                {
                    var s = ModelState.Fresh(7); s.coins = 15; File.WriteAllText(P(d,1), Codec.Encode(s));
                    var game = Open(d); Require(game.RecoveryRequired && !File.Exists(P(d,0)), "Missing primary granted fresh coins");
                    Require(game.RestoreBackup().coins == 15 && !File.Exists(P(d,4)), "Missing-primary rollback fabricated state/evidence");
                });
                Case(root, "binary-corruption", d =>
                {
                    var raw=new byte[]{0,255,128,123,100};File.WriteAllBytes(P(d,0),raw);
                    var encoded=new System.Text.UTF8Encoding(true).GetPreamble();var text=System.Text.Encoding.UTF8.GetBytes(backup);var bom=new byte[encoded.Length+text.Length];
                    Array.Copy(encoded,bom,encoded.Length);Array.Copy(text,0,bom,encoded.Length,text.Length);File.WriteAllBytes(P(d,1),bom);
                    var game=Open(d);Require(game.RecoveryRequired&&game.BackupAvailable,"Binary primary/BOM backup validation");game.RestoreBackup();
                    Require(Convert.ToBase64String(File.ReadAllBytes(P(d,4)))==Convert.ToBase64String(raw)&&Convert.ToBase64String(File.ReadAllBytes(P(d,5)))==Convert.ToBase64String(bom),"Recovery transcoded original evidence");
                });

                var invalid = new[] { (string)null, "{invalid", backup.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), backup.Replace("\"coins\":37", "\"coins\":-1"), backup.Replace("\"coins\":37", "\"coins\":9223372036854775807") };
                for (int i=0;i<invalid.Length;i++)
                {
                    var raw = invalid[i];
                    Case(root, "invalid-"+i, d =>
                    {
                        File.WriteAllText(P(d,0), "bad"); if(raw!=null)File.WriteAllText(P(d,1),raw);
                        var game=Open(d); Fails(()=>game.RestoreBackup());
                        Require(game.RecoveryRequired && File.ReadAllText(P(d,0))=="bad" && !File.Exists(P(d,4)) && !File.Exists(P(d,5)), "Invalid/missing/overflow candidate mutated originals");
                        Require(raw==null?!File.Exists(P(d,1)):File.ReadAllText(P(d,1))==raw,"Invalid backup overwritten");
                    });
                }
                Case(root, "evidence-without-source", d =>
                {
                    File.WriteAllText(P(d,4),"old corruption");var game=Open(d);
                    Require(game.RecoveryRequired && !File.Exists(P(d,0)),"Recovery evidence allowed fresh grant");
                });
                Case(root, "slot-conflict", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);File.WriteAllText(P(d,4),"different old corruption");
                    var game=Open(d);Fails(()=>game.RestoreBackup());
                    Require(game.RecoveryRequired&&File.ReadAllText(P(d,0))=="bad"&&File.ReadAllText(P(d,1))==backup&&File.ReadAllText(P(d,4))=="different old corruption","Conflicting slot overwritten");
                });
                Case(root, "restore-write-conflict", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);File.WriteAllText(P(d,7),"{partial restore");
                    var game=Open(d);Fails(()=>game.RestoreBackup());
                    Require(game.RecoveryRequired&&File.ReadAllText(P(d,0))=="bad"&&File.ReadAllText(P(d,7))=="{partial restore","Interrupted restore overwritten");
                });
                Case(root, "restore-write-complete", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);File.WriteAllText(P(d,7),committed);
                    var game=Open(d);Require(game.RecoveryRequired,"Interrupted restore silently promoted");
                    Require(game.RestoreBackup().coins==5&&!File.Exists(P(d,7)),"Explicit retry did not use identical frozen candidate");
                });
                Case(root, "changed-backup", d =>
                {
                    File.WriteAllText(P(d,0),"bad");var changed=Codec.Encode(ModelState.Fresh(13));File.WriteAllText(P(d,1),changed);
                    Fails(()=>new FileSnapshotStorage(d).RestoreBackup(backup,committed));
                    Require(File.ReadAllText(P(d,0))=="bad"&&File.ReadAllText(P(d,1))==changed&&!File.Exists(P(d,5)),"Candidate changed after validation but was promoted");
                });
                Case(root, "locked-primary", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);
                    using(var denied=new FileStream(P(d,0),FileMode.Open,FileAccess.Read,FileShare.None))
                    {var game=Open(d);Require(game.RecoveryRequired&&game.BackupAvailable,"Valid backup hidden by inaccessible primary");Fails(()=>game.RestoreBackup());Require(!File.Exists(P(d,4)),"Inaccessible primary was overwritten");}
                    Require(File.ReadAllText(P(d,0))=="bad"&&File.ReadAllText(P(d,1))==backup,"Locked primary/backup changed");
                });
                Case(root, "locked-source", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);
                    using(var denied=new FileStream(P(d,1),FileMode.Open,FileAccess.Read,FileShare.None))
                    {var game=Open(d);Fails(()=>game.RestoreBackup());Require(game.RecoveryRequired&&!File.Exists(P(d,4)),"Inaccessible backup changed primary");}
                    Require(File.ReadAllText(P(d,0))=="bad"&&File.ReadAllText(P(d,1))==backup,"Locked source overwritten");
                });
                Case(root, "locked-promotion", d =>
                {
                    File.WriteAllText(P(d,0),"bad");File.WriteAllText(P(d,1),backup);File.WriteAllText(P(d,2),"partial normal write");
                    var game=Open(d);
                    using(var denied=new FileStream(P(d,0),FileMode.Open,FileAccess.Read,FileShare.Read))
                    {Fails(()=>game.RestoreBackup());Require(game.RecoveryRequired&&File.ReadAllText(P(d,0))=="bad"&&File.Exists(P(d,7)),"Failed promotion was treated as success");}
                    Require(File.ReadAllText(P(d,6))=="partial normal write"&&!File.Exists(P(d,2)),"Failed promotion lost interrupted normal-write evidence");
                    game=Open(d);Require(game.RecoveryRequired,"Failed promotion auto-restored");
                    Require(game.RestoreBackup().coins==5,"Explicit recovery after failed promotion failed");
                });
                if(Environment.OSVersion.Platform==PlatformID.Win32NT) Case(root,"alias",d=>
                {
                    File.WriteAllText(P(d,0),Codec.Encode(selected));
                    Require(CreateHardLink(P(d,1),P(d,0),IntPtr.Zero),"Private hard-link fixture creation failed");
                    File.WriteAllText(P(d,2),"orphan");File.WriteAllText(P(d,3),"different orphan");
                    var game=Open(d);Require(game.RecoveryRequired&&game.BackupAvailable,"Alias fixture did not block");Fails(()=>game.RestoreBackup());
                    Require(File.ReadAllText(P(d,0))==Codec.Encode(selected)&&!File.Exists(P(d,4)),"Same-file primary/backup recovery changed evidence");
                });
                return new[]{"Explicit backup rollback preserves originals and resolves frozen spin/purchase once across restart",
                    "Missing/invalid/version/overflow candidates, bounded slot conflicts and no fresh grant with recovery evidence",
                    "Interrupted restore, inaccessible backup, failed promotion and same-file aliases fail closed"};
            }
            finally { Directory.Delete(root,false); }
        }
    }
}
