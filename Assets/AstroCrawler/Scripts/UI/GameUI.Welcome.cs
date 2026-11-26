// AstroCrawler - the welcome login calendar (five days, see WelcomeLogin).
//
// Opens by itself on the main menu the first time each day a reward is
// waiting, once the menu has been on screen for a moment and no other sheet is
// up. Built at runtime from the shared cut-corner sheet (GameUI.Sheets.cs):
// five day tiles, the claim action, a line about what was won and, for a card,
// the card itself revealed over the sheet.

using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private static readonly Color WelcomeGold = Palette.Ambar;
        private static readonly Color WelcomeGoldFill = Palette.Vidro(Palette.Ambar, 0.18f);

        private RectTransform welcomePopup;
        private Image welcomeSheet;
        private Image welcomeTag;
        private readonly RectTransform[] welcomeDays = new RectTransform[WelcomeLogin.Days];
        private Button welcomeClaim;
        private TMP_Text welcomeTitle;
        private TMP_Text welcomeSubtitle;
        private TMP_Text welcomeNote;
        private RectTransform welcomeReveal;
        private RectTransform welcomeRevealCard;
        private int welcomeShownDay = int.MinValue;
        private float welcomeMenuSince = -1f;
        private float nextWelcomeTick;
        private string welcomeLastNote;

        private bool WelcomePopupOpen => welcomePopup != null && welcomePopup.gameObject.activeSelf;

        /// <summary>Called from Update: opens the calendar once a day when a reward is waiting.</summary>
        private void TickWelcome()
        {
            if (Time.unscaledTime < nextWelcomeTick) return;
            nextWelcomeTick = Time.unscaledTime + 0.25f;

            var onMenu = run != null && run.phase == RunPhase.ShipSelection && mainMenu != null &&
                mainMenu.gameObject.activeInHierarchy;
            if (!onMenu || WelcomePopupOpen)
            {
                welcomeMenuSince = -1f;
                return;
            }
            if (welcomeShownDay == WelcomeLogin.Today || !WelcomeLogin.CanClaimToday) return;
            if (RewardsPopupOpen || StorePopupOpen || StarterPopupOpen || LoginPromptOpen || BaseDeckRevealOpen)
            {
                welcomeMenuSince = -1f;
                return;
            }
            if (welcomeMenuSince < 0f)
            {
                welcomeMenuSince = Time.unscaledTime;
                return;
            }
            // Let the menu land before the sheet covers it.
            if (Time.unscaledTime - welcomeMenuSince < 0.8f) return;
            OpenWelcomePopup();
        }

        private void OpenWelcomePopup()
        {
            if (welcomePopup == null) BuildWelcomePopup();
            welcomeShownDay = WelcomeLogin.Today;
            welcomeLastNote = null;
            welcomeReveal.gameObject.SetActive(false);
            RefreshWelcome();
            welcomePopup.gameObject.SetActive(true);
            welcomePopup.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(welcomePopup), 0.2f);
            UIMotion.Pop(welcomeSheet.rectTransform, 0f, 0.34f, 0.84f);
            UIMotion.Pop(welcomeTag.transform, 0.1f, 0.28f, 0.6f);
            var tiles = new Transform[welcomeDays.Length];
            for (var i = 0; i < tiles.Length; i++) tiles[i] = welcomeDays[i];
            UIMotion.Stagger(tiles, 0.12f, 0.06f);
            UIMotion.Pop(welcomeClaim.transform, 0.45f, 0.3f, 0.7f);
            Sfx.Play(Sound.CardsDealt);
        }

        private void CloseWelcomePopup()
        {
            HideScreen(welcomePopup, SyncAdNavigation);
        }

        private void BuildWelcomePopup()
        {
            Image backdrop;
            welcomePopup = UIFactory.Page(safeRoot, "WelcomePopup", new Color(0.005f, 0.01f, 0.025f, 0.9f), out backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, CloseWelcomePopup);

            var size = UIFactory.Pick(new Vector2(1260f, 760f), new Vector2(990f, 1000f));
            welcomeSheet = BuildCutSheet(welcomePopup, "Panel", size, WithAlpha(WelcomeGold, 0.8f));
            var panel = welcomeSheet.rectTransform;

            welcomeTag = CutTag(welcomePopup, "Tag", ArtLibrary.Art("PictoIcon_Gift_1"), WelcomeGold, new Vector2(460f, 52f));
            UIFactory.Place(welcomeTag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, size.y * 0.5f),
                new Vector2(460f, 52f));

            welcomeTitle = UIFactory.Label(panel, "Title", "", UIFactory.TextTitle, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(welcomeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f),
                new Vector2(size.x - 120f, 84f));
            welcomeTitle.characterSpacing = 10f;
            welcomeTitle.enableAutoSizing = true;
            welcomeTitle.fontSizeMin = 30f;
            welcomeTitle.fontSizeMax = UIFactory.TextTitle;
            UIFactory.AddUnderlay(welcomeTitle, 0.3f, 0.15f);
            UIFactory.GlowShadow(welcomeTitle, WithAlpha(WelcomeGold, 0.35f), 24f, 0.04f);

            welcomeSubtitle = UIFactory.Label(panel, "Subtitle", "", UIFactory.TextBody, new Color(0.84f, 0.91f, 0.94f),
                TextAlignmentOptions.Center);
            UIFactory.Place(welcomeSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(size.x - 140f, 64f));
            welcomeSubtitle.enableAutoSizing = true;
            welcomeSubtitle.fontSizeMin = 26f;
            welcomeSubtitle.fontSizeMax = UIFactory.TextBody;

            var tile = UIFactory.Pick(new Vector2(214f, 300f), new Vector2(170f, 340f));
            var gap = UIFactory.Pick(24f, 12f);
            var top = UIFactory.Pick(-226f, -232f);
            for (var i = 0; i < WelcomeLogin.Days; i++)
            {
                var day = BuildWelcomeDay(panel, i + 1, tile);
                UIFactory.Place(day, new Vector2(0.5f, 1f),
                    new Vector2((i - (WelcomeLogin.Days - 1) * 0.5f) * (tile.x + gap), top), tile);
                welcomeDays[i] = day;
            }

            welcomeNote = UIFactory.Label(panel, "Note", "", UIFactory.TextBody, WelcomeGold, TextAlignmentOptions.Center, true);
            UIFactory.Place(welcomeNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, UIFactory.Pick(178f, 196f)),
                new Vector2(size.x - 140f, 60f));
            welcomeNote.enableAutoSizing = true;
            welcomeNote.fontSizeMin = 26f;
            welcomeNote.fontSizeMax = UIFactory.TextBody;
            welcomeNote.characterSpacing = 2f;

            var claimSize = new Vector2(UIFactory.Pick(720f, 760f), 120f);
            welcomeClaim = CutSheetAction(panel, "Claim", ArtLibrary.Art("PictoIcon_Gift_1"), WithAlpha(WelcomeGold, 0.95f),
                WelcomeGoldFill, claimSize, Sound.None, true, 40);
            UIFactory.Place(welcomeClaim.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 44f), claimSize);
            UIFactory.GlowShadow(welcomeClaim.GetComponent<Image>(), WithAlpha(WelcomeGold, 0.35f), 26f, 0.08f);
            UIFactory.AddShine(welcomeClaim.GetComponent<RectTransform>());
            Rebind(welcomeClaim, ClaimWelcome);

            var close = UIFactory.RoundButton(panel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep, UIFactory.Touch, Sound.Back);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            close.transform.Find("Ring").GetComponent<Image>().color = WithAlpha(WelcomeGold, 0.85f);
            Rebind(close, CloseWelcomePopup);

            BuildWelcomeReveal();
            welcomePopup.gameObject.SetActive(false);
        }

        /// <summary>One day: cut-corner tile, "DAY n", the reward's icon and amount, a check once collected.</summary>
        private RectTransform BuildWelcomeDay(RectTransform parent, int day, Vector2 size)
        {
            var frame = CutFrameSprite;
            var border = UIFactory.Panel9(parent, "Day" + day, WithAlpha(UIFactory.Accent, 0.45f), frame);
            border.pixelsPerUnitMultiplier = 0.3f;
            border.raycastTarget = false;
            var rect = border.rectTransform;
            var inset = UIFactory.Panel9(rect, "Inset", UIFactory.ActionFillMuted, frame);
            UIFactory.Stretch(inset.rectTransform, 4f);
            inset.pixelsPerUnitMultiplier = 0.3f;
            inset.raycastTarget = false;
            ApplyKitFrame(border, inset, KitRole.Card);
            UIFactory.GlowShadow(border, WithAlpha(WelcomeGold, 0f), 26f, 0.06f);

            var header = UIFactory.Label(rect, "Header", "", 28, UIFactory.InkDim, TextAlignmentOptions.Center, true);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(size.x - 16f, 40f));
            header.characterSpacing = 4f;

            var kind = WelcomeLogin.KindOf(day);
            var art = kind == WelcomeLogin.RewardKind.Card ? ArtLibrary.IconDeck
                : kind == WelcomeLogin.RewardKind.ShipCredits ? ArtLibrary.Art("wire_ship") : ArtLibrary.IconCredits;
            var icon = UIFactory.Rect(rect, "Icon", kind == WelcomeLogin.RewardKind.Credits ? WelcomeGold : UIFactory.Ink, art);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSize = size.x * 0.5f;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(iconSize, iconSize));
            if (kind == WelcomeLogin.RewardKind.ShipCredits)
            {
                icon.color = WelcomeGold;
                var jewel = UIFactory.Rect(rect, "Jewel", WelcomeGold, ArtLibrary.IconCredits);
                jewel.preserveAspect = true;
                jewel.raycastTarget = false;
                UIFactory.Place(jewel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(iconSize * 0.32f, -iconSize * 0.18f),
                    new Vector2(iconSize * 0.42f, iconSize * 0.42f));
            }

            var amount = UIFactory.Label(rect, "Amount", "", 30, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(size.x - 12f, 44f));
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 22f;
            amount.fontSizeMax = 30f;

            var check = UIFactory.Rect(rect, "Check", UIFactory.Good, ArtLibrary.IconCheck);
            check.preserveAspect = true;
            check.raycastTarget = false;
            // Over the reward's icon, so the "DAY n" header stays readable.
            UIFactory.Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size.x * 0.22f, -size.x * 0.1f),
                new Vector2(58f, 58f));
            check.gameObject.SetActive(false);
            return rect;
        }

        /// <summary>A card won on the calendar, shown big over the sheet; any tap closes it.</summary>
        private void BuildWelcomeReveal()
        {
            Image veil;
            welcomeReveal = UIFactory.Page(welcomePopup, "Reveal", new Color(0.01f, 0.015f, 0.03f, 0.9f), out veil);
            var close = veil.gameObject.AddComponent<Button>();
            close.targetGraphic = veil;
            close.transition = Selectable.Transition.None;
            Rebind(close, () => HideScreen(welcomeReveal));

            var heading = UIFactory.Label(welcomeReveal, "Heading", "", 58, UIFactory.Evolved, TextAlignmentOptions.Center, true);
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(330f, 470f)),
                new Vector2(1000f, 80f));
            heading.characterSpacing = 10f;
            UIFactory.AddUnderlay(heading);
            UIFactory.GlowShadow(heading, WithAlpha(UIFactory.Evolved, 0.5f), 28f, 0.05f);

            welcomeRevealCard = UIFactory.NewRect(welcomeReveal, "Card");
            UIFactory.Place(welcomeRevealCard, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(DeckCardWidth, DeckCardHeight));

            var hint = UIFactory.Label(welcomeReveal, "Hint", "", UIFactory.TextBody, UIFactory.InkDim, TextAlignmentOptions.Center, true);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(-360f, -520f)),
                new Vector2(900f, 60f));
            hint.characterSpacing = 6f;
            welcomeReveal.gameObject.SetActive(false);
        }

        private void RefreshWelcome()
        {
            if (welcomePopup == null) return;
            var claimed = WelcomeLogin.Claimed;
            var next = WelcomeLogin.NextDay;
            var canClaim = WelcomeLogin.CanClaimToday;

            welcomeTitle.text = L("BÔNUS DE BOAS-VINDAS", "WELCOME BONUS", "BONO DE BIENVENIDA");
            welcomeSubtitle.text = L("Entre no jogo em 5 dias diferentes e ganhe uma recompensa por dia.",
                "Play on 5 different days and collect one reward each day.",
                "Juega 5 días distintos y recibe una recompensa cada día.");
            SetLabel(welcomeTag.transform, "Label", L("LOGIN DIÁRIO", "DAILY LOGIN", "INICIO DIARIO"));

            for (var i = 0; i < welcomeDays.Length; i++)
            {
                var day = i + 1;
                var tile = welcomeDays[i];
                var kind = WelcomeLogin.KindOf(day);
                SetLabel(tile, "Header", L("DIA ", "DAY ", "DÍA ") + day);
                SetLabel(tile, "Amount", kind == WelcomeLogin.RewardKind.Card
                    ? L("CARTA", "CARD", "CARTA")
                    : "+" + WelcomeLogin.CreditsFor(day));

                var done = day <= claimed;
                var today = !done && day == next && canClaim;
                var border = tile.GetComponent<Image>();
                border.color = done ? WithAlpha(UIFactory.Good, 0.7f)
                    : today ? WelcomeGold
                    : WithAlpha(UIFactory.Accent, 0.32f);
                tile.Find("Inset").GetComponent<Image>().color = today ? WelcomeGoldFill : UIFactory.ActionFillMuted;
                tile.Find("Check").gameObject.SetActive(done);
                UIFactory.Group(tile).alpha = done ? 0.6f : today ? 1f : 0.78f;
                var glow = tile.GetComponent<LeTai.TrueShadow.TrueShadow>();
                if (glow != null) glow.Color = WithAlpha(WelcomeGold, today ? 0.55f : 0f);
                var icon = tile.Find("Icon");
                LeanTween.cancel(icon.gameObject, false, TweenAction.SCALE);
                icon.localScale = Vector3.one;
                if (today)
                {
                    LeanTween.scale(icon.gameObject, Vector3.one * 1.1f, 0.6f).setEase(LeanTweenType.easeInOutSine)
                        .setLoopPingPong().setIgnoreTimeScale(true);
                }
            }

            string action;
            if (WelcomeLogin.Finished) action = L("BÔNUS CONCLUÍDO", "BONUS COMPLETE", "BONO COMPLETADO");
            else if (canClaim) action = L("RESGATAR DIA ", "CLAIM DAY ", "RECLAMAR DÍA ") + next;
            else action = L("VOLTE AMANHÃ", "COME BACK TOMORROW", "VUELVE MAÑANA");
            SetButtonLabel(welcomeClaim, action);
            welcomeClaim.interactable = canClaim;
            UIFactory.Group(welcomeClaim).alpha = canClaim ? 1f : 0.55f;

            welcomeNote.text = !string.IsNullOrEmpty(welcomeLastNote) ? welcomeLastNote
                : L("Dia 5: créditos para comprar uma nave nova!",
                    "Day 5: enough credits to buy a new ship!",
                    "Día 5: ¡créditos para comprar una nave nueva!");
        }

        private void ClaimWelcome()
        {
            WelcomeLogin.Result result;
            if (!WelcomeLogin.Claim(run, out result))
            {
                Sfx.Play(Sound.Denied);
                Juice.Punch(welcomeClaim.transform, 0.06f, 0.25f);
                return;
            }

            Sfx.Play(Sound.Purchase);
            Juice.ScreenFlash(new Color(1f, 0.85f, 0.45f, 0.3f), 0.45f);
            if (result.card != null)
            {
                welcomeLastNote = L("Nova carta no deck base: ", "New base deck card: ", "Nueva carta del mazo base: ") +
                    GameLocalization.Content(result.card.name);
            }
            else
            {
                welcomeLastNote = "+" + result.credits + L(" CRÉDITOS", " CREDITS", " CRÉDITOS") +
                    (result.kind == WelcomeLogin.RewardKind.ShipCredits
                        ? L("  ·  o valor de uma nave!", "  ·  a whole ship's worth!", "  ·  ¡el valor de una nave!")
                        : "");
            }
            RefreshWelcome();
            RefreshMainMenu();

            var tile = welcomeDays[Mathf.Clamp(result.day - 1, 0, welcomeDays.Length - 1)];
            UIMotion.Pop(tile, 0f, 0.4f, 0.7f);
            Juice.Punch(welcomeNote.transform, 0.2f, 0.35f);
            if (result.card != null) ShowWelcomeCard(result.card);
            else if (result.credits > 0)
            {
                // Credits: the coins burst out of the day's tile into the menu counter.
                FlyCreditsToMenu(tile, result.credits);
                ClosePopupAfterCoins(() => { if (WelcomePopupOpen) CloseWelcomePopup(); });
            }
        }

        private void ShowWelcomeCard(CardDefinition card)
        {
            for (var i = welcomeRevealCard.childCount - 1; i >= 0; i--)
                Destroy(welcomeRevealCard.GetChild(i).gameObject);
            var widget = CardWidget.Create(welcomeRevealCard, "NewCard", new Vector2(DeckCardWidth, DeckCardHeight));
            UIFactory.Place(widget.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DeckCardWidth, DeckCardHeight));
            widget.Bind(new RuntimeCard(-1, card, null), 0);
            widget.interactable = false;
            widget.SetRestScale(UIFactory.Pick(1.5f, 2f));

            SetLabel(welcomeReveal, "Heading", L("NOVA CARTA!", "NEW CARD!", "¡NUEVA CARTA!"));
            SetLabel(welcomeReveal, "Hint", L("TOQUE PARA CONTINUAR", "TAP TO CONTINUE", "TOCA PARA CONTINUAR"));
            welcomeReveal.gameObject.SetActive(true);
            welcomeReveal.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(welcomeReveal), 0.2f);
            Juice.PopIn(widget.root, widget.RestScale, 0.45f, 0.05f, true);
            Juice.PopIn(welcomeReveal.Find("Heading"), 1f, 0.35f, 0f, true);
            Sfx.Play(Sound.UpgradeChosen);
        }
    }
}
