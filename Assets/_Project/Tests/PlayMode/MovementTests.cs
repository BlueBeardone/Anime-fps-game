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

        [UnityTest]
        public IEnumerator CrouchShrinksCapsuleAndSpeed()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward, crouch: true);
                yield return rig.WaitSteps(40);
                Assert.IsTrue(rig.Movement.IsCrouching);
                Assert.AreEqual(rig.Movement.Profile.crouchHeight, rig.Capsule.height, 0.05f);
                Assert.AreEqual(rig.Movement.Profile.crouchEyeHeight, rig.Movement.EyeHeight, 0.05f);
                // Source stop-speed friction means the achieved speed settles a little below the
                // crouch wish speed (~1.95 m/s); the point is that crouching is clearly slower.
                Assert.Less(rig.Movement.HorizontalSpeed, rig.Movement.Profile.runSpeed - 1f);
                Assert.Greater(rig.Movement.HorizontalSpeed, 1.2f);
                rig.SetIntent(Vector3.zero);
                yield return rig.WaitSteps(20);
                Assert.AreEqual(rig.Movement.Profile.capsuleHeight, rig.Capsule.height, 0.05f);
                Assert.IsFalse(rig.Movement.IsCrouching);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator CrouchBlockedUnderLowCeiling()
        {
            var rig = MovementTestRig.Create();
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                rig.Movement.ApplyCrouchDimensions(true);
                ceiling.transform.position = MovementTestRig.At(0f, 2.25f, 0f);
                ceiling.transform.localScale = new Vector3(4f, 1.5f, 4f);
                Physics.SyncTransforms();
                yield return rig.WaitSteps(5);
                rig.SetIntent(Vector3.zero, crouch: false);
                yield return rig.WaitSteps(20);
                Assert.IsTrue(rig.Movement.IsCrouching, "Must stay crouched while the ceiling blocks standing");
                Assert.AreEqual(rig.Movement.Profile.crouchHeight, rig.Capsule.height, 0.05f);
            }
            finally
            {
                Object.Destroy(ceiling);
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator TeleportClearsVelocity()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(40);
                Assert.Greater(rig.Movement.HorizontalSpeed, 3f);
                rig.Movement.Teleport(MovementTestRig.At(5f, 0.02f, 5f), Quaternion.identity);
                yield return new WaitForFixedUpdate();
                Assert.Less(rig.Movement.Velocity.magnitude, 0.1f);
                Assert.AreEqual(5f, rig.LocalPosition.x, 0.05f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PauseStopsMovement()
        {
            var rig = MovementTestRig.Create();
            try
            {
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(30);
                rig.Movement.SetPaused(true);
                var pausedPosition = rig.BodyRigidbody.position;
                yield return rig.WaitSteps(30);
                Assert.Less(rig.Movement.Velocity.magnitude, 0.1f);
                Assert.AreEqual(pausedPosition.z, rig.BodyRigidbody.position.z, 0.05f);
                rig.Movement.SetPaused(false);
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(20);
                Assert.Greater(rig.Movement.HorizontalSpeed, 0.5f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StepUpClimbsSmallStep()
        {
            var rig = MovementTestRig.Create();
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                step.transform.position = MovementTestRig.At(0f, 0.15f, 4f);
                step.transform.localScale = new Vector3(2f, 0.3f, 6f);
                Physics.SyncTransforms();
                rig.SetIntent(Vector3.forward);
                yield return rig.WaitSteps(40);
                Assert.Greater(rig.LocalPosition.y, 0.2f, "Must climb the 0.3 m step");
                Assert.Greater(rig.LocalPosition.z, 1.5f, "Must advance onto the step");
            }
            finally
            {
                Object.Destroy(step);
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SteepSlopeSlides()
        {
            var rig = MovementTestRig.Create();
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Plane);
            try
            {
                // High above the test floor: the slide must stay on the ramp, not drop onto the floor.
                ramp.transform.position = MovementTestRig.At(0f, 8f, 0f);
                ramp.transform.rotation = Quaternion.Euler(-60f, 0f, 0f);
                ramp.transform.localScale = new Vector3(2f, 1f, 2f);
                rig.Movement.Teleport(MovementTestRig.At(0f, 8.4f, 0f), Quaternion.identity);
                Physics.SyncTransforms();
                yield return rig.WaitSteps(25);
                Assert.IsFalse(rig.Movement.IsGrounded, "A 60 degree surface is not standable");
                Assert.IsTrue(rig.Movement.IsSliding);
                var startedAt = rig.BodyRigidbody.position;
                yield return rig.WaitSteps(50);
                Assert.Greater(Vector3.Distance(startedAt, rig.BodyRigidbody.position), 0.3f);
            }
            finally
            {
                Object.Destroy(ramp);
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AddImpulseMovesGroundedBodyHorizontally()
        {
            var rig = MovementTestRig.Create();
            try
            {
                yield return rig.WaitSteps(10);
                rig.Movement.AddImpulse(Vector3.forward * 5f);
                yield return new WaitForFixedUpdate();
                Assert.Greater(rig.Movement.HorizontalSpeed, 3f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AddImpulseLaunchesGroundedBody()
        {
            var rig = MovementTestRig.Create();
            try
            {
                yield return rig.WaitSteps(10);
                rig.Movement.AddImpulse(Vector3.up * 4f);
                yield return rig.WaitSteps(2);
                Assert.Greater(rig.LocalPosition.y, 0.1f);
                Assert.Greater(rig.Movement.Velocity.y, 1f);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ProxyCrouchResizesCapsule()
        {
            var rig = MovementTestRig.Create();
            try
            {
                yield return rig.WaitSteps(5);
                rig.Movement.ApplyCrouchDimensions(true);
                Assert.IsTrue(rig.Movement.IsCrouching);
                Assert.AreEqual(rig.Movement.Profile.crouchHeight, rig.Capsule.height, 0.05f);
                rig.Movement.ApplyCrouchDimensions(false);
                Assert.IsFalse(rig.Movement.IsCrouching);
                Assert.AreEqual(rig.Movement.Profile.capsuleHeight, rig.Capsule.height, 0.05f);
            }
            finally
            {
                rig.Dispose();
            }
        }
    }
}
