# Main Menu — Design

Date: 2026-11-16
Status: awaiting approval

## Intent

Give the game a proper entry point: a main menu with **Campaign**, **Multiplayer**, **Range**, **Settings**
and **Exit**. Campaign and Multiplayer are visible but disabled until those modes exist. Settings is a
clickable placeholder. Range drops the player into the current empty test scene.

## Goals

- New `Assets/_Project/Scenes/MainMenu.unity` as the startup scene (build index 0).
- Buttons: Campaign (disabled), Multiplayer (disabled), Range, Settings (no-op), Exit.
- Range loads `Main.unity` and starts a playable single-player session immediately.
- Keep the current world/player unchanged.

## Non-goals

- Campaign, multiplayer menu flow, settings screen, pause menu, "back to menu" from Range.
- New art, music, animations, or a custom font.
- Changing the player controller or weapon behaviour.

## Constraints

- Unity 6000.6.9f1, URP, uGUI + Input System only, no git (no easy undo).
- `ProjectSetup.cs` regenerates the scene/NetworkManager and must stay consistent.
- `PlayerMotor`, `PlayerLook`, `PlayerWeapons` are `NetworkBehaviour`s that only initialise in
  `OnNetworkSpawn` and depend on `NetworkVariable`/RPCs.

## Approaches

1. **Solo local host (chosen).** Range loads `Main.unity`; `NetworkBootstrap` starts a local host
   automatically, so the existing player prefab and scripts work untouched. No connection UI. Dev
   `-host` / `-join` flags still win when present. Multiplayer can reuse this layer later.
2. **Offline player refactor.** Strip networking from Range. Needs rewriting three `NetworkBehaviour`
   scripts, guarding `NetworkVariable`/RPC use, and an offline spawn path — high risk for little visible
   gain now, and likely to be undone when multiplayer returns.
3. **Keep the Host / Join panel.** One click more than the user asked for and exposes dead networking UI.

## Design

- `MainMenu.unity` contains a `MainMenuController` that builds a full-screen uGUI overlay at runtime
  (same pattern as `HostJoinMenu`): dark backdrop, title **ANIME FPS**, five buttons stacked vertically.
  Campaign/Multiplayer are `interactable = false` and dimmed; Settings logs one line; Exit calls
  `Application.Quit()` (stops Play Mode in the editor); Range calls `SceneManager.LoadScene("Main")`.
  Cursor is visible/unlocked; Range is the default selected button for keyboard/gamepad.
- Build Settings: `MainMenu` at index 0, `Main` at index 1.
- `Main.unity`: remove the `HostJoinMenu` component; add `startSinglePlayer` to `NetworkBootstrap`
  (true in the scene), which calls `Host()` after command-line handling when no session started.
- `ProjectSetup.cs`: register `MainMenu.unity` in Build Settings and stop adding `HostJoinMenu`.
- Keep `HostJoinMenu.cs` in the project for the future multiplayer flow; it is simply not attached.

## My calls (correctable)

- Title `ANIME FPS`, minimal dark palette matching the existing panel; no logo/art yet.
- Settings stays clickable (not grayed) because it is a placeholder, not a missing feature.
- Disabled modes are only dimmed — no "coming soon" labels or tooltips.
- No return-to-menu key from Range yet; it is a separate feature.

## What is left to the builder

- Exact palette, font sizes, spacing and button sizing.
- Whether Range should later get a pause/Esc menu.
