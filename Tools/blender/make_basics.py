# Makes the underwear and the hip filler worn under wardrobe pieces, cut from close-fitting pieces the avatars came with:
#   blender -b -P Tools/blender/make_basics.py -- <project>/Assets/Starlets
# Writes Assets/Starlets/basics (a pseudo-avatar with only these parts, all plain so the game dyes them):
#   hips    the leggings from the upper thigh to the waist, drawn as skin to fill what the shared body lacks there
#   briefs  the same surface cut high on the leg         shorts  cut straight across the thigh
#   bra     a bandeau cut from a fitted crop top         tank    the crop top without its sleeves, as a sports bra / tank
import bpy, bmesh, sys, os, json
assets = sys.argv[sys.argv.index('--') + 1]
out = os.path.join(assets, 'basics'); os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def load(avatar, part):
    thin = os.path.join(assets, avatar, 'pieces_thin.fbx')     # garments before they were given thickness
    bpy.ops.import_scene.fbx(filepath=thin if os.path.exists(thin) else os.path.join(assets, avatar, 'starlet.fbx'))
    got = None
    for o in list(bpy.context.selected_objects):
        if o.name.split('.')[0] == part and got is None: got = o
        else: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT'); got.select_set(True); bpy.context.view_layer.objects.active = got
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); got.data.materials.clear(); return got

def cut(src, name, planes):
    """A copy of src trimmed cleanly by planes: each (point, normal) keeps the side its normal points to."""
    c = src.copy(); c.data = src.data.copy(); bpy.context.scene.collection.objects.link(c); c.name = c.data.name = name
    bm = bmesh.new(); bm.from_mesh(c.data)
    for co, no in planes:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no, clear_inner=True)
    bm.to_mesh(c.data); bm.free()
    for p in c.data.polygons: p.use_smooth = True
    return c

legs = load('ivy', 'bottom'); top = load('lina', 'top')
zs = [v.co.z for v in legs.data.vertices]; print('LEGS z', min(zs), max(zs), 'TOP z', min(v.co.z for v in top.data.vertices), max(v.co.z for v in top.data.vertices))
up, down, left, right = (0, 0, 1), (0, 0, -1), (1, 0, 0), (-1, 0, 0)
parts = [cut(legs, 'hips', [((0, 0, 0.58), up)]),                                                       # well down the thigh, so its edge hides inside the leg
         cut(legs, 'briefs', [((0, 0, 0.80), (-0.9, 0, 1)), ((0, 0, 0.80), (0.9, 0, 1)), ((0, 0, 0.985), down)]),              # cut high on each leg
         cut(legs, 'shorts', [((0, 0, 0.745), up), ((0, 0, 0.995), down)]),
         cut(top, 'bra', [((0, 0, 1.215), up), ((0, 0, 1.375), down), ((-0.158, 0, 0), left), ((0.158, 0, 0), right)]),
         cut(top, 'tank', [((0, 0, 1.46), down), ((-0.166, 0, 1.40), (1, 0, 0.2)), ((0.166, 0, 1.40), (-1, 0, 0.2))])]
# torso skin: no avatar has skin under her own clothes, but each bares a different part, so together they cover the chest,
# back and waist. Their torsos (arms left out: the shared body has those) are joined into one piece.
torsos = []
for who in ('jenny', 'rosie', 'eve', 'cute', 'kelly', 'lina', 'olivia'):
    b = load(who, 'body'); torsos.append(cut(b, 'torso_' + who, [((0, 0, 0.93), up), ((0, 0, 1.47), down), ((-0.172, 0, 0), left), ((0.172, 0, 0), right)])); bpy.data.objects.remove(b, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for o in torsos: o.select_set(True)
bpy.context.view_layer.objects.active = torsos[0]; bpy.ops.object.join(); torso = bpy.context.view_layer.objects.active; torso.name = torso.data.name = 'torso'
parts.append(torso)
for o in (legs, top): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                         apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
json.dump({'id': 'basics', 'label': 'basics', 'parts': [{'mesh': p.name, 'material': 'Plain', 'tex': '', 'normal': '', 'alpha': False, 'color': [1, 1, 1, 1], 'rough': 0.7, 'metal': 0.0,
           'tone': [1, 1, 1], 'lum': 1.0} for p in parts]}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
print('BASICS', [(p.name, len(p.data.polygons)) for p in parts])
