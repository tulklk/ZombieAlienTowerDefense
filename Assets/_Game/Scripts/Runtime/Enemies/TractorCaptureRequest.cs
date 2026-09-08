using AlienDefense.Common;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Everything EnemyCaptureController needs to pull and lift one enemy. Carries no gameplay service
    /// references (no Economy/WaveController/EnemyPool) — those stay owned by the resolve pipeline.</summary>
    public readonly struct TractorCaptureRequest
    {
        public readonly Transform BeamGroundAnchor;
        public readonly Transform CaptureSocket;
        public readonly float PullSpeed;
        public readonly float LiftSpeed;
        public readonly float CenterThreshold;
        public readonly float SocketThreshold;
        public readonly bool ShrinkDuringLift;
        public readonly float MinimumVisualScale;
        public readonly float SpinSpeedDegreesPerSecond;

        /// <summary>Optional. Read live every frame (see IPullSpeedBoostSource) instead of baked in once here —
        /// null keeps PullSpeed/LiftSpeed exactly as given, unchanged from before this field existed.</summary>
        public readonly IPullSpeedBoostSource SpeedBoost;

        public TractorCaptureRequest(
            Transform beamGroundAnchor,
            Transform captureSocket,
            float pullSpeed,
            float liftSpeed,
            float centerThreshold,
            float socketThreshold,
            bool shrinkDuringLift,
            float minimumVisualScale,
            float spinSpeedDegreesPerSecond,
            IPullSpeedBoostSource speedBoost = null)
        {
            BeamGroundAnchor = beamGroundAnchor;
            CaptureSocket = captureSocket;
            PullSpeed = pullSpeed;
            LiftSpeed = liftSpeed;
            CenterThreshold = centerThreshold;
            SocketThreshold = socketThreshold;
            ShrinkDuringLift = shrinkDuringLift;
            MinimumVisualScale = minimumVisualScale;
            SpinSpeedDegreesPerSecond = spinSpeedDegreesPerSecond;
            SpeedBoost = speedBoost;
        }

        public bool IsValid => BeamGroundAnchor != null && CaptureSocket != null;
    }
}
