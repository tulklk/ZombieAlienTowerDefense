using AlienDefense.Vfx;
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

        /// <summary>Optional. If this damage is the killing blow, the victim plays this instead of its usual
        /// defeat effect (e.g. an ice lance shattering the enemy it kills).</summary>
        public readonly VfxDefinition KillVfx;

        public DamageInfo(float amount, GameObject source, Vector3 hitPosition, DamageType damageType = DamageType.Physical,
            VfxDefinition killVfx = null)
        {
            Amount = amount;
            Source = source;
            HitPosition = hitPosition;
            DamageType = damageType;
            KillVfx = killVfx;
        }

        public DamageInfo WithKillVfx(VfxDefinition killVfx)
        {
            return new DamageInfo(Amount, Source, HitPosition, DamageType, killVfx);
        }
    }
}
