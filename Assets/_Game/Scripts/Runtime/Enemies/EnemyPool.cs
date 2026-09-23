using UnityEngine;
using UnityEngine.Pool;

namespace AlienDefense.Enemies
{
    /// <summary>Typed object pool for one EnemyController prefab, backed by UnityEngine.Pool.ObjectPool.</summary>
    public sealed class EnemyPool
    {
        private readonly ObjectPool<EnemyController> _pool;
        private readonly EnemyController _prefab;
        private readonly Transform _parent;

        public EnemyPool(EnemyController prefab, Transform parent, int defaultCapacity, int maxSize, bool collectionChecks)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<EnemyController>(
                CreateEnemy,
                OnGetEnemy,
                OnReleaseEnemy,
                OnDestroyEnemy,
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

            var buffer = new EnemyController[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        public EnemyController Get()
        {
            return _pool.Get();
        }

        public void Release(EnemyController enemy)
        {
            _pool.Release(enemy);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private EnemyController CreateEnemy()
        {
            EnemyController instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetEnemy(EnemyController enemy)
        {
        }

        private void OnReleaseEnemy(EnemyController enemy)
        {
            enemy.HandleReturnedToPool();
            enemy.gameObject.SetActive(false);
        }

        private void OnDestroyEnemy(EnemyController enemy)
        {
            if (enemy != null)
            {
                Object.Destroy(enemy.gameObject);
            }
        }
    }
}
