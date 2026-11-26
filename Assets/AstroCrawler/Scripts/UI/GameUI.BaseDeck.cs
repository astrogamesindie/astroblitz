// AstroCrawler - the Deck base section and locked cards.
//
// The base deck is every Common card: shared by all the ships, three open
// from the start, the rest bought here one at a time (a random locked card per
// purchase, CardUnlocks). The page is built at runtime the first time it opens.
// Locked cards keep their text readable under a dark veil with a padlock and
// what opens them; the ship deck viewer uses the same veil for the exclusive
// cards still waiting on the ship's upgrade level.

using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const float DeckCardWidth = 340f;
        private const float DeckCardHeight = 320f;

        private RectTransform baseDeckPage;
        private RectTransform baseDeckContent;
        private ScrollRect baseDeckScroll;
        private TMP_Text baseDeckSubtitle;
        private TMP_Text baseDeckCredits;
        private Button baseDeckBuy;
        private int baseDeckMessageStamp;

        private void BuildBaseDeckPage()
        {
            if (baseDeckPage != null) return;

            TMP_Text title;
            baseDeckPage = BuildMenuPage("BaseDeckPage", out title);

            baseDeckSubtitle = UIFactory.Label(baseDeckPage, "Subtitle", "", 30, BonusInk,
                TextAlignmentOptions.Center, true);
            // Under the credits chip (it ends ~136 below the top).
            UIFactory.Place(baseDeckSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -156f),
                new Vector2(UIFactory.Pick(1300f, 1000f), 44f));
            baseDeckSubtitle.textWrappingMode = TextWrappingModes.NoWrap;
            baseDeckSubtitle.enableAutoSizing = true;
            baseDeckSubtitle.fontSizeMin = 24f;
            baseDeckSubtitle.fontSizeMax = 30f;

            // Credits, top right: what the purchase below spends.
            var chip = UIFactory.Panel9(baseDeckPage, "Credits", UIFactory.PanelDeep);
            UIFactory.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(-32f, -32f),
                new Vector2(UIFactory.Pick(260f, 230f), UIFactory.Touch));
            HudFrame(chip, UIFactory.Evolved);
            var jewel = UIFactory.Rect(chip.rectTransform, "Icon", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            UIFactory.Place(jewel.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(50f, 50f));
            baseDeckCredits = UIFactory.Label(chip.rectTransform, "Value", "0", 44, UIFactory.Evolved,
                TextAlignmentOptions.Right, true);
            UIFactory.Place(baseDeckCredits.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                new Vector2(150f, 60f));

            var scrollRoot = UIFactory.NewRect(baseDeckPage, "DeckScroll");
            UIFactory.Stretch(scrollRoot);
            scrollRoot.offsetMin = new Vector2(24f, 200f);
            scrollRoot.offsetMax = new Vector2(-24f, -206f);
            baseDeckScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            baseDeckScroll.horizontal = false;
            baseDeckScroll.vertical = true;
            baseDeckScroll.movementType = ScrollRect.MovementType.Elastic;
            baseDeckScroll.scrollSensitivity = 35f;
            var viewport = UIFactory.NewRect(scrollRoot, "Viewport");
            UIFactory.Stretch(viewport);
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            baseDeckContent = UIFactory.NewRect(viewport, "Content");
            UIFactory.Place(baseDeckContent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 0f));
            baseDeckScroll.viewport = viewport;
            baseDeckScroll.content = baseDeckContent;

            // The purchase: one random locked card, priced on the button.
            baseDeckBuy = UIFactory.Button(baseDeckPage, "Buy", null, UIFactory.Primary, UIFactory.Ink,
                UIFactory.TextButton, Sound.None);
            var buy = baseDeckBuy.GetComponent<RectTransform>();
            StyleCutAction(baseDeckBuy, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"),
                UIFactory.ActionBorder, UIFactory.ActionFillStrong, false);
            UIFactory.Place(buy, new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(UIFactory.Pick(820f, 860f), 130f));
            UIFactory.DropShadow(baseDeckBuy.GetComponent<Image>(), 22f, 8f, 0.55f);
            var cardIcon = UIFactory.Rect(buy, "Icon", UIFactory.Ink, ArtLibrary.IconDeck);
            cardIcon.preserveAspect = true;
            UIFactory.Place(cardIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(64f, 64f));
            var caption = UIFactory.Label(buy, "Caption", "", 40, UIFactory.Ink, TextAlignmentOptions.Left, true);
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(1f, 1f);
            caption.rectTransform.offsetMin = new Vector2(112f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-230f, 0f);
            caption.characterSpacing = 3f;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 28f;
            caption.fontSizeMax = 40f;
            UIFactory.AddUnderlay(caption);
            var costJewel = UIFactory.Rect(buy, "CostIcon", UIFactory.Evolved, ArtLibrary.IconCredits);
            costJewel.preserveAspect = true;
            UIFactory.Place(costJewel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(48f, 48f));
            var cost = UIFactory.Label(buy, "Cost", "", 46, UIFactory.Evolved, TextAlignmentOptions.Right, true);
            UIFactory.Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(140f, 64f));
            UIFactory.AddUnderlay(cost);
            Rebind(baseDeckBuy, BuyBaseDeckCard);

            baseDeckPage.gameObject.SetActive(false);
        }

        /// <summary>The bought card's reveal (shared with the ship upgrade screen) is showing.</summary>
        private bool BaseDeckRevealOpen
        {
            get { return CardRevealOpen; }
        }

        private void OpenBaseDeckPage()
        {
            BuildBaseDeckPage();
            DismissCardReveal(false);
            RefreshBaseDeckPage();
            baseDeckScroll.verticalNormalizedPosition = 1f;
            baseDeckPage.gameObject.SetActive(true);
            baseDeckPage.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(baseDeckPage), 0.2f);
        }

        private void RefreshBaseDeckPage(string highlightId = null)
        {
            if (baseDeckPage == null) return;
            var unlocks = run.cardUnlocks;
            var deck = unlocks.BaseDeck();
            var locked = unlocks.LockedBaseCount();

            SetLabel(baseDeckPage, "Title", L("DECK BASE", "BASE DECK", "MAZO BASE"));
            ShowBaseDeckSummary();
            baseDeckCredits.text = run.passiveUpgrades.credits.ToString();

            var allOpen = locked == 0;
            var cost = unlocks.NextCost;
            var affordable = run.passiveUpgrades.credits >= cost;
            TintSettingsAction(baseDeckBuy, !allOpen && affordable);
            SetButtonLabel(baseDeckBuy, allOpen
                ? L("DECK COMPLETO", "DECK COMPLETE", "MAZO COMPLETO")
                : L("COMPRAR CARTA", "BUY CARD", "COMPRAR CARTA"));
            SetLabel(baseDeckBuy.transform, "Cost", allOpen ? "" : cost.ToString());
            var costIcon = baseDeckBuy.transform.Find("CostIcon");
            if (costIcon != null) costIcon.gameObject.SetActive(!allOpen);
            var costLabel = baseDeckBuy.transform.Find("Cost");
            if (costLabel != null)
                costLabel.GetComponent<TMP_Text>().color = affordable ? UIFactory.Evolved : UIFactory.Danger;
            UIFactory.Group(baseDeckBuy).alpha = allOpen ? 0.5f : 1f;

            // Unlocked first, then the locked ones, each in catalogue order.
            var ordered = new List<CardDefinition>();
            for (var pass = 0; pass < 2; pass++)
                for (var i = 0; i < deck.Count; i++)
                    if (unlocks.IsUnlocked(deck[i], null) == (pass == 0)) ordered.Add(deck[i]);

            var widgets = FillCardGrid(baseDeckContent, baseDeckPage, ordered, card =>
                unlocks.IsUnlocked(card, null) ? null : L("BLOQUEADA", "LOCKED", "BLOQUEADA"));
            for (var i = 0; i < widgets.Count; i++)
            {
                if (ordered[i].id != highlightId) continue;
                Juice.Punch(widgets[i].root, 0.12f, 0.4f);
            }
        }

        private void ShowBaseDeckSummary()
        {
            var unlocks = run.cardUnlocks;
            var total = unlocks.BaseDeck().Count;
            var open = total - unlocks.LockedBaseCount();
            baseDeckSubtitle.color = BonusInk;
            baseDeckSubtitle.text = L("CARTAS DE TODAS AS NAVES  ·  ", "SHARED BY EVERY SHIP  ·  ",
                "CARTAS DE TODAS LAS NAVES  ·  ") + open + "/" + total +
                L(" LIBERADAS", " UNLOCKED", " DESBLOQUEADAS");
        }

        private void BuyBaseDeckCard()
        {
            var unlocks = run.cardUnlocks;
            if (unlocks.LockedBaseCount() == 0)
            {
                Sfx.Play(Sound.Denied);
                SetBaseDeckMessage(L("Todas as cartas já estão liberadas.", "Every card is already unlocked.",
                    "Todas las cartas ya están desbloqueadas."), UIFactory.InkDim);
                Juice.Punch(baseDeckBuy.transform, 0.06f, 0.25f);
                return;
            }
            var cost = unlocks.NextCost;
            if (run.passiveUpgrades.credits < cost)
            {
                Sfx.Play(Sound.Denied);
                SetBaseDeckMessage(L("Faltam ", "Need ", "Faltan ") + (cost - run.passiveUpgrades.credits) +
                    L(" créditos  ·  termine runs para ganhar", " more credits  ·  finish runs to earn",
                        " créditos  ·  termina partidas para ganar"), UIFactory.Danger);
                Juice.Punch(baseDeckCredits.transform.parent, 0.12f, 0.3f);
                Juice.Punch(baseDeckBuy.transform, 0.06f, 0.25f);
                return;
            }

            var card = unlocks.BuyRandomBaseCard();
            if (card == null) return;
            Sfx.Play(Sound.Purchase);
            RefreshBaseDeckPage(card.id);
            RefreshMainMenu();
            ShowBaseDeckReveal(card);
        }

        /// <summary>The "new card" moment: the same charge-and-flip reveal as the ship's exclusive cards.</summary>
        private void ShowBaseDeckReveal(CardDefinition card)
        {
            ShowCardReveal(card, L("NOVA CARTA!", "NEW CARD!", "¡NUEVA CARTA!"),
                L("DECK BASE", "BASE DECK", "MAZO BASE"), UIFactory.Accent,
                L("Agora ela pode aparecer nas escolhas de todas as naves.", "It can now show up in every ship's picks.",
                    "Ahora puede aparecer en las elecciones de todas las naves."),
                null);
        }

        private void SetBaseDeckMessage(string message, Color color)
        {
            baseDeckSubtitle.text = message;
            baseDeckSubtitle.color = color;
            Juice.Punch(baseDeckSubtitle.transform, 0.08f, 0.25f);
            var stamp = ++baseDeckMessageStamp;
            LeanTween.delayedCall(baseDeckSubtitle.gameObject, 3f, () =>
            {
                if (baseDeckSubtitle != null && stamp == baseDeckMessageStamp) ShowBaseDeckSummary();
            }).setIgnoreTimeScale(true);
        }

        // ------------------------------------------------------------------
        // Shared by the base deck and the ship deck viewer
        // ------------------------------------------------------------------

        /// <summary>
        /// Lays cards out in a grid (three columns in landscape, two in portrait)
        /// inside a scroll content. <paramref name="lockLabel"/> returns null for
        /// an open card, or the text shown under the padlock.
        /// </summary>
        private List<CardWidget> FillCardGrid(RectTransform content, RectTransform page, List<CardDefinition> cards,
            System.Func<CardDefinition, string> lockLabel, Color? previewTint = null)
        {
            for (var i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            const float gap = 32f;
            const float padding = 16f;
            var columns = UIFactory.Pick(3, 2);
            var pageWidth = page.rect.width > 0f ? page.rect.width : UIFactory.ReferenceWidth;
            var contentWidth = Mathf.Max(1f, pageWidth - 64f);
            var scale = Mathf.Max(0.1f, Mathf.Min(1.75f,
                (contentWidth - padding * 2f - gap * (columns - 1)) / (DeckCardWidth * columns)));
            var columnStep = DeckCardWidth * scale + gap;
            var rowStep = DeckCardHeight * scale + gap;
            var rows = (cards.Count + columns - 1) / columns;
            content.sizeDelta = new Vector2(contentWidth,
                rows > 0 ? rows * DeckCardHeight * scale + (rows - 1) * gap + padding * 2f : 0f);

            var widgets = new List<CardWidget>(cards.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                var widget = CardWidget.Create(content, "DeckCard_" + cards[i].id, new Vector2(DeckCardWidth, DeckCardHeight));
                UIFactory.Place(widget.root, new Vector2(0.5f, 1f),
                    new Vector2((i % columns - (columns - 1) * 0.5f) * columnStep, -(i / columns * rowStep + padding)),
                    new Vector2(DeckCardWidth, DeckCardHeight));
                widget.root.pivot = new Vector2(0.5f, 1f);
                widget.SetRestScale(scale);
                widget.Bind(new RuntimeCard(-1, cards[i], null), 0, previewTint: previewTint);
                widget.interactable = false;
                var label = lockLabel != null ? lockLabel(cards[i]) : null;
                if (label != null) VeilLockedCard(widget, label);
                widgets.Add(widget);
            }
            return widgets;
        }

        /// <summary>
        /// A dark veil and, over the card's footer, a plate with a padlock and
        /// what opens the card. Name, text and icon stay hidden until unlocked.
        /// </summary>
        private static void VeilLockedCard(CardWidget widget, string requirement)
        {
            widget.ShowAsMystery();
            var veil = UIFactory.Rect(widget.root, "LockVeil", new Color(0.01f, 0.015f, 0.03f, 0.62f));
            UIFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = false;

            var plate = UIFactory.Rect(widget.root, "LockPlate", new Color(0.03f, 0.035f, 0.07f, 0.95f));
            plate.rectTransform.anchorMin = new Vector2(0f, 0f);
            plate.rectTransform.anchorMax = new Vector2(1f, 0f);
            plate.rectTransform.pivot = new Vector2(0.5f, 0f);
            plate.rectTransform.offsetMin = new Vector2(14f, 14f);
            plate.rectTransform.offsetMax = new Vector2(-14f, 66f);
            plate.raycastTarget = false;
            var lockIcon = UIFactory.Rect(plate.rectTransform, "Lock", UIFactory.Evolved, ArtLibrary.Art("ic_lock"));
            lockIcon.preserveAspect = true;
            UIFactory.Place(lockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(34f, 34f));
            lockIcon.raycastTarget = false;
            var text = UIFactory.Label(plate.rectTransform, "Text", requirement, 28, UIFactory.Evolved,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(52f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            text.characterSpacing = 4f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 24f;
            text.raycastTarget = false;
        }
    }
}
