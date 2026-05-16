# Implementation Plan

Current date reference: 2026-05-13.
This is a living document. Update when milestones complete or priorities shift.

---

## Status

| Layer | Status |
|---|---|
| Hero movement (walk, run, jump, coyote, dash, wall-slide) | Done |
| Directional melee combat (Side / Up / Down swings) | Done |
| Animancer-driven animation (locomotion, action states) | Done |
| Input System integration (KB+Mouse, Gamepad) | Done |
| ScriptableObject tuning pipeline | Done |
| Boot scene / persistent infrastructure | Partial |
| Camera system | Done |
| Enemy AI framework | Not started |
| Interactables / checkpoints | Not started |
| HeroHealthComponent / hurt / death / respawn | Partial |
| Ability unlock system | Not started |
| Save / load system | Not started |
| Scene transitions | Partial |
| UI (HUD, menus) | Not started |
| Audio system | Partial |

---

## Immediate — No Code Required

These must happen before any milestone work begins. Both are preconditions for the systems they unblock.

- [ ] Add `Interact` action (button) to the Player action map in `InputSystem_Actions.inputactions`. Bindings: E (keyboard), South button (gamepad). Expose `InteractPressedThisFrame` on `HeroInputReader` alongside the existing input signals.
- [ ] Write `Docs/FeatureSpecs/HUD.md` — boundary-setting spec: what the HUD subscribes to, Canvas hierarchy skeleton, vertical-slice scope (health only).
- [ ] Write `Docs/FeatureSpecs/Audio.md` — boundary-setting spec: `AudioManager` API, call site rules, music routing.

---

## Milestone 0 — Persistent Infrastructure

**Goal:** All persistent singletons exist and boot correctly before any gameplay scene loads.

- [ ] Create boot scene (Build Index 0) with a `Bootstrap` MonoBehaviour
- [ ] Implement `GameManager` — four responsibilities only: `GameState` enum, `SceneInit` event, `BeginSceneTransition`, `Pause` / `Unpause`. See `Docs/Architecture.md` — Scene Transitions.
- [ ] Implement `AudioManager` — `PlaySFX(AudioClip)` and `PlayMusic(AudioClip, bool loop)` only. DontDestroyOnLoad.
- [ ] Implement `InteractManager` — priority-sorted interactable list, interact input routing. DontDestroyOnLoad.
- [ ] Wire `Bootstrap`: instantiate and DontDestroyOnLoad all four managers in order; call `SaveManager.LoadOrCreate()`; then load a placeholder scene (or the camera/level scene once Milestone 1 is complete).
- [ ] Resolve the `HeroController.ResolveDependencies` AssetDatabase fallback — link SOs via Inspector instead.
 - [x] Create boot scene (Build Index 0) with a `Bootstrap` MonoBehaviour (Boot.unity + `Bootstrap` exist)
 - [x] Implement `GameManager` — four responsibilities only: `GameState` enum, `SceneInit` event, `BeginSceneTransition`, `Pause` / `Unpause`. See `Docs/Architecture.md` — Scene Transitions. (Done)
 - [x] Implement `AudioManager` — `PlaySFX(AudioClip)` and `PlayMusic(AudioClip, bool loop)` only. DontDestroyOnLoad. (Done)
 - [ ] Implement `InteractManager` — priority-sorted interactable list, interact input routing. DontDestroyOnLoad.
 - [ ] Wire `Bootstrap`: instantiate and DontDestroyOnLoad all four managers in order; call `SaveManager.LoadOrCreate()`; then load a placeholder scene (or the camera/level scene once Milestone 1 is complete). (Partial — Bootstrap instantiates GameManager/AudioManager/GameCameras prefabs; SaveManager/InteractManager not present.)
 - [ ] Resolve the `HeroController.ResolveDependencies` AssetDatabase fallback — link SOs via Inspector instead.

---

## Milestone 1 — Camera and Scene Foundation

**Goal:** A functional room the hero can move through, with a working camera and room boundary.

- [x] Implement `CameraController`, `CameraTarget`, `CameraBoundsVolume`, and `CameraLockArea` — smooth follow, room bounds, and lock zones. See `Docs/FeatureSpecs/Camera.md`.
- [x] Create `CameraConfig` SO at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.
- [ ] Author first test level: platforms, walls, pits, at least two rooms.
- [ ] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Include door variant (requires interact) and auto variant (trigger on entry). `TransitionPoint` sets `SaveManager.ActiveRespawnMarker` on entry.
- [ ] Implement `HazardZone` and `HazardRespawnMarker` — place in the pit of the test level.
- [ ] Implement `HeroHealthComponent` — TakeDamage, TriggerHazardDeath, i-frames (reference-counted), OnDamaged / OnDeath events.
- [ ] Implement hero hurt response in `HeroController`: subscribe to OnDamaged → blackboard Hurt state, knockback, control lock.
- [ ] Implement basic respawn sequence on OnDeath: fade out, position at active marker, fade in, restore control.
- [ ] Validate sensor probes against authored geometry; confirm `terrainLayers` is set correctly.
 - [ ] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Include door variant (requires interact) and auto variant (trigger on entry). `TransitionPoint` sets `SaveManager.ActiveRespawnMarker` on entry.
 - [x] Implement `HazardZone` (Done) and stub `RespawnMarker` (Stubbed). (HazardZone calls `HeroBox.TriggerHazardDeath`; `RespawnMarker` is currently a compile-time stub so SaveManager integration is pending.)
 - [x] Implement `HeroHealthComponent` — TakeDamage, TriggerHazardDeath, i-frames (reference-counted), OnDamaged / OnDeath events. (Done)
 - [ ] Implement hero hurt response in `HeroController`: subscribe to OnDamaged → blackboard Hurt state, knockback, control lock. (Partial)
 - [x] Implement basic respawn sequence on OnDeath: fade out, position at nearest `RespawnMarker`, fade in, restore control. (Partial — runtime respawn exists via `GameManager.BeginRespawnSequence` but persistent active markers / SaveManager wiring is not implemented.)
 - [ ] Validate sensor probes against authored geometry; confirm `terrainLayers` is set correctly.

---

## Milestone 2 — Enemy Loop

**Goal:** At least one enemy the player can fight and kill; a checkpoint to respawn from.

These two come together because neither is meaningful without the other — a combat loop requires both an enemy to fight and a recovery point.

- [ ] Implement `EnemyController`, `EnemyStateBlackboard`, `EnemyMotor`, `EnemyPerception`, `EnemyBehaviour`. See `Docs/FeatureSpecs/EnemyAI.md`.
- [ ] Implement `EnemyHealthComponent` — implements `IHeroAttackReceiver`; handles damage and death, delegating hit reaction to `EnemyRecoil`.
- [ ] Create `EnemyConfig` SO for the first enemy type.
- [ ] Implement `IHeroDownslashResponder` on a hazard or enemy (downslash bounce).
- [ ] Implement `InteractableBase` base class (used by CheckpointInteractable and future interactables).
- [ ] Implement `CheckpointInteractable` — calls `SaveManager.Save()`, sets active respawn marker.
- [ ] Place a checkpoint and at least one enemy in the test level; validate the full loop: fight → die → respawn → fight.
- [ ] Wire attack SFX through `AudioManager.PlaySFX` in `HeroAttackModule` (remove the AudioSource field).
- [ ] Add basic action SFX (jump, land, dash, hurt) via `AudioManager.PlaySFX` calls in the relevant Action classes.

---

## Milestone 3 — Ability System

**Goal:** Gated traversal abilities can be unlocked and the gate is data-driven.

- [ ] Create `PlayerAbilityState` SO at `Assets/_Project/ScriptableObjects/World/PlayerAbilityState.asset`.
- [ ] Gate dash and wall-slide behind flags (defaulted true) to establish the unlock pattern.
- [ ] Implement wall-jump (`HeroWallJumpAction`). See `Docs/FeatureSpecs/Abilities.md`.
- [ ] Implement wall-latch / aimed wall launch (`HeroWallLatchAction`).
- [ ] Implement sprint (`HeroSprintAction`).
- [ ] Author a gate (locked door / ability gate) in the test level that requires an unlocked ability to pass.

---

## Milestone 4 — Save System

**Goal:** Progress persists across sessions.

- [ ] Implement `SaveManager` and `ISaveTarget` interface. See `Docs/FeatureSpecs/SaveSystem.md`.
- [ ] Implement save data classes: `PlayerSaveData`, `AbilitySaveData`, `WorldSaveData`, `MetaSaveData`.
- [ ] Wire `PlayerAbilityState` and `WorldStateRegistry` as `ISaveTarget` implementors.
- [ ] Validate save on checkpoint; reload preserves ability flags and respawn marker.
- [ ] Handle missing / corrupt save file gracefully (fresh state, no crash).

---

## Milestone 5 — Polish Pass

- [ ] HUD: health display wired to `HeroHealthComponent` events. See `Docs/FeatureSpecs/HUD.md`.
- [ ] Pause menu wired through `GameManager.Pause()` / `Unpause()`.
- [ ] Main menu scene loaded from boot after a fresh start.
- [ ] Full SFX pass: all hero actions, all enemy actions, UI sounds.
- [ ] Music routing through `GameManager.BeginSceneTransition`.
- [ ] Game feel: hit-pause, screen shake on landing/hit, particle VFX on impacts.
- [ ] Performance: profile and optimise FixedUpdate sensor raycasts for large levels.

---

## Known Technical Debt

- `HeroConfig` has deprecated box-hitbox fields (`attackSideOffset`, `attackSideSize`, etc.) — remove once all attack modules use authored PolygonCollider2D shapes.
- `HeroAttackModule` currently holds an `AudioSource` field — replace with `AudioClip slashSfx` and route through `AudioManager.PlaySFX` in Milestone 2.
- `SampleScene` is the only scene; a boot scene (Build Index 0) must be created in Milestone 0 before any additional scenes are added to Build Settings.
- `HeroController.ResolveDependencies` falls back to editor-only `AssetDatabase` calls — resolve via Inspector wire-up in Milestone 0.

## Current Integration Risks

- RespawnMarker is currently a compile-time stub (`Assets/_Project/Scripts/World/RespawnMarker.cs`) — SaveManager / persistent active-respawn-marker wiring is not implemented yet.
- `SaveManager` and `InteractManager` are not present in the codebase; several systems (checkpoints, hazard respawn, Save persistence) reference them in docs and comments.
- `CheckpointInteractable` and `HazardRespawnMarker` are not implemented — checkpoint activation and hazard-specific respawn persistence are pending.
- Runtime respawn exists via `GameManager.BeginRespawnSequence`, but it uses nearest-marker lookup and does not honour a persisted "active" marker (Save system missing) — this is a functional gap for correct respawn semantics.
- Prefab / Inspector wiring risk: `Bootstrap` expects `GameManager`, `AudioManager`, and `GameCameras` prefabs to be configured in the Boot scene; the prefabs exist at `Assets/_Project/Prefabs/Managers/_GameManager.prefab`, `_AudioManager.prefab`, and `_GameCameras.prefab` but inspector hookups should be verified in-editor.
- Engine/API compatibility: the code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g., `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor.
- HUD / UI wiring is not implemented: `HeroHealthComponent` exposes events, but `HealthDisplay` and HUD subscription are not yet in place.

