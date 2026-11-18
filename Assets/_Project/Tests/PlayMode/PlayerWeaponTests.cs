using System.Collections;
using AnimeFpsGame.Player;
using AnimeFpsGame.Weapons;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class PlayerWeaponTests
    {
        /// <summary>
        /// NGO's NetworkManager is DontDestroyOnLoad and each test loads Main, which auto-hosts on
        /// UDP 7777. Shut the host down between tests or the next bind fails.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownNetwork()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.Shutdown();
            }

            yield return null;

            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerHasFourGunsAndReloadAction()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            Assert.IsNotNull(weapons, "Player must spawn");
            Assert.AreEqual(4, weapons.SlotCount);
            Assert.IsNotNull(weapons.ActiveGun);
            Assert.AreEqual(GunArchetype.Rifle, weapons.ActiveGun.Stats.archetype);

            var input = weapons.GetComponent<PlayerInput>();
            Assert.IsNotNull(input.actions["Reload"], "Reload action must exist");
            Assert.IsNotNull(weapons.GetComponent<AnimeFpsGame.Combat.Health>());
        }

        [UnityTest]
        public IEnumerator AmmoSurvivesWeaponSwitch()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            var gun = weapons.ActiveGun;
            Assert.IsTrue(gun.TryFire(Vector3.zero, Vector3.forward, Time.time, true, true, out _));
            var afterShot = gun.AmmoInMagazine;
            Assert.IsTrue(gun.TryStartReload());

            weapons.SetSlot(1);
            yield return null;
            Assert.IsFalse(gun.IsReloading, "Switching away cancels the reload");

            weapons.SetSlot(0);
            yield return null;

            Assert.AreSame(gun, weapons.ActiveGun, "Slot 0 keeps its gun instance");
            Assert.AreEqual(afterShot, weapons.ActiveGun.AmmoInMagazine);
        }

        [UnityTest]
        public IEnumerator RifleViewmodelIsVisibleInFrontOfCamera()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            Assert.IsNotNull(weapons, "Player must spawn");
            Assert.IsNotNull(weapons.CurrentViewmodel, "The owner must hold a viewmodel");
            Assert.IsTrue(weapons.CurrentViewmodel.activeInHierarchy);

            var renderers = weapons.CurrentViewmodel.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, "The viewmodel must have visible geometry");

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var planes = GeometryUtility.CalculateFrustumPlanes(weapons.ViewCamera);
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, bounds),
                "The rifle must be inside the camera frustum");

            var angle = Vector3.Angle(
                weapons.CurrentViewmodel.transform.forward,
                weapons.ViewCamera.transform.forward);
            Assert.Less(angle, 15f, "The barrel must point where the camera looks");
        }

        [UnityTest]
        public IEnumerator MuzzleFallsBackInFrontWhenViewmodelIsBehindCamera()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            Assert.IsNotNull(weapons);
            Assert.IsNotNull(weapons.CurrentViewmodel);

            // Stop the feel update from restoring the slot position for this frame.
            weapons.enabled = false;
            weapons.CurrentViewmodel.transform.localPosition = new Vector3(0f, 0f, -1f);

            var camera = weapons.ViewCamera.transform;
            var toMuzzle = weapons.MuzzleWorldPosition - camera.position;

            Assert.Greater(Vector3.Dot(toMuzzle, camera.forward), 0f,
                "A muzzle behind the camera must fall back to a point in front of it");
        }

        [UnityTest]
        public IEnumerator MuzzleIsInFrontOfTheCamera()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            Assert.IsNotNull(weapons);

            var camera = weapons.ViewCamera.transform;
            var toMuzzle = weapons.MuzzleWorldPosition - camera.position;

            Assert.Greater(Vector3.Dot(toMuzzle, camera.forward), 0f,
                "The muzzle must be in front of the camera");
            Assert.Less(toMuzzle.magnitude, 3f,
                "The muzzle must be on the held gun, not across the map");
        }
    }
}
