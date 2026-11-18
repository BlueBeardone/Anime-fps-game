using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>Finds the muzzle in a viewmodel's local space: a child named Muzzle wins, otherwise the
    /// most forward corner of the combined renderer bounds. The fallback keeps effect spawning safe for
    /// art without a marker.</summary>
    public static class ViewmodelMuzzle
    {
        static readonly Vector3 Fallback = new Vector3(0f, 0f, 0.5f);

        public static Vector3 ComputeLocal(GameObject viewmodel)
        {
            if (viewmodel == null)
            {
                return Fallback;
            }

            var root = viewmodel.transform;
            var marker = root.Find("Muzzle");
            if (marker != null)
            {
                return root.InverseTransformPoint(marker.position);
            }

            var best = Fallback;
            var bestZ = float.NegativeInfinity;
            var hasBounds = false;

            foreach (var renderer in viewmodel.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var local = root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (local.z > bestZ)
                    {
                        bestZ = local.z;
                        best = local;
                    }
                }

                hasBounds = true;
            }

            return hasBounds ? best : Fallback;
        }
    }
}
