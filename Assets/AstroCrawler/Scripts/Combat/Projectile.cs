// AstroCrawler - pooled straight shots from the auto-cannon and healing volleys.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// A straight-flying bullet. Recycled through a static free list because the
    /// ship fires up to 20 of these a second at max cadence.
    /// </summary>
    /// <summary>What a ship's signature adds to one bullet (PlayerShip.Signature).</summary>
    [System.Flags]
    public enum ShotTrait
    {
        None = 0,
        /// <summary>Bastião Aegis: shoves the enemy back.</summary>
        Knockback = 1,
        /// <summary>Espectro Nulo: 15% slow for a moment.</summary>
        PhaseSlow = 2,
        /// <summary>Orion: damage past the kill jumps to the nearest enemy.</summary>
        Overkill = 4,
        /// <summary>Aurora Médica: links to a second enemy and drains it.</summary>
        Tether = 8,
        /// <summary>Aurora Médica (Reparo): the enemy drops healing motes when it dies.</summary>
        Seed = 16,
        /// <summary>Espectro Nulo (Estase): stops in the air until the stasis ends.</summary>
        StasisHang = 32,
        /// <summary>Aurora Médica: bends towards its target.</summary>
        Homing = 64,
        /// <summary>Secondary shot (echo, plate): does not trigger on-hit cards.</summary>
        NoProc = 128,
    }

    public class Projectile : MonoBehaviour
    {
        private const float Speed = 16f;
        private const float HitRadius = 0.12f;

        /// <summary>
        /// Bullets fade a little past the detection ring instead of crossing
        /// the whole arena, so a miss cannot snipe something still off screen.
        /// </summary>
        private const float Lifetime = (Arena.DetectionRadius + 1.5f) / Speed;

        // Shared by every bullet: hits are gathered first and applied after,
        // because a kill can spawn fragments into the list being scanned.
        private static readonly List<Enemy> hits = new List<Enemy>();

        private static readonly Stack<Projectile> pool = new Stack<Projectile>();
        private static Transform root;

        private Vector2 velocity;
        private float damage;
        private bool piercing;
        private bool radialMissile;
        private float blastDamage, blastRadius;
        private float life;
        private float age;
        private CombatWorld world;
        private ShotTrait traits;
        private int maxHits;
        private bool hanging;
        private float baseScale;
        private Vector3 baseShape = Vector3.one;

        // Signature look (ShotLook): extra layers and motion, off for plain shots.
        private bool styled;
        private ShotLook look;
        private SpriteRenderer core, halo;
        private float sparkleTimer;
        private float defaultTrailTime = -1f, defaultTrailWidth;
        private AnimationCurve defaultTrailCurve;
        private static AnimationCurve taperCurve;

        // The enemy this bullet was fired at, for Enemy.incomingDamage. Held
        // with its spawn id because a pooled enemy can die and come back as a
        // different one while the bullet is still flying.
        private Enemy aimedAt;
        private int aimedSpawnId;

        private SpriteRenderer body;
        private TrailRenderer trail;

        // Spawn ids rather than object references: a pierced enemy can die while
        // the bullet is still in flight, and pooling means the same object can
        // come back as a different enemy before this bullet expires.
        private readonly HashSet<int> alreadyHit = new HashSet<int>();

        public static Projectile Spawn(Vector2 position, Vector2 direction, float damage, bool piercing,
            CombatWorld world, Enemy aimedAt = null, float blastDamage = 0f, float blastRadius = 0f,
            bool radialMissile = false, Color? tintOverride = null)
        {
            Projectile projectile = null;
            while (projectile == null && pool.Count > 0)
            {
                // Entries can go stale if a run tore down in the same frame a
                // projectile recycled itself.
                projectile = pool.Pop();
            }
            if (projectile == null)
            {
                projectile = Create();
            }

            projectile.gameObject.SetActive(true);
            projectile.transform.position = position;
            projectile.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            projectile.velocity = direction.normalized * Speed;
            projectile.damage = damage;
            projectile.piercing = piercing;
            projectile.radialMissile = radialMissile;
            projectile.blastDamage = blastDamage;
            projectile.blastRadius = blastRadius;
            projectile.life = radialMissile ? Arena.LostRadius / Speed : Lifetime;
            projectile.world = world;
            projectile.age = 0f;
            projectile.traits = ShotTrait.None;
            projectile.maxHits = 0;
            projectile.hanging = false;
            projectile.ResetLook();
            projectile.alreadyHit.Clear();
            projectile.aimedAt = aimedAt;
            projectile.aimedSpawnId = aimedAt != null ? aimedAt.spawnId : 0;
            if (aimedAt != null)
            {
                aimedAt.AddIncoming(damage);
            }

            // Slender pink diamond darts, unlike the orange homing missile sprite.
            projectile.body.sprite = radialMissile ? SpriteFactory.Polygon(4) : ArtLibrary.Bullet;
            var tint = tintOverride ?? (radialMissile ? new Color(1f, 0.45f, 0.8f) :
                blastDamage > 0f ? new Color(1f, 0.65f, 0.1f) :
                piercing ? new Color(1f, 0.85f, 0.35f) : new Color(0.75f, 0.95f, 1f));
            projectile.body.color = tint;

            if (projectile.trail != null)
            {
                // Without clearing, the trail draws a line from wherever this
                // instance died last time to where it just respawned.
                projectile.trail.Clear();
                projectile.trail.startColor = new Color(tint.r, tint.g, tint.b, 0.75f);
                projectile.trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            }

            // Pierce shots read as heavier by being visibly fatter.
            var scale = radialMissile ? 0.42f : blastDamage > 0f ? 0.52f : piercing ? 0.42f : 0.3f;
            var shape = radialMissile ? new Vector3(0.6f, 1.7f, 1f) : Vector3.one;
            projectile.baseScale = scale;
            projectile.baseShape = shape;
            projectile.transform.localScale = shape * scale * 0.55f;
            LeanTween.cancel(projectile.gameObject);
            LeanTween.scale(projectile.gameObject, shape * scale, 0.09f)
                .setEase(LeanTweenType.easeOutQuad);

            return projectile;
        }

        /// <summary>
        /// A ship's signature on this bullet: traits, colour, size and, for
        /// piercing shots, how many enemies it may pass through (0 = all).
        /// </summary>
        public Projectile Style(ShotTrait shotTraits, Color? tint = null, float scale = 1f, int pierceLimit = 0)
        {
            traits = shotTraits;
            if (pierceLimit > 0)
            {
                piercing = true;
                maxHits = pierceLimit;
            }
            if (tint.HasValue)
            {
                var color = tint.Value;
                body.color = color;
                if (trail != null)
                {
                    trail.startColor = new Color(color.r, color.g, color.b, 0.75f * color.a);
                    trail.endColor = new Color(color.r, color.g, color.b, 0f);
                }
            }
            if (!Mathf.Approximately(scale, 1f))
            {
                baseScale *= Mathf.Max(0.2f, scale);
                LeanTween.cancel(gameObject);
                transform.localScale = baseShape * baseScale * 0.55f;
                LeanTween.scale(gameObject, baseShape * baseScale, 0.09f).setEase(LeanTweenType.easeOutQuad);
            }
            return this;
        }

        /// <summary>
        /// Dresses the bullet in its ship's look: shape, colour, a white-hot
        /// core, a soft halo, a tapered trail and its motion (spin, flicker,
        /// pulse, glints). The impact reads from the same look.
        /// </summary>
        public Projectile Look(ShotLook shotLook)
        {
            styled = true;
            look = shotLook;
            var color = shotLook.color;
            color.a *= shotLook.alpha;

            Sprite sprite;
            Vector3 shape;
            switch (shotLook.shape)
            {
                case ShotShape.Tracer: sprite = ArtLibrary.Bullet; shape = new Vector3(0.5f, 2.1f, 1f); break;
                case ShotShape.Hex: sprite = SpriteFactory.Polygon(6); shape = new Vector3(1f, 1f, 1f); break;
                case ShotShape.Diamond: sprite = SpriteFactory.Polygon(4); shape = new Vector3(0.55f, 1.8f, 1f); break;
                case ShotShape.Orb: sprite = ArtLibrary.SoftCircle; shape = new Vector3(1.15f, 1.15f, 1f); break;
                case ShotShape.Shell: sprite = SpriteFactory.Disc(0.25f); shape = new Vector3(1f, 1.25f, 1f); break;
                default: sprite = ArtLibrary.Bullet; shape = Vector3.one; break;
            }
            body.sprite = sprite;
            body.color = color;

            if (core == null)
            {
                core = SpriteFactory.NewRenderer("Core", sprite, Color.white, 16, transform);
            }
            core.sprite = sprite;
            core.enabled = true;
            core.color = new Color(1f, 1f, 1f, 0.85f * shotLook.alpha);
            core.transform.localScale = Vector3.one * 0.48f;

            if (halo == null)
            {
                halo = SpriteFactory.NewRenderer("Halo", ArtLibrary.Glow, color, 13, transform);
            }
            halo.enabled = shotLook.halo > 0f;
            halo.color = new Color(color.r, color.g, color.b, shotLook.halo * shotLook.alpha);
            // The halo stays round even on stretched bodies.
            halo.transform.localScale = new Vector3(2.6f / shape.x, 2.6f / shape.y, 1f) *
                ArtLibrary.ScaleToDiameter(ArtLibrary.Glow, 1f);

            if (trail != null)
            {
                if (taperCurve == null) taperCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.time = shotLook.trailTime;
                trail.widthCurve = taperCurve;
                trail.widthMultiplier = shotLook.trailWidth;
                // A hot, pale head fading into the ship's colour.
                var head = Color.Lerp(color, Color.white, 0.45f);
                trail.startColor = new Color(head.r, head.g, head.b, 0.9f * shotLook.alpha);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }

            baseShape = shape;
            baseScale *= Mathf.Max(0.2f, shotLook.size);
            LeanTween.cancel(gameObject);
            transform.localScale = baseShape * baseScale * 0.5f;
            LeanTween.scale(gameObject, baseShape * baseScale, 0.08f).setEase(LeanTweenType.easeOutBack);
            sparkleTimer = 0f;
            return this;
        }

        private void ResetLook()
        {
            styled = false;
            if (core != null) core.enabled = false;
            if (halo != null) halo.enabled = false;
            if (trail != null)
            {
                if (defaultTrailTime < 0f)
                {
                    defaultTrailTime = trail.time;
                    defaultTrailWidth = trail.widthMultiplier;
                    defaultTrailCurve = trail.widthCurve;
                }
                trail.time = defaultTrailTime;
                trail.widthMultiplier = defaultTrailWidth;
                trail.widthCurve = defaultTrailCurve;
            }
        }

        private void TickLook(float dt)
        {
            if (look.spin != 0f) transform.Rotate(0f, 0f, look.spin * dt);
            if (look.flicker && core != null)
            {
                // A phase shot stutters between here and not-quite-here.
                var a = Random.value < 0.18f ? 0.25f : 0.85f;
                core.color = new Color(1f, 1f, 1f, a * look.alpha);
            }
            if (look.pulse)
            {
                var beat = 1f + 0.18f * Mathf.Sin(age * 28f);
                transform.localScale = baseShape * baseScale * beat;
            }
            if (look.sparkle > 0f)
            {
                sparkleTimer -= dt;
                if (sparkleTimer <= 0f)
                {
                    sparkleTimer = 1f / look.sparkle;
                    var side = new Vector2(-velocity.y, velocity.x).normalized * Random.Range(-0.6f, 0.6f);
                    ShotFx.Glint(transform.position, -velocity * 0.06f + side, look.color,
                        0.18f * look.size, 0.22f);
                }
            }
        }

        /// <summary>The impact in the shot's own language.</summary>
        private void PlayImpact(Vector2 point, Enemy enemy)
        {
            if (!styled)
            {
                Impact.Spark(point, body.color, 0.5f);
                return;
            }
            var color = look.color;
            var size = look.impactSize;
            var direction = velocity.normalized;
            switch (look.impact)
            {
                case ShotImpact.Clang:
                    // Bastião: a hard green ring, a white flash and shards back off the armour.
                    Impact.Spark(point, Color.white, size * 0.7f);
                    Impact.Ring(point, color, 0.25f, size * 1.9f, 0.22f);
                    ShotFx.Shards(point, -direction, color, 5, 6f, 60f);
                    break;
                case ShotImpact.Glitch:
                    // Espectro: the hit lands twice, a frame apart in space.
                    var side = new Vector2(-direction.y, direction.x) * 0.18f;
                    Impact.Spark(point + side, color, size * 0.6f);
                    Impact.Spark(point - side, new Color(0.55f, 0.95f, 1f), size * 0.5f);
                    ShotFx.Shards(point, direction, color, 3, 5f, 25f);
                    break;
                case ShotImpact.Bloom:
                    // Aurora: a soft pink flower of light.
                    Impact.Spark(point, color, size);
                    ShotFx.Halo(point, color, size * 1.6f, 0.3f);
                    for (var i = 0; i < 3; i++)
                        ShotFx.Glint(point, Random.insideUnitCircle * 1.2f, color, 0.16f, 0.35f);
                    break;
                case ShotImpact.Blast:
                    // Orion: heavy - a hot core, a halo and a spray of gold.
                    Impact.Spark(point, Color.white, size * 0.8f);
                    Impact.Spark(point, color, size * 1.3f);
                    ShotFx.Halo(point, color, size * 2.2f, 0.35f);
                    ShotFx.Shards(point, direction, color, 7, 8f, 75f);
                    break;
                default:
                    Impact.Spark(point, color, size);
                    ShotFx.Shards(point, direction, color, 3, 6f, 40f);
                    break;
            }
        }

        private static Projectile Create()
        {
            if (root == null)
            {
                root = new GameObject("Projectiles").transform;
            }

            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.projectile : null;
            if (prefab != null)
            {
                var spawned = Object.Instantiate(prefab, root);
                spawned.CacheParts();
                return spawned;
            }

            var go = new GameObject("Projectile");
            go.transform.SetParent(root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.Bullet;
            renderer.sortingOrder = 15;
            go.transform.localScale = Vector3.one * 0.3f;

            var projectile = go.AddComponent<Projectile>();
            projectile.body = renderer;
            projectile.trail = BuildTrail(go);
            return projectile;
        }

        /// <summary>Picks up the renderer and trail the prefab already carries.</summary>
        public void CacheParts()
        {
            if (body == null) { body = GetComponent<SpriteRenderer>(); }
            if (trail == null) { trail = GetComponent<TrailRenderer>(); }
        }

        private static TrailRenderer BuildTrail(GameObject go)
        {
            var material = ArtLibrary.SpriteMaterial;
            if (material == null)
            {
                return null;
            }

            var trail = go.AddComponent<TrailRenderer>();
            trail.material = material;
            trail.time = 0.11f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.06f;
            trail.numCapVertices = 2;
            trail.sortingOrder = 14;
            trail.receiveShadows = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.alignment = LineAlignment.View;
            return trail;
        }

        /// <summary>Drops the pooled instances; called when a run tears down.</summary>
        public static void ResetPool()
        {
            pool.Clear();
            ShotFx.Reset();
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Estase of the Espectro: the bullet parks near its target and
            // waits; when time starts again they all land together, harder.
            if ((traits & ShotTrait.StasisHang) != 0)
            {
                if (world.specialStasis)
                {
                    if (hanging) return;
                    var close = aimedAt != null && aimedAt.isAlive && aimedAt.spawnId == aimedSpawnId &&
                        ((Vector2)aimedAt.transform.position - (Vector2)transform.position).magnitude < aimedAt.radius + 0.7f;
                    if (close || age > 0.3f)
                    {
                        hanging = true;
                        Impact.Spark(transform.position, new Color(0.75f, 0.5f, 1f), 0.35f);
                        return;
                    }
                }
                else if (hanging)
                {
                    hanging = false;
                    traits &= ~ShotTrait.StasisHang;
                    damage *= 1.25f;
                    Impact.Spark(transform.position, new Color(0.8f, 0.55f, 1f), 0.7f);
                }
            }

            age += dt;
            life -= dt;
            if (life <= 0f)
            {
                Recycle();
                return;
            }

            if (styled) TickLook(dt);

            if ((traits & ShotTrait.Homing) != 0 && aimedAt != null && aimedAt.isAlive && aimedAt.spawnId == aimedSpawnId)
            {
                var wanted = ((Vector2)aimedAt.transform.position - (Vector2)transform.position).normalized;
                var current = velocity.normalized;
                var turned = Vector3.RotateTowards(current, wanted, 4.2f * dt, 0f);
                velocity = (Vector2)turned * velocity.magnitude;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
            }

            // Swept, not sampled: at 30 fps (or with the editor's balance bot
            // running fast) a bullet moves further per frame than a small
            // meteor is wide, and a point test would pass straight through it.
            var from = (Vector2)transform.position;
            var step = velocity * dt;
            var to = from + step;
            transform.position = to;

            var stepLengthSqr = step.sqrMagnitude;
            var nearestT = float.MaxValue;
            Enemy nearest = null;
            hits.Clear();

            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive || alreadyHit.Contains(enemy.spawnId))
                {
                    continue;
                }

                var center = (Vector2)enemy.transform.position;
                var t = stepLengthSqr > 0f
                    ? Mathf.Clamp01(Vector2.Dot(center - from, step) / stepLengthSqr)
                    : 0f;
                var reach = enemy.radius + HitRadius;
                if ((from + step * t - center).sqrMagnitude > reach * reach)
                {
                    continue;
                }

                if (piercing)
                {
                    hits.Add(enemy);
                }
                else if (t < nearestT)
                {
                    nearestT = t;
                    nearest = enemy;
                }
            }

            if (nearest != null)
            {
                Hit(nearest);
                Recycle();
                return;
            }

            for (var i = 0; i < hits.Count; i++)
            {
                if (maxHits > 0 && alreadyHit.Count >= maxHits) break;
                Hit(hits[i]);
            }
            hits.Clear();
            if (maxHits > 0 && alreadyHit.Count >= maxHits) Recycle();
        }

        private void Hit(Enemy enemy)
        {
            if (enemy == null || !enemy.isAlive)
            {
                return;
            }
            alreadyHit.Add(enemy.spawnId);
            if (enemy == aimedAt)
            {
                ReleaseAim();
            }
            // On the face the bullet struck, not at the centre of the rock.
            var point = (Vector2)enemy.transform.position - velocity.normalized * (enemy.radius * 0.8f);
            PlayImpact(point, enemy);
            var hitPosition = (Vector2)enemy.transform.position;
            var hitSpawnId = enemy.spawnId;
            var hpBefore = enemy.hp;
            var killed = enemy.TakeDamage(damage);
            if (!radialMissile && (traits & ShotTrait.NoProc) == 0) world.NotifyCannonHit(hitPosition, hitSpawnId);
            if (traits != ShotTrait.None)
                PlayerShip.ApplyShotTraits(traits, enemy, killed, hitPosition, hitSpawnId, damage, hpBefore,
                    velocity.normalized, world);
            if (blastDamage > 0f)
            {
                var payload = blastDamage;
                blastDamage = 0f;
                Shockwave.Spawn(hitPosition, blastRadius, payload, world);
            }
        }

        /// <summary>Takes this bullet's damage off its target's incoming total.</summary>
        private void ReleaseAim()
        {
            if (aimedAt != null && aimedAt.spawnId == aimedSpawnId)
            {
                aimedAt.AddIncoming(-damage);
            }
            aimedAt = null;
        }

        private void Recycle()
        {
            traits = ShotTrait.None;
            maxHits = 0;
            hanging = false;
            blastDamage = blastRadius = 0f;
            radialMissile = false;
            ReleaseAim();
            if (trail != null)
            {
                trail.Clear();
            }
            gameObject.SetActive(false);
            pool.Push(this);
        }
    }

}
