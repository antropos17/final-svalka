using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JunkToNeon.SaveInspector
{
    public sealed class InputRefused : Exception
    {
        public string Category { get; }
        public InputRefused(string category, string detail) : base(detail) { Category = category; }
    }

    // One explicitly named, copied snapshot inside an explicit workspace. Read once,
    // read-only; the same buffer is hashed and decoded.
    public sealed class LoadedInput
    {
        public string Role { get; }
        public string FullPath { get; }
        public byte[] Bytes { get; }
        public string Sha256 { get; }
        public string Text { get; }
        public DirectoryFingerprint Neighbours { get; }
        public LoadedInput(string role, string fullPath, byte[] bytes, string text, DirectoryFingerprint neighbours)
        {
            Role = role; FullPath = fullPath; Bytes = bytes; Text = text; Neighbours = neighbours;
            Sha256 = InputGuard.Hash(bytes);
        }
    }

    // Names and sizes of the input's directory entries, to prove no adjacent
    // save/recovery/quarantine file appeared during analysis.
    public sealed class DirectoryFingerprint
    {
        readonly SortedDictionary<string, long> entries;
        DirectoryFingerprint(SortedDictionary<string, long> entries) { this.entries = entries; }
        public static DirectoryFingerprint Capture(string directory)
        {
            var map = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                map[entry.Name] = entry is FileInfo file ? file.Length : -1;
            return new DirectoryFingerprint(map);
        }
        public bool SameAs(DirectoryFingerprint other)
        {
            if (other == null || other.entries.Count != entries.Count) return false;
            foreach (var pair in entries)
                if (!other.entries.TryGetValue(pair.Key, out long size) || size != pair.Value) return false;
            return true;
        }
    }

    public static class InputGuard
    {
        // Production's Reader rejects snapshots over 65536 UTF-16 characters. FileSnapshotStorage
        // reads with File.ReadAllText, which honours UTF-8/UTF-16/UTF-32 byte-order marks. The
        // largest byte form of a 65536-character text is UTF-32: 4 bytes per character plus a
        // 4-byte BOM. This pre-check never rejects bytes production could accept; the
        // production Reader remains the authoritative character bound.
        public const int ProductionMaximumCharacters = 65536;
        public const long MaximumInputBytes = 4L * ProductionMaximumCharacters + 4;

        static readonly string[] DeviceStems = { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³" };

        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Convert.ToHexString(sha.ComputeHash(bytes));
        }

        public static string HashText(string text) { return Hash(Encoding.UTF8.GetBytes(text)); }

        // Pure string checks, unit-tested without touching the filesystem.
        public static void CheckPathText(string path, string what)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InputRefused("usage-missing-path", what + " path is required");
            if (path.IndexOf('\0') >= 0) throw new InputRefused("device-path", what + " path contains a NUL character");
            string slashed = path.Replace('/', '\\');
            if (slashed.StartsWith("\\\\", StringComparison.Ordinal)) throw new InputRefused("device-path", what + " path is a UNC or device path");
            int colon = slashed.IndexOf(':');
            if (colon >= 0 && (colon != 1 || slashed.IndexOf(':', 2) >= 0)) throw new InputRefused("alternate-stream", what + " path names a stream or device");
            foreach (string segment in slashed.Split('\\'))
            {
                if (segment.Length == 0 || segment == "." || segment == ".." || (segment.Length == 2 && segment[1] == ':')) continue;
                if (segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                    throw new InputRefused("device-path", what + " path segment has a trailing dot or space");
                string stem = segment;
                int dot = stem.IndexOf('.');
                if (dot >= 0) stem = stem.Substring(0, dot);
                stem = stem.TrimEnd(' ').ToUpperInvariant();
                foreach (string device in DeviceStems)
                    if (stem == device) throw new InputRefused("device-path", what + " path names a reserved device");
            }
        }

        // The normal game's Unity persistentDataPath on Windows lives under AppData\LocalLow.
        public static bool IsProtectedLocation(string fullPath, string userProfile)
        {
            string normal = fullPath.Replace('/', '\\').TrimEnd('\\') + "\\";
            if (normal.IndexOf("\\AppData\\LocalLow\\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(userProfile))
            {
                string low = Path.Combine(userProfile, "AppData", "LocalLow").Replace('/', '\\').TrimEnd('\\') + "\\";
                if (normal.StartsWith(low, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static bool IsLink(FileSystemInfo info)
        {
            return (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget != null;
        }

        // Missing leaves are allowed during self-test setup, but every existing ancestor
        // must be checked before CreateDirectory can follow it into another location.
        public static string ValidateWorkspacePath(string workspace)
        {
            CheckPathText(workspace, "Workspace");
            string full = Path.GetFullPath(workspace).TrimEnd('\\', '/');
            CheckPathText(full, "Workspace");
            if (IsProtectedLocation(full, Environment.GetEnvironmentVariable("USERPROFILE")))
                throw new InputRefused("protected-location", "Workspace is inside the normal game data location");
            var ancestors = new Stack<DirectoryInfo>();
            for (var cursor = new DirectoryInfo(full); cursor != null; cursor = cursor.Parent) ancestors.Push(cursor);
            while (ancestors.Count != 0)
            {
                var cursor = ancestors.Pop();
                FileAttributes attributes;
                try { attributes = File.GetAttributes(cursor.FullName); }
                catch (FileNotFoundException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0 || cursor.LinkTarget != null)
                    throw new InputRefused("reparse-point", "Workspace path contains a reparse point");
                if ((attributes & FileAttributes.Directory) == 0)
                    throw new InputRefused("not-a-directory", "Workspace path contains a non-directory ancestor");
            }
            return full;
        }

        public static string ResolveWorkspace(string workspace)
        {
            string full = ValidateWorkspacePath(workspace);
            if (!Directory.Exists(full)) throw new InputRefused("workspace-missing", "Workspace directory does not exist");
            return full;
        }

        public static LoadedInput Load(string role, string workspace, string input)
        {
            string root = ResolveWorkspace(workspace);
            CheckPathText(input, "Input");
            string full = Path.GetFullPath(Path.IsPathRooted(input) ? input : Path.Combine(root, input));
            CheckPathText(full, "Input");
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InputRefused("outside-workspace", "Input is not inside the workspace");
            if (IsProtectedLocation(full, Environment.GetEnvironmentVariable("USERPROFILE")))
                throw new InputRefused("protected-location", "Input is inside the normal game data location");
            if (Directory.Exists(full)) throw new InputRefused("not-a-file", "Input is a directory");
            var file = new FileInfo(full);
            if (!file.Exists) throw new InputRefused("missing", "Input file does not exist");
            if (IsLink(file)) throw new InputRefused("reparse-point", "Input is a reparse point or link");
            for (var cursor = file.Directory; cursor != null && cursor.FullName.Length > root.Length; cursor = cursor.Parent)
                if (IsLink(cursor)) throw new InputRefused("reparse-point", "Input path contains a reparse point");
            if (file.Length == 0) throw new InputRefused("empty", "Input file is empty");
            if (file.Length > MaximumInputBytes) throw new InputRefused("too-large", "Input exceeds the byte bound derived from the production snapshot limit");
            var neighbours = DirectoryFingerprint.Capture(file.DirectoryName);
            byte[] bytes = ReadBounded(full);
            return new LoadedInput(role, full, bytes, DecodeLikeProduction(bytes), neighbours);
        }

        static byte[] ReadBounded(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                return ReadBounded(stream, null);
        }

        static byte[] ReadBounded(Stream stream, long? expectedLength)
        {
            long limit = expectedLength ?? MaximumInputBytes;
            if (stream.Length > MaximumInputBytes || expectedLength.HasValue && stream.Length != expectedLength.Value)
                throw new InputRefused("input-changed", "Input length changed before bounded reading");
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                int read;
                while ((read = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, limit - buffer.Length + 1))) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > limit) throw new InputRefused("too-large", "Input grew beyond the bounded read length");
                }
                if (expectedLength.HasValue && (buffer.Length != expectedLength.Value || stream.Length != expectedLength.Value))
                    throw new InputRefused("input-changed", "Input length changed during bounded reading");
                return buffer.ToArray();
            }
        }

        // Same text conversion as File.ReadAllText in FileSnapshotStorage: UTF-8 default with
        // byte-order-mark detection and replacement of invalid sequences.
        public static string DecodeLikeProduction(byte[] bytes)
        {
            using (var reader = new StreamReader(new MemoryStream(bytes, false), Encoding.UTF8, true)) return reader.ReadToEnd();
        }

        public static Obj Integrity(LoadedInput input, out bool intact)
        { return Integrity(input, path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan), out intact); }

        // The opener seam makes replacement/growth races reproducible with private fixtures;
        // normal callers always use the read-only FileStream above.
        internal static Obj Integrity(LoadedInput input, Func<string, Stream> openRead, out bool intact)
        {
            bool sameBytes = false; long size = -1; string hash = null; bool sameNeighbours = false;
            try
            {
                var file = new FileInfo(input.FullPath);
                ValidateWorkspacePath(file.DirectoryName);
                if (file.Exists)
                {
                    if (IsLink(file)) throw new InputRefused("reparse-point", "Input became a reparse point");
                    size = file.Length;
                    // A length mismatch already proves a violation. Do not open/hash a changed
                    // file, especially one that has grown beyond the original input bound.
                    if (size == input.Bytes.LongLength && size <= MaximumInputBytes)
                        using (var stream = openRead(input.FullPath))
                        {
                            size = stream.Length;
                            byte[] bytes = ReadBounded(stream, input.Bytes.LongLength);
                            hash = Hash(bytes);
                            sameBytes = hash == input.Sha256;
                        }
                }
                sameNeighbours = input.Neighbours.SameAs(DirectoryFingerprint.Capture(Path.GetDirectoryName(input.FullPath)));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InputRefused) { }
            intact = sameBytes && sameNeighbours;
            return new Obj().Add("bytesAfter", size).Add("sha256After", hash).Add("unchanged", sameBytes).Add("adjacentEntriesUnchanged", sameNeighbours);
        }
    }
}
