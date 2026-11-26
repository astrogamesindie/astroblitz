// AstroCrawler - designer-facing data definitions.
//
// Reuses the definition/runtime split that CCG Kit applies to cards: a plain
// serializable "library" object edited by designers (Card / CardType there,
// CardDefinition here) and a lightweight runtime instance that points back at
// it (RuntimeCard in both). Everything here is loaded from JSON at startup, so
// balancing never requires a code change (GDD 9).

using System;
using System.Collections.Generic;

namespace AstroCrawler
{
    /// <summary>
    /// A named numeric parameter. Effects read their tuning values through this
    /// bag instead of hard-coded fields so a designer can add a knob to the JSON
    /// without touching the effect code.
    /// </summary>
    [Serializable]
    public class ParamEntry
    {
        public string key;
        public float value;
    }

    /// <summary>
    /// An ordered set of named numeric parameters.
    /// </summary>
    [Serializable]
    public class ParamSet
    {
        public List<ParamEntry> entries = new List<ParamEntry>();

        public float Get(string key, float fallback = 0f)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    return entries[i].value;
                }
            }
            return fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            return UnityEngine.Mathf.RoundToInt(Get(key, fallback));
        }

        public void Set(string key, float value)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    entries[i].value = value;
                    return;
                }
            }
            entries.Add(new ParamEntry { key = key, value = value });
        }
    }

    /// <summary>
    /// A card as authored by the designer. Both the base and the evolved form of
    /// a card live in the same definition, because evolving never swaps a card
    /// for a different one: it flips a run-wide flag and every copy of that type,
    /// including the ones still in the draw pile, upgrades with it (GDD 3).
    /// </summary>
    [Serializable]
    public class CardDefinition
    {
        /// <summary>Stable slug used by deck lists, e.g. "double_attack".</summary>
        public string id;

        public string name;
        public string description;
        public string evolvedName;
        public string evolvedDescription;

        /// <summary>Level 3, the maximum form.</summary>
        public string maxName;
        public string maxDescription;

        public CardCategory category;
        public CardEffectId effect;

        /// <summary>Hex colour (RRGGBB) used for the card frame tint in the UI.</summary>
        public string tint = "3A6EA5";

        /// <summary>
        /// Base deck (Common) only: unlocked from the first launch. The other
        /// common cards are bought in the Deck base section (CardUnlocks).
        /// </summary>
        public bool starter;

        /// <summary>
        /// Exclusive cards only: the ship's upgrade level (levels bought in
        /// Upgrades, 0 to 160) that unlocks the card. 0 = from the start.
        /// </summary>
        public int unlockLevel;

        public ParamSet baseParams = new ParamSet();
        public ParamSet evolvedParams = new ParamSet();
        public ParamSet maxParams = new ParamSet();

        /// <summary>Name at an upgrade level: 1 base, 2 evolved, 3 maximum (0 reads as base).</summary>
        public string NameFor(int level)
        {
            if (level >= 3 && !string.IsNullOrEmpty(maxName)) return maxName;
            if (level >= 2 && !string.IsNullOrEmpty(evolvedName)) return evolvedName;
            return name;
        }

        public string DescriptionFor(int level)
        {
            if (level >= 3 && !string.IsNullOrEmpty(maxDescription)) return maxDescription;
            if (level >= 2 && !string.IsNullOrEmpty(evolvedDescription)) return evolvedDescription;
            return description;
        }

        /// <summary>
        /// Parameters for the requested level. Each level falls back to the one
        /// below for any key the designer did not override.
        /// </summary>
        public float Param(int level, string key, float fallback = 0f)
        {
            var evolved = evolvedParams.Get(key, baseParams.Get(key, fallback));
            return level >= 3 ? maxParams.Get(key, evolved) : level == 2 ? evolved : baseParams.Get(key, fallback);
        }

        public int ParamInt(int level, string key, int fallback = 0)
        {
            return UnityEngine.Mathf.RoundToInt(Param(level, key, fallback));
        }
    }

    /// <summary>
    /// One exclusive card in a ship's deck, keyed by card id. The shared cards
    /// are not listed here: every Common card is the base deck.
    /// </summary>
    [Serializable]
    public class DeckEntry
    {
        public string cardId;
    }

    /// <summary>
    /// A playable ship: its combat statline and unique upgrade cards.
    /// </summary>
    public enum ShipSpecialKind { None, Overdrive, Aegis, Stasis, Repair, Amplify, ChristmasStar }

    [Serializable]
    public class ShipSpecialDefinition
    {
        public ShipSpecialKind kind;
        public string name, nameEn, nameEs;
        public string description, descriptionEn, descriptionEs;
        public float cooldown = 30f;
        public float duration = 5f;
        public float damageScale = 1f;
        public float fireRateScale = 1f;
        public float healFraction;
    }

    [Serializable]
    public class ShipDefinition
    {
        public string id;
        public string name;
        public string tagline;
        public string identity;

        public ShipSpecialDefinition special = new ShipSpecialDefinition();

        public int maxHp = 100;

        /// <summary>Seconds between shots.</summary>
        public float fireInterval = 0.2f;

        public float damagePerShot = 7f;
        public int baseProjectiles = 1;

        /// <summary>Hex colour (RRGGBB) used for the hull and UI accent.</summary>
        public string tint = "5FD3F3";

        public List<DeckEntry> deck = new List<DeckEntry>();

        public int DeckSize()
        {
            return deck.Count;
        }
    }

    /// <summary>
    /// An enemy archetype (GDD 5). Meteors fragment when killed by weapon fire;
    /// <see cref="fragmentInto"/> plus <see cref="fragmentCount"/> encode that.
    /// </summary>
    [Serializable]
    public class EnemyDefinition
    {
        public EnemyKind kind;
        public string name;

        public float maxHp = 1f;

        /// <summary>
        /// XP dropped when weapon or ability damage kills it. Fragments carry
        /// their own, so a large meteor is worth the whole chain.
        /// </summary>
        public int xp = 1;

        public float collisionDamage = 5f;
        public float speed = 1.2f;
        public float radius = 0.35f;

        public MoveBehaviour behaviour = MoveBehaviour.Straight;

        /// <summary>Hex colour (RRGGBB) of the enemy sprite.</summary>
        public string tint = "B9A48B";

        /// <summary>Number of sprite corners. 0 renders a circle.</summary>
        public int shapeSides = 7;

        /// <summary>Enemy spawned when this one is destroyed by weapon fire.</summary>
        public EnemyKind fragmentInto = EnemyKind.MeteorSmall;
        public int fragmentCount;

        /// <summary>Boss only: seconds between reinforcement summons.</summary>
        public float summonInterval;
        public int summonCount;
    }

    /// <summary>
    /// How XP turns into levels during a run, and how the orbs that carry it
    /// behave. One per game; see ProgressionAsset.
    /// </summary>
    [Serializable]
    public class ProgressionDefinition
    {
        /// <summary>XP from level 1 to level 2.</summary>
        public int xpBase = 10;

        /// <summary>Extra XP each further level costs.</summary>
        public int xpPerLevel = 17;

        /// <summary>
        /// The ship stops gaining XP here. The curve is tuned so the hardest
        /// stage arrives at about this level on its own; the cap makes sure
        /// boss reinforcements cannot be farmed past it. 0 means no cap.
        /// </summary>
        public int maxLevel = 12;

        /// <summary>
        /// Times the boss drops a full level of XP as it loses health, evenly
        /// spaced, so the last wave has upgrade picks in the middle of the fight.
        /// </summary>
        public int bossLevelDrops = 3;

        /// <summary>Orbs each boss drop is split into.</summary>
        public int bossDropOrbs = 6;

        /// <summary>Seconds of combat time between a cleared wave and the next.</summary>
        public float waveBreak = 1f;

        /// <summary>World units per second an orb leaves the kill at.</summary>
        public float orbScatterSpeed = 2.2f;

        /// <summary>Seconds an orb drifts before it homes on the ship.</summary>
        public float orbMagnetDelay = 0.35f;

        public float orbSpeed = 5f;
        public float orbAcceleration = 16f;

        /// <summary>Distance from the ship's centre at which an orb counts.</summary>
        public float orbCollectRadius = 0.5f;

        /// <summary>Past this many orbs in flight, XP is credited without one.</summary>
        public int maxActiveOrbs = 80;

        /// <summary>XP needed to leave <paramref name="level"/> (1-based).</summary>
        public int XpForLevel(int level)
        {
            return Math.Max(1, xpBase + xpPerLevel * Math.Max(0, level - 1));
        }
    }

    /// <summary>
    /// N enemies of one kind inside a wave.
    /// </summary>
    [Serializable]
    public class WaveEntry
    {
        public EnemyKind kind;
        public int count = 1;
    }

    /// <summary>
    /// One of the 10 active base waves (GDD 7). The other campaign stages derive their
    /// rosters from these definitions; ship choice does not change a wave.
    /// </summary>
    [Serializable]
    public class WaveDefinition
    {
        public int index;
        public string label;
        public string designNote;

        /// <summary>Seconds between spawns inside the wave.</summary>
        public float spawnInterval = 0.35f;

        /// <summary>True for the final-wave boss fight, which ends on the core's death.</summary>
        public bool isBossWave;

        public List<WaveEntry> entries = new List<WaveEntry>();

        public int TotalEnemies()
        {
            var total = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                total += entries[i].count;
            }
            return total;
        }
    }
}
