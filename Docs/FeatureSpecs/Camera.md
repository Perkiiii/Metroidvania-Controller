# Feature Spec — Camera

**Last audited:** 2026-07-26

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

### Canonical Player Collider

`CameraLockArea`, `CameraBoundsVolume`, and `CameraOffsetArea` must only ever recognise one specific
collider as "the player": `GameCameras.TrackedPlayerCollider`, exposed for identity checks through
the static `GameCameras.IsCanonicalPlayerCollider(Collider2D)`. This is a single deterministic
lookup — `GameObject.FindWithTag("Player")` then `hero.GetComponent<Collider2D>()` on that exact
root GameObject, never a hierarchy traversal (`GetComponentInChildren`) that could resolve to
whichever child collider happens to be visited first. A Player-tagged root without its own
`Collider2D` has no canonical identity; camera volumes simply stay unregistered until one is
authored there rather than guessing a child.

Camera volumes must not fall back to a bare `other.CompareTag("Player")` or
`other.transform.root.CompareTag("Player")` check. The hero hierarchy carries other triggers that
share the Player tag or sit under the Player-tagged root without being the canonical body collider
— the `Herobox` hurtbox child, and the transient `HeroAttackModule` damage/clash hitboxes that
enable and disable every swing. A tag/hierarchy-based check treats every one of those as "the
player" too: enabling or disabling a temporary attack hitbox while it overlaps a lock or bounds
trigger fires a genuine physics enter/exit for that collider, and because registration was keyed
only by the trigger area (not by which collider entered), the resulting exit call deregisters the
lock or bounds entirely — even though the hero never moved and its own body collider is still
inside. If the hero's own collider never generated a physics-tracked contact for that trigger (e.g.
it was granted registration through the already-positioned-player path above, as boss arenas
typically are), nothing self-heals the drop, and the camera visibly snaps to unclamped follow
framing on the very next swing. Camera volumes must key overlap recognition off
`GameCameras.IsCanonicalPlayerCollider` so a temporary hero-child collider's own enable/disable can
never register or drop a volume.

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

Readiness frames the camera from the hero's `Transform`, so the state it verifies is only
render-valid if the `Transform` already reflects the destination gate. Gate placement therefore
writes the hero `Transform` immediately (`HeroMotor.TeleportTo` sets the `Transform`, not only
`Rigidbody2D.position`, which would not propagate to the `Transform` until the next physics step).
Without this, readiness would snap and "verify" the camera against the stale pre-teleport position
(the self-consistent `RenderedPosition == CurrentDestination` check still passes), then the hero
would appear at the real gate one physics step later, forcing a large camera catch-up on reveal.

On timeout, `GameCameras` logs the destination scene and unresolved dependency, applies the safest
available direct snap to the positioned hero, clears stale camera motion/transition state, reports
that fallback in `LastSceneEntryReadiness`, and lets the transition reveal continue. A failed
transition also clears the fade in guaranteed cleanup rather than trapping the player behind black.

The transition-owned persistent hard freeze remains active across load, readiness, and the first
moments of fade-in — the player sees the framed destination while the screen is still black or just
beginning to reveal. Explicit hidden immediate positioning is permitted while frozen. The freeze is
then released at the reveal seam (see below), not after entry motion; releasing it suppresses the
transition handle's otherwise-normal `OverrideReleased` blend because the incoming state was already
committed. If the transition fails or takes the no-entry path, the handle is instead released in
`finally`. Unrelated freeze handles remain registered and retain their normal release semantics.

Reveal sequencing: after readiness the fade-in starts while the camera is still frozen at the framed
gate. Once the fade has begun to reveal (`GameCameras.FadeAlpha` crosses a small threshold, bounded
by a frame cap so a stalled fade cannot hang entry), `SceneTransitionManager` releases the transition
freeze and *then* begins `HeroSceneEntry` motion. Handing the camera back to normal follow before the
walk-in means the camera tracks the directional entry live, so the first visible destination frame is
already framed on the hero and the camera never performs a second late correction after the walk-in
finishes. Entry motion thus begins after the destination is becoming visible rather than under a
fully black screen, without any arbitrary fixed delay.

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

## Presentation Requests (Camera Phase 3)

Phase 3 adds a source-owned **presentation** layer on top of the Phase 1 underlying framing and the
Phase 2 transition layer. It is the reusable seam for boss beats, dialogue, rewards, and scripted
sequences: those systems describe *what to frame*, never *how the camera works*.

### Ownership and layer order

`GameCameras` owns presentation registrations exactly as it owns freeze registrations — same
registry shape, same pruning rules, no new singleton. `CameraController` never chooses between
requests; it receives the one `GameCameras` selected and resolves it into framing, zoom, and a
legal destination.

The camera layer order is:

1. Base follow + room bounds + selected lock + offset areas (**underlying framing**)
2. Active presentation request
3. Free/manual mode and positioning overrides
4. Freeze
5. Scene-entry immediate positioning / transition-owned hidden setup

Consequences, all covered by tests:

- A hard freeze freezes the rendered camera **and** zoom during a presentation.
- Free/manual mode is never silently replaced; a request registers but starts no live blend.
- Lock, bounds, and offset registration keep updating beneath an active request.
- Releasing resolves toward the **current** underlying destination — never a snapshot taken when
  the request began.
- Scene entry always frames the underlying destination. `GameCameras` deselects any request before
  hidden immediate positioning, and a presentation cannot influence framing while the scene-start
  snap timer is running; a deliberately persistent request re-enters afterwards through the normal
  blend.

### Requests and handles

`GameCameras.AcquirePresentation(settings, targets, source, lifetime, priority, duration)` (also
reachable through `CameraEventService.AcquirePresentation`) returns a `CameraPresentationHandle`.
Each registration carries a unique ID, owning source, source scene, priority, an explicit monotonic
registration sequence, optional independent unscaled duration, and `Scene` (default) or
deliberately `Persistent` lifetime.

- Selection is **highest priority, then newest registration sequence** — the same rule as locks.
- `Release`/`Dispose` removes only that request; repeated release is safe; one owner can never
  release another's.
- `Update(settings[, targets[, priority]])` re-authors a request in place without changing its
  registration order, so a per-frame refresh cannot steal or lose selection. A changed priority
  re-resolves selection.
- A request that cannot resolve a focus (target-driven mode with no valid targets) is **never
  registered**, and an existing one is pruned. Destroyed Unity sources, expired durations, and
  scene-local sources whose scene unloaded are pruned in `GameCameras.Update`. Removing the
  selected request falls back to the next best; removing a non-selected one changes nothing.

### Framing modes

`CameraPresentationMode` is deliberately small:

- **`FocusTarget`** — one `Transform` plus `framingOffset`.
- **`FocusWorldPoint`** — one authored world position (the basic authored-pan case; needs no target).
- **`FrameTargets`** — the region enclosing every valid target, offset as a whole.

Hero/boss/reward specifics live in scene-side adapters that resolve Transforms and create generic
requests. No gameplay identity exists inside the camera system.

### Automatic zoom

**Zoom is applied as field of view, never by dollying the camera along Z.** Camera Z is already
load-bearing — `CameraConfig.cameraZ` is enforced projection state, `GetFrustumHalfExtents` and
`CameraInfoCache` derive half-extents from `Mathf.Abs(position.z)`, `CameraParent` owns shake
motion above the camera, and every snap path preserves Z while writing X/Y. FOV is a pure
projection parameter `CameraInfoCache` already reads live, and it also preserves the relative
scale of the 2.5D parallax layers, which a dolly would change.

Zoom is expressed as a **multiplier of the authored base viewport** (`zoom = 1` is the
`CameraConfig` framing), so it is resolution- and aspect-independent:
`fov = 2·atan(tan(baseFov/2) · zoom)`.

Per frame the controller resolves, in order:

1. Selected request and its valid targets → framed bounds (+ `framingOffset`)
2. Desired zoom — authored, or auto-fit to `bounds.extents + padding` on both axes
3. Clamp to the effective zoom limits
4. Smooth the rendered zoom and apply it as FOV
5. Resolve the room/lock legal-centre region **at the rendered half-extents**
6. Clamp the desired centre into that region
7. `SmoothDamp` the rendered centre
8. Refresh `CameraInfoCache`

Steps 4–6 deliberately clamp against the *rendered* zoom rather than the not-yet-reached target
zoom: clamping against a target the camera has not reached could reveal space outside the room
mid-blend.

Zoom limits are `CameraConfig.minZoom`/`maxZoom`, optionally overridden per request
(`overrideZoomLimits`), and then **capped by the active `CameraBoundsVolume`**: the viewport may
never grow past the authored room. That cap never drops below 1, so a room already smaller than
the authored viewport keeps its existing framing instead of being silently zoomed in. Room and lock
bounds therefore remain authoritative — a lock only constrains the camera centre, the room
constrains what may be revealed.

Stability: movement and zoom smooth independently; zoom uses asymmetric damp times (`zoomOutDampTime`
short so targets stay visible, `zoomInDampTime` longer to avoid oscillation), a velocity clamp
(`maxZoomSpeed`), and a hysteresis dead-band (`zoomHysteresis`, plus `zoomContractHysteresis` applied
only when contracting) so small target jitter cannot pump the zoom. Hysteresis applies only to auto
zoom, so authored zoom and the return to 1 on release are exact.

Padding is **additive**: effective padding = `CameraConfig.presentationPadding*` + the request's
own padding. Zero on a request therefore means "just the shared default".

### Blending and transition causes

`CameraTransitionCause` gained `PresentationEntered`, `PresentationChanged`, and
`PresentationReleased`, with matching shared defaults in `CameraConfig`
(`presentationEnterTransition`/`presentationChangeTransition`/`presentationReleaseTransition`). A
request may set `overrideBlend` with its own `blendIn`/`blendOut`, mirroring `CameraLockArea`'s
override pattern. No Phase 2 cause is overloaded, so diagnostics stay unambiguous.

Each request also carries a `weight` (0–1). The resolved destination is
`Lerp(underlyingDestination, presentationCentre, weight)` and the zoom target is
`Lerp(1, requestZoom, weight)`, which is how Timeline clip weight blends presentation against
underlying framing without churning handles.

### Timeline adapter

`Assets/_Project/Scripts/Camera/Timeline/` contains `CameraPresentationTrack`,
`CameraPresentationClip`, `CameraPresentationBehaviour`, `CameraPresentationMixerBehaviour`, and the
scene-side `CameraPresentationReceiver` (the track's binding type).

Timeline never touches the gameplay camera transform, the controller, or `GameCameras`. The mixer
pushes high-level data to the receiver, and the receiver owns **exactly one handle per driving
track** for the life of the graph, re-authored in place every frame. Clips author mode, targets
(as `ExposedReference<Transform>`, resolved through the playing `PlayableDirector`), world point,
framing offset, padding, auto-zoom toggle, authored zoom, zoom-limit overrides, priority, and blend
settings; `ClipCaps.Blending` enables clip ease and overlap.

Overlapping clips resolve deterministically: numeric framing values are weighted-averaged, while
discrete choices (mode, targets, zoom flags, priority) come from the highest-weighted clip, ties
going to the later track input. Aggregate clip weight becomes the request weight.

Cleanup is layered because `PlayableBehaviour` destroy callbacks are **not** guaranteed on every
teardown path (notably the Editor `Evaluate`/scrub path):

- Total clip weight reaching zero releases through the mixer's own `ProcessFrame`.
- `OnBehaviourPause` / `OnGraphStop` / `OnPlayableDestroy` release when they do fire.
- `CameraPresentationReceiver.LateUpdate` is the authoritative backstop: if its `PlayableDirector`
  is not `Playing`, every handle it owns is released. A `timeScale` pause leaves the director
  `Playing`, so pausing the game never drops a presentation.
- `OnDisable`/`OnDestroy` release everything; scene unload prunes scene-lifetime requests.

Runtime Play Mode behaviour is the contract. Editor preview is a no-op by construction —
`GameCameras.Instance` does not exist outside Play Mode, so the receiver simply acquires nothing.

### Diagnostics

All read-only, extending the existing surfaces rather than adding a second debug system:

- `CameraController`: `HasPresentationRequest`, `PresentationRequestId`, `PresentationPriority`,
  `PresentationSourceLabel`, `PresentationSettings`, `PresentationFraming` (mode, valid target
  count, framed bounds, padding, desired centre, desired/clamped zoom, min/max zoom, zoom-clamped
  and centre-clamped flags, weight), `UnderlyingDestination`, `CurrentZoom`, `TargetZoom`,
  `BaseViewportHalfHeight`.
- `GameCameras`: `ActivePresentationCount`, `SelectedPresentationId`, `GetPresentationSnapshots()`
  (id, priority, mode, owning source, lifetime, remaining duration, valid target count, target
  names, selected flag, weight).
- `CameraControllerEditor` renders all of the above plus the registered-request list below the
  existing freeze block. It mutates nothing.

### Gizmos

`CameraController.OnDrawGizmosSelected` (Play Mode, selected-only, toggled by
`drawPresentationGizmos`) draws the resolved framing region, the padded region, the desired centre,
the resolved legal centre region, the requested viewport at the clamped zoom, the actually rendered
viewport, and a label with mode/priority/source/weight/zoom/clamp state and target names.
`BossEncounterCameraPresenter` draws its boss and reward focus points when selected.

### Boss presentation adapter

`BossEncounterCameraPresenter` (`Scripts/Boss/`) is the scene-side adapter and the only place that
knows both "this encounter" and "the camera". `BossEncounterController` and
`UndeadExecutionerBehaviour` gained no camera knowledge. See
`Docs/FeatureSpecs/BossEncounters.md`.

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
- Camera Phase 3 authoring in `SampleScene4`: `UndeadExecutionerEncounter` carries
  `BossEncounterCameraPresenter`, and its child `CameraPresentation` carries a `PlayableDirector`
  (`Assets/_Project/Timelines/UndeadExecutionerIntroCamera.playable`, `playOnAwake = false`,
  wrap mode `None`) plus `CameraPresentationReceiver`, with the timeline's
  `CameraPresentationTrack` bound to that receiver and its clip's exposed target bound to the boss
  `ActorRoot`.
- A `CameraPresentationReceiver` must use `Scene` lifetime in authored room content; `Persistent`
  is rejected by the validator because such a request would outlive its own scene.
- `Tools/Project/Validate Camera Phase 3` validates the shared presentation tuning
  (finite/non-negative values, `minZoom <= maxZoom`), the persistent prefab's `CameraConfig`
  assignment, per-scene presentation adapters (missing encounter, duplicate adapters on one
  encounter, missing required boss/reward focus, a `phaseSource` that does not implement
  `IBossPresentationPhaseSource`, an intro director with no `CameraPresentationTrack`, non-finite
  or negative authored settings and durations), Timeline clip configuration (settings validity,
  unbound receiver, target-driven clips whose exposed references resolve to nothing), persistent
  receiver lifetimes, and that every `BossEncounterController` in `SampleScene4` has a presenter.
  It then runs `Tools/Project/Validate Camera Phase 1` so one action still covers the whole camera
  contract; geometry, trigger, axis, and layer rules remain that validator's responsibility.
- `Tools/Project/Validate Camera Phase 1` checks the persistent prefab, trigger/axis/layer contracts, framed-region validity, explicit look overrides/order, and viewport-inset room/lock intersection across enabled Build Settings scenes. It reports smaller-than-reference-viewport regions without changing geometry. Camera Phase 2 extended it (same menu item, kept historical name) to also validate: the shared `CameraConfig` transition settings referenced by the persistent prefab's `CameraController` are finite and non-negative, and any per-`CameraLockArea` `entryTransitionOverride`/`exitTransitionOverride` (when `useTransitionOverride` is enabled) are finite and non-negative. It does not validate boss-lock references or lock-activity defaults — those remain `BossEncounterValidator`'s responsibility.

---

## Rules

- Camera systems may read the hero `Transform`; they must not reference `HeroController`, `HeroStateBlackboard`, or other hero subsystems.
- Camera systems must not reference boss or enemy subsystems either. Presentation requests carry
  only `Transform`s and authored data; encounter-specific knowledge lives in scene-side adapters.
- Timeline may request presentation; it must never animate the gameplay camera transform, and it is
  never a gameplay-completion authority.
- Zoom is field of view only. Do not dolly the gameplay camera along Z.
- Do not add tk2d or PlayMaker dependencies.
- Do not call `Animator` APIs from the camera system.
- Room bounds go through `CameraBoundsVolume`; temporary locks go through `CameraLockArea`.
- Scene transitions await `GameCameras` readiness while black; readiness snaps `CameraTarget` to the repositioned hero before snapping and verifying `CameraController`.

## Automated Test Coverage

- `CameraPhaseThreeTests` (EditMode) — request ownership (acquire/release, duplicate release,
  cross-owner isolation, priority and newest-entry selection, non-selected removal, selected-removal
  fallback, destroyed source/target pruning, scene-lifetime cleanup, explicit persistence, timed
  expiry, in-place update ordering), camera-layer interaction (locks and bounds live under a
  request, release returns to the *current* lock, hard-freeze suppression, freeze-release resume,
  free-mode non-replacement, scene-entry isolation, `SceneInit` clearing, presentation-specific
  transition causes, per-request blend override), and framing/zoom (single target, world point,
  two- and multi-target bounds, padding, shared+request padding additivity, min/max clamps, room
  zoom cap, small-room no-zoom-in rule, legal-region centre clamp, smaller-than-viewport collapse,
  jitter hysteresis, FOV-only zoom with unchanged Z, zoom return on release, weight blending,
  underlying-destination reporting, snapshot diagnostics, finite/non-negative validation).
- `CameraPresentationTimelineTests` (EditMode) — clip activation, no duplicate requests under
  repeated evaluation or scrubbing, release past the clip, director stop, graph destruction,
  playing-director retention, receiver disable, scene unload, deterministic overlap resolution,
  clip ease driving weight, missing binding, and unresolved exposed targets.
- `BossEncounterCameraPresenterTests` (EditMode) — intro acquisition, activation handover to
  hero+boss framing, self-expiring phase focus falling back to combat framing, defeat focus, reward
  reveal and its expiry, hero death, component disable, unload cleanup, arena-lock authority, failed
  start, and non-interference with persistence and participant completion.
- `CameraPresentationPlayModeTests` (PlayMode) — runtime-only behaviour: zoom smoothing across real
  frames and settling, FOV derived from the base FOV, zoom frozen under a hard freeze and resuming
  on release, a genuinely playing `PlayableDirector` acquiring exactly one request and releasing it
  on stop, and receiver destruction releasing.
- `CameraLifecyclePlayModeTests` (PlayMode) — lock/bounds registration lifecycle against real
  Physics2D triggers, including the canonical-player-collider contract: a temporary hero-child
  trigger collider (mirroring an authored attack damage/clash hitbox) enabling and disabling while
  overlapping an active lock or bounds volume must not itself register, drop, or begin a live
  transition on that volume, and repeated swing-style enable/disable cycles must not accumulate or
  drop registrations either.

## TODOs

- Camera Phase 2 (transition profiles, Scene gizmos, runtime diagnostics Inspector, extended validator) is implemented; see "Lock Transitions (Camera Phase 2)" above. Outstanding from this phase: human manual review/approval of the transition feel (arena-entry abruptness, lock-boundary naturalness, freeze-release smoothness — automated tests only cover cause selection, lifecycle, and legal-region correctness, not subjective feel) and a decision on whether `SampleScene4`'s `ArenaCameraLock` smaller-than-viewport framing is the desired fixed-centre boss camera or should be retuned.
- Camera Phase 3 (presentation requests, focus/pan, multi-target framing, automatic zoom, Timeline
  adapter, boss presentation adapter, diagnostics, gizmos, validator, tests) is implemented. Outstanding:
  - **Human feel review is required and has not happened.** Nothing about boss-intro framing, arena
    combat zoom, zoom stability, phase-transition focus, boss-death focus, reward reveal, return to
    normal framing, or behaviour at non-16:9 aspect ratios has been played or approved.
  - **`SampleScene4` framing is geometrically constrained, and this is a design decision, not a bug.**
    `ArenaCameraLock` is 24.5 × 14.5 while the authored viewport is ≈28.8 × 16.2, so the legal camera
    centre collapses to a fixed point and boss presentation currently expresses itself almost entirely
    as *zoom*, not as pans. Separately, `CameraBounds_SampleScene4` (37.5 × 17.39) caps zoom-out at
    ≈1.07. Auto zoom-in during combat (down to 0.85) does make the lock larger than the viewport on Y
    and re-enables some vertical movement. Enlarging the arena lock and/or the room is the lever if
    more dramatic framing is wanted; that is the same open decision already recorded for Phase 2.
- Add authored slide/super-move camera signals when those hero states exist.
- Add world-position distance filtering for camera shake requests if offscreen impact effects become noisy.
- Add render hooks or capture-to-texture only when a specific vertical-slice presentation feature needs them.
