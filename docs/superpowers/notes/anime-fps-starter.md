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
| 1 / 2, or mouse wheel | K98 rifle / Luger P08 pistol |
| Left mouse | prints `shoot` (local) and `shoot from client N` (server log) |

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
  Scripts/Networking/          NetworkBootstrap, HostJoinMenu
  Editor/ProjectSetup.cs       one-shot generator (Anime FPS menu)
  Tests/PlayMode/              headless host + weapon-swap smoke test
```

`Anime FPS > Run All` rebuilds the scene, NetworkManager and player prefab from scratch
(idempotent; it is how everything was generated).

## Weapons

The soldier prefab already carries all eight guns as skinned meshes with animator ints
(`K98_status`, `LugerP08_status`, ...). A weapon slot is four things:

- `soldierMeshName` — which child mesh to show on other players (`K98`, `Luger_P08`)
- `animatorInt` — which animator int to set to `1`
- `viewmodelPrefab` — what the owner holds in first person
- viewmodel position / rotation / scale (tune these; they are placeholder values)

Add a slot in `Player.prefab` → `PlayerWeapons` → `slots` and extend the mesh-name list in
`PlayerWeapons.cs` if you add a weapon outside the pack.

## Replacing the placeholder art

`Visuals` under `Player.prefab` is a plain prefab instance. Swap it for an anime character
prefab; keep the animator parameters named the same (`Walk_status`, `K98_status`,
`LugerP08_status`, ...) or update `WeaponDefinition.animatorInt`. The scripts have no
dependency on the WW2 pack beyond those names and mesh names. The first-person viewmodels
are separate prefabs, so they can be replaced independently.

## Known gaps (on purpose)

- No aiming, shooting, damage, health or respawn — fire only logs.
- Main menu Campaign/Multiplayer/Settings are placeholders. Range runs a solo local host, not a true
  offline mode. There is no pause or return-to-menu yet.
- No anime art yet; the WW2 pack is a placeholder.
- LAN/IP only. Relay, lobbies and Accounts (Unity Multiplayer Services) are installed but unused.
- Viewmodel placement is eyeballed and needs a tuning pass in the editor.
- Remote weapon-swap and the shoot RPC were not exercised end-to-end (no input automation in the
  test harness); both are standard `NetworkVariable` / `ServerRpc` paths.
- No CI, no git repository yet.
