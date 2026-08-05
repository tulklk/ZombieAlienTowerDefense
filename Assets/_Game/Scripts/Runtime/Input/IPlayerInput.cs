using UnityEngine;

namespace AlienDefense.Input
{
    /// <summary>Movement-plane input abstraction consumed by PlayerMovement.</summary>
    public interface IPlayerInput
    {
        /// <summary>Raw 2D move input, X = left/right, Y = forward/back.</summary>
        Vector2 MoveInput { get; }
    }
}
