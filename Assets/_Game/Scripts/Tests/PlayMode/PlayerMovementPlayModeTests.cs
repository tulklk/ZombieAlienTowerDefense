using System.Collections;
using System.Reflection;
using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.Input;
using AlienDefense.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class PlayerMovementPlayModeTests
    {
        private sealed class FakePlayerInput : IPlayerInput
        {
            public Vector2 MoveInput { get; set; }
        }

        private GameObject _playerObject;
        private GameObject _boundsObject;
        private PlayerDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
            {
                Object.Destroy(_playerObject);
            }

            if (_boundsObject != null)
            {
                Object.Destroy(_boundsObject);
            }

            if (_definition != null)
            {
                Object.Destroy(_definition);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private LevelBounds CreateBounds(Vector2 center, Vector2 extents)
        {
            _boundsObject = new GameObject("TestLevelBounds");
            var bounds = _boundsObject.AddComponent<LevelBounds>();
            SetPrivateField(bounds, "_center", center);
            SetPrivateField(bounds, "_extents", extents);
            return bounds;
        }

        [UnityTest]
        public IEnumerator PlayerMovement_MovesAlongPositiveZ_WhenGivenForwardInput()
        {
            _definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            _playerObject = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerMovement));
            var movement = _playerObject.GetComponent<PlayerMovement>();
            var input = new FakePlayerInput { MoveInput = new Vector2(0f, 1f) };

            movement.Initialize(_definition, input, null);

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.Greater(_playerObject.transform.position.z, 0f);
        }

        [UnityTest]
        public IEnumerator PlayerMovement_HoldsHoverHeight_WhileMovingOnXZ()
        {
            _definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            _playerObject = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerMovement));
            var movement = _playerObject.GetComponent<PlayerMovement>();
            var input = new FakePlayerInput { MoveInput = new Vector2(1f, 1f) };

            movement.Initialize(_definition, input, null);
            float expectedHeight = _definition.HoverHeight;

            for (int i = 0; i < 15; i++)
            {
                yield return null;
                Assert.AreEqual(expectedHeight, _playerObject.transform.position.y, 0.01f);
            }
        }

        [UnityTest]
        public IEnumerator PlayerMovement_DoesNotMove_WhenMovementDisabled()
        {
            _definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            _playerObject = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerMovement));
            var movement = _playerObject.GetComponent<PlayerMovement>();
            var input = new FakePlayerInput { MoveInput = new Vector2(1f, 0f) };

            movement.Initialize(_definition, input, null);
            movement.SetMovementEnabled(false);

            Vector3 startPosition = _playerObject.transform.position;

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.AreEqual(startPosition.x, _playerObject.transform.position.x, 0.001f);
            Assert.AreEqual(startPosition.z, _playerObject.transform.position.z, 0.001f);
        }

        [UnityTest]
        public IEnumerator PlayerMovement_DoesNotMove_WhenInputIsBelowDeadZone()
        {
            _definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            _playerObject = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerMovement));
            var movement = _playerObject.GetComponent<PlayerMovement>();
            var input = new FakePlayerInput { MoveInput = new Vector2(0.05f, 0f) };

            movement.Initialize(_definition, input, null);
            Vector3 startPosition = _playerObject.transform.position;

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.AreEqual(startPosition.x, _playerObject.transform.position.x, 0.001f);
            Assert.AreEqual(startPosition.z, _playerObject.transform.position.z, 0.001f);
        }

        [UnityTest]
        public IEnumerator PlayerMovement_IsClampedByLevelBounds_WhenRunningTowardTheEdge()
        {
            _definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            LevelBounds bounds = CreateBounds(Vector2.zero, new Vector2(3f, 3f));
            _playerObject = new GameObject("TestPlayer", typeof(CharacterController), typeof(PlayerMovement));
            var movement = _playerObject.GetComponent<PlayerMovement>();
            var input = new FakePlayerInput { MoveInput = new Vector2(0f, 1f) };

            movement.Initialize(_definition, input, bounds);

            for (int i = 0; i < 120; i++)
            {
                yield return null;
            }

            Assert.LessOrEqual(_playerObject.transform.position.z, 3.01f);
        }
    }
}
