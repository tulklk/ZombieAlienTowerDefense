using System;
using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Common;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.Vfx
{
    /// <summary>The Frost Tower's Ice Stun: a few tall faceted crystals burst out of the ground around the enemy - big
    /// ones to its left and right, a shorter one behind, a small one in front - leaving the enemy itself visible between
    /// them, plus a frost patch, a little cold mist and tiny shards. Visual only: EnemyStatusController tells it the stun
    /// started (and for how long) and it plays the whole thing - grow, hold, shatter - inside that time.
    ///
    /// Crystals are placed relative to the gameplay camera, not the enemy's facing, so "left/right/behind/in front"
    /// always read the same way on screen and the front one never hides the enemy. Everything is fitted to the enemy's
    /// rendered body each time, so a Runner and the Boss each get crystals their own size.
    ///
    /// Hierarchy built on Attach (procedural meshes shared by every enemy - see IceCrystalMeshes):
    ///   Vfx_IceStun (this, under the body's centre on the ground)
    ///   ├── Crystal_Right / Crystal_Left / Crystal_Back / Crystal_Front (/ Crystal_BackLeft on large enemies)
    ///   ├── FrostGround
    ///   ├── SmallShards
    ///   └── ColdMist</summary>
    public sealed class IceStunVisual : MonoBehaviour, IStatusEffectVisual
    {
        [Serializable]
        public struct CrystalSlot
        {
            public string Name;

            [Tooltip("Degrees round the enemy as seen by the camera: 0 = towards the camera, 90 = screen right, " +
                "-90 = screen left, 180 = behind.")]
            public float Angle;

            [Tooltip("Distance of the crystal's base from the enemy's centre, x body width.")]
            public float Distance;

            [Tooltip("Crystal height, x body height.")]
            public float Height;

            [Tooltip("Crystal diameter, x body width.")]
            public float Width;

            [Tooltip("Degrees the crystal leans away from the enemy.")]
            public float Tilt;

            [Tooltip("Only grown on enemies at least Large Enemy Height tall.")]
            public bool LargeEnemiesOnly;
        }

        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float GroundOpacity = 0.6f;

        [Header("Materials")]
        [SerializeField]
        [Tooltip("MAT_IceCrystal - crystals, frost patch and shards.")]
        private Material _crystalMaterial;

        [SerializeField]
        [Tooltip("Soft mist sprite for ColdMist (IceMist).")]
        private Material _mistMaterial;

        [Header("Layout (relative to the enemy's rendered body)")]
        [SerializeField]
        private CrystalSlot[] _slots =
        {
            new CrystalSlot { Name = "Crystal_Right", Angle = 75f, Distance = 0.58f, Height = 1.15f, Width = 0.44f, Tilt = 7f },
            new CrystalSlot { Name = "Crystal_Left", Angle = -80f, Distance = 0.58f, Height = 0.9f, Width = 0.4f, Tilt = 9f },
            new CrystalSlot { Name = "Crystal_Back", Angle = 170f, Distance = 0.48f, Height = 0.75f, Width = 0.36f, Tilt = 5f },
            new CrystalSlot { Name = "Crystal_Front", Angle = 35f, Distance = 0.66f, Height = 0.55f, Width = 0.28f, Tilt = 14f },
            new CrystalSlot { Name = "Crystal_BackLeft", Angle = -150f, Distance = 0.52f, Height = 0.6f, Width = 0.3f, Tilt = 10f, LargeEnemiesOnly = true },
        };

        [SerializeField, Min(0f)]
        [Tooltip("Bodies at least this tall (m) also get the LargeEnemiesOnly crystals.")]
        private float _largeEnemyHeight = 3f;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Large enemies (the Boss): crystal height/width and the frost patch radius are scaled by this, so a 5 m body " +
                 "doesn't get 6 m spires that swallow the screen.")]
        private float _largeEnemySizeScale = 0.45f;

        [SerializeField, Range(0.3f, 1f)]
        [Tooltip("Large enemies: crystal distance from the body is scaled by this (their render bounds are loose).")]
        private float _largeEnemySpreadScale = 0.7f;

        [SerializeField, Min(0.5f)]
        [Tooltip("FrostGround radius, x body radius.")]
        private float _groundRadius = 1.1f;

        [Header("Timing (all inside the stun's own duration)")]
        [SerializeField, Min(0.05f)]
        private float _growDuration = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra delay per crystal, so they burst up one after another.")]
        private float _growStagger = 0.04f;

        [SerializeField, Min(0.05f)]
        [Tooltip("The crystals start cracking/fading this long before the stun ends.")]
        private float _shatterLead = 0.25f;

        [SerializeField, Min(0.05f)]
        private float _shatterDuration = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("The shard burst fires this long before the stun ends.")]
        private float _shardsLead = 0.15f;

        [Header("Small shards")]
        [SerializeField, Min(0)]
        private int _growShardCount = 6;

        [SerializeField, Min(0)]
        private int _shatterShardCount = 10;

        private static Terrain[] s_terrains;

        private Transform _owner;
        private Renderer[] _bodyRenderers = new Renderer[0];
        private Camera _camera;

        private readonly List<Transform> _crystals = new List<Transform>();
        private readonly List<MeshRenderer> _crystalRenderers = new List<MeshRenderer>();
        private Vector3[] _crystalScales = new Vector3[0];
        private bool[] _crystalActive = new bool[0];
        private Transform _ground;
        private MeshRenderer _groundRenderer;
        private Vector3 _groundScale = Vector3.one;
        private ParticleSystem _mist;
        private ParticleSystem _shards;
        private MaterialPropertyBlock _crystalBlock;
        private MaterialPropertyBlock _groundBlock;
        private Sequence _sequence;
        private float _alpha = 1f;
        private bool _isShowing;
        private bool _isShattering;
        private bool _isBuilt;

        // ---------------------------------------------------------------------------------------------------------
        // IStatusEffectVisual
        // ---------------------------------------------------------------------------------------------------------

        public void Attach(Transform owner)
        {
            _owner = owner;
            Build();

            // The body is what is inside VisualRoot; never the health bar, target point or this VFX.
            Transform visualRoot = owner.Find("VisualRoot");
            var renderers = new List<Renderer>();
            foreach (Renderer candidate in (visualRoot != null ? visualRoot : owner).GetComponentsInChildren<Renderer>(true))
            {
                if (candidate is ParticleSystemRenderer || candidate is TrailRenderer || candidate is LineRenderer ||
                    candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                renderers.Add(candidate);
            }

            _bodyRenderers = renderers.ToArray();
            ResetVisual();
        }

        public void PlayApply(float duration)
        {
            if (!_isBuilt)
            {
                return;
            }

            KillSequence();
            Fit();
            SetAlpha(1f);
            _isShowing = true;
            _isShattering = false;

            int grown = 0;
            _sequence = DOTween.Sequence().SetLink(gameObject);
            for (int i = 0; i < _crystals.Count; i++)
            {
                Transform crystal = _crystals[i];
                crystal.gameObject.SetActive(_crystalActive[i]);
                if (!_crystalActive[i])
                {
                    continue;
                }

                Vector3 full = _crystalScales[i];
                crystal.localScale = new Vector3(full.x, 0.001f, full.z); // pivot on the ground: grows upwards only
                _sequence.Insert(grown * _growStagger, crystal.DOScaleY(full.y, _growDuration).SetEase(Ease.OutCubic));
                grown++;
            }

            _ground.gameObject.SetActive(true);
            _ground.localScale = _groundScale * 0.3f;
            _sequence.Insert(0f, _ground.DOScale(_groundScale, 0.15f).SetEase(Ease.OutQuad));

            _shards.Emit(_growShardCount);
            _mist.Play();

            // Grow, hold, then crack and fade so that it has all gone exactly when the stun ends.
            float grownAt = Mathf.Max(0, grown - 1) * _growStagger + _growDuration;
            float shatterAt = Mathf.Max(grownAt, duration - _shatterLead);
            float endAt = Mathf.Max(shatterAt + _shatterDuration, duration);
            _sequence.InsertCallback(shatterAt, BeginShatter);
            int order = 0;
            for (int i = 0; i < _crystals.Count; i++)
            {
                if (!_crystalActive[i])
                {
                    continue;
                }

                Vector3 full = _crystalScales[i];
                _sequence.Insert(shatterAt + order * 0.02f, _crystals[i]
                    .DOScale(new Vector3(full.x * 0.8f, full.y * 0.55f, full.z * 0.8f), _shatterDuration)
                    .SetEase(Ease.InQuad));
                order++;
            }

            _sequence.Insert(shatterAt, DOTween.To(() => _alpha, SetAlpha, 0f, _shatterDuration).SetEase(Ease.InQuad));
            _sequence.InsertCallback(Mathf.Clamp(duration - _shardsLead, shatterAt, endAt), BurstShards);
            _sequence.InsertCallback(endAt, HideParts);
        }

        public void PlayExpire()
        {
            // Normally the crystals have already shattered by the time the stun ends; only cut it short when the
            // enemy died (or the stun was removed) with the ice still standing.
            if (!_isBuilt || !_isShowing || _isShattering)
            {
                return;
            }

            KillSequence();
            BeginShatter();
            BurstShards();
            _sequence = DOTween.Sequence().SetLink(gameObject);
            for (int i = 0; i < _crystals.Count; i++)
            {
                if (_crystalActive[i])
                {
                    Vector3 current = _crystals[i].localScale;
                    _sequence.Insert(0f, _crystals[i].DOScale(new Vector3(current.x * 0.8f, current.y * 0.55f, current.z * 0.8f), 0.12f));
                }
            }

            _sequence.Insert(0f, DOTween.To(() => _alpha, SetAlpha, 0f, 0.12f));
            _sequence.OnComplete(HideParts);
        }

        public void ResetVisual()
        {
            if (!_isBuilt)
            {
                return;
            }

            KillSequence();
            _shards.Clear(true);
            HideParts();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Timeline steps
        // ---------------------------------------------------------------------------------------------------------

        private void BeginShatter()
        {
            _isShattering = true;
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void BurstShards()
        {
            _shards.Emit(_shatterShardCount);
        }

        private void HideParts()
        {
            _isShowing = false;
            _isShattering = false;
            _mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < _crystals.Count; i++)
            {
                _crystals[i].gameObject.SetActive(false);
            }

            _ground.gameObject.SetActive(false);
            SetAlpha(1f);
        }

        private void SetAlpha(float value)
        {
            _alpha = value;
            _crystalBlock.SetFloat(AlphaId, value);
            for (int i = 0; i < _crystalRenderers.Count; i++)
            {
                _crystalRenderers[i].SetPropertyBlock(_crystalBlock);
            }

            _groundBlock.SetFloat(AlphaId, value * GroundOpacity);
            _groundRenderer.SetPropertyBlock(_groundBlock);
        }

        private void KillSequence()
        {
            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Fit
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Measures the body as it stands now and lays the crystals out around it, bases on the ground.</summary>
        private void Fit()
        {
            Bounds body = MeasureBody();
            float bodyTop = Mathf.Max(0.5f, body.max.y); // enemies stand on their own origin
            float bodyWidth = Mathf.Max(0.4f, (body.size.x + body.size.z) * 0.5f);
            float bodyRadius = bodyWidth * 0.5f;
            bool large = bodyTop >= _largeEnemyHeight;
            float sizeScale = large ? _largeEnemySizeScale : 1f;
            float spreadScale = large ? _largeEnemySpreadScale : 1f;

            transform.localPosition = new Vector3(body.center.x, 0f, body.center.z);
            transform.localRotation = Quaternion.identity;

            // Screen-relative directions, flattened onto the ground.
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            Vector3 towardsCamera = _camera != null ? -_camera.transform.forward : -_owner.forward;
            Vector3 screenRight = _camera != null ? _camera.transform.right : _owner.right;
            towardsCamera.y = 0f;
            screenRight.y = 0f;
            towardsCamera = towardsCamera.sqrMagnitude > 0.0001f ? towardsCamera.normalized : Vector3.back;
            screenRight = screenRight.sqrMagnitude > 0.0001f ? screenRight.normalized : Vector3.right;

            float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            if (s_terrains == null || s_terrains.Length == 0 || s_terrains[0] == null)
            {
                s_terrains = Terrain.activeTerrains; // allocates: fetched once, not per stun
            }

            Terrain[] terrains = s_terrains;
            int seed = Mathf.Abs(GetInstanceID());

            for (int i = 0; i < _crystals.Count; i++)
            {
                CrystalSlot slot = _slots[i];
                _crystalActive[i] = !slot.LargeEnemiesOnly || large;
                if (!_crystalActive[i])
                {
                    continue;
                }

                float angle = slot.Angle * Mathf.Deg2Rad;
                Vector3 outward = (towardsCamera * Mathf.Cos(angle) + screenRight * Mathf.Sin(angle)).normalized;
                float height = slot.Height * bodyTop * sizeScale;

                Vector3 position = transform.position + outward * (slot.Distance * bodyWidth * spreadScale);
                // Sunk a touch into the ground so a slope never leaves the base floating.
                position.y = GroundHeight(terrains, position, transform.position.y) - height * 0.04f;

                float tilt = slot.Tilt * Mathf.Deg2Rad;
                Vector3 leanedUp = Vector3.up * Mathf.Cos(tilt) + outward * Mathf.Sin(tilt);
                Quaternion yaw = Quaternion.Euler(0f, (seed * 37 + i * 97) % 360, 0f);

                Transform crystal = _crystals[i];
                crystal.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, leanedUp) * yaw);
                float radius = slot.Width * bodyWidth * 0.5f * sizeScale;
                _crystalScales[i] = new Vector3(radius, height, radius) / parentScale;
            }

            _ground.localPosition = Vector3.zero;
            float groundRadius = bodyRadius * _groundRadius * sizeScale;
            _groundScale = new Vector3(groundRadius, Mathf.Clamp(bodyRadius * sizeScale, 0.5f, 2f), groundRadius) / parentScale;

            ParticleSystem.ShapeModule mistShape = _mist.shape;
            mistShape.radius = bodyRadius * spreadScale;
            ParticleSystem.ShapeModule shardShape = _shards.shape;
            shardShape.radius = bodyRadius * 0.7f * spreadScale;
            shardShape.position = new Vector3(0f, bodyTop * 0.3f, 0f);
        }

        private Bounds MeasureBody()
        {
            Bounds body = default;
            bool hasBody = false;
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                Renderer bodyRenderer = _bodyRenderers[i];
                if (bodyRenderer == null || !bodyRenderer.enabled || !bodyRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds world = bodyRenderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 local = _owner.InverseTransformPoint(world.center + Vector3.Scale(world.extents, sign));
                    if (!hasBody)
                    {
                        body = new Bounds(local, Vector3.zero);
                        hasBody = true;
                    }
                    else
                    {
                        body.Encapsulate(local);
                    }
                }
            }

            return hasBody ? body : new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 0.8f));
        }

        private static float GroundHeight(Terrain[] terrains, Vector3 position, float fallback)
        {
            float height = float.MinValue;
            bool found = false;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (position.x < origin.x || position.x > origin.x + size.x || position.z < origin.z || position.z > origin.z + size.z)
                {
                    continue;
                }

                height = origin.y + terrain.SampleHeight(position);
                found = true;
                break;
            }

            if (WalkableSurface.TryGetHeight(position, out float surface) && (!found || surface > height))
            {
                return surface;
            }

            return found ? height : fallback;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Construction
        // ---------------------------------------------------------------------------------------------------------

        private void Build()
        {
            if (_isBuilt)
            {
                return;
            }

            _isBuilt = true;
            _crystalBlock = new MaterialPropertyBlock();
            _groundBlock = new MaterialPropertyBlock();
            _groundBlock.SetColor(BaseColorId, new Color(0.8f, 0.95f, 1f, 0.25f)); // a faint frost film under the feet

            int seed = Mathf.Abs(GetInstanceID());
            for (int i = 0; i < _slots.Length; i++)
            {
                Transform crystal = CreateChild(string.IsNullOrEmpty(_slots[i].Name) ? "Crystal_" + i : _slots[i].Name);
                _crystalRenderers.Add(AddMeshRenderer(crystal.gameObject, IceCrystalMeshes.Crystal(seed + i)));
                _crystals.Add(crystal);
            }

            _crystalScales = new Vector3[_slots.Length];
            _crystalActive = new bool[_slots.Length];

            _ground = CreateChild("FrostGround");
            _groundRenderer = AddMeshRenderer(_ground.gameObject, IceCrystalMeshes.FrostPatch());

            _shards = CreateShards();
            _mist = CreateMist();
        }

        private Transform CreateChild(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }

        private MeshRenderer AddMeshRenderer(GameObject target, Mesh mesh)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _crystalMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return meshRenderer;
        }

        private ParticleSystem CreateShards()
        {
            var go = new GameObject("SmallShards");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // only ever Emit()ted in small bursts

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // dome up

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = IceCrystalMeshes.Shard();
            particleRenderer.alignment = ParticleSystemRenderSpace.World;
            particleRenderer.sharedMaterial = _crystalMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            go.SetActive(true);
            return ps;
        }

        private ParticleSystem CreateMist()
        {
            var go = new GameObject("ColdMist");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new Color(0.85f, 0.97f, 1f, 0.35f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 6f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f); // flat on the ground
            shape.position = new Vector3(0f, 0.08f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = _mistMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            go.SetActive(true);
            return ps;
        }
    }
}
