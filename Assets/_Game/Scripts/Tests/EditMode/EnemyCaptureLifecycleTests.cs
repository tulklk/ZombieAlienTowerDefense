using System.Reflection;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers EnemyCaptureController's Pull -> Lift -> Completed state machine in isolation: it owns the
    /// Transform, follows moving anchors, shrinks VisualRoot, and never touches EnemyRegistry/Pool/Economy.</summary>
    public class EnemyCaptureLifecycleTests
    {
        private GameObject _enemyObject;
        private GameObject _anchorObject;
        private GameObject _socketObject;
        private GameObject _visualRootObject;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _enemyObject, _anchorObject, _socketObject, _visualRootObject })
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EnemyCaptureController CreateController(Vector3 startPosition)
        {
            _enemyObject = new GameObject("TestEnemy");
            _enemyObject.transform.position = startPosition;

            _visualRootObject = new GameObject("VisualRoot");
            _visualRootObject.transform.SetParent(_enemyObject.transform, false);
            _visualRootObject.transform.localScale = new Vector3(2f, 2f, 2f);

            var controller = _enemyObject.AddComponent<EnemyCaptureController>();
            SetPrivateField(controller, "_visualRoot", _visualRootObject.transform);

            // Re-run Awake manually: EditMode component creation does not run Awake synchronously, and Awake is
            // where the controller caches VisualRoot's initial scale for later shrink/restore.
            typeof(EnemyCaptureController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);

            return controller;
        }

        private TractorCaptureRequest CreateRequest(Vector3 anchorPosition, Vector3 socketPosition, bool shrink = true, float spinSpeed = 240f)
        {
            _anchorObject = new GameObject("BeamGroundAnchor") { transform = { position = anchorPosition } };
            _socketObject = new GameObject("CaptureSocket") { transform = { position = socketPosition } };

            return new TractorCaptureRequest(
                _anchorObject.transform,
                _socketObject.transform,
                pullSpeed: 8f,
                liftSpeed: 6f,
                centerThreshold: 0.2f,
                socketThreshold: 0.15f,
                shrinkDuringLift: shrink,
                minimumVisualScale: 0.25f,
                spinSpeedDegreesPerSecond: spinSpeed);
        }

        [Test]
        public void BeginCapture_EntersPullingState()
        {
            EnemyCaptureController controller = CreateController(new Vector3(5f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f));

            controller.BeginCapture(request, null);

            Assert.AreEqual(EnemyCaptureState.Pulling, controller.State);
        }

        [Test]
        public void Pulling_MovesTowardAnchor_KeepingOriginalGroundY()
        {
            EnemyCaptureController controller = CreateController(new Vector3(5f, 0.5f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f));
            controller.BeginCapture(request, null);

            controller.Tick(0.1f);

            Vector3 position = controller.transform.position;
            Assert.Less(position.x, 5f, "Enemy should move toward the anchor on X.");
            Assert.AreEqual(0.5f, position.y, 0.001f, "Y must stay at the enemy's original ground height during Pull.");
        }

        [Test]
        public void Pulling_FollowsAnchorThatMoves_NotACachedPosition()
        {
            EnemyCaptureController controller = CreateController(new Vector3(5f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(new Vector3(0f, 0f, 0f), new Vector3(0f, 1.5f, 0f));
            controller.BeginCapture(request, null);

            controller.Tick(0.1f);
            float xAfterFirstTick = controller.transform.position.x;

            // Simulate the UFO having moved: the anchor Transform itself moves.
            _anchorObject.transform.position = new Vector3(10f, 0f, 0f);
            controller.Tick(0.1f);

            Assert.Greater(controller.transform.position.x, xAfterFirstTick,
                "Enemy must keep following the anchor's current position, not a position cached at BeginCapture.");
        }

        [Test]
        public void ReachingCenterThreshold_TransitionsToLifting()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0.05f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f));
            controller.BeginCapture(request, null);

            controller.Tick(0.1f);

            Assert.AreEqual(EnemyCaptureState.Lifting, controller.State);
        }

        [Test]
        public void Lifting_MovesTowardSocket_OnXAndY()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0.05f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f));
            controller.BeginCapture(request, null);
            controller.Tick(0.1f);
            Assert.AreEqual(EnemyCaptureState.Lifting, controller.State);

            Vector3 beforeLift = controller.transform.position;
            controller.Tick(0.1f);
            Vector3 afterLift = controller.transform.position;

            Assert.Greater(afterLift.y, beforeLift.y, "Lift must raise the enemy toward the capture socket.");
        }

        [Test]
        public void Lifting_ShrinksVisualRoot_TowardMinimumScale()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0.05f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 0.3f, 0f));
            controller.BeginCapture(request, null);
            controller.Tick(0.1f);
            Assert.AreEqual(EnemyCaptureState.Lifting, controller.State);

            controller.Tick(0.1f);

            float scaleRatio = _visualRootObject.transform.localScale.x / 2f;
            Assert.Less(scaleRatio, 1f, "VisualRoot should have started shrinking during Lift.");
            Assert.GreaterOrEqual(scaleRatio, 0.25f, "VisualRoot must never shrink past MinimumVisualScale.");
        }

        [Test]
        public void Lifting_DoesNotShrink_WhenShrinkDuringLiftIsFalse()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0.05f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f), shrink: false);
            controller.BeginCapture(request, null);
            controller.Tick(0.1f);
            controller.Tick(0.1f);

            Assert.AreEqual(2f, _visualRootObject.transform.localScale.x, 0.001f);
        }

        [Test]
        public void ReachingSocket_CompletesCapture_FiresCallbackExactlyOnce()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, 0f));
            int callbackCount = 0;
            controller.BeginCapture(request, () => callbackCount++);

            for (int i = 0; i < 20 && controller.State != EnemyCaptureState.Completed; i++)
            {
                controller.Tick(0.1f);
            }

            Assert.AreEqual(EnemyCaptureState.Completed, controller.State);
            Assert.AreEqual(1, callbackCount);

            controller.Tick(0.1f);
            Assert.AreEqual(1, callbackCount, "Completion callback must never fire twice.");
        }

        [Test]
        public void Abort_DuringPulling_StopsMovement_RestoresVisual_NoCallback()
        {
            EnemyCaptureController controller = CreateController(new Vector3(5f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 1.5f, 0f));
            int callbackCount = 0;
            controller.BeginCapture(request, () => callbackCount++);
            controller.Tick(0.1f);

            controller.Abort();
            Vector3 positionAtAbort = controller.transform.position;
            controller.Tick(0.5f);

            Assert.AreEqual(EnemyCaptureState.Aborted, controller.State);
            Assert.AreEqual(positionAtAbort, controller.transform.position, "Aborted capture must stop moving the Transform.");
            Assert.AreEqual(0, callbackCount);
            Assert.AreEqual(2f, _visualRootObject.transform.localScale.x, 0.001f);
        }

        [Test]
        public void ResetState_RestoresInactiveAndVisual_ForPoolReuse()
        {
            EnemyCaptureController controller = CreateController(new Vector3(0.05f, 0f, 0f));
            TractorCaptureRequest request = CreateRequest(Vector3.zero, new Vector3(0f, 0.3f, 0f));
            controller.BeginCapture(request, null);
            controller.Tick(0.1f);
            controller.Tick(0.1f);

            controller.ResetState();

            Assert.AreEqual(EnemyCaptureState.Inactive, controller.State);
            Assert.AreEqual(2f, _visualRootObject.transform.localScale.x, 0.001f);
            Assert.AreEqual(Quaternion.identity, _visualRootObject.transform.localRotation);
        }
    }
}
