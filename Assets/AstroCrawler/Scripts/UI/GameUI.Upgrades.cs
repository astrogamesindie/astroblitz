// AstroCrawler - the permanent upgrade tree.
//
// Credits earned at the end of every run buy random levels across one ship's
// eight passive attributes. The screen has to answer three questions at a
// glance: which ship am I upgrading, how far along is each attribute, and can
// I afford the next purchase. So the ship being viewed glows and points at the
// tree, every attribute carries a bar out of 20, and the buy buttons say what
// they cost - in red when the balance does not cover it.
//
// Built at runtime in Bind, inside the (baked) ship screen.

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
        private sealed class PassiveNode
        {
            public PassiveUpgrade upgrade;
            public RectTransform root;
            public Vector2 home;
            public Image panel;
            public Image icon;
            public TMP_Text title;
            public TMP_Text level;
            public TMP_Text bonus;
            public Image fill;
            public TrueShadow glow;
        }

        private static readonly int PassiveCount = Enum.GetValues(typeof(PassiveUpgrade)).Length;

        private readonly List<PassiveNode> passiveNodes = new List<PassiveNode>();
        private RectTransform passiveScreen;
        private Image passiveShipHull;
        private TMP_Text passiveShipName;
        private TMP_Text passiveLevels;
        private Image passiveLevelsFill;
        private TMP_Text passiveMessage;
        private TMP_Text passiveBalance;
        private RectTransform creditsChip;
        private Button buyOneButton;
        private Button buyFiveButton;
        private TMP_Text buyOneCost;
        private TMP_Text buyFiveCost;
        private string passiveShipId;
        private int shownCredits = -1;
        private int messageStamp;

        // The screen fills the page: header, the eight attributes sharing the
        // middle, the two purchases along the bottom. Portrait: one attribute
        // per row; landscape: two columns of four.
        // Portrait: a little taller than before, so the "random attribute" rule
        // gets a full-width line of its own under the ship.
        private static float PassiveHeaderHeight => UIFactory.Pick(190f, 214f);
        // Buy buttons (150); in portrait also the next exclusive card strip above
        // them. In landscape the strip sits in the header, where there is room.
        private static float PassiveFooterHeight => UIFactory.Pick(150f, 150f + NextCardStripHeight + 10f);
        private const float NodeGap = 12f;
        /// <summary>Secondary text on the upgrade screen: brighter than InkDim so it reads on the dark nodes.</summary>
        private static readonly Color BonusInk = new Color(0.8f, 0.85f, 0.94f);

        // Near the Vazio, like every surface (the grey rows read heavy and off-palette).
        private static readonly Color NodePanel = Palette.Vidro(Palette.Ciano, 0.05f, 0.98f);
        private static readonly Color NodePanelActive = Palette.Vidro(Palette.Ciano, 0.1f, 0.98f);
        /// <summary>Bars, levels and icons of the tree: the interface's cyan, not the ship's colour (red read as danger).</summary>
        private static Color PassiveAccent => UIFactory.Accent;
        private TrueShadow buyFiveGlow;
        private static readonly Color BuyOneColor = UIFactory.ActionFill;
        private static readonly Color BuyFiveColor = UIFactory.ActionFillStrong;
        private static readonly Color BuyDisabled = UIFactory.ActionFillMuted;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        /// <summary>
        /// The upgrade tree of the ship being flown (the one equipped in the
        /// hangar); there is no ship picker here.
        /// </summary>
        private void BuildPassiveScreen()
        {
            if (passiveScreen != null || shipScreen == null) return;

            // Under the title and back button, down to the bottom edge; the
            // rewarded-coins button keeps a strip below it.
            var background = UIFactory.Panel9(shipScreen, "PassiveUpgrades", UIFactory.Panel);
            passiveScreen = background.rectTransform;
            UIFactory.Stretch(passiveScreen);
            passiveScreen.offsetMin = new Vector2(24f, 24f);
            passiveScreen.offsetMax = new Vector2(-24f, -150f);
            UIFactory.DropShadow(background, 36f, 14f, 0.6f);

            BuildPassiveHeader();
            BuildCreditsChip();
            BuildTree();
            BuildBuyButtons();
            BuildNextCardStrip();

            passiveShipId = !string.IsNullOrEmpty(selectedShipId)
                ? selectedShipId
                : config.ships.Count > 0 ? config.ships[0].id : null;
            RefreshPassiveLanguage();
            RefreshPassiveScreen();
            passiveScreen.gameObject.SetActive(false);
        }

        /// <summary>The ship being upgraded: its hull, name and total progress.</summary>
        private void BuildPassiveHeader()
        {
            var plate = UIFactory.Rect(passiveScreen, "ShipPlate", new Color(0.02f, 0.03f, 0.06f, 0.95f), ArtLibrary.RoundFill);
            UIFactory.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(136f, 136f));
            passiveShipHull = UIFactory.Rect(plate.rectTransform, "Hull", UIFactory.Accent, ArtLibrary.Ship);
            passiveShipHull.preserveAspect = true;
            UIFactory.Stretch(passiveShipHull.rectTransform, 22f);
            UIFactory.GlowShadow(passiveShipHull, WithAlpha(Color.white, 0.25f), 18f, 0.05f);

            passiveShipName = UIFactory.Label(passiveScreen, "ShipName", "", 42, UIFactory.Accent,
                TextAlignmentOptions.TopLeft, true);
            // The credits chip owns the top right corner; the header stops short of it.
            passiveShipName.rectTransform.anchorMin = new Vector2(0f, 1f);
            passiveShipName.rectTransform.anchorMax = new Vector2(1f, 1f);
            passiveShipName.rectTransform.pivot = new Vector2(0f, 1f);
            passiveShipName.rectTransform.offsetMin = new Vector2(184f, -78f);
            passiveShipName.rectTransform.offsetMax = new Vector2(-350f, -26f);
            passiveShipName.characterSpacing = 5f;
            passiveShipName.textWrappingMode = TextWrappingModes.NoWrap;
            passiveShipName.enableAutoSizing = true;
            passiveShipName.fontSizeMin = 28f;
            passiveShipName.fontSizeMax = 42f;

            passiveLevels = UIFactory.Label(passiveScreen, "Levels", "", 32, UIFactory.Ink,
                TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(passiveLevels.rectTransform, new Vector2(0f, 1f), new Vector2(184f, -82f), new Vector2(360f, 40f));
            passiveLevels.characterSpacing = 3f;

            Image back;
            passiveLevelsFill = UIFactory.Bar(passiveScreen, "LevelsBar", UIFactory.Accent,
                new Color(0.02f, 0.03f, 0.06f, 0.95f), out back);
            UIFactory.Place((RectTransform)passiveLevelsFill.rectTransform.parent, new Vector2(0f, 1f),
                new Vector2(184f, -128f), new Vector2(UIFactory.Pick(420f, 340f), 16f));

            // The explanation by default; purchase results and "not enough
            // credits" replace it for a few seconds. One line, two jobs.
            passiveMessage = UIFactory.Label(passiveScreen, "Message", "", UIFactory.Pick(30, 34), UIFactory.Ink,
                TextAlignmentOptions.TopLeft, true);
            passiveMessage.rectTransform.anchorMin = new Vector2(0f, 1f);
            passiveMessage.rectTransform.anchorMax = new Vector2(1f, 1f);
            passiveMessage.rectTransform.pivot = new Vector2(0f, 1f);
            // Landscape: right of the level count (the next card strip owns the row below).
            // Portrait: its own full-width line under the ship plate, in the
            // bright ink - it is the rule of the whole screen.
            passiveMessage.rectTransform.offsetMin = new Vector2(UIFactory.Pick(570f, 28f),
                UIFactory.Pick(-112f, -PassiveHeaderHeight + 8f));
            passiveMessage.rectTransform.offsetMax = new Vector2(UIFactory.Pick(-350f, -28f), UIFactory.Pick(-80f, -166f));
            passiveMessage.textWrappingMode = TextWrappingModes.NoWrap;
            passiveMessage.enableAutoSizing = true;
            passiveMessage.fontSizeMin = UIFactory.Pick(24f, 26f);
            passiveMessage.fontSizeMax = UIFactory.Pick(30f, 34f);
        }

        private void BuildCreditsChip()
        {
            var chip = UIFactory.Panel9(passiveScreen, "Credits", UIFactory.PanelDeep);
            creditsChip = chip.rectTransform;
            UIFactory.Place(creditsChip, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(300f, 112f));
            UIFactory.GlowShadow(chip, WithAlpha(UIFactory.Evolved, 0.28f), 22f, 0.05f);

            var jewel = UIFactory.Rect(creditsChip, "Icon", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            UIFactory.Place(jewel.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(64f, 64f));

            // The jewel says "credits": only the number.
            passiveBalance = UIFactory.Label(creditsChip, "Value", "0", 54, UIFactory.Evolved,
                TextAlignmentOptions.Right, true);
            UIFactory.Place(passiveBalance.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(190f, 70f));
        }

        private void BuildTree()
        {
            var tree = UIFactory.NewRect(passiveScreen, "Tree");
            UIFactory.Stretch(tree);
            tree.offsetMin = new Vector2(20f, PassiveFooterHeight);
            tree.offsetMax = new Vector2(-20f, -PassiveHeaderHeight);

            var columns = UIFactory.Pick(2, 1);
            var rows = (PassiveCount + columns - 1) / columns;
            for (var i = 0; i < PassiveCount; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var upgrade = (PassiveUpgrade)i;

                // Each attribute owns an equal share of the tree, whatever the
                // screen's height: no gaps on a tall phone, no overlap on a tablet.
                var panel = UIFactory.Panel9(tree, "Node_" + i, NodePanel);
                var rect = panel.rectTransform;
                rect.anchorMin = new Vector2((float)column / columns, 1f - (float)(row + 1) / rows);
                rect.anchorMax = new Vector2((float)(column + 1) / columns, 1f - (float)row / rows);
                rect.offsetMin = new Vector2(NodeGap * 0.5f, NodeGap * 0.5f);
                rect.offsetMax = new Vector2(-NodeGap * 0.5f, -NodeGap * 0.5f);

                var node = new PassiveNode
                {
                    upgrade = upgrade,
                    root = rect,
                    home = rect.anchoredPosition,
                    panel = panel
                };
                UIFactory.DropShadow(panel, 16f, 5f, 0.45f);
                node.glow = UIFactory.GlowShadow(panel, WithAlpha(UIFactory.Accent, 0f), 26f, 0.1f);

                // The content is a 110-high band centred in the node, so a tall
                // node does not leave it hanging at the top.
                var body = UIFactory.NewRect(rect, "Body");
                body.anchorMin = new Vector2(0f, 0.5f);
                body.anchorMax = new Vector2(1f, 0.5f);
                body.pivot = new Vector2(0.5f, 0.5f);
                body.sizeDelta = new Vector2(0f, 124f);
                body.anchoredPosition = Vector2.zero;

                var iconBack = UIFactory.Rect(body, "IconBack", new Color(0.02f, 0.03f, 0.06f, 0.95f),
                    ArtLibrary.CircleFilled);
                UIFactory.Place(iconBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(100f, 100f));

                node.icon = UIFactory.Rect(body, "Icon", UIFactory.Accent, ArtLibrary.PassiveIcon(upgrade));
                node.icon.preserveAspect = true;
                UIFactory.Place(node.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(60f, 60f));

                node.title = UIFactory.Label(body, "Name", "", 42, UIFactory.Ink,
                    TextAlignmentOptions.TopLeft, true);
                Band(node.title.rectTransform, 136f, 160f, 0f, 50f);
                node.title.enableAutoSizing = true;
                node.title.fontSizeMin = 28f;
                node.title.fontSizeMax = 42f;
                node.title.textWrappingMode = TextWrappingModes.NoWrap;

                node.level = UIFactory.Label(body, "Level", "", 42, UIFactory.Accent,
                    TextAlignmentOptions.TopRight, true);
                UIFactory.Place(node.level.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, 0f), new Vector2(150f, 50f));

                Image back;
                node.fill = UIFactory.Bar(body, "Progress", UIFactory.Accent, new Color(0.02f, 0.03f, 0.06f, 0.95f), out back);
                var bar = (RectTransform)node.fill.rectTransform.parent;
                Band(bar, 136f, 20f, -58f, 16f);

                // A notch every five levels makes "how far" countable at a glance.
                for (var tick = 1; tick < 4; tick++)
                {
                    var notch = UIFactory.Rect(bar, "Tick" + tick, new Color(0.03f, 0.04f, 0.08f, 1f), ArtLibrary.Pixel);
                    notch.rectTransform.anchorMin = new Vector2(tick / 4f, 0f);
                    notch.rectTransform.anchorMax = new Vector2(tick / 4f, 1f);
                    notch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    notch.rectTransform.sizeDelta = new Vector2(3f, 0f);
                    notch.rectTransform.anchoredPosition = Vector2.zero;
                }

                // The current -> next bonus: the information to read, so large and bright.
                node.bonus = UIFactory.Label(body, "Bonus", "", 36, BonusInk, TextAlignmentOptions.TopLeft, true);
                Band(node.bonus.rectTransform, 136f, 20f, -80f, 44f);
                node.bonus.textWrappingMode = TextWrappingModes.NoWrap;
                node.bonus.enableAutoSizing = true;
                node.bonus.fontSizeMin = 28f;
                node.bonus.fontSizeMax = 36f;

                passiveNodes.Add(node);
            }
        }

        /// <summary>A rect spanning its parent's width (minus margins), at a fixed top offset and height.</summary>
        private static void Band(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, top - height);
            rect.offsetMax = new Vector2(-right, top);
        }

        private void BuildBuyButtons()
        {
            buyOneButton = BuildBuyButton("BuyOne", 0, BuyOneColor, out buyOneCost);
            buyFiveButton = BuildBuyButton("BuyFive", 1, BuyFiveColor, out buyFiveCost);

            // Badges are magenta (UI pillars); the button itself is the amber one.
            var discount = UIFactory.Pill(buyFiveButton.transform, "Discount", "-20%", Palette.Magenta,
                UIFactory.Ink, 40f, 24);
            buyFiveGlow = UIFactory.GlowShadow(buyFiveButton.GetComponent<Image>(), WithAlpha(Palette.Ambar, 0f), 30f, 0.06f);
            UIFactory.Place(discount.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, 22f), new Vector2(104f, 40f));
            discount.sprite = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            discount.type = Image.Type.Sliced;
            discount.pixelsPerUnitMultiplier = 0.5f;

            Rebind(buyOneButton, () => BuyPassive(1));
            Rebind(buyFiveButton, () => BuyPassive(5));
        }

        /// <summary>One of the two purchases, each half of the bottom strip.</summary>
        private Button BuildBuyButton(string name, int side, Color color, out TMP_Text cost)
        {
            // Quiet by itself: BuyPassive plays Purchase or Denied depending on
            // whether the purchase went through.
            var button = UIFactory.Button(passiveScreen, name, null, color, UIFactory.Ink, UIFactory.TextButton, Sound.None);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(side * 0.5f, 0f);
            rect.anchorMax = new Vector2(side * 0.5f + 0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(side == 0 ? 26f : 10f, 22f);
            rect.offsetMax = new Vector2(side == 0 ? -10f : -26f, 22f + 116f);
            UIFactory.DropShadow(button.GetComponent<Image>(), 18f, 6f, 0.5f);
            StyleCutAction(button, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"),
                UIFactory.ActionBorder, color, false);

            // Caption on the left, price on the right: the price is the part
            // that changes colour, so it gets its own spot.
            var caption = UIFactory.Label(rect, "Caption", "", UIFactory.TextButton, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(caption.rectTransform, 8f);
            caption.rectTransform.offsetMax = new Vector2(-190f, -8f);
            caption.characterSpacing = 4f;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 28f;
            caption.fontSizeMax = UIFactory.TextButton;
            UIFactory.AddUnderlay(caption);

            var divider = UIFactory.Rect(rect, "Divider", WithAlpha(UIFactory.Accent, 0.45f), ArtLibrary.Pixel);
            UIFactory.Place(divider.rectTransform, new Vector2(1f, 0.5f), new Vector2(-186f, 0f), new Vector2(2f, 64f));

            var jewel = UIFactory.Rect(rect, "CostIcon", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            UIFactory.Place(jewel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-132f, 0f), new Vector2(38f, 38f));

            cost = UIFactory.Label(rect, "Cost", "", 38, UIFactory.Evolved, TextAlignmentOptions.Right, true);
            UIFactory.Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(104f, 52f));
            return button;
        }

        // ------------------------------------------------------------------
        // Behaviour
        // ------------------------------------------------------------------

        private void BuyPassive(int amount)
        {
            var progress = run.passiveUpgrades;
            var price = progress.Cost(passiveShipId, amount);
            var button = amount == 1 ? buyOneButton : buyFiveButton;
            var remaining = progress.AvailableLevels(passiveShipId);

            // The buttons stay clickable when a purchase is impossible, so the
            // tap can say why instead of just not working.
            if (remaining < amount)
            {
                Sfx.Play(Sound.Denied);
                SetPassiveMessage(remaining == 0
                    ? L("Nave no máximo.", "Ship fully upgraded.", "Nave al máximo.")
                    : L("Restam só ", "Only ", "Solo quedan ") + remaining +
                      L(" níveis.", " levels left.", " niveles."), UIFactory.InkDim);
                Juice.Punch(button.transform, 0.06f, 0.25f);
                return;
            }
            if (progress.credits < price)
            {
                Sfx.Play(Sound.Denied);
                SetPassiveMessage(L("Faltam ", "Need ", "Faltan ") + (price - progress.credits) +
                    L(" créditos  ·  termine runs para ganhar", " more credits  ·  finish runs to earn",
                        " créditos  ·  termina partidas para ganar"), UIFactory.Danger);
                Juice.Punch(creditsChip, 0.12f, 0.3f);
                Juice.Punch(button.transform, 0.06f, 0.25f);
                return;
            }

            var levelBefore = progress.TotalLevel(passiveShipId);
            var bought = progress.Buy(passiveShipId, amount);
            if (bought == null) return;
            Sfx.Play(Sound.Purchase);
            var unlocked = ExclusiveUnlockedBetween(passiveShipId, levelBefore, progress.TotalLevel(passiveShipId));

            var counts = new int[PassiveCount];
            for (var i = 0; i < bought.Count; i++) counts[(int)bought[i]]++;

            builder.Length = 0;
            for (var i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (builder.Length > 0) builder.Append("   ·   ");
                builder.Append('+').Append(counts[i]).Append(' ').Append(PassiveUpgrades.Name((PassiveUpgrade)i));
            }
            if (unlocked != null)
            {
                // The purchase crossed an unlock level: that news comes first.
                // The full-screen reveal (AnimatePassivePurchase) brings the flash and sound.
                SetPassiveMessage(L("NOVA CARTA EXCLUSIVA: ", "NEW EXCLUSIVE CARD: ", "NUEVA CARTA EXCLUSIVA: ") +
                    GameLocalization.Content(unlocked.name).ToUpperInvariant(), UIFactory.Evolved);
            }
            else
            {
                SetPassiveMessage(builder.ToString(), UIFactory.Good);
            }

            RefreshPassiveScreen();

            // Every attribute that went up flashes, so a random purchase still
            // shows exactly where the credits went.
            for (var i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0 || i >= passiveNodes.Count) continue;
                var node = passiveNodes[i];
                node.root.localScale = Vector3.one;
                Juice.Punch(node.root, 0.07f, 0.35f);
                Juice.Punch(node.level.transform, 0.3f, 0.35f);
                UIFactory.FlashGlow(node.glow, WithAlpha(UIFactory.Good, 1f), node.glow.Color, 0.9f);
            }
            Juice.Punch(creditsChip, 0.08f, 0.3f);
            AnimatePassivePurchase(levelBefore, progress.TotalLevel(passiveShipId), counts, unlocked);
        }

        private void SetPassiveMessage(string message, Color color)
        {
            if (passiveMessage == null) return;
            passiveMessage.text = message;
            passiveMessage.color = color;
            Juice.Punch(passiveMessage.transform, 0.08f, 0.25f);

            // Back to the explanation after a moment. A delayed call rather
            // than Update: it only exists while a message is showing.
            var stamp = ++messageStamp;
            LeanTween.delayedCall(passiveMessage.gameObject, 3.5f, () =>
            {
                if (passiveMessage != null && stamp == messageStamp) ShowPassiveExplanation();
            }).setIgnoreTimeScale(true);
        }

        private void ShowPassiveExplanation()
        {
            // The next card has its own strip above the buy buttons now.
            passiveMessage.color = UIFactory.Ink;
            passiveMessage.text = L("Cada compra: +1 nível num atributo aleatório",
                "Each purchase: +1 level to a random attribute",
                "Cada compra: +1 nivel en un atributo aleatorio");
        }

        /// <summary>The ship's next exclusive card still waiting on its level, or null.</summary>
        private CardDefinition NextExclusiveUnlock(string shipId)
        {
            var ship = config.GetShip(shipId);
            if (ship == null) return null;
            var level = run.passiveUpgrades.TotalLevel(shipId);
            CardDefinition next = null;
            for (var i = 0; i < ship.deck.Count; i++)
            {
                var card = config.GetCard(ship.deck[i].cardId);
                if (card == null || card.unlockLevel <= level) continue;
                if (next == null || card.unlockLevel < next.unlockLevel) next = card;
            }
            return next;
        }

        /// <summary>An exclusive card whose unlock level lies in (before, after], or null.</summary>
        private CardDefinition ExclusiveUnlockedBetween(string shipId, int before, int after)
        {
            var ship = config.GetShip(shipId);
            if (ship == null) return null;
            for (var i = 0; i < ship.deck.Count; i++)
            {
                var card = config.GetCard(ship.deck[i].cardId);
                if (card != null && card.unlockLevel > before && card.unlockLevel <= after) return card;
            }
            return null;
        }

        private void RefreshPassiveScreen()
        {
            if (passiveScreen == null || string.IsNullOrEmpty(passiveShipId)) return;
            var ship = config.GetShip(passiveShipId);
            if (ship == null) return;

            var progress = run.passiveUpgrades;
            // The ship keeps its colour in the header (hull and name); the tree is cyan.
            var shipTint = SpriteFactory.ParseColor(ship.tint);
            var accent = PassiveAccent;
            var cap = PassiveUpgrades.MaxLevel * PassiveCount;

            passiveShipName.text = GameLocalization.Content(ship.name).ToUpperInvariant();
            passiveShipName.color = shipTint;
            passiveShipHull.color = shipTint;
            var total = TotalLevels(passiveShipId);
            passiveLevels.text = L("NÍVEIS ", "LEVELS ", "NIVELES ") + total + " / " + cap;
            passiveLevelsFill.color = accent;
            passiveLevelsFill.fillAmount = (float)total / cap;
            SetCredits(progress.credits);

            for (var i = 0; i < passiveNodes.Count; i++)
            {
                var node = passiveNodes[i];
                var level = progress.Level(passiveShipId, node.upgrade);
                var maxed = level >= PassiveUpgrades.MaxLevel;
                var tone = maxed ? UIFactory.Evolved : level > 0 ? accent : UIFactory.InkDim;

                node.title.text = PassiveUpgrades.Name(node.upgrade);
                node.level.text = maxed ? L("MÁX", "MAX", "MÁX") : level + "/" + PassiveUpgrades.MaxLevel;
                node.level.color = tone;
                node.icon.color = tone;
                node.fill.color = maxed ? UIFactory.Evolved : accent;
                node.fill.fillAmount = (float)level / PassiveUpgrades.MaxLevel;
                node.panel.color = level > 0 ? NodePanelActive : NodePanel;

                // Current -> next: the next value in cyan behind a real arrow
                // (the ">" character read as code). Level 0 has no "current":
                // only what the first level gives.
                var next = "<color=" + UIFactory.ColorTag(PassiveAccent) + ">";
                if (maxed)
                    node.bonus.text = PassiveUpgrades.Bonus(node.upgrade, level);
                else if (level == 0)
                    node.bonus.text = next + PassiveUpgrades.Bonus(node.upgrade, 1) + "</color>";
                else
                    node.bonus.text = PassiveUpgrades.Bonus(node.upgrade, level) + BonusArrow(node.bonus) +
                        next + PassiveUpgrades.Bonus(node.upgrade, level + 1) + "</color>";

                UIFactory.FadeGlow(node.glow, WithAlpha(UIFactory.Evolved, maxed ? 0.35f : 0f), 0f);
            }

            var remaining = progress.AvailableLevels(passiveShipId);
            StyleBuyButton(buyOneButton, buyOneCost, BuyOneColor, 1, progress.Cost(passiveShipId, 1),
                remaining, progress.credits, false);
            StyleBuyButton(buyFiveButton, buyFiveCost, BuyFiveColor, 5, progress.Cost(passiveShipId, 5),
                remaining, progress.credits, true);
            RefreshNextCardStrip();
            RefreshLevelMilestones();
        }

        /// <summary>
        /// One purchase button. The +5 (the better price) is the screen's single
        /// amber button when it can be bought; the +1 stays cyan.
        /// </summary>
        private void StyleBuyButton(Button button, TMP_Text cost, Color color, int amount, int price,
            int remaining, int credits, bool primary)
        {
            if (button == null) return;

            var hasRoom = remaining >= amount;
            var affordable = credits >= price;
            var ready = hasRoom && affordable;
            var amber = ready && primary;

            button.GetComponent<Image>().color = amber ? Palette.Ambar
                : ready ? UIFactory.ActionBorder : WithAlpha(UIFactory.Accent, 0.32f);
            var inset = button.transform.Find("Inset");
            if (inset != null) inset.GetComponent<Image>().color =
                amber ? Palette.Vidro(Palette.Ambar, 0.4f) : ready ? color : BuyDisabled;
            var divider = button.transform.Find("Divider");
            if (divider != null)
                divider.GetComponent<Image>().color = WithAlpha(amber ? Palette.Ambar : UIFactory.Accent, 0.45f);
            if (primary && buyFiveGlow != null) buyFiveGlow.Color = WithAlpha(Palette.Ambar, amber ? 0.4f : 0f);

            string caption;
            if (remaining == 0)
                caption = L("MÁXIMO", "MAXED", "MÁXIMO");
            else if (!hasRoom)
                caption = L("RESTAM ", "ONLY ", "QUEDAN ") + remaining;
            else
                caption = amount == 1
                    ? L("+1 NÍVEL", "+1 LEVEL", "+1 NIVEL")
                    : L("+5 NÍVEIS", "+5 LEVELS", "+5 NIVELES");
            SetButtonLabel(button, caption);

            var label = button.transform.Find("Caption");
            if (label != null) label.GetComponent<TMP_Text>().color = ready ? UIFactory.Ink : UIFactory.InkDim;

            cost.text = price.ToString();
            // On the amber button the price is ink (amber on amber does not read).
            cost.color = !affordable ? UIFactory.Danger : amber ? UIFactory.Ink : UIFactory.Evolved;
        }

        /// <summary>A real arrow between the current and the next bonus when the font has one.</summary>
        private static string BonusArrow(TMP_Text label)
        {
            var font = label != null ? label.font : null;
            return font != null && font.HasCharacter('\u2192', true) ? "  \u2192  " : "   \u203A   ";
        }

        /// <summary>Shows the balance, counting up or down to it after a change.</summary>
        private void SetCredits(int credits)
        {
            if (passiveBalance == null) return;
            if (shownCredits < 0 || !passiveBalance.gameObject.activeInHierarchy)
            {
                shownCredits = credits;
                passiveBalance.text = credits.ToString();
                return;
            }
            if (shownCredits == credits) return;

            var label = passiveBalance;
            LeanTween.cancel(label.gameObject, false, TweenAction.CALLBACK);
            LeanTween.value(label.gameObject, value =>
            {
                if (label != null) label.text = Mathf.RoundToInt(value).ToString();
            }, shownCredits, credits, 0.45f).setEase(LeanTweenType.easeOutCubic).setIgnoreTimeScale(true);
            shownCredits = credits;
        }

        private int TotalLevels(string shipId)
        {
            var total = 0;
            for (var i = 0; i < PassiveCount; i++)
                total += run.passiveUpgrades.Level(shipId, (PassiveUpgrade)i);
            return total;
        }

        private void RefreshPassiveLanguage()
        {
            if (passiveScreen == null) return;
            ShowPassiveExplanation();
        }
    }
}
