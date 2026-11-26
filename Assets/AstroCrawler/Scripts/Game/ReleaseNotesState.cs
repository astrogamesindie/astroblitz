using System;
using UnityEngine;

namespace AstroCrawler
{
    [Serializable]
    public sealed class ReleaseNotesDefinition
    {
        public string version;
        public string revision;
        public string[] notes;
        /// <summary>Credits the notes give once per account for this revision (0: no button).</summary>
        public int reward;
    }

    public static class ReleaseNotesState
    {
        private const string SeenKey = "AstroCrawler.ReleaseNotes.v1.seen";
        /// <summary>The revision whose reward was claimed; in the progress snapshot, so it holds across devices.</summary>
        public const string RewardKey = "AstroCrawler.ReleaseNotes.v1.rewarded";
        private static ReleaseNotesDefinition current;
        public static ReleaseNotesDefinition Current => current ?? (current = Load());
        public static string Token => BuildToken(Application.version, Application.buildGUID, Current.revision);
        public static bool Pending => ShouldShow(PlayerPrefs.GetString(SeenKey, ""), Token);

        public static string BuildToken(string version, string build, string revision) =>
            (version ?? "") + "|" + (string.IsNullOrEmpty(build) ? "development" : build) + "|" + (revision ?? "");
        public static bool ShouldShow(string seen, string token) => !string.Equals(seen, token, StringComparison.Ordinal);
        public static void Acknowledge()
        {
            PlayerPrefs.SetString(SeenKey, Token);
            PlayerPrefs.Save(); // Device preference, never an account/cloud checkpoint.
        }

        public static int Reward => Mathf.Max(0, Current.reward);
        public static bool RewardClaimed => PlayerPrefs.GetString(RewardKey, "") == (Current.revision ?? "");

        /// <summary>Pays this revision's reward once; returns the credits paid (0 if none or already claimed).</summary>
        public static int ClaimReward(PassiveUpgrades wallet)
        {
            var amount = Reward;
            if (wallet == null || amount <= 0 || RewardClaimed) return 0;
            wallet.AddRewardedCredits(amount);
            PlayerPrefs.SetString(RewardKey, Current.revision ?? "");
            ProgressSnapshot.SaveLocal();
            return amount;
        }

        private static ReleaseNotesDefinition Load()
        {
            var asset = Resources.Load<TextAsset>("AstroCrawler/ReleaseNotes");
            if (asset != null)
            {
                try
                {
                    var definition = JsonUtility.FromJson<ReleaseNotesDefinition>(asset.text);
                    if (definition != null && definition.notes != null &&
                        (string.IsNullOrEmpty(definition.version) || definition.version == Application.version)) return definition;
                }
                catch (ArgumentException) { }
            }
            return new ReleaseNotesDefinition { revision = "default", notes = new[] { "Melhorias e correções nesta versão." } };
        }
    }
}
