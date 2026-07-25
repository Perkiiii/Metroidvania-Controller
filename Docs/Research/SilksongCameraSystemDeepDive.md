# **Silksong Camera Lock, Framing, and Boss-Arena Camera Systems Deep Dive**

> **Status:** External research and architecture review.
>
> This document is advisory, not project ground truth. The current codebase is authoritative for
> implementation status. Recommendations must be verified against the current working tree before
> implementation.

## **Executive verdict**

Silksong’s camera is a layered, production-tested system built around three persistent runtime authorities:

1. `CameraTarget` converts hero state into a smoothed target point.  
2. `CameraController` moves the rendered camera, selects active lock areas, clamps movement, and owns global modes.  
3. Scene-local `CameraLockArea` and `CameraOffsetArea` components author spatial framing.

Its strongest reusable patterns are:

* Overlapping lock areas remain registered underneath the current area.  
* Highest numeric priority wins.  
* Equal priority is resolved by most-recent entry.  
* Removing the active area restores the highest-priority remaining area.  
* Camera locks author camera-centre limits separately from their activation collider.  
* Scene-entry positioning can apply a lock immediately rather than waiting for smoothing.  
* Lock changes alter damping for an eased lock-to-lock or lock-to-follow transition.  
* Battle controllers activate and deactivate ordinary lock-area objects instead of embedding boss behavior in camera code.  
* Scene unload explicitly drains the lock collection.

Its important weaknesses are:

* Equal-priority behavior is incidental list behavior rather than an explicit contract.  
* Lock and temporary-camera operations are global mutable state, not source-owned leases.  
* Temporary panning stores only one previous mode, making overlapping overrides unsafe.  
* Destroyed/stale lock references rely heavily on Unity lifecycle ordering.  
* Camera bounds assume a fixed viewport half-size (`14.6 × 8.3`) derived from the room tilemap, rather than calculating the current viewport.  
* Shake requests store a source but cancellation is profile-based, not source-based.  
* `CameraOffsetArea` registration exists, but the supplied `CameraTarget.Update` does not visibly apply the returned offset to its normal destination.  
* Complete boss-room framing behavior remains partly hidden in absent scenes, prefabs, and PlayMaker graphs.

Underbrew should not copy Silksong wholesale. Its persistent `GameCameras`, typed `CameraEventService`, explicit boss request source, viewport-aware perspective clamping, and separation between boss behavior and camera internals are cleaner foundations.

Underbrew’s immediate correctness work should be:

* Make equal-priority policy explicit and test it.  
* Fix axis-specific locking: `CameraTarget` currently clamps both axes even when only one is enabled.  
* Intersect lock limits with room bounds so a lock cannot force the camera outside its room volume.  
* Prevent lock enter/exit from cancelling an active freeze or temporary override.  
* Restore the underlying lock correctly when free/focus control ends.  
* Replace the single global freeze coroutine with source-owned freeze leases.  
* Add immediate already-inside detection for ordinary lock areas, not only the boss-specific explicit request.  
* Add tests for overlap, fallback, disable, scene rebind, axis flags, viewport clamping, and temporary-state restoration.

Transition profiles, camera-centre previews, runtime stack diagnostics, and temporary focus/pan requests are recommended before serious boss presentation work. Dynamic zoom, multi-target boss framing, Timeline ownership, Cinemachine, and custom camera graph tools should remain deferred.

Evidence labels used below:

* **Direct evidence** — behavior is visible in supplied code or checked-in Unity YAML.  
* **Strong inference** — connected code implies the behavior, but required scene, prefab, FSM, or asset is unavailable.  
* **Unknown** — the supplied material cannot establish the behavior.

---

## **Silksong camera architecture map**

| Step | Owner | Data/request source | Lifetime and cleanup | Scope | Evidence |
| ----- | ----- | ----- | ----- | ----- | ----- |
| Hero motion/state | `HeroController` | Transform, facing, look, dash, fall, sprint, super-jump state | Actor lifetime | Actor-local | `Silksong_Study_ReadOnly/Assembly-CSharp/CameraTarget.cs`, `Update`; fields `heroTransform`, `hero_ctrl`. **Direct evidence** |
| Camera tracking intent | `CameraTarget` | Hero transform/state, offset areas, lock limits | Persistent through `GameCameras`; reset in `SceneInit` | Global persistent component with scene-local inputs | `CameraTarget.cs`, `SceneInit`, `Update`, `EnterLockZone`, `ExitLockZone`. **Direct evidence** |
| Lock-area activation | `CameraLockArea` via `TrackTriggerObjects` | Trigger overlap collection | Scene-local; base `OnDisable` emits exit | Scene-local | `CameraLockArea.cs`, `OnInsideStateChanged`; `TrackTriggerObjects.cs`, `OnEnable`, `OnDisable`, `GetOverlappedColliders`. **Direct evidence** |
| Active lock collection | `CameraController.lockZoneList` | `LockToArea`/`ReleaseLock` calls | Recreated in `SceneInit`; drained in `OnLevelUnload` | Persistent owner, scene-local contents | `CameraController.cs`, `SceneInit`, `LockToArea`, `ReleaseLock`, `OnLevelUnload`. **Direct evidence** |
| Active-area decision | `CameraController` | Area priority and list order | Re-evaluated on entry/removal | Global camera decision | `CameraController.cs`, `LockToArea`, `ReleaseLock`. **Direct evidence** |
| Room limits | `CameraController` | Active tilemap width/height | Refreshed per scene | Scene-derived global state | `CameraController.cs`, `GetTilemapInfo`; fields `sceneWidth`, `sceneHeight`, `xLimit`, `yLimit`. **Direct evidence** |
| Lock limits | `CameraController` and `CameraTarget` | Selected `CameraLockArea.cameraXMin/Max`, `cameraYMin/Max` | Until selected area changes | Scene-local authoring applied by global camera | `CameraController.cs`, `LockToArea`; `CameraTarget.cs`, `EnterLockZone`. **Direct evidence** |
| Look offsets and tracking | `CameraTarget` plus `CameraController` | Facing/action look-ahead; look-up/down input | Per frame | Global | `CameraTarget.cs`, `Update`; `CameraController.cs`, `LateUpdate`. **Direct evidence** |
| Final motion | `CameraController` | Target transform plus vertical look offset | Per `LateUpdate` | Global camera authority | `CameraController.cs`, `LateUpdate`, `KeepWithinSceneBounds`. **Direct evidence** |
| Temporary freeze/free/pan | `CameraController`, `CameraTarget`, PlayMaker actions, lift scripts | Global calls and FSM-state lifetime | Explicit stop/state exit; generally no source lease | Global mutable state | `CameraController.cs`, `FreezeInPlace`, `StopFreeze`, `SetMode`; PlayMaker camera actions; `LiftControl.cs`; `WeaverLift.cs`. **Direct evidence** |
| Shake | `CameraShakeManager` and `CameraManagerReference` | Profile, source, manager reference | Timed completion, profile cancellation, or scene cancellation | Global/reference-routed | `CameraShakeManager.cs`, `DoShake`, `CancelAllShakes`; `CameraManagerReference.cs`, `DoShake`, `CancelShake`. **Direct evidence** |
| Battle request | `BattleScene` | Encounter start/end | Scene-local encounter lifetime | Scene-local requester | `BattleScene.cs`, `LockInBattle`, `EndBattle`; field `camLocks`. **Direct evidence** |
| Restoration | `CameraController`/`CameraTarget` | Remaining lock list or normal hero follow | On release, free-mode exit, scene positioning, unload | Global | `CameraController.cs`, `ReleaseLock`, `DoPositionToHero`; `CameraTarget.cs`, `EndFreeMode`. **Direct evidence** |

Condensed flow:

HeroController  
  \-\> CameraTarget hero intent and action look-ahead  
  \-\> CameraController destination  
  \-\> room-bound clamp  
  \-\> camera smoothing  
  \-\> final camera transform

CameraLockArea overlap  
  \-\> CameraController.lockZoneList  
  \-\> priority \+ latest-entry selection  
  \-\> CameraTarget lock-zone limits  
  \-\> selected-area look clamps  
  \-\> fallback area or normal follow on release

BattleScene  
  \-\> activates camLocks GameObject group  
  \-\> normal CameraLockArea overlap/registration  
  \-\> disables group after completion  
  \-\> base OnDisable releases the lock  
---

## **Silksong source inventory**

Primary sources inspected:

| Exact path | Relevant classes and responsibilities | Evidence |
| ----- | ----- | ----- |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraLockArea.cs` | Scene-authored activation area, camera-centre limits, priority, look clamps | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/TrackTriggerObjects.cs` | Overlap collection, already-inside refresh, idempotent entry, disable cleanup | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraController.cs` | Active lock collection, selection, fallback, room limits, final smoothing, freeze, positioning | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraTarget.cs` | Hero tracking, action look-ahead, target smoothing, lock application, free mode | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraOffsetArea.cs` | Scene-authored offset registration | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/GameCameras.cs` | Persistent composition root and scene initialization | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/BattleScene.cs` | Battle-controlled activation/deactivation of camera-lock objects | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/BossSceneController.cs` | Special boss/challenge scene transitions; no direct camera-lock API | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/GameManager.cs` | Scene transition, death, hazard-death, and menu camera freezing | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraShakeManager.cs` | Concurrent shake evaluation, render-time offset, freeze frames, scene cleanup | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraManagerReference.cs` | Shake manager registration, looping shakes, range filtering, profile cancellation | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/CameraShakeTarget.cs` | Serialized camera/profile pairing used by requesters | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/LiftControl.cs` | Scripted Y camera movement and previous-mode restoration | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/WeaverLift.cs` | Scripted pan/teleport framing and explicit previous-mode restoration | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/SceneTransitionZoneBase.cs` | Optional camera freeze coordinated with control loss/fade | **Direct evidence** |
| `Silksong_Study_ReadOnly/Assembly-CSharp/HutongGames/PlayMaker/Actions/CameraFreezeInPlace.cs` | FSM-facing global freeze | **Direct evidence** |
| `.../CameraStopFreeze.cs` | FSM-facing global release | **Direct evidence** |
| `.../StartFreeCameraMode.cs`, `StartFreeCameraModeV2.cs`, `EndFreeCameraMode.cs` | Free-target manipulation and restoration | **Direct evidence** |
| `.../CameraFollowInState.cs`, `CameraFollowYInState.cs` | State-scoped target following/panning | **Direct evidence** |
| `.../DoCameraShakeV4.cs`, `CancelCameraShake.cs` | Profile shake requests and cancellation | **Direct evidence** |
| `.../TweenCamera.cs` | Generic FOV/orthographic-size/property tweening | **Direct evidence of capability; use by boss content unknown** |

The supplied source does not include the full camera prefab, room scenes, boss-room prefabs, or PlayMaker graphs. Exact authored values and graph sequencing are therefore partly **Unknown**.

---

## **CameraLockArea deep dive**

### **Fields**

`Silksong_Study_ReadOnly/Assembly-CSharp/CameraLockArea.cs`, class `CameraLockArea`:

| Field | Meaning | Authored/runtime |
| ----- | ----- | ----- |
| `cameraXMin`, `cameraXMax` | Legal camera-centre X range | Authored; converted to world space and repaired at runtime |
| `cameraYMin`, `cameraYMax` | Legal camera-centre Y range | Authored; converted/repaired at runtime |
| `positionSpace` | Whether limit values are world coordinates or offsets from the area transform | Authored, then changed to `Space.World` by `ValidateBounds` |
| `preventLookUp` | Enables a separate upper look clamp | Authored |
| `lookYMax` | Maximum camera-centre Y while looking up | Authored; defaults to `cameraYMax` when negative |
| `preventLookDown` | Enables a lower look clamp | Authored |
| `lookYMin` | Minimum camera-centre Y while looking down | Authored; defaults to `cameraYMin` when negative |
| `priority` | Higher numeric values override lower ones | Authored |
| `maxPriority` | Obsolete migration flag; `OnValidate` converts it to `priority = 1` | Legacy serialized data |
| `ignoreInSuperjump` | Suppresses immediate activation during `hero_ctrl.cState.superDashing` | Authored |
| `leftSideX/rightSideX/topSideY/botSideY` | Activation-collider world edges used to mark entry/exit direction | Runtime |
| `box2d` | Activation collider | Runtime cache |
| `cameraCtrl`, `camTarget`, `gcams` | Direct references to persistent camera system | Runtime |

There are no serialized `lockX` or `lockY` booleans. Axis locking is achieved through the numeric range:

X locked to a point: cameraXMin \== cameraXMax  
X allowed to follow: cameraXMin \< cameraXMax  
Equivalent rule for Y

That is **Direct evidence** from `CameraTarget.Update`, which clamps each target coordinate independently to its selected min/max. Whether scenes commonly use equality for one-axis locks is **Strong inference**, because scene assets are absent.

### **Collider requirements and inside detection**

`CameraLockArea` inherits `TrackTriggerObjects`; it does not have `[RequireComponent]`. `Awake` calls `GetComponent<Collider2D>`, so any `Collider2D` is accepted and a missing collider is tolerated for edge-direction calculations. Actual overlap discovery enumerates all colliders on the object.

`TrackTriggerObjects.OnTriggerEnter2D` filters with:

* the physics collision-layer mask derived from the area’s layer;  
* optional serialized include/exclude tag lists;  
* `ignoreLayers`;  
* paused hero-trigger state;  
* duplicate membership.

The first accepted object changes the area to inside. The last removal changes it to outside.

This means the base type is not intrinsically hero-only. `CameraLockArea.OnInsideStateChanged` nevertheless reads `HeroController.SilentInstance` and uses the hero’s position. Correct scene collision/tag setup must therefore ensure that only the hero counts. That requirement is a **Strong inference** from connected code, not an enforced type check.

### **Registration**

Exact source: `CameraLockArea.cs`, `CameraLockArea.OnInsideStateChanged`.

if (\!CameraLockArea.IsInApplicableGameState())  
    return;

cameraCtrl.LockToArea(this);

The area registers directly with `CameraController`, not through a global event service. Applicable states are `PLAYING`, `ENTERING_LEVEL`, and `CUTSCENE`.

### **Unregistration**

Exact source: same method.

if (\!isInside)  
{  
    // Record approximate exit side on CameraTarget.  
    cameraCtrl.ReleaseLock(this);  
    return;  
}

Exit is not restricted by game state, ensuring cleanup can occur during transitions.

### **Enable, disable, destruction, and already-inside behavior**

`TrackTriggerObjects.OnEnable` subscribes to `HeroController.heroInPosition`. If the hero is already positioned, it immediately performs collider overlap queries. `OnHeroInPosition` refreshes overlaps again.

Therefore an area supports a hero already inside when:

* it is enabled after the hero is positioned; or  
* the hero-position event fires after the area was enabled.

This is **Direct evidence** from `TrackTriggerObjects.OnEnable`, `OnHeroInPosition`, and `GetOverlappedColliders`.

`TrackTriggerObjects.OnDisable`:

* unsubscribes from the hero-position event;  
* sends exit notifications;  
* clears the tracked list;  
* calls `OnInsideStateChanged(false)`.

Thus disabling an active area normally releases it. Destruction normally passes through Unity’s disable lifecycle. `CameraLockArea.OnDestroy` itself only invokes `OnDestroyEvent`; that event is used by `CameraController` to remove the area from `instantLockedArea`, not from `lockZoneList`. Correct lock-list cleanup therefore relies on `OnDisable` executing before destruction. This is a Unity lifecycle assumption.

### **Idempotence and stay behavior**

`TrackTriggerObjects` rejects duplicate objects with `insideGameObjects.Contains`. It only emits inside=true when count changes from zero to one and inside=false when it reaches zero. Repeated trigger entries are therefore idempotent.

There is no `CameraLockArea.OnTriggerStay2D`; already-inside restoration is implemented by explicit overlap scans.

### **Bounds validation**

`CameraLockArea.OnEnable` validates immediately for self-space values. `StartRoutine` additionally waits until `CameraController.tilemap` belongs to the same scene, validates again, and caches collider edges.

`ValidateBounds` converts self-space coordinates to world coordinates, then replaces negative values:

cameraXMin \< 0 \-\> 14.6  
cameraXMax \< 0 \-\> scene xLimit  
cameraYMin \< 0 \-\> 8.3  
cameraYMax \< 0 \-\> scene yLimit  
lookYMin \< 0   \-\> cameraYMin  
lookYMax \< 0   \-\> cameraYMax

The numeric values are legal camera-centre limits, not room geometry and not hero bounds.

### **Direction flags**

Entry/exit compares the hero position against narrow bands around activation-collider edges:

* left/right: ±1 unit;  
* bottom: ±1 unit;  
* top: ±2 units.

It writes `enteredLeft`, `enteredRight`, `enteredTop`, `enteredBot` or corresponding exit flags on `CameraTarget`. The supplied `CameraTarget` exposes these fields, but their complete downstream effect is not visible in the inspected normal movement methods. Their broader use is therefore **Unknown**.

---

## **Active-area selection algorithm**

Collection owner: `CameraController.lockZoneList`.

### **Entry algorithm**

Exact source: `CameraController.cs`, `CameraController.LockToArea`.

if area is neither new nor current:  
    do nothing

if area is not current:  
    append area to lockZoneList

if current exists and current.priority \> area.priority:  
    keep current  
else if area is ignored during current super-jump:  
    leave it registered but do not select it  
else:  
    current \= area  
    copy and scene-clamp its camera-centre limits  
    if camera is not frozen:  
        mode \= LOCKED

    if scene-start timer requires immediate application:  
        clamp target and camera immediately  
    else:  
        CameraTarget.EnterLockZone(...)

Higher numeric priority wins.

Equal priority replaces the current area because the comparison is strictly `current.priority > new.priority`. Therefore most-recent entry wins.

This is deterministic for a fixed event order, but it is incidental rather than named as a policy.

A noteworthy edge case: `ignoreInSuperjump` is checked after the area is appended. It may later become a fallback candidate even though no explicit post-super-jump activation pass is shown.

### **Removal and fallback**

Exact source: `CameraController.cs`, `CameraController.ReleaseLock`.

remove area from lockZoneList

if removed area was not current:  
    no selection change

else if list is non-empty:  
    bestPriority \= int.MinValue  
    iterate list from newest to oldest  
    if candidate.priority \> bestPriority:  
        best \= candidate  
        bestPriority \= candidate.priority

    current \= best  
    copy/scene-clamp best bounds  
    CameraTarget.EnterLockZone(best bounds)

else:  
    remember last camera position  
    CameraTarget.ExitLockZone()  
    current \= null

    if hero alive, not hazard-dead, not leaving scene, and camera not frozen:  
        mode \= FOLLOWING

Because fallback iterates newest-to-oldest and uses strict `>`, the newest equal-priority area remains selected.

Removing a non-active area only removes it from the collection. Removing or disabling the active area restores the best remaining area. With no remaining area, normal follow resumes subject to death/transition/freeze guards.

The collection is not sorted. Selection is linear.

There is no stale-reference check inside fallback. Normal disable cleanup is expected to prevent stale entries.

---

## **Bounds and movement behavior**

### **Room bounds**

`CameraController.GetTilemapInfo` derives:

xLimit \= tilemap.width  \- 14.6  
yLimit \= tilemap.height \- 8.3

The legal camera centre is:

x ∈ \[14.6, xLimit\]  
y ∈ \[8.3,  yLimit\]

This implies a fixed full visible footprint of approximately `29.2 × 16.6` world units. The tilemap represents the room, while these limits represent legal camera-centre positions.

`KeepWithinSceneBounds` applies these hard limits to destinations. `LateUpdate` performs a second hard correction after smoothing, including `cameraParent` offset.

Dynamic orthographic size or aspect-based recalculation is not visible. Whether another component guarantees the exact viewport is **Strong inference**.

### **Lock bounds**

A `CameraLockArea` directly authors legal camera-centre limits. `CameraController.LockToArea` intersects those values with room-centre limits. `CameraTarget.Update` clamps its destination to them.

hero position  
\+ action look-ahead  
\+ movement offsets  
\-\> clamp target X to \[xLockMin, xLockMax\]  
\-\> clamp target Y to \[yLockMin, yLockMax\]  
\-\> smooth CameraTarget  
\-\> CameraController follows CameraTarget \+ manual vertical look  
\-\> scene-centre clamp

Unlike Underbrew, the activation collider and camera-centre region are separate data.

### **Look offsets**

Two distinct mechanisms exist:

1. Hero action/facing look-ahead in `CameraTarget.Update`: facing lead, dash, sprint, sliding, harpoon, updraft, super-jump, rising, falling, and umbrella framing.  
2. Manual look-up/down in `CameraController.LateUpdate`: approximately `+6` or `-6` world units.

Selected lock areas may restrict manual vertical look with `preventLookUp/lookYMax` and `preventLookDown/lookYMin`. The clamp is applied before scene clamping.

`CameraOffsetArea` registers areas in list order and `GetCameraOffset` returns the most recent area’s offset. However, in the supplied `CameraTarget.Update`, the computed offset is not visibly added to the normal destination. `PositionToStart` reads it, but largely uses it in boundary/debug calculations. Normal runtime offset behavior is therefore not proven by this C\# and may be incomplete, optimized/decompiled oddly, or rely on absent content.

### **Smoothing and transitions**

There are two smoothing layers:

* `CameraTarget` smooths hero intent independently on X/Y.  
* `CameraController` smooths the camera toward `CameraTarget`.

`CameraTarget.EnterLockZone` compares the hero’s position under the old and new lock ranges. If the clamped positions differ:

* displacement over 9 units selects `dampTimeSlower`;  
* smaller displacement selects `dampTimeSlow`;  
* `slowTimer` holds the transition damping temporarily;  
* damping gradually returns to normal afterward.

`ExitLockZone` performs equivalent old-lock versus room-range comparison.

Thus lock entry, lock-to-lock restoration, and lock-to-follow exit ease through damping. They do not use per-area transition settings or animation curves.

During the scene-start timer, `CameraController.LockToArea` may call `EnterLockZoneInstant` and snap both target and camera directly. Scene-start locks can therefore be immediate while normal traversal locks ease.

Hard room clamps are applied after smoothing, so the camera can ease toward a boundary but cannot render beyond it.

---

## **Temporary camera controls**

| Feature | Implementation | Ownership/overlap/restoration | Evidence |
| ----- | ----- | ----- | ----- |
| Freeze | `CameraController.FreezeInPlace(bool)` sets global `FROZEN`; optionally freezes target by making it `FREE` | No source, count, or priority. Any caller can stop another caller’s freeze. `StopFreeze` restores follow and optionally target/underlying lock | `CameraController.cs`, `FreezeInPlace`, `StopFreeze`; PlayMaker freeze actions. **Direct evidence** |
| Free target | `CameraTarget.StartFreeMode` stops hero tracking and exposes the target object to FSMs | One global manual-free flag. `EndFreeMode` restores hero follow and reapplies current lock | `CameraTarget.cs`, `StartFreeMode`, `EndFreeMode`; `StartFreeCameraMode*.cs`. **Direct evidence** |
| Scripted follow | `CameraFollowInState`/`CameraFollowYInState` set controller to `PANNING` and manually snap camera/target each update | State-scoped; `OnExit` uses `PREVIOUS`. Only one previous mode exists | PlayMaker action files. **Direct evidence** |
| Lift pan | Lift scripts set `PANNING`, move target Y, then select `PREVIOUS` or a captured mode | Requester owns coroutine but not a formal camera lease | `LiftControl.cs`, movement coroutine; `WeaverLift.cs`, `TeleportRoutine`. **Direct evidence** |
| Reposition to hero | Temporarily freezes, snaps/positions, waits 0.1 seconds, then restores prior mode or current lock | One controller coroutine; a new request cancels the previous one | `CameraController.cs`, `PositionToHero`, `DoPositionToHero`. **Direct evidence** |
| Zoom | Generic PlayMaker `TweenCamera` supports FOV and orthographic size | No camera-specific restoration contract; graph must provide it | `TweenCamera.cs`, `DoTween`. **Direct evidence of capability; usage unknown** |
| Shake | Multiple shake trackers add offsets and are magnitude-capped | Concurrent profiles supported. Source stored for diagnostics but cancellation is by profile, not source | `CameraShakeManager.cs`, `DoShake`, `EvaluateShakes`; `CameraManagerReference.cs`, `CancelShake`. **Direct evidence** |
| Shake/lock interaction | Shake offset is applied during render and then removed | It layers over follow/lock rather than changing lock bounds | `CameraShakeManager.cs`, `OnPreCull`, `OnPostRender`. **Direct evidence** |
| Shake scene cleanup | Scene activation cancels non-persistent shakes and blocks new finishable shakes for 0.5 seconds | Profiles may opt into `PersistThroughScenes` | `CameraShakeManager.cs`, `OnEnable`, `CancelAllShakes`. **Direct evidence** |
| Cinematic flag | `GameCameras.OnCinematicBegin/End` changes effect configuration | Does not itself own a focus/shot | `GameCameras.cs`. **Direct evidence** |

The general temporary-control model is global single-state mutation. It is not source-owned and is unsafe for arbitrary overlapping cinematic requesters. `CameraMode.PREVIOUS` only stores one preceding mode, so nested pans can restore incorrectly.

If a requester is destroyed:

* PlayMaker `OnExit` restoration is expected when the FSM state exits, but destruction behavior depends on PlayMaker lifecycle: **Strong inference**.  
* Lift coroutine abnormal destruction has no explicit camera lease cleanup in the shown method: **Unknown**.  
* Shake trackers do not automatically cancel when their `Source` becomes destroyed: **Direct evidence**.

---

## **Boss/battle integration**

### **Ordinary battle rooms**

`BattleScene` owns a `GameObject camLocks`.

At encounter start, `DoStartBattle` calls `LockInBattle` before its start pause:

// BattleScene.cs — BattleScene.LockInBattle  
camLocks.SetActive(true);  
SendEventToChildren("BG CLOSE");

This coordinates camera-lock activation with gate-close events.

At battle completion:

// BattleScene.cs — BattleScene.EndBattle  
if (camLocks \!= null && \!dontDisableCamlocksOnEnd)  
    camLocks.SetActive(false);

if (openGatesOnEnd)  
    SendEventToChildren("BG OPEN");

Disabling the lock group invokes the inherited `TrackTriggerObjects.OnDisable` cleanup on contained lock areas. The arena therefore uses normal area-lock architecture, not a boss-only camera API.

What the C\# proves:

* The arena lock begins when the battle coroutine starts.  
* Camera locks and gate closure are initiated together.  
* The lock normally releases after end delay/fanfare sequencing.  
* `dontDisableCamlocksOnEnd` supports encounters that retain framing.  
* Hero-death persistence may be suppressed with `skipSaveIfPlayerIsDead`.

What remains unproven:

* Whether every `camLocks` group is initially inactive.  
* Exact camera priorities/bounds for boss rooms.  
* Whether a completed-room load always leaves the lock inactive.  
* Boss-specific FSM camera commands.  
* Abnormal destruction cleanup beyond component/lifecycle cleanup.

`BattleScene.Start` calls `BattleCompleted` when persistent completion is already set. `BattleCompleted` disables triggers, opens gates quickly, and disables waves, but does not explicitly disable `camLocks`. Quiet completed-room framing therefore depends on authored initial state or other scene/FSM behavior: **Unknown**.

### **`BossSceneController`**

`BossSceneController` is a separate boss-challenge/sequence scene controller. It handles transition presentation, boss death aggregation, bindings, and exit completion. It contains no direct camera-lock reference or call.

Accordingly:

* Core arena camera locking is not owned by `BossSceneController`.  
* Special boss-scene transition effects may be provided by its transition prefab/events.  
* Any boss-specific camera FSM behavior is **Unknown** without those assets.

### **Control locks and death**

`BattleScene` does not directly relinquish hero control in the inspected start path. Intro control coordination may exist in boss or gate FSMs: **Unknown**.

`GameManager.PlayerDead` and `PlayerDeadFromHazard` freeze the camera. Scene unload then drains lock areas. This proves global death camera freezing and lock cleanup, but not every boss-room retry presentation detail.

---

## **Scene-transition and restoration behavior**

### **New scene**

`GameCameras` is persistent through `DontDestroyOnLoad`. `GameCameras.StartScene` invokes:

1. `CameraController.SceneInit`  
2. `CameraTarget.SceneInit`

`CameraController.SceneInit` creates a fresh `lockZoneList`, refreshes tilemap bounds, resets smoothing, and sets scene-centre limits.

`CameraLockArea.StartRoutine` waits until the persistent controller references the tilemap belonging to the area’s scene before validating authored limits. This avoids validating against the outgoing room.

### **Outgoing scene**

`GameManager` freezes the camera before normal transition fade. It raises `UnloadingLevel`; `CameraController.OnLevelUnload` repeatedly releases every registered lock. That provides deterministic scene-local lock cleanup despite persistent camera ownership.

### **Hero placement**

`CameraController.PositionToHero`:

* waits one fixed update;  
* refreshes tilemap info;  
* positions `CameraTarget`;  
* freezes camera mode temporarily;  
* clamps against room and current lock;  
* clears smoothing velocity;  
* waits 0.1 seconds;  
* restores previous mode, falling back to follow if the old lock no longer exists;  
* reapplies current lock when necessary.

### **Death and respawn**

Normal death freezes the camera in `GameManager.PlayerDead`, saves, then transitions to the resolved respawn scene. Hazard death also freezes, fades, broadcasts reload, and invokes hazard respawn if the hero is not fully dead.

The supplied code proves camera freeze and subsequent scene reinitialization. The full boss-specific respawn/retry path is **Unknown**.

### **Area destruction**

Normal Unity disable-before-destroy behavior releases the area through `TrackTriggerObjects.OnDisable`. `CameraLockArea.OnDestroyEvent` only removes entries from `instantLockedArea`; it is not a general lock-list safety net.

---

## **Silksong authoring workflow**

### **Ordinary room**

A level designer appears to author:

* a tilemap whose width/height define global room bounds;  
* one or more `CameraLockArea` objects;  
* one or more activation colliders on each lock area;  
* separate world- or self-space camera-centre min/max values;  
* optional priority;  
* optional manual-look clamps;  
* optional super-jump suppression;  
* optional `CameraOffsetArea` triggers.

This is partly **Direct evidence** from serialized fields and partly **Strong inference** because scenes are absent.

### **Boss/battle room**

A battle-room author appears to provide:

* `BattleScene`;  
* a `camLocks` parent, normally containing one or more `CameraLockArea` components;  
* gate objects with gate FSMs;  
* start trigger collider;  
* encounter waves;  
* persistence field(s);  
* optional `dontDisableCamlocksOnEnd`.

The lock objects may be inactive until combat. That initial state is **Strong inference**.

### **Debugging and validation**

Visible support includes:

* `CameraLockArea.OnValidate` legacy-priority migration.  
* Runtime verbose logging in `CameraController` and `CameraTarget`.  
* Public Inspector-visible lock list, current limits, modes, destinations, and bounds flags.  
* `CameraLockArea.AddDebugDrawComponent` using camera-lock debug color.  
* `TrackTriggerObjects.AddDebugDrawComponent` for generic regions.  
* Conditional Inspector attributes and modifiable-property annotations.  
* Runtime bounds repair after tilemap readiness.

No complete custom editor or scene gizmo implementation was present in the inspected files.

---

## **Underbrew current implementation map**

| Responsibility | Current owner and behavior | Exact evidence |
| ----- | ----- | ----- |
| Persistent composition | `_GameCameras` prefab with `GameCameras`, target, controller, fade, shake, parent rig, HUD | `Assets/_Project/Prefabs/Managers/_GameCameras.prefab`; `GameCameras.cs`, `Awake`. |
| Hero intent adapter | `HeroCameraSignalBridge` sends facing, velocity, dash, sprint, and manual-look input one-way | `Assets/_Project/Scripts/Hero/HeroCameraSignalBridge.cs`, `Tick`. |
| Target tracking | `CameraTarget` infers velocity, look-ahead, fall framing, offsets, target smoothing | `Assets/_Project/Scripts/Camera/CameraTarget.cs`, `Tick`. |
| Final camera movement | `CameraController` smooths X/Y, clamps bounds/locks, owns modes | `CameraController.cs`, `LateUpdate`, `ComputeDestination`. |
| Room bounds | `CameraBoundsVolume` trigger; most-recent valid volume wins | `CameraBoundsVolume.cs`; `CameraController.GetActiveBoundsVolume`. |
| Lock registration | `CameraLockArea` raises typed events; `GameCameras` routes to controller | `CameraLockArea.cs`; `CameraEventService.cs`; `GameCameras.cs`. |
| Active lock selection | `lockStack`; highest priority, newest equal-priority wins | `CameraController.GetActiveLockArea`. |
| Viewport clamping | Perspective frustum half-extents inset room/lock geometry | `CameraController.GetFrustumHalfExtents`, `ClampToBounds`, `ClampToLockArea`. |
| Boss integration | Encounter explicitly activates area and raises enter; release reverses both | `BossEncounterController.cs`, `SetCameraLockActive`. |
| Freeze | Typed request contains source, but one global coroutine handles it | `CameraEventService.CameraFreezeRequest`; `GameCameras.OnCameraFreezeRequested`. |
| Shake | Typed request routed to MM Feel parent shaker | `CameraEventService`; `CameraShakeCueService`; `_GameCameras.prefab`. |
| Scene transitions | Freeze, fade, scene load, hero placement, camera rebind/snap, fade-in | `SceneTransitionManager.cs`, `TransitionRoutine`; `GameCameras.RebindForSceneEntry`. |
| Completed boss revisit | Initialize completed state with lock inactive and barriers open | `BossEncounterController.InitializeEncounter`. |
| Death cleanup | Boss interrupt releases camera lock; full reload normally reconstructs encounter | `BossEncounterController.HandleHeroDeath`; `GameManager.BeginRespawnSequence`. |

Scene evidence:

* Only `Assets/_Project/Scenes/SampleScene4.unity` contains the inspected camera volumes.  
* `CameraBounds_SampleScene4`: active `CameraBoundsVolume`, collider size `37.5 × 17.394745`.  
* `ArenaCameraLock`: inactive child of `UndeadExecutionerEncounter`, collider size `24.5 × 14.5`, priority `50`, X/Y enabled.  
* The encounter references that lock directly.  
* `SampleScene`, `SampleScene2`, and `SampleScene3` contain no matching checked-in `CameraLockArea` or `CameraBoundsVolume` script GUIDs.

---

## **Direct comparison matrix**

| Capability | Silksong implementation | Exact Silksong evidence | Underbrew implementation | Exact Underbrew evidence | Meaningful difference | Recommendation |
| ----- | ----- | ----- | ----- | ----- | ----- | ----- |
| Area registration | Direct `LockToArea(this)` on first inside transition | `CameraLockArea.cs`, `OnInsideStateChanged` | Typed event routed by persistent root | `CameraLockArea.cs`; `CameraEventService.cs`; `GameCameras.cs` | Underbrew is more decoupled | Retain |
| Area removal | Direct `ReleaseLock(this)`; base disable emits outside | `CameraLockArea.cs`; `TrackTriggerObjects.OnDisable` | Exit event and `OnDisable` | Underbrew `CameraLockArea.cs` | Similar; Underbrew event boundary cleaner | Retain and test |
| Priority | Highest integer wins | `CameraController.LockToArea`, `ReleaseLock` | Highest integer wins | `CameraController.GetActiveLockArea` | Equivalent | Retain |
| Equal priorities | Newest entry wins incidentally | Same methods; strict comparisons/list direction | Newest entry wins incidentally | `GetActiveLockArea`, reverse scan \+ strict `>` | Same undocumented behavior | Make explicit |
| Overlap | All areas remain in `lockZoneList` | `LockToArea` | All areas remain in `lockStack` | `EnterLockArea` | Equivalent | Retain |
| Fallback | Re-selects highest remaining area | `ReleaseLock` | Recomputes active area | `ExitLockArea`, `RefreshActiveLock` | Equivalent normal path | Add direct tests |
| Bounds representation | Authored legal camera-centre min/max separate from trigger | `CameraLockArea` fields | Trigger rectangle represents visible world region | `GetLockRect`, `ClampToLockArea` | Underbrew is viewport-aware but couples trigger/framing | Retain viewport model; add centre preview |
| Room bounds | Tilemap size minus fixed half-view | `GetTilemapInfo` | Authored room volume inset by current frustum | `ClampToBounds` | Underbrew is more flexible | Retain Underbrew |
| Viewport clamping | Fixed constants | `14.6`, `8.3` throughout controller | Calculated FOV/aspect/distance | `GetFrustumHalfExtents` | Underbrew safer for projection changes | Retain and validate |
| X-only lock | Numeric X range; other axis can remain broad | `CameraTarget.Update` | Flag exists, but target clamps both axes | `CameraTarget.SmoothToDestination`; `CameraController.ClampToLockArea` | Underbrew flag is not fully honored | Fix now |
| Y-only lock | Same | Same | Same defect | Same | Genuine correctness issue | Fix now |
| Look offsets | Hero action offsets plus manual ±6 | `CameraTarget.Update`; `CameraController.LateUpdate` | Hero bridge, target offsets, manual input offset | `HeroCameraSignalBridge`; `CameraTarget`; `CameraController` | Underbrew is more decoupled/configurable | Retain |
| Look clamps | Selected area limits manual vertical look | `CameraController.LateUpdate` | Selected area limits manual look | `ClampToLockArea` | Underbrew zero-as-unset sentinel is ambiguous | Add explicit override flags |
| Area offsets | Most-recent registered area; normal application not proven | `CameraOffsetArea`; `CameraTarget.GetCameraOffset` | Active offsets sum with magnitude cap | Underbrew `CameraTarget.GetCameraOffset` | Underbrew is clearer/more capable | Retain |
| Smoothing | Two-stage target \+ camera smoothing | `CameraTarget.Update`; `CameraController.LateUpdate` | Same broad split | Underbrew target/controller | Equivalent pattern | Retain |
| Entry transition | Old/new clamp delta selects slow/slower damping | `CameraTarget.EnterLockZone` | Generic slow damp and distance selection | `RefreshActiveLock`; `EnterLockZone` | Underbrew lacks cause-specific/profile data | Add transition context/profile |
| Exit transition | Equivalent old-lock/room comparison | `CameraTarget.ExitLockZone` | Generic slow damp | `RefreshActiveLock` | Less explicit | Add transition context |
| Active-area disable | Base disable releases | `TrackTriggerObjects.OnDisable` | Area `OnDisable` sends exit | Underbrew `CameraLockArea.OnDisable` | Equivalent normal path | Retain |
| Scene unload | Explicitly drains lock list | `CameraController.OnLevelUnload` | `SceneInit` clears lists after load | Underbrew `CameraController.SceneInit` | Silksong cleans before unload; Underbrew resets after | Add explicit scene teardown/lease clearing |
| Hero already inside | Explicit overlap query on enable/hero positioned | `TrackTriggerObjects.OnEnable` | `OnTriggerStay` eventually re-enters; boss explicitly raises enter | Underbrew `CameraLockArea`; `BossEncounterController` | Ordinary area activation can wait for physics | Add immediate overlap refresh |
| Freeze | Global mode, no source | `FreezeInPlace`, `StopFreeze` | Request has source, implementation ignores it | `CameraFreezeRequest`; `GameCameras` | Underbrew contract is ready but semantics incomplete | Source-owned leases now |
| Freeze under lock | Controller avoids replacing frozen mode on lock entry | `LockToArea` | `RefreshActiveLock` sets `Locked` unconditionally | Underbrew `RefreshActiveLock` | Lock event can cancel freeze | Fix now |
| Shake | Concurrent profile trackers, capped sum | `CameraShakeManager` | MM Feel event backend | `CameraShakeCueService` | Silksong supports richer concurrency/range | Keep backend; honor source before loops |
| Shake cancellation | By profile/all, not source | `CameraManagerReference.CancelShake` | `Cancel(source)` stops all | `CameraShakeCueService.Cancel` | Both weaker than source ownership | Improve when persistent/looping shakes expand |
| Scripted focus | Free target/Panning/FSM follow | PlayMaker action files | No typed focus request | No focus contract found | Missing presentation seam | Add narrow focus lease in Phase 3 |
| Zoom | Generic FSM tween | `TweenCamera` | None | No request/code found | Not currently needed | Defer |
| Boss activation | Activates lock group at battle start | `BattleScene.LockInBattle` | Activates lock and explicitly registers | `BossEncounterController.SetCameraLockActive` | Underbrew safer for already-inside hero | Retain |
| Boss completion | Disables lock group after delays | `BattleScene.EndBattle` | Releases lock synchronously at completion commit | `TryCommitCompletion` | Underbrew lifecycle is more explicit | Retain |
| Hero death | Global camera freeze; battle retry specifics incomplete | `GameManager.PlayerDead`; battle source | Encounter releases own lock, manager owns respawn | `HandleHeroDeath`; `GameManager` | Underbrew ownership cleaner | Retain |
| Completed-room revisit | Partial C\# only; cam-lock suppression unproven | `BattleScene.Start`, `BattleCompleted` | Explicit lock inactive/barriers open | `InitializeEncounter` | Underbrew fully proves quiet restoration | Retain |
| Authoring | Trigger plus separate centre limits and priority | `CameraLockArea` fields | Collider defines both trigger and framed world region | Underbrew `CameraLockArea` | Underbrew simpler but less expressive | Keep simple model; optionally separate activation volume later |
| Debugging | Verbose fields/logs and runtime debug draw | Silksong controller/target/area | Target/offset gizmos only | Underbrew `CameraTarget.OnDrawGizmos`, `CameraOffsetArea.OnDrawGizmosSelected` | Lock stack/centre preview absent | Add tools |
| Validation | Bounds self-correction, migration, debug attributes | `ValidateBounds`, `OnValidate` | Boss validator only checks required reference | `BossEncounterValidator` | Camera geometry/invariant validation absent | Add validator |
| Tests | No supplied camera tests | **Unknown** | Boss death test checks lock GameObject off; no camera selection tests | `BossEncounterFoundationTests` | Core camera contracts untested | Add EditMode \+ PlayMode tests |

---

## **What Underbrew already gets right**

* `GameCameras` is a clean persistent composition root. The prefab separates `CameraParent` shake motion from `MainCamera` follow motion.  
* `CameraEventService` is a typed request boundary and avoids PlayMaker/string-event coupling.  
* Camera components do not read `HeroController`, motor, sensors, or boss internals. `HeroCameraSignalBridge` is a one-way adapter.  
* `CameraController` remains final motion authority.  
* `CameraLockArea` remains scene-authored framing rather than boss logic.  
* `BossEncounterController` is only a request source. Concrete boss behavior has no camera reference.  
* Boss activation explicitly registers an inactive lock area, so the hero-already-inside case is safe for the current encounter.  
* Boss camera acquisition/release is guarded by `cameraLockHeld`, making repeated calls idempotent.  
* Boss death, completion, failed startup, disable, and destruction release encounter-owned camera state.  
* Completed-room initialization explicitly avoids reacquiring the arena lock.  
* Room/lock clamps account for the actual perspective viewport.  
* Offset areas sum and are magnitude-limited rather than relying on incidental last-entry behavior.  
* The architecture does not need Cinemachine to resolve the identified problems.  
* No hero movement tuning needs to change.

---

## **Genuine correctness gaps**

1. **Axis flags are not honored end-to-end.**  
   `CameraController.ClampToLockArea` respects `LockX`/`LockY`, but `CameraTarget.SmoothToDestination` clamps both axes whenever target mode is `LockZone`. X-only and Y-only locks therefore still constrain the supposedly unlocked axis.  
2. **Lock events can cancel temporary freeze.**  
   `RefreshActiveLock` always sets `Locked` or `Follow`. Entering, exiting, or disabling a lock during `Frozen` can silently resume motion.  
3. **Free-mode release does not reliably restore the lock target.**  
   `CameraController.EndFreeMode` sets controller mode from `lockStack`, then calls `CameraTarget.EndFreeMode`, which always selects `FollowHero` rather than reapplying the selected lock rectangle.  
4. **Room and lock clamps are sequential rather than intersected.**  
   `ComputeDestination` clamps to room bounds and then lock bounds. A malformed lock outside the room can push the destination back outside the room. Silksong clamps selected lock values to room-centre limits during activation.  
5. **Freeze source is serialized but ignored.**  
   A new request stops the previous timed coroutine. Any release stops the active freeze regardless of requester. Old timed coroutines can also affect later scene/request state unless explicitly cleared.  
6. **Already-inside support is delayed for normal lock areas.**  
   `OnTriggerStay2D` should eventually register, but there is no immediate enable/scene-init overlap scan. Boss locking works only because the encounter explicitly raises entry.  
7. **Look-clamp zero is ambiguous.**  
   `HasLookYMax/Min` treats zero as “not authored,” making a legitimate world-Y zero clamp impossible.  
8. **Core lock semantics have no focused tests.**  
   Existing boss tests verify that a lock GameObject becomes inactive after interruption, not that the persistent camera collection, fallback, or movement is correct.  
9. **Shake source cancellation is not real source ownership.**  
   The request carries `Source`, but `GameCameras` discards it when starting a shake and `CameraShakeCueService.Cancel` stops all shakes.

---

## **Production-feel and authoring gaps**

These are not current correctness failures:

* No distinct room-to-lock, lock-to-lock, or lock-to-follow transition profiles.  
* No entry direction or transition-cause information.  
* No camera-centre-region preview after viewport inset.  
* No warning when a region is smaller than the current viewport.  
* No lock/bounds Gizmos beyond Unity’s collider rendering.  
* No runtime display of registered locks, selected lock, priorities, sequences, bounds, mode, freeze owners, or focus owners.  
* No camera-specific validator.  
* No authored temporary focus/pan seam for boss introductions.  
* No visibility/range filtering for world-position shake requests.  
* Only one real room currently proves camera bounds and boss locking.

---

## **Recommended Underbrew target architecture**

| Capability | Classification | Recommendation |
| ----- | ----- | ----- |
| Explicit equal-priority policy | **Required now** | Define “highest priority, then newest entry sequence.” Store sequence explicitly. |
| Axis-specific lock correctness | **Required now** | Pass axis flags into target lock state or remove lock clamping from `CameraTarget` and let controller own final axis clamps. |
| Lock/room intersection | **Required now** | Calculate one legal camera-centre region from room, viewport, and enabled lock axes. |
| Lock changes under freeze | **Required now** | Separate underlying framing state from temporary override state. Lock changes update underlying state without changing active override. |
| Source-owned freeze | **Required now** | Replace single coroutine with leases keyed by source/token. Camera remains frozen until all applicable leases release/expire. |
| Scene cleanup for requests | **Required now** | Clear scene-owned leases on scene teardown/rebind; retain only explicitly persistent requests. |
| Immediate already-inside detection | **Recommended before boss polish** | Overlap-query hero on area enable and after scene rebind. Keep `OnTriggerStay` as safety net. |
| Transition profiles | **Recommended before boss polish** | Add small data-driven profile for lock-enter, lock-switch, and lock-exit. |
| Centre-region preview | **Recommended before boss polish** | Draw activation collider and calculated legal camera-centre rectangle separately. |
| Camera validator | **Recommended before boss polish** | Check triggers, dimensions, axis settings, room intersection, look-clamp ordering, boss references, and duplicate ambiguous priorities. |
| Runtime active-lock display | **Recommended before boss polish** | Read-only custom Inspector/debug overlay. |
| Temporary focus lease | **Prove with another room/boss** | Add only when an actual intro/outro needs target focus. Preserve underlying lock. |
| Temporary position/pan lease | **Prove with another room/boss** | Add after one lift/cinematic requires an absolute or path-driven camera position. |
| Request priorities | **Prove with focus/pan** | Freeze can use aggregation; focus/pan needs priority plus newest-sequence tie-break. |
| Timeline adapter | **Defer** | Add only as a presentation requester into typed focus/pan APIs. Timeline must not own camera state directly. |
| Automatic zoom | **Defer** | Justified by a boss/room that cannot frame acceptably at fixed projection. |
| Dynamic multi-target framing | **Defer** | Justified by a large aerial or duo encounter that fails with authored regions. |
| Cinemachine migration | **Defer indefinitely** | No unresolvable reason exists. |
| Custom shot graph/node editor | **Defer** | Justified only after several cinematics prove profiles and Timeline adapters inadequate. |

Recommended layered state:

Underlying framing  
  \= hero follow \+ room bounds \+ active CameraLockArea \+ offset areas

Temporary overrides  
  \= zero or more freeze leases  
  \+ optional highest-priority focus/pan lease

Final camera position  
  \= resolve underlying destination  
  \-\> apply selected temporary override  
  \-\> apply transition smoothing  
  \-\> clamp to legal room/lock centre region  
  \-\> write MainCamera  
  \-\> CameraParent shake remains additive

Lock changes should continue updating underneath a freeze. When the last freeze releases, the camera resolves the current lock collection, not the lock that existed when freezing began.

---

## **Code-level design sketches**

### **Active-area registration and deterministic selection**

public readonly struct CameraLockRegistration  
{  
    public CameraLockRegistration(  
        CameraLockArea area,  
        int priority,  
        long entrySequence,  
        CameraLockApplyMode applyMode);  
}

public void EnterLockArea(  
    CameraLockArea area,  
    CameraLockApplyMode applyMode \= CameraLockApplyMode.Live)  
{  
    if (area \== null)  
        return;

    if (registrations.TryGetValue(area, out \_))  
        return; // idempotent; do not silently change sequence

    registrations.Add(area, new CameraLockRegistration(  
        area,  
        area.Priority,  
        \++nextEntrySequence,  
        applyMode));

    RefreshUnderlyingFraming(CameraTransitionCause.LockEntered);  
}

### **Selection and tie-breaking**

best \= null

for each valid registration:  
    if best is null  
       or registration.priority \> best.priority  
       or registration.priority \== best.priority  
          and registration.entrySequence \> best.entrySequence:  
        best \= registration

return best

Do not depend on reverse iteration to define policy.

### **Removal and fallback**

public void ExitLockArea(CameraLockArea area)  
{  
    if (area \== null || \!registrations.Remove(area))  
        return;

    RefreshUnderlyingFraming(CameraTransitionCause.LockExited);  
}

`RefreshUnderlyingFraming` selects the current winner, updates target framing, and chooses transition data. It must not clear a temporary freeze/focus override.

### **Axis-safe legal region**

roomCentreRegion \= inset(roomWorldBounds, currentViewportHalfExtents)

if activeLock exists:  
    lockCentreRegion \= inset(activeLock.WorldBounds, currentViewportHalfExtents)

    legalX \= activeLock.LockX  
        ? intersection(roomCentreRegion.X, lockCentreRegion.X)  
        : roomCentreRegion.X

    legalY \= activeLock.LockY  
        ? intersection(roomCentreRegion.Y, lockCentreRegion.Y)  
        : roomCentreRegion.Y  
else:  
    legalRegion \= roomCentreRegion

`CameraTarget` should not clamp an axis the area does not own. The cleanest option is for `CameraTarget` to produce unconstrained framing intent and for `CameraController` to own all final legal-region clamping.

### **Transition request data**

public enum CameraTransitionCause  
{  
    SceneStart,  
    FollowToLock,  
    LockToLock,  
    LockToFollow,  
    TemporaryOverrideReleased  
}

\[Serializable\]  
public struct CameraTransitionSettings  
{  
    public float duration;  
    public AnimationCurve positionCurve;  
    public bool snap;  
    public bool resetVelocity;  
}

public readonly struct CameraFramingTransitionRequest  
{  
    public CameraTransitionCause Cause { get; }  
    public CameraTransitionSettings Settings { get; }  
    public object Source { get; }  
}

A shared `CameraTransitionProfile` can map cause to settings. Per-area overrides should be optional, not required.

### **Immediate versus live lock application**

public enum CameraLockApplyMode  
{  
    Live,       // normal room traversal; ease  
    Immediate   // scene entry, respawn, hidden activation  
}

Boss encounter activation should normally use `Live`. Scene rebind behind black should use `Immediate`.

### **Source-owned freeze**

public readonly struct CameraRequestHandle : IDisposable  
{  
    public int Id { get; }  
    public void Dispose(); // releases only this request  
}

public static CameraRequestHandle AcquireFreeze(  
    object source,  
    CameraFreezeKind kind,  
    float duration \= \-1f,  
    bool persistAcrossScenes \= false);

Internal model:

freezeRequests\[id\] \= {  
    source,  
    kind,  
    expiresAtUnscaledTime?,  
    sceneHandle,  
    persistAcrossScenes  
}

camera is frozen while freezeRequests.Count \> 0  
hard target freeze is active while any request.Kind \== Hard

A release must identify a handle or exact source. It must not mean “release every freeze.”

### **Temporary focus**

Recommended only when a real presentation uses it:

public static CameraRequestHandle AcquireFocus(  
    object source,  
    Transform target,  
    Vector2 offset,  
    int priority,  
    CameraTransitionSettings enter,  
    CameraTransitionSettings exit,  
    bool clampToUnderlyingArea \= true);

Selection:

highest priority  
then newest request sequence

On release or source destruction, resolve the next focus request. If none remains, transition to the current underlying area lock/follow destination.

### **Automatic cleanup**

Each update or scene teardown:  
    remove request if:  
      source is a destroyed UnityEngine.Object  
      or request is scene-owned and source scene unloaded  
      or duration expired

On GameCameras.OnDisable/OnDestroy:  
    release all handles and unsubscribe events

On scene rebind:  
    clear non-persistent scene requests  
    rebuild bounds/lock overlaps

Plain C\# source objects cannot be automatically recognized as destroyed; they require handle disposal or a component-owned `OnDisable` release.

---

## **Prioritised implementation roadmap**

### **Camera Phase 1 — deterministic correctness and ownership**

Files likely involved:

* `Assets/_Project/Scripts/Camera/CameraController.cs`  
* `Assets/_Project/Scripts/Camera/CameraTarget.cs`  
* `Assets/_Project/Scripts/Camera/CameraLockArea.cs`  
* `Assets/_Project/Scripts/Camera/CameraBoundsVolume.cs`  
* `Assets/_Project/Scripts/Camera/CameraEventService.cs`  
* `Assets/_Project/Scripts/Camera/GameCameras.cs`  
* `Assets/_Project/Scripts/Camera/CameraMode.cs`  
* `Assets/_Project/Scripts/Boss/BossEncounterController.cs` only if the entry API gains an apply mode  
* new camera EditMode tests

Work:

* Explicit registration records and entry sequence.  
* Document newest-entry equal-priority rule.  
* Correct axis locking.  
* Intersect lock and room centre regions.  
* Preserve underlying locks while frozen/free.  
* Source-owned freeze handles and expiry.  
* Clear request state during scene teardown/rebind.  
* Immediate overlap scan for already-inside hero.  
* Add tests for all lifecycle paths.

Public changes:

* Freeze acquire/release handle API.  
* Optional lock apply mode.  
* Read-only debug properties for active/registered areas.  
* Potential removal or deprecation of global `RequestFreeze(Release)` semantics.

Risks:

* Existing Animancer freeze events expect global release.  
* Changing target/controller clamping ownership can alter feel near region edges.  
* Physics overlap queries need the correct player collider/layer selection.

Deferred:

* Focus, pan, zoom, Timeline, dynamic framing.

### **Camera Phase 2 — transitions, validation, and authoring**

Files likely involved:

* `CameraController.cs`  
* `CameraLockArea.cs`  
* `CameraBoundsVolume.cs`  
* `CameraConfig.cs`  
* new `CameraTransitionProfile.cs`  
* new Editor Inspector/Gizmo scripts  
* new camera validator and tests  
* `_GameCameras.prefab`  
* `SampleScene4.unity` only when implementation is authorized

Work:

* Cause-specific transition settings.  
* Optional per-area override.  
* Activation-volume and centre-region Gizmos.  
* Viewport-size preview using current camera configuration.  
* Runtime read-only stack/lease Inspector.  
* Validator for geometry and lifecycle invariants.  
* Prove behavior in at least one ordinary second room and the existing boss room.

Risks:

* Designers may overuse per-area transition overrides.  
* Preview math must match runtime perspective-plane assumptions.  
* Scene YAML should be changed through Unity, not manual text editing.

### **Camera Phase 3 — presentation seam**

Files likely involved:

* `CameraEventService.cs`  
* `GameCameras.cs`  
* `CameraController.cs`  
* new narrow request/handle types  
* optional new encounter-presentation component  
* presentation tests and documentation

Work:

* Source-owned temporary focus.  
* Optional absolute-position/pan request only if required.  
* Priority/sequence resolution.  
* Restore to underlying current lock.  
* Optional Timeline adapter that only acquires/releases typed requests.  
* Boss intro/outro integration through an optional scene-authored presentation requester.

Risks:

* Nested request ordering and skipped-cutscene cleanup.  
* Destroyed target/requester cleanup.  
* Control-lock and camera-release timing.  
* Timeline must not become gameplay completion authority.

---

## **Exact Underbrew file plan**

| File | Proposed responsibility/change |
| ----- | ----- |
| `Assets/_Project/Scripts/Camera/CameraController.cs` | Own explicit registration records, tie-breaking, legal centre-region intersection, underlying framing state, override resolution, debug state |
| `Assets/_Project/Scripts/Camera/CameraTarget.cs` | Produce hero framing intent; stop clamping disabled lock axes; potentially stop owning lock geometry entirely |
| `Assets/_Project/Scripts/Camera/CameraLockArea.cs` | Immediate overlap refresh, explicit look-clamp override semantics, optional transition override, authoring/debug properties |
| `Assets/_Project/Scripts/Camera/CameraBoundsVolume.cs` | Disable cleanup, overlap refresh, debug properties, optional priority if overlapping rooms prove necessary |
| `Assets/_Project/Scripts/Camera/CameraEventService.cs` | Typed handle-based freeze and later focus/pan contracts |
| `Assets/_Project/Scripts/Camera/GameCameras.cs` | Persistent request broker, expiry/source cleanup, scene request clearing, routing |
| `Assets/_Project/Scripts/Camera/CameraConfig.cs` | Shared transition defaults and preview plane settings |
| `Assets/_Project/Scripts/Camera/CameraMode.cs` | Consider exposing resolved mode separately from underlying framing mode |
| `Assets/_Project/Scripts/Camera/CameraShakeCueService.cs` | Later preserve source identity if infinite or overlapping shake cancellation becomes required |
| `Assets/_Project/Scripts/Hero/Animation/HeroCameraAnimancerBridge.cs` | Store/dispose its own freeze handle instead of issuing global release |
| `Assets/_Project/Scripts/Hero/HeroCameraSignalBridge.cs` | No movement changes; retain one-way signals |
| `Assets/_Project/Scripts/Boss/BossEncounterController.cs` | Remain a request source only; at most specify live/immediate lock acquisition |
| `Assets/_Project/Scripts/Scene/SceneTransitionManager.cs` | Acquire and release a transition-owned freeze handle in `try/finally` |
| `Assets/_Project/Scripts/Managers/GameManager.cs` | Only update legacy/local recovery freeze call sites if needed; no new camera authority |
| `_GameCameras.prefab` | Assign transition profile/debug broker settings through Unity |
| `SampleScene4.unity` | Validate existing priority-50 lock and preview; do not redesign boss behavior |
| New camera Editor tests | Selection, lifecycle, clamping, leases, restoration |
| New camera validator/editor scripts | Geometry checks, Gizmos, read-only runtime view |

No concrete boss behavior, `HeroMotor`, `HeroSensors`, or validated movement values should change.

---

## **Unity Editor work required**

* Assign a shared `CameraTransitionProfile` to `_GameCameras`.  
* Draw each `CameraBoundsVolume` world rectangle.  
* Draw each `CameraLockArea` activation rectangle.  
* Draw the calculated legal camera-centre rectangle after viewport inset.  
* Use different colors for X-only, Y-only, and XY locks.  
* Display priority and equal-priority sequence policy in the scene label.  
* Warn when the framed rectangle is smaller than the viewport.  
* Warn when lock and room centre regions do not intersect.  
* Warn when `lookYMin > lookYMax` or a look limit lies outside legal Y.  
* Warn when a required trigger is not a trigger collider.  
* Warn when a boss requires a lock but its area is active by default.  
* Show runtime mode, underlying mode, selected lock, registered stack, entry sequences, current centre region, freeze owners, focus owners, and transition state in a read-only Inspector.  
* Author a second ordinary room/overlap case before treating the architecture as proven.

---

## **Test and validation plan**

### **EditMode tests**

1. Highest priority wins regardless of entry order.  
2. Newest entry wins equal priority.  
3. Re-entering the same area is idempotent and does not change sequence.  
4. Removing active area restores highest remaining.  
5. Removing non-active area does not change selection.  
6. Disabling/destroying active area restores fallback.  
7. Stale entries are removed safely.  
8. X-only lock leaves Y controlled only by room bounds.  
9. Y-only lock leaves X controlled only by room bounds.  
10. Lock limits cannot push outside room limits.  
11. Region smaller than viewport centres deterministically.  
12. Look-up/down clamps work at world Y zero.  
13. Lock changes during freeze update underlying framing without ending freeze.  
14. Releasing one freeze owner leaves other freezes active.  
15. Expiring a timed freeze releases only that request.  
16. Destroyed Unity request source cleans itself up.  
17. Free/focus release restores the currently selected lock, including one entered during override.  
18. Scene rebind clears scene-owned requests and stale areas.  
19. Immediate application snaps; live application transitions.  
20. Shake cancellation semantics match documented source behavior.

### **PlayMode tests**

1. Hero begins a scene inside an ordinary lock and is framed before fade-in.  
2. Walk through overlapping equal- and mixed-priority regions.  
3. Disable the active region while the hero remains inside the fallback.  
4. Enter and exit the SampleScene4 boss encounter.  
5. Die during boss intro and active combat; verify lock, freeze, barriers, control, and HUD cleanup.  
6. Reload checkpoint and verify fresh boss/lock registration.  
7. Defeat boss and revisit completed room with no lock replay.  
8. Transition from a locked room to a new room and verify no old request survives.  
9. Start a focus/freeze, unload its scene, and verify restoration.  
10. Verify camera-parent shake does not change controller position or legal-region state.

Do not infer camera correctness from the existing boss test that only checks `gameObject.activeSelf`.

---

## **Documentation updates**

Update:

* `Docs/FeatureSpecs/Camera.md`  
  * Explicit equal-priority rule.  
  * Underlying framing versus temporary override state.  
  * Immediate/live lock application.  
  * Freeze lease ownership.  
  * Axis semantics.  
  * Room/lock intersection.  
  * Already-inside behavior.  
  * Boss integration and cleanup.  
* `Docs/FeatureSpecs/CameraImplementationChecklist.md`  
  * Mark historical behavior as superseded where appropriate.  
  * Add tests and authoring validation checklist.  
* `Docs/Architecture.md`  
  * Add the layered camera-state model and request-handle ownership.  
  * Preserve camera/hero/boss independence.  
* `Docs/FeatureSpecs/BossEncounters.md`  
  * State that the encounter acquires a normal scene lock and never owns camera internals.  
  * Describe optional presentation requester separately.  
* `Docs/ImplementationPlan.md`  
  * Add the three camera phases and second-room proof gate.  
* `Docs/HeroFeelTuning.md`  
  * State explicitly that camera changes do not authorize movement-value changes.  
* `Docs/Research/SilksongBossSystemsProductionReview.md`  
  * Link to this deeper report and defer to it for camera details.

---

## **Deferred capabilities**

| Capability | What future content would justify it |
| ----- | ----- |
| Dynamic multi-target camera | A duo, escort, or arena mechanic that cannot remain readable with a static authored region |
| Automatic hero/boss framing solver | A very large or highly mobile boss that repeatedly leaves acceptable composition |
| Automatic zoom | A real room that requires materially different visible scale during play |
| Cinematic shot graph | Several complex cinematics whose sequencing cannot be maintained with profiles plus Timeline adapters |
| Timeline camera ownership | None recommended; Timeline may request typed leases, not own global camera state |
| Cinemachine migration | A demonstrated requirement that the existing controller cannot implement safely or economically |
| Custom camera node editor | Multiple authored cinematics proving Inspector profiles insufficient |
| Boss-specific controller logic | None; concrete bosses should remain camera-independent |
| Direct camera references in bosses | None; use encounter presentation requesters |
| Camera logic in `HeroController` | None; retain the signal bridge |
| Camera changes to `HeroMotor`/sensors | None; validated movement remains untouched |

---

## **Unknowns and limitations**

* The Silksong source is decompiled and lacks complete scenes, camera prefabs, boss-room prefabs, and PlayMaker FSM assets.  
* Exact Silksong camera projection, viewport, and aspect guarantees are not fully proven.  
* The purpose and downstream consumption of lock entry/exit direction flags are incomplete.  
* Normal runtime application of `CameraOffsetArea.Offset` is not proven by the supplied `CameraTarget.Update`.  
* Exact boss-specific focus, pan, zoom, or intro camera graphs are unknown.  
* Silksong completed-room camera-lock initial states are unknown.  
* The full Silksong world-boss death/retry camera path is only partially visible.  
* Underbrew’s current repository had uncommitted working-tree changes, including `_GameCameras.prefab` and `BossEncounterController.cs`; this report describes that current working tree, not only `HEAD`.  
* No source files, scenes, prefabs, docs, or repository state were modified.  
* No tests were run, because this was a read-only research pass and Unity test execution can update project-local generated state.  
* Subjective camera feel in SampleScene4 was not evaluated in Play Mode.  
* Only SampleScene4 currently supplies meaningful checked-in room/boss camera-volume evidence, so transition and overlap recommendations still need proof in another authored room.

