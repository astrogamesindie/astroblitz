// AstroCrawler - each ship's signature: who it aims at, how its shot behaves
// and the passive that ties the shot to its identity. Always on, never a card.
//
//   Vanguarda-7    nearest       Calor de foco: hits on the same target heat the shot (+4%/hit, max +40%);
//                                Sobrecarga adds two side shots.
//   Bastião Aegis  nearest       Canhão de impacto: shots shove enemies back; 3 orbiting plates fire at
//                                anything close, charged by damage taken into shockwaves; Égide reflects.
//   Espectro Nulo  spread        Tiro de fase: pierces 1, slows 15%, a ghost echo repeats it at 40%;
//                                slowed enemies always take a critical; Estase parks the bullets.
//   Aurora Médica  weakest       Sutura: homing shots, every 4th links and drains a second enemy;
//                                kills drop healing motes, overheal becomes a bubble; Reparo seeds.
//   Orion          strongest     Artilharia de cerco: every 5th shot is a charged shell with an area
//                                blast; damage past a kill jumps on; Amplificação charges every shot.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public partial class PlayerShip
    {
        private enum Signature { None, Heat, Plates, Phase, Suture, Siege }

        private struct Volley
        {
            public float damageScale;
            public float scale;
            public Color tint;
            public bool tinted;
            public ShotTrait traits;
            public bool charged;
            public bool pierce;
            public int pierceLimit;
            public bool tether;
        }

        private struct Echo
        {
            public float at;
            public Vector2 position, direction;
            public float damage;
        }

        private sealed class Mote
        {
            public SpriteRenderer body;
            public Vector2 velocity;
            public float age;
            public float heal;
        }

        private static readonly Color HeatCold = new Color(1f, 0.55f, 0.2f);
        private static readonly Color HeatHot = new Color(1f, 0.96f, 0.82f);
        private static readonly Color PlateColor = new Color(0.36f, 0.84f, 0.63f);
        private static readonly Color PhaseColor = new Color(0.72f, 0.48f, 1f);
        private static readonly Color SutureColor = new Color(1f, 0.53f, 0.78f);
        private static readonly Color SiegeColor = new Color(1f, 0.78f, 0.34f);

        private const int HeatMaxStacks = 10;
        private const float HeatPerStack = 0.04f;
        private const int PlateCount = 3;
        private const float PlateOrbit = 1.05f;
        private const float PlateRange = 2.6f;
        private const float KnockbackZone = 3.5f;
        private const float PlateCooldown = 1.3f;
        private const float EchoDelay = 0.5f;
        private const float EchoDamage = 0.4f;
        private const int TetherEvery = 4;
        private const float MoteHeal = 0.006f;
        private const int MaxMotes = 24;
        private const float BubbleCap = 0.2f;
        private const int ChargeEvery = 5;
        private const float ChargedBlastShare = 0.8f;
        private const float ChargedBlastRadius = 1.3f;

        private Signature signature;
        private float signatureClock;

        private int heatTargetId, heatStacks;
        private SpriteRenderer[] plates;
        private float[] plateCooldowns;
        private float plateAngle;
        private int plateCharges;
        private readonly Dictionary<int, float> lastAimed = new Dictionary<int, float>();
        private readonly List<Echo> echoes = new List<Echo>();
        private int sutureCount;
        private float seedTime;
        private readonly HashSet<int> seeded = new HashSet<int>();
        private float bubble;
        private SpriteRenderer bubbleRing;
        private readonly List<Mote> motes = new List<Mote>();
        private readonly Stack<Mote> freeMotes = new Stack<Mote>();
        private Transform moteRoot;
        private int siegeCount;
        private SpriteRenderer chargeGlow;

        private static Signature SignatureFor(string shipId)
        {
            switch (shipId)
            {
                case "vanguard": return Signature.Heat;
                case "bastion": return Signature.Plates;
                case "spectre": return Signature.Phase;
                case "aurora": return Signature.Suture;
                case "orion": return Signature.Siege;
                default: return Signature.None;
            }
        }

        // ------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------

        private void SetupSignature()
        {
            signature = SignatureFor(definition != null ? definition.id : "");
            signatureClock = 0f;
            heatTargetId = heatStacks = 0;
            plateCharges = 0;
            plateAngle = 0f;
            lastAimed.Clear();
            echoes.Clear();
            sutureCount = 0;
            seedTime = 0f;
            seeded.Clear();
            bubble = 0f;
            siegeCount = 0;
            ClearMotes();

            BuildPlates(signature == Signature.Plates);
            if (bubbleRing == null)
            {
                bubbleRing = SpriteFactory.NewRenderer("Bubble", ArtLibrary.RingSprite, SutureColor, 21, transform);
                bubbleRing.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(ArtLibrary.RingSprite, radius * 3.4f);
            }
            bubbleRing.enabled = false;
            if (chargeGlow == null)
            {
                chargeGlow = SpriteFactory.NewRenderer("Charge", ArtLibrary.Glow, SiegeColor, 22, transform);
            }
            chargeGlow.enabled = false;

            if (world != null)
            {
                world.onEnemyKilled -= OnSignatureKill;
                world.onEnemyKilled += OnSignatureKill;
            }
        }

        private void BuildPlates(bool active)
        {
            if (plates == null)
            {
                if (!active) return;
                plates = new SpriteRenderer[PlateCount];
                plateCooldowns = new float[PlateCount];
                var sprite = SpriteFactory.Polygon(6);
                for (var i = 0; i < PlateCount; i++)
                {
                    plates[i] = SpriteFactory.NewRenderer("Plate" + i, sprite, PlateColor, 19, transform);
                    plates[i].transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(sprite, 0.42f);
                    // A soft green halo so the plates read as live armour, not debris.
                    var plateHalo = SpriteFactory.NewRenderer("Halo", ArtLibrary.Glow,
                        new Color(PlateColor.r, PlateColor.g, PlateColor.b, 0.45f), 18, plates[i].transform);
                    plateHalo.transform.localScale = Vector3.one * (ArtLibrary.ScaleToDiameter(ArtLibrary.Glow, 1.0f) /
                        Mathf.Max(0.0001f, ArtLibrary.ScaleToDiameter(sprite, 0.42f)));
                }
            }
            for (var i = 0; i < PlateCount; i++)
            {
                plates[i].enabled = active;
                foreach (var child in plates[i].GetComponentsInChildren<SpriteRenderer>(true)) child.enabled = active;
                plateCooldowns[i] = i * PlateCooldown / PlateCount;
            }
        }

        private void OnDestroy()
        {
            if (world != null) world.onEnemyKilled -= OnSignatureKill;
            if (Active == this) Active = null;
            if (moteRoot != null) Destroy(moteRoot.gameObject);
        }

        // ------------------------------------------------------------------
        // Aim
        // ------------------------------------------------------------------

        private Enemy SelectTarget()
        {
            if (signature != Signature.Phase && signature != Signature.Suture && signature != Signature.Siege)
                return world.NearestInRange(Arena.ShipPosition, skipDoomed: true, preferNonBoss: true);

            Enemy best = null, doomed = null;
            var bestScore = float.MaxValue;
            var doomedScore = float.MaxValue;
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive || !Arena.InRange(enemy.transform.position, enemy.radius)) continue;
                var distance = ((Vector2)enemy.transform.position - Arena.ShipPosition).magnitude;
                float score;
                switch (signature)
                {
                    case Signature.Phase:
                        // Whoever went longest without a bullet: the slow spreads over the whole group.
                        float last;
                        score = (lastAimed.TryGetValue(enemy.spawnId, out last) ? last : -1000f) * 100f + distance;
                        break;
                    case Signature.Suture:
                        // The weakest first: finished enemies become healing motes.
                        score = (enemy.isBoss ? 1e6f : 0f) + enemy.hp + distance * 0.01f;
                        break;
                    default:
                        // Orion: the biggest threat first, bosses above all.
                        score = (enemy.isBoss ? -1e6f : 0f) - enemy.hp + distance * 0.01f;
                        break;
                }
                if (enemy.isDoomed)
                {
                    if (score < doomedScore) { doomedScore = score; doomed = enemy; }
                }
                else if (score < bestScore) { bestScore = score; best = enemy; }
            }
            var target = best != null ? best : doomed;
            if (target != null && signature == Signature.Phase)
            {
                if (lastAimed.Count > 256) lastAimed.Clear();
                lastAimed[target.spawnId] = signatureClock;
            }
            return target;
        }

        // ------------------------------------------------------------------
        // Shot
        // ------------------------------------------------------------------

        private Volley BeginVolley(Enemy target)
        {
            var volley = new Volley { damageScale = 1f, scale = 1f };
            switch (signature)
            {
                case Signature.Heat:
                    var id = target != null ? target.spawnId : 0;
                    if (id != 0 && id == heatTargetId) heatStacks = Mathf.Min(HeatMaxStacks, heatStacks + 1);
                    else heatStacks = 0;
                    heatTargetId = id;
                    var heat = heatStacks / (float)HeatMaxStacks;
                    volley.damageScale = 1f + HeatPerStack * heatStacks;
                    volley.tint = Color.Lerp(HeatCold, HeatHot, heat);
                    volley.tinted = true;
                    volley.scale = 1f + 0.3f * heat;
                    if (heatStacks == HeatMaxStacks)
                        Impact.Spark(Arena.ShipPosition + (Vector2)hull.transform.up * radius * 1.2f, HeatCold, 0.8f);
                    break;
                case Signature.Plates:
                    volley.traits = ShotTrait.Knockback;
                    volley.tint = PlateColor;
                    volley.tinted = true;
                    volley.scale = 1.25f;
                    break;
                case Signature.Phase:
                    volley.traits = ShotTrait.PhaseSlow | (SpecialIs(ShipSpecialKind.Stasis) ? ShotTrait.StasisHang : 0);
                    volley.tint = PhaseColor;
                    volley.tinted = true;
                    volley.pierceLimit = 2;
                    break;
                case Signature.Suture:
                    sutureCount++;
                    volley.tether = sutureCount % TetherEvery == 0;
                    volley.traits = ShotTrait.Homing | (seedTime > 0f ? ShotTrait.Seed : 0);
                    volley.tint = volley.tether ? Color.Lerp(SutureColor, Color.white, 0.35f) : SutureColor;
                    volley.tinted = true;
                    volley.scale = volley.tether ? 1.35f : 1f;
                    break;
                case Signature.Siege:
                    var amplified = SpecialIs(ShipSpecialKind.Amplify);
                    siegeCount++;
                    volley.charged = amplified || siegeCount >= ChargeEvery;
                    if (siegeCount >= ChargeEvery) siegeCount = 0;
                    volley.pierce = amplified;
                    volley.traits = ShotTrait.Overkill;
                    volley.tint = SiegeColor;
                    volley.tinted = true;
                    volley.scale = volley.charged ? 1.7f : 1.15f;
                    break;
            }
            return volley;
        }

        private void StyleShot(Projectile projectile, Volley volley, bool firstLane, bool sieged, bool modsPierce)
        {
            if (projectile == null || volley.traits == ShotTrait.None && !volley.tinted) return;
            var traits = volley.traits;
            if (volley.tether && firstLane) traits |= ShotTrait.Tether;
            var pierceLimit = modsPierce || volley.pierce ? 0 : volley.pierceLimit;
            if (sieged)
            {
                // A card's siege charge keeps its own orange round.
                projectile.Style(traits, null, 1f, pierceLimit);
                return;
            }
            // Shape, colour, size and trail come from the ship's ShotLook.
            projectile.Style(traits, null, 1f, pierceLimit).Look(LookFor(volley, firstLane));
        }

        private void QueueEcho(Vector2 position, Vector2 direction, float damage)
        {
            if (signature != Signature.Phase) return;
            echoes.Add(new Echo { at = signatureClock + EchoDelay, position = position, direction = direction,
                damage = damage * EchoDamage });
        }

        private void FireSignatureExtras(Vector2 muzzle, Vector2 direction, CombatModifiers mods)
        {
            // Vanguarda in Sobrecarga: two heated side shots at half damage.
            if (signature != Signature.Heat || !SpecialIs(ShipSpecialKind.Overdrive)) return;
            var aim = Mathf.Atan2(direction.y, direction.x);
            var heat = heatStacks / (float)HeatMaxStacks;
            for (var s = -1; s <= 1; s += 2)
            {
                var angle = aim + 12f * Mathf.Deg2Rad * s;
                var fan = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Projectile.Spawn(muzzle, fan, RollDamage(mods, null) * 0.5f * (1f + HeatPerStack * heatStacks),
                    mods.pierce, world).Style(ShotTrait.NoProc).Look(FanLook(Color.Lerp(HeatCold, HeatHot, heat)));
            }
        }

        private void ChargedRecoil(Vector2 direction)
        {
            var muzzle = Arena.ShipPosition + direction * (radius * 1.2f);
            Impact.Ring(muzzle, SiegeColor, 0.2f, 1.3f, 0.25f);
            Impact.Spark(muzzle, SiegeColor, 1.1f);
            Juice.Shake(0.06f, 0.12f);
            var kick = -(Vector3)direction * 0.22f;
            LeanTween.cancel(hull.gameObject, false, TweenAction.MOVE_LOCAL);
            hull.transform.localPosition = kick;
            LeanTween.moveLocal(hull.gameObject, Vector3.zero, 0.2f).setEase(LeanTweenType.easeOutCubic);
        }

        /// <summary>
        /// Espectro Nulo: a critical on anything held by its time control - a
        /// card slow of 20% or more, paralysis or the Estase. The 15% slow of
        /// its own phase shot does not count, or nearly every shot would crit.
        /// </summary>
        private bool SignatureCrit(Enemy target) => signature == Signature.Phase && target != null &&
            (world.specialStasis || target.SpeedFactor <= 0.8f);

        // ------------------------------------------------------------------
        // What a styled bullet does on contact (called by Projectile.Hit)
        // ------------------------------------------------------------------

        public static void ApplyShotTraits(ShotTrait traits, Enemy enemy, bool killed, Vector2 position, int spawnId,
            float damage, float hpBefore, Vector2 direction, CombatWorld world)
        {
            var ship = Active;
            var alive = !killed && enemy != null && enemy.isAlive && enemy.spawnId == spawnId;
            // Only inside the "safe zone": shoving far enemies out of range would stall the wave.
            if ((traits & ShotTrait.Knockback) != 0 && alive &&
                ((Vector2)enemy.transform.position - Arena.ShipPosition).magnitude < KnockbackZone)
                enemy.Push(direction * (enemy.isBoss ? 0.04f : 0.3f));
            if ((traits & ShotTrait.PhaseSlow) != 0 && alive)
                enemy.ApplySlow(0.85f, 1.6f);
            if ((traits & ShotTrait.Seed) != 0 && alive && ship != null)
            {
                ship.seeded.Add(spawnId);
                Impact.Ring(position, SutureColor, 0.2f, 0.8f, 0.25f);
            }
            if ((traits & ShotTrait.Overkill) != 0 && killed)
            {
                var overflow = damage - Mathf.Max(0f, hpBefore);
                var next = overflow > 1f ? NearestOther(world, position, 3.2f, spawnId) : null;
                if (next != null)
                {
                    Impact.Lightning(position, next.transform.position, SiegeColor);
                    next.TakeDamage(overflow);
                }
            }
            if ((traits & ShotTrait.Tether) != 0)
            {
                var other = NearestOther(world, position, 3f, spawnId);
                if (other != null)
                {
                    Impact.Lightning(position, other.transform.position, SutureColor);
                    other.TakeDamage(damage * 0.5f);
                    if (ship != null && ship.isAlive) ship.ApplyHealing(ship.maxHp * 0.004f, false);
                }
            }
        }

        private static Enemy NearestOther(CombatWorld world, Vector2 from, float range, int excludeId)
        {
            if (world == null) return null;
            Enemy best = null;
            var bestDistance = range * range;
            var enemies = world.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive || enemy.spawnId == excludeId) continue;
                var distance = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = enemy; }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // Damage, healing and specials
        // ------------------------------------------------------------------

        /// <summary>Égide of the Bastião: what it blocks bursts out as shards around the hull.</summary>
        private void AegisReflect(float amount, Vector2 towards)
        {
            if (signature != Signature.Plates || world == null) return;
            world.DamageInRadius(Arena.ShipPosition, 2.4f, amount * 1.5f);
            Impact.Debris(Arena.ShipPosition + towards.normalized * radius, PlateColor, radius * 4f, 10);
            Impact.Ring(Arena.ShipPosition, PlateColor, radius * 2f, 2.4f * 2f, 0.3f);
        }

        private float AbsorbWithBubble(float amount)
        {
            if (bubble <= 0f) return amount;
            var taken = Mathf.Min(bubble, amount);
            bubble -= taken;
            Impact.Ring(Arena.ShipPosition, SutureColor, radius * 3f, radius * 4.4f, 0.25f);
            Juice.Number(transform.position + Vector3.up * 0.8f, "-" + Mathf.CeilToInt(taken), SutureColor, 0.9f);
            return amount - taken;
        }

        private void AddBubble(float overflow)
        {
            if (signature != Signature.Suture) return;
            bubble = Mathf.Min(maxHp * BubbleCap, bubble + overflow);
        }

        private void OnSignatureDamaged()
        {
            if (signature == Signature.Plates) plateCharges = Mathf.Min(PlateCount, plateCharges + 1);
        }

        private void OnSignatureSpecial(ShipSpecialKind kind)
        {
            if (signature == Signature.Suture && kind == ShipSpecialKind.Repair)
            {
                seedTime = 4f;
                Impact.Ring(Arena.ShipPosition, SutureColor, radius, radius * 8f, 0.5f);
            }
        }

        private void OnSignatureKill(Enemy enemy, bool byPlayerDamage)
        {
            if (signature != Signature.Suture || !byPlayerDamage || !isAlive || enemy == null) return;
            var count = seeded.Remove(enemy.spawnId) ? 3 : 1;
            for (var i = 0; i < count; i++) SpawnMote(enemy.transform.position, maxHp * MoteHeal);
        }

        // ------------------------------------------------------------------
        // Per frame
        // ------------------------------------------------------------------

        private void TickSignature(float dt)
        {
            if (dt <= 0f) return;
            signatureClock += dt;
            seedTime = Mathf.Max(0f, seedTime - dt);

            TickEchoes();
            TickPlates(dt);
            TickMotes(dt);

            if (bubbleRing != null)
            {
                var fill = maxHp > 0f ? bubble / (maxHp * BubbleCap) : 0f;
                bubbleRing.enabled = fill > 0.01f;
                if (bubbleRing.enabled)
                    bubbleRing.color = new Color(SutureColor.r, SutureColor.g, SutureColor.b,
                        0.25f + 0.55f * fill + 0.1f * Mathf.Sin(signatureClock * 4f));
            }

            if (chargeGlow != null)
            {
                // Orion's barrel fills with light as the charged shell comes up.
                var charging = signature == Signature.Siege && hull != null;
                var amount = charging ? siegeCount / (float)(ChargeEvery - 1) : 0f;
                if (SpecialIs(ShipSpecialKind.Amplify)) amount = 1f;
                chargeGlow.enabled = charging && amount > 0.05f;
                if (chargeGlow.enabled)
                {
                    chargeGlow.transform.position = Arena.ShipPosition + (Vector2)hull.transform.up * radius * 1.15f;
                    var size = 0.25f + 0.55f * Mathf.Clamp01(amount) + 0.05f * Mathf.Sin(signatureClock * 18f);
                    chargeGlow.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(chargeGlow.sprite, size);
                    chargeGlow.color = new Color(SiegeColor.r, SiegeColor.g, SiegeColor.b, 0.35f + 0.5f * amount);
                }
            }
        }

        private void TickEchoes()
        {
            for (var i = echoes.Count - 1; i >= 0; i--)
            {
                var echo = echoes[i];
                if (echo.at > signatureClock) continue;
                echoes.RemoveAt(i);
                var traits = ShotTrait.PhaseSlow | ShotTrait.NoProc |
                    (SpecialIs(ShipSpecialKind.Stasis) ? ShotTrait.StasisHang : 0);
                Projectile.Spawn(echo.position, echo.direction, echo.damage, false, world)
                    .Style(traits, null, 1f, 2).Look(EchoLook());
            }
        }

        private void TickPlates(float dt)
        {
            if (signature != Signature.Plates || plates == null) return;
            plateAngle += 75f * dt;
            for (var i = 0; i < PlateCount; i++)
            {
                var plate = plates[i];
                var angle = (plateAngle + i * 360f / PlateCount) * Mathf.Deg2Rad;
                plate.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * PlateOrbit;
                plate.transform.localRotation = Quaternion.Euler(0f, 0f, plateAngle * 2f);
                var charged = i < plateCharges;
                plate.color = charged ? Color.Lerp(PlateColor, Color.white, 0.55f + 0.2f * Mathf.Sin(signatureClock * 10f))
                    : PlateColor;

                plateCooldowns[i] -= dt;
                if (plateCooldowns[i] > 0f) continue;
                var from = (Vector2)plate.transform.position;
                var target = NearestOther(world, Arena.ShipPosition, PlateRange, 0);
                if (target == null) { plateCooldowns[i] = 0.15f; continue; }
                plateCooldowns[i] = PlateCooldown;
                var to = (Vector2)target.transform.position;
                if (plateCharges > 0)
                {
                    plateCharges--;
                    Impact.Lightning(from, to, PlateColor);
                    Shockwave.Spawn(to, 1.3f, CannonDamageEstimate * 1.6f, world);
                }
                else
                {
                    Projectile.Spawn(from, (to - from).normalized, CannonDamageEstimate * 0.5f, false, world, target)
                        .Style(ShotTrait.Knockback | ShotTrait.NoProc).Look(PlateBoltLook());
                }
                Impact.Spark(from, PlateColor, 0.5f);
                Juice.Punch(plate.transform, 0.3f, 0.2f);
            }
        }

        private void SpawnMote(Vector2 position, float heal)
        {
            if (motes.Count >= MaxMotes)
            {
                ApplyHealing(heal, false);
                return;
            }
            if (moteRoot == null)
            {
                moteRoot = new GameObject("SignatureMotes").transform;
                if (world != null) moteRoot.SetParent(world.transform, false);
            }
            var mote = freeMotes.Count > 0 ? freeMotes.Pop() : null;
            if (mote == null || mote.body == null)
            {
                mote = new Mote();
                mote.body = SpriteFactory.NewRenderer("Mote", ArtLibrary.SoftCircle, SutureColor, 13, moteRoot);
                mote.body.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(ArtLibrary.SoftCircle, 0.26f);
            }
            mote.body.gameObject.SetActive(true);
            mote.body.transform.position = position;
            var angle = Random.Range(0f, Mathf.PI * 2f);
            mote.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.5f, 2.5f);
            mote.age = 0f;
            mote.heal = heal;
            motes.Add(mote);
        }

        private void TickMotes(float dt)
        {
            for (var i = motes.Count - 1; i >= 0; i--)
            {
                var mote = motes[i];
                if (mote.body == null) { motes.RemoveAt(i); continue; }
                mote.age += dt;
                var position = (Vector2)mote.body.transform.position;
                if (mote.age < 0.25f)
                {
                    mote.body.transform.position = position + mote.velocity * dt;
                    mote.velocity *= Mathf.Exp(-5f * dt);
                    continue;
                }
                var toShip = Arena.ShipPosition - position;
                var distance = toShip.magnitude;
                var step = (4f + 10f * mote.age) * dt;
                if (distance <= radius + step)
                {
                    motes.RemoveAt(i);
                    mote.body.gameObject.SetActive(false);
                    freeMotes.Push(mote);
                    ApplyHealing(mote.heal, false);
                    Impact.Spark(Arena.ShipPosition + toShip.normalized * -radius, SutureColor, 0.45f);
                    continue;
                }
                mote.body.transform.position = position + toShip / distance * step;
            }
        }

        private void ClearMotes()
        {
            for (var i = 0; i < motes.Count; i++)
            {
                if (motes[i].body == null) continue;
                motes[i].body.gameObject.SetActive(false);
                freeMotes.Push(motes[i]);
            }
            motes.Clear();
        }
    }
}
