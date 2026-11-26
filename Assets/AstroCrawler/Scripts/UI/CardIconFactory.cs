// AstroCrawler - procedural symbols for upgrade cards and the HUD.

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public static class CardIconFactory
    {
        // Drawn at 128 px with 3x3 samples per pixel, so the edges are
        // antialiased like the imported line art.
        private const int Size = 128;
        private const int Samples = 3;

        /// <summary>
        /// Line weight shared by every card icon: half the stroke, in the
        /// glyphs' -1..1 space, so a stroke is 4.7% of the icon's width - the
        /// same as the imported line art (ic_*.png, reprocessed to match).
        /// </summary>
        public const float Stroke = 0.047f;
        private static readonly Dictionary<CardEffectId, Sprite> cache =
            new Dictionary<CardEffectId, Sprite>();

        private static readonly Dictionary<CardEffectId, Vector2> glyphCenters =
            new Dictionary<CardEffectId, Vector2>();

        // Effects whose cached sprite comes from imported artwork (as is, or
        // recoloured to white by Monochrome), for CenterArtwork's offsets.
        private static readonly HashSet<CardEffectId> importedArt = new HashSet<CardEffectId>();

        /// <summary>Above this average saturation an imported icon is treated as coloured artwork.</summary>
        private const float ColouredArtSaturation = 0.08f;

        /// <summary>Centers the visible ink, compensating for asymmetric margins and visual weight.</summary>
        public static void CenterArtwork(Image image, CardEffectId effect, Vector2 basePosition)
        {
            var offset = Vector2.zero;
            // Alpha-weighted centres (>16/255), in texture pixels of the card
            // set (Art/CardIcons, 128 px). Measured offline so imported
            // textures do not need CPU readability.
            // Y is inverted because the source image rows run top to bottom.
            // These corrections apply only to the imported artwork, not fallback glyphs.
            if (image.sprite != null && (image.sprite == ArtLibrary.EffectIcon(effect) ||
                (importedArt.Contains(effect) && cache.TryGetValue(effect, out var imported) && image.sprite == imported)))
            {
                switch (effect)
                {
                    case CardEffectId.DoubleAttack: offset = new Vector2(-0.007f, -0.022f); break;
                    case CardEffectId.PiercingShot: offset = new Vector2(0.905f, 0.553f); break;
                    case CardEffectId.Missiles: offset = new Vector2(0.589f, -0.414f); break;
                    case CardEffectId.Shield: offset = new Vector2(-0.606f, 1.741f); break;
                    case CardEffectId.EngineOverload: offset = new Vector2(-10.537f, 0.02f); break;
                    case CardEffectId.WildVolley: offset = new Vector2(-0.445f, -0.097f); break;
                    case CardEffectId.BattleFury: offset = new Vector2(0.005f, 0.02f); break;
                    case CardEffectId.BlackHole: offset = new Vector2(1.146f, 1.088f); break;
                    case CardEffectId.RepulsionField: offset = new Vector2(-0.103f, -4.706f); break;
                    case CardEffectId.EmergencyRepair: offset = new Vector2(-0.001f, 8.104f); break;
                    case CardEffectId.DebrisWall: offset = new Vector2(1.177f, -7.531f); break;
                    case CardEffectId.CoreOverload: offset = new Vector2(-0.085f, 0.01f); break;
                    case CardEffectId.DualGravityWell: offset = new Vector2(-0.016f, 0.015f); break;
                    case CardEffectId.TemporalDistortion: offset = new Vector2(-0.014f, -0.033f); break;
                    case CardEffectId.ChainDetonation: offset = new Vector2(-0.04f, -7.836f); break;
                    case CardEffectId.OrbitalStrike: offset = new Vector2(-0.004f, 14.261f); break;
                    case CardEffectId.GravityMagnet: offset = new Vector2(0.003f, -0.196f); break;
                    case CardEffectId.Execution: offset = new Vector2(-0.035f, -4.217f); break;
                    case CardEffectId.OverdriveFury: offset = new Vector2(-0.125f, 0.123f); break;
                    case CardEffectId.TemporalRift: offset = new Vector2(-0.085f, 0.01f); break;
                    case CardEffectId.ParadoxRecharge: offset = new Vector2(5.839f, 1.63f); break;
                }
            }
            else if (cache.TryGetValue(effect, out var generated) && image.sprite == generated &&
                glyphCenters.TryGetValue(effect, out var center))
            {
                offset = center;
            }
            var scale = image.sprite == null ? 1f : Mathf.Min(
                image.rectTransform.rect.width / image.sprite.rect.width,
                image.rectTransform.rect.height / image.sprite.rect.height);
            image.rectTransform.anchoredPosition = basePosition - offset * scale;
        }

        public static Sprite Get(CardEffectId effect)
        {
            Sprite cached;
            if (cache.TryGetValue(effect, out cached)) return cached;

            // The Astro Survivors icon set covers every effect; the vector
            // glyphs below stay as the fallback for anything it does not.
            var drawn = ArtLibrary.EffectIcon(effect);
            if (drawn != null)
            {
                // Every card tints its icon with the card's colour, which only
                // reads right on white artwork: a coloured source (ShieldIcon
                // is beige) came out in another colour.
                drawn = Monochrome(drawn);
                cache[effect] = drawn;
                importedArt.Add(effect);
                return drawn;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "Icon_" + effect;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var pixels = new Color32[Size * Size];
            var inkCount = 0f;
            var inkSum = Vector2.zero;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            var nx = (x + (sx + 0.5f) / Samples) / Size * 2f - 1f;
                            var ny = (y + (sy + 0.5f) / Samples) / Size * 2f - 1f;
                            if (Ink(effect, nx, ny)) hits++;
                        }
                    }
                    var coverage = hits / (float)(Samples * Samples);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                    if (coverage > 0f)
                    {
                        inkSum += new Vector2(x + 0.5f, y + 0.5f) * coverage;
                        inkCount += coverage;
                    }
                }
            }
            glyphCenters[effect] = inkCount > 0f
                ? inkSum / inkCount - new Vector2(Size * 0.5f, Size * 0.5f)
                : Vector2.zero;
            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f), Size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[effect] = sprite;
            return sprite;
        }

        /// <summary>
        /// A white copy of coloured artwork: each pixel keeps its alpha and its
        /// brightness relative to the brightest one, so the tint applied by the
        /// UI becomes the card's exact colour, inner shading included. White
        /// artwork is returned untouched. Reads the pixels through a
        /// RenderTexture, so the source does not need Read/Write enabled.
        /// </summary>
        private static Sprite Monochrome(Sprite source)
        {
            if (source == null || source.texture == null) return source;
            Color32[] pixels;
            var rect = source.textureRect;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return source;

            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D copy;
            try
            {
                Graphics.Blit(source.texture, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
                copy.Apply();
                pixels = copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }

            var saturation = 0f;
            var visible = 0;
            var brightest = 0f;
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a < 40) continue;
                var max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                var min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                saturation += max > 0 ? (max - min) / (float)max : 0f;
                brightest = Mathf.Max(brightest, Luminance(p));
                visible++;
            }
            if (visible == 0 || saturation / visible < ColouredArtSaturation || brightest <= 0f)
            {
                Object.Destroy(copy);
                return source;
            }

            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var value = (byte)Mathf.Clamp(Mathf.RoundToInt(Luminance(p) / brightest * 255f), 0, 255);
                pixels[i] = new Color32(value, value, value, p.a);
            }
            copy.SetPixels32(pixels);
            copy.wrapMode = TextureWrapMode.Clamp;
            copy.filterMode = source.texture.filterMode;
            copy.Apply();
            copy.name = source.name + "_Mono";
            copy.hideFlags = HideFlags.HideAndDontSave;

            var pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            var sprite = Sprite.Create(copy, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit,
                0, SpriteMeshType.FullRect, source.border);
            sprite.name = copy.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float Luminance(Color32 p) => 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;

        private static bool Ink(CardEffectId id, float x, float y)
        {
            // One weight for every line (see Stroke); filled dots stay as accents.
            const float t = Stroke;
            switch (id)
            {
                case CardEffectId.BouncingSnowball:
                    return Ring(x, y, 0.35f, t) || Line(x, y, -0.75f, -0.6f, -0.45f, -0.2f, t) ||
                        Line(x, y, 0.45f, 0.2f, 0.75f, 0.6f, t) || Line(x, y, -0.75f, 0.65f, 0.75f, 0.65f, t);
                case CardEffectId.GiantSnowball:
                    return Ring(x, y, 0.72f, t) || Ring(x, y, 0.51f, t) ||
                        Line(x, y, -0.26f, 0f, 0.26f, 0f, t) || Line(x, y, 0f, -0.26f, 0f, 0.26f, t);
                case CardEffectId.FrostCircle:
                    return Ring(x, y, 0.72f, t) || Ring(x, y, 0.42f, t) || Rays(x, y, t, 0.1f, 0.29f);
                case CardEffectId.GiftBombs:
                    return Box(x, y + 0.15f, 0.58f, 0.45f, t) || Line(x, y, 0f, -0.6f, 0f, 0.32f, t) ||
                        Line(x, y, -0.58f, 0.03f, 0.58f, 0.03f, t) ||
                        Ring(x + 0.22f, y - 0.43f, 0.22f, t) || Ring(x - 0.22f, y - 0.43f, 0.22f, t);
                case CardEffectId.IceShards:
                    return Line(x, y, 0f, -0.75f, -0.2f, 0.25f, t) || Line(x, y, -0.2f, 0.25f, 0f, 0.8f, t) ||
                        Line(x, y, 0f, 0.8f, 0.2f, 0.25f, t) || Line(x, y, 0.2f, 0.25f, 0f, -0.75f, t) ||
                        Line(x, y, -0.7f, 0.35f, -0.43f, -0.45f, t) || Line(x, y, 0.7f, 0.35f, 0.43f, -0.45f, t);
                case CardEffectId.Snowstorm:
                    return Line(x, y, -0.65f, 0.65f, 0.65f, 0.65f, t) ||
                        Line(x, y, -0.55f, 0.3f, -0.75f, -0.5f, t) ||
                        Line(x, y, 0f, 0.3f, -0.2f, -0.65f, t) ||
                        Line(x, y, 0.55f, 0.3f, 0.35f, -0.5f, t);
                case CardEffectId.ChainLightning:
                    return Line(x, y, 0.25f, 0.8f, -0.3f, 0.05f, t) ||
                        Line(x, y, -0.3f, 0.05f, 0.3f, 0.05f, t) ||
                        Line(x, y, 0.3f, 0.05f, -0.25f, -0.8f, t);
                case CardEffectId.SiegeRound:
                    // Heavy shell breaking a barrier; avoid the radial burst used by DoubleAttack.
                    return Line(x, y, -0.22f, -0.45f, -0.22f, 0.35f, t) ||
                        Line(x, y, 0.22f, -0.45f, 0.22f, 0.35f, t) ||
                        Line(x, y, -0.22f, 0.35f, 0f, 0.78f, t) ||
                        Line(x, y, 0.22f, 0.35f, 0f, 0.78f, t) ||
                        Line(x, y, -0.22f, -0.45f, 0.22f, -0.45f, t) ||
                        Line(x, y, 0f, -0.65f, 0f, -0.8f, t) ||
                        Line(x, y, -0.78f, 0.05f, -0.5f, 0.05f, t) ||
                        Line(x, y, -0.5f, 0.05f, -0.4f, 0.28f, t) ||
                        Line(x, y, 0.78f, 0.05f, 0.5f, 0.05f, t) ||
                        Line(x, y, 0.5f, 0.05f, 0.4f, 0.28f, t);
                case CardEffectId.GuaranteedCritical:
                    return Ring(x, y, 0.74f, t) ||
                        Rays(x, y, t, 0.18f, 0.55f) || Disc(x, y, 0.12f);
                case CardEffectId.ArmorPlating:
                    return Box(x, y, 0.62f, 0.7f, t) ||
                        Line(x, y, -0.4f, -0.45f, 0.4f, 0.45f, t) ||
                        Line(x, y, -0.4f, 0.45f, 0.4f, -0.45f, t);
                case CardEffectId.BossHunter:
                    return Ring(x, y, 0.5f, t) || Disc(x, y, 0.12f) ||
                        Line(x, y, 0f, 0.38f, 0f, 0.8f, t) || Line(x, y, 0f, -0.38f, 0f, -0.8f, t) ||
                        Line(x, y, 0.38f, 0f, 0.8f, 0f, t) || Line(x, y, -0.38f, 0f, -0.8f, 0f, t);
                case CardEffectId.DoubleAttack:
                    return Arrow(x + 0.28f, y, t) || Arrow(x - 0.28f, y, t);
                case CardEffectId.Shield:
                    return Line(x, y, -0.62f, 0.58f, 0f, 0.82f, t) ||
                        Line(x, y, 0f, 0.82f, 0.62f, 0.58f, t) ||
                        Line(x, y, -0.62f, 0.58f, -0.48f, -0.26f, t) ||
                        Line(x, y, 0.62f, 0.58f, 0.48f, -0.26f, t) ||
                        Line(x, y, -0.48f, -0.26f, 0f, -0.78f, t) ||
                        Line(x, y, 0.48f, -0.26f, 0f, -0.78f, t);
                case CardEffectId.Missiles:
                    return Ring(x, y + 0.2f, 0.38f, t) || Line(x, y, 0f, -0.45f, 0f, 0.18f, t) ||
                        Line(x, y, -0.28f, -0.15f, -0.52f, -0.5f, t) ||
                        Line(x, y, 0.28f, -0.15f, 0.52f, -0.5f, t) ||
                        Line(x, y, 0f, -0.42f, 0f, -0.82f, t);
                case CardEffectId.BlackHole:
                    return Ring(x, y, 0.7f, t) || Ring(x, y, 0.42f, t) || Disc(x, y, 0.13f);
                case CardEffectId.PiercingShot:
                    return HorizontalArrow(x, y, t) || Ring(x + 0.28f, y, 0.31f, t) ||
                        Ring(x - 0.32f, y, 0.31f, t);
                case CardEffectId.EngineOverload:
                    return Chevron(x + 0.34f, y, t) || Chevron(x, y, t) || Chevron(x - 0.34f, y, t);
                case CardEffectId.ImpactCharge:
                    // A healing cross shooting four small arrows outwards.
                    return Plus(x, y, 0.28f, t) ||
                        OutArrow(x, y, 0f, 1f, t) || OutArrow(x, y, 0f, -1f, t) ||
                        OutArrow(x, y, 1f, 0f, t) || OutArrow(x, y, -1f, 0f, t);
                case CardEffectId.WildVolley:
                    return AngledArrow(x, y, -0.48f, t) || Arrow(x, y, t) || AngledArrow(x, y, 0.48f, t);
                case CardEffectId.BattleFury:
                    return Ring(x, y + 0.18f, 0.28f, t) ||
                        Line(x, y, -0.38f, 0.05f, 0f, -0.7f, t) ||
                        Line(x, y, 0.38f, 0.05f, 0f, -0.7f, t) ||
                        Line(x, y, -0.14f, 0.52f, 0f, 0.12f, t);
                case CardEffectId.RepulsionField:
                    return Ring(x, y, 0.22f, t) || Ring(x, y, 0.48f, t) || Ring(x, y, 0.76f, t);
                case CardEffectId.EmergencyRepair:
                    return Plus(x, y, 0.7f, t);
                case CardEffectId.DebrisWall:
                    return Box(x, y, 0.74f, 0.56f, t) || Line(x, y, -0.74f, 0f, 0.74f, 0f, t) ||
                        Line(x, y, -0.34f, 0f, -0.34f, 0.56f, t) || Line(x, y, 0.34f, 0f, 0.34f, -0.56f, t);
                case CardEffectId.CoreOverload:
                    return Ring(x, y, 0.32f, t) || Disc(x, y, 0.11f) || Rays(x, y, t, 0.46f, 0.78f);
                case CardEffectId.DualGravityWell:
                    return Ring(x + 0.34f, y, 0.42f, t) || Ring(x - 0.34f, y, 0.42f, t) ||
                        Disc(x + 0.34f, y, 0.1f) || Disc(x - 0.34f, y, 0.1f);
                case CardEffectId.TemporalDistortion:
                    return Line(x, y, -0.5f, 0.72f, 0.5f, 0.72f, t) ||
                        Line(x, y, -0.5f, -0.72f, 0.5f, -0.72f, t) ||
                        Line(x, y, -0.48f, 0.65f, 0.35f, -0.62f, t) ||
                        Line(x, y, 0.48f, 0.65f, -0.35f, -0.62f, t);
                case CardEffectId.SpecialCooldown:
                    return Ring(x, y, 0.65f, t) ||
                        Line(x, y, 0f, 0f, 0f, 0.45f, t) ||
                        Line(x, y, 0f, 0f, 0.35f, -0.2f, t);
                case CardEffectId.TemporalParalysis:
                    return Box(x + 0.28f, y, 0.12f, 0.65f, t) ||
                        Box(x - 0.28f, y, 0.12f, 0.65f, t);
                case CardEffectId.ChainDetonation:
                    return Ring(x - 0.3f, y + 0.25f, 0.36f, t) ||
                        Ring(x + 0.3f, y - 0.25f, 0.36f, t) ||
                        Line(x, y, -0.12f, 0.08f, 0.12f, -0.08f, t);
                case CardEffectId.SupportDrones:
                    return Disc(x + 0.42f, y + 0.38f, 0.16f) ||
                        Disc(x - 0.42f, y + 0.38f, 0.16f) ||
                        Disc(x, y - 0.4f, 0.16f) || Ring(x, y, 0.55f, t);
                case CardEffectId.OrbitalBlades:
                    return Ring(x, y, 0.62f, t) || Disc(x, y, 0.12f) ||
                        Blade(x, y, 0f, 0.62f, t) || Blade(x, y, 2.094f, 0.62f, t) || Blade(x, y, 4.189f, 0.62f, t);
                case CardEffectId.Shrapnel:
                    return Disc(x, y, 0.16f) || Rays(x, y, t, 0.34f, 0.8f);
                case CardEffectId.DiagonalShot:
                    return AngledArrow(x, y, -0.62f, t) || Arrow(x, y, t) || AngledArrow(x, y, 0.62f, t);
                case CardEffectId.PlasmaAura:
                    return Disc(x, y, 0.2f) || Ring(x, y, 0.48f, t) || Ring(x, y, 0.76f, t);
                case CardEffectId.SolarNova:
                    return Ring(x, y, 0.3f, t) || Disc(x, y, 0.12f) || Rays(x, y, t, 0.46f, 0.82f);
                case CardEffectId.NanoRepair:
                    return Ring(x, y, 0.72f, t) ||
                        Plus(x, y, 0.42f, t);
                case CardEffectId.PhoenixCore:
                    // A rising arrow with spread wings, inside a faint ring.
                    return Arrow(x, y, t) || Line(x, y, -0.5f, -0.5f, 0f, -0.18f, t) ||
                        Line(x, y, 0.5f, -0.5f, 0f, -0.18f, t) || Ring(x, y, 0.82f, t);
                case CardEffectId.KillStreak:
                    // Three stacked chevrons pointing up.
                    return Chevron(y - 0.36f, x, t) || Chevron(y, x, t) || Chevron(y + 0.36f, x, t);
                case CardEffectId.Thorns:
                    return Ring(x, y, 0.42f, t) || Rays(x, y, t, 0.56f, 0.86f);
                case CardEffectId.SpecialDuration:
                    return ShieldOutline(x, y, t) || Line(x, y, -0.22f, 0.05f, 0.22f, 0.05f, t) ||
                        Line(x, y, 0f, 0.05f, 0f, 0.35f, t);
                case CardEffectId.ShieldMend:
                    return ShieldOutline(x, y, t) ||
                        Plus(x, y - 0.05f, 0.3f, t);
                case CardEffectId.TemporalEcho:
                    return Ring(x - 0.2f, y, 0.42f, t) || Ring(x + 0.22f, y, 0.42f, t) || Disc(x - 0.2f, y, 0.1f);
                case CardEffectId.Overheal:
                    return Ring(x, y, 0.74f, t) || Ring(x, y, 0.56f, t) ||
                        Plus(x, y, 0.34f, t);
                case CardEffectId.VitalPulse:
                    return Ring(x, y, 0.5f, t) || Ring(x, y, 0.78f, t) ||
                        Plus(x, y, 0.26f, t);
                case CardEffectId.EmergencyProtocol:
                    return Box(x, y, 0.66f, 0.52f, t) ||
                        Plus(x, y, 0.32f, t);
                case CardEffectId.Railgun:
                    return Line(x, y, -0.8f, 0f, 0.8f, 0f, t) ||
                        Line(x, y, -0.55f, 0.3f, -0.55f, -0.3f, t) || Line(x, y, 0.05f, 0.3f, 0.05f, -0.3f, t) ||
                        Line(x, y, 0.62f, 0.22f, 0.82f, 0f, t) || Line(x, y, 0.62f, -0.22f, 0.82f, 0f, t);
                case CardEffectId.TargetMark:
                    return Ring(x, y, 0.5f, t) || Disc(x, y, 0.1f) ||
                        Line(x, y, 0.42f, 0.42f, 0.72f, 0.72f, t) || Line(x, y, -0.42f, 0.42f, -0.72f, 0.72f, t) ||
                        Line(x, y, 0.42f, -0.42f, 0.72f, -0.72f, t) || Line(x, y, -0.42f, -0.42f, -0.72f, -0.72f, t);
                case CardEffectId.AmplifyShockwave:
                    return Disc(x, y, 0.14f) || Ring(x, y, 0.38f, t) || Ring(x, y, 0.6f, t) ||
                        Ring(x, y, 0.82f, t);
                default:
                    return Ring(x, y, 0.55f, t);
            }
        }

        /// <summary>A small diamond blade on a ring at <paramref name="angle"/> (radians).</summary>
        private static bool Blade(float x, float y, float angle, float radius, float t)
        {
            var cx = Mathf.Cos(angle) * radius;
            var cy = Mathf.Sin(angle) * radius;
            var dx = x - cx;
            var dy = y - cy;
            // Rotate into the blade's frame (tangent along the orbit).
            var u = dx * -Mathf.Sin(angle) + dy * Mathf.Cos(angle);
            var v = dx * Mathf.Cos(angle) + dy * Mathf.Sin(angle);
            return Mathf.Abs(u) / 0.26f + Mathf.Abs(v) / 0.12f <= 1f;
        }

        private static bool ShieldOutline(float x, float y, float t)
        {
            return Line(x, y, -0.62f, 0.58f, 0f, 0.82f, t) ||
                Line(x, y, 0f, 0.82f, 0.62f, 0.58f, t) ||
                Line(x, y, -0.62f, 0.58f, -0.48f, -0.26f, t) ||
                Line(x, y, 0.62f, 0.58f, 0.48f, -0.26f, t) ||
                Line(x, y, -0.48f, -0.26f, 0f, -0.78f, t) ||
                Line(x, y, 0.48f, -0.26f, 0f, -0.78f, t);
        }

        /// <summary>A plus sign drawn with two strokes of half-length <paramref name="arm"/>.</summary>
        private static bool Plus(float x, float y, float arm, float t)
        {
            return Line(x, y, -arm, 0f, arm, 0f, t) || Line(x, y, 0f, -arm, 0f, arm, t);
        }

        /// <summary>A short arrow on the axis (dx, dy), from 0.45 to 0.82, pointing outwards.</summary>
        private static bool OutArrow(float x, float y, float dx, float dy, float t)
        {
            float px = -dy, py = dx;
            return Line(x, y, dx * 0.45f, dy * 0.45f, dx * 0.82f, dy * 0.82f, t) ||
                Line(x, y, dx * 0.82f, dy * 0.82f, dx * 0.66f + px * 0.13f, dy * 0.66f + py * 0.13f, t) ||
                Line(x, y, dx * 0.82f, dy * 0.82f, dx * 0.66f - px * 0.13f, dy * 0.66f - py * 0.13f, t);
        }

        private static bool Arrow(float x, float y, float t)
        {
            return Mathf.Abs(x) < t && y > -0.65f && y < 0.62f ||
                Line(x, y, 0f, 0.68f, -0.25f, 0.35f, t) ||
                Line(x, y, 0f, 0.68f, 0.25f, 0.35f, t);
        }

        private static bool HorizontalArrow(float x, float y, float t)
        {
            return Mathf.Abs(y) < t && x > -0.72f && x < 0.7f ||
                Line(x, y, 0.72f, 0f, 0.38f, 0.28f, t) ||
                Line(x, y, 0.72f, 0f, 0.38f, -0.28f, t);
        }

        private static bool AngledArrow(float x, float y, float angle, float t)
        {
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            return Arrow(x * c + y * s, -x * s + y * c, t);
        }

        private static bool Chevron(float x, float y, float t)
        {
            return Line(x, y, -0.18f, -0.62f, 0.18f, 0f, t) ||
                Line(x, y, 0.18f, 0f, -0.18f, 0.62f, t);
        }

        private static bool InwardArrow(float x, float y, float ox, float oy, float t)
        {
            var length = Mathf.Sqrt(ox * ox + oy * oy);
            var dx = ox / length;
            var dy = oy / length;
            return Line(x, y, ox, oy, dx * 0.16f, dy * 0.16f, t) ||
                Line(x, y, dx * 0.12f, dy * 0.12f, dx * 0.34f - dy * 0.18f, dy * 0.34f + dx * 0.18f, t) ||
                Line(x, y, dx * 0.12f, dy * 0.12f, dx * 0.34f + dy * 0.18f, dy * 0.34f - dx * 0.18f, t);
        }

        private static bool Rays(float x, float y, float t, float inner, float outer)
        {
            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI * 0.25f;
                if (Line(x, y, Mathf.Cos(a) * inner, Mathf.Sin(a) * inner,
                    Mathf.Cos(a) * outer, Mathf.Sin(a) * outer, t)) return true;
            }
            return false;
        }

        private static bool Line(float x, float y, float ax, float ay, float bx, float by, float thickness)
        {
            var abx = bx - ax;
            var aby = by - ay;
            var projection = Mathf.Clamp01(((x - ax) * abx + (y - ay) * aby) / (abx * abx + aby * aby));
            var dx = x - (ax + abx * projection);
            var dy = y - (ay + aby * projection);
            return dx * dx + dy * dy <= thickness * thickness;
        }

        private static bool Ring(float x, float y, float radius, float thickness)
        {
            return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - radius) <= thickness;
        }

        private static bool Disc(float x, float y, float radius)
        {
            return x * x + y * y <= radius * radius;
        }

        private static bool Box(float x, float y, float halfWidth, float halfHeight, float thickness)
        {
            // Centred on the edge, like Line and Ring: the full stroke is 2x thickness.
            return Mathf.Abs(x) <= halfWidth + thickness && Mathf.Abs(y) <= halfHeight + thickness &&
                (Mathf.Abs(Mathf.Abs(x) - halfWidth) <= thickness ||
                 Mathf.Abs(Mathf.Abs(y) - halfHeight) <= thickness);
        }
    }
}
