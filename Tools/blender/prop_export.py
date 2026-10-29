# Turns the furniture model currently loaded in Blender (for example a Poly Haven import) into a room piece for Unity:
#   import prop_export; prop_export.process('sofa', 1.24, '<project>/Assets/Furniture', '<project>/Art3D/furniture')
# The piece is centred on its footprint, stood on the ground and scaled to the given width in metres (the game's own
# width for that kind of furniture), split by material, and written as <kind>/prop.fbx with textures and manifest.json.
import bpy, os, json
from mathutils import Matrix, Vector

def _image(mat, socket):
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
    if not bsdf or socket not in bsdf.inputs or not bsdf.inputs[socket].is_linked: return None
    todo = [bsdf.inputs[socket].links[0].from_node]; seen = set()
    while todo:
        n = todo.pop(0)
        if n in seen: continue
        seen.add(n)
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo += [i.links[0].from_node for i in n.inputs if i.is_linked]
    return None

def process(kind, width, assets_dir, art_dir, turn=0, max_faces=30000, max_height=1.1):
    out = os.path.join(assets_dir, kind); os.makedirs(out, exist_ok=True); os.makedirs(art_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art_dir, kind + '.blend'), copy=True)
    dg = bpy.context.evaluated_depsgraph_get(); made = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and [m for m in o.data.materials if m] and len(o.data.polygons) > 0]:
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg); me.transform(o.matrix_world)
        b = bpy.data.objects.new('bake', me); bpy.context.scene.collection.objects.link(b); made.append(b)
    for o in list(bpy.data.objects):
        if o not in made: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for b in made: b.select_set(True)
    bpy.context.view_layer.objects.active = made[0]
    if len(made) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    import math
    o.data.transform(Matrix.Rotation(math.radians(turn), 4, 'Z'))
    xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]; zs = [v.co.z for v in o.data.vertices]
    k = min(width / (max(xs) - min(xs)), max_height / (max(zs) - min(zs)))
    o.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))))
    if len(o.data.polygons) > max_faces:
        m = o.modifiers.new('thin', 'DECIMATE'); m.ratio = max_faces / len(o.data.polygons); bpy.ops.object.modifier_apply(modifier='thin')
    for p in o.data.polygons: p.use_smooth = True
    if len([m for m in o.data.materials if m]) > 1:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    pieces = [p for p in bpy.context.selected_objects if p.type == 'MESH'] or [o]
    parts = []; saved = {}
    def save(img, name):
        if img is None: return ''
        if img.name in saved: return saved[img.name]
        path = os.path.join(out, name + '.png'); img.file_format = 'PNG'
        try: img.save(filepath=path)
        except Exception: img.filepath_raw = path; img.save()
        saved[img.name] = os.path.basename(path); return saved[img.name]
    for i, p in enumerate(sorted(pieces, key=lambda q: -len(q.data.polygons))):
        p.name = p.data.name = 'part%d' % i; mat = p.data.materials[0] if p.data.materials else None
        bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
        parts.append({'mesh': p.name, 'material': mat.name if mat else '', 'tex': save(_image(mat, 'Base Color'), 'tex%d' % i), 'normal': save(_image(mat, 'Normal'), 'nrm%d' % i),
                      'alpha': bool(bsdf and bsdf.inputs['Alpha'].is_linked), 'color': [round(c, 4) for c in (bsdf.inputs['Base Color'].default_value if bsdf else (1, 1, 1, 1))],
                      'rough': 0.6, 'metal': 0.0, 'tris': sum(len(f.vertices) - 2 for f in p.data.polygons)})
    bpy.ops.object.select_all(action='DESELECT')
    for p in pieces: p.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'prop.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    json.dump({'id': kind, 'label': kind, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    dims = [(max(v.co[a] for p in pieces for v in p.data.vertices) - min(v.co[a] for p in pieces for v in p.data.vertices)) for a in range(3)]
    print('PROP', kind, 'size %.2f %.2f %.2f' % tuple(dims), [(q['mesh'], q['material'], q['tex'], q['alpha'], q['tris']) for q in parts])
