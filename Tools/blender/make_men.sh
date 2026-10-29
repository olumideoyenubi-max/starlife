#!/bin/zsh
# Generates the men and passers-by (make_men.py), cuts each into pieces (wardrobe_export.py) and saves its skeleton
# (rig_export.py). Usage: Tools/blender/make_men.sh [id ...]   (no ids: everyone in make_men.py)
cd "$(dirname "$0")/../.."; P=$PWD; B=/Applications/Blender.app/Contents/MacOS/Blender
$B -b -P Tools/blender/make_men.py -- "$P" "$@" 2>&1 | grep -E '^MAN|FAIL|Error|Traceback'
ids=("$@"); [ ${#ids} -eq 0 ] && ids=($(grep -oE "^    '[a-z_]+':" Tools/blender/make_men.py | tr -d "': "))
for id in $ids; do
  [ -f Art3D/avatars/$id.blend ] || continue
  $B -b Art3D/avatars/$id.blend -P Tools/blender/wardrobe_export.py -- $id "$P/Assets/Starlets" --nosculpt 2>&1 | grep -E '^WARDROBE|Error|Traceback|KEPT'
  $B -b Art3D/avatars/$id.blend -P Tools/blender/rig_export.py -- $id "$P/Assets/Resources/Rigs" 2>&1 | grep -E '^RIG|Error|Traceback'
done
