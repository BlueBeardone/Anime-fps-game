using System.Collections;
using AnimeFpsGame.Player;
using AnimeFpsGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class WeaponHudTests
    {
        [Test]
        public void CrosshairGapMapsSpreadToPixels()
        {
            Assert.AreEqual(6f, WeaponHud.ComputeCrosshairGap(0f, 5f), 0.01f);
            Assert.AreEqual(48f, WeaponHud.ComputeCrosshairGap(5f, 5f), 0.01f);
            Assert.Greater(WeaponHud.ComputeCrosshairGap(2.5f, 5f), 6f);
        }

        [UnityTest]
        public IEnumerator PlayerHasHudAndEffects()
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
            Assert.IsNotNull(weapons.GetComponent<WeaponHud>());
            Assert.IsNotNull(weapons.GetComponent<AnimeFpsGame.Effects.ShotEffects>());
        }

        [UnityTearDown]
        public IEnumerator TearDownNetwork()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
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
    }
}
