// AstroCrawler - what the ship shows when it is repaired or hit.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Heal and damage effects on the ship, drawn with the Hovl Studio
    /// textures (HovlVfx): light columns, motes and twinkles rising out of a
    /// green pulse for a repair; a spray of sparks, a flare and a red shock for
    /// a hit. Emitters are built once per ship and fed with Emit; strength
    /// scales the particle counts, so a sip of regeneration stays quiet.
    /// Cosmetic only: hit points live on PlayerShip.
    /// </summary>
    internal sealed class ShipFeedbackVfx
    {
        private static readonly Color HealColor = new Color(0.45f, 1f, 0.62f);
        private static readonly Color HealLight = new Color(0.82f, 1f, 0.88f);
        private static readonly Color HitColor = new Color(1f, 0.32f, 0.22f);
        private static readonly Color HitLight = new Color(1f, 0.82f, 0.55f);

        private readonly ParticleSystem glow;
        private readonly ParticleSystem rings;
        private readonly ParticleSystem columns;
        private readonly ParticleSystem motes;
        private readonly ParticleSystem flares;
        private readonly ParticleSystem sparks;
        private readonly float radius;
        private float nextRegenMote;

        public ShipFeedbackVfx(Transform parent, float hullRadius)
        {
            radius = hullRadius;
            var root = new GameObject("FeedbackParticles").transform;
            root.SetParent(parent, false);
            // Pulses and rings under the hull (20); motes, flares and sparks over it.
            glow = HovlVfx.Emitter(root, "Glow", "Debris2", 6, 5, VfxSize.Pop);
            rings = HovlVfx.Emitter(root, "Rings", "Aura4", 6, 6, VfxSize.Grow);
            columns = HovlVfx.Emitter(root, "Columns", "Quad1", 24, 22, VfxSize.Steady, VfxFade.InOut);
            motes = HovlVfx.Emitter(root, "Motes", "Glow1", 64, 23, VfxSize.Shrink);
            flares = HovlVfx.Emitter(root, "Flares", "Flare6", 16, 25, VfxSize.Pop);
            sparks = HovlVfx.Emitter(root, "Sparks", "Glow2", 64, 24, VfxSize.Shrink, VfxFade.Out,
                ParticleSystemRenderMode.Stretch);
        }

        /// <summary>A repair; <paramref name="fraction"/> is the share of the hull it restored.</summary>
        public void Heal(float fraction)
        {
            var strength = Mathf.Clamp01(fraction * 4f);
            HovlVfx.Emit(glow, Vector2.zero, Vector2.zero, WithAlpha(HealColor, 0.5f + 0.3f * strength),
                0.6f, radius * (5f + 3f * strength));
            HovlVfx.Emit(rings, Vector2.zero, Vector2.zero, WithAlpha(HealColor, 0.75f), 0.65f,
                radius * (5f + 2f * strength), Random.Range(0f, 360f), 220f);

            var columnCount = 3 + Mathf.RoundToInt(5f * strength);
            for (var i = 0; i < columnCount; i++)
            {
                var x = Random.Range(-1.6f, 1.6f) * radius;
                HovlVfx.Emit(columns, new Vector2(x, Random.Range(-0.8f, 0.2f) * radius),
                    Vector2.up * Random.Range(2f, 3.2f), WithAlpha(HealColor, 0.7f), Random.Range(0.5f, 0.7f),
                    new Vector2(radius * 0.35f, radius * Random.Range(1.6f, 2.4f)));
            }

            var moteCount = 6 + Mathf.RoundToInt(12f * strength);
            for (var i = 0; i < moteCount; i++) RegenMote(Random.Range(0.08f, 0.16f));

            var flareCount = 2 + Mathf.RoundToInt(3f * strength);
            for (var i = 0; i < flareCount; i++)
            {
                var spot = Random.insideUnitCircle * radius * 1.6f;
                HovlVfx.Emit(flares, spot, Vector2.up * 0.6f, HealLight, Random.Range(0.35f, 0.55f),
                    radius * Random.Range(0.9f, 1.5f), Random.Range(0f, 90f), Random.Range(-90f, 90f));
            }
        }

        /// <summary>Regeneration ticks every quarter second: one green mote now and then, nothing more.</summary>
        public void Regenerate()
        {
            if (Time.time < nextRegenMote) return;
            nextRegenMote = Time.time + 0.35f;
            RegenMote(Random.Range(0.06f, 0.1f));
        }

        private void RegenMote(float size)
        {
            var spot = Random.insideUnitCircle * radius * 1.7f;
            HovlVfx.Emit(motes, spot, new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.8f, 1.7f)),
                Color.Lerp(HealColor, HealLight, Random.value), Random.Range(0.6f, 1f), size);
        }

        /// <summary>
        /// A hit; <paramref name="fraction"/> is the share of the hull it took and
        /// <paramref name="towards"/> points from the ship to what struck it.
        /// </summary>
        public void Hit(float fraction, Vector2 towards)
        {
            var strength = Mathf.Clamp01(fraction * 5f);
            if (towards.sqrMagnitude < 0.0001f) towards = Random.insideUnitCircle.normalized;
            towards.Normalize();
            var contact = towards * radius * 0.9f;

            HovlVfx.Emit(glow, Vector2.zero, Vector2.zero, WithAlpha(HitColor, 0.45f + 0.3f * strength),
                0.3f, radius * (4f + 2f * strength));
            HovlVfx.Emit(rings, Vector2.zero, Vector2.zero, WithAlpha(HitColor, 0.55f), 0.32f,
                radius * (3.5f + 2f * strength), Random.Range(0f, 360f), -320f);
            HovlVfx.Emit(flares, contact, Vector2.zero, HitLight, 0.2f, radius * (2.2f + 1.4f * strength),
                Random.Range(0f, 45f));

            // Sparks fly off the impact, away from the hull.
            var baseAngle = Mathf.Atan2(towards.y, towards.x);
            var sparkCount = 8 + Mathf.RoundToInt(14f * strength);
            for (var i = 0; i < sparkCount; i++)
            {
                var angle = baseAngle + Random.Range(-1.2f, 1.2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                HovlVfx.Emit(sparks, contact, direction * Random.Range(4f, 9f),
                    Color.Lerp(HitColor, HitLight, Random.value), Random.Range(0.22f, 0.4f),
                    Random.Range(0.08f, 0.14f));
            }
        }

        public void Clear()
        {
            HovlVfx.Clear(glow);
            HovlVfx.Clear(rings);
            HovlVfx.Clear(columns);
            HovlVfx.Clear(motes);
            HovlVfx.Clear(flares);
            HovlVfx.Clear(sparks);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
