using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    /// <summary>
    /// Headless smoke test: the Main scene hosts, NGO spawns the player prefab, and the weapon
    /// slot network variable changes when the owner asks for the pistol.
    /// </summary>
    public class PlayerSmokeTests
    {
        [UnityTest]
        public IEnumerator HostSpawnsAPlayerAndSwapsToThePistol()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.IsNotNull(NetworkManager.Singleton, "Main scene must contain a NetworkManager");

            var bootstrap = Object.FindAnyObjectByType<Networking.NetworkBootstrap>();
            Assert.IsNotNull(bootstrap, "NetworkBootstrap must be on the NetworkManager");

            bootstrap.Host();

            var weapons = default(Player.PlayerWeapons);
            var deadline = Time.realtimeSinceStartup + 15f;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<Player.PlayerWeapons>();
                yield return null;
            }
            Assert.IsNotNull(weapons, "Hosting must spawn the player prefab");
            Assert.AreEqual(0, weapons.CurrentSlot, "Weapon slot 0 is the main weapon (K98)");

            var motor = weapons.GetComponent<Player.PlayerMotor>();
            Assert.IsNotNull(motor, "Spawned player has PlayerMotor");
            Assert.IsNotNull(weapons.GetComponent<Player.PlayerLook>(), "Spawned player has PlayerLook");
            Assert.IsNotNull(weapons.GetComponent<UnityEngine.InputSystem.PlayerInput>(), "Spawned player has PlayerInput");

            weapons.SetSlot(1);
            yield return null;
            Assert.AreEqual(1, weapons.CurrentSlot, "Owner can switch to weapon slot 1 (Luger)");
            Assert.IsNotNull(weapons.CurrentViewmodel, "Switching to the pistol instantiates its viewmodel");
            StringAssert.Contains("Luger", weapons.CurrentViewmodel.name, "Luger viewmodel is the one shown");

            NetworkManager.Singleton.Shutdown();
            yield return null;
        }
    }
}
