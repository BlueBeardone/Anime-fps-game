using AnimeFpsGame.Weapons;
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// A weapon slot: which gun runs, which mesh other players see, which animator int to set, and
    /// which first-person viewmodel to hold.
    /// </summary>
    [System.Serializable]
    public class WeaponDefinition
    {
        public string displayName = "Rifle";
        public string animatorInt = "status_k98";
        public string soldierMeshName = "K98";
        public GameObject viewmodelPrefab;
        public Vector3 viewmodelLocalPosition = new Vector3(0.22f, -0.2f, 0.45f);
        public Vector3 viewmodelLocalEuler = new Vector3(0f, 90f, 0f);
        public float viewmodelScale = 0.08f;

        /// <summary>Barrel tip in viewmodel-root local space. Effects and tracers start here.
        /// Zero falls back to a child named Muzzle or the combined bounds' forward centre.</summary>
        public Vector3 muzzleLocalPosition;
        public GunStatsAsset stats;
    }
}
