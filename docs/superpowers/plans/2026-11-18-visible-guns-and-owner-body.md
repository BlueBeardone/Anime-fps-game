# Visible Guns and Owner Body Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every weapon slot show a gun at a believable size, and let the owner see their own jacket, arms and legs instead of a floating camera.

**Architecture:** Two independent, data-and-visibility changes. The scale fix edits existing serialized slot data in the `Player` prefab and its generator. The body fix replaces one owner/remote renderer-visibility rule in `PlayerWeapons` with a pure, unit-testable predicate. No RPC, weapon-rule, or effect code changes.

**Tech Stack:** Unity 6000.6.9f1, URP, Netcode for GameObjects, PlayMode tests (NUnit, `AnimeFpsGame.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-18-visible-rifle-and-owner-body-design.md`

## Global Constraints

- Unity Editor must stay closed while batch tests run; launch tests through
  `C:\Users\baren\AppData\Local\Temp\opencode\run-unity-tests.ps1`.
- Full suite must end `failed="0"` before the final commit.
- Do not change damage, cadence, reload, ammo, tracer, muzzle or RPC behaviour.
- Do not change what remote clients see, except that the rule stays exactly "whole soldier plus the
  slot's weapon".
- The Shotgun slot intentionally reuses the K98 viewmodel; leave that as it is.
- Do not touch unrelated work in the working tree; commit only the files listed per task.

## Review Focus

- Switching slots at runtime: each slot must show its own gun with a believable size and the
  correct body/weapon renderers, not a stale one (Task 1 slot loop, Task 2 predicate tests).
- Owner looking at their body: the camera must never sit inside an enabled body renderer, or the
  screen is filled with mesh interior (Task 2 scene test).
- Remote players: head and helmet must stay enabled and exactly one weapon mesh must show, or
  everyone else becomes a headless mannequin carrying every gun (Task 2 predicate test).
- The enlarged gun: it must still be inside the camera frustum, barrel aligned, with the muzzle in
  front of the camera and under 3 m away (existing `RifleViewmodelIsVisibleInFrontOfCamera`,
  `MuzzleIsInFrontOfTheCamera`; re-run in Task 3).
- The enlarged gun: it must not cross the near plane at the stock end (0.31 m at scale 0.5 versus a
  0.02 m near plane; the frustum test fails if it does).

---

### Task 1: Visible Guns in All Four Slots

**Files:**
- Modify: `Assets/_Project/Prefabs/Player/Player.prefab` (four `viewmodelScale` lines)
- Modify: `Assets/_Project/Editor/ProjectSetup.cs:382-389`
- Test: `Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs`

**Interfaces:**
- Consumes: `PlayerWeapons.SetSlot(byte)`, `PlayerWeapons.CurrentViewmodel`, `PlayerWeapons.SlotCount`.
- Produces: slot data only; no API.

- [ ] **Step 1: Write the failing test**

Append to `PlayerWeaponTests`:

```csharp
[UnityTest]
public IEnumerator EveryWeaponSlotShowsAVisibleGun()
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
    Assert.AreEqual(4, weapons.SlotCount);

    for (var slot = 0; slot < weapons.SlotCount; slot++)
    {
        weapons.SetSlot((byte)slot);
        yield return null;

        var viewmodel = weapons.CurrentViewmodel;
        Assert.IsNotNull(viewmodel, $"Slot {slot} must instantiate a viewmodel");

        var renderers = viewmodel.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers, $"Slot {slot} viewmodel must have geometry");

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        Assert.Greater(longest, 0.1f,
            $"Slot {slot} viewmodel is {longest:F3} m long; the placeholder scale made it invisible");
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.PlayerWeaponTests -Results 'D:/Making Games/Anime fps game/Temp/scale.xml' -Log 'D:/Making Games/Anime fps game/Temp/scale.log'`

Expected: FAIL on slot 0 with a message like `Slot 0 viewmodel is 0.093 m long`.

- [ ] **Step 3: Raise the four scales**

In `Player.prefab`, replace all four `viewmodelScale: 0.08` lines with `viewmodelScale: 0.5`
(keep each slot's `viewmodelLocalPosition` and `viewmodelLocalEuler` untouched).

In `ProjectSetup.cs`, change the last numeric argument of each `ConfigureSlot` call from `0.08f` to
`0.5f` (Rifle, Handgun, SMG, Shotgun).

- [ ] **Step 4: Run the tests and verify they pass**

Run the Step 2 command again. Expected: `total=6 passed=6 failed=0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Prefabs/Player/Player.prefab Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs
git commit -m "feat: size every weapon viewmodel for the screen"
```

---

### Task 2: Owner Body Without Head or Hands

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs` (constants near `WeaponMeshNames`,
  `VisualsRoot` property, `ApplyVisibleWeaponMeshes`)
- Test: `Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs`

**Interfaces:**
- Consumes: `IsOwner` (NetworkBehaviour), `visualsRoot`, `WeaponDefinition.soldierMeshName`,
  `PlayerWeapons.ViewCamera`.
- Produces:
  - `public Transform VisualsRoot { get; }` — the soldier model root that holds every body and
    weapon renderer.
  - `public static bool ShouldRenderSoldierMesh(bool isOwner, string meshName, string soldierMeshName)`
    — the single visibility rule for the soldier hierarchy.

- [ ] **Step 1: Write the failing tests**

Add `using System.Linq;` is not needed. Append to `PlayerWeaponTests`:

```csharp
[Test]
public void OwnerShowsBodyButNotHeadOrHeldWeapon()
{
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(true, "Jacket", "K98"));
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(true, "Throusers", "K98"));
    Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "Helmet", "K98"));
    Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "Wehrmacht_soilder_A", "K98"));
    Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(true, "K98", "K98"));
}

[Test]
public void RemoteShowsWholeBodyAndOnlyTheHeldWeapon()
{
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Jacket", "K98"));
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Helmet", "K98"));
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "Wehrmacht_soilder_A", "K98"));
    Assert.IsTrue(PlayerWeapons.ShouldRenderSoldierMesh(false, "K98", "K98"));
    Assert.IsFalse(PlayerWeapons.ShouldRenderSoldierMesh(false, "Luger_P08", "K98"));
}

[UnityTest]
public IEnumerator OwnerSeesBodyWithoutHeadOrHeldWeapon()
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
    Assert.IsNotNull(weapons.VisualsRoot, "Player must have a Visuals root");

    var renderers = weapons.VisualsRoot.GetComponentsInChildren<Renderer>(true);

    Renderer Named(string name)
    {
        foreach (var renderer in renderers)
        {
            if (renderer.gameObject.name == name)
            {
                return renderer;
            }
        }

        Assert.Fail($"Missing renderer {name}");
        return null;
    }

    Assert.IsTrue(Named("Jacket").enabled, "Owner must see the jacket and its arms");
    Assert.IsTrue(Named("Throusers").enabled, "Owner must see their legs");
    Assert.IsFalse(Named("Helmet").enabled, "The helmet would swallow the camera");
    Assert.IsFalse(Named("Wehrmacht_soilder_A").enabled, "The head mesh would swallow the camera");
    Assert.IsFalse(Named("K98").enabled, "The held gun must be the viewmodel, not the body's mesh");

    var eye = weapons.ViewCamera.transform.position;
    foreach (var renderer in renderers)
    {
        if (!renderer.enabled)
        {
            continue;
        }

        Assert.IsFalse(renderer.bounds.Contains(eye),
            $"{renderer.gameObject.name} is enabled and contains the camera");
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.PlayerWeaponTests -Results 'D:/Making Games/Anime fps game/Temp/body.xml' -Log 'D:/Making Games/Anime fps game/Temp/body.log'`

Expected: compile error, `'PlayerWeapons' does not contain a definition for 'ShouldRenderSoldierMesh'`
(and no `VisualsRoot`).

- [ ] **Step 3: Implement the rule**

In `PlayerWeapons.cs`, next to `WeaponMeshNames`:

```csharp
// Meshes whose geometry contains the owner's eye point: hiding them keeps the camera out of the
// soldier's own head. Wehrmacht_soilder_A also carries the hands, so the owner has no hands yet.
static readonly string[] OwnerHiddenMeshNames = { "Helmet", "Wehrmacht_soilder_A" };
```

Add beside the other public accessors:

```csharp
public Transform VisualsRoot => visualsRoot;
```

Add the predicate:

```csharp
/// <summary>Owner: body only, no head/helmet and no held weapon mesh. Everyone else: the whole
/// soldier plus exactly the weapon for the active slot.</summary>
public static bool ShouldRenderSoldierMesh(bool isOwner, string meshName, string soldierMeshName)
{
    var isWeapon = System.Array.IndexOf(WeaponMeshNames, meshName) >= 0;

    if (isOwner)
    {
        return !isWeapon && System.Array.IndexOf(OwnerHiddenMeshNames, meshName) < 0;
    }

    return !isWeapon || meshName == soldierMeshName;
}
```

Replace the body of `ApplyVisibleWeaponMeshes` with:

```csharp
void ApplyVisibleWeaponMeshes(WeaponDefinition definition)
{
    if (visualsRoot == null)
    {
        return;
    }

    // Keep the Visuals root active: it carries the Animator that NetworkAnimator replicates,
    // and a disabled Animator silently drops every SetInteger call.
    var soldierMeshName = definition != null ? definition.soldierMeshName : null;
    foreach (var renderer in visualsRoot.GetComponentsInChildren<Renderer>(true))
    {
        renderer.enabled = ShouldRenderSoldierMesh(IsOwner, renderer.gameObject.name, soldierMeshName);
    }
}
```

Update the method's doc comment to: `/// <summary>Owner sees their body without head or hands; other clients see the soldier and one gun.</summary>`

- [ ] **Step 4: Run the tests and verify they pass**

Run the Step 2 command. Expected: `total=9 passed=9 failed=0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Player/PlayerWeapons.cs Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs
git commit -m "feat: show the owner's body below the head"
```

---

### Task 3: Full Suite and Notes

**Files:**
- Modify: `docs/superpowers/specs/2026-11-18-visible-rifle-and-owner-body-design.md`
- Modify: `docs/superpowers/notes/anime-fps-starter.md`

**Interfaces:**
- Consumes: everything above.
- Produces: no code.

- [ ] **Step 1: Run the full PlayMode suite**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Results 'D:/Making Games/Anime fps game/Temp/all-tests.xml' -Log 'D:/Making Games/Anime fps game/Temp/all-tests.log' -TimeoutMinutes 30`

Expected: exit code `0`, `failed="0"`, 75 tests or more.

- [ ] **Step 2: Update the design status**

Change the header `Status: awaiting approval` to `Status: approved and implemented`.

- [ ] **Step 3: Update the starter notes**

In `docs/superpowers/notes/anime-fps-starter.md`, replace:

```
Each client spawns on its own marker (`SpawnPoint 1`–`4` in `Main.unity`); you see other players'
soldiers, never your own body.
```

with:

```
Each client spawns on its own marker (`SpawnPoint 1`–`4` in `Main.unity`). You see your own jacket,
arms and legs, but not your head, hands or a body-carried weapon: the held gun is the viewmodel.
Other players see your full soldier plus the weapon for your active slot.
```

In the Weapons section, after the bolt-action paragraph, add:

```
Every slot's `viewmodelScale` is `0.5`; the original `0.08` made each gun about 9 cm long on
screen. Position, rotation and scale are per-slot placeholder tuning values, safe to edit in the
Inspector.
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-11-18-visible-rifle-and-owner-body-design.md docs/superpowers/notes/anime-fps-starter.md
git commit -m "docs: record the visible guns and owner body"
```

---

## Self-Review Notes

- Spec coverage: scale for all four slots (Task 1), owner body without head/helmet/weapon and
  unchanged remote appearance (Task 2), tests and docs (Tasks 1–3). No spec item lacks a task.
- Types: `ShouldRenderSoldierMesh(bool, string, string)` and `VisualsRoot` are defined in Task 2 and
  used only there.
- Known accepted risk, not a task: crouching lowers the eye to 1.05 m, inside the jacket's bind-pose
  bounds, and the soldier has no crouch animation. Visual check during playtest; the fix belongs
  with the future arms rig.
