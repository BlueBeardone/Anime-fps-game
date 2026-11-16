using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Main weapon + pistol. The active slot is a NetworkVariable so every client shows the same gun.
    /// Firing is deliberately not implemented: it logs "shoot" locally and on the server.
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

        readonly NetworkVariable<byte> activeSlot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        InputAction attackAction;
        InputAction nextAction;
        InputAction previousAction;
        GameObject currentViewmodel;
        float lastScrollTime;

        public Camera ViewCamera => viewCamera;
        public int CurrentSlot => activeSlot.Value;
        public GameObject CurrentViewmodel => currentViewmodel;

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
                nextAction = input.actions["Next"];
                previousAction = input.actions["Previous"];
            }
        }

        public override void OnNetworkDespawn()
        {
            activeSlot.OnValueChanged -= OnSlotChanged;
        }

        void Update()
        {
            if (!IsOwner || slots == null || slots.Length == 0)
            {
                return;
            }

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

            if (attackAction != null && attackAction.WasPressedThisFrame())
            {
                Debug.Log("shoot");
                ShootServerRpc();
            }
        }

        public void SetSlot(byte slot)
        {
            if (!IsOwner || slots == null || slots.Length == 0)
            {
                return;
            }

            activeSlot.Value = (byte)(slot % slots.Length);
        }

        void OnSlotChanged(byte previous, byte current)
        {
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
                currentViewmodel.transform.localPosition = definition.viewmodelLocalPosition;
                currentViewmodel.transform.localRotation = Quaternion.Euler(definition.viewmodelLocalEuler);
                currentViewmodel.transform.localScale = Vector3.one * definition.viewmodelScale;
            }

            if (animator != null && !string.IsNullOrEmpty(definition.animatorInt))
            {
                foreach (var other in slots)
                {
                    if (!string.IsNullOrEmpty(other.animatorInt))
                    {
                        animator.SetInteger(other.animatorInt, 0);
                    }
                }

                animator.SetInteger(definition.animatorInt, 1);
            }

            ApplyVisibleWeaponMeshes(definition);
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

        [ServerRpc]
        void ShootServerRpc()
        {
            Debug.Log($"shoot from client {OwnerClientId}");
        }
    }
}
