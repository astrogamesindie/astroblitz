// AstroCrawler - fits the fixed combat camera to the screen's shape.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Keeps the arena framed on any screen. In landscape the camera shows the
    /// usual half-height of 7; in portrait it widens until the detection ring
    /// and a margin fit across, which shows more space above and below the
    /// ship. It also publishes the visible area (Arena.ViewHalfSize) for spawns,
    /// and turns the starfield a quarter so its band covers a tall screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ArenaCamera : MonoBehaviour
    {
        private Camera view;
        private Transform starfield;
        private float aspect = -1f;

        /// <summary>The star planes behind the arena (the warp stretches them).</summary>
        public Transform Starfield { get { return starfield; } }

        public void Configure(Transform stars)
        {
            starfield = stars;
            aspect = -1f;
            Apply();
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (view != null && !Mathf.Approximately(view.aspect, aspect)) Apply();
        }

        private void Apply()
        {
            if (view == null) view = GetComponent<Camera>();
            if (view == null) return;

            aspect = view.aspect;
            view.orthographicSize = Arena.FitCameraSize(aspect);
            Arena.SetView(new Vector2(view.orthographicSize * aspect, view.orthographicSize));

            // The star band is 32 x 20 units, laid out for landscape.
            if (starfield != null)
                starfield.localRotation = Quaternion.Euler(0f, 0f, aspect < 1f ? 90f : 0f);
        }
    }
}
