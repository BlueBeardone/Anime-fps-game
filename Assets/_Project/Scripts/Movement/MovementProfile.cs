using UnityEngine;

namespace AnimeFpsGame.Movement
{
    /// <summary>
    /// Every tunable of the Source-style movement model. Shared by players, AI and tests; tweak the
    /// asset in <c>Assets/_Project/Settings/Movement</c> instead of the code.
    /// </summary>
    [CreateAssetMenu(menuName = "Anime FPS/Movement Profile")]
    public class MovementProfile : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        public float runSpeed = 6.35f;
        public float sprintSpeed = 8f;
        public float crouchSpeed = 2.15f;

        [Header("Acceleration")]
        public float groundAcceleration = 5.5f;
        public float airAcceleration = 12f;
        public float airSpeedCap = 0.76f;

        [Header("Friction")]
        public float friction = 5.2f;
        public float stopSpeed = 2f;

        [Header("Jump and gravity")]
        public float jumpHeight = 1.45f;
        public float gravity = 20.32f;
        public float terminalVelocity = 50f;
        public float jumpBuffer = 0.1f;
        public float coyoteTime = 0.05f;
        public bool autoBhop;

        [Header("Slopes and steps")]
        public float maxSlopeAngle = 50f;
        public float slideAngle = 51f;
        public float slideAcceleration = 8f;
        public float stepHeight = 0.4f;
        public float groundProbeDistance = 0.15f;
        public float groundSnapDistance = 0.3f;
        public float groundStickSpeed = 2f;

        [Header("Body")]
        public float capsuleHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float eyeHeight = 1.65f;
        public float crouchEyeHeight = 1.05f;
        public float crouchTransition = 0.12f;
        public float sprintForwardDot = 0.7f;
        public float playerMass = 80f;

        public static MovementProfile CreateDefault()
        {
            return CreateInstance<MovementProfile>();
        }
    }
}
