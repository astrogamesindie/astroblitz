// AstroCrawler - the Squadron mode's wingman pick.
//
// In the Squadron mode, every RunController.SquadronPickEvery-th level the
// pick is not an upgrade but an allied ship. Two steps on one screen, with the
// game frozen like any other pick: first one of up to three ships, then one of
// the three slots in a triangle around the main ship (top, bottom-left,
// bottom-right). Slots already taken show their ship and cannot be chosen.
// Runtime-only, built the first time it is needed, like the menu pages.

using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private RectTransform wingmanScreen;
        private TMP_Text wingmanCaption;
        private TMP_Text wingmanTitle;
        private TMP_Text wingmanHint;
        private RectTransform wingmanOfferRow;
        private RectTransform wingmanCornerGrid;
        private Image wingmanCenterHull;
        private Button wingmanBack;
        private readonly List<Button> wingmanOfferButtons = new List<Button>();
        private readonly List<Button> wingmanCornerButtons = new List<Button>();
        private readonly List<ShipDefinition> wingmanOccupants = new List<ShipDefinition>();
        private ShipDefinition wingmanChosen;

        private static readonly Vector2 WingmanOfferSize = new Vector2(420f, 520f);
        private const float WingmanOfferGap = 36f;
        // A triangle like the one in the arena: one slot above the main ship,
        // two below it to the sides; the ship stays visible in the middle and
        // the grid clears the TROCAR NAVE button below.
        private static float WingmanCornerSize => UIFactory.Pick(210f, 280f);
        private static float WingmanCenterSize => UIFactory.Pick(140f, 170f);

        private static Vector2 WingmanSlotPosition(int slot)
        {
            if (slot == 0) return new Vector2(0f, UIFactory.Pick(175f, 230f));
            var side = slot == 1 ? -1f : 1f;
            return new Vector2(side * UIFactory.Pick(210f, 250f), -UIFactory.Pick(115f, 140f));
        }

        private void BuildWingmanScreen()
        {
            if (wingmanScreen != null) return;
            wingmanOfferButtons.Clear();
            wingmanCornerButtons.Clear();
            var parent = evolutionScreen != null ? evolutionScreen.parent : safeRoot;
            if (parent == null) return;

            Image backdrop;
            wingmanScreen = UIFactory.Page(parent, "WingmanScreen", new Color(0.015f, 0.025f, 0.05f, 1f), out backdrop);
            // Same layer as the upgrade pick: under the pause and end screens built after it.
            if (evolutionScreen != null && evolutionScreen.parent == parent)
                wingmanScreen.SetSiblingIndex(evolutionScreen.GetSiblingIndex() + 1);
            UIFactory.Group(wingmanScreen);
            SweepGlow(wingmanScreen, "GlowTop", true, 420f, WithAlpha(UIFactory.Accent, 0.2f));
            SweepGlow(wingmanScreen, "GlowBottom", false, 320f, WithAlpha(UIFactory.Accent, 0.14f));

            var textWidth = UIFactory.Pick(1500f, 1020f);
            wingmanCaption = UIFactory.Label(wingmanScreen, "Caption", "", UIFactory.TextCaption, UIFactory.Accent,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(wingmanCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(textWidth, 36f));
            wingmanCaption.characterSpacing = 14f;

            wingmanTitle = UIFactory.Label(wingmanScreen, "Title", "", UIFactory.Pick(58, 50), UIFactory.Evolved,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(wingmanTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(textWidth, 72f));
            wingmanTitle.characterSpacing = UIFactory.Pick(10f, 6f);
            wingmanTitle.enableAutoSizing = true;
            wingmanTitle.fontSizeMin = 36f;
            wingmanTitle.fontSizeMax = UIFactory.Pick(58f, 50f);

            wingmanHint = UIFactory.Label(wingmanScreen, "Hint", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(wingmanHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(textWidth, 90f));
            wingmanHint.enableAutoSizing = true;
            wingmanHint.fontSizeMin = UIFactory.TextCaption - 6f;
            wingmanHint.fontSizeMax = UIFactory.TextBody;

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");

            // Step 1: the ships on offer.
            wingmanOfferRow = UIFactory.NewRect(wingmanScreen, "Offers");
            UIFactory.Place(wingmanOfferRow, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1500f, 600f));
            for (var i = 0; i < CardSystem.ChoiceCount; i++)
            {
                var index = i;
                var button = BuildWingmanOffer(wingmanOfferRow, i, cutFrame);
                Rebind(button, () => HandleWingmanOffer(index));
                wingmanOfferButtons.Add(button);
            }

            // Step 2: the corners around the main ship.
            wingmanCornerGrid = UIFactory.NewRect(wingmanScreen, "Corners");
            UIFactory.Place(wingmanCornerGrid, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(-30f, -40f)),
                new Vector2(1000f, 800f));
            var center = UIFactory.Rect(wingmanCornerGrid, "CenterPlate", new Color(0.02f, 0.03f, 0.06f, 0.95f), ArtLibrary.CircleFilled);
            center.raycastTarget = false;
            UIFactory.Place(center.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * WingmanCenterSize);
            UIFactory.GlowShadow(center, WithAlpha(UIFactory.Accent, 0.45f), 24f, 0.05f);
            wingmanCenterHull = UIFactory.Rect(center.rectTransform, "Hull", UIFactory.Accent, ArtLibrary.Ship);
            wingmanCenterHull.preserveAspect = true;
            wingmanCenterHull.raycastTarget = false;
            UIFactory.Stretch(wingmanCenterHull.rectTransform, WingmanCenterSize * 0.18f);

            for (var slot = 0; slot < SquadronWing.SlotCount; slot++)
            {
                var index = slot;
                var offset = WingmanSlotPosition(slot);
                var button = UIFactory.Button(wingmanCornerGrid, "Corner" + slot, "", UIFactory.PanelDeep, UIFactory.Ink,
                    UIFactory.TextCaption, Sound.UpgradeChosen);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), offset,
                    new Vector2(WingmanCornerSize, WingmanCornerSize));
                StyleCutAction(button, cutFrame, WithAlpha(UIFactory.Accent, 0.8f), UIFactory.ActionFill);
                var hull = UIFactory.Rect(button.transform, "Hull", UIFactory.Ink, ArtLibrary.Ship);
                hull.preserveAspect = true;
                hull.raycastTarget = false;
                UIFactory.Place(hull.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f),
                    Vector2.one * WingmanCornerSize * 0.48f);
                var caption = button.transform.Find("Caption") as RectTransform;
                if (caption != null)
                {
                    caption.anchorMin = new Vector2(0f, 0f);
                    caption.anchorMax = new Vector2(1f, 0f);
                    caption.pivot = new Vector2(0.5f, 0f);
                    caption.anchoredPosition = new Vector2(0f, 18f);
                    caption.sizeDelta = new Vector2(-24f, 60f);
                }
                Rebind(button, () => HandleWingmanCorner(index));
                wingmanCornerButtons.Add(button);
            }

            wingmanBack = UIFactory.Button(wingmanScreen, "Back", "", UIFactory.PanelDeep, UIFactory.Ink,
                UIFactory.TextButton, Sound.Back);
            UIFactory.Place(wingmanBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(480f, 108f));
            StyleCutAction(wingmanBack, cutFrame, WithAlpha(UIFactory.Accent, 0.75f), UIFactory.ActionFill);
            Rebind(wingmanBack, () => ShowWingmanStep(null));

            wingmanScreen.gameObject.SetActive(false);
        }

        private Button BuildWingmanOffer(RectTransform row, int index, Sprite cutFrame)
        {
            var button = UIFactory.Button(row, "Offer" + index, "", UIFactory.PanelDeep, UIFactory.Ink,
                UIFactory.TextCaption, Sound.Select);
            var rect = button.GetComponent<RectTransform>();
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, WingmanOfferSize);
            StyleCutAction(button, cutFrame, WithAlpha(UIFactory.Accent, 0.8f), UIFactory.ActionFill, false, KitRole.Card);
            UIFactory.DropShadow(button.GetComponent<Image>(), 24f, 8f, 0.55f);

            var plate = UIFactory.Rect(rect, "Plate", new Color(0.02f, 0.03f, 0.06f, 0.9f), ArtLibrary.CircleFilled);
            plate.raycastTarget = false;
            UIFactory.Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(220f, 220f));
            var hull = UIFactory.Rect(plate.rectTransform, "Hull", UIFactory.Ink, ArtLibrary.Ship);
            hull.preserveAspect = true;
            hull.raycastTarget = false;
            UIFactory.Stretch(hull.rectTransform, 34f);

            var name = UIFactory.Label(rect, "Name", "", 40, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(WingmanOfferSize.x - 40f, 56f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 26f;
            name.fontSizeMax = 40f;
            name.raycastTarget = false;

            var stats = UIFactory.Label(rect, "Stats", "", 30, UIFactory.InkDim, TextAlignmentOptions.Top, false);
            UIFactory.Place(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(WingmanOfferSize.x - 50f, 140f));
            stats.enableAutoSizing = true;
            stats.fontSizeMin = 22f;
            stats.fontSizeMax = 30f;
            stats.lineSpacing = 6f;
            stats.raycastTarget = false;

            // The default caption is not used: name and stats carry the card.
            var caption = button.transform.Find("Caption");
            if (caption != null) caption.gameObject.SetActive(false);
            return button;
        }

        private void ShowWingmanScreen()
        {
            BuildWingmanScreen();
            if (wingmanScreen == null) return;
            wingmanChosen = null;
            if (run.wing != null) run.wing.CopyOccupants(wingmanOccupants);
            else wingmanOccupants.Clear();
            if (run.shipDefinition != null)
            {
                wingmanCenterHull.sprite = ArtLibrary.ShipFor(run.shipDefinition.id);
                wingmanCenterHull.color = ArtLibrary.ShipTint(run.shipDefinition.id, SpriteFactory.ParseColor(run.shipDefinition.tint));
            }

            wingmanCaption.text = RunController.SquadronName + "  ·  " + L("NÍVEL ", "LEVEL ", "NIVEL ") + run.claimingLevel;
            ShowWingmanStep(null);

            wingmanScreen.gameObject.SetActive(true);
            FadeScreenIn(UIFactory.Group(wingmanScreen));
            Sfx.Play(Sound.CardsDealt);
            Juice.PopIn(wingmanTitle.transform, 1f, 0.4f, 0f, true);
        }

        /// <summary>Null: step 1 (pick a ship). A ship: step 2 (pick its corner).</summary>
        private void ShowWingmanStep(ShipDefinition chosen)
        {
            wingmanChosen = chosen;
            var picking = chosen == null;
            wingmanOfferRow.gameObject.SetActive(picking);
            wingmanCornerGrid.gameObject.SetActive(!picking);
            wingmanBack.gameObject.SetActive(!picking);
            SetButtonLabel(wingmanBack, L("TROCAR NAVE", "CHANGE SHIP", "CAMBIAR NAVE"));

            if (picking)
            {
                wingmanTitle.text = L("NAVE ALIADA", "ALLIED SHIP", "NAVE ALIADA");
                wingmanHint.text = string.Format(L(
                    "No lugar da melhoria deste nível, escolha uma nave para voar ao seu lado. Posições livres: {0} de {1}.",
                    "Instead of this level's upgrade, pick a ship to fly at your side. Free slots: {0} of {1}.",
                    "En lugar de la mejora de este nivel, elige una nave para volar a tu lado. Posiciones libres: {0} de {1}."),
                    run.wing != null ? run.wing.FreeSlots : 0, SquadronWing.SlotCount);
                RefreshWingmanOffers();
            }
            else
            {
                wingmanTitle.text = L("ESCOLHA A POSIÇÃO", "CHOOSE THE SLOT", "ELIGE LA POSICIÓN");
                wingmanHint.text = string.Format(L("Onde a {0} vai ficar em relação à sua nave?",
                    "Where should {0} fly around your ship?", "¿Dónde volará {0} alrededor de tu nave?"),
                    GameLocalization.Content(chosen.name).ToUpperInvariant());
                RefreshWingmanCorners();
            }
        }

        private void RefreshWingmanOffers()
        {
            var offers = run.WingmanOffers;
            var count = Mathf.Max(1, offers.Count);
            var bounds = wingmanScreen.rect;
            var width = bounds.width > 0f ? bounds.width : UIFactory.ReferenceWidth;
            var scale = Mathf.Min(1f, (width - 80f) / (count * WingmanOfferSize.x + (count - 1) * WingmanOfferGap));
            var step = (WingmanOfferSize.x + WingmanOfferGap) * scale;
            for (var i = 0; i < wingmanOfferButtons.Count; i++)
            {
                var button = wingmanOfferButtons[i];
                var visible = i < offers.Count;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                var ship = offers[i];
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2((i - (offers.Count - 1) * 0.5f) * step, 0f);
                rect.localScale = Vector3.one * scale;
                button.interactable = true;

                var tint = ArtLibrary.ShipTint(ship.id, SpriteFactory.ParseColor(ship.tint));
                var hull = button.transform.Find("Plate/Hull").GetComponent<Image>();
                hull.sprite = ArtLibrary.ShipFor(ship.id);
                hull.color = tint;
                var name = button.transform.Find("Name").GetComponent<TMP_Text>();
                name.text = GameLocalization.Content(ship.name).ToUpperInvariant();
                name.color = tint;

                var damage = ship.damagePerShot * SquadronWing.DamageShare * Mathf.Max(1, ship.baseProjectiles);
                var rate = 1f / Mathf.Max(0.01f, ship.fireInterval * SquadronWing.FireIntervalScale);
                SetLabel(button.transform, "Stats",
                    L("DANO ", "DAMAGE ", "DAÑO ") + damage.ToString("0.#") + "\n" +
                    L("TIROS/S ", "SHOTS/S ", "DISPAROS/S ") + rate.ToString("0.#") + "\n" +
                    L("DPS ", "DPS ", "DPS ") + "<color=" + UIFactory.ColorTag(UIFactory.Evolved) + ">" +
                    (damage * rate).ToString("0") + "</color>");

                LeanTween.cancel(button.gameObject, false, TweenAction.SCALE);
                Juice.PopIn(button.transform, scale, 0.3f + i * 0.06f, 0f, true);
            }
        }

        private void RefreshWingmanCorners()
        {
            for (var slot = 0; slot < wingmanCornerButtons.Count; slot++)
            {
                var button = wingmanCornerButtons[slot];
                var occupant = slot < wingmanOccupants.Count ? wingmanOccupants[slot] : null;
                var free = occupant == null;
                button.interactable = free;
                var hull = button.transform.Find("Hull").GetComponent<Image>();
                var shown = free ? wingmanChosen : occupant;
                hull.sprite = ArtLibrary.ShipFor(shown.id);
                var tint = ArtLibrary.ShipTint(shown.id, SpriteFactory.ParseColor(shown.tint));
                // A free corner previews the chosen ship faintly; a taken one shows its ship, dimmed.
                hull.color = free ? WithAlpha(tint, 0.4f) : WithAlpha(tint, 0.85f);
                button.GetComponent<Image>().color = WithAlpha(free ? UIFactory.Accent : Palette.Fosforo, free ? 0.85f : 0.3f);
                SetButtonLabel(button, free ? L("LIVRE", "FREE", "LIBRE")
                    : GameLocalization.Content(occupant.name).ToUpperInvariant());
            }
        }

        private void HandleWingmanOffer(int index)
        {
            if (run.phase != RunPhase.UpgradeSelection || !run.isWingmanPick) return;
            var offers = run.WingmanOffers;
            if (index < 0 || index >= offers.Count) return;
            ShowWingmanStep(offers[index]);
        }

        private void HandleWingmanCorner(int slot)
        {
            if (run.phase != RunPhase.UpgradeSelection || !run.isWingmanPick || wingmanChosen == null) return;
            for (var i = 0; i < wingmanCornerButtons.Count; i++) wingmanCornerButtons[i].interactable = false;
            if (!run.ConfirmWingman(wingmanChosen, slot))
            {
                Sfx.Play(Sound.Denied);
                RefreshWingmanCorners();
            }
        }

        private void HideWingmanScreen()
        {
            if (wingmanScreen != null) wingmanScreen.gameObject.SetActive(false);
            wingmanChosen = null;
        }
    }
}
