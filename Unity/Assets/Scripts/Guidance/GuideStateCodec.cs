using System;
using System.Globalization;

namespace JunkToNeon.Guidance
{
    // A strict canonical nested payload string, not a file or an independent save.
    // Every string token is a registered guide/project ID or bounded digest; none
    // can contain '|'. Core distinguishes a missing aggregate from PRESENT null.
    public static class GuideStateCodec
    {
        public const int MaximumLength = 4096;
        public static string Encode(GuideState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string encoded = string.Join("|", new[] { "g1", N((int)state.Origin), N(state.AcknowledgementBits), N(state.TutorialBits),
                N(state.LastObservedSequence), state.LastObservationKey ?? "-", state.LastCompletion == null ? "-" : state.LastCompletion.ProjectId,
                N(state.LastCompletion == null ? 0 : state.LastCompletion.Sequence), N(state.LastAckSequence), state.LastAckHintId ?? "-", N(state.CompletionAcknowledgedSequence) });
            if (encoded.Length > MaximumLength) throw new FormatException("Guide state exceeds bounded payload");
            return encoded;
        }
        public static GuideState DecodeOrDefault(string payload, GuideOrigin missingOrigin)
        {
            return payload == null ? GuideState.Create(missingOrigin) : Decode(payload);
        }
        public static GuideState Decode(string payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaximumLength) throw new FormatException("Invalid guide payload size");
            var fields = payload.Split('|');
            if (fields.Length != 11 || fields[0] != "g1") throw new FormatException("Invalid guide payload/version");
            try
            {
                long completionSequence = Number(fields[7]);
                if (fields[6] == "-" && completionSequence != 0) throw new FormatException("Completion identity missing");
                var completion = fields[6] == "-" ? null : new ProjectCompletion(fields[6], completionSequence);
                var state = new GuideState((GuideOrigin)checked((int)Number(fields[1])), checked((int)Number(fields[2])), checked((int)Number(fields[3])), Number(fields[4]),
                    fields[5] == "-" ? null : fields[5], completion, Number(fields[8]), fields[9] == "-" ? null : fields[9], Number(fields[10]));
                if (Encode(state) != payload) throw new FormatException("Noncanonical guide payload");
                return state;
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException)
            { throw new FormatException("Invalid guide payload fields", error); }
        }
        static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        static long Number(string value)
        {
            long result;
            if (value.Length == 0 || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) || result < 0 || N(result) != value) throw new FormatException("Expected canonical nonnegative integer");
            return result;
        }
    }
}
