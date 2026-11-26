// AstroCrawler - the rewards sheet on the main menu.
//
// One gift button in the top-right corner opens it. Two offers:
//   - a video for +100 credits (rewarded ad; back 5 minutes after it pays out);
//   - a daily reward, rolled once per day between 50 and 400 credits, taken as
//     it is or doubled by watching the rewarded video.
// A dot on the gift button says something is waiting.

using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const string DailyDayKey = "AstroCrawler.DailyReward.Day";
        private const string DailyAmountKey = "AstroCrawler.DailyReward.Amount";
        private const string DailyClaimedKey = "AstroCrawler.DailyReward.ClaimedDay";
        private const int DailyMin = 50;
        private const int DailyMax = 400;

        private Button rewardsButton;
        private Image rewardsDot;
        private RectTransform rewardsPopup;
        private RectTransform rewardsPanel;
        private TMP_Text dailyAmountLabel;
        private TMP_Text dailyStatus;
        private Button dailyClaimButton;
        private Button dailyDoubleButton;
        private float nextRewardsRefresh;
        private static Color RewardBorder => WithAlpha(UIFactory.Evolved, 0.7f);
        private static Color RewardFill => Darken(UIFactory.Evolved, 0.18f);
        private static Color RewardFillStrong => Darken(UIFactory.Evolved, 0.28f);
        private static Color RewardFillMuted => Darken(UIFactory.Evolved, 0.08f);

        // ------------------------------------------------------------------
        // Daily reward state
        // ------------------------------------------------------------------

        /// <summary>Today as a day number in the player's local calendar.</summary>
        private static int Today
        {
            get { return (int)(DateTime.Now.Date - new DateTime(2000, 1, 1)).TotalDays; }
        }

        /// <summary>Today's amount, rolled the first time it is asked for and kept for the day.</summary>
        private static int DailyAmount()
        {
            var today = Today;
            if (PlayerPrefs.GetInt(DailyDayKey, -1) != today)
            {
                // Multiples of 10 between 50 and 400.
                var amount = UnityEngine.Random.Range(DailyMin / 10, DailyMax / 10 + 1) * 10;
                PlayerPrefs.SetInt(DailyDayKey, today);
                PlayerPrefs.SetInt(DailyAmountKey, amount);
                ProgressSnapshot.SaveLocal();
            }
            return Mathf.Clamp(PlayerPrefs.GetInt(DailyAmountKey, DailyMin), DailyMin, DailyMax);
        }

        private static bool DailyClaimed
        {
            get { return PlayerPrefs.GetInt(DailyClaimedKey, -1) == Today; }
        }

        private bool RewardsPopupOpen
        {
            get { return rewardsPopup != null && rewardsPopup.gameObject.activeSelf; }
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildRewardsPanel()
        {
            if (rewardsPopup != null || mainMenu == null) return;

            // The gift button, top right (the credits chip is centred).
            rewardsButton = UIFactory.RoundButton(mainMenu, "Rewards", ArtLibrary.Art("PictoIcon_Gift_1"),
                UIFactory.PanelSoft, UIFactory.Touch, Sound.Navigate);
            var corner = rewardsButton.GetComponent<RectTransform>();
            UIFactory.Place(corner, new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(UIFactory.Touch, UIFactory.Touch));
            UIFactory.GlowShadow(rewardsButton.GetComponent<Image>(), WithAlpha(UIFactory.Evolved, 0.35f), 22f, 0.06f);
            corner.Find("Ring").GetComponent<Image>().color = WithAlpha(UIFactory.Evolved, 0.8f);
            var gift = corner.Find("Icon");
            if (gift != null) gift.GetComponent<Image>().color = UIFactory.Evolved;
            rewardsDot = UIFactory.Rect(corner, "Dot", UIFactory.Danger, ArtLibrary.CircleFilled);
            UIFactory.Place(rewardsDot.rectTransform, new Vector2(1f, 1f), new Vector2(4f, 4f), new Vector2(34f, 34f));
            UIFactory.GlowShadow(rewardsDot, WithAlpha(UIFactory.Danger, 0.7f), 14f, 0.05f);
            Rebind(rewardsButton, OpenRewardsPopup);

            // The sheet.
            Image backdrop;
            rewardsPopup = UIFactory.Page(safeRoot, "RewardsPopup", new Color(0.01f, 0.015f, 0.03f, 0.85f), out backdrop);
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            Rebind(dismiss, CloseRewardsPopup);

            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var panel = UIFactory.Panel9(rewardsPopup, "Panel", RewardBorder, cutFrame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            rewardsPanel = panel.rectTransform;
            panel.raycastTarget = true;
            UIFactory.Place(rewardsPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 980f));
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            var surface = UIFactory.Panel9(rewardsPanel, "Surface", RewardFillMuted, cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            ApplyKitFrame(panel, surface, KitRole.Painel);
            var rule = UIFactory.Rect(rewardsPanel, "TopRule", RewardBorder, ArtLibrary.Pixel);
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            rule.raycastTarget = false;

            var title = UIFactory.Label(rewardsPanel, "Title", "", UIFactory.TextHeading, UIFactory.Evolved,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 64f));
            title.characterSpacing = 3f;

            BuildDailySection();
            BuildVideoSection();

            var close = UIFactory.RoundButton(rewardsPanel, "Close", ArtLibrary.IconCancel, UIFactory.PanelDeep,
                UIFactory.Touch, Sound.Back);
            close.GetComponent<Image>().color = RewardFillMuted;
            close.transform.Find("Ring").GetComponent<Image>().color = RewardBorder;
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(UIFactory.Touch, UIFactory.Touch));
            Rebind(close, CloseRewardsPopup);

            rewardsPopup.gameObject.SetActive(false);
            RefreshRewards();
        }

        /// <summary>A lit box inside the sheet, holding one offer.</summary>
        private RectTransform RewardBox(string name, float top, float height, Color accent)
        {
            var cutFrame = ArtLibrary.Ui("Cut Frame Filled Big (300ppu)");
            var box = UIFactory.Panel9(rewardsPanel, name, WithAlpha(UIFactory.Evolved, 0.45f), cutFrame);
            box.pixelsPerUnitMultiplier = 0.3f;
            box.raycastTarget = false;
            var rect = box.rectTransform;
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(840f, height));
            var inset = UIFactory.Panel9(rect, "Inset", RewardFill, cutFrame);
            UIFactory.Stretch(inset.rectTransform, 4f);
            inset.pixelsPerUnitMultiplier = 0.3f;
            inset.raycastTarget = false;
            ApplyKitFrame(box, inset, KitRole.Card);
            var stripe = UIFactory.Rect(rect, "Accent", WithAlpha(accent, 0.7f), ArtLibrary.Pixel);
            UIFactory.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(180f, 3f));
            stripe.raycastTarget = false;
            return rect;
        }

        private void BuildDailySection()
        {
            var box = RewardBox("Daily", -120f, 470f, UIFactory.Evolved);

            var caption = UIFactory.Label(box, "Caption", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(780f, 40f));
            caption.characterSpacing = 3f;

            var gift = UIFactory.Rect(box, "Gift", UIFactory.Evolved, ArtLibrary.Art("PictoIcon_Gift_1"));
            gift.preserveAspect = true;
            UIFactory.Place(gift.rectTransform, new Vector2(0.5f, 1f), new Vector2(-210f, -78f), new Vector2(144f, 144f));
            gift.raycastTarget = false;

            dailyAmountLabel = UIFactory.Label(box, "Amount", "", 88, UIFactory.Evolved, TextAlignmentOptions.Center, true);
            UIFactory.Place(dailyAmountLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(10f, -92f), new Vector2(280f, 110f));
            UIFactory.AddUnderlay(dailyAmountLabel);
            var jewel = UIFactory.Rect(box, "Jewel", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            UIFactory.Place(jewel.rectTransform, new Vector2(0.5f, 1f), new Vector2(215f, -116f), new Vector2(64f, 64f));
            jewel.raycastTarget = false;

            dailyClaimButton = UIFactory.Button(box, "Claim", "", UIFactory.PanelSoft, UIFactory.Ink,
                UIFactory.TextButton, Sound.None);
            UIFactory.Place(dailyClaimButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(-205f, 92f), new Vector2(370f, 120f));
            Rebind(dailyClaimButton, () => ClaimDaily(1));
            StyleCutAction(dailyClaimButton, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), RewardBorder, RewardFillStrong);

            // Doubled by the rewarded video: play icon and "x2".
            dailyDoubleButton = UIFactory.Button(box, "Double", null, UIFactory.Primary, UIFactory.Ink,
                UIFactory.TextButton, Sound.Tap);
            var doubleRect = dailyDoubleButton.GetComponent<RectTransform>();
            UIFactory.Place(doubleRect, new Vector2(0.5f, 0f), new Vector2(205f, 92f), new Vector2(370f, 120f));
            StyleCutAction(dailyDoubleButton, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), RewardBorder, RewardFill);
            var play = UIFactory.Rect(doubleRect, "Icon", UIFactory.Ink, ArtLibrary.IconPlay);
            play.preserveAspect = true;
            UIFactory.Place(play.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(50f, 50f));
            var doubleCaption = UIFactory.Label(doubleRect, "Caption", "", 40, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(doubleCaption.rectTransform);
            doubleCaption.rectTransform.offsetMin = new Vector2(80f, 0f);
            UIFactory.AddUnderlay(doubleCaption);
            Rebind(dailyDoubleButton, WatchDailyDouble);

            dailyStatus = UIFactory.Label(box, "Status", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.Center);
            UIFactory.Place(dailyStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(780f, 48f));
        }

        private void BuildVideoSection()
        {
            var box = RewardBox("Video", -620f, 330f, UIFactory.Evolved);

            var caption = UIFactory.Label(box, "Caption", "", UIFactory.TextBody, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(780f, 40f));
            caption.characterSpacing = 3f;

            // The rewarded-coins button (also driven by GameUI.Ads: label,
            // icon, availability).
            coinsAdButton = UIFactory.Button(box, "RewardedCoins", null, UIFactory.Primary,
                UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            var coins = coinsAdButton.GetComponent<RectTransform>();
            UIFactory.Place(coins, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(520f, 130f));
            StyleCutAction(coinsAdButton, ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"), RewardBorder, RewardFillStrong);
            var play = UIFactory.Rect(coins, "Icon", UIFactory.Ink, ArtLibrary.IconPlay);
            play.preserveAspect = true;
            UIFactory.Place(play.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(60f, 60f));
            var amount = UIFactory.Label(coins, "Caption", "", 56, UIFactory.Ink, TextAlignmentOptions.Center, true);
            UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 80f));
            UIFactory.AddUnderlay(amount);
            var jewel = UIFactory.Rect(coins, "Credits", UIFactory.Evolved, ArtLibrary.IconCredits);
            jewel.preserveAspect = true;
            UIFactory.Place(jewel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(60f, 60f));
            Rebind(coinsAdButton, WatchCoinsAd);

            coinsAdStatus = UIFactory.Label(box, "Status", "", UIFactory.TextCaption, UIFactory.InkDim,
                TextAlignmentOptions.Center);
            UIFactory.Place(coinsAdStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(780f, 48f));
        }

        // ------------------------------------------------------------------
        // Behaviour
        // ------------------------------------------------------------------

        private void OpenRewardsPopup()
        {
            if (rewardsPopup == null) return;
            RefreshRewards();
            rewardsPopup.gameObject.SetActive(true);
            rewardsPopup.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(rewardsPopup), 0.18f);
            Juice.PopIn(rewardsPanel, 1f, 0.28f, 0f, true);
            RefreshAdControls();
        }

        private void CloseRewardsPopup()
        {
            HideScreen(rewardsPopup, RefreshAdControls);
            RefreshAdControls();
        }

        private void ClaimDaily(int multiplier)
        {
            if (DailyClaimed) { Sfx.Play(Sound.Denied); return; }
            var amount = DailyAmount() * multiplier;
            PlayerPrefs.SetInt(DailyClaimedKey, Today);
            run.passiveUpgrades.AddRewardedCredits(amount);
            ProgressSnapshot.SaveLocal();
            Sfx.Play(Sound.Purchase);
            RefreshMainMenu();
            RefreshRewards();
            Juice.Punch(dailyAmountLabel.transform, 0.25f, 0.35f);
            // The coins burst out of the amount and fly into the menu counter
            // (which counts up and punches as they land); the sheet steps aside.
            FlyCreditsToMenu(dailyAmountLabel.transform, amount);
            ClosePopupAfterCoins(() => { if (RewardsPopupOpen) CloseRewardsPopup(); });
        }

        private bool CanDoubleDaily()
        {
            return this != null && run != null && run.phase == RunPhase.ShipSelection &&
                RewardsPopupOpen && !DailyClaimed;
        }

        private void WatchDailyDouble()
        {
            if (!CanDoubleDaily()) return;
            if (PremiumRewards) { ClaimDaily(2); return; }
            if (ads == null) return;
            if (!ads.CanReward) { ads.RetryLoading(); return; }
            ads.ShowRewarded(() => ClaimDaily(2), CanDoubleDaily);
        }

        /// <summary>Texts, buttons and the waiting dot; cheap, run on open and once a second.</summary>
        private void RefreshRewards()
        {
            if (rewardsPopup == null) return;
            var claimed = DailyClaimed;
            var amount = DailyAmount();
            // Premium keeps both rewards, without the video.
            var premium = PremiumRewards;
            var adsOn = premium || ads != null && ads.Supported && AdsPolicy.CanShowAds;

            SetLabel(rewardsPanel, "Title", L("RECOMPENSAS", "REWARDS", "RECOMPENSAS"));
            SetLabel(rewardsPanel, "Daily/Caption", L("RECOMPENSA DIÁRIA", "DAILY REWARD", "RECOMPENSA DIARIA"));
            SetLabel(rewardsPanel, "Video/Caption", premium ? "PREMIUM" : L("ASSISTA E GANHE", "WATCH AND EARN", "MIRA Y GANA"));

            dailyAmountLabel.text = "+" + amount;
            dailyAmountLabel.color = claimed ? UIFactory.InkDim : UIFactory.Evolved;
            SetButtonLabel(dailyClaimButton, claimed ? L("COLETADA", "CLAIMED", "RECOGIDA") : L("COLETAR", "CLAIM", "RECOGER"));
            dailyClaimButton.interactable = !claimed;
            TintRewardAction(dailyClaimButton, !claimed, true);
            dailyDoubleButton.gameObject.SetActive(adsOn && !claimed);
            dailyDoubleButton.interactable = adsOn && (ads == null || !ads.Showing);
            TintRewardAction(dailyDoubleButton, dailyDoubleButton.interactable, false);
            SetButtonLabel(dailyDoubleButton, "x2  ·  +" + amount * 2);
            var doubleIcon = dailyDoubleButton.transform.Find("Icon");
            if (doubleIcon != null && (premium || ads != null))
                doubleIcon.GetComponent<Image>().sprite = premium ? ArtLibrary.IconPremium :
                    ads.CanReward ? ArtLibrary.IconPlay : ArtLibrary.IconRetry;

            // The claim button sits in the middle when there is no video to double it.
            var claimRect = dailyClaimButton.GetComponent<RectTransform>();
            claimRect.anchoredPosition = new Vector2(adsOn && !claimed ? -205f : 0f, claimRect.anchoredPosition.y);

            if (claimed)
            {
                var untilMidnight = DateTime.Now.Date.AddDays(1) - DateTime.Now;
                SetTimerText(dailyStatus, (int)untilMidnight.TotalHours + "h " + untilMidnight.Minutes.ToString("00") + "m");
            }
            else
            {
                HideTimerClock(dailyStatus);
                dailyStatus.text = adsOn && !premium
                    ? L("Assista um vídeo para dobrar.", "Watch a video to double it.", "Mira un video para duplicarla.")
                    : "";
            }

            var videoBox = rewardsPanel.Find("Video");
            if (videoBox != null) videoBox.gameObject.SetActive(adsOn);
            rewardsPanel.sizeDelta = new Vector2(920f, adsOn ? 980f : 620f);

            // Something to collect: the daily reward, or the video when it is back.
            if (rewardsDot != null)
                rewardsDot.gameObject.SetActive(!claimed || adsOn && CoinsAdSecondsLeft() <= 0.0);
        }

        private static void TintRewardAction(Button button, bool active, bool primary)
        {
            button.GetComponent<Image>().color = WithAlpha(UIFactory.Evolved, active ? 0.7f : 0.3f);
            button.transform.Find("Inset").GetComponent<Image>().color = active
                ? primary ? RewardFillStrong : RewardFill
                : RewardFillMuted;
        }

        /// <summary>Called from Update: keeps the countdowns and the dot current.</summary>
        private void TickRewards()
        {
            if (Time.unscaledTime < nextRewardsRefresh) return;
            nextRewardsRefresh = Time.unscaledTime + 1f;
            if (mainMenu == null || !mainMenu.gameObject.activeInHierarchy) return;
            RefreshRewards();
            if (RewardsPopupOpen) RefreshAdControls();
        }
    }
}
