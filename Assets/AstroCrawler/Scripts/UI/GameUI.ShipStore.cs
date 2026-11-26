// AstroCrawler - hangar: a locked paid ship offers two buttons side by side,
// credits (ore) on the left and real money (store price) on the right.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const float ShipBuyGap = 12f;
        private ShipPurchases shipStore;

        private void BindShipStore()
        {
            shipStore = ShipPurchases.Instance;
            if (shipStore == null) return;
            shipStore.Changed -= HandleShipStoreChanged;
            shipStore.Changed += HandleShipStoreChanged;
            shipStore.Purchased -= HandleShipPurchased;
            shipStore.Purchased += HandleShipPurchased;
        }

        private void UnbindShipStore()
        {
            if (shipStore == null) return;
            shipStore.Changed -= HandleShipStoreChanged;
            shipStore.Purchased -= HandleShipPurchased;
        }

        private void HandleShipStoreChanged()
        {
            if (this == null) return;
            if (shipScreen != null && shipScreen.gameObject.activeInHierarchy) RefreshSelectedShip();
        }

        private void HandleShipPurchased(string shipId)
        {
            if (this == null || config == null || config.GetShip(shipId) == null) return;
            Sfx.Play(Sound.Purchase);
            Juice.ScreenFlash(new Color(1f, 0.85f, 0.45f, 0.35f), 0.45f);
            var index = shipButtons.FindIndex(entry => entry != null && entry.shipId == shipId);
            if (index >= 0 && index < shipPanels.Count) Juice.Punch(shipPanels[index], 0.06f, 0.4f);
            RefreshMainMenu();
            SelectShip(shipId);
        }

        /// <summary>
        /// Called for every ship card by RefreshSelectedShip: splits the band
        /// into credits + money for a locked paid ship, restores the single
        /// select button otherwise.
        /// </summary>
        private void RefreshShipMoneyButton(ShipButton entry, bool owned)
        {
            var select = entry.button.GetComponent<RectTransform>();
            var band = select.parent as RectTransform;
            if (band == null) return;
            var full = new Vector2(band.rect.width - 42f, select.sizeDelta.y);
            var money = band.Find("BuyMoney");
            var split = !owned && ShipUnlocks.IsAvailableForAcquisition(entry.shipId) && ShipPurchases.IsPaidShip(entry.shipId);

            if (!split)
            {
                if (money != null) money.gameObject.SetActive(false);
                UIFactory.Place(select, new Vector2(0.5f, 0.5f), Vector2.zero, full);
                ShowCreditIcon(entry.button, false);
                return;
            }

            var half = new Vector2((full.x - ShipBuyGap) * 0.5f, full.y);
            UIFactory.Place(select, new Vector2(0.5f, 0.5f), new Vector2(-(half.x + ShipBuyGap) * 0.5f, 0f), half);
            ShowCreditIcon(entry.button, true);
            SetButtonLabel(entry.button, ShipUnlocks.Price.ToString());

            Button moneyButton;
            if (money == null)
            {
                var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
                moneyButton = UIFactory.Button(band, "BuyMoney", "", Darken(UIFactory.Evolved, 0.42f), UIFactory.Ink,
                    UIFactory.TextButton, Sound.Tap);
                StyleCutAction(moneyButton, cutFrame, UIFactory.Evolved, Darken(UIFactory.Evolved, 0.3f));
                UIFactory.DropShadow(moneyButton.GetComponent<Image>(), 16f, 5f, 0.5f);
                AddButtonIcon(moneyButton, ArtLibrary.IconPremium, UIFactory.Evolved);
                money = moneyButton.transform;
            }
            else moneyButton = money.GetComponent<Button>();
            money.gameObject.SetActive(true);
            UIFactory.Place(money.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2((half.x + ShipBuyGap) * 0.5f, 0f), half);
            var shipId = entry.shipId;
            Rebind(moneyButton, () => BuyShipWithMoney(shipId));

            var price = shipStore != null ? shipStore.PriceOf(shipId) : "";
            var loading = shipStore != null && shipStore.Busy;
            // Use the store's formatted price verbatim, including local currency and separators.
            SetButtonLabel(moneyButton, !string.IsNullOrEmpty(price) ? price : loading ? "..."
                : L("INDISPONÍVEL", "UNAVAILABLE", "NO DISPONIBLE"));
            var priceLabel = moneyButton.transform.Find("Caption").GetComponent<TMP_Text>();
            priceLabel.enableAutoSizing = true;
            priceLabel.fontSizeMin = 22f;
            priceLabel.fontSizeMax = 32f;
            priceLabel.characterSpacing = 0f;
            priceLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var available = shipStore != null && shipStore.CanBuy(shipId);
            moneyButton.interactable = available;
            moneyButton.GetComponent<Image>().color = WithAlpha(UIFactory.Evolved, available ? 1f : 0.5f);
        }

        private void BuyShipWithMoney(string shipId)
        {
            var index = shipButtons.FindIndex(entry => entry != null && entry.shipId == shipId);
            var band = index >= 0 && shipButtons[index].button != null ? shipButtons[index].button.transform.parent : null;
            var money = band != null ? band.Find("BuyMoney") : null;
            if (shipStore != null && shipStore.CanBuy(shipId))
            {
                shipStore.Buy(shipId);
                RefreshSelectedShip();
                return;
            }

            // No store here (Editor, no Google Play / App Store account, product not active yet).
            Sfx.Play(Sound.Denied);
            if (money == null) return;
            SetButtonLabel(money, shipStore != null && shipStore.Busy
                ? L("AGUARDE", "WAIT", "ESPERA")
                : L("LOJA INDISPONÍVEL", "STORE UNAVAILABLE", "TIENDA NO DISPONIBLE"));
            Juice.Punch(money, 0.08f, 0.3f);
            LeanTween.delayedCall(money.gameObject, 2f, () =>
            {
                if (money != null) RefreshSelectedShip();
            }).setIgnoreTimeScale(true);
        }

        /// <summary>The ore jewel on the left of the credits button; the caption makes room for it.</summary>
        private static void ShowCreditIcon(Button button, bool show)
        {
            var icon = button.transform.Find("PriceIcon");
            if (icon == null && !show) return;
            if (icon == null) icon = AddButtonIcon(button, ArtLibrary.IconCredits, UIFactory.Evolved).transform;
            icon.gameObject.SetActive(show);
            var caption = button.transform.Find("Caption") as RectTransform;
            if (caption != null) caption.offsetMin = new Vector2(show ? 64f : 8f, caption.offsetMin.y);
        }

        private static Image AddButtonIcon(Button button, Sprite sprite, Color color)
        {
            var icon = UIFactory.Rect(button.transform, "PriceIcon", color, sprite);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 0f);
            rect.sizeDelta = new Vector2(40f, 40f);
            var caption = button.transform.Find("Caption") as RectTransform;
            if (caption != null) caption.offsetMin = new Vector2(64f, caption.offsetMin.y);
            return icon;
        }
    }
}
