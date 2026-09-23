using UnityEngine;
using UnityEngine.Pool;

namespace AlienDefense.Combat
{
    /// <summary>Typed object pool for one ProjectileController prefab.</summary>
    public sealed class ProjectilePool
    {
        private readonly ObjectPool<ProjectileController> _pool;
        private readonly ProjectileController _prefab;
        private readonly Transform _parent;

        public ProjectilePool(ProjectileController prefab, Transform parent, int defaultCapacity, int maxSize, bool collectionChecks)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<ProjectileController>(
                CreateProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyProjectile,
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

            var buffer = new ProjectileController[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        public ProjectileController Get()
        {
            return _pool.Get();
        }

        public void Release(ProjectileController projectile)
        {
            _pool.Release(projectile);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private ProjectileController CreateProjectile()
        {
            ProjectileController instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetProjectile(ProjectileController projectile)
        {
        }

        private void OnReleaseProjectile(ProjectileController projectile)
        {
            projectile.HandleReturnedToPool();
            projectile.gameObject.SetActive(false);
        }

        private void OnDestroyProjectile(ProjectileController projectile)
        {
            if (projectile != null)
            {
                Object.Destroy(projectile.gameObject);
            }
        }
    }
}
