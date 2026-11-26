// AstroCrawler - procedural symbols for upgrade cards and the HUD.

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public static class CardIconFactory
    {
        private const int Size = 64;
        private static readonly Dictionary<CardEffectId, Sprite> cache =
            new Dictionary<CardEffectId, Sprite>();

        private static readonly Dictionary<CardEffectId, Vector2> glyphCenters =
            new Dictionary<CardEffectId, Vector2>();

        /// <summary>Centers the visible ink, compensating for asymmetric margins and visual weight.</summary>
        public static void CenterArtwork(Image image, CardEffectId effect, Vector2 basePosition)
        {
            var offset = Vector2.zero;
            // Alpha-weighted centres (>16/255), in original texture pixels.
            // Measured offline so imported textures do not need CPU readability.
            // Y is inverted because the source image rows run top to bottom.
            // These corrections apply only to the imported artwork, not fallback glyphs.
            if (image.sprite != null && image.sprite == ArtLibrary.EffectIcon(effect))
            {
                switch (effect)
                {
                    case CardEffectId.Missiles: offset = new Vector2(0.452f, -0.436f); break;
                    case CardEffectId.Shield: offset = new Vector2(-0.242f, 0.182f); break;
                    case CardEffectId.EngineOverload: offset = new Vector2(-8.258f, 0.0f); break;
                    case CardEffectId.WildVolley: offset = new Vector2(-0.314f, -0.138f); break;
                    case CardEffectId.BlackHole: offset = new Vector2(0.833f, 0.811f); break;
                    case CardEffectId.RepulsionField: offset = new Vector2(0.0f, -3.614f); break;
                    case CardEffectId.EmergencyRepair: offset = new Vector2(-0.001f, 32.723f); break;
                    case CardEffectId.DebrisWall: offset = new Vector2(0.925f, -5.194f); break;
                    case CardEffectId.ChainDetonation: offset = new Vector2(0.0f, -4.815f); break;
                    case CardEffectId.OrbitalStrike: offset = new Vector2(0.0f, 10.286f); break;
                    case CardEffectId.GravityMagnet: offset = new Vector2(0.002f, -27.133f); break;
                    case CardEffectId.Execution: offset = new Vector2(0.049f, 10.879f); break;
                    case CardEffectId.OverdriveFury: offset = new Vector2(-0.491f, 0.506f); break;
                    case CardEffectId.ParadoxRecharge: offset = new Vector2(4.588f, 1.152f); break;
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
                cache[effect] = drawn;
                return drawn;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "Icon_" + effect;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var pixels = new Color32[Size * Size];
            var inkCount = 0;
            var inkSum = Vector2.zero;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var nx = (x + 0.5f) / Size * 2f - 1f;
                    var ny = (y + 0.5f) / Size * 2f - 1f;
                    var visible = Ink(effect, nx, ny);
                    pixels[y * Size + x] = visible
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                    if (visible)
                    {
                        inkSum += new Vector2(x + 0.5f, y + 0.5f);
                        inkCount++;
                    }
                }
            }
            glyphCenters[effect] = inkCount > 0
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

        private static bool Ink(CardEffectId id, float x, float y)
        {
            const float t = 0.095f;
            switch (id)
            {
                case CardEffectId.BouncingSnowball:
                    return Ring(x, y, 0.35f, t) || Line(x, y, -0.75f, -0.6f, -0.45f, -0.2f, t) ||
                        Line(x, y, 0.45f, 0.2f, 0.75f, 0.6f, t) || Line(x, y, -0.75f, 0.65f, 0.75f, 0.65f, t);
                case CardEffectId.GiantSnowball:
                    return Ring(x, y, 0.72f, t) || Ring(x, y, 0.51f, t * 0.6f) ||
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
                    return Ring(x, y, 0.74f, t * 0.65f) ||
                        Rays(x, y, t, 0.18f, 0.55f) || Disc(x, y, 0.12f);
                case CardEffectId.ArmorPlating:
                    return Box(x, y, 0.62f, 0.7f, t) ||
                        Line(x, y, -0.4f, -0.45f, 0.4f, 0.45f, t) ||
                        Line(x, y, -0.4f, 0.45f, 0.4f, -0.45f, t);
                case CardEffectId.BossHunter:
                    return Ring(x, y, 0.5f, t) || Disc(x, y, 0.12f) ||
                        Mathf.Abs(x) < t && Mathf.Abs(y) > 0.38f && Mathf.Abs(y) < 0.8f ||
                        Mathf.Abs(y) < t && Mathf.Abs(x) > 0.38f && Mathf.Abs(x) < 0.8f;
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
                    return Ring(x, y + 0.2f, 0.38f, t) || Mathf.Abs(x) < t && y > -0.45f ||
                        Line(x, y, -0.28f, -0.15f, -0.52f, -0.5f, t) ||
                        Line(x, y, 0.28f, -0.15f, 0.52f, -0.5f, t) ||
                        Line(x, y, 0f, -0.42f, 0f, -0.82f, t * 1.4f);
                case CardEffectId.BlackHole:
                    return Ring(x, y, 0.7f, t) || Ring(x, y, 0.42f, t) || Disc(x, y, 0.13f);
                case CardEffectId.PiercingShot:
                    return HorizontalArrow(x, y, t) || Ring(x + 0.28f, y, 0.31f, t * 0.75f) ||
                        Ring(x - 0.32f, y, 0.31f, t * 0.75f);
                case CardEffectId.EngineOverload:
                    return Chevron(x + 0.34f, y, t) || Chevron(x, y, t) || Chevron(x - 0.34f, y, t);
                case CardEffectId.ImpactCharge:
                    return Mathf.Abs(x) < 0.09f && Mathf.Abs(y) < 0.28f ||
                        Mathf.Abs(y) < 0.09f && Mathf.Abs(x) < 0.28f ||
                        Arrow(x * 1.8f, (y - 0.61f) * 3f, t * 1.2f) ||
                        Arrow(-x * 1.8f, (-y - 0.61f) * 3f, t * 1.2f) ||
                        Arrow(-y * 1.8f, (x - 0.61f) * 3f, t * 1.2f) ||
                        Arrow(y * 1.8f, (-x - 0.61f) * 3f, t * 1.2f);
                case CardEffectId.WildVolley:
                    return AngledArrow(x, y, -0.48f, t) || Arrow(x, y, t) || AngledArrow(x, y, 0.48f, t);
                case CardEffectId.BattleFury:
                    return Ring(x, y + 0.18f, 0.28f, t) ||
                        Line(x, y, -0.38f, 0.05f, 0f, -0.7f, t * 1.25f) ||
                        Line(x, y, 0.38f, 0.05f, 0f, -0.7f, t * 1.25f) ||
                        Line(x, y, -0.14f, 0.52f, 0f, 0.12f, t);
                case CardEffectId.RepulsionField:
                    return Ring(x, y, 0.22f, t) || Ring(x, y, 0.48f, t) || Ring(x, y, 0.76f, t);
                case CardEffectId.EmergencyRepair:
                    return Mathf.Abs(x) < 0.16f && Mathf.Abs(y) < 0.7f ||
                        Mathf.Abs(y) < 0.16f && Mathf.Abs(x) < 0.7f;
                case CardEffectId.DebrisWall:
                    return Box(x, y, 0.74f, 0.56f, t) || Mathf.Abs(y) < t ||
                        Mathf.Abs(x + 0.34f) < t && y > 0f || Mathf.Abs(x - 0.34f) < t && y < 0f;
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
                        Line(x, y, -0.12f, 0.08f, 0.12f, -0.08f, t * 1.2f);
                case CardEffectId.SupportDrones:
                    return Disc(x + 0.42f, y + 0.38f, 0.16f) ||
                        Disc(x - 0.42f, y + 0.38f, 0.16f) ||
                        Disc(x, y - 0.4f, 0.16f) || Ring(x, y, 0.55f, t);
                case CardEffectId.OrbitalBlades:
                    return Ring(x, y, 0.62f, t * 0.6f) || Disc(x, y, 0.12f) ||
                        Blade(x, y, 0f, 0.62f, t) || Blade(x, y, 2.094f, 0.62f, t) || Blade(x, y, 4.189f, 0.62f, t);
                case CardEffectId.Shrapnel:
                    return Disc(x, y, 0.16f) || Rays(x, y, t, 0.34f, 0.8f);
                case CardEffectId.DiagonalShot:
                    return AngledArrow(x, y, -0.62f, t) || Arrow(x, y, t) || AngledArrow(x, y, 0.62f, t);
                case CardEffectId.PlasmaAura:
                    return Disc(x, y, 0.2f) || Ring(x, y, 0.48f, t * 0.8f) || Ring(x, y, 0.76f, t * 0.6f);
                case CardEffectId.SolarNova:
                    return Ring(x, y, 0.3f, t) || Disc(x, y, 0.12f) || Rays(x, y, t, 0.46f, 0.82f);
                case CardEffectId.NanoRepair:
                    return Ring(x, y, 0.72f, t * 0.8f) ||
                        Mathf.Abs(x) < 0.11f && Mathf.Abs(y) < 0.42f || Mathf.Abs(y) < 0.11f && Mathf.Abs(x) < 0.42f;
                case CardEffectId.PhoenixCore:
                    // A rising arrow with spread wings, inside a faint ring.
                    return Arrow(x, y, t) || Line(x, y, -0.5f, -0.5f, 0f, -0.18f, t) ||
                        Line(x, y, 0.5f, -0.5f, 0f, -0.18f, t) || Ring(x, y, 0.82f, t * 0.5f);
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
                        Mathf.Abs(x) < 0.08f && Mathf.Abs(y - 0.05f) < 0.3f || Mathf.Abs(y - 0.05f) < 0.08f && Mathf.Abs(x) < 0.3f;
                case CardEffectId.TemporalEcho:
                    return Ring(x - 0.2f, y, 0.42f, t) || Ring(x + 0.22f, y, 0.42f, t * 0.6f) || Disc(x - 0.2f, y, 0.1f);
                case CardEffectId.Overheal:
                    return Ring(x, y, 0.74f, t * 0.7f) || Ring(x, y, 0.56f, t * 0.5f) ||
                        Mathf.Abs(x) < 0.1f && Mathf.Abs(y) < 0.34f || Mathf.Abs(y) < 0.1f && Mathf.Abs(x) < 0.34f;
                case CardEffectId.VitalPulse:
                    return Ring(x, y, 0.5f, t * 0.7f) || Ring(x, y, 0.78f, t * 0.5f) ||
                        Mathf.Abs(x) < 0.09f && Mathf.Abs(y) < 0.26f || Mathf.Abs(y) < 0.09f && Mathf.Abs(x) < 0.26f;
                case CardEffectId.EmergencyProtocol:
                    return Box(x, y, 0.66f, 0.52f, t) ||
                        Mathf.Abs(x) < 0.1f && Mathf.Abs(y) < 0.32f || Mathf.Abs(y) < 0.1f && Mathf.Abs(x) < 0.32f;
                case CardEffectId.Railgun:
                    return Mathf.Abs(y) < t * 1.3f && x > -0.8f && x < 0.8f ||
                        Line(x, y, -0.55f, 0.3f, -0.55f, -0.3f, t) || Line(x, y, 0.05f, 0.3f, 0.05f, -0.3f, t) ||
                        Line(x, y, 0.62f, 0.22f, 0.82f, 0f, t) || Line(x, y, 0.62f, -0.22f, 0.82f, 0f, t);
                case CardEffectId.TargetMark:
                    return Ring(x, y, 0.5f, t) || Disc(x, y, 0.1f) ||
                        Line(x, y, 0.42f, 0.42f, 0.72f, 0.72f, t) || Line(x, y, -0.42f, 0.42f, -0.72f, 0.72f, t) ||
                        Line(x, y, 0.42f, -0.42f, 0.72f, -0.72f, t) || Line(x, y, -0.42f, -0.42f, -0.72f, -0.72f, t);
                case CardEffectId.AmplifyShockwave:
                    return Disc(x, y, 0.14f) || Ring(x, y, 0.38f, t * 0.8f) || Ring(x, y, 0.6f, t * 0.7f) ||
                        Ring(x, y, 0.82f, t * 0.5f);
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
            return Mathf.Abs(x) <= halfWidth && Mathf.Abs(y) <= halfHeight &&
                (Mathf.Abs(Mathf.Abs(x) - halfWidth) <= thickness ||
                 Mathf.Abs(Mathf.Abs(y) - halfHeight) <= thickness);
        }
    }
}
