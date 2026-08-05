using UnityEngine;

namespace AlienDefense.CameraSystem
{
    /// <summary>Exposes current movement direction for camera look-ahead and visual tilt.</summary>
    public interface IMovementDirectionSource
    {
        /// <summary>Movement direction on the XZ input plane; zero when idle.</summary>
        Vector2 MovementDirection { get; }
    }
}
