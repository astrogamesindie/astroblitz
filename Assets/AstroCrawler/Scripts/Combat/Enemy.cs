// AstroCrawler - enemies (GDD 5).

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// A single enemy in the arena. Enemies never shoot: they close on the ship
    /// and damage it by contact only (GDD 2.5), which is what makes the auto-fire
    /// build a pure race between the player's DPS and the wave's mass.
    /// </summary>
    public partial class Enemy : MonoBehaviour
    {
        public EnemyDefinition definition { get; private set; }

        public float hp { get; private set; }
        public float maxHp { get; private set; }

        public bool isAlive { get; private set; }

        /// <summary>
        /// Damage already flying at this enemy. The auto-cannon skips targets
        /// whose incoming bullets will kill them anyway: with enemies that take
        /// two or three hits, re-aiming at a doomed target wasted about a third
        /// of every volley while the bullets were still in the air.
        /// </summary>
        public float incomingDamage { get; private set; }

        public bool isDoomed
        {
            get { return incomingDamage >= hp; }
        }

        public void AddIncoming(float amount)
        {
            incomingDamage = Mathf.Max(0f, incomingDamage + amount);
        }

        /// <summary>Radius used for both weapon hits and hull contact.</summary>
        public float radius { get; private set; }

        public bool isBoss
        {
            get { return definition != null && definition.behaviour == MoveBehaviour.Core; }
        }

        [Header("Partes (vindas do prefab)")]
        [Tooltip("Renderer do corpo. Se vazio, e criado em runtime.")]
        [SerializeField] private SpriteRenderer body;

        [Tooltip("Anel externo de chefes e fragatas. Se vazio, e criado sob demanda.")]
        [SerializeField] private SpriteRenderer glow;

        private CombatWorld world;

        private Vector2 heading;
        private float wavePhase;
        private float slowTimer;
        private float slowFactor = 1f;
        private bool paralyzed;

        // Marcacao de Alvo (Orion): extra damage taken until markUntil (combat time).
        private float markBonus;
        private float markUntil;
        private SpriteRenderer markRing;

        private Color baseColor;
        private float bodyScale;
        private float spin;

        private float summonTimer;

        // ------------------------------------------------------------------

        /// <summary>
        /// Unique per spawn, not per object. Pooled enemies reuse their instance
        /// id, so a piercing bullet that had already hit this object would
        /// wrongly skip the new occupant; it tracks this instead.
        /// </summary>
        public int spawnId { get; private set; }

        private static int nextSpawnId = 1;

        public void Setup(EnemyDefinition definition, CombatWorld world, Vector2 position,
            float healthScale = 1f)
        {
            this.definition = definition;
            this.world = world;
            spawnId = nextSpawnId++;

            maxHp = Mathf.Max(1f, Mathf.Round(definition.maxHp * Mathf.Max(0.1f, healthScale)));
            hp = maxHp;
            incomingDamage = 0f;
            radius = Mathf.Max(0.1f, definition.radius);
            isAlive = true;
            slowFactor = 1f;
            slowTimer = 0f;
            paralyzed = false;
            markBonus = 0f;
            markUntil = 0f;
            if (markRing != null) markRing.enabled = false;
            wavePhase = Random.Range(0f, Mathf.PI * 2f);
            summonTimer = definition.summonInterval;
            ResetBossState();

            transform.position = position;
            // A pooled instance arrives carrying the last occupant's rotation.
            transform.rotation = Quaternion.identity;
            heading = (Arena.ShipPosition - position).normalized;

            baseColor = SpriteFactory.ParseColor(definition.tint);
            spin = Random.Range(14f, 30f) * (Random.value < 0.5f ? -1f : 1f);

            var variant = GetHashCode() & int.MaxValue;
            var sprite = ArtLibrary.Enemy(definition.kind, variant);
            bodyScale = ArtLibrary.ScaleToDiameter(sprite, radius * 2f);

            // The prefab already carries these renderers; they are only created
            // here when this enemy was built procedurally (no prefab library).
            if (body == null)
            {
                body = SpriteFactory.NewRenderer("Body", sprite, baseColor, 10, transform);
            }
            body.sprite = sprite;
            body.color = baseColor;
            body.transform.localScale = Vector3.one * bodyScale;
            body.transform.localPosition = Vector3.zero;

            var wantsRing = isBoss || definition.kind == EnemyKind.Frigate || definition.kind == EnemyKind.Cocoon ||
                definition.kind == EnemyKind.BossOrb;
            if (wantsRing && glow == null)
            {
                glow = SpriteFactory.NewRenderer("Glow", ArtLibrary.DashedRing,
                    Color.white, 9, transform);
            }

            if (glow != null)
            {
                glow.gameObject.SetActive(wantsRing);
                if (wantsRing)
                {
                    var ringSprite = ArtLibrary.DashedRing;
                    glow.sprite = ringSprite;
                    glow.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.55f);
                    glow.transform.localScale =
                        Vector3.one * ArtLibrary.ScaleToDiameter(ringSprite, radius * 3.1f);
                    glow.transform.localRotation = Quaternion.identity;

                    // Cancel first: a reused instance would otherwise stack a
                    // second rotation and a second breathe on top of the old.
                    LeanTween.cancel(glow.gameObject);

                    // A slowly counter-rotating ring reads as "this one is heavy".
                    LeanTween.rotateZ(glow.gameObject, 360f, isBoss ? 7f : 11f)
                        .setLoopClamp()
                        .setEase(LeanTweenType.linear);

                    Juice.Breathe(glow, 0.3f, 0.7f, isBoss ? 0.8f : 1.3f);
                }
            }

            // Everything arrives with a pop, so a spawn is never a hard cut.
            transform.localScale = Vector3.one * 0.15f;
            LeanTween.scale(gameObject, Vector3.one, isBoss ? 0.7f : 0.34f)
                .setEase(LeanTweenType.easeOutBack);

            if (isBoss)
            {
                Juice.Shake(0.4f, 0.8f);
                Juice.ScreenFlash(new Color(1f, 0.2f, 0.25f, 0.28f), 0.6f);
            }
        }

        private void Update()
        {
            if (markRing != null && markRing.enabled)
            {
                if (!isAlive || Time.time >= markUntil) markRing.enabled = false;
                else markRing.transform.Rotate(0f, 0f, -90f * Time.deltaTime);
            }

            if (!isAlive || world.specialStasis)
            {
                return;
            }

            var dt = Time.deltaTime;

            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                if (slowTimer <= 0f)
                {
                    slowFactor = 1f;
                    paralyzed = false;
                }
            }

            Move(dt);

            if (isBoss)
            {
                TickBoss(dt);
            }
            else if (definition.behaviour == MoveBehaviour.Anchor)
            {
                TickCocoon(dt);
            }
        }

        private void Move(float dt)
        {
            if (paralyzed)
            {
                return;
            }

            var speed = definition.speed * slowFactor * world.globalEnemySpeedScale;
            var position = (Vector2)transform.position;
            var toShip = Arena.ShipPosition - position;

            switch (definition.behaviour)
            {
                case MoveBehaviour.Straight:
                    break;

                case MoveBehaviour.Drift:
                    // Weaves around the line to the ship. Turning the heading
                    // itself accumulated up to 45 degrees of drift to one side,
                    // and a rock that wide misses the detection range entirely
                    // and loops out to LostRadius - one stalled a wave for 40 s.
                    wavePhase += dt * 0.8f;
                    heading = Rotate(toShip.normalized, Mathf.Sin(wavePhase) * 28f);
                    break;

                case MoveBehaviour.Dive:
                case MoveBehaviour.Siege:
                    heading = Vector2.Lerp(heading, toShip.normalized, dt * 3f).normalized;
                    break;

                case MoveBehaviour.Zigzag:
                    wavePhase += dt * 6f;
                    var straight = toShip.normalized;
                    var side = new Vector2(-straight.y, straight.x);
                    heading = (straight + side * Mathf.Sin(wavePhase) * 0.9f).normalized;
                    break;

                case MoveBehaviour.Core:
                    MoveBoss(dt, speed);
                    return;

                case MoveBehaviour.Anchor:
                    MoveCocoon(dt);
                    return;
            }

            // A drifting enemy can slip past the fixed ship; re-aim rather than
            // leaving the wave permanently uncompletable.
            if (toShip.magnitude > Arena.LostRadius)
            {
                heading = toShip.normalized;
            }

            transform.position = position + heading * speed * dt;

            // Rocks tumble; hulls point where they are going.
            if (ArtLibrary.IsMeteor(definition.kind))
            {
                transform.Rotate(0f, 0f, spin * dt, Space.Self);
            }
            else
            {
                var angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // ------------------------------------------------------------------
        // Damage and death
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies weapon or ability damage. Returns true if this killed the
        /// enemy. Kills from damage fragment meteors (GDD 5.1).
        /// </summary>
        public bool TakeDamage(float amount)
        {
            if (!isAlive || amount <= 0f)
            {
                return false;
            }

            if (markBonus > 0f && Time.time < markUntil) amount *= 1f + markBonus;
            hp -= amount;

            Juice.FlashSprite(body, Color.white, baseColor, 0.13f);
            Juice.Number(transform.position, Mathf.Max(1f, amount).ToString("0"),
                new Color(1f, 0.93f, 0.7f), 0.85f);

            if (hp > 0f)
            {
                // A survivor recoils; the squash is what sells the bullet's mass.
                // Scale only: cancelling everything here would kill the flash.
                LeanTween.cancel(body.gameObject, false, TweenAction.SCALE);
                body.transform.localScale = Vector3.one * (bodyScale * 1.16f);
                LeanTween.scale(body.gameObject, Vector3.one * bodyScale, 0.14f)
                    .setEase(LeanTweenType.easeOutCubic);
                return false;
            }

            world.KillEnemy(this, true);
            return true;
        }

        /// <summary>
        /// The death beat. Called by CombatWorld before the object goes away -
        /// not from Despawn, because clearing the arena between waves must not
        /// set off twenty explosions at once.
        /// </summary>
        public void PlayDeathEffect(bool byPlayerDamage)
        {
            var position = (Vector2)transform.position;

            Impact.Burst(position, baseColor, radius * 2.2f);
            Impact.Debris(position, baseColor, radius * 2f, isBoss ? 14 : 4);

            if (isBoss)
            {
                Juice.Shake(0.55f, 1.1f);
                Juice.HitStop(0.22f);
                Juice.ScreenFlash(new Color(1f, 0.85f, 0.6f, 0.5f), 0.8f);
            }
            else if (!byPlayerDamage)
            {
                // A hull collision: the ship felt this one.
                Juice.Shake(0.12f, 0.18f);
            }
        }

        /// <summary>
        /// Removes the enemy without fragmenting it. Used when it crashes into
        /// the hull: contact damage is fixed regardless of how the enemy would
        /// otherwise have broken up (GDD 5.1).
        /// </summary>
        public void Consume()
        {
            world.KillEnemy(this, false);
        }

        /// <summary>Called by CombatWorld once the death has been accounted for.</summary>
        public void Despawn()
        {
            isAlive = false;

            // Every tween on this object and its parts has to go, or the next
            // enemy out of the pool inherits a rotation or a colour fade.
            LeanTween.cancel(gameObject);
            if (body != null) { LeanTween.cancel(body.gameObject); }
            if (glow != null) { LeanTween.cancel(glow.gameObject); }

            gameObject.SetActive(false);

            if (world != null)
            {
                world.Recycle(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ------------------------------------------------------------------
        // Crowd control
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies a slow. <paramref name="factor"/> is a speed multiplier
        /// (0.6 = 40% slower). The strongest active slow wins.
        /// </summary>
        public bool IsMarked
        {
            get { return isAlive && Time.time < markUntil; }
        }

        /// <summary>Takes <paramref name="bonus"/> more damage from everything for a while (gold ring).</summary>
        public void Mark(float bonus, float duration)
        {
            if (!isAlive || duration <= 0f) return;
            markBonus = Mathf.Max(0f, bonus);
            markUntil = Time.time + duration;
            if (markRing == null)
            {
                var sprite = ArtLibrary.DashedRing;
                markRing = SpriteFactory.NewRenderer("Mark", sprite, new Color(1f, 0.82f, 0.35f, 0.95f), 9, transform);
            }
            markRing.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(markRing.sprite, radius * 2.8f);
            markRing.enabled = true;
            Impact.Ring(transform.position, new Color(1f, 0.82f, 0.35f), radius * 4f, radius * 2.8f, 0.3f);
        }

        /// <summary>Current speed multiplier from slows: 1 free, 0 paralysed.</summary>
        public float SpeedFactor => !isAlive || slowTimer <= 0f ? 1f : paralyzed ? 0f : slowFactor;

        public void ApplySlow(float factor, float duration)
        {
            factor = Mathf.Clamp01(factor);
            if (slowTimer <= 0f || factor < slowFactor)
            {
                slowFactor = factor;
            }
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        /// <summary>Full stop, used by evolved Distorcao Temporal on weak enemies.</summary>
        public void Paralyze(float duration)
        {
            paralyzed = true;
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        /// <summary>Instantly displaces the enemy, used by Campo de Repulsao.</summary>
        public void Push(Vector2 delta)
        {
            if (world.specialStasis) return;
            transform.position = (Vector2)transform.position + delta;
        }

        /// <summary>Moves the enemy towards a point, used by gravity wells.</summary>
        public void PullTowards(Vector2 point, float strength, float dt)
        {
            if (paralyzed || world.specialStasis)
            {
                return;
            }
            var position = (Vector2)transform.position;
            var toPoint = point - position;
            var distance = toPoint.magnitude;
            if (distance < 0.01f)
            {
                return;
            }
            transform.position = position + toPoint / distance * strength * dt;
        }

        /// <summary>
        /// Moves an enemy instantly and refreshes its travel direction. Straight
        /// movers otherwise keep the heading from their original spawn point.
        /// </summary>
        public void Relocate(Vector2 position)
        {
            if (!isAlive) return;
            transform.position = position;
            if (!isBoss)
            {
                heading = (Arena.ShipPosition - position).normalized;
            }
        }

        public float hpFraction
        {
            get { return maxHp <= 0f ? 0f : Mathf.Clamp01(hp / maxHp); }
        }
    }
}
