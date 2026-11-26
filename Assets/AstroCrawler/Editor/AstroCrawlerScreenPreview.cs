// AstroCrawler - editor shortcuts to preview the interface on phone and tablet
// shapes, in portrait and landscape.
//
// The game picks its layout from the shape of the screen (GameUI.ScreenIsPortrait),
// so in the editor the Game view's resolution is what decides. These menu items
// add the resolution to the Game view's list when it is missing and select it.
// Unity has no public API for this; the GameViewSizes calls go through
// reflection and fail with a warning, without side effects, if a future
// version renames them.

using System;
using System.Reflection;

using UnityEditor;

using UnityEngine;

namespace AstroCrawler
{
    public static class AstroCrawlerScreenPreview
    {
        private const string Menu = "Tools/AstroCrawler/Tela/";

        [MenuItem(Menu + "Celular retrato (1080 x 2340)", false, 40)]
        private static void PhonePortrait() { Select(1080, 2340, "AstroCrawler celular retrato"); }

        [MenuItem(Menu + "Celular retrato 16:9 (1080 x 1920)", false, 41)]
        private static void PhonePortraitClassic() { Select(1080, 1920, "AstroCrawler celular retrato 16:9"); }

        [MenuItem(Menu + "Tablet retrato (1536 x 2048)", false, 42)]
        private static void TabletPortrait() { Select(1536, 2048, "AstroCrawler tablet retrato"); }

        [MenuItem(Menu + "Celular paisagem (2340 x 1080)", false, 60)]
        private static void PhoneLandscape() { Select(2340, 1080, "AstroCrawler celular paisagem"); }

        [MenuItem(Menu + "Tablet paisagem (2048 x 1536)", false, 61)]
        private static void TabletLandscape() { Select(2048, 1536, "AstroCrawler tablet paisagem"); }

        /// <summary>Selects (adding it first if needed) a fixed resolution in the Game view.</summary>
        public static bool Select(int width, int height, string label)
        {
            try
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance").GetValue(null, null);
                var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                var groupClass = group.GetType();

                var index = Find(group, groupClass, width, height);
                if (index < 0)
                {
                    var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                    var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                    var fixedResolution = Enum.Parse(kindType, "FixedResolution");
                    var size = Activator.CreateInstance(sizeType, fixedResolution, width, height, label);
                    groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = Find(group, groupClass, width, height);
                }
                if (index < 0) throw new InvalidOperationException("resolution not added");

                var gameViewType = assembly.GetType("UnityEditor.GameView");
                var gameView = EditorWindow.GetWindow(gameViewType);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var selected = gameViewType.GetProperty("selectedSizeIndex", flags);
                if (selected != null) selected.SetValue(gameView, index, null);
                else gameViewType.GetMethod("SizeSelectionCallback", flags).Invoke(gameView, new object[] { index, null });
                gameView.Repaint();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AstroCrawler] Não foi possível trocar a resolução do Game view (" + width + " x " +
                                 height + "); escolha-a manualmente. " + exception.Message);
                return false;
            }
        }

        private static int Find(object group, Type groupClass, int width, int height)
        {
            var count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupClass.GetMethod("GetGameViewSize");
            for (var i = 0; i < count; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var type = size.GetType();
                var w = (int)type.GetProperty("width").GetValue(size, null);
                var h = (int)type.GetProperty("height").GetValue(size, null);
                var kind = type.GetProperty("sizeType").GetValue(size, null).ToString();
                if (w == width && h == height && kind == "FixedResolution") return i;
            }
            return -1;
        }
    }
}
