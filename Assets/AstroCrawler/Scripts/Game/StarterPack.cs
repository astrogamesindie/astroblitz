// AstroCrawler - Starter Pack: one random ship + 3,000 credits, sold once
// for real money (product "starter_pack"), offered for 24 h on the main menu.

using System;
using System.Globalization;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// The offer window opens the first time the main menu asks for it and
    /// closes 24 h later (device clock, local pref). The contents are granted
    /// once per account: the "granted" key travels in ProgressSnapshot, so a
    /// restore on a device that already loaded the account's progress does
    /// not pay twice. A purchase confirmed after the window (pending payment,
    /// restore on a new device) is still granted.
    /// </summary>
    public static class StarterPack
    {
        public const string ProductId = "starter_pack";
        public const int Credits = 3000;
        public const double OfferHours = 24.0;

        public const string GrantedKey = "AstroCrawler.Starter.v1.granted";
        public const string ShipKey = "AstroCrawler.Starter.v1.ship";
        private const string StartKey = "AstroCrawler.Starter.v1.offerStartUtc";

        public static bool Granted => PlayerPrefs.GetInt(GrantedKey, 0) == 1;
        /// <summary>The ship the pack gave, or empty (none yet, or every paid ship was already owned).</summary>
        public static string GrantedShip => PlayerPrefs.GetString(ShipKey, "");
        /// <summary>The store confirmed the purchase and the contents are not in the save yet.</summary>
        public static bool PendingGrant => ShipPurchases.OwnsStarter && !Granted;

        /// <summary>Time left to buy; starts the 24 h window on first call.</summary>
        public static TimeSpan Remaining()
        {
            var now = DateTime.UtcNow;
            long ticks;
            var saved = PlayerPrefs.GetString(StartKey, "");
            if (!long.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                ticks <= 0 || ticks > now.Ticks)
            {
                // First look (or a clock set backwards): the window starts now.
                ticks = now.Ticks;
                PlayerPrefs.SetString(StartKey, ticks.ToString(CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            var left = new DateTime(ticks, DateTimeKind.Utc).AddHours(OfferHours) - now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        /// <summary>The menu button shows while the offer runs and nothing was bought.</summary>
        public static bool OfferVisible => !Granted && !ShipPurchases.OwnsStarter && Remaining() > TimeSpan.Zero;

        public static string Clock(TimeSpan left)
        {
            var hours = (int)Math.Floor(left.TotalHours);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", hours, left.Minutes, left.Seconds);
        }

        /// <summary>The credits (3000) written the way the current language groups thousands.</summary>
        public static string CreditsText()
        {
            switch (GameLocalization.Current)
            {
                case GameLocalization.Language.Portuguese:
                case GameLocalization.Language.Spanish:
                case GameLocalization.Language.German:
                case GameLocalization.Language.Italian:
                    return Credits.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
                case GameLocalization.Language.French:
                    return Credits.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
                default:
                    return Credits.ToString("#,0", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Pays the pack once: a random paid ship the player does not own
        /// (or its credit price when they own them all) plus the credits.
        /// Returns the ship id, or null when the price was paid instead.
        /// </summary>
        public static string Grant(PassiveUpgrades wallet, ShipUnlocks ships)
        {
            if (Granted || wallet == null || ships == null) return GrantedShip;
            var candidates = new System.Collections.Generic.List<string>();
            for (var i = 0; i < ShipPurchases.PaidShips.Length; i++)
                if (ShipUnlocks.IsAvailableForAcquisition(ShipPurchases.PaidShips[i]) &&
                    !ships.IsUnlocked(ShipPurchases.PaidShips[i])) candidates.Add(ShipPurchases.PaidShips[i]);

            string ship = null;
            var bonus = 0;
            if (candidates.Count > 0)
            {
                ship = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                ships.Grant(ship);
            }
            else bonus = ShipUnlocks.Price;

            wallet.AddRewardedCredits(Credits + bonus);
            PlayerPrefs.SetInt(GrantedKey, 1);
            PlayerPrefs.SetString(ShipKey, ship ?? "");
            ProgressSnapshot.SaveLocal();
            return ship;
        }
    }
}
