using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon;
using JunkToNeon.Core;
using JunkToNeon.Guidance;

internal static class Program
{
    const int Iterations = 64, Rounds = 8;
    static readonly JsonStateCodec Codec = new JsonStateCodec();
    static object sink;
    static int assertions;
    sealed class Storage : ISnapshotStorage
    {
        public string Raw;
        public int Writes;
        public string ReadPrimary() => Raw;
        public string ReadBackup() => null;
        public void Write(string value) { Writes++; Raw = value; }
    }
    readonly struct Result
    {
        public readonly GuideState Progress;
        public readonly GuideEvaluation Evaluation;
        public readonly bool Transition;
        public Result(GuideState progress, GuideEvaluation evaluation, bool transition)
        { Progress = progress; Evaluation = evaluation; Transition = transition; }
    }
    static void Need(bool value, string why)
    { assertions++; if (!value) throw new InvalidOperationException(why); }

    // Exact non-Busy RenderJourney query sequence at baseline 53276a2:
    // null guard, Guidance (three snapshot reads), Journey, Chapter.
    // No Unity setters or entire-frame timing is simulated here.
    static Result Before(GameSession session)
    {
        if (session?.State == null) throw new InvalidOperationException("Missing live state");
        var evaluation = session.Guidance;
        var progress = session.Journey.Guidance;
        return new Result(progress, evaluation, session.Chapter.TransitionPending);
    }
    static Result After(GameSession session)
    {
        var snapshot = session?.State;
        if (snapshot == null) throw new InvalidOperationException("Missing live state");
        var view = JtnJourneyView.FromSnapshot(snapshot);
        return new Result(view.Progress, view.Evaluation, view.TransitionPending);
    }
    static string Identity(Result value) => JsonSerializer.Serialize(new
    {
        progress = GuideStateCodec.Encode(value.Progress),
        value.Evaluation.Suggestion?.HintId, value.Evaluation.Suggestion?.Title,
        value.Evaluation.Suggestion?.Body, value.Evaluation.Suggestion?.BlocksActions,
        project = value.Evaluation.LastCompletion?.ProjectId,
        sequence = value.Evaluation.LastCompletion?.Sequence, value.Transition
    });
    static object Measure(GameSession session, bool after)
    {
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < Iterations; i++)
        {
            var value = after ? After(session) : Before(session);
            sink = value.Evaluation;
        }
        double micros = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency / Iterations;
        return new { microsecondsPerQuery = micros, bytesPerQuery = (GC.GetAllocatedBytesForCurrentThread() - allocation) / (double)Iterations };
    }
    static object Check(string name, ModelState state, string expectedHint = null, bool? expectedTransition = null)
    {
        var storage = new Storage { Raw = Codec.Encode(state) };
        var game = GameSession.Open(new GameStore(storage, Codec), 1);
        Need(!game.RecoveryRequired, name + ": cannot open private memory fixture: " + game.Error);
        string encoded = Codec.Encode(game.State), raw = storage.Raw;
        int writes = storage.Writes;
        var before = Before(game); var after = After(game);
        string identity = Identity(before);
        Need(identity == Identity(after), name + ": output differs");
        if (expectedHint != null) Need((after.Evaluation.Suggestion?.HintId ?? "<none>") == expectedHint, name + ": independent hint expectation");
        if (expectedTransition.HasValue) Need(after.Transition == expectedTransition.Value, name + ": independent transition expectation");
        var copy = game.State; var retained = JtnJourneyView.FromSnapshot(copy);
        copy.coins = 0; copy.seed = 9; copy.chapter = null; copy.journey = null;
        Need(Identity(new Result(retained.Progress, retained.Evaluation, retained.TransitionPending)) == identity, name + ": mutable snapshot escaped");
        for (int i = 0; i < 16; i++) { Before(game); After(game); }
        var measurements = new List<object>();
        // ABBA / BAAB paired order, bounded at 512 measured queries per variant/fixture.
        for (int round = 0; round < Rounds; round++)
        {
            bool aFirst = round % 4 == 0 || round % 4 == 3;
            object first = Measure(game, !aFirst), second = Measure(game, aFirst);
            measurements.Add(new { round, order = aFirst ? "before-after" : "after-before", before = aFirst ? first : second, after = aFirst ? second : first });
        }
        Need(storage.Writes == writes && storage.Raw == raw && Codec.Encode(game.State) == encoded, name + ": read changed persisted state");
        return new { name, identity, encodedSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(encoded))), measurements };
    }
    static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
            var results = new List<object>(); var inputs = new Dictionary<string, string>();
            results.Add(Check("legacy-fresh", ModelState.Fresh(1), "<none>", false));
            var fresh = ModelState.Fresh(1); fresh.journey = JourneyState.Fresh();
            results.Add(Check("modern-fresh", fresh, GuideIds.Welcome, false));
            foreach (string path in new[] {
                "Checks/Milestones/fixtures/v2-selected540.json",
                "Checks/Milestones/fixtures/late23-skills6.json",
                "Checks/Guidance/fixtures/late-repair-era-committed.json",
                "Checks/Guidance/fixtures/late-repair-final-committed.json",
                "Checks/OptionalOrderStorage/fixtures/ready-sort-no-claim.json" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, path));
                inputs[path] = Convert.ToHexString(SHA256.HashData(bytes));
                results.Add(Check(path, Codec.Decode(System.Text.Encoding.UTF8.GetString(bytes)), expectedTransition: false));
            }
            bool rejected = false;
            try { JtnJourneyView.FromSnapshot(null); } catch (ArgumentNullException) { rejected = true; }
            Need(rejected, "Null snapshot was accepted");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonSerializer.Serialize(new { baseline = "53276a25197131c463590eef6daecb673375947f", runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                iterationsPerBlock = Iterations, pairedRounds = Rounds, warmupsPerVariant = 16, assertions, inputs, results,
                scope = "Pure synchronous live Journey query only; memory storage; no Unity/Android/frame/durable-write timing" }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS " + assertions + " scoped assertions; seven fixture query comparisons; " + output);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
