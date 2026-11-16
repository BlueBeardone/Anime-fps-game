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

        int ammoInMagazine;
        int reserveAmmo;
        bool isReloading;
        float reloadRemaining;
        float nextFireTime = float.NegativeInfinity;

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
        public float CurrentSpreadDegrees => stats.baseSpreadDegrees;

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

        public event Action<Vector2> RecoilKicked;
        public event Action AmmoChanged;
        public event Action ReloadStateChanged;

        public bool CanFire(float now)
        {
            return !isReloading && ammoInMagazine > 0 && now >= nextFireTime;
        }

        public virtual void Tick(float deltaTime)
        {
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

            if (stats.fireMode == FireMode.SemiAuto && !triggerPressed)
            {
                return false;
            }

            if (stats.fireMode == FireMode.FullAuto && !triggerHeld)
            {
                return false;
            }

            ammoInMagazine--;
            nextFireTime = now + 60f / Mathf.Max(1f, stats.roundsPerMinute);

            shot = new ShotSpec
            {
                Origin = origin,
                Directions = new[] { aimForward.normalized },
                Damage = stats.damage,
                Range = stats.range
            };

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
            ReloadStateChanged?.Invoke();

            if (reloadRemaining <= 0f)
            {
                CompleteReload();
            }

            return true;
        }

        public void CancelReload()
        {
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
            nextFireTime = float.NegativeInfinity;
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
            ReloadStateChanged?.Invoke();
            AmmoChanged?.Invoke();
        }

        protected void RaiseRecoil(Vector2 degrees)
        {
            RecoilKicked?.Invoke(degrees);
        }
    }
}
