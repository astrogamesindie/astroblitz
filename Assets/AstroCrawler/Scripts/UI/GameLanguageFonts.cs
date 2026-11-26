using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AstroCrawler
{
    /// <summary>One dynamic locale font at a time; Latin keeps the original art direction.</summary>
    public static class GameLanguageFonts
    {
        private static ConditionalWeakTable<TMP_Text, Binding> bindings = new ConditionalWeakTable<TMP_Text, Binding>();
        private static TMP_FontAsset nativeFont;
        private static Font nativeSource;
        private static GameLocalization.Language? fontLanguage;
        private static readonly List<TMP_FontAsset> retired = new List<TMP_FontAsset>();
        private static readonly Regex hindiWord = new Regex(@"[\u0900-\u097f\u200c\u200d]+", RegexOptions.CultureInvariant);
        private static Dictionary<string, string> hindiWords;

        [Serializable] private sealed class Word { public string key; public string value; }
        [Serializable] private sealed class WordTable { public Word[] entries; }

        private sealed class Binding : ITextPreprocessor
        {
            public TMP_Text owner;
            public TMP_FontAsset original;
            public bool syntheticBold;
            public float spacing;
            public bool capturedSpacing;

            public string PreprocessText(string text)
            {
                if (owner != null)
                {
                    if (!capturedSpacing || GameLocalization.Current != GameLocalization.Language.Hindi)
                    { spacing = owner.characterSpacing; capturedSpacing = true; }
                    if (GameLocalization.Current == GameLocalization.Language.Hindi && owner.characterSpacing != 0f)
                        owner.characterSpacing = 0f;
                }
                return ShapeHindi(text);
            }
        }

        public static void Reset()
        {
            Release(nativeFont);
            foreach (var asset in retired) Release(asset);
            retired.Clear();
            nativeFont = null;
            nativeSource = null;
            fontLanguage = null;
            hindiWords = null;
            bindings = new ConditionalWeakTable<TMP_Text, Binding>();
        }

        private static void Release(TMP_FontAsset asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }

        public static void Apply(TMP_Text text)
        {
            if (text == null || !Application.isPlaying) return;
            EnsureNativeFont();
            var binding = bindings.GetValue(text, key => new Binding { owner = key, original = key.font });
            if (text.font != null && !text.font.name.StartsWith("Astro Locale ", StringComparison.Ordinal))
                binding.original = text.font;
            text.textPreprocessor = binding;
            if (nativeFont != null)
            {
                var bold = binding.original != null && binding.original == UIFactory.TmpBoldFont;
                if (bold && (text.fontStyle & FontStyles.Bold) == 0)
                { text.fontStyle |= FontStyles.Bold; binding.syntheticBold = true; }
                if (text.font != nativeFont) text.font = nativeFont;
            }
            else
            {
                if (binding.syntheticBold) { text.fontStyle &= ~FontStyles.Bold; binding.syntheticBold = false; }
                if (binding.original != null && text.font != binding.original) text.font = binding.original;
                if (binding.capturedSpacing) text.characterSpacing = binding.spacing;
            }
        }

        public static void ApplyScene()
        {
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Apply(text);
                if (GameLocalization.Current != GameLocalization.Language.Hindi && bindings.TryGetValue(text, out var binding) && binding.capturedSpacing)
                    text.characterSpacing = binding.spacing;
                text.SetAllDirty();
                var shadow = text.GetComponent<TextUnderlay>();
                if (shadow != null) shadow.Apply();
            }
            foreach (var asset in retired) Release(asset);
            retired.Clear();
        }

        private static void EnsureNativeFont()
        {
            var language = GameLocalization.Current;
            var needsNative = language == GameLocalization.Language.Japanese || language == GameLocalization.Language.Hindi ||
                language == GameLocalization.Language.Korean || language == GameLocalization.Language.SimplifiedChinese;
            if (fontLanguage == language) return;
            if (nativeFont != null) retired.Add(nativeFont);
            nativeFont = null;
            nativeSource = null;
            fontLanguage = language;
            if (!needsNative) return;
            var code = GameLocalization.Code(language);
            nativeSource = Resources.Load<Font>("AstroCrawler/Localization/Fonts/Locale-" + code);
            if (nativeSource != null)
                nativeFont = TMP_FontAsset.CreateFontAsset(nativeSource, 48, 5, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (nativeFont == null || nativeFont.material == null || nativeFont.atlasTexture == null)
            {
                Release(nativeFont);
                nativeFont = null;
                Debug.LogError("[AstroCrawler] Unable to create locale font: " + code);
                return;
            }
            nativeFont.name = "Astro Locale " + code + " SDF";
            nativeFont.hideFlags = HideFlags.HideAndDontSave;
            nativeFont.fallbackFontAssetTable = new List<TMP_FontAsset> { UIFactory.TmpFont };
        }

        /// <summary>Precomposed word glyphs preserve HarfBuzz Indic shaping without a native runtime dependency.</summary>
        public static string ShapeHindi(string text)
        {
            if (string.IsNullOrEmpty(text) || GameLocalization.Current != GameLocalization.Language.Hindi) return text;
            if (hindiWords == null)
            {
                hindiWords = new Dictionary<string, string>();
                var resource = Resources.Load<TextAsset>("AstroCrawler/Localization/HindiWords");
                var table = resource != null ? JsonUtility.FromJson<WordTable>(resource.text) : null;
                if (table != null && table.entries != null)
                {
                    foreach (var word in table.entries) hindiWords[word.key] = word.value;
                }
                else Debug.LogError("[AstroCrawler] Missing Hindi shaping dictionary.");
            }
            return hindiWord.Replace(text, match => hindiWords.TryGetValue(match.Value, out var shaped) ? shaped : match.Value);
        }
    }
}
