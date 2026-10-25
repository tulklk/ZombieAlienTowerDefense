using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Background traffic (a scooter, a farm vehicle) that drives a fixed loop of points along a dirt road,
    /// now and then parking for a while at a stop. Purely cosmetic: no physics, no NavMesh, no gameplay interaction.
    /// The route is stored in world space on the component (no child Transforms to keep alive) and the height follows
    /// the Terrain; a parked vehicle costs one comparison per frame.</summary>
    [DisallowMultipleComponent]
    public sealed class AmbientRouteMover : MonoBehaviour
    {
        [SerializeField]
        private Vector3[] _route = new Vector3[0];

        [SerializeField, Min(0.1f)]
        private float _speed = 3f;

        [SerializeField, Min(1f)]
        private float _turnDegreesPerSecond = 160f;

        [SerializeField]
        [Tooltip("Indices of route points where the vehicle parks before continuing.")]
        private int[] _stopIndices = new int[0];

        [SerializeField, Min(0f)]
        private float _minStopSeconds = 4f;

        [SerializeField, Min(0f)]
        private float _maxStopSeconds = 10f;

        [SerializeField]
        private float _heightOffset;

        private int _target = 1;
        private float _parkedFor;
        private static Terrain[] s_terrains;

        public void Configure(Vector3[] route, float speed, int[] stopIndices, float heightOffset)
        {
            _route = route;
            _speed = speed;
            _stopIndices = stopIndices;
            _heightOffset = heightOffset;
        }

        private void OnEnable()
        {
            if (_route == null || _route.Length < 2)
            {
                enabled = false;
                return;
            }

            transform.position = Grounded(_route[0]);
            _target = 1;
        }

        private void Update()
        {
            if (_parkedFor > 0f)
            {
                _parkedFor -= Time.deltaTime;
                return;
            }

            Vector3 position = transform.position;
            Vector3 goal = _route[_target];
            Vector3 flat = new Vector3(goal.x - position.x, 0f, goal.z - position.z);
            float distance = flat.magnitude;
            float step = _speed * Time.deltaTime;

            if (distance <= step)
            {
                transform.position = Grounded(goal);
                if (System.Array.IndexOf(_stopIndices, _target) >= 0)
                {
                    _parkedFor = Random.Range(_minStopSeconds, _maxStopSeconds);
                }

                _target = (_target + 1) % _route.Length;
                return;
            }

            Vector3 direction = flat / distance;
            transform.position = Grounded(position + direction * step);
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, _turnDegreesPerSecond * Time.deltaTime);
        }

        private Vector3 Grounded(Vector3 point)
        {
            if (s_terrains == null || s_terrains.Length == 0)
            {
                s_terrains = Terrain.activeTerrains; // allocates: fetched once, shared by every mover
            }

            for (int i = 0; i < s_terrains.Length; i++)
            {
                Terrain terrain = s_terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= origin.x && point.x <= origin.x + size.x && point.z >= origin.z && point.z <= origin.z + size.z)
                {
                    point.y = origin.y + terrain.SampleHeight(point) + _heightOffset;
                    return point;
                }
            }

            return point;
        }

        private void OnDestroy()
        {
            s_terrains = null;
        }
    }
}
