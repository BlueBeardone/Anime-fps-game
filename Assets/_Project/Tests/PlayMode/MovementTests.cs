using System.Collections;
using AnimeFpsGame.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class MovementTests
    {
        [Test]
        public void ProfileDefaultsMatchDesign()
        {
            var profile = MovementProfile.CreateDefault();
            Assert.AreEqual(6.35f, profile.runSpeed, 0.001f);
            Assert.AreEqual(8f, profile.sprintSpeed, 0.001f);
            Assert.AreEqual(2.15f, profile.crouchSpeed, 0.001f);
            Assert.AreEqual(20.32f, profile.gravity, 0.001f);
            Assert.AreEqual(1.45f, profile.jumpHeight, 0.001f);
            Assert.AreEqual(5.2f, profile.friction, 0.001f);
            Assert.AreEqual(0.4f, profile.stepHeight, 0.001f);
            Object.DestroyImmediate(profile);
        }

        [UnityTest]
        public IEnumerator RunSpeedReachesProfileOnFlatGround()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(120);
                Assert.IsTrue(rig.Movement.IsGrounded, "Body must be grounded");
                Assert.AreEqual(rig.Movement.Profile.runSpeed, rig.Movement.HorizontalSpeed, 0.25f);
                Assert.Greater(rig.LocalPosition.z, 4f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FrictionStopsBodyAfterIntentCleared()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(60);
                Assert.Greater(rig.Movement.HorizontalSpeed, 5f);
                rig.SetIntent(Vector3.zero);
                yield return rig.WaitSteps(60);
                Assert.Less(rig.Movement.HorizontalSpeed, 0.3f);
            }
            finally
            {
                rig.Dispose();
            }
        }
    }
}
