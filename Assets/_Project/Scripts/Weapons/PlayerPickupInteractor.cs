using System.Collections.Generic;
using AnimeFpsGame.Input;
using AnimeFpsGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Finds the ground gun the owner is standing next to and publishes it for the HUD.</summary>
    public class PlayerPickupInteractor : NetworkBehaviour
    {
        public const float DefaultPickupRadius = 3f;

        public readonly struct PickupCandidate
        {
            public PickupCandidate(Vector3 position, GunArchetype archetype, float spawnedAt,
                bool runtimeDrop)
            {
                Position = position;
                Archetype = archetype;
                SpawnedAt = spawnedAt;
                RuntimeDrop = runtimeDrop;
            }

            public Vector3 Position { get; }
            public GunArchetype Archetype { get; }
            public float SpawnedAt { get; }
            public bool RuntimeDrop { get; }
        }

        [SerializeField] PlayerWeapons weapons;
        [SerializeField] InputReader input;
        [SerializeField] WeaponPickup pickupPrefab;
        [SerializeField] float pickupRadius = DefaultPickupRadius;

        readonly List<PickupCandidate> candidates = new();
        readonly List<WeaponPickup> pickups = new();

        public WeaponPickup NearestPickup { get; private set; }
        public GunArchetype? NearestArchetype =>
            NearestPickup != null ? NearestPickup.Archetype : (GunArchetype?)null;

        public static int FindNearestIndex(Vector3 playerPosition, float radius,
            GunArchetype held, float now, IReadOnlyList<PickupCandidate> candidates)
        {
            var best = -1;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Archetype == held)
                {
                    continue;
                }

                if (candidate.RuntimeDrop &&
                    now - candidate.SpawnedAt < WeaponPickup.DropCooldownSeconds)
                {
                    continue;
                }

                var dx = candidate.Position.x - playerPosition.x;
                var dz = candidate.Position.z - playerPosition.z;
                var distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > radius || distance >= bestDistance)
                {
                    continue;
                }

                best = i;
                bestDistance = distance;
            }

            return best;
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            NearestPickup = null;
            if (weapons == null || weapons.IsDead)
            {
                return;
            }

            candidates.Clear();
            pickups.Clear();
            foreach (var pickup in WeaponPickup.All)
            {
                if (!pickup.IsSpawned || pickup.IsInDropCooldown)
                {
                    continue;
                }

                candidates.Add(new PickupCandidate(
                    pickup.transform.position, pickup.Archetype, pickup.SpawnedAt,
                    pickup.IsRuntimeDrop));
                pickups.Add(pickup);
            }

            var index = FindNearestIndex(transform.position, pickupRadius,
                weapons.CarriedArchetype((byte)weapons.CurrentSlot), Time.time, candidates);
            if (index >= 0)
            {
                NearestPickup = pickups[index];
            }
        }
    }
}
