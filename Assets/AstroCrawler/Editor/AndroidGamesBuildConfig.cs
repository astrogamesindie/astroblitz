using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace AstroCrawler.Editor
{
    /// <summary>
    /// Writes the Play Games Services project id (Resources/AstroCrawler/GameServices.json,
    /// "androidAppId") into the generated Android manifest as a string resource, which is
    /// what the Play Games SDK reads. Without an id the build continues and the ranking
    /// reports "not configured" in the game.
    /// </summary>
    public sealed class AndroidGamesBuildConfig : IPostGenerateGradleAndroidProject
    {
        private const string MetaName = "com.google.android.gms.games.APP_ID";
        private const string ResourceName = "astro_games_app_id";
        public int callbackOrder => 1002;

        [System.Serializable]
        private sealed class Config
        {
            public string androidAppId;
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AstroCrawler/Resources/AstroCrawler/GameServices.json");
            var appId = asset != null ? (JsonUtility.FromJson<Config>(asset.text)?.androidAppId ?? "").Trim() : "";
            if (string.IsNullOrEmpty(appId))
            {
                Debug.LogWarning("[AstroCrawler] GameServices.json sem androidAppId: o ranking do Play Games fica desligado neste build.");
                return;
            }
            if (!Regex.IsMatch(appId, "^[0-9]+$"))
                throw new UnityEditor.Build.BuildFailedException("GameServices.json: androidAppId deve ser só o número do projeto do Play Games.");

            var values = Path.Combine(path, "src/main/res/values");
            Directory.CreateDirectory(values);
            File.WriteAllText(Path.Combine(values, "astro_games.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n" +
                "    <string name=\"" + ResourceName + "\" translatable=\"false\">" + appId + "</string>\n</resources>\n");
            ConfigureManifest(Path.Combine(path, "src/main/AndroidManifest.xml"));
        }

        private static void ConfigureManifest(string manifest)
        {
            const string android = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(manifest);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", android);
            var application = doc.SelectSingleNode("/manifest/application") as XmlElement;
            if (application == null) throw new UnityEditor.Build.BuildFailedException("AndroidManifest sem <application>.");
            var meta = application.SelectSingleNode("meta-data[@android:name='" + MetaName + "']", ns) as XmlElement;
            if (meta == null)
            {
                meta = doc.CreateElement("meta-data");
                meta.SetAttribute("name", android, MetaName);
                application.AppendChild(meta);
            }
            meta.SetAttribute("value", android, "@string/" + ResourceName);
            doc.Save(manifest);
        }
    }
}
