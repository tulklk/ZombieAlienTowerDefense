using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Sets the visual model's Animator.speed once at spawn. Exists because Animator.speed is a
    /// runtime-only property — it has no Inspector field and is NOT part of a prefab's serialized data (confirmed
    /// directly: setting it via script and saving the prefab asset silently resets back to 1 on reload) — so it
    /// can't just be baked into the Enemy_* prefabs like every other value here. Needed at all because these
    /// zombies' Mixamo walk clips have no baked root motion to match against move speed (EnemyMovement drives
    /// world position entirely on its own, via EnemyDefinition.MoveSpeed): the clip's own natural stride pace
    /// (measured directly per variant, from how far a planted foot swings per loop) is far slower than a
    /// playable MoveSpeed, so the legs must cycle faster than 1x to avoid visibly sliding down the path.</summary>
    public sealed class EnemyAnimatorSpeed : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        [SerializeField, Min(0.01f)]
        [Tooltip("MoveSpeed / (the assigned zombie variant's own natural walk-stride speed) — see " +
            "ZombieVariantBuilder/conversation notes for how this was measured per variant.")]
        private float _speedMultiplier = 1f;

        private void Awake()
        {
            if (_animator != null)
            {
                _animator.speed = _speedMultiplier;
            }
        }
    }
}
