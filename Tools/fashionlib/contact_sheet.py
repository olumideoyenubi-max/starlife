# Contact sheets of the fashion library: one row per item (product shot, worn front, worn back), per tier.
#   python3 Tools/fashionlib/contact_sheet.py <library>
import os, sys, json
from PIL import Image, ImageDraw, ImageFont

LIB = sys.argv[1]; T = 300
try: font = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', 15)
except Exception: font = ImageFont.load_default()
for tier in ('luxury', 'mid'):
    root = os.path.join(LIB, tier)
    if not os.path.isdir(root): continue
    ids = sorted(d for d in os.listdir(root) if os.path.exists(os.path.join(root, d, 'manifest.json')))
    cols = 2; rows = (len(ids) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * 3 * T, rows * (T + 24)), (245, 244, 240)); d = ImageDraw.Draw(sheet)
    for i, id in enumerate(ids):
        man = json.load(open(os.path.join(root, id, 'manifest.json'))); x0 = (i % cols) * 3 * T; y0 = (i // cols) * (T + 24)
        for k, f in enumerate(['preview.png', 'onbody.png', 'onbody_back.png']):
            p = os.path.join(root, id, f)
            if os.path.exists(p): sheet.paste(Image.open(p).convert('RGB').resize((T, T)), (x0 + k * T, y0 + 24))
        d.text((x0 + 6, y0 + 4), f"{id}  -  {man['name']}  -  LOD0 {man['triangles']['LOD0']:,} tris", fill=(30, 30, 30), font=font)
    sheet.save(os.path.join(LIB, f'contact_{tier}.jpg'), quality=88); print('sheet', tier, len(ids))
