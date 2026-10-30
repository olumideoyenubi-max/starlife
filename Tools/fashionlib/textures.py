# Tileable PBR textures for the fashion library: albedo, normal (OpenGL, +Y up, as glTF and Unity expect) and roughness.
# Every map wraps at its edges, so UVs can repeat it. Original patterns only (the silk print is an adire-style motif
# drawn here, the tortoiseshell is noise), no real brand marks.
#   python3 Tools/fashionlib/textures.py <library>/textures [size]
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]; N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)

def vnoise(cells, n=N, seed=0):
    """Periodic value noise: a random grid of cells x cells, smoothly interpolated, wrapping at the edges."""
    g = np.random.default_rng(seed).random((cells, cells))
    t = np.arange(n) * cells / n; i0 = np.floor(t).astype(int); f = t - i0; f = f * f * (3 - 2 * f); i1 = (i0 + 1) % cells
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def fbm(base, octaves=5, n=N, seed=0):
    s = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        s += vnoise(base * 2 ** o, n, seed + o) * amp; tot += amp; amp *= 0.5
    return s / tot

def worley(points, n=N, seed=0):
    """Periodic cellular noise: distance to the nearest and second-nearest of `points` random points (F1, F2)."""
    p = np.random.default_rng(seed).random((points, 2))
    y, x = np.mgrid[0:n, 0:n] / n
    f1 = np.full((n, n), 9.0); f2 = np.full((n, n), 9.0)
    for px, py in p:
        dx = np.abs(x - px); dx = np.minimum(dx, 1 - dx); dy = np.abs(y - py); dy = np.minimum(dy, 1 - dy)
        d = np.sqrt(dx * dx + dy * dy)
        f2 = np.where(d < f1, f1, np.minimum(f2, d)); f1 = np.minimum(f1, d)
    return f1, f2

def norm01(a): a = a - a.min(); return a / max(a.max(), 1e-9)

def normal_map(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength * N / 1024; dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength * N / 1024
    nx, ny, nz = -dx, dy, np.ones_like(h); l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx / l + 1) / 2, (ny / l + 1) / 2, (nz / l + 1) / 2], -1)

def save(name, arr):
    arr = np.clip(np.broadcast_to(arr, (N, N)) if np.ndim(arr) < 2 else arr, 0, 1); im = Image.fromarray((arr * 255 + 0.5).astype(np.uint8)); im.save(os.path.join(OUT, name + '.png'))

def tint(gray, lo, hi):
    lo = np.array(lo) / 255; hi = np.array(hi) / 255; return lo + (hi - lo) * gray[..., None]

def write(name, height, strength, albedo, rough):
    save(name + '_nrm', normal_map(height, strength)); save(name + '_col', albedo); save(name + '_rgh', rough)
    print('texture', name)

# ---- leathers: pebble grain from cellular noise, creases between the pebbles, a little blotch so it is never flat
f1, f2 = worley(1400 if N >= 1024 else 500, seed=1)
pebble = norm01(np.sqrt(norm01(f2 - f1))) * 0.8 + fbm(48, 3, seed=2) * 0.2
blotch = fbm(4, 4, seed=3)
for name, lo, hi, r0, r1 in (('lambskin_black', (8, 8, 9), (26, 25, 27), 0.38, 0.55),
                             ('calf_cognac', (92, 44, 20), (150, 82, 42), 0.42, 0.62),
                             ('calf_ivory', (196, 186, 168), (236, 229, 214), 0.45, 0.6),
                             ('calf_burgundy', (60, 10, 18), (110, 26, 36), 0.4, 0.58)):
    g = 0.55 * pebble + 0.45 * blotch
    write(name, pebble, 3.0 if name != 'lambskin_black' else 2.2, tint(g, lo, hi), r0 + (r1 - r0) * (1 - pebble) * 0.8 + 0.08 * blotch)

# ---- patent leather: almost no grain, very glossy
fine = fbm(24, 3, seed=4)
write('patent_blush', fine * 0.3, 0.6, tint(0.6 + 0.4 * fbm(3, 3, seed=5), (196, 140, 128), (226, 176, 160)), 0.08 + 0.05 * fine)

# ---- suede: dense fine fuzz, matte, the nap lighter in patches
nap = fbm(160, 3, seed=6); patches = fbm(5, 3, seed=7)
write('suede_taupe', nap, 1.2, tint(0.5 * nap + 0.5 * patches, (104, 88, 74), (150, 132, 112)), 0.85 + 0.1 * nap)

# ---- woven fabrics: an over-under weave from two sine families, plus slub (thick and thin threads)
y, x = np.mgrid[0:N, 0:N] / N
def weave(threads, seed):
    wx = np.abs(np.sin(math.pi * x * threads)); wy = np.abs(np.sin(math.pi * y * threads))
    over = (np.floor(x * threads) + np.floor(y * threads)) % 2
    h = np.where(over > 0, wx * 0.6 + wy * 0.25, wy * 0.6 + wx * 0.25)
    slub = vnoise(threads // 4, seed=seed); return norm01(h * (0.8 + 0.4 * slub))
for name, threads, lo, hi, r in (('canvas_natural', 96, (178, 164, 136), (222, 210, 184), 0.85),
                                  ('canvas_navy', 96, (18, 26, 52), (44, 56, 92), 0.82),
                                  ('twill_khaki', 128, (128, 114, 80), (170, 156, 116), 0.8),
                                  ('webbing_olive', 64, (58, 64, 38), (96, 104, 64), 0.75)):
    h = weave(threads, threads); dirt = fbm(6, 4, seed=threads + 1)
    write(name, h, 2.0, tint(0.65 * h + 0.35 * dirt, lo, hi), r + 0.08 * (1 - h))

# ---- ripstop nylon: smooth with a reinforcing grid every few millimetres, a slight sheen
grid = np.maximum(np.abs(np.sin(math.pi * x * 24)) ** 40, np.abs(np.sin(math.pi * y * 24)) ** 40)
tex = weave(160, 9) * 0.3 + grid * 0.7
write('nylon_black', tex, 1.4, tint(0.4 * tex + 0.6 * fbm(4, 3, seed=11), (14, 14, 16), (34, 34, 38)), 0.45 + 0.15 * grid)

# ---- felt: matted random fibres, very matte
fib = fbm(90, 5, seed=12)
write('felt_camel', fib, 1.6, tint(0.6 * fib + 0.4 * fbm(5, 3, seed=13), (140, 102, 62), (186, 144, 98)), 0.92)

# ---- satin: smooth, a soft directional sheen comes from the shader; just a faint warp texture
sat = np.abs(np.sin(math.pi * y * 400)) * 0.2 + fbm(8, 3, seed=14) * 0.8
write('satin_emerald', sat * 0.2, 0.5, tint(sat, (8, 70, 52), (20, 110, 82)), 0.25 + 0.05 * sat)

# ---- rubber and EVA: fine pores
por = 1 - norm01(worley(900 if N >= 1024 else 400, seed=15)[0]) ** 0.3
write('rubber_white', por * 0.4, 1.0, tint(0.7 + 0.3 * fbm(6, 3, seed=16), (214, 210, 200), (240, 238, 232)), 0.7)
write('rubber_black', por * 0.4, 1.0, tint(fbm(6, 3, seed=17), (16, 16, 16), (30, 30, 30)), 0.72)

# ---- metals: brushed lines along U, plus fine wear
lines = vnoise(4, seed=18)[:, :1] * 0 + np.random.default_rng(19).random((N, 1)); lines = np.repeat(lines, N, 1)
lines = np.array(Image.fromarray((lines * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))) / 255.0
wear = fbm(12, 4, seed=20)
write('gold_brushed', lines * 0.5, 0.8, tint(0.5 + 0.5 * wear, (196, 150, 70), (228, 186, 104)), 0.22 + 0.12 * lines)
write('gold_polished', wear * 0.05, 0.3, tint(0.5 + 0.5 * wear, (212, 168, 86), (240, 200, 120)), 0.08 + 0.06 * wear)
write('steel_brushed', lines * 0.5, 0.8, tint(0.5 + 0.5 * wear, (150, 152, 156), (196, 198, 202)), 0.25 + 0.12 * lines)
write('silver_polished', wear * 0.05, 0.3, tint(0.5 + 0.5 * wear, (200, 202, 208), (232, 234, 238)), 0.1 + 0.05 * wear)

# ---- tortoiseshell acetate: warm blotches of amber and near-black
t1 = fbm(5, 5, seed=21); t2 = fbm(11, 4, seed=22); shell = np.clip((t1 * 0.7 + t2 * 0.3 - 0.35) * 2.4, 0, 1)
alb = tint(shell, (150, 82, 22), (20, 10, 6)) * (0.9 + 0.1 * t2[..., None])
write('acetate_tortoise', t2 * 0.05, 0.3, alb, 0.12 + 0.04 * t2)

# ---- silk carré print: an original adire-style resist-dye motif (indigo ground, pale linework, a gold border)
S = 4; W = N * S
im = Image.new('RGB', (W, W), (24, 38, 92)); d = ImageDraw.Draw(im)
pale = (226, 232, 244); gold = (214, 168, 80); cells = 4; c = W // cells
for gy in range(cells):
    for gx in range(cells):
        x0, y0 = gx * c, gy * c; cx, cy = x0 + c // 2, y0 + c // 2; kind = (gx + gy) % 3
        if kind == 0:      # concentric circles with a spoked centre
            for r in range(c // 2 - 30, 20, -38): d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=pale, width=10)
            for k in range(12):
                a = k * math.pi / 6; d.line([cx, cy, cx + math.cos(a) * c * 0.18, cy + math.sin(a) * c * 0.18], fill=pale, width=8)
        elif kind == 1:    # nested diamonds with dots
            for r in range(c // 2 - 30, 30, -44): d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], outline=pale, width=10)
            for k in range(8): a = k * math.pi / 4; rr = c * 0.36; d.ellipse([cx + math.cos(a) * rr - 14, cy + math.sin(a) * rr - 14, cx + math.cos(a) * rr + 14, cy + math.sin(a) * rr + 14], fill=gold)
        else:              # wave stripes
            for k in range(9):
                pts = [(x0 + t, y0 + 30 + k * (c - 60) / 8 + math.sin(t / c * math.tau * 2) * 18) for t in range(0, c + 1, 8)]
                d.line(pts, fill=pale, width=9)
        d.rectangle([x0 + 6, y0 + 6, x0 + c - 6, y0 + c - 6], outline=gold, width=8)
im = im.resize((N, N), Image.LANCZOS)
bleed = fbm(30, 3, seed=23)      # resist dye is never crisp: soften and mottle it
alb = np.array(im.filter(ImageFilter.GaussianBlur(0.8))) / 255.0 * (0.88 + 0.12 * bleed[..., None])
write('silk_adire', weave(220, 24) * 0.15, 0.4, alb, 0.3 + 0.08 * bleed)

# ---- the dial: a sunburst for the luxury watch, matte black with numerals for the field watch (single images, not tiling)
D = 1024
dial = Image.new('RGB', (D, D), (232, 226, 210)); d = ImageDraw.Draw(dial); c = D / 2
for k in range(360): a = k * math.pi / 180; v = 222 + int(14 * math.sin(k * 0.7)); d.line([c, c, c + math.cos(a) * D, c + math.sin(a) * D], fill=(v, v - 6, v - 20), width=3)
for k in range(12):
    a = k * math.pi / 6 - math.pi / 2; r0, r1 = D * 0.36, D * 0.44
    d.line([c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a) * r1, c + math.sin(a) * r1], fill=(150, 112, 40), width=16 if k % 3 == 0 else 9)
for k in range(60): a = k * math.pi / 30; d.line([c + math.cos(a) * D * 0.465, c + math.sin(a) * D * 0.465, c + math.cos(a) * D * 0.48, c + math.sin(a) * D * 0.48], fill=(70, 60, 40), width=3)
dial.save(os.path.join(OUT, 'dial_sunburst.png'))
dial = Image.new('RGB', (D, D), (20, 22, 22)); d = ImageDraw.Draw(dial)
from PIL import ImageFont
try: f = ImageFont.truetype('/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf', 96)
except Exception: f = ImageFont.load_default()
for k in range(1, 13):
    a = k * math.pi / 6 - math.pi / 2; r = D * 0.36
    d.text((c + math.cos(a) * r, c + math.sin(a) * r), str(k), font=f, fill=(232, 228, 214), anchor='mm')
for k in range(60):
    a = k * math.pi / 30; r0 = D * (0.44 if k % 5 else 0.42)
    d.line([c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a) * D * 0.47, c + math.sin(a) * D * 0.47], fill=(232, 228, 214), width=8 if k % 5 == 0 else 4)
dial.save(os.path.join(OUT, 'dial_field.png'))
print('dials')
