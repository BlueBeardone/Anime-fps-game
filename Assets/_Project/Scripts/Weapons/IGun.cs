using System;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>The behaviour every weapon type exposes to the player runtime.</summary>
    public interface IGun
    {
        string DisplayName { get; }
        GunStats Stats { get; }
        int AmmoInMagazine { get; }
        int ReserveAmmo { get; }
        bool IsReloading { get; }
        float ReloadProgress01 { get; }
        float BoltCycleProgress01 { get; }
        float CurrentSpreadDegrees { get; }
        bool CanFire(float now);

        void Tick(float deltaTime);
        bool TryFire(Vector3 origin, Vector3 aimForward, float now, bool triggerPressed,
            bool triggerHeld, out ShotSpec shot);
        bool TryStartReload();
        void CancelReload();
        void ResetAmmo();
        void RestoreAmmo(int magazine, int reserve);

        event Action<Vector2> RecoilKicked;
        event Action AmmoChanged;
        event Action ReloadStateChanged;
    }
}
