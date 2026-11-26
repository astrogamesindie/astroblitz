// AstroCrawler - all screens and the HUD.
//
// Combat is automatic; the one decision in a run is a permanent upgrade before
// each wave. The interface has two arrangements: portrait (the default, 1080 x
// 1920 reference) and landscape (1920 x 1080), picked from the shape of the
// screen and rebuilt when it turns. Every tap target is at least ~48dp
// (UIFactory.Touch), no text drops below ~12sp (UIFactory.Text*), and anything
// with a universal symbol is an icon first and a word second. Copy is kept to
// what the player needs in the moment.
//
// Split by screen:
//   GameUI.cs           lifecycle, phase routing, shared helpers
//   GameUI.Hud.cs       in-run HUD, banners, toasts, upgrade details
//   GameUI.Menu.cs      main menu, stage select, settings
//   GameUI.Hangar.cs    ship select and the deck viewer
//   GameUI.Run.cs       the per-wave upgrade pick and the end-of-run screen
//   GameUI.Upgrades.cs  the permanent upgrade tree
//   GameUI.Pause.cs     the pause menu and the back button
//
// The HUD, ship select, upgrade pick and end screen are baked into the scene
// by the scene builder (Construct); the menu pages are built at runtime (Bind).

using System;
using System.Text;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI : MonoBehaviour
    {
        /// <summary>
        /// Bumped whenever what Construct builds changes. A scene baked with an
        /// older layout is rebuilt at runtime - correct, just slower to boot -
        /// until Tools > AstroCrawler > Reassar interface bakes the new one.
        /// </summary>
        public const int LayoutVersion = 35;

        private RunController run;
        private GameConfig config;
        private EffectRunner effects;
        private CombatWorld world;

        // Serialized so the interface can be authored into the scene instead of
        // built on the first frame. Construct fills these in; Bind re-attaches
        // the behaviour, because runtime listeners and C# events are not saved.
        [SerializeField] private int bakedLayout;
        [Tooltip("Arrangement of the baked screens. The scene is baked in portrait, the default.")]
        [SerializeField] private bool bakedPortrait;
        [SerializeField] private Canvas canvas;
        [Tooltip("Everything the player reads or taps lives under this, inside Screen.safeArea.")]
        [SerializeField] private RectTransform safeRoot;

        private const string SelectedShipKey = "AstroCrawler.SelectedShipId";
        private string selectedShipId;
        private bool built;

        private readonly StringBuilder builder = new StringBuilder();

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        /// <summary>Builds the hierarchy and then wires it. The runtime path.</summary>
        public void Build(RunController run, GameConfig config, EffectRunner effects, CombatWorld world)
        {
            Construct(config, ScreenIsPortrait);
            Bind(run, config, effects, world);
        }

        /// <summary>The screen is taller than wide: the portrait arrangement fits it.</summary>
        public static bool ScreenIsPortrait
        {
            get { return Screen.height > Screen.width; }
        }

        /// <summary>The scene builder's entry point: portrait, the default arrangement.</summary>
        public void Construct(GameConfig config)
        {
            Construct(config, true);
        }

        /// <summary>
        /// Creates the baked screens. Called at runtime by Build and at author
        /// time by the scene builder, so it wires no behaviour and starts no
        /// tweens: both would be lost - or saved half-way - in the scene.
        /// </summary>
        public void Construct(GameConfig config, bool portrait)
        {
            if (canvas != null)
            {
                return;
            }

            this.config = config;
            UIFactory.Portrait = portrait;
            bakedPortrait = portrait;

            canvas = UIFactory.CreateCanvas("AstroCanvas", transform, 100);
            canvas.gameObject.AddComponent<ScreenFit>();

            safeRoot = UIFactory.NewRect(canvas.transform, "SafeArea");
            UIFactory.Stretch(safeRoot);
            safeRoot.gameObject.AddComponent<SafeArea>();

            BuildHud(safeRoot);
            BuildShipScreen(safeRoot);
            BuildEvolutionScreen(safeRoot);
            BuildEndScreen(safeRoot);

            bakedLayout = LayoutVersion;
        }

        /// <summary>
        /// Throws away a baked canvas so Construct can build the current layout.
        /// The scene builder uses it to re-bake; Bind uses it when the scene is
        /// older than the code.
        /// </summary>
        public void DiscardLayout()
        {
            // Immediately, also in Play: the screens built at runtime test their
            // own reference for null before building, and a deferred Destroy
            // would leave them pointing at the old canvas for the rest of the frame.
            if (canvas != null) DestroyImmediate(canvas.gameObject);
            canvas = null;
            safeRoot = null;
            bakedLayout = 0;
            ClearHangarReferences();
            evolutionWidgets.Clear();
            ClearRuntimeLayout();
        }

        /// <summary>
        /// Forgets what the runtime screens built and what the HUD last drew, so
        /// a rebuilt layout starts clean and redraws everything once.
        /// </summary>
        private void ClearRuntimeLayout()
        {
            stageGlows.Clear();
            stageDetail = null;
            stagePlay = null;
            rankingRows.Clear();
            passiveNodes.Clear();
            if (purchaseStore != null) purchaseStore.Changed -= RefreshStorePanel;

            lastPiles = null;
            lastUpgradeIconCount = -1;
            lastUpgradeIconShip = null;
            lastHpFraction = -1f;
            lastBossFraction = -1f;
            lastWaveIndex = -1;
            lastAlive = -1;
            lastShield = -1;
            lastXp = -1;
            lastLevel = -1;
            shownCredits = -1;
        }

        /// <summary>
        /// Attaches the behaviour to the hierarchy, whether it was just built or
        /// loaded with the scene, and builds the runtime-only menu pages.
        /// </summary>
        public void Bind(RunController run, GameConfig config, EffectRunner effects, CombatWorld world)
        {
            this.run = run;
            this.config = config;
            this.effects = effects;
            this.world = world;

            selectedShipId = PlayerPrefs.GetString(SelectedShipKey, "");
            // A ship not owned (or gone) falls back to the free one.
            if ((config.GetShip(selectedShipId) == null || !run.shipUnlocks.IsUnlocked(selectedShipId)) &&
                config.ships.Count > 0)
            {
                selectedShipId = config.GetShip(ShipUnlocks.FreeShipId) != null
                    ? ShipUnlocks.FreeShipId : config.ships[0].id;
                PlayerPrefs.SetString(SelectedShipKey, selectedShipId);
                ProgressSnapshot.SaveLocal();
            }

            // A stage saved from before stages were locked per ship may be out of reach.
            run.ClampStageToShip(selectedShipId);

            var portrait = ScreenIsPortrait;
            if (canvas != null && bakedLayout != LayoutVersion)
            {
                Debug.Log("[AstroCrawler] A interface assada na cena e de uma versao anterior; " +
                          "reconstruindo em runtime. Use Tools > AstroCrawler > Reassar interface.");
                DiscardLayout();
            }
            else if (canvas != null && bakedPortrait != portrait)
            {
                // The scene is baked in portrait; a landscape screen builds its own.
                DiscardLayout();
            }
            if (canvas == null)
            {
                Construct(config, portrait);
            }
            UIFactory.Portrait = bakedPortrait;

            RestoreText();
            BindButtons();

            BuildMainMenu();
#if UNITY_EDITOR
            BuildEditorTestControls();
#endif
            BuildLevelsPage();
            BuildModesPage();
            BuildRankingPage();
            BuildSettingsPage();
            BuildStorePanel();
            BuildStarterPack();
            BuildPassiveScreen();
            BuildDeckScreen();
            BuildPauseScreen();
            BuildAdControls();
            BuildReleaseAccess();
            BuildSpecialControl();
            BuildUpdateScreen();
            RefreshLanguage();

            run.onPhaseChanged += HandlePhaseChanged;
            run.onWaveStarted += HandleWaveStarted;
            run.onWaveCleared += HandleWaveCleared;
            run.cards.onUpgradeAcquired += HandleUpgradeAcquired;

            built = true;
            HandlePhaseChanged(run.phase);
            OpenPendingReleaseNotes();
        }

        /// <summary>
        /// Gives every label its font back and re-applies text shadows.
        /// Labels authored into the scene keep a reference to the baked font
        /// asset; this only catches ones saved before that asset existed.
        /// </summary>
        private void RestoreText()
        {
            var font = UIFactory.TmpFont;
            if (font != null)
            {
                var labels = GetComponentsInChildren<TMP_Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    if (labels[i].font == null)
                    {
                        labels[i].font = font;
                    }
                }
            }

            var underlays = GetComponentsInChildren<TextUnderlay>(true);
            for (var i = 0; i < underlays.Length; i++)
            {
                underlays[i].Apply();
            }
        }

        /// <summary>
        /// Re-attaches every click handler on the baked screens. Listeners are
        /// removed first, so this is safe to run twice.
        /// </summary>
        private void BindButtons()
        {
            for (var i = 0; i < evolutionWidgets.Count; i++)
            {
                var widget = evolutionWidgets[i];
                if (widget == null) continue;
                widget.RebindEvents();
                widget.onClicked -= HandleEvolutionChoice;
                widget.onClicked += HandleEvolutionChoice;
                widget.onInfoRequested -= HandleEvolutionInfo;
                widget.onInfoRequested += HandleEvolutionInfo;
            }

            if (upgradeDetailsCard != null)
            {
                upgradeDetailsCard.RebindEvents();
            }

            Rebind(detailsDismissButton, HideUpgradeDetails);
            Rebind(detailsCloseButton, HideUpgradeDetails);
            Rebind(againButton, ReplayRun);
            Rebind(nextLevelButton, PlayNextLevel);
            Rebind(endMenuButton, () => AfterResultAd(() => run.Restart()));
            Rebind(pauseButton, OpenPause);
            Rebind(speedButton, CycleGameSpeed);
            Rebind(upgradesToggleButton, ToggleUpgradesPanel);
            ApplyUpgradesPanelState(false);
            BindHangarButtons();
        }

        private void OnDestroy()
        {
            if (updateGate != null) updateGate.Changed -= RefreshUpdateScreen;
            if (ads != null) { ads.Changed -= RefreshAdControls; ads.SetMenuVisible(false); }
            if (purchaseStore != null) purchaseStore.Changed -= RefreshStorePanel;
            UnbindShipStore();
            UnbindStarterPack();
            DetachShip();
            UnsubscribeRun();
        }

        private void UnsubscribeRun()
        {
            if (run == null)
            {
                return;
            }
            run.onPhaseChanged -= HandlePhaseChanged;
            run.onWaveStarted -= HandleWaveStarted;
            run.onWaveCleared -= HandleWaveCleared;
            run.cards.onUpgradeAcquired -= HandleUpgradeAcquired;
        }

        // ------------------------------------------------------------------
        // Orientation
        // ------------------------------------------------------------------

        private float orientationChangedAt = -1f;

        /// <summary>
        /// Rebuilds the interface in the other arrangement when the screen turns
        /// (the setting was changed, or the editor's Game view was resized). It
        /// waits for the shape to settle and for nothing to be holding time
        /// frozen, so a half-finished rotation does not build twice.
        /// </summary>
        private void WatchOrientation()
        {
            OrientationSetting.Tick();
            if (ScreenIsPortrait == bakedPortrait || canvas == null)
            {
                orientationChangedAt = -1f;
                return;
            }
            if (orientationChangedAt < 0f)
            {
                orientationChangedAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - orientationChangedAt < 0.3f || paused) return;
            if (upgradeDetailsScreen != null && upgradeDetailsScreen.gameObject.activeSelf) return;
            RebuildLayout();
        }

        private void RebuildLayout()
        {
            orientationChangedAt = -1f;
            var restoreNotes = ReleaseNotesPopupOpen;
            var restoreSupport = SupportPopupOpen;
            SaveSupportDraft();
            // Land back on the page that was open (the switch itself is made
            // from Ajustes).
            var reopen = MenuSection.Play;
            if (settingsPage != null && settingsPage.gameObject.activeInHierarchy) reopen = MenuSection.Settings;
            else if (languagesPage != null && languagesPage.gameObject.activeInHierarchy) reopen = MenuSection.Languages;
            else if (levelsPage != null && levelsPage.gameObject.activeInHierarchy) reopen = MenuSection.Stages;
            else if (modesPage != null && modesPage.gameObject.activeInHierarchy) reopen = MenuSection.Modes;
            else if (rankingPage != null && rankingPage.gameObject.activeInHierarchy) reopen = MenuSection.Ranking;
            else if (baseDeckPage != null && baseDeckPage.gameObject.activeInHierarchy) reopen = MenuSection.BaseDeck;
            else if (shipScreen != null && shipScreen.gameObject.activeInHierarchy)
                reopen = shipScreenUpgradesMode ? MenuSection.Upgrades : MenuSection.Ships;

            UnsubscribeRun();
            built = false;
            DiscardLayout();
            Bind(run, config, effects, world);

            if (reopen != MenuSection.Play && run.phase == RunPhase.ShipSelection) OpenMenuSection(reopen);
            if (restoreNotes && !ReleaseNotesPopupOpen) OpenReleaseNotes();
            if (restoreSupport) OpenSupportPopup();
        }

        // ------------------------------------------------------------------
        // Phase routing
        // ------------------------------------------------------------------

        private void HandlePhaseChanged(RunPhase phase)
        {
            // Restarting or quitting from the pause menu lands here too.
            ClosePause(false);

            var selectingShip = phase == RunPhase.ShipSelection;
            var inRun = phase == RunPhase.WaveInProgress || phase == RunPhase.UpgradeSelection;

            // The HUD only means something during a run; outside one it showed
            // through the translucent menus as an empty hull bar.
            if (hudRoot != null) hudRoot.gameObject.SetActive(inRun);
            if (inRun) RefreshSpeedButton(false);
            if (SoundDirector.Instance != null) SoundDirector.Instance.SetInRun(inRun);

            HideMenuPages();
            if (mainMenu != null) mainMenu.gameObject.SetActive(selectingShip);
            if (selectingShip)
            {
                RefreshMainMenu();
                if (!string.IsNullOrEmpty(passiveShipId)) RefreshPassiveScreen();
                FadeScreenIn(UIFactory.Group(mainMenu), 0.3f);
                StaggerMainMenu();
            }

            evolutionScreen.gameObject.SetActive(false);
            endScreen.gameObject.SetActive(false);
            HideUpgradeDetails();

            switch (phase)
            {
                case RunPhase.UpgradeSelection:
                    ShowEvolutionScreen();
                    break;
                case RunPhase.Victory:
                    ShowEndScreen(true);
                    break;
                case RunPhase.Defeat:
                    ShowEndScreen(false);
                    break;
            }
            SyncAdNavigation();
            RefreshUpdateScreen();
        }

        /// <summary>
        /// Screens and sheets leave through here: a quick fade (UIMotion.Exit),
        /// then the object is deactivated and <paramref name="after"/> runs.
        /// </summary>
        private static void HideScreen(Component screen, Action after = null)
        {
            if (screen == null)
            {
                after?.Invoke();
                return;
            }
            var go = screen.gameObject;
            if (!go.activeSelf)
            {
                after?.Invoke();
                return;
            }
            UIMotion.Exit(UIFactory.Group(screen), () =>
            {
                if (go != null) go.SetActive(false);
                after?.Invoke();
            });
        }

        /// <summary>Every screen and sheet arrives through here (UIMotion: fade and settle).</summary>
        private static void FadeScreenIn(CanvasGroup group, float time = 0.28f)
        {
            if (group == null)
            {
                return;
            }
            UIMotion.Enter(group, Mathf.Max(0.22f, time + 0.04f));
        }

        // ------------------------------------------------------------------
        // Per-frame
        // ------------------------------------------------------------------

        private void Update()
        {
            // If Bind threw partway through, half the fields are null. One
            // logged failure is useful; the same exception every frame is not.
            if (run == null || !built)
            {
                return;
            }

            if (ads != null && ads.Showing) return;
            if (IntroScreenOpen)
            {
                WatchOrientation();
                if (BackPressed()) HandleBack();
                return; // No login/calendar/gameplay overlay can cover these screens.
            }
            if (updateGate != null && updateGate.BlocksPlay) return;
            // Nothing answers Back or rebuilds the layout in the middle of a jump.
            if (WarpTransition.Running) return;

            WatchOrientation();
            if (!built) return;

            if (BackPressed())
            {
                HandleBack();
            }

            TrackShip();
            TickToast();
            RefreshHud();
            if (specialControl != null) specialControl.Refresh();
            TickCoinsAd();
            TickRewards();
            TickAccountRow();
            TickWelcome();
            TickStarterPack();
        }

        // ------------------------------------------------------------------
        // Language
        // ------------------------------------------------------------------

        private void RefreshLanguage()
        {
            RefreshMenuLanguage();
            RefreshShipLanguage();
            RefreshPassiveLanguage();
            RefreshPauseLanguage();
            RefreshWelcome();
            RefreshRunLanguage();
            RefreshHudLanguage();
            RefreshAdControls();
            RefreshUpdateScreen();

            RefreshMainMenu();
            if (passiveScreen != null && !string.IsNullOrEmpty(passiveShipId)) RefreshPassiveScreen();
            GameLanguageFonts.ApplyScene();
        }

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------

        private static string L(string portuguese, string english, string spanish)
        {
            return GameLocalization.Choose(portuguese, english, spanish);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>A panel tinted towards an accent: how "this one is chosen" reads.</summary>
        private static Color SelectedPanelColor(Color accent, float strength)
        {
            return new Color(0.11f + accent.r * strength, 0.14f + accent.g * strength,
                0.2f + accent.b * strength, 0.98f);
        }

        private static Color Darken(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        private static void SetLabel(Transform root, string path, string value)
        {
            if (root == null) return;
            var child = root.Find(path);
            if (child == null) return;
            var label = child.GetComponent<TMP_Text>();
            if (label != null) label.text = value;
        }

        private static void SetButtonLabel(Component button, string value)
        {
            if (button == null) return;
            var caption = button.transform.Find("Caption");
            var label = caption != null ? caption.GetComponent<TMP_Text>() : null;
            if (label != null && !label.enableAutoSizing)
            {
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = Mathf.Min(label.fontSize, Mathf.Max(18f, label.fontSize * 0.7f));
                label.enableAutoSizing = true;
            }
            SetLabel(button.transform, "Caption", value);
        }

        private static void Rebind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static int CompareCardDisplayOrder(CardDefinition a, CardDefinition b)
        {
            var aExclusive = a.category != CardCategory.Common;
            var bExclusive = b.category != CardCategory.Common;
            if (aExclusive != bExclusive)
            {
                return aExclusive ? 1 : -1;
            }
            return StringComparer.CurrentCultureIgnoreCase.Compare(a.name, b.name);
        }

        /// <summary>
        /// The line under a ship's name in the hangar: its permanent level (all
        /// upgrade levels bought for it), the number in the ship's colour.
        /// </summary>
        private string ShipLevelLine(string shipId, Color accent)
        {
            var level = run != null ? run.passiveUpgrades.TotalLevel(shipId) : 0;
            return L("NÍVEL ", "LEVEL ", "NIVEL ") + "<color=" + UIFactory.ColorTag(accent) + ">" + level + "</color>";
        }
    }
}
