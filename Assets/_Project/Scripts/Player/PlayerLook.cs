using System.Collections.Generic;
using AnimeFpsGame.Input;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// Yaw on the body, pitch on the camera pivot. Only the owning player drives the camera.
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] InputReader input;
        [SerializeField] AnimeFpsGame.Movement.CharacterMovement movement;
        [SerializeField] Rigidbody body;
        [SerializeField] float sensitivity = 2f;
        [SerializeField] float minPitch = -85f;
        [SerializeField] float maxPitch = 85f;
        [SerializeField] float recoilRecoverySpeed = 14f;
        [SerializeField] float recoilRecoveryDelay = 0.15f;

        const float MouseScale = 0.1f;

        Camera playerCamera;
        AnimeFpsGame.Combat.Health health;
        float pitch;
        float yaw;
        Vector2 recoilOffset;
        float recoilIdleTime;

        public float Yaw => yaw;

        // Scene cameras we switched off for this session, restored when the player leaves.
        readonly List<Camera> disabledCameras = new();
        readonly List<AudioListener> disabledListeners = new();

        void Awake()
        {
            health = GetComponent<AnimeFpsGame.Combat.Health>();
            input ??= GetComponent<InputReader>();
            movement ??= GetComponent<AnimeFpsGame.Movement.CharacterMovement>();
            body ??= GetComponent<Rigidbody>();
        }

        /// <summary>Immediately sets the body facing, used after a respawn teleport.</summary>
        public void SetYaw(float degrees)
        {
            yaw = degrees;
            if (body != null)
            {
                body.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
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

            yaw = body != null ? body.rotation.eulerAngles.y : transform.eulerAngles.y;
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
            if (input == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                return;
            }

            var look = input.Look * (sensitivity * MouseScale);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            if (movement != null && cameraPivot != null)
            {
                cameraPivot.localPosition = new Vector3(0f, movement.EyeHeight, 0f);
            }

            recoilIdleTime += Time.deltaTime;
            if (recoilIdleTime > recoilRecoveryDelay)
            {
                recoilOffset = Vector2.Lerp(recoilOffset, Vector2.zero,
                    recoilRecoverySpeed * Time.deltaTime);
            }

            if (cameraPivot != null)
            {
                var recoilPitch = Mathf.Clamp(pitch + recoilOffset.y, minPitch, maxPitch);
                cameraPivot.localRotation = Quaternion.Euler(recoilPitch, 0f, 0f);
            }
        }

        void FixedUpdate()
        {
            if (body != null)
            {
                body.MoveRotation(Quaternion.Euler(0f, yaw + recoilOffset.x, 0f));
            }
        }
    }
}
