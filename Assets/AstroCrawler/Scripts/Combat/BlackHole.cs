// AstroCrawler - Buraco Negro and its variants (GDD 3, 3.1.3).
//
// Lives in its own file because Unity only creates a MonoScript - and so can
// only serialise a component into a prefab - for a class whose file name
// matches it.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Buraco Negro and its variants (GDD 3, 3.1.3). Pulls enemies in, ticks light
    /// damage, and - once evolved into Colapso Estelar - detonates on expiry.
    /// Keeps a static registry of currently active volumes.
    /// </summary>
    public class BlackHole : MonoBehaviour
    {
        public static readonly List<BlackHole> Active = new List<BlackHole>();

        public float radius { get; private set; }

        private float duration;
        private float dps;
        private float pullStrength;
        private float collapseDamage;
        private CombatWorld world;

        [SerializeField] private SpriteRenderer core;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private SpriteRenderer accretion;
        private readonly List<Enemy> buffer = new List<Enemy>();

        public static BlackHole Spawn(Vector2 position, float radius, float duration, float dps,
            float pullStrength, float collapseDamage, CombatWorld world)
        {
            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.blackHole : null;
            var hole = AbilitySpawner.Create(prefab, "BlackHole", position);
            var go = hole.gameObject;

            hole.radius = radius;
            hole.duration = duration;
            hole.dps = dps;
            hole.pullStrength = pullStrength;
            hole.collapseDamage = collapseDamage;
            hole.world = world;

            var haloSprite = ArtLibrary.Glow;
            hole.halo = AbilitySpawner.EnsureRenderer(hole.halo, go.transform, "Halo", haloSprite,
                new Color(0.55f, 0.3f, 0.9f, 0.25f), 6);
            hole.halo.transform.localScale =
                Vector3.one * ArtLibrary.ScaleToDiameter(haloSprite, radius * 2f);

            var coreSprite = ArtLibrary.ArtOr("poly_disc", SpriteFactory.Disc(0.35f));
            hole.core = AbilitySpawner.EnsureRenderer(hole.core, go.transform, "Core", coreSprite,
                new Color(0.08f, 0.02f, 0.15f, 0.97f), 7);
            hole.core.transform.localScale =
                Vector3.one * ArtLibrary.ScaleToDiameter(coreSprite, radius * 0.6f);

            // An accretion ring turning the other way sells the pull without
            // costing a particle system.
            var discSprite = ArtLibrary.DashedRing;
            hole.accretion = AbilitySpawner.EnsureRenderer(hole.accretion, go.transform, "Accretion",
                discSprite, new Color(0.8f, 0.55f, 1f, 0.55f), 8);
            hole.accretion.transform.localScale =
                Vector3.one * ArtLibrary.ScaleToDiameter(discSprite, radius * 1.5f);
            LeanTween.cancel(hole.accretion.gameObject);
            LeanTween.rotateZ(hole.accretion.gameObject, -360f, 2.2f)
                .setLoopClamp().setEase(LeanTweenType.linear);

            Juice.PopIn(go.transform, 1f, 0.34f);
            Impact.Ring(position, new Color(0.7f, 0.4f, 1f), radius * 0.4f, radius * 2.4f, 0.35f);
            Juice.Shake(0.14f, 0.22f);

            return hole;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>True when the point lies inside the event horizon.</summary>
        public bool Contains(Vector2 point)
        {
            return ((Vector2)transform.position - point).sqrMagnitude <= radius * radius;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var center = (Vector2)transform.position;

            duration -= dt;
            core.transform.Rotate(0f, 0f, 180f * dt);
            halo.color = new Color(0.55f, 0.3f, 0.9f, 0.18f + Mathf.PingPong(Time.time * 0.4f, 0.12f));

            world.InRadius(center, radius, buffer);
            for (var i = buffer.Count - 1; i >= 0; i--)
            {
                var enemy = buffer[i];
                enemy.PullTowards(center, pullStrength, dt);
                enemy.TakeDamage(dps * dt);
            }

            if (duration <= 0f)
            {
                Collapse();
            }
        }

        private void Collapse()
        {
            if (collapseDamage > 0f)
            {
                Shockwave.Spawn(transform.position, radius, collapseDamage, world);
            }
            else
            {
                // Even a harmless expiry gets a beat, so the volume never just
                // blinks out from under the enemies it was holding.
                Impact.Ring(transform.position, new Color(0.7f, 0.4f, 1f),
                    radius * 1.6f, radius * 0.2f, 0.25f);
            }
            Destroy(gameObject);
        }
    }
}
