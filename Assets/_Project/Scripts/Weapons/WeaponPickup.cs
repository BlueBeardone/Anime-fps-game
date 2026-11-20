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

        const float ThrowForwardSpeed = 4.5f;
        const float ThrowUpSpeed = 2.2f;
        const float SettleSpeed = 0.15f;
        const float SettleAngularSpeed = 0.6f;
        const float SettleSeconds = 0.35f;

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
        Rigidbody body;
        BoxCollider physicsCollider;
        float stillTime;
        bool launched;
        bool grounded;
        bool settled;

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

        /// <summary>Forward and upward throw shared by the server launch and the drop tests.</summary>
        public static Vector3 ThrowVelocity(Vector3 flatForward)
        {
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            return flatForward.normalized * ThrowForwardSpeed + Vector3.up * ThrowUpSpeed;
        }

        /// <summary>Server-only: turns a runtime drop into a physical object and throws it.</summary>
        public void Launch(Vector3 velocity)
        {
            if (!IsServer || !IsSpawned || settled)
            {
                return;
            }

            EnsurePhysics();
            launched = true;
            stillTime = 0f;
            grounded = false;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = velocity;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
            body.WakeUp();
        }

        void EnsurePhysics()
        {
            if (body == null)
            {
                body = gameObject.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = gameObject.AddComponent<Rigidbody>();
                }

                body.mass = 1f;
                body.linearDamping = 0.4f;
                body.angularDamping = 1.5f;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (physicsCollider == null)
            {
                physicsCollider = gameObject.GetComponent<BoxCollider>();
                if (physicsCollider == null)
                {
                    physicsCollider = gameObject.AddComponent<BoxCollider>();
                }

                var hasBounds = false;
                var min = Vector3.zero;
                var max = Vector3.zero;
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    // BoxCollider axes follow this root, so build the box from mesh-local corners
                    // instead of copying a world-space AABB (which is wrong once the gun is yawed).
                    var localBounds = renderer.localBounds;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var point = new Vector3(
                            (corner & 1) == 0 ? localBounds.min.x : localBounds.max.x,
                            (corner & 2) == 0 ? localBounds.min.y : localBounds.max.y,
                            (corner & 4) == 0 ? localBounds.min.z : localBounds.max.z);
                        var local = transform.InverseTransformPoint(
                            renderer.transform.TransformPoint(point));
                        if (!hasBounds)
                        {
                            min = local;
                            max = local;
                            hasBounds = true;
                        }
                        else
                        {
                            min = Vector3.Min(min, local);
                            max = Vector3.Max(max, local);
                        }
                    }
                }

                if (hasBounds)
                {
                    physicsCollider.center = (min + max) * 0.5f;
                    physicsCollider.size = max - min;
                }
                else
                {
                    physicsCollider.center = Vector3.zero;
                    physicsCollider.size = Vector3.one * 0.25f;
                }
            }

            physicsCollider.enabled = true;
        }

        void FixedUpdate()
        {
            // Only the server simulates a throw; clients follow the NetworkTransform.
            if (!IsServer || !launched || settled || body == null)
            {
                return;
            }

            var slow = body.linearVelocity.sqrMagnitude <= SettleSpeed * SettleSpeed &&
                       body.angularVelocity.sqrMagnitude <=
                       SettleAngularSpeed * SettleAngularSpeed;

            // Ground contact comes from the collision callback after the previous physics step.
            // Requiring it keeps a gun from freezing mid-air at the top of its arc or against a wall.
            if (slow && grounded)
            {
                stillTime += Time.fixedDeltaTime;
            }
            else
            {
                stillTime = 0f;
            }

            grounded = false;
            if (stillTime >= SettleSeconds)
            {
                Settle();
            }
        }

        void OnCollisionStay(Collision collision)
        {
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(collision.GetContact(i).normal.y) > 0.5f)
                {
                    grounded = true;
                    return;
                }
            }
        }

        void Settle()
        {
            settled = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            if (physicsCollider != null)
            {
                // A rest gun must never block or push a player.
                physicsCollider.enabled = false;
            }
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
