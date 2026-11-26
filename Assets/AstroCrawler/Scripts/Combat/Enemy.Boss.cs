// AstroCrawler - the Aberrant Core's fight, its orbs and the cocoons it lays.
//
// The core fights in a cycle:
//   1. Hold: it parks at its station, inside the cannon's range, and sways in a
//      slow figure-eight. This is the window to damage it.
//   2. Retreat: it backs away to a point out of range (but on screen).
//   3. Charge: it swells and flashes for a moment, then fires an energy orb
//      straight at the ship (two in quick succession from stage 6 on).
//   4. Return to the station, and again.
// The orb (EnemyKind.BossOrb) is a slow, tough enemy: the cannon and the cards
// target it like any other, so the ship gets the long flight to shoot it down,
// and the player time to answer with the special (Aegis blocks it, Stasis
// freezes it, Overdrive brings it down faster). Hitting the hull, it deals a
// heavy blow; a shield charge stops it.
// Meanwhile the core keeps calling reinforcements (half warp out of the core),
// throws cocoons that hatch scouts unless destroyed, and at half health splits
// into two smaller cores - 60% of the health left each - that cycle out of
// step. The wave ends when both are destroyed.

using UnityEngine;

namespace AstroCrawler
{
    public partial class Enemy
    {
        private enum BossStep { Enter, Hold, Retreat, Charge, Return }

        private const float SwayX = 2.3f;
        private const float SwayY = 0.65f;
        private const float SwayPeriod = 7.5f;
        private const float HoldTime = 6.5f;
        private const float ChargeTime = 1.1f;
        private const float FarDistance = 10.5f;
        private const float MoveSpeed = 3.2f;
        private const float CocoonInterval = 9f;
        private const float SplitAt = 0.5f;
        private const float TwinShare = 0.6f;
        private const float TwinScale = 0.72f;
        private const float HatchTime = 6.5f;
        private const int HatchScouts = 3;

        private static readonly Color ChargeColor = new Color(1f, 0.35f, 0.9f);

        private BossStep bossStep;
        private float stepTimer;
        private float bossClock;
        private float cocoonTimer;
        private bool hasSplit;
        private int mirror = 1;
        private Vector2 stationOffset;
        private Vector2 farPoint;
        private float partScale = 1f;
        private int orbsLeft;

        private Vector2 cocoonFrom;
        private Vector2 cocoonTarget;
        private float cocoonFlight;
        private float hatchTimer;

        /// <summary>One of the two halves the core becomes at half health.</summary>
        public bool IsTwin { get; private set; }

        private bool bossArrived { get { return bossStep != BossStep.Enter; } }

        private void ResetBossState()
        {
            bossStep = BossStep.Enter;
            stepTimer = 0f;
            bossClock = 0f;
            cocoonTimer = CocoonInterval * 0.6f;
            hasSplit = false;
            mirror = 1;
            stationOffset = Vector2.zero;
            partScale = 1f;
            IsTwin = false;
            orbsLeft = 0;
            cocoonFlight = -1f;
            hatchTimer = HatchTime;
        }

        // ------------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------------

        private void MoveBoss(float dt, float speed)
        {
            var position = (Vector2)transform.position;
            var station = Arena.BossStation + stationOffset;
            switch (bossStep)
            {
                case BossStep.Enter:
                    // The entrance from below the screen is brisk (it used to take ~15 s on a tall phone).
                    if (Glide(position, station, Mathf.Max(speed, 2.6f * slowFactor), dt)) StartHold();
                    break;

                case BossStep.Hold:
                    bossClock += dt * slowFactor;
                    var phase = bossClock * Mathf.PI * 2f / SwayPeriod;
                    var target = station + new Vector2(Mathf.Sin(phase) * SwayX * mirror * (IsTwin ? 0.55f : 1f),
                        Mathf.Sin(phase * 2f) * SwayY);
                    var limit = Mathf.Max(0.5f, Arena.ViewHalfSize.x - radius - 0.4f);
                    target.x = Mathf.Clamp(target.x, -limit, limit);
                    transform.position = Vector2.MoveTowards(position, target, Mathf.Max(speed, 1.2f) * 1.6f * dt);
                    break;

                case BossStep.Retreat:
                    if (Glide(position, farPoint, MoveSpeed * slowFactor, dt))
                    {
                        bossStep = BossStep.Charge;
                        stepTimer = ChargeTime;
                        orbsLeft = world.stageLevel >= 5 ? 2 : 1;
                        Juice.FlashSprite(body, Color.white, baseColor, ChargeTime);
                        Impact.Ring(position, ChargeColor, radius * 3.2f, radius, ChargeTime);
                    }
                    break;

                case BossStep.Charge:
                    // Swells while charging; the shot leaves at the peak.
                    var swell = 1f + 0.18f * Mathf.Clamp01(1f - stepTimer / ChargeTime);
                    body.transform.localScale = Vector3.one * bodyScale * swell;
                    break;

                case BossStep.Return:
                    if (Glide(position, station, MoveSpeed * slowFactor, dt)) StartHold();
                    break;
            }
            transform.Rotate(0f, 0f, (bossStep == BossStep.Charge ? 70f : 14f) * dt, Space.Self);
        }

        /// <summary>Moves towards a point; true once there.</summary>
        private bool Glide(Vector2 position, Vector2 target, float speed, float dt)
        {
            var to = target - position;
            var distance = to.magnitude;
            if (distance <= 0.12f) return true;
            transform.position = position + to / distance * Mathf.Min(speed * dt, distance);
            return false;
        }

        private void StartHold()
        {
            bossStep = BossStep.Hold;
            // Twins hold out of step, so one is usually in range while the other shoots.
            stepTimer = HoldTime * (IsTwin ? (mirror > 0 ? 0.6f : 1.1f) : 1f);
        }

        /// <summary>
        /// Out of the cannon's range but on screen: a point up to FarDistance
        /// from the ship, as close as possible to where the core already is.
        /// </summary>
        private Vector2 PickFarPoint()
        {
            var view = Arena.ViewHalfSize;
            var inset = radius + 0.8f;
            var from = (Vector2)transform.position - Arena.ShipPosition;
            var baseAngle = Mathf.Atan2(from.y, from.x);
            var best = Arena.ShipPosition + from.normalized * FarDistance;
            var bestScore = float.MaxValue;
            for (var i = 0; i < 24; i++)
            {
                var angle = baseAngle + (i % 2 == 0 ? 1f : -1f) * (i / 2) * Mathf.PI / 12f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // As far as possible up to FarDistance without leaving the view
                // (and below the HUD at the top).
                var reach = FarDistance;
                if (Mathf.Abs(dir.x) > 0.001f) reach = Mathf.Min(reach, (view.x - inset) / Mathf.Abs(dir.x));
                if (Mathf.Abs(dir.y) > 0.001f)
                    reach = Mathf.Min(reach, (view.y - inset - (dir.y > 0f ? 2.2f : 0f)) / Mathf.Abs(dir.y));
                var point = Arena.ShipPosition + dir * reach;
                // Prefer points clearly out of range, then the shortest trip.
                var score = (reach < Arena.DetectionRadius + 1.5f ? 100f : 0f) + (point - (Vector2)transform.position).magnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = point;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // The fight
        // ------------------------------------------------------------------

        private void TickBoss(float dt)
        {
            if (paralyzed) return;

            if (definition.summonInterval > 0f && definition.summonCount > 0)
            {
                summonTimer -= dt;
                if (summonTimer <= 0f)
                {
                    summonTimer = definition.summonInterval * (IsTwin ? 1.6f : 1f);
                    Summon(IsTwin ? Mathf.Max(1, definition.summonCount - 1) : definition.summonCount);
                }
            }

            if (!hasSplit && !IsTwin && hpFraction <= SplitAt)
            {
                Split();
                return;
            }

            if (!bossArrived) return;

            switch (bossStep)
            {
                case BossStep.Hold:
                    stepTimer -= dt * slowFactor;
                    if (stepTimer <= 0f)
                    {
                        farPoint = PickFarPoint();
                        bossStep = BossStep.Retreat;
                    }
                    break;

                case BossStep.Charge:
                    stepTimer -= dt * slowFactor;
                    if (stepTimer <= 0f)
                    {
                        FireOrb();
                        orbsLeft--;
                        if (orbsLeft > 0)
                        {
                            stepTimer = 0.55f;
                        }
                        else
                        {
                            body.transform.localScale = Vector3.one * bodyScale;
                            bossStep = BossStep.Return;
                        }
                    }
                    break;
            }

            if (bossStep == BossStep.Hold)
            {
                cocoonTimer -= dt * slowFactor;
                if (cocoonTimer <= 0f)
                {
                    cocoonTimer = CocoonInterval * (IsTwin ? 1.5f : 1f);
                    LayCocoons(IsTwin ? 1 : 2);
                }
            }
        }

        /// <summary>An energy orb straight at the ship: slow, tough, heavy if it lands.</summary>
        private void FireOrb()
        {
            var position = (Vector2)transform.position;
            var toShip = (Arena.ShipPosition - position).normalized;
            var orb = world.Spawn(EnemyKind.BossOrb, position + toShip * (radius + 0.3f));
            Impact.Burst(position + toShip * radius, ChargeColor, radius * 1.6f);
            Juice.Shake(0.12f, 0.2f);
            LeanTween.cancel(body.gameObject, false, TweenAction.SCALE);
            body.transform.localScale = Vector3.one * (bodyScale * 0.86f);
            LeanTween.scale(body.gameObject, Vector3.one * bodyScale, 0.3f).setEase(LeanTweenType.easeOutBack);
            if (orb != null) Sfx.Play(Sound.MissileLaunch);
        }

        /// <summary>Reinforcements: half warp out of the core, half arrive from the edge as before.</summary>
        private void Summon(int count)
        {
            var position = (Vector2)transform.position;
            Impact.Ring(position, new Color(1f, 0.3f, 0.35f), radius, radius * 3f, 0.4f);
            var fromCore = count / 2;
            for (var i = 0; i < fromCore; i++)
            {
                var dir = Rotate(Vector2.up, Random.Range(-70f, 70f));
                var kind = Random.value < 0.6f ? EnemyKind.Scout : EnemyKind.Interceptor;
                var minion = world.Spawn(kind, position + dir * (radius + 0.4f));
                if (minion != null) Impact.Burst(minion.transform.position, new Color(1f, 0.4f, 0.5f), 0.9f);
            }
            if (count - fromCore > 0) world.SummonBossReinforcements(count - fromCore);
        }

        /// <summary>Throws cocoons to points around the ship, inside the view.</summary>
        private void LayCocoons(int count)
        {
            var position = (Vector2)transform.position;
            var view = Arena.ViewHalfSize;
            for (var i = 0; i < count; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(3.2f, 5.2f);
                var target = Arena.ShipPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                target.x = Mathf.Clamp(target.x, -view.x + 0.9f, view.x - 0.9f);
                target.y = Mathf.Clamp(target.y, -view.y + 1.6f, view.y - 2.6f);
                var cocoon = world.Spawn(EnemyKind.Cocoon, position);
                if (cocoon != null) cocoon.BeginCocoonFlight(position, target);
            }
        }

        private void Split()
        {
            hasSplit = true;
            var share = Mathf.Max(1f, hp * TwinShare);
            var position = (Vector2)transform.position;

            Juice.Shake(0.45f, 0.9f);
            Juice.ScreenFlash(new Color(1f, 0.4f, 0.95f, 0.35f), 0.5f);
            Impact.Ring(position, ChargeColor, radius, radius * 5f, 0.5f);
            Impact.Debris(position, baseColor, radius * 2f, 10);
            Sfx.Play(Sound.BossIncoming);

            MakeTwin(-1, share);
            var other = world.Spawn(definition.kind, position);
            if (other != null)
            {
                other.MakeTwin(1, share);
                other.bossClock = bossClock + SwayPeriod * 0.5f;
                other.summonTimer = definition.summonInterval * 0.5f;
            }
        }

        private void MakeTwin(int side, float health)
        {
            IsTwin = true;
            hasSplit = true;
            mirror = side;
            stationOffset = new Vector2(side * 1.9f, 0f);
            maxHp = health;
            hp = health;
            incomingDamage = 0f;
            partScale = TwinScale;
            radius = Mathf.Max(0.1f, definition.radius * partScale);
            bodyScale *= partScale;
            if (body != null) body.transform.localScale = Vector3.one * bodyScale;
            if (glow != null) glow.transform.localScale *= partScale;
            // Both halves take their places, then fall into the cycle.
            bossStep = BossStep.Return;
            transform.localScale = Vector3.one * 0.6f;
            LeanTween.cancel(gameObject, false, TweenAction.SCALE);
            LeanTween.scale(gameObject, Vector3.one, 0.45f).setEase(LeanTweenType.easeOutBack);
        }

        // ------------------------------------------------------------------
        // Cocoons
        // ------------------------------------------------------------------

        private void BeginCocoonFlight(Vector2 from, Vector2 to)
        {
            cocoonFrom = from;
            cocoonTarget = to;
            cocoonFlight = 0f;
            hatchTimer = HatchTime;
        }

        private void MoveCocoon(float dt)
        {
            if (cocoonFlight < 0f || cocoonFlight >= 1f) return;
            cocoonFlight = Mathf.Min(1f, cocoonFlight + dt / 0.85f);
            var k = 1f - (1f - cocoonFlight) * (1f - cocoonFlight);
            var arc = Mathf.Sin(cocoonFlight * Mathf.PI) * 1.2f;
            transform.position = Vector2.Lerp(cocoonFrom, cocoonTarget, k) + Vector2.up * arc;
            transform.Rotate(0f, 0f, 220f * dt, Space.Self);
            if (cocoonFlight >= 1f)
            {
                Impact.Ring(transform.position, baseColor, radius, radius * 3f, 0.3f);
            }
        }

        private void TickCocoon(float dt)
        {
            if (cocoonFlight >= 0f && cocoonFlight < 1f) return;
            if (paralyzed) return;
            hatchTimer -= dt * slowFactor;
            // It throbs faster as it gets ready.
            var urgency = 1f - Mathf.Clamp01(hatchTimer / HatchTime);
            var throb = 1f + 0.08f * Mathf.Sin(Time.time * (4f + urgency * 14f));
            body.transform.localScale = Vector3.one * bodyScale * throb;
            body.color = Color.Lerp(baseColor, Color.white, urgency * 0.35f);
            if (hatchTimer > 0f) return;

            var position = (Vector2)transform.position;
            Impact.Burst(position, baseColor, radius * 2.6f);
            for (var i = 0; i < HatchScouts; i++)
            {
                var dir = Rotate(Vector2.up, i * 360f / HatchScouts + Random.Range(-20f, 20f));
                world.Spawn(EnemyKind.Scout, position + dir * (radius + 0.25f));
            }
            // Hatched, not destroyed: pays nothing.
            world.KillEnemy(this, false);
        }
    }
}
