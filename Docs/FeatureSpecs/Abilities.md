# Feature Spec — Abilities

**Last audited:** 2026-05-20

## Responsibilities

Gated traversal and combat abilities that the player earns through progression. Abilities extend the hero's move set without modifying core controller logic. The baseline movement — run, jump, wall-slide — should stay fast and readable; abilities layer expressiveness and depth on top of that foundation.

---

## Current State

The ability unlock spine is implemented (Milestone 3). `PlayerAbilityState` exists; dash and wall-cling are gated; pickup and gate scene objects are available. Traversal ability tuning lives in `HeroAbilityConfig`; unlock flags remain in `PlayerAbilityState`.

| Ability | Status | Notes |
|---|---|---|
| Dash | **Gated** | `HeroDashAction`; gate: `PlayerAbilityState.dashUnlocked` (default true); covers both ground and air dash |
| Sprint | Planned | Hold input to increase grounded move speed |
| Wall-slide | **Gated** | `HeroWallSlideAction`; gate: `PlayerAbilityState.wallClingUnlocked` (default true) |
| Wall-jump | **Gated** | `HeroWallJumpAction`; gate: `PlayerAbilityState.wallClingUnlocked` (shared with wall-slide) |
| Wall latch / aimed wall launch | Planned | Hold jump to latch, aim, and launch off wall |
| Spirit cast | Planned | Forward projectile ability; should use a separate spell/cast config, not `HeroAbilityConfig` |
| Double-jump | **Implemented (First Pass)** | `HeroJumpAction`; gate: `PlayerAbilityState.doubleJumpUnlocked` (default false); one double jump per airtime; coyote jump takes priority |
| Drift Cloak | Planned | — |
| Directional attack variants | Partial | Up/Down/Side all present; gating not implemented |

---

## Implemented Unlock System

### AbilityId (`Assets/_Project/Scripts/Hero/Core/AbilityId.cs`)
```csharp
public enum AbilityId { Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast }
```

### PlayerAbilityState (`Assets/_Project/Scripts/Hero/Core/PlayerAbilityState.cs`)
ScriptableObject at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` (create via CreateAssetMenu after compile; wire into HeroController Inspector field).

Fields: `dashUnlocked` (default true), `wallClingUnlocked` (default true), `sprintUnlocked`, `wallLatchUnlocked`, `doubleJumpUnlocked`, `driftCloakUnlocked`, `spiritCastUnlocked` (all default false).

Methods: `IsUnlocked(AbilityId)`, `Unlock(AbilityId)`, `Lock(AbilityId)`, `SetUnlocked(AbilityId, bool)`, `ResetToDefaults()`.

Event: `AbilityChanged(AbilityId, bool)` — fired by `SetUnlocked` only when the value actually changes. Scene objects (e.g. `AbilityGate`) subscribe to this event to react at runtime without polling.

**Save integration:** `PlayerAbilityState` implements `ISaveTarget`. `GatherSaveData(SaveData)` copies the 7 bool fields into `data.abilities`. `ApplySaveData(SaveData)` calls `SetUnlocked(AbilityId, bool)` for each flag (never direct field assignment), so runtime subscribers receive `AbilityChanged` events when values change. Initial scene gates are correct after load because `AbilityGate.OnEnable()` calls `Refresh()` against the already-applied state. `SaveManager` holds a serialized reference to this asset and calls both methods at save/load time. `ResetToDefaults()` is intentionally excluded from the save pipeline — it does not fire events and is reserved for editor/debug resets only.

### Unlock flag model
- **Dash** uses `dashUnlocked`. Covers both ground dash and air dash — there are no separate `groundDashUnlocked` or `airDashUnlocked` flags.
- **Wall-slide and wall-jump** share `wallClingUnlocked`. There are no separate `wallSlideUnlocked` or `wallJumpUnlocked` flags.
- Sprint, WallLatch, DoubleJump, DriftCloak, and SpiritCast are defined in the enum and `PlayerAbilityState` but their action classes do not exist yet.

### AbilityPickup (`Assets/_Project/Scripts/World/AbilityPickup.cs`)
MonoBehaviour. Serialized fields: `PlayerAbilityState abilityState`, `AbilityId ability`, `bool disableAfterPickup`. Detects the hero via `GetComponentInParent<HeroBox>()` with `HeroController` fallback. Calls `abilityState.Unlock(ability)` on trigger.

**Save integration (deferred):** Unlocking an ability updates `PlayerAbilityState` immediately. The unlock persists across sessions only if the player activates a checkpoint (or quits while auto-save is enabled) after the pickup. Full world-state persistence (`collectedPickupIds` in `WorldSaveData`) is deferred to Milestone 4 when `WorldStateRegistry` is implemented. The design decision of whether ability pickups should force an immediate save is also deferred.

### AbilityGate (`Assets/_Project/Scripts/World/AbilityGate.cs`)
MonoBehaviour. Serialized fields: `PlayerAbilityState abilityState`, `AbilityId requiredAbility`, `GameObject blocker`, `Collider2D blockerCollider`. Calls `Refresh()` in `OnEnable` so gates match already-loaded ability state, then subscribes to `AbilityChanged` for runtime unlock/lock changes; unsubscribes in `OnDisable`. Exposes `Refresh()` — call manually after `ResetToDefaults()` if needed at runtime.

---

## Intended Architecture

### Ability Unlock State
- Store unlocked ability flags on a `PlayerAbilityState` ScriptableObject.
- Each Action class checks the relevant flag in its `CanStart` condition before proceeding.
- `HeroConfig` holds per-ability tuning values; the unlock flag is separate from tuning.
- Always-available abilities (dash, wall-slide) are represented in the same system defaulted to true so they can be gated later if scope changes.

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
- This ability likely requires a `PlayerResourceState` SO or equivalent to track castable resources independently of unlock flags.
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
- `PlayerResourceState` (TODO) — castable resource tracking; required by Spirit Cast
- Projectile prefab / projectile data assets (TODO) — used by Spirit Cast; behaviour lives on the prefab, not in the cast action

---

## Rules

- Ability gates live in the Action class that implements the ability, not in `HeroController` or `HeroStateBlackboard`.
- Unlocking an ability must not require scene changes or code recompilation — it must be data-driven through `PlayerAbilityState`.
- Abilities do not communicate with each other directly; coordination happens through the blackboard.
- Locomotion-modifying abilities (sprint) must route movement changes through the action/motor/config pipeline — they may not write `Rigidbody2D` velocity directly or bypass `HeroMotor`.
- Combat abilities that spawn entities (spirit cast) must separate cast logic from spawned entity behaviour — the action class is responsible up to and including the spawn request only.
- Standard wall jump and wall latch are distinct behaviours and must remain so even though both interact with wall-slide state. Do not merge them into a single action class.
