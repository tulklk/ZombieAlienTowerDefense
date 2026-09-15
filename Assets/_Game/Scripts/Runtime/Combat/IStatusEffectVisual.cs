using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Optional component on the root of a StatusEffectDefinition's attached VFX prefab that animates the visual
    /// itself (e.g. ice crystals growing around a stunned enemy). Without one, the enemy just plays/stops the prefab's
    /// particle systems. Purely visual: it is told what happened, it never decides gameplay.</summary>
    public interface IStatusEffectVisual
    {
        /// <summary>Called once, right after the visual is instantiated under <paramref name="owner"/>.</summary>
        void Attach(Transform owner);

        /// <summary>The effect was applied or refreshed and will last <paramref name="duration"/> seconds from now. The
        /// whole animation - in and out - should fit inside that time.</summary>
        void PlayApply(float duration);

        /// <summary>The effect ended (or its enemy died): finish quickly if still showing, then hide.</summary>
        void PlayExpire();

        /// <summary>Hide at once with no animation (pool reuse, level cleanup).</summary>
        void ResetVisual();
    }
}
