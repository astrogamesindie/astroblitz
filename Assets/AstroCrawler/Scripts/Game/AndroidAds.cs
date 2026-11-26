using System;
using UnityEngine;

namespace AstroCrawler
{
    public sealed partial class AndroidAds : MonoBehaviour
    {
        [SerializeField] private AndroidAdsConfig config;
        public static AndroidAds Instance { get; private set; }
        public event Action Changed;
        public bool RewardReady { get; private set; }
        public bool Showing { get; private set; }
        public bool PrivacyRequired { get; private set; }
        public int BannerHeightPixels { get; private set; }
        public int RewardCoins => config != null ? Mathf.Max(0, config.rewardCoins) : 100;
        private bool IsIos => Application.platform == RuntimePlatform.IPhonePlayer;
        public bool Supported => Application.isEditor ? config != null && config.officialPreviewInEditor :
            Application.platform == RuntimePlatform.Android || IsIos;
        public bool TestAds => Application.isEditor || Debug.isDebugBuild || (config != null && config.useTestAds);
        public string BannerUnitId => TestAds ? IsIos ? "ca-app-pub-3940256099942544/2934735716" :
            "ca-app-pub-3940256099942544/6300978111" : IsIos ? config.iosBannerId : config.bannerId;
        public string RewardUnitId => TestAds ? IsIos ? "ca-app-pub-3940256099942544/1712485313" :
            "ca-app-pub-3940256099942544/5224354917" : IsIos ? config.iosRewardedId : config.rewardedId;
        public string InterstitialUnitId => TestAds ? IsIos ? "ca-app-pub-3940256099942544/4411468910" :
            "ca-app-pub-3940256099942544/1033173712" : IsIos ? config.iosInterstitialId : config.interstitialId;
        public string AppOpenUnitId => TestAds ? IsIos ? "ca-app-pub-3940256099942544/5662855259" :
            "ca-app-pub-3940256099942544/3419835294" : IsIos ? config.iosAppOpenId : config.appOpenId;
        public string Status { get; private set; } = "idle";
        public string LastError { get; private set; } = "";
        public bool CanRetry => enabledAds && !Showing && Time.realtimeSinceStartup >= retryAfter;
        public bool CanReward => Supported && RewardReady && RewardFresh && !Showing && !privacyBusy && AdsPolicy.CanShowAds;
        private AndroidPurchases store;
        private StoreUpdateGate updates;
        private bool enabledAds;
        private bool menuVisible;
        private int sequence;
        private int pendingRequest;
        private bool earned;
        private Action grant;
        private Func<bool> stillEligible;
        private float retryAfter;

        public static AndroidAds EnsureCreated()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<AndroidAds>("AstroCrawler/AndroidAds");
            if (prefab == null) { Debug.LogError("[AstroCrawler] Prefab AndroidAds ausente."); return null; }
            return Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (config == null) { Debug.LogError("[AstroCrawler] AndroidAdsConfig ausente."); Destroy(gameObject); return; }
            Instance = this;
            gameObject.name = "AstroCrawler.AndroidAds";
            DontDestroyOnLoad(gameObject);
            PrepareGoogleClient();
            store = AndroidPurchases.EnsureCreated();
            if (store != null) store.Changed += RefreshPolicy;
            updates = StoreUpdateGate.Instance;
            if (updates != null) updates.Changed += RefreshPolicy;
            RefreshPolicy();
        }

        private void RefreshPolicy()
        {
            var allow = Supported && AdsPolicy.CanShowAds;
            if (allow == enabledAds) return;
            enabledAds = allow;
            Status = allow ? "loading" : "blocked";
            if (!allow) { RewardReady = false; BannerHeightPixels = 0; }
            Native("setEnabled", allow);
            if (allow) Native("setMenuVisible", menuVisible);
            Changed?.Invoke();
        }

        public void SetMenuVisible(bool visible)
        {
            if (menuVisible == visible) return;
            menuVisible = visible;
            if (!visible) BannerHeightPixels = 0;
            if (enabledAds) Native("setMenuVisible", visible);
            Changed?.Invoke();
        }

        public bool ShowRewarded(Action onEarned, Func<bool> eligibility)
        {
            if (!CanReward || onEarned == null || eligibility == null || !eligibility()) return false;
            pendingRequest = ++sequence;
            earned = false;
            grant = onEarned;
            stillEligible = eligibility;
            Showing = true;
            RewardReady = false;
            Native("showReward", pendingRequest);
            Changed?.Invoke();
            return true;
        }

        public void ShowPrivacyOptions()
        {
            if (Supported && PrivacyRequired && !Showing) Native("privacyOptions");
        }

        public void RefreshLayout()
        {
            if (enabledAds && menuVisible) Native("refreshLayout");
        }

        public void RetryLoading()
        {
            if (!CanRetry) return;
            retryAfter = Time.realtimeSinceStartup + 15f;
            LastError = "";
            Status = "loading";
            // Retry consent and cached formats after a recoverable SDK/network failure.
            Native("setEnabled", true);
            Native("retry");
            Changed?.Invoke();
        }

        private void Native(string method, params object[] args)
        {
            GoogleCommand(method, args);
        }

        private void FinishReward(bool success)
        {
            var callback = grant;
            var valid = stillEligible;
            grant = null; stillEligible = null; pendingRequest = 0; earned = false; Showing = false;
            if (success && enabledAds && AdsPolicy.CanShowAds && valid != null && valid()) callback?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            HandleGooglePause(paused);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            DisposeGoogleClient();
            if (store != null) store.Changed -= RefreshPolicy;
            if (updates != null) updates.Changed -= RefreshPolicy;
            grant = null; stillEligible = null;
            Changed = null;
            Instance = null;
        }
    }
}
