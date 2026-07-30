# Feature Spec — Abilities

**Last audited:** 2026-07-27

## Responsibilities

Gated traversal and combat abilities that the player earns through progression. Abilities extend
the hero's move set without modifying core controller logic. Core run and jump remain readable;
permanent traversal and utility abilities become available only through explicit progression
unlocks.

---

## Current State

The ability unlock spine is implemented (Milestone 3). `PlayerAbilityState` exists; dash and wall-cling are gated; pickup and gate scene objects are available. Traversal ability tuning lives in `HeroAbilityConfig`; unlock flags remain in `PlayerAbilityState`.

| Ability | Status | Notes |
|---|---|---|
| Dash | **Gated** | `HeroDashAction`; gate: `PlayerAbilityState.dashUnlocked`; covers both ground and air dash |
| Wildstride (`Sprint`) | **Corrected movement implemented** | Shared Dash command; natural ground completion or one-shot air-Dash landing handoff; resource-free base movement; attack/presentation remain planned |
| Wall-slide | **Gated** | `HeroWallSlideAction`; gate: `PlayerAbilityState.wallClingUnlocked` |
| Wall-jump | **Gated** | `HeroWallJumpAction`; gate: `PlayerAbilityState.wallClingUnlocked` (shared with wall-slide) |
| Wall latch / aimed wall launch | Planned | Hold jump to latch, aim, and launch off wall |
| Spirit cast | Planned | Forward projectile ability; should use a separate spell/cast config, not `HeroAbilityConfig` |
| Double-jump | **Implemented (First Pass)** | `HeroJumpAction`; gate: `PlayerAbilityState.doubleJumpUnlocked` (default false); one double jump per airtime; coyote jump takes priority |
| Drift Cloak | Planned | — |
| Bind | **Gated** | `HeroBindAction`; gate: `PlayerAbilityState.bindUnlocked` (default false); grounded, hold-to-heal |
| Directional attack variants | Partial | Up/Down/Side all present; gating not implemented |

---

## Implemented Unlock System

### AbilityId (`Assets/_Project/Scripts/Hero/Core/AbilityId.cs`)
```csharp
public enum AbilityId { Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast, Bind }
```

### PlayerAbilityState (`Assets/_Project/Scripts/Hero/Core/PlayerAbilityState.cs`)
ScriptableObject at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` (create via CreateAssetMenu after compile; wire into HeroController Inspector field).

Fields: `dashUnlocked`, `wallClingUnlocked`, `sprintUnlocked`, `wallLatchUnlocked`,
`doubleJumpUnlocked`, `driftCloakUnlocked`, `spiritCastUnlocked`, and `bindUnlocked`.

**Defaults corrected (Package A2 Stage 0, 2026-07-28).** Field initializers, `AbilitySaveData`,
`ResetToDefaults()`, and the serialized `PlayerAbilityState.asset` now all start every one of the
eight flags locked, matching the authoritative new-game contract below.

Existing saves are preserved. `GatherSaveData` writes all eight booleans explicitly, so
deserialization overwrites the initializers with the stored values; a save whose ability section is
missing or null gets a fresh `AbilitySaveData` and therefore the all-locked default, which is the
intended fresh state. `SaveDataMigrator.CurrentSaveVersion` was **not** bumped: no migration
behaviour changed, only initializer values.

To test with abilities unlocked, use the UI Sandbox's isolated runtime ability state or the
in-world `AbilityPickup` objects. Do not edit the production asset back to unlocked —
`PlayerPersistentStateTests.ProductionAbilityStateAssetShipsFullyLocked` fails if you do.

Methods: `IsUnlocked(AbilityId)`, `Unlock(AbilityId)`, `Lock(AbilityId)`, `SetUnlocked(AbilityId, bool)`, `ResetToDefaults()`.

Event: `AbilityChanged(AbilityId, bool)` — fired by `SetUnlocked` only when the value actually changes. Scene objects (e.g. `AbilityGate`) subscribe to this event to react at runtime without polling.

**Save integration:** `PlayerAbilityState` implements `ISaveTarget`. `GatherSaveData(SaveData)` copies the 8 bool fields into `data.abilities`. `ApplySaveData(SaveData)` calls `SetUnlocked(AbilityId, bool)` for each flag (never direct field assignment), so runtime subscribers receive `AbilityChanged` events when values change. Initial scene gates are correct after load because `AbilityGate.OnEnable()` calls `Refresh()` against the already-applied state. `SaveManager` holds a serialized reference to this asset and calls both methods at save/load time. `ResetToDefaults()` is intentionally excluded from the save pipeline — it does not fire events and is reserved for editor/debug resets only.

### Unlock flag model
- **Dash** uses `dashUnlocked`. Covers both ground dash and air dash — there are no separate `groundDashUnlocked` or `airDashUnlocked` flags.
- **Wall-slide and wall-jump** share `wallClingUnlocked`. There are no separate `wallSlideUnlocked` or `wallJumpUnlocked` flags.
- WallLatch, DriftCloak, and SpiritCast remain defined but unimplemented. Sprint is implemented as
  player-facing Wildstride movement; its attack and final presentation remain planned.
- **Bind** uses `bindUnlocked`. Checked in `HeroBindAction.CanStart()` only (not `CanContinue()`), matching the dash/wall-cling convention of gating on entry.

### AbilityPickup (`Assets/_Project/Scripts/World/Persistence/Participants/AbilityPickup.cs`)
MonoBehaviour. Serialized fields: `string worldObjectId`, `PlayerAbilityState abilityState`, `AbilityId ability`, `WorldStateRegistry registry`, `bool disableAfterPickup`. Detects the hero via `GetComponentInParent<HeroBox>()` with `HeroController` fallback. Calls `abilityState.Unlock(ability)` and `registry.MarkPickupCollected(worldObjectId)` on trigger.

**Save integration (implemented):** Unlocking an ability updates `PlayerAbilityState` immediately, and the physical pickup's consumption is recorded in `WorldStateRegistry.collectedPickupIds` (serialized via `WorldSaveData`) in the same trigger — no longer deferred. `PlayerAbilityState` remains the sole authority for ability ownership; the registry only remembers that this specific physical pickup instance was consumed, so a defeated/suppressed pickup GameObject stays disabled after reload without needing to re-check ability state every scene load. On `Awake()`, `AbilityPickup` reconciles the two records: if the ability is already unlocked, a missing pickup record is filled in without replaying unlock feedback; if the pickup is recorded consumed but the ability is not unlocked (an inconsistent state — this should not occur in normal play), `PlayerAbilityState` wins: the stale record is cleared via `WorldStateRegistry.ClearPickupCollectedRecord` and the pickup stays active for reacquisition, without unlocking the ability, firing `AbilityChanged`, or replaying collection feedback.

### AbilityGate (`Assets/_Project/Scripts/World/AbilityGate.cs`)
MonoBehaviour. Serialized fields: `PlayerAbilityState abilityState`, `AbilityId requiredAbility`, `GameObject blocker`, `Collider2D blockerCollider`. Calls `Refresh()` in `OnEnable` so gates match already-loaded ability state, then subscribes to `AbilityChanged` for runtime unlock/lock changes; unsubscribes in `OnDisable`. Exposes `Refresh()` — call manually after `ResetToDefaults()` if needed at runtime.

---

## Intended Architecture

### Ability Unlock State
- Store unlocked ability flags on a `PlayerAbilityState` ScriptableObject.
- Each Action class checks the relevant flag in its `CanStart` condition before proceeding.
- `HeroAbilityConfig` holds per-ability tuning values; the unlock flag is separate from tuning.
- A new game begins with every permanent ability locked.
- Explicit progression unlocks are the only way permanent abilities become available.

### Authoritative new-game unlock contract

For a true new game:

| Ability flag | Intended initial state |
|---|---|
| Dash | Locked |
| Wall Cling | Locked |
| Sprint | Locked |
| Wall Latch | Locked |
| Double Jump | Locked |
| Drift Cloak | Locked |
| Spirit Cast | Locked |
| Bind | Locked |

Dash and Wall Cling are progression unlocks, not starting capabilities. Code, save-data, reset, and
asset defaults all match this contract as of Package A2 Stage 0.

### Gear presentation boundary

`PlayerAbilityState` remains authoritative for permanent ability unlocks. The planned Gear screen
is a read-only presentation of acquired physical progression objects mapped to that ownership. It
does not unlock, purchase, equip, tune, or modify abilities. A true new game therefore presents an
intentional empty Gear collection until the first approved ability-granting physical object is
acquired. See `Docs/FeatureSpecs/Gear.md`.

### Wildstride (internal Sprint)

Wildstride shares the Dash command. A fresh press attempts Dash immediately; there is no hold
threshold and no standalone Sprint action. A typed `HeroDashCompletion` identifies one Dash
sequence/version, its ground/air origin, direction, and typed end reason.

A natural grounded completion may enter Wildstride immediately. A natural ordinary air-Dash
completion may instead create one pending landing authorization. The first landing consumes that
exact version whether entry succeeds or fails. Both entry paths require `sprintUnlocked`, held and
armed Dash, a valid direction (current input for initial entry/air-Dash landing, remembered sequence
direction for an established Wildstride landing), and no incompatible owner.
  Zero resource does not block either path. Idle holds, cooldown holds, stale completions,
cancelled Dashes, and unrelated landings cannot begin Wildstride.

`HeroSprintAction` owns a small internal phase model, authorization, landing consumption, captured
carry direction, a short ledge-jump buffer, typed cancellation, and disarm-until-release. Only grounded
Wildstride, jump carry, and direction are mirrored to the blackboard. Ordinary movement explicitly
requests `Walk`; grounded Wildstride requests `Wildstride`. `HeroMotor` remains the sole velocity
writer.

Jump uses the unchanged normal vertical path plus motor-owned horizontal carry. The private phase
model distinguishes `AirborneCarry` (forced `sprintJumpSpeed` plus first-landing authorisation) from
`AirborneAuthorised` (forced carry ended; ordinary airborne steering/gravity active; first-landing
authorisation retained). Carry ends at normal falling, neutral/opposite airborne input, Double Jump,
or residual carry cleanup without ending the sequence. Landing consumes that authorisation exactly
once and reconciles directly to grounded Wildstride in the same fixed step. There is no airborne
timer, apex expiry, sustaining-state expiry, or generic-not-carrying expiry. Grounded direction
reversal preserves authorization: the motor decelerates toward zero, changes facing at the turn
seam, and accelerates in the new direction. Once grounded Wildstride is active, neutral horizontal
input retains the last valid direction and continues Wildstride while Dash remains held and armed;
a later opposite input uses the same turn path. Dash release ends the sequence.

Double Jump cancels forced carry, leaves the existing normal Double Jump vertical path and tuning
untouched, keeps Dash armed, and preserves an existing Wildstride landing authorisation. This is an
intentional Underbrew continuity decision: the supplied Silksong C# proves that Double Jump cancels
shuttlecock carry, but the hidden `sprintFSM` was not supplied, so its complete landing policy is
not proven by the reference. Underbrew deliberately preserves landing Wildstride authorisation
after Double Jump for continuity and game feel. An ordinary Jump plus Double Jump cannot create
authorisation. Wall/ledge/pogo, Attack/Bind, hurt/death, control/input loss, scene lifecycle, and
runtime unlock loss remain genuine hard cancellations.

Base Wildstride, Dash, jump carry, and landing resumption never read or mutate
`PlayerResourceState`. A provisional `sprintLedgeJumpBufferTime` (0.08 seconds) lets a Jump started
just after leaving a ledge from active Wildstride consume one private authorization and begin the
specialized carry. It does not alter normal coyote time or normal Jump buffering.

A future optional enhanced-speed Gear may drain resource only while its grounded bonus is active.
Base Wildstride remains available at empty resource; airborne carry receives no bonus or drain.
Exact Gear ownership and tuning remain deferred.

### Wall-Jump
Standard wall jump is the quick, responsive traversal option. It should feel immediate and intuitive.

- Trigger condition: while wall-sliding, a short Jump press performs a standard wall jump.
- Input model: tap Jump to wall-jump; holding Jump beyond the latch threshold should not trigger the standard wall jump.
- On trigger: apply a jump velocity with a horizontal component away from the wall; suppress wall-slide immediately; optionally apply a brief relatch lockout to prevent instantly re-entering the wall.
- Use `HeroWallJumpAction`; instantiate and tick it in `HeroActionController`.
- The velocity application belongs in a new `HeroMotor.StartWallJump(int wallDirection)` method.
- A `wallJumping` flag should be written to `HeroStateBlackboard` so `HeroWallSlideAction` and other actions can react to it.
- Animation: play `HeroAnimationLibrary.wallJump` (clip slot already exists).
- Gate: check `PlayerAbilityState.wallClingUnlocked` before allowing (shared with wall-slide).

### Wall Latch / Aimed Wall Launch
Wall latch is the more deliberate, skill-based complement to standard wall jump. It is not a replacement — both behaviours coexist, with wall jump as the quick option and wall latch as the expressive one.

- Entry: while on a valid wall, holding Jump beyond a short latch threshold enters a latch state.
- While latched: the player can choose a launch direction using movement input. The hero sticks to the wall and does not slide.
- Launch: releasing Jump launches the player in the aimed direction. A second Jump press can optionally also be supported if desired.
- Launch direction uses a directional vector or clamped angle; always clamp so the launch is away from the wall — the player cannot launch directly into the wall surface.
- Use `HeroWallLatchAction` to own: latch hold detection, latch entry, latch state, aim direction tracking, launch, and relatch cooldown.
- `HeroWallLatchAction` should write a `wallLatched` flag to `HeroStateBlackboard`; `HeroWallSlideAction` should treat this as a blocking condition.
- The velocity application on launch belongs in `HeroMotor`.
- Gate: check `PlayerAbilityState.wallLatchUnlocked` before allowing.

### Spirit Cast
Spirit Cast is the first ranged combat ability. A cast fires a forward-travelling projectile.

- Trigger condition: Cast input pressed, ability unlocked, and sufficient player resource available.
- On successful cast: consume the resource immediately, then request a projectile spawn.
- Use `HeroSpiritCastAction` to own: input detection, unlock check, resource check, cooldown, and projectile spawn request.
- Projectile behaviour — movement, collision, damage delivery, lifetime, and VFX — lives in a separate component on the projectile prefab. The cast action is not responsible for anything that happens after spawn.
- Damage delivery should use the established hit-receiver interface pattern (`IHeroAttackReceiver`) so projectile hits behave consistently with melee hits.
- `PlayerResourceState` is the implemented, persisted resource owner currently displayed by the HUD
  and spent by Bind. A future Spirit Cast design must explicitly decide whether it spends that same
  resource and then use the established owner rather than inventing UI-owned resource state.
- Gate: check `PlayerAbilityState.spiritCastUnlocked` and resource availability before allowing.

### Adding a New Ability
1. Add an unlock flag to `PlayerAbilityState`.
2. If the ability requires new physics behaviour, add a method to `HeroMotor`.
3. If the ability is a new triggered action (wall-jump, spirit cast), create a `Hero<Name>Action` plain C# class and instantiate it in `HeroActionController.Initialize`.
4. If an ability modifies locomotion, use a typed locomotion request and communicate through
   `HeroMotor`; actions never write velocity directly.
5. If the ability involves a new shared state that other actions need to react to (e.g. `wallJumping`, `wallLatched`), add a flag to `HeroStateBlackboard` and write it from the owning action class.
6. If the ability spawns an entity (spirit cast), keep the spawn request in the action class and all spawned-entity behaviour in a separate component.
7. Reserve an animation slot in `HeroAnimationLibrary` and add the clip.
8. **Add tuning values to `HeroAbilityConfig`** (traversal) or `HeroConfig` (core shared movement/combat). Keep unlock flags on `PlayerAbilityState` only. Spirit Cast should use a separate spell/cast config — do not add cast tuning to `HeroAbilityConfig`.

---

## Dependencies

- `HeroActionController` — hosts and ticks all action instances
- `HeroMotor` — receives velocity commands and movement modifier signals
- `HeroStateBlackboard` — shared action state flags
- `HeroConfig` — core shared movement/combat tuning (walk/run, base jump, gravity, attack, pogo, sensors, health/hurt, animation fades)
- `HeroAbilityConfig` — gated traversal ability tuning (dash, wall-slide, wall-jump, double-jump); asset at `Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset`
- `PlayerAbilityState` — unlock flags only (implemented; asset at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`); does not store any tuning values
- `PlayerResourceState` — implemented and persisted; displayed by `ResourceDisplay`, spent by
  `HeroBindAction`, and generated by eligible combat hits. Base Wildstride does not consume it.
  Future Spirit Cast and optional enhanced-speed Gear usage remain design decisions.
- Projectile prefab / projectile data assets (TODO) — used by Spirit Cast; behaviour lives on the prefab, not in the cast action

---

## Rules

- Ability gates live in the Action class that implements the ability, not in `HeroController` or `HeroStateBlackboard`.
- Unlocking an ability must not require scene changes or code recompilation — it must be data-driven through `PlayerAbilityState`.
- Abilities do not communicate with each other directly; coordination happens through the blackboard.
- Locomotion-modifying abilities (sprint) must route movement changes through the action/motor/config pipeline — they may not write `Rigidbody2D` velocity directly or bypass `HeroMotor`.
- Combat abilities that spawn entities (spirit cast) must separate cast logic from spawned entity behaviour — the action class is responsible up to and including the spawn request only.
- Standard wall jump and wall latch are distinct behaviours and must remain so even though both interact with wall-slide state. Do not merge them into a single action class.
- Ledge climb is always available core movement. Do not add it to `AbilityId` or
  `PlayerAbilityState`, and do not merge it with the planned Wall Latch ability. Its isolated
  tuning remains in `HeroConfig`; see `Docs/FeatureSpecs/LedgeClimb.md`.

## Planned validation

The new-game-default correction is covered by `PlayerPersistentStateTests` (EditMode), which proves:

- A new `AbilitySaveData` and a new `PlayerAbilityState` instance start with all eight locked.
- `PlayerAbilityState.ResetToDefaults()` restores all eight to locked.
- The fresh-save composition (`new SaveData` → `Migrate` → apply) yields all eight locked.
- A save with a missing or explicitly null ability section migrates to all-locked defaults without
  a schema version increase.
- A save with explicit unlocks applies exactly those values, unchanged by the new defaults.
- All 256 explicit unlock combinations survive a serialize/deserialize/apply round trip.
- The production `PlayerAbilityState.asset` ships fully locked.
- A true new-game state produces zero acquired entries in Gear (`GearScreenTests`).

The full EditMode suite (458 tests) and PlayMode suite (45 tests) pass, so existing ability
mechanics, gates, and pickup reconciliation regress unchanged. Hero feel was not re-playtested.

## Unity Editor work performed

- `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` reset to all-locked.
- `_SaveManager.prefab`, Hero prefab, pickups, and gates were not modified and continue to
  reference the same shared state asset.

Existing development save slots were **not** inspected or deleted. Any slot already containing
explicit unlocks keeps them; the change only affects a genuinely new game.

## Risks and open decisions

Risks:

- Changing code defaults without preserving explicit unlocks in existing development saves.
- Resetting a mutable asset while an unintended development state is still needed for a test scene.
- Treating `AbilityChanged` during save application as acquisition feedback.
- Letting Gear display data become a second ownership or tuning source.

Open decisions remain limited to ability-specific future mechanics and content: Wildstride attack,
presentation and final tuning, Wall Latch controls, Spirit Cast resource/cast design, Drift Cloak behavior, and physical
Gear identities/art/copy. Whether Dash or Wall Cling start unlocked is not open; both start locked
and appear in Gear only after progression acquisition and approved physical representation.
