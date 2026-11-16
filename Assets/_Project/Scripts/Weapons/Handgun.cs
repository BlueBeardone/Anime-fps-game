using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Semi-auto pistol: one ray, short punchy pattern.</summary>
    public class Handgun : Gun
    {
        static readonly Vector2[] Pattern = BuildPattern();

        public Handgun(GunStats stats) : base(stats)
        {
        }

        protected override Vector2[] SprayPattern => Pattern;

        public static GunStats CreateDefaultStats()
        {
            return new GunStats
            {
                displayName = "Handgun",
                archetype = GunArchetype.Handgun,
                fireMode = FireMode.SemiAuto,
                roundsPerMinute = 400f,
                damage = 30f,
                pellets = 1,
                magazineSize = 12,
                reserveAmmo = 60,
                reloadSeconds = 1.5f,
                range = 100f,
                baseSpreadDegrees = 0.2f,
                bloomPerShotDegrees = 0.7f,
                maxBloomDegrees = 3f,
                bloomRecoveryDegreesPerSecond = 8f,
                sprayResetSeconds = 0.4f
            };
        }

        static Vector2[] BuildPattern()
        {
            var pattern = new Vector2[8];
            for (var i = 0; i < pattern.Length; i++)
            {
                pattern[i] = new Vector2(Mathf.Sin(i * 0.8f) * 0.5f, 0.65f * i);
            }

            pattern[0] = Vector2.zero;
            return pattern;
        }
    }
}
