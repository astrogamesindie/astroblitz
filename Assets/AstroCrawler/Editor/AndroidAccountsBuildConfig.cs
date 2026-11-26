using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace AstroCrawler.Editor
{
    /// <summary>Keep the Player Accounts callback on every exported Unity launcher activity.</summary>
    public sealed class AndroidAccountsBuildConfig : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1001;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/UnityPlayerAccountSettings.asset");
            if (asset == null) throw new BuildFailedException("Unity Player Accounts settings are missing.");
            var settings = new SerializedObject(asset);
            if (string.IsNullOrWhiteSpace(settings.FindProperty("clientId").stringValue))
                throw new BuildFailedException("Unity Player Accounts Client ID is missing.");
            var custom = settings.FindProperty("useCustomDeepLinkUri").boolValue;
            var scheme = custom ? settings.FindProperty("customScheme").stringValue : "unitydl";
            var host = custom ? settings.FindProperty("customHost").stringValue : "com.unityplayeraccounts." + Application.cloudProjectId;
            ConfigureManifest(Path.Combine(path, "src/main/AndroidManifest.xml"), scheme, host);
        }

        public static void ConfigureManifest(string path, string scheme, string host)
        {
            if (string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(host))
                throw new BuildFailedException("Unity Player Accounts redirect URI is missing.");
            const string android = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(path);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", android);
            var launchers = doc.SelectNodes("/manifest/application/activity[intent-filter/action[@android:name='android.intent.action.MAIN'] and intent-filter/category[@android:name='android.intent.category.LAUNCHER']]", ns);
            if (launchers == null || launchers.Count == 0)
                throw new BuildFailedException("Unity Player Accounts: no Android launcher activity found.");
            foreach (XmlElement activity in launchers)
            {
                // Replace only our default callback filters; preserve links from other integrations.
                var old = activity.SelectNodes("intent-filter[action[@android:name='android.intent.action.VIEW']]", ns);
                if (old != null) foreach (XmlNode filter in old)
                    foreach (XmlElement dataElement in filter.SelectNodes("data"))
                        if (dataElement.GetAttribute("host", android).StartsWith("com.unityplayeraccounts.") ||
                            (dataElement.GetAttribute("host", android) == host && dataElement.GetAttribute("scheme", android) == scheme))
                        { activity.RemoveChild(filter); break; }
                var filterNode = doc.CreateElement("intent-filter");
                void Add(string tag, string name)
                {
                    var element = doc.CreateElement(tag);
                    element.SetAttribute("name", android, name);
                    filterNode.AppendChild(element);
                }
                Add("action", "android.intent.action.VIEW");
                Add("category", "android.intent.category.DEFAULT");
                Add("category", "android.intent.category.BROWSABLE");
                var data = doc.CreateElement("data");
                data.SetAttribute("scheme", android, scheme);
                data.SetAttribute("host", android, host);
                filterNode.AppendChild(data);
                activity.AppendChild(filterNode);
                activity.SetAttribute("exported", android, "true");
            }
            doc.Save(path);
            Debug.Log("[AstroCrawler/Account] Android login return configured on launcher activities.");
        }
    }
}
