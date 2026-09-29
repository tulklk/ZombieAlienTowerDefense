using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>The one water height for the whole river. Boats, ducks and ripples read it from here, so the river
    /// can be raised or lowered by moving this single transform.</summary>
    public sealed class RiverWaterLevel : MonoBehaviour
    {
        public float Level => transform.position.y;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(20f, 0f, 20f));
        }
#endif
    }
}
