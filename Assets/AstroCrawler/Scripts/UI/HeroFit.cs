// AstroCrawler - keeps the main menu's ship group inside its zone.

using UnityEngine;
using UnityEngine.EventSystems;

namespace AstroCrawler
{
    /// <summary>
    /// Scales <see cref="content"/> down (never up) so its height fits the
    /// RectTransform this sits on. The menu's hero zone is whatever is left
    /// between the logo and the action block, which shrinks on short screens
    /// and under the ad banner; at full size the ship's level chip ran into
    /// the "MODO DE JOGO" caption.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroFit : UIBehaviour
    {
        public RectTransform content;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Fit();
        }

        private void Fit()
        {
            if (content == null) return;
            var available = ((RectTransform)transform).rect.height;
            var needed = content.rect.height;
            if (available <= 0f || needed <= 0f) return;
            content.localScale = Vector3.one * Mathf.Min(1f, available / needed);
        }
    }
}
