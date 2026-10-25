using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>No longer driven by EnemyController: every enemy now deals its BaseDamage once on reaching the
    /// base and disappears at once (see EnemyController.HandleReachedBase), with no attack animation. The
    /// component stays on the Enemy_* prefabs (added by ZombieVariantBuilder) so they keep loading cleanly, and
    /// PlayAttack still works if something wants to trigger the Animator's "Attack" state explicitly.</summary>
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
