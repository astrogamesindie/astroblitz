// AstroCrawler - the 1x / 2x / 3x fast-forward.
//
// The run controller owns Time.timeScale: it freezes the game for the upgrade
// pick and runs it again when a wave starts. This is the speed it runs it at.
// The pause, the upgrade details and the hit stop each save the scale they
// interrupt and put it back, so they carry the speed through untouched.
//
// The choice is remembered between runs and sessions: a player who likes 2x
// should not have to ask for it every wave.

using UnityEngine;

namespace AstroCrawler
{
    public static class GameSpeed
    {
        public const int Max = 3;

        private const string SpeedKey = "AstroCrawler.GameSpeed";

        // 0 until read from the save.
        private static int multiplier;

        /// <summary>
        /// Holds the game at 1x without touching the saved choice. The balance
        /// bot sets it: its measurements assume the normal step.
        /// </summary>
        public static bool forceNormal;

        public static int Multiplier
        {
            get
            {
                if (forceNormal) return 1;
                if (multiplier == 0)
                {
                    multiplier = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 1), 1, Max);
                }
                return multiplier;
            }
        }

        /// <summary>Sets and saves the speed selected in the HUD.</summary>
        public static void Set(int speed)
        {
            multiplier = Mathf.Clamp(speed, 1, Max);
            PlayerPrefs.SetInt(SpeedKey, multiplier);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets the cached value. Called at the start of every play session.</summary>
        public static void Reset()
        {
            multiplier = 0;
        }
    }
}
