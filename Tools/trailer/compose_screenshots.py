# Astro Blitz - 8 store screenshots in two formats, English, in the trailer's sci-fi style:
#   PlayConsole/screenshots/landscape_1920x1080/   (16:9, what Google Play asks for to feature games)
#   PlayConsole/screenshots/portrait_1080x1920/    (9:16, phone listing)
#
# Uses the frames recorded for the trailer (Trailer/raw) and the helpers of
# compose_trailer.py (logo-style Rajdhani text, sci-fi frame, plates, chips, bloom).
# 24-bit PNG, no alpha.
#
# Run: python Tools/trailer/compose_screenshots.py

import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import compose_trailer as ct  # noqa: E402

OUT = os.path.join(ct.ROOT, 'PlayConsole', 'screenshots')
CYAN, GOLD, ORANGE, RED, PURPLE, WHITE = ct.CYAN, ct.GOLD, ct.ORANGE, ct.RED, ct.PURPLE, ct.WHITE
LILAC = (215, 175, 255)
SOFT_ORANGE = (255, 170, 110)

# name, shot, frame, label, title, accent line, accent colour, body, theme colour,
# magnifier focus in the shot (None = no magnifier), UI crop (rows above the editor's test banner) or None
SHOTS = [
    ('01_defend_the_galaxy', '02_gameplay', 270, '// AUTO-AIM  ·  AUTO-FIRE', 'DEFEND', 'THE GALAXY', CYAN,
     'Pilot a lone starship against endless alien waves.', CYAN, (540, 950), None),
    ('02_level_up', '03_levelup', 60, '// SHIP LEVEL UP', 'LEVEL UP', 'PICK 1 OF 3 UPGRADES', GOLD,
     'Every level brings a new choice. Build your run.', CYAN, None, None),
    ('03_stack_insane_combos', '02b_gameplay', 130, '// LOADOUT', 'STACK', 'INSANE COMBOS', LILAC,
     None, PURPLE, None, None),
    ('04_unleash_your_special', '04_special', 75, '// OVERDRIVE ONLINE', 'UNLEASH', 'YOUR SPECIAL', SOFT_ORANGE,
     'Every ship has its own ultimate ability.', ORANGE, (540, 960), None),
    ('05_crush_giant_bosses', '05_boss', 420, '!! WARNING  ·  ABERRANT CORE', 'CRUSH', 'GIANT BOSSES', GOLD,
     'Clear 10 waves, then take down the core.', RED, (540, 1200), None),
    ('06_choose_your_ship', None, None, '// HANGAR', 'CHOOSE', 'YOUR SHIP', CYAN,
     'Each ship has its own playstyle.', CYAN, None, None),
    ('07_permanent_upgrades', '07_upgrades', 100, '// SPEND YOUR CREDITS', 'UPGRADE', 'PERMANENT POWER', GOLD,
     'Fire rate, damage, hull, crits and more for every ship.', GOLD, None, ct.CARD_CROP),
    ('08_unlock_new_cards', '08_basedeck', 80, '// BASE DECK', 'UNLOCK', 'NEW CARDS', LILAC,
     'Shared cards for every ship, plus exclusives for each one.', PURPLE, None, ct.CARD_CROP),
]

HANGAR_CARDS = [(10, (255, 110, 70)), (50, (90, 220, 160)), (83, (170, 120, 255)), (116, (255, 120, 170)), (150, (240, 190, 70))]


# ----------------------------------------------------------------------
# Pieces (all sized from the canvas)
# ----------------------------------------------------------------------

def frame(shot, index):
    return Image.fromarray(ct.raw(shot, index))


def background(size, src, accent, seed):
    """Deep space, the shot itself blown up and dimmed behind everything, nebulae, stars, vignette."""
    sw, sh = size
    rnd = random.Random(seed)
    scale = max(sw / src.width, sh / src.height)
    amb = src.resize((int(src.width * scale) + 1, int(src.height * scale) + 1), Image.BILINEAR)
    left, top = (amb.width - sw) // 2, (amb.height - sh) // 2
    amb = amb.crop((left, top, left + sw, top + sh)).filter(ImageFilter.GaussianBlur(28))
    img = Image.blend(Image.new('RGB', (sw, sh), (5, 8, 22)), amb, 0.28)
    neb = Image.new('RGB', (sw, sh))
    d = ImageDraw.Draw(neb)
    for _ in range(4):
        x, y, r = rnd.uniform(0, sw), rnd.uniform(0, sh), rnd.uniform(260, 520)
        c = rnd.choice([accent, (70, 26, 125), (10, 70, 105)])
        d.ellipse((x - r, y - r, x + r, y + r), fill=tuple(int(v * 0.45) for v in c))
    img = ImageChops.add(img, neb.filter(ImageFilter.GaussianBlur(150)))
    d = ImageDraw.Draw(img)
    for _ in range(int(sw * sh / 8000)):
        x, y = rnd.uniform(0, sw), rnd.uniform(0, sh)
        r = rnd.choice([1, 1, 1, 2, 2, 3])
        v = rnd.randint(110, 255)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(v, v, min(255, v + 20)))
    yy, xx = [a.astype('float32') for a in np.mgrid[0:sh // 4, 0:sw // 4]]
    nx, ny = (xx / (sw / 4) - 0.5) * 2, (yy / (sh / 4) - 0.5) * 2
    v = np.clip(1 - (nx ** 2 * 0.35 + ny ** 2 * 0.45), 0.35, 1)
    vig = Image.fromarray((v * 255).astype('uint8')).resize((sw, sh), Image.BILINEAR)
    return Image.composite(img, Image.new('RGB', (sw, sh), (2, 4, 12)), vig).convert('RGBA')


def overlay(canvas):
    return Image.new('RGBA', canvas.size, (0, 0, 0, 0))


def speed_lines(canvas, cx, cy, color, seed, count=60, alpha=90, r0=(380, 560)):
    rnd = random.Random(seed)
    layer = overlay(canvas)
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        ang = rnd.uniform(0, 2 * math.pi)
        a = rnd.uniform(*r0)
        b = a + rnd.uniform(200, 700)
        d.line([(cx + math.cos(ang) * a, cy + math.sin(ang) * a), (cx + math.cos(ang) * b, cy + math.sin(ang) * b)],
               fill=color + (rnd.randint(alpha // 3, alpha),), width=rnd.choice([2, 2, 3, 4]))
    canvas.alpha_composite(layer)


def rings(canvas, cx, cy, color, radii=(520, 600)):
    layer = overlay(canvas)
    d = ImageDraw.Draw(layer)
    for k, r in enumerate(radii):
        for a0 in range(0, 360, 40):
            d.arc((cx - r, cy - r, cx + r, cy + r), a0 + k * 12, a0 + 24 + k * 12, fill=color + (120 - 40 * k,), width=4 - k)
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(1)))


def device(canvas, src, cx, cy, height, tilt, color, crop=None):
    crop = crop or 1920
    shot = ct.juicy(src, 0.6, 1.2, 1.05).crop((0, 0, 1080, crop))
    h = int(height)
    w = int(1080 * h / crop)
    shot = shot.resize((w, h), Image.LANCZOS).convert('RGBA')
    radius = int(h * 0.035)
    shot.putalpha(ct.round_mask(w, h, radius))
    pad = 80
    layer = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', layer.size, 0)
    ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + w, pad + h), radius, fill=230)
    glow = Image.new('RGBA', layer.size, color + (0,))
    glow.putalpha(g.filter(ImageFilter.GaussianBlur(38)))
    layer.alpha_composite(glow)
    layer.alpha_composite(shot, (pad, pad))
    ImageDraw.Draw(layer).rounded_rectangle((pad - 4, pad - 4, pad + w + 4, pad + h + 4), radius + 4,
                                            outline=color + (255,), width=5)
    if tilt:
        layer = layer.rotate(tilt, Image.BICUBIC, expand=True)
    canvas.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)))
    return w, h


def inset(canvas, src, sx, sy, cx, cy, radius, zoom, color, anchor):
    """A round magnifier of part of the shot, ringed, with a line back to where it looks."""
    crop_r = radius / zoom
    piece = ct.juicy(src, 0.9, 1.25, 1.06).crop((int(sx - crop_r), int(sy - crop_r), int(sx + crop_r), int(sy + crop_r)))
    piece = piece.resize((radius * 2, radius * 2), Image.LANCZOS).convert('RGBA')
    mask = Image.new('L', (radius * 4, radius * 4), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, radius * 4 - 1, radius * 4 - 1), fill=255)
    piece.putalpha(mask.resize((radius * 2, radius * 2), Image.LANCZOS))
    layer = overlay(canvas)
    d = ImageDraw.Draw(layer)
    d.line([anchor, (cx, cy)], fill=color + (200,), width=3)
    d.ellipse((anchor[0] - 14, anchor[1] - 14, anchor[0] + 14, anchor[1] + 14), outline=color + (255,), width=4)
    canvas.alpha_composite(layer)
    pad = 50
    holder = Image.new('RGBA', (radius * 2 + pad * 2, radius * 2 + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', holder.size, 0)
    ImageDraw.Draw(g).ellipse((pad, pad, pad + radius * 2, pad + radius * 2), fill=220)
    glow = Image.new('RGBA', holder.size, color + (0,))
    glow.putalpha(g.filter(ImageFilter.GaussianBlur(24)))
    holder.alpha_composite(glow)
    holder.alpha_composite(piece, (pad, pad))
    d = ImageDraw.Draw(holder)
    d.ellipse((pad - 3, pad - 3, pad + radius * 2 + 3, pad + radius * 2 + 3), outline=color + (255,), width=6)
    for a0 in range(0, 360, 90):
        d.arc((pad - 18, pad - 18, pad + radius * 2 + 18, pad + radius * 2 + 18), a0 + 20, a0 + 70, fill=color + (200,), width=3)
    canvas.alpha_composite(holder, (int(cx - holder.width / 2), int(cy - holder.height / 2)))


def sparks(canvas, cx, cy, color, seed, count=18, reach=300):
    rnd = random.Random(seed)
    layer = overlay(canvas)
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        ang = rnd.uniform(0, 2 * math.pi)
        r0 = rnd.uniform(300, 420)
        r1 = r0 + reach * rnd.uniform(0.2, 0.6)
        d.line([(cx + math.cos(ang) * r0, cy + math.sin(ang) * r0 * 0.4),
                (cx + math.cos(ang) * r1, cy + math.sin(ang) * r1 * 0.4)],
               fill=color + (rnd.randint(120, 230),), width=rnd.choice([2, 3, 4]))
    canvas.alpha_composite(layer)


def hud(canvas, color=CYAN):
    sw, sh = canvas.size
    layer = overlay(canvas)
    d = ImageDraw.Draw(layer)
    m, L = 30, 110
    for x, y, sx, sy in ((m, m, 1, 1), (sw - m, m, -1, 1), (m, sh - m, 1, -1), (sw - m, sh - m, -1, -1)):
        d.line([(x, y + sy * L), (x, y), (x + sx * L, y)], fill=color + (180,), width=4)
        d.line([(x + sx * 16, y + sy * 16), (x + sx * 46, y + sy * 16)], fill=color + (110,), width=2)
    for y in range(0, sh, 4):
        d.line([(0, y), (sw, y)], fill=(0, 0, 0, 14))
    canvas.alpha_composite(layer)


def text_block(canvas, cx, y0, label, title, accent, accent_color, body, color, title_size=168, max_w=860):
    T = 1.0  # every animated helper drawn fully open
    ct.label(canvas, T, 0, 99, label, cx, y0, RED if label.startswith('!') else color, 36)
    spr = ct.txt(title, title_size, WHITE, color, ct.BOLD, 0.14, 4, 0.85, None, max_w)
    ct.title_frame(canvas, T, 0, 99, cx, y0 + title_size * 0.9, spr.width - title_size * 0.45, title_size * 1.3, color)
    ct.put(canvas, spr, cx, y0 + title_size * 0.87)
    sparks(canvas, cx, y0 + title_size * 0.87, color, len(title))
    a_size = int(title_size * 0.45)
    line = ct.txt(accent, a_size, accent_color, accent_color, ct.BOLD, 0.2, 3, 0.7, None, max_w)
    ay = y0 + title_size * 1.97
    ct.plate(canvas, T, 0, 99, cx, ay, line.width - a_size * 0.55, a_size * 1.15, accent_color)
    ct.put(canvas, line, cx, ay)
    y = ay + a_size * 1.1
    if body:
        ct.put(canvas, ct.txt(body, 40, (200, 220, 238), None, ct.SEMI, 0.04, 0, 0, None, max_w - 60), cx, y + 30)
        y += 70
    return y


def brand(canvas, cx, cy, icon=120, gap=30):
    """Icon + name + FREE TO PLAY, centred as a group on cx. The name's letters are measured (the
    sprite carries glow padding), so a longer name keeps the same gap to the icon instead of
    touching it. Same layout as brand() in compose_appstore_duo.py."""
    name = ct.txt('ASTRO BLITZ', 64, WHITE, CYAN, ct.BOLD, 0.14, 3, 0.8)
    box = name.getchannel('A').point(lambda v: 255 if v > 200 else 0).getbbox()
    left, right = box[0] - name.width / 2, box[2] - name.width / 2
    x0 = cx - (icon + gap + right - left) / 2
    name_x = x0 + icon + gap - left
    ct.put(canvas, ct.app_icon(icon), x0 + icon / 2, cy)
    ct.put(canvas, name, name_x, cy - 18)
    ct.put(canvas, ct.txt('FREE TO PLAY', 30, (150, 190, 215), None, ct.SEMI, 0.3, 0, 0), name_x, cy + 34)


def chips(canvas, cx, y, spacing_x=450, spacing_y=108):
    for k, (n, c) in enumerate(ct.COMBO_CHIPS):
        col, row = k % 2, k // 2
        ct.put(canvas, ct.chip_sprite(n, c), cx - spacing_x / 2 + col * spacing_x, y + row * spacing_y)


def ship_fan(canvas, cx0, cy0, spacing, centre_w, side_w):
    for slot in (0, 4, 1, 3, 2):  # outer cards first, the middle one last
        idx, accent = HANGAR_CARDS[slot]
        img = frame('06_hangar', idx).crop((176, 330, 904, 1560))
        img = ImageEnhance.Contrast(ImageEnhance.Color(img).enhance(1.2)).enhance(1.05)
        w = centre_w if slot == 2 else side_w
        h = int(w * img.height / img.width)
        card = img.resize((w, h), Image.LANCZOS).convert('RGBA')
        card.putalpha(ct.round_mask(w, h, 22))
        pad = 60
        layer = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
        g = Image.new('L', layer.size, 0)
        ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + w, pad + h), 22, fill=230)
        glow = Image.new('RGBA', layer.size, accent + (0,))
        glow.putalpha(g.filter(ImageFilter.GaussianBlur(26)))
        layer.alpha_composite(glow)
        layer.alpha_composite(card, (pad, pad))
        ImageDraw.Draw(layer).rounded_rectangle((pad - 3, pad - 3, pad + w + 3, pad + h + 3), 25, outline=accent + (255,), width=4)
        off = slot - 2
        layer = layer.rotate(-off * 7, Image.BICUBIC, expand=True)
        x, y = cx0 + off * spacing, cy0 + abs(off) * 40
        canvas.alpha_composite(layer, (int(x - layer.width / 2), int(y - layer.height / 2)))


def save(canvas, folder, name):
    path = os.path.join(OUT, folder, name + '.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    canvas.convert('RGB').save(path, optimize=True)
    return path


# ----------------------------------------------------------------------
# Layouts
# ----------------------------------------------------------------------

def landscape(i, spec):
    name, shot, index, label, title, accent, accent_color, body, color, focus, crop = spec
    right = i % 2 == 0  # alternate the device's side
    src = frame(shot, index) if shot else frame('06_hangar', 83)
    canvas = background((1920, 1080), src, color, i + 1)
    dev_x = 1430 if right else 490
    txt_x = 640 if right else 1300
    if shot is None:  # the hangar: a fan of the ship cards
        txt_x = 470
        speed_lines(canvas, 1360, 540, color, i, 40, 60)
        ship_fan(canvas, 1360, 560, 180, 340, 280)
    else:
        speed_lines(canvas, dev_x, 540, color, i)
        rings(canvas, dev_x, 540, color)
        device(canvas, src, dev_x, 540, 960, -4 if right else 4, color, crop)
        if focus:
            fx, fy = focus
            inset(canvas, src, fx, fy, dev_x + (-250 if right else 250), 870, 130, 1.9, color,
                  (dev_x + (fx - 540) * 0.5, 540 + (fy - 960) * 0.5))
    y = text_block(canvas, txt_x, 250, label, title, accent, accent_color, body, color)
    if name.startswith('03_'):
        chips(canvas, txt_x, y + 40)
        brand(canvas, txt_x, 990)
    else:
        brand(canvas, txt_x, max(y + 90, 920))
    hud(canvas, color)
    return save(canvas, 'landscape_1920x1080', name)


def portrait(i, spec):
    name, shot, index, label, title, accent, accent_color, body, color, focus, crop = spec
    src = frame(shot, index) if shot else frame('06_hangar', 83)
    canvas = background((1080, 1920), src, color, i + 11)
    y = text_block(canvas, 540, 150, label, title, accent, accent_color, body, color, 156, 940)
    if shot is None:
        speed_lines(canvas, 540, 1200, color, i, 40, 60)
        ship_fan(canvas, 540, 1190, 175, 330, 250)
    else:
        dev_cy, dev_h = (1310, 940) if name.startswith('03_') else (1235, 1090)
        speed_lines(canvas, 540, dev_cy, color, i, 70, 90, (420, 600))
        rings(canvas, 540, dev_cy, color, (600, 680))
        device(canvas, src, 540, dev_cy, dev_h, 3 if i % 2 else -3, color, crop)
        if name.startswith('03_'):
            chips(canvas, 540, y + 40, 470, 100)
        if focus:
            fx, fy = focus
            scale = dev_h / 1920
            side = 1 if i % 2 == 0 else -1
            inset(canvas, src, fx, fy, 540 + side * 330, dev_cy + 300, 125, 1.9, color,
                  (540 + (fx - 540) * scale, dev_cy + (fy - 960) * scale))
    brand(canvas, 540, 1835)
    hud(canvas, color)
    return save(canvas, 'portrait_1080x1920', name)


def main():
    for i, spec in enumerate(SHOTS):
        print('wrote', landscape(i, spec))
        print('wrote', portrait(i, spec))


if __name__ == '__main__':
    main()
