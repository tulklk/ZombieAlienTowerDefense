namespace AlienDefense.Combat
{
    /// <summary>Runtime state of one active status effect on one enemy: remaining duration, stacks, tick timer.</summary>
    public sealed class StatusEffectInstance
    {
        private readonly float _durationScale;

        public StatusEffectDefinition Definition { get; }
        public float RemainingDuration { get; private set; }
        public int StackCount { get; private set; }
        public float TickTimer { get; private set; }

        /// <param name="durationScale">Per-target duration multiplier (e.g. a Boss shrugging a Stun off in half the time).</param>
        public StatusEffectInstance(StatusEffectDefinition definition, float durationScale = 1f)
        {
            Definition = definition;
            _durationScale = durationScale;
            RemainingDuration = definition.Duration * durationScale;
            StackCount = 1;
            TickTimer = definition.TickInterval;
        }

        /// <summary>Reapplies the same definition: refreshes duration and, for StackMagnitude, grows the stack.</summary>
        public void Reapply()
        {
            RemainingDuration = Definition.Duration * _durationScale;

            if (Definition.StackingRule == StatusStackingRule.StackMagnitude && StackCount < Definition.MaxStacks)
            {
                StackCount++;
            }
        }

        /// <summary>Advances the timers by deltaTime. Returns true once per elapsed tick interval.</summary>
        public bool Tick(float deltaTime)
        {
            RemainingDuration -= deltaTime;
            TickTimer -= deltaTime;

            if (TickTimer > 0f)
            {
                return false;
            }

            TickTimer += Definition.TickInterval;
            return true;
        }

        public bool IsExpired => RemainingDuration <= 0f;
    }
}
