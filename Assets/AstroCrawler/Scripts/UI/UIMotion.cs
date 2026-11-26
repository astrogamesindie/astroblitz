// AstroCrawler - how screens and sheets enter, leave and hand over (LeanTween).
//
// Only scale and alpha are animated, never position, so a motion can never
// fight the layout (rotation rebuilds, safe area, ScreenFit). Everything runs
// on unscaled time: pause, card picks and menus hold the game at time scale 0.
// A new motion on an object cancels only the tweens this class started there,
// never the object's other tweens or delayed calls.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public static class UIMotion
    {
        public const float EnterTime = 0.3f;
        public const float ExitTime = 0.16f;

        // Tweens started here, by object, so they can be stopped selectively.
        private static readonly Dictionary<EntityId, List<int>> running = new Dictionary<EntityId, List<int>>();

        /// <summary>
        /// A screen arriving: it fades in while settling from slightly larger,
        /// which also hides nothing at the edges of a full-screen page.
        /// </summary>
        public static void Enter(CanvasGroup group, float time = EnterTime, float fromScale = 1.045f, float delay = 0f)
        {
            if (group == null) return;
            var go = group.gameObject;
            Stop(go);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;
            go.transform.localScale = Vector3.one * fromScale;
            Track(go, LeanTween.scale(go, Vector3.one, time).setDelay(delay)
                .setEase(LeanTweenType.easeOutCubic).setIgnoreTimeScale(true));
            Track(go, LeanTween.value(go, v => { if (group != null) group.alpha = v; }, 0f, 1f, time * 0.75f)
                .setDelay(delay).setEase(LeanTweenType.easeOutQuad).setIgnoreTimeScale(true));
        }

        /// <summary>
        /// A screen leaving: input is cut at once, it fades while drifting a
        /// little larger, and <paramref name="done"/> runs at the end (usually
        /// deactivating it). Scale and alpha are put back for the next entry.
        /// </summary>
        public static void Exit(CanvasGroup group, Action done, float time = ExitTime, float toScale = 1.03f)
        {
            if (group == null)
            {
                done?.Invoke();
                return;
            }
            var go = group.gameObject;
            if (!go.activeInHierarchy)
            {
                done?.Invoke();
                return;
            }
            Stop(go);
            group.blocksRaycasts = false;
            group.interactable = false;
            Track(go, LeanTween.scale(go, Vector3.one * toScale, time).setEase(LeanTweenType.easeInQuad)
                .setIgnoreTimeScale(true));
            Track(go, LeanTween.value(go, v => { if (group != null) group.alpha = v; }, group.alpha, 0f, time)
                .setEase(LeanTweenType.easeInQuad).setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    Forget(go);
                    if (group == null) return;
                    go.transform.localScale = Vector3.one;
                    done?.Invoke();
                    group.alpha = 1f;
                    group.blocksRaycasts = true;
                    group.interactable = true;
                }));
        }

        /// <summary>Exit, then deactivate.</summary>
        public static void Hide(GameObject target)
        {
            if (target == null || !target.activeSelf) return;
            Exit(UIFactory.Group(target.GetComponent<RectTransform>()), () =>
            {
                if (target != null) target.SetActive(false);
            });
        }

        /// <summary>
        /// A sheet or card landing: grows from small with a soft overshoot.
        /// For panels inside a screen whose backdrop fades on its own.
        /// </summary>
        public static void Pop(Transform panel, float delay = 0f, float time = 0.34f, float from = 0.86f)
        {
            if (panel == null) return;
            var go = panel.gameObject;
            Stop(go);
            panel.localScale = Vector3.one * from;
            Track(go, LeanTween.scale(go, Vector3.one, time).setDelay(delay)
                .setEase(LeanTweenType.easeOutBack).setIgnoreTimeScale(true));
        }

        /// <summary>
        /// Elements of a screen arriving one after the other (menu tiles, rows,
        /// day cards): each pops in a beat after the previous one.
        /// </summary>
        public static void Stagger(IList<Transform> items, float delay = 0.06f, float step = 0.045f)
        {
            if (items == null) return;
            var k = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                Pop(item, delay + step * k, 0.32f, 0.7f);
                k++;
            }
        }

        private static void Track(GameObject go, LTDescr tween)
        {
            if (go == null || tween == null) return;
            var key = go.GetEntityId();
            List<int> ids;
            if (!running.TryGetValue(key, out ids))
            {
                ids = new List<int>(4);
                running[key] = ids;
            }
            ids.Add(tween.uniqueId);
        }

        private static void Forget(GameObject go)
        {
            if (go != null) running.Remove(go.GetEntityId());
        }

        private static void Stop(GameObject go)
        {
            if (go == null) return;
            var key = go.GetEntityId();
            List<int> ids;
            if (!running.TryGetValue(key, out ids)) return;
            for (var i = 0; i < ids.Count; i++) LeanTween.cancel(go, ids[i]);
            running.Remove(key);
            go.transform.localScale = Vector3.one;
        }
    }
}
