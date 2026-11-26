using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace AstroCrawler.Editor
{
    /// <summary>Reject mismatched mobile configuration before exporting a build.</summary>
    public sealed class FirebaseBuildConfig : IPreprocessBuildWithReport
    {
        private const string AndroidConfig = "Assets/google-services.json";
        private const string IosConfig = "Assets/GoogleService-Info.plist";
        public int callbackOrder => -110;

        [Serializable] private sealed class ProjectInfo { public string project_id; }
        [Serializable] private sealed class AndroidInfo { public string package_name; }
        [Serializable] private sealed class ClientInfo { public string mobilesdk_app_id; public AndroidInfo android_client_info; }
        [Serializable] private sealed class Client { public ClientInfo client_info; }
        [Serializable] private sealed class GoogleServices { public ProjectInfo project_info; public Client[] client; }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS) return;
            Validate();
        }

        [MenuItem("Tools/AstroCrawler/Firebase/Validate mobile configuration")]
        public static void Validate()
        {
            if (!File.Exists(AndroidConfig) || !File.Exists(IosConfig))
                throw new BuildFailedException("Firebase: add both mobile configuration files to Assets.");
            var android = JsonUtility.FromJson<GoogleServices>(File.ReadAllText(AndroidConfig));
            var androidId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            var iosId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            var match = false;
            if (android?.client != null)
                foreach (var client in android.client)
                    if (client?.client_info?.android_client_info?.package_name == androidId &&
                        !string.IsNullOrWhiteSpace(client.client_info.mobilesdk_app_id)) match = true;
            if (!match) throw new BuildFailedException("Firebase Android: package name does not match Player Settings.");
            var plist = new XmlDocument { XmlResolver = null };
            plist.Load(IosConfig);
            string Read(string key) => plist.SelectSingleNode("/plist/dict/key[.='" + key + "']/following-sibling::*[1]")?.InnerText;
            if (Read("BUNDLE_ID") != iosId || string.IsNullOrWhiteSpace(Read("GOOGLE_APP_ID")))
                throw new BuildFailedException("Firebase iOS: bundle ID does not match Player Settings.");
            if (string.IsNullOrWhiteSpace(android?.project_info?.project_id) || Read("PROJECT_ID") != android.project_info.project_id)
                throw new BuildFailedException("Firebase: Android and iOS must use the same Firebase project.");
            Debug.Log("[AstroCrawler/Firebase] Both mobile configurations validated: " + android.project_info.project_id);
        }

#if UNITY_IOS
        [PostProcessBuild(6)]
        public static void ConfigureIos(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            // Core integration only; enable collection deliberately when adding Analytics.
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            plist.WriteToFile(plistPath);
        }
#endif
    }
}
