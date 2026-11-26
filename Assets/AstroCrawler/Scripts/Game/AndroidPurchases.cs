using System;
using UnityEngine;
using UnityEngine.Scripting;
using System.Runtime.InteropServices;

namespace AstroCrawler
{
    /// <summary>Permanent Premium on Google Play/App Store; prefs are an offline entitlement cache.</summary>
    public sealed class AndroidPurchases : MonoBehaviour
    {
        [SerializeField] private AndroidStoreConfig config;
        public static AndroidPurchases Instance { get; private set; }
        public event Action Changed;
        public bool AdsRemoved { get; private set; }
        public bool Busy { get; private set; }
        public bool ProductAvailable { get; private set; }
        public string Price { get; private set; } = "";
        public string Status { get; private set; } = "connecting";
        public bool Supported => Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
        public bool CanPurchase => Supported && ProductAvailable && !Busy && !AdsRemoved && Status != "pending";
        private string CacheKey => "AstroCrawler.Store.v1." + config.removeAdsProductId;
        private float operationStarted;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AstroPremiumConfigure(string receiver, string productId);
        [DllImport("__Internal")] private static extern void AstroPremiumRefresh([MarshalAs(UnmanagedType.I1)] bool restore);
        [DllImport("__Internal")] private static extern void AstroPremiumBuy();
        [DllImport("__Internal")] private static extern void AstroPremiumDispose();
        private bool iosConfigured;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
#endif

        public static AndroidPurchases EnsureCreated()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<AndroidPurchases>("AstroCrawler/AndroidPurchases");
            if (prefab == null)
            {
                Debug.LogError("[AstroCrawler] Prefab AndroidPurchases ausente em Resources.");
                return null;
            }
            return Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (config == null || string.IsNullOrWhiteSpace(config.removeAdsProductId))
            {
                Debug.LogError("[AstroCrawler] AndroidStoreConfig inválido.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.name = "AstroCrawler.AndroidPurchases";
            DontDestroyOnLoad(gameObject);
            // Read before any ad adapter initializes, avoiding a banner flash at launch.
            AdsRemoved = Supported && (PlayerPrefs.GetInt(CacheKey, 0) == 1 ||
                Application.platform == RuntimePlatform.Android && PlayerPrefs.GetInt("AstroCrawler.Store.v1.remove_ads", 0) == 1);
            if (Supported) RefreshPurchases(false);
            else Status = "unsupported";
        }

        public void BuyRemoveAds()
        {
            if (!CanPurchase) return;
            BeginOperation("buying");
            CallBridge("purchase");
        }

        public void RestorePurchases()
        {
            RefreshPurchases(true);
        }

        private void RefreshPurchases(bool explicitRestore)
        {
            if (!Supported || Busy) return;
            ProductAvailable = false;
            Price = "";
            BeginOperation("restoring");
            CallBridge(explicitRestore ? "restore" : "refresh");
        }

        private void BeginOperation(string status)
        {
            Busy = true;
            Status = status;
            operationStarted = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        private void CallBridge(string method)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (bridge == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        bridge = new AndroidJavaObject("com.astrocrawler.store.AstroBilling", activity,
                            gameObject.name, config.removeAdsProductId);
                }
                bridge.Call(method == "restore" ? "refresh" : method);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Billing indisponível: " + exception.GetType().Name);
                Busy = false;
                Status = "error";
                Changed?.Invoke();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!iosConfigured)
                {
                    AstroPremiumConfigure(gameObject.name, config.removeAdsProductId);
                    iosConfigured = true;
                }
                if (method == "purchase") AstroPremiumBuy();
                else AstroPremiumRefresh(method == "restore");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] StoreKit indisponível: " + exception.GetType().Name);
                Busy = false;
                Status = "error";
                Changed?.Invoke();
            }
#endif
        }

        [Serializable]
        private sealed class StoreMessage
        {
            public string type;
            public string price;
            public bool owned;
        }

        // Invoked by UnitySendMessage on the Unity thread; preserve with IL2CPP stripping.
        [Preserve]
        public void OnBillingEvent(string json)
        {
            var message = JsonUtility.FromJson<StoreMessage>(json);
            if (message == null) return;
            switch (message.type)
            {
                case "product":
                    Price = message.price;
                    ProductAvailable = !string.IsNullOrEmpty(Price);
                    break;
                case "owned":
                    AdsRemoved = message.owned;
                    PlayerPrefs.SetInt(CacheKey, AdsRemoved ? 1 : 0);
                    if (Application.platform == RuntimePlatform.Android)
                        PlayerPrefs.SetInt("AstroCrawler.Store.v1.remove_ads", AdsRemoved ? 1 : 0);
                    PlayerPrefs.Save();
                    Busy = false;
                    Status = AdsRemoved ? "owned" : "ready";
                    break;
                case "pending": Busy = false; Status = "pending"; break;
                case "cancelled": Busy = false; Status = "cancelled"; break;
                case "error": Busy = false; ProductAvailable = false; Status = "error"; break;
            }
            Changed?.Invoke();
        }

        private void Update()
        {
            // Unscaled: neither a paused run nor a lost store callback can lock the buttons forever.
            if (Busy && Time.realtimeSinceStartup - operationStarted > 60f)
            {
                Busy = false;
                Status = "error";
                Changed?.Invoke();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && Supported && !Busy)
            {
                RefreshPurchases(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) { bridge.Call("dispose"); bridge.Dispose(); bridge = null; }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            if (iosConfigured) AstroPremiumDispose();
#endif
            Changed = null;
            Instance = null;
        }
    }
}
