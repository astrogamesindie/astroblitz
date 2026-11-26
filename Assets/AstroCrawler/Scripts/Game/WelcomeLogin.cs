// AstroCrawler - the welcome login: five rewards, one per day the player comes back.
//
// It runs once per account (the two keys travel with the progress in Cloud
// Save). Each new calendar day in which the player opens the game unlocks the
// next reward; days do not have to be consecutive and nothing resets. After the
// fifth reward the calendar is finished for good. The daily reward of the gift
// button is separate and keeps working as before.
//
//   day 1  a random base deck card        day 2  credits
//   day 3  a random base deck card        day 4  credits
//   day 5  credits worth a ship (ShipUnlocks.Price)
//
// A card day with every base card already unlocked pays the price of the next
// card in credits instead.

using System;

using UnityEngine;

namespace AstroCrawler
{
    public static class WelcomeLogin
    {
        public const int Days = 5;
        public const string ClaimedKey = "AstroCrawler.Welcome.v1.claimed";
        public const string LastDayKey = "AstroCrawler.Welcome.v1.lastDay";

        public const int Day2Credits = 300;
        public const int Day4Credits = 500;

        public enum RewardKind { Card, Credits, ShipCredits }

        public struct Result
        {
            public int day;
            public RewardKind kind;
            public int credits;
            public CardDefinition card;
        }

        public static RewardKind KindOf(int day)
        {
            if (day >= Days) return RewardKind.ShipCredits;
            return day % 2 == 1 ? RewardKind.Card : RewardKind.Credits;
        }

        /// <summary>Credits a day pays (0 for the card days).</summary>
        public static int CreditsFor(int day)
        {
            switch (day)
            {
                case 2: return Day2Credits;
                case 4: return Day4Credits;
                case Days: return ShipUnlocks.Price;
                default: return 0;
            }
        }

        /// <summary>Rewards already collected (0 to 5).</summary>
        public static int Claimed { get { return Mathf.Clamp(PlayerPrefs.GetInt(ClaimedKey, 0), 0, Days); } }

        public static bool Finished { get { return Claimed >= Days; } }

        /// <summary>The day whose reward is next (1 to 5).</summary>
        public static int NextDay { get { return Mathf.Min(Days, Claimed + 1); } }

        /// <summary>Same calendar day as the daily reward: days since 1 Jan 2000, local time.</summary>
        public static int Today { get { return (int)(DateTime.Now.Date - new DateTime(2000, 1, 1)).TotalDays; } }

        /// <summary>True when a reward is waiting: not finished and nothing collected today yet.</summary>
        public static bool CanClaimToday
        {
            get { return !Finished && PlayerPrefs.GetInt(LastDayKey, int.MinValue) != Today; }
        }

        /// <summary>Collects today's reward. False when nothing is waiting.</summary>
        public static bool Claim(RunController run, out Result result)
        {
            result = new Result();
            if (run == null || !CanClaimToday) return false;

            var day = NextDay;
            result.day = day;
            result.kind = KindOf(day);
            if (result.kind == RewardKind.Card)
            {
                result.card = run.cardUnlocks != null ? run.cardUnlocks.GrantRandomBaseCard() : null;
                if (result.card == null)
                {
                    // Every base card is already owned: the card's price instead.
                    result.credits = run.cardUnlocks != null ? run.cardUnlocks.NextCost : CardUnlocks.BaseCost;
                }
            }
            else
            {
                result.credits = CreditsFor(day);
            }
            if (result.credits > 0) run.passiveUpgrades.AddRewardedCredits(result.credits);

            PlayerPrefs.SetInt(ClaimedKey, day);
            PlayerPrefs.SetInt(LastDayKey, Today);
            ProgressSnapshot.SaveLocal();
            return true;
        }
    }
}
