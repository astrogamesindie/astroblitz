using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroCrawler
{
    /// <summary>
    /// Press feel for every button: a scale response and a sound. The sound is
    /// per button (a back button sounds different from a buy button) and plays
    /// on click, not on press, so dragging a finger off a button stays silent.
    /// </summary>
    public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Tooltip("Played when the button is clicked. None for buttons whose handler plays " +
                 "its own, context-dependent sound (buy: purchase or denied).")]
        public Sound clickSound = Sound.Tap;

        private RectTransform target;
        private Vector3 baseScale;
        private bool hovered;
        private Selectable selectable;

        public void Initialize(RectTransform rect)
        {
            target = rect;
            baseScale = rect.localScale;
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (target == null)
            {
                Initialize((RectTransform)transform);
            }
        }

        private bool Interactable
        {
            get { return selectable == null || selectable.IsInteractable(); }
        }

        private void Animate(float multiplier)
        {
            if (target == null) return;
            LeanTween.cancel(target.gameObject, false, TweenAction.SCALE);
            LeanTween.scale(target.gameObject, baseScale * multiplier, 0.14f)
                .setEase(LeanTweenType.easeOutQuad).setIgnoreTimeScale(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Animate(1.04f);

            // Mouse pointers have negative ids; a finger's is zero or more. On
            // a touch screen "enter" arrives with the tap itself, and a hover
            // sound there would just double the click.
            if (eventData.pointerId < 0 && Interactable) Sfx.Play(Sound.Hover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Animate(1f);
        }

        public void OnPointerDown(PointerEventData eventData) { Animate(0.94f); }
        public void OnPointerUp(PointerEventData eventData) { Animate(hovered ? 1.04f : 1f); }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable) Sfx.Play(clickSound);
        }

        private void OnDisable()
        {
            if (target != null) target.localScale = baseScale;
            hovered = false;
        }
    }
}
