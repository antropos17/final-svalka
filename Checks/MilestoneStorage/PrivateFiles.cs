using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal sealed class PrivateFiles
{
    internal static readonly string[] Names = {
        "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json",
        "jtn.recovery-primary.v1.json", "jtn.recovery-backup.v1.json", "jtn.recovery-pending.v1.json", "jtn.recovery-write.v1.json"
    };
    internal const int MaximumFileBytes = 131072, MaximumFixtureBytes = 2097152;
    readonly int fileByteLimit, fixtureByteLimit;
    internal string Workspace { get; }
    internal string Root { get; }
    internal PrivateFiles(string workspace, string runGroup = "milestone-storage", int fileByteLimit = MaximumFileBytes, int fixtureByteLimit = MaximumFixtureBytes)
    {
        if (runGroup != "milestone-storage" || fileByteLimit < MaximumFileBytes || fileByteLimit > 524288 || fixtureByteLimit < MaximumFixtureBytes || fixtureByteLimit > 4194304) throw new ArgumentException("Invalid bounded private run configuration");
        this.fileByteLimit = fileByteLimit; this.fixtureByteLimit = fixtureByteLimit;
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This check qualifies Windows file-sharing semantics only");
        if (!Path.IsPathFullyQualified(workspace) || !Path.GetPathRoot(workspace).Equals("X:\\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Explicit absolute X: workspace required");
        Workspace = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar);
        if (!Workspace.Equals(Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Run from the explicit source-only workspace");
        RejectReparse(Workspace);
        if ((!File.Exists(Path.Combine(Workspace, ".git")) && !Directory.Exists(Path.Combine(Workspace, ".git"))) || !File.Exists(Path.Combine(Workspace, "Checks/MilestoneStorage/MilestoneStorageChecks.csproj"))) throw new ArgumentException("An isolated source worktree with this harness is required");
        string parent = Path.Combine(Workspace, ".work", runGroup);
        RejectReparse(parent);
        if (Directory.Exists(parent))
        {
            var prior = Directory.GetDirectories(parent);
            foreach (string item in prior) RejectReparse(item);
            if (prior.Length >= 3) throw new IOException("Three preserved runs exist; inspect exact task retention before another run");
        }
        Root = Path.GetFullPath(Path.Combine(parent, "run-" + Guid.NewGuid().ToString("N")));
        RejectReparse(Root); Directory.CreateDirectory(Root);
    }
    internal static void RejectReparse(string path)
    {
        for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Private checks do not follow reparse targets"); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal string Inside(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Fixture path escapes the exact private run");
        RejectReparse(full); return full;
    }
    internal string Case(string name)
    {
        if (name.Length < 1 || name.Length > 40 || name.Any(c => !(c >= 'a' && c <= 'z') && c != '-')) throw new ArgumentException("Invalid fixture case name");
        string path = Inside(Path.Combine(Root, name)); Directory.CreateDirectory(path); return path;
    }
    internal string PathAt(string directory, int index) { return Inside(Path.Combine(directory, Names[index])); }
    internal void Put(string directory, int index, string text)
    {
        string path = PathAt(directory, index);
        if (Encoding.UTF8.GetByteCount(text) > fileByteLimit) throw new IOException("Fixture too large");
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
    internal Dictionary<string, string> Inventory(string directory)
    {
        Inside(directory);
        if (Directory.GetDirectories(directory).Length != 0) throw new IOException("Unexpected nested fixture directory");
        var files = Directory.GetFiles(directory);
        if (files.Length > Names.Length) throw new IOException("Fixture file budget exceeded");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in files.OrderBy(x => x, StringComparer.Ordinal))
        {
            Inside(path);
            if (!Names.Contains(Path.GetFileName(path)) || new FileInfo(path).Length > fileByteLimit) throw new IOException("Unexpected or oversized fixture file");
            result.Add(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) + ":" + File.GetLastWriteTimeUtc(path).Ticks);
        }
        return result;
    }
    internal long CheckBudget()
    {
        var cases = Directory.GetDirectories(Root);
        if (cases.Length > 8) throw new IOException("Fixture case budget exceeded");
        long bytes = 0;
        foreach (string directory in cases)
        {
            Inventory(directory);
            foreach (string path in Directory.GetFiles(directory)) bytes += new FileInfo(path).Length;
        }
        if (bytes > fixtureByteLimit) throw new IOException("Private fixture budget exceeded");
        return bytes;
    }
}
