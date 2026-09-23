using UnityEngine;
using UnityEngine.Pool;

namespace AlienDefense.Vfx
{
    /// <summary>Typed object pool for one PooledVfx prefab.</summary>
    public sealed class VfxPool
    {
        private readonly ObjectPool<PooledVfx> _pool;
        private readonly PooledVfx _prefab;
        private readonly Transform _parent;

        public VfxPool(PooledVfx prefab, Transform parent, int defaultCapacity, int maxSize, bool collectionChecks)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<PooledVfx>(
                CreateVfx,
                OnGetVfx,
                OnReleaseVfx,
                OnDestroyVfx,
                collectionChecks,
                Mathf.Max(1, defaultCapacity),
                Mathf.Max(1, maxSize));
        }

        /// <summary>How many instances are currently checked out of the pool. Read-only bookkeeping the pool already
        /// keeps (UnityEngine.Pool.ObjectPool) - used by the development performance monitor.</summary>
        public int ActiveCount => _pool.CountActive;

        public void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            var buffer = new PooledVfx[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        public PooledVfx Get()
        {
            return _pool.Get();
        }

        public void Release(PooledVfx vfx)
        {
            _pool.Release(vfx);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private PooledVfx CreateVfx()
        {
            PooledVfx instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetVfx(PooledVfx vfx)
        {
        }

        private void OnReleaseVfx(PooledVfx vfx)
        {
            vfx.HandleReturnedToPool();
            vfx.gameObject.SetActive(false);
        }

        private void OnDestroyVfx(PooledVfx vfx)
        {
            if (vfx != null)
            {
                Object.Destroy(vfx.gameObject);
            }
        }
    }
}
