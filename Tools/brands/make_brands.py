# Starlife's brands, drawn: a logo (wordmark and emblem, for signs), a badge (the emblem alone, for blade signs, bags and
# discs) and, for the fashion houses, a monogram tile for all-over prints. All original designs for the game's own
# brands. Then the branded garments: copies of the refitted garments (Assets/Starlets/f_*) whose fabric is the brand's
# monogram, shaded by the original's folds.
#   python3 Tools/brands/make_brands.py [brand ...]   (writes Assets/Resources/Brands and Assets/Starlets/f_*_<brand>)
import os, sys, math, json, shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Brands'); os.makedirs(OUT, exist_ok=True)
SUP = '/System/Library/Fonts/Supplemental/'; SYS = '/System/Library/Fonts/'
def font(name, size, index=0):
    for d in (SUP, SYS):
        p = d + name
        if os.path.exists(p): return ImageFont.truetype(p, size, index=index)
    return ImageFont.truetype(os.path.join(ROOT, 'Assets/Resources/Fonts/DMSans-Bold.ttf'), size)
def hexc(h, a=255): h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)
S = 4      # draw at 4x and scale down, for smooth edges

def canvas(w, h): return Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
def done(im, w, h): return im.resize((w, h), Image.LANCZOS)
def text_c(d, xy, s, f, fill, spacing=0):
    if spacing:
        widths = [d.textlength(ch, font=f) for ch in s]; total = sum(widths) + spacing * (len(s) - 1); x = xy[0] - total / 2
        for ch, w in zip(s, widths): d.text((x, xy[1]), ch, font=f, fill=fill, anchor='lm'); x += w + spacing
    else: d.text(xy, s, font=f, fill=fill, anchor='mm')
def star(d, cx, cy, r, fill, points=4, inner=0.38, rot=0):
    pts = []
    for i in range(points * 2):
        a = rot + i * math.pi / points; rr = r if i % 2 == 0 else r * inner
        pts.append((cx + math.sin(a) * rr, cy - math.cos(a) * rr))
    d.polygon(pts, fill=fill)

# ------------------------------------------------------------------ emblems (drawn into a square of size n, at S)
def emblem(bid, n, col, bg=None):
    im = canvas(n, n); d = ImageDraw.Draw(im); c = n * S / 2; R = n * S / 2 - 6 * S
    if bg: d.ellipse([c - R, c - R, c + R, c + R], fill=bg)
    if bid == 'boutique':          # a four-point star inside a fine double ring, "MS" beneath
        d.ellipse([c - R, c - R, c + R, c + R], outline=col, width=5 * S); d.ellipse([c - R * 0.86, c - R * 0.86, c + R * 0.86, c + R * 0.86], outline=col, width=2 * S)
        star(d, c, c - R * 0.12, R * 0.42, col); text_c(d, (c, c + R * 0.48), 'MS', font('Didot.ttc', int(R * 0.34)), col, spacing=int(R * 0.05))
    elif bid == 'velours':         # a Bodoni V crowned by a fleuron, in a laurel ring
        d.ellipse([c - R, c - R, c + R, c + R], outline=col, width=4 * S)
        for i in range(18):
            for side in (-1, 1):
                a = math.pi / 2 + side * (0.35 + i * 0.13); x = c + math.cos(a) * R * 0.8; y = c + math.sin(a) * R * 0.8
                d.ellipse([x - R * 0.07, y - R * 0.035, x + R * 0.07, y + R * 0.035], fill=col)
        text_c(d, (c, c + R * 0.05), 'V', font('Bodoni 72.ttc', int(R * 1.05)), col)
        star(d, c, c - R * 0.58, R * 0.1, col, points=5, inner=0.45)
    elif bid == 'noir':            # N&O in a hexagon
        pts = [(c + math.cos(math.pi / 6 + i * math.pi / 3) * R, c + math.sin(math.pi / 6 + i * math.pi / 3) * R) for i in range(6)]
        d.polygon(pts, outline=col, width=5 * S); pts2 = [(c + (x - c) * 0.84, c + (y - c) * 0.84) for x, y in pts]; d.polygon(pts2, outline=col, width=2 * S)
        text_c(d, (c, c), 'N&O', font('Copperplate.ttc', int(R * 0.52)), col)
    elif bid == 'hartwell':        # a shield with an H and the year
        w = R * 0.8; top = c - R * 0.85
        shield = [(c - w, top), (c + w, top), (c + w, c + R * 0.05), (c, c + R * 0.95), (c - w, c + R * 0.05)]
        d.polygon(shield, outline=col, width=5 * S); text_c(d, (c, c - R * 0.12), 'H', font('Baskerville.ttc', int(R * 0.95)), col)
        text_c(d, (c, c + R * 0.5), 'EST 1898', font('Copperplate.ttc', int(R * 0.15)), col)
    elif bid == 'aurelle':         # a faceted diamond under a small crown
        g = [(c, c + R * 0.7), (c - R * 0.7, c - R * 0.05), (c - R * 0.4, c - R * 0.4), (c + R * 0.4, c - R * 0.4), (c + R * 0.7, c - R * 0.05)]
        d.polygon(g, outline=col, width=5 * S); d.line([(c - R * 0.7, c - R * 0.05), (c + R * 0.7, c - R * 0.05)], fill=col, width=3 * S)
        for x in (-0.4, 0, 0.4): d.line([(c + R * x, c - R * 0.4), (c, c + R * 0.7)], fill=col, width=2 * S)
        crown = [(c - R * 0.35, c - R * 0.55), (c - R * 0.35, c - R * 0.85), (c - R * 0.17, c - R * 0.68), (c, c - R * 0.92), (c + R * 0.17, c - R * 0.68), (c + R * 0.35, c - R * 0.85), (c + R * 0.35, c - R * 0.55)]
        d.polygon(crown, fill=col)
    elif bid == 'celestine':       # an eight-point star in a crescent
        d.ellipse([c - R, c - R, c + R, c + R], fill=col); d.ellipse([c - R * 0.7, c - R * 1.05, c + R * 1.15, c + R * 0.8], fill=(0, 0, 0, 0))
        star(d, c + R * 0.18, c - R * 0.1, R * 0.5, col, points=8, inner=0.42)
    elif bid == 'glimmer':         # sparkles round a bubbly G
        text_c(d, (c, c + R * 0.05), 'G', font('Futura.ttc', int(R * 1.3), 4), col)
        for (x, y, r) in ((0.62, -0.6, 0.18), (-0.7, -0.45, 0.12), (0.7, 0.5, 0.1)): star(d, c + R * x, c + R * y, R * r, col, inner=0.3)
    elif bid == 'sneakers':        # SV, slanted, with speed lines
        text_c(d, (c + R * 0.08, c), 'SV', font('Futura.ttc', int(R * 0.95), 4), col)
        for i in range(3): d.rounded_rectangle([c - R * 0.98, c - R * 0.28 + i * R * 0.26, c - R * 0.5 + i * R * 0.06, c - R * 0.2 + i * R * 0.26], radius=R * 0.04, fill=col)
    elif bid == 'greenwave':       # three waves in a ring of text
        d.ellipse([c - R, c - R, c + R, c + R], fill=col); d.ellipse([c - R * 0.62, c - R * 0.62, c + R * 0.62, c + R * 0.62], fill=(255, 255, 255, 255))
        for i in range(3):
            y = c - R * 0.28 + i * R * 0.26; pts = [(c - R * 0.5 + k * R * 0.05, y + math.sin(k * 0.6) * R * 0.07) for k in range(21)]
            d.line(pts, fill=col, width=int(R * 0.09), joint='curve')
        f = font('Futura.ttc', int(R * 0.15), 2); txt = 'GREENWAVE  COFFEE  ·  '
        for i, ch in enumerate(txt * 1):
            a = -math.pi / 2 + i * 2 * math.pi / len(txt); x = c + math.cos(a) * R * 0.81; y = c + math.sin(a) * R * 0.81
            ch_im = Image.new('RGBA', (int(R * 0.3), int(R * 0.3)), (0, 0, 0, 0)); ImageDraw.Draw(ch_im).text((R * 0.15, R * 0.15), ch, font=f, fill=(255, 255, 255, 255), anchor='mm')
            ch_im = ch_im.rotate(-math.degrees(a) - 90, resample=Image.BICUBIC); im.alpha_composite(ch_im, (int(x - R * 0.15), int(y - R * 0.15)))
    elif bid == 'bigbun':          # a burger bun, sesame seeds and all, over BIG BUN
        d.ellipse([c - R, c - R, c + R, c + R], fill=hexc('#c8102e')); y = c - R * 0.1
        d.chord([c - R * 0.62, y - R * 0.55, c + R * 0.62, y + R * 0.45], 180, 360, fill=hexc('#ffc72c'))
        for (x, yy) in ((-0.3, -0.25), (0, -0.38), (0.28, -0.24), (-0.12, -0.15), (0.15, -0.12)): d.ellipse([c + R * x - R * 0.04, y + R * yy - R * 0.02, c + R * x + R * 0.04, y + R * yy + R * 0.02], fill=(255, 255, 255, 255))
        d.rounded_rectangle([c - R * 0.62, y + R * 0.02, c + R * 0.62, y + R * 0.14], radius=R * 0.05, fill=hexc('#ffc72c'))
        text_c(d, (c, c + R * 0.5), 'BIG BUN', font('Futura.ttc', int(R * 0.3), 4), hexc('#ffc72c'))
    elif bid == 'sunnyside':       # a sunny-side-up egg with rays
        for i in range(12): a = i * math.pi / 6; d.line([(c + math.cos(a) * R * 0.62, c + math.sin(a) * R * 0.62), (c + math.cos(a) * R * 0.95, c + math.sin(a) * R * 0.95)], fill=col, width=int(R * 0.08))
        d.ellipse([c - R * 0.55, c - R * 0.5, c + R * 0.55, c + R * 0.5], fill=(255, 255, 255, 255)); d.ellipse([c - R * 0.25, c - R * 0.25, c + R * 0.25, c + R * 0.25], fill=col)
    elif bid == 'jollof':          # a steaming pot with adinkra-like dots
        d.rounded_rectangle([c - R * 0.6, c - R * 0.1, c + R * 0.6, c + R * 0.62], radius=R * 0.15, fill=col)
        d.rectangle([c - R * 0.75, c - R * 0.12, c + R * 0.75, c - R * 0.02], fill=col)
        for i in (-1, 0, 1): d.line([(c + i * R * 0.25 + math.sin(k * 0.8) * R * 0.06, c - R * 0.2 - k * R * 0.07) for k in range(9)], fill=col, width=int(R * 0.06))
        for i in range(5): d.ellipse([c - R * 0.4 + i * R * 0.2 - R * 0.035, c + R * 0.3 - R * 0.035, c - R * 0.4 + i * R * 0.2 + R * 0.035, c + R * 0.3 + R * 0.035], fill=(255, 236, 168, 255))
    elif bid == 'bella':           # a script B on a tricolour ribbon
        for i, h in enumerate(('#2e8b3a', '#ffffff', '#c8312a')): d.rectangle([c - R * 0.9 + i * R * 0.6, c + R * 0.35, c - R * 0.3 + i * R * 0.6, c + R * 0.5], fill=hexc(h))
        text_c(d, (c, c - R * 0.15), 'Bella', font('SnellRoundhand.ttc', int(R * 0.75), 2), col)
    elif bid == 'sakura':          # a five-petal blossom
        for i in range(5):
            a = -math.pi / 2 + i * 2 * math.pi / 5; x = c + math.cos(a) * R * 0.42; y = c + math.sin(a) * R * 0.42
            d.ellipse([x - R * 0.36, y - R * 0.36, x + R * 0.36, y + R * 0.36], fill=col)
        d.ellipse([c - R * 0.16, c - R * 0.16, c + R * 0.16, c + R * 0.16], fill=hexc('#b8264f'))
    elif bid == 'gilded':          # a G in a laurel-cornered frame
        d.rectangle([c - R * 0.8, c - R * 0.8, c + R * 0.8, c + R * 0.8], outline=col, width=4 * S); d.rectangle([c - R * 0.68, c - R * 0.68, c + R * 0.68, c + R * 0.68], outline=col, width=2 * S)
        text_c(d, (c, c + R * 0.02), 'G', font('Didot.ttc', int(R * 1.05), 1), col)
    elif bid == 'lumiere':         # a candle flame star
        star(d, c, c - R * 0.1, R * 0.7, col, points=4, inner=0.18); star(d, c, c - R * 0.1, R * 0.38, col, points=4, inner=0.18, rot=math.pi / 4)
        d.line([(c - R * 0.55, c + R * 0.75), (c + R * 0.55, c + R * 0.75)], fill=col, width=3 * S)
    elif bid == 'gym':             # a heartbeat through a ring
        d.ellipse([c - R, c - R, c + R, c + R], outline=col, width=7 * S)
        pts = [(-1, 0), (-0.45, 0), (-0.3, -0.25), (-0.12, 0.45), (0.05, -0.65), (0.22, 0.1), (0.35, 0), (1, 0)]
        d.line([(c + x * R * 0.95, c + y * R * 0.95) for x, y in pts], fill=col, width=int(R * 0.1), joint='curve')
    return done(im, n, n)

# ------------------------------------------------------------------ brands: name, wordmark font, colours (ink on sign, sign, print ground)
BRANDS = {
    'boutique':  ('MAISON STARLIFE', ('Didot.ttc', 0), '#d6b25e', '#1a1714', '#f3ecdf', 'Paris · Starlife'),
    'velours':   ('MAISON VELOURS', ('Bodoni 72.ttc', 0), '#e2c47a', '#4a0f1e', '#5a1424', 'Couture'),
    'noir':      ('NOIR & OR', ('Copperplate.ttc', 1), '#d9b45a', '#0d0d0f', '#141416', 'Maroquinerie'),
    'hartwell':  ('HARTWELL & SONS', ('Baskerville.ttc', 0), '#e8dcc0', '#1f2a44', '#1f2a44', 'Tailors since 1898'),
    'aurelle':   ('Aurelle', ('SnellRoundhand.ttc', 2), '#e2c47a', '#141018', None, 'Haute joaillerie'),
    'celestine': ('CELESTINE', ('Didot.ttc', 0), '#bfe3ff', '#0e1a2c', None, 'Diamonds'),
    'glimmer':   ('glimmer', ('Futura.ttc', 4), '#ff4f8b', '#fff0f6', None, 'accessories'),
    'sneakers':  ('SNEAKER VAULT', ('Futura.ttc', 4), '#ffd166', '#121212', '#f4f4f4', 'est. Starlife Town'),
    'greenwave': ('Greenwave Coffee', ('Futura.ttc', 2), '#1e6b4a', '#f4efe6', None, 'Coffee & pastries'),
    'bigbun':    ('BIG BUN', ('Futura.ttc', 4), '#ffc72c', '#c8102e', None, 'Burgers · Fries · Shakes'),
    'sunnyside': ('Sunny Side', ('SnellRoundhand.ttc', 2), '#3a2a1a', '#f2b134', None, 'Brunch'),
    'jollof':    ('JOLLOF & CO.', ('Rockwell.ttc', 1), '#ffe9a8', '#d9541e', None, 'West African kitchen'),
    'bella':     ('Trattoria Bella', ('SnellRoundhand.ttc', 2), '#fbf3e4', '#2e6b3a', None, 'Cucina italiana'),
    'sakura':    ('SAKURA', ('Didot.ttc', 0), '#f2c6cf', '#1b1b1f', None, 'Omakase'),
    'gilded':    ('THE GILDED', ('Didot.ttc', 0), '#e2c47a', '#3b1f14', None, 'Steakhouse'),
    'lumiere':   ('Maison Lumière', ('Didot.ttc', 2), '#8a6d3b', '#f4f0e8', None, 'Haute cuisine'),
    'gym':       ('PULSE', ('Futura.ttc', 4), '#39e6a0', '#15171d', None, 'Fitness'),
}

def logo(bid):
    name, (fn, fi), ink, sign, ground, tag = BRANDS[bid]; w, h = 1024, 384
    im = canvas(w, h); d = ImageDraw.Draw(im); col = hexc(ink)
    em = emblem(bid, 300, col if bid not in ('bigbun', 'greenwave') else hexc(ink)).resize((300 * S, 300 * S), Image.LANCZOS)
    im.alpha_composite(em, (int(42 * S), int(42 * S)))
    f = font(fn, 120 * S if len(name) < 10 else 96 * S if len(name) < 14 else 80 * S, fi)
    tw = d.textlength(name, font=f); x0 = 370 * S; maxw = (w - 400) * S
    if tw > maxw: f = font(fn, int(f.size * maxw / tw), fi)
    d.text((x0, 165 * S), name, font=f, fill=col, anchor='lm')
    d.text((x0 + 4 * S, 285 * S), tag.upper() if bid not in ('glimmer',) else tag, font=font('Futura.ttc', 34 * S, 2), fill=col[:3] + (210,), anchor='lm')
    return done(im, w, h)

PRINT_INK = {'sneakers': '#121212'}     # where the sign's ink would be too faint on the print's ground
ONLY = set(sys.argv[1:])                # brands to redo (none: all)
def mono(bid):
    """The all-over print: the emblem in a lattice, with small stars between, on the brand's ground."""
    name, _, ink, sign, ground, _ = BRANDS[bid]; ink = PRINT_INK.get(bid, ink)
    n = 512; im = Image.new('RGBA', (n * S, n * S), hexc(ground)); d = ImageDraw.Draw(im)
    col = hexc(ink); dim = col[:3] + (150,)
    e = emblem(bid, 200, col).resize((200 * S, 200 * S), Image.LANCZOS)
    # one emblem in the middle and one on the corners (in quarters, so the tile repeats seamlessly)
    im.alpha_composite(e, (int(156 * S), int(156 * S)))
    big = Image.new('RGBA', (n * 2 * S, n * 2 * S), (0, 0, 0, 0)); big.alpha_composite(e, (int((n - 100) * S), int((n - 100) * S)))
    for (qx, qy) in ((0, 0), (1, 0), (0, 1), (1, 1)):
        q = big.crop(((n - 100 + qx * 100) * S, (n - 100 + qy * 100) * S, (n - 100 + qx * 100 + 100) * S, (n - 100 + qy * 100 + 100) * S))
        im.alpha_composite(q, (int((n - 100) * S) if qx == 0 else 0, int((n - 100) * S) if qy == 0 else 0))
    for (x, y) in ((256, 0), (0, 256), (256, 512), (512, 256)):
        star(d, x * S, y * S, 26 * S, dim, points=4, inner=0.3)
    return im.resize((n, n), Image.LANCZOS)

for bid in BRANDS:
    if ONLY and bid not in ONLY: continue
    logo(bid).save(os.path.join(OUT, bid + '_logo.png'))
    name, _, ink, sign, ground, _ = BRANDS[bid]
    emblem(bid, 512, hexc(ink), hexc(sign)).save(os.path.join(OUT, bid + '_badge.png'))
    if ground: mono(bid).save(os.path.join(OUT, bid + '_mono.png'))
    print('BRAND', bid)

# ------------------------------------------------------------------ branded garments: the brand's monogram for fabric
STAR = os.path.join(ROOT, 'Assets', 'Starlets')
GARMENTS = [('f_monohood', 'boutique', 6), ('f_tee', 'boutique', 6), ('f_silk', 'velours', 7), ('f_wrap', 'velours', 6), ('f_leather', 'noir', 7),
            ('f_tee', 'noir', 6), ('f_blazer', 'hartwell', 7), ('f_runners', 'sneakers', 5), ('f_hoodiejk', 'sneakers', 6)]
for base, bid, reps in GARMENTS:
    if ONLY and bid not in ONLY: continue
    src, dst = os.path.join(STAR, base), os.path.join(STAR, base + '_' + bid)
    os.makedirs(dst, exist_ok=True)
    man = json.load(open(os.path.join(src, 'manifest.json')))
    tile = mono(bid).convert('RGB')
    for f in ('starlet.fbx',): shutil.copyfile(os.path.join(src, f), os.path.join(dst, f))
    for p in man['parts']:
        for key in ('normal',):
            if p.get(key): shutil.copyfile(os.path.join(src, p[key]), os.path.join(dst, p[key]))
        if not p.get('tex'): continue
        orig = Image.open(os.path.join(src, p['tex'])).convert('RGB'); W, H = orig.size
        lum = orig.convert('L'); mean = sum(lum.resize((64, 64)).getdata()) / 4096.0
        shade = lum.point(lambda v: max(0, min(255, int(128 * v / max(mean, 8)))))      # the folds, around mid-grey
        t = tile.resize((W // reps, H // reps), Image.LANCZOS); pattern = Image.new('RGB', (W, H))
        for y in range(0, H, t.size[1]):
            for x in range(0, W, t.size[0]): pattern.paste(t, (x, y))
        out = ImageChops.multiply(pattern, Image.merge('RGB', [shade.point(lambda v: min(255, v * 2))] * 3))
        out.save(os.path.join(dst, p['tex']))
        p['tone'] = [round(c / 255, 4) for c in tile.resize((1, 1), Image.BOX).getpixel((0, 0))]; p['lum'] = round(sum(p['tone']) / 3, 4)
    man['id'] = base + '_' + bid
    json.dump(man, open(os.path.join(dst, 'manifest.json'), 'w'), indent=1)
    print('GARMENT', man['id'])
