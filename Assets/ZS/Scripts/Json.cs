using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZS
{
    // Minimal JSON reader for the kit's ScriptableObject dumps (udk/sojson.py): objects become
    // Dictionary<string, object>, arrays List<object>, every scalar stays a string as Unity wrote it.
    public static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            return Value(s, ref i);
        }

        public static float F(object o) => o is string s && s.Length > 0 ? float.Parse(s, CultureInfo.InvariantCulture) : 0f;
        public static int I(object o) => o is string s && s.Length > 0 ? int.Parse(s, CultureInfo.InvariantCulture) : 0;
        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
        public static List<object> L(object o) => o as List<object> ?? new List<object>();

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    i++; // ':'
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i++] == '}') return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i++] == ']') return l;
                }
            }
            if (c == '"') return Str(s, ref i);
            int start = i;
            while (i < s.Length && ",}] \n\r\t".IndexOf(s[i]) < 0) i++;
            string word = s.Substring(start, i - start);
            return word == "null" ? null : word;
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'u') { sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
