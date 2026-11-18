# Visible rifle and owner body — design

Status: approved and implemented
Date: 2026-11-18
Branch: `master` (continues the bolt-action rifle work)

## Problem

Playtest of the bolt-action rifle found two things:

1. **The gun is too small.** `K98_Viewmodel.prefab` is authored at real size (its renderer
   bounds are 1.16 m long at scale 1), but the Rifle slot's `viewmodelScale` is `0.08`, so the
   gun on screen is about 9 cm long. The scale was placeholder tuning from when the slot also
   pointed the wrong way and was not actually looked at.
2. **The owner sees no body.** `PlayerWeapons.ApplyVisibleWeaponMeshes` disables every renderer
   under `visualsRoot` when `IsOwner`. The camera is a floating head: no arms, chest or legs.

Both are cosmetic (the rifle fires, reloads and aims correctly), but they make the game look
unfinished.

## Evidence

Measured in batchmode with `PrefabUtility.InstantiatePrefab`:

- `K98_Viewmodel.prefab` renderer bounds at scale 1: size `(0.07, 0.21, 1.16)`,
  `z` from `-0.34` (stock) to `0.82` (muzzle). At the slot's `0.08` scale that is 9.3 cm.
- `Player.prefab` camera: `CameraPivot` at local `(0, 1.65, 0)` (matches
  `MovementProfile.eyeHeight`), pitch-only, body yaw driven by `PlayerLook.FixedUpdate`.
- Soldier renderers (bind pose): `Jacket` spans y `0.73`–`1.56`, `Throusers` y `-0.03`–`0.83`,
  `Helmet` y `1.52`–`1.80`, `Wehrmacht_soilder_A` (head + hands) y `1.28`–`1.74`.
  The camera point `(0, 1.65, 0)` is **inside** the helmet and head-mesh bounds and outside the
  jacket and trousers bounds.
- The soldier model has no arms-only mesh; body, head+hands, helmet, jacket and trousers are
  separate skinned meshes, plus one skinned mesh per weapon.

## Decisions

### 1. Visible guns in every slot

Raise `viewmodelScale` from `0.08` to `0.5` in **all four slots**, keeping each slot's position
and rotation. Measured authored lengths at scale 1: K98 1.16 m, MP40 0.72 m, Luger 0.25 m, so on
screen that is 58 cm, 36 cm and 12.5 cm respectively (the Shotgun reuses the K98 viewmodel).
At 75° FOV those read as roughly 80%, 50% and 25% of the screen height at their anchor depths,
which is the normal range for FPS viewmodels.

Update both `Player.prefab` and `ProjectSetup.cs` so a regeneration keeps the values. Playtest
tuning may adjust position or scale afterwards; `0.5` is the first honest value.

### 2. Owner body: torso, arms and legs, no head (option B)

For the **owner only**, show every soldier renderer except:

- the head-bearing meshes: `Wehrmacht_soilder_A` (head + hands) and `Helmet`;
- the per-weapon skinned meshes (`K98`, `Luger_P08`, `MP40`, …), because the owner already
  holds the floating viewmodel.

`Jacket` and `Throusers` stay visible, so looking down shows jacket sleeves (arms), chest and
legs. Hiding the head removes the hands as well, because they share one mesh; the floating gun
stays where it is, so there is nothing for hands to grip yet. This is accepted for this step;
the hands return with the later arms-rig option.

Remote players keep today's behaviour: full body plus exactly one weapon mesh, head included.

The rule lives in one place (`ApplyVisibleWeaponMeshes`, renamed to describe both audiences), so
a slot switch or respawn restores the right set. Head/helmet names go in a documented constant,
next to the existing `WeaponMeshNames`.

## Non-goals

- No first-person arms rig, no arm IK, no hand poses; the soldier's animated arms hold an
  invisible weapon. That is the deferred option C.
- No change to damage, cadence, reload, RPCs, tracers or the muzzle point (the muzzle follows the
  viewmodel transform, so the bigger rifle simply puts it further ahead).
- No change to how remote players look, including their visible weapon.
- No dead-body behaviour change.
- No HUD work.

## Tests

- **Viewmodel size guard** (new): with the Rifle active, the world-space renderer bounds of the
  current viewmodel are at least 0.4 m on their longest axis. This is RED at `0.08` (0.09 m) and
  GREEN at `0.5` (0.58 m).
- **Owner body visibility** (new): the owner has `Jacket` and `Throusers` renderers enabled,
  `Helmet`, `Wehrmacht_soilder_A` and the active weapon mesh disabled, and the camera is not
  inside the bounds of any enabled renderer.
- **Remote body visibility** (new): a non-owner player keeps head and helmet enabled and exactly
  one weapon mesh enabled.
- Existing suite stays green (72 tests today).

## Risks

- Hiding the head mesh also hides the hands and the neck; the jacket collar may show a small gap
  when looking straight down. Accepted for this step; option C replaces it later.
- The soldier's animated arms will not line up with the floating gun, especially during reload.
  Accepted for option B; it is the documented trade-off.
- `0.5` is a first value; position and scale remain playtest-tunable per slot.
- Crouching lowers the eye to 1.05 m, which is inside the jacket's bind-pose bounds, and the soldier
  has no crouch animation. The owner may see the inside of their own chest while crouched. Not
  addressed here; it belongs with the future arms rig.
