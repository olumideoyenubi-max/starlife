# Turns the avatar currently loaded in Blender (a rigged Sketchfab import) into a starlet for Unity.
#   import avatar_export; avatar_export.process('aria', '<project>/Assets/Starlets', '<project>/Art3D/avatars')
# Relaxes the T-pose, bakes the pose into the meshes (the game only turns her), stands her on the ground at
# 1.70 m, splits her by material, and writes <id>/starlet.fbx, her textures and manifest.json. The scene is
# also saved to Art3D/avatars/<id>.blend so the source stays with the project.
import bpy, os, json, math, re
from mathutils import Matrix, Vector

HEIGHT = 1.70

def _swing(arm, name, deg):
    pb = arm.pose.bones[name]; mw = arm.matrix_world @ pb.matrix; head = mw.translation.copy()
    sign = 1 if (arm.matrix_world @ pb.tail).x > head.x else -1
    r = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg * sign), 4, 'Y') @ Matrix.Translation(-head)
    pb.matrix = arm.matrix_world.inverted() @ r @ mw; bpy.context.view_layer.update()

def _image(mat, socket):
    """The image feeding a Principled input, following normal-map and mix nodes back to a texture."""
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
    if not bsdf or socket not in bsdf.inputs or not bsdf.inputs[socket].is_linked: return None
    todo = [bsdf.inputs[socket].links[0].from_node]; seen = set()
    while todo:
        n = todo.pop(0)
        if n in seen: continue
        seen.add(n)
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        for i in n.inputs:
            if i.is_linked: todo.append(i.links[0].from_node)
    return None

def fresh():
    """Empty the scene before the next import."""
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)

def process(sid, assets_dir, art_dir, elbow=10, bake=True):
    out = os.path.join(assets_dir, sid); os.makedirs(art_dir, exist_ok=True)
    if bake: os.makedirs(out, exist_ok=True)
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    arm = max(arms, key=lambda a: len(a.data.bones)) if arms else None
    skinned = [o for o in bpy.data.objects if o.type == 'MESH' and o.data.materials and len(o.data.polygons) > 0
               and (arm is None or any(m.type == 'ARMATURE' for m in o.modifiers) or o.parent_type == 'BONE')]
    if not skinned: raise RuntimeError('no avatar meshes found')
    if arm:
        for o in arms: o.data.pose_position = 'POSE'
        # keep the imported pose (Sketchfab rigs carry their upright orientation in it); just stop any animation on its first frame
        bpy.context.scene.frame_set(int(arm.animation_data.action.frame_range[0]) if arm.animation_data and arm.animation_data.action else bpy.context.scene.frame_current)
        if arm.animation_data and arm.animation_data.action:
            for pb in arm.pose.bones: basis = pb.matrix_basis.copy(); pb.matrix_basis = basis
            arm.animation_data.action = None
        bpy.context.view_layer.update()
        def find(key): return next((b.name for b in arm.pose.bones if re.sub(r'^mixamorig:?', '', b.name).startswith(key)), None)
        for side in ('Left', 'Right'):
            up, fore = find(side + 'Arm'), find(side + 'ForeArm')
            if up:
                # bring the upper arm to about 16 degrees from hanging straight down, whatever pose she came in
                pb = arm.pose.bones[up]; d = (arm.matrix_world @ pb.tail) - (arm.matrix_world @ pb.head)
                ang = math.degrees(math.acos(max(-1, min(1, -d.z / d.length))))
                if ang > 22:
                    _swing(arm, up, ang - 16)
                    if fore and ang > 60: _swing(arm, fore, elbow)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art_dir, sid + '.blend'), copy=True)
    if not bake:      # only keep the posed, rigged source (wardrobe_export.py works from that)
        print('STASHED', sid, [(o.name, o.data.materials[0].name if o.data.materials[0] else None) for o in skinned]); return []

    dg = bpy.context.evaluated_depsgraph_get(); baked = []
    for o in skinned:
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        me.transform(o.matrix_world); b = bpy.data.objects.new('bake', me); bpy.context.scene.collection.objects.link(b); baked.append(b)
    pts = [v.co for b in baked for v in b.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    k = HEIGHT / (hi.z - lo.z)
    # centre on the body, not on the arm span or a ponytail: use the points in the middle third of her height
    mid = [p for p in pts if lo.z + (hi.z - lo.z) * 0.35 < p.z < lo.z + (hi.z - lo.z) * 0.6 and abs(p.x - (lo.x + hi.x) / 2) < (hi.z - lo.z) * 0.12]
    cx = sum(p.x for p in mid) / len(mid) if mid else (lo.x + hi.x) / 2; cy = sum(p.y for p in mid) / len(mid) if mid else (lo.y + hi.y) / 2
    fix = Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-cx, -cy, -lo.z)))
    for b in baked: b.data.transform(fix)

    # one object per material, named part<N>, so Unity gets exactly one known material per renderer
    bpy.ops.object.select_all(action='DESELECT')
    for b in baked: b.select_set(True)
    bpy.context.view_layer.objects.active = baked[0]
    if len(baked) > 1: bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    pieces = [o for o in bpy.context.selected_objects if o.type == 'MESH'] or [body]
    parts, saved = [], {}
    def save(img, name):
        if img is None: return ''
        if img.name in saved: return saved[img.name]
        path = os.path.join(out, name + '.png'); img.file_format = 'PNG'
        try: img.save(filepath=path)
        except Exception:
            img.filepath_raw = path; img.save()
        saved[img.name] = os.path.basename(path); return saved[img.name]
    for i, o in enumerate(sorted(pieces, key=lambda p: -len(p.data.polygons))):
        mat = o.data.materials[0] if o.data.materials else None
        o.name = o.data.name = 'part%d' % i
        for p in o.data.polygons: p.use_smooth = True
        base, nrm = (_image(mat, 'Base Color'), _image(mat, 'Normal')) if mat else (None, None)
        bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.use_nodes else None
        col = list(bsdf.inputs['Base Color'].default_value) if bsdf else [1, 1, 1, 1]
        alpha = bool(bsdf and bsdf.inputs['Alpha'].is_linked)
        zs = [v.co.z for v in o.data.vertices]
        parts.append({'mesh': o.name, 'material': mat.name if mat else '', 'tex': save(base, 'tex%d' % i), 'normal': save(nrm, 'nrm%d' % i),
                      'alpha': alpha, 'color': [round(c, 4) for c in col], 'rough': round(bsdf.inputs['Roughness'].default_value, 3) if bsdf else 0.6,
                      'metal': round(bsdf.inputs['Metallic'].default_value, 3) if bsdf and not bsdf.inputs['Metallic'].is_linked else 0.0,
                      'zmin': round(min(zs), 3), 'zmax': round(max(zs), 3), 'tris': sum(len(p.vertices) - 2 for p in o.data.polygons)})
    for o in list(bpy.data.objects):
        if o not in pieces: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'starlet.fbx'), use_selection=True, object_types={'MESH'}, bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
    json.dump({'id': sid, 'height': HEIGHT, 'parts': parts}, open(os.path.join(out, 'manifest.json'), 'w'), indent=1)
    print('AVATAR', sid, 'parts', [(p['mesh'], p['material'], p['tex'], p['normal'], p['alpha'], p['tris']) for p in parts])
    return parts
