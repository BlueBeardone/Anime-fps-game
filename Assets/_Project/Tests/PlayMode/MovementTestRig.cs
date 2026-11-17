using System.Collections;
using AnimeFpsGame.Movement;
using UnityEngine;

namespace AnimeFpsGame.Tests
{
    public class MovementTestRig : System.IDisposable
    {
        /// <summary>
        /// Far from the world origin so colliders left behind by other PlayMode fixtures (the range
        /// player, dummies, props) cannot block the test body. Test assertions are relative to it.
        /// </summary>
        public static readonly Vector3 ArenaOrigin = new Vector3(2000f, 0f, 2000f);

        public GameObject Floor;
        public GameObject Body;
        public Rigidbody BodyRigidbody;
        public CapsuleCollider Capsule;
        public CharacterMovement Movement;
        public PhysicsMaterial BodyMaterial;

        public Vector3 LocalPosition => Body.transform.position - ArenaOrigin;

        public static Vector3 At(float x, float y, float z)
        {
            return ArenaOrigin + new Vector3(x, y, z);
        }

        public static MovementTestRig Create(MovementProfile profile = null)
        {
            var rig = new MovementTestRig();
            rig.Floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rig.Floor.name = "TestFloor";
            rig.Floor.transform.position = At(0f, -0.5f, 0f);
            rig.Floor.transform.localScale = new Vector3(40f, 1f, 40f);

            rig.Body = new GameObject("TestBody");
            rig.Body.layer = 6;
            rig.Body.transform.position = At(0f, 0.02f, 0f);
            rig.Capsule = rig.Body.AddComponent<CapsuleCollider>();
            rig.Capsule.height = 1.8f;
            rig.Capsule.radius = 0.3f;
            rig.Capsule.center = new Vector3(0f, 0.9f, 0f);
            // The movement model applies Source friction itself; PhysX friction on the capsule would
            // cancel horizontal velocity through the ground-stick contact impulse.
            rig.BodyMaterial = new PhysicsMaterial("TestBodyMaterial")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            rig.Capsule.sharedMaterial = rig.BodyMaterial;
            rig.BodyRigidbody = rig.Body.AddComponent<Rigidbody>();
            rig.BodyRigidbody.mass = 80f;
            rig.BodyRigidbody.useGravity = false;
            rig.BodyRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rig.BodyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rig.Movement = rig.Body.AddComponent<CharacterMovement>();
            rig.Movement.Profile = profile ?? MovementProfile.CreateDefault();
            Physics.SyncTransforms();
            return rig;
        }

        public void SetIntent(Vector3 direction, bool sprint = false, bool crouch = false, bool jump = false)
        {
            Movement.SetIntent(new MovementIntent
            {
                WishDirection = direction, Sprint = sprint, Crouch = crouch, Jump = jump
            });
        }

        public IEnumerator WaitSteps(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        public void Dispose()
        {
            Object.Destroy(Body);
            Object.Destroy(Floor);
            Object.Destroy(BodyMaterial);
        }
    }
}
