# Splits a posed avatar (Art3D/avatars/<id>.blend, saved by avatar_export.py) into swappable pieces for Unity:
#   blender -b Art3D/avatars/<id>.blend -P Tools/blender/wardrobe_export.py -- <id> <project>/Assets/Starlets
# Pieces: head, hair, eyes, teeth, extra (glasses and the like), body, feet (the skin inside the shoes), top, bottom,
# shoes; a one-piece avatar gives head and suit instead. Every avatar is scaled and moved so its neck joint sits at the
# same point, which is what lets one starlet wear another's clothes. Writes <id>/starlet.fbx, textures and manifest.json.
import bpy, bmesh, sys, os, json, re
from mathutils import Matrix, Vector

sid, assets = sys.argv[sys.argv.index('--') + 1:][:2]
SCULPT = '--nosculpt' not in sys.argv      # the generated men (make_men.py) already have detailed faces
out = os.path.join(assets, sid); os.makedirs(out, exist_ok=True)
NECK = Vector((0.0, 0.0, 1.43))

arm = max((o for o in bpy.data.objects if o.type == 'ARMATURE'), key=lambda a: len(a.data.bones))
def bone(key):
    pb = next((b for b in arm.pose.bones if re.sub(r'^mixamorig:?', '', b.name).startswith(key)), None)
    return (arm.matrix_world @ pb.head) if pb else None
neck = bone('Neck'); k = NECK.z / neck.z
fix = Matrix.Translation(NECK) @ Matrix.Scale(k, 4) @ Matrix.Translation(-neck)
def mark(key):
    p = bone(key); return [round(v, 4) for v in (fix @ p)] if p is not None else None

marks = {key: mark(b) for key, b in (('hips', 'Hips'), ('head', 'Head'), ('headTop', 'HeadTop'), ('eyeL', 'LeftEye'), ('eyeR', 'RightEye'), ('handL', 'LeftHand'), ('handR', 'RightHand'), ('armL', 'LeftArm'), ('footL', 'LeftFoot'))}

def kind(mat):
    n = (mat or '').lower()
    for key, part in (('sl_eyebrow', 'brows'), ('sl_eyelash', 'lashes'), ('sl_beard', 'beard'), ('outfit_top', 'top'), ('outfit_bottom', 'bottom'), ('outfit_footwear', 'shoes'), ('hair', 'hair'), ('eye', 'eyes'), ('teeth', 'teeth'),
                      ('skin', 'head'), ('head', 'head'), ('body', 'body'), ('facewear', 'extra'), ('glasses', 'extra'), ('headwear', 'extra'), ('avatar', 'suit')):
        if key in n: return part
    return 'extra'

def weights(o, keys):
    """Per vertex, how much of its weight goes to bones whose name starts with one of the keys."""
    idx = {g.index for g in o.vertex_groups if any(re.sub(r'^mixamorig:?', '', g.name).startswith(key) for key in keys)}
    w = []
    for v in o.data.vertices:
        tot = sum(g.weight for g in v.groups); w.append(sum(g.weight for g in v.groups if g.group in idx) / tot if tot > 0 else 0)
    return w

def split(obj, w, name_in, name_out):
    """Faces whose vertices mostly carry weight w go to one object, the rest to another."""
    res = []
    for keep_in, name in ((True, name_in), (False, name_out)):
        c = obj.copy(); c.data = obj.data.copy(); bpy.context.scene.collection.objects.link(c)
        bm = bmesh.new(); bm.from_mesh(c.data); bm.faces.ensure_lookup_table()
        drop = [f for f in bm.faces if (sum(w[v.index] for v in f.verts) / len(f.verts) > 0.5) != keep_in]
        bmesh.ops.delete(bm, geom=drop, context='FACES'); bm.to_mesh(c.data); bm.free()
        if len(c.data.polygons) > 0: res.append((name, c))
        else: bpy.data.objects.remove(c, do_unlink=True)
    bpy.data.objects.remove(obj, do_unlink=True); return res

def lift_hair(obj):
    """A one-piece avatar's head still has its hair (and any hat) attached. They are separate shells of the mesh, so
    take every sizeable shell that is not the one carrying the nose out as 'hair'. Returns (head, hair or None)."""
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    shell = {}; n = 0
    for v in bm.verts:
        if v.index in shell: continue
        todo = [v]; shell[v.index] = n
        while todo:
            a = todo.pop()
            for e in a.link_edges:
                o = e.other_vert(a)
                if o.index not in shell: shell[o.index] = n; todo.append(o)
        n += 1
    size = [0] * n; lo = [Vector((9, 9, 9)) for _ in range(n)]; hi = [Vector((-9, -9, -9)) for _ in range(n)]
    for v in bm.verts:
        i = shell[v.index]; size[i] += 1
        for a in range(3): lo[i][a] = min(lo[i][a], v.co[a]); hi[i][a] = max(hi[i][a], v.co[a])
    skin = max(range(n), key=lambda i: size[i])            # the head itself is the biggest shell
    def on_face(i):                                        # eyes, lashes, brows and teeth are small shells on the front of the face
        c = (lo[i] + hi[i]) / 2
        return abs(c.x) < 0.065 and c.y < -0.055 and 1.47 < c.z < 1.635 and (hi[i] - lo[i]).length < 0.09
    hairy = {i for i in range(n) if i != skin and not on_face(i)}
    bm.free()
    if not hairy: return obj, None
    res = []
    for want_hair in (False, True):
        c = obj.copy(); c.data = obj.data.copy(); bpy.context.scene.collection.objects.link(c)
        b2 = bmesh.new(); b2.from_mesh(c.data)
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if (shell[f.verts[0].index] in hairy) != want_hair], context='FACES'); b2.to_mesh(c.data); b2.free()
        res.append(c)
    bpy.data.objects.remove(obj, do_unlink=True)
    print('LIFTED HAIR', sid, 'shells', n, 'sizes', sorted(size, reverse=True)[:8], 'hair polys', len(res[1].data.polygons), 'head polys', len(res[0].data.polygons))
    return res[0], res[1]

dg = bpy.context.evaluated_depsgraph_get(); pieces = {}
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.data.materials and len(o.data.polygons) > 0 and (any(m.type == 'ARMATURE' for m in o.modifiers) or o.parent_type == 'BONE')]:
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(o.matrix_world); me.transform(fix)
    b = bpy.data.objects.new('bake', me); bpy.context.scene.collection.objects.link(b)
    part = kind(o.data.materials[0].name if o.data.materials[0] else '')
    same = len(o.data.vertices) == len(me.vertices)
    if part == 'suit' and same:
        got = []
        for name, obj in split(b, weights(o, ('Head', 'Neck', 'LeftEye', 'RightEye')), 'head', 'suit'):
            if name == 'head':
                h, hair = lift_hair(obj); got.append(('head', h))
                if hair is not None: got.append(('hair', hair))
            else: got.append((name, obj))
    elif part == 'body' and same: got = split(b, weights(o, ('LeftFoot', 'RightFoot', 'LeftToe', 'RightToe')), 'feet', 'body')
    else: got = [(part, b)]
    for name, obj in got: pieces.setdefault(name, []).append(obj)

def image(mat, socket):
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
    if not bsdf or not bsdf.inputs[socket].is_linked: return None
    todo = [bsdf.inputs[socket].links[0].from_node]; seen = set()
    while todo:
        n = todo.pop(0)
        if n in seen: continue
        seen.add(n)
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo += [i.links[0].from_node for i in n.inputs if i.is_linked]
    return None
saved = {}
def save(img, name):
    if img is None: return ''
    if img.name in saved: return saved[img.name]
    path = os.path.join(out, name + '.png'); img.file_format = 'PNG'
    try: img.save(filepath=path)
    except Exception: img.filepath_raw = path; img.save()
    saved[img.name] = os.path.basename(path); return saved[img.name]
def tone(img):
    """Average colour and brightness of the lit part of a texture (the skin, for a body texture)."""
    if img is None or img.size[0] == 0: return [1, 1, 1], 1
    import numpy as np
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, img.channels)[::7]
    lum = px[:, 0] * 0.299 + px[:, 1] * 0.587 + px[:, 2] * 0.114; lit = px[lum > 0.12]
    if len(lit) == 0: return [1, 1, 1], 1
    return [round(float(v), 4) for v in lit[:, :3].mean(axis=0)], round(float(lum[lum > 0.12].mean()), 4)

parts = []
for f in [f for f in os.listdir(out) if f.endswith('.png') or f.endswith('.fbx')]: os.remove(os.path.join(out, f))
for name in sorted(pieces):
    objs = pieces[name]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = name
    while len(o.data.materials) > 1: o.data.materials.pop(index=len(o.data.materials) - 1)   # joined eyes share one material
    if name in ('head', 'body', 'feet'):       # skin: all quads where the triangles pair up, then one level of subdivision, so light curves over it smoothly
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.tris_convert_to_quads(uvs=True, seam=True); bpy.ops.object.mode_set(mode='OBJECT')
        m = o.modifiers.new('smooth', 'SUBSURF'); m.levels = 1; m.boundary_smooth = 'PRESERVE_CORNERS'; bpy.ops.object.modifier_apply(modifier='smooth')
    if name == 'head' and SCULPT:               # sculpt her face and bake the sculpt onto the game head (face_sculpt.py)
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import face_sculpt
        try: face_sculpt.run(o, marks, sid, os.path.normpath(os.path.join(assets, '..', '..', 'Art3D')))
        except Exception as e: import traceback; traceback.print_exc(); print('SCULPT FAILED', sid, e)
    for p in o.data.polygons: p.use_smooth = True; p.material_index = 0
    mat = o.data.materials[0]; base, nrm = image(mat, 'Base Color'), image(mat, 'Normal')
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
    rgb, lum = tone(base); zs = [v.co.z for v in o.data.vertices]; xs = [v.co.x for v in o.data.vertices]
    tname = 'body' if name == 'feet' else name
    parts.append({'mesh': name, 'material': mat.name if mat else '', 'tex': save(base, tname), 'normal': save(nrm, tname + '_n'),
                  'alpha': bool(bsdf and bsdf.inputs['Alpha'].is_linked), 'color': [round(c, 4) for c in (bsdf.inputs['Base Color'].default_value if bsdf else (1, 1, 1, 1))],
                  'rough': round(bsdf.inputs['Roughness'].default_value, 3) if bsdf else 0.6, 'metal': 0.0, 'tone': rgb, 'lum': lum,
                  'zmin': round(min(zs), 3), 'zmax': round(max(zs), 3), 'xmax': round(max(abs(x) for x in xs), 3), 'tris': sum(len(p.vertices) - 2 for p in o.data.polygons)})
keep = {p['mesh'] for p in parts}
for o in list(bpy.data.objects):
    if o.name not in keep: bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
# Clothes get real thickness: the thin pieces are kept (pieces_thin.fbx, which the underwear and accessory scripts cut from),
# then every garment is made all quads where it can be, subdivided once and given an outer layer a few millimetres out,
# so hems, cuffs and collars have an edge and the cloth stands off the body instead of looking painted on.
import shutil
shutil.copyfile(os.path.join(out, 'starlet.fbx'), os.path.join(out, 'pieces_thin.fbx'))
for o in bpy.data.objects:
    if o.name not in ('top', 'bottom', 'suit'): continue
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    thin = o.data.copy(); before = [d for d in o.dimensions]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.tris_convert_to_quads(uvs=True, seam=True); bpy.ops.object.mode_set(mode='OBJECT')
    m = o.modifiers.new('smooth', 'SUBSURF'); m.levels = 1; m.boundary_smooth = 'PRESERVE_CORNERS'; bpy.ops.object.modifier_apply(modifier='smooth')
    m = o.modifiers.new('thick', 'SOLIDIFY'); m.thickness = 0.0035; m.offset = 1.0; m.use_even_offset = False; m.thickness_clamp = 1.0; m.use_rim = True; bpy.ops.object.modifier_apply(modifier='thick')
    bpy.context.view_layer.update()
    if any(abs(a - b) > 0.025 for a, b in zip(o.dimensions, before)):      # an outfit with sharp ornaments (wings, armour) can spike when thickened: leave it as it came
        o.data = thin; print('KEPT THIN', sid, o.name)
    for p in o.data.polygons: p.use_smooth = True
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
json.dump({'id': sid, 'scale': round(k, 4), 'marks': marks, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
print('WARDROBE', sid, 'k %.3f' % k, 'hips', marks['hips'], 'armL', marks['armL'], 'handL', marks['handL'], '|', ' '.join('%s[%.2f-%.2f %d]' % (p['mesh'], p['zmin'], p['zmax'], p['tris']) for p in parts))
