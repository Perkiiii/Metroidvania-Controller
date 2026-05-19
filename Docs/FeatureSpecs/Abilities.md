# Feature Spec — Abilities

**Last audited:** 2026-05-19

## Responsibilities

Gated traversal and combat abilities that the player earns through progression. Abilities extend the hero's move set without modifying core controller logic. The baseline movement — run, jump, wall-slide — should stay fast and readable; abilities layer expressiveness and depth on top of that foundation.

---

## Current State

No ability-unlock system exists yet. The following are planned or in progress:

| Ability | Status | Notes |
|---|---|---|
| Dash | Implemented (always available) | `HeroDashAction`; no unlock gate yet |
| Sprint | Planned | Hold input to increase grounded move speed |
| Wall-slide | Implemented (always available) | `HeroWallSlideAction` |
| Wall-jump | **Implemented** | `HeroWallJumpAction`; wired in `HeroActionController`; no unlock gate yet (PlayerAbilityState not created) |
| Wall latch / aimed wall launch | Planned | Hold jump to latch, aim, and launch off wall |
| Spirit cast | Planned | Forward projectile ability |
| Air dash / double-jump | Not started | — |
| Directional attack variants | Partial | Up/Down/Side all present; gating not implemented |

---

## Intended Architecture

### Ability Unlock State
- Store unlocked ability flags on a `PlayerAbilityState` ScriptableObject.
- Each Action class checks the relevant flag in its `CanStart` condition before proceeding.
- `HeroConfig` holds per-ability tuning values; the unlock flag is separate from tuning.
- Always-available abilities (dash, wall-slide) can be represented in the same system if desired — simply default the relevant flag to true. Unlock state should remain data-driven regardless.

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
- Gate: check `PlayerAbilityState.wallJumpUnlocked` before allowing.

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
8. Add tuning values to `HeroConfig`; keep unlock flags on `PlayerAbilityState`.

---

## Dependencies

- `HeroActionController` — hosts and ticks all action instances
- `HeroMotor` — receives velocity commands and movement modifier signals
- `HeroStateBlackboard` — shared action state flags
- `HeroConfig` — per-ability tuning values
- `PlayerAbilityState` (TODO) — unlock flags SO
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
