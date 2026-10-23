# Luật lái kart đọc từ code gốc (Zing Speed Mobile, IL2CPP ARM32)

Nguồn (bản dựng lại trên binary đã gỡ XOR, `UDK_DUMP=~/unity-decode-kit/out/zs`):
- C#: `out/zs/code/decomp/NssMain/<File>.c:dòng` (ghi tắt `File.c:dòng`).
- Native: `out/zs/native/libNssPhysicsPlugin/<File>.c:dòng` (ghi `native/File.c:dòng`).
- Vài lớp không nằm trong bộ decompile mặc định (`FallBehindCompeMgr`, `EvtMini`, `PhyPluginUtils`) được
  decompile riêng bằng `tools/ghidra/decompile.py`; trích dẫn ghi `Lớp.Hàm @địa_chỉ`.
- Giá trị tham số: `Assets/ZS/Resources/ZSData/carparams/*_00.asset.json`. Curve ghi `(x→y, ...)`.
- "(suy luận)" = code không nói thẳng.

## 0. Kiến trúc và quy ước đọc code

- C# tính tham số mỗi frame (`PHYSICS_SIMULATE_FRAMERATE = 60`) rồi đẩy xuống `libNssPhysicsPlugin.so`
  (Havok). Native giờ đọc được. Đường đi chính mỗi bước:
  - Gia tốc động cơ: `KartMotorization::_simulateKartMotorization` (`native/KartMotorization.c:307`).
  - Lực hãm (phanh, vượt tốc): `KartMotorization::_simulateVelocityDamp` (`native/KartMotorization.c:181`).
  - Drift slerp vận tốc, mô-men lái, ma sát ngang: `PhyVehicleExtraVelocity::applyExtraVelocity`
    (`native/PhyVehicleExtraVelocity.c:114`).
  - Kẹp tốc độ quay yaw: `KartMotorization::_limitChassisYawAngleVelocity` (`native/KartMotorization.c:653`).
  - Va tường: `KartWallCollider` (`native/KartWallCollider.c`).
  - Các export `PHY_VehicleSolveDrifting/Steering/EngineAcceleration` của C# KHÔNG được C# gọi (`udk callers` = 0);
    native tự gọi `NssSolveVehicle*` bên trong.
- Bộ giải `NssVehicleVelocitySolver` (= `tPHY_VEHICLE_VELOCITY_SOLVER`) là dãy float liền nhau. Offset ghi trong
  `types.cs` cho struct này là rác; offset thật suy từ thứ tự field và khớp với chỗ native ghi kết quả (0x4C..0x54):

  | off | field | off | field |
  |---|---|---|---|
  | 0x00 | deltaTime | 0x34 | param0_0 |
  | 0x04 | moveDir xyz | 0x38 | param0_1 |
  | 0x10 | carPlane xyz (pháp tuyến mặt đường) | 0x3C | param0_2 |
  | 0x1C | linvel_i xyz | 0x40 | param1_0 |
  | 0x28 | angvel_i xyz | 0x4C | linvel_o xyz |
  | | | 0x58 | angvel_o xyz |

  `tPHY_POWERCHAIN_PARAM` theo thứ tự field: `[0] m_reversedForce, [1] m_maxEngineTorque, [2] m_topSpeedMPS,
  [3] m_factor, [4] m_enable` (native `PHY_VehicleSetPowerChain`, `native/PHY.c:2556-2563`).
- Tên slot interface trong pseudocode Ghidra lệch một ô: hàm thật là hàm ĐỨNG TRƯỚC nhãn trong khai báo interface.
  Kiểm chứng: `IInputModule` slot 7 nhãn `GetKeyUp` là `GetKey`; `IVehicleDriftHelper` slot 3 nhãn `ReleaseDrifting`
  là `GetXvdAngle(steering)`. Chỉ số slot (`func_0x087dd32c(obj, iface, N)`) thì tin được.
- Đơn vị: `*KMPH` là km/h, đổi sang m/s bằng `* 0.2777778`. "Torque" trong C# thật ra là GIA TỐC, đơn vị km/h mỗi giây
  (xem 2a). `VdAngle` (`NssVehicleSimOutput.mVdAngle`) là góc giữa vận tốc và mũi xe, đơn vị độ.
- `NssVehicleState`: None=0, Normal=1, Drifting=2, Leaping=3, Damaged=4. `DriftStateType`: NormalDrift=0,
  SidewaysDrift=1, Sliding=2. `InputKeyType`: SmallBoost=5, Brake=6, Drift=8, SidewaysDrift=9.

## 1. Tốc độ tối đa — ĐÃ RÕ (trừ con số per-kart)

- Chỉ số per-kart `NssVehicleCoreData.m_vehicleTopSpeed` (km/h) lấy từ bảng TDR `VehicleBaseParamConfig.fMaxSpeed`
  (`NssVehicleCoreData.c:382`) hoặc `Protocol.VehicleParameter` (server). Bảng này KHÔNG có trong client.
- `mCoreTopSpeedMPS = m_vehicleTopSpeed * 0.2777778` (`NssPhyVehiclePowerChain.c:1151`).
- Mỗi frame, `NssPhyVehiclePowerChain.Update` (`NssPhyVehiclePowerChain.c:1698`):
  ```
  baseTop = ga ≥ -0.1 ? ECU.ReadNormalTopMPS() : m_reverseTopKMPH*0.2777778                     (:1798-1817)
  top     = SteerDampKMPH*0.2777778 + mMPSScaleCoef*mBaseMPSScaleCoef*(baseTop + mExtraKMPH*0.2777778)   (:1826-1828)
  top    -= mDelExtraKMPH*0.2777778   (khi boost đang tắt dần, :1830-1843)
  top     = min(top, mForceTopMPS)    (mặc định 1000, :1844; NssPhyVehiclePowerChain.c:33)
  ```
  `*(iVar2+8) < -0.1` là input ga lùi (suy luận: cùng giá trị đó `|x|<0.0001` thì mô-men = 0, `:2062`).
  `mExtraKMPH` là tổng các khoản cộng trong frame (nitro, mini boost, catch-up qua `AddExtraKMPH`), reset 0 cuối frame (`:2071`).
- Native dùng `top` thế nào: `m_topSpeedMPS` thành `VehicleKartParam.maxLinvel` (+0x24, `native/VehicleKartParam.c:126`),
  rồi làm trần cho gia tốc (mục 2b). Vượt trần thì bị hãm bằng `m_extraReverseForceCurve` (mục 2c).
- Gợi ý độ lớn (suy luận): `CarDrift_00.m_driftHighKMPH_Curve` = (180→200, 210→210) lấy x = top km/h, nên kart thường
  có top 180–210 km/h. `m_reverseTopKMPH = 106.39`.

## 2. Gia tốc / power chain — ĐÃ RÕ công thức, thiếu số per-kart

a) C# (`NssPhyVehiclePowerChain.Update`):
  ```
  baseTorque' = PhyPluginUtils.EvaluateBaseTorque(SimOutput.mMps, baseTopMps, ECU.ReadParamEngineTorque(),
                                                  m_basePowerChainFactor)                          (:2026-2031)
  mTorque     = mTorqueScaleCoef * dir(±1) * (baseTorque' + mExtraTorque) * 0.2777778               (:2034)
  nếu đang phanh hoặc |ga| < 0.0001: mTorque = 0                                                      (:1974, :2062-2064)
  m_maxEngineTorque = mTorque ; tên biến C# là mCurFinalEngineAcceleration                          (:2182-2184)
  ```
  `* 0.2777778` cho thấy "torque" có đơn vị km/h/s, sau đổi ra m/s². Mô-men gốc per-kart `m_maxTorque`
  (`fBasicDynamic`, `NssVehicleCoreData.c:35`) không có số trong client.
  `EvaluateBaseTorque @0x0B771B14` (NssPlugins, đọc từ asm):
  ```
  x = baseTopMps > 1 ? curMps / baseTopMps : 1
  baseTorque' = m_basePowerChainFactor(Clamp01(x)) * baseTorque
  ```
  CarEngine_00: `m_basePowerChainFactor` = (0→1.0, 1→0.2).

b) Native (`_simulateKartMotorization`, `native/KartMotorization.c:337-349`; `NssSolveVehicleMotorization_EngineAcceleration`,
`native/NssSolveVehicleMotorization.c:14-51`):
  ```
  a_muốn = m_maxEngineTorque * m_factor                                  (solver 0x3C * 0x40)
  v_ph   = |linvel − n(n·linvel)|      (tốc độ trong mặt đường)
  a_trần = clamp(top − v_ph, 0, top) / dt
  a      = sign(a_muốn) * min(|a_muốn|, a_trần)                            (m/s²)
  linvel += a * dt * moveDir
  ```
  Nghĩa là: gia tốc là hằng số theo frame (không có khối lượng), chỉ bị cắt để không vượt `top` trong một bước.
  Ga không đi xuống native; C# tự đặt `mTorque = 0` khi nhả ga.

c) Lực hãm (`m_reversedForce`), đi vào `NssSolveVehicleLinearVelocityDamp_Deceleration`:
  ```
  Δv mỗi bước = F * chassis[+0x1AC] * dt, trừ vào vận tốc trong mặt đường, không đổi chiều
                                                        (native/NssSolveVehicleLinearVelocityDamp.c:26; native/KartMotorization.c:289-292)
  F (vượt tốc) = m_extraReverseForceCurve((v_ph − top)*3.6) * 0.2777778 * 1000 * (1 hoặc m_leapExtraReverseForceCoef khi bay)
                 chỉ khi v_ph − top > 0.01                                     (NssPhyVehiclePowerChain.c:588-633)
  F (phanh)   += coef * curve(x) * 0.2777778 * 1000, curve = m_brakeReverseForceCurve (lái thường) hoặc
                 m_DriftingBrakeReverseForceCurve (drift); coef = m_heavyBrakeCoef khi phanh gấp, ngược lại 1+ExtraBrakeCoef  (:1963-2016)
  ```
  `chassis[+0x1AC]` nhiều khả năng là 1/khối lượng của Havok; hệ số `*1000` gợi ý khối lượng chuẩn 1000 kg, khi đó
  giảm tốc = y của curve tính bằng km/h/s (suy luận). x của curve phanh lấy từ một hàm ECU, phạm vi 0..300 nên là km/h (suy luận).
  CarAerodynamic_00: `m_extraReverseForceCurve` = (0.05→0, 0.2→1, 300→400) (x = km/h vượt trần);
  CarEngine_00: `m_brakeReverseForceCurve` = (0→80, 300→200), `m_DriftingBrakeReverseForceCurve` = (80→10, 200→20),
  `m_heavyBrakeCoef = 2`, `m_heavyBrakeTime = 0.2`.

d) Hệ số công suất `m_factor` (`_UpdatePowerChainFactor`, `NssPhyVehiclePowerChain.c:2188`):
  ```
  state != Drifting:                       factor = 1                                                 (:2300)
  Drifting và cờ ECU slot 0x1f = false:    factor = 0                                                 (:2266-2268)
  Drifting và cờ đó = true:                factor = m_powerChainFactorCurve(|VdAngle|) + mExtraPowerChainFactor   (:2279-2295)
  đang dập công suất sau va chạm:          factor = mCollisionDumpPowerCurve(t giây từ lúc va)              (:2307-2313)
  m_factor = Clamp01(factor)                                                                           (:2327-2328)
  ```
  `mExtraPowerChainFactor = m_extraPowerChainFactor` chỉ gán trong `ForceEnablePowerChain` (`:979`), mặc định 0.
  CarEngine_00: `m_powerChainFactorCurve` = (50°→1.0, 90°→0.05); `m_extraPowerChainFactor = 0.5`.

## 3. Lái thường (yaw) — ĐÃ RÕ phần trần yaw, mô-men lái là xung Havok

- Trần yaw lúc lái thường, `NssVehicleDriftCtrl.GetNormalDrivingMaxAngVel` (`NssVehicleDriftCtrl.c:605-674`):
  ```
  x       = kmph / m_vehicleTopSpeed   (0 nếu kmph ≤ 0.001)                                    (:640)
  yawMax  = curve(x) * mDrivingMaxAngVelScaleCoef(=1)                                          (:656)
  nếu steering != 0: yawMax *= m_JoystickHorizontalDrivingIntensityScale(|steering|)          (:661)
  return yawMax * 0.017453292   (độ/s → rad/s)                                                 (:674)
  ```
  `curve` = `NssVehicleDriftData.GetMaxSteeringAngSpeedCurve()`: trả curve ghi đè nếu có, không thì
  `m_steerCommonParams.m_drivingMaxAngSpeedCurve` (`NssVehicleDriftData.c:19-37`).
- `NormalState` (`NssVehicleDriftCtrl.c:1443`) gọi `helper.SetMaxYawVel(yawMax)` mỗi frame. `SetMaxYawVel`
  (`NssVehicleDriftHelper.c:1815`): nếu |yaw hiện tại| > trần thì trần được kéo từ |yaw| về giá trị đích bằng
  `func_0x087e41b8(|yaw|, trần, damping*dt)` (nhiều khả năng Mathf.Lerp hoặc MoveTowards, suy luận), damping =
  `m_normalSpinDamping`=5 khi |yaw| < `m_collisionThreshold`=1.4 rad/s, ngược lại `m_collisionSpinDamping`=8
  (`:1980-2017`); rồi `*= mDriveAssistDriftMaxAngelSpeedCoef` (`:2021`) và đẩy xuống
  `PHY_VehicleParamKartSetMaxAngularVelocity`.
- Native kẹp cứng: `|ω_yaw cục bộ| ≤ maxAngVel + extraMaxAngVel + |phần ω thêm đang tắt dần|`
  (`native/KartMotorization.c:698`; setter `native/PHY.c:6087`).
- Mô-men lái: `NssPlayerVehicleDrivingCtrl.GetSteeringValue` (`NssPlayerVehicleDrivingCtrl.c:1`):
  ```
  dấu = sign(steering), đảo nếu ga < -0.1
  giá_trị = dấu * m_assistSteerTorqueCurve(thời gian giữ phím lái, s) * mSteerTorqueScaleCoef(=1) * (-30) * dt
  ```
  `dt` xác nhận từ asm (`r2` chép sang `r8` ở 0x03CF4400, nhân ở 0x03CF45B8). Chỉ tính khi |steering| > 0.0001.
  Thời gian giữ lái là `mSteeringTimeL/R` theo dấu steering (`GetSteeringTime`, `NssPlayerVehicleDrivingCtrl.c:174`).
  Kết quả đi qua `PHY_VehicleDoSteering(giá_trị)` (`NssPlayerVehicleDrivingCtrl.c:489` → `NssPhyVehicleCtrl.c:2725`); native ghi vào
  `PhyVehicleExtraVelocity` (+0x0C, cờ +0x09; `native/PhyVehicleBase.c:342`) và áp vào vận tốc góc của thân xe
  quanh trục đứng (`native/PhyVehicleExtraVelocity.c:222-231`, nhân hằng này với một vector lấy từ thân xe, suy luận là trục đứng). Mô-men rất lớn so với trần
  (650–700 × 30), nên yaw thực tế gần như luôn bằng trần ở trên (suy luận).
- CarSteer_00: `m_drivingMaxAngSpeedCurve` = (0.24→82, 0.70→58, 0.95→40) độ/s, x = tỉ lệ tốc độ;
  `m_JoystickHorizontalDrivingIntensityScale` = (0→0.6, 0.8→1.0), x = |steering|;
  `m_assistSteerTorqueCurve` = (0→700, 1→650), x = giây giữ lái.
- Góc bánh lái native: `m_maxSteeringAngle = 0.61086524` rad (35°), `m_maxSpeedFullSteeringAngle = top m/s`
  (`NssVehicleBasePhyParamHelper.c:197,207`). `m_maxSteerAngleCurve` (0→32, 300→20) đọc ở
  `NssPlayerVehicleDrivingCtrl.SetMaxSteeringAngle` (`NssPlayerVehicleDrivingCtrl.c:909-916`), x là km/h (suy luận).
  Với kart, bánh lái chỉ ảnh hưởng tiếp xúc; hướng quay do mô-men lái + trần yaw quyết định (suy luận).

## 4. Drift — ĐÃ RÕ vào/giữ/thoát và lerp vận tốc

Vào drift
- Phím drift (`InputKeyType.Drift`) đi qua `NssPhyVehicleCab.BeginDrifting(dir)`: cần hướng Left/Right, `None` thì
  không (`NssPhyVehicleCab.c:63`). `NssPlayerVehicleDrivingCtrl.Handbrake` chỉ phát `OnHandBrakePressing` khi xe đang
  Normal (state 1) (`NssPlayerVehicleDrivingCtrl.c:545-548`); nhả phím phát `OnHandBrakeReleased` (`:537-541`).
  Bên nhận sự kiện không thấy trong C# (có thể Lua); việc nó gọi `ECU.ToggleDrifting/ReleaseDrifting` là suy luận theo tên.
- `NssVehicleDriftHelper.ToggleDrifting(steering)` (`NssVehicleDriftHelper.c:225`):
  - từ chối nếu `mForceEndDrift` (khóa sau va chạm, `:281`) hoặc đang Drifting mà chưa Sliding (`:299`);
  - hướng drift = steering > 0 ? 1 : 2 (`:306-308`);
  - vào lại từ Sliding: phải cùng hướng cũ (`:337`) hoặc lái ngược dấu VdAngle (`:329`);
  - rồi `_BeginDrift`: ECU sang Drifting, lưu vận tốc trước drift, đổi velocity damper sang Drift (`:2032-2128`).
- KHÔNG có ngưỡng tốc độ vào drift trong C#. `m_driftMinKMPH` (`fDriftMinKMPH`) chỉ dùng làm sàn tốc độ đích khi drift
  (`mCoreDriftMinMPS`, `NssVehicleDriftHelper.c:1499-1504`, dùng ở `:1008`). Sửa lại nhận định cũ.
- Drift ngang (Sideways): ở tick đầu, nếu giữ phím `SidewaysDrift`(9) và |VdAngle| < `EnterSidewaysDriftingMaxVDAngle`=40
  thì sang SidewaysDrift (`DriftState_Normal.c:83-110`). Giữ tối thiểu `SidewaysDriftingMinKeepTime`=0.37 s
  (`DriftState_Sliding.c:737`), tối đa 1.5 s.

Giữ drift (`DriftState_Normal.Update`, `DriftState_Normal.c:2240`)
- Drift tiếp tục khi: steering cùng hướng drift và ≠ 0 (`:2362`) VÀ (steering × VdAngle ≤ 0 hoặc một cờ của motion) (`:2411`).
  Không cần giữ phím drift: nhả phím mà vẫn giữ lái thì `ReleaseDrifting` không làm gì (`NssVehicleDriftHelper.c:1567`).
- Mỗi frame khi drift:
  - mô-men quay: `(m_driftTurnExtraTorqueCurve(xvd) [+ m_driftTurnExtraTorqueCurve_1(xvd) nếu vào từ lái thường]
    [+ m_SidewaysDriftToNormalDriftExtraTorqueCurve]) * dấu * (-30) * dt` → `DoSteering` (`DriftState_Normal.c:1548-1599`;
    thừa số cuối là `dt` theo cùng mẫu với lái thường, suy luận);
  - trần yaw = `m_driftMaxAngSpeed`=175 độ/s (`NssVehicleDriftHelper.c:1809`);
  - lerp vận tốc (bên dưới) với hệ số `m_driftForwardExtraForceCurve(xvd)` (`DriftState_Normal.c:2739`).
  - `xvd = GetXvdAngle(steering)` = |VdAngle|, đổi dấu âm khi VdAngle × steering > 0 (`NssVehicleDriftHelper.c:637-676`).
- Phanh trong drift kiểu "general": nếu tốc ≤ `m_BrakeTimeEndGeneralDriftingMinMPH`=45 (so với km/h dù tên là MPH)
  thì kết thúc (`DriftState_Normal.c:1459`); điều kiện kích hoạt nhánh này là một cờ ECU (suy luận: đang phanh).

Thoát drift
- Khi steering về 0 hoặc đổi hướng (`DriftState_Normal.c:2617-2644`): `helper.ReleaseDrifting()`; nếu Drifting và
  steering == 0 thì `_EndDrift` (damper về Normal) và `mIsDriftSliding = true` (`NssVehicleDriftHelper.c:1564-1576`).
  Máy trạng thái sang Sliding.
- Sliding (`DriftState_Sliding.Update`, `DriftState_Sliding.c:2378`): trần yaw = `m_SlidingMaxAngSpeed`=128 độ/s × |steering|
  (`:2479`, `:2528`); lerp vận tốc với `m_slidingVelocityDirLerpCurve(|VdAngle|)` (hoặc bản small) (`:2115-2118`).
- Sliding kết thúc, xe về Normal (`SwitchStateToNormal`, `DriftState_Sliding.c:664`) khi |VdAngle| <
  `m_driftEndParamsLerpVDThreshold`=50 (`:769`), và với drift nhỏ/vừa còn cần thời gian trượt > `m_smallDriftOutSlidingTime`=0.4 s (`:784`).
- Vào lại drift từ Sliding không cần bấm phím: lái cùng hướng cũ và |VdAngle| > `m_SlidingStateEnterContinuousDriftMinVdAngle`=50
  (hoặc nằm trong cửa sổ swing-tail 0..18°) (`DriftState_Sliding.c:563-655`).
- Sau va chạm: `mForceEndDrift` tự xóa khi `mForceEndDriftDelay > m_driftCollisionEndDelay`=0.1 s (`NssVehicleDriftCtrl.c:2792`).

Lerp vận tốc khi drift / sliding (C# `DriftingSlideVelDamping`, `NssVehicleDriftHelper.c:735`; native
`NssSolveVehicleDrifting_SlerpLinvel`, `native/NssSolveVehicleDrifting.c:1`, gọi từ `native/PhyVehicleExtraVelocity.c:352-360`)
  ```
  C#:
  s        = mCurSlerpScaleCoef (về 1 với tốc m_DriftingCollisionVehicle_VelLerpScaleBackSpeed=0.6/s, +0.7 khi bấm lại hướng
             sau va chạm, kẹp 0..1)                                                       (:844-853)
  f        = Clamp01(m_driftSpeedFactorCurve(|Vd|))   (NormalDrift)
             Clamp01(m_SidewaysDriftSpeedFactorCurve(|Vd|)) (Sideways), Clamp01(m_slideSpeedFactorCurve(|Vd|)) (Sliding)   (:910-930)
  highKmph = m_driftHighKMPH_Curve(m_vehicleTopSpeed)   (UseNewDriftDecel=0)                     (:959)
  target   = driftMinMps + f*(highKmph*0.2777778 − driftMinMps)    (m/s)                         (:1008)
  acc      = GetExpAcc(|Vd|, cur − target)  (km/h/s), theo asm @0x09CF3A5C:
             |Vd| < m_driftAccVDAngleThreshold → m_driftAccelerationCurveEx((cur−target)*3.6) * mSuperDriftAccExCoef
             |Vd| ≥ ngưỡng                     → m_driftAccelerationCurve((cur−target)*3.6) * mSuperDriftAccCoef * mDriveAssistDriftAccCoef
             * m_OnGroundAutoEnterDriftAcc[Ex]Coef nếu tự vào drift khi chạm đất; * m_heavyBrakeCoef nếu phanh gấp   (:582-632)
             (pseudocode Ghidra ở :580 đọc chiều so sánh ngược lại; asm đáng tin hơn, nhưng nên kiểm lại khi remake)
  exAcc    = (|Vd| < 83, vừa vào từ lái thường, không phải sideways) ? m_ExtraAccel_NormalToDrift(cur*3.6)*0.2777778 : 0   (:870-895)
  PHY_VehicleApplyExtraDriftFriction(slerpCoef = s * hệ_số_lerp * dt,
                                     extraMPS  = (exAcc + acc*0.2777778) * dt)       (:1042; dt ở cả hai, xác nhận từ asm 0x09CF3534/353C)
  native:
  v_ph  = linvel − n(n·linvel); hướng đích = moveDir (đảo nếu ga < −0.1)
  dir'  = slerp(normalize(v_ph), moveDir, slerpCoef)
  linvel_o = n(n·linvel) + (|v_ph| + extraMPS) * dir'                         (native/NssSolveVehicleDrifting.c:43-216)
  ```
  `hệ_số_lerp` = `m_driftForwardExtraForceCurve(xvd)` khi NormalDrift (tên "ForwardExtraForce" nhưng dùng làm hệ số slerp),
  `m_slidingVelocityDirLerpCurve(|Vd|)` khi Sliding (`DriftState_Sliding.c:2042-2181`). Đơn vị: 1/s.
- Giá trị:
  - CarDrift_00: `m_driftForwardExtraForceCurve` = (-30→0.8, 0→0.1, 45→0.1, 86→1, 100→2, 105→2.2), x = xvd độ;
    `m_slidingVelocityDirLerpCurve` = (46→3.5, 55→0.85, 90→0.85, 100→2, 105→2.2), x = |Vd| độ;
    `m_smallSlidingVelocityDirLerpCurve` = (40→1.1, 45→0.82, 90→0.82, 100→2, 105→2.2);
    `m_driftHighKMPH_Curve` = (180→200, 210→210), x = top km/h, y = km/h.
  - CarDriftExt_00: `m_driftSpeedFactorCurve` = (83→0.45, 96→0.05); `m_SidewaysDriftSpeedFactorCurve` = (10→0.8, 50→0.65);
    `m_slideSpeedFactorCurve` = (0→0.8, 74→0.35, 96→0.05) (x = |Vd| độ, y = tỉ lệ 0..1);
    `m_driftAccelerationCurve` = (0→0, 20→-80, 60→-100, 140→-320); `m_driftAccelerationCurveEx` = (0→0, 1.5→-20, 20→-120,
    70→-120, 90→-160) (x = km/h vượt target, y = km/h/s); `m_driftAccVDAngleThreshold = 83`;
    `m_ExtraAccel_NormalToDrift` = (0→150, 115→80, 130→0) (x = km/h, y = km/h/s);
    `m_driftTurnExtraTorqueCurve` = (-15→200, 0→200); `m_driftTurnExtraTorqueCurve_1` = (0→50, 20→100, 80→150) (x = xvd);
    `m_driftMaxAngSpeed = 175`, `m_SlidingMaxAngSpeed = 128` (độ/s).
- Mất tốc khi drift gồm: (1) power chain bị nhân `m_powerChainFactorCurve(|Vd|)` (mục 2d); (2) giảm tốc về `target`
  theo `m_driftAccelerationCurve`; (3) hướng vận tốc bị slerp về mũi xe nên tốc trong hướng đi giảm (suy luận).
  Ma sát ngang khi không drift đi qua `PHY_VehicleApplySideVelocityDamp(dampKMPH_P_SEC)` (`NssPhyVehicleMotionCtrlHelper.c:963`;
  native nhân 0.27778 rồi gọi `NssSolveVehicleFriction_SideLinvelDamp_ForNewStable`, `native/PhyVehicleExtraVelocity.c:336-341`).
  Nối nó với `m_sideVelocityDeceleration` (0→45, 60→45, 70→200, x là góc) là suy luận; chưa đọc thân hàm friction.

## 5. Nitro và mini boost — ĐÃ RÕ cơ chế, thiếu số per-kart

- Thanh nitro đầy = 1000 (`SingleNitrogenCollect.c:2364` `uVar5 - 1000`, `mCurShowPower = 999` ở `:2386`). Đầy thì tạo 1 bình
  nếu còn ô (số ô không settled; remake dùng 2).
- Chỉ nạp khi `mCurVehicleState == 2` (Drifting) (`SingleNitrogenCollect.c:5174`). Lượng nạp mỗi frame (`CalcCollectPower`, `:1915`):
  ```
  speedDiff = mLastSpeed − curSpeed                                      (:1663)
  p = speedDiff * (mSpeedDiffAccumFactor + ExtraSpeedDiffAccumFactor)    (:1701-1709)
  p *= m120/m165VelDiffCollectCompensateCoef ở 120/165 fps
  nếu p < compensate * TARGET_COMP_COEF_SCALE_FPS_30 * dt: p = sàn theo compensate * curve * dt     (:2096-2100)
  p = min(p, 100) * mFailAccumValueScaleCoef * (1 + mExtraAccumValueScaleCoef)                     (:2139-2146)
  ```
  `mSpeedDiffAccumFactor` ↔ `fGatherNitroSpeed` (per-kart, suy luận); curve sàn ↔ `CarTurbo_00.m_compensatePowerFactorCurve`
  (0→0.95, 70→1.25), x = VdAngle (suy luận).
- Bình nitro (`EvtN2O.OnRefreshVehicleParams`): `ExtraKMPH = m_boostExtraMaxKMPH`, `ExtraTorque = m_N2OBoostTorque`,
  `Duration = m_N2OBoostDuration + mExParamDuration` (`EvtN2O.c:423, 437, 504`). Ba số per-kart. Cộng vào `top` (mục 1)
  và vào gia tốc (mục 2a).
- Mini boost (`EvtMini`, decompile riêng):
  - Theo dõi `|VdAngle|` lớn nhất khi đang drift NormalDrift (`UpdateDriftingMaxAbsVDAngle @0x07801988`).
  - Khi drift đã ngắt: nếu góc lớn nhất > `m_miniBoostMinVDAngle`=35 và |VdAngle| hiện tại < `m_miniBoostMaxVDAngle`=50
    thì mở cửa sổ (`UpdateInCheckingCanTrigger @0x078023A0`).
  - Cửa sổ kéo dài `CanTriggerTime = m_miniBoostDelay`=1.4 s (+0.5 s trên mặt nước) (`OnRefreshVehicleParams @0x078004B8`).
  - Bấm phím `SmallBoost`(5) trong cửa sổ (`VehicleInputHelper.ProcessSmallBoost`, `VehicleInputHelper.c:3822`). Phím này
    thử lần lượt LeaveWater → Mini → Dual → Air → FallingGround → PassiveMini và lấy loại đầu tiên đang mở
    (`ExtraVelocityTriggerMgr.GetCanTriggerExtraVelType @0x09978864`).
  - Hiệu ứng: `ExtraKMPH = m_miniBoostExtraMaxKMPH`, `ExtraTorque = m_MiniBoostTorque`, `Duration = m_miniBoostDuration`
    (per-kart `fSmallNitro*`).
  - `GetHasTriggerMiniBoostDriftDir` trên ECU chỉ là stub hotfix trả `None` (`NssPhyVehicleECUBase.c:1210`); sửa nhận định cũ.
- Dual boost: `mDualBoostDelayCheckTime = 0.2`, `m_LastDriftKeyAndFirstMiniBoostTime = 1` dùng trong `EvtDual`; chưa đọc.

## 6. Va tường — ĐÃ RÕ vận tốc thẳng và yaw; còn mở: luật ngắt drift, khóa lái khi đâm liên tiếp

Native `KartWallCollider` (đã đối chiếu asm ở chỗ pseudocode hỏng). Curve native là Hermite giữ giá trị đầu/cuối ngoài
khoảng key, tương đương `AnimationCurve.Evaluate`. Bảng curve nạp qua `PhyVehicleBase::_GetCurve` (`native/PhyVehicleBase.c:2392`),
nguồn C# trong `NssPhyVehicleCtrl.c` (BUMPMPS `:94`, DUMPTANGVEL `:6510`, DUMPTANGVEL_FRONT `:6520`, ANGVEL `:6530`...).

Phân loại cú va (`native/KartWallCollider.c:1630-1695`, `_SetWallCollisionPart` `:1143`)
- `n_w` = pháp tuyến tiếp xúc chiếu lên mặt đất, chuẩn hóa. `góc = acos(forward·n_w)` độ (0..180).
- góc < 85 → Tail; < 105 → Side; < 150 → HeadS (đầu-hông); còn lại → HeadF (đâm thẳng đầu).
- Chọn curve (`:925-1022`):
  - HeadF: bounce = `m_fenceCollisionBounceKMPH` (bản `_Drift` khi drift), tiếp tuyến = `m_frontFenceCollisionDumpTangVelCoef`.
  - Không drift, HeadS/Side/Tail: `m_fenceCollisionBounceKMPH` + `m_fenceCollisionDumpTangVelCoef`; riêng HeadS mà lái ngược
    phía tường thì tiếp tuyến nhân thêm `m_extraSteerCollWallTangentCoef`=0.9.
  - Drift, HeadS/Side/Tail: nếu hướng drift × phía tường < 0 → `_Drift` bounce + front tangential; ngược lại
    `_DriftOpp` bounce + `DumpTangVelCoefDriftOpp`.

Vận tốc thẳng (`_SolveExtraCollisionLinearvel`, `native/KartWallCollider.c:846`)
  ```
  x      = |v·n_w| * 3.6                            (km/h đâm vào tường, dùng cho cả hai curve)
  nếu v·n_w ≤ 0 (đang lao vào tường):  v_n' = n_w * BOUNCE(x) * 0.27778 * m_bumpDampCoef   (m/s, bật ra khỏi tường)
  v_t'   = v_t * DUMPTANG(x) * (1 hoặc 0.9)
  v_dọc  bị bỏ, trừ khi bật cờ giữ vận tốc dọc
  ```
  `m_bumpDampCoef` mặc định chưa tìm thấy (suy luận = 1). Tốc mất = phần pháp tuyến (bị thay bằng bounce) + `1 − DUMPTANG(x)` của phần tiếp tuyến.

Yaw (`_SolveExtraCollisionAngularvel`, `native/KartWallCollider.c:747`)
  ```
  extra = ANGVEL(x) [độ/s] * 0.0174533 * ANGCOEF(góc va) * C330(=1)
  d     = ±1 theo phía tường so với vector phải của xe
  nếu ω yaw hiện tại cùng chiều d: ω' = d*(extra + min(|ω|, MAXANGVEL(|v_sau| km/h)*0.0174533))
  ngược lại:                       ω' = d*extra   (trừ khi lái ngược d và |ω| > extra: giữ ω, kẹp ±0.698 rad/s)
  ```
  ANGCOEF = `m_fenceCollisionAngularCoefCurve` (HeadF, HeadS, khi drift) hoặc `_NoDrift` (Side/Tail khi không drift).

Phía C# (`NssPhyVehicleESP._WallCollisionProcess`, `NssPhyVehicleESP.c:2511`) — sửa nhận định cũ về section:
- part 2 = HeadS: thời gian bị bật `m_collideWallBouncedTime(tốc*3.6)` (`:2713`); tốc đó nhiều khả năng là tốc vào tường (suy luận).
- part 1 = HeadF: đưa `m_frontFenceCollisionPowerDumpCurve` vào power chain (`:2799`), `m_factor = curve(t giây từ lúc va)` (mục 2d).
- Đâm liên tiếp (`_MultiWallCollisionLockSteer`): gọi một slot với (true, `m_multiWallCollideSpeedDownTime`=0.5); tên hàm cho
  thấy là khóa lái, không phải cắt tốc trực tiếp (suy luận). Luật đếm số lần chưa giải.
- Ngắt drift khi va tường: native trả cờ `mCollisionWallIsBreakDrifting` nhưng chưa tìm ra luật đặt cờ. `m_collisionBreakDriftDeltaKMPH`
  chỉ dùng cho va xe–xe.

CarCollision_00 (x = km/h vào tường trừ khi ghi khác):
| Curve | Điểm | y |
|---|---|---|
| `m_fenceCollisionBounceKMPH` | 0→0, 60→22, 150→70 | km/h bật ra |
| `_Drift` / `_DriftOpp` | 0→0, 100→20 / 0→0, 150→90 | km/h bật ra |
| `m_fenceCollisionDumpTangVelCoef` (và DriftOpp) | 0→0.98, 80→0.75 | hệ số giữ |
| `m_frontFenceCollisionDumpTangVelCoef` | 0→0.5, 80→0.3 | hệ số giữ |
| `m_fenceCollisionAngularVelCurve` | 0→5, 140→140 | độ/s |
| `m_fenceCollisionAngularCoefCurve` | 0→0, 120→1, 150→1, 180→0 (x = góc va °) | hệ số |
| `m_fenceCollisionAngularCoefCurve_NoDrift` | 60→0, 100→0.3 (x = góc va °) | hệ số |
| `m_fenceCollisionMaxExtraAngularVel` | 140 hằng (x = km/h sau va) | độ/s |
| `m_collideWallBouncedTime` | 0→0.05, 100→0.2 | giây |
| `m_frontFenceCollisionPowerDumpCurve` | 0→0.4, 0.6→1.0 (x = giây từ lúc va) | hệ số công suất |

Va xe khác khi drift mất `m_DriftCollCarLossKmph = 20` km/h (chưa đọc lại code).

## 7. Catch-up (FallBehindCompeMgr) — ĐÃ RÕ

Decompile riêng; địa chỉ trong `libil2cpp.so`.
- Chạy mỗi tick của `PlayVehicleMgr.Update` (`FallBehindCompeMgr.Update @0x03670CC4`).
- `FallBehindDis` (m) = quãng đường của xe hạng 1 trừ của mình, tính theo `dist + round * mMapSingleRoundLen`
  (`GetFallBehindDis @0x03671684`, đọc từ asm).
- Công thức (`CalcFallBehindCompe @0x03671244`):
  ```
  x              = FallBehindDis / mMapTotalLen        (tỉ lệ cả cuộc đua, mMapTotalLen = 1 vòng × số vòng)
  CurCompeKMPH   = clamp(FallBehindCompeTopVelCurve(x)  * CompeCoefScaleRatio, 0, 40)   (MAX_COMPE_KMPH)
  CurCompeTorque = clamp(FallBehindCompeDynamicCurve(x) * CompeCoefScaleRatio, 0, 20)   (MAX_COMPE_DYNAMIC)
  ```
  Hai số này cộng vào `ExtraKMPH`/`ExtraTorque` qua `VhcActionAddExtraVelParam` (liên tục), tức cộng vào `top` và gia tốc.
  `CompeCoefScaleRatio` khởi đầu 1.0, chỉ đổi qua `AddCompeCoefScaleRatio`.
- `mFallBehindCompeCoefCurve` (PlayVehicleModuleConfig.json: 0→1.0, 0.15→1.3) chỉ vào `SkillFallBehindCoef`, không vào lực kéo.
- Tắt khi: xe là hạng 1 (`CanFallBehindCompe @0x03670F6C`), replay, `mMapTotalLen ≤ 0`, hoặc chế độ chơi thuộc danh sách
  loại trừ (shadow, gold coin, race king, ball…, `IsEnableFallBehindCompe @0x0366FE0C`). Có một bitmask theo loại người chơi
  (Real/AI) chưa giải được, nên chưa biết AI có được catch-up không.
- CarEngine_00: `FallBehindCompeTopVelCurve` = (0→0, 0.01→3, 0.12→17, 0.24→19) km/h; `FallBehindCompeDynamicCurve` =
  (0→0, 0.01→1.5, 0.12→8.5, 0.24→10.5) km/h/s. Sửa nhận định cũ: x là tỉ lệ trên tổng chiều dài đua, không phải trên 1 vòng.

## Việc cần làm cho remake (theo thứ tự lợi ích)

1. Gia tốc: `a = baseAccel(km/h/s) * m_basePowerChainFactor(Clamp01(v/top)) * 0.2777778 * m_factor`, cộng
   `ExtraTorque` (nitro, mini boost, catch-up) trước khi nhân 0.2777778; cắt để không vượt `top` trong một bước. Nhả ga
   thì a = 0. Hiện `Kart.cs` dùng nhầm curve drift làm curve theo % tốc độ; `m_powerChainFactorCurve(|Vd|)` chỉ áp khi drift.
2. Drift: giữ drift theo HƯỚNG LÁI, không theo phím: vào bằng phím drift + hướng khi đang lái thường; drift còn khi lái cùng
   hướng; nhả lái → Sliding; Sliding hết khi |Vd| < 50°; lái lại cùng hướng khi |Vd| > 50° thì drift tiếp không cần phím.
   Không có ngưỡng tốc độ vào drift.
3. Vận tốc khi drift: mỗi frame slerp hướng vận tốc (trong mặt đường) về mũi xe với `t = hệ_số_lerp * dt`
   (`m_driftForwardExtraForceCurve(xvd)` khi drift, `m_slidingVelocityDirLerpCurve(|Vd|)` khi Sliding), và cộng
   `(exAcc + acc*0.2777778)*dt` vào độ lớn, với `acc` kéo tốc về `target = driftMin + f(|Vd|)*(m_driftHighKMPH_Curve(top)/3.6 − driftMin)`.
4. Yaw: lái thường kẹp `|ω| ≤ m_drivingMaxAngSpeedCurve(kmh/top) * joystick(|steer|)` độ/s; drift kẹp 175 độ/s;
   Sliding kẹp 128 × |steer| độ/s. Mô-men lái đủ lớn để chạm trần (suy luận), nên remake có thể đặt thẳng ω = trần × dấu lái.
5. Tường: phân loại theo góc 85/105/150; thay phần pháp tuyến bằng `BounceKMPH(km/h vào tường)` bật ra; nhân phần tiếp tuyến
   với `DumpTangVelCoef` (front khi đâm thẳng); thêm yaw `AngularVelCurve × AngularCoefCurve(góc)`; đâm thẳng thì dập công suất
   theo `m_frontFenceCollisionPowerDumpCurve(t)`. Thay `WallSpeedLoss` hiện tại.
6. Mini boost: sau drift có góc đỉnh > 35°, khi |Vd| < 50° mở cửa sổ 1.4 s; bấm SmallBoost trong cửa sổ để nhận
   +km/h, +gia tốc trong `m_miniBoostDuration`.
7. Catch-up: `x = khoảng cách sau xe nhất / tổng chiều dài đua`; cộng `TopVel(x)` km/h (≤40) và `Dynamic(x)` km/h/s (≤20);
   tắt cho xe hạng 1.
8. Nitro nạp theo tốc độ mất khi drift (`speedDiff * factor`, sàn tối thiểu, ≤100/frame, đầy 1000) thay `NitroChargePerSecond`.
9. Phanh: giảm tốc = `m_brakeReverseForceCurve(km/h)` km/h/s (×2 khi phanh gấp); vượt top bị hãm theo `m_extraReverseForceCurve`
   (giả định khối lượng 1000 kg, suy luận).
10. Số per-kart (top km/h, gia tốc gốc `fBasicDynamic`, `fDriftMinKMPH`, nitro/mini boost km/h, gia tốc, thời lượng) vẫn
    không có trong client: giữ là hằng cấu hình và chỉnh theo clip.

Còn mở: luật ngắt drift khi va tường; khóa lái khi đâm tường liên tiếp; bên nhận sự kiện phím drift (có thể ở Lua);
hàm `func_0x087e41b8` trong `SetMaxYawVel`; bitmask Real/AI của catch-up; chiều so sánh ngưỡng 83° trong `GetExpAcc`;
thân hàm ma sát ngang `NssSolveVehicleFriction_*`; Dual boost.
