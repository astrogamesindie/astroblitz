// AstroCrawler - the player ship (GDD 2, 6).
//
// The ship is stationary and fires itself. The player never aims, never moves
// and never pulls a trigger, so this class deliberately reads no input: all of
// its behaviour comes from its statline plus whatever CombatModifiers the card
// effects currently impose.

using System;

using UnityEngine;

namespace AstroCrawler
{
    public partial class PlayerShip : MonoBehaviour
    {
        public ShipDefinition definition { get; private set; }

        /// <summary>The ship in the arena (shot traits call back into its signature).</summary>
        public static PlayerShip Active { get; private set; }

        public float hp { get; private set; }
        public float maxHp { get; private set; }

        /// <summary>Remaining collision blocks from the Defesa card.</summary>
        public int shieldCharges { get; private set; }

        public float shieldTimeLeft { get; private set; }

        private float siegeDamage, siegeRadius;
        public bool HasSiegeCharge => siegeDamage > 0f;
        public void SetSiegeCharge(float damage, float blastRadius)
        {
            siegeDamage = Mathf.Max(0f, damage);
            siegeRadius = Mathf.Max(0f, blastRadius);
        }
        public void ClearSiegeCharge() { siegeDamage = siegeRadius = 0f; }

        public float radius = 0.55f;

        public event Action<float> onDamaged;
        public event Action onDied;
        public event Action onShieldChanged;

        /// <summary>
        /// Gap between the bullets of one volley. Ataque Duplo and Triplo fire
        /// side by side rather than fanned out: a cone splays wide enough at
        /// range to miss a small meteor with every bullet but the middle one,
        /// while 0.2 keeps all three lanes inside the smallest enemy's hitbox.
        /// </summary>
        private const float LaneSpacing = 0.2f;
        private const float BaseCriticalMultiplier = 2f;

        private const int RangeRingSegments = 96;
        private const float RangeRingIdleAlpha = 0.08f;
        private const float RangeRingEngagedAlpha = 0.2f;

        private EffectRunner effects;
        private PassiveUpgrades passiveUpgrades;
        private CombatWorld world;
        private float fireTimer;
        private bool engaged;
        private LineRenderer rangeRing;
        private float rangeRingAlpha = -1f;
        private float regenerationTimer;
        private float reviveProtection;
        [Header("Partes (vindas do prefab)")]
        [SerializeField] private SpriteRenderer aura;
        [SerializeField] private SpriteRenderer hull;
        [SerializeField] private SpriteRenderer shieldRing;
        private Color hullColor;
        private Color HullTint => ArtLibrary.ShipTint(definition != null ? definition.id : "", hullColor);
        private ShipFeedbackVfx feedbackVfx;
        private float hullBaseScale = 1f;
        private float shieldBaseScale = 1f;

        public bool isAlive
        {
            get { return hp > 0f; }
        }

        public float hpFraction
        {
            get { return maxHp <= 0f ? 0f : Mathf.Clamp01(hp / maxHp); }
        }

        // ------------------------------------------------------------------

        public void Setup(ShipDefinition definition, CombatWorld world, EffectRunner effects,
            PassiveUpgrades passiveUpgrades)
        {
            this.definition = definition;
            this.world = world;
            this.effects = effects;
            this.passiveUpgrades = passiveUpgrades;
            Active = this;

            maxHp = definition.maxHp * (1f + PassiveLevel(PassiveUpgrade.Hull) * 0.05f);
            hp = maxHp;
            shieldCharges = 0;
            shieldTimeLeft = 0f;
            ClearSiegeCharge();
            fireTimer = 0f;
            regenerationTimer = 0f;
            reviveProtection = 0f;
            ResetSpecial();

            transform.position = Arena.ShipPosition;

            hullColor = SpriteFactory.ParseColor(definition.tint);

            // The prefab carries these three renderers; they are only built here
            // when the ship was created procedurally.
            var auraSprite = ArtLibrary.Glow;
            if (aura == null)
            {
                aura = SpriteFactory.NewRenderer("Aura", auraSprite, Color.white, 4, transform);
            }
            aura.sprite = auraSprite;
            aura.color = new Color(hullColor.r, hullColor.g, hullColor.b, 0.2f);
            aura.transform.localScale =
                Vector3.one * ArtLibrary.ScaleToDiameter(auraSprite, radius * 6f);
            LeanTween.cancel(aura.gameObject);
            Juice.Breathe(aura, 0.12f, 0.26f, 1.7f);

            var hullSprite = ArtLibrary.ShipFor(definition.id);
            hullBaseScale = ArtLibrary.ScaleToDiameter(hullSprite, radius * 2.4f);
            if (hull == null)
            {
                hull = SpriteFactory.NewRenderer("Hull", hullSprite, hullColor, 20, transform);
            }
            hull.sprite = hullSprite;
            hull.color = HullTint;
            hull.enabled = true;
            hull.transform.localScale = Vector3.one * hullBaseScale;
            hull.transform.localPosition = Vector3.zero;

            var shieldSprite = ArtLibrary.DashedRing;
            shieldBaseScale = ArtLibrary.ScaleToDiameter(shieldSprite, radius * 3.6f);
            if (shieldRing == null)
            {
                shieldRing = SpriteFactory.NewRenderer("Shield", shieldSprite,
                    new Color(0.55f, 0.9f, 1f, 0.9f), 21, transform);
            }
            shieldRing.sprite = shieldSprite;
            shieldRing.transform.localScale = Vector3.one * shieldBaseScale;
            shieldRing.gameObject.SetActive(false);

            BuildRangeRing();
            SetupSignature();
            SetupShipGlow();
            if (specialVfx == null) specialVfx = new ShipSpecialVfx(transform);
            if (feedbackVfx == null) feedbackVfx = new ShipFeedbackVfx(transform, radius);
            else feedbackVfx.Clear();

            // The ship lands in the arena rather than appearing in it.
            Juice.PopIn(transform, 1f, 0.5f);
        }

        private void Update()
        {
            if (!isAlive)
            {
                return;
            }

            var dt = Time.deltaTime;

            TickShield(dt);
            reviveProtection = Mathf.Max(0f, reviveProtection - dt);
            TickRegeneration(dt);
            TickFiring(dt);
            TickSignature(dt);
            TickShipGlow(dt);
            TickRangeRing(dt);
            CheckCollisions();
        }

        // ------------------------------------------------------------------
        // Detection range
        // ------------------------------------------------------------------

        /// <summary>
        /// A faint circle at <see cref="Arena.DetectionRadius"/>. Without it the
        /// player sees enemies on screen that the ship ignores and cannot tell
        /// why; with it, "they are not in range yet" reads at a glance.
        /// </summary>
        private void BuildRangeRing()
        {
            if (rangeRing == null)
            {
                var go = new GameObject("RangeRing");
                go.transform.SetParent(transform, false);
                rangeRing = go.AddComponent<LineRenderer>();
                rangeRing.useWorldSpace = false;
                rangeRing.loop = true;
                rangeRing.material = ArtLibrary.SpriteMaterial;
                rangeRing.widthMultiplier = 0.04f;
                rangeRing.numCapVertices = 0;
                rangeRing.numCornerVertices = 0;
                rangeRing.alignment = LineAlignment.View;
                rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rangeRing.receiveShadows = false;
                // Above the starfield, under every enemy and bullet.
                rangeRing.sortingOrder = 2;
                rangeRing.positionCount = RangeRingSegments;
                for (var i = 0; i < RangeRingSegments; i++)
                {
                    var angle = i / (float)RangeRingSegments * Mathf.PI * 2f;
                    rangeRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                        Arena.DetectionRadius);
                }
            }

            rangeRing.enabled = true;
            engaged = false;
            rangeRingAlpha = -1f;
            TickRangeRing(0f);
        }

        private void TickRangeRing(float dt)
        {
            if (rangeRing == null)
            {
                return;
            }

            // Brightens while something is inside it, so the ring also says
            // "the cannon is working" without another HUD element.
            var target = engaged ? RangeRingEngagedAlpha : RangeRingIdleAlpha;
            var alpha = rangeRingAlpha < 0f ? target : Mathf.MoveTowards(rangeRingAlpha, target, dt * 0.6f);
            if (Mathf.Approximately(alpha, rangeRingAlpha))
            {
                return;
            }
            rangeRingAlpha = alpha;
            var color = new Color(hullColor.r, hullColor.g, hullColor.b, alpha);
            rangeRing.startColor = color;
            rangeRing.endColor = color;
        }

        // ------------------------------------------------------------------
        // Auto cannon
        // ------------------------------------------------------------------

        private void TickFiring(float dt)
        {
            var mods = effects.Modifiers;

            fireTimer -= dt;
            if (fireTimer > 0f)
            {
                return;
            }

            var target = SelectTarget();
            engaged = target != null;
            if (target == null)
            {
                fireTimer = 0f;
                return;
            }

            var interval = Mathf.Max(0.03f, definition.fireInterval * mods.fireIntervalScale / SpecialFireRate /
                (1f + PassiveLevel(PassiveUpgrade.FireRate) * 0.02f));
            fireTimer = interval;

            var direction = ((Vector2)target.transform.position - Arena.ShipPosition).normalized;
            hull.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            var shots = Mathf.Max(1, mods.projectileMultiplier);
            var side = new Vector2(-direction.y, direction.x);
            var muzzle = Arena.ShipPosition + direction * radius;

            var volley = BeginVolley(target);
            for (var i = 0; i < shots; i++)
            {
                var lane = (i - (shots - 1) * 0.5f) * LaneSpacing;
                var damage = RollDamage(mods, target, true) * volley.damageScale;
                var sieged = siegeDamage > 0f;
                // Orion's charged shell rides on the first lane, like a siege charge.
                var blast = sieged ? siegeDamage : i == 0 && volley.charged ? damage * ChargedBlastShare : 0f;
                var blastRadius = sieged ? siegeRadius : ChargedBlastRadius;
                var projectile = Projectile.Spawn(muzzle + side * lane, direction, damage, mods.pierce || volley.pierce,
                    world, target, blast, blastRadius);
                StyleShot(projectile, volley, i == 0, sieged, mods.pierce);
                QueueEcho(muzzle + side * lane, direction, damage);
                // One heavy projectile per charge, even with multiple cannon lanes.
                ClearSiegeCharge();
            }
            FireSignatureExtras(muzzle, direction, mods);

            // Disparo Diagonal: pairs fanned either side of the aim, weaker shots.
            int pairs;
            float fanAngle, fanScale;
            if (effects.DiagonalShots(out pairs, out fanAngle, out fanScale))
            {
                var aim = Mathf.Atan2(direction.y, direction.x);
                for (var p = 1; p <= pairs; p++)
                {
                    var spread = fanAngle * (pairs > 1 ? p / (float)pairs * 1.6f : 1f) * Mathf.Deg2Rad;
                    for (var s = -1; s <= 1; s += 2)
                    {
                        var fan = new Vector2(Mathf.Cos(aim + spread * s), Mathf.Sin(aim + spread * s));
                        Projectile.Spawn(muzzle, fan, RollDamage(mods, null) * fanScale, mods.pierce, world);
                    }
                }
            }

            PlayMuzzleFeedback(direction, shots);
            if (volley.charged) ChargedRecoil(direction);
        }

        /// <summary>
        /// Recoil and a muzzle flash. With no player input, this is the only
        /// thing that makes the auto-cannon feel operated rather than scripted -
        /// so it scales with how much is being fired.
        /// </summary>
        private void PlayMuzzleFeedback(Vector2 direction, int shots)
        {
            var muzzle = Arena.ShipPosition + direction * (radius * 1.15f);
            var flash = MuzzleColor();
            // A white-hot centre inside a flash in the ship's own colour, and a
            // couple of sparks thrown forward.
            Impact.Spark(muzzle, flash, 0.5f + shots * 0.05f);
            Impact.Spark(muzzle, Color.white, 0.22f + shots * 0.03f);
            ShotFx.Shards(muzzle, direction, flash, 2, 5f, 22f);
            if (signature == Signature.Plates) Impact.Ring(muzzle, PlateColor, 0.15f, 0.7f, 0.14f);

            var kick = -(Vector3)direction * Mathf.Min(0.06f + shots * 0.012f, 0.16f);
            LeanTween.cancel(hull.gameObject, false, TweenAction.MOVE_LOCAL);
            hull.transform.localPosition = kick;
            LeanTween.moveLocal(hull.gameObject, Vector3.zero, 0.11f)
                .setEase(LeanTweenType.easeOutCubic);
        }

        private float RollDamage(CombatModifiers mods, Enemy target, bool mainCannon = false)
        {
            var damage = definition.damagePerShot * mods.damageMultiplier * SpecialDamage *
                (1f + PassiveLevel(PassiveUpgrade.Damage) * 0.03f);
            if (target != null && target.isBoss) damage *= 1f + mods.bossDamageBonus;

            var criticalMultiplier = BaseCriticalMultiplier +
                PassiveLevel(PassiveUpgrade.CriticalDamage) * 0.05f;

            var crit = mainCannon && effects != null && effects.ConsumeCannonCritical();
            if (crit || SignatureCrit(target))
            {
                damage *= criticalMultiplier;
            }
            else if (UnityEngine.Random.value < Mathf.Min(1f,
                mods.critChance + PassiveLevel(PassiveUpgrade.CriticalChance) * 0.01f))
            {
                damage *= criticalMultiplier;
            }

            return damage;
        }

        // ------------------------------------------------------------------
        // Shield
        // ------------------------------------------------------------------

        private void TickShield(float dt)
        {
            if (shieldCharges <= 0)
            {
                return;
            }
            shieldTimeLeft -= dt;
            if (shieldTimeLeft <= 0f)
            {
                SetShield(0, 0f);
            }
        }

        /// <summary>
        /// Grants shield charges. A fresh cast replaces the old one rather than
        /// stacking, so the Defesa card cannot be banked across waves.
        /// </summary>
        public void SetShield(int charges, float duration)
        {
            var had = shieldCharges;
            shieldCharges = Mathf.Max(0, charges);
            shieldTimeLeft = shieldCharges > 0 ? duration : 0f;

            var active = shieldCharges > 0;
            shieldRing.gameObject.SetActive(active);

            if (active)
            {
                LeanTween.cancel(shieldRing.gameObject);
                shieldRing.color = new Color(0.55f, 0.9f, 1f, 0.9f);

                // A block flashes hard; gaining the shield just settles in.
                var blocked = had > shieldCharges;
                Juice.PopIn(shieldRing.transform, shieldBaseScale, blocked ? 0.18f : 0.3f);
                Juice.Breathe(shieldRing, 0.45f, 0.95f, 0.9f);

                if (blocked)
                {
                    Impact.Ring(Arena.ShipPosition, new Color(0.6f, 0.95f, 1f),
                        radius * 2f, radius * 5f, 0.3f);
                    Juice.Shake(0.14f, 0.18f);
                }
            }

            if (onShieldChanged != null)
            {
                onShieldChanged();
            }
        }

        public void AddShieldCharges(int extra, float duration)
        {
            SetShield(shieldCharges + extra, Mathf.Max(shieldTimeLeft, duration));
        }

        // ------------------------------------------------------------------
        // Contact damage
        // ------------------------------------------------------------------

        private void CheckCollisions()
        {
            var enemies = world.Enemies;

            // Iterated backwards because a contact kill removes from this very
            // list; the bounds check covers fragments removing more than one.
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count)
                {
                    continue;
                }

                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive)
                {
                    continue;
                }

                var distance = ((Vector2)enemy.transform.position - Arena.ShipPosition).magnitude;
                if (distance > radius + enemy.radius)
                {
                    continue;
                }

                if (shieldCharges > 0)
                {
                    Sfx.Play(Sound.ShieldBlock);
                    SetShield(shieldCharges - 1, shieldTimeLeft);
                    enemy.Consume();
                    if (effects != null)
                    {
                        effects.OnShieldBlocked();
                        effects.OnHullContact();
                    }
                    continue;
                }

                TakeDamage(enemy.definition.collisionDamage,
                    (Vector2)enemy.transform.position - Arena.ShipPosition);
                enemy.Consume();
                if (isAlive && effects != null) effects.OnHullContact();

                if (!isAlive)
                {
                    return;
                }
            }

        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector2.zero);
        }

        /// <param name="towards">From the ship to what hit it; zero when unknown.</param>
        public void TakeDamage(float amount, Vector2 towards)
        {
            if (!isAlive || amount <= 0f || reviveProtection > 0f)
            {
                return;
            }
            if (SpecialProtected)
            {
                AegisReflect(amount, towards);
                return;
            }

            if (UnityEngine.Random.value < PassiveLevel(PassiveUpgrade.Evasion) * 0.015f)
            {
                Juice.Number(transform.position + Vector3.up * 0.8f, "ESQUIVA",
                    new Color(0.55f, 0.9f, 1f), 1f);
                return;
            }

            amount *= 1f - (effects != null ? effects.Modifiers.damageReduction : 0f);
            amount = AbsorbWithBubble(amount);
            if (amount <= 0f) return;
            hp = Mathf.Max(0f, hp - amount);
            OnSignatureDamaged();
            var phoenixTriggered = hp <= 0f && effects != null && effects.TryPhoenix(out _);

            Juice.FlashSprite(hull, new Color(1f, 0.25f, 0.25f), HullTint, 0.22f);
            Juice.Number(transform.position + Vector3.up * 0.8f, "-" + Mathf.CeilToInt(amount),
                new Color(1f, 0.4f, 0.4f), 1.15f);

            LeanTween.cancel(hull.gameObject, false, TweenAction.SCALE);
            hull.transform.localScale = Vector3.one * (hullBaseScale * 1.25f);
            LeanTween.scale(hull.gameObject, Vector3.one * hullBaseScale, 0.25f)
                .setEase(LeanTweenType.easeOutBack);

            // Sparks off the impact, scaled by how much of the hull the hit
            // took. The screen's reaction (rim, shake, hit stop) is the HUD's.
            feedbackVfx?.Hit(maxHp > 0f ? amount / maxHp : 0f, towards);

            if (onDamaged != null)
            {
                onDamaged(amount);
            }

            if (phoenixTriggered) return;

            if (hp > 0f)
            {
                if (effects != null) effects.OnHullDamaged();
                return;
            }

            Impact.Burst(Arena.ShipPosition, hullColor, radius * 5f);
            specialVfx?.Stop();
            Impact.Debris(Arena.ShipPosition, hullColor, radius * 4f, 12);
            Juice.Shake(0.5f, 0.9f);
            Juice.HitStop(0.2f);
            hull.enabled = false;
            SetShipGlow(false);
            if (rangeRing != null)
            {
                rangeRing.enabled = false;
            }

            if (onDied != null)
            {
                onDied();
            }
        }

        internal void RestoreFromPhoenix(float hullFraction)
        {
            hp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hullFraction));
            reviveProtection = Mathf.Max(reviveProtection, 2f);
        }

        public void Revive()
        {
            if (isAlive) return;
            hp = maxHp;
            reviveProtection = 3f;
            fireTimer = 0f;
            regenerationTimer = 0f;
            hull.enabled = true;
            SetShipGlow(true);
            hull.color = HullTint;
            hull.transform.localScale = Vector3.one * hullBaseScale;
            if (rangeRing != null) rangeRing.enabled = true;
            onDamaged?.Invoke(0f);
        }

        public void Heal(float amount)
        {
            ApplyHealing(amount, true);
        }

        private void ApplyHealing(float amount, bool showFeedback)
        {
            if (!isAlive || amount <= 0f) return;
            // Sobrecura: whatever does not fit in the hull can become shield.
            var overflow = hp + amount - maxHp;
            if (overflow > 0f && effects != null) effects.OnOverheal(overflow);
            if (overflow > 0f) AddBubble(overflow);

            // At full hull there is nothing to show: Furia de Batalha would
            // otherwise flash the ship and float a "+1" on every kill.
            if (hp >= maxHp)
            {
                return;
            }
            var previousHp = hp;
            hp = Mathf.Min(maxHp, hp + amount);
            var recovered = hp - previousHp;
            if (recovered <= 0f) return;
            if (effects != null) effects.OnHealed(recovered);

            if (showFeedback)
            {
                Juice.FlashSprite(hull, new Color(0.5f, 1f, 0.65f), hullColor, 0.3f);
                Juice.Number(transform.position + Vector3.up * 0.8f, "+" + Mathf.CeilToInt(recovered),
                    new Color(0.5f, 1f, 0.65f), 1f);
                Impact.Ring(Arena.ShipPosition, new Color(0.5f, 1f, 0.7f), radius, radius * 4f, 0.4f);
                feedbackVfx?.Heal(maxHp > 0f ? recovered / maxHp : 0f);
            }
            else
            {
                // Regeneration: a green mote now and then shows it working.
                feedbackVfx?.Regenerate();
            }

            if (onDamaged != null)
            {
                onDamaged(0f);
            }
        }

        private int PassiveLevel(PassiveUpgrade upgrade)
        {
            return passiveUpgrades == null ? 0 : passiveUpgrades.Level(definition.id, upgrade);
        }

        private void TickRegeneration(float dt)
        {
            var rate = PassiveLevel(PassiveUpgrade.Regeneration) * 0.2f +
                (effects != null ? effects.RegenFraction * maxHp : 0f);
            if (rate <= 0f || hp >= maxHp) return;
            regenerationTimer += dt;
            if (regenerationTimer < 0.25f) return;
            var amount = rate * regenerationTimer;
            regenerationTimer = 0f;
            ApplyHealing(amount, false);
        }
    }
}
