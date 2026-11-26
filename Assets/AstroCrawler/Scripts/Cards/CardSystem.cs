// AstroCrawler - permanent upgrade selection, one pick per ship level.

using System;
using System.Collections.Generic;

namespace AstroCrawler
{
    /// <summary>
    /// Builds three upgrade choices for every level gained and tracks the permanent
    /// upgrades acquired during the run.
    /// Pure logic: no MonoBehaviour, no Unity API, so it can be unit tested.
    /// </summary>
    public class CardSystem
    {
        public const int ChoiceCount = 3;
        /// <summary>
        /// Base form, evolved form, then the maximum form. Every effect reads
        /// its parameters per level (CardDefinition.Param), and each level
        /// changes the card, so none of the three is a wasted pick.
        /// </summary>
        public const int MaxUpgradeLevel = 3;

        private readonly List<CardDefinition> pool = new List<CardDefinition>();
        private readonly List<RuntimeCard> choices = new List<RuntimeCard>();
        private readonly Dictionary<string, int> acquired = new Dictionary<string, int>();

        private int nextInstanceId;

        public event Action<CardDefinition> onUpgradeAcquired;

        public int acquiredCount
        {
            get
            {
                var total = 0;
                foreach (var pair in acquired) total += pair.Value;
                return total;
            }
        }

        public IReadOnlyList<RuntimeCard> Choices
        {
            get { return choices; }
        }

        /// <summary>
        /// Builds the upgrade pool: the base deck (every Common card) plus the
        /// ship's exclusive cards, keeping only those <paramref name="isUnlocked"/>
        /// accepts (null accepts everything).
        /// </summary>
        public void StartRun(ShipDefinition ship, GameConfig config, Func<CardDefinition, bool> isUnlocked = null)
        {
            pool.Clear();
            choices.Clear();
            acquired.Clear();
            nextInstanceId = 0;
            var seen = new HashSet<string>();

            for (var i = 0; i < config.cards.Count; i++)
            {
                var definition = config.cards[i];
                if (definition.category != CardCategory.Common) continue;
                if (isUnlocked != null && !isUnlocked(definition)) continue;
                if (seen.Add(definition.id)) pool.Add(definition);
            }

            for (var i = 0; i < ship.deck.Count; i++)
            {
                var definition = config.GetCard(ship.deck[i].cardId);
                if (definition == null || definition.category == CardCategory.Common) continue;
                if (isUnlocked != null && !isUnlocked(definition)) continue;
                if (seen.Add(definition.id)) pool.Add(definition);
            }
        }

        /// <summary>Cards that can come up in this run.</summary>
        public int PoolCount
        {
            get { return pool.Count; }
        }

        public bool IsEvolved(string cardId)
        {
            return UpgradeLevel(cardId) > 1;
        }

        public int UpgradeLevel(string cardId)
        {
            int level;
            return acquired.TryGetValue(cardId, out level) ? level : 0;
        }

        public void PrepareChoices()
        {
            choices.Clear();
            var shuffled = new List<CardDefinition>();
            for (var i = 0; i < pool.Count; i++)
            {
                if (UpgradeLevel(pool[i].id) < MaxUpgradeLevel)
                {
                    shuffled.Add(pool[i]);
                }
            }
            shuffled.Shuffle();
            for (var i = 0; i < shuffled.Count && i < ChoiceCount; i++)
            {
                choices.Add(new RuntimeCard(nextInstanceId++, shuffled[i], this));
            }
        }

        public bool Acquire(RuntimeCard chosen)
        {
            if (chosen == null || !choices.Contains(chosen))
            {
                return false;
            }

            var id = chosen.definition.id;
            if (UpgradeLevel(id) >= MaxUpgradeLevel)
            {
                return false;
            }

            acquired[id] = UpgradeLevel(id) + 1;
            choices.Clear();
            if (onUpgradeAcquired != null) onUpgradeAcquired(chosen.definition);
            return true;
        }
    }
}
