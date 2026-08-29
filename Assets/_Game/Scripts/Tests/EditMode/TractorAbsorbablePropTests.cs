using UnityEngine;
using AlienDefense.Environment;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers TractorAbsorbableProp's own Pull->Lift->Absorbed state machine in isolation. There is no
    /// reward path to test here by construction: this class holds no reference to any Economy/Energy/Progression
    /// service (see its class doc comment) — absorption is purely visual/removal.</summary>
    public class TractorAbsorbablePropTests
    {
        private GameObject _propObject;
        private GameObject _anchorObject;
        private GameObject _socketObject;

        [TearDown]
        public void TearDown()
        {
            if (_propObject != null) Object.DestroyImmediate(_propObject);
            if (_anchorObject != null) Object.DestroyImmediate(_anchorObject);
            if (_socketObject != null) Object.DestroyImmediate(_socketObject);
        }

        private TractorAbsorbableProp CreateProp()
        {
            _propObject = new GameObject("TestProp");
            return _propObject.AddComponent<TractorAbsorbableProp>();
        }

        private TractorPropAbsorptionRequest CreateRequest()
        {
            _anchorObject = new GameObject("Anchor");
            _socketObject = new GameObject("Socket");
            return new TractorPropAbsorptionRequest(
                _anchorObject.transform, _socketObject.transform,
                pullSpeed: 8f, liftSpeed: 6f, centerThreshold: 0.2f, socketThreshold: 0.15f,
                shrinkDuringLift: true, minimumVisualScale: 0.15f, spinSpeedDegreesPerSecond: 0f);
        }

        [Test]
        public void NewProp_StartsIdle_Absorbable_AndDisabled()
        {
            TractorAbsorbableProp prop = CreateProp();
            Assert.AreEqual(TractorAbsorbablePropState.Idle, prop.State);
            Assert.IsTrue(prop.IsAbsorbable);
            Assert.IsFalse(prop.enabled, "Idle props must not run Update.");
        }

        [Test]
        public void CanBeAbsorbed_False_RejectsAdmission()
        {
            TractorAbsorbableProp prop = CreateProp();
            var serialized = new UnityEditor.SerializedObject(prop);
            serialized.FindProperty("_canBeAbsorbed").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            bool started = prop.TryBeginAbsorption(CreateRequest());

            Assert.IsFalse(started, "A prop with CanBeAbsorbed=false must never start absorption, matching the opt-in-only rule.");
        }

        [Test]
        public void FullPullLiftCycle_FiresAbsorbed_ThenDestroysTheGameObject()
        {
            TractorAbsorbableProp prop = CreateProp();
            bool absorbed = false;
            TractorAbsorbablePropState stateDuringEvent = default;
            // State must be read from inside the event handler: by the time Tick() returns, DestroyImmediate
            // (the edit-mode path) has already invalidated `prop`, so reading prop.State afterward would throw.
            prop.Absorbed += _ =>
            {
                absorbed = true;
                stateDuringEvent = prop.State;
            };

            bool started = prop.TryBeginAbsorption(CreateRequest());
            Assert.IsTrue(started);

            for (int i = 0; i < 10 && !absorbed; i++)
            {
                prop.Tick(0.5f);
            }

            Assert.IsTrue(absorbed);
            Assert.AreEqual(TractorAbsorbablePropState.Absorbed, stateDuringEvent);
            Assert.IsTrue(_propObject == null, "Absorbed props are actually Destroyed (for memory/perf with many props on a level) — scene reload restores them (see class doc comment).");
        }

        [Test]
        public void Registry_UnregistersItself_OnAbsorbed()
        {
            var registry = new TractorAbsorbablePropRegistry();
            TractorAbsorbableProp prop = CreateProp();
            prop.Register(registry);
            Assert.AreEqual(1, registry.Count);

            bool absorbed = false;
            prop.Absorbed += _ => absorbed = true;

            prop.TryBeginAbsorption(CreateRequest());
            // Must stop ticking the instant absorption completes: CompleteAbsorption destroys the GameObject
            // (edit mode: immediately), so a further prop.Tick() call afterward would throw.
            for (int i = 0; i < 10 && !absorbed; i++)
            {
                prop.Tick(0.5f);
            }

            Assert.AreEqual(0, registry.Count, "An absorbed prop must remove itself from the registry so the beam stops scanning it.");
        }
    }
}
