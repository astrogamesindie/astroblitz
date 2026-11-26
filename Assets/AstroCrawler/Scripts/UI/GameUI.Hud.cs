// AstroCrawler - the in-run HUD.
//
// Laid out for a thumb on each side of a landscape phone: the wave and the
// hull top left, the acquired upgrades top right, pause in the corner where
// every mobile game keeps it with the 2x/3x speed controls under it, and nothing in
// the middle of the arena. Numbers
// that change animate to their new value, and the hull bar keeps a slower
// "ghost" layer so a burst of damage leaves a visible scar.

using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        [Header("HUD")]
        [SerializeField] private RectTransform hudRoot;
        [SerializeField] private TMP_Text waveCount;
        [SerializeField] private TMP_Text waveNote;
        [SerializeField] private RectTransform waveTrack;
        [SerializeField] private List<RectTransform> waveNodes = new List<RectTransform>();
        [SerializeField] private List<Image> waveNodeFrames = new List<Image>();
        [SerializeField] private List<Image> waveNodeFills = new List<Image>();
        [SerializeField] private List<Image> waveLinks = new List<Image>();
        [SerializeField] private List<TrueShadow> waveNodeGlows = new List<TrueShadow>();
        [SerializeField] private TrueShadow speedGlow;
        [SerializeField] private TrueShadow hpGlow;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image hpGhost;
        [SerializeField] private RectTransform hullPanel;

        private const float HealFeedbackInterval = 0.45f;
        private float lastHealFeedback = -1f;
        private float feedbackHp = -1f;
        [SerializeField] private Image xpFill;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private TMP_Text pilesLabel;
        [SerializeField] private RectTransform upgradeIcons;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speedButton;
        [SerializeField] private RectTransform upgradesPanel;
        [SerializeField] private Button upgradesToggleButton;
        [SerializeField] private RectTransform upgradesToggleIcon;
        [SerializeField] private RectTransform bossPanel;
        [SerializeField] private Image bossFill;
        [SerializeField] private TMP_Text bossLabel;
        [SerializeField] private TMP_Text toastLabel;
        [SerializeField] private RectTransform toastRoot;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text bannerLabel;
        [SerializeField] private RectTransform bannerRoot;
        [SerializeField] private CanvasGroup bannerGroup;

        [Header("Detalhe de melhoria")]
        [SerializeField] private RectTransform upgradeDetailsScreen;
        [SerializeField] private CanvasGroup upgradeDetailsGroup;
        [SerializeField] private CardWidget upgradeDetailsCard;
        [SerializeField] private Button detailsDismissButton;
        [SerializeField] private Button detailsCloseButton;

        // Initial dimensions; the live panel fits the acquired icons and safe area.
        private const float UpgradesPanelHeight = 250f;
        private const float UpgradesPanelWidth = 468f;
        private const float UpgradesHeaderHeight = 68f;
        private const string UpgradesMinimizedKey = "AstroCrawler.UpgradesMinimized";
        private bool upgradesMinimized;

        private const int IconColumns = 6;
        private const float IconSize = 72f;
        private const float IconStep = 84f;
        private int upgradeIconSlots;
        private bool upgradeHasSpecial;
        private Vector2 upgradeLayoutSize;

        private float timeScaleBeforeDetails = 1f;
        private bool detailsPausedGame;
        private float toastTimer;
        private string lastPiles;
        private int lastUpgradeIconCount = -1;
        private ShipDefinition lastUpgradeIconShip;
        private float lastHpFraction = -1f;
        private float lastBossFraction = -1f;
        private int lastWaveIndex = -1;
        private int lastAlive = -1;
        private int lastShield = -1;
        private int lastXp = -1;
        private int lastLevel = -1;
        private PlayerShip trackedShip;

        // ------------------------------------------------------------------
        // Construction (baked)
        // ------------------------------------------------------------------

        private void BuildHud(Transform parent)
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            hudRoot = UIFactory.NewRect(parent, "HUD");
            UIFactory.Stretch(hudRoot);

            // Keeps the HUD text readable over a bright explosion at the top of
            // the arena without boxing the arena in.
            UIFactory.EdgeFade(hudRoot, "TopFade", true, 280f, 0.55f);

            BuildHullPanel();
            BuildUpgradesPanel();
            BuildPauseButton(hudRoot);
            BuildSpeedButton(hudRoot);
            BuildWaveTrack();
            BuildBossPanel();
            BuildBanner();
            BuildToast();
            BuildUpgradeDetails(hudRoot);
        }

        /// <summary>
        /// The sci-fi frame of a HUD panel: a faint coloured halo (True Shadow,
        /// additive) and small glowing corner brackets. Static, so it costs one
        /// blur at the first frame and nothing after.
        /// </summary>
        private static void HudFrame(Image panel, Color accent)
        {
            UIFactory.GlowShadow(panel, WithAlpha(accent, 0.16f), 22f, 0.03f);
            HudBrackets(panel.rectTransform, accent);
        }

        private static void HudBrackets(RectTransform panel, Color accent)
        {
            var brackets = UIFactory.Brackets(panel, WithAlpha(accent, 0.75f), 22f, -3f);
            var corners = brackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < corners.Length; i++)
            {
                // Decoration only: it must never swallow a tap meant for the panel.
                corners[i].raycastTarget = false;
            }
        }

        private void BuildHullPanel()
        {
            var left = UIFactory.Panel9(hudRoot, "LeftPanel", UIFactory.Panel);
            hullPanel = left.rectTransform;
            UIFactory.Place(hullPanel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(600f, 160f));
            UIFactory.DropShadow(left, 26f, 10f, 0.55f);
            HudFrame(left, UIFactory.Accent);

            waveNote = UIFactory.Label(hullPanel, "Note", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(waveNote.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -12f),
                new Vector2(548f, 34f));
            waveNote.characterSpacing = 2f;

            hpFill = UIFactory.GhostBar(hullPanel, "HpBar", UIFactory.Good,
                Palette.Alfa(Palette.Magenta, 0.75f), Palette.Vidro(Palette.Magenta, 0.1f, 0.95f), out hpGhost);
            UIFactory.Place((RectTransform)hpFill.rectTransform.parent, new Vector2(0f, 0f),
                new Vector2(26f, 68f), new Vector2(548f, 46f));

            hpLabel = UIFactory.Label(hullPanel, "HpLabel", "", UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(hpLabel.rectTransform, new Vector2(0f, 0f), new Vector2(26f, 68f),
                new Vector2(548f, 46f));
            UIFactory.AddUnderlay(hpLabel);

            // A halo behind the hull bar's track: green while healthy, red when low.
            var hpBack = ((RectTransform)hpFill.rectTransform.parent).Find("Back").GetComponent<Image>();
            hpGlow = UIFactory.GlowShadow(hpBack, WithAlpha(UIFactory.Good, 0.28f), 16f, 0.04f);

            BuildXpBar();
        }

        private void BuildUpgradesPanel()
        {
            // Landscape: left of the pause button, which owns the corner.
            // Portrait: bottom left, opposite the ability and speed controls.
            var width = UpgradesPanelWidth;
            var right = UIFactory.Panel9(hudRoot, "RightPanel", UIFactory.Panel);
            upgradesPanel = right.rectTransform;
            if (UIFactory.Portrait)
                UIFactory.Place(right.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 32f),
                    new Vector2(width, UpgradesPanelHeight));
            else
                UIFactory.Place(right.rectTransform, new Vector2(1f, 1f), new Vector2(-172f, -24f),
                    new Vector2(width, UpgradesPanelHeight));
            UIFactory.DropShadow(right, 26f, 10f, 0.55f);
            HudFrame(right, UIFactory.Accent);

            pilesLabel = UIFactory.Label(right.rectTransform, "Piles", "", UIFactory.TextCaption, UIFactory.Ink,
                TextAlignmentOptions.TopLeft, true);
            UIFactory.Place(pilesLabel.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -14f),
                new Vector2(width - 44f, 40f));
            pilesLabel.characterSpacing = 3f;

            upgradeIcons = UIFactory.NewRect(right.rectTransform, "UpgradeIcons");
            UIFactory.Place(upgradeIcons, new Vector2(0f, 1f), new Vector2(22f, -66f),
                new Vector2(width - 44f, UpgradesPanelHeight - 82f));

            // The whole header is the tap target (a chevron alone would be too
            // small for a thumb); the chevron only shows which way it goes.
            var header = UIFactory.Rect(right.rectTransform, "Toggle", new Color(1f, 1f, 1f, 0f), ArtLibrary.Pixel);
            header.raycastTarget = true;
            UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(width, UpgradesHeaderHeight));
            upgradesToggleButton = header.gameObject.AddComponent<Button>();
            upgradesToggleButton.targetGraphic = header;
            upgradesToggleButton.transition = Selectable.Transition.None;
            var feedback = header.gameObject.AddComponent<ButtonFeedback>();
            feedback.clickSound = Sound.Toggle;

            var chevron = UIFactory.Rect(header.rectTransform, "Chevron", UIFactory.InkDim, ArtLibrary.IconNext);
            chevron.preserveAspect = true;
            upgradesToggleIcon = chevron.rectTransform;
            UIFactory.Place(upgradesToggleIcon, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(34f, 34f));
            upgradesToggleIcon.pivot = new Vector2(0.5f, 0.5f);
            upgradesToggleIcon.anchoredPosition = new Vector2(-35f, 0f);
        }

        private void ToggleUpgradesPanel()
        {
            upgradesMinimized = !upgradesMinimized;
            PlayerPrefs.SetInt(UpgradesMinimizedKey, upgradesMinimized ? 1 : 0);
            PlayerPrefs.Save();
            ApplyUpgradesPanelState(true);
        }

        /// <summary>
        /// Minimized, the panel is just its header ("MELHORIAS · n") and the
        /// arena behind it is free. The choice is remembered.
        /// </summary>
        private void ApplyUpgradesPanelState(bool fromTap)
        {
            if (upgradesPanel == null) return;
            if (!fromTap) upgradesMinimized = PlayerPrefs.GetInt(UpgradesMinimizedKey, 0) == 1;

            LayoutUpgradesPanel();
            upgradeIcons.gameObject.SetActive(!upgradesMinimized);
            if (upgradesToggleIcon != null)
            {
                // Fold toward the edge that owns the panel; expand away from it.
                var angle = upgradesMinimized ? -90f : 90f;
                upgradesToggleIcon.localRotation = Quaternion.Euler(0f, 0f, UIFactory.Portrait ? -angle : angle);
            }
        }

        private void LayoutUpgradesPanel()
        {
            if (upgradesPanel == null || hudRoot == null) return;
            var specialSlots = upgradeHasSpecial ? 1 : 0;
            var cardSlots = Mathf.Max(0, upgradeIconSlots - specialSlots);
            var columns = Mathf.Max(1, specialSlots + Mathf.Min(IconColumns, cardSlots));
            var rows = cardSlots > IconColumns ? 2 : 1;
            var contentWidth = columns * IconStep - (IconStep - IconSize);
            // Reserve the opposite controls, hull panel and margins in canvas units.
            var available = Mathf.Max(180f, hudRoot.rect.width - UIFactory.Pick(844f, 324f));
            var width = Mathf.Min(contentWidth + 44f, available);
            var scale = Mathf.Min(1f, (width - 44f) / contentWidth);
            var contentHeight = rows * IconStep - (IconStep - IconSize);
            var height = upgradesMinimized ? UpgradesHeaderHeight : 82f + contentHeight * scale;
            var size = new Vector2(width, height);
            if (size == upgradeLayoutSize) return;
            upgradeLayoutSize = size;
            upgradesPanel.sizeDelta = size;
            pilesLabel.rectTransform.sizeDelta = new Vector2(width - 84f, 40f);
            // Small panels keep only the fold control; the long heading must
            // not force empty icon slots or overflow the compact frame.
            pilesLabel.gameObject.SetActive(width >= 320f);
            pilesLabel.enableAutoSizing = true;
            pilesLabel.fontSizeMin = 22f;
            ((RectTransform)upgradesToggleButton.transform).sizeDelta = new Vector2(width, UpgradesHeaderHeight);
            upgradeIcons.sizeDelta = new Vector2(contentWidth, contentHeight);
            upgradeIcons.localScale = Vector3.one * scale;
            var slot = 0;
            for (var i = 0; i < upgradeIcons.childCount; i++)
            {
                var child = (RectTransform)upgradeIcons.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                child.anchoredPosition = UpgradeIconPosition(slot);
                slot++;
            }
        }

        private Vector2 UpgradeIconPosition(int slot)
        {
            if (upgradeHasSpecial && slot == 0) return Vector2.zero;
            var cardSlot = slot - (upgradeHasSpecial ? 1 : 0);
            var row = cardSlot / IconColumns;
            var column = cardSlot % IconColumns + (upgradeHasSpecial && row == 0 ? 1 : 0);
            return new Vector2(column * IconStep, -row * IconStep);
        }

        /// <summary>
        /// Each tap steps 1x, 2x, 3x and back.
        /// </summary>
        private void BuildSpeedButton(RectTransform hud)
        {
            speedButton = UIFactory.RoundButton(hud, "Speed", null, UIFactory.Panel, 128f, Sound.Tap);
            speedGlow = UIFactory.GlowShadow(speedButton.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.1f), 22f, 0.05f);
            var rect = speedButton.GetComponent<RectTransform>();
            // Keep speed directly below pause in both orientations.
            UIFactory.Place(rect, new Vector2(1f, 1f), new Vector2(-24f, -168f),
                new Vector2(128f, 128f));
            var label = UIFactory.Label(rect, "Caption", "1×", 44, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(label.rectTransform, 8f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.AddUnderlay(label);
        }

        private const float WaveNodeStep = 58f;
        private const float WaveNodeSize = 20f;
        private const float WaveBossSize = 46f;

        /// <summary>
        /// The stage at a glance, top centre: one diamond per wave joined by a
        /// line, the boss as an icon at the end. Beaten waves are filled, the
        /// current one is larger and lit, the ones ahead are hollow.
        /// </summary>
        private void BuildWaveTrack()
        {
            if (waveTrack != null) Destroy(waveTrack.gameObject);
            waveNodes.Clear();
            waveNodeFrames.Clear();
            waveNodeFills.Clear();
            waveLinks.Clear();
            waveNodeGlows.Clear();

            var count = run != null ? run.waveTrackLength : RunController.WavesPerRun;
            waveTrack = UIFactory.NewRect(hudRoot, "WaveTrack");
            // Portrait: its own row under the hull panel and the corner buttons.
            UIFactory.Place(waveTrack, new Vector2(0.5f, 1f), new Vector2(0f, UIFactory.Pick(-28f, -204f)),
                new Vector2((count - 1) * WaveNodeStep + 80f, 64f));

            var start = -(count - 1) * WaveNodeStep * 0.5f;

            // Links first, so the diamonds sit on top of the line ends.
            for (var i = 0; i < count - 1; i++)
            {
                var link = UIFactory.Rect(waveTrack, "Link" + i, UIFactory.InkDim, ArtLibrary.Pixel);
                UIFactory.Place(link.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(start + (i + 0.5f) * WaveNodeStep, 0f), new Vector2(WaveNodeStep, 3f));
                link.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                link.rectTransform.anchoredPosition = new Vector2(start + (i + 0.5f) * WaveNodeStep, 0f);
                waveLinks.Add(link);
            }

            for (var i = 0; i < count; i++)
            {
                var boss = i == count - 1;
                var node = UIFactory.NewRect(waveTrack, "Node" + i);
                node.anchorMin = node.anchorMax = node.pivot = new Vector2(0.5f, 0.5f);
                node.anchoredPosition = new Vector2(start + i * WaveNodeStep, 0f);
                node.sizeDelta = Vector2.one * (boss ? WaveBossSize : WaveNodeSize);
                waveNodes.Add(node);

                if (boss)
                {
                    // A dark plate keeps the line from showing through the icon.
                    var plate = UIFactory.Rect(node, "Frame", UIFactory.PanelDeep, ArtLibrary.CircleFilled);
                    UIFactory.Stretch(plate.rectTransform, -4f);
                    var icon = UIFactory.Rect(node, "Fill", UIFactory.Danger, ArtLibrary.IconBossLevel);
                    icon.preserveAspect = true;
                    UIFactory.Stretch(icon.rectTransform);
                    waveNodeFrames.Add(plate);
                    waveNodeFills.Add(icon);
                    waveNodeGlows.Add(UIFactory.GlowShadow(plate, WithAlpha(UIFactory.Danger, 0.4f), 18f, 0.1f));
                    continue;
                }

                // A square turned 45 degrees: an outline with a smaller fill.
                node.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var frame = UIFactory.Rect(node, "Frame", UIFactory.InkDim, ArtLibrary.Pixel);
                UIFactory.Stretch(frame.rectTransform);
                var fill = UIFactory.Rect(node, "Fill", UIFactory.PanelDeep, ArtLibrary.Pixel);
                UIFactory.Stretch(fill.rectTransform, 3f);
                waveNodeFrames.Add(frame);
                waveNodeFills.Add(fill);
                waveNodeGlows.Add(UIFactory.GlowShadow(frame, WithAlpha(UIFactory.Accent, 0f), 14f, 0.1f));
            }

            // Enemies left in the wave, under the diamond the player is on.
            waveCount = UIFactory.Label(waveTrack, "Count", "", UIFactory.TextCaption, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            waveCount.rectTransform.anchorMin = waveCount.rectTransform.anchorMax =
                waveCount.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            waveCount.rectTransform.sizeDelta = new Vector2(320f, 34f);
            waveCount.rectTransform.anchoredPosition = new Vector2(start, -42f);
            waveCount.characterSpacing = 2f;
            waveCount.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.AddUnderlay(waveCount);
        }

        private void RefreshWaveTrack()
        {
            if (waveTrack == null) return;

            if (waveNodes.Count != run.waveTrackLength) BuildWaveTrack();
            var current = run.waveTrackIndex;
            var hollow = new Color(0.03f, 0.05f, 0.09f, 0.95f);
            var ahead = WithAlpha(UIFactory.InkDim, 0.7f);

            for (var i = 0; i < waveNodes.Count; i++)
            {
                var boss = i == waveNodes.Count - 1;
                var isCurrent = i == current;
                var done = i < current;
                var node = waveNodes[i];

                LeanTween.cancel(node.gameObject, false, TweenAction.SCALE);
                node.localScale = Vector3.one * (isCurrent ? (boss ? 1.25f : 1.8f) : 1f);

                if (boss)
                {
                    waveNodeFills[i].color = isCurrent ? UIFactory.Danger : WithAlpha(UIFactory.Danger, 0.6f);
                    waveNodeFrames[i].color = isCurrent ? Palette.Vidro(Palette.Magenta, 0.22f) : UIFactory.PanelDeep;
                }
                else
                {
                    waveNodeFrames[i].color = isCurrent ? UIFactory.Ink : done ? UIFactory.Accent : ahead;
                    waveNodeFills[i].color = isCurrent ? UIFactory.Accent
                        : done ? WithAlpha(UIFactory.Accent, 0.55f) : hollow;
                }

                // The node the player is on shines; beaten ones keep a trace of it.
                if (i < waveNodeGlows.Count)
                {
                    var glow = boss ? WithAlpha(UIFactory.Danger, isCurrent ? 0.85f : 0.4f)
                        : isCurrent ? WithAlpha(UIFactory.Accent, 0.95f)
                        : done ? WithAlpha(UIFactory.Accent, 0.28f) : WithAlpha(UIFactory.Accent, 0f);
                    UIFactory.FadeGlow(waveNodeGlows[i], glow, 0.3f);
                }

                // Only the node the player is on moves.
                if (isCurrent) Juice.Punch(node, 0.35f, 0.4f);
            }

            for (var i = 0; i < waveLinks.Count; i++)
            {
                waveLinks[i].color = i < current ? UIFactory.Accent : WithAlpha(UIFactory.InkDim, 0.45f);
            }

            if (waveCount != null && current >= 0 && current < waveNodes.Count)
            {
                waveCount.rectTransform.anchoredPosition =
                    new Vector2(waveNodes[current].anchoredPosition.x, -42f);
            }
        }

        private void BuildBossPanel()
        {
            var boss = UIFactory.Panel9(hudRoot, "BossPanel", Palette.Vidro(Palette.Magenta, 0.1f, 0.94f));
            bossPanel = boss.rectTransform;
            // Along the bottom edge, below the boss itself; the top belongs
            // to the wave track and the panels.
            // Portrait: above the upgrades panel, clear of both bottom corners.
            if (UIFactory.Portrait)
                UIFactory.Place(bossPanel, new Vector2(0f, 0f), new Vector2(24f, 32f + UpgradesPanelHeight + 24f), new Vector2(580f, 92f));
            else
                UIFactory.Place(bossPanel, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(580f, 92f));
            UIFactory.DropShadow(boss, 26f, 10f, 0.55f);
            UIFactory.GlowShadow(boss, WithAlpha(UIFactory.Danger, 0.35f), 26f, 0.08f);
            HudBrackets(boss.rectTransform, UIFactory.Danger);

            bossLabel = UIFactory.Label(bossPanel, "BossName", "", UIFactory.TextCaption, UIFactory.Danger,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(bossLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f),
                new Vector2(540f, 36f));
            bossLabel.characterSpacing = 6f;
            UIFactory.AddUnderlay(bossLabel);

            Image bossBack;
            bossFill = UIFactory.Bar(bossPanel, "BossBar", UIFactory.Danger,
                Palette.Vidro(Palette.Magenta, 0.14f, 0.9f), out bossBack);
            UIFactory.Place((RectTransform)bossFill.rectTransform.parent, new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(540f, 30f));

            bossPanel.gameObject.SetActive(false);
        }

        private void BuildBanner()
        {
            bannerRoot = UIFactory.NewRect(hudRoot, "WaveBanner");
            UIFactory.Place(bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(130f, 260f)),
                new Vector2(UIFactory.Pick(1400f, 1040f), 140f));
            bannerGroup = UIFactory.Group(bannerRoot);
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;

            var rule = UIFactory.Rect(bannerRoot, "Rule", Palette.Alfa(Palette.Ciano, 0.55f), ArtLibrary.Pixel);
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(620f, 2f));
            UIFactory.GlowShadow(rule, WithAlpha(UIFactory.Accent, 0.55f), 14f, 0.1f);

            bannerLabel = UIFactory.Label(bannerRoot, "BannerText", "", 84, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(bannerLabel.rectTransform);
            bannerLabel.characterSpacing = 12f;
            // "NÍVEL 10 CONCLUÍDO" is wider than a portrait screen at full size.
            bannerLabel.textWrappingMode = TextWrappingModes.NoWrap;
            bannerLabel.enableAutoSizing = true;
            bannerLabel.fontSizeMin = 44f;
            bannerLabel.fontSizeMax = 84f;
            UIFactory.AddUnderlay(bannerLabel, 0.3f, 0.2f);
        }

        /// <summary>
        /// The ship's level in this run, right under the hull bar: thinner and
        /// blue, so the two bars read as one block without being confused.
        /// </summary>
        private void BuildXpBar()
        {
            Image back;
            xpFill = UIFactory.Bar(hullPanel, "XpBar", UIFactory.Accent,
                new Color(0.04f, 0.07f, 0.13f, 0.95f), out back);
            UIFactory.GlowShadow(back, WithAlpha(UIFactory.Accent, 0.22f), 14f, 0.03f);
            UIFactory.Place((RectTransform)xpFill.rectTransform.parent, new Vector2(0f, 0f),
                new Vector2(26f, 20f), new Vector2(548f, 36f));
            xpFill.fillAmount = 0f;

            xpLabel = UIFactory.Label(hullPanel, "XpLabel", "", UIFactory.TextCaption, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(xpLabel.rectTransform, new Vector2(0f, 0f), new Vector2(26f, 20f),
                new Vector2(548f, 36f));
            xpLabel.characterSpacing = 3f;
            xpLabel.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.AddUnderlay(xpLabel);
        }

        private void BuildToast()
        {
            toastRoot = UIFactory.NewRect(hudRoot, "Toast");
            // Portrait: above the ability button and the boss bar.
            UIFactory.Place(toastRoot, new Vector2(0.5f, 0f), new Vector2(0f, UIFactory.Pick(160f, 422f)),
                new Vector2(UIFactory.Pick(1100f, 1000f), 88f));
            toastGroup = UIFactory.Group(toastRoot);
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;

            var back = UIFactory.Panel9(toastRoot, "Back", new Color(0.07f, 0.06f, 0.03f, 0.92f));
            UIFactory.Stretch(back.rectTransform);
            UIFactory.GlowShadow(back, WithAlpha(UIFactory.Evolved, 0.25f), 22f, 0.05f);

            toastLabel = UIFactory.Label(toastRoot, "Label", "", 34, UIFactory.Evolved,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(toastLabel.rectTransform, 12f);
            UIFactory.AddUnderlay(toastLabel);

            toastRoot.gameObject.SetActive(false);
        }

        private void BuildUpgradeDetails(Transform parent)
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            Image backdrop;
            upgradeDetailsScreen = UIFactory.Page(parent, "UpgradeDetails",
                new Color(0.01f, 0.015f, 0.03f, 0.9f), out backdrop);
            upgradeDetailsGroup = UIFactory.Group(upgradeDetailsScreen);

            // Tapping anywhere outside the card closes it, like any mobile sheet.
            detailsDismissButton = backdrop.gameObject.AddComponent<Button>();
            detailsDismissButton.targetGraphic = backdrop;
            detailsDismissButton.transition = Selectable.Transition.None;

            var heading = UIFactory.Label(upgradeDetailsScreen, "Heading", "", 40, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f),
                new Vector2(900f, 56f));
            heading.characterSpacing = 8f;

            upgradeDetailsCard = CardWidget.Create(upgradeDetailsScreen, "UpgradeCard", new Vector2(340f, 320f));
            UIFactory.Place(upgradeDetailsCard.root, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(340f, 320f));
            upgradeDetailsCard.SetRestScale(1.45f);

            detailsCloseButton = UIFactory.RoundButton(upgradeDetailsScreen, "Close", ArtLibrary.IconCancel,
                UIFactory.PanelSoft, UIFactory.Touch, Sound.Back);
            UIFactory.Place(detailsCloseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -330f), new Vector2(UIFactory.Touch, UIFactory.Touch));

            upgradeDetailsScreen.gameObject.SetActive(false);
        }

        private void CycleGameSpeed()
        {
            GameSpeed.Set(GameSpeed.Multiplier % GameSpeed.Max + 1);
            run.ApplyGameSpeed();
            RefreshSpeedButton(true);
        }

        /// <summary>
        /// Shows the current speed. Above 1x the button is tinted (blue at 2x,
        /// gold at 3x) so a sped-up game is never mistaken for a normal one.
        /// </summary>
        private void RefreshSpeedButton(bool punch)
        {
            if (speedButton == null) return;
            var speed = GameSpeed.Multiplier;
            var accent = speed == 1 ? UIFactory.Ink : speed == 2 ? UIFactory.Accent : UIFactory.Evolved;
            speedButton.GetComponent<Image>().color = speed == 1 ? UIFactory.Panel : SelectedPanelColor(accent, 0.22f);
            UIFactory.FadeGlow(speedGlow, speed == 1 ? WithAlpha(UIFactory.Accent, 0.1f) : WithAlpha(accent, 0.55f), 0.25f);
            var label = speedButton.transform.Find("Caption")?.GetComponent<TMP_Text>();
            if (label == null) return;
            label.text = speed + "×";
            label.color = accent;
            // ButtonFeedback animates the button itself, so only pulse its label.
            if (punch) Juice.Punch(label.transform, 0.18f, 0.25f);
        }

        private void RefreshHudLanguage()
        {
            SetLabel(upgradeDetailsScreen, "Heading", L("MELHORIA", "UPGRADE", "MEJORA"));
            if (bossLabel != null)
                bossLabel.text = GameLocalization.Content("Núcleo Aberrante").ToUpperInvariant();
            lastPiles = null;
            lastWaveIndex = -1;
            lastAlive = -1;
            lastShield = -1;
            lastHpFraction = -1f;
            lastXp = -1;
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        private void HandleWaveStarted(WaveDefinition wave)
        {
            if (JuiceDirector.Instance != null)
            {
                JuiceDirector.Instance.ClearNumbers();
            }

            var isBossWave = wave.isBossWave;
            ShowBanner(isBossWave ? GameLocalization.Content("Núcleo Aberrante").ToUpperInvariant()
                : L("ONDA ", "WAVE ", "OLEADA ") + run.waveIndex,
                isBossWave ? UIFactory.Danger : UIFactory.Ink);

            if (isBossWave)
            {
                Sfx.Play(Sound.BossIncoming);
                Juice.Shake(0.35f, 0.7f);
                Juice.ScreenFlash(Palette.Alfa(Palette.Magenta, 0.3f), 0.6f);
            }
            else
            {
                Sfx.Play(Sound.WaveStart);
            }
        }

        /// <summary>
        /// The reward beat. It fires just before the upgrade screen pauses the
        /// game, so the banner uses unscaled time like everything else here.
        /// </summary>
        private void HandleWaveCleared(int waveIndex)
        {
            // The last wave of a stage announces the stage itself, not the wave.
            var lastWave = !run.isEndless && waveIndex >= RunController.WavesPerRun;
            ShowBanner(lastWave
                ? StageCatalog.Name(run.stageIndex) + L(" CONCLUÍDO", " CLEARED", " COMPLETADO")
                : L("ONDA ", "WAVE ", "OLEADA ") + waveIndex + L(" LIMPA", " CLEARED", " SUPERADA"),
                UIFactory.Good);
            Juice.ScreenFlash(Palette.Alfa(Palette.Acido, 0.16f), 0.45f);
            if (run.isEndless || waveIndex < RunController.WavesPerRun)
            {
                Sfx.Play(Sound.WaveCleared);
            }
        }

        private void ShowBanner(string text, Color color)
        {
            bannerLabel.text = text;
            bannerLabel.color = color;

            LeanTween.cancel(bannerRoot.gameObject);
            LeanTween.cancel(bannerGroup.gameObject);

            bannerRoot.localScale = new Vector3(1.35f, 1.35f, 1f);
            bannerGroup.alpha = 0f;

            LeanTween.scale(bannerRoot, Vector3.one, 0.45f)
                .setEase(LeanTweenType.easeOutCubic)
                .setIgnoreTimeScale(true);

            LeanTween.alphaCanvas(bannerGroup, 1f, 0.2f)
                .setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    if (bannerGroup == null) return;
                    LeanTween.alphaCanvas(bannerGroup, 0f, 0.45f).setDelay(0.9f).setIgnoreTimeScale(true);
                });
        }

        private void HandleUpgradeAcquired(CardDefinition definition)
        {
            // Names identify the effect consistently across all three levels.
            var level = run.cards.UpgradeLevel(definition.id);
            Toast(GameLocalization.Content(definition.NameFor(level)) +
                L("  ·  ATIVA", "  ·  ACTIVE", "  ·  ACTIVA"));
        }

        private void Toast(string message)
        {
            toastLabel.text = message;
            toastRoot.gameObject.SetActive(true);
            toastTimer = 2.2f;

            // DealIn cancels every tween on this object, so the fade has to be
            // started after it - the group and the rect are the same GameObject.
            Juice.DealIn(toastRoot, new Vector2(0f, -40f), 0f, 0.3f);
            toastGroup.alpha = 0f;
            LeanTween.alphaCanvas(toastGroup, 1f, 0.18f).setIgnoreTimeScale(true);
        }

        private void TickToast()
        {
            if (toastTimer <= 0f) return;

            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer > 0f) return;

            Juice.Fade(toastGroup, 0f, 0.3f, 0f, () =>
            {
                if (toastRoot != null && toastTimer <= 0f)
                {
                    toastRoot.gameObject.SetActive(false);
                }
            });
        }

        /// <summary>
        /// The ship is created per run, after this UI exists, so damage feedback
        /// has to be wired up whenever a new hull shows up.
        /// </summary>
        private void TrackShip()
        {
            if (run.ship == trackedShip) return;

            DetachShip();
            trackedShip = run.ship;
            if (trackedShip != null)
            {
                trackedShip.onDamaged += HandleShipDamaged;
                feedbackHp = trackedShip.hp;
                lastHealFeedback = -1f;
                lastHpFraction = -1f;
                lastShield = -1;
                lastXp = -1;
                lastLevel = -1;
            }
        }

        private void DetachShip()
        {
            if (trackedShip != null)
            {
                trackedShip.onDamaged -= HandleShipDamaged;
                trackedShip = null;
            }
        }

        private void HandleShipDamaged(float amount)
        {
            if (trackedShip == null) return;

            var previousHp = feedbackHp;
            feedbackHp = trackedShip.hp;
            if (previousHp < 0f) return;

            if (amount <= 0f)
            {
                // Regeneration reports fractional heals too. Pulse only when
                // the displayed HP increases, and never move the sibling XP bar.
                if (Mathf.CeilToInt(feedbackHp) <= Mathf.CeilToInt(previousHp) ||
                    Time.unscaledTime - lastHealFeedback < HealFeedbackInterval)
                {
                    return;
                }
                lastHealFeedback = Time.unscaledTime;
                Sfx.Play(Sound.Heal);
                Juice.Punch(hpLabel.transform, 0.05f, 0.3f);
                return;
            }

            if (feedbackHp >= previousHp) return;
            Sfx.Play(Sound.ShipHit);
            Juice.Punch(hpLabel.transform, 0.09f, 0.35f);
            // A red rim instead of a full-screen wash: the fight stays readable.
            var hitShare = trackedShip.maxHp > 0f ? amount / trackedShip.maxHp : 0.1f;
            Juice.DamageVignette(0.55f + Mathf.Clamp01(hitShare * 4f) * 0.45f);
            Juice.ScreenFlash(Palette.Alfa(Palette.Magenta, 0.1f), 0.25f);
            Juice.Shake(0.28f, 0.28f);
            Juice.HitStop(0.06f);
        }

        // ------------------------------------------------------------------
        // Per-frame refresh
        // ------------------------------------------------------------------

        private void RefreshHud()
        {
            if (hudRoot == null || !hudRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            if (run.waveIndex != lastWaveIndex)
            {
                lastWaveIndex = run.waveIndex;
                RefreshWaveTrack();
                waveNote.text = StageCatalog.Name(run.stageIndex);
                lastAlive = -1;
            }

            // Rebuilt only when the count moves: this line used to allocate a
            // new string every frame.
            var remaining = run.enemiesRemaining;
            if (remaining != lastAlive)
            {
                lastAlive = remaining;
                if (waveCount != null)
                {
                    waveCount.text = lastAlive + (lastAlive == 1
                        ? L(" INIMIGO", " ENEMY", " ENEMIGO")
                        : L(" INIMIGOS", " ENEMIES", " ENEMIGOS"));
                }
            }

            RefreshEndlessScore();
            RefreshHull();
            RefreshXp();
            RefreshPiles();
            RefreshBoss();
        }

        // Endless only: the run's points under the wave track. Runtime-only
        // (not baked), created the first time an endless run shows the HUD.
        private TMP_Text endlessScoreLabel;
        private int lastScoreShown = -1;

        private void RefreshEndlessScore()
        {
            if (!run.isEndless)
            {
                if (endlessScoreLabel != null && endlessScoreLabel.gameObject.activeSelf)
                    endlessScoreLabel.gameObject.SetActive(false);
                return;
            }
            if (endlessScoreLabel == null)
            {
                endlessScoreLabel = UIFactory.Label(hudRoot, "EndlessScore", "", 34, UIFactory.Evolved,
                    TextAlignmentOptions.Center, true);
                UIFactory.Place(endlessScoreLabel.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(0f, UIFactory.Pick(-28f, -204f) - 112f), new Vector2(520f, 44f));
                endlessScoreLabel.characterSpacing = 3f;
                endlessScoreLabel.textWrappingMode = TextWrappingModes.NoWrap;
                endlessScoreLabel.raycastTarget = false;
                UIFactory.AddUnderlay(endlessScoreLabel);
                lastScoreShown = -1;
            }
            if (!endlessScoreLabel.gameObject.activeSelf) endlessScoreLabel.gameObject.SetActive(true);
            // Rebuilt only when the score moves.
            if (run.score == lastScoreShown) return;
            lastScoreShown = run.score;
            endlessScoreLabel.text = L("PONTOS ", "SCORE ", "PUNTOS ") + EndlessScore.Format(run.score);
        }

        private void RefreshHull()
        {
            var ship = run.ship;
            if (ship == null)
            {
                hpFill.fillAmount = 0f;
                hpGhost.fillAmount = 0f;
                hpLabel.text = "";
                lastHpFraction = -1f;
                return;
            }

            var fraction = ship.hpFraction;
            if (!Mathf.Approximately(fraction, lastHpFraction))
            {
                var initializing = lastHpFraction < 0f;
                var lost = fraction < lastHpFraction;
                lastHpFraction = fraction;

                if (initializing)
                {
                    LeanTween.cancel(hpFill.gameObject, false, TweenAction.CALLBACK);
                    hpFill.fillAmount = fraction;
                }
                else
                {
                    Juice.FillTo(hpFill, fraction, 0.16f);
                }

                // The ghost trails behind on damage and snaps forward on a heal,
                // so the red sliver always means "just lost".
                LeanTween.cancel(hpGhost.gameObject);
                var ghost = hpGhost;
                if (initializing)
                {
                    ghost.fillAmount = fraction;
                }
                else
                {
                    LeanTween.value(ghost.gameObject, value =>
                    {
                        if (ghost != null) ghost.fillAmount = value;
                    }, ghost.fillAmount, fraction, lost ? 0.45f : 0.16f)
                        .setDelay(lost ? 0.35f : 0f)
                        .setEase(LeanTweenType.easeOutQuad)
                        .setIgnoreTimeScale(true);
                }

                // Estado (vida crítica) e evento (dano) não falam com a mesma voz: o vermelho
                // único é da vida crítica; o flash de dano é Magenta.
                var hpColor = fraction > 0.35f ? UIFactory.Good : UIFactory.Critical;
                hpFill.color = hpColor;
                UIFactory.FadeGlow(hpGlow, WithAlpha(hpColor, fraction > 0.35f ? 0.28f : 0.55f), 0.3f);
                lastShield = -1;
            }

            // The shield count used to sit on the ship-name line; that line
            // is gone, so it rides on the hull bar.
            if (ship.shieldCharges != lastShield)
            {
                lastShield = ship.shieldCharges;
                hpLabel.text = Mathf.CeilToInt(ship.hp) + " / " + Mathf.CeilToInt(ship.maxHp) +
                    (lastShield > 0 ? L("   ·   ESCUDO ", "   ·   SHIELD ", "   ·   ESCUDO ") + lastShield : "");
            }
        }

        private void RefreshXp()
        {
            if (xpFill == null || (run.xp == lastXp && run.level == lastLevel))
            {
                return;
            }

            var initializing = lastXp < 0 || lastLevel < 0;
            var leveled = !initializing && run.level > lastLevel;
            lastXp = run.xp;
            lastLevel = run.level;

            if (initializing)
            {
                LeanTween.cancel(xpFill.gameObject, false, TweenAction.CALLBACK);
                xpFill.fillAmount = run.xpFraction;
            }
            else
            {
                Juice.FillTo(xpFill, run.xpFraction, 0.15f);
            }
            builder.Length = 0;
            // "Nave", because the stages are called Nível 1/2/3 too.
            builder.Append(L("NAVE NV ", "SHIP LV ", "NAVE NV ")).Append(run.level).Append("   ·   ");
            if (run.isMaxLevel) builder.Append(L("MÁXIMO", "MAX", "MÁXIMO"));
            else builder.Append(run.xp).Append(" / ").Append(run.xpToNext).Append(" XP");
            xpLabel.text = builder.ToString();
            if (leveled) Juice.Punch(xpLabel.transform, 0.18f, 0.3f);
        }

        private void RefreshPiles()
        {
            builder.Length = 0;
            builder.Append(L("MELHORIAS  ·  ", "UPGRADES  ·  ", "MEJORAS  ·  ")).Append(run.cards.acquiredCount);
            var text = builder.ToString();
            if (text != lastPiles)
            {
                lastPiles = text;
                pilesLabel.text = text;
                Juice.Punch(pilesLabel.transform, 0.14f, 0.28f);
            }

            // The ability badge takes the ship's colour, so a new ship rebuilds the
            // icons even when the number of upgrades happens to be the same.
            var iconShip = run.ship != null ? run.ship.definition : null;
            if (lastUpgradeIconCount != run.cards.acquiredCount || lastUpgradeIconShip != iconShip)
            {
                lastUpgradeIconCount = run.cards.acquiredCount;
                lastUpgradeIconShip = iconShip;
                RefreshUpgradeIcons();
            }
            LayoutUpgradesPanel();
        }

        private void RefreshUpgradeIcons()
        {
            for (var i = upgradeIcons.childCount - 1; i >= 0; i--)
            {
                upgradeIcons.GetChild(i).gameObject.SetActive(false);
                Destroy(upgradeIcons.GetChild(i).gameObject);
            }

            upgradeHasSpecial = BuildSpecialDetailsIcon(0);
            var slot = upgradeHasSpecial ? 1 : 0;
            var sortedCards = new List<CardDefinition>(config.cards);
            sortedCards.Sort(CompareCardDisplayOrder);
            for (var i = 0; i < sortedCards.Count; i++)
            {
                var definition = sortedCards[i];
                var level = run.cards.UpgradeLevel(definition.id);
                if (level <= 0) continue;

                var color = CardWidget.UpgradeColor(level);
                var holder = UIFactory.NewRect(upgradeIcons, definition.id);
                UIFactory.Place(holder, new Vector2(0f, 1f),
                    UpgradeIconPosition(slot),
                    new Vector2(IconSize, IconSize));

                var glow = UIFactory.Rect(holder, "Glow", new Color(color.r, color.g, color.b, 0.3f),
                    ArtLibrary.CircleGlow);
                UIFactory.Stretch(glow.rectTransform, -8f);

                // A hexagonal plate and outline: the same shape the cards use.
                var plate = UIFactory.Rect(holder, "Plate", new Color(0.02f, 0.03f, 0.06f, 0.85f),
                    ArtLibrary.ArtOr("hex_disc", ArtLibrary.CircleFilled));
                UIFactory.Stretch(plate.rectTransform, 2f);
                plate.raycastTarget = false;

                var ring = UIFactory.Rect(holder, "LevelRing", color,
                    ArtLibrary.ArtOr("wire_hex", ArtLibrary.CircleOutline));
                UIFactory.Stretch(ring.rectTransform);
                ring.raycastTarget = true;

                var icon = UIFactory.Rect(holder, "Icon", color, CardIconFactory.Get(definition.effect));
                icon.preserveAspect = true;
                UIFactory.Stretch(icon.rectTransform, 15f);
                CardIconFactory.CenterArtwork(icon, definition.effect, Vector2.zero);

                var levelText = UIFactory.Label(holder, "Level", level.ToString(), 28, color,
                    TextAlignmentOptions.BottomRight, true);
                UIFactory.Place(levelText.rectTransform, new Vector2(1f, 0f), new Vector2(2f, -2f),
                    new Vector2(26f, 28f));
                UIFactory.AddUnderlay(levelText);

                var selectedDefinition = definition;
                var selectedLevel = level;
                var button = holder.gameObject.AddComponent<Button>();
                button.targetGraphic = ring;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ShowUpgradeDetails(selectedDefinition, selectedLevel));
                holder.gameObject.AddComponent<ButtonFeedback>().Initialize(holder);

                // The newest icon is the one the player just picked; it lands last.
                Juice.PopIn(holder, 1f, 0.3f, slot * 0.03f, true);
                slot++;
            }

            if (slot == 0)
            {
                var empty = UIFactory.Label(upgradeIcons, "Empty",
                    L("Nenhuma ainda", "None yet", "Ninguna aún"), UIFactory.TextCaption,
                    UIFactory.InkDim, TextAlignmentOptions.TopLeft);
                UIFactory.Stretch(empty.rectTransform);
            }
            upgradeIconSlots = slot;
            upgradeLayoutSize = Vector2.zero;
            LayoutUpgradesPanel();
        }

        private void RefreshBoss()
        {
            var boss = world.boss;
            if (boss == null)
            {
                if (bossPanel.gameObject.activeSelf) bossPanel.gameObject.SetActive(false);
                return;
            }

            if (!bossPanel.gameObject.activeSelf)
            {
                bossPanel.gameObject.SetActive(true);
                bossLabel.text = GameLocalization.Content(boss.definition.name).ToUpperInvariant();
                lastBossFraction = -1f;
                Juice.DealIn(bossPanel, new Vector2(0f, -90f), 0f, 0.45f);
                Juice.BreatheUI(bossLabel, 0.75f, 1f, 0.9f);
            }

            // Only retween on an actual change: FillTo cancels and restarts, so
            // calling it every frame would keep the bar pinned at its old value.
            // Both halves together once the core splits.
            var fraction = world.BossHealthFraction;
            if (!Mathf.Approximately(fraction, lastBossFraction))
            {
                lastBossFraction = fraction;
                Juice.FillTo(bossFill, fraction, 0.12f);
            }
        }

        // ------------------------------------------------------------------
        // Upgrade details
        // ------------------------------------------------------------------

        private void ShowUpgradeDetails(CardDefinition definition, int level)
        {
            SetLabel(upgradeDetailsScreen, "Heading", L("MELHORIA", "UPGRADE", "MEJORA"));
            var card = new RuntimeCard(-1, definition, run.cards);
            upgradeDetailsCard.Bind(card, 0, L("NÍVEL ", "LEVEL ", "NIVEL ") + level, level);
            upgradeDetailsCard.interactable = false;
            ShowDetailsOverlay();
        }

        private void ShowDetailsOverlay()
        {
            // The sheet must sit above the upgrade-selection page, not under its HUD sibling.
            upgradeDetailsScreen.SetParent(safeRoot, false);
            UIFactory.Stretch(upgradeDetailsScreen);
            upgradeDetailsCard.ResizeDetails(new Vector2(420f, 440f));
            var height = safeRoot.rect.height > 0f ? safeRoot.rect.height : UIFactory.ReferenceHeight;
            var width = safeRoot.rect.width > 0f ? safeRoot.rect.width : UIFactory.ReferenceWidth;
            var scale = Mathf.Min(2f, Mathf.Min((height - 260f) / 440f, (width - 96f) / 420f));
            upgradeDetailsCard.SetRestScale(Mathf.Max(0.1f, scale));
            var half = 220f * upgradeDetailsCard.RestScale;
            UIFactory.Place(upgradeDetailsScreen.Find("Heading") as RectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, half + 90f), new Vector2(width - 80f, 56f));
            UIFactory.Place(detailsCloseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -half - 70f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            detailsPausedGame = Time.timeScale > 0f;
            if (detailsPausedGame)
            {
                timeScaleBeforeDetails = Time.timeScale;
                Time.timeScale = 0f;
            }

            upgradeDetailsScreen.gameObject.SetActive(true);
            upgradeDetailsScreen.SetAsLastSibling();
            FadeScreenIn(upgradeDetailsGroup, 0.2f);
            Juice.PopIn(upgradeDetailsCard.root, upgradeDetailsCard.RestScale, 0.32f, 0.05f, true);
        }

        private void HideUpgradeDetails()
        {
            if (upgradeDetailsScreen == null || !upgradeDetailsScreen.gameObject.activeSelf)
            {
                return;
            }

            HideScreen(upgradeDetailsScreen);
            if (detailsPausedGame)
            {
                Time.timeScale = timeScaleBeforeDetails;
                detailsPausedGame = false;
            }
        }
    }
}
