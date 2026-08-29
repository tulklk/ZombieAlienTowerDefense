using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Everything EnergyPickupController needs to pull and lift one pickup. Carries no gameplay service
    /// references (no EnergyWallet/PlayerLevelProgression/Pool) — those stay owned by the collection pipeline.
    /// Mirrors AlienDefense.Enemies.TractorCaptureRequest's shape.</summary>
    public readonly struct TractorEnergyAbsorptionRequest
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

        public TractorEnergyAbsorptionRequest(
            Transform beamGroundAnchor,
            Transform captureSocket,
            float pullSpeed,
            float liftSpeed,
            float centerThreshold,
            float socketThreshold,
            bool shrinkDuringLift,
            float minimumVisualScale,
            float spinSpeedDegreesPerSecond)
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
        }

        public bool IsValid => BeamGroundAnchor != null && CaptureSocket != null;
    }
}
