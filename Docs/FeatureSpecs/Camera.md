# Feature Spec — Camera

**Last audited:** 2026-05-19

## Responsibilities

Follow the hero through the world, frame combat and traversal readably, support room bounds and lock zones, and provide camera fades and shake routing.

---

## Current Architecture

```
_GameCameras (DontDestroyOnLoad prefab)
├── GameCameras              persistent singleton and scene-init router
├── CameraEventService       static request/event surface for camera signals
├── CameraShakeCueService    named shake cue router backed by MM Feel
├── CameraFade               CanvasGroup fade driver
├── CameraParent             shake target
│   └── MainCamera
│       └── CameraController perspective follow, smoothing, bounds, lock zones
├── HUDCamera                orthographic UI camera
├── FadeCanvas               screen-space camera canvas for fades
└── CameraTarget             smoothed hero target point
```

`Bootstrap` instantiates `_GameCameras`. `GameManager.SceneInit` calls `GameCameras`, which initializes `CameraTarget` before `CameraController`. The camera system intentionally does not use tk2d or PlayMaker.

`CameraEventService` is the replacement for PlayMaker-style camera messages. Trigger volumes, animation bridges, and gameplay helpers raise high-level requests; `GameCameras` routes them to the current target, controller, fade, or shake service.

---

## Tuning

All camera tuning belongs in `CameraConfig` at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.

The runtime components keep serialized fallback values so older prefabs do not break, but `_GameCameras` should reference the shared `CameraConfig` asset.

Important defaults:

- Main camera projection: perspective, FOV `24`, camera local Z `-38.1`
- Target damp: normal `0.35`, slow `0.15`
- Camera damp: normal `0.32`, slow `0.15`
- Horizontal look-ahead: `0.16`
- Dash look-ahead: `2.51`
- Falling look-ahead: `1.25`
- Dash lead threshold: horizontal speed above `5`
- Base vertical framing offset: `1`
- Fast-fall vertical framing offset: `-1.5`
- Max combined offset-area magnitude: `6`
- Look input offset: `6`
- Max camera velocity: `65`

---

## Follow Behaviour

`CameraTarget` reads only the hero `Transform`. It infers horizontal facing, dash lead, rising, falling, and fast-fall framing from per-frame transform deltas, with optional one-way velocity hints from the hero bridge so fall framing remains reliable across physics/update timing.

`HeroCameraSignalBridge` is a hero-side adapter. It reads hero/input state and sends one-way signals such as look up/down, facing, dash, and sprint hints into `GameCameras`. Camera scripts must not read `HeroController`, `HeroStateBlackboard`, or `HeroInputReader` directly. Until ledge/edge detection exists, manual look up/down is gated to grounded, stationary hero input.

Per frame, `CameraController.LateUpdate`:

1. Ticks `CameraTarget`.
2. Stops if frozen.
3. Snaps to target during the scene-start lock timer.
4. Smooths camera X and Y independently toward the target.
5. Uses state-aware Y damping: slower while rising, a lower forward frame while falling, and temporary slow damp while looking up/down.
6. Clamps to the active bounds volume and top-priority lock area.
7. Smooths manual look offsets back toward zero when input is released.

`CameraTarget` owns modes (`FollowHero`, `LockZone`, `Free`), look-ahead, dash offset, offset areas, vertical framing, and fall catcher behaviour. `HeroCameraSignalBridge` owns manual look eligibility and hold timing, while `CameraController` owns viewport-aware clamping, applying look input, freezes, positioning, final camera smoothing, and public camera modes (`Follow`, `Locked`, `Frozen`, `Free`).

`CameraInfoCache` caches camera position, aspect, and world half-extents once per frame after the controller moves. Other systems can query `CameraInfoCache.WorldRect`, `HalfWidth`, and `HalfHeight` without recalculating projection math.

---

## Bounds And Lock Zones

`CameraBoundsVolume` is a scene trigger volume sized to the legal room or level travel area. The controller clamps the perspective camera using frustum half-extents at the gameplay plane, so the camera does not reveal outside the volume.

`CameraLockArea` is a trigger volume for temporary hard locks. It raises lock enter/exit events through `CameraEventService`. Active lock areas are stack-based; the highest `priority` wins. Lock areas can clamp X, clamp Y, and optionally prevent look-up or look-down offsets at authored vertical limits.

`CameraOffsetArea` is a trigger volume for soft room framing. It raises offset enter/exit events through `CameraEventService`. Active offset areas are stack-based; their offsets are summed and clamped by `CameraConfig.maxCombinedOffsetAreaMagnitude`.

Bounds, locks, and offset areas must be authored in scene space with `BoxCollider2D` triggers.

---

## Fades And Shake

`CameraFade` drives a `CanvasGroup` with unscaled time so fades continue while the game is paused or during transition time-scale changes. Standard `FadeOut(float)` / `FadeIn(float)` calls still use serialized fallback durations and built-in curves. Scene transitions resolve an optional `FadeProfile` through `SceneTransitionManager`: explicit request override first, transition-kind default second, then `CameraFade` defaults. Normal gates do not expose fade fields.

Camera shake is routed through `CameraEventService` and `ICameraShakeService`. `CameraShakeCueService` is the current MM Feel implementation. It can play optional `MMF_Player` presets when assigned, otherwise it falls back to More Mountains camera shake events. The shaker should move `CameraParent`, leaving `MainCamera` free to own follow position.

`CameraParent` must have `MMCameraShaker` enabled on Int channel `0`, and its required `MMWiggle` must keep Position Active enabled. `MMCameraShaker` triggers `MMWiggle.WigglePosition`; it does not make the shake visible if position wiggle is disabled.

`HeroCameraAnimancerBridge` is an optional animation-event bridge. Animation events should call high-level bridge methods such as `RequestSmallShake`, `RequestMediumShake`, `RequestHardFreeze`, or `ReleaseFreeze`; clips should not move camera transforms directly.

## Unity Setup Notes

- `_GameCameras` must keep references to `CameraController`, `CameraTarget`, `CameraFade`, `CameraShakeCueService`, and `HUDCamera`.
- `CameraController` and `CameraTarget` should reference `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.
- `CameraShakeCueService` can be left with no `MMF_Player` fields assigned; it will use MM camera shake events. Assign optional small/medium/intense `MMF_Player` presets later if desired.
- `CameraParent` should keep `MMWiggle.PositionActive` enabled, with Position Wiggle permitted off while idle. Shake requests will temporarily permit and time-limit the wiggle.
- Add `HeroCameraAnimancerBridge` to the hero only if animation events need camera requests.
- Lock, offset, and bounds volumes need `BoxCollider2D` triggers and must overlap the `Player` tagged hero.

---

## Rules

- Camera systems may read the hero `Transform`; they must not reference `HeroController`, `HeroStateBlackboard`, or other hero subsystems.
- Do not add tk2d or PlayMaker dependencies.
- Do not call `Animator` APIs from the camera system.
- Room bounds go through `CameraBoundsVolume`; temporary locks go through `CameraLockArea`.
- Scene transitions snap `CameraTarget` to the repositioned hero before snapping `CameraController`.

## TODOs

- Add authored slide/super-move camera signals when those hero states exist.
- Add world-position distance filtering for camera shake requests if offscreen impact effects become noisy.
- Add render hooks or capture-to-texture only when a specific vertical-slice presentation feature needs them.
