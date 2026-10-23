using UnityEngine;

namespace ZS
{
    // Kart tuning. Values read from the game's carparams are marked [data <asset>.<field>]; values
    // the client data does not hold (the server sends per-kart stats) are marked [reconstructed]
    // with where the number comes from. Assets/ZS/RULES.md lists both kinds.
    public class KartParams
    {
        public AnimationCurve DrivingMaxAngSpeed;  // [data CarSteer.m_drivingMaxAngSpeedCurve] deg/s by speed/top (0.24..0.95)
        public AnimationCurve PowerChain;          // [data CarEngine.m_powerChainFactorCurve] by percent of top speed (50..90)
        public AnimationCurve FallBehindTopVel;    // [data CarEngine.FallBehindCompeTopVelCurve] km/h by gap (laps)
        public AnimationCurve SlidingVelDirLerp;   // [data CarDrift.m_slidingVelocityDirLerpCurve] x read as drift angle [reconstructed]
        public float MiniBoostMinVDAngle = 35f;    // [data CarTurbo.m_miniBoostMinVDAngle]
        public float MiniBoostWindow = 1.4f;       // [data CarTurbo.m_miniBoostDelay]
        public float ReverseKmh = 106.39f;         // [data CarEngine.m_reverseTopKMPH]
        public float DriftMinKmh = 45f;            // [data CarDrift.m_BrakeTimeEndGeneralDriftingMinMPH]

        public float TopKmh = 196f;                // [reconstructed] server stat; CarDrift.m_driftHighKMPH_Curve spans 180-210 km/h
        public float Accel = 30f;                  // [reconstructed] m/s^2 at rest
        public float Brake = 40f;                  // [reconstructed]
        public float Drag = 0.08f;                 // [reconstructed]
        public float DriftDrag = 0.25f;            // [reconstructed]
        public float DriftYawScale = 1.6f;         // [reconstructed]
        public float GripVelDirLerp = 12f;         // [reconstructed]
        public float NitroChargePerSecond = 0.45f; // [reconstructed]
        public float NitroDuration = 2.5f;         // [reconstructed]
        public float MiniBoostDuration = 0.6f;     // [reconstructed]
        public float BoostExtraKmh = 45f;          // [reconstructed]
        public float BoostAccel = 25f;             // [reconstructed]
        public float WallSpeedLoss = 0.6f;         // [reconstructed]

        public static KartParams Load(string set = "00")
        {
            var p = new KartParams();
            var steer = ZsData.Fields("carparams", "CarSteer_" + set);
            var engine = ZsData.Fields("carparams", "CarEngine_" + set);
            var drift = ZsData.Fields("carparams", "CarDrift_" + set);
            var turbo = ZsData.Fields("carparams", "CarTurbo_" + set);
            p.DrivingMaxAngSpeed = ZsData.Curve(steer["m_drivingMaxAngSpeedCurve"]);
            p.PowerChain = ZsData.Curve(engine["m_powerChainFactorCurve"]);
            p.SlidingVelDirLerp = ZsData.Curve(drift["m_slidingVelocityDirLerpCurve"]);
            p.FallBehindTopVel = ZsData.Curve(engine["FallBehindCompeTopVelCurve"]);
            p.MiniBoostMinVDAngle = Json.F(turbo["m_miniBoostMinVDAngle"]);
            p.MiniBoostWindow = Json.F(turbo["m_miniBoostDelay"]);
            p.ReverseKmh = Json.F(engine["m_reverseTopKMPH"]);
            p.DriftMinKmh = Json.F(drift["m_BrakeTimeEndGeneralDriftingMinMPH"]);
            return p;
        }
    }
}
