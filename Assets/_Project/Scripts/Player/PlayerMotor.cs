using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Owner-authority first-person movement. Remote copies are moved by NetworkTransform instead.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float sprintSpeed = 8f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -20f;
        [SerializeField] Animator animator;

        CharacterController controller;
        AnimeFpsGame.Combat.Health health;
        InputAction moveAction;
        InputAction sprintAction;
        InputAction jumpAction;
        float verticalVelocity;

        /// <summary>Horizontal speed in m/s, used to drive the walk animation.</summary>
        public float Speed { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<AnimeFpsGame.Combat.Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            var input = GetComponent<PlayerInput>();
            if (input != null && input.actions != null)
            {
                moveAction = input.actions["Move"];
                sprintAction = input.actions["Sprint"];
                jumpAction = input.actions["Jump"];
            }
        }

        void Update()
        {
            if (moveAction == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                Speed = 0f;
                return;
            }

            var input = moveAction.ReadValue<Vector2>();
            var isSprinting = sprintAction != null && sprintAction.IsPressed();
            var targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            var direction = transform.right * input.x + transform.forward * input.y;
            // Diagonals would otherwise be sqrt(2) faster than forward/side movement.
            direction = Vector3.ClampMagnitude(direction, 1f);
            Speed = direction.magnitude * targetSpeed;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (jumpAction != null && jumpAction.WasPressedThisFrame())
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((direction * targetSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

            if (animator != null)
            {
                animator.SetInteger("Status_walk", direction.sqrMagnitude > 0.01f ? 1 : 0);
            }
        }
    }
}
