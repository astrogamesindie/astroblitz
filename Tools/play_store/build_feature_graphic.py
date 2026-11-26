# Astro Blitz - Google Play feature graphic ("recurso gráfico", 1024 x 500), also the site banner.
#
# Rebuilds the graphic in the look of the first one (2026-10-22, made without a script in the
# project): space gradient with small stars, the AppIcon ship on the right and the glowing
# Rajdhani title on the left. Gradient, stars, glow, colours, sizes and positions were measured
# on that first graphic.
#
# Writes (24-bit PNG, no alpha):
#   PlayConsole/recurso_grafico_1024x500.png         default graphic, English subtitle
#   PlayConsole/recurso_grafico_1024x500_pt-BR.png   Portuguese subtitle, for the pt-BR listing
# Tools/build_site.py copies the default graphic to Site/banner.png.
#
# Run: python Tools/play_store/build_feature_graphic.py

import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
FONT_DIR = os.path.join(ROOT, 'Assets', 'AstroCrawler', 'Resources', 'AstroCrawler', 'Art')
ICON = os.path.join(ROOT, 'Assets', 'AstroCrawler', 'Art', 'AppIcon.png')
OUT_DIR = os.path.join(ROOT, 'PlayConsole')
W, H = 1024, 500

TITLE = 'ASTRO BLITZ'
# Output file -> subtitle. The subtitle follows the store title ("Astro Blitz: Space Defense",
# "Astro Blitz: Defesa Espacial"); the first graphic said "Space Roguelite Shooter".
VARIANTS = {
    'recurso_grafico_1024x500.png': 'SPACE DEFENSE ROGUELITE',
    'recurso_grafico_1024x500_pt-BR.png': 'ROGUELITE DE DEFESA ESPACIAL',
}

# Background: bilinear gradient between the four corners plus soft glows.
CORNERS = ((17, 13, 41), (8, 17, 33), (27, 47, 85), (16, 20, 43))  # top-left, top-right, bottom-left, bottom-right
GLOWS = (  # centre, radii, RGB added at the centre (negative darkens)
    ((450, 500), (180, 250), (3, 34, 39)),     # teal light from below
    ((200, 50), (130, 180), (56, 21, 91)),     # violet nebula, top left...
    ((200, 50), (80, 130), (-24, -7, -37)),    # ...with a softer core
    ((600, -50), (130, 250), (9, 35, 36)),     # teal haze at the top
    ((50, 450), (180, 130), (15, -7, 20)),     # violet, bottom left
)

# Stars: 1-px crosses and small dots, kept off the ship.
STAR_SEED = 20261112
STAR_COUNT = 120
STAR_TINTS = ((255, 255, 255), (205, 228, 255), (170, 220, 245), (230, 190, 215))

# Ship: cut out of the 512-px AppIcon (its cyan glow decides what is kept) and laid over the
# background with a "lighten" blend. The icon's bottom edge sits on the graphic's bottom edge,
# so the flames run off it as they do in the icon (tip about 40 px below the top).
SHIP_SCALE = 1.06
SHIP_CENTER_X = 848
ICON_SHIP_PIXEL = (256, 256)  # a pixel inside the ship in AppIcon.png

# Text block (ink box positions in px).
TITLE_FONT, TITLE_SIZE, TITLE_TRACK = 'Rajdhani-Bold.ttf', 119, 0.5
TITLE_MAX_W = 615          # a wider title shrinks below TITLE_SIZE, to stay clear of the ship's wing
TITLE_LEFT, TITLE_BOTTOM = 65, 261.75  # the baseline stays put when the title shrinks
TITLE_COLOR = (235, 252, 255)
GLOW_LAYERS = (  # blur radius, RGB added where the blurred title is fully on (clipped at 255)
    (16, (61, 273, 253)),
    (36, (32, 169, 244)),
)
BAR = (68, 284, 179, 290)  # left, top, right, bottom (right and bottom exclusive)
BAR_COLOR = (60, 230, 255)
SUB_FONT, SUB_SIZE = 'Rajdhani-SemiBold.ttf', 38
SUB_LEFT, SUB_TOP = 68, 317
SUB_MAX_W = 560
SUB_COLOR = (170, 225, 240)


def font(name, size):
    return ImageFont.truetype(os.path.join(FONT_DIR, name), size)


def ink_box(f, text, track=0.0):
    """Ink box of text set glyph by glyph from x = 0, without kerning, like the first graphic."""
    x, box = 0.0, None
    for ch in text:
        if ch != ' ':
            l, t, r, b = f.getbbox(ch)
            if box is None:
                box = [x + l, t, x + r, b]
            else:
                box = [box[0], min(box[1], t), x + r, max(box[3], b)]
        x += f.getlength(ch) + track
    return box


def fit_size(text, face, size, max_w, track=0.0):
    while size > 20:
        x0, _, x1, _ = ink_box(font(face, size), text, track)
        if x1 - x0 <= max_w:
            return size
        size -= 1
    raise ValueError('text does not fit: %r' % text)


def text_mask(text, face, size, left, top=None, bottom=None, track=0.0, k=1):
    """Coverage (0..1) of the text with its ink box at `left` and `top` (or `bottom`).
    k > 1 draws it k times larger and averages it down, for smoother edges."""
    f = font(face, size * k)
    x0, y0, _, y1 = ink_box(f, text, track * k)
    y = top * k - y0 if bottom is None else bottom * k - y1
    mask = Image.new('L', (W * k, H * k), 0)
    draw = ImageDraw.Draw(mask)
    x = left * k - x0
    for ch in text:
        draw.text((round(x), round(y)), ch, font=f, fill=255)
        x += f.getlength(ch) + track * k
    if k > 1:
        mask = mask.resize((W, H), Image.BOX)
    return np.asarray(mask, np.float64) / 255.0


def blur(mask, radius):
    im = Image.fromarray(np.uint8(np.clip(mask * 255.0 + 0.5, 0, 255)), 'L')
    return np.asarray(im.filter(ImageFilter.GaussianBlur(radius)), np.float64) / 255.0


def background():
    y, x = np.mgrid[0:H, 0:W].astype(np.float64)
    u, v = (x / (W - 1))[..., None], (y / (H - 1))[..., None]
    tl, tr, bl, br = (np.array(c, np.float64) for c in CORNERS)
    img = (1 - u) * (1 - v) * tl + u * (1 - v) * tr + (1 - u) * v * bl + u * v * br
    for (cx, cy), (rx, ry), add in GLOWS:
        g = np.exp(-(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2))
        img += g[..., None] * np.array(add, np.float64)
    return img


def part_containing(mask, seed):
    """The connected part of a 0/255 mask that contains the seed pixel."""
    assert mask.getpixel(seed) == 255, 'seed pixel outside the mask'
    part = Image.new('L', mask.size, 0)
    part.putpixel(seed, 255)
    while True:
        grown = ImageChops.darker(part.filter(ImageFilter.MaxFilter(3)), mask)
        if ImageChops.difference(grown, part).getbbox() is None:
            return grown
        part = grown


def ship_layer():
    """The ship on a black canvas (RGB) and where it sits (0..1), both W x H."""
    icon = np.asarray(Image.open(ICON).convert('RGB'), np.float64)
    r, g, b = icon[..., 0], icon[..., 1], icon[..., 2]
    # The ship is cyan/teal with blue >= green; navy, violet and green nebulae and white stars are
    # not. Only the cyan part joined to the ship is kept (bluish stars and a teal wisp are not).
    cyan = np.clip((np.minimum(g, b) - r - 15) / 90.0, 0, 1) * (b >= 0.85 * g)
    zone = part_containing(Image.fromarray(np.uint8((cyan > 0.35) * 255)), ICON_SHIP_PIXEL)
    zone = zone.filter(ImageFilter.MaxFilter(21))
    zone = np.asarray(zone.filter(ImageFilter.GaussianBlur(4)), np.float64) / 255.0
    # Around the ship, keep its cyan glow but not the nebula tints of the icon's background:
    # green is capped at blue (the glow stays cyan) and violet/navy pixels fade out.
    keep = np.clip((np.minimum(g, b) - r - 5) / 30.0, 0, 1)
    icon = np.stack([r, np.minimum(g, b), b], axis=-1)
    ship = np.uint8(np.clip(icon * (zone * keep)[..., None] + 0.5, 0, 255))
    size = (round(ship.shape[1] * SHIP_SCALE), round(ship.shape[0] * SHIP_SCALE))
    ship = np.asarray(Image.fromarray(ship, 'RGB').resize(size, Image.LANCZOS), np.float64)
    zone = np.asarray(Image.fromarray(np.uint8(zone * 255 + 0.5)).resize(size, Image.LANCZOS), np.float64) / 255.0
    left = round(SHIP_CENTER_X - size[0] / 2)
    top = H - size[1]
    assert top <= 0, 'the ship must fill the height'
    x0, x1 = max(left, 0), min(left + size[0], W)  # the icon's empty sides may stick out
    zone = zone[-top:H - top]
    assert zone[:, :x0 - left].max(initial=0) < 0.02 and zone[:, x1 - left:].max(initial=0) < 0.02, \
        'the ship and its glow must fit the width'
    rgb, where = np.zeros((H, W, 3)), np.zeros((H, W))
    rgb[:, x0:x1] = ship[-top:H - top, x0 - left:x1 - left]
    where[:, x0:x1] = zone[:, x0 - left:x1 - left]
    return rgb, np.clip(where, 0, 1)


def add_stars(img, keep_out):
    rng = random.Random(STAR_SEED)
    k = 4  # drawn 4x larger, then averaged down for soft edges
    masks = [Image.new('L', (W * k, H * k), 0) for _ in STAR_TINTS]
    draws = [ImageDraw.Draw(m) for m in masks]
    for _ in range(STAR_COUNT):
        x, y = rng.uniform(4, W - 4), rng.uniform(4, H - 4)
        draw = draws[rng.randrange(len(STAR_TINTS))]
        a = int(255 * rng.uniform(0.3, 0.95))
        if rng.random() < 0.5:  # 1-px cross
            cx, cy = int(x) * k, int(y) * k
            draw.rectangle((cx - k, cy, cx + 2 * k - 1, cy + k - 1), fill=a)
            draw.rectangle((cx, cy - k, cx + k - 1, cy + 2 * k - 1), fill=a)
        else:  # small dot
            r = rng.uniform(1.8, 2.8) * k
            draw.ellipse((x * k - r, y * k - r, x * k + r, y * k + r), fill=a)
    for tint, m in zip(STAR_TINTS, masks):
        a = np.asarray(m.resize((W, H), Image.BOX), np.float64) / 255.0
        a = (a * (1 - keep_out))[..., None]
        img *= 1 - a
        img += a * np.array(tint, np.float64)


def compose(subtitle, title=TITLE):
    ship, ship_zone = ship_layer()
    img = background()
    add_stars(img, ship_zone)
    img = np.maximum(img, ship)

    size = fit_size(title, TITLE_FONT, TITLE_SIZE, TITLE_MAX_W, TITLE_TRACK)
    m = text_mask(title, TITLE_FONT, size, TITLE_LEFT, bottom=TITLE_BOTTOM, track=TITLE_TRACK, k=4)
    for radius, rgb in GLOW_LAYERS:
        img += blur(m, radius)[..., None] * np.array(rgb, np.float64)
    img = np.clip(img, 0, 255)
    img = img * (1 - m[..., None]) + m[..., None] * np.array(TITLE_COLOR, np.float64)

    l, t, r, b = BAR
    img[t:b, l:r] = BAR_COLOR

    if fit_size(subtitle, SUB_FONT, SUB_SIZE, SUB_MAX_W) != SUB_SIZE:
        raise ValueError('subtitle wider than %d px: %r' % (SUB_MAX_W, subtitle))
    s = text_mask(subtitle, SUB_FONT, SUB_SIZE, SUB_LEFT, top=SUB_TOP)
    img = img * (1 - s[..., None]) + s[..., None] * np.array(SUB_COLOR, np.float64)
    return Image.fromarray(np.uint8(np.clip(img + 0.5, 0, 255)), 'RGB'), size


def main():
    for name, subtitle in VARIANTS.items():
        im, title_size = compose(subtitle)
        path = os.path.join(OUT_DIR, name)
        im.save(path, optimize=True)
        print('%s  %dx%d %s  title %d px  %d KB' % (os.path.relpath(path, ROOT), im.width, im.height, im.mode,
                                                   title_size, os.path.getsize(path) // 1024))


if __name__ == '__main__':
    main()
