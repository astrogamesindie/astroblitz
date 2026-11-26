using UnityEngine;

namespace AstroCrawler
{
    public partial class PlayerShip
    {
        public float SpecialCooldownLeft { get; private set; }
        public float SpecialActiveLeft { get; private set; }
        public ShipSpecialDefinition Special => definition?.special;
        private float specialCooldownReduction;
        private float specialDurationBonus;
        private ShipSpecialVfx specialVfx;

        /// <summary>True while the ship's special of that kind is running.</summary>
        public bool SpecialRunning(ShipSpecialKind kind)
        {
            return SpecialIs(kind);
        }

        /// <summary>Égide Prolongada: seconds added to every activation (not to Repair).</summary>
        public void SetSpecialDurationBonus(float seconds)
        {
            specialDurationBonus = Mathf.Max(0f, seconds);
        }

        /// <summary>Sobrecarga Furiosa: keeps a running special going a little longer.</summary>
        public void ExtendSpecial(float seconds)
        {
            if (SpecialActiveLeft > 0f && seconds > 0f) SpecialActiveLeft += seconds;
        }

        /// <summary>Paradoxo: takes seconds off the special's recharge.</summary>
        public void ReduceSpecialCooldown(float seconds)
        {
            if (seconds > 0f) SpecialCooldownLeft = Mathf.Max(0f, SpecialCooldownLeft - seconds);
        }

        /// <summary>One cannon shot's damage without a critical roll (Eco Temporal).</summary>
        public float CannonDamageEstimate
        {
            get
            {
                if (definition == null) return 0f;
                var mods = effects != null ? effects.Modifiers : null;
                return definition.damagePerShot * (mods != null ? mods.damageMultiplier : 1f) * SpecialDamage *
                    (1f + PassiveLevel(PassiveUpgrade.Damage) * 0.03f);
            }
        }

        public void SetSpecialCooldownReduction(float reduction)
        {
            var previous = SpecialCooldown;
            specialCooldownReduction = Mathf.Clamp(reduction, 0f, 0.5f);
            // Upgrading also shortens a running recharge, without restarting it.
            SpecialCooldownLeft = Mathf.Max(0f, SpecialCooldownLeft - (previous - SpecialCooldown));
        }

        public float SpecialCooldown => Special == null ? 0f
            : Mathf.Max(Special.duration, Mathf.Max(1f, Special.cooldown * (1f - specialCooldownReduction)));
        private bool SpecialIs(ShipSpecialKind kind) => SpecialActiveLeft > 0f && Special != null && Special.kind == kind;
        private float SpecialFireRate => SpecialIs(ShipSpecialKind.Overdrive) ? Mathf.Max(1f, Special.fireRateScale) : 1f;
        private float SpecialDamage => SpecialIs(ShipSpecialKind.Amplify) ? Mathf.Max(1f, Special.damageScale) : 1f;
        private bool SpecialProtected => SpecialIs(ShipSpecialKind.Aegis);

        private void ResetSpecial()
        {
            specialVfx?.Stop();
            specialCooldownReduction = 0f;
            specialDurationBonus = 0f;
            SpecialCooldownLeft = SpecialActiveLeft = 0f;
            world.specialStasis = false;
        }

        // RunController owns the clock: no cooldown advances in selections or defeat.
        public void TickSpecial(float dt)
        {
            if (!isAlive)
            {
                specialVfx?.Stop();
                return;
            }
            if (dt <= 0f) return;
            SpecialCooldownLeft = Mathf.Max(0f, SpecialCooldownLeft - dt);
            SpecialActiveLeft = Mathf.Max(0f, SpecialActiveLeft - dt);
            world.specialStasis = SpecialIs(ShipSpecialKind.Stasis);
            specialVfx?.Tick(dt, SpecialActiveLeft, hull != null ? (Vector2)hull.transform.up : Vector2.up);
        }

        internal bool ActivateSpecial()
        {
            if (!isAlive || Special == null || Special.kind == ShipSpecialKind.None ||
                SpecialCooldownLeft > 0f || SpecialActiveLeft > 0f) return false;
            SpecialCooldownLeft = SpecialCooldown;
            SpecialActiveLeft = Special.kind == ShipSpecialKind.Repair ? 0f
                : Mathf.Max(0.1f, Special.duration + specialDurationBonus);
            if (Special.kind == ShipSpecialKind.Repair)
                Heal(maxHp * Mathf.Clamp01(Special.healFraction));
            world.specialStasis = SpecialIs(ShipSpecialKind.Stasis);
            specialVfx?.Play(Special.kind, hullColor, radius,
                hull != null ? (Vector2)hull.transform.up : Vector2.up);
            if (effects != null) effects.OnSpecialActivated(Special.kind);
            OnSignatureSpecial(Special.kind);
            return true;
        }

        private void OnDisable()
        {
            specialVfx?.Stop();
        }
    }
}
