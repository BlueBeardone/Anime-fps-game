using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.Input
{
    /// <summary>
    /// The one place gameplay reads the Input System. Caches the shared Player action map once and
    /// exposes plain properties, so movement, look and weapons never touch actions themselves and a
    /// future AI controller can bypass this component entirely.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InputReader : MonoBehaviour
    {
        InputAction move;
        InputAction look;
        InputAction sprint;
        InputAction crouch;
        InputAction jump;
        InputAction attack;
        InputAction reload;
        InputAction pickup;
        InputAction next;
        InputAction previous;

        public Vector2 Move => move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => look != null ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintHeld => sprint != null && sprint.IsPressed();
        public bool CrouchHeld => crouch != null && crouch.IsPressed();
        public bool JumpPressed => jump != null && jump.WasPressedThisFrame();
        public bool AttackPressed => attack != null && attack.WasPressedThisFrame();
        public bool AttackHeld => attack != null && attack.IsPressed();
        public bool ReloadPressed => reload != null && reload.WasPressedThisFrame();
        public bool PickupPressed => pickup != null && pickup.WasPressedThisFrame();
        public bool CycleNextPressed => next != null && next.WasPressedThisFrame();
        public bool CyclePreviousPressed => previous != null && previous.WasPressedThisFrame();
        public float ScrollY => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            var actions = playerInput != null ? playerInput.actions : null;
            if (actions == null)
            {
                return;
            }

            PlayerInputBindings.EnsureLoaded(actions);
            move = actions.FindAction("Move");
            look = actions.FindAction("Look");
            sprint = actions.FindAction("Sprint");
            crouch = actions.FindAction("Crouch");
            jump = actions.FindAction("Jump");
            attack = actions.FindAction("Attack");
            reload = actions.FindAction("Reload");
            pickup = actions.FindAction("Pickup");
            next = actions.FindAction("Next");
            previous = actions.FindAction("Previous");
        }
    }
}
