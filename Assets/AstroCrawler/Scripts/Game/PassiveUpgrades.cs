using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroCrawler
{
    public enum PassiveUpgrade
    {
        FireRate, Damage, Hull, Regeneration, Evasion, Income, CriticalChance, CriticalDamage
    }

    /// <summary>Progressão persistente compartilhando créditos entre as naves.</summary>
    public sealed class PassiveUpgrades
    {
        public const int MaxLevel = 20;

        // Level prices rise with the ship's level: the first costs SingleCost
        // and every CostStepLevels levels bought add CostStep. A pack of five
        // costs PackShare of the five prices. Sized with the balance bot and a
        // progression model so that one ship, played an hour a day at 2x,
        // clears the tenth sector in about a week (DOCUMENTACAO_TECNICA 4.4).
        public const int SingleCost = 100;
        public const int CostStep = 10;
        public const int CostStepLevels = 3;
        public const float PackShare = 0.8f;

        // Credits for a finished run: a base, a payment per wave cleared and a
        // victory bonus, all scaled by the stage (StageCatalog.RewardScale).
        public const int RewardBase = 10;
        public const int RewardPerWave = 6;
        public const int RewardVictory = 80;
        private const string Prefix = "AstroCrawler.Passive.v1.";
        private const int UpgradeTypes = 8;

        private readonly Dictionary<string, int[]> shipLevels = new Dictionary<string, int[]>();
        public int credits { get; private set; }

        public PassiveUpgrades()
        {
            credits = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "credits", 0));
        }

        public int Level(string shipId, PassiveUpgrade upgrade)
        {
            return Levels(shipId)[(int)upgrade];
        }

        /// <summary>
        /// Test hook for the balance bot: plays a ship with these levels for
        /// the rest of the session without saving them. Dropped by ReloadLevels.
        /// </summary>
        public void OverrideLevels(string shipId, int[] levels)
        {
            if (string.IsNullOrEmpty(shipId) || levels == null || levels.Length != UpgradeTypes) return;
            var copy = new int[UpgradeTypes];
            for (var i = 0; i < copy.Length; i++) copy[i] = Mathf.Clamp(levels[i], 0, MaxLevel);
            shipLevels[shipId] = copy;
        }

        /// <summary>Forgets every cached level; the next read comes from the save.</summary>
        public void ReloadLevels()
        {
            shipLevels.Clear();
        }

        /// <summary>The ship's level: every upgrade level bought for it (0 to 160).</summary>
        public int TotalLevel(string shipId)
        {
            var levels = Levels(shipId);
            var total = 0;
            for (var i = 0; i < levels.Length; i++) total += levels[i];
            return total;
        }

        /// <summary>Pays for something outside the upgrade tree (the base deck); false when short.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0 || credits < amount) return false;
            credits -= amount;
            SaveCredits();
            return true;
        }

        public int AvailableLevels(string shipId)
        {
            var levels = Levels(shipId);
            var count = 0;
            for (var i = 0; i < levels.Length; i++) count += MaxLevel - levels[i];
            return count;
        }

        public void Award(int clearedWaves, bool victory, string shipId, int alreadyPaid = 0,
            float rewardScale = 1f)
        {
            // The stage multiplier applies to the whole result, victory bonus included.
            var baseReward = (RewardBase + Mathf.Max(0, clearedWaves) * RewardPerWave +
                (victory ? RewardVictory : 0)) * Mathf.Max(0f, rewardScale);
            var incomeBonus = Level(shipId, PassiveUpgrade.Income) * 0.03f;
            var reward = Mathf.Max(0, Mathf.RoundToInt(baseReward * (1f + incomeBonus)) - alreadyPaid);
            credits += reward;
            SaveCredits();
            lastReward = reward;
        }

        public int lastReward { get; private set; }

        /// <summary>Extra credits paid on top of the last reward (evolution guarantee); counted in lastReward.</summary>
        public void AddToLastReward(int amount)
        {
            if (amount <= 0) return;
            credits = (int)Math.Min(int.MaxValue, (long)credits + amount);
            lastReward += amount;
            SaveCredits();
        }

        public void AddRewardedCredits(int amount)
        {
            if (amount <= 0) return;
            credits = (int)Math.Min(int.MaxValue, (long)credits + amount);
            SaveCredits();
        }

        /// <summary>Price of one level for a ship that already has <paramref name="ownedLevels"/>.</summary>
        public static int LevelCost(int ownedLevels)
        {
            return SingleCost + CostStep * (Mathf.Max(0, ownedLevels) / CostStepLevels);
        }

        /// <summary>
        /// Price of the ship's next <paramref name="amount"/> levels (1 or 5):
        /// a pack of five costs PackShare of their prices, rounded to 10.
        /// </summary>
        public int Cost(string shipId, int amount)
        {
            if (amount != 1 && amount != 5) return int.MaxValue;
            var owned = TotalLevel(shipId);
            var total = 0;
            for (var i = 0; i < amount; i++) total += LevelCost(owned + i);
            return amount == 1 ? total : Mathf.RoundToInt(total * PackShare / 10f) * 10;
        }

        public List<PassiveUpgrade> Buy(string shipId, int amount)
        {
            var cost = Cost(shipId, amount);
            if (string.IsNullOrEmpty(shipId) || credits < cost || AvailableLevels(shipId) < amount)
                return null;

            var levels = Levels(shipId);
            var results = new List<PassiveUpgrade>(amount);
            for (var i = 0; i < amount; i++)
            {
                var available = new List<int>(UpgradeTypes);
                for (var j = 0; j < levels.Length; j++)
                    if (levels[j] < MaxLevel) available.Add(j);
                var picked = available[UnityEngine.Random.Range(0, available.Count)];
                levels[picked]++;
                results.Add((PassiveUpgrade)picked);
            }

            credits -= cost;
            PlayerPrefs.SetInt(Prefix + shipId + ".levels.0", levels[0]);
            for (var i = 1; i < levels.Length; i++)
                PlayerPrefs.SetInt(Prefix + shipId + ".levels." + i, levels[i]);
            SaveCredits();
            return results;
        }

        private int[] Levels(string shipId)
        {
            if (string.IsNullOrEmpty(shipId)) return new int[UpgradeTypes];
            int[] levels;
            if (shipLevels.TryGetValue(shipId, out levels)) return levels;
            levels = new int[UpgradeTypes];
            for (var i = 0; i < levels.Length; i++)
                levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + shipId + ".levels." + i, 0), 0, MaxLevel);
            shipLevels.Add(shipId, levels);
            return levels;
        }

        private void SaveCredits()
        {
            PlayerPrefs.SetInt(Prefix + "credits", credits);
            ProgressSnapshot.SaveLocal();
        }

        public static string Name(PassiveUpgrade upgrade)
        {
            switch (upgrade)
            {
                case PassiveUpgrade.FireRate: return GameLocalization.Choose("Cadência de tiro", "Fire rate", "Cadencia de tiro");
                case PassiveUpgrade.Damage: return GameLocalization.Choose("Dano", "Damage", "Daño");
                case PassiveUpgrade.Hull: return GameLocalization.Choose("Vida", "Hull", "Vida");
                case PassiveUpgrade.Regeneration: return GameLocalization.Choose("Recuperação de vida", "Health regeneration", "Regeneración de vida");
                case PassiveUpgrade.Evasion: return GameLocalization.Choose("Esquiva", "Evasion", "Evasión");
                case PassiveUpgrade.Income: return GameLocalization.Choose("Créditos por partida", "Credits per run", "Créditos por partida");
                case PassiveUpgrade.CriticalChance: return GameLocalization.Choose("Chance crítica", "Critical chance", "Probabilidad crítica");
                default: return GameLocalization.Choose("Dano crítico", "Critical damage", "Daño crítico");
            }
        }

        public static string Bonus(PassiveUpgrade upgrade, int level)
        {
            switch (upgrade)
            {
                case PassiveUpgrade.FireRate: return "+" + (level * 2) + GameLocalization.Choose("% velocidade", "% fire rate", "% cadencia");
                case PassiveUpgrade.Damage: return "+" + (level * 3) + GameLocalization.Choose("% dano", "% damage", "% daño");
                case PassiveUpgrade.Hull: return "+" + (level * 5) + GameLocalization.Choose("% vida", "% hull", "% vida");
                case PassiveUpgrade.Regeneration: return "+" + (level * 0.2f).ToString("0.0") + GameLocalization.Choose(" vida/s", " HP/s", " vida/s");
                case PassiveUpgrade.Evasion: return "+" + (level * 1.5f).ToString("0.0") + GameLocalization.Choose("% esquiva", "% evasion", "% evasión");
                case PassiveUpgrade.Income: return "+" + (level * 3) + GameLocalization.Choose("% créditos", "% credits", "% créditos");
                case PassiveUpgrade.CriticalChance: return "+" + level + GameLocalization.Choose("% chance", "% chance", "% probabilidad");
                default: return "+" + (level * 5) + GameLocalization.Choose("% dano crítico", "% critical damage", "% daño crítico");
            }
        }
    }
}
