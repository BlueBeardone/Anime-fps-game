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

            Assert.AreEqual(0f, local.x, 0.001f, "The bore is centred, not a corner");
            Assert.AreEqual(0f, local.y, 0.001f, "The bore is centred, not a corner");
            Assert.AreEqual(2.05f, local.z, 0.001f,
                "The muzzle is the most forward point of the gun's bounds");

            Object.DestroyImmediate(viewmodel);
        }

        [Test]
        public void LocalBoundsAreExpressedInViewmodelSpace()
        {
            var viewmodel = new GameObject("Viewmodel");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(viewmodel.transform, false);
            body.transform.localPosition = new Vector3(0.4f, 0.3f, 2f);
            body.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            Assert.IsTrue(ViewmodelMuzzle.TryComputeLocalBounds(viewmodel, out var min, out var max));

            Assert.AreEqual(0.3f, min.x, 0.001f);
            Assert.AreEqual(0.2f, min.y, 0.001f);
            Assert.AreEqual(1.9f, min.z, 0.001f);
            Assert.AreEqual(0.5f, max.x, 0.001f);
            Assert.AreEqual(0.4f, max.y, 0.001f);
            Assert.AreEqual(2.1f, max.z, 0.001f);

            Object.DestroyImmediate(viewmodel);
        }

        [Test]
        public void MuzzleFallbackUsesTheForwardCentreNotACorner()
        {
            var viewmodel = new GameObject("Viewmodel");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(viewmodel.transform, false);
            body.transform.localPosition = new Vector3(0.4f, 0.3f, 2f);
            body.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            var local = ViewmodelMuzzle.ComputeLocal(viewmodel);

            Assert.AreEqual(0.4f, local.x, 0.001f);
            Assert.AreEqual(0.3f, local.y, 0.001f);
            Assert.AreEqual(2.1f, local.z, 0.001f);

            Object.DestroyImmediate(viewmodel);
        }

        [Test]
        public void MuzzleFallbackUsesTheBoreNotTheGrip()
        {
            var viewmodel = new GameObject("Viewmodel");
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(viewmodel.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, 2f);
            barrel.transform.localScale = Vector3.one * 0.1f;

            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(viewmodel.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            grip.transform.localScale = Vector3.one * 0.2f;

            var local = ViewmodelMuzzle.ComputeLocal(viewmodel);

            Assert.AreEqual(0f, local.x, 0.001f);
            Assert.AreEqual(0f, local.y, 0.001f,
                "The muzzle must sit on the barrel, not the grip-heavy bounds centre");
            Assert.AreEqual(2.05f, local.z, 0.001f);

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
