// AstroCrawler - procedural uGUI construction helpers.
//
// The interface is built in code for the same reason the arena is: no prefabs,
// no broken references, and the layout is reviewable as source. All text is
// TextMeshPro, and the panels and frames come from the Astro Survivors sci-fi UI
// set, nine-sliced so a single 300ppu texture serves every panel size.

using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AstroCrawler
{
    public static class UIFactory
    {
        /// <summary>
        /// Whether the screens being built are the portrait layout (1080 x 1920
        /// reference) or the landscape one (1920 x 1080). GameUI sets it before
        /// building anything; every screen reads it to pick its arrangement.
        /// </summary>
        public static bool Portrait;

        public static float ReferenceWidth { get { return Portrait ? 1080f : 1920f; } }
        public static float ReferenceHeight { get { return Portrait ? 1920f : 1080f; } }

        /// <summary>The landscape value or the portrait one, for the layout being built.</summary>
        public static T Pick<T>(T landscape, T portrait)
        {
            return Portrait ? portrait : landscape;
        }

        // Apelidos por função sobre a paleta normativa (Palette.cs, PILARES_DE_UI §3).
        // Os nomes antigos ficaram para não mexer nas telas; o valor é que mudou.
        public static readonly Color Ink = Palette.Fosforo;
        public static readonly Color InkDim = Palette.FosforoDim;
        // Miolo quase o Vazio: o contorno define a forma (pilar 1).
        public static readonly Color Panel = Palette.Vidro(Palette.Ciano, 0.05f, 0.92f);
        public static readonly Color PanelSoft = Palette.Vidro(Palette.Ciano, 0.1f, 0.85f);
        public static readonly Color Accent = Palette.Ciano;
        // Same blue and opacity as the main menu's Settings button ring.
        public static readonly Color ActionBorder = new Color(Accent.r, Accent.g, Accent.b, 0.7f);
        public static readonly Color ActionFill = Palette.Vidro(Palette.Ciano, 0.14f);
        public static readonly Color ActionFillStrong = Palette.Vidro(Palette.Ciano, 0.22f);
        public static readonly Color ActionFillMuted = Palette.Vidro(Palette.Ciano, 0.06f);
        // Âmbar: o que se ganha (créditos, recompensa, compra possível, JOGAR).
        public static readonly Color Evolved = Palette.Ambar;
        // Magenta: dano, perigo, chefe. O vermelho fica só para a vida crítica.
        public static readonly Color Danger = Palette.Magenta;
        public static readonly Color Critical = Palette.Critico;
        public static readonly Color Good = Palette.Acido;
        public static readonly Color PanelDeep = Palette.Vidro(Palette.Ciano, 0.02f, 0.97f);
        public static readonly Color Primary = Palette.Vidro(Palette.Ciano, 0.45f);
        public static readonly Color DangerDeep = Palette.Vidro(Palette.Magenta, 0.4f);

        // ------------------------------------------------------------------
        // Mobile sizing
        //
        // On a 6.5" phone in landscape the 1080-unit reference height is about
        // 68 mm, so one Android dp is ~2.5-3 units. Text below ~30 units reads
        // under 12sp, and a 48dp touch target is ~144 units. Everything the
        // player reads or taps is sized from these, not from pixel intuition.
        // ------------------------------------------------------------------

        // Revisão de legibilidade (23/11/2026): legenda 26→34, corpo 30→36,
        // botão 36→40; toque 120→144 (~48 dp num aparelho de 360 dp de largura).
        public const int TextCaption = 34;
        public const int TextBody = 36;
        public const int TextButton = 40;
        public const int TextHeading = 48;
        public const int TextTitle = 64;
        /// <summary>Menor tamanho que o autoajuste pode reduzir um texto de leitura.</summary>
        public const int TextMin = 28;
        public const float Touch = 144f;
        public const float TouchLarge = 160f;
        /// <summary>Altura das pílulas de informação (moedas), que não são alvo de toque.</summary>
        public const float Chip = 120f;

        // ------------------------------------------------------------------
        // Typeface
        // ------------------------------------------------------------------

        private static TMP_FontAsset tmpFont;

        /// <summary>
        /// The game's typeface, as a TextMeshPro asset built at runtime from the
        /// Rajdhani TTF. Building it here rather than committing a generated
        /// .asset keeps the "no serialised references" rule intact, and a dynamic
        /// atlas means accented Portuguese glyphs rasterise on demand instead of
        /// having to be baked in advance.
        /// </summary>
        public static TMP_FontAsset TmpFont
        {
            get
            {
                if (tmpFont != null)
                {
                    return tmpFont;
                }

                // The baked asset first: it is what the scene's labels reference,
                // and using the same object avoids a second atlas in memory.
                tmpFont = Resources.Load<TMP_FontAsset>("AstroCrawler/Art/AstroCrawler SDF");

                if (tmpFont == null)
                {
                    var ttf = Resources.Load<Font>("AstroCrawler/Art/Rajdhani-SemiBold");
                    if (ttf == null)
                    {
                        ttf = Resources.Load<Font>("AstroCrawler/Art/RobotoCondensed-Bold");
                    }

                    if (ttf != null)
                    {
                        tmpFont = BuildFontAsset(ttf);
                    }
                }

                // Whatever happens, hand back something that can render text.
                //
                // The usual fallbacks are not reliable in this project: the TMP
                // essentials were moved to "Fonts & Materials (fora do build)",
                // so TMP_Settings.defaultFontAsset resolves to null and the
                // LiberationSans Resources path no longer exists. Building from
                // an OS family is the one step that cannot come back empty.
                if (tmpFont == null)
                {
                    tmpFont = TMP_Settings.defaultFontAsset;
                }
                if (tmpFont == null)
                {
                    tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                }
                if (tmpFont == null)
                {
                    tmpFont = CreateFromSystemFont();
                }
                if (tmpFont == null)
                {
                    Debug.LogError("[AstroCrawler] Nenhuma fonte TextMeshPro pode ser criada; " +
                                   "a interface ficara sem texto. Verifique " +
                                   "Resources/AstroCrawler/Art/Rajdhani-SemiBold.ttf.");
                }

                return tmpFont;
            }
        }

        /// <summary>
        /// Forgets the generated font asset. The asset is created at runtime and
        /// destroyed when play mode ends, but this static reference outlives it
        /// when Domain Reload is disabled - and a destroyed font asset renders
        /// nothing at all.
        /// </summary>
        public static void ResetFont()
        {
            GameLanguageFonts.Reset();
            tmpFont = null;
            boldFont = null;
            boldFontLoaded = false;
        }

        private static TMP_FontAsset boldFont;
        private static bool boldFontLoaded;

        /// <summary>
        /// Rajdhani Bold, baked like the SemiBold asset. Null when it has not
        /// been generated, in which case bold labels fall back to TMP's
        /// synthesized bold on the regular font.
        /// </summary>
        public static TMP_FontAsset TmpBoldFont
        {
            get
            {
                if (!boldFontLoaded)
                {
                    boldFontLoaded = true;
                    boldFont = Resources.Load<TMP_FontAsset>("AstroCrawler/Art/AstroCrawler Bold SDF");
                }
                return boldFont;
            }
        }

        /// <summary>Gives a label the real bold cut instead of a synthesized one.</summary>
        public static void UseBold(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }
            var bold = TmpBoldFont;
            if (bold != null)
            {
                text.font = bold;
                text.fontStyle &= ~FontStyles.Bold;
            }
            else
            {
                text.fontStyle |= FontStyles.Bold;
            }
            GameLanguageFonts.Apply(text);
        }

        /// <summary>
        /// Builds the SDF asset, checking that it actually came out usable.
        ///
        /// TMP hands back a TMP_FontAsset with a null material and no atlas
        /// texture when it is asked to build one before its own shader lookup
        /// has warmed up - which is exactly what happens if the first call lands
        /// inside a MonoBehaviour's Awake. The object is not null, so only
        /// inspecting the material catches it; a text component given such an
        /// asset then throws ArgumentNullException the moment anything reads
        /// fontMaterial. Hence: try, verify, and try a different way.
        /// </summary>
        private static TMP_FontAsset BuildFontAsset(Font ttf)
        {
            var asset = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);

            if (!IsUsable(asset))
            {
                // The defaults-only overload takes a different path internally
                // and survives the early-Awake case.
                asset = TMP_FontAsset.CreateFontAsset(ttf);
            }

            if (asset == null)
            {
                return null;
            }

            asset.name = "AstroCrawler SDF";
            asset.hideFlags = HideFlags.HideAndDontSave;

            if (!IsUsable(asset))
            {
                Debug.LogWarning("[AstroCrawler] O font asset gerado a partir de " + ttf.name +
                                 " saiu sem material ou sem atlas; caindo para a fonte padrao.");
                return null;
            }

            return asset;
        }

        /// <summary>
        /// A TMP asset is only safe to hand to a text component once it has both
        /// a material and an atlas texture.
        /// </summary>
        private static bool IsUsable(TMP_FontAsset asset)
        {
            return asset != null && asset.material != null && asset.atlasTexture != null;
        }

        /// <summary>Last resort: build a TMP asset from a font the OS provides.</summary>
        private static TMP_FontAsset CreateFromSystemFont()
        {
            var families = new[] { "Segoe UI", "Arial", "Liberation Sans", "DejaVu Sans" };
            for (var i = 0; i < families.Length; i++)
            {
                var asset = TMP_FontAsset.CreateFontAsset(families[i], "Regular", 90);
                if (IsUsable(asset))
                {
                    asset.hideFlags = HideFlags.HideAndDontSave;
                    return asset;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Art accessors
        // ------------------------------------------------------------------

        public static Sprite CardFrame
        {
            get { return ArtLibrary.Frame != null ? ArtLibrary.Frame : Resources.Load<Sprite>("AstroCrawler/Art/CardBackground"); }
        }

        public static Sprite CardGlow { get { return ArtLibrary.FrameGlow; } }
        public static Sprite PanelSprite { get { return ArtLibrary.Frame; } }
        public static Sprite GradientSprite { get { return ArtLibrary.GradientPanel; } }
        public static Sprite ShieldIcon { get { return Resources.Load<Sprite>("AstroCrawler/Art/ShieldIcon"); } }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>An opaque or tinted rectangle, optionally using a sprite.</summary>
        public static Image Rect(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
            }
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A sci-fi panel: the cut-corner frame, nine-sliced so the 26px border
        /// stays crisp at any size.
        /// </summary>
        public static Image Panel9(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var frame = sprite != null ? sprite : PanelSprite;
            if (frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.4f;
            }

            image.raycastTarget = false;
            return image;
        }

        /// <summary>A soft glow behind a panel, used to lift it off the starfield.</summary>
        public static Image Glow(Transform parent, string name, Color color, float spread = 22f)
        {
            var image = Rect(parent, name, color, ArtLibrary.FrameGlow);
            if (image.sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2f;
            }
            Stretch(image.rectTransform, -spread);
            return image;
        }

        public static TMP_Text Label(Transform parent, string name, string content, int size, Color color,
            TextAlignmentOptions anchor = TextAlignmentOptions.TopLeft, bool boldFace = false)
        {
            var rect = NewRect(parent, name);

            // TMP's Awake runs the instant the component is added, and if the
            // object is active at that moment it goes looking for a default font
            // asset - which this project does not have, so it logs a warning per
            // label. Adding it while inactive lets the font be assigned first.
            var go = rect.gameObject;
            go.SetActive(false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TmpFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = FontStyles.Normal;
            if (boldFace)
            {
                UseBold(text);
            }
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;

            GameLanguageFonts.Apply(text);

            go.SetActive(true);
            return text;
        }

        /// <summary>
        /// Gives a label a dark drop shadow via TMP's underlay, which is a
        /// material feature rather than a second draw call like uGUI's Shadow.
        /// </summary>
        public static void AddUnderlay(TMP_Text text, float softness = 0.2f, float dilate = 0.1f)
        {
            if (text == null)
            {
                return;
            }

            // The marker is what survives into the scene; the material change
            // below does not, and is re-applied from the marker on load.
            var marker = text.GetComponent<TextUnderlay>();
            if (marker == null)
            {
                marker = text.gameObject.AddComponent<TextUnderlay>();
            }
            marker.softness = softness;
            marker.dilate = dilate;

            ApplyUnderlay(text, softness, dilate);
        }

        /// <summary>Writes the underlay onto the label's material instance.</summary>
        public static void ApplyUnderlay(TMP_Text text, float softness, float dilate)
        {
            if (text == null)
            {
                return;
            }

            // Reading fontMaterial clones the shared material, and TMP throws
            // ArgumentNullException if there is nothing to clone. A missing
            // shadow is a cosmetic loss; an exception here aborts the whole
            // screen being built, so this check is load-bearing.
            if (text.fontSharedMaterial == null)
            {
                return;
            }

            var material = text.fontMaterial;
            if (material == null || !material.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            {
                return;
            }

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        }

        public static Button Button(Transform parent, string name, string caption, Color background,
            Color captionColor, int fontSize = TextButton, Sound sound = Sound.Tap)
        {
            var rect = NewRect(parent, name);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            var frame = PanelSprite;
            if (frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.4f;
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // null means no caption at all (the caller lays out its own);
            // an empty string still creates one, to be filled in later.
            if (caption != null)
            {
                var label = Label(rect, "Caption", caption, fontSize, captionColor,
                    TextAlignmentOptions.Center, true);
                Stretch(label.rectTransform, 8f);
                label.characterSpacing = 6f;
                label.enableAutoSizing = true;
                label.fontSizeMin = Mathf.Min(fontSize, Mathf.Max(18f, fontSize * 0.7f));
                label.fontSizeMax = fontSize;
                AddUnderlay(label);
            }

            // Buttons grow slightly under the pointer; LeanTween handles the
            // press feel that Unity's colour tint alone does not sell.
            var feedback = rect.gameObject.AddComponent<ButtonFeedback>();
            feedback.Initialize(rect);
            feedback.clickSound = sound;

            return button;
        }

        /// <summary>Sets the sound a button makes (on the ButtonFeedback every factory button carries).</summary>
        public static void SetClickSound(Component button, Sound sound)
        {
            if (button == null)
            {
                return;
            }
            var feedback = button.GetComponent<ButtonFeedback>();
            if (feedback == null)
            {
                feedback = button.gameObject.AddComponent<ButtonFeedback>();
            }
            feedback.clickSound = sound;
        }

        /// <summary>
        /// A full-screen page whose backdrop bleeds past the safe area. The page
        /// itself is laid out inside the safe area (its parent), but the dark
        /// backdrop extends well beyond it so the strip under a notch is covered
        /// too instead of showing a seam.
        /// </summary>
        public static RectTransform Page(Transform parent, string name, Color backdrop, out Image background)
        {
            var root = NewRect(parent, name);
            Stretch(root);
            background = Rect(root, "Backdrop", backdrop);
            Stretch(background.rectTransform, -400f);
            background.raycastTarget = true;
            return root;
        }

        /// <summary>
        /// A round icon button: the mobile default for anything that has a
        /// universal symbol (back, pause, settings, close).
        /// </summary>
        // ------------------------------------------------------------------
        // Gameplay x menus: o kit Sci-Fi do Astro Survivor veste só os menus.
        // ------------------------------------------------------------------

        /// <summary>Verdadeiro enquanto uma tela de gameplay é construída: as molduras do kit não são aplicadas.</summary>
        public static bool KeepGameplayFrames { get; private set; }

        /// <summary>Escopo de construção de gameplay: <c>using var _ = UIFactory.GameplayFrames();</c></summary>
        public static GameplayFrameScope GameplayFrames()
        {
            var scope = new GameplayFrameScope(KeepGameplayFrames);
            KeepGameplayFrames = true;
            return scope;
        }

        public readonly struct GameplayFrameScope : System.IDisposable
        {
            private readonly bool previous;
            public GameplayFrameScope(bool previous) { this.previous = previous; }
            public void Dispose() { KeepGameplayFrames = previous; }
        }

        /// <summary>
        /// Amplia a área de toque de um controle sem mudar o desenho: um filho
        /// transparente maior recebe o raio e o evento sobe para o Button/Slider pai.
        /// </summary>
        public static Image ExpandHitArea(RectTransform target, float minWidth, float minHeight)
        {
            if (target == null) return null;
            var existing = target.Find("HitArea");
            var hit = existing != null ? existing.GetComponent<Image>() : Rect(target, "HitArea", Color.clear, null);
            hit.raycastTarget = true;
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var size = target.rect.size;
            if (size.x <= 0f) size.x = target.sizeDelta.x;
            if (size.y <= 0f) size.y = target.sizeDelta.y;
            rt.sizeDelta = new Vector2(Mathf.Max(minWidth, size.x), Mathf.Max(minHeight, size.y));
            rt.SetAsFirstSibling();
            return hit;
        }

        public static Button RoundButton(Transform parent, string name, Sprite icon, Color background,
            float size = Touch, Sound sound = Sound.Tap)
        {
            var rect = NewRect(parent, name);
            rect.sizeDelta = new Vector2(size, size);

            // The face fills the inner circle of the "Icon Button 07" ring drawn over it;
            // callers recolour the ring through its "Ring" child.
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtLibrary.IconRingFill;
            image.color = background;
            image.raycastTarget = true;

            var ring = Rect(rect, "Ring", new Color(1f, 1f, 1f, 0.9f), ArtLibrary.IconRingFrame);
            Stretch(ring.rectTransform);
            ring.raycastTarget = false;

            // Fora da gameplay, o par redondo do kit (vidro + linha) no lugar do Icon Button 07.
            if (!KeepGameplayFrames)
            {
                var kitGlass = ArtLibrary.Ui("Kit/sf_redondo_vidro");
                var kitLine = ArtLibrary.Ui("Kit/sf_redondo_linha");
                if (kitGlass != null && kitLine != null)
                {
                    image.sprite = kitGlass;
                    ring.sprite = kitLine;
                }
            }

            if (icon != null)
            {
                var glyph = Rect(rect, "Icon", Ink, icon);
                glyph.preserveAspect = true;
                Stretch(glyph.rectTransform, size * 0.27f);
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            DropShadow(image, 20f, 7f, 0.55f);
            var feedback = rect.gameObject.AddComponent<ButtonFeedback>();
            feedback.Initialize(rect);
            feedback.clickSound = sound;
            return button;
        }

        /// <summary>
        /// A big tile: icon on top, one short word under it. Menu sections are
        /// tiles because on a phone the icon is what the thumb aims for and the
        /// word only confirms it.
        /// </summary>
        public static Button Tile(Transform parent, string name, Sprite icon, string caption, Color background,
            Vector2 size, Color iconColor, Sound sound = Sound.Navigate)
        {
            var button = Button(parent, name, null, background, Ink, TextButton, sound);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var glyph = Rect(rect, "Icon", iconColor, icon);
            glyph.preserveAspect = true;
            var iconSize = Mathf.Min(size.x, size.y) * 0.42f;
            Place(glyph.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.14f),
                new Vector2(iconSize, iconSize));

            var label = Label(rect, "Caption", caption, TextBody, Ink, TextAlignmentOptions.Bottom, true);
            Stretch(label.rectTransform, 10f);
            label.rectTransform.offsetMin = new Vector2(10f, size.y * 0.1f);
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = TextCaption;
            label.fontSizeMax = TextBody;
            AddUnderlay(label);

            DropShadow(button.GetComponent<Image>(), 22f, 8f, 0.55f);
            return button;
        }

        /// <summary>
        /// A button with an icon at its left and the caption in the remaining
        /// space: for actions that need a word (JOGAR, CONTINUAR) but should
        /// still be recognisable by shape.
        /// </summary>
        public static Button IconButton(Transform parent, string name, string caption, Sprite icon,
            Color background, Vector2 size, int fontSize = TextButton, Sound sound = Sound.Tap)
        {
            var button = Button(parent, name, caption, background, Ink, fontSize, sound);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var iconSize = Mathf.Min(size.y * 0.46f, 64f);
            AddButtonIcon(button, icon, Ink, iconSize);
            var glyph = rect.Find("Icon") as RectTransform;
            if (glyph != null)
            {
                glyph.anchoredPosition = new Vector2(size.y * 0.3f, 0f);
            }

            var caption2 = rect.Find("Caption") as RectTransform;
            if (caption2 != null)
            {
                caption2.offsetMin = new Vector2(size.y * 0.3f + iconSize + 12f, caption2.offsetMin.y);
                caption2.offsetMax = new Vector2(-size.y * 0.3f, caption2.offsetMax.y);
            }
            return button;
        }

        /// <summary>A rounded tag. Height decides the cap radius, so the ends stay circular.</summary>
        public static Image Pill(Transform parent, string name, string text, Color background, Color ink,
            float height, int fontSize = TextCaption, Sprite icon = null)
        {
            var rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = false;
            var sprite = ArtLibrary.Pill;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                // The sprite's 64px caps must come out as half the height.
                image.pixelsPerUnitMultiplier = 42.67f / Mathf.Max(1f, height);
            }

            var label = Label(rect, "Label", text, fontSize, ink, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 2f);
            label.rectTransform.offsetMin = new Vector2(icon != null ? height * 0.85f : height * 0.4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-height * 0.4f, -2f);
            label.characterSpacing = 3f;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            if (icon != null)
            {
                var glyph = Rect(rect, "Icon", ink, icon);
                glyph.preserveAspect = true;
                Place(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(height * 0.28f, 0f),
                    new Vector2(height * 0.5f, height * 0.5f));
            }
            return image;
        }

        /// <summary>
        /// Four glowing corner brackets around a rect: the sci-fi "locked on"
        /// look for whatever is currently selected. Returned as one object so it
        /// can be switched on and off.
        /// </summary>
        public static RectTransform Brackets(Transform target, Color color, float size = 34f, float inset = -10f)
        {
            var root = NewRect(target, "Brackets");
            Stretch(root, inset);
            var sprite = ArtLibrary.CornerGlow;

            // One sprite, four rotations. Each corner is pivoted on its own
            // outer point so the rotation keeps it in its corner.
            var anchors = new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            var angles = new[] { 0f, 270f, 180f, 90f };
            for (var i = 0; i < anchors.Length; i++)
            {
                var corner = Rect(root, "Corner" + i, color, sprite);
                var rect = corner.rectTransform;
                rect.anchorMin = anchors[i];
                rect.anchorMax = anchors[i];
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(size, size);
                rect.localEulerAngles = new Vector3(0f, 0f, angles[i]);
            }
            return root;
        }

        /// <summary>
        /// A light streak that sweeps across a button every few seconds (the
        /// Shift kit's "shiny move"), so the primary action draws the eye
        /// without blinking. Runtime only: a tween started while the scene
        /// builder bakes the UI would be saved half-way.
        /// </summary>
        public static void AddShine(RectTransform button, float period = 3.2f, float alpha = 0.28f)
        {
            if (button == null || !Application.isPlaying || ArtLibrary.ShineStreak == null)
            {
                return;
            }

            var mask = NewRect(button, "ShineMask");
            Stretch(mask, 4f);
            mask.gameObject.AddComponent<RectMask2D>();

            var shine = Rect(mask, "Shine", new Color(1f, 1f, 1f, alpha), ArtLibrary.ShineStreak);
            var height = button.sizeDelta.y;
            var width = button.sizeDelta.x;
            Place(shine.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(height * 1.1f, height * 1.6f));

            var from = -height * 1.2f;
            var to = width + height * 0.2f;
            const float sweep = 0.75f;
            var rect = shine.rectTransform;
            LeanTween.value(shine.gameObject, t =>
            {
                if (rect == null) return;
                var k = Mathf.Clamp01(t * period / sweep);
                rect.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k * k * (3f - 2f * k)), 0f);
            }, 0f, 1f, period).setLoopClamp().setIgnoreTimeScale(true);
        }

        /// <summary>A soft vertical fade at the top or bottom edge, for text over the starfield.</summary>
        public static Image EdgeFade(Transform parent, string name, bool top, float height, float alpha)
        {
            var image = Rect(parent, name, new Color(0.01f, 0.015f, 0.03f, alpha),
                top ? ArtLibrary.GradientTop : ArtLibrary.GradientBottom);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.offsetMin = new Vector2(-400f, top ? -height : -400f);
            rect.offsetMax = new Vector2(400f, top ? 400f : height);
            return image;
        }


        /// <summary>
        /// Puts an icon at the left edge of a button. The caption stays centred,
        /// so a column of icon buttons still reads as one aligned stack.
        /// </summary>
        public static Image AddButtonIcon(Button button, Sprite icon, Color color, float size = 30f)
        {
            if (button == null || icon == null)
            {
                return null;
            }

            var image = Rect(button.transform, "Icon", color, icon);
            image.preserveAspect = true;
            Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(size, size));
            return image;
        }

        /// <summary>A small filled tag - "SELECIONADA", "MÁX" - with an optional icon.</summary>
        public static Image Badge(Transform parent, string name, string text, Color background, Color ink,
            Sprite icon = null, int fontSize = 16)
        {
            var badge = Panel9(parent, name, background);
            var label = Label(badge.rectTransform, "Label", text, fontSize, ink, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 4f);
            label.characterSpacing = 4f;
            if (icon != null)
            {
                label.rectTransform.offsetMin = new Vector2(30f, 4f);
                var image = Rect(badge.rectTransform, "Icon", ink, icon);
                image.preserveAspect = true;
                Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(18f, 18f));
            }
            return badge;
        }

        // ------------------------------------------------------------------
        // True Shadow
        // ------------------------------------------------------------------

        /// <summary>
        /// A soft drop shadow that follows the element's real outline, so the
        /// cut corners of the sci-fi frame cast cut-corner shadows.
        ///
        /// IgnoreCasterColor is about cost as much as looks: True Shadow caches
        /// the blurred texture under a hash that includes the caster's colour,
        /// so without it every colour tween on a button would re-blur.
        /// </summary>
        public static TrueShadow DropShadow(Graphic graphic, float size = 24f, float distance = 8f,
            float alpha = 0.55f)
        {
            var shadow = AddShadow(graphic);
            if (shadow == null)
            {
                return null;
            }
            shadow.BlendMode = BlendMode.Normal;
            shadow.Size = size;
            shadow.OffsetAngle = 90f;
            shadow.OffsetDistance = distance;
            shadow.Color = new Color(0f, 0f, 0f, alpha);
            return shadow;
        }

        /// <summary>
        /// A coloured halo, additive so it reads as emitted light. Selection and
        /// hover change only its colour, which True Shadow applies to vertices:
        /// the blur itself stays cached.
        /// </summary>
        public static TrueShadow GlowShadow(Graphic graphic, Color color, float size = 32f, float spread = 0.1f)
        {
            var shadow = AddShadow(graphic);
            if (shadow == null)
            {
                return null;
            }
            shadow.BlendMode = BlendMode.Additive;
            shadow.Size = size;
            shadow.Spread = spread;
            shadow.OffsetDistance = 0f;
            shadow.Color = color;
            return shadow;
        }

        private static TrueShadow AddShadow(Graphic graphic)
        {
            if (graphic == null)
            {
                return null;
            }
            var shadow = graphic.gameObject.AddComponent<TrueShadow>();
            shadow.IgnoreCasterColor = true;
            shadow.UseCasterAlpha = true;
            return shadow;
        }

        /// <summary>The drop shadow on a graphic, adding one if it has none.</summary>
        public static TrueShadow EnsureDropShadow(Graphic graphic, float size = 24f, float distance = 8f,
            float alpha = 0.55f)
        {
            var existing = FindShadow(graphic, BlendMode.Normal);
            return existing != null ? existing : DropShadow(graphic, size, distance, alpha);
        }

        /// <summary>The glow on a graphic, adding a dark (invisible) one if it has none.</summary>
        public static TrueShadow EnsureGlow(Graphic graphic, Color color, float size = 32f, float spread = 0.1f)
        {
            var existing = FindShadow(graphic, BlendMode.Additive);
            return existing != null ? existing : GlowShadow(graphic, color, size, spread);
        }

        private static TrueShadow FindShadow(Component target, BlendMode mode)
        {
            if (target == null)
            {
                return null;
            }
            var shadows = target.GetComponents<TrueShadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                if (shadows[i].BlendMode == mode)
                {
                    return shadows[i];
                }
            }
            return null;
        }

        // One running fade per glow. Cancelling by id rather than by object
        // matters: the glow shares its GameObject with the graphic, and a
        // blanket cancel would also kill that graphic's own colour tweens.
        private static readonly Dictionary<EntityId, int> glowTweens = new Dictionary<EntityId, int>();

        /// <summary>Fades a glow to a colour (usually the same hue, new alpha).</summary>
        public static void FadeGlow(TrueShadow glow, Color target, float duration)
        {
            if (glow == null)
            {
                return;
            }

            var key = glow.GetEntityId();
            int running;
            if (glowTweens.TryGetValue(key, out running))
            {
                LeanTween.cancel(running);
                glowTweens.Remove(key);
            }

            if (duration <= 0f || !glow.isActiveAndEnabled)
            {
                glow.Color = target;
                return;
            }

            var from = glow.Color;
            var tween = LeanTween.value(glow.gameObject, t =>
            {
                if (glow != null)
                {
                    glow.Color = Color.Lerp(from, target, t);
                }
            }, 0f, 1f, duration).setEase(LeanTweenType.easeOutQuad).setIgnoreTimeScale(true);
            glowTweens[key] = tween.uniqueId;
        }

        /// <summary>A bright flash that decays back to a resting colour.</summary>
        public static void FlashGlow(TrueShadow glow, Color flash, Color rest, float duration)
        {
            if (glow == null)
            {
                return;
            }
            glow.Color = flash;
            FadeGlow(glow, rest, duration);
        }

        public static void ResetGlowTweens()
        {
            glowTweens.Clear();
        }

        // ------------------------------------------------------------------
        // Layout helpers
        // ------------------------------------------------------------------

        /// <summary>Stretches a rect to fill its parent with a uniform padding.</summary>
        public static RectTransform Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        /// <summary>
        /// Anchors a fixed-size rect against a corner. <paramref name="pivot"/>
        /// doubles as the anchor, so (0,1) is top-left and (0.5,0) bottom-centre.
        /// </summary>
        public static RectTransform Place(RectTransform rect, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Adds (or finds) a CanvasGroup, for fading whole screens.</summary>
        public static CanvasGroup Group(Component target)
        {
            if (target == null)
            {
                return null;
            }
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.gameObject.AddComponent<CanvasGroup>();
            }
            return group;
        }

        /// <summary>A left-to-right filling bar. Returns the fill image.</summary>
        public static Image Bar(Transform parent, string name, Color fillColor, Color backColor, out Image back)
        {
            var container = NewRect(parent, name);

            back = Rect(container, "Back", backColor);
            Stretch(back.rectTransform);

            var fill = Rect(container, "Fill", fillColor);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            // Image.Type.Filled is ignored without a sprite. It has to be a real
            // asset: a sprite generated at runtime cannot be saved into the
            // baked scene, and a baked bar with no sprite always draws full.
            fill.sprite = ArtLibrary.Pixel;

            return fill;
        }

        /// <summary>
        /// A bar with a slower "ghost" layer behind the fill, so a burst of
        /// damage leaves a visible trail of what was just lost.
        /// </summary>
        public static Image GhostBar(Transform parent, string name, Color fillColor, Color ghostColor,
            Color backColor, out Image ghost)
        {
            var container = NewRect(parent, name);

            var back = Rect(container, "Back", backColor);
            Stretch(back.rectTransform);

            ghost = Rect(container, "Ghost", ghostColor);
            ghost.rectTransform.anchorMin = Vector2.zero;
            ghost.rectTransform.anchorMax = Vector2.one;
            ghost.rectTransform.offsetMin = new Vector2(2f, 2f);
            ghost.rectTransform.offsetMax = new Vector2(-2f, -2f);
            ghost.type = Image.Type.Filled;
            ghost.fillMethod = Image.FillMethod.Horizontal;
            ghost.fillOrigin = 0;
            ghost.fillAmount = 1f;
            ghost.sprite = ArtLibrary.Pixel;

            var fill = Rect(container, "Fill", fillColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.sprite = ArtLibrary.Pixel;

            return fill;
        }

        public static string ColorTag(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
