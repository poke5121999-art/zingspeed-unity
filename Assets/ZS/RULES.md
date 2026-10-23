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
| Va chạm đường | Dựng lại | Va chạm gốc là Havok (`Pick_Track`, `Pick_Wall`), client không đọc lại được; dùng mesh hình của scene làm MeshCollider |
| Tốc độ góc lái tối đa theo tốc độ | Dữ liệu | `CarSteer_00.m_drivingMaxAngSpeedCurve` (x = tốc độ / tốc độ tối đa) |
| Lực kéo giảm khi gần tốc độ tối đa | Dữ liệu | `CarEngine_00.m_powerChainFactorCurve` (x = % tốc độ tối đa) |
| Bù tốc cho xe tụt lại | Dữ liệu | `CarEngine_00.FallBehindCompeTopVelCurve` (km/h theo khoảng cách) |
| Ngưỡng drift, cửa sổ tăng tốc nhỏ sau drift | Dữ liệu | `CarDrift_00.m_BrakeTimeEndGeneralDriftingMinMPH`, `CarTurbo_00.m_miniBoostDelay`, `m_miniBoostMinVDAngle` |
| Tốc độ tối đa 196 km/h, gia tốc, thời gian nitro, nạp nitro | Dựng lại | Chỉ số xe do server gửi; `CarDrift_00.m_driftHighKMPH_Curve` cho khoảng 180–210 km/h. Chờ đọc code `NssVehicleDriftCtrl`, `DriftStateMachine` |
| Tự tăng tốc, chỉ có phanh | Dựng lại | Bản mobile không có nút ga |
| Bot | Dựng lại | Bot gốc chạy ở server; bám đường đua với làn riêng |
