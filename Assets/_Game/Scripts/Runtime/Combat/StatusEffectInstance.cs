namespace AlienDefense.Combat
{
    /// <summary>Runtime state of one active status effect on one enemy: remaining duration, stacks, tick timer.</summary>
    public sealed class StatusEffectInstance
    {
        public StatusEffectDefinition Definition { get; }
        public float RemainingDuration { get; private set; }
        public int StackCount { get; private set; }
        public float TickTimer { get; private set; }

        public StatusEffectInstance(StatusEffectDefinition definition)
        {
            Definition = definition;
            RemainingDuration = definition.Duration;
            StackCount = 1;
            TickTimer = definition.TickInterval;
        }

        /// <summary>Reapplies the same definition: refreshes duration and, for StackMagnitude, grows the stack.</summary>
        public void Reapply()
        {
            RemainingDuration = Definition.Duration;

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
