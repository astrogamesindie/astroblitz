using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    /// <summary>
    /// Keeps the whole 1920x1080 layout on screen at any aspect ratio.
    ///
    /// On a phone wider than 16:9 (most are 19.5:9 to 21:9) the canvas matches
    /// height: the layout keeps its full size and the extra width goes to the
    /// sides. On anything narrower (tablets at 4:3, 16:10) it matches width
    /// instead, so the edges are never cropped. A fixed 0.5 blend does neither
    /// well: on a 20:9 phone it shrinks every button by about 10%.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class ScreenFit : MonoBehaviour
    {
        private CanvasScaler scaler;
        private float aspect;

        private void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
            RestoreFullscreen();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) RestoreFullscreen();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) RestoreFullscreen();
        }

        private static void RestoreFullscreen()
        {
            if (!Application.isMobilePlatform) return;
            Screen.fullScreen = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var fullscreen = new AndroidJavaClass("com.astrocrawler.display.AstroFullscreen"))
                fullscreen.CallStatic("apply", activity);
#endif
        }

        private void Update()
        {
            if (Screen.height > 0 && !Mathf.Approximately(aspect, Screen.width / (float)Screen.height))
            {
                Apply();
                RestoreFullscreen();
            }
        }

        private void Apply()
        {
            if (scaler == null || Screen.height <= 0)
            {
                return;
            }
            aspect = Screen.width / (float)Screen.height;
            var reference = scaler.referenceResolution.x / scaler.referenceResolution.y;
            scaler.matchWidthOrHeight = aspect >= reference ? 1f : 0f;
        }
    }
}
