// AstroCrawler - which ships the player owns.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Vanguarda-7 is free; every other ship is bought once, in the hangar, for
    /// a fixed price in credits. Saved in PlayerPrefs.
    ///
    /// Price: a stage 1 run pays about 40-150 credits (a loss mid-stage to a
    /// victory, PassiveUpgrades.Award), so 4000 is roughly 27 to 100 stage 1
    /// runs (later stages pay more). Raised from 1000 on 05/11/2026.
    ///
    /// Saves from before ships were sold keep any ship that already had
    /// upgrade levels bought for it (credits were spent on it).
    /// </summary>
    public sealed class ShipUnlocks
    {
        public const int Price = 4000;
        public const string FreeShipId = "vanguard";
        private const string Prefix = "AstroCrawler.Ships.v1.unlocked.";

        private readonly PassiveUpgrades wallet;

        /// <summary>Seasonal ships stay in saves/catalogue but cannot be newly acquired.</summary>
        public static bool IsAvailableForAcquisition(string shipId) =>
            !string.IsNullOrEmpty(shipId) && shipId != "noel" && shipId != "arvore" && shipId != "neve";

        public ShipUnlocks(PassiveUpgrades wallet)
        {
            this.wallet = wallet;
        }

        public bool IsUnlocked(string shipId)
        {
            if (string.IsNullOrEmpty(shipId)) return false;
            if (shipId == FreeShipId || PlayerPrefs.GetInt(Prefix + shipId, 0) == 1) return true;
            // Bought with real money (store-confirmed cache, ShipPurchases): a refund revokes it.
            if (ShipPurchases.OwnsPaid(shipId)) return true;
            if (wallet != null && wallet.TotalLevel(shipId) > 0)
            {
                PlayerPrefs.SetInt(Prefix + shipId, 1);
                ProgressSnapshot.SaveLocal();
                return true;
            }
            return false;
        }

        /// <summary>Unlocks for good without spending (Starter Pack).</summary>
        public void Grant(string shipId)
        {
            if (!IsAvailableForAcquisition(shipId)) return;
            PlayerPrefs.SetInt(Prefix + shipId, 1);
            ProgressSnapshot.SaveLocal();
        }

        /// <summary>Spends the price and unlocks the ship; false when short of credits (nothing spent).</summary>
        public bool Buy(string shipId)
        {
            if (IsUnlocked(shipId)) return true;
            if (!IsAvailableForAcquisition(shipId)) return false;
            if (wallet == null || !wallet.Spend(Price)) return false;
            PlayerPrefs.SetInt(Prefix + shipId, 1);
            ProgressSnapshot.SaveLocal();
            return true;
        }
    }
}
