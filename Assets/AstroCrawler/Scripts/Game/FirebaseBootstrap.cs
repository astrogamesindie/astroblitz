using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Firebase;
using Firebase.Extensions;
#endif

namespace AstroCrawler
{
    /// <summary>Initializes the configured mobile Firebase app without blocking gameplay.</summary>
    public static class FirebaseBootstrap
    {
        public static bool IsReady { get; private set; }
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private static bool initializing;
        private static int session;
        private static FirebaseApp app;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            IsReady = false;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            session++;
            initializing = false;
            app = null;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (initializing || IsReady) return;
            initializing = true;
            var currentSession = session;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (currentSession != session) return;
                initializing = false;
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError("[AstroCrawler/Firebase] Dependency initialization failed: " + task.Exception);
                    return;
                }
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogError("[AstroCrawler/Firebase] Dependencies unavailable: " + task.Result);
                    return;
                }
                try
                {
                    app = FirebaseApp.DefaultInstance;
                    IsReady = app != null;
                    if (IsReady) Debug.Log("[AstroCrawler/Firebase] Mobile app initialized: " + app.Options.ProjectId);
                }
                catch (System.Exception error)
                {
                    Debug.LogError("[AstroCrawler/Firebase] App initialization failed: " + error);
                }
            });
#endif
        }
    }
}
