using System.Collections.Generic;
using UnityEngine;

namespace ZS
{
    // The race line of one track, read from the game's own pick config (CheckPointConfig asset,
    // dumped to JSON by unity-decode-kit): TrackPointDataList (one point every ~10 m with left and
    // right widths; main curve only), CheckPointDataList (start/finish and gravity), ResetPointDataList (respawns).
    public class Track
    {
        public readonly List<Vector3> Pos = new List<Vector3>();
        public readonly List<Vector3> Dir = new List<Vector3>();
        public readonly List<float> Dist = new List<float>();
        public readonly List<float> LeftWidth = new List<float>();
        public readonly List<float> RightWidth = new List<float>();
        public readonly List<Pose> Resets = new List<Pose>();
        public float Length;
        public float Gravity = 9.81f;
        public int Laps = 1;
        public Vector3 StartPos, StartDir;

        public static Track Load(string env, int laps)
        {
            var cfg = Json.D(Json.D(Json.Parse(ZsData.Text(env, "CheckPointConfig")))["fields"]);
            var t = new Track { Length = Json.F(cfg["MainCurveLength"]), Laps = laps };
            foreach (var o in Json.L(cfg["TrackPointDataList"]))
            {
                var p = Json.D(o);
                // CurveID 0 is the main loop; other curves are side branches (shortcuts) measured from
                // their own start, which would break the distance order the race line relies on.
                if (Json.I(p["CurveID"]) != 0) continue;
                t.Pos.Add(V(p["Position"]));
                t.Dir.Add(V(p["Direction"]).normalized);
                t.Dist.Add(Json.F(p["TrackDistance"]));
                t.LeftWidth.Add(Mathf.Abs(Json.F(p["LeftWidth"])));
                t.RightWidth.Add(Mathf.Abs(Json.F(p["RightWidth"])));
            }
            var cps = Json.L(cfg["CheckPointDataList"]);
            var start = Json.D(cps[Json.I(cfg["RaceStartCheckPointIndex"])]);
            t.StartPos = V(start["Position"]);
            t.StartDir = V(start["Forward"]).normalized;
            t.Gravity = Mathf.Abs(V(start["Gravity"]).y);
            foreach (var o in Json.L(cfg["ResetPointDataList"]))
            {
                var r = Json.D(o);
                if (r.TryGetValue("Position", out var p) && r.TryGetValue("Rotation", out var q))
                    t.Resets.Add(new Pose(V(p), Q(q)));
            }
            return t;
        }

        // Distance along the race line, searched near the previous answer so laps do not jump.
        public float Progress(Vector3 p, ref int hint)
        {
            int n = Pos.Count, best = hint;
            float bestD = float.MaxValue;
            for (int k = -8; k <= 8; k++)
            {
                int i = ((hint + k) % n + n) % n;
                float d = (Pos[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (bestD > 60f * 60f)  // lost (respawn, shortcut): search everything once
                for (int i = 0; i < n; i++)
                {
                    float d = (Pos[i] - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
            hint = best;
            return Dist[best] + Vector3.Dot(p - Pos[best], Dir[best]);
        }

        public Vector3 PointAt(float dist, float lateral, out Vector3 dir)
        {
            dist = Mathf.Repeat(dist, Length);
            int i = Dist.BinarySearch(dist);
            if (i < 0) i = Mathf.Max(0, ~i - 1);
            dir = Dir[i];
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float w = lateral >= 0 ? RightWidth[i] : LeftWidth[i];
            return Pos[i] + dir * (dist - Dist[i]) + right * lateral * w;
        }

        // How far p is beside the race line at point i, against the road half-width on that side.
        public float Outside(Vector3 p, int i)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Dir[i]).normalized;
            float side = Vector3.Dot(p - Pos[i], right);
            return Mathf.Abs(side) - (side >= 0 ? RightWidth[i] : LeftWidth[i]);
        }

        public Pose OnLine(int i) => new Pose(Pos[i] + Vector3.up, Quaternion.LookRotation(Dir[i]));

        public Pose NearestReset(Vector3 p)
        {
            Pose best = new Pose(StartPos, Quaternion.LookRotation(StartDir));
            float bd = float.MaxValue;
            foreach (var r in Resets)
            {
                float d = (r.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        static Vector3 V(object o)
        {
            var d = Json.D(o);
            return new Vector3(Json.F(d["x"]), Json.F(d["y"]), Json.F(d["z"]));
        }

        static Quaternion Q(object o)
        {
            var d = Json.D(o);
            var q = new Quaternion(Json.F(d["x"]), Json.F(d["y"]), Json.F(d["z"]), Json.F(d["w"]));
            return q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0 ? Quaternion.identity : q;
        }
    }
}
