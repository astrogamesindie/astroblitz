// AstroCrawler - turns the Hovl Studio "Background VFX" pack into game assets.
//
// The pack's materials use the built-in pipeline's Particles/Standard Unlit
// shader. The game renders with URP (2D Renderer), so this tool writes URP
// copies of the materials it needs and the warp prefab built from two of the
// pack's effects, into Resources where the runtime (HovlVfx) loads them. The
// plugin folder itself is never modified.
//
// Idempotent: existing assets are overwritten in place, so references into
// them survive. Run it again after changing the lists below.

using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

namespace AstroCrawler
{
    public static class AstroCrawlerVfxBuilder
    {
        private const string HovlFolder = "Assets/Plugins/Hovl Studio/Background VFX";
        private const string OutputFolder = "Assets/AstroCrawler/Resources/AstroCrawler/VFX";
        private const string MaterialFolder = OutputFolder + "/Materials";

        /// <summary>Materials the code-built emitters use (HovlVfx.Material), besides those the prefabs bring.</summary>
        private static readonly string[] EmitterMaterials =
        {
            "Flare6", "Glow1", "Glow2", "Aura4", "Aura8", "Debris2", "Point1", "Smoke1", "Quad1"
        };

        [MenuItem("Tools/AstroCrawler/Gerar VFX (Hovl)", false, 46)]
        public static void BuildAll()
        {
            AstroCrawlerAssetBuilder.EnsureFolder(OutputFolder);
            AstroCrawlerAssetBuilder.EnsureFolder(MaterialFolder);

            var converted = new Dictionary<Material, Material>();
            for (var i = 0; i < EmitterMaterials.Length; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(HovlFolder + "/Materials/" + EmitterMaterials[i] + ".mat");
                if (source == null) Debug.LogWarning("[AstroCrawler] Material Hovl ausente: " + EmitterMaterials[i]);
                else Convert(source, converted);
            }

            BuildWarp(converted);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AstroCrawler] VFX Hovl gerados em " + OutputFolder + " (" + converted.Count + " materiais).");
        }

        // ------------------------------------------------------------------
        // Materials
        // ------------------------------------------------------------------

        /// <summary>A URP Particles/Unlit copy of a Hovl material: same texture, tint and blending.</summary>
        private static Material Convert(Material source, Dictionary<Material, Material> converted)
        {
            if (source == null) return null;
            Material copy;
            if (converted.TryGetValue(source, out copy)) return copy;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var path = MaterialFolder + "/" + HovlVfx.MaterialPrefix + source.name + ".mat";
            copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(shader);
                AssetDatabase.CreateAsset(copy, path);
            }
            copy.shader = shader;
            copy.SetTexture("_BaseMap", source.mainTexture);
            copy.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);

            // Particles/Standard modes: 2 Fade, 3 Transparent, 4 Additive, 6 Modulate.
            var mode = source.HasProperty("_Mode") ? Mathf.RoundToInt(source.GetFloat("_Mode")) : 4;
            var additive = mode == 4;
            var multiply = mode == 6;
            copy.SetFloat("_Surface", 1f);
            copy.SetFloat("_Blend", additive ? 2f : multiply ? 3f : 0f);
            copy.SetFloat("_SrcBlend", (float)(multiply ? BlendMode.DstColor : BlendMode.SrcAlpha));
            copy.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : multiply ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            copy.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            copy.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            copy.SetFloat("_ZWrite", 0f);
            copy.SetFloat("_Cull", (float)CullMode.Off);
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (multiply) copy.EnableKeyword("_ALPHAMODULATE_ON");
            else copy.DisableKeyword("_ALPHAMODULATE_ON");
            copy.renderQueue = (int)RenderQueue.Transparent;
            copy.enableInstancing = true;
            EditorUtility.SetDirty(copy);

            converted[source] = copy;
            return copy;
        }

        // ------------------------------------------------------------------
        // Warp prefab
        // ------------------------------------------------------------------

        /// <summary>
        /// The jump into a stage: Hovl's "Star trip" (stars, streaks and nebula
        /// rushing outward from the centre, where WarpTransition draws the
        /// ship). Unscaled time - the stage starts with the game paused on the
        /// first card pick - and prewarm, so the field is full on frame one.
        /// </summary>
        private static void BuildWarp(Dictionary<Material, Material> converted)
        {
            var root = new GameObject(HovlVfx.WarpPrefab);
            AddEffect(root.transform, "Star trip", "Stars", 320, converted);
            PrefabUtility.SaveAsPrefabAsset(root, OutputFolder + "/" + HovlVfx.WarpPrefab + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void AddEffect(Transform parent, string source, string name, int order,
            Dictionary<Material, Material> converted)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HovlFolder + "/Prefabs/" + source + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[AstroCrawler] Prefab Hovl ausente: " + source);
                return;
            }
            // A plain copy, not a prefab instance: the game prefab must not
            // follow later edits to the plugin's demo prefabs.
            var effect = Object.Instantiate(prefab, parent, false);
            effect.name = name;

            var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                var main = system.main;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                main.prewarm = main.loop;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                renderer.sharedMaterial = Convert(renderer.sharedMaterial, converted);
                renderer.sortingOrder = order + i;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
