using TMPro;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Per-BuildNode display driven by PlayerBuildNodeProximityController: the ground ring (a single
    /// mesh using AlienDefense/RingIndicator - see that shader's doc comment) that stays visible as a "you can
    /// act here" border whenever this node is channelable, and sweeps a brighter fill color clockwise while the
    /// UFO channels here; plus a world-space "{wallet}/{cost}" Energy Ball cost badge shown whenever this node
    /// still needs Energy for its next action (build if Available, upgrade if Occupied and not max level -
    /// hidden once maxed). Pure display - never reads BuildNode/EnergyWallet state itself.</summary>
    public sealed class BuildNodeChannelUI : MonoBehaviour
    {
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        [SerializeField]
        [Tooltip("The ground ring mesh (AlienDefense/RingIndicator material). Its own GameObject active state " +
            "is the ring's visibility - see SetRingVisible - independent of _FillAmount.")]
        private MeshRenderer _ringRenderer;

        [SerializeField]
        private GameObject _costBadgeRoot;

        [SerializeField]
        private TMP_Text _costText;

        [SerializeField]
        [Tooltip("Keeps the cost badge's World Space canvas facing the fixed isometric camera - without it the " +
            "text renders edge-on and unreadable, since the canvas plane is otherwise vertical. The ground ring " +
            "itself lies flat and never needs this.")]
        private WorldSpaceBillboard _billboard;

        private MaterialPropertyBlock _ringBlock;

        /// <summary>Intended caller: LevelCompositionRoot, once, with the same camera transform passed to every
        /// other world-space billboard (enemy health bars, etc).</summary>
        public void Initialize(Transform cameraTransform)
        {
            if (_billboard != null)
            {
                _billboard.Initialize(cameraTransform);
            }
        }

        /// <summary>Whether this node's ring shows at all - true for the whole time it's channelable (Available,
        /// or Occupied and not max level), independent of whether the UFO is actually here channeling right now.
        /// Intended caller: PlayerBuildNodeProximityController, once per relevant state change.</summary>
        public void SetRingVisible(bool visible)
        {
            if (_ringRenderer != null && _ringRenderer.gameObject.activeSelf != visible)
            {
                _ringRenderer.gameObject.SetActive(visible);
            }
        }

        /// <param name="normalized01">0 = nothing filled yet, 1 = channel complete. Never touches the ring's own
        /// visibility - see SetRingVisible.</param>
        public void SetChannelProgress(float normalized01)
        {
            if (_ringRenderer == null)
            {
                return;
            }

            _ringBlock ??= new MaterialPropertyBlock();
            _ringRenderer.GetPropertyBlock(_ringBlock);
            _ringBlock.SetFloat(FillAmountId, Mathf.Clamp01(normalized01));
            _ringRenderer.SetPropertyBlock(_ringBlock);
        }

        /// <param name="cost">0 hides the badge entirely (e.g. the node is already at max level).</param>
        public void SetCost(int currentWallet, int cost)
        {
            bool show = cost > 0;
            if (_costBadgeRoot != null)
            {
                _costBadgeRoot.SetActive(show);
            }

            if (_costText != null && show)
            {
                _costText.text = currentWallet + "/" + cost;
            }
        }
    }
}
