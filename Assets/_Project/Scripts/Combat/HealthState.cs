using UnityEngine;

namespace AnimeFpsGame.Combat
{
    /// <summary>Plain health rules, testable without a scene or network.</summary>
    public class HealthState
    {
        public HealthState(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>Returns true only on the transition from alive to dead.</summary>
        public bool ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return false;
            }

            Current = Mathf.Max(0f, Current - amount);
            return IsDead;
        }

        public void Revive()
        {
            Current = Max;
        }
    }
}
