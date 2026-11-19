# Anime fps game — starter notes

## Run it

1. Open the project (`D:\Making Games\Anime fps game`) in Unity **6000.6.9f1**.
2. Open `Assets/_Project/Scenes/MainMenu.unity` (build index 0; the game starts here).
3. Press Play. The main menu appears: **Campaign**, **Multiplayer**, **Range**, **Settings**, **Exit**.
4. Click **Range** to enter the empty test field as a single player. Campaign and Multiplayer are
   greyed out; Settings is a placeholder; Exit quits (or stops Play Mode in the editor).

Two clients without the editor: build the Windows player once from the editor (File > Build Settings,
or Build and Run) into `Builds/Windows`. The copy in that folder may predate the current player
prefab, so rebuild it before testing online. `-host` / `-join` skip the menu and start the requested
session directly:

```powershell
# host
"Builds\Windows\Anime fps game.exe" -host
# client
"Builds\Windows\Anime fps game.exe" -join 127.0.0.1
```

The port is `7777` (Unity Transport). The host binds to `0.0.0.0`, so a second machine can join
with the host's LAN address (`ipconfig` → IPv4 Address) instead of `127.0.0.1`. If the connection
times out, allow the game through the Windows firewall on the host.

## Controls

| Input | Action |
|---|---|
| WASD | move |
| Mouse | look |
| Shift | sprint |
| Space | jump |
| C | crouch (hold) |
| 1 / 2, or mouse wheel | cycle all four weapons (Rifle → Handgun → SMG → Shotgun) |
| Left mouse | fire (Rifle: one shot per press; hold for the SMG) |
| R | reload |

Each client spawns on its own marker (`SpawnPoint 1`–`4` in `Main.unity`). You see your own jacket,
arms and legs, but not your head, hands or a body-carried weapon: the held gun is the viewmodel.
Other players see your full soldier plus the weapon for your active slot.

## Where things live

```
Assets/_Project/
  Art/WW2_German_Soldiers/     placeholder soldier, guns, demo scene
  Prefabs/Player/Player.prefab the networked player (Rigidbody capsule, camera, weapons)
  Prefabs/Weapons/             K98 / Luger first-person viewmodels
  Scenes/MainMenu.unity        startup menu (Campaign, Multiplayer, Range, Settings, Exit)
  Scenes/Main.unity            ground, light, NetworkManager, solo auto-host
  Scripts/Menu/                MainMenuController
  Scripts/Input/               InputReader, PlayerInputBindings (rebinding + persistence)
  Scripts/Movement/            CharacterMovement, MovementIntent, MovementProfile
  Scripts/Player/              PlayerMotor, PlayerLook, PlayerWeapons, WeaponDefinition
  Scripts/Weapons/             IGun/Gun, Rifle, Handgun, SMG, Shotgun, GunFactory
  Scripts/Combat/              Health, HealthState, PlayerRespawn, TargetDummy
  Scripts/UI/, Scripts/Effects/ WeaponHud, ShotEffects
  Settings/Movement/           DefaultMovementProfile (all movement tuning)
  Settings/Weapons/            Rifle / Handgun / SMG / Shotgun GunStatsAssets
  Scripts/Networking/          NetworkBootstrap, HostJoinMenu
  Editor/ProjectSetup.cs       one-shot generator (Anime FPS menu)
  Tests/PlayMode/              headless tests: movement, input, guns, health, player, HUD, combat
```

`Anime FPS > Run All` rebuilds the scene, NetworkManager and player prefab from scratch
(idempotent; it is how everything was generated).

## Movement

The body is a real dynamic `Rigidbody` capsule (`Player` layer, zero-friction physics material), so
ragdolls are possible later. `CharacterMovement` (`Scripts/Movement/CharacterMovement.cs`) owns the
Source-style model: friction, ground acceleration, air acceleration with air-strafing, jump, gravity,
crouch, step-up, slope sliding and external impulses. It never reads input, networking or the camera,
so one implementation serves players and future AI.

The contract is `MovementIntent`: set `WishDirection` (world space, magnitude ≤ 1), `Sprint`, `Crouch`
and `Jump`, then the core integrates in `FixedUpdate`. `PlayerMotor` is the only owner/network glue
(it fills the intent from input, replicates crouch, pauses movement while dead) and a new AI brain
would fill the same intent instead. `PlayerLook` turns the body with `Rigidbody.MoveRotation`.

Tuning lives in `Assets/_Project/Settings/Movement/DefaultMovementProfile.asset`: walk/sprint/crouch
speeds, ground and air acceleration, friction, stop speed, jump height, gravity, step height, slope
angle, capsule and eye heights, mass. **`Run All` never overwrites an existing profile**, so Inspector
edits survive regeneration.

## Input

`InputReader` (`Scripts/Input/InputReader.cs`) is the only gameplay code that touches the Input System;
it caches the shared `Player` action map and exposes plain properties (`Move`, `Look`, `SprintHeld`,
`JumpPressed`, ...). Movement, look and weapons never query actions themselves.

`PlayerInputBindings` owns rebinding: apply a new control, clear the old owner of that control
(including a default binding, which is unbound explicitly), reset one binding or everything, and save.
Overrides persist as JSON under the `PlayerPrefs` key `animefps.input.bindings.v1`, so they survive
restarts. Defaults come from `Assets/InputSystem_Actions.inputactions`. There is no Settings screen
yet; a UI would call `PlayerInputBindings` directly.

## Weapons

Four slots ship today: Rifle (K98), Handgun (Luger), SMG (MP40) and Shotgun (K98 placeholder).
Each slot has a `GunStatsAsset` at `Settings/Weapons/`; tune cadence, damage, pellets, magazine,
reserve, reload, range, bloom and pellet spread there. **`Run All` never overwrites an existing
asset**, so Inspector edits survive regeneration. Delete an asset if you want the defaults back.

The Rifle is a 5-round bolt action: one shot, a 1 s bolt cycle, then the normal 2.2 s reload after
the fifth round or on R. Tracers and the muzzle flash start at the held gun's muzzle; the camera
ray still decides aim and damage.

Every slot's `viewmodelScale` is `0.5`; the original `0.08` made each gun about 9 cm long on
screen. Position, rotation and scale are per-slot placeholder tuning values, safe to edit in the
Inspector.

The gun rules live in plain C# (`IGun` / `Gun`), so they are unit-tested without a scene. Each
`PlayerWeapons` builds one gun instance per slot and keeps it for the player's lifetime, so ammo
and reloads survive weapon switches. Ammo is owner-authoritative; **damage is server-authoritative**
— the client reports origin and directions and the server re-raycasts, checking pellet count,
origin distance, a 60° aim cone and cadence before touching health.

A weapon slot is four things:

- `soldierMeshName` — which child mesh to show on other players (`K98`, `Luger_P08`, `MP40`)
- `animatorInt` — which animator int to set to `1`
- `viewmodelPrefab` — what the owner holds in first person
- viewmodel position / rotation / scale (tune these; they are placeholder values)
- `stats` — the `GunStatsAsset` that drives that slot
- muzzle is derived from the viewmodel (a child named Muzzle overrides it), so replacement
  viewmodels work without code changes

Add a slot in `Player.prefab` → `PlayerWeapons` → `slots` and extend the mesh-name list in
`PlayerWeapons.cs` if you add a weapon outside the pack.

## Replacing the placeholder art

`Visuals` under `Player.prefab` is a plain prefab instance. Swap it for an anime character
prefab; keep the animator parameters named the same (`Walk_status`, `K98_status`,
`LugerP08_status`, ...) or update `WeaponDefinition.animatorInt`. The scripts have no
dependency on the WW2 pack beyond those names and mesh names. The first-person viewmodels
are separate prefabs, so they can be replaced independently.

## Weapon pickups

Four guns lie on the range (Rifle, Handgun, SMG, Shotgun). Walk within 3 m and the HUD shows the
gun's picture, its name and "Press F to switch". F swaps it into the held slot and drops the old gun
at your feet with the exact rounds it had left. Both the ground gun and the drop are networked:
another player cannot take the same gun. Swaps are server-authoritative, so the visible swap lands
one RPC round-trip later.

## Known gaps (on purpose)

- Movement is owner-authoritative with no client prediction or reconciliation: the owner simulates and
  `NetworkTransform` replicates, so a high-ping client sees its own motion immediately but remote
  bodies interpolate. Crouch replication is not lag-compensated either.
- No slide, mantle or auto-bhop. Jump must be re-pressed; there is no jump buffering beyond the
  per-tick intent.
- No Settings screen; movement sensitivity and crouch/jump/sprint keys are rebindable through
  `PlayerInputBindings` but only from code. Rebind UI and sensitivity/invert options are missing.
- Ragdolls are not implemented yet; the dynamic body is the groundwork.
- No ADS, headshots, damage falloff or wall penetration. No lag compensation.
- No shooting, impact or reload audio yet.
- Ammo is owner-authoritative and not replicated, so another peer cannot see your magazine.
- Main menu Campaign/Multiplayer/Settings are placeholders. Range runs a solo local host, not a true
  offline mode. There is no pause or return-to-menu yet.
- No anime art yet; the WW2 pack is a placeholder.
- LAN/IP only. Relay, lobbies and Accounts (Unity Multiplayer Services) are installed but unused.
- Viewmodel placement is eyeballed and needs a tuning pass in the editor.
- The server-side shot validation is unit-covered only indirectly (the combat test drives `Health`); the two-client visual path and RPC-level validation still need a manual host + client session.
- No CI.
