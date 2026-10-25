using System;
using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.CameraSystem;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Waves;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Level
{
    /// <summary>Boss introduction cinematic. WaveController spawns the boss group frozen (this controller asks it
    /// to hold); this then locks the player, flies the gameplay camera down to a low three-quarter shot of the
    /// boss framed from its own anchors, holds on it with the warning banner, flies back to wherever the player is
    /// NOW (TopDownCameraController.GetRestCameraPosition, evaluated every frame of the return), hands following
    /// back, unlocks the player and finally releases the boss group via WaveController.ActivateBossEncounter.
    ///
    /// Spawns nothing and owns no wave logic. Uses DOTween on scaled time, so pausing the game pauses the intro.</summary>
    public sealed class BossIntroController : MonoBehaviour
    {
        private static readonly int WalkStateHash = Animator.StringToHash("Walk");

        [Header("Scene References")]
        [SerializeField]
        private TopDownCameraController _cameraController;

        [SerializeField]
        [Tooltip("The camera TopDownCameraController moves (Main Camera).")]
        private Transform _cameraTransform;

        [SerializeField]
        private PlayerController _player;

        [SerializeField]
        private UFOTractorBeamController _tractorBeam;

        [SerializeField]
        [Tooltip("Optional. Build-node channel input is locked during the intro.")]
        private PlayerBuildNodeProximityController _buildProximity;

        [SerializeField]
        [Tooltip("Optional. Joystick / bottom controls hidden during the intro.")]
        private GameObject _joystickRoot;

        [SerializeField]
        [Tooltip("Optional. Gameplay HUD faded out during the intro.")]
        private CanvasGroup _hudCanvasGroup;

        [SerializeField]
        [Tooltip("Optional. BOSS + name banner.")]
        private BossIntroPanelView _panel;

        [Header("Intro")]
        [SerializeField]
        private bool _bossIntroEnabled = true;

        [SerializeField]
        [Tooltip("Debug: the boss group spawns and fights straight away with no cinematic. Editor/Development only.")]
        private bool _skipBossIntro;

        [SerializeField, Range(0f, 1f)]
        private float _lockLeadTime = 0.2f;

        [SerializeField, Range(0.3f, 2f)]
        private float _cameraTravelDuration = 1f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Seconds the camera stays on the boss (the banner shows for this long).")]
        private float _bossHoldDuration = 5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Where in the Walk cycle (0..1) the boss is posed while it stands still for the intro. The boss has no idle " +
                 "clip; the first Walk frame is its most neutral standing stance.")]
        private float _bossStandPoseTime = 0f;

        [SerializeField, Range(0.3f, 2f)]
        private float _cameraReturnDuration = 1f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Pause after the camera is back before the player and the boss group are released.")]
        private float _releaseDelay = 0.1f;

        [SerializeField, Range(0.05f, 0.6f)]
        private float _hudFadeDuration = 0.25f;

        [Header("Framing")]
        [SerializeField, Range(0.4f, 0.85f)]
        [Tooltip("Portion of the screen height the boss should fill.")]
        private float _screenHeightFill = 0.55f;

        [SerializeField, Range(0.2f, 1.2f)]
        [Tooltip("Camera height as a fraction of the boss height (a little above the waist looks up at the chest).")]
        private float _cameraHeightFactor = 0.75f;

        [SerializeField, Range(-45f, 45f)]
        [Tooltip("Yaw away from dead-front, for a three-quarter view.")]
        private float _cameraYawOffset = 25f;

        [SerializeField, Range(0f, 0.4f)]
        [Tooltip("How far above screen centre the focus point sits (viewport fraction), leaving the lower part of " +
                 "the screen for the warning banner.")]
        private float _focusScreenLift = 0.12f;

        [SerializeField, Min(2f)]
        private float _minCameraDistance = 6f;

        [Header("Clear view")]
        [SerializeField, Range(0, 90)]
        [Tooltip("Other yaw angles (either side of the preferred three-quarter view) tried when scenery blocks the shot; " +
                 "the angle with the least in the way wins.")]
        private int _alternativeYawStep = 30;

        [SerializeField, Min(1f)]
        [Tooltip("Renderers bigger than this (metres, any side) are never hidden - ground, terrain, water.")]
        private float _maxHiddenOccluderSize = 18f;

        [SerializeField, Min(2f)]
        [Tooltip("How far behind the boss a tall prop still counts as filling the background of the shot.")]
        private float _backdropDepth = 14f;

        private readonly List<Renderer> _sceneRenderers = new List<Renderer>(512);
        private readonly List<Renderer> _hiddenOccluders = new List<Renderer>(32);
        private readonly Vector3[] _shotTargets = new Vector3[3];

        private WaveController _waves;
        private Sequence _sequence;
        private Tween _cameraTween;
        private EnemyController _boss;
        private Animator _bossAnimator;
        private Quaternion _gameplayCameraRotation;
        private bool _joystickWasActive;
        private bool _tractorBeamWasEnabled;
        private bool _locked;
        private bool _playIntro;
        private EnemyRegistry _enemies;
        private readonly List<EnemyController> _frozen = new List<EnemyController>(64);
        private readonly List<int> _frozenGenerations = new List<int>(64);

        public bool IsPlaying { get; private set; }

        public event Action IntroStarted;
        public event Action IntroFinished;

        private bool SkipEffective => _skipBossIntro && (Application.isEditor || Debug.isDebugBuild);

        public void Initialize(WaveController waves)
        {
            Initialize(waves, null);
        }

        /// <param name="enemies">Optional. When given, every other enemy still walking is frozen for the whole
        /// intro and released with the boss, so nothing advances on the base while the camera is away.</param>
        public void Initialize(WaveController waves, EnemyRegistry enemies)
        {
            Unsubscribe();
            _enemies = enemies;
            _waves = waves;
            if (_waves == null)
            {
                return;
            }

            _playIntro = _bossIntroEnabled && !SkipEffective && _cameraController != null && _cameraTransform != null;
            _waves.SetBossIntroHold(_playIntro);
            _waves.BossEncounterSpawned += HandleBossEncounterSpawned;
        }

        private void HandleBossEncounterSpawned(EnemyController boss, IReadOnlyList<EnemyController> group)
        {
            if (!_playIntro)
            {
                return; // WaveController already released the group
            }

            if (boss == null)
            {
                _waves.ActivateBossEncounter();
                return;
            }

            PlayIntro(boss);
        }

        private void PlayIntro(EnemyController boss)
        {
            _boss = boss;
            _bossAnimator = boss.GetComponentInChildren<Animator>();
            HoldBossStill();
            IsPlaying = true;
            IntroStarted?.Invoke();

            _gameplayCameraRotation = _cameraTransform.rotation;
            Vector3 startPosition = _cameraTransform.position;
            Quaternion startRotation = _cameraTransform.rotation;
            ComputeBossShot(boss, out Vector3 shotPosition, out Quaternion shotRotation);
            HideOccluders(shotPosition, shotRotation, boss.transform.position);
            _sceneRenderers.Clear();

            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetLink(gameObject);

            _sequence.AppendInterval(_lockLeadTime);
            _sequence.AppendCallback(LockGameplay);
            _sequence.AppendInterval(0.2f);

            // Camera -> boss.
            _sequence.AppendCallback(() => _cameraController.enabled = false);
            _sequence.Append(CreateCameraBlend(
                () => startPosition, () => startRotation,
                () => shotPosition, () => shotRotation,
                _cameraTravelDuration, Ease.InOutCubic));
            _sequence.InsertCallback(_lockLeadTime + 0.2f + _cameraTravelDuration * 0.55f, ShowBanner);

            // Hold on the boss, standing still.
            _sequence.AppendInterval(_bossHoldDuration);

            // Camera -> the player, wherever the player is now.
            _sequence.AppendCallback(HideBanner);
            _sequence.Append(CreateCameraBlend(
                () => shotPosition, () => shotRotation,
                _cameraController.GetRestCameraPosition, () => _gameplayCameraRotation,
                _cameraReturnDuration, Ease.InOutCubic));
            _sequence.AppendCallback(RestoreGameplayCamera);
            _sequence.AppendInterval(_releaseDelay);
            _sequence.AppendCallback(FinishIntro);
        }

        /// <summary>A 0..1 driver whose endpoints are re-read every frame, so the return leg tracks the player's
        /// current camera rest position instead of one captured when the intro started.</summary>
        private Tween CreateCameraBlend(Func<Vector3> fromPosition, Func<Quaternion> fromRotation,
            Func<Vector3> toPosition, Func<Quaternion> toRotation, float duration, Ease ease)
        {
            float t = 0f;
            return DOTween.To(() => t, value =>
                {
                    t = value;
                    _cameraTransform.SetPositionAndRotation(
                        Vector3.LerpUnclamped(fromPosition(), toPosition(), value),
                        Quaternion.SlerpUnclamped(fromRotation(), toRotation(), value));
                }, 1f, duration)
                .SetEase(ease);
        }

        /// <summary>Low three-quarter shot framed from the boss's own anchors: looks at BossFocusPoint (chest/head),
        /// sized so the boss (root to HealthBarAnchor) fills about _screenHeightFill of the screen height.
        ///
        /// Scenery props have no colliders, so the physics check alone let a tree or a barn fill the frame (user,
        /// 2026-10-15). The shot therefore tries the preferred angle and a few alternatives either side, keeps the one
        /// with the fewest renderers between the camera and the boss, and HideOccluders then hides what is still in
        /// the way until the camera is back.</summary>
        private void ComputeBossShot(EnemyController boss, out Vector3 position, out Quaternion rotation)
        {
            Transform root = boss.transform;
            Vector3 basePoint = root.position;

            float height = MeasureBossHeight(boss);
            Transform focusAnchor = root.Find("BossFocusPoint");
            Vector3 focus = focusAnchor != null ? focusAnchor.position : basePoint + Vector3.up * height * 0.62f;
            _shotTargets[0] = focus;
            _shotTargets[1] = basePoint + Vector3.up * height * 0.25f;
            _shotTargets[2] = basePoint + Vector3.up * height * 0.95f;

            Vector3 forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            Camera cam = _cameraTransform.GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 40f;
            float visibleHeight = height / Mathf.Max(0.1f, _screenHeightFill);
            float distance = Mathf.Max(_minCameraDistance, visibleHeight * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));

            CollectSceneRenderers(cam);
            position = default;
            int bestScore = int.MaxValue;
            int tries = _alternativeYawStep > 0 ? 5 : 1;
            for (int i = 0; i < tries && bestScore > 0; i++)
            {
                // Preferred angle first, then +step, -step, +2 step, -2 step. Something in front of the boss weighs far
                // more than something right behind it (a barn wall filling the background), but both count.
                int ring = (i + 1) / 2;
                float yaw = _cameraYawOffset + (i % 2 == 1 ? 1 : -1) * ring * _alternativeYawStep;
                Vector3 toCamera = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                Vector3 candidate = LiftAboveColliders(basePoint + toCamera * distance + Vector3.up * (height * _cameraHeightFactor), focus);
                int score = CountOccluders(candidate, null) * 100 + CountBackdropClutter(candidate, focus);
                if (score < bestScore)
                {
                    bestScore = score;
                    position = candidate;
                }
            }

            // Pitch down a little so the focus sits above centre (the boss reads in the upper-middle of the frame).
            float liftPitch = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * _focusScreenLift * 2f) * Mathf.Rad2Deg;
            rotation = Quaternion.LookRotation(focus - position, Vector3.up) * Quaternion.Euler(liftPitch, 0f, 0f);
        }

        /// <summary>Terrain / anything with a collider: lift the camera until the line to the focus is clear.</summary>
        private static Vector3 LiftAboveColliders(Vector3 position, Vector3 focus)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 toFocus = focus - position;
                if (!Physics.Raycast(position, toFocus.normalized, out RaycastHit hit, toFocus.magnitude - 0.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponentInParent<EnemyController>() != null)
                {
                    break;
                }

                position += Vector3.up * 0.75f;
            }

            return position;
        }

        /// <summary>Once per intro: every visible, prop-sized renderer the gameplay camera draws, except the enemies'
        /// own (the boss group stays in the picture).</summary>
        private void CollectSceneRenderers(Camera cam)
        {
            _sceneRenderers.Clear();
            int mask = cam != null ? cam.cullingMask : ~0;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || (mask & (1 << renderer.gameObject.layer)) == 0
                    || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                Vector3 size = renderer.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > _maxHiddenOccluderSize
                    || renderer.GetComponentInParent<EnemyController>() != null || renderer.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                _sceneRenderers.Add(renderer);
            }
        }

        /// <summary>How many collected renderers sit between <paramref name="cameraPosition"/> and the boss (its
        /// focus, lower body and head). With a list, they are added to it.</summary>
        private int CountOccluders(Vector3 cameraPosition, List<Renderer> into)
        {
            int count = 0;
            for (int r = 0; r < _sceneRenderers.Count; r++)
            {
                Renderer renderer = _sceneRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                for (int t = 0; t < _shotTargets.Length; t++)
                {
                    Vector3 toTarget = _shotTargets[t] - cameraPosition;
                    float length = toTarget.magnitude;
                    if (length > 0.01f && bounds.IntersectRay(new Ray(cameraPosition, toTarget / length), out float hitDistance)
                        && hitDistance < length - 0.3f)
                    {
                        count++;
                        into?.Add(renderer);
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>Big things just behind the boss along the line of sight (within _backdropDepth metres) - a wall
        /// there fills the whole picture behind it.</summary>
        private int CountBackdropClutter(Vector3 cameraPosition, Vector3 focus)
        {
            Vector3 toFocus = focus - cameraPosition;
            float length = toFocus.magnitude;
            if (length < 0.01f)
            {
                return 0;
            }

            var ray = new Ray(cameraPosition, toFocus / length);
            int count = 0;
            for (int r = 0; r < _sceneRenderers.Count; r++)
            {
                Renderer renderer = _sceneRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (bounds.size.y > 1.5f && bounds.IntersectRay(ray, out float hitDistance)
                    && hitDistance > length + 0.5f && hitDistance < length + _backdropDepth)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Whatever still stands between the shot and the boss is switched off until the camera is back.</summary>
        private void HideOccluders(Vector3 shotPosition, Quaternion shotRotation, Vector3 bossPosition)
        {
            RestoreOccluders();
            CountOccluders(shotPosition, _hiddenOccluders);
            CollectForeground(shotPosition, shotRotation, Vector3.Distance(shotPosition, bossPosition) * 0.75f);
            for (int i = 0; i < _hiddenOccluders.Count; i++)
            {
                _hiddenOccluders[i].enabled = false;
            }
        }

        /// <summary>A bush or crate right under the camera fills the bottom of the frame even when it hides nothing of
        /// the boss: rays through the lower half of the picture pick up anything nearer than <paramref name="maxDistance"/>.</summary>
        private void CollectForeground(Vector3 shotPosition, Quaternion shotRotation, float maxDistance)
        {
            Camera cam = _cameraTransform.GetComponent<Camera>();
            float tanV = Mathf.Tan((cam != null ? cam.fieldOfView : 40f) * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * (cam != null ? cam.aspect : 0.5f);
            for (int r = 0; r < _sceneRenderers.Count; r++)
            {
                Renderer renderer = _sceneRenderers[r];
                if (renderer == null || _hiddenOccluders.Contains(renderer))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (bounds.size.y < 0.6f)
                {
                    continue; // ground tiles, paths, flat beds: hiding them would punch holes in the ground
                }

                bool hit = false;
                for (float vy = -0.95f; vy <= -0.15f && !hit; vy += 0.2f)
                {
                    for (float vx = -0.9f; vx <= 0.91f && !hit; vx += 0.3f)
                    {
                        Vector3 direction = shotRotation * new Vector3(vx * tanH, vy * tanV, 1f);
                        hit = bounds.IntersectRay(new Ray(shotPosition, direction.normalized), out float distance) && distance < maxDistance;
                    }
                }

                if (hit)
                {
                    _hiddenOccluders.Add(renderer);
                }
            }
        }

        private void RestoreOccluders()
        {
            for (int i = 0; i < _hiddenOccluders.Count; i++)
            {
                if (_hiddenOccluders[i] != null)
                {
                    _hiddenOccluders[i].enabled = true;
                }
            }

            _hiddenOccluders.Clear();
        }

        private static float MeasureBossHeight(EnemyController boss)
        {
            Transform healthBarAnchor = boss.transform.Find("HealthBarAnchor");
            if (healthBarAnchor != null)
            {
                return Mathf.Max(1f, healthBarAnchor.position.y - boss.transform.position.y - 0.5f);
            }

            Renderer[] renderers = boss.GetComponentsInChildren<Renderer>();
            bool any = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer || renderers[i].GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return any ? Mathf.Max(1f, bounds.max.y - boss.transform.position.y) : 3f;
        }

        private void LockGameplay()
        {
            if (_locked)
            {
                return;
            }

            _locked = true;
            FreezeOtherEnemies();
            if (_player != null)
            {
                _player.SetMovementEnabled(false);
            }

            if (_tractorBeam != null)
            {
                _tractorBeamWasEnabled = _tractorBeam.IsEnabled;
                _tractorBeam.SetEnabled(false);
            }

            if (_buildProximity != null)
            {
                _buildProximity.SetInputEnabled(false);
            }

            if (_joystickRoot != null)
            {
                _joystickWasActive = _joystickRoot.activeSelf;
                _joystickRoot.SetActive(false);
            }

            FadeHud(0f, blocksRaycasts: false);
        }

        /// <summary>The whole battlefield stops while the boss is introduced: every enemy still in play (except the
        /// boss group, which WaveController already holds) stands still, untargetable and with its animation stopped.</summary>
        private void FreezeOtherEnemies()
        {
            _frozen.Clear();
            _frozenGenerations.Clear();
            if (_enemies == null)
            {
                return;
            }

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies.GetAt(i);
                if (enemy == null || !enemy.IsCombatActive || !enemy.IsTargetable)
                {
                    continue; // the held boss group, or an enemy already being carried off by the beam
                }

                enemy.SetCombatActive(false, 0f);
                _frozen.Add(enemy);
                _frozenGenerations.Add(enemy.Generation);
            }
        }

        private void ReleaseOtherEnemies()
        {
            for (int i = 0; i < _frozen.Count; i++)
            {
                EnemyController enemy = _frozen[i];
                // Same spawn only: a pooled enemy reused since then is not ours to release.
                if (enemy != null && enemy.Generation == _frozenGenerations[i])
                {
                    enemy.SetCombatActive(true);
                }
            }

            _frozen.Clear();
            _frozenGenerations.Clear();
        }

        private void UnlockGameplay()
        {
            if (!_locked)
            {
                return;
            }

            _locked = false;
            if (_player != null)
            {
                _player.SetMovementEnabled(true);
            }

            if (_tractorBeam != null && _tractorBeamWasEnabled)
            {
                _tractorBeam.SetEnabled(true);
            }

            if (_buildProximity != null)
            {
                _buildProximity.SetInputEnabled(true);
            }

            if (_joystickRoot != null && _joystickWasActive)
            {
                _joystickRoot.SetActive(true);
            }

            FadeHud(1f, blocksRaycasts: true);
        }

        private Tween _hudTween;

        private void FadeHud(float alpha, bool blocksRaycasts)
        {
            if (_hudCanvasGroup == null)
            {
                return;
            }

            _hudTween?.Kill();
            _hudCanvasGroup.blocksRaycasts = blocksRaycasts;
            _hudTween = DOTween.To(() => _hudCanvasGroup.alpha, a => _hudCanvasGroup.alpha = a, alpha, _hudFadeDuration)
                .SetLink(gameObject);
        }

        private void ShowBanner()
        {
            if (_panel != null && _boss != null)
            {
                _panel.Show(_boss.Definition != null ? _boss.Definition.DisplayName : "Boss");
            }
        }

        private void HideBanner()
        {
            if (_panel != null)
            {
                _panel.Hide();
            }
        }

        /// <summary>Poses the boss in a standing stance and stops its animation for the whole intro - no attack, no
        /// walk shuffle. EnemyController restores the animator speed it had before the hold when the group is released,
        /// and FinishIntro blends back into Walk.</summary>
        private void HoldBossStill()
        {
            // Only while WaveController holds the boss: releasing it is what restores the animator speed.
            if (_bossAnimator == null || _boss.IsCombatActive || !_bossAnimator.HasState(0, WalkStateHash))
            {
                return;
            }

            _bossAnimator.Play(WalkStateHash, 0, _bossStandPoseTime);
            _bossAnimator.speed = 0f;
            _bossAnimator.Update(0f);
        }

        private void RestoreGameplayCamera()
        {
            RestoreOccluders();
            _cameraTransform.rotation = _gameplayCameraRotation;
            _cameraController.ResumeFollow();
        }

        private void FinishIntro()
        {
            if (_bossAnimator != null && _bossAnimator.HasState(0, WalkStateHash))
            {
                _bossAnimator.CrossFadeInFixedTime(WalkStateHash, 0.2f, 0);
            }

            ReleaseOtherEnemies();
            UnlockGameplay();
            IsPlaying = false;
            _sequence = null;
            _boss = null;
            _bossAnimator = null;

            _waves?.ActivateBossEncounter();
            IntroFinished?.Invoke();
        }

        private void Unsubscribe()
        {
            if (_waves != null)
            {
                _waves.BossEncounterSpawned -= HandleBossEncounterSpawned;
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
            RestoreOccluders();
            Unsubscribe();
        }
    }
}
