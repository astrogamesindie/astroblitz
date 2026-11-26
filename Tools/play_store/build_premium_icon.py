"""Gera o ícone do produto Premium (`primium`) para Play Console e App Store.

Saída: PlayConsole/premium/premium_1024.png e premium_512.png
(PNG 32 bits, opaco, 1:1, sem texto). A coroa usa a mesma geometria de
`SpriteFactory.Premium` (coroa do botão e da janela Premium no jogo), dentro
do hexágono dourado usado nos ícones das naves.

Uso, a partir da raiz do projeto:  python Tools/play_store/build_premium_icon.py
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "PlayConsole", "premium")
S = 2048  # desenho em 2x, reduzido no fim
C = S / 2

GOLD = (255, 205, 96)
GOLD_LIGHT = (255, 240, 190)
GOLD_DEEP = (150, 96, 22)
CYAN = (90, 230, 255)

# Contorno da coroa de SpriteFactory.Premium (coordenadas -1..1, y para cima).
CROWN = [(-0.64, 0.4), (-0.46, -0.3), (0.46, -0.3), (0.64, 0.4), (0.27, 0.13),
         (0.0, 0.62), (-0.27, 0.13)]
BASE = [(-0.43, -0.49), (0.43, -0.49)]
JEWEL_Y = -0.06
CROWN_SCALE = 560
CROWN_DY = 40  # leve descida para centrar visualmente no hexágono


def px(p):
    return (C + p[0] * CROWN_SCALE, C - p[1] * CROWN_SCALE + CROWN_DY)


def background():
    img = Image.new("RGB", (S, S))
    d = ImageDraw.Draw(img)
    for r in range(int(S * 0.75), 0, -4):
        t = r / (S * 0.75)
        col = tuple(int(a + (b - a) * t) for a, b in zip((44, 30, 26), (7, 7, 14)))
        d.ellipse((C - r, C - r, C + r, C + r), fill=col)
    # raios dourados suaves
    rays = Image.new("L", (S, S), 0)
    rd = ImageDraw.Draw(rays)
    for i in range(18):
        a = i * math.tau / 18 + 0.09
        w = 0.05
        pts = [(C, C)] + [(C + math.cos(a + s) * S, C + math.sin(a + s) * S) for s in (-w, w)]
        rd.polygon(pts, fill=34)
    rays = rays.filter(ImageFilter.GaussianBlur(26))
    img = Image.composite(Image.new("RGB", (S, S), GOLD), img, rays)
    # estrelas
    rnd = random.Random(7)
    d = ImageDraw.Draw(img)
    for _ in range(170):
        x, y = rnd.uniform(0, S), rnd.uniform(0, S)
        r = rnd.choice((2, 2, 3, 3, 4, 5))
        v = rnd.randint(120, 235)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(v, v, min(255, v + 15)))
    return img


def glow(img, mask, color, radius, strength):
    blurred = mask.filter(ImageFilter.GaussianBlur(radius))
    blurred = blurred.point(lambda v: min(255, int(v * strength)))
    return Image.composite(Image.new("RGB", (S, S), color), img, blurred)


def hexagon(r):
    return [(C + r * math.cos(math.radians(90 + 60 * i)), C - r * math.sin(math.radians(90 + 60 * i)))
            for i in range(6)]


def stroke_mask(paths, width):
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    for pts, closed in paths:
        seq = pts + [pts[0]] if closed else pts
        d.line(seq, fill=255, width=width, joint="curve")
        for p in seq:
            d.ellipse((p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2), fill=255)
    return m


def main():
    img = background()

    # hexágono: interior escuro e borda dourada
    hx = hexagon(720)
    inner = Image.new("L", (S, S), 0)
    ImageDraw.Draw(inner).polygon(hx, fill=255)
    img = Image.composite(Image.new("RGB", (S, S), (16, 13, 16)), img, inner.point(lambda v: v * 0.86))
    hex_line = stroke_mask([(hx, True)], 16)
    img = glow(img, hex_line, GOLD, 22, 1.2)
    img = Image.composite(Image.new("RGB", (S, S), (214, 160, 66)), img, hex_line)

    crown = [px(p) for p in CROWN]
    base = [px(p) for p in BASE]

    # halo atrás da coroa
    halo = Image.new("L", (S, S), 0)
    ImageDraw.Draw(halo).ellipse((C - 470, C - 430 + CROWN_DY, C + 470, C + 430 + CROWN_DY), fill=150)
    img = glow(img, halo, (255, 190, 80), 150, 1.0)

    # preenchimento da coroa com degradê dourado (claro em cima)
    fill_mask = Image.new("L", (S, S), 0)
    fd = ImageDraw.Draw(fill_mask)
    fd.polygon(crown, fill=255)
    fd.rectangle((base[0][0], base[0][1] - 26, base[1][0], base[1][1] + 26), fill=255)
    grad = Image.new("RGB", (S, S))
    gd = ImageDraw.Draw(grad)
    top, bottom = int(px((0, 0.62))[1]), int(base[0][1] + 30)
    for y in range(S):
        t = min(1, max(0, (y - top) / max(1, bottom - top)))
        gd.line((0, y, S, y), fill=tuple(int(a + (b - a) * t) for a, b in zip((240, 186, 80), GOLD_DEEP)))
    img = Image.composite(grad, img, fill_mask)

    # contorno brilhante (mesmo traço do ícone do jogo)
    outline = stroke_mask([(crown, True), (base, False)], 42)
    img = glow(img, outline, GOLD, 40, 1.6)
    img = Image.composite(Image.new("RGB", (S, S), GOLD_LIGHT), img, outline)

    # pontas com pérolas de luz
    tips = [px(p) for p in ((-0.64, 0.4), (0.0, 0.62), (0.64, 0.4))]
    pearl = Image.new("L", (S, S), 0)
    pdw = ImageDraw.Draw(pearl)
    for x, y in tips:
        pdw.ellipse((x - 34, y - 34, x + 34, y + 34), fill=255)
    img = glow(img, pearl, (255, 245, 210), 30, 1.4)
    img = Image.composite(Image.new("RGB", (S, S), (255, 252, 235)), img, pearl)

    # joia central (losango do ícone do jogo), em ciano
    jx, jy = px((0, JEWEL_Y))
    jr = 0.17 * CROWN_SCALE
    gem = [(jx, jy - jr), (jx + jr, jy), (jx, jy + jr), (jx - jr, jy)]
    gem_mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(gem_mask).polygon(gem, fill=255)
    img = glow(img, gem_mask, CYAN, 36, 1.5)
    img = Image.composite(Image.new("RGB", (S, S), (40, 170, 220)), img, gem_mask)
    facet = Image.new("L", (S, S), 0)
    ImageDraw.Draw(facet).polygon([(jx, jy - jr), (jx + jr, jy), (jx, jy)], fill=255)
    img = Image.composite(Image.new("RGB", (S, S), (170, 245, 255)), img, facet)
    img = Image.composite(Image.new("RGB", (S, S), GOLD_LIGHT), img, stroke_mask([(gem, True)], 14))

    # brilho em estrela no canto da coroa
    spark = Image.new("L", (S, S), 0)
    sd = ImageDraw.Draw(spark)
    sx, sy = px((0.5, 0.58))
    for a, l, w in ((0, 120, 10), (90, 120, 10)):
        r = math.radians(a)
        sd.line((sx - math.cos(r) * l, sy - math.sin(r) * l, sx + math.cos(r) * l, sy + math.sin(r) * l), fill=255, width=w)
    spark = spark.filter(ImageFilter.GaussianBlur(3))
    img = glow(img, spark, (255, 240, 200), 12, 1.2)
    img = Image.composite(Image.new("RGB", (S, S), (255, 255, 245)), img, spark)

    os.makedirs(OUT, exist_ok=True)
    for size in (1024, 512):
        out = img.resize((size, size), Image.LANCZOS).convert("RGBA")
        out.save(os.path.join(OUT, "premium_%d.png" % size), optimize=True)
        print("premium_%d.png" % size, out.size, out.mode)


if __name__ == "__main__":
    main()
