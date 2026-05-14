# Feature Spec — Camera

## Responsibilities

Follow the hero through the world; frame combat and traversal readably; respect room/area boundaries.

---

## Current State

No camera system exists. The `SampleScene` uses a default Unity camera. This spec describes the intended design.

---

## Design Goals

- Tight, responsive follow with a configurable horizontal lead in the facing direction
- Vertical tracking with a lookahead bias when falling
- Hard-locked to room/area bounds — never shows void outside designed geometry
- Fully decoupled from `HeroController` — reads the hero's transform only, not any hero component

---

## Intended Architecture

```
CameraController (MonoBehaviour on the Camera GameObject)
├── target: Transform          (hero transform — assigned in scene, not via HeroController)
├── CameraConfig (SO)          (all tuning: follow speed, lead distance, bounds, etc.)
└── room bounds: Collider2D[]  (or a dedicated CameraZone component per room)
```

`CameraController` should not reference `HeroController`, `HeroStateBlackboard`, or any hero subsystem. It observes the target position only.

### Follow Behaviour

- Track `target.position` with a lerp/slerp using a configurable `followSpeed`.
- Apply a horizontal offset (`leadDistance`) in the direction the hero is moving (or facing).
- Optionally apply a vertical downward bias when the hero is falling fast.
- Clamp the final camera position to the active room's bounds, accounting for viewport half-extents.

### Room/Zone System

- Each room defines a `CameraZone` (simple trigger volume) that carries a bounds rect or a reference to a confiner collider.
- When the hero enters a zone, `CameraController` transitions to that zone's bounds with a short lerp.
- TODO: decide whether to use Cinemachine confiner or a hand-rolled zone system.

---

## Dependencies

- Hero `Transform` (read only, no hero component reference)
- `CameraConfig` ScriptableObject (tuning)
- Room geometry / `CameraZone` components (scene-authored)

---

## Extension Points

- **Cutscene camera** — add a second camera or a `CameraController.OverrideTarget(Transform, float duration)` method; use `HeroController.AddControlLock` in parallel to freeze hero input.
- **Split-screen** — out of scope for this project.

---

## Rules

- `CameraController` must not `GetComponent<HeroController>` or reference any hero subsystem.
- All camera tuning lives in `CameraConfig` SO — no magic numbers in code.
- The camera system must compile and function independently of any hero feature being in or out of the scene.
