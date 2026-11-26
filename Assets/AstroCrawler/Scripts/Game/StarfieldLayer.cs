// AstroCrawler - subtle background motion and independent star shimmer.

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// Gives each star plane its own drift speed and shimmer. Different layer
    /// speeds create depth even though the combat camera itself remains fixed.
    /// </summary>
    public class StarfieldLayer : MonoBehaviour
    {
        // Serialized because the starfield is baked into the scene: Configure
        // runs once at author time, and without these the arrays would come back
        // null on load and Update would throw on every frame.
        [SerializeField] private SpriteRenderer[] stars;
        [SerializeField] private float[] baseAlpha;
        [SerializeField] private Vector2 drift;
        [SerializeField] private float twinkle;
        [SerializeField] private float speed;
        [SerializeField] private Vector3 origin;

        public void Configure(SpriteRenderer[] renderers, Vector2 driftAmount,
            float twinkleAmount, float movementSpeed)
        {
            stars = renderers;
            drift = driftAmount;
            twinkle = twinkleAmount;
            speed = movementSpeed;
            origin = transform.localPosition;

            baseAlpha = new float[stars.Length];
            for (var i = 0; i < stars.Length; i++)
            {
                baseAlpha[i] = stars[i].color.a;
            }
        }

        private void Update()
        {
            if (stars == null || baseAlpha == null)
            {
                return;
            }

            var time = Time.unscaledTime;
            transform.localPosition = origin + new Vector3(
                Mathf.Sin(time * speed) * drift.x,
                Mathf.Cos(time * speed * 0.73f) * drift.y,
                0f);

            for (var i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                var color = star.color;
                var shimmer = Mathf.Sin(time * (0.65f + i % 7 * 0.11f) + i * 1.71f);
                color.a = baseAlpha[i] * (1f - twinkle * 0.5f + shimmer * twinkle * 0.5f);
                star.color = color;
            }
        }
    }
}
