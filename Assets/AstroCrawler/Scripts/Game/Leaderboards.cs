// AstroCrawler - endless ranking on Google Play Games (Android) and Game Center (iOS).
//
// The platform keeps the scores and the player identity; the game only sends
// its best score and reads the top 50 to draw them in its own ranking screen.
// Native bridges: Plugins/Android/AstroGames.java and Plugins/iOS/AstroRanking.swift.
// Ids live in Resources/AstroCrawler/GameServices.json; with an empty id the
// ranking reports "not configured" and nothing native is called.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Scripting;

namespace AstroCrawler
{
    public sealed class Leaderboards : MonoBehaviour
    {
        public const int TopCount = 50;
        private const string PendingKey = "AstroCrawler.Endless.v1.pendingSubmit";

        public enum State { NotConfigured, Unsupported, SignedOut, SigningIn, Ready, Loading, Loaded, Error }

        [Serializable]
        public sealed class Entry
        {
            public int rank;
            public string name;
            public long score;
            public bool me;
        }

        [Serializable]
        private sealed class Config
        {
            public string androidAppId;
            public string androidLeaderboardId;
            public string iosLeaderboardId;
        }

        [Serializable]
        private sealed class Message
        {
            public string type;
            public bool ok;
            public string name;
            public string error;
            public List<Entry> entries;
            public Entry player;
        }

        public static Leaderboards Instance { get; private set; }
        public event Action Changed;

        public State Status { get; private set; } = State.NotConfigured;
        public string PlayerName { get; private set; } = "";
        public readonly List<Entry> Entries = new List<Entry>();
        public Entry PlayerEntry { get; private set; }
        public bool IsAndroid => Application.platform == RuntimePlatform.Android;
        public bool IsIos => Application.platform == RuntimePlatform.IPhonePlayer;
        public bool SignedIn => Status == State.Ready || Status == State.Loading || Status == State.Loaded;
        public bool Busy => Status == State.SigningIn || Status == State.Loading;

        private string leaderboardId = "";
        private float operationStarted;
        private bool wantsTop;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AstroRankingConfigure(string receiver);
        [DllImport("__Internal")] private static extern void AstroRankingSignIn([MarshalAs(UnmanagedType.I1)] bool interactive);
        [DllImport("__Internal")] private static extern void AstroRankingSubmit(string leaderboardId, long score);
        [DllImport("__Internal")] private static extern void AstroRankingLoadTop(string leaderboardId, int count);
        [DllImport("__Internal")] private static extern void AstroRankingShow(string leaderboardId);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
#endif

        public static Leaderboards EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AstroCrawler.Leaderboards");
            return go.AddComponent<Leaderboards>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gameObject.name = "AstroCrawler.Leaderboards";
            DontDestroyOnLoad(gameObject);

            var config = ReadConfig();
            leaderboardId = config == null ? "" : IsAndroid ? config.androidLeaderboardId : IsIos ? config.iosLeaderboardId : "";
            leaderboardId = (leaderboardId ?? "").Trim();
            if (!IsAndroid && !IsIos) Status = State.Unsupported;
            else if (string.IsNullOrEmpty(leaderboardId) || IsAndroid && string.IsNullOrWhiteSpace(config.androidAppId))
                Status = State.NotConfigured;
            else
            {
                Status = State.SignedOut;
                // Silent: Play Games signs in by itself at launch, Game Center shows its
                // own "welcome back" banner. Neither opens a browser or leaves the app.
                SignIn(false);
            }
        }

        private static Config ReadConfig()
        {
            var asset = Resources.Load<TextAsset>("AstroCrawler/GameServices");
            if (asset == null) return null;
            try { return JsonUtility.FromJson<Config>(asset.text); }
            catch (Exception) { return null; }
        }

        /// <summary>Platform name for buttons: "Play Games" or "Game Center".</summary>
        public string PlatformName => IsIos ? "Game Center" : "Play Games";

        public bool Available => Status != State.NotConfigured && Status != State.Unsupported;

        /// <summary>Interactive shows the platform's own sign-in sheet over the game.</summary>
        public void SignIn(bool interactive)
        {
            if (!Available || Busy) return;
            Begin(State.SigningIn);
            Call(interactive ? "signIn" : "silentSignIn");
        }

        /// <summary>
        /// Sends a score. Without a signed-in player it waits in prefs and goes
        /// out on the next sign-in; only the highest waiting score is kept.
        /// </summary>
        public void Submit(long score)
        {
            if (score <= 0 || !Available) return;
            if (score > PlayerPrefs.GetInt(PendingKey, 0))
            {
                PlayerPrefs.SetInt(PendingKey, (int)Math.Min(score, int.MaxValue));
                PlayerPrefs.Save();
            }
            if (SignedIn) Call("submit", score);
        }

        /// <summary>Loads the top 50; signs in first (interactively) when needed.</summary>
        public void LoadTop()
        {
            if (!Available) { Changed?.Invoke(); return; }
            if (!SignedIn)
            {
                wantsTop = true;
                if (!Busy) SignIn(true);
                return;
            }
            if (Status == State.Loading) return;
            Begin(State.Loading);
            Call("loadTop", TopCount);
        }

        /// <summary>Opens the platform's own leaderboard screen, still inside the app.</summary>
        public void ShowNative()
        {
            if (!SignedIn) { LoadTop(); return; }
            Call("show");
        }

        private void Begin(State state)
        {
            Status = state;
            operationStarted = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        private void Call(string method, long value = 0)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (bridge == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        bridge = new AndroidJavaObject("com.astrocrawler.games.AstroGames", activity,
                            gameObject.name, leaderboardId);
                }
                switch (method)
                {
                    case "submit": bridge.Call("submit", value); break;
                    case "loadTop": bridge.Call("loadTop", (int)value); break;
                    case "signIn": bridge.Call("signIn", true); break;
                    case "silentSignIn": bridge.Call("signIn", false); break;
                    default: bridge.Call(method); break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Play Games indisponível: " + exception.GetType().Name);
                Fail();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!iosConfigured)
                {
                    AstroRankingConfigure(gameObject.name);
                    iosConfigured = true;
                }
                switch (method)
                {
                    case "submit": AstroRankingSubmit(leaderboardId, value); break;
                    case "loadTop": AstroRankingLoadTop(leaderboardId, (int)value); break;
                    case "signIn": AstroRankingSignIn(true); break;
                    case "silentSignIn": AstroRankingSignIn(false); break;
                    case "show": AstroRankingShow(leaderboardId); break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Game Center indisponível: " + exception.GetType().Name);
                Fail();
            }
#else
            Fail();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private bool iosConfigured;
#endif

        private void Fail()
        {
            if (Status == State.SigningIn) Status = State.SignedOut;
            else if (Status == State.Loading) Status = State.Error;
            wantsTop = false;
            Changed?.Invoke();
        }

        // Invoked by UnitySendMessage on the Unity thread; preserved for IL2CPP stripping.
        [Preserve]
        public void OnGamesEvent(string json)
        {
            Message message;
            try { message = JsonUtility.FromJson<Message>(json); }
            catch (Exception) { return; }
            if (message == null) return;

            switch (message.type)
            {
                case "auth":
                    if (message.ok)
                    {
                        Status = State.Ready;
                        PlayerName = message.name ?? "";
                        var pending = PlayerPrefs.GetInt(PendingKey, 0);
                        var best = Mathf.Max(pending, EndlessScore.Best);
                        if (best > 0) Call("submit", best);
                        if (wantsTop)
                        {
                            wantsTop = false;
                            Begin(State.Loading);
                            Call("loadTop", TopCount);
                            return;
                        }
                    }
                    else
                    {
                        Status = State.SignedOut;
                        wantsTop = false;
                    }
                    break;
                case "submitted":
                    if (message.ok)
                    {
                        PlayerPrefs.DeleteKey(PendingKey);
                        PlayerPrefs.Save();
                    }
                    return; // Nothing on screen changes.
                case "scores":
                    Entries.Clear();
                    if (message.entries != null)
                        for (var i = 0; i < message.entries.Count && Entries.Count < TopCount; i++)
                            if (message.entries[i] != null) Entries.Add(message.entries[i]);
                    PlayerEntry = message.player != null && message.player.rank > 0 ? message.player : null;
                    Status = State.Loaded;
                    break;
                case "error":
                    Debug.LogWarning("[AstroCrawler] Ranking: " + message.error);
                    if (Status == State.SigningIn) Status = State.SignedOut;
                    else if (Status == State.Loading) Status = State.Error;
                    wantsTop = false;
                    break;
                case "signedOut":
                    Status = State.SignedOut;
                    PlayerName = "";
                    break;
            }
            Changed?.Invoke();
        }

        private void Update()
        {
            // A lost native callback must not leave the ranking spinning forever.
            if (Busy && Time.realtimeSinceStartup - operationStarted > 30f)
            {
                Debug.LogWarning("[AstroCrawler] Ranking: tempo esgotado.");
                Fail();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Back from the Play Games / Game Center screens: send what waited.
            if (!paused && SignedIn && PlayerPrefs.GetInt(PendingKey, 0) > 0)
                Call("submit", PlayerPrefs.GetInt(PendingKey, 0));
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
