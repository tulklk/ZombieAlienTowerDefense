using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Shows a translucent hologram "ghost" of a Tower prefab on a BuildNode while the player has that
    /// tower type selected to build. Clones only the tower's clean visual-only child subtree (mesh renderers with
    /// no scripts/colliders anywhere inside — see FindVisualOnlySubtree) so nothing gameplay-related is ever
    /// duplicated: no per-component Disable() list to keep in sync as new tower types get added. Recolors via
    /// MaterialPropertyBlock, never renderer.material, so showing/hiding/recoloring the preview never allocates a
    /// new material instance. Owns no build/economy/placement logic — see BuildNode/BuildService for that.</summary>
    public sealed class TowerHologramPreview : MonoBehaviour
    {
        private static readonly int HologramColorId = Shader.PropertyToID("_HologramColor");
        private static readonly int FresnelColorId = Shader.PropertyToID("_FresnelColor");

        [SerializeField]
        [Tooltip("TowerPreviewRoot — the preview is parented here, at this transform's local origin.")]
        private Transform _previewRoot;

        [SerializeField]
        private Material _hologramMaterial;

        [Header("Placement fine-tune (use instead of touching the Tower model's import pivot)")]
        [SerializeField]
        private Vector3 _previewPositionOffset = new Vector3(0f, 0.02f, 0f);

        [SerializeField]
        private Vector3 _previewRotationOffset = Vector3.zero;

        [SerializeField]
        private Vector3 _previewScale = Vector3.one;

        [Header("Valid / Invalid colors (same shader — only _HologramColor/_FresnelColor change)")]
        [SerializeField]
        private Color _validColor = new Color(0f, 0.961f, 1f); // #00F5FF

        [SerializeField]
        private Color _invalidColor = new Color(1f, 0.227f, 0.227f); // #FF3A3A

        private GameObject _instance;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private GameObject _sourcePrefab;

        public bool IsShowing => _instance != null;

        /// <summary>Spawns (or re-poses, if already showing this exact prefab) the hologram. Pass the real Tower
        /// prefab's GameObject (e.g. TowerDefinition.Prefab.gameObject) — never the gameplay prefab's material is
        /// touched; only the cloned preview is.</summary>
        public void ShowPreview(GameObject towerPrefab)
        {
            if (towerPrefab == null)
            {
                HidePreview();
                return;
            }

            if (_instance != null && _sourcePrefab == towerPrefab)
            {
                return; // already showing this exact tower type — avoid a pointless destroy+respawn every frame
            }

            HidePreview();

            Transform visualRoot = FindVisualOnlySubtree(towerPrefab.transform);
            if (visualRoot == null)
            {
                Debug.LogWarning(
                    $"[TowerHologramPreview] '{towerPrefab.name}' has no child that is pure visuals (only " +
                    "Transform/MeshFilter/(Skinned)MeshRenderer, no scripts or colliders anywhere inside) — " +
                    "cannot build a safe preview from it. Every current Tower_*.prefab keeps its meshes under a " +
                    "single 'Model_...' child for exactly this reason.", this);
                return;
            }

            // Captured before Instantiate() copies it onto the clone: the model child's own authored scale
            // (e.g. Tower_Blaster's "Model_turret_1_1" is 0.2, not 1) — _previewScale is a fine-tune multiplier
            // on top of that, never a hard replacement, or the preview would come out ~5x too big/small.
            Vector3 sourceScale = visualRoot.localScale;

            _sourcePrefab = towerPrefab;
            _instance = Instantiate(visualRoot.gameObject, _previewRoot);
            _instance.name = "PreviewModel";
            _instance.transform.localPosition = _previewPositionOffset;
            _instance.transform.localRotation = Quaternion.Euler(_previewRotationOffset);
            _instance.transform.localScale = Vector3.Scale(sourceScale, _previewScale);

            // Defense in depth: the subtree above is already verified visual-only before cloning, so this should
            // normally find nothing — kept in case a future tower model nests something unexpected.
            StripAnyGameplayComponents(_instance);

            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            ApplyHologramMaterial();
            SetValid(true);
        }

        public void HidePreview()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
                _renderers = null;
                _sourcePrefab = null;
            }
        }

        /// <summary>Call once the real Tower has actually been built on this node. Functionally the same as
        /// HidePreview(); kept as its own method so a "build succeeded" call site reads as intentional rather
        /// than indistinguishable from a cancel.</summary>
        public void ConfirmBuild()
        {
            HidePreview();
        }

        /// <summary>Switches the hologram tint between the valid/invalid colors below without touching the shared
        /// material asset (MaterialPropertyBlock only).</summary>
        public void SetValid(bool valid)
        {
            SetColor(valid ? _validColor : _invalidColor);
        }

        public void SetColor(Color color)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                // Read-modify-write per renderer: MaterialPropertyBlock has no per-renderer memory of its own, so
                // skipping GetPropertyBlock would silently drop any other overrides a renderer might carry.
                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(HologramColorId, color);
                _propertyBlock.SetColor(FresnelColorId, color);
                r.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ApplyHologramMaterial()
        {
            if (_hologramMaterial == null || _renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                // sharedMaterials (not .materials) — assigning the array itself never clones a material instance.
                // Every slot gets the same hologram material; none of this project's tower meshes need an
                // exception today, but if one ever does, this is the one place to add a name/tag-based skip.
                var slots = r.sharedMaterials;
                for (int s = 0; s < slots.Length; s++)
                {
                    slots[s] = _hologramMaterial;
                }
                r.sharedMaterials = slots;
            }
        }

        /// <summary>Depth-first search for the first direct child whose entire subtree contains only
        /// Transform/MeshFilter/MeshRenderer/SkinnedMeshRenderer — no MonoBehaviour, Collider, Rigidbody, etc.
        /// This matches every Tower_*.prefab in this project (gameplay + collider on the root, a single
        /// script-free "Model_..." child holding the mesh hierarchy) without hardcoding that child's name, so a
        /// newly added tower type is picked up automatically as long as it follows the same convention.</summary>
        private static Transform FindVisualOnlySubtree(Transform towerRoot)
        {
            foreach (Transform child in towerRoot)
            {
                if (IsVisualOnly(child))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsVisualOnly(Transform root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void StripAnyGameplayComponents(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(rb);
            }

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Destroy(behaviour);
            }
        }

        private void OnDestroy()
        {
            HidePreview();
        }
    }
}
