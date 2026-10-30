# The Starphone Duo's app icons and wallpaper (Assets/Resources/Phone), drawn flat at 4x and scaled down.
import os, math
from PIL import Image, ImageDraw
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Phone')
S = 4; N = 128
def hexc(h): h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)
def grad(a, b, n):
    im = Image.new('RGBA', (n, n)); d = ImageDraw.Draw(im); ca, cb = hexc(a), hexc(b)
    for y in range(n): t = y / (n - 1); d.line([(0, y), (n, y)], fill=tuple(int(ca[i] + (cb[i] - ca[i]) * t) for i in range(4)))
    return im
W = (255, 255, 255, 255)
def icon(name, a, b, draw):
    n = N * S; im = grad(a, b, n); d = ImageDraw.Draw(im); draw(d, n)
    mask = Image.new('L', (n, n), 0); ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=int(n * 0.23), fill=255)
    out = Image.new('RGBA', (n, n), (0, 0, 0, 0)); out.paste(im, (0, 0), mask); out.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, name + '.png'))
def maps(d, n):
    d.polygon([(n * .2, n * .3), (n * .4, n * .22), (n * .6, n * .3), (n * .8, n * .22), (n * .8, n * .72), (n * .6, n * .8), (n * .4, n * .72), (n * .2, n * .8)], fill=(255, 255, 255, 235))
    d.line([(n * .4, n * .22), (n * .4, n * .72)], fill=hexc('#2f9e6b'), width=n // 40); d.line([(n * .6, n * .3), (n * .6, n * .8)], fill=hexc('#2f9e6b'), width=n // 40)
    c = (n * .62, n * .42); d.ellipse([c[0] - n * .12, c[1] - n * .12, c[0] + n * .12, c[1] + n * .12], fill=hexc('#ff4f6b')); d.polygon([(c[0] - n * .1, c[1] + n * .05), (c[0] + n * .1, c[1] + n * .05), (c[0], c[1] + n * .24)], fill=hexc('#ff4f6b'))
    d.ellipse([c[0] - n * .045, c[1] - n * .045, c[0] + n * .045, c[1] + n * .045], fill=W)
def places(d, n):
    for i in range(3):
        y = n * (.3 + i * .2); d.ellipse([n * .22, y - n * .05, n * .32, y + n * .05], fill=W); d.rounded_rectangle([n * .38, y - n * .035, n * .78, y + n * .035], radius=n * .03, fill=W)
def friends(d, n):
    for (x, s) in ((.38, 1), (.62, .85)):
        d.ellipse([n * (x - .1 * s), n * (.28), n * (x + .1 * s), n * (.28 + .2 * s)], fill=W); d.pieslice([n * (x - .18 * s), n * .52, n * (x + .18 * s), n * (.52 + .36 * s)], 180, 360, fill=W)
def settings(d, n):
    c = n / 2
    for i in range(8): a = i * math.pi / 4; d.line([(c + math.cos(a) * n * .2, c + math.sin(a) * n * .2), (c + math.cos(a) * n * .33, c + math.sin(a) * n * .33)], fill=W, width=n // 9)
    d.ellipse([c - n * .24, c - n * .24, c + n * .24, c + n * .24], fill=W); d.ellipse([c - n * .1, c - n * .1, c + n * .1, c + n * .1], fill=hexc('#8e8e93'))
def camera(d, n):
    d.rounded_rectangle([n * .18, n * .32, n * .82, n * .74], radius=n * .08, fill=W); d.rounded_rectangle([n * .38, n * .25, n * .62, n * .36], radius=n * .04, fill=W)
    d.ellipse([n * .37, n * .39, n * .63, n * .65], fill=hexc('#3a3a3c')); d.ellipse([n * .43, n * .45, n * .57, n * .59], fill=hexc('#6e6e73'))
def photos(d, n):
    c = n / 2; cols = ('#ff9f0a', '#ffd60a', '#30d158', '#64d2ff', '#0a84ff', '#bf5af2', '#ff375f', '#ff6b35')
    for i, col in enumerate(cols):
        a = i * math.pi / 4; x = c + math.cos(a) * n * .17; y = c + math.sin(a) * n * .17; k = hexc(col)[:3] + (215,)
        d.ellipse([x - n * .13, y - n * .13, x + n * .13, y + n * .13], fill=k)
def wallet(d, n):
    for i, col in enumerate(('#ff9f0a', '#30d158', '#0a84ff')): d.rounded_rectangle([n * .2, n * (.26 + i * .1), n * .8, n * (.5 + i * .1)], radius=n * .05, fill=hexc(col))
    d.rounded_rectangle([n * .16, n * .5, n * .84, n * .78], radius=n * .07, fill=W); d.ellipse([n * .62, n * .58, n * .74, n * .7], fill=hexc('#1c1c1e'))
def emotes(d, n):
    c = n / 2; d.ellipse([c - n * .3, c - n * .3, c + n * .3, c + n * .3], fill=hexc('#ffd60a'))
    for x in (-.1, .1): d.ellipse([c + n * x - n * .04, c - n * .12, c + n * x + n * .04, c - n * .02], fill=hexc('#3a2a00'))
    d.arc([c - n * .17, c - n * .12, c + n * .17, c + n * .18], 20, 160, fill=hexc('#3a2a00'), width=n // 22)
def clock(d, n):
    c = n / 2; d.ellipse([c - n * .32, c - n * .32, c + n * .32, c + n * .32], fill=W)
    for i in range(12): a = i * math.pi / 6; d.line([(c + math.cos(a) * n * .25, c + math.sin(a) * n * .25), (c + math.cos(a) * n * .29, c + math.sin(a) * n * .29)], fill=hexc('#1c1c1e'), width=n // 60)
    d.line([(c, c), (c + n * .0, c - n * .2)], fill=hexc('#1c1c1e'), width=n // 30); d.line([(c, c), (c + n * .14, c + n * .05)], fill=hexc('#1c1c1e'), width=n // 30); d.line([(c, c), (c - n * .1, c + n * .18)], fill=hexc('#ff9f0a'), width=n // 70)
def music(d, n):
    d.ellipse([n * .26, n * .6, n * .42, n * .74], fill=W); d.ellipse([n * .56, n * .54, n * .72, n * .68], fill=W)
    d.line([(n * .4, n * .66), (n * .4, n * .3)], fill=W, width=n // 22); d.line([(n * .7, n * .6), (n * .7, n * .24)], fill=W, width=n // 22); d.polygon([(n * .38, n * .28), (n * .72, n * .2), (n * .72, n * .3), (n * .38, n * .38)], fill=W)
for args in (('maps', '#34c759', '#248a3d', maps), ('places', '#ff9f0a', '#ff6a00', places), ('friends', '#ff375f', '#d70f5f', friends), ('settings', '#9a9aa0', '#636366', settings),
             ('camera', '#5a5a60', '#2c2c2e', camera), ('photos', '#ffffff', '#f2f2f7', photos), ('wallet', '#1c1c1e', '#000000', wallet), ('emotes', '#ff9f0a', '#ff6a00', emotes),
             ('clock', '#1c1c1e', '#000000', clock), ('music', '#ff2d55', '#d70040', music)):
    icon(*args)
# the wallpaper: a soft dusk gradient with blurred light
from PIL import ImageFilter
w, h = 360, 760; wp = Image.new('RGB', (w, h)); d = ImageDraw.Draw(wp)
for y in range(h):
    t = y / h; a, b = (40, 22, 74), (240, 120, 150); d.line([(0, y), (w, y)], fill=tuple(int(a[i] + (b[i] - a[i]) * t ** 1.4) for i in range(3)))
glow = Image.new('RGBA', (w, h), (0, 0, 0, 0)); g = ImageDraw.Draw(glow)
for (x, y, r, col) in ((80, 520, 160, (255, 160, 120, 140)), (300, 300, 130, (150, 110, 255, 120)), (200, 700, 200, (255, 90, 140, 150))): g.ellipse([x - r, y - r, x + r, y + r], fill=col)
wp = Image.alpha_composite(wp.convert('RGBA'), glow.filter(ImageFilter.GaussianBlur(60))); wp.save(os.path.join(OUT, 'wallpaper.png'))
print('icons done')
