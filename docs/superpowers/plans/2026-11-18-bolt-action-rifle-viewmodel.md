# Bolt-Action Rifle, Held Viewmodel and Muzzle Tracers — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the K98 held in first person, turn the Rifle slot into a 5-round bolt action with a 1 s cycle and normal reload, and start tracers/muzzle flash at the gun's muzzle for everyone.

**Architecture:** Keep the existing persistent `IGun`/`Gun` logic and `PlayerWeapons` runtime. Add a `BoltAction` fire mode with per-shot cadence and fixed recoil, fix the Rifle slot's viewmodel rotation, derive a muzzle point from the viewmodel (a `Muzzle` child overrides it), and thread the muzzle through local effects and the fire RPC for remote effects. Camera raycasts stay the source of truth for aim and damage.

**Tech Stack:** Unity 6000.6.9f1, URP, Netcode for GameObjects 2.14.0, NUnit PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-18-bolt-action-rifle-viewmodel-design.md`

## Global Constraints

- Unity editor: `E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe`; project: `D:\Making Games\Anime fps game`.
- **Close the Unity Editor before any batchmode command and keep it closed until Task 5 is committed.** Ask the user to close it before Task 1's first test run and to reopen it after Task 5.
- Test command (replace the filter per task; omit `-testFilter` for the full suite):

```powershell
& 'E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe' -batchmode -projectPath 'D:\Making Games\Anime fps game' -runTests -testPlatform PlayMode -testFilter AnimeFpsGame.Tests.GunTests -testResults 'D:\Making Games\Anime fps game\Temp\gun-tests.xml' -logFile 'D:\Making Games\Anime fps game\Temp\gun-tests.log'
```

A green run means exit code `0` and `failed="0"` in the results XML. Unity sometimes writes results to `%LocalAppDataLow%\AnimeFps\Anime fps game\TestResults.xml` instead of `-testResults`; check both.

- Only the Rifle slot changes. Handgun, SMG and Shotgun behaviour and viewmodels are untouched.
- `GunArchetype.Rifle` stays the slot-0 archetype; the bolt action is data (`FireMode`), not a new class.
- Raycasts, damage and server validation keep using the **camera origin**. The muzzle is visual only.
- Do not touch `Assets/_Project/Scenes/Main.unity`; it has an unrelated uncommitted change.
- Never run `Anime FPS > Run All` as part of this work; edit the generated prefab and `ProjectSetup` so both stay correct.

## Review Focus

- Holding the fire button across the 1 s cycle: the Rifle must fire once per press, not auto-fire. Pinned by `BoltActionFiresOnlyOnPress` (Task 1).
- A sideways or off-frustum viewmodel: the held gun must point where the camera looks and be inside the camera frustum. Pinned by `RifleViewmodelIsVisibleInFrontOfCamera` (Task 3).
- A muzzle behind/inside the camera or missing on a bare prefab: must fall back to a point in front of the camera without throwing. Pinned by `MuzzleFallbackDoesNotThrowWithoutRenderers` and `MuzzlePrefersNamedMarker` (Task 2), and `MuzzleIsInFrontOfTheCamera` (Task 3).
- A zero or negative `boltCycleSeconds` in a tuned asset: must fall back to the RPM cadence, never lock the rifle forever. Pinned by `BoltActionWithoutBoltCycleFallsBackToRpm` (Task 1).
- The fifth round: must start the normal reload immediately. Pinned by `BoltActionReloadsAfterFiveRounds` (Task 1).

---

### Task 1: Bolt-Action Fire Mode

**Files:**
- Modify: `Assets/_Project/Scripts/Weapons/FireMode.cs`
- Modify: `Assets/_Project/Scripts/Weapons/GunStats.cs`
- Modify: `Assets/_Project/Scripts/Weapons/IGun.cs`
- Modify: `Assets/_Project/Scripts/Weapons/Gun.cs`
- Modify: `Assets/_Project/Scripts/Weapons/Rifle.cs`
- Modify: `Assets/_Project/Settings/Weapons/Rifle.asset`
- Test: `Assets/_Project/Tests/PlayMode/GunTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `FireMode.BoltAction`
  - `GunStats.boltCycleSeconds` (`float`), `GunStats.boltRecoilDegrees` (`Vector2`)
  - `IGun.BoltCycleProgress01` (`float`, 0 when not cycling, 0..1 during the cycle)
  - `Rifle.CreateDefaultStats()` now returns the bolt-action tuning below

- [ ] **Step 1: Write the failing tests**

In `GunTests.cs`, update `TestStats()` so the existing cadence/bloom/spray tests keep testing the generic loop:

```csharp
static GunStats TestStats()
{
    var stats = Rifle.CreateDefaultStats();
    stats.fireMode = FireMode.FullAuto;
    stats.boltCycleSeconds = 0f;
    stats.boltRecoilDegrees = Vector2.zero;
    stats.baseSpreadDegrees = 0f;
    stats.bloomPerShotDegrees = 0f;
    stats.magazineSize = 3;
    stats.reserveAmmo = 6;
    stats.reloadSeconds = 1f;
    stats.roundsPerMinute = 600f;
    return stats;
}
```

Append these tests to `GunTests`:

```csharp
static GunStats BoltStats()
{
    var stats = TestStats();
    stats.fireMode = FireMode.BoltAction;
    stats.boltCycleSeconds = 1f;
    return stats;
}

[Test]
public void BoltActionFiresOnlyOnPress()
{
    var gun = new Rifle(BoltStats());

    Assert.IsFalse(Fire(gun, 0f, pressed: false, held: true),
        "Holding the trigger must not fire a bolt action");
    Assert.IsTrue(Fire(gun, 0f));
    Assert.IsFalse(Fire(gun, 1.01f, pressed: false, held: true),
        "Holding after the cycle must not auto-fire");
    Assert.IsTrue(Fire(gun, 1.01f));
}

[Test]
public void BoltActionLocksFiringForTheBoltCycle()
{
    var gun = new Rifle(BoltStats());

    Assert.IsTrue(Fire(gun, 0f));
    Assert.IsFalse(Fire(gun, 0.5f), "The bolt cycle blocks the next shot");
    Assert.IsTrue(Fire(gun, 1.01f), "The next shot works after one second");
}

[Test]
public void BoltActionWithoutBoltCycleFallsBackToRpm()
{
    var stats = BoltStats();
    stats.boltCycleSeconds = 0f;
    stats.roundsPerMinute = 600f;
    var gun = new Rifle(stats);

    Assert.IsTrue(Fire(gun, 0f));
    Assert.IsFalse(Fire(gun, 0.05f));
    Assert.IsTrue(Fire(gun, 0.11f));
}

[Test]
public void BoltActionReloadsAfterFiveRounds()
{
    var stats = BoltStats();
    stats.magazineSize = 5;
    stats.reserveAmmo = 10;
    var gun = new Rifle(stats);

    for (var i = 0; i < 5; i++)
    {
        Assert.IsTrue(Fire(gun, i * 1.01f), $"Shot {i + 1} must fire");
    }

    Assert.AreEqual(0, gun.AmmoInMagazine);
    Assert.IsTrue(gun.IsReloading, "The fifth shot starts the normal reload");
}

[Test]
public void BoltActionKicksTheSameEveryShot()
{
    var stats = BoltStats();
    stats.boltRecoilDegrees = new Vector2(0.5f, 2f);
    var gun = new Rifle(stats);
    var kicks = new System.Collections.Generic.List<Vector2>();
    gun.RecoilKicked += kicks.Add;

    Fire(gun, 0f);
    Fire(gun, 1.01f);

    Assert.AreEqual(2, kicks.Count);
    Assert.AreEqual(new Vector2(0.5f, 2f), kicks[0], "First shot kicks");
    Assert.AreEqual(new Vector2(0.5f, 2f), kicks[1], "Every bolt-action shot kicks the same");
}

[Test]
public void BoltCycleProgressRunsThenClears()
{
    var gun = new Rifle(BoltStats());

    Fire(gun, 0f);
    Assert.AreEqual(0f, gun.BoltCycleProgress01, 0.01f);
    gun.Tick(0.5f);
    Assert.AreEqual(0.5f, gun.BoltCycleProgress01, 0.01f);
    gun.Tick(0.6f);
    Assert.AreEqual(0f, gun.BoltCycleProgress01, 0.01f);
}

[Test]
public void BoltCycleAdvancesWhileStowed()
{
    var gun = new Rifle(BoltStats());

    Assert.IsTrue(Fire(gun, 0f));
    gun.CancelReload();
    gun.Tick(1.01f);

    Assert.IsTrue(Fire(gun, 1.01f), "Stowing must not leave the bolt locked");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the test command with `-testFilter AnimeFpsGame.Tests.GunTests`.

Expected: compile failure because `FireMode.BoltAction`, `BoltCycleProgress01`, `boltCycleSeconds` and `boltRecoilDegrees` do not exist.

- [ ] **Step 3: Implement the mode, stats and bolt logic**

`FireMode.cs` — add `BoltAction` **last** so serialized values 0/1 keep their meaning:

```csharp
public enum FireMode
{
    SemiAuto,
    FullAuto,
    BoltAction
}
```

`GunStats.cs` — add `using UnityEngine;` and these fields after `pelletSpreadDegrees`:

```csharp
public float boltCycleSeconds;
public Vector2 boltRecoilDegrees;
```

`IGun.cs` — add after `ReloadProgress01`:

```csharp
float BoltCycleProgress01 { get; }
```

`Gun.cs`:
- Add `float boltRemaining;` next to `reloadRemaining`.
- Add the property:

```csharp
public float BoltCycleProgress01 =>
    boltRemaining > 0f && stats.boltCycleSeconds > 0f
        ? Mathf.Clamp01(1f - boltRemaining / stats.boltCycleSeconds)
        : 0f;
```

- In `Tick`, after `timeSinceLastShot += deltaTime;`, advance the bolt even while reloading or stowed:

```csharp
if (boltRemaining > 0f)
{
    boltRemaining = Mathf.Max(0f, boltRemaining - deltaTime);
}
```

- In `ResetAmmo`, add `boltRemaining = 0f;`.
- Replace the body of `TryFire` with this ordering (the trigger gate, cadence and recoil branch are the decisions the tests pin):

```csharp
public bool TryFire(Vector3 origin, Vector3 aimForward, float now, bool triggerPressed,
    bool triggerHeld, out ShotSpec shot)
{
    shot = default;

    if (!CanFire(now))
    {
        return false;
    }

    var boltAction = stats.fireMode == FireMode.BoltAction;

    if ((stats.fireMode == FireMode.SemiAuto || boltAction) && !triggerPressed)
    {
        return false;
    }

    if (stats.fireMode == FireMode.FullAuto && !triggerHeld)
    {
        return false;
    }

    if (timeSinceLastShot > stats.sprayResetSeconds)
    {
        sprayIndex = 0;
    }

    var kick = Vector2.zero;
    if (!boltAction)
    {
        var pattern = SprayPattern;
        var index = Mathf.Clamp(sprayIndex, 0, pattern.Length - 1);
        var previous = index > 0 ? pattern[index - 1] : Vector2.zero;
        kick = pattern[index] - previous;
        sprayIndex = Mathf.Min(sprayIndex + 1, pattern.Length - 1);
    }

    ammoInMagazine--;
    nextFireTime = now + (boltAction && stats.boltCycleSeconds > 0f
        ? stats.boltCycleSeconds
        : 60f / Mathf.Max(1f, stats.roundsPerMinute));
    timeSinceLastShot = 0f;
    bloomDegrees = Mathf.Min(stats.maxBloomDegrees, bloomDegrees + stats.bloomPerShotDegrees);

    if (boltAction)
    {
        boltRemaining = stats.boltCycleSeconds;
        sprayIndex = 0;
        kick = stats.boltRecoilDegrees;
    }

    shot = new ShotSpec
    {
        Origin = origin,
        Directions = BuildDirections(aimForward),
        Damage = stats.damage,
        Range = stats.range
    };

    RaiseRecoil(kick);
    AmmoChanged?.Invoke();

    if (ammoInMagazine <= 0 && reserveAmmo > 0)
    {
        TryStartReload();
    }

    return true;
}
```

`Rifle.CreateDefaultStats()` — set the bolt-action tuning (keep `range`, spread, bloom and `sprayResetSeconds` as they are):

```csharp
displayName = "Rifle",
archetype = GunArchetype.Rifle,
fireMode = FireMode.BoltAction,
roundsPerMinute = 600f,
damage = 55f,
pellets = 1,
magazineSize = 5,
reserveAmmo = 30,
reloadSeconds = 2.2f,
range = 200f,
baseSpreadDegrees = 0.25f,
bloomPerShotDegrees = 0.35f,
maxBloomDegrees = 3.5f,
bloomRecoveryDegreesPerSecond = 8f,
sprayResetSeconds = 0.4f,
boltCycleSeconds = 1f,
boltRecoilDegrees = new Vector2(0.4f, 2.2f)
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the `GunTests` filter again.

Expected: `failed="0"`, all old tests plus the seven new bolt-action tests pass.

- [ ] **Step 5: Update the serialized Rifle asset**

`Run All` never overwrites existing assets, so `Rifle.asset` must carry the same values. In `Assets/_Project/Settings/Weapons/Rifle.asset`, change:

```yaml
    fireMode: 2
    damage: 55
    magazineSize: 5
    reserveAmmo: 30
    boltCycleSeconds: 1
    boltRecoilDegrees: {x: 0.4, y: 2.2}
```

Keep `reloadSeconds: 2.2`, `roundsPerMinute: 600` and every other field unchanged.

- [ ] **Step 6: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add 'Assets/_Project/Scripts/Weapons' 'Assets/_Project/Settings/Weapons/Rifle.asset' 'Assets/_Project/Tests/PlayMode/GunTests.cs'
git -C 'D:\Making Games\Anime fps game' commit -m "feat: turn the rifle slot into a bolt action"
```

---

### Task 2: Muzzle Point Helper

**Files:**
- Create: `Assets/_Project/Scripts/Player/ViewmodelMuzzle.cs`
- Test: `Assets/_Project/Tests/PlayMode/ViewmodelMuzzleTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public static Vector3 ViewmodelMuzzle.ComputeLocal(GameObject viewmodel)` — a muzzle offset in the viewmodel root's local space.

- [ ] **Step 1: Write the failing tests**

Create `ViewmodelMuzzleTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the test command with `-testFilter AnimeFpsGame.Tests.ViewmodelMuzzleTests`.

Expected: compile failure because `ViewmodelMuzzle` does not exist.

- [ ] **Step 3: Implement `ViewmodelMuzzle`**

Create `Scripts/Player/ViewmodelMuzzle.cs`:

```csharp
using UnityEngine;

namespace AnimeFpsGame.Player
{
    /// <summary>Finds the muzzle in a viewmodel's local space: a child named Muzzle wins, otherwise the
    /// most forward corner of the combined renderer bounds. The fallback keeps effect spawning safe for
    /// art without a marker.</summary>
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

            var best = Fallback;
            var bestZ = float.NegativeInfinity;
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
                    if (local.z > bestZ)
                    {
                        bestZ = local.z;
                        best = local;
                    }
                }

                hasBounds = true;
            }

            return hasBounds ? best : Fallback;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the `ViewmodelMuzzleTests` filter again.

Expected: `failed="0"`, 3 tests passed.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add 'Assets/_Project/Scripts/Player/ViewmodelMuzzle.cs' 'Assets/_Project/Tests/PlayMode/ViewmodelMuzzleTests.cs'
git -C 'D:\Making Games\Anime fps game' commit -m "feat: derive a muzzle point from the viewmodel"
```

---

### Task 3: Held Viewmodel and Local Muzzle Effects

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Modify: `Assets/_Project/Scripts/Effects/ShotEffects.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs:382-383`
- Modify: `Assets/_Project/Prefabs/Player/Player.prefab:437`
- Test: `Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs`

**Interfaces:**
- Consumes: `ViewmodelMuzzle.ComputeLocal(GameObject)` from Task 2; `IGun.BoltCycleProgress01` from Task 1.
- Produces:
  - `PlayerWeapons.MuzzleWorldPosition` (`Vector3`)
  - `ShotEffects.PlayLocal(Vector3 muzzleOrigin, Vector3 rayOrigin, Vector3[] directions, RaycastHit[] hits)`

- [ ] **Step 1: Write the failing scene tests**

Append these tests to `PlayerWeaponTests` (they load `Main` and spawn the host player, like the existing tests):

```csharp
[UnityTest]
public IEnumerator RifleViewmodelIsVisibleInFrontOfCamera()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while (weapons == null && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    Assert.IsNotNull(weapons, "Player must spawn");
    Assert.IsNotNull(weapons.CurrentViewmodel, "The owner must hold a viewmodel");
    Assert.IsTrue(weapons.CurrentViewmodel.activeInHierarchy);

    var renderers = weapons.CurrentViewmodel.GetComponentsInChildren<Renderer>();
    Assert.IsNotEmpty(renderers, "The viewmodel must have visible geometry");

    var bounds = renderers[0].bounds;
    for (var i = 1; i < renderers.Length; i++)
    {
        bounds.Encapsulate(renderers[i].bounds);
    }

    var planes = GeometryUtility.CalculateFrustumPlanes(weapons.ViewCamera);
    Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, bounds),
        "The rifle must be inside the camera frustum");

    var angle = Vector3.Angle(
        weapons.CurrentViewmodel.transform.forward,
        weapons.ViewCamera.transform.forward);
    Assert.Less(angle, 15f, "The barrel must point where the camera looks");
}

[UnityTest]
public IEnumerator MuzzleIsInFrontOfTheCamera()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while (weapons == null && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    Assert.IsNotNull(weapons);

    var camera = weapons.ViewCamera.transform;
    var toMuzzle = weapons.MuzzleWorldPosition - camera.position;

    Assert.Greater(Vector3.Dot(toMuzzle, camera.forward), 0f,
        "The muzzle must be in front of the camera");
    Assert.Less(toMuzzle.magnitude, 3f,
        "The muzzle must be on the held gun, not across the map");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the test command with `-testFilter AnimeFpsGame.Tests.PlayerWeaponTests`.

Expected: compile failure because `MuzzleWorldPosition` does not exist, and the visibility test would fail on the current sideways rotation.

- [ ] **Step 3: Fix the Rifle slot rotation**

In `ProjectSetup.cs`, change only the Rifle `ConfigureSlot` call:

```csharp
ConfigureSlot(slots.GetArrayElementAtIndex(0), "Rifle", "status_k98", "K98", k98Viewmodel,
    new Vector3(0.24f, -0.22f, 0.48f), new Vector3(0f, 0f, 0f), 0.08f, rifleStats);
```

In `Player.prefab`, change only the Rifle block (the `displayName: Rifle` one around line 432):

```yaml
    viewmodelLocalEuler: {x: 0, y: 0, z: 0}
```

Leave the Handgun, SMG and Shotgun Euler values at `(0, 90, 0)`.

- [ ] **Step 4: Implement the muzzle and bolt dip in `PlayerWeapons`**

- Add a field beside `viewmodelBaseEuler`: `Vector3 muzzleLocal;`
- Add the public property:

```csharp
public Vector3 MuzzleWorldPosition => currentViewmodel != null
    ? currentViewmodel.transform.TransformPoint(muzzleLocal)
    : viewCamera != null
        ? viewCamera.transform.position + viewCamera.transform.forward * 0.5f
        : transform.position + transform.forward * 0.5f;
```

- In `ApplySlot`, after `currentViewmodel.transform.localScale = ...`, add:

```csharp
muzzleLocal = ViewmodelMuzzle.ComputeLocal(currentViewmodel);
```

- In `TryFireActive`, pass the muzzle to local effects:

```csharp
if (effects != null)
{
    effects.PlayLocal(MuzzleWorldPosition, shot.Origin, shot.Directions, hits);
}
```

- In `UpdateViewmodelFeel`, add a bolt dip beside the reload dip:

```csharp
var bolt01 = gun != null && gun.BoltCycleProgress01 > 0f
    ? Mathf.Sin(gun.BoltCycleProgress01 * Mathf.PI)
    : 0f;

currentViewmodel.transform.localPosition = viewmodelBasePosition + new Vector3(
    0f,
    -0.06f * reload01 - 0.02f * bolt01,
    -viewmodelKickBack - 0.03f * reload01);

currentViewmodel.transform.localRotation = Quaternion.Euler(
    viewmodelBaseEuler + new Vector3(-viewmodelKickUp + 25f * reload01 - 8f * bolt01, 0f, 0f));
```

- [ ] **Step 5: Start local tracers and the flash at the muzzle**

In `ShotEffects.cs`, change `PlayLocal` to:

```csharp
public void PlayLocal(Vector3 muzzleOrigin, Vector3 rayOrigin, Vector3[] directions,
    RaycastHit[] hits)
{
    if (directions == null)
    {
        return;
    }

    PlayFlash(muzzleOrigin);

    for (var i = 0; i < directions.Length; i++)
    {
        var end = rayOrigin + directions[i] * tracerMissDistance;
        var hasHit = hits != null && i < hits.Length && hits[i].collider != null;
        if (hasHit)
        {
            end = hits[i].point;
        }

        PlayTracer(muzzleOrigin, end);

        if (hasHit)
        {
            PlayPuff(end, hits[i].normal);
        }
    }
}
```

In `PlayFlash`, place the light exactly at the muzzle:

```csharp
muzzleLight.transform.position = origin;
```

- [ ] **Step 6: Run the tests to verify they pass**

Run the `PlayerWeaponTests` filter.

Expected: `failed="0"`, 4 tests passed (2 existing + 2 new).

If the visibility test fails by a few degrees, adjust only the Rifle slot `viewmodelLocalEuler` values (start with `(0, 0, 0)` and correct yaw/pitch) and rerun. Do not change the other slots.

- [ ] **Step 7: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add 'Assets/_Project/Scripts/Player/PlayerWeapons.cs' 'Assets/_Project/Scripts/Effects/ShotEffects.cs' 'Assets/_Project/Editor/ProjectSetup.cs' 'Assets/_Project/Prefabs/Player/Player.prefab' 'Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs'
git -C 'D:\Making Games\Anime fps game' commit -m "feat: hold the rifle upright and fire tracers from its muzzle"
```

---

### Task 4: Remote Muzzle Effects

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Modify: `Assets/_Project/Scripts/Effects/ShotEffects.cs`

**Interfaces:**
- Consumes: `PlayerWeapons.MuzzleWorldPosition` and `ShotEffects.PlayLocal` from Task 3.
- Produces:
  - `PlayerWeapons.FireServerRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions, ServerRpcParams)`
  - `PlayerWeapons.FireEffectClientRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions, Vector3[] points, Vector3[] normals, ClientRpcParams)`
  - `ShotEffects.PlayRemote(Vector3 muzzleOrigin, Vector3 rayOrigin, Vector3[] directions, Vector3[] points)`

- [ ] **Step 1: Send the shooter's muzzle with the fire request**

In `TryFireActive`, capture the muzzle once and pass it to the server call:

```csharp
var muzzle = MuzzleWorldPosition;

if (IsSpawned)
{
    FireServerRpc(shot.Slot, shot.Origin, muzzle, shot.Directions);
}
```

Change `FireServerRpc` to:

```csharp
[ServerRpc]
void FireServerRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions,
    ServerRpcParams rpcParams = default)
```

After the existing origin distance check, add the same sanity rule for the cosmetic muzzle:

```csharp
if (Vector3.Distance(muzzleOrigin, transform.position) > 3f)
{
    return;
}
```

Pass the muzzle on to the broadcast:

```csharp
FireEffectClientRpc(slot, origin, muzzleOrigin, validated.ToArray(), points.ToArray(),
    normals.ToArray(), targetsParams);
```

Change `FireEffectClientRpc` to:

```csharp
[ClientRpc]
void FireEffectClientRpc(byte slot, Vector3 origin, Vector3 muzzleOrigin, Vector3[] directions,
    Vector3[] points, Vector3[] normals, ClientRpcParams rpcParams = default)
```

and call:

```csharp
effects.PlayRemote(muzzleOrigin, origin, directions, points);
```

- [ ] **Step 2: Draw remote tracers from that muzzle**

In `ShotEffects.cs`, change `PlayRemote` to:

```csharp
public void PlayRemote(Vector3 muzzleOrigin, Vector3 rayOrigin, Vector3[] directions,
    Vector3[] points)
{
    if (directions == null)
    {
        return;
    }

    PlayFlash(muzzleOrigin);

    for (var i = 0; i < directions.Length; i++)
    {
        var hasPoint = points != null && i < points.Length;
        var end = hasPoint ? points[i] : rayOrigin + directions[i] * tracerMissDistance;
        PlayTracer(muzzleOrigin, end);

        if (hasPoint)
        {
            PlayPuff(end, -directions[i]);
        }
    }
}
```

- [ ] **Step 3: Compile and run the fast suite**

Run the test command with `-testFilter AnimeFpsGame.Tests.GunTests` (it forces a full compile of the runtime and test assemblies).

Expected: `failed="0"`.

- [ ] **Step 4: Note the manual check**

Remote tracers cannot be observed in batchmode. Record in the commit body that a host + client session is still needed to see another player's tracer leave their gun; the mechanics (server RPC, muzzle validation, remote playback) are compile-checked only.

- [ ] **Step 5: Commit**

```powershell
git -C 'D:\Making Games\Anime fps game' add 'Assets/_Project/Scripts/Player/PlayerWeapons.cs' 'Assets/_Project/Scripts/Effects/ShotEffects.cs'
git -C 'D:\Making Games\Anime fps game' commit -m "feat: send the shooter muzzle with the fire RPC" -m "Remote tracers are compile-checked only; a two-client session should confirm another player's tracer leaves their gun."
```

---

### Task 5: Full Suite, Notes and Reopen the Editor

**Files:**
- Modify: `docs/superpowers/specs/2026-11-18-bolt-action-rifle-viewmodel-design.md`
- Modify: `docs/superpowers/notes/anime-fps-starter.md`

**Interfaces:**
- Consumes: everything above.
- Produces: no code.

- [ ] **Step 1: Run the full PlayMode suite**

Run the test command without `-testFilter`, writing to `Temp/all-tests.xml` and `Temp/all-tests.log`.

Expected: exit code `0`, `failed="0"`. If anything fails, fix it before continuing; do not update the notes around a failing suite.

- [ ] **Step 2: Update the design status**

Change the design doc header from `Status: awaiting approval` to `Status: approved and implemented`.

- [ ] **Step 3: Update the starter notes**

In `docs/superpowers/notes/anime-fps-starter.md`:
- Controls table: change the fire row to `| Left mouse | fire (Rifle: one shot per press; hold for the SMG) |`.
- Weapons section: add `The Rifle is a 5-round bolt action: one shot, a 1 s bolt cycle, then the normal 2.2 s reload after the fifth round or on R. Tracers and the muzzle flash start at the held gun's muzzle; the camera ray still decides aim and damage.`
- Add to the weapon-slot bullet list: `- muzzle is derived from the viewmodel (a child named Muzzle overrides it), so replacement viewmodels work without code changes`.

- [ ] **Step 4: Commit the docs**

```powershell
git -C 'D:\Making Games\Anime fps game' add 'docs/superpowers/specs/2026-11-18-bolt-action-rifle-viewmodel-design.md' 'docs/superpowers/notes/anime-fps-starter.md'
git -C 'D:\Making Games\Anime fps game' commit -m "docs: record the bolt-action rifle and muzzle effects"
```

- [ ] **Step 5: Hand back to the user**

Ask the user to reopen the Unity editor, press Play, and check: the K98 is visible and upright, one shot per click with a 1 s cycle, a 5/30 HUD, and tracers leaving the barrel. Report anything that looks off (position, scale, dip amount) as a tuning request, not a bug.
