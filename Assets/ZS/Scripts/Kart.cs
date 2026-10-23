using UnityEngine;

namespace ZS
{
    public struct KartInput
    {
        public float Throttle, Steer;  // -1..1
        public bool Drift, Nitro;
    }

    // Kart motion on the original track meshes. Parameters come from the game's carparams assets
    // (NssEngineCommonParams, NssSteerCommonParams, NssDriftCommonParams, NssTurboCommonParams);
    // how each is applied follows the decompiled NssVehicle* code where recorded in Assets/ZS/RULES.md,
    // and is marked as reconstructed where the code could not settle it.
    public class Kart : MonoBehaviour
    {
        public KartParams P;
        public KartInput In;
        public Track Track;
        public float Gravity = 63.75f;

        public float Speed;            // m/s along velocity
        public Vector3 Velocity;
        public float NitroGauge;       // 0..1, fills while drifting
        public int NitroSlots;         // 0..2
        public float BoostTime;        // seconds of boost left
        public float CatchUpKmh;       // extra top speed for a kart behind the leader (bots only)
        public bool Drifting;
        public float DriftAngle;       // degrees between heading and velocity
        public bool Grounded;

        public int TrackHint;
        public float Progress, LastProgress;
        public int Lap = -1;           // first crossing of the start line starts lap 0
        public float FinishTime = -1;

        float driftHeld, miniWindow;

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float dt)
        {
            float kmh = Speed * 3.6f;

            // Ground: stick to the track surface under the kart.
            Grounded = Physics.Raycast(transform.position + transform.up * 1.5f, -transform.up, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore);
            if (Grounded)
            {
                var fwd = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd, hit.normal), 12f * dt);
                transform.position = hit.point;
            }

            // Steering: yaw rate from the steer curves by speed; drifting turns harder.
            float maxYaw = P.DrivingMaxAngSpeed.Evaluate(Mathf.Clamp01(kmh / P.TopKmh));  // curve x is speed / top
            float yaw = In.Steer * maxYaw * (Drifting ? P.DriftYawScale : 1f);
            if (Speed > 0.5f) transform.Rotate(transform.up, yaw * dt, Space.World);

            // Drift: starts with the drift key while steering at speed; velocity lags the heading.
            bool wantDrift = In.Drift && Mathf.Abs(In.Steer) > 0.2f && kmh > P.DriftMinKmh;
            if (wantDrift && !Drifting) { Drifting = true; driftHeld = 0; }
            if (Drifting && (!In.Drift || kmh < P.DriftMinKmh * 0.6f))
            {
                Drifting = false;
                miniWindow = P.MiniBoostWindow;  // release: a nitro tap soon after gives a small boost
            }
            Vector3 heading = transform.forward;
            Vector3 vdir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : heading;
            float lerp = Drifting ? P.SlidingVelDirLerp.Evaluate(DriftAngle) : P.GripVelDirLerp;
            vdir = Vector3.Slerp(vdir, heading, Mathf.Clamp01(lerp * dt));
            DriftAngle = Vector3.Angle(vdir, heading);
            if (Drifting)
            {
                driftHeld += dt;
                NitroGauge += dt * P.NitroChargePerSecond * Mathf.Clamp01(DriftAngle / P.MiniBoostMinVDAngle) * Mathf.Clamp01(kmh / 100f);
                if (NitroGauge >= 1f) { NitroGauge = 0; NitroSlots = Mathf.Min(2, NitroSlots + 1); }
            }

            // Nitro: a full slot gives a long boost; the small post-drift boost is shorter.
            miniWindow -= dt;
            if (In.Nitro && BoostTime <= 0)
            {
                if (NitroSlots > 0) { NitroSlots--; BoostTime = P.NitroDuration; }
                else if (miniWindow > 0) { BoostTime = P.MiniBoostDuration; miniWindow = 0; }
            }
            BoostTime -= dt;

            // Speed: engine power falls off toward top speed; drift and braking cost speed.
            float top = P.TopKmh + CatchUpKmh + (BoostTime > 0 ? P.BoostExtraKmh : 0f);
            // powerChain curve x is percent of top speed (50 -> 1, 90 -> 0.05); full power below 50%.
            float accel = In.Throttle >= 0 ? In.Throttle * P.Accel * P.PowerChain.Evaluate(100f * kmh / top) : In.Throttle * P.Brake;
            if (BoostTime > 0) accel += P.BoostAccel;
            Speed += accel * dt;
            Speed -= (Drifting ? P.DriftDrag : P.Drag) * Speed * dt;
            Speed = Mathf.Clamp(Speed, -P.ReverseKmh / 3.6f, top / 3.6f);

            Velocity = vdir * Speed;
            if (!Grounded) Velocity += Vector3.down * Gravity * dt;
            var step = Velocity * dt;
            if (Physics.SphereCast(transform.position + transform.up * 0.7f, 0.6f, step.normalized, out var wall, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(wall.normal, transform.up) < 0.5f)
            {
                // Wall: slide along it and lose speed, more for a head-on hit.
                float head = Mathf.Abs(Vector3.Dot(step.normalized, wall.normal));
                Speed *= 1f - P.WallSpeedLoss * head;
                step = Vector3.ProjectOnPlane(step, wall.normal);
                Drifting = false;
            }
            transform.position += step;

            if (Track != null)
            {
                LastProgress = Progress;
                Progress = Track.Progress(transform.position, ref TrackHint);
                if (LastProgress > Track.Length * 0.8f && Progress < Track.Length * 0.2f) Lap++;
                else if (LastProgress < Track.Length * 0.2f && Progress > Track.Length * 0.8f) Lap--;
                if (transform.position.y < Track.Pos[TrackHint].y - 30f) Respawn();
            }
        }

        public void Respawn()
        {
            var r = Track.NearestReset(transform.position);
            transform.SetPositionAndRotation(r.position + Vector3.up * 0.5f, r.rotation);
            Speed = 0;
            Velocity = Vector3.zero;
            Drifting = false;
        }

        public float RaceDistance => Lap * Track.Length + Progress;
    }
}
