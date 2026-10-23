using UnityEngine;

namespace ZS
{
    // Kart tuning. Values read from the game's carparams are marked [data <asset>.<field>]; values
    // the client data does not hold (the server sends per-kart stats) are marked [reconstructed]
    // with where the number comes from. Assets/ZS/RULES.md lists both kinds.
    public class KartParams
    {
        public AnimationCurve DrivingMaxAngSpeed;  // [data CarSteer.m_drivingMaxAngSpeedCurve] deg/s by speed/top (0.24..0.95)
        public AnimationCurve BasePowerChain;      // [data CarEngine.m_basePowerChainFactor] torque scale by speed/top (1.0 -> 0.2)
        public AnimationCurve DriftPowerChain;     // [data CarEngine.m_powerChainFactorCurve] while drifting, by |drift angle| (50 -> 1, 90 -> 0.05)
        public AnimationCurve CompensatePower;     // [data CarTurbo.m_compensatePowerFactorCurve] nitro floor while drifting, x read as drift angle [reconstructed]
        public AnimationCurve WallTangKeep;        // [data CarCollision.m_fenceCollisionDumpTangVelCoef] speed kept along a wall, by km/h
        public AnimationCurve FrontWallTangKeep;   // [data CarCollision.m_frontFenceCollisionDumpTangVelCoef] same for a head-on hit
        public AnimationCurve WallBounceKmh;       // [data CarCollision.m_fenceCollisionBounceKMPH] bounce off the wall, by km/h
        public AnimationCurve WallPowerDump;       // [data CarCollision.m_frontFenceCollisionPowerDumpCurve] power after a head-on hit, by seconds
        public AnimationCurve FallBehindTopVel;    // [data CarEngine.FallBehindCompeTopVelCurve] km/h by gap (laps)
        public AnimationCurve SlidingVelDirLerp;   // [data CarDrift.m_slidingVelocityDirLerpCurve] x read as drift angle [reconstructed]
        public float MiniBoostMinVDAngle = 35f;    // [data CarTurbo.m_miniBoostMinVDAngle]
        public float MiniBoostWindow = 1.4f;       // [data CarTurbo.m_miniBoostDelay]
        public float ReverseKmh = 106.39f;         // [data CarEngine.m_reverseTopKMPH]
        public float DriftMinKmh = 45f;            // [data CarDrift.m_BrakeTimeEndGeneralDriftingMinMPH] bots drift above it; no entry minimum in the original

        public float TopKmh = 196f;                // [reconstructed] server stat; CarDrift.m_driftHighKMPH_Curve spans 180-210 km/h
        public float Accel = 30f;                  // [reconstructed] m/s^2 at rest
        public float Brake = 40f;                  // [reconstructed]
        public float Drag = 0.08f;                 // [reconstructed]
        public float DriftDrag = 0.25f;            // [reconstructed]
        public float DriftMaxYaw = 175f;           // deg/s while drifting (native NssSolveVehicleSteering)
        public float SlideMaxYaw = 128f;           // deg/s x |steer| while sliding (DriftState_Sliding)
        public float SlideReenterAngle = 50f;      // [data CarDrift.m_SlidingStateEnterContinuousDriftMinVdAngle]
        public float MiniBoostMaxVDAngle = 50f;    // [data CarTurbo.m_miniBoostMaxVDAngle]
        public AnimationCurve DriftForwardLerp;    // [data CarDrift.m_driftForwardExtraForceCurve] velocity-to-nose slerp rate by drift angle
        public float GripVelDirLerp = 12f;         // [reconstructed]
        public float NitroFull = 1000f;            // gauge units for one nitro (SingleNitrogenCollect)
        public float NitroMaxPerFrame = 100f;      // gain cap per 60 Hz physics frame (SingleNitrogenCollect.CalcCollectPower)
        public float NitroPerKmhLost = 9f;         // [reconstructed] per-kart fGatherNitroSpeed is server data; ~4 drifts per nitro
        public float NitroFloorPerSecond = 120f;   // [reconstructed] per-kart fCompensatePowerFactor, times CompensatePower
        public float NitroDuration = 2.5f;         // [reconstructed]
        public float MiniBoostDuration = 0.6f;     // [reconstructed]
        public float BoostExtraKmh = 45f;          // [reconstructed]
        public float BoostAccel = 25f;             // [reconstructed]

        public static KartParams Load(string set = "00")
        {
            var p = new KartParams();
            var steer = ZsData.Fields("carparams", "CarSteer_" + set);
            var engine = ZsData.Fields("carparams", "CarEngine_" + set);
            var drift = ZsData.Fields("carparams", "CarDrift_" + set);
            var turbo = ZsData.Fields("carparams", "CarTurbo_" + set);
            p.DrivingMaxAngSpeed = ZsData.Curve(steer["m_drivingMaxAngSpeedCurve"]);
            var wall = ZsData.Fields("carparams", "CarCollision_" + set);
            p.BasePowerChain = ZsData.Curve(engine["m_basePowerChainFactor"]);
            p.DriftPowerChain = ZsData.Curve(engine["m_powerChainFactorCurve"]);
            p.CompensatePower = ZsData.Curve(turbo["m_compensatePowerFactorCurve"]);
            p.WallTangKeep = ZsData.Curve(wall["m_fenceCollisionDumpTangVelCoef"]);
            p.FrontWallTangKeep = ZsData.Curve(wall["m_frontFenceCollisionDumpTangVelCoef"]);
            p.WallBounceKmh = ZsData.Curve(wall["m_fenceCollisionBounceKMPH"]);
            p.WallPowerDump = ZsData.Curve(wall["m_frontFenceCollisionPowerDumpCurve"]);
            p.SlidingVelDirLerp = ZsData.Curve(drift["m_slidingVelocityDirLerpCurve"]);
            p.DriftForwardLerp = ZsData.Curve(drift["m_driftForwardExtraForceCurve"]);
            p.SlideReenterAngle = Json.F(drift["m_SlidingStateEnterContinuousDriftMinVdAngle"]);
            p.MiniBoostMaxVDAngle = Json.F(turbo["m_miniBoostMaxVDAngle"]);
            p.FallBehindTopVel = ZsData.Curve(engine["FallBehindCompeTopVelCurve"]);
            p.MiniBoostMinVDAngle = Json.F(turbo["m_miniBoostMinVDAngle"]);
            p.MiniBoostWindow = Json.F(turbo["m_miniBoostDelay"]);
            p.ReverseKmh = Json.F(engine["m_reverseTopKMPH"]);
            p.DriftMinKmh = Json.F(drift["m_BrakeTimeEndGeneralDriftingMinMPH"]);
            return p;
        }
    }
}
