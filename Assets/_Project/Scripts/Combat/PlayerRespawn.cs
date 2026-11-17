using System.Collections;
using AnimeFpsGame.Movement;
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
        [SerializeField] CharacterMovement movement;
        [SerializeField] PlayerLook playerLook;
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

            if (movement == null)
            {
                movement = GetComponent<CharacterMovement>();
            }

            if (playerLook == null)
            {
                playerLook = GetComponent<PlayerLook>();
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

            // Teleporting through the movement core clears intent and velocity together.
            if (movement != null)
            {
                movement.Teleport(spawnPosition, spawnRotation);
            }
            else
            {
                transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            }

            if (playerLook != null)
            {
                playerLook.SetYaw(spawnRotation.eulerAngles.y);
            }

            if (weapons != null)
            {
                weapons.ResetAmmo();
            }
        }
    }
}
