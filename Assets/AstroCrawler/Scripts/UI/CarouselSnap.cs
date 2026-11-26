// AstroCrawler - snaps a horizontal ScrollRect so one card sits in the middle.


using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroCrawler
{
    /// <summary>
    /// Lives next to a horizontal ScrollRect whose cards are evenly spaced.
    /// When a drag ends it glides to the nearest card (a quick flick moves on
    /// to the next one), and the side arrows step one card at a time. The
    /// arrows dim at either end.
    /// </summary>
    public class CarouselSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        // Positions are in the content's own units: the centre of the first
        // card measured from the content's left edge, and the card-to-card step.
        private ScrollRect scroll;
        private float firstCentre;
        private float step;
        private int count;
        private Button previous;
        private Button next;
        private int index;

        /// <summary>A flick at least this fast (units/s) moves to the next card.</summary>
        private const float FlickSpeed = 900f;
        private const float GlideTime = 0.3f;

        public int Index { get { return index; } }

        // Optional: the cards themselves, in index order. The one in the middle
        // is fully lit and its neighbours fade with their distance from it, so a
        // card cut by the screen edge reads as "more this way", not as a glitch.
        private readonly System.Collections.Generic.List<CanvasGroup> focusGroups =
            new System.Collections.Generic.List<CanvasGroup>();
        private float focusMinimum = 1f;

        /// <summary>Fades the cards away from the middle down to <paramref name="minimumAlpha"/>.</summary>
        public void SetFocusFade(System.Collections.Generic.IList<RectTransform> cards, float minimumAlpha)
        {
            focusGroups.Clear();
            focusMinimum = minimumAlpha;
            if (cards == null) return;
            for (var i = 0; i < cards.Count; i++)
                focusGroups.Add(cards[i] != null ? UIFactory.Group(cards[i]) : null);
        }

        private void LateUpdate()
        {
            if (focusGroups.Count == 0 || scroll == null || scroll.content == null || scroll.viewport == null) return;
            var exact = CurrentIndexExact();
            for (var i = 0; i < focusGroups.Count; i++)
            {
                var group = focusGroups[i];
                if (group == null) continue;
                group.alpha = Mathf.Lerp(1f, focusMinimum, Mathf.Clamp01(Mathf.Abs(i - exact)));
            }
        }

        public void Configure(ScrollRect target, float firstCardCentre, float cardStep, int cards,
            Button previousArrow, Button nextArrow)
        {
            scroll = target;
            firstCentre = firstCardCentre;
            step = cardStep;
            count = cards;
            previous = previousArrow;
            next = nextArrow;
            if (previous != null)
            {
                previous.onClick.RemoveAllListeners();
                previous.onClick.AddListener(() => Step(-1));
            }
            if (next != null)
            {
                next.onClick.RemoveAllListeners();
                next.onClick.AddListener(() => Step(1));
            }
            RefreshArrows();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (scroll != null) LeanTween.cancel(scroll.content.gameObject);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (scroll == null || count <= 0) return;
            var nearest = NearestIndex();
            // A flick goes one card further in the direction of the throw.
            var velocity = scroll.velocity.x;
            if (Mathf.Abs(velocity) > FlickSpeed)
            {
                var current = CurrentIndexExact();
                nearest = velocity < 0f ? Mathf.CeilToInt(current + 0.01f) : Mathf.FloorToInt(current - 0.01f);
            }
            GlideTo(nearest, false);
        }

        /// <summary>One card left (-1) or right (+1).</summary>
        public void Step(int direction)
        {
            GlideTo(NearestIndex() + direction, false);
        }

        /// <summary>Centres a card, gliding or at once.</summary>
        public void GlideTo(int target, bool instant)
        {
            if (scroll == null || scroll.content == null || count <= 0) return;
            index = Mathf.Clamp(target, 0, count - 1);
            Canvas.ForceUpdateCanvases();

            var content = scroll.content;
            var viewport = ((RectTransform)scroll.viewport).rect.width;
            var travel = Mathf.Max(0f, content.rect.width - viewport);
            var x = -Mathf.Clamp(firstCentre + index * step - viewport * 0.5f, 0f, travel);

            LeanTween.cancel(content.gameObject);
            scroll.StopMovement();
            if (instant)
            {
                content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
            }
            else
            {
                LeanTween.value(content.gameObject, value =>
                {
                    if (content != null) content.anchoredPosition = new Vector2(value, content.anchoredPosition.y);
                }, content.anchoredPosition.x, x, GlideTime)
                    .setEase(LeanTweenType.easeOutCubic).setIgnoreTimeScale(true);
            }
            RefreshArrows();
        }

        private float CurrentIndexExact()
        {
            var viewport = ((RectTransform)scroll.viewport).rect.width;
            var centre = -scroll.content.anchoredPosition.x + viewport * 0.5f;
            return step > 0f ? (centre - firstCentre) / step : 0f;
        }

        private int NearestIndex()
        {
            return Mathf.Clamp(Mathf.RoundToInt(CurrentIndexExact()), 0, Mathf.Max(0, count - 1));
        }

        private void RefreshArrows()
        {
            SetArrow(previous, index > 0);
            SetArrow(next, index < count - 1);
        }

        private static void SetArrow(Button arrow, bool enabled)
        {
            if (arrow == null) return;
            arrow.interactable = enabled;
            var group = UIFactory.Group(arrow);
            group.alpha = enabled ? 1f : 0.3f;
        }
    }
}
