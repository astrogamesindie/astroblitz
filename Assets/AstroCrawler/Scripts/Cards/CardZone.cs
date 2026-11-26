// AstroCrawler - card zones and runtime card instances.
//
// Adapted from CCG Kit's RuntimeZone / RuntimeCard. The kit's version carried
// per-card stats, keywords and an owning networked player; none of that applies
// here, so what survives is the useful part: a named pile of cards that raises
// events when it changes, which the HUD subscribes to.

using System;
using System.Collections.Generic;

namespace AstroCrawler
{
    /// <summary>
    /// One physical copy of a card inside a run. The evolved/base form is not
    /// stored per copy: it is a property of the card *type* for the rest of the
    /// run (GDD 3), so it is resolved through the owning <see cref="CardSystem"/>.
    /// </summary>
    public class RuntimeCard
    {
        public readonly int instanceId;
        public readonly CardDefinition definition;

        private readonly CardSystem owner;

        public RuntimeCard(int instanceId, CardDefinition definition, CardSystem owner)
        {
            this.instanceId = instanceId;
            this.definition = definition;
            this.owner = owner;
        }

        /// <summary>Upgrade level acquired in this run (0 when none, or without an owner).</summary>
        public int level
        {
            get { return owner != null ? owner.UpgradeLevel(definition.id) : 0; }
        }

        public bool isEvolved
        {
            get { return level > 1; }
        }

        public string displayName
        {
            get { return definition.NameFor(level); }
        }

        public string displayDescription
        {
            get { return definition.DescriptionFor(level); }
        }

        /// <summary>Reads a tuning value at the card's current level.</summary>
        public float Param(string key, float fallback = 0f)
        {
            return definition.Param(level, key, fallback);
        }

        public int ParamInt(string key, int fallback = 0)
        {
            return definition.ParamInt(level, key, fallback);
        }
    }

    /// <summary>
    /// A pile of cards (draw, hand or discard) that notifies observers on change.
    /// </summary>
    public class CardZone
    {
        public readonly string name;

        /// <summary>0 means unlimited.</summary>
        public readonly int maxCards;

        private readonly List<RuntimeCard> cards = new List<RuntimeCard>();

        public event Action<CardZone> onChanged;

        public CardZone(string name, int maxCards = 0)
        {
            this.name = name;
            this.maxCards = maxCards;
        }

        public int count
        {
            get { return cards.Count; }
        }

        public bool isFull
        {
            get { return maxCards > 0 && cards.Count >= maxCards; }
        }

        public IReadOnlyList<RuntimeCard> Cards
        {
            get { return cards; }
        }

        public RuntimeCard this[int index]
        {
            get { return cards[index]; }
        }

        public bool Add(RuntimeCard card)
        {
            if (card == null || isFull || cards.Contains(card))
            {
                return false;
            }
            cards.Add(card);
            Raise();
            return true;
        }

        public bool Remove(RuntimeCard card)
        {
            if (!cards.Remove(card))
            {
                return false;
            }
            Raise();
            return true;
        }

        /// <summary>Removes and returns the top card, or null when empty.</summary>
        public RuntimeCard DrawTop()
        {
            if (cards.Count == 0)
            {
                return null;
            }
            var card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            Raise();
            return card;
        }

        /// <summary>
        /// Moves every card out of this zone into <paramref name="target"/> and
        /// shuffles the target. Used to recycle the discard pile (GDD 4.5).
        /// </summary>
        public void MoveAllTo(CardZone target)
        {
            if (cards.Count == 0)
            {
                return;
            }
            for (var i = 0; i < cards.Count; i++)
            {
                target.cards.Add(cards[i]);
            }
            cards.Clear();
            target.Shuffle();
            Raise();
        }

        public void Shuffle()
        {
            cards.Shuffle();
            Raise();
        }

        public void Clear()
        {
            if (cards.Count == 0)
            {
                return;
            }
            cards.Clear();
            Raise();
        }

        public int IndexOf(RuntimeCard card)
        {
            return cards.IndexOf(card);
        }

        private void Raise()
        {
            if (onChanged != null)
            {
                onChanged(this);
            }
        }
    }
}
