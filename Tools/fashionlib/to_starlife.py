# Puts the fashion library into Starlife as two pseudo-avatars (like goth and acc: pieces only, no body):
#   lib     pieces that ride one part of her (SLRig.Pin): hats, glasses and earrings on the head (hat_/glasses_/ear_),
#           hand-held bags rigid in the hand (hand_), and necklaces, scarves, belts and crossbody bags on the torso
#   libfit  pieces skinned like clothes, by the nearest point of the body: shoes and what is worn on a wrist
# Each item's LOD0 (<item>_starlife.fbx: its non-metal, metal, see-through and trim parts) is renamed, gathered into
# starlet.fbx with its baked maps, and described in manifest.json for SLBuild.Import3D. Also writes items.json: the rows
# for Tools/core_api.js and the entries for SLWardrobe3D.Map.
#   blender -b -P Tools/fashionlib/to_starlife.py -- <library> <StarLife>/Assets/Starlets
import bpy, os, sys, json, shutil

LIB, STAR = sys.argv[sys.argv.index('--') + 1:][:2]
cat = json.load(open(os.path.join(LIB, 'catalog.json')))

# library slot -> (Starlife slot, which pseudo-avatar, mesh-name prefix for SLRig.Pin, 2D drawing to borrow for the thumbnail)
SLOTS = {
    'crossbody': ('bag', 'lib', '', 'crossbody'), 'back': ('bag', 'lib', '', 'crossbody'), 'hand_r': ('bag', 'lib', 'hand_', 'tote'),
    'neck': ('necklace', 'lib', '', 'pendant'), 'ears': ('earrings', 'lib', 'ear_', 'hoops'), 'eyes': ('glasses', 'lib', 'glasses_', 'sunnies'),
    'head': ('hat', 'lib', 'hat_', 'sunhat'), 'waist': ('belt', 'lib', '', 'choker'),
    'feet': ('shoes', 'libfit', '', 'flats'), 'wrist_l': ('watch', 'libfit', '', 'choker'), 'wrist_r': ('bracelet', 'libfit', '', 'pearls'),
}
BASE = {'lux_clutch': 'clutch', 'mid_tote': 'tote', 'lux_tote': 'tote', 'lux_hoops': 'hoops', 'mid_studs': 'studs', 'lux_scarf': 'choker', 'mid_chain': 'pearls',
        'lux_fedora': 'sunhat', 'mid_cap': 'cap', 'mid_bucket': 'sunhat', 'lux_aviator': 'roundspecs', 'mid_sunglasses': 'sunnies',
        'lux_loafer': 'loafers', 'lux_ballet': 'flats', 'mid_sneaker': 'sneakers', 'mid_slide': 'sandals'}
PRICE = {'luxury': 180, 'mid': 45}

def hexcol(c): return '#%02x%02x%02x' % tuple(int(max(0, min(1, v)) ** (1 / 2.2) * 255) for v in c[:3])

groups = {'lib': [], 'libfit': []}; rows = []; maps = []
for id, man in sorted(cat.items()):
    sl_slot, group, prefix, base = SLOTS[man['slot']]; base = BASE.get(id, base)
    groups[group].append((id, man, prefix))
    rows.append([id, man['name'], sl_slot, 'luxe' if man['tier'] == 'luxury' else 'everyday', base, '#888888', PRICE[man['tier']], ['chic', 'glam'] if man['tier'] == 'luxury' else ['casual']])
    maps.append((id, group, [prefix + p['mesh'] for p in man['starlife_parts']]))

for group, items in groups.items():
    out = os.path.join(STAR, group); os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if not f.endswith('.meta'): os.remove(os.path.join(out, f))
    bpy.ops.wm.read_factory_settings(use_empty=True); parts = []
    for id, man, prefix in items:
        d = os.path.join(LIB, man['tier'], id)
        bpy.ops.import_scene.fbx(filepath=os.path.join(d, id + '_starlife.fbx'))
        by = {p['mesh']: p for p in man['starlife_parts']}
        for o in list(bpy.context.selected_objects):
            if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True); continue
            key = o.name.split('.')[0]; p = by[key]; o.name = o.data.name = prefix + key
            for k in ('tex', 'normal'):
                if p[k]: shutil.copy(os.path.join(d, p[k]), os.path.join(out, p[k]))
            # the item's own colour, never dyed: materials named Bead... keep theirs (SLStage3D); the wardrobe map adds "!" as well
            parts.append({'mesh': o.name, 'material': 'BeadLib_' + id, 'tex': p['tex'], 'normal': p['normal'], 'alpha': p['alpha'], 'color': p['color'],
                          'rough': p['rough'], 'metal': p['metal'], 'tone': [1, 1, 1], 'lum': 1.0})
        # the colour shown in the shop and wardrobe swatches: the albedo atlas' average, else the first flat part's colour
        for r in rows:
            if r[0] == id:
                tex = next((p['tex'] for p in man['starlife_parts'] if p['tex']), None)
                if tex:
                    im = bpy.data.images.load(os.path.join(d, tex)); px = im.pixels[:]; n = len(px) // 4; step = max(1, n // 20000)
                    lit = [i for i in range(0, n, step) if px[i * 4] + px[i * 4 + 1] + px[i * 4 + 2] > 0.02]
                    r[5] = hexcol([sum(px[i * 4 + c] for i in lit) / max(1, len(lit)) for c in range(3)]); bpy.data.images.remove(im)
                else: r[5] = hexcol(man['starlife_parts'][0]['color'])
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    json.dump({'id': group, 'label': group, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    print('GROUP', group, len(items), 'items', len(parts), 'parts')

json.dump({'rows': rows, 'map': [[id, group + ':' + '+'.join(ms) + '!'] for id, group, ms in maps]}, open(os.path.join(LIB, 'starlife_items.json'), 'w'), indent=1)
print('ROWS', len(rows))
