using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Immutable description of one damage event: how much, from where, hitting where.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPosition;
        public readonly DamageType DamageType;

        public DamageInfo(float amount, GameObject source, Vector3 hitPosition, DamageType damageType = DamageType.Physical)
        {
            Amount = amount;
            Source = source;
            HitPosition = hitPosition;
            DamageType = damageType;
        }
    }
}
