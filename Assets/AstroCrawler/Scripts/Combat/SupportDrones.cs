// AstroCrawler - orbiting support drones that fire at nearby enemies.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>
    /// A temporary squad of orbiting drones. Each firing cycle sends every drone
    /// toward the closest enemy already in the ship's detection range.
    /// </summary>
    public class SupportDrones : MonoBehaviour
    {
        private const float OrbitRadius = 1.35f;

        private readonly List<Transform> drones = new List<Transform>();
        private CombatWorld world;
        private float damage;
        private float fireInterval;
        private float remaining;
        private float fireTimer;
        private float orbitAngle;

        public static void Spawn(int droneCount, float damage, float fireInterval,
            float duration, CombatWorld world)
        {
            if (world == null || droneCount <= 0 || duration <= 0f)
            {
                return;
            }

            var squad = AbilitySpawner.Create<SupportDrones>(null, "SupportDrones", Arena.ShipPosition);
            squad.world = world;
            squad.damage = Mathf.Max(0f, damage);
            squad.fireInterval = Mathf.Max(0.1f, fireInterval);
            squad.remaining = duration;
            squad.fireTimer = 0.2f;
            squad.orbitAngle = Random.Range(0f, 360f);

            for (var i = 0; i < droneCount; i++)
            {
                var drone = new GameObject("SupportDrone");
                drone.transform.SetParent(squad.transform, false);
                var renderer = drone.AddComponent<SpriteRenderer>();
                renderer.sprite = SpriteFactory.Polygon(3, 0f);
                renderer.color = new Color(0.56f, 0.78f, 1f);
                renderer.sortingOrder = 16;
                drone.transform.localScale = Vector3.one * 0.34f;
                squad.drones.Add(drone.transform);
            }

            Impact.Ring(Arena.ShipPosition, new Color(0.56f, 0.78f, 1f), 0.65f, 1.1f, 0.25f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            remaining -= dt;
            if (remaining <= 0f)
            {
                Impact.Burst(Arena.ShipPosition, new Color(0.56f, 0.78f, 1f), 0.65f);
                Destroy(gameObject);
                return;
            }

            orbitAngle += dt * 90f;
            for (var i = 0; i < drones.Count; i++)
            {
                var angleDegrees = orbitAngle + i * 360f / drones.Count;
                var angle = angleDegrees * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OrbitRadius;
                drones[i].position = Arena.ShipPosition + offset;
                drones[i].rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            }

            fireTimer -= dt;
            if (fireTimer > 0f)
            {
                return;
            }

            fireTimer = fireInterval;
            var target = world.NearestInRange(Arena.ShipPosition, true);
            if (target == null || !target.isAlive)
            {
                return;
            }

            var targetPosition = (Vector2)target.transform.position;
            for (var i = 0; i < drones.Count; i++)
            {
                var dronePosition = (Vector2)drones[i].position;
                var direction = (targetPosition - dronePosition).normalized;
                Projectile.Spawn(dronePosition, direction, damage, false, world, target);
                Impact.Spark(dronePosition, new Color(0.56f, 0.78f, 1f), 0.3f);
            }
        }
    }
}
