using System.Collections;
using System.Collections.Generic;
using AlienDefense.UI;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Continuously watches the Player's XZ distance to a fixed set of BuildNodes. Flying into a node's
    /// proximity radius and staying there for _channelDuration seconds (visualized as BuildNodeChannelUI's ring
    /// filling clockwise - reset the instant the Player leaves or a different node becomes nearest, and poured
    /// full of a continuous stream of Energy balls from the UFO into the tower base while it fills) triggers,
    /// if the Energy wallet can afford this node's next action, either the TowerChoicePresenter popup (node
    /// Available - the player still
    /// picks which tower) or an immediate EnergyTowerTransactionService upgrade (node Occupied - already a
    /// specific tower, no choice needed). Deliberately does NOT touch TowerSelectionService - proximity alone
    /// used to auto-select the nearest tower (showing its RangeIndicator, a green circle) but that visually
    /// clashed with this controller's own yellow channel ring, so tower selection here is tap-only again (see
    /// WorldSelectionController). Never spawns/charges anything itself - only forwards to
    /// EnergyTowerTransactionService/TowerChoicePresenter.</summary>
    public sealed class PlayerBuildNodeProximityController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Every BuildNode this controller should react to. Scene-local list, not a global registry.")]
        private BuildNode[] _nodes;

        [SerializeField, Min(0.1f)]
        [Tooltip("World units (XZ only) the Player must be within for a node to react.")]
        private float _proximityRadius = 2f;

        [SerializeField, Min(0.02f)]
        private float _scanInterval = 0.1f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the Player must stay in range before a build/upgrade actually triggers.")]
        private float _channelDuration = 3f;

        [Header("Energy Stream")]
        [SerializeField]
        [Tooltip("Optional. The energy ball art poured from the UFO into the tower base while a node channels - " +
            "purely decorative. Leave empty to skip the stream.")]
        private Sprite _energyBallSprite;

        [SerializeField]
        [Tooltip("Where the stream leaves the UFO - assign the tractor beam's CaptureSocket, the same point absorbed " +
            "objects fly into. The saucer model hovers well above the UFO's root transform, so the root is a poor " +
            "stand-in; if this is left empty the stream starts Stream Origin Fallback Height above the root instead.")]
        private Transform _streamOrigin;

        [SerializeField]
        [Tooltip("Only used when Stream Origin is empty: metres above the Player's root to start the stream.")]
        private float _streamOriginFallbackHeight = 1.8f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds each ball takes to travel from the UFO to the tower base. Together with Stream Interval " +
            "this sets how many balls are in the air at once, i.e. how solid the stream looks.")]
        private float _flyDuration = 0.4f;

        [SerializeField, Min(0.005f)]
        [Tooltip("Seconds between balls leaving the UFO. Small enough relative to Fly Duration and the balls overlap " +
            "into one continuous rope of energy instead of a dotted line.")]
        private float _streamInterval = 0.025f;

        [SerializeField, Min(0f)]
        [Tooltip("How far the stream bows above the straight line from the UFO to the tower base. Shared by every " +
            "ball, so the whole stream traces a single smooth curve.")]
        private float _streamArcHeight = 0.6f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Size of each ball in world units. The sprite is rescaled to this whatever its import size - the " +
            "energy ball art imports at nearly 13m across, so a fixed scale factor is meaningless.")]
        private float _flyBallWorldSize = 0.45f;

        private Transform _player;
        private Transform _cameraTransform;
        private EnergyTowerTransactionService _energyTransactions;
        private TowerChoicePresenter _towerChoicePresenter;

        private BuildNode _channelingNode;
        private float _channelTimer;
        private bool _isChannelBusy; // true while a fly animation / choice popup is resolving - suspends new channels
        private float _scanTimer;
        private bool _isInputEnabled = true;

        // Energy stream state. Balls are pooled: a 3-second channel launches over a hundred of them, which is far
        // too much churn to Instantiate/Destroy each time.
        private readonly Stack<Transform> _flyBallPool = new Stack<Transform>();
        private readonly List<Transform> _allFlyBalls = new List<Transform>();
        private float _flyBallScale = 1f;
        private float _streamTimer;
        private float _streamTailLandTime;

        public void Initialize(Transform player, EnergyTowerTransactionService energyTransactions, TowerChoicePresenter towerChoicePresenter, Transform cameraTransform)
        {
            _player = player;
            _cameraTransform = cameraTransform;
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            _energyTransactions = energyTransactions;
            _towerChoicePresenter = towerChoicePresenter;
            _channelingNode = null;
            _channelTimer = 0f;
            _isChannelBusy = false;
            _scanTimer = 0f;
            _streamTimer = 0f;

            if (_energyBallSprite != null)
            {
                Vector3 spriteSize = _energyBallSprite.bounds.size;
                _flyBallScale = _flyBallWorldSize / Mathf.Max(0.0001f, Mathf.Max(spriteSize.x, spriteSize.y));
            }

            InitializeBillboards(cameraTransform);
            RefreshAllCostBadges();
        }

        /// <summary>Keeps every node's ring/badge facing the fixed isometric camera - otherwise the World Space
        /// canvas plane renders edge-on and unreadable from this camera's steep top-down angle.</summary>
        private void InitializeBillboards(Transform cameraTransform)
        {
            if (_nodes == null || cameraTransform == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i]?.ChannelUI?.Initialize(cameraTransform);
            }
        }

        /// <summary>Mirrors WorldSelectionController.SetInputEnabled: disabled during Paused/Victory/Defeat. Just
        /// stops new proximity triggers — never force-clears an already-open TowerDetailsPanel or in-flight
        /// channel/animation.</summary>
        public void SetInputEnabled(bool value)
        {
            _isInputEnabled = value;
        }

        private void Update()
        {
            if (_player == null || _nodes == null || _nodes.Length == 0)
            {
                return;
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                Scan();
            }

            TickChannel(Time.deltaTime);
        }

        private void Scan()
        {
            if (!_isInputEnabled)
            {
                return;
            }

            Vector3 playerPosition = _player.position;
            float nearestSqrDistance = _proximityRadius * _proximityRadius;
            BuildNode nearestNode = null;

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node == null || node.State == BuildNodeState.Disabled)
                {
                    continue;
                }

                Vector3 toNode = node.BuildPoint.position - playerPosition;
                toNode.y = 0f;
                float sqrDistance = toNode.sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestNode = node;
            }

            BuildNode channelTarget = nearestNode != null && IsChannelable(nearestNode) ? nearestNode : null;
            if (channelTarget != _channelingNode && !_isChannelBusy)
            {
                CancelChannel();
                _channelingNode = channelTarget;
            }
        }

        private bool IsChannelable(BuildNode node)
        {
            if (node.State == BuildNodeState.Available)
            {
                return true;
            }

            return node.State == BuildNodeState.Occupied && node.CurrentTower != null && !node.CurrentTower.IsMaxLevel;
        }

        private void TickChannel(float deltaTime)
        {
            if (_channelingNode == null || _isChannelBusy || !_isInputEnabled)
            {
                return;
            }

            // A pending tower choice means a node is already being decided on. The popup pauses GameSpeed, but
            // if anything lets time run while it is still open, channelling on would replay the energy stream
            // and re-open the same popup on a loop.
            if (_towerChoicePresenter != null && _towerChoicePresenter.IsShowing)
            {
                return;
            }

            _channelTimer += deltaTime;
            _channelingNode.ChannelUI?.SetChannelProgress(_channelTimer / _channelDuration);
            TickEnergyStream(_channelingNode, deltaTime);

            if (_channelTimer < _channelDuration)
            {
                return;
            }

            BuildNode node = _channelingNode;
            CancelChannel();
            StartCoroutine(ResolveChannelComplete(node));
        }

        private void CancelChannel()
        {
            _channelingNode?.ChannelUI?.SetChannelProgress(0f);
            _channelingNode = null;
            _channelTimer = 0f;
            _streamTimer = 0f; // the next channel's stream starts pouring on its very first frame
        }

        private bool CanAffordNode(BuildNode node)
        {
            if (node == null || _energyTransactions == null)
            {
                return false;
            }

            return node.State == BuildNodeState.Available
                ? _towerChoicePresenter != null && _towerChoicePresenter.HasAnyAffordableTower()
                : _energyTransactions.CanAffordUpgrade(node.CurrentTower);
        }

        /// <summary>Runs the build/upgrade action once the channel completes - a coroutine (not an instant call) so
        /// the tail of the energy stream finishes landing first and the tower reads as being built by the energy
        /// that just poured into it.</summary>
        private IEnumerator ResolveChannelComplete(BuildNode node)
        {
            if (node == null || _energyTransactions == null)
            {
                yield break;
            }

            bool isBuild = node.State == BuildNodeState.Available;
            if (!CanAffordNode(node))
            {
                yield break; // channel simply fizzles - no stream was poured, no popup
            }

            _isChannelBusy = true;

            // The stream's tweens run on scaled time and the build popup pauses GameSpeed, so opening it while
            // balls are still mid-flight would leave them frozen in the air behind it.
            float tail = _streamTailLandTime - Time.time;
            if (tail > 0f)
            {
                yield return new WaitForSeconds(tail);
            }

            if (isBuild)
            {
                _towerChoicePresenter?.ShowForNode(node);
            }
            else
            {
                _energyTransactions.TryUpgrade(node.CurrentTower);
            }

            _isChannelBusy = false;
        }

        /// <summary>While a node channels, pours a continuous stream of energy balls out of the UFO's belly and
        /// down into the tower base, so the ring filling up visibly IS the energy being transferred. Only pours
        /// while the node is actually affordable - streaming energy into a build that is going to fizzle would
        /// promise something the channel can't deliver.
        ///
        /// Cadence is kept with an accumulating timer (several balls per frame if the frame was long) rather than
        /// one ball per frame, so the stream's density is the same at 30fps as at 60fps. Capped per frame so a
        /// hitch can't dump a burst of balls on top of each other.</summary>
        private void TickEnergyStream(BuildNode node, float deltaTime)
        {
            if (_energyBallSprite == null || _player == null || node == null)
            {
                return;
            }

            _streamTimer -= deltaTime;
            if (_streamTimer > 0f)
            {
                return;
            }

            if (!CanAffordNode(node))
            {
                _streamTimer = _streamInterval;
                return;
            }

            // Out of the saucer's belly (the beam's capture socket), into the tower base.
            Vector3 start = _streamOrigin != null
                ? _streamOrigin.position
                : _player.position + Vector3.up * _streamOriginFallbackHeight;
            Vector3 end = node.BuildPoint.position + Vector3.up * 0.3f;

            const int maxBallsPerFrame = 3;
            for (int i = 0; i < maxBallsPerFrame && _streamTimer <= 0f; i++)
            {
                LaunchFlyBall(start, end);
                _streamTimer += _streamInterval;
            }

            if (_streamTimer < 0f)
            {
                _streamTimer = 0f;
            }
        }

        /// <summary>One link of the stream. Every ball follows the identical arc on a linear ease, so consecutive
        /// balls sit at an even spacing along one curve and overlap into a continuous rope - any per-ball
        /// randomness in path or timing breaks that up into a scattered spray.
        ///
        /// The balls are flat sprites, so each is turned to face the camera for its whole flight: a plain
        /// SpriteRenderer faces +Z and this level's camera looks along -X, which used to render the old fly
        /// animation perfectly edge-on as a thin line.</summary>
        private void LaunchFlyBall(Vector3 start, Vector3 end)
        {
            Transform ball = RentFlyBall();
            ball.position = start;
            ball.localScale = Vector3.one * (_flyBallScale * 0.5f);
            FaceCamera(ball);

            // Targeted at the ball so OnDestroy's DOTween.Kill(ball) can find it - tweens nested inside a sequence
            // aren't looked up by their own targets.
            Sequence flight = DOTween.Sequence().SetTarget(ball);
            flight.Append(ball.DOJump(end, _streamArcHeight, 1, _flyDuration).SetEase(Ease.Linear));
            flight.Join(ball.DOScale(_flyBallScale, _flyDuration * 0.2f).SetEase(Ease.OutQuad));
            flight.Insert(_flyDuration * 0.8f, ball.DOScale(0f, _flyDuration * 0.2f).SetEase(Ease.InQuad));
            flight.OnUpdate(() => FaceCamera(ball));

            // OnKill rather than OnComplete: it also fires after a normal completion (auto-kill) and covers the
            // tween being killed early, so a ball is always handed back to the pool.
            flight.OnKill(() => ReturnFlyBall(ball));

            _streamTailLandTime = Mathf.Max(_streamTailLandTime, Time.time + _flyDuration);
        }

        private Transform RentFlyBall()
        {
            while (_flyBallPool.Count > 0)
            {
                Transform pooled = _flyBallPool.Pop();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var go = new GameObject("EnergyBallFlyVisual");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _energyBallSprite;
            renderer.sortingOrder = 100; // drawn after the beam's own transparent cone/particles

            _allFlyBalls.Add(go.transform);
            return go.transform;
        }

        private void ReturnFlyBall(Transform ball)
        {
            if (ball == null)
            {
                return;
            }

            ball.gameObject.SetActive(false);
            _flyBallPool.Push(ball);
        }

        /// <summary>The pooled balls live at the scene root (not under this controller, whose own scale would
        /// distort their world size), so they are cleaned up explicitly.</summary>
        private void OnDestroy()
        {
            for (int i = 0; i < _allFlyBalls.Count; i++)
            {
                Transform ball = _allFlyBalls[i];
                if (ball == null)
                {
                    continue;
                }

                DOTween.Kill(ball);
                Destroy(ball.gameObject);
            }

            _allFlyBalls.Clear();
            _flyBallPool.Clear();
        }

        private void FaceCamera(Transform target)
        {
            if (target != null && _cameraTransform != null)
            {
                target.rotation = _cameraTransform.rotation;
            }
        }

        /// <summary>Cheap - just text updates, called once on Initialize and again whenever the Energy wallet
        /// changes (see LevelCompositionRoot wiring) so every badge stays accurate without a per-frame cost.</summary>
        public void RefreshAllCostBadges()
        {
            if (_nodes == null || _energyTransactions == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node == null || node.ChannelUI == null)
                {
                    continue;
                }

                int cost;
                if (node.State == BuildNodeState.Available)
                {
                    cost = _towerChoicePresenter != null ? _towerChoicePresenter.CheapestBuildCost() : 0;
                }
                else if (node.State == BuildNodeState.Occupied)
                {
                    cost = _energyTransactions.GetUpgradeCost(node.CurrentTower);
                }
                else
                {
                    cost = 0;
                }

                int wallet = _energyTransactions != null ? CurrentWalletEnergy() : 0;
                node.ChannelUI.SetCost(wallet, cost);

                node.ChannelUI.SetRingVisible(IsChannelable(node));
            }
        }

        private int CurrentWalletEnergy()
        {
            return _energyTransactions != null ? _energyTransactions.CurrentWalletEnergy : 0;
        }
    }
}
