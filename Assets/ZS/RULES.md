# Luật chơi: lấy từ đâu

Mỗi luật trong `Assets/ZS/Scripts` thuộc một trong ba loại:

- **Dữ liệu**: số đọc thẳng từ asset của game (JSON do unity-decode-kit xuất từ export AssetRipper).
- **Code gốc**: cách dùng số đó, đọc từ thân method đã dịch ngược (`udk method`, dump `out/zs/code/decomp`).
- **Dựng lại**: phần game giao cho server (bot, chỉ số từng xe, thời gian nitro...) hoặc code chưa đọc được;
  ghi rõ căn cứ.

| Luật | Loại | Nguồn |
|---|---|---|
| Đường đua: 513 điểm, độ rộng trái/phải, vạch xuất phát, 163 điểm hồi sinh | Dữ liệu | `CheckPointConfig` (e_11citynew) |
| Trọng lực 63,75 | Dữ liệu | `CheckPointConfig.CheckPointDataList[].Gravity` |
| 2 vòng | Dữ liệu | `2_StandardData` (xe mẫu qua vạch 3 lần; cách bản web suy ra) |
| Va chạm đường | Dữ liệu | Havok gốc `E_11CityNew_Pick_Track` (3182 tam giác mặt đường) và `Pick_Wall` (1950 tam giác tường), kit giải bằng `udk.havok`; mesh hình chỉ là dự phòng |
| Drift: vào, giữ, trượt, thoát; yaw tối đa; mini boost; catch-up | Code gốc | `RULES_from_code.md` (decomp C# + `libNssPhysicsPlugin` native) |
| Bot: ga, giới hạn tốc, tránh va xe, drift, boost, tự đặt lại vị trí | Dữ liệu (cấu trúc) + dựng lại (cách lái) | cây behaviac `SpeedModeAI`, `AiCar_DriftingState` (kit giải `.bson`); bot của bản này bám đường đua và theo cùng các nhánh đó |
| Tốc độ góc lái tối đa theo tốc độ | Dữ liệu | `CarSteer_00.m_drivingMaxAngSpeedCurve` (x = tốc độ / tốc độ tối đa) |
| Lực kéo giảm khi gần tốc độ tối đa | Code gốc | `CarEngine_00.m_basePowerChainFactor` (1,0 → 0,2), x = tốc độ / tốc độ tối đa: `NssPhyVehiclePowerChain.c:2026` (`RULES_from_code.md` mục 2a) |
| Drift góc lớn mất lực kéo | Code gốc | `CarEngine_00.m_powerChainFactorCurve` (50° → 1, 90° → 0,05), x = \|góc drift\|, chỉ khi đang drift: `_UpdatePowerChainFactor` (mục 2b) |
| Nitro nạp theo tốc độ mất khi drift, đầy 1000, tối đa 100/frame | Code gốc | `SingleNitrogenCollect.CalcCollectPower` (mục 5); hệ số mỗi xe là dựng lại |
| Va tường: giữ tốc theo km/h, va đầu giữ ít hơn, bật ra, dập công suất | Code gốc + dữ liệu | `CarCollision_00` (`m_fenceCollisionDumpTangVelCoef`, bản front, `m_fenceCollisionBounceKMPH`, `m_frontFenceCollisionPowerDumpCurve`); công thức cuối nằm trong `libNssPhysicsPlugin.so` nên là suy luận (mục 6) |
| Bù tốc cho xe tụt lại | Dữ liệu | `CarEngine_00.FallBehindCompeTopVelCurve` (km/h theo khoảng cách) |
| Ngưỡng drift, cửa sổ tăng tốc nhỏ sau drift | Dữ liệu | `CarDrift_00.m_BrakeTimeEndGeneralDriftingMinMPH`, `CarTurbo_00.m_miniBoostDelay`, `m_miniBoostMinVDAngle` |
| Tốc độ tối đa 196 km/h, mô-men, +km/h và thời gian nitro, hệ số nạp nitro | Dựng lại | Chỉ số từng xe (`VehicleBaseParamConfig`: `fMaxSpeed`, `fBasicDynamic`, `fNitro*`, `fGatherNitroSpeed`) do server gửi, không có trong client; `CarDrift_00.m_driftHighKMPH_Curve` cho khoảng 180–210 km/h |
| Tự tăng tốc, chỉ có phanh | Dựng lại | Bản mobile không có nút ga |

