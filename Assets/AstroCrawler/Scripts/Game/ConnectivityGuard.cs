// AstroCrawler - the game needs an internet connection.

using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AstroCrawler
{
    /// <summary>
    /// Watches the connection and, while it is down, covers the game with a
    /// popup asking the player to turn the internet on (ads, purchases, the
    /// account and its cloud save all need it). A run in progress is frozen
    /// under the popup and resumes when the connection is back.
    ///
    /// Two signals: Application.internetReachability (instant, but only says a
    /// network interface is up) and a small HTTP probe that must answer 204
    /// (catches Wi-Fi without internet). The probe runs every 15 s while
    /// online, every 3 s while the popup is up, and at once on "Tentar de novo".
    /// Two failed probes in a row count as offline.
    /// </summary>
    public sealed class ConnectivityGuard : MonoBehaviour
    {
        private const string ProbeUrl = "https://clients3.google.com/generate_204";
        private const float OnlineProbeInterval = 15f;
        private const float OfflineProbeInterval = 3f;

        public static ConnectivityGuard Instance { get; private set; }

        /// <summary>Editor/test switch: behave as if the device had no connection.</summary>
        public static bool SimulateOffline;

        public bool Offline { get; private set; }

        private int failedProbes;
        private bool probing;
        private float nextProbe;
        private float frozenScale = -1f;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform panel;
        private TMP_Text title;
        private TMP_Text body;
        private Button retry;

        public static ConnectivityGuard EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ConnectivityGuard");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ConnectivityGuard>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var reachable = !SimulateOffline && Application.internetReachability != NetworkReachability.NotReachable;
            if (!reachable)
            {
                failedProbes = 2;
                SetOffline(true);
            }
            else if (!probing && Time.unscaledTime >= nextProbe)
            {
                StartCoroutine(Probe());
            }
            if (Offline) KeepFrozen();
        }

        private IEnumerator Probe()
        {
            probing = true;
            using (var request = UnityWebRequest.Head(ProbeUrl))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                var ok = !SimulateOffline && request.result == UnityWebRequest.Result.Success &&
                    (request.responseCode == 204 || request.responseCode == 200);
                failedProbes = ok ? 0 : failedProbes + 1;
                SetOffline(failedProbes >= 2);
            }
            nextProbe = Time.unscaledTime + (Offline ? OfflineProbeInterval : OnlineProbeInterval);
            probing = false;
        }

        private void Retry()
        {
            Sfx.Play(Sound.Tap);
            if (retry != null) Juice.Punch(retry.transform, 0.08f, 0.25f);
            if (body != null) body.text = GameLocalization.Choose("Verificando a conexão…", "Checking the connection…", "Comprobando la conexión…");
            nextProbe = 0f;
        }

        // ------------------------------------------------------------------
        // Popup and freezing the run
        // ------------------------------------------------------------------

        private void SetOffline(bool offline)
        {
            if (offline == Offline) return;
            Offline = offline;
            if (offline)
            {
                Build();
                scaler.referenceResolution = new Vector2(UIFactory.ReferenceWidth, UIFactory.ReferenceHeight);
                RefreshTexts();
                canvas.gameObject.SetActive(true);
                Juice.PopIn(panel, 1f, 0.3f, 0f, true);
                Sfx.Play(Sound.Denied);
                KeepFrozen();
            }
            else
            {
                if (canvas != null) canvas.gameObject.SetActive(false);
                // Only undo our own freeze; something else may have changed it since.
                if (frozenScale >= 0f && Time.timeScale == 0f) Time.timeScale = frozenScale;
                frozenScale = -1f;
                Sfx.Play(Sound.Purchase);
            }
        }

        /// <summary>Freezes combat under the popup (the pause, picks and menus are already still).</summary>
        private void KeepFrozen()
        {
            var run = GameManager.Instance != null ? GameManager.Instance.run : null;
            if (run == null || run.phase != RunPhase.WaveInProgress || Time.timeScale == 0f) return;
            frozenScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void RefreshTexts()
        {
            title.text = GameLocalization.Choose("SEM CONEXÃO", "NO CONNECTION", "SIN CONEXIÓN");
            body.text = GameLocalization.Choose(
                "Ative a internet (Wi-Fi ou dados móveis) para jogar e usar a conta, a loja e as recompensas.",
                "Turn on the internet (Wi-Fi or mobile data) to play and use your account, the store and rewards.",
                "Activa internet (Wi-Fi o datos móviles) para jugar y usar la cuenta, la tienda y las recompensas.");
            SetCaption(retry, GameLocalization.Choose("TENTAR DE NOVO", "TRY AGAIN", "REINTENTAR"));
        }

        private static void SetCaption(Button button, string text)
        {
            var caption = button != null ? button.transform.Find("Caption") : null;
            var label = caption != null ? caption.GetComponent<TMP_Text>() : null;
            if (label != null) label.text = text;
        }

        private void Build()
        {
            if (canvas != null) return;
            // Above the game UI (100) and the warp (900), under the screen flash (1000).
            canvas = UIFactory.CreateCanvas("ConnectivityPopup", transform, 950);
            scaler = canvas.GetComponent<CanvasScaler>();

            var veil = UIFactory.Rect(canvas.transform, "Veil", new Color(0.01f, 0.015f, 0.03f, 0.9f));
            UIFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = true;

            var safe = UIFactory.NewRect(canvas.transform, "SafeArea");
            UIFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var accent = UIFactory.ActionBorder;
            var cutFrame = ArtLibrary.Frame;
            var card = UIFactory.Panel9(safe, "Panel", accent, cutFrame);
            card.pixelsPerUnitMultiplier = 0.2f;
            panel = card.rectTransform;
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));
            UIFactory.DropShadow(card, 34f, 12f, 0.6f);
            var surface = UIFactory.Panel9(panel, "Surface", new Color(0.035f, 0.075f, 0.095f), cutFrame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var gradient = UIFactory.Rect(surface.transform, "Gradient", new Color(0.06f, 0.15f, 0.18f, 0.55f),
                ArtLibrary.GradientPanel);
            UIFactory.Stretch(gradient.rectTransform);
            gradient.raycastTarget = false;

            var iconBack = UIFactory.Rect(panel, "IconBack", UIFactory.ActionFill, ArtLibrary.Art("hex_disc"));
            UIFactory.Place(iconBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(170f, 170f));
            iconBack.raycastTarget = false;
            var ring = UIFactory.Rect(iconBack.transform, "Ring", accent, ArtLibrary.Art("wire_hex"));
            UIFactory.Stretch(ring.rectTransform);
            ring.preserveAspect = true;
            ring.raycastTarget = false;
            var globe = UIFactory.Rect(iconBack.rectTransform, "Globe", UIFactory.Accent, ArtLibrary.IconLanguage);
            globe.preserveAspect = true;
            globe.raycastTarget = false;
            UIFactory.Place(globe.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            var cross = UIFactory.Rect(iconBack.rectTransform, "Cross", UIFactory.Danger, ArtLibrary.IconCancel);
            cross.preserveAspect = true;
            cross.raycastTarget = false;
            UIFactory.Place(cross.rectTransform, new Vector2(1f, 0f), new Vector2(-6f, 6f), new Vector2(64f, 64f));
            Juice.BreatheUI(cross, 0.5f, 1f, 1.1f);

            title = UIFactory.Label(panel, "Title", "", UIFactory.TextHeading, UIFactory.Accent, TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -264f), new Vector2(760f, 72f));
            title.characterSpacing = 3f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 28f;
            title.fontSizeMax = UIFactory.TextHeading;

            body = UIFactory.Label(panel, "Body", "", 32, UIFactory.Ink, TextAlignmentOptions.Center);
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(740f, 160f));
            body.enableAutoSizing = true;
            body.fontSizeMin = 28f;
            body.fontSizeMax = 32f;

            retry = UIFactory.Button(panel, "Retry", "", accent, UIFactory.Ink, UIFactory.TextButton, Sound.None);
            UIFactory.Place(retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 64f),
                new Vector2(740f, 112f));
            GameUI.StyleCutAction(retry, cutFrame, accent, UIFactory.ActionFillStrong);
            var retryIcon = UIFactory.Rect(retry.transform, "Icon", UIFactory.Ink, ArtLibrary.IconRetry);
            UIFactory.Place(retryIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), Vector2.one * 42f);
            retryIcon.preserveAspect = true;
            retryIcon.raycastTarget = false;
            var retryCaption = retry.transform.Find("Caption").GetComponent<TMP_Text>();
            retryCaption.rectTransform.offsetMin = new Vector2(86f, 12f);
            retryCaption.rectTransform.offsetMax = new Vector2(-32f, -12f);
            retryCaption.characterSpacing = 3f;
            retryCaption.enableAutoSizing = true;
            retryCaption.fontSizeMin = 28f;
            retryCaption.fontSizeMax = UIFactory.TextButton;
            retry.onClick.AddListener(Retry);

            canvas.gameObject.SetActive(false);
        }
    }
}
