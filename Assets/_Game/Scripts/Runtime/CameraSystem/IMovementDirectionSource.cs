using UnityEngine;

namespace AlienDefense.CameraSystem
{
    /// <summary>Exposes current movement direction for camera look-ahead and visual tilt.</summary>
    public interface IMovementDirectionSource
    {
        /// <summary>Actual world-space XZ movement direction (x=world X, y=world Z), already remapped from any
        /// camera-relative input; zero when idle.</summary>
        Vector2 MovementDirection { get; }
    }
}
