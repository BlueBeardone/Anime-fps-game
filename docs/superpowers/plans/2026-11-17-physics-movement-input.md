# Physics Movement and Rebindable Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `CharacterController` movement with a dynamic Rigidbody capsule that feels like CS2 plus sprint, share that movement with future AI through a plain intent contract, and add a persisted rebinding backend ready for a later Settings screen.

**Architecture:** `CharacterMovement` is a plain `MonoBehaviour` that consumes `MovementIntent`, runs Source-style friction/acceleration/jump/gravity in `FixedUpdate`, and owns the Rigidbody capsule. `PlayerMotor` is the only network/owner glue and `InputReader` is the only gameplay place that touches Input System. `PlayerInputBindings` owns overrides, conflict clearing and persistence.

**Tech Stack:** Unity 6000.6.9f1, URP, Input System 1.20.1, Netcode for GameObjects 2.14.0, NUnit via Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-11-17-physics-movement-input-design.md`

## Global Constraints

- Unity editor: `E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe`. Close the editor before batchmode commands.
- Project path: `D:\Making Games\Anime fps game`.
- `Assets/_Project/Editor/ProjectSetup.cs` is authoritative: `Anime FPS > Run All` rebuilds `Main.unity`, `Player.prefab` and `NetworkManager`. Any wiring not in `ProjectSetup` is lost.
- No `CharacterController` remains on the player prefab after Task 7.
- Fixed timestep is `0.02` s; movement runs in `FixedUpdate`.
- `Assets/_Project/Settings/Movement/DefaultMovementProfile.asset` must survive `Run All` if it already exists, like the weapon stats assets.
- Movement stays owner-authoritative on `NetworkTransform`; damage stays server-authoritative. No prediction/reconciliation.
- Namespaces: `AnimeFpsGame.Movement`, `AnimeFpsGame.Input`, `AnimeFpsGame.Player`; tests in `AnimeFpsGame.Tests`.
- Test command (change `-testFilter`, result and log names per task; omit the filter for the full suite):

```powershell
& 'E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe' -batchmode -projectPath 'D:\Making Games\Anime fps game' -runTests -testPlatform PlayMode -testFilter AnimeFpsGame.Tests.MovementTests -testResults 'D:\Making Games\Anime fps game\Temp\movement-tests.xml' -logFile 'D:\Making Games\Anime fps game\Temp\movement-tests.log'
```

Expected for a green run: exit code `0`, and the results XML reports `failed="0"`.

- Regenerate after prefab/generator changes:

```powershell
& 'E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\Making Games\Anime fps game' -executeMethod AnimeFpsGame.EditorTools.ProjectSetup.RunAll -logFile 'D:\Making Games\Anime fps game\Temp\project-setup.log'
```

Expected: exit code `0` and a log line containing `[ProjectSetup] RunAll complete`.

## Review Focus

- **Grounded explosion launch:** a grounded player given an upward impulse must leave the ground instead of being snapped back down. Pinned in Task 4 (`AddImpulseLaunchesGroundedBody`).
- **Rebinding onto a default key:** rebinding another action to `S` must unbound `Move/down`, whose `S` comes from a default, not silently restore the duplicate. Pinned in Task 5 (`ConflictUnbindsDefaultBindingAndPersists`).
- **Holding jump:** holding the jump button must produce exactly one jump, not auto-bhop. Pinned in Task 2 (`HoldingJumpJumpsOnce`).
- **Remote crouch hitbox:** the non-owner/server proxy capsule must shrink to crouch height so shots at low cover miss. Pinned in Task 3 (`CrouchShrinksCapsule`) and Task 7 (`ProxyCrouchResizesCapsule`), plus the manual two-client check in Task 8.
- **Dead player drift:** a dead owner must stop, and respawn must clear velocity and teleport the Rigidbody rather than leaving it moving. Pinned in Task 3 (`PauseStopsMovement`, `TeleportClearsVelocity`).

---

## Task 0: Baseline

**Files:**
- Commit: `docs/superpowers/specs/2026-11-17-physics-movement-input-design.md`, `docs/superpowers/plans/2026-11-17-physics-movement-input.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a green baseline and a clean commit to build on.

- [ ] **Step 1: Run the existing full PlayMode suite**

Run the test command above with `-testFilter` removed and result/log paths `Temp\baseline.xml` / `Temp\baseline.log`.
Expected: exit code `0`, `failed="0"`. If it fails, stop and report the failure before changing anything.

- [ ] **Step 2: Commit the approved spec and plan**

```powershell
git -C 'D:\Making Games\Anime fps game' add docs/superpowers/specs/2026-11-17-physics-movement-input-design.md docs/superpowers/plans/2026-11-17-physics-movement-input.md
git -C 'D:\Making Games\Anime fps game' commit -m "docs: approve physics movement and rebindable input design"
```

---

## Task 1: Movement Types, Profile and Grounded Run

**Files:**
- Create: `Assets/_Project/Scripts/Movement/MovementIntent.cs`
- Create: `Assets/_Project/Scripts/Movement/MovementState.cs`
- Create: `Assets/_Project/Scripts/Movement/MovementProfile.cs`
- Create: `Assets/_Project/Scripts/Movement/CharacterMovement.cs`
- Test: `Assets/_Project/Tests/PlayMode/MovementTestRig.cs`
- Test: `Assets/_Project/Tests/PlayMode/MovementTests.cs`

**Interfaces:**
- Consumes: `Rigidbody`, `CapsuleCollider`, `Physics`.
- Produces:
  - `[Flags] enum MovementState { None = 0, Grounded = 1, Sprinting = 2, Crouching = 4, Sliding = 8, Moving = 16 }`
  - `struct MovementIntent { public Vector3 WishDirection; public bool Sprint; public bool Crouch; public bool Jump; public static MovementIntent None => default; }`
  - `class MovementProfile : ScriptableObject` with public fields `runSpeed=6.35f`, `sprintSpeed=8f`, `crouchSpeed=2.15f`, `groundAcceleration=5.5f`, `airAcceleration=12f`, `airSpeedCap=0.76f`, `friction=5.2f`, `stopSpeed=2f`, `jumpHeight=1.45f`, `gravity=20.32f`, `terminalVelocity=50f`, `maxSlopeAngle=50f`, `slideAngle=51f`, `slideAcceleration=8f`, `stepHeight=0.4f`, `groundProbeDistance=0.15f`, `groundSnapDistance=0.3f`, `groundStickSpeed=2f`, `jumpBuffer=0.1f`, `coyoteTime=0.05f`, `autoBhop=false`, `capsuleHeight=1.8f`, `crouchHeight=1.2f`, `eyeHeight=1.65f`, `crouchEyeHeight=1.05f`, `crouchTransition=0.12f`, `sprintForwardDot=0.7f`, `playerMass=80f`; `static MovementProfile CreateDefault()`.
  - `class CharacterMovement : MonoBehaviour` with `MovementProfile Profile { get; set; }`, `MovementState State { get; }`, `bool IsGrounded { get; }`, `bool IsCrouching { get; }`, `bool IsSprinting { get; }`, `bool IsSliding { get; }`, `float HorizontalSpeed { get; }`, `Vector3 Velocity { get; }`, `float EyeHeight { get; }`, `void SetIntent(in MovementIntent intent)`, `void Teleport(Vector3 position, Quaternion rotation)`, `void AddImpulse(Vector3 deltaVelocity)`, `void SetPaused(bool paused)`, `void ApplyCrouchDimensions(bool crouched)`, events `Action Jumped`, `Action<float> Landed`, `Action<bool> CrouchChanged`.
  - Test helper `MovementTestRig` with `Floor`, `Body`, `BodyRigidbody`, `Capsule`, `Movement`, `static Create(MovementProfile profile = null)`, `SetIntent(Vector3 direction, bool sprint = false, bool crouch = false, bool jump = false)`, `IEnumerator WaitSteps(int steps)`, `Dispose()`.

- [ ] **Step 1: Write the failing tests**

Create `MovementTestRig.cs`:

```csharp
using System.Collections;
using AnimeFpsGame.Movement;
using UnityEngine;

namespace AnimeFpsGame.Tests
{
    public class MovementTestRig : System.IDisposable
    {
        public GameObject Floor;
        public GameObject Body;
        public Rigidbody BodyRigidbody;
        public CapsuleCollider Capsule;
        public CharacterMovement Movement;

        public static MovementTestRig Create(MovementProfile profile = null)
        {
            var rig = new MovementTestRig();
            rig.Floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rig.Floor.name = "TestFloor";
            rig.Floor.transform.position = new Vector3(0f, -0.5f, 0f);
            rig.Floor.transform.localScale = new Vector3(40f, 1f, 40f);

            rig.Body = new GameObject("TestBody");
            rig.Body.layer = 6;
            rig.Body.transform.position = new Vector3(0f, 0.02f, 0f);
            rig.Capsule = rig.Body.AddComponent<CapsuleCollider>();
            rig.Capsule.height = 1.8f;
            rig.Capsule.radius = 0.3f;
            rig.Capsule.center = new Vector3(0f, 0.9f, 0f);
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
        }
    }
}
```

Create `MovementTests.cs` with these three tests:

```csharp
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
                Assert.Greater(rig.Body.transform.position.z, 4f);
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the test command with `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: FAIL, compile errors for missing `MovementProfile`/`CharacterMovement`.

- [ ] **Step 3: Implement the movement types and grounded run**

- `MovementProfile`: all fields above as public serializable fields, `[CreateAssetMenu(menuName = "Anime FPS/Movement Profile")]`, and `CreateDefault()` returning `CreateInstance<MovementProfile>()`.
- `CharacterMovement.Awake`: cache `Rigidbody` and `CapsuleCollider`; `profile ??= MovementProfile.CreateDefault()`; force `rb.useGravity = false`; add `RigidbodyConstraints.FreezeRotation`; set `rb.mass = profile.playerMass`.
- Ground probe: sphere cast down with radius `capsule.radius * 0.95f`, origin `rb.position + Vector3.up * capsule.radius`, distance `capsule.radius + profile.groundProbeDistance`, mask `Physics.DefaultRaycastLayers & ~(1 << gameObject.layer)`, `QueryTriggerInteraction.Ignore`. Grounded when hit angle to `Vector3.up` is `<= profile.maxSlopeAngle`; keep a 0.1 s grace so stepping does not flicker.
- `FixedUpdate` when not paused: read `rb.linearVelocity` as the working velocity; if grounded apply friction then accelerate toward `runSpeed`; else no friction. Apply gravity (`vy -= gravity * dt`, clamp to `-terminalVelocity`); while grounded and not jumping set `vy = -groundStickSpeed`. Write the working velocity back with `rb.linearVelocity`.
- Friction and acceleration use the exact Source formulas from the spec (scale horizontal velocity by `(speed - max(speed, stopSpeed) * friction * dt) / speed`; add `min(accel * wishSpeed * dt, wishSpeed - dot(velocity, wishDir))` along `wishDir`).
- Set `State` flags: `Grounded`, `Moving` when horizontal speed > 0.1. `HorizontalSpeed` is the XZ magnitude. `Velocity` returns `rb.linearVelocity`.
- `Teleport`: set `rb.position`, `rb.rotation`, zero both velocities, `Physics.SyncTransforms()`.
- `SetPaused(true)`: clear intent, zero velocities. `SetPaused(false)`: resume.
- Leave `Sprint`, `Crouch`, `Jump`, `AddImpulse`, `ApplyCrouchDimensions`, `EyeHeight` as inert placeholders so the file compiles; later tasks fill them.

- [ ] **Step 4: Run tests to verify they pass**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: PASS, `failed="0"`.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Movement Assets/_Project/Tests/PlayMode/MovementTestRig.cs Assets/_Project/Tests/PlayMode/MovementTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: add physics movement core with Source friction and acceleration"
```

---

## Task 2: Jump, Air Control and Sprint

**Files:**
- Modify: `Assets/_Project/Scripts/Movement/CharacterMovement.cs`
- Modify: `Assets/_Project/Tests/PlayMode/MovementTests.cs`

**Interfaces:**
- Consumes: Task 1's `CharacterMovement`, `MovementProfile`, `MovementTestRig`.
- Produces: working `Jumped` event, ground/coyote buffering, air acceleration, sprint selection, `IsSprinting`.

- [ ] **Step 1: Write the failing tests**

Append to `MovementTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: FAIL on jump (no vertical impulse), air strafe and sprint assertions.

- [ ] **Step 3: Implement jump, air movement and sprint**

- `SetIntent` latches `Jump` only on a false→true edge into `jumpBufferExpiry = Time.time + profile.jumpBuffer` (when `autoBhop` is false). `Jump` is never re-triggered while held.
- Track `lastGroundedTime`; a buffered jump fires when grounded or within `profile.coyoteTime` of leaving ground. It sets vertical velocity to `Mathf.Sqrt(2f * profile.gravity * profile.jumpHeight)`, clears the buffer, resets the grounded timestamp and raises `Jumped`.
- While airborne, accelerate toward `min(wishSpeed, profile.airSpeedCap)` with `profile.airAcceleration`, never applying air friction.
- Sprint applies when `intent.Sprint`, grounded, not crouching, and `Vector3.Dot(transform.forward, wishDir) >= profile.sprintForwardDot`. `IsSprinting` is true only while that condition holds. Fall back to `runSpeed` otherwise.
- Do not apply ground stick on the tick a jump fires. Invoke `Landed(impactSpeed)` when transitioning from airborne to grounded.

- [ ] **Step 4: Run tests to verify they pass**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Movement/CharacterMovement.cs Assets/_Project/Tests/PlayMode/MovementTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: add CS2 jump, air control and sprint to movement"
```

---

## Task 3: Crouch, Pause and Teleport

**Files:**
- Modify: `Assets/_Project/Scripts/Movement/CharacterMovement.cs`
- Modify: `Assets/_Project/Tests/PlayMode/MovementTests.cs`

**Interfaces:**
- Consumes: Task 2 movement.
- Produces: `EyeHeight`, `IsCrouching`, `CrouchChanged`, `ApplyCrouchDimensions(bool)`, working `SetPaused`/`Teleport`, crouch speed and blocked-uncrouch handling.

- [ ] **Step 1: Write the failing tests**

Append to `MovementTests.cs`:

```csharp
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
        Assert.AreEqual(rig.Movement.Profile.crouchSpeed, rig.Movement.HorizontalSpeed, 0.25f);
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
        ceiling.transform.position = new Vector3(0f, 2.25f, 0f);
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
        rig.Movement.Teleport(new Vector3(5f, 0.02f, 5f), Quaternion.identity);
        yield return new WaitForFixedUpdate();
        Assert.Less(rig.Movement.Velocity.magnitude, 0.1f);
        Assert.AreEqual(5f, rig.BodyRigidbody.position.x, 0.05f);
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
```

Note: `ceiling.transform.position.y = 2.25` with height 1.5 puts its underside at 1.5 m, which blocks the 1.8 m standing capsule but clears the 1.2 m crouched one.

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: FAIL; crouch stays standing, pause/teleport behavior incomplete.

- [ ] **Step 3: Implement crouch, pause and teleport**

- Maintain `crouchT` (0 standing → 1 crouched). A crouch intent moves it toward 1 over `profile.crouchTransition`; clearing crouch only moves it toward 0 when a standing capsule overlap check is clear (mask and radius as the ground probe), otherwise it stays at 1. While transitioning, set capsule height `Lerp(crouchHeight, capsuleHeight, crouchT)` and center `(0, height / 2, 0)`. `EyeHeight` is `Lerp(crouchEyeHeight, eyeHeight, crouchT)`.
- `IsCrouching` is the target crouch state; raise `CrouchChanged` on changes. While crouched, wish speed is `crouchSpeed` and sprint is disabled.
- `ApplyCrouchDimensions(bool crouched)` resizes instantly, sets `crouchT` to 0 or 1, updates `IsCrouching`, and raises `CrouchChanged` only on change. This is the method the network proxy uses.
- `SetPaused(true)` sets a flag, clears intent, zeroes `linearVelocity`/`angularVelocity`; `FixedUpdate` returns immediately while paused. `Teleport` also clears the paused movement state? No: teleport only moves the body and clears velocity; pause remains whatever it was.
- Update `State` with `Crouching`.

- [ ] **Step 4: Run tests to verify they pass**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Movement/CharacterMovement.cs Assets/_Project/Tests/PlayMode/MovementTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: add crouch, pause and teleport to movement"
```

---

## Task 4: Step-Up, Slope Sliding and External Impulses

**Files:**
- Modify: `Assets/_Project/Scripts/Movement/CharacterMovement.cs`
- Modify: `Assets/_Project/Tests/PlayMode/MovementTests.cs`

**Interfaces:**
- Consumes: Task 3 movement.
- Produces: working `AddImpulse`, `IsSliding`, step climbing up to `profile.stepHeight`, downhill slope acceleration.

- [ ] **Step 1: Write the failing tests**

Append to `MovementTests.cs`:

```csharp
[UnityTest]
public IEnumerator StepUpClimbsSmallStep()
{
    var rig = MovementTestRig.Create();
    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try
    {
        step.transform.position = new Vector3(0f, 0.15f, 4f);
        step.transform.localScale = new Vector3(2f, 0.3f, 6f);
        Physics.SyncTransforms();
        rig.SetIntent(Vector3.forward);
        yield return rig.WaitSteps(40);
        Assert.Greater(rig.BodyRigidbody.position.y, 0.2f, "Must climb the 0.3 m step");
        Assert.Greater(rig.BodyRigidbody.position.z, 1.5f, "Must advance onto the step");
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
        ramp.transform.position = new Vector3(0f, 0f, 0f);
        ramp.transform.rotation = Quaternion.Euler(-60f, 0f, 0f);
        ramp.transform.localScale = new Vector3(2f, 1f, 2f);
        rig.Movement.Teleport(new Vector3(0f, 1f, 0f), Quaternion.identity);
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
        Assert.Greater(rig.BodyRigidbody.position.y, 0.1f);
        Assert.Greater(rig.Movement.Velocity.y, 1f);
    }
    finally
    {
        rig.Dispose();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: FAIL; the body is blocked by the step, does not slide, and impulses are overwritten.

- [ ] **Step 3: Implement step-up, slope sliding and impulses**

- Step-up, only while grounded with a non-zero wish direction:
  - Capsule-cast from `rb.position + up * 0.05f` to `rb.position + up * 0.2f` with radius `0.15f` forward by `radius + 0.15f`; return when nothing is hit.
  - Raycast down from `rb.position + up * stepHeight + wishDir * (radius + 0.2f)` for `stepHeight + 0.1f`; compute `rise = hit.y - rb.position.y`; return unless `0.02 < rise <= stepHeight`.
  - Check a capsule overlap at `rb.position + up * (rise + 0.02f)`; return if blocked.
  - Set `rb.position += up * (rise + 0.02f)` and `Physics.SyncTransforms()`. Do not change vertical velocity.
- Slope: if the ground normal angle exceeds `profile.slideAngle`, treat as not grounded, set `IsSliding`, skip friction and add `Vector3.ProjectOnPlane(Vector3.down, normal).normalized * profile.slideAcceleration * dt` to horizontal velocity.
- `AddImpulse(Vector3 deltaVelocity)`: add the delta to `rb.linearVelocity`; if `deltaVelocity.y > 0`, set `ignoreGroundStickUntil = Time.time + 0.15f`. Ground stick and ground snap are skipped while that timer is active. Never write a cached intent velocity back over PhysX's velocity.
- Ground snap: while grounded, not jumping and `verticalVelocity <= 0`, raycast down from the capsule bottom for `profile.groundSnapDistance`; if it hits, move the body's Rigidbody position down to the hit point (leaving a small skin) and sync transforms. This keeps the body attached when running down slopes and small steps.
- `Landed` fires once on the airborne→grounded transition with the downward speed at impact.

- [ ] **Step 4: Run tests to verify they pass**

Run `-testFilter AnimeFpsGame.Tests.MovementTests`.
Expected: PASS. If the slope test is unstable in batchmode, keep the `IsSliding` assertion and widen the displacement tolerance rather than deleting the test.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Movement/CharacterMovement.cs Assets/_Project/Tests/PlayMode/MovementTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: add step-up, slope sliding and external impulses"
```

---

## Task 5: Rebindable Input Facade

**Files:**
- Create: `Assets/_Project/Scripts/Input/PlayerInputBindings.cs`
- Test: `Assets/_Project/Tests/PlayMode/InputBindingTests.cs`

**Interfaces:**
- Consumes: `InputActionAsset`, `InputAction`, `InputSystem`.
- Produces:
  - `enum RebindOutcome { Success, ConflictResolved, Canceled, Failed }`
  - `struct RebindResult { public RebindOutcome Outcome; public string[] ClearedBindings; }`
  - `static class PlayerInputBindings` with `public const string PrefsKey = "animefps.input.bindings.v1"`, `void EnsureLoaded(InputActionAsset asset)`, `bool BeginRebind(InputActionAsset asset, string actionName, int bindingIndex, Action<RebindResult> completed)`, `void CancelRebind()`, `RebindResult ApplyBindingOverride(InputActionAsset asset, string actionName, int bindingIndex, string controlPath)`, `void ResetBinding(InputActionAsset asset, string actionName, int bindingIndex)`, `void ResetAllBindings(InputActionAsset asset)`, `string ExportOverrides(InputActionAsset asset)`, `void ImportOverrides(InputActionAsset asset, string json)`, `void Save(InputActionAsset asset)`.

- [ ] **Step 1: Write the failing tests**

Create `InputBindingTests.cs`:

```csharp
using AnimeFpsGame.Input;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class InputBindingTests
    {
        InputActionAsset asset;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PlayerInputBindings.PrefsKey);
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            map.AddAction("Move", InputActionType.Value, "<Keyboard>/w");
            map.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInputBindings.CancelRebind();
            PlayerPrefs.DeleteKey(PlayerInputBindings.PrefsKey);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void RebindingMovesTheActionAndExportsTheOverride()
        {
            var result = PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            Assert.AreEqual(RebindOutcome.Success, result.Outcome);
            Assert.AreEqual("<Keyboard>/t", asset.FindAction("Reload").bindings[0].effectivePath);
            StringAssert.Contains("<Keyboard>/t", PlayerInputBindings.ExportOverrides(asset));
        }

        [Test]
        public void ConflictUnbindsDefaultBindingAndPersists()
        {
            var result = PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/w");
            Assert.AreEqual(RebindOutcome.ConflictResolved, result.Outcome);
            Assert.Contains("Move#0", result.ClearedBindings);
            Assert.AreEqual(string.Empty, asset.FindAction("Move").bindings[0].effectivePath);

            var fresh = ScriptableObject.CreateInstance<InputActionAsset>();
            fresh.LoadBindingOverridesFromJson(PlayerInputBindings.ExportOverrides(asset));
            Assert.AreEqual(string.Empty, fresh.FindAction("Move").bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/w", fresh.FindAction("Reload").bindings[0].effectivePath);
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void ResetBindingRestoresDefault()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/w");
            PlayerInputBindings.ResetBinding(asset, "Move", 0);
            Assert.AreEqual("<Keyboard>/w", asset.FindAction("Move").bindings[0].effectivePath);
        }

        [Test]
        public void ResetAllRemovesEveryOverride()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            PlayerInputBindings.ResetAllBindings(asset);
            Assert.AreEqual("<Keyboard>/r", asset.FindAction("Reload").bindings[0].effectivePath);
            Assert.IsFalse(PlayerPrefs.HasKey(PlayerInputBindings.PrefsKey));
        }

        [Test]
        public void EscapeAndUnknownActionsAreRejected()
        {
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/escape").Outcome);
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Nope", 0, "<Keyboard>/t").Outcome);
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 9, "<Keyboard>/t").Outcome);
        }

        [Test]
        public void EnsureLoadedAppliesSavedOverrides()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            PlayerInputBindings.Save(asset);

            var fresh = ScriptableObject.CreateInstance<InputActionAsset>();
            var freshMap = fresh.AddActionMap("Player");
            freshMap.AddAction("Move", InputActionType.Value, "<Keyboard>/w");
            freshMap.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
            PlayerInputBindings.EnsureLoaded(fresh);
            Assert.AreEqual("<Keyboard>/t", fresh.FindAction("Reload").bindings[0].effectivePath);
            Object.DestroyImmediate(fresh);
        }

        [UnityTest]
        public IEnumerator BeginRebindCapturesKeyboardKey()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("RebindTestKeyboard");
            try
            {
                RebindResult? result = null;
                Assert.IsTrue(PlayerInputBindings.BeginRebind(asset, "Reload", 0, r => result = r));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                var deadline = Time.realtimeSinceStartup + 2f;
                while (!result.HasValue && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.IsTrue(result.HasValue, "Rebind must complete");
                Assert.AreEqual(RebindOutcome.Success, result.Value.Outcome);
                Assert.AreEqual("<Keyboard>/t", asset.FindAction("Reload").bindings[0].effectivePath);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.InputBindingTests`.
Expected: FAIL, `PlayerInputBindings` missing.

- [ ] **Step 3: Implement the rebinding facade and persistence**

- `ApplyBindingOverride`: reject unknown action, out-of-range binding, empty path, or `<Keyboard>/escape` with `Failed`. Apply with `action.ApplyBindingOverride(index, controlPath)`. Then scan every binding of every action in the `Player` map by `effectivePath`; skip empty paths and the new binding. For every other match, call `otherAction.ApplyBindingOverride(otherIndex, string.Empty)` and record `"ActionName#otherIndex"`. Return `ConflictResolved` when anything was cleared, else `Success`.
- `Save`: `PlayerPrefs.SetString(PrefsKey, ExportOverrides(asset)); PlayerPrefs.Save();`.
- `EnsureLoaded`: if the key exists, disable the asset if enabled, call `LoadBindingOverridesFromJson`, re-enable, and leave the maps in their previous enabled state.
- `ResetBinding`: `FindAction(...).RemoveBindingOverride(bindingIndex)`, then `Save`. `ResetAllBindings`: `RemoveAllBindingOverrides`, delete the key, save PlayerPrefs.
- `BeginRebind`: return false when an operation is already running or the action/index is invalid. Disable the `Player` map, start `action.PerformInteractiveRebinding(bindingIndex)` with `WithCancelingThrough("<Keyboard>/escape")`, exclude `<Mouse>/position` and `<Mouse>/delta`, `OnMatchWaitForAnother(0.1f)`. On complete, use `operation.selectedControl.path` with `ApplyBindingOverride`, save, dispose, re-enable the map and invoke the callback. On cancel, restore the map and return `Canceled`.
- `CancelRebind` calls `Cancel()` on the active operation.
- `ExportOverrides` returns `asset.SaveBindingOverridesAsJson()`; `ImportOverrides` calls `LoadBindingOverridesFromJson`.

- [ ] **Step 4: Run tests to verify they pass**

Run `-testFilter AnimeFpsGame.Tests.InputBindingTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Input/PlayerInputBindings.cs Assets/_Project/Tests/PlayMode/InputBindingTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: add persisted input rebinding with conflict clearing"
```

---

## Task 6: InputReader and Centralized Gameplay Input

**Files:**
- Create: `Assets/_Project/Scripts/Input/InputReader.cs`
- Modify: `Assets/_Project/Scripts/Player/PlayerLook.cs`
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs` (add `InputReader` in `BuildPlayerPrefab`)
- Test: `Assets/_Project/Tests/PlayMode/InputReaderTests.cs`
- Modify: `Assets/_Project/Tests/PlayMode/PlayerSmokeTests.cs` (assert `InputReader` on the spawned player)

**Interfaces:**
- Consumes: `PlayerInput`, `PlayerInputBindings`, existing `PlayerLook`/`PlayerWeapons`.
- Produces:
  - `class InputReader : MonoBehaviour` with `Vector2 Move`, `Vector2 Look`, `bool SprintHeld`, `bool CrouchHeld`, `bool JumpPressed`, `bool AttackPressed`, `bool AttackHeld`, `bool ReloadPressed`, `bool CycleNextPressed`, `bool CyclePreviousPressed`, `float ScrollY`.
  - Prefab gains an `InputReader`; `PlayerLook` and `PlayerWeapons` no longer read `PlayerInput` or `Mouse.current` directly.

- [ ] **Step 1: Write the failing tests**

Create `InputReaderTests.cs`:

```csharp
using System.Collections;
using AnimeFpsGame.Input;
using AnimeFpsGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class InputReaderTests
    {
        [UnityTearDown]
        public IEnumerator TearDownNetwork()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                manager.Shutdown();
            }
            yield return null;
            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InputReaderReadsMoveFromKeyboard()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("MovementTestKeyboard");
            try
            {
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;

                var weapons = default(PlayerWeapons);
                var deadline = Time.realtimeSinceStartup + 10f;
                while (weapons == null && Time.realtimeSinceStartup < deadline)
                {
                    weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                    yield return null;
                }

                var reader = weapons.GetComponent<InputReader>();
                Assert.IsNotNull(reader, "Spawned player has InputReader");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                yield return null;
                Assert.Greater(reader.Move.y, 0.5f);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
```

In `PlayerSmokeTests.RangeSpawnsAPlayerAndSwapsToThePistol`, add after the `PlayerInput` assertion:

```csharp
Assert.IsNotNull(weapons.GetComponent<AnimeFpsGame.Input.InputReader>(), "Spawned player has InputReader");
```

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.InputReaderTests`.
Expected: FAIL, `InputReader` missing.

- [ ] **Step 3: Implement InputReader and refactor input consumers**

- `InputReader` requires `PlayerInput`. Cache actions once in `Awake` or `OnEnable` with `FindAction("Move")` etc.; call `PlayerInputBindings.EnsureLoaded(playerInput.actions)` before caching. Return safe defaults when an action is missing.
- `AttackPressed`/`JumpPressed`/`ReloadPressed`/`CycleNextPressed`/`CyclePreviousPressed` use `WasPressedThisFrame`; `AttackHeld`/`SprintHeld`/`CrouchHeld` use `IsPressed`; `ScrollY` reads `Mouse.current?.scroll.ReadValue().y ?? 0f`.
- In `PlayerWeapons`: replace the four `InputAction` fields, the `OnNetworkSpawn` action lookup and the direct `Mouse.current` read with `InputReader` properties. Keep the existing 0.2 s scroll debounce and all firing/reload/slot behavior.
- In `PlayerLook`: replace its action lookup and read with `InputReader.Look`.
- In `ProjectSetup.BuildPlayerPrefab`, add `var inputReader = root.AddComponent<AnimeFpsGame.Input.InputReader>();` after `PlayerInput`, and `SetReference` it into `PlayerLook`/`PlayerWeapons` using field name `input`.
- Give `PlayerLook` and `PlayerWeapons` a serialized `InputReader input` field with a `GetComponent` fallback in `Awake`.

- [ ] **Step 4: Regenerate the prefab and run tests**

Run the regeneration command, then the full suite.
Expected: `RunAll complete`, exit code `0`, `failed="0"`.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Input/InputReader.cs Assets/_Project/Scripts/Player/PlayerLook.cs Assets/_Project/Scripts/Player/PlayerWeapons.cs Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Prefabs/Player/Player.prefab Assets/_Project/Tests/PlayMode/InputReaderTests.cs Assets/_Project/Tests/PlayMode/PlayerSmokeTests.cs
git -C 'D:\Making Games\Anime fps game' commit -m "feat: centralize gameplay input in InputReader"
```

---

## Task 7: Physics Player Body, Motor, Crouch Replication and Respawn

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerMotor.cs`
- Modify: `Assets/_Project/Scripts/Player/PlayerLook.cs`
- Modify: `Assets/_Project/Scripts/Combat/PlayerRespawn.cs`
- Modify: `Assets/_Project/Scripts/Combat/Health.cs` (remove the now-dead `CharacterController` branch)
- Modify: `Assets/_Project/Editor/ProjectSetup.cs` (physics body, layer, profile, material, wiring)
- Create: `Assets/_Project/Art/Materials/PlayerPhysicsMaterial.physicsMaterial` (via generator)
- Create: `Assets/_Project/Settings/Movement/DefaultMovementProfile.asset` (via generator)
- Modify: `Assets/_Project/Tests/PlayMode/MovementTests.cs` (proxy crouch test)
- Modify: `Assets/_Project/Tests/PlayMode/PlayerSmokeTests.cs` (body assertions)

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: player prefab with `Rigidbody` + `CapsuleCollider` + `CharacterMovement` + `InputReader`; `PlayerMotor` owns movement intent and `NetworkVariable<bool> Crouched`; `PlayerLook` yaw through the Rigidbody and eye height from movement; respawn teleports through `CharacterMovement`.

- [ ] **Step 1: Write the failing tests**

Append to `MovementTests.cs`:

```csharp
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
```

In `PlayerSmokeTests.RangeSpawnsAPlayerAndSwapsToThePistol`, replace the `motor` assertion block with:

```csharp
var motor = weapons.GetComponent<Player.PlayerMotor>();
Assert.IsNotNull(motor, "Spawned player has PlayerMotor");
Assert.IsNotNull(weapons.GetComponent<Player.PlayerLook>(), "Spawned player has PlayerLook");
Assert.IsNotNull(weapons.GetComponent<UnityEngine.InputSystem.PlayerInput>(), "Spawned player has PlayerInput");
Assert.IsNotNull(weapons.GetComponent<AnimeFpsGame.Input.InputReader>(), "Spawned player has InputReader");
var body = weapons.GetComponent<Rigidbody>();
Assert.IsNotNull(body, "Player body is a Rigidbody");
Assert.IsNotNull(weapons.GetComponent<CapsuleCollider>(), "Player body has a capsule");
Assert.IsNotNull(weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>(), "Player uses CharacterMovement");
Assert.IsNull(weapons.GetComponent<CharacterController>(), "CharacterController must be gone");
Assert.IsFalse(body.isKinematic, "The owner simulates the body");
```

- [ ] **Step 2: Run tests to verify they fail**

Run `-testFilter AnimeFpsGame.Tests.MovementTests` (the proxy crouch test should already pass from Task 3) and the PlayerSmokeTests filter.
Expected: PlayerSmokeTests FAIL on the missing Rigidbody/capsule/CharacterMovement components.

- [ ] **Step 3: Implement the player body and glue**

- In `ProjectSetup.BuildPlayerPrefab`:
  - Ensure a `Player` layer: scan `TagManager.asset` layers 6–31 for the name; if absent, take the first empty slot and write `Player`. Assign that layer recursively to the player root and all children.
  - Create `EnsurePlayerPhysicsMaterial()` at `Assets/_Project/Art/Materials/PlayerPhysicsMaterial.physicsMaterial`: `dynamicFriction = 0`, `staticFriction = 0`, `bounciness = 0`, both combines `Minimum`.
  - Create `EnsureMovementProfile()` at `Assets/_Project/Settings/Movement/DefaultMovementProfile.asset` only when missing; use `MovementProfile.CreateDefault()`.
  - Replace `CharacterController` with `Rigidbody` (mass 80, `useGravity = false`, `constraints = FreezeRotation`, `interpolation = Interpolate`, `collisionDetectionMode = ContinuousSpeculative`) and `CapsuleCollider` (height 1.8, radius 0.3, center `(0, 0.9, 0)`, shared material from above).
  - Add `CharacterMovement`, set its `profile` reference. Add `InputReader`. Wire `PlayerMotor` (`movement`, `input`, `playerLook`, `health`, `animator`), `PlayerLook` (`input`, `movement`, `body`, `cameraPivot`), `PlayerWeapons` (`input`), and `PlayerRespawn` (`movement`, `playerLook`).
- `PlayerMotor`:
  - Keep `NetworkBehaviour`; cache `CharacterMovement`, `InputReader`, `PlayerLook`, `Health`, `Animator`.
  - `readonly NetworkVariable<bool> crouched = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);`
  - On spawn: non-owner applies `movement.ApplyCrouchDimensions(crouched.Value)` and subscribes to `crouched.OnValueChanged`; owner subscribes to `movement.CrouchChanged` and writes the NetworkVariable. Keep the component enabled on non-owners for this; guard all input/animator work with `IsOwner`.
  - Owner `Update`: when dead, `movement.SetIntent(MovementIntent.None)` and `movement.SetPaused(true)`; otherwise `SetPaused(false)`, build a camera-relative direction from `input.Move` and `playerLook.Yaw` (`Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y)` clamped), and `movement.SetIntent` with sprint/crouch held and `Jump = input.JumpPressed`. Set animator `Status_walk` to 1 when `movement.HorizontalSpeed > 0.1f`, else 0.
  - Unsubscribe from both events in `OnNetworkDespawn`.
  - `PlayerLook`:
  - Use `InputReader.Look`; keep sensitivity, recoil and cursor/camera handling.
  - Expose `public float Yaw => yaw;` and `public void SetYaw(float degrees)` (sets `yaw` and the body rotation immediately).
  - Apply yaw on the body with `body.MoveRotation(Quaternion.Euler(0, yaw + recoilOffset.x, 0))` in `FixedUpdate`; keep pitch on `cameraPivot` in `Update`.
  - Set `cameraPivot.localPosition = new Vector3(0f, movement.EyeHeight, 0f)` each `Update`.
  - Initialize `yaw` from `body.rotation.eulerAngles.y` on spawn.
- `PlayerRespawn`: replace `CharacterController controller` with `CharacterMovement movement` and add a serialized `PlayerLook playerLook`; on respawn call `movement.Teleport(spawnPosition, spawnRotation)` and `playerLook.SetYaw(spawnRotation.eulerAngles.y)`. Do not toggle a controller.
- `Health`: delete the `controller` field, `controllerEnabledAtSpawn`, and the controller branch in `ApplyDeathState`; collider caching stays. Update the class comment.

- [ ] **Step 4: Regenerate, then run the full suite**

Run the regeneration command, then the full suite.
Expected: `RunAll complete`, exit code `0`, `failed="0"`.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add Assets/_Project/Scripts/Player Assets/_Project/Scripts/Combat/PlayerRespawn.cs Assets/_Project/Scripts/Combat/Health.cs Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Prefabs/Player/Player.prefab Assets/_Project/Scenes/Main.unity Assets/_Project/Settings/Movement Assets/_Project/Art/Materials Assets/_Project/Tests/PlayMode/MovementTests.cs Assets/_Project/Tests/PlayMode/PlayerSmokeTests.cs ProjectSettings/TagManager.asset
git -C 'D:\Making Games\Anime fps game' commit -m "feat: migrate player to a physics body with shared movement"
```

---

## Task 8: Documentation and Final Verification

**Files:**
- Modify: `docs/superpowers/notes/anime-fps-starter.md`
- Modify: `docs/superpowers/notes/2026-11-16-main-menu-ledger.md` only if the menu behavior changed (it should not)

**Interfaces:**
- Consumes: everything above.
- Produces: accurate run/controls/tuning/gap docs and a verified final state.

- [ ] **Step 1: Update the starter notes**

- Controls table: sprint is Shift, jump Space, crouch Ctrl (verify the binding in `InputSystem_Actions.inputactions` and document the actual key).
- Add a "Movement" section: `Scripts/Movement/CharacterMovement.cs` (no Input System/network), intent contract for AI, `MovementProfile` at `Settings/Movement`, `Run All` never overwrites an existing profile.
- Add an "Input" section: `InputReader`, `PlayerInputBindings`, bindings saved under `PlayerPrefs` key `animefps.input.bindings.v1`, no Settings UI yet.
- Known gaps: no prediction/reconciliation, crouch replication is not lag compensated, no slide/mantle, no settings UI, sensitivity/invert still hardcoded, ragdolls not implemented yet.

- [ ] **Step 2: Run full verification**

Run the regeneration command and the full PlayMode suite.
Expected: `RunAll complete`, exit code `0`, `failed="0"`.

- [ ] **Step 3: Manual two-client check**

Build the Windows player, run `"Builds\Windows\Anime fps game.exe" -host` and `"Builds\Windows\Anime fps game.exe" -join 127.0.0.1`, then check:

- Movement and camera feel correct on the client; no jitter or rubber-banding.
- Crouching behind a low obstacle makes the client harder to hit on the host (server proxy capsule shrinks).
- Sprinting, jumping, crouch-jumping and step-up work.
- No `CharacterController` warnings and no missing `InputReader`/profile references in the console.

Record the result in the commit message or the notes.

- [ ] **Step 4: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add docs/superpowers/notes/anime-fps-starter.md
git -C 'D:\Making Games\Anime fps game' commit -m "docs: document physics movement, controls and input backend"
```
