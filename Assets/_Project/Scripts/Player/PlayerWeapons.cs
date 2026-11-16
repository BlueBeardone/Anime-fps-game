using AnimeFpsGame.Combat;
using AnimeFpsGame.Effects;
using AnimeFpsGame.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [SerializeField] WeaponDefinition[] slots;
        [SerializeField] Transform viewmodelAnchor;
        [SerializeField] Transform visualsRoot;
        [SerializeField] Animator animator;
        [SerializeField] Camera viewCamera;
        [SerializeField] Health health;
        [SerializeField] ShotEffects effects;

        readonly NetworkVariable<byte> activeSlot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        IGun[] guns;
        IGun boundGun;
        PlayerLook playerLook;
        InputAction attackAction;
        InputAction reloadAction;
        InputAction nextAction;
        InputAction previousAction;
        GameObject currentViewmodel;
        Vector3 viewmodelBasePosition;
        Vector3 viewmodelBaseEuler;
        float viewmodelKickBack;
        float viewmodelKickUp;
        float lastScrollTime;

        public Camera ViewCamera => viewCamera;
        public Health Health => health;
        public int CurrentSlot => activeSlot.Value;
        public int SlotCount => slots?.Length ?? 0;
        public GameObject CurrentViewmodel => currentViewmodel;

        public IGun ActiveGun =>
            guns != null && CurrentSlot >= 0 && CurrentSlot < guns.Length ? guns[CurrentSlot] : null;

        void Awake()
        {
            playerLook = GetComponent<PlayerLook>();
            BuildGuns();
        }

        public override void OnNetworkSpawn()
        {
            activeSlot.OnValueChanged += OnSlotChanged;
            ApplySlot(activeSlot.Value);
            Debug.Log($"[Player] spawned client={OwnerClientId} owner={IsOwner} slot={activeSlot.Value} pos={transform.position}");

            if (!IsOwner)
            {
                return;
            }

            var input = GetComponent<PlayerInput>();
            if (input != null && input.actions != null)
            {
                attackAction = input.actions["Attack"];
                reloadAction = input.actions["Reload"];
                nextAction = input.actions["Next"];
                previousAction = input.actions["Previous"];
            }

            BindActiveGun();
        }

        public override void OnNetworkDespawn()
        {
            activeSlot.OnValueChanged -= OnSlotChanged;
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

            if (reloadAction != null && reloadAction.WasPressedThisFrame())
            {
                gun.TryStartReload();
            }

            if (attackAction != null)
            {
                var pressed = attackAction.WasPressedThisFrame();
                var held = attackAction.IsPressed();
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

        void BuildGuns()
        {
            if (slots == null)
            {
                guns = System.Array.Empty<IGun>();
                return;
            }

            guns = new IGun[slots.Length];
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
            if (nextAction != null && nextAction.WasPressedThisFrame())
            {
                SetSlot((byte)((activeSlot.Value + 1) % slots.Length));
            }

            if (previousAction != null && previousAction.WasPressedThisFrame())
            {
                SetSlot((byte)((activeSlot.Value + slots.Length - 1) % slots.Length));
            }

            // Mouse wheel is not bound to Next/Previous in the shared input actions, so read it here.
            var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f && Time.unscaledTime - lastScrollTime > 0.2f)
            {
                lastScrollTime = Time.unscaledTime;
                SetSlot((byte)((activeSlot.Value + (scroll > 0f ? 1 : slots.Length - 1)) % slots.Length));
            }
        }

        void TryFireActive(IGun gun, bool pressed, bool held)
        {
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
                effects.PlayLocal(shot.Origin, shot.Directions, hits);
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

            currentViewmodel.transform.localPosition = viewmodelBasePosition + new Vector3(
                0f,
                -0.06f * reload01,
                -viewmodelKickBack - 0.03f * reload01);

            currentViewmodel.transform.localRotation = Quaternion.Euler(
                viewmodelBaseEuler + new Vector3(-viewmodelKickUp + 25f * reload01, 0f, 0f));
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

            var definition = slots[Mathf.Clamp(slot, 0, slots.Length - 1)];

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

        /// <summary>Own body is hidden from the owner's camera; other clients see the soldier and one gun.</summary>
        void ApplyVisibleWeaponMeshes(WeaponDefinition definition)
        {
            if (visualsRoot == null)
            {
                return;
            }

            // Keep the Visuals root active: it carries the Animator that NetworkAnimator replicates,
            // and a disabled Animator silently drops every SetInteger call.
            foreach (var renderer in visualsRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (IsOwner)
                {
                    renderer.enabled = false;
                    continue;
                }

                if (System.Array.IndexOf(WeaponMeshNames, renderer.gameObject.name) < 0)
                {
                    continue;
                }

                renderer.enabled = renderer.gameObject.name == definition.soldierMeshName;
            }
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
