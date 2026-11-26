using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Fits its RectTransform to Screen.safeArea, so nothing the player has to
    /// read or tap ends up under a notch, a punch-hole camera or the rounded
    /// corners of a phone held in landscape. Backgrounds that should bleed to
    /// the edge live outside it (see UIFactory.Screen).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int screen;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            var rect = (RectTransform)transform;
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // Clamped to the screen: right after a rotation (and in the editor
            // when the Game view changes shape) Unity can still report the safe
            // area of the previous orientation, which reaches past the screen.
            // A safe area turned the other way, or one cutting more than a
            // quarter of a side (notches take far less), is such a stale value.
            var min = Vector2.Max(applied.position, Vector2.zero);
            var max = Vector2.Min(applied.position + applied.size, new Vector2(Screen.width, Screen.height));
            var stale = (applied.width > applied.height) != (Screen.width > Screen.height);
            if (stale || max.x - min.x < Screen.width * 0.75f || max.y - min.y < Screen.height * 0.75f)
            {
                min = Vector2.zero;
                max = new Vector2(Screen.width, Screen.height);
            }
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (AndroidAds.Instance != null) AndroidAds.Instance.RefreshLayout();
        }
    }
}
