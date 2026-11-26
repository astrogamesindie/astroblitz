// AstroCrawler - procedural sprite generation.
//
// The game ships no bitmap art for the arena: hulls, meteors and projectiles are
// generated as textures at boot. That keeps the scene free of prefab/GUID
// references (so it always opens clean) and makes silhouettes a data decision -
// an enemy's shape and colour live in enemies.json next to its stats.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Builds and caches the handful of sprites the arena needs.
    /// </summary>
    public static class SpriteFactory
    {
        private const int TextureSize = 128;
        private const float PixelsPerUnit = 128f;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite ChristmasHull()
        {
            if (cache.TryGetValue("christmas_hull", out var cached)) return cached;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.name = "Noel_CandyCaneHull";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var nx = (x + 0.5f) / TextureSize * 2f - 1f;
                var ny = (y + 0.5f) / TextureSize * 2f - 1f;
                var width = (0.9f - ny) * 0.48f;
                var inside = ny > -0.73f && ny < 0.9f && Mathf.Abs(nx) < width &&
                    (ny > -0.38f || Mathf.Abs(nx) > (-ny - 0.38f) * 0.62f);
                if (!inside) continue;
                var red = Mathf.FloorToInt((ny + nx * 0.7f + 3f) * 7f) % 2 == 0;
                var color = red ? new Color(0.94f, 0.12f, 0.2f) : new Color(1f, 0.98f, 0.94f);
                if (Mathf.Abs(nx) < 0.115f && ny > -0.08f && ny < 0.42f)
                    color = Mathf.Abs(nx) < 0.075f ? new Color(0.35f, 0.9f, 1f) : new Color(1f, 0.8f, 0.25f);
                pixels[y * TextureSize + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache["christmas_hull"] = sprite;
            return sprite;
        }

        public static Sprite ChristmasStar()
        {
            if (cache.TryGetValue("christmas_star", out var cached)) return cached;
            return Build("christmas_star", (x, y) =>
            {
                var angle = Mathf.Atan2(y, x) - Mathf.PI * 0.5f;
                var ray = 0.44f + 0.22f * Mathf.Cos(angle * 5f);
                return IconStrokeAlpha(Mathf.Abs(new Vector2(x, y).magnitude - ray));
            });
        }

        /// <summary>
        /// A soft-edged disc. Used for meteors, projectiles, black holes and glow.
        /// </summary>
        public static Sprite Disc(float softness = 0.12f)
        {
            var key = "disc_" + softness.ToString("F2");
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var sprite = Build(key, (nx, ny) =>
            {
                var d = Mathf.Sqrt(nx * nx + ny * ny);
                return Mathf.InverseLerp(1f, 1f - softness, d);
            });
            return sprite;
        }

        /// <summary>
        /// A convex regular polygon with <paramref name="sides"/> corners, rotated
        /// so a point faces up. Three sides gives the ship and the alien hulls;
        /// six or seven gives chunky asteroid silhouettes.
        /// </summary>
        public static Sprite Polygon(int sides, float rotationDegrees = 0f)
        {
            sides = Mathf.Max(3, sides);
            var key = "poly_" + sides + "_" + rotationDegrees.ToString("F0");
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var rotation = rotationDegrees * Mathf.Deg2Rad;
            var half = Mathf.PI / sides;
            var cosHalf = Mathf.Cos(half);

            return Build(key, (nx, ny) =>
            {
                var d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d < 0.0001f)
                {
                    return 1f;
                }
                // Distance to the polygon edge along this direction, for a
                // polygon whose circumradius is 1.
                var angle = Mathf.Atan2(ny, nx) - rotation;
                var sector = Mathf.Repeat(angle + half, 2f * half) - half;
                var edge = cosHalf / Mathf.Cos(sector);
                return Mathf.InverseLerp(edge, edge * 0.88f, d);
            });
        }

        /// <summary>
        /// A hollow ring, used for shields, repulsion pulses and shockwaves.
        /// </summary>
        public static Sprite Ring(float thickness = 0.14f)
        {
            var key = "ring_" + thickness.ToString("F2");
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            return Build(key, (nx, ny) =>
            {
                var d = Mathf.Sqrt(nx * nx + ny * ny);
                var inner = 1f - thickness;
                if (d > 1f || d < inner)
                {
                    return 0f;
                }
                var t = Mathf.InverseLerp(inner, 1f, d);
                return Mathf.Sin(t * Mathf.PI);
            });
        }

        /// <summary>A cloud outline and upload arrow, shared by cloud-save UI.</summary>
        public static Sprite CloudSave()
        {
            if (cache.TryGetValue("cloud_save", out var cached)) return cached;
            return Build("cloud_save", (x, y) =>
            {
                // Union of three lobes and a flat base forms one continuous contour.
                var left = new Vector2(x + 0.38f, y - 0.08f).magnitude - 0.26f;
                var top = new Vector2(x, y - 0.25f).magnitude - 0.39f;
                var right = new Vector2(x - 0.39f, y - 0.02f).magnitude - 0.28f;
                var box = Mathf.Max(Mathf.Abs(x) - 0.43f, Mathf.Abs(y + 0.04f) - 0.18f);
                var contour = Mathf.Abs(Mathf.Min(Mathf.Min(left, top), Mathf.Min(right, box)));
                // Open the base around the arrow so the two symbols stay legible.
                var cloud = Mathf.Abs(x) < 0.2f && y < -0.1f ? 0f
                    : IconStrokeAlpha(contour);
                var shaft = IconSegmentDistance(x, y, 0f, -0.65f, 0f, 0.06f);
                var arrowLeft = IconSegmentDistance(x, y, -0.18f, -0.14f, 0f, 0.06f);
                var arrowRight = IconSegmentDistance(x, y, 0.18f, -0.14f, 0f, 0.06f);
                var arrow = IconStrokeAlpha(Mathf.Min(shaft, Mathf.Min(arrowLeft, arrowRight)));
                return Mathf.Max(cloud, arrow);
            });
        }

        /// <summary>Crossed-out megaphone, without lettering or a background plate.</summary>
        public static Sprite NoAds()
        {
            if (cache.TryGetValue("no_ads", out var cached)) return cached;
            return Build("no_ads", (x, y) =>
            {
                var ring = IconStrokeAlpha(Mathf.Abs(new Vector2(x, y).magnitude - 0.84f));
                var slashDistance = IconSegmentDistance(x, y, -0.59f, 0.59f, 0.59f, -0.59f);
                var body = Mathf.Min(
                    IconSegmentDistance(x, y, -0.43f, 0.18f, 0.28f, 0.43f),
                    IconSegmentDistance(x, y, -0.43f, -0.14f, 0.28f, -0.34f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, -0.43f, -0.14f, -0.43f, 0.18f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, 0.28f, -0.34f, 0.28f, 0.43f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, -0.27f, -0.2f, -0.17f, -0.46f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, -0.17f, -0.46f, -0.02f, -0.46f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, -0.02f, -0.46f, -0.09f, -0.24f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, 0.43f, 0.09f, 0.59f, 0.09f));
                body = Mathf.Min(body, IconSegmentDistance(x, y, 0.42f, 0.3f, 0.53f, 0.39f));
                var megaphone = slashDistance < 0.09f ? 0f : IconStrokeAlpha(body);
                return Mathf.Max(ring, Mathf.Max(megaphone, IconStrokeAlpha(slashDistance)));
            });
        }

        public static Sprite Premium()
        {
            if (cache.TryGetValue("premium_crown", out var cached)) return cached;
            return Build("premium_crown", (x, y) =>
            {
                var edge = IconSegmentDistance(x, y, -0.64f, 0.4f, -0.46f, -0.3f);
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, -0.46f, -0.3f, 0.46f, -0.3f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, 0.46f, -0.3f, 0.64f, 0.4f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, 0.64f, 0.4f, 0.27f, 0.13f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, 0.27f, 0.13f, 0f, 0.62f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, 0f, 0.62f, -0.27f, 0.13f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, -0.27f, 0.13f, -0.64f, 0.4f));
                edge = Mathf.Min(edge, IconSegmentDistance(x, y, -0.43f, -0.49f, 0.43f, -0.49f));
                var jewel = Mathf.Abs(Mathf.Abs(x) + Mathf.Abs(y + 0.06f) - 0.12f) * 0.707f;
                if (Mathf.Abs(x) < 0.18f && Mathf.Abs(y + 0.06f) < 0.18f) edge = Mathf.Min(edge, jewel);
                return IconStrokeAlpha(edge);
            });
        }

        private static float IconStrokeAlpha(float distance)
        {
            // Unity SmoothStep interpolates between values; its third argument
            // must be normalized, unlike the GLSL smoothstep(edge0, edge1, x).
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.05f, distance));
        }

        private static float IconSegmentDistance(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            return new Vector2(x - ax - t * dx, y - ay - t * dy).magnitude;
        }

        /// <summary>A single white pixel, for UI fills and bars.</summary>
        public static Sprite White()
        {
            Sprite cached;
            if (cache.TryGetValue("white", out cached))
            {
                return cached;
            }
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache["white"] = sprite;
            return sprite;
        }

        private static Sprite Build(string key, System.Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var pixels = new Color32[TextureSize * TextureSize];
            var half = TextureSize * 0.5f;

            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var nx = (x + 0.5f - half) / half;
                    var ny = (y + 0.5f - half) / half;
                    var a = Mathf.Clamp01(alphaAt(nx, ny));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Drops every generated sprite. Required because this cache is static:
        /// with Domain Reload disabled it would otherwise survive into the next
        /// play session holding textures Unity has already destroyed.
        /// </summary>
        public static void Clear()
        {
            cache.Clear();
        }

        /// <summary>
        /// Parses an "RRGGBB" or "#RRGGBB" string, falling back to white.
        /// </summary>
        public static Color ParseColor(string hex, float alpha = 1f)
        {
            Color color;
            if (!string.IsNullOrEmpty(hex))
            {
                var text = hex.StartsWith("#") ? hex : "#" + hex;
                if (ColorUtility.TryParseHtmlString(text, out color))
                {
                    color.a = alpha;
                    return color;
                }
            }
            return new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// Creates a GameObject carrying a SpriteRenderer, ready to be positioned.
        /// </summary>
        public static SpriteRenderer NewRenderer(string name, Sprite sprite, Color color, int sortingOrder,
            Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
