using System.Collections;
using System.Reflection;
using AlienDefense.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class TopDownCameraControllerPlayModeTests
    {
        private GameObject _rigObject;
        private GameObject _cameraObject;
        private GameObject _followTargetObject;

        [TearDown]
        public void TearDown()
        {
            if (_rigObject != null) Object.Destroy(_rigObject);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_followTargetObject != null) Object.Destroy(_followTargetObject);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator TopDownCameraController_SnapsToFollowTargetPlusOffset_OnFirstFrame()
        {
            _followTargetObject = new GameObject("FollowTarget");
            _followTargetObject.transform.position = new Vector3(4f, 0f, 6f);

            _cameraObject = new GameObject("Main Camera");
            _rigObject = new GameObject("CameraRig");
            var controller = _rigObject.AddComponent<TopDownCameraController>();

            var offset = new Vector3(0f, 12f, -9f);
            SetPrivateField(controller, "_followTarget", _followTargetObject.transform);
            SetPrivateField(controller, "_cameraTransform", _cameraObject.transform);
            SetPrivateField(controller, "_positionOffset", offset);

            yield return null; // one LateUpdate pass

            Vector3 expected = _followTargetObject.transform.position + offset;
            Vector3 actual = _cameraObject.transform.position;
            Assert.AreEqual(expected.x, actual.x, 0.01f);
            Assert.AreEqual(expected.y, actual.y, 0.01f);
            Assert.AreEqual(expected.z, actual.z, 0.01f);
        }

        [UnityTest]
        public IEnumerator TopDownCameraController_FollowsMovingTarget_TowardsItOverTime()
        {
            _followTargetObject = new GameObject("FollowTarget");
            _cameraObject = new GameObject("Main Camera");
            _rigObject = new GameObject("CameraRig");
            var controller = _rigObject.AddComponent<TopDownCameraController>();

            SetPrivateField(controller, "_followTarget", _followTargetObject.transform);
            SetPrivateField(controller, "_cameraTransform", _cameraObject.transform);
            SetPrivateField(controller, "_positionOffset", Vector3.zero);
            SetPrivateField(controller, "_followSmoothTime", 0.5f);

            yield return null; // snaps to (0,0,0)

            _followTargetObject.transform.position = new Vector3(10f, 0f, 0f);

            yield return null;
            float distanceAfterOneFrame = Vector3.Distance(_cameraObject.transform.position, _followTargetObject.transform.position);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            float distanceAfterSettling = Vector3.Distance(_cameraObject.transform.position, _followTargetObject.transform.position);

            Assert.Less(distanceAfterSettling, distanceAfterOneFrame);
        }
    }
}
