// AstroCrawler - game modes (campaign / endless / squadron) and the endless ranking.
//
// The mode button on the main menu (under the ship) opens the modes page: one
// card per mode. Tapping a card selects that mode; JOGAR on it starts it at
// once; the second button leads further in (the campaign's levels, the endless
// ranking). The ranking page draws the top 50 from Play Games / Game Center
// (Leaderboards) inside the game; the platform's own screen is one tap away.
// Both pages are runtime-only, built in Bind like the other menu pages.

using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private RectTransform modesPage;
        private RectTransform rankingPage;
        private RectTransform campaignCard;
        private RectTransform endlessCard;
        private RectTransform squadronCard;
        private TrueShadow campaignGlow;
        private TrueShadow endlessGlow;
        private TrueShadow squadronGlow;

        private TMP_Text rankingSubtitle;
        private TMP_Text rankingBest;
        private TMP_Text rankingStatus;
        private RectTransform rankingContent;
        private ScrollRect rankingScroll;
        private Button rankingPrimary;
        private Button rankingNative;
        private readonly List<RectTransform> rankingRows = new List<RectTransform>();
        private Leaderboards leaderboards;

        private const float RankingRowHeight = 96f;
        private const float RankingRowStep = 106f;

        private static readonly Color CampaignAccent = UIFactory.Accent;
        // Cyan too: on the menu every button is cyan, and magenta means danger
        // (a locked mode painted magenta read as an error). The modes differ by
        // icon, name and text, not by colour.
        private static readonly Color EndlessAccent = UIFactory.Accent;
        private static readonly Color SquadronAccent = UIFactory.Accent;
        private static readonly Color Gold = Palette.Ambar;
        private static readonly Color Silver = Palette.Fosforo;
        private static readonly Color Bronze = new Color(0.9f, 0.58f, 0.35f);

        private static Sprite IconEndless => ArtLibrary.ArtOr("ic_infinity", ArtLibrary.IconStages);
        private static Sprite IconSquadron => ArtLibrary.ArtOr("ic_esquadrao", ArtLibrary.Ship);
        private static Sprite IconRanking => ArtLibrary.ArtOr("ic_trofeu", ArtLibrary.IconStarFilled);

        // ------------------------------------------------------------------
        // Modes page
        // ------------------------------------------------------------------

        private void BuildModesPage()
        {
            if (modesPage != null) return;
            TMP_Text title;
            modesPage = BuildMenuPage("ModesPage", out title);

            var portrait = UIFactory.Portrait;
            // Three modes: stacked in portrait (the title keeps its place at the
            // top), side by side in landscape.
            var size = portrait ? new Vector2(960f, 540f) : new Vector2(580f, 620f);
            var campaignAt = portrait ? new Vector2(0f, 520f) : new Vector2(-610f, -40f);
            var endlessAt = portrait ? new Vector2(0f, -40f) : new Vector2(0f, -40f);
            var squadronAt = portrait ? new Vector2(0f, -600f) : new Vector2(610f, -40f);

            // A compass, like the menu's mode button (the old "Campaign" glyph read as ▶).
            campaignCard = BuildModeCard("Campaign", CampaignAccent, MenuCampaignIcon, size, campaignAt,
                out campaignGlow, SelectCampaignMode, PlayCampaignMode, OpenCampaignLevels);
            endlessCard = BuildModeCard("Endless", EndlessAccent, IconEndless, size, endlessAt,
                out endlessGlow, SelectEndlessMode, PlayEndlessMode, () => OpenMenuSection(MenuSection.Ranking));
            // No second page to open: JOGAR takes the whole bottom row.
            squadronCard = BuildModeCard("Squadron", SquadronAccent, IconSquadron, size, squadronAt,
                out squadronGlow, SelectSquadronMode, PlaySquadronMode, null);

            modesPage.gameObject.SetActive(false);
        }

        private RectTransform BuildModeCard(string name, Color accent, Sprite icon, Vector2 size, Vector2 at,
            out TrueShadow glow, UnityEngine.Events.UnityAction select, UnityEngine.Events.UnityAction play,
            UnityEngine.Events.UnityAction secondary)
        {
            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var card = UIFactory.Panel9(modesPage, name, UIFactory.PanelSoft);
            var rect = card.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), at, size);
            UIFactory.DropShadow(card, 34f, 12f, 0.6f);
            glow = UIFactory.GlowShadow(card, WithAlpha(accent, 0f), 44f, 0.12f);

            // The whole card selects the mode.
            card.raycastTarget = true;
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card;
            cardButton.transition = Selectable.Transition.None;
            if (!StyleCutAction(cardButton, cutFrame, accent, Darken(accent, 0.1f), false, KitRole.Card))
            {
                card.pixelsPerUnitMultiplier = 0.2f;
                var inset = rect.Find("Inset").GetComponent<Image>();
                inset.pixelsPerUnitMultiplier = 0.2f;
                UIFactory.Stretch(inset.rectTransform, 8f);
            }
            UIFactory.SetClickSound(cardButton, Sound.None);
            Rebind(cardButton, select);

            var stripe = UIFactory.Rect(rect, "Stripe", accent, ArtLibrary.Pixel);
            UIFactory.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(size.x - 100f, 5f));

            var plate = UIFactory.Rect(rect, "IconPlate", new Color(0.02f, 0.03f, 0.06f, 0.9f), ArtLibrary.CircleFilled);
            UIFactory.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -50f), new Vector2(128f, 128f));
            plate.raycastTarget = false;
            UIFactory.GlowShadow(plate, WithAlpha(accent, 0.45f), 22f, 0.05f);
            var glyph = UIFactory.Rect(plate.rectTransform, "Icon", accent, icon);
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            UIFactory.Stretch(glyph.rectTransform, 26f);

            var label = UIFactory.Label(rect, "Name", "", 58, UIFactory.Ink, TextAlignmentOptions.Left, true);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(196f, -56f), new Vector2(size.x - 236f, 70f));
            label.characterSpacing = 5f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 58f;

            var badge = UIFactory.Pill(rect, "SelectedBadge", "", accent, new Color(0.03f, 0.04f, 0.07f), 52f, 26,
                ArtLibrary.IconCheck);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(196f, -128f), new Vector2(290f, 52f));
            badge.sprite = cutFrame;
            badge.type = Image.Type.Sliced;
            badge.pixelsPerUnitMultiplier = 0.4f;

            var description = UIFactory.Label(rect, "Description", "", 34, UIFactory.InkDim, TextAlignmentOptions.TopLeft);
            UIFactory.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -206f),
                new Vector2(size.x - 90f, Mathf.Min(150f, size.y - 438f)));
            description.enableAutoSizing = true;
            description.fontSizeMin = UIFactory.TextCaption;
            description.fontSizeMax = 34f;
            description.lineSpacing = 4f;

            var status = UIFactory.Label(rect, "Status", "", 36, accent, TextAlignmentOptions.Left, true);
            UIFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(45f, 168f), new Vector2(size.x - 90f, 56f));
            status.characterSpacing = 3f;
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.enableAutoSizing = true;
            status.fontSizeMin = 28f;
            status.fontSizeMax = 36f;

            var buttonWidth = (size.x - 90f - 24f) * 0.5f;
            if (secondary != null)
            {
                var more = UIFactory.Button(rect, "Secondary", "", UIFactory.PanelDeep, UIFactory.Ink, UIFactory.TextButton, Sound.Navigate);
                UIFactory.Place(more.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(45f, 36f),
                    new Vector2(buttonWidth, 116f));
                StyleCutAction(more, cutFrame, WithAlpha(accent, 0.75f), UIFactory.ActionFill);
                Rebind(more, secondary);
            }
            else
            {
                buttonWidth = size.x - 90f;
            }

            var start = UIFactory.Button(rect, "Play", "", Darken(accent, 0.42f), UIFactory.Ink, UIFactory.TextButton, Sound.Play);
            UIFactory.Place(start.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-45f, 36f),
                new Vector2(buttonWidth, 116f));
            StyleCutAction(start, cutFrame, accent, Darken(accent, 0.32f));
            UIFactory.DropShadow(start.GetComponent<Image>(), 16f, 5f, 0.5f);
            Rebind(start, play);
            return rect;
        }

        private void SelectCampaignMode()
        {
            var changed = run.isInfinite;
            Sfx.Play(changed ? Sound.Select : Sound.Tap);
            run.SelectCampaign();
            RefreshModesPage();
            if (changed) UIFactory.FlashGlow(campaignGlow, WithAlpha(CampaignAccent, 1f), WithAlpha(CampaignAccent, SelectedGlow), 0.5f);
        }

        private void SelectEndlessMode()
        {
            if (!run.SelectEndless())
            {
                Sfx.Play(Sound.Denied);
                if (endlessCard != null) Juice.Punch(endlessCard, 0.04f, 0.25f);
                return;
            }
            Sfx.Play(Sound.Select);
            RefreshModesPage();
            UIFactory.FlashGlow(endlessGlow, WithAlpha(EndlessAccent, 1f), WithAlpha(EndlessAccent, SelectedGlow), 0.5f);
        }

        private void SelectSquadronMode()
        {
            var changed = !run.isSquadron;
            if (!run.SelectSquadron())
            {
                Sfx.Play(Sound.Denied);
                return;
            }
            Sfx.Play(changed ? Sound.Select : Sound.Tap);
            RefreshModesPage();
            if (changed) UIFactory.FlashGlow(squadronGlow, WithAlpha(SquadronAccent, 1f), WithAlpha(SquadronAccent, SelectedGlow), 0.5f);
        }

        private void PlaySquadronMode()
        {
            if (!run.SelectSquadron()) return;
            StartSelectedShip();
        }

        private void PlayCampaignMode()
        {
            run.SelectCampaign();
            StartSelectedShip();
        }

        private void PlayEndlessMode()
        {
            if (!run.SelectEndless())
            {
                Sfx.Play(Sound.Denied);
                if (endlessCard != null) Juice.Punch(endlessCard, 0.04f, 0.25f);
                return;
            }
            StartSelectedShip();
        }

        private void OpenCampaignLevels()
        {
            run.SelectCampaign();
            OpenMenuSection(MenuSection.Stages);
        }

        private void RefreshModesPage()
        {
            if (modesPage == null) return;
            SetLabel(modesPage, "Title", L("MODOS DE JOGO", "GAME MODES", "MODOS DE JUEGO"));

            var ship = config.GetShip(selectedShipId);
            var shipName = ship != null ? GameLocalization.Content(ship.name).ToUpperInvariant() : "";
            RefreshModeCard(campaignCard, campaignGlow, CampaignAccent, true, !run.isInfinite,
                L("CAMPANHA", "CAMPAIGN", "CAMPAÑA"),
                L("10 níveis, cada um termina com um chefe. Cada nave tem o próprio progresso.",
                    "10 levels, each ending with a boss. Every ship has its own progress.",
                    "10 niveles, cada uno termina con un jefe. Cada nave tiene su propio progreso."),
                shipName + "   ·   " + StageProgress.ClearedCount(selectedShipId) + " / " + StageCatalog.Count +
                    L(" NÍVEIS CONCLUÍDOS", " LEVELS CLEARED", " NIVELES COMPLETADOS"),
                L("NÍVEIS", "LEVELS", "NIVELES"));

            var unlocked = run.endlessUnlocked;
            RefreshModeCard(endlessCard, endlessGlow, EndlessAccent, unlocked, run.isEndless,
                StageCatalog.EndlessName,
                unlocked ? EndlessDescription() + "\n" + EndlessScoreDescription() : EndlessUnlockDescription(),
                EndlessScore.Best > 0 ? L("RECORDE ", "BEST ", "RÉCORD ") + EndlessScore.Format(EndlessScore.Best) : "",
                L("RANKING", "RANKING", "RANKING"));

            var standard = config.GetShip(ShipUnlocks.FreeShipId);
            var standardName = standard != null ? GameLocalization.Content(standard.name).ToUpperInvariant() : "";
            RefreshModeCard(squadronCard, squadronGlow, SquadronAccent, true, run.isSquadron,
                RunController.SquadronName,
                string.Format(L(
                    "Voe com a nave padrão. A cada {0} níveis, troque a melhoria por uma nave aliada (até {1}, em triângulo).",
                    "Fly the standard ship. Every {0} levels, trade the upgrade for an allied ship (up to {1}, in a triangle).",
                    "Vuela con la nave estándar. Cada {0} niveles, cambia la mejora por una nave aliada (hasta {1}, en triángulo)."),
                    RunController.SquadronPickEvery, SquadronWing.SlotCount),
                standardName + (RunController.SquadronBest > 0
                    ? "   ·   " + L("RECORDE ", "BEST ", "RÉCORD ") + EndlessScore.Format(RunController.SquadronBest)
                    : ""),
                "");
        }

        private static string EndlessScoreDescription() => L(
            "Pontue destruindo inimigos e vencendo ondas. Os 50 melhores entram no ranking.",
            "Score by destroying enemies and clearing waves. The top 50 make the ranking.",
            "Puntúa destruyendo enemigos y superando oleadas. Los 50 mejores entran en el ranking.");

        private void RefreshModeCard(RectTransform card, TrueShadow glow, Color accent, bool unlocked, bool selected,
            string name, string description, string status, string secondary)
        {
            if (card == null) return;
            // Locked: Fósforo, quiet - neither the mode colour nor a danger colour.
            var frame = unlocked ? accent : Palette.Fosforo;
            card.GetComponent<Image>().color = WithAlpha(frame, selected ? 1f : unlocked ? 0.6f : 0.22f);
            var inset = card.Find("Inset");
            if (inset != null) inset.GetComponent<Image>().color = Palette.Vidro(frame, selected ? 0.12f : 0.05f);
            UIFactory.Group(card).alpha = !unlocked ? 0.8f : selected ? 1f : 0.9f;
            var stripe = card.Find("Stripe");
            if (stripe != null) stripe.GetComponent<Image>().color = WithAlpha(frame, unlocked ? 1f : 0.3f);
            var glyph = card.Find("IconPlate/Icon");
            if (glyph != null) glyph.GetComponent<Image>().color = unlocked ? accent : WithAlpha(Palette.Fosforo, 0.45f);
            UIFactory.FadeGlow(glow, WithAlpha(accent, selected ? SelectedGlow : 0f), 0.25f);
            LeanTween.cancel(card.gameObject, false, TweenAction.SCALE);
            LeanTween.scale(card.gameObject, Vector3.one * (selected ? 1.02f : 1f), 0.22f)
                .setEase(LeanTweenType.easeOutBack).setIgnoreTimeScale(true);

            SetLabel(card, "Name", name);
            SetLabel(card, "Description", description);
            SetLabel(card, "Status", status);
            var badge = card.Find("SelectedBadge");
            if (badge != null)
            {
                badge.gameObject.SetActive(selected || !unlocked);
                SetLabel(badge, "Label", unlocked ? L("SELECIONADO", "SELECTED", "ELEGIDO") : L("BLOQUEADO", "LOCKED", "BLOQUEADO"));
                var icon = badge.Find("Icon") as RectTransform;
                if (icon != null)
                {
                    var image = icon.GetComponent<Image>();
                    image.sprite = unlocked ? ArtLibrary.IconCheck : ArtLibrary.IconLock;
                    image.color = unlocked ? new Color(0.03f, 0.04f, 0.07f) : UIFactory.InkDim;
                }
                badge.GetComponent<Image>().color = unlocked ? accent : WithAlpha(Palette.Fosforo, 0.16f);
                var badgeLabel = badge.Find("Label");
                if (badgeLabel != null)
                    badgeLabel.GetComponent<TMP_Text>().color = unlocked ? new Color(0.03f, 0.04f, 0.07f) : UIFactory.InkDim;
            }

            // JOGAR is amber on the chosen mode only: one amber button per
            // screen, the same colour as the menu's JOGAR. The other card's
            // JOGAR stays cyan; a locked one is dark.
            var play = card.Find("Play");
            if (play != null)
            {
                var amber = unlocked && selected;
                var playColor = amber ? Palette.Ambar : frame;
                play.GetComponent<Button>().interactable = unlocked;
                SetButtonLabel(play.GetComponent<Button>(), L("JOGAR", "PLAY", "JUGAR"));
                play.GetComponent<Image>().color = WithAlpha(playColor, !unlocked ? 0.25f : amber ? 1f : 0.75f);
                var playInset = play.Find("Inset");
                if (playInset != null) playInset.GetComponent<Image>().color =
                    Palette.Vidro(playColor, !unlocked ? 0.03f : amber ? 0.4f : 0.14f);
                var playRail = play.Find("BottomRail");
                if (playRail != null) playRail.GetComponent<Image>().color = WithAlpha(playColor, unlocked ? 0.5f : 0.15f);
                var playCaption = play.Find("Caption");
                if (playCaption != null)
                    playCaption.GetComponent<TMP_Text>().color = unlocked ? UIFactory.Ink : UIFactory.InkDim;
            }
            // The second button (levels, ranking) waits for the mode too.
            var more = card.Find("Secondary");
            if (more != null)
            {
                SetButtonLabel(more.GetComponent<Button>(), secondary);
                more.GetComponent<Button>().interactable = unlocked;
                more.GetComponent<Image>().color = WithAlpha(frame, unlocked ? 0.75f : 0.25f);
                var moreInset = more.Find("Inset");
                if (moreInset != null) moreInset.GetComponent<Image>().color =
                    unlocked ? UIFactory.ActionFill : Palette.Vidro(Palette.Fosforo, 0.03f);
                var moreRail = more.Find("BottomRail");
                if (moreRail != null) moreRail.GetComponent<Image>().color = WithAlpha(frame, unlocked ? 0.5f : 0.15f);
                var moreCaption = more.Find("Caption");
                if (moreCaption != null)
                    moreCaption.GetComponent<TMP_Text>().color = unlocked ? UIFactory.Ink : UIFactory.InkDim;
            }
        }

        /// <summary>The menu's mode button: which mode, and the level or the record under it.</summary>
        private string ModeLine()
        {
            if (run.isSquadron)
            {
                var line = "<color=" + UIFactory.ColorTag(SquadronAccent) + ">" + RunController.SquadronName + "</color>";
                if (RunController.SquadronBest > 0)
                    line += "  ·  " + L("RECORDE ", "BEST ", "RÉCORD ") + EndlessScore.Format(RunController.SquadronBest);
                return line;
            }
            if (run.isEndless)
            {
                var line = "<color=" + UIFactory.ColorTag(EndlessAccent) + ">" + StageCatalog.EndlessName + "</color>";
                if (EndlessScore.Best > 0)
                    line += "  ·  " + L("RECORDE ", "BEST ", "RÉCORD ") + EndlessScore.Format(EndlessScore.Best);
                return line;
            }
            var stage = run.stageIndex;
            return L("CAMPANHA", "CAMPAIGN", "CAMPAÑA") + "  ·  <color=" + UIFactory.ColorTag(StageCatalog.Accent(stage)) + ">" +
                StageCatalog.Name(stage) + "</color>" +
                (StageProgress.IsCleared(selectedShipId, stage)
                    ? "  <color=" + UIFactory.ColorTag(UIFactory.Good) + ">· " +
                      L("CONCLUÍDO", "CLEARED", "COMPLETADO") + "</color>"
                    : "");
        }

        // ------------------------------------------------------------------
        // Ranking page
        // ------------------------------------------------------------------

        private void BuildRankingPage()
        {
            if (rankingPage != null) return;
            rankingRows.Clear();
            TMP_Text title;
            rankingPage = BuildMenuPage("RankingPage", out title);
            // Back returns to the modes, where the ranking was opened from.
            Rebind(rankingPage.Find("Back").GetComponent<Button>(), () => OpenMenuSection(MenuSection.Modes));

            rankingSubtitle = UIFactory.Label(rankingPage, "Subtitle", "", 32, EndlessAccent, TextAlignmentOptions.Center, true);
            UIFactory.Place(rankingSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(UIFactory.Pick(1300f, 1000f), 44f));
            rankingSubtitle.characterSpacing = 4f;
            rankingSubtitle.textWrappingMode = TextWrappingModes.NoWrap;
            rankingSubtitle.enableAutoSizing = true;
            rankingSubtitle.fontSizeMin = 26f;
            rankingSubtitle.fontSizeMax = 32f;

            // The player's own line, always visible above the list.
            var best = UIFactory.Panel9(rankingPage, "Best", UIFactory.PanelDeep);
            UIFactory.Place(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f),
                new Vector2(UIFactory.Pick(1200f, 1000f), 96f));
            HudFrame(best, UIFactory.Evolved);
            var trophy = UIFactory.Rect(best.rectTransform, "Icon", UIFactory.Evolved, IconRanking);
            trophy.preserveAspect = true;
            UIFactory.Place(trophy.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(60f, 60f));
            rankingBest = UIFactory.Label(best.rectTransform, "Value", "", 36, UIFactory.Ink, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(rankingBest.rectTransform);
            rankingBest.rectTransform.offsetMin = new Vector2(104f, 0f);
            rankingBest.rectTransform.offsetMax = new Vector2(-24f, 0f);
            rankingBest.textWrappingMode = TextWrappingModes.NoWrap;
            rankingBest.enableAutoSizing = true;
            rankingBest.fontSizeMin = 28f;
            rankingBest.fontSizeMax = 36f;

            var scrollRoot = UIFactory.NewRect(rankingPage, "RankingScroll");
            UIFactory.Stretch(scrollRoot);
            scrollRoot.offsetMin = new Vector2(24f, 196f);
            scrollRoot.offsetMax = new Vector2(-24f, -316f);
            rankingScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            rankingScroll.horizontal = false;
            rankingScroll.vertical = true;
            rankingScroll.movementType = ScrollRect.MovementType.Elastic;
            rankingScroll.scrollSensitivity = 35f;
            var viewport = UIFactory.NewRect(scrollRoot, "Viewport");
            UIFactory.Stretch(viewport);
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            rankingContent = UIFactory.NewRect(viewport, "Content");
            UIFactory.Place(rankingContent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(UIFactory.Pick(1200f, 1000f), 0f));
            rankingScroll.viewport = viewport;
            rankingScroll.content = rankingContent;

            for (var i = 0; i < Leaderboards.TopCount; i++) rankingRows.Add(BuildRankingRow(i));

            rankingStatus = UIFactory.Label(scrollRoot, "Status", "", 36, UIFactory.InkDim, TextAlignmentOptions.Center, true);
            UIFactory.Place(rankingStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(880f, 260f));
            rankingStatus.lineSpacing = 6f;

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var width = UIFactory.Pick(560f, 470f);
            rankingPrimary = UIFactory.Button(rankingPage, "Primary", "", UIFactory.Primary, UIFactory.Ink, UIFactory.TextButton, Sound.Navigate);
            UIFactory.Place(rankingPrimary.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(-(width * 0.5f + 12f), 48f), new Vector2(width, 120f));
            StyleCutAction(rankingPrimary, cutFrame, UIFactory.ActionBorder, UIFactory.ActionFillStrong);
            Rebind(rankingPrimary, RankingPrimaryAction);

            rankingNative = UIFactory.Button(rankingPage, "Native", "", UIFactory.PanelDeep, UIFactory.Ink, UIFactory.TextButton, Sound.Navigate);
            UIFactory.Place(rankingNative.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(width * 0.5f + 12f, 48f), new Vector2(width, 120f));
            StyleCutAction(rankingNative, cutFrame, WithAlpha(EndlessAccent, 0.75f), UIFactory.ActionFill);
            UIFactory.AddButtonIcon(rankingNative, IconRanking, UIFactory.Evolved, 44f);
            Rebind(rankingNative, () => { if (leaderboards != null) leaderboards.ShowNative(); });

            rankingPage.gameObject.SetActive(false);
        }

        private RectTransform BuildRankingRow(int index)
        {
            var width = UIFactory.Pick(1200f, 1000f);
            var row = UIFactory.Panel9(rankingContent, "Row_" + index, UIFactory.PanelSoft);
            var rect = row.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -index * RankingRowStep), new Vector2(width, RankingRowHeight));
            row.raycastTarget = false;

            var rank = UIFactory.Label(rect, "Rank", "", 40, UIFactory.InkDim, TextAlignmentOptions.Center, true);
            UIFactory.Place(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(120f, 60f));
            rank.enableAutoSizing = true;
            rank.fontSizeMin = 28f;
            rank.fontSizeMax = 40f;

            var name = UIFactory.Label(rect, "Name", "", 36, UIFactory.Ink, TextAlignmentOptions.Left, false);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(148f, 0f), new Vector2(width - 148f - 300f, 60f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableAutoSizing = true;
            name.fontSizeMin = 28f;
            name.fontSizeMax = 36f;

            var score = UIFactory.Label(rect, "Score", "", 40, UIFactory.Evolved, TextAlignmentOptions.Right, true);
            UIFactory.Place(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(280f, 60f));
            score.textWrappingMode = TextWrappingModes.NoWrap;
            score.enableAutoSizing = true;
            score.fontSizeMin = 28f;
            score.fontSizeMax = 40f;

            rect.gameObject.SetActive(false);
            return rect;
        }

        private void OpenRankingPage()
        {
            if (leaderboards == null) leaderboards = Leaderboards.EnsureCreated();
            if (leaderboards != null)
            {
                leaderboards.Changed -= RefreshRankingPage;
                leaderboards.Changed += RefreshRankingPage;
                if (leaderboards.Available) leaderboards.LoadTop();
            }
            if (rankingScroll != null) rankingScroll.verticalNormalizedPosition = 1f;
            RefreshRankingPage();
        }

        private void RankingPrimaryAction()
        {
            if (leaderboards == null || !leaderboards.Available) return;
            if (!leaderboards.SignedIn) leaderboards.SignIn(true);
            leaderboards.LoadTop();
        }

        private void RefreshRankingPage()
        {
            if (rankingPage == null || leaderboards == null) return;
            var lb = leaderboards;
            SetLabel(rankingPage, "Title", L("RANKING", "RANKING", "RANKING"));
            rankingSubtitle.text = StageCatalog.EndlessName + "  ·  TOP " + Leaderboards.TopCount;

            builder.Length = 0;
            builder.Append(L("SEU RECORDE  ", "YOUR BEST  ", "TU RÉCORD  "))
                .Append("<color=").Append(UIFactory.ColorTag(UIFactory.Evolved)).Append('>')
                .Append(EndlessScore.Format(EndlessScore.Best)).Append("</color>");
            if (lb.PlayerEntry != null)
                builder.Append("   ·   ").Append(L("POSIÇÃO ", "RANK ", "PUESTO ")).Append('#').Append(lb.PlayerEntry.rank);
            rankingBest.text = builder.ToString();

            var shown = lb.Status == Leaderboards.State.Loaded ? lb.Entries.Count : 0;
            for (var i = 0; i < rankingRows.Count; i++)
            {
                var row = rankingRows[i];
                var visible = i < shown;
                row.gameObject.SetActive(visible);
                if (!visible) continue;
                var entry = lb.Entries[i];
                var podium = entry.rank == 1 ? Gold : entry.rank == 2 ? Silver : entry.rank == 3 ? Bronze : UIFactory.InkDim;
                var rowImage = row.GetComponent<Image>();
                rowImage.color = entry.me ? WithAlpha(EndlessAccent, 0.55f) : entry.rank <= 3 ? WithAlpha(podium, 0.22f) : UIFactory.PanelSoft;
                var rank = row.Find("Rank").GetComponent<TMP_Text>();
                rank.text = "#" + entry.rank;
                rank.color = entry.rank <= 3 ? podium : UIFactory.InkDim;
                var name = row.Find("Name").GetComponent<TMP_Text>();
                name.text = string.IsNullOrEmpty(entry.name) ? "-" : entry.name;
                name.color = entry.me ? UIFactory.Ink : new Color(0.85f, 0.9f, 0.98f);
                SetLabel(row, "Score", EndlessScore.Format(entry.score));
            }
            rankingContent.sizeDelta = new Vector2(rankingContent.sizeDelta.x, Mathf.Max(0, shown) * RankingRowStep);

            string status;
            string primary;
            var primaryVisible = true;
            switch (lb.Status)
            {
                case Leaderboards.State.Unsupported:
                    status = L("O ranking funciona no celular, pelo Play Games ou Game Center.",
                        "The ranking works on phones, through Play Games or Game Center.",
                        "El ranking funciona en el móvil, con Play Games o Game Center.");
                    primary = ""; primaryVisible = false;
                    break;
                case Leaderboards.State.NotConfigured:
                    status = L("O ranking ainda não está disponível nesta versão.",
                        "The ranking is not available in this version yet.",
                        "El ranking aún no está disponible en esta versión.");
                    primary = ""; primaryVisible = false;
                    break;
                case Leaderboards.State.SigningIn:
                case Leaderboards.State.Loading:
                    status = L("CARREGANDO...", "LOADING...", "CARGANDO...");
                    primary = L("ATUALIZAR", "REFRESH", "ACTUALIZAR");
                    break;
                case Leaderboards.State.SignedOut:
                    status = string.Format(L("Entre no {0} para ver o ranking e enviar seus pontos.",
                        "Sign in to {0} to see the ranking and send your score.",
                        "Inicia sesión en {0} para ver el ranking y enviar tu puntuación."), lb.PlatformName);
                    primary = L("ENTRAR", "SIGN IN", "ENTRAR");
                    break;
                case Leaderboards.State.Error:
                    status = L("Não foi possível carregar o ranking. Verifique a internet.",
                        "Couldn't load the ranking. Check your connection.",
                        "No se pudo cargar el ranking. Revisa tu conexión.");
                    primary = L("ATUALIZAR", "REFRESH", "ACTUALIZAR");
                    break;
                default:
                    status = shown == 0 ? L("Ninguém pontuou ainda. Seja o primeiro!",
                        "No scores yet. Be the first!", "Nadie ha puntuado aún. ¡Sé el primero!") : "";
                    primary = L("ATUALIZAR", "REFRESH", "ACTUALIZAR");
                    break;
            }
            rankingStatus.text = status;
            rankingStatus.gameObject.SetActive(!string.IsNullOrEmpty(status));

            rankingPrimary.gameObject.SetActive(primaryVisible);
            SetButtonLabel(rankingPrimary, primary);
            rankingPrimary.interactable = !lb.Busy;
            rankingNative.gameObject.SetActive(primaryVisible);
            SetButtonLabel(rankingNative, lb.PlatformName.ToUpperInvariant());
            rankingNative.interactable = lb.SignedIn;
        }
    }
}
