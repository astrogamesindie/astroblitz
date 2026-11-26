using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public static class Juice
    {
        public static void Shake(float strength, float duration)
        {
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.Shake(strength, duration);
        }

        public static void HitStop(float duration)
        {
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.HitStop(duration);
        }

        public static void ScreenFlash(Color color, float duration)
        {
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.ScreenFlash(color, duration);
        }

        /// <summary>A red rim around the screen that fades out; intensity 0 to 1.</summary>
        public static void DamageVignette(float intensity)
        {
            if (JuiceDirector.Instance != null)
                JuiceDirector.Instance.Vignette(Palette.Magenta, Mathf.Clamp01(intensity) * 0.6f, 0.45f);
        }

        public static void Number(Vector3 position, string value, Color color, float size)
        {
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.Number(position, value, color, size);
        }

        public static void PopIn(Transform target, float scale, float duration,
            float delay = 0f, bool ignoreTimeScale = false)
        {
            if (target == null) return;
            LeanTween.cancel(target.gameObject, false, TweenAction.SCALE);
            target.localScale = Vector3.one * (scale * 0.15f);
            LeanTween.scale(target.gameObject, Vector3.one * scale, duration)
                .setDelay(delay).setEase(LeanTweenType.easeOutBack)
                .setIgnoreTimeScale(ignoreTimeScale);
        }

        // The scale each target rests at while a punch is in flight, by instance
        // id (a value type, so a destroyed target leaves nothing dangling). An entry
        // stops counting once its punch would have finished.
        private struct PunchRest
        {
            public Vector3 scale;
            public float until;
        }

        private static readonly Dictionary<EntityId, PunchRest> punchRests = new Dictionary<EntityId, PunchRest>();

        /// <summary>Forgets in-flight punches. Called at the start of every play session.</summary>
        public static void ResetPunches()
        {
            punchRests.Clear();
        }

        public static void Punch(Transform target, float amount, float duration)
        {
            if (target == null) return;

            // A punch that starts while another is still running must return to
            // the scale the first one left from, not to wherever it is now. Read
            // from the current scale, every repeat ratcheted the rest scale up:
            // Furia Implacavel heals on every kill and grew the hull panel until
            // it covered half the screen.
            var id = target.GetEntityId();
            var now = Time.unscaledTime;
            PunchRest rest;
            var baseScale = punchRests.TryGetValue(id, out rest) && now < rest.until
                ? rest.scale
                : target.localScale;
            punchRests[id] = new PunchRest { scale = baseScale, until = now + duration };

            LeanTween.cancel(target.gameObject, false, TweenAction.SCALE);
            LeanTween.scale(target.gameObject, baseScale * (1f + amount), duration * 0.35f)
                .setEase(LeanTweenType.easeOutQuad).setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    if (target != null)
                        LeanTween.scale(target.gameObject, baseScale, duration * 0.65f)
                            .setEase(LeanTweenType.easeOutBack).setIgnoreTimeScale(true);
                });
        }

        public static void DealIn(RectTransform target, Vector2 offset,
            float delay = 0f, float duration = 0.4f)
        {
            if (target == null) return;
            LeanTween.cancel(target.gameObject);
            var destination = target.anchoredPosition;
            target.anchoredPosition = destination + offset;
            LeanTween.value(target.gameObject, position =>
            {
                if (target != null) target.anchoredPosition = position;
            }, target.anchoredPosition, destination, duration)
                .setDelay(delay).setEase(LeanTweenType.easeOutCubic)
                .setIgnoreTimeScale(true);
        }

        public static void Fade(CanvasGroup group, float alpha, float duration,
            float delay = 0f, Action completed = null)
        {
            if (group == null) return;
            LeanTween.value(group.gameObject, value =>
            {
                if (group != null) group.alpha = value;
            }, group.alpha, alpha, duration).setDelay(delay)
                .setIgnoreTimeScale(true).setOnComplete(() => completed?.Invoke());
        }

        public static void FillTo(Image image, float amount, float duration)
        {
            if (image == null) return;
            LeanTween.cancel(image.gameObject, false, TweenAction.CALLBACK);
            LeanTween.value(image.gameObject, value =>
            {
                if (image != null) image.fillAmount = value;
            }, image.fillAmount, amount, duration).setIgnoreTimeScale(true);
        }

        public static void FlashSprite(SpriteRenderer renderer, Color flash,
            Color original, float duration)
        {
            if (renderer == null) return;
            renderer.color = flash;
            LeanTween.value(renderer.gameObject, value =>
            {
                if (renderer != null) renderer.color = Color.Lerp(flash, original, value);
            }, 0f, 1f, duration);
        }

        public static void TintGraphic(Graphic graphic, Color color, float duration)
        {
            if (graphic == null) return;
            var original = graphic.color;
            LeanTween.value(graphic.gameObject, value =>
            {
                if (graphic != null) graphic.color = Color.Lerp(original, color, value);
            }, 0f, 1f, duration).setIgnoreTimeScale(true);
        }

        public static void Breathe(SpriteRenderer renderer, float minAlpha,
            float maxAlpha, float duration)
        {
            if (renderer == null) return;
            LeanTween.value(renderer.gameObject, alpha =>
            {
                if (renderer == null) return;
                var color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }, minAlpha, maxAlpha, duration).setLoopPingPong();
        }

        public static void BreatheUI(Graphic graphic, float minAlpha,
            float maxAlpha, float duration)
        {
            if (graphic == null) return;
            LeanTween.value(graphic.gameObject, alpha =>
            {
                if (graphic == null) return;
                var color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }, minAlpha, maxAlpha, duration).setLoopPingPong()
                .setIgnoreTimeScale(true);
        }
    }
}
