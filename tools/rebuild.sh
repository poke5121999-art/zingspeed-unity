#!/bin/sh
# Regenerate the Unity 6 project content from the original Android package with unity-decode-kit.
# Usage: tools/rebuild.sh [path/to/zingspeed.xapk] [--import]
# Needs: ~/unity-decode-kit (sh tools/setup.sh done), Unity 6000.3.18f1 under ~/Unity/Hub/Editor.
set -eu
XAPK=${1:-$HOME/zingspeed-ref/dl/zingspeed.xapk}
[ $# -gt 0 ] && shift
KIT=${UDK_KIT:-$HOME/unity-decode-kit}
OUT=${UDK_OUT:-$KIT/out/zs}
HERE=$(cd "$(dirname "$0")/.." && pwd)
VERSION=6000.3.18f1

"$KIT/bin/udk" build "$XAPK" "$OUT"
# What the playable build needs: one track (scene pair + pick configs), lobby and podium stages,
# karts, drivers, car parameters, and the UI prefabs the reconstructed screens use.
"$KIT/.venv/bin/python" -m udk.unity "$OUT/assets/ExportedProject" "$HERE" "$VERSION" \
  --root 'Scenes/Preview/LevelScene/Level_11CityNew(_Art)?\.unity$' \
  --root 'environments/e_11citynew/model/pick/' \
  --root 'Scenes/StateScene/(Lobby_L|Lobby_L_Art|Podium)\.unity$' \
  --root 'vehicles/carparams/' \
  "$@"
