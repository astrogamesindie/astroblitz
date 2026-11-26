# Astro Idle - TikTok cut of the team's talking-head video (1080 x 1920, 30 fps, PT-BR).
#
# Inputs:  the recorded video (passed on the command line, e.g. Downloads/video1.mp4)
#          Trailer/TikTok/transcript_whisper.json  word timings from faster-whisper (model "small", pt)
#          Trailer/raw/<shot>/00000.jpg            the gameplay takes recorded for the trailer
#          Trailer/build/music.wav                 the trailer's original music (compose_music.py)
# Output:  Trailer/TikTok/AstroIdle_TikTok_PT.mp4 and Trailer/TikTok/legendas.srt
#
# The edit: pauses are cut out (jump cuts, alternating framing), a hook opens on
# gameplay, trailer takes cover what is being described, sci-fi headlines in the
# trailer's style, big word-by-word captions with the spoken word in orange, an
# end card with the icon and "LINK NA BIO", the trailer music ducked under the
# voice and some of the game's own sounds on the transitions.
#
# Run:  python Tools/trailer/compose_tiktok.py <video.mp4>
#       python Tools/trailer/compose_tiktok.py <video.mp4> --preview 0.5 12 40   (output seconds, JPGs only)

import functools
import json
import math
import os
import subprocess
import sys
import wave
from multiprocessing import Pool

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import compose_trailer as ct  # noqa: E402  (effects and text sprites shared with the trailer)

W, H, FPS = ct.W, ct.H, ct.FPS
ROOT = ct.ROOT
OUT_DIR = os.path.join(ROOT, 'Trailer', 'TikTok')
BUILD = os.path.join(OUT_DIR, 'build')
FRAMES = os.path.join(BUILD, 'frames')
TALK = os.path.join(BUILD, 'talk')
TRANSCRIPT = os.path.join(OUT_DIR, 'transcript_whisper.json')
OUTPUT = os.path.join(OUT_DIR, 'AstroIdle_TikTok_PT.mp4')
SRT = os.path.join(OUT_DIR, 'legendas.srt')
VIDEO = None  # set from argv in main / the pool initializer

ORANGE = (255, 150, 40)
CAPTION_Y = 1460          # low, just above TikTok's caption/description band
SHOW_TITLES = False       # hook, section headlines and stickers (removed at the user's request)
CAPTION_MAX_W = 820       # clear of the like/comment column on the right
TITLE_Y = 330

# ----------------------------------------------------------------------
# Transcript: corrections and the cut list
# ----------------------------------------------------------------------

# Whisper's words fixed by (segment, word) index; None drops a word.
FIXES = {
    (0, 6): 'Christiann.',
    (1, 3): 'chamo', (1, 4): 'Ely.',
    (2, 20): 'Idle.',
    (3, 7): 'idle',
    (5, 5): 'as',
    (5, 8): 'os', (5, 9): 'upgrades,', (5, 10): None,
    (14, 12): 'deem',
    (15, 3): 'deixa',
    (19, 5): 'Astro Idle,',
}

GAP = 0.30     # a silence longer than this between words is cut
PRE, POST = 0.07, 0.12


@functools.lru_cache(maxsize=1)
def words():
    segments = json.load(open(TRANSCRIPT, encoding='utf-8'))
    out = []
    for si, seg in enumerate(segments):
        ws = seg['words']
        for wi, w in enumerate(ws):
            text = FIXES.get((si, wi), w['w'].strip())
            if text is None:
                # merged into the previous word ("P" + "-Grace" -> "upgrades")
                if out:
                    out[-1] = (out[-1][0], out[-1][1], w['e'], out[-1][3])
                continue
            end = w['e']
            out.append((text, w['s'], end, si))
    return out


@functools.lru_cache(maxsize=1)
def chunks():
    """Source intervals kept, in order: speech with a little air around it."""
    ws = words()
    spans = []
    s, e = ws[0][1], ws[0][2]
    for _, ws_, we, _ in ws[1:]:
        if ws_ - e > GAP:
            spans.append((s, e))
            s = ws_
        e = max(e, we)
    spans.append((s, e))
    out = []
    for s, e in spans:
        s, e = max(0.0, s - PRE), e + POST
        if out and s <= out[-1][1]:
            out[-1] = (out[-1][0], e)
        else:
            out.append((s, e))
    return out


@functools.lru_cache(maxsize=1)
def chunk_starts():
    starts, acc = [], 0.0
    for s, e in chunks():
        starts.append(acc)
        acc += e - s
    return starts, acc


def duration():
    return chunk_starts()[1]


def src_to_out(t):
    starts, _ = chunk_starts()
    for (s, e), o in zip(chunks(), starts):
        if t < s:
            return o
        if t <= e:
            return o + (t - s)
    return starts[-1] + chunks()[-1][1] - chunks()[-1][0]


def out_to_src(t):
    """Source time at an output time, and the index of the chunk it falls in."""
    starts, _ = chunk_starts()
    for k, ((s, e), o) in enumerate(zip(chunks(), starts)):
        if t < o + (e - s) or k == len(starts) - 1:
            return s + (t - o), k
    return chunks()[-1][1], len(starts) - 1


# ----------------------------------------------------------------------
# The plan (source times; converted to output time)
# ----------------------------------------------------------------------

# B-roll: (src_start, src_end, shot, first frame, last frame, zoom, zoom centre x, y)
# Zoom 1.12 around y 860 keeps the AdMob test banner of the menu screens out of frame.
BROLL_SRC = [
    (0.00, 1.72, '02b_gameplay', 40, 130, 1.5, 540, 950),        # hook
    (9.26, 10.95, '01_menu_warp', 0, 44, 1.12, 540, 860),        # "o Astro Idle"
    (10.95, 14.95, '01_menu_warp', 44, 192, 1.0, 540, 960),      # space, warp, wave 1
    (17.40, 22.40, '02_gameplay', 0, 299, 1.45, 540, 950),       # the ship stays put, enemies come
    (23.40, 27.10, '03_levelup', 40, 100, 1.0, 540, 960),        # choosing upgrades
    (27.10, 31.00, '02b_gameplay', 0, 200, 1.45, 540, 950),      # fighting asteroids and ships
    (32.45, 34.05, '04_special', 22, 110, 1.6, 540, 950),        # the special
    (41.70, 44.10, '06_hangar', 0, 161, 1.12, 540, 860),         # the ships
    (45.05, 48.60, '08_basedeck', 25, 134, 1.12, 540, 860),      # a new card
    (53.75, 55.40, '02b_gameplay', 60, 140, 1.5, 540, 950),      # aggressive style
    (56.75, 59.10, '07_upgrades', 20, 110, 1.12, 540, 860),      # buying with time
    (60.85, 64.85, '05_boss', 0, 170, 1.3, 540, 1050),           # stages and bosses
    (68.40, 72.40, '05_boss', 330, 470, 1.3, 540, 1050),         # beating the boss
    (96.70, 104.40, '02_gameplay', 150, 299, 1.35, 540, 950),    # end card background
]
END_CARD_SRC = (96.70, 104.40)
BOSS_DOWN_FRAME = 454

# Headlines: (src_start, src_end, text, colour, glow)
TITLES_SRC = [
    (12.30, 14.90, 'JOGO IDLE ESPACIAL', ct.WHITE, ct.CYAN),
    (17.50, 19.60, 'COMO FUNCIONA?', ct.WHITE, ct.CYAN),
    (24.00, 26.90, 'ESCOLHA UPGRADES', ct.WHITE, ct.CYAN),
    (32.50, 34.05, 'HABILIDADE ESPECIAL', ct.WHITE, ORANGE),
    (41.80, 44.05, 'NAVES ÚNICAS', ct.WHITE, ct.CYAN),
    (45.10, 48.55, 'MELHORIAS ÚNICAS', ct.WHITE, ct.PURPLE),
    (53.80, 55.40, 'ESTILO AGRESSIVO', ct.WHITE, ct.RED),
    (63.60, 64.85, 'CHEFÕES!', ct.WHITE, ct.RED),
    (70.00, 71.95, 'DERROTE O CHEFE!', ct.WHITE, ct.GOLD),   # ends as the boss blows up
]

# Stickers over the talking parts: (src_start, src_end, text, colour)
STICKERS_SRC = [
    (75.70, 79.45, 'COMENTA O QUE ACHOU!', ct.CYAN),
    (86.25, 88.85, 'PRIMEIRO PROJETO!', ct.GOLD),
    (89.50, 93.05, 'SEGUE PRA VER OS PRÓXIMOS', ct.CYAN),
]

HOOK_OUT = 4.6   # the hook title stays until the introductions are over


@functools.lru_cache(maxsize=1)
def plan():
    broll = [(src_to_out(a), src_to_out(b), shot, f0, f1, z, cx, cy) for a, b, shot, f0, f1, z, cx, cy in BROLL_SRC]
    titles = [(src_to_out(a), src_to_out(b), text, col, glow) for a, b, text, col, glow in TITLES_SRC]
    stickers = [(src_to_out(a), src_to_out(b), text, col) for a, b, text, col in STICKERS_SRC]
    end = (src_to_out(END_CARD_SRC[0]), src_to_out(END_CARD_SRC[1]))
    return broll, titles, stickers, end


def broll_at(t):
    for item in plan()[0]:
        if item[0] <= t < item[1]:
            return item
    return None


def boss_down_time():
    for a, b, shot, f0, f1, *_ in plan()[0]:
        if shot == '05_boss' and f0 <= BOSS_DOWN_FRAME <= f1:
            return a + (b - a) * (BOSS_DOWN_FRAME - f0) / (f1 - f0)
    return None


# ----------------------------------------------------------------------
# Captions: three words at a time, the spoken word in orange
# ----------------------------------------------------------------------

@functools.lru_cache(maxsize=1)
def phrases():
    """[(out_start, out_end, [(word, out_s, out_e), ...])]"""
    groups, current = [], []
    for text, s, e, seg in words():
        current.append((text, src_to_out(s), src_to_out(e), seg))
        ends_clause = text[-1:] in '.,!?'
        if len(current) == 3 or ends_clause or sum(len(w[0]) for w in current) > 15:
            groups.append(current)
            current = []
    if current:
        groups.append(current)
    out = []
    for i, g in enumerate(groups):
        start = g[0][1]
        end = g[-1][2] + 0.15
        if i + 1 < len(groups):
            end = min(end, groups[i + 1][0][1])
        out.append((start, end, [(w[0], w[1], w[2]) for w in g]))
    return out


def caption_text(word):
    return word.upper()


@functools.lru_cache(maxsize=512)
def caption_sprite(key, active):
    """key: tuple of words; active: index of the spoken word (-1 none)."""
    size = 96
    face = ct.font(ct.BOLD, size)
    texts = [caption_text(w) for w in key]
    space = int(size * 0.28)
    while True:
        widths = [face.getbbox(t)[2] - face.getbbox(t)[0] for t in texts]
        total = sum(widths) + space * (len(texts) - 1)
        if total <= CAPTION_MAX_W or size <= 56:
            break
        size -= 4
        face = ct.font(ct.BOLD, size)
        space = int(size * 0.28)
    stroke = max(6, size // 11)
    pad = stroke + 30
    layer = Image.new('RGBA', (total + pad * 2, size + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x = pad
    top = pad - face.getbbox('H')[1] + 4
    for i, (t, w) in enumerate(zip(texts, widths)):
        color = ORANGE if i == active else ct.WHITE
        d.text((x, top), t, font=face, fill=color + (255,), stroke_width=stroke, stroke_fill=(0, 0, 0, 255))
        x += w + space
    shadow = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    shadow.putalpha(layer.getchannel('A').filter(ImageFilter.GaussianBlur(10)).point(lambda v: int(v * 0.7)))
    out = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    out.alpha_composite(shadow, (0, 8))
    out.alpha_composite(layer)
    return out


def draw_captions(canvas, t):
    for start, end, ws in phrases():
        if start <= t < end:
            active = -1
            for i, (_, s, e) in enumerate(ws):
                if s <= t < e + 0.05:
                    active = i
            spr = caption_sprite(tuple(w[0] for w in ws), active)
            a = t - start
            scale = 0.82 + 0.18 * ct.ease_out_back(min(1.0, a / 0.14), 2.4)
            ct.put(canvas, spr, W / 2, CAPTION_Y, scale, ct.clamp01(a / 0.05))
            return


def write_srt():
    def stamp(x):
        ms = int(round(x * 1000))
        return '%02d:%02d:%02d,%03d' % (ms // 3600000, ms // 60000 % 60, ms // 1000 % 60, ms % 1000)
    lines = []
    for i, (s, e, ws) in enumerate(phrases(), 1):
        lines += [str(i), '%s --> %s' % (stamp(s), stamp(e)), ' '.join(w[0] for w in ws), '']
    with open(SRT, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))


# ----------------------------------------------------------------------
# Pictures
# ----------------------------------------------------------------------

def talk_frame(src_t):
    index = max(1, min(talk_count(), int(round(src_t * FPS)) + 1))
    img = Image.open(os.path.join(TALK, '%05d.jpg' % index)).convert('RGB')
    img = img.resize((W, H), Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=2.2, percent=70, threshold=2))
    img = ImageEnhance.Color(ImageEnhance.Contrast(img).enhance(1.06)).enhance(1.1)
    return img


@functools.lru_cache(maxsize=1)
def talk_count():
    return len([n for n in os.listdir(TALK) if n.endswith('.jpg')])


@functools.lru_cache(maxsize=1)
def vignette():
    y, x = np.mgrid[0:H, 0:W]
    d = np.sqrt(((x - W / 2) / (W * 0.75)) ** 2 + ((y - H * 0.45) / (H * 0.7)) ** 2)
    return np.clip(1.15 - d * 0.55, 0.55, 1.0)[..., None].astype(np.float32)


def talking(t):
    src_t, k = out_to_src(t)
    img = talk_frame(src_t)
    # Jump-cut framing: every other chunk a tighter shot, with a slow push-in.
    s, e = chunks()[k]
    p = ct.clamp01((src_t - s) / max(0.3, e - s))
    zoom = (1.0 if k % 2 == 0 else 1.14) + 0.03 * p
    img = ct.framed(img, zoom, 540, 820, 0, 0)
    arr = np.asarray(img).astype(np.float32) * vignette()
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def broll(t, item):
    a, b, shot, f0, f1, zoom, cx, cy = item
    p = (t - a) / max(0.01, b - a)
    pos = f0 + (f1 - f0) * p
    img = ct.source(shot, pos)
    push = zoom * (1.0 + 0.05 * p)
    img = ct.framed(img, push, cx, cy, 0, 0)
    img = ct.juicy(img, 0.7, 1.3, 1.08)
    if item[0] == plan()[3][0]:
        # end card: darker footage under the logo
        img = Image.fromarray((np.asarray(img).astype(np.float32) * 0.45).astype(np.uint8))
    return img


@functools.lru_cache(maxsize=1)
def app_icon():
    icon = Image.open(ct.APP_ICON).convert('RGBA').resize((300, 300), Image.LANCZOS)
    mask = Image.new('L', icon.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, 299, 299), radius=66, fill=255)
    icon.putalpha(mask)
    out = Image.new('RGBA', (380, 380), (0, 0, 0, 0))
    glow = Image.new('RGBA', out.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle((30, 30, 349, 349), radius=76, fill=ct.ORANGE + (200,))
    out.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    out.alpha_composite(icon, (40, 40))
    return out


@functools.lru_cache(maxsize=32)
def sticker_sprite(text, color):
    spr = ct.txt(text, 64, ct.WHITE, color, max_w=860)
    w, h = spr.width + 70, spr.height + 30
    plate = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(plate)
    c = 22
    poly = [(c, 0), (w - 1, 0), (w - 1, h - 1 - c), (w - 1 - c, h - 1), (0, h - 1), (0, c)]
    d.polygon(poly, fill=(6, 12, 28, 225), outline=color + (255,))
    d.line(poly + [poly[0]], fill=color + (255,), width=4)
    out = Image.new('RGBA', (w + 60, h + 60), (0, 0, 0, 0))
    glow = Image.new('RGBA', out.size, (0, 0, 0, 0))
    glow.alpha_composite(plate, (30, 30))
    out.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14)))
    out.alpha_composite(plate, (30, 30))
    out.alpha_composite(spr, (30 + (w - spr.width) // 2, 30 + (h - spr.height) // 2))
    return out


def overlays(canvas, t):
    broll_items, titles, stickers, (end_a, end_b) = plan()

    # Hook: two lines over the opening gameplay, kept through the introductions.
    if SHOW_TITLES and t < HOOK_OUT:
        ct.anim(canvas, t, 0.05, HOOK_OUT, ct.txt('FIZEMOS NOSSO', 70, ct.CYAN, ct.CYAN), W / 2, TITLE_Y - 120, 'left')
        ct.headline(canvas, t, 0.18, HOOK_OUT, 'PRIMEIRO JOGO!', W / 2, TITLE_Y + 20, 132, ct.WHITE, ORANGE, ORANGE)

    for a, b, text, col, glow in titles if SHOW_TITLES else ():
        if a <= t <= b:
            ct.headline(canvas, t, a, b, text, W / 2, TITLE_Y, 112, col, glow, glow, seed=int(a * 10))

    for a, b, text, col in stickers if SHOW_TITLES else ():
        if a <= t <= b:
            ct.anim(canvas, t, a, b, sticker_sprite(text, col), W / 2, TITLE_Y + 40, 'pop')

    # End card: icon, name, link in bio.
    if end_a <= t <= end_b:
        ct.anim(canvas, t, end_a + 0.05, end_b, app_icon(), W / 2, 520, 'pop')
        ct.headline(canvas, t, end_a + 0.25, end_b, 'ASTRO IDLE', W / 2, 790, 140, ct.WHITE, ct.CYAN, ct.CYAN, seed=7)
        pulse = 1.0 + 0.04 * math.sin((t - end_a) * 2 * math.pi * 1.6)
        if t >= end_a + 0.6:
            ct.put(canvas, sticker_sprite('LINK NA BIO', ct.GOLD), W / 2, 990,
                   pulse * (0.4 + 0.6 * ct.ease_out_back(min(1, (t - end_a - 0.6) / 0.25))), 1.0)
        ct.anim(canvas, t, end_a + 0.9, end_b, ct.txt('GRÁTIS NA PLAY STORE E APP STORE', 46, ct.WHITE, ct.CYAN, ct.SEMI),
                W / 2, 1125, 'rise')

    # Last words: a small reminder over the thank-you.
    if SHOW_TITLES and t > end_b + 0.1:
        ct.anim(canvas, t, end_b + 0.1, duration() + 1, sticker_sprite('ASTRO IDLE · LINK NA BIO', ct.CYAN),
                W / 2, TITLE_Y, 'pop')

    down = boss_down_time()
    if down is not None:
        ct.shockwave(canvas, t, down, 540, 1050, ct.WHITE, 1.3)
        ct.sparks(canvas, t, down, 540, 1050, ct.GOLD, 26, 620, seed=3)

    draw_captions(canvas, t)


def transition(img, t):
    """Flash and zoom blur into each take, a quick whip back to the talk."""
    for a, b, *_ in plan()[0]:
        if 0 <= t - a < 0.14:
            k = 1 - (t - a) / 0.14
            img = ct.zoom_blur(img, 0.06 * k)
            img = ct.flash(img, 0.75 * k)
        if a > 0.01 and 0 <= a - t < 0.06:
            img = ct.flash(img, 0.4 * (1 - (a - t) / 0.06))
        if 0 <= t - b < 0.12:
            k = 1 - (t - b) / 0.12
            img = ct.whip(img, 140 * k, int(14 * k))
    down = boss_down_time()
    if down is not None and 0 <= t - down < 0.3:
        k = 1 - (t - down) / 0.3
        img = ct.flash(img, 0.85 * k)
        img = ct.framed(img, 1.0 + 0.04 * k, W / 2, H / 2, math.sin(t * 90) * 22 * k, math.cos(t * 70) * 18 * k)
    return img


def render(index):
    t = index / FPS
    item = broll_at(t)
    img = broll(t, item) if item else talking(t)
    canvas = img.convert('RGBA')
    overlays(canvas, t)
    img = transition(canvas.convert('RGB'), t)
    img.save(os.path.join(FRAMES, '%05d.jpg' % index), quality=94, subsampling=0)
    return index


# ----------------------------------------------------------------------
# Sound
# ----------------------------------------------------------------------

def whoosh_wav(path, length=0.42, rising=True):
    sr = 48000
    n = int(sr * length)
    rng = np.random.default_rng(5 if rising else 9)
    noise = rng.standard_normal(n)
    # Sweep a resonant band through the noise with a one-pole filter pair.
    out = np.zeros(n)
    lp = bp = 0.0
    for i in range(n):
        p = i / n
        f = (0.02 + 0.25 * p) if rising else (0.27 - 0.25 * p)
        lp += f * (noise[i] - lp)
        bp += f * (lp - bp)
        out[i] = lp - bp
    env = np.sin(np.linspace(0, math.pi, n)) ** (1.6 if rising else 0.8)
    out = out * env
    out = out / (np.abs(out).max() + 1e-9) * 0.8
    stereo = np.stack([out, np.roll(out, 90)], axis=1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes((stereo * 32767).astype(np.int16).tobytes())


def sfx_events():
    p = os.path.join(ct.AUDIO, 'Partida')
    u = os.path.join(ct.AUDIO, 'UI')
    swoosh_in = os.path.join(BUILD, 'whoosh_in.wav')
    swoosh_out = os.path.join(BUILD, 'whoosh_out.wav')
    ev = []
    for a, b, shot, *_ in plan()[0]:
        ev.append((max(0.0, a - 0.22), swoosh_in, 0.55))
        ev.append((b - 0.05, swoosh_out, 0.35))
    if SHOW_TITLES:
        for a, b, text, *_ in plan()[1]:
            ev.append((a + 0.02, os.path.join(p, 'Impact_2_Top_End.wav'), 0.35))
        ev.append((0.2, os.path.join(p, 'Impact_2_Mid.wav'), 0.45))
        for a, b, *_ in plan()[2]:
            ev.append((a, os.path.join(u, 'Click_Combo_3_High.wav'), 0.45))
    down = boss_down_time()
    if down is not None:
        ev.append((down, os.path.join(p, 'Impact_2_Mid.wav'), 0.7))
        ev.append((down + 0.02, os.path.join(p, 'Impact_2_Reso.wav'), 0.5))
    end_a = plan()[3][0]
    ev.append((end_a + 0.6, os.path.join(u, 'Tone1B_MajorTriadUp.wav'), 0.45))
    return ev


def build_audio():
    """Voice (cut like the picture) + music bed ducked under it + effects -> build/mix.wav"""
    voice = os.path.join(BUILD, 'voice.wav')
    parts, labels = [], []
    for k, (s, e) in enumerate(chunks()):
        fade = min(0.012, (e - s) / 4)
        parts.append('[0:a]atrim=%.3f:%.3f,asetpts=N/SR/TB,afade=t=in:d=%.3f,afade=t=out:st=%.3f:d=%.3f[c%d]'
                     % (s, e, fade, e - s - fade, fade, k))
        labels.append('[c%d]' % k)
    parts.append('%sconcat=n=%d:v=0:a=1,aformat=sample_rates=48000:channel_layouts=stereo[v]' % (''.join(labels), len(labels)))
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', VIDEO, '-filter_complex', ';'.join(parts), '-map', '[v]', voice],
                   check=True)

    whoosh_wav(os.path.join(BUILD, 'whoosh_in.wav'), 0.42, True)
    whoosh_wav(os.path.join(BUILD, 'whoosh_out.wav'), 0.3, False)

    total = duration()
    events = sfx_events()
    # The music's driving middle (beats 8-40 at 128 BPM) loops cleanly.
    loop_a, loop_b = ct.beat(8), ct.beat(40)
    cmd = ['ffmpeg', '-y', '-v', 'error', '-i', voice, '-i', ct.MUSIC]
    for _, path, _ in events:
        cmd += ['-i', path]
    f = ['[0:a]volume=1.0,asplit=2[voice][key]',
         '[1:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=%.4f:%.4f,asetpts=N/SR/TB,'
         'aloop=loop=-1:size=%d,atrim=0:%.3f,volume=0.32[bed]' % (loop_a, loop_b, int((loop_b - loop_a) * 48000), total),
         '[bed][key]sidechaincompress=threshold=0.03:ratio=8:attack=20:release=350:makeup=1[duck]']
    labels = ['[voice]', '[duck]']
    for k, (at, _, vol) in enumerate(events):
        ms = int(max(0, at) * 1000)
        f.append('[%d:a]aformat=sample_rates=48000:channel_layouts=stereo,volume=%.2f,adelay=%d|%d[s%d]'
                 % (k + 2, vol, ms, ms, k))
        labels.append('[s%d]' % k)
    f.append('%samix=inputs=%d:normalize=0:duration=first,alimiter=limit=0.9:level=0,'
             'loudnorm=I=-14:TP=-1.5:LRA=11,aresample=48000[a]' % (''.join(labels), len(labels)))
    cmd += ['-filter_complex', ';'.join(f), '-map', '[a]', '-t', '%.3f' % total, os.path.join(BUILD, 'mix.wav')]
    subprocess.run(cmd, check=True)


def encode():
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', str(FPS), '-i', os.path.join(FRAMES, '%05d.jpg'),
                    '-i', os.path.join(BUILD, 'mix.wav'), '-map', '0:v', '-map', '1:a',
                    # Capped so the file stays under the ~72 MB the TikTok Android app accepts.
                    '-c:v', 'libx264', '-preset', 'slow', '-crf', '19', '-maxrate', '5M', '-bufsize', '10M',
                    '-pix_fmt', 'yuv420p', '-profile:v', 'high',
                    '-r', str(FPS), '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', OUTPUT],
                   check=True)


# ----------------------------------------------------------------------

def init(video):
    global VIDEO
    VIDEO = video


def extract_talk():
    os.makedirs(TALK, exist_ok=True)
    if len([n for n in os.listdir(TALK) if n.endswith('.jpg')]) > 0:
        return
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', VIDEO, '-vf', 'fps=%d' % FPS, '-q:v', '2',
                    os.path.join(TALK, '%05d.jpg')], check=True)


def main():
    if len(sys.argv) < 2 or not os.path.isfile(sys.argv[1]):
        print('usage: python compose_tiktok.py <video.mp4> [--preview t ...]')
        sys.exit(1)
    init(os.path.abspath(sys.argv[1]))
    os.makedirs(FRAMES, exist_ok=True)
    extract_talk()
    total = int(duration() * FPS)
    print('cut: %.1f s -> %.1f s (%d chunks), %d frames' % (chunks()[-1][1], duration(), len(chunks()), total))
    if '--preview' in sys.argv:
        for v in sys.argv[sys.argv.index('--preview') + 1:]:
            print(os.path.join(FRAMES, '%05d.jpg' % render(int(round(float(v) * FPS)))))
        return
    write_srt()
    with Pool(max(1, os.cpu_count() - 1), initializer=init, initargs=(VIDEO,)) as pool:
        for done, _ in enumerate(pool.imap_unordered(render, range(total), chunksize=8), 1):
            if done % 300 == 0:
                print('frames %d / %d' % (done, total), flush=True)
    build_audio()
    encode()
    print('ok', OUTPUT)


if __name__ == '__main__':
    main()
