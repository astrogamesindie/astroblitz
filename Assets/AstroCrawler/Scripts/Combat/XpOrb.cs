// AstroCrawler - the XP an enemy leaves behind.
//
// The ship never moves, so an orb collects itself: it scatters from the kill
// for a beat (long enough to read as a drop) and then homes on the hull,
// accelerating so nothing is left crawling across the arena. XP only counts
// when the orb arrives, which is what makes a level-up land a moment after the
// kill that paid for it instead of on the same frame.
//
// Pooled: a fragmenting wave drops dozens of these in a few seconds.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public class XpOrb : MonoBehaviour
    {
        private static readonly Color Tint = new Color(0.36f, 0.82f, 1f, 0.95f);

        private static readonly Stack<XpOrb> pool = new Stack<XpOrb>();
        private static readonly List<XpOrb> live = new List<XpOrb>();
        private static Transform root;

        [Tooltip("Renderer do orbe. Se vazio, e criado em runtime.")]
        [SerializeField] private SpriteRenderer body;

        private ProgressionDefinition tuning;
        private Action<int> onCollected;
        private Vector2 velocity;
        private float age;
        private int value;
        private bool flying;

        public static int ActiveCount
        {
            get { return live.Count; }
        }

        /// <summary>
        /// Drops <paramref name="value"/> XP at a point. Past the orb budget the
        /// XP is credited directly: the limit is cosmetic and must never cost
        /// the player progress.
        /// </summary>
        public static void Spawn(Vector2 position, int value, ProgressionDefinition tuning,
            Action<int> onCollected)
        {
            if (value <= 0 || tuning == null || onCollected == null)
            {
                return;
            }
            if (live.Count >= Mathf.Max(1, tuning.maxActiveOrbs))
            {
                onCollected(value);
                return;
            }

            var orb = Acquire();
            orb.tuning = tuning;
            orb.onCollected = onCollected;
            orb.value = value;
            orb.age = 0f;
            orb.flying = true;

            var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            orb.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                UnityEngine.Random.Range(0.6f, 1f) * tuning.orbScatterSpeed;

            var sprite = ArtLibrary.SoftCircle;
            if (orb.body == null)
            {
                orb.body = SpriteFactory.NewRenderer("Body", sprite, Tint, 13, orb.transform);
            }
            orb.body.sprite = sprite;
            orb.body.color = Tint;
            orb.body.transform.localScale = Vector3.one *
                ArtLibrary.ScaleToDiameter(sprite, 0.2f + 0.03f * Mathf.Min(value, 8));

            orb.transform.position = position;
            live.Add(orb);
        }

        /// <summary>Splits one reward into several orbs, so a big drop reads as big.</summary>
        public static void Burst(Vector2 position, int value, int orbs, ProgressionDefinition tuning,
            Action<int> onCollected)
        {
            orbs = Mathf.Clamp(orbs, 1, Mathf.Max(1, value));
            var share = value / orbs;
            var remainder = value - share * orbs;
            for (var i = 0; i < orbs; i++)
            {
                Spawn(position, share + (i < remainder ? 1 : 0), tuning, onCollected);
            }
        }

        private static XpOrb Acquire()
        {
            XpOrb orb = null;
            while (orb == null && pool.Count > 0)
            {
                // Entries go stale if a scene teardown destroyed them.
                orb = pool.Pop();
            }
            if (orb != null)
            {
                orb.gameObject.SetActive(true);
                return orb;
            }

            if (root == null)
            {
                root = new GameObject("XpOrbs").transform;
                if (CombatWorld.Instance != null)
                {
                    root.SetParent(CombatWorld.Instance.transform, false);
                }
            }

            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.xpOrb : null;
            if (prefab != null)
            {
                return Instantiate(prefab, root);
            }

            // No prefab library: keep working the way the other spawners do.
            var go = new GameObject("XpOrb");
            go.transform.SetParent(root, false);
            return go.AddComponent<XpOrb>();
        }

        private void Update()
        {
            if (!flying)
            {
                return;
            }

            var dt = Time.deltaTime;
            age += dt;
            var position = (Vector2)transform.position;

            if (age < tuning.orbMagnetDelay)
            {
                transform.position = position + velocity * dt;
                velocity *= Mathf.Exp(-5f * dt);
                return;
            }

            var toShip = Arena.ShipPosition - position;
            var distance = toShip.magnitude;
            var step = (tuning.orbSpeed + tuning.orbAcceleration * (age - tuning.orbMagnetDelay)) * dt;
            if (distance <= tuning.orbCollectRadius + step)
            {
                Collect();
                return;
            }
            transform.position = position + toShip / distance * step;
        }

        private void Collect()
        {
            var callback = onCollected;
            var amount = value;
            live.Remove(this);
            Recycle();
            if (callback != null)
            {
                callback(amount);
            }
        }

        private void Recycle()
        {
            flying = false;
            onCollected = null;
            value = 0;
            gameObject.SetActive(false);
            pool.Push(this);
        }

        /// <summary>Removes every orb in flight without paying it out. Run end and teardown.</summary>
        public static void ClearAll()
        {
            for (var i = live.Count - 1; i >= 0; i--)
            {
                if (live[i] != null)
                {
                    live[i].Recycle();
                }
            }
            live.Clear();
        }

        /// <summary>Forgets the pool. Called at the start of every play session.</summary>
        public static void ResetPool()
        {
            pool.Clear();
            live.Clear();
            root = null;
        }
    }
}
