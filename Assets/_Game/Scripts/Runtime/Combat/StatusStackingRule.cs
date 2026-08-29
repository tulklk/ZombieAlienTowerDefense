namespace AlienDefense.Combat
{
    /// <summary>How reapplying the same StatusEffectDefinition combines with an already-active instance.</summary>
    public enum StatusStackingRule
    {
        /// <summary>Only duration refreshes; magnitude never stacks. Used by Slow.</summary>
        RefreshDurationOnly = 0,

        /// <summary>Duration refreshes and stack count increases up to MaxStacks; magnitude scales with stacks. Used by Burn.</summary>
        StackMagnitude = 1
    }
}
