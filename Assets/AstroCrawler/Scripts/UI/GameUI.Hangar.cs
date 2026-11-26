// AstroCrawler - ship select and the deck viewer.
//
// Scrollable ship cards the player compares at a glance: the hull in its colour,
// the name, a one-line role instead of a paragraph, and four bars scaled
// against the best ship in each stat. The chosen one is lit, bracketed and
// slightly larger; the others step back. The whole card is the tap target.
//
// The same screen doubles as the frame of the upgrade tree (Upgrades mode).

using System;
using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        /// <summary>The buttons of one baked ship card, looked up again by id in Bind.</summary>
        [Serializable]
        public class ShipButton
        {
            public string shipId;
            public Button button;
            public Button card;
            public Button deck;
            public Color accent = Color.white;
        }

        /// <summary>Glow strength of the chosen ship or stage card.</summary>
        private const float SelectedGlow = 0.9f;

        /// <summary>Alpha of a hangar card one step away from the middle of the carousel.</summary>
        private const float HangarNeighbourAlpha = 0.4f;

        /// <summary>Portrait: height of the carousel arrows, in the card's lower half (thumb zone).</summary>
        // Portrait hangar card sizing (BindHangarButtons): the card grows with the
        // screen, the neighbours keep a glimpse at the edges.
        private const float HangarCardWidthShare = 0.72f; // the arrows at the edges stay clear of the card
        private const float HangarCardMaxScale = 1.7f;
        private const float HangarCardMargin = 30f;
        private const float HangarSpareBelowShare = 0.35f;
        // At the old 1.25 scale; scaled with the card.
        private const float CarouselArrowYPortrait = -330f;

        /// <summary>Frame, bars and glow of the hangar cards: the interface's cyan, whatever the ship's colour.</summary>
        private static Color HangarFrame => UIFactory.Accent;

        private static readonly string[] StatRows = { "Hp", "Damage", "FireRate", "Dps" };

        [Header("Naves")]
        [SerializeField] private RectTransform shipScreen;
        [SerializeField] private CanvasGroup shipGroup;
        [SerializeField] private Button shipBackButton;
        [SerializeField] private List<RectTransform> shipPanels = new List<RectTransform>();
        [SerializeField] private List<ShipButton> shipButtons = new List<ShipButton>();
        [SerializeField] private List<TrueShadow> shipGlows = new List<TrueShadow>();
        [SerializeField] private List<RectTransform> shipBrackets = new List<RectTransform>();
        [SerializeField] private List<Image> shipBadges = new List<Image>();
        [SerializeField] private ScrollRect shipScroll;
        [SerializeField] private RectTransform shipContent;

        private bool shipScreenUpgradesMode;

        // Portrait carousel: a larger drawing scale, and room at both ends so
        // the first and last ships can sit in the middle like the others.
        private const float ShipCarouselScale = 1.25f;
        private static float ShipCarouselPad { get { return UIFactory.Pick(0f, 240f); } }

        private RectTransform deckScreen;
        private RectTransform deckContent;
        private ScrollRect deckScroll;
        private TMP_Text deckTitle;
        private TMP_Text deckSubtitle;

        // ------------------------------------------------------------------
        // Construction (baked)
        // ------------------------------------------------------------------

        private void BuildShipScreen(Transform parent)
        {
            var visibleShips = config.ships.FindAll(ship => ShipUnlocks.IsAvailableForAcquisition(ship.id));
            Image backdrop;
            shipScreen = UIFactory.Page(parent, "ShipScreen", new Color(0.02f, 0.03f, 0.06f, 0.97f), out backdrop);
            shipGroup = UIFactory.Group(shipScreen);

            shipBackButton = UIFactory.RoundButton(shipScreen, "Back", ArtLibrary.IconBack, UIFactory.PanelSoft,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(shipBackButton.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(32f, -32f), new Vector2(UIFactory.Touch, UIFactory.Touch));

            var title = UIFactory.Label(shipScreen, "Title", "", UIFactory.TextTitle, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            // Portrait: clear of the back button on both sides.
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f),
                new Vector2(UIFactory.Pick(1300f, 760f), 80f));
            title.characterSpacing = UIFactory.Pick(12f, 6f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 36f;
            title.fontSizeMax = UIFactory.TextTitle;
            UIFactory.AddUnderlay(title, 0.3f, 0.15f);

            var scrollRoot = UIFactory.NewRect(shipScreen, "ShipScroll");
            UIFactory.Stretch(scrollRoot);
            scrollRoot.offsetMin = new Vector2(20f, 0f);
            scrollRoot.offsetMax = new Vector2(-20f, -125f);
            shipScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            shipScroll.horizontal = true;
            shipScroll.vertical = false;
            shipScroll.movementType = ScrollRect.MovementType.Clamped;
            shipScroll.scrollSensitivity = 35f;
            // Portrait: the carousel is drawn larger, one card in the middle
            // with a glimpse of its neighbours.
            if (UIFactory.Portrait) scrollRoot.localScale = Vector3.one * ShipCarouselScale;
            var viewport = UIFactory.NewRect(scrollRoot, "Viewport");
            UIFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            shipContent = UIFactory.NewRect(viewport, "Content");
            shipContent.anchorMin = new Vector2(0f, 0.5f);
            shipContent.anchorMax = new Vector2(0f, 0.5f);
            shipContent.pivot = new Vector2(0f, 0.5f);
            shipContent.sizeDelta = new Vector2(visibleShips.Count * 580f + 40f + ShipCarouselPad * 2f, 950f);
            shipScroll.viewport = viewport;
            shipScroll.content = shipContent;
            for (var i = 0; i < visibleShips.Count; i++)
            {
                BuildShipCard(visibleShips[i], (i - (visibleShips.Count - 1) * 0.5f) * 580f);
            }

            // Drawn last: the portrait carousel is scaled up and reaches the top
            // corner, where it used to swallow taps meant for the back button.
            // An arrow on each side steps one ship; CarouselSnap (wired in
            // Bind) also centres the card a drag ends on.
            BuildCarouselArrows(shipScreen, "Ship", -85f);

            title.transform.SetAsLastSibling();
            shipBackButton.transform.SetAsLastSibling();

            shipScreen.gameObject.SetActive(false);
        }

        private void BuildShipCard(ShipDefinition ship, float x)
        {
            const float width = 540f;
            var accent = SpriteFactory.ParseColor(ship.tint);

            var panel = UIFactory.Panel9(shipContent, "Ship_" + ship.id, UIFactory.PanelSoft);
            var rect = panel.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(width, 900f));
            shipPanels.Add(rect);

            panel.raycastTarget = true;
            var card = panel.gameObject.AddComponent<Button>();
            card.targetGraphic = panel;
            card.transition = Selectable.Transition.None;
            var frameAccent = accent;
            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            if (!StyleCutAction(card, cutFrame, frameAccent, Darken(accent, 0.12f), false, KitRole.Card))
            {
                panel.pixelsPerUnitMultiplier = 0.2f;
                var inset = rect.Find("Inset").GetComponent<Image>();
                inset.pixelsPerUnitMultiplier = 0.2f;
                UIFactory.Stretch(inset.rectTransform, 8f);
            }

            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            shipGlows.Add(UIFactory.GlowShadow(panel, WithAlpha(accent, 0f), 44f, 0.12f));
            var brackets = UIFactory.Brackets(rect, accent);
            brackets.gameObject.SetActive(false);
            shipBrackets.Add(brackets);

            var stripe = UIFactory.Rect(rect, "Stripe", accent, ArtLibrary.Pixel);
            UIFactory.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(width - 100f, 5f));

            var hull = UIFactory.Rect(rect, "Hull", ArtLibrary.ShipTint(ship.id, accent), ArtLibrary.ShipFor(ship.id));
            hull.preserveAspect = true;
            UIFactory.Place(hull.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(124f, 124f));
            UIFactory.GlowShadow(hull, WithAlpha(accent, 0.6f), 22f, 0.05f);

            var name = UIFactory.Label(rect, "Name", "", UIFactory.TextHeading, accent, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(width - 30f, 58f));
            name.characterSpacing = 5f;
            name.enableAutoSizing = true;
            name.fontSizeMin = 34f;
            name.fontSizeMax = UIFactory.TextHeading;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.AddUnderlay(name);

            var role = UIFactory.Label(rect, "Role", "", UIFactory.TextBody, UIFactory.InkDim, TextAlignmentOptions.Center, true);
            UIFactory.Place(role.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(width - 30f, 40f));
            role.characterSpacing = 3f;
            role.enableAutoSizing = true;
            role.fontSizeMin = UIFactory.TextCaption;
            role.fontSizeMax = UIFactory.TextBody;
            role.textWrappingMode = TextWrappingModes.NoWrap;

            BuildStatBars(rect, accent, width - 60f);

            BuildShipSpecial(rect, ship, width - 40f);

            // The deck viewer is an icon, not a word.
            var deck = UIFactory.Button(rect, "ViewDeck", null, Darken(accent, 0.26f), UIFactory.Ink, 32, Sound.Navigate);
            UIFactory.Place(deck.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -656f),
                new Vector2(140f, 84f));
            StyleCutAction(deck, cutFrame, WithAlpha(frameAccent, 0.75f), Darken(accent, 0.18f), false, KitRole.Icone);
            var deckIcon = UIFactory.Rect(deck.transform, "Icon", UIFactory.Ink, ArtLibrary.IconDeck);
            deckIcon.preserveAspect = true;
            UIFactory.Place(deckIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));

            var band = UIFactory.Rect(rect, "ButtonBand", Darken(accent, 0.08f), cutFrame);
            band.type = Image.Type.Sliced;
            band.pixelsPerUnitMultiplier = 0.3f;
            UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(width - 28f, 140f));
            var bandAccent = UIFactory.Rect(band.rectTransform, "Accent", WithAlpha(accent, 0.45f), ArtLibrary.Pixel);
            UIFactory.Place(bandAccent.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width - 60f, 2f));

            // The card plays the select sound itself (Select or a plain tap,
            // depending on whether it changed anything), so the button is quiet.
            var select = UIFactory.Button(band.rectTransform, "Select", "", Darken(accent, 0.42f), UIFactory.Ink,
                UIFactory.TextButton, Sound.None);
            UIFactory.Place(select.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width - 70f, 108f));
            StyleCutAction(select, cutFrame, frameAccent, Darken(accent, 0.32f));
            UIFactory.DropShadow(select.GetComponent<Image>(), 16f, 5f, 0.5f);

            var badge = UIFactory.Pill(rect, "SelectedBadge", "", accent, new Color(0.03f, 0.04f, 0.07f), 52f, 24,
                ArtLibrary.IconCheck);
            UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(270f, 52f));
            shipBadges.Add(badge);

            // Recorded rather than wired: listeners would not survive the bake.
            shipButtons.Add(new ShipButton { shipId = ship.id, button = select, card = card, deck = deck, accent = accent });
        }

        /// <summary>
        /// The ship's power, set apart from the stats: a lit block with a round
        /// badge carrying the power's icon, its name, one line of what it does
        /// and the recharge as a clock icon and seconds.
        /// </summary>
        private static void BuildShipSpecial(RectTransform panel, ShipDefinition ship, float width)
        {
            var special = ship.special;
            var color = ShipAbilityButton.ColorFor(ship);

            var box = UIFactory.Panel9(panel, "Special", new Color(0.035f, 0.045f, 0.085f, 0.95f));
            var rect = box.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -500f), new Vector2(width, 146f));
            UIFactory.GlowShadow(box, WithAlpha(color, 0.35f), 22f, 0.05f);
            var brackets = UIFactory.Brackets(rect, WithAlpha(color, 0.9f), 20f, -3f);
            var corners = brackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < corners.Length; i++) corners[i].raycastTarget = false;

            // The badge looks like the ability button the player will press in a run.
            var badge = UIFactory.Rect(rect, "Badge", color, ArtLibrary.RoundFill);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(104f, 104f));
            UIFactory.GlowShadow(badge, WithAlpha(color, 0.6f), 20f, 0.06f);
            var ring = UIFactory.Rect(badge.rectTransform, "Ring", new Color(1f, 1f, 1f, 0.3f), ArtLibrary.RoundRing);
            UIFactory.Stretch(ring.rectTransform);
            var kind = special != null ? special.kind : ShipSpecialKind.None;
            var icon = UIFactory.Rect(badge.rectTransform, "Icon", UIFactory.Ink, ShipAbilityButton.IconFor(kind));
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            ShipAbilityButton.CenterIcon(icon, kind);

            var name = UIFactory.Label(rect, "Name", "", 30, color, TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(136f, -16f), new Vector2(width - 250f, 40f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 26f;
            name.fontSizeMax = 30f;
            UIFactory.AddUnderlay(name);

            var description = UIFactory.Label(rect, "Description", "", 28, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            UIFactory.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(136f, -60f),
                new Vector2(width - 152f, 74f));
            description.enableAutoSizing = true;
            description.fontSizeMin = 24f;
            description.fontSizeMax = 24f;
            description.lineSpacing = -4f;

            var clock = UIFactory.Rect(rect, "CooldownIcon", UIFactory.InkDim, ArtLibrary.IconRetry);
            clock.preserveAspect = true;
            UIFactory.Place(clock.rectTransform, new Vector2(1f, 1f), new Vector2(-74f, -20f), new Vector2(28f, 28f));
            var cooldown = UIFactory.Label(rect, "Cooldown", "", 28, UIFactory.InkDim, TextAlignmentOptions.TopRight, true);
            UIFactory.Place(cooldown.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -18f), new Vector2(60f, 32f));

            rect.gameObject.SetActive(kind != ShipSpecialKind.None);
        }

        private static void BuildStatBars(RectTransform panel, Color accent, float width)
        {
            var group = UIFactory.NewRect(panel, "StatBars");
            UIFactory.Place(group, new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(width, 4 * 52f));

            for (var row = 0; row < StatRows.Length; row++)
            {
                var line = UIFactory.NewRect(group, StatRows[row]);
                UIFactory.Place(line, new Vector2(0f, 1f), new Vector2(0f, -row * 52f), new Vector2(width, 48f));

                // An icon names the stat; the word stays only as an invisible
                // label (it is still set by SetStatRow) for nothing to break.
                var icon = UIFactory.Rect(line, "Icon", UIFactory.InkDim, StatIcon(row));
                icon.preserveAspect = true;
                UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(40f, 40f));

                var label = UIFactory.Label(line, "Label", "", UIFactory.TextCaption, UIFactory.InkDim,
                    TextAlignmentOptions.Left, true);
                UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(170f, 48f));
                label.gameObject.SetActive(false);

                Image back;
                var fill = UIFactory.Bar(line, "Bar", accent, new Color(0.02f, 0.03f, 0.06f, 0.9f), out back);
                UIFactory.Place((RectTransform)fill.rectTransform.parent, new Vector2(0f, 0.5f),
                    new Vector2(64f, 0f), new Vector2(width - 64f - 100f, 16f));

                var value = UIFactory.Label(line, "Value", "", 32, UIFactory.Ink, TextAlignmentOptions.Right, true);
                UIFactory.Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(94f, 48f));
            }
        }

        /// <summary>Hull, damage, fire rate and damage per second, as the upgrade tree draws them.</summary>
        private static Sprite StatIcon(int row)
        {
            switch (row)
            {
                case 0: return ArtLibrary.PassiveIcon(PassiveUpgrade.Hull);
                case 1: return ArtLibrary.PassiveIcon(PassiveUpgrade.Damage);
                case 2: return ArtLibrary.PassiveIcon(PassiveUpgrade.FireRate);
                default: return ArtLibrary.IconEnergy;
            }
        }

        private void ClearHangarReferences()
        {
            shipScreen = null;
            shipGroup = null;
            shipBackButton = null;
            shipPanels.Clear();
            shipButtons.Clear();
            shipGlows.Clear();
            shipBrackets.Clear();
            shipBadges.Clear();
            shipScroll = null;
            shipContent = null;
        }

        /// <summary>Round left and right arrows at mid height of a carousel.</summary>
        private static void BuildCarouselArrows(RectTransform page, string prefix, float y)
        {
            for (var side = 0; side < 2; side++)
            {
                var arrow = UIFactory.RoundButton(page, prefix + (side == 0 ? "Previous" : "Next"),
                    side == 0 ? ArtLibrary.IconBack : ArtLibrary.IconNext, UIFactory.PanelDeep, UIFactory.Touch, Sound.Navigate);
                UIFactory.Place(arrow.GetComponent<RectTransform>(), new Vector2(side, 0.5f),
                    new Vector2(side == 0 ? 16f : -16f, y), new Vector2(UIFactory.Touch, UIFactory.Touch));
                UIFactory.GlowShadow(arrow.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.3f), 20f, 0.05f);
                arrow.transform.Find("Ring").GetComponent<Image>().color = WithAlpha(UIFactory.Accent, 0.7f);
            }
        }

        private CarouselSnap shipSnap;

        private static Button ArrowAt(RectTransform page, string name)
        {
            var arrow = page != null ? page.Find(name) : null;
            return arrow != null ? arrow.GetComponent<Button>() : null;
        }

        /// <summary>
        /// Portrait: sizes the hangar carousel to the room the page has. Run on
        /// Bind and again whenever the banner footer changes the page height
        /// (the banner loads after Bind).
        /// </summary>
        private void FitHangarCard()
        {
            if (!UIFactory.Portrait) return;
            // The card used to be drawn at a fixed 1.25 and left an empty band
            // on tall phones. It now grows to the screen: as large as the
            // height allows, up to HangarCardWidthShare of the width (so the
            // neighbours still peek in), never below the old 1.25. What height
            // is left goes mostly above the card, the button nearer the thumb.
            // The carousel only moves x, so the content's y stays.
            var scale = ShipCarouselScale;
            var drop = 0f;
            var root = shipScroll != null ? shipScroll.transform as RectTransform : null;
            if (root != null && shipPanels.Count > 0 && shipPanels[0] != null)
            {
                var card = shipPanels[0].rect;
                var room = root.rect;
                if (card.height > 0f && room.height > 0f)
                {
                    var byHeight = (room.height - HangarCardMargin * 2f) / card.height;
                    var byWidth = room.width * HangarCardWidthShare / card.width;
                    scale = Mathf.Clamp(Mathf.Min(byHeight, byWidth), ShipCarouselScale, HangarCardMaxScale);
                    root.localScale = Vector3.one * scale;
                    var spare = Mathf.Max(0f, room.height - HangarCardMargin * 2f - card.height * scale);
                    drop = spare * HangarSpareBelowShare;
                    if (shipContent != null)
                        shipContent.anchoredPosition = new Vector2(shipContent.anchoredPosition.x, -drop / scale);
                }
            }
            foreach (var name in new[] { "ShipPrevious", "ShipNext" })
            {
                var arrow = shipScreen != null ? shipScreen.Find(name) as RectTransform : null;
                if (arrow != null) arrow.anchoredPosition = new Vector2(arrow.anchoredPosition.x,
                    CarouselArrowYPortrait * scale / ShipCarouselScale - drop);
            }
        }

        private void BindHangarButtons()
        {
            Rebind(shipBackButton, GoToMainMenu);
            if (shipScroll != null)
            {
                shipSnap = shipScroll.GetComponent<CarouselSnap>();
                if (shipSnap == null) shipSnap = shipScroll.gameObject.AddComponent<CarouselSnap>();
                shipSnap.Configure(shipScroll, ShipCarouselPad + 310f, 580f, shipButtons.Count,
                    ArrowAt(shipScreen, "ShipPrevious"), ArrowAt(shipScreen, "ShipNext"));
                // The neighbours peeking in at the edges fade, the middle card is lit.
                shipSnap.SetFocusFade(shipPanels, HangarNeighbourAlpha);
            }
            // Portrait: the arrows go down to the card's lower half, in reach of
            // the thumb (they used to sit at mid screen). Runtime, so the baked
            // scene does not need a rebake.
            FitHangarCard();
            for (var i = 0; i < shipButtons.Count; i++)
            {
                var entry = shipButtons[i];
                if (entry == null) continue;
                var shipId = entry.shipId;
                Rebind(entry.button, () => SelectShip(shipId));
                Rebind(entry.card, () => SelectShip(shipId));
                Rebind(entry.deck, () => ShowShipDeck(shipId));
            }
            BindShipStore();
        }

        // ------------------------------------------------------------------
        // Modes and selection
        // ------------------------------------------------------------------

        /// <summary>
        /// The screen serves two sections: the hangar (pick a ship) and the
        /// upgrade tree (spend credits on one). Heading and content switch together.
        /// </summary>
        private void SetShipScreenMode(bool upgrades)
        {
            // Anything added to the screen at runtime (the upgrade list, the ad
            // button) must not end up over the back button.
            if (shipBackButton != null) shipBackButton.transform.SetAsLastSibling();
            if (shipScroll != null) shipScroll.gameObject.SetActive(!upgrades);
            shipScreenUpgradesMode = upgrades;
            if (!upgrades) shipStore?.RefreshProducts();
            for (var i = 0; i < shipPanels.Count; i++)
                if (shipPanels[i] != null) shipPanels[i].gameObject.SetActive(!upgrades);
            if (passiveScreen != null) passiveScreen.gameObject.SetActive(upgrades);
            RefreshShipScreenHeading();
            RefreshAdControls();
            var arrows = !upgrades;
            var previousArrow = ArrowAt(shipScreen, "ShipPrevious");
            var nextArrow = ArrowAt(shipScreen, "ShipNext");
            if (previousArrow != null) previousArrow.gameObject.SetActive(arrows);
            if (nextArrow != null) nextArrow.gameObject.SetActive(arrows);
            if (!upgrades && shipSnap != null)
            {
                // Opens on the ship being flown.
                var index = shipButtons.FindIndex(entry => entry.shipId == selectedShipId);
                shipSnap.GlideTo(Mathf.Max(0, index), true);
            }
        }

        private void RefreshShipScreenHeading()
        {
            SetLabel(shipScreen, "Title", shipScreenUpgradesMode
                ? L("UPGRADES", "UPGRADES", "MEJORAS")
                : L("ESCOLHA SUA NAVE", "CHOOSE YOUR SHIP", "ELIGE TU NAVE"));
        }

        private void SelectShip(string shipId)
        {
            if (config.GetShip(shipId) == null) return;
            if (!run.shipUnlocks.IsUnlocked(shipId))
            {
                BuyShip(shipId);
                return;
            }
            var changed = selectedShipId != shipId;
            Sfx.Play(changed ? Sound.Select : Sound.Tap);

            selectedShipId = shipId;
            passiveShipId = shipId;
            // Each ship has its own progress: back to the stage it is working on.
            if (changed) run.SyncStageToShip(shipId);
            PlayerPrefs.SetString(SelectedShipKey, shipId);
            ProgressSnapshot.SaveLocal();
            RefreshSelectedShip();
            if (passiveScreen != null) RefreshPassiveScreen();

            if (!changed) return;
            for (var i = 0; i < shipButtons.Count && i < shipGlows.Count; i++)
            {
                if (shipButtons[i].shipId != shipId) continue;
                // Settle on the selected intensity, not the glow's current
                // colour: the fade towards it has only just started.
                var accent = HangarFrame;
                UIFactory.FlashGlow(shipGlows[i], WithAlpha(accent, 1f), WithAlpha(accent, SelectedGlow), 0.5f);
            }
        }

        /// <summary>A locked ship's button buys it: the fixed price, then it is selected.</summary>
        private void BuyShip(string shipId)
        {
            if (!ShipUnlocks.IsAvailableForAcquisition(shipId))
            {
                Sfx.Play(Sound.Denied);
                return;
            }
            var index = shipButtons.FindIndex(entry => entry != null && entry.shipId == shipId);
            var entry = index >= 0 ? shipButtons[index] : null;
            if (!run.shipUnlocks.Buy(shipId))
            {
                Sfx.Play(Sound.Denied);
                if (entry != null && entry.button != null)
                {
                    SetButtonLabel(entry.button, L("FALTAM ", "NEED ", "FALTAN ") +
                        (ShipUnlocks.Price - run.passiveUpgrades.credits) + L(" CRÉDITOS", " MORE", " CRÉDITOS"));
                    Juice.Punch(entry.button.transform, 0.08f, 0.3f);
                    var button = entry.button;
                    LeanTween.delayedCall(button.gameObject, 2f, () =>
                    {
                        if (button != null) RefreshSelectedShip();
                    }).setIgnoreTimeScale(true);
                }
                return;
            }

            Sfx.Play(Sound.Purchase);
            Juice.ScreenFlash(new Color(1f, 0.85f, 0.45f, 0.35f), 0.45f);
            if (index >= 0 && index < shipPanels.Count) Juice.Punch(shipPanels[index], 0.06f, 0.4f);
            RefreshMainMenu();
            SelectShip(shipId);
        }

        private void StartSelectedShip()
        {
            // Squadron always flies the free default ship, whatever the hangar has selected.
            var ship = config.GetShip(run.isSquadron ? ShipUnlocks.FreeShipId : selectedShipId);
            if (ship != null)
            {
                if (!run.isSquadron) run.ClampStageToShip(ship.id);
                WarpToStage(run.stageIndex, ship, () => run.BeginRun(ship));
            }
        }

        /// <summary>Same ship, same stage, from wave one.</summary>
        private void ReplayRun()
        {
            var ship = run.shipDefinition != null ? run.shipDefinition : config.GetShip(selectedShipId);
            AfterResultAd(() =>
            {
                if (ship != null) WarpToStage(run.stageIndex, ship, () => run.BeginRun(ship));
                else run.Restart();
            });
        }

        private void RefreshSelectedShip()
        {
            for (var i = 0; i < shipButtons.Count; i++)
            {
                var entry = shipButtons[i];
                if (entry == null || entry.button == null || i >= shipPanels.Count) continue;
                var isSelected = entry.shipId == selectedShipId;
                var owned = run == null || run.shipUnlocks.IsUnlocked(entry.shipId);
                var available = ShipUnlocks.IsAvailableForAcquisition(entry.shipId);

                SetButtonLabel(entry.button, !owned
                    ? !available ? L("INDISPONÍVEL", "UNAVAILABLE", "NO DISPONIBLE")
                        : L("DESBLOQUEAR  ·  ", "UNLOCK  ·  ", "DESBLOQUEAR  ·  ") + ShipUnlocks.Price
                    : isSelected
                        ? L("SELECIONADA", "SELECTED", "ELEGIDA")
                        : L("SELECIONAR", "SELECT", "ELEGIR"));
                entry.button.interactable = owned || available;
                RefreshShipMoneyButton(entry, owned);
                // The frame is the interface's (cyan; Fósforo when not owned),
                // not the ship's: an orange or red ship turned the whole card
                // into a "danger" colour. The ship keeps its tint where it is
                // the ship - hull, name, top stripe, power and passive.
                var frameAccent = owned ? HangarFrame : Palette.Fosforo;
                // The chosen ship's button is a state, not an action: no lit
                // frame, dim fill. The check badge on top is gone (it said the
                // same thing twice).
                entry.button.GetComponent<Image>().color = WithAlpha(frameAccent, !owned ? 0.4f : isSelected ? 0.35f : 0.75f);
                var actionInset = entry.button.transform.Find("Inset");
                if (actionInset != null) actionInset.GetComponent<Image>().color =
                    Palette.Vidro(frameAccent, !owned ? 0.06f : isSelected ? 0.05f : 0.16f);
                var actionRail = entry.button.transform.Find("BottomRail");
                if (actionRail != null) actionRail.GetComponent<Image>().color =
                    WithAlpha(frameAccent, isSelected && owned ? 0.2f : 0.5f);
                var actionCaption = entry.button.transform.Find("Caption");
                if (actionCaption != null)
                    actionCaption.GetComponent<TMP_Text>().color = isSelected && owned ? UIFactory.InkDim : UIFactory.Ink;
                if (entry.deck != null)
                {
                    entry.deck.GetComponent<Image>().color = WithAlpha(HangarFrame, 0.6f);
                    var deckInset = entry.deck.transform.Find("Inset");
                    if (deckInset != null) deckInset.GetComponent<Image>().color = Palette.Vidro(HangarFrame, 0.1f);
                }

                var panel = shipPanels[i];
                ShowShipLock(panel, !owned);
                // Under the name: the level the player has with this ship (it
                // moves with every upgrade bought, so refresh on each open).
                SetLabel(panel, "Role", ShipLevelLine(entry.shipId, entry.accent));
                panel.GetComponent<Image>().color = WithAlpha(frameAccent, isSelected ? 1f : 0.45f);
                var panelInset = panel.Find("Inset");
                if (panelInset != null) panelInset.GetComponent<Image>().color =
                    Palette.Vidro(frameAccent, isSelected ? 0.09f : 0.05f);
                var band = panel.Find("ButtonBand");
                if (band != null)
                {
                    band.GetComponent<Image>().color = Palette.Vidro(frameAccent, 0.05f);
                    var bandAccent = band.Find("Accent");
                    if (bandAccent != null) bandAccent.GetComponent<Image>().color = WithAlpha(frameAccent, 0.3f);
                }

                // How far each card is from the middle sets its alpha now
                // (CarouselSnap focus fade), not whether it is the chosen one.
                if (Application.isPlaying)
                {
                    LeanTween.cancel(panel.gameObject, false, TweenAction.SCALE);
                    LeanTween.scale(panel.gameObject, Vector3.one * (isSelected ? 1.03f : 1f), 0.22f)
                        .setEase(LeanTweenType.easeOutBack).setIgnoreTimeScale(true);
                }

                if (i < shipGlows.Count)
                    UIFactory.FadeGlow(shipGlows[i], WithAlpha(HangarFrame, isSelected ? SelectedGlow * 0.6f : 0f), 0.25f);
                if (i < shipBrackets.Count) shipBrackets[i].gameObject.SetActive(false);
                if (i < shipBadges.Count) shipBadges[i].gameObject.SetActive(false);
            }
        }

        /// <summary>A padlock and the price on a ship card the player does not own yet (built on first need).</summary>
        private void ShowShipLock(RectTransform panel, bool locked)
        {
            var existing = panel.Find("ShipLock");
            if (!locked)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }
            if (existing == null)
            {
                var badge = UIFactory.Pill(panel, "ShipLock", "", UIFactory.Evolved, new Color(0.05f, 0.04f, 0.02f, 0.96f),
                    60f, 30, ArtLibrary.Art("ic_lock"));
                UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(300f, 60f));
                existing = badge.transform;
            }
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
            SetLabel(existing, "Label", L("BLOQUEADA", "LOCKED", "BLOQUEADA"));
        }

        private void RefreshShipLanguage()
        {
            for (var i = 0; i < shipPanels.Count && i < shipButtons.Count; i++)
            {
                var ship = config.GetShip(shipButtons[i].shipId);
                if (ship == null) continue;
                var panel = shipPanels[i];
                SetLabel(panel, "Name", GameLocalization.Content(ship.name).ToUpperInvariant());
                SetLabel(panel, "Role", ShipLevelLine(ship.id, shipButtons[i].accent));
                RefreshShipPassive(panel, ship);
                var special = ship.special;
                if (special != null && special.kind != ShipSpecialKind.None)
                {
                    SetLabel(panel, "Special/Name",
                        GameLocalization.Choose(special.name, special.nameEn, special.nameEs).ToUpperInvariant());
                    SetLabel(panel, "Special/Description",
                        GameLocalization.Choose(special.description, special.descriptionEn, special.descriptionEs));
                    SetLabel(panel, "Special/Cooldown", special.cooldown.ToString("0") + "s");

                    // Set at runtime, not baked: some power icons (Stasis) are
                    // drawn in code, and a generated sprite cannot be saved in
                    // the scene - the baked badge came back empty.
                    var icon = panel.Find("Special/Badge/Icon");
                    if (icon != null)
                    {
                        var image = icon.GetComponent<Image>();
                        image.sprite = ShipAbilityButton.IconFor(special.kind);
                        ShipAbilityButton.CenterIcon(image, special.kind);
                    }
                }
                SetLabel(panel, "SelectedBadge/Label", L("SELECIONADA", "SELECTED", "ELEGIDA"));
            }
            RefreshShipStatBars();
            RefreshShipScreenHeading();
        }

        /// <summary>
        /// Fills the bars relative to the best ship in each stat, so the cards
        /// compare at a glance instead of by reading numbers.
        /// </summary>
        private void RefreshShipStatBars()
        {
            float bestHp = 0f, bestDamage = 0f, bestRate = 0f, bestDps = 0f;
            for (var i = 0; i < shipButtons.Count; i++)
            {
                var ship = config.GetShip(shipButtons[i].shipId);
                if (ship == null) continue;
                bestHp = Mathf.Max(bestHp, ship.maxHp);
                bestDamage = Mathf.Max(bestDamage, ship.damagePerShot);
                bestRate = Mathf.Max(bestRate, ShotsPerSecond(ship));
                bestDps = Mathf.Max(bestDps, Dps(ship));
            }

            for (var i = 0; i < shipPanels.Count && i < shipButtons.Count; i++)
            {
                var ship = config.GetShip(shipButtons[i].shipId);
                if (ship == null) continue;
                var bars = shipPanels[i].Find("StatBars");
                if (bars == null) continue;
                SetStatRow(bars, "Hp", L("VIDA", "HULL", "VIDA"), ship.maxHp, bestHp, ship.maxHp.ToString("0"));
                SetStatRow(bars, "Damage", L("DANO", "DAMAGE", "DAÑO"),
                    ship.damagePerShot, bestDamage, ship.damagePerShot.ToString("0"));
                SetStatRow(bars, "FireRate", L("TIROS/S", "SHOTS/S", "DISP./S"),
                    ShotsPerSecond(ship), bestRate, ShotsPerSecond(ship).ToString("0.0"));
                SetStatRow(bars, "Dps", L("DANO/S", "DPS", "DAÑO/S"), Dps(ship), bestDps, Dps(ship).ToString("0"));
            }
        }

        private static void SetStatRow(Transform bars, string row, string label, float value, float best, string text)
        {
            var line = bars.Find(row);
            if (line == null) return;
            SetLabel(line, "Label", label);
            SetLabel(line, "Value", text);
            var fill = line.Find("Bar/Fill");
            // A measure, not the ship's identity: cyan on every card, so the
            // bars compare across ships instead of changing colour with them.
            if (fill != null) fill.GetComponent<Image>().color = HangarFrame;
            if (fill != null)
                fill.GetComponent<Image>().fillAmount = best > 0f ? Mathf.Clamp(value / best, 0.06f, 1f) : 0f;
        }

        private static float ShotsPerSecond(ShipDefinition ship)
        {
            return 1f / Mathf.Max(0.01f, ship.fireInterval);
        }

        private static float Dps(ShipDefinition ship)
        {
            return ship.damagePerShot * Mathf.Max(1, ship.baseProjectiles) * ShotsPerSecond(ship);
        }

        // ------------------------------------------------------------------
        // Deck viewer (runtime)
        // ------------------------------------------------------------------

        private void BuildDeckScreen()
        {
            if (deckScreen != null) return;

            Image backdrop;
            deckScreen = UIFactory.Page(safeRoot, "DeckScreen", new Color(0.02f, 0.03f, 0.06f, 1f), out backdrop);

            var back = UIFactory.RoundButton(deckScreen, "Back", ArtLibrary.IconBack, UIFactory.PanelSoft,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -32f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(back, CloseShipDeck);

            deckTitle = UIFactory.Label(deckScreen, "Title", "", 54, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(deckTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(UIFactory.Pick(1300f, 760f), 70f));
            deckTitle.textWrappingMode = TextWrappingModes.NoWrap;
            deckTitle.enableAutoSizing = true;
            deckTitle.fontSizeMin = 36f;
            deckTitle.fontSizeMax = 54f;
            deckTitle.characterSpacing = 6f;
            deckSubtitle = UIFactory.Label(deckScreen, "Subtitle", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(deckSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f),
                new Vector2(UIFactory.Pick(1300f, 1020f), 42f));
            deckSubtitle.enableAutoSizing = true;
            deckSubtitle.fontSizeMin = 26f;
            deckSubtitle.fontSizeMax = UIFactory.TextBody;

            var scrollRoot = UIFactory.NewRect(deckScreen, "DeckScroll");
            UIFactory.Stretch(scrollRoot);
            scrollRoot.offsetMin = new Vector2(32f, 32f);
            scrollRoot.offsetMax = new Vector2(-32f, -190f);
            deckScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            deckScroll.horizontal = false;
            deckScroll.vertical = true;
            deckScroll.movementType = ScrollRect.MovementType.Elastic;
            deckScroll.scrollSensitivity = 35f;

            var viewport = UIFactory.NewRect(scrollRoot, "Viewport");
            UIFactory.Stretch(viewport);
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            deckContent = UIFactory.NewRect(viewport, "Content");
            UIFactory.Place(deckContent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1500f, 0f));
            deckScroll.viewport = viewport;
            deckScroll.content = deckContent;

            deckScreen.gameObject.SetActive(false);
        }

        private void CloseShipDeck()
        {
            HideScreen(deckScreen, SyncAdNavigation);
            SyncAdNavigation();
        }

        private void ShowShipDeck(string shipId)
        {
            var ship = config.GetShip(shipId);
            if (ship == null) return;

            deckTitle.text = GameLocalization.Content(ship.name).ToUpperInvariant();
            deckTitle.color = SpriteFactory.ParseColor(ship.tint);

            for (var i = deckContent.childCount - 1; i >= 0; i--)
                Destroy(deckContent.GetChild(i).gameObject);

            // Only the ship's exclusive cards: the shared ones are the base
            // deck (its own section). Ordered by the level that opens them.
            var cards = new List<CardDefinition>();
            var seen = new HashSet<string>();
            for (var i = 0; i < ship.deck.Count; i++)
            {
                var definition = config.GetCard(ship.deck[i].cardId);
                if (definition != null && definition.category != CardCategory.Common && seen.Add(definition.id))
                    cards.Add(definition);
            }
            cards.Sort((a, b) => a.unlockLevel != b.unlockLevel
                ? a.unlockLevel.CompareTo(b.unlockLevel) : CompareCardDisplayOrder(a, b));

            var shipLevel = run.passiveUpgrades.TotalLevel(ship.id);
            var open = 0;
            for (var i = 0; i < cards.Count; i++) if (run.cardUnlocks.IsUnlocked(cards[i], ship.id)) open++;
            deckSubtitle.text = L("CARTAS EXCLUSIVAS  ·  ", "EXCLUSIVE CARDS  ·  ", "CARTAS EXCLUSIVAS  ·  ") +
                open + "/" + cards.Count + L(" LIBERADAS  ·  NAVE NÍVEL ", " UNLOCKED  ·  SHIP LEVEL ",
                    " DESBLOQUEADAS  ·  NAVE NIVEL ") + shipLevel;

            FillCardGrid(deckContent, deckScreen, cards, card => run.cardUnlocks.IsUnlocked(card, shipId)
                ? null
                : L("NAVE NÍVEL ", "SHIP LEVEL ", "NAVE NIVEL ") + card.unlockLevel +
                  "  ·  " + Mathf.Min(shipLevel, card.unlockLevel) + "/" + card.unlockLevel,
                SpriteFactory.ParseColor(ship.tint));

            deckScroll.verticalNormalizedPosition = 1f;
            deckScreen.SetAsLastSibling();
            deckScreen.gameObject.SetActive(true);
            // The footer banner sits under the page (its height is reserved),
            // then the block is centred in what is left.
            SyncAdNavigation();
            CenterDeckScreen();
            FadeScreenIn(UIFactory.Group(deckScreen), 0.2f);
        }

        /// <summary>
        /// Title, subtitle and cards as one block in the middle of the page
        /// when the cards do not fill it; at the top (scrolling) when they do.
        /// </summary>
        private void CenterDeckScreen()
        {
            Canvas.ForceUpdateCanvases();
            const float header = 190f;
            const float bottom = 32f;
            var pageHeight = deckScreen.rect.height;
            var shift = Mathf.Max(0f, (pageHeight - header - bottom - deckContent.sizeDelta.y) * 0.5f);
            deckTitle.rectTransform.anchoredPosition = new Vector2(0f, -40f - shift);
            deckSubtitle.rectTransform.anchoredPosition = new Vector2(0f, -112f - shift);
            var scroll = (RectTransform)deckScroll.transform;
            scroll.offsetMin = new Vector2(32f, bottom + shift);
            scroll.offsetMax = new Vector2(-32f, -header - shift);
            deckScroll.verticalNormalizedPosition = 1f;
        }
    }
}
