# Generates a complete character in Blender with MPFB (MakeHuman Plugin for Blender, free, assets CC0/CC-BY):
# body from settings (gender, age, ethnicity, height, weight, muscle, proportions), skin, eyes, brows, lashes,
# hair and clothes from MPFB's library, an optional game rig. Run inside Blender with MPFB enabled:
#   import make_character; make_character.make(dict(name='Ada', gender=0.0, age=0.25, african=1.0, hair='elvs_micky_afro',
#        skin='young_african_female', clothes=['punkduck_high_neck_crop_top', 'punkduck_female_short_jeans', 'punkduck_running_shoes_01']))
# gender 0 = female, 1 = male; age 0.5 = about 25 years old; every other setting 0..1, 0.5 = average.
import bpy, os
from mathutils import Vector
from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

DATA = LocationService.get_user_data()
def _asset(kind, name, ext):
    d = os.path.join(DATA, kind, name)
    hits = [f for f in os.listdir(d) if f.endswith(ext)] if os.path.isdir(d) else []
    if not hits: raise ValueError('no %s asset %r (%s)' % (kind, name, ext))
    return os.path.join(d, hits[0])

def make(spec, location=(0, 0, 0), rig=None):
    race = {'african': spec.get('african', 0.0), 'asian': spec.get('asian', 0.0), 'caucasian': spec.get('caucasian', 0.0)}
    tot = sum(race.values()) or 1.0; race = {k: v / tot for k, v in race.items()}
    macro = {'gender': spec.get('gender', 0.0), 'age': spec.get('age', 0.5), 'muscle': spec.get('muscle', 0.5), 'weight': spec.get('weight', 0.5),
             'proportions': spec.get('proportions', 0.5), 'height': spec.get('height', 0.5), 'cupsize': spec.get('cupsize', 0.5), 'firmness': spec.get('firmness', 0.5), 'race': race}
    before = set(bpy.data.objects)
    body = HumanService.create_human(macro_detail_dict=macro, scale=0.1)
    body.name = spec.get('name', 'Character')
    if spec.get('skin'): HumanService.set_character_skin(_asset('skins', spec['skin'], '.mhmat'), body, skin_type='ENHANCED_SSS')
    for kind, name, atype in (('eyes', 'low-poly', 'Eyes'), ('eyebrows', spec.get('brows', 'eyebrow001'), 'Eyebrows'), ('eyelashes', 'eyelashes01', 'Eyelashes')):
        HumanService.add_mhclo_asset(_asset(kind, name, '.mhclo'), body, asset_type=atype, subdiv_levels=0)
    if spec.get('hair'): HumanService.add_mhclo_asset(_asset('hair', spec['hair'], '.mhclo'), body, asset_type='Hair', subdiv_levels=0)
    for c in spec.get('clothes', []): HumanService.add_mhclo_asset(_asset('clothes', c, '.mhclo'), body, asset_type='Clothes', subdiv_levels=1)
    if rig: HumanService.add_builtin_rig(body, rig)
    for o in set(bpy.data.objects) - before:        # everything made for her moves with her
        if o.parent is None or o.parent not in (set(bpy.data.objects) - before): o.location = o.location + Vector(location)
    parts = [body] + [o for o in bpy.data.objects if o.parent == body or (o.parent and o.parent.parent == body)]
    print('CHARACTER', body.name, 'height %.2f m' % body.dimensions.z, 'parts', len(parts), 'faces', sum(len(o.data.polygons) for o in parts if o.type == 'MESH'))
    return body

# ------------------------------------------------------------------ fit: no skin or inner layer showing through
import bmesh, math, random
from mathutils.bvhtree import BVHTree
INNER = ('jeans', 'trouser', 'pants', 'shorts', 'skirt', 'tights')
OUTER = ('jacket', 'hoodie', 'coat', 'suit', 'blazer')

def _bvh(objs):
    dg = bpy.context.evaluated_depsgraph_get(); bm = bmesh.new()
    for o in objs:
        ev = o.evaluated_get(dg); me = ev.to_mesh(); me.transform(o.matrix_world); bm.from_mesh(me); ev.to_mesh_clear()
    bm.normal_update(); return BVHTree.FromBMesh(bm)

def fix_layers(body, margin=0.006):
    """Every garment is pushed just clear of what lies under it: skin first, then bottoms, then tops, then jackets."""
    clothes = [o for o in bpy.data.objects if o.parent == body and o.type == 'MESH' and not any(k in o.name for k in ('eyebrow', 'eyelash', 'low-poly', 'hair', 'afro', 'braid', 'bob', 'ponytail', 'short0', 'curly', 'long0'))]
    rank = lambda o: 0 if any(k in o.name for k in ('shoe', 'boot', 'sneaker', 'sandal', 'flat')) else 1 if any(k in o.name for k in INNER) else 3 if any(k in o.name for k in OUTER) else 2
    below = [body]; moved = 0
    for o in sorted(clothes, key=rank):
        if rank(o) == 0: continue
        bpy.context.view_layer.objects.active = o
        for m in [m for m in o.modifiers if m.type == 'SUBSURF']:          # smoothing pulls cloth inwards: fit the smoothed shape, not the cage
            m.show_viewport = True; m.levels = max(m.levels, m.render_levels, 1)
            with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)
        tree = _bvh(below); mw = o.matrix_world; inv = mw.inverted(); me = o.data; n = len(me.vertices)
        disp = [Vector((0, 0, 0)) for _ in range(n)]
        for v in me.vertices:
            p = mw @ v.co; q, nr, _, d = tree.find_nearest(p, 0.04)
            if q is None: continue
            out = p - q; inside = out.dot(nr) < 0
            if inside and out.length > 0.03: continue                         # far inside: the garment means to be there (a pocket, a fold), leave it
            if inside or out.length < margin: disp[v.index] = (q + nr * margin) - p
        # spread each push over the cloth around it, so the garment moves out smoothly instead of in dents and spikes
        nbr = [[] for _ in range(n)]
        for e in me.edges: a, b = e.vertices; nbr[a].append(b); nbr[b].append(a)
        need = [d.length for d in disp]
        for _ in range(6):
            disp = [(disp[k] * 2 + sum((disp[m] for m in nbr[k]), Vector((0, 0, 0)))) / (2 + len(nbr[k])) for k in range(n)]
        for k, v in enumerate(me.vertices):
            d = disp[k]
            if need[k] > 0 and d.length < need[k]: d = d.normalized() * need[k] if d.length > 1e-6 else d       # still clears what it had to clear
            if d.length > 0: v.co = inv @ (mw @ v.co + d); moved += 1
        o.data.update(); below.append(o)
    print('FIT', body.name, 'garments', len(clothes), 'vertices moved', moved)

# ------------------------------------------------------------------ realistic hair (Blender's curves hair system)
NODES = os.path.join(os.path.dirname(bpy.app.binary_path), '..', 'Resources', '%d.%d' % bpy.app.version[:2], 'datafiles', 'assets', 'nodes', 'procedural_hair_node_assets.blend')
def _nodes(obj, name, **vals):
    if name not in bpy.data.node_groups:
        with bpy.data.libraries.load(NODES) as (src, dst): dst.node_groups = [name]
    g = bpy.data.node_groups[name]; m = obj.modifiers.new(name, 'NODES'); m.node_group = g
    ids = {i.name: i.identifier for i in g.interface.items_tree if i.item_type == 'SOCKET' and i.in_out == 'INPUT'}
    for k, v in vals.items(): m.properties.inputs[ids[k.replace('_', ' ')]]['value'] = v
    return m

HAIR_STYLES = {   # length below the shoulders (m), curl (radius, turns per strand), strands per m2, clump, frizz
    'long_straight': dict(length=0.30, curl=None, density=60000, clump=0.35, frizz=0.0015),
    'long_wavy':     dict(length=0.26, curl=(0.012, 5.0), density=55000, clump=0.55, frizz=0.002),
    'long_curly':    dict(length=0.18, curl=(0.009, 14.0), density=50000, clump=0.75, frizz=0.003),
    'shoulder_bob':  dict(length=0.02, curl=None, density=60000, clump=0.3, frizz=0.0015)}

def realistic_hair(body, style='long_straight', melanin=0.95, redness=0.15, seed=1):
    """Long hair grown from the scalp, falling over the head and resting on the shoulders, back and chest."""
    S = HAIR_STYLES[style]; dg = bpy.context.evaluated_depsgraph_get(); ev = body.evaluated_get(dg); me = ev.to_mesh()
    mw = body.matrix_world; co = [mw @ v.co for v in me.vertices]
    head = [c for c in co if c.z > max(c.z for c in co) - 0.26]
    top = max(c.z for c in head); yc = sum(c.y for c in head) / len(head); xc = sum(c.x for c in head) / len(head)
    front = min(c.y for c in head if c.z > top - 0.15); brow = top - 0.115; shoulder = top - 0.30
    def scalp(c): return c.z > brow + 0.012 + max(0.0, (yc - c.y) - 0.035) * 0.0 + (0.0 if c.y > yc - 0.04 else 0.035) and (c.z > brow + 0.05 or abs(c.x - xc) < 0.07 or c.y > yc)
    bm = bmesh.new(); bm.from_mesh(me); ev.to_mesh_clear(); bm.transform(mw)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(scalp(v.co) for v in f.verts) or (all(v.co.y < yc + 0.02 for v in f.verts) and min(abs(v.co.x - xc) for v in f.verts) < 0.003)], context='FACES')   # and a parting down the front, so strands are not blended across her face
    sm = bpy.data.meshes.new(body.name + '_Scalp'); bm.to_mesh(sm); bm.free()
    uv = sm.uv_layers.new(name='UVMap'); cen = Vector((xc, yc, top - 0.1))
    for l in sm.loops:
        d = (sm.vertices[l.vertex_index].co - cen).normalized(); r = math.acos(max(-1, min(1, d.z))) / math.pi; a = math.atan2(d.y, d.x)
        uv.data[l.index].uv = (0.5 + math.cos(a) * r * 0.5, 0.5 + math.sin(a) * r * 0.5)
    scalp_ob = bpy.data.objects.new(body.name + '_Scalp', sm); bpy.context.scene.collection.objects.link(scalp_ob); scalp_ob.hide_render = True
    # guides: grown in the ways real hair grows, then dropped onto her
    tree = _bvh([body] + [o for o in bpy.data.objects if o.parent == body and o.type == 'MESH' and 'eye' not in o.name]); rnd = random.Random(seed)
    roots = []; sm.calc_loop_triangles() if hasattr(sm, 'calc_loop_triangles') else None
    for v in sm.vertices:
        if all((v.co - r).length > 0.016 for r, _ in roots): roots.append((v.co.copy(), v.normal.copy()))
    end_z = shoulder - S['length']; guides = []
    for i, (r, n) in enumerate(roots):
        p = r + n * 0.003; pts = [r - n * 0.002, p.copy()]; side = 1 if r.x >= xc else -1
        front_root = r.y < yc - 0.035
        d = (Vector((side * 0.7, 0.75, 0.35)) if front_root else n).normalized()       # the front is combed back and to the side, behind the ears
        wob = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * 0.03; wob = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * 0.03
        for k in range(160):
            on_head = p.z > brow - 0.02
            if front_root and on_head: d = (Vector((side * 0.7, 0.75, 0.1)).normalized() * 0.5 + Vector((0, 0, -1)) * 0.5 + wob * 0.2).normalized()   # combed back over the head until it is past the ears
            else: d = (d * (0.6 if on_head else 0.2) + Vector((0, 0, -1)) * (0.4 if on_head else 0.8) + wob * 0.2).normalized()
            p = p + d * 0.01
            if p.y < yc - 0.01 and brow - 0.14 < p.z < brow + 0.03 and abs(p.x - xc) < 0.095: p.x = xc + side * 0.095      # never down over the face
            if p.z < shoulder + 0.03 and abs(p.x - xc) > 0.15: p.x = xc + (0.15 if p.x > xc else -0.15)      # off the shoulder it falls in front of or behind her, not into the air beside her arm
            q, nrm, _, dist = tree.find_nearest(p, 0.12)
            if q is not None:
                out = p - q; rest = 0.004 + 0.012 * rnd.random() * (0 if on_head else 1)
                if out.dot(nrm) < 0 or out.length < rest: p = q + nrm * rest                     # lies on her, never through her
                elif on_head and out.length > 0.014: p = q + out.normalized() * 0.012                 # over the head it lies close to the scalp, as combed hair does
                elif p.z < shoulder + 0.04 and out.length < 0.07: p = p + (q + nrm * rest - p) * 0.25     # settles on shoulders, back and chest
            if p.z < shoulder + 0.03 and abs(p.x - xc) > 0.15: p.x = xc + (0.15 if p.x > xc else -0.15)      # again after the collision: an arm must not carry it sideways
            p.z = min(p.z, pts[-1].z - 0.002) if not on_head else p.z                                          # below the head it only ever falls
            pts.append(p.copy())
            if p.z < end_z + rnd.uniform(-0.03, 0.03): break
        for q in pts:                                   # a last pass: below the head nothing reaches past her shoulders
            if q.z < brow - 0.01 and abs(q.x - xc) > 0.15: q.x = xc + (0.15 if q.x > xc else -0.15)
        guides.append(pts)
    bpy.ops.object.select_all(action='DESELECT'); scalp_ob.select_set(True); bpy.context.view_layer.objects.active = scalp_ob
    bpy.ops.object.curves_empty_hair_add(); hair = bpy.context.object; hair.name = body.name + '_Hair_' + style; cu = hair.data
    P = 16; cu.add_curves([P] * len(guides)); pos = cu.attributes['position']
    for i, g in enumerate(guides):
        L = [0.0]
        for a, b in zip(g, g[1:]): L.append(L[-1] + (b - a).length)
        for k in range(P):
            t = L[-1] * k / (P - 1); j = max(0, min(len(g) - 2, next((m for m in range(len(L) - 1) if L[m + 1] >= t), len(L) - 2))); f = (t - L[j]) / max(1e-6, L[j + 1] - L[j])
            pos.data[i * P + k].vector = g[j].lerp(g[j + 1], min(1, max(0, f)))
    for m in list(hair.modifiers):
        if m.type == 'NODES' and m.node_group and 'Dynamics' in m.node_group.name: hair.modifiers.remove(m)
    bpy.ops.object.mode_set(mode='SCULPT_CURVES'); bpy.ops.curves.snap_curves_to_surface(attach_mode='NEAREST'); bpy.ops.object.mode_set(mode='OBJECT')
    _nodes(hair, 'Interpolate Hair Curves', Density=float(S['density']), Interpolation_Guides=3, Part_by_Mesh_Islands=True, Seed=seed)
    _nodes(hair, 'Clump Hair Curves', Factor=float(S['clump']), Shape=0.6, Tip_Spread=0.003)
    if S['curl']: _nodes(hair, 'Curl Hair Curves', Factor=1.0, Subdivision=2, Radius=float(S['curl'][0]), Frequency=float(S['curl'][1]), Curl_Start=0.06, Random_Offset=1.0)
    _nodes(hair, 'Frizz Hair Curves', Factor=1.0, Distance=float(S['frizz']), Shape=0.5)
    _nodes(hair, 'Set Hair Curve Profile', Replace_Radius=True, Radius=0.00045, Shape=0.4)
    mat = bpy.data.materials.new(hair.name + '_mat'); mat.use_nodes = True; nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    hb = nt.nodes.new('ShaderNodeBsdfHairPrincipled'); out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'); nt.links.new(hb.outputs[0], out.inputs['Surface'])
    try: hb.parametrization = 'MELANIN'
    except Exception: pass
    for k, v in (('Melanin', melanin), ('Melanin Redness', redness), ('Roughness', 0.32), ('Radial Roughness', 0.45), ('Random Color', 0.12), ('Random Roughness', 0.15), ('Coat', 0.05)):
        if k in hb.inputs: hb.inputs[k].default_value = v
    cu.materials.append(mat)
    for o in [o for o in bpy.data.objects if o.parent == body and o.type == 'MESH' and any(k in o.name for k in ('afro', 'braid', 'bob', 'ponytail', 'short0', 'curly', 'long0'))]: o.hide_viewport = o.hide_render = True    # the old mesh hair goes
    hair.parent = body; scalp_ob.parent = body; hair.matrix_parent_inverse = body.matrix_world.inverted(); scalp_ob.matrix_parent_inverse = body.matrix_world.inverted()
    dg = bpy.context.evaluated_depsgraph_get(); n = len(hair.evaluated_get(dg).data.curves)
    print('HAIR', hair.name, 'guides', len(guides), 'strands', n)
    return hair
