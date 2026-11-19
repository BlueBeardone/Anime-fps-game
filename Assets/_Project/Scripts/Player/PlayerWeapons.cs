using System.Collections.Generic;
using System.Linq;
using AnimeFpsGame.Combat;
using AnimeFpsGame.Effects;
using AnimeFpsGame.Input;
using AnimeFpsGame.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Owns one persistent gun instance per serialized slot, routes owner input to the active gun,
    /// drives the first-person viewmodel feel, and reports shots locally and to the server.
    /// </summary>
    public class PlayerWeapons : NetworkBehaviour
    {
        static readonly string[] WeaponMeshNames =
        {
            "K98", "Luger_P08", "STG44", "MP40", "MG42",
            "PanzerSchreck", "Panzerfaust_launcher", "Flammenwerfer"
        };

        // Meshes whose geometry contains the owner's eye point: hiding them keeps the camera out of
        // the soldier's own head. Wehrmacht_soilder_A also carries the hands, so the owner has no
        // hands yet.
        static readonly string[] OwnerHiddenMeshNames = { "Helmet", "Wehrmacht_soilder_A" };

        [SerializeField] WeaponDefinition[] slots;
        [SerializeField] Transform viewmodelAnchor;
        [SerializeField] Transform visualsRoot;
        [SerializeField] Animator animator;
        [SerializeField] Camera viewCamera;
        [SerializeField] Health health;
        [SerializeField] ShotEffects effects;
        [SerializeField] InputReader input;

        readonly NetworkVariable<byte> activeSlot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        readonly NetworkList<byte> carriedArchetypes = new();

        byte pendingSwapSlot;
        GunArchetype pendingSwapArchetype;
        int pendingSwapMagazine;
        int pendingSwapReserve;
        bool hasPendingSwap;

        IGun[] guns;
        IGun boundGun;
        PlayerLook playerLook;
        GameObject currentViewmodel;
        Vector3 viewmodelBasePosition;
        Vector3 viewmodelBaseEuler;
        Vector3 muzzleLocal;
        float viewmodelKickBack;
        float viewmodelKickUp;
        float lastScrollTime;
        float[] serverNextShotTime;
        float serverNextAnyShotTime;

        public Camera ViewCamera => viewCamera;
        public Transform VisualsRoot => visualsRoot;
        public Health Health => health;
        public int CurrentSlot => activeSlot.Value;
        public int SlotCount => slots?.Length ?? 0;
        public GameObject CurrentViewmodel => currentViewmodel;
        public bool IsDead => health != null && health.IsDead;

        public Vector3 MuzzleWorldPosition
        {
            get
            {
                var cameraTransform = viewCamera != null ? viewCamera.transform : null;
                var fallback = cameraTransform != null
                    ? cameraTransform.position + cameraTransform.forward * 0.5f
                    : transform.position + transform.forward * 0.5f;

                if (currentViewmodel == null)
                {
                    return fallback;
                }

                var candidate = currentViewmodel.transform.TransformPoint(muzzleLocal);
                // A replacement viewmodel or a tuned marker can put the muzzle at or behind the
                // camera plane; firing the flash there would white out the screen.
                return cameraTransform != null &&
                       Vector3.Dot(candidate - cameraTransform.position, cameraTransform.forward) <= 0.01f
                    ? fallback
                    : candidate;
            }
        }

        public IGun ActiveGun =>
            guns != null && CurrentSlot >= 0 && CurrentSlot < guns.Length ? guns[CurrentSlot] : null;

        void Awake()
        {
            playerLook = GetComponent<PlayerLook>();
            input ??= GetComponent<InputReader>();
            BuildGuns();
        }

        public override void OnNetworkSpawn()
        {
            activeSlot.OnValueChanged += OnSlotChanged;
            carriedArchetypes.OnListChanged += OnCarriedArchetypesChanged;
            if (IsServer && slots != null && carriedArchetypes.Count != slots.Length)
            {
                carriedArchetypes.Clear();
                for (var i = 0; i < slots.Length; i++)
                {
                    carriedArchetypes.Add((byte)slots[i].stats.stats.archetype);
                }
            }

            if (health != null)
            {
                health.DeathStateChanged += OnDeathStateChanged;
            }

            ApplySlot(activeSlot.Value);
            ApplyDeathVisuals(IsDead);
            Debug.Log($"[Player] spawned client={OwnerClientId} owner={IsOwner} slot={activeSlot.Value} pos={transform.position}");

            if (!IsOwner)
            {
                return;
            }

            BindActiveGun();
        }

        public override void OnNetworkDespawn()
        {
            activeSlot.OnValueChanged -= OnSlotChanged;
            carriedArchetypes.OnListChanged -= OnCarriedArchetypesChanged;

            if (health != null)
            {
                health.DeathStateChanged -= OnDeathStateChanged;
            }

            UnbindGun();
        }

        void Update()
        {
            if (!IsOwner || slots == null || slots.Length == 0)
            {
                return;
            }

            TickGuns();
            HandleSlotInput();

            if (health != null && health.IsDead)
            {
                UpdateViewmodelFeel();
                return;
            }

            var gun = ActiveGun;
            if (gun == null)
            {
                return;
            }

            if (input != null && input.ReloadPressed)
            {
                gun.TryStartReload();
            }

            if (input != null)
            {
                var pressed = input.AttackPressed;
                var held = input.AttackHeld;
                if (pressed || held)
                {
                    TryFireActive(gun, pressed, held);
                }
            }

            UpdateViewmodelFeel();
        }

        public void SetSlot(byte slot)
        {
            if (!IsOwner || slots == null || slots.Length == 0)
            {
                return;
            }

            activeSlot.Value = (byte)(slot % slots.Length);
        }

        public void ResetAmmo()
        {
            if (guns == null)
            {
                return;
            }

            foreach (var gun in guns)
            {
                gun?.ResetAmmo();
            }
        }

        public WeaponDefinition DefinitionFor(GunArchetype archetype)
        {
            if (slots == null)
            {
                return null;
            }

            foreach (var slot in slots)
            {
                if (slot?.stats?.stats != null && slot.stats.stats.archetype == archetype)
                {
                    return slot;
                }
            }

            return null;
        }

        public GunArchetype CarriedArchetype(byte slot)
        {
            if (slots == null || slots.Length == 0)
            {
                return GunArchetype.Rifle;
            }

            var index = Mathf.Clamp(slot, 0, slots.Length - 1);
            if (carriedArchetypes.Count > index)
            {
                return (GunArchetype)carriedArchetypes[index];
            }

            return slots[index].stats.stats.archetype;
        }

        public void SetPendingPickupAmmo(byte slot, GunArchetype archetype, int magazine, int reserve)
        {
            pendingSwapSlot = slot;
            pendingSwapArchetype = archetype;
            pendingSwapMagazine = magazine;
            pendingSwapReserve = reserve;
            hasPendingSwap = true;
        }

        public GunArchetype SwapSlotServer(byte slot, GunArchetype incoming, int magazine, int reserve)
        {
            if (!IsServer || slots == null || carriedArchetypes.Count <= slot)
            {
                return CarriedArchetype(slot);
            }

            var outgoing = CarriedArchetype(slot);
            if (outgoing == incoming)
            {
                return outgoing;
            }

            SetPendingPickupAmmo(slot, incoming, magazine, reserve);
            carriedArchetypes[slot] = (byte)incoming;
            return outgoing;
        }

        void BuildGuns()
        {
            if (slots == null)
            {
                guns = System.Array.Empty<IGun>();
                return;
            }

            guns = new IGun[slots.Length];
            serverNextShotTime = new float[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var stats = slots[i] != null && slots[i].stats != null ? slots[i].stats.stats : null;
                if (stats == null)
                {
                    Debug.LogError($"[PlayerWeapons] Slot {i} has no GunStatsAsset; run Anime FPS > Run All.");
                    continue;
                }

                guns[i] = GunFactory.Create(stats);
            }
        }

        void TickGuns()
        {
            if (guns == null)
            {
                return;
            }

            foreach (var gun in guns)
            {
                gun?.Tick(Time.deltaTime);
            }
        }

        void HandleSlotInput()
        {
            if (input == null)
            {
                return;
            }

            if (input.CycleNextPressed)
            {
                SetSlot((byte)((activeSlot.Value + 1) % slots.Length));
            }

            if (input.CyclePreviousPressed)
            {
                SetSlot((byte)((activeSlot.Value + slots.Length - 1) % slots.Length));
            }

            // Mouse wheel is not bound to Next/Previous in the shared input actions, so InputReader
            // exposes the raw scroll axis.
            var scroll = input.ScrollY;
            if (Mathf.Abs(scroll) > 0.01f && Time.unscaledTime - lastScrollTime > 0.2f)
            {
                lastScrollTime = Time.unscaledTime;
                SetSlot((byte)((activeSlot.Value + (scroll > 0f ? 1 : slots.Length - 1)) % slots.Length));
            }
        }

        void TryFireActive(IGun gun, bool pressed, bool held)
        {
            if (health != null && health.IsDead)
            {
                return;
            }

            var cameraTransform = viewCamera != null ? viewCamera.transform : transform;
            if (!gun.TryFire(cameraTransform.position, cameraTransform.forward, Time.time,
                    pressed, held, out var shot))
            {
                return;
            }

            shot.Slot = activeSlot.Value;

            var hits = new RaycastHit[shot.Directions.Length];
            for (var i = 0; i < shot.Directions.Length; i++)
            {
                TryRaycast(shot.Origin, shot.Directions[i], shot.Range, transform, out hits[i]);
            }

            if (effects != null)
            {
                effects.PlayLocal(MuzzleWorldPosition, shot.Origin, shot.Directions, hits);
            }

            var muzzle = MuzzleWorldPosition;

            if (IsSpawned)
            {
                FireServerRpc(shot.Slot, shot.Origin, muzzle, shot.Directions);
            }

            viewmodelKickBack += 0.02f;
            viewmodelKickUp += UnityEngine.Random.Range(1.5f, 2.5f);
        }

        /// <summary>Nearest non-shooter hit for one ray; shared by local feedback and server validation.</summary>
        public static bool TryRaycast(Vector3 origin, Vector3 direction, float range, Transform shooter,
            out RaycastHit hit)
        {
            hit = default;
            var hits = Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore);
            var bestDistance = float.MaxValue;
            var found = false;

            foreach (var candidate in hits)
            {
                if (shooter != null && candidate.collider.transform.IsChildOf(shooter))
                {
                    continue;
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// The server re-raycasts what the client reported. It never trusts client damage: pellets,
        /// range, origin, aim cone and cadence are all checked here before any health changes.
        /// </summary>
        [ServerRpc]
        void FireServerRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions,
            ServerRpcParams rpcParams = default)
        {
            if (health != null && health.IsDead)
            {
                return;
            }

            if (directions == null || directions.Length == 0)
            {
                return;
            }

            if (guns == null || slot >= guns.Length || guns[slot] == null)
            {
                return;
            }

            if (serverNextShotTime == null || slot >= serverNextShotTime.Length)
            {
                return;
            }

            var stats = guns[slot].Stats;
            if (directions.Length > Mathf.Max(1, stats.pellets))
            {
                return;
            }

            if (Vector3.Distance(origin, transform.position) > 3f)
            {
                return;
            }

            if (Vector3.Distance(muzzleOrigin, transform.position) > 3f)
            {
                return;
            }

            var now = Time.time;
            // One global floor stops a client from alternating slots to beat the per-slot cadence.
            if (now < serverNextAnyShotTime)
            {
                return;
            }

            if (now < serverNextShotTime[slot])
            {
                return;
            }

            var flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();

            var validated = new List<Vector3>(directions.Length);
            foreach (var raw in directions)
            {
                if (raw.sqrMagnitude < 0.0001f)
                {
                    return;
                }

                var direction = raw.normalized;
                var flat = direction;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f || Vector3.Angle(flatForward, flat) > 60f)
                {
                    return;
                }

                validated.Add(direction);
            }

            // A small tolerance keeps client/server frame jitter from rejecting legitimate shots.
            serverNextShotTime[slot] = now + Gun.ShotIntervalSeconds(stats) * 0.8f;
            // 20 shots/s ceiling: above the fastest configured gun (SMG, 15/s), so it only
            // catches clients alternating slots to exceed any single gun's cadence.
            serverNextAnyShotTime = now + 0.05f;

            var points = new List<Vector3>(validated.Count);
            var normals = new List<Vector3>(validated.Count);

            foreach (var direction in validated)
            {
                if (!TryRaycast(origin, direction, stats.range, transform, out var hit))
                {
                    points.Add(origin + direction * stats.range);
                    normals.Add(-direction);
                    continue;
                }

                points.Add(hit.point);
                normals.Add(hit.normal);

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.ApplyDamage(stats.damage, hit.point, hit.normal, OwnerClientId);
            }

            var targets = NetworkManager.Singleton.ConnectedClientsIds
                .Where(id => id != OwnerClientId)
                .ToArray();
            var targetsParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = targets }
            };

            FireEffectClientRpc(slot, origin, muzzleOrigin, validated.ToArray(), points.ToArray(),
                normals.ToArray(), targetsParams);
        }

        [ClientRpc]
        void FireEffectClientRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions,
            Vector3[] points, Vector3[] normals, ClientRpcParams rpcParams = default)
        {
            if (effects != null)
            {
                effects.PlayRemote(muzzleOrigin, origin, directions, points);
            }
        }

        void OnDeathStateChanged(bool dead)
        {
            ApplyDeathVisuals(dead);
        }

        void ApplyDeathVisuals(bool dead)
        {
            if (dead)
            {
                if (guns != null)
                {
                    foreach (var gun in guns)
                    {
                        gun?.CancelReload();
                    }
                }

                if (currentViewmodel != null)
                {
                    currentViewmodel.SetActive(false);
                }

                // Remote corpses disappear; the owner loses the viewmodel, the HUD and their own
                // body, so a death while crouched cannot freeze the camera inside the chest.
                if (visualsRoot != null)
                {
                    if (IsOwner)
                    {
                        foreach (var renderer in visualsRoot.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.enabled = false;
                        }
                    }
                    else
                    {
                        visualsRoot.gameObject.SetActive(false);
                    }
                }

                return;
            }

            if (visualsRoot != null)
            {
                visualsRoot.gameObject.SetActive(true);
            }

            if (slots != null && slots.Length > 0)
            {
                var definition = DefinitionFor(
                    CarriedArchetype((byte)Mathf.Clamp(CurrentSlot, 0, slots.Length - 1)));
                if (definition != null)
                {
                    ApplyVisibleWeaponMeshes(definition);
                }
            }

            if (IsOwner && currentViewmodel != null)
            {
                currentViewmodel.SetActive(true);
            }
        }

        void UpdateViewmodelFeel()
        {
            viewmodelKickBack = Mathf.Lerp(viewmodelKickBack, 0f, 12f * Time.deltaTime);
            viewmodelKickUp = Mathf.Lerp(viewmodelKickUp, 0f, 12f * Time.deltaTime);

            if (currentViewmodel == null)
            {
                return;
            }

            var gun = ActiveGun;
            var reload01 = gun != null && gun.IsReloading
                ? Mathf.Sin(gun.ReloadProgress01 * Mathf.PI)
                : 0f;
            var bolt01 = gun != null && gun.BoltCycleProgress01 > 0f
                ? Mathf.Sin(gun.BoltCycleProgress01 * Mathf.PI)
                : 0f;

            currentViewmodel.transform.localPosition = viewmodelBasePosition + new Vector3(
                0f,
                -0.06f * reload01 - 0.02f * bolt01,
                -viewmodelKickBack - 0.03f * reload01);

            currentViewmodel.transform.localRotation = Quaternion.Euler(
                viewmodelBaseEuler + new Vector3(-viewmodelKickUp + 25f * reload01 - 8f * bolt01, 0f, 0f));
        }

        void OnCarriedArchetypesChanged(NetworkListEvent<byte> change)
        {
            // Initial sync arrives as Add events. Guns were already built from the serialized catalog;
            // only a real slot change should rebuild one.
            if (change.Type != NetworkListEvent<byte>.EventType.Value)
            {
                return;
            }

            var index = change.Index;
            if (guns == null || index < 0 || index >= guns.Length)
            {
                return;
            }

            RebuildSlotGun(index);
            if (index == CurrentSlot)
            {
                ApplySlot((byte)index);
            }
        }

        void RebuildSlotGun(int index)
        {
            var archetype = (GunArchetype)carriedArchetypes[index];
            var definition = DefinitionFor(archetype);
            var stats = definition?.stats?.stats;
            if (stats == null)
            {
                return;
            }

            var gun = GunFactory.Create(stats);
            if (hasPendingSwap && pendingSwapSlot == index && pendingSwapArchetype == archetype)
            {
                gun.RestoreAmmo(pendingSwapMagazine, pendingSwapReserve);
                hasPendingSwap = false;
            }

            guns[index] = gun;
            if (IsOwner && index == CurrentSlot)
            {
                BindActiveGun();
            }
        }

        void OnSlotChanged(byte previous, byte current)
        {
            if (guns != null && previous < guns.Length)
            {
                guns[previous]?.CancelReload();
            }

            ApplySlot(current);
        }

        void ApplySlot(byte slot)
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            var definition = DefinitionFor(CarriedArchetype(slot));
            if (definition == null)
            {
                definition = slots[Mathf.Clamp(slot, 0, slots.Length - 1)];
            }

            if (currentViewmodel != null)
            {
                Destroy(currentViewmodel);
            }

            if (IsOwner && viewmodelAnchor != null && definition.viewmodelPrefab != null)
            {
                currentViewmodel = Instantiate(definition.viewmodelPrefab, viewmodelAnchor);
                viewmodelBasePosition = definition.viewmodelLocalPosition;
                viewmodelBaseEuler = definition.viewmodelLocalEuler;
                currentViewmodel.transform.localPosition = viewmodelBasePosition;
                currentViewmodel.transform.localRotation = Quaternion.Euler(viewmodelBaseEuler);
                currentViewmodel.transform.localScale = Vector3.one * definition.viewmodelScale;
                muzzleLocal = ViewmodelMuzzle.ComputeLocal(currentViewmodel);
                // Switching slots while dead must not bring the gun back on screen.
                currentViewmodel.SetActive(!IsDead);
            }

            if (animator != null && !string.IsNullOrEmpty(definition.animatorInt))
            {
                foreach (var other in slots)
                {
                    if (other != null && !string.IsNullOrEmpty(other.animatorInt))
                    {
                        animator.SetInteger(other.animatorInt, 0);
                    }
                }

                animator.SetInteger(definition.animatorInt, 1);
            }

            ApplyVisibleWeaponMeshes(definition);

            if (IsOwner)
            {
                BindActiveGun();
            }
        }

        /// <summary>Owner sees their body without head or hands; other clients see the soldier and one gun.</summary>
        void ApplyVisibleWeaponMeshes(WeaponDefinition definition)
        {
            if (visualsRoot == null)
            {
                return;
            }

            // Keep the Visuals root active: it carries the Animator that NetworkAnimator replicates,
            // and a disabled Animator silently drops every SetInteger call.
            var soldierMeshName = definition != null ? definition.soldierMeshName : null;
            foreach (var renderer in visualsRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = ShouldRenderSoldierMesh(IsOwner, renderer.gameObject.name, soldierMeshName);
            }
        }

        /// <summary>Owner: body only, no head/helmet and no held weapon mesh. Everyone else: the whole
        /// soldier plus exactly the weapon for the active slot.</summary>
        public static bool ShouldRenderSoldierMesh(bool isOwner, string meshName, string soldierMeshName)
        {
            var isWeapon = System.Array.IndexOf(WeaponMeshNames, meshName) >= 0;

            if (isOwner)
            {
                return !isWeapon && System.Array.IndexOf(OwnerHiddenMeshNames, meshName) < 0;
            }

            return !isWeapon || meshName == soldierMeshName;
        }

        void BindActiveGun()
        {
            var gun = ActiveGun;
            if (boundGun == gun)
            {
                return;
            }

            UnbindGun();
            boundGun = gun;
            if (boundGun != null)
            {
                boundGun.RecoilKicked += OnRecoilKicked;
            }
        }

        void UnbindGun()
        {
            if (boundGun != null)
            {
                boundGun.RecoilKicked -= OnRecoilKicked;
                boundGun = null;
            }
        }

        void OnRecoilKicked(Vector2 degrees)
        {
            if (IsOwner && playerLook != null)
            {
                playerLook.AddRecoil(degrees);
            }
        }
    }
}
