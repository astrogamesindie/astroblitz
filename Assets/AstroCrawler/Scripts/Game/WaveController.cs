// AstroCrawler - wave spawning and clear detection (GDD 7).

using System;
using System.Collections;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Spawns one wave's roster over time and decides when it is beaten. Every
    /// wave is identical for every ship on purpose: it is the benchmark that
    /// tells the player whether their build is keeping up (GDD 1).
    /// </summary>
    public class WaveController : MonoBehaviour
    {
        public WaveDefinition current { get; private set; }
        public bool isRunning { get; private set; }

        public int spawned { get; private set; }
        public int totalToSpawn { get; private set; }

        public event Action onWaveCleared;

        private CombatWorld world;
        private Coroutine spawnRoutine;
        private bool allSpawned;
        private bool suspended;

        public void SetSuspended(bool value) { suspended = value; }

        public void Initialize(CombatWorld world)
        {
            this.world = world;
        }

        public void StartWave(WaveDefinition wave)
        {
            Stop();

            current = wave;
            spawned = 0;
            allSpawned = false;
            totalToSpawn = wave.TotalEnemies();
            isRunning = true;
            suspended = false;

            spawnRoutine = StartCoroutine(SpawnRoutine(wave));
        }

        /// <summary>
        /// Enemies left in this wave: everything on the field plus everything
        /// still to arrive, each counted with the fragments it will break
        /// into. It starts at the wave's full total and only goes down - a
        /// meteor splitting does not raise it. The exception is the boss wave,
        /// where the core keeps summoning reinforcements.
        /// </summary>
        public int RemainingEnemies()
        {
            var total = world != null ? world.KillsToClearAlive() : 0;
            if (current == null || !isRunning)
            {
                return total;
            }

            var index = 0;
            for (var e = 0; e < current.entries.Count; e++)
            {
                var entry = current.entries[e];
                var unspawned = Mathf.Clamp(index + entry.count - spawned, 0, entry.count);
                total += unspawned * world.KillsToClear(entry.kind);
                index += entry.count;
            }
            return total;
        }

        public void Stop()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
            isRunning = false;
            suspended = false;
        }

        private IEnumerator SpawnRoutine(WaveDefinition wave)
        {
            // A short beat between the upgrade choice and the first enemy.
            yield return new WaitForSeconds(1.2f);

            for (var e = 0; e < wave.entries.Count; e++)
            {
                var entry = wave.entries[e];
                for (var i = 0; i < entry.count; i++)
                {
                    if (entry.kind == EnemyKind.AberrantCore)
                    {
                        // Enters from below the screen and slides up to its station.
                        world.Spawn(entry.kind, Arena.BossEntryPoint());
                    }
                    else
                    {
                        world.SpawnOnRing(entry.kind);
                    }
                    spawned++;
                    yield return new WaitForSeconds(wave.spawnInterval);
                }
            }

            allSpawned = true;
            spawnRoutine = null;
        }

        private void Update()
        {
            if (suspended || !isRunning || !allSpawned)
            {
                return;
            }

            if (current.isBossWave)
            {
                // Reinforcements are infinite while the core lives, so the only
                // clear condition is the core's death (GDD 5.3).
                if (world.boss == null)
                {
                    world.ClearAll();
                    Complete();
                }
                return;
            }

            // Meteors fragment on death, so "spawned everything" is not enough:
            // the arena has to actually be empty.
            if (world.aliveCount == 0)
            {
                Complete();
            }
        }

        private void Complete()
        {
            isRunning = false;
            if (onWaveCleared != null)
            {
                onWaveCleared();
            }
        }
    }
}
