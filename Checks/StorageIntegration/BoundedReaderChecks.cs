using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JunkToNeon.Core;

internal static class BoundedReaderChecks
{
    internal static void Run(string workspace)
    {
        var files = new PrivateFiles(workspace, "bounded-reader-r10", BoundedSnapshotReader.MaximumEncodedBytes + 4, 4194304);
        var groups = new List<object>();
        var codec = new JsonStateCodec();
        void Need(bool ok, string detail) { if (!ok) throw new Exception(detail); }
        void Limit(Action action) { try { action(); } catch (SnapshotSizeLimitException) { return; } throw new Exception("Expected bounded-read limit"); }
        void Put(string directory, int index, byte[] bytes)
        {
            if (bytes.Length > BoundedSnapshotReader.MaximumEncodedBytes + 4) throw new Exception("Reader fixture exceeds byte budget");
            File.WriteAllBytes(files.PathAt(directory, index), bytes);
        }
        byte[] Encode(Encoding encoding, string value) { return encoding.GetPreamble().Concat(encoding.GetBytes(value)).ToArray(); }
        string active = "encoding-boundaries";
        try
        {
            var encodings = new[] { new UTF8Encoding(true), (Encoding)new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UTF32Encoding(false, true), new UTF32Encoding(true, true) };
            string[] labels = { "utf-eight", "utf-sixteen-le", "utf-sixteen-be", "utf-thirtytwo-le", "utf-thirtytwo-be" };
            string maximum = new string('\u20ac', 65536);
            for (int i = 0; i < encodings.Length; i++)
            {
                string directory = files.Case(labels[i]); var encoding = encodings[i];
                Put(directory, 0, Encode(encoding, maximum)); Put(directory, 1, Encode(encoding, maximum + "x"));
                var encodedInventory = files.Inventory(directory);
                Need(BoundedSnapshotReader.ReadFile(files.PathAt(directory, 0)) == File.ReadAllText(files.PathAt(directory, 0)) && BoundedSnapshotReader.ReadFile(files.PathAt(directory, 0)).Length == 65536, "Accepted max/BOM decoding differs from existing reader");
                long consumed;
                using (var input = new CountingStream(new FileStream(files.PathAt(directory, 1), FileMode.Open, FileAccess.Read, FileShare.Read, 1)))
                {
                    Limit(() => BoundedSnapshotReader.ReadStream(input, true));
                    Need(input.CanRead, "leaveOpen=true closed source after rejection");
                    consumed = input.BytesRead;
                    Need(consumed <= BoundedSnapshotReader.MaximumEncodedBytes + 1L, "Encoded byte bound exceeded");
                }
                Need(Same(encodedInventory, files.Inventory(directory)), "Read/reject changed original encoded bytes or write metadata");
                groups.Add(new { name = labels[i], maxCharacters = 65536, excessCharacters = 65537, rejected = true, consumedBytes = consumed });
            }

            active = "growing-real-file";
            string growth = files.Case("growing-file");
            Put(growth, 0, new byte[1024].Select(_ => (byte)'a').ToArray());
            long initialBytes = new FileInfo(files.PathAt(growth, 0)).Length;
            long growthBytes;
            using (var input = new CountingStream(new FileStream(files.PathAt(growth, 0), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1)))
            {
                input.AfterFirstRead = () =>
                {
                    using (var append = new FileStream(files.PathAt(growth, 0), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        var extra = new byte[BoundedSnapshotReader.MaximumEncodedBytes + 4 - 1024];
                        Array.Fill(extra, (byte)'a'); append.Write(extra, 0, extra.Length); append.Flush(true);
                    }
                };
                Limit(() => BoundedSnapshotReader.ReadStream(input, true)); growthBytes = input.BytesRead;
                Need(input.BytesRead < new FileInfo(files.PathAt(growth, 0)).Length && input.BytesRead <= BoundedSnapshotReader.MaximumEncodedBytes + 1L, "Growing file was consumed whole or without a hard bound");
            }
            groups.Add(new { name = active, initialBytes, finalBytes = new FileInfo(files.PathAt(growth, 0)).Length, consumedBytes = growthBytes, rejected = true });

            active = "codec-and-backup-presence";
            string compatibility = files.Case("codec-backup");
            string canonical = codec.Encode(ModelState.Fresh(1));
            string padded = canonical + new string(' ', 65536 - canonical.Length);
            Put(compatibility, 0, Encoding.UTF8.GetBytes(padded + " "));
            var actual = new FileSnapshotStorage(compatibility);
            Need(actual.ReadPrimary().Length == 65537, "Baseline unbounded storage boundary did not reproduce");
            Limit(() => BoundedSnapshotReader.ReadFile(files.PathAt(compatibility, 0)));
            File.Move(files.PathAt(compatibility, 0), files.PathAt(compatibility, 1)); // preserve oversized candidate
            Put(compatibility, 0, Encoding.UTF8.GetBytes(padded));
            Need(codec.Decode(BoundedSnapshotReader.ReadFile(files.PathAt(compatibility, 0))).coins == 37, "Codec max65536 semantics changed");
            using (var exclusive = new FileStream(files.PathAt(compatibility, 0), FileMode.Open, FileAccess.Read, FileShare.None))
                Need(BoundedSnapshotReader.ReadStream(exclusive, true) == padded && exclusive.CanRead, "Exclusive backup handle comparison did not retain source");
            // This adapter mirrors the agreed 02 wiring; production GameSession.cs
            // is intentionally unchanged in this worker's commit.
            var bounded = new ProposedBoundedStorage(files, compatibility);
            var before = files.Inventory(compatibility);
            var game = GameSession.Open(new GameStore(bounded, codec), 99);
            Need(!game.RecoveryRequired && game.State.coins == 37 && !game.BackupAvailable && bounded.WriteCalls == 0, "Oversized backup blocked healthy primary or wrote state");
            Need(Same(before, files.Inventory(compatibility)), "Invalid backup/read changed original evidence");
            File.Move(files.PathAt(compatibility, 0), files.PathAt(compatibility, 3)); // preserve valid primary, now deliberately absent
            before = files.Inventory(compatibility);
            game = GameSession.Open(new GameStore(bounded, codec), 99);
            Need(game.RecoveryRequired && bounded.WriteCalls == 0 && !File.Exists(files.PathAt(compatibility, 0)) && Same(before, files.Inventory(compatibility)), "Oversized present backup was treated as absent and granted fresh coins");
            Need(BoundedSnapshotReader.ReadFile(files.PathAt(compatibility, 2)) == null, "Missing-file behavior changed");
            groups.Add(new { name = active, baselineReturnedCharacters = 65537, codecMaxAccepted = true, invalidBackupPresenceRetained = true, healthyPrimaryOpens = true, absentPrimaryBlocksFresh = true, writeCalls = bounded.WriteCalls, wiring = "test adapter; actual GameSession wiring owned by02" });

            active = "replacement-sharing-and-malformed-bytes";
            string sharing = files.Case("sharing-malformed");
            Put(sharing, 0, Encoding.UTF8.GetBytes("snapshot")); Put(sharing, 1, Encoding.UTF8.GetBytes("replacement"));
            var inventory = files.Inventory(sharing); int sharingError = 0;
            using (var input = new CountingStream(new FileStream(files.PathAt(sharing, 0), FileMode.Open, FileAccess.Read, FileShare.Read, 1)))
            {
                input.AfterFirstRead = () =>
                {
                    try { File.Replace(files.PathAt(sharing, 1), files.PathAt(sharing, 0), null); }
                    catch (IOException error) { sharingError = error.HResult & 65535; }
                };
                Need(BoundedSnapshotReader.ReadStream(input, true) == "snapshot" && sharingError == 32, "Existing deny-delete sharing semantics changed");
            }
            Need(Same(inventory, files.Inventory(sharing)), "Attempted replacement altered locked originals");
            using (var locked = new FileStream(files.PathAt(sharing, 0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool denied = false; try { BoundedSnapshotReader.ReadFile(files.PathAt(sharing, 0)); } catch (IOException error) { denied = (error.HResult & 65535) == 32; }
                Need(denied, "Exclusive sharing error was swallowed");
            }
            byte[][] malformed = { new byte[] { 0xef, 0xbb }, new byte[] { 0xff, 0xfe, 0, 0xd8, 0 }, new byte[] { 0, 0, 0xfe, 0xff, 0, 0 }, new byte[] { 0xc3, 0x28, 0xff } };
            for (int i = 0; i < malformed.Length; i++)
            {
                Put(sharing, i + 2, malformed[i]); string path = files.PathAt(sharing, i + 2);
                Need(BoundedSnapshotReader.ReadFile(path) == File.ReadAllText(path), "Malformed BOM/default replacement fallback changed");
            }
            string surrogate = string.Concat(Enumerable.Repeat("\U0001f642", 32768));
            Put(sharing, 6, new UTF8Encoding(false).GetBytes(surrogate));
            Need(BoundedSnapshotReader.ReadFile(files.PathAt(sharing, 6)) == surrogate && surrogate.Length == 65536, "Character cap is not UTF16 units");
            var closedInput = new FileStream(files.PathAt(sharing, 0), FileMode.Open, FileAccess.Read, FileShare.Read);
            Need(BoundedSnapshotReader.ReadStream(closedInput, false) == "snapshot" && !closedInput.CanRead, "leaveOpen=false did not close source");
            groups.Add(new { name = active, replacementError = sharingError, malformedCases = malformed.Length, utf16SurrogateUnits = surrogate.Length, explicitOwnership = true });

            long fixtureBytes = files.CheckBudget();
            var receipt = new { status = "PASS", sourceBase = "1b15ed9", characterCap = BoundedSnapshotReader.MaximumCharacters,
                encodedByteCap = BoundedSnapshotReader.MaximumEncodedBytes, maximumProbeBytes = 1, privateRoot = files.Root, fixtureBytes,
                groups, limits = "Helper + agreed-wiring adapter only; production GameSession.cs unchanged, no Android/power-loss claim" };
            string raw = JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(files.Inside(Path.Combine(files.Root, "receipt.json")), raw);
            foreach (object result in groups) Console.WriteLine("PASS " + JsonSerializer.Serialize(result));
            Console.WriteLine("PASS bounded reader; receipt " + Path.Combine(files.Root, "receipt.json"));
        }
        catch (Exception error)
        {
            File.WriteAllText(files.Inside(Path.Combine(files.Root, "receipt.json")), JsonSerializer.Serialize(new { status = "FAIL", active, type = error.GetType().Name, error.Message, privateRoot = files.Root, groups }, new JsonSerializerOptions { WriteIndented = true }));
            throw;
        }
    }
    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) { return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value); }
}

internal sealed class CountingStream : Stream
{
    readonly Stream source;
    internal long BytesRead;
    internal Action AfterFirstRead;
    bool observed;
    internal CountingStream(Stream source) { this.source = source; }
    public override bool CanRead { get { return source.CanRead; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { throw new NotSupportedException("Length must not decide the bound"); } }
    public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count); BytesRead += read;
        if (!observed && read != 0) { observed = true; AfterFirstRead?.Invoke(); }
        return read;
    }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
    public override void SetLength(long value) { throw new NotSupportedException(); }
    public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
}

internal sealed class ProposedBoundedStorage : ISnapshotStorage
{
    readonly PrivateFiles files;
    readonly string directory;
    readonly FileSnapshotStorage actual;
    internal int WriteCalls;
    internal ProposedBoundedStorage(PrivateFiles files, string directory) { this.files = files; this.directory = directory; actual = new FileSnapshotStorage(directory); }
    public string ReadPrimary() { return BoundedSnapshotReader.ReadFile(files.PathAt(directory, 0)); }
    public string ReadBackup() { try { return BoundedSnapshotReader.ReadFile(files.PathAt(directory, 1)); } catch (SnapshotSizeLimitException) { return ""; } }
    public void Write(string raw) { WriteCalls++; actual.Write(raw); }
}
