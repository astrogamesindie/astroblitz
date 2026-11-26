using UnityEngine;
using UnityEngine.Rendering;

namespace AstroCrawler
{
    /// <summary>
    /// Run-owned particle emitters, prepared once and reused on every activation:
    /// three procedural ones (flash, sparks, rings) and four on Hovl Studio
    /// textures (HovlVfx) that keep a visible aura while an ability lasts -
    /// turning spiral rings for Aegis, a breathing gravity field and drifting
    /// dust for Stasis, heat and a nose glint for Overdrive, a fast swirl for
    /// Amplify. Cosmetic only: the ship owns the ability clock and controls
    /// continued emission.
    /// </summary>
    internal sealed class ShipSpecialVfx
    {
        private readonly ParticleSystem flash;
        private readonly ParticleSystem sparks;
        private readonly ParticleSystem rings;
        private readonly ParticleSystem aura;
        private readonly ParticleSystem haze;
        private readonly ParticleSystem glints;
        private readonly ParticleSystem dust;
        private readonly ParticleSystem heat;
        private ShipSpecialKind kind;
        private Color color;
        private float radius, elapsed, emissionTimer, pulseTimer, releaseDelay, auraTimer;
        private bool sustained, running, auraTurn;

        public ShipSpecialVfx(Transform parent)
        {
            var root = new GameObject("SpecialAbilityParticles").transform;
            root.SetParent(parent, false);
            flash = Build(root, "CoreFlash", ArtLibrary.SoftCircle, 8, 22, false);
            sparks = Build(root, "EnergySparks", SpriteFactory.Polygon(4), 128, 23, false);
            rings = Build(root, "EnergyRings", ArtLibrary.RingSprite, 12, 19, true);
            aura = HovlVfx.Emitter(root, "AuraSpiral", "Aura4", 6, 18, VfxSize.Steady, VfxFade.InOut);
            haze = HovlVfx.Emitter(root, "AuraHaze", "Aura8", 6, 17, VfxSize.Steady, VfxFade.InOut);
            glints = HovlVfx.Emitter(root, "AuraGlints", "Flare6", 24, 26, VfxSize.Pop);
            dust = HovlVfx.Emitter(root, "AuraDust", "Point1", 96, 22, VfxSize.Steady, VfxFade.InOut);
            heat = HovlVfx.Emitter(root, "AuraHeat", "Debris2", 12, 17, VfxSize.Grow);
        }

        private static ParticleSystem Build(Transform parent, string name, Sprite sprite,
            int limit, int order, bool expand)
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
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, expand
                ? AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f)
                : AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ArtLibrary.SpriteMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = order;
            renderer.maxParticleSize = 2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The existing sprite shader is included in builds. Each renderer supplies
            // its texture without cloning or changing the material shared by gameplay.
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", sprite.texture);
            renderer.SetPropertyBlock(properties);
            go.SetActive(true);
            return system;
        }

        private static void Emit(ParticleSystem system, Vector2 position, Vector2 velocity,
            Color tint, float lifetime, Vector2 size)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = tint,
                startLifetime = lifetime,
                startSize3D = new Vector3(size.x, size.y, 1f),
                rotation = velocity.sqrMagnitude > 0.001f
                    ? 90f - Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0f
            };
            system.Emit(parameters, 1);
        }

        private void Ring(float diameter, float lifetime, float alpha = 0.7f)
        {
            var tint = color;
            tint.a = alpha;
            Emit(rings, Vector2.zero, Vector2.zero, tint, lifetime, Vector2.one * diameter);
        }

        private void Radial(int count, float distance, float speed, float lifetime, float size)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Emit(sparks, direction * distance, direction * speed, color,
                    lifetime, new Vector2(size, size * 2.4f));
            }
        }

        public void Play(ShipSpecialKind ability, Color tint, float hullRadius, Vector2 forward)
        {
            Stop();
            kind = ability;
            color = Color.Lerp(tint, Color.white, 0.25f);
            color.a = 1f;
            radius = hullRadius;
            sustained = kind != ShipSpecialKind.Repair;
            running = true;
            flash.Play(false);
            sparks.Play(false);
            rings.Play(false);
            Emit(flash, Vector2.zero, Vector2.zero, Color.Lerp(color, Color.white, 0.65f),
                0.16f, Vector2.one * radius * 2.5f);
            // The activation star: a big four-point flare turning once.
            HovlVfx.Emit(glints, Vector2.zero, Vector2.zero, Color.Lerp(color, Color.white, 0.5f), 0.4f,
                radius * 7f, Random.Range(0f, 45f), 160f);
            auraTimer = 0f;

            switch (kind)
            {
                case ShipSpecialKind.Overdrive:
                    Radial(28, radius, 4.2f, 0.42f, 0.08f);
                    Ring(radius * 7f, 0.4f);
                    EngineJet(forward, 12);
                    Sfx.Play(Sound.AreaAbility);
                    Juice.Shake(0.07f, 0.16f);
                    break;
                case ShipSpecialKind.Aegis:
                    Radial(36, radius * 1.2f, 1.2f, 0.45f, 0.11f);
                    Ring(radius * 5.5f, 0.55f, 0.9f);
                    Sfx.Play(Sound.ShieldBlock);
                    Juice.Shake(0.055f, 0.12f);
                    break;
                case ShipSpecialKind.Stasis:
                    Radial(64, radius, 11f, 0.62f, 0.07f);
                    Ring(Arena.DetectionRadius * 2f, 0.7f, 0.55f);
                    Sfx.Play(Sound.AreaAbility);
                    Juice.Shake(0.045f, 0.2f);
                    break;
                case ShipSpecialKind.Repair:
                    Radial(24, radius * 3.5f, -2.4f, 0.65f, 0.12f);
                    for (var i = 0; i < 6; i++)
                    {
                        var angle = i * Mathf.PI / 3f;
                        HovlVfx.Emit(glints, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 2.4f,
                            Vector2.zero, Color.Lerp(color, Color.white, 0.4f), 0.45f, radius * 1.4f, 0f, 90f);
                    }
                    RepairCross();
                    Ring(radius * 6f, 0.7f, 0.6f);
                    Sfx.Play(Sound.Heal);
                    break;
                case ShipSpecialKind.Amplify:
                case ShipSpecialKind.ChristmasStar:
                    Radial(24, radius * 3.5f, -4.5f, 0.32f, 0.13f);
                    releaseDelay = 0.12f;
                    Sfx.Play(Sound.AreaAbility);
                    break;
            }
        }

        /// <summary>The sustained look of each ability, refreshed in overlapping waves.</summary>
        private void Aura(Vector2 forward)
        {
            auraTurn = !auraTurn;
            var turn = auraTurn ? 1f : -1f;
            switch (kind)
            {
                case ShipSpecialKind.Aegis:
                    // Two spirals turning against each other: a shell that visibly holds.
                    auraTimer = 0.42f;
                    HovlVfx.Emit(aura, Vector2.zero, Vector2.zero, WithAlpha(color, 0.85f), 0.9f,
                        radius * 4.6f, Random.Range(0f, 360f), 140f * turn);
                    for (var i = 0; i < 2; i++)
                    {
                        var angle = Random.Range(0f, Mathf.PI * 2f);
                        HovlVfx.Emit(glints, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 2.2f,
                            Vector2.zero, Color.Lerp(color, Color.white, 0.6f), 0.3f, radius * 1.3f);
                    }
                    break;
                case ShipSpecialKind.Stasis:
                    // A slow gravity field over the whole slowed zone, with dust
                    // hanging in it: time thickening around the ship.
                    auraTimer = 0.7f;
                    var field = Arena.DetectionRadius * 2.2f;
                    HovlVfx.Emit(haze, Vector2.zero, Vector2.zero, WithAlpha(color, 0.3f), 1.4f, field,
                        Random.Range(0f, 360f), 12f * turn);
                    HovlVfx.Emit(aura, Vector2.zero, Vector2.zero, WithAlpha(color, 0.14f), 1.4f, field * 0.95f,
                        Random.Range(0f, 360f), 22f * turn);
                    for (var i = 0; i < 10; i++)
                    {
                        var spot = Random.insideUnitCircle * Arena.DetectionRadius;
                        var tangent = new Vector2(-spot.y, spot.x).normalized * 0.35f;
                        HovlVfx.Emit(dust, spot, tangent, Color.Lerp(color, Color.white, Random.value * 0.6f),
                            Random.Range(1.2f, 1.6f), Random.Range(0.05f, 0.1f));
                    }
                    break;
                case ShipSpecialKind.Overdrive:
                    // Heat behind the engines, a tight spiral spinning hard and a glint on the nose.
                    auraTimer = 0.28f;
                    HovlVfx.Emit(aura, Vector2.zero, Vector2.zero, WithAlpha(color, 0.5f), 0.45f,
                        radius * 3.4f, Random.Range(0f, 360f), 480f * turn);
                    HovlVfx.Emit(heat, -forward * radius * 1.2f, -forward * 1.4f, WithAlpha(color, 0.55f), 0.5f,
                        radius * 3.2f, Random.Range(0f, 360f));
                    HovlVfx.Emit(glints, forward * radius * 1.1f, Vector2.zero,
                        Color.Lerp(color, Color.white, 0.7f), 0.22f, radius * 1.8f, Random.Range(0f, 45f));
                    break;
                case ShipSpecialKind.Amplify:
                    // A fast swirl and sparks drawn in: power gathering.
                    auraTimer = 0.35f;
                    HovlVfx.Emit(aura, Vector2.zero, Vector2.zero, WithAlpha(color, 0.6f), 0.7f,
                        radius * 5f, Random.Range(0f, 360f), 320f * turn);
                    for (var i = 0; i < 2; i++)
                    {
                        var angle = Random.Range(0f, Mathf.PI * 2f);
                        var from = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 3.4f;
                        HovlVfx.Emit(glints, from, -from * 2.6f, Color.Lerp(color, Color.white, 0.5f), 0.32f,
                            radius * 1.2f);
                    }
                    break;
                default:
                    auraTimer = 1f;
                    break;
            }
        }

        private static Color WithAlpha(Color tint, float alpha)
        {
            tint.a = alpha;
            return tint;
        }

        private void EngineJet(Vector2 forward, int count)
        {
            var side = new Vector2(-forward.y, forward.x);
            for (var i = 0; i < count; i++)
            {
                var lane = Random.Range(-0.45f, 0.45f);
                Emit(sparks, -forward * radius + side * lane * radius,
                    -forward * Random.Range(2.5f, 5f), color, 0.45f,
                    new Vector2(0.1f, 0.5f));
            }
        }

        private void RepairCross()
        {
            // A readable medical cross that rises and dissolves into repair motes.
            for (var i = -4; i <= 4; i++)
            {
                var step = i * radius * 0.32f;
                Emit(sparks, new Vector2(step, radius * 2f), Vector2.up * 0.8f,
                    color, 0.8f, Vector2.one * 0.2f);
                if (i != 0) Emit(sparks, new Vector2(0f, step + radius * 2f), Vector2.up * 0.8f,
                    color, 0.8f, Vector2.one * 0.2f);
            }
        }

        public void Tick(float dt, float activeLeft, Vector2 forward)
        {
            if (!running || dt <= 0f) return;
            elapsed += dt;
            if (releaseDelay > 0f)
            {
                releaseDelay -= dt;
                if (releaseDelay <= 0f)
                {
                    Radial(36, radius, 5.5f, 0.45f, 0.12f);
                    Ring(radius * 8f, 0.45f, 0.85f);
                    Juice.Shake(0.1f, 0.18f);
                }
            }
            if (!sustained)
            {
                if (releaseDelay <= 0f && flash.particleCount == 0 && sparks.particleCount == 0 &&
                    rings.particleCount == 0 && glints.particleCount == 0) Stop();
                return;
            }
            if (activeLeft <= 0f)
            {
                sustained = false;
                Ring(radius * 3.5f, 0.25f, 0.3f);
                return;
            }
            auraTimer -= dt;
            if (auraTimer <= 0f) Aura(forward);
            pulseTimer -= dt;
            if (pulseTimer <= 0f)
            {
                pulseTimer = 0.55f;
                if (kind == ShipSpecialKind.Aegis) Ring(radius * 4.8f, 0.75f, 0.5f);
                else if (kind == ShipSpecialKind.Stasis) Ring(Arena.DetectionRadius * 1.8f, 0.85f, 0.12f);
                else if (kind == ShipSpecialKind.Amplify)
                    Emit(flash, Vector2.zero, Vector2.zero, new Color(color.r, color.g, color.b, 0.3f),
                        0.35f, Vector2.one * radius * 2.6f);
            }
            emissionTimer -= dt;
            if (emissionTimer > 0f) return;
            emissionTimer = 0.12f;
            switch (kind)
            {
                case ShipSpecialKind.Overdrive:
                    EngineJet(forward, 4);
                    break;
                case ShipSpecialKind.Aegis:
                case ShipSpecialKind.Stasis:
                    var shell = kind == ShipSpecialKind.Aegis ? radius * 2.1f : Arena.DetectionRadius * 0.9f;
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = elapsed * 1.8f + i * Mathf.PI * 0.25f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Emit(sparks, direction * shell, new Vector2(-direction.y, direction.x) * 0.75f,
                            color, 0.6f, Vector2.one * (kind == ShipSpecialKind.Aegis ? 0.13f : 0.1f));
                    }
                    break;
                case ShipSpecialKind.Amplify:
                    Radial(4, radius * 1.8f, -0.8f, 0.35f, 0.08f);
                    break;
            }
        }

        public void Stop()
        {
            kind = ShipSpecialKind.None;
            sustained = false;
            elapsed = emissionTimer = pulseTimer = releaseDelay = 0f;
            if (!running) return;
            running = false;
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            HovlVfx.Clear(aura);
            HovlVfx.Clear(haze);
            HovlVfx.Clear(glints);
            HovlVfx.Clear(dust);
            HovlVfx.Clear(heat);
        }
    }
}
