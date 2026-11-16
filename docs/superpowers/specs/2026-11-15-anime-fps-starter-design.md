# Anime FPS Game — Starter Design

Date: 2026-11-15
Status: awaiting approval

## Intent

Turn the current Unity project (`D:\Making Games\My project`) into the start of **Anime fps game**: a
multiplayer-ready first-person shooter skeleton, using the WW2 German soldier pack as placeholder art
until anime-style characters exist.

Why: the project should have one clear structure and a playable, networked base to build on, rather than
a default URP scene and a loose asset pack.

## Goals

- Rename the project to `Anime fps game` (folder, solution, product name).
- One predictable folder layout under `Assets/_Project`.
- First-person controller: walk, sprint, jump, gravity, mouse look, CharacterController-based.
- Two weapon slots per character: **K98 rifle** (main) and **Luger P08** (pistol). Swap with `1` / `2`
  or mouse wheel. Only the active weapon is shown.
- Shoot input prints `shoot` (no aiming, no shooting, no damage).
- Multiplayer: **Netcode for GameObjects 2.14.0 + Unity Transport**. Host / join by IP, each connected
  player spawns as a soldier, movement and weapon choice replicate.
- Art stays swappable: player visuals are a child of the player prefab, not baked into scripts.

## Non-goals

- Aiming, shooting, damage, health, respawn.
- Matchmaking, Relay, accounts, dedicated servers (Unity Services Sessions stay installed for later).
- Anime art, animation polish, audio, HUD beyond the host/join panel.

## Constraints

- Unity 6.6.9f1, URP, Input System only (legacy input is not used).
- The Unity Editor currently has this project open with unsaved changes; it must be closed before the
  folder rename.
- No git repository — there is no easy undo, so moves must be GUID-preserving (always move `.meta`
  files with their assets).
- Installed `com.unity.transport` is `6.6.0` (pulled in by Multiplayer Services), while NGO 2.14.0
  declares `2.6.0`. This must be verified: if compile or connection fails, pin transport to 2.6.0 or
  fall back to the newest NGO 2.x compatible build. NGO 3.x is not eligible (needs Unity 6.7).

## Approaches

1. **Single player prefab + observable weapon meshes (chosen).** The soldier model already contains all
   eight guns as skinned children, with animator ints (`Walk_status`, `K98_status`, `LugerP08_status`).
   Toggle those meshes and drive the animator; attach the standalone gun prefabs only as the local
   first-person viewmodel. Least new art, matches how the pack was authored.
2. **Separate body and weapon prefabs glued to hand bones.** More flexible later, but needs socket setup,
   per-weapon poses, and fights the existing animations. Rejected for a starter.
3. **Third-person-only camera.** Simplest networking, but it is not an FPS. Rejected.

## Design

### Project layout

```
D:\Making Games\Anime fps game\
  Assets\
    _Project\
      Art\WW2_German_Soldiers\      (moved from Assets\WW2_German_soilders, typo fixed)
      Blocks\                        (existing Unity Multiplayer Blocks sample, moved)
      Prefabs\Player\                Player.prefab
      Prefabs\Weapons\               K98.prefab, LugerP08.prefab (viewmodels)
      Scenes\Main.unity              (replaces SampleScene)
      Scripts\Player\                PlayerMotor, PlayerLook, PlayerWeapons
      Scripts\Networking\            NetworkBootstrap, HostJoinMenu
      Settings\                      (moved from Assets\Settings)
      ThirdParty\TutorialInfo\       (moved URP readme)
    Readme.asset
  docs\superpowers\specs\
```

All moves keep `.meta` files, so existing references (materials, controller, scene) survive.

### Player

Root `Player.prefab` (NetworkObject):
- `CharacterController` (height ~1.8, radius 0.3)
- `PlayerMotor` — owner-authority movement from Input System (`Wasd`, sprint, jump); gravity; no
  root motion.
- `PlayerLook` — yaw on the body, pitch on the camera, clamped, cursor locked while playing.
- `PlayerWeapons` — slot 0 = K98, slot 1 = Luger; `NetworkVariable<byte> ActiveSlot` (owner write) so
  other clients see the right gun; `1`/`2`/scroll to swap; fire button logs `shoot` locally and sends a
  `ServerRpc` that logs `shoot from client N` (proves the network path without gameplay).
- Camera at eye height with a `ViewModel` anchor holding the active gun prefab, tuned in scale/position.
- `Visuals` child = Wehrmacht_A soldier prefab instance. For the local owner its renderers are hidden
  (so the camera is not inside the model); remote copies show the full body and only the active weapon.
- Remote copies use `NetworkTransform` (position + yaw, interpolation on) and an `Animator` driven by
  `Walk_status` from replicated speed.

### Networking

- `Main.unity` contains `NetworkManager`, `UnityTransport` (IP + port 7777), and `HostJoinMenu`
  (uGUI: IP field, Host, Join, status text).
- `NetworkBootstrap` registers the player prefab and starts host/client/join from menu input.
- LAN/IP play only; Relay/Sessions are a later step.

### Naming

- Folder `Anime fps game`, solution `Anime fps game.sln`, Product Name `Anime fps game`.
- Namespace `AnimeFpsGame`, scripts under `AnimeFpsGame.Player` / `AnimeFpsGame.Networking`.

## My calls (correctable)

- Weapon 1 = K98, weapon 2 = Luger P08 (matches "main weapon + pistol").
- Local player's own body hidden; full soldier model visible to other players.
- `SampleScene` becomes `Main.unity`; the URP `TutorialInfo` readme is moved, not deleted.
- Existing `Blocks` sample is kept and moved, not deleted.
- Single test scene with a simple flat ground plane, so the controller is testable immediately.

## What is left to the builder

- Exact viewmodel offsets/scale per gun (the gun prefabs are authored at ~12x scale).
- Whether to hide the head only or the whole body for the local owner, once it is seen in play.
- Ground/environment layout beyond a flat test floor.
