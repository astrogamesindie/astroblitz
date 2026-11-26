// AstroCrawler - Starter Pack on the main menu: a pulsing offer button with
// a 24 h countdown (under the gift) and the sheet that sells the pack.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        // Oferta da loja é Ácido (PILARES_DE_UI §3); o rosa não tinha papel na paleta.
        private static readonly Color StarterAccent = Palette.Acido;

        private Button starterMenuButton;
        private TMP_Text starterMenuTimer;
        private RectTransform starterPopup;
        private RectTransform starterPanel;
        private TMP_Text starterTimerLine;
        private TMP_Text starterStatus;
        private TMP_Text starterShipCaption;
        private TMP_Text starterShipMark;
        private Image starterShipHull;
        private Button starterBuyButton;
        private float starterNextTick;
        private float starterNextPunch;

        private bool StarterPopupOpen => starterPopup != null && starterPopup.gameObject.activeSelf;

        private void BuildStarterPack()
        {
            if (starterPopup != null || mainMenu == null) return;

            // --- Menu button: right column, under the gift. ---
            starterMenuButton = UIFactory.RoundButton(mainMenu, "StarterPack", ArtLibrary.Ship, UIFactory.PanelSoft,
                UIFactory.Touch, Sound.Navigate);
            var corner = starterMenuButton.GetComponent<RectTransform>();
            UIFactory.Place(corner, new Vector2(1f, 1f), new Vector2(-32f, -32f - UIFactory.Touch - 24f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            var halo = UIFactory.Rect(corner, "Halo", WithAlpha(StarterAccent, 0.55f), ArtLibrary.Glow);
            UIFactory.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 230f));
            halo.raycastTarget = false;
            halo.transform.SetAsFirstSibling();
            Juice.BreatheUI(halo, 0.25f, 0.75f, 0.9f);
            UIFactory.GlowShadow(starterMenuButton.GetComponent<Image>(), WithAlpha(StarterAccent, 0.45f), 24f, 0.08f);
            var ring = corner.Find("Ring");
            if (ring != null) ring.GetComponent<Image>().color = StarterAccent;
            var icon = corner.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = UIFactory.Evolved;

            var ribbon = UIFactory.Pill(corner, "Ribbon", L("OFERTA", "OFFER", "OFERTA"), StarterAccent,
                Palette.Vazio, 34f, 20);
            UIFactory.Place(ribbon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(140f, 34f));
            var timerPill = UIFactory.Pill(corner, "Timer", "24:00:00", Palette.Vidro(Palette.Acido, 0.06f, 0.92f),
                UIFactory.Ink, 40f, 22);
            UIFactory.Place(timerPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -UIFactory.Touch - 8f),
                new Vector2(176f, 40f));
            starterMenuTimer = timerPill.transform.Find("Label").GetComponent<TMP_Text>();
            starterMenuTimer.characterSpacing = 2f;
            Rebind(starterMenuButton, OpenStarterPopup);

            // --- The sheet. ---
            Image backdrop;
            starterPopup = UIFactory.Page(safeRoot, "StarterPopup", new Color(0.01f, 0.015f, 0.03f, 0.88f), out backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, CloseStarterPopup);

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(starterPopup, "Panel", StarterAccent, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            starterPanel = panel.rectTransform;
            panel.raycastTarget = true;
            UIFactory.Place(starterPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 1040f));
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            UIFactory.GlowShadow(panel, WithAlpha(StarterAccent, 0.35f), 40f, 0.1f);
            var surface = UIFactory.Panel9(starterPanel, "Surface", Palette.Vidro(Palette.Acido, 0.06f), cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(panel, surface, KitRole.Painel);
            var rule = UIFactory.Rect(starterPanel, "TopRule", StarterAccent, ArtLibrary.Pixel);
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            rule.raycastTarget = false;

            var title = UIFactory.Label(starterPanel, "Title", "STARTER PACK", UIFactory.TextHeading + 8, UIFactory.Evolved,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 76f));
            title.characterSpacing = 6f;
            UIFactory.GlowShadow(title, WithAlpha(UIFactory.Evolved, 0.45f), 22f, 0.05f);

            starterTimerLine = UIFactory.Label(starterPanel, "Timer", "", UIFactory.TextBody, StarterAccent,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(starterTimerLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(820f, 48f));
            starterTimerLine.characterSpacing = 3f;

            // Two prize slots with a "+" between them.
            var slotSize = new Vector2(370f, 430f);
            var shipSlot = StarterSlot(cutFrame, "ShipSlot", -205f, slotSize, StarterAccent);
            starterShipHull = UIFactory.Rect(shipSlot, "Hull", Color.black, ArtLibrary.Ship);
            starterShipHull.preserveAspect = true;
            starterShipHull.raycastTarget = false;
            UIFactory.Place(starterShipHull.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(210f, 210f));
            UIFactory.GlowShadow(starterShipHull, WithAlpha(StarterAccent, 0.7f), 26f, 0.06f);
            starterShipMark = UIFactory.Label(shipSlot, "Mystery", "?", 120, UIFactory.Evolved, TextAlignmentOptions.Center, true);
            UIFactory.Place(starterShipMark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(200f, 150f));
            UIFactory.AddUnderlay(starterShipMark);
            starterShipCaption = UIFactory.Label(shipSlot, "Caption", "", UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(starterShipCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(330f, 90f));
            starterShipCaption.enableAutoSizing = true;
            starterShipCaption.fontSizeMin = 26f;
            starterShipCaption.fontSizeMax = UIFactory.TextBody;
            var shipHint = UIFactory.Label(shipSlot, "Hint", "", UIFactory.TextCaption - 4, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(shipHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(330f, 64f));
            shipHint.enableAutoSizing = true;
            shipHint.fontSizeMin = 20f;
            shipHint.fontSizeMax = UIFactory.TextCaption - 4;

            var oreSlot = StarterSlot(cutFrame, "OreSlot", 205f, slotSize, UIFactory.Evolved);
            var jewel = UIFactory.Rect(oreSlot, "Jewel", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            jewel.raycastTarget = false;
            UIFactory.Place(jewel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(170f, 170f));
            UIFactory.GlowShadow(jewel, WithAlpha(UIFactory.Evolved, 0.6f), 26f, 0.06f);
            var amount = UIFactory.Label(oreSlot, "Amount", "", 64, UIFactory.Evolved, TextAlignmentOptions.Center, true);
            UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(330f, 80f));
            UIFactory.AddUnderlay(amount);
            var oreCaption = UIFactory.Label(oreSlot, "Caption", "", UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(oreCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(330f, 56f));
            oreCaption.enableAutoSizing = true;
            oreCaption.fontSizeMin = 26f;
            oreCaption.fontSizeMax = UIFactory.TextBody;

            var plus = UIFactory.Label(starterPanel, "Plus", "+", 84, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(plus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(80f, 100f));
            UIFactory.AddUnderlay(plus);

            starterStatus = UIFactory.Label(starterPanel, "Status", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.Center);
            UIFactory.Place(starterStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(820f, 80f));

            starterBuyButton = UIFactory.Button(starterPanel, "Buy", "", UIFactory.Primary, UIFactory.Ink,
                UIFactory.TextButton, Sound.Tap);
            UIFactory.Place(starterBuyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 48f),
                new Vector2(760f, 124f));
            StyleCutAction(starterBuyButton, cutFrame, UIFactory.Evolved, Darken(UIFactory.Evolved, 0.3f));
            UIFactory.DropShadow(starterBuyButton.GetComponent<Image>(), 18f, 6f, 0.5f);
            Rebind(starterBuyButton, BuyStarterPack);

            var close = UIFactory.RoundButton(starterPanel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(close, CloseStarterPopup);

            var store = ShipPurchases.Instance;
            if (store != null)
            {
                store.Changed -= RefreshStarterPack;
                store.Changed += RefreshStarterPack;
                store.StarterPurchased -= HandleStarterPurchased;
                store.StarterPurchased += HandleStarterPurchased;
            }
            starterPopup.gameObject.SetActive(false);
            RefreshStarterPack();
        }

        private RectTransform StarterSlot(Sprite cutFrame, string name, float x, Vector2 size, Color accent)
        {
            var frame = UIFactory.Panel9(starterPanel, name, WithAlpha(accent, 0.85f), cutFrame);
            frame.pixelsPerUnitMultiplier = 0.25f;
            frame.raycastTarget = false;
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -196f), size);
            var fill = UIFactory.Panel9(frame.rectTransform, "Fill", Darken(accent, 0.14f), cutFrame);
            UIFactory.Stretch(fill.rectTransform, 6f);
            fill.pixelsPerUnitMultiplier = 0.25f;
            fill.raycastTarget = false;
            return frame.rectTransform;
        }

        private void UnbindStarterPack()
        {
            var store = ShipPurchases.Instance;
            if (store == null) return;
            store.Changed -= RefreshStarterPack;
            store.StarterPurchased -= HandleStarterPurchased;
        }

        private void OpenStarterPopup()
        {
            if (starterPopup == null) return;
            RefreshStarterPack();
            starterPopup.gameObject.SetActive(true);
            starterPopup.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(starterPopup), 0.18f);
            Juice.PopIn(starterPanel, 1f, 0.28f, 0f, true);
        }

        private void CloseStarterPopup()
        {
            if (starterPopup != null) HideScreen(starterPopup);
        }

        private void BuyStarterPack()
        {
            var store = ShipPurchases.Instance;
            if (StarterPack.Granted) { CloseStarterPopup(); return; }
            if (store != null && store.CanBuyStarter)
            {
                store.BuyStarter();
                RefreshStarterPack();
                return;
            }
            Sfx.Play(Sound.Denied);
            Juice.Punch(starterBuyButton.transform, 0.08f, 0.3f);
        }

        private void HandleStarterPurchased()
        {
            if (this == null) return;
            DeliverStarterPack(true);
        }

        /// <summary>Pays a confirmed pack that is not in the save yet; celebrates when the player just bought it.</summary>
        private void DeliverStarterPack(bool celebrate)
        {
            if (run == null || !StarterPack.PendingGrant) { RefreshStarterPack(); return; }
            var ship = StarterPack.Grant(run.passiveUpgrades, run.shipUnlocks);
            RefreshMainMenu();
            RefreshStarterPack();
            if (!celebrate) return;
            Sfx.Play(Sound.Purchase);
            Juice.ScreenFlash(WithAlpha(StarterAccent, 0.4f), 0.5f);
            if (!StarterPopupOpen) OpenStarterPopup();
            if (starterShipHull != null) Juice.PopIn(starterShipHull.transform, 1.25f, 0.45f, 0f, true);
            if (!string.IsNullOrEmpty(ship) && config.GetShip(ship) != null)
            {
                // The new ship is the one in the hangar now.
                selectedShipId = ship;
                passiveShipId = ship;
                run.SyncStageToShip(ship);
                PlayerPrefs.SetString(SelectedShipKey, ship);
                ProgressSnapshot.SaveLocal();
                RefreshMainMenu();
            }
        }

        private void TickStarterPack()
        {
            if (starterMenuButton == null || Time.unscaledTime < starterNextTick) return;
            starterNextTick = Time.unscaledTime + 0.5f;
            if (StarterPack.PendingGrant) DeliverStarterPack(false);
            RefreshStarterPack();
            if (starterMenuButton.gameObject.activeInHierarchy && Time.unscaledTime >= starterNextPunch)
            {
                starterNextPunch = Time.unscaledTime + 4f;
                Juice.Punch(starterMenuButton.transform, 0.1f, 0.35f);
            }
        }

        private void RefreshStarterPack()
        {
            if (this == null || starterMenuButton == null) return;
            var store = ShipPurchases.Instance;
            var granted = StarterPack.Granted;
            var left = StarterPack.Remaining();
            starterMenuButton.gameObject.SetActive(StarterPack.OfferVisible);
            LayoutMenuCorners();
            starterMenuTimer.text = StarterPack.Clock(left);
            if (starterPanel == null) return;

            starterTimerLine.text = granted
                ? L("RECEBIDO!", "RECEIVED!", "¡RECIBIDO!")
                : left > TimeSpan.Zero
                    ? L("A OFERTA ACABA EM ", "OFFER ENDS IN ", "LA OFERTA TERMINA EN ") + StarterPack.Clock(left)
                    : L("OFERTA ENCERRADA", "OFFER ENDED", "OFERTA TERMINADA");

            var shipId = StarterPack.GrantedShip;
            var ship = granted && !string.IsNullOrEmpty(shipId) ? config.GetShip(shipId) : null;
            if (ship != null)
            {
                var accent = SpriteFactory.ParseColor(ship.tint);
                starterShipHull.sprite = ArtLibrary.ShipFor(ship.id);
                starterShipHull.color = ArtLibrary.ShipTint(ship.id, accent);
                starterShipMark.gameObject.SetActive(false);
                starterShipCaption.text = GameLocalization.Content(ship.name).ToUpperInvariant();
                starterShipCaption.color = accent;
            }
            else
            {
                starterShipHull.sprite = ArtLibrary.Ship;
                starterShipHull.color = Color.black;
                starterShipMark.gameObject.SetActive(!granted);
                starterShipCaption.text = granted
                    // The bonus is a ship's price (StarterPack.Grant); the
                    // translations keep "1000" in their key, swapped here.
                    ? L("+1000 MINÉRIOS (FROTA COMPLETA)", "+1000 CREDITS (FLEET COMPLETE)", "+1000 CRÉDITOS (FLOTA COMPLETA)")
                        .Replace("1000", ShipUnlocks.Price.ToString())
                    : L("1 NAVE ALEATÓRIA", "1 RANDOM SHIP", "1 NAVE ALEATORIA");
                starterShipCaption.color = UIFactory.Ink;
            }
            // The four ships it can be, in the ships' own translated names.
            var names = new System.Collections.Generic.List<string>();
            foreach (var id in ShipPurchases.PaidShips)
            {
                if (!ShipUnlocks.IsAvailableForAcquisition(id)) continue;
                var candidate = config.GetShip(id);
                if (candidate != null) names.Add(GameLocalization.Content(candidate.name));
            }
            SetLabel(starterPanel, "ShipSlot/Hint", granted ? "" : string.Join(" · ", names));
            SetLabel(starterPanel, "OreSlot/Amount", StarterPack.CreditsText());
            SetLabel(starterPanel, "OreSlot/Caption", L("MINÉRIOS", "CREDITS", "CRÉDITOS"));

            var buying = store != null && store.Busy && store.BusyShip == ShipPurchases.StarterBusy;
            var canBuy = store != null && store.CanBuyStarter && left > TimeSpan.Zero;
            SetButtonLabel(starterBuyButton, granted
                ? L("FECHAR", "CLOSE", "CERRAR")
                : buying ? "..."
                : canBuy ? store.StarterPrice
                : L("LOJA INDISPONÍVEL", "STORE UNAVAILABLE", "TIENDA NO DISPONIBLE"));
            starterBuyButton.GetComponent<Image>().color = WithAlpha(UIFactory.Evolved, granted || canBuy ? 1f : 0.45f);

            if (granted)
                starterStatus.text = L("Tudo já está na sua conta.", "Everything is in your account.", "Todo ya está en tu cuenta.");
            else if (store != null && store.Status == "pending")
                starterStatus.text = L("Pagamento pendente. O pacote chega após a confirmação.",
                    "Payment pending. The pack arrives after confirmation.", "Pago pendiente. El paquete llega tras la confirmación.");
            else if (store != null && store.Status == "error")
                starterStatus.text = L("Não foi possível consultar a loja.", "Couldn't contact the store.", "No se pudo consultar la tienda.");
            else
                starterStatus.text = L("Compra única. Só uma vez por conta.", "One-time purchase. Once per account.",
                    "Compra única. Solo una vez por cuenta.");
        }
    }
}
