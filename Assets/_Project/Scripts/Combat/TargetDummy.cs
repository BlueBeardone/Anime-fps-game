using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Combat
{
    /// <summary>
    /// A networked practice target. Hides on death and comes back after a short delay so a range
    /// session never runs out of things to shoot.
    /// </summary>
    public class TargetDummy : NetworkBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] float respawnSeconds = 3f;

        Renderer[] renderers;
        Coroutine respawnRoutine;

        void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        public override void OnNetworkSpawn()
        {
            renderers = GetComponentsInChildren<Renderer>(true);

            if (health != null)
            {
                health.DeathStateChanged += OnDeathStateChanged;
                ApplyVisibility(!health.IsDead);
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
            ApplyVisibility(!dead);

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

            if (health != null && health.IsDead)
            {
                health.Revive();
            }
        }

        void ApplyVisibility(bool visible)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
