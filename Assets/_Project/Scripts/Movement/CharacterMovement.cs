using System;
using UnityEngine;

namespace AnimeFpsGame.Movement
{
    /// <summary>
    /// Source-style (CS2) movement on a dynamic Rigidbody capsule. Passive: a human motor or an AI
    /// brain pushes a <see cref="MovementIntent"/>; this component never reads input, cameras or
    /// networking. Every tick starts from the Rigidbody's actual velocity so PhysX contacts, prop
    /// hits and impulses survive.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterMovement : MonoBehaviour
    {
        const float GroundedGrace = 0.1f;
        const float MovingSpeedThreshold = 0.1f;

        [SerializeField] MovementProfile profile;

        Rigidbody body;
        CapsuleCollider capsule;
        int groundMask;

        Vector3 wishDirection;
        bool sprintHeld;
        bool crouchHeld;
        bool jumpHeld;
        bool paused;
        bool grounded;
        bool sprinting;
        bool crouchTarget;
        float crouchT;
        float lastGroundedTime = float.NegativeInfinity;
        float jumpBufferExpiry = float.NegativeInfinity;

        public MovementProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                ApplyProfile();
            }
        }

        public MovementState State { get; private set; }
        public bool IsGrounded => grounded;
        public bool IsCrouching => crouchTarget;
        public bool IsSprinting => sprinting;
        public bool IsSliding => false;
        public float HorizontalSpeed => Horizontal(body.linearVelocity).magnitude;
        public Vector3 Velocity => body.linearVelocity;
        public float EyeHeight => profile != null
            ? Mathf.Lerp(profile.eyeHeight, profile.crouchEyeHeight, crouchT)
            : 1.65f;

        public event Action Jumped;
        public event Action<bool> CrouchChanged;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            profile ??= MovementProfile.CreateDefault();
            groundMask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            ApplyProfile();
        }

        void ApplyProfile()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.constraints |= RigidbodyConstraints.FreezeRotation;
            body.mass = profile.playerMass;
        }

        public void SetIntent(in MovementIntent intent)
        {
            var direction = intent.WishDirection;
            direction.y = 0f;
            wishDirection = Vector3.ClampMagnitude(direction, 1f);
            sprintHeld = intent.Sprint;
            crouchHeld = intent.Crouch;

            // A jump is only queued on the rising edge, so holding the button never auto-bhops.
            if (intent.Jump && !jumpHeld)
            {
                jumpBufferExpiry = Time.time + profile.jumpBuffer;
            }

            jumpHeld = intent.Jump;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ClearIntent();
            lastGroundedTime = float.NegativeInfinity;
            grounded = false;
            Physics.SyncTransforms();
        }

        public void AddImpulse(Vector3 deltaVelocity)
        {
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (!paused)
            {
                return;
            }

            ClearIntent();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Instantly resize to a crouched or standing capsule. Used by network proxies so their body
        /// (and therefore server hit validation) matches the owner.
        /// </summary>
        public void ApplyCrouchDimensions(bool crouched)
        {
            crouchT = crouched ? 1f : 0f;
            SetCrouchTarget(crouched);
            ApplyCapsule(crouchT);
        }

        void ClearIntent()
        {
            wishDirection = Vector3.zero;
            sprintHeld = false;
            crouchHeld = false;
            jumpHeld = false;
            jumpBufferExpiry = float.NegativeInfinity;
        }

        void FixedUpdate()
        {
            if (paused)
            {
                return;
            }

            var dt = Time.fixedDeltaTime;
            UpdateGrounded();
            UpdateCrouch(dt);

            var velocity = body.linearVelocity;
            var horizontal = Horizontal(velocity);
            var vertical = velocity.y;

            if (TryJump())
            {
                vertical = Mathf.Sqrt(2f * profile.gravity * profile.jumpHeight);
            }
            else if (grounded)
            {
                UpdateSprint();
                ApplyFriction(ref horizontal, dt);
                ApplyAcceleration(ref horizontal, wishDirection, WishSpeed,
                    profile.groundAcceleration, dt);

                // Contacts keep the capsule on the ground; never force downward velocity (that would
                // fight the solver and make a freshly teleported body report speed).
                vertical = Mathf.Min(vertical, 0f);
            }
            else
            {
                sprinting = false;
                var wishSpeed = Mathf.Min(WishSpeed, profile.airSpeedCap);
                ApplyAcceleration(ref horizontal, wishDirection, wishSpeed,
                    profile.airAcceleration, dt);
                vertical -= profile.gravity * dt;
                vertical = Mathf.Max(vertical, -profile.terminalVelocity);
            }

            body.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
            UpdateState(horizontal);
        }

        float WishSpeed => crouchTarget
            ? profile.crouchSpeed
            : sprinting ? profile.sprintSpeed : profile.runSpeed;

        void UpdateCrouch(float dt)
        {
            var desired = crouchHeld;
            if (!desired && crouchTarget && IsStandingBlocked())
            {
                desired = true;
            }

            SetCrouchTarget(desired);

            var target = crouchTarget ? 1f : 0f;
            if (Mathf.Approximately(crouchT, target))
            {
                return;
            }

            var step = dt / Mathf.Max(profile.crouchTransition, 0.0001f);
            crouchT = Mathf.MoveTowards(crouchT, target, step);
            ApplyCapsule(crouchT);
        }

        void SetCrouchTarget(bool value)
        {
            if (crouchTarget == value)
            {
                return;
            }

            crouchTarget = value;
            CrouchChanged?.Invoke(value);
        }

        void ApplyCapsule(float t)
        {
            var height = Mathf.Lerp(profile.capsuleHeight, profile.crouchHeight, t);
            height = Mathf.Max(height, capsule.radius * 2f);
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        bool IsStandingBlocked()
        {
            var radius = capsule.radius * 0.95f;
            var bottom = body.position + Vector3.up * (radius + 0.05f);
            var top = body.position + Vector3.up * (profile.capsuleHeight - radius - 0.05f);
            return Physics.CheckCapsule(bottom, top, radius, groundMask, QueryTriggerInteraction.Ignore);
        }

        void UpdateSprint()
        {
            sprinting = sprintHeld
                && !crouchTarget
                && wishDirection.sqrMagnitude > 0.0001f
                && Vector3.Dot(transform.forward, wishDirection) >= profile.sprintForwardDot;
        }

        bool TryJump()
        {
            if (jumpBufferExpiry < 0f || Time.time > jumpBufferExpiry)
            {
                return false;
            }

            if (!grounded && Time.time - lastGroundedTime > profile.coyoteTime)
            {
                return false;
            }

            jumpBufferExpiry = float.NegativeInfinity;
            grounded = false;
            lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
            return true;
        }

        void UpdateGrounded()
        {
            // Rising means airborne even while the probe still grazes the ground: otherwise the
            // ground-stick velocity would cancel this or any other upward impulse.
            if (body.linearVelocity.y > 0.1f)
            {
                grounded = false;
                return;
            }

            if (ProbeGround(out _))
            {
                lastGroundedTime = Time.time;
            }

            grounded = Time.time - lastGroundedTime <= GroundedGrace;
        }

        bool ProbeGround(out RaycastHit hit)
        {
            var radius = capsule.radius * 0.95f;
            var origin = body.position + Vector3.up * capsule.radius;
            var distance = capsule.radius + profile.groundProbeDistance;
            if (!Physics.SphereCast(origin, radius, Vector3.down, out hit, distance, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return Vector3.Angle(hit.normal, Vector3.up) <= profile.maxSlopeAngle;
        }

        void ApplyFriction(ref Vector3 horizontal, float dt)
        {
            var speed = horizontal.magnitude;
            if (speed < 0.01f)
            {
                horizontal = Vector3.zero;
                return;
            }

            var control = Mathf.Max(speed, profile.stopSpeed);
            var drop = control * profile.friction * dt;
            var newSpeed = Mathf.Max(speed - drop, 0f);
            horizontal *= newSpeed / speed;
        }

        void ApplyAcceleration(ref Vector3 horizontal, Vector3 direction, float wishSpeed,
            float acceleration, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f || wishSpeed <= 0f)
            {
                return;
            }

            var currentSpeed = Vector3.Dot(horizontal, direction);
            var addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f)
            {
                return;
            }

            var accelSpeed = Mathf.Min(acceleration * wishSpeed * dt, addSpeed);
            horizontal += direction * accelSpeed;
        }

        void UpdateState(Vector3 horizontal)
        {
            var state = MovementState.None;
            if (grounded)
            {
                state |= MovementState.Grounded;
            }

            if (sprinting)
            {
                state |= MovementState.Sprinting;
            }

            if (crouchTarget)
            {
                state |= MovementState.Crouching;
            }

            if (horizontal.magnitude > MovingSpeedThreshold)
            {
                state |= MovementState.Moving;
            }

            State = state;
        }

        static Vector3 Horizontal(Vector3 velocity)
        {
            return new Vector3(velocity.x, 0f, velocity.z);
        }
    }
}
