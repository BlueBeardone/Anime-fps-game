using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>Finds the muzzle in a viewmodel's local space: a child named Muzzle wins, otherwise the
    /// centre of the combined renderer bounds' forward-most cross-section. Centring on the bore keeps
    /// the tracer on the barrel instead of a bounding-box corner. The fallback keeps effect spawning
    /// safe for art without a marker.</summary>
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

            if (!TryComputeLocalBounds(viewmodel, out var min, out var max))
            {
                return Fallback;
            }

            return new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z);
        }

        /// <summary>Combined renderer bounds expressed in the viewmodel root's local space.</summary>
        public static bool TryComputeLocalBounds(GameObject viewmodel, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            if (viewmodel == null)
            {
                return false;
            }

            var root = viewmodel.transform;
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
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

                hasBounds = true;
            }

            return hasBounds;
        }
    }
}
