// AstroCrawler - coins that burst out of a reward and fly into the credits counter.
//
// One overlay (its own nested canvas, on top of everything) owns a fixed pool
// of coin and spark images. A burst:
//   1. flashes a ring at the source and throws the coins outwards, spinning;
//   2. after a short, staggered hang, each coin curves into the counter's icon;
//   3. every landing punches the counter, sparkles and ticks the number up,
//      and the last one plays the purchase sound and settles the final value.
// Everything runs on unscaled time: the result screen and the menu may sit at
// timeScale 0. Nothing is allocated per frame; the pool never grows.

using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public sealed class CoinBurst : MonoBehaviour
    {
        private const int CoinPool = 64;
        private const int SparkPool = 160;
        private const int MinCoins = 8;
        private const int MaxCoins = 22;
        private const float CoinSize = 66f;

        private static CoinBurst instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private struct Coin
        {
            public RectTransform rect;
            public RectTransform haloRect;
            public Image image;
            public Image halo;
            public Burst burst;
            public float start;
            public float explode;
            public float hang;
            public float fly;
            public Vector2 origin;
            public Vector2 peak;
            public Vector2 bendOffset;
            public float spin;
            public float spinSpeed;
            public float size;
            public float nextTrail;
        }

        private struct Spark
        {
            public RectTransform rect;
            public Image image;
            public bool active;
            public float start;
            public float life;
            public Vector2 from;
            public Vector2 travel;
            public float size0;
            public float size1;
            public Color color;
            public bool ring;
        }

        private sealed class Burst
        {
            /// <summary>The real counter element: when it goes away, the burst settles.</summary>
            public RectTransform anchor;
            public RectTransform landing;
            public bool ghost;
            public TMP_Text ghostCounter;
            public Transform punch;
            public TMP_Text counter;
            public Color counterColor;
            public int from;
            public int to;
            public int total;
            public int landed;
            public float shown;
            public int written;
            public float flash;
            public Color tint;
            public Action done;
        }

        private RectTransform layer;
        private Coin[] coins;
        private bool[] coinActive;
        private Spark[] sparks;
        private readonly List<Burst> bursts = new List<Burst>();
        private Sprite coinSprite;
        private Sprite glowSprite;
        private Sprite ringSprite;

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Throws coins out of <paramref name="sourceWorld"/> and flies them into
        /// <paramref name="landing"/>. The <paramref name="counter"/> (optional)
        /// counts from <paramref name="fromValue"/> to <paramref name="toValue"/>
        /// as they land; <paramref name="punch"/> bounces on each landing.
        /// <paramref name="done"/> runs once, when the last coin lands or when the
        /// counter disappears first.
        /// </summary>
        public static void Play(Transform canvasRoot, Vector3 sourceWorld, RectTransform landing, Transform punch,
            TMP_Text counter, int fromValue, int toValue, Color tint, Action done = null, bool highlight = false)
        {
            if (canvasRoot == null || landing == null || !landing.gameObject.activeInHierarchy || toValue <= fromValue)
            {
                if (counter != null) counter.text = toValue.ToString();
                if (done != null) done();
                return;
            }
            Ensure(canvasRoot).Launch(sourceWorld, landing, punch, counter, fromValue, toValue, tint, done, highlight);
        }

        /// <summary>True while a burst is counting on this label: refreshes should leave its text alone.</summary>
        public static bool Holds(TMP_Text counter)
        {
            if (instance == null || counter == null) return false;
            var list = instance.bursts;
            for (var i = 0; i < list.Count; i++)
                if (list[i].counter == counter) return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private static CoinBurst Ensure(Transform canvasRoot)
        {
            if (instance != null)
            {
                instance.transform.SetAsLastSibling();
                return instance;
            }

            var go = new GameObject("CoinBurstFx", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            // Its own canvas: dozens of moving images rebuild only this batch,
            // not the menu, and always draw above every screen and popup.
            var nested = go.AddComponent<Canvas>();
            nested.overrideSorting = true;
            nested.sortingOrder = 500;

            instance = go.AddComponent<CoinBurst>();
            instance.Build(rect);
            return instance;
        }

        private void Build(RectTransform root)
        {
            layer = root;
            coinSprite = ArtLibrary.IconCredits;
            glowSprite = ArtLibrary.Glow;
            ringSprite = ArtLibrary.CircleOutline;

            sparks = new Spark[SparkPool];
            for (var i = 0; i < SparkPool; i++)
            {
                var image = NewImage(layer, "Spark", glowSprite);
                sparks[i].rect = image.rectTransform;
                sparks[i].image = image;
                image.gameObject.SetActive(false);
            }

            coins = new Coin[CoinPool];
            coinActive = new bool[CoinPool];
            for (var i = 0; i < CoinPool; i++)
            {
                var halo = NewImage(layer, "CoinHalo", glowSprite);
                var image = NewImage(layer, "Coin", coinSprite);
                image.preserveAspect = true;
                coins[i].rect = image.rectTransform;
                coins[i].image = image;
                coins[i].halo = halo;
                coins[i].haloRect = halo.rectTransform;
                halo.gameObject.SetActive(false);
                image.gameObject.SetActive(false);
            }
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // ------------------------------------------------------------------
        // Launch
        // ------------------------------------------------------------------

        private void Launch(Vector3 sourceWorld, RectTransform landing, Transform punch, TMP_Text counter,
            int fromValue, int toValue, Color tint, Action done, bool highlight)
        {
            var amount = toValue - fromValue;
            var count = Mathf.Clamp(MinCoins + amount / 30, MinCoins, MaxCoins);

            var burst = new Burst
            {
                anchor = landing,
                landing = landing,
                punch = punch != null ? punch : landing,
                counter = counter,
                counterColor = counter != null ? counter.color : Color.white,
                from = fromValue,
                to = toValue,
                shown = fromValue,
                written = fromValue,
                tint = tint,
                done = done
            };
            if (counter != null) counter.text = fromValue.ToString();

            // The real counter may sit under a popup or page: draw a lit copy
            // of it on top, which the coins land on and which counts up.
            if (highlight && ShowGhost(burst.punch as RectTransform, landing, counter, tint, fromValue))
            {
                burst.ghost = true;
                burst.landing = ghostIcon != null ? ghostIcon : ghostRect;
                burst.punch = ghostRect;
                burst.ghostCounter = ghostLabel;
            }

            var origin = ToLayer(sourceWorld);
            var now = Time.unscaledTime;

            // The pop at the source: a flash, a ring and a spray of sparks.
            SpawnSpark(origin, Vector2.zero, 0.32f, 90f, 360f, WithAlpha(Color.white, 0.75f), false);
            SpawnSpark(origin, Vector2.zero, 0.5f, 60f, 480f, WithAlpha(tint, 0.9f), true);
            for (var i = 0; i < 12; i++)
            {
                var angle = i / 12f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnSpark(origin, dir * UnityEngine.Random.Range(160f, 300f), UnityEngine.Random.Range(0.35f, 0.55f),
                    UnityEngine.Random.Range(26f, 40f), 6f, WithAlpha(tint, 0.95f), false);
            }

            var launched = 0;
            for (var i = 0; i < CoinPool && launched < count; i++)
            {
                if (coinActive[i]) continue;
                coinActive[i] = true;
                launched++;

                ref var coin = ref coins[i];
                coin.burst = burst;
                coin.start = now + launched * 0.012f;
                coin.explode = UnityEngine.Random.Range(0.3f, 0.42f);
                // Staggered hang: they leave one after another, a stream not a clump.
                coin.hang = 0.06f + launched * 0.035f + UnityEngine.Random.Range(0f, 0.05f);
                coin.fly = UnityEngine.Random.Range(0.42f, 0.56f);
                coin.origin = origin;
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var radius = UnityEngine.Random.Range(110f, 280f);
                // A slight upward bias reads as "fountain" rather than "explosion".
                coin.peak = origin + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.85f + 40f);
                coin.bendOffset = new Vector2(UnityEngine.Random.Range(-260f, 260f), UnityEngine.Random.Range(60f, 220f));
                coin.spin = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                coin.spinSpeed = UnityEngine.Random.Range(9f, 16f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                coin.size = CoinSize * UnityEngine.Random.Range(0.85f, 1.15f);
                coin.nextTrail = 0f;

                coin.image.color = tint;
                coin.halo.color = WithAlpha(tint, 0.45f);
                coin.rect.anchoredPosition = origin;
                coin.haloRect.anchoredPosition = origin;
                coin.rect.localScale = Vector3.zero;
                coin.haloRect.localScale = Vector3.zero;
                coin.rect.sizeDelta = new Vector2(coin.size, coin.size);
                coin.haloRect.sizeDelta = new Vector2(coin.size * 2.1f, coin.size * 2.1f);
                coin.halo.gameObject.SetActive(true);
                coin.image.gameObject.SetActive(true);
            }

            burst.total = Mathf.Max(1, launched);
            bursts.Add(burst);
            if (launched == 0) Finish(burst);
        }

        // ------------------------------------------------------------------
        // Simulation
        // ------------------------------------------------------------------

        private void Update()
        {
            var now = Time.unscaledTime;
            var dt = Time.unscaledDeltaTime;

            // A counter that vanished (screen closed) settles at once.
            for (var b = bursts.Count - 1; b >= 0; b--)
            {
                var burst = bursts[b];
                if (burst.anchor == null || !burst.anchor.gameObject.activeInHierarchy) Abort(burst);
            }
            UpdateGhost(now);

            for (var i = 0; i < CoinPool; i++)
            {
                if (!coinActive[i]) continue;
                UpdateCoin(i, now, dt);
            }

            for (var i = 0; i < SparkPool; i++)
            {
                if (!sparks[i].active) continue;
                UpdateSpark(ref sparks[i], now);
            }

            for (var b = bursts.Count - 1; b >= 0; b--)
                UpdateCounter(bursts[b], dt);
        }

        private void UpdateCoin(int index, float now, float dt)
        {
            ref var coin = ref coins[index];
            var t = now - coin.start;
            if (t < 0f) return;

            coin.spin += coin.spinSpeed * dt;
            // A flat jewel turning on its vertical axis: width follows the cosine.
            var flip = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(coin.spin));
            Vector2 position;
            float scale;

            if (t < coin.explode)
            {
                var u = t / coin.explode;
                position = Vector2.LerpUnclamped(coin.origin, coin.peak, EaseOutCubic(u));
                scale = EaseOutBack(u) * 1.15f;
            }
            else if (t < coin.explode + coin.hang)
            {
                // Hovering: a soft bob while it waits its turn.
                var h = t - coin.explode;
                position = coin.peak + new Vector2(0f, Mathf.Sin(h * 9f + coin.spin) * 6f);
                scale = Mathf.Lerp(1.15f, 1f, Mathf.Clamp01(h / 0.12f));
            }
            else
            {
                var u = (t - coin.explode - coin.hang) / coin.fly;
                var target = LandingPoint(coin.burst);
                if (u >= 1f)
                {
                    Land(index, target);
                    return;
                }
                // Accelerates into the counter along a curve, shrinking as it goes.
                var e = u * u * (1.6f - 0.6f * u);
                var bend = Vector2.Lerp(coin.peak, target, 0.35f) + coin.bendOffset;
                var a = Vector2.LerpUnclamped(coin.peak, bend, e);
                var c = Vector2.LerpUnclamped(bend, target, e);
                position = Vector2.LerpUnclamped(a, c, e);
                scale = Mathf.Lerp(1f, 0.55f, e);
                flip = Mathf.Lerp(flip, 1f, e);

                if (now >= coin.nextTrail)
                {
                    coin.nextTrail = now + 0.045f;
                    SpawnSpark(position, UnityEngine.Random.insideUnitCircle * 30f, 0.28f,
                        coin.size * 0.55f * scale, 4f, WithAlpha(coin.burst.tint, 0.7f), false);
                }
            }

            coin.rect.anchoredPosition = position;
            coin.rect.localScale = new Vector3(scale * flip, scale, 1f);
            coin.haloRect.anchoredPosition = position;
            var pulse = 0.9f + 0.15f * Mathf.Sin(coin.spin * 0.5f);
            coin.haloRect.localScale = new Vector3(scale * pulse, scale * pulse, 1f);
        }

        private void Land(int index, Vector2 target)
        {
            ref var coin = ref coins[index];
            var burst = coin.burst;
            Release(index);
            if (burst == null || !bursts.Contains(burst)) return;

            burst.landed++;
            burst.flash = 1f;
            var last = burst.landed >= burst.total;

            SpawnSpark(target, Vector2.zero, last ? 0.5f : 0.28f, 40f, last ? 300f : 150f,
                WithAlpha(burst.tint, last ? 1f : 0.8f), true);
            SpawnSpark(target, Vector2.zero, 0.2f, 60f, last ? 220f : 110f, WithAlpha(Color.white, 0.6f), false);
            var spray = last ? 10 : 3;
            for (var i = 0; i < spray; i++)
            {
                var dir = UnityEngine.Random.insideUnitCircle.normalized;
                SpawnSpark(target, dir * UnityEngine.Random.Range(90f, last ? 260f : 160f), 0.32f,
                    UnityEngine.Random.Range(18f, 30f), 4f, WithAlpha(burst.tint, 0.95f), false);
            }

            if (burst.punch != null) Juice.Punch(burst.punch, last ? 0.22f : 0.09f, last ? 0.4f : 0.18f);
            Sfx.Play(last ? Sound.Purchase : Sound.Select);
        }

        private void UpdateCounter(Burst burst, float dt)
        {
            var goal = burst.landed >= burst.total
                ? burst.to
                : burst.from + (burst.to - burst.from) * (float)burst.landed / burst.total;
            burst.shown = Mathf.Lerp(burst.shown, goal, 1f - Mathf.Exp(-16f * dt));
            if (Mathf.Abs(goal - burst.shown) < 0.5f) burst.shown = goal;
            burst.flash = Mathf.Max(0f, burst.flash - dt * 6f);

            var shownValue = Mathf.RoundToInt(burst.shown);
            if (shownValue != burst.written)
            {
                burst.written = shownValue;
                if (burst.counter != null) burst.counter.text = shownValue.ToString();
                if (burst.ghostCounter != null) burst.ghostCounter.text = shownValue.ToString();
            }
            if (burst.counter != null) burst.counter.color = Color.Lerp(burst.counterColor, Color.white, burst.flash);
            if (burst.ghostCounter != null) burst.ghostCounter.color = Color.Lerp(burst.counterColor, Color.white, burst.flash);

            if (burst.landed >= burst.total && burst.written == burst.to && burst.flash <= 0f) Finish(burst);
        }

        private void Abort(Burst burst)
        {
            for (var i = 0; i < CoinPool; i++)
                if (coinActive[i] && coins[i].burst == burst) Release(i);
            burst.landed = burst.total;
            burst.written = burst.to;
            burst.flash = 0f;
            if (burst.counter != null) burst.counter.text = burst.to.ToString();
            Finish(burst);
        }

        private void Finish(Burst burst)
        {
            if (!bursts.Remove(burst)) return;
            if (burst.counter != null) burst.counter.color = burst.counterColor;
            if (burst.ghost) HideGhost();
            var done = burst.done;
            burst.done = null;
            if (done != null) done();
        }

        private void Release(int index)
        {
            coinActive[index] = false;
            coins[index].burst = null;
            coins[index].image.gameObject.SetActive(false);
            coins[index].halo.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Highlighted counter ("ghost"): a lit copy of the real counter drawn
        // above everything while coins fly into a counter that is covered.
        // ------------------------------------------------------------------

        private const float GhostIn = 0.22f;
        private const float GhostHold = 0.45f;
        private const float GhostOut = 0.3f;

        private RectTransform ghostRoot;
        private CanvasGroup ghostGroup;
        private RectTransform ghostRect;
        private RectTransform ghostBody;
        private Image ghostGlow;
        private Image ghostBorder;
        private Image ghostPanel;
        private RectTransform ghostIcon;
        private Image ghostIconImage;
        private TextMeshProUGUI ghostLabel;
        private Color ghostTint;
        private float ghostShownAt = -1f;
        private float ghostHideAt = -1f;

        private bool ShowGhost(RectTransform chip, RectTransform icon, TMP_Text counter, Color tint, int value)
        {
            if (chip == null) return false;
            if (ghostRoot == null) BuildGhost();

            ghostRoot.gameObject.SetActive(true);
            ghostRoot.SetSiblingIndex(0); // under the coins and sparks
            ghostRect.localScale = Vector3.one;
            ghostBody.localScale = Vector3.one;
            ghostTint = tint;

            // The copy sits exactly over the real chip.
            Vector2 min, max;
            LayerBounds(chip, out min, out max);
            var center = (min + max) * 0.5f;
            ghostRect.anchoredPosition = center;
            ghostRect.sizeDelta = max - min;

            var chipImage = chip.GetComponent<Image>();
            CopyImage(ghostPanel, chipImage);
            if (chipImage == null) ghostPanel.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
            CopyImage(ghostBorder, chipImage);
            ghostBorder.color = tint;
            ghostGlow.color = WithAlpha(tint, 0.5f);

            var iconImage = icon != null && icon != chip ? icon.GetComponent<Image>() : null;
            ghostIcon.gameObject.SetActive(iconImage != null);
            if (iconImage != null)
            {
                CopyImage(ghostIconImage, iconImage);
                ghostIconImage.preserveAspect = iconImage.preserveAspect;
                PlaceOver(ghostIcon, icon, center);
            }

            if (counter == null) ghostLabel.gameObject.SetActive(false);
            else
            {
                ghostLabel.font = counter.font;
                ghostLabel.fontSharedMaterial = counter.fontSharedMaterial;
                ghostLabel.enableAutoSizing = false;
                ghostLabel.fontSize = counter.fontSize; // the size auto-sizing settled on
                ghostLabel.alignment = counter.alignment;
                ghostLabel.characterSpacing = counter.characterSpacing;
                ghostLabel.color = counter.color;
                ghostLabel.textWrappingMode = TextWrappingModes.NoWrap;
                ghostLabel.text = value.ToString();
                PlaceOver(ghostLabel.rectTransform, counter.rectTransform, center);
                ghostLabel.gameObject.SetActive(true);
            }

            ghostShownAt = Time.unscaledTime;
            ghostHideAt = -1f;
            UpdateGhost(ghostShownAt);
            return true;
        }

        private void BuildGhost()
        {
            ghostRoot = NewRect(layer, "CounterHighlight");
            ghostRoot.anchorMin = Vector2.zero;
            ghostRoot.anchorMax = Vector2.one;
            ghostRoot.offsetMin = Vector2.zero;
            ghostRoot.offsetMax = Vector2.zero;
            ghostGroup = ghostRoot.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;

            ghostRect = NewRect(ghostRoot, "Chip");
            ghostBody = NewRect(ghostRect, "Body");
            ghostGlow = NewImage(ghostBody, "Glow", glowSprite);
            ghostBorder = NewImage(ghostBody, "Border", null);
            ghostPanel = NewImage(ghostBody, "Panel", null);
            ghostIconImage = NewImage(ghostBody, "Icon", coinSprite);
            ghostIcon = ghostIconImage.rectTransform;
            var labelGo = new GameObject("Value", typeof(RectTransform));
            // Inactive until ShowGhost gives it the counter's font: TMP looks
            // for its default font when it wakes, and the project has none.
            labelGo.SetActive(false);
            labelGo.transform.SetParent(ghostBody, false);
            ghostLabel = labelGo.AddComponent<TextMeshProUGUI>();
            ghostLabel.raycastTarget = false;
            ghostRoot.gameObject.SetActive(false);
        }

        private void HideGhost()
        {
            if (ghostRoot == null || !ghostRoot.gameObject.activeSelf) return;
            // Another highlighted burst may still be running.
            for (var i = 0; i < bursts.Count; i++)
                if (bursts[i].ghost) return;
            ghostHideAt = Time.unscaledTime + GhostHold;
        }

        private void UpdateGhost(float now)
        {
            if (ghostRoot == null || !ghostRoot.gameObject.activeSelf) return;

            // Pop in with a slight overshoot; fade out a beat after the last coin.
            var t = Mathf.Clamp01((now - ghostShownAt) / GhostIn);
            var alpha = t;
            var scale = Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(t));
            if (ghostHideAt >= 0f && now >= ghostHideAt)
            {
                var u = (now - ghostHideAt) / GhostOut;
                if (u >= 1f)
                {
                    ghostRoot.gameObject.SetActive(false);
                    ghostHideAt = -1f;
                    return;
                }
                alpha = 1f - u;
                scale = Mathf.Lerp(1f, 0.92f, u);
            }
            ghostGroup.alpha = alpha;
            // Intro/outro scale lives on the body; punches (Juice.Punch) own the chip's scale.
            ghostBody.localScale = new Vector3(scale, scale, 1f);
            var size = ghostRect.sizeDelta;
            var wave = Mathf.Sin(now * 7f);
            ghostGlow.rectTransform.sizeDelta = new Vector2(size.x + 140f, size.y + 120f) * (1f + 0.06f * wave);
            ghostGlow.color = WithAlpha(ghostTint, 0.45f + 0.15f * wave);
            ghostBorder.rectTransform.sizeDelta = new Vector2(size.x + 10f, size.y + 10f);
            ghostPanel.rectTransform.sizeDelta = size;
        }

        private void LayerBounds(RectTransform source, out Vector2 min, out Vector2 max)
        {
            var corners = new Vector3[4];
            source.GetWorldCorners(corners);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var p = ToLayer(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }

        private void PlaceOver(RectTransform target, RectTransform source, Vector2 chipCenter)
        {
            Vector2 min, max;
            LayerBounds(source, out min, out max);
            target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = (min + max) * 0.5f - chipCenter;
            target.sizeDelta = max - min;
            target.localScale = Vector3.one;
        }

        private static void CopyImage(Image target, Image source)
        {
            if (source == null) return;
            target.sprite = source.sprite;
            target.type = source.type;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.color = source.color;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        // ------------------------------------------------------------------
        // Sparks
        // ------------------------------------------------------------------

        private void SpawnSpark(Vector2 from, Vector2 travel, float life, float size0, float size1, Color color, bool ring)
        {
            for (var i = 0; i < SparkPool; i++)
            {
                if (sparks[i].active) continue;
                ref var spark = ref sparks[i];
                spark.active = true;
                spark.start = Time.unscaledTime;
                spark.life = Mathf.Max(0.05f, life);
                spark.from = from;
                spark.travel = travel;
                spark.size0 = size0;
                spark.size1 = size1;
                spark.color = color;
                if (spark.ring != ring || spark.image.sprite == null)
                {
                    spark.ring = ring;
                    spark.image.sprite = ring ? ringSprite : glowSprite;
                }
                spark.image.color = color;
                spark.rect.anchoredPosition = from;
                spark.rect.sizeDelta = new Vector2(size0, size0);
                spark.rect.SetAsLastSibling();
                spark.image.gameObject.SetActive(true);
                return;
            }
            // Pool exhausted: the spark is decoration, skipping it is fine.
        }

        private static void UpdateSpark(ref Spark spark, float now)
        {
            var t = (now - spark.start) / spark.life;
            if (t >= 1f)
            {
                spark.active = false;
                spark.image.gameObject.SetActive(false);
                return;
            }
            var move = 1f - (1f - t) * (1f - t);
            spark.rect.anchoredPosition = spark.from + spark.travel * move;
            var size = Mathf.Lerp(spark.size0, spark.size1, spark.ring ? EaseOutCubic(t) : t);
            spark.rect.sizeDelta = new Vector2(size, size);
            var fade = spark.ring ? (1f - t) * (1f - t) : 1f - t;
            spark.image.color = WithAlpha(spark.color, spark.color.a * fade);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private Vector2 LandingPoint(Burst burst)
        {
            if (burst == null || burst.landing == null) return Vector2.zero;
            return ToLayer(burst.landing.TransformPoint(burst.landing.rect.center));
        }

        private Vector2 ToLayer(Vector3 world)
        {
            return layer.InverseTransformPoint(world);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static float EaseOutCubic(float t)
        {
            var f = 1f - t;
            return 1f - f * f * f;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
