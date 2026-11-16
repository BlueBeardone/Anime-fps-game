using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Persistent tuning for one gun slot, so Inspector edits survive ProjectSetup re-runs.</summary>
    [CreateAssetMenu(fileName = "GunStats", menuName = "Anime FPS/Gun Stats")]
    public class GunStatsAsset : ScriptableObject
    {
        public GunStats stats = new GunStats();
    }
}
