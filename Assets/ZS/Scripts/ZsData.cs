using System.Collections.Generic;
using UnityEngine;

namespace ZS
{
    // Game data dumped by unity-decode-kit (udk/sojson.py) into Assets/ZS/Resources/ZSData/, one JSON
    // TextAsset per original ScriptableObject asset: {"script", "name", "fields"}.
    public static class ZsData
    {
        static Dictionary<string, TextAsset> byPath;

        static void Index()
        {
            if (byPath != null) return;
            byPath = new Dictionary<string, TextAsset>();
            foreach (var t in Resources.LoadAll<TextAsset>("ZSData"))
                byPath[t.name.ToLowerInvariant()] = t;
        }

        // The JSON of asset `name` (file name without extension) whose original folder mentions `scope`
        // (e.g. "e_11citynew"); TextAsset names lose the folder, so the scope is matched on content.
        public static string Text(string scope, string name)
        {
            Index();
            var key = (name + ".asset").ToLowerInvariant();
            foreach (var t in Resources.LoadAll<TextAsset>("ZSData/" + scope))
                if (t.name.ToLowerInvariant() == key) return t.text;
            if (byPath.TryGetValue(key, out var any)) return any.text;
            throw new KeyNotFoundException($"ZSData has no {name} under {scope}");
        }

        public static Dictionary<string, object> Fields(string scope, string name) =>
            Json.D(Json.D(Json.Parse(Text(scope, name)))["fields"]);

        // A serialized AnimationCurve ({m_Curve: [{time, value, inSlope, outSlope, ...}], m_PreInfinity...}).
        public static AnimationCurve Curve(object o)
        {
            var d = Json.D(o);
            var keys = new List<Keyframe>();
            foreach (var k in Json.L(d["m_Curve"]))
            {
                var kd = Json.D(k);
                keys.Add(new Keyframe(Json.F(kd["time"]), Json.F(kd["value"]), Json.F(kd["inSlope"]), Json.F(kd["outSlope"])));
            }
            var c = new AnimationCurve(keys.ToArray());
            c.preWrapMode = Wrap(Json.I(d["m_PreInfinity"]));
            c.postWrapMode = Wrap(Json.I(d["m_PostInfinity"]));
            return c;
        }

        static WrapMode Wrap(int unity) => unity switch { 1 => WrapMode.Loop, 2 => WrapMode.ClampForever, 4 => WrapMode.PingPong, _ => WrapMode.ClampForever };
    }
}
