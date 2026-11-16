using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>The result of one successful trigger pull.</summary>
    public struct ShotSpec
    {
        public byte Slot;
        public Vector3 Origin;
        public Vector3[] Directions;
        public float Damage;
        public float Range;
    }
}
