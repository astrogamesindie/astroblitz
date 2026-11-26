// AstroCrawler - creates (and registers) the single playable scene.
//
// The scene is generated rather than committed as YAML on purpose: it holds two
// objects and no prefab references, so generating it guarantees the component
// GUIDs always match the freshly compiled scripts.

using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroCrawler
{
    public static class AstroCrawlerSceneBuilder
    {
        public const string ScenePath = "Assets/AstroCrawler/Scenes/AstroCrawler.unity";

        private const string BootstrapKey = "AstroCrawler_SceneBootstrapped";
        private const int MaxBootstrapRetries = 20;

        private static int bootstrapRetries;

        /// <summary>
        /// Creates the scene the first time the project is opened after import,
        /// so the game is playable without hunting for a menu item.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void BootstrapOnce()
        {
            bootstrapRetries = 0;
            EditorApplication.delayCall += TryBootstrap;
        }

        private static void TryBootstrap()
        {
            if (EditorPrefs.GetBool(BootstrapKey, false) || File.Exists(ScenePath))
            {
                RegisterInBuildSettings();
                return;
            }

            // Right after an import Unity may still be compiling or refreshing;
            // creating a scene in that window is unreliable, so wait it out.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            {
                if (bootstrapRetries++ < MaxBootstrapRetries)
                {
                    EditorApplication.delayCall += TryBootstrap;
                }
                return;
            }

            // Only take over the editor when doing so costs the user nothing:
            // creating the scene replaces whatever is currently open.
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                Debug.Log("[AstroCrawler] A cena do jogo ainda não existe. " +
                          "Use Tools ▸ AstroCrawler ▸ Abrir cena do jogo para criá-la " +
                          "(a cena aberta agora seria substituída, então não criei sozinho).");
                EditorPrefs.SetBool(BootstrapKey, true);
                return;
            }

            if (CreateScene())
            {
                EditorPrefs.SetBool(BootstrapKey, true);
            }
        }

        [MenuItem("Tools/AstroCrawler/Abrir cena do jogo", false, 21)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateScene();
                return; // CreateScene leaves the new scene open.
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                RegisterInBuildSettings();
            }
        }

        /// <summary>
        /// Builds the scene and leaves it open. Returns false if the user backed
        /// out of the "save your changes?" prompt.
        /// </summary>
        public static bool CreateScene()
        {
            var folder = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // NewScene refuses to run additively while an untitled scene is open
            // and unsaved - which is exactly the state of a freshly imported
            // project - so the scene is created in Single mode instead. Ask about
            // unsaved work first, because Single mode replaces what is open.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Arena.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.08f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            BuildHierarchy();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[AstroCrawler] Não consegui salvar a cena em " + ScenePath);
                return false;
            }

            AssetDatabase.Refresh();
            RegisterInBuildSettings();

            Debug.Log("[AstroCrawler] Cena criada em " + ScenePath +
                      " e registrada em Build Settings. Aperte Play.");
            return true;
        }

        /// <summary>
        /// Builds the game object graph in the open scene: the manager, the four
        /// systems, the effects director, the event system and the baked
        /// starfield, all wired into GameManager's serialized fields.
        ///
        /// Everything here used to be created in GameManager.Awake. Authoring it
        /// into the scene means the engine deserializes it with the scene instead
        /// of the game building it a component at a time on the first frame - and
        /// the starfield alone is 213 objects.
        /// </summary>
        [MenuItem("Tools/AstroCrawler/Reconstruir hierarquia da cena", false, 22)]
        public static void BuildHierarchy()
        {
            var existing = Object.FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var root = new GameObject("AstroCrawler");
            var manager = root.AddComponent<GameManager>();

            var juice = Child<JuiceDirector>(root.transform, "JuiceDirector");
            var world = Child<CombatWorld>(root.transform, "CombatWorld");
            var effects = Child<EffectRunner>(root.transform, "EffectRunner");
            var waves = Child<WaveController>(root.transform, "WaveController");
            var run = Child<RunController>(root.transform, "RunController");
            var sound = Child<SoundDirector>(root.transform, "SoundDirector");

            var events = new GameObject("EventSystem");
            events.transform.SetParent(root.transform, false);
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var starfield = GameManager.BuildStarfieldInto(root.transform);

            // The interface is authored here too. It needs the database, because
            // the ship-selection cards are generated from it - which also means
            // this has to be re-run after changing a ship's stats or deck.
            GameUI ui = null;
            var database = GameDatabase.Load();
            if (database != null)
            {
                var uiGo = new GameObject("GameUI");
                uiGo.transform.SetParent(root.transform, false);
                ui = uiGo.AddComponent<GameUI>();
                ui.Construct(database.BuildConfig());
            }
            else
            {
                Debug.LogWarning("[AstroCrawler] Banco de dados ausente; a interface sera " +
                                 "construida em runtime. Rode Tools > AstroCrawler > " +
                                 "Gerar ScriptableObjects e reconstrua a cena.");
            }

            var serialized = new SerializedObject(manager);
            Assign(serialized, "sceneJuice", juice);
            Assign(serialized, "sceneWorld", world);
            Assign(serialized, "sceneEffects", effects);
            Assign(serialized, "sceneWaves", waves);
            Assign(serialized, "sceneRun", run);
            Assign(serialized, "sceneStarfield", starfield);
            Assign(serialized, "sceneUI", ui);
            Assign(serialized, "sceneSound", sound);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);

            var stars = starfield != null ? starfield.GetComponentsInChildren<SpriteRenderer>().Length : 0;
            var widgets = ui != null ? ui.GetComponentsInChildren<Transform>(true).Length : 0;
            Debug.Log("[AstroCrawler] Hierarquia reconstruida: sistemas na cena, " +
                      stars + " estrelas e " + widgets + " objetos de interface assados.");
        }

        /// <summary>
        /// Rebuilds only the baked interface, leaving the rest of the scene
        /// alone. Run it after changing what GameUI.Construct builds (the
        /// LayoutVersion constant says when that happened); until then the game
        /// still works, it just rebuilds the old bake at runtime on every boot.
        /// </summary>
        [MenuItem("Tools/AstroCrawler/Reassar interface", false, 23)]
        public static void RebakeInterface()
        {
            var ui = Object.FindFirstObjectByType<GameUI>();
            var database = GameDatabase.Load();
            if (ui == null || database == null)
            {
                Debug.LogWarning("[AstroCrawler] Abra a cena do jogo (com GameUI e o banco gerado) antes de reassar.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Reassar interface");
            ui.DiscardLayout();
            ui.Construct(database.BuildConfig());
            EditorUtility.SetDirty(ui);

            // Older scenes predate the sound system; wire one in while here.
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager != null)
            {
                var serialized = new SerializedObject(manager);
                var soundField = serialized.FindProperty("sceneSound");
                if (soundField != null && soundField.objectReferenceValue == null)
                {
                    var sound = Object.FindFirstObjectByType<SoundDirector>();
                    if (sound == null)
                    {
                        sound = Child<SoundDirector>(manager.transform, "SoundDirector");
                    }
                    soundField.objectReferenceValue = sound;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            var widgets = ui.GetComponentsInChildren<Transform>(true).Length;
            Debug.Log("[AstroCrawler] Interface reassada (layout " + GameUI.LayoutVersion + "): " +
                      widgets + " objetos. Salve a cena.");
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static void Assign(SerializedObject serialized, string field, Object value)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[AstroCrawler] Campo '" + field + "' nao existe em GameManager.");
                return;
            }
            property.objectReferenceValue = value;
        }

        /// <summary>Makes AstroCrawler the one and only scene in the build.</summary>
        private static void RegisterInBuildSettings()
        {
            if (!File.Exists(ScenePath))
            {
                return;
            }

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 1 && scenes[0].path == ScenePath && scenes[0].enabled)
            {
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
