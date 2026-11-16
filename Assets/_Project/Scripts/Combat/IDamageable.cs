using UnityEngine;

namespace AnimeFpsGame.Combat
{
    public interface IDamageable
    {
        void ApplyDamage(float amount, Vector3 point, Vector3 normal, ulong attackerClientId);
    }
}
