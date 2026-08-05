using UnityEngine;

namespace AlienDefense.CameraSystem
{
    /// <summary>
    /// The one piece of player state the camera (and hover visual) is allowed to read: which way
    /// it's currently moving. Defined here, next to its consumers, so TopDownCameraController never
    /// needs a compile-time reference to PlayerController — anything can implement this.
    /// </summary>
    public interface IMovementDirectionSource
    {
        /// <summary>Current normalized-ish movement direction on the XZ input plane; zero when idle.</summary>
        Vector2 MovementDirection { get; }
    }
}
