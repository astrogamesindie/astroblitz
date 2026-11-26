using System;
using System.Globalization;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private AndroidAds ads;
        private Button reviveAdButton;
        private Button adPrivacyButton;
        private TMP_Text reviveAdStatus;
        private Button coinsAdButton;
        private TMP_Text coinsAdStatus;

        private void BuildAdControls()
        {
            if (ads != null) ads.Changed -= RefreshAdControls;
            ads = AndroidAds.EnsureCreated();
            if (ads == null) return;
            ads.Changed += RefreshAdControls;
            if (reviveAdButton != null) { RefreshAdControls(); return; }
            // The base end screen may be baked in the scene; reserve space without regenerating it.
            if (!UIFactory.Portrait)
            {
                endTitle.rectTransform.anchoredPosition = new Vector2(0f, 310f);
                endBody.rectTransform.anchoredPosition = new Vector2(0f, 100f);
            }
            againButton.GetComponent<RectTransform>().anchoredPosition = EndButtonPosition(-1, true);
            endMenuButton.GetComponent<RectTransform>().anchoredPosition = EndButtonPosition(1, true);
            reviveAdButton = UIFactory.Button(endScreen, "RewardedRevive", "", UIFactory.Primary,
                UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(reviveAdButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                EndWideButtonPosition, EndWideButtonSize);
            Rebind(reviveAdButton, WatchReviveAd);
            reviveAdStatus = UIFactory.Label(endScreen, "RewardedStatus", "", UIFactory.TextCaption,
                UIFactory.InkDim, TextAlignmentOptions.Center);
            UIFactory.Place(reviveAdStatus.rectTransform, new Vector2(0.5f, 0.5f),
                EndWideButtonPosition + new Vector2(0f, -70f), new Vector2(UIFactory.Pick(1300f, 920f), 48f));
            // The coins offer lives in the rewards sheet (GameUI.Rewards).
            BuildRewardsPanel();
            adPrivacyButton = UIFactory.Button(settingsPage, "AdPrivacy", "", UIFactory.PanelDeep,
                UIFactory.Ink, UIFactory.TextCaption, Sound.Tap);
            UIFactory.Place(adPrivacyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(720f, 104f));
            // Era um link só de texto (74 de altura); agora é uma ação com ícone de escudo.
            StyleCutAction(adPrivacyButton, CutFrameSprite, WithAlpha(UIFactory.Accent, 0.45f), UIFactory.ActionFillMuted);
            var privacyIcon = UIFactory.Rect(adPrivacyButton.transform, "Glyph", UIFactory.Ink, ArtLibrary.IconShield);
            UIFactory.Place(privacyIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(60f, 60f));
            privacyIcon.preserveAspect = true;
            privacyIcon.raycastTarget = false;
            var privacyCaption = adPrivacyButton.transform.Find("Caption") as RectTransform;
            if (privacyCaption != null) privacyCaption.offsetMin = new Vector2(96f, privacyCaption.offsetMin.y);
            Rebind(adPrivacyButton, () => ads.ShowPrivacyOptions());
            RefreshAdControls();
        }

        /// <summary>
        /// Premium (the one-time store product) removes every ad and hands out
        /// the video rewards without a video: the revive, the coins and the daily x2.
        /// Limits stay the same (one revive per run, the coins cooldown).
        /// </summary>
        private static bool PremiumRewards
        {
            get
            {
                var store = AndroidPurchases.Instance;
                return store != null && store.AdsRemoved &&
                    (StoreUpdateGate.Instance == null || !StoreUpdateGate.Instance.BlocksPlay);
            }
        }

        private void WatchReviveAd()
        {
            if (run == null || !run.CanRevive) return;
            if (PremiumRewards) { run.ReviveFromReward(); return; }
            if (ads == null) return;
            if (!ads.CanReward) { ads.RetryLoading(); return; }
            var expectedShip = run.ship;
            ads.ShowRewarded(() => { if (run != null) run.ReviveFromReward(); },
                () => this != null && run != null && run.CanRevive && run.ship == expectedShip);
        }

        private void AfterResultAd(System.Action navigation)
        {
            var expected = run;
            var phase = run.phase;
            System.Action proceed = () =>
            {
                if (this != null && run == expected && run.phase == phase) navigation();
            };
            if (ads != null) ads.ShowInterstitialThen(proceed);
            else proceed();
        }

        // The coins offer comes back 5 minutes after it was used. The moment it
        // returns is saved (UTC), so closing the game does not reset it.
        private const string CoinsAdReadyKey = "AstroCrawler.CoinsAdReadyAtUtc";
        private const double CoinsAdCooldownSeconds = 300.0;
        private float nextCoinsAdCheck;
        private bool coinsAdWasCooling;

        /// <summary>Seconds until the coins offer shows again; 0 when it is available.</summary>
        private static double CoinsAdSecondsLeft()
        {
            long ticks;
            var saved = PlayerPrefs.GetString(CoinsAdReadyKey, "");
            if (!long.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return 0.0;
            var left = (new DateTime(ticks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
            // A clock moved backwards cannot stretch the wait past one cooldown.
            return Math.Max(0.0, Math.Min(left, CoinsAdCooldownSeconds));
        }

        private static void StartCoinsAdCooldown()
        {
            var readyAt = DateTime.UtcNow.AddSeconds(CoinsAdCooldownSeconds);
            PlayerPrefs.SetString(CoinsAdReadyKey, readyAt.Ticks.ToString(CultureInfo.InvariantCulture));
            ProgressSnapshot.SaveLocal();
        }

        /// <summary>Called from Update: brings the button back when the wait ends.</summary>
        private void TickCoinsAd()
        {
            if (Time.unscaledTime < nextCoinsAdCheck) return;
            nextCoinsAdCheck = Time.unscaledTime + 1f;
            var cooling = CoinsAdSecondsLeft() > 0.0;
            if (cooling == coinsAdWasCooling) return;
            coinsAdWasCooling = cooling;
            RefreshAdControls();
        }

        private bool CanEarnCoins()
        {
            return this != null && run != null && run.phase == RunPhase.ShipSelection &&
                RewardsPopupOpen && !StorePopupOpen && CoinsAdSecondsLeft() <= 0.0;
        }

        private void WatchCoinsAd()
        {
            if (ads == null || !CanEarnCoins()) return;
            var coins = ads.RewardCoins;
            if (PremiumRewards) { GrantRewardCoins(coins); return; }
            if (!ads.CanReward) { ads.RetryLoading(); return; }
            ads.ShowRewarded(() => GrantRewardCoins(coins), CanEarnCoins);
        }

        private void GrantRewardCoins(int coins)
        {
            run.passiveUpgrades.AddRewardedCredits(coins);
            StartCoinsAdCooldown();
            coinsAdWasCooling = true;
            RefreshPassiveScreen();
            RefreshMainMenu();
            RefreshRewards();
            Sfx.Play(Sound.Purchase);
            FlyCreditsToMenu(coinsAdButton != null ? coinsAdButton.transform : mainMenu, coins);
            ClosePopupAfterCoins(() => { if (RewardsPopupOpen) CloseRewardsPopup(); });
        }

        /// <summary>
        /// The footer banner shows on the main menu, the hangar, the upgrade
        /// tree, the stage select, the base deck, a ship's deck and the
        /// settings (each reserves its height) - not over anything in a run.
        /// </summary>
        private void SyncAdNavigation()
        {
            if (ads == null) return;
            var onBannerPage = Active(mainMenu) || Active(shipScreen) || Active(levelsPage) || Active(baseDeckPage) ||
                Active(deckScreen) || Active(settingsPage) || Active(modesPage) || Active(rankingPage);
            ads.SetMenuVisible(onBannerPage && !IntroScreenOpen && !ReleaseNotesState.Pending && run != null && run.phase == RunPhase.ShipSelection);
            RefreshAdControls();
        }

        private static void ReserveFooter(RectTransform page, float units)
        {
            if (page != null) page.offsetMin = new Vector2(page.offsetMin.x, units);
        }

        private static bool Active(RectTransform page)
        {
            if (page == null || !page.gameObject.activeInHierarchy) return false;
            // A page playing its exit (UIMotion) has already let go of input: it is on its way out.
            var group = page.GetComponent<CanvasGroup>();
            return group == null || group.blocksRaycasts;
        }

        private void RefreshAdControls()
        {
            if (ads == null) return;
            var premium = PremiumRewards;
            var eligible = run != null && run.CanRevive && (premium || ads.Supported && AdsPolicy.CanShowAds);
            if (reviveAdButton != null)
            {
                reviveAdButton.gameObject.SetActive(eligible);
                reviveAdButton.interactable = eligible && !ads.Showing;
                SetButtonLabel(reviveAdButton, premium ? L("Reviver", "Revive", "Revivir").ToUpperInvariant() : ads.CanReward ?
                    L("VER ANÚNCIO PARA RENASCER", "WATCH AD TO REVIVE", "VER ANUNCIO PARA REVIVIR") :
                    L("TENTAR CARREGAR ANÚNCIO", "RETRY LOADING AD", "REINTENTAR CARGAR ANUNCIO"));
            }
            if (reviveAdStatus != null)
            {
                reviveAdStatus.gameObject.SetActive(eligible);
                reviveAdStatus.text = premium ? "PREMIUM" : ads.Showing ? L("Aguardando conclusão do anúncio…", "Waiting for ad completion…", "Esperando el anuncio…") :
                    ads.RewardReady ? L("Uma vez por partida. A recompensa exige concluir o anúncio.", "Once per run. Complete the ad to earn the reward.", "Una vez por partida. Completa el anuncio para recibir la recompensa.") :
                    L("Anúncio indisponível. Aguarde o carregamento ou tente mais tarde.", "Ad unavailable. Wait for loading or try later.", "Anuncio no disponible. Espera o inténtalo más tarde.");
            }
            if (againButton != null) againButton.interactable = !ads.Showing;
            if (endMenuButton != null) endMenuButton.interactable = !ads.Showing;
            if (coinsAdButton != null)
            {
                // Inside the rewards sheet: always shown, dimmed with a countdown
                // during the 5-minute wait.
                var wait = CoinsAdSecondsLeft();
                coinsAdButton.interactable = !ads.Showing && CanEarnCoins();
                UIFactory.Group(coinsAdButton).alpha = wait > 0.0 ? 0.45f : 1f;
                SetButtonLabel(coinsAdButton, "+" + ads.RewardCoins);
                // Play when an ad is ready; the retry arrow while one loads (a tap retries).
                var icon = coinsAdButton.transform.Find("Icon");
                if (icon != null) icon.GetComponent<Image>().sprite = premium ? ArtLibrary.IconPremium :
                    ads.CanReward ? ArtLibrary.IconPlay : ArtLibrary.IconRetry;
                if (wait > 0.0)
                {
                    SetTimerText(coinsAdStatus, (int)(wait / 60.0) + ":" + ((int)wait % 60).ToString("00"));
                }
                else
                {
                    HideTimerClock(coinsAdStatus);
                    coinsAdStatus.text = premium ? "" : ads.Showing ? L("Aguardando anúncio…", "Waiting for ad…", "Esperando anuncio…") :
                        ads.RewardReady ? "" : L("Anúncio indisponível", "Ad unavailable", "Anuncio no disponible");
                }
            }
            if (adPrivacyButton != null)
            {
                adPrivacyButton.gameObject.SetActive(ads.Supported && ads.PrivacyRequired);
                adPrivacyButton.interactable = !ads.Showing;
                SetButtonLabel(adPrivacyButton, L("PRIVACIDADE DOS ANÚNCIOS", "AD PRIVACY OPTIONS", "PRIVACIDAD DE ANUNCIOS"));
            }
            // The safe area as SafeArea applied it (Screen.safeArea can be stale
            // right after a rotation, or the Device Simulator's in the editor).
            var safeBottom = safeRoot != null ? safeRoot.anchorMin.y * Screen.height : 0f;
            var safeHeight = safeRoot != null ? (safeRoot.anchorMax.y - safeRoot.anchorMin.y) * Screen.height : 0f;
            if (safeRoot != null && safeHeight > 0f)
            {
                // Reserve the actual native banner height within the safe area,
                // on every page the banner sits under.
                var pixels = Mathf.Max(0f, ads.BannerHeightPixels - safeBottom);
                var units = pixels * safeRoot.rect.height / safeHeight;
                ReserveFooter(mainMenu, units);
                ReserveFooter(shipScreen, units);
                ReserveFooter(levelsPage, units);
                ReserveFooter(baseDeckPage, units);
                ReserveFooter(deckScreen, units);
                ReserveFooter(settingsPage, units);
                ReserveFooter(modesPage, units);
                ReserveFooter(rankingPage, units);
                // The hangar card is sized to the page height, which just changed.
                FitHangarCard();
            }
        }
    }
}
