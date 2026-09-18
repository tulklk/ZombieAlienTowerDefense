using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Visual-only kick on the barrel whose muzzle just fired. Listens to
    /// <see cref="TowerAttackController.FiredFromMuzzle"/>; never touches combat math.</summary>
    public sealed class MortarBarrelRecoil : MonoBehaviour
    {
        [SerializeField]
        private TowerAttackController _attack;

        [SerializeField]
        private Transform _leftBarrel;

        [SerializeField]
        private Transform _rightBarrel;

        [SerializeField, Min(0f)]
        private float _recoilDistance = 0.06f;

        [SerializeField, Min(0.01f)]
        private float _recoilDuration = 0.1f;

        private Vector3 _leftRest;
        private Vector3 _rightRest;

        private void Awake()
        {
            CacheRestPositions();
        }

        private void OnEnable()
        {
            if (_attack != null)
            {
                _attack.FiredFromMuzzle += OnFiredFromMuzzle;
            }
        }

        private void OnDisable()
        {
            if (_attack != null)
            {
                _attack.FiredFromMuzzle -= OnFiredFromMuzzle;
            }

            KillAndRestore(_leftBarrel, _leftRest);
            KillAndRestore(_rightBarrel, _rightRest);
        }

        private void CacheRestPositions()
        {
            if (_leftBarrel != null)
            {
                _leftRest = _leftBarrel.localPosition;
            }

            if (_rightBarrel != null)
            {
                _rightRest = _rightBarrel.localPosition;
            }
        }

        private void OnFiredFromMuzzle(Transform muzzle)
        {
            if (muzzle == null)
            {
                return;
            }

            Transform barrel = muzzle.parent;
            if (barrel == _leftBarrel)
            {
                Punch(barrel, _leftRest);
            }
            else if (barrel == _rightBarrel)
            {
                Punch(barrel, _rightRest);
            }
        }

        private void Punch(Transform barrel, Vector3 rest)
        {
            if (barrel == null || _recoilDistance <= 0f)
            {
                return;
            }

            DOTween.Kill(barrel);
            barrel.localPosition = rest;
            Vector3 kicked = rest + new Vector3(0f, 0f, -_recoilDistance);
            float outDuration = _recoilDuration * 0.4f;
            float backDuration = _recoilDuration * 0.6f;

            DOTween.Sequence()
                .SetTarget(barrel)
                .Append(DOTween.To(() => barrel.localPosition, p => barrel.localPosition = p, kicked, outDuration)
                    .SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => barrel.localPosition, p => barrel.localPosition = p, rest, backDuration)
                    .SetEase(Ease.OutCubic));
        }

        private static void KillAndRestore(Transform barrel, Vector3 rest)
        {
            if (barrel == null)
            {
                return;
            }

            DOTween.Kill(barrel);
            barrel.localPosition = rest;
        }
    }
}
