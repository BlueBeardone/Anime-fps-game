# Anime fps game — starter notes

## Run it

1. Open the project (`D:\Making Games\Anime fps game`) in Unity **6000.6.9f1**.
2. Open `Assets/_Project/Scenes/MainMenu.unity` (build index 0; the game starts here).
3. Press Play. The main menu appears: **Campaign**, **Multiplayer**, **Range**, **Settings**, **Exit**.
4. Click **Range** to enter the empty test field as a single player. Campaign and Multiplayer are
   greyed out; Settings is a placeholder; Exit quits (or stops Play Mode in the editor).

Two clients without the editor (a build already exists in `Builds/Windows`). `-host` / `-join` skip the
menu and start the requested session directly:

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
| 1–4, or mouse wheel | Rifle / Handgun / SMG / Shotgun |
| Left mouse | fire (hold for the full-auto Rifle and SMG) |
| R | reload |

Each client spawns on its own marker (`SpawnPoint 1`–`4` in `Main.unity`); you see other players'
soldiers, never your own body.

## Where things live

```
Assets/_Project/
  Art/WW2_German_Soldiers/     placeholder soldier, guns, demo scene
  Prefabs/Player/Player.prefab the networked player (CharacterController, camera, weapons)
  Prefabs/Weapons/             K98 / Luger first-person viewmodels
  Scenes/MainMenu.unity        startup menu (Campaign, Multiplayer, Range, Settings, Exit)
  Scenes/Main.unity            ground, light, NetworkManager, solo auto-host
  Scripts/Menu/                MainMenuController
  Scripts/Player/              PlayerMotor, PlayerLook, PlayerWeapons, WeaponDefinition
  Scripts/Weapons/             IGun/Gun, Rifle, Handgun, SMG, Shotgun, GunFactory
  Scripts/Combat/              Health, HealthState, PlayerRespawn, TargetDummy
  Scripts/UI/, Scripts/Effects/ WeaponHud, ShotEffects
  Settings/Weapons/            Rifle / Handgun / SMG / Shotgun GunStatsAssets
  Scripts/Networking/          NetworkBootstrap, HostJoinMenu
  Editor/ProjectSetup.cs       one-shot generator (Anime FPS menu)
  Tests/PlayMode/              headless tests: guns, health, player, HUD, combat
```

`Anime FPS > Run All` rebuilds the scene, NetworkManager and player prefab from scratch
(idempotent; it is how everything was generated).

## Weapons

Four slots ship today: Rifle (K98), Handgun (Luger), SMG (MP40) and Shotgun (K98 placeholder).
Each slot has a `GunStatsAsset` at `Settings/Weapons/`; tune cadence, damage, pellets, magazine,
reserve, reload, range, bloom and pellet spread there. **`Run All` never overwrites an existing
asset**, so Inspector edits survive regeneration. Delete an asset if you want the defaults back.

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

Add a slot in `Player.prefab` → `PlayerWeapons` → `slots` and extend the mesh-name list in
`PlayerWeapons.cs` if you add a weapon outside the pack.

## Replacing the placeholder art

`Visuals` under `Player.prefab` is a plain prefab instance. Swap it for an anime character
prefab; keep the animator parameters named the same (`Walk_status`, `K98_status`,
`LugerP08_status`, ...) or update `WeaponDefinition.animatorInt`. The scripts have no
dependency on the WW2 pack beyond those names and mesh names. The first-person viewmodels
are separate prefabs, so they can be replaced independently.

## Known gaps (on purpose)

- No ADS, headshots, damage falloff or wall penetration. No lag compensation.
- No shooting, impact or reload audio yet.
- Ammo is owner-authoritative and not replicated, so another peer cannot see your magazine.
- Main menu Campaign/Multiplayer/Settings are placeholders. Range runs a solo local host, not a true
  offline mode. There is no pause or return-to-menu yet.
- No anime art yet; the WW2 pack is a placeholder.
- LAN/IP only. Relay, lobbies and Accounts (Unity Multiplayer Services) are installed but unused.
- Viewmodel placement is eyeballed and needs a tuning pass in the editor.
- The shot server RPC is exercised by the headless combat test through `Health`, but the two-client
  visual path (remote tracer/impact) still needs a manual host + client session.
- No CI.
