# Makes African hairstyles from scratch on the starlets' shared head and exports them for the game:
#   blender -b -P Tools/blender/make_hair.py -- Assets/Starlets/olivia/starlet.fbx <project>/Assets/Hair <project>/Art3D/hair [style ...]
# Braids, twists and locs are tubes grown from points on the scalp and hung under gravity around the head and
# shoulders; puffs are lumpy spheres; every style gets a close layer of hair over the scalp. A preview of each
# style is rendered to <Art3D/hair>/preview/<id>.png.
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hair_fit

args = sys.argv[sys.argv.index('--') + 1:]
ref_fbx, assets, art = args[:3]; only = set(args[3:])
DARK = (0.022, 0.016, 0.013, 1); GOLD = (0.83, 0.56, 0.12, 1)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=ref_fbx)
for o in list(bpy.data.objects):
    if o.name in ('head', 'body', 'top'): o.name = 'REF_' + o.name
    else: bpy.data.objects.remove(o, do_unlink=True)
head = bpy.data.objects['REF_head']
hm = head.data.copy(); hm.transform(head.matrix_world)
hb = bmesh.new(); hb.from_mesh(hm); bmesh.ops.remove_doubles(hb, verts=hb.verts[:], dist=0.0002); hb.normal_update()
bvh = BVHTree.FromBMesh(hb)
# the whole upper body, for hair to fall on: neck, shoulders, back and chest
bb = bmesh.new()
for nm in ('REF_head', 'REF_body', 'REF_top'):
    o = bpy.data.objects.get(nm)
    if o is None: continue
    tmp = o.data.copy(); tmp.transform(o.matrix_world); bb.from_mesh(tmp); bpy.data.meshes.remove(tmp)
bmesh.ops.remove_doubles(bb, verts=bb.verts[:], dist=0.0002); bmesh.ops.recalc_face_normals(bb, faces=bb.faces[:]); bb.normal_update()
body_bvh = BVHTree.FromBMesh(bb)

def is_scalp(co, low=1.50, slope=1.5): return co.z > low + max(0.0, -co.y - 0.02) * slope and (co.z > 1.61 or abs(co.x) < 0.074)
SCALP = [(v.co.copy(), v.normal.copy()) for v in hb.verts if is_scalp(v.co)]

def roots(spacing, seed=1, where=None):
    """Points on the scalp, no two closer than spacing."""
    rnd = random.Random(seed); pool = SCALP[:]; rnd.shuffle(pool); out = []
    for co, n in pool:
        if where and not where(co): continue
        if all((co - c).length > spacing for c, _ in out): out.append((co, n))
    return out

def mat(name, color, rough=0.55):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name); m.use_nodes = True
        b = m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = color; b.inputs['Roughness'].default_value = rough
    return m

def tube(points, radius, kind='braid', name='strand', material='Hair', res=2):
    """A tube along points. kind sets how its thickness varies: knotted for braids, a tighter ripple for twists, uneven for locs."""
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = radius; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1)
    rnd = random.Random(len(points) * 7 + int(points[0].x * 9999))
    for i, p in enumerate(points):
        sp.points[i].co = (p.x, p.y, p.z, 1)
        if kind == 'braid': r = 1.0 if i % 2 == 0 else 0.66
        elif kind == 'twist': r = 0.92 + 0.16 * math.sin(i * 2.4)
        elif kind == 'loc': r = 0.84 + 0.30 * rnd.random()
        elif kind == 'plain': r = 1.0
        else: r = 1.0
        if i >= len(points) - 2 and kind not in ('ridge', 'plain'): r *= 0.6            # taper the tip
        sp.points[i].radius = r
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat(material, DARK if material == 'Hair' else GOLD, 0.35 if material != 'Hair' else 0.55))
    return o

def resample(points, step):
    """The same path with points every step metres."""
    out = [points[0].copy()]; carry = 0.0
    for a, b in zip(points, points[1:]):
        seg = (b - a).length
        if seg < 1e-6: continue
        d = step - carry
        while d <= seg: out.append(a + (b - a) * (d / seg)); d += step
        carry = seg - (d - step)
    return out

def frames(points):
    """A side and an out direction at each point of a path, kept from flipping along it."""
    res = []; prev = None
    for i, p in enumerate(points):
        t = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        ref = Vector((p.x, p.y + 0.045, 0)); ref = ref.normalized() if ref.length > 1e-3 else Vector((0, 1, 0))     # away from the head's axis
        n = (ref - t * ref.dot(t)); n = n.normalized() if n.length > 1e-3 else (prev or Vector((1, 0, 0)))
        if prev is not None and n.dot(prev) < 0: n = -n
        prev = n; res.append((n, t.cross(n).normalized()))
    return res

def braid(points, radius, material='Hair', flat=1.0, res=1):
    """A real three-strand plait along the path: each strand weaves over and under the other two."""
    pts = resample(points, radius * 1.15); fr = frames(pts); n = len(pts); made = []
    for k in range(3):
        strand = []
        for i, p in enumerate(pts):
            th = i * 1.15 / 3.4 + k * math.tau / 3; taper = 1.0 if i < n - 4 else max(0.35, (n - i) / 4.0)
            strand.append(p + fr[i][1] * (radius * 0.95 * math.sin(th) * taper) + fr[i][0] * (radius * 0.45 * math.sin(2 * th) * flat * taper))
        made.append(tube(strand, radius * 0.62, 'plain', material=material, res=res))
    return made

def twist(points, radius, material='Hair', res=1):
    """A two-strand twist: two strands wound round each other."""
    pts = resample(points, radius * 1.1); fr = frames(pts); n = len(pts)
    for k in range(2):
        strand = []
        for i, p in enumerate(pts):
            th = i * 1.1 / 2.2 + k * math.pi; taper = 1.0 if i < n - 4 else max(0.4, (n - i) / 4.0)
            strand.append(p + fr[i][0] * (radius * 0.55 * math.cos(th) * taper) + fr[i][1] * (radius * 0.55 * math.sin(th) * taper))
        tube(strand, radius * 0.72, 'plain', material=material, res=res)

def ball(centre, radius, lumpy=0.0, squash=(1, 1, 1), material='Hair', subdiv=5, seed=0, grain=0.03):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=centre)
    o = bpy.context.object; o.scale = squash
    if lumpy > 0:      # a coily surface: big soft lumps, then small tight coils on top of them
        for j, (g, st) in enumerate(((grain, lumpy), (grain * 0.28, lumpy * 0.42), (grain * 0.11, lumpy * 0.2))):
            tex = bpy.data.textures.new('coils%d_%d' % (seed, j), 'CLOUDS'); tex.noise_scale = g; tex.noise_depth = 2
            m = o.modifiers.new('coils%d' % j, 'DISPLACE'); m.texture = tex; m.strength = st; m.texture_coords = 'GLOBAL'
    o.data.materials.append(mat(material, DARK if material == 'Hair' else GOLD, 0.35 if material != 'Hair' else 0.7))
    return o

def off_face(c): return not (c.y < -0.055 and abs(c.x) < 0.064)     # no strands growing from the front of the crown: they would hang over the face

# ------------------------------------------------------------------ groomed hair
# The grooming workflow, done here in code so it runs the same in any Blender version: a scalp, guide strands on it,
# then the three steps the hair nodes do: interpolate (many strands between the guides), clump (strands gather into
# locks towards the tip) and curl / frizz (no two strands alike). Each strand becomes a thin three-sided tube.
def groom(guides, count, radius, clump=0.6, clump_shape=1.0, frizz=0.004, curl=None, points=12, seed=1, clear_face=True, spread=0.004):
    rnd = random.Random(seed)
    G = []
    for g in guides:
        path = resample(g, max(1e-4, sum((b - a).length for a, b in zip(g, g[1:])) / (points - 1)))
        while len(path) < points: path.append(path[-1].copy())
        G.append(path[:points])
    # roots spread evenly over the scalp, by area
    tris = []
    for f in hb.faces:
        if all(is_scalp(v.co) and (not clear_face or off_face(v.co)) for v in f.verts):
            vs = [v.co for v in f.verts]
            for k in range(1, len(vs) - 1): tris.append((vs[0], vs[k], vs[k + 1], (vs[k] - vs[0]).cross(vs[k + 1] - vs[0]).length / 2))
    total = sum(t[3] for t in tris); acc = 0; cum = []
    for t in tris: acc += t[3]; cum.append(acc)
    import bisect
    verts = []; faces = []
    for sidx in range(count):
        a, b, c, _ = tris[min(bisect.bisect(cum, rnd.random() * total), len(tris) - 1)]
        u, v = rnd.random(), rnd.random()
        if u + v > 1: u, v = 1 - u, 1 - v
        root = a + (b - a) * u + (c - a) * v
        near = sorted(range(len(G)), key=lambda i: (G[i][0] - root).length_squared)[:3]            # interpolate between the three nearest guides
        w = [1.0 / ((G[i][0] - root).length_squared + 1e-6) for i in near]; ws = sum(w)
        lead = G[near[0]]; off = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * spread
        length = rnd.uniform(0.88, 1.0); phase = rnd.uniform(0, math.tau); wob = Vector((0, 0, 0)); path = []
        for k in range(points):
            t = k / (points - 1); kk = min(points - 1, k * length); k0 = int(kk); k1 = min(points - 1, k0 + 1); fr = kk - k0
            def at(gd): return gd[k0] + (gd[k1] - gd[k0]) * fr
            p = root + sum(((at(G[i]) - G[i][0]) * (w[n] / ws) for n, i in enumerate(near)), Vector((0, 0, 0)))
            p = p.lerp(at(lead) + off, clump * (t ** clump_shape))                                   # clump: towards its lock, more at the tip
            wob += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * frizz; p = p + wob * t      # frizz: every strand strays its own way
            path.append(p)
        if curl:                                                                                      # curl: wind the strand round its own path
            fine = resample(path, max(1e-4, sum((q - p).length for p, q in zip(path, path[1:])) / (points * 3 - 1))); frm = frames(fine); path = []
            for k, p in enumerate(fine):
                t = k / max(1, len(fine) - 1); ang = phase + t * curl[1]; r = curl[0] * min(1.0, t * 4)
                path.append(p + frm[k][0] * (math.cos(ang) * r) + frm[k][1] * (math.sin(ang) * r))
        frm = frames(path); base = len(verts); n = len(path)
        for k, p in enumerate(path):
            r = radius * (1.0 if k < n - 3 else max(0.25, (n - k) / 3.0))
            for q in range(3):
                ang = q * math.tau / 3; verts.append(p + frm[k][0] * (math.cos(ang) * r) + frm[k][1] * (math.sin(ang) * r))
        for k in range(n - 1):
            for q in range(3):
                a0 = base + k * 3 + q; a1 = base + k * 3 + (q + 1) % 3; faces.append((a0, a1, a1 + 3, a0 + 3))
    me = bpy.data.meshes.new('Groom'); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new('GroomedHair', me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat('Hair', DARK))
    print('GROOMED', len(guides), 'guides ->', count, 'strands,', len(faces) * 2, 'triangles'); return o

def clear_head(p, margin=1.05, seed_x=0.0):
    """Keep a point outside the head: an ellipsoid above the ears, an upright ellipse from there down to the jaw."""
    if p.z > 1.47:
        a, b, c, cy, cz = 0.094, 0.116, 0.120, -0.045, 1.585
        up = max(0.0, (p.z - cz) / c); need = margin * margin - up * up
        if need <= 0: return p
        k = math.sqrt((p.x / a) ** 2 + ((p.y - cy) / b) ** 2); want = math.sqrt(need)
        if k < want:
            if k < 0.05: p.x += math.copysign(0.01, seed_x); p.y += 0.01; k = math.sqrt((p.x / a) ** 2 + ((p.y - cy) / b) ** 2)
            p.x *= want / k; p.y = cy + (p.y - cy) * want / k
    return p

def hang(root, n, end_z, step=0.012, lift=0.005, back=None, sway=0.0, seed=0, rest=0.009):
    """A strand from the scalp that lies over the head and falls, draped behind the back or in front of the shoulder."""
    rnd = random.Random(seed); pts = [root - n * 0.004, root + n * lift]; p = pts[-1].copy()
    if back is None: back = root.y > -0.035
    wob = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * sway
    # over the crown the strand first runs outwards (to the side, or to the back near the middle), then gravity takes it
    out = Vector((root.x, max(root.y + 0.045, 0.0) if abs(root.x) < 0.03 else (root.y + 0.045) * 0.5, 0))
    out = out.normalized() if out.length > 1e-4 else Vector((0, 1, 0))
    over = back and abs(root.x) < 0.05 and root.y < 0.0          # a strand from the middle of the front that goes back: it is combed over the crown, never down the face
    if over: out = Vector((root.x * 2, 1, 0)).normalized()
    while p.z > end_z and len(pts) < 160:
        high = max(0.0, min(1.0, (p.z - 1.63) / 0.06))                    # 1 on top of the head, 0 at its sides
        d = (out * (1.1 if over else 0.22) * high + Vector((0, 0, -1))).normalized()          # mostly down: the head itself (clear_head) turns the fall into a drape over the crown
        p = clear_head(p + d * step + wob * step, seed_x=root.x if abs(root.x) > 1e-3 else rnd.uniform(-1, 1))
        if p.z < 1.52:                                                    # below the jaw the strand meets the body: it lies on the shoulders, back or chest
            if p.z > 1.40: p.y += 0.0035 if back else -0.0045              # behind the shoulder, or forward over it
            hit = body_bvh.find_nearest(p)
            if hit[0] is not None:
                q, nrm = hit[0], hit[1]; out = (p - q); dist = out.length; inside = out.dot(nrm) < 0
                if inside: p = q + nrm * rest                              # never through her
                elif dist < rest: p = q + out.normalized() * rest          # resting on her
                elif dist < 0.09 and p.z < 1.43 and not (abs(q.x) < 0.065 and q.z > 1.36): p = p + (q + out.normalized() * rest - p) * 0.3      # hanging near her shoulders, back or chest (not her neck): it settles against her instead of floating
            if not back: p.x = math.copysign(max(abs(p.x), 0.062), p.x if abs(p.x) > 1e-3 else root.x)      # front strands fall either side of the throat, not across it
        pts.append(p.copy())
    return pts

def on_scalp(x, y, lift=0.004):
    """The point of the scalp under (x, y), raised a little."""
    hit = bvh.ray_cast(Vector((x, y, 2.2)), Vector((0, 0, -1)))
    return (hit[0] + hit[1] * lift) if hit[0] is not None else None

def finish(sid, label, cap_lift=0.0025, cap_color=DARK, faces=320000):
    """Join everything made so far into HAIR, add the scalp layer, export, and render a preview."""
    bpy.ops.object.select_all(action='DESELECT'); made = [o for o in bpy.data.objects if not o.name.startswith('REF_') and o.name not in ('CAP',)]
    for o in made:
        o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    made = [o for o in bpy.context.selected_objects]
    bpy.context.view_layer.objects.active = made[0]
    if len(made) > 1: bpy.ops.object.join()
    h = bpy.context.view_layer.objects.active; h.name = 'HAIR'
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    hair_fit.cap(color=cap_color, lift=cap_lift)
    preview(sid)
    hair_fit.export(sid, assets, art, label=label, max_faces=faces)
    hair_fit.clear()

def preview(sid):
    sc = bpy.context.scene; os.makedirs(os.path.join(art, 'preview'), exist_ok=True)
    if 'REF_PrevCam' not in bpy.data.objects:
        cam = bpy.data.objects.new('REF_PrevCam', bpy.data.cameras.new('PrevCam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
        sun = bpy.data.objects.new('REF_Sun', bpy.data.lights.new('Sun', 'SUN')); sc.collection.objects.link(sun); sun.rotation_euler = (math.radians(55), 0, math.radians(-30)); sun.data.energy = 3.5
        w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.68, 0.8, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
        for nm in ('REF_head', 'REF_body', 'REF_top'):
            o = bpy.data.objects.get(nm)
            if o: o.data.materials.clear(); o.data.materials.append(mat('Skin', (0.35, 0.2, 0.13, 1), 0.6))
        sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 12; sc.cycles.use_denoising = False
        sc.render.resolution_x = 300; sc.render.resolution_y = 420; sc.render.image_settings.file_format = 'PNG'; sc.view_settings.view_transform = 'Standard'
    cam = bpy.data.objects['REF_PrevCam']; cam.data.ortho_scale = 0.80
    for tag, loc, rot in (('front', (0.25, -3, 1.42), (math.radians(90), 0, math.radians(5))), ('back', (-0.9, 2.8, 1.42), (math.radians(90), 0, math.radians(197.5)))):
        cam.location = loc; cam.rotation_euler = rot
        sc.render.filepath = os.path.join(art, 'preview', '%s-%s.png' % (sid, tag)); bpy.ops.render.render(write_still=True)

def want(sid): return not only or sid in only

# ------------------------------------------------------------------ the styles
if want('blowout'):         # a blown-out afro: thousands of strands standing out from the head, soft and frizzy
    g = [[co - n * 0.003] + [co + n * (0.010 + 0.012 * k) for k in range(8)] for co, n in roots(0.02, 11)]
    groom(g, 3600, 0.0019, clump=0.15, frizz=0.0030, curl=(0.006, 34.0), points=8, seed=3, clear_face=False, spread=0.012)
    finish('blowout', 'Blowout afro', faces=420000)

if want('washngo'):         # wash-and-go: defined curls to the shoulders
    g = [hang(co, n, 1.40 + (i % 5) * 0.015, step=0.012, lift=0.014, sway=0.05, seed=i) for i, (co, n) in enumerate(roots(0.024, 12, off_face))]
    groom(g, 2200, 0.0021, clump=0.85, clump_shape=0.6, frizz=0.0012, curl=(0.009, 40.0), points=12, seed=5, spread=0.007)
    finish('washngo', 'Wash-and-go curls', faces=420000)

if want('silkpress'):       # a silk press: straight, sleek hair past the shoulders
    g = [hang(co, n, 1.22 + (i % 4) * 0.01, step=0.012, lift=0.006, back=(co.y > -0.02 or abs(co.x) < 0.05), sway=0.01, seed=i) for i, (co, n) in enumerate(roots(0.022, 13, off_face))]
    groom(g, 5200, 0.0018, clump=0.35, clump_shape=1.4, frizz=0.0005, points=16, seed=7, spread=0.006)
    finish('silkpress', 'Silk press', faces=420000)

if want('boxbraids'):       # long box braids to the middle of the back, the front ones over the shoulders
    for i, (co, n) in enumerate(roots(0.0215, 1, off_face)):
        back = co.y > -0.02 or abs(co.x) < 0.05
        braid(hang(co, n, 1.02 + (i % 5) * 0.012 if back else 1.20 + (i % 4) * 0.015, back=back, sway=0.02, seed=i), 0.0056)
    finish('boxbraids', 'Box braids')

if want('twists'):          # two-strand twists to the jaw, standing a little away from the head
    for i, (co, n) in enumerate(roots(0.0165, 2, off_face)):
        twist(hang(co, n, 1.47 + (i % 6) * 0.012, step=0.009, lift=0.012, sway=0.07, seed=i), 0.0050)
    finish('twists', 'Twists')

if want('longlocs'):        # thick locs past the shoulders, laid the same way as the box braids
    for i, (co, n) in enumerate(roots(0.0245, 3, off_face)):
        back = co.y > -0.02 or abs(co.x) < 0.05
        path = hang(co, n, (1.06 if back else 1.22) + (i % 5) * 0.014, back=back, sway=0.02, seed=i, rest=0.011)
        tube(resample(path, 0.006), 0.0072, 'loc', res=3)
    finish('longlocs', 'Long locs')

if False and want('puffs'):   # (the earlier, lumpy-sphere version)           # two afro puffs
    for s in (-1, 1): ball((s * 0.088, -0.005, 1.705), 0.078, 0.026, seed=s + 2)
    for s in (-1, 1): tube([Vector((s * 0.066 + math.cos(a) * 0.018 * s, -0.005 + math.sin(a) * 0.03, 1.668 + math.cos(a) * 0.012)) for a in [k * math.pi / 6 for k in range(13)]], 0.004, 'ridge', material='Bead')
    finish('puffs', 'Afro puffs')

if False and want('highpuff'):   # (the earlier, lumpy-sphere version)        # one big puff on top, the sides slicked up to it
    ball((0, 0.0, 1.775), 0.108, 0.034, squash=(1.0, 1.0, 0.86), seed=5)
    tube([Vector((math.cos(a) * 0.058, 0.0 + math.sin(a) * 0.062, 1.700)) for a in [k * math.pi / 8 for k in range(17)]], 0.007, 'ridge', material='Bead')
    finish('highpuff', 'High puff')

if False and want('twa'):   # (the earlier, lumpy-sphere version)             # a short natural crop: a thick, coily layer over the whole scalp
    hair_fit.cap(color=DARK, lift=0.011, low=1.50, slope=1.5)
    c = bpy.data.objects['CAP']; c.name = 'crop'
    bpy.context.view_layer.objects.active = c; c.select_set(True)
    m = c.modifiers.new('sub', 'SUBSURF'); m.levels = 4
    for j, (g, st) in enumerate(((0.012, 0.012), (0.004, 0.006), (0.0016, 0.003))):
        tex = bpy.data.textures.new('crop%d' % j, 'CLOUDS'); tex.noise_scale = g; tex.noise_depth = 2
        d = c.modifiers.new('coils%d' % j, 'DISPLACE'); d.texture = tex; d.strength = st; d.texture_coords = 'GLOBAL'; d.mid_level = 0.35
    c.data.materials.clear(); c.data.materials.append(mat('Hair', DARK))
    finish('twa', 'Short natural')

if False and want('frohawk'):   # (the earlier, lumpy-sphere version)         # a ridge of puffs from the forehead to the nape, the sides cropped close
    for k, (y, z, r) in enumerate(((-0.105, 1.705, 0.050), (-0.060, 1.745, 0.062), (-0.005, 1.758, 0.068), (0.050, 1.735, 0.064), (0.092, 1.685, 0.056), (0.110, 1.625, 0.046))):
        ball((0, y, z), r, 0.024, squash=(0.82, 1, 1), seed=10 + k, subdiv=4)
    finish('frohawk', 'Frohawk')

if want('fulani'):          # cornrows running back from the hairline into braids, two braids forward at the temples, gold beads
    for k, x in enumerate((-0.066, -0.044, -0.022, 0.0, 0.022, 0.044, 0.066)):
        ridge = [p for p in (on_scalp(x * (1 - 0.25 * max(0, t - 0.6)), -0.118 + t * 0.20) for t in [i / 22 for i in range(23)]) if p is not None]
        last = ridge[-1]; n = Vector((0, 0.6, -0.3)).normalized()
        tail = hang(last, n, 1.16 + (k % 3) * 0.03, back=True, lift=0.006, sway=0.015, seed=k)[1:]
        braid(ridge + tail, 0.0060, flat=0.7); ball(tail[-1], 0.0085, material='Bead', subdiv=2)
    for s in (-1, 1):
        for j, y in enumerate((-0.075, -0.045)):
            start = on_scalp(s * 0.078, y)
            if start is None: continue
            pts = hang(start, Vector((s, -0.2, 0)).normalized(), 1.36 - j * 0.03, back=False, lift=0.008, seed=30 + j)
            braid(pts, 0.0054); ball(pts[-1], 0.008, material='Bead', subdiv=2); ball(pts[-4], 0.0075, material='Bead', subdiv=2)
    finish('fulani', 'Fulani braids')

if want('braidbun'):        # braids swept up from all round the hairline into a coiled bun on the crown
    top = Vector((0, 0.005, 1.722))
    for k in range(16):
        a = k * math.tau / 16; edge = None
        for r in (0.115, 0.105, 0.095, 0.085):                      # the lowest scalp point in this direction
            p = Vector((math.cos(a) * r * 0.82, -0.04 + math.sin(a) * r, 0)); hit = on_scalp(p.x, p.y)
            if hit is not None and is_scalp(hit): edge = p; break
        if edge is None: continue
        pts = [q for q in (on_scalp(edge.x + (top.x - edge.x) * t, edge.y + (top.y - edge.y) * t, 0.0045) for t in [i / 12 for i in range(13)]) if q is not None]
        if len(pts) > 3: braid(pts, 0.0058, flat=0.6)
    coil = [Vector((math.cos(t) * (0.058 - 0.0024 * t), 0.005 + math.sin(t) * (0.058 - 0.0024 * t), 1.722 + 0.0022 * t)) for t in [i * 0.35 for i in range(64)]]
    braid(coil, 0.014, res=2)
    finish('braidbun', 'Braided bun')

def coil_ball(centre, radius, count, seed=1, squash=(1, 1, 1), length=0.5, strand=0.0019, curl=(0.0055, 30.0), frizz=0.0016, points=22):
    """A puff of coily hair: a dark core, and curled strands growing out of it in every direction."""
    rnd = random.Random(seed); centre = Vector(centre); sq = Vector(squash)
    core = ball(centre, radius * 0.66, 0.0, squash=squash, subdiv=3)
    verts = []; faces = []
    for _ in range(count):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized(); d = Vector((d.x * sq.x, d.y * sq.y, d.z * sq.z))
        n = d.normalized(); side = n.cross(Vector((0.31, 0.2, 0.93))).normalized(); up = n.cross(side)
        ln = radius * length * rnd.uniform(0.8, 1.15); phase = rnd.uniform(0, math.tau); wob = Vector((0, 0, 0)); base = len(verts)
        for k in range(points):
            t = k / (points - 1); ang = phase + t * curl[1]; r = curl[0] * min(1.0, t * 5)
            wob += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * frizz
            p = centre + d * radius * 0.6 + n * ln * t + side * (math.cos(ang) * r) + up * (math.sin(ang) * r) + wob * t
            rr = strand * (1.0 if k < points - 3 else max(0.3, (points - k) / 3.0))
            for q in range(3):
                a = q * math.tau / 3; verts.append(p + side * (math.cos(a) * rr) + up * (math.sin(a) * rr))
        for k in range(points - 1):
            for q in range(3):
                a0 = base + k * 3 + q; a1 = base + k * 3 + (q + 1) % 3; faces.append((a0, a1, a1 + 3, a0 + 3))
    me = bpy.data.meshes.new('Coils'); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new('Coils', me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat('Hair', DARK)); return o

def sleek(count=2600, seed=4):
    """Hair combed flat against the head, for styles that gather it into a puff or a bun."""
    g = [[co + n * 0.002, co + n * 0.004 + Vector((0, 0.004, 0.006)), co + n * 0.005 + Vector((0, 0.008, 0.012))] for co, n in roots(0.02, seed)]
    return groom(g, count, 0.0016, clump=0.2, frizz=0.0003, points=4, seed=seed, clear_face=False, spread=0.002)

if want('puffs'):           # two afro puffs of coily strands
    for sgn in (-1, 1): coil_ball((sgn * 0.088, -0.005, 1.708), 0.078, 1500, seed=3 + sgn)
    for sgn in (-1, 1): tube([Vector((sgn * 0.066 + math.cos(a) * 0.018 * sgn, -0.005 + math.sin(a) * 0.03, 1.668 + math.cos(a) * 0.012)) for a in [k * math.pi / 6 for k in range(13)]], 0.004, 'ridge', material='Bead')
    sleek(); finish('puffs', 'Afro puffs', faces=520000)

if want('highpuff'):        # one big puff on top, the sides slicked up to it
    coil_ball((0, 0.0, 1.78), 0.108, 2600, seed=5, squash=(1.0, 1.0, 0.86))
    tube([Vector((math.cos(a) * 0.058, math.sin(a) * 0.062, 1.700)) for a in [k * math.pi / 8 for k in range(17)]], 0.007, 'ridge', material='Bead')
    sleek(); finish('highpuff', 'High puff', faces=520000)

if want('frohawk'):         # a ridge of coily puffs from the forehead to the nape
    for k, (y, z, r) in enumerate(((-0.105, 1.705, 0.050), (-0.060, 1.745, 0.062), (-0.005, 1.758, 0.068), (0.050, 1.735, 0.064), (0.092, 1.685, 0.056), (0.110, 1.625, 0.046))):
        coil_ball((0, y, z), r, 520, seed=10 + k, squash=(0.8, 1, 1), points=18)
    sleek(1800); finish('frohawk', 'Frohawk', faces=520000)

if want('twa'):             # a short natural crop: tight coils all over the head
    g = [[co - n * 0.002] + [co + n * (0.004 + 0.004 * k) for k in range(4)] for co, n in roots(0.02, 21)]
    groom(g, 5200, 0.0018, clump=0.1, frizz=0.0008, curl=(0.0032, 16.0), points=5, seed=9, clear_face=False, spread=0.003)
    finish('twa', 'Short natural', faces=520000)

print('MAKE HAIR DONE')
