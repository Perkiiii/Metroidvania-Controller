# Feature Spec — Player Controller

**Last audited:** 2026-05-20

## Responsibilities

`HeroController` is the root coordinator for the hero character. It owns serialized references to tuning, animation, and persistent state assets only; all gameplay logic lives in the subsystems it creates and ticks.

Subsystem responsibilities:

| Class | Job |
|---|---|
| `HeroInputReader` | Translate Unity Input System events into typed per-frame signals |
| `HeroSensors` | Raycast-based environment probes; write results to blackboard |
| `HeroMotor` | Apply horizontal velocity, gravity scale, jump velocity to `Rigidbody2D` |
| `HeroActionController` | Orchestrate action timing; delegate to action objects |
| `HeroAnimationController` | Select and cross-fade animation clips based on blackboard |
| `HeroStateBlackboard` | Shared runtime state — written by subsystems, read by all |
| `HeroBox` | Explicit child hurtbox that routes enemy contact, instant-death hazards, and recoverable local hazard damage |

---

## Dependencies

- `Rigidbody2D` and `Collider2D` — required components (`[RequireComponent]` enforced)
- `AnimancerComponent` — added automatically if not present
- `HeroConfig` (ScriptableObject) — all tuning parameters
- `PlayerHealthState` / `PlayerResourceState` (ScriptableObjects) — persistent values passed to the health facade and actions
- `PlayerResourceConfig` (ScriptableObject) — temporary Bind cost, duration, and normal-health heal tuning
- `HeroAnimationLibrary` (ScriptableObject) — animation clip references
- Unity Input System — `InputActionAsset` at `Assets/_Project/Input/InputSystem_Actions.inputactions`

---

## Blackboard Fields

`HeroStateBlackboard` is the communication bus. Key fields:

| Field | Set by | Read by |
|---|---|---|
| `grounded`, `wasGrounded` | Sensors | Motor, Actions, Animation |
| `touchingWallFront/Back` | Sensors | WallSlideAction |
| `touchingCeiling` | Sensors | JumpAction |
| `controlLocked` | HeroController (lock set) | ActionController, Motor |
| `inputBlocked` | (TODO: cutscene/menu system) | ActionController |
| `dashing` | DashAction | AttackAction, Motor, Animation |
| `attacking`, `attackDirection` | AttackAction | Animation |
| `wallSliding` | WallSlideAction | Motor, Animation |
| `ledgeClimbing` | LedgeClimbAction | ActionController, Motor, Animation |
| `jumping`, `jumpSustaining` | Motor | JumpAction, Animation |
| `facingRight`, `FacingDirection` | Motor | AttackAction, Sensors, Motor visuals |
| `velocity` | Motor | Animation |

---

## Movement Rules

- Horizontal target speed is `walkSpeed` or `runSpeed` depending on `requireSprintForRun` and sprint input.
- Acceleration/deceleration rates differ for grounded and airborne states (four values in `HeroConfig`).
- Facing direction is set by the horizontal input sign; it persists when input is zero.
- Facing is applied via `SpriteRenderer.flipX` (preferred) or `spriteRoot.localScale.x` fallback.

---

## Control Lock

- Any system can suppress all player input by calling `HeroController.AddControlLock(source)`.
- The lock is reference-counted via a `HashSet<object>`. Input is restored only when all locks are removed.
- Cutscene and hit-stun systems should use this rather than disabling the component.

---

## Scene Entry

`HeroSceneEntry` owns destination-gate placement, facing, control locking, and directional entry
motion. Placement remains synchronous and routes through `HeroMotor` while the screen is black.
`HeroMotor.TeleportTo` writes the hero `Transform` immediately (not only `Rigidbody2D.position`), so
the same-frame scene-entry camera readiness that runs right after placement frames the true gate
rather than the stale pre-teleport position.
`SceneTransitionManager` does not start `BeginSceneEntryMotion` until `GameCameras` reports that the
positioned hero, incoming camera geometry, target, and rendered camera are ready. Entry motion then
begins once the fade-in has started to reveal the destination (not while fully black), after the
transition freeze has been handed back to normal follow, so the camera tracks the walk-in live and
the movement is visible without a late camera correction.

`HeroMotor` remains the only authority that writes scripted-entry `Rigidbody2D` velocity. Camera
readiness changes no run acceleration, top speed, entry distance, duration, or supported direction.
`HeroSceneEntry` retains its own control-lock and guaranteed motor cleanup contract.

---

## Ledge Climb

`HeroLedgeClimbAction` is an always-available plain-C# action. It is evaluated before wall-slide
entry, but it cannot start from an established wall slide or while grounded. A horizontal dash may
supply approach intent; a successful candidate ends dash through `HeroDashAction` ownership before
the ledge motor mode begins.

`HeroSensors` owns strict Terrain-only geometry validation and returns target-local Catch, Crest,
and Standing positions under an explicit `TargetFrame`. `HeroMotor` alone suppresses normal
movement, suspends gravity, advances the code-timed phases, and performs exact final placement.
Animation is optional presentation. See `Docs/FeatureSpecs/LedgeClimb.md`.

---

## Hero Hurtbox

- `PlayerHealthState` is the sole owner of current, maximum, and bonus health. `HeroHealthComponent` stays on the Hero root as the scene-side facade for i-frames, damage/hazard context, and death events; `HeroController` injects the state asset during initialization without resetting it.
- `HeroBox` lives on the `Hero/Herobox` child and marks the collider that can receive enemy contact damage, instant-death hazards, and recoverable local hazard damage.
- Hero attack polygons, sensors, VFX, and other child colliders must not have `HeroBox`; they should not be treated as the hero body.

---

## Extension Points

- **New traversal action** — create a `Hero<Name>Action` plain C# class, instantiate it in `HeroActionController.Initialize`, and tick it in `Tick` / `FixedTick`.
- **Bind action** — `HeroBindAction` is a plain C# action owned by `HeroActionController`. It is grounded-only, suppresses voluntary movement through the blackboard/motor path, and spends resource plus heals normal health only once after the configured uninterrupted hold completes. Input release, damage, hazards, death, transitions, control locks, loss of ground, and incompatible actions cancel without mutation.
- **New blackboard field** — add to `HeroStateBlackboard`; it is inspector-visible by default (Odin Inspector available).
- **New tuning parameter** — add to `HeroConfig`; inject via existing `Initialize` signatures (no new wiring required if the subsystem already holds a config reference).

---

## Rules

- Do not access `HeroInputReader` from any class other than `HeroActionController` and the action objects.
- Do not read `Rigidbody2D.linearVelocity` outside of `HeroMotor`; use `blackboard.velocity` or `HeroMotor.Velocity`.
- Action objects (HeroJumpAction, etc.) are plain C# classes — they must not hold `MonoBehaviour` references beyond what is passed in their constructor.
