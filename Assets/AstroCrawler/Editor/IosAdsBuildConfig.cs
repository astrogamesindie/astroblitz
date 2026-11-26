#if UNITY_IOS
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace AstroCrawler.Editor
{
    public sealed class IosAdsBuildConfig : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var config = Load();
            if (!report.summary.options.HasFlag(BuildOptions.Development) && !config.useTestAds &&
                (string.IsNullOrWhiteSpace(config.iosAppId) || !config.iosAppId.Contains("~") ||
                 string.IsNullOrWhiteSpace(config.iosBannerId) || string.IsNullOrWhiteSpace(config.iosRewardedId) ||
                 string.IsNullOrWhiteSpace(config.iosInterstitialId) || string.IsNullOrWhiteSpace(config.iosAppOpenId)))
                throw new BuildFailedException("Preencher App ID e as quatro unidades iOS em AndroidAds.asset.");
        }

        private static AndroidAdsConfig Load()
        {
            var config = AssetDatabase.LoadAssetAtPath<AndroidAdsConfig>("Assets/AstroCrawler/Database/AndroidAds.asset");
            if (config == null) throw new BuildFailedException("AndroidAds.asset ausente.");
            return config;
        }

        [PostProcessBuild(5)]
        public static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var config = Load();
            var test = EditorUserBuildSettings.development || config.useTestAds;
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            plist.root.SetString("GADApplicationIdentifier", test
                ? "ca-app-pub-3940256099942544~1458002511" : config.iosAppId);
            plist.root.SetString("GADUUnityVersion", Application.unityVersion);
            if (!plist.root.values.ContainsKey("NSUserTrackingUsageDescription"))
                plist.root.SetString("NSUserTrackingUsageDescription",
                    "Your permission helps us show relevant ads and support Astro Blitz.");
            var ids = new XmlDocument();
            ids.Load("Assets/GoogleMobileAds/Editor/GoogleMobileAdsSKAdNetworkItems.xml");
            var networks = plist.root.values.ContainsKey("SKAdNetworkItems")
                ? plist.root["SKAdNetworkItems"].AsArray() : plist.root.CreateArray("SKAdNetworkItems");
            foreach (XmlNode node in ids.SelectNodes("//SKAdNetworkIdentifier"))
            {
                var exists = false;
                foreach (var entry in networks.values)
                    if (entry.AsDict().values.TryGetValue("SKAdNetworkIdentifier", out var value) && value.AsString() == node.InnerText)
                        exists = true;
                if (!exists) networks.AddDict().SetString("SKAdNetworkIdentifier", node.InnerText);
            }
            plist.WriteToFile(plistPath);
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            project.AddBuildProperty(project.GetUnityFrameworkTargetGuid(), "OTHER_LDFLAGS", "-ObjC");
            project.WriteToFile(projectPath);
            Debug.Log("[AstroCrawler/Ads] Build iOS: " + (test ? "TEST" : "PRODUCTION") + "; Google SDK via CocoaPods/EDM4U.");
        }
    }
}
#endif
