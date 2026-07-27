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
| Sprint | Planned | Hold input to increase grounded move speed |
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

**Current code/design contradiction:** field initializers, `AbilitySaveData`, and
`ResetToDefaults()` currently set Dash and Wall Cling unlocked. The mutable serialized asset also
currently has Double Jump and Bind unlocked. Those are current implementation/development values,
not intended product defaults. The confirmed contract below requires all eight flags locked for a
new game. A future ability/save implementation pass must align field, save-data, reset, and
new-save initialization defaults; clean up mutable development assets; and review compatibility
with existing development saves. This has not been corrected.

Methods: `IsUnlocked(AbilityId)`, `Unlock(AbilityId)`, `Lock(AbilityId)`, `SetUnlocked(AbilityId, bool)`, `ResetToDefaults()`.

Event: `AbilityChanged(AbilityId, bool)` — fired by `SetUnlocked` only when the value actually changes. Scene objects (e.g. `AbilityGate`) subscribe to this event to react at runtime without polling.

**Save integration:** `PlayerAbilityState` implements `ISaveTarget`. `GatherSaveData(SaveData)` copies the 8 bool fields into `data.abilities`. `ApplySaveData(SaveData)` calls `SetUnlocked(AbilityId, bool)` for each flag (never direct field assignment), so runtime subscribers receive `AbilityChanged` events when values change. Initial scene gates are correct after load because `AbilityGate.OnEnable()` calls `Refresh()` against the already-applied state. `SaveManager` holds a serialized reference to this asset and calls both methods at save/load time. `ResetToDefaults()` is intentionally excluded from the save pipeline — it does not fire events and is reserved for editor/debug resets only.

### Unlock flag model
- **Dash** uses `dashUnlocked`. Covers both ground dash and air dash — there are no separate `groundDashUnlocked` or `airDashUnlocked` flags.
- **Wall-slide and wall-jump** share `wallClingUnlocked`. There are no separate `wallSlideUnlocked` or `wallJumpUnlocked` flags.
- Sprint, WallLatch, DriftCloak, and SpiritCast are defined in the enum and `PlayerAbilityState` but their action classes do not exist yet.
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

Dash and Wall Cling are progression unlocks, not starting capabilities. Current defaults that
unlock them are known implementation debt and must not redefine player-facing behavior.

### Gear presentation boundary

`PlayerAbilityState` remains authoritative for permanent ability unlocks. The planned Gear screen
is a read-only presentation of acquired physical progression objects mapped to that ownership. It
does not unlock, purchase, equip, tune, or modify abilities. A true new game therefore presents an
intentional empty Gear collection until the first approved ability-granting physical object is
acquired. See `Docs/FeatureSpecs/Gear.md`.

### Sprint
Sprint is a movement modifier, not an action — it adjusts grounded move speed while held.

- Trigger condition: `SprintHeld && grounded && moving`.
- While active, movement speed increases (tuned in `HeroConfig`; separate from `runSpeed` or gated behind it depending on final design).
- Sprint ends immediately when Sprint input is released or movement conditions are no longer met.
- Use a `HeroSprintAction` class that evaluates conditions each `Tick` and signals the motor.
- The motor applies the speed change through the normal horizontal velocity pipeline — sprint must not bypass `HeroMotor` or write velocity directly.
- Gate: check `PlayerAbilityState.sprintUnlocked` before allowing.

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
4. If the ability is a movement modifier (sprint), create an action class that evaluates conditions each `Tick` and communicates the result to `HeroMotor` through the normal pipeline — not by writing velocity directly.
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
- `PlayerResourceState` — implemented and persisted; displayed by `ResourceDisplay` and spent by
  `HeroBindAction`. Future Spirit Cast resource usage remains a design decision.
- Projectile prefab / projectile data assets (TODO) — used by Spirit Cast; behaviour lives on the prefab, not in the cast action

---

## Rules

- Ability gates live in the Action class that implements the ability, not in `HeroController` or `HeroStateBlackboard`.
- Unlocking an ability must not require scene changes or code recompilation — it must be data-driven through `PlayerAbilityState`.
- Abilities do not communicate with each other directly; coordination happens through the blackboard.
- Locomotion-modifying abilities (sprint) must route movement changes through the action/motor/config pipeline — they may not write `Rigidbody2D` velocity directly or bypass `HeroMotor`.
- Combat abilities that spawn entities (spirit cast) must separate cast logic from spawned entity behaviour — the action class is responsible up to and including the spawn request only.
- Standard wall jump and wall latch are distinct behaviours and must remain so even though both interact with wall-slide state. Do not merge them into a single action class.

## Planned validation

The prerequisite new-game-default correction must add coverage proving:

- A newly created save starts with all eight abilities locked.
- `PlayerAbilityState.ResetToDefaults()` restores all eight abilities to locked.
- Explicit unlocks persist through save/load.
- Existing save data applies its explicitly stored values rather than being overwritten by new
  defaults.
- A true new-game state produces zero acquired entries in Gear.
- Mutable production/development asset values are not used as test defaults.

Existing ability mechanics, gates, pickup reconciliation, and validated Hero feel must regress
unchanged. No Unity compilation, tests, Editor validation, or playtesting was run for this
documentation update.

## Required Unity Editor work for the future correction

- Reset or recreate `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` so mutable
  development values do not masquerade as new-game defaults.
- Verify `_SaveManager.prefab`, Hero prefab, pickups, and gates continue referencing the intended
  shared state asset.
- Review existing development save slots before changing defaults or migration behavior.

No Unity Editor work was performed by this documentation update.

## Risks and open decisions

Risks:

- Changing code defaults without preserving explicit unlocks in existing development saves.
- Resetting a mutable asset while an unintended development state is still needed for a test scene.
- Treating `AbilityChanged` during save application as acquisition feedback.
- Letting Gear display data become a second ownership or tuning source.

Open decisions remain limited to ability-specific future mechanics and content: Sprint behavior and
tuning, Wall Latch controls, Spirit Cast resource/cast design, Drift Cloak behavior, and physical
Gear identities/art/copy. Whether Dash or Wall Cling start unlocked is not open; both start locked
and appear in Gear only after progression acquisition and approved physical representation.
