using System;
using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>One-shot pooled visual effect: plays its ParticleSystems on spawn, auto-returns to pool after its lifetime.</summary>
    public sealed class PooledVfx : MonoBehaviour
    {
        [SerializeField]
        private ParticleSystem[] _particleSystems = Array.Empty<ParticleSystem>();

        [SerializeField]
        [Tooltip("Ignore the spawn rotation and always play upright (e.g. an explosion whose shockwave ring should " +
            "stay flat, whatever direction the projectile that triggered it was flying).")]
        private bool _keepWorldUpright;

        private Action<PooledVfx> _releaseToPool;
        private float _lifetime;
        private float _elapsed;
        private bool _isActive;
        private Vector3 _prefabScale;
        private bool _hasPrefabScale;

        /// <summary>Uniform size for this one playback (1 = the prefab's own scale). Always reset on release, so a
        /// scaled-up one-off - a boss explosion sized from the boss itself - never leaks into the next user of the
        /// pooled instance.</summary>
        public void SetScaleMultiplier(float multiplier)
        {
            CachePrefabScale();
            transform.localScale = _prefabScale * Mathf.Max(0.01f, multiplier);
        }

        public void Play(float lifetime, Action<PooledVfx> releaseToPool)
        {
            CachePrefabScale();
            _lifetime = Mathf.Max(0.05f, lifetime);
            _releaseToPool = releaseToPool;
            _elapsed = 0f;
            _isActive = true;

            if (_keepWorldUpright)
            {
                transform.rotation = Quaternion.identity;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                if (_particleSystems[i] != null)
                {
                    _particleSystems[i].Clear(true);
                    _particleSystems[i].Play(true);
                }
            }
        }

        /// <summary>Called by the owning pool when this instance is returned, including prewarm.</summary>
        public void HandleReturnedToPool()
        {
            _isActive = false;
            _elapsed = 0f;

            if (_hasPrefabScale)
            {
                transform.localScale = _prefabScale;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                if (_particleSystems[i] != null)
                {
                    _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void CachePrefabScale()
        {
            if (_hasPrefabScale)
            {
                return;
            }

            _prefabScale = transform.localScale;
            _hasPrefabScale = true;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                _isActive = false;
                _releaseToPool?.Invoke(this);
            }
        }
    }
}
