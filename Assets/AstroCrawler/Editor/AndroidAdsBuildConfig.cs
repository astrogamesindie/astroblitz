using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace AstroCrawler.Editor
{
    public sealed class AndroidAdsBuildConfig : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var config = AssetDatabase.LoadAssetAtPath<AndroidAdsConfig>("Assets/AstroCrawler/Database/AndroidAds.asset");
            if (config == null) throw new BuildFailedException("AndroidAds.asset ausente.");
            var test = EditorUserBuildSettings.development || config.useTestAds;
            if (!test && (string.IsNullOrWhiteSpace(config.androidAppId) ||
                !config.androidAppId.Contains("~") || string.IsNullOrWhiteSpace(config.bannerId) ||
                string.IsNullOrWhiteSpace(config.rewardedId) || string.IsNullOrWhiteSpace(config.interstitialId) ||
                string.IsNullOrWhiteSpace(config.appOpenId)))
                throw new BuildFailedException("Preencher App ID e as quatro unidades Android em AndroidAds.asset.");
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(manifestPath);
            var application = document.SelectSingleNode("/manifest/application");
            if (application == null) throw new BuildFailedException("Manifesto Android sem application.");
            const string android = "http://schemas.android.com/apk/res/android";
            XmlElement entry = null;
            foreach (XmlNode node in application.ChildNodes)
                if (node is XmlElement element && element.Name == "meta-data" &&
                    element.GetAttribute("name", android) == "com.google.android.gms.ads.APPLICATION_ID") entry = element;
            if (entry == null) { entry = document.CreateElement("meta-data"); application.AppendChild(entry); }
            entry.SetAttribute("name", android, "com.google.android.gms.ads.APPLICATION_ID");
            entry.SetAttribute("value", android, test ? "ca-app-pub-3940256099942544~3347511713" :
                config.androidAppId);
            document.Save(manifestPath);
            Debug.Log("[AstroCrawler/Ads] Build Android: " + (test ? "TEST" : "PRODUCTION"));
        }
    }
}
