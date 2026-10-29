# A designer collection for MPFB characters: new garments made from MPFB library garments (their cut, stitching and
# folds are kept) with new fabrics and prints, my own designs in the manner of luxury houses (no real logos).
#   python3 Tools/mpfb/make_designer.py
import os, re, shutil, uuid, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
D = os.path.expanduser('~/Library/Application Support/Blender/5.2/extensions/.user/blender_org/mpfb/data/clothes')

def shading(img):
    a = np.asarray(img.convert('RGB'), dtype=np.float32) / 255; lum = a @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    m = np.median(lum[lum > 0.03]) if (lum > 0.03).any() else 0.5
    sh = np.clip(lum / max(m, 0.05), 0.35, 1.5)
    # keep only the broad folds and creases: the old print's own motifs, stains and pocket marks are blurred away
    img2 = Image.fromarray((np.clip(sh / 1.5, 0, 1) * 255).astype(np.uint8))
    broad = np.asarray(img2.filter(ImageFilter.MedianFilter(9)).filter(ImageFilter.GaussianBlur(max(img.size) / 256)), np.float32) / 255 * 1.5
    fine = np.asarray(img2.filter(ImageFilter.GaussianBlur(1.0)), np.float32) / 255 * 1.5
    sh = broad * 0.85 + np.clip(fine / np.maximum(broad, 0.05), 0.85, 1.15) * 0.15 * broad
    # flatten what is left of the old print: anything that differs from its surroundings by more than a fold would is evened out
    local = np.asarray(Image.fromarray((np.clip(sh / 1.5, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(max(img.size) / 48)), np.float32) / 255 * 1.5
    dev = np.clip(sh - local, -0.12, 0.12)                   # folds are gentle: big local jumps (graphics, patches, a lighter collar) are cut back
    return np.clip(1.0 + (local - 1.0) * 0.5 + dev, 0.78, 1.18)

def save(arr, path): Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8)).save(path)
def rgb(h): h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], dtype=np.float32)

def fabric(kind, w, h):
    """The new cloth, flat, at the texture's size: an RGB array 0..1."""
    rnd = np.random.default_rng(7); y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    if kind == 'monogram':                        # tan canvas with my own motif: a four-point star and a diamond, on a diagonal grid
        out = np.ones((h, w, 3), np.float32) * rgb('#6b4a2e'); img = Image.fromarray((out * 255).astype(np.uint8)); d = ImageDraw.Draw(img); s = max(w, h) // 22
        for j in range(-1, h // s + 2):
            for i in range(-1, w // s + 2):
                cx, cy = i * s + (j % 2) * s // 2, j * s
                if (i + j) % 2 == 0:
                    r = s * 0.28; d.polygon([(cx, cy - r), (cx + r * 0.25, cy - r * 0.25), (cx + r, cy), (cx + r * 0.25, cy + r * 0.25), (cx, cy + r), (cx - r * 0.25, cy + r * 0.25), (cx - r, cy), (cx - r * 0.25, cy - r * 0.25)], fill=(214, 182, 128))
                else:
                    r = s * 0.16; d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], outline=(214, 182, 128), width=max(1, s // 22))
        return np.asarray(img, np.float32) / 255 * (0.94 + 0.06 * rnd.random((h, w, 1)))
    if kind == 'knit':                             # bone-coloured knit with a darker woven stripe, for runners
        rib = 0.9 + 0.1 * np.sin(y * 2 * math.pi / 6) * np.sin(x * 2 * math.pi / 9)
        out = rgb('#d8cfc0')[None, None] * rib[..., None]; band = (np.sin((x * 0.7 + y) * 2 * math.pi / (h / 5)) > 0.75)
        out[band] = out[band] * 0.55 + rgb('#8a8174') * 0.45; return out
    if kind == 'carre':                            # a silk scarf print: medallions, chains and a border, orange, navy and gold
        img = Image.new('RGB', (w, h), (245, 238, 226)); d = ImageDraw.Draw(img); s = max(w, h) // 5
        for j in range(-1, h // s + 2):
            for i in range(-1, w // s + 2):
                cx, cy = i * s + s // 2, j * s + s // 2
                for k, (c, r) in enumerate((((227, 106, 24), 0.44), ((20, 40, 92), 0.36), ((206, 160, 66), 0.28), ((245, 238, 226), 0.2), ((20, 40, 92), 0.12))):
                    d.ellipse([cx - r * s, cy - r * s, cx + r * s, cy + r * s], fill=c)
                for a in range(12):
                    t = a * math.pi / 6; d.ellipse([cx + math.cos(t) * s * 0.4 - s * 0.03, cy + math.sin(t) * s * 0.4 - s * 0.03, cx + math.cos(t) * s * 0.4 + s * 0.03, cy + math.sin(t) * s * 0.4 + s * 0.03], fill=(206, 160, 66))
                d.line([cx + s * 0.5, cy - s * 0.5, cx + s * 0.5, cy + s * 0.5], fill=(20, 40, 92), width=max(2, s // 40))
        return np.asarray(img, np.float32) / 255
    if kind == 'tweed':                            # bouclé tweed: pink, cream and black flecks
        base = rgb('#e8b9c6')[None, None] * np.ones((h, w, 1), np.float32); n = rnd.random((h, w))
        base[n > 0.82] = rgb('#f8f1e8'); base[n < 0.12] = rgb('#2a2328'); base[(n > 0.6) & (n < 0.63)] = rgb('#c9a24a')
        weave = 0.88 + 0.12 * ((np.floor(x / 3) + np.floor(y / 3)) % 2); return np.asarray(Image.fromarray((base * weave[..., None] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255
    if kind == 'leather':                          # black leather with a faint grain
        g = np.asarray(Image.fromarray((rnd.random((h, w)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255
        return rgb('#1c1a1d')[None, None] * (0.85 + 0.3 * g[..., None])
    if kind == 'emerald':                          # emerald silk satin
        return rgb('#0f6b4f')[None, None] * (0.9 + 0.1 * np.sin(x / w * 40)[..., None]) * np.ones((h, w, 1), np.float32)
    if kind == 'quilt':                            # black quilted leather: a diamond stitch grid
        s = max(w, h) / 26; u = (x + y) / s; v = (x - y) / s; seam = (np.abs(u - np.round(u)) < 0.05) | (np.abs(v - np.round(v)) < 0.05)
        puff = 0.75 + 0.25 * np.sin(np.pi * (u - np.floor(u))) * np.sin(np.pi * (v - np.floor(v)))
        out = rgb('#1f1c22')[None, None] * puff[..., None]; out[seam] = rgb('#0c0b0d'); return out
    if kind == 'suede':
        g = np.asarray(Image.fromarray((rnd.random((h, w)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
        return rgb('#a8794a')[None, None] * (0.85 + 0.25 * g[..., None])
    if kind == 'darkmono':                         # a dark shirt with a fine tonal version of the star motif
        return fabric('monogram', w, h) * 0.35 + rgb('#14161c') * 0.65
    if kind in ('butter', 'chocolate', 'cream', 'sage', 'black', 'white', 'cobalt'):     # this season's solids, with a faint woven grain
        c = {'butter': '#f3dc8e', 'chocolate': '#4a2c1e', 'cream': '#efe6d6', 'sage': '#a8b89a', 'black': '#151418', 'white': '#f6f4ef', 'cobalt': '#2747b8'}[kind]
        g = np.asarray(Image.fromarray((rnd.random((h, w)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255
        return rgb(c)[None, None] * (0.94 + 0.08 * g[..., None])
    if kind == 'ankara':                           # a bold wax print of my own: interlocking circles and leaves, magenta, saffron, teal
        img = Image.new('RGB', (w, h), (232, 46, 120)); d = ImageDraw.Draw(img); s = max(w, h) // 9
        for j in range(-1, h // s + 2):
            for i in range(-1, w // s + 2):
                cx, cy = i * s + (j % 2) * s // 2, j * s
                d.ellipse([cx - s * 0.42, cy - s * 0.42, cx + s * 0.42, cy + s * 0.42], fill=(245, 176, 30)); d.ellipse([cx - s * 0.3, cy - s * 0.3, cx + s * 0.3, cy + s * 0.3], fill=(18, 128, 128))
                d.ellipse([cx - s * 0.16, cy - s * 0.16, cx + s * 0.16, cy + s * 0.16], fill=(250, 240, 220))
                for a in range(4):
                    t = a * math.pi / 2 + math.pi / 4; px, py = cx + math.cos(t) * s * 0.55, cy + math.sin(t) * s * 0.55
                    d.ellipse([px - s * 0.08, py - s * 0.16, px + s * 0.08, py + s * 0.16], fill=(20, 24, 60))
        return np.asarray(img, np.float32) / 255
    if kind == 'leopard':
        img = Image.new('RGB', (w, h), (214, 164, 96)); d = ImageDraw.Draw(img); r = random.Random(3); s = max(w, h) / 40
        for _ in range(int(w * h / (s * s) * 1.1)):
            x0, y0 = r.random() * w, r.random() * h; a = s * r.uniform(0.6, 1.1)
            for k in range(5):
                t = k * 1.2566 + r.random() * 0.5; d.ellipse([x0 + math.cos(t) * a - s * 0.22, y0 + math.sin(t) * a * 0.8 - s * 0.18, x0 + math.cos(t) * a + s * 0.22, y0 + math.sin(t) * a * 0.8 + s * 0.18], fill=(38, 26, 20))
            d.ellipse([x0 - a * 0.6, y0 - a * 0.5, x0 + a * 0.6, y0 + a * 0.5], fill=(150, 96, 44))
        return np.asarray(img, np.float32) / 255
    if kind == 'raw_denim':                        # dark indigo denim with a diagonal twill
        tw = 0.88 + 0.12 * (((x + y) % 6) < 3); g = rnd.random((h, w)) * 0.06
        return rgb('#22305a')[None, None] * (tw + g)[..., None]
    if kind == 'pinstripe':                        # charcoal with a fine chalk stripe
        out = rgb('#2b2d33')[None, None] * np.ones((h, w, 1), np.float32); out[(x % 24) < 1.5] = rgb('#bfbcb5'); return out
    raise ValueError(kind)

ITEMS = [   # new name, base garment, fabric, label, shininess
    ('sl_monogram_hoodie', 'elvs_hooded_sweat_jacket1', 'monogram', 'Monogram Canvas Hoodie', 0.15),
    ('sl_knit_runners', 'punkduck_running_shoes_01', 'knit', 'Knit Runners', 0.1),
    ('sl_silk_carre_blouse', 'punkduck_lace_up_blouse', 'carre', 'Silk Carre Blouse', 0.45),
    ('sl_tweed_suit', 'female_elegantsuit01', 'tweed', 'Boucle Tweed Suit', 0.08),
    ('sl_leather_trousers', 'mindfront_female_trousers_1', 'leather', 'Leather Trousers', 0.6),
    ('sl_emerald_gown', 'elvs_halter_dress_long', 'emerald', 'Emerald Satin Gown', 0.7),
    ('sl_quilted_dress', 'toigo_shift_dress', 'quilt', 'Quilted Leather Dress', 0.45),
    ('sl_suede_chelsea', 'toigo_ankle_boots_male', 'suede', 'Suede Chelsea Boots', 0.05),
    ('sl_monogram_shirt', 'elvs_male_shirt_untucked_bd1', 'darkmono', 'Tonal Monogram Shirt', 0.25),
    # the fashion edit: this season's cuts in this season's colours
    ('sl_butter_offshoulder', 'punkduck_off-shoulder_long-sleeve_top', 'butter', 'Butter Satin Off-Shoulder Top', 0.55),
    ('sl_ankara_corset', 'punkduck_tube_top', 'ankara', 'Ankara Bandeau Top', 0.2),
    ('sl_ankara_maxi', 'mindfront_f_dress_05', 'ankara', 'Ankara Maxi Dress', 0.2),
    ('sl_cream_wideleg', 'toigo_wool_pants', 'cream', 'Cream Wide-Leg Trousers', 0.1),
    ('sl_chocolate_cargo', 'cortu_cargo_pants', 'chocolate', 'Chocolate Cargo Pants', 0.1),
    ('sl_sage_halter', 'toigo_halter_dress_midi', 'sage', 'Sage Satin Halter Midi', 0.6),
    ('sl_leopard_cami', 'toigo_camisole_top', 'leopard', 'Leopard Silk Cami', 0.5),
    ('sl_black_keyhole', 'toigo_keyhole_neck_dress', 'black', 'Black Keyhole Dress', 0.35),
    ('sl_raw_denim', 'punkduck_male_classic_jeans', 'raw_denim', 'Raw Indigo Jeans', 0.15),
    ('sl_white_tee', 'toigo_basic_tucked_t-shirt', 'white', 'Heavyweight White Tee', 0.05),
    ('sl_cobalt_tank', 'elvs_male_athletic_tank1', 'cobalt', 'Cobalt Ribbed Tank', 0.05),
    ('sl_pinstripe_suit', 'male_elegantsuit01', 'pinstripe', 'Chalk-Stripe Suit', 0.15),
    ('sl_cream_oxford', 'mindfront_shoes_oxford_male', 'cream', 'Cream Leather Loafers', 0.45),
    ('sl_black_runners', 'punkduck_running_shoes_01', 'black', 'Triple Black Runners', 0.2)]

for new, base, kind, label, shine in ITEMS:
    src, dst = os.path.join(D, base), os.path.join(D, new)
    if os.path.isdir(dst): shutil.rmtree(dst)
    shutil.copytree(src, dst)
    mhclo = [f for f in os.listdir(dst) if f.endswith('.mhclo')][0]; text = open(os.path.join(dst, mhclo)).read()
    mat = re.search(r'^material\s+(\S+)', text, re.M).group(1); mtext = open(os.path.join(dst, mat)).read()
    diff = re.search(r'^diffuseTexture\s+(\S+)', mtext, re.M).group(1)
    try: orig = Image.open(os.path.join(dst, diff))
    except Exception:                               # a texture Pillow cannot read (some library files are DDS or odd PNGs): use a flat sheet
        orig = Image.new('RGB', (1024, 1024), (200, 200, 200))
    w, h = orig.size
    shade = shading(orig)[..., None]; cloth = fabric(kind, w, h)
    out = np.clip(cloth * shade, 0, 1); alpha = np.asarray(orig.convert('RGBA'), np.float32)[..., 3:] / 255
    alpha = np.where(alpha > 0.02, 1.0, alpha).astype(np.float32)        # sheer lace in the old garment becomes solid cloth in the new one
    save(np.concatenate([out, alpha], axis=2) if orig.mode == 'RGBA' else out, os.path.join(dst, new + '_diffuse.png'))
    mtext = re.sub(r'^diffuseTexture\s+\S+', 'diffuseTexture ' + new + '_diffuse.png', mtext, flags=re.M)
    mtext = re.sub(r'^name\s+.*', 'name ' + new, mtext, count=1, flags=re.M)
    mtext = re.sub(r'^shininess\s+\S+', 'shininess %.2f' % shine, mtext, flags=re.M)
    mtext = re.sub(r'^(normalmapTexture|bumpmapTexture|bumpTexture|transparencymapTexture)\s+.*\n', '', mtext, flags=re.M)   # the old garment's surface relief (lace, graphics) does not belong on the new cloth
    mtext = re.sub(r'^transparent\s+\S+', 'transparent False', mtext, flags=re.M); mtext = re.sub(r'^opacity\s+\S+', 'opacity 1.0', mtext, flags=re.M)
    open(os.path.join(dst, new + '.mhmat'), 'w').write(mtext); os.remove(os.path.join(dst, mat)) if mat != new + '.mhmat' else None
    text = re.sub(r'^uuid\s+\S+', 'uuid ' + str(uuid.uuid4()), text, flags=re.M)
    text = re.sub(r'^name\s+.*', 'name ' + new, text, count=1, flags=re.M)
    text = re.sub(r'^material\s+\S+', 'material ' + new + '.mhmat', text, flags=re.M)
    text = re.sub(r'^tag\s+.*\n', '', text, flags=re.M); text = 'tag designer\ntag ' + label + '\n' + text
    open(os.path.join(dst, new + '.mhclo'), 'w').write(text); os.remove(os.path.join(dst, mhclo))
    for f in os.listdir(dst):
        if f.endswith('.thumb'): os.remove(os.path.join(dst, f))
    print('DESIGNER', new, '<-', base, kind, w, 'x', h)
