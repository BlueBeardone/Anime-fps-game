using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Yaw on the body, pitch on the camera pivot. Only the owning player drives the camera.
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float sensitivity = 2f;
        [SerializeField] float minPitch = -85f;
        [SerializeField] float maxPitch = 85f;
        [SerializeField] float recoilRecoverySpeed = 14f;
        [SerializeField] float recoilRecoveryDelay = 0.15f;

        const float MouseScale = 0.1f;

        InputAction lookAction;
        Camera playerCamera;
        AnimeFpsGame.Combat.Health health;
        float pitch;
        float yaw;
        Vector2 recoilOffset;
        float recoilIdleTime;

        // Scene cameras we switched off for this session, restored when the player leaves.
        readonly List<Camera> disabledCameras = new();
        readonly List<AudioListener> disabledListeners = new();

        void Awake()
        {
            health = GetComponent<AnimeFpsGame.Combat.Health>();
        }

        public override void OnNetworkSpawn()
        {
            playerCamera = cameraPivot != null ? cameraPivot.GetComponentInChildren<Camera>(true) : null;

            if (!IsOwner)
            {
                if (playerCamera != null)
                {
                    Destroy(playerCamera.gameObject);
                }

                enabled = false;
                return;
            }

            // The scene has a preview camera until a player exists; ours takes over.
            foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (other != playerCamera && other.enabled)
                {
                    other.enabled = false;
                    disabledCameras.Add(other);
                }
            }

            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if ((playerCamera == null || listener != playerCamera.GetComponent<AudioListener>()) && listener.enabled)
                {
                    listener.enabled = false;
                    disabledListeners.Add(listener);
                }
            }

            var input = GetComponent<PlayerInput>();
            if (input != null && input.actions != null)
            {
                lookAction = input.actions["Look"];
            }

            yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Adds camera recoil in degrees: x = yaw, y = pitch.</summary>
        public void AddRecoil(Vector2 degrees)
        {
            recoilOffset += degrees;
            recoilIdleTime = 0f;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            foreach (var camera in disabledCameras)
            {
                if (camera != null)
                {
                    camera.enabled = true;
                }
            }

            foreach (var listener in disabledListeners)
            {
                if (listener != null)
                {
                    listener.enabled = true;
                }
            }

            disabledCameras.Clear();
            disabledListeners.Clear();
        }

        void Update()
        {
            if (lookAction == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                return;
            }

            var look = lookAction.ReadValue<Vector2>() * (sensitivity * MouseScale);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            recoilIdleTime += Time.deltaTime;
            if (recoilIdleTime > recoilRecoveryDelay)
            {
                recoilOffset = Vector2.Lerp(recoilOffset, Vector2.zero,
                    recoilRecoverySpeed * Time.deltaTime);
            }

            transform.localRotation = Quaternion.Euler(0f, yaw + recoilOffset.x, 0f);

            if (cameraPivot != null)
            {
                var recoilPitch = Mathf.Clamp(pitch + recoilOffset.y, minPitch, maxPitch);
                cameraPivot.localRotation = Quaternion.Euler(recoilPitch, 0f, 0f);
            }
        }
    }
}
