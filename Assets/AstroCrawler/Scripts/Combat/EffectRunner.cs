// AstroCrawler - real-time resolution of card effects.
//
// This replaces CCG Kit's EffectSolver wholesale. The kit resolves effects as
// discrete, server-authoritative steps inside a turn; AstroCrawler resolves them
// as timed states layered over a continuously firing ship. What carries over is
// the shape of the contract: a card is data, and one place turns that data into
// game state.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// The aggregate of every active effect, recomputed once per frame and read
    /// by <see cref="PlayerShip"/> when it fires and when it takes contact.
    /// </summary>
    public class CombatModifiers
    {
        public int projectileMultiplier = 1;

        /// <summary>Multiplies the ship's base fire interval; lower is faster.</summary>
        public float fireIntervalScale = 1f;

        public float damageMultiplier = 1f;
        public float damageReduction;
        public float bossDamageBonus;
        public bool pierce;

        public float critChance;


        public void Reset()
        {
            projectileMultiplier = 1;
            fireIntervalScale = 1f;
            damageMultiplier = 1f;
            damageReduction = 0f;
            bossDamageBonus = 0f;
            pierce = false;
            critChance = 0f;
        }
    }

    /// <summary>
    /// A card effect that is currently ticking down.
    /// </summary>
    public class TimedEffect
    {
        public CardDefinition definition;
        public int level = 1;
        public bool permanent;
        public float remaining;
        public float total;

        public CardEffectId id
        {
            get { return definition.effect; }
        }

        public float Param(string key, float fallback = 0f)
        {
            return definition.Param(level, key, fallback);
        }

        public int ParamInt(string key, int fallback = 0)
        {
            return Mathf.RoundToInt(Param(key, fallback));
        }

        public float fraction
        {
            get { return total <= 0f ? 0f : Mathf.Clamp01(remaining / total); }
        }
    }

    public partial class EffectRunner : MonoBehaviour
    {
        private readonly List<TimedEffect> active = new List<TimedEffect>();
        // Abilities that fire by themselves on a cooldown, and the combat
        // seconds left until each one is ready (same index).
        private readonly List<RuntimeCard> automaticUpgrades = new List<RuntimeCard>();
        private readonly List<float> automaticCooldowns = new List<float>();

        /// <summary>Card parameter: seconds of combat between activations.</summary>
        public const string CooldownKey = "cooldown";

        // A freshly picked ability fires almost at once, so the pick is felt.
        private const float FirstActivationDelay = 0.8f;
        private readonly CombatModifiers modifiers = new CombatModifiers();
        private readonly List<Enemy> buffer = new List<Enemy>();

        private PlayerShip ship;
        private CombatWorld world;
        private CardSystem cards;

        // Run-long state granted by evolved Tiro Penetrante (GDD 3).
        private bool permanentPierce;
        private float permanentDamageBonus;

        private int modifiersFrame = -1;
        private float lightningCooldown;
        private readonly HashSet<int> lightningVisited = new HashSet<int>();
        private bool resolvingPermanent;
        private bool resolvingAutomaticUpgrade;

        /// <summary>Raised whenever a card resolves, for HUD feedback.</summary>
        public event Action<RuntimeCard> onCardResolved;

        public IReadOnlyList<TimedEffect> ActiveEffects
        {
            get { return active; }
        }

        /// <summary>
        /// Wires the runner to the arena. Called once at boot, before any run
        /// exists, so every per-frame path below tolerates a null ship.
        /// </summary>
        public void Initialize(CombatWorld world)
        {
            this.world = world;

            world.onEnemyKilled -= HandleEnemyKilled;
            world.onEnemyKilled += HandleEnemyKilled;
            world.onCannonHit -= HandleCannonHit;
            world.onCannonHit += HandleCannonHit;
        }

        /// <summary>Binds the runner to the ship and upgrade system of a starting run.</summary>
        public void AttachRun(PlayerShip ship, CardSystem cards)
        {
            this.ship = ship;
            this.cards = cards;
        }

        private void OnDestroy()
        {
            if (world != null)
            {
                world.onEnemyKilled -= HandleEnemyKilled;
                world.onCannonHit -= HandleCannonHit;
            }
        }

        /// <summary>Wipes every effect. Called when a run ends.</summary>
        public void ResetForRun()
        {
            guaranteedCriticalShots = 0;
            if (ship != null) ship.ClearSiegeCharge();
            lightningCooldown = 0f;
            lightningVisited.Clear();
            active.Clear();
            automaticUpgrades.Clear();
            automaticCooldowns.Clear();
            permanentPierce = false;
            permanentDamageBonus = 0f;
            modifiersFrame = -1;
            ResetArsenal();
            ResetChristmas();
            if (world != null)
            {
                world.globalEnemySpeedScale = 1f;
            }
        }

        // ------------------------------------------------------------------
        // Per-frame state
        // ------------------------------------------------------------------

        private void Update()
        {
            if (world == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (ship != null && ship.isAlive && run != null && run.phase == RunPhase.WaveInProgress)
                lightningCooldown = Mathf.Max(0f, lightningCooldown - dt);

            for (var i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].permanent)
                {
                    continue;
                }
                active[i].remaining -= dt;
                if (active[i].remaining <= 0f)
                {
                    active.RemoveAt(i);
                    modifiersFrame = -1;
                }
            }

            ApplyWorldEffects(dt);
            UpdateRecurring(dt);
            UpdateArsenal(dt);
            UpdateChristmas(dt);
        }

        /// <summary>
        /// Ticks every recurring ability. Each has its own cooldown, counted
        /// in combat time: it runs through waves and the breather between
        /// them, and stops with the pause and the upgrade pick. A ready
        /// offensive ability waits for a target instead of firing at nothing,
        /// and a wait never banks extra activations.
        /// </summary>
        private void UpdateRecurring(float dt)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (automaticUpgrades.Count == 0 || ship == null || !ship.isAlive || dt <= 0f ||
                run == null || run.phase != RunPhase.WaveInProgress)
            {
                return;
            }

            var targetChecked = false;
            var hasTarget = false;
            for (var i = 0; i < automaticUpgrades.Count; i++)
            {
                if (automaticUpgrades[i].definition.effect == CardEffectId.SiegeRound &&
                    ship.HasSiegeCharge) continue;
                automaticCooldowns[i] = Mathf.Max(0f, automaticCooldowns[i] - dt);
                if (automaticCooldowns[i] > 0f)
                {
                    continue;
                }

                var upgrade = automaticUpgrades[i];
                if (RequiresEnemyInRange(upgrade.definition.effect))
                {
                    if (!targetChecked)
                    {
                        targetChecked = true;
                        hasTarget = world.NearestInRange(Arena.ShipPosition) != null;
                    }
                    if (!hasTarget)
                    {
                        continue;
                    }
                }

                // Before resolving: a kill inside Resolve can end the run and
                // clear these lists.
                automaticCooldowns[i] = CooldownOf(upgrade);
                ResolveAutomatic(upgrade);
                if (i >= automaticUpgrades.Count)
                {
                    return;
                }
            }
        }

        /// <summary>Seconds between activations. Misseis keeps its older "fireInterval" key.</summary>
        private static float CooldownOf(RuntimeCard upgrade)
        {
            return Mathf.Max(0.5f, upgrade.Param(CooldownKey, upgrade.Param("fireInterval", 12f)));
        }

        /// <summary>Blocks the Defesa card keeps the ship at, or 0 without it.</summary>
        private int ShieldCardCharges()
        {
            for (var i = 0; i < automaticUpgrades.Count; i++)
            {
                if (automaticUpgrades[i].definition.effect == CardEffectId.Shield)
                {
                    return automaticUpgrades[i].ParamInt("charges", 1);
                }
            }
            return 0;
        }

        /// <summary>
        /// Effects that act on the arena rather than on the ship: the global slow
        /// and the independent Temporal Paralysis health threshold.
        /// </summary>
        private void ApplyWorldEffects(float dt)
        {
            var speedScale = 1f;
            var paralyzeThreshold = 0f;

            for (var i = 0; i < active.Count; i++)
            {
                var effect = active[i];
                if (effect.id == CardEffectId.TemporalDistortion)
                {
                    speedScale = Mathf.Min(speedScale, 1f - Mathf.Clamp01(effect.Param("slow", 0.1f)));
                }
                else if (effect.id == CardEffectId.TemporalParalysis)
                {
                    paralyzeThreshold = Mathf.Max(paralyzeThreshold,
                        Mathf.Clamp01(effect.Param("paralyzeThreshold")));
                }
            }

            world.globalEnemySpeedScale = speedScale;

            if (paralyzeThreshold > 0f)
            {
                // Only inside the detection range: an enemy frozen outside it
                // can never be shot, and the wave could not end (a gravity well
                // left one at 15% far out and the wave stalled for minutes).
                var enemies = world.Enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy != null && enemy.isAlive && enemy.hpFraction <= paralyzeThreshold &&
                        Arena.InRange(enemy.transform.position, enemy.radius))
                    {
                        enemy.Paralyze(0.25f);
                    }
                }
            }
        }

        /// <summary>
        /// The combined modifier set for this frame. Cached per frame so the ship
        /// can query it from several places without recomputing.
        /// </summary>
        public CombatModifiers Modifiers
        {
            get
            {
                if (modifiersFrame == Time.frameCount)
                {
                    return modifiers;
                }
                modifiersFrame = Time.frameCount;
                Recompute();
                return modifiers;
            }
        }

        private void Recompute()
        {
            modifiers.Reset();
            modifiers.pierce = permanentPierce;
            modifiers.damageMultiplier = 1f + permanentDamageBonus;

            for (var i = 0; i < active.Count; i++)
            {
                var effect = active[i];
                switch (effect.id)
                {
                    case CardEffectId.ArmorPlating:
                        modifiers.damageReduction = Mathf.Clamp(effect.Param("damageReduction"), 0f, 0.5f);
                        break;
                    case CardEffectId.BossHunter:
                        modifiers.bossDamageBonus = Mathf.Max(0f, effect.Param("bossDamageBonus"));
                        break;
                    case CardEffectId.DoubleAttack:
                        modifiers.projectileMultiplier = Mathf.Max(modifiers.projectileMultiplier,
                            Mathf.RoundToInt(effect.Param("projectiles", 2f)));
                        break;

                    case CardEffectId.PiercingShot:
                        modifiers.pierce = true;
                        modifiers.damageMultiplier += effect.Param("damageBonus");
                        break;

                    case CardEffectId.EngineOverload:
                        modifiers.fireIntervalScale *= 1f / (1f + effect.Param("fireRateBonus", 0.5f));
                        break;

                    case CardEffectId.WildVolley:
                        modifiers.critChance = Mathf.Max(modifiers.critChance, effect.Param("critChance", 0.15f));
                        break;

                    default:
                        RecomputeArsenal(effect);
                        break;

                }
            }
        }

        // ------------------------------------------------------------------
        // Playing a card
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves a card the moment it is played. Instantaneous effects fire
        /// here; timed ones are pushed onto the active list.
        /// </summary>
        public void Resolve(RuntimeCard card)
        {
            var definition = card.definition;
            var level = Mathf.Max(1, card.level);
            var origin = Arena.ShipPosition;

            switch (definition.effect)
            {
                case CardEffectId.SiegeRound:
                    ship.SetSiegeCharge(card.Param("explosionDamage"), Arena.ToWorld(card.Param("explosionRadius")));
                    break;
                case CardEffectId.SpecialCooldown:
                    ship.SetSpecialCooldownReduction(card.Param("reduction"));
                    break;
                // ---------------------------------------------------- common
                case CardEffectId.GuaranteedCritical:
                case CardEffectId.DoubleAttack:
                case CardEffectId.BouncingSnowball:
                case CardEffectId.GiantSnowball:
                case CardEffectId.FrostCircle:
                case CardEffectId.GiftBombs:
                case CardEffectId.IceShards:
                case CardEffectId.Snowstorm:
                case CardEffectId.EngineOverload:
                case CardEffectId.ArmorPlating:
                case CardEffectId.BossHunter:
                case CardEffectId.ImpactCharge:
                case CardEffectId.WildVolley:
                case CardEffectId.BattleFury:
                case CardEffectId.ChainLightning:
                case CardEffectId.TemporalDistortion:
                case CardEffectId.TemporalParalysis:
                case CardEffectId.OrbitalBlades:
                case CardEffectId.Shrapnel:
                case CardEffectId.DiagonalShot:
                case CardEffectId.PlasmaAura:
                case CardEffectId.GravityMagnet:
                case CardEffectId.NanoRepair:
                case CardEffectId.PhoenixCore:
                case CardEffectId.KillStreak:
                case CardEffectId.Execution:
                case CardEffectId.OverdriveFury:
                case CardEffectId.Thorns:
                case CardEffectId.ShieldMend:
                case CardEffectId.ParadoxRecharge:
                case CardEffectId.TemporalEcho:
                case CardEffectId.Overheal:
                case CardEffectId.VitalPulse:
                case CardEffectId.EmergencyProtocol:
                case CardEffectId.AmplifyShockwave:
                    AddTimed(definition, level);
                    break;

                case CardEffectId.SpecialDuration:
                    if (ship != null) ship.SetSpecialDurationBonus(card.Param("bonus"));
                    AddTimed(definition, level);
                    break;

                case CardEffectId.SolarNova:
                case CardEffectId.OrbitalStrike:
                case CardEffectId.TemporalRift:
                case CardEffectId.Railgun:
                case CardEffectId.TargetMark:
                    ResolveArsenalAbility(card);
                    break;

                case CardEffectId.PiercingShot:
                    if (card.Param("permanent") > 0f)
                    {
                        permanentPierce = true;
                        permanentDamageBonus += card.Param("damageBonus", 0.2f);
                        modifiersFrame = -1;
                    }
                    else
                    {
                        AddTimed(definition, level);
                    }
                    break;

                case CardEffectId.Shield:
                    // A recharge tops the shield back up; it never removes
                    // blocks another card added on top.
                    if (resolvingAutomaticUpgrade)
                    {
                        ship.SetShield(Mathf.Max(ship.shieldCharges, card.ParamInt("charges", 1)),
                            float.PositiveInfinity);
                    }
                    else
                    {
                        ship.SetShield(card.ParamInt("charges", 1), card.Param("duration", 15f));
                    }
                    break;

                case CardEffectId.Missiles:
                {
                    var count = card.ParamInt("count", 2);
                    var splash = Arena.ToWorld(card.Param("splashRadius"));
                    var targets = world.RandomEnemiesInRange(count);
                    for (var i = 0; i < count; i++)
                    {
                        var target = targets.Count > 0 ? targets[i % targets.Count] : null;
                        if (target == null)
                        {
                            break;
                        }
                        Missile.Spawn(origin, target, card.Param("damage", 12f), splash, world);
                    }
                    break;
                }

                case CardEffectId.BlackHole:
                    SpawnWell(card, 1, card.Param("radius", 150f));
                    break;

                // -------------------------------------------------- vanguard
                // (handled by the timed block above)

                // --------------------------------------------------- bastion
                case CardEffectId.RepulsionField:
                {
                    var radius = Arena.ToWorld(card.Param("radius", 120f));
                    var push = card.Param("push", 4f);
                    var slow = card.Param("slow");
                    var slowDuration = card.Param("slowDuration");

                    Pulse.Spawn(origin, radius, new Color(0.5f, 0.85f, 1f, 0.9f));
                    world.InRadius(origin, radius, buffer);
                    for (var i = 0; i < buffer.Count; i++)
                    {
                        var away = ((Vector2)buffer[i].transform.position - origin);
                        if (away.sqrMagnitude < 0.0001f)
                        {
                            away = UnityEngine.Random.insideUnitCircle.normalized;
                        }
                        buffer[i].Push(away.normalized * push);
                        if (slow > 0f)
                        {
                            buffer[i].ApplySlow(1f - slow, slowDuration);
                        }
                    }
                    break;
                }

                case CardEffectId.EmergencyRepair:
                    ship.Heal(ship.maxHp * card.Param("healPercent", 0.2f));
                    if (card.Param("extraShield") > 0f)
                    {
                        if (!resolvingAutomaticUpgrade)
                        {
                            ship.AddShieldCharges(card.ParamInt("extraShield"), card.Param("shieldDuration", 5f));
                        }
                        else
                        {
                            // On a cooldown this would otherwise bank blocks
                            // every cycle; it only replaces the ones it gave.
                            var missing = ShieldCardCharges() + card.ParamInt("extraShield") - ship.shieldCharges;
                            if (missing > 0)
                            {
                                ship.AddShieldCharges(missing, float.PositiveInfinity);
                            }
                        }
                    }
                    Pulse.Spawn(origin, 1.2f, new Color(0.5f, 1f, 0.6f, 0.9f));
                    break;

                case CardEffectId.DebrisWall:
                {
                    var threat = world.NearestInRange(origin);
                    if (threat == null)
                    {
                        threat = world.Nearest(origin);
                    }
                    var direction = threat != null
                        ? ((Vector2)threat.transform.position - origin).normalized
                        : Vector2.up;
                    var position = origin + direction * card.Param("distance", 2.2f);
                    DebrisWall.Spawn(position, card.Param("hp", 10f), card.Param("duration", 6f),
                        card.Param("size", 1.1f), world);
                    break;
                }

                // --------------------------------------------------- spectre
                case CardEffectId.SupportDrones:
                {
                    SupportDrones.Spawn(card.ParamInt("drones", 2),
                        card.Param("damage", 5f), card.Param("fireInterval", 1.2f),
                        card.Param("duration", 12f), world);
                    break;
                }
            }

            if (onCardResolved != null)
            {
                onCardResolved(card);
            }
        }

        /// <summary>
        /// Installs an upgrade for the rest of the run. Modifier-style effects
        /// never expire. Abilities become recurring: the first activation
        /// comes right after the pick, then one per cooldown. Selecting the
        /// same upgrade again promotes it to its evolved values and keeps the
        /// cooldown already running.
        /// </summary>
        public void ResolvePermanent(RuntimeCard upgrade)
        {
            // The install beat. Fires for every upgrade, including the ones
            // whose effect is a passive modifier with nothing to show for it -
            // otherwise half the picks would land with no feedback at all.
            var tint = SpriteFactory.ParseColor(upgrade.definition.tint);
            Impact.Ring(Arena.ShipPosition, tint, 0.6f, 4.5f, 0.55f);
            Impact.Burst(Arena.ShipPosition, tint, 1.2f);

            if (IsRecurring(upgrade.definition.effect))
            {
                for (var i = 0; i < automaticUpgrades.Count; i++)
                {
                    if (automaticUpgrades[i].definition == upgrade.definition)
                    {
                        automaticUpgrades[i] = upgrade;
                        if (upgrade.definition.effect == CardEffectId.SiegeRound && ship.HasSiegeCharge)
                            ship.SetSiegeCharge(upgrade.Param("explosionDamage"), Arena.ToWorld(upgrade.Param("explosionRadius")));
                        automaticCooldowns[i] = Mathf.Min(automaticCooldowns[i], CooldownOf(upgrade));
                        return;
                    }
                }
                automaticUpgrades.Add(upgrade);
                automaticCooldowns.Add(upgrade.definition.effect == CardEffectId.SiegeRound
                    ? CooldownOf(upgrade) : FirstActivationDelay);
                return;
            }

            resolvingPermanent = true;
            try
            {
                Resolve(upgrade);
            }
            finally
            {
                resolvingPermanent = false;
            }
        }

        private void ResolveAutomatic(RuntimeCard upgrade)
        {
            resolvingAutomaticUpgrade = true;
            try
            {
                Resolve(upgrade);
            }
            finally
            {
                resolvingAutomaticUpgrade = false;
            }
        }

        private static bool RequiresEnemyInRange(CardEffectId id)
        {
            return id != CardEffectId.Shield && id != CardEffectId.EmergencyRepair &&
                id != CardEffectId.SiegeRound;
        }

        /// <summary>True for abilities that fire on a cooldown rather than modify the ship.</summary>
        public static bool IsRecurring(CardEffectId id)
        {
            return !IsPersistentModifier(id);
        }

        private static bool IsPersistentModifier(CardEffectId id)
        {
            switch (id)
            {
                case CardEffectId.GuaranteedCritical:
                case CardEffectId.BouncingSnowball:
                case CardEffectId.GiantSnowball:
                case CardEffectId.FrostCircle:
                case CardEffectId.GiftBombs:
                case CardEffectId.IceShards:
                case CardEffectId.Snowstorm:
                case CardEffectId.ArmorPlating:
                case CardEffectId.BossHunter:
                case CardEffectId.SpecialCooldown:
                case CardEffectId.DoubleAttack:
                case CardEffectId.PiercingShot:
                case CardEffectId.EngineOverload:
                case CardEffectId.ImpactCharge:
                case CardEffectId.WildVolley:
                case CardEffectId.BattleFury:
                case CardEffectId.ChainLightning:
                case CardEffectId.TemporalDistortion:
                case CardEffectId.TemporalParalysis:
                    return true;
                case CardEffectId.OrbitalBlades:
                case CardEffectId.Shrapnel:
                case CardEffectId.DiagonalShot:
                case CardEffectId.PlasmaAura:
                case CardEffectId.GravityMagnet:
                case CardEffectId.NanoRepair:
                case CardEffectId.PhoenixCore:
                case CardEffectId.KillStreak:
                case CardEffectId.Execution:
                case CardEffectId.OverdriveFury:
                case CardEffectId.Thorns:
                case CardEffectId.SpecialDuration:
                case CardEffectId.ShieldMend:
                case CardEffectId.ParadoxRecharge:
                case CardEffectId.TemporalEcho:
                case CardEffectId.Overheal:
                case CardEffectId.VitalPulse:
                case CardEffectId.EmergencyProtocol:
                case CardEffectId.AmplifyShockwave:
                    return true;
                default:
                    return false;
            }
        }

        private void SpawnWell(RuntimeCard card, int count, float designRadius)
        {
            var radius = Arena.ToWorld(designRadius);
            var duration = card.Param("duration", 6f);
            var dps = card.Param("dps", 6f);
            var pull = card.Param("pull", 1.6f);
            var collapse = card.Param("collapseDamage");

            for (var i = 0; i < Mathf.Max(1, count); i++)
            {
                var position = PickWellPosition(i, count);
                BlackHole.Spawn(position, radius, duration, dps, pull, collapse, world);
            }
        }

        /// <summary>
        /// Drops a well where it will actually catch something: on the densest
        /// cluster of enemies when there is one, otherwise just ahead of the hull.
        /// </summary>
        private Vector2 PickWellPosition(int index, int count)
        {
            var target = world.NearestInRange(Arena.ShipPosition);
            var anchor = target != null
                ? (Vector2)target.transform.position
                : Arena.ShipPosition + Vector2.up * 3f;

            if (count <= 1)
            {
                return anchor;
            }

            var angle = (index / (float)count) * Mathf.PI * 2f;
            return anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.6f;
        }

        private void AddTimed(CardDefinition definition, int level)
        {
            var duration = definition.Param(level, "duration", 8f);

            // A second copy refreshes rather than stacks.
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].definition == definition)
                {
                    active[i].level = level;
                    active[i].permanent |= resolvingPermanent;
                    active[i].remaining = active[i].permanent ? float.PositiveInfinity : duration;
                    active[i].total = active[i].remaining;
                    modifiersFrame = -1;
                    return;
                }
            }

            active.Add(new TimedEffect
            {
                definition = definition,
                level = level,
                permanent = resolvingPermanent,
                remaining = resolvingPermanent ? float.PositiveInfinity : duration,
                total = resolvingPermanent ? float.PositiveInfinity : duration
            });
            modifiersFrame = -1;
        }

        public bool IsActive(CardEffectId id)
        {
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private int guaranteedCriticalShots;

        /// <summary>Counts actual main-cannon projectiles; auxiliary attacks do not advance it.</summary>
        public bool ConsumeCannonCritical()
        {
            var effect = Find(CardEffectId.GuaranteedCritical);
            if (effect == null) return false;
            guaranteedCriticalShots++;
            if (guaranteedCriticalShots < Mathf.Max(1, effect.ParamInt("shots", 6))) return false;
            guaranteedCriticalShots = 0;
            return true;
        }

        private TimedEffect Find(CardEffectId id)
        {
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].id == id)
                {
                    return active[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Healing and kill triggers
        // ------------------------------------------------------------------

        /// <summary>One radial volley per effective heal, including passive regeneration.</summary>
        public void OnHealed(float recovered)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (recovered <= 0f || ship == null || !ship.isAlive || world == null ||
                run == null || run.phase != RunPhase.WaveInProgress || Time.deltaTime <= 0f) return;
            OnArsenalHealed(recovered);
            var effect = Find(CardEffectId.ImpactCharge);
            if (effect == null) return;
            var count = Mathf.Clamp(Mathf.RoundToInt(effect.Param("missiles", 6f)), 1, 10);
            var damage = effect.Param("damage", 12f);
            var origin = (Vector2)ship.transform.position;
            Sfx.Play(Sound.MissileLaunch);
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / count;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Projectile.Spawn(origin + direction * (ship.radius + 0.12f), direction,
                    damage, false, world, radialMissile: true);
            }
        }

        private void HandleEnemyKilled(Enemy enemy, bool byPlayerDamage)
        {
            if (!byPlayerDamage)
            {
                return;
            }

            var fury = Find(CardEffectId.BattleFury);
            if (fury != null && ship != null)
            {
                ship.Heal(ship.maxHp * 0.01f * Mathf.Max(1f, fury.Param("returnCards", 1f)));
            }

            OnArsenalKill(enemy);


        }

        /// <summary>Secondary hits never publish cannon-hit events, preventing recursive chains.</summary>
        private void HandleCannonHit(Vector2 origin, int hitSpawnId)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (ship == null || !ship.isAlive || run == null || run.phase != RunPhase.WaveInProgress ||
                Time.deltaTime <= 0f) return;
            OnArsenalCannonHit(origin, hitSpawnId);
            if (lightningCooldown > 0f) return;
            var card = Find(CardEffectId.ChainLightning);
            if (card == null) return;
            lightningVisited.Clear();
            lightningVisited.Add(hitSpawnId);
            var range = Arena.ToWorld(card.Param("jumpRadius"));
            var maxHops = Mathf.Clamp(Mathf.RoundToInt(card.Param("jumps")), 1, 8);
            for (var hop = 0; hop < maxHops; hop++)
            {
                Enemy next = null;
                var distance = range * range;
                var enemies = world.Enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    var candidate = enemies[i];
                    if (candidate == null || !candidate.isAlive || lightningVisited.Contains(candidate.spawnId)) continue;
                    var sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
                    if (sqr <= distance) { distance = sqr; next = candidate; }
                }
                if (next == null) break;
                lightningVisited.Add(next.spawnId);
                var position = (Vector2)next.transform.position;
                // Reserve the cooldown before damage callbacks can change the run.
                lightningCooldown = Mathf.Max(0.1f, card.Param("cooldown"));
                Impact.Lightning(origin, position);
                next.TakeDamage(card.Param("damage"));
                origin = position;
                if (ship == null || !ship.isAlive || run.phase != RunPhase.WaveInProgress) break;
            }
        }
    }
}
