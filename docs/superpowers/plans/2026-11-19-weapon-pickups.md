# Weapon Pickups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put networked guns on the range that show a Call of Duty–style bottom-screen prompt ("Press F to switch") and swap into the player's held slot, with the old gun dropped at their feet carrying its own ammo.

**Architecture:** Ground guns are `NetworkObject`s (one prefab, four scene instances). Pressing F goes through a server RPC that validates distance and cooldown, swaps the player's networked carried archetype, despawns the pickup, and spawns a drop carrying the outgoing gun's clamped ammo. `PlayerWeapons` turns its fixed slot catalog into a networked per-player loadout. The HUD renders the real gun model into a RenderTexture for the prompt icon.

**Tech Stack:** Unity 6000.6.9f1, URP, Input System 1.20.1, Netcode for GameObjects 2.14.0, PlayMode tests (NUnit, `AnimeFpsGame.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-19-weapon-pickups-design.md`

## Global Constraints

- Unity Editor must stay closed while batch runs happen. Run tests through
  `C:\Users\baren\AppData\Local\Temp\opencode\run-unity-tests.ps1`.
- Regenerate scene/prefab/input wiring with:
  `& 'E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\Making Games\Anime fps game' -executeMethod AnimeFpsGame.EditorTools.ProjectSetup.RunAll -logFile 'D:\Making Games\Anime fps game\Temp\project-setup.log'`
  Expected: exit code `0` and `[ProjectSetup] RunAll complete` in the log.
- `Assets/_Project/Editor/ProjectSetup.cs` is authoritative. Any scene object, prefab, input
  action or network-prefab registration must be generated there and survive `Run All`.
- Ammo stays owner-authoritative. The server clamps reported ammo; it does not simulate it.
- `BuildGuns` must keep running on every peer: the server's `guns[slot].Stats` is what
  `FireServerRpc` validates remote shots against.
- Keep namespaces: `AnimeFpsGame.Weapons`, `AnimeFpsGame.Player`, `AnimeFpsGame.UI`,
  `AnimeFpsGame.Input`, tests in `AnimeFpsGame.Tests`.
- Existing PlayMode tests must stay green. Commit only the files listed for each task.

## Review Focus

- **A spent gun must not refill by swapping.** Fire the rifle down, swap it for a ground gun, then
  check the dropped pickup's magazine/reserve equal what was left. Pinned in Task 2
  (`RestoreAmmoClampsAndRaisesEvents`) and Task 6 (`PickupFlowCarriesAmmoBothWays`).
- **No instant re-grab or duplicate gun.** A freshly dropped gun must be invisible to pickup selection
  for 0.75 s, and a second server request for the same pickup must fail. Pinned in Task 5
  (`FindNearestIndexSkipsCooldownAndHeldArchetype`) and Task 6 (`PickupFlowCarriesAmmoBothWays`).
- **Server shot validation after a swap.** `guns[slot].Stats` must match the new archetype or every
  remote shot from that slot is rejected. Pinned in Task 2
  (`SwapSlotServerChangesOwnerAndServerStats`).
- **Shotgun and rifle prompts are pixel-identical art.** The prompt must show the display name next to
  the picture. Pinned in Task 7 (`PickupPromptShowsNameAndAction`).
- **Remote players must see the swapped gun.** The visible body mesh resolves from the networked
  carried archetype, not the serialized initial slot. Pinned in Task 2
  (`SwapSlotServerChangesOwnerAndServerStats`, which asserts `CarriedArchetype` and `DefinitionFor`).

---

## Task 0: Baseline

**Files:**
- Commit: `docs/superpowers/specs/2026-11-19-weapon-pickups-design.md`, `docs/superpowers/plans/2026-11-19-weapon-pickups.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a green baseline and a clean commit to build on.

- [ ] **Step 1: Run the existing PlayMode suite**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Results 'D:/Making Games/Anime fps game/Temp/pickup-baseline.xml' -Log 'D:/Making Games/Anime fps game/Temp/pickup-baseline.log' -TimeoutMinutes 30`

Expected: `failed=0`. If it fails, stop and report the failure before changing anything.

- [ ] **Step 2: Commit the approved spec and plan**

```bash
git -C 'D:/Making Games/Anime fps game' add docs/superpowers/specs/2026-11-19-weapon-pickups-design.md docs/superpowers/plans/2026-11-19-weapon-pickups.md
git -C 'D:/Making Games/Anime fps game' commit -m "docs: approve weapon pickup design and plan"
```

---

## Task 1: Restore Ammo on a Gun

**Files:**
- Modify: `Assets/_Project/Scripts/Weapons/IGun.cs`
- Modify: `Assets/_Project/Scripts/Weapons/Gun.cs`
- Test: `Assets/_Project/Tests/PlayMode/GunTests.cs`

**Interfaces:**
- Consumes: `Gun.AmmoInMagazine`, `Gun.ReserveAmmo`, `GunStats.magazineSize`, `GunStats.reserveAmmo`.
- Produces:
  - `void IGun.RestoreAmmo(int magazine, int reserve)`
  - `Gun.RestoreAmmo(int magazine, int reserve)` — clamps both to the stats, cancels any reload,
    raises `AmmoChanged` and `ReloadStateChanged`.

- [ ] **Step 1: Write the failing test**

Append to `GunTests`:

```csharp
[Test]
public void RestoreAmmoClampsAndRaisesEvents()
{
    var gun = new Rifle(TestStats());
    var ammoEvents = 0;
    var reloadEvents = 0;
    gun.AmmoChanged += () => ammoEvents++;
    gun.ReloadStateChanged += () => reloadEvents++;

    gun.RestoreAmmo(2, 4);
    Assert.AreEqual(2, gun.AmmoInMagazine);
    Assert.AreEqual(4, gun.ReserveAmmo);

    gun.RestoreAmmo(99, 99);
    Assert.AreEqual(3, gun.AmmoInMagazine, "Magazine clamps to the stats");
    Assert.AreEqual(6, gun.ReserveAmmo, "Reserve clamps to the stats");
    Assert.IsFalse(gun.IsReloading);
    Assert.GreaterOrEqual(ammoEvents, 2);
    Assert.GreaterOrEqual(reloadEvents, 2);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.GunTests -Results 'D:/Making Games/Anime fps game/Temp/restore-ammo.xml' -Log 'D:/Making Games/Anime fps game/Temp/restore-ammo.log'`

Expected: compile error, `'Rifle' does not contain a definition for 'RestoreAmmo'`.

- [ ] **Step 3: Implement `RestoreAmmo`**

Add `void RestoreAmmo(int magazine, int reserve);` to `IGun` under `ResetAmmo()`.

In `Gun`, add:

```csharp
/// <summary>Replaces the live ammo with carried rounds, e.g. a gun picked up off the ground.</summary>
public void RestoreAmmo(int magazine, int reserve)
{
    ammoInMagazine = Mathf.Clamp(magazine, 0, stats.magazineSize);
    reserveAmmo = Mathf.Clamp(reserve, 0, stats.reserveAmmo);
    isReloading = false;
    reloadRemaining = 0f;
    boltRemaining = 0f;
    nextFireTime = float.NegativeInfinity;
    bloomDegrees = 0f;
    timeSinceLastShot = 999f;
    sprayIndex = 0;
    AmmoChanged?.Invoke();
    ReloadStateChanged?.Invoke();
}
```

- [ ] **Step 4: Run the test and verify it passes**

Run the Step 2 command again. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/Weapons/IGun.cs Assets/_Project/Scripts/Weapons/Gun.cs Assets/_Project/Tests/PlayMode/GunTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: let a gun take carried ammo"
```

---

## Task 2: Networked Carried Loadout and Ammo-Aware Swap

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Test: `Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs`

**Interfaces:**
- Consumes: `GunArchetype`, `GunFactory.Create`, `IGun.RestoreAmmo`, `WeaponDefinition.stats.stats`.
- Produces:
  - `public WeaponDefinition DefinitionFor(GunArchetype archetype)` — catalog lookup.
  - `public GunArchetype CarriedArchetype(byte slot)` — networked current content, with serialized
    fallback before sync.
  - `public void SetPendingPickupAmmo(byte slot, GunArchetype archetype, int magazine, int reserve)` —
    tells the next rebuild of that slot which rounds to restore.
  - `public GunArchetype SwapSlotServer(byte slot, GunArchetype incoming, int magazine, int reserve)` —
    server-only; returns the outgoing archetype.
  - `DefinitionFor` and `CarriedArchetype` are consumed by the HUD (Task 7).

- [ ] **Step 1: Write the failing test**

Append to `PlayerWeaponTests`:

```csharp
[UnityTest]
public IEnumerator SwapSlotServerChangesOwnerAndServerStats()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    Assert.IsNotNull(weapons);
    var slot = (byte)weapons.CurrentSlot;
    Assert.AreEqual(GunArchetype.Rifle, weapons.CarriedArchetype(slot));
    Assert.IsNotNull(weapons.DefinitionFor(GunArchetype.Handgun));

    var outgoing = weapons.SwapSlotServer(slot, GunArchetype.Handgun, 5, 7);
    yield return null;

    Assert.AreEqual(GunArchetype.Rifle, outgoing);
    Assert.AreEqual(GunArchetype.Handgun, weapons.CarriedArchetype(slot));
    Assert.AreEqual(GunArchetype.Handgun, weapons.ActiveGun.Stats.archetype,
        "Server stats must follow the swap or remote shot validation breaks");
    Assert.AreEqual(5, weapons.ActiveGun.AmmoInMagazine);
    Assert.AreEqual(7, weapons.ActiveGun.ReserveAmmo);
    Assert.AreEqual(GunArchetype.Handgun, weapons.DefinitionFor(weapons.CarriedArchetype(slot))
        .stats.stats.archetype);

    weapons.SwapSlotServer(slot, GunArchetype.Rifle, 3, 12);
    yield return null;

    Assert.AreEqual(GunArchetype.Rifle, weapons.CarriedArchetype(slot));
    Assert.AreEqual(3, weapons.ActiveGun.AmmoInMagazine);
    Assert.AreEqual(12, weapons.ActiveGun.ReserveAmmo);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.PlayerWeaponTests -Results 'D:/Making Games/Anime fps game/Temp/swap-slot.xml' -Log 'D:/Making Games/Anime fps game/Temp/swap-slot.log'`

Expected: compile error, no `CarriedArchetype` / `SwapSlotServer` / `DefinitionFor`.

- [ ] **Step 3: Implement the carried loadout**

In `PlayerWeapons`, add near the other network state:

```csharp
readonly NetworkList<byte> carriedArchetypes = new();

byte pendingSwapSlot;
GunArchetype pendingSwapArchetype;
int pendingSwapMagazine;
int pendingSwapReserve;
bool hasPendingSwap;
```

Add the public API:

```csharp
public WeaponDefinition DefinitionFor(GunArchetype archetype)
{
    if (slots == null)
    {
        return null;
    }

    foreach (var slot in slots)
    {
        if (slot?.stats?.stats != null && slot.stats.stats.archetype == archetype)
        {
            return slot;
        }
    }

    return null;
}

public GunArchetype CarriedArchetype(byte slot)
{
    if (slots == null || slots.Length == 0)
    {
        return GunArchetype.Rifle;
    }

    var index = Mathf.Clamp(slot, 0, slots.Length - 1);
    if (carriedArchetypes.Count > index)
    {
        return (GunArchetype)carriedArchetypes[index];
    }

    return slots[index].stats.stats.archetype;
}

public void SetPendingPickupAmmo(byte slot, GunArchetype archetype, int magazine, int reserve)
{
    pendingSwapSlot = slot;
    pendingSwapArchetype = archetype;
    pendingSwapMagazine = magazine;
    pendingSwapReserve = reserve;
    hasPendingSwap = true;
}

public GunArchetype SwapSlotServer(byte slot, GunArchetype incoming, int magazine, int reserve)
{
    if (!IsServer || slots == null || carriedArchetypes.Count <= slot)
    {
        return CarriedArchetype(slot);
    }

    var outgoing = CarriedArchetype(slot);
    if (outgoing == incoming)
    {
        return outgoing;
    }

    SetPendingPickupAmmo(slot, incoming, magazine, reserve);
    carriedArchetypes[slot] = (byte)incoming;
    return outgoing;
}
```

In `OnNetworkSpawn`, after the existing `activeSlot` subscription, add:

```csharp
carriedArchetypes.OnValueChanged += OnCarriedArchetypesChanged;
if (IsServer && slots != null && carriedArchetypes.Count != slots.Length)
{
    carriedArchetypes.Clear();
    for (var i = 0; i < slots.Length; i++)
    {
        carriedArchetypes.Add((byte)slots[i].stats.stats.archetype);
    }
}
```

In `OnNetworkDespawn`, remove the subscription:
`carriedArchetypes.OnValueChanged -= OnCarriedArchetypesChanged;`

Add the change handler and rebuild:

```csharp
void OnCarriedArchetypesChanged(NetworkListEvent<byte> change)
{
    // Initial sync arrives as Add events. Guns were already built from the serialized catalog;
    // only a real slot change should rebuild one.
    if (change.Type != NetworkListEvent<byte>.EventType.Value)
    {
        return;
    }

    var index = change.Index;
    if (guns == null || index < 0 || index >= guns.Length)
    {
        return;
    }

    RebuildSlotGun(index);
    if (index == CurrentSlot)
    {
        ApplySlot((byte)index);
    }
}

void RebuildSlotGun(int index)
{
    var archetype = (GunArchetype)carriedArchetypes[index];
    var definition = DefinitionFor(archetype);
    var stats = definition?.stats?.stats;
    if (stats == null)
    {
        return;
    }

    var gun = GunFactory.Create(stats);
    if (hasPendingSwap && pendingSwapSlot == index && pendingSwapArchetype == archetype)
    {
        gun.RestoreAmmo(pendingSwapMagazine, pendingSwapReserve);
        hasPendingSwap = false;
    }

    guns[index] = gun;
    if (IsOwner && index == CurrentSlot)
    {
        BindActiveGun();
    }
}
```

Change `ApplySlot` to resolve the definition through the carried archetype, with the serialized
catalog as the fallback:

```csharp
var definition = DefinitionFor(CarriedArchetype(slot));
if (definition == null)
{
    definition = slots[Mathf.Clamp(slot, 0, slots.Length - 1)];
}
```

In `ApplyDeathVisuals`, replace the direct `slots[...]` lookup with:

```csharp
var definition = DefinitionFor(CarriedArchetype((byte)Mathf.Clamp(CurrentSlot, 0, slots.Length - 1)));
if (definition != null)
{
    ApplyVisibleWeaponMeshes(definition);
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the Step 2 command again. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/Player/PlayerWeapons.cs Assets/_Project/Tests/PlayMode/PlayerWeaponTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: make each player's carried slots networked and swappable"
```

---

## Task 3: Pickup Input Action

**Files:**
- Modify: `Assets/_Project/Editor/ProjectSetup.cs` (`EnsureReloadAction` and its call site)
- Modify: `Assets/_Project/Scripts/Input/InputReader.cs`
- Modify: `Assets/InputSystem_Actions.inputactions` (generated by ProjectSetup)
- Test: `Assets/_Project/Tests/PlayMode/InputReaderTests.cs`

**Interfaces:**
- Consumes: `InputReader`'s existing action cache and `PlayerInputBindings.EnsureLoaded`.
- Produces:
  - `public bool InputReader.PickupPressed` — `Pickup` action, `WasPressedThisFrame`.
  - A `Pickup` action in the `Player` map bound to `<Keyboard>/f` and `<Gamepad>/buttonNorth`.

- [ ] **Step 1: Write the failing test**

Append to `InputReaderTests`:

```csharp
[UnityTest]
public IEnumerator SpawnedPlayerHasPickupActionBoundToF()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 10f;
    PlayerWeapons weapons = null;
    while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    Assert.IsNotNull(weapons);
    var actions = weapons.GetComponent<PlayerInput>().actions;
    var pickup = actions.FindAction("Pickup");
    Assert.IsNotNull(pickup, "Pickup action must exist on the Player map");

    var boundToF = false;
    foreach (var binding in pickup.bindings)
    {
        if (binding.effectivePath == "<Keyboard>/f")
        {
            boundToF = true;
        }
    }

    Assert.IsTrue(boundToF, "Pickup must be bound to F");
    Assert.IsFalse(weapons.GetComponent<InputReader>().PickupPressed);
}
```

No extra `using` directives are needed for this test.

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.InputReaderTests -Results 'D:/Making Games/Anime fps game/Temp/pickup-input.xml' -Log 'D:/Making Games/Anime fps game/Temp/pickup-input.log'`

Expected: compile error for `PickupPressed`, or a failed assertion that the `Pickup` action is missing.

- [ ] **Step 3: Add the input action in ProjectSetup**

Rename `EnsureReloadAction()` to `EnsurePlayerActions()` and update its call site in
`BuildPlayerPrefab`. **Do not** keep the early `return` when Reload already exists, or Pickup is
never added on the existing project. Restructure it so each action is checked on its own:

```csharp
static void EnsurePlayerActions()
{
    var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
    if (asset == null)
    {
        Debug.LogWarning("[ProjectSetup] Input actions asset not found: " + ActionsPath);
        return;
    }

    var map = asset.FindActionMap("Player", false);
    if (map == null)
    {
        return;
    }

    // PlayerInput components can leave the imported asset enabled while the scene is open.
    var wasEnabled = asset.enabled;
    asset.Disable();
    var changed = false;

    if (map.FindAction("Reload", false) == null)
    {
        var reload = map.AddAction("Reload", InputActionType.Button);
        reload.AddBinding("<Keyboard>/r");
        reload.AddBinding("<Gamepad>/dpad/down");
        changed = true;
    }

    if (map.FindAction("Pickup", false) == null)
    {
        var pickup = map.AddAction("Pickup", InputActionType.Button);
        pickup.AddBinding("<Keyboard>/f");
        pickup.AddBinding("<Gamepad>/buttonNorth");
        changed = true;
    }

    if (changed)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }

    if (wasEnabled)
    {
        asset.Enable();
    }
}
```

- [ ] **Step 4: Expose `PickupPressed` in `InputReader`**

Add an `InputAction pickup;` field, resolve it in `Awake` with
`pickup = actions.FindAction("Pickup");`, and add:

```csharp
public bool PickupPressed => pickup != null && pickup.WasPressedThisFrame();
```

- [ ] **Step 5: Regenerate the scene and input asset**

Run the `RunAll` command from the Global Constraints. Expected: exit code `0` and
`[ProjectSetup] RunAll complete`.

Confirm `Assets/InputSystem_Actions.inputactions` now contains a `"name": "Pickup"` action and a
`<Keyboard>/f` binding.

- [ ] **Step 6: Run the test and verify it passes**

Run the Step 2 command again. Expected: `failed=0`.

- [ ] **Step 7: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Scripts/Input/InputReader.cs Assets/InputSystem_Actions.inputactions Assets/_Project/Tests/PlayMode/InputReaderTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: bind F to a pickup action"
```

---

## Task 4: Ground Pickup Object, Prefab, Registration and Range Placement

**Files:**
- Create: `Assets/_Project/Scripts/Weapons/WeaponPickup.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs`
- Create (generated): `Assets/_Project/Prefabs/Weapons/WeaponPickup.prefab`
- Modify (generated): `Assets/_Project/Scenes/Main.unity`
- Modify (generated): `Assets/DefaultNetworkPrefabs.asset`
- Test: `Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs`

**Interfaces:**
- Consumes: `GunArchetype`, `GunStatsAsset`, `NetworkObject`, `NetworkVariable`.
- Produces:
  - `public class WeaponPickupEntry { public GunArchetype archetype; public GameObject worldPrefab; public GunStatsAsset stats; }`
  - `public class WeaponPickup : NetworkBehaviour` with:
    - `public const float DropCooldownSeconds = 0.75f;`
    - `public static IReadOnlyList<WeaponPickup> All`
    - `public GunArchetype Archetype`
    - `public int MagazineAmmo`
    - `public int ReserveAmmo`
    - `public bool IsRuntimeDrop`
    - `public bool IsInDropCooldown`
    - `public void Initialize(GunArchetype archetype, int magazine, int reserve)` — server, before
      `NetworkObject.Spawn()`; also marks `IsRuntimeDrop = true`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs`:

```csharp
using System.Collections;
using System.Linq;
using AnimeFpsGame.Weapons;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class WeaponPickupTests
    {
        [UnityTearDown]
        public IEnumerator TearDownNetwork()
        {
            var manager = NetworkManager.Singleton;
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
        public IEnumerator MainHasFourDistinctGroundGuns()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var deadline = Time.realtimeSinceStartup + 15f;
            while (WeaponPickup.All.Count(p => p.IsSpawned) < 4 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            var spawned = WeaponPickup.All.Where(p => p.IsSpawned).ToArray();
            Assert.AreEqual(4, spawned.Length, "The range must have four ground guns");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    GunArchetype.Rifle, GunArchetype.Handgun,
                    GunArchetype.Shotgun, GunArchetype.SubmachineGun
                },
                spawned.Select(p => p.Archetype).ToArray());

            foreach (var pickup in spawned)
            {
                Assert.IsFalse(pickup.IsRuntimeDrop, "Scene pickups are not runtime drops");
                Assert.Greater(pickup.MagazineAmmo, 0, $"{pickup.Archetype} must have loaded rounds");
                var renderers = pickup.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers, $"{pickup.Archetype} must show a gun mesh");
            }
        }
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.WeaponPickupTests -Results 'D:/Making Games/Anime fps game/Temp/pickups.xml' -Log 'D:/Making Games/Anime fps game/Temp/pickups.log'`

Expected: compile error, `WeaponPickup` does not exist.

- [ ] **Step 3: Implement `WeaponPickup`**

Create `Assets/_Project/Scripts/Weapons/WeaponPickup.cs`:

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    [System.Serializable]
    public class WeaponPickupEntry
    {
        public GunArchetype archetype;
        public GameObject worldPrefab;
        public GunStatsAsset stats;
    }

    /// <summary>A networked gun lying on the ground. Server owns its identity and ammo.</summary>
    public class WeaponPickup : NetworkBehaviour
    {
        public const float DropCooldownSeconds = 0.75f;

        static readonly List<WeaponPickup> Registry = new();

        [SerializeField] GunArchetype initialArchetype = GunArchetype.Rifle;
        [SerializeField] WeaponPickupEntry[] entries;

        readonly NetworkVariable<byte> archetype = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> magazineAmmo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> reserveAmmo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> runtimeDrop = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        bool initializedBySpawner;
        GameObject visual;

        public static IReadOnlyList<WeaponPickup> All
        {
            get
            {
                Registry.RemoveAll(p => p == null);
                return Registry;
            }
        }

        public GunArchetype Archetype => (GunArchetype)archetype.Value;
        public int MagazineAmmo => magazineAmmo.Value;
        public int ReserveAmmo => reserveAmmo.Value;

        /// <summary>True when the server spawned this from a swap. Networked so every peer agrees.</summary>
        public bool IsRuntimeDrop => runtimeDrop.Value;

        public float SpawnedAt { get; private set; }

        /// <summary>Only a freshly dropped gun is withheld; scene pickups are usable immediately.</summary>
        public bool IsInDropCooldown =>
            IsRuntimeDrop && Time.time - SpawnedAt < DropCooldownSeconds;

        /// <summary>Server-only, called before Spawn by a runtime drop.</summary>
        public void Initialize(GunArchetype value, int magazine, int reserve)
        {
            initializedBySpawner = true;
            runtimeDrop.Value = true;
            archetype.Value = (byte)value;
            magazineAmmo.Value = magazine;
            reserveAmmo.Value = reserve;
        }

        public override void OnNetworkSpawn()
        {
            SpawnedAt = Time.time;

            if (IsServer && !initializedBySpawner)
            {
                var entry = FindEntry(initialArchetype);
                archetype.Value = (byte)initialArchetype;
                if (entry?.stats?.stats != null)
                {
                    magazineAmmo.Value = entry.stats.stats.magazineSize;
                    reserveAmmo.Value = entry.stats.stats.reserveAmmo;
                }
            }

            Registry.Add(this);
            BuildVisual();
        }

        public override void OnNetworkDespawn()
        {
            Registry.Remove(this);
        }

        void Update()
        {
            // Spin the visual only. The root transform is the networked position and has no
            // NetworkTransform, so rotating it would drift apart between server and clients.
            if (visual != null)
            {
                visual.transform.Rotate(0f, 24f * Time.deltaTime, 0f, Space.World);
            }
        }

        void BuildVisual()
        {
            if (visual != null)
            {
                Destroy(visual);
            }

            var entry = FindEntry(Archetype);
            if (entry?.worldPrefab == null)
            {
                return;
            }

            visual = Instantiate(entry.worldPrefab, transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            visual.transform.localScale = Vector3.one;

            // The pickup registry handles proximity; stray model colliders must not block movement.
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        WeaponPickupEntry FindEntry(GunArchetype value)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.archetype == value)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: Generate the prefab and register it**

In `ProjectSetup`, add:

```csharp
public const string PickupPrefabPath = ProjectRoot + "/Prefabs/Weapons/WeaponPickup.prefab";

static void EnsurePickupPrefab()
{
    EnsureFolder(ProjectRoot + "/Prefabs/Weapons");

    var root = new GameObject("WeaponPickup");
    root.AddComponent<NetworkObject>();
    var pickup = root.AddComponent<AnimeFpsGame.Weapons.WeaponPickup>();

    var serialized = new SerializedObject(pickup);
    serialized.FindProperty("initialArchetype").enumValueIndex = (int)GunArchetype.Rifle;

    var entries = serialized.FindProperty("entries");
    entries.arraySize = 4;

    // Create the stat assets here, not in BuildPlayerPrefab: RunAll builds this prefab before the
    // player prefab, and the pickup needs the same magazine/reserve values the player's gun uses.
    var rifleStats = EnsureGunStatsAsset("Rifle", Rifle.CreateDefaultStats());
    var handgunStats = EnsureGunStatsAsset("Handgun", Handgun.CreateDefaultStats());
    var smgStats = EnsureGunStatsAsset("SMG", SubmachineGun.CreateDefaultStats());
    var shotgunStats = EnsureGunStatsAsset("Shotgun", Shotgun.CreateDefaultStats());

    var rows = new[]
    {
        (GunArchetype.Rifle, GunsDir + "/K98_prefab.prefab", rifleStats),
        (GunArchetype.Handgun, GunsDir + "/LugerP08_prefab.prefab", handgunStats),
        (GunArchetype.SubmachineGun, GunsDir + "/MP40_prefab.prefab", smgStats),
        (GunArchetype.Shotgun, GunsDir + "/K98_prefab.prefab", shotgunStats),
    };

    for (var i = 0; i < rows.Length; i++)
    {
        var row = entries.GetArrayElementAtIndex(i);
        row.FindPropertyRelative("archetype").enumValueIndex = (int)rows[i].Item1;
        row.FindPropertyRelative("worldPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(rows[i].Item2);
        row.FindPropertyRelative("stats").objectReferenceValue = rows[i].Item3;
    }

    serialized.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath);
    Object.DestroyImmediate(root);
    AssetDatabase.SaveAssets();

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
    var list = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>(
        "Assets/DefaultNetworkPrefabs.asset");
    if (prefab != null && list != null && !list.Contains(prefab))
    {
        list.Add(new Unity.Netcode.NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();
    }
}
```

- [ ] **Step 5: Place the four scene pickups**

Add to `ProjectSetup`:

```csharp
static readonly (string Name, GunArchetype Archetype, Vector3 Position)[] PickupSpots =
{
    ("Pickup Rifle", GunArchetype.Rifle, new Vector3(1.5f, 0.2f, -2f)),
    ("Pickup Handgun", GunArchetype.Handgun, new Vector3(-1.5f, 0.2f, -2f)),
    ("Pickup SMG", GunArchetype.SubmachineGun, new Vector3(3f, 0.2f, 4f)),
    ("Pickup Shotgun", GunArchetype.Shotgun, new Vector3(-3f, 0.2f, 4f)),
};

static void EnsureWeaponPickups(Scene scene)
{
    EnsurePickupPrefab();
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
    if (prefab == null)
    {
        return;
    }

    foreach (var spot in PickupSpots)
    {
        var existing = GameObject.Find(spot.Name);
        var pickup = existing;
        if (pickup == null)
        {
            pickup = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            pickup.name = spot.Name;
            pickup.transform.position = spot.Position;
            pickup.transform.rotation = Quaternion.identity;
        }

        var component = pickup.GetComponent<AnimeFpsGame.Weapons.WeaponPickup>();
        if (component == null)
        {
            continue;
        }

        var serialized = new SerializedObject(component);
        serialized.FindProperty("initialArchetype").enumValueIndex = (int)spot.Archetype;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }
}
```

Call `EnsureWeaponPickups(scene);` in `SetupScene` next to `EnsureTargetDummies();`.

Change `RunAll` to generate the pickup prefab before the scene:

```csharp
[MenuItem("Anime FPS/Run All")]
public static void RunAll()
{
    EnsurePickupPrefab();
    SetupScene();
    BuildPlayerPrefab();
    SetupNetworkManager();
    SetupMainMenu();
    Debug.Log("[ProjectSetup] RunAll complete");
}
```

Add `using UnityEngine.SceneManagement;` if `Scene` is unknown (it is
`UnityEngine.SceneManagement.Scene`).

- [ ] **Step 6: Regenerate and run the test**

Run the `RunAll` command, then the Step 2 test command. Expected: `failed=0`, and the log contains
`[ProjectSetup] RunAll complete`.

- [ ] **Step 7: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/Weapons/WeaponPickup.cs Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Prefabs/Weapons/WeaponPickup.prefab Assets/_Project/Scenes/Main.unity Assets/DefaultNetworkPrefabs.asset Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: place networked ground guns on the range"
```

---

## Task 5: Proximity Interactor

**Files:**
- Create: `Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs`
- Modify: `Assets/_Project/Editor/ProjectSetup.cs`
- Test: `Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs`

**Interfaces:**
- Consumes: `WeaponPickup.All`, `WeaponPickup.Archetype`, `WeaponPickup.IsRuntimeDrop`,
  `WeaponPickup.IsInDropCooldown`, `PlayerWeapons.CarriedArchetype`, `PlayerWeapons.CurrentSlot`.
- Produces:
  - `public readonly struct PlayerPickupInteractor.PickupCandidate`
    (`Position`, `Archetype`, `SpawnedAt`, `RuntimeDrop`).
  - `public static int PlayerPickupInteractor.FindNearestIndex(Vector3 playerPosition, float radius,
    GunArchetype held, float now, IReadOnlyList<PickupCandidate> candidates)` — -1 when none.
  - `public WeaponPickup NearestPickup { get; }`
  - `public GunArchetype? NearestArchetype { get; }`
  - `public const float DefaultPickupRadius = 3f;`

- [ ] **Step 1: Write the failing tests**

Append to `WeaponPickupTests`:

```csharp
[Test]
public void FindNearestIndexPicksClosestInRange()
{
    var candidates = new[]
    {
        new PlayerPickupInteractor.PickupCandidate(
            new Vector3(0f, 0f, 5f), GunArchetype.Rifle, 0f, false),
        new PlayerPickupInteractor.PickupCandidate(
            new Vector3(2f, 0f, 0f), GunArchetype.Handgun, 0f, false),
        new PlayerPickupInteractor.PickupCandidate(
            new Vector3(0f, 0f, 1f), GunArchetype.Rifle, 0f, false),
    };

    Assert.AreEqual(2, PlayerPickupInteractor.FindNearestIndex(
        Vector3.zero, 3f, GunArchetype.Shotgun, 10f, candidates));
    Assert.AreEqual(-1, PlayerPickupInteractor.FindNearestIndex(
        Vector3.zero, 0.5f, GunArchetype.Shotgun, 10f, candidates));
}

[Test]
public void FindNearestIndexSkipsCooldownAndHeldArchetype()
{
    var candidates = new[]
    {
        // A drop that just landed is invisible to selection for its cooldown.
        new PlayerPickupInteractor.PickupCandidate(Vector3.forward, GunArchetype.Handgun, 9.5f, true),
        // A scene pickup is usable at once, but this one is the archetype already held.
        new PlayerPickupInteractor.PickupCandidate(Vector3.forward, GunArchetype.Shotgun, 0f, false),
    };

    Assert.AreEqual(-1, PlayerPickupInteractor.FindNearestIndex(
        Vector3.zero, 3f, GunArchetype.Shotgun, 10f, candidates),
        "One is cooling down and the other is the held archetype");
}

[UnityTest]
public IEnumerator SpawnedPlayerTracksNearestGroundGun()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    var interactor = weapons.GetComponent<PlayerPickupInteractor>();
    Assert.IsNotNull(interactor, "The player must carry the pickup interactor");

    var held = weapons.CarriedArchetype((byte)weapons.CurrentSlot);
    var pickup = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held);
    weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>().enabled = false;
    weapons.transform.position = pickup.transform.position + new Vector3(0f, 1f, 1f);

    yield return null;
    yield return null;

    Assert.AreSame(pickup, interactor.NearestPickup);
    Assert.AreEqual(pickup.Archetype, interactor.NearestArchetype);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.WeaponPickupTests -Results 'D:/Making Games/Anime fps game/Temp/proximity.xml' -Log 'D:/Making Games/Anime fps game/Temp/proximity.log'`

Expected: compile error, `PlayerPickupInteractor` does not exist.

- [ ] **Step 3: Implement the candidate rule and owner scan**

Create `Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs`:

```csharp
using System.Collections.Generic;
using AnimeFpsGame.Input;
using AnimeFpsGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace AnimeFpsGame.Weapons
{
    /// <summary>Finds the ground gun the owner is standing next to and publishes it for the HUD.</summary>
    public class PlayerPickupInteractor : NetworkBehaviour
    {
        public const float DefaultPickupRadius = 3f;

        public readonly struct PickupCandidate
        {
            public PickupCandidate(Vector3 position, GunArchetype archetype, float spawnedAt,
                bool runtimeDrop)
            {
                Position = position;
                Archetype = archetype;
                SpawnedAt = spawnedAt;
                RuntimeDrop = runtimeDrop;
            }

            public Vector3 Position { get; }
            public GunArchetype Archetype { get; }
            public float SpawnedAt { get; }
            public bool RuntimeDrop { get; }
        }

        [SerializeField] PlayerWeapons weapons;
        [SerializeField] InputReader input;
        [SerializeField] WeaponPickup pickupPrefab;
        [SerializeField] float pickupRadius = DefaultPickupRadius;

        readonly List<PickupCandidate> candidates = new();
        readonly List<WeaponPickup> pickups = new();

        public WeaponPickup NearestPickup { get; private set; }
        public GunArchetype? NearestArchetype =>
            NearestPickup != null ? NearestPickup.Archetype : (GunArchetype?)null;

        public static int FindNearestIndex(Vector3 playerPosition, float radius,
            GunArchetype held, float now, IReadOnlyList<PickupCandidate> candidates)
        {
            var best = -1;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Archetype == held)
                {
                    continue;
                }

                if (candidate.RuntimeDrop &&
                    now - candidate.SpawnedAt < WeaponPickup.DropCooldownSeconds)
                {
                    continue;
                }

                var dx = candidate.Position.x - playerPosition.x;
                var dz = candidate.Position.z - playerPosition.z;
                var distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > radius || distance >= bestDistance)
                {
                    continue;
                }

                best = i;
                bestDistance = distance;
            }

            return best;
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            NearestPickup = null;
            if (weapons == null || weapons.IsDead)
            {
                return;
            }

            candidates.Clear();
            pickups.Clear();
            foreach (var pickup in WeaponPickup.All)
            {
                if (!pickup.IsSpawned || pickup.IsInDropCooldown)
                {
                    continue;
                }

                candidates.Add(new PickupCandidate(
                    pickup.transform.position, pickup.Archetype, pickup.SpawnedAt,
                    pickup.IsRuntimeDrop));
                pickups.Add(pickup);
            }

            var index = FindNearestIndex(transform.position, pickupRadius,
                weapons.CarriedArchetype((byte)weapons.CurrentSlot), Time.time, candidates);
            if (index >= 0)
            {
                NearestPickup = pickups[index];
            }
        }
    }
}
```

The pickup action and server request are added in Task 6; this task only publishes the target.

- [ ] **Step 4: Add the component to the player prefab**

In `ProjectSetup.BuildPlayerPrefab`, after `var weapons = ...` and the input reader exist, add:

```csharp
var pickupInteractor = root.AddComponent<AnimeFpsGame.Weapons.PlayerPickupInteractor>();
SetReference(pickupInteractor, "weapons", weapons);
SetReference(pickupInteractor, "input", inputReader);
SetReference(pickupInteractor, "pickupPrefab",
    AssetDatabase.LoadAssetAtPath<AnimeFpsGame.Weapons.WeaponPickup>(PickupPrefabPath));
```

`BuildPlayerPrefab` must call `EnsurePickupPrefab()` before this when the prefab is missing, so an
individual "Build Player Prefab" run still works.

- [ ] **Step 5: Regenerate and run the tests**

Run the `RunAll` command, then the Step 2 test command again. Expected: `failed=0`.

- [ ] **Step 6: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs Assets/_Project/Editor/ProjectSetup.cs Assets/_Project/Prefabs/Player/Player.prefab Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: find the nearest ground gun for the owner"
```

---

## Task 6: Server Pickup Flow, Drop and Ammo

**Files:**
- Modify: `Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs`
- Test: `Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs`

**Interfaces:**
- Consumes: `PlayerWeapons.SwapSlotServer`, `PlayerWeapons.SetPendingPickupAmmo`,
  `PlayerWeapons.DefinitionFor`, `WeaponPickup.Initialize`, `WeaponPickup.MagazineAmmo`,
  `WeaponPickup.ReserveAmmo`.
- Produces:
  - `public bool PlayerPickupInteractor.TryPickup(WeaponPickup target)` — owner entry point; on the
    host it applies immediately, on a client it sends the RPC.
  - `public bool PlayerPickupInteractor.ApplyPickupServer(WeaponPickup target, int outgoingMagazine,
    int outgoingReserve)` — server validation and swap.
  - `[ServerRpc] void TryPickupServerRpc(NetworkObjectReference target, int outgoingMagazine,
    int outgoingReserve)`.

- [ ] **Step 1: Write the failing tests**

Append to `WeaponPickupTests`:

```csharp
[UnityTest]
public IEnumerator PickupFlowCarriesAmmoBothWays()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    var interactor = weapons.GetComponent<PlayerPickupInteractor>();
    var slot = (byte)weapons.CurrentSlot;
    var held = weapons.CarriedArchetype(slot);
    var target = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held && !p.IsRuntimeDrop);

    // Spend two rounds so the drop must carry less than a full magazine.
    var gun = weapons.ActiveGun;
    gun.TryFire(Vector3.zero, Vector3.forward, 0f, true, true, out _);
    gun.TryFire(Vector3.zero, Vector3.forward, 10f, true, true, out _);
    var outgoingMagazine = gun.AmmoInMagazine;
    var outgoingReserve = gun.ReserveAmmo;
    var incomingMagazine = target.MagazineAmmo;
    var incomingReserve = target.ReserveAmmo;
    var incomingArchetype = target.Archetype;
    var targetObject = target.NetworkObject;

    weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>().enabled = false;
    weapons.transform.position = target.transform.position + new Vector3(0f, 1f, 1f);
    yield return null;
    yield return null;

    Assert.IsTrue(interactor.TryPickup(target), "Host pickup must apply immediately");
    yield return null;

    Assert.AreEqual(incomingArchetype, weapons.CarriedArchetype(slot));
    Assert.AreEqual(incomingMagazine, weapons.ActiveGun.AmmoInMagazine);
    Assert.AreEqual(incomingReserve, weapons.ActiveGun.ReserveAmmo);
    Assert.IsFalse(targetObject.IsSpawned, "The consumed ground gun despawns for everyone");

    var drop = WeaponPickup.All.FirstOrDefault(p => p.IsRuntimeDrop && p.Archetype == held);
    Assert.IsNotNull(drop, "The old gun must drop at the player's feet");
    Assert.AreEqual(outgoingMagazine, drop.MagazineAmmo);
    Assert.AreEqual(outgoingReserve, drop.ReserveAmmo);
    Assert.IsTrue(drop.IsInDropCooldown, "A fresh drop must not be instantly re-grabbable");
    Assert.IsFalse(interactor.ApplyPickupServer(drop, 0, 0),
        "A pickup in cooldown must reject a second request");
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.WeaponPickupTests -Results 'D:/Making Games/Anime fps game/Temp/pickup-flow.xml' -Log 'D:/Making Games/Anime fps game/Temp/pickup-flow.log'`

Expected: compile error, no `TryPickup` / `ApplyPickupServer`.

- [ ] **Step 3: Implement the owner request and server swap**

In `PlayerPickupInteractor`, add the input handling at the end of `Update` after `NearestPickup` is
resolved:

```csharp
if (input != null && input.PickupPressed && NearestPickup != null)
{
    TryPickup(NearestPickup);
}
```

Add the methods:

```csharp
public bool TryPickup(WeaponPickup target)
{
    if (!IsOwner || target == null || weapons == null || weapons.IsDead || weapons.ActiveGun == null)
    {
        return false;
    }

    var slot = (byte)weapons.CurrentSlot;
    weapons.SetPendingPickupAmmo(slot, target.Archetype, target.MagazineAmmo, target.ReserveAmmo);

    var outgoingMagazine = weapons.ActiveGun.AmmoInMagazine;
    var outgoingReserve = weapons.ActiveGun.ReserveAmmo;

    if (IsServer)
    {
        return ApplyPickupServer(target, outgoingMagazine, outgoingReserve);
    }

    TryPickupServerRpc(new NetworkObjectReference(target.NetworkObject),
        outgoingMagazine, outgoingReserve);
    return true;
}

[ServerRpc]
void TryPickupServerRpc(NetworkObjectReference target, int outgoingMagazine, int outgoingReserve)
{
    if (target.TryGet(out var networkObject))
    {
        ApplyPickupServer(networkObject.GetComponent<WeaponPickup>(),
            outgoingMagazine, outgoingReserve);
    }
}

public bool ApplyPickupServer(WeaponPickup target, int outgoingMagazine, int outgoingReserve)
{
    if (!IsServer || target == null || !target.IsSpawned || weapons == null || weapons.IsDead)
    {
        return false;
    }

    if (target.IsInDropCooldown)
    {
        return false;
    }

    if (Vector3.Distance(transform.position, target.transform.position) > pickupRadius + 0.5f)
    {
        return false;
    }

    var slot = (byte)weapons.CurrentSlot;
    var outgoing = weapons.CarriedArchetype(slot);
    if (outgoing == target.Archetype)
    {
        return false;
    }

    var incomingMagazine = target.MagazineAmmo;
    var incomingReserve = target.ReserveAmmo;
    var outgoingDefinition = weapons.DefinitionFor(outgoing);
    var outgoingStats = outgoingDefinition?.stats?.stats;
    if (outgoingStats == null)
    {
        return false;
    }

    outgoingMagazine = Mathf.Clamp(outgoingMagazine, 0, outgoingStats.magazineSize);
    outgoingReserve = Mathf.Clamp(outgoingReserve, 0, outgoingStats.reserveAmmo);

    weapons.SwapSlotServer(slot, target.Archetype, incomingMagazine, incomingReserve);
    target.NetworkObject.Despawn(true);
    SpawnDrop(outgoing, outgoingMagazine, outgoingReserve);
    return true;
}

void SpawnDrop(GunArchetype archetype, int magazine, int reserve)
{
    if (pickupPrefab == null)
    {
        return;
    }

    // Keep repeated swaps from filling the range forever.
    var drops = new List<WeaponPickup>();
    foreach (var pickup in WeaponPickup.All)
    {
        if (pickup.IsRuntimeDrop && pickup.IsSpawned)
        {
            drops.Add(pickup);
        }
    }

    if (drops.Count >= 12)
    {
        drops.Sort((a, b) => a.SpawnedAt.CompareTo(b.SpawnedAt));
        drops[0].NetworkObject.Despawn(true);
    }

    var forward = transform.forward;
    forward.y = 0f;
    if (forward.sqrMagnitude < 0.0001f)
    {
        forward = Vector3.forward;
    }

    var drop = Instantiate(pickupPrefab, transform.position + forward.normalized * 0.75f,
        Quaternion.identity);
    drop.Initialize(archetype, magazine, reserve);
    drop.NetworkObject.Spawn();
}
```

Add `using Unity.Netcode;` is already present.

- [ ] **Step 4: Run the tests and verify they pass**

Run the Step 2 command again. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/Weapons/PlayerPickupInteractor.cs Assets/_Project/Tests/PlayMode/WeaponPickupTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: swap and drop guns with their ammo on the server"
```

---

## Task 7: HUD Pickup Prompt and Gun Picture

**Files:**
- Modify: `Assets/_Project/Scripts/UI/WeaponHud.cs`
- Test: `Assets/_Project/Tests/PlayMode/WeaponHudTests.cs`

**Interfaces:**
- Consumes: `PlayerPickupInteractor.NearestPickup`, `PlayerPickupInteractor.NearestArchetype`,
  `PlayerWeapons.DefinitionFor`, `WeaponDefinition.viewmodelPrefab`, `WeaponDefinition.displayName`.
- Produces:
  - `public bool WeaponHud.PickupPromptVisible { get; }`
  - `public string WeaponHud.PickupPromptActionText { get; }`
  - `public string WeaponHud.PickupPromptNameText { get; }`
  - `public GunArchetype? WeaponHud.PreviewArchetype { get; }`

- [ ] **Step 1: Write the failing test**

Append to `WeaponHudTests`:

```csharp
[UnityTest]
public IEnumerator PickupPromptShowsNameAndAction()
{
    SceneManager.LoadScene("Main");
    yield return null;
    yield return null;

    var deadline = Time.realtimeSinceStartup + 15f;
    PlayerWeapons weapons = null;
    while ((weapons == null || !weapons.IsSpawned) && Time.realtimeSinceStartup < deadline)
    {
        weapons = Object.FindAnyObjectByType<PlayerWeapons>();
        yield return null;
    }

    var hud = weapons.GetComponent<WeaponHud>();
    var movement = weapons.GetComponent<AnimeFpsGame.Movement.CharacterMovement>();
    movement.enabled = false;

    // Pickups sit near the spawn points, so step away first to prove the prompt is proximity-based.
    weapons.transform.position = new Vector3(0f, 1f, 30f);
    yield return null;
    yield return null;
    Assert.IsFalse(hud.PickupPromptVisible, "No prompt when nothing is close");

    var held = weapons.CarriedArchetype((byte)weapons.CurrentSlot);
    var pickup = WeaponPickup.All.First(p => p.IsSpawned && p.Archetype != held);
    weapons.transform.position = pickup.transform.position + new Vector3(0f, 1f, 1f);
    yield return null;
    yield return null;

    Assert.IsTrue(hud.PickupPromptVisible);
    Assert.AreEqual("Press F to switch", hud.PickupPromptActionText);
    Assert.AreEqual(weapons.DefinitionFor(pickup.Archetype).displayName, hud.PickupPromptNameText);
    Assert.AreEqual(pickup.Archetype, hud.PreviewArchetype);

    weapons.transform.position = new Vector3(0f, 1f, 30f);
    yield return null;
    yield return null;
    Assert.IsFalse(hud.PickupPromptVisible, "Walking away hides the prompt");
}
```

Add `using System.Linq;` and `using AnimeFpsGame.Weapons;` to the test file.

- [ ] **Step 2: Run the test and verify it fails**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Filter AnimeFpsGame.Tests.WeaponHudTests -Results 'D:/Making Games/Anime fps game/Temp/prompt.xml' -Log 'D:/Making Games/Anime fps game/Temp/prompt.log'`

Expected: compile error, no `PickupPromptVisible`.

- [ ] **Step 3: Build the prompt and preview rig**

In `WeaponHud` (add `using AnimeFpsGame.Weapons;` if it is missing):

- Add `PlayerPickupInteractor pickupInteractor;` and resolve it lazily at the top of `Update`
  (`if (pickupInteractor == null) pickupInteractor = GetComponent<PlayerPickupInteractor>();`), so
  component order on the prefab cannot leave it null.
- Add the public properties listed above. `PickupPromptVisible` must use
  `pickupPrompt != null && pickupPrompt.activeInHierarchy` (not `activeSelf`, because the canvas root
  is turned off on death).
- In `Build()`, create a bottom-centre panel (`Image` background `new Color(0f, 0f, 0f, 0.45f)`,
  `sizeDelta = new Vector2(280f, 190f)`, `anchoredPosition = new Vector2(0f, 200f)`), a `RawImage`
  child (`sizeDelta = new Vector2(220f, 110f)`, `anchoredPosition = new Vector2(0f, 38f)`,
  `raycastTarget = false`), a name `Text`
  (child of the panel, `sizeDelta = new Vector2(260f, 24f)`, `anchoredPosition = new Vector2(0f, -24f)`,
  `alignment = TextAnchor.MiddleCenter`, font size 18), and an action `Text`
  (child of the panel, `sizeDelta = new Vector2(260f, 24f)`, `anchoredPosition = new Vector2(0f, -52f)`,
  `alignment = TextAnchor.MiddleCenter`, font size 20, text `"Press F to switch"`). Use the existing
  `CreateImage` / `CreateText` helpers; add the `RawImage` with
  `new GameObject("PickupPreview")` + `AddComponent<RawImage>()` and set anchors/pivot to centre.
  Keep the panel as a child of `canvasRoot` with `anchorMin = anchorMax = pivot = (0.5, 0)`.
- Start with the panel inactive.

- In `Update()`, after the `canvasRoot.SetActive(true)` block and before the
  `var gun = weapons.ActiveGun;` early return, add:

```csharp
UpdatePickupPrompt();
```

- Implement:

```csharp
void UpdatePickupPrompt()
{
    var target = pickupInteractor != null ? pickupInteractor.NearestPickup : null;
    var visible = target != null && !weapons.IsDead;
    pickupPrompt.SetActive(visible);

    if (!visible)
    {
        ReleasePreview();
        return;
    }

    var definition = weapons.DefinitionFor(target.Archetype);
    pickupName.text = definition != null ? definition.displayName : target.Archetype.ToString();
    pickupAction.text = "Press F to switch";
    if (previewArchetype != target.Archetype)
    {
        BuildPreview(target.Archetype, definition);
    }
}
```

- `BuildPreview(GunArchetype archetype, WeaponDefinition definition)`:
  - Create the rig once as a child of `transform` at **local** position `(0f, -1000f, 0f)`: the
    player's body moves the rig, but nothing else is in that frustum. Rig rotation identity.
  - Add a `Camera` child at **local** `(0f, 0f, -2.4f)` with rotation identity (it looks down +Z at
    the gun at the rig origin), `orthographic = true`, `orthographicSize = 0.75f`,
    `clearFlags = CameraClearFlags.SolidColor`,
    `backgroundColor = new Color(0f, 0f, 0f, 0f)`, `cullingMask = ~0`,
    `targetTexture = previewTexture`. Create `previewTexture` once as
    `new RenderTexture(512, 256, 16, RenderTextureFormat.ARGB32)`.
  - Add a point-light child of the rig at local `(2f, 3f, -3f)`, `range = 8f`, `intensity = 2.5f`.
  - Destroy the previous preview instance, instantiate `definition.viewmodelPrefab` under the rig at
    local zero, rotate it `Quaternion.Euler(10f, 130f, 0f)`, then normalize scale: compute the
    combined `Renderer.bounds` in the instance's local space and set
    `localScale *= 1.2f / Mathf.Max(size.x, size.y, size.z)` (guard against `size == 0`). Recompute
    bounds afterwards and offset the instance so its centre sits at the rig origin, so the shot is
    framed regardless of the prefab's pivot.
  - Set `previewArchetype = archetype` and assign `previewTexture` to the `RawImage.texture`.
  - `ReleasePreview()` destroys the instance, sets `previewArchetype = null`, clears
    `pickupPreview.texture`, and keeps the RenderTexture allocated.
  - `OnDestroy` destroys the RenderTexture and destroys the rig.

- [ ] **Step 4: Run the test and verify it passes**

Run the Step 2 command again. Expected: `failed=0`.

- [ ] **Step 5: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add Assets/_Project/Scripts/UI/WeaponHud.cs Assets/_Project/Tests/PlayMode/WeaponHudTests.cs
git -C 'D:/Making Games/Anime fps game' commit -m "feat: show the nearby gun and the F prompt on the HUD"
```

---

## Task 8: Full Suite, Design Status and Notes

**Files:**
- Modify: `docs/superpowers/specs/2026-11-19-weapon-pickups-design.md`
- Modify: `docs/superpowers/notes/anime-fps-starter.md`

**Interfaces:**
- Consumes: everything above.
- Produces: no code.

- [ ] **Step 1: Run the full PlayMode suite**

Run:
`powershell -NoProfile -ExecutionPolicy Bypass -File 'C:/Users/baren/AppData/Local/Temp/opencode/run-unity-tests.ps1' -Results 'D:/Making Games/Anime fps game/Temp/all-tests.xml' -Log 'D:/Making Games/Anime fps game/Temp/all-tests.log' -TimeoutMinutes 30`

Expected: exit code `0`, `failed=0`.

- [ ] **Step 2: Mark the design implemented**

Change the spec header to `Status: approved and implemented`.

- [ ] **Step 3: Record the feature in the starter notes**

In `docs/superpowers/notes/anime-fps-starter.md`, add a short section:

```
## Weapon pickups

Four guns lie on the range (Rifle, Handgun, SMG, Shotgun). Walk within 3 m and the HUD shows the
gun's picture, its name and "Press F to switch". F swaps it into the held slot and drops the old gun
at your feet with the exact rounds it had left. Both the ground gun and the drop are networked:
another player cannot take the same gun. Swaps are server-authoritative, so the visible swap lands
one RPC round-trip later.
```

- [ ] **Step 4: Commit**

```bash
git -C 'D:/Making Games/Anime fps game' add docs/superpowers/specs/2026-11-19-weapon-pickups-design.md docs/superpowers/notes/anime-fps-starter.md
git -C 'D:/Making Games/Anime fps game' commit -m "docs: record the weapon pickup system"
```

---

## Self-Review Notes

- **Spec coverage:** mutable networked loadout (Task 2), ammo round trip (Tasks 1, 2, 6), four ground
  guns and registration (Task 4), proximity prompt data (Task 5), server swap/drop/cooldown/cap
  (Task 6), HUD picture + name + action (Task 7), F input (Task 3), docs (Task 8). No spec section
  is without a task.
- **Types:** `FindNearestIndex` uses `PickupCandidate` defined in Task 5 and only used there.
  `WeaponPickup.Initialize` is defined in Task 4 and used in Task 6. `DefinitionFor`/`CarriedArchetype`
  are defined in Task 2 and used in Tasks 5, 6 and 7.
- **Known accepted risk, not a task:** the server trusts the owner-reported outgoing ammo (consistent
  with owner-authoritative ammo today); the drop only exists to carry those numbers, so a modified
  client can inflate its own dropped gun's rounds, never another player's.
- **Not tested, verified by playtest:** ground-gun tilt/scale and the preview camera framing. The
  tests assert a mesh exists and the prompt switches; they do not judge the look.
