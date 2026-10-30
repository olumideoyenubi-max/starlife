# Repairs to finished hairstyles, run on their saved scenes (Art3D/hair/<id>.blend) and re-exported with hair_fit.export:
#   cap   the scalp layer under the hair, rebuilt: hair_fit.cap() used to leave a square notch of bare scalp at the
#         front of the hairline (it showed through afros as a bald patch)
#   drop  a ponytail that sticks straight out behind the head is swung down to hang against the back, and gathered
#   Blender -b Art3D/hair/<id>.blend -P Tools/blender/fix_hair.py -- <project> <id> <label> <max faces> cap|drop ...
import bpy, bmesh, os, sys, math
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index('--') + 1:]
PROJECT, sid, label, faces = args[0], args[1], args[2], int(args[3]); todo = set(args[4:])
sys.path.insert(0, os.path.join(PROJECT, 'Tools', 'blender'))
import hair_fit

head = bpy.data.objects['REF_head']; hw = head.data.copy(); hw.transform(head.matrix_world)
hc = sum((v.co for v in hw.vertices), Vector()) / len(hw.vertices)

if 'cap' in todo and 'CAP' in bpy.data.objects:      # rebuilt with hair_fit.cap's (now notch-free) rule, in the same colour
    old = bpy.data.objects['CAP'].data.materials[0] if bpy.data.objects['CAP'].data.materials else None
    col = tuple(next((n for n in old.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')).inputs['Base Color'].default_value) if old and old.use_nodes else (0.035, 0.028, 0.025, 1)
    hair_fit.cap(color=col)

if 'drop' in todo:
    h = bpy.data.objects['HAIR']; mw = h.matrix_world; inv = mw.inverted()
    back = max(v.co.y for v in hw.vertices)                       # the back of the head
    ws = [mw @ v.co for v in h.data.vertices]
    tail = [w for w in ws if w.y > back + 0.015]
    if tail:
        tie = Vector((0, back, sum(w.z for w in tail if w.y < back + 0.05) / max(1, len([w for w in tail if w.y < back + 0.05]))))
        print('DROP tail verts', len(tail), 'tie', tuple(round(x, 3) for x in tie))
        for v, w in zip(h.data.vertices, ws):
            d = w.y - back
            if d <= 0: continue
            k = min(1.0, d / 0.05); k = k * k * (3 - 2 * k)          # bends over its first 5 cm
            p = w - tie
            g = 1 - 0.5 * min(1.0, d / 0.15)                         # gathered as it falls (the fan spreads sideways and up and down)
            p.x *= g; p.z *= g
            p = Matrix.Rotation(math.radians(-82) * k, 4, 'X') @ p   # +y (straight out behind) swings to -z (hanging down)
            p.y = max(p.y, 0.004 * k)                                # and never into the neck
            v.co = inv @ (tie + p)
        h.data.update()

hair_fit.export(sid, os.path.join(PROJECT, 'Assets', 'Hair'), os.path.join(PROJECT, 'Art3D', 'hair'), label=label, max_faces=faces)
print('FIXED', sid)
