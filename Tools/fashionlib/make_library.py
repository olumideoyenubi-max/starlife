# The fashion library: original luxury and mid-range accessories, shoes, bags, jewellery and hats, modelled from scratch
# and fitted to Starlife's shared body (olivia), for Starlife first and to be adapted for other games.
#   blender -b -P Tools/fashionlib/make_library.py -- <library> <StarLife>/Assets/Starlets [id ...]
# For each item, <library>/<tier>/<id>/ gets:
#   <id>.blend          cinematic version: live bevels and subdivision, procedural detail, the body as a hidden reference
#   <id>.glb            game version: <id>_LOD0 / _LOD1 / _LOD2 nodes, PBR textures, olivia's space (metres, Y-up in glTF)
#   <id>_starlife.fbx   LOD0 alone, laid out like the goth pieces (Z-up, olivia's space), for SLBuild.Import3D
#   preview.png, onbody.png, manifest.json (slot, attach point, materials, triangle counts)
# Designs are original: no real brand names, logos or signature trade dress. Hardware carries Starlife's own star emblem.
import bpy, bmesh, os, sys, json, math, shutil
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from shoes import Last, sole_mesh, ZS, M as MSTRIP

args = sys.argv[sys.argv.index('--') + 1:]
LIB, STAR = args[0], args[1]; ONLY = set(args[2:])
TEX = os.path.join(LIB, 'textures')
OLIVIA = os.path.join(STAR, 'olivia')

# ======================================================================================================== scene basics
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene; s.unit_settings.system = 'METRIC'; return s

def link(o, coll=None): (coll or bpy.context.scene.collection).objects.link(o); return o

def mesh_obj(name, bm, material=None, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); o = link(bpy.data.objects.new(name, me))
    for p in me.polygons: p.use_smooth = smooth
    if material: me.materials.append(material)
    return o

def select(objs, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]

def apply_xf(o): select([o]); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def bevel(o, width, segs=3, limit='ANGLE', angle=40):
    m = o.modifiers.new('Bevel', 'BEVEL'); m.width = width; m.segments = segs; m.limit_method = limit
    if limit == 'ANGLE': m.angle_limit = math.radians(angle)
    m.harden_normals = False; return m

def subsurf(o, view=1, render=2):
    m = o.modifiers.new('Subdivision', 'SUBSURF'); m.levels = view; m.render_levels = render; return m

def solidify(o, t, offset=-1):
    m = o.modifiers.new('Solidify', 'SOLIDIFY'); m.thickness = t; m.offset = offset; m.use_even_offset = False; return m

# ======================================================================================================== materials
MATS = {}
def img(name, colour=True):
    p = os.path.join(TEX, name + '.png'); im = bpy.data.images.load(p, check_existing=True)
    if not colour: im.colorspace_settings.name = 'Non-Color'
    return im

def pbr(name, tex=None, color=(0.8, 0.8, 0.8, 1), metal=0.0, rough=0.5, coat=0.0, sheen=0.0, alpha=1.0, trans=0.0, normal_strength=1.0):
    """A Principled material. With `tex`, it uses <tex>_col/_nrm/_rgh from the library's textures (UV-mapped)."""
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough; b.inputs['Base Color'].default_value = color
    if coat: b.inputs['Coat Weight'].default_value = coat; b.inputs['Coat Roughness'].default_value = 0.05
    if sheen: b.inputs['Sheen Weight'].default_value = sheen
    if trans: b.inputs['Transmission Weight'].default_value = trans
    if alpha < 1: b.inputs['Alpha'].default_value = alpha; m.surface_render_method = 'BLENDED'
    if tex:
        c = nt.nodes.new('ShaderNodeTexImage'); c.image = img(tex + '_col'); nt.links.new(c.outputs['Color'], b.inputs['Base Color'])
        r = nt.nodes.new('ShaderNodeTexImage'); r.image = img(tex + '_rgh', False); nt.links.new(r.outputs['Color'], b.inputs['Roughness'])
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = img(tex + '_nrm', False); nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal_strength
        nt.links.new(n.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    m['tex'] = tex or ''; MATS[name] = m; return m

def decal(name, image, metal=0.0, rough=0.4):
    """A single-image material (watch dials): UVs are laid out by the builder."""
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(image); nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough; m['tex'] = image; MATS[name] = m; return m

M = {}
def materials():
    MATS.clear()
    M.update({
        'lamb': pbr('Lambskin Black', 'lambskin_black', normal_strength=0.35), 'cognac': pbr('Calf Cognac', 'calf_cognac', normal_strength=0.4),
        'ivory': pbr('Calf Ivory', 'calf_ivory', normal_strength=0.35), 'burgundy': pbr('Calf Burgundy', 'calf_burgundy', normal_strength=0.4),
        'patent': pbr('Patent Blush', 'patent_blush', coat=0.6), 'suede': pbr('Suede Taupe', 'suede_taupe', sheen=0.4),
        'satin': pbr('Satin Emerald', 'satin_emerald', sheen=0.08), 'felt': pbr('Felt Camel', 'felt_camel', sheen=0.3),
        'silk': pbr('Silk Adire', 'silk_adire', sheen=0.2), 'canvas': pbr('Canvas Natural', 'canvas_natural'),
        'navy': pbr('Canvas Navy', 'canvas_navy'), 'khaki': pbr('Twill Khaki', 'twill_khaki'), 'webbing': pbr('Webbing Olive', 'webbing_olive'),
        'nylon': pbr('Nylon Black', 'nylon_black'), 'rubberw': pbr('Rubber White', 'rubber_white'), 'rubberb': pbr('Rubber Black', 'rubber_black'),
        'gold': pbr('Gold Polished', 'gold_polished', metal=1.0), 'goldb': pbr('Gold Brushed', 'gold_brushed', metal=1.0),
        'steel': pbr('Steel Brushed', 'steel_brushed', metal=1.0), 'silver': pbr('Silver Polished', 'silver_polished', metal=1.0),
        'tort': pbr('Acetate Tortoise', 'acetate_tortoise', coat=0.5),
        'lens_grad': pbr('Lens Brown', color=(0.16, 0.09, 0.05, 1), rough=0.03, alpha=0.72, coat=1.0),
        'lens_dark': pbr('Lens Smoke', color=(0.03, 0.035, 0.04, 1), rough=0.03, alpha=0.85, coat=1.0),
        'crystal': pbr('Crystal', color=(0.95, 0.97, 1, 1), rough=0.0, trans=1.0, alpha=0.55),
        'pearl': pbr('Pearl', color=(0.93, 0.9, 0.86, 1), rough=0.18, coat=0.8, sheen=0.5),
        'malachite': pbr('Malachite', color=(0.02, 0.32, 0.18, 1), rough=0.12, coat=0.8),
        'thread_ivory': pbr('Thread Ivory', color=(0.85, 0.8, 0.7, 1), rough=0.7), 'thread_dark': pbr('Thread Dark', color=(0.08, 0.06, 0.05, 1), rough=0.7),
        'edge': pbr('Edge Paint', color=(0.12, 0.05, 0.03, 1), rough=0.35), 'grosgrain': pbr('Grosgrain Black', color=(0.025, 0.025, 0.03, 1), rough=0.6, sheen=0.4),
        'lining': pbr('Lining', color=(0.45, 0.32, 0.22, 1), rough=0.8),
        'bead_coral': pbr('Bead Coral', color=(0.72, 0.18, 0.12, 1), rough=0.25, coat=0.5), 'bead_amber': pbr('Bead Amber', color=(0.85, 0.5, 0.08, 1), rough=0.2, coat=0.6),
        'bead_teal': pbr('Bead Teal', color=(0.05, 0.42, 0.45, 1), rough=0.25, coat=0.5), 'bead_cream': pbr('Bead Cream', color=(0.88, 0.82, 0.7, 1), rough=0.35),
        'bead_black': pbr('Bead Black', color=(0.03, 0.03, 0.03, 1), rough=0.2, coat=0.6),
        'dial_lux': decal('Dial Sunburst', 'dial_sunburst', 0.3, 0.35), 'dial_field': decal('Dial Field', 'dial_field', 0.0, 0.6),
        'elastic': pbr('Elastic Black', color=(0.03, 0.03, 0.03, 1), rough=0.7),
    })

# ======================================================================================================== geometry helpers
def frame(t, up_hint=Vector((0, 0, 1))):
    t = t.normalized(); s = t.cross(up_hint)
    if s.length < 1e-4: s = t.cross(Vector((1, 0, 0)))
    s.normalize(); u = s.cross(t); return s, u

def ribbon(name, pts, normals, width, thick, material, closed=False, round_edge=True):
    """A flat strap along a path: lies in the surface given by `normals` (one per point), width across, thickness along normal."""
    bm = bmesh.new(); rings = []; n = len(pts)
    for i in range(n):
        a = pts[(i - 1) % n] if closed or i > 0 else pts[i]; b = pts[(i + 1) % n] if closed or i < n - 1 else pts[i]
        t = (b - a).normalized(); nn = normals[i].normalized(); s = t.cross(nn).normalized(); nn = s.cross(t).normalized()
        w = width / 2; h = thick / 2; p = pts[i]
        q = [p + s * w + nn * h, p - s * w + nn * h, p - s * w - nn * h, p + s * w - nn * h]
        rings.append([bm.verts.new(v) for v in q])
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(4): bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k]))
    if not closed: bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    o = mesh_obj(name, bm, material)
    if round_edge: bevel(o, min(thick, width) * 0.3, 2, 'ANGLE', 60)
    return o

def tube(name, pts, radius, material, closed=False, segs=10, radii=None):
    bm = bmesh.new(); rings = []; n = len(pts)
    for i in range(n):
        a = pts[(i - 1) % n] if closed or i > 0 else pts[i]; b = pts[(i + 1) % n] if closed or i < n - 1 else pts[i]
        s, u = frame(b - a); r = radii[i] if radii else radius
        rings.append([bm.verts.new(pts[i] + (s * math.cos(k * math.tau / segs) + u * math.sin(k * math.tau / segs)) * r) for k in range(segs)])
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(segs): bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    if not closed: bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    return mesh_obj(name, bm, material)

def torus_into(bm, mat_world, R, r, seg=16, rseg=8, stretch=0.0):
    """An (optionally stadium-stretched) torus in the XY plane, transformed by mat_world, added to bm."""
    rings = []
    for i in range(seg):
        a = i * math.tau / seg; c = Vector((math.cos(a), math.sin(a), 0)); centre = c * R + Vector((math.copysign(stretch, c.x) if abs(c.x) > 1e-6 else 0, 0, 0))
        rings.append([bm.verts.new(mat_world @ (centre + (c * math.cos(k * math.tau / rseg) + Vector((0, 0, math.sin(k * math.tau / rseg)))) * r)) for k in range(rseg)])
    for i in range(seg):
        r0, r1 = rings[i], rings[(i + 1) % seg]
        for k in range(rseg): bm.faces.new((r0[k], r1[k], r1[(k + 1) % rseg], r0[(k + 1) % rseg]))

def resample(pts, step, closed=False):
    """Points every `step` metres along a polyline (with their tangents)."""
    seq = pts + ([pts[0]] if closed else []); out = []; carry = 0.0
    for a, b in zip(seq[:-1], seq[1:]):
        L = (b - a).length; d = carry
        while d < L: out.append((a.lerp(b, d / L), (b - a).normalized())); d += step
        carry = d - L
    return out

def chain(name, pts, link_len, wire, material, closed=False, curb=False):
    """Interlocking oval links along a path, each turned 90 degrees from the last (curb: flattened and twisted)."""
    bm = bmesh.new()
    for i, (p, t) in enumerate(resample(pts, link_len * 0.78, closed)):
        s, u = frame(t); roll = (math.pi / 2 if i % 2 else 0) if not curb else (0.35 if i % 2 else -0.35)
        rot = Matrix.Rotation(roll, 4, t)
        basis = Matrix((t, s, u)).transposed().to_4x4(); m = Matrix.Translation(p) @ rot @ basis
        if curb: m = m @ Matrix.Diagonal((1, 1, 0.55, 1))
        torus_into(bm, m, link_len * 0.32, wire, seg=14, rseg=6, stretch=link_len * 0.14)
    o = mesh_obj(name, bm, material); o['trim'] = 1
    px = tube(name + '_proxy', [p for p, t in resample(pts, link_len * 2, closed)], link_len * 0.3, material, closed=closed, segs=5); px['lod2'] = 1
    return o

def lathe(name, prof, material, segs=48, m=Matrix(), cap_top=True, cap_bottom=True):
    """Revolve a (radius, height) profile around local Z; m places it."""
    bm = bmesh.new(); rings = []
    for r, z in prof: rings.append([bm.verts.new(m @ Vector((math.cos(k * math.tau / segs) * r, math.sin(k * math.tau / segs) * r, z))) for k in range(segs)])
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(segs): bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    if cap_bottom and prof[0][0] > 1e-5: bm.faces.new(rings[0][::-1])
    if cap_top and prof[-1][0] > 1e-5: bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); return mesh_obj(name, bm, material)

def rbox(name, size, centre, material, r=0.004, segs=3, rot=(0, 0, 0), cuts=0):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    if cuts: bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts); o = mesh_obj(name, bm, material)
    o.location = centre; o.rotation_euler = rot; apply_xf(o)
    if r: bevel(o, r, segs)
    return o

def outline_prism(name, outline, depth, material, m=Matrix(), r=0.0, segs=3):
    """Extrude a closed 2D outline [(x, y)] by depth along local Z (centred), placed by m."""
    bm = bmesh.new(); lo = [bm.verts.new((x, y, -depth / 2)) for x, y in outline]; hi = [bm.verts.new((x, y, depth / 2)) for x, y in outline]
    bm.faces.new(lo[::-1]); bm.faces.new(hi); n = len(outline)
    for i in range(n): bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = mesh_obj(name, bm, material, smooth=False)
    if r: bevel(o, r, segs, 'ANGLE', 30)
    return o

def star_outline(R, inner=0.38, points=4, steps=4):
    pts = []
    for i in range(points * 2):
        a = i * math.pi / points; rr = R if i % 2 == 0 else R * inner; pts.append((math.sin(a) * rr, math.cos(a) * rr))
    return pts

def stitches(name, pts, spacing, length, radius, material, closed=False):
    """Saddle stitching: short thread capsules along a path."""
    bm = bmesh.new()
    for p, t in resample(pts, spacing, closed):
        s, u = frame(t); mm = Matrix((t, s, u)).transposed().to_4x4()
        bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=1.0, matrix=Matrix.Translation(p) @ mm @ Matrix.Diagonal((length / 2, radius, radius, 1)))
    return mesh_obj(name, bm, material)

def quilt_box(name, w, h, d, material, cell=0.035, depth=0.004, r=0.012, faces=('front', 'back')):
    """A soft box (w along X, h along Z, d along Y) whose front and back panels are pushed in along a diamond grid."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=28, use_grid_fill=True); bmesh.ops.scale(bm, vec=(w, d, h), verts=bm.verts)
    for v in bm.verts:
        x, y, z = v.co
        # round the box: pull corners in so the edges read as padded, not sharp
        ex = max(0.0, abs(x) - (w / 2 - r)) / r; ez = max(0.0, abs(z) - (h / 2 - r)) / r; ey = max(0.0, abs(y) - (d / 2 - r)) / r
        k = min(1.0, math.sqrt(ex * ex + ez * ez + ey * ey))
        bulge = (1 - (2 * x / w) ** 2) * (1 - (2 * z / h) ** 2)       # panels puff out in the middle
        if abs(abs(y) - d / 2) < 1e-6:
            if (y < 0 and 'front' in faces) or (y > 0 and 'back' in faces):
                u, vv = x / cell, z / cell; a = (u + vv) % 1; b = (u - vv) % 1
                line = min(abs(a - 0.5), abs(b - 0.5)) * 2      # 0 on a stitch line
                v.co.y += math.copysign(1, y) * (-depth * max(0.0, 1 - line / 0.18) ** 2 + 0.006 * bulge)
            else: v.co.y += math.copysign(1, y) * 0.004 * bulge
        v.co.x -= math.copysign(1, x) * r * 0.25 * k * k; v.co.z -= math.copysign(1, z) * r * 0.25 * k * k
    o = mesh_obj(name, bm, material); bevel(o, r * 0.6, 4, 'ANGLE', 50); return o

# ======================================================================================================== the body
TORSO = 0.19    # the furthest the torso reaches from its axis; the arms hang beyond this
class Body:
    def __init__(self):
        bpy.ops.import_scene.fbx(filepath=os.path.join(OLIVIA, 'pieces_thin.fbx')); self.parts = {}
        coll = bpy.data.collections.new('ref_body'); bpy.context.scene.collection.children.link(coll)
        for o in list(bpy.context.selected_objects):
            if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True); continue
            apply_xf(o); name = o.name.split('.')[0]; o.name = 'ref_' + name; self.parts[name] = o
            for c in o.users_collection: c.objects.unlink(o)
            coll.objects.link(o); self.skin(o, name)
        self.coll = coll
        self.trees = {k: self.tree(o) for k, o in self.parts.items()}

    def skin(self, o, name):
        tex = {'body': 'body', 'feet': 'body', 'head': 'head', 'hair': 'hair', 'eyes': 'eyes', 'teeth': 'teeth', 'top': 'top', 'bottom': 'bottom', 'shoes': 'shoes'}.get(name)
        p = os.path.join(OLIVIA, (tex or '') + '.png')
        m = bpy.data.materials.new('ref_' + name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']; b.inputs['Roughness'].default_value = 0.6
        if tex and os.path.exists(p):
            t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(p, check_existing=True); m.node_tree.links.new(t.outputs['Color'], b.inputs['Base Color'])
        o.data.materials.clear(); o.data.materials.append(m)

    def tree(self, o):
        bm = bmesh.new(); bm.from_mesh(o.data); t = BVHTree.FromBMesh(bm); bm.free(); return t

    def ring(self, part, zlo, zhi, side=0):
        vs = [v.co for v in self.parts[part].data.vertices if zlo < v.co.z < zhi and (side == 0 and abs(v.co.x) < 0.2 or side != 0 and v.co.x * side > 0.18)]
        xs = [p.x for p in vs]; ys = [p.y for p in vs]
        return Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (zlo + zhi) / 2)), (max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2

    def hit(self, origin, direction, parts=('body', 'top', 'bottom')):
        """First surface hit among `parts` (the outermost one: the hit nearest the origin, which starts outside)."""
        best = None
        for k in parts:
            loc, nrm, _, dist = self.trees[k].ray_cast(Vector(origin), Vector(direction).normalized())
            if loc is not None and (best is None or dist < best[2]): best = (loc, nrm, dist)
        return best

    def waist(self, z, offset, n=80, parts=('body', 'top', 'bottom')):
        """A closed ring round the torso at height z. Rays go outwards from inside it: from outside, the sides would land on the hanging arms."""
        pts = []; nrms = []; c = Vector((0, -0.03, z))
        for k in range(n):
            a = k * math.tau / n; d = Vector((math.sin(a), -math.cos(a), 0)); r = 0.0
            for name in parts:      # the body mesh has gaps under her clothes: a ray through one reaches the arm, so anything past the torso does not count
                loc, _, _, dist = self.trees[name].ray_cast(c, d)
                if loc is not None and dist < TORSO: r = max(r, dist)
            r = r or (pts[-1] - c).length - offset if pts else r or 0.14
            pts.append(c + d * (r + offset)); nrms.append(d)
        for _ in range(2):      # the seams and folds of the clothes would otherwise show as dents in a stiff strap
            pts = [Vector((c.x, c.y, z)) + ((pts[i - 1] + pts[i] * 2 + pts[(i + 1) % n]) / 4 - c) for i in range(n)]
        return pts, nrms

    def sash(self, side_x, start, end, hip, offset, n=64, parts=('body', 'top', 'bottom')):
        """The line of a bag strap: from `start` on the bag across the chest, over the shoulder at side_x, down the back, round the hip through `hip` to `end`
        on the bag. Below the shoulders each point is found from inside the torso, so the hanging arm is never hit, then the whole line is smoothed so it runs taut."""
        key = [start, Vector((side_x, -0.115, 1.27)), Vector((side_x, -0.075, 1.36)), Vector((side_x, -0.03, 1.41)), Vector((side_x, 0.015, 1.36)), Vector((side_x, 0.07, 1.26)), Vector((side_x * 0.4, 0.10, 1.12)), Vector((hip.x * 0.7, 0.10, hip.z + 0.06)), hip, end]
        line = [q for q, _ in resample(key, sum((key[i + 1] - key[i]).length for i in range(len(key) - 1)) / n)] + [end]
        pts, nrms = self.lay(line[1:-1], offset, parts)
        return self.taut([line[0]] + pts + [line[-1]], [Vector((0, -1, 0))] + nrms + [Vector((0, 1, 0))])

    def lay(self, line, offset, parts=('body', 'top', 'bottom')):
        """Puts points on her: straight down over the shoulder itself (further out that would land on the chest), elsewhere from inside the torso, so the hanging arm is never hit."""
        pts = []; nrms = []
        for p in line:
            h = self.hit(p + Vector((0, 0, 0.4)), (0, 0, -1), parts) if abs(p.y + 0.03) < 0.05 else None
            if h and h[0].z > 1.2: pts.append(h[0] + h[1] * offset); nrms.append(h[1]); continue
            c = Vector((0, -0.03, p.z)); d = p - c; d.z = 0; d.normalize(); r = 0.0
            for name in parts:
                loc, _, _, dist = self.trees[name].ray_cast(c, d)
                if loc is not None and dist < TORSO: r = max(r, dist)
            pts.append(c + d * (r + offset) if r else p); nrms.append(d)
        return pts, nrms

    def taut(self, pts, nrms, passes=4):
        """Smooths a line laid on her so a strap runs taut over the folds; its ends stay where they are."""
        for _ in range(passes):
            pts = [pts[0]] + [(pts[i - 1] + pts[i] * 2 + pts[i + 1]) / 4 for i in range(1, len(pts) - 1)] + [pts[-1]]
            nrms = [nrms[0]] + [(nrms[i - 1] + nrms[i] * 2 + nrms[i + 1]).normalized() for i in range(1, len(nrms) - 1)] + [nrms[-1]]
        return pts, nrms

    def drape(self, pts, offset, parts=('body', 'top', 'bottom'), centre=None):
        """Push path points out onto the body surface: rays from far outside towards the torso axis (or straight down for points over the shoulders)."""
        out = []; nrms = []
        for p in pts:
            p = Vector(p); c = Vector(centre) if centre else Vector((0, -0.03, p.z))
            if p.z > 1.40 and abs(p.x) < 0.2:
                h = self.hit(p + Vector((0, 0, 0.4)), (0, 0, -1), parts)
            else:
                d = (p - c); d.z = 0; d.normalize(); h = self.hit(c + d * 0.6, -d, parts)
            if h: out.append(h[0] + h[1] * offset); nrms.append(h[1])
            else: out.append(p); nrms.append(Vector((0, 0, 1)))
        return out, nrms

# ======================================================================================================== the items
# Each builder takes the Body and returns {'slot', 'attach', 'tier', 'name', 'notes'}; the objects it makes are collected.
ITEMS = {}
def item(id, tier, name, slot):
    def deco(fn): ITEMS[id] = (fn, tier, name, slot); return fn
    return deco

def emblem(name, centre, normal, R, material, depth=0.0025, up=Vector((0, 0, 1))):
    """Starlife's four-point star, as a raised metal plate facing `normal`."""
    n = Vector(normal).normalized(); x = up.cross(n).normalized(); y = n.cross(x)
    m = Matrix((x, y, n)).transposed().to_4x4(); m.translation = centre
    return outline_prism(name, star_outline(R, 0.34), depth, material, m, r=depth * 0.3, segs=2)

# ---------------------------------------------------------------- luxury bags
@item('lux_flapbag', 'luxury', 'Quilted flap bag on a gold chain', 'crossbody')
def lux_flapbag(B):
    W, H, D = 0.22, 0.14, 0.07
    cx, cz = -0.11, 0.86; back = B.hit((cx, 0.0, cz), (0, -1, 0))[0].y if B.hit((cx, 0.0, cz), (0, -1, 0)) else -0.15
    h = B.hit((cx, -0.6, cz), (0, 1, 0)); front_of_thigh = h[0].y if h else -0.13
    cy = front_of_thigh - D / 2 - 0.006
    body = quilt_box('lux_flapbag_body', W, H, D, M['lamb'], cell=0.036, depth=0.005); body.location = (cx, cy, cz)
    # the flap: a padded panel over the top and two thirds of the front, curving over the top edge
    bm = bmesh.new(); nu, nv = 40, 30
    grid = [[None] * (nu + 1) for _ in range(nv + 1)]
    for j in range(nv + 1):
        for i in range(nu + 1):
            u = (i / nu - 0.5) * (W + 0.006); t = j / nv          # t: 0 at the back top edge, 1 at the flap's lower edge
            L1 = D * 0.9; L2 = H * 0.66; s = t * (L1 + L2)
            if s < L1: y = D / 2 - s; z = H / 2 + 0.006 + 0.004 * math.sin(math.pi * s / L1)
            else: y = -D / 2 - 0.006; z = H / 2 - (s - L1)
            corner = max(0.0, abs(u) - (W / 2 - 0.02)) / 0.02
            if s > L1 + L2 - 0.03: z += 0.03 * (1 - math.sqrt(max(0.0, 1 - corner ** 2))) * min(1, (s - (L1 + L2 - 0.03)) / 0.03)
            q = (u / 0.036 + z / 0.036); r_ = (u / 0.036 - z / 0.036); line = min(abs(q % 1 - 0.5), abs(r_ % 1 - 0.5)) * 2
            push = -0.004 * max(0.0, 1 - line / 0.18) ** 2 if s >= L1 else 0
            puff = 0.004 * math.sin(math.pi * min(1, max(0, (s - L1) / L2))) * (1 - (2 * u / W) ** 2)
            grid[j][i] = bm.verts.new((cx + u, cy + y - push - puff if s >= L1 else cy + y, cz + z))
    for j in range(nv):
        for i in range(nu): bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    flap = mesh_obj('lux_flapbag_flap', bm, M['lamb']); solidify(flap, 0.004, 1); bevel(flap, 0.0015, 2, 'ANGLE', 60)
    # the clasp: a brushed-gold plate and the star turn-lock
    clasp_z = cz + H / 2 - H * 0.66 + 0.012; cf = cy - D / 2 - 0.012
    rbox('lux_flapbag_plate', (0.05, 0.006, 0.026), (cx, cf + 0.003, clasp_z), M['goldb'], r=0.003)
    emblem('lux_flapbag_star', Vector((cx, cf - 0.002, clasp_z)), (0, -1, 0), 0.016, M['gold'], 0.004)
    # chain strap: from the bag's top corners, across the chest, over the opposite shoulder, down the back and round the hip
    corner_l = Vector((cx + W / 2 - 0.012, cy, cz + H / 2 + 0.01)); corner_r = Vector((cx - W / 2 + 0.012, cy, cz + H / 2 + 0.01))
    start, end = (corner_l, corner_r) if cx < 0 else (corner_r, corner_l)
    path, _ = B.sash(-math.copysign(0.135, cx), start, end, Vector((cx + math.copysign(W / 2 + 0.03, cx), 0.02, cz + 0.05)), 0.028, n=90)
    chain('lux_flapbag_chain', path, 0.016, 0.0019, M['gold'], curb=True)
    for p in (corner_l, corner_r):
        m = Matrix.Translation(p) @ Matrix.Rotation(math.pi / 2, 4, 'Y'); bm = bmesh.new(); torus_into(bm, m, 0.007, 0.0016); mesh_obj('lux_flapbag_ring', bm, M['gold'])
    return dict(attach=(cx, cy, cz), notes='Crossbody: chain over the left shoulder, bag at the right front hip.')

@item('lux_tote', 'luxury', 'Structured top-handle tote', 'hand_r')
def lux_tote(B):
    hx, hy, hz = -0.253, -0.073, 0.93                   # olivia's right hand
    W, H, D = 0.30, 0.22, 0.13                           # width runs front-to-back beside the leg
    cz = hz - 0.12 - H / 2; cx = hx - 0.035
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
    for v in bm.verts:            # trapezoid: wider at the top, gussets pinched in
        x, y, z = v.co; top = z + 0.5
        v.co = Vector((x * D * (0.85 + 0.15 * top) * (1 - 0.12 * (abs(y) * 2) ** 4 * top), y * W * (0.9 + 0.1 * top), z * H))
    bm.verts.ensure_lookup_table()
    top_faces = [f for f in bm.faces if f.normal.z > 0.9 and f.calc_center_median().z > H / 2 - 1e-4]; bmesh.ops.delete(bm, geom=top_faces, context='FACES')
    body = mesh_obj('lux_tote_body', bm, M['cognac']); body.location = (cx, hy, cz); apply_xf(body); solidify(body, 0.003); bevel(body, 0.006, 3, 'ANGLE', 35)
    # rolled top handles rising to the hand, gold rings where they meet the bag
    for side in (1, -1):
        y0, y1 = hy - 0.07, hy + 0.07; xs = cx + side * (D * 0.5 + 0.002); pts = []
        for k in range(25):
            a = k / 24 * math.pi; pts.append(Vector((xs * 0.15 + hx * 0.85 if 0.25 < k / 24 < 0.75 else xs, (y0 + y1) / 2 - math.cos(a) * 0.07, cz + H / 2 + 0.012 + math.sin(a) * 0.115)))
        pts = [Vector((xs + (hx - xs) * math.sin(k / 24 * math.pi) * 0.85, (y0 + y1) / 2 - math.cos(k / 24 * math.pi) * 0.07, cz + H / 2 + 0.012 + math.sin(k / 24 * math.pi) * 0.12)) for k in range(25)]
        tube('lux_tote_handle', pts, 0.0055, M['cognac'], segs=12)
        for p in (pts[0], pts[-1]):
            m = Matrix.Translation(p - Vector((0, 0, 0.012))) @ Matrix.Rotation(math.pi / 2, 4, 'Y'); b2 = bmesh.new(); torus_into(b2, m, 0.009, 0.0022); mesh_obj('lux_tote_ring', b2, M['gold'])
    # contrast edge paint on the rim, gold feet, the star on the front gusset, top stitching
    rim = [Vector((cx + math.copysign(D * 0.5, math.cos(a)) * min(1, abs(math.cos(a)) * 3), hy + math.copysign(W * 0.5, math.sin(a)) * min(1, abs(math.sin(a)) * 3), cz + H / 2)) for a in [k * math.tau / 64 for k in range(64)]]
    tube('lux_tote_edge', rim, 0.0022, M['edge'], closed=True, segs=8)
    for sx in (1, -1):
        for sy in (1, -1):
            lathe('lux_tote_foot', [(0.0, -0.004), (0.006, -0.004), (0.0065, -0.001), (0.004, 0.001)], M['gold'], 16, Matrix.Translation((cx + sx * D * 0.3, hy + sy * W * 0.38, cz - H / 2)))
    emblem('lux_tote_star', Vector((cx + D * 0.5 * 0.85 + 0.004, hy, cz + 0.02)), (1, 0, 0), 0.018, M['goldb'], 0.003)
    emblem('lux_tote_star2', Vector((cx - D * 0.5 * 0.85 - 0.004, hy, cz + 0.02)), (-1, 0, 0), 0.018, M['goldb'], 0.003)
    return dict(attach=(hx, hy, hz), notes='Held in the right hand by both handles; the long side runs front to back beside the leg.')

@item('lux_clutch', 'luxury', 'Satin box clutch with crystal clasp', 'hand_r')
def lux_clutch(B):
    hx, hy, hz = -0.253, -0.073, 0.93
    W, H, D = 0.21, 0.11, 0.045
    c = Vector((hx - 0.005, hy, hz - 0.025))
    body = rbox('lux_clutch_body', (D, W, H), c, M['satin'], r=0.012, segs=5, cuts=4); subsurf(body, 1, 2)
    # the frame: a polished gold rim around the opening, rounded ends
    rim = [c + Vector((math.cos(a) * D * 0.52, math.sin(a) * W * 0.5, H / 2 - 0.004 + 0.002 * math.cos(2 * a))) for a in [k * math.tau / 72 for k in range(72)]]
    for v in rim:
        d = Vector((v.x - c.x, v.y - c.y)); s = max(abs(d.x) / (D * 0.52), abs(d.y) / (W * 0.5)); v.x = c.x + (v.x - c.x) / max(s, 1e-6) * 1.0 if s > 1 else v.x
    tube('lux_clutch_frame', rim, 0.003, M['gold'], closed=True, segs=10)
    # clasp: two crystal-set balls meeting on top
    for s in (1, -1):
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.0065, matrix=Matrix.Translation(c + Vector((s * 0.0048, 0, H / 2 + 0.006)))); mesh_obj('lux_clutch_clasp', bm, M['gold'])
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.004, matrix=Matrix.Translation(c + Vector((s * 0.0062, 0, H / 2 + 0.011)))); mesh_obj('lux_clutch_crystal', bm, M['crystal'], smooth=False)
    # a soft pleat line across the front face (two gathered tucks)
    for s in (1, -1):
        tube('lux_clutch_pleat', [c + Vector((s * (D / 2 + 0.0005), y, -0.02 + 0.004 * math.sin(y * 40))) for y in [(-W / 2 + 0.02) + k * (W - 0.04) / 20 for k in range(21)]], 0.0018, M['satin'], segs=6)
    return dict(attach=(hx, hy, hz), notes='Held flat against the palm in the right hand.')

# ---------------------------------------------------------------- shoes: built on olivia's own feet, so they fit the shared body
def foot_shell(B, side, cut, offset=0.004, thickness=0.0025, name='upper', material=None):
    """The upper of a shoe: olivia's foot, inflated and trimmed to the opening `cut(x, y) -> z`; vertices above it are removed."""
    src = B.parts['feet']; bm = bmesh.new(); bm.from_mesh(src.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x * side <= 0], context='VERTS')
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True, smooth=0.6)
    bm.normal_update()
    for v in bm.verts: v.co += v.normal * offset
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > cut(v.co.x * side, v.co.y) or v.co.z < 0.024], context='VERTS')
    o = mesh_obj(name, bm, material); solidify(o, thickness, -1); subsurf(o, 1, 2); return o

def footprint(B, side, grow=0.006, z_hi=0.05):
    """The outline of olivia's foot seen from below: a convex-ish ring of (x, y) for soles, slightly larger than the foot."""
    vs = [v.co for v in B.parts['feet'].data.vertices if v.co.x * side > 0 and v.co.z < z_hi]
    cx = sum(v.x for v in vs) / len(vs); cy = sum(v.y for v in vs) / len(vs); ring = []
    for k in range(48):
        a = k * math.tau / 48; d = Vector((math.cos(a), math.sin(a)))
        r = max(((v.x - cx) * d.x + (v.y - cy) * d.y) for v in vs if abs(-(v.x - cx) * d.y + (v.y - cy) * d.x) < 0.012)
        ring.append((cx + d.x * (r + grow), cy + d.y * (r + grow)))
    return ring

def sole(name, ring, z0, z1, material, r=0.003, taper=0.0):
    bm = bmesh.new(); cx = sum(p[0] for p in ring) / len(ring); cy = sum(p[1] for p in ring) / len(ring)
    lo = [bm.verts.new((cx + (x - cx) * (1 - taper), cy + (y - cy) * (1 - taper), z0)) for x, y in ring]; hi = [bm.verts.new((x, y, z1)) for x, y in ring]
    bm.faces.new(lo[::-1]); bm.faces.new(hi); n = len(ring)
    for i in range(n): bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    o = mesh_obj(name, bm, material); bevel(o, r, 3, 'ANGLE', 40); return o

def foot_top(B, side, y, offset=0.006):
    """The top of the foot (the instep) at a given y: a ring of points over it, x from inside to outside."""
    vs = [v.co for v in B.parts['feet'].data.vertices if v.co.x * side > 0 and abs(v.co.y - y) < 0.006]
    xs = sorted(vs, key=lambda c: c.x); lo, hi = xs[0].x, xs[-1].x; pts = []
    for k in range(17):
        x = lo + (hi - lo) * k / 16; col = [c for c in vs if abs(c.x - x) < 0.008]; z = max(c.z for c in col) if col else 0.03
        pts.append(Vector((x, y, z + offset)))
    pts[0].z = 0.03; pts[-1].z = 0.03; return pts

def arc_normals(pts):
    """Normals for a ribbon laid across the foot: in the cross-section plane, perpendicular to the arc, pointing out."""
    c = sum(pts, Vector()) / len(pts); out = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized(); n = Vector((0, 1, 0)).cross(t).normalized()
        out.append(n if n.dot(p - Vector((c.x, p.y, c.z - 0.05))) > 0 else -n)
    return out

@item('lux_loafer', 'luxury', 'Calf loafer with a gold bar', 'feet')
def lux_loafer(B):
    for side in (1, -1):
        L = Last(B, side, 0.0035)
        L.upper('lux_loafer_upper', lambda y: 0.068 + 0.004 * max(0.0, y - 0.04) / 0.04 + 2.2 * max(0.0, -0.03 - y) ** 1.3 * 6, M['burgundy'], 0.0022)
        ring = L.footprint(0.0035)
        sole_mesh('lux_loafer_sole', ring, 0.012, ZS + 0.001, M['cognac'], 0.0018, spring=0.004)            # leather sole, edge-trimmed
        heel = [(x, y) for x, y in ring if y > L.heel - 0.075]
        sole_mesh('lux_loafer_heel', heel, 0.004, 0.0125, M['edge'], 0.0012)                                # stacked heel block
        welt = [Vector((x, y, ZS + 0.0012)) for x, y in ring]; tube('lux_loafer_welt', welt, 0.0014, M['cognac'], closed=True, segs=6)
        stitches('lux_loafer_weltst', [w + Vector((0, 0, 0.0012)) for w in welt], 0.0035, 0.0024, 0.00045, M['thread_ivory'], closed=True)
        # the moc apron: a raised seam loop over the forefoot, hand-stitched
        apron = []
        for y in [-0.04 - 0.085 * k / 12 for k in range(13)]: apron.append(L.across(y, 0.24, 0.24, 0.0012, 2)[0])
        tipy = -0.04 - 0.085; front = L.across(tipy - 0.008, 0.24, 0.76, 0.0012, 9)
        for y in [-0.04 - 0.085 * k / 12 for k in range(12, -1, -1)]: front.append(L.across(y, 0.76, 0.76, 0.0012, 2)[0])
        loop = apron + front; tube('lux_loafer_moc', loop, 0.0017, M['burgundy'], segs=8)
        stitches('lux_loafer_mocst', [p + Vector((0, 0, 0.0016)) for p in loop], 0.003, 0.0022, 0.00045, M['thread_dark'])
        # saddle strap across the vamp, with the gold bar on it
        strap = L.across(-0.062, 0.1, 0.9, 0.0026, 21)
        ribbon('lux_loafer_strap', strap, arc_normals(strap), 0.017, 0.0024, M['burgundy'])
        mid = strap[len(strap) // 2]
        bar = rbox('lux_loafer_bar', (0.03, 0.0055, 0.004), mid + Vector((0, 0, 0.0035)), M['gold'], r=0.0018)
        for s_ in (-1, 1):
            bm = bmesh.new(); torus_into(bm, Matrix.Translation(mid + Vector((s_ * 0.0175, 0, 0.0032))) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 0.0042, 0.0012); mesh_obj('lux_loafer_bit', bm, M['gold'])
    return dict(attach=(0, 0, 0), notes='Flat; built on a last taken from olivia\'s own feet.')

@item('lux_ballet', 'luxury', 'Patent ballet flat with a grosgrain bow', 'feet')
def lux_ballet(B):
    for side in (1, -1):
        L = Last(B, side, 0.003)
        L.upper('lux_ballet_upper', lambda y: 0.05 + 0.008 * max(0.0, y - 0.02) / 0.06 + 13 * max(0.0, -0.07 - y) ** 1.3, M['patent'], 0.0016)
        ring = L.footprint(0.002)
        sole_mesh('lux_ballet_sole', ring, 0.0145, ZS + 0.0005, M['calf_lining'] if 'calf_lining' in M else M['edge'], 0.0012, spring=0.003)
        sole_mesh('lux_ballet_tread', ring, 0.0115, 0.015, M['rubberb'], 0.001, taper=0.02, spring=0.003)
        tl = L.topline_path(); tube('lux_ballet_bind', tl, 0.0016, M['grosgrain'], segs=8)
        c = L.across(-0.084, 0.5, 0.5, 0.004, 2)[0]
        for s_ in (1, -1):        # two loops and two tails of grosgrain
            loop = [c + Vector((s_ * (0.011 + 0.009 * math.cos(a)), -0.002 + 0.003 * math.sin(a), 0.004 * math.sin(a) + 0.001)) for a in [k * math.tau / 24 for k in range(24)]]
            ribbon('lux_ballet_bow', loop, [Vector((0, -0.3, 1))] * 24, 0.009, 0.0011, M['grosgrain'], closed=True, round_edge=False)
            tail = [c + Vector((s_ * 0.0028 * k, -0.005 - 0.0016 * k, 0.0006 - 0.0016 * k)) for k in range(6)]
            ribbon('lux_ballet_tail', tail, [Vector((0, -1, 0.5))] * 6, 0.007, 0.0011, M['grosgrain'], round_edge=False)
        rbox('lux_ballet_knot', (0.007, 0.005, 0.008), c + Vector((0, -0.0015, 0.001)), M['grosgrain'], r=0.0018)
    return dict(attach=(0, 0, 0), notes='Flat; built on a last taken from olivia\'s feet.')

@item('mid_sneaker', 'mid', 'Leather low-top sneaker', 'feet')
def mid_sneaker(B):
    for side in (1, -1):
        L = Last(B, side, 0.006)
        def throat(y):          # a narrow lace opening along the top of the instep, dropping to a low collar round the ankle
            if y < -0.066: return 0.5
            open_ = L.top(y) - 0.012 - 0.006 * max(0.0, y + 0.06) / 0.06
            if y < 0.0: return open_
            f = min(1.0, y / 0.032); f = f * f * (3 - 2 * f); return open_ * (1 - f) + 0.086 * f
        L.upper('mid_sneaker_upper', throat, M['ivory'], 0.0025)
        ring = L.footprint(0.004)
        sole_mesh('mid_sneaker_cup', ring, 0.006, ZS + 0.009, M['rubberw'], 0.0035, spring=0.006)           # cupsole wraps up the sides
        groove = [Vector((x, y, 0.02)) for x, y in L.footprint(0.0045)]; tube('mid_sneaker_groove', groove, 0.0012, M['rubberb'], closed=True, segs=6)
        sole_mesh('mid_sneaker_tread', ring, 0.003, 0.007, M['rubberb'], 0.0012, taper=0.015, spring=0.006)
        # suede toe cap and heel counter: the last again, a little larger, cut to the overlay shapes
        T = Last(B, side, 0.0075); T.upper('mid_sneaker_toecap', lambda y: 0.5, M['suede'], 0.0012, yrange=(-1, -0.112))
        H = Last(B, side, 0.0075); H.upper('mid_sneaker_heelcap', lambda y: 0.062, M['suede'], 0.0012, yrange=(0.035, 1))
        # eyestays with eyelets, the tongue, laces crossing between them
        a = [pts[MSTRIP // 2 - 1] for op, pts in L.rows if op and -0.064 < pts[0].y < 0.0]
        b = [pts[MSTRIP // 2] for op, pts in L.rows if op and -0.064 < pts[0].y < 0.0]
        ca = []; cb = []
        for k in range(5):
            i = int(k * (len(a) - 1) / 4); pa = a[i] + (a[i] - b[i]).normalized() * 0.006 + Vector((0, 0, -0.002)); pb = b[i] + (b[i] - a[i]).normalized() * 0.006 + Vector((0, 0, -0.002))
            ca.append(pa); cb.append(pb)
            for p in (pa, pb):
                bm = bmesh.new(); torus_into(bm, Matrix.Translation(p + Vector((0, 0, 0.0018))), 0.0024, 0.0007, 12, 6); mesh_obj('mid_sneaker_eyelet', bm, M['steel'])
        tongue = []
        for op, pts in L.rows:
            y = pts[0].y
            if op and -0.07 < y < 0.012:
                k = min(range(len(L.ys)), key=lambda i: abs(L.ys[i] - y)); r = L.run(L.rings[k]); mid = [p for p in r if p.z > throat(y) - 0.004]
                if len(mid) > 2: tongue.append([p + Vector((0, 0, 0.002 + 0.006 * max(0.0, y) / 0.026)) for p in __import__('shoes')._resample(mid, 9)])
        if len(tongue) > 2:
            bm = bmesh.new(); g = [[bm.verts.new(p) for p in row] for row in tongue]
            for i in range(len(g) - 1):
                for j in range(8): bm.faces.new((g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]))
            t = mesh_obj('mid_sneaker_tongue', bm, M['ivory']); solidify(t, 0.004, 0); subsurf(t, 1, 2)
        for k in range(5):
            m = (ca[k] + cb[k]) / 2 + Vector((0, 0, 0.006))
            tube('mid_sneaker_lace', [ca[k] + Vector((0, 0, 0.002)), m, cb[k] + Vector((0, 0, 0.002))], 0.0021, M['thread_ivory'], segs=8)
            if k < 4:
                m2 = (ca[k] + cb[k + 1]) / 2 + Vector((0, 0, 0.0065))
                tube('mid_sneaker_lace', [ca[k] + Vector((0, 0, 0.0025)), m2, cb[k + 1] + Vector((0, 0, 0.0025))], 0.002, M['thread_ivory'], segs=8)
        # padded collar round the opening and a pull tab at the heel
        tube('mid_sneaker_collar', L.topline_path(), 0.0038, M['ivory'], segs=10)
        back = max(L.topline_path(), key=lambda p: p.y)
        tab = [back + Vector((0, 0.003, -0.022 + 0.03 * k / 6)) for k in range(7)]
        ribbon('mid_sneaker_tab', tab, [Vector((0, 1, 0))] * 7, 0.014, 0.0025, M['suede'])
    return dict(attach=(0, 0, 0), notes='Low-top with a cupsole; built on a last taken from olivia\'s feet.')

@item('mid_slide', 'mid', 'Moulded slide sandal', 'feet')
def mid_slide(B):
    for side in (1, -1):
        L = Last(B, side, 0.005); L.upper('cut', lambda y: 0.5, None)
        ring = L.footprint(0.007); bpy.data.objects.remove(bpy.data.objects['cut'], do_unlink=True)
        bed = sole_mesh('mid_slide_bed', ring, 0.006, 0.025, M['rubberb'], 0.006, spring=0.004)
        rows = [L.across(y, 0.0, 1.0, 0.0, 25) for y in [-0.075 + 0.006 * k for k in range(11)]]
        bm = bmesh.new(); g = [[bm.verts.new(p + Vector((0, 0, 0.001))) for p in row] for row in rows]
        for i in range(len(g) - 1):
            for j in range(24): bm.faces.new((g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]))
        st = mesh_obj('mid_slide_strap', bm, M['rubberb']); solidify(st, 0.007, 1); bevel(st, 0.002, 3, 'ANGLE', 50); subsurf(st, 1, 2)
        top = L.across(-0.045, 0.5, 0.5, 0.0085, 2)[0]
        emblem('mid_slide_star', top, (0, 0, 1), 0.011, M['rubberw'], 0.0015, up=Vector((0, -1, 0)))
    return dict(attach=(0, 0, 0), notes='Slides; one wide moulded strap with a raised star.')

# ---------------------------------------------------------------- luxury jewellery and watch
def wrist(B, side=1):
    c, rx, ry = B.ring('body', 0.965, 0.985, side); c2, _, _ = B.ring('body', 1.0, 1.02, side)
    axis = (c2 - c).normalized(); return c, rx, ry, axis

def band_around(name, c, rx, ry, axis, width, thick, material, grow=0.003, n=64, bulge=0.0):
    s, u = frame(axis, Vector((0, 1, 0))); pts = []; nrm = []
    for k in range(n):
        a = k * math.tau / n; d = s * math.cos(a) * (rx + grow) + u * math.sin(a) * (ry + grow)
        pts.append(c + d); nrm.append(d.normalized())
    return ribbon(name, pts, nrm, width, thick, material, closed=True), s, u

@item('lux_watch', 'luxury', 'Gold dress watch on a leather strap', 'wrist_l')
def lux_watch(B):
    c, rx, ry, axis = wrist(B, 1)
    strap, s, u = band_around('lux_watch_strap', c, rx, ry, axis, 0.018, 0.0032, M['lamb'], 0.0035)
    # the case sits on the outside of the wrist (+X), dial facing out
    out = s if s.x > 0 else -s; up = axis
    X = -up; Y = out.cross(X).normalized(); m = Matrix((X, Y, out)).transposed().to_4x4(); m.translation = c + out * (rx + 0.006)
    lathe('lux_watch_case', [(0.0, -0.002), (0.0165, -0.002), (0.0175, 0.0015), (0.0172, 0.004), (0.0155, 0.0052)], M['gold'], 64, m, cap_bottom=True, cap_top=False)
    lathe('lux_watch_bezel', [(0.0155, 0.0052), (0.0148, 0.0058), (0.0142, 0.0058)], M['gold'], 64, m, cap_bottom=False, cap_top=False)
    dial = lathe('lux_watch_dial', [(0.0142, 0.004)], M['dial_lux'], 64, m, cap_bottom=False, cap_top=True)
    uvl = dial.data.uv_layers.new(); inv = m.inverted()
    for loop in dial.data.loops:
        p = inv @ dial.data.vertices[loop.vertex_index].co; uvl.data[loop.index].uv = (0.5 + p.x / 0.0284, 0.5 + p.y / 0.0284)
    lathe('lux_watch_glass', [(0.0142, 0.0058), (0.012, 0.0068), (0.0, 0.0071)], M['crystal'], 64, m, cap_bottom=False, cap_top=False)
    # hands, crown, lugs
    for L, w, ang in ((0.0085, 0.0011, 0.9), (0.012, 0.0008, -1.2)):
        hm = m @ Matrix.Rotation(ang, 4, 'Z') @ Matrix.Translation((0, L / 2, 0.0046)); rbox('lux_watch_hand', (w, L, 0.0004), hm.translation, M['goldb'], r=0, rot=hm.to_euler())
    cm = m @ Matrix.Translation((0.018, 0, 0.0015)) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
    lathe('lux_watch_crown', [(0.0, 0.0), (0.0022, 0.0), (0.0024, 0.0015), (0.0022, 0.003), (0.0, 0.003)], M['gold'], 20, cm)
    for sy in (1, -1):
        for sx in (1, -1): rbox('lux_watch_lug', (0.003, 0.007, 0.003), (m @ Vector((sx * 0.009, sy * 0.0175, 0.0))), M['gold'], r=0.001, rot=m.to_euler())
    return dict(attach=tuple(c), notes='Left wrist, dial on the outer side of the wrist.')

@item('lux_necklace', 'luxury', 'Gold chain with a star pendant', 'neck')
def lux_necklace(B):
    nc, nrx, nry = B.ring('head', 1.425, 1.455)
    front = B.hit((0, -0.6, 1.345), (0, 1, 0)); fy = front[0].y - 0.004 if front else -0.13
    loop = []
    for i in range(64):
        a = i * math.tau / 64; f = (1 - math.cos(a)) / 2
        loop.append(Vector((math.sin(a) * (nrx + 0.012) * (1 + 0.5 * f), (nc.y + nry + 0.008) * (1 - f) + fy * f, 1.452 * (1 - f ** 1.4) + 1.35 * f ** 1.4)))
    pts, _ = B.drape(loop, 0.003, centre=(0, nc.y, 0)); pts = [p if i % 64 not in range(26, 39) else loop[i] for i, p in enumerate(pts)]
    chain('lux_necklace_chain', pts, 0.006, 0.0007, M['gold'], closed=True)
    p = Vector((0, fy - 0.004, 1.335))
    bm = bmesh.new(); torus_into(bm, Matrix.Translation(p + Vector((0, 0, 0.015))) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.0028, 0.0008); mesh_obj('lux_necklace_bail', bm, M['gold'])
    emblem('lux_necklace_star', p, (0, -1, 0), 0.013, M['gold'], 0.003)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.0028, matrix=Matrix.Translation(p + Vector((0, -0.002, 0)))); mesh_obj('lux_necklace_stone', bm, M['crystal'], smooth=False)
    return dict(attach=(0, nc.y, 1.44), notes='Rests on the collarbone; pendant at the sternum.')

def ear_point(B, side):
    head = B.parts['head']; vs = [v.co for v in head.data.vertices if 1.535 < v.co.z < 1.58 and v.co.x * side > 0]
    e = max(vs, key=lambda c: c.x * side); return Vector((e.x, e.y, 1.548))

@item('lux_hoops', 'luxury', 'Gold hoops with pearl drops', 'ears')
def lux_hoops(B):
    for side in (1, -1):
        e = ear_point(B, side) + Vector((side * 0.002, 0.004, 0))
        m = Matrix.Translation(e + Vector((0, 0, -0.016))) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
        bm = bmesh.new(); torus_into(bm, m, 0.016, 0.0016, seg=40, rseg=10); mesh_obj('lux_hoops_hoop', bm, M['gold'])
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=0.0042, matrix=Matrix.Translation(e + Vector((0, 0, -0.037)))); mesh_obj('lux_hoops_pearl', bm, M['pearl'])
        lathe('lux_hoops_cap', [(0.0, 0.0), (0.0018, 0.0), (0.0026, -0.0018), (0.0, -0.0018)], M['gold'], 16, Matrix.Translation(e + Vector((0, 0, -0.031))))
    return dict(attach=tuple(ear_point(B, 1)), notes='Hoops through both lobes, pearls hanging inside the hoop.')

@item('lux_bangle', 'luxury', 'Gold cuff with malachite inlay', 'wrist_r')
def lux_bangle(B):
    c, rx, ry, axis = wrist(B, -1); c = c + axis * 0.012
    o, s, u = band_around('lux_bangle_cuff', c, rx + 0.004, ry + 0.004, axis, 0.022, 0.003, M['gold'], 0.004)
    subsurf(o, 1, 2)
    pts = []; nrm = []
    for k in range(40):           # the inlay: a malachite strip round the outer half
        a = -1.2 + 2.4 * k / 39; d = s * math.cos(a) * (rx + 0.0105) + u * math.sin(a) * (ry + 0.0105); pts.append(c + d); nrm.append(d.normalized())
    ribbon('lux_bangle_inlay', pts, nrm, 0.010, 0.0012, M['malachite'])
    return dict(attach=tuple(c), notes='Right wrist, open cuff.')

@item('lux_belt', 'luxury', 'Calf belt with a star buckle', 'waist')
def lux_belt(B):
    pts, nrm = B.waist(1.0, 0.012)
    ribbon('lux_belt_strap', pts, [Vector((n.x, n.y, 0)).normalized() for n in nrm], 0.03, 0.0035, M['lamb'], closed=True)
    f = pts[0]
    frame_pts = [Vector((math.cos(a) * 0.024, 0, math.sin(a) * 0.019)) for a in [k * math.tau / 40 for k in range(40)]]
    tube('lux_belt_buckle', [f + Vector((0, -0.006, 0)) + p for p in frame_pts], 0.0024, M['gold'], closed=True)
    emblem('lux_belt_star', f + Vector((0, -0.009, 0)), (0, -1, 0), 0.015, M['gold'], 0.003)
    stitches('lux_belt_stitch', [p + Vector((0, 0, 0.011)) + Vector((n.x, n.y, 0)).normalized() * 0.0018 for p, n in zip(pts, nrm)], 0.005, 0.0035, 0.0006, M['thread_ivory'], closed=True)
    stitches('lux_belt_stitch2', [p + Vector((0, 0, -0.011)) + Vector((n.x, n.y, 0)).normalized() * 0.0018 for p, n in zip(pts, nrm)], 0.005, 0.0035, 0.0006, M['thread_ivory'], closed=True)
    return dict(attach=(0, f.y, 1.0), notes='At the waist, over the trousers or skirt.')

# ---------------------------------------------------------------- hats and the scarf
HEAD_C = Vector((0, -0.03, 1.62))
def hat_fit(B):
    """Centre and radii of the hat band: the head plus hair at band height, so it sits over the hair."""
    vs = [v.co for k in ('head', 'hair') for v in B.parts[k].data.vertices if 1.60 < v.co.z < 1.66]
    xs = [v.x for v in vs]; ys = [v.y for v in vs]
    return Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 1.635)), (max(xs) - min(xs)) / 2 + 0.006, (max(ys) - min(ys)) / 2 + 0.004

def oval_lathe(name, prof, rx, ry, material, centre, segs=64, tilt=0.0, pinch=None, cap=True):
    """A lathe whose cross-sections are ovals (rx, ry scale the unit profile radius)."""
    bm = bmesh.new(); rings = []
    rot = Matrix.Rotation(tilt, 4, 'X')
    for r, z in prof:
        ring = []
        for k in range(segs):
            a = k * math.tau / segs; x, y = math.cos(a) * r * rx, math.sin(a) * r * ry; zz = z
            if pinch: zz += pinch(a, r, z)
            ring.append(bm.verts.new(centre + (rot @ Vector((x, y, zz)))))
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(segs): bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    if cap and prof[-1][0] > 1e-5: bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); return mesh_obj(name, bm, material)

@item('lux_fedora', 'luxury', 'Wide-brim felt fedora', 'head')
def lux_fedora(B):
    c, rx, ry = hat_fit(B); tilt = math.radians(-4)
    # brim (rolled up slightly at the sides), crown with a centre dent and front pinches
    brim = oval_lathe('lux_fedora_brim', [(1.0, 0.0), (1.4, -0.002), (1.85, 0.0)], rx, ry, M['felt'], c, 96, tilt,
                      pinch=lambda a, r, z: (0.012 * abs(math.cos(a)) ** 2 - 0.006 * max(0, -math.sin(a))) * max(0, (r - 1.0) / 0.85), cap=False)
    solidify(brim, 0.003, 0); bevel(brim, 0.001, 2, 'ANGLE', 60)
    prof = [(1.0, 0.0), (1.0, 0.03), (0.98, 0.07), (0.92, 0.10), (0.75, 0.118), (0.45, 0.112), (0.2, 0.10), (0.0, 0.096)]
    dent = lambda a, r, z: -0.022 * max(0, 1 - r / 0.55) ** 1.5 - 0.016 * max(0, -math.sin(a)) ** 6 * max(0, (z - 0.06) / 0.06) * (1 - abs(math.cos(a)) * 0.4)
    crown = oval_lathe('lux_fedora_crown', prof, rx, ry, M['felt'], c, 96, tilt, pinch=dent); solidify(crown, 0.0025, 1); subsurf(crown, 1, 2)
    band = oval_lathe('lux_fedora_band', [(1.012, 0.004), (1.010, 0.030)], rx, ry, M['grosgrain'], c, 96, tilt, cap=False); solidify(band, 0.0015, 1)
    # the band's bow on the left side
    bp = c + Matrix.Rotation(tilt, 4, 'X') @ Vector((rx * 1.02, -0.005, 0.017))
    rbox('lux_fedora_bow', (0.004, 0.022, 0.024), bp, M['grosgrain'], r=0.0015)
    return dict(attach=tuple(c), notes='Sits over the hair at the band line; tilted slightly forward.')

@item('lux_scarf', 'luxury', 'Silk carre tied at the neck (adire print)', 'neck')
def lux_scarf(B):
    nc, nrx, nry = B.ring('head', 1.43, 1.46)
    pts = []; nrm = []
    for k in range(64):            # a twisted roll round the neck, knotted at the front left
        a = k * math.tau / 64; d = Vector((math.sin(a) * (nrx + 0.013), -math.cos(a) * (nry + 0.013), 0))
        pts.append(Vector((nc.x, nc.y, 1.437 - 0.012 * (1 - math.cos(a)) / 2)) + d); nrm.append(d.normalized())
    r = [0.0085 + 0.002 * math.sin(k * 0.9) for k in range(64)]
    tube('lux_scarf_roll', pts, 0.009, M['silk'], closed=True, segs=12, radii=r)
    knot = Vector((0.03, nc.y - nry - 0.02, 1.43))
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=10, radius=1, matrix=Matrix.Translation(knot) @ Matrix.Diagonal((0.016, 0.012, 0.014, 1))); kn = mesh_obj('lux_scarf_knot', bm, M['silk'])
    for s, ang in ((1, 0.35), (-1, -0.15)):         # two pointed tails falling from the knot
        bm = bmesh.new(); n = 12; rows = []
        for j in range(n + 1):
            t = j / n; w = 0.03 * (1 - t) + 0.002; zc = knot.z - 0.006 - 0.085 * t; xc = knot.x + s * 0.012 * t + math.sin(t * 3) * 0.004
            row = [bm.verts.new((xc + w * (i / 4 - 0.5) * math.cos(ang), knot.y - 0.004 - 0.01 * t + w * (i / 4 - 0.5) * math.sin(ang) + 0.003 * math.sin(i * 1.7 + t * 4), zc)) for i in range(5)]
            rows.append(row)
        for j in range(n):
            for i in range(4): bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
        o = mesh_obj('lux_scarf_tail', bm, M['silk']); solidify(o, 0.0012, 0); subsurf(o, 1, 2)
    return dict(attach=(0, nc.y, 1.44), notes='Silk neckerchief, knotted at the front left.')

@item('lux_aviator', 'luxury', 'Gold aviators with gradient lenses', 'eyes')
def lux_aviator(B):
    eyes = B.parts['eyes']; ez = sum(v.co.z for v in eyes.data.vertices) / len(eyes.data.vertices)
    front = min(v.co.y for v in B.parts['head'].data.vertices if abs(v.co.x) < 0.04 and abs(v.co.z - ez) < 0.01)
    ly = min(v.co.y for v in eyes.data.vertices) - 0.017
    for side in (1, -1):        # teardrop lenses
        cx = side * 0.034; outline = []
        for k in range(48):
            a = k * math.tau / 48; x = math.cos(a) * 0.026; z = math.sin(a) * (0.021 if math.sin(a) > 0 else 0.025)
            x *= 1 - 0.18 * max(0, -math.sin(a)) * (1 if math.cos(a) * side > 0 else 0) - 0.05 * max(0, -math.sin(a))
            outline.append(Vector((cx + x - side * 0.002 * max(0, -math.sin(a)), ly + 0.004 * (x / 0.026) ** 2 * 0, ez + z - 0.004)))
        lens = mesh_obj('lux_aviator_lens', bmesh.new(), M['lens_grad'])
        bm = bmesh.new(); vs = [bm.verts.new(p) for p in outline]; bm.faces.new(vs); bm.to_mesh(lens.data); bm.free(); solidify(lens, 0.0015, 0)
        tube('lux_aviator_rim', outline, 0.0011, M['gold'], closed=True, segs=8)
        # temple arm back to the ear
        tube('lux_aviator_temple', [Vector((side * 0.06, ly + 0.002, ez + 0.012)), Vector((side * 0.074, ly + 0.03, ez + 0.012)), Vector((side * 0.083, ly + 0.08, ez + 0.006)), Vector((side * 0.085, ly + 0.11, ez - 0.012))], 0.0012, M['gold'], segs=8)
        tube('lux_aviator_tip', [Vector((side * 0.085, ly + 0.098, ez - 0.006)), Vector((side * 0.084, ly + 0.118, ez - 0.022))], 0.0022, M['tort'], segs=8)
        rbox('lux_aviator_pad', (0.004, 0.003, 0.008), (side * 0.011, ly + 0.012, ez - 0.012), M['crystal'], r=0.0012)
    tube('lux_aviator_bridge', [Vector((-0.012, ly, ez + 0.012)), Vector((0, ly - 0.002, ez + 0.014)), Vector((0.012, ly, ez + 0.012))], 0.0012, M['gold'], segs=8)
    tube('lux_aviator_brow', [Vector((-0.052, ly, ez + 0.014)), Vector((-0.02, ly - 0.001, ez + 0.019)), Vector((0, ly - 0.002, ez + 0.019)), Vector((0.02, ly - 0.001, ez + 0.019)), Vector((0.052, ly, ez + 0.014))], 0.0011, M['gold'], segs=8)
    return dict(attach=(0, ly, ez), notes='Over the eyes, temples to the ears.')

# ---------------------------------------------------------------- mid-range bags
@item('mid_tote', 'mid', 'Canvas tote with leather trim', 'hand_r')
def mid_tote(B):
    hx, hy, hz = -0.253, -0.073, 0.93                    # olivia's right hand
    W, H, D = 0.34, 0.30, 0.09                            # the long side runs front to back beside the leg
    cx, cy, cz = hx - 0.03, hy, hz - 0.13 - H / 2
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=10, use_grid_fill=True)
    for v in bm.verts:            # soft canvas: the gussets fold in towards the top, the base sags a little
        x, y, z = v.co
        v.co = Vector((x * D * (1 - 0.45 * (1 - (2 * y) ** 2) * max(0, z + 0.3)), y * W, z * H - 0.008 * (1 - (2 * y) ** 2) * (z < -0.45)))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z > 0.9 and f.calc_center_median().z > H / 2 - 1e-4], context='FACES')
    body = mesh_obj('mid_tote_body', bm, M['canvas']); body.location = (cx, cy, cz); apply_xf(body); solidify(body, 0.002); subsurf(body, 1, 2)
    for side in (1, -1):          # flat leather handles from each face up into the hand
        xs = cx + side * D * 0.3; pts = []; nrm = []
        for k in range(21):
            t = k / 20; a = t * math.pi; f = math.sin(a)
            pts.append(Vector((xs + (hx - xs) * f, cy - math.cos(a) * 0.075, cz + H / 2 - 0.03 + f * 0.155))); nrm.append(Vector((side, 0, 0)))
        ribbon('mid_tote_handle', pts, nrm, 0.022, 0.003, M['cognac'])
        for p in (pts[0], pts[-1]): rbox('mid_tote_tab', (0.004, 0.03, 0.05), p + Vector((side * 0.001, 0, -0.005)), M['cognac'], r=0.0015)
    for sy in (1, -1):            # leather corners at the base
        rbox('mid_tote_corner', (D * 0.75, 0.05, 0.05), (cx, cy + sy * (W / 2 - 0.022), cz - H / 2 + 0.022), M['cognac'], r=0.004)
    rim = [Vector((cx + math.cos(t) * D * 0.5 * 0.8, cy + math.sin(t) * W * 0.5, cz + H / 2)) for t in [k * math.tau / 64 for k in range(64)]]
    for v in rim: v.y = cy + max(-W / 2, min(W / 2, (v.y - cy) * 1.6))
    tube('mid_tote_hem', rim, 0.003, M['canvas'], closed=True, segs=8)
    return dict(attach=(hx, hy, hz), notes='Held in the right hand by both handles.')

@item('mid_crossbody', 'mid', 'Nylon camera bag', 'crossbody')
def mid_crossbody(B):
    W, H, D = 0.19, 0.13, 0.065; cx, cz = 0.12, 0.88
    h = B.hit((cx, -0.6, cz), (0, 1, 0)); cy = (h[0].y if h else -0.13) - D / 2 - 0.006
    body = rbox('mid_crossbody_body', (W, D, H), (cx, cy, cz), M['nylon'], r=0.018, segs=5, cuts=3); subsurf(body, 1, 2)
    # zip track along the top and a puller, a front zip pocket, leather tab with the brand badge
    top = [Vector((cx + x, cy, cz + H / 2 + 0.003)) for x in [(-W / 2 + 0.01) + k * (W - 0.02) / 30 for k in range(31)]]
    ribbon('mid_crossbody_zip', top, [Vector((0, 0, 1))] * 31, 0.008, 0.002, M['rubberb'], round_edge=False)
    rbox('mid_crossbody_puller', (0.008, 0.004, 0.022), (cx + 0.05, cy - 0.008, cz + H / 2 - 0.004), M['steel'], r=0.0015)
    pocket = [Vector((cx + x, cy - D / 2 - 0.002, cz + 0.005)) for x in [(-W / 2 + 0.025) + k * (W - 0.05) / 24 for k in range(25)]]
    ribbon('mid_crossbody_pzip', pocket, [Vector((0, -1, 0))] * 25, 0.006, 0.0015, M['rubberb'], round_edge=False)
    rbox('mid_crossbody_tab', (0.03, 0.003, 0.03), (cx - 0.05, cy - D / 2 - 0.003, cz - 0.03), M['cognac'], r=0.002)
    emblem('mid_crossbody_star', Vector((cx - 0.05, cy - D / 2 - 0.0055, cz - 0.03)), (0, -1, 0), 0.009, M['silver'], 0.0015)
    # webbing strap: from the top of the bag across the chest, over the opposite shoulder, down the back and round the hip to the bag's far end
    pts, nrm = B.sash(-0.14, Vector((cx - W / 2 + 0.012, cy, cz + H / 2 - 0.004)), Vector((cx + W / 2 - 0.002, cy, cz + H / 2 - 0.012)), Vector((cx + W / 2 + 0.03, 0.02, cz + 0.05)), 0.028)
    ribbon('mid_crossbody_strap', pts, nrm, 0.024, 0.0025, M['webbing'])
    return dict(attach=(cx, cy, cz), notes='Crossbody: strap over the right shoulder, bag at the left front hip.')

@item('mid_backpack', 'mid', 'Mini nylon backpack', 'back')
def mid_backpack(B):
    W, H, D = 0.24, 0.30, 0.11; cx, cz = 0.0, 1.20
    h = B.hit((cx, 0.6, cz), (0, -1, 0)); cy = (h[0].y if h else 0.1) + D / 2 + 0.008
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=8, use_grid_fill=True)
    for v in bm.verts:
        x, y, z = v.co; top = z + 0.5; dome = 1 - (2 * x) ** 2
        v.co = Vector((x * W * (1 - 0.15 * top ** 3), y * D * (1 - 0.25 * top ** 2) + 0.012 * (y > 0) * dome * (1 - (2 * z) ** 2), z * H + 0.03 * max(0, z) * dome * 2))
    body = mesh_obj('mid_backpack_body', bm, M['nylon']); body.location = (cx, cy, cz); apply_xf(body); bevel(body, 0.01, 3, 'ANGLE', 35); subsurf(body, 1, 2)
    # front pocket, zip arc, grab loop, shoulder straps down to the lower corners
    rbox('mid_backpack_pocket', (W * 0.7, 0.03, H * 0.38), (cx, cy + D / 2 + 0.012, cz - H * 0.2), M['nylon'], r=0.012, segs=4, cuts=2)
    arc = [Vector((cx + math.cos(a) * W * 0.46, cy, cz + math.sin(a) * H * 0.5 + 0.0)) for a in [math.pi * k / 30 for k in range(31)]]
    arc, nrm = [p + Vector((0, 0, 0.012 * math.sin(math.pi * i / 30))) for i, p in enumerate(arc)], None
    tube('mid_backpack_zip', arc, 0.0025, M['rubberb'], segs=6)
    tube('mid_backpack_loop', [Vector((cx - 0.025, cy - 0.01, cz + H / 2 + 0.02)), Vector((cx, cy - 0.01, cz + H / 2 + 0.05)), Vector((cx + 0.025, cy - 0.01, cz + H / 2 + 0.02))], 0.004, M['webbing'], segs=8)
    for side in (1, -1):
        path = [Vector((side * 0.06, cy - D / 2, cz + H / 2 - 0.02)), Vector((side * 0.11, 0.03, 1.36)), Vector((side * 0.12, -0.03, 1.41)), Vector((side * 0.125, -0.085, 1.34)),
                Vector((side * 0.14, -0.11, 1.25)), Vector((side * 0.16, -0.05, 1.14)), Vector((side * 0.13, cy - D / 2, cz - H / 2 + 0.03))]
        fine = []
        for a, b in zip(path[:-1], path[1:]): fine += [a.lerp(b, k / 5) for k in range(5)]
        fine.append(path[-1]); mid_pts, nrm = B.lay(fine[1:-1], 0.024)
        pts, nrm = B.taut([fine[0]] + mid_pts + [fine[-1]], [Vector((0, 1, 0))] + nrm + [Vector((0, 1, 0))])
        ribbon('mid_backpack_strap', pts, nrm, 0.04, 0.006, M['nylon'])
    emblem('mid_backpack_star', Vector((cx, cy + D / 2 + 0.028, cz - H * 0.15)), (0, 1, 0), 0.012, M['silver'], 0.0015)
    return dict(attach=(0, cy, cz), notes='On the back, straps over both shoulders and under the arms.')

# ---------------------------------------------------------------- mid-range watch, eyewear, jewellery, belt, hats
@item('mid_watch', 'mid', 'Steel field watch on a woven strap', 'wrist_l')
def mid_watch(B):
    c, rx, ry, axis = wrist(B, 1)
    strap, s, u = band_around('mid_watch_strap', c, rx, ry, axis, 0.018, 0.0022, M['webbing'], 0.003)
    out = s if s.x > 0 else -s; up = axis
    X = -up; Y = out.cross(X).normalized(); m = Matrix((X, Y, out)).transposed().to_4x4(); m.translation = c + out * (rx + 0.006)
    lathe('mid_watch_case', [(0.0, -0.002), (0.0185, -0.002), (0.0195, 0.002), (0.019, 0.006), (0.0172, 0.0068)], M['steel'], 48, m, cap_top=False)
    dial = lathe('mid_watch_dial', [(0.0168, 0.005)], M['dial_field'], 48, m, cap_bottom=False)
    uvl = dial.data.uv_layers.new(); inv = m.inverted()
    for loop in dial.data.loops:
        p = inv @ dial.data.vertices[loop.vertex_index].co; uvl.data[loop.index].uv = (0.5 + p.x / 0.0336, 0.5 + p.y / 0.0336)
    lathe('mid_watch_glass', [(0.0172, 0.0068), (0.0, 0.0074)], M['crystal'], 48, m, cap_bottom=False, cap_top=False)
    for L, w, ang in ((0.01, 0.0016, 0.5), (0.014, 0.0012, -2.2)):
        hm = m @ Matrix.Rotation(ang, 4, 'Z') @ Matrix.Translation((0, L / 2, 0.0056)); rbox('mid_watch_hand', (w, L, 0.0005), hm.translation, M['thread_ivory'], r=0, rot=hm.to_euler())
    cm = m @ Matrix.Translation((0.02, 0, 0.002)) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
    lathe('mid_watch_crown', [(0.0, 0.0), (0.0028, 0.0), (0.003, 0.002), (0.0028, 0.0035), (0.0, 0.0035)], M['steel'], 16, cm)
    return dict(attach=tuple(c), notes='Left wrist; pass-through woven strap.')

@item('mid_sunglasses', 'mid', 'Square tortoiseshell sunglasses', 'eyes')
def mid_sunglasses(B):
    eyes = B.parts['eyes']; ez = sum(v.co.z for v in eyes.data.vertices) / len(eyes.data.vertices)
    ly = min(v.co.y for v in eyes.data.vertices) - 0.017
    bm_lens = []
    for side in (1, -1):
        cx = side * 0.034; w, h, r = 0.05, 0.04, 0.012
        outline = []
        for k in range(64):
            a = k * math.tau / 64; x = math.cos(a); z = math.sin(a)
            sq = 1 / max(abs(x), abs(z)) ** 0.75; x *= sq; z *= sq                     # a softened square
            outline.append((cx + x * w / 2 * 0.93, ez + z * h / 2 * 0.93 + 0.001 * x * side))
        m = Matrix.Translation((0, ly, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'X')
        fr = outline_prism('mid_sunglasses_frame', [(x, z) for x, z in outline], 0.006, M['tort'], m, r=0.0015)
        inner = [(cx + (x - cx) * 0.84, ez + (z - ez) * 0.82) for x, z in outline]
        lens = outline_prism('mid_sunglasses_lens', inner, 0.0018, M['lens_dark'], Matrix.Translation((0, ly - 0.0035, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        bool_ = fr.modifiers.new('Cut', 'BOOLEAN'); cutter = outline_prism('cutter', inner, 0.02, None, m); bool_.object = cutter; bool_.operation = 'DIFFERENCE'
        select([fr]); bpy.ops.object.modifier_apply(modifier='Cut'); bpy.data.objects.remove(cutter, do_unlink=True)
        tube('mid_sunglasses_temple', [Vector((side * 0.06, ly + 0.003, ez + 0.012)), Vector((side * 0.074, ly + 0.03, ez + 0.012)), Vector((side * 0.083, ly + 0.08, ez + 0.006)), Vector((side * 0.085, ly + 0.11, ez - 0.014))], 0.0024, M['tort'], segs=8)
        for zz in (0.012,):
            bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=0.0012, matrix=Matrix.Translation((side * 0.058, ly - 0.0035, ez + zz))); mesh_obj('mid_sunglasses_pin', bm, M['silver'])
    rbox('mid_sunglasses_bridge', (0.014, 0.006, 0.006), (0, ly, ez + 0.008), M['tort'], r=0.002)
    return dict(attach=(0, ly, ez), notes='Over the eyes.')

@item('mid_beads', 'mid', 'Stacked bead bracelets', 'wrist_r')
def mid_beads(B):
    c, rx, ry, axis = wrist(B, -1); s, u = frame(axis, Vector((0, 1, 0)))
    palettes = [('bead_coral', 'bead_cream'), ('bead_amber', 'bead_black'), ('bead_teal', 'bead_cream')]
    for row, (ma, mb) in enumerate(palettes):
        cc = c + axis * (0.004 + row * 0.0085); n = 26
        for k in range(n):
            a = k * math.tau / n; d = s * math.cos(a) * (rx + 0.0075) + u * math.sin(a) * (ry + 0.0075)
            mat = M[ma] if (k + row) % 5 else M[mb]
            bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=0.0038 + 0.0004 * math.sin(k * 2.3), matrix=Matrix.Translation(cc + d)); mesh_obj('mid_beads_bead', bm, mat)
        tube('mid_beads_cord', [cc + s * math.cos(a) * (rx + 0.0075) + u * math.sin(a) * (ry + 0.0075) for a in [k * math.tau / 48 for k in range(48)]], 0.0007, M['elastic'], closed=True, segs=6)
    return dict(attach=tuple(c), notes='Three strands on the right wrist.')

@item('mid_studs', 'mid', 'Silver star studs and a cuff', 'ears')
def mid_studs(B):
    for side in (1, -1):
        e = ear_point(B, side) + Vector((side * 0.0015, -0.002, 0.0))
        emblem('mid_studs_star', e, (side, -0.4, 0), 0.0055, M['silver'], 0.0018)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.0016, matrix=Matrix.Translation(e + Vector((side * 0.0014, -0.0006, 0)))); mesh_obj('mid_studs_stone', bm, M['crystal'], smooth=False)
        if side == 1:     # an ear cuff higher up the left ear
            m = Matrix.Translation(e + Vector((0.0, 0.006, 0.026))) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
            bm = bmesh.new(); torus_into(bm, m, 0.0045, 0.0009, 24, 6); mesh_obj('mid_studs_cuff', bm, M['silver'])
    return dict(attach=tuple(ear_point(B, 1)), notes='Studs in both lobes, a cuff on the left helix.')

@item('mid_belt', 'mid', 'Webbing belt with a roller buckle', 'waist')
def mid_belt(B):
    pts, nrm = B.waist(1.0, 0.012)
    ribbon('mid_belt_strap', pts, [Vector((n.x, n.y, 0)).normalized() for n in nrm], 0.032, 0.0025, M['webbing'], closed=True)
    f = pts[0] + Vector((0.02, -0.006, 0))
    frame_pts = [f + Vector((x, 0, z)) for x, z in ((-0.012, -0.019), (0.012, -0.019), (0.012, 0.019), (-0.012, 0.019))]
    tube('mid_belt_buckle', frame_pts, 0.0022, M['steel'], closed=True, segs=8)
    tube('mid_belt_roller', [f + Vector((-0.012, -0.002, -0.017)), f + Vector((-0.012, -0.002, 0.017))], 0.0032, M['steel'], segs=10)
    rbox('mid_belt_tip', (0.02, 0.004, 0.032), pts[0] + Vector((-0.035, -0.0045, 0)), M['steel'], r=0.0015)
    return dict(attach=(0, pts[0].y, 1.0), notes='At the waist.')

@item('mid_cap', 'mid', 'Six-panel cotton cap', 'head')
def mid_cap(B):
    c, rx, ry = hat_fit(B); c = c + Vector((0, 0, -0.004))
    prof = [(1.0, 0.0), (1.0, 0.02), (0.96, 0.05), (0.85, 0.08), (0.65, 0.102), (0.35, 0.115), (0.0, 0.118)]
    crown = oval_lathe('mid_cap_crown', prof, rx, ry, M['navy'], c, 96); solidify(crown, 0.002, 1); subsurf(crown, 1, 2)
    for k in range(6):         # panel seams from the band to the button
        a = k * math.tau / 6 + math.pi / 6
        seam = [c + Vector((math.cos(a) * r * rx * 1.008, math.sin(a) * r * ry * 1.008, z + 0.0015)) for r, z in prof[1:]]
        tube('mid_cap_seam', seam, 0.0012, M['navy'], segs=6)
    lathe('mid_cap_button', [(0.0, 0.0), (0.008, 0.0), (0.0085, 0.003), (0.0, 0.006)], M['navy'], 20, Matrix.Translation(c + Vector((0, 0, 0.116))))
    # curved peak to the front (-Y)
    bm = bmesh.new(); rows = []
    for j in range(9):
        t = j / 8; row = []
        for i in range(17):
            a = math.pi * (0.12 + 0.76 * i / 16); x = math.cos(a) * rx * 1.0; yb = -math.sin(a) * ry * 1.0
            y = yb - t * 0.075 * math.sin(a); z = 0.004 - 0.012 * t * t - 0.012 * (math.cos(a)) ** 2 * t
            row.append(bm.verts.new(c + Vector((x * (1 - 0.1 * t), y, z))))
        rows.append(row)
    for j in range(8):
        for i in range(16): bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    peak = mesh_obj('mid_cap_peak', bm, M['navy']); solidify(peak, 0.0035, 0); bevel(peak, 0.0012, 2, 'ANGLE', 60)
    emblem('mid_cap_star', c + Vector((0, -ry * 0.97 - 0.002, 0.045)), (0, -1, 0.25), 0.014, M['thread_ivory'], 0.0012)
    return dict(attach=tuple(c), notes='Over the hair, peak to the front.')

@item('mid_bucket', 'mid', 'Cotton twill bucket hat', 'head')
def mid_bucket(B):
    c, rx, ry = hat_fit(B)
    prof = [(1.55, -0.045), (1.38, -0.03), (1.15, -0.008), (1.02, 0.0), (1.0, 0.04), (0.97, 0.075), (0.88, 0.095), (0.5, 0.104), (0.0, 0.106)]
    hat = oval_lathe('mid_bucket_hat', prof, rx, ry, M['khaki'], c, 96, pinch=lambda a, r, z: 0.004 * math.sin(a * 5) * max(0, r - 1.05))
    solidify(hat, 0.0025, 1); subsurf(hat, 1, 2)
    for r_, z_ in ((1.47, -0.039), (1.33, -0.027), (1.2, -0.016)):   # rows of top stitching round the brim
        tube('mid_bucket_stitch', [c + Vector((math.cos(a) * r_ * rx, math.sin(a) * r_ * ry, z_ + 0.0018 + 0.004 * math.sin(a * 5) * max(0, r_ - 1.05))) for a in [k * math.tau / 96 for k in range(96)]], 0.0005, M['thread_ivory'], closed=True, segs=4)
    for k in range(2):
        a = math.pi * (0.25 + k * 0.5)
        bm = bmesh.new(); torus_into(bm, Matrix.Translation(c + Vector((math.cos(a) * rx * 0.985, math.sin(a) * ry * 0.985, 0.06))) @ Matrix.Rotation(math.pi / 2, 4, Vector((-math.sin(a), math.cos(a), 0))), 0.003, 0.0009, 12, 6); mesh_obj('mid_bucket_eyelet', bm, M['steel'])
    return dict(attach=tuple(c), notes='Over the hair, brim turned down.')

@item('mid_chain', 'mid', 'Silver curb chain', 'neck')
def mid_chain(B):
    nc, nrx, nry = B.ring('head', 1.425, 1.455)
    front = B.hit((0, -0.6, 1.39), (0, 1, 0)); fy = front[0].y - 0.006 if front else -0.12
    loop = [Vector((math.sin(a) * (nrx + 0.014) * (1 + 0.25 * (1 - math.cos(a)) / 2), (nc.y + nry + 0.008) * (1 - (1 - math.cos(a)) / 2) + fy * (1 - math.cos(a)) / 2, 1.452 - 0.06 * ((1 - math.cos(a)) / 2) ** 1.4)) for a in [k * math.tau / 64 for k in range(64)]]
    pts, _ = B.drape(loop, 0.004, centre=(0, nc.y, 0))
    chain('mid_chain_chain', pts, 0.011, 0.0016, M['silver'], closed=True, curb=True)
    return dict(attach=(0, nc.y, 1.44), notes='A short curb chain at the base of the neck.')

# ======================================================================================================== export and renders
def tris(o):
    dg = bpy.context.evaluated_depsgraph_get(); e = o.evaluated_get(dg); me = e.to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); e.to_mesh_clear(); return n

def uv_all(objs):
    """World-scale box UVs (one texture tile = 6 cm; silk prints 24 cm) for every mesh that has none, so the tiling textures read at true size."""
    for o in objs:
        if o.data.uv_layers: continue
        select([o]); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=0.24 if 'silk' in (o.data.materials[0].name.lower() if o.data.materials else '') else 0.06, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')

def game_copy(objs, name, view_levels=True):
    """One joined, modifier-applied mesh for the game (subdivision at its viewport level)."""
    dg = bpy.context.evaluated_depsgraph_get(); parts = []
    for o in objs:
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg)); c = link(bpy.data.objects.new(o.name + '_g', me)); c.matrix_world = o.matrix_world; parts.append(c)
        at = me.attributes.new('trim', 'INT', 'FACE'); at.data.foreach_set('value', [1 if o.get('trim') else 0] * len(me.polygons))
    select(parts); bpy.ops.object.join(); g = bpy.context.view_layer.objects.active; g.name = g.data.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True); return g

def lod(src, name, ratio):
    c = src.copy(); c.data = src.data.copy(); c.name = c.data.name = name; link(c)
    m = c.modifiers.new('Decimate', 'DECIMATE'); m.ratio = ratio; m.use_collapse_triangulate = True
    select([c]); bpy.ops.object.modifier_apply(modifier='Decimate'); return c

def studio(scene):
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = scene.render.resolution_y = 640; scene.render.film_transparent = False
    try: scene.eevee.taa_render_samples = 48
    except Exception: pass
    w = bpy.data.worlds.new('studio'); scene.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.82, 0.8, 0.78, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    scene.view_settings.view_transform = 'AgX'
    for name, loc, energy, size in (('key', (1.2, -1.6, 2.4), 260, 1.2), ('fill', (-1.8, -1.0, 1.4), 90, 2.0), ('rim', (0.4, 1.8, 2.2), 180, 1.0)):
        L = bpy.data.lights.new(name, 'AREA'); L.energy = energy; L.size = size; o = link(bpy.data.objects.new(name, L)); o.location = loc; o['dir'] = Vector(loc).normalized()
        o.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return scene

def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

def shoot(scene, objs, path, direction, pad=1.25, lens=85):
    lo, hi = bounds(objs); c = (lo + hi) / 2; size = max((hi - lo).length, 0.05)
    cam_d = bpy.data.cameras.new('cam'); cam_d.lens = lens; cam = link(bpy.data.objects.new('cam', cam_d)); scene.camera = cam
    dist = size * pad / (2 * math.tan(cam_d.angle / 2)); d = Vector(direction).normalized(); cam.location = c + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); cam_d.clip_start = 0.005; cam_d.clip_end = 50
    for L in [o for o in scene.objects if o.type == 'LIGHT' and 'dir' in o]:       # lights stay 3 m away, so close-ups are exposed like full-body shots
        d0 = Vector(L['dir']); L.location = c + d0 * 3.0; L.rotation_euler = (-d0).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path; bpy.ops.render.render(write_still=True); bpy.data.objects.remove(cam, do_unlink=True)

def principled(m): return next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if m and m.use_nodes else None
def is_alpha(m):
    b = principled(m); return b is not None and (m.surface_render_method == 'BLENDED' or b.inputs['Transmission Weight'].default_value > 0.5)
def metal_of(m):
    b = principled(m); return b.inputs['Metallic'].default_value if b else 0.0

def face_flags(o):
    """Per-face integer attributes that survive joins and decimation: 'metal' (1 on metal parts) and 'lens' (1 on see-through parts)."""
    for key, test in (('metal', lambda m: metal_of(m) > 0.5), ('lens', is_alpha)):
        at = o.data.attributes.get(key) or o.data.attributes.new(key, 'INT', 'FACE')
        vals = [1 if test(o.data.materials[p.material_index] if p.material_index < len(o.data.materials) else None) else 0 for p in o.data.polygons]
        at.data.foreach_set('value', vals)

def split_faces(o, keep, name):
    """A new object holding the faces of o for which keep(metal, lens) is true (or None if there are none)."""
    me = o.data; metal = [a.value for a in me.attributes['metal'].data]; lens = [a.value for a in me.attributes['lens'].data]
    trim = [a.value for a in me.attributes['trim'].data] if 'trim' in me.attributes else [0] * len(me.polygons)
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    drop = [f for f in bm.faces if not keep(metal[f.index], lens[f.index] or 0, trim[f.index])]
    if len(drop) == len(bm.faces): bm.free(); return None
    bmesh.ops.delete(bm, geom=drop, context='FACES'); m2 = bpy.data.meshes.new(name); bm.to_mesh(m2); bm.free()
    for mat in me.materials: m2.materials.append(mat)
    c = link(bpy.data.objects.new(name, m2)); c.matrix_world = o.matrix_world; return c

def emit_copy(m, what):
    """A copy of a material that emits its base colour ('col') or its metalness ('met'), for baking those through EMIT."""
    if m is None: m = M['tort']
    c = m.copy(); nt = c.node_tree; b = principled(c); out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Strength'].default_value = 1.0
    if what == 'col':
        src = b.inputs['Base Color']
        if src.is_linked: nt.links.new(src.links[0].from_socket, e.inputs['Color'])
        else: e.inputs['Color'].default_value = src.default_value
    else:
        v = metal_of(m); e.inputs['Color'].default_value = (v, v, v, 1)
    nt.links.new(e.outputs['Emission'], out.inputs['Surface']); return c

RES = {'feet': 2048, 'crossbody': 2048, 'hand_r': 2048, 'back': 2048, 'shoulder_r': 2048, 'head': 2048}

def bake_maps(id, objs, g0, out, res):
    """Bake the cinematic version onto LOD0: albedo (with ambient-occlusion grime), tangent normal, roughness, metalness."""
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'
    bk = scene.render.bake; bk.margin = 6; bk.cage_extrusion = 0.003; bk.max_ray_distance = 0.02
    for o in objs:
        for m in o.modifiers:
            if m.type == 'SUBSURF': m.levels = m.render_levels
    hi = game_copy(objs, id + '_high'); face_flags(hi)
    for o in objs:
        for m in o.modifiers:
            if m.type == 'SUBSURF': m.levels = 1
    lens_hi = split_faces(hi, lambda mt, ln, tr: ln or tr, 'tmp'); opaque_hi = split_faces(hi, lambda mt, ln, tr: not ln and not tr, id + '_high_o')
    for x in (hi, lens_hi):
        if x: bpy.data.objects.remove(x, do_unlink=True)
    hi = opaque_hi
    # the target's UVs: one non-overlapping atlas
    select([g0])
    for l in list(g0.data.uv_layers): g0.data.uv_layers.remove(l)
    g0.data.uv_layers.new(name='UVMap')
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.004); bpy.ops.object.mode_set(mode='OBJECT')
    imgs = {}
    for k in ('col', 'nrm', 'rgh', 'met', 'ao'):
        im = bpy.data.images.new(id + '_' + k, res, res, alpha=False)
        if k != 'col': im.colorspace_settings.name = 'Non-Color'
        imgs[k] = im
    tgt = bpy.data.materials.new(id + '_bake'); tgt.use_nodes = True; node = tgt.node_tree.nodes.new('ShaderNodeTexImage'); tgt.node_tree.nodes.active = node
    g0.data.materials.clear(); g0.data.materials.append(tgt)
    for p in g0.data.polygons: p.material_index = 0
    for o in bpy.data.objects: o.hide_render = o not in (g0, hi)
    def bake(kind, key, samples=1, selected=True, **kw):
        node.image = imgs[key]; scene.cycles.samples = samples; bk.use_selected_to_active = selected
        select([hi, g0] if selected else [g0], g0); bpy.ops.object.bake(type=kind, use_selected_to_active=selected, margin=6, **kw)
    bake('NORMAL', 'nrm', normal_space='TANGENT')
    bake('ROUGHNESS', 'rgh')
    orig = list(hi.data.materials)
    for what in ('col', 'met'):
        for i, m in enumerate(orig): hi.data.materials[i] = emit_copy(m, what)
        bake('EMIT', what)
    for i, m in enumerate(orig): hi.data.materials[i] = m
    hi.hide_render = True; bake('AO', 'ao', samples=48, selected=False); hi.hide_render = False
    # grime: darken the albedo where the surface is occluded (creases, seams, under hardware)
    import numpy as np
    col = np.array(imgs['col'].pixels[:]).reshape(-1, 4); ao = np.array(imgs['ao'].pixels[:]).reshape(-1, 4)[:, :1]
    met = np.array(imgs['met'].pixels[:]).reshape(-1, 4)[:, :1]          # metal stays bright (a dark-albedo metal reads grey), fabric and leather take the grime
    col[:, :3] *= (0.55 + 0.45 * ao) * (1 - met) + (0.88 + 0.12 * ao) * met; imgs['col'].pixels = col.ravel().tolist()
    paths = {}
    for k, im in imgs.items():
        if k == 'ao': continue
        path = os.path.join(out, id + '_' + k + '.png'); im.filepath_raw = path; im.file_format = 'PNG'
        im.save(); paths[k] = path
    bpy.data.objects.remove(hi, do_unlink=True)
    # the game material: the four maps on one Principled shader
    m = bpy.data.materials.new(id); m.use_nodes = True; nt = m.node_tree; b = principled(m)
    def tex(k, colour):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(paths[k]); t.image.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'; return t
    nt.links.new(tex('col', True).outputs['Color'], b.inputs['Base Color'])
    nt.links.new(tex('rgh', False).outputs['Color'], b.inputs['Roughness'])
    nt.links.new(tex('met', False).outputs['Color'], b.inputs['Metallic'])
    nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tex('nrm', False).outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    g0.data.materials[0] = m; bpy.data.materials.remove(tgt)
    for o in bpy.data.objects: o.hide_render = False
    return paths

def lights(scene, mode):
    """Studio (grey sweep, three soft boxes), day (sun and a blue sky) or night (dark blue ambient, one warm street lamp, a cool rim)."""
    for o in [o for o in scene.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o, do_unlink=True)
    w = bpy.data.worlds.new(mode); scene.world = w; w.use_nodes = True; bgn = w.node_tree.nodes['Background']
    if mode == 'studio':
        bgn.inputs['Color'].default_value = (0.82, 0.8, 0.78, 1); bgn.inputs['Strength'].default_value = 0.6
        rig = (('key', (1.2, -1.6, 2.4), 260, 1.2, (1, 1, 1)), ('fill', (-1.8, -1.0, 1.4), 90, 2.0, (1, 1, 1)), ('rim', (0.4, 1.8, 2.2), 180, 1.0, (1, 1, 1)))
    elif mode == 'day':
        bgn.inputs['Color'].default_value = (0.45, 0.62, 0.95, 1); bgn.inputs['Strength'].default_value = 0.9
        L = bpy.data.lights.new('sun', 'SUN'); L.energy = 4.5; L.angle = math.radians(1.5); L.color = (1, 0.96, 0.9); o = link(bpy.data.objects.new('sun', L)); o.rotation_euler = (math.radians(40), 0, math.radians(30)); o['fixed'] = True
        rig = ()
    else:
        bgn.inputs['Color'].default_value = (0.02, 0.03, 0.07, 1); bgn.inputs['Strength'].default_value = 0.5
        rig = (('lamp', (1.0, -1.2, 3.0), 380, 0.4, (1.0, 0.72, 0.42)), ('rim', (-0.6, 1.6, 1.8), 120, 1.0, (0.55, 0.7, 1.0)))
    for name, loc, energy, size, colr in rig:
        L = bpy.data.lights.new(name, 'AREA'); L.energy = energy; L.size = size; L.color = colr; o = link(bpy.data.objects.new(name, L)); o.location = loc; o['dir'] = Vector(loc).normalized()
    scene.view_settings.view_transform = 'AgX'

def build(id):
    fn, tier, name, slot = ITEMS[id]
    scene = reset(); materials(); B = Body()
    before = set(bpy.data.objects); info = fn(B)
    objs = [o for o in bpy.data.objects if o not in before and o.type == 'MESH' and not o.get('lod2')]
    proxies = [o for o in bpy.data.objects if o not in before and o.get('lod2')]
    for o in proxies: o.hide_render = True
    coll = bpy.data.collections.new(id); scene.collection.children.link(coll)
    for o in objs:
        for c in o.users_collection: c.objects.unlink(o)
        coll.objects.link(o)
    uv_all(objs)
    out = os.path.join(LIB, tier, id); os.makedirs(out, exist_ok=True)
    # cinematic .blend: everything live, the body as a hidden reference
    B.coll.hide_render = True; B.coll.hide_viewport = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, id + '.blend'), copy=True, relative_remap=True)
    cine_tris = sum(tris_render(o) for o in objs)
    # game version: LOD0 at the slot's budget, the cinematic detail baked into its maps, then LOD1 and LOD2 from it
    full = game_copy(objs, id + '_full'); face_flags(full); tf = tri_count(full)
    # see-through parts (lenses, crystals) and trim (chains) keep their own simple materials; the rest is baked into one atlas
    extra = split_faces(full, lambda mt, ln, tr: ln or tr, id + '_extra'); body = split_faces(full, lambda mt, ln, tr: not ln and not tr, id + '_body')
    tb = tri_count(body) if body else 0; te = tri_count(extra) if extra else 0; bpy.data.objects.remove(full, do_unlink=True)
    ratio = min(1.0, BUDGET.get(slot, 8000) / (tb + te))
    B.coll.hide_render = True
    if body:
        g0 = lod(body, id + '_LOD0', ratio); bpy.data.objects.remove(body, do_unlink=True)
        paths = bake_maps(id, objs, g0, out, RES.get(slot, 1024)); g2 = lod(g0, id + '_LOD2', 0.15)   # far away: coarse, with plain tubes in place of chains
    else:              # nothing to bake (a chain on its own): LOD0 is the trim, LOD2 the plain tube
        g0 = lod(extra, id + '_LOD0', ratio); bpy.data.objects.remove(extra, do_unlink=True); extra = None; paths = {}
        g2 = game_copy(proxies, id + '_LOD2') if proxies else lod(g0, id + '_LOD2', 0.15); proxies = []
    if proxies and body is not None:
        pc = game_copy(proxies, id + '_px'); select([g2, pc], g2); bpy.ops.object.join()
    if extra:
        ex0 = lod(extra, id + '_extra0', ratio); bpy.data.objects.remove(extra, do_unlink=True)
        select([g0, ex0], g0); bpy.ops.object.join()
    t0 = tri_count(g0); g1 = lod(g0, id + '_LOD1', 0.4); t1 = tri_count(g1); t2 = tri_count(g2)
    gcoll = bpy.data.collections.new(id + '_game'); scene.collection.children.link(gcoll)
    for g in (g0, g1, g2):
        for c in g.users_collection: c.objects.unlink(g)
        gcoll.objects.link(g)
    select([g0, g1, g2])
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, id + '.glb'), use_selection=True, export_format='GLB', export_apply=True, export_yup=True, export_image_format='AUTO')
    # Starlife: LOD0 cut into its non-metal, metal and see-through parts (one material each in the game)
    parts = []
    for suffix, keep in (('', lambda mt, ln, tr: not mt and not ln and not tr), ('_m', lambda mt, ln, tr: mt and not ln and not tr), ('_a', lambda mt, ln, tr: ln), ('_t', lambda mt, ln, tr: tr and not ln)):
        o = split_faces(g0, keep, id + suffix)
        if o: parts.append(o)
    select(parts)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, id + '_starlife.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    def flat(o):      # an untextured part's colour: its main material's base colour (the mean of its texture, if it has one)
        m = max(o.data.materials, key=lambda m: sum(1 for p in o.data.polygons if o.data.materials[p.material_index] == m)); b = principled(m); src = b.inputs['Base Color']
        if src.is_linked and src.links[0].from_node.type == 'TEX_IMAGE':
            im = src.links[0].from_node.image; px = im.pixels[:]; n = len(px) // 4; step = max(1, n // 4096)
            return [sum(px[i * 4 + c] for i in range(0, n, step)) / len(range(0, n, step)) for c in range(3)] + [1.0]
        return list(src.default_value)
    sl_parts = []
    for o in parts:
        k = o.name[len(id):]; textured = k in ('', '_m')
        sl_parts.append(dict(mesh=o.name, metal=1.0 if k in ('_m', '_t') else 0.0, alpha=k == '_a', tex=os.path.basename(paths['col']) if textured else '',
                             normal=os.path.basename(paths['nrm']) if textured else '', rough={'': 0.5, '_m': 0.22, '_a': 0.05, '_t': 0.18}[k],
                             color=[1, 1, 1, 1] if textured else flat(o)))
    for o in parts: bpy.data.objects.remove(o, do_unlink=True)
    # renders: product (cinematic), product (game LOD0 with its baked maps), and worn in studio, daylight and night light
    scene.render.engine = 'BLENDER_EEVEE'; scene.render.resolution_x = scene.render.resolution_y = 640
    try: scene.eevee.taa_render_samples = 48
    except Exception: pass
    for g in (g0, g1, g2): g.hide_render = True
    lights(scene, 'studio'); shoot(scene, objs, os.path.join(out, 'preview.png'), (0.55, -1.0, 0.45))
    for o in objs: o.hide_render = True
    g0.hide_render = False; shoot(scene, [g0], os.path.join(out, 'game.png'), (0.55, -1.0, 0.45)); g0.hide_render = True
    for o in objs: o.hide_render = False
    B.coll.hide_render = False
    show = {'body', 'head', 'hair', 'eyes', 'teeth', 'top', 'bottom', 'feet', 'shoes'}
    if slot == 'feet': show -= {'shoes'}
    for k, o in B.parts.items(): o.hide_render = k not in show
    close = slot in ('feet', 'head', 'eyes', 'ears', 'neck', 'wrist_l', 'wrist_r')
    frame = objs if close else [B.parts['body'], B.parts['head']] + objs; pad = 2.6 if close else 1.15
    shoot(scene, frame, os.path.join(out, 'onbody.png'), (0.6, -1.0, 0.25), pad=pad)
    shoot(scene, frame, os.path.join(out, 'onbody_back.png'), (-0.7, 1.0, 0.3), pad=pad)
    lights(scene, 'day'); shoot(scene, frame, os.path.join(out, 'day.png'), (0.6, -1.0, 0.25), pad=pad)
    lights(scene, 'night'); shoot(scene, frame, os.path.join(out, 'night.png'), (0.6, -1.0, 0.25), pad=pad)
    mats = sorted({m.name for o in objs for m in o.data.materials if m})
    texs = sorted({MATS[m]['tex'] for m in mats if m in MATS and MATS[m]['tex']})
    man = dict(id=id, name=name, tier=tier, slot=slot, space='olivia (Starlife shared body), metres; FBX Z-up, GLB Y-up, forward -Y in Blender',
               attach=[round(x, 4) for x in info['attach']], notes=info['notes'], materials=mats, source_textures=texs,
               triangles={'cinematic': cine_tris, 'LOD0': t0, 'LOD1': t1, 'LOD2': t2}, maps={k: os.path.basename(v) for k, v in paths.items()}, map_size=RES.get(slot, 1024),
               starlife_parts=sl_parts,
               files={'blend': id + '.blend', 'glb': id + '.glb', 'starlife_fbx': id + '_starlife.fbx', 'preview': 'preview.png', 'game': 'game.png',
                      'onbody': ['onbody.png', 'onbody_back.png', 'day.png', 'night.png']})
    json.dump(man, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    print('ITEM', id, man['triangles'])
    return man

# LOD0 triangle budgets per slot: what one accessory may cost on a mid-range phone with a dressed character on screen
BUDGET = {'crossbody': 12000, 'hand_r': 10000, 'shoulder_r': 10000, 'back': 12000, 'feet': 10000, 'wrist_l': 4000, 'wrist_r': 5000,
          'neck': 6000, 'ears': 3000, 'eyes': 4000, 'head': 8000, 'waist': 5000}

def tri_count(o): o.data.calc_loop_triangles(); return len(o.data.loop_triangles)
def tris_render(o):
    for m in o.modifiers:
        if m.type == 'SUBSURF': m.levels = m.render_levels
    n = tris(o)
    for m in o.modifiers:
        if m.type == 'SUBSURF': m.levels = 1
    return n

if __name__ == '__main__':
    done = []
    for id in ITEMS:
        if ONLY and id not in ONLY: continue
        try: done.append(build(id))
        except Exception as e:
            import traceback; traceback.print_exc(); print('FAILED', id, e)
    cat = os.path.join(LIB, 'catalog.json'); old = json.load(open(cat)) if os.path.exists(cat) else {}
    for m in done: old[m['id']] = m
    json.dump(old, open(cat, 'w'), indent=1)
