using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using JunkToNeon.Core;

namespace JunkToNeon.SaveInspector
{
    public static class ExitCodes
    {
        public const int Ok = 0, Internal = 1, Usage = 2, InputRefused = 3, SnapshotRejected = 4,
            PreviewNotApplicable = 5, PreviewFailed = 6, SelfTestFailed = 7, IntegrityViolation = 8;
    }

    public static class Cli
    {
        public const string ToolName = "junk-save-inspector";
        public const string ToolVersion = "1.6.0";
        // A build-time manifest identifies actual linked bytes, including dirty builds.
        // A historical Git pin cannot truthfully identify sources linked by wildcards.
        static readonly Obj BuildIdentity = ProductionIdentity.Read();
        public const string Usage =
            "usage:\n" +
            "  junk-save-inspector inspect --workspace <dir> --input <copied-snapshot> [--format text|json]\n" +
            "  junk-save-inspector diff --workspace <dir> --before <copy-a> --after <copy-b> [--format text|json]\n" +
            "  junk-save-inspector preview-pending --workspace <dir> --input <copied-snapshot> [--format text|json]\n" +
            "  junk-save-inspector self-test --repo <repository-root> --workspace <empty-private-dir> [--format text|json]\n" +
            "Inputs must be explicit copies inside --workspace. Nothing is written; reports go to stdout.\n";

        static readonly JsonStateCodec Codec = new JsonStateCodec();

        public static int Run(string[] args, Stream stdout)
        {
            Dictionary<string, string> options;
            string command = args.Length == 0 ? null : args[0];
            try { options = Parse(args); }
            catch (ArgumentException) { return UsageError(stdout); }
            string format = options.TryGetValue("format", out var f) ? f : "text";
            if (format != "text" && format != "json") return UsageError(stdout);
            try
            {
                switch (command)
                {
                    case "inspect": return Need(options, "workspace", "input") ? Inspect(options, format, stdout) : UsageError(stdout);
                    case "diff": return Need(options, "workspace", "before", "after") ? Diff(options, format, stdout) : UsageError(stdout);
                    case "preview-pending": return Need(options, "workspace", "input") ? Preview(options, format, stdout) : UsageError(stdout);
                    case "self-test": return Need(options, "repo", "workspace") ? SelfTest.Run(options["repo"], options["workspace"], format, stdout) : UsageError(stdout);
                    default: return UsageError(stdout);
                }
            }
            catch (InputRefused refused)
            {
                Emit(stdout, format, Envelope(command, "input-refused").Add("error", new Obj().Add("category", refused.Category).Add("detail", refused.Message)));
                return ExitCodes.InputRefused;
            }
            catch (Exception error) when (!(error is OutOfMemoryException))
            {
                Emit(stdout, format, Envelope(command, "internal-error").Add("error", new Obj().Add("category", "tool-internal").Add("type", error.GetType().Name)));
                return ExitCodes.Internal;
            }
        }

        static Dictionary<string, string> Parse(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || result.ContainsKey(key.Substring(2)))
                    throw new ArgumentException("Malformed option");
                string name = key.Substring(2);
                if (name != "workspace" && name != "input" && name != "before" && name != "after" && name != "format" && name != "repo")
                    throw new ArgumentException("Unknown option");
                result[name] = args[++i];
            }
            return result;
        }

        static bool Need(Dictionary<string, string> options, params string[] keys)
        {
            foreach (string key in keys) if (!options.ContainsKey(key)) return false;
            return true;
        }

        static int UsageError(Stream stdout)
        {
            Write(stdout, System.Text.Encoding.ASCII.GetBytes(Usage));
            return ExitCodes.Usage;
        }

        public static Obj Envelope(string command, string status)
        {
            return new Obj().Add("tool", new Obj().Add("name", ToolName).Add("version", ToolVersion).Add("productionSources", BuildIdentity))
                .Add("command", command).Add("status", status);
        }

        public static void Emit(Stream stdout, string format, Obj report)
        {
            Write(stdout, format == "json" ? Report.Json(report) : Report.Text(report));
        }

        static void Write(Stream stdout, byte[] bytes) { stdout.Write(bytes, 0, bytes.Length); stdout.Flush(); }

        // Stable categories by exception type. Production messages are never echoed verbatim:
        // only an ASCII prefix before the first ':' or '(' survives, so no input-derived key,
        // id or payload fragment can reach the report.
        public static Obj Rejection(Exception error)
        {
            string category = error is FormatException ? "json-format-or-shape"
                : error is OverflowException ? "numeric-range"
                : error is InvalidOperationException ? "production-validation"
                : error is ArgumentException ? "production-invariant"
                : "production-other";
            string message = error.Message ?? "";
            int cut = message.IndexOfAny(new[] { ':', '(' });
            if (cut >= 0) message = message.Substring(0, cut);
            message = Report.Ascii(message).Trim();
            if (message.Length > 96) message = message.Substring(0, 96);
            return new Obj().Add("category", category).Add("productionReason", message);
        }

        public sealed class Decoded
        {
            public LoadedInput Input;
            public ModelState State;
            public Exception Error;
        }

        public static Decoded Decode(LoadedInput input)
        {
            var result = new Decoded { Input = input };
            try { result.State = Codec.Decode(input.Text); GameLogic.Validate(result.State); }
            catch (Exception error) when (error is FormatException || error is InvalidOperationException || error is ArgumentException || error is OverflowException)
            { result.State = null; result.Error = error; }
            return result;
        }

        public static string Canonical(ModelState state) { return Codec.Encode(state); }

        static Obj InputBlock(LoadedInput input, ModelState state, out bool intact)
        {
            var o = new Obj().Add("role", input.Role).Add("bytes", (long)input.Bytes.Length).Add("sha256", input.Sha256);
            o.Add("canonicalSha256", state == null ? null : InputGuard.HashText(Canonical(state)));
            o.Add("integrity", InputGuard.Integrity(input, out intact));
            return o;
        }

        // Whether the raw document has a non-null top-level "pending" member. A hint only, used to
        // label a rejected snapshot; nothing else from a rejected snapshot is reported.
        static object PendingHint(LoadedInput input)
        {
            try
            {
                using (var doc = JsonDocument.Parse(input.Text, new JsonDocumentOptions { MaxDepth = 8 }))
                    return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("pending", out var p) ? (object)(p.ValueKind != JsonValueKind.Null) : false;
            }
            catch (JsonException) { return "unknown"; }
        }

        static int Inspect(Dictionary<string, string> o, string format, Stream stdout)
        {
            var input = InputGuard.Load("input", o["workspace"], o["input"]);
            var decoded = Decode(input);
            Obj report;
            int code;
            if (decoded.State == null)
            {
                report = Envelope("inspect", "snapshot-rejected").Add("error", Rejection(decoded.Error))
                    .Add("pendingFieldPresentHint", PendingHint(input))
                    .Add("partialFacts", "none by design: production rejects the whole snapshot");
                code = ExitCodes.SnapshotRejected;
            }
            else
            {
                report = Envelope("inspect", "ok").Add("facts", Facts.Extract(decoded.State)).Add("notes", InspectNotes());
                code = ExitCodes.Ok;
            }
            report.Add("input", InputBlock(input, decoded.State, out bool intact));
            return Finish(stdout, format, report, intact, code);
        }

        static int Finish(Stream stdout, string format, Obj report, bool intact, int code)
        {
            if (!intact) { report.Add("integrityViolation", true); code = ExitCodes.IntegrityViolation; }
            Emit(stdout, format, report);
            return code;
        }

        static List<object> InspectNotes()
        {
            return new List<object> {
                "Read-only: decoded with production JsonStateCodec.Decode and GameLogic.Validate; no migration, settlement, recovery or acknowledgement was persisted.",
                "projected-not-persisted sections are production's in-memory projection of absent aggregates; inspection does not adopt them.",
                "Offline clock fields are shown as stored; elapsed time is not settled against the current wall clock.",
                "Guidance observations and acknowledgements record committed operations; they do not prove that any text was visibly seen.",
                "lastSpin and latestClaim are retained historical receipts, not new reward proposals." };
        }

        static int Diff(Dictionary<string, string> o, string format, Stream stdout)
        {
            var before = InputGuard.Load("before", o["workspace"], o["before"]);
            var after = InputGuard.Load("after", o["workspace"], o["after"]);
            var a = Decode(before); var b = Decode(after);
            Obj report; int code;
            if (a.State == null || b.State == null)
            {
                report = Envelope("diff", "snapshot-rejected");
                if (a.State == null) report.Add("beforeError", Rejection(a.Error));
                if (b.State == null) report.Add("afterError", Rejection(b.Error));
                code = ExitCodes.SnapshotRejected;
            }
            else
            {
                report = Envelope("diff", "ok");
                SemanticDiff.Describe(a.State, b.State, report);
                code = ExitCodes.Ok;
            }
            report.Add("inputs", new List<object> { InputBlock(before, a.State, out bool ia), InputBlock(after, b.State, out bool ib) });
            return Finish(stdout, format, report, ia && ib, code);
        }

        static int Preview(Dictionary<string, string> o, string format, Stream stdout)
        {
            var input = InputGuard.Load("input", o["workspace"], o["input"]);
            var decoded = Decode(input);
            Obj report; int code;
            if (decoded.State == null)
            {
                report = Envelope("preview-pending", "snapshot-rejected").Add("error", Rejection(decoded.Error))
                    .Add("pendingFieldPresentHint", PendingHint(input))
                    .Add("preview", "refused: an invalid frozen operation is never repaired or replayed");
                code = ExitCodes.SnapshotRejected;
            }
            else code = PendingPreview.Describe(decoded.State, out report);
            report.Add("input", InputBlock(input, decoded.State, out bool intact));
            return Finish(stdout, format, report, intact, code);
        }
    }
}
