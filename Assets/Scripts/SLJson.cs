// A small JSON reader for data with free-form objects (Starlife Town's tables), which JsonUtility cannot read.
// Objects become Dictionary<string, object>, arrays List<object>, numbers double, plus string, bool and null.
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SLJson {
    public static object Parse(string s) { int i = 0; return Value(s, ref i); }
    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    static object Value(string s, ref int i) {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{') {
            var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true) { Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++; d[k] = Value(s, ref i); Ws(s, ref i); if (s[i++] == '}') return d; }
        }
        if (c == '[') {
            var l = new List<object>(); i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true) { l.Add(Value(s, ref i)); Ws(s, ref i); if (s[i++] == ']') return l; }
        }
        if (c == '"') return Str(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }
    static string Str(string s, ref int i) {
        var sb = new StringBuilder(); i++;
        while (s[i] != '"') {
            if (s[i] == '\\') {
                i++; char e = s[i];
                if (e == 'u') { sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
            } else sb.Append(s[i]);
            i++;
        }
        i++; return sb.ToString();
    }
    // ---- reading helpers
    public static Dictionary<string, object> O(object o) => o as Dictionary<string, object>;
    public static List<object> A(object o) => o as List<object> ?? new List<object>();
    public static object Get(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v : null;
    public static float F(object o, float def = 0) => o is double d ? (float)d : def;
    public static string S(object o, string def = null) => o as string ?? def;
    public static bool B(object o) => o is bool b && b;
    public static bool Bool(object o) => o is bool b && b;
}
