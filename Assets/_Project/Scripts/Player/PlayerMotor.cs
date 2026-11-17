using AnimeFpsGame.Combat;
using AnimeFpsGame.Input;
using AnimeFpsGame.Movement;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Owner/network glue between input, the shared <see cref="CharacterMovement"/> core and the
    /// replicated crouch state. The movement model itself never reads input or networking, so an AI
    /// brain can drive it the same way this component does.
    /// </summary>
    [RequireComponent(typeof(CharacterMovement))]
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField] CharacterMovement movement;
        [SerializeField] InputReader input;
        [SerializeField] PlayerLook playerLook;
        [SerializeField] Health health;
        [SerializeField] Animator animator;

        readonly NetworkVariable<bool> crouched = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Horizontal speed in m/s, used to drive the walk animation.</summary>
        public float Speed => movement != null ? movement.HorizontalSpeed : 0f;

        public bool IsCrouched => crouched.Value;

        void Awake()
        {
            movement ??= GetComponent<CharacterMovement>();
            input ??= GetComponent<InputReader>();
            playerLook ??= GetComponent<PlayerLook>();
            health ??= GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // The owner simulates; proxies follow NetworkTransform and only need the matching
                // crouch dimensions so server hit validation sees the right capsule.
                movement.SetPaused(true);
                movement.ApplyCrouchDimensions(crouched.Value);
                crouched.OnValueChanged += OnCrouchedChanged;
                return;
            }

            movement.CrouchChanged += OnCrouchChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                movement.CrouchChanged -= OnCrouchChanged;
            }
            else
            {
                crouched.OnValueChanged -= OnCrouchedChanged;
            }
        }

        void Update()
        {
            if (!IsOwner || movement == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                movement.SetIntent(MovementIntent.None);
                movement.SetPaused(true);
                SetWalking(false);
                return;
            }

            movement.SetPaused(false);

            var move = input != null ? input.Move : Vector2.zero;
            var yaw = playerLook != null ? playerLook.Yaw : transform.eulerAngles.y;
            var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);

            movement.SetIntent(new MovementIntent
            {
                WishDirection = Vector3.ClampMagnitude(direction, 1f),
                Sprint = input != null && input.SprintHeld,
                Crouch = input != null && input.CrouchHeld,
                Jump = input != null && input.JumpPressed,
            });

            SetWalking(movement.HorizontalSpeed > 0.1f);
        }

        void SetWalking(bool walking)
        {
            if (animator != null)
            {
                animator.SetInteger("Status_walk", walking ? 1 : 0);
            }
        }

        void OnCrouchChanged(bool value)
        {
            if (IsOwner && IsSpawned)
            {
                crouched.Value = value;
            }
        }

        void OnCrouchedChanged(bool previous, bool current)
        {
            movement.ApplyCrouchDimensions(current);
        }
    }
}
