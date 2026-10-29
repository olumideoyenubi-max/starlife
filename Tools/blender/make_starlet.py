# Builds the 3D starlet from the Sketchfab "Valery" avatar (CC-BY, xander.dice):
#   Blender -b -P make_starlet.py -- <scene.gltf> <out dir>
# Cleans the import, names the parts, relaxes the T-pose, adds recolour controls for hair and outfit,
# lights it, renders previews and saves starlet.blend / starlet.glb.
import bpy, sys, os, math
from mathutils import Matrix, Vector
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
os.makedirs(os.path.join(out, 'renders'), exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

# ---- clean up and name the parts
names = {'avaturn_body_material': 'Body', 'avaturn_hair_0_material': 'HairInner', 'avaturn_hair_1_material': 'Hair', 'avaturn_look_0_material': 'Outfit', 'avaturn_shoes_0_material': 'Shoes'}
parts = {}
for o in list(bpy.data.objects):
    if o.type == 'MESH':
        m = o.data.materials[0].name if o.data.materials else None
        if m in names: o.name = o.data.name = names[m]; o.data.materials[0].name = names[m]; parts[names[m]] = o
        else: bpy.data.objects.remove(o, do_unlink=True)   # the viewer's backdrop sphere
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); arm.name = 'StarletRig'
for o in parts.values():
    for p in o.data.polygons: p.use_smooth = True

# ---- relax the T-pose: arms down by the sides, a slight bend at the elbow
bpy.context.view_layer.update()
def swing(bone, deg):
    pb = arm.pose.bones[bone]; mw = arm.matrix_world @ pb.matrix; head = mw.translation.copy()
    sign = 1 if (arm.matrix_world @ pb.tail).x > head.x else -1
    r = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg * sign), 4, 'Y') @ Matrix.Translation(-head)
    pb.matrix = arm.matrix_world.inverted() @ r @ mw; bpy.context.view_layer.update()
def find(key): return next(b.name for b in arm.pose.bones if b.name.startswith(key))
for side in ('Left', 'Right'):
    swing(find(side + 'Arm'), 74); swing(find(side + 'ForeArm'), 8)

# ---- recolour controls: a Hue/Saturation/Value node after each base-colour texture
def recolour(mat_name, hue=0.5, sat=1.0, val=1.0):
    mat = bpy.data.materials[mat_name]; nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); inp = bsdf.inputs['Base Color']
    hsv = nt.nodes.get('StarlifeTint')
    if hsv is None:
        if not inp.is_linked: print('NO BASE COLOUR LINK', mat_name); return
        srcsock = inp.links[0].from_socket
        hsv = nt.nodes.new('ShaderNodeHueSaturation'); hsv.name = hsv.label = 'StarlifeTint'
        nt.links.new(srcsock, hsv.inputs['Color']); nt.links.new(hsv.outputs['Color'], inp)
    hsv.inputs['Hue'].default_value = hue; hsv.inputs['Saturation'].default_value = sat; hsv.inputs['Value'].default_value = val
for m in ('Hair', 'HairInner', 'Outfit', 'Shoes', 'Body'): recolour(m)
skin = bpy.data.materials['Body'].node_tree.nodes.get('Principled BSDF') or next(n for n in bpy.data.materials['Body'].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
for k, v in (('Subsurface Weight', 0.12), ('Subsurface Scale', 0.02)):
    if k in skin.inputs: skin.inputs[k].default_value = v

# ---- studio: soft key, fill, rim, and a Starlife-pink backdrop
def area(name, loc, energy, size, color=(1, 1, 1)):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = energy; l.size = size; l.color = color
    o = bpy.data.objects.new(name, l); bpy.context.scene.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); return o
area('Key', (-1.6, -2.6, 2.4), 110, 2.0, (1, .96, .92)); area('Fill', (2.2, -2.2, 1.4), 35, 3.0, (.93, .95, 1)); area('Rim', (0.8, 2.4, 2.4), 90, 1.5, (1, .9, .95))
world = bpy.data.worlds.new('Studio'); bpy.context.scene.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.94, 0.72, 0.79, 1); world.node_tree.nodes['Background'].inputs[1].default_value = 0.3
bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0)); floor = bpy.context.object; floor.name = 'Backdrop'
fm = bpy.data.materials.new('Backdrop'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.94, 0.74, 0.80, 1); fm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.9; floor.data.materials.append(fm)

cam = bpy.data.objects.new('Camera', bpy.data.cameras.new('Camera')); bpy.context.scene.collection.objects.link(cam); bpy.context.scene.camera = cam
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48; sc.cycles.use_denoising = True
sc.render.image_settings.file_format = 'PNG'; sc.view_settings.view_transform = 'Standard'
head = arm.matrix_world @ arm.pose.bones[find('Head')].head
def shot(name, target, dist, lens, w, h):
    cam.data.lens = lens; cam.location = (target[0], target[1] - dist, target[2])
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.filepath = os.path.join(out, 'renders', name + '.png'); bpy.ops.render.render(write_still=True)
shot('full', (0, 0, 0.86), 3.9, 70, 560, 900)
shot('portrait', (head.x, head.y, head.z + 0.09), 1.25, 85, 640, 640)
# two restyles to show the controls: auburn hair with a rose outfit, platinum hair with a teal outfit
recolour('Hair', 0.52, 1.5, 1.6); recolour('HairInner', 0.52, 1.5, 1.6); recolour('Outfit', 0.93, 1.3, 1.1); shot('restyle-auburn-rose', (0, 0, 0.86), 3.9, 70, 560, 900)
recolour('Hair', 0.56, 0.35, 4.0); recolour('HairInner', 0.56, 0.35, 4.0); recolour('Outfit', 0.02, 1.2, 1.0); shot('restyle-platinum', (0, 0, 0.86), 3.9, 70, 560, 900)
for m in ('Hair', 'HairInner', 'Outfit'): recolour(m)

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'starlet.blend'))
for o in bpy.data.objects: o.select_set(o.type in ('MESH', 'ARMATURE') and o.name != 'Backdrop')
bpy.ops.export_scene.gltf(filepath=os.path.join(out, 'starlet.glb'), use_selection=True, export_format='GLB')
print('STARLET DONE', sorted(parts))
