using System.Collections;
using AnimeFpsGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Combat
{
    /// <summary>
    /// Server-driven respawn. The server owns the death timer and revival; the owning client is the
    /// only peer that moves its own body, so it teleports itself back to the first spawn point.
    /// </summary>
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] CharacterController controller;
        [SerializeField] float respawnSeconds = 3f;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        Coroutine respawnRoutine;

        void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (weapons == null)
            {
                weapons = GetComponent<PlayerWeapons>();
            }

            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
            }

            if (IsServer && health != null)
            {
                health.DeathStateChanged += OnDeathStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (health != null)
            {
                health.DeathStateChanged -= OnDeathStateChanged;
            }

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }
        }

        void OnDeathStateChanged(bool dead)
        {
            if (!IsServer)
            {
                return;
            }

            if (!dead)
            {
                if (respawnRoutine != null)
                {
                    StopCoroutine(respawnRoutine);
                    respawnRoutine = null;
                }

                return;
            }

            if (respawnRoutine == null)
            {
                respawnRoutine = StartCoroutine(RespawnAfterDelay());
            }
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnSeconds);
            respawnRoutine = null;

            if (health == null || !health.IsDead)
            {
                yield break;
            }

            health.Revive();
            RespawnClientRpc();
        }

        [ClientRpc]
        void RespawnClientRpc()
        {
            if (!IsOwner)
            {
                return;
            }

            // The CharacterController must be off while teleporting or it fights the new position.
            var wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            if (wasEnabled)
            {
                controller.enabled = true;
            }

            if (weapons != null)
            {
                weapons.ResetAmmo();
            }
        }
    }
}
