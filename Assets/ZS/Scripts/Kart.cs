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
        public bool IsPlayer;
        public int Visual = -1;        // index into Race.KartVisuals
        public float Gravity = 63.75f;

        public float Speed;            // m/s along velocity
        public Vector3 Velocity;
        public float NitroGauge;       // 0..P.NitroFull, fills from speed lost while drifting
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

        float driftHeld, miniWindow, lastKmh, sinceFrontHit = 99f;
        public string LastWall;        // collider of the last wall hit (smoke test diagnostics)
        float offTrack;
        int lastOnTrack;

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
            // Below walking pace the wheels still turn the kart while the pedal is down, so a kart
            // stopped against a wall can steer away [reconstructed]; reversing turns the other way.
            float turn = Mathf.Max(Mathf.Clamp01(Mathf.Abs(Speed) / 3f), In.Throttle != 0 ? 0.35f : 0f);
            transform.Rotate(transform.up, yaw * turn * Mathf.Sign(Speed < -0.1f ? -1f : 1f) * dt, Space.World);

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
                // Charged by the speed the drift costs, with a floor while the drift holds; capped per 60 Hz frame.
                driftHeld += dt;
                float gain = Mathf.Max(0f, lastKmh - kmh) * P.NitroPerKmhLost;
                gain = Mathf.Max(gain, P.NitroFloorPerSecond * P.CompensatePower.Evaluate(DriftAngle) * dt);
                NitroGauge += Mathf.Min(gain, P.NitroMaxPerFrame * dt * 60f);
                if (NitroGauge >= P.NitroFull) { NitroGauge = 0; NitroSlots = Mathf.Min(2, NitroSlots + 1); }
            }
            lastKmh = kmh;

            // Nitro: a full slot gives a long boost; the small post-drift boost is shorter.
            miniWindow -= dt;
            if (In.Nitro && BoostTime <= 0)
            {
                if (NitroSlots > 0)
                {
                    NitroSlots--;
                    BoostTime = P.NitroDuration;
                    if (IsPlayer) ZsAudio.Play(ZsAudio.Nitro);
                }
                else if (miniWindow > 0)
                {
                    BoostTime = P.MiniBoostDuration;
                    miniWindow = 0;
                    if (IsPlayer) ZsAudio.Play(ZsAudio.MiniBoost);
                }
            }
            BoostTime -= dt;

            // Speed: engine power falls off toward top speed; drift and braking cost speed.
            float top = P.TopKmh + CatchUpKmh + (BoostTime > 0 ? P.BoostExtraKmh : 0f);
            // Torque by speed ratio; a drift past 50 degrees cuts it; a head-on wall hit dumps it for a moment.
            sinceFrontHit += dt;
            float power = P.BasePowerChain.Evaluate(Mathf.Clamp01(kmh / top))
                          * (Drifting ? P.DriftPowerChain.Evaluate(DriftAngle) : 1f)
                          * P.WallPowerDump.Evaluate(sinceFrontHit);
            float accel = In.Throttle >= 0 ? In.Throttle * P.Accel * power : In.Throttle * P.Brake;
            if (BoostTime > 0 && In.Throttle > 0) accel += P.BoostAccel;
            Speed += accel * dt;
            Speed -= (Drifting ? P.DriftDrag : P.Drag) * Speed * dt;
            Speed = Mathf.Clamp(Speed, -P.ReverseKmh / 3.6f, top / 3.6f);

            Velocity = vdir * Speed;
            if (!Grounded) Velocity += Vector3.down * Gravity * dt;
            var step = Velocity * dt;
            // Probe from 1.1 m with a 0.6 m radius: anything lower than ~0.5 m (kerbs, road seams) is climbed by
            // the ground ray instead of stopping the kart, as the original's wheel physics does [reconstructed].
            if (Physics.SphereCast(transform.position + transform.up * 1.1f, 0.6f, step.normalized, out var wall, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(wall.normal, transform.up) < 0.5f)
            {
                // Wall: keep a speed-dependent share along it (less for a head-on hit) and bounce off.
                // The original classes the hit by angle (tail 85, side 105, head 150 degrees); head-on here
                // means the wall faces the kart within 30 degrees [reconstructed].
                bool front = Vector3.Angle(-wall.normal, step.normalized) < 30f;
                LastWall = wall.collider.name;
                // A grazing contact (sliding along the wall) only slides; speed loss and the nose turn
                // belong to an impact [reconstructed: the original's Havok contact does this].
                bool impact = Vector3.Dot(step.normalized, -wall.normal) > 0.25f;
                if (impact) Speed *= (front ? P.FrontWallTangKeep : P.WallTangKeep).Evaluate(kmh);
                step = Vector3.ProjectOnPlane(step, wall.normal) + wall.normal * P.WallBounceKmh.Evaluate(kmh) / 3.6f * dt;
                if (front) sinceFrontHit = 0;
                // Turn the nose along the wall, as the original's bounce does (m_collideWallBouncedTime).
                var along = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(transform.forward, wall.normal), transform.up);
                if (impact && along.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(along, transform.up), 0.15f);
                Drifting = false;
            }
            transform.position += step;

            if (Track != null)
            {
                LastProgress = Progress;
                Progress = Track.Progress(transform.position, ref TrackHint);
                if (LastProgress > Track.Length * 0.8f && Progress < Track.Length * 0.2f) Lap++;
                else if (LastProgress < Track.Length * 0.2f && Progress > Track.Length * 0.8f) Lap--;
                // Off the road (beyond its width plus a verge) or airborne too long: the game puts the
                // kart back on the line (CheckPointData.ResetType, ResetPointDataList) [reconstructed timing].
                bool off = !Grounded || Track.Outside(transform.position, TrackHint) > 6f;
                offTrack = off ? offTrack + dt : 0f;
                if (!off) lastOnTrack = TrackHint;
                if (offTrack > 1.5f || transform.position.y < Track.Pos[TrackHint].y - 30f) Respawn();
            }
        }

        public void Respawn()
        {
            var r = Track.OnLine(lastOnTrack);
            offTrack = 0f;
            TrackHint = lastOnTrack;
            Progress = LastProgress = Track.Dist[lastOnTrack];
            transform.SetPositionAndRotation(r.position + Vector3.up * 0.5f, r.rotation);
            Speed = 0;
            Velocity = Vector3.zero;
            Drifting = false;
        }

        public float RaceDistance => Lap * Track.Length + Progress;
    }
}
