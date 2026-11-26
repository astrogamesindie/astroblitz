// AstroCrawler - which stages each ship has beaten, and what that unlocks.
//
// Progress belongs to a ship: beating "Nível 4" with the Vanguard says nothing
// about the Bastion. A stage is open for a ship when the one before it has been
// beaten with that same ship (the first stage is always open), so every ship
// climbs the ten stages on its own.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public static class StageProgress
    {
        private const string Prefix = "AstroCrawler.StageCleared.v2.";

        // The first version of this progress was one flag per stage for all ships.
        private const string GlobalPrefix = "AstroCrawler.StageCleared.v1.";
        private const string SelectedShipKey = "AstroCrawler.SelectedShipId";

        private static readonly Dictionary<string, bool[]> cleared = new Dictionary<string, bool[]>();
        private static bool migrated;

        /// <summary>
        /// While true nothing is recorded. The balance bot plays whole runs and
        /// must not unlock stages in the player's save.
        /// </summary>
        public static bool readOnly;

        public static bool IsCleared(string shipId, int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= StageCatalog.Count || string.IsNullOrEmpty(shipId)) return false;
            return Flags(shipId)[stageIndex];
        }

        /// <summary>The first stage is always open; each later one needs the previous beaten.</summary>
        public static bool IsUnlocked(string shipId, int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= StageCatalog.Count) return false;
            if (DebugMode.Enabled) return true;
            return stageIndex == 0 || IsCleared(shipId, stageIndex - 1);
        }

        public static int ClearedCount(string shipId)
        {
            if (string.IsNullOrEmpty(shipId)) return 0;
            var flags = Flags(shipId);
            var count = 0;
            for (var i = 0; i < flags.Length; i++) if (flags[i]) count++;
            return count;
        }

        /// <summary>
        /// The stage a ship is working on: the first one it has not beaten, or
        /// the last one once it has beaten them all.
        /// </summary>
        public static int CurrentStage(string shipId)
        {
            for (var i = 0; i < StageCatalog.Count; i++)
            {
                if (!IsCleared(shipId, i)) return i;
            }
            return StageCatalog.Count - 1;
        }

        /// <summary>Marks a stage as beaten by a ship. True when it was not beaten before.</summary>
        public static bool MarkCleared(string shipId, int stageIndex)
        {
            if (readOnly || stageIndex < 0 || stageIndex >= StageCatalog.Count || string.IsNullOrEmpty(shipId))
            {
                return false;
            }

            var flags = Flags(shipId);
            if (flags[stageIndex]) return false;

            flags[stageIndex] = true;
            PlayerPrefs.SetInt(Prefix + shipId + "." + stageIndex, 1);
            ProgressSnapshot.SaveLocal();
            return true;
        }

        /// <summary>Forgets the cached flags. Called at the start of every play session.</summary>
        public static void Reset()
        {
            cleared.Clear();
            migrated = false;
            readOnly = false;
        }

        /// <summary>
        /// Gives the old, ship-less flags to the ship that was selected when
        /// they were recorded, so nobody loses a stage they already beat, and
        /// removes them. There is no way to know better.
        /// </summary>
        private static void MigrateGlobalFlags()
        {
            if (migrated) return;
            migrated = true;

            // No ship was ever picked: keep the old flags for a later session.
            var ship = PlayerPrefs.GetString(SelectedShipKey, "");
            if (string.IsNullOrEmpty(ship)) return;

            var moved = false;
            for (var i = 0; i < StageCatalog.Count; i++)
            {
                if (PlayerPrefs.GetInt(GlobalPrefix + i, 0) != 1) continue;
                PlayerPrefs.SetInt(Prefix + ship + "." + i, 1);
                PlayerPrefs.DeleteKey(GlobalPrefix + i);
                moved = true;
            }
            if (moved) ProgressSnapshot.SaveLocal();
        }

        private static bool[] Flags(string shipId)
        {
            MigrateGlobalFlags();
            bool[] flags;
            if (cleared.TryGetValue(shipId, out flags)) return flags;

            flags = new bool[StageCatalog.Count];
            for (var i = 0; i < flags.Length; i++)
            {
                flags[i] = PlayerPrefs.GetInt(Prefix + shipId + "." + i, 0) == 1;
            }
            cleared.Add(shipId, flags);
            return flags;
        }
    }
}
