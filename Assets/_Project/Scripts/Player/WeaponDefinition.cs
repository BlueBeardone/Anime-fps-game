using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>
    /// A weapon slot. The soldier prefab already carries every gun as a skinned mesh, so a slot is
    /// "which mesh to show, which animator int to set, and which viewmodel to hold in first person".
    /// </summary>
    [System.Serializable]
    public class WeaponDefinition
    {
        public string displayName = "K98";
        public string animatorInt = "status_k98";
        public string soldierMeshName = "K98";
        public GameObject viewmodelPrefab;
        public Vector3 viewmodelLocalPosition = new Vector3(0.22f, -0.2f, 0.45f);
        public Vector3 viewmodelLocalEuler = new Vector3(0f, 90f, 0f);
        public float viewmodelScale = 0.08f;
    }
}
