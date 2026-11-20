using System.Collections.Generic;
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>Finds the muzzle in a viewmodel's local space: a child named Muzzle wins, otherwise the
    /// centroid of the forward-most mesh vertices (the barrel face). Averaging the actual bore keeps
    /// the tracer on the barrel instead of a bounds corner or a magazine-heavy cross-section. The
    /// fallback keeps effect spawning safe for art without readable meshes.</summary>
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

            if (TryComputeBoreLocal(viewmodel, out var bore))
            {
                return bore;
            }

            if (!TryComputeLocalBounds(viewmodel, out var min, out var max))
            {
                return Fallback;
            }

            return new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z);
        }

        /// <summary>Centroid of the vertices in the forward-most slab of readable meshes, expressed in
        /// the viewmodel root's local space. For a gun this is the barrel face, not the grip.</summary>
        public static bool TryComputeBoreLocal(GameObject viewmodel, out Vector3 local)
        {
            local = Fallback;
            if (viewmodel == null)
            {
                return false;
            }

            var root = viewmodel.transform;
            var points = new List<Vector3>();
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;

            foreach (var filter in viewmodel.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable)
                {
                    continue;
                }

                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var point = root.InverseTransformPoint(
                        filter.transform.TransformPoint(vertices[i]));
                    points.Add(point);
                    minZ = Mathf.Min(minZ, point.z);
                    maxZ = Mathf.Max(maxZ, point.z);
                }
            }

            if (points.Count == 0)
            {
                return false;
            }

            // Look only at the tip: the forward 1% of the gun's length keeps the barrel face while
            // ignoring the receiver, magazine and stock.
            var depth = maxZ - minZ;
            var threshold = Mathf.Max(0.001f, depth * 0.01f);
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].z < maxZ - threshold)
                {
                    continue;
                }

                sum += points[i];
                count++;
            }

            local = sum / count;
            return true;
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
