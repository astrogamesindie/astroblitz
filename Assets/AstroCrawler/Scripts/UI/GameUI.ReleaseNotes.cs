using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private RectTransform releaseNotesPopup, supportPopup;
        private TMP_InputField supportInput;
        private TMP_Text supportStatus;
        private Button supportSend;
        private readonly Button[] supportTopics = new Button[3];
        private string supportDraft = "";
        private int supportTopic;
        private bool ReleaseNotesPopupOpen => releaseNotesPopup != null && releaseNotesPopup.gameObject.activeSelf;
        private bool SupportPopupOpen => supportPopup != null && supportPopup.gameObject.activeSelf;
        private bool IntroScreenOpen => ReleaseNotesPopupOpen || SupportPopupOpen || DebugPopupOpen;
        private Button settingsSupportButton;

        private RectTransform BuildReleasePanel(string name, string title, out RectTransform panel)
        {
            var page = UIFactory.Page(safeRoot, name, new Color(0.005f, 0.012f, 0.025f, 1f), out var backdrop);
            backdrop.raycastTarget = true;
            UIFactory.Group(page).ignoreParentGroups = true;
            var frame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var outline = UIFactory.Panel9(page, "Panel", UIFactory.ActionBorder, frame);
            outline.pixelsPerUnitMultiplier = 0.2f;
            panel = outline.rectTransform;
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1000f));
            var surface = UIFactory.Panel9(panel, "Surface", UIFactory.ActionFillMuted, frame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(outline, surface, KitRole.Painel);
            var caption = UIFactory.Label(panel, "Title", title, UIFactory.TextHeading, UIFactory.Accent, TextAlignmentOptions.Center, true);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 72f));
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 28f;
            caption.fontSizeMax = UIFactory.TextHeading;
            return page;
        }

        private Button ReleaseAction(RectTransform panel, string name, string caption, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var button = UIFactory.Button(panel, name, caption, UIFactory.ActionBorder, UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(button.GetComponent<RectTransform>(), anchor, position, size);
            StyleCutAction(button, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), UIFactory.ActionBorder, UIFactory.ActionFill, false);
            return button;
        }

        private void BuildReleaseAccess()
        {
            // The support button lives in the settings panel's last row (BuildSettingsPage).
            if (settingsPage == null || settingsSupportButton == null) return;
            SetButtonLabel(settingsSupportButton, L("FALE COM A EQUIPE", "CONTACT THE TEAM", "CONTACTA CON EL EQUIPO"));
        }

        private void OpenPendingReleaseNotes()
        {
            if (ReleaseNotesState.Pending && run != null && run.phase == RunPhase.ShipSelection) OpenReleaseNotes();
        }

        private void OpenReleaseNotes()
        {
            if (releaseNotesPopup != null)
            {
                releaseNotesPopup.gameObject.SetActive(false);
                Destroy(releaseNotesPopup.gameObject);
            }
            releaseNotesPopup = BuildReleasePanel("ReleaseNotes", L("NOTAS DA VERSÃO", "RELEASE NOTES", "NOTAS DE LA VERSIÓN"), out var panel);
            var version = UIFactory.Label(panel, "Version", "v" + Application.version, UIFactory.TextBody, UIFactory.Evolved, TextAlignmentOptions.Center, true);
            UIFactory.Place(version.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(760f, 48f));

            // A version gift (ReleaseNotes.json "reward") takes a row under the notes.
            var reward = ReleaseNotesState.Reward;
            var scrollRoot = UIFactory.NewRect(panel, "NotesScroll");
            UIFactory.Place(scrollRoot, new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(800f, reward > 0 ? 410f : 520f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            var viewport = UIFactory.Panel9(scrollRoot, "Viewport", new Color(0.03f, 0.08f, 0.12f));
            UIFactory.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIFactory.NewRect(viewport.transform, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var body = new StringBuilder();
            var notes = ReleaseNotesState.Current.notes;
            for (var i = 0; i < notes.Length; i++)
            {
                if (i > 0) body.Append("\n\n");
                body.Append(i + 1).Append(". ").Append(GameLocalization.Content(notes[i]));
            }
            var text = UIFactory.Label(content, "Notes", body.ToString(), 34, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            text.richText = false;
            var textHeight = Mathf.Max(reward > 0 ? 370f : 480f, text.GetPreferredValues(body.ToString(), 744f, float.PositiveInfinity).y);
            content.sizeDelta = new Vector2(0f, textHeight + 48f);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(744f, textHeight));
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.verticalNormalizedPosition = 1f;

            var done = ReleaseAction(panel, "Continue", L("CONTINUAR", "CONTINUE", "CONTINUAR"), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(760f, 96f));
            Rebind(done, CloseReleaseNotes);
            var contact = ReleaseAction(panel, "Contact", L("DÚVIDAS, FEEDBACK E BUGS", "QUESTIONS, FEEDBACK & BUGS", "DUDAS, COMENTARIOS Y ERRORES"),
                new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(760f, 80f));
            Rebind(contact, OpenSupportPopup);
            if (reward > 0) BuildReleaseReward(panel, reward);
            var close = UIFactory.RoundButton(panel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep, UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f), Vector2.one * UIFactory.Touch);
            Rebind(close, CloseReleaseNotes);
            releaseNotesPopup.SetAsLastSibling();
            RefreshUpdateScreen();
            SyncAdNavigation();
        }

        /// <summary>The gold "CLAIM +N" row between the notes and Continue; once per account per revision.</summary>
        private void BuildReleaseReward(RectTransform panel, int reward)
        {
            var button = ReleaseAction(panel, "Reward", "", new Vector2(0.5f, 0f), new Vector2(0f, 262f), new Vector2(760f, 100f));
            var rect = button.GetComponent<RectTransform>();
            var jewel = UIFactory.Rect(rect, "Credits", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            jewel.raycastTarget = false;
            UIFactory.Place(jewel.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f));
            Rebind(button, () => ClaimReleaseReward(button));
            RefreshReleaseReward(button, reward);
        }

        private void RefreshReleaseReward(Button button, int reward)
        {
            var claimed = ReleaseNotesState.RewardClaimed;
            SetButtonLabel(button, claimed
                ? L("COLETADA", "CLAIMED", "RECOGIDA")
                : L("COLETAR", "CLAIM", "RECOGER") + "  +" + reward);
            button.interactable = !claimed;
            // ReleaseAction already styled it (frame + "Inset"): retint only, in gold.
            button.GetComponent<Image>().color = claimed ? WithAlpha(UIFactory.Evolved, 0.3f) : RewardBorder;
            var inset = button.transform.Find("Inset");
            if (inset != null) inset.GetComponent<Image>().color = claimed ? RewardFillMuted : RewardFillStrong;
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption != null) caption.color = claimed ? UIFactory.InkDim : UIFactory.Evolved;
        }

        private void ClaimReleaseReward(Button button)
        {
            if (run == null || button == null) return;
            var amount = ReleaseNotesState.ClaimReward(run.passiveUpgrades);
            if (amount <= 0) { Sfx.Play(Sound.Denied); return; }
            Sfx.Play(Sound.Purchase);
            RefreshReleaseReward(button, amount);
            RefreshMainMenu();
            RefreshPassiveScreen();
            Juice.Punch(button.transform, 0.12f, 0.3f);
            // The coins burst out of the button into the menu counter; the notes step aside.
            FlyCreditsToMenu(button.transform, amount);
            ClosePopupAfterCoins(() => { if (ReleaseNotesPopupOpen) CloseReleaseNotes(); });
        }

        private void CloseReleaseNotes()
        {
            if (!ReleaseNotesPopupOpen || SupportPopupOpen) return;
            ReleaseNotesState.Acknowledge();
            releaseNotesPopup.gameObject.SetActive(false);
            RefreshUpdateScreen();
            SyncAdNavigation();
        }

        private void OpenSupportPopup()
        {
            SaveSupportDraft();
            if (supportPopup != null)
            {
                supportPopup.gameObject.SetActive(false);
                Destroy(supportPopup.gameObject);
            }
            supportPopup = BuildReleasePanel("ContactSupport", L("FALE COM A EQUIPE", "CONTACT THE TEAM", "CONTACTA CON EL EQUIPO"), out var panel);
            var email = SupportEmail();
            var description = UIFactory.Label(panel, "Description", L("Escolha o assunto e escreva sua mensagem.",
                "Choose a topic and write your message.", "Elige un tema y escribe tu mensaje.") + "\n" + email,
                UIFactory.TextBody, UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(790f, 88f));
            var topics = new[] { L("DÚVIDA", "QUESTION", "DUDA"), L("FEEDBACK", "FEEDBACK", "COMENTARIOS"), L("BUG", "BUG", "ERROR") };
            for (var i = 0; i < supportTopics.Length; i++)
            {
                var index = i;
                var button = ReleaseAction(panel, "Topic_" + i, topics[i], new Vector2(0.5f, 1f), new Vector2((i - 1) * 264f, -242f), new Vector2(248f, 78f));
                supportTopics[i] = button;
                Rebind(button, () => { supportTopic = index; RefreshSupportTopics(); });
            }

            var inputBack = UIFactory.Panel9(panel, "MessageInput", UIFactory.ActionFill);
            UIFactory.Place(inputBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -344f), new Vector2(792f, 260f));
            inputBack.gameObject.SetActive(false);
            var viewport = UIFactory.NewRect(inputBack.transform, "Viewport");
            UIFactory.Stretch(viewport, 16f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UIFactory.Label(viewport, "Text", "", 32, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(text.rectTransform);
            text.richText = false;
            text.textPreprocessor = null; // Keep input/caret indexes on the original text, including Hindi.
            var placeholder = UIFactory.Label(viewport, "Placeholder", L("Escreva sua mensagem aqui…", "Write your message here…", "Escribe tu mensaje aquí…"), 30, UIFactory.InkDim);
            UIFactory.Stretch(placeholder.rectTransform);
            supportInput = inputBack.gameObject.AddComponent<TMP_InputField>();
            supportInput.textViewport = viewport;
            supportInput.textComponent = text;
            supportInput.placeholder = placeholder;
            supportInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            supportInput.characterLimit = SupportMail.MessageLimit;
            supportInput.text = supportDraft;
            inputBack.gameObject.SetActive(true);

            supportStatus = UIFactory.Label(panel, "Status", L("Inclui versão, plataforma e idioma; não inclui dados da conta.",
                "Includes version, platform and language; no account data.", "Incluye versión, plataforma e idioma; sin datos de la cuenta."), UIFactory.TextCaption, UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(supportStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -626f), new Vector2(790f, 66f));
            supportSend = ReleaseAction(panel, "Email", L("ABRIR E-MAIL", "OPEN EMAIL", "ABRIR CORREO"),
                new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(792f, 96f));
            Rebind(supportSend, OpenSupportEmail);
            // Release notes moved to Ajustes; copy now spans the row.
            var copy = ReleaseAction(panel, "Copy", L("COPIAR MENSAGEM", "COPY MESSAGE", "COPIAR MENSAJE"),
                new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(792f, 80f));
            Rebind(copy, () =>
            {
                SaveSupportDraft();
                GUIUtility.systemCopyBuffer = email + "\n" + SupportSubject() + "\n\n" + SupportBody();
                supportStatus.text = L("Mensagem e contato copiados.", "Message and contact copied.", "Mensaje y contacto copiados.");
            });
            var note = UIFactory.Label(panel, "Note", L("Você revisará e enviará pelo seu aplicativo de e-mail.",
                "You will review and send using your email app.", "Revisarás y enviarás desde tu aplicación de correo."),
                UIFactory.TextCaption, UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(790f, 44f));
            var close = UIFactory.RoundButton(panel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep, UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f), Vector2.one * UIFactory.Touch);
            Rebind(close, CloseSupportPopup);
            supportInput.onValueChanged.AddListener(value => { supportDraft = value; RefreshSupportTopics(); });
            RefreshSupportTopics();
            supportPopup.SetAsLastSibling();
            RefreshUpdateScreen();
            SyncAdNavigation();
        }

        private string SupportEmail()
        {
            var resource = Resources.Load<TextAsset>("AstroCrawler/ReleaseLinks");
            return resource == null ? "" : JsonUtility.FromJson<ReleaseLinks>(resource.text)?.supportEmail ?? "";
        }

        private void RefreshSupportTopics()
        {
            for (var i = 0; i < supportTopics.Length; i++)
                if (supportTopics[i] != null) TintSettingsAction(supportTopics[i], i == supportTopic);
            if (supportSend != null) supportSend.interactable = SupportMail.ValidEmail(SupportEmail()) && !string.IsNullOrWhiteSpace(supportDraft);
        }

        private void SaveSupportDraft()
        {
            if (supportInput != null) supportDraft = supportInput.text;
        }

        private string SupportSubject()
        {
            var topic = supportTopic == 0 ? L("DÚVIDA", "QUESTION", "DUDA") : supportTopic == 1 ? "FEEDBACK" : "BUG";
            return "Astro Blitz · " + topic;
        }

        private string SupportBody() => SupportMail.ComposeBody(supportDraft, Application.version, Application.buildGUID,
            Application.platform.ToString(), GameLocalization.Code(GameLocalization.Current));

        private void OpenSupportEmail()
        {
            SaveSupportDraft();
            if (!SupportMail.ValidEmail(SupportEmail()) || string.IsNullOrWhiteSpace(supportDraft)) return;
            Application.OpenURL(SupportMail.ComposeUri(SupportEmail(), SupportSubject(), SupportBody()));
            supportStatus.text = L("Revise e envie no aplicativo de e-mail. Se não abrir, use Copiar.",
                "Review and send in your email app. If it doesn't open, use Copy.",
                "Revisa y envía desde tu correo. Si no se abre, usa Copiar.");
        }

        private void CloseSupportPopup()
        {
            SaveSupportDraft();
            if (supportPopup != null) supportPopup.gameObject.SetActive(false);
            RefreshUpdateScreen();
            SyncAdNavigation();
        }
    }
}
