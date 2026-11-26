using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace AstroCrawler
{
    public sealed class StoreUpdateGate : MonoBehaviour
    {
        [SerializeField] private StoreUpdateConfig config;
        public static StoreUpdateGate Instance { get; private set; }
        public event Action Changed;
        public bool Checking { get; private set; }
        public bool Required { get; private set; }
        public bool Launching { get; private set; }
        public bool BlocksPlay => Checking || Required || Launching;
        public string LatestVersion { get; private set; } = "";
        public string Status { get; private set; } = "idle";
        private int requestId;
        private float lastCheck = float.NegativeInfinity;
        private string storeUrl;
        private Coroutine requestRoutine;
        // Scope the offline lock to the installed build, not just its marketing version.
        private string CacheKey => "AstroCrawler.Update.v1." + Application.identifier + "." +
            Application.version + "." + Application.buildGUID;
        private bool Supported => Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;
        private bool Enabled => config != null && config.enabledChecks && Supported &&
            (!Debug.isDebugBuild || config.checkDevelopmentBuilds);
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
#endif

        public static StoreUpdateGate EnsureCreated()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<StoreUpdateGate>("AstroCrawler/StoreUpdateGate");
            if (prefab == null) { Debug.LogError("[AstroCrawler] Prefab StoreUpdateGate ausente."); return null; }
            return Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gameObject.name = "AstroCrawler.StoreUpdateGate";
            DontDestroyOnLoad(gameObject);
            if (!Enabled) return;
            LatestVersion = PlayerPrefs.GetString(CacheKey, "");
            Required = !string.IsNullOrEmpty(LatestVersion);
            CheckNow();
        }

        public void CheckNow()
        {
            if (!Enabled || Checking || Launching) return;
            lastCheck = Time.realtimeSinceStartup;
            Checking = true;
            Status = "checking";
            var id = ++requestId;
            Changed?.Invoke();
            if (requestRoutine != null) StopCoroutine(requestRoutine);
            if (Application.platform == RuntimePlatform.Android)
            {
                requestRoutine = StartCoroutine(AndroidTimeout(id));
                Native("check", id, true);
            }
            else requestRoutine = StartCoroutine(CheckAppStore(id));
        }

        private IEnumerator AndroidTimeout(int id)
        {
            yield return new WaitForSecondsRealtime(Mathf.Clamp(config.timeoutSeconds, 5, 30));
            if (id == requestId && Checking) { ++requestId; Fail("error"); }
        }

        [Serializable] private sealed class Lookup { public int resultCount; public StoreApp[] results; }
        [Serializable] private sealed class StoreApp
        {
            public string bundleId, version, minimumOsVersion, trackViewUrl;
            public long trackId;
        }

        private IEnumerator CheckAppStore(int id)
        {
            var bundle = Application.identifier;
            if (string.IsNullOrEmpty(bundle) || bundle.StartsWith("com.Company.", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[AstroCrawler] Atualização iOS: configurar Bundle ID real no Player Settings.");
                Fail("configuration"); yield break;
            }
            var country = (config.iosStoreCountry ?? "").Trim();
            if (country.Length != 2 || !char.IsLetter(country[0]) || !char.IsLetter(country[1]))
            { Fail("configuration"); yield break; }
            var selector = string.IsNullOrWhiteSpace(config.iosAppId) ?
                "bundleId=" + UnityWebRequest.EscapeURL(bundle) : "id=" + UnityWebRequest.EscapeURL(config.iosAppId.Trim());
            var url = "https://itunes.apple.com/lookup?" + selector + "&country=" + country.ToLowerInvariant() + "&entity=software";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.Clamp(config.timeoutSeconds, 5, 30);
                request.SetRequestHeader("Cache-Control", "no-cache");
                yield return request.SendWebRequest();
                if (id != requestId) yield break;
                if (request.result != UnityWebRequest.Result.Success) { Fail("error"); yield break; }
                Lookup data = null;
                try { data = JsonUtility.FromJson<Lookup>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                if (data == null || data.resultCount < 1 || data.results == null) { Fail("unpublished"); yield break; }
                foreach (var app in data.results)
                {
                    if (app == null || !string.Equals(app.bundleId, bundle, StringComparison.Ordinal)) continue;
                    if (!StoreVersion.TryCompare(app.version, Application.version, out var comparison))
                    { Fail("error"); yield break; }
#if UNITY_IOS && !UNITY_EDITOR
                    if (!string.IsNullOrEmpty(app.minimumOsVersion) &&
                        StoreVersion.TryCompare(app.minimumOsVersion, UnityEngine.iOS.Device.systemVersion, out var osComparison) && osComparison > 0)
                    { MarkCurrent(); Status = "incompatible"; yield break; }
#endif
                    // Build the link from the verified numeric ID, never open an arbitrary response URL.
                    if (app.trackId <= 0) { Fail("error"); yield break; }
                    storeUrl = "https://apps.apple.com/" + country.ToLowerInvariant() + "/app/id" + app.trackId;
                    if (comparison > 0) Require(app.version);
                    else MarkCurrent();
                    yield break;
                }
                Fail("unpublished");
            }
        }

        public void UpdateNow()
        {
            if (!Enabled || !Required || Checking || Launching) return;
            if (Application.platform == RuntimePlatform.Android)
            {
                Checking = true;
                Status = "checking";
                var id = ++requestId;
                Changed?.Invoke();
                if (requestRoutine != null) StopCoroutine(requestRoutine);
                requestRoutine = StartCoroutine(AndroidTimeout(id));
                Native("update", id);
            }
            else OpenStore();
        }

        private void OpenStore()
        {
            if (Application.platform == RuntimePlatform.Android)
                Application.OpenURL("https://play.google.com/store/apps/details?id=" + UnityWebRequest.EscapeURL(Application.identifier));
            else if (!string.IsNullOrEmpty(storeUrl)) Application.OpenURL(storeUrl);
            else { CheckNow(); return; }
            Status = "store";
            Changed?.Invoke();
        }

        private void Require(string version)
        {
            Checking = false;
            Launching = false;
            Required = true;
            LatestVersion = version;
            Status = "required";
            PlayerPrefs.SetString(CacheKey, version);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private void MarkCurrent()
        {
            Checking = false; Required = false; Launching = false; LatestVersion = ""; Status = "current";
            PlayerPrefs.DeleteKey(CacheKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private void Fail(string status)
        {
            Checking = false; Launching = false; Status = status;
            // An unknown/offline store is not evidence of an update. Preserve only a known lock.
            Changed?.Invoke();
        }

        [Serializable] private sealed class AndroidMessage
        { public string type; public int request; public int version; }

        [Preserve]
        public void OnAndroidUpdateEvent(string json)
        {
            var value = JsonUtility.FromJson<AndroidMessage>(json);
            if (value == null || value.request != requestId || !Enabled) return;
            switch (value.type)
            {
                case "required":
                    if (value.version > 0) Require(value.version.ToString());
                    else Fail("error");
                    break;
                case "current": MarkCurrent(); break;
                case "launching": Checking = false; Launching = true; Status = "launching"; Changed?.Invoke(); break;
                case "store": Checking = false; Launching = false; OpenStore(); break;
                case "cancelled": Fail("cancelled"); break;
                case "error": Fail("error"); break;
            }
        }

        private void Native(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (bridge == null)
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        bridge = new AndroidJavaObject("com.astrocrawler.updates.AstroUpdates", activity, gameObject.name);
                bridge.Call(method, args);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Atualização Android indisponível: " + exception.GetType().Name);
                Fail("error");
            }
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused || !Enabled || Checking) return;
            if (Launching) { Native("check", requestId, true); return; }
            if (AndroidAds.Instance != null && AndroidAds.Instance.Showing) return;
            if (AndroidPurchases.Instance != null && AndroidPurchases.Instance.Busy) return;
            if (Required || Time.realtimeSinceStartup - lastCheck >= Mathf.Max(60, config.foregroundIntervalSeconds)) CheckNow();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) { bridge.Call("dispose"); bridge.Dispose(); bridge = null; }
#endif
            Changed = null;
            Instance = null;
        }
    }
}
