using AnimeFpsGame.Player;
using NUnit.Framework;
using UnityEngine;

namespace AnimeFpsGame.Tests
{
    public class ViewmodelMuzzleTests
    {
        [Test]
        public void MuzzlePrefersNamedMarker()
        {
            var viewmodel = new GameObject("Viewmodel");
            var marker = new GameObject("Muzzle");
            marker.transform.SetParent(viewmodel.transform, false);
            marker.transform.localPosition = new Vector3(0.1f, 0.2f, 1.3f);

            Assert.AreEqual(new Vector3(0.1f, 0.2f, 1.3f),
                ViewmodelMuzzle.ComputeLocal(viewmodel));

            Object.DestroyImmediate(viewmodel);
        }

        [Test]
        public void MuzzleUsesTheFarthestRendererCorner()
        {
            var viewmodel = new GameObject("Viewmodel");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(viewmodel.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 2f);
            body.transform.localScale = Vector3.one * 0.1f;

            var local = ViewmodelMuzzle.ComputeLocal(viewmodel);

            Assert.AreEqual(2.05f, local.z, 0.001f,
                "The muzzle is the most forward point of the gun's bounds");

            Object.DestroyImmediate(viewmodel);
        }

        [Test]
        public void MuzzleFallbackDoesNotThrowWithoutRenderers()
        {
            var viewmodel = new GameObject("Viewmodel");

            Assert.AreEqual(new Vector3(0f, 0f, 0.5f),
                ViewmodelMuzzle.ComputeLocal(viewmodel));

            Object.DestroyImmediate(viewmodel);
        }
    }
}
