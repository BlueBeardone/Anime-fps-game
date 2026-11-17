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

        [UnityTest]
        public IEnumerator JumpApexMatchesProfile()
        {
            var rig = MovementTestRig.Create();
            try
            {
                yield return rig.WaitSteps(10);
                var startY = rig.BodyRigidbody.position.y;
                rig.SetIntent(Vector3.zero, jump: true);
                yield return null;
                rig.SetIntent(Vector3.zero);
                var apex = startY;
                for (var i = 0; i < 90; i++)
                {
                    yield return new WaitForFixedUpdate();
                    apex = Mathf.Max(apex, rig.BodyRigidbody.position.y);
                }
                Assert.AreEqual(rig.Movement.Profile.jumpHeight, apex - startY, 0.2f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator HoldingJumpJumpsOnce()
        {
            var rig = MovementTestRig.Create();
            try
            {
                var jumps = 0;
                rig.Movement.Jumped += () => jumps++;
                rig.SetIntent(Vector3.zero, jump: true);
                yield return rig.WaitSteps(120);
                Assert.AreEqual(1, jumps, "Holding jump must not auto-bhop");
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AirStrafeAddsLateralVelocity()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(60);
                rig.SetIntent(Vector3.zero, jump: true);
                yield return null;
                rig.SetIntent(Vector3.right);
                yield return rig.WaitSteps(20);
                Assert.IsFalse(rig.Movement.IsGrounded);
                Assert.Greater(rig.Movement.Velocity.x, 0.3f, "Air acceleration must add lateral speed");
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SprintIsForwardOnlyAndReachesSprintSpeed()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward, sprint: true);
                yield return rig.WaitSteps(120);
                Assert.AreEqual(rig.Movement.Profile.sprintSpeed, rig.Movement.HorizontalSpeed, 0.25f);
                Assert.IsTrue(rig.Movement.IsSprinting);
            }
            finally
            {
                rig.Dispose();
            }

            var backward = MovementTestRig.Create();
            try
            {
                backward.SetIntent(Vector3.back, sprint: true);
                yield return backward.WaitSteps(120);
                Assert.AreEqual(backward.Movement.Profile.runSpeed, backward.Movement.HorizontalSpeed, 0.25f);
                Assert.IsFalse(backward.Movement.IsSprinting);
            }
            finally
            {
                backward.Dispose();
            }
        }
    }
}
