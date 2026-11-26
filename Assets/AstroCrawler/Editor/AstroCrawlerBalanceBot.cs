// AstroCrawler - automated balance runs.
//
// Plays whole runs in Play Mode with no input: starts a run, picks an upgrade
// before every wave, and writes how long each wave took and how much hull it
// cost to Logs/AstroCrawlerBalance.txt. The clock runs at a fixed 1/30 s step
// (a 30 fps phone) as fast as the editor can render, so a 10-wave run takes a
// minute or two instead of ten.
//
// Tools > AstroCrawler > Balanceamento. The save is left as it was found: the
// credits a run awards and the selected sector are restored when it finishes.

using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace AstroCrawler
{
    [InitializeOnLoad]
    public static class AstroCrawlerBalanceBot
    {
        private const string ReportPath = "Logs/AstroCrawlerBalance.txt";
        private const float Step = 1f / 30f;
        private const float WaveTimeout = 240f;

        private const string CreditsKey = "AstroCrawler.Passive.v1.credits";
        private const string StageKey = "AstroCrawler.SelectedStage";

        // How a reasonable player ranks the upgrades: firepower first, then
        // the abilities that clear groups, then sustain. Upgrading a card it
        // already owns is preferred over a new card of similar weight.
        private static readonly Dictionary<string, float> Priority = new Dictionary<string, float>
        {
            { "double_attack", 10f }, { "engine_overload", 9f }, { "wild_volley", 8f },
            { "piercing_shot", 7f }, { "missiles", 6f }, { "black_hole", 6.5f },
            { "support_drones", 6f }, { "critical_pulse", 5.5f }, { "temporal_distortion", 5f },
            { "chain_lightning", 6.5f }, { "shield", 5f }, { "emergency_repair", 4.5f },
            { "temporal_paralysis", 5f },
            { "siege_round", 6f }, { "repulsion_field", 3f }, { "battle_fury", 3.5f },
            { "debris_wall", 2.5f }, { "impact_charge", 2f }
        };

        private struct Plan
        {
            public string shipId;
            public int stage;

            /// <summary>Permanent-upgrade levels for this run; -1 keeps the save's.</summary>
            public int levels;
        }

        private class WaveRecord
        {
            public int index;
            public float duration;
            public float damage;
            public float hpEnd;
            public float hpMax;
            public string pick;
            public string outcome = "";

            /// <summary>Seconds with enemies alive but none in range: the wave stalls.</summary>
            public float idle;

            /// <summary>Closest any enemy's edge got to the hull; 0 is a hit.</summary>
            public float closest = float.MaxValue;
        }

        // Total permanent-upgrade levels given to every ship in a simulation,
        // spread evenly over the eight types; -1 keeps whatever the save has.
        private static int fixedPassiveLevels = -1;

        private static bool active;
        private static readonly List<Plan> plans = new List<Plan>();
        private static int planIndex;
        private static bool runStarted;
        private static int bootFrames;

        private static readonly List<WaveRecord> waves = new List<WaveRecord>();
        private static WaveRecord currentWave;
        private static float waveStartTime;
        private static float lastRunTime;
        private static float lastHp;
        private static string lastPick;
        private static readonly StringBuilder summary = new StringBuilder();

        private static bool hadCredits;
        private static int savedCredits;
        private static bool hadStage;
        private static int savedStage;

        static AstroCrawlerBalanceBot()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------

        [MenuItem("Tools/AstroCrawler/Balanceamento/Simular Setor Alfa (5 naves)", false, 80)]
        private static void SimulateAlpha()
        {
            Begin(new[] { 0 });
        }

        [MenuItem("Tools/AstroCrawler/Balanceamento/Simular os 3 setores (5 naves)", false, 81)]
        private static void SimulateAll()
        {
            Begin(new[] { 0, 1, 2 });
        }

        [MenuItem("Tools/AstroCrawler/Balanceamento/Parar simulação", false, 82)]
        private static void StopMenu()
        {
            if (active)
            {
                Finish("Interrompida pelo usuário.");
            }
        }

        [MenuItem("Tools/AstroCrawler/Balanceamento/Parar simulação", true)]
        private static bool CanStop()
        {
            return active;
        }

        /// <summary>
        /// Starts a simulation of the given sectors, with every ship or only
        /// the listed ones.
        /// </summary>
        public static void Begin(int[] stages, string[] shipIds = null, int passiveLevels = -1)
        {
            if (active)
            {
                Debug.LogWarning("[AstroCrawler] A simulação de balanceamento já está rodando.");
                return;
            }

            plans.Clear();
            var ships = shipIds ?? new[] { "vanguard", "bastion", "spectre", "aurora", "orion" };
            for (var s = 0; s < stages.Length; s++)
            {
                for (var i = 0; i < ships.Length; i++)
                {
                    plans.Add(new Plan { shipId = ships[i], stage = stages[s], levels = passiveLevels });
                }
            }
            Start(passiveLevels < 0 ? "as do save" : passiveLevels + " níveis por nave, distribuídos por igual");
        }

        /// <summary>
        /// A sweep for the progression curve: one ship plays stages[i] once
        /// for each permanent-upgrade total in levels[i], <paramref name="repeats"/>
        /// times each.
        /// </summary>
        public static void BeginSweep(string shipId, int[] stages, int[][] levels, int repeats = 1)
        {
            if (active)
            {
                Debug.LogWarning("[AstroCrawler] A simulação de balanceamento já está rodando.");
                return;
            }

            plans.Clear();
            for (var s = 0; s < stages.Length && s < levels.Length; s++)
            {
                for (var l = 0; l < levels[s].Length; l++)
                {
                    for (var r = 0; r < Mathf.Max(1, repeats); r++)
                    {
                        plans.Add(new Plan { shipId = shipId, stage = stages[s], levels = levels[s][l] });
                    }
                }
            }
            Start("varredura, com os níveis indicados em cada corrida");
        }

        private static void Start(string levelsNote)
        {
            // Whole runs are played; none of it may unlock stages in the save.
            StageProgress.readOnly = true;

            hadCredits = PlayerPrefs.HasKey(CreditsKey);
            savedCredits = PlayerPrefs.GetInt(CreditsKey, 0);
            hadStage = PlayerPrefs.HasKey(StageKey);
            savedStage = PlayerPrefs.GetInt(StageKey, 0);

            planIndex = 0;
            runStarted = false;
            bootFrames = 0;
            summary.Length = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, "AstroCrawler - simulação de balanceamento\n" +
                "Passo fixo de " + Step.ToString("0.000") + " s; escolhas por prioridade; melhorias permanentes: " +
                levelsNote + ".\n\n");

            active = true;
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        // ------------------------------------------------------------------
        // Driver
        // ------------------------------------------------------------------

        private static void Tick()
        {
            if (!active)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                // Someone left Play Mode by hand; don't drag them back in.
                if (!EditorApplication.isPlayingOrWillChangePlaymode && bootFrames > 0)
                {
                    Finish("Play Mode encerrado durante a simulação.");
                }
                return;
            }

            var game = GameManager.Instance;
            if (game == null || game.run == null || game.ui == null)
            {
                return;
            }

            // A few frames for the UI and the menu to settle before driving it.
            if (bootFrames++ < 10)
            {
                return;
            }

            Time.captureDeltaTime = Step;
            Application.targetFrameRate = -1;
            AudioListener.pause = true;

            // The player's 2x/3x choice would stretch the step; it stays saved.
            GameSpeed.forceNormal = true;

            var run = game.run;
            switch (run.phase)
            {
                case RunPhase.ShipSelection:
                    if (!runStarted)
                    {
                        StartRun(game);
                    }
                    break;

                case RunPhase.UpgradeSelection:
                    // Picks come from levels now, in the middle of a wave, so
                    // they no longer mark where one wave ends.
                    PickUpgrade(run);
                    break;

                case RunPhase.WaveInProgress:
                    TrackWave(run);
                    break;

                case RunPhase.Victory:
                case RunPhase.Defeat:
                    EndRun(run, run.phase == RunPhase.Victory);
                    break;
            }
        }

        private static void StartRun(GameManager game)
        {
            if (planIndex >= plans.Count)
            {
                Finish("Concluída.");
                return;
            }

            var plan = plans[planIndex];
            var ship = game.config.GetShip(plan.shipId);
            if (ship == null)
            {
                planIndex++;
                return;
            }

            waves.Clear();
            currentWave = null;
            lastPick = null;
            runStarted = true;
            fixedPassiveLevels = plan.levels;
            if (fixedPassiveLevels >= 0)
            {
                game.run.passiveUpgrades.OverrideLevels(plan.shipId, SpreadLevels(fixedPassiveLevels));
            }
            else
            {
                game.run.passiveUpgrades.ReloadLevels();
            }
            game.run.BeginRun(ship, plan.stage);
        }

        private static void PickUpgrade(RunController run)
        {
            var choices = run.cards.Choices;
            if (choices.Count == 0)
            {
                return;
            }

            RuntimeCard best = null;
            var bestScore = float.MinValue;
            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                float weight;
                if (!Priority.TryGetValue(card.definition.id, out weight))
                {
                    weight = 3f;
                }
                var level = run.cards.UpgradeLevel(card.definition.id);
                // Finishing a card it already owns beats opening a new one of
                // similar weight, a little more so for the first upgrade.
                var score = weight + (level == 1 ? 2.5f : level == 2 ? 1.5f : 0f) +
                    Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = card;
                }
            }

            lastPick = best.definition.id + " " + (run.cards.UpgradeLevel(best.definition.id) + 1);
            if (!run.ConfirmUpgrade(best))
            {
                return;
            }

            if (currentWave != null)
            {
                currentWave.pick = currentWave.pick == "-" ? lastPick : currentWave.pick + ", " + lastPick;
                return;
            }

            // The starting pick: the first wave begins with it.
            currentWave = new WaveRecord
            {
                index = run.waveIndex,
                pick = lastPick,
                hpMax = run.ship.maxHp
            };
            waveStartTime = run.runDuration;
            lastRunTime = run.runDuration;
            lastHp = run.ship.hp;
        }

        private static void TrackWave(RunController run)
        {
            if (currentWave == null || run.ship == null)
            {
                return;
            }

            // Waves follow one another without a screen in between.
            if (run.waveIndex != currentWave.index)
            {
                CloseWave(run, "");
                currentWave = new WaveRecord { index = run.waveIndex, pick = "-", hpMax = run.ship.maxHp };
                waveStartTime = run.runDuration;
                lastRunTime = run.runDuration;
                lastHp = run.ship.hp;
            }

            if (run.ship.hp < lastHp)
            {
                currentWave.damage += lastHp - run.ship.hp;
            }
            lastHp = run.ship.hp;

            var world = CombatWorld.Instance;
            var elapsed = run.runDuration - lastRunTime;
            lastRunTime = run.runDuration;
            if (world != null)
            {
                if (world.aliveCount > 0 && world.NearestInRange(Arena.ShipPosition) == null)
                {
                    currentWave.idle += elapsed;
                }
                var enemies = world.Enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || !enemy.isAlive) continue;
                    var gap = ((Vector2)enemy.transform.position - Arena.ShipPosition).magnitude -
                        enemy.radius - run.ship.radius;
                    currentWave.closest = Mathf.Min(currentWave.closest, Mathf.Max(0f, gap));
                }
            }

            if (run.runDuration - waveStartTime > WaveTimeout)
            {
                CloseWave(run, "TEMPO ESGOTADO");
                EndRun(run, false);
            }
        }

        private static void CloseWave(RunController run, string outcome)
        {
            if (currentWave == null)
            {
                return;
            }
            if (run.ship != null)
            {
                if (run.ship.hp < lastHp)
                {
                    currentWave.damage += lastHp - run.ship.hp;
                }
                currentWave.hpEnd = run.ship.hp;
            }
            currentWave.duration = run.runDuration - waveStartTime;
            currentWave.outcome = outcome;
            waves.Add(currentWave);
            currentWave = null;
        }

        private static void EndRun(RunController run, bool victory)
        {
            if (!runStarted)
            {
                return;
            }

            CloseWave(run, victory ? "" : "DESTRUÍDA");

            var plan = plans[planIndex];
            var report = new StringBuilder();
            report.Append("== ").Append(plan.shipId).Append(" | setor ").Append(plan.stage + 1)
                .Append(" ==\n");
            report.Append("onda  tempo   dano   vida     ocioso  perto  escolha\n");
            var totalDamage = 0f;
            var totalTime = 0f;
            for (var i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                totalDamage += wave.damage;
                totalTime += wave.duration;
                report.Append(wave.index.ToString().PadLeft(4))
                    .Append(wave.duration.ToString("0.0").PadLeft(7)).Append("s")
                    .Append(wave.damage.ToString("0").PadLeft(6))
                    .Append(("  " + Mathf.CeilToInt(wave.hpEnd) + "/" + Mathf.CeilToInt(wave.hpMax)).PadRight(10))
                    .Append(wave.idle.ToString("0.0").PadLeft(6)).Append("s")
                    .Append((wave.closest > 100f ? "-" : wave.closest.ToString("0.0")).PadLeft(6))
                    .Append("  ").Append(wave.pick)
                    .Append(string.IsNullOrEmpty(wave.outcome) ? "" : "   <- " + wave.outcome)
                    .Append('\n');
            }

            var lastWave = waves.Count > 0 ? waves[waves.Count - 1].index : 0;
            var line = plan.shipId + " setor " + (plan.stage + 1) +
                (fixedPassiveLevels >= 0 ? " [" + fixedPassiveLevels + " melhorias]" : "") + ": " +
                (victory ? "VITÓRIA" : "derrota na onda " + lastWave) +
                " | nível " + run.level + " | " + totalTime.ToString("0") + "s | dano recebido " + totalDamage.ToString("0") +
                (waves.Count > 0 && waves[waves.Count - 1].index == RunController.WavesPerRun
                    ? " | chefe " + waves[waves.Count - 1].duration.ToString("0.0") + "s"
                    : "");
            report.Append(line).Append("\n\n");
            summary.Append(line).Append('\n');

            File.AppendAllText(ReportPath, report.ToString());
            Debug.Log("[AstroCrawler] Balanceamento: " + line);

            runStarted = false;
            planIndex++;
            run.Restart();
        }

        private static int[] SpreadLevels(int total)
        {
            var levels = new int[8];
            total = Mathf.Clamp(total, 0, levels.Length * 20);
            for (var i = 0; i < total; i++) levels[i % levels.Length]++;
            return levels;
        }

        private static void Finish(string reason)
        {
            active = false;
            StageProgress.readOnly = false;
            var game = GameManager.Instance;
            if (game != null && game.run != null) game.run.passiveUpgrades.ReloadLevels();
            Time.captureDeltaTime = 0f;
            AudioListener.pause = false;
            GameSpeed.forceNormal = false;

            if (hadCredits) PlayerPrefs.SetInt(CreditsKey, savedCredits);
            else PlayerPrefs.DeleteKey(CreditsKey);
            if (hadStage) PlayerPrefs.SetInt(StageKey, savedStage);
            else PlayerPrefs.DeleteKey(StageKey);
            PlayerPrefs.Save();

            File.AppendAllText(ReportPath, "RESUMO (" + reason + ")\n" + summary + "\n");
            Debug.Log("[AstroCrawler] Simulação de balanceamento: " + reason + " Relatório em " +
                Path.GetFullPath(ReportPath) + "\n" + summary);

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
