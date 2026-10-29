# Saves an avatar's skeleton and skin weights so Starlife Town can make her walk:
#   blender -b Art3D/avatars/<id>.blend -P Tools/blender/rig_export.py -- <id> <project>/Assets/Resources/Rigs
# The pieces in Assets/Starlets are plain meshes (wardrobe_export.py bakes them in her pose). This writes <id>.bytes:
# the main joints where they sit in that same pose and space, and a cloud of points over her own meshes, each with the
# joints it follows. In town every piece takes its weights from the nearest point of the avatar it was cut from.
# Coordinates are Unity's (x = -x, y = z, z = -y of Blender, after the same neck fix as wardrobe_export.py).
import bpy, sys, os, re, struct
from mathutils import Matrix, Vector

sid, out = sys.argv[sys.argv.index('--') + 1:][:2]
os.makedirs(out, exist_ok=True)
NECK = Vector((0.0, 0.0, 1.43))
KEEP = ['Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head',
        'LeftShoulder', 'LeftArm', 'LeftForeArm', 'LeftHand', 'RightShoulder', 'RightArm', 'RightForeArm', 'RightHand',
        'LeftUpLeg', 'LeftLeg', 'LeftFoot', 'LeftToeBase', 'RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase']

arm = max((o for o in bpy.data.objects if o.type == 'ARMATURE'), key=lambda a: len(a.data.bones))
def clean(n): return re.sub(r'_\d+$', '', re.sub(r'^mixamorig:?', '', n))
def pbone(key): return next((b for b in arm.pose.bones if clean(b.name) == key), None)
neck = arm.matrix_world @ pbone('Neck').head; k = NECK.z / neck.z
fix = Matrix.Translation(NECK) @ Matrix.Scale(k, 4) @ Matrix.Translation(-neck)
def U(v): return (-v.x, v.z, -v.y)

# every bone of the rig belongs to the nearest kept joint above it (fingers to the hand, eyes to the head...)
owner = {}
for b in arm.pose.bones:
    p = b
    while p is not None and clean(p.name) not in KEEP: p = p.parent
    owner[b.name] = KEEP.index(clean(p.name)) if p is not None else 0
joints = []
for key in KEEP:
    b = pbone(key); par = b.parent
    while par is not None and clean(par.name) not in KEEP: par = par.parent
    joints.append((key, KEEP.index(clean(par.name)) if par is not None else -1, U(fix @ (arm.matrix_world @ b.head))))

dg = bpy.context.evaluated_depsgraph_get(); cells = {}
VOX = 0.006
for o in [o for o in bpy.data.objects if o.type == 'MESH' and len(o.data.polygons) > 0 and (any(m.type == 'ARMATURE' for m in o.modifiers) or o.parent_type == 'BONE')]:
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(o.matrix_world); me.transform(fix)
    if len(me.vertices) != len(o.data.vertices): print('SKIP (topology changed)', o.name); continue
    gname = {g.index: g.name for g in o.vertex_groups}
    rigid = owner.get(o.parent_bone) if o.parent_type == 'BONE' else None
    for v, src in zip(me.vertices, o.data.vertices):
        acc = {}
        if rigid is not None: acc[rigid] = 1.0
        else:
            for g in src.groups:
                n = gname.get(g.group)
                if n in owner and g.weight > 0: acc[owner[n]] = acc.get(owner[n], 0) + g.weight
        if not acc: continue
        top = sorted(acc.items(), key=lambda t: -t[1])[:4]; s = sum(w for _, w in top)
        key = (round(v.co.x / VOX), round(v.co.y / VOX), round(v.co.z / VOX))
        if key in cells: continue
        cells[key] = (U(v.co), [(j, w / s) for j, w in top])
    bpy.data.meshes.remove(me)

with open(os.path.join(out, sid + '.bytes'), 'wb') as f:
    f.write(struct.pack('<i', len(joints)))
    for name, parent, p in joints:
        nb = name.encode(); f.write(struct.pack('<i', len(nb))); f.write(nb); f.write(struct.pack('<i3f', parent, *p))
    f.write(struct.pack('<i', len(cells)))
    for p, ws in cells.values():
        ws = ws + [(0, 0.0)] * (4 - len(ws))
        f.write(struct.pack('<3f4B4B', *p, *[j for j, _ in ws], *[max(0, min(255, round(w * 255))) for _, w in ws]))
print('RIG', sid, 'joints', len(joints), 'points', len(cells), 'hips', joints[0][2], 'handL', joints[KEEP.index('LeftHand')][2])
