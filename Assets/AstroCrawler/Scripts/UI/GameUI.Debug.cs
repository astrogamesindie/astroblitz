// AstroCrawler - the hidden debug panel (10 taps on the menu logo + password).
// Developer tool: its text is Portuguese only and stays out of the locale catalogs.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
#if UNITY_EDITOR
        // Runtime-only controls: never baked into scenes or compiled into players.
        private void BuildEditorTestControls()
        {
            if (!Application.isPlaying || mainMenu == null || mainMenu.Find("EditorTestControls") != null) return;
            var root = UIFactory.NewRect(mainMenu, "EditorTestControls");
            root.gameObject.tag = "EditorOnly";
            root.gameObject.hideFlags = HideFlags.DontSave;
            UIFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(760f, 72f));

            var reset = UIFactory.Button(root, "ResetProgress", "ZERAR ATRIBUTOS", UIFactory.PanelSoft,
                UIFactory.Ink, 26, Sound.Back);
            UIFactory.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(-195f, 0f), new Vector2(370f, 72f));
            StyleCutAction(reset, ArtLibrary.Frame, UIFactory.Danger, new Color(0.22f, 0.045f, 0.05f), false);
            Rebind(reset, ResetEditorProgress);

            var coins = UIFactory.Button(root, "Add1000Coins", "+1.000 MOEDAS", UIFactory.PanelSoft,
                UIFactory.Ink, 26, Sound.Purchase);
            UIFactory.Place(coins.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(195f, 0f), new Vector2(370f, 72f));
            StyleCutAction(coins, ArtLibrary.Frame, UIFactory.Evolved, new Color(0.2f, 0.14f, 0.025f), false);
            Rebind(coins, () =>
            {
                if (run.phase != RunPhase.ShipSelection) return;
                run.passiveUpgrades.AddRewardedCredits(1000);
                RefreshMainMenu();
            });
        }

        private void ResetEditorProgress()
        {
            if (run.phase != RunPhase.ShipSelection) return;
            ProgressSnapshot.Wipe(config);
            ShipPurchases.EditorClearLocalPurchases();
            DebugMode.SetEnabled(false);
            ProgressSnapshot.SaveLocal();
            // Reload also discards cached upgrade levels, wallet and selected ship.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.path);
        }
#endif

        private const float DebugTapGap = 1.2f;

        private RectTransform debugPopup;
        private TMP_InputField debugInput;
        private TMP_Text debugStatus;
        private int debugTaps;
        private float debugLastTap;

        private bool DebugPopupOpen => debugPopup != null && debugPopup.gameObject.activeSelf;

        /// <summary>Makes the menu logo count taps; the 10th in a row opens the panel.</summary>
        private void BindDebugLogo(TMP_Text logo)
        {
            logo.raycastTarget = true;
            var button = logo.gameObject.AddComponent<Button>();
            button.targetGraphic = logo;
            button.transition = Selectable.Transition.None;
            Rebind(button, CountDebugTap);
        }

        private void CountDebugTap()
        {
            var now = Time.unscaledTime;
            debugTaps = now - debugLastTap <= DebugTapGap ? debugTaps + 1 : 1;
            debugLastTap = now;
            if (debugTaps < DebugMode.TapsToOpen) return;
            debugTaps = 0;
            OpenDebugPopup();
        }

        private void OpenDebugPopup()
        {
            // Built fresh each time so it always matches the current orientation.
            if (debugPopup != null) Destroy(debugPopup.gameObject);
            debugPopup = BuildReleasePanel("DebugMode", "MODO DEBUG", out var panel);

            debugStatus = UIFactory.Label(panel, "Status", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center);
            UIFactory.Place(debugStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f),
                new Vector2(790f, 130f));

            if (DebugMode.Enabled) BuildDebugActions(panel);
            else BuildDebugLogin(panel);

            var close = UIFactory.RoundButton(panel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f),
                Vector2.one * UIFactory.Touch);
            Rebind(close, CloseDebugPopup);
            debugPopup.SetAsLastSibling();
            SyncAdNavigation();
        }

        private void BuildDebugLogin(RectTransform panel)
        {
            debugStatus.text = "Digite a senha para liberar todos os níveis e créditos.";

            var inputBack = UIFactory.Panel9(panel, "PasswordInput", UIFactory.ActionFill);
            UIFactory.Place(inputBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f),
                new Vector2(792f, 110f));
            inputBack.gameObject.SetActive(false);
            var viewport = UIFactory.NewRect(inputBack.transform, "Viewport");
            UIFactory.Stretch(viewport, 20f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UIFactory.Label(viewport, "Text", "", 40, UIFactory.Ink, TextAlignmentOptions.MidlineLeft);
            UIFactory.Stretch(text.rectTransform);
            text.richText = false;
            text.textPreprocessor = null;
            var placeholder = UIFactory.Label(viewport, "Placeholder", "Senha", 36, UIFactory.InkDim,
                TextAlignmentOptions.MidlineLeft);
            UIFactory.Stretch(placeholder.rectTransform);
            debugInput = inputBack.gameObject.AddComponent<TMP_InputField>();
            debugInput.textViewport = viewport;
            debugInput.textComponent = text;
            debugInput.placeholder = placeholder;
            debugInput.lineType = TMP_InputField.LineType.SingleLine;
            debugInput.contentType = TMP_InputField.ContentType.Password;
            debugInput.characterLimit = 64;
            inputBack.gameObject.SetActive(true);
            debugInput.onSubmit.AddListener(_ => SubmitDebugPassword());

            var enter = ReleaseAction(panel, "Enter", "ENTRAR", new Vector2(0.5f, 0f), new Vector2(0f, 120f),
                new Vector2(792f, 96f));
            Rebind(enter, SubmitDebugPassword);
        }

        private void BuildDebugActions(RectTransform panel)
        {
            debugStatus.text = "Modo debug ativo neste aparelho: todos os níveis e o modo infinito liberados. " +
                "Pontuações não vão para o ranking.";

            var credits = ReleaseAction(panel, "Credits", "+" + EndlessScore.Format(DebugMode.CreditGrant) + " CRÉDITOS",
                new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(792f, 96f));
            Rebind(credits, () =>
            {
                GrantDebugCredits();
                debugStatus.text = "Créditos adicionados: " + EndlessScore.Format(run.passiveUpgrades.credits) + " no total.";
            });
            var off = ReleaseAction(panel, "Disable", "DESATIVAR MODO DEBUG", new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(792f, 96f));
            Rebind(off, () =>
            {
                DebugMode.SetEnabled(false);
                AfterDebugChange();
                CloseDebugPopup();
            });
        }

        private void SubmitDebugPassword()
        {
            if (debugInput == null) return;
            if (!DebugMode.CheckPassword(debugInput.text))
            {
                debugInput.text = "";
                debugStatus.text = "Senha incorreta.";
                debugStatus.color = UIFactory.Danger;
                Sfx.Play(Sound.Back);
                return;
            }

            DebugMode.SetEnabled(true);
            GrantDebugCredits();
            AfterDebugChange();
            OpenDebugPopup(); // Rebuilds as the "active" panel.
            debugStatus.text = "Modo debug ativado: níveis liberados e +" +
                EndlessScore.Format(DebugMode.CreditGrant) + " créditos.";
            debugStatus.color = UIFactory.Evolved;
        }

        private void GrantDebugCredits()
        {
            if (run == null || run.passiveUpgrades == null) return;
            run.passiveUpgrades.AddRewardedCredits(DebugMode.CreditGrant);
            ProgressSnapshot.SaveLocal();
            RefreshMainMenu();
        }

        /// <summary>Menu text that depends on which stages are open.</summary>
        private void AfterDebugChange()
        {
            RefreshMainMenu();
            RefreshModesPage();
        }

        private void CloseDebugPopup()
        {
            if (debugInput != null) debugInput.DeactivateInputField();
            if (debugPopup != null) Destroy(debugPopup.gameObject);
            debugPopup = null;
            debugInput = null;
            SyncAdNavigation();
        }
    }
}
