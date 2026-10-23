#!/bin/sh
# Regenerate the Unity 6 project content from the original Android package with unity-decode-kit.
# Usage: tools/rebuild.sh [path/to/zingspeed.xapk] [--import]   (SKIP_BUILD=1: reuse an existing dump)
# Needs: ~/unity-decode-kit (sh tools/setup.sh done), Unity 6000.3.18f1 under ~/Unity/Hub/Editor.
set -eu
XAPK=${1:-$HOME/zingspeed-ref/dl/zingspeed.xapk}
[ $# -gt 0 ] && shift
KIT=${UDK_KIT:-$HOME/unity-decode-kit}
OUT=${UDK_OUT:-$KIT/out/zs}
HERE=$(cd "$(dirname "$0")/.." && pwd)
VERSION=6000.3.18f1
export PYTHONPATH="$KIT${PYTHONPATH:+:$PYTHONPATH}"

[ "${SKIP_BUILD:-0}" = 1 ] || "$KIT/bin/udk" build "$XAPK" "$OUT"
# What the playable build needs: one track (scene pair + pick configs), lobby and podium stages,
# karts, drivers, car parameters, and the UI prefabs the reconstructed screens use.
"$KIT/.venv/bin/python" -m udk.unity "$OUT/assets/ExportedProject" "$HERE" "$VERSION" \
  --root 'Scenes/Preview/LevelScene/Level_11CityNew(_Art)?\.unity$' \
  --root 'environments/e_11citynew/model/pick/' \
  --root 'Scenes/StateScene/(Lobby_L|Lobby_L_Art|Podium)\.unity$' \
  --root 'vehicles/carparams/' \
  --root 'vehicles/car/(00004|00006|00297|00016|00175|00284)/.*/HNCar_[0-9]+\.prefab$' \
  --root 'id_car/01ncarshow/NCar_(00004|00006|00297|00016|00175|00284)_Splited\.png$' \
  "$@"
# Data the reconstructed gameplay reads (track line, car parameters) as JSON TextAssets.
DATA="$HERE/Assets/ZS/Resources/ZSData"
rm -rf "$DATA"
"$KIT/.venv/bin/python" -m udk.sojson "$OUT/assets/ExportedProject" "$DATA/e_11citynew" 'environments/e_11citynew/model/pick/' --flat
"$KIT/.venv/bin/python" -m udk.sojson "$OUT/assets/ExportedProject" "$DATA/carparams" 'vehicles/carparams/' --flat
# The track's own collision (Havok, decoded by the kit) as OBJ text the editor turns into colliders.
for part in Track Wall; do
  "$KIT/.venv/bin/python" -m udk.havok "$DATA/e_11citynew/E_11CityNew_Pick_$part.asset.json" "$DATA/e_11citynew/E_11CityNew_Pick_$part.obj.txt" >/dev/null
done
# Original sounds the race uses (see Assets/ZS/Scripts/ZsAudio.cs for which event plays when).
"$KIT/.venv/bin/python" -m udk.audiopick "$OUT" "$HERE/Assets/ZS/Resources/ZSAudio" \
  '^Play_Mode_Spray_UI_Countdown$' '^Play_Mode_RongLuTaoTai_SFX_(Nitro|BoostSurge)$' '^Play_Mode_GoldGrab_UI_Win$' \
  '^Play_Mode_Dajiangsai_UI_Loser$' '^Play_UI_SaiJiHeYue_JieSuan$' '^Play_Hall_(Home_enter|PutCar|SEStart)$'
