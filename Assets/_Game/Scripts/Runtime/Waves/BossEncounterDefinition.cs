using System;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Config-only description of a level's closing boss encounter, played by WaveController once every
    /// normal wave is cleared (or its optional countdown runs out): one Boss plus an escort group, all spawned together in a formation at the start of
    /// the enemy path and held frozen until something (normally BossIntroController) activates them.</summary>
    [CreateAssetMenu(fileName = "BossEncounterDefinition", menuName = "AlienDefense/Waves/Boss Encounter Definition")]
    public sealed class BossEncounterDefinition : ScriptableObject
    {
        [Serializable]
        public struct FormationSlot
        {
            [Tooltip("Metres along the enemy path from its first waypoint (larger = further toward the base).")]
            public float DistanceAlongPath;

            [Tooltip("Metres sideways from the path centre line (positive = right of travel).")]
            public float LateralOffset;
        }

        [Header("Boss")]
        [SerializeField]
        private EnemyDefinition _bossDefinition;

        [SerializeField]
        [Tooltip("Boss slot in the formation. The escorts are laid out around it.")]
        private FormationSlot _bossSlot = new FormationSlot { DistanceAlongPath = 7f, LateralOffset = 0f };

        [SerializeField]
        [Tooltip("Off = the boss does not summon its BossBehaviorDefinition minions during this encounter (the " +
            "escort group already fills that role). The boss's phase-two speed/armour still applies.")]
        private bool _bossMinionsEnabled;

        [Header("Escorts")]
        [SerializeField]
        private EnemySpawnEntry[] _escorts = Array.Empty<EnemySpawnEntry>();

        [SerializeField]
        [Tooltip("One slot per escort, in spawn order. Extra escorts beyond the slot list are placed in rows behind the boss.")]
        private FormationSlot[] _escortSlots = Array.Empty<FormationSlot>();

        [Header("Pacing")]
        [SerializeField, Min(0f)]
        [Tooltip("Pause between the last normal enemy dying and the boss group appearing.")]
        private float _delayAfterNormalWaves = 0.75f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds from the first wave starting until the boss arrives even if normal enemies are still " +
            "alive (they fight on alongside the boss). Clearing the normal waves first still brings the boss early. " +
            "0 = no countdown, the boss only comes once the normal waves are cleared.")]
        private float _bossCountdown;

        [Header("Debug (Editor / Development builds only)")]
        [SerializeField]
        [Tooltip("Skips every normal wave and starts straight at the boss encounter.")]
        private bool _debugSkipNormalWaves;

        public EnemyDefinition BossDefinition => _bossDefinition;
        public FormationSlot BossSlot => _bossSlot;
        public bool BossMinionsEnabled => _bossMinionsEnabled;
        public int EscortEntryCount => _escorts?.Length ?? 0;
        public float DelayAfterNormalWaves => _delayAfterNormalWaves;
        public float BossCountdown => _bossCountdown;

        public bool DebugSkipNormalWaves => _debugSkipNormalWaves && (Application.isEditor || Debug.isDebugBuild);

        public bool IsValid => _bossDefinition != null;

        public EnemySpawnEntry GetEscortEntry(int index)
        {
            return _escorts[index];
        }

        public int TotalEscortCount()
        {
            int total = 0;
            for (int i = 0; i < EscortEntryCount; i++)
            {
                if (_escorts[i] != null && _escorts[i].IsValid)
                {
                    total += _escorts[i].Count;
                }
            }

            return total;
        }

        public FormationSlot GetEscortSlot(int escortIndex)
        {
            if (_escortSlots != null && escortIndex < _escortSlots.Length)
            {
                return _escortSlots[escortIndex];
            }

            // Fallback rows behind the boss, three abreast.
            int overflow = escortIndex - (_escortSlots?.Length ?? 0);
            int row = overflow / 3;
            int column = overflow % 3;
            return new FormationSlot
            {
                DistanceAlongPath = Mathf.Max(0.5f, _bossSlot.DistanceAlongPath - 3f - row * 1.6f),
                LateralOffset = (column - 1) * 1.6f
            };
        }
    }
}
