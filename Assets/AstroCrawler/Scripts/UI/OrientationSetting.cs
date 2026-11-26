// AstroCrawler - portrait or landscape, chosen in the settings.
//
// Portrait is the default. The player can switch to landscape in Ajustes; the
// choice is saved and applied before the first scene loads. The layout itself
// does not read this preference: GameUI follows the shape the screen actually
// has, so the editor's Game view, a tablet and a phone all get the arrangement
// that fits them.
//
// Player Settings allow portrait and both landscape sides (iOS only lets the app
// rotate to orientations listed there); this class narrows them at runtime.

using UnityEngine;

namespace AstroCrawler
{
    public static class OrientationSetting
    {
        private const string Key = "AstroCrawler.LandscapeMode";

        // A fixed orientation first, so the device turns at once even when it
        // is held the other way; once the screen has turned, landscape is
        // opened up to both sides.
        private static bool pendingAutoRotation;

        public static bool Landscape
        {
            get { return PlayerPrefs.GetInt(Key, 0) == 1; }
        }

        public static void Set(bool landscape)
        {
            if (landscape == Landscape) return;
            PlayerPrefs.SetInt(Key, landscape ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtBoot()
        {
            pendingAutoRotation = false;
            Apply();
        }

        public static void Apply()
        {
            // The editor and desktop builds have no orientation: the window
            // decides, and GameUI follows it.
            if (!Application.isMobilePlatform) return;

            var landscape = Landscape;
            Screen.autorotateToPortrait = !landscape;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = landscape;
            Screen.autorotateToLandscapeRight = landscape;
            Screen.orientation = landscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            pendingAutoRotation = landscape;
        }

        /// <summary>Called every frame by GameUI; cheap until a switch is pending.</summary>
        public static void Tick()
        {
            if (!pendingAutoRotation || Screen.width <= Screen.height) return;
            pendingAutoRotation = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
