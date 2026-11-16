using AnimeFpsGame.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AnimeFpsGame.Tests
{
    public class GunTests
    {
        static GunStats TestStats()
        {
            var stats = Rifle.CreateDefaultStats();
            stats.baseSpreadDegrees = 0f;
            stats.bloomPerShotDegrees = 0f;
            stats.magazineSize = 3;
            stats.reserveAmmo = 6;
            stats.reloadSeconds = 1f;
            stats.roundsPerMinute = 600f;
            return stats;
        }

        static bool Fire(Gun gun, float now, bool pressed = true, bool held = true)
            => gun.TryFire(Vector3.zero, Vector3.forward, now, pressed, held, out _);

        [Test]
        public void FireConsumesRoundAndRespectsCadence()
        {
            var gun = new Rifle(TestStats());
            Assert.IsTrue(Fire(gun, 0f));
            Assert.AreEqual(2, gun.AmmoInMagazine);
            Assert.IsFalse(Fire(gun, 0.05f));
            Assert.IsTrue(Fire(gun, 0.11f));
        }

        [Test]
        public void SemiAutoFiresOnlyOnPress()
        {
            var stats = TestStats();
            stats.fireMode = FireMode.SemiAuto;
            var gun = new Rifle(stats);
            Assert.IsFalse(Fire(gun, 0f, pressed: false, held: true));
            Assert.IsTrue(Fire(gun, 0f));
            Assert.IsFalse(Fire(gun, 0.2f, pressed: false, held: true));
        }

        [Test]
        public void ReloadTakesTimeAndMovesReserve()
        {
            var gun = new Rifle(TestStats());
            Fire(gun, 0f);
            Assert.IsTrue(gun.TryStartReload());
            gun.Tick(0.99f);
            Assert.IsTrue(gun.IsReloading);
            gun.Tick(0.02f);
            Assert.IsFalse(gun.IsReloading);
            Assert.AreEqual(3, gun.AmmoInMagazine);
            Assert.AreEqual(5, gun.ReserveAmmo);
        }

        [Test]
        public void AutoReloadStartsOnceWhenEmpty()
        {
            var gun = new Rifle(TestStats());
            Fire(gun, 0f);
            Fire(gun, 0.11f);
            Fire(gun, 0.22f);
            Assert.AreEqual(0, gun.AmmoInMagazine);
            Assert.IsTrue(gun.IsReloading, "Empty magazine must start a reload");
            gun.Tick(1.01f);
            Assert.AreEqual(3, gun.AmmoInMagazine);
            Assert.AreEqual(3, gun.ReserveAmmo);
            gun.Tick(1f);
            Assert.AreEqual(3, gun.AmmoInMagazine, "A second reload must not run automatically");
        }

        [Test]
        public void PartialReserveLoadsOnlyAvailable()
        {
            var stats = TestStats();
            stats.reserveAmmo = 1;
            var gun = new Rifle(stats);
            Fire(gun, 0f);
            Fire(gun, 0.11f);
            Assert.AreEqual(1, gun.AmmoInMagazine);
            Assert.IsTrue(gun.TryStartReload());
            gun.Tick(1.01f);
            Assert.AreEqual(2, gun.AmmoInMagazine);
            Assert.AreEqual(0, gun.ReserveAmmo);
        }

        [Test]
        public void NoReloadWhenFullOrReserveEmpty()
        {
            var full = new Rifle(TestStats());
            Assert.IsFalse(full.TryStartReload());

            var stats = TestStats();
            stats.reserveAmmo = 0;
            var emptyReserve = new Rifle(stats);
            Fire(emptyReserve, 0f);
            Assert.IsFalse(emptyReserve.TryStartReload());
        }

        [Test]
        public void CancelReloadKeepsAmmo()
        {
            var gun = new Rifle(TestStats());
            Fire(gun, 0f);
            Assert.IsTrue(gun.TryStartReload());
            gun.CancelReload();
            gun.Tick(2f);
            Assert.IsFalse(gun.IsReloading);
            Assert.AreEqual(2, gun.AmmoInMagazine);
        }

        [Test]
        public void BloomGrowsClampsAndRecovers()
        {
            var stats = TestStats();
            stats.baseSpreadDegrees = 0f;
            stats.bloomPerShotDegrees = 1f;
            stats.maxBloomDegrees = 2f;
            stats.bloomRecoveryDegreesPerSecond = 10f;
            stats.roundsPerMinute = 6000f;
            stats.magazineSize = 20;
            stats.reserveAmmo = 0;
            var gun = new Rifle(stats);

            Fire(gun, 0f);
            Assert.AreEqual(1f, gun.CurrentSpreadDegrees, 0.01f);
            for (var i = 1; i <= 5; i++)
            {
                Fire(gun, i * 0.02f);
            }

            Assert.AreEqual(2f, gun.CurrentSpreadDegrees, 0.01f, "Bloom must clamp at max");
            gun.Tick(0.2f);
            Assert.Less(gun.CurrentSpreadDegrees, 2f, "Bloom must recover after firing stops");
        }

        [Test]
        public void SprayKicksOnSecondShotAndResetsAfterGap()
        {
            var gun = new Rifle(TestStats());
            var kicks = new System.Collections.Generic.List<Vector2>();
            gun.RecoilKicked += kicks.Add;

            Fire(gun, 0f);
            Assert.AreEqual(Vector2.zero, kicks[0], "First shot has no pattern kick");
            Fire(gun, 0.11f);
            Assert.AreNotEqual(Vector2.zero, kicks[1], "Second shot follows the pattern");
            gun.Tick(0.5f);
            Fire(gun, 0.7f);
            Assert.AreEqual(Vector2.zero, kicks[2], "Spray index resets after the delay");
        }

        [Test]
        public void ShotgunEmitsConfiguredPelletsWithinCone()
        {
            var stats = Shotgun.CreateDefaultStats();
            stats.pellets = 8;
            stats.pelletSpreadDegrees = 3f;
            var gun = new Shotgun(stats);

            Assert.IsTrue(gun.TryFire(Vector3.zero, Vector3.forward, 0f, true, true, out var shot));
            Assert.AreEqual(8, shot.Directions.Length);
            foreach (var direction in shot.Directions)
            {
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, direction), 3.01f);
            }
        }

        [Test]
        public void ShotgunDamageIsPerPellet()
        {
            var stats = Shotgun.CreateDefaultStats();
            stats.damage = 9f;
            var gun = new Shotgun(stats);
            Assert.IsTrue(gun.TryFire(Vector3.zero, Vector3.forward, 0f, true, true, out var shot));
            Assert.AreEqual(9f, shot.Damage, 0.001f);
        }

        [Test]
        public void FactoryCreatesTheConfiguredArchetype()
        {
            Assert.IsInstanceOf<Rifle>(GunFactory.Create(Rifle.CreateDefaultStats()));
            Assert.IsInstanceOf<Handgun>(GunFactory.Create(Handgun.CreateDefaultStats()));
            Assert.IsInstanceOf<Shotgun>(GunFactory.Create(Shotgun.CreateDefaultStats()));
            Assert.IsInstanceOf<SubmachineGun>(GunFactory.Create(SubmachineGun.CreateDefaultStats()));
        }

        [Test]
        public void GunStatsAssetHoldsStats()
        {
            var asset = ScriptableObject.CreateInstance<GunStatsAsset>();
            asset.stats = Handgun.CreateDefaultStats();
            Assert.AreEqual(GunArchetype.Handgun, asset.stats.archetype);
            Object.DestroyImmediate(asset);
        }
    }
}
