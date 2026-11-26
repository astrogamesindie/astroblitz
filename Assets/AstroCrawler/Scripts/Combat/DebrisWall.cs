// AstroCrawler - Muralha de Destrocos (GDD 3.1.2).
//
// Lives in its own file because Unity only creates a MonoScript - and so can
// only serialise a component into a prefab - for a class whose file name
// matches it.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Muralha de Destrocos (GDD 3.1.2). A physical plug between the hull and the
    /// nearest threat: enemies cannot pass through it and grind it down instead.
    /// </summary>
    public class DebrisWall : MonoBehaviour
    {
        private const float DamageInterval = 0.5f;

        private float hp;
        private float maxHp;
        private float duration;
        private float radius;
        private float damageTimer;
        private CombatWorld world;
        [SerializeField] private SpriteRenderer body;
        private readonly List<Enemy> buffer = new List<Enemy>();

        public static void Spawn(Vector2 position, float hp, float duration, float radius, CombatWorld world)
        {
            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.debrisWall : null;
            var wall = AbilitySpawner.Create(prefab, "DebrisWall", position);
            var go = wall.gameObject;

            wall.hp = hp;
            wall.maxHp = hp;
            wall.duration = duration;
            wall.radius = radius;
            wall.world = world;

            var sprite = ArtLibrary.ArtOr("wire_hex", SpriteFactory.Polygon(6, 0f));
            wall.body = AbilitySpawner.EnsureRenderer(wall.body, go.transform, "Body", sprite,
                new Color(0.72f, 0.66f, 0.55f, 0.95f), 12);
            wall.body.transform.localScale =
                Vector3.one * ArtLibrary.ScaleToDiameter(sprite, radius * 2f);

            LeanTween.cancel(wall.body.gameObject);
            LeanTween.rotateZ(wall.body.gameObject, 360f, 14f)
                .setLoopClamp().setEase(LeanTweenType.linear);

            Juice.PopIn(go.transform, 1f, 0.3f);
            Impact.Ring(position, new Color(0.9f, 0.82f, 0.6f), radius * 0.4f, radius * 2.2f, 0.3f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var center = (Vector2)transform.position;

            duration -= dt;
            damageTimer -= dt;

            world.InRadius(center, radius, buffer);
            for (var i = 0; i < buffer.Count; i++)
            {
                var enemy = buffer[i];
                var away = ((Vector2)enemy.transform.position - center);
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = Random.insideUnitCircle.normalized;
                }
                // Hold the enemy on the outside face of the barrier.
                enemy.transform.position = center + away.normalized * (radius + enemy.radius);

                if (damageTimer <= 0f)
                {
                    hp -= enemy.definition.collisionDamage * 0.5f;
                }
            }

            if (damageTimer <= 0f)
            {
                damageTimer = DamageInterval;
            }

            // Reddens as well as fades, so "about to break" is readable at a
            // glance rather than only from the alpha.
            var fade = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
            body.color = new Color(0.72f, 0.4f + 0.26f * fade, 0.3f + 0.25f * fade,
                0.35f + 0.6f * fade);

            if (hp <= 0f || duration <= 0f)
            {
                Impact.Debris(center, new Color(0.72f, 0.66f, 0.55f), radius * 1.6f, 7);
                Juice.Shake(0.16f, 0.25f);
                Destroy(gameObject);
            }
        }
    }
}
