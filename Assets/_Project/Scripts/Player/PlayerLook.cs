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

        const float MouseScale = 0.1f;

        InputAction lookAction;
        Camera playerCamera;
        float pitch;

        // Scene cameras we switched off for this session, restored when the player leaves.
        readonly List<Camera> disabledCameras = new();
        readonly List<AudioListener> disabledListeners = new();

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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

            var look = lookAction.ReadValue<Vector2>() * (sensitivity * MouseScale);
            transform.Rotate(Vector3.up, look.x, Space.Self);

            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }
    }
}
