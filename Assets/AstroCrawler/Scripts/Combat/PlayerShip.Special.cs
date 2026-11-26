using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public partial class PlayerShip
    {
        /// <summary>
        /// A special borrowed from a Squadron wingman. It runs on this ship
        /// (the wingmen only shoot), with its own recharge, next to the ship's
        /// own special. Up to SquadronWing.SlotCount of them.
        /// </summary>
        public sealed class WingSpecial
        {
            public ShipDefinition owner;
            public ShipSpecialDefinition special;
            public float cooldownLeft;
            public float activeLeft;
            internal ShipSpecialVfx vfx;
        }

        private readonly List<WingSpecial> wingSpecials = new List<WingSpecial>();

        public int WingSpecialCount => wingSpecials.Count;

        public WingSpecial GetWingSpecial(int index) =>
            index >= 0 && index < wingSpecials.Count ? wingSpecials[index] : null;

        /// <summary>Adds the special of a wingman just placed; ignored when that ship has none.</summary>
        public void AddWingSpecial(ShipDefinition owner)
        {
            if (owner == null || owner.special == null || owner.special.kind == ShipSpecialKind.None) return;
            if (wingSpecials.Count >= SquadronWing.SlotCount) return;
            // Starts charged: the pick is the reward, the button is usable at once.
            wingSpecials.Add(new WingSpecial { owner = owner, special = owner.special, vfx = new ShipSpecialVfx(transform) });
        }

        public float WingSpecialCooldown(WingSpecial slot)
        {
            if (slot == null || slot.special == null) return 0f;
            return Mathf.Max(slot.special.duration, Mathf.Max(1f, slot.special.cooldown * (1f - specialCooldownReduction)));
        }

        public bool CanActivateWingSpecial(int index)
        {
            var slot = GetWingSpecial(index);
            return isAlive && slot != null && slot.cooldownLeft <= 0f && slot.activeLeft <= 0f;
        }

        internal bool ActivateWingSpecial(int index)
        {
            if (!CanActivateWingSpecial(index)) return false;
            var slot = wingSpecials[index];
            var special = slot.special;
            slot.cooldownLeft = WingSpecialCooldown(slot);
            slot.activeLeft = special.kind == ShipSpecialKind.Repair ? 0f
                : Mathf.Max(0.1f, special.duration + specialDurationBonus);
            if (special.kind == ShipSpecialKind.Repair)
                Heal(maxHp * Mathf.Clamp01(special.healFraction));
            world.specialStasis = SpecialIs(ShipSpecialKind.Stasis);
            slot.vfx?.Play(special.kind, SpriteFactory.ParseColor(slot.owner.tint), radius,
                hull != null ? (Vector2)hull.transform.up : Vector2.up);
            if (effects != null) effects.OnSpecialActivated(special.kind);
            return true;
        }

        private void ClearWingSpecials()
        {
            for (var i = 0; i < wingSpecials.Count; i++) wingSpecials[i].vfx?.Stop();
            wingSpecials.Clear();
        }

        /// <summary>The strongest running special of that kind (own or borrowed), or null.</summary>
        private ShipSpecialDefinition ActiveSpecial(ShipSpecialKind kind)
        {
            ShipSpecialDefinition best = null;
            if (SpecialActiveLeft > 0f && Special != null && Special.kind == kind) best = Special;
            for (var i = 0; i < wingSpecials.Count; i++)
            {
                var slot = wingSpecials[i];
                if (slot.activeLeft <= 0f || slot.special.kind != kind) continue;
                if (best == null || slot.special.damageScale + slot.special.fireRateScale >
                    best.damageScale + best.fireRateScale) best = slot.special;
            }
            return best;
        }
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
        public void SetSpecialDurationBonus(float seconds) // also lengthens the wingmen's specials
        {
            specialDurationBonus = Mathf.Max(0f, seconds);
        }

        /// <summary>Sobrecarga Furiosa: keeps a running special going a little longer.</summary>
        public void ExtendSpecial(float seconds)
        {
            if (seconds <= 0f) return;
            if (SpecialActiveLeft > 0f) SpecialActiveLeft += seconds;
            for (var i = 0; i < wingSpecials.Count; i++)
                if (wingSpecials[i].activeLeft > 0f) wingSpecials[i].activeLeft += seconds;
        }

        /// <summary>Paradoxo: takes seconds off the special's recharge.</summary>
        public void ReduceSpecialCooldown(float seconds)
        {
            if (seconds <= 0f) return;
            SpecialCooldownLeft = Mathf.Max(0f, SpecialCooldownLeft - seconds);
            for (var i = 0; i < wingSpecials.Count; i++)
                wingSpecials[i].cooldownLeft = Mathf.Max(0f, wingSpecials[i].cooldownLeft - seconds);
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
        private bool SpecialIs(ShipSpecialKind kind) => ActiveSpecial(kind) != null;
        private float SpecialFireRate
        {
            get
            {
                var overdrive = ActiveSpecial(ShipSpecialKind.Overdrive);
                return overdrive != null ? Mathf.Max(1f, overdrive.fireRateScale) : 1f;
            }
        }
        private float SpecialDamage
        {
            get
            {
                var amplify = ActiveSpecial(ShipSpecialKind.Amplify);
                return amplify != null ? Mathf.Max(1f, amplify.damageScale) : 1f;
            }
        }
        private bool SpecialProtected => SpecialIs(ShipSpecialKind.Aegis);

        private void ResetSpecial()
        {
            specialVfx?.Stop();
            specialCooldownReduction = 0f;
            specialDurationBonus = 0f;
            SpecialCooldownLeft = SpecialActiveLeft = 0f;
            ClearWingSpecials();
            world.specialStasis = false;
        }

        // RunController owns the clock: no cooldown advances in selections or defeat.
        public void TickSpecial(float dt)
        {
            if (!isAlive)
            {
                specialVfx?.Stop();
                for (var i = 0; i < wingSpecials.Count; i++) wingSpecials[i].vfx?.Stop();
                return;
            }
            if (dt <= 0f) return;
            SpecialCooldownLeft = Mathf.Max(0f, SpecialCooldownLeft - dt);
            SpecialActiveLeft = Mathf.Max(0f, SpecialActiveLeft - dt);
            var forward = hull != null ? (Vector2)hull.transform.up : Vector2.up;
            for (var i = 0; i < wingSpecials.Count; i++)
            {
                var slot = wingSpecials[i];
                slot.cooldownLeft = Mathf.Max(0f, slot.cooldownLeft - dt);
                slot.activeLeft = Mathf.Max(0f, slot.activeLeft - dt);
                slot.vfx?.Tick(dt, slot.activeLeft, forward);
            }
            world.specialStasis = SpecialIs(ShipSpecialKind.Stasis);
            specialVfx?.Tick(dt, SpecialActiveLeft, forward);
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
            for (var i = 0; i < wingSpecials.Count; i++) wingSpecials[i].vfx?.Stop();
        }
    }
}
