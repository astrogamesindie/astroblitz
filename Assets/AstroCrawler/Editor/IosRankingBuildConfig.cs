#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace AstroCrawler.Editor
{
    /// <summary>
    /// Endless ranking on Game Center: compiles AstroRanking.swift, links GameKit and
    /// turns on the Game Center capability. Only the ranking uses Game Center; the
    /// account and the save stay on Unity Cloud. Skipped while GameServices.json has
    /// no "iosLeaderboardId" (the game then shows the ranking as not configured).
    /// </summary>
    public static class IosRankingBuildConfig
    {
        [System.Serializable]
        private sealed class Config
        {
            public string iosLeaderboardId;
        }

        [PostProcessBuild(40)]
        public static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AstroCrawler/Resources/AstroCrawler/GameServices.json");
            var id = asset != null ? (JsonUtility.FromJson<Config>(asset.text)?.iosLeaderboardId ?? "").Trim() : "";
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[AstroCrawler] GameServices.json sem iosLeaderboardId: Game Center fica desligado neste build.");
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var main = project.GetUnityMainTargetGuid();
            var framework = project.GetUnityFrameworkTargetGuid();

            const string source = "Libraries/Plugins/iOS/AstroRanking.swift";
            if (string.IsNullOrEmpty(project.FindFileGuidByProjectPath(source)))
            {
                if (!File.Exists(Path.Combine(path, source)))
                    throw new BuildFailedException("AstroRanking.swift não foi copiado para o projeto iOS.");
                project.AddFileToBuild(framework, project.AddFile(source, source, PBXSourceTree.Source));
            }
            project.AddFrameworkToProject(framework, "GameKit.framework", false);
            project.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(framework, "CLANG_ENABLE_MODULES", "YES");

            // Reuse the entitlements file the export already has (Premium, push...), or create one.
            var entitlements = project.GetBuildPropertyForAnyConfig(main, "CODE_SIGN_ENTITLEMENTS");
            entitlements = string.IsNullOrEmpty(entitlements) ? "Unity-iPhone/AstroIdle.entitlements"
                : entitlements.Trim('"').Replace("$(SRCROOT)/", "").Replace("${SRCROOT}/", "");
            project.WriteToFile(projectPath);

            var capabilities = new ProjectCapabilityManager(projectPath, entitlements, null, main);
            capabilities.AddGameCenter();
            capabilities.WriteToFile();
            Debug.Log("[AstroCrawler] Game Center ligado para o ranking do modo infinito (" + id + ").");
        }
    }
}
#endif
