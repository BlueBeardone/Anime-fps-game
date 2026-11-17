using System;

namespace AnimeFpsGame.Movement
{
    [Flags]
    public enum MovementState
    {
        None = 0,
        Grounded = 1,
        Sprinting = 2,
        Crouching = 4,
        Sliding = 8,
        Moving = 16,
    }
}
