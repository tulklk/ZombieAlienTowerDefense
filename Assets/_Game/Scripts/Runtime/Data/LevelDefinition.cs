using UnityEngine;

namespace AlienDefense.Data
{
    /// <summary>
    /// Configuration-only description of a level: starting economy, base health and
    /// pacing. Contains no runtime mutable state (no current health, no current gold) —
    /// those live in the pure C# services the composition root creates from this data.
    /// Fields grow across phases (player definition, wave list, ...) as those systems land;
    /// nothing here should ever be written to at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "AlienDefense/Level/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        private string _levelId = "level_01";

        [SerializeField, Min(0)]
        private int _startingResource = 250;

        [SerializeField, Min(1)]
        private int _baseMaxHealth = 20;

        [SerializeField, Min(0f)]
        private float _preparationDuration = 5f;

        [SerializeField, Min(1)]
        private int _targetFrameRate = 60;

        public string LevelId => _levelId;
        public int StartingResource => _startingResource;
        public int BaseMaxHealth => _baseMaxHealth;
        public float PreparationDuration => _preparationDuration;
        public int TargetFrameRate => _targetFrameRate;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_levelId))
            {
                Debug.LogError($"[LevelDefinition] '{name}' has an empty Level Id.", this);
            }

            if (_startingResource < 0)
            {
                _startingResource = 0;
            }

            if (_baseMaxHealth < 1)
            {
                _baseMaxHealth = 1;
            }

            if (_preparationDuration < 0f)
            {
                _preparationDuration = 0f;
            }

            if (_targetFrameRate < 1)
            {
                _targetFrameRate = 1;
            }
        }
    }
}
