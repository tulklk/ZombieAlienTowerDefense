using System.Collections;
using System.Reflection;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class EnemyMovementPlayModeTests
    {
        private GameObject _enemyObject;
        private GameObject _pathObject;
        private GameObject[] _waypointObjects;

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null) Object.Destroy(_enemyObject);
            if (_pathObject != null) Object.Destroy(_pathObject);

            if (_waypointObjects != null)
            {
                foreach (GameObject waypoint in _waypointObjects)
                {
                    if (waypoint != null)
                    {
                        Object.Destroy(waypoint);
                    }
                }
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EnemyPath3D CreatePath(params Vector3[] points)
        {
            _pathObject = new GameObject("TestEnemyPath");
            LogAssert.Expect(LogType.Error, "[EnemyPath3D] 'TestEnemyPath' needs at least two waypoints.");
            var path = _pathObject.AddComponent<EnemyPath3D>();

            _waypointObjects = new GameObject[points.Length];
            var waypointTransforms = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject($"Waypoint_{i}");
                waypoint.transform.position = points[i];
                _waypointObjects[i] = waypoint;
                waypointTransforms[i] = waypoint.transform;
            }

            SetPrivateField(path, "_waypoints", waypointTransforms);
            return path;
        }

        [UnityTest]
        public IEnumerator EnemyMovement_StartsAtFirstWaypoint()
        {
            EnemyPath3D path = CreatePath(new Vector3(3f, 0f, 4f), new Vector3(3f, 0f, 14f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();

            movement.Initialize(path, 5f, 360f, 0.1f);

            Assert.AreEqual(new Vector3(3f, 0f, 4f), _enemyObject.transform.position);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyMovement_MovesTowardNextWaypoint_OnXZ()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();

            movement.Initialize(path, 5f, 360f, 0.1f);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.Greater(_enemyObject.transform.position.z, 0f);
            Assert.AreEqual(0f, _enemyObject.transform.position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator EnemyMovement_RotatesOnlyAroundY()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();

            movement.Initialize(path, 5f, 360f, 0.1f);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Vector3 euler = _enemyObject.transform.rotation.eulerAngles;
            Assert.AreEqual(0f, euler.x, 0.01f);
            Assert.AreEqual(0f, euler.z, 0.01f);
        }

        [UnityTest]
        public IEnumerator EnemyMovement_PathProgressIncreasesOverTime()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();

            movement.Initialize(path, 5f, 360f, 0.1f);
            float initialProgress = movement.PathProgress;

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.Greater(movement.PathProgress, initialProgress);
        }

        [UnityTest]
        public IEnumerator EnemyMovement_FiresDestinationReachedExactlyOnce()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();
            int reachedCount = 0;
            movement.DestinationReached += () => reachedCount++;

            movement.Initialize(path, 5f, 360f, 0.1f);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(1, reachedCount);
        }

        [UnityTest]
        public IEnumerator EnemyMovement_StopsMoving_WhenStopMovementCalled()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f));
            _enemyObject = new GameObject("TestEnemy", typeof(EnemyMovement));
            var movement = _enemyObject.GetComponent<EnemyMovement>();

            movement.Initialize(path, 5f, 360f, 0.1f);
            movement.StopMovement();
            Vector3 startPosition = _enemyObject.transform.position;

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(startPosition, _enemyObject.transform.position);
        }
    }
}
