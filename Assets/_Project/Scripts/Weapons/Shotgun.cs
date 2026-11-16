using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Pump shotgun: eight pellets per shot, wide fixed cone, one hard kick step.</summary>
    public class Shotgun : Gun
    {
        static readonly Vector2[] Pattern =
        {
            Vector2.zero,
            new Vector2(0f, 2f)
        };

        public Shotgun(GunStats stats) : base(stats)
        {
        }

        protected override Vector2[] SprayPattern => Pattern;

        public static GunStats CreateDefaultStats()
        {
            return new GunStats
            {
                displayName = "Shotgun",
                archetype = GunArchetype.Shotgun,
                fireMode = FireMode.SemiAuto,
                roundsPerMinute = 70f,
                damage = 9f,
                pellets = 8,
                magazineSize = 6,
                reserveAmmo = 24,
                reloadSeconds = 2.6f,
                range = 40f,
                baseSpreadDegrees = 2.5f,
                bloomPerShotDegrees = 1f,
                maxBloomDegrees = 6f,
                bloomRecoveryDegreesPerSecond = 8f,
                sprayResetSeconds = 0.4f,
                pelletSpreadDegrees = 3f
            };
        }
    }
}
