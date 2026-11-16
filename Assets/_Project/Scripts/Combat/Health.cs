using System;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Combat
{
    /// <summary>
    /// Server-authoritative health. Players and dummies both use it; death state is replicated so
    /// corpses stop blocking and absorbing shots on every client.
    /// </summary>
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] bool ignoreOwnerDamage = true;

        readonly NetworkVariable<float> currentHealth = new(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<bool> isDead = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth.Value;
        public bool IsDead => isDead.Value;

        public event Action Died;
        public event Action<bool> DeathStateChanged;

        public override void OnNetworkSpawn()
        {
            isDead.OnValueChanged += OnDeadChanged;

            if (IsServer)
            {
                currentHealth.Value = maxHealth;
                isDead.Value = false;
            }

            ApplyDeathState(isDead.Value);
        }

        public override void OnNetworkDespawn()
        {
            isDead.OnValueChanged -= OnDeadChanged;
        }

        public void ApplyDamage(float amount, Vector3 point, Vector3 normal, ulong attackerClientId)
        {
            if (!IsServer || IsDead || amount <= 0f)
            {
                return;
            }

            if (ignoreOwnerDamage && attackerClientId == OwnerClientId)
            {
                return;
            }

            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
            if (currentHealth.Value <= 0f)
            {
                isDead.Value = true;
            }
        }

        public void Revive()
        {
            if (!IsServer)
            {
                return;
            }

            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }

        void OnDeadChanged(bool previous, bool dead)
        {
            ApplyDeathState(dead);
            DeathStateChanged?.Invoke(dead);

            if (dead)
            {
                Died?.Invoke();
            }
        }

        /// <summary>Colliders and the CharacterController are disabled on every peer while dead.</summary>
        void ApplyDeathState(bool dead)
        {
            foreach (var bodyCollider in GetComponentsInChildren<Collider>(true))
            {
                bodyCollider.enabled = !dead;
            }

            if (TryGetComponent<CharacterController>(out var controller))
            {
                controller.enabled = !dead;
            }
        }
    }
}
