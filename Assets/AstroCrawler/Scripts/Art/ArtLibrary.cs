// AstroCrawler - the bridge between Resources and the game.
//
// Every sprite the game shows now comes from the Astro Survivors art set under
// Resources/AstroCrawler. Nothing here is mandatory: each accessor falls back to
// the procedural SpriteFactory shape it replaced, so a missing or renamed file
// degrades to the old look instead of rendering an invisible object.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public static class ArtLibrary
    {
        public const string ArtPath = "AstroCrawler/Art/";
        public const string UiPath = "AstroCrawler/UI/";

        // Misses are cached as nulls on purpose: an accessor called every frame
        // must not hit Resources.Load repeatedly just because a file is absent.
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite Load(string resourcePath)
        {
            Sprite cached;
            if (sprites.TryGetValue(resourcePath, out cached))
            {
                return cached;
            }
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                // Textures imported in "Multiple" sprite mode keep their one sprite as a sub-asset.
                var all = Resources.LoadAll<Sprite>(resourcePath);
                if (all != null && all.Length > 0) sprite = all[0];
            }
            sprites[resourcePath] = sprite;
            return sprite;
        }

        public static Sprite Art(string name)
        {
            return Load(ArtPath + name);
        }

        public static Sprite Ui(string name)
        {
            return Load(UiPath + name);
        }

        /// <summary>Art sprite, or the given procedural shape when it is missing.</summary>
        public static Sprite ArtOr(string name, Sprite fallback)
        {
            var sprite = Art(name);
            return sprite != null ? sprite : fallback;
        }

        public static Sprite UiOr(string name, Sprite fallback)
        {
            var sprite = Ui(name);
            return sprite != null ? sprite : fallback;
        }

        /// <summary>Clears the cache so a domain reload cannot serve dead sprites.</summary>
        public static void Clear()
        {
            sprites.Clear();
        }

        // ------------------------------------------------------------------
        // Arena
        // ------------------------------------------------------------------

        public static Sprite Ship { get { return ArtOr("ship", SpriteFactory.Polygon(3, -90f)); } }
        private static bool IsChristmasShip(string id) => id == "noel" || id == "arvore" || id == "neve";
        public static Sprite ShipFor(string id) => IsChristmasShip(id)
            ? ArtOr("ship_" + id, SpriteFactory.ChristmasHull()) : Ship;
        public static Color ShipTint(string id, Color accent) => IsChristmasShip(id) ? Color.white : accent;
        public static Sprite ShipOutline { get { return ArtOr("wire_ship", null); } }
        public static Sprite Bullet { get { return ArtOr("bullet", SpriteFactory.Disc(0.5f)); } }
        public static Sprite MissileBody { get { return ArtOr("missile", SpriteFactory.Polygon(3, -90f)); } }
        public static Sprite Glow { get { return ArtOr("glow_circle", SpriteFactory.Disc(0.9f)); } }
        public static Sprite SoftCircle { get { return ArtOr("soft_circle", SpriteFactory.Disc(0.6f)); } }
        public static Sprite RingSprite { get { return ArtOr("ring", SpriteFactory.Ring(0.14f)); } }
        public static Sprite DashedRing { get { return ArtOr("dashed_ring", SpriteFactory.Ring(0.1f)); } }
        public static Sprite WireRing { get { return ArtOr("wire_ring", SpriteFactory.Ring(0.08f)); } }
        public static Sprite WireFrame { get { return ArtOr("wire_frame", SpriteFactory.Polygon(4, 45f)); } }
        public static Sprite Pixel { get { return ArtOr("pixel", SpriteFactory.White()); } }
        public static Sprite AddGlow { get { return ArtOr("Add Glow", SpriteFactory.Disc(0.95f)); } }
        // There is deliberately no Logo accessor. The only logo in the imported
        // set reads "ASTRO SURVIVOR", and the title screen sets its own name as
        // type instead; the file itself was moved out to _Archive/.

        /// <summary>One of the five hand-drawn asteroid silhouettes.</summary>
        public static Sprite Asteroid(int variant)
        {
            var sprite = Art("asteroid_" + Mathf.Abs(variant) % 5);
            return sprite != null ? sprite : SpriteFactory.Polygon(7, 0f);
        }

        /// <summary>
        /// The silhouette for an enemy kind. Meteors get a random-but-stable
        /// asteroid variant so a wave of twelve rocks does not look stamped.
        /// </summary>
        public static Sprite Enemy(EnemyKind kind, int variantSeed)
        {
            switch (kind)
            {
                case EnemyKind.MeteorSmall:
                    return Asteroid(variantSeed);
                case EnemyKind.MeteorMedium:
                    return Asteroid(variantSeed + 1);
                case EnemyKind.MeteorLarge:
                    return Asteroid(variantSeed + 2);
                case EnemyKind.Scout:
                    return ArtOr("wire_diamond", SpriteFactory.Polygon(3, -90f));
                case EnemyKind.Interceptor:
                    return ArtOr("diamond", SpriteFactory.Polygon(3, -90f));
                case EnemyKind.Frigate:
                    return ArtOr("wire_hex", SpriteFactory.Polygon(6, 0f));
                case EnemyKind.AberrantCore:
                    return ArtOr("hex_disc", SpriteFactory.Polygon(6, 0f));
                case EnemyKind.Cocoon:
                    return ArtOr("poly_disc", SpriteFactory.Polygon(8, 0f));
                case EnemyKind.BossOrb:
                    return ArtOr("glow_circle", CircleFilled);
                default:
                    return Asteroid(variantSeed);
            }
        }

        /// <summary>
        /// The uniform scale that makes a sprite's largest side span
        /// <paramref name="diameter"/> world units.
        ///
        /// The procedural shapes were authored at exactly one world unit, but
        /// the imported art has its own pixel sizes and pixels-per-unit, so a
        /// raw scale of "radius * 2" would size every enemy differently. This
        /// keeps enemies.json's radius authoritative whichever sprite is used.
        /// </summary>
        public static float ScaleToDiameter(Sprite sprite, float diameter)
        {
            if (sprite == null)
            {
                return diameter;
            }
            var size = sprite.bounds.size;
            var largest = Mathf.Max(size.x, size.y);
            return largest <= 0.0001f ? diameter : diameter / largest;
        }

        /// <summary>True when the sprite is a rock rather than a directed hull.</summary>
        public static bool IsMeteor(EnemyKind kind)
        {
            return kind == EnemyKind.MeteorSmall || kind == EnemyKind.MeteorMedium ||
                kind == EnemyKind.MeteorLarge;
        }

        // ------------------------------------------------------------------
        // Interface
        // ------------------------------------------------------------------

        /// <summary>Sci-fi cut-corner frame, nine-sliced (26px border at 300 ppu).</summary>
        public static Sprite Frame { get { return Ui("Cut Frame Filled Big (300ppu)"); } }
        public static Sprite FrameGlow { get { return Ui("Cut Frame Glow Big - 6px (250ppu)"); } }
        public static Sprite FrameFilledGlow { get { return Ui("Cut Frame Filled Glow Big (200ppu)"); } }
        public static Sprite GradientPanel { get { return Ui("Gradient Background"); } }
        public static Sprite GradientPanel2 { get { return Ui("Gradient Background 2"); } }
        public static Sprite CircleFilled { get { return UiOr("Circle Filled", SpriteFactory.Disc(0.02f)); } }
        public static Sprite CircleOutline { get { return UiOr("Circle Outline - Stroke 5px", SpriteFactory.Ring(0.12f)); } }
        public static Sprite CircleDash { get { return UiOr("Circle Outline Dash - Stroke 3px", SpriteFactory.Ring(0.1f)); } }
        public static Sprite CircleGlow { get { return UiOr("Circle Filled Glow", SpriteFactory.Disc(0.9f)); } }

        public static Sprite IconPlay { get { return Ui("Play"); } }
        public static Sprite IconRetry { get { return Ui("Refresh"); } }
        public static Sprite IconHome { get { return Ui("Home"); } }
        public static Sprite IconCheck { get { return Ui("Check"); } }
        public static Sprite IconCancel { get { return Ui("Cancel"); } }
        public static Sprite IconHud { get { return Ui("HUD"); } }
        public static Sprite IconSettings { get { return Ui("Settings"); } }
        public static Sprite IconLock { get { return Ui("Lock"); } }
        public static Sprite IconCloudSave { get { return SpriteFactory.CloudSave(); } }
        public static Sprite IconNoAds { get { return SpriteFactory.NoAds(); } }
        public static Sprite IconPremium { get { return SpriteFactory.Premium(); } }
        public static Sprite IconBack { get { return Ui("Arrow Left"); } }
        public static Sprite IconMusic { get { return Ui("Music"); } }
        public static Sprite IconLanguage { get { return Ui("Language"); } }

        // From the Shift sci-fi UI kit (same family and stroke as the icons
        // above, which is why they sit together without a style clash).
        public static Sprite IconNext { get { return Ui("Arrow Right"); } }
        public static Sprite IconAudio { get { return Ui("Audio"); } }
        public static Sprite IconStages { get { return UiOr("Campaign", Art("PictoIcon_Compass_1")); } }
        public static Sprite IconStar { get { return Ui("Star"); } }
        public static Sprite IconStarFilled { get { return Ui("Star Filled"); } }
        public static Sprite IconSwap { get { return Ui("Swap"); } }
        public static Sprite IconDeck { get { return Ui("Tutorial"); } }
        public static Sprite IconShield { get { return UiOr("Armor Glow", Art("ShieldIcon")); } }

        /// <summary>The currency mark: credits, costs and the income upgrade.</summary>
        public static Sprite IconCredits { get { return Art("PictoIcon_Jewel_6"); } }
        public static Sprite IconUpgrades { get { return Art("ic_thruster"); } }

        /// <summary>Filled circle for round icon buttons (Shift, 9-sliced).</summary>
        public static Sprite RoundFill { get { return UiOr("Rounded Circle (250ppu)", CircleFilled); } }
        public static Sprite RoundRing { get { return UiOr("Rounded Circle Outline - Stroke 6px (225ppu)", CircleOutline); } }

        /// <summary>
        /// Round icon buttons: the sci-fi ring "Icon Button 07" (white, tinted by
        /// the caller) over a disc that fills exactly its inner circle.
        /// </summary>
        public static Sprite IconRingFrame { get { return ArtOr("IconButton07White_2", RoundRing); } }
        public static Sprite IconRingFill { get { return ArtOr("IconButton07Fill", RoundFill); } }

        /// <summary>The ship's ability button: hexagon "Icon Button 04" over a matching hexagonal face.</summary>
        public static Sprite IconHexFrame { get { return ArtOr("IconButton04White_2", RoundRing); } }
        public static Sprite IconHexFill { get { return ArtOr("IconButton04Fill", RoundFill); } }

        /// <summary>Horizontal pill with round caps: badges, toggles, cost tags.</summary>
        public static Sprite Pill { get { return Ui("Rounded Filled (300ppu)"); } }
        public static Sprite FrameOutline { get { return Ui("Cut Frame - 3px"); } }
        public static Sprite CornerGlow { get { return Ui("Cut Frame Corner Glow"); } }
        public static Sprite ShineStreak { get { return Ui("Shiny Move"); } }
        public static Sprite GradientTop { get { return Ui("Gradient Top"); } }
        public static Sprite GradientBottom { get { return Ui("Gradient Bottom"); } }
        public static Sprite SoftGlow { get { return UiOr("Background Glow", CircleGlow); } }
        public static Sprite LensFlare { get { return Ui("Lens Flare"); } }

        /// <summary>The symbol for each permanent (between-runs) ship attribute.</summary>
        public static Sprite PassiveIcon(PassiveUpgrade upgrade)
        {
            switch (upgrade)
            {
                case PassiveUpgrade.FireRate: return Art("ic_acelerador");
                case PassiveUpgrade.Damage: return Art("ic_blaster");
                case PassiveUpgrade.Hull: return Art("PictoIcon_Life");
                case PassiveUpgrade.Regeneration: return Art("ic_retry");
                case PassiveUpgrade.Evasion: return Art("ic_traseiro");
                case PassiveUpgrade.Income: return IconCredits;
                case PassiveUpgrade.CriticalChance: return Art("ic_estilhaco");
                default: return Art("PictoIcon_Energy");
            }
        }

        /// <summary>Hull integrity pip used beside the HP bar.</summary>
        public static Sprite IconLife { get { return Art("PictoIcon_Life"); } }
        public static Sprite IconEnemyLevel { get { return Art("PictoIcon_EnemyLevel"); } }
        public static Sprite IconBossLevel { get { return Art("PictoIcon_BossLevel"); } }
        public static Sprite IconEnergy { get { return Art("PictoIcon_Energy"); } }

        // ------------------------------------------------------------------
        // Upgrade symbols
        // ------------------------------------------------------------------

        /// <summary>
        /// The drawn symbol for an upgrade. Returns null when the set has no
        /// match, which lets CardIconFactory fall back to its vector glyph.
        /// </summary>
        public static Sprite EffectIcon(CardEffectId effect)
        {
            switch (effect)
            {
                case CardEffectId.DoubleAttack: return Art("ic_barragem");
                case CardEffectId.PiercingShot: return Art("ic_calibre");
                case CardEffectId.Missiles: return Art("ic_missile");
                case CardEffectId.Shield: return Art("ShieldIcon");
                case CardEffectId.EngineOverload: return Art("ic_acelerador");
                case CardEffectId.ImpactCharge: return null; // Healing-triggered radial volley glyph.
                case CardEffectId.WildVolley: return Art("ic_estilhaco");
                case CardEffectId.BattleFury: return Art("ic_infinity");
                case CardEffectId.BlackHole: return Art("ic_nucleo");
                case CardEffectId.RepulsionField: return Art("ic_orbital");
                case CardEffectId.EmergencyRepair: return Art("PictoIcon_Life");
                case CardEffectId.DebrisWall: return Art("ic_nevoa");
                case CardEffectId.CoreOverload: return Art("ic_sobrecarga");
                case CardEffectId.DualGravityWell: return Art("ic_nucleo_denso");
                case CardEffectId.TemporalDistortion: return Art("ic_gear");
                case CardEffectId.TemporalParalysis: return null; // Procedural pause glyph.
                case CardEffectId.ChainDetonation: return Art("ic_blaster");
                case CardEffectId.SupportDrones: return null;
                case CardEffectId.OrbitalStrike: return Art("ic_tractor");
                case CardEffectId.GravityMagnet: return Art("PictoIcon_Magnet");
                case CardEffectId.Execution: return Art("PictoIcon_EnemyLevel");
                case CardEffectId.OverdriveFury: return Art("PictoIcon_Energy");
                case CardEffectId.TemporalRift: return Art("ic_sobrecarga");
                case CardEffectId.ParadoxRecharge: return Art("ic_retry");
                default: return null; // The other new cards use CardIconFactory glyphs.
            }
        }

        // ------------------------------------------------------------------
        // Materials
        // ------------------------------------------------------------------

        private static Material spriteMaterial;

        /// <summary>
        /// The unlit sprite material used by trail renderers.
        ///
        /// This must be a real asset, not a material built at runtime: a
        /// HideAndDontSave material cannot be serialized into a prefab, so a
        /// prefab that referenced one came back with a null material and Unity
        /// drew it in magenta. TrailSprite.mat ships with the art set and uses
        /// the built-in Sprites/Default shader.
        /// </summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial != null)
                {
                    return spriteMaterial;
                }

                spriteMaterial = Resources.Load<Material>(ArtPath + "TrailSprite");
                if (spriteMaterial != null)
                {
                    return spriteMaterial;
                }

                // Only reached if the asset is missing; good enough at runtime,
                // but it will not survive being saved into a prefab.
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                }
                if (shader == null)
                {
                    return null;
                }

                spriteMaterial = new Material(shader);
                spriteMaterial.hideFlags = HideFlags.HideAndDontSave;
                return spriteMaterial;
            }
        }
    }
}
