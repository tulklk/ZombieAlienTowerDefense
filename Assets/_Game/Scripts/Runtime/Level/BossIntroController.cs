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
        private static readonly int AttackStateHash = Animator.StringToHash("Attack");
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

        [SerializeField, Range(0.5f, 4f)]
        private float _bossHoldDuration = 2f;

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

        public bool IsPlaying { get; private set; }

        public event Action IntroStarted;
        public event Action IntroFinished;

        private bool SkipEffective => _skipBossIntro && (Application.isEditor || Debug.isDebugBuild);

        public void Initialize(WaveController waves)
        {
            Unsubscribe();
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
            IsPlaying = true;
            IntroStarted?.Invoke();

            _gameplayCameraRotation = _cameraTransform.rotation;
            Vector3 startPosition = _cameraTransform.position;
            Quaternion startRotation = _cameraTransform.rotation;
            ComputeBossShot(boss, out Vector3 shotPosition, out Quaternion shotRotation);

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

            // Hold: the boss taunts.
            _sequence.AppendCallback(PlayBossTaunt);
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
        /// sized so the boss (root to HealthBarAnchor) fills about _screenHeightFill of the screen height.</summary>
        private void ComputeBossShot(EnemyController boss, out Vector3 position, out Quaternion rotation)
        {
            Transform root = boss.transform;
            Vector3 basePoint = root.position;

            float height = MeasureBossHeight(boss);
            Transform focusAnchor = root.Find("BossFocusPoint");
            Vector3 focus = focusAnchor != null ? focusAnchor.position : basePoint + Vector3.up * height * 0.62f;

            Vector3 forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 toCamera = Quaternion.AngleAxis(_cameraYawOffset, Vector3.up) * forward;

            Camera cam = _cameraTransform.GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 40f;
            float visibleHeight = height / Mathf.Max(0.1f, _screenHeightFill);
            float distance = Mathf.Max(_minCameraDistance, visibleHeight * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));

            position = basePoint + toCamera * distance + Vector3.up * (height * _cameraHeightFactor);

            // Keep terrain / props from blocking the shot: lift the camera until the line to the focus is clear.
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

            // Pitch down a little so the focus sits above centre (the boss reads in the upper-middle of the frame).
            float liftPitch = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * _focusScreenLift * 2f) * Mathf.Rad2Deg;
            rotation = Quaternion.LookRotation(focus - position, Vector3.up) * Quaternion.Euler(liftPitch, 0f, 0f);
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

        private void PlayBossTaunt()
        {
            if (_bossAnimator == null)
            {
                return;
            }

            _bossAnimator.speed = 1f; // EnemyController restores its own speed when the group is released
            if (_bossAnimator.HasState(0, AttackStateHash))
            {
                _bossAnimator.CrossFadeInFixedTime(AttackStateHash, 0.15f, 0);
            }
        }

        private void RestoreGameplayCamera()
        {
            _cameraTransform.rotation = _gameplayCameraRotation;
            _cameraController.ResumeFollow();
        }

        private void FinishIntro()
        {
            if (_bossAnimator != null && _bossAnimator.HasState(0, WalkStateHash))
            {
                _bossAnimator.CrossFadeInFixedTime(WalkStateHash, 0.2f, 0);
            }

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
            Unsubscribe();
        }
    }
}
