# Sculpts a starlet's face and bakes the sculpt onto her game head. Called by wardrobe_export.py for the head piece.
#   high-poly copy (two more subdivision levels) -> primary forms (cheekbones, brow, nose bridge, lips, chin, jaw,
#   slight asymmetry) -> secondary forms (smile lines, philtrum, lip line, under-eye) -> pores
#   -> baked to a tangent-space normal map and an occlusion pass multiplied into the skin colour.
# The primary forms are also applied to the game mesh itself, so the silhouette changes too. Strokes are placed from
# each face's own landmarks (eye joints, nose tip found on the mesh), so every face keeps its character.
# The sculpt is saved to <art>/faces/<id>_sculpt.blend: open it to carry on by hand with the sculpt brushes.
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector, noise

def _front(co, x, z, r=0.004):
    """The front-most point of the face near (x, z)."""
    best = None
    for c in co:
        if abs(c.x - x) < r and abs(c.z - z) < r and (best is None or c.y < best.y): best = c
    return best.copy() if best is not None else None

def landmarks(mesh, marks):
    co = [v.co for v in mesh.vertices]
    eye = Vector(marks['eyeL']) if marks.get('eyeL') else Vector((0.03, -0.09, 1.585)); ex, ez = abs(eye.x), eye.z
    band = [c for c in co if abs(c.x) < 0.01 and ez - 0.065 < c.z < ez - 0.012]
    nose = min(band, key=lambda c: c.y).copy() if band else Vector((0, -0.13, ez - 0.035))
    L = {'eye_x': ex, 'eye_z': ez, 'nose': nose}
    L['mouth'] = _front(co, 0, nose.z - 0.034, 0.006) or nose + Vector((0, 0.012, -0.034))
    L['chin'] = _front(co, 0, nose.z - 0.078, 0.008) or nose + Vector((0, 0.02, -0.078))
    L['face_y'] = nose.y
    return L

def strokes(L):
    """The sculpt as a list of dabs: (centre x, z; radius; push out along the normal in metres; primary form?).
    Each dab lands on the surface at the front of the face. Mirrored ones come in pairs."""
    ex, ez, n, m, c = L['eye_x'], L['eye_z'], L['nose'], L['mouth'], L['chin']; S = []
    def dab(x, z, r, amt, primary=True, mirror=True):
        S.append((x, z, r, amt, primary))
        if mirror and abs(x) > 1e-4: S.append((-x, z, r, amt * 0.94, primary))        # the two sides are never quite alike
    dab(ex * 1.55, ez - 0.030, 0.020, 0.0016)                    # cheekbones
    dab(ex * 1.05, ez + 0.019, 0.015, 0.0011)                    # brow ridge
    dab(0, ez + 0.004, 0.009, 0.0008, mirror=False)              # bridge of the nose
    dab(0.011, n.z + 0.002, 0.007, 0.0006)                       # wings of the nose
    dab(0, m.z + 0.006, 0.012, 0.0009, mirror=False)             # upper lip
    dab(0, m.z - 0.007, 0.013, 0.0012, mirror=False)             # lower lip
    dab(0, c.z + 0.004, 0.014, 0.0011, mirror=False)             # chin
    dab(ex * 1.9, m.z - 0.012, 0.022, 0.0009)                    # jaw
    dab(ex * 1.25, ez - 0.058, 0.016, -0.0007)                   # hollow under the cheekbone
    for t in [i / 27 for i in range(28)]:                        # smile lines: from the wing of the nose, round the mouth
        x = 0.016 + 0.014 * t + 0.004 * math.sin(t * math.pi); z = n.z + 0.004 + (m.z - 0.012 - n.z - 0.004) * t
        dab(x, z, 0.0042, -0.00016 * (1 - 0.5 * t), primary=False)       # kept faint: deeper lines age a young face
    for t in [i / 9 for i in range(1, 9)]: dab(0, n.z - 0.006 + (m.z + 0.011 - n.z + 0.006) * t, 0.0032, -0.00028, False, False)     # philtrum
    for t in [i / 20 for i in range(21)]: dab(0.021 * t, m.z - 0.0005 - 0.0012 * t * t, 0.0026, -0.00034, False)             # the line between the lips
    for t in [i / 15 for i in range(16)]: dab(ex * (0.5 + 0.9 * t), ez - 0.016 - 0.004 * math.sin(t * math.pi), 0.0045, -0.00012, False)   # under the eye
    return S

def sculpt(mesh, L, detail):
    """Apply the dabs to a mesh. detail: also the secondary forms and pores (the high-poly copy)."""
    co = np.array([v.co[:] for v in mesh.vertices]); nrm = np.array([v.normal[:] for v in mesh.vertices]); move = np.zeros(len(co))
    face = (co[:, 1] < L['face_y'] + 0.075)                       # only the front of the head
    for x, z, r, amt, primary in strokes(L):
        if not primary and not detail: continue
        near = face & (np.abs(co[:, 0] - x) < r * 1.2) & (np.abs(co[:, 2] - z) < r * 1.2)
        if not near.any(): continue
        y = co[near][:, 1].min(); d = np.sqrt((co[:, 0] - x) ** 2 + (co[:, 2] - z) ** 2 + ((co[:, 1] - y) * 0.6) ** 2)
        move += np.where(face, amt * np.exp(-(d / (r * 0.6)) ** 2), 0)
    for i in np.nonzero(face | True)[0]:                          # asymmetry: a broad, very slight unevenness over the whole head
        p = Vector(co[i]); move[i] += 0.00045 * noise.noise(p * 14.0)
        if detail: move[i] += 0.00010 * noise.noise(p * 900.0) + 0.00006 * noise.noise(p * 2100.0)      # pores and fine grain
    co += nrm * move[:, None]
    mesh.vertices.foreach_set('co', co.ravel()); mesh.update()

def _node(mat, kind): return next((n for n in mat.node_tree.nodes if n.type == kind), None)

def run(o, marks, sid, art_dir, size=2048, ao_strength=0.55):
    """o: the head piece (active, with UVs and its material). Sculpts and bakes; leaves o's material using the baked maps."""
    sc = bpy.context.scene; L = landmarks(o.data, marks)
    high = o.copy(); high.data = o.data.copy(); high.name = sid + '_face_sculpt'; sc.collection.objects.link(high)
    bpy.ops.object.select_all(action='DESELECT'); high.select_set(True); bpy.context.view_layer.objects.active = high
    m = high.modifiers.new('sculpt levels', 'SUBSURF'); m.levels = 2; bpy.ops.object.modifier_apply(modifier='sculpt levels')
    sculpt(high.data, L, True)
    for p in high.data.polygons: p.use_smooth = True
    sculpt(o.data, L, False)                                       # the big forms on the game mesh as well
    mat = o.data.materials[0].copy(); o.data.materials[0] = mat; nt = mat.node_tree; bsdf = _node(mat, 'BSDF_PRINCIPLED')
    def target(name, colour, noncolor):
        img = bpy.data.images.new(sid + name, size, size, alpha=False); img.generated_color = colour
        if noncolor: img.colorspace_settings.name = 'Non-Color'
        node = nt.nodes.new('ShaderNodeTexImage'); node.image = img
        for n in nt.nodes: n.select = False
        node.select = True; nt.nodes.active = node; return img, node
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 12; sc.cycles.use_denoising = False
    bk = sc.render.bake; bk.use_selected_to_active = True; bk.cage_extrusion = 0.003; bk.max_ray_distance = 0.008; bk.margin = 8
    bpy.ops.object.select_all(action='DESELECT'); high.select_set(True); o.select_set(True); bpy.context.view_layer.objects.active = o
    n_img, n_node = target('_head_n', (0.5, 0.5, 1, 1), True)
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')
    # the baked normal map replaces the old one (whose detail the sculpt carried over), behind a Normal Map node
    nm = _node(mat, 'NORMAL_MAP')
    if nm is None: nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    nt.links.new(n_node.outputs['Color'], nm.inputs['Color'])
    # occlusion from the sculpt (the creases and hollows) is multiplied into the skin colour
    base_node = None
    if bsdf.inputs['Base Color'].is_linked:
        todo = [bsdf.inputs['Base Color'].links[0].from_node]
        while todo:
            n = todo.pop(0)
            if n.type == 'TEX_IMAGE' and n.image: base_node = n; break
            todo += [i.links[0].from_node for i in n.inputs if i.is_linked]
    if base_node is not None:
        b = base_node.image; w, h = b.size; base = np.array(b.pixels[:], dtype=np.float32).reshape(h, w, b.channels)
        # a cavity map from the baked normals: where the surface folds inwards (creases, pores, the lip line) the skin is a little darker
        nm_px = np.array(n_img.pixels[:], dtype=np.float32).reshape(size, size, 4)
        div = np.gradient(nm_px[:, :, 0], axis=1) + np.gradient(nm_px[:, :, 1], axis=0)
        cav = np.clip(-div * 9.0, 0, 1); cav[(np.abs(div) > 0.2)] = 0                      # the edges of the texture's islands are not creases
        cav = cav[(np.arange(h) * size // h)[:, None], (np.arange(w) * size // w)[None, :]]
        out = np.ones((h, w, 4), dtype=np.float32); out[:, :, :3] = base[:, :, :3] * (1 - ao_strength * cav)[:, :, None]
        if b.channels == 4: out[:, :, 3] = base[:, :, 3]
        c_img = bpy.data.images.new(sid + '_head_c', w, h, alpha=True); c_img.pixels = out.ravel(); base_node.image = c_img
    # keep the sculpt for hand work, out of the way of the export
    os.makedirs(os.path.join(art_dir, 'faces'), exist_ok=True)
    keep = set(bpy.data.objects); bpy.data.libraries.write(os.path.join(art_dir, 'faces', sid + '_sculpt.blend'), {high, o}, fake_user=True)
    tris = sum(len(p.vertices) - 2 for p in high.data.polygons); bpy.data.objects.remove(high, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    print('SCULPTED', sid, 'high', tris, 'tris, game head', sum(len(p.vertices) - 2 for p in o.data.polygons), 'tris, maps', size, 'nose', [round(v, 3) for v in L['nose']], 'mouth z', round(L['mouth'].z, 3))
