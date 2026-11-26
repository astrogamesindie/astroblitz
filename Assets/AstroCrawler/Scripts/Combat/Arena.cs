// AstroCrawler - arena geometry.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Fixed measurements of the play space. The ship never moves (GDD 2), so
    /// every spawn, pull and push is expressed relative to the origin.
    /// </summary>
    public static class Arena
    {
        /// <summary>Where the player ship is parked, for the whole run.</summary>
        public static readonly Vector2 ShipPosition = Vector2.zero;

        /// <summary>Radius of the ring enemies spawn on, just off camera.</summary>
        public const float SpawnRadius = 12.5f;

        /// <summary>
        /// Beyond this an enemy is considered lost and re-aims at the ship. A
        /// tall portrait view spawns past 16, so it grows with the view.
        /// </summary>
        public static float LostRadius
        {
            get { return Mathf.Max(16f, ViewHalfSize.magnitude + 3f); }
        }

        /// <summary>
        /// Half the visible area in world units, kept up to date by ArenaCamera.
        /// Starts at 16:9 landscape.
        /// </summary>
        public static Vector2 ViewHalfSize { get; private set; } = new Vector2(CameraSize * 16f / 9f, CameraSize);

        /// <summary>Taller than wide: the portrait layout.</summary>
        public static bool PortraitView
        {
            get { return ViewHalfSize.y > ViewHalfSize.x; }
        }

        /// <summary>
        /// The narrowest half-width the camera may show. In landscape the
        /// half-height (CameraSize) is the short side; in portrait the width is,
        /// and it keeps the detection ring (6) and a margin around it in view,
        /// at about the scale landscape shows the arena.
        /// </summary>
        public const float MinHalfWidth = 7.5f;

        /// <summary>
        /// The lowest half-height a portrait view may have. The portrait HUD
        /// takes about a quarter of a tablet's height at the top; below 13.5
        /// the top of the detection ring would slide under it.
        /// </summary>
        public const float MinPortraitHalfHeight = 13.5f;

        /// <summary>Orthographic size that fits the arena on a screen of this aspect.</summary>
        public static float FitCameraSize(float aspect)
        {
            var fitWidth = MinHalfWidth / Mathf.Max(0.05f, aspect);
            return aspect < 1f
                ? Mathf.Max(fitWidth, MinPortraitHalfHeight)
                : Mathf.Max(CameraSize, fitWidth);
        }

        public static void SetView(Vector2 halfSize)
        {
            ViewHalfSize = halfSize;
        }

        /// <summary>How far past the edge of a portrait view an enemy appears.</summary>
        private const float SpawnMargin = 1.2f;

        /// <summary>
        /// Where the final-wave boss holds station: low on the screen, because
        /// the top is where the HUD lives and the boss used to sit behind it.
        /// </summary>
        public static readonly Vector2 BossStation = new Vector2(0f, -3.3f);

        public const float CameraSize = 7f;

        /// <summary>
        /// How far the auto-cannon and the automatic upgrades reach. Kept well
        /// inside the camera (half-height 7, minus the HUD corners), so nothing
        /// dies before the player has seen it arrive. The ship draws this as a
        /// faint ring.
        /// </summary>
        public const float DetectionRadius = 6f;

        /// <summary>
        /// The test for "inside the range": the enemy's edge crossing the ring,
        /// not its centre, so a frigate is engaged as soon as its hull touches it.
        /// </summary>
        public static bool InRange(Vector2 position, float enemyRadius)
        {
            var reach = DetectionRadius + enemyRadius;
            return (position - ShipPosition).sqrMagnitude <= reach * reach;
        }

        /// <summary>
        /// A uniformly random direction on the spawn ring. In landscape the ring
        /// is used as it always was. A portrait view is taller than the ring, so
        /// above and below the ship the point is pushed just past the edge of
        /// the screen instead of appearing in plain sight; to the sides the ring
        /// is already off screen and stays.
        /// </summary>
        public static Vector2 RandomSpawnPoint()
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (!PortraitView) return direction * SpawnRadius;

            var view = ViewHalfSize;
            var toEdgeX = Mathf.Abs(direction.x) > 0.0001f ? view.x / Mathf.Abs(direction.x) : float.MaxValue;
            var toEdgeY = Mathf.Abs(direction.y) > 0.0001f ? view.y / Mathf.Abs(direction.y) : float.MaxValue;
            var toEdge = Mathf.Min(toEdgeX, toEdgeY);
            return direction * Mathf.Max(SpawnRadius, toEdge + SpawnMargin);
        }

        /// <summary>Where the boss enters: below the screen, wherever its bottom edge is.</summary>
        public static Vector2 BossEntryPoint()
        {
            var below = PortraitView ? -(ViewHalfSize.y + SpawnMargin) : BossStation.y - 4f;
            return new Vector2(BossStation.x, Mathf.Min(BossStation.y - 4f, below));
        }

        /// <summary>
        /// The GDD authors ability radii in design pixels (150, 220, 120...).
        /// This is the single knob that maps them onto the arena; raising it
        /// shrinks every area effect at once.
        /// </summary>
        public const float PixelsPerUnit = 60f;

        public static float ToWorld(float designPixels)
        {
            return designPixels / PixelsPerUnit;
        }
    }
}
