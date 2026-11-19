using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    [System.Serializable]
    public class WeaponPickupEntry
    {
        public GunArchetype archetype;
        public GameObject worldPrefab;
        public GunStatsAsset stats;
    }

    /// <summary>A networked gun lying on the ground. Server owns its identity and ammo.</summary>
    public class WeaponPickup : NetworkBehaviour
    {
        public const float DropCooldownSeconds = 0.75f;

        static readonly List<WeaponPickup> Registry = new();

        [SerializeField] GunArchetype initialArchetype = GunArchetype.Rifle;
        [SerializeField] WeaponPickupEntry[] entries;

        readonly NetworkVariable<byte> archetype = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> magazineAmmo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> reserveAmmo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> runtimeDrop = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        bool initializedBySpawner;
        GunArchetype pendingArchetype;
        int pendingMagazine;
        int pendingReserve;
        GameObject visual;

        public static IReadOnlyList<WeaponPickup> All
        {
            get
            {
                Registry.RemoveAll(p => p == null);
                return Registry;
            }
        }

        public GunArchetype Archetype => (GunArchetype)archetype.Value;
        public int MagazineAmmo => magazineAmmo.Value;
        public int ReserveAmmo => reserveAmmo.Value;

        /// <summary>True when the server spawned this from a swap. Networked so every peer agrees.</summary>
        public bool IsRuntimeDrop => runtimeDrop.Value;

        public float SpawnedAt { get; private set; }

        /// <summary>Only a freshly dropped gun is withheld; scene pickups are usable immediately.</summary>
        public bool IsInDropCooldown =>
            IsRuntimeDrop && Time.time - SpawnedAt < DropCooldownSeconds;

        /// <summary>Server-only, called before Spawn by a runtime drop.</summary>
        public void Initialize(GunArchetype value, int magazine, int reserve)
        {
            initializedBySpawner = true;
            pendingArchetype = value;
            pendingMagazine = magazine;
            pendingReserve = reserve;
        }

        public override void OnNetworkSpawn()
        {
            SpawnedAt = Time.time;

            if (IsServer)
            {
                if (initializedBySpawner)
                {
                    runtimeDrop.Value = true;
                    archetype.Value = (byte)pendingArchetype;
                    magazineAmmo.Value = pendingMagazine;
                    reserveAmmo.Value = pendingReserve;
                }
                else
                {
                    var entry = FindEntry(initialArchetype);
                    archetype.Value = (byte)initialArchetype;
                    if (entry?.stats?.stats != null)
                    {
                        magazineAmmo.Value = entry.stats.stats.magazineSize;
                        reserveAmmo.Value = entry.stats.stats.reserveAmmo;
                    }
                }
            }

            Registry.Add(this);
            BuildVisual();
        }

        public override void OnNetworkDespawn()
        {
            Registry.Remove(this);
            if (visual != null)
            {
                Destroy(visual);
                visual = null;
            }
        }

        void Update()
        {
            // Spin the visual only. The root transform is the networked position and has no
            // NetworkTransform, so rotating it would drift apart between server and clients.
            if (visual != null)
            {
                visual.transform.Rotate(0f, 24f * Time.deltaTime, 0f, Space.World);
            }
        }

        void BuildVisual()
        {
            if (visual != null)
            {
                Destroy(visual);
            }

            var entry = FindEntry(Archetype);
            if (entry?.worldPrefab == null)
            {
                return;
            }

            visual = Instantiate(entry.worldPrefab, transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            visual.transform.localScale = Vector3.one;

            // The pickup registry handles proximity; stray model colliders must not block movement.
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        WeaponPickupEntry FindEntry(GunArchetype value)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.archetype == value)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
