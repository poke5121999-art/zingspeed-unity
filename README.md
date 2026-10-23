# zingspeed-unity

Bài nghiệm thu của [unity-decode-kit](https://github.com/poke5121999-art/unity-decode-kit): giải Zing Speed Mobile
(VNG, Unity 2019.4.41f1, IL2CPP ARM32, Lua, Wwise) thành một project Unity 6 (6000.3.18f1) chơi offline được.

## Repo này chứa gì

- `tools/rebuild.sh`: chạy kit trên file xapk gốc của bạn, rồi dựng nội dung project (scene, prefab, mesh,
  texture, carparams...) và nâng lên Unity 6. Mọi bước giải mã nằm trong kit, không sửa tay ở đây.
- `Assets/ZS/`: phần dựng lại. Game gốc chạy theo server và phần logic nằm trong mã máy IL2CPP, nên luật chơi
  (lái, drift, nitro, bot, vòng đua) được viết lại theo những gì kit đọc ra từ bản build. Mỗi luật ghi nguồn.

Nội dung game (asset gốc) **không** nằm trong repo: export đầy đủ nặng 14 GB và là tài sản của nhà phát hành.
Ai có bản xapk thì chạy `tools/rebuild.sh` để sinh lại.

## Dựng lại

```sh
cd ~/unity-decode-kit && sh tools/setup.sh
cd ~/zingspeed-unity && tools/rebuild.sh ~/zingspeed-ref/dl/zingspeed.xapk --import
```

Dấu hiệu xong: `UpgradeReport.tsv` ở gốc repo liệt kê các thay đổi khi nâng cấp, và lần mở batchmode báo
`compile errors: 0`.

## Trạng thái

Đang làm. Xem `~/handoff/unity-decode-kit-STATUS.md`.
