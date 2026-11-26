# Astro Blitz - App Store screenshots, in English, same 8 shots and style as
# compose_screenshots.py (the Google Play set), drawn at the exact App Store Connect
# sizes, one folder per display class (SETS below):
#   AppStore/iphone_duo/screenshots/{landscape_2034x1398,portrait_1398x2034,landscape_2853x2007,portrait_2007x2853}/
#   AppStore/iphone_dynamic_island_medium/screenshots/portrait_1206x2622/   (required for iPhone)
#   AppStore/ipad_13/screenshots/portrait_2064x2752/                         (required when the app runs on iPad)
#
# These canvases are not 16:9, so the layouts are redone here. Every size is
# written in "design units" (the short side = 1080) and multiplied by
# k = short side / 1080, so text, frames and the gameplay are drawn at full
# resolution instead of being upscaled. A 19.5:9 portrait page ("tall") gets a
# bigger title, a device no wider than 800 units, the combo chips under the text
# and the ship cards in a cross. 24-bit PNG, no alpha.
#
# Run: python Tools/trailer/compose_appstore_duo.py [--set NAME ...] [shot prefix ...]
#      no --set = every set; e.g. --set ipad_13 03_ redoes one shot of one set.

import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import compose_trailer as ct  # noqa: E402
import compose_screenshots as cs  # noqa: E402

# App Store Connect "Screenshot specifications" (checked 12/11/2026).
SETS = {
    # "iPhone Duo", outer and inner display; required from April 2027 for apps built with the iOS 27.1 SDK
    'iphone_duo': [(2034, 1398), (1398, 2034), (2853, 2007), (2007, 2853)],
    # "iPhone with Dynamic Island (medium display)", required for iPhone; Apple also takes 1179 x 2556
    'iphone_dynamic_island_medium': [(1206, 2622)],
    # "iPad 13-inch display", required if the app runs on iPad; Apple also takes 2048 x 2732
    'ipad_13': [(2064, 2752)],
}
OUT = os.path.join(ct.ROOT, 'AppStore', 'iphone_duo', 'screenshots')  # main() points it at each set
CYAN, RED, WHITE = ct.CYAN, ct.RED, ct.WHITE


class Page:
    def __init__(self, size, src, color, seed):
        self.w, self.h = size
        self.k = min(size) / 1080.0
        self.dw, self.dh = self.w / self.k, self.h / self.k  # canvas in design units
        self.canvas = background(size, src, color, seed, self.k)

    def s(self, v):
        return v * self.k

    def p(self, x, y):
        return x * self.k, y * self.k


def background(size, src, accent, seed, k):
    sw, sh = size
    rnd = random.Random(seed)
    scale = max(sw / src.width, sh / src.height)
    amb = src.resize((int(src.width * scale) + 1, int(src.height * scale) + 1), Image.BILINEAR)
    left, top = (amb.width - sw) // 2, (amb.height - sh) // 2
    amb = amb.crop((left, top, left + sw, top + sh)).filter(ImageFilter.GaussianBlur(28 * k))
    img = Image.blend(Image.new('RGB', (sw, sh), (5, 8, 22)), amb, 0.28)
    # nebulae drawn small and blurred, then scaled up (a 150*k blur at full size is slow)
    q = 4
    neb = Image.new('RGB', (sw // q, sh // q))
    d = ImageDraw.Draw(neb)
    for _ in range(4):
        x, y, r = rnd.uniform(0, sw), rnd.uniform(0, sh), rnd.uniform(260, 520) * k
        c = rnd.choice([accent, (70, 26, 125), (10, 70, 105)])
        d.ellipse(((x - r) / q, (y - r) / q, (x + r) / q, (y + r) / q), fill=tuple(int(v * 0.45) for v in c))
    neb = neb.filter(ImageFilter.GaussianBlur(150 * k / q)).resize((sw, sh), Image.BILINEAR)
    from PIL import ImageChops
    img = ImageChops.add(img, neb)
    d = ImageDraw.Draw(img)
    for _ in range(int(sw * sh / (8000 * k * k))):
        x, y = rnd.uniform(0, sw), rnd.uniform(0, sh)
        r = rnd.choice([1, 1, 1, 2, 2, 3]) * k
        v = rnd.randint(110, 255)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(v, v, min(255, v + 20)))
    import numpy as np
    yy, xx = [a.astype('float32') for a in np.mgrid[0:sh // 4, 0:sw // 4]]
    nx, ny = (xx / (sw / 4) - 0.5) * 2, (yy / (sh / 4) - 0.5) * 2
    v = np.clip(1 - (nx ** 2 * 0.35 + ny ** 2 * 0.45), 0.35, 1)
    vig = Image.fromarray((v * 255).astype('uint8')).resize((sw, sh), Image.BILINEAR)
    return Image.composite(img, Image.new('RGB', (sw, sh), (2, 4, 12)), vig).convert('RGBA')


def layer_of(pg):
    return Image.new('RGBA', pg.canvas.size, (0, 0, 0, 0))


def speed_lines(pg, cx, cy, color, seed, count=60, alpha=90, r0=(380, 560)):
    rnd = random.Random(seed)
    layer = layer_of(pg)
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        ang = rnd.uniform(0, 2 * math.pi)
        a = rnd.uniform(*r0)
        b = a + rnd.uniform(200, 700)
        d.line([pg.p(cx + math.cos(ang) * a, cy + math.sin(ang) * a), pg.p(cx + math.cos(ang) * b, cy + math.sin(ang) * b)],
               fill=color + (rnd.randint(alpha // 3, alpha),), width=max(1, round(rnd.choice([2, 2, 3, 4]) * pg.k)))
    pg.canvas.alpha_composite(layer)


def rings(pg, cx, cy, color, radii=(520, 600)):
    layer = layer_of(pg)
    d = ImageDraw.Draw(layer)
    for n, r in enumerate(radii):
        x0, y0 = pg.p(cx - r, cy - r)
        x1, y1 = pg.p(cx + r, cy + r)
        for a0 in range(0, 360, 40):
            d.arc((x0, y0, x1, y1), a0 + n * 12, a0 + 24 + n * 12, fill=color + (120 - 40 * n,),
                  width=max(1, round((4 - n) * pg.k)))
    pg.canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(pg.k)))


def glow_card(img, radius, color, pad, blur, outline, k):
    """Rounded image with a coloured glow and outline, on a padded transparent layer."""
    w, h = img.size
    img = img.convert('RGBA')
    img.putalpha(ct.round_mask(w, h, radius))
    layer = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', layer.size, 0)
    ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + w, pad + h), radius, fill=230)
    glow = Image.new('RGBA', layer.size, color + (0,))
    glow.putalpha(g.filter(ImageFilter.GaussianBlur(blur)))
    layer.alpha_composite(glow)
    layer.alpha_composite(img, (pad, pad))
    o = int(round(outline * 0.8))
    ImageDraw.Draw(layer).rounded_rectangle((pad - o, pad - o, pad + w + o, pad + h + o), radius + o,
                                            outline=color + (255,), width=outline)
    return layer


def device(pg, src, cx, cy, height, tilt, color, crop=None):
    crop = crop or 1920
    shot = ct.juicy(src, 0.6, 1.2, 1.05).crop((0, 0, 1080, crop))
    h = int(pg.s(height))
    w = int(1080 * h / crop)
    shot = shot.resize((w, h), Image.LANCZOS)
    layer = glow_card(shot, int(h * 0.035), color, int(pg.s(80)), pg.s(38), max(2, round(pg.s(5))), pg.k)
    if tilt:
        layer = layer.rotate(tilt, Image.BICUBIC, expand=True)
    x, y = pg.p(cx, cy)
    pg.canvas.alpha_composite(layer, (int(x - layer.width / 2), int(y - layer.height / 2)))
    return w / pg.k, h / pg.k


def inset(pg, src, sx, sy, cx, cy, radius, zoom, color, anchor):
    r = int(pg.s(radius))
    crop_r = radius / zoom
    piece = ct.juicy(src, 0.9, 1.25, 1.06).crop((int(sx - crop_r), int(sy - crop_r), int(sx + crop_r), int(sy + crop_r)))
    piece = piece.resize((r * 2, r * 2), Image.LANCZOS).convert('RGBA')
    mask = Image.new('L', (r * 4, r * 4), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, r * 4 - 1, r * 4 - 1), fill=255)
    piece.putalpha(mask.resize((r * 2, r * 2), Image.LANCZOS))
    layer = layer_of(pg)
    d = ImageDraw.Draw(layer)
    ax, ay = pg.p(*anchor)
    px, py = pg.p(cx, cy)
    d.line([(ax, ay), (px, py)], fill=color + (200,), width=max(1, round(pg.s(3))))
    e = pg.s(14)
    d.ellipse((ax - e, ay - e, ax + e, ay + e), outline=color + (255,), width=max(1, round(pg.s(4))))
    pg.canvas.alpha_composite(layer)
    pad = int(pg.s(50))
    holder = Image.new('RGBA', (r * 2 + pad * 2, r * 2 + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', holder.size, 0)
    ImageDraw.Draw(g).ellipse((pad, pad, pad + r * 2, pad + r * 2), fill=220)
    glow = Image.new('RGBA', holder.size, color + (0,))
    glow.putalpha(g.filter(ImageFilter.GaussianBlur(pg.s(24))))
    holder.alpha_composite(glow)
    holder.alpha_composite(piece, (pad, pad))
    d = ImageDraw.Draw(holder)
    o, t = pg.s(3), pg.s(18)
    d.ellipse((pad - o, pad - o, pad + r * 2 + o, pad + r * 2 + o), outline=color + (255,), width=max(2, round(pg.s(6))))
    for a0 in range(0, 360, 90):
        d.arc((pad - t, pad - t, pad + r * 2 + t, pad + r * 2 + t), a0 + 20, a0 + 70, fill=color + (200,),
              width=max(1, round(pg.s(3))))
    pg.canvas.alpha_composite(holder, (int(px - holder.width / 2), int(py - holder.height / 2)))


def sparks(pg, cx, cy, color, seed, count=18, reach=300):
    rnd = random.Random(seed)
    layer = layer_of(pg)
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        ang = rnd.uniform(0, 2 * math.pi)
        r0 = rnd.uniform(300, 420)
        r1 = r0 + reach * rnd.uniform(0.2, 0.6)
        d.line([pg.p(cx + math.cos(ang) * r0, cy + math.sin(ang) * r0 * 0.4),
                pg.p(cx + math.cos(ang) * r1, cy + math.sin(ang) * r1 * 0.4)],
               fill=color + (rnd.randint(120, 230),), width=max(1, round(rnd.choice([2, 3, 4]) * pg.k)))
    pg.canvas.alpha_composite(layer)


def hud(pg, color=CYAN):
    sw, sh = pg.canvas.size
    layer = layer_of(pg)
    d = ImageDraw.Draw(layer)
    m, L = pg.s(30), pg.s(110)
    for x, y, sx, sy in ((m, m, 1, 1), (sw - m, m, -1, 1), (m, sh - m, 1, -1), (sw - m, sh - m, -1, -1)):
        d.line([(x, y + sy * L), (x, y), (x + sx * L, y)], fill=color + (180,), width=max(2, round(pg.s(4))))
        d.line([(x + sx * pg.s(16), y + sy * pg.s(16)), (x + sx * pg.s(46), y + sy * pg.s(16))], fill=color + (110,),
               width=max(1, round(pg.s(2))))
    step = max(4, int(round(pg.s(4))))
    for y in range(0, sh, step):
        d.line([(0, y), (sw, y)], fill=(0, 0, 0, 14), width=max(1, round(pg.k)))
    pg.canvas.alpha_composite(layer)


def text_sprite(pg, text, size, color, glow, face, em, stroke, glow_alpha, max_w):
    return ct.txt(text, int(pg.s(size)), color, glow, face, em, max(1, round(stroke * pg.k)), glow_alpha, None,
                  int(pg.s(max_w)))


def put(pg, sprite, cx, cy):
    ct.put(pg.canvas, sprite, *pg.p(cx, cy))


def text_block(pg, cx, y0, label, title, accent, accent_color, body, color, title_size=168, max_w=860):
    lc = RED if label.startswith('!') else color
    put(pg, text_sprite(pg, label, 36, lc, lc, ct.SEMI, 0.3, 2, 0.45, max_w + 140), cx, y0)
    spr = text_sprite(pg, title, title_size, WHITE, color, ct.BOLD, 0.14, 4, 0.85, max_w)
    tw = spr.width / pg.k
    ty = y0 + title_size * 0.9
    ct.title_frame(pg.canvas, 1.0, 0, 99, *pg.p(cx, ty), pg.s(tw - title_size * 0.45), pg.s(title_size * 1.3), color)
    put(pg, spr, cx, y0 + title_size * 0.87)
    sparks(pg, cx, y0 + title_size * 0.87, color, len(title))
    a_size = title_size * 0.45
    line = text_sprite(pg, accent, a_size, accent_color, accent_color, ct.BOLD, 0.2, 3, 0.7, max_w)
    ay = y0 + title_size * 1.97
    ct.plate(pg.canvas, 1.0, 0, 99, *pg.p(cx, ay), pg.s(line.width / pg.k - a_size * 0.55), pg.s(a_size * 1.15),
             accent_color)
    put(pg, line, cx, ay)
    y = ay + a_size * 1.1
    if body:
        put(pg, text_sprite(pg, body, 40, (200, 220, 238), None, ct.SEMI, 0.04, 0, 0, max_w - 60), cx, y + 30)
        y += 70
    return y


def brand(pg, cx, cy, icon=120, gap=30):
    """Icon + name + FREE TO PLAY, centred as a group. The name's glyphs are measured (the sprite
    carries glow padding), so a longer name keeps the same gap to the icon instead of touching it."""
    name = text_sprite(pg, 'ASTRO BLITZ', 64, WHITE, CYAN, ct.BOLD, 0.14, 3, 0.8, 960)
    box = name.getchannel('A').point(lambda v: 255 if v > 200 else 0).getbbox()
    left, right = (box[0] - name.width / 2) / pg.k, (box[2] - name.width / 2) / pg.k
    x0 = cx - (icon + gap + right - left) / 2
    name_x = x0 + icon + gap - left
    put(pg, ct.app_icon(int(pg.s(icon))), x0 + icon / 2, cy)
    put(pg, name, name_x, cy - 18)
    put(pg, text_sprite(pg, 'FREE TO PLAY', 30, (150, 190, 215), None, ct.SEMI, 0.3, 0, 0, 960), name_x, cy + 34)


def chip(pg, text, accent, scale=1.0):
    """ct.chip_sprite drawn at the page's scale."""
    k = pg.k * scale
    spr = ct.txt(text, int(44 * k), WHITE, accent, ct.BOLD, 0.14, max(1, round(2 * k)), 0.5, None, int(960 * k))
    w, h = int(spr.width - 10 * k + 70 * k), int(88 * k)
    m = int(20 * k)
    img = Image.new('RGBA', (w + m * 2, h + m * 2), (0, 0, 0, 0))
    glow = Image.new('L', img.size, 0)
    c = m
    poly = [(m + c, m), (m + w, m), (m + w, m + h - c), (m + w - c, m + h), (m, m + h), (m, m + c)]
    ImageDraw.Draw(glow).polygon(poly, fill=150)
    gl = Image.new('RGBA', img.size, accent + (0,))
    gl.putalpha(glow.filter(ImageFilter.GaussianBlur(12 * k)))
    img = Image.alpha_composite(img, gl)
    d = ImageDraw.Draw(img)
    d.polygon(poly, fill=(6, 12, 30, 235))
    d.line(poly + [poly[0]], fill=accent + (255,), width=max(2, round(3 * k)))
    b = 12 * k
    d.polygon([(m, m + c), (m + c, m), (m + c + b, m), (m + b, m + c + b), (m + b, m + h), (m, m + h)], fill=accent + (255,))
    img.alpha_composite(spr, (int(m + 42 * k), int(m + h / 2 - spr.height / 2)))
    return img


def chips_grid(pg, cx, y, spacing_x=450, spacing_y=108, scale=1.0):
    for n, (name, c) in enumerate(ct.COMBO_CHIPS):
        col, row = n % 2, n // 2
        put(pg, chip(pg, name, c, scale), cx - spacing_x / 2 + col * spacing_x, y + row * spacing_y)


def chips_column(pg, cx, y, spacing_y=118):
    for n, (name, c) in enumerate(ct.COMBO_CHIPS):
        put(pg, chip(pg, name, c), cx, y + n * spacing_y)


def ship_fan(pg, cx0, cy0, spacing, centre_w, side_w):
    for slot in (0, 4, 1, 3, 2):  # outer cards first, the middle one last
        idx, accent = cs.HANGAR_CARDS[slot]
        img = cs.frame('06_hangar', idx).crop((176, 330, 904, 1560))
        img = ImageEnhance.Contrast(ImageEnhance.Color(img).enhance(1.2)).enhance(1.05)
        w = int(pg.s(centre_w if slot == 2 else side_w))
        h = int(w * img.height / img.width)
        layer = glow_card(img.resize((w, h), Image.LANCZOS), int(pg.s(22)), accent, int(pg.s(60)), pg.s(26),
                          max(2, round(pg.s(4))), pg.k)
        off = slot - 2
        layer = layer.rotate(-off * 7, Image.BICUBIC, expand=True)
        x, y = pg.p(cx0 + off * spacing, cy0 + abs(off) * 40)
        pg.canvas.alpha_composite(layer, (int(x - layer.width / 2), int(y - layer.height / 2)))


def ship_cross(pg, cx, cy, centre_w=440, side_w=340, dx=240, dy=430):
    """The ship cards for a tall page, where a fan would stay small: four in the corners,
    tilted away from the middle, and the middle one on top."""
    places = {0: (-1, -1), 4: (1, -1), 1: (-1, 1), 3: (1, 1), 2: (0, 0)}
    for slot in (0, 4, 1, 3, 2):
        idx, accent = cs.HANGAR_CARDS[slot]
        img = cs.frame('06_hangar', idx).crop((176, 330, 904, 1560))
        img = ImageEnhance.Contrast(ImageEnhance.Color(img).enhance(1.2)).enhance(1.05)
        w = int(pg.s(centre_w if slot == 2 else side_w))
        h = int(w * img.height / img.width)
        layer = glow_card(img.resize((w, h), Image.LANCZOS), int(pg.s(22)), accent, int(pg.s(60)), pg.s(26),
                          max(2, round(pg.s(4))), pg.k)
        sx, sy = places[slot]
        if sx:
            layer = layer.rotate(sx * sy * 6, Image.BICUBIC, expand=True)
        x, y = pg.p(cx + sx * dx, cy + sy * dy)
        pg.canvas.alpha_composite(layer, (int(x - layer.width / 2), int(y - layer.height / 2)))


def save(pg, name):
    folder = '%s_%dx%d' % ('landscape' if pg.w > pg.h else 'portrait', pg.w, pg.h)
    path = os.path.join(OUT, folder, name + '.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pg.canvas.convert('RGB').save(path, optimize=True)
    return path


# ----------------------------------------------------------------------
# Layouts (design units: the short side is 1080)
# ----------------------------------------------------------------------

def landscape(i, spec, size):
    name, shot, index, label, title, accent, accent_color, body, color, focus, crop = spec
    src = cs.frame(shot, index) if shot else cs.frame('06_hangar', 83)
    pg = Page(size, src, color, i + 1)
    W = pg.dw  # about 1520-1570
    right = i % 2 == 0
    dev_x = W - 400 if right else 400
    txt_x = (W - 760) / 2 + 10 if right else W - (W - 760) / 2 - 10
    max_w = 740
    if shot is None:  # the hangar: a fan of the ship cards
        txt_x = 380
        fan_x = W - 420
        speed_lines(pg, fan_x, 540, color, i, 40, 60)
        ship_fan(pg, fan_x, 560, 135, 290, 210)
    else:
        speed_lines(pg, dev_x, 540, color, i)
        rings(pg, dev_x, 540, color, (500, 580))
        device(pg, src, dev_x, 540, 870, -4 if right else 4, color, crop)
        if focus:
            fx, fy = focus
            inset(pg, src, fx, fy, dev_x + (-235 if right else 235), 850, 115, 1.9, color,
                  (dev_x + (fx - 540) * 0.453, 540 + (fy - 960) * 0.453))
    y = text_block(pg, txt_x, 230, label, title, accent, accent_color, body, color, 148, max_w)
    if name.startswith('03_'):
        chips_grid(pg, txt_x, y + 50, 375, 90, 0.82)
        brand(pg, txt_x, 990)
    else:
        brand(pg, txt_x, max(y + 110, 930))
    hud(pg, color)
    return save(pg, name)


def portrait(i, spec, size):
    name, shot, index, label, title, accent, accent_color, body, color, focus, crop = spec
    src = cs.frame(shot, index) if shot else cs.frame('06_hangar', 83)
    pg = Page(size, src, color, i + 11)
    H = pg.dh  # 1440 (iPad 13"), about 1520-1570 (iPhone Duo), 2348 (19.5:9 iPhone)
    tall = H > 1900  # 19.5:9: the device would fill the width, so it is capped and the rest rearranged
    y = text_block(pg, 540, 110 if tall else 95, label, title, accent, accent_color, body, color,
                   150 if tall else 128, 920)
    brand_y = H - (110 if tall else 85)
    top, bottom = y + 40, brand_y - 85  # room for the device
    chips_y = None
    if tall and name.startswith('03_'):  # the combo chips go in a 2 x 2 grid under the text
        chips_y, top = top + 55, top + 290
    dev_h = bottom - top
    dev_cy = (top + bottom) / 2
    if tall:
        dev_h = min(dev_h, 800 * (crop or 1920) / 1080.0)  # at most 800 of the 1080 units wide
    if shot is None:
        speed_lines(pg, 540, dev_cy, color, i, 40, 60)
        if tall:
            ship_cross(pg, 540, dev_cy)
        else:
            ship_fan(pg, 540, dev_cy - 40, 165, 300, 230)
    elif name.startswith('03_') and not tall:  # device on the left, the combo chips beside it
        dev_x = 300
        speed_lines(pg, dev_x, dev_cy, color, i, 60, 80, (380, 520))
        rings(pg, dev_x, dev_cy, color, (440, 500))
        device(pg, src, dev_x, dev_cy, dev_h, -3, color, crop)
        chips_column(pg, 790, dev_cy - 177, 118)
    else:
        speed_lines(pg, 540, dev_cy, color, i, 70, 90, (420, 600))
        rings(pg, 540, dev_cy, color, (480, 550))
        dw, _ = device(pg, src, 540, dev_cy, dev_h, 3 if i % 2 else -3, color, crop)
        if focus:
            fx, fy = focus
            scale = dev_h / 1920
            side = 1 if i % 2 == 0 else -1
            inset(pg, src, fx, fy, 540 + side * 345, dev_cy + dev_h * 0.22, 120, 1.9, color,
                  (540 + (fx - 540) * scale, dev_cy + (fy - 960) * scale))
        if chips_y is not None:
            chips_grid(pg, 540, chips_y, 470, 112, 0.92)
    brand(pg, 540, brand_y)
    hud(pg, color)
    return save(pg, name)


def main():
    global OUT
    import argparse
    ap = argparse.ArgumentParser(description='App Store screenshots at the App Store Connect sizes.')
    ap.add_argument('--set', dest='sets', action='append', choices=sorted(SETS),
                    help='display class to draw (repeatable); default: every set')
    ap.add_argument('only', nargs='*', help='shot name prefixes to redo, e.g. 03_')
    args = ap.parse_args()
    for set_name in args.sets or list(SETS):
        OUT = os.path.join(ct.ROOT, 'AppStore', set_name, 'screenshots')
        for size in SETS[set_name]:
            for i, spec in enumerate(cs.SHOTS):
                if args.only and not any(spec[0].startswith(o) for o in args.only):
                    continue
                path = landscape(i, spec, size) if size[0] > size[1] else portrait(i, spec, size)
                print('wrote', os.path.relpath(path, ct.ROOT), flush=True)


if __name__ == '__main__':
    main()
