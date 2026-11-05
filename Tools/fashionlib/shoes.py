# Shoes on a proper last: olivia's foot is sliced into cross-sections (rays cast inward round each slice), each slice is
# eased outward and resampled into a clean quad strip, and the upper is lofted through them and trimmed to the style's
# topline exactly (the strip is cut where it crosses the topline, so the edge is a smooth curve, not a staircase).
# The sole follows the last's footprint. Used by make_library.py (imported after its helpers are defined).
import bpy, bmesh, math
from mathutils import Vector, Matrix

ZS = 0.027          # where the upper meets the sole (olivia's sole is flat at z 0.022)
M = 32              # points round each section of the upper

def _resample(pts, n):
    if len(pts) < 2: return [pts[0].copy() for _ in range(n)]
    L = [0.0]
    for a, b in zip(pts[:-1], pts[1:]): L.append(L[-1] + (b - a).length)
    out = []; j = 0
    for k in range(n):
        s = L[-1] * k / (n - 1)
        while j < len(L) - 2 and L[j + 1] < s: j += 1
        t = (s - L[j]) / max(L[j + 1] - L[j], 1e-9); out.append(pts[j].lerp(pts[j + 1], t))
    return out

class Last:
    """The last for one foot (side +1 left, -1 right), eased outward by `ease` metres."""
    def __init__(self, B, side, ease=0.004, stations=34):
        self.B, self.side, self.ease = B, side, ease
        vs = [v.co.copy() for v in B.parts['feet'].data.vertices if v.co.x * side > 0]
        self.vs = vs; self.toe = min(v.y for v in vs); self.heel = max(v.y for v in vs)
        self.ys = [self.heel - 0.005 - (self.heel - self.toe - 0.027) * k / (stations - 1) for k in range(stations)]   # stop short of the big toe; the cap makes the nose
        rings = [self.section(y) for y in self.ys]           # dense rings, starting at the bottom, going outward-up-over-inward
        for _ in range(3):                                    # smooth along the foot too (foot and leg sections alternate near the ankle)
            rings = [rings[0]] + [[(a[k] + b[k] * 2 + c[k]) / 4 for k in range(96)] for a, b, c in zip(rings[:-2], rings[1:-1], rings[2:])] + [rings[-1]]
            for r, y in zip(rings, self.ys):
                for p in r: p.y = y
        self.rings = rings

    def hit(self, origin, d):
        """The outermost of the foot and the lower leg (the body mesh carries the ankle above the foot mesh)."""
        best = (None, None, 9.0)
        for k in ('feet', 'body'):
            loc, nrm, _, dist = self.B.trees[k].ray_cast(origin, d, 0.25)
            if loc is not None and k == 'feet' and dist < best[2]: best = (loc, nrm, dist)
        return best[0], best[1]

    def hull(self, pts, c):
        """The ring's convex hull in the section plane, sampled back along the same rays from c."""
        P = sorted({(round(p.x, 6), round(p.z, 6)) for p in pts})
        def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
        lo = []; hi = []
        for q in P:
            while len(lo) >= 2 and cross(lo[-2], lo[-1], q) <= 0: lo.pop()
            lo.append(q)
        for q in reversed(P):
            while len(hi) >= 2 and cross(hi[-2], hi[-1], q) <= 0: hi.pop()
            hi.append(q)
        H = lo[:-1] + hi[:-1]; out = []
        for p in pts:
            d = Vector((p.x - c.x, p.z - c.z)); best = None
            for i in range(len(H)):
                a = Vector(H[i]); b = Vector(H[(i + 1) % len(H)]); e = b - a; den = d.x * e.y - d.y * e.x
                if abs(den) < 1e-12: continue
                w = a - Vector((c.x, c.z)); t = (w.x * e.y - w.y * e.x) / den; u = (w.x * d.y - w.y * d.x) / den
                if t > 0 and -1e-6 <= u <= 1 + 1e-6 and (best is None or t > best): best = t
            out.append(Vector((c.x + d.x * best, p.y, c.z + d.y * best)) if best else p)
        return out

    def section(self, y):
        band = [v for v in self.vs if abs(v.y - y) < 0.008]
        c = Vector((sum(v.x for v in band) / len(band), y, max(sum(v.z for v in band) / len(band), 0.045)))
        pts = []
        for k in range(96):
            a = -math.pi / 2 + k * math.tau / 96 * self.side          # start at the bottom, go round via the outer side
            d = Vector((math.cos(a), 0, math.sin(a)))
            loc, nrm = self.hit(c + d * 0.2, -d)
            if loc is None: pts.append(None); continue
            n = Vector((nrm.x, 0, nrm.z)); n = n.normalized() if n.length > 1e-6 else d
            p = loc + n * self.ease; p.y = y; pts.append(p)
        for i in range(96):                                           # fill misses from the neighbours
            if pts[i] is None:
                j = next(((i + k) % 96 for k in range(1, 96) if pts[(i + k) % 96] is not None))
                pts[i] = pts[j].copy()
        pts = self.hull(pts, c)                                       # a shoe bridges hollows (toe clefts, the arch): use the convex hull
        for _ in range(3):                                            # smooth out facets of the low-poly foot
            pts = [(pts[i - 1] + pts[i] * 2 + pts[(i + 1) % 96]) / 4 for i in range(96)]
        return pts

    def top(self, y):
        """Height of the top of the last at y."""
        k = min(range(len(self.ys)), key=lambda i: abs(self.ys[i] - y)); return max(p.z for p in self.rings[k])

    def run(self, ring):
        """The part of a ring above the sole line, outer sole edge first, as a polyline (with exact end points on ZS)."""
        idx = [i for i, p in enumerate(ring) if p.z > ZS]
        if not idx: return [ring[0], ring[1]]
        i0, i1 = idx[0], idx[-1]; seq = ring[i0:i1 + 1]
        def edge(a, b):
            t = (ZS - a.z) / max(b.z - a.z, 1e-9); return a.lerp(b, max(0.0, min(1.0, t)))
        return [edge(ring[i0 - 1], ring[i0])] + seq + [edge(ring[(i1 + 1) % 96], ring[i1])]

    def strips(self, topline):
        """Per station, M points: either one arc over the top (closed) or two side arcs cut at the topline (open: gap after M//2-1)."""
        out = []
        for y, ring in zip(self.ys, self.rings):
            r = self.run(ring); h = topline(y)
            top = max(p.z for p in r)
            if h >= top - 1e-4: out.append((False, _resample(r, M))); continue
            k0 = next((i for i in range(1, len(r)) if r[i].z > h), None); k1 = next((i for i in range(len(r) - 2, -1, -1) if r[i].z > h), None)
            def cut(a, b): t = (h - a.z) / max(b.z - a.z, 1e-9); return a.lerp(b, t)
            A = r[:k0] + [cut(r[k0 - 1], r[k0])]; Bs = [cut(r[k1 + 1], r[k1])] + r[k1 + 1:]
            out.append((True, _resample(A, M // 2) + _resample(Bs, M // 2)))
        return out

    def dome(self, rows, forward, n=7, zkeep=0.35, reach=0.011):
        """Rounded end caps: rings shrunk toward their centre and pushed out along the foot. `zkeep`: how much height the
        cap keeps (a toe slopes down to the sole, a heel counter stays nearly full height)."""
        caps = []
        last = rows[-1][1]; c = sum(last, Vector()) / len(last); c.z = ZS
        for k in range(1, n + 1):
            t = (0.995 if zkeep >= 0.5 else 0.985) * math.sin(k / n * math.pi / 2); s = math.sqrt(1 - t * t)
            if zkeep < 0.5:     # a toe: shrink round a point a third of the way up, so the end closes as a rounded nose
                zc = ZS + (max(p.z for p in last) - ZS) * 0.33
                caps.append((rows[-1][0], [Vector((c.x + (p.x - c.x) * s, p.y + forward * t * reach, zc + (p.z - zc) * s)) for p in last]))
            else:
                caps.append((rows[-1][0], [Vector((c.x + (p.x - c.x) * s, p.y + forward * t * reach, ZS + (p.z - ZS) * (zkeep + (1 - zkeep) * s))) for p in last]))
        return caps

    def upper(self, name, topline, material, thick=0.0018, yrange=None):
        """The upper (or, with yrange, an overlay panel over part of the foot: end caps only where the range reaches an end)."""
        rows = self.strips(topline); heel_cap = toe_cap = True
        if yrange:
            keep = [r for r in rows if yrange[0] <= r[1][0].y <= yrange[1]]
            heel_cap = rows[0] in keep; toe_cap = rows[-1] in keep; rows = keep
        rows = (list(reversed(self.dome(list(reversed(rows)), +1, zkeep=0.92, reach=0.008))) if heel_cap else []) + rows + (self.dome(rows, -1, reach=0.02 + self.ease) if toe_cap else [])
        bm = bmesh.new(); grid = [[bm.verts.new(p) for p in pts] for _, pts in rows]
        for i in range(len(rows) - 1):
            open_ = rows[i][0] or rows[i + 1][0]
            for j in range(M - 1):
                if open_ and j == M // 2 - 1: continue
                bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
        def close(row, opened, push):
            if opened:      # an open end (the back of a heel): bridge the outer strip to the inner one, height for height
                for j in range(M // 2 - 1): bm.faces.new((row[j], row[j + 1], row[M - 2 - j], row[M - 1 - j]))
            else:           # a closed end (a toe): a small fan to a point just beyond it
                tip = sum((v.co for v in row), Vector()) / M; tip.y += push; tv = bm.verts.new(tip)     # the ring's centre, a little beyond it: a convex nose
                for j in range(M - 1): bm.faces.new((row[j], row[j + 1], tv))
        if toe_cap: close(grid[-1], False, -0.0008)
        if heel_cap: close(grid[0], rows[0][0], 0.0015)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        o = self.B_mesh(name, bm, material); o.modifiers.new('Solidify', 'SOLIDIFY').thickness = thick
        o.modifiers['Solidify'].offset = -1; o.modifiers['Solidify'].use_even_offset = False
        s = o.modifiers.new('Subdivision', 'SUBSURF'); s.levels = 1; s.render_levels = 2
        self.rows = rows; return o

    def footprint(self, grow=0.003):
        """Outline at the sole line (outer edge heel to toe, then inner edge toe to heel), grown outward."""
        rows = self.rows; outer = [r[1][0] for r in rows]; inner = [r[1][-1] for r in rows]
        c = sum(outer + inner, Vector()) / (2 * len(rows)); ring = outer + inner[::-1]; out = []
        for i, p in enumerate(ring):
            a = ring[i - 1]; b = ring[(i + 1) % len(ring)]; t = (b - a); n = Vector((t.y, -t.x, 0)).normalized()
            if n.dot(Vector((p.x - c.x, p.y - c.y, 0))) < 0: n = -n
            out.append((p.x + n.x * grow, p.y + n.y * grow))
        return out

    def topline_path(self):
        """The upper's edge round the opening (inner side heel to front, then outer side front to heel), for collars and bindings."""
        a = [pts[M // 2 - 1] for op, pts in self.rows if op]; b = [pts[M // 2] for op, pts in self.rows if op]
        return a + b[::-1]

    def across(self, y, u0=0.0, u1=1.0, lift=0.0, n=17):
        """Points across the top of the (closed) upper at station y, from arc parameter u0 to u1, lifted off it."""
        k = min(range(len(self.ys)), key=lambda i: abs(self.ys[i] - y)); r = _resample(self.run(self.rings[k]), 65)
        pts = r[int(u0 * 64):int(u1 * 64) + 1]; c = sum(r, Vector()) / len(r)
        return [p + (p - Vector((c.x, p.y, c.z))).normalized() * lift for p in _resample(pts, n)]

    def B_mesh(self, name, bm, material):
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
        for p in me.polygons: p.use_smooth = True
        if material: me.materials.append(material)
        return o

def sole_mesh(name, ring, z0, z1, material, bevel_w=0.003, taper=0.0, spring=0.0, toe_y=None, heel_y=None):
    """A sole slab from an outline; `spring` lifts the toe end (toe spring), `taper` pulls the bottom in."""
    bm = bmesh.new(); cx = sum(p[0] for p in ring) / len(ring); cy = sum(p[1] for p in ring) / len(ring)
    ys = [p[1] for p in ring]; ty = min(ys) if toe_y is None else toe_y; hy = max(ys) if heel_y is None else heel_y
    def lift(y): f = max(0.0, (y - (ty + 0.06)) / -0.06) if y < ty + 0.06 else 0.0; return spring * f * f
    lo = [bm.verts.new((cx + (x - cx) * (1 - taper), cy + (y - cy) * (1 - taper * 0.5), z0 + lift(y))) for x, y in ring]
    hi = [bm.verts.new((x, y, z1 + lift(y) * 0.5)) for x, y in ring]
    bm.faces.new(lo[::-1]); bm.faces.new(hi); n = len(ring)
    for i in range(n): bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(material)
    m = o.modifiers.new('Bevel', 'BEVEL'); m.width = bevel_w; m.segments = 3; m.limit_method = 'ANGLE'; m.angle_limit = math.radians(40)
    for p in me.polygons: p.use_smooth = True
    return o
