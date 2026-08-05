using UnityEngine;

namespace AlienDefense.Input
{
    /// <summary>
    /// Movement-plane input abstraction. PlayerMovement depends on this, never on a concrete
    /// keyboard/gamepad/on-screen-stick source, so the same movement code works in the Editor
    /// (WASD/gamepad) and on device (virtual joystick) without branching.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Raw 2D move input, X = left/right, Y = forward/back. Not normalized or dead-zoned.</summary>
        Vector2 MoveInput { get; }
    }
}
