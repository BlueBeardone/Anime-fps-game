# Muzzle, Physical Drops and Hover Pickups — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put tracers on the true bore, make dropped guns physical thrown objects, and make picked-up
guns travel into the player's hands.

**Architecture:** A per-slot muzzle position on `WeaponDefinition` drives effects. Runtime drops use
a server-side Rigidbody and a generated NetworkTransform. The pickup hover is an owner-side viewmodel
ease that preserves the existing instant, server-authoritative swap.

**Tech Stack:** Unity 6000.6.9f1, URP, Netcode for GameObjects 2.14.0, NUnit PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-20-weapon-muzzle-drop-pickup-design.md`

## Global Constraints

- `Assets/_Project/Editor/ProjectSetup.cs` is the only authoritative generator; all prefab/scene
  wiring must be idempotent and survive `Anime FPS > Run All`.
- The shot ray must keep originating at the camera; only effects move to the muzzle.
- Pickup swaps and drops remain server-authoritative.
- Existing PlayMode tests must stay green.

## Review Focus

- A drop thrown into a wall or over an edge: expected to collide or fall, never sink below ground.
- A drop that settles at a player's feet: expected to be visible and pickable, not a movement blocker.
- Switching slots while a pickup hover is in progress: expected to snap cleanly to the new gun with
  no lingering hover state.
- A viewmodel with no `Muzzle` child and an off-centre mesh: expected tracer at the barrel centreline.
- A pickup requested twice in the same window: expected one winner, no duplicate swap.

---

### Task 1: Muzzle reference and centred fallback

**Files:**
- Modify: `Assets/_Project/Scripts/Player/WeaponDefinition.cs`
- Modify: `Assets/_Project/Scripts/Player/ViewmodelMuzzle.cs`
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs`
- Test: `Assets/_Project/Tests/PlayMode/ViewmodelMuzzleTests.cs`

**Interfaces:**
- Produces: `WeaponDefinition.muzzleLocalPosition : Vector3`
- Produces: `ViewmodelMuzzle.ComputeLocal(GameObject viewmodel) : Vector3` (same signature, centred)
- Produces: `ViewmodelMuzzle.ComputeLocalBoundsCentre(GameObject viewmodel) : Vector3`
- Produces: `ProjectSetup.EnsureViewmodelMuzzle(GameObject viewmodel) : Vector3`

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run the test and verify it fails**

Run the focused PlayMode filter `AnimeFpsGame.Tests.ViewmodelMuzzleTests`. Expected: the new test
fails because the old code returns the corner `(0.5, 0.4, 2.1)`.

- [ ] **Step 3: Implement the centred fallback and definition field**

`ViewmodelMuzzle.ComputeLocal` keeps the named-marker branch, then returns the combined renderer
bounds centre `x`/`y` with `max.z`. Add `muzzleLocalPosition` to `WeaponDefinition`.
`PlayerWeapons.ApplySlot` uses `definition.muzzleLocalPosition` when its `sqrMagnitude` is above a
small epsilon, otherwise the helper.

`ProjectSetup.EnsureViewmodelMuzzle` loads the prefab, computes the same point, adds or moves a direct
child named `Muzzle`, saves the prefab, and returns the local position. `ConfigureSlot` writes it into
the slot.

- [ ] **Step 4: Run the focused test and verify it passes**

Expected: 4/4 `ViewmodelMuzzleTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Player/WeaponDefinition.cs \
        Assets/_Project/Scripts/Player/ViewmodelMuzzle.cs \
        Assets/_Project/Scripts/Player/PlayerWeapons.cs \
        Assets/_Project/Editor/ProjectSetup.cs \
        Assets/_Project/Tests/PlayMode/ViewmodelMuzzleTests.cs
git commit -m "fix: start weapon effects at the real muzzle"
```

---

### Task 2: Physical thrown drops

**Files:**
- Modify: `Assets/_Project/Scripts/Weapons/WeaponPickup.cs`
- Modify: `Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs`
- Test: `Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs`

**Interfaces:**
- Produces: `WeaponPickup.Launch(Vector3 velocity)` (server only)
- Produces: `WeaponPickup.ThrowVelocity(Vector3 flatForward) : Vector3` (static, unit-tested)
- Produces: `PlayerPickupInteractor.SpawnDrop` uses `WeaponPickup.ThrowVelocity`
- Produces: `EnsurePickupPrefab` adds `NetworkTransform` with server authority and interpolation

- [ ] **Step 1: Write the failing pure test**

```csharp
[Test]
public void ThrowVelocityGoesForwardAndUp()
{
    var velocity = WeaponPickup.ThrowVelocity(Vector3.forward);
    Assert.Greater(velocity.z, 3f);
    Assert.Greater(velocity.y, 1f);
    Assert.AreEqual(0f, velocity.x, 0.001f);
}
```

- [ ] **Step 2: Run it and verify it fails to compile**

Expected: `WeaponPickup.ThrowVelocity` does not exist.

- [ ] **Step 3: Implement server physics**

`EnsurePickupPrefab` adds `<NetworkTransform>`; `Launch` adds a server-only `Rigidbody` and a
`BoxCollider` sized from the visible renderer bounds, applies the velocity and spin, uses gravity and
continuous collision, and settles to kinematic with the collider disabled after a short stillness
window. `SpawnDrop` spawns at chest height and calls `Launch(ThrowVelocity(flatForward))`.

- [ ] **Step 4: Add the PlayMode assertions**

In `PickupFlowCarriesAmmoBothWays`, after the drop is found, assert a `Rigidbody` exists and that
after one second the drop is still above ground (`transform.position.y > 0f`). Also assert a scene
pickup does not have a Rigidbody.

- [ ] **Step 5: Run the focused pickup tests and verify they pass**

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Weapons/WeaponPickup.cs \
        Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs \
        Assets/_Project/Editor/ProjectSetup.cs \
        Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs
git commit -m "feat: throw dropped guns with physics"
```

---

### Task 3: Hover pickup presentation

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Modify: `Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs`

**Interfaces:**
- Consumes: `PlayerWeapons.ApplySlot`, `currentViewmodel`, `viewmodelBasePosition`
- Produces: `PlayerWeapons.BeginPickupHover(Vector3 worldPosition)`
- Produces: `PlayerWeapons.PickupHoverActive : bool`
- Produces: `PlayerPickupInteractor` caches `pendingHoverWorldPosition`

- [ ] **Step 1: Write the failing hover test**

Add a PlayMode test that picks up a scene gun on the host, then asserts
`weapons.PickupHoverActive` is true immediately and false after 0.6 s, and that the viewmodel returns
to `viewmodelBasePosition` within a small tolerance.

- [ ] **Step 2: Run it and verify it fails**

Expected: `PickupHoverActive` does not exist.

- [ ] **Step 3: Implement the hover state**

`BeginPickupHover` stores the target world position, converts it into camera-local space and sets a
0.3 s timer. `UpdateViewmodelFeel` lerps from that starting local position to the base position with
an ease-out while the timer runs, then resumes normal sway/kick. Switching slots clears the hover.
`PlayerPickupInteractor` calls `BeginPickupHover(target.transform.position)` when its swap lands.

- [ ] **Step 4: Run the hover test and verify it passes**

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Player/PlayerWeapons.cs \
        Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs \
        Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs
git commit -m "feat: hover picked-up guns into the hands"
```

---

### Task 4: Regenerate and verify

**Files:**
- Modify: generated `Assets/_Project/Prefabs/Weapons/WeaponPickup.prefab`,
  `Assets/_Project/Prefabs/Player/Player.prefab`, viewmodel prefabs, `Assets/_Project/Scenes/Main.unity`

- [ ] **Step 1: Run the generator**

Run `E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe -batchmode -quit -projectPath "D:\Making Games\Anime fps game" -executeMethod AnimeFpsGame.EditorTools.ProjectSetup.RunAll -logFile <temp>`

- [ ] **Step 2: Run the full PlayMode suite**

Run the same editor with `-runTests -testPlatform PlayMode -testResults <temp.xml> -logFile <temp>`.
Expected: all tests pass.

- [ ] **Step 3: Inspect the generated prefabs**

Confirm the pickup prefab has `NetworkTransform`, each viewmodel has a `Muzzle` child, and the player
prefab's four slots have non-zero muzzle positions.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Prefabs Assets/_Project/Scenes/Main.unity
git commit -m "chore: regenerate weapon prefabs and scene"
```

---

## Execution record

Executed inline on branch `fix/weapon-muzzle-physics-pickup`.

- `62748bc` fix: start weapon effects at the real muzzle
- `0ef113a` feat: throw dropped guns with physics
- `786bd46` feat: hover picked-up guns into the hands
- `af1075d` fix: place the weapon muzzle on the bore centreline
- `d9fccbc` chore: regenerate weapon prefabs with muzzle and physics wiring

Verification:

- `ViewmodelMuzzleTests`: 5/5 pass.
- `WeaponPickupTests`: 7/7 pass.
- Full PlayMode suite: 90/90 pass.
- `RunAll` completed with `-quit` in batchmode; pickup prefab has `NetworkTransform` (server, interpolated), each viewmodel has a `Muzzle` child, and all four player slots have non-zero muzzle positions.
