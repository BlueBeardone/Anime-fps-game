# Anime FPS Game — Starter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `D:\Making Games\My project` into `Anime fps game`, with a tidy asset layout, a basic networked first-person controller using the WW2 German soldier pack as placeholder art, and a main weapon plus a pistol.

**Architecture:** One `NetworkObject` player prefab with a `CharacterController` (owner-authority movement), a camera + viewmodel anchor, and the soldier prefab as a swappable `Visuals` child. Weapon choice is a `NetworkVariable<byte>`; the active gun is shown by toggling the soldier's weapon meshes and driving the existing animator ints. NGO + Unity Transport provide host/join over IP.

**Tech Stack:** Unity 6.6.9f1, URP, Input System (`InputSystem_Actions`), Netcode for GameObjects 2.14.0, Unity Transport, uGUI (runtime-built menu).

**Spec:** `docs/superpowers/specs/2026-11-15-anime-fps-starter-design.md`

## Global Constraints

- Unity version `6000.6.9f1`; editor at `E:\Unity\Editors\6000.6.9f1\Editor\Unity.exe`.
- Input System only; no legacy `Input` class.
- `com.unity.netcode.gameobjects` `2.14.0` (not 3.x, which needs Unity 6.7).
- Never move an asset without its `.meta`; GUIDs must survive.
- Namespaces `AnimeFpsGame.Player`, `AnimeFpsGame.Networking`, `AnimeFpsGame.EditorTools`.
- No git repository exists: verification checkpoints replace commits, and no step may be destructive without a check.
- Editor must be closed for file-level renames; batchmode is used for compile checks.

## Review Focus

- Unity package resolution with `com.unity.transport` 6.6.0 already installed while NGO 2.14 wants 2.6.0 — must compile and host without transport errors.
- Moving `Assets/Settings` and scenes must not break URP asset references held in `ProjectSettings/GraphicsSettings.asset` (GUID-based, but verify no missing-script/missing-asset log lines).
- The soldier prefab's nested animator/renderers must still resolve inside the player prefab after the folder move and typo rename.
- Opening the game with no host must not leave the player frozen or the cursor locked; menu-first, cursor visible.
- A second client must see the first client's active weapon mesh, not all eight guns.

---

### Task 1: Rename the project

**Files:**
- Move: `D:\Making Games\My project` → `D:\Making Games\Anime fps game`
- Delete: `My project.sln`, `*.csproj`, `.vsconfig` (Unity regenerates them)
- Modify: `ProjectSettings/ProjectSettings.asset` (`productName`, `companyName`)

**Interfaces:**
- Produces: project root `D:\Making Games\Anime fps game`; all later paths are relative to it.

- [ ] **Step 1: Confirm the editor is closed**

Run: `Get-Process Unity -ErrorAction SilentlyContinue`
Expected: no `Unity` process (Unity Hub may run).

- [ ] **Step 2: Move the folder and drop generated solution files**

PowerShell: `Move-Item "D:\Making Games\My project" "D:\Making Games\Anime fps game"`, then delete `Anime fps game.sln`, `Assembly-CSharp*.csproj`, `Blocks*.csproj`, `.vsconfig`.

- [ ] **Step 3: Set the product name**

In `ProjectSettings/ProjectSettings.asset`, set `productName: Anime fps game` and `companyName: AnimeFps` (or the user's studio name if they give one).

- [ ] **Step 4: Verify**

Run: `Test-Path "D:\Making Games\Anime fps game\ProjectSettings\ProjectVersion.txt"` and `Select-String -Path "...\ProjectSettings.asset" -Pattern "productName"`.
Expected: `True` and `productName: Anime fps game`.

---

### Task 2: Restructure Assets

**Files:**
- Create: `Assets/_Project/{Art,Prefabs/Player,Prefabs/Weapons,Scenes,Scripts/Player,Scripts/Networking,Editor}`, `Assets/_Project/ThirdParty`
- Move: `Assets/WW2_German_soilders` → `Assets/_Project/Art/WW2_German_Soldiers`; `Assets/Scenes` → `Assets/_Project/Scenes`; `Assets/Settings` → `Assets/_Project/Settings`; `Assets/Blocks` → `Assets/_Project/Blocks`; `Assets/TutorialInfo` → `Assets/_Project/ThirdParty/TutorialInfo`

**Interfaces:**
- Produces: `Assets/_Project` as the single home for project-owned and sample content.

- [ ] **Step 1: Create the folder tree** (each new folder gets a `.meta` on next Unity open; moving existing folders keeps theirs).

- [ ] **Step 2: Move each folder together with its `.meta` file** using `Move-Item -LiteralPath` on both the folder and `<folder>.meta`.

- [ ] **Step 3: Rename `WW2_German_soilders` → `WW2_German_Soldiers`** (folder and `.meta`), fixing the typo.

- [ ] **Step 4: Verify no orphan `.meta` or duplicate folder names**

Run a PowerShell check that every directory/file in `Assets` has a sibling `.meta` and that `WW2_German_soilders` no longer exists.
Expected: zero missing `.meta`, zero leftovers.

---

### Task 3: Editor setup tool + main scene

**Files:**
- Create: `Assets/_Project/Editor/ProjectSetup.cs`
- Create: `Assets/_Project/Scenes/Main.unity`
- Delete: `Assets/_Project/Scenes/SampleScene.unity` (after saving Main)

**Interfaces:**
- Produces: `public static class AnimeFpsGame.EditorTools.ProjectSetup` with
  `public static void SetupScene()`, `SetupNetworkManager()`, `BuildPlayerPrefab()`, `RunAll()`, each callable via `-executeMethod`.
- Produces: `Main.unity` with `Main Camera`, `Directional Light`, `Global Volume`, `Ground`.

- [ ] **Step 1: Write `ProjectSetup.SetupScene()`** — open `SampleScene`, add a `Ground` GameObject with `PrimitiveType.Plane`, scale `(2,1,2)` at origin, a `Ground` material or default-lit colour; save as `Assets/_Project/Scenes/Main.unity`; delete `SampleScene`.
- [ ] **Step 2: Run it in batchmode**

Run: `Unity.exe -batchmode -quit -projectPath "D:\Making Games\Anime fps game" -executeMethod AnimeFpsGame.EditorTools.ProjectSetup.SetupScene -logFile "C:\Users\baren\AppData\Local\Temp\opencode\setup-scene.log"`
Expected: `Main.unity` exists, no `error CS` and no `Aborting` in the log.

- [ ] **Step 3: Verify**

Open the log and search for `Exception`, `error CS`, `NullReference`.
Expected: none.

---

### Task 4: Install Netcode and add the NetworkManager

**Files:**
- Modify: `Packages/manifest.json` (add `"com.unity.netcode.gameobjects": "2.14.0"`)
- Modify: `Assets/_Project/Scenes/Main.unity` via `ProjectSetup.SetupNetworkManager()`

**Interfaces:**
- Produces: `NetworkManager` GameObject in `Main.unity` with `UnityTransport` (address `127.0.0.1`, port `7777`), `NetworkConfig.PlayerPrefab` wired in Task 6.

- [ ] **Step 1: Add the dependency** and run Unity in batchmode so it resolves and compiles.

- [ ] **Step 2: Record the resolved transport version** from `Packages/packages-lock.json` (`com.unity.transport`).
Expected: a version that NGO 2.14.0 accepts; if transport stays at 6.6.0 and `error CS` appears, pin `"com.unity.transport": "2.6.0"` and re-run.

- [ ] **Step 3: Write `ProjectSetup.SetupNetworkManager()`** — creates the object, adds `UnityTransport`, sets `ConnectionData` to `127.0.0.1:7777`, adds `NetworkManager`, saves the scene.
- [ ] **Step 4: Run in batchmode and check the log for `error CS`.**

---

### Task 5: Player scripts

**Files:**
- Create: `Assets/_Project/Scripts/Player/PlayerMotor.cs`
- Create: `Assets/_Project/Scripts/Player/PlayerLook.cs`
- Create: `Assets/_Project/Scripts/Player/PlayerWeapons.cs`
- Create: `Assets/_Project/Scripts/Player/WeaponDefinition.cs`

**Interfaces:**
- Produces: `PlayerMotor.Speed` (`float`, meters/second for the animator); `PlayerLook.Sensitivity` (`float`, default `2`); `PlayerWeapons.ActiveSlot` (`NetworkVariable<byte>`, `WritePermission.Owner`), `PlayerWeapons.SetSlot(byte)`.
- Consumes: `InputSystem_Actions` map `Player` with actions `Move` (Vector2), `Look` (Vector2), `Jump`, `Sprint`, `Attack`, `Previous`, `Next`.

- [ ] **Step 1: `WeaponDefinition`** — `[System.Serializable] class WeaponDefinition { public string displayName; public string animatorInt = "K98_status"; public string soldierMeshName = "K98"; public GameObject viewmodelPrefab; public Vector3 viewmodelLocalPosition; public Vector3 viewmodelLocalEuler; public float viewmodelScale = 1f; }`
- [ ] **Step 2: `PlayerMotor`** — `[RequireComponent(typeof(CharacterController))]`, `NetworkBehaviour`; fields `walkSpeed = 5f`, `sprintSpeed = 8f`, `jumpHeight = 1.2f`, `gravity = -20f`; in `OnNetworkSpawn` disable itself when `!IsOwner`; `Update` reads `Move`/`Sprint`/`Jump`, applies `CharacterController.Move` with gravity, sets `Speed`.
- [ ] **Step 3: `PlayerLook`** — `NetworkBehaviour`; yaw on `transform`, pitch on the assigned `cameraPivot`; cursor lock only when owning and playing; `OnNetworkSpawn` destroys the assigned camera and its AudioListener when `!IsOwner`.
- [ ] **Step 4: `PlayerWeapons`** — `NetworkBehaviour`; `WeaponDefinition[] slots` (index 0 K98, 1 Luger); `ActiveSlot` with `WritePermission.Owner`; `OnValueChanged` → `ApplySlot`; `Update` reads `1`/`2`-equivalent `Previous`/`Next` and `Attack`; on Attack: `Debug.Log("shoot")` then `ShootServerRpc()`; `[ServerRpc]` logs `shoot from client {OwnerClientId}`; `ApplySlot` toggles viewmodel, sets the animator int, and enables only the active weapon mesh on other players (owner hides the whole `Visuals` child).
- [ ] **Step 5: Compile check in batchmode.** Expected: no `error CS`.

---

### Task 6: Player prefab, viewmodels, and spawn wiring

**Files:**
- Create: `Assets/_Project/Prefabs/Weapons/K98_Viewmodel.prefab`, `LugerP08_Viewmodel.prefab`
- Create: `Assets/_Project/Prefabs/Player/Player.prefab`
- Modify: `Main.unity` (NetworkManager `PlayerPrefab`)

**Interfaces:**
- Consumes: `WeaponDefinition` fields from Task 5.
- Produces: `Player.prefab` with `NetworkObject`, `CharacterController`, `NetworkTransform`, `NetworkAnimator` (owner authority), `PlayerInput` (actions asset, map `Player`), `PlayerMotor`, `PlayerLook`, `PlayerWeapons`, child `Camera`+`ViewModel` anchor, child `Visuals` = `Wehrmacht_A_prefab`.

- [ ] **Step 1: Write `ProjectSetup.BuildPlayerPrefab()`** — copies the gun prefabs into `Prefabs/Weapons` as viewmodels (root scale normalised to ~0.08), builds the player hierarchy, wires every serialized field, saves the prefab, assigns it to `NetworkManager.NetworkConfig.PlayerPrefab`.
- [ ] **Step 2: Run in batchmode**, then read the log.
Expected: prefab and both viewmodels exist; no exceptions; `PlayerPrefab` non-null.

- [ ] **Step 3: Check weapon meshes exist** on the soldier prefab: `K98`, `Luger_P08` renderers found by `RaycastAll`-free lookup (`GetComponentsInChildren<Renderer>(true)` filtered by `gameObject.name`).

---

### Task 7: Host/join menu and bootstrap

**Files:**
- Create: `Assets/_Project/Scripts/Networking/NetworkBootstrap.cs`
- Create: `Assets/_Project/Scripts/Networking/HostJoinMenu.cs`
- Modify: `Main.unity` via `ProjectSetup.SetupNetworkManager()`

**Interfaces:**
- Produces: `NetworkBootstrap.Host()`, `Join(string address)`; `HostJoinMenu` builds a canvas at runtime with IP text field, Host, Join, and a status label; hides itself once connected.

- [ ] **Step 1: `NetworkBootstrap`** — serialized `NetworkManager`; `Host()` calls `StartHost`; `Join(address)` sets `UnityTransport.ConnectionData.Address = address` then `StartClient`; exposes `IsConnected` and a `Status` string.
- [ ] **Step 2: `HostJoinMenu`** — `OnGUI`-free runtime uGUI (Canvas + `LegacyRuntime.ttf` font), default address `127.0.0.1`, calls bootstrap; cursor visible until connected.
- [ ] **Step 3: Attach both in `SetupNetworkManager()`**, rebuild the scene, batchmode compile.
- [ ] **Step 4: Play-mode smoke test** — enter play mode via the editor and confirm host starts, one player spawns at the ground, WASD/mouse/jump/sprint work, `1`/`2` swaps the viewmodel, clicking logs `shoot`, and the host log shows `shoot from client 0`.

---

### Task 8: Two-client verification and cleanup

**Files:**
- Modify: whatever the smoke test exposes.
- Create: `docs/superpowers/notes/anime-fps-starter.md` (what works, what is placeholder, how to host/join).

- [ ] **Step 1: Make a Windows build** (`-buildWindows64Player` via batchmode or editor menu) into `Builds/Windows/Anime fps game.exe`.
- [ ] **Step 2: Run two instances** (build + editor) with the editor as host at `127.0.0.1:7777`; confirm the second client sees the host's soldier, the correct active weapon mesh only, and movement replication.
- [ ] **Step 3: Write the notes file** with the host/join steps and the known gaps (no aiming, no shooting, no anime art yet).
