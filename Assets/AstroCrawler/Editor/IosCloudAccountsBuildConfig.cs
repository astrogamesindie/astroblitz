#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace AstroCrawler.Editor
{
    /// <summary>Remove the retired Game Center login bridge (AstroGameCenter.mm) from reused exports.</summary>
    public static class IosCloudAccountsBuildConfig
    {
        [PostProcessBuild(20)]
        public static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var main = project.GetUnityMainTargetGuid();
            var framework = project.GetUnityFrameworkTargetGuid();
            foreach (var source in new[] { "Libraries/Plugins/iOS/AstroGameCenter.mm", "Libraries/AstroGameCenter.mm" })
            {
                var guid = project.FindFileGuidByProjectPath(source);
                if (string.IsNullOrEmpty(guid)) continue;
                if (!string.IsNullOrEmpty(main)) project.RemoveFileFromBuild(main, guid);
                if (!string.IsNullOrEmpty(framework)) project.RemoveFileFromBuild(framework, guid);
                project.RemoveFile(guid);
            }
            // GameKit and the game-center entitlement are not removed any more: since
            // 03/11/2026 the endless ranking uses them (IosRankingBuildConfig, order 40).
            project.WriteToFile(projectPath);
            Debug.Log("[AstroCrawler/Accounts] Unity Cloud accounts retained; old Game Center login bridge removed from export.");
        }
    }
}
#endif
