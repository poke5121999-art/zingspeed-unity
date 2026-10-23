using UnityEngine;

namespace ZS
{
    // AI driver [reconstructed]: the original bots run server-side, so this one follows the game's
    // race line (TrackPointDataList) with its own lane offset, drifts into sharp bends, and spends
    // nitro on straights. A kart behind the leader gets the catch-up the engine data describes
    // (CarEngine.FallBehindCompeTopVelCurve: extra km/h by gap).
    [RequireComponent(typeof(Kart))]
    public class Bot : MonoBehaviour
    {
        public float Lane;          // -1 (left edge) .. 1 (right edge), scaled down to stay on the road
        public float Skill = 1f;    // throttle scale
        public Kart Leader;
        Kart kart;
        float stuck, reverse;

        void Awake() => kart = GetComponent<Kart>();

        void Update() => Think();

        public void Think()
        {
            if (kart == null) kart = GetComponent<Kart>();
            if (kart.Track == null) return;
            float look = 12f + kart.Speed * 0.6f;
            Vector3 target = kart.Track.PointAt(kart.Progress + look, Lane * 0.5f, out var ahead);
            Vector3 local = transform.InverseTransformPoint(target);
            float bend = Vector3.SignedAngle(transform.forward, ahead, transform.up);
            // Brake when the line turns hard within the next stretch and the kart is fast.
            kart.Track.PointAt(kart.Progress + look * 2.5f, 0f, out var far);
            float corner = Vector3.Angle(transform.forward, far);
            float kmh = kart.Speed * 3.6f;
            float throttle = corner > 55f && kmh > 120f ? -1f : corner > 35f && kmh > 170f ? 0f : Skill;
            float steer = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 1f)) * 2.2f, -1f, 1f);
            // Stuck against something: back off for a second with the wheels turned the other way.
            float dt = Time.inFixedTimeStep || !Application.isPlaying ? 0.02f : Time.deltaTime;
            stuck = Mathf.Abs(kmh) < 4f && reverse <= 0f && throttle > 0f ? stuck + dt : 0f;
            if (stuck > 1f) { reverse = 1.2f; stuck = 0f; }
            if (reverse > 0f) { reverse -= dt; throttle = -1f; steer = -steer; }
            var input = new KartInput
            {
                Steer = steer,
                Throttle = throttle,
                Drift = Mathf.Abs(bend) > 28f && kart.Speed * 3.6f > kart.P.DriftMinKmh + 10f,
                Nitro = Mathf.Abs(bend) < 8f && corner < 25f && throttle > 0f && (kart.NitroSlots > 0 || kart.BoostTime <= 0),
            };
            float gap = Leader != null && Leader != kart ? (Leader.RaceDistance - kart.RaceDistance) / kart.Track.Length : 0f;
            kart.CatchUpKmh = kart.P.FallBehindTopVel.Evaluate(Mathf.Max(0f, gap));
            kart.In = input;
        }
    }
}
