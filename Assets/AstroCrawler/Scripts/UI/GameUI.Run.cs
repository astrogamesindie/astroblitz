// AstroCrawler - the per-level upgrade pick and the end-of-run screen.
//
// Three compact upgrade choices share one row. Hold a choice for its full card.

using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const float PickCardScale = 1.65f;
        private const float PickCardGap = 24f;

        [Header("Escolha de melhoria")]
        [SerializeField] private RectTransform evolutionScreen;
        [SerializeField] private CanvasGroup evolutionGroup;
        [SerializeField] private TMP_Text evolutionCaption;
        [SerializeField] private Image evolutionPending;
        [SerializeField] private TMP_Text evolutionTitle;
        [SerializeField] private TMP_Text evolutionHint;
        [SerializeField] private List<CardWidget> evolutionWidgets = new List<CardWidget>();

        [Header("Fim de run")]
        [SerializeField] private RectTransform endScreen;
        [SerializeField] private CanvasGroup endGroup;
        [SerializeField] private TMP_Text endTitle;
        [SerializeField] private TMP_Text endBody;
        [SerializeField] private Button againButton;
        [SerializeField] private Button endMenuButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Image endClearedBadge;
        [SerializeField] private TrueShadow endPanelGlow;
        [SerializeField] private RectTransform endBrackets;

        // ------------------------------------------------------------------
        // Upgrade pick
        // ------------------------------------------------------------------

        private void BuildEvolutionScreen(Transform parent)
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            Image backdrop;
            // Fully opaque: the HUD behind it used to show through and collide
            // with the title.
            evolutionScreen = UIFactory.Page(parent, "EvolutionScreen", new Color(0.015f, 0.025f, 0.05f, 1f), out backdrop);
            evolutionGroup = UIFactory.Group(evolutionScreen);

            // Cold light along the top and bottom edges: a console, not a dimmed game.
            SweepGlow(evolutionScreen, "GlowTop", true, 420f, WithAlpha(UIFactory.Accent, 0.2f));
            SweepGlow(evolutionScreen, "GlowBottom", false, 320f, WithAlpha(UIFactory.Accent, 0.14f));

            var textWidth = UIFactory.Pick(1500f, 1020f);
            evolutionCaption = UIFactory.Label(evolutionScreen, "Caption", "", UIFactory.TextCaption, UIFactory.Accent,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(evolutionCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                new Vector2(textWidth, 36f));
            evolutionCaption.characterSpacing = 14f;
            UIFactory.AddUnderlay(evolutionCaption);

            evolutionTitle = UIFactory.Label(evolutionScreen, "Title", "", UIFactory.Pick(58, 50), UIFactory.Evolved,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(evolutionTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                new Vector2(textWidth, 72f));
            evolutionTitle.characterSpacing = UIFactory.Pick(10f, 6f);
            UIFactory.AddUnderlay(evolutionTitle, 0.3f, 0.15f);

            // A lit rule on each side of the title, ending in a diamond; shorter
            // and closer in portrait, where the title takes most of the width.
            var ruleCentre = UIFactory.Pick(620f, 440f);
            var ruleLength = UIFactory.Pick(340f, 100f);
            var capOffset = UIFactory.Pick(436f, 380f);
            for (var side = -1; side <= 1; side += 2)
            {
                var rule = UIFactory.Rect(evolutionScreen, side < 0 ? "RuleLeft" : "RuleRight",
                    WithAlpha(UIFactory.Accent, 0.85f), ArtLibrary.Pixel);
                rule.raycastTarget = false;
                UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * ruleCentre, -126f),
                    new Vector2(ruleLength, 3f));
                UIFactory.GlowShadow(rule, WithAlpha(UIFactory.Accent, 0.6f), 16f, 0.1f);

                var cap = UIFactory.Rect(evolutionScreen, side < 0 ? "CapLeft" : "CapRight", UIFactory.Accent,
                    ArtLibrary.ArtOr("diamond", ArtLibrary.CircleFilled));
                cap.raycastTarget = false;
                UIFactory.Place(cap.rectTransform, new Vector2(0.5f, 1f), new Vector2(side * capOffset, -126f),
                    new Vector2(18f, 18f));
                cap.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                cap.rectTransform.anchoredPosition = new Vector2(side * capOffset, -126f);
            }

            evolutionHint = UIFactory.Label(evolutionScreen, "Hint", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(evolutionHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -164f),
                new Vector2(textWidth, 44f));
            evolutionHint.enableAutoSizing = true;
            evolutionHint.fontSizeMin = UIFactory.TextCaption;
            evolutionHint.fontSizeMax = UIFactory.TextBody;

            // How many more picks are waiting behind this one.
            evolutionPending = UIFactory.Pill(evolutionScreen, "Pending", "", UIFactory.Evolved,
                new Color(0.05f, 0.04f, 0.02f), 48f, 24, ArtLibrary.IconStar);
            UIFactory.Place(evolutionPending.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f),
                new Vector2(620f, 48f));
            UIFactory.GlowShadow(evolutionPending, WithAlpha(UIFactory.Evolved, 0.45f), 20f, 0.06f);
            evolutionPending.gameObject.SetActive(false);

            var size = new Vector2(300f, 340f);
            var step = size.x * PickCardScale + PickCardGap;
            for (var i = 0; i < CardSystem.ChoiceCount; i++)
            {
                var widget = CardWidget.Create(evolutionScreen, "Choice" + i, size);
                widget.SetCompactChoice(size);
                UIFactory.Place(widget.root, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * step, -50f * PickCardScale - 40f), size);
                widget.SetRestScale(PickCardScale);
                // Subscribed in Bind, not here: a C# event is not serialized.
                evolutionWidgets.Add(widget);
            }

            evolutionScreen.gameObject.SetActive(false);
        }

        /// <summary>A full-width gradient along the top or bottom edge of a screen.</summary>
        private static Image SweepGlow(RectTransform parent, string name, bool top, float height, Color color)
        {
            var image = UIFactory.Rect(parent, name, color, top ? ArtLibrary.GradientTop : ArtLibrary.GradientBottom);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            return image;
        }

        private void ShowEvolutionScreen()
        {
            var choices = run.cards.Choices;
            LayoutEvolutionCards();

            evolutionCaption.text = L("SELEÇÃO DE MELHORIA", "UPGRADE SELECT", "SELECCIÓN DE MEJORA");
            var waiting = run.pendingPicksAfterThis;
            evolutionPending.gameObject.SetActive(waiting > 0);
            if (waiting > 0)
            {
                SetLabel(evolutionPending.transform, "Label", "+" + waiting +
                    (waiting == 1 ? L(" ESCOLHA EM ESPERA", " PICK WAITING", " ELECCIÓN EN ESPERA")
                                  : L(" ESCOLHAS EM ESPERA", " PICKS WAITING", " ELECCIONES EN ESPERA")));
            }

            // One pick per ship level: the title is the level being claimed.
            evolutionTitle.text = L("NAVE  ·  NÍVEL ", "SHIP  ·  LEVEL ", "NAVE  ·  NIVEL ") +
                (run.level - run.pendingPicksAfterThis);
            evolutionHint.text = L("Toque para escolher. Toque em i para ver os detalhes.",
                "Tap to choose. Tap i to view details.", "Toca para elegir. Toca i para ver los detalles.");

            var dealt = 0;
            for (var i = 0; i < evolutionWidgets.Count; i++)
            {
                var widget = evolutionWidgets[i];
                if (i >= choices.Count)
                {
                    widget.SetVisible(false);
                    continue;
                }

                var card = choices[i];
                var nextLevel = run.cards.UpgradeLevel(card.definition.id) + 1;
                var footer = nextLevel == 1
                    ? L("NOVA", "NEW", "NUEVA")
                    : L("NÍVEL ", "LEVEL ", "NIVEL ") + nextLevel;

                widget.SetVisible(true);
                widget.Bind(card, 0, footer, nextLevel);
                widget.interactable = true;
                widget.PlayDeal(dealt++);
            }

            evolutionScreen.gameObject.SetActive(true);
            FadeScreenIn(evolutionGroup);
            Sfx.Play(Sound.CardsDealt);
            Juice.PopIn(evolutionTitle.transform, 1f, 0.4f, 0f, true);
        }

        private void LayoutEvolutionCards()
        {
            // Apply to existing baked widgets as well; no full UI rebuild needed.
            var bounds = evolutionScreen.rect;
            var width = bounds.width > 0f ? bounds.width : UIFactory.ReferenceWidth;
            var height = bounds.height > 0f ? bounds.height : UIFactory.ReferenceHeight;

            // The same horizontal row in portrait and landscape, centred even with fewer options.
            var count = Mathf.Max(1, Mathf.Min(evolutionWidgets.Count, run.cards.Choices.Count));
            var widthScale = (width - 96f - PickCardGap * (count - 1)) / (300f * (count + 0.05f));
            // Include the separate information button and its gap above each card.
            var heightScale = (height - 440f) / (440f * 1.05f);
            var scale = Mathf.Max(0.1f, Mathf.Min(PickCardScale, Mathf.Min(widthScale, heightScale)));
            var step = 300f * scale + PickCardGap;
            for (var i = 0; i < evolutionWidgets.Count; i++)
            {
                var widget = evolutionWidgets[i];
                widget.ResetVisualState();
                widget.SetRestScale(scale);
                widget.SetHomePosition(new Vector2((i - (count - 1) * 0.5f) * step, -50f * scale - 40f));
            }
        }

        private void HandleEvolutionInfo(CardWidget widget)
        {
            if (run.phase != RunPhase.UpgradeSelection || widget.card == null ||
                (upgradeDetailsScreen != null && upgradeDetailsScreen.gameObject.activeSelf)) return;
            Sfx.Play(Sound.Navigate);
            var nextLevel = run.cards.UpgradeLevel(widget.card.definition.id) + 1;
            ShowUpgradeDetails(widget.card.definition, nextLevel);
        }

        private void HandleEvolutionChoice(CardWidget widget)
        {
            if (run.phase != RunPhase.UpgradeSelection || widget.card == null ||
                (upgradeDetailsScreen != null && upgradeDetailsScreen.gameObject.activeSelf))
            {
                return;
            }

            // Lock the other two immediately: the confirmation animation runs
            // for a beat, and a second tap in that window would be a bug.
            for (var i = 0; i < evolutionWidgets.Count; i++)
            {
                evolutionWidgets[i].interactable = false;
            }

            Sfx.Play(Sound.UpgradeChosen);
            run.ConfirmUpgrade(widget.card);
        }

        // ------------------------------------------------------------------
        // End of run
        // ------------------------------------------------------------------

        private void BuildEndScreen(Transform parent)
        {
            Image backdrop;
            endScreen = UIFactory.Page(parent, "EndScreen", new Color(0.02f, 0.03f, 0.06f, 0.95f), out backdrop);
            endGroup = UIFactory.Group(endScreen);

            // The result sits in a lit panel with corner brackets; its colour
            // says how the run went (green, or red on a loss).
            // Portrait stacks the same content into a narrower, taller panel.
            var panel = UIFactory.Panel9(endScreen, "Panel", new Color(0.04f, 0.06f, 0.11f, 0.82f));
            panel.raycastTarget = false;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), UIFactory.Pick(new Vector2(0f, 136f), new Vector2(0f, 180f)),
                UIFactory.Pick(new Vector2(1400f, 628f), new Vector2(1000f, 880f)));
            UIFactory.DropShadow(panel, 40f, 14f, 0.65f);
            endPanelGlow = UIFactory.GlowShadow(panel, WithAlpha(UIFactory.Good, 0.3f), 36f, 0.06f);
            endBrackets = UIFactory.Brackets(panel.rectTransform, UIFactory.Good, 40f, -8f);
            var bracketImages = endBrackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < bracketImages.Length; i++) bracketImages[i].raycastTarget = false;

            endClearedBadge = UIFactory.Pill(endScreen, "ClearedBadge", "", UIFactory.Good,
                new Color(0.03f, 0.04f, 0.07f), 52f, 26, ArtLibrary.IconCheck);
            UIFactory.Place(endClearedBadge.rectTransform, new Vector2(0.5f, 0.5f),
                UIFactory.Pick(new Vector2(0f, 415f), new Vector2(0f, 620f)),
                new Vector2(UIFactory.Pick(760f, 720f), 52f));
            UIFactory.GlowShadow(endClearedBadge, WithAlpha(UIFactory.Good, 0.5f), 22f, 0.06f);

            endTitle = UIFactory.Label(endScreen, "Title", "", 88, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 0.5f), EndTitlePosition,
                new Vector2(UIFactory.Pick(1500f, 960f), 110f));
            endTitle.characterSpacing = UIFactory.Pick(14f, 8f);
            endTitle.textWrappingMode = TextWrappingModes.NoWrap;
            endTitle.enableAutoSizing = true;
            endTitle.fontSizeMin = 52f;
            endTitle.fontSizeMax = 88f;
            UIFactory.AddUnderlay(endTitle, 0.3f, 0.2f);

            endBody = UIFactory.Label(endScreen, "Body", "", 36, UIFactory.InkDim, TextAlignmentOptions.Top, true);
            UIFactory.Place(endBody.rectTransform, new Vector2(0.5f, 0.5f), EndBodyPosition,
                UIFactory.Pick(new Vector2(1200f, 250f), new Vector2(920f, 300f)));
            endBody.lineSpacing = 14f;
            endBody.enableAutoSizing = true;
            endBody.fontSizeMin = UIFactory.TextBody;
            endBody.fontSizeMax = 36f;

            var buttonSize = UIFactory.Pick(new Vector2(500f, 132f), new Vector2(440f, 132f));
            againButton = UIFactory.IconButton(endScreen, "Again", "", ArtLibrary.IconRetry, UIFactory.Primary,
                buttonSize, UIFactory.TextButton, Sound.Play);
            UIFactory.Place(againButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                EndButtonPosition(-1, false), buttonSize);
            UIFactory.DropShadow(againButton.GetComponent<Image>(), 24f, 8f, 0.55f);
            UIFactory.GlowShadow(againButton.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.4f), 28f, 0.08f);

            // Same slot as the "watch an ad to revive" button, which only shows
            // on a defeat; this one only shows on a victory.
            nextLevelButton = UIFactory.IconButton(endScreen, "NextLevel", "", ArtLibrary.IconNext, UIFactory.Primary,
                EndWideButtonSize, UIFactory.TextButton, Sound.Play);
            UIFactory.Place(nextLevelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                EndWideButtonPosition, EndWideButtonSize);
            UIFactory.DropShadow(nextLevelButton.GetComponent<Image>(), 24f, 8f, 0.55f);
            UIFactory.GlowShadow(nextLevelButton.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.5f), 28f, 0.08f);
            nextLevelButton.gameObject.SetActive(false);

            endMenuButton = UIFactory.IconButton(endScreen, "Menu", "", ArtLibrary.IconHome, UIFactory.PanelSoft,
                buttonSize, UIFactory.TextButton, Sound.Back);
            UIFactory.Place(endMenuButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                EndButtonPosition(1, false), buttonSize);
            UIFactory.DropShadow(endMenuButton.GetComponent<Image>(), 24f, 8f, 0.55f);

            endScreen.gameObject.SetActive(false);
        }

        // End screen positions. "withAds": the rewarded-revive row needs the
        // landscape layout to spread out a little (portrait already has room).
        private static Vector2 EndTitlePosition { get { return UIFactory.Pick(new Vector2(0f, 270f), new Vector2(0f, 500f)); } }
        private static Vector2 EndBodyPosition { get { return UIFactory.Pick(new Vector2(0f, 60f), new Vector2(0f, 290f)); } }
        private static Vector2 EndWideButtonSize { get { return UIFactory.Pick(new Vector2(1050f, 100f), new Vector2(920f, 100f)); } }
        private static Vector2 EndWideButtonPosition { get { return UIFactory.Pick(new Vector2(0f, -105f), new Vector2(0f, -110f)); } }

        private static Vector2 EndButtonPosition(int side, bool withAds)
        {
            return UIFactory.Portrait
                ? new Vector2(side * 235f, -350f)
                : new Vector2(side * 275f, withAds ? -295f : -260f);
        }

        private void ShowEndScreen(bool victory)
        {
            endTitle.text = victory
                ? L("NÚCLEO DESTRUÍDO", "CORE DESTROYED", "NÚCLEO DESTRUIDO")
                // Nunca "você morreu": a derrota termina num convite (PILARES_DE_UI §6).
                : L("TENTE OUTRA VEZ!", "TRY AGAIN!", "¡INTÉNTALO DE NUEVO!");
            endTitle.color = victory ? UIFactory.Good : UIFactory.Accent;
            if (run.isInfinite && run.newRecord)
            {
                endTitle.text = L("NOVO RECORDE!", "NEW BEST!", "¡NUEVO RÉCORD!");
                endTitle.color = UIFactory.Evolved;
            }

            var stage = run.stageIndex;
            var hasNext = !run.isInfinite && victory && stage < StageCatalog.Count - 1;
            var tone = victory ? UIFactory.Good : UIFactory.Accent;
            UIFactory.FadeGlow(endPanelGlow, WithAlpha(tone, 0.32f), 0.4f);
            var corners = endBrackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < corners.Length; i++) corners[i].color = tone;

            endClearedBadge.gameObject.SetActive(victory);
            if (victory)
            {
                endClearedBadge.color = StageCatalog.Accent(stage);
                SetLabel(endClearedBadge.transform, "Label", stage >= StageCatalog.Count - 1
                    ? L("CAMPANHA COMPLETA", "CAMPAIGN COMPLETE", "CAMPAÑA COMPLETA")
                    : StageCatalog.Name(stage) + L(" CONCLUÍDO", " CLEARED", " COMPLETADO") +
                      (run.stageNewlyCleared ? "   ·   " + L("NOVO", "NEW", "NUEVO") : ""));
            }
            nextLevelButton.gameObject.SetActive(hasNext);
            if (hasNext)
            {
                SetButtonLabel(nextLevelButton, L("PRÓXIMO NÍVEL  ·  ", "NEXT LEVEL  ·  ", "SIGUIENTE NIVEL  ·  ") +
                    StageCatalog.Name(stage + 1));
            }

            var cleared = run.completedWaves;
            var minutes = Mathf.FloorToInt(run.runDuration / 60f);
            var seconds = Mathf.FloorToInt(run.runDuration % 60f);

            // Four lines: what you flew, how far you got, what you earned, and
            // how long it took. The credits are the reason to go back to the
            // upgrade tree, so they are the one coloured line.
            builder.Length = 0;
            if (run.shipDefinition != null)
            {
                builder.Append(GameLocalization.Content(run.shipDefinition.name).ToUpperInvariant())
                    .Append("   ·   ").Append(run.modeName).Append('\n');
            }
            builder.Append(L("ONDAS ", "WAVES ", "OLEADAS ")).Append(cleared);
            if (!run.isInfinite) builder.Append(" / ").Append(RunController.WavesPerRun);
            if (run.isInfinite)
            {
                var best = run.isSquadron ? RunController.SquadronBest : EndlessScore.Best;
                // The score is the point of this mode: on the waves line, gold when
                // it is a record (the title then says so), else next to the record.
                builder.Append("   ·   ").Append(L("PONTOS ", "SCORE ", "PUNTOS ")).Append("<color=")
                    .Append(UIFactory.ColorTag(run.newRecord ? UIFactory.Evolved : UIFactory.Ink)).Append('>')
                    .Append(EndlessScore.Format(run.score)).Append("</color>");
                if (!run.newRecord && best > 0)
                    builder.Append("   ·   ").Append(L("RECORDE ", "BEST ", "RÉCORD ")).Append(EndlessScore.Format(best));
            }
            builder.Append('\n');
            var earned = run.isInfinite ? run.earnedCredits : run.passiveUpgrades.lastReward;
            var creditsTextIndex = builder.Length;
            builder.Append("<color=").Append(UIFactory.ColorTag(UIFactory.Evolved)).Append(">+")
                .Append(earned).Append(L(" CRÉDITOS", " CREDITS", " CRÉDITOS"));
            // The evolution guarantee's share, so the player sees why a loss still paid for something.
            if (run.guaranteeBonus > 0)
                builder.Append("   ·   ").Append(L("BÔNUS DE EVOLUÇÃO +", "EVOLUTION BONUS +", "BONO DE EVOLUCIÓN +"))
                    .Append(run.guaranteeBonus);
            builder.Append("</color>\n");
            builder.Append(L("NAVE NV ", "SHIP LV ", "NAVE NV ")).Append(run.level).Append("   ·   ")
                .Append(L("MELHORIAS ", "UPGRADES ", "MEJORAS ")).Append(run.cards.acquiredCount)
                .Append("   ·   ").Append(minutes).Append("m ").Append(seconds.ToString("00")).Append('s');

            endBody.text = builder.ToString();
            endScreen.gameObject.SetActive(true);
            FadeScreenIn(endGroup, 0.4f);
            // The credits chip shows the balance before the run; the coins then
            // burst out of the "+N" line and count it up.
            PlayEndCredits(earned, creditsTextIndex);

            Sfx.Play(victory ? Sound.Victory : Sound.Defeat);
            Juice.PopIn(endTitle.transform, 1f, 0.5f, 0.15f, true);
            Juice.ScreenFlash(victory
                ? Palette.Alfa(Palette.Acido, 0.3f)
                : Palette.Alfa(Palette.Magenta, 0.3f), 0.7f);
            Juice.Shake(victory ? 0.2f : 0.45f, 0.5f);
        }

        /// <summary>The stage after the one just beaten, with the same ship.</summary>
        private void PlayNextLevel()
        {
            var next = run.stageIndex + 1;
            if (next >= StageCatalog.Count) return;
            var ship = run.shipDefinition != null ? run.shipDefinition : config.GetShip(selectedShipId);
            AfterResultAd(() =>
            {
                if (ship != null) WarpToStage(next, ship, () => run.BeginRun(ship, next));
                else run.Restart();
            });
        }

        private static void IconOnly(Button button)
        {
            if (button == null) return;
            SetButtonLabel(button, "");
            var icon = button.transform.Find("Icon") as RectTransform;
            if (icon != null) UIFactory.Place(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
        }

        private void RefreshRunLanguage()
        {
            // Retry and home are icons: no caption, the glyph in the middle.
            IconOnly(againButton);
            IconOnly(endMenuButton);
        }
    }
}
