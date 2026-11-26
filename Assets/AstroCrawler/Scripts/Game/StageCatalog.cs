using System;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// The ten selectable stages ("Nível 1" to "Nível 10"), each a ten-wave
    /// campaign. Stage 1 plays the waves exactly as authored (waves.json); every
    /// later stage derives its roster from them: more enemies, tougher enemies,
    /// a tighter spawn rhythm and escorts that grow in kind and number.
    ///
    /// Difficulty is meant to outrun a fresh ship by a wide margin: the last
    /// stages are only beaten with a good share of the permanent upgrades
    /// (PassiveUpgrades), which is what a week of play pays for.
    /// </summary>
    public static class StageCatalog
    {
        public const int Count = 10;

        public static string Name(int index)
        {
            return GameLocalization.Choose("NÍVEL ", "LEVEL ", "NIVEL ") +
                (Mathf.Clamp(index, 0, Count - 1) + 1);
        }

        // Portuguese, English, Spanish. Short: the card has room for about
        // four lines.
        private static readonly string[][] Descriptions =
        {
            new[] { "Primeiro contato. Sobreviva à rota original de 10 ondas.",
                    "First contact. Survive the original 10-wave route.",
                    "Primer contacto. Sobrevive a la ruta original de 10 oleadas." },
            new[] { "A invasão acelera. Mais batedores, reforços e ondas densas.",
                    "The invasion accelerates. More scouts, reinforcements and dense waves.",
                    "La invasión se acelera. Más exploradores, refuerzos y oleadas densas." },
            new[] { "Interceptadores rápidos e fragatas fecham o cerco.",
                    "Fast interceptors and frigates close the net.",
                    "Interceptores rápidos y fragatas cierran el cerco." },
            new[] { "Batedores e interceptadores chegam juntos. Fragatas surgem antes do chefe.",
                    "Scouts and interceptors arrive together. Frigates show up before the boss.",
                    "Exploradores e interceptores llegan juntos. Las fragatas aparecen antes del jefe." },
            new[] { "As fragatas chegam mais cedo e o enxame não para de crescer.",
                    "Frigates arrive earlier and the swarm keeps growing.",
                    "Las fragatas llegan antes y el enjambre no deja de crecer." },
            new[] { "Ondas lotadas. O fogo cruzado cobra caro de quem tem pouca vida.",
                    "Crowded waves. Crossfire is costly for a thin hull.",
                    "Oleadas repletas. El fuego cruzado sale caro con poco casco." },
            new[] { "Escoltas reforçadas. O Núcleo Aberrante chega com um exército.",
                    "Reinforced escorts. The Aberrant Core brings an army.",
                    "Escoltas reforzadas. El Núcleo Aberrante llega con un ejército." },
            new[] { "Pressão total desde a primeira onda. Cada erro custa caro.",
                    "Full pressure from the first wave. Every mistake counts.",
                    "Presión total desde la primera oleada. Cada error cuenta." },
            new[] { "O limite do setor. Só naves muito bem equipadas chegam ao chefe.",
                    "The edge of the sector. Only well-equipped ships reach the boss.",
                    "El límite del sector. Solo las naves bien equipadas llegan al jefe." },
            new[] { "O último nível. Enxames, fragatas e um Núcleo no auge da força.",
                    "The final level. Swarms, frigates and a Core at full strength.",
                    "El último nivel. Enjambres, fragatas y un Núcleo en su máximo poder." }
        };

        public static string Description(int index)
        {
            var texts = Descriptions[Mathf.Clamp(index, 0, Count - 1)];
            return GameLocalization.Choose(texts[0], texts[1], texts[2]);
        }

        private static readonly Color[] Accents =
        {
            new Color(0.36f, 0.82f, 1f),
            new Color(1f, 0.72f, 0.31f),
            new Color(1f, 0.38f, 0.48f),
            new Color(0.72f, 0.45f, 1f),
            new Color(0.45f, 0.95f, 0.6f),
            new Color(1f, 0.55f, 0.25f),
            new Color(1f, 0.4f, 0.85f),
            new Color(0.3f, 0.9f, 0.85f),
            new Color(1f, 0.3f, 0.3f),
            new Color(1f, 0.85f, 0.35f)
        };

        public static Color Accent(int index)
        {
            return Accents[Mathf.Clamp(index, 0, Count - 1)];
        }

        /// <summary>
        /// Wave 1 enemies are a little softer than their table value, so the
        /// meteor fragments die in one shot while the ship has no upgrades yet.
        /// </summary>
        public const float HealthAtWaveOne = 0.85f;

        /// <summary>Extra enemy health per wave while the player is still building up.</summary>
        public const float HealthPerEarlyWave = 0.12f;

        /// <summary>Extra enemy health per wave once most upgrades are in (wave 13 on).</summary>
        public const float HealthPerLateWave = 0.06f;

        private const int EarlyWaves = 12;

        // Enemy health by stage, relative to the first. Stages 2 and 3 were
        // tuned with the balance bot against cards of three levels and a ship
        // capped at level 12; the rest follow from the target that the last
        // stage is only beaten with most of the permanent upgrades.
        private static readonly float[] HealthFactor =
            { 1f, 1.25f, 1.5f, 1.86f, 2.21f, 2.57f, 2.93f, 3.29f, 3.64f, 4f };

        // Enemies per wave by stage, relative to the authored roster.
        private static readonly float[] CountFactor =
            { 1f, 1.2f, 1.4f, 1.5f, 1.6f, 1.75f, 1.9f, 2f, 2.1f, 2.2f };

        // Escorts are capped so the boss wave stays readable and affordable.
        private const int MaxBossScouts = 16;
        private const int MaxBossInterceptors = 18;

        /// <summary>
        /// Credits multiplier for finishing a run in this stage. Higher stages
        /// pay more, so progress is also faster progress.
        /// </summary>
        public static float RewardScale(int stageIndex)
        {
            return 1f + RewardPerStage * Mathf.Clamp(stageIndex, 0, Count - 1);
        }

        public const float RewardPerStage = 0.25f;

        /// <summary>
        /// Health multiplier for every enemy spawned in a wave. It climbs
        /// quickly while each pick still makes the ship much stronger, then
        /// slows down: by wave 13 the deck is mostly evolved and the roster
        /// itself (frigates, the wave 19 gauntlet) carries the difficulty.
        /// The harder stages multiply the whole curve.
        /// </summary>
        public static float HealthScale(int stageIndex, int waveIndex)
        {
            var stage = StageMultiplier(HealthFactor, stageIndex, waveIndex);
            var steps = Mathf.Max(0, waveIndex - 1);
            var early = Mathf.Min(steps, EarlyWaves);
            var late = Mathf.Max(0, steps - EarlyWaves);
            return stage * (HealthAtWaveOne + HealthPerEarlyWave * early + HealthPerLateWave * late);
        }

        // Stages 1 to 3 apply their factors from the first wave, as they always
        // did. From the fourth on the factor grows over the stage instead: a
        // ship that has just taken its first pick cannot meet the final stage's
        // full strength on wave 1, and the permanent upgrades are supposed to
        // be what carries it to the end, not a wall at the start.
        private const int FlatStages = 3;
        private const float FirstWaveShare = 0.4f;

        private static float Ramp(int stageIndex, int waveIndex)
        {
            if (stageIndex < FlatStages) return 1f;
            return FirstWaveShare + (1f - FirstWaveShare) * Mathf.Clamp01((waveIndex - 1) / 9f);
        }

        private static float StageMultiplier(float[] factors, int stageIndex, int waveIndex)
        {
            var k = Mathf.Clamp(stageIndex, 0, Count - 1);
            return 1f + (factors[k] - 1f) * Ramp(k, waveIndex);
        }

        // Escort rules. k is the stage index (0 = first stage), w the wave.
        // The values for k = 1 and k = 2 are the ones those two stages always
        // had; the formulas just keep going from there. Stage 3 (k = 2) has
        // interceptors and frigates but no scouts.

        private static int Scouts(int k, int w, bool boss)
        {
            if (k == 0 || k == 2) return 0;
            if (boss) return Math.Min(MaxBossScouts, 6 + 2 * k);
            return 2 + k / 2 + w / Math.Max(3, 7 - k);
        }

        private static int Interceptors(int k, int w, bool boss)
        {
            if (k < 2) return 0;
            if (boss) return Math.Min(MaxBossInterceptors, 8 + 2 * k);
            return 1 + k / 2 + w / Math.Max(2, 5 - k / 2);
        }

        private static int Frigates(int k, int w, bool boss)
        {
            if (k < 2) return 0;
            if (boss) return 2 + (k - 2) / 2;
            return w >= Math.Max(5, 12 - k) ? 1 + (k - 2) / 3 : 0;
        }

        public const int EndlessCycleLength = 5;
        public const float EndlessXpScale = 0.75f;
        public static string EndlessName => GameLocalization.Choose("MODO INFINITO", "ENDLESS MODE", "MODO INFINITO");

        /// <summary>Stage index (0-based) any ship must beat to open the endless mode: Nível 3.</summary>
        public const int EndlessUnlockStage = 2;

        public static bool EndlessUnlocked(GameConfig config)
        {
            if (config == null) return false;
            if (DebugMode.Enabled) return true;
            foreach (var ship in config.ships)
                if (StageProgress.IsCleared(ship.id, EndlessUnlockStage)) return true;
            return false;
        }

        public static WaveDefinition GetEndlessWave(GameConfig config, int waveIndex)
        {
            if (waveIndex < 1) return null;
            var cycle = (waveIndex - 1) / EndlessCycleLength;
            var slot = (waveIndex - 1) % EndlessCycleLength;
            // Reuse authored rosters, clone them before scaling, and reserve every fifth wave for the boss.
            var sourceIndex = slot == 4 ? WavesBossIndex : new[] { 1, 3, 6, 8 }[slot];
            var source = GetWave(config, Mathf.Min(Count - 1, 2 + cycle), sourceIndex);
            if (source == null) return null;
            var wave = new WaveDefinition { index = waveIndex, label = source.label,
                designNote = source.designNote, isBossWave = slot == 4,
                spawnInterval = Mathf.Max(0.12f, source.spawnInterval / (1f + cycle * 0.06f)) };
            foreach (var entry in source.entries)
                wave.entries.Add(new WaveEntry { kind = entry.kind,
                    count = entry.kind == EnemyKind.AberrantCore ? 1 :
                        Mathf.Max(1, Mathf.CeilToInt(entry.count * Mathf.Min(2f, 1f + cycle * 0.1f))) });
            return wave;
        }

        private const int WavesBossIndex = RunController.WavesPerRun;
        public static float EndlessHealthScale(int waveIndex) =>
            HealthScale(Mathf.Min(Count - 1, 2 + (waveIndex - 1) / EndlessCycleLength), waveIndex);

        public static WaveDefinition GetWave(GameConfig config, int stageIndex, int waveIndex)
        {
            var source = config.GetWave(waveIndex);
            if (source == null || stageIndex <= 0) return source;

            var k = Mathf.Clamp(stageIndex, 0, Count - 1);
            var wave = new WaveDefinition
            {
                index = source.index,
                label = source.label,
                designNote = source.designNote,
                isBossWave = source.isBossWave,
                spawnInterval = Mathf.Max(0.12f, source.spawnInterval * Mathf.Max(0.55f, 1f - 0.1f * k))
            };

            var multiplier = StageMultiplier(CountFactor, k, waveIndex);
            for (var i = 0; i < source.entries.Count; i++)
            {
                var entry = source.entries[i];
                wave.entries.Add(new WaveEntry
                {
                    kind = entry.kind,
                    count = entry.kind == EnemyKind.AberrantCore ? 1 :
                        Mathf.Max(1, Mathf.CeilToInt(entry.count * multiplier))
                });
            }

            var boss = source.isBossWave;
            var share = boss ? 1f : Ramp(k, waveIndex);
            AddEscort(wave, EnemyKind.Scout, Scaled(Scouts(k, waveIndex, boss), share));
            AddEscort(wave, EnemyKind.Interceptor, Scaled(Interceptors(k, waveIndex, boss), share));
            AddEscort(wave, EnemyKind.Frigate, Scaled(Frigates(k, waveIndex, boss), share));
            return wave;
        }

        private static int Scaled(int count, float share)
        {
            return count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * share));
        }

        private static void AddEscort(WaveDefinition wave, EnemyKind kind, int count)
        {
            if (count > 0)
            {
                wave.entries.Add(new WaveEntry { kind = kind, count = count });
            }
        }
    }
}
