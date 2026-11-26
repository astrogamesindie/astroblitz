// AstroCrawler - the Hovl Studio effects, as the game uses them.

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace AstroCrawler
{
    /// <summary>How a particle's size moves over its life.</summary>
    public enum VfxSize
    {
        Steady,
        Grow,
        Shrink,
        Pop
    }

    /// <summary>How a particle's alpha moves over its life.</summary>
    public enum VfxFade
    {
        Out,
        InOut
    }

    /// <summary>
    /// Loads the URP copies of the Hovl Studio materials and the warp prefab
    /// that Tools > AstroCrawler > Gerar VFX (Hovl) writes into Resources, and
    /// builds small code-driven emitters on them. Emitters are made once (per
    /// ship or per transition) and fed with Emit, so combat never instantiates.
    /// </summary>
    public static class HovlVfx
    {
        public const string Folder = "AstroCrawler/VFX/";
        public const string MaterialPrefix = "Hovl_";
        public const string WarpPrefab = "Warp";

        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> emitterMaterials = new Dictionary<string, Material>();
        private static GameObject warp;

        /// <summary>
        /// Hovl's materials multiply by up to 8 (HDR) to burn in a full-screen
        /// background. Particle colours stop at 1, so on a small emitter that
        /// turns every tint white; emitters get a copy capped at this.
        /// </summary>
        private const float EmitterIntensity = 1.6f;

        /// <summary>A converted Hovl material by its original name ("Flare6"); the sprite material if missing.</summary>
        public static Material Material(string name)
        {
            Material material;
            if (materials.TryGetValue(name, out material) && material != null) return material;
            material = Resources.Load<Material>(Folder + "Materials/" + MaterialPrefix + name);
            if (material == null)
            {
                Debug.LogWarning("[AstroCrawler] VFX sem material " + name + "; rode Tools > AstroCrawler > Gerar VFX (Hovl).");
                material = ArtLibrary.SpriteMaterial;
            }
            materials[name] = material;
            return material;
        }

        public static GameObject Warp
        {
            get
            {
                if (warp == null) warp = Resources.Load<GameObject>(Folder + WarpPrefab);
                return warp;
            }
        }

        /// <summary>The material an Emitter uses: the converted one, its intensity capped (one copy per name).</summary>
        private static Material EmitterMaterial(string name)
        {
            Material material;
            if (emitterMaterials.TryGetValue(name, out material) && material != null) return material;
            var shared = Material(name);
            material = shared;
            if (shared != null && shared.HasProperty("_BaseColor"))
            {
                var tint = shared.GetColor("_BaseColor");
                var peak = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                if (peak > EmitterIntensity)
                {
                    material = new Material(shared) { name = shared.name + " (Fx)", hideFlags = HideFlags.HideAndDontSave };
                    var scale = EmitterIntensity / peak;
                    material.SetColor("_BaseColor", new Color(tint.r * scale, tint.g * scale, tint.b * scale, tint.a));
                }
            }
            emitterMaterials[name] = material;
            return material;
        }

        public static void Clear()
        {
            foreach (var pair in emitterMaterials)
            {
                var material = pair.Value;
                if (material != null && (material.hideFlags & HideFlags.DontSave) != 0) Object.Destroy(material);
            }
            emitterMaterials.Clear();
            materials.Clear();
            warp = null;
        }

        /// <summary>
        /// One emitter for Emit calls: no emission or shape of its own, local
        /// space (it rides on the ship), a fade and a size curve.
        /// </summary>
        public static ParticleSystem Emitter(Transform parent, string name, string material, int limit,
            int order, VfxSize size, VfxFade fade = VfxFade.Out,
            ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize3D = true;
            main.maxParticles = limit;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fade == VfxFade.InOut
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f),
                        new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            if (size != VfxSize.Steady)
            {
                var curve = system.sizeOverLifetime;
                curve.enabled = true;
                AnimationCurve shapeCurve;
                switch (size)
                {
                    case VfxSize.Grow:
                        shapeCurve = AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f);
                        break;
                    case VfxSize.Shrink:
                        shapeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f);
                        break;
                    default:
                        shapeCurve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1.15f),
                            new Keyframe(1f, 0.8f));
                        break;
                }
                curve.size = new ParticleSystem.MinMaxCurve(1f, shapeCurve);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EmitterMaterial(material);
            renderer.renderMode = mode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 2f;
            }
            renderer.sortingOrder = order;
            renderer.maxParticleSize = 4f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(true);
            system.Play(false);
            return system;
        }

        public static void Emit(ParticleSystem system, Vector2 position, Vector2 velocity, Color color,
            float lifetime, Vector2 size, float rotation = 0f, float spin = 0f)
        {
            if (system == null) return;
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startLifetime = lifetime,
                startSize3D = new Vector3(size.x, size.y, 1f),
                rotation = rotation,
                angularVelocity = spin
            };
            system.Emit(parameters, 1);
        }

        public static void Emit(ParticleSystem system, Vector2 position, Vector2 velocity, Color color,
            float lifetime, float size, float rotation = 0f, float spin = 0f)
        {
            Emit(system, position, velocity, color, lifetime, new Vector2(size, size), rotation, spin);
        }

        public static void Clear(ParticleSystem system)
        {
            if (system != null) system.Clear(false);
        }
    }
}
