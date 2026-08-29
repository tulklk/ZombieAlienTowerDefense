using UnityEngine;
using UnityEngine.Pool;

namespace AlienDefense.Pickups
{
    /// <summary>Typed object pool for the EnergyPickup prefab, backed by UnityEngine.Pool.ObjectPool. There is
    /// only ever one EnergyPickup prefab (unlike Enemies, which pool per-Definition), so this needs no registry
    /// keyed by type — one pool is enough.</summary>
    public sealed class EnergyPickupPool
    {
        private readonly ObjectPool<EnergyPickupController> _pool;
        private readonly EnergyPickupController _prefab;
        private readonly Transform _parent;

        public EnergyPickupPool(EnergyPickupController prefab, Transform parent, int defaultCapacity, int maxSize, bool collectionChecks)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<EnergyPickupController>(
                CreatePickup,
                OnGetPickup,
                OnReleasePickup,
                OnDestroyPickup,
                collectionChecks,
                Mathf.Max(1, defaultCapacity),
                Mathf.Max(1, maxSize));
        }

        public void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            var buffer = new EnergyPickupController[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        public EnergyPickupController Get()
        {
            return _pool.Get();
        }

        public void Release(EnergyPickupController pickup)
        {
            _pool.Release(pickup);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private EnergyPickupController CreatePickup()
        {
            EnergyPickupController instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetPickup(EnergyPickupController pickup)
        {
        }

        private void OnReleasePickup(EnergyPickupController pickup)
        {
            pickup.HandleReturnedToPool();
            pickup.gameObject.SetActive(false);
        }

        private void OnDestroyPickup(EnergyPickupController pickup)
        {
            if (pickup != null)
            {
                Object.Destroy(pickup.gameObject);
            }
        }
    }
}
