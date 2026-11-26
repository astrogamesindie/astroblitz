// AstroCrawler - the whole designer-authored database, loaded from JSON.
//
// Adapted from CCG Kit's GameConfiguration: same idea (a plain C# object graph
// serialized with FullSerializer, loaded from Resources at runtime and written
// back from a custom editor window), retargeted at AstroCrawler's four tables
// and stripped of the turn-based / networked pieces the kit needed.

using System;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

using FullSerializer;

namespace AstroCrawler
{
    /// <summary>
    /// Cards, ships, enemies and waves. One instance is owned by
    /// <see cref="GameManager"/> and shared by every system.
    /// </summary>
    public class GameConfig
    {
        public const string ResourceFolder = "AstroCrawler";

        public List<CardDefinition> cards = new List<CardDefinition>();
        public List<ShipDefinition> ships = new List<ShipDefinition>();
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        public List<WaveDefinition> waves = new List<WaveDefinition>();
        public ProgressionDefinition progression = new ProgressionDefinition();

        private static readonly fsSerializer serializer = new fsSerializer();

        /// <summary>
        /// Loads the database. The ScriptableObject is the source of truth; the
        /// JSON tables remain only as a fallback so an install that has not been
        /// migrated yet still boots, and as the import format for the editor
        /// tool.
        /// </summary>
        public static GameConfig LoadFromResources()
        {
            var database = GameDatabase.Load();
            if (database != null)
            {
                return database.BuildConfig();
            }

            Debug.LogWarning("[AstroCrawler] GameDatabase nao encontrado em Resources/" +
                             GameDatabase.ResourcePath + "; carregando as tabelas JSON. " +
                             "Use Tools > AstroCrawler > Gerar ScriptableObjects.");
            return LoadFromJson();
        }

        /// <summary>The pre-ScriptableObject loader, kept for migration.</summary>
        public static GameConfig LoadFromJson()
        {
            var config = new GameConfig();
            config.cards = LoadTable<CardDefinition>("cards");
            config.ships = LoadTable<ShipDefinition>("ships");
            config.enemies = LoadTable<EnemyDefinition>("enemies");
            config.waves = LoadTable<WaveDefinition>("waves");
            config.waves.Sort((a, b) => a.index.CompareTo(b.index));

            // A single object, not a table; optional so older installs still load.
            var progressionJson = Resources.Load<TextAsset>(ResourceFolder + "/progression");
            if (progressionJson != null)
            {
                config.progression = Deserialize<ProgressionDefinition>(progressionJson.text) ??
                    new ProgressionDefinition();
            }
            return config;
        }

        private static List<T> LoadTable<T>(string file) where T : class
        {
            var path = ResourceFolder + "/" + file;
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError("[AstroCrawler] Missing configuration file Resources/" + path + ".json");
                return new List<T>();
            }
            return Deserialize<List<T>>(asset.text) ?? new List<T>();
        }

        private static T Deserialize<T>(string json) where T : class
        {
            var data = fsJsonParser.Parse(json);
            object deserialized = null;
            var result = serializer.TryDeserialize(data, typeof(T), ref deserialized);
            if (result.Failed)
            {
                Debug.LogError("[AstroCrawler] Failed to parse configuration: " + result.FormattedMessages);
                return null;
            }
            return deserialized as T;
        }

        // ------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------

        public CardDefinition GetCard(string id)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].id == id)
                {
                    return cards[i];
                }
            }
            return null;
        }

        public ShipDefinition GetShip(string id)
        {
            for (var i = 0; i < ships.Count; i++)
            {
                if (ships[i].id == id)
                {
                    return ships[i];
                }
            }
            return null;
        }

        public EnemyDefinition GetEnemy(EnemyKind kind)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].kind == kind)
                {
                    return enemies[i];
                }
            }
            return null;
        }

        public WaveDefinition GetWave(int index)
        {
            for (var i = 0; i < waves.Count; i++)
            {
                if (waves[i].index == index)
                {
                    return waves[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The distinct card types a ship starts with, in deck order. Used by the
        /// ship selection screen to preview a ship's identity.
        /// </summary>
        public List<CardDefinition> CardsInDeck(ShipDefinition ship)
        {
            var result = new List<CardDefinition>();
            if (ship == null)
            {
                return result;
            }
            for (var i = 0; i < ship.deck.Count; i++)
            {
                var card = GetCard(ship.deck[i].cardId);
                if (card != null)
                {
                    result.Add(card);
                }
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns every structural problem found in the data. Surfaced by the
        /// editor window and logged at boot.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (cards.Count == 0) problems.Add("No cards defined.");
            if (ships.Count == 0) problems.Add("No ships defined.");
            if (enemies.Count == 0) problems.Add("No enemies defined.");
            if (waves.Count == 0) problems.Add("No waves defined.");

            var seen = new HashSet<string>();
            foreach (var card in cards)
            {
                if (string.IsNullOrEmpty(card.id))
                {
                    problems.Add("A card has an empty id.");
                }
                else if (!seen.Add(card.id))
                {
                    problems.Add("Duplicate card id: " + card.id);
                }
                if (string.IsNullOrEmpty(card.evolvedName))
                {
                    problems.Add("Card " + card.id + " has no evolved name.");
                }
                if (string.IsNullOrEmpty(card.maxName) || string.IsNullOrEmpty(card.maxDescription))
                {
                    problems.Add("Card " + card.id + " has no level 3 name or description.");
                }
                if (card.maxParams == null || card.maxParams.entries.Count == 0)
                {
                    problems.Add("Card " + card.id + " has no level 3 parameters.");
                }
            }

            var commonIds = new HashSet<string>();
            var commonEffects = new HashSet<CardEffectId>();
            var exclusiveEffects = new HashSet<CardEffectId>();
            foreach (var card in cards)
            {
                if (card.category == CardCategory.Common) commonIds.Add(card.id);
                if (card.category == CardCategory.Common) commonEffects.Add(card.effect);
                else if (!exclusiveEffects.Add(card.effect))
                    problems.Add("Exclusive card effect must not repeat: " + card.effect);
                if (card.effect == CardEffectId.SpecialCooldown)
                    for (var level = 1; level <= CardSystem.MaxUpgradeLevel; level++)
                        if (card.Param(level, "reduction") <= 0f || card.Param(level, "reduction") > 0.5f)
                            problems.Add("Card " + card.id + " has an invalid special cooldown reduction.");
                if (card.effect == CardEffectId.ImpactCharge)
                    for (var level = 1; level <= CardSystem.MaxUpgradeLevel; level++)
                        if (card.Param(level, "missiles") != 4 + level * 2 || card.Param(level, "damage") <= 0f)
                            problems.Add("Card " + card.id + " has invalid healing volley parameters at level " + level);
                if (card.effect == CardEffectId.ArmorPlating || card.effect == CardEffectId.BossHunter)
                    for (var level = 1; level <= CardSystem.MaxUpgradeLevel; level++)
                    {
                        var key = card.effect == CardEffectId.ArmorPlating ? "damageReduction" : "bossDamageBonus";
                        var value = card.Param(level, key);
                        var limit = card.effect == CardEffectId.ArmorPlating ? 0.5f : 1f;
                        if (value <= 0f || value > limit)
                            problems.Add("Card " + card.id + " has an invalid " + key + " at level " + level);
                    }
                if (card.effect == CardEffectId.ChainLightning || card.effect == CardEffectId.SiegeRound)
                    for (var level = 1; level <= CardSystem.MaxUpgradeLevel; level++)
                    {
                        var valid = card.Param(level, "cooldown") > 0f;
                        if (card.effect == CardEffectId.ChainLightning)
                            valid &= card.Param(level, "damage") > 0f && card.Param(level, "jumpRadius") > 0f &&
                                card.ParamInt(level, "jumps") >= 1 && card.ParamInt(level, "jumps") <= 8;
                        else valid &= card.Param(level, "explosionDamage") > 0f && card.Param(level, "explosionRadius") > 0f;
                        if (!valid) problems.Add("Card " + card.id + " has invalid artillery parameters at level " + level);
                    }
            }
            if (exclusiveEffects.Overlaps(commonEffects))
                problems.Add("Exclusive card effects must not repeat common card effects.");
            if (commonIds.Count < 6) problems.Add("The base deck needs at least six common cards.");
            var starters = 0;
            foreach (var card in cards)
            {
                if (card.category == CardCategory.Common && card.starter) starters++;
                if (card.category == CardCategory.Common && card.unlockLevel != 0)
                    problems.Add("Base deck card " + card.id + " must not have an unlock level (it is bought).");
                if (card.category != CardCategory.Common && card.starter)
                    problems.Add("Exclusive card " + card.id + " cannot be a base deck starter.");
                if (card.unlockLevel < 0 || card.unlockLevel > PassiveUpgrades.MaxLevel * 8)
                    problems.Add("Card " + card.id + " has an unlock level outside 0-160.");
            }
            if (starters < 3) problems.Add("The base deck needs at least three starter cards.");
            var exclusiveOwners = new HashSet<string>();
            var specialKinds = new HashSet<ShipSpecialKind>();
            foreach (var ship in ships)
            {
                var special = ship.special;
                if (special != null && !specialKinds.Add(special.kind))
                    problems.Add("Ship " + ship.id + " special must be unique.");
                if (special == null || special.kind == ShipSpecialKind.None || string.IsNullOrEmpty(special.name) ||
                    string.IsNullOrEmpty(special.description) || special.duration < 0f || special.cooldown <= 0f ||
                    (special.kind != ShipSpecialKind.Repair && special.duration <= 0f) || special.cooldown < special.duration ||
                    special.fireRateScale < 1f || special.damageScale < 1f || special.healFraction < 0f || special.healFraction > 1f)
                    problems.Add("Ship " + ship.id + " has an invalid special ability.");
                if (special != null && ((special.kind != ShipSpecialKind.Overdrive && special.fireRateScale != 1f) ||
                    (special.kind != ShipSpecialKind.Amplify && special.damageScale != 1f) ||
                    (special.kind != ShipSpecialKind.Repair && special.healFraction != 0f)))
                    problems.Add("Ship " + ship.id + " special must have only one effect.");
                var deckIds = new HashSet<string>();
                var exclusiveCount = 0;
                var openAtStart = 0;
                foreach (var entry in ship.deck)
                {
                    var deckCard = GetCard(entry.cardId);
                    if (deckCard != null && deckCard.category == CardCategory.Common)
                        problems.Add("Ship " + ship.id + " lists base deck card " + deckCard.id + "; the base deck is shared.");
                    if (deckCard != null && deckCard.category != CardCategory.Common)
                    {
                        exclusiveCount++;
                        if (deckCard.unlockLevel == 0) openAtStart++;
                        if (!exclusiveOwners.Add(deckCard.id))
                            problems.Add("Exclusive card " + deckCard.id + " is shared between ships.");
                        CardCategory shipCategory;
                        if (!Enum.TryParse(ship.id, true, out shipCategory) || deckCard.category != shipCategory)
                            problems.Add("Ship " + ship.id + " has a card from another ship category.");
                        if (!string.Equals(deckCard.tint, ship.tint, StringComparison.OrdinalIgnoreCase))
                            problems.Add("Exclusive card " + deckCard.id + " must use its ship colour.");
                    }
                    if (GetCard(entry.cardId) == null)
                    {
                        problems.Add("Ship " + ship.id + " references unknown card " + entry.cardId + ".");
                    }
                    if (!deckIds.Add(entry.cardId))
                    {
                        problems.Add("Ship " + ship.id + " has duplicate card " + entry.cardId + ".");
                    }
                }
                if (exclusiveCount != 6 || openAtStart != 3)
                    problems.Add("Ship " + ship.id + " must list six exclusive cards, three of them open from the start.");
            }

            foreach (var wave in waves)
            {
                foreach (var entry in wave.entries)
                {
                    if (GetEnemy(entry.kind) == null)
                    {
                        problems.Add("Wave " + wave.index + " references undefined enemy " + entry.kind + ".");
                    }
                }
                if (wave.entries.Count == 0)
                {
                    problems.Add("Wave " + wave.index + " is empty.");
                }
            }

            foreach (var enemy in enemies)
            {
                if (enemy.xp < 0)
                {
                    problems.Add("Enemy " + enemy.kind + " has negative xp.");
                }
            }

            if (progression == null)
            {
                problems.Add("No progression defined.");
            }
            else
            {
                if (progression.xpBase < 1) problems.Add("Progression xpBase must be at least 1.");
                if (progression.xpPerLevel < 0) problems.Add("Progression xpPerLevel is negative.");
                if (progression.maxLevel < 0) problems.Add("Progression maxLevel is negative.");
                if (progression.bossLevelDrops < 0) problems.Add("Progression bossLevelDrops is negative.");
                if (progression.orbCollectRadius <= 0f) problems.Add("Progression orbCollectRadius must be positive.");
                if (progression.orbSpeed <= 0f && progression.orbAcceleration <= 0f)
                {
                    problems.Add("Progression orbs would never reach the ship (speed and acceleration are zero).");
                }
            }

            foreach (var card in cards)
            {
                if (!EffectRunner.IsRecurring(card.effect))
                {
                    continue;
                }
                for (var level = 1; level <= CardSystem.MaxUpgradeLevel; level++)
                {
                    if (card.Param(level, EffectRunner.CooldownKey, card.Param(level, "fireInterval")) <= 0f)
                    {
                        problems.Add("Card " + card.id + " is recurring but has no positive cooldown at level " + level + ".");
                    }
                }
            }

            for (var i = 1; i <= RunController.WavesPerRun; i++)
            {
                if (GetWave(i) == null)
                {
                    problems.Add("Wave " + i + " is missing.");
                }
            }

            return problems;
        }

        // ------------------------------------------------------------------
        // Editor-side persistence
        // ------------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Reads the tables from a folder of loose .json files (editor only).
        /// </summary>
        public static GameConfig LoadFromFolder(string folder)
        {
            var config = new GameConfig();
            config.cards = ReadFile<CardDefinition>(folder, "cards");
            config.ships = ReadFile<ShipDefinition>(folder, "ships");
            config.enemies = ReadFile<EnemyDefinition>(folder, "enemies");
            config.waves = ReadFile<WaveDefinition>(folder, "waves");
            config.waves.Sort((a, b) => a.index.CompareTo(b.index));

            var progressionPath = Path.Combine(folder, "progression.json");
            if (File.Exists(progressionPath))
            {
                config.progression = Deserialize<ProgressionDefinition>(File.ReadAllText(progressionPath)) ??
                    new ProgressionDefinition();
            }
            return config;
        }

        private static List<T> ReadFile<T>(string folder, string file) where T : class
        {
            var path = Path.Combine(folder, file + ".json");
            if (!File.Exists(path))
            {
                return new List<T>();
            }
            return Deserialize<List<T>>(File.ReadAllText(path)) ?? new List<T>();
        }

        /// <summary>
        /// Writes the tables back as pretty-printed JSON (editor only).
        /// </summary>
        public void SaveToFolder(string folder)
        {
            Directory.CreateDirectory(folder);
            WriteFile(folder, "cards", cards);
            WriteFile(folder, "ships", ships);
            WriteFile(folder, "enemies", enemies);
            WriteFile(folder, "waves", waves);
            WriteFile(folder, "progression", progression);
            AssetDatabase.Refresh();
        }

        private static void WriteFile<T>(string folder, string file, T data) where T : class
        {
            fsData serialized;
            serializer.TrySerialize(data, out serialized);
            File.WriteAllText(Path.Combine(folder, file + ".json"), fsJsonPrinter.PrettyJson(serialized));
        }
#endif
    }
}
