// AstroCrawler - the look of each ship's shot (shape, hot core, halo, trail,
// sparkles, impact) and the shared world-space particle emitters behind it.

using UnityEngine;

namespace AstroCrawler
{
    public enum ShotShape { Default, Tracer, Hex, Diamond, Orb, Shell }

    /// <summary>How the impact of a styled shot reads.</summary>
    public enum ShotImpact { Spark, Clang, Glitch, Bloom, Blast }

    /// <summary>Everything that makes one ship's bullet recognisable at a glance.</summary>
    public struct ShotLook
    {
        public ShotShape shape;
        public Color color;
        /// <summary>Size relative to a plain cannon bullet.</summary>
        public float size;
        public float trailTime;
        public float trailWidth;
        /// <summary>Alpha of the soft halo around the bullet (0 = none).</summary>
        public float halo;
        /// <summary>Degrees per second the body spins (Hex slugs).</summary>
        public float spin;
        /// <summary>Glints shed per second along the path.</summary>
        public float sparkle;
        public bool flicker;
        public bool pulse;
        public ShotImpact impact;
        public float impactSize;
        /// <summary>Overall alpha (echoes are ghosts).</summary>
        public float alpha;

        public static ShotLook Make(ShotShape shape, Color color, ShotImpact impact)
        {
            return new ShotLook
            {
                shape = shape, color = color, size = 1f, trailTime = 0.12f, trailWidth = 0.16f, halo = 0.4f,
                impact = impact, impactSize = 0.6f, alpha = 1f
            };
        }
    }

    /// <summary>
    /// Pooled-by-design particles for bullets: one set of emitters for the
    /// whole arena (world space), fed with Emit. Rebuilt after a teardown.
    /// </summary>
    public static class ShotFx
    {
        private static Transform root;
        private static ParticleSystem glints, shards, rings;

        private static bool Ensure()
        {
            if (glints != null && shards != null && rings != null) return true;
            if (root != null) Object.Destroy(root.gameObject);
            root = new GameObject("ShotFx").transform;
            root.position = Vector3.zero;
            glints = HovlVfx.Emitter(root, "Glints", "Glow1", 220, 16, VfxSize.Shrink);
            shards = HovlVfx.Emitter(root, "Shards", "Flare6", 120, 17, VfxSize.Shrink, VfxFade.Out,
                ParticleSystemRenderMode.Stretch);
            rings = HovlVfx.Emitter(root, "Rings", "Aura4", 40, 12, VfxSize.Grow);
            return glints != null;
        }

        /// <summary>A small light left behind a bullet.</summary>
        public static void Glint(Vector2 position, Vector2 drift, Color color, float size, float lifetime = 0.28f)
        {
            if (!Ensure()) return;
            HovlVfx.Emit(glints, position, drift, color, lifetime, size);
        }

        /// <summary>Streaks thrown out of an impact, fanned around <paramref name="direction"/>.</summary>
        public static void Shards(Vector2 position, Vector2 direction, Color color, int count, float speed, float spread = 70f)
        {
            if (!Ensure() || count <= 0) return;
            var aim = Mathf.Atan2(direction.y, direction.x);
            for (var i = 0; i < count; i++)
            {
                var angle = aim + Random.Range(-spread, spread) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.6f, 1.2f);
                HovlVfx.Emit(shards, position, velocity, color, Random.Range(0.14f, 0.26f), new Vector2(0.09f, 0.09f));
            }
        }

        /// <summary>A soft expanding halo (charged shots, impacts).</summary>
        public static void Halo(Vector2 position, Color color, float size, float lifetime = 0.3f)
        {
            if (!Ensure()) return;
            HovlVfx.Emit(rings, position, Vector2.zero, color, lifetime, size);
        }

        /// <summary>Forgets the emitters (run teardown / session reset).</summary>
        public static void Reset()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            glints = shards = rings = null;
        }
    }
}
