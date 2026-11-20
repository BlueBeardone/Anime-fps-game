using System.Collections;
using System.Linq;
using AnimeFpsGame.Player;
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
                Assert.IsNull(pickup.GetComponent<Rigidbody>(),
                    "Scene pickups stay static; only thrown drops are physical");
            }
        }

        [Test]
        public void ThrowVelocityGoesForwardAndUp()
        {
            var velocity = WeaponPickup.ThrowVelocity(Vector3.forward);

            Assert.Greater(velocity.z, 3f, "The throw must carry the gun forward");
            Assert.Greater(velocity.y, 1f, "The throw must have some lift");
            Assert.AreEqual(0f, velocity.x, 0.001f);
        }

        [Test]
        public void FindNearestIndexPicksClosestInRange()
        {
            var candidates = new[]
            {
                new PlayerPickupInteractor.PickupCandidate(
                    new Vector3(0f, 0f, 5f), GunArchetype.Rifle, 0f, false),
                new PlayerPickupInteractor.PickupCandidate(
                    new Vector3(2f, 0f, 0f), GunArchetype.Handgun, 0f, false),
                new PlayerPickupInteractor.PickupCandidate(
                    new Vector3(0f, 0f, 1f), GunArchetype.Rifle, 0f, false),
            };

            Assert.AreEqual(2, PlayerPickupInteractor.FindNearestIndex(
                Vector3.zero, 3f, GunArchetype.Shotgun, 10f, candidates));
            Assert.AreEqual(-1, PlayerPickupInteractor.FindNearestIndex(
                Vector3.zero, 0.5f, GunArchetype.Shotgun, 10f, candidates));
        }

        [Test]
        public void FindNearestIndexSkipsCooldownAndHeldArchetype()
        {
            var candidates = new[]
            {
                // A drop that just landed is invisible to selection for its cooldown.
                new PlayerPickupInteractor.PickupCandidate(Vector3.forward, GunArchetype.Handgun, 9.5f, true),
                // A scene pickup is usable at once, but this one is the archetype already held.
                new PlayerPickupInteractor.PickupCandidate(Vector3.forward, GunArchetype.Shotgun, 0f, false),
            };

            Assert.AreEqual(-1, PlayerPickupInteractor.FindNearestIndex(
                Vector3.zero, 3f, GunArchetype.Shotgun, 10f, candidates),
                "One is cooling down and the other is the held archetype");
        }

        [UnityTest]
        public IEnumerator SpawnedPlayerTracksNearestGroundGun()
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

            var interactor = weapons.GetComponent<PlayerPickupInteractor>();
            Assert.IsNotNull(interactor, "The player must carry the pickup interactor");

            var held = weapons.CarriedArchetype((byte)weapons.CurrentSlot);
            var pickup = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held);
            weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>().enabled = false;
            Teleport(weapons, pickup.transform.position + new Vector3(0f, 1f, 1f));

            yield return null;
            yield return null;

            Assert.AreSame(pickup, interactor.NearestPickup);
            Assert.AreEqual(pickup.Archetype, interactor.NearestArchetype);
        }

        [UnityTest]
        public IEnumerator PickupFlowCarriesAmmoBothWays()
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

            var interactor = weapons.GetComponent<PlayerPickupInteractor>();
            var slot = (byte)weapons.CurrentSlot;
            var held = weapons.CarriedArchetype(slot);
            var target = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held && !p.IsRuntimeDrop);

            // Spend two rounds so the drop must carry less than a full magazine.
            var gun = weapons.ActiveGun;
            gun.TryFire(Vector3.zero, Vector3.forward, 0f, true, true, out _);
            gun.TryFire(Vector3.zero, Vector3.forward, 10f, true, true, out _);
            var outgoingMagazine = gun.AmmoInMagazine;
            var outgoingReserve = gun.ReserveAmmo;
            var incomingMagazine = target.MagazineAmmo;
            var incomingReserve = target.ReserveAmmo;
            var incomingArchetype = target.Archetype;
            var targetObject = target.NetworkObject;

            weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>().enabled = false;
            Teleport(weapons, target.transform.position + new Vector3(0f, 1f, 1f));
            yield return null;
            yield return null;

            var otherSlot = (byte)((slot + 1) % weapons.SlotCount);
            Assert.IsFalse(interactor.ApplyPickupServer(target, outgoingMagazine, outgoingReserve, otherSlot),
                "A request from a slot the player no longer holds must be rejected");
            Assert.IsTrue(target.IsSpawned, "A rejected request must not consume the ground gun");

            Assert.IsTrue(interactor.TryPickup(target), "Host pickup must apply immediately");
            yield return null;

            Assert.AreEqual(incomingArchetype, weapons.CarriedArchetype(slot));
            Assert.AreEqual(incomingMagazine, weapons.ActiveGun.AmmoInMagazine);
            Assert.AreEqual(incomingReserve, weapons.ActiveGun.ReserveAmmo);
            Assert.IsFalse(targetObject.IsSpawned, "The consumed ground gun despawns for everyone");
            Assert.IsNull(targetObject.transform.Find("Visual"),
                "A consumed pickup must not leave a visible gun behind");

            var drop = WeaponPickup.All.FirstOrDefault(p => p.IsRuntimeDrop && p.Archetype == held);
            Assert.IsNotNull(drop, "The old gun must drop at the player's feet");
            Assert.AreEqual(outgoingMagazine, drop.MagazineAmmo);
            Assert.AreEqual(outgoingReserve, drop.ReserveAmmo);
            Assert.IsTrue(drop.IsInDropCooldown, "A fresh drop must not be instantly re-grabbable");
            Assert.IsFalse(interactor.ApplyPickupServer(drop, 0, 0),
                "A pickup in cooldown must reject a second request");

            var body = drop.GetComponent<Rigidbody>();
            Assert.IsNotNull(body, "A thrown drop must be a physics object");
            Assert.Greater(drop.transform.position.y, 0.5f, "The drop must start above the ground");

            yield return new WaitForSeconds(1.5f);

            Assert.IsNotNull(drop, "The drop must survive landing");
            Assert.Greater(drop.transform.position.y, -0.05f,
                "The drop must never sink through the ground");
            Assert.Less(drop.transform.position.y, 1f,
                "The drop must come to rest on the ground");
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
    }
}
