# Astro Blitz - App Store app previews for "iPhone Duo", from the App Store cut of the trailer:
#   AppStore/iphone_duo/previews/preview_portrait_886x1920.mp4
#   AppStore/iphone_duo/previews/preview_landscape_1920x886.mp4
#
# Source: Trailer/AstroIdle_Trailer_30s_EN_iOS.mp4 (compose_trailer.py --ios; 1080 x 1920,
# no other platform named on the end card). The whole trailer frame is kept: it is
# scaled to fit and the rest of the canvas is the same video blurred and darkened
# behind it, so nothing is cropped.
#
# Encoding follows Apple's app preview spec: H.264 High, 30 fps, yuv420p, up to
# ~10 Mbps, AAC stereo 256 kbps 48 kHz, 30 s or less, .mp4.
#
# Run: python Tools/trailer/compose_appstore_previews.py   (ffmpeg on the PATH)

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCE = os.path.join(ROOT, 'Trailer', 'AstroIdle_Trailer_30s_EN_iOS.mp4')
OUT = os.path.join(ROOT, 'AppStore', 'iphone_duo', 'previews')

TARGETS = [('preview_portrait_886x1920.mp4', 886, 1920), ('preview_landscape_1920x886.mp4', 1920, 886)]


def build(name, w, h):
    portrait = h > w
    fit = 'scale=%d:-2' % w if portrait else 'scale=-2:%d' % h
    graph = (
        '[0:v]split[a][b];'
        '[a]scale=%d:%d:force_original_aspect_ratio=increase,crop=%d:%d,'
        'boxblur=luma_radius=%d:luma_power=2,eq=brightness=-0.18:saturation=1.25[bg];'
        '[b]%s,setsar=1[fg];'
        '[bg][fg]overlay=(W-w)/2:(H-h)/2,fps=30,format=yuv420p[v]'
    ) % (w, h, w, h, 30 if portrait else 40, fit)
    path = os.path.join(OUT, name)
    cmd = ['ffmpeg', '-y', '-v', 'error', '-i', SOURCE, '-filter_complex', graph, '-map', '[v]', '-map', '0:a',
           '-map_metadata', '-1', '-map_chapters', '-1',
           '-c:v', 'libx264', '-preset', 'slow', '-profile:v', 'high', '-level', '4.0',
           '-b:v', '9M', '-maxrate', '10M', '-bufsize', '20M', '-r', '30',
           '-c:a', 'aac', '-b:a', '256k', '-ar', '48000', '-ac', '2',
           '-t', '30', '-movflags', '+faststart', path]
    subprocess.run(cmd, check=True)
    return path


def main():
    if not os.path.exists(SOURCE):
        sys.exit('missing %s - run: python Tools/trailer/compose_trailer.py --ios' % SOURCE)
    os.makedirs(OUT, exist_ok=True)
    for name, w, h in TARGETS:
        print('wrote', os.path.relpath(build(name, w, h), ROOT), flush=True)


if __name__ == '__main__':
    main()
