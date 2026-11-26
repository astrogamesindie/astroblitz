// AstroCrawler - which cards a player can draw.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Card availability, saved in PlayerPrefs.
    ///
    /// Base deck (every Common card, shared by the ships): the starter cards are
    /// open from the first launch; the rest are bought one at a time in the
    /// Deck base section - each purchase opens a random locked card and costs
    /// more than the last: 150, 400, 650, 800, 950... (FirstCost, SecondCost,
    /// then BaseCost + CostStep x (purchases - 1)).
    ///
    /// Exclusive cards: open once the ship's upgrade level (levels bought in
    /// Upgrades, PassiveUpgrades.TotalLevel) reaches the card's unlockLevel.
    /// </summary>
    public sealed class CardUnlocks
    {
        public const int BaseCost = 500;
        public const int CostStep = 150;
        /// <summary>The first card is cheap so a new player has a deck evolution within the first runs.</summary>
        public const int FirstCost = 150;
        public const int SecondCost = 400;
        private const string Prefix = "AstroCrawler.BaseDeck.v1.";

        private readonly GameConfig config;
        private readonly PassiveUpgrades wallet;

        public CardUnlocks(GameConfig config, PassiveUpgrades wallet)
        {
            this.config = config;
            this.wallet = wallet;
        }

        /// <summary>Base deck cards in catalogue order.</summary>
        public List<CardDefinition> BaseDeck()
        {
            var result = new List<CardDefinition>();
            if (config == null) return result;
            for (var i = 0; i < config.cards.Count; i++)
                if (config.cards[i].category == CardCategory.Common) result.Add(config.cards[i]);
            return result;
        }

        public int Purchases
        {
            get { return Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "purchases", 0)); }
        }

        /// <summary>Price of the next base deck card.</summary>
        public int NextCost
        {
            get
            {
                var purchases = Purchases;
                if (purchases == 0) return FirstCost;
                if (purchases == 1) return SecondCost;
                return BaseCost + CostStep * (purchases - 1);
            }
        }

        public int LockedBaseCount()
        {
            var deck = BaseDeck();
            var count = 0;
            for (var i = 0; i < deck.Count; i++) if (!IsUnlocked(deck[i], null)) count++;
            return count;
        }

        /// <summary>
        /// Whether a card can come up in a run of <paramref name="shipId"/>.
        /// Base deck cards ignore the ship.
        /// </summary>
        public bool IsUnlocked(CardDefinition card, string shipId)
        {
            if (card == null) return false;
            if (card.category == CardCategory.Common)
                return card.starter || PlayerPrefs.GetInt(Prefix + "unlocked." + card.id, 0) == 1;
            if (card.unlockLevel <= 0) return true;
            return wallet != null && !string.IsNullOrEmpty(shipId) && wallet.TotalLevel(shipId) >= card.unlockLevel;
        }

        /// <summary>
        /// Buys one base deck card: a random locked one. Null when there is
        /// nothing left to unlock or not enough credits (nothing is spent).
        /// </summary>
        public CardDefinition BuyRandomBaseCard()
        {
            var locked = new List<CardDefinition>();
            var deck = BaseDeck();
            for (var i = 0; i < deck.Count; i++) if (!IsUnlocked(deck[i], null)) locked.Add(deck[i]);
            if (locked.Count == 0 || wallet == null || !wallet.Spend(NextCost)) return null;

            var card = locked[Random.Range(0, locked.Count)];
            PlayerPrefs.SetInt(Prefix + "unlocked." + card.id, 1);
            PlayerPrefs.SetInt(Prefix + "purchases", Purchases + 1);
            ProgressSnapshot.SaveLocal();
            return card;
        }

        /// <summary>
        /// Gives one random locked base deck card for free (the welcome login).
        /// It does not count as a purchase, so the shop price does not go up.
        /// Null when every base card is already unlocked.
        /// </summary>
        public CardDefinition GrantRandomBaseCard()
        {
            var locked = new List<CardDefinition>();
            var deck = BaseDeck();
            for (var i = 0; i < deck.Count; i++) if (!IsUnlocked(deck[i], null)) locked.Add(deck[i]);
            if (locked.Count == 0) return null;

            var card = locked[Random.Range(0, locked.Count)];
            PlayerPrefs.SetInt(Prefix + "unlocked." + card.id, 1);
            ProgressSnapshot.SaveLocal();
            return card;
        }
    }
}
