using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public class JuiceDirector : MonoBehaviour
    {
        public static JuiceDirector Instance { get; private set; }

        private readonly Stack<DamageNumber> numberPool = new Stack<DamageNumber>();
        private readonly List<DamageNumber> liveNumbers = new List<DamageNumber>();
        private Camera targetCamera;
        private Vector3 cameraPosition;
        private float shakeTime;
        private float shakeDuration;
        private float shakeStrength;
        private float hitStopEnd;
        private float previousTimeScale = 1f;
        private Image flashImage;
        private Canvas overlayCanvas;
        private Image vignetteImage;
        private Sprite vignetteSprite;

        private void Awake()
        {
            Instance = this;
        }

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            if (camera != null) cameraPosition = camera.transform.localPosition;
        }

        private void Update()
        {
            if (targetCamera != null && shakeTime > 0f)
            {
                shakeTime = Mathf.Max(0f, shakeTime - Time.unscaledDeltaTime);
                var amount = shakeStrength * (shakeDuration > 0f ? shakeTime / shakeDuration : 0f);
                targetCamera.transform.localPosition = cameraPosition +
                    (Vector3)(Random.insideUnitCircle * amount);
                if (shakeTime == 0f) targetCamera.transform.localPosition = cameraPosition;
            }
            if (hitStopEnd > 0f && Time.unscaledTime >= hitStopEnd) ClearHitStop();
        }

        public void Shake(float strength, float duration)
        {
            if (targetCamera == null || duration <= 0f) return;
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeDuration = shakeTime = Mathf.Max(shakeTime, duration);
        }

        public void HitStop(float duration)
        {
            if (duration <= 0f || Time.timeScale == 0f) return;
            if (hitStopEnd <= 0f) previousTimeScale = Time.timeScale;
            hitStopEnd = Mathf.Max(hitStopEnd, Time.unscaledTime + duration);
            Time.timeScale = 0.05f;
        }

        public void ClearHitStop()
        {
            if (hitStopEnd > 0f && Mathf.Approximately(Time.timeScale, 0.05f))
                Time.timeScale = previousTimeScale;
            hitStopEnd = 0f;
        }

        public void ScreenFlash(Color color, float duration)
        {
            if (duration <= 0f) return;
            if (flashImage == null) flashImage = OverlayImage("Flash");
            LeanTween.cancel(flashImage.gameObject);
            flashImage.color = color;
            LeanTween.value(flashImage.gameObject, alpha =>
            {
                if (flashImage == null) return;
                var current = flashImage.color;
                current.a = alpha;
                flashImage.color = current;
            }, color.a, 0f, duration).setIgnoreTimeScale(true);
        }

        /// <summary>
        /// A coloured rim around the screen (clear in the middle), faded out
        /// over the duration. Used for hits on the ship: the edge of vision
        /// reddens without hiding the fight.
        /// </summary>
        public void Vignette(Color color, float intensity, float duration)
        {
            if (duration <= 0f || intensity <= 0f) return;
            if (vignetteImage == null)
            {
                vignetteImage = OverlayImage("Vignette");
                vignetteImage.sprite = VignetteSprite();
                vignetteImage.transform.SetAsFirstSibling();
            }
            LeanTween.cancel(vignetteImage.gameObject);
            // Stack with a vignette still fading instead of restarting lower.
            color.a = Mathf.Max(vignetteImage.color.a, Mathf.Clamp01(intensity));
            vignetteImage.color = color;
            LeanTween.value(vignetteImage.gameObject, alpha =>
            {
                if (vignetteImage == null) return;
                var current = vignetteImage.color;
                current.a = alpha;
                vignetteImage.color = current;
            }, color.a, 0f, duration).setEase(LeanTweenType.easeInQuad).setIgnoreTimeScale(true);
        }

        /// <summary>A full-screen image on the effects canvas, above the game's UI.</summary>
        private Image OverlayImage(string name)
        {
            if (overlayCanvas == null)
            {
                var canvasObject = new GameObject("ScreenFlash", typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(transform, false);
                overlayCanvas = canvasObject.GetComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 1000;
            }
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(overlayCanvas.transform, false);
            var image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>White with alpha rising towards the edges; built once, 64 x 64.</summary>
        private Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    // Rounded-rectangle distance: the rim hugs all four edges.
                    var edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                    var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.1f, edge));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            vignetteSprite.hideFlags = HideFlags.HideAndDontSave;
            return vignetteSprite;
        }

        public void Number(Vector3 position, string value, Color color, float size)
        {
            DamageNumber number = null;
            while (number == null && numberPool.Count > 0) number = numberPool.Pop();
            if (number == null)
            {
                var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.damageNumber : null;
                if (prefab != null) number = Instantiate(prefab, transform);
                else
                {
                    var go = new GameObject("DamageNumber");
                    go.transform.SetParent(transform, false);
                    go.SetActive(false);
                    var label = go.AddComponent<TextMeshPro>();
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 3.4f;
                    number = go.AddComponent<DamageNumber>();
                }
            }
            liveNumbers.Add(number);
            number.Show(position, value, color, size, ReturnNumber);
        }

        private void ReturnNumber(DamageNumber number)
        {
            if (number == null || !liveNumbers.Remove(number)) return;
            number.Hide();
            numberPool.Push(number);
        }

        public void ClearNumbers()
        {
            for (var i = liveNumbers.Count - 1; i >= 0; i--)
            {
                var number = liveNumbers[i];
                if (number == null) continue;
                number.Hide();
                numberPool.Push(number);
            }
            liveNumbers.Clear();
        }

        private void OnDestroy()
        {
            ClearHitStop();
            ClearNumbers();
            if (vignetteSprite != null)
            {
                Destroy(vignetteSprite.texture);
                Destroy(vignetteSprite);
            }
            if (targetCamera != null) targetCamera.transform.localPosition = cameraPosition;
            if (Instance == this) Instance = null;
        }
    }
}
