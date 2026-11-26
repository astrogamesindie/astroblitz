# Astro Blitz - 30 s portrait trailer (1080 x 1920, 30 fps, English).
#
# Input:  the shots recorded in the Unity Editor by
#           Tools > AstroCrawler > Trailer > Gravar cenas do trailer  ->  Trailer/raw/<shot>/00000.jpg
#         the original music from compose_music.py                    ->  Trailer/build/music.wav
# Output: Trailer/AstroIdle_Trailer_30s_EN.mp4
#
# The edit sits on the music's grid (128 BPM, 64 beats = 30 s): every cut is on
# a beat, the two drops land on the space jump and on the boss going down.
# Juice: neon bloom and grade on the footage, a pulse + chromatic kick on every
# beat of the drops, shockwaves, light sweeps, shakes and hit-stops on impacts,
# HUD text styled like the game's logo (Rajdhani, wide tracking, cyan glow, lens
# flare), cut-corner plates, decoding lines, typed captions, corner brackets and
# scanlines, and zoom / whip / glitch / stripe /
# flash transitions on the cuts.
#
# Run:  python Tools/trailer/compose_trailer.py                  (music + frames + encode)
#       python Tools/trailer/compose_trailer.py --preview 4.2 9.8 (only those seconds, as JPGs)
#       python Tools/trailer/compose_trailer.py --encode-only   (needs Trailer/build/frames)
#       python Tools/trailer/compose_trailer.py --music track.wav  (use another 30 s track)
#       python Tools/trailer/compose_trailer.py --ios     (App Store cut: no other platform named,
#                                                         frames in Trailer/build/frames_ios)

import functools
import math
import os
import random
import subprocess
import sys
from multiprocessing import Pool

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RAW = os.path.join(ROOT, 'Trailer', 'raw')
BUILD = os.path.join(ROOT, 'Trailer', 'build')
# --ios: the App Store does not accept other platforms in previews, so the end card
# drops "ANDROID & iOS". Read at import, so the worker processes see it too.
IOS = '--ios' in sys.argv
FRAMES = os.path.join(BUILD, 'frames_ios' if IOS else 'frames')
MUSIC = os.path.join(BUILD, 'music.wav')
OUTPUT = os.path.join(ROOT, 'Trailer', 'AstroIdle_Trailer_30s_EN_iOS.mp4' if IOS else 'AstroIdle_Trailer_30s_EN.mp4')
GAME = os.path.join(ROOT, 'Assets', 'AstroCrawler')
FONTS = os.path.join(GAME, 'Resources', 'AstroCrawler', 'Art')
AUDIO = os.path.join(GAME, 'Audio')
APP_ICON = os.path.join(GAME, 'Art', 'AppIcon.png')

W, H, FPS = 1080, 1920, 30
DURATION = 30.0
TOTAL = int(DURATION * FPS)
BEAT = 60.0 / 128.0


def beat(k):
    return k * BEAT


WHITE = (255, 255, 255)
CYAN = (60, 228, 255)
GOLD = (255, 200, 70)
ORANGE = (255, 120, 50)
RED = (255, 64, 80)
PURPLE = (190, 110, 255)
NAVY = (6, 9, 22)

# Beats where the drops run (kick on every beat).
DROPS = [(8, 32), (40, 60)]


# ----------------------------------------------------------------------
# Easing and small maths
# ----------------------------------------------------------------------

def clamp01(x):
    return 0.0 if x < 0 else 1.0 if x > 1 else x


def ease_out_cubic(t):
    t = clamp01(t)
    return 1 - (1 - t) ** 3


def ease_in_cubic(t):
    t = clamp01(t)
    return t ** 3


def ease_out_back(t, s=1.9):
    t = clamp01(t) - 1
    return t * t * ((s + 1) * t + s) + 1


def ease_in_out(t):
    t = clamp01(t)
    return t * t * (3 - 2 * t)


def in_drop(t):
    b = t / BEAT
    return any(s <= b < e for s, e in DROPS)


def since_beat(t):
    return t - math.floor(t / BEAT) * BEAT


# ----------------------------------------------------------------------
# Source frames
# ----------------------------------------------------------------------

@functools.lru_cache(maxsize=None)
def shot_count(shot):
    return len([f for f in os.listdir(os.path.join(RAW, shot)) if f.endswith('.jpg')])


@functools.lru_cache(maxsize=24)
def raw(shot, index):
    index = max(0, min(shot_count(shot) - 1, int(index)))
    return np.asarray(Image.open(os.path.join(RAW, shot, '%05d.jpg' % index)).convert('RGB'))


def source(shot, pos, blur=1, span=1.0):
    """Frame at a fractional position; blur>1 averages `blur` frames over `span` (motion blur)."""
    if blur > 1:
        acc = np.zeros((H, W, 3), np.uint16)
        for k in range(blur):
            acc += raw(shot, round(pos + span * k / blur))
        return Image.fromarray((acc // blur).astype(np.uint8))
    i0 = int(math.floor(pos))
    frac = pos - i0
    a = raw(shot, i0)
    if frac < 0.05 or i0 + 1 >= shot_count(shot):
        return Image.fromarray(a)
    b = raw(shot, i0 + 1)
    return Image.fromarray((a * (1 - frac) + b * frac).astype(np.uint8))


def framed(img, scale=1.0, cx=W / 2, cy=H / 2, dx=0.0, dy=0.0):
    """Zoom around (cx, cy) of the source, which lands at the screen centre plus (dx, dy)."""
    if abs(scale - 1) < 1e-3 and abs(cx - W / 2) < 0.5 and abs(cy - H / 2) < 0.5 and abs(dx) < 0.5 and abs(dy) < 0.5:
        return img
    a = 1.0 / scale
    data = (a, 0, cx - (W / 2 + dx) * a, 0, a, cy - (H / 2 + dy) * a)
    return img.transform((W, H), Image.AFFINE, data, Image.BILINEAR, fillcolor=NAVY)


def juicy(img, bloom_strength=0.75, saturation=1.28, contrast=1.08):
    """Grade + neon bloom for the gameplay footage."""
    img = ImageEnhance.Contrast(ImageEnhance.Color(img).enhance(saturation)).enhance(contrast)
    small = np.asarray(img.resize((W // 4, H // 4), Image.BILINEAR)).astype(np.float32)
    lum = small.max(axis=2)
    mask = np.clip((lum - 120) / 100, 0, 1) ** 1.5
    bright = Image.fromarray((small * mask[..., None]).astype(np.uint8))
    glow = np.asarray(bright.filter(ImageFilter.GaussianBlur(5)).resize((W, H), Image.BILINEAR)).astype(np.float32)
    wide = np.asarray(bright.filter(ImageFilter.GaussianBlur(16)).resize((W, H), Image.BILINEAR)).astype(np.float32)
    base = np.asarray(img).astype(np.float32)
    out = base + (glow * 0.8 + wide * 0.7) * bloom_strength
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def kick_punch(t, amount=0.045):
    return 1.0 + amount * math.exp(-since_beat(t) * 9.0) if in_drop(t) else 1.0


def shake_at(t, events):
    dx = dy = 0.0
    for t0, strength in events:
        a = t - t0
        if 0 <= a < 0.32:
            k = (1 - a / 0.32) ** 2 * strength
            dx += 30 * k * math.sin(a * 97.0 + t0)
            dy += 30 * k * math.cos(a * 83.0 + t0 * 2)
    return dx, dy


# ----------------------------------------------------------------------
# Sprites and overlays
# ----------------------------------------------------------------------

def font(name, size):
    return ImageFont.truetype(name if os.path.isabs(name) else os.path.join(FONTS, name), size)


BOLD = 'Rajdhani-Bold.ttf'
SEMI = 'Rajdhani-SemiBold.ttf'
# Big words: the game's logo face (Rajdhani Bold) with the logo's 0.14 em tracking.
DISPLAY, DISPLAY_EM = BOLD, 0.14


def put(canvas, sprite, cx, cy, scale=1.0, alpha=1.0):
    if alpha <= 0.01 or scale <= 0.01:
        return
    s = sprite
    if abs(scale - 1) > 1e-3:
        s = sprite.resize((max(1, int(sprite.width * scale)), max(1, int(sprite.height * scale))), Image.BILINEAR)
    if alpha < 0.999:
        s = s.copy()
        s.putalpha(s.getchannel('A').point(lambda v: int(v * alpha)))
    x, y = int(cx - s.width / 2), int(cy - s.height / 2)
    if x >= canvas.width or y >= canvas.height or x + s.width <= 0 or y + s.height <= 0:
        return
    canvas.alpha_composite(s, (max(0, x), max(0, y)), (max(0, -x), max(0, -y)))


def anim(canvas, t, t_in, t_out, sprite, cx, cy, style='pop', exit_time=0.12):
    """Animated text: 'pop' (overshoot), 'slam' (from big), 'left'/'right' (slide), 'rise'."""
    if t < t_in or t > t_out:
        return
    a = t - t_in
    scale, alpha, x, y = 1.0, clamp01(a / 0.06), cx, cy
    if style == 'pop':
        scale = 0.3 + 0.7 * ease_out_back(a / 0.24)
    elif style == 'slam':
        scale = 1.0 + 1.6 * (1 - ease_out_cubic(a / 0.18))
        alpha = clamp01(a / 0.08)
    elif style == 'left':
        x = cx - 1000 * (1 - ease_out_cubic(a / 0.26))
    elif style == 'right':
        x = cx + 1000 * (1 - ease_out_cubic(a / 0.26))
    elif style == 'rise':
        y = cy + 110 * (1 - ease_out_cubic(a / 0.28))
    elif style == 'fade':
        alpha = clamp01(a / 0.05)
    left = t_out - t
    if left < exit_time:
        k = 1 - left / exit_time
        alpha *= 1 - k
        scale *= 1 + 0.15 * k
    put(canvas, sprite, x, y, scale, alpha)


@functools.lru_cache(maxsize=16)
def band(height, strength, where='bottom'):
    """Dark gradient so text reads over gameplay."""
    ys = np.linspace(0, 1, height)
    if where == 'bottom':
        col = ys ** 1.4
    elif where == 'top':
        col = (1 - ys) ** 1.4
    elif where == 'bottom_hard':
        col = np.clip(ys / 0.3, 0, 1) ** 1.2
    else:
        col = np.clip((1 - np.abs(ys - 0.5) * 2) * 1.6, 0, 1)
    alpha = (col * 255 * strength).astype(np.uint8)
    img = Image.new('RGBA', (W, height), NAVY + (0,))
    img.putalpha(Image.fromarray(np.repeat(alpha[:, None], W, axis=1)))
    return img


@functools.lru_cache(maxsize=32)
def chip_sprite(text, accent):
    """Loadout chip: a cut-corner HUD plate with an accent bar."""
    size = 44
    spr = txt(text, size, WHITE, accent, BOLD, 0.14, 2, 0.5)
    w, h = spr.width - 10 + 70, 88
    img = Image.new('RGBA', (w + 40, h + 40), (0, 0, 0, 0))
    glow = Image.new('L', img.size, 0)
    c = 20
    poly = [(20 + c, 20), (20 + w, 20), (20 + w, 20 + h - c), (20 + w - c, 20 + h), (20, 20 + h), (20, 20 + c)]
    ImageDraw.Draw(glow).polygon(poly, fill=150)
    glow = glow.filter(ImageFilter.GaussianBlur(12))
    gl = Image.new('RGBA', img.size, accent + (0,))
    gl.putalpha(glow)
    img = Image.alpha_composite(img, gl)
    d = ImageDraw.Draw(img)
    d.polygon(poly, fill=(6, 12, 30, 235))
    d.line(poly + [poly[0]], fill=accent + (255,), width=3)
    d.polygon([(20, 20 + c), (20 + c, 20), (20 + c + 12, 20), (32, 20 + c + 12), (32, 20 + h), (20, 20 + h)], fill=accent + (255,))
    img.alpha_composite(spr, (58 + 4, int(20 + h / 2 - spr.height / 2)))
    return img


def ripple(canvas, t, t_tap, x, y, color=WHITE):
    """A finger tap: the dot grows in just before the tap, rings expand after it."""
    a = t - t_tap
    if a < -0.25 or a > 0.6:
        return
    layer = Image.new('RGBA', (440, 440), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    c = 220
    if a < 0:
        k = ease_out_cubic((a + 0.25) / 0.25)
        r = 72 - 26 * k
        d.ellipse((c - r, c - r, c + r, c + r), fill=color + (int(150 * k),), outline=color + (int(255 * k),), width=6)
    else:
        for delay, width in ((0.0, 12), (0.1, 7)):
            b = a - delay
            if b < 0:
                continue
            k = ease_out_cubic(b / 0.45)
            r = 46 + 160 * k
            d.ellipse((c - r, c - r, c + r, c + r), outline=color + (int(255 * (1 - k)),), width=width)
        k = clamp01(a / 0.25)
        r = 46 * (1 - 0.3 * k)
        d.ellipse((c - r, c - r, c + r, c + r), fill=color + (int(210 * (1 - k)),))
    canvas.alpha_composite(layer, (int(x - c), int(y - c)))


def sparks(canvas, t, t0, cx, cy, color=GOLD, count=16, reach=420, seed=0):
    a = t - t0
    if a < 0 or a > 0.4:
        return
    rnd = random.Random(seed)
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    k = ease_out_cubic(a / 0.35)
    fade = 1 - clamp01(a / 0.4)
    for _ in range(count):
        ang = rnd.uniform(0, 2 * math.pi)
        r0 = rnd.uniform(60, 160)
        r1 = r0 + reach * rnd.uniform(0.5, 1.0) * k
        r_start = r0 + (r1 - r0) * 0.55
        x0, y0 = cx + math.cos(ang) * r_start, cy + math.sin(ang) * r_start * 0.6
        x1, y1 = cx + math.cos(ang) * r1, cy + math.sin(ang) * r1 * 0.6
        d.line([(x0, y0), (x1, y1)], fill=color + (int(255 * fade),), width=rnd.choice([3, 4, 6]))
    canvas.alpha_composite(layer)


def shockwave(canvas, t, t0, cx, cy, color=WHITE, size=1.0):
    a = t - t0
    if a < 0 or a > 0.5:
        return
    k = ease_out_cubic(a / 0.5)
    r = 40 + 1000 * size * k
    w = int(40 * (1 - k) + 4)
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    alpha = int(255 * (1 - k) ** 1.3)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=color + (alpha,), width=w)
    d.ellipse((cx - r * 0.82, cy - r * 0.82, cx + r * 0.82, cy + r * 0.82), outline=CYAN + (alpha // 2,), width=max(2, w // 2))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(2)))


def light_sweep(img, t, t0, length=0.45):
    a = t - t0
    if a < 0 or a > length:
        return img
    k = ease_in_out(a / length)
    pos = -500 + (W + H + 1000) * k
    yy, xx = np.mgrid[0:H // 4, 0:W // 4]
    d = (xx * 4 + yy * 4 * 0.55) - pos
    m = np.exp(-(d / 110.0) ** 2) * 0.55 + np.exp(-(d / 320.0) ** 2) * 0.18
    mask = Image.fromarray((m * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)
    return Image.composite(Image.new('RGB', (W, H), (235, 250, 255)), img, mask)


@functools.lru_cache(maxsize=8)
def speed_lines_layer(seed):
    rnd = random.Random(seed)
    layer = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(layer)
    for _ in range(70):
        ang = rnd.uniform(0, 2 * math.pi)
        r0 = rnd.uniform(380, 700)
        r1 = r0 + rnd.uniform(500, 1300)
        width = rnd.choice([2, 3, 4, 6])
        d.line([(540 + math.cos(ang) * r0, 960 + math.sin(ang) * r0),
                (540 + math.cos(ang) * r1, 960 + math.sin(ang) * r1)], fill=rnd.randint(120, 255), width=width)
    return layer


def speed_lines(canvas, t, strength):
    if strength <= 0.02:
        return
    lines = speed_lines_layer(int(t * FPS) % 6)
    layer = Image.new('RGBA', (W, H), (220, 245, 255, 0))
    layer.putalpha(lines.point(lambda v: int(v * strength * 0.55)))
    canvas.alpha_composite(layer)


@functools.lru_cache(maxsize=1)
def edge_mask():
    yy, xx = np.mgrid[0:H // 4, 0:W // 4]
    nx, ny = (xx / (W / 4) - 0.5) * 2, (yy / (H / 4) - 0.5) * 2
    d = np.clip(np.maximum(np.abs(nx) ** 3, np.abs(ny) ** 6), 0, 1)
    return Image.fromarray((d * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)


def edge_glow(img, color, amount):
    if amount <= 0.02:
        return img
    m = edge_mask().point(lambda v: int(v * amount / 255.0))
    return Image.composite(Image.new('RGB', (W, H), color), img, m)


def flash(img, amount, color=WHITE):
    if amount <= 0.01:
        return img
    return Image.blend(img, Image.new('RGB', img.size, color), clamp01(amount))


def rgb_split(img, px):
    if px < 1:
        return img
    r, g, b = img.split()
    r = ImageChops.offset(r, int(px), 0)
    b = ImageChops.offset(b, -int(px), int(px * 0.3))
    return Image.merge('RGB', (r, g, b))


def zoom_blur(img, amount, steps=5):
    if amount < 0.01:
        return img
    acc = np.zeros((H, W, 3), np.float32)
    for k in range(steps):
        acc += np.asarray(framed(img, 1 + amount * k / (steps - 1)), np.float32)
    return Image.fromarray((acc / steps).astype(np.uint8))


def whip(img, offset, blur):
    acc = np.zeros((H, W, 3), np.float32)
    steps = 6
    for k in range(steps):
        acc += np.asarray(framed(img, 1.0, dx=offset + blur * (k / (steps - 1) - 0.5)), np.float32)
    return Image.fromarray((acc / steps).astype(np.uint8))


def glitch(img, strength, seed):
    if strength <= 0.02:
        return img
    rnd = random.Random(seed)
    a = np.asarray(img).copy()
    y = 0
    while y < H:
        h = rnd.randint(16, 140)
        if rnd.random() < 0.5 * strength + 0.15:
            shift = int(rnd.uniform(-90, 90) * strength)
            a[y:y + h] = np.roll(a[y:y + h], shift, axis=1)
        y += h
    for _ in range(int(6 * strength)):
        x0, y0 = rnd.randint(0, W - 200), rnd.randint(0, H - 60)
        col = rnd.choice([CYAN, PURPLE, (255, 255, 255), RED])
        a[y0:y0 + rnd.randint(8, 40), x0:x0 + rnd.randint(60, 360)] = col
    return rgb_split(Image.fromarray(a), 22 * strength)


# ----------------------------------------------------------------------
# Sci-fi HUD text, styled like the game's logo: Rajdhani, wide tracking,
# white/cyan with a soft glow and the logo's lens flare.
# ----------------------------------------------------------------------

UI_ART = os.path.join(GAME, 'Resources', 'AstroCrawler', 'UI')
GLYPHS = 'ABCDEFGHJKLMNPRSTUVWXYZ0123456789#/<>=+'


def fit_size(text, size, em, face=BOLD, max_w=960):
    while size > 28:
        f = font(face, size)
        w = sum(f.getlength(c) for c in text) + int(size * em) * (len(text) - 1)
        if w <= max_w:
            return size
        size -= 4
    return size


@functools.lru_cache(maxsize=400)
def hud_sprite(final, shown, size, color, glow, face, em, stroke, glow_alpha):
    """Text drawn in fixed slots (the final text's widths), so decoding never jitters."""
    f = font(face, size)
    tr = int(size * em)
    widths = [f.getlength(c) for c in final]
    width = int(sum(widths) + tr * max(0, len(final) - 1))
    ascent, descent = f.getmetrics()
    pad = int(size * 0.45) + stroke
    w, h = width + pad * 2, ascent + descent + pad * 2
    fill = Image.new('L', (w, h), 0)
    edge = Image.new('L', (w, h), 0)
    df, de = ImageDraw.Draw(fill), ImageDraw.Draw(edge)
    x = pad
    for c, cw in zip(shown, widths):
        if c != ' ':
            gx = x + (cw - f.getlength(c)) / 2
            df.text((gx, pad), c, font=f, fill=255)
            de.text((gx, pad), c, font=f, fill=255, stroke_width=stroke, stroke_fill=255)
        x += cw + tr
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    if glow is not None:
        halo = edge.filter(ImageFilter.GaussianBlur(size * 0.14))
        g = Image.new('RGBA', (w, h), glow + (0,))
        g.putalpha(halo.point(lambda v: int(min(255, v * glow_alpha * 1.5))))
        out = Image.alpha_composite(out, g)
    dark = Image.new('RGBA', (w, h), (2, 6, 16, 0))
    dark.putalpha(edge.point(lambda v: int(v * 0.75)))
    out = Image.alpha_composite(out, dark)
    top = Image.new('RGBA', (w, h), color + (0,))
    top.putalpha(fill)
    return Image.alpha_composite(out, top)


def txt(text, size, color=WHITE, glow=CYAN, face=BOLD, em=0.14, stroke=4, glow_alpha=0.8, shown=None, max_w=960):
    size = fit_size(text, size, em, face, max_w)
    return hud_sprite(text, shown if shown is not None else text, size, color, glow, face, em, stroke, glow_alpha)


def decode(text, t, t0, duration=0.32, seed=0):
    k = (t - t0) / duration
    if k >= 1:
        return text
    rnd = random.Random(int(t * FPS) * 31 + seed)
    n = max(1, len(text))
    return ''.join(c if c == ' ' or i / n < k else rnd.choice(GLYPHS) for i, c in enumerate(text))


@functools.lru_cache(maxsize=8)
def flare_sprite(width, color):
    im = Image.open(os.path.join(UI_ART, 'Lens Flare.png')).convert('RGBA')
    band = im.crop((0, 300, im.width, 600)).resize((width, int(width * 300 / im.width)), Image.BILINEAR)
    lum = band.getchannel('A').point(lambda v: v) if band.mode == 'RGBA' else band.convert('L')
    lum = ImageChops.multiply(band.convert('L'), lum)
    out = Image.new('RGBA', band.size, color + (0,))
    out.putalpha(lum.point(lambda v: min(255, int(v * 2.2))))
    core = Image.new('RGBA', band.size, (255, 255, 255, 0))
    core.putalpha(lum.point(lambda v: max(0, min(255, int((v - 90) * 2.4)))))
    out.alpha_composite(core)
    return out


def flare(canvas, t, t_in, t_out, cx, cy, width=1150, color=CYAN):
    """The lens-flare streak that sits under the logo in the game's menu."""
    if t < t_in or t > t_out:
        return
    a = t - t_in
    k = 0.15 + 0.85 * ease_out_cubic(a / 0.3)
    alpha = clamp01(a / 0.05) * clamp01((t_out - t) / 0.12) * (0.8 + 0.2 * math.exp(-a * 6))
    spr = flare_sprite(width, color)
    spr = spr.resize((max(1, int(spr.width * k)), spr.height), Image.BILINEAR)
    put(canvas, spr, cx, cy, 1.0, alpha)


def plate(canvas, t, t_in, t_out, cx, cy, w, h, color=CYAN, fill=(5, 12, 30)):
    """A cut-corner holographic plate that opens from the centre and flickers in."""
    if t < t_in or t > t_out:
        return
    a = t - t_in
    k = ease_out_cubic(a / 0.18)
    out = clamp01((t_out - t) / 0.1)
    flick = 0.35 if a < 0.14 and int(a * FPS) % 2 == 1 else 1.0
    ww = max(2.0, w * k)
    layer = Image.new('RGBA', (int(w) + 60, int(h) + 60), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0 = (layer.width - ww) / 2
    x1 = x0 + ww
    y0, y1 = 30, 30 + h
    c = min(22, ww / 4)
    poly = [(x0 + c, y0), (x1, y0), (x1, y1 - c), (x1 - c, y1), (x0, y1), (x0, y0 + c)]
    d.polygon(poly, fill=fill + (int(205 * out * flick),))
    d.line(poly + [poly[0]], fill=color + (int(235 * out * flick),), width=3)
    tick = min(46, ww / 5)
    for (px, py, sx, sy) in ((x0 - 10, y0 - 10, 1, 1), (x1 + 10, y1 + 10, -1, -1)):
        d.line([(px, py + sy * tick), (px, py), (px + sx * tick, py)], fill=color + (int(255 * out * flick),), width=4)
    canvas.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)))


def hud_line(canvas, t, t_in, t_out, text, cx, cy, size=92, color=CYAN, glow=CYAN, plate_color=None, em=0.2, seed=0):
    """Secondary line: plate + decoding text."""
    if t < t_in or t > t_out:
        return
    spr = txt(text, size, color, glow, BOLD, em, 3, 0.7)
    plate(canvas, t, t_in, t_out, cx, cy, spr.width - size * 0.55, size * 1.15, plate_color or color)
    shown = decode(text, t, t_in + 0.04, 0.3, seed)
    anim(canvas, t, t_in + 0.04, t_out, txt(text, size, color, glow, BOLD, em, 3, 0.7, shown), cx, cy, 'fade')


def label(canvas, t, t_in, t_out, text, cx, cy, color=CYAN, size=36):
    """Small HUD caption typed in, with a blinking cursor."""
    if t < t_in or t > t_out:
        return
    n = int((t - t_in) / 0.02) + 1
    cursor = '_' if int(t * 5) % 2 == 0 else ' '
    shown = (text[:n] + (cursor if n < len(text) else '')).ljust(len(text))[:len(text)]
    if n >= len(text):
        shown = text
    alpha = clamp01((t_out - t) / 0.1)
    put(canvas, txt(text, size, color, color, SEMI, 0.3, 2, 0.45, shown, 1000), cx, cy, 1.0, alpha)


@functools.lru_cache(maxsize=8)
def ui_sprite(name, folder=None):
    return Image.open(os.path.join(folder or UI_ART, name)).convert('RGBA')


def nine_slice(src, w, h, border, scale=1.0):
    """Stretches a sprite keeping its corners (border in source pixels, drawn at `scale`)."""
    w, h = max(2, int(w)), max(2, int(h))
    sw, sh = src.size
    b = border
    db = max(1, int(b * scale))
    db = min(db, w // 2, h // 2)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    xs = [(0, b, 0, db), (b, sw - b, db, w - db), (sw - b, sw, w - db, w)]
    ys = [(0, b, 0, db), (b, sh - b, db, h - db), (sh - b, sh, h - db, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 - dx0 <= 0 or dy1 - dy0 <= 0:
                continue
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.BILINEAR)
            out.alpha_composite(piece, (dx0, dy0))
    return out


def tint(img, color, boost=1.0, core=True):
    """White sprite -> coloured glow, with a whiter core where it is brightest."""
    a = img.getchannel('A')
    out = Image.new('RGBA', img.size, color + (0,))
    out.putalpha(a.point(lambda v: min(255, int(v * boost))))
    if core:
        c = Image.new('RGBA', img.size, (255, 255, 255, 0))
        c.putalpha(a.point(lambda v: max(0, min(255, int((v - 170) * 2.5)))))
        out.alpha_composite(c)
    return out


@functools.lru_cache(maxsize=48)
def title_frame_sprite(w, h, color):
    """The sci-fi pack's glowing cut-corner frame, stretched behind a title, with a dark glass fill."""
    w, h = int(w), int(h)
    fill = nine_slice(ui_sprite('Cut Frame Filled Big (300ppu).png'), w - 16, h - 16, 40, 0.9)
    glass = Image.new('RGBA', fill.size, (6, 12, 30, 0))
    glass.putalpha(fill.getchannel('A').point(lambda v: int(v * 0.62)))
    frame = tint(nine_slice(ui_sprite('Cut Frame Glow Big - 6px (250ppu).png'), w, h, 52, 1.0), color, 1.25)
    out = Image.new('RGBA', (w + 40, h + 40), (0, 0, 0, 0))
    out.alpha_composite(glass, (28, 28))
    out.alpha_composite(frame, (20, 20))
    return out


@functools.lru_cache(maxsize=8)
def hex_sprite(size, color):
    hx = ui_sprite('wire_hex.png', os.path.join(GAME, 'Resources', 'AstroCrawler', 'Art')).resize((size, size), Image.BILINEAR)
    return tint(hx, color, 1.0, False)


def title_frame(canvas, t, t_in, t_out, cx, cy, w, h, color=CYAN):
    """Opens from the centre with a flicker, a light sweeps across, hexes spin at the ends."""
    if t < t_in or t > t_out:
        return
    a = t - t_in
    k = ease_out_cubic(a / 0.22)
    out = clamp01((t_out - t) / 0.12)
    flick = 0.3 if a < 0.16 and int(a * FPS) % 2 == 1 else 1.0
    spr = title_frame_sprite(int(w), int(h), color)
    ww = max(60, int(spr.width * k))
    shown = spr.resize((ww, spr.height), Image.BILINEAR) if ww != spr.width else spr
    put(canvas, shown, cx, cy, 1.0, out * flick)
    # light sweep across the glass once it is open
    s = (a - 0.22) / 0.45
    if 0 <= s <= 1:
        shine = ui_sprite('Shiny Move.png').resize((int(h * 0.9), int(h * 0.95)), Image.BILINEAR)
        sx = cx - w / 2 + 40 + (w - 80) * ease_in_out(s)
        put(canvas, tint(shine, (220, 250, 255), 0.55, False), sx, cy, 1.0, out * (1 - abs(s - 0.5) * 1.6))
    if k > 0.6:
        hsz = int(h * 0.42)
        hx = hex_sprite(hsz, color).rotate(a * 60, Image.BILINEAR)
        for side in (-1, 1):
            put(canvas, hx, cx + side * (ww / 2 + hsz * 0.15), cy, 1.0, out * flick * clamp01((k - 0.6) / 0.3))


def tech_rule(canvas, t, t_in, cx, cy, width, color=CYAN):
    """Under the logo: a thin line that draws out, dashes, and a hex at each end."""
    if t < t_in:
        return
    a = t - t_in
    k = ease_out_cubic(a / 0.35)
    layer = Image.new('RGBA', (int(width) + 120, 60), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    mid = layer.width / 2
    half = width / 2 * k
    d.line([(mid - half, 30), (mid + half, 30)], fill=color + (230,), width=3)
    for i in range(int(half // 26)):
        for side in (-1, 1):
            x = mid + side * (half - i * 26)
            if abs(x - mid) > 40:
                d.line([(x, 38), (x, 44)], fill=color + (150,), width=2)
    glow = layer.filter(ImageFilter.GaussianBlur(6))
    canvas.alpha_composite(glow, (int(cx - layer.width / 2), int(cy - 30)))
    canvas.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - 30)))
    if k > 0.8:
        hx = hex_sprite(46, color).rotate(a * 50, Image.BILINEAR)
        for side in (-1, 1):
            put(canvas, hx, cx + side * (half + 18), cy, 1.0, clamp01((k - 0.8) / 0.2))


def headline(canvas, t, t_in, t_out, text, cx, cy, size=150, color=WHITE, glow=CYAN, flare_color=CYAN, seed=0):
    """Main line: the sci-fi frame opens behind it, the text slams in with a glitch, sparks."""
    if t < t_in or t > t_out:
        return
    spr = txt(text, size, color, glow)
    title_frame(canvas, t, t_in, t_out, cx, cy + size * 0.04, spr.width - size * 0.45, size * 1.3, flare_color)
    a = t - t_in
    if a < 0.1:
        r, g, b, al = spr.split()
        off = int(18 * (1 - a / 0.1))
        spr = Image.merge('RGBA', (ImageChops.offset(r, off, 0), g, ImageChops.offset(b, -off, 0), al))
    anim(canvas, t, t_in, t_out, spr, cx, cy, 'slam')
    sparks(canvas, t, t_in + 0.08, cx, cy, flare_color, seed=seed)


@functools.lru_cache(maxsize=1)
def hud_frame():
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    m, L = 34, 96
    for x, y, sx, sy in ((m, m, 1, 1), (W - m, m, -1, 1), (m, H - m, 1, -1), (W - m, H - m, -1, -1)):
        d.line([(x, y + sy * L), (x, y), (x + sx * L, y)], fill=CYAN + (170,), width=4)
        d.line([(x + sx * 14, y + sy * 14), (x + sx * 40, y + sy * 14)], fill=CYAN + (110,), width=2)
    lines = Image.new('L', (W, H), 0)
    ld = ImageDraw.Draw(lines)
    for y in range(0, H, 4):
        ld.line([(0, y), (W, y)], fill=16)
    scan = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    scan.putalpha(lines)
    scan.alpha_composite(layer)
    return scan


# ----------------------------------------------------------------------
# Backgrounds, device frame, end card art
# ----------------------------------------------------------------------

@functools.lru_cache(maxsize=1)
def space_base():
    ys = np.linspace(0, 1, H)[:, None]
    top, bottom = np.array([8, 10, 30]), np.array([18, 10, 40])
    img = (top * (1 - ys) + bottom * ys)[:, None, :].repeat(W, axis=1).reshape(H, W, 3)
    base = Image.fromarray(img.astype(np.uint8))
    neb = Image.new('RGB', (W, H))
    d = ImageDraw.Draw(neb)
    for x, y, r, c in [(200, 420, 380, (70, 26, 125)), (900, 1250, 420, (10, 70, 105)),
                       (300, 1700, 300, (48, 20, 95)), (820, 300, 260, (14, 58, 82))]:
        d.ellipse((x - r, y - r, x + r, y + r), fill=c)
    neb = neb.filter(ImageFilter.GaussianBlur(120))
    return ImageChops.add(base, neb)


@functools.lru_cache(maxsize=1)
def stars():
    rnd = random.Random(11)
    return [(rnd.uniform(0, W), rnd.uniform(0, H), rnd.choice([1, 1, 1, 2, 2, 3]), rnd.uniform(0.4, 1.0))
            for _ in range(320)]


def space(t, speed=60.0, warp=0.0):
    img = space_base().copy()
    d = ImageDraw.Draw(img)
    for x, y, r, b in stars():
        yy = (y + t * speed * r) % H
        v = int(255 * b)
        if warp > 0:
            dx, dy = x - 540, yy - 960
            d.line([(x, yy), (x + dx * warp * 0.25, yy + dy * warp * 0.25)], fill=(v, v, min(255, v + 20)), width=r)
        else:
            d.ellipse((x - r, yy - r, x + r, yy + r), fill=(v, v, min(255, v + 20)))
    return img


CARD_CROP = 1760  # source rows above the editor's test ad banner


@functools.lru_cache(maxsize=4)
def round_mask(w, h, radius):
    m = Image.new('L', (w * 2, h * 2), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w * 2 - 1, h * 2 - 1), radius * 2, fill=255)
    return m.resize((w, h), Image.LANCZOS)


@functools.lru_cache(maxsize=4)
def device_glow(w, h, radius, color):
    pad = 70
    g = Image.new('L', (w + pad * 2, h + pad * 2), 0)
    ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + w, pad + h), radius, fill=210)
    g = g.filter(ImageFilter.GaussianBlur(34))
    img = Image.new('RGBA', g.size, color + (0,))
    img.putalpha(g)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((pad - 3, pad - 3, pad + w + 3, pad + h + 3), radius + 3, outline=color + (255,), width=5)
    return img


def device(canvas, frame_img, cx, top, scale, alpha=1.0, color=CYAN, tilt=0.0):
    """The UI shot inside a rounded phone-like card (crops the editor's test banner)."""
    w, h = int(W * scale), int(CARD_CROP * scale)
    shot = juicy(frame_img, 0.35, 1.15, 1.04).crop((0, 0, W, CARD_CROP)).resize((w, h), Image.BILINEAR).convert('RGBA')
    radius = int(48 * scale / 0.78)
    shot.putalpha(round_mask(w, h, radius))
    layer = Image.new('RGBA', (w + 140, h + 140), (0, 0, 0, 0))
    layer.alpha_composite(device_glow(w, h, radius, color))
    layer.alpha_composite(shot, (70, 70))
    if abs(tilt) > 0.05:
        layer = layer.rotate(tilt, Image.BILINEAR, expand=True)
    if alpha < 0.999:
        layer.putalpha(layer.getchannel('A').point(lambda v: int(v * alpha)))
    canvas.alpha_composite(layer, (int(cx - layer.width / 2), int(top + h / 2 - layer.height / 2)))


def to_device(x, y, top=420, scale=0.78):
    return 540 + (x - 540) * scale, top + y * scale


@functools.lru_cache(maxsize=1)
def app_icon(size=380):
    im = Image.open(APP_ICON).convert('RGBA').resize((size, size), Image.LANCZOS)
    im.putalpha(round_mask(size, size, int(size * 0.22)))
    pad = 90
    out = Image.new('RGBA', (size + pad * 2, size + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', out.size, 0)
    ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + size, pad + size), int(size * 0.22), fill=230)
    g = g.filter(ImageFilter.GaussianBlur(40))
    glow = Image.new('RGBA', out.size, CYAN + (0,))
    glow.putalpha(g)
    out.alpha_composite(glow)
    out.alpha_composite(im, (pad, pad))
    return out


@functools.lru_cache(maxsize=4)
def pill(text, color, w=640, h=128):
    pad = 60
    img = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    g = Image.new('L', img.size, 0)
    ImageDraw.Draw(g).rounded_rectangle((pad, pad, pad + w, pad + h), h // 2, fill=220)
    g = g.filter(ImageFilter.GaussianBlur(26))
    glow = Image.new('RGBA', img.size, color + (0,))
    glow.putalpha(g)
    img.alpha_composite(glow)
    # Solid fill: a vertical gradient of the colour, a soft top highlight and white text.
    top = tuple(min(255, int(c * 1.05 + 20)) for c in color)
    bottom = tuple(int(c * 0.72) for c in color)
    grad = Image.new('RGBA', (w, h))
    gd = ImageDraw.Draw(grad)
    for y in range(h):
        k = y / (h - 1)
        gd.line((0, y, w, y), fill=tuple(int(top[i] * (1 - k) + bottom[i] * k) for i in range(3)) + (255,))
    grad.putalpha(round_mask(w, h, h // 2))
    img.alpha_composite(grad, (pad, pad))
    shine = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shine).rounded_rectangle((pad + 8, pad + 6, pad + w - 8, pad + h // 2), h // 2 - 8,
                                            fill=(255, 255, 255, 45))
    img.alpha_composite(shine)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((pad, pad, pad + w, pad + h), h // 2, outline=(255, 235, 190, 255), width=3)
    # Same tracking as the logo, white, with a soft dark drop.
    label_img = txt(text, 60, WHITE, None, BOLD, 0.12, 0, 0, None, w - 60)
    shadow = Image.new('RGBA', label_img.size, (90, 40, 0, 0))
    shadow.putalpha(label_img.getchannel('A').point(lambda v: int(v * 0.6)))
    img.alpha_composite(shadow, (int(pad + w / 2 - label_img.width / 2 + 3), int(pad + h / 2 - label_img.height / 2 + 5)))
    img.alpha_composite(label_img, (int(pad + w / 2 - label_img.width / 2), int(pad + h / 2 - label_img.height / 2 + 2)))
    return img


# ----------------------------------------------------------------------
# Timeline (beats -> seconds)
# ----------------------------------------------------------------------

T_MENU, T_WARP, T_WAVES, T_PICK, T_COMBO, T_SPECIAL = beat(0), beat(4), beat(8), beat(15), beat(22), beat(28)
T_BOSS, T_FIGHT, T_KILL, T_REWARD, T_HANGAR = beat(32), beat(34), beat(39), beat(40), beat(43)
T_UPGRADE, T_DECK, T_END = beat(48), beat(50), beat(52)

T_TAP_PLAY = beat(3.5)
T_TAP_CARD = beat(18)
T_TAP_SPECIAL = beat(29)

# Moments that shake the camera (time, strength).
SHAKES = [(T_WAVES, 1.0), (T_TAP_SPECIAL, 0.8), (T_BOSS, 0.9), (T_REWARD, 1.1), (T_END, 0.9), (beat(60), 0.7),
          (beat(9), 0.25), (beat(23), 0.25), (beat(35), 0.25), (beat(41), 0.25)]

# Cuts and their transitions.
CUTS = [(T_WARP, 'zoom'), (T_WAVES, 'drop'), (T_PICK, 'glitch'), (T_COMBO, 'whip'), (T_SPECIAL, 'stripe'),
        (T_BOSS, 'boss'), (T_REWARD, 'drop'), (T_HANGAR, 'zoom'), (T_UPGRADE, 'whip'), (T_DECK, 'whip'),
        (T_END, 'drop')]


# ----------------------------------------------------------------------
# Sections
# ----------------------------------------------------------------------

def sec_menu(t):
    a = t - T_MENU
    if t < T_TAP_PLAY:
        pos = 42 * a / (T_TAP_PLAY - T_MENU)
    else:
        pos = 42 + (t - T_TAP_PLAY) * 30
    img = source('01_menu_warp', pos)
    scale = 1.2 + 0.05 * ease_in_out(a / 1.8)
    img = juicy(framed(img, scale, 540, 900), 0.6)
    canvas = img.convert('RGBA')
    label(canvas, t, T_MENU + 0.3, T_WARP, '// PILOT ONLINE', 540, 150)
    play_y = 960 + (1278 - 900) * scale
    ripple(canvas, t, T_TAP_PLAY, 540, play_y)
    out = canvas.convert('RGB')
    return Image.blend(Image.new('RGB', (W, H)), out, ease_out_cubic(a / 0.3)) if a < 0.3 else out


def sec_warp(t):
    a = t - T_WARP
    pos = 49 + a / (T_WAVES - T_WARP) * 23
    img = source('01_menu_warp', pos)
    k = a / (T_WAVES - T_WARP)
    img = juicy(framed(img, 1.12 + 0.3 * ease_in_cubic(k), 540, 960), 0.9)
    canvas = img.convert('RGBA')
    speed_lines(canvas, t, 0.35 + 0.65 * k)
    canvas.alpha_composite(band(780, 0.85, 'bottom'), (0, H - 780))
    label(canvas, t, T_WARP + 0.05, T_WAVES, '// HYPERJUMP ENGAGED', 540, 1270)
    headline(canvas, t, beat(5), T_WAVES, 'ONE SHIP', 540, 1390, seed=1)
    hud_line(canvas, t, beat(6), T_WAVES, 'VS THE GALAXY', 540, 1575, 92, CYAN, seed=11)
    return canvas.convert('RGB')


def sec_waves(t):
    a = t - T_WAVES
    img = source('02_gameplay', 140 + a * 30 * 1.4, blur=2, span=1.4)
    scale = (1.16 + 0.12 * ease_in_out(a / (T_PICK - T_WAVES))) * kick_punch(t)
    scale *= 1 + 0.25 * (1 - ease_out_cubic(a / 0.3))
    img = juicy(framed(img, scale, 540, 950))
    canvas = img.convert('RGBA')
    speed_lines(canvas, t, 0.8 * (1 - a / 0.5))
    canvas.alpha_composite(band(740, 0.9, 'bottom'), (0, H - 740))
    canvas.alpha_composite(band(360, 0.7, 'top'), (0, 0))
    label(canvas, t, T_WAVES + 0.12, T_PICK, '// AUTO-AIM  ·  AUTO-FIRE', 540, 200, size=40)
    label(canvas, t, beat(8.5), T_PICK, '// SECTOR 08  ·  WAVE 08', 540, 1280)
    headline(canvas, t, beat(9), T_PICK, 'SURVIVE', 540, 1400, seed=2)
    hud_line(canvas, t, beat(10), T_PICK, 'ENDLESS WAVES', 540, 1585, 92, CYAN, seed=12)
    return canvas.convert('RGB')


def sec_pick(t):
    a = t - T_PICK
    pos = 76 - (T_TAP_CARD - T_PICK) * 30 + a * 30
    img = source('03_levelup', pos)
    img = juicy(framed(img, (1.0 + 0.04 * ease_in_out(a / 3.3)) * kick_punch(t, 0.02)), 0.6)
    canvas = img.convert('RGBA')
    end = T_TAP_CARD + 0.07
    if t < end:
        canvas.alpha_composite(band(440, 0.95, 'top'), (0, 0))
    headline(canvas, t, T_PICK + 0.15, end, 'LEVEL UP', 540, 160, 140, WHITE, CYAN, GOLD, seed=3)
    hud_line(canvas, t, beat(16), end, 'PICK YOUR UPGRADE', 540, 318, 64, CYAN, seed=13)
    ripple(canvas, t, T_TAP_CARD, 540, 1524)
    shockwave(canvas, t, T_TAP_CARD, 540, 1524, CYAN, 0.5)
    if t > T_TAP_CARD + 0.1:
        canvas.alpha_composite(band(560, 0.85, 'bottom'), (0, H - 560))
        label(canvas, t, T_TAP_CARD + 0.1, T_COMBO, '// MODULE INSTALLED', 540, 1460)
        anim(canvas, t, T_TAP_CARD + 0.15, T_COMBO, chip_sprite('BLADE RING', CYAN), 540, 1560, 'pop')
        hud_line(canvas, t, beat(20), T_COMBO, 'STRONGER EVERY LEVEL', 540, 1715, 58, WHITE, CYAN, seed=14)
    out = canvas.convert('RGB')
    return flash(out, 0.55 * (1 - (t - T_TAP_CARD) / 0.18) if t >= T_TAP_CARD else 0, (180, 240, 255))


COMBO_CHIPS = [('VOLTAIC CHAIN', PURPLE), ('BLADE STORM', CYAN), ('HOMING MISSILES', ORANGE), ('BLACK HOLE', (150, 90, 255))]


def sec_combos(t):
    a = t - T_COMBO
    img = source('02b_gameplay', 30 + a * 30 * 2.0, blur=2, span=2.0)
    img = juicy(framed(img, 1.22 * kick_punch(t, 0.05), 540, 950))
    canvas = img.convert('RGBA')
    canvas.alpha_composite(band(580, 0.85, 'top'), (0, 0))
    canvas.alpha_composite(band(680, 0.8, 'bottom'), (0, H - 680))
    headline(canvas, t, T_COMBO + 0.04, T_SPECIAL, 'STACK', 540, 200, seed=4)
    hud_line(canvas, t, beat(23), T_SPECIAL, 'INSANE COMBOS', 540, 385, 92, CYAN, seed=15)
    label(canvas, t, beat(23.5), T_SPECIAL, '// LOADOUT', 70 + 150, 1330, size=36)
    for k, (name, color) in enumerate(COMBO_CHIPS):
        chip = chip_sprite(name, color)
        anim(canvas, t, beat(24 + k), T_SPECIAL, chip, 70 + chip.width / 2, 1410 + k * 112, 'left')
    return canvas.convert('RGB')


def sec_special(t):
    a = t - T_SPECIAL
    pos = 30 - (T_TAP_SPECIAL - T_SPECIAL) * 30 + a * 30
    if t >= T_TAP_SPECIAL:
        pos = max(30.0, pos - 4)  # hit-stop: hold the frame of the tap for 4 frames
    img = source('04_special', pos)
    k = ease_out_cubic((t - T_TAP_SPECIAL - 0.1) / 0.3) if t > T_TAP_SPECIAL + 0.1 else 0.0
    img = juicy(framed(img, (1.0 + 0.34 * k) * kick_punch(t, 0.04), 540, 960 - 10 * k), 0.8 + 0.6 * k)
    canvas = img.convert('RGBA')
    canvas.alpha_composite(band(660, 0.85, 'bottom'), (0, H - 660))
    if t < T_TAP_SPECIAL + 0.1:
        label(canvas, t, T_SPECIAL + 0.02, T_TAP_SPECIAL + 0.1, '// TAP TO ACTIVATE', 700, 1640, ORANGE, 40)
    ripple(canvas, t, T_TAP_SPECIAL, 918, 1775)
    shockwave(canvas, t, T_TAP_SPECIAL + 0.1, 540, 960, (255, 170, 90), 0.9)
    speed_lines(canvas, t, 0.9 * (1 - clamp01((t - T_TAP_SPECIAL - 0.1) / 0.6)) if t > T_TAP_SPECIAL + 0.1 else 0)
    label(canvas, t, T_TAP_SPECIAL + 0.12, T_BOSS, '// OVERDRIVE ONLINE', 540, 1275, ORANGE)
    headline(canvas, t, T_TAP_SPECIAL + 0.12, T_BOSS, 'UNLEASH', 540, 1395, 150, WHITE, ORANGE, ORANGE, seed=5)
    hud_line(canvas, t, beat(30), T_BOSS, 'YOUR SPECIAL', 540, 1580, 92, (255, 170, 110), ORANGE, ORANGE, seed=16)
    out = canvas.convert('RGB')
    return flash(out, 0.8 * (1 - (t - T_TAP_SPECIAL) / 0.2) if t >= T_TAP_SPECIAL else 0, (255, 160, 90))


def sec_boss(t):
    if t < T_FIGHT:
        a = t - T_BOSS
        img = source('05_boss', 12 + a * 30)
        img = juicy(framed(img, 1.06 + 0.05 * a, 540, 900), 0.8)
        img = edge_glow(img, RED, 200 * (0.5 + 0.5 * math.cos(a * 2 * math.pi / BEAT)))
        canvas = img.convert('RGBA')
        canvas.alpha_composite(band(600, 0.85, 'bottom'), (0, H - 600))
        label(canvas, t, T_BOSS + 0.02, T_FIGHT + 0.02, '!! WARNING  ·  ABERRANT CORE', 540, 1395, RED, 38)
        blink = (a / (BEAT / 2)) % 2 < 1.4
        if blink:
            headline(canvas, t, T_BOSS + 0.04, T_FIGHT + 0.02, 'BOSS INCOMING', 540, 1515, 112, WHITE, RED, RED, seed=6)
        return canvas.convert('RGB')
    if t < T_KILL:
        a = t - T_FIGHT
        pos = 230 + a / (T_KILL - T_FIGHT) * 218
        img = source('05_boss', pos, blur=3, span=3.0)
        img = juicy(framed(img, 1.12 * (1 + 0.035 * math.exp(-since_beat(t) * 9)), 540, 1080))
    else:
        a = t - T_KILL
        pos = 448 + min(4.5, a * 30 * 0.32)
        img = source('05_boss', pos)
        img = juicy(framed(img, 1.12 + 0.35 * ease_in_cubic(a / BEAT), 540, 1130), 1.0 + a * 2)
        img = ImageEnhance.Color(img).enhance(1 - 0.5 * a / BEAT)
    canvas = img.convert('RGBA')
    canvas.alpha_composite(band(560, 0.9, 'top'), (0, 0))
    headline(canvas, t, T_FIGHT + 0.03, T_KILL + 0.5, 'CRUSH', 540, 195, seed=7)
    hud_line(canvas, t, beat(35), T_KILL + 0.5, 'GIANT BOSSES', 540, 380, 92, CYAN, seed=17)
    return canvas.convert('RGB')


@functools.lru_cache(maxsize=1)
def coin_burst():
    rnd = random.Random(5)
    return [(rnd.uniform(0, 2 * math.pi), rnd.uniform(500, 1400), rnd.uniform(16, 34), rnd.uniform(-6, 6), rnd.uniform(0, 0.1))
            for _ in range(60)]


def sec_reward(t):
    a = t - T_REWARD
    img = source('05_boss', 458 + a * 30)
    img = juicy(framed(img, (1.1 - 0.07 * ease_out_cubic(a / 1.4)) * kick_punch(t, 0.03), 540, 760), 0.7)
    canvas = img.convert('RGBA')
    canvas.alpha_composite(band(640, 0.97, 'bottom_hard'), (0, H - 640))
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for ang, speed, size, spin, delay in coin_burst():
        b = a - delay
        if b <= 0 or b > 1.3:
            continue
        r = speed * ease_out_cubic(b / 0.9)
        x = 540 + math.cos(ang) * r
        y = 760 + math.sin(ang) * r * 0.8 + 420 * b * b
        s = size * (1 - 0.4 * clamp01(b / 1.3))
        rot = spin * b
        pts = [(x + s * math.cos(rot + q * math.pi / 2) * (1 if q % 2 == 0 else 0.62),
                y + s * math.sin(rot + q * math.pi / 2) * (1 if q % 2 == 0 else 0.62)) for q in range(4)]
        alpha = int(255 * (1 - clamp01((b - 0.9) / 0.4)))
        d.polygon(pts, fill=GOLD + (alpha,), outline=(255, 240, 180, alpha))
    canvas.alpha_composite(layer)
    value = int(635 * ease_out_cubic((a - 0.1) / 0.7)) if a > 0.1 else 0
    label(canvas, t, T_REWARD + 0.05, T_HANGAR, '// MISSION COMPLETE', 540, 1565, GOLD)
    plate(canvas, t, T_REWARD + 0.08, T_HANGAR, 540, 1680, 860, 150, GOLD)
    put(canvas, txt('+%d CREDITS' % value, 104, GOLD, ORANGE, BOLD, 0.14, 3, 0.7, None, 820), 540, 1680,
        1.0 + 0.25 * (1 - ease_out_back((a - 0.08) / 0.25)) if a > 0.08 else 0.0, clamp01((a - 0.08) / 0.06))
    hud_line(canvas, t, beat(41), T_HANGAR, 'SPEND THEM ON UPGRADES', 540, 1822, 46, WHITE, CYAN, seed=18)
    return canvas.convert('RGB')


def sec_hangar(t):
    a = t - T_HANGAR
    shot = source('06_hangar', 24 + a * 30 * 1.94, blur=2, span=1.94)
    canvas = space(t, 90).convert('RGBA')
    k = ease_out_cubic(a / 0.3)
    device(canvas, shot, 540, 420 + 600 * (1 - k), 0.78 * kick_punch(t, 0.015), clamp01(a / 0.1), CYAN, 8 * (1 - k))
    for tap in (30, 63, 96, 129):
        t_tap = T_HANGAR + (tap - 24) / (30 * 1.94)
        x, y = to_device(1004, 979)
        ripple(canvas, t, t_tap, x, y)
    headline(canvas, t, T_HANGAR + 0.05, T_UPGRADE, 'CHOOSE', 540, 160, seed=8)
    hud_line(canvas, t, beat(44), T_UPGRADE, 'YOUR SHIP', 540, 325, 84, CYAN, seed=19)
    return canvas.convert('RGB')


def sec_meta(t):
    canvas = space(t, 90).convert('RGBA')
    if t < T_DECK:
        a = t - T_UPGRADE
        shot = source('07_upgrades', 22 + a * 30 * 1.28)
        k = ease_out_cubic(a / 0.22)
        device(canvas, shot, 540 + 900 * (1 - k), 420, 0.78, 1.0, GOLD, -6 * (1 - k))
        headline(canvas, t, T_UPGRADE + 0.03, T_DECK, 'UPGRADE', 540, 160, 150, WHITE, CYAN, GOLD, seed=9)
        hud_line(canvas, t, beat(49), T_DECK, 'YOUR SHIP', 540, 325, 84, GOLD, ORANGE, GOLD, seed=20)
        for tap in (30, 54):
            x, y = to_device(200, 1680)
            ripple(canvas, t, T_UPGRADE + (tap - 22) / (30 * 1.28), x, y)
    else:
        a = t - T_DECK
        shot = source('08_basedeck', 26 + a * 30 * 2.0)
        k = ease_out_cubic(a / 0.22)
        device(canvas, shot, 540 + 900 * (1 - k), 420, 0.78, 1.0, PURPLE, -6 * (1 - k))
        headline(canvas, t, T_DECK + 0.03, T_END, 'UNLOCK', 540, 160, 150, WHITE, PURPLE, PURPLE, seed=10)
        hud_line(canvas, t, beat(51), T_END, 'NEW CARDS', 540, 325, 84, (215, 175, 255), PURPLE, PURPLE, seed=21)
        x, y = to_device(540, 1675)
        ripple(canvas, t, T_DECK + (30 - 26) / 60.0, x, y)
    return canvas.convert('RGB')


def sec_end(t):
    a = t - T_END
    canvas = space(t, 140.0, 0.6 * (1 - clamp01(a / 0.8))).convert('RGBA')
    glow = Image.new('L', (W // 8, H // 8), 0)
    r = (420 + 40 * math.exp(-since_beat(t) * 6)) / 8
    ImageDraw.Draw(glow).ellipse((540 / 8 - r, 640 / 8 - r, 540 / 8 + r, 640 / 8 + r), fill=120)
    glow = glow.filter(ImageFilter.GaussianBlur(14)).resize((W, H), Image.BILINEAR)
    halo = Image.new('RGBA', (W, H), (40, 120, 200, 0))
    halo.putalpha(glow)
    canvas.alpha_composite(halo)

    pulse = 1 + 0.03 * math.exp(-since_beat(t) * 8)
    put(canvas, app_icon(), 540, 620, (0.2 + 0.8 * ease_out_back(a / 0.35)) * pulse, clamp01(a / 0.08))
    shockwave(canvas, t, T_END + 0.02, 540, 620, CYAN, 1.0)

    # Title like the menu logo: Rajdhani Bold, 0.14 em tracking, the cyan lens flare under it.
    title = 'ASTRO BLITZ'
    size = fit_size(title, 150, DISPLAY_EM, DISPLAY, 960)
    f = font(DISPLAY, size)
    tracking = int(size * DISPLAY_EM)
    widths = [f.getlength(c) for c in title]
    total = sum(widths) + tracking * (len(title) - 1)
    tech_rule(canvas, t, beat(53) + 0.1, 540, 1020, 900, CYAN)
    x = 540 - total / 2
    for i, c in enumerate(title):
        t_in = beat(53) + i * 0.035
        if c != ' ' and t >= t_in:
            anim(canvas, t, t_in, 99, txt(c, size, WHITE, CYAN, DISPLAY, 0.0, 4, 0.85), x + widths[i] / 2, 940, 'slam')
        x += widths[i] + tracking
    sparks(canvas, t, beat(53) + 0.3, 540, 940, CYAN, 22, 520, seed=8)
    if t >= beat(54):
        shown = decode('SPACE ROGUELITE SHOOTER', t, beat(54), 0.4, 30)
        put(canvas, txt('SPACE ROGUELITE SHOOTER', 50, (170, 230, 250), CYAN, SEMI, 0.3, 2, 0.45, shown, 1000),
            540, 1080, 1.0, clamp01((t - beat(54)) / 0.06))

    if t >= beat(56):
        b = pill('PLAY FREE NOW', (255, 140, 30))
        put(canvas, b, 540, 1270, (0.3 + 0.7 * ease_out_back((t - beat(56)) / 0.3)) * pulse, clamp01((t - beat(56)) / 0.08))
    label(canvas, t, beat(57), 99, 'FREE TO PLAY' if IOS else 'FREE TO PLAY  ·  ANDROID & iOS', 540, 1440,
          (150, 190, 215), 38)
    out = canvas.convert('RGB')
    if t > DURATION - 0.3:
        out = Image.blend(out, Image.new('RGB', (W, H), (0, 0, 0)), clamp01((t - (DURATION - 0.3)) / 0.3))
    return out


SECTIONS = [
    (T_MENU, T_WARP, sec_menu), (T_WARP, T_WAVES, sec_warp), (T_WAVES, T_PICK, sec_waves),
    (T_PICK, T_COMBO, sec_pick), (T_COMBO, T_SPECIAL, sec_combos), (T_SPECIAL, T_BOSS, sec_special),
    (T_BOSS, T_REWARD, sec_boss), (T_REWARD, T_HANGAR, sec_reward), (T_HANGAR, T_UPGRADE, sec_hangar),
    (T_UPGRADE, T_END, sec_meta), (T_END, DURATION + 1, sec_end),
]


def section_at(t):
    for i, (start, end, fn) in enumerate(SECTIONS):
        if start <= t < end:
            return i
    return len(SECTIONS) - 1


# ----------------------------------------------------------------------
# Transitions and global juice
# ----------------------------------------------------------------------

F = 1.0 / FPS


def transition(img, t, index):
    for cut, kind in CUTS:
        d = t - cut
        if kind in ('zoom',):
            if -4 * F <= d < 0:
                k = (d + 4 * F) / (4 * F)
                img = flash(zoom_blur(img, 0.5 * k), 0.45 * k)
            elif 0 <= d < 6 * F:
                k = 1 - d / (6 * F)
                img = flash(zoom_blur(img, 0.3 * k), 0.6 * k)
        elif kind == 'drop':
            if -7 * F <= d < 0:
                k = (d + 7 * F) / (7 * F)
                img = flash(zoom_blur(img, 0.6 * ease_in_cubic(k)), ease_in_cubic(k))
            elif 0 <= d < 10 * F:
                k = 1 - d / (10 * F)
                img = flash(img, ease_in_cubic(k) * 1.0)
        elif kind in ('glitch', 'boss'):
            if -4 * F <= d < 5 * F:
                k = 1 - abs(d) / (5 * F)
                img = glitch(img, k, index)
                if kind == 'boss' and d >= 0:
                    img = flash(img, 0.6 * k, (255, 50, 70))
        elif kind == 'whip':
            if -4 * F <= d < 0:
                k = ease_in_cubic((d + 4 * F) / (4 * F))
                img = whip(img, -W * 0.7 * k, 260 * k)
            elif 0 <= d < 4 * F:
                k = 1 - ease_out_cubic(d / (4 * F))
                img = whip(img, W * 0.7 * k, 260 * k)
        elif kind == 'stripe':
            if -6 * F <= d < 4 * F:
                k = (d + 6 * F) / (10 * F)
                prev = SECTIONS[section_at(cut - F)][2](t)
                nxt = SECTIONS[section_at(cut)][2](t)
                img = stripe_wipe(prev, nxt, ease_in_out(k))
    return img


def stripe_wipe(a, b, k):
    yy, xx = np.mgrid[0:H // 4, 0:W // 4]
    diag = (xx * 4 + yy * 4 * 0.5) / (W + H * 0.5)
    bands = ((yy * 4) // 240) % 3
    front = k * 1.45 - bands * 0.12
    m = (diag < front).astype(np.uint8) * 255
    mask = Image.fromarray(m).resize((W, H), Image.NEAREST)
    edge = Image.fromarray(((np.abs(diag - front) < 0.02) * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)
    out = Image.composite(b, a, mask)
    return Image.composite(Image.new('RGB', (W, H), CYAN), out, edge)


def post(img, t):
    # Kick pulse: brightness, chromatic split and an edge glow on every beat of the drops.
    if in_drop(t) and t < T_END:
        p = math.exp(-since_beat(t) * 10.0)
        img = ImageEnhance.Brightness(img).enhance(1 + 0.1 * p)
        img = rgb_split(img, 5 * p)
        img = edge_glow(img, CYAN if t < T_BOSS else GOLD, 90 * p)
    # Sci-fi monitor: corner brackets and faint scanlines until the end card.
    if t < T_END:
        canvas = img.convert('RGBA')
        canvas.alpha_composite(hud_frame())
        img = canvas.convert('RGB')
    # Big moments: light sweeps.
    for t0 in (T_WAVES, T_REWARD, T_END + 0.1):
        img = light_sweep(img, t, t0)
    # Camera shake (scaled a touch so the edges never show).
    dx, dy = shake_at(t, SHAKES)
    if abs(dx) + abs(dy) > 0.5:
        img = framed(img, 1.04, dx=dx, dy=dy)
    return img


def render(index):
    t = index / FPS
    i = section_at(t)
    img = SECTIONS[i][2](t)
    canvas = img.convert('RGBA')
    for t0 in (T_WAVES, T_REWARD):
        shockwave(canvas, t, t0, 540, 960, WHITE, 1.1)
    img = canvas.convert('RGB')
    img = transition(img, t, index)
    img = post(img, t)
    img.save(os.path.join(FRAMES, '%05d.jpg' % index), quality=95, subsampling=0)
    return index


# ----------------------------------------------------------------------
# Audio: the original music plus a few of the game's own UI sounds
# ----------------------------------------------------------------------

def sfx_events():
    p, u = os.path.join(AUDIO, 'Partida'), os.path.join(AUDIO, 'UI')
    ev = [
        (T_TAP_PLAY, os.path.join(u, 'Click_Mid-High.wav'), 0.7),
        (T_TAP_CARD, os.path.join(u, 'Click_Mid-High.wav'), 0.7),
        (T_TAP_CARD + 0.03, os.path.join(u, 'Tone1B_MajorTriadUp.wav'), 0.5),
        (T_PICK + 0.15, os.path.join(u, 'Tone1B_OctaveUp.wav'), 0.4),
        (T_TAP_SPECIAL, os.path.join(u, 'Click_Mid-High.wav'), 0.7),
        (T_TAP_SPECIAL + 0.02, os.path.join(p, 'Tone1A_OctaveUp.wav'), 0.45),
        (T_REWARD + 0.1, os.path.join(p, 'Tone1A_MajorTriadUp.wav'), 0.5),
        (T_UPGRADE, os.path.join(p, 'Click_Combo_5.wav'), 0.5),
        (T_DECK + 0.07, os.path.join(u, 'Tone1C_FifthUp.wav'), 0.45),
    ]
    for k in range(len(COMBO_CHIPS)):
        ev.append((beat(24 + k), os.path.join(u, 'Click_Combo_%d_High.wav' % min(4, k + 2)), 0.45))
    for tap in (30, 63, 96, 129):
        ev.append((T_HANGAR + (tap - 24) / (30 * 1.94), os.path.join(u, 'Click_Pitched_Down.wav'), 0.4))
    return ev


def encode():
    events = sfx_events()
    cmd = ['ffmpeg', '-y', '-v', 'error', '-framerate', str(FPS), '-i', os.path.join(FRAMES, '%05d.jpg'),
           '-i', MUSIC]
    for _, path, _ in events:
        cmd += ['-i', path]
    parts = ['[1:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:%.2f[m]' % DURATION]
    labels = ['[m]']
    for k, (at, _, vol) in enumerate(events):
        ms = int(max(0, at) * 1000)
        parts.append('[%d:a]aformat=sample_rates=48000:channel_layouts=stereo,volume=%.2f,adelay=%d|%d[s%d]'
                     % (k + 2, vol, ms, ms, k))
        labels.append('[s%d]' % k)
    parts.append('%samix=inputs=%d:normalize=0:duration=first,alimiter=limit=0.85:level=0,'
                 'loudnorm=I=-14:TP=-1.5:LRA=11,aresample=48000,alimiter=limit=0.79:level=0[a]'
                 % (''.join(labels), len(labels)))
    cmd += ['-filter_complex', ';'.join(parts), '-map', '0:v', '-map', '[a]',
            '-map_chapters', '-1', '-map_metadata', '-1', '-dn',
            '-c:v', 'libx264', '-preset', 'slow', '-crf', '16', '-pix_fmt', 'yuv420p', '-profile:v', 'high',
            '-r', str(FPS), '-c:a', 'aac', '-b:a', '192k', '-t', '%.2f' % DURATION, '-movflags', '+faststart', OUTPUT]
    subprocess.run(cmd, check=True)


def main():
    os.makedirs(FRAMES, exist_ok=True)
    if '--preview' in sys.argv:
        times = [float(v) for v in sys.argv[sys.argv.index('--preview') + 1:]]
        for t in times:
            index = int(round(t * FPS))
            render(index)
            os.replace(os.path.join(FRAMES, '%05d.jpg' % index), os.path.join(BUILD, 'preview_%05.2f.jpg' % t))
        return
    global MUSIC
    if '--music' in sys.argv:
        # A licensed track instead of the synthesised one (30 s; the cuts follow 128 BPM).
        MUSIC = os.path.abspath(sys.argv[sys.argv.index('--music') + 1])
    if '--encode-only' not in sys.argv:
        if '--music' not in sys.argv:
            subprocess.run([sys.executable, os.path.join(HERE, 'compose_music.py')], check=True)
        with Pool(min(14, os.cpu_count() or 4)) as pool:
            for n, _ in enumerate(pool.imap_unordered(render, range(TOTAL), chunksize=4)):
                if n % 150 == 0:
                    print('frames', n, '/', TOTAL, flush=True)
    encode()
    print('wrote', OUTPUT)


if __name__ == '__main__':
    main()
