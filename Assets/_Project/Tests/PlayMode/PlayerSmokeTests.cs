using System.Collections;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using AnimeFpsGame.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    /// <summary>
    /// Headless smoke tests: loading Main starts a solo local session, NGO spawns the player prefab,
    /// and the weapon slot network variable changes when the owner asks for the pistol.
    /// </summary>
    public class PlayerSmokeTests
    {
        /// <summary>
        /// NGO marks NetworkManager DontDestroyOnLoad, so it survives the next LoadScene in the same
        /// PlayMode run. Destroy it between tests or the next test reuses a shut-down singleton.
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
        public IEnumerator RangeSpawnsAPlayerAndSwapsToThePistol()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.IsNotNull(NetworkManager.Singleton, "Main scene must contain a NetworkManager");

            var bootstrap = Object.FindAnyObjectByType<Networking.NetworkBootstrap>();
            Assert.IsNotNull(bootstrap, "NetworkBootstrap must be on the NetworkManager");
            var connectDeadline = Time.realtimeSinceStartup + 5f;
            while (!bootstrap.IsConnected && Time.realtimeSinceStartup < connectDeadline)
            {
                yield return null;
            }
            Assert.IsTrue(bootstrap.IsConnected, "Main must auto-start a solo host");

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
        }

        [UnityTest]
        public IEnumerator MainSceneAutoHostsWithoutHostJoinMenu()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<AnimeFpsGame.Networking.HostJoinMenu>(),
                "The Range must not show the host/join panel");
            var bootstrap = Object.FindAnyObjectByType<AnimeFpsGame.Networking.NetworkBootstrap>();
            Assert.IsNotNull(bootstrap, "Main scene must contain a NetworkBootstrap");
            var connectDeadline = Time.realtimeSinceStartup + 5f;
            while (!bootstrap.IsConnected && Time.realtimeSinceStartup < connectDeadline)
            {
                yield return null;
            }
            Assert.IsTrue(bootstrap.IsConnected, "Main must auto-start a solo host");

            var deadline = Time.realtimeSinceStartup + 10f;
            while (Object.FindAnyObjectByType<AnimeFpsGame.Player.PlayerWeapons>() == null &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsNotNull(Object.FindAnyObjectByType<AnimeFpsGame.Player.PlayerWeapons>(),
                "Solo host must spawn the player");
        }

        [UnityTest]
        public IEnumerator HostFailureShowsAVisibleNotice()
        {
            var blocker = new UdpClient(7777);
            try
            {
                // Unity Transport reports the bind failure as an error; that is the condition under test.
                LogAssert.Expect(LogType.Error, new Regex("Failed to bind UDP socket.*port 7777"));
                LogAssert.Expect(LogType.Error, new Regex("Server failed to bind.*"));
                LogAssert.Expect(LogType.Error, new Regex(".*network transport start failure.*"));

                SceneManager.LoadScene("Main");

                var notice = default(RangeNotice);
                var deadline = Time.realtimeSinceStartup + 8f;
                while (notice == null && Time.realtimeSinceStartup < deadline)
                {
                    notice = Object.FindAnyObjectByType<RangeNotice>();
                    yield return null;
                }

                Assert.IsNotNull(notice, "A failed solo host must show a visible notice");
                StringAssert.Contains("7777", notice.Message, "The notice must name the blocked port");
            }
            finally
            {
                blocker.Dispose();
            }
        }
    }
}
