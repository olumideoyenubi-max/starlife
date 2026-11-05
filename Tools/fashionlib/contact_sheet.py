# Contact sheets of the fashion library: one row per item (cinematic, game LOD0 with its baked maps, worn front and back,
# daylight, night), per tier.
#   python3 Tools/fashionlib/contact_sheet.py <library>
import os, sys, json
from PIL import Image, ImageDraw, ImageFont

LIB = sys.argv[1]; T = 240; SHOTS = ['preview.png', 'game.png', 'onbody.png', 'onbody_back.png', 'day.png', 'night.png']
try: font = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', 15)
except Exception: font = ImageFont.load_default()
for tier in ('luxury', 'mid'):
    root = os.path.join(LIB, tier)
    if not os.path.isdir(root): continue
    ids = sorted(d for d in os.listdir(root) if os.path.exists(os.path.join(root, d, 'manifest.json')))
    cols = 1; rows = len(ids); W = len(SHOTS) * T
    sheet = Image.new('RGB', (W, rows * (T + 24)), (245, 244, 240)); d = ImageDraw.Draw(sheet)
    for i, id in enumerate(ids):
        man = json.load(open(os.path.join(root, id, 'manifest.json'))); x0 = 0; y0 = i * (T + 24)
        for k, f in enumerate(SHOTS):
            p = os.path.join(root, id, f)
            if os.path.exists(p): sheet.paste(Image.open(p).convert('RGB').resize((T, T)), (x0 + k * T, y0 + 24))
        d.text((x0 + 6, y0 + 4), f"{id}  -  {man['name']}  -  LOD0 {man['triangles']['LOD0']:,} / LOD1 {man['triangles']['LOD1']:,} / LOD2 {man['triangles']['LOD2']:,} tris, maps {man.get('map_size', '-')} px   (cinematic | game | worn | back | day | night)", fill=(30, 30, 30), font=font)
    sheet.save(os.path.join(LIB, f'contact_{tier}.jpg'), quality=88); print('sheet', tier, len(ids))
