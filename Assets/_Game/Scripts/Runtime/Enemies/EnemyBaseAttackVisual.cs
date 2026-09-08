using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Optional. Plays an "Attack" Animator trigger when this enemy reaches PlayerBase instead of
    /// vanishing instantly, and reports how long one attack cycle takes - EnemyController repeats BaseDamage
    /// every Duration seconds for as long as this enemy stays alive (see EnemyController.AttackBaseRepeatedly),
    /// rather than a single hit. The Attack Animator state is expected to LOOP on its own (no back-transition to
    /// Walk) once triggered, since this enemy never walks again after reaching the base - PlayAttack only needs
    /// to fire the trigger once. If a Tower kills this enemy while it's attacking, EnemyHealth.Died resolves it
    /// as Defeated (see EnemyController.Resolve's _isResolved guard), which the repeating loop notices on its
    /// next wake and stops - so a dead zombie never keeps damaging the base. Enemies without this component keep
    /// the original instant single-hit resolve behavior (see EnemyController.HandleReachedBase).</summary>
    public sealed class EnemyBaseAttackVisual : MonoBehaviour
    {
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");

        [SerializeField]
        private Animator _animator;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds per attack cycle - BaseDamage is applied once per this duration for as long as this enemy stays alive at the base. Set to match the assigned (looping) Attack clip's own length.")]
        private float _attackHoldDuration = 1f;

        public float Duration => _attackHoldDuration;

        public void PlayAttack()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(AttackTrigger);
            }
        }
    }
}
