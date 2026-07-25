# Feature Spec — Camera

**Last audited:** 2026-07-25

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

`CameraTarget` owns follow intent, look-ahead, dash offset, offset areas, vertical framing, fall catcher behaviour, and target smoothing. It does not clamp lock geometry. `HeroCameraSignalBridge` owns manual look eligibility and hold timing, while `CameraController` is the sole owner of viewport-aware room/lock legal-region resolution, applying look input, temporary camera modes, positioning, and final camera smoothing.

`CameraInfoCache` caches camera position, aspect, and world half-extents once per frame after the controller moves. Other systems can query `CameraInfoCache.WorldRect`, `HalfWidth`, and `HalfHeight` without recalculating projection math.

---

## Bounds And Lock Zones

`CameraBoundsVolume` is a scene trigger volume sized to the legal room or level travel area. The controller clamps the perspective camera using frustum half-extents at the gameplay plane, so the camera does not reveal outside the volume.

`CameraLockArea` is a trigger volume for temporary hard locks. It raises lock enter/exit events through `CameraEventService`. Each first valid entry creates one registration containing the area, its priority, an explicit monotonic entry sequence, and its source scene. Duplicate entry is idempotent and never refreshes the sequence. The selected registration is the highest numeric priority, then the newest explicit entry sequence. Removing the selected area restores the best remaining registration; removing a non-selected area does not disturb the current selection. Disable, destruction, scene unload, and scene rebind remove only the affected scene registrations.

Lock axes are independent. X-only uses the lock on X and room follow on Y; Y-only does the reverse; XY owns both. `CameraController` first insets room and lock world regions by the current perspective-frustum half-extents. On each lock-owned axis it uses the intersection of the room-centre and lock-centre intervals; other axes use the room interval. A smaller-than-viewport interval collapses to its authored centre. An empty room/lock intersection collapses to the nearest legal room point, so malformed lock geometry cannot move the camera outside the room or create NaN/oscillation.

Look prevention and numeric look limits are separate authoring choices. `overrideLookYMin` / `overrideLookYMax` distinguish an unassigned limit from the valid world value `0`. When no numeric override is enabled, prevention uses the resolved legal-region edge. Minimum greater than maximum is invalid authoring.

`CameraOffsetArea` is a trigger volume for soft room framing. It raises offset enter/exit events through `CameraEventService`. Active offset areas are stack-based; their offsets are summed and clamped by `CameraConfig.maxCombinedOffsetAreaMagnitude`.

Bounds, locks, and offset areas must be authored in scene space with `BoxCollider2D` triggers.

`GameCameras` binds the current player collider during scene initialization and refreshes overlaps against the enabled-area registries before the camera snap/fade-in path. Enabling an ordinary lock or room bounds around an already-positioned player therefore registers immediately. Trigger enter/stay remains a physics safety net. Boss encounters may also raise explicit entry after enabling their inactive arena lock; duplicate registration remains idempotent.

---

## Destination Scene Readiness

`SceneTransitionManager` keeps the persistent fade fully black after destination activation until
`GameCameras.RebindAndPositionForSceneEntry` reports completion. Readiness is dependency-driven:
the positioned `Player` hero and its collider must exist, `CameraTarget` must bind to that hero,
enabled `CameraBoundsVolume` and ordinary `CameraLockArea` overlaps must be refreshed, and
`CameraController` must apply and verify the resolved legal destination.

Scene-entry application is always the hidden `Immediate` / `SceneStart` path. It snaps
`CameraTarget`, positions the rendered camera, clears both target/controller smoothing velocity,
clears stale live transition state, and consumes the ordinary scene-start snap timer while the
screen is black. No live follow-to-lock transition is replayed on reveal. `Physics2D.SyncTransforms`
updates teleport overlap geometry in the same hidden frame; no arbitrary `FixedUpdate` or fixed
black-screen delay is required. The coroutine may yield one frame only while a late scene
dependency finishes enabling, and has a 0.5-second unscaled timeout.

On timeout, `GameCameras` logs the destination scene and unresolved dependency, applies the safest
available direct snap to the positioned hero, clears stale camera motion/transition state, reports
that fallback in `LastSceneEntryReadiness`, and lets the transition reveal continue. A failed
transition also clears the fade in guaranteed cleanup rather than trapping the player behind black.

The transition-owned persistent hard freeze remains active across load, readiness, fade-in, and
entry motion. Explicit hidden immediate positioning is permitted while frozen. Its handle alone is
released in `SceneTransitionManager` cleanup; that release suppresses the transition handle's
otherwise-normal `OverrideReleased` blend because the incoming state was already committed.
Unrelated freeze handles remain registered and retain their normal release semantics.

Fade-in and `HeroSceneEntry` motion begin only after readiness, in the same reveal frame. Thus the
first visible destination frame is already framed, while directional entry motion remains visible
and owned by the hero subsystem.

---

## Underlying Framing And Temporary Requests

Underlying framing is hero follow + current room bounds + the selected lock + offset areas. Freeze and free/manual mode are temporary overrides layered above it. Lock entry, exit, disable, fallback, and scene cleanup continue updating underlying framing while an override is active. Releasing the final freeze or ending free mode resolves the current underlying lock; it never restores a stale snapshot from when the override began.

Freeze acquisition returns a `CameraRequestHandle`. Each registration has a unique ID, source, hard/soft kind, optional independent unscaled duration, and `Scene` or deliberately `Persistent` lifetime. Releasing or disposing a handle removes only that request, and repeated release is safe. The camera remains frozen while any registration remains; any hard registration additionally freezes target intent. Timed expiry removes only its own registration. Destroyed Unity sources and unloaded scene-local sources are pruned defensively.

`SceneTransitionManager` owns one persistent hard-freeze handle across the outgoing/incoming scene boundary and releases it in `finally`. `HeroCameraAnimancerBridge` owns its current animation-event handle and releases it on the matching event or disable. Rebind preserves applicable persistent requests and removes scene-local camera registrations.

---

## Lock Transitions (Camera Phase 2)

`CameraController` owns a small data-driven transition layer on top of the existing damped-follow smoothing. It does not replace `Vector3.SmoothDamp`-based movement with a tween/duration system; a transition only selects which damp-time values `LateUpdate` blends toward and for how long.

`CameraTransitionCause` is one of `SceneStart`, `FollowToLock`, `LockToLock`, `LockToFollow`, or `OverrideReleased`. `CameraTransitionSettings` (`dampTimeX`, `dampTimeY`, `blendDuration`, `resetVelocity`, `applyImmediate`) is a small serializable struct. `CameraConfig` owns one shared default per cause (`sceneStartTransition`, `followToLockTransition`, `lockToLockTransition`, `lockToFollowTransition`, `overrideReleasedTransition`); `CameraController` keeps matching serialized fallback fields so older prefabs without a `CameraConfig` reference still behave sensibly.

- **Scene start / hidden rebind** applies immediately: `SceneInit`, `RebindAndPositionForSceneEntry`, `SnapToTarget`, and `PositionToHero` bypass the live blend entirely, reset `SmoothDamp` velocity, consume the scene-start snap timer, and clear any in-progress transition. Nothing replays after fade-in.
- **Follow → lock** (no lock selected, then one becomes selected), **lock → lock** (the selected lock changes to a different one), and **lock → follow** (the selected lock becomes none) each select their configured settings and blend `currentDampX`/`currentDampY` from the transition's starting damp values toward the normal state-driven target over `blendDuration`. The move always resolves from the camera's current rendered position — nothing is snapped or replayed.
- **Override release**: when the final active freeze (`CameraController.ApplyFreezeState`) or free mode (`EndFreeMode`) ends, the controller begins an `OverrideReleased` transition toward whatever lock is currently selected (which may have changed while frozen — lock/bounds registration keeps updating underneath an override; only the live blend is suppressed). It never restores a stale pre-freeze snapshot.
- Live transitions are only started when `startTimer <= 0` and no freeze/free/positioning override is active; lock registration itself (`EnterLockArea`/`ExitLockArea`/`ClearSceneRegistrations`) always runs regardless, so underlying framing stays correct even while a transition is suppressed.
- Duplicate lock entry, and removing a non-active lock, never touch transition state (both are no-ops before `RefreshActiveLock` is reached, exactly as in Phase 1). Removing the active lock and resolving a fallback (or none) begins the matching transition.
- A `CameraLockArea` may set `useTransitionOverride` with its own `entryTransitionOverride`/`exitTransitionOverride` in place of the shared `CameraConfig` defaults. Missing/disabled overrides fall back to the shared defaults; no area is required to reference a profile asset.

**Diagnostics** (read-only, no new ownership): `CameraController.CurrentTransitionCause`, `IsTransitioning`, `TransitionSourceLockArea`/`TransitionDestinationLockArea`, `TransitionElapsed`/`TransitionDuration`/`TransitionProgress`, `LastApplicationWasImmediate`, `CurrentDampTimeX`/`Y`, `CurrentDestination`, `RenderedPosition`, `GetRegisteredLockAreasSorted()` (locks in current selection order), and `GetLegalRegion()` (the resolved room/lock legal-centre interval per axis, or unconstrained). `GameCameras.GetFreezeSnapshots()` exposes freeze kind/source/remaining-duration per active registration for the same purpose. None of this is a second source of truth — lock selection remains owned by the Phase 1 registration model and freeze ownership remains handle-based.

`CameraControllerEditor` (custom Inspector) appends a read-only Play Mode diagnostics block below the default Inspector using the values above. `CameraLockArea`/`CameraBoundsVolume` gained Scene-view gizmos: an always-on outline (orange for locks, cyan/green for bounds, brighter when currently selected in Play Mode) and a selected-only detail view showing the framed region, the resolved camera-centre region after viewport inset (collapsing to a line/point when smaller than the viewport, matching runtime), and a label with priority/axes/override/warning state. Gizmos use `CameraGizmoUtility`, which prefers the live `CameraInfoCache` frustum in Play Mode and otherwise approximates from any `CameraConfig` asset found in the project (or hardcoded FOV 24 / Z -38.1 defaults), clearly marked "(approx. viewport)" when not live.

---

## Fades And Shake

`CameraFade` drives a `CanvasGroup` with unscaled time so fades continue while the game is paused or during transition time-scale changes. Standard `FadeOut(float)` / `FadeIn(float)` calls still use serialized fallback durations and built-in curves. Scene transitions resolve an optional `FadeProfile` through `SceneTransitionManager`: explicit request override first, transition-kind default second, then `CameraFade` defaults. Normal gates do not expose fade fields.

Camera shake is routed through `CameraEventService` and `ICameraShakeService`. `CameraShakeCueService` is the current MM Feel implementation. It can play optional `MMF_Player` presets when assigned, otherwise it falls back to More Mountains camera shake events. The shaker should move `CameraParent`, leaving `MainCamera` free to own follow position.

`CameraParent` must have `MMCameraShaker` enabled on Int channel `0`, and its required `MMWiggle` must keep Position Active enabled. `MMCameraShaker` triggers `MMWiggle.WigglePosition`; it does not make the shake visible if position wiggle is disabled.

`HeroCameraAnimancerBridge` is an optional animation-event bridge. Animation events should call high-level bridge methods such as `RequestSmallShake`, `RequestMediumShake`, `RequestHardFreeze`, or `ReleaseFreeze`; clips should not move camera transforms directly. Its freeze methods own one exact handle and cannot release another requester's freeze.

## Unity Setup Notes

- `_GameCameras` must keep references to `CameraController`, `CameraTarget`, `CameraFade`, `CameraShakeCueService`, and `HUDCamera`.
- `CameraController` and `CameraTarget` should reference `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.
- `CameraShakeCueService` can be left with no `MMF_Player` fields assigned; it will use MM camera shake events. Assign optional small/medium/intense `MMF_Player` presets later if desired.
- `CameraParent` should keep `MMWiggle.PositionActive` enabled, with Position Wiggle permitted off while idle. Shake requests will temporarily permit and time-limit the wiggle.
- Add `HeroCameraAnimancerBridge` to the hero only if animation events need camera requests.
- Lock, offset, and bounds volumes need `BoxCollider2D` triggers and must overlap the `Player` tagged hero.
- Camera lock/bounds trigger objects must use a layer excluded from `HeroConfig.terrainLayers`; current `SampleScene4` authoring uses `Ignore Raycast`.
- `Tools/Project/Validate Camera Phase 1` checks the persistent prefab, trigger/axis/layer contracts, framed-region validity, explicit look overrides/order, and viewport-inset room/lock intersection across enabled Build Settings scenes. It reports smaller-than-reference-viewport regions without changing geometry. Camera Phase 2 extended it (same menu item, kept historical name) to also validate: the shared `CameraConfig` transition settings referenced by the persistent prefab's `CameraController` are finite and non-negative, and any per-`CameraLockArea` `entryTransitionOverride`/`exitTransitionOverride` (when `useTransitionOverride` is enabled) are finite and non-negative. It does not validate boss-lock references or lock-activity defaults — those remain `BossEncounterValidator`'s responsibility.

---

## Rules

- Camera systems may read the hero `Transform`; they must not reference `HeroController`, `HeroStateBlackboard`, or other hero subsystems.
- Do not add tk2d or PlayMaker dependencies.
- Do not call `Animator` APIs from the camera system.
- Room bounds go through `CameraBoundsVolume`; temporary locks go through `CameraLockArea`.
- Scene transitions await `GameCameras` readiness while black; readiness snaps `CameraTarget` to the repositioned hero before snapping and verifying `CameraController`.

## TODOs

- Camera Phase 2 (transition profiles, Scene gizmos, runtime diagnostics Inspector, extended validator) is implemented; see "Lock Transitions (Camera Phase 2)" above. Outstanding from this phase: human manual review/approval of the transition feel (arena-entry abruptness, lock-boundary naturalness, freeze-release smoothness — automated tests only cover cause selection, lifecycle, and legal-region correctness, not subjective feel) and a decision on whether `SampleScene4`'s `ArenaCameraLock` smaller-than-viewport framing is the desired fixed-centre boss camera or should be retuned.
- Phase 3 (deferred): temporary focus/pan handles, boss-intro/phase-transition/reward camera focus, a Timeline adapter, automatic zoom, dynamic hero/boss or multi-target framing, and Cinemachine. None of these are implemented.
- Add authored slide/super-move camera signals when those hero states exist.
- Add world-position distance filtering for camera shake requests if offscreen impact effects become noisy.
- Add render hooks or capture-to-texture only when a specific vertical-slice presentation feature needs them.
