using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Fast full-auto SMG: high bloom, long steep spray.</summary>
    public class SubmachineGun : Gun
    {
        static readonly Vector2[] Pattern = BuildPattern();

        public SubmachineGun(GunStats stats) : base(stats)
        {
        }

        protected override Vector2[] SprayPattern => Pattern;

        public static GunStats CreateDefaultStats()
        {
            return new GunStats
            {
                displayName = "SMG",
                archetype = GunArchetype.SubmachineGun,
                fireMode = FireMode.FullAuto,
                roundsPerMinute = 900f,
                damage = 18f,
                pellets = 1,
                magazineSize = 32,
                reserveAmmo = 160,
                reloadSeconds = 1.8f,
                range = 80f,
                baseSpreadDegrees = 0.5f,
                bloomPerShotDegrees = 0.45f,
                maxBloomDegrees = 5f,
                bloomRecoveryDegreesPerSecond = 10f,
                sprayResetSeconds = 0.4f
            };
        }

        static Vector2[] BuildPattern()
        {
            var pattern = new Vector2[32];
            for (var i = 0; i < pattern.Length; i++)
            {
                pattern[i] = new Vector2(
                    Mathf.Sin(i * 0.42f) * 1.4f,
                    0.26f * i + 0.008f * i * i);
            }

            pattern[0] = Vector2.zero;
            return pattern;
        }
    }
}
