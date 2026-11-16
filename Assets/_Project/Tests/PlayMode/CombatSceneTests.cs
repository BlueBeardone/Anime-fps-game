using System.Collections;
using AnimeFpsGame.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class CombatSceneTests
    {
        [UnityTest]
        public IEnumerator HostDamageUpdatesAndRespawnsADummy()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            TargetDummy dummy = null;
            while (dummy == null && Time.realtimeSinceStartup < deadline)
            {
                dummy = Object.FindAnyObjectByType<TargetDummy>();
                yield return null;
            }

            Assert.IsNotNull(dummy, "Main must contain a target dummy");
            var health = dummy.GetComponent<Health>();
            Assert.AreEqual(100f, health.CurrentHealth, 0.01f);

            health.ApplyDamage(60f, dummy.transform.position, Vector3.up, 0);
            yield return null;
            Assert.AreEqual(40f, health.CurrentHealth, 0.01f);

            health.ApplyDamage(200f, dummy.transform.position, Vector3.up, 0);
            yield return null;
            Assert.IsTrue(health.IsDead);
            Assert.IsFalse(dummy.GetComponentInChildren<Renderer>().enabled, "Dead dummy is hidden");

            var reviveDeadline = Time.realtimeSinceStartup + 4.5f;
            while (health.IsDead && Time.realtimeSinceStartup < reviveDeadline)
                yield return null;

            Assert.IsFalse(health.IsDead, "Dummy must respawn");
            Assert.AreEqual(100f, health.CurrentHealth, 0.01f);
            Assert.IsTrue(dummy.GetComponentInChildren<Renderer>().enabled);
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
