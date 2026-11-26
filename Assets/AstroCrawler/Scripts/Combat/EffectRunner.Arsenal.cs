// AstroCrawler - the base deck cards and the level-unlocked exclusive cards.
//
// Nine shared cards (orbiting blades, shrapnel, diagonal shots, plasma aura,
// solar nova, orbital strike, gravity magnet, nano repair, phoenix core) and
// three more exclusives per ship. Each one is a modifier installed for the run
// (AddTimed, permanent) or an ability on a cooldown (ResolveArsenalAbility);
// the ship calls the hooks below from the moments they care about (contact,
// shield block, overflow healing, low hull, special activation, destruction).

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public partial class EffectRunner
    {
        private struct Echo
        {
            public int spawnId;
            public float damage;
            public float time;
        }

        private static readonly Color BladeColor = new Color(0.62f, 0.95f, 1f);
        private static readonly Color PlasmaColor = new Color(0.45f, 0.85f, 1f);
        private static readonly Color StrikeColor = new Color(0.55f, 0.9f, 1f);
        private static readonly Color RiftColor = new Color(0.72f, 0.48f, 1f);
        private static readonly Color RailColor = new Color(1f, 0.82f, 0.35f);
        private static readonly Color VitalColor = new Color(1f, 0.55f, 0.82f);
        private static readonly Color ThornColor = new Color(0.4f, 0.9f, 0.65f);

        private Transform arsenalRoot;
        private readonly List<SpriteRenderer> blades = new List<SpriteRenderer>();
        private readonly Dictionary<int, float> bladeReady = new Dictionary<int, float>();
        private float bladeAngle;
        private ParticleSystem plasmaFx;
        private float plasmaTick;
        private float plasmaFxTimer;
        private float lastShrapnel = -10f;
        private int streakStacks;
        private float streakTimer;
        private float overdriveExtended;
        private float phoenixCooldown;
        private float overhealPool;
        private float vitalPool;
        private float vitalTimer;
        private float protocolRearm;
        private readonly List<Echo> echoes = new List<Echo>();
        private LineRenderer railLine;
        private readonly List<Enemy> arsenalBuffer = new List<Enemy>();

        // ------------------------------------------------------------------
        // Values the ship reads
        // ------------------------------------------------------------------

        /// <summary>Ima Gravitacional: extra share of XP (0.15 = +15%).</summary>
        public float XpBonus
        {
            get
            {
                var effect = Find(CardEffectId.GravityMagnet);
                return effect != null ? Mathf.Max(0f, effect.Param("xpBonus")) : 0f;
            }
        }

        /// <summary>Nanorreparo: share of max hull regenerated per second.</summary>
        public float RegenFraction
        {
            get
            {
                var effect = Find(CardEffectId.NanoRepair);
                return effect != null ? Mathf.Max(0f, effect.Param("regen")) : 0f;
            }
        }

        /// <summary>Disparo Diagonal: extra shot pairs per volley, their angle and damage share.</summary>
        public bool DiagonalShots(out int pairs, out float angle, out float damageScale)
        {
            var effect = Find(CardEffectId.DiagonalShot);
            pairs = effect != null ? Mathf.Clamp(Mathf.RoundToInt(effect.Param("pairs", 1f)), 1, 3) : 0;
            angle = effect != null ? effect.Param("angle", 25f) : 0f;
            damageScale = effect != null ? Mathf.Clamp01(effect.Param("damageScale", 0.5f)) : 0f;
            return effect != null;
        }

        // ------------------------------------------------------------------
        // Lifetime
        // ------------------------------------------------------------------

        private void ResetArsenal()
        {
            for (var i = 0; i < blades.Count; i++)
                if (blades[i] != null) blades[i].gameObject.SetActive(false);
            bladeReady.Clear();
            bladeAngle = 0f;
            plasmaTick = plasmaFxTimer = 0f;
            HovlVfx.Clear(plasmaFx);
            lastShrapnel = -10f;
            streakStacks = 0;
            streakTimer = 0f;
            overdriveExtended = 0f;
            phoenixCooldown = 0f;
            overhealPool = vitalPool = vitalTimer = protocolRearm = 0f;
            echoes.Clear();
            if (railLine != null) railLine.enabled = false;
        }

        private Transform ArsenalRoot()
        {
            if (arsenalRoot == null)
            {
                arsenalRoot = new GameObject("ArsenalEffects").transform;
                arsenalRoot.SetParent(transform, false);
            }
            return arsenalRoot;
        }

        private bool Fighting(float dt)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            return dt > 0f && ship != null && ship.isAlive && world != null && run != null &&
                run.phase == RunPhase.WaveInProgress;
        }

        // ------------------------------------------------------------------
        // Per frame
        // ------------------------------------------------------------------

        private void UpdateArsenal(float dt)
        {
            var fighting = Fighting(dt);
            UpdateBlades(dt, fighting);
            UpdatePlasma(dt, fighting);

            if (streakStacks > 0 && dt > 0f)
            {
                streakTimer -= dt;
                if (streakTimer <= 0f)
                {
                    streakStacks = 0;
                    modifiersFrame = -1;
                }
            }

            if (fighting)
            {
                protocolRearm = Mathf.Max(0f, protocolRearm - dt);
            }
            TickPhoenix(dt, fighting);
            if (ship != null && !ship.SpecialRunning(ShipSpecialKind.Overdrive)) overdriveExtended = 0f;

            if (echoes.Count > 0)
            {
                var now = Time.time;
                for (var i = echoes.Count - 1; i >= 0; i--)
                {
                    if (i >= echoes.Count || echoes[i].time > now) continue;
                    var echo = echoes[i];
                    echoes.RemoveAt(i);
                    if (!fighting) continue;
                    var target = FindEnemy(echo.spawnId);
                    if (target == null) continue;
                    Impact.Ring(target.transform.position, RiftColor, target.radius * 1.2f, target.radius * 3f, 0.25f);
                    target.TakeDamage(echo.damage);
                }
            }

            if (vitalTimer > 0f) vitalTimer -= dt;
            if (fighting && vitalPool > 0f && vitalTimer <= 0f) ReleaseVitalPulse();
        }

        /// <summary>Lâminas Orbitais: blades circling the hull, each enemy cut at most once per interval.</summary>
        private void UpdateBlades(float dt, bool fighting)
        {
            var effect = Find(CardEffectId.OrbitalBlades);
            var count = effect != null && ship != null && ship.isAlive
                ? Mathf.Clamp(Mathf.RoundToInt(effect.Param("blades", 2f)), 1, 6) : 0;
            while (blades.Count < count) blades.Add(BuildBlade(blades.Count));
            for (var i = 0; i < blades.Count; i++)
                if (blades[i] != null) blades[i].gameObject.SetActive(i < count);
            if (count == 0) return;

            var orbit = Arena.ToWorld(effect.Param("orbit", 100f));
            var damage = effect.Param("damage", 10f);
            var interval = Mathf.Max(0.1f, effect.Param("hitInterval", 0.45f));
            bladeAngle += effect.Param("spin", 220f) * dt;
            var center = Arena.ShipPosition;

            for (var b = 0; b < count; b++)
            {
                var angle = (bladeAngle + b * 360f / count) * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbit;
                var blade = blades[b];
                blade.transform.position = position;
                blade.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                if (!fighting) continue;

                var enemies = world.Enemies;
                for (var i = enemies.Count - 1; i >= 0; i--)
                {
                    if (i >= enemies.Count) continue;
                    var enemy = enemies[i];
                    if (enemy == null || !enemy.isAlive) continue;
                    var reach = enemy.radius + 0.28f;
                    if (((Vector2)enemy.transform.position - position).sqrMagnitude > reach * reach) continue;
                    float ready;
                    if (bladeReady.TryGetValue(enemy.spawnId, out ready) && Time.time < ready) continue;
                    bladeReady[enemy.spawnId] = Time.time + interval;
                    Impact.Spark(position, BladeColor, 0.5f);
                    enemy.TakeDamage(damage);
                    if (ship == null || !ship.isAlive) return;
                }
            }
            if (bladeReady.Count > 256) bladeReady.Clear();
        }

        private SpriteRenderer BuildBlade(int index)
        {
            var sprite = ArtLibrary.Art("diamond") ?? SpriteFactory.Polygon(4);
            var blade = SpriteFactory.NewRenderer("Blade" + index, sprite, BladeColor, 22, ArsenalRoot());
            blade.transform.localScale = new Vector3(ArtLibrary.ScaleToDiameter(sprite, 0.24f),
                ArtLibrary.ScaleToDiameter(sprite, 0.52f), 1f);
            var glow = SpriteFactory.NewRenderer("Glow", ArtLibrary.SoftCircle,
                new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0.35f), 21, blade.transform);
            glow.transform.localScale = Vector3.one * 2.4f;
            return blade;
        }

        /// <summary>Aura de Plasma: damage ticks to everything close to the hull, under a turning ring.</summary>
        private void UpdatePlasma(float dt, bool fighting)
        {
            var effect = Find(CardEffectId.PlasmaAura);
            if (effect == null || ship == null || !ship.isAlive) return;
            var radius = Arena.ToWorld(effect.Param("radius", 100f));

            if (plasmaFx == null)
                plasmaFx = HovlVfx.Emitter(ArsenalRoot(), "PlasmaAura", "Aura4", 6, 3, VfxSize.Steady, VfxFade.InOut);
            plasmaFx.transform.position = Arena.ShipPosition;
            plasmaFxTimer -= dt;
            if (plasmaFxTimer <= 0f && dt > 0f)
            {
                plasmaFxTimer = 0.45f;
                HovlVfx.Emit(plasmaFx, Vector2.zero, Vector2.zero, new Color(PlasmaColor.r, PlasmaColor.g, PlasmaColor.b, 0.32f),
                    1f, radius * 2.2f, Random.Range(0f, 360f), 70f);
            }

            if (!fighting) return;
            plasmaTick -= dt;
            if (plasmaTick > 0f) return;
            plasmaTick = 0.25f;
            var damage = effect.Param("dps", 8f) * 0.25f;
            world.InRadius(Arena.ShipPosition, radius, arsenalBuffer);
            for (var i = 0; i < arsenalBuffer.Count; i++)
                if (arsenalBuffer[i] != null && arsenalBuffer[i].isAlive) arsenalBuffer[i].TakeDamage(damage);
            arsenalBuffer.Clear();
        }

        /// <summary>Modifiers that depend on run state (kill stacks, the Overdrive window).</summary>
        private void RecomputeArsenal(TimedEffect effect)
        {
            switch (effect.id)
            {
                case CardEffectId.KillStreak:
                    if (streakStacks > 0)
                        modifiers.fireIntervalScale *= 1f / (1f + streakStacks * Mathf.Max(0f, effect.Param("bonus", 0.04f)));
                    break;
                case CardEffectId.OverdriveFury:
                    if (ship != null && ship.SpecialRunning(ShipSpecialKind.Overdrive))
                        modifiers.damageMultiplier += Mathf.Max(0f, effect.Param("damageBonus"));
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Abilities on a cooldown
        // ------------------------------------------------------------------

        private void ResolveArsenalAbility(RuntimeCard card)
        {
            if (ship == null || world == null) return;
            var origin = Arena.ShipPosition;
            switch (card.definition.effect)
            {
                case CardEffectId.SolarNova:
                {
                    // Shockwave deals the damage and draws the ring.
                    var radius = Arena.ToWorld(card.Param("radius", 150f));
                    Pulse.Spawn(origin, radius, new Color(1f, 0.78f, 0.35f, 0.9f));
                    Shockwave.Spawn(origin, radius, card.Param("damage", 20f), world);
                    Sfx.Play(Sound.AreaAbility);
                    break;
                }

                case CardEffectId.OrbitalStrike:
                {
                    var targets = world.RandomEnemiesInRange(Mathf.Clamp(card.ParamInt("targets", 2), 1, 8));
                    var damage = card.Param("damage", 22f);
                    for (var i = 0; i < targets.Count; i++)
                    {
                        var target = targets[i];
                        if (target == null || !target.isAlive) continue;
                        var position = (Vector2)target.transform.position;
                        Impact.Lightning(position + new Vector2(Random.Range(-1.5f, 1.5f), 16f), position);
                        Impact.Burst(position, StrikeColor, target.radius * 2.6f);
                        target.TakeDamage(damage);
                        if (!ship.isAlive) break;
                    }
                    if (targets.Count > 0)
                    {
                        Sfx.Play(Sound.AreaAbility);
                        Juice.Shake(0.08f, 0.15f);
                    }
                    break;
                }

                case CardEffectId.TemporalRift:
                {
                    var radius = Arena.ToWorld(card.Param("radius", 110f));
                    var center = DensestCluster(radius);
                    if (center == null) break;
                    var point = center.Value;
                    Pulse.Spawn(point, radius, new Color(RiftColor.r, RiftColor.g, RiftColor.b, 0.9f), 0.5f);
                    Impact.Ring(point, RiftColor, radius * 2.4f, radius * 0.4f, 0.45f);
                    var slow = Mathf.Clamp01(card.Param("slow", 0.5f));
                    var slowDuration = card.Param("slowDuration", 3f);
                    var damage = card.Param("damage", 10f);
                    world.InRadius(point, radius, arsenalBuffer);
                    var hits = new List<Enemy>(arsenalBuffer);
                    arsenalBuffer.Clear();
                    for (var i = 0; i < hits.Count; i++)
                    {
                        if (hits[i] == null || !hits[i].isAlive) continue;
                        hits[i].ApplySlow(1f - slow, slowDuration);
                        hits[i].TakeDamage(damage);
                    }
                    Sfx.Play(Sound.AreaAbility);
                    break;
                }

                case CardEffectId.Railgun:
                    FireRailgun(card);
                    break;

                case CardEffectId.TargetMark:
                {
                    var target = Toughest(true);
                    if (target == null) break;
                    target.Mark(card.Param("bonus", 0.3f), card.Param("duration", 4f));
                    Sfx.Play(Sound.Select);
                    break;
                }
            }
        }

        /// <summary>Canhão de Trilho: a beam from the hull through the toughest enemy, across the arena.</summary>
        private void FireRailgun(RuntimeCard card)
        {
            var target = Toughest(false);
            if (target == null) return;
            var origin = Arena.ShipPosition;
            var direction = ((Vector2)target.transform.position - origin).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
            var end = origin + direction * Arena.LostRadius;
            var halfWidth = Arena.ToWorld(card.Param("width", 30f)) * 0.5f;
            var damage = card.Param("damage", 45f);

            var hits = new List<Enemy>();
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive) continue;
                var point = (Vector2)enemy.transform.position;
                var along = Vector2.Dot(point - origin, direction);
                if (along < 0f) continue;
                var offset = (point - origin - direction * along).magnitude;
                if (offset <= halfWidth + enemy.radius) hits.Add(enemy);
            }
            for (var i = 0; i < hits.Count; i++)
            {
                if (!hits[i].isAlive) continue;
                Impact.Spark(hits[i].transform.position, RailColor, 0.8f);
                hits[i].TakeDamage(damage);
                if (ship == null || !ship.isAlive) break;
            }

            if (railLine == null)
            {
                var go = new GameObject("RailBeam");
                go.transform.SetParent(ArsenalRoot(), false);
                railLine = go.AddComponent<LineRenderer>();
                railLine.useWorldSpace = true;
                railLine.positionCount = 2;
                railLine.material = ArtLibrary.SpriteMaterial;
                railLine.numCapVertices = 4;
                railLine.alignment = LineAlignment.View;
                railLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                railLine.receiveShadows = false;
                railLine.sortingOrder = 26;
            }
            railLine.SetPosition(0, origin + direction * 0.5f);
            railLine.SetPosition(1, end);
            railLine.enabled = true;
            var beam = railLine;
            var width = halfWidth * 2.4f;
            LeanTween.cancel(beam.gameObject);
            LeanTween.value(beam.gameObject, t =>
            {
                if (beam == null) return;
                beam.widthMultiplier = width * t;
                var color = new Color(RailColor.r, RailColor.g, RailColor.b, t);
                beam.startColor = color;
                beam.endColor = new Color(1f, 1f, 1f, t * 0.4f);
                if (t <= 0.001f) beam.enabled = false;
            }, 1f, 0f, 0.35f).setEase(LeanTweenType.easeInQuad);
            Impact.Burst(origin + direction * 0.6f, RailColor, 1.2f);
            Juice.Shake(0.16f, 0.2f);
            Sfx.Play(Sound.MissileLaunch);
        }

        // ------------------------------------------------------------------
        // Triggers
        // ------------------------------------------------------------------

        private void OnArsenalKill(Enemy enemy)
        {
            if (ship == null || !ship.isAlive || enemy == null) return;

            var shrapnel = Find(CardEffectId.Shrapnel);
            if (shrapnel != null && Time.time - lastShrapnel >= Mathf.Max(0.05f, shrapnel.Param("interval", 0.12f)))
            {
                lastShrapnel = Time.time;
                var count = Mathf.Clamp(Mathf.RoundToInt(shrapnel.Param("fragments", 3f)), 1, 10);
                var damage = shrapnel.Param("damage", 6f);
                var origin = (Vector2)enemy.transform.position;
                var turn = Random.Range(0f, Mathf.PI * 2f);
                for (var i = 0; i < count; i++)
                {
                    var angle = turn + i * Mathf.PI * 2f / count;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Projectile.Spawn(origin + direction * (enemy.radius + 0.1f), direction, damage, false, world,
                        radialMissile: true, tintOverride: new Color(1f, 0.78f, 0.4f));
                }
            }

            var streak = Find(CardEffectId.KillStreak);
            if (streak != null)
            {
                streakStacks = Mathf.Min(streakStacks + 1, Mathf.Max(1, streak.ParamInt("maxStacks", 5)));
                streakTimer = Mathf.Max(0.5f, streak.Param("stackDuration", 3f));
                modifiersFrame = -1;
            }

            var fury = Find(CardEffectId.OverdriveFury);
            if (fury != null && ship.SpecialRunning(ShipSpecialKind.Overdrive))
            {
                var extend = fury.Param("killExtend");
                var cap = fury.Param("maxExtend", 4f);
                if (extend > 0f && overdriveExtended < cap)
                {
                    var amount = Mathf.Min(extend, cap - overdriveExtended);
                    overdriveExtended += amount;
                    ship.ExtendSpecial(amount);
                }
            }

            var paradox = Find(CardEffectId.ParadoxRecharge);
            if (paradox != null) ship.ReduceSpecialCooldown(paradox.Param("reduction", 0.3f));
        }

        private void OnArsenalCannonHit(Vector2 origin, int spawnId)
        {
            var execution = Find(CardEffectId.Execution);
            if (execution != null)
            {
                var target = FindEnemy(spawnId);
                if (target != null && !target.isBoss && target.hpFraction <= execution.Param("threshold", 0.12f))
                {
                    Impact.Burst(target.transform.position, new Color(1f, 0.42f, 0.29f), target.radius * 3f);
                    target.TakeDamage(target.hp + 1f);
                }
            }

            var echo = Find(CardEffectId.TemporalEcho);
            if (echo != null && Random.value < echo.Param("chance", 0.15f))
            {
                echoes.Add(new Echo
                {
                    spawnId = spawnId,
                    damage = ship.CannonDamageEstimate * Mathf.Max(0f, echo.Param("damageScale", 0.6f)),
                    time = Time.time + Mathf.Max(0.05f, echo.Param("delay", 0.3f))
                });
            }
        }

        private void OnArsenalHealed(float recovered)
        {
            if (Find(CardEffectId.VitalPulse) != null) vitalPool += recovered;
        }

        /// <summary>Pulso Vital: what was healed since the last pulse comes back out as damage.</summary>
        private void ReleaseVitalPulse()
        {
            var effect = Find(CardEffectId.VitalPulse);
            if (effect == null)
            {
                vitalPool = 0f;
                return;
            }
            var radius = Arena.ToWorld(effect.Param("radius", 130f));
            var damage = Mathf.Max(effect.Param("minDamage", 6f), vitalPool * effect.Param("damageScale", 2f));
            vitalPool = 0f;
            vitalTimer = Mathf.Max(0.2f, effect.Param("interval", 1f));
            Pulse.Spawn(Arena.ShipPosition, radius, new Color(VitalColor.r, VitalColor.g, VitalColor.b, 0.85f));
            world.DamageInRadius(Arena.ShipPosition, radius, damage);
        }

        /// <summary>Casco Espinhoso: an enemy touched the hull (hit or blocked).</summary>
        public void OnHullContact()
        {
            var effect = Find(CardEffectId.Thorns);
            if (effect == null || ship == null || world == null) return;
            var radius = Arena.ToWorld(effect.Param("radius", 110f));
            Pulse.Spawn(Arena.ShipPosition, radius, new Color(ThornColor.r, ThornColor.g, ThornColor.b, 0.9f), 0.3f);
            world.DamageInRadius(Arena.ShipPosition, radius, effect.Param("damage", 20f));
        }

        /// <summary>Escudo Regenerativo: a shield charge just stopped an enemy.</summary>
        public void OnShieldBlocked()
        {
            var effect = Find(CardEffectId.ShieldMend);
            if (effect == null || ship == null) return;
            ship.Heal(ship.maxHp * Mathf.Clamp01(effect.Param("heal", 0.03f)));
        }

        /// <summary>Sobrecura: healing that did not fit in the hull turns into shield blocks.</summary>
        public void OnOverheal(float excess)
        {
            var effect = Find(CardEffectId.Overheal);
            if (effect == null || ship == null || excess <= 0f) return;
            overhealPool += excess;
            var threshold = Mathf.Max(1f, ship.maxHp * Mathf.Max(0.01f, effect.Param("threshold", 0.1f)));
            var cap = ShieldCardCharges() + Mathf.Max(1, effect.ParamInt("maxBlocks", 1));
            while (overhealPool >= threshold)
            {
                if (ship.shieldCharges >= cap)
                {
                    overhealPool = 0f;
                    break;
                }
                overhealPool -= threshold;
                ship.AddShieldCharges(1, float.PositiveInfinity);
                Impact.Ring(Arena.ShipPosition, VitalColor, ship.radius, ship.radius * 4f, 0.35f);
            }
        }

        /// <summary>Protocolo de Emergência: the hull just dropped; heal once per rearm window.</summary>
        public void OnHullDamaged()
        {
            var effect = Find(CardEffectId.EmergencyProtocol);
            if (effect == null || ship == null || !ship.isAlive || protocolRearm > 0f) return;
            if (ship.hpFraction >= effect.Param("threshold", 0.3f)) return;
            protocolRearm = Mathf.Max(1f, effect.Param("rearm", 40f));
            Pulse.Spawn(Arena.ShipPosition, 1.6f, new Color(0.5f, 1f, 0.65f, 0.9f));
            ship.Heal(ship.maxHp * Mathf.Clamp01(effect.Param("heal", 0.2f)));
        }

        /// <summary>The ship's special just started.</summary>
        public void OnSpecialActivated(ShipSpecialKind kind)
        {
            if (ship == null || world == null) return;
            if (kind == ShipSpecialKind.ChristmasStar) ActivateChristmasStar();
            if (kind == ShipSpecialKind.Overdrive) overdriveExtended = 0f;

            if (kind == ShipSpecialKind.Amplify)
            {
                var resonance = Find(CardEffectId.AmplifyShockwave);
                if (resonance != null)
                {
                    var stun = resonance.Param("stun");
                    if (stun > 0f)
                    {
                        world.InRadius(Arena.ShipPosition, Arena.DetectionRadius, arsenalBuffer);
                        for (var i = 0; i < arsenalBuffer.Count; i++)
                            if (arsenalBuffer[i] != null && arsenalBuffer[i].isAlive) arsenalBuffer[i].Paralyze(stun);
                        arsenalBuffer.Clear();
                    }
                    Shockwave.Spawn(Arena.ShipPosition, Arena.DetectionRadius, resonance.Param("damage", 50f), world);
                }
            }

            if (kind == ShipSpecialKind.Aegis)
            {
                var aegis = Find(CardEffectId.SpecialDuration);
                var push = aegis != null ? aegis.Param("push") : 0f;
                if (push > 0f) PushAway(Arena.ToWorld(200f), push);
            }
        }

        /// <summary>Reviver: survives lethal damage, then recharges in combat time.</summary>
        private void TickPhoenix(float dt, bool fighting)
        {
            if (fighting && dt > 0f) phoenixCooldown = Mathf.Max(0f, phoenixCooldown - dt);
        }

        public bool TryPhoenix(out float hullFraction)
        {
            hullFraction = 0f;
            var effect = Find(CardEffectId.PhoenixCore);
            if (effect == null || phoenixCooldown > 0f || ship == null || ship.isAlive) return false;
            phoenixCooldown = Mathf.Max(1f, effect.Param("rearm", 60f));
            hullFraction = Mathf.Clamp(effect.Param("reviveHp", 0.3f), 0.05f, 1f);

            // Restore before the blast can kill enemies and trigger wave/XP callbacks.
            ship.RestoreFromPhoenix(hullFraction);

            var push = effect.Param("push");
            if (push > 0f) PushAway(Arena.ToWorld(220f), push);
            var blast = effect.Param("blastDamage");
            if (blast > 0f && world != null) world.DamageInRadius(Arena.ShipPosition, Arena.ToWorld(220f), blast);

            var gold = new Color(1f, 0.72f, 0.3f);
            Impact.Ring(Arena.ShipPosition, gold, 0.5f, 8f, 0.7f);
            Impact.Burst(Arena.ShipPosition, gold, 3f);
            Juice.ScreenFlash(new Color(1f, 0.8f, 0.45f, 0.45f), 0.6f);
            Juice.Shake(0.35f, 0.5f);
            Sfx.Play(Sound.Heal);
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void PushAway(float radius, float strength)
        {
            world.InRadius(Arena.ShipPosition, radius, arsenalBuffer);
            for (var i = 0; i < arsenalBuffer.Count; i++)
            {
                var enemy = arsenalBuffer[i];
                if (enemy == null || !enemy.isAlive) continue;
                var away = (Vector2)enemy.transform.position - Arena.ShipPosition;
                if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;
                enemy.Push(away.normalized * strength);
            }
            arsenalBuffer.Clear();
            Pulse.Spawn(Arena.ShipPosition, radius, new Color(0.5f, 0.85f, 1f, 0.9f));
        }

        private Enemy FindEnemy(int spawnId)
        {
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].isAlive && enemies[i].spawnId == spawnId) return enemies[i];
            return null;
        }

        /// <summary>The enemy in range with the most hit points (unmarked first, when asked).</summary>
        private Enemy Toughest(bool preferUnmarked)
        {
            Enemy best = null;
            var bestScore = float.MinValue;
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive || !Arena.InRange(enemy.transform.position, enemy.radius)) continue;
                var score = enemy.hp - (preferUnmarked && enemy.IsMarked ? 100000f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>The in-range enemy with the most neighbours inside <paramref name="radius"/>.</summary>
        private Vector2? DensestCluster(float radius)
        {
            var enemies = world.Enemies;
            var best = -1;
            Vector2 bestPoint = Vector2.zero;
            var sqr = radius * radius;
            for (var i = 0; i < enemies.Count; i++)
            {
                var a = enemies[i];
                if (a == null || !a.isAlive || !Arena.InRange(a.transform.position, a.radius)) continue;
                var pa = (Vector2)a.transform.position;
                var count = 0;
                for (var j = 0; j < enemies.Count; j++)
                {
                    var b = enemies[j];
                    if (b != null && b.isAlive && ((Vector2)b.transform.position - pa).sqrMagnitude <= sqr) count++;
                }
                if (count > best)
                {
                    best = count;
                    bestPoint = pa;
                }
            }
            return best < 0 ? (Vector2?)null : bestPoint;
        }
    }
}
