# Localization

676 authored keys per additional locale: ja, hi, ko, zh-Hans, de, fr and it.
Includes all names and descriptions of the 51 cards at their three upgrade tiers.
Card names now identify the effect and stay the same across tiers; the compact
selection shows the offered level separately. Legacy translation keys remain available.
Portuguese, English and Spanish remain in GameLocalization and inline Choose calls.
Existing saved language IDs 0/1/2 are preserved; new IDs are appended.
The new catalogs use the Portuguese source as a stable key. Preserve whitespace
at concatenation boundaries, numeric mechanics, rich-text tags and `{0}` placeholders.
All runtime localization works offline; translation services are not part of the game.

Translations were prepared with machine assistance and reviewed for menu actions,
combat terminology, numbers and card mechanics. Native-speaker editorial review is
still useful before publishing marketing copy in these languages.

## Fonts

Latin locales keep Rajdhani. Japanese, Korean and Simplified Chinese use static
weight-500 subsets derived from Noto Sans JP/KR/SC. Hindi uses a derivative of
Noto Sans Devanagari with precomposed word glyphs. The four TTF files total
564,236 bytes. Only one locale TMP font/atlas is kept after switching languages;
old dynamic atlases are destroyed. These files are bundled, not system-font fallbacks.

Font sources (Google Fonts, OFL 1.1):

- https://github.com/google/fonts/tree/main/ofl/notosansjp
- https://github.com/google/fonts/tree/main/ofl/notosanskr
- https://github.com/google/fonts/tree/main/ofl/notosanssc
- https://github.com/google/fonts/tree/main/ofl/notosansdevanagari

The original CJK license is in Fonts/OFL-CJK.txt (identical across the three sources).
The Devanagari license is in Fonts/OFL-Devanagari.txt. All derived families are
renamed Astro Locale. Source checksums and measured output sizes are recorded in
Tools/Localization/FontBuildReport.json.

## Hindi rendering and regeneration

This TMP version does not perform complete Indic shaping. The build script uses
HarfBuzz to compose every authored Hindi word, including conjuncts and vowel
placement, into one glyph in the Unicode Private Use Area. HindiWords.json maps
801 normal readable words to those glyphs. ITextPreprocessor performs the replacement
only for rendering; localized strings, placeholders and saves remain ordinary text.
Spaces and line breaks remain available for wrapping. Character tracking is removed
while rendering Hindi and restored when changing language.

After editing Hindi text or adding Asian characters, regenerate fonts and the word map:

1. Install Python fonttools 4.67.0 and uharfbuzz 0.56.3 in a development environment.
2. Download the original Google Fonts TTF files and name them NotoSansJP.ttf,
   NotoSansKR.ttf, NotoSansSC.ttf and NotoSansDevanagari.ttf in a source directory.
   Check source hashes against FontBuildReport.json when reproducing this revision.
3. From the project root, run `python Tools/Localization/build_locale_fonts.py --sources /path/to/sources`.
4. Update the technical documentation, check glyph coverage and inspect the game.

The Hindi word atlas covers the authored UI, not arbitrary new user-generated Hindi
strings. New words must be regenerated; unknown words retain their readable source
instead of being silently replaced. The original Devanagari glyphs remain in the font.
Do not use PUA strings as translation source text.
