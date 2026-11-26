// AstroCrawler - the Misseis card's homing shot.
//
// Lives in its own file because Unity only creates a MonoScript - and so can
// only serialise a component into a prefab - for a class whose file name
// matches it.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// A homing missile from the Misseis card. Evolved missiles detonate for area
    /// damage instead of a single hit (GDD 3).
    /// </summary>
    public class Missile : MonoBehaviour
    {
        private const float Speed = 9f;
        private const float TurnRate = 6f;
        private const float HitRadius = 0.2f;

        private Enemy target;
        private int targetSpawnId;
        private Vector2 heading;
        private float damage;
        private float splashRadius;
        private CombatWorld world;

        public static void Spawn(Vector2 position, Enemy target, float damage, float splashRadius,
            CombatWorld world)
        {
            // A volley spawns its missiles in one frame; the cue's cooldown
            // turns that into a single launch sound.
            Sfx.Play(Sound.MissileLaunch);

            var go = new GameObject("Missile");
            go.transform.SetParent(CombatWorld.Instance.effectRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.MissileBody;
            renderer.color = new Color(1f, 0.72f, 0.5f);
            renderer.sortingOrder = 16;
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = position;

            var material = ArtLibrary.SpriteMaterial;
            if (material != null)
            {
                var trail = go.AddComponent<TrailRenderer>();
                trail.material = material;
                trail.time = 0.3f;
                trail.startWidth = 0.22f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.05f;
                trail.sortingOrder = 15;
                trail.startColor = new Color(1f, 0.6f, 0.3f, 0.7f);
                trail.endColor = new Color(1f, 0.3f, 0.1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.alignment = LineAlignment.View;
            }

            var missile = go.AddComponent<Missile>();
            missile.target = target;
            missile.targetSpawnId = target != null ? target.spawnId : -1;
            missile.damage = damage;
            missile.splashRadius = splashRadius;
            missile.world = world;
            missile.heading = Random.insideUnitCircle.normalized;

            Juice.PopIn(go.transform, 0.4f, 0.2f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (target == null || target.spawnId != targetSpawnId || !world.IsVisible(target))
            {
                // Launching still requires a target in range; once flying,
                // keep pursuing another visible hull even outside that ring.
                target = world.NearestVisible(transform.position);
                if (target == null)
                {
                    Destroy(gameObject);
                    return;
                }
                targetSpawnId = target.spawnId;
            }

            var toTarget = ((Vector2)target.transform.position - (Vector2)transform.position);
            heading = Vector2.Lerp(heading, toTarget.normalized, dt * TurnRate).normalized;

            transform.position = (Vector2)transform.position + heading * Speed * dt;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);

            if (toTarget.magnitude <= target.radius + HitRadius)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            Impact.Burst(transform.position, new Color(1f, 0.6f, 0.3f), splashRadius > 0f ? 1.4f : 0.8f);
            Juice.Shake(splashRadius > 0f ? 0.16f : 0.07f, 0.2f);

            if (splashRadius > 0f)
            {
                Shockwave.Spawn(transform.position, splashRadius, damage, world);
            }
            else if (target != null)
            {
                target.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }
}
