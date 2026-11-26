// AstroCrawler - the wingmen of the Squadron mode.
//
// Up to three allied ships parked in a triangle around the player ship
// (top, bottom-left, bottom-right). They do not take hits and
// do not move: each one is a second auto-cannon with its own ship's statline,
// at a share of its damage, that also follows the run's damage and fire-rate
// modifiers so the upgrades picked for the main ship pay off for the wing too.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    public class SquadronWing : MonoBehaviour
    {
        public const int SlotCount = 3;

        /// <summary>Distance of each slot from the main ship (world units): the triangle's radius.</summary>
        public const float SlotRadius = 1.5f;

        /// <summary>A wingman hits for this share of its ship's base damage per shot.</summary>
        public const float DamageShare = 0.55f;

        /// <summary>And fires a little slower than the same ship would as the main one.</summary>
        public const float FireIntervalScale = 1.25f;

        private const float HullDiameter = 0.55f * 2.4f * 0.68f;

        private sealed class Wingman
        {
            public ShipDefinition definition;
            public Transform root;
            public SpriteRenderer hull;
            public Color color;
            public float fireTimer;
            public float baseScale;
        }

        private readonly Wingman[] slots = new Wingman[SlotCount];
        private CombatWorld world;
        private EffectRunner effects;

        public static SquadronWing Create(CombatWorld world, EffectRunner effects)
        {
            var wing = AbilitySpawner.Create<SquadronWing>(null, "SquadronWing", Arena.ShipPosition);
            wing.world = world;
            wing.effects = effects;
            return wing;
        }

        /// <summary>0 top, 1 bottom-left, 2 bottom-right: an equilateral triangle around the ship.</summary>
        public static Vector2 SlotOffset(int slot)
        {
            var angle = (90f + slot * 120f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SlotRadius;
        }

        public bool IsOccupied(int slot) => slot >= 0 && slot < SlotCount && slots[slot] != null;

        public ShipDefinition Occupant(int slot) => IsOccupied(slot) ? slots[slot].definition : null;

        public int FreeSlots
        {
            get
            {
                var free = 0;
                for (var i = 0; i < SlotCount; i++) if (slots[i] == null) free++;
                return free;
            }
        }

        public bool Contains(string shipId)
        {
            for (var i = 0; i < SlotCount; i++)
                if (slots[i] != null && slots[i].definition.id == shipId) return true;
            return false;
        }

        public bool Place(ShipDefinition definition, int slot)
        {
            if (definition == null || slot < 0 || slot >= SlotCount || slots[slot] != null) return false;

            var go = new GameObject("Wingman_" + definition.id);
            go.transform.SetParent(transform, false);
            var position = Arena.ShipPosition + SlotOffset(slot);
            go.transform.position = position;

            var color = ArtLibrary.ShipTint(definition.id, SpriteFactory.ParseColor(definition.tint));
            var sprite = ArtLibrary.ShipFor(definition.id);
            var hull = SpriteFactory.NewRenderer("Hull", sprite, color, 19, go.transform);
            var scale = ArtLibrary.ScaleToDiameter(sprite, HullDiameter);
            hull.transform.localScale = Vector3.one * scale;

            var glow = SpriteFactory.NewRenderer("Aura", ArtLibrary.Glow, new Color(color.r, color.g, color.b, 0.18f), 4, go.transform);
            glow.transform.localScale = Vector3.one * ArtLibrary.ScaleToDiameter(ArtLibrary.Glow, HullDiameter * 2.2f);
            Juice.Breathe(glow, 0.1f, 0.22f, 1.9f);

            slots[slot] = new Wingman
            {
                definition = definition,
                root = go.transform,
                hull = hull,
                color = color,
                fireTimer = 0.3f + slot * 0.07f,
                baseScale = scale
            };

            Juice.PopIn(go.transform, 1f, 0.45f);
            Impact.Ring(position, color, 0.3f, 1.2f, 0.3f);
            return true;
        }

        private void Update()
        {
            if (world == null) return;
            var ship = PlayerShip.Active;
            if (ship == null || !ship.isAlive) return;

            var dt = Time.deltaTime;
            var mods = effects != null ? effects.Modifiers : null;
            for (var i = 0; i < SlotCount; i++)
            {
                var wingman = slots[i];
                if (wingman == null) continue;
                wingman.fireTimer -= dt;
                if (wingman.fireTimer > 0f) continue;
                Fire(wingman, mods);
            }
        }

        private void Fire(Wingman wingman, CombatModifiers mods)
        {
            var from = (Vector2)wingman.root.position;
            var target = world.NearestInRange(from, true);
            if (target == null || !target.isAlive)
            {
                wingman.fireTimer = 0.1f;
                return;
            }

            var definition = wingman.definition;
            var intervalScale = mods != null ? mods.fireIntervalScale : 1f;
            wingman.fireTimer = Mathf.Max(0.06f, definition.fireInterval * FireIntervalScale * intervalScale);

            var damage = definition.damagePerShot * DamageShare * (mods != null ? mods.damageMultiplier : 1f);
            if (mods != null && target.isBoss) damage *= 1f + mods.bossDamageBonus;
            if (mods != null && Random.value < Mathf.Min(1f, mods.critChance)) damage *= 2f;

            var direction = ((Vector2)target.transform.position - from).normalized;
            wingman.hull.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            var shots = Mathf.Max(1, definition.baseProjectiles);
            var side = new Vector2(-direction.y, direction.x);
            var muzzle = from + direction * 0.3f;
            for (var s = 0; s < shots; s++)
            {
                var lane = (s - (shots - 1) * 0.5f) * 0.16f;
                Projectile.Spawn(muzzle + side * lane, direction, damage, mods != null && mods.pierce, world, target,
                    0f, 0f, false, wingman.color);
            }
            Impact.Spark(muzzle, wingman.color, 0.28f);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null && slots[i].root != null) LeanTween.cancel(slots[i].root.gameObject);
                slots[i] = null;
            }
        }

        /// <summary>The wingmen already placed, in slot order (null for a free corner).</summary>
        public void CopyOccupants(List<ShipDefinition> into)
        {
            into.Clear();
            for (var i = 0; i < SlotCount; i++) into.Add(slots[i] != null ? slots[i].definition : null);
        }
    }
}
