# Generates Starlife's men (and the town's passers-by) with MPFB (MakeHuman for Blender; CC0 / CC-BY assets) and saves
# each as an avatar .blend shaped like the Ready Player Me ones, so wardrobe_export.py and rig_export.py take them as
# they are: the skeleton uses the same bone names (Hips, Spine, ... LeftToeBase), the body is cut into head and body,
# and materials are named the way wardrobe_export.py sorts pieces (Wolf3D_Outfit_Top, ..., SL_Beard, SL_Eyebrow).
#   Blender -b -P Tools/blender/make_men.py -- <project> [id ...]
# Then, per id: wardrobe_export.py (with --nosculpt) and rig_export.py; Tools/blender/make_men.sh does all three.
# Do not call read_factory_settings here: it switches MPFB off.
import bpy, bmesh, os, sys, math
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

args = sys.argv[sys.argv.index('--') + 1:]
PROJECT = args[0]; ONLY = set(args[1:])
ART = os.path.join(PROJECT, 'Art3D', 'avatars')
DATA = LocationService.get_user_data()
M, F = 1.0, 0.0
AF, AS, CA = {'african': 1.0, 'asian': 0.0, 'caucasian': 0.0}, {'african': 0.0, 'asian': 1.0, 'caucasian': 0.0}, {'african': 0.0, 'asian': 0.0, 'caucasian': 1.0}
MIX = lambda a, s, c: {'african': a, 'asian': s, 'caucasian': c}
# the men's shared body: every piece of menswear is made on this one, so each fits every man (as the women's do on olivia)
BASE = dict(gender=M, age=0.45, muscle=0.55, weight=0.48, height=0.55, race=MIX(0.34, 0.33, 0.33), skin='young_caucasian_male')

# clothes: (MPFB asset, part) with part top / bottom / shoes / suit (cut into top and bottom at the hips) / beard
PEOPLE = {
    # the five starlets (the web game's men)
    'kai':  dict(gender=M, age=0.42, muscle=0.6, weight=0.45, height=0.6, race=AF, skin='young_african_male', hair='elvs_braided_rows', brows='eyebrow002',
                 clothes=[('elvs_male_logo_tshirt1', 'top'), ('punkduck_male_classic_jeans', 'bottom'), ('culturalibre_sneakers', 'shoes'), ('elvs_scruffy_beard1', 'beard')]),
    'leo':  dict(gender=M, age=0.45, muscle=0.55, weight=0.47, height=0.62, race=MIX(0.15, 0.0, 0.85), skin='young_caucasian_male', hair='short02', brows='eyebrow002',
                 clothes=[('male_elegantsuit01', 'suit'), ('mindfront_shoes_oxford_male', 'shoes'), ('grinsegold_moustache', 'beard')]),
    'jin':  dict(gender=M, age=0.38, muscle=0.5, weight=0.4, height=0.5, race=AS, skin='young_asian_male', hair='short04', brows='eyebrow001',
                 clothes=[('sl_monogram_hoodie', 'top'), ('mindfront_male_trousers_1', 'bottom'), ('sl_knit_runners', 'shoes')]),
    'theo': dict(gender=M, age=0.4, muscle=0.85, weight=0.5, height=0.66, race=AF, skin='young_african_male', hair='short01', brows='eyebrow003',
                 clothes=[('namuhekam_male_polo_shirt', 'top'), ('elvs_male_trouser_short_1', 'bottom'), ('punkduck_running_shoes_01', 'shoes'), ('culturalibre_dal_moustache', 'beard')]),
    'omar': dict(gender=M, age=0.55, muscle=0.5, weight=0.55, height=0.56, race=MIX(0.15, 0.15, 0.7), skin='young_caucasian_male', hair='short03', brows='eyebrow002',
                 clothes=[('elvs_male_shirt_untucked_bd1', 'top'), ('toigo_wool_pants', 'bottom'), ('sl_suede_chelsea', 'shoes'), ('grinsegold_full_beard', 'beard')]),
    # the shared body in athletic shorts (his underwear), and the menswear, all on that same body
    'manbase':   dict(BASE, hair='short01', clothes=[('elvs_male_trouser_short_1', 'bottom')]),
    'm_suit':    dict(BASE, hair='short01', clothes=[('male_elegantsuit01', 'suit'), ('mindfront_shoes_oxford_male', 'shoes')]),
    'm_shirt':   dict(BASE, hair='short01', clothes=[('elvs_male_shirt_tie_tucked1', 'top'), ('elvs_male_trouser', 'bottom'), ('toigo_ankle_boots_male', 'shoes')]),
    'm_polo':    dict(BASE, hair='short01', clothes=[('namuhekam_male_polo_shirt', 'top'), ('mindfront_male_trousers_1', 'bottom'), ('sl_knit_runners', 'shoes')]),
    'm_hoodie':  dict(BASE, hair='short01', clothes=[('elvs_hooded_sweat_jacket1', 'top'), ('punkduck_male_classic_jeans', 'bottom'), ('culturalibre_sneakers', 'shoes')]),
    'm_tee':     dict(BASE, hair='short01', clothes=[('sl_white_tee', 'top'), ('cortu_jeans_shorts', 'bottom'), ('elvs_male_flip_flop_sandals1', 'shoes')]),
    'm_casual':  dict(BASE, hair='short01', clothes=[('elvs_male_tankshirt1', 'top'), ('sl_chocolate_cargo', 'bottom'), ('shoes02', 'shoes')]),
    'm_tweed':   dict(BASE, hair='short01', clothes=[('sl_tweed_suit', 'suit'), ('shoes04', 'shoes')]),
    # passers-by in Starlife Town, each their own person
    'p_ada':    dict(gender=F, age=0.5, muscle=0.5, weight=0.55, height=0.45, race=AF, skin='young_african_female', hair='elvs_micky_afro', brows='eyebrow003',
                 clothes=[('punkduck_retro_top', 'top'), ('toigo_harem_pants', 'bottom'), ('toigo_flats', 'shoes')]),
    'p_grace':  dict(gender=F, age=0.62, muscle=0.5, weight=0.6, height=0.5, race=CA, skin='young_caucasian_female', hair='bob02', brows='eyebrow001',
                 clothes=[('female_elegantsuit01', 'suit'), ('toigo_flats', 'shoes')]),
    'p_mei':    dict(gender=F, age=0.32, muscle=0.5, weight=0.42, height=0.42, race=AS, skin='young_asian_female', hair='ponytail01', brows='eyebrow001',
                 clothes=[('punkduck_v_neck_top', 'top'), ('punkduck_female_tight_jeans', 'bottom'), ('punkduck_running_shoes_01', 'shoes')]),
    'p_priya':  dict(gender=F, age=0.4, muscle=0.5, weight=0.5, height=0.47, race=MIX(0.2, 0.6, 0.2), skin='cutoff3d_indian_female_skin', hair='long01', brows='eyebrow002',
                 clothes=[('sl_silk_carre_blouse', 'top'), ('sl_cream_wideleg', 'bottom'), ('dressupdoc_sandals1', 'shoes')]),
    'p_sam':    dict(gender=M, age=0.3, muscle=0.5, weight=0.42, height=0.6, race=CA, skin='young_caucasian_male', hair='o4saken_curly01', brows='eyebrow001',
                 clothes=[('drednicolson_short-tail_camo_tee', 'top'), ('cortu_cargo_pants', 'bottom'), ('sl_black_runners', 'shoes')]),
    'p_femi':   dict(gender=M, age=0.7, muscle=0.5, weight=0.65, height=0.5, race=AF, skin='middleage_african_male', hair='short03', brows='eyebrow002',
                 clothes=[('sl_pinstripe_suit', 'suit'), ('mindfront_shoes_oxford_male', 'shoes'), ('grinsegold_moustache', 'beard')]),
    'p_ken':    dict(gender=M, age=0.5, muscle=0.6, weight=0.5, height=0.55, race=AS, skin='young_asian_male', hair='short02', brows='eyebrow001',
                 clothes=[('sl_monogram_shirt', 'top'), ('sl_raw_denim', 'bottom'), ('sl_cream_oxford', 'shoes')]),
    'p_rosa':   dict(gender=F, age=0.75, muscle=0.45, weight=0.62, height=0.42, race=MIX(0.2, 0.0, 0.8), skin='callharvey3d_midtoned_female', hair='bob01', brows='eyebrow002',
                 clothes=[('mindfront_f_dress_05', 'suit'), ('toigo_flats', 'shoes')]),
    # more of the town (shop and restaurant staff and their customers come from these too); face: a seed for face_shape();
    # MPFB's age is 0.5 at 25 and 1.0 at 90 (0.3 is a teenager), so these grown-ups are 0.5 and up
    'p_amara':  dict(gender=F, age=0.52, muscle=0.5, weight=0.45, height=0.55, race=AF, skin='young_african_female', hair='afro01', brows='eyebrow003', face=11,
                 clothes=[('female_casualsuit02', 'suit'), ('culturalibre_sneakers', 'shoes')]),
    'p_chioma': dict(gender=F, age=0.68, muscle=0.45, weight=0.66, height=0.46, race=AF, skin='middleage_african_female', hair='elvs_braid_bun', brows='eyebrow002', face=12,
                 clothes=[('sl_ankara_maxi', 'suit'), ('dressupdoc_sandals1', 'shoes')]),
    'p_lena':   dict(gender=F, age=0.55, muscle=0.5, weight=0.44, height=0.58, race=CA, skin='young_caucasian_female', hair='braid01', brows='eyebrow001', face=13,
                 clothes=[('punkduck_off-shoulder_long-sleeve_top', 'top'), ('mindfront_female_trousers_1', 'bottom'), ('elvs_flatshoe_plain1', 'shoes')]),
    'p_yuki':   dict(gender=F, age=0.5, muscle=0.45, weight=0.4, height=0.4, race=AS, skin='young_asian_female', hair='bob01', brows='eyebrow001', face=14,
                 clothes=[('toigo_shift_dress', 'suit'), ('elvs_flatshoe_plain1', 'shoes')]),
    'p_sofia':  dict(gender=F, age=0.58, muscle=0.5, weight=0.52, height=0.47, race=MIX(0.15, 0.05, 0.8), skin='callharvey3d_midtoned_female', hair='punkduck_alpha7_curly', brows='eyebrow002', face=15,
                 clothes=[('toigo_halter_dress_midi', 'suit'), ('dressupdoc_sandals1', 'shoes')]),
    'p_nadia':  dict(gender=F, age=0.53, muscle=0.5, weight=0.47, height=0.5, race=MIX(0.3, 0.3, 0.4), skin='cutoff3d_indian_female_skin', hair='long01', brows='eyebrow002', face=16,
                 clothes=[('sl_butter_offshoulder', 'top'), ('punkduck_female_short_jeans', 'bottom'), ('sl_black_runners', 'shoes')]),
    'p_bisi':   dict(gender=F, age=0.86, muscle=0.4, weight=0.6, height=0.42, race=AF, skin='middleage_african_female', hair='bob02', brows='eyebrow002', face=17,
                 clothes=[('female_elegantsuit01', 'suit'), ('elvs_flatshoe_plain1', 'shoes')]),
    'p_tunde':  dict(gender=M, age=0.55, muscle=0.75, weight=0.5, height=0.62, race=AF, skin='young_african_male', hair='short04', brows='eyebrow003', face=21,
                 clothes=[('elvs_crude_t-shirt_male', 'top'), ('punkduck_male_classic_jeans', 'bottom'), ('culturalibre_sneakers', 'shoes'), ('elvs_scruffy_beard1', 'beard')]),
    'p_raj':    dict(gender=M, age=0.6, muscle=0.5, weight=0.5, height=0.52, race=MIX(0.2, 0.6, 0.2), skin='young_asian_male', hair='short01', brows='eyebrow002', face=22,
                 clothes=[('male_casualsuit03', 'suit'), ('shoes03', 'shoes')]),
    'p_marco':  dict(gender=M, age=0.7, muscle=0.5, weight=0.62, height=0.55, race=CA, skin='young_caucasian_male', hair='short03', brows='eyebrow002', face=23,
                 clothes=[('male_casualsuit05', 'suit'), ('shoes05', 'shoes'), ('grinsegold_full_beard', 'beard')]),
    'p_kwame':  dict(gender=M, age=0.88, muscle=0.45, weight=0.58, height=0.5, race=AF, skin='middleage_african_male', hair='short01', brows='eyebrow002', face=24,
                 clothes=[('male_worksuit01', 'suit'), ('toigo_ankle_boots_male', 'shoes'), ('grinsegold_moustache', 'beard')]),
    'p_hiro':   dict(gender=M, age=0.5, muscle=0.6, weight=0.4, height=0.5, race=AS, skin='young_asian_male', hair='punkduck_alpha7_curly', brows='eyebrow001', face=25,
                 clothes=[('elvs_male_muscle_shirt1', 'top'), ('cortu_cargo_pants', 'bottom'), ('punkduck_running_shoes_01', 'shoes')]),
    'p_luis':   dict(gender=M, age=0.62, muscle=0.55, weight=0.55, height=0.57, race=MIX(0.3, 0.0, 0.7), skin='young_caucasian_male', hair='afro01', brows='eyebrow003', face=26,
                 clothes=[('namuhekam_male_polo_shirt', 'top'), ('elvs_male_trouser', 'bottom'), ('shoes01', 'shoes'), ('culturalibre_dal_moustache', 'beard')]),
}
# a face of its own: MakeHuman's face targets (nose, jaw, chin, cheekbones, eyes, lips, ears, head shape), picked from a seed
FACE_AXES = ['nose-scale-horiz', 'nose-scale-vert', 'nose-point-width', 'nose-hump', 'nose-flaring', 'chin-width', 'chin-prominent', 'chin-height',
             'head-scale-horiz', 'mouth-scale-horiz', 'mouth-lowerlip-volume', 'cheek-bones', 'eye-scale', 'ear-scale']
HEADS = ['head-oval', 'head-round', 'head-square', 'head-diamond', 'head-triangular', 'head-rectangular', 'head-invertedtriangular']
def face_shape(body, seed):
    import random
    from bl_ext.blender_org.mpfb.services.targetservice import TargetService
    r = random.Random(seed); td = LocationService.get_mpfb_data('targets')
    folder = {'nose': 'nose', 'chin': 'chin', 'head': 'head', 'mouth': 'mouth', 'cheek': 'cheek', 'eye': 'eyes', 'ear': 'ears'}
    picks = [(a + ('-incr' if r.random() < 0.5 else '-decr'), r.uniform(0.15, 0.65)) for a in FACE_AXES]
    picks.append((r.choice(HEADS), r.uniform(0.3, 0.7)))
    for name, w in picks:
        sides = ['l-', 'r-'] if name.split('-')[0] in ('cheek', 'eye', 'ear') else ['']
        for sd in sides:
            path = os.path.join(td, folder[name.split('-')[0]], sd + name + '.target.gz')
            if os.path.exists(path): TargetService.load_target(body, path, weight=w, name=sd + name)
            else: print('NO TARGET', path)

# the game-engine skeleton's bones, by the Ready Player Me names the rest of the pipeline uses
RENAME = {'pelvis': 'Hips', 'spine_01': 'Spine', 'spine_02': 'Spine1', 'spine_03': 'Spine2', 'neck_01': 'Neck', 'head': 'Head',
          'clavicle_l': 'LeftShoulder', 'upperarm_l': 'LeftArm', 'lowerarm_l': 'LeftForeArm', 'hand_l': 'LeftHand',
          'clavicle_r': 'RightShoulder', 'upperarm_r': 'RightArm', 'lowerarm_r': 'RightForeArm', 'hand_r': 'RightHand',
          'thigh_l': 'LeftUpLeg', 'calf_l': 'LeftLeg', 'foot_l': 'LeftFoot', 'ball_l': 'LeftToeBase',
          'thigh_r': 'RightUpLeg', 'calf_r': 'RightLeg', 'foot_r': 'RightFoot', 'ball_r': 'RightToeBase'}
MAT = {'top': 'Wolf3D_Outfit_Top', 'bottom': 'Wolf3D_Outfit_Bottom', 'shoes': 'Wolf3D_Outfit_Footwear', 'beard': 'SL_Beard'}

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

def bvh(objs):
    dg = bpy.context.evaluated_depsgraph_get(); bm = bmesh.new()
    for o in objs:
        ev = o.evaluated_get(dg); me = ev.to_mesh(); me.transform(o.matrix_world); bm.from_mesh(me); ev.to_mesh_clear()
    bm.normal_update(); return BVHTree.FromBMesh(bm)

def fit(body, garments, margin=0.005):
    """Push every garment just clear of what is under it (shoes first, then bottoms, then tops), so no skin shows through."""
    order = {'shoes': 0, 'bottom': 1, 'suit': 2, 'top': 3}
    below = [body]
    for o, part in sorted(garments, key=lambda g: order.get(g[1], 4)):
        if part == 'shoes': continue
        tree = bvh(below); mw = o.matrix_world; inv = mw.inverted()
        for v in o.data.vertices:
            p = mw @ v.co; q, nr, _, d = tree.find_nearest(p, 0.03)
            if q is None: continue
            out = p - q
            if out.dot(nr) < 0 or out.length < margin: v.co = inv @ (q + nr * margin)
        o.data.update(); below.append(o)

def all_images(tree, seen=None):
    seen = seen or set()
    for n in tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image: yield n.image
        if n.type == 'GROUP' and n.node_tree and n.node_tree.name not in seen:
            seen.add(n.node_tree.name); yield from all_images(n.node_tree, seen)

def simple_material(o, name, alpha):
    """MPFB's materials are node groups: rebuild as image -> Principled (colour, alpha for cards, normal) under the given name."""
    m = o.material_slots[0].material if o.material_slots else None
    imgs = list(all_images(m.node_tree)) if m and m.use_nodes else []
    diffuse = next((i for i in imgs if any(k in i.name.lower() for k in ('diffuse', 'albedo', '_color', 'basecolor', '_diff'))), None)
    normal = next((i for i in imgs if 'normal' in i.name.lower() or '_nrm' in i.name.lower()), None)
    if diffuse is None:
        rest = [i for i in imgs if i is not normal and not any(k in i.name.lower() for k in ('rough', 'spec', 'ao', 'bump', 'sss', 'disp', 'metal', 'trans'))]
        diffuse = rest[0] if rest else None
    nm = bpy.data.materials.new(name); nm.use_nodes = True; nt = nm.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); p = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(p.outputs['BSDF'], out.inputs['Surface'])
    p.inputs['Roughness'].default_value = 0.6
    if diffuse:
        d = nt.nodes.new('ShaderNodeTexImage'); d.image = diffuse; nt.links.new(d.outputs['Color'], p.inputs['Base Color'])
        if alpha: nt.links.new(d.outputs['Alpha'], p.inputs['Alpha'])
    if normal:
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = normal; n.image.colorspace_settings.name = 'Non-Color'
        mm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(n.outputs['Color'], mm.inputs['Color']); nt.links.new(mm.outputs['Normal'], p.inputs['Normal'])
    o.data.materials.clear(); o.data.materials.append(nm)

def split_by_weight(o, keys, name_in, name_out, above=None, below=None):
    """Faces mostly weighted to bones named in keys (and, if given, above that height) go to a copy named name_in;
    the rest stay in o, renamed name_out."""
    idx = {g.index for g in o.vertex_groups if g.name in keys}; bones = {g.index for g in o.vertex_groups if g.name in RENAME.values()}
    w = [sum(g.weight for g in v.groups if g.group in idx) / max(1e-6, sum(g.weight for g in v.groups if g.group in bones)) for v in o.data.vertices]
    c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c)
    for obj, keep_in in ((c, True), (o, False)):
        bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
        inside = lambda f: sum(w[v.index] for v in f.verts) / len(f.verts) > 0.5 and (above is None or (obj.matrix_world @ f.calc_center_median()).z > above) and (below is None or (obj.matrix_world @ f.calc_center_median()).z < below)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if inside(f) != keep_in], context='FACES')
        bm.to_mesh(obj.data); bm.free()
    c.name = name_in; o.name = name_out
    return c, o

def swing(rig, bone, deg, axis='Y'):
    """Turns a pose bone about a world axis through its head (the arms come down from MPFB's A-pose this way)."""
    pb = rig.pose.bones[bone]; mw = rig.matrix_world @ pb.matrix; head = mw.translation.copy()
    r = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-head)
    pb.matrix = rig.matrix_world.inverted() @ r @ mw; bpy.context.view_layer.update()

def make(pid, spec):
    clear()
    macro = {'gender': spec['gender'], 'age': spec['age'], 'muscle': spec.get('muscle', 0.5), 'weight': spec['weight'], 'proportions': 0.5,
             'height': 0.5 + (spec['height'] - 0.5) * 0.45, 'cupsize': 0.5, 'firmness': 0.5, 'race': spec['race']}
    body = HumanService.create_human(macro_detail_dict=macro, scale=0.1); body.name = pid
    HumanService.set_character_skin(asset('skins', spec['skin'], '.mhmat'), body, skin_type='MAKESKIN')
    if 'face' in spec: face_shape(body, spec['face'])
    before = set(bpy.data.objects)
    HumanService.add_mhclo_asset(asset('eyes', 'low-poly', '.mhclo'), body, asset_type='Eyes', subdiv_levels=0); eyes = set(bpy.data.objects) - before; before = set(bpy.data.objects)
    HumanService.add_mhclo_asset(asset('eyebrows', spec.get('brows', 'eyebrow001'), '.mhclo'), body, asset_type='Eyebrows', subdiv_levels=0); brows = set(bpy.data.objects) - before; before = set(bpy.data.objects)
    HumanService.add_mhclo_asset(asset('eyelashes', 'eyelashes01', '.mhclo'), body, asset_type='Eyelashes', subdiv_levels=0); lashes = set(bpy.data.objects) - before; before = set(bpy.data.objects)
    HumanService.add_mhclo_asset(asset('hair', spec['hair'], '.mhclo'), body, asset_type='Hair', subdiv_levels=0); hair = set(bpy.data.objects) - before
    worn = []
    for c, part in spec['clothes']:
        before = set(bpy.data.objects)
        HumanService.add_mhclo_asset(asset('clothes', c, '.mhclo'), body, asset_type='Clothes', subdiv_levels=0)
        for o in set(bpy.data.objects) - before:
            if o.type == 'MESH': worn.append((o, part))
    fit(body, [(o, p) for o, p in worn if p != 'beard'])
    clothes_objs = {o for o, p in worn if p in ('top', 'suit', 'bottom', 'shoes')}; torso_objs = {o for o, p in worn if p in ('top', 'suit')}
    HumanService.add_builtin_rig(body, 'game_engine')
    rig = body.parent if body.parent and body.parent.type == 'ARMATURE' else next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    rig.name = pid + '_rig'
    for o in bpy.data.objects:
        for m in list(getattr(o, 'modifiers', [])):
            if m.type == 'SUBSURF': o.modifiers.remove(m)
    # MPFB rigs only the body: hair, clothes, brows and lashes take the body's weights
    for o in [x for x in bpy.data.objects if x.type == 'MESH' and x is not body]:
        if any(m.type == 'ARMATURE' for m in o.modifiers): continue
        for x in bpy.data.objects: x.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        dt = o.modifiers.new('weights', 'DATA_TRANSFER'); dt.object = body; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
        dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.datalayout_transfer(modifier=dt.name); bpy.ops.object.modifier_apply(modifier=dt.name)
        bones = {b.name for b in rig.data.bones}
        for g in list(o.vertex_groups):
            if g.name not in bones: o.vertex_groups.remove(g)
        # a shirt or jacket hem hangs beside the hands in the A-pose and would follow them when the arms come down:
        # below the armpits and inside the shoulders, cloth follows the body, not the arms
        if o in clothes_objs:
            # cloth follows the arms only where it is a sleeve: near the arm's bones it keeps the arm's weights, beyond
            # 11 cm it follows the body, and in between it blends, so seams and hems neither tear nor stand out
            arm_groups = {g.index for g in o.vertex_groups if any(k in g.name for k in ('upperarm', 'lowerarm', 'hand_', 'thumb', 'index', 'middle', 'ring', 'pinky'))}
            spine = o.vertex_groups.get('spine_03') or o.vertex_groups.get('spine_02')
            segs = []
            for side in ('l', 'r'):
                pb = rig.pose.bones
                a, e, h = (rig.matrix_world @ pb['upperarm_' + side].head, rig.matrix_world @ pb['lowerarm_' + side].head, rig.matrix_world @ pb['hand_' + side].tail)
                segs += [(a, e), (e, h)]
            def dist(p):
                best = 9.0
                for a, b in segs:
                    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9))); best = min(best, (a + ab * t - p).length)
                return best
            for v in o.data.vertices:
                if o in torso_objs:
                    d = dist(o.matrix_world @ v.co); k = max(0.0, min(1.0, (0.11 - d) / 0.04)); f = k * k * (3 - 2 * k)
                else: f = 0.0          # trousers and shoes never follow the arms
                if f >= 1: continue
                moved = 0.0
                for g in v.groups:
                    if g.group in arm_groups and g.weight > 0: moved += g.weight * (1 - f); g.weight *= f
                if moved > 0 and spine is not None: spine.add([v.index], moved, 'ADD')
        if o.vertex_groups:
            with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
                bpy.ops.object.vertex_group_normalize_all(lock_active=False)
        w = o.matrix_world.copy(); o.parent = rig; o.matrix_world = w
        am = o.modifiers.new('rig', 'ARMATURE'); am.object = rig
    # Ready Player Me bone names, on the skeleton and on every mesh's weights
    for b in rig.data.bones:
        if b.name in RENAME: b.name = RENAME[b.name]
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        for g in o.vertex_groups:
            if g.name in RENAME: g.name = RENAME[g.name]
    # MPFB's hidden helper geometry (joint cubes, hair and clothes helpers) goes for good, so meshes match their evaluated selves
    if body.data.shape_keys:
        with bpy.context.temp_override(object=body, active_object=body, selected_objects=[body]): bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    for m in [m for m in body.modifiers if m.type == 'MASK']:
        with bpy.context.temp_override(object=body, active_object=body): bpy.ops.object.modifier_apply(modifier=m.name)
    # arms down at her sides as the starlets stand: each arm aimed from the shoulder to where their hands are, elbow eased
    bpy.context.view_layer.update()
    for side in ('Left', 'Right'):
        a = rig.pose.bones[side + 'Arm']; hand = rig.pose.bones[side + 'Hand']
        sh = rig.matrix_world @ a.head; hd = rig.matrix_world @ hand.head; out = 1 if hd.x > sh.x else -1
        want = Vector((0.13 * out, -0.05, -0.47)).normalized(); have = (hd - sh).normalized()
        q = have.rotation_difference(want); mw = rig.matrix_world @ a.matrix
        a.matrix = rig.matrix_world.inverted() @ (Matrix.Translation(sh) @ q.to_matrix().to_4x4() @ Matrix.Translation(-sh) @ mw); bpy.context.view_layer.update()
    # lowering the arms bunches cloth at the armpits and shoulder seams: a corrective smooth (against the cloth's own
    # rest shape) irons that out, around the shoulders only, so the rest of the garment keeps its detail
    sh = [rig.matrix_world @ rig.pose.bones[b].head for b in ('LeftArm', 'RightArm')]
    for o in [g for g, p in worn if p in ('top', 'suit')] + [body]:
        vg = o.vertex_groups.new(name='sl_shoulders')
        for v in o.data.vertices:
            d = min(((o.matrix_world @ v.co) - c).length for c in sh); k = max(0.0, min(1.0, (0.24 - d) / 0.12))
            if k > 0: vg.add([v.index], k * k * (3 - 2 * k), 'REPLACE')
        cs = o.modifiers.new('ease', 'CORRECTIVE_SMOOTH'); cs.rest_source = 'ORCO'; cs.smooth_type = 'LENGTH_WEIGHTED'
        cs.iterations = 12; cs.factor = 0.6; cs.vertex_group = vg.name; cs.use_pin_boundary = True
    # the starlets (Ready Player Me) have heads about a fifth larger for their bodies than MakeHuman's: everything on
    # the head grows 15% about the top of the neck, easing into the neck over 8 cm so there is no step
    pivot = rig.matrix_world @ rig.pose.bones['Head'].head
    for o in [body] + [x for x in list(hair) + list(brows) + list(lashes) + list(eyes) if x.type == 'MESH'] + [g for g, p in worn if p == 'beard']:
        mw = o.matrix_world; inv = mw.inverted()
        for v in o.data.vertices:
            w = mw @ v.co; k = max(0.0, min(1.0, (w.z - (pivot.z - 0.08)) / 0.08)); k = k * k * (3 - 2 * k)
            if k <= 0: continue
            v.co = inv @ (pivot + (w - pivot) * (1 + 0.15 * k))
        o.data.update()
    # the body cut into head and body (the face and neck follow the head), skin named as the starlets' is
    shoulder = (rig.matrix_world @ rig.pose.bones['LeftArm'].head).z
    head, rest = split_by_weight(body, {'Head', 'Neck'}, 'mpfb_head', 'mpfb_body', above=shoulder)     # not 'head' / 'body': wardrobe_export.py names its pieces so
    simple_material(head, 'Wolf3D_Skin', False); simple_material(rest, 'Wolf3D_Body', False)
    for o in eyes:
        if o.type == 'MESH': simple_material(o, 'Wolf3D_Eye', False)
    for o in brows:
        if o.type == 'MESH': simple_material(o, 'SL_Eyebrow', True)
    for o in lashes:
        if o.type == 'MESH': simple_material(o, 'SL_Eyelash', True)
    for o in hair:
        if o.type == 'MESH': simple_material(o, 'Wolf3D_Hair', True)
    for o, part in worn:
        if part == 'suit':
            # cut at the crotch: everything above (the jacket, its hem and the top of the trousers under it) is the top
            crotch = (rig.matrix_world @ rig.pose.bones['Hips'].head).z - 0.09
            bottom, top = split_by_weight(o, {g.name for g in o.vertex_groups}, o.name + '_bottom', o.name + '_top', below=crotch)
            simple_material(top, MAT['top'], False); simple_material(bottom, MAT['bottom'], False)
        else: simple_material(o, MAT[part], part == 'beard')
    # lighter shoes, hair and clothes for the game (the body and head keep their full mesh); weights survive the decimation
    for o in [x for x in list(hair) + [g for g, p in worn if p == 'shoes'] if x.type == 'MESH' and len(x.data.polygons) > 9000]:
        m = o.modifiers.new('lighter', 'DECIMATE'); m.ratio = 9000 / len(o.data.polygons)
        while o.modifiers[0] != m:
            with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_move_up(modifier=m.name)
        with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)
    os.makedirs(ART, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, pid + '.blend'))
    h = max((o.matrix_world @ Vector(c)).z for o in bpy.data.objects if o.type == 'MESH' for c in o.bound_box)
    print(f'MAN {pid}: {h:.2f} m, ' + ', '.join(sorted({s.material.name for o in bpy.data.objects if o.type == 'MESH' for s in o.material_slots if s.material})), flush=True)

for pid, spec in PEOPLE.items():
    if ONLY and pid not in ONLY: continue
    try: make(pid, spec)
    except Exception as e:
        import traceback; traceback.print_exc(); print('FAIL', pid, repr(e), flush=True)
