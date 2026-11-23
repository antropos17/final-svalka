using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    // Focused private-fixture regressions for the three07 findings. The same cases run
    // alone through the test project and once within the normal integrated self-test.
    internal static class FixRegressions
    {
        internal static List<Obj> Run(string repo, string workspace)
        {
            string root = SelfTest.PrepareWorkspace(repo, workspace);
            var cases = new List<Obj>();
            void Case(string id, Action test)
            {
                var result = new Obj().Add("id", id);
                try { test(); result.Add("passed", true); }
                catch (Exception error) when (!(error is OutOfMemoryException))
                { result.Add("passed", false).Add("failure", error.GetType().Name + ": " + Report.Ascii(error.Message)); }
                cases.Add(result);
            }
            Case("fix01-missing-workspace-link-ancestor", () => {
                string target = Path.Combine(root, "private-target"), alias = Path.Combine(root, "private-alias");
                Directory.CreateDirectory(target);
                string sentinel = Path.Combine(target, "sentinel.txt"); File.WriteAllText(sentinel, "private target unchanged");
                string hash = InputGuard.Hash(File.ReadAllBytes(sentinel));
                Directory.CreateSymbolicLink(alias, target);
                try
                {
                    // The target substitutes for a repository/protected location; neither the
                    // real repository nor LocalLow receives a test write even if a check fails.
                    using (var output = new MemoryStream())
                        Refused(() => SelfTest.Run(target, Path.Combine(alias, "missing", "workspace"), "json", output), "reparse-point");
                    Refused(() => SelfTest.PrepareWorkspace(target, alias), "reparse-point");
                    Require(!Directory.Exists(Path.Combine(target, "missing")) && Directory.GetFileSystemEntries(target).Length == 1 &&
                        InputGuard.Hash(File.ReadAllBytes(sentinel)) == hash, "Link target changed before refusal");
                }
                finally { Directory.Delete(alias); } // unlink only; never recursively traverse a link
                string normal = SelfTest.PrepareWorkspace(repo, Path.Combine(root, "normal", "workspace"));
                Require(Directory.Exists(normal) && InputGuard.ResolveWorkspace(normal) == normal, "Normal created workspace did not validate");
            });
            Case("fix02-valid-retained-chapter-at-difference", () => {
                var codec = new JsonStateCodec();
                string raw = File.ReadAllText(Path.Combine(repo, "Checks/EraMigration/R8/Fixtures/committed-all-skills-spin.json"));
                var before = codec.Decode(raw); var after = before.Copy(); var old = before.lastSpin.chapter;
                Require(old.At == 2000, "Independent timestamp fixture oracle changed");
                after.lastSpin.chapter = new ChapterOperation(2, old.Kind, old.Sequence, 2001, old.ActionId, old.Price, old.CoinsBefore, old.CoinsAfter,
                    old.SeedBefore, old.SeedAfter, old.Symbols, old.BaseCoins, old.WorkshopCoins, old.Before, old.After, old.Effects, old.SkillRequest, old.Settlement);
                GameLogic.Validate(before); GameLogic.Validate(after);
                before = codec.Decode(codec.Encode(before)); after = codec.Decode(codec.Encode(after));
                var report = new Obj(); SemanticDiff.Describe(before, after, report);
                var changes = (List<object>)report.Get("changes");
                Require(!(bool)report.Get("equal") && changes.Count == 1 && (int)report.Get("changeCount") == 1, "Valid canonical difference has no precise change");
                var change = (Obj)changes[0];
                Require((string)change.Get("path") == "rngCarry.lastSpin.components.at" && (string)change.Get("before") == "2000" && (string)change.Get("after") == "2001", "Wrong retained receipt timestamp change");
                Require(!((List<object>)report.Get("inferred")).Cast<string>().Any(x => x.Contains("Only clock/sequence")), "Receipt difference labelled clock-only");
                string ca = Cli.Canonical(before), cb = Cli.Canonical(after);
                var missing = SemanticDiff.ChangesWithCoverage(new Obj(), new Obj(), ca, cb, out var groups, out bool unexplained);
                Require(unexplained && missing.Count == 1 && groups.Contains("format"), "Unexplained canonical difference lost");
                var partialA = new Obj().Add("clockSequence", new Obj().Add("sequence", before.sequence));
                after.sequence++; after.revision += 2; GameLogic.Validate(after);
                var partialB = new Obj().Add("clockSequence", new Obj().Add("sequence", after.sequence));
                SemanticDiff.ChangesWithCoverage(partialA, partialB, ca, Cli.Canonical(after), out groups, out unexplained);
                Require(unexplained && groups.Contains("format"), "Partial clock facts mask an unexplained receipt difference");
            });
            string probe = Path.Combine(root, "probe.json");
            byte[] original = File.ReadAllBytes(Path.Combine(repo, "Checks/EraMigration/Fixtures/r6/fresh.json"));
            LoadedInput Load() { File.WriteAllBytes(probe, original); return InputGuard.Load("probe", root, probe); }
            Case("fix03-length-mismatch-never-opens-input", () => {
                var loaded = Load(); int opens = 0;
                using (var file = new FileStream(probe, FileMode.Open, FileAccess.Write, FileShare.None)) file.SetLength(InputGuard.MaximumInputBytes + 1);
                var report = InputGuard.Integrity(loaded, path => { opens++; throw new InvalidOperationException("Must not open oversized input"); }, out bool intact);
                Require(!intact && opens == 0 && report.Get("sha256After") == null && (long)report.Get("bytesAfter") == InputGuard.MaximumInputBytes + 1, "Oversized changed input was opened/hashed");
                using (var file = new FileStream(probe, FileMode.Open, FileAccess.Write, FileShare.None)) file.SetLength(1);
                InputGuard.Integrity(loaded, path => { opens++; throw new InvalidOperationException("Must not open truncated input"); }, out intact);
                Require(!intact && opens == 0, "Truncated changed input was opened");
            });
            Case("fix04-replacement-and-growth-read-bound", () => {
                var loaded = Load(); CountingStream replacement = null;
                var result = InputGuard.Integrity(loaded, path => {
                    File.Delete(path);
                    using (var writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) writer.SetLength(InputGuard.MaximumInputBytes + 1);
                    replacement = new CountingStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
                    return replacement;
                }, out bool intact);
                Require(!intact && replacement.BytesRead == 0 && result.Get("sha256After") == null, "Post-metadata replacement caused an unbounded read");
                loaded = Load(); var large = new byte[(int)InputGuard.MaximumInputBytes + 8192];
                Array.Copy(original, large, original.Length);
                var growing = new CountingStream(new MemoryStream(large, false), original.Length);
                result = InputGuard.Integrity(loaded, path => growing, out intact);
                Require(!intact && growing.BytesRead == original.Length + 1 && result.Get("sha256After") == null, "Growth read exceeded expected bytes plus one sentinel");
            });
            Case("fix05-same-length-change-and-link-replacement", () => {
                var loaded = Load(); var changed = (byte[])original.Clone(); changed[0] ^= 1; File.WriteAllBytes(probe, changed);
                var result = InputGuard.Integrity(loaded, out bool intact);
                Require(!intact && result.Get("sha256After") != null && (string)result.Get("sha256After") != loaded.Sha256, "Same-length replacement escaped fingerprint check");
                loaded = Load(); string target = Path.Combine(root, "link-target.json"); File.WriteAllBytes(target, original); File.Delete(probe);
                File.CreateSymbolicLink(probe, target);
                try
                {
                    int opens = 0;
                    InputGuard.Integrity(loaded, path => { opens++; throw new InvalidOperationException("Must not follow replaced link"); }, out intact);
                    Require(!intact && opens == 0 && InputGuard.Hash(File.ReadAllBytes(target)) == loaded.Sha256, "Integrity followed a replaced input link");
                }
                finally { File.Delete(probe); }
            });
            return cases;
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Refused(Action operation, string category)
        {
            try { operation(); }
            catch (InputRefused error) { Require(error.Category == category, "Unexpected refusal category"); return; }
            throw new InvalidOperationException("Unsafe workspace was not refused");
        }
        sealed class CountingStream : Stream
        {
            readonly Stream inner; readonly long? reportedLength;
            public long BytesRead { get; private set; }
            public CountingStream(Stream inner, long? reportedLength = null) { this.inner = inner; this.reportedLength = reportedLength; }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return reportedLength ?? inner.Length; } }
            public override long Position { get { return inner.Position; } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { int got = inner.Read(buffer, offset, count); BytesRead += got; return got; }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        }
    }
}
