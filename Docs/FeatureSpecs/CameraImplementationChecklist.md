# Camera Implementation Checklist

> **Historical / superseded:** This checklist records the pre–Camera Phase 1 implementation.
> `Docs/FeatureSpecs/Camera.md` is the current contract. In particular, target-side lock clamping
> and the global freeze operation described below were superseded on 2026-07-25.

## Guide Mapping

- `CameraTarget` remains the intent/brain layer: hero tracking, look-ahead, vertical anticipation, offset volumes, and lock-zone target clamping.
- `CameraController` remains the rig/execution layer: movement smoothing, camera modes, bounds clamping, projection, freeze, and scene positioning.
- `GameCameras` acts as the lightweight scene director and event router for fade, shake, lock, offset, and freeze requests.
- `CameraEventService` replaces PlayMaker-style messages with explicit C# requests.
- MM Feel remains the shake backend behind `ICameraShakeService`.

## Implementation Status

- [x] Preserve current follow behaviour before refactoring deeper.
- [x] Keep intent and execution split between `CameraTarget` and `CameraController`.
- [x] Add explicit camera modes: Follow, Locked, Frozen, Free.
- [x] Add camera event service for lock, offset, fade, shake, freeze, and mode changes.
- [x] Route lock and offset trigger volumes through the event service.
- [x] Keep priority-based camera lock selection and safe cleanup on disable.
- [x] Stack camera offset volumes and clamp combined authored offset.
- [x] Add structured camera shake contract with MM Feel implementation.
- [x] Preserve existing `CameraShakeRequester` helpers as compatibility wrappers.
- [x] Add fade request routing through the existing `CameraFade` setup.
- [x] Add hard and soft freeze requests.
- [x] Add optional Animancer-facing camera bridge methods.
- [x] Add `CameraInfoCache` for once-per-frame camera position, aspect, and world half-extents.
- [x] Update camera docs with setup notes and deferred work.

## Unity Setup Notes

- `_GameCameras` should reference `CameraController`, `CameraTarget`, `CameraFade`, `CameraShakeCueService`, and the HUD camera.
- `CameraController` and `CameraTarget` should both reference `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.
- `CameraParent` should remain the MM Feel shake target. `MainCamera` should stay under `CameraParent`.
- `CameraParent` should keep `MMWiggle.PositionActive` enabled; `MMCameraShaker` requires it to visibly move the rig.
- `MainCamera` should remain perspective, FOV `24`, local Z `-38.1`.
- `CameraLockArea`, `CameraOffsetArea`, and `CameraBoundsVolume` require `BoxCollider2D` set as trigger.
- `HeroCameraSignalBridge` should live on the hero and be initialized by `HeroController`.
- `HeroCameraAnimancerBridge` is optional. Add it only when animation events need to emit high-level camera requests.

## Deferred TODOs

- Add authored slide and super-move camera offsets once those hero states exist.
- Add distance/visibility filtering for world-positioned camera shake requests.
- Add render hooks or capture-to-texture only if the vertical slice needs special presentation effects.
- Add ledge/edge detection before making look-up/look-down edge-specific.
