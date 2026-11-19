using System.Collections;
using System.Linq;
using AnimeFpsGame.Player;
using AnimeFpsGame.UI;
using AnimeFpsGame.Weapons;
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

        [UnityTest]
        public IEnumerator PickupPromptShowsNameAndAction()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            PlayerWeapons weapons = null;
            while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            var hud = weapons.GetComponent<WeaponHud>();
            var movement = weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>();
            movement.enabled = false;

            // Pickups sit near the spawn points, so step away first to prove the prompt is proximity-based.
            Teleport(weapons, new Vector3(0f, 1f, 30f));
            yield return null;
            yield return null;
            Assert.IsFalse(hud.PickupPromptVisible, "No prompt when nothing is close");

            var held = weapons.CarriedArchetype((byte)weapons.CurrentSlot);
            var pickup = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held);
            Teleport(weapons, pickup.transform.position + new Vector3(0f, 1f, 1f));
            yield return null;
            yield return null;

            Assert.IsTrue(hud.PickupPromptVisible);
            Assert.AreEqual("Press F to switch", hud.PickupPromptActionText);
            Assert.AreEqual(weapons.DefinitionFor(pickup.Archetype).displayName, hud.PickupPromptNameText);
            Assert.AreEqual(pickup.Archetype, hud.PreviewArchetype);

            Teleport(weapons, new Vector3(0f, 1f, 30f));
            yield return null;
            yield return null;
            Assert.IsFalse(hud.PickupPromptVisible, "Walking away hides the prompt");
        }

        static void Teleport(PlayerWeapons weapons, Vector3 position)
        {
            var body = weapons.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
            }

            weapons.transform.position = position;
            Physics.SyncTransforms();
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
