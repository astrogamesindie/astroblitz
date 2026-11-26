// AstroCrawler - the main menu, stage select and settings.
//
// The main menu is one decision deep: the ship you will fly is shown large in
// the middle (tap it to change), JOGAR is the biggest thing on screen, and the
// three sections sit in a row of icon tiles at thumb height. Settings is a gear
// in the corner, credits a chip in the other. Built at runtime in Bind.

using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private enum MenuSection { Play, Ships, Stages, Upgrades, Settings, Languages, BaseDeck, Modes, Ranking }

        private RectTransform mainMenu;
        private RectTransform levelsPage;
        private RectTransform settingsPage;
        private Image menuHero;
        private Image menuHeroGlow;
        private Image menuHeroRing;
        private Image menuHeroPad;
        private Image menuHeroPadGlow;
        private TMP_Text menuTagline;
        private TMP_Text menuShipName;
        private TMP_Text menuStageLine;
        private Image menuModeIcon;
        private TMP_Text menuCredits;
        private Button playButton;
        private readonly Button[] menuTiles = new Button[3];

        // The ten level tiles live in a grid (StageGrid), the chosen one's
        // details and JOGAR in a panel at the foot of the page.
        private TMP_Text stageProgressLabel;
        private RectTransform stageContent;
        private RectTransform stageDetail;
        private Button stagePlay;

        private readonly List<TrueShadow> stageGlows = new List<TrueShadow>();

        private Button musicToggle;
        private Button effectsToggle;
        private Slider volumeSlider;
        private readonly Button[] languageButtons = new Button[GameLocalization.LanguageCount];
        private readonly Button[] orientationButtons = new Button[2];

        // Eight rows: music, effects, volume, language, screen orientation,
        // account, support and release notes. Landscape rows (and the controls
        // in them) are shorter so the panel keeps the height it had with six
        // rows and still fits a 1080-high screen.
        private const int SettingsRows = 8;
        private static float SettingsRowStep { get { return UIFactory.Pick(112f, 150f); } }
        private static float SettingsRowHeight { get { return UIFactory.Pick(96f, 128f); } }
        private static float SettingsControlHeight { get { return UIFactory.Pick(84f, 96f); } }
        private static float SettingsPanelHeight
        {
            get { return 36f + (SettingsRows - 1) * SettingsRowStep + SettingsRowHeight + 36f; }
        }
        private Button accountButton;
        private float accountSignOutArmedUntil;
        private static float SettingsRowWidth { get { return UIFactory.Pick(980f, 940f); } }

        // ------------------------------------------------------------------
        // Main menu
        // ------------------------------------------------------------------

        // Retrato (revisão de 23/11/2026): o bloco de ações fica preso à base
        // da área segura (que já desconta o banner), na zona do polegar, de
        // baixo para cima: atalhos, JOGAR, trilho de fases, botão de modo e a
        // legenda "MODO DE JOGO". A nave ocupa a zona livre entre o logo e esse
        // bloco. Distâncias medidas da base da área segura, em unidades de canvas.
#if UNITY_EDITOR
        // No Editor, os botões de teste (GameUI.Debug) ocupam a faixa de baixo.
        private const float MenuBase = 120f;
#else
        private const float MenuBase = 36f;
#endif
        private static readonly Vector2 MenuTileSizePortrait = new Vector2(260f, 184f);
        private static readonly Vector2 MenuPlaySizePortrait = new Vector2(760f, 176f);
        private static readonly Vector2 MenuModeSizePortrait = new Vector2(600f, 100f);
        private const float MenuPlayBottom = MenuBase + 184f + 40f;
        private const float MenuModeBottom = MenuPlayBottom + 176f + 64f;
        private const float MenuHeroBottom = MenuModeBottom + 100f + 74f;
        private const float MenuTitleTopPortrait = 250f;
        private const float MenuTitleSizePortrait = 88f;
        private const float MenuHeroTopPortrait = MenuTitleTopPortrait + MenuTitleSizePortrait + 52f;

        // Paisagem: a nave numa coluna à esquerda e as ações numa à direita
        // (empilhadas no centro, nome, legenda e trilho se sobrepunham).
        private const float MenuHeroXLandscape = -460f;
        private const float MenuActionsXLandscape = 420f;

        private Image menuLevelChip;
        private TMP_Text menuLevelLabel;
        private RectTransform menuStageTrack;
        private readonly List<Image> menuStageSegments = new List<Image>();
        private readonly Image[] menuTileDots = new Image[3];

        private void BuildMainMenu()
        {
            if (mainMenu != null) return;
            var actionAccent = UIFactory.ActionBorder;
            var actionFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var portrait = UIFactory.Portrait;

            // Translucent: the starfield drifting behind is the one thing on
            // this screen that moves on its own.
            Image backdrop;
            mainMenu = UIFactory.Page(safeRoot, "MainMenu", new Color(0.015f, 0.025f, 0.055f, 0.6f), out backdrop);
            UIFactory.EdgeFade(mainMenu, "BottomFade", false, 460f, 0.85f);

            // Portrait: the logo is smaller than it was (128) and sits between
            // the settings gear (left) and the column of notices (right); for a
            // returning player the space is worth more to the ship than to the name.
            var titleTop = UIFactory.Pick(-50f, -MenuTitleTopPortrait);
            var titleSize = UIFactory.Pick(100f, MenuTitleSizePortrait);
            var title = UIFactory.Label(mainMenu, "Title", "ASTRO BLITZ", (int)titleSize, UIFactory.Ink,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, titleTop),
                new Vector2(UIFactory.Pick(1300f, 680f), titleSize + 24f));
            title.characterSpacing = UIFactory.Pick(14f, 8f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 60f;
            title.fontSizeMax = titleSize;
            UIFactory.GlowShadow(title, WithAlpha(UIFactory.Accent, 0.5f), 30f, 0.05f);
            BindDebugLogo(title);

            var flare = UIFactory.Rect(mainMenu, "TitleFlare", WithAlpha(UIFactory.Accent, 0.8f), ArtLibrary.LensFlare);
            UIFactory.Place(flare.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, titleTop - titleSize + 8f),
                new Vector2(UIFactory.Pick(1100f, 760f), UIFactory.Pick(90f, 70f)));
            flare.rectTransform.SetSiblingIndex(title.rectTransform.GetSiblingIndex());
            flare.raycastTarget = false;

            menuTagline = UIFactory.Label(mainMenu, "Tagline", "", 28, UIFactory.InkDim,
                TextAlignmentOptions.Top, true);
            UIFactory.Place(menuTagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, titleTop - titleSize - 30f),
                new Vector2(1000f, 34f));
            menuTagline.characterSpacing = UIFactory.Pick(12f, 8f);

            var settings = UIFactory.RoundButton(mainMenu, "Settings", ArtLibrary.IconSettings,
                UIFactory.PanelSoft, UIFactory.Touch, Sound.Navigate);
            UIFactory.Place(settings.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -32f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            UIFactory.GlowShadow(settings.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.22f), 22f, 0.05f);
            settings.transform.Find("Ring").GetComponent<Image>().color = WithAlpha(UIFactory.Accent, 0.7f);
            Rebind(settings, () => OpenMenuSection(MenuSection.Settings));

            BuildMenuCredits();
            BuildHero();
            BuildModeButton();

            // JOGAR é o único botão âmbar do menu (PILARES_DE_UI §3) e a maior
            // peça da tela: mais largo que o botão de modo, miolo âmbar mais
            // forte, brilho próprio e um reflexo que passa a cada 4 s. Um só
            // sinal de ação (o ▶); a seta › saiu.
            var playAccent = Palette.Ambar;
            var playSize = UIFactory.Pick(new Vector2(640f, UIFactory.TouchLarge), MenuPlaySizePortrait);
            playButton = UIFactory.IconButton(mainMenu, "Play", "JOGAR", ArtLibrary.IconPlay, playAccent,
                playSize, UIFactory.Pick(48, 60), Sound.Play);
            var playRect = playButton.GetComponent<RectTransform>();
            if (portrait)
                UIFactory.Place(playRect, new Vector2(0.5f, 0f), new Vector2(0f, MenuPlayBottom), playSize);
            else
                UIFactory.Place(playRect, new Vector2(0.5f, 0.5f), new Vector2(MenuActionsXLandscape, 10f), playSize);
            var playImage = playButton.GetComponent<Image>();
            StyleCutAction(playButton, actionFrame, playAccent, Palette.Vidro(Palette.Ambar, 0.4f), false);
            UIFactory.GlowShadow(playImage, WithAlpha(playAccent, 0.45f), 34f, 0.06f);
            var playIcon = playRect.Find("Icon") as RectTransform;
            if (playIcon != null) playIcon.anchoredPosition = new Vector2(playSize.y * 0.36f, 0f);
            // The word centred on the whole button, clear of the icon on the left.
            var playCaption = playRect.Find("Caption") as RectTransform;
            if (playCaption != null)
            {
                var inset = playSize.y * 0.36f + 64f + 16f;
                playCaption.offsetMin = new Vector2(inset, playCaption.offsetMin.y);
                playCaption.offsetMax = new Vector2(-inset, playCaption.offsetMax.y);
                var playLabel = playCaption.GetComponent<TMP_Text>();
                if (playLabel != null) playLabel.characterSpacing = 10f;
            }
            UIFactory.AddShine(playRect, 4f, 0.22f);
            Rebind(playButton, () => OpenMenuSection(MenuSection.Play));

            // Destinations along the bottom: ships, the shared base deck and
            // upgrades, each with a one-word label under its icon and a magenta
            // dot when there is something the credits can already buy there.
            var tileSize = UIFactory.Pick(new Vector2(200f, 150f), MenuTileSizePortrait);
            var tileStep = tileSize.x + UIFactory.Pick(20f, 30f);
            var sections = new[] { MenuSection.Ships, MenuSection.BaseDeck, MenuSection.Upgrades };
            var icons = new[] { ArtLibrary.Ship, ArtLibrary.IconDeck, ArtLibrary.IconUpgrades };
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                var tile = UIFactory.Tile(mainMenu, "Tile_" + section, icons[i], "", UIFactory.PanelSoft,
                    tileSize, UIFactory.Accent);
                var tileRect = tile.GetComponent<RectTransform>();
                var x = (i - (sections.Length - 1) * 0.5f) * tileStep;
                if (portrait)
                    UIFactory.Place(tileRect, new Vector2(0.5f, 0f), new Vector2(x, MenuBase), tileSize);
                else
                    UIFactory.Place(tileRect, new Vector2(0.5f, 0.5f), new Vector2(MenuActionsXLandscape + x, -190f), tileSize);
                var glyph = tileRect.Find("Icon") as RectTransform;
                if (glyph != null)
                    UIFactory.Place(glyph, new Vector2(0.5f, 1f), new Vector2(0f, -22f),
                        Vector2.one * UIFactory.Pick(72f, 84f));
                var caption = tileRect.Find("Caption") as RectTransform;
                if (caption != null)
                {
                    caption.offsetMin = new Vector2(12f, 18f);
                    caption.offsetMax = new Vector2(-12f, -tileSize.y * 0.58f);
                    var label = caption.GetComponent<TMP_Text>();
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSizeMax = UIFactory.TextCaption;
                    label.fontSizeMin = 24f;
                    label.characterSpacing = 3f;
                    label.color = UIFactory.Ink;
                }
                Rebind(tile, () => OpenMenuSection(section));
                menuTiles[i] = tile;
                StyleCutAction(tile, actionFrame, WithAlpha(actionAccent, 0.75f), UIFactory.ActionFill, false, KitRole.Icone);

                var dot = UIFactory.Rect(tileRect, "Dot", UIFactory.Danger, ArtLibrary.CircleFilled);
                UIFactory.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(30f, 30f));
                dot.raycastTarget = false;
                UIFactory.GlowShadow(dot, WithAlpha(UIFactory.Danger, 0.7f), 14f, 0.05f);
                dot.gameObject.SetActive(false);
                menuTileDots[i] = dot;
            }

            mainMenu.gameObject.SetActive(false);
        }

        /// <summary>
        /// The ship you are about to fly, large, with its name, its level and
        /// a swap badge on the scanner ring (the whole hero opens the hangar).
        /// Portrait: centred in the free zone between the logo and the actions.
        /// </summary>
        private void BuildHero()
        {
            var portrait = UIFactory.Portrait;
            // Portrait centres the group in the zone between the logo and the
            // actions; landscape moves it into the left column.
            var zone = new GameObject("HeroZone", typeof(RectTransform)).GetComponent<RectTransform>();
            zone.SetParent(mainMenu, false);
            UIFactory.Stretch(zone);
            if (portrait)
            {
                zone.offsetMin = new Vector2(0f, MenuHeroBottom);
                zone.offsetMax = new Vector2(0f, -MenuHeroTopPortrait);
            }
            else
            {
                zone.offsetMin = new Vector2(MenuHeroXLandscape, 0f);
                zone.offsetMax = new Vector2(MenuHeroXLandscape, 0f);
            }

            var shipSize = UIFactory.Pick(220f, 300f);
            var shipY = UIFactory.Pick(40f, 64f);
            // The ring is wide enough that the pad never crosses it.
            var ringRadius = shipSize * 0.8f;
            var nameY = shipY - ringRadius - 40f;

            // A round glow that fades on every side, kept faint so the ship's
            // colour does not turn into a muddy disc.
            menuHeroGlow = UIFactory.Rect(zone, "HeroGlow", WithAlpha(UIFactory.Accent, 0.07f), ArtLibrary.CircleGlow);
            UIFactory.Place(menuHeroGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, shipY),
                Vector2.one * ringRadius * 2.3f);
            menuHeroGlow.raycastTarget = false;

            // A dashed ring turning slowly behind the hull: the hangar's scanner.
            menuHeroRing = UIFactory.Rect(zone, "HeroRing", WithAlpha(UIFactory.Accent, 0.3f),
                ArtLibrary.CircleDash != null ? ArtLibrary.CircleDash : ArtLibrary.CircleOutline);
            menuHeroRing.preserveAspect = true;
            menuHeroRing.raycastTarget = false;
            UIFactory.Place(menuHeroRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, shipY),
                Vector2.one * ringRadius * 2f);
            menuHeroRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            LeanTween.rotateAroundLocal(menuHeroRing.gameObject, Vector3.forward, -360f, 40f)
                .setRepeat(-1).setIgnoreTimeScale(true);

            // The pad the ship floats over: a lit ellipse and a pool of light,
            // narrow enough to stay inside the ring. Static: the ship's float
            // and the ring's turn are motion enough.
            var padY = shipY - shipSize * 0.5f + 10f;
            var padSize = new Vector2(shipSize * 1.13f, shipSize * 0.29f);
            menuHeroPadGlow = UIFactory.Rect(zone, "HeroPadGlow", WithAlpha(UIFactory.Accent, 0.18f), ArtLibrary.CircleGlow);
            UIFactory.Place(menuHeroPadGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, padY),
                padSize * 1.35f);
            menuHeroPadGlow.raycastTarget = false;
            menuHeroPad = UIFactory.Rect(zone, "HeroPad", WithAlpha(UIFactory.Accent, 0.6f), ArtLibrary.CircleOutline);
            UIFactory.Place(menuHeroPad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, padY), padSize);
            menuHeroPad.raycastTarget = false;
            UIFactory.GlowShadow(menuHeroPad, WithAlpha(UIFactory.Accent, 0.5f), 16f, 0.05f);

            menuHero = UIFactory.Rect(zone, "Hero", UIFactory.Accent, ArtLibrary.Ship);
            menuHero.preserveAspect = true;
            menuHero.raycastTarget = false;
            UIFactory.Place(menuHero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, shipY),
                new Vector2(shipSize, shipSize));
            UIFactory.GlowShadow(menuHero, WithAlpha(Color.white, 0.3f), 24f, 0.05f);
            var hero = menuHero.rectTransform;
            LeanTween.value(menuHero.gameObject, t =>
            {
                if (hero != null) hero.anchoredPosition = new Vector2(0f, shipY + t);
            }, -8f, 8f, 1.8f).setEase(LeanTweenType.easeInOutSine).setLoopPingPong().setIgnoreTimeScale(true);

            // The swap badge sits on the ring, down and to the right: "this
            // ship can be changed". The hero tap below takes the touch.
            var swapSize = UIFactory.Pick(72f, 96f);
            var swapAngle = UIFactory.Pick(-0.2f, -0.35f);
            var swapX = ringRadius * Mathf.Sqrt(1f - swapAngle * swapAngle);
            var swapGlass = ArtLibrary.Ui("Kit/sf_redondo_vidro");
            var swapLine = ArtLibrary.Ui("Kit/sf_redondo_linha");
            var swap = UIFactory.Rect(zone, "HeroSwap", UIFactory.PanelSoft, swapGlass != null ? swapGlass : ArtLibrary.CircleFilled);
            UIFactory.Place(swap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(swapX, shipY + ringRadius * swapAngle),
                Vector2.one * swapSize);
            swap.raycastTarget = false;
            var swapRing = UIFactory.Rect(swap.rectTransform, "Ring", WithAlpha(UIFactory.Accent, 0.8f),
                swapLine != null ? swapLine : ArtLibrary.CircleOutline);
            UIFactory.Stretch(swapRing.rectTransform);
            swapRing.raycastTarget = false;
            var swapIcon = UIFactory.Rect(swap.rectTransform, "Icon", UIFactory.Accent, ArtLibrary.IconSwap);
            swapIcon.preserveAspect = true;
            swapIcon.raycastTarget = false;
            UIFactory.Place(swapIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * swapSize * 0.5f);

            // The hero and its name are one big tap target that opens the hangar.
            var heroTap = UIFactory.Rect(zone, "HeroTap", Color.clear);
            heroTap.raycastTarget = true;
            var heroButton = heroTap.gameObject.AddComponent<Button>();
            heroButton.targetGraphic = heroTap;
            heroButton.transition = Selectable.Transition.None;
            UIFactory.SetClickSound(heroButton, Sound.Navigate);
            Rebind(heroButton, () => OpenMenuSection(MenuSection.Ships));

            menuShipName = UIFactory.Label(zone, "ShipName", "", 50, UIFactory.Accent,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(menuShipName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, nameY),
                new Vector2(UIFactory.Pick(760f, 820f), 62f));
            menuShipName.characterSpacing = 6f;
            menuShipName.textWrappingMode = TextWrappingModes.NoWrap;
            menuShipName.enableAutoSizing = true;
            menuShipName.fontSizeMin = 36f;
            menuShipName.fontSizeMax = 50f;
            UIFactory.AddUnderlay(menuShipName);

            // The ship's level (the one the hangar cards show), as a chip:
            // under the name.
            var chipSize = new Vector2(220f, 52f);
            menuLevelChip = UIFactory.Pill(zone, "ShipLevel", "", Palette.Vidro(Palette.Ciano, 0.12f, 0.9f),
                UIFactory.Ink, chipSize.y, UIFactory.TextCaption);
            UIFactory.Place(menuLevelChip.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, nameY - 66f), chipSize);
            menuLevelLabel = menuLevelChip.transform.Find("Label").GetComponent<TMP_Text>();
            menuLevelLabel.enableAutoSizing = true;
            menuLevelLabel.fontSizeMin = 24f;
            menuLevelLabel.fontSizeMax = UIFactory.TextCaption;

            // The tap covers ship, ring, name and level.
            var tapTop = shipY + ringRadius;
            var tapBottom = nameY - 96f;
            UIFactory.Place(heroTap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, (tapTop + tapBottom) * 0.5f),
                new Vector2(UIFactory.Pick(640f, 720f), tapTop - tapBottom));
        }

        /// <summary>
        /// The game-mode button: which mode (campaign or endless) and its
        /// level or record, with the campaign's ten stages as a track under
        /// it; it opens the modes page.
        /// </summary>
        private void BuildModeButton()
        {
            var size = UIFactory.Pick(new Vector2(640f, 96f), MenuModeSizePortrait);
            var stageButton = UIFactory.Button(mainMenu, "StageSelect", null, UIFactory.PanelDeep, UIFactory.Ink,
                UIFactory.TextBody, Sound.Navigate);
            var stageRect = stageButton.GetComponent<RectTransform>();
            if (UIFactory.Portrait)
                UIFactory.Place(stageRect, new Vector2(0.5f, 0f), new Vector2(0f, MenuModeBottom), size);
            else
                UIFactory.Place(stageRect, new Vector2(0.5f, 0.5f), new Vector2(MenuActionsXLandscape, 190f), size);
            StyleCutAction(stageButton, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"),
                WithAlpha(UIFactory.Accent, 0.8f), UIFactory.ActionFill, false);
            Rebind(stageButton, () => OpenMenuSection(MenuSection.Modes));

            menuModeIcon = UIFactory.Rect(stageRect, "Icon", UIFactory.Accent, MenuCampaignIcon);
            menuModeIcon.preserveAspect = true;
            menuModeIcon.raycastTarget = false;
            UIFactory.Place(menuModeIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(54f, 54f));

            // "GAME MODE" over the button, so it reads as a choice and not a caption.
            var modeCaption = UIFactory.Label(stageRect, "ModeCaption", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.Bottom, true);
            UIFactory.Place(modeCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 48f), new Vector2(640f, 38f));
            modeCaption.characterSpacing = 6f;
            modeCaption.raycastTarget = false;
            var chevron = UIFactory.Rect(stageRect, "Chevron", UIFactory.InkDim, ArtLibrary.IconNext);
            chevron.preserveAspect = true;
            chevron.raycastTarget = false;
            UIFactory.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(40f, 40f));

            menuStageLine = UIFactory.Label(stageRect, "StageLine", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(menuStageLine.rectTransform);
            menuStageLine.rectTransform.offsetMin = new Vector2(96f, 0f);
            menuStageLine.rectTransform.offsetMax = new Vector2(-80f, 0f);
            menuStageLine.characterSpacing = 3f;
            menuStageLine.textWrappingMode = TextWrappingModes.NoWrap;
            menuStageLine.enableAutoSizing = true;
            menuStageLine.fontSizeMin = 26f;
            menuStageLine.fontSizeMax = 40f;

            // Campaign track: one segment per stage, lit once this ship has
            // beaten it, the selected stage outlined in amber. Hidden in endless.
            menuStageTrack = new GameObject("StageTrack", typeof(RectTransform)).GetComponent<RectTransform>();
            menuStageTrack.SetParent(stageRect, false);
            var trackWidth = size.x - 60f;
            UIFactory.Place(menuStageTrack, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(trackWidth, 10f));
            menuStageSegments.Clear();
            const float gap = 6f;
            var count = StageCatalog.Count;
            var segment = (trackWidth - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var piece = UIFactory.Rect(menuStageTrack, "Stage" + i, WithAlpha(UIFactory.Ink, 0.16f), ArtLibrary.Pixel);
                piece.raycastTarget = false;
                UIFactory.Place(piece.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * (segment + gap), 0f),
                    new Vector2(segment, 8f));
                menuStageSegments.Add(piece);
            }
        }

        /// <summary>A compass for the campaign: the old "Campaign" glyph read as a second ▶ above JOGAR.</summary>
        private static Sprite MenuCampaignIcon
        {
            get
            {
                var compass = ArtLibrary.Art("PictoIcon_Compass_1");
                return compass != null ? compass : ArtLibrary.IconStages;
            }
        }

        /// <summary>Credits, top centre; tapping them opens the upgrade tree they buy.</summary>
        private void BuildMenuCredits()
        {
            var chip = UIFactory.Panel9(mainMenu, "Credits", UIFactory.PanelDeep);
            var rect = chip.rectTransform;
            // Landscape: the logo owns the top centre, so the chip sits left of the gift button.
            UIFactory.Place(rect, UIFactory.Pick(new Vector2(1f, 1f), new Vector2(0.5f, 1f)),
                UIFactory.Pick(new Vector2(-32f - UIFactory.Touch - 24f, -32f), new Vector2(0f, -32f)),
                new Vector2(300f, UIFactory.Chip));
            chip.raycastTarget = true;
            HudFrame(chip, UIFactory.Evolved);

            var icon = UIFactory.Rect(rect, "Icon", UIFactory.Evolved, ArtLibrary.IconCredits);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(56f, 56f));

            // The number right next to the jewel (it sat at the far end, with a gap in the middle).
            menuCredits = UIFactory.Label(rect, "Value", "0", 46, UIFactory.Evolved, TextAlignmentOptions.Left, true);
            UIFactory.Place(menuCredits.rectTransform, new Vector2(0f, 0.5f), new Vector2(92f, 0f),
                new Vector2(186f, 64f));
            menuCredits.textWrappingMode = TextWrappingModes.NoWrap;
            menuCredits.enableAutoSizing = true;
            menuCredits.fontSizeMin = 30f;
            menuCredits.fontSizeMax = 46f;

            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.transition = Selectable.Transition.None;
            Rebind(button, () => OpenMenuSection(MenuSection.Upgrades));
            chip.gameObject.AddComponent<ButtonFeedback>().clickSound = Sound.Navigate;
        }

        private void RefreshMainMenu()
        {
            if (mainMenu == null) return;

            var ship = config.GetShip(selectedShipId);
            if (ship != null)
            {
                var accent = SpriteFactory.ParseColor(ship.tint);
                menuShipName.text = GameLocalization.Content(ship.name).ToUpperInvariant();
                menuShipName.color = accent;
                menuHero.sprite = ArtLibrary.ShipFor(ship.id);
                menuHero.color = ArtLibrary.ShipTint(ship.id, accent);
                menuHeroGlow.color = WithAlpha(accent, 0.07f);
                menuHeroRing.color = WithAlpha(accent, 0.3f);
                menuHeroPad.color = WithAlpha(accent, 0.6f);
                menuHeroPadGlow.color = WithAlpha(accent, 0.18f);
                if (menuLevelLabel != null) menuLevelLabel.text = ShipLevelLine(ship.id, accent);
                if (menuLevelChip != null) menuLevelChip.color = Palette.Vidro(accent, 0.14f, 0.9f);
            }
            menuTagline.text = "";

            menuStageLine.text = ModeLine();
            if (menuModeIcon != null)
            {
                menuModeIcon.sprite = run.isEndless ? IconEndless : MenuCampaignIcon;
                menuModeIcon.color = run.isEndless ? EndlessAccent : UIFactory.Accent;
            }
            RefreshMenuStageTrack();
            // While coins are flying in, the counter is theirs to tick up.
            if (!CoinBurst.Holds(menuCredits)) menuCredits.text = run.passiveUpgrades.credits.ToString();
            RefreshMenuTileDots();
            LayoutMenuCorners();

            RefreshSelectedShip();
            RefreshStageSelection();
        }

        /// <summary>Campaign track under the mode button: beaten stages lit, the selected one in amber.</summary>
        private void RefreshMenuStageTrack()
        {
            if (menuStageTrack == null) return;
            menuStageTrack.gameObject.SetActive(!run.isEndless);
            if (run.isEndless) return;
            for (var i = 0; i < menuStageSegments.Count; i++)
            {
                var cleared = StageProgress.IsCleared(selectedShipId, i);
                var color = i == run.stageIndex ? Palette.Ambar
                    : cleared ? UIFactory.Accent
                    : WithAlpha(UIFactory.Ink, 0.16f);
                menuStageSegments[i].color = color;
            }
        }

        /// <summary>
        /// The magenta dot on a destination: the credits already pay for the
        /// next level of the selected ship (Upgrades) or the next base card
        /// (Base deck). Ships has no dot: a ship is never that cheap.
        /// </summary>
        private void RefreshMenuTileDots()
        {
            if (run == null || run.passiveUpgrades == null) return;
            var credits = run.passiveUpgrades.credits;
            var upgrades = run.passiveUpgrades.AvailableLevels(selectedShipId) > 0 &&
                           credits >= run.passiveUpgrades.Cost(selectedShipId, 1);
            var deck = run.cardUnlocks != null && run.cardUnlocks.LockedBaseCount() > 0 &&
                       credits >= run.cardUnlocks.NextCost;
            SetMenuDot(0, false);
            SetMenuDot(1, deck);
            SetMenuDot(2, upgrades);
        }

        private void SetMenuDot(int index, bool on)
        {
            if (menuTileDots[index] != null) menuTileDots[index].gameObject.SetActive(on);
        }

        /// <summary>
        /// The right column holds the notices (PILARES_DE_UI §5: the top corners
        /// are things that open by themselves): gift, then the timed Starter Pack
        /// offer while it runs, then Premium until it is owned. Stacked without
        /// gaps; the offer gets room for its ribbon above and timer below.
        /// </summary>
        private void LayoutMenuCorners()
        {
            if (mainMenu == null) return;
            var y = -32f;
            foreach (var name in new[] { "Rewards", "StarterPack", "Premium" })
            {
                var corner = mainMenu.Find(name) as RectTransform;
                if (corner == null || !corner.gameObject.activeSelf) continue;
                var offer = name == "StarterPack";
                if (offer && y < -32f) y -= 20f;
                corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(1f, 1f);
                corner.anchoredPosition = new Vector2(-32f, y);
                y -= UIFactory.Touch + (offer ? 84f : 24f);
            }
        }

        // ------------------------------------------------------------------
        // Navigation
        // ------------------------------------------------------------------

        private void OpenMenuSection(MenuSection section)
        {
            if (section == MenuSection.Play)
            {
                StartSelectedShip();
                return;
            }

            if (section == MenuSection.Languages) BuildLanguagesPage();
            HideMenuPages();
            HideScreen(mainMenu, SyncAdNavigation);
            SyncAdNavigation();

            switch (section)
            {
                case MenuSection.Ships:
                case MenuSection.Upgrades:
                    var upgrades = section == MenuSection.Upgrades;
                    shipScreen.gameObject.SetActive(true);
                    // Upgrades always shows the ship equipped in the hangar.
                    if (upgrades) passiveShipId = selectedShipId;
                    SetShipScreenMode(upgrades);
                    if (upgrades) RefreshPassiveScreen();
                    else RefreshSelectedShip();
                    FadeScreenIn(shipGroup, 0.2f);
                    break;
                case MenuSection.Stages:
                    RefreshStageSelection();
                    levelsPage.gameObject.SetActive(true);
                    FadeScreenIn(UIFactory.Group(levelsPage), 0.2f);
                    break;
                case MenuSection.Languages:
                    RefreshLanguageChoices();
                    languagesPage.gameObject.SetActive(true);
                    FadeScreenIn(UIFactory.Group(languagesPage), 0.2f);
                    break;
                case MenuSection.Settings:
                    RefreshSettings();
                    settingsPage.gameObject.SetActive(true);
                    FadeScreenIn(UIFactory.Group(settingsPage), 0.2f);
                    break;
                case MenuSection.BaseDeck:
                    OpenBaseDeckPage();
                    break;
                case MenuSection.Modes:
                    RefreshModesPage();
                    modesPage.gameObject.SetActive(true);
                    FadeScreenIn(UIFactory.Group(modesPage), 0.2f);
                    break;
                case MenuSection.Ranking:
                    rankingPage.gameObject.SetActive(true);
                    OpenRankingPage();
                    FadeScreenIn(UIFactory.Group(rankingPage), 0.2f);
                    break;
            }
            SyncAdNavigation();
        }

        /// <summary>The open page fades out (UIMotion) while the next one fades in over it.</summary>
        private void HideMenuPages()
        {
            HideScreen(shipScreen, SyncAdNavigation);
            HideScreen(deckScreen, SyncAdNavigation);
            HideScreen(levelsPage, SyncAdNavigation);
            HideScreen(settingsPage, SyncAdNavigation);
            HideScreen(languagesPage, SyncAdNavigation);
            HideScreen(baseDeckPage, SyncAdNavigation);
            HideScreen(modesPage, SyncAdNavigation);
            HideScreen(rankingPage, SyncAdNavigation);
        }

        private void GoToMainMenu()
        {
            if (SoundDirector.Instance != null) SoundDirector.Instance.SaveSettings();
            HideMenuPages();
            mainMenu.gameObject.SetActive(true);
            SyncAdNavigation();
            RefreshMainMenu();
            FadeScreenIn(UIFactory.Group(mainMenu), 0.2f);
            StaggerMainMenu();
        }

        /// <summary>The menu's actions land one after another as it appears (UIMotion).</summary>
        private void StaggerMainMenu()
        {
            if (mainMenu == null) return;
            var items = new List<Transform>(8);
            if (playButton != null) items.Add(playButton.transform);
            for (var i = 0; i < menuTiles.Length; i++)
                if (menuTiles[i] != null) items.Add(menuTiles[i].transform);
            foreach (var name in new[] { "Settings", "Rewards", "StarterPack", "Premium" })
            {
                var corner = mainMenu.Find(name);
                if (corner != null) items.Add(corner);
            }
            UIMotion.Stagger(items, 0.08f, 0.05f);
        }

        /// <summary>Title and round back button shared by every menu page.</summary>
        private RectTransform BuildMenuPage(string name, out TMP_Text title)
        {
            Image backdrop;
            var page = UIFactory.Page(safeRoot, name, new Color(0.02f, 0.03f, 0.06f, 0.97f), out backdrop);

            var back = UIFactory.RoundButton(page, "Back", ArtLibrary.IconBack, UIFactory.PanelSoft,
                UIFactory.Touch, Sound.Back);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -32f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(back, GoToMainMenu);
            UIFactory.GlowShadow(back.GetComponent<Image>(), WithAlpha(UIFactory.Accent, 0.22f), 22f, 0.05f);
            back.transform.Find("Ring").GetComponent<Image>().color = WithAlpha(UIFactory.Accent, 0.7f);

            var flare = UIFactory.Rect(page, "TitleFlare", WithAlpha(UIFactory.Accent, 0.6f), ArtLibrary.LensFlare);
            UIFactory.Place(flare.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -102f),
                new Vector2(UIFactory.Pick(900f, 760f), 40f));
            flare.raycastTarget = false;

            title =UIFactory.Label(page, "Title", "", UIFactory.TextTitle, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f),
                new Vector2(UIFactory.Pick(1200f, 720f), 80f));
            title.characterSpacing = 12f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 40f;
            title.fontSizeMax = UIFactory.TextTitle;
            UIFactory.AddUnderlay(title, 0.3f, 0.15f);
            return page;
        }

        /// <summary>
        /// Moves a menu page's title (and its flare) from the top of the page
        /// to just above content centred on the page; <paramref name="contentTop"/>
        /// is that content's top edge, measured from the page centre.
        /// </summary>
        private static void PlaceTitleAbove(RectTransform page, float contentTop)
        {
            var title = page.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f);
                title.pivot = new Vector2(0.5f, 0f);
                title.anchoredPosition = new Vector2(0f, contentTop + 60f);
            }
            var flare = page.Find("TitleFlare") as RectTransform;
            if (flare != null)
            {
                flare.anchorMin = flare.anchorMax = new Vector2(0.5f, 0.5f);
                flare.pivot = new Vector2(0.5f, 0.5f);
                flare.anchoredPosition = new Vector2(0f, contentTop + 52f);
            }
        }

        // ------------------------------------------------------------------
        // Stages
        // ------------------------------------------------------------------

        /// <summary>
        /// The campaign's ten levels at once: a grid of numbered tiles (state at
        /// a glance - cleared, chosen, open, locked) and, at the foot of the page
        /// in reach of the thumb, the chosen level's details and JOGAR. It used
        /// to be a carousel showing one level at a time.
        /// </summary>
        private void BuildLevelsPage()
        {
            if (levelsPage != null) return;

            TMP_Text title;
            levelsPage = BuildMenuPage("LevelsPage", out title);
            // Campaign levels only (the endless mode has its own card on the modes page);
            // opened from there, so Back returns there.
            Rebind(levelsPage.Find("Back").GetComponent<Button>(), () => OpenMenuSection(MenuSection.Modes));

            // "3 / 10 levels cleared", under the page title.
            stageProgressLabel = UIFactory.Label(levelsPage, "Progress", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(stageProgressLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f),
                new Vector2(UIFactory.Pick(1200f, 1020f), 40f));
            stageProgressLabel.characterSpacing = UIFactory.Pick(4f, 2f);
            stageProgressLabel.textWrappingMode = TextWrappingModes.NoWrap;
            stageProgressLabel.enableAutoSizing = true;
            stageProgressLabel.fontSizeMin = 26f;
            stageProgressLabel.fontSizeMax = UIFactory.TextBody;

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var margin = UIFactory.Pick(64f, 48f);
            BuildStageDetail(cutFrame, margin);

            // The grid fills what is left between the progress line and the
            // detail panel: every tile gets an equal share, whatever the screen.
            stageContent = UIFactory.NewRect(levelsPage, "StageGrid");
            UIFactory.Stretch(stageContent);
            stageContent.offsetMin = new Vector2(margin - StageTileGap * 0.5f,
                StageDetailBottom + StageDetailHeight + UIFactory.Pick(28f, 36f));
            stageContent.offsetMax = new Vector2(-margin + StageTileGap * 0.5f, -UIFactory.Pick(180f, 190f));

            var columns = UIFactory.Pick(5, 2);
            var rows = (StageCatalog.Count + columns - 1) / columns;
            for (var index = 0; index < StageCatalog.Count; index++)
            {
                var selectedIndex = index;
                var column = index % columns;
                var row = index / columns;

                var tile = UIFactory.Panel9(stageContent, "Stage_" + index, UIFactory.PanelSoft);
                var rect = tile.rectTransform;
                rect.anchorMin = new Vector2((float)column / columns, 1f - (float)(row + 1) / rows);
                rect.anchorMax = new Vector2((float)(column + 1) / columns, 1f - (float)row / rows);
                rect.offsetMin = new Vector2(StageTileGap * 0.5f, StageTileGap * 0.5f);
                rect.offsetMax = new Vector2(-StageTileGap * 0.5f, -StageTileGap * 0.5f);
                stageGlows.Add(UIFactory.GlowShadow(tile, WithAlpha(UIFactory.Accent, 0f), 36f, 0.1f));

                tile.raycastTarget = true;
                var tileButton = tile.gameObject.AddComponent<Button>();
                tileButton.targetGraphic = tile;
                tileButton.transition = Selectable.Transition.None;
                if (!StyleCutAction(tileButton, cutFrame, UIFactory.Accent, Palette.Vidro(Palette.Ciano, 0.06f), false,
                        KitRole.Card))
                {
                    tile.pixelsPerUnitMultiplier = 0.25f;
                    var inset = rect.Find("Inset").GetComponent<Image>();
                    inset.pixelsPerUnitMultiplier = 0.25f;
                }
                UIFactory.SetClickSound(tileButton, Sound.None);
                Rebind(tileButton, () => SelectStage(selectedIndex));

                // The level's number is the tile: large, centred.
                var number = UIFactory.Label(rect, "Number", (index + 1).ToString(), UIFactory.Pick(64, 84),
                    UIFactory.Ink, TextAlignmentOptions.Center, true);
                UIFactory.Stretch(number.rectTransform, 10f);
                number.enableAutoSizing = true;
                number.fontSizeMin = 40f;
                number.fontSizeMax = UIFactory.Pick(64f, 84f);
                number.raycastTarget = false;
                UIFactory.AddUnderlay(number);

                // State in the corner: a check once cleared, a padlock while locked.
                var state = UIFactory.Rect(rect, "State", UIFactory.Good, ArtLibrary.IconCheck);
                state.preserveAspect = true;
                state.raycastTarget = false;
                UIFactory.Place(state.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(44f, 44f));
                state.gameObject.SetActive(false);
            }

            levelsPage.gameObject.SetActive(false);
        }

        private const float StageTileGap = 22f;
        // Portrait 460 (was 560): the ships row sits just above JOGAR, so a level
        // nobody has beaten leaves no tall empty band.
        private static float StageDetailHeight => UIFactory.Pick(300f, 460f);
        private static float StageDetailBottom => UIFactory.Pick(32f, 40f);

        /// <summary>The chosen level: name, waves, description, the ships that beat it and JOGAR.</summary>
        private void BuildStageDetail(Sprite cutFrame, float margin)
        {
            var panel = UIFactory.Panel9(levelsPage, "StageDetail", UIFactory.Accent, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(margin, StageDetailBottom);
            rect.offsetMax = new Vector2(-margin, StageDetailBottom + StageDetailHeight);
            var fill = UIFactory.Panel9(rect, "Inset", Palette.Vidro(Palette.Ciano, 0.06f), cutFrame);
            fill.pixelsPerUnitMultiplier = 0.2f;
            UIFactory.Stretch(fill.rectTransform, 4f);
            UIFactory.DropShadow(panel, 30f, 10f, 0.55f);
            stageDetail = rect;

            var landscape = !UIFactory.Portrait;
            var playWidth = 520f;

            var name = UIFactory.Label(rect, "Name", "", 54, UIFactory.Ink, TextAlignmentOptions.TopLeft, true);
            Band(name.rectTransform, 44f, 320f, -30f, 64f);
            name.characterSpacing = 5f;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 38f;
            name.fontSizeMax = 54f;
            UIFactory.AddUnderlay(name);

            var waves = UIFactory.Label(rect, "Waves", "", UIFactory.TextBody, UIFactory.Accent,
                TextAlignmentOptions.TopRight, true);
            UIFactory.Place(waves.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, -40f), new Vector2(300f, 44f));
            waves.characterSpacing = 4f;
            waves.textWrappingMode = TextWrappingModes.NoWrap;
            waves.enableAutoSizing = true;
            waves.fontSizeMin = UIFactory.TextCaption;
            waves.fontSizeMax = UIFactory.TextBody;

            var description = UIFactory.Label(rect, "Description", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.TopLeft);
            Band(description.rectTransform, 44f, landscape ? playWidth + 80f : 44f, -108f, landscape ? 84f : 96f);
            description.enableAutoSizing = true;
            description.fontSizeMin = UIFactory.TextCaption;
            description.fontSizeMax = UIFactory.TextBody;
            description.lineSpacing = 4f;

            // The ships that beat this level (each ship has its own progress).
            BuildStageShips(rect);
            var ships = rect.Find("Ships") as RectTransform;
            if (ships != null)
            {
                if (landscape)
                {
                    ships.anchorMin = ships.anchorMax = new Vector2(0f, 0f);
                    ships.pivot = new Vector2(0f, 0f);
                    ships.anchoredPosition = new Vector2(44f, 28f);
                }
                else
                {
                    ships.anchoredPosition = new Vector2(0f, 212f);
                }
            }

            // The screen's one amber button: play the chosen level now.
            var play = UIFactory.Button(rect, "Play", "", Palette.Vidro(Palette.Ambar, 0.4f), UIFactory.Ink,
                UIFactory.TextButton, Sound.Play);
            var playRect = play.GetComponent<RectTransform>();
            if (landscape)
            {
                UIFactory.Place(playRect, new Vector2(1f, 0f), new Vector2(-36f, 32f), new Vector2(playWidth, 116f));
            }
            else
            {
                playRect.anchorMin = new Vector2(0f, 0f);
                playRect.anchorMax = new Vector2(1f, 0f);
                playRect.pivot = new Vector2(0.5f, 0f);
                playRect.offsetMin = new Vector2(36f, 32f);
                playRect.offsetMax = new Vector2(-36f, 32f + 124f);
            }
            StyleCutAction(play, cutFrame, Palette.Ambar, Palette.Vidro(Palette.Ambar, 0.4f), false);
            UIFactory.GlowShadow(play.GetComponent<Image>(), WithAlpha(Palette.Ambar, 0.4f), 30f, 0.06f);
            var caption = playRect.Find("Caption");
            if (caption != null) caption.GetComponent<TMP_Text>().characterSpacing = 10f;
            UIFactory.AddShine(playRect, 4f, 0.22f);
            stagePlay = play;
            Rebind(play, PlayCampaignMode);
        }

        private static string EndlessDescription() => L("Ciclos sem fim: 4 ondas e um chefe. XP 25% menor. Nível máximo 12.",
            "Endless cycles: 4 waves and a boss. 25% less XP. Maximum level 12.",
            "Ciclos sin fin: 4 oleadas y un jefe. 25% menos de XP. Nivel máximo 12.");
        private static string EndlessUnlockDescription() => L("Conclua o Nível 3 com qualquer nave para liberar.",
            "Clear Level 3 with any ship to unlock.", "Completa el Nivel 3 con cualquier nave para desbloquearlo.");

        private void SelectStage(int index)
        {
            if (!StageProgress.IsUnlocked(selectedShipId, index))
            {
                // Locked for this ship: say so instead of selecting.
                Sfx.Play(Sound.Denied);
                var locked = StageCard(index);
                if (locked != null) Juice.Punch(locked, 0.04f, 0.25f);
                return;
            }

            var changed = run.isEndless || run.stageIndex != index;
            Sfx.Play(changed ? Sound.Select : Sound.Tap);
            run.SelectStage(index, selectedShipId);
            RefreshStageSelection();
            if (changed && index < stageGlows.Count)
            {
                var accent = UIFactory.Accent;
                UIFactory.FlashGlow(stageGlows[index], WithAlpha(accent, 1f), WithAlpha(accent, 0.55f), 0.5f);
                if (stageDetail != null) Juice.Punch(stageDetail, 0.03f, 0.25f);
            }
        }

        private const float ShipSlot = 84f;
        private const float ShipGap = 14f;

        private static readonly Color Silhouette = new Color(0.24f, 0.29f, 0.4f, 1f);

        /// <summary>
        /// One slot per ship on a stage card: the ship in its colour once it
        /// has beaten the stage, a dark silhouette while it still has to.
        /// </summary>
        private void BuildStageShips(RectTransform card)
        {
            var count = config.ships.Count;
            var row = UIFactory.NewRect(card, "Ships");
            UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0f, 250f),
                new Vector2(count * ShipSlot + (count - 1) * ShipGap, ShipSlot));

            for (var j = 0; j < count; j++)
            {
                var tint = SpriteFactory.ParseColor(config.ships[j].tint);
                var slot = UIFactory.NewRect(row, "Ship_" + j);
                UIFactory.Place(slot, new Vector2(0f, 0.5f), new Vector2(j * (ShipSlot + ShipGap), 0f),
                    new Vector2(ShipSlot, ShipSlot));

                var plate = UIFactory.Rect(slot, "Plate", new Color(0.02f, 0.03f, 0.06f, 0.9f), ArtLibrary.CircleFilled);
                UIFactory.Stretch(plate.rectTransform);
                plate.raycastTarget = false;

                var hull = UIFactory.Rect(slot, "Hull", Silhouette, ArtLibrary.Ship);
                hull.preserveAspect = true;
                hull.raycastTarget = false;
                UIFactory.Stretch(hull.rectTransform, 16f);
                UIFactory.GlowShadow(hull, WithAlpha(tint, 0f), 16f, 0.05f);

                var outline = UIFactory.Rect(slot, "Outline", WithAlpha(tint, 0.5f), ArtLibrary.ShipOutline != null
                    ? ArtLibrary.ShipOutline : ArtLibrary.Ship);
                outline.preserveAspect = true;
                outline.raycastTarget = false;
                UIFactory.Stretch(outline.rectTransform, 16f);
                outline.enabled = ArtLibrary.ShipOutline != null;

                // The ship currently selected on the hangar screen.
                var ring = UIFactory.Rect(slot, "Ring", WithAlpha(tint, 0.9f), ArtLibrary.CircleOutline);
                ring.raycastTarget = false;
                UIFactory.Stretch(ring.rectTransform);
                ring.gameObject.SetActive(false);

                var check = UIFactory.Rect(slot, "Check", UIFactory.Good, ArtLibrary.IconCheck);
                check.preserveAspect = true;
                check.raycastTarget = false;
                UIFactory.Place(check.rectTransform, new Vector2(1f, 0f), new Vector2(4f, -2f), new Vector2(32f, 32f));
                check.gameObject.SetActive(false);
            }
        }

        private void RefreshStageShips(Transform card, int stage)
        {
            // Only the ships that have beaten this stage are shown, side by
            // side and centred on the card; a stage nobody beat has an empty row.
            var row = card.Find("Ships") as RectTransform;
            var cleared = 0;
            for (var j = 0; j < config.ships.Count; j++)
                if (StageProgress.IsCleared(config.ships[j].id, stage)) cleared++;
            var rowWidth = row != null ? row.sizeDelta.x : 0f;
            var used = cleared * ShipSlot + Mathf.Max(0, cleared - 1) * ShipGap;
            var x = (rowWidth - used) * 0.5f;

            for (var j = 0; j < config.ships.Count; j++)
            {
                var slot = card.Find("Ships/Ship_" + j) as RectTransform;
                if (slot == null) continue;

                var ship = config.ships[j];
                var done = StageProgress.IsCleared(ship.id, stage);
                slot.gameObject.SetActive(done);
                if (!done) continue;

                slot.anchoredPosition = new Vector2(x, 0f);
                x += ShipSlot + ShipGap;

                var tint = SpriteFactory.ParseColor(ship.tint);
                var hull = slot.Find("Hull").GetComponent<Image>();
                hull.color = tint;
                var glow = hull.GetComponent<TrueShadow>();
                if (glow != null) glow.Color = WithAlpha(tint, 0.55f);
                slot.Find("Outline").gameObject.SetActive(false);
                slot.Find("Check").gameObject.SetActive(true);
                slot.Find("Ring").gameObject.SetActive(ship.id == selectedShipId);
            }
        }

        private Transform StageCard(int index)
        {
            return stageContent != null ? stageContent.Find("Stage_" + index) : null;
        }

        private void RefreshStageSelection()
        {
            if (levelsPage == null) return;

            // Every ship has its own progress, so the page always reads for
            // the ship picked on the hangar screen.
            var shipId = selectedShipId;
            var ship = config.GetShip(shipId);
            // In endless no campaign level shows as selected.
            var current = run.isEndless ? -1 : run.stageIndex;

            if (stageProgressLabel != null)
            {
                var shipColor = ship != null ? SpriteFactory.ParseColor(ship.tint) : UIFactory.Ink;
                var shipName = ship != null ? GameLocalization.Content(ship.name).ToUpperInvariant() : "";
                stageProgressLabel.text = "<color=" + UIFactory.ColorTag(shipColor) + ">" + shipName + "</color>   ·   " +
                    StageProgress.ClearedCount(shipId) + " / " + StageCatalog.Count +
                    L(" NÍVEIS CONCLUÍDOS", " LEVELS CLEARED", " NIVELES COMPLETADOS");
            }

            // Tiles: chosen = cyan, lit and a little larger; cleared = a green
            // check; open = cyan, quieter; locked = Fósforo, a padlock. One
            // colour family, the state is in the brightness and the icon (the
            // ten per-level colours said nothing).
            for (var i = 0; i < StageCatalog.Count && i < stageGlows.Count; i++)
            {
                var card = StageCard(i);
                if (card == null) continue;

                var unlocked = StageProgress.IsUnlocked(shipId, i);
                var selected = unlocked && i == current;
                var beaten = StageProgress.IsCleared(shipId, i);
                var frame = unlocked ? UIFactory.Accent : Palette.Fosforo;

                card.GetComponent<Image>().color = WithAlpha(frame, selected ? 1f : unlocked ? 0.55f : 0.18f);
                var inset = card.Find("Inset");
                if (inset != null) inset.GetComponent<Image>().color =
                    Palette.Vidro(frame, selected ? 0.16f : unlocked ? 0.06f : 0.03f);
                var number = card.Find("Number");
                if (number != null) number.GetComponent<TMP_Text>().color =
                    !unlocked ? WithAlpha(Palette.Fosforo, 0.35f) : selected ? UIFactory.Ink : UIFactory.InkDim;
                var state = card.Find("State");
                if (state != null)
                {
                    state.gameObject.SetActive(beaten || !unlocked);
                    var icon = state.GetComponent<Image>();
                    icon.sprite = beaten ? ArtLibrary.IconCheck : ArtLibrary.IconLock;
                    icon.color = beaten ? UIFactory.Good : WithAlpha(Palette.Fosforo, 0.5f);
                }

                LeanTween.cancel(card.gameObject, false, TweenAction.SCALE);
                if (Application.isPlaying)
                    LeanTween.scale(card.gameObject, Vector3.one * (selected ? 1.04f : 1f), 0.22f)
                        .setEase(LeanTweenType.easeOutBack).setIgnoreTimeScale(true);
                UIFactory.FadeGlow(stageGlows[i], WithAlpha(UIFactory.Accent, selected ? 0.55f : 0f), 0.25f);
            }

            RefreshStageDetail(current < 0 ? Mathf.Clamp(run.stageIndex, 0, StageCatalog.Count - 1) : current);
        }

        /// <summary>The detail panel follows the chosen level.</summary>
        private void RefreshStageDetail(int stage)
        {
            if (stageDetail == null) return;
            SetLabel(stageDetail, "Name", StageCatalog.Name(stage));
            SetLabel(stageDetail, "Waves", RunController.WavesPerRun + L(" ONDAS", " WAVES", " OLEADAS"));
            SetLabel(stageDetail, "Description", StageCatalog.Description(stage));
            if (stagePlay != null) SetButtonLabel(stagePlay, L("JOGAR", "PLAY", "JUGAR"));
            RefreshStageShips(stageDetail, stage);
        }

        // ------------------------------------------------------------------
        // Settings
        // ------------------------------------------------------------------

        private void BuildSettingsPage()
        {
            if (settingsPage != null) return;

            TMP_Text title;
            settingsPage = BuildMenuPage("SettingsPage", out title);

            // Centred, with the page title brought down to sit right above
            // it: no empty bottom half on a tall phone. (Remove ads moved to
            // the main menu.)
            var accent = UIFactory.ActionBorder;
            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(settingsPage, "Panel", accent, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            var rect = panel.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(-30f, -40f)),
                new Vector2(UIFactory.Pick(1060f, 1000f), SettingsPanelHeight));
            PlaceTitleAbove(settingsPage, rect.anchoredPosition.y + SettingsPanelHeight * 0.5f);
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            var surface = UIFactory.Panel9(rect, "Surface", UIFactory.ActionFillMuted, cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(panel, surface, KitRole.Painel);
            var topRule = UIFactory.Rect(rect, "TopRule", accent, ArtLibrary.Pixel);
            UIFactory.Place(topRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            topRule.raycastTarget = false;
            // Keep the existing page title/back navigation; use restrained accents.
            var titleFlare = settingsPage.Find("TitleFlare").GetComponent<Image>();
            titleFlare.color = WithAlpha(accent, 0.18f);

            musicToggle = SettingsRow(rect, "Music", ArtLibrary.IconMusic, 0, true);
            Rebind(musicToggle, () =>
            {
                var sound = SoundDirector.Instance;
                if (sound != null) sound.SetMusicEnabled(!sound.musicEnabled);
                RefreshSettings();
            });

            effectsToggle = SettingsRow(rect, "Effects", ArtLibrary.IconAudio, 1, true);
            Rebind(effectsToggle, () =>
            {
                var sound = SoundDirector.Instance;
                if (sound != null) sound.SetSfxEnabled(!sound.sfxEnabled);
                RefreshSettings();
            });

            var volumeRow = SettingsRow(rect, "Volume", ArtLibrary.IconAudio, 2, false);
            volumeSlider = BuildSlider(volumeRow.transform);
            volumeSlider.onValueChanged.AddListener(value =>
            {
                if (SoundDirector.Instance != null) SoundDirector.Instance.SetMasterVolume(value);
                else AudioListener.volume = value;
            });

            var languageRow = SettingsRow(rect, "Language", ArtLibrary.IconLanguage, 3, false);
            languageValueButton = UIFactory.Button(languageRow.transform, "ChooseLanguage", "", UIFactory.PanelSoft,
                UIFactory.Ink, UIFactory.TextButton, Sound.Navigate);
            UIFactory.Place(languageValueButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(440f, SettingsControlHeight));
            var languageCaption = languageValueButton.GetComponentInChildren<TMP_Text>();
            languageCaption.enableAutoSizing = true;
            languageCaption.fontSizeMin = 28f;
            languageCaption.fontSizeMax = UIFactory.TextButton;
            StyleCutAction(languageValueButton, cutFrame, accent, UIFactory.ActionFill);
            Rebind(languageValueButton, () => OpenMenuSection(MenuSection.Languages));

            // Portrait (default) or landscape. On a phone the screen turns and
            // the interface is rebuilt for it (GameUI.WatchOrientation).
            var orientationRow = SettingsRow(rect, "Orientation", ArtLibrary.IconSettings, 4, false);
            for (var i = 0; i < orientationButtons.Length; i++)
            {
                var landscape = i == 1;
                var button = UIFactory.Button(orientationRow.transform, "Orientation_" + i, "", UIFactory.PanelSoft,
                    UIFactory.Ink, UIFactory.TextBody, Sound.Toggle);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                    new Vector2(-24f - (1 - i) * 256f, 0f), new Vector2(240f, SettingsControlHeight));
                orientationButtons[i] = button;
                StyleCutAction(button, cutFrame, WithAlpha(accent, 0.45f), UIFactory.ActionFillMuted);
                // Ícone de aparelho em pé / deitado à esquerda do rótulo.
                var phone = UIFactory.Panel9(button.transform, "Phone", UIFactory.Ink, ArtLibrary.FrameOutline);
                phone.pixelsPerUnitMultiplier = 1.4f;
                phone.raycastTarget = false;
                UIFactory.Place(phone.rectTransform, new Vector2(0f, 0.5f), new Vector2(landscape ? 22f : 30f, 0f),
                    landscape ? new Vector2(52f, 32f) : new Vector2(32f, 52f));
                var orientationCaption = button.transform.Find("Caption") as RectTransform;
                if (orientationCaption != null)
                {
                    orientationCaption.offsetMin = new Vector2(landscape ? 86f : 72f, orientationCaption.offsetMin.y);
                    orientationCaption.offsetMax = new Vector2(-18f, orientationCaption.offsetMax.y);
                    var orientationLabel = orientationCaption.GetComponent<TMP_Text>();
                    orientationLabel.enableAutoSizing = true;
                    orientationLabel.fontSizeMin = 24f;
                    orientationLabel.fontSizeMax = UIFactory.TextBody;
                    orientationLabel.characterSpacing = 1f;
                }
                Rebind(button, () =>
                {
                    OrientationSetting.Set(landscape);
                    RefreshSettings();
                });
            }

            // Account: Unity Player Accounts sign-in or sign-out.
            var accountRow = SettingsRow(rect, "Account", ArtLibrary.Ui("Multiplayer"), 5, false);
            var accountLabel = accountRow.transform.Find("Label") as RectTransform;
            if (accountLabel != null) accountLabel.sizeDelta = new Vector2(400f, 100f);
            accountButton = UIFactory.Button(accountRow.transform, "AccountAction", "", UIFactory.Primary,
                UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(accountButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(UIFactory.Pick(420f, 400f), SettingsControlHeight));
            var accountCaption = accountButton.GetComponentInChildren<TMP_Text>();
            accountCaption.enableAutoSizing = true;
            accountCaption.fontSizeMin = 24f;
            accountCaption.fontSizeMax = UIFactory.TextBody;
            StyleCutAction(accountButton, cutFrame, accent, UIFactory.ActionFillStrong);
            Rebind(accountButton, OnAccountAction);

            // Support: opens the contact sheet (GameUI.ReleaseNotes). It used
            // to float in the page's top-right corner, outside the panel.
            var supportRow = SettingsRow(rect, "Support", ArtLibrary.UiOr("Gamepad", ArtLibrary.IconSettings), 6, false);
            settingsSupportButton = UIFactory.Button(supportRow.transform, "ContactSupport", "", UIFactory.PanelSoft,
                UIFactory.Ink, UIFactory.TextBody, Sound.Navigate);
            UIFactory.Place(settingsSupportButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(UIFactory.Pick(420f, 400f), SettingsControlHeight));
            var supportCaption = settingsSupportButton.GetComponentInChildren<TMP_Text>();
            supportCaption.enableAutoSizing = true;
            supportCaption.fontSizeMin = 24f;
            supportCaption.fontSizeMax = UIFactory.TextBody;
            StyleCutAction(settingsSupportButton, cutFrame, accent, UIFactory.ActionFill);
            Rebind(settingsSupportButton, OpenSupportPopup);

            // Release notes: the button shows the installed version and opens
            // the notes sheet (it used to sit in the support popup).
            var notesRow = SettingsRow(rect, "Notes", ArtLibrary.UiOr("Tutorial", ArtLibrary.IconSettings), 7, false);
            var notesLabel = notesRow.transform.Find("Label") as RectTransform;
            if (notesLabel != null) notesLabel.sizeDelta = new Vector2(400f, 60f);
            var notesButton = UIFactory.Button(notesRow.transform, "OpenReleaseNotes", "v" + Application.version,
                UIFactory.PanelSoft, UIFactory.Ink, UIFactory.TextBody, Sound.Navigate);
            UIFactory.Place(notesButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(UIFactory.Pick(420f, 400f), SettingsControlHeight));
            StyleCutAction(notesButton, cutFrame, accent, UIFactory.ActionFill);
            Rebind(notesButton, () => { if (!ReleaseNotesPopupOpen) OpenReleaseNotes(); });

            settingsPage.gameObject.SetActive(false);
        }

        private void OnAccountAction()
        {
            var account = AccountService.Instance;
            if (account == null || !account.Ready || account.Busy) return;
            if (!account.SignedIn)
            {
                account.SignIn();
                return;
            }
            // Signing out wipes this device's progress (it stays in the account): ask twice.
            if (Time.unscaledTime > accountSignOutArmedUntil)
            {
                accountSignOutArmedUntil = Time.unscaledTime + 3f;
                RefreshAccountRow();
                Juice.Punch(accountButton.transform, 0.08f, 0.25f);
                return;
            }
            accountSignOutArmedUntil = 0f;
            account.SignOut();
        }

        private float nextAccountRefresh;

        /// <summary>The account changes on its own (browser sign-in, cloud sync): follow it while Ajustes is open.</summary>
        private void TickAccountRow()
        {
            TickLoginPrompt();
            if (settingsPage == null || !settingsPage.gameObject.activeInHierarchy || Time.unscaledTime < nextAccountRefresh) return;
            nextAccountRefresh = Time.unscaledTime + 0.5f;
            RefreshAccountRow();
        }

        private RectTransform loginPrompt;
        private bool LoginPromptOpen => loginPrompt != null && loginPrompt.gameObject.activeSelf;

        private void TickLoginPrompt()
        {
            var account = AccountService.Instance;
            if (account == null) return;
            if (account.SignedIn)
            {
                if (LoginPromptOpen) loginPrompt.gameObject.SetActive(false);
                return;
            }
            if (!account.Ready || account.Busy || !account.CanSignIn || account.LoginPromptDismissed ||
                run.phase != RunPhase.ShipSelection || mainMenu == null || !mainMenu.gameObject.activeInHierarchy ||
                RewardsPopupOpen || StorePopupOpen || StarterPopupOpen || LoginPromptOpen || WelcomePopupOpen) return;

            if (loginPrompt == null) BuildLoginPrompt();
            loginPrompt.gameObject.SetActive(true);
            loginPrompt.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(loginPrompt), 0.18f);
        }

        private void CloseLoginPrompt()
        {
            AccountService.Instance?.DismissLoginPrompt();
            HideScreen(loginPrompt);
        }

        private void BuildLoginPrompt()
        {
            // Reuse the menu's reward/store sheet and close/back conventions.
            loginPrompt = UIFactory.Page(safeRoot, "LoginPrompt", new Color(0.005f, 0.01f, 0.025f, 0.94f), out var backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, CloseLoginPrompt);
            var accent = UIFactory.ActionBorder;
            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(loginPrompt, "Panel", accent, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            panel.raycastTarget = true;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(900f, UIFactory.Pick(760f, 860f)));
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            // Matching cut-corner layers form a crisp border; mask the background
            // lighting so it cannot fill the diagonal corners back into a rectangle.
            var surface = UIFactory.Panel9(panel.transform, "Surface", new Color(0.035f, 0.075f, 0.095f, 1f), cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            ApplyKitFrame(panel, surface, KitRole.Painel);

            var gradient = UIFactory.Rect(surface.transform, "Gradient", new Color(0.06f, 0.15f, 0.18f, 0.55f), ArtLibrary.GradientPanel);
            UIFactory.Stretch(gradient.rectTransform, 6f);
            gradient.raycastTarget = false;
            SweepGlow(surface.rectTransform, "TopLight", true, 160f, WithAlpha(accent, 0.06f));

            var topRule = UIFactory.Rect(panel.transform, "TopRule", WithAlpha(accent, 0.6f), ArtLibrary.Pixel);
            UIFactory.Place(topRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            topRule.raycastTarget = false;

            var title = UIFactory.Label(panel.transform, "Title", L("SALVE SEU PROGRESSO", "SAVE YOUR PROGRESS", "GUARDA TU PROGRESO"),
                UIFactory.TextHeading, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(740f, 72f));
            title.characterSpacing = 3f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 28f;
            title.fontSizeMax = UIFactory.TextHeading;

            // A single cloud-save symbol replaces the decorative ship instrument panel.
            var cloud = UIFactory.Rect(panel.transform, "CloudSave", accent, ArtLibrary.IconCloudSave);
            var emblemSize = UIFactory.Pick(192f, 224f);
            UIFactory.Place(cloud.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, UIFactory.Pick(-230f, -248f)), Vector2.one * emblemSize);
            cloud.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cloud.preserveAspect = true;
            cloud.raycastTarget = false;
            var body = UIFactory.Label(panel.transform, "Body",
                L("Faça login para salvar seu progresso na nuvem.",
                    "Sign in to save your progress to the cloud.",
                    "Inicia sesión para guardar tu progreso en la nube."),
                UIFactory.TextBody, new Color(0.84f, 0.91f, 0.94f), TextAlignmentOptions.Center);
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, UIFactory.Pick(-344f, -400f)), new Vector2(740f, 100f));
            body.enableAutoSizing = true;
            body.fontSizeMin = 28f;
            body.fontSizeMax = UIFactory.TextBody;
            var signIn = UIFactory.Button(panel.transform, "SignIn", L("LOGAR", "SIGN IN", "INICIAR SESIÓN"),
                new Color(0.055f, 0.34f, 0.4f), UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            var signInRect = signIn.GetComponent<RectTransform>();
            UIFactory.Place(signInRect, new Vector2(0.5f, 0f), new Vector2(0f, 154f), new Vector2(740f, 112f));
            StyleCutAction(signIn, cutFrame, accent, UIFactory.ActionFillStrong);
            var actionIcon = UIFactory.Rect(signInRect, "CloudSave", UIFactory.Ink, ArtLibrary.IconCloudSave);
            UIFactory.Place(actionIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(40f, 40f));
            actionIcon.preserveAspect = true;
            actionIcon.raycastTarget = false;
            var buttonRule = UIFactory.Rect(signInRect, "Accent", accent, ArtLibrary.Pixel);
            UIFactory.Place(buttonRule.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(4f, 76f));
            buttonRule.raycastTarget = false;
            var arrow = UIFactory.Rect(signInRect, "Arrow", UIFactory.Ink, ArtLibrary.IconNext);
            UIFactory.Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(36f, 36f));
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            var caption = signInRect.Find("Caption").GetComponent<TMP_Text>();
            caption.rectTransform.offsetMin = new Vector2(76f, 12f);
            caption.rectTransform.offsetMax = new Vector2(-76f, -12f);
            caption.characterSpacing = 4f;
            Rebind(signIn, () => { CloseLoginPrompt(); AccountService.Instance?.SignIn(); });
            var skip = UIFactory.Button(panel.transform, "Skip", L("AGORA NÃO", "NOT NOW", "AHORA NO"),
                new Color(0.035f, 0.075f, 0.09f), UIFactory.Ink, UIFactory.TextBody, Sound.Back);
            UIFactory.Place(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(740f, 100f));
            StyleCutAction(skip, cutFrame, WithAlpha(accent, 0.45f), UIFactory.ActionFillMuted);
            Rebind(skip, CloseLoginPrompt);
            var close = UIFactory.RoundButton(panel.transform, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep, UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(close, CloseLoginPrompt);
        }

        /// <summary>
        /// Borda recortada + vidro de um botão. Fora da gameplay, veste a peça do
        /// kit do papel indicado (Botao por padrão); devolve true quando o kit foi
        /// aplicado, para o chamador não reescalar o 9-slice da moldura antiga.
        /// </summary>
        internal static bool StyleCutAction(Button button, Sprite cutFrame, Color border, Color fill, bool showRail = true,
            KitRole role = KitRole.Botao)
        {
            var image = button.GetComponent<Image>();
            image.sprite = cutFrame;
            image.color = border;
            image.pixelsPerUnitMultiplier = 0.3f;
            var inset = UIFactory.Panel9(button.transform, "Inset", fill, cutFrame);
            UIFactory.Stretch(inset.rectTransform, 5f);
            inset.pixelsPerUnitMultiplier = 0.3f;
            inset.raycastTarget = false;
            inset.transform.SetAsFirstSibling();
            var kit = ApplyKitFrame(image, inset, role);
            if (!showRail) return kit;
            var rail = UIFactory.Rect(button.transform, "BottomRail", WithAlpha(border, 0.5f), ArtLibrary.Pixel);
            UIFactory.Place(rail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(180f, 3f));
            rail.raycastTarget = false;
            return kit;
        }

        private static void TintSettingsAction(Button button, bool active, bool danger = false)
        {
            var accent = danger ? UIFactory.Danger : UIFactory.ActionBorder;
            button.GetComponent<Image>().color = WithAlpha(accent, active ? (danger ? 1f : UIFactory.ActionBorder.a) : 0.38f);
            var inset = button.transform.Find("Inset");
            if (inset != null) inset.GetComponent<Image>().color = danger
                ? new Color(0.28f, 0.065f, 0.07f)
                : active ? UIFactory.ActionFillStrong : UIFactory.ActionFillMuted;
        }

        private void RefreshAccountRow()
        {
            if (accountButton == null || settingsPage == null) return;
            var account = AccountService.Instance;
            var row = accountButton.transform.parent;
            string label = L("CONTA", "ACCOUNT", "CUENTA");
            string action;
            var color = UIFactory.Primary;
            var interactable = true;

            if (account == null || !account.Ready || !account.CanSignIn && !account.SignedIn)
            {
                action = L("INDISPONÍVEL", "UNAVAILABLE", "NO DISPONIBLE");
                label += "<br><size=60%>" + (account != null && account.Ready
                    ? L("Login ainda não configurado", "Sign-in not set up yet", "Inicio de sesión sin configurar")
                    : L("Sem conexão com o serviço", "Service unreachable", "Servicio no disponible")) + "</size>";
                color = UIFactory.PanelSoft;
                interactable = false;
            }
            else if (account.Busy)
            {
                action = L("CONECTANDO…", "CONNECTING…", "CONECTANDO…");
                color = UIFactory.PanelSoft;
                interactable = false;
            }
            else if (account.SignedIn)
            {
                var armed = Time.unscaledTime <= accountSignOutArmedUntil;
                action = armed ? L("TOQUE DE NOVO", "TAP AGAIN", "TOCA OTRA VEZ") : L("SAIR", "SIGN OUT", "SALIR");
                color = armed ? UIFactory.Danger : UIFactory.PanelSoft;
                var email = account.Email;
                label += "<br><size=60%>" + (string.IsNullOrEmpty(email)
                    ? L("Conectada", "Signed in", "Conectada") : email) + "</size>";
            }
            else
            {
                action = L("LOGAR", "SIGN IN", "INICIAR SESIÓN");
                label += "<br><size=60%>" + (string.IsNullOrEmpty(account.LastError)
                    ? L("Salve o progresso na conta", "Save progress to your account", "Guarda el progreso en la cuenta")
                    : L("Não foi possível entrar", "Sign-in failed", "No se pudo entrar")) + "</size>";
            }

            SetLabel(row, "Label", label);
            SetButtonLabel(accountButton, action);
            TintSettingsAction(accountButton, interactable, color == UIFactory.Danger);
            accountButton.interactable = interactable;
        }
        /// pill (toggle rows are a single big button) or room for a control.
        /// </summary>
        private Button SettingsRow(RectTransform panel, string name, Sprite icon, int row, bool toggle)
        {
            var button = UIFactory.Button(panel, name, null, UIFactory.PanelDeep, UIFactory.Ink,
                UIFactory.TextButton, toggle ? Sound.Toggle : Sound.None);
            var rect = button.GetComponent<RectTransform>();
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -36f - row * SettingsRowStep),
                new Vector2(SettingsRowWidth, SettingsRowHeight));
            StyleCutAction(button, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"),
                WithAlpha(UIFactory.Accent, 0.26f), UIFactory.ActionFillMuted, false);
            button.interactable = toggle;
            if (!toggle)
            {
                // Plain rows hold other controls; they are not buttons themselves.
                button.transition = Selectable.Transition.None;
                button.GetComponent<Image>().raycastTarget = false;
            }

            var glyph = UIFactory.Rect(rect, "Icon", UIFactory.Accent, icon);
            glyph.preserveAspect = true;
            UIFactory.Place(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(64f, 64f));

            var label = UIFactory.Label(rect, "Label", "", UIFactory.TextButton, UIFactory.Ink, TextAlignmentOptions.Left, true);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(360f, 60f));
            label.characterSpacing = 4f;
            label.enableAutoSizing = true;
            label.fontSizeMin = UIFactory.TextMin;
            label.fontSizeMax = UIFactory.TextButton;

            if (toggle)
            {
                // Estado por ícone (✓ / ✕) e cor, não por palavra: a linha inteira é o botão.
                var state = UIFactory.Pill(rect, "State", "", UIFactory.Good, new Color(0.03f, 0.06f, 0.04f), 88f,
                    UIFactory.TextBody);
                UIFactory.Place(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(176f, 88f));
                state.sprite = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
                state.type = Image.Type.Sliced;
                state.pixelsPerUnitMultiplier = 0.4f;
                var stateIcon = UIFactory.Rect(state.rectTransform, "StateIcon", UIFactory.Ink, ArtLibrary.IconCheck);
                UIFactory.Place(stateIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
                stateIcon.preserveAspect = true;
                stateIcon.raycastTarget = false;
            }
            return button;
        }

        /// <summary>A slider with a thumb big enough for a finger.</summary>
        private static Slider BuildSlider(Transform row)
        {
            var root = UIFactory.NewRect(row, "Slider");
            UIFactory.Place(root, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(480f, 112f));
            var slider = root.gameObject.AddComponent<Slider>();

            var track = UIFactory.Pill(root, "Track", "", new Color(0.02f, 0.03f, 0.06f, 1f), Color.clear, 22f);
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 22f));
            // A generous invisible hit area: tapping anywhere along the row
            // moves the thumb, not only on the thin track.
            var hit = UIFactory.Rect(root, "HitArea", Color.clear);
            UIFactory.Stretch(hit.rectTransform);
            hit.raycastTarget = true;
            hit.rectTransform.SetAsFirstSibling();

            var fillArea = UIFactory.NewRect(root, "Fill Area");
            UIFactory.Place(fillArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 22f));
            var fill = UIFactory.Pill(fillArea, "Fill", "", UIFactory.Accent, Color.clear, 22f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = UIFactory.NewRect(root, "Handle Slide Area");
            UIFactory.Place(handleArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 84f));
            var handle = UIFactory.Rect(handleArea, "Handle", UIFactory.Accent, ArtLibrary.ArtOr("hex_disc", ArtLibrary.RoundFill));
            // The slider stretches the handle over its area vertically, so the
            // area sets the height and only the width is given here.
            handle.rectTransform.sizeDelta = new Vector2(84f, 0f);
            handle.raycastTarget = true;
            UIFactory.DropShadow(handle, 14f, 4f, 0.5f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = AudioListener.volume;
            return slider;
        }

        private void RefreshSettings()
        {
            if (settingsPage == null) return;
            BuildReleaseAccess();
            RefreshStorePanel();
            RefreshAccountRow();
            var sound = SoundDirector.Instance;
            SetToggleState(musicToggle, sound == null || sound.musicEnabled);
            SetToggleState(effectsToggle, sound == null || sound.sfxEnabled);
            volumeSlider.SetValueWithoutNotify(AudioListener.volume);

            SetButtonLabel(languageValueButton, GameLocalization.NativeName(GameLocalization.Current) + "  >");
            for (var i = 0; i < languageButtons.Length; i++)
            {
                if (languageButtons[i] == null) continue;
                var selected = (int)GameLocalization.Current == i;
                TintSettingsAction(languageButtons[i], selected);
                var check = languageButtons[i].transform.Find("Check");
                if (check != null) check.gameObject.SetActive(selected);
            }

            var landscape = OrientationSetting.Landscape;
            for (var i = 0; i < orientationButtons.Length; i++)
            {
                if (orientationButtons[i] == null) continue;
                var selected = (i == 1) == landscape;
                TintSettingsAction(orientationButtons[i], selected);
                SetButtonLabel(orientationButtons[i], i == 0
                    ? L("RETRATO", "PORTRAIT", "VERTICAL")
                    : L("PAISAGEM", "LANDSCAPE", "HORIZONTAL"));
            }
        }

        private void SetToggleState(Button row, bool on)
        {
            var state = row.transform.Find("State");
            if (state == null) return;
            state.GetComponent<Image>().color = on ? UIFactory.ActionBorder : new Color(0.16f, 0.23f, 0.27f);
            SetLabel(state, "Label", "");
            var stateIcon = state.Find("StateIcon");
            if (stateIcon != null)
            {
                var image = stateIcon.GetComponent<Image>();
                image.sprite = on ? ArtLibrary.IconCheck : ArtLibrary.IconCancel;
                image.color = on ? new Color(0.02f, 0.07f, 0.09f) : UIFactory.Ink;
            }
            var icon = row.transform.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = on ? UIFactory.Accent : UIFactory.InkDim;
        }

        // ------------------------------------------------------------------
        // Language
        // ------------------------------------------------------------------

        private void RefreshMenuLanguage()
        {
            SetButtonLabel(playButton, L("JOGAR", "PLAY", "JUGAR"));
            // One word under each destination icon.
            SetLabel(mainMenu, "Tile_Ships/Caption", L("NAVES", "SHIPS", "NAVES"));
            SetLabel(mainMenu, "Tile_BaseDeck/Caption", L("DECK BASE", "BASE DECK", "MAZO BASE"));
            SetLabel(mainMenu, "Tile_Upgrades/Caption", L("UPGRADES", "UPGRADES", "MEJORAS"));

            SetLabel(levelsPage, "Title", L("FASES", "STAGES", "FASES"));
            SetLabel(mainMenu, "StageSelect/ModeCaption", L("MODO DE JOGO", "GAME MODE", "MODO DE JUEGO"));
            // The tiles carry only their number; the detail panel's words are
            // set by RefreshStageSelection (below).

            SetLabel(settingsPage, "Title", L("AJUSTES", "SETTINGS", "AJUSTES"));
            SetLabel(settingsPage, "Panel/Music/Label", L("MÚSICA", "MUSIC", "MÚSICA"));
            SetLabel(settingsPage, "Panel/Effects/Label", L("EFEITOS", "EFFECTS", "EFECTOS"));
            SetLabel(settingsPage, "Panel/Volume/Label", L("VOLUME", "VOLUME", "VOLUMEN"));
            SetLabel(settingsPage, "Panel/Language/Label", L("IDIOMA", "LANGUAGE", "IDIOMA"));
            SetLabel(settingsPage, "Panel/Orientation/Label", L("TELA", "SCREEN", "PANTALLA"));
            SetLabel(settingsPage, "Panel/Support/Label", L("SUPORTE", "SUPPORT", "SOPORTE"));
            SetLabel(settingsPage, "Panel/Notes/Label", L("NOTAS DA VERSÃO", "RELEASE NOTES", "NOTAS DE LA VERSIÓN"));
            RefreshStageSelection();
            RefreshModesPage();
            RefreshSettings();
        }
    }
}
