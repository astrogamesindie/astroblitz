// AstroCrawler - the prefabs the game instantiates at runtime.
//
// Why this exists: building an enemy used to be `new GameObject()` plus an
// AddComponent per part, every single spawn. AddComponent is the slow half of
// that, and a dense wave does it dozens of times a second while the auto-cannon
// adds up to twenty projectiles a second on top. Instantiate copies a prepared
// object graph instead, which is the single biggest win available here.
//
// Every field is optional. A null prefab makes the corresponding system fall
// back to the procedural construction it used before, so a half-filled library
// degrades performance rather than breaking the game.

using UnityEngine;

namespace AstroCrawler
{
    [CreateAssetMenu(menuName = "AstroCrawler/Biblioteca de Prefabs", fileName = "PrefabLibrary")]
    public class PrefabLibrary : ScriptableObject
    {
        [Header("Combate")]
        public Enemy enemy;
        public PlayerShip playerShip;
        public Projectile projectile;
        public Missile missile;
        public XpOrb xpOrb;

        [Header("Habilidades")]
        public BlackHole blackHole;
        public Shockwave shockwave;
        public DebrisWall debrisWall;
        public Pulse pulse;

        [Header("Efeitos")]
        public SpriteRenderer impactSprite;
        public DamageNumber damageNumber;
        public SpriteRenderer star;

        [Header("Interface")]
        [Tooltip("Canvas completo do jogo. Nulo faz a GameUI construir a hierarquia em codigo.")]
        public GameUI gameUI;

        /// <summary>
        /// The library the running game is using, set by GameManager at boot so
        /// static spawners (Projectile, Impact) can reach it without a lookup.
        /// </summary>
        public static PrefabLibrary Active { get; private set; }

        public static void SetActive(PrefabLibrary library)
        {
            Active = library;
        }

        /// <summary>Convenience for spawners that only need one field.</summary>
        public static T Get<T>(System.Func<PrefabLibrary, T> pick) where T : Object
        {
            return Active != null ? pick(Active) : null;
        }
    }
}
