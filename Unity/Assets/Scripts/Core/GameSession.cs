using System;
using System.IO;

namespace JunkToNeon.Core
{
    public interface IStateCodec { string Encode(ModelState state); ModelState Decode(string json); }
    public interface ISnapshotStorage { string ReadPrimary(); string ReadBackup(); void Write(string validatedSnapshot); }
    public interface IBackupRecoveryStorage
    {
        bool HasRecoveryEvidence { get; }
        void RestoreBackup(string expectedBackup, string committedSnapshot);
    }
    public interface IInterruptedSnapshotStorage
    {
        bool HasInterruptedWrite { get; }
        void PreserveInterruptedWrite();
    }

    public sealed partial class GameStore
    {
        readonly ISnapshotStorage storage;
        readonly IStateCodec codec;
        readonly object gate = new object();
        ModelState state;
        bool blocked = true;
        public bool RecoveryRequired { get { lock (gate) return blocked; } }
        public string Error { get; private set; }
        public bool BackupAvailable { get; private set; }
        public bool RecoveredSettlement { get; private set; }
        public ModelState State { get { lock (gate) return state == null ? null : state.Copy(); } }
        public GameStore(ISnapshotStorage storage, IStateCodec codec) { this.storage = storage; this.codec = codec; }
        void Write(ModelState next)
        {
            // The exact sealed codec fully validates both its input and decoded
            // output. Keep Decode (including strict JSON bounds) and retain both
            // boundary checks for every other IStateCodec implementation.
            bool codecValidates = codec != null && codec.GetType() == typeof(JsonStateCodec);
            if (!codecValidates) GameLogic.Validate(next);
            var raw = codec.Encode(next);
            var decoded = codec.Decode(raw);
            if (!codecValidates) GameLogic.Validate(decoded);
            storage.Write(raw); state = next.Copy();
        }
        public void Open(uint seed,bool initializeJourney=false)
        {
            lock (gate)
            {
                if (state != null && !blocked) throw new InvalidOperationException("Session already open");
                blocked = true; Error = null; BackupAvailable = false;RecoveredSettlement=false;
                try
                {
                    string backup; bool backupReadable=true;
                    try { backup = storage.ReadBackup(); }
                    catch (SnapshotSizeLimitException) { backup = string.Empty; backupReadable=false; } // Existing but invalid; never authorize a fresh grant. 
                    var interrupted = storage as IInterruptedSnapshotStorage;
                    if (backup != null && backupReadable) { try { GameLogic.Validate(codec.Decode(backup)); BackupAvailable = true; } catch { /* Preserve uncertain candidates. */ } }
                    var primary = storage.ReadPrimary();
                    if (primary == null)
                    {
                        if (interrupted != null && interrupted.HasInterruptedWrite) throw new InvalidOperationException("Primary missing with interrupted write; originals preserved");
                        if (backup != null) throw new InvalidOperationException("Primary save missing; backup preserved for explicit recovery");
                        if (storage is IBackupRecoveryStorage recovery && recovery.HasRecoveryEvidence) throw new InvalidOperationException("Recovery evidence exists; fresh save blocked");
                        var fresh=ModelState.Fresh(seed);
                        if(initializeJourney)fresh.journey=JourneyState.Fresh();
                        Write(fresh);
                    }
                    else
                    {
                        state = codec.Decode(primary); GameLogic.Validate(state);
                        // Only a validated primary authorizes bounded quarantine. The
                        // orphan never becomes a new economic command or RNG draw.
                        if (interrupted != null) interrupted.PreserveInterruptedWrite();
                    }
                    if (state.pending != null)
                    {
                        bool settlement=state.pending.kind=="settle";
                        Write(GameLogic.ApplyPending(state));RecoveredSettlement=settlement;
                    }
                    blocked = false;
                }
                catch (Exception error) { Error = error.Message; blocked = true; }
            }
        }
        public ModelState Execute(string kind, int target, long sequence,long at=0)
        {
            lock (gate)
            {
                if (blocked || state == null) throw new InvalidOperationException("Save mutation blocked: " + Error);
                var pending = GameLogic.Reserve(state, kind, target, sequence,at);
                if (ReferenceEquals(pending, state)) return state.Copy();
                try { Write(pending); Write(GameLogic.ApplyPending(pending)); return state.Copy(); }
                catch (Exception error) { blocked = true; Error = error.Message; throw; }
            }
        }
        public ModelState ObserveTime(long now,bool returning)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Clock mutation blocked: "+Error);
                if(now<=0||now>OfflineIncome.MaxUtcSeconds)throw new InvalidOperationException("Invalid UTC timestamp");
                if(now<=state.lastSettledAt)return state.Copy();
                bool income=returning&&state.lastSettledAt!=0&&OfflineIncome.Coins(state.lastSettledAt,now,state.workshopBuilt)>0;
                return Execute(income?"settle":"clock",0,state.sequence+1,now);
            }
        }
        public ModelState ExecuteUpgrade(JunkToNeon.Upgrades.UpgradePurchaseRequest request,long at)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Upgrade mutation blocked: "+Error);
                var result=JunkToNeon.Upgrades.CoinBoostShop.Propose(state.sequence,state.coins,state.houseStage,WaveProgress.Of(state).Slot,request);
                if(result.Status==JunkToNeon.Upgrades.PurchaseStatus.Replay)return state.Copy();
                if(result.Status!=JunkToNeon.Upgrades.PurchaseStatus.Proposed)throw new InvalidOperationException("Upgrade quote changed: "+result.Status);
                if(state.chapter!=null)
                    throw new InvalidOperationException("Legacy price quote is stale; inspect the current skill offer");
                return Execute("upgrade",0,request.Sequence,at);
            }
        }
        public ModelState RestoreBackup()
        {
            lock (gate)
            {
                if (!blocked) throw new InvalidOperationException("Recovery requires a blocked session");
                if (!(storage is IBackupRecoveryStorage recovery)) throw new InvalidOperationException("Storage does not support explicit recovery");
                BackupAvailable = false;
                try
                {
                    var raw = storage.ReadBackup();
                    var candidate = codec.Decode(raw); GameLogic.Validate(candidate);
                    // Resolve a frozen pending result before changing any originals, never reroll.
                    candidate = GameLogic.ApplyPending(candidate);
                    var committed = codec.Encode(candidate); GameLogic.Validate(codec.Decode(committed));
                    BackupAvailable = true;
                    recovery.RestoreBackup(raw, committed);
                    var actual = codec.Decode(storage.ReadPrimary()); GameLogic.Validate(actual);
                    if (codec.Encode(actual) != committed) throw new IOException("Restored snapshot verification failed");
                    state = actual; blocked = false; Error = null;
                    return state.Copy();
                }
                catch (Exception error) { Error = error.Message; blocked = true; throw; }
            }
        }
    }

    public sealed partial class GameSession
    {
        readonly GameStore store;
        GameSession(GameStore store) { this.store = store; }
        public static GameSession Open(GameStore store, uint seed,bool initializeJourney=false) { store.Open(seed,initializeJourney); return new GameSession(store); }
        public ModelState State { get { return store.State; } }
        public bool RecoveryRequired { get { return store.RecoveryRequired; } }
        public string Error { get { return store.Error; } }
        public bool BackupAvailable { get { return store.BackupAvailable; } }
        public bool RecoveredSettlement { get { return store.RecoveredSettlement; } }
        public ModelState ExecuteSpin(long sequence,long at=0) { return store.Execute("spin", 0, sequence,at); }
        public ModelState SelectProject(int target, long sequence,long at=0) { return store.Execute("select", target, sequence,at); }
        public ModelState BuySlotUpgrade(long sequence,long at=0) { return store.Execute("upgrade",0,sequence,at); }
        public ModelState BuySlotUpgrade(JunkToNeon.Upgrades.UpgradePurchaseRequest request,long at=0) { return store.ExecuteUpgrade(request,at); }
        public ModelState AcknowledgeAutoHint(long sequence,long at=0) { return store.Execute("hint", 0, sequence,at); }
        public ModelState SettleOffline(long now) { return store.ObserveTime(now,true); }
        public ModelState Checkpoint(long now) { return store.ObserveTime(now,false); }
        public ModelState AcknowledgeOffline(long sequence,long at=0) { return store.Execute("seen",0,sequence,at); }
        public ModelState RestoreBackup() { return store.RestoreBackup(); }
    }

    // A complete file snapshot is flushed before replacement. One quarantine slot
    // preserves an orphan temp after primary validation; conflicts fail closed.
    public sealed class FileSnapshotStorage : ISnapshotStorage, IInterruptedSnapshotStorage, IBackupRecoveryStorage
    {
        readonly string primary, backup, temporary, interrupted;
        readonly string recoveryPrimary, recoveryBackup, recoveryPending, restoreTemporary;
        public FileSnapshotStorage(string directory)
        {
            Directory.CreateDirectory(directory);
            primary = Path.Combine(directory, "jtn.save.v1.json");
            backup = Path.Combine(directory, "jtn.backup.v1.json");
            temporary = Path.Combine(directory, "jtn.pending-write.v1.json");
            interrupted = Path.Combine(directory, "jtn.interrupted-write.v1.json");
            recoveryPrimary = Path.Combine(directory, "jtn.recovery-primary.v1.json");
            recoveryBackup = Path.Combine(directory, "jtn.recovery-backup.v1.json");
            recoveryPending = Path.Combine(directory, "jtn.recovery-pending.v1.json");
            restoreTemporary = Path.Combine(directory, "jtn.recovery-write.v1.json");
        }
        public bool HasRecoveryEvidence { get { return File.Exists(recoveryPrimary) || File.Exists(recoveryBackup) || File.Exists(recoveryPending) || File.Exists(restoreTemporary); } }
        static void RejectLink(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Recovery does not follow file links");
        }
        static bool EqualBytes(Stream left, Stream right)
        {
            if (left.Length != right.Length) return false;
            left.Position = right.Position = 0;
            int a;
            while ((a = left.ReadByte()) != -1) if (a != right.ReadByte()) return false;
            return true;
        }
        static void Preserve(Stream source, string destination)
        {
            RejectLink(destination);
            if (File.Exists(destination))
            {
                using (var existing = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
                    if (!EqualBytes(source, existing)) throw new IOException("Recovery preservation slot occupied; originals unchanged");
                return;
            }
            source.Position = 0;
            using (var copy = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { source.CopyTo(copy); copy.Flush(true); }
        }
        public void RestoreBackup(string expectedBackup, string committedSnapshot)
        {
            foreach (var path in new[] { primary, backup, temporary, restoreTemporary }) RejectLink(path);
            // Exclusive backup access also rejects aliases/hard links to an open primary.
            using (var original = File.Exists(primary) ? new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.Read) : null)
            using (var source = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (BoundedSnapshotReader.ReadStream(source, true) != expectedBackup) throw new IOException("Backup changed; select recovery again");
                Preserve(source, recoveryBackup);
                if (original != null) Preserve(original, recoveryPrimary);
                using (var pending = File.Exists(temporary) ? new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.None) : null)
                    if (pending != null) Preserve(pending, recoveryPending);
            }
            var bytes = new System.Text.UTF8Encoding(false).GetBytes(committedSnapshot);
            // A distinct interrupted restore is uncertain evidence, never overwritten.
            if (File.Exists(restoreTemporary))
            {
                using (var expected = new MemoryStream(bytes))
                using (var existing = new FileStream(restoreTemporary, FileMode.Open, FileAccess.Read, FileShare.None))
                    if (!EqualBytes(expected, existing)) throw new IOException("Interrupted restore conflicts; originals preserved");
            }
            else
            {
                using (var write = new FileStream(restoreTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { write.Write(bytes, 0, bytes.Length); write.Flush(true); }
            }
            // A preserved interrupted normal write must not be replayed after the rollback.
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(primary)) File.Replace(restoreTemporary, primary, null);
            else File.Move(restoreTemporary, primary);
        }
        public string ReadPrimary()
        {
            return BoundedSnapshotReader.ReadFile(primary);
        }
        public bool HasInterruptedWrite { get { return File.Exists(temporary); } }
        public void PreserveInterruptedWrite()
        {
            if (!File.Exists(temporary)) return;
            if (!File.Exists(interrupted)) { File.Move(temporary, interrupted); return; }
            // Never replace distinct uncertain evidence or create an unbounded list.
            using (var source = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var preserved = new FileStream(interrupted, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (source.Length != preserved.Length) throw new IOException("Interrupted-write preservation slot occupied; recovery required");
                var a = new byte[4096]; var b = new byte[4096]; int count;
                while ((count = source.Read(a, 0, a.Length)) > 0)
                {
                    var read = 0;
                    while (read < count) { var next = preserved.Read(b, read, count - read); if (next == 0) throw new IOException("Interrupted-write comparison failed"); read += next; }
                    for (var i = 0; i < count; i++) if (a[i] != b[i]) throw new IOException("Interrupted-write preservation slot occupied; recovery required");
                }
            }
            // Exact duplicate is already preserved in the fixed slot.
            File.Delete(temporary);
        }
        public string ReadBackup() { return BoundedSnapshotReader.ReadFile(backup); }
        public void Write(string validatedSnapshot)
        {
            var bytes = new System.Text.UTF8Encoding(false).GetBytes(validatedSnapshot);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(primary)) File.Replace(temporary, primary, backup);
            else File.Move(temporary, primary);
        }
    }
}
