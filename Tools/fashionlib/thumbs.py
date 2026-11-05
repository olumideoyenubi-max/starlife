# Wardrobe and shop thumbnails from the library's own renders: the grey backdrop keyed out, cropped to the piece, square.
#   python3 Tools/fashionlib/thumbs.py <library> <StarLife>/Assets/Resources/LibThumbs
import json, os, sys
from PIL import Image, ImageFilter

LIB, OUT = sys.argv[1:3]; os.makedirs(OUT, exist_ok=True)
for id, man in sorted(json.load(open(os.path.join(LIB, 'catalog.json'))).items()):
    im = Image.open(os.path.join(LIB, man['tier'], id, 'preview.png')).convert('RGB'); w, h = im.size; px = im.load()
    bg = px[0, 0]; near = lambda c: sum(abs(a - b) for a, b in zip(c, bg)) < 16
    # every backdrop pixel, not only those reached from the corners: the inside of a belt or a strap's loop is backdrop too
    alpha = Image.frombytes('L', (w, h), bytes(0 if near(px[x, y]) else 255 for y in range(h) for x in range(w))).filter(ImageFilter.GaussianBlur(0.7))
    rgba = im.convert('RGBA'); rgba.putalpha(alpha); box = alpha.point(lambda a: 255 if a > 40 else 0).getbbox()
    if man['slot'] == 'ears': box = (box[0], box[1], (box[0] + box[2]) // 2, box[3])     # a pair sits a head's width apart: show one, large
    if man['slot'] == 'ears': x0, y0 = box[:2]; inner = alpha.crop(box).point(lambda a: 255 if a > 40 else 0).getbbox(); box = (x0 + inner[0], y0 + inner[1], x0 + inner[2], y0 + inner[3])
    piece = rgba.crop(box); side = int(max(piece.size) * 1.1)
    sq = Image.new('RGBA', (side, side), bg + (0,)); sq.paste(piece, ((side - piece.width) // 2, (side - piece.height) // 2))
    sq.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, id + '.png')); print('THUMB', id, box)
