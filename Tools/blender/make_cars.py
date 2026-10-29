# Starlife Town's cars, from "Generic passenger car pack" by Comrade1280 (Sketchfab, CC-BY; the copy downloaded for
# Kigali Frenzy). Each car is put at the origin facing forward (Unity +z), wheels on the ground, and saved as
# Assets/Resources/Cars/<id>.fbx: one "body" mesh with three materials (Paint, Glass, Lights) and four wheels
# (wheel_fl, wheel_fr, wheel_rl, wheel_rr) whose origins are their hubs, so the game can turn them. The paint texture,
# glass and lights go to Assets/Resources/Cars/<id>_paint.png etc.; cars.json has the sizes. A limousine is the sedan
# stretched in the middle.
#   Blender -b -P Tools/blender/make_cars.py -- <project> [<pack dir>]
import bpy, bmesh, os, sys, json, math
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index('--') + 1:]
PROJECT = args[0]; PACK = args[1] if len(args) > 1 else '/Volumes/NHBuild/KigaliFrenzy/Art3D/src/sketchfab/carpack'
OUT = os.path.join(PROJECT, 'Assets', 'Resources', 'Cars'); os.makedirs(OUT, exist_ok=True)
NAMES = {'Compact': 'compact', 'Coupe': 'coupe', 'Hatchback': 'hatch', 'minivan': 'van', 'Offroad': 'offroad', 'Pickup': 'pickup', 'Sedan': 'sedan', 'Sport': 'sport', 'SUV': 'suv', 'Wagon': 'wagon'}

def fresh():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(PACK, 'scene.gltf'))
    bpy.context.view_layer.update()

def world_verts(o): return [o.matrix_world @ v.co for v in o.data.vertices]

def image_of(mat):
    """The image that feeds the material's base colour (not its roughness or metal map)."""
    if not mat or not mat.use_nodes: return None
    b = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is not None and b.inputs['Base Color'].is_linked:
        todo = [b.inputs['Base Color'].links[0].from_node]
        while todo:
            n = todo.pop(0)
            if n.type == 'TEX_IMAGE' and n.image: return n.image
            todo += [i.links[0].from_node for i in n.inputs if i.is_linked]
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and 'metallic' not in n.image.name.lower() and 'rough' not in n.image.name.lower(): return n.image
    return None

def redness(obj, at_end):
    """How red the lights are at one end of the car (tail lights are red, headlights pale)."""
    img = image_of(obj.data.materials[0]) if obj.data.materials else None
    uv = obj.data.uv_layers.active
    if img is None or uv is None: return 0
    px = img.pixels[:]; w, h = img.size; tot = n = 0
    for poly in obj.data.polygons:
        if not at_end(obj.matrix_world @ poly.center): continue
        for li in poly.loop_indices:
            u, v = uv.data[li].uv; x = min(w - 1, max(0, int((u % 1) * w))); y = min(h - 1, max(0, int((v % 1) * h))); i = (y * w + x) * 4
            r, g, b = px[i], px[i + 1], px[i + 2]; tot += r - (g + b) / 2; n += 1
    return tot / max(n, 1)

def build(prefix, cid, stretch=0.0):
    fresh()
    def find(k):
        for o in bpy.data.objects:
            if o.type == 'MESH' and o.name.lower().startswith(prefix.lower() + ' ') and k.lower() in o.name.lower(): return o
        return None
    parts = {k: find(k) for k in ('Glass', 'Optics')}
    parts['Body'] = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.lower().startswith(prefix.lower() + ' ') and 'glass' not in o.name.lower() and 'optic' not in o.name.lower())
    body = parts['Body']; vs = world_verts(body)
    c = sum(vs, Vector()) / len(vs)
    # the car's long axis (principal direction in the ground plane)
    sxx = sum((v.x - c.x) ** 2 for v in vs); syy = sum((v.y - c.y) ** 2 for v in vs); sxy = sum((v.x - c.x) * (v.y - c.y) for v in vs)
    ang = 0.5 * math.atan2(2 * sxy, sxx - syy); axis = Vector((math.cos(ang), math.sin(ang), 0))
    proj = [(v - c).dot(axis) for v in vs]; half = (max(proj) - min(proj)) / 2
    mid = (max(proj) + min(proj)) / 2; c = c + axis * mid
    opt = parts['Optics']
    if opt is not None:
        ra = redness(opt, lambda p: (p - c).dot(axis) > half * 0.5); rb = redness(opt, lambda p: (p - c).dot(axis) < -half * 0.5)
        if ra > rb: axis = -axis            # the red end is the back
    centroid = lambda o: sum(world_verts(o), Vector()) / len(o.data.vertices)
    # the four wheels nearest the car's centre, one in each corner
    cands = sorted([o for o in bpy.data.objects if o.type == 'MESH' and 'whee' in o.name.lower()], key=lambda o: (centroid(o) - c).length)
    side = axis.cross(Vector((0, 0, 1))); wheels = []; corners = set()
    for o in cands:
        d = centroid(o) - c; key = ((d.dot(axis) > 0), (d.dot(side) > 0))
        if key in corners or d.length > half * 1.3: continue
        corners.add(key); wheels.append(o)
        if len(wheels) == 4: break
    ground = min(min(v.z for v in world_verts(w)) for w in wheels)
    # to the origin, front along Blender -Y (Unity +Z once exported), ground at 0
    yaw = math.atan2(axis.y, axis.x) - math.radians(-90)
    T = Matrix.Rotation(-yaw, 4, 'Z') @ Matrix.Translation(Vector((-c.x, -c.y, -ground)))
    keep = [o for o in parts.values() if o is not None] + wheels
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    for o in keep:
        me = o.data.copy(); me.transform(o.matrix_world); me.transform(T); o.data = me; o.parent = None; o.matrix_world = Matrix.Identity(4)
    if stretch:
        for o in keep:
            for v in o.data.vertices:
                y = v.co.y; v.co.y = y * (1 + stretch / 0.35) if abs(y) < 0.35 else y + math.copysign(stretch, y)
            if o in wheels: pass
        for w in wheels:
            yc = sum(v.co.y for v in w.data.vertices) / len(w.data.vertices)
            # wheels moved with the stretch above; nothing else to do
    # wheels: origin at the hub, named by corner
    named = {}
    for w in wheels:
        vs2 = [v.co for v in w.data.vertices]; hub = sum(vs2, Vector()) / len(vs2)
        w.data.transform(Matrix.Translation(-hub)); w.location = hub
        named[('wheel_' + ('f' if hub.y < 0 else 'r') + ('l' if hub.x > 0 else 'r'))] = w
    for k, w in named.items(): w.name = w.data.name = k
    # one body with three materials, renamed for the game
    for k, o in parts.items():
        if o is None: continue
        if o.data.materials: o.data.materials[0].name = {'Body': 'Paint', 'Glass': 'Glass', 'Optics': 'Lights'}[k] + '_' + cid
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts.values():
        if o is not None: o.select_set(True)
    bpy.context.view_layer.objects.active = body; bpy.ops.object.join(); body.name = body.data.name = 'body'
    for w in named.values():
        for m in w.data.materials: m.name = 'Wheel_' + cid
    # textures
    tex = {}
    for o in [body] + list(named.values()):
        for m in o.data.materials:
            img = image_of(m)
            if img is None: continue
            kind = m.name.split('_')[0].lower(); path = os.path.join(OUT, f'{cid}_{kind}.png')
            if kind in tex: continue
            img2 = img.copy(); img2.filepath_raw = path; img2.file_format = 'PNG'
            try: img2.save()
            except Exception as e: print('TEX', cid, kind, e); continue
            tex[kind] = os.path.basename(path)
    # export
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, cid + '.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    xs = [v.co.x for v in body.data.vertices]; ys = [v.co.y for v in body.data.vertices]; zs = [v.co.z for v in body.data.vertices]
    wr = max((max(v.co.z for v in w.data.vertices) - min(v.co.z for v in w.data.vertices)) / 2 for w in named.values())
    info = dict(id=cid, length=round(max(ys) - min(ys), 2), width=round(max(xs) - min(xs), 2), height=round(max(zs), 2), wheel=round(wr, 3),
                wheelbase=round(abs(named['wheel_fl'].location.y - named['wheel_rl'].location.y), 2) if 'wheel_fl' in named and 'wheel_rl' in named else 0,
                materials=[m.name for m in body.data.materials], tex=tex, tris=sum(len(p.vertices) - 2 for p in body.data.polygons))
    print('CAR', json.dumps(info), flush=True)
    return info

infos = []
for prefix, cid in NAMES.items():
    try: infos.append(build(prefix, cid))
    except Exception as e:
        import traceback; traceback.print_exc(); print('FAIL', cid, e)
try: infos.append(build('Sedan', 'limo', stretch=0.95))
except Exception as e:
    import traceback; traceback.print_exc(); print('FAIL limo', e)
json.dump(infos, open(os.path.join(OUT, 'cars.json'), 'w'), indent=1)
