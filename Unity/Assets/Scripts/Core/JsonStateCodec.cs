using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JunkToNeon.Core
{
    // Strict small JSON codec for this save schema; no Unity or external JSON dependency.
    // Missing/duplicate fields, noninteger numbers and trailing data fail closed.
    public sealed partial class JsonStateCodec : IStateCodec
    {
        static Dictionary<string, object> Object(object value, params string[] keys)
        {
            var result = value as Dictionary<string, object>;
            if (result == null || result.Count != keys.Length) throw new FormatException("Invalid save object shape");
            foreach (var key in keys) if (!result.ContainsKey(key)) throw new FormatException("Missing save field: " + key);
            return result;
        }
        static long Number(object value) { if (!(value is long)) throw new FormatException("Expected integer"); return (long)value; }
        static int Int(object value) { return checked((int)Number(value)); }
        static uint UInt(object value) { return checked((uint)Number(value)); }
        static string Text(object value) { var text = value as string; if (text == null) throw new FormatException("Expected text"); return text; }
        static long OptionalLong(object value, string key)
        {
            var fields = value as Dictionary<string, object>;
            if (fields == null || !fields.TryGetValue(key, out var raw)) return 0;
            fields.Remove(key); return Number(raw);
        }
        static int OptionalInt(object value,string key) { return checked((int)OptionalLong(value,key)); }
        static int[] Reels(object value)
        {
            if (value == null) return null;
            var items = value as List<object>;
            if (items == null || items.Count != 3) throw new FormatException("Expected three symbols");
            return new[] { Int(items[0]), Int(items[1]), Int(items[2]) };
        }
        public ModelState Decode(string json)
        {
            var parsed = new Reader(json).ReadDocument();
            string expansion=OptionalExpansion(parsed);
            object journey=OptionalObject(parsed,"journey");
            object chapter=OptionalObject(parsed,"chapter");
            object wave = OptionalObject(parsed,"wave");
            int workshopBuilt = OptionalInt(parsed, "workshopBuilt");
            long settledAt=OptionalLong(parsed,"lastSettledAt");
            long seen=OptionalLong(parsed,"lastSettlementSeen");
            object receipt=null;var map=parsed as Dictionary<string,object>;
            if(map!=null&&map.TryGetValue("lastSettlement",out receipt))map.Remove("lastSettlement");
            var fields = parsed as Dictionary<string, object>;
            // Existing version-1 saves predate the optional hint acknowledgement.
            object hint = null;
            bool hasHint = fields != null && fields.TryGetValue("autoHintSeen", out hint);
            if (hasHint) fields.Remove("autoHintSeen");
            var s = Object(parsed, "schemaVersion", "contentVersion", "revision", "sequence", "coins", "houseStage", "selectedStage", "seed", "lastSpin", "pending");
            var state = new ModelState { schemaVersion = Int(s["schemaVersion"]), contentVersion = Int(s["contentVersion"]), revision = Number(s["revision"]), sequence = Number(s["sequence"]), coins = Number(s["coins"]), houseStage = Int(s["houseStage"]), selectedStage = Int(s["selectedStage"]), seed = UInt(s["seed"]) };
            state.autoHintSeen = hasHint ? Int(hint) : 0;
            state.workshopBuilt = workshopBuilt;
            state.wave = DecodeWave(wave);
            state.chapter=chapter==null?null:DecodeChapterState(chapter);
            state.journey=journey==null?null:DecodeJourneyState(journey);
            state.expansion=expansion==null?null:ExpansionCodec.DecodeState(expansion);
            state.lastSettledAt=settledAt;
            state.lastSettlementSeen=seen;
            if(receipt!=null)
            {
                var r=Object(receipt,"sequence","from","to","seconds","coins","workshop");
                state.lastSettlement=new OfflineReceipt {sequence=Number(r["sequence"]),from=Number(r["from"]),to=Number(r["to"]),seconds=Number(r["seconds"]),coins=Number(r["coins"]),workshop=Int(r["workshop"])};
            }
            if (s["lastSpin"] != null)
            {
                string spinExpansion=OptionalExpansion(s["lastSpin"]);
                object spinJourney=OptionalObject(s["lastSpin"],"journey");
                object spinChapter=OptionalObject(s["lastSpin"],"chapter");
                object spinWave = OptionalObject(s["lastSpin"],"wave");
                int bonus = OptionalInt(s["lastSpin"], "bonus");
                var spin = Object(s["lastSpin"], "sequence", "symbols", "reward");
                state.lastSpin = new SpinReceipt { sequence = Number(spin["sequence"]), symbols = Reels(spin["symbols"]), reward = Int(spin["reward"]), bonus = bonus, wave=DecodeOperation(spinWave,Reels(spin["symbols"])),chapter=spinChapter==null?null:DecodeChapterOperation(spinChapter),journey=spinJourney==null?null:DecodeJourneyOperation(spinJourney) };
                state.lastSpin.expansion=spinExpansion==null?null:ExpansionCodec.DecodeOperation(spinExpansion);
            }
            if (s["pending"] != null)
            {
                string pendingExpansion=OptionalExpansion(s["pending"]);
                object pendingJourney=OptionalObject(s["pending"],"journey");
                object pendingChapter=OptionalObject(s["pending"],"chapter");
                object pendingWave=OptionalObject(s["pending"],"wave");
                long at=OptionalLong(s["pending"],"at");
                var pending = Object(s["pending"], "sequence", "kind", "symbols", "seedAfter", "target");
                state.pending = new PendingOperation { sequence = Number(pending["sequence"]), kind = Text(pending["kind"]), symbols = Reels(pending["symbols"]), seedAfter = UInt(pending["seedAfter"]), target = Int(pending["target"]) };
                state.pending.at=at;
                state.pending.wave=DecodeOperation(pendingWave,state.pending.symbols);
                state.pending.chapter=pendingChapter==null?null:DecodeChapterOperation(pendingChapter);
                state.pending.journey=pendingJourney==null?null:DecodeJourneyOperation(pendingJourney);
                state.pending.expansion=pendingExpansion==null?null:ExpansionCodec.DecodeOperation(pendingExpansion);
            }
            GameLogic.Validate(state); return state;
        }
        static string N(long n) { return n.ToString(CultureInfo.InvariantCulture); }
        static string Symbols(int[] values) { return values == null ? "null" : "[" + string.Join(",", values) + "]"; }
        static string OptionalExpansion(object raw)
        {
            var fields=raw as Dictionary<string,object>;
            if(fields==null||!fields.TryGetValue("expansion",out object value))return null;
            fields.Remove("expansion");
            if(!(value is string token))throw new FormatException("Present expansion must be a non-null canonical string");
            return token;
        }
        public string Encode(ModelState s)
        {
            GameLogic.Validate(s);
            var spin = s.lastSpin == null ? "null" : "{\"sequence\":" + N(s.lastSpin.sequence) + ",\"symbols\":" + Symbols(s.lastSpin.symbols) + ",\"reward\":" + N(s.lastSpin.reward) + ",\"bonus\":" + N(s.lastSpin.bonus) + (s.lastSpin.wave==null?"":",\"wave\":"+EncodeOperation(s.lastSpin.wave)) + (s.lastSpin.chapter==null?"":",\"chapter\":"+EncodeChapterOperation(s.lastSpin.chapter)) + (s.lastSpin.journey==null?"":",\"journey\":"+EncodeJourneyOperation(s.lastSpin.journey)) + (s.lastSpin.expansion==null?"":",\"expansion\":"+Q(ExpansionCodec.EncodeOperation(s.lastSpin.expansion))) + "}";
            var p = s.pending;
            var pending = p == null ? "null" : "{\"sequence\":" + N(p.sequence) + ",\"kind\":\"" + p.kind + "\",\"symbols\":" + Symbols(p.symbols) + ",\"seedAfter\":" + N(p.seedAfter) + ",\"target\":" + N(p.target) + ",\"at\":" + N(p.at) + (p.wave==null?"":",\"wave\":"+EncodeOperation(p.wave)) + (p.chapter==null?"":",\"chapter\":"+EncodeChapterOperation(p.chapter)) + (p.journey==null?"":",\"journey\":"+EncodeJourneyOperation(p.journey)) + (p.expansion==null?"":",\"expansion\":"+Q(ExpansionCodec.EncodeOperation(p.expansion))) + "}";
            var r=s.lastSettlement;
            var settlement=r==null?"null":"{\"sequence\":"+N(r.sequence)+",\"from\":"+N(r.from)+",\"to\":"+N(r.to)+",\"seconds\":"+N(r.seconds)+",\"coins\":"+N(r.coins)+",\"workshop\":"+N(r.workshop)+"}";
            return "{\"schemaVersion\":" + N(s.schemaVersion) + ",\"contentVersion\":" + N(s.contentVersion) + ",\"revision\":" + N(s.revision) + ",\"sequence\":" + N(s.sequence) + ",\"coins\":" + N(s.coins) + ",\"houseStage\":" + N(s.houseStage) + ",\"selectedStage\":" + N(s.selectedStage) + ",\"seed\":" + N(s.seed) + ",\"autoHintSeen\":" + N(s.autoHintSeen) + ",\"workshopBuilt\":" + N(s.workshopBuilt) + ",\"lastSettledAt\":"+N(s.lastSettledAt)+",\"lastSettlementSeen\":"+N(s.lastSettlementSeen)+",\"lastSettlement\":"+settlement+",\"lastSpin\":" + spin + ",\"pending\":" + pending + (s.wave==null?"":",\"wave\":"+EncodeWave(s.wave)) + (s.chapter==null?"":",\"chapter\":"+EncodeChapterState(s.chapter)) + (s.journey==null?"":",\"journey\":"+EncodeJourneyState(s.journey)) + (s.expansion==null?"":",\"expansion\":"+Q(ExpansionCodec.EncodeState(s.expansion))) + "}";
        }

        sealed class Reader
        {
            readonly string json; int offset; int depth;
            public Reader(string json) { if (json == null || json.Length > 65536) throw new FormatException("Invalid save size"); this.json = json; }
            void Whitespace() { while (offset < json.Length && (json[offset] == ' ' || json[offset] == '\r' || json[offset] == '\n' || json[offset] == '\t')) offset++; }
            bool Take(char c) { Whitespace(); if (offset < json.Length && json[offset] == c) { offset++; return true; } return false; }
            void Need(char c) { if (!Take(c)) throw new FormatException("Invalid JSON punctuation"); }
            public object ReadDocument() { var value = Value(); Whitespace(); if (offset != json.Length) throw new FormatException("Trailing JSON data"); return value; }
            object Value(string fieldName=null)
            {
                Whitespace(); if (offset >= json.Length || ++depth > 8) throw new FormatException("Invalid JSON depth");
                object result; var c = json[offset];
                if (c == '{')
                {
                    offset++; var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (!Take('}')) { do { var key = String(); Need(':'); if (map.ContainsKey(key)) throw new FormatException("Duplicate JSON field"); map.Add(key, Value(key)); } while (Take(',')); Need('}'); }
                    result = map;
                }
                else if (c == '[')
                {
                    offset++; var list = new List<object>();
                    if (!Take(']')) { do { if (list.Count >= (fieldName=="sources"?JunkToNeon.Contracts.ChapterSymbolModifiers.MaximumSources:10)) throw new FormatException("Invalid JSON array size"); list.Add(Value()); } while (Take(',')); Need(']'); }
                    result = list;
                }
                else if (c == '"') result = String();
                else if (c == 'n' && offset + 4 <= json.Length && json.Substring(offset, 4) == "null") { offset += 4; result = null; }
                else
                {
                    var start = offset; if (json[offset] == '-') offset++;
                    var digits = offset; while (offset < json.Length && json[offset] >= '0' && json[offset] <= '9') offset++;
                    long n;
                    if (digits == offset || (offset - digits > 1 && json[digits] == '0') || !long.TryParse(json.Substring(start, offset - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n)) throw new FormatException("Invalid JSON integer");
                    result = n;
                }
                depth--; return result;
            }
            string String()
            {
                Need('"'); var result = new StringBuilder();
                while (offset < json.Length)
                {
                    var c = json[offset++];
                    if (c == '"') return result.ToString();
                    if (c < 32) throw new FormatException("Invalid JSON string");
                    if (c == '\\')
                    {
                        if (offset >= json.Length) throw new FormatException("Invalid JSON escape");
                        c = json[offset++];
                        switch (c)
                        {
                            case '"': case '\\': case '/': break;
                            case 'b': c = '\b'; break; case 'f': c = '\f'; break; case 'n': c = '\n'; break; case 'r': c = '\r'; break; case 't': c = '\t'; break;
                            case 'u':
                                ushort code;
                                if (offset + 4 > json.Length || !ushort.TryParse(json.Substring(offset, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) throw new FormatException("Invalid Unicode escape");
                                c = (char)code; offset += 4; break;
                            default: throw new FormatException("Invalid JSON escape");
                        }
                    }
                    result.Append(c);
                }
                throw new FormatException("Unterminated JSON string");
            }
        }
    }
}
