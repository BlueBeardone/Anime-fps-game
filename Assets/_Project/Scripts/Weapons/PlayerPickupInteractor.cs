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

            if (input != null && input.PickupPressed && NearestPickup != null)
            {
                TryPickup(NearestPickup);
            }
        }

        /// <summary>Owner entry point; the host applies at once, a client asks the server.</summary>
        public bool TryPickup(WeaponPickup target)
        {
            if (!IsOwner || target == null || weapons == null || weapons.IsDead || weapons.ActiveGun == null)
            {
                return false;
            }

            var slot = (byte)weapons.CurrentSlot;
            weapons.SetPendingPickupAmmo(slot, target.Archetype, target.MagazineAmmo, target.ReserveAmmo);
            weapons.SetPendingPickupHover(target.transform.position);

            var outgoingMagazine = weapons.ActiveGun.AmmoInMagazine;
            var outgoingReserve = weapons.ActiveGun.ReserveAmmo;

            if (IsServer)
            {
                return ApplyPickupServer(target, outgoingMagazine, outgoingReserve, slot);
            }

            TryPickupServerRpc(new NetworkObjectReference(target.NetworkObject), slot,
                outgoingMagazine, outgoingReserve);
            return true;
        }

        [ServerRpc]
        void TryPickupServerRpc(NetworkObjectReference target, byte slot, int outgoingMagazine,
            int outgoingReserve)
        {
            if (target.TryGet(out var networkObject))
            {
                ApplyPickupServer(networkObject.GetComponent<WeaponPickup>(),
                    outgoingMagazine, outgoingReserve, slot);
            }
        }

        /// <summary>
        /// Re-checks everything the client reported before swapping: cooldown, range, the held
        /// archetype and the outgoing gun's ammo caps. The client never decides what it gets.
        /// </summary>
        public bool ApplyPickupServer(WeaponPickup target, int outgoingMagazine, int outgoingReserve,
            byte? requestSlot = null)
        {
            if (!IsServer || target == null || !target.IsSpawned || weapons == null || weapons.IsDead)
            {
                return false;
            }

            if (target.IsInDropCooldown)
            {
                return false;
            }

            if (Vector3.Distance(transform.position, target.transform.position) > pickupRadius + 0.5f)
            {
                return false;
            }

            var slot = (byte)weapons.CurrentSlot;
            if (requestSlot.HasValue && requestSlot.Value != slot)
            {
                return false;
            }

            var outgoing = weapons.CarriedArchetype(slot);
            if (outgoing == target.Archetype)
            {
                return false;
            }

            var incomingMagazine = target.MagazineAmmo;
            var incomingReserve = target.ReserveAmmo;
            var outgoingDefinition = weapons.DefinitionFor(outgoing);
            var outgoingStats = outgoingDefinition?.stats?.stats;
            if (outgoingStats == null)
            {
                return false;
            }

            outgoingMagazine = Mathf.Clamp(outgoingMagazine, 0, outgoingStats.magazineSize);
            outgoingReserve = Mathf.Clamp(outgoingReserve, 0, outgoingStats.reserveAmmo);

            weapons.SwapSlotServer(slot, target.Archetype, incomingMagazine, incomingReserve);
            // Runtime drops are ours to destroy; a scene pickup stays alive but despawned, which is
            // what NGO recommends for in-scene network objects.
            target.NetworkObject.Despawn(target.IsRuntimeDrop);
            SpawnDrop(outgoing, outgoingMagazine, outgoingReserve);
            return true;
        }

        void SpawnDrop(GunArchetype archetype, int magazine, int reserve)
        {
            if (pickupPrefab == null)
            {
                return;
            }

            // Keep repeated swaps from filling the range forever.
            var drops = new List<WeaponPickup>();
            foreach (var pickup in WeaponPickup.All)
            {
                if (pickup.IsRuntimeDrop && pickup.IsSpawned)
                {
                    drops.Add(pickup);
                }
            }

            if (drops.Count >= 12)
            {
                drops.Sort((a, b) => a.SpawnedAt.CompareTo(b.SpawnedAt));
                drops[0].NetworkObject.Despawn(true);
            }

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            // Throw from chest height so the gun clears the ground and reads as leaving the hands.
            var spawn = transform.position + Vector3.up * 1.2f + forward.normalized * 0.5f;
            if (Physics.Raycast(spawn + Vector3.up * 0.1f, Vector3.down, out var ground, 3f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                spawn.y = Mathf.Max(spawn.y, ground.point.y + 0.2f);
            }

            var drop = Instantiate(pickupPrefab, spawn,
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            drop.Initialize(archetype, magazine, reserve);
            drop.NetworkObject.Spawn();
            drop.Launch(WeaponPickup.ThrowVelocity(forward));
        }
    }
}
