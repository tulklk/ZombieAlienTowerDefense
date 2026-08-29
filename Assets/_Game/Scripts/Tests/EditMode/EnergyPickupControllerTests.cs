using UnityEngine;
using AlienDefense.Pickups;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers EnergyPickupController's own Pull->Lift->Collected state machine in isolation (no beam,
    /// no EnergyCollectionService) — the reward side effect lives in EnergyPickupFactory, not here.</summary>
    public class EnergyPickupControllerTests
    {
        private GameObject _pickupObject;
        private GameObject _anchorObject;
        private GameObject _socketObject;

        [TearDown]
        public void TearDown()
        {
            if (_pickupObject != null) Object.DestroyImmediate(_pickupObject);
            if (_anchorObject != null) Object.DestroyImmediate(_anchorObject);
            if (_socketObject != null) Object.DestroyImmediate(_socketObject);
        }

        private EnergyPickupController CreatePickup()
        {
            _pickupObject = new GameObject("TestPickup");
            var pickup = _pickupObject.AddComponent<EnergyPickupController>();
            pickup.Initialize(3);
            return pickup;
        }

        private TractorEnergyAbsorptionRequest CreateRequest()
        {
            _anchorObject = new GameObject("Anchor");
            _socketObject = new GameObject("Socket");
            return new TractorEnergyAbsorptionRequest(
                _anchorObject.transform, _socketObject.transform,
                pullSpeed: 8f, liftSpeed: 6f, centerThreshold: 0.2f, socketThreshold: 0.15f,
                shrinkDuringLift: true, minimumVisualScale: 0.25f, spinSpeedDegreesPerSecond: 0f);
        }

        [Test]
        public void Initialize_StartsIdle_AndDisabled()
        {
            EnergyPickupController pickup = CreatePickup();
            Assert.AreEqual(EnergyPickupState.Idle, pickup.State);
            Assert.IsTrue(pickup.IsAbsorbable);
            Assert.IsFalse(pickup.enabled, "Idle pickups must not run Update (see performance requirement).");
        }

        [Test]
        public void TryBeginAbsorption_EntersPulling_AndEnablesUpdate()
        {
            EnergyPickupController pickup = CreatePickup();
            bool started = pickup.TryBeginAbsorption(CreateRequest());

            Assert.IsTrue(started);
            Assert.AreEqual(EnergyPickupState.Pulling, pickup.State);
            Assert.IsFalse(pickup.IsAbsorbable, "No longer admissible once absorption has started.");
            Assert.IsTrue(pickup.enabled);
        }

        [Test]
        public void TryBeginAbsorption_FailsSilently_WhenNotIdle()
        {
            EnergyPickupController pickup = CreatePickup();
            pickup.TryBeginAbsorption(CreateRequest());

            bool secondAttempt = pickup.TryBeginAbsorption(CreateRequest());

            Assert.IsFalse(secondAttempt, "A pickup already Pulling must reject a second admission.");
        }

        [Test]
        public void FullPullLiftCycle_FiresCollected_DisablesUpdateAgain()
        {
            EnergyPickupController pickup = CreatePickup();
            bool collected = false;
            pickup.Collected += _ => collected = true;

            pickup.TryBeginAbsorption(CreateRequest());

            // Anchor and socket are both at the origin in this rig, so a handful of large-deltaTime ticks crosses
            // both the Pull and Lift thresholds.
            for (int i = 0; i < 10 && !collected; i++)
            {
                pickup.Tick(0.5f);
            }

            Assert.IsTrue(collected);
            Assert.AreEqual(EnergyPickupState.Collected, pickup.State);
            Assert.IsFalse(pickup.enabled, "Update must stop again once absorption completes.");
        }

        [Test]
        public void HandleReturnedToPool_ResetsToIdle_ForReuse()
        {
            EnergyPickupController pickup = CreatePickup();
            pickup.TryBeginAbsorption(CreateRequest());
            for (int i = 0; i < 10; i++)
            {
                pickup.Tick(0.5f);
            }

            pickup.HandleReturnedToPool();

            Assert.AreEqual(EnergyPickupState.Idle, pickup.State);
            Assert.IsTrue(pickup.IsAbsorbable);
            Assert.IsFalse(pickup.enabled);
        }
    }
}
