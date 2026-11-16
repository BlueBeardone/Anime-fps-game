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
    }
}
