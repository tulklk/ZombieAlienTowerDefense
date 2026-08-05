using AlienDefense.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class LevelBoundsTests
    {
        private GameObject _boundsObject;

        [TearDown]
        public void TearDown()
        {
            if (_boundsObject != null)
            {
                Object.DestroyImmediate(_boundsObject);
            }
        }

        private LevelBounds CreateBounds(Vector2 center, Vector2 extents)
        {
            _boundsObject = new GameObject("TestLevelBounds");
            var bounds = _boundsObject.AddComponent<LevelBounds>();

            var serialized = new SerializedObject(bounds);
            serialized.FindProperty("_center").vector2Value = center;
            serialized.FindProperty("_extents").vector2Value = extents;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return bounds;
        }

        [Test]
        public void ClampXZ_LeavesPositionUnchanged_WhenInsideBounds()
        {
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(10f, 10f));

            Vector3 result = bounds.ClampXZ(new Vector3(3f, 5f, -2f));

            Assert.AreEqual(new Vector3(3f, 5f, -2f), result);
        }

        [Test]
        public void ClampXZ_ClampsToEdge_WhenOutsideBounds()
        {
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(10f, 10f));

            Vector3 result = bounds.ClampXZ(new Vector3(50f, 0f, -50f));

            Assert.AreEqual(10f, result.x, 0.001f);
            Assert.AreEqual(-10f, result.z, 0.001f);
        }

        [Test]
        public void ClampXZ_DoesNotTouchY()
        {
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(10f, 10f));

            Vector3 result = bounds.ClampXZ(new Vector3(0f, 7.25f, 0f));

            Assert.AreEqual(7.25f, result.y, 0.0001f);
        }

        [Test]
        public void ClampXZ_WithPadding_ShrinksUsableRange()
        {
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(10f, 10f));

            Vector3 result = bounds.ClampXZ(new Vector3(9f, 0f, 0f), padding: 2f);

            Assert.AreEqual(8f, result.x, 0.001f);
        }

        [Test]
        public void ClampXZ_WithPaddingLargerThanExtents_CollapsesToMidpoint_InsteadOfInverting()
        {
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(5f, 5f));

            Vector3 result = bounds.ClampXZ(new Vector3(100f, 0f, -100f), padding: 50f);

            Assert.AreEqual(0f, result.x, 0.001f);
            Assert.AreEqual(0f, result.z, 0.001f);
        }
    }
}
