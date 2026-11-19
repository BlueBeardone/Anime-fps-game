using System.Collections;
using System.Linq;
using AnimeFpsGame.Weapons;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class WeaponPickupTests
    {
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
        public IEnumerator MainHasFourDistinctGroundGuns()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            while (WeaponPickup.All.Count(p => p.IsSpawned) < 4 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            var spawned = WeaponPickup.All.Where(p => p.IsSpawned).ToArray();
            Assert.AreEqual(4, spawned.Length, "The range must have four ground guns");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    GunArchetype.Rifle, GunArchetype.Handgun,
                    GunArchetype.Shotgun, GunArchetype.SubmachineGun
                },
                spawned.Select(p => p.Archetype).ToArray());

            foreach (var pickup in spawned)
            {
                Assert.IsFalse(pickup.IsRuntimeDrop, "Scene pickups are not runtime drops");
                Assert.Greater(pickup.MagazineAmmo, 0, $"{pickup.Archetype} must have loaded rounds");
                var renderers = pickup.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers, $"{pickup.Archetype} must show a gun mesh");
            }
        }
    }
}
