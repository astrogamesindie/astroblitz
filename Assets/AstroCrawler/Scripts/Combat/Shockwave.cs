// AstroCrawler - a one-shot expanding ring that damages on spawn.
//
// Lives in its own file because Unity only creates a MonoScript - and so can
// only serialise a component into a prefab - for a class whose file name
// matches it.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// A one-shot expanding ring that deals its damage the frame it appears.
    /// Used by Colapso Estelar, evolved missiles and Disparo de Cerco.
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        private const float ExpandTime = 0.28f;

        private float age;
        private float endScale;

        [SerializeField] private SpriteRenderer ring;

        public static void Spawn(Vector2 position, float radius, float damage, CombatWorld world)
        {
            var hits = world.DamageInRadius(position, radius, damage);

            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.shockwave : null;
            var wave = AbilitySpawner.Create(prefab, "Shockwave", position);

            var sprite = ArtLibrary.RingSprite;
            wave.endScale = ArtLibrary.ScaleToDiameter(sprite, radius * 2f);
            wave.age = 0f;
            wave.ring = AbilitySpawner.EnsureRenderer(wave.ring, wave.transform, "Ring", sprite,
                new Color(1f, 0.75f, 0.4f, 0.9f), 18);
            wave.ring.transform.localScale = Vector3.one * (wave.endScale * 0.1f);

            Impact.Burst(position, new Color(1f, 0.7f, 0.35f), radius * 0.9f);

            // The kick scales with how much it actually caught, so a blast that
            // hits nothing does not shake the screen like one that wipes a wave.
            Juice.Shake(Mathf.Min(0.12f + hits * 0.03f, 0.4f), 0.28f);
            if (hits >= 4)
            {
                Juice.HitStop(0.05f);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / ExpandTime);

            ring.transform.localScale = Vector3.one * Mathf.Lerp(endScale * 0.1f, endScale, t);
            var color = ring.color;
            color.a = 1f - t;
            ring.color = color;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
