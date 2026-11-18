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

        [Test]
        public void OwnerShowsBodyButNotHeadOrHeldWeapon()
        {
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(true, "Jacket", "K98"));
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(true, "Throusers", "K98"));
            Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "Helmet", "K98"));
            Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "Wehrmacht_soilder_A", "K98"));
            Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "K98", "K98"));
        }

        [Test]
        public void RemoteShowsWholeBodyAndOnlyTheHeldWeapon()
        {
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Jacket", "K98"));
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Helmet", "K98"));
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Wehrmacht_soilder_A", "K98"));
            Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "K98", "K98"));
            Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(false, "Luger_P08", "K98"));
        }

        [UnityTest]
        public IEnumerator OwnerSeesBodyWithoutHeadOrHeldWeapon()
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
            Assert.IsNotNull(weapons.VisualsRoot, "Player must have a Visuals root");

            var renderers = weapons.VisualsRoot.GetComponentsInChildren<Renderer>(true);

            Renderer Named(string name)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer.gameObject.name == name)
                    {
                        return renderer;
                    }
                }

                Assert.Fail($"Missing renderer {name}");
                return null;
            }

            Assert.IsTrue(Named("Jacket").enabled, "Owner must see the jacket and its arms");
            Assert.IsTrue(Named("Throusers").enabled, "Owner must see their legs");
            Assert.IsFalse(Named("Helmet").enabled, "The helmet would swallow the camera");
            Assert.IsFalse(Named("Wehrmacht_soilder_A").enabled, "The head mesh would swallow the camera");
            Assert.IsFalse(Named("K98").enabled, "The held gun must be the viewmodel, not the body's mesh");

            var eye = weapons.ViewCamera.transform.position;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                Assert.IsFalse(renderer.bounds.Contains(eye),
                    $"{renderer.gameObject.name} is enabled and contains the camera");
            }
        }

        [UnityTest]
        public IEnumerator DeadOwnerBodyIsHidden()
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
            Assert.IsNotNull(weapons.Health, "Player must have health");
            Assert.IsNotNull(weapons.VisualsRoot, "Player must have a Visuals root");
            Assert.IsFalse(weapons.Health.IsDead);

            weapons.Health.ApplyDamage(weapons.Health.MaxHealth + 1f, weapons.transform.position,
                Vector3.up, 999);

            deadline = Time.realtimeSinceStartup + 5f;
            while (!weapons.Health.IsDead && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(weapons.Health.IsDead, "Player must die");

            foreach (var renderer in weapons.VisualsRoot.GetComponentsInChildren<Renderer>(true))
            {
                Assert.IsFalse(renderer.enabled,
                    $"{renderer.gameObject.name} must be hidden while dead");
            }

            Assert.IsFalse(weapons.CurrentViewmodel.activeInHierarchy, "The viewmodel must be hidden while dead");
        }

        [UnityTest]
        public IEnumerator EveryWeaponSlotShowsAVisibleGun()
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

            for (var slot = 0; slot < weapons.SlotCount; slot++)
            {
                weapons.SetSlot((byte)slot);
                yield return null;

                var viewmodel = weapons.CurrentViewmodel;
                Assert.IsNotNull(viewmodel, $"Slot {slot} must instantiate a viewmodel");

                var renderers = viewmodel.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers, $"Slot {slot} viewmodel must have geometry");

                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                var longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                Assert.Greater(longest, 0.1f,
                    $"Slot {slot} viewmodel is {longest:F3} m long; the placeholder scale made it invisible");

                var angle = Vector3.Angle(viewmodel.transform.forward, weapons.ViewCamera.transform.forward);
                Assert.Less(angle, 15f, $"Slot {slot} barrel points {angle:F0}° away from the camera");
            }
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
