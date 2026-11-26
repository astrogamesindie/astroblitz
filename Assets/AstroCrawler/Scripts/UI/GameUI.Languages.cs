using TMPro;
using UnityEngine;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private RectTransform languagesPage;
        private UnityEngine.UI.Button languageValueButton;

        private void BuildLanguagesPage()
        {
            if (languagesPage != null) return;
            languagesPage = BuildMenuPage("LanguagesPage", out var title);
            title.text = L("IDIOMA", "LANGUAGE", "IDIOMA");
            Rebind(languagesPage.Find("Back").GetComponent<UnityEngine.UI.Button>(), () => OpenMenuSection(MenuSection.Settings));
            // Latin labels keep every option readable without loading four script fonts together.
            // Revisão de legibilidade (23/11/2026): cada opção é um botão de 144 de altura com a
            // moldura do kit, selo do código à esquerda e ✓ no idioma ativo (RefreshSettings).
            var codes = new[] { "PT", "EN", "ES", "JA", "HI", "KO", "ZH", "DE", "FR", "IT" };
            var names = new[] { "Português", "English", "Español", "Japanese", "Hindi",
                "Korean", "Chinese", "Deutsch", "Français", "Italiano" };
            var cutFrame = CutFrameSprite;
            var width = UIFactory.Pick(640f, 470f);
            var height = UIFactory.Touch;
            var pitch = height + UIFactory.Pick(16f, 20f);
            var top = UIFactory.Pick(200f, 300f);
            for (var i = 0; i < GameLocalization.LanguageCount; i++)
            {
                var index = i;
                var button = UIFactory.Button(languagesPage, "Language_" + i, names[i], UIFactory.PanelSoft,
                    UIFactory.Ink, UIFactory.TextButton, Sound.Toggle);
                var rect = button.GetComponent<RectTransform>();
                var x = (i % 2 == 0 ? -1f : 1f) * (width * 0.5f + 10f);
                UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(x, -top - i / 2 * pitch), new Vector2(width, height));
                StyleCutAction(button, cutFrame, WithAlpha(UIFactory.ActionBorder, 0.45f), UIFactory.ActionFillMuted, false);

                var badge = UIFactory.Panel9(rect, "Badge", WithAlpha(UIFactory.Accent, 0.22f), ArtLibrary.FrameOutline);
                badge.pixelsPerUnitMultiplier = 1.4f;
                badge.raycastTarget = false;
                UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(92f, 80f));
                var code = UIFactory.Label(badge.rectTransform, "Code", codes[i], UIFactory.TextButton, UIFactory.Accent,
                    TextAlignmentOptions.Center, true);
                UIFactory.Stretch(code.rectTransform, 0f);
                code.raycastTarget = false;

                var caption = rect.GetComponentInChildren<TMP_Text>();
                caption.rectTransform.anchorMin = Vector2.zero;
                caption.rectTransform.anchorMax = Vector2.one;
                caption.rectTransform.offsetMin = new Vector2(132f, 8f);
                caption.rectTransform.offsetMax = new Vector2(-76f, -8f);
                caption.alignment = TextAlignmentOptions.MidlineLeft;
                caption.enableAutoSizing = true;
                caption.fontSizeMin = UIFactory.TextMin;
                caption.fontSizeMax = UIFactory.TextButton;
                caption.textWrappingMode = TextWrappingModes.NoWrap;

                var check = UIFactory.Rect(rect, "Check", UIFactory.Ink, ArtLibrary.IconCheck);
                UIFactory.Place(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(52f, 52f));
                check.preserveAspect = true;
                check.raycastTarget = false;
                check.gameObject.SetActive(false);

                languageButtons[i] = button;
                Rebind(button, () =>
                {
                    GameLocalization.Set((GameLocalization.Language)index);
                    RefreshLanguage();
                    OpenMenuSection(MenuSection.Settings);
                });
            }
            languagesPage.gameObject.SetActive(false);
            GameLanguageFonts.ApplyScene();
        }

        private void RefreshLanguageChoices()
        {
            SetLabel(languagesPage, "Title", L("IDIOMA", "LANGUAGE", "IDIOMA"));
            RefreshSettings();
        }
    }
}
