// AstroCrawler - single entry point.
//
// Drop this component on one empty GameObject and the whole game exists: it
// loads the JSON tables, builds the camera, the arena, the systems and the UI.
// Keeping the scene down to a single component means there are no prefab GUIDs
// to break, and the game can be reconstructed from source alone.
//
// The singleton-that-owns-the-configuration shape is lifted from CCG Kit's
// GameManager; everything it points at is new.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AstroCrawler
{
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Tooltip("Log every data problem found in the JSON tables at boot.")]
        public bool validateConfigurationOnBoot = true;

        [Tooltip("Skip ship selection and start with this ship id. Leave empty for the menu.")]
        public string debugStartShipId = "";

        // Wired by the scene builder. Anything left empty is created at runtime,
        // so the component still works when dropped on a bare GameObject.
        [Header("Sistemas (objetos da cena)")]
        [SerializeField] private JuiceDirector sceneJuice;
        [SerializeField] private CombatWorld sceneWorld;
        [SerializeField] private EffectRunner sceneEffects;
        [SerializeField] private WaveController sceneWaves;
        [SerializeField] private RunController sceneRun;
        [SerializeField] private Transform sceneStarfield;
        [SerializeField] private GameUI sceneUI;
        [SerializeField] private SoundDirector sceneSound;

        public GameConfig config { get; private set; }
        public GameDatabase database { get; private set; }
        public CombatWorld world { get; private set; }
        public EffectRunner effects { get; private set; }
        public WaveController waves { get; private set; }
        public RunController run { get; private set; }
        public GameUI ui { get; private set; }
        public JuiceDirector juice { get; private set; }
        public SoundDirector sound { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            StoreUpdateGate.EnsureCreated();
            AndroidPurchases.EnsureCreated();
            ShipPurchases.EnsureCreated();
            AndroidAds.EnsureCreated();
            // Both survive scene reloads (an account change reloads the scene).
            AccountService.EnsureCreated();
            ConnectivityGuard.EnsureCreated();
            // Silent Play Games / Game Center sign-in for the endless ranking.
            Leaderboards.EnsureCreated();

            // The database carries both the tables and the prefab library, so
            // one Resources lookup wires up the whole game.
            database = GameDatabase.Load();
            if (database != null)
            {
                config = database.BuildConfig();
                PrefabLibrary.SetActive(database.prefabs);
            }
            else
            {
                config = GameConfig.LoadFromResources();
            }

            if (validateConfigurationOnBoot)
            {
                var problems = config.Validate();
                for (var i = 0; i < problems.Count; i++)
                {
                    Debug.LogWarning("[AstroCrawler] " + problems[i]);
                }
            }

            // A dense wave with a pierce build runs a lot of concurrent tweens:
            // bullets, sparks, debris and numbers. Sizing the pool up front
            // avoids LeanTween reallocating mid-fight.
            LeanTween.init(2400);

            var camera = BuildCamera();

            // Built before anything that can shake, flash or pop - which, with
            // the ship's entrance animation, is almost everything below.
            juice = Attach(sceneJuice, "JuiceDirector");
            juice.Initialize(camera);

            sound = Attach(sceneSound, "SoundDirector");
            sound.Initialize(database != null ? database.sounds : null);

            // The scene builder bakes the starfield, so this only runs on a bare
            // GameObject - 213 stars is 213 AddComponent calls otherwise.
            var starfield = sceneStarfield;
            if (starfield == null)
            {
                starfield = BuildStarfield();
            }

            // Portrait or landscape: the camera follows the screen's shape.
            var fit = camera.GetComponent<ArenaCamera>();
            if (fit == null) fit = camera.gameObject.AddComponent<ArenaCamera>();
            fit.Configure(starfield);

            world = Attach(sceneWorld, "CombatWorld");
            world.Initialize(config);

            effects = Attach(sceneEffects, "EffectRunner");
            effects.Initialize(world);

            waves = Attach(sceneWaves, "WaveController");
            waves.Initialize(world);

            run = Attach(sceneRun, "RunController");
            run.Initialize(config, world, effects, waves);

            EnsureEventSystem();
        }

        private void Start()
        {
            // The UI is built here rather than in Awake on purpose. Its first
            // label is what forces TextMeshPro to build the SDF font asset, and
            // asking TMP for that during the scene's very first Awake returns an
            // asset with no material - which then throws the moment anything
            // reads it. By Start, TMP has finished initialising.
            if (sceneUI != null)
            {
                // Authored into the scene: the hierarchy is already there, so
                // only the behaviour needs attaching.
                ui = sceneUI;
                ui.Bind(run, config, effects, world);
            }
            else
            {
                ui = new GameObject("GameUI").AddComponent<GameUI>();
                ui.transform.SetParent(transform, false);
                ui.Build(run, config, effects, world);
            }

            if (string.IsNullOrEmpty(debugStartShipId))
            {
                return;
            }

            var ship = config.GetShip(debugStartShipId);
            if (ship != null)
            {
                run.BeginRun(ship);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Static caches would otherwise hand the next scene sprites and
            // pooled objects that Unity has already destroyed.
            LeanTween.cancelAll();
            Impact.Dispose();
            HovlVfx.Clear();
            ArtLibrary.Clear();
            Projectile.ResetPool();
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------
        // Scene construction
        // ------------------------------------------------------------------

        /// <summary>
        /// Uses the system the scene already carries, or builds one. Keeping the
        /// fallback means this component still works when someone drops it on an
        /// empty GameObject, which is how the project worked before the scene
        /// was authored.
        /// </summary>
        private T Attach<T>(T fromScene, string name) where T : MonoBehaviour
        {
            if (fromScene != null)
            {
                return fromScene;
            }

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private Camera BuildCamera()
        {
            var existing = Camera.main;
            if (existing == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                existing = go.AddComponent<Camera>();
            }

            existing.orthographic = true;
            existing.orthographicSize = Arena.CameraSize;
            existing.transform.position = new Vector3(0f, 0f, -10f);
            existing.clearFlags = CameraClearFlags.SolidColor;
            existing.backgroundColor = new Color(0.035f, 0.04f, 0.08f);

            // Without a listener every AudioSource "plays" - isPlaying is true -
            // and nothing reaches the speakers. The scene shipped without one,
            // which is exactly how the game ended up silent.
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                existing.gameObject.AddComponent<AudioListener>();
            }
            return existing;
        }

        private Transform BuildStarfield()
        {
            return BuildStarfieldInto(transform);
        }

        /// <summary>
        /// Builds the three parallax star planes. Public and parented explicitly
        /// so the scene builder can bake the result into the scene, which turns
        /// 213 runtime AddComponent calls into zero.
        /// </summary>
        public static Transform BuildStarfieldInto(Transform parent)
        {
            var root = new GameObject("Starfield").transform;
            root.SetParent(parent, false);

            var sprite = ArtLibrary.SoftCircle;
            var random = new System.Random(20260909);

            BuildStarLayer(root, sprite, random, "Far", 120, -30,
                0.025f, 0.055f, 0.12f, 0.34f, new Color(0.55f, 0.68f, 1f),
                new Vector2(0.045f, 0.025f), 0.10f, 0.045f);
            BuildStarLayer(root, sprite, random, "Mid", 65, -29,
                0.05f, 0.10f, 0.28f, 0.62f, new Color(0.72f, 0.84f, 1f),
                new Vector2(0.09f, 0.045f), 0.18f, 0.085f);
            BuildStarLayer(root, sprite, random, "Near", 28, -28,
                0.09f, 0.16f, 0.55f, 0.95f, new Color(0.92f, 0.95f, 1f),
                new Vector2(0.16f, 0.08f), 0.28f, 0.15f);

            return root;
        }

        private static void BuildStarLayer(Transform root, Sprite sprite, System.Random random,
            string name, int count, int sortingOrder, float minSize, float maxSize,
            float minBrightness, float maxBrightness, Color tint, Vector2 drift, float twinkle,
            float movementSpeed)
        {
            var layer = new GameObject(name).transform;
            layer.SetParent(root, false);
            var stars = new SpriteRenderer[count];

            for (var i = 0; i < count; i++)
            {
                var x = (float)(random.NextDouble() * 32.0 - 16.0);
                var y = (float)(random.NextDouble() * 20.0 - 10.0);
                var size = Mathf.Lerp(minSize, maxSize, (float)random.NextDouble());
                var brightness = Mathf.Lerp(minBrightness, maxBrightness, (float)random.NextDouble());

                // 213 stars is 213 AddComponent calls at boot without a prefab.
                var starPrefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.star : null;
                SpriteRenderer star;
                if (starPrefab != null)
                {
                    star = Instantiate(starPrefab, layer);
                    star.sprite = sprite;
                    star.sortingOrder = sortingOrder;
                }
                else
                {
                    star = SpriteFactory.NewRenderer("Star", sprite, Color.white, sortingOrder, layer);
                }
                star.color = new Color(tint.r, tint.g, tint.b, brightness);
                star.transform.localPosition = new Vector3(x, y, 0f);
                // The star sizes are authored in world units, so the imported
                // sprite has to be normalised the same way enemies are.
                star.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(sprite, size);
                stars[i] = star;
            }

            var motion = layer.gameObject.AddComponent<StarfieldLayer>();
            motion.Configure(stars, drift, twinkle, movementSpeed);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
