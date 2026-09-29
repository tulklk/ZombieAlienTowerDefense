using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>One plot in the base world: the empty dirt plot, the scaffolding, and the building itself.
    ///
    /// Pure visual. It never reads the save or the clock - BaseWorldPresenter tells it which state and level to
    /// show. The building model is instantiated only when the level actually changes (not on every refresh), so a
    /// refresh fired by an unrelated currency change costs nothing here.</summary>
    public sealed class BaseBuildingView : MonoBehaviour
    {
        [SerializeField]
        private BaseBuildingDefinition _definition;

        [SerializeField]
        [Tooltip("The ConstructionVisualRoot (dirt, rope boundary, delivered materials). Shown only while the plot is " +
            "empty and no build has started - disable it and the plot reads as bare ground.")]
        private GameObject _emptyPlotRoot;

        [SerializeField]
        [Tooltip("The ConstructionSiteVisual (wooden scaffolding, materials, dust). Shown while a build or upgrade runs.")]
        private GameObject _scaffoldingRoot;

        [SerializeField]
        [Tooltip("Optional part of the construction site that fills the middle of the plot (foundation, mixer, " +
            "bricks). Only shown for a first build - during an upgrade the existing model stands there.")]
        private GameObject _constructionInteriorRoot;

        [SerializeField]
        [Tooltip("The level's model is spawned under this.")]
        private Transform _buildingRoot;

        [SerializeField]
        [Tooltip("Where the floating timer / level badge / bubble sits. Put it just above the roof.")]
        private Transform _indicatorAnchor;

        [SerializeField]
        [Tooltip("Receives taps. A box, never a MeshCollider (mobile).")]
        private BoxCollider _tapCollider;

        private GameObject _spawnedModel;
        private int _spawnedLevel = -1;
        private Tween _punch;
        private Tween _highlight;
        private Tween _sitePop;

        /// <summary>The construction site is fitted to the building's footprint with a non-uniform scale; the pop
        /// animation multiplies this instead of overwriting it.</summary>
        private Vector3 _siteBaseScale;
        private bool _siteBaseScaleCached;

        public BaseBuildingDefinition Definition => _definition;
        public Transform IndicatorAnchor => _indicatorAnchor != null ? _indicatorAnchor : transform;

        /// <summary>The empty-plot construction-site visual. Apply() already manages it; exposed for effects that
        /// want to hide it early (e.g. a build-start animation).</summary>
        public GameObject ConstructionVisualRoot => _emptyPlotRoot;

        public void Configure(BaseBuildingDefinition definition)
        {
            _definition = definition;
        }

        /// <summary>The single place that switches the plot's visual state:
        ///   Empty        - empty-plot visual on, construction site off, no model;
        ///   Constructing - empty-plot visual off, construction site on (with its interior on a first build);
        ///   Built        - model of the level on, everything else off.
        /// A building that is being upgraded keeps its current model visible inside the scaffolding.</summary>
        public void Apply(BaseBuildingState state, int level)
        {
            bool building = state == BaseBuildingState.Constructing || state == BaseBuildingState.Upgrading;
            bool hasModel = level > 0;

            if (_emptyPlotRoot != null && _emptyPlotRoot.activeSelf != (!hasModel && !building))
            {
                _emptyPlotRoot.SetActive(!hasModel && !building);
            }

            if (_scaffoldingRoot != null && _scaffoldingRoot.activeSelf != building)
            {
                _scaffoldingRoot.SetActive(building);
            }

            if (_constructionInteriorRoot != null && _constructionInteriorRoot.activeSelf != (building && !hasModel))
            {
                _constructionInteriorRoot.SetActive(building && !hasModel);
            }

            if (hasModel)
            {
                EnsureModel(level);
            }
            else if (_spawnedModel != null)
            {
                Destroy(_spawnedModel);
                _spawnedModel = null;
                _spawnedLevel = -1;
            }
        }

        private void EnsureModel(int level)
        {
            if (_spawnedLevel == level && _spawnedModel != null)
            {
                return;
            }

            GameObject prefab = ResolvePrefab(level);
            if (prefab == null)
            {
                return;
            }

            // Levels that share a prefab (only one model exists for now) keep the existing instance instead of
            // tearing it down and rebuilding the identical thing.
            if (_spawnedModel != null && _spawnedPrefab == prefab)
            {
                _spawnedLevel = level;
                return;
            }

            if (_spawnedModel != null)
            {
                Destroy(_spawnedModel);
            }

            Transform parent = _buildingRoot != null ? _buildingRoot : transform;
            _spawnedModel = Instantiate(prefab, parent);
            _spawnedModel.transform.localPosition = Vector3.zero;
            _spawnedModel.transform.localRotation = Quaternion.identity;
            _spawnedPrefab = prefab;
            _spawnedLevel = level;

            // The wrapper carries its own collider for the prefab workflow; the plot's collider is the one that
            // receives taps, so the model's copy is switched off to keep a single hit target per plot.
            foreach (Collider c in _spawnedModel.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            SetLayerRecursively(_spawnedModel, gameObject.layer);
        }

        private GameObject _spawnedPrefab;

        /// <summary>The level's own prefab, falling back to the highest lower level that has one. Keeps the
        /// architecture ready for per-level models while only one model exists per building.</summary>
        private GameObject ResolvePrefab(int level)
        {
            if (_definition == null)
            {
                return null;
            }

            for (int l = level; l >= 1; l--)
            {
                if (_definition.TryGetLevel(l, out BuildingLevelDefinition levelDefinition) &&
                    levelDefinition.VisualPrefab != null)
                {
                    return levelDefinition.VisualPrefab;
                }
            }

            return null;
        }

        /// <summary>The completed building pops in: 0.92 -> 1.05 -> 1.0 over ~0.35 s. Played for both ways a
        /// build can end (timer or Finish), because both arrive through the same completion event. Unscaled so it
        /// plays even behind a paused overlay.</summary>
        public void PlayCompletionPunch()
        {
            Transform target = _buildingRoot != null ? _buildingRoot : transform;
            _punch?.Kill(true);
            target.localScale = Vector3.one * 0.92f;
            _punch = DOTween.Sequence()
                .Append(target.DOScale(1.05f, 0.2f).SetEase(Ease.OutQuad))
                .Append(target.DOScale(1f, 0.15f).SetEase(Ease.InOutSine))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>The construction site pops up when a build starts: scale 0.9 -> 1.03 -> 1.0 over 0.3 s.
        /// Only called for a build started while the player watches - a site restored from the save just appears.</summary>
        public void PlayConstructionStart()
        {
            if (_scaffoldingRoot == null)
            {
                return;
            }

            Transform site = _scaffoldingRoot.transform;
            if (!_siteBaseScaleCached)
            {
                _siteBaseScale = site.localScale;
                _siteBaseScaleCached = true;
            }

            if (!_scaffoldingRoot.activeSelf)
            {
                _scaffoldingRoot.SetActive(true);
            }

            _sitePop?.Kill(true);
            site.localScale = _siteBaseScale * 0.9f;
            _sitePop = DOTween.Sequence()
                .Append(site.DOScale(_siteBaseScale * 1.03f, 0.2f).SetEase(Ease.OutQuad))
                .Append(site.DOScale(_siteBaseScale, 0.1f).SetEase(Ease.InOutSine))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>Brief pulse used when the camera focuses this plot from a requirement's GO button.</summary>
        public void PlayHighlight()
        {
            Transform target = _buildingRoot != null ? _buildingRoot : transform;
            _highlight?.Kill(true);
            target.localScale = Vector3.one;
            _highlight = target.DOScale(1.08f, 0.25f)
                .SetLoops(4, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _punch?.Kill();
            _highlight?.Kill();
            _sitePop?.Kill();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
