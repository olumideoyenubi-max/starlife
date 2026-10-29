# Brings Poly Haven (CC0) furniture into the game for the shops and restaurants in Starlife Town: downloads each model's
# 1k glTF, imports it, turns seats so their fronts face -Y (the side away from the backrest), and hands it to
# prop_export.process, which sizes it and writes Assets/Furniture/<kind>/prop.fbx (SLBuild.Import3D makes the prefab).
#   Blender -b -P Tools/blender/fetch_furniture.py -- <project> <download dir> [kind ...]
import bpy, os, sys, json, math, urllib.request
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index('--') + 1:]
PROJECT, CACHE = args[0], args[1]; ONLY = set(args[2:])
sys.path.insert(0, os.path.join(PROJECT, 'Tools', 'blender'))
import prop_export

# kind: (Poly Haven id, width in metres, greatest height, is it a seat with a back to face away from)
KINDS = {
    'chair_dining':  ('dining_chair_02', 0.5, 1.0, True),
    'chair_cafe':    ('painted_wooden_chair_01', 0.46, 0.95, True),
    'stool':         ('metal_stool_02', 0.42, 0.78, False),
    'barber':        ('BarberShopChair_01', 0.75, 1.2, True),
    'lounge':        ('mid_century_lounge_chair', 0.8, 1.0, True),
    'armchair2':     ('modern_arm_chair_01', 0.72, 0.9, True),
    'ottoman':       ('Ottoman_01', 0.8, 0.45, False),
    'table_cafe':    ('round_wooden_table_01', 1.0, 0.74, False),
    'table_dining':  ('wooden_table_02', 1.1, 0.76, False),
    'chandelier':    ('Chandelier_01', 0.95, 0.85, False),
    'crystal':       ('Chandelier_03', 0.9, 0.9, False),
    'pendant':       ('hanging_industrial_lamp', 0.5, 0.9, False),
    'ceiling_lamp':  ('modern_ceiling_lamp_01', 0.55, 0.8, False),
    'shelves':       ('wooden_display_shelves_01', 1.4, 1.45, False),
    'register':      ('CashRegister_01', 0.4, 0.4, False),
    'coffeecart':    ('CoffeeCart_01', 0.8, 1.05, False),
    'cake':          ('strawberry_chocolate_cake', 0.24, 0.14, False),
    'mirror':        ('ornate_mirror_01', 0.9, 1.25, False),
    'vase':          ('ceramic_vase_01', 0.3, 0.4, False),
    'teaset':        ('tea_set_01', 0.5, 0.25, False),
}

def get(url):     # Poly Haven turns away Python's default user agent
    return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'StarlifeTown-furniture/1.0'})).read()

def fetch(pid):
    d = os.path.join(CACHE, pid); os.makedirs(d, exist_ok=True)
    files = json.loads(get('https://api.polyhaven.com/files/' + pid))
    g = files['gltf']['1k']['gltf']; main = os.path.join(d, os.path.basename(g['url']))
    if not os.path.exists(main): open(main, 'wb').write(get(g['url']))
    for rel, inc in g.get('include', {}).items():
        p = os.path.join(d, rel); os.makedirs(os.path.dirname(p), exist_ok=True)
        if not os.path.exists(p): open(p, 'wb').write(get(inc['url']))
    return main

def face_front(width_axis_hint=True):
    """A seat's back is its tallest part: turn the model so the back is at +Y and the front faces -Y."""
    vs = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == 'MESH' for v in o.data.vertices]
    top = max(v.z for v in vs); lo = min(v.z for v in vs); cx = sum(v.x for v in vs) / len(vs); cy = sum(v.y for v in vs) / len(vs)
    high = [v for v in vs if v.z > lo + (top - lo) * 0.75]
    bx = sum(v.x for v in high) / len(high) - cx; by = sum(v.y for v in high) / len(high) - cy
    ang = math.atan2(bx, by)            # where the back is, measured from +Y
    rot = Matrix.Rotation(ang, 4, 'Z')
    for o in [o for o in bpy.data.objects if o.parent is None]: o.matrix_world = rot @ o.matrix_world
    return math.degrees(ang)

for kind, (pid, width, height, seat) in KINDS.items():
    if ONLY and kind not in ONLY: continue
    try:
        path = fetch(pid)
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=path)
        turned = face_front() if seat else 0
        prop_export.process(kind, width, os.path.join(PROJECT, 'Assets', 'Furniture'), os.path.join(PROJECT, 'Art3D', 'furniture'), max_height=height)
        print('FURNITURE', kind, pid, 'turned %.0f' % turned, flush=True)
    except Exception as e:
        import traceback; traceback.print_exc(); print('FAIL', kind, pid, repr(e), flush=True)
