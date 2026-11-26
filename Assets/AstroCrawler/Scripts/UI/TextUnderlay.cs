// AstroCrawler - remembers that a label wants a drop shadow.
//
// The shadow is a TMP material feature, and the material is an instance created
// from a font asset that this project builds at runtime. Neither survives being
// saved into the scene, so the *intent* is stored here as plain serialized
// numbers and re-applied on load.

using TMPro;

using UnityEngine;

namespace AstroCrawler
{
    [RequireComponent(typeof(TMP_Text))]
    public class TextUnderlay : MonoBehaviour
    {
        public float softness = 0.2f;
        public float dilate = 0.1f;

        private TMP_Text label;

        /// <summary>
        /// Assigns the font when it is missing and re-applies the underlay.
        /// Safe to call repeatedly.
        /// </summary>
        public void Apply()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }
            if (label == null)
            {
                return;
            }

            if (label.font == null)
            {
                label.font = UIFactory.TmpFont;
            }

            UIFactory.ApplyUnderlay(label, softness, dilate);
        }
    }
}
