using System.Collections.Generic;
using UnityEngine;

namespace AstroCrawler
{
    public partial class EffectRunner
    {
        private sealed class SnowActor
        {
            public SpriteRenderer renderer;
            public Vector2 position, velocity;
            public float life, radius, damage, slow, tick, duration;
            public int kind;
            public bool live;
            public readonly Dictionary<int, float> hits = new Dictionary<int, float>();
        }

        private const int SnowCapacity = 48;
        private readonly SnowActor[] snowActors = new SnowActor[SnowCapacity];
        private readonly float[] snowCooldowns = new float[6];
        private readonly List<Enemy> snowTargets = new List<Enemy>(128);
        private int snowEpoch;
        private static readonly Color SnowColor = new Color(0.75f, 0.94f, 1f);

        private void ResetChristmas()
        {
            snowEpoch++;
            for (var i = 0; i < snowCooldowns.Length; i++) snowCooldowns[i] = 0f;
            for (var i = 0; i < snowActors.Length; i++)
            {
                var actor = snowActors[i];
                if (actor == null) continue;
                actor.live = false;
                actor.hits.Clear();
                if (actor.renderer != null) actor.renderer.gameObject.SetActive(false);
            }
        }

        private SnowActor SpawnSnow(int kind, Vector2 position, Vector2 velocity, TimedEffect effect)
        {
            for (var i = 0; i < snowActors.Length; i++)
            {
                var actor = snowActors[i];
                if (actor != null && actor.live) continue;
                if (actor == null)
                {
                    actor = new SnowActor { renderer = SpriteFactory.NewRenderer("ChristmasEffect_" + i, ArtLibrary.CircleFilled, SnowColor, 24, transform) };
                    snowActors[i] = actor;
                }
                actor.kind = kind;
                actor.position = position;
                actor.velocity = velocity;
                actor.life = actor.duration = effect.Param("duration", 6f);
                actor.radius = effect.Param("radius", 0.5f);
                actor.damage = effect.Param("damage", 20f);
                actor.slow = effect.Param("slow");
                actor.tick = 0f;
                actor.live = true;
                actor.hits.Clear();
                var sprite = kind == 2 || kind == 5 ? ArtLibrary.RingSprite
                    : kind == 3 ? CardIconFactory.Get(CardEffectId.GiftBombs)
                    : kind == 4 ? SpriteFactory.Polygon(4, 0f) : ArtLibrary.CircleFilled;
                actor.renderer.sprite = sprite;
                actor.renderer.color = kind == 3 ? new Color(1f, 0.3f, 0.36f) : SnowColor;
                actor.renderer.transform.position = position;
                actor.renderer.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(sprite, kind == 3 ? 0.9f : actor.radius * 2f);
                actor.renderer.gameObject.SetActive(true);
                return actor;
            }
            return null;
        }

        private void UpdateChristmas(float dt)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (dt <= 0f || world == null || ship == null || !ship.isAlive || run == null || run.phase != RunPhase.WaveInProgress) return;
            var epoch = snowEpoch;
            for (var i = 0; i < snowActors.Length; i++)
            {
                var actor = snowActors[i];
                if (actor == null || !actor.live) continue;
                actor.life -= dt;
                if (actor.kind == 3)
                {
                    actor.renderer.transform.Rotate(0f, 0f, dt * 110f);
                    if (actor.life <= 0f)
                    {
                        actor.live = false;
                        actor.renderer.gameObject.SetActive(false);
                        Impact.Ring(actor.position, Color.red, 0.2f, actor.radius, 0.4f);
                        HitSnow(actor, true);
                    }
                }
                else if (actor.life <= 0f)
                {
                    actor.live = false;
                    actor.renderer.gameObject.SetActive(false);
                }
                else
                {
                    var previous = actor.position;
                    actor.position += actor.velocity * dt;
                    if (actor.kind == 0)
                        ReflectSnow(ref actor.position, ref actor.velocity, Arena.ViewHalfSize, actor.radius);
                    actor.renderer.transform.position = actor.position;
                    actor.renderer.transform.Rotate(0f, 0f, dt * 75f);
                    actor.tick -= dt;
                    if (actor.kind == 2 || actor.kind == 5)
                    {
                        if (actor.tick <= 0f) { actor.tick = 0.5f; HitSnow(actor, true); }
                    }
                    else HitSnow(actor, false, previous);
                }
                if (snowEpoch != epoch) return;
            }
            for (var i = 0; i < snowCooldowns.Length; i++)
            {
                var effect = Find((CardEffectId)((int)CardEffectId.BouncingSnowball + i));
                if (effect == null) continue;
                snowCooldowns[i] = Mathf.Max(0f, snowCooldowns[i] - dt);
                if (snowCooldowns[i] > 0f) continue;
                var target = world.NearestVisible(Arena.ShipPosition);
                if (target == null) continue;
                snowCooldowns[i] = effect.Param("cooldown", 12f);
                var direction = ((Vector2)target.transform.position - Arena.ShipPosition).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
                if (i == 4)
                {
                    var count = effect.ParamInt("count", 6);
                    for (var j = 0; j < count; j++)
                    {
                        var angle = j * Mathf.PI * 2f / count;
                        SpawnSnow(i, Arena.ShipPosition, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * effect.Param("speed", 9f), effect);
                    }
                }
                else if (i == 5)
                {
                    var enemies = world.Enemies;
                    var count = effect.ParamInt("count", 3);
                    var spawned = 0;
                    for (var j = enemies.Count - 1; j >= 0 && spawned < count; j--)
                    {
                        var enemy = enemies[j];
                        if (enemy == null || !enemy.isAlive || !world.IsVisible(enemy)) continue;
                        SpawnSnow(i, enemy.transform.position, Vector2.zero, effect);
                        spawned++;
                    }
                }
                else SpawnSnow(i, i == 3 ? (Vector2)target.transform.position : Arena.ShipPosition,
                    i <= 1 ? direction * effect.Param("speed", 7f) : Vector2.zero, effect);
            }
        }

        // Mirror the overshoot instead of dropping travel when a long frame crosses an edge.
        internal static void ReflectSnow(ref Vector2 position, ref Vector2 velocity, Vector2 bounds, float radius)
        {
            var x = Mathf.Max(0.1f, bounds.x - radius);
            var y = Mathf.Max(0.1f, bounds.y - radius);
            ReflectSnowAxis(ref position.x, ref velocity.x, x);
            ReflectSnowAxis(ref position.y, ref velocity.y, y);
        }

        private static void ReflectSnowAxis(ref float position, ref float velocity, float bound)
        {
            if (position >= -bound && position <= bound) return;
            var cell = Mathf.FloorToInt((position + bound) / (2f * bound));
            position = Mathf.PingPong(position + bound, 2f * bound) - bound;
            if ((cell & 1) != 0) velocity = -velocity;
        }

        private void HitSnow(SnowActor actor, bool area, Vector2 previous = default)
        {
            var epoch = snowEpoch;
            var segment = actor.position - previous;
            snowTargets.Clear();
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].isAlive) snowTargets.Add(enemies[i]);
            for (var i = snowTargets.Count - 1; i >= 0; i--)
            {
                var enemy = snowTargets[i];
                if (enemy == null || !enemy.isAlive) continue;
                var point = (Vector2)enemy.transform.position;
                var closest = area || segment.sqrMagnitude < 0.0001f ? actor.position
                    : previous + segment * Mathf.Clamp01(Vector2.Dot(point - previous, segment) / segment.sqrMagnitude);
                if ((point - closest).sqrMagnitude > (actor.radius + enemy.radius) * (actor.radius + enemy.radius)) continue;
                if (!area && actor.hits.TryGetValue(enemy.spawnId, out var last) && Time.time - last < 0.45f) continue;
                if (!area) actor.hits[enemy.spawnId] = Time.time;
                if (actor.slow > 0f) enemy.ApplySlow(1f - actor.slow, 0.7f);
                enemy.TakeDamage(actor.damage);
                if (snowEpoch != epoch) return;
            }
        }

        private void ActivateChristmasStar()
        {
            world.InRadius(Arena.ShipPosition, Arena.DetectionRadius, snowTargets);
            for (var i = 0; i < snowTargets.Count; i++)
                if (snowTargets[i] != null && snowTargets[i].isAlive) snowTargets[i].Mark(0.35f, 6f);
            Impact.Ring(Arena.ShipPosition, new Color(1f, 0.85f, 0.35f), 0.5f, Arena.DetectionRadius, 0.55f);
        }
    }
}
