using UnityEngine;

namespace AnimeFpsGame.Movement
{
    /// <summary>
    /// What a movement driver wants to do this step. Humans build it from input; AI builds it from
    /// steering. <see cref="CharacterMovement"/> never reads input or networking itself.
    /// </summary>
    public struct MovementIntent
    {
        /// <summary>World space, Y ignored, clamped to unit length.</summary>
        public Vector3 WishDirection;

        /// <summary>Held sprint.</summary>
        public bool Sprint;

        /// <summary>Held crouch.</summary>
        public bool Crouch;

        /// <summary>Jump pressed this step; the motor latches and consumes it.</summary>
        public bool Jump;

        public static MovementIntent None => default;
    }
}
