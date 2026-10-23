# Luật lái kart đọc từ code gốc (Zing Speed Mobile, IL2CPP ARM32)

Nguồn: bản decode của unity-decode-kit (`UDK_DUMP=~/unity-decode-kit/out/zs`). Trích dẫn dạng
`File.c:dòng` là đường dẫn trong `out/zs/code/decomp/NssMain/`. Giá trị tham số lấy từ
`Assets/ZS/Resources/ZSData/carparams/*_00.asset.json`. "(suy luận)" = code không nói thẳng.

## 0. Kiến trúc (đọc trước)

- Vật lý lõi chạy trong thư viện native `libNssPhysicsPlugin.so` (không decompile được), gọi qua
  `PhyDllWrapper` (`PHY_VehicleSolveEngineAcceleration`, `PHY_VehicleSolveDrifting`,
  `PHY_VehicleSolveSteering`, `PHY_VehicleSolveFriction`, `PHY_VehicleSetPowerChain`...).
  `PHYSICS_SIMULATE_FRAMERATE = 60`. Phần C# chỉ tính tham số rồi đẩy xuống mỗi frame:
  `tPHY_POWERCHAIN_PARAM { m_topSpeedMPS, m_maxEngineTorque, m_factor, m_enable, m_reversedForce }`.
  Vì vậy công thức gia tốc/ma sát/va tường cuối cùng nằm trong native: không đọc được.
- Nhiều hàm C# decompile hỏng ("bad instruction data"): `DriftState_Sliding.Update/ProcessVelLerp`,
  `NssVehicleDriftCtrl.GetNormalDrivingMaxAngVel`, `NssVehicleDriftHelper.*`, `NssPhyVehicleCab.BeginDrifting`...
  Với các hàm này chỉ còn chữ ký (tên tham số), ghi rõ ở từng mục.
- Tên slot vtable/interface trong pseudocode Ghidra hay lệch một ô; chỉ tin tên field (`->m_xxx`) và hàm gọi trực tiếp.
- Đơn vị: field `*KMPH` là km/h; code đổi sang m/s bằng `* 0.2777778`. `VdAngle` (`NssVehicleSimOutput.mVdAngle`)
  là góc giữa vận tốc và hướng mũi xe, đơn vị độ (suy luận từ phạm vi các curve 0..105 và `m_miniBoostMinVDAngle=35`).
- Trạng thái xe `NssVehicleState`: None=0, Normal=1, Drifting=2, Leaping=3, Damaged=4.
  `DriftStateType`: NormalDrift=0, SidewaysDrift=1, Sliding=2.

## 1. Tốc độ tối đa — ĐÃ RÕ (trừ con số)

Luật: tốc độ tối đa là chỉ số riêng từng xe `NssVehicleCoreData.m_vehicleTopSpeed` (km/h), lấy từ bảng TDR
`CSRes.VehicleBaseParamConfig.fMaxSpeed` (local) hoặc `Protocol.VehicleParameter` (server gửi). Bảng này
KHÔNG có trong dump client (chỉ thấy tên trong `JitPrecompile.txt`), nên không có số.

- `NssVehicleCoreData.c:382` `m_vehicleTopSpeed = cfg->fMaxSpeed` (`FromTdrLocalConfig`); bản server:
  `FromTdrProtocolConfig(VehicleParameter)`, gọi từ `VehicleMgr.RefreshVehicleParam`.
- `NssPhyVehiclePowerChain.c:1151` `mCoreTopSpeedMPS = m_vehicleTopSpeed * 0.2777778` (RefreshVehicleParams).
- Mỗi frame (`NssPhyVehiclePowerChain.Update`, `:1696`):
  ```
  baseTop = (tiến) ECU.ReadNormalTopMPS()          | (lùi, vận tốc dọc < -0.1) m_reverseTopKMPH*0.2777778   (:1813)
  top     = SteerDampKMPH*0.2777778 + mMPSScaleCoef*mBaseMPSScaleCoef*(baseTop + mExtraKMPH*0.2777778)   (:1823)
  top     = min(top, mForceTopMPS)                                                                     (:1840)
  ```
  `mExtraKMPH` là tổng các khoản cộng thêm trong frame (nitro, mini boost, catch-up... qua `AddExtraKMPH`),
  reset về 0 cuối frame. `mDelExtraKMPH` trừ dần khi hết boost (`DelExtraKMPHWithLongStep`).
- Các chỉ số per-kart khác cùng bảng (`VehicleBaseParamConfig`): `fBasicDynamic` (→ `m_maxTorque`, suy luận theo thứ tự field),
  `fGatherNitroSpeed`, `fSmallNitroExtraSpeed/Duration/Dynamic`, `fNitroExtraSpeed/Duration/Dynamic`,
  `fCompensatePowerFactor`, `fCollisionMass`, `fDriftColliderLossRate`, `fDriftMinKMPH`, `fStartBoost*`, `fSteerDecelerateMaxKMPH`.
- Gợi ý độ lớn (suy luận): `CarDrift_00.m_driftHighKMPH_Curve` = (180→200, 210→210) có trục x giống
  top speed km/h, nên kart thường có top 180–210 km/h. Giữ `TopKmh` là hằng số cấu hình.
- `m_reverseTopKMPH = 106.39` (CarEngine_00).

## 2. Gia tốc / power chain — PHẦN LỚN ĐÃ RÕ

Có hai curve khác nhau, trục x khác nhau. Bản remake hiện đang dùng nhầm curve.

a) `m_basePowerChainFactor` = curve mô-men theo TỈ LỆ TỐC ĐỘ. Ở `NssPhyVehiclePowerChain.c:2026` có lời gọi
`PhyPluginUtils.EvaluateBaseTorque(curSpdMps, baseTopSpdMps, baseTorque, speedRatioTorqueCurve)`. Trong disassembly
tại `0x098AF6D8..0x098AF70C`, đối số thứ 4 nạp từ `mCarParams->m_engineCommonParams + 0x0C`, tức
`m_basePowerChainFactor`, còn `curSpdMps` là `SimOutput.mMps`. Thân `EvaluateBaseTorque` (NssPlugins) không decompile được.
  ```
  baseTorque' = EvaluateBaseTorque(curMps, baseTopMps, engineTorque, m_basePowerChainFactor)
              ≈ engineTorque * m_basePowerChainFactor(curMps / baseTopMps)        (suy luận từ tên tham số)
  mTorque     = mTorqueScaleCoef * dir(+1/-1) * (baseTorque' + mExtraTorque) * 0.2777778          (:2030)
  ```
  Khi đang phanh: `mTorque = 0`, thay bằng lực hãm `m_brakeReverseForceCurve` (lúc lái thường) hoặc
  `m_DriftingBrakeReverseForceCurve` (lúc drift) `* coef * 0.2777778 * 1000` (`:2012`); x là một giá trị lấy từ ECU, tên slot không chắc (suy luận: km/h, vì phạm vi 0..300).
  Giá trị CarEngine_00: `m_basePowerChainFactor` = (0→1.0, 1→0.2) tuyến tính; `m_brakeReverseForceCurve` = (0→80, 300→200);
  `m_DriftingBrakeReverseForceCurve` = (80→10, 200→20); `m_heavyBrakeTime=0.2`, `m_heavyBrakeCoef=2`.

b) `m_powerChainFactorCurve` = hệ số công suất KHI DRIFT, x = |VdAngle| (độ). `_UpdatePowerChainFactor`
(`NssPhyVehiclePowerChain.c:2184`):
  ```
  if VehicleState != Drifting(2):  factor = 1, mExtraPowerChainFactor = 0                         (:2296)
  else if <cờ drift thứ hai của ECU> == false: factor = 0
  else factor = m_powerChainFactorCurve(|SimOutput.mVdAngle|) + mExtraPowerChainFactor            (:2289-2291)
  if va chạm đang dập công suất: factor = mCollisionDumpPowerCurve(t từ lúc va)                     (:2304)
  m_factor = Clamp01(factor)  → gửi native                                                         (:2323)
  ```
  `mExtraPowerChainFactor = m_extraPowerChainFactor` chỉ được gán trong `ForceEnablePowerChain` (`:979`), hàm này không
  có caller C# (có thể Lua gọi), nên mặc định là 0.
  CarEngine_00: `m_powerChainFactorCurve` = (50°→1.0, 90°→0.05); `m_extraPowerChainFactor = 0.5`.
  Nghĩa là drift góc ≤50° vẫn đủ công suất, gần 90° thì gần như mất lực kéo.

c) Mô-men gốc `m_maxTorque` (`fBasicDynamic`) là chỉ số per-kart, không có số. Native biến torque thành gia tốc. Không settled.

## 3. Lái (steering) — MỘT PHẦN

- `m_drivingMaxAngSpeedCurve`: x = `kmph / m_vehicleTopSpeed` (tỉ lệ tốc độ 0..1), y = tốc độ quay yaw tối đa, đơn vị độ/s.
  Bằng chứng: `DriftState_Normal.c:1893` `fVar27 = mKmph / mCoreTopSpeed`, evaluate curve
  `NssVehicleDriftData.GetMaxSteeringAngSpeedCurve()`, rồi `* 0.017453292` (độ→rad) ở `:1926` và đẩy vào drift helper.
  `mCoreTopSpeed = m_coreData.m_vehicleTopSpeed` (`DriftState_Normal.c:1518`, `NssVehicleDriftCtrl.c:1685`).
  Thân `GetMaxSteeringAngSpeedCurve`/`GetDrivingMaxAngVelCurve` hỏng; việc nó trả về `m_drivingMaxAngSpeedCurve` là suy luận.
  Lúc lái thường, `NssVehicleDriftCtrl.NormalState` (`:1255`) lấy `GetNormalDrivingMaxAngVel(carParams, steering)` rồi đặt làm yaw tối đa
  cho helper (thân hàm hỏng, chỉ còn chữ ký: tham số thứ hai là `steering`).
  CarSteer_00: `m_drivingMaxAngSpeedCurve` = (0.24→82, 0.70→58, 0.95→40) độ/s.
- Góc bánh lái native: `NssVehicleBasePhyParamHelper.ToSteeringParams` đặt `m_maxSteeringAngle = 0.61086524` rad (35°)
  (`NssVehicleBasePhyParamHelper.c:197`) và `m_maxSpeedFullSteeringAngle = m_vehicleTopSpeed * 0.2777778` (`:207`).
  Việc native giảm góc lái theo tốc độ bằng hai số này là suy luận.
- `m_maxSteerAngleCurve` = (0→32, 300→20): không thấy chỗ dùng trong C# decompile được. Không settled; x có thể là km/h (suy luận).
- Tốc độ yaw được tạo trong native (`PHY_VehicleSolveSteering`): không settled. Các tham số khác: `m_steeringVelDampStartTime=0.2`,
  `m_steeringVelDampTime=0.7`, `m_steeringVelRaiseTime=1` (thời gian tăng/giảm dần input lái, suy luận từ tên).

## 4. Drift — MỘT PHẦN

- Vào drift: `NssVehicleDriftCtrl.ToggleDrifting` (`:2403`) chuyển máy trạng thái sang `NormalDrift`. Điều kiện phím
  nằm trong `NssPhyVehicleCab.BeginDrifting(NssDriftDir)` (hỏng). Ngưỡng tốc độ là chỉ số per-kart
  `m_driftMinKMPH` (`fDriftMinKMPH`), không có số. `m_BrakeTimeEndGeneralDriftingMinMPH = 45` là ngưỡng kết thúc
  drift khi phanh làm tụt tốc, không phải ngưỡng vào (suy luận theo tên).
- Thoát drift: `UpdateDrifting` (`NssVehicleDriftCtrl.c:2583`): khi `NssDriftDir == None` (nhả phím/hết hướng) thì kết thúc. Sau va chạm, drift bị
  ép dừng khi `mForceEndDriftDelay > m_driftCollisionEndDelay` (0.1 s) (`:2567`).
- Máy trạng thái (`DriftStateMachine`, `DriftState_Normal.Update` `:1690`):
  - `NormalDrift`: mỗi frame thử `SwitchStateToSidewaysDrift()` trước. Nếu chưa `mIsDriftSliding` thì chạy
    `DoDrifting` + `VelLerpToVehicleHead` + `SetMaxYawVel` (`:2029-2031`). Nếu `mIsDriftSliding` (đã nhả drift) thì
    `mNextStateType = Sliding` (`:2093`).
  - `SidewaysDrift` (drift ngang ngắn): vào khi VdAngle trong [`EnterSidewaysDriftingMinVDAngle`=0, `EnterSidewaysDriftingMaxVDAngle`=40],
    giữ tối thiểu `SidewaysDriftingMinKeepTime`=0.37 s, tối đa `SidewaysDriftingMaxKeepTime`=1.5 s; vận tốc phụ
    `SidewaysDriftingExVelCurve` (50→45, 250→75), lerp coef 300. Hằng số lấy từ tên field, logic ở `SwitchStateToSidewaysDrift` (`DriftState_Normal.c:1`).
  - Swing-tail: `EnterSwingTailDriftingMaxVDAngle=18`, `SwingTailDriftingMaxKeepTime=0.7`.
  - `Sliding` (trượt ra sau khi nhả drift): `DriftState_Sliding.ProcessVelLerp(float deltaTime, float absVdAngle)` →
    `m_slidingVelocityDirLerpCurve` có x = |VdAngle| (độ). Chữ ký chứng minh x; thân hàm hỏng. y được dùng làm hệ số lerp
    hướng vận tốc về mũi xe nhân `deltaTime` (suy luận). Vào lại drift liên tục cần `m_SlidingStateEnterContinuousDriftMinVdAngle=50`.
- Lực đẩy tới khi drift: `VelLerpToVehicleHead` (`DriftState_Normal.c:2098`) evaluate `m_driftForwardExtraForceCurve`.
  Biến thể: `m_SameDirContinuous[Medium]DriftForwardExtraForceCurve`, `m_BrakeReverseDriftForwardExtraForceCurve`.
  Trục x là output của một hàm helper nhận `mSteering` (tên slot không chắc, không settled).
- Mất tốc khi drift: gồm (a) công suất bị cắt theo `m_powerChainFactorCurve(|VdAngle|)` (mục 2b) và (b) native trượt ngang
  (`DriftingSlideVelDamping`, `CarCollision_00.m_sideVelocityDeceleration` = (0→45, 60→45, 70→200), x là góc? (suy luận)).
  Không settled ở mức công thức.
- CarDrift_00: `m_slidingVelocityDirLerpCurve` = (46→3.5, 55→0.85, 90→0.85, 100→2, 105→2.2);
  `m_smallSlidingVelocityDirLerpCurve` = (40→1.1, 45→0.82, 90→0.82, 100→2, 105→2.2);
  `m_driftForwardExtraForceCurve` = (-30→0.8, 0→0.1, 45→0.1, 86→1, 100→2, 105→2.2); `m_smallDriftOutSlidingTime=0.4`;
  `m_driftEndParamsLerpVDThreshold=50`; `m_bigOrOtherDriftVDAngleCoef=65`; `m_MediumOrSmallDriftVDAngleCoef=50`.

## 5. Nitro — ĐÃ RÕ cách nạp, thiếu số per-kart

- Thanh nitro đầy = 1000 đơn vị (`SingleNitrogenCollect.AddExtraPower`, `SingleNitrogenCollect.c:2095` `uVar5 - 1000`,
  `mCurShowPower = 999` khi đầy `:2117`). Đầy thì `EndCollect` và `GenOneNitrogenProp()` tạo 1 bình nitro nếu
  `GetRemainSlotCount() > 0`. Số ô do `mScenePropsMgr` (ô đạo cụ) quyết định; con số không settled (bản remake dùng 2).
- Chỉ nạp khi `mCurVehicleState == 2` (Drifting) (`:3844`). Lượng nạp mỗi frame (`CalcCollectPower`, `:1646`):
  ```
  speedDiff = lastSpeed - curSpeed                         (:1391)   // tốc độ MẤT trong frame
  p = speedDiff>0 ? speedDiff * (mSpeedDiffAccumFactor + ExtraSpeedDiffAccumFactor) : 0     (:1437)
  p *= m120/m165VelDiffCollectCompensateCoef nếu chạy 120/165 fps
  if p < compensate*TARGET_COMP_COEF_SCALE_FPS_30*dt:  p = compensate * curve(...) * scale * dt   // sàn tối thiểu khi drift
  p = min(p, 100) * mFailAccumValueScaleCoef * (1 + mExtraAccumValueScaleCoef)                (:1869)
  ```
  Tức là nitro nạp theo TỐC ĐỘ BỊ MẤT do drift, cộng một mức sàn khi vẫn đang drift. Không nạp theo thời gian.
  `mSpeedDiffAccumFactor` tương ứng `m_powerAccumSpeedFactor` (`fGatherNitroSpeed`, per-kart, suy luận).
  `compensate` tương ứng `m_compensatePowerFactor` (`fCompensatePowerFactor`). Curve sàn nhiều khả năng là
  `CarTurbo_00.m_compensatePowerFactorCurve` (0→0.95, 70→1.25), x = VdAngle (suy luận).
- Bình nitro đầy (`EvtN2O.OnRefreshVehicleParams`): `ExtraKMPH = m_boostExtraMaxKMPH` (`EvtN2O.c:334`),
  `ExtraTorque = m_N2OBoostTorque` (`:348`), `Duration = m_N2OBoostDuration + mExParamDuration` (`:415`). Cả ba là
  chỉ số per-kart (`fNitroExtraSpeed`, `fNitroDynamic`, `fNitroDuration`), không có số. Cơ chế: cộng vào `top` (mục 1) và torque.
- Mini boost: thời lượng/tốc/torque = `m_miniBoostDuration`, `m_miniBoostExtraMaxKMPH`, `m_MiniBoostTorque` (per-kart, `fSmallNitro*`).
  Cửa sổ kích hoạt: `m_miniBoostDelay = 1.4` s; vùng góc `m_miniBoostMinVDAngle=35`..`m_miniBoostMaxVDAngle=50`. Hai góc này
  chỉ thấy dùng ở UI chỉ báo drift (`RacingDriftIndicatorStatus`). Logic kích hoạt nằm trong hàm ECU hỏng
  (`GetHasTriggerMiniBoostDriftDir`). Không settled.
- Dual boost: `mDualBoostDelayCheckTime = 0.2`, `m_ContinuousMiniBoostLimitTime = 0.5`, `m_LastDriftKeyAndFirstMiniBoostTime = 1`.
  Không đọc được logic.

## 6. Va tường — MỘT PHẦN

- `NssPhyVehicleESP._WallCollisionProcess` (`NssPhyVehicleESP.c:2452`). Bounce, giảm vận tốc tiếp tuyến và quay xe do
  native tính, dùng các curve được nạp sẵn qua `ePHY_VEHICLE_CURVE_ID` (`FENCE_COLL_BUMPMPS` = `m_fenceCollisionBounceKMPH`,
  `FENCE_COLL_DUMPTANGVEL[_FRONT|_DRIFTOPP]` = `m_[front]fenceCollisionDumpTangVelCoef`...; `NssPhyVehicleCtrl.GetVehicleCurve`).
- Phần C# thấy được:
  - section đuôi (2): thời gian bị bật lại `m_collideWallBouncedTime(v_vào_tường * 3.6)` (x = km/h) (`:2650-2652`).
  - section 1: đưa `m_frontFenceCollisionPowerDumpCurve` vào power chain (`:2732`). Theo `_UpdatePowerChainFactor`, công suất
    = curve(t giây từ lúc va) trong suốt thời lượng dập (mục 2b).
- Suy luận công thức giữ lại vận tốc: `v_tangent *= m_fenceCollisionDumpTangVelCoef(km/h)` (va hông) hoặc
  `m_frontFenceCollisionDumpTangVelCoef(km/h)` (va đầu). Thành phần pháp tuyến bật ra `m_fenceCollisionBounceKMPH(km/h)`.
- CarCollision_00: `m_fenceCollisionDumpTangVelCoef` = (0→0.98, 80→0.75); `m_frontFenceCollisionDumpTangVelCoef` = (0→0.5, 80→0.3);
  `m_fenceCollisionBounceKMPH` = (0→0, 60→22, 150→70); `m_frontFenceCollisionPowerDumpCurve` = (0→0.4, 0.6→1.0);
  `m_collideWallBouncedTime` = (0→0.05, 100→0.2); góc phân loại `m_fenceCollTailAngle=85`, `m_fenceCollSideAngle=105`,
  `m_fenceCollHeadSideAngle=150`; đâm tường liên tiếp: `m_multiWallCollideNum=3` lần trong `0.5` s, khi tốc ≥ `50` km/h.
  Va xe khác khi drift mất `m_DriftCollCarLossKmph = 20` km/h.

## 7. Catch-up (FallBehindCompe) — KHÔNG settled phần công thức

- `FallBehindCompeMgr` (không có trong phần đã decompile; `CalcFallBehindCompe` @0x03671244). Từ kiểu dữ liệu:
  hằng `MAX_COMPE_KMPH = 40`, `MAX_COMPE_DYNAMIC = 20`; field `FallBehindDis`, `mMapSingleRoundLen`, `mMapTotalLen`,
  `CurCompeKMPH`, `CurCompeTorque`; curve `mFallBehindCompeTopVelCurve` / `mFallBehindCompeDynamicCurve` (từ CarEngine)
  và `mFallBehindCompeCoefCurve` (từ `Config/PlayVehicleModuleConfig.json`: 0→1.0, 0.15→1.3). Kết quả cộng qua
  `IVhcActionAddExtraVelParam` (extra km/h + torque).
- Suy luận: x của `FallBehindCompeTopVelCurve` = `FallBehindDis / mMapSingleRoundLen` (số vòng bị bỏ lại so với xe đầu);
  y = km/h cộng thêm, kẹp ≤ 40. CarEngine_00: TopVel = (0→0, 0.01→3, 0.12→17, 0.24→19).
  Chỉ áp khi `CanFallBehindCompe(firstActor)` (luật chế độ chơi, không đọc được).

## Việc cần làm cho remake (theo thứ tự lợi ích)

1. Đổi gia tốc: dùng `m_basePowerChainFactor(speed/top)` (1.0→0.2) cho mô-men lúc lái thường. Chỉ nhân
   `m_powerChainFactorCurve(|DriftAngle|)` khi đang drift. Hiện `Kart.cs` đang dùng nhầm curve drift làm curve theo % tốc độ.
2. Nitro nạp theo tốc độ mất khi drift (`speedDiff * factor`, sàn tối thiểu, ≤100/frame, đầy 1000) thay cho `NitroChargePerSecond`.
3. Yaw: `m_drivingMaxAngSpeedCurve(kmh / TopKmh)` độ/s (khớp code hiện tại). Hướng vận tốc khi Sliding lerp theo
   `m_slidingVelocityDirLerpCurve(|VdAngle|)` (khớp code hiện tại).
4. Wall: thay `WallSpeedLoss` bằng `m_fenceCollisionDumpTangVelCoef(kmh)` / bản front, và dập công suất theo
   `m_frontFenceCollisionPowerDumpCurve(t)`.
5. Số per-kart (TopKmh, torque, nitro +km/h, thời lượng) vẫn là ước lượng: không có trong client.
