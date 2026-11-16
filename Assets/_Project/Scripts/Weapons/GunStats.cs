using System;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Tuning for one gun. Serialized into a <see cref="GunStatsAsset"/>.</summary>
    [Serializable]
    public class GunStats
    {
        public string displayName = "Gun";
        public GunArchetype archetype = GunArchetype.Rifle;
        public FireMode fireMode = FireMode.FullAuto;
        public float roundsPerMinute = 600f;
        public float damage = 24f;
        public int pellets = 1;
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadSeconds = 2.2f;
        public float range = 200f;
        public float baseSpreadDegrees = 0.25f;
        public float bloomPerShotDegrees = 0.35f;
        public float maxBloomDegrees = 3.5f;
        public float bloomRecoveryDegreesPerSecond = 8f;
        public float sprayResetSeconds = 0.4f;
        public float pelletSpreadDegrees;

        public GunStats Clone()
        {
            return (GunStats)MemberwiseClone();
        }
    }
}
