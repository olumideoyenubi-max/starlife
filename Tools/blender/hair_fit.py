# Fits a downloaded hairstyle to the starlets' shared head, in the live Blender session:
#   import hair_fit; hair_fit.collect()                 # bake the import into one object called HAIR (drops mannequin heads)
#   hair_fit.place(centre=(0, -0.03, 1.66), size=0.30)  # move and scale it until it sits right on REF_head
#   hair_fit.export('afro', '<project>/Assets/Hair', '<project>/Art3D/hair')
# The reference head (objects named REF_*) is any starlet's exported head: all of them share one head position.
import bpy, os, json, math
from mathutils import Matrix, Vector

def collect(drop=(), keep_only=None, min_faces=1):
    """Join every imported mesh (except REF_* and names containing a word in drop) into HAIR, with the pose baked in."""
    dg = bpy.context.evaluated_depsgraph_get(); made = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('REF_') and o.name != 'HAIR']:
        mats = ' '.join(m.name for m in o.data.materials if m) + ' ' + o.name
        if not [m for m in o.data.materials if m]: continue      # helper shapes that come with an import
        if any(d.lower() in mats.lower() for d in drop) or len(o.data.polygons) < min_faces: continue
        if keep_only and not any(k.lower() in mats.lower() for k in keep_only): continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg); me.transform(o.matrix_world)
        b = bpy.data.objects.new('bake', me); bpy.context.scene.collection.objects.link(b); made.append(b)
    for o in list(bpy.data.objects):
        if o not in made and not o.name.startswith('REF_'): bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for b in made: b.select_set(True)
    bpy.context.view_layer.objects.active = made[0]
    if len(made) > 1: bpy.ops.object.join()
    h = bpy.context.view_layer.objects.active; h.name = 'HAIR'; h.data.name = 'HAIR_src'
    h['src'] = h.data.copy().name    # the untouched mesh, so place() can be called again and again
    return info()

def clear():
    """Remove everything except the reference head, before the next import."""
    for o in list(bpy.data.objects):
        if not o.name.startswith('REF_'): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0: bpy.data.meshes.remove(m)

def info():
    h = bpy.data.objects['HAIR']; ws = [h.matrix_world @ v.co for v in h.data.vertices]
    lo = Vector((min(p.x for p in ws), min(p.y for p in ws), min(p.z for p in ws))); hi = Vector((max(p.x for p in ws), max(p.y for p in ws), max(p.z for p in ws)))
    print('HAIR verts %d faces %d  x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f  size %.3f %.3f %.3f  mats %s' % (len(h.data.vertices), len(h.data.polygons), lo.x, hi.x, lo.y, hi.y, lo.z, hi.z, hi.x - lo.x, hi.y - lo.y, hi.z - lo.z, [m.name for m in h.data.materials if m]))
    return lo, hi

def place(centre, size=None, scale=None, stretch=(1, 1, 1), turn=0, tilt=0):
    """Put the bounding box centre at centre. size = the box's width (x) in metres, or scale = a plain factor. turn/tilt in degrees."""
    h = bpy.data.objects['HAIR']; src = bpy.data.meshes[h['src']]
    me = src.copy(); old = h.data; h.data = me; h.matrix_world = Matrix.Identity(4)
    if old.name != src.name: bpy.data.meshes.remove(old)
    me.transform(Matrix.Rotation(math.radians(turn), 4, 'Z') @ Matrix.Rotation(math.radians(tilt), 4, 'X'))
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    k = scale if scale is not None else size / (max(xs) - min(xs))
    me.transform(Matrix.Translation(Vector(centre)) @ Matrix.Diagonal((k * stretch[0], k * stretch[1], k * stretch[2], 1)) @ Matrix.Translation(-c))
    return info()

def cap(color=(0.035, 0.028, 0.025, 1), lift=0.0025, low=1.50, slope=1.5):
    """A close-cropped layer of hair over the scalp (object CAP), for styles that leave the scalp showing between partings."""
    import bmesh
    if 'CAP' in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects['CAP'], do_unlink=True)
    ref = bpy.data.objects['REF_head']; me = ref.data.copy(); me.transform(ref.matrix_world)
    c = bpy.data.objects.new('CAP', me); bpy.context.scene.collection.objects.link(c)
    bm = bmesh.new(); bm.from_mesh(me)
    def scalp(v): return v.co.z > low + max(0.0, -v.co.y - 0.02) * slope and (v.co.z > 1.61 or abs(v.co.x) < 0.074)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(scalp(v) for v in f.verts)], context='FACES')
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0002); bm.normal_update()    # close the texture seam so it does not open when lifted
    for v in bm.verts: v.co += v.normal * lift
    bm.to_mesh(me); bm.free()
    mat = bpy.data.materials.new('Cap'); mat.use_nodes = True; mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = color
    me.materials.clear(); me.materials.append(mat)
    print('CAP faces', len(me.polygons)); return c

def _image(mat, socket):
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
    if not bsdf or not bsdf.inputs[socket].is_linked: return None
    todo = [bsdf.inputs[socket].links[0].from_node]; seen = set()
    while todo:
        n = todo.pop(0)
        if n in seen: continue
        seen.add(n)
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo += [i.links[0].from_node for i in n.inputs if i.is_linked]
    return None

def export(name, assets_dir, art_dir, label=None, color=None, max_faces=40000):
    """Write <assets>/<name>/hair.fbx, its textures and manifest.json (one part per material), and keep the scene in art_dir."""
    out = os.path.join(assets_dir, name); os.makedirs(out, exist_ok=True); os.makedirs(art_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art_dir, name + '.blend'), copy=True)
    h = bpy.data.objects['HAIR']; c = h.copy(); c.data = h.data.copy(); bpy.context.scene.collection.objects.link(c)
    bpy.ops.object.select_all(action='DESELECT'); c.select_set(True); bpy.context.view_layer.objects.active = c
    if len(c.data.polygons) > max_faces:      # heavy sculpted hair: thin it out for the game
        m = c.modifiers.new('thin', 'DECIMATE'); m.ratio = max_faces / len(c.data.polygons); bpy.ops.object.modifier_apply(modifier='thin')
    if 'CAP' in bpy.data.objects:             # the scalp layer travels with the hair as a second material
        k = bpy.data.objects['CAP'].copy(); k.data = bpy.data.objects['CAP'].data.copy(); bpy.context.scene.collection.objects.link(k)
        k.select_set(True); bpy.ops.object.join()
    for p in c.data.polygons: p.use_smooth = True
    if len([m for m in c.data.materials if m]) > 1:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    pieces = [o for o in bpy.context.selected_objects if o.type == 'MESH'] or [c]
    parts = []; saved = {}
    def save(img, fname):
        if img is None: return ''
        if img.name in saved: return saved[img.name]
        path = os.path.join(out, fname + '.png'); img.file_format = 'PNG'
        try: img.save(filepath=path)
        except Exception: img.filepath_raw = path; img.save()
        saved[img.name] = os.path.basename(path); return saved[img.name]
    for i, o in enumerate(sorted(pieces, key=lambda p: -len(p.data.polygons))):
        o.name = o.data.name = 'hair%d' % i; mat = o.data.materials[0] if o.data.materials else None
        bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
        base = _image(mat, 'Base Color') if mat else None
        parts.append({'mesh': o.name, 'material': mat.name if mat else '', 'tex': save(base, 'hair%d' % i), 'normal': '', 'alpha': bool(bsdf and bsdf.inputs['Alpha'].is_linked),
                      'color': [round(v, 4) for v in (color or (bsdf.inputs['Base Color'].default_value if bsdf else (0.1, 0.08, 0.07, 1)))], 'rough': 0.6, 'metal': 0.0,
                      'tris': sum(len(p.vertices) - 2 for p in o.data.polygons)})
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'hair.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    json.dump({'id': name, 'label': label or name, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    for o in pieces: bpy.data.objects.remove(o, do_unlink=True)
    print('HAIR EXPORT', name, [(p['mesh'], p['material'], p['tex'], p['alpha'], p['tris']) for p in parts])
