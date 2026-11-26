// AstroCrawler - the cosmetic ring used by instantaneous abilities.
//
// Lives in its own file because Unity only creates a MonoScript - and so can
// only serialise a component into a prefab - for a class whose file name
// matches it.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Purely cosmetic pulse used by Campo de Repulsao and other instant effects.
    /// </summary>
    public class Pulse : MonoBehaviour
    {
        private float age;
        private float lifetime;
        private float endScale;

        [SerializeField] private SpriteRenderer ring;

        public static void Spawn(Vector2 position, float radius, Color color, float lifetime = 0.35f)
        {
            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.pulse : null;
            var pulse = AbilitySpawner.Create(prefab, "Pulse", position);

            var sprite = ArtLibrary.RingSprite;
            pulse.lifetime = lifetime;
            pulse.age = 0f;
            pulse.endScale = ArtLibrary.ScaleToDiameter(sprite, radius * 2f);
            pulse.ring = AbilitySpawner.EnsureRenderer(pulse.ring, pulse.transform, "Ring",
                sprite, color, 17);
            pulse.ring.transform.localScale = Vector3.one * (pulse.endScale * 0.1f);

            // A second, faster ring gives the pulse an edge instead of one
            // lonely expanding circle.
            Impact.Ring(position, color, radius * 0.5f, radius * 1.4f, lifetime * 0.6f);
            Juice.Shake(0.1f, 0.18f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / lifetime);

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
