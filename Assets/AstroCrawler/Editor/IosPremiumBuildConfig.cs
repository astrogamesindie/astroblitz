#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace AstroCrawler.Editor
{
    public static class IosPremiumBuildConfig
    {
        [PostProcessBuild(30)]
        public static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var file = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(file);
            var framework = project.GetUnityFrameworkTargetGuid();
            var main = project.GetUnityMainTargetGuid();
            // AstroPremium: remove ads; AstroShipStore: ships sold for real money.
            foreach (var name in new[] { "AstroPremium.swift", "AstroShipStore.swift" })
            {
                var source = "Libraries/Plugins/iOS/" + name;
                if (!string.IsNullOrEmpty(project.FindFileGuidByProjectPath(source))) continue;
                if (!File.Exists(Path.Combine(path, source)))
                    throw new BuildFailedException(name + " não foi copiado para o projeto iOS.");
                project.AddFileToBuild(framework, project.AddFile(source, source, PBXSourceTree.Source));
            }
            project.AddFrameworkToProject(framework, "StoreKit.framework", false);
            project.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(framework, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(framework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.SetBuildProperty(main, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.AddBuildProperty(framework, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");
            project.WriteToFile(file);
        }
    }
}
#endif
