// AstroCrawler - the jump into a stage.

using System;
using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    /// <summary>
    /// Plays between the button that enters a stage and the stage itself: the
    /// screen's UI fades, Hovl Studio's "Star trip" fills the screen (tinted
    /// with the stage's colour, its streaks thickened), the ship that will fly
    /// the stage sits in the middle with its engine burning, the stars rush
    /// past as the camera pushes in, the stage's name comes up, the ship lunges
    /// forward and a flash hands over to the stage. Everything runs on unscaled
    /// time - the stage begins paused on its first card pick - and touches are
    /// blocked until the hand-over. Without the generated prefab only the ship,
    /// the title and the flash play.
    /// </summary>
    public sealed class WarpTransition : MonoBehaviour
    {
        private const float TravelTime = 1.5f;
        private const float ScreenFadeTime = 0.25f;
        /// <summary>Width of the Star trip field at scale 1, measured in the editor.</summary>
        private const float FieldDiameter = 20f;
        /// <summary>Hull length on screen, in world units (the arena ship is about 1.3).</summary>
        private const float ShipSize = 2.2f;

        private static WarpTransition instance;

        private GameObject warp;
        private ParticleSystem[] systems;
        private ParticleSystem.MinMaxGradient[] authoredColors;
        private float[] authoredRates;

        private Transform shipRoot;
        private SpriteRenderer shipHull;
        private SpriteRenderer shipGlow;
        private ParticleSystem shipFlame;

        private Canvas overlay;
        private CanvasScaler overlayScaler;
        private CanvasGroup overlayGroup;
        private TMP_Text caption;
        private TMP_Text title;
        private bool running;

        public static bool Running { get { return instance != null && instance.running; } }

        /// <summary>
        /// Starts the jump; <paramref name="arrive"/> (which begins the stage)
        /// runs at the flash. False when a jump is already under way.
        /// </summary>
        public static bool Play(Color accent, Color shipTint, string heading, string name, CanvasGroup screen,
            Action arrive, string shipId = null)
        {
            if (arrive == null || Running) return false;
            if (instance == null)
            {
                var go = new GameObject("WarpTransition");
                if (JuiceDirector.Instance != null) go.transform.SetParent(JuiceDirector.Instance.transform, false);
                instance = go.AddComponent<WarpTransition>();
            }
            instance.StartCoroutine(instance.Travel(accent, shipTint, heading, name, screen, arrive, shipId));
            return true;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private IEnumerator Travel(Color accent, Color shipTint, string heading, string name, CanvasGroup screen,
            Action arrive, string shipId)
        {
            running = true;
            BuildOverlay();
            overlayScaler.referenceResolution = new Vector2(UIFactory.ReferenceWidth, UIFactory.ReferenceHeight);
            overlay.gameObject.SetActive(true);
            overlayGroup.alpha = 0f;
            caption.text = heading;
            caption.color = Color.Lerp(accent, Color.white, 0.35f);
            title.text = name;

            if (screen != null)
            {
                screen.blocksRaycasts = false;
                LeanTween.cancel(screen.gameObject);
                LeanTween.alphaCanvas(screen, 0f, ScreenFadeTime).setIgnoreTimeScale(true);
            }

            var view = Camera.main;
            var arena = view != null ? view.GetComponent<ArenaCamera>() : null;
            var stars = arena != null ? arena.Starfield : null;
            var starScale = stars != null ? stars.localScale : Vector3.one;
            var viewSize = view != null ? view.orthographicSize : 0f;
            var center = (Vector3)Arena.ShipPosition;

            var scale = 1f;
            if (BuildWarp())
            {
                var half = view != null
                    ? new Vector2(view.orthographicSize * view.aspect, view.orthographicSize)
                    : Arena.ViewHalfSize;
                // The star field should reach past the screen's long side.
                scale = Mathf.Max(half.x, half.y) * 2.2f / FieldDiameter;
                warp.transform.position = center;
                warp.transform.localScale = Vector3.one * scale;
                warp.transform.rotation = Quaternion.identity;
                Tint(accent);
                warp.SetActive(true);
                for (var i = 0; i < systems.Length; i++)
                {
                    var main = systems[i].main;
                    main.simulationSpeed = 1f;
                    systems[i].Clear(false);
                    systems[i].Play(false);
                }
            }

            BuildShip();
            // The transition is reused: refresh the selected hull on every jump.
            shipHull.sprite = ArtLibrary.ShipFor(shipId);
            shipHull.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(shipHull.sprite, ShipSize);
            shipRoot.position = center;
            shipRoot.localScale = Vector3.one;
            shipRoot.rotation = Quaternion.identity;
            shipHull.color = ArtLibrary.ShipTint(shipId, shipTint);
            shipGlow.color = new Color(shipTint.r, shipTint.g, shipTint.b, 0.3f);
            shipRoot.gameObject.SetActive(true);
            Juice.PopIn(shipRoot, 1f, 0.35f, 0f, true);
            shipFlame.Clear(false);
            shipFlame.Play(false);
            Sfx.Play(Sound.AreaAbility);

            var time = 0f;
            var flameTimer = 0f;
            while (time < TravelTime)
            {
                var step = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                time += step;
                var progress = Mathf.Clamp01(time / TravelTime);
                var push = progress * progress;

                if (warp != null && warp.activeSelf)
                {
                    for (var i = 0; i < systems.Length; i++)
                    {
                        var main = systems[i].main;
                        main.simulationSpeed = 1f + 3f * push;
                    }
                    // Flying in: the field swells and turns a little.
                    warp.transform.localScale = Vector3.one * scale * (1f + 0.35f * push);
                    warp.transform.Rotate(0f, 0f, (8f + 50f * push) * step, Space.Self);
                }

                // The ship holds the centre, shaking harder as the jump builds,
                // then lunges forward in the last quarter.
                var lunge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, progress));
                var jitter = (0.02f + 0.06f * push) * (1f - lunge);
                shipRoot.position = center + new Vector3(UnityEngine.Random.Range(-jitter, jitter),
                    Mathf.Sin(time * 9f) * 0.05f + UnityEngine.Random.Range(-jitter, jitter) + lunge * 1.4f, 0f);
                shipRoot.localScale = Vector3.one * (1f + 0.25f * lunge);
                flameTimer -= step;
                if (flameTimer <= 0f)
                {
                    flameTimer = 0.02f;
                    EmitFlame(shipTint, 0.6f + 1.4f * push);
                }

                if (stars != null) stars.localScale = starScale * (1f + 1.5f * push);
                if (view != null) view.orthographicSize = viewSize * (1f - 0.12f * push);
                overlayGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.4f, progress));
                Juice.Shake(0.03f + 0.12f * push, 0.08f);
                yield return null;
            }

            Juice.ScreenFlash(Color.Lerp(accent, Color.white, 0.55f), 0.6f);
            Juice.Shake(0.25f, 0.3f);
            if (stars != null) stars.localScale = starScale;
            if (view != null) view.orthographicSize = viewSize;
            shipRoot.gameObject.SetActive(false);
            shipFlame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (warp != null)
            {
                // The rest of the field blows away over the new stage.
                for (var i = 0; i < systems.Length; i++)
                {
                    var main = systems[i].main;
                    main.simulationSpeed = 3f;
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            overlayGroup.alpha = 0f;
            overlay.gameObject.SetActive(false);

            try
            {
                arrive();
            }
            finally
            {
                if (screen != null)
                {
                    LeanTween.cancel(screen.gameObject);
                    screen.alpha = 1f;
                    screen.blocksRaycasts = true;
                }
                running = false;
            }

            var settle = 0f;
            while (settle < 0.9f && !running)
            {
                settle += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!running && warp != null)
            {
                for (var i = 0; i < systems.Length; i++) systems[i].Clear(false);
                warp.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Construction (first jump only)
        // ------------------------------------------------------------------

        private bool BuildWarp()
        {
            if (warp != null) return true;
            var prefab = HovlVfx.Warp;
            if (prefab == null) return false;
            warp = Instantiate(prefab);
            warp.name = "Warp";
            warp.SetActive(false);
            systems = warp.GetComponentsInChildren<ParticleSystem>(true);
            authoredColors = new ParticleSystem.MinMaxGradient[systems.Length];
            authoredRates = new float[systems.Length];
            for (var i = 0; i < systems.Length; i++)
            {
                authoredColors[i] = systems[i].main.startColor;
                var emission = systems[i].emission;
                authoredRates[i] = emission.rateOverTimeMultiplier;
                // Star trip is a calm background at its authored rates; the
                // jump wants it dense: many more streaks, more stars.
                var boost = systems[i].name == "Trails" ? 6f : systems[i].name == "Stars" ? 1.6f : 1f;
                emission.rateOverTimeMultiplier = authoredRates[i] * boost;
            }
            return true;
        }

        /// <summary>The ship in the middle of the jump: hull, a glow under it and a burning engine.</summary>
        private void BuildShip()
        {
            if (shipRoot != null) return;
            shipRoot = new GameObject("WarpShip").transform;
            shipRoot.SetParent(transform, false);

            // A glow that fades out on every side (soft_circle has a hard rim).
            var glowSprite = ArtLibrary.CircleGlow;
            shipGlow = SpriteFactory.NewRenderer("Glow", glowSprite, Color.white, 355, shipRoot);
            shipGlow.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(glowSprite, ShipSize * 3f);

            var hullSprite = ArtLibrary.Ship;
            shipHull = SpriteFactory.NewRenderer("Hull", hullSprite, Color.white, 360, shipRoot);
            shipHull.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(hullSprite, ShipSize);

            shipFlame = HovlVfx.Emitter(shipRoot, "Engine", "Glow2", 96, 358, VfxSize.Shrink, VfxFade.Out,
                ParticleSystemRenderMode.Stretch);
            var main = shipFlame.main;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            shipRoot.gameObject.SetActive(false);
        }

        /// <summary>Exhaust streaks out of the back of the hull, longer as the jump builds.</summary>
        private void EmitFlame(Color tint, float power)
        {
            var exhaust = (Vector2)shipRoot.position + Vector2.down * ShipSize * 0.42f * shipRoot.localScale.y;
            var hot = Color.Lerp(tint, Color.white, 0.55f);
            for (var i = 0; i < 2; i++)
            {
                var spread = UnityEngine.Random.Range(-0.18f, 0.18f) * ShipSize;
                HovlVfx.Emit(shipFlame, exhaust + new Vector2(spread, 0f),
                    new Vector2(spread * 0.6f, -UnityEngine.Random.Range(6f, 10f) * power),
                    Color.Lerp(hot, tint, UnityEngine.Random.value), UnityEngine.Random.Range(0.12f, 0.22f),
                    UnityEngine.Random.Range(0.12f, 0.2f));
            }
        }

        /// <summary>Pulls the effect's own colours towards the stage's accent.</summary>
        private void Tint(Color accent)
        {
            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                var authored = authoredColors[i];
                switch (authored.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        main.startColor = TintColor(authored.color, accent);
                        break;
                    case ParticleSystemGradientMode.TwoColors:
                        main.startColor = new ParticleSystem.MinMaxGradient(TintColor(authored.colorMin, accent),
                            TintColor(authored.colorMax, accent));
                        break;
                    default:
                        main.startColor = authored;
                        break;
                }
            }
        }

        private static Color TintColor(Color authored, Color accent)
        {
            var tinted = Color.Lerp(authored, accent * Mathf.Max(authored.maxColorComponent, 0.6f), 0.55f);
            tinted.a = authored.a;
            return tinted;
        }

        private void BuildOverlay()
        {
            if (overlay != null) return;
            overlay = UIFactory.CreateCanvas("WarpOverlay", transform, 900);
            overlayScaler = overlay.GetComponent<CanvasScaler>();
            overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();

            // Blocks every touch until the stage begins.
            var blocker = UIFactory.Rect(overlay.transform, "Blocker", Color.clear);
            UIFactory.Stretch(blocker.rectTransform);
            blocker.raycastTarget = true;

            // Above the ship (which holds the middle of the screen).
            caption = UIFactory.Label(overlay.transform, "Caption", "", 34, UIFactory.Accent,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(330f, 430f)),
                new Vector2(1000f, 50f));
            caption.characterSpacing = 14f;
            UIFactory.AddUnderlay(caption);

            title = UIFactory.Label(overlay.transform, "Title", "", 104, UIFactory.Ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(240f, 340f)),
                new Vector2(1000f, 130f));
            title.characterSpacing = 10f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 60f;
            title.fontSizeMax = 104f;
            UIFactory.AddUnderlay(title);
            UIFactory.GlowShadow(title, new Color(1f, 1f, 1f, 0.35f), 30f, 0.05f);

            overlay.gameObject.SetActive(false);
        }
    }
}
