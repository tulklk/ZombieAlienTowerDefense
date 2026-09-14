using DG.Tweening;
using TMPro;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Per-BuildNode display driven by PlayerBuildNodeProximityController: the ground ring (a single
    /// mesh using AlienDefense/RingIndicator - see that shader's doc comment) that stays visible as a "you can
    /// act here" border whenever this node is channelable, and sweeps a brighter fill color clockwise while the
    /// UFO channels here; plus a world-space "{deposited}/{cost}" Energy Ball badge shown whenever this node
    /// still needs Energy for its next action (build if Available, upgrade if Occupied and not max level -
    /// hidden once maxed), with a bobbing green arrow beside it when the player carries enough to finish paying.
    /// Pure display - never reads BuildNode/EnergyWallet state itself.</summary>
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
        [Tooltip("Optional. Green 'ready' arrow placed right after the cost text; shown only when the player can " +
            "finish paying for this node's build/upgrade.")]
        private RectTransform _affordableArrow;

        [SerializeField]
        [Tooltip("Optional. The arrow's image child that bobs up and down (the arrow root itself is re-positioned " +
            "after the text every refresh).")]
        private RectTransform _affordableArrowGraphic;

        [SerializeField]
        private float _arrowSpacing = 6f;

        [SerializeField, Min(0f)]
        private float _arrowBobDistance = 10f;

        [SerializeField, Min(0.05f)]
        private float _arrowBobDuration = 0.45f;

        [SerializeField]
        [Tooltip("Keeps the cost badge's World Space canvas facing the fixed isometric camera - without it the " +
            "text renders edge-on and unreadable, since the canvas plane is otherwise vertical. The ground ring " +
            "itself lies flat and never needs this.")]
        private WorldSpaceBillboard _billboard;

        private MaterialPropertyBlock _ringBlock;
        private Tween _arrowBob;
        private Vector2 _arrowGraphicRest;
        private bool _arrowRestCached;

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

        /// <param name="deposited">Energy already paid into this node.</param>
        /// <param name="cost">0 hides the badge entirely (e.g. the node is already at max level).</param>
        /// <param name="canComplete">Deposit plus what the player carries covers the cost - shows the arrow.</param>
        public void SetCost(int deposited, int cost, bool canComplete)
        {
            bool show = cost > 0;
            if (_costBadgeRoot != null)
            {
                _costBadgeRoot.SetActive(show);
            }

            if (_costText != null && show)
            {
                _costText.text = Mathf.Min(deposited, cost) + "/" + cost;
            }

            SetArrowVisible(show && canComplete);
        }

        private void SetArrowVisible(bool visible)
        {
            if (_affordableArrow == null)
            {
                return;
            }

            if (visible && _costText != null)
            {
                // Directly after the text's actual glyphs, not its (wider) rect.
                RectTransform textRect = _costText.rectTransform;
                float textWidth = _costText.GetPreferredValues(_costText.text).x;
                Vector2 position = _affordableArrow.anchoredPosition;
                float textLeft = textRect.anchoredPosition.x - textRect.pivot.x * textRect.rect.width;
                position.x = textLeft + textWidth + _arrowSpacing;
                _affordableArrow.anchoredPosition = position;
            }

            if (_affordableArrow.gameObject.activeSelf != visible)
            {
                _affordableArrow.gameObject.SetActive(visible);
            }

            if (visible)
            {
                StartArrowBob();
            }
            else
            {
                StopArrowBob();
            }
        }

        private void StartArrowBob()
        {
            if (_affordableArrowGraphic == null || _arrowBobDistance <= 0f || (_arrowBob != null && _arrowBob.IsActive()))
            {
                return;
            }

            if (!_arrowRestCached)
            {
                _arrowGraphicRest = _affordableArrowGraphic.anchoredPosition;
                _arrowRestCached = true;
            }

            _affordableArrowGraphic.anchoredPosition = _arrowGraphicRest;
            RectTransform graphic = _affordableArrowGraphic;
            float y = _arrowGraphicRest.y;
            _arrowBob = DOTween.To(() => y, value =>
                {
                    y = value;
                    graphic.anchoredPosition = new Vector2(_arrowGraphicRest.x, value);
                }, _arrowGraphicRest.y + _arrowBobDistance, _arrowBobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(graphic.gameObject);
        }

        private void StopArrowBob()
        {
            if (_arrowBob != null)
            {
                _arrowBob.Kill();
                _arrowBob = null;
            }

            if (_affordableArrowGraphic != null && _arrowRestCached)
            {
                _affordableArrowGraphic.anchoredPosition = _arrowGraphicRest;
            }
        }

        private void OnDestroy()
        {
            _arrowBob?.Kill();
        }
    }
}
