using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Core;

// Every read, write, flush, replace, quarantine and restore delegates to actual
// production FileSnapshotStorage. The wrapper only observes results or holds a
// Windows deny-delete read handle after an explicitly chosen successful write.
internal sealed class AuditedStorage : ISnapshotStorage, IInterruptedSnapshotStorage, IBackupRecoveryStorage, IDisposable
{
    readonly FileSnapshotStorage actual;
    readonly PrivateFiles files;
    internal string DirectoryPath { get; }
    internal int WriteAttempts, RestoreAttempts, PreserveCalls, ReadAttempts;
    internal List<WriteFrame> Writes { get; } = new List<WriteFrame>();
    internal bool LockAfterNextWrite;
    FileStream held;
    internal AuditedStorage(PrivateFiles files, string directory)
    {
        this.files = files; DirectoryPath = files.Inside(directory); actual = new FileSnapshotStorage(DirectoryPath);
    }
    public string ReadPrimary() { ReadAttempts++; files.Inventory(DirectoryPath); return actual.ReadPrimary(); }
    public string ReadBackup() { ReadAttempts++; files.Inventory(DirectoryPath); return actual.ReadBackup(); }
    public bool HasInterruptedWrite { get { ReadAttempts++; files.Inventory(DirectoryPath); return actual.HasInterruptedWrite; } }
    public bool HasRecoveryEvidence { get { ReadAttempts++; files.Inventory(DirectoryPath); return actual.HasRecoveryEvidence; } }
    public void PreserveInterruptedWrite() { files.Inventory(DirectoryPath); PreserveCalls++; actual.PreserveInterruptedWrite(); files.Inventory(DirectoryPath); }
    public void Write(string snapshot)
    {
        files.Inventory(DirectoryPath);
        if (WriteAttempts >= 16 || System.Text.Encoding.UTF8.GetByteCount(snapshot) > PrivateFiles.MaximumFileBytes) throw new IOException("Bounded write budget exceeded");
        WriteAttempts++; actual.Write(snapshot);
        Writes.Add(new WriteFrame { Primary = actual.ReadPrimary(), Backup = actual.ReadBackup() });
        files.Inventory(DirectoryPath);
        if (LockAfterNextWrite) { LockAfterNextWrite = false; HoldPrimary(); }
    }
    public void RestoreBackup(string expectedBackup, string committedSnapshot)
    {
        files.Inventory(DirectoryPath); RestoreAttempts++; actual.RestoreBackup(expectedBackup, committedSnapshot); files.Inventory(DirectoryPath);
    }
    internal void HoldPrimary()
    {
        if (held != null) throw new InvalidOperationException("Private lock already held");
        held = new FileStream(files.PathAt(DirectoryPath, 0), FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    internal void Release() { held?.Dispose(); held = null; }
    public void Dispose() { Release(); }
}
internal sealed class WriteFrame { internal string Primary, Backup; }
