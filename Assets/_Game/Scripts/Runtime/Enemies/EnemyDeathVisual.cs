using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Optional. Plays a "Die" Animator trigger when this enemy is defeated and reports how long
    /// EnemyController should wait before releasing it back to the pool, so the animation is actually visible
    /// instead of the corpse vanishing the instant health hits zero. Enemies without this component (the
    /// original Capsule prefabs) keep releasing to the pool immediately — see EnemyController.Resolve.</summary>
    public sealed class EnemyDeathVisual : MonoBehaviour
    {
        private static readonly int DieTrigger = Animator.StringToHash("Die");

        [SerializeField]
        private Animator _animator;

        [SerializeField, Min(0f)]
        [Tooltip("How long to keep the corpse visible/animating before the enemy is returned to the pool.")]
        private float _deathHoldDuration = 1.2f;

        public float Duration => _deathHoldDuration;

        public void PlayDeath()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(DieTrigger);
            }
        }
    }
}
