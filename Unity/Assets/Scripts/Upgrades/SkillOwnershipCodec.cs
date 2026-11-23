using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JunkToNeon.Contracts;

namespace JunkToNeon.Upgrades
{
    // Canonical bounded integration payload, not an independent save file.
    public static class SkillOwnershipCodec
    {
        public const int MaximumJsonLength = 24576;
        public static SkillOwnership DecodeOrMigrate(string json, UpgradeOwnership legacy)
        {
            if (legacy == null) throw new ArgumentNullException(nameof(legacy));
            if (json == null) return SkillOwnership.Migrate(legacy);
            var decoded = DecodeOrDefault(json);
            if (legacy.Level == 0) return decoded;
            foreach (var receipt in decoded.Receipts)
                if (receipt.Legacy && receipt.Request.Sequence == legacy.PurchaseSequence &&
                    receipt.Request.ExpectedWallet == legacy.PurchaseWalletBefore && receipt.LegacyHouseStage == legacy.PurchaseHouseStage)
                    return decoded;
            throw new FormatException("Present skill state loses or conflicts with the existing slot purchase receipt");
        }
        public static string Encode(SkillOwnership ownership)
        {
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            var output = new StringBuilder("{\"version\":" + ownership.Version + ",\"receipts\":[");
            bool first = true;
            foreach (var receipt in ownership.Receipts)
            {
                if (!first) output.Append(','); first = false;
                var r = receipt.Request; var c = r.Context;
                output.Append('{');
                void S(string key, string value) { output.Append('"').Append(key).Append("\":\"").Append(value).Append("\","); }
                void N(string key, long value) { output.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture)).Append(','); }
                S("node", r.NodeId); S("operation", r.OperationId); N("sequence", r.Sequence); N("wallet", r.ExpectedWallet);
                S("ownership", r.ExpectedOwnership); N("era", c?.Era ?? 0); N("house", c?.HouseStage ?? receipt.LegacyHouseStage); N("workshop", c?.WorkshopLevel ?? 0);
                N("recycler", c?.RecyclerLevel ?? 0); N("pigeon", c?.PigeonLoftLevel ?? 0); N("cart", c?.CartStage ?? 0); N("power", c?.PowerSourceStage ?? 0);
                S("selected", c?.SelectedConstruction ?? "-"); N("legacy", receipt.Legacy ? 1 : 0); N("price", receipt.FrozenPrice);
                if (ownership.Version == 3) S("priceRevision", r.PricingRevision);
                output.Length--; output.Append('}');
            }
            return output.Append("]}").ToString();
        }
        public static SkillOwnership DecodeOrDefault(string json)
        {
            if (json == null) return SkillOwnership.Empty;
            if (json.Length > MaximumJsonLength) throw new FormatException("Skill payload too large");
            try { return new Reader(json).Read(); }
            catch (Exception e) when (e is ArgumentException || e is OverflowException)
            { throw new FormatException("Invalid bounded skill state", e); }
        }
        sealed class Reader
        {
            readonly string source; int index;
            readonly List<bool> pricingFields = new List<bool>();
            internal Reader(string source) { this.source = source; }
            void Space() { while (index < source.Length && (source[index] == ' ' || source[index] == '\t' || source[index] == '\r' || source[index] == '\n')) index++; }
            bool Take(char c) { Space(); if (index < source.Length && source[index] == c) { index++; return true; } return false; }
            void Need(char c) { if (!Take(c)) throw new FormatException("Expected " + c); }
            string Text()
            {
                Need('"'); int start = index;
                while (index < source.Length && source[index] != '"')
                {
                    char c = source[index++];
                    if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '.' && c != '-' && c != '/' && c != ':' && c != '|')
                        throw new FormatException("Invalid skill string");
                }
                if (index - start > 2048) throw new FormatException("Skill string too long");
                string result = source.Substring(start, index - start); Need('"'); return result;
            }
            long Number()
            {
                Space(); int start = index;
                if (index < source.Length && source[index] == '-') index++;
                int digits = index;
                while (index < source.Length && source[index] >= '0' && source[index] <= '9') index++;
                if (index == digits || index - digits > 1 && source[digits] == '0' ||
                    !long.TryParse(source.Substring(start, index - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                    throw new FormatException("Invalid skill integer");
                return value;
            }
            internal SkillOwnership Read()
            {
                Need('{'); int version = 0; bool receipts = false; var entries = new List<SkillReceipt>();
                do
                {
                    string key = Text(); Need(':');
                    if (key == "version" && version == 0) { version = checked((int)Number()); if (version != 2 && version != 3) throw new FormatException("Unknown skill version"); }
                    else if (key == "receipts" && !receipts)
                    {
                        receipts = true; Need('[');
                        if (!Take(']'))
                        {
                            do { if (entries.Count == 6) throw new FormatException("Too many skill receipts"); entries.Add(Receipt()); } while (Take(','));
                            Need(']');
                        }
                    }
                    else throw new FormatException("Unknown or duplicate skill field");
                } while (Take(','));
                Need('}'); Space();
                if (version == 0 || !receipts || index != source.Length) throw new FormatException("Incomplete skill payload");
                foreach (bool present in pricingFields)
                    if (present != (version == 3)) throw new FormatException("Missing or unexpected receipt pricing revision");
                var restored = new SkillOwnership(entries);
                if (restored.Version != version) throw new FormatException("Skill payload version contradicts pricing history");
                return restored;
            }
            SkillReceipt Receipt()
            {
                Need('{'); var strings = new Dictionary<string, string>(StringComparer.Ordinal);
                var numbers = new Dictionary<string, long>(StringComparer.Ordinal);
                do
                {
                    string key = Text(); Need(':');
                    if (strings.ContainsKey(key) || numbers.ContainsKey(key)) throw new FormatException("Duplicate receipt field");
                    switch (key)
                    {
                        case "node": case "operation": case "ownership": case "selected": case "priceRevision": strings.Add(key, Text()); break;
                        case "sequence": case "wallet": case "era": case "house": case "workshop": case "recycler": case "pigeon":
                        case "cart": case "power": case "legacy": case "price": numbers.Add(key, Number()); break;
                        default: throw new FormatException("Unknown receipt field");
                    }
                } while (Take(','));
                Need('}');
                bool pricingPresent = strings.ContainsKey("priceRevision");
                pricingFields.Add(pricingPresent);
                if (strings.Count != (pricingPresent ? 5 : 4) || numbers.Count != 11 || numbers["legacy"] < 0 || numbers["legacy"] > 1)
                    throw new FormatException("Incomplete receipt");
                int N(string key) => checked((int)numbers[key]);
                bool legacy = numbers["legacy"] == 1;
                if (legacy && (N("era") != 0 || N("workshop") != 0 || N("recycler") != 0 || N("pigeon") != 0 ||
                    N("cart") != 0 || N("power") != 0 || strings["selected"] != "-")) throw new FormatException("Fabricated legacy context");
                int era = N("era");
                if (!legacy && era != 1 && era != 2) throw new FormatException("Unknown receipt era");
                var context = legacy ? null : new SkillContext(new FamilyStages(era == 1 ? EraIds.Junkyard : EraIds.WorkshopYard,
                    new[] { new FamilyStage(FamilyIds.House, N("house")), new FamilyStage(FamilyIds.Workshop, N("workshop")),
                        new FamilyStage(FamilyIds.Recycler, N("recycler")), new FamilyStage(FamilyIds.PigeonLoft, N("pigeon")),
                        new FamilyStage(FamilyIds.Cart, N("cart")), new FamilyStage(FamilyIds.PowerSource, N("power")) }),
                    strings["selected"] == "-" ? null : strings["selected"]);
                var request = new SkillPurchaseRequest(strings["node"], strings["operation"], numbers["sequence"], numbers["wallet"], strings["ownership"], context,
                    pricingPresent ? strings["priceRevision"] : SkillPricing.LegacyRevision);
                return new SkillReceipt(request, legacy, numbers["price"], legacy ? N("house") : 0);
            }
        }
    }
}
