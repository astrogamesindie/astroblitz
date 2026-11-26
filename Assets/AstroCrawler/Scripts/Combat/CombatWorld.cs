// AstroCrawler - the arena registry.
//
// Every combat query in the game goes through here: nearest target for the
// auto-cannon, enemies inside a black hole's pull radius, the wave's remaining
// count. Keeping one authoritative list avoids per-frame FindObjectsOfType and
// gives abilities a single place to hook kills (Furia de Batalha, Detonacao em
// Cadeia).

using System;
using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public class CombatWorld : MonoBehaviour
    {
        public static CombatWorld Instance { get; private set; }

        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly Stack<Enemy> pool = new Stack<Enemy>();

        private GameConfig config;
        private Transform enemyRoot;

        /// <summary>
        /// Parent for every transient object an ability drops into the arena
        /// (wells, walls, missiles, shockwaves), so a run teardown can clear the
        /// whole lot in one call.
        /// </summary>
        public Transform effectRoot { get; private set; }

        /// <summary>Global speed multiplier applied by Distorcao Temporal.</summary>
        public float globalEnemySpeedScale = 1f;
        // Separate from card slows; freezes movement and boss summons, including new spawns.
        public bool specialStasis;

        /// <summary>
        /// Multiplies the health of everything spawned from now on. Set per wave
        /// from <see cref="StageCatalog.HealthScale"/>, so late waves and harder
        /// sectors keep pace with the upgrades the player has collected.
        /// </summary>
        public float healthScale = 1f;

        /// <summary>
        /// Raised for every enemy death. The bool says whether the kill came from
        /// player damage (as opposed to crashing into the hull), which decides
        /// whether meteors fragment and whether kill-triggered cards fire.
        /// </summary>
        public event Action<Enemy, bool> onEnemyKilled;
        public event Action<Vector2, int> onCannonHit;

        public void NotifyCannonHit(Vector2 position, int spawnId)
        {
            onCannonHit?.Invoke(position, spawnId);
        }

        public int aliveCount
        {
            get { return enemies.Count; }
        }

        public IReadOnlyList<Enemy> Enemies
        {
            get { return enemies; }
        }

        /// <summary>
        /// While true the boss keeps summoning and the wave cannot be cleared by
        /// emptying the arena - only by killing the core (GDD 5.3).
        /// </summary>
        public bool bossActive { get { return bosses.Count > 0; } }

        /// <summary>
        /// The boss on the field; after the core splits, the first half still
        /// alive. Null once every part is down.
        /// </summary>
        public Enemy boss { get { return bosses.Count > 0 ? bosses[0] : null; } }

        // Every boss part alive (the core, or its two halves after the split).
        private readonly List<Enemy> bosses = new List<Enemy>(2);
        private float bossPool;

        /// <summary>
        /// Health left of the whole boss fight, 0 to 1: the parts alive
        /// against the core's original health (the bar does not refill when it splits).
        /// </summary>
        public float BossHealthFraction
        {
            get
            {
                if (bosses.Count == 0 || bossPool <= 0f) return 0f;
                var left = 0f;
                for (var i = 0; i < bosses.Count; i++) left += Mathf.Max(0f, bosses[i].hp);
                return Mathf.Clamp01(left / bossPool);
            }
        }

        /// <summary>Stage of the run (0-9), set with each wave; sizes the boss's bolts.</summary>
        public int stageLevel;

        private void Awake()
        {
            Instance = this;
            enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(transform, false);

            effectRoot = new GameObject("AbilityObjects").transform;
            effectRoot.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Initialize(GameConfig config)
        {
            this.config = config;
        }

        // ------------------------------------------------------------------
        // Spawning
        // ------------------------------------------------------------------

        public Enemy Spawn(EnemyKind kind, Vector2 position)
        {
            var definition = config.GetEnemy(kind);
            if (definition == null)
            {
                Debug.LogWarning("[AstroCrawler] No definition for enemy " + kind);
                return null;
            }

            var enemy = Acquire();
            enemy.Setup(definition, this, position, healthScale);
            enemies.Add(enemy);

            if (enemy.isBoss)
            {
                if (bosses.Count == 0) bossPool = enemy.maxHp;
                bosses.Add(enemy);
            }

            return enemy;
        }

        public Enemy SpawnOnRing(EnemyKind kind)
        {
            return Spawn(kind, Arena.RandomSpawnPoint());
        }

        /// <summary>
        /// Takes an enemy from the pool, or builds one. Enemies are the only
        /// frequently spawned object that used to be destroyed on death, so they
        /// are the ones that benefit most from both the prefab and the pool.
        /// </summary>
        private Enemy Acquire()
        {
            Enemy enemy = null;
            while (enemy == null && pool.Count > 0)
            {
                // Entries go stale if a scene teardown destroyed them.
                enemy = pool.Pop();
            }

            if (enemy != null)
            {
                enemy.gameObject.SetActive(true);
                return enemy;
            }

            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.enemy : null;
            if (prefab != null)
            {
                return Instantiate(prefab, enemyRoot);
            }

            // No prefab library: keep working the way the game did before.
            var go = new GameObject("Enemy");
            go.transform.SetParent(enemyRoot, false);
            return go.AddComponent<Enemy>();
        }

        /// <summary>Called by Enemy.Despawn once it has been taken out of play.</summary>
        public void Recycle(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }
            pool.Push(enemy);
        }

        /// <summary>Destroys the pooled instances; called when a run tears down.</summary>
        public void ClearPool()
        {
            while (pool.Count > 0)
            {
                var enemy = pool.Pop();
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
        }

        /// <summary>
        /// Boss reinforcements (GDD 5.3). They are ordinary enemies, so they
        /// count towards nothing except the pressure they apply.
        /// </summary>
        public void SummonBossReinforcements(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var kind = UnityEngine.Random.value < 0.6f ? EnemyKind.Scout : EnemyKind.Interceptor;
                SpawnOnRing(kind);
            }
        }

        // ------------------------------------------------------------------
        // Deaths
        // ------------------------------------------------------------------

        /// <summary>
        /// Removes an enemy, fragmenting meteors when the kill came from damage.
        /// </summary>
        public void KillEnemy(Enemy enemy, bool byPlayerDamage)
        {
            if (enemy == null || !enemy.isAlive || !enemies.Remove(enemy))
            {
                return;
            }

            var position = (Vector2)enemy.transform.position;
            var definition = enemy.definition;

            if (byPlayerDamage)
            {
                Sfx.Play(enemy.isBoss ? Sound.BossDown : Sound.EnemyDown);
            }

            bosses.Remove(enemy);

            if (onEnemyKilled != null)
            {
                onEnemyKilled(enemy, byPlayerDamage);
            }

            enemy.PlayDeathEffect(byPlayerDamage);
            enemy.Despawn();

            if (byPlayerDamage && definition.fragmentCount > 0)
            {
                for (var i = 0; i < definition.fragmentCount; i++)
                {
                    var angle = (i / (float)definition.fragmentCount) * Mathf.PI * 2f +
                                UnityEngine.Random.Range(0f, Mathf.PI);
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (definition.radius * 0.9f);
                    Spawn(definition.fragmentInto, position + offset);
                }
            }
        }

        /// <summary>Clears the arena between waves and on death.</summary>
        public void ClearAll()
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] != null)
                {
                    enemies[i].Despawn();
                }
            }
            enemies.Clear();
            bosses.Clear();
            globalEnemySpeedScale = 1f;
            specialStasis = false;
            healthScale = 1f;

            for (var i = effectRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(effectRoot.GetChild(i).gameObject);
            }
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        private readonly Dictionary<EnemyKind, int> killsToClear = new Dictionary<EnemyKind, int>();

        /// <summary>
        /// How many kills one enemy of this kind stands for once its fragments
        /// are counted: a large meteor is itself, two mediums and four smalls.
        /// </summary>
        public int KillsToClear(EnemyKind kind)
        {
            int total;
            if (killsToClear.TryGetValue(kind, out total))
            {
                return total;
            }

            total = 1;
            var definition = config != null ? config.GetEnemy(kind) : null;
            if (definition != null && definition.fragmentCount > 0 && definition.fragmentInto != kind)
            {
                // Stored first, so a cycle in the data cannot recurse forever.
                killsToClear[kind] = 1;
                total += definition.fragmentCount * KillsToClear(definition.fragmentInto);
            }
            killsToClear[kind] = total;
            return total;
        }

        /// <summary>Kills still needed to empty the arena, fragments included.</summary>
        public int KillsToClearAlive()
        {
            var total = 0;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy != null && enemy.isAlive && enemy.definition != null)
                {
                    total += KillsToClear(enemy.definition.kind);
                }
            }
            return total;
        }

        /// <summary>Closest living enemy to a point, or null if the arena is empty.</summary>
        public Enemy Nearest(Vector2 from)
        {
            Enemy best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive)
                {
                    continue;
                }
                var distance = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>
        /// Closest living enemy inside the ship's detection range
        /// (<see cref="Arena.DetectionRadius"/>), or null. Everything that fires
        /// on its own targets through this, so nothing is shot before it is on
        /// screen and inside the ring the ship draws.
        /// With <paramref name="skipDoomed"/>, enemies that the bullets already
        /// in flight will kill are passed over, unless nothing else is in range.
        /// With preferNonBoss, approaching enemies take precedence over the
        /// stationary boss; distance still decides between enemies of the same kind.
        /// </summary>
        public Enemy NearestInRange(Vector2 from, bool skipDoomed = false, bool preferNonBoss = false)
        {
            Enemy best = null;
            Enemy doomed = null;
            var bestDistance = float.MaxValue;
            var doomedDistance = float.MaxValue;
            var bestPriority = int.MaxValue;
            var doomedPriority = int.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive || !Arena.InRange(enemy.transform.position, enemy.radius))
                {
                    continue;
                }
                var distance = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                var priority = preferNonBoss && enemy.isBoss ? 1 : 0;
                if (skipDoomed && enemy.isDoomed)
                {
                    if (priority < doomedPriority || (priority == doomedPriority && distance < doomedDistance))
                    {
                        doomedPriority = priority;
                        doomedDistance = distance;
                        doomed = enemy;
                    }
                    continue;
                }
                if (priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    bestPriority = priority;
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best != null ? best : doomed;
        }

        /// <summary>Whether any part of a living enemy's hull is inside the gameplay camera.</summary>
        public bool IsVisible(Enemy enemy)
        {
            return IsVisible(enemy, Camera.main);
        }

        private static bool IsVisible(Enemy enemy, Camera camera)
        {
            if (enemy == null || !enemy.isAlive || camera == null) return false;
            var position = camera.WorldToViewportPoint(enemy.transform.position);
            if (position.z <= 0f) return false;
            var halfHeight = camera.orthographicSize;
            var marginY = enemy.radius / (2f * halfHeight);
            var marginX = marginY / camera.aspect;
            return position.x >= -marginX && position.x <= 1f + marginX &&
                position.y >= -marginY && position.y <= 1f + marginY;
        }

        /// <summary>Missiles already in flight may re-aim anywhere visible, beyond the firing ring.</summary>
        public Enemy NearestVisible(Vector2 from)
        {
            var camera = Camera.main;
            Enemy best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!IsVisible(enemy, camera)) continue;
                var distance = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with living enemies inside a radius.
        /// The caller owns the list so this allocates nothing per frame.
        /// </summary>
        public void InRadius(Vector2 center, float radius, List<Enemy> results)
        {
            results.Clear();
            var sqr = radius * radius;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isAlive)
                {
                    continue;
                }
                if (((Vector2)enemy.transform.position - center).sqrMagnitude <= sqr)
                {
                    results.Add(enemy);
                }
            }
        }

        /// <summary>
        /// Area damage centred on a point. Uses a local buffer because a kill can
        /// re-enter combat queries while processing the damage callbacks.
        /// </summary>
        public int DamageInRadius(Vector2 center, float radius, float damage)
        {
            var hits = new List<Enemy>();
            InRadius(center, radius, hits);
            var killed = 0;
            for (var i = 0; i < hits.Count; i++)
            {
                if (hits[i].TakeDamage(damage))
                {
                    killed++;
                }
            }
            return killed;
        }

        /// <summary>
        /// Up to <paramref name="count"/> distinct random living enemies inside
        /// the detection range.
        /// </summary>
        public List<Enemy> RandomEnemiesInRange(int count)
        {
            var pool = new List<Enemy>(enemies.Count);
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy != null && enemy.isAlive && Arena.InRange(enemy.transform.position, enemy.radius))
                {
                    pool.Add(enemy);
                }
            }
            var picked = new List<Enemy>();
            while (picked.Count < count && pool.Count > 0)
            {
                var index = UnityEngine.Random.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }
    }
}
