// AstroCrawler - tech-art pass on the ship: a soft bloom around the
// hull, a flickering engine flame with exhaust, and the look of each ship's
// shot (ShotLook) so every hull fires something recognisable.

using UnityEngine;

namespace AstroCrawler
{
    public partial class PlayerShip
    {
        private SpriteRenderer bloom, engine;
        private float exhaustTimer;
        private Vector3 engineBaseScale;

        // ------------------------------------------------------------------
        // Ship glow
        // ------------------------------------------------------------------

        private void SetupShipGlow()
        {
            if (hull == null) return;
            var color = hullColor;

            // A tight, brighter bloom under the wide aura.
            if (bloom == null) bloom = SpriteFactory.NewRenderer("Bloom", ArtLibrary.Glow, color, 5, transform);
            bloom.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(ArtLibrary.Glow, radius * 3.4f);
            bloom.color = new Color(color.r, color.g, color.b, 0.4f);
            LeanTween.cancel(bloom.gameObject);
            Juice.Breathe(bloom, 0.28f, 0.5f, 0.9f);

            // Engine flame at the tail, in the hull's own space so it turns with it.
            if (engine == null) engine = SpriteFactory.NewRenderer("Engine", ArtLibrary.Glow, Color.white, 19, hull.transform);
            var tail = hull.sprite != null ? hull.sprite.bounds.extents.y * 0.95f : 0.5f;
            engine.transform.localPosition = new Vector3(0f, -tail, 0f);
            engine.transform.localRotation = Quaternion.identity;
            var flame = ArtLibrary.ScaleToDiameter(ArtLibrary.Glow, radius * 0.9f) / Mathf.Max(0.0001f, hullBaseScale);
            engineBaseScale = new Vector3(0.75f, 1.35f, 1f) * flame;
            engine.transform.localScale = engineBaseScale;
            var hot = Color.Lerp(color, Color.white, 0.45f);
            engine.color = new Color(hot.r, hot.g, hot.b, 0.85f);
            exhaustTimer = 0f;
            SetShipGlow(true);
        }

        private void SetShipGlow(bool on)
        {
            if (bloom != null) bloom.enabled = on;
            if (engine != null) engine.enabled = on;
        }

        private void TickShipGlow(float dt)
        {
            if (engine == null || hull == null || !hull.enabled || dt <= 0f) return;
            // Flicker: a flame never holds still.
            var flick = Random.Range(0.82f, 1.22f);
            engine.transform.localScale = new Vector3(engineBaseScale.x * Random.Range(0.9f, 1.05f),
                engineBaseScale.y * flick, 1f);

            exhaustTimer -= dt;
            if (exhaustTimer > 0f) return;
            exhaustTimer = 0.055f;
            var back = -(Vector2)hull.transform.up;
            var drift = back * Random.Range(1.6f, 2.6f) + Random.insideUnitCircle * 0.35f;
            ShotFx.Glint(engine.transform.position, drift, hullColor, radius * 0.32f, 0.26f);
        }

        // ------------------------------------------------------------------
        // Shot looks
        // ------------------------------------------------------------------

        /// <summary>Colour of the muzzle flash: the ship's shot colour.</summary>
        private Color MuzzleColor()
        {
            switch (signature)
            {
                case Signature.Heat: return Color.Lerp(HeatCold, HeatHot, heatStacks / (float)HeatMaxStacks);
                case Signature.Plates: return PlateColor;
                case Signature.Phase: return PhaseColor;
                case Signature.Suture: return SutureColor;
                case Signature.Siege: return SiegeColor;
                default: return Color.Lerp(hullColor, Color.white, 0.5f);
            }
        }

        private ShotLook LookFor(Volley volley, bool firstLane)
        {
            ShotLook look;
            switch (signature)
            {
                case Signature.Heat:
                    // Vanguarda: a long thin tracer that runs white-hot as it locks on.
                    var heat = heatStacks / (float)HeatMaxStacks;
                    look = ShotLook.Make(ShotShape.Tracer, volley.tint, ShotImpact.Spark);
                    look.size = volley.scale;
                    look.trailTime = 0.16f;
                    look.trailWidth = 0.11f + 0.06f * heat;
                    look.halo = 0.3f + 0.35f * heat;
                    look.sparkle = heat >= 0.99f ? 12f : 0f;
                    look.impactSize = 0.5f + 0.3f * heat;
                    return look;
                case Signature.Plates:
                    // Bastião: a big spinning hex slug with a thick green wake - impossible to miss.
                    look = ShotLook.Make(ShotShape.Hex, PlateColor, ShotImpact.Clang);
                    look.size = 1.5f;
                    look.trailTime = 0.1f;
                    look.trailWidth = 0.34f;
                    look.halo = 0.65f;
                    look.spin = 540f;
                    look.impactSize = 0.85f;
                    return look;
                case Signature.Phase:
                    // Espectro: a stretched diamond that stutters, with a long ghost trail.
                    look = ShotLook.Make(ShotShape.Diamond, PhaseColor, ShotImpact.Glitch);
                    look.size = 1.1f;
                    look.trailTime = 0.24f;
                    look.trailWidth = 0.13f;
                    look.halo = 0.35f;
                    look.flicker = true;
                    return look;
                case Signature.Suture:
                    // Aurora: a soft pulsing orb shedding sparkles.
                    look = ShotLook.Make(ShotShape.Orb, volley.tint, ShotImpact.Bloom);
                    look.size = volley.scale * 1.05f;
                    look.trailTime = 0.18f;
                    look.trailWidth = volley.tether ? 0.3f : 0.22f;
                    look.halo = volley.tether ? 0.7f : 0.5f;
                    look.pulse = true;
                    look.sparkle = volley.tether ? 26f : 14f;
                    look.impactSize = volley.tether ? 0.8f : 0.6f;
                    return look;
                case Signature.Siege:
                    // Orion: a gold shell; the charged one is a fat glowing round.
                    var charged = volley.charged && firstLane;
                    look = ShotLook.Make(ShotShape.Shell, SiegeColor, ShotImpact.Blast);
                    look.size = charged ? 1.5f : 1.15f;
                    look.trailTime = charged ? 0.2f : 0.12f;
                    look.trailWidth = charged ? 0.4f : 0.18f;
                    look.halo = charged ? 0.75f : 0.4f;
                    look.sparkle = charged ? 20f : 0f;
                    look.impactSize = charged ? 1.05f : 0.55f;
                    return look;
                default:
                    return ShotLook.Make(ShotShape.Default, Color.white, ShotImpact.Spark);
            }
        }

        private static ShotLook EchoLook()
        {
            var look = ShotLook.Make(ShotShape.Diamond, PhaseColor, ShotImpact.Glitch);
            look.size = 0.95f;
            look.alpha = 0.5f;
            look.trailTime = 0.28f;
            look.trailWidth = 0.11f;
            look.halo = 0.3f;
            look.flicker = true;
            look.impactSize = 0.45f;
            return look;
        }

        private static ShotLook PlateBoltLook()
        {
            var look = ShotLook.Make(ShotShape.Hex, PlateColor, ShotImpact.Clang);
            look.size = 1.05f;
            look.trailTime = 0.08f;
            look.trailWidth = 0.22f;
            look.halo = 0.55f;
            look.spin = 720f;
            look.impactSize = 0.55f;
            return look;
        }

        private ShotLook FanLook(Color color)
        {
            var look = ShotLook.Make(ShotShape.Tracer, color, ShotImpact.Spark);
            look.size = 0.85f;
            look.trailTime = 0.13f;
            look.trailWidth = 0.09f;
            look.halo = 0.3f;
            look.impactSize = 0.45f;
            return look;
        }
    }
}
