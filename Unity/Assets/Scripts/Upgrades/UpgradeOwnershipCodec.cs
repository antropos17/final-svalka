using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JunkToNeon.Upgrades
{
    // Small bounded JSON payload, independent of the shared save codec. Null means absent legacy field.
    public static class UpgradeOwnershipCodec
    {
        public const int MaximumJsonLength = 512;
        static readonly Regex Pair = new Regex("\\G[ \\t\\r\\n]*\"([A-Za-z]+)\"[ \\t\\r\\n]*:[ \\t\\r\\n]*(-?(?:0|[1-9][0-9]*))[ \\t\\r\\n]*([,}])", RegexOptions.CultureInvariant);
        static bool Space(char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n'; }
        public static string Encode(UpgradeOwnership state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string N(long n) => n.ToString(CultureInfo.InvariantCulture);
            return "{\"version\":" + N(state.Version) + ",\"level\":" + N(state.Level) + ",\"revision\":" + N(state.Revision) +
                ",\"sequence\":" + N(state.PurchaseSequence) + ",\"walletBefore\":" + N(state.PurchaseWalletBefore) +
                ",\"houseStage\":" + N(state.PurchaseHouseStage) + "}";
        }
        public static UpgradeOwnership DecodeOrDefault(string json)
        {
            if (json == null) return UpgradeOwnership.Empty;
            if (json.Length > MaximumJsonLength) throw new FormatException("Upgrade payload too large");
            int start = 0; while (start < json.Length && Space(json[start])) start++;
            if (start == json.Length || json[start++] != '{') throw new FormatException("Expected upgrade object");
            var fields = new Dictionary<string, long>(StringComparer.Ordinal);
            bool ended = false;
            while (!ended)
            {
                var match = Pair.Match(json, start);
                if (!match.Success || match.Index != start || !long.TryParse(match.Groups[2].Value,
                    NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)) throw new FormatException("Invalid upgrade integer/object");
                string key = match.Groups[1].Value;
                if (key != "version" && key != "level" && key != "revision" && key != "sequence" && key != "walletBefore" && key != "houseStage")
                    throw new FormatException("Unknown upgrade field");
                if (fields.ContainsKey(key)) throw new FormatException("Duplicate upgrade field");
                fields.Add(key, value); ended = match.Groups[3].Value == "}"; start = match.Index + match.Length;
            }
            while (start < json.Length && Space(json[start])) start++;
            if (start != json.Length || fields.Count != 6) throw new FormatException("Invalid upgrade shape/trailing content");
            try
            {
                return new UpgradeOwnership(checked((int)fields["version"]), checked((int)fields["level"]), fields["revision"],
                    fields["sequence"], fields["walletBefore"], checked((int)fields["houseStage"]));
            }
            catch (Exception e) when (e is ArgumentException || e is OverflowException)
            { throw new FormatException("Invalid bounded upgrade state", e); }
        }
    }
}
