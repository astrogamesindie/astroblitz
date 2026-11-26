// AstroCrawler - points of the endless mode and the local record.
//
// Score = kills + wave clears. Each cycle (4 waves and a boss) raises the
// value of everything by 25%, so going deeper always beats farming early waves.
//   kill:       enemy XP x 10 x cycle multiplier (a meteor fragment is worth its own XP)
//   wave clear: 100 x global wave number
//   boss wave:  +1000 x cycle number on top of the wave clear
// Game speed (1x/2x/3x) does not change the score.

using System.Globalization;

using UnityEngine;

namespace AstroCrawler
{
    public static class EndlessScore
    {
        public const string BestKey = "AstroCrawler.Endless.v1.best";
        public const int KillPoints = 10;
        public const int WavePoints = 100;
        public const int BossPoints = 1000;
        public const float CycleStep = 0.25f;

        /// <summary>Cycle of a global wave, starting at 0 (waves 1-5 are cycle 0).</summary>
        public static int Cycle(int waveIndex) => Mathf.Max(0, waveIndex - 1) / StageCatalog.EndlessCycleLength;

        public static float Multiplier(int waveIndex) => 1f + CycleStep * Cycle(waveIndex);

        public static int ForKill(int enemyXp, int waveIndex) =>
            Mathf.RoundToInt(Mathf.Max(1, enemyXp) * KillPoints * Multiplier(waveIndex));

        public static int ForWave(int waveIndex)
        {
            var points = WavePoints * Mathf.Max(1, waveIndex);
            if (waveIndex > 0 && waveIndex % StageCatalog.EndlessCycleLength == 0)
                points += BossPoints * (Cycle(waveIndex) + 1);
            return points;
        }

        public static int Best => Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));

        /// <summary>Keeps the higher score; true when <paramref name="score"/> is a new record.</summary>
        public static bool Record(int score)
        {
            if (score <= 0 || score <= Best) return false;
            PlayerPrefs.SetInt(BestKey, score);
            ProgressSnapshot.SaveLocal();
            return true;
        }

        /// <summary>12.345 in pt/es/de/it, 12 345 in fr, 12,345 elsewhere.</summary>
        public static string Format(long score)
        {
            var text = score.ToString("#,0", CultureInfo.InvariantCulture);
            switch (GameLocalization.Current)
            {
                case GameLocalization.Language.Portuguese:
                case GameLocalization.Language.Spanish:
                case GameLocalization.Language.German:
                case GameLocalization.Language.Italian:
                    return text.Replace(',', '.');
                case GameLocalization.Language.French:
                    return text.Replace(',', ' ');
                default:
                    return text;
            }
        }
    }
}
