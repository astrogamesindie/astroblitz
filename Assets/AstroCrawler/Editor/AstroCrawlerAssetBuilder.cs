// AstroCrawler - migrates the JSON tables into ScriptableObject assets.
//
// Idempotent: run it twice and it updates the assets in place rather than
// creating duplicates, so it doubles as "re-import the JSON after editing it by
// hand". The individual assets deliberately live outside Resources - only the
// GameDatabase needs to be there, and Unity pulls in everything it references.

using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;

namespace AstroCrawler
{
    public static class AstroCrawlerAssetBuilder
    {
        public const string DatabaseFolder = "Assets/AstroCrawler/Database";
        public const string ResourcesFolder = "Assets/AstroCrawler/Resources/AstroCrawler";
        public const string DatabasePath = ResourcesFolder + "/GameDatabase.asset";
        public const string PrefabLibraryPath = DatabaseFolder + "/PrefabLibrary.asset";
        public const string ProgressionPath = DatabaseFolder + "/Progression.asset";

        [MenuItem("Tools/AstroCrawler/Gerar ScriptableObjects", false, 40)]
        public static void BuildFromJson()
        {
            var config = GameConfig.LoadFromJson();
            if (config.cards.Count == 0 && config.ships.Count == 0)
            {
                Debug.LogError("[AstroCrawler] Nenhuma tabela JSON encontrada; nada a converter.");
                return;
            }

            EnsureFolder(DatabaseFolder);
            EnsureFolder(DatabaseFolder + "/Cards");
            EnsureFolder(DatabaseFolder + "/Ships");
            EnsureFolder(DatabaseFolder + "/Enemies");
            EnsureFolder(DatabaseFolder + "/Waves");

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            database.cards.Clear();
            database.ships.Clear();
            database.enemies.Clear();
            database.waves.Clear();

            for (var i = 0; i < config.cards.Count; i++)
            {
                var definition = config.cards[i];
                var asset = LoadOrCreate<CardAsset>(
                    DatabaseFolder + "/Cards/Card_" + Safe(definition.id) + ".asset");
                asset.definition = definition;
                EditorUtility.SetDirty(asset);
                database.cards.Add(asset);
            }

            for (var i = 0; i < config.ships.Count; i++)
            {
                var definition = config.ships[i];
                var asset = LoadOrCreate<ShipAsset>(
                    DatabaseFolder + "/Ships/Ship_" + Safe(definition.id) + ".asset");
                asset.definition = definition;
                EditorUtility.SetDirty(asset);
                database.ships.Add(asset);
            }

            for (var i = 0; i < config.enemies.Count; i++)
            {
                var definition = config.enemies[i];
                var asset = LoadOrCreate<EnemyAsset>(
                    DatabaseFolder + "/Enemies/Enemy_" + definition.kind + ".asset");
                asset.definition = definition;
                EditorUtility.SetDirty(asset);
                database.enemies.Add(asset);
            }

            for (var i = 0; i < config.waves.Count; i++)
            {
                var definition = config.waves[i];
                var asset = LoadOrCreate<WaveAsset>(
                    DatabaseFolder + "/Waves/Wave_" + definition.index.ToString("00") + ".asset");
                asset.definition = definition;
                EditorUtility.SetDirty(asset);
                database.waves.Add(asset);
            }

            var progression = LoadOrCreate<ProgressionAsset>(ProgressionPath);
            progression.definition = config.progression;
            EditorUtility.SetDirty(progression);
            database.progression = progression;

            // The prefab library is created empty here and filled by the prefab
            // builder; wiring it now means the database is complete either way.
            var prefabs = LoadOrCreate<PrefabLibrary>(PrefabLibraryPath);
            database.prefabs = prefabs;

            EditorUtility.SetDirty(prefabs);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AstroCrawler] ScriptableObjects gerados: " +
                      database.cards.Count + " cartas, " +
                      database.ships.Count + " naves, " +
                      database.enemies.Count + " inimigos, " +
                      database.waves.Count + " ondas. Banco em " + DatabasePath);
        }

        /// <summary>
        /// Bakes the Rajdhani SDF font asset into the project.
        ///
        /// Building it at runtime worked, but a runtime asset cannot be
        /// serialized: every label authored into the scene came back with a null
        /// font, and TMP logged a warning per label on every scene load. As a
        /// real asset it is referenced like any other, and the SDF no longer has
        /// to be rasterized at boot.
        /// </summary>
        [MenuItem("Tools/AstroCrawler/Gerar fonte TMP", false, 43)]
        public static void BuildFontAsset()
        {
            BakeFont("Rajdhani-SemiBold.ttf", FontAssetName);

            // The real bold cut, for titles and buttons: on a phone screen a
            // true bold reads better than TMP's synthesized one, which only
            // dilates the SemiBold outline.
            BakeFont("Rajdhani-Bold.ttf", BoldFontAssetName);
        }

        private static void BakeFont(string ttfName, string assetName)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ResourcesFolder + "/Art/" + ttfName);
            if (ttf == null)
            {
                Debug.LogError("[AstroCrawler] " + ttfName + " nao encontrada em " +
                               ResourcesFolder + "/Art/");
                return;
            }

            var path = ResourcesFolder + "/Art/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            if (existing != null)
            {
                Debug.Log("[AstroCrawler] Fonte TMP ja existe em " + path);
                return;
            }

            // Dynamic population so accented Portuguese glyphs are rasterized on
            // demand instead of having to be baked in advance.
            var fontAsset = TMPro.TMP_FontAsset.CreateFontAsset(ttf, 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                TMPro.AtlasPopulationMode.Dynamic, true);

            if (fontAsset == null)
            {
                Debug.LogError("[AstroCrawler] TMP nao conseguiu gerar o font asset.");
                return;
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, path);

            // The atlas and the material have to travel with it as sub-assets,
            // or the saved font asset points at objects that no longer exist.
            if (fontAsset.atlasTextures != null)
            {
                for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var atlas = fontAsset.atlasTextures[i];
                    if (atlas == null) { continue; }
                    atlas.name = "Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AstroCrawler] Fonte TMP gerada em " + path);
        }

        public const string FontAssetName = "AstroCrawler SDF";
        public const string BoldFontAssetName = "AstroCrawler Bold SDF";

        [MenuItem("Tools/AstroCrawler/Selecionar banco de dados", false, 41)]
        public static void SelectDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogWarning("[AstroCrawler] Banco ainda nao gerado. " +
                                 "Use Tools > AstroCrawler > Gerar ScriptableObjects.");
                return;
            }
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        // ------------------------------------------------------------------

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "sem_id";
            }
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                builder.Append(System.Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            }
            return builder.ToString();
        }

        /// <summary>Used by the validation pass to report what is wired up.</summary>
        public static List<string> Describe(GameDatabase database)
        {
            var lines = new List<string>();
            if (database == null)
            {
                lines.Add("Banco de dados ausente.");
                return lines;
            }
            lines.Add("Cartas: " + database.cards.Count);
            lines.Add("Naves: " + database.ships.Count);
            lines.Add("Inimigos: " + database.enemies.Count);
            lines.Add("Ondas: " + database.waves.Count);
            lines.Add("Progressao: " + (database.progression != null ? "ligada" : "ausente"));
            lines.Add("Prefabs: " + (database.prefabs != null ? "ligado" : "ausente"));
            return lines;
        }
    }
}
