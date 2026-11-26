"""Rebuild offline locale subsets and precomposed Hindi words from the committed catalogs.

Requires fonttools==4.67.0 and uharfbuzz==0.56.3. Original OFL fonts are supplied
through --sources; see Assets/AstroCrawler/Resources/AstroCrawler/Localization/README.md.
No translation service or network request is used by this build step.
"""
import argparse
import hashlib
import io
import json
import re
from pathlib import Path

import uharfbuzz as hb
from fontTools import subset
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

PROJECT = Path(__file__).resolve().parents[2]
LOCALES = PROJECT / "Assets/AstroCrawler/Resources/AstroCrawler/Localization"
SCRIPT = re.compile(r"[\u0900-\u097f\u200c\u200d]+")


def read_strings(code):
    entries = json.loads((LOCALES / (code + ".json")).read_text())["entries"]
    return [row["value"] for row in entries]


def static_font(path):
    font = TTFont(path)
    if "fvar" in font:
        axes = {axis.axisTag: (500 if axis.axisTag == "wght" else axis.defaultValue)
                for axis in font["fvar"].axes}
        font = instantiateVariableFont(font, axes, inplace=True)
    return font


def rename(font, family):
    font["name"].names = [name for name in font["name"].names if name.nameID not in [1, 4, 6, 16]]
    for name_id in [1, 4, 6, 16]:
        value = family.replace(" ", "") if name_id == 6 else family
        font["name"].setName(value, name_id, 3, 1, 0x409)
        font["name"].setName(value, name_id, 1, 0, 0)


def save_subset(font, unicodes, path, family):
    options = subset.Options()
    options.name_IDs = ["*"]  # Preserve copyright and license records.
    options.layout_features = []  # Hindi shaping is already composed; CJK needs no substitutions here.
    job = subset.Subsetter(options=options)
    job.populate(unicodes=unicodes)
    job.subset(font)
    rename(font, family)
    font.save(path)
    return len(font.getGlyphOrder())


def build(sources, output):
    (output / "Fonts").mkdir(parents=True, exist_ok=True)
    reports = {}
    latin = set(range(32, 256)) | set(range(0x2000, 0x2070))
    for code, filename, native in [("ja", "NotoSansJP.ttf", "日本語"),
                                   ("ko", "NotoSansKR.ttf", "한국어"),
                                   ("zh-Hans", "NotoSansSC.ttf", "简体中文")]:
        font = static_font(sources / filename)
        strings = read_strings(code) + [native, "ASTRO BLITZ"]
        required = {ord(c) for text in strings for c in text + text.upper() if not c.isspace()}
        missing = required - font.getBestCmap().keys()
        if missing:
            raise ValueError((code, "Missing characters", sorted(missing)))
        path = output / "Fonts" / ("Locale-" + code + ".ttf")
        count = save_subset(font, required | latin, path, "Astro Locale " + code)
        reports[code] = {"glyphs": count, "bytes": path.stat().st_size}

    font = static_font(sources / "NotoSansDevanagari.ttf")
    original = io.BytesIO()
    font.save(original)
    face = hb.Face(original.getvalue())
    shaper = hb.Font(face)
    shaper.scale = (font["head"].unitsPerEm, font["head"].unitsPerEm)
    hb.ot_font_set_funcs(shaper)
    strings = read_strings("hi") + ["हिन्दी"]
    words = sorted({match.group() for text in strings for match in SCRIPT.finditer(text)})
    glyph_set = font.getGlyphSet()
    cmap = font.getBestCmap()
    available = iter(cp for cp in range(0xE000, 0xF900) if cp not in cmap)
    mapping = []
    new_names = []
    for index, word in enumerate(words):
        buffer = hb.Buffer()
        buffer.add_str(word)
        buffer.guess_segment_properties()
        buffer.language = "hi"
        hb.shape(shaper, buffer)
        pen = TTGlyphPen(glyph_set)
        advance_x = advance_y = 0
        for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
            if info.codepoint == 0:
                raise ValueError(("Hindi missing glyph", word))
            name = font.getGlyphName(info.codepoint)
            pen.addComponent(name, (1, 0, 0, 1,
                                   advance_x + position.x_offset, advance_y + position.y_offset))
            advance_x += position.x_advance
            advance_y += position.y_advance
        name = "astroHindiWord" + str(index)
        glyph = pen.glyph()
        font["glyf"].glyphs[name] = glyph
        glyph.recalcBounds(font["glyf"])
        font["hmtx"].metrics[name] = (advance_x, getattr(glyph, "xMin", 0))
        new_names.append(name)
        cp = next(available)
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[cp] = name
        mapping.append({"key": word, "value": chr(cp)})
    font.setGlyphOrder(font.getGlyphOrder() + new_names)
    path = output / "Fonts/Locale-hi.ttf"
    required = latin | set(range(0x0900, 0x0980)) | {ord(entry["value"]) for entry in mapping}
    count = save_subset(font, required, path, "Astro Locale hi")
    (output / "HindiWords.json").write_text(json.dumps({"entries": mapping}, ensure_ascii=False, indent=2) + "\n")
    reports["hi"] = {"glyphs": count, "words": len(words), "bytes": path.stat().st_size}
    reports["sources"] = {file.name: hashlib.sha256(file.read_bytes()).hexdigest()
                          for file in sources.glob("*.ttf")}
    (output / "FontBuildReport.json").write_text(json.dumps(reports, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(reports, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=LOCALES)
    args = parser.parse_args()
    build(args.sources, args.output)
