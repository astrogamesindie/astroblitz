#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
using System;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace AstroCrawler
{
    // Kept under the existing component name to preserve scene/prefab references.
    public sealed partial class AndroidAds
    {
        private BannerView googleBanner;
        private RewardedAd googleReward;
        private InterstitialAd googleInterstitial;
        private AppOpenAd googleOpen;
        private SynchronizationContext unityContext;
        private bool googleDisposed, googleInitialized, googleInitializing, privacyBusy, consentStarted, googleConsentAllows;
        private bool loadingReward, loadingInterstitial, loadingOpen;
        private int googleGeneration, googleFullScreenSequence;
        private float rewardLoaded, interstitialLoaded, openLoaded, nextGoogleTick;
        private float rewardRetry, interstitialRetry, openRetry, consentRetry, bannerRetry;
        private float lastFullScreen, backgroundAt = -1f, timeBeforeGoogleAd;
        private bool pendingGoogleClose, pendingGoogleReward;
        private Action afterGoogleAd, destroyVisibleAd;
        private bool RewardFresh => googleReward != null && Time.realtimeSinceStartup - rewardLoaded < 3500f;

        private void PrepareGoogleClient()
        {
            unityContext = SynchronizationContext.Current;
            lastFullScreen = Time.realtimeSinceStartup;
#if UNITY_EDITOR
            // The official preview keeps this static helper when Domain Reload is disabled.
            var helper = typeof(GoogleMobileAds.Unity.BaseAdClient).GetField("AdBehaviour",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (helper != null && (UnityEngine.Object)helper.GetValue(null) == null)
                helper.SetValue(null, new GameObject("GoogleMobileAds.Editor").AddComponent<AdBehaviour>());
#endif
        }

        private void Dispatch(Action action, Action whenDisposed = null)
        {
            unityContext.Post(_ =>
            {
                if (this == null || googleDisposed) { whenDisposed?.Invoke(); return; }
                action();
            }, null);
        }

        private void GoogleCommand(string method, object[] args)
        {
            if (!Supported || googleDisposed) return;
            switch (method)
            {
                case "setEnabled":
                    if (!enabledAds) ClearGoogleCache();
                    else GatherGoogleConsent();
                    break;
                case "setMenuVisible":
                    if (!menuVisible) DestroyGoogleBanner();
                    else ReconcileGoogleAds();
                    break;
                case "refreshLayout": DestroyGoogleBanner(); ReconcileGoogleAds(); break;
                case "showReward": ShowGoogleReward(); break;
                case "retry":
                    rewardRetry = interstitialRetry = openRetry = bannerRetry = consentRetry = 0f;
                    if (!googleConsentAllows) consentStarted = false;
                    GatherGoogleConsent();
                    break;
                case "privacyOptions":
                    if (!PrivacyRequired || privacyBusy || Showing) break;
                    privacyBusy = true;
                    ClearGoogleCache();
                    ConsentForm.ShowPrivacyOptionsForm(error => Dispatch(() =>
                    {
                        privacyBusy = false;
                        if (error != null) GoogleError(error.Message);
                        RefreshGooglePrivacy();
                        InitializeGoogleAds();
                    }));
                    break;
            }
        }

        private void GatherGoogleConsent()
        {
            if (ReleaseNotesState.Pending) return;
            if (!enabledAds || privacyBusy || googleDisposed || Time.realtimeSinceStartup < consentRetry) return;
            if (consentStarted) { InitializeGoogleAds(); return; }
            consentStarted = privacyBusy = true;
            ConsentInformation.Update(new ConsentRequestParameters(), error => Dispatch(() =>
            {
                if (error != null)
                {
                    privacyBusy = false;
                    consentStarted = false;
                    consentRetry = Time.realtimeSinceStartup + 60f;
                    GoogleError(error.Message);
                    RefreshGooglePrivacy();
                    InitializeGoogleAds(); // Only if UMP has a previously valid decision.
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Dispatch(() =>
                {
                    privacyBusy = false;
                    if (formError != null)
                    {
                        consentStarted = false;
                        consentRetry = Time.realtimeSinceStartup + 60f;
                        GoogleError(formError.Message);
                    }
                    RefreshGooglePrivacy();
                    InitializeGoogleAds();
                }));
            }));
        }

        private void RefreshGooglePrivacy()
        {
            googleConsentAllows = ConsentInformation.CanRequestAds();
            PrivacyRequired = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
            Changed?.Invoke();
        }

        private void InitializeGoogleAds()
        {
            if (!enabledAds || privacyBusy || !googleConsentAllows) return;
            if (googleInitialized) { ReconcileGoogleAds(); return; }
            if (googleInitializing) return;
            googleInitializing = true;
            MobileAds.Initialize(status => Dispatch(() =>
            {
                googleInitializing = false;
                googleInitialized = status != null;
                if (googleInitialized) ReconcileGoogleAds();
                else { consentRetry = Time.realtimeSinceStartup + 60f; GoogleError("SDK initialization failed"); }
            }));
        }

        private bool GoogleAllowed => enabledAds && !ReleaseNotesState.Pending && !googleDisposed && !privacyBusy &&
            googleInitialized && googleConsentAllows && AdsPolicy.CanShowAds;

        private void Update()
        {
            if (!enabledAds || googleDisposed || Showing || Time.realtimeSinceStartup < nextGoogleTick) return;
            nextGoogleTick = Time.realtimeSinceStartup + 1f;
            if (!googleInitialized || !consentStarted) GatherGoogleConsent();
            else ReconcileGoogleAds();
        }

        private void ReconcileGoogleAds()
        {
            if (!GoogleAllowed || Showing) return;
            var now = Time.realtimeSinceStartup;
            if (menuVisible && googleBanner == null && now >= bannerRetry) CreateGoogleBanner();
            if (googleReward != null && !RewardFresh) { googleReward.Destroy(); googleReward = null; RewardReady = false; }
            if (googleInterstitial != null && now - interstitialLoaded >= 3500f)
            { googleInterstitial.Destroy(); googleInterstitial = null; }
            if (googleOpen != null && now - openLoaded >= 14400f) { googleOpen.Destroy(); googleOpen = null; }
            var generation = googleGeneration;
            if (googleReward == null && !loadingReward && now >= rewardRetry)
            {
                loadingReward = true;
                RewardedAd.Load(RewardUnitId, new AdRequest(), (ad, error) => Dispatch(() =>
                {
                    if (generation != googleGeneration || !GoogleAllowed) { ad?.Destroy(); return; }
                    loadingReward = false;
                    if (error != null || ad == null) { rewardRetry = Time.realtimeSinceStartup + 60f; GoogleError(error?.ToString()); return; }
                    googleReward = ad; rewardLoaded = Time.realtimeSinceStartup;
                    RewardReady = ad.CanShowAd(); Status = "ready"; LastError = ""; Changed?.Invoke();
                }, () => ad?.Destroy()));
            }
            if (googleInterstitial == null && !loadingInterstitial && now >= interstitialRetry)
            {
                loadingInterstitial = true;
                InterstitialAd.Load(InterstitialUnitId, new AdRequest(), (ad, error) => Dispatch(() =>
                {
                    if (generation != googleGeneration || !GoogleAllowed) { ad?.Destroy(); return; }
                    loadingInterstitial = false;
                    if (error != null || ad == null) { interstitialRetry = Time.realtimeSinceStartup + 60f; GoogleError(error?.ToString()); return; }
                    googleInterstitial = ad; interstitialLoaded = Time.realtimeSinceStartup;
                }, () => ad?.Destroy()));
            }
            if (googleOpen == null && !loadingOpen && now >= openRetry)
            {
                loadingOpen = true;
                AppOpenAd.Load(AppOpenUnitId, new AdRequest(), (ad, error) => Dispatch(() =>
                {
                    if (generation != googleGeneration || !GoogleAllowed) { ad?.Destroy(); return; }
                    loadingOpen = false;
                    if (error != null || ad == null) { openRetry = Time.realtimeSinceStartup + 60f; GoogleError(error?.ToString()); return; }
                    googleOpen = ad; openLoaded = Time.realtimeSinceStartup;
                }, () => ad?.Destroy()));
            }
        }

        private void CreateGoogleBanner()
        {
            var size = AdSize.GetCurrentOrientationLargeAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            var banner = new BannerView(BannerUnitId, size, AdPosition.Bottom);
            googleBanner = banner;
            banner.OnBannerAdLoaded += () => Dispatch(() =>
            {
                if (googleBanner != banner || !GoogleAllowed || !menuVisible) return;
                BannerHeightPixels = Mathf.RoundToInt(banner.GetHeightInPixels());
#if UNITY_EDITOR
                // Google's preview canvas can scale differently from its reported ad pixels.
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.transform.parent != null || !canvas.name.EndsWith("(Clone)")) continue;
                    var image = canvas.transform.Find("Image") as RectTransform;
                    if (image == null) continue;
                    var corners = new Vector3[4]; image.GetWorldCorners(corners);
                    BannerHeightPixels = Mathf.Max(BannerHeightPixels, Mathf.RoundToInt(corners[1].y - corners[0].y));
                }
#endif
                Changed?.Invoke();
            });
            banner.OnBannerAdLoadFailed += error => Dispatch(() =>
            {
                if (googleBanner != banner) return;
                bannerRetry = Time.realtimeSinceStartup + 60f;
                DestroyGoogleBanner(); GoogleError(error.ToString());
            });
            banner.LoadAd(new AdRequest());
        }

        private int BeginGoogleFullScreen(Action dispose, Action continuation, bool rewarded)
        {
            googleFullScreenSequence++;
            Showing = true;
            pendingGoogleClose = false;
            pendingGoogleReward = rewarded;
            destroyVisibleAd = dispose;
            afterGoogleAd = continuation;
            timeBeforeGoogleAd = Time.timeScale;
            Time.timeScale = 0f;
            DestroyGoogleBanner();
            Changed?.Invoke();
            return googleFullScreenSequence;
        }

        private void GoogleClosed(bool reward, string error = null)
        {
            if (!Showing || pendingGoogleClose) return;
            if (error != null) { earned = false; GoogleError(error); }
            pendingGoogleReward = reward;
            pendingGoogleClose = true;
        }

        private void ShowGoogleReward()
        {
            if (!GoogleAllowed || !RewardFresh || !googleReward.CanShowAd()) { FinishReward(false); return; }
            var id = pendingRequest;
            var ad = googleReward; googleReward = null;
            BeginGoogleFullScreen(ad.Destroy, null, true);
            ad.OnAdFullScreenContentClosed += () => Dispatch(() => { if (id == pendingRequest) GoogleClosed(true); });
            ad.OnAdFullScreenContentFailed += error => Dispatch(() => { if (id == pendingRequest) GoogleClosed(true, error.ToString()); });
            try { ad.Show(reward => Dispatch(() => { if (Showing && id == pendingRequest && GoogleAllowed) earned = true; })); }
            catch (Exception error) { GoogleClosed(true, error.ToString()); }
        }

        /// <summary>Only at a results-screen transition. No fill never delays navigation.</summary>
        public void ShowInterstitialThen(Action continuation)
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (Showing) return;
            if (!GoogleAllowed || (store != null && store.Busy) || run == null ||
                (run.phase != RunPhase.Victory && run.phase != RunPhase.Defeat) ||
                Time.realtimeSinceStartup - lastFullScreen < Mathf.Max(30, config.interstitialIntervalSeconds) ||
                googleInterstitial == null || !googleInterstitial.CanShowAd() ||
                Time.realtimeSinceStartup - interstitialLoaded >= 3500f)
            { continuation?.Invoke(); return; }
            var ad = googleInterstitial; googleInterstitial = null;
            var sequence = BeginGoogleFullScreen(ad.Destroy, continuation, false);
            ad.OnAdFullScreenContentClosed += () => Dispatch(() => { if (sequence == googleFullScreenSequence) GoogleClosed(false); });
            ad.OnAdFullScreenContentFailed += error => Dispatch(() => { if (sequence == googleFullScreenSequence) GoogleClosed(false, error.ToString()); });
            try { ad.Show(); }
            catch (Exception error) { GoogleClosed(false, error.ToString()); }
        }

        private void HandleGooglePause(bool paused)
        {
            if (paused) { backgroundAt = Showing || privacyBusy ? -1f : Time.realtimeSinceStartup; return; }
            var away = backgroundAt >= 0f ? Time.realtimeSinceStartup - backgroundAt : 0f;
            backgroundAt = -1f;
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (!GoogleAllowed || Showing || !menuVisible || run == null || run.phase != RunPhase.ShipSelection ||
                (store != null && store.Busy) || away < Mathf.Max(30, config.appOpenBackgroundSeconds) ||
                Time.realtimeSinceStartup - lastFullScreen < 60f || googleOpen == null || !googleOpen.CanShowAd() ||
                Time.realtimeSinceStartup - openLoaded >= 14400f) return;
            var ad = googleOpen; googleOpen = null;
            var sequence = BeginGoogleFullScreen(ad.Destroy, null, false);
            ad.OnAdFullScreenContentClosed += () => Dispatch(() => { if (sequence == googleFullScreenSequence) GoogleClosed(false); });
            ad.OnAdFullScreenContentFailed += error => Dispatch(() => { if (sequence == googleFullScreenSequence) GoogleClosed(false, error.ToString()); });
            try { ad.Show(); }
            catch (Exception error) { GoogleClosed(false, error.ToString()); }
        }

        private void LateUpdate()
        {
            if (!pendingGoogleClose) return;
            pendingGoogleClose = false;
            var reward = pendingGoogleReward;
            var continuation = afterGoogleAd;
            afterGoogleAd = null;
            destroyVisibleAd?.Invoke(); destroyVisibleAd = null;
            Time.timeScale = timeBeforeGoogleAd; // After Google's Editor prefab restores its own scale.
            lastFullScreen = Time.realtimeSinceStartup;
            if (reward) FinishReward(earned);
            else Showing = false;
            Changed?.Invoke();
            continuation?.Invoke();
            ReconcileGoogleAds();
        }

        private void GoogleError(string detail)
        {
            LastError = detail ?? "Ad load failed";
            if (!RewardReady) Status = googleConsentAllows ? "unavailable" : "consent";
            Debug.LogWarning("[AstroCrawler/Ads] " + LastError);
            Changed?.Invoke();
        }

        private void DestroyGoogleBanner()
        {
            googleBanner?.Destroy(); googleBanner = null;
            BannerHeightPixels = 0;
        }

        private void ClearGoogleCache()
        {
            googleGeneration++;
            DestroyGoogleBanner();
            googleReward?.Destroy(); googleReward = null;
            googleInterstitial?.Destroy(); googleInterstitial = null;
            googleOpen?.Destroy(); googleOpen = null;
            loadingReward = loadingInterstitial = loadingOpen = RewardReady = false;
        }

        private void DisposeGoogleClient()
        {
            googleDisposed = true;
            ClearGoogleCache();
            destroyVisibleAd?.Invoke(); destroyVisibleAd = null;
            afterGoogleAd = null;
            if (Showing) Time.timeScale = timeBeforeGoogleAd;
        }
    }
}
#else
namespace AstroCrawler
{
    public sealed partial class AndroidAds
    {
        private bool privacyBusy => false;
        private bool RewardFresh => false;
        private void PrepareGoogleClient() { }
        private void GoogleCommand(string method, object[] args) { }
        private void HandleGooglePause(bool paused) { }
        private void DisposeGoogleClient() { }
        public void ShowInterstitialThen(System.Action continuation) { continuation?.Invoke(); }
    }
}
#endif
