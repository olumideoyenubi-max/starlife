# Makes the goth accessories from scratch, fitted to the starlets' shared body, and exports them for the game:
#   blender -b -P Tools/blender/make_goth.py -- <project>/Assets/Starlets <project>/Art3D
# Writes Assets/Starlets/goth (a pseudo-avatar holding only these pieces) and Art3D/goth.blend. Leather and fabric
# parts are plain, so the game dyes them to the item's colour; metal parts (material Bead...) keep their silver.
#   choker, collar (+ spikes), cuffs (+ spikes), chain (cross pendant), belt (+ studs), horns, catears, earrings,
#   bag (coffin bag on a strap), fishnets (tights), meshtop
import bpy, bmesh, sys, os, json, math
from mathutils import Vector, Matrix
assets, art = sys.argv[sys.argv.index('--') + 1:][:2]
out = os.path.join(assets, 'goth'); os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
SILVER = (0.78, 0.80, 0.84, 1); BLACK = (1, 1, 1, 1)

def load(avatar, part):
    thin = os.path.join(assets, avatar, 'pieces_thin.fbx')     # garments before they were given thickness
    bpy.ops.import_scene.fbx(filepath=thin if os.path.exists(thin) else os.path.join(assets, avatar, 'starlet.fbx')); got = None
    for o in list(bpy.context.selected_objects):
        if o.name.split('.')[0] == part and got is None: got = o
        else: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT'); got.select_set(True); bpy.context.view_layer.objects.active = got
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); return got

def mat(name, color):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name); m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = color
    return m

def ring(obj, zlo, zhi, side=0):
    """Centre and radii of a body part's cross-section between two heights (side: +1 / -1 for one arm)."""
    pts = [v.co for v in obj.data.vertices if zlo < v.co.z < zhi and (side == 0 and abs(v.co.x) < 0.2 or side != 0 and v.co.x * side > 0.19)]
    xs = [p.x for p in pts]; ys = [p.y for p in pts]
    return Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (zlo + zhi) / 2)), (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2

made = {}      # part name -> objects to join
def keep(part, o, material): o.data.materials.clear(); o.data.materials.append(material); made.setdefault(part, []).append(o); return o

def band(part, c, rx, ry, height, grow=0.004, material=None, n=56):
    """A strap round a cross-section: a short tube a little wider than the body there."""
    bm = bmesh.new(); lo = []; hi = []
    for i in range(n):
        a = i * math.tau / n; x, y = c.x + math.cos(a) * (rx + grow), c.y + math.sin(a) * (ry + grow)
        lo.append(bm.verts.new((x, y, c.z - height / 2))); hi.append(bm.verts.new((x, y, c.z + height / 2)))
    for i in range(n): bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(o)
    return keep(part, o, material or mat('Leather', BLACK))

def spikes(part, c, rx, ry, count, size=0.006, length=0.02, grow=0.006, skip_back=False):
    for i in range(count):
        a = (i + 0.5) * math.tau / count
        if skip_back and math.sin(a) > 0.75: continue
        d = Vector((math.cos(a), math.sin(a), 0)); p = Vector((c.x + d.x * (rx + grow + length / 2), c.y + d.y * (ry + grow + length / 2), c.z))
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=size, radius2=0, depth=length, location=p, rotation=d.to_track_quat('Z', 'Y').to_euler())
        keep(part, bpy.context.object, mat('BeadSilver', SILVER))

def tube(part, points, radius, material, closed=False):
    cu = bpy.data.curves.new(part, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = radius; cu.bevel_resolution = 2; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1); sp.use_cyclic_u = closed
    for i, p in enumerate(points): sp.points[i].co = (p[0], p[1], p[2], 1)
    o = bpy.data.objects.new(part, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.convert(target='MESH')
    return keep(part, bpy.context.object, material)

def box(part, centre, size, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre, rotation=rot); o = bpy.context.object; o.scale = size; return keep(part, o, material)

head = load('olivia', 'head'); body = load('olivia', 'body'); legs = load('ivy', 'bottom'); top = load('lina', 'top'); chestref = load('olivia', 'top')
leather, silver = mat('Leather', BLACK), mat('BeadSilver', SILVER)

# ---- neck: velvet choker with a ring, and a spiked collar
nc, nrx, nry = ring(head, 1.425, 1.455)
band('choker', nc, nrx, nry, 0.02, 0.003)
bpy.ops.mesh.primitive_torus_add(major_radius=0.008, minor_radius=0.0017, location=(nc.x, nc.y - nry - 0.006, nc.z - 0.014), rotation=(math.radians(90), 0, 0)); keep('choker_m', bpy.context.object, silver)
band('collar', nc, nrx, nry, 0.028, 0.004); spikes('collar_m', nc, nrx, nry, 12, 0.0055, 0.02, 0.004, skip_back=True)

# ---- wrists: spiked cuffs
for side in (1, -1):
    wc, wrx, wry = ring(body, 0.985, 1.02, side)
    band('cuffs', wc, wrx, wry, 0.034, 0.003); spikes('cuffs_m', wc, wrx, wry, 8, 0.0045, 0.014, 0.003)

# ---- a fine chain with a cross that rests on the chest
front = min(v.co.y for v in chestref.data.vertices if abs(v.co.x) < 0.03 and 1.31 < v.co.z < 1.36) - 0.006
loop = []
for i in range(48):
    a = i * math.tau / 48; f = (1 - math.cos(a)) / 2      # 0 at the nape, 1 at the pendant
    loop.append((math.sin(a) * (nrx + 0.006) * (1 - 0.35 * f), (nc.y + nry + 0.004) * (1 - f) + front * f, 1.462 * (1 - f ** 1.4) + 1.335 * f ** 1.4))
tube('chain_m', loop, 0.0015, silver, closed=True)
box('chain_m', (0, front - 0.003, 1.312), (0.006, 0.004, 0.036), silver); box('chain_m', (0, front - 0.003, 1.320), (0.022, 0.004, 0.006), silver)

# ---- waist: studded belt with a buckle
bc, brx, bry = ring(legs, 1.0, 1.035)
band('belt', bc, brx, bry, 0.032, 0.004); spikes('belt_m', bc, brx, bry, 22, 0.006, 0.008, 0.004)
box('belt_m', (bc.x, bc.y - bry - 0.008, bc.z), (0.04, 0.006, 0.04), silver)

# ---- head: devil horns and cat ears, each on a band over the crown
arc = [(math.cos(a) * 0.096, -0.035, 1.60 + math.sin(a) * 0.105) for a in [math.radians(8 + i * 164 / 24) for i in range(25)]]
for part in ('horns', 'catears'): tube(part, arc, 0.004, leather)
for side in (1, -1):
    for k in range(6):       # a horn curving up and in, from stacked tapering sections
        t = k / 6; p = Vector((side * (0.062 - 0.022 * t * t), -0.035 + 0.01 * t, 1.692 + 0.075 * t)); r0, r1 = 0.016 * (1 - t), 0.016 * (1 - (k + 1) / 6)
        bpy.ops.mesh.primitive_cone_add(vertices=14, radius1=r0, radius2=max(r1, 0.0005), depth=0.016, location=p, rotation=(math.radians(-10 * t), math.radians(side * -26 * t), 0)); keep('horns', bpy.context.object, leather)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.036, radius2=0.002, depth=0.062, location=(side * 0.062, -0.035, 1.722), rotation=(0, math.radians(side * 14), math.radians(45)))
    o = bpy.context.object; o.scale = (1, 0.42, 1); keep('catears', o, leather)

# ---- ears: small cross earrings
ear = max((v.co for v in head.data.vertices if 1.545 < v.co.z < 1.575), key=lambda c: c.x)
for side in (1, -1):
    x = side * (ear.x + 0.002)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.006, minor_radius=0.0012, location=(x, ear.y, 1.542), rotation=(0, math.radians(90), 0)); keep('earrings_m', bpy.context.object, silver)
    box('earrings_m', (x, ear.y, 1.518), (0.003, 0.004, 0.03), silver); box('earrings_m', (x, ear.y, 1.524), (0.003, 0.016, 0.005), silver)

# ---- a coffin-shaped bag on a strap across the body
shape = [(-0.03, 0.11), (0.03, 0.11), (0.055, 0.05), (0.035, -0.11), (-0.035, -0.11), (-0.055, 0.05)]
bm = bmesh.new(); f = bm.faces.new([bm.verts.new((0, x, z)) for x, z in shape]); r = bmesh.ops.extrude_face_region(bm, geom=[f])
bmesh.ops.translate(bm, verts=[e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)], vec=(0.05, 0, 0)); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new('bag'); bm.to_mesh(me); bm.free(); bag = bpy.data.objects.new('bag', me); bpy.context.scene.collection.objects.link(bag)
bag.location = (0.205, -0.03, 0.93); keep('bag', bag, leather)
box('bag_m', (0.258, -0.03, 0.955), (0.004, 0.008, 0.07), silver); box('bag_m', (0.258, -0.03, 0.975), (0.004, 0.04, 0.008), silver)
strap = []
for i in range(40):          # round the torso, from the far shoulder down to the bag
    a = i * math.tau / 40; u = (1 + math.cos(a)) / 2          # 1 at the bag side, 0 at the shoulder side
    strap.append((-0.105 + 0.33 * u - 0.0, -0.02 + math.sin(a) * (0.118 + 0.02 * u), 1.455 - 0.42 * u))
tube('bag', strap, 0.006, leather, closed=True)

# ---- fishnet tights and a mesh top: the leggings and a fitted top again, in an open net
net = bpy.data.images.new('net', 128, 128, alpha=True); px = []
for y in range(128):
    for x in range(128): on = ((x + y) % 32 < 5) or ((x - y) % 32 < 5); px += [1, 1, 1, 1 if on else 0]
net.pixels = px; net.file_format = 'PNG'; net.filepath_raw = os.path.join(out, 'net.png'); net.save()
for part, src, rep in (('fishnets', legs, 22), ('meshtop', top, 16)):
    c = src.copy(); c.data = src.data.copy(); bpy.context.scene.collection.objects.link(c)
    for uv in c.data.uv_layers[0].data: uv.uv = (uv.uv[0] * rep, uv.uv[1] * rep)
    keep(part, c, mat('Net', BLACK))

# ---- join, export, describe
for o in (head, body, legs, top, chestref): bpy.data.objects.remove(o, do_unlink=True)
parts = []
for part, objs in made.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = part
    for p in o.data.polygons: p.use_smooth = part in ('fishnets', 'meshtop', 'horns', 'chain_m', 'bag')
    m = o.data.materials[0].name; netted = m == 'Net'
    parts.append({'mesh': part, 'material': m, 'tex': 'net.png' if netted else '', 'normal': '', 'alpha': netted, 'color': list(SILVER if m.startswith('Bead') else (0.03, 0.03, 0.035, 1)),
                  'rough': 0.3 if m.startswith('Bead') else 0.55, 'metal': 0.0, 'tone': [1, 1, 1], 'lum': 1.0})
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art, 'goth.blend'), copy=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
json.dump({'id': 'goth', 'label': 'goth', 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
print('GOTH', [(p['mesh'], p['material']) for p in parts])
