using System;

namespace AnimeFpsGame.Weapons
{
    public static class GunFactory
    {
        public static IGun Create(GunStats stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            return stats.archetype switch
            {
                GunArchetype.Rifle => new Rifle(stats),
                GunArchetype.Handgun => new Handgun(stats),
                GunArchetype.Shotgun => new Shotgun(stats),
                GunArchetype.SubmachineGun => new SubmachineGun(stats),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(stats), stats.archetype, "Unknown gun archetype")
            };
        }
    }
}
