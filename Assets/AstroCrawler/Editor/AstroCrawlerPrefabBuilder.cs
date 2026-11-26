// AstroCrawler - authors the runtime prefabs and fills the PrefabLibrary.
//
// The prefabs are generated rather than hand-built for the same reason the scene
// is: every value that matters (sorting order, trail width, sprite choice) is
// already a decision made in source, and duplicating it by hand in the Inspector
// is how the two drift apart. Run this again after changing those values.
//
// Idempotent - SaveAsPrefabAsset overwrites in place, so existing references
// into these prefabs survive.

using System.IO;

using TMPro;

using UnityEditor;

using UnityEngine;

namespace AstroCrawler
{
    public static class AstroCrawlerPrefabBuilder
    {
        public const string PrefabFolder = "Assets/AstroCrawler/Prefabs";

        [MenuItem("Tools/AstroCrawler/Gerar Prefabs", false, 42)]
        public static void BuildAll()
        {
            AstroCrawlerAssetBuilder.EnsureFolder(PrefabFolder);

            var library = AstroCrawlerAssetBuilder.LoadOrCreate<PrefabLibrary>(
                AstroCrawlerAssetBuilder.PrefabLibraryPath);

            library.enemy = Save(BuildEnemy(), "Enemy").GetComponent<Enemy>();
            library.playerShip = Save(BuildPlayerShip(), "PlayerShip").GetComponent<PlayerShip>();
            library.projectile = Save(BuildProjectile(), "Projectile").GetComponent<Projectile>();
            library.missile = Save(BuildMissile(), "Missile").GetComponent<Missile>();
            library.xpOrb = Save(BuildXpOrb(), "XpOrb").GetComponent<XpOrb>();
            library.blackHole = Save(BuildBlackHole(), "BlackHole").GetComponent<BlackHole>();
            library.shockwave = Save(BuildRingAbility<Shockwave>("Shockwave", 18), "Shockwave").GetComponent<Shockwave>();
            library.pulse = Save(BuildRingAbility<Pulse>("Pulse", 17), "Pulse").GetComponent<Pulse>();
            library.debrisWall = Save(BuildDebrisWall(), "DebrisWall").GetComponent<DebrisWall>();

            library.damageNumber = Save(BuildDamageNumber(), "DamageNumber").GetComponent<DamageNumber>();
            library.impactSprite = Save(BuildImpactSprite(), "ImpactSprite").GetComponent<SpriteRenderer>();
            library.star = Save(BuildStar(), "Star").GetComponent<SpriteRenderer>();

            EditorUtility.SetDirty(library);

            // Keep the database pointing at the library even if it was generated
            // before this tool existed.
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(
                AstroCrawlerAssetBuilder.DatabasePath);
            if (database != null && database.prefabs != library)
            {
                database.prefabs = library;
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AstroCrawler] Prefabs gerados em " + PrefabFolder +
                      " e ligados em " + AstroCrawlerAssetBuilder.PrefabLibraryPath);
        }

        /// <summary>
        /// Saves only XpOrb.prefab and wires it into the library, leaving every
        /// other prefab (and any hand edit made to it) untouched.
        /// </summary>
        [MenuItem("Tools/AstroCrawler/Gerar prefab do orbe de XP", false, 42)]
        public static void BuildXpOrbOnly()
        {
            AstroCrawlerAssetBuilder.EnsureFolder(PrefabFolder);
            var library = AstroCrawlerAssetBuilder.LoadOrCreate<PrefabLibrary>(
                AstroCrawlerAssetBuilder.PrefabLibraryPath);
            library.xpOrb = Save(BuildXpOrb(), "XpOrb").GetComponent<XpOrb>();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[AstroCrawler] XpOrb.prefab gerado e ligado na PrefabLibrary.");
        }

        // ------------------------------------------------------------------

        private static GameObject Save(GameObject instance, string name)
        {
            var path = PrefabFolder + "/" + name + ".prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return asset;
        }

        private static SpriteRenderer AddRenderer(GameObject parent, string name, Sprite sprite,
            Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Writes a private [SerializeField] reference on a component.</summary>
        private static void Wire(Component target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[AstroCrawler] Campo '" + field + "' nao existe em " +
                                 target.GetType().Name);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------

        private static GameObject BuildEnemy()
        {
            var root = new GameObject("Enemy");
            var enemy = root.AddComponent<Enemy>();

            var body = AddRenderer(root, "Body", ArtLibrary.Asteroid(0), Color.white, 10);
            var glow = AddRenderer(root, "Glow", ArtLibrary.DashedRing, Color.white, 9);
            glow.gameObject.SetActive(false);

            Wire(enemy, "body", body);
            Wire(enemy, "glow", glow);
            return root;
        }

        private static GameObject BuildPlayerShip()
        {
            var root = new GameObject("PlayerShip");
            var ship = root.AddComponent<PlayerShip>();

            var aura = AddRenderer(root, "Aura", ArtLibrary.Glow, Color.white, 4);
            var hull = AddRenderer(root, "Hull", ArtLibrary.Ship, Color.white, 20);
            var shield = AddRenderer(root, "Shield", ArtLibrary.DashedRing,
                new Color(0.55f, 0.9f, 1f, 0.9f), 21);
            shield.gameObject.SetActive(false);

            Wire(ship, "aura", aura);
            Wire(ship, "hull", hull);
            Wire(ship, "shieldRing", shield);
            return root;
        }

        private static GameObject BuildXpOrb()
        {
            var root = new GameObject("XpOrb");
            var orb = root.AddComponent<XpOrb>();
            var body = AddRenderer(root, "Body", ArtLibrary.SoftCircle,
                new Color(0.36f, 0.82f, 1f, 0.95f), 13);
            Wire(orb, "body", body);
            return root;
        }

        private static GameObject BuildProjectile()
        {
            var root = new GameObject("Projectile");
            root.transform.localScale = Vector3.one * 0.3f;

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.Bullet;
            renderer.sortingOrder = 15;

            AddTrail(root, 0.11f, 0.16f, 14);
            root.AddComponent<Projectile>();
            return root;
        }

        private static GameObject BuildMissile()
        {
            var root = new GameObject("Missile");
            root.transform.localScale = Vector3.one * 0.4f;

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.MissileBody;
            renderer.color = new Color(1f, 0.72f, 0.5f);
            renderer.sortingOrder = 16;

            var trail = AddTrail(root, 0.3f, 0.22f, 15);
            if (trail != null)
            {
                trail.startColor = new Color(1f, 0.6f, 0.3f, 0.7f);
                trail.endColor = new Color(1f, 0.3f, 0.1f, 0f);
            }

            root.AddComponent<Missile>();
            return root;
        }

        private static TrailRenderer AddTrail(GameObject go, float time, float width, int sortingOrder)
        {
            var material = ArtLibrary.SpriteMaterial;
            if (material == null)
            {
                return null;
            }

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.06f;
            trail.numCapVertices = 2;
            trail.sortingOrder = sortingOrder;
            trail.receiveShadows = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.alignment = LineAlignment.View;
            return trail;
        }

        private static GameObject BuildDamageNumber()
        {
            var root = new GameObject("DamageNumber");

            // Inactive while the component is added: TMP's Awake goes hunting
            // for a default font asset this project does not have, and logs a
            // warning per instance if it runs before the font is assigned.
            root.SetActive(false);

            var label = root.AddComponent<TextMeshPro>();
            label.fontSize = 3.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(6f, 2f);

            var meshRenderer = root.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 60;
            }

            var number = root.AddComponent<DamageNumber>();
            Wire(number, "label", label);
            Wire(number, "meshRenderer", meshRenderer);
            return root;
        }

        /// <summary>Shockwave and Pulse are the same shape: a root plus one ring.</summary>
        private static GameObject BuildRingAbility<T>(string name, int sortingOrder)
            where T : MonoBehaviour
        {
            var root = new GameObject(name);
            var component = root.AddComponent<T>();
            var ring = AddRenderer(root, "Ring", ArtLibrary.RingSprite, Color.white, sortingOrder);
            Wire(component, "ring", ring);
            return root;
        }

        private static GameObject BuildBlackHole()
        {
            var root = new GameObject("BlackHole");
            var hole = root.AddComponent<BlackHole>();

            var halo = AddRenderer(root, "Halo", ArtLibrary.Glow,
                new Color(0.55f, 0.3f, 0.9f, 0.25f), 6);
            var core = AddRenderer(root, "Core",
                ArtLibrary.ArtOr("poly_disc", SpriteFactory.Disc(0.35f)),
                new Color(0.08f, 0.02f, 0.15f, 0.97f), 7);
            var accretion = AddRenderer(root, "Accretion", ArtLibrary.DashedRing,
                new Color(0.8f, 0.55f, 1f, 0.55f), 8);

            Wire(hole, "halo", halo);
            Wire(hole, "core", core);
            Wire(hole, "accretion", accretion);
            return root;
        }

        private static GameObject BuildDebrisWall()
        {
            var root = new GameObject("DebrisWall");
            var wall = root.AddComponent<DebrisWall>();
            var body = AddRenderer(root, "Body",
                ArtLibrary.ArtOr("wire_hex", SpriteFactory.Polygon(6, 0f)),
                new Color(0.72f, 0.66f, 0.55f, 0.95f), 12);
            Wire(wall, "body", body);
            return root;
        }

        private static GameObject BuildImpactSprite()
        {
            var root = new GameObject("Impact");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.Glow;
            renderer.sortingOrder = 30;
            return root;
        }

        private static GameObject BuildStar()
        {
            var root = new GameObject("Star");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtLibrary.SoftCircle;
            renderer.sortingOrder = -30;
            return root;
        }
    }
}
