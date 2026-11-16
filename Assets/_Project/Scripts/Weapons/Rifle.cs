using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Full-auto rifle: one ray, moderate bloom, long learnable spray.</summary>
    public class Rifle : Gun
    {
        static readonly Vector2[] Pattern = BuildPattern();

        public Rifle(GunStats stats) : base(stats)
        {
        }

        protected override Vector2[] SprayPattern => Pattern;

        public static GunStats CreateDefaultStats()
        {
            return new GunStats
            {
                displayName = "Rifle",
                archetype = GunArchetype.Rifle,
                fireMode = FireMode.FullAuto,
                roundsPerMinute = 600f,
                damage = 24f,
                pellets = 1,
                magazineSize = 30,
                reserveAmmo = 120,
                reloadSeconds = 2.2f,
                range = 200f,
                baseSpreadDegrees = 0.25f,
                bloomPerShotDegrees = 0.35f,
                maxBloomDegrees = 3.5f,
                bloomRecoveryDegreesPerSecond = 8f,
                sprayResetSeconds = 0.4f
            };
        }

        /// <summary>Climbs with a gentle left/right weave. Values are tuning, not physics.</summary>
        static Vector2[] BuildPattern()
        {
            var pattern = new Vector2[30];
            for (var i = 0; i < pattern.Length; i++)
            {
                pattern[i] = new Vector2(
                    Mathf.Sin(i * 0.55f) * 0.9f,
                    0.38f * i + 0.004f * i * i);
            }

            pattern[0] = Vector2.zero;
            return pattern;
        }
    }
}
