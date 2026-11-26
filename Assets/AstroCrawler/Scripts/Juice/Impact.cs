// AstroCrawler - one-shot combat visuals: hit sparks, explosions, debris.
//
// These are pooled sprite renderers driven entirely by LeanTween rather than a
// particle system, for the same reason the rest of the game is procedural: no
// prefabs to wire up, and every timing is readable in source.
//
// Everything lives under one root that RunController clears between runs, so a
// wave never inherits the previous run's leftovers.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public static class Impact
    {
        private const int SortingOrder = 30;

        private static Transform root;
        private static readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private static readonly List<SpriteRenderer> live = new List<SpriteRenderer>();

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    root = new GameObject("ImpactFX").transform;
                }
                return root;
            }
        }

        private static SpriteRenderer Acquire(Sprite sprite, Color color, int sortingOrder)
        {
            SpriteRenderer renderer = null;
            while (renderer == null && pool.Count > 0)
            {
                renderer = pool.Pop();
            }

            if (renderer == null)
            {
                var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.impactSprite : null;
                if (prefab != null)
                {
                    renderer = Object.Instantiate(prefab, Root);
                }
                else
                {
                    var go = new GameObject("Impact");
                    go.transform.SetParent(Root, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                }
            }

            LeanTween.cancel(renderer.gameObject);
            renderer.transform.rotation = Quaternion.identity;
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.gameObject.SetActive(true);
            live.Add(renderer);
            return renderer;
        }

        private static void Release(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }
            live.Remove(renderer);
            renderer.gameObject.SetActive(false);
            pool.Push(renderer);
        }

        /// <summary>Fades a renderer out over time and returns it to the pool.</summary>
        private static void FadeAndRelease(SpriteRenderer renderer, float time)
        {
            var start = renderer.color;
            LeanTween.value(renderer.gameObject, value =>
            {
                if (renderer == null)
                {
                    return;
                }
                var color = start;
                color.a = start.a * value;
                renderer.color = color;
            }, 1f, 0f, time)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => Release(renderer));
        }

        // ------------------------------------------------------------------

        /// <summary>A short jagged arc, using the existing pooled impact prefab.</summary>
        public static void Lightning(Vector2 from, Vector2 to, Color? color = null)
        {
            var tint = color ?? new Color(1f, 0.88f, 0.3f, 1f);
            var direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;
            var side = new Vector2(-direction.y, direction.x).normalized;
            var previous = from;
            var sprite = ArtLibrary.Pixel;
            for (var i = 1; i <= 4; i++)
            {
                var next = Vector2.Lerp(from, to, i / 4f) +
                    side * (i == 4 ? 0f : i % 2 == 0 ? -0.12f : 0.12f);
                var segment = next - previous;
                var renderer = Acquire(sprite, tint, SortingOrder + 2);
                renderer.transform.position = (previous + next) * 0.5f;
                renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                renderer.transform.localScale = new Vector3(segment.magnitude / sprite.bounds.size.x,
                    0.055f / sprite.bounds.size.y, 1f);
                FadeAndRelease(renderer, 0.16f);
                previous = next;
            }
        }

        /// <summary>A small flash where a bullet connected.</summary>
        public static void Spark(Vector2 position, Color color, float size)
        {
            var renderer = Acquire(ArtLibrary.Glow, new Color(color.r, color.g, color.b, 0.9f),
                SortingOrder);
            renderer.transform.position = position;
            renderer.transform.localScale = Vector3.one * (size * 0.35f);

            LeanTween.scale(renderer.gameObject, Vector3.one * size, 0.13f)
                .setEase(LeanTweenType.easeOutQuad);
            FadeAndRelease(renderer, 0.13f);
        }

        /// <summary>A bloom plus an expanding ring - the "something died" beat.</summary>
        public static void Burst(Vector2 position, Color color, float size)
        {
            var core = Acquire(ArtLibrary.Glow, new Color(1f, 1f, 1f, 0.95f), SortingOrder + 1);
            core.transform.position = position;
            core.transform.localScale = Vector3.one * (size * 0.4f);
            LeanTween.scale(core.gameObject, Vector3.one * (size * 1.5f), 0.22f)
                .setEase(LeanTweenType.easeOutQuad);
            FadeAndRelease(core, 0.22f);

            var ring = Acquire(ArtLibrary.RingSprite, new Color(color.r, color.g, color.b, 0.8f),
                SortingOrder);
            ring.transform.position = position;
            ring.transform.localScale = Vector3.one * (size * 0.3f);
            LeanTween.scale(ring.gameObject, Vector3.one * (size * 2.4f), 0.34f)
                .setEase(LeanTweenType.easeOutCubic);
            FadeAndRelease(ring, 0.34f);
        }

        /// <summary>An expanding outline, used for shockwaves and pulses.</summary>
        public static void Ring(Vector2 position, Color color, float fromSize, float toSize, float time)
        {
            var ring = Acquire(ArtLibrary.RingSprite, new Color(color.r, color.g, color.b, 0.85f),
                SortingOrder);
            ring.transform.position = position;
            ring.transform.localScale = Vector3.one * fromSize;

            LeanTween.scale(ring.gameObject, Vector3.one * toSize, time)
                .setEase(LeanTweenType.easeOutCubic);
            FadeAndRelease(ring, time);
        }

        /// <summary>Shards thrown outward from a kill.</summary>
        public static void Debris(Vector2 position, Color color, float size, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var shard = Acquire(ArtLibrary.Asteroid(i + Random.Range(0, 5)),
                    new Color(color.r, color.g, color.b, 0.95f), SortingOrder - 1);

                shard.transform.position = position;
                var shardSize = size * Random.Range(0.16f, 0.3f);
                shard.transform.localScale = Vector3.one * shardSize;
                shard.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                var direction = Random.insideUnitCircle.normalized;
                var distance = size * Random.Range(0.7f, 1.6f);
                var time = Random.Range(0.3f, 0.5f);

                LeanTween.move(shard.gameObject,
                    (Vector3)(position + direction * distance), time)
                    .setEase(LeanTweenType.easeOutCubic);

                LeanTween.rotateZ(shard.gameObject,
                    shard.transform.eulerAngles.z + Random.Range(-220f, 220f), time)
                    .setEase(LeanTweenType.easeOutCubic);

                FadeAndRelease(shard, time);
            }
        }

        /// <summary>Hides every live effect. Called when a run tears down.</summary>
        public static void ClearAll()
        {
            for (var i = live.Count - 1; i >= 0; i--)
            {
                var renderer = live[i];
                if (renderer == null)
                {
                    continue;
                }
                LeanTween.cancel(renderer.gameObject);
                renderer.gameObject.SetActive(false);
                pool.Push(renderer);
            }
            live.Clear();
        }

        /// <summary>Destroys the pool entirely, so a scene reload starts clean.</summary>
        public static void Dispose()
        {
            live.Clear();
            pool.Clear();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                root = null;
            }
        }
    }
}
