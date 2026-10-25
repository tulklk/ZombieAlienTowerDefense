using Unity.Profiling;
using System;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Owns attack cooldown and delegates the actual attack to an IAttackStrategy.</summary>
    public sealed class TowerAttackController : MonoBehaviour
    {
        [SerializeField]
        private Transform _firePoint;

        [SerializeField]
        [Tooltip("Optional. Extra muzzles (e.g. the second barrel of a twin gun). When Alternate Fire Points is " +
            "off, every muzzle fires together and the attack's damage is shared evenly so DPS is unchanged; " +
            "on-hit effects (slow, stun build-up) ride only on the fire point's shot. When Alternate is on, " +
            "exactly one muzzle fires per attack (cycling) with full damage.")]
        private Transform[] _extraFirePoints = Array.Empty<Transform>();

        [SerializeField]
        [Tooltip("Cycle one muzzle per attack (L→R→L…) with full damage. Off = simultaneous multi-muzzle fire " +
            "with damage split (Frost twin barrels).")]
        private bool _alternateFirePoints;

        private IAttackStrategy _attackStrategy;
        private GameObject _sourceObject;
        private float _damage;
        private float _attacksPerSecond;
        private float _cooldownTimer;
        private bool _isAttackEnabled = true;
        private VfxService _vfxService;
        private VfxDefinition _muzzleVfx;
        private int _alternateMuzzleIndex;
        private int _activeMuzzleCount = int.MaxValue;
        private float _critChance;
        private float _critDamage = 1f;

        /// <summary>Raised after a successful shot from a specific muzzle (visuals only — recoil, etc.).</summary>
        public event Action<Transform> FiredFromMuzzle;

        public void Initialize(IAttackStrategy attackStrategy, GameObject sourceObject)
        {
            _attackStrategy = attackStrategy;
            _sourceObject = sourceObject;
            _cooldownTimer = 0f;
            _alternateMuzzleIndex = 0;
        }

        /// <summary>Optional. Wires the muzzle flash VFX played on every successful attack.</summary>
        public void SetMuzzleVfx(VfxService vfxService, VfxDefinition muzzleVfx)
        {
            _vfxService = vfxService;
            _muzzleVfx = muzzleVfx;
        }

        /// <summary>Optional. Chance (0-1) that an attack is a critical hit and the multiplier it then deals (see
        /// DefenseMetaConfig). One roll per attack: every barrel of a volley shares it. Off (0) by default.</summary>
        public void SetCritical(float chance, float damageMultiplier)
        {
            _critChance = Mathf.Clamp01(chance);
            _critDamage = Mathf.Max(1f, damageMultiplier);
        }

        private float RollDamage()
        {
            return _critChance > 0f && UnityEngine.Random.value < _critChance ? _damage * _critDamage : _damage;
        }

        public void ApplyStats(float damage, float attacksPerSecond)
        {
            _damage = damage;
            _attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
        }

        /// <summary>How many of the tower's muzzles (fire point first, then the extras in order) may fire at its
        /// current level - see TowerLevelData.ActiveMuzzles. 1 = only the fire point's barrel; the others stay
        /// silent until a level that unlocks them.</summary>
        public void SetActiveMuzzleCount(int count)
        {
            count = Mathf.Max(1, count);
            if (_activeMuzzleCount == count)
            {
                return;
            }

            _activeMuzzleCount = count;
            _alternateMuzzleIndex = 0;
        }

        public void SetAttackEnabled(bool value)
        {
            _isAttackEnabled = value;
        }

        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("AlienDefense.Tower.Attack");

        public void Tick(float deltaTime, EnemyController target)
        {
            using (TickMarker.Auto())
            {
                TickCore(deltaTime, target);
            }
        }

        private void TickCore(float deltaTime, EnemyController target)
        {
            if (!_isAttackEnabled || target == null || _firePoint == null || _attackStrategy == null)
            {
                return;
            }

            _cooldownTimer -= deltaTime;
            if (_cooldownTimer > 0f)
            {
                return;
            }

            if (_alternateFirePoints)
            {
                TryAlternateAttack(target);
                return;
            }

            int muzzleCount = 1 + CountExtraFirePoints();
            var damageInfo = new DamageInfo(RollDamage() / muzzleCount, _sourceObject, target.AimPoint.position);
            if (!_attackStrategy.TryAttack(target, damageInfo, _firePoint))
            {
                return;
            }

            _cooldownTimer = 1f / _attacksPerSecond;
            PlayMuzzleFeedback(_firePoint);

            int extrasLeft = muzzleCount - 1;
            for (int i = 0; i < _extraFirePoints.Length && extrasLeft > 0; i++)
            {
                Transform muzzle = _extraFirePoints[i];
                if (muzzle == null)
                {
                    continue;
                }

                extrasLeft--;
                if (_attackStrategy.TryAttack(target, damageInfo, muzzle, isFollowUpShot: true))
                {
                    PlayMuzzleFeedback(muzzle);
                }
            }
        }

        private void TryAlternateAttack(EnemyController target)
        {
            Transform muzzle = ResolveAlternateMuzzle();
            if (muzzle == null)
            {
                return;
            }

            var damageInfo = new DamageInfo(RollDamage(), _sourceObject, target.AimPoint.position);
            if (!_attackStrategy.TryAttack(target, damageInfo, muzzle))
            {
                return;
            }

            _cooldownTimer = 1f / _attacksPerSecond;
            PlayMuzzleFeedback(muzzle);
            AdvanceAlternateMuzzle();
        }

        private void PlayMuzzleFeedback(Transform muzzle)
        {
            _vfxService?.Play(_muzzleVfx, muzzle.position, muzzle.rotation);
            FiredFromMuzzle?.Invoke(muzzle);
        }

        private Transform ResolveAlternateMuzzle()
        {
            int total = 1 + CountExtraFirePoints();
            if (total <= 0)
            {
                return null;
            }

            int index = _alternateMuzzleIndex % total;
            if (index < 0)
            {
                index += total;
            }

            if (index == 0)
            {
                return _firePoint;
            }

            int seen = 0;
            for (int i = 0; i < _extraFirePoints.Length; i++)
            {
                if (_extraFirePoints[i] == null)
                {
                    continue;
                }

                seen++;
                if (seen == index)
                {
                    return _extraFirePoints[i];
                }
            }

            return _firePoint;
        }

        private void AdvanceAlternateMuzzle()
        {
            int total = 1 + CountExtraFirePoints();
            if (total <= 0)
            {
                return;
            }

            _alternateMuzzleIndex = (_alternateMuzzleIndex + 1) % total;
        }

        /// <summary>Extra muzzles allowed to fire at the current level (never more than exist).</summary>
        private int CountExtraFirePoints()
        {
            int allowed = _activeMuzzleCount - 1;
            if (allowed <= 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _extraFirePoints.Length; i++)
            {
                if (_extraFirePoints[i] != null)
                {
                    count++;
                }
            }

            return Mathf.Min(count, allowed);
        }
    }
}
