// AstroCrawler - the part of the save that belongs to an account.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Packs, applies and wipes the progress keys of the PlayerPrefs save:
    /// credits, upgrade levels, cleared stages, the chosen ship and stage,
    /// bought cards and ships, the daily reward and the video wait. Device
    /// preferences (sound, language, speed, orientation, the ad-free purchase,
    /// update checks) are not progress and stay on the device.
    ///
    /// PlayerPrefs cannot list its keys, so the list is built from the game
    /// data (ship and card ids) and must grow with any new progress key.
    /// </summary>
    public static class ProgressSnapshot
    {
        public const int Version = 1;

        [Serializable]
        private sealed class Entry
        {
            public string k;
            public int i;
            public string s;
            public bool text;
        }

        [Serializable]
        private sealed class Payload
        {
            public int version;
            public List<Entry> entries = new List<Entry>();
        }

        private enum Kind { Int, Text }

        /// <summary>Persist a progress mutation locally and queue a cloud checkpoint.</summary>
        public static void SaveLocal()
        {
            PlayerPrefs.Save();
            AccountService.Instance?.NotifyProgressChanged();
        }

        private static List<KeyValuePair<string, Kind>> Keys(GameConfig config)
        {
            var keys = new List<KeyValuePair<string, Kind>>();
            Action<string> number = key => keys.Add(new KeyValuePair<string, Kind>(key, Kind.Int));
            Action<string> text = key => keys.Add(new KeyValuePair<string, Kind>(key, Kind.Text));

            number("AstroCrawler.Passive.v1.credits");
            text("AstroCrawler.SelectedShipId");
            number("AstroCrawler.SelectedStage");
            number("AstroCrawler.BaseDeck.v1.purchases");
            number("AstroCrawler.DailyReward.Day");
            number("AstroCrawler.DailyReward.Amount");
            number("AstroCrawler.DailyReward.ClaimedDay");
            text("AstroCrawler.CoinsAdReadyAtUtc");
            number(WelcomeLogin.ClaimedKey);
            number(WelcomeLogin.LastDayKey);
            text(ReleaseNotesState.RewardKey);
            number(EndlessScore.BestKey);
            number(StarterPack.GrantedKey);
            text(StarterPack.ShipKey);
            for (var stage = 0; stage < StageCatalog.Count; stage++) number("AstroCrawler.StageCleared.v1." + stage);

            if (config != null)
            {
                for (var s = 0; s < config.ships.Count; s++)
                {
                    var id = config.ships[s].id;
                    for (var level = 0; level < 8; level++) number("AstroCrawler.Passive.v1." + id + ".levels." + level);
                    for (var stage = 0; stage < StageCatalog.Count; stage++)
                        number("AstroCrawler.StageCleared.v2." + id + "." + stage);
                    number("AstroCrawler.Ships.v1.unlocked." + id);
                }
                for (var c = 0; c < config.cards.Count; c++)
                    if (config.cards[c].category == CardCategory.Common)
                        number("AstroCrawler.BaseDeck.v1.unlocked." + config.cards[c].id);
            }
            return keys;
        }

        /// <summary>The progress currently on the device, as JSON (only keys that exist).</summary>
        public static string Capture(GameConfig config)
        {
            var payload = new Payload { version = Version };
            foreach (var pair in Keys(config))
            {
                if (!PlayerPrefs.HasKey(pair.Key)) continue;
                payload.entries.Add(pair.Value == Kind.Text
                    ? new Entry { k = pair.Key, s = PlayerPrefs.GetString(pair.Key, ""), text = true }
                    : new Entry { k = pair.Key, i = PlayerPrefs.GetInt(pair.Key, 0) });
            }
            return JsonUtility.ToJson(payload);
        }

        private static bool TryRead(GameConfig config, string json, out Payload payload)
        {
            payload = null;
            if (config == null || string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                payload = JsonUtility.FromJson<Payload>(json);
            }
            catch (Exception)
            {
                return false;
            }
            if (payload == null || payload.version != Version || payload.entries == null) return false;
            var allowed = new Dictionary<string, Kind>(Keys(config));
            var seen = new HashSet<string>();
            foreach (var entry in payload.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.k) || !seen.Add(entry.k) ||
                    !allowed.TryGetValue(entry.k, out var kind) || entry.text != (kind == Kind.Text)) return false;
            }
            return true;
        }

        public static bool IsValid(GameConfig config, string json) => TryRead(config, json, out _);

        /// <summary>Validate the complete payload before replacing any local progress.</summary>
        public static bool Apply(GameConfig config, string json)
        {
            if (!TryRead(config, json, out var payload)) return false;

            Wipe(config);
            for (var i = 0; i < payload.entries.Count; i++)
            {
                var entry = payload.entries[i];
                if (entry.text) PlayerPrefs.SetString(entry.k, entry.s ?? "");
                else PlayerPrefs.SetInt(entry.k, entry.i);
            }
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Removes every progress key: the device starts like a new player.</summary>
        public static void Wipe(GameConfig config)
        {
            foreach (var pair in Keys(config)) PlayerPrefs.DeleteKey(pair.Key);
            StageProgress.Reset();
            PlayerPrefs.Save();
        }
    }
}
