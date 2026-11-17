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
        bool paused;
        bool grounded;
        float lastGroundedTime = float.NegativeInfinity;

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
        public bool IsCrouching => false;
        public bool IsSprinting => false;
        public bool IsSliding => false;
        public float HorizontalSpeed => Horizontal(body.linearVelocity).magnitude;
        public Vector3 Velocity => body.linearVelocity;
        public float EyeHeight => profile != null ? profile.eyeHeight : 1.65f;

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
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
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

        public void ApplyCrouchDimensions(bool crouched)
        {
        }

        void ClearIntent()
        {
            wishDirection = Vector3.zero;
        }

        void FixedUpdate()
        {
            if (paused)
            {
                return;
            }

            var dt = Time.fixedDeltaTime;
            UpdateGrounded();

            var velocity = body.linearVelocity;
            var horizontal = Horizontal(velocity);
            var vertical = velocity.y;

            if (grounded)
            {
                ApplyFriction(ref horizontal, dt);
                ApplyAcceleration(ref horizontal, wishDirection, profile.runSpeed,
                    profile.groundAcceleration, dt);
                vertical = -profile.groundStickSpeed;
            }
            else
            {
                vertical -= profile.gravity * dt;
                vertical = Mathf.Max(vertical, -profile.terminalVelocity);
            }

            body.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
            UpdateState(horizontal);
        }

        void UpdateGrounded()
        {
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
