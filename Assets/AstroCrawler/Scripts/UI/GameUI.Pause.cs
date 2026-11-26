// AstroCrawler - the in-run pause menu, and the back button everywhere.
//
// The HUD has a round pause button in the top-right corner; Esc, the Android
// back button and a gamepad's Start open the same menu, and the game pauses
// itself when the app loses focus on a device. From the menu the player can
// resume, restart the run with the same ship and stage, or go back to the main
// menu - the last two ask first, because they throw the run away.
//
// Outside a run the same back input walks up the menu pages.

using System;

using TMPro;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private RectTransform pauseScreen;
        private CanvasGroup pauseGroup;
        private RectTransform pausePanel;
        private RectTransform pauseActions;
        private RectTransform pauseConfirm;
        private Image pauseSheet;
        private Image pauseTag;
        private TMP_Text pauseInfo;
        private TMP_Text pauseConfirmTitle;
        private TMP_Text pauseConfirmBody;
        private Button pauseResumeButton;
        private Button pauseRestartButton;
        private Button pauseMenuButton;
        private Button pauseCancelButton;
        private Button pauseConfirmButton;

        private bool paused;
        private float timeScaleBeforePause = 1f;
        private Action pendingConfirm;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        /// <summary>The pause button, baked into the HUD's top-right corner.</summary>
        private void BuildPauseButton(RectTransform hud)
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            // Quiet by itself: OpenPause plays the pause sound, and it is also
            // reached from Esc and the Android back button.
            pauseButton = UIFactory.RoundButton(hud, "Pause", null, UIFactory.Panel, 128f, Sound.None);
            var rect = pauseButton.GetComponent<RectTransform>();
            UIFactory.Place(rect, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(128f, 128f));
            UIFactory.GlowShadow(pauseButton.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.22f), 22f, 0.05f);
            // The default white ring becomes a cyan one, like the HUD brackets.
            var ring = rect.Find("Ring");
            if (ring != null) ring.GetComponent<Image>().color = WithAlpha(UIFactory.Accent, 0.7f);

            // Two bars: the pause glyph reads in any language, and neither icon
            // set has one.
            for (var i = 0; i < 2; i++)
            {
                var bar = UIFactory.Rect(rect, "Bar" + i, UIFactory.Ink, ArtLibrary.Pixel);
                UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2((i == 0 ? -1f : 1f) * 13f, 0f), new Vector2(13f, 44f));
            }
        }

        private void BuildPauseScreen()
        {
            if (pauseScreen != null) return;
            // A pausa é gameplay: fica com a moldura recortada original, sem o kit.
            using var gameplayFrames = UIFactory.GameplayFrames();

            Image backdrop;
            pauseScreen = UIFactory.Page(safeRoot, "PauseScreen", new Color(0.005f, 0.01f, 0.025f, 0.9f), out backdrop);
            pauseGroup = UIFactory.Group(pauseScreen);

            // The same cut-corner sheet as the login prompt and the other menus.
            // Opaque glass: whatever the pause interrupted (often the upgrade
            // cards) must not read through the menu.
            var accent = UIFactory.ActionBorder;
            var sheetHeight = UIFactory.Pick(760f, 800f);
            pauseSheet = BuildCutSheet(pauseScreen, "Panel", new Vector2(UIFactory.Pick(820f, 860f), sheetHeight), accent);
            pausePanel = pauseSheet.rectTransform;

            // A status tag straddling the top edge, like a console header.
            pauseTag = CutTag(pauseScreen, "Tag", ArtLibrary.IconStages, UIFactory.Accent, new Vector2(440f, 52f));
            UIFactory.Place(pauseTag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, sheetHeight * 0.5f),
                new Vector2(440f, 52f));

            var title = UIFactory.Label(pausePanel, "Title", "", UIFactory.TextTitle, UIFactory.Ink,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(740f, 80f));
            title.characterSpacing = 16f;
            UIFactory.AddUnderlay(title, 0.3f, 0.15f);
            UIFactory.GlowShadow(title, WithAlpha(UIFactory.Accent, 0.4f), 24f, 0.04f);

            pauseInfo = UIFactory.Label(pausePanel, "Info", "", UIFactory.TextBody, new Color(0.84f, 0.91f, 0.94f),
                TextAlignmentOptions.Top, true);
            UIFactory.Place(pauseInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(720f, 90f));
            pauseInfo.lineSpacing = 8f;

            var rule = UIFactory.Rect(pausePanel, "Rule", WithAlpha(UIFactory.Accent, 0.35f), ArtLibrary.Pixel);
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(640f, 2f));
            rule.raycastTarget = false;

            pauseActions = UIFactory.NewRect(pausePanel, "Actions");
            UIFactory.Stretch(pauseActions);

            // All three are quiet: each action plays its own sound (resume,
            // or the warning tone of the confirmation it opens).
            var width = UIFactory.Pick(700f, 720f);
            pauseResumeButton = CutSheetAction(pauseActions, "Resume", ArtLibrary.IconPlay, accent,
                UIFactory.ActionFillStrong, new Vector2(width, 128f), Sound.None, true, 42);
            UIFactory.Place(pauseResumeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -284f), new Vector2(width, 128f));
            UIFactory.GlowShadow(pauseResumeButton.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.35f), 26f, 0.08f);
            UIFactory.AddShine(pauseResumeButton.GetComponent<RectTransform>());

            pauseRestartButton = CutSheetAction(pauseActions, "Restart", ArtLibrary.IconRetry,
                WithAlpha(UIFactory.Evolved, 0.8f), UIFactory.ActionFillMuted, new Vector2(width, 112f), Sound.None);
            UIFactory.Place(pauseRestartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -436f), new Vector2(width, 112f));

            pauseMenuButton = CutSheetAction(pauseActions, "Menu", ArtLibrary.IconHome,
                WithAlpha(UIFactory.Danger, 0.85f), PauseDangerFill, new Vector2(width, 112f), Sound.None);
            UIFactory.Place(pauseMenuButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -572f), new Vector2(width, 112f));

            pauseConfirm = UIFactory.NewRect(pausePanel, "Confirm");
            UIFactory.Stretch(pauseConfirm);

            pauseConfirmTitle = UIFactory.Label(pauseConfirm, "Title", "", UIFactory.TextHeading, UIFactory.Ink,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(pauseConfirmTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -284f),
                new Vector2(720f, 56f));
            pauseConfirmTitle.characterSpacing = 6f;

            pauseConfirmBody = UIFactory.Label(pauseConfirm, "Body", "", UIFactory.TextBody, new Color(0.84f, 0.91f, 0.94f),
                TextAlignmentOptions.Top);
            UIFactory.Place(pauseConfirmBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -356f),
                new Vector2(680f, 150f));

            var half = (width - 32f) * 0.5f;
            pauseCancelButton = CutSheetAction(pauseConfirm, "Cancel", ArtLibrary.IconBack, WithAlpha(accent, 0.45f),
                UIFactory.ActionFillMuted, new Vector2(half, 112f), Sound.Back);
            UIFactory.Place(pauseCancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(-(half + 32f) * 0.5f, 48f), new Vector2(half, 112f));

            pauseConfirmButton = CutSheetAction(pauseConfirm, "Confirm", ArtLibrary.IconCheck, UIFactory.Danger,
                PauseDangerFill, new Vector2(half, 112f), Sound.Tap);
            UIFactory.Place(pauseConfirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2((half + 32f) * 0.5f, 48f), new Vector2(half, 112f));
            UIFactory.GlowShadow(pauseConfirmButton.GetComponent<Image>(), WithAlpha(UIFactory.Danger, 0.35f), 24f, 0.08f);

            Rebind(pauseResumeButton, ResumeGame);
            Rebind(pauseRestartButton, () => AskConfirm(
                L("REINICIAR?", "RESTART?", "¿REINICIAR?"),
                L("Volta à onda 1 com a mesma nave e fase.\nAs melhorias desta run se perdem.",
                    "Back to wave 1 with the same ship and stage.\nThis run's upgrades are lost.",
                    "Vuelves a la oleada 1 con la misma nave y fase.\nSe pierden las mejoras de esta partida."),
                L("REINICIAR", "RESTART", "REINICIAR"), RestartFromPause));
            Rebind(pauseMenuButton, () => AskConfirm(
                L("SAIR DA RUN?", "LEAVE THE RUN?", "¿SALIR DE LA PARTIDA?"),
                L("A run termina aqui e o progresso se perde.",
                    "The run ends here and its progress is lost.",
                    "La partida termina aquí y se pierde el progreso."),
                L("SAIR", "LEAVE", "SALIR"), QuitToMenuFromPause));
            Rebind(pauseCancelButton, CancelConfirm);
            Rebind(pauseConfirmButton, ConfirmPending);

            pauseConfirm.gameObject.SetActive(false);
            pauseScreen.gameObject.SetActive(false);
        }

        /// <summary>Dark red glass for the actions that throw the run away.</summary>
        private static readonly Color PauseDangerFill = Palette.Vidro(Palette.Magenta, 0.18f);

        private void RefreshPauseLanguage()
        {
            if (pauseScreen == null) return;
            SetLabel(pausePanel, "Title", L("PAUSA", "PAUSED", "PAUSA"));
            pauseTag.transform.Find("Label").GetComponent<TMP_Text>().text =
                L("SISTEMA EM PAUSA", "SYSTEM PAUSED", "SISTEMA EN PAUSA");
            SetButtonLabel(pauseResumeButton, L("CONTINUAR", "RESUME", "CONTINUAR"));
            SetButtonLabel(pauseRestartButton, L("REINICIAR", "RESTART", "REINICIAR"));
            SetButtonLabel(pauseMenuButton, L("MENU", "MENU", "MENÚ"));
            SetButtonLabel(pauseCancelButton, L("VOLTAR", "BACK", "VOLVER"));
        }

        private void RefreshPauseInfo()
        {
            builder.Length = 0;
            builder.Append(L("ONDA ", "WAVE ", "OLEADA ")).Append(run.waveIndex);
            if (!run.isInfinite) builder.Append(" / ").Append(RunController.WavesPerRun);
            builder.Append("   ·   ").Append(run.modeName);
            if (run.isInfinite) builder.Append("   ·   ").Append(L("PONTOS ", "SCORE ", "PUNTOS ")).Append(EndlessScore.Format(run.score));
            if (run.shipDefinition != null)
            {
                builder.Append('\n').Append(GameLocalization.Content(run.shipDefinition.name).ToUpperInvariant());
                if (run.ship != null)
                    builder.Append("   ·   ").Append(Mathf.CeilToInt(run.ship.hp)).Append(" / ")
                        .Append(Mathf.CeilToInt(run.ship.maxHp)).Append(L(" VIDA", " HULL", " VIDA"));
            }
            pauseInfo.text = builder.ToString();
        }

        // ------------------------------------------------------------------
        // Behaviour
        // ------------------------------------------------------------------

        private bool CanPause
        {
            get
            {
                return run != null && (run.phase == RunPhase.WaveInProgress ||
                    run.phase == RunPhase.UpgradeSelection);
            }
        }

        private void OpenPause()
        {
            if (paused || pauseScreen == null || !CanPause) return;

            // Close the details overlay first: it paused time itself, and its
            // saved time scale has to be restored before this one is taken.
            HideUpgradeDetails();
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.ClearHitStop();

            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
            pendingConfirm = null;
            Sfx.Play(Sound.PauseOpen);

            RefreshPauseLanguage();
            RefreshPauseInfo();
            pauseActions.gameObject.SetActive(true);
            pauseConfirm.gameObject.SetActive(false);
            TintPauseFrame(UIFactory.Accent);

            LeanTween.cancel(pauseScreen.gameObject);
            pauseScreen.gameObject.SetActive(true);
            pauseScreen.SetAsLastSibling();
            FadeScreenIn(pauseGroup, 0.18f);
            UIMotion.Pop(pausePanel, 0f, 0.3f, 0.82f);
            UIMotion.Pop(pauseTag.transform, 0.08f, 0.26f, 0.6f);
            UIMotion.Stagger(new Transform[] { pauseResumeButton.transform, pauseRestartButton.transform,
                pauseMenuButton.transform }, 0.1f, 0.05f);
        }

        private void ResumeGame()
        {
            if (!paused) return;
            Sfx.Play(Sound.PauseClose);
            ClosePause(true);
        }

        /// <summary>
        /// Hides the menu. <paramref name="restoreTime"/> is false when the run
        /// is being torn down, because the run controller sets the time scale
        /// itself and a restored 1.0 would unfreeze the upgrade screen.
        /// </summary>
        private void ClosePause(bool restoreTime)
        {
            if (!paused) return;
            paused = false;
            pendingConfirm = null;
            if (restoreTime) Time.timeScale = timeScaleBeforePause;
            if (pauseScreen == null) return;

            LeanTween.cancel(pauseScreen.gameObject);
            if (!restoreTime)
            {
                pauseScreen.gameObject.SetActive(false);
                return;
            }
            pauseGroup.blocksRaycasts = false;
            Juice.Fade(pauseGroup, 0f, 0.14f, 0f, () =>
            {
                if (!paused && pauseScreen != null) pauseScreen.gameObject.SetActive(false);
            });
        }

        private void AskConfirm(string title, string body, string confirmLabel, Action action)
        {
            pendingConfirm = action;
            pauseConfirmTitle.text = title;
            pauseConfirmBody.text = body;
            SetButtonLabel(pauseConfirmButton, confirmLabel);
            pauseActions.gameObject.SetActive(false);
            pauseConfirm.gameObject.SetActive(true);
            Sfx.Play(Sound.ConfirmPrompt);
            Juice.Punch(pausePanel, 0.04f, 0.25f);
            UIMotion.Stagger(new Transform[] { pauseConfirmTitle.transform, pauseConfirmBody.transform,
                pauseCancelButton.transform, pauseConfirmButton.transform }, 0.02f, 0.04f);
            TintPauseFrame(UIFactory.Danger);
        }

        /// <summary>The frame turns red while the menu asks about throwing the run away.</summary>
        private void TintPauseFrame(Color accent)
        {
            TintSheet(pauseSheet, WithAlpha(accent, accent == UIFactory.Accent ? UIFactory.ActionBorder.a : 0.9f));
            pauseTag.color = Color.Lerp(new Color(0.04f, 0.08f, 0.12f, 1f), accent, 0.22f);
            pauseTag.transform.Find("Label").GetComponent<TMP_Text>().color = accent;
            pauseTag.transform.Find("Icon").GetComponent<Image>().color = accent;
            var border = pauseTag.transform.Find("Border");
            if (border != null) border.GetComponent<Image>().color = WithAlpha(accent, 0.75f);
            var glow = pauseTag.GetComponent<LeTai.TrueShadow.TrueShadow>();
            if (glow != null) glow.Color = WithAlpha(accent, 0.45f);
        }

        private void CancelConfirm()
        {
            TintPauseFrame(UIFactory.Accent);
            pendingConfirm = null;
            pauseConfirm.gameObject.SetActive(false);
            pauseActions.gameObject.SetActive(true);
            UIMotion.Stagger(new Transform[] { pauseResumeButton.transform, pauseRestartButton.transform,
                pauseMenuButton.transform }, 0.02f, 0.04f);
        }

        private void ConfirmPending()
        {
            var action = pendingConfirm;
            pendingConfirm = null;
            if (action != null) action();
        }

        private void RestartFromPause()
        {
            var ship = run.shipDefinition;
            if (ship == null)
            {
                ClosePause(false);
                run.Restart();
                return;
            }
            // The run stays paused under the warp; the pause closes at the hand-over.
            WarpToStage(run.stageIndex, ship, () =>
            {
                ClosePause(false);
                run.BeginRun(ship);
            });
        }

        private void QuitToMenuFromPause()
        {
            ClosePause(false);
            run.Restart();
        }

        /// <summary>
        /// One back input for the whole game: it closes whatever is on top, and
        /// only pauses when there is nothing left to close.
        /// </summary>
        private void HandleBack()
        {
            if (DebugPopupOpen) { Sfx.Play(Sound.Back); CloseDebugPopup(); return; }
            if (CardRevealOpen) { CloseCardReveal(); return; }
            if (SupportPopupOpen) { Sfx.Play(Sound.Back); CloseSupportPopup(); return; }
            if (ReleaseNotesPopupOpen) { Sfx.Play(Sound.Back); CloseReleaseNotes(); return; }
            if (LoginPromptOpen)
            {
                Sfx.Play(Sound.Back);
                CloseLoginPrompt();
                return;
            }
            if (WelcomePopupOpen)
            {
                Sfx.Play(Sound.Back);
                if (welcomeReveal != null && welcomeReveal.gameObject.activeSelf) HideScreen(welcomeReveal);
                else CloseWelcomePopup();
                return;
            }
            if (paused)
            {
                if (pauseConfirm.gameObject.activeSelf)
                {
                    Sfx.Play(Sound.Back);
                    CancelConfirm();
                }
                else ResumeGame();
                return;
            }
            if (upgradeDetailsScreen != null && upgradeDetailsScreen.gameObject.activeSelf)
            {
                Sfx.Play(Sound.Back);
                HideUpgradeDetails();
                return;
            }
            if (CanPause)
            {
                OpenPause();
                return;
            }
            if (RewardsPopupOpen)
            {
                Sfx.Play(Sound.Back);
                CloseRewardsPopup();
                return;
            }
            if (StarterPopupOpen)
            {
                Sfx.Play(Sound.Back);
                CloseStarterPopup();
                return;
            }
            if (StorePopupOpen)
            {
                Sfx.Play(Sound.Back);
                CloseStorePopup();
                return;
            }
            if (deckScreen != null && deckScreen.gameObject.activeSelf)
            {
                Sfx.Play(Sound.Back);
                CloseShipDeck();
                return;
            }
            if (run.phase == RunPhase.ShipSelection &&
                (rankingPage != null && rankingPage.gameObject.activeSelf || levelsPage != null && levelsPage.gameObject.activeSelf))
            {
                Sfx.Play(Sound.Back);
                OpenMenuSection(MenuSection.Modes);
                return;
            }
            if (languagesPage != null && languagesPage.gameObject.activeSelf)
            {
                Sfx.Play(Sound.Back);
                OpenMenuSection(MenuSection.Settings);
                return;
            }
            if (run.phase == RunPhase.ShipSelection && !mainMenu.gameObject.activeSelf)
            {
                Sfx.Play(Sound.Back);
                GoToMainMenu();
            }
        }

        private static bool BackPressed()
        {
            // The Input System reports the Android back button as Escape.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.startButton.wasPressedThisFrame;
        }

        // A phone call or the home button in the middle of a wave should not
        // cost the run. Not in the editor, where clicking another window would
        // pause the game every time.
        private void OnApplicationPause(bool pausing)
        {
#if !UNITY_EDITOR
            if (pausing && built && run != null && run.phase == RunPhase.WaveInProgress) OpenPause();
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
#if !UNITY_EDITOR
            if (!focused && built && run != null && run.phase == RunPhase.WaveInProgress) OpenPause();
#endif
        }
    }
}
