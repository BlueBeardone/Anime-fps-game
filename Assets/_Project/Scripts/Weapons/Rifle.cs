namespace AnimeFpsGame.Weapons
{
    /// <summary>Full-auto rifle: one ray, moderate bloom, long learnable spray.</summary>
    public class Rifle : Gun
    {
        public Rifle(GunStats stats) : base(stats)
        {
        }

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
    }
}
