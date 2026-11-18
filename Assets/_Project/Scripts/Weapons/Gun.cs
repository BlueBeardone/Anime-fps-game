using System;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>
    /// Shared gun loop: trigger gate, cadence, ammo, reload. Subclasses describe only their archetype.
    /// Gun instances live for as long as the player does, so ammo survives weapon switches.
    /// </summary>
    public abstract class Gun : IGun
    {
        readonly GunStats stats;

        static readonly Vector2[] NoPattern = { Vector2.zero };

        int ammoInMagazine;
        int reserveAmmo;
        bool isReloading;
        float reloadRemaining;
        float boltRemaining;
        float nextFireTime = float.NegativeInfinity;
        float bloomDegrees;
        float timeSinceLastShot = 999f;
        int sprayIndex;

        protected Gun(GunStats stats)
        {
            this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
            ResetAmmo();
        }

        public string DisplayName => stats.displayName;
        public GunStats Stats => stats;
        public int AmmoInMagazine => ammoInMagazine;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public float CurrentSpreadDegrees => stats.baseSpreadDegrees + bloomDegrees;

        protected virtual Vector2[] SprayPattern => NoPattern;
        protected virtual int PelletCount => Mathf.Max(1, stats.pellets);
        protected virtual float ShotSpreadDegrees =>
            PelletCount > 1 ? stats.pelletSpreadDegrees : CurrentSpreadDegrees;

        public float ReloadProgress01
        {
            get
            {
                if (!isReloading)
                {
                    return 0f;
                }

                if (stats.reloadSeconds <= 0f)
                {
                    return 1f;
                }

                return Mathf.Clamp01(1f - reloadRemaining / stats.reloadSeconds);
            }
        }

        public float BoltCycleProgress01 =>
            boltRemaining > 0f && stats.boltCycleSeconds > 0f
                ? Mathf.Clamp01(1f - boltRemaining / stats.boltCycleSeconds)
                : 0f;

        public event Action<Vector2> RecoilKicked;
        public event Action AmmoChanged;
        public event Action ReloadStateChanged;

        public bool CanFire(float now)
        {
            return !isReloading && ammoInMagazine > 0 && now >= nextFireTime;
        }

        /// <summary>Minimum seconds between shots, shared by the client gun loop and the server check.
        /// A configured bolt cycle wins; zero or negative values fall back to the RPM interval.</summary>
        public static float ShotIntervalSeconds(GunStats stats)
        {
            if (stats == null)
            {
                return 0f;
            }

            return stats.fireMode == FireMode.BoltAction && stats.boltCycleSeconds > 0f
                ? stats.boltCycleSeconds
                : 60f / Mathf.Max(1f, stats.roundsPerMinute);
        }

        public virtual void Tick(float deltaTime)
        {
            timeSinceLastShot += deltaTime;

            if (boltRemaining > 0f)
            {
                boltRemaining = Mathf.Max(0f, boltRemaining - deltaTime);
            }

            if (timeSinceLastShot > 0.15f)
            {
                bloomDegrees = Mathf.MoveTowards(
                    bloomDegrees, 0f, stats.bloomRecoveryDegreesPerSecond * deltaTime);
            }

            if (timeSinceLastShot > stats.sprayResetSeconds)
            {
                sprayIndex = 0;
            }

            if (!isReloading)
            {
                return;
            }

            reloadRemaining -= deltaTime;
            if (reloadRemaining <= 0f)
            {
                CompleteReload();
            }
        }

        public bool TryFire(Vector3 origin, Vector3 aimForward, float now, bool triggerPressed,
            bool triggerHeld, out ShotSpec shot)
        {
            shot = default;

            if (!CanFire(now))
            {
                return false;
            }

            var boltAction = stats.fireMode == FireMode.BoltAction;

            if ((stats.fireMode == FireMode.SemiAuto || boltAction) && !triggerPressed)
            {
                return false;
            }

            if (stats.fireMode == FireMode.FullAuto && !triggerHeld)
            {
                return false;
            }

            if (timeSinceLastShot > stats.sprayResetSeconds)
            {
                sprayIndex = 0;
            }

            var kick = Vector2.zero;
            if (!boltAction)
            {
                var pattern = SprayPattern;
                var index = Mathf.Clamp(sprayIndex, 0, pattern.Length - 1);
                var previous = index > 0 ? pattern[index - 1] : Vector2.zero;
                kick = pattern[index] - previous;
                sprayIndex = Mathf.Min(sprayIndex + 1, pattern.Length - 1);
            }

            ammoInMagazine--;
            nextFireTime = now + ShotIntervalSeconds(stats);
            timeSinceLastShot = 0f;
            bloomDegrees = Mathf.Min(stats.maxBloomDegrees, bloomDegrees + stats.bloomPerShotDegrees);

            if (boltAction)
            {
                boltRemaining = stats.boltCycleSeconds;
                sprayIndex = 0;
                kick = stats.boltRecoilDegrees;
            }

            shot = new ShotSpec
            {
                Origin = origin,
                Directions = BuildDirections(aimForward),
                Damage = stats.damage,
                Range = stats.range
            };

            RaiseRecoil(kick);
            AmmoChanged?.Invoke();

            if (ammoInMagazine <= 0 && reserveAmmo > 0)
            {
                TryStartReload();
            }

            return true;
        }

        public bool TryStartReload()
        {
            if (isReloading || ammoInMagazine >= stats.magazineSize || reserveAmmo <= 0)
            {
                return false;
            }

            isReloading = true;
            reloadRemaining = stats.reloadSeconds;
            sprayIndex = 0;
            ReloadStateChanged?.Invoke();

            if (reloadRemaining <= 0f)
            {
                CompleteReload();
            }

            return true;
        }

        public void CancelReload()
        {
            // A slot switch cancels the reload and must also start a fresh spray pattern.
            sprayIndex = 0;

            if (!isReloading)
            {
                return;
            }

            isReloading = false;
            reloadRemaining = 0f;
            ReloadStateChanged?.Invoke();
        }

        public void ResetAmmo()
        {
            ammoInMagazine = stats.magazineSize;
            reserveAmmo = stats.reserveAmmo;
            isReloading = false;
            reloadRemaining = 0f;
            boltRemaining = 0f;
            nextFireTime = float.NegativeInfinity;
            bloomDegrees = 0f;
            timeSinceLastShot = 999f;
            sprayIndex = 0;
            AmmoChanged?.Invoke();
            ReloadStateChanged?.Invoke();
        }

        void CompleteReload()
        {
            var needed = stats.magazineSize - ammoInMagazine;
            var loaded = Mathf.Min(needed, reserveAmmo);
            ammoInMagazine += loaded;
            reserveAmmo -= loaded;
            isReloading = false;
            reloadRemaining = 0f;
            sprayIndex = 0;
            ReloadStateChanged?.Invoke();
            AmmoChanged?.Invoke();
        }

        /// <summary>One direction per pellet, scattered inside the current cone.</summary>
        protected virtual Vector3[] BuildDirections(Vector3 aimForward)
        {
            var forward = aimForward.normalized;
            var count = PelletCount;
            var spread = Mathf.Max(0f, ShotSpreadDegrees);
            var directions = new Vector3[count];

            for (var i = 0; i < count; i++)
            {
                if (spread <= 0f)
                {
                    directions[i] = forward;
                    continue;
                }

                var offset = UnityEngine.Random.insideUnitCircle * spread;
                directions[i] = (Quaternion.Euler(-offset.y, offset.x, 0f) * forward).normalized;
            }

            return directions;
        }

        protected void RaiseRecoil(Vector2 degrees)
        {
            RecoilKicked?.Invoke(degrees);
        }
    }
}
