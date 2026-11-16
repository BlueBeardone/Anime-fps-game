# Anime FPS Game — Shooting, Reloading and Damage Design

Date: 2026-11-16
Status: awaiting approval

## Intent

Replace the placeholder "shoot" log with a real, tunable gun system: four weapon archetypes behind one
gun contract, raycast firing with bloom and learnable spray, magazine/reserve reloading, and
server-validated damage against dummies and players. This is the combat foundation the rest of the game
will build on.

Why now: the controller, multiplayer, and weapon-slot switching exist; shooting is the next playable
layer and the one every later feature (enemies, modes, progression) depends on.

## Goals

- One `IGun` contract with an abstract `Gun` base and four concrete types: **Rifle**, **Handgun**,
  **Shotgun**, **SubmachineGun**.
- Fully working firing: semi-auto and full-auto, per-gun cadence, raycast hits, shotgun pellet spread.
- Bloom: spread grows while firing, caps per gun, recovers when you stop.
- Spray pattern: a fixed, per-gun recoil path that moves the camera predictably, so it can be learned.
- Reloading: magazine + reserve ammo, reload timer, auto-reload when empty, cancel on weapon switch.
- Damage: `IDamageable` / networked `Health`, applied by the **server** after re-raycasting the shot.
- Test dummies in `Main.unity`; players can damage each other and respawn.
- Visible feedback: crosshair that breathes with bloom, ammo readout, tracers, impact puffs, muzzle
  flash, procedural viewmodel kick and reload dip.
- All four guns available in the existing slot system (cycle with Next/Previous and mouse wheel).

## Non-goals

- Aim-down-sights, attachments, grenades, weapon throwing, ammo pickups.
- Headshot multipliers, damage falloff, wall penetration.
- Lag compensation, server-side ammo authority, shot prediction/reconciliation.
- Audio, authored reload animations, new weapon art.
- Enemy AI, game modes, progression, true offline mode.

## Anti-goals

- A system where only the host sees correct damage. Damage must work for clients on a dedicated-style
  server/host.
- Four near-duplicate gun scripts. Shared behaviour lives in `Gun`; subclasses only describe what makes
  the archetype different.
- Gun state on the viewmodel. Viewmodels are destroyed on weapon switch; ammo and reloads must survive.

## Constraints

- Unity 6000.6.9f1, URP, Input System 1.20.1, Netcode for GameObjects 2.14.0 (owner-authority movement
  and weapon slot already in place).
- `Assets/_Project/Editor/ProjectSetup.cs` is the authoritative generator: `Anime FPS > Run All` rebuilds
  the scene, NetworkManager, player prefab and menu. Any prefab/scene wiring must be added there or it is
  lost on the next run.
- The shared `InputSystem_Actions.inputactions` has `Attack` (button), `Next`, `Previous`, `Look`, but no
  `Reload`. A `Reload` action must be added.
- Only two first-person viewmodels exist (K98, Luger P08). MP40, STG44 and MG42 art exists for the
  soldier body. The shotgun has no art.
- Existing PlayMode tests must keep passing; the project is not a git repository from the assistant's
  working directory, so changes should stay small and reviewable.

## Approaches

1. **Subclasses on the viewmodel prefabs.** Abstract `Gun : MonoBehaviour` on each gun prefab, stats in
   the Inspector. Literal and simple, but viewmodels are destroyed on slot change, so ammo/reload state
   would reset every swap; stats also need duplicating per prefab.
2. **ScriptableObject definitions plus one generic runtime.** Cheap variants and designer-friendly, but
   the archetype behaviour collapses into data, contrary to the requested interface-plus-classes shape,
   and adds indirection for four guns.
3. **Hybrid pure-logic classes + persistent runtime owner (chosen).** `IGun` / abstract `Gun` own the
   reusable loop (ammo, reload, cadence, bloom, spray). `Rifle`, `Handgun`, `Shotgun`, `SubmachineGun`
   supply only archetype differences. `PlayerWeapons` creates one gun instance per serialized slot and
   keeps them alive across switches. Gun logic is plain C#, so it is testable without a scene; Unity
   lifecycle, input, raycasts, effects and networking stay in `PlayerWeapons`.

Why the chosen approach wins: it matches the requested shape, preserves ammo across swaps, keeps gun
rules unit-testable, and leaves one obvious place (`WeaponDefinition`) for tuning in the Inspector.

## Decisions made with the human

- Scope **B**: mechanics plus damageable targets, health, and server-validated hits.
- Architecture **C** (hybrid).
- Server re-raycasts client-reported shot rays; ammo and reloads are owner-authoritative.

## Design

### Gun contract

`Scripts/Weapons/IGun.cs` — the public contract:

```csharp
public interface IGun
{
    string DisplayName { get; }
    GunStats Stats { get; }
    int AmmoInMagazine { get; }
    int ReserveAmmo { get; }
    bool IsReloading { get; }
    float ReloadProgress01 { get; }
    float CurrentSpreadDegrees { get; }
    bool CanFire(float now);

    void Tick(float deltaTime);
    bool TryFire(Vector3 origin, Vector3 aimForward, float now, bool triggerPressed,
        bool triggerHeld, out ShotSpec shot);
    bool TryStartReload();
    void CancelReload();
    void ResetAmmo();

    event System.Action<Vector2> RecoilKicked;      // degrees: x = yaw, y = pitch
    event System.Action AmmoChanged;
    event System.Action ReloadStateChanged;
}
```

`ShotSpec` is the result of one trigger pull:

```csharp
public struct ShotSpec
{
    public byte Slot;
    public Vector3 Origin;
    public Vector3[] Directions;   // one per pellet
    public float Damage;           // per pellet
    public float Range;
}
```

`Gun` (`Scripts/Weapons/Gun.cs`) implements the shared loop:

- fire-mode gate (`SemiAuto` needs `triggerPressed`, `FullAuto` accepts `triggerHeld`) and RPM cadence;
- magazine/reserve bookkeeping and reload timer (`Tick`);
- bloom growth/cap/recovery and spray index advance/reset;
- shot construction: aim direction + bloom cone + pellet count/spread;
- recoil event driven by the subclass's spray pattern.

Concrete types override only:

| Type | File | Behaviour |
|---|---|---|
| Rifle | `Rifle.cs` | Full-auto, 1 ray, 30-round pattern, moderate bloom |
| Handgun | `Handgun.cs` | Semi-auto, 1 ray, short 8-step pattern, punchy kick |
| Shotgun | `Shotgun.cs` | Semi-auto pump cadence, 8 pellets, cone spread, no learnable pattern |
| SubmachineGun | `SubmachineGun.cs` | Full-auto, 1 ray, fast cadence, high bloom, long pattern |

`GunStats` (`Scripts/Weapons/GunStats.cs`, `[System.Serializable]`) is the tuning block: archetype, fire
mode, RPM, damage, pellets, magazine, reserve, reload seconds, range, base spread, bloom per shot, max
bloom, bloom recovery, spray reset delay, pellet spread. Each subclass supplies `CreateDefaultStats()`.

**Tuning survives `Run All`** via `GunStatsAsset : ScriptableObject` (`Scripts/Weapons/GunStatsAsset.cs`)
with one asset per archetype under `Assets/_Project/Settings/Weapons/` (`Rifle.asset`, `Handgun.asset`,
`Shotgun.asset`, `SMG.asset`). `WeaponDefinition` holds a reference to the asset, so Inspector edits are
persistent. `ProjectSetup` creates a missing asset with that archetype's defaults but never overwrites an
existing one. `GunFactory.Create(GunStats)` reads `stats.archetype` to pick the concrete class.

### Runtime ownership

`WeaponDefinition` gains `GunStatsAsset stats`. `PlayerWeapons` builds `IGun[] guns` once, one per slot,
reading the asset's stats, and routes input to the active one. Ammo, reload and spray state
therefore survive slot changes. Switching cancels an in-progress reload on the old gun. `ResetAmmo()` is
called on respawn.

`PlayerWeapons` responsibilities remaining in Unity land:

- input: `Attack` (`IsPressed` for auto, `WasPressedThisFrame` for semi), `Reload`, `Next`/`Previous`,
  mouse wheel;
- raycasts for local feedback, damage requests to the server, tracer/impact effects;
- applying `RecoilKicked` to `PlayerLook`;
- driving the viewmodel kick/reload dip and the `WeaponHud`.

### Bloom and spray

- **Effective spread** = `baseSpread + bloom`; bloom rises by `bloomPerShot` per shot, clamps at
  `maxBloom`, and recovers at `bloomRecoveryPerSecond` once the gun has not fired for ~0.15 s.
- **Shot direction** = aim forward rotated by a random point inside a cone whose half-angle is the
  effective spread. Shotgun pellets use `pelletSpread` instead of the bloom cone.
- **Spray pattern** = an array of cumulative `Vector2` camera offsets in degrees. Each shot applies the
  delta from the previous pattern point to `PlayerLook` as recoil. The camera therefore follows a fixed
  path while firing; the crosshair (and bullets) follow it, so the pattern is learnable and identical
  every burst.
- Spray index advances per shot, clamps at the pattern end, and resets to 0 after `sprayResetSeconds`
  without firing, on reload, or on slot change.
- First shot has zero pattern kick (pattern starts at `(0,0)`).

Example defaults (first tuning pass, all Inspector-overridable):

| Gun | Mode | RPM | Dmg/pellet | Pellets | Mag/reserve | Reload | Base spread | Bloom/shot | Max bloom |
|---|---|---|---|---|---|---|---|---|---|
| Rifle | Full | 600 | 24 | 1 | 30 / 120 | 2.2 s | 0.25° | 0.35° | 3.5° |
| Handgun | Semi | 400 | 30 | 1 | 12 / 60 | 1.5 s | 0.20° | 0.70° | 3.0° |
| Shotgun | Semi | 70 | 9 | 8 | 6 / 24 | 2.6 s | 2.5° (pellet cone 3°) | 1.0° | 6.0° |
| SMG | Full | 900 | 18 | 1 | 32 / 160 | 1.8 s | 0.50° | 0.45° | 5.0° |

Reload rules: reload only when not full and reserve > 0; loads
`min(magazineSize - current, reserve)`; firing is blocked during reload; auto-reload starts when a shot
empties the magazine; full-auto does not chain reloads unintentionally.

### Recoil and viewmodel feel

- `PlayerLook` gains `AddRecoil(Vector2 degrees)` and a separate recoil offset layered on top of mouse
  pitch/yaw. The offset recovers toward zero after a short delay; mouse input is never blocked.
- `PlayerWeapons` procedurally kicks the viewmodel backward/upward on each shot and dips/rotates it down
  during reload, using the existing `viewmodelAnchor` and current viewmodel transform. No authored clips.

### Damage and health

- `Scripts/Combat/HealthState.cs` — plain C# core: `Current`, `Max`, `IsDead`, `ApplyDamage(float)`
  (returns true only on the transition to dead), `Revive()`. This is what the unit tests drive.
- `Scripts/Combat/IDamageable.cs`: `void ApplyDamage(float amount, Vector3 point, Vector3 normal, ulong
  attackerClientId)`.
- `Scripts/Combat/Health.cs` (`NetworkBehaviour`): wraps `HealthState`, exposes
  `NetworkVariable<float>` health and `NetworkVariable<bool> IsDead` (read by everyone, written by the
  server), and a `Died` event fired once. `ApplyDamage` is server-only, ignores self-damage on
  players (dummies disable that check because scene `NetworkObject`s are server-owned), and ignores
  hits while dead. `Revive()` restores full health and clears `IsDead`.
- Hit resolution: the raycast hit's `Health` is found with `GetComponentInParent<Health>()`; the server
  applies per-pellet damage.
- On `IsDead`, all peers disable the player's `CharacterController`, colliders and renderers, so corpses
  neither absorb shots nor block movement. `PlayerMotor` and `PlayerWeapons` also read `IsDead` and
  suppress movement/firing (the owner stops sending input).
- Players: health 100. `Scripts/Combat/PlayerRespawn.cs` (`NetworkBehaviour`, owner + server) caches the
  owner's first spawn position/rotation, runs the 3 s server timer, calls `Health.Revive()`, and sends a
  `RespawnClientRpc` to the owner. The **owner** teleports itself to its cached spawn and calls
  `PlayerWeapons.ResetAmmo()`, so owner-authority `NetworkTransform` replicates the move without
  rubber-banding and `NetworkBootstrap` needs no new spawn bookkeeping.
- Dummies: health 100, hide and disable colliders on death, restore after 3 s. Three dummies placed down
  range in `Main.unity`.

### Networking

- Client fires immediately for feel: local raycast → local effects, HUD, recoil.
- Client sends `FireServerRpc(byte slot, Vector3 origin, Vector3[] directions)` on `PlayerWeapons`.
- **Server validation** before applying damage:
  - slot exists and the pellet count is ≤ the slot's configured pellet count;
  - `origin` is within 3 m of the shooter's server-side position (cheap muzzle sanity);
  - every direction is normalized and within a 60° horizontal cone of the shooter's server-side forward;
  - per-slot cadence limit (server rejects shots faster than ~95% of the configured RPM);
  - server raycasts from `origin` for each pellet with the gun's range, ignoring the shooter's own
    colliders and triggers, then applies damage.
- Server broadcasts `FireEffectClientRpc` (origin, directions, hit points/normals, shooter id) to
  everyone except the shooter, so remote clients see tracers and impacts. The shooter already played
  them locally.
- Ammo/reload stay owner-side and are **not** replicated or validated (accepted tradeoff; documented in
  code). Health is server-authoritative.
- No lag compensation: the server trusts the aim direction but decides what the ray hits. Moving targets
  can be hit slightly late; acceptable for this milestone.
- Respawn is split deliberately: the **server** owns the timer, death state and health; the **owner**
  owns the teleport and ammo reset. This respects owner-authority movement and avoids new spawn
  assignment state in `NetworkBootstrap`.

### UI and effects

- `Scripts/UI/WeaponHud.cs` builds its own runtime uGUI canvas on the owning player (no scene assets, no
  TextMeshPro dependency): a four-bar crosshair whose gap tracks `CurrentSpreadDegrees`, ammo text
  `mag / reserve`, and a reload progress bar/label.
- `Scripts/Effects/ShotEffects.cs` + a tiny pool: tracer `LineRenderer` (short-lived, faded), impact puff
  (small pooled sphere scaling/fading), and a brief muzzle point light. Placeholder primitives only.
- Effects are local and cosmetic; they never drive damage.

### Input

Add a `Reload` action to the `Player` map in `InputSystem_Actions.inputactions`, bound to
`<Keyboard>/r` and `<Gamepad>/dpad/down` (`buttonWest` is already `Attack`; keeping fire untouched).
`Attack` stays a button action; full-auto reads `IsPressed`, semi-auto reads `WasPressedThisFrame`.
Number-key direct slot selection is not added in this pass (cycle with Next/Previous and wheel).

### Slots and art mapping

`PlayerWeapons.slots` grows from 2 to 4:

| Slot | Archetype | Viewmodel | Soldier mesh | Animator int |
|---|---|---|---|---|
| 0 | Rifle | K98_Viewmodel | K98 | `status_k98` |
| 1 | Handgun | LugerP08_Viewmodel | Luger_P08 | `Status_LugerP08` |
| 2 | SMG | MP40_Viewmodel (new copy) | MP40 | `status_MP40` |
| 3 | Shotgun | K98_Viewmodel (placeholder) | K98 | `status_k98` |

`ProjectSetup.EnsureViewmodel` copies `MP40_prefab.prefab` into `Prefabs/Weapons/MP40_Viewmodel.prefab`.
The shotgun borrows the K98 until real art exists.

### ProjectSetup changes

Because `Run All` rebuilds the player prefab, all wiring is added to `Editor/ProjectSetup.cs`:

- copy the MP40 viewmodel;
- ensure the four `GunStatsAsset` assets exist without overwriting tuned values;
- configure four slots, referencing those assets;
- add `Health` and `PlayerRespawn` to the player prefab;
- add three target dummies to `Main.unity` (capsules with `NetworkObject` + `Health` + `TargetDummy`);
- keep every step idempotent so re-running does not duplicate objects.

`Health` on scene dummies relies on NGO scene management (enabled by default) auto-spawning scene
`NetworkObject`s.

### Files

```
Assets/_Project/Scripts/Weapons/
  IGun.cs, Gun.cs, GunStats.cs, GunStatsAsset.cs, GunArchetype.cs, ShotSpec.cs, GunFactory.cs
  Rifle.cs, Handgun.cs, Shotgun.cs, SubmachineGun.cs
Assets/_Project/Scripts/Combat/
  IDamageable.cs, HealthState.cs, Health.cs, PlayerRespawn.cs, TargetDummy.cs
Assets/_Project/Scripts/Player/
  PlayerWeapons.cs (rewritten), PlayerLook.cs (recoil), WeaponDefinition.cs (stats asset)
Assets/_Project/Scripts/UI/WeaponHud.cs
Assets/_Project/Scripts/Effects/ShotEffects.cs
Assets/_Project/Editor/ProjectSetup.cs (updated)
Assets/_Project/Settings/Weapons/
  Rifle.asset, Handgun.asset, Shotgun.asset, SMG.asset (created by ProjectSetup)
Assets/InputSystem_Actions.inputactions (Reload action)
Assets/_Project/Tests/PlayMode/GunTests.cs, HealthStateTests.cs
docs/superpowers/specs/2026-11-16-shooting-design.md (this file)
```

### Tests

Pure-logic PlayMode tests (no scene) for `Gun`:

- fire consumes one round and respects RPM cadence;
- full-auto fires while `triggerHeld`, semi-auto only on `triggerPressed`;
- reload takes the configured time, moves rounds from reserve, and caps at magazine size;
- auto-reload on empty, no reload when full or out of reserve;
- switch/reload cancels correctly (via `CancelReload`);
- bloom rises, clamps, and recovers;
- shotgun emits `Pellets` directions within the pellet cone;
- spray index advances, clamps, and resets after the delay;
- `HealthState` clamps at zero, reports death once, and `Revive` restores.

Existing player/menu smoke tests must stay green.

## My calls (correctable)

- Plain C# gun classes owned by `PlayerWeapons`, not MonoBehaviours on viewmodels (state must survive
  swaps and stay testable).
- Stats live in ScriptableObject assets so Inspector tuning survives `Run All`; behaviour stays in
  classes.
- Server re-raycasts client directions and validates origin/cone/cadence; aim direction is trusted.
- Owner-authoritative ammo.
- Server owns death timer/health; owner teleports itself on respawn using its cached first-spawn point.
- Magazines reload for the shotgun too (not shell-by-shell) to keep the first pass simple.
- Simple player death/respawn loop included so player damage is observable; dead bodies are hidden and
  uncollidable.
- Runtime-built uGUI HUD rather than scene assets, because `ProjectSetup` owns scene generation.
- Placeholder art: MP40 viewmodel for the SMG, K98 for the shotgun.
- Gamepad reload uses d-pad down to avoid rebinding fire.
- No headshots, falloff, or penetration in this pass.

## What is left to the builder

- Exact spray pattern arrays and feel (`bloom`, recoil recovery, viewmodel kick) after playing it.
- Dummy placement and range layout in `Main.unity`.
- Tracer/puff colours and lifetimes.
- Whether to add direct 1–4 slot keys and a hitmarker later.

## Risks

- Adding `Health` (a `NetworkBehaviour`) to the generated player prefab changes NGO component ordering;
  verify host + client spawn after `Run All`.
- Scene-placed dummy `NetworkObject`s depend on scene management; if they do not spawn, register them as
  network prefabs and spawn from `NetworkBootstrap` instead.
- `ClientRpc` payloads for eight pellets are small, but keep effects data compact.
- Without aim replication, a cheat could aim through walls toward a target; server raycast still blocks
  shooting through geometry. Acceptable until lag compensation exists.
