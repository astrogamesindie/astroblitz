using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        [Serializable] private sealed class ReleaseLinks
        {
            public string privacyPolicyUrl;
            public string supportUrl;
            public string supportEmail;
        }
        private RectTransform privacyPopup;
        private Button privacyAccess, deleteAccountButton;
        private TMP_Text privacyDescription;
        private bool confirmAccountDeletion;
        private bool PrivacyPopupOpen => privacyPopup != null && privacyPopup.gameObject.activeSelf;

        private void BuildPrivacyAccess()
        {
            if (settingsPage == null || privacyAccess != null) return;
            privacyAccess = UIFactory.Button(settingsPage, "PrivacyAndAccount", "", UIFactory.ActionBorder, UIFactory.Ink,
                UIFactory.TextCaption, Sound.Navigate);
            UIFactory.Place(privacyAccess.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(440f, 74f));
            StyleCutAction(privacyAccess, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), UIFactory.ActionBorder, UIFactory.ActionFill, false);
            SetButtonLabel(privacyAccess, L("PRIVACIDADE E CONTA", "PRIVACY & ACCOUNT", "PRIVACIDAD Y CUENTA"));
            Rebind(privacyAccess, OpenPrivacyPopup);
        }

        private void OpenPrivacyPopup()
        {
            if (privacyPopup != null) Destroy(privacyPopup.gameObject);
            confirmAccountDeletion = false;
            privacyPopup = UIFactory.Page(safeRoot, "PrivacyPopup", new Color(0.005f, 0.01f, 0.025f, 0.94f), out var backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, ClosePrivacyPopup);
            var sprite = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(privacyPopup, "Panel", UIFactory.ActionBorder, sprite);
            panel.pixelsPerUnitMultiplier = 0.2f;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1000f));
            var surface = UIFactory.Panel9(panel.transform, "Surface", UIFactory.ActionFillMuted, sprite);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(panel, surface, KitRole.Painel);
            var title = UIFactory.Label(panel.transform, "Title", L("PRIVACIDADE E CONTA", "PRIVACY & ACCOUNT", "PRIVACIDAD Y CUENTA"),
                UIFactory.TextHeading, UIFactory.Accent, TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(740f, 80f));
            privacyDescription = UIFactory.Label(panel.transform, "Description",
                L("Gerencie seus dados e sua conta. Excluir a conta do jogo apaga o progresso local e na nuvem.",
                  "Manage your data and account. Deleting your game account erases local and cloud progress.",
                  "Gestiona tus datos y tu cuenta. Eliminar la cuenta del juego borra el progreso local y en la nube."),
                UIFactory.TextBody, UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(privacyDescription.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(760f, 150f));

            var resource = Resources.Load<TextAsset>("AstroCrawler/ReleaseLinks");
            var links = resource != null ? JsonUtility.FromJson<ReleaseLinks>(resource.text) : new ReleaseLinks();
            var policy = PrivacyAction(panel.rectTransform, "Policy", L("POLÍTICA DE PRIVACIDADE", "PRIVACY POLICY", "POLÍTICA DE PRIVACIDAD"), 320f);
            policy.interactable = Uri.TryCreate(links.privacyPolicyUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https";
            Rebind(policy, () => Application.OpenURL(links.privacyPolicyUrl));
            var support = PrivacyAction(panel.rectTransform, "Support", L("SUPORTE", "SUPPORT", "SOPORTE"), 432f);
            support.interactable = Uri.TryCreate(links.supportUrl, UriKind.Absolute, out var supportUri) && supportUri.Scheme == "https";
            Rebind(support, () => Application.OpenURL(links.supportUrl));
            var unity = PrivacyAction(panel.rectTransform, "UnityAccount", L("GERENCIAR CONTA UNITY", "MANAGE UNITY ACCOUNT", "GESTIONAR CUENTA UNITY"), 544f);
            Rebind(unity, () => Application.OpenURL("https://player-account.unity.com/delete-account"));
            deleteAccountButton = PrivacyAction(panel.rectTransform, "DeleteGameAccount", L("EXCLUIR CONTA DO JOGO", "DELETE GAME ACCOUNT", "ELIMINAR CUENTA DEL JUEGO"), 656f);
            Rebind(deleteAccountButton, ConfirmDeleteAccount);
            var note = UIFactory.Label(panel.transform, "Note",
                L("A conta Unity é compartilhada com outros jogos e pode ser gerenciada separadamente. Compras da loja não são apagadas.",
                  "Your Unity account is shared with other games and can be managed separately. Store purchases are not deleted.",
                  "La cuenta Unity se comparte con otros juegos y se gestiona por separado. Las compras de la tienda no se borran."),
                UIFactory.TextCaption, UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(760f, 150f));
            var close = UIFactory.RoundButton(panel.transform, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep, UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(close, ClosePrivacyPopup);
            RefreshPrivacyAccount();
            privacyPopup.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(privacyPopup), 0.18f);
        }

        private Button PrivacyAction(RectTransform panel, string name, string caption, float top)
        {
            var button = UIFactory.Button(panel, name, caption, UIFactory.ActionBorder, UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(760f, 92f));
            StyleCutAction(button, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), UIFactory.ActionBorder, UIFactory.ActionFill, false);
            return button;
        }

        private void ConfirmDeleteAccount()
        {
            var account = AccountService.Instance;
            if (account == null || !account.SignedIn || account.Busy) return;
            if (!confirmAccountDeletion)
            {
                confirmAccountDeletion = true;
                privacyDescription.text = L("Excluir permanentemente sua conta do jogo e todo o progresso? Esta ação não pode ser desfeita.",
                    "Permanently delete your game account and all progress? This action cannot be undone.",
                    "¿Eliminar permanentemente tu cuenta del juego y todo el progreso? Esta acción no se puede deshacer.");
                SetButtonLabel(deleteAccountButton, L("CONFIRMAR EXCLUSÃO", "CONFIRM DELETION", "CONFIRMAR ELIMINACIÓN"));
                TintSettingsAction(deleteAccountButton, true, true);
                return;
            }
            account.DeleteGameAccount();
        }

        private void RefreshPrivacyAccount()
        {
            if (!PrivacyPopupOpen || deleteAccountButton == null) return;
            var account = AccountService.Instance;
            deleteAccountButton.interactable = account != null && account.SignedIn && !account.Busy;
            if (confirmAccountDeletion && account != null && !account.Busy && !string.IsNullOrEmpty(account.LastError))
                privacyDescription.text = L("Não foi possível concluir. Verifique a conexão e tente novamente.",
                    "Couldn't complete the request. Check your connection and try again.",
                    "No se pudo completar. Comprueba la conexión e inténtalo de nuevo.");
        }

        private void ClosePrivacyPopup()
        {
            if (AccountService.Instance != null && AccountService.Instance.Busy && confirmAccountDeletion) return;
            if (privacyPopup != null) privacyPopup.gameObject.SetActive(false);
            confirmAccountDeletion = false;
        }
    }
}
