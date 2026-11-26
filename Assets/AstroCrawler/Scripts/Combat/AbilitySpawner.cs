// AstroCrawler - shared spawn path for the objects card effects drop into the
// arena.
//
// Each of these used to be "new GameObject, AddComponent, AddComponent again for
// the renderer" per cast. Instantiating a prepared prefab replaces all of that
// with one copy, and the null-prefab branch keeps the game running on an install
// whose prefab library has not been generated yet.

using UnityEngine;

namespace AstroCrawler
{
    internal static class AbilitySpawner
    {
        public static T Create<T>(T prefab, string name, Vector2 position) where T : MonoBehaviour
        {
            var parent = CombatWorld.Instance != null ? CombatWorld.Instance.effectRoot : null;

            T instance;
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, parent);
            }
            else
            {
                var go = new GameObject(name);
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }
                instance = go.AddComponent<T>();
            }

            instance.transform.position = position;

            // Every area ability (black hole, shockwave, pulse, debris wall)
            // passes through here, so this is the one place to voice them.
            Sfx.Play(Sound.AreaAbility);
            return instance;
        }

        /// <summary>
        /// Returns the renderer the prefab carries, creating one only when the
        /// object was built procedurally.
        /// </summary>
        public static SpriteRenderer EnsureRenderer(SpriteRenderer existing, Transform parent,
            string name, Sprite sprite, Color color, int sortingOrder)
        {
            var renderer = existing;
            if (renderer == null)
            {
                renderer = SpriteFactory.NewRenderer(name, sprite, color, sortingOrder, parent);
            }
            else
            {
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = sortingOrder;
            }
            return renderer;
        }
    }
}
