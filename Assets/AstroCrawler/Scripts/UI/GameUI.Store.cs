using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private AndroidPurchases purchaseStore;
        private RectTransform storePopup;
        private RectTransform storePanel;
        private Button removeAdsButton;
        private Button restorePurchasesButton;
        private Button storeCloseButton;
        private Button menuRemoveAdsButton;
        private TMP_Text storeTitle;
        private TMP_Text storeDescription;
        private TMP_Text storeStatus;

        /// <summary>
        /// Premium lives on the main menu: a crown button opens this
        /// sheet with the purchase, restore and the store's status.
        /// </summary>
        private void BuildStorePanel()
        {
            if (storePopup != null || mainMenu == null) return;
            purchaseStore = AndroidPurchases.EnsureCreated();

            // Premium access: right column, after the gift and the Starter Pack
            // (LayoutMenuCorners stacks them). Under the gear it touched the logo.
            menuRemoveAdsButton = UIFactory.RoundButton(mainMenu, "Premium", ArtLibrary.IconPremium, UIFactory.PanelSoft,
                UIFactory.Touch, Sound.Navigate);
            var badge = menuRemoveAdsButton.GetComponent<RectTransform>();
            UIFactory.Place(badge, new Vector2(1f, 1f), new Vector2(-32f, -32f - UIFactory.Touch - 24f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            UIFactory.GlowShadow(menuRemoveAdsButton.GetComponent<Image>(), WithAlpha(UIFactory.Evolved, 0.25f), 22f, 0.05f);
            badge.Find("Ring").GetComponent<Image>().color = WithAlpha(UIFactory.Evolved, 0.8f);
            badge.Find("Icon").GetComponent<Image>().color = UIFactory.Evolved;
            Rebind(menuRemoveAdsButton, OpenStorePopup);

            // The sheet.
            Image backdrop;
            storePopup = UIFactory.Page(safeRoot, "StorePopup", new Color(0.01f, 0.015f, 0.03f, 0.85f), out backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, CloseStorePopup);

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(storePopup, "AndroidStore", UIFactory.ActionBorder, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            storePanel = panel.rectTransform;
            panel.raycastTarget = true;
            UIFactory.Place(storePanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 860f));
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            var surface = UIFactory.Panel9(storePanel, "Surface", UIFactory.ActionFillMuted, cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(panel, surface, KitRole.Painel);
            var topRule = UIFactory.Rect(storePanel, "TopRule", UIFactory.ActionBorder, ArtLibrary.Pixel);
            UIFactory.Place(topRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            topRule.raycastTarget = false;

            var noAdsIcon = UIFactory.Rect(storePanel, "PremiumIcon", UIFactory.Evolved, ArtLibrary.IconPremium);
            UIFactory.Place(noAdsIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(164f, 164f));
            noAdsIcon.raycastTarget = false;
            for (var side = -1; side <= 1; side += 2)
            {
                var connector = UIFactory.Rect(storePanel, "IconConnector" + side, WithAlpha(UIFactory.Accent, 0.35f), ArtLibrary.Pixel);
                UIFactory.Place(connector.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * 162f, -222f), new Vector2(94f, 3f));
                connector.raycastTarget = false;
                var marker = UIFactory.Rect(storePanel, "IconMarker" + side, UIFactory.ActionBorder, ArtLibrary.Pixel);
                UIFactory.Place(marker.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * 223f, -222f), new Vector2(10f, 10f));
                marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                marker.raycastTarget = false;
            }

            storeTitle = UIFactory.Label(storePanel, "Title", "", UIFactory.TextHeading, UIFactory.Accent,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(storeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 64f));
            storeDescription = UIFactory.Label(storePanel, "Description", "", UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center);
            UIFactory.Place(storeDescription.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(820f, 80f));
            storeStatus = UIFactory.Label(storePanel, "Status", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.Center);
            UIFactory.Place(storeStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(820f, 90f));

            removeAdsButton = UIFactory.Button(storePanel, "BuyRemoveAds", "", UIFactory.Primary,
                UIFactory.Ink, UIFactory.TextButton, Sound.Tap);
            UIFactory.Place(removeAdsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 160f), new Vector2(760f, 120f));
            StyleCutAction(removeAdsButton, cutFrame, UIFactory.ActionBorder, UIFactory.ActionFillStrong);
            restorePurchasesButton = UIFactory.Button(storePanel, "Restore", "", UIFactory.PanelDeep,
                UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(restorePurchasesButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(760f, 100f));
            StyleCutAction(restorePurchasesButton, cutFrame, WithAlpha(UIFactory.ActionBorder, 0.45f), UIFactory.ActionFill);

            storeCloseButton = UIFactory.RoundButton(storePanel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(storeCloseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(40f, 40f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(storeCloseButton, CloseStorePopup);

            Rebind(removeAdsButton, () => purchaseStore?.BuyRemoveAds());
            Rebind(restorePurchasesButton, () =>
            {
                purchaseStore?.RestorePurchases();
                ShipPurchases.Instance?.Restore();
            });
            if (purchaseStore != null)
            {
                purchaseStore.Changed -= RefreshStorePanel;
                purchaseStore.Changed += RefreshStorePanel;
            }
            storePopup.gameObject.SetActive(false);
            RefreshStorePanel();
        }

        private bool StorePopupOpen
        {
            get { return storePopup != null && storePopup.gameObject.activeSelf; }
        }

        private void OpenStorePopup()
        {
            if (storePopup == null) return;
            RefreshStorePanel();
            storePopup.gameObject.SetActive(true);
            storePopup.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(storePopup), 0.18f);
            Juice.PopIn(storePanel, 1f, 0.28f, 0f, true);
        }

        private void CloseStorePopup()
        {
            HideScreen(storePopup);
        }

        private void RefreshStorePanel()
        {
            if (storePanel == null) return;
            var store = purchaseStore;
            var owned = store != null && store.AdsRemoved;
            // Once ads are gone the button on the menu has nothing left to offer.
            if (menuRemoveAdsButton != null) menuRemoveAdsButton.gameObject.SetActive(!owned);
            LayoutMenuCorners();

            storeTitle.text = "PREMIUM";
            storeDescription.text = L("Compra única e permanente. Sem anúncios e recompensas sem vídeo.",
                "One permanent purchase. No ads, and rewards without videos.",
                "Compra única y permanente. Sin anuncios y recompensas sin videos.");
            SetButtonLabel(removeAdsButton, owned ? L("ADQUIRIDO", "PURCHASED", "ADQUIRIDO") :
                store != null && store.ProductAvailable ? store.Price : L("INDISPONÍVEL", "UNAVAILABLE", "NO DISPONIBLE"));
            SetButtonLabel(restorePurchasesButton, L("RESTAURAR COMPRAS", "RESTORE PURCHASES", "RESTAURAR COMPRAS"));
            removeAdsButton.interactable = store != null && store.CanPurchase;
            restorePurchasesButton.interactable = store != null && store.Supported && !store.Busy;
            TintSettingsAction(removeAdsButton, removeAdsButton.interactable);
            TintSettingsAction(restorePurchasesButton, restorePurchasesButton.interactable);
            if (restorePurchasesButton.interactable)
                restorePurchasesButton.transform.Find("Inset").GetComponent<Image>().color = UIFactory.ActionFill;
            if (store == null || !store.Supported)
                storeStatus.text = L("INDISPONÍVEL", "UNAVAILABLE", "NO DISPONIBLE");
            else if (store.Busy)
                storeStatus.text = L("Aguardando Google Play…", "Waiting for Google Play…", "Esperando Google Play…");
            else if (store.Status == "error")
                storeStatus.text = L("Não foi possível consultar a loja. Use Restaurar para tentar novamente.", "Couldn't contact the store. Use Restore to try again.", "No se pudo consultar la tienda. Usa Restaurar para reintentar.");
            else if (store.Status == "pending")
                storeStatus.text = L("Pagamento pendente. A remoção será ativada após a confirmação.", "Payment pending. Ads will be removed after confirmation.", "Pago pendiente. Los anuncios se quitarán tras la confirmación.");
            else if (owned)
                storeStatus.text = L("Anúncios removidos nesta conta.", "Ads removed for this account.", "Anuncios eliminados en esta cuenta.");
            else if (store.Status == "cancelled")
                storeStatus.text = L("Compra cancelada. Nenhuma alteração aplicada.", "Purchase cancelled. No changes applied.", "Compra cancelada. Sin cambios.");
            else
                storeStatus.text = L("Já comprou? Restaure usando a mesma conta Google Play.", "Already purchased? Restore with the same Google Play account.", "¿Ya lo compraste? Restaura con la misma cuenta de Google Play.");
            if (Application.platform == RuntimePlatform.IPhonePlayer)
                storeStatus.text = storeStatus.text.Replace("Google Play", "App Store");
            if (owned) storeStatus.text = "PREMIUM · " + storeStatus.text;
            // Buying (or restoring) Premium turns the video buttons into free rewards right away.
            RefreshAdControls();
            RefreshRewards();
        }
    }
}
