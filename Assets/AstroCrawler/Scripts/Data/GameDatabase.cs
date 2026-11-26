// AstroCrawler - the designer database, as ScriptableObjects.
//
// Each card, ship, enemy and wave is its own asset (CardAsset, ShipAsset,
// EnemyAsset, WaveAsset), so it can be selected, inspected, diffed and
// referenced like any other Unity object. The payload is the same [Serializable]
// definition class the rest of the game already reads, which is why this swap
// cost no changes in EffectRunner, CardSystem or the UI.
//
// Why this replaced the JSON tables: parsing four files through FullSerializer's
// reflection at boot was the most expensive thing GameManager did.
// ScriptableObjects are deserialized by the engine, so the data is already in
// memory by the time Awake runs.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// The one asset the game loads at boot. Everything else hangs off it, so
    /// there is exactly one Resources lookup for the whole database.
    /// </summary>
    [CreateAssetMenu(menuName = "AstroCrawler/Banco de Dados", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        /// <summary>Path under a Resources folder, without the extension.</summary>
        public const string ResourcePath = "AstroCrawler/GameDatabase";

        public List<CardAsset> cards = new List<CardAsset>();
        public List<ShipAsset> ships = new List<ShipAsset>();
        public List<EnemyAsset> enemies = new List<EnemyAsset>();
        public List<WaveAsset> waves = new List<WaveAsset>();

        [Tooltip("Curva de XP, drops do chefe e comportamento dos orbes. Sem isto valem os padroes do codigo.")]
        public ProgressionAsset progression;

        [Tooltip("Prefabs instanciados em runtime. Sem isto o jogo cai para a construcao procedural.")]
        public PrefabLibrary prefabs;

        [Tooltip("Sons de interface e combate e a musica do menu. Sem isto o jogo fica mudo.")]
        public SoundLibrary sounds;

        public static GameDatabase Load()
        {
            return Resources.Load<GameDatabase>(ResourcePath);
        }

        /// <summary>
        /// Flattens the assets into the lookup object the systems already use.
        ///
        /// The definitions are handed over by reference rather than copied: no
        /// system mutates a definition (card evolution is tracked separately in
        /// CardSystem), so sharing avoids allocating the whole database twice.
        /// </summary>
        public GameConfig BuildConfig()
        {
            var config = new GameConfig();

            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null) { config.cards.Add(cards[i].definition); }
            }
            for (var i = 0; i < ships.Count; i++)
            {
                if (ships[i] != null) { config.ships.Add(ships[i].definition); }
            }
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null) { config.enemies.Add(enemies[i].definition); }
            }
            for (var i = 0; i < waves.Count; i++)
            {
                if (waves[i] != null) { config.waves.Add(waves[i].definition); }
            }

            if (progression != null) { config.progression = progression.definition; }

            config.waves.Sort((a, b) => a.index.CompareTo(b.index));
            return config;
        }
    }
}
