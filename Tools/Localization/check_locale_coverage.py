"""Checks that localized strings can be drawn by the bundled locale fonts, without rebuilding them.

The Japanese, Korean and Simplified Chinese fonts are subsets: a character missing from
their cmap renders as a box. Hindi is drawn from precomposed word glyphs (HindiWords.json):
a word missing from that map falls back to unshaped Devanagari. Run this after adding
strings; anything reported needs either a different wording or a font rebuild with
build_locale_fonts.py.

Usage: python Tools/Localization/check_locale_coverage.py [text ...]
       (no arguments: checks every value in ja/ko/zh-Hans/hi.json)
"""
import json
import re
import struct
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]
LOCALES = PROJECT / "Assets/AstroCrawler/Resources/AstroCrawler/Localization"
SCRIPT = re.compile("[ऀ-ॿ‌‍]+")


def cmap(path):
    """Unicode code points covered by a TrueType font (formats 4 and 12)."""
    data = path.read_bytes()
    num_tables = struct.unpack(">H", data[4:6])[0]
    for i in range(num_tables):
        tag, _, offset, _ = struct.unpack(">4sIII", data[12 + 16 * i:28 + 16 * i])
        if tag == b"cmap":
            break
    else:
        raise ValueError("no cmap")
    count = struct.unpack(">H", data[offset + 2:offset + 4])[0]
    points = set()
    for i in range(count):
        _, _, sub = struct.unpack(">HHI", data[offset + 4 + 8 * i:offset + 12 + 8 * i])
        base = offset + sub
        fmt = struct.unpack(">H", data[base:base + 2])[0]
        if fmt == 4:
            seg2 = struct.unpack(">H", data[base + 6:base + 8])[0]
            seg = seg2 // 2
            ends = struct.unpack(">%dH" % seg, data[base + 14:base + 14 + seg2])
            starts = struct.unpack(">%dH" % seg, data[base + 16 + seg2:base + 16 + 2 * seg2])
            deltas = struct.unpack(">%dh" % seg, data[base + 16 + 2 * seg2:base + 16 + 3 * seg2])
            ro_pos = base + 16 + 3 * seg2
            ranges = struct.unpack(">%dH" % seg, data[ro_pos:ro_pos + seg2])
            for k in range(seg):
                for c in range(starts[k], ends[k] + 1):
                    if c == 0xFFFF:
                        continue
                    if ranges[k] == 0:
                        glyph = (c + deltas[k]) & 0xFFFF
                    else:
                        at = ro_pos + 2 * k + ranges[k] + 2 * (c - starts[k])
                        glyph = struct.unpack(">H", data[at:at + 2])[0]
                        if glyph:
                            glyph = (glyph + deltas[k]) & 0xFFFF
                    if glyph:
                        points.add(c)
        elif fmt == 12:
            groups = struct.unpack(">I", data[base + 12:base + 16])[0]
            for g in range(groups):
                s, e, _ = struct.unpack(">III", data[base + 16 + 12 * g:base + 28 + 12 * g])
                points.update(range(s, e + 1))
    return points


def missing_chars(text, points):
    return sorted({c for c in text if not c.isspace() and ord(c) not in points and ord(c) > 0x7F})


def main():
    fonts = {code: cmap(LOCALES / "Fonts" / ("Locale-%s.ttf" % code)) for code in ("ja", "ko", "zh-Hans")}
    words = json.loads((LOCALES / "HindiWords.json").read_text(encoding="utf-8"))
    entries = words["entries"] if isinstance(words, dict) and "entries" in words else words
    hindi = {row["key"] for row in entries}
    problems = 0
    for code in ("ja", "ko", "zh-Hans", "hi"):
        values = sys.argv[1:] or [e["value"] for e in json.loads((LOCALES / (code + ".json")).read_text(encoding="utf-8"))["entries"]]
        for text in values:
            if code == "hi":
                # Same tokens as GameLanguageFonts.ShapeHindi: runs of
                # Devanagari letters (plus ZWNJ/ZWJ); hyphens and spaces split.
                for core in SCRIPT.findall(text):
                    if core not in hindi:
                        print("hi  missing word %r in %r" % (core, text))
                        problems += 1
            else:
                miss = missing_chars(text, fonts[code])
                if miss:
                    print("%-7s missing %s in %r" % (code, "".join(miss), text))
                    problems += 1
    print("problems:", problems)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
