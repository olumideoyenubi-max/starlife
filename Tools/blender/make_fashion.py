# Real garments for the starlets' wardrobe, from MPFB's (MakeHuman's) clothes, refitted onto the starlets' shared body.
# For each garment: an MPFB woman is made wearing it and rigged; her limbs are posed onto olivia's joints (the shared
# body the wardrobe is worn on) and she is scaled so her neck meets olivia's; the garment is bound to her body (Surface
# Deform) and her body is shrink-wrapped onto olivia's (body, feet and the skin fillers from basics), so the garment
# follows; finally it is lifted a few millimetres clear of the skin. Each garment set is saved like an avatar's pieces:
# Assets/Starlets/f_<id>/starlet.fbx + textures + manifest.json, which SLBuild.Import3D takes as it is. In town the
# pieces take olivia's skin weights (SLRig falls back to her rig), so they walk with the body they were fitted to.
#   Blender -b -P Tools/blender/make_fashion.py -- <project> [id ...]
# Do not call read_factory_settings here: it switches MPFB off.
import bpy, bmesh, os, sys, json, math, struct
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

args = sys.argv[sys.argv.index('--') + 1:]
PROJECT = args[0]; ONLY = set(args[1:])
STAR = os.path.join(PROJECT, 'Assets', 'Starlets')
DATA = LocationService.get_user_data()

# id: [(MPFB clothes asset, part name in the game)]; parts: top, bottom, dress, outer, shoes (keep_print: textured fabric)
SETS = {
    # dresses
    'f_gown':     [('sl_emerald_gown', 'dress')],
    'f_halter':   [('elvs_halter_dress_long', 'dress')],
    'f_midi':     [('toigo_halter_dress_midi', 'dress')],
    'f_keyhole':  [('toigo_keyhole_neck_dress', 'dress')],
    'f_tiered':   [('toigo_dress_with_tiered_skirt', 'dress')],
    'f_shift':    [('toigo_shift_dress', 'dress')],
    'f_quilted':  [('sl_quilted_dress', 'dress')],
    'f_fashion':  [('elvs_simple_fashion_dress_1', 'dress')],
    'f_sixties':  [('elvs_simple_60s_dress', 'dress')],
    'f_wrap':     [('mindfront_f_dress_02', 'dress')],
    'f_ankara':   [('sl_ankara_maxi', 'dress')],
    'f_blackkey': [('sl_black_keyhole', 'dress')],
    # tops
    'f_tee':      [('joepal_crude_t-shirt_female', 'top')],
    'f_cami':     [('toigo_camisole_top', 'top')],
    'f_silk':     [('sl_silk_carre_blouse', 'top')],
    'f_lace':     [('punkduck_lace_up_blouse', 'top')],
    'f_peasant':  [('elvs_ruffle_sleeve_peasant_blouse_1', 'top')],
    'f_offshoulder': [('punkduck_off-shoulder_long-sleeve_top', 'top')],
    'f_corset':   [('sl_ankara_corset', 'top')],
    'f_leopard':  [('sl_leopard_cami', 'top')],
    'f_crop':     [('punkduck_sleeveless_crop_top', 'top')],
    'f_highneck': [('punkduck_high_neck_crop_top', 'top')],
    'f_tube':     [('punkduck_tube_top', 'top')],
    'f_tank':     [('elvs_ladies_tank1', 'top')],
    'f_cobalt':   [('sl_cobalt_tank', 'top')],
    'f_disco':    [('elvs_disco_top2', 'top')],
    'f_butter':   [('sl_butter_offshoulder', 'top')],
    'f_sage':     [('sl_sage_halter', 'top')],
    'f_vneck':    [('punkduck_v_neck_top', 'top')],
    # bottoms
    'f_jeans':    [('punkduck_female_tight_jeans', 'bottom')],
    'f_bootcut':  [('elvs_jeans_bootcut', 'bottom')],
    'f_shorts':   [('punkduck_female_short_jeans', 'bottom')],
    'f_wideleg':  [('sl_cream_wideleg', 'bottom')],
    'f_leather':  [('sl_leather_trousers', 'bottom')],
    'f_trousers': [('mindfront_female_trousers_1', 'bottom')],
    'f_harem':    [('toigo_harem_pants', 'bottom')],
    # tailoring
    'f_suit':     [('female_elegantsuit01', 'dress')],
    'f_casual':   [('female_casualsuit01', 'dress')],
    # jackets and skirts, cut at the waist from suits and dresses ('split': the part above is the top, below the bottom)
    'f_blazer':   [('female_elegantsuit01', 'split')],
    'f_softsuit': [('female_casualsuit01', 'split')],
    'f_tweed':    [('sl_tweed_suit', 'split')],
    'f_hoodiejk': [('elvs_hooded_sweat_jacket1', 'outer')],
    'f_monohood': [('sl_monogram_hoodie', 'outer')],
    'f_tieredsk': [('toigo_dress_with_tiered_skirt', 'split')],
    'f_sixtiessk': [('elvs_simple_60s_dress', 'split')],
    'f_shiftsk':  [('toigo_shift_dress', 'split')],
    # boots
    'f_chelsea':  [('sl_suede_chelsea', 'shoes')],
    'f_workboot': [('toigo_ankle_boots_male', 'shoes')],
    # shoes
    'f_flats':    [('toigo_flats', 'shoes')],
    'f_sandals':  [('dressupdoc_sandals1', 'shoes')],
    'f_runners':  [('punkduck_running_shoes_01', 'shoes')],
}
# sets whose source print would show through any dye (the dye shades by brightness): their pieces get a plain twill instead
PLAIN = {'f_sixtiessk'}

def plain_weave(dst, normal, size=1024):
    """A plain 2/2 twill, mid grey, for the game to dye: the colour map (or, with normal=True, its normal map)."""
    import numpy as np
    y, x = np.mgrid[0:size, 0:size].astype(np.float32); t = 2 * math.pi / 4      # 4 px a thread
    h = 0.5 + 0.5 * np.sin((x + y) * t) * np.sin(x * t) * 0.6 + 0.4 * np.sin((x + y) * t * 0.5)
    h += np.random.default_rng(7).normal(0, 0.04, h.shape)
    if normal:
        dx = np.roll(h, -1, 1) - h; dy = np.roll(h, -1, 0) - h; k = 1.2
        n = np.stack([-dx * k, -dy * k, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True); rgb = n * 0.5 + 0.5
    else:
        g = 0.5 + (h - 0.5) * 0.12; rgb = np.stack([g, g, g], -1)
    im = bpy.data.images.new(os.path.basename(dst), size, size); px = np.concatenate([np.clip(rgb, 0, 1), np.ones((size, size, 1), np.float32)], -1)
    im.pixels.foreach_set(px[::-1].ravel()); im.filepath_raw = dst; im.file_format = 'PNG'; im.save(); return im

MAT = {'top': 'Wolf3D_Outfit_Top', 'bottom': 'Wolf3D_Outfit_Bottom', 'dress': 'Wolf3D_Outfit_Top', 'outer': 'Wolf3D_Outfit_Top', 'shoes': 'Wolf3D_Outfit_Footwear', 'split': 'Wolf3D_Outfit_Top'}

def asset(kind, name, ext):
    d = os.path.join(DATA, kind, name)
    hits = [f for f in os.listdir(d) if f.endswith(ext)] if os.path.isdir(d) else []
    if not hits: raise ValueError(f'no {kind} asset {name!r}')
    return os.path.join(d, hits[0])

def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images, bpy.data.node_groups):
        for x in list(coll):
            if x.users == 0: coll.remove(x)

def olivia_joints():
    b = open(os.path.join(PROJECT, 'Assets', 'Resources', 'Rigs', 'olivia.bytes'), 'rb').read(); o = 0; J = {}
    n = struct.unpack_from('<i', b, o)[0]; o += 4
    for _ in range(n):
        l = struct.unpack_from('<i', b, o)[0]; o += 4; name = b[o:o + l].decode(); o += l
        par, x, y, z = struct.unpack_from('<i3f', b, o); o += 16
        J[name] = Vector((-x, -z, y))       # Unity back to Blender (the neck-fixed space the pieces live in)
    return J

def target_surface():
    """olivia's skin as the game draws it under clothes: her body and feet, and the basics fillers for chest and hips."""
    objs = []
    for fbx, keep in ((os.path.join(STAR, 'olivia', 'starlet.fbx'), ('body', 'feet')), (os.path.join(STAR, 'basics', 'starlet.fbx'), ('torso', 'hips'))):
        before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=fbx)
        for o in set(bpy.data.objects) - before:
            if o.type == 'MESH' and o.name.split('.')[0] in keep: objs.append(o)
            else: bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new()
    for o in objs:
        me = o.data.copy(); me.transform(o.matrix_world); bm.from_mesh(me); bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new('target'); bm.to_mesh(me); bm.free()
    t = bpy.data.objects.new('target', me); bpy.context.scene.collection.objects.link(t)
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    return t

def all_images(tree, seen=None):
    seen = seen or set()
    for n in tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image: yield n.image
        if n.type == 'GROUP' and n.node_tree and n.node_tree.name not in seen:
            seen.add(n.node_tree.name); yield from all_images(n.node_tree, seen)

def textures(o):
    m = o.material_slots[0].material if o.material_slots else None
    imgs = list(all_images(m.node_tree)) if m and m.use_nodes else []
    diffuse = next((i for i in imgs if any(k in i.name.lower() for k in ('diffuse', 'albedo', '_color', 'basecolor', '_diff'))), None)
    normal = next((i for i in imgs if 'normal' in i.name.lower() or '_nrm' in i.name.lower()), None)
    if diffuse is None:
        rest = [i for i in imgs if i is not normal and not any(k in i.name.lower() for k in ('rough', 'spec', 'ao', 'bump', 'sss', 'disp', 'metal', 'trans'))]
        diffuse = rest[0] if rest else None
    return diffuse, normal

def aim(rig, bone, target_from, target_to):
    """Turns a pose bone (about its head) so that it points from target_from to target_to, as the same bone of olivia does."""
    pb = rig.pose.bones[bone]; bpy.context.view_layer.update()
    mw = rig.matrix_world @ pb.matrix; head = mw.translation.copy(); tail = rig.matrix_world @ pb.tail
    q = (tail - head).normalized().rotation_difference((target_to - target_from).normalized())
    pb.matrix = rig.matrix_world.inverted() @ (Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ mw); bpy.context.view_layer.update()

def tone(img):
    if img is None or img.size[0] == 0: return [0.8, 0.8, 0.8], 0.8
    import numpy as np
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, img.channels)[::7]
    a = px[:, 3] if img.channels == 4 else np.ones(len(px)); px = px[a > 0.5] if (a > 0.5).sum() > 50 else px
    lum = px[:, 0] * 0.299 + px[:, 1] * 0.587 + px[:, 2] * 0.114
    return [round(float(v), 4) for v in px[:, :3].mean(axis=0)], round(float(lum.mean()), 4)

def make(sid, garments, J, target):
    for o in [x for x in bpy.data.objects if x is not target]: bpy.data.objects.remove(o, do_unlink=True)
    macro = {'gender': 0.0, 'age': 0.42, 'muscle': 0.5, 'weight': 0.42, 'proportions': 0.6, 'height': 0.55, 'cupsize': 0.55, 'firmness': 0.6,
             'race': {'african': 0.34, 'asian': 0.33, 'caucasian': 0.33}}
    body = HumanService.create_human(macro_detail_dict=macro, scale=0.1); body.name = 'mpfb_body'
    worn = []
    for c, part in garments:
        before = set(bpy.data.objects)
        HumanService.add_mhclo_asset(asset('clothes', c, '.mhclo'), body, asset_type='Clothes', subdiv_levels=0)
        for o in set(bpy.data.objects) - before:
            if o.type == 'MESH': worn.append((o, part))
    HumanService.add_builtin_rig(body, 'game_engine')
    rig = body.parent if body.parent and body.parent.type == 'ARMATURE' else next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for o in bpy.data.objects:
        for m in list(getattr(o, 'modifiers', [])):
            if m.type == 'SUBSURF': o.modifiers.remove(m)
    for m in [m for m in body.modifiers if m.type == 'MASK']: body.modifiers.remove(m)     # the whole body, also where clothes hide it: the garments bind to it
    if body.data.shape_keys:
        with bpy.context.temp_override(object=body, active_object=body, selected_objects=[body]): bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    # garments take the body's weights (MPFB rigs only the body); sleeves follow the arms, the rest the body
    pb = rig.pose.bones
    segs = [(rig.matrix_world @ pb['upperarm_' + s].head, rig.matrix_world @ pb['lowerarm_' + s].head, rig.matrix_world @ pb['hand_' + s].tail) for s in ('l', 'r')]
    segs = [(a, e) for a, e, h in segs] + [(e, h) for a, e, h in segs]
    def dist(p):
        best = 9.0
        for a, b in segs:
            ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9))); best = min(best, (a + ab * t - p).length)
        return best
    for o, part in worn:
        for x in bpy.data.objects: x.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        for m in [m for m in o.modifiers if m.type == 'ARMATURE']: o.modifiers.remove(m)
        dt = o.modifiers.new('weights', 'DATA_TRANSFER'); dt.object = body; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
        dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.datalayout_transfer(modifier=dt.name); bpy.ops.object.modifier_apply(modifier=dt.name)
        bones = {b.name for b in rig.data.bones}
        for g in list(o.vertex_groups):
            if g.name not in bones: o.vertex_groups.remove(g)
        arm_groups = {g.index for g in o.vertex_groups if any(k in g.name for k in ('upperarm', 'lowerarm', 'hand_', 'thumb', 'index', 'middle', 'ring', 'pinky'))}
        spine = o.vertex_groups.get('spine_03')
        for v in o.data.vertices:
            f = 0.0
            if part in ('top', 'dress', 'outer', 'split'):
                d = dist(o.matrix_world @ v.co); k = max(0.0, min(1.0, (0.11 - d) / 0.04)); f = k * k * (3 - 2 * k)
            if f >= 1: continue
            moved = 0.0
            for g in v.groups:
                if g.group in arm_groups and g.weight > 0: moved += g.weight * (1 - f); g.weight *= f
            if moved > 0 and spine is not None: spine.add([v.index], moved, 'ADD')
        if o.vertex_groups:
            with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]): bpy.ops.object.vertex_group_normalize_all(lock_active=False)
        w = o.matrix_world.copy(); o.parent = rig; o.matrix_world = w
        am = o.modifiers.new('rig', 'ARMATURE'); am.object = rig
    # scale and place her so her neck meets olivia's, then pose her limbs onto olivia's joints
    bpy.context.view_layer.update()
    neck = rig.matrix_world @ pb['neck_01'].head; k = J['Neck'].z / neck.z
    rig.matrix_world = Matrix.Translation(Vector((-neck.x * k, J['Neck'].y - neck.y * k, 0))) @ Matrix.Scale(k, 4) @ rig.matrix_world
    bpy.context.view_layer.update()
    for s, S in (('l', 'Left'), ('r', 'Right')):
        aim(rig, 'thigh_' + s, J[S + 'UpLeg'], J[S + 'Leg']); aim(rig, 'calf_' + s, J[S + 'Leg'], J[S + 'Foot'])
        aim(rig, 'upperarm_' + s, J[S + 'Arm'], J[S + 'ForeArm']); aim(rig, 'lowerarm_' + s, J[S + 'ForeArm'], J[S + 'Hand'])
    for o in [body] + [g for g, p in worn]:
        cs = o.modifiers.new('ease', 'CORRECTIVE_SMOOTH'); cs.rest_source = 'ORCO'; cs.smooth_type = 'LENGTH_WEIGHTED'; cs.iterations = 6; cs.factor = 0.5; cs.use_pin_boundary = True
    # bake her posed body and the posed garments
    dg = bpy.context.evaluated_depsgraph_get()
    def bake(o, name):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg); me.transform(o.matrix_world)
        b = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(b)
        for m in o.data.materials: pass
        return b
    src = bake(body, 'src'); baked = [(bake(o, 'g_' + part), part, o) for o, part in worn]
    # the garments follow her body as it is shrink-wrapped onto olivia's
    for g, part, orig in baked:
        sd = g.modifiers.new('follow', 'SURFACE_DEFORM'); sd.target = src; sd.falloff = 4
        with bpy.context.temp_override(object=g, active_object=g): bpy.ops.object.surfacedeform_bind(modifier=sd.name)
    sw = src.modifiers.new('onto', 'SHRINKWRAP'); sw.target = target; sw.wrap_method = 'NEAREST_SURFACEPOINT'; sw.wrap_mode = 'ON_SURFACE'
    sm = src.modifiers.new('smooth', 'CORRECTIVE_SMOOTH'); sm.rest_source = 'ORCO'; sm.iterations = 4; sm.factor = 0.3
    bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(target, dg)
    out = os.path.join(STAR, sid); os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith(('.png', '.fbx', '.json')): os.remove(os.path.join(out, f))
    finals, parts = [], []
    waist = J['Hips'].z + 0.05
    work = []
    for g, part, orig in baked:
        me0 = bpy.data.meshes.new_from_object(g.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        if part != 'split': work.append((me0, part, orig)); continue
        for keep, name in ((True, 'top'), (False, 'bottom')):
            me1 = me0.copy(); bm = bmesh.new(); bm.from_mesh(me1); bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.calc_center_median().z > waist) != keep], context='FACES')
            bm.to_mesh(me1); bm.free()
            if len(me1.polygons) > 30: work.append((me1, name, orig))
    for me, part, orig in work:
        # loose fabric (a skirt's folds, away from the legs) is eased smooth: posing the legs creases it
        if part in ('dress', 'bottom'):
            hip = J['Hips'].z; bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
            loose = []
            for v in bm.verts:
                if v.co.z > hip - 0.05 or v.is_boundary: continue     # hems and seams stay put: moving them parts the pieces sewn there
                q, nr, _, d = tree.find_nearest(v.co, 0.3)
                if q is not None and (v.co - q).length > 0.025: loose.append(v)
            for _ in range(4):
                moved = {}
                for v in loose:
                    ns = [e.other_vert(v).co for e in v.link_edges]
                    if ns: moved[v] = v.co.lerp(sum(ns, Vector()) / len(ns), 0.5)
                for v, c in moved.items(): v.co = c
            bm.to_mesh(me); bm.free()
        # clear of the skin by a few millimetres, so it never shows through
        for v in me.vertices:
            q, nr, _, d = tree.find_nearest(v.co, 0.05)
            if q is None: continue
            gap = (0.004 if part != 'shoes' else 0.002 if v.co.z < 0.08 else 0.008) + (0.006 if part == 'outer' else 0)   # a boot's shaft stands clear of the ankle
            if (v.co - q).dot(nr) < gap: v.co = q + nr * gap
        f = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(f); me.name = part
        diffuse, normal = textures(orig)
        nm = bpy.data.materials.new(MAT[part]); me.materials.clear(); me.materials.append(nm)
        for p in me.polygons: p.use_smooth = True
        def save_tex(img, name):
            if img is None: return ''
            src = bpy.path.abspath(img.filepath); dst = os.path.join(out, name + '.png')
            try:
                if src.lower().endswith('.png') and os.path.exists(src): import shutil; shutil.copyfile(src, dst)
                else:
                    im = bpy.data.images.load(src) if os.path.exists(src) else img; sc = bpy.context.scene
                    sc.render.image_settings.file_format = 'PNG'; im.save_render(dst, scene=sc)
                return name + '.png'
            except Exception as e: print('TEX', sid, name, e); return ''
        tex = save_tex(diffuse, part); ntex = save_tex(normal, part + '_n')
        if sid in PLAIN:
            diffuse = plain_weave(os.path.join(out, part + '.png'), False); plain_weave(os.path.join(out, part + '_n.png'), True)
            tex, ntex = part + '.png', part + '_n.png'
        if diffuse is not None and not diffuse.has_data:
            try: diffuse.reload()
            except Exception: pass
        rgb, lum = tone(diffuse)
        zs = [v.co.z for v in me.vertices]; xs = [abs(v.co.x) for v in me.vertices]
        parts.append({'mesh': part, 'material': MAT[part], 'tex': tex, 'normal': ntex, 'alpha': False, 'color': [1, 1, 1, 1], 'rough': 0.7, 'metal': 0.0,
                      'tone': rgb, 'lum': lum, 'zmin': round(min(zs), 3), 'zmax': round(max(zs), 3), 'xmax': round(max(xs), 3), 'tris': sum(len(p.vertices) - 2 for p in me.polygons)})
        finals.append(f)
    for o in [x for x in bpy.data.objects if x not in finals and x is not target]: bpy.data.objects.remove(o, do_unlink=True)
    target.hide_set(True); target.select_set(False)
    for f in finals: f.hide_set(False); f.select_set(True)
    bpy.context.view_layer.objects.active = finals[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    json.dump({'id': sid, 'scale': 1.0, 'marks': {}, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    blend = os.path.join(PROJECT, 'Art3D', 'fashion'); os.makedirs(blend, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend, sid + '.blend'), copy=True)
    for f in finals: bpy.data.objects.remove(f, do_unlink=True)
    target.hide_set(False)
    print('FASHION', sid, ' '.join(f"{p['mesh']}[{p['zmin']}-{p['zmax']} {p['tris']}]" for p in parts), flush=True)

clear()
J = olivia_joints(); target = target_surface()
print('TARGET', round(max(v.co.z for v in target.data.vertices), 3), len(target.data.vertices))
for sid, garments in SETS.items():
    if ONLY and sid not in ONLY: continue
    try: make(sid, garments, J, target)
    except Exception as e:
        import traceback; traceback.print_exc(); print('FAIL', sid, repr(e), flush=True)
