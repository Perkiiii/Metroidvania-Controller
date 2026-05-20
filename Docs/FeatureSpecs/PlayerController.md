# Feature Spec — Player Controller

**Last audited:** 2026-05-20

## Responsibilities

`HeroController` is the root coordinator for the hero character. It owns nothing directly except the `HeroConfig` and `HeroAnimationLibrary` references; all logic lives in the subsystems it creates and ticks.

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

## Hero Hurtbox

- `HeroHealthComponent` stays on the Hero root and owns health, i-frames, damage events, and death events.
- `HeroBox` lives on the `Hero/Herobox` child and marks the collider that can receive enemy contact damage, instant-death hazards, and recoverable local hazard damage.
- Hero attack polygons, sensors, VFX, and other child colliders must not have `HeroBox`; they should not be treated as the hero body.

---

## Extension Points

- **New traversal action** — create a `Hero<Name>Action` plain C# class, instantiate it in `HeroActionController.Initialize`, and tick it in `Tick` / `FixedTick`.
- **New blackboard field** — add to `HeroStateBlackboard`; it is inspector-visible by default (Odin Inspector available).
- **New tuning parameter** — add to `HeroConfig`; inject via existing `Initialize` signatures (no new wiring required if the subsystem already holds a config reference).

---

## Rules

- Do not access `HeroInputReader` from any class other than `HeroActionController` and the action objects.
- Do not read `Rigidbody2D.linearVelocity` outside of `HeroMotor`; use `blackboard.velocity` or `HeroMotor.Velocity`.
- Action objects (HeroJumpAction, etc.) are plain C# classes — they must not hold `MonoBehaviour` references beyond what is passed in their constructor.
