import bpy, sys
src = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in bpy.data.objects:
    extra = ''
    if o.type == 'MESH':
        extra = 'verts %d mats %s shapekeys %d' % (len(o.data.vertices), [m.name for m in o.data.materials], len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0)
    if o.type == 'ARMATURE':
        extra = 'bones ' + ' '.join(b.name for b in o.data.bones if any(k in b.name for k in ('Arm', 'Shoulder', 'Head', 'Neck', 'Hips', 'Spine')))
    print('OBJ', o.type, o.name, [round(v, 2) for v in o.dimensions], extra)
