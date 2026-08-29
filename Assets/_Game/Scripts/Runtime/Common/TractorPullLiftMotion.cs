using UnityEngine;

namespace AlienDefense.Common
{
    /// <summary>Pure, stateless Pull→Lift position math shared by tractor-beam absorption controllers (Energy
    /// Pickup, Environment Prop — see EnergyPickupController / TractorAbsorbableProp). Knows nothing about
    /// Enemy/Energy/Prop identity, reward, pooling, or waves — only positions, speeds, and thresholds. Every
    /// method is a pure function: no fields, no allocation, safe to call from Update or from tests with an
    /// explicit deltaTime.
    ///
    /// EnemyCaptureController intentionally keeps its own inline copy of the same math rather than being
    /// refactored onto this helper: it already shipped and is covered by tests, and retrofitting a working,
    /// tested capture flow onto a new abstraction is a regression risk this refactor does not need to take.</summary>
    public static class TractorPullLiftMotion
    {
        /// <summary>Moves current toward (groundAnchorXZ.x, groundY, groundAnchorXZ.z) at pullSpeed.
        /// reachedCenter is true once the XZ distance to the anchor is within centerThreshold.</summary>
        public static Vector3 TickPull(
            Vector3 current,
            Vector3 groundAnchorPosition,
            float groundY,
            float pullSpeed,
            float centerThreshold,
            float deltaTime,
            out bool reachedCenter)
        {
            Vector3 target = new Vector3(groundAnchorPosition.x, groundY, groundAnchorPosition.z);
            Vector3 next = Vector3.MoveTowards(current, target, pullSpeed * deltaTime);

            float dx = next.x - groundAnchorPosition.x;
            float dz = next.z - groundAnchorPosition.z;
            reachedCenter = (dx * dx + dz * dz) <= centerThreshold * centerThreshold;
            return next;
        }

        /// <summary>Moves current toward captureSocketPosition at liftSpeed. reachedSocket is true once the
        /// remaining distance is within socketThreshold.</summary>
        public static Vector3 TickLift(
            Vector3 current,
            Vector3 captureSocketPosition,
            float liftSpeed,
            float socketThreshold,
            float deltaTime,
            out float remainingDistance,
            out bool reachedSocket)
        {
            Vector3 next = Vector3.MoveTowards(current, captureSocketPosition, liftSpeed * deltaTime);
            remainingDistance = Vector3.Distance(next, captureSocketPosition);
            reachedSocket = remainingDistance <= socketThreshold;
            return next;
        }

        /// <summary>0..1 shrink progress for the Lift phase, based on how far the object has closed the distance
        /// it started Lift with. Callers Lerp(1, minimumScale, this) themselves.</summary>
        public static float ComputeLiftProgress(float remainingDistance, float liftStartDistance)
        {
            return Mathf.Clamp01(1f - remainingDistance / Mathf.Max(0.01f, liftStartDistance));
        }
    }
}
