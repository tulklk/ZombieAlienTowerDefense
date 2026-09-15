using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Lightweight decorative wander behaviour for background farm/ranch animals (cows, horses...) -
    /// picks a random point within a radius of its home position, walks there, idles a bit, repeats. Purely
    /// cosmetic: no gameplay interaction, no NavMesh (pens are small and flat, a direct-line walk reads fine
    /// at this scale), no pooling. Re-samples Terrain height every frame while moving so it stays glued to the
    /// ground even on the gentle pen-to-mountain slope transition.</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalWanderer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional. Auto-found via GetComponentInChildren if left empty.")]
        private Animator _animator;

        [SerializeField, Min(0f)]
        [Tooltip("How far from home this animal will wander. Keep smaller than the pen's fence radius.")]
        private float _wanderRadius = 3.5f;

        [SerializeField, Min(0.01f)]
        private float _walkSpeed = 1.1f;

        [SerializeField, Min(0f)]
        private float _rotationDegreesPerSecond = 200f;

        [SerializeField, Min(0f)]
        private float _minIdleSeconds = 2f;

        [SerializeField, Min(0f)]
        private float _maxIdleSeconds = 5f;

        [SerializeField]
        [Tooltip("Animator float parameter driving the Idle<->Walk transition. Left at 0 while idle, 1 while walking.")]
        private string _speedParameter = "Speed";

        private Vector3 _home;
        private Vector3 _target;
        private float _idleTimer;
        private bool _isWalking;
        private int _speedParamHash;
        private bool _hasSpeedParam;

        /// <summary>Editor/bulk-setup helper so callers (e.g. a pen-building script) can set the tunables right
        /// after AddComponent without going through SerializedObject.</summary>
        public void Configure(float wanderRadius, float walkSpeed)
        {
            _wanderRadius = wanderRadius;
            _walkSpeed = walkSpeed;
        }

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _speedParamHash = Animator.StringToHash(_speedParameter);
            _hasSpeedParam = _animator != null && HasParameter(_animator, _speedParamHash);
        }

        private void Start()
        {
            _home = transform.position;
            // Randomize the first idle so a whole pen of animals doesn't all start walking on the same frame.
            _idleTimer = Random.Range(0f, _maxIdleSeconds);
        }

        private void Update()
        {
            if (_isWalking)
            {
                TickWalk();
            }
            else
            {
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f)
                {
                    PickNewTarget();
                }
            }
        }

        private void TickWalk()
        {
            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.15f)
            {
                BeginIdle();
                return;
            }

            Vector3 direction = toTarget / Mathf.Max(distance, 0.0001f);
            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, _rotationDegreesPerSecond * Time.deltaTime);

            Vector3 move = direction * Mathf.Min(_walkSpeed * Time.deltaTime, distance);
            Vector3 newPosition = transform.position + move;
            newPosition.y = SampleGroundY(newPosition, transform.position.y);
            transform.position = newPosition;
        }

        private void BeginIdle()
        {
            _isWalking = false;
            _idleTimer = Random.Range(_minIdleSeconds, _maxIdleSeconds);
            SetSpeed(0f);
        }

        private void PickNewTarget()
        {
            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            _target = _home + new Vector3(offset.x, 0f, offset.y);
            _isWalking = true;
            SetSpeed(1f);
        }

        private void SetSpeed(float value)
        {
            if (_hasSpeedParam)
            {
                _animator.SetFloat(_speedParamHash, value);
            }
        }

        // Terrain.activeTerrains allocates a new array on every call - called per walking animal per frame it was the
        // game's only steady per-frame GC allocation. Cached once and shared; re-queried only if a tile goes away.
        private static Terrain[] s_terrains;

        private static float SampleGroundY(Vector3 worldPosition, float fallback)
        {
            if (s_terrains == null)
            {
                s_terrains = Terrain.activeTerrains;
            }

            Terrain[] terrains = s_terrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    s_terrains = null; // scene changed: fetch the current tiles next time
                    return fallback;
                }

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (worldPosition.x >= origin.x && worldPosition.x <= origin.x + size.x &&
                    worldPosition.z >= origin.z && worldPosition.z <= origin.z + size.z)
                {
                    return terrain.SampleHeight(new Vector3(worldPosition.x, 0f, worldPosition.z)) + origin.y;
                }
            }

            return fallback;
        }

        private static bool HasParameter(Animator animator, int hash)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
