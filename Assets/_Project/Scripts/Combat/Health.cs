using System;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Combat
{
    /// <summary>
    /// Server-authoritative health. Players and dummies both use it; death state is replicated so
    /// corpses stop blocking and absorbing shots on every peer. The clamp/death rules themselves
    /// live in <see cref="HealthState"/> so they are tested without a scene or network.
    /// </summary>
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] bool ignoreOwnerDamage = true;

        readonly NetworkVariable<float> currentHealth = new(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<bool> isDead = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        HealthState state;
        Collider[] bodyColliders;
        bool[] colliderEnabledAtSpawn;
        CharacterController controller;
        bool controllerEnabledAtSpawn;

        public float MaxHealth => state?.Max ?? maxHealth;
        public float CurrentHealth => IsServer && state != null ? state.Current : currentHealth.Value;
        public bool IsDead => IsServer && state != null ? state.IsDead : isDead.Value;

        public event Action Died;
        public event Action<bool> DeathStateChanged;

        void Awake()
        {
            state = new HealthState(maxHealth);
            controller = GetComponent<CharacterController>();
            CacheBodyParts();
        }

        public override void OnNetworkSpawn()
        {
            isDead.OnValueChanged += OnDeadChanged;

            if (IsServer)
            {
                state ??= new HealthState(maxHealth);
                state.Revive();
                currentHealth.Value = state.Current;
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
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            state ??= new HealthState(maxHealth);
            if (state.IsDead)
            {
                return;
            }

            if (ignoreOwnerDamage && attackerClientId == OwnerClientId)
            {
                return;
            }

            var died = state.ApplyDamage(amount);
            currentHealth.Value = state.Current;
            if (died)
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

            state ??= new HealthState(maxHealth);
            state.Revive();
            currentHealth.Value = state.Current;
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

        /// <summary>
        /// Colliders and the CharacterController are disabled on every peer while dead, then
        /// restored to exactly the enabled state they had at spawn — not blindly re-enabled.
        /// </summary>
        void ApplyDeathState(bool dead)
        {
            if (bodyColliders == null)
            {
                CacheBodyParts();
            }

            for (var i = 0; i < bodyColliders.Length; i++)
            {
                if (bodyColliders[i] != null)
                {
                    bodyColliders[i].enabled = !dead && colliderEnabledAtSpawn[i];
                }
            }

            if (controller != null)
            {
                controller.enabled = !dead && controllerEnabledAtSpawn;
            }
        }

        void CacheBodyParts()
        {
            bodyColliders = GetComponentsInChildren<Collider>(true);
            colliderEnabledAtSpawn = new bool[bodyColliders.Length];
            for (var i = 0; i < bodyColliders.Length; i++)
            {
                colliderEnabledAtSpawn[i] = bodyColliders[i] != null && bodyColliders[i].enabled;
            }

            controllerEnabledAtSpawn = controller != null && controller.enabled;
        }
    }
}
