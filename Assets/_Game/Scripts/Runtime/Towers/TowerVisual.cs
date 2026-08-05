using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Rotates the turret pivot toward the current target on the Y axis only. Deals no damage.</summary>
    public sealed class TowerVisual : MonoBehaviour
    {
        [SerializeField]
        private Transform _turretPivot;

        [SerializeField]
        [Tooltip("The turret model's local forward direction. Must be horizontal (Y = 0).")]
        private Vector3 _forwardAxis = Vector3.forward;

        [SerializeField]
        [Tooltip("Optional. Fired via PlayMuzzleFlash on each attack.")]
        private ParticleSystem _muzzleFlash;

        private float _rotationSpeed = 360f;

        public void SetRotationSpeed(float rotationSpeed)
        {
            _rotationSpeed = Mathf.Max(0f, rotationSpeed);
        }

        public void Tick(float deltaTime, EnemyController target)
        {
            if (_turretPivot == null || target == null)
            {
                return;
            }

            Vector3 toTarget = target.AimPoint.position - _turretPivot.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Quaternion axisCorrection = Quaternion.FromToRotation(_forwardAxis, Vector3.forward);
            Quaternion targetRotation = lookRotation * axisCorrection;

            _turretPivot.rotation = Quaternion.RotateTowards(_turretPivot.rotation, targetRotation, _rotationSpeed * deltaTime);
        }

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlash != null)
            {
                _muzzleFlash.Play();
            }
        }
    }
}
