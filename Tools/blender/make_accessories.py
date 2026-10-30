# Makes the wardrobe's everyday accessories from scratch, fitted to the starlets' shared body (olivia), and exports them
# for the game like make_goth.py does: Assets/Starlets/acc (a pseudo-avatar holding only these pieces) and Art3D/acc.blend.
#   blender -b -P Tools/blender/make_accessories.py -- <project>/Assets/Starlets <project>/Art3D
# Everything is placed from her own measurements (neck, ears, eyes, crown, chest, hips, hands), so it sits on her, not
# near her. Part names tell the game what each rides on (SLRig.Pin): hat_* / glasses_* / ear_* on the head, hand_* rigid
# in her hand, anything else on her body but never her arms or legs. Plain parts are dyed to the item's colour; parts in a
# Bead... material keep their own (gold, pearl, lenses).
import bpy, bmesh, sys, os, json, math
from mathutils import Vector, Matrix
assets, art = sys.argv[sys.argv.index('--') + 1:][:2]
out = os.path.join(assets, 'acc'); os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
GOLD = (0.86, 0.66, 0.30, 1); PEARL = (0.95, 0.93, 0.88, 1); LENS = (0.06, 0.05, 0.07, 1); PLAIN = (1, 1, 1, 1); SILVER = (0.8, 0.82, 0.86, 1)

def load(avatar, part):
    bpy.ops.import_scene.fbx(filepath=os.path.join(assets, avatar, 'starlet.fbx')); got = None
    for o in list(bpy.context.selected_objects):
        if o.name.split('.')[0] == part and got is None: got = o
        else: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT'); got.select_set(True); bpy.context.view_layer.objects.active = got
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); return got
def mat(name, color, rough=0.5):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = color; b.inputs['Roughness'].default_value = rough
    return m
made = {}
def keep(part, o, material): o.data.materials.clear(); o.data.materials.append(material); made.setdefault(part, []).append(o); return o
def tube(part, pts, r, material, closed=False, res=3):
    cu = bpy.data.curves.new(part, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = r; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1); sp.use_cyclic_u = closed
    for i, p in enumerate(pts): sp.points[i].co = (p[0], p[1], p[2], 1)
    o = bpy.data.objects.new(part, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.convert(target='MESH')
    return keep(part, bpy.context.object, material)
def ball(part, c, r, material, squash=(1, 1, 1), sub=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=c); o = bpy.context.object; o.scale = squash; return keep(part, o, material)
def torus(part, c, R, r, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=c, rotation=rot, major_segments=40, minor_segments=10); return keep(part, bpy.context.object, material)
def solid(part, verts2d, depth, material, at, rot=(0, 0, 0), bevel=0.003):
    """A flat outline (x, z) given thickness along y and soft edges: bag bodies, a bow."""
    bm = bmesh.new(); f = bm.faces.new([bm.verts.new((x, -depth / 2, z)) for x, z in verts2d]); r = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, verts=[e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)], vec=(0, depth, 0)); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(o)
    o.location = at; o.rotation_euler = rot
    if bevel: m = o.modifiers.new('b', 'BEVEL'); m.width = bevel; m.segments = 3; bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.modifier_apply(modifier='b')
    return keep(part, o, material)
def lathe(part, profile, material, at, n=48, sx=1.0, sy=1.0):
    """A round shape turned from a profile of (radius, height) points: hats, the clasp of a bag."""
    bm = bmesh.new(); rings = []
    for (r, z) in profile: rings.append([bm.verts.new((at[0] + math.cos(a) * r * sx, at[1] + math.sin(a) * r * sy, at[2] + z)) for a in [i * math.tau / n for i in range(n)]])
    for a, b in zip(rings, rings[1:]):
        for i in range(n): bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(o); return keep(part, o, material)

head = load('olivia', 'head'); body = load('olivia', 'body'); eyes = load('olivia', 'eyes'); top = load('olivia', 'top'); hair = load('olivia', 'hair')
plain, gold, pearl, lens, silver = mat('Fabric', PLAIN, 0.6), mat('BeadGold', GOLD, 0.25), mat('BeadPearl', PEARL, 0.18), mat('BeadLens', LENS, 0.05), mat('BeadSilver', SILVER, 0.25)
leather = mat('Leather', PLAIN, 0.42)

# ------------------------------------------------------------------ her measurements
def span(obj, test):
    vs = [v.co for v in obj.data.vertices if test(v.co)]; return vs
H = head.data.vertices
neck = [v.co for v in H if 1.42 < v.co.z < 1.45]
nc = Vector(((min(p.x for p in neck) + max(p.x for p in neck)) / 2, (min(p.y for p in neck) + max(p.y for p in neck)) / 2, 1.435)); nrx = (max(p.x for p in neck) - min(p.x for p in neck)) / 2; nry = (max(p.y for p in neck) - min(p.y for p in neck)) / 2
E = [v.co for v in eyes.data.vertices]; ec = sum(E, Vector()) / len(E)
eye_l = sum((p for p in E if p.x > 0), Vector()) / max(1, len([p for p in E if p.x > 0])); eye_r = sum((p for p in E if p.x < 0), Vector()) / max(1, len([p for p in E if p.x < 0]))
front_eye = min(p.y for p in E)                                         # the front of her eyes (her face looks to -y)
ear = max((v.co for v in H if 1.53 < v.co.z < 1.58), key=lambda c: c.x)    # the outer edge of her ear
lobe = min((v.co for v in H if abs(v.co.x) > ear.x - 0.012 and v.co.x > 0 and v.co.z < 1.56 and v.co.y > ear.y - 0.02), key=lambda c: c.z)
nose = min((v.co for v in H if abs(v.co.x) < 0.006 and abs(v.co.z - ec.z) < 0.015), key=lambda c: c.y)   # the bridge of her nose
crown = [v.co for v in list(H) + list(hair.data.vertices) if v.co.z > 1.62]
cc = Vector(((min(p.x for p in crown) + max(p.x for p in crown)) / 2, (min(p.y for p in crown) + max(p.y for p in crown)) / 2, 0)); crx = (max(p.x for p in crown) - min(p.x for p in crown)) / 2; cry = (max(p.y for p in crown) - min(p.y for p in crown)) / 2
topz = max(p.z for p in crown)
chest = min((v.co for v in top.data.vertices if abs(v.co.x) < 0.02 and 1.30 < v.co.z < 1.36), key=lambda c: c.y)
hipv = [v.co for v in body.data.vertices if 0.94 < v.co.z < 0.98 and abs(v.co.x) < 0.2]; hip_x = max(p.x for p in hipv); hip_front = min(p.y for p in hipv)
hand = [v.co for v in body.data.vertices if v.co.x > 0.2 and 0.6 < v.co.z < 1.1]; hz = min(p.z for p in hand)     # her hand (not her foot)
palm = sum((p for p in hand if p.z < hz + 0.09), Vector()) / max(1, len([p for p in hand if p.z < hz + 0.09]))
print('MEASURE neck', tuple(round(x, 3) for x in nc), round(nrx, 3), round(nry, 3), 'eyes', tuple(round(x, 3) for x in ec), 'ear', tuple(round(x, 3) for x in ear), 'crown', round(crx, 3), round(cry, 3), round(topz, 3), 'palm', tuple(round(x, 3) for x in palm))

# ------------------------------------------------------------------ necklaces and ties: round her neck, falling to the chest
def necklace(depth_z, front_y, sag=1.4, grow=0.006, n=64):
    pts = []
    for i in range(n):
        a = i * math.tau / n; f = (1 - math.cos(a)) / 2            # 0 at the nape, 1 at the front
        pts.append((math.sin(a) * (nrx + grow) * (1 - 0.3 * f), (nc.y + nry + 0.004) * (1 - f) + front_y * f, 1.462 * (1 - f ** sag) + depth_z * f ** sag))
    return pts
cy = chest.y - 0.004
# pearls: a strand of beads at the base of her neck
for i, p in enumerate(necklace(1.385, cy + 0.02, 1.6)):
    if i % 2 == 0: ball('pearls', p, 0.0052, pearl)
# pendant: a fine gold chain to a teardrop on the breastbone
tube('pendant', necklace(1.345, cy, 1.5), 0.0011, gold, closed=True)
ball('pendant', (0, cy - 0.006, 1.333), 0.009, mat('BeadGem', (0.65, 0.08, 0.2, 1), 0.08), squash=(1, 0.55, 1.35))
torus('pendant', (0, cy - 0.004, 1.333), 0.0105, 0.0016, gold, rot=(math.radians(90), 0, 0))
# a velvet choker with a little gold charm
def band(part, c, rx, ry, height, grow, material, n=56):
    bm = bmesh.new(); lo = []; hi = []
    for i in range(n):
        a = i * math.tau / n; x, y = c.x + math.cos(a) * (rx + grow), c.y + math.sin(a) * (ry + grow)
        lo.append(bm.verts.new((x, y, c.z - height / 2))); hi.append(bm.verts.new((x, y, c.z + height / 2)))
    for i in range(n): bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(o)
    return keep(part, o, material)
band('choker', nc, nrx, nry, 0.016, 0.0025, mat('Velvet', PLAIN, 0.8))
ball('choker', (0, nc.y - nry - 0.007, nc.z - 0.014), 0.005, gold, squash=(1, 0.6, 1))
# tie: a knot at the collar and a blade down the shirt front; bow tie: a bow at the collar
knot_y = nc.y - nry - 0.012
solid('tie', [(-0.012, 0.012), (0.012, 0.012), (0.008, -0.012), (-0.008, -0.012)], 0.012, plain, (0, knot_y, 1.415))
solid('tie', [(-0.009, 0), (0.009, 0), (0.042, -0.21), (0, -0.25), (-0.042, -0.21)], 0.006, plain, (0, cy - 0.012, 1.405), rot=(math.radians(-6), 0, 0))
solid('bowtie', [(0, 0), (0.05, 0.022), (0.052, -0.022)], 0.012, plain, (0, knot_y, 1.415)); solid('bowtie', [(0, 0), (-0.05, 0.022), (-0.052, -0.022)], 0.012, plain, (0, knot_y, 1.415))
ball('bowtie', (0, knot_y - 0.002, 1.415), 0.009, plain, squash=(1, 0.8, 1.1))

# ------------------------------------------------------------------ earrings: on her lobes
for side in (1, -1):
    lx, ly, lz = side * (lobe.x + 0.001), lobe.y, lobe.z - 0.002
    torus('ear_hoops', (lx, ly, lz - 0.02), 0.019, 0.0018, gold, rot=(0, math.radians(90), 0))
    ball('ear_studs', (lx + side * 0.001, ly - 0.002, lz + 0.002), 0.0042, mat('BeadDiamond', (0.92, 0.95, 1.0, 1), 0.02))
    tube('ear_drops', [(lx, ly, lz), (lx, ly, lz - 0.022)], 0.0007, gold)
    ball('ear_drops', (lx, ly, lz - 0.032), 0.0065, mat('BeadGem', (0.65, 0.08, 0.2, 1), 0.08), squash=(0.75, 0.75, 1.3))

# ------------------------------------------------------------------ glasses: frames on the bridge of her nose and her ears
ex = (eye_l.x - eye_r.x) / 2; gy = min(front_eye, nose.y + 0.008) - 0.012; gz = ec.z
def lens_shape(kind):
    if kind == 'round': return [(math.cos(a) * 0.024, math.sin(a) * 0.024) for a in [i * math.tau / 32 for i in range(32)]]
    if kind == 'cat':   return [(math.cos(a) * 0.028 * (1.1 if math.cos(a) > 0 else 0.95), math.sin(a) * 0.019 + (0.008 if (math.cos(a) > 0.5 and math.sin(a) > 0) else 0)) for a in [i * math.tau / 32 for i in range(32)]]
    if kind == 'heart': return [(0.024 * (16 * math.sin(t) ** 3) / 16, 0.024 * (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)) / 17 + 0.004) for t in [i * math.tau / 40 for i in range(40)]]
    return [(math.copysign(abs(math.cos(a)) ** 0.6, math.cos(a)) * 0.029, math.copysign(abs(math.sin(a)) ** 0.6, math.sin(a)) * 0.021 - (0.003 if math.sin(a) < 0 else 0)) for a in [i * math.tau / 40 for i in range(40)]]   # rounded square
def glasses(part, kind, tint, rim=0.0022):
    for side in (1, -1):
        sh = [(side * ex + x * (1 if kind != 'cat' else side), z) for x, z in lens_shape(kind)]
        tube(part, [(x, gy, gz + z) for x, z in sh], rim, plain, closed=True)
        bm = bmesh.new(); f = bm.faces.new([bm.verts.new((x, gy + 0.0005, gz + z)) for x, z in sh]); me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(o); keep(part, o, tint)
        hinge = (side * (ex + 0.029), gy, gz + 0.006); back = (side * (ear.x - 0.004), ear.y + 0.01, ear.z + 0.012)
        tube(part, [hinge, (side * (ear.x + 0.002), (gy + ear.y) / 2, gz + 0.008), back, (back[0] - side * 0.004, back[1] + 0.012, back[2] - 0.016)], rim * 0.75, plain)
    tube(part, [(-ex + 0.022, gy, gz + 0.004), (0, gy - 0.003, gz + 0.008), (ex - 0.022, gy, gz + 0.004)], rim * 0.8, plain)
glasses('glasses_round', 'round', mat('BeadClear', (0.85, 0.9, 0.95, 1), 0.02)); glasses('glasses_cat', 'cat', mat('BeadClear', (0.85, 0.9, 0.95, 1), 0.02))
glasses('glasses_sun', 'square', lens); glasses('glasses_heart', 'heart', mat('BeadRose', (0.85, 0.2, 0.45, 1), 0.05))

# ------------------------------------------------------------------ hats: sized to her crown and her hair
head_top = max(v.co.z for v in H); base = head_top - 0.058                               # the band of a hat: round her head at the hairline
band_pts = [v.co for v in H if abs(v.co.z - base) < 0.008]
hc = Vector(((min(p.x for p in band_pts) + max(p.x for p in band_pts)) / 2, (min(p.y for p in band_pts) + max(p.y for p in band_pts)) / 2, 0))
rx, ry = (max(p.x for p in band_pts) - min(p.x for p in band_pts)) / 2 + 0.014, (max(p.y for p in band_pts) - min(p.y for p in band_pts)) / 2 + 0.014     # room for a little hair under it
lathe('hat_beret', [(1.0, 0.0), (1.08, 0.015), (1.22, 0.04), (1.05, 0.07), (0.55, 0.085), (0.0, 0.088)], plain, (hc.x + 0.012, hc.y + 0.01, base + 0.004), sx=rx, sy=ry)      # slouched a little to one side
ball('hat_beret', (hc.x + 0.012, hc.y + 0.01, base + 0.093), 0.006, plain)
lathe('hat_beanie', [(1.0, -0.005), (1.02, 0.03), (1.0, 0.032), (0.97, 0.06), (0.82, 0.088), (0.45, 0.103), (0.0, 0.106)], plain, (hc.x, hc.y, base - 0.01), sx=rx, sy=ry)
lathe('hat_beanie', [(1.04, -0.008), (1.05, 0.034)], plain, (hc.x, hc.y, base - 0.01), sx=rx, sy=ry)          # the turned-up cuff
lathe('hat_sunhat', [(2.6, -0.006), (2.5, 0.0), (1.12, 0.012), (1.05, 0.02), (1.02, 0.07), (0.6, 0.095), (0.0, 0.1)], plain, (hc.x, hc.y, base), sx=rx, sy=ry)
lathe('hat_sunhat', [(1.06, 0.018), (1.065, 0.04)], mat('BeadRibbon', (0.12, 0.1, 0.09, 1), 0.5), (hc.x, hc.y, base), sx=rx, sy=ry)
lathe('hat_cap', [(1.02, 0.0), (1.02, 0.03), (0.9, 0.075), (0.5, 0.1), (0.0, 0.105)], plain, (hc.x, hc.y, base), sx=rx, sy=ry)
solid('hat_cap', [(-0.075, 0.0), (0.075, 0.0), (0.06, 0.075), (0.0, 0.088), (-0.06, 0.075)], 0.006, plain, (hc.x, hc.y - ry, base + 0.006), rot=(math.radians(-80), 0, 0))
ball('hat_cap', (hc.x, hc.y, base + 0.106), 0.008, plain)
# tiara: a gold band over the crown with a row of peaks and stones
arc = [(math.cos(a) * rx * 0.98, hc.y - 0.02 + math.sin(a) * 0.012, base + 0.02 + math.sin(a) * 0.05) for a in [math.radians(12 + i * 156 / 30) for i in range(31)]]
tube('hat_tiara', arc, 0.0022, gold)
for k in range(-3, 4):
    a = math.radians(90 + k * 13); x, z = math.cos(a) * rx * 0.98, base + 0.02 + math.sin(a) * 0.05; h = 0.03 - abs(k) * 0.006
    tube('hat_tiara', [(x - 0.006, hc.y - 0.034, z), (x, hc.y - 0.034, z + h), (x + 0.006, hc.y - 0.034, z)], 0.0016, gold)
    ball('hat_tiara', (x, hc.y - 0.036, z + h * 0.45), 0.0035 if k else 0.005, mat('BeadDiamond', (0.92, 0.95, 1.0, 1), 0.02))

# ------------------------------------------------------------------ bags
# a crossbody: the strap over her far shoulder, the bag against her hip at the front
bx, bz = hip_x + 0.01, 0.97; by = (hip_front + 0.0) / 2 - 0.035      # at the side of her hip, a little forward
solid('crossbody', [(-0.075, 0.0), (0.075, 0.0), (0.072, 0.11), (-0.072, 0.11)], 0.032, leather, (bx - 0.02, by, bz - 0.1), rot=(0, 0, math.radians(-55)))
solid('crossbody', [(-0.076, 0.0), (0.076, 0.0), (0.06, -0.06), (-0.06, -0.06)], 0.034, leather, (bx - 0.02, by - 0.0005, bz + 0.012), rot=(math.radians(180), 0, math.radians(-55)))   # the flap
ball('crossbody', (bx - 0.02, by - 0.019, bz - 0.045), 0.006, gold, squash=(1.4, 0.5, 1))
strap = []
for i in range(48):
    a = i * math.tau / 48; u = (1 + math.cos(a)) / 2            # 1 at the bag, 0 at her far shoulder
    strap.append((-0.11 + (bx + 0.11) * u, (nc.y) + math.sin(a) * (0.125 + 0.04 * u) - 0.01 * u, 1.45 - (1.45 - bz) * u))
tube('crossbody', strap, 0.0055, leather, closed=True)
# a heart-shaped bag on the same strap
heart = [(0.06 * (16 * math.sin(t) ** 3) / 16, 0.06 * (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)) / 17) for t in [i * math.tau / 48 for i in range(48)]]
solid('heartbag', heart, 0.03, leather, (bx - 0.02, by, bz - 0.06), rot=(0, 0, math.radians(-55)))
tube('heartbag', strap, 0.0045, gold, closed=True)
# a tote in her hand, hanging by its handles; a clutch held in her hand
tx, ty, tz = palm.x + 0.005, palm.y, palm.z - 0.012
solid('hand_tote', [(-0.16, 0.0), (0.16, 0.0), (0.13, -0.28), (-0.13, -0.28)], 0.075, leather, (tx, ty, tz - 0.05), rot=(0, 0, math.radians(90)))
for d in (-0.035, 0.035):
    tube('hand_tote', [(tx, ty + d - 0.1, tz - 0.05), (tx, ty + d * 0.4, tz + 0.012), (tx, ty + d + 0.1, tz - 0.05)], 0.0045, leather)
solid('hand_clutch', [(-0.11, 0.0), (0.11, 0.0), (0.1, -0.11), (-0.1, -0.11)], 0.022, leather, (palm.x + 0.012, palm.y - 0.02, palm.z + 0.05), rot=(0, 0, math.radians(90)))
solid('hand_clutch', [(-0.11, 0.0), (0.11, 0.0), (0.0, -0.06)], 0.024, leather, (palm.x + 0.012, palm.y - 0.0205, palm.z + 0.05), rot=(0, 0, math.radians(90)))
ball('hand_clutch', (palm.x + 0.025, palm.y - 0.02, palm.z + 0.0), 0.006, gold)

# ------------------------------------------------------------------ join each part, export, describe
for o in (head, body, eyes, top, hair): bpy.data.objects.remove(o, do_unlink=True)
parts = []
for part, objs in made.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = part
    for p in o.data.polygons: p.use_smooth = True
    # one part per material: dyed pieces and their gold, pearl or lens bits go apart
    mats = {m.name for m in o.data.materials if m}
    if len(mats) > 1:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    for q in [x for x in bpy.context.selected_objects]:
        m = q.data.materials[0].name; name = part if not m.startswith('Bead') else part + '_m' if part + '_m' not in [p['mesh'] for p in parts] else part + '_m2'
        q.name = q.data.name = name; keep_col = m.startswith('Bead')
        col = list(next(n for n in q.data.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs['Base Color'].default_value)
        parts.append({'mesh': name, 'material': m, 'tex': '', 'normal': '', 'alpha': False, 'color': col if keep_col else [0.6, 0.6, 0.6, 1],
                      'rough': 0.2 if keep_col else 0.5, 'metal': 0.8 if m in ('BeadGold', 'BeadSilver') else 0.0, 'tone': [1, 1, 1], 'lum': 1.0,
                      'tris': sum(len(f.vertices) - 2 for f in q.data.polygons)})
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art, 'acc.blend'), copy=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
json.dump({'id': 'acc', 'label': 'acc', 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
print('ACC', [(p['mesh'], p['material'], p['tris']) for p in parts])
