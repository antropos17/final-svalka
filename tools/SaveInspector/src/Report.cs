using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace JunkToNeon.SaveInspector
{
    // Ordered report node. Values are string, long, int, bool, null, Obj or List<object>.
    // Insertion order is the output order, so JSON and text are deterministic.
    public sealed class Obj
    {
        readonly List<KeyValuePair<string, object>> items = new List<KeyValuePair<string, object>>();
        public IReadOnlyList<KeyValuePair<string, object>> Items { get { return items; } }
        public Obj Add(string key, object value) { items.Add(new KeyValuePair<string, object>(key, value)); return this; }
        public object Get(string key)
        {
            foreach (var item in items) if (item.Key == key) return item.Value;
            return null;
        }
        public Obj Child(string key) { return Get(key) as Obj; }
    }

    public static class Report
    {
        public static byte[] Json(Obj root)
        {
            using (var stream = new MemoryStream())
            {
                var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.Default };
                using (var writer = new Utf8JsonWriter(stream, options)) WriteValue(writer, root);
                stream.WriteByte((byte)'\n');
                return stream.ToArray();
            }
        }

        static void WriteValue(Utf8JsonWriter w, object value)
        {
            switch (value)
            {
                case null: w.WriteNullValue(); break;
                case string s: w.WriteStringValue(Ascii(s)); break;
                case bool b: w.WriteBooleanValue(b); break;
                case int i: w.WriteNumberValue(i); break;
                case long l: w.WriteNumberValue(l); break;
                case uint u: w.WriteNumberValue(u); break;
                case Obj o:
                    w.WriteStartObject();
                    foreach (var item in o.Items) { w.WritePropertyName(item.Key); WriteValue(w, item.Value); }
                    w.WriteEndObject(); break;
                case List<object> list:
                    w.WriteStartArray();
                    foreach (var item in list) WriteValue(w, item);
                    w.WriteEndArray(); break;
                default: throw new InvalidOperationException("Unsupported report value type");
            }
        }

        public static byte[] Text(Obj root)
        {
            var b = new StringBuilder();
            WriteText(b, root, 0);
            return Encoding.ASCII.GetBytes(b.ToString());
        }

        static void WriteText(StringBuilder b, Obj o, int indent)
        {
            foreach (var item in o.Items)
            {
                b.Append(' ', indent * 2).Append(item.Key).Append(':');
                WriteTextValue(b, item.Value, indent);
            }
        }

        static void WriteTextValue(StringBuilder b, object value, int indent)
        {
            if (value is Obj child)
            {
                if (child.Items.Count == 0) { b.Append(" {}\n"); return; }
                b.Append('\n'); WriteText(b, child, indent + 1); return;
            }
            if (value is List<object> list)
            {
                if (list.Count == 0) { b.Append(" []\n"); return; }
                bool simple = true;
                foreach (var entry in list) if (entry is Obj || entry is List<object> || entry is string text && (text.Length > 40 || text.IndexOf(',') >= 0)) simple = false;
                if (simple)
                {
                    b.Append(' ');
                    for (int i = 0; i < list.Count; i++) { if (i != 0) b.Append(", "); b.Append(Scalar(list[i])); }
                    b.Append('\n'); return;
                }
                b.Append('\n');
                for (int i = 0; i < list.Count; i++)
                {
                    b.Append(' ', (indent + 1) * 2).Append('-');
                    if (list[i] is Obj entryObj) { b.Append('\n'); WriteText(b, entryObj, indent + 2); }
                    else { b.Append(' ').Append(Scalar(list[i])).Append('\n'); }
                }
                return;
            }
            b.Append(' ').Append(Scalar(value)).Append('\n');
        }

        public static string Scalar(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return Ascii(s);
                case bool v: return v ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case uint u: return u.ToString(CultureInfo.InvariantCulture);
                default: return "<complex>";
            }
        }

        // Every string written is already a validated production identifier or a tool
        // constant; this is a final guard so no control or non-ASCII byte reaches stdout.
        public static string Ascii(string value)
        {
            var b = new StringBuilder(value.Length);
            foreach (char c in value) b.Append(c >= 32 && c < 127 ? c : '?');
            return b.ToString();
        }

        // Flatten for semantic diff: path -> scalar text, in report order.
        public static List<KeyValuePair<string, string>> Flatten(Obj root)
        {
            var result = new List<KeyValuePair<string, string>>();
            Flatten(root, "", result);
            return result;
        }

        static void Flatten(object value, string path, List<KeyValuePair<string, string>> output)
        {
            if (value is Obj o)
            {
                if (o.Items.Count == 0) { output.Add(new KeyValuePair<string, string>(path, "{}")); return; }
                foreach (var item in o.Items) Flatten(item.Value, path.Length == 0 ? item.Key : path + "." + item.Key, output);
                return;
            }
            if (value is List<object> list)
            {
                bool simple = true;
                foreach (var entry in list) if (entry is Obj || entry is List<object>) simple = false;
                if (simple)
                {
                    var parts = new string[list.Count];
                    for (int i = 0; i < list.Count; i++) parts[i] = Scalar(list[i]);
                    output.Add(new KeyValuePair<string, string>(path, "[" + string.Join(",", parts) + "]"));
                    return;
                }
                output.Add(new KeyValuePair<string, string>(path + ".count", list.Count.ToString(CultureInfo.InvariantCulture)));
                for (int i = 0; i < list.Count; i++) Flatten(list[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", output);
                return;
            }
            output.Add(new KeyValuePair<string, string>(path, Scalar(value)));
        }
    }
}
