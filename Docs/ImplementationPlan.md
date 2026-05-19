# Implementation Plan

**Last audited:** 2026-05-19  
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
| Enemy AI framework | Partial |
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
- [x] Write `Docs/FeatureSpecs/HUD.md` — boundary-setting spec: what the HUD subscribes to, Canvas hierarchy skeleton, vertical-slice scope (health only). (Done)
- [x] Write `Docs/FeatureSpecs/Audio.md` — boundary-setting spec: `AudioManager` API, call site rules, music routing. (Done)

---

## Milestone 0 — Persistent Infrastructure

**Goal:** All persistent singletons exist and boot correctly before any gameplay scene loads.

- [x] Create boot scene (Build Index 0) with a `Bootstrap` MonoBehaviour. (Boot.unity + `Bootstrap` exist)
- [x] Implement `GameManager` — `GameState` enum, `SceneInit` event, `BeginSceneTransition`, `Pause` / `Unpause`. See `Docs/Architecture.md` — Scene Transitions. (Done; also has `HitStop` and `BeginRespawnSequence` — see Known Technical Debt)
- [x] Implement `AudioManager` — `PlaySFX(AudioClip)` and `PlayMusic(AudioClip, bool loop)` only. DontDestroyOnLoad. (Done)
- [ ] Implement `InteractManager` — priority-sorted interactable list, interact input routing. DontDestroyOnLoad.
- [ ] Wire `Bootstrap`: instantiate and DontDestroyOnLoad all four managers in order; call `SaveManager.LoadOrCreate()`; then load a placeholder scene. (Partial — Bootstrap instantiates GameManager / AudioManager / GameCameras prefabs; SaveManager and InteractManager not present yet.)
- [ ] Resolve the `HeroController.ResolveDependencies` AssetDatabase fallback — link SOs via Inspector instead.

---

## Milestone 1 — Camera and Scene Foundation

**Goal:** A functional room the hero can move through, with a working camera and room boundary.

- [x] Implement `CameraController`, `CameraTarget`, `CameraBoundsVolume`, and `CameraLockArea` — smooth follow, room bounds, and lock zones. See `Docs/FeatureSpecs/Camera.md`.
- [x] Create `CameraConfig` SO at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.
- [ ] Author first test level: platforms, walls, pits, at least two rooms.
- [ ] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Include door variant (requires interact) and auto variant (trigger on entry). `TransitionPoint` sets `SaveManager.ActiveRespawnMarker` on entry.
- [x] Implement `HazardZone` (Done) and stub `RespawnMarker`. (`HazardZone` calls `HeroBox.TriggerHazardDeath`; `RespawnMarker` is a compile-time stub — SaveManager integration is pending.)
- [ ] Implement `HazardRespawnMarker` — placed near pits; `HazardZone` sets active hazard respawn marker on `SaveManager`.
- [x] Implement `HeroHealthComponent` — TakeDamage, TriggerHazardDeath, i-frames (reference-counted), OnDamaged / OnDeath events. (Done)
- [ ] Implement hero hurt response in `HeroController`: subscribe to OnDamaged → blackboard Hurt state, knockback, control lock. (Partial)
- [x] Implement basic respawn sequence on OnDeath: fade out, position at marker, fade in, restore control. (Partial — runtime respawn exists via `GameManager.BeginRespawnSequence` using nearest-marker lookup; persistent active markers / SaveManager wiring is not implemented.)
- [ ] Validate sensor probes against authored geometry; confirm `terrainLayers` is set correctly.

---

## Milestone 2 — Enemy Loop

**Goal:** At least one enemy the player can fight and kill; a checkpoint to respawn from.

These two come together because neither is meaningful without the other — a combat loop requires both an enemy to fight and a recovery point.

- [x] Implement `EnemyController`, `EnemyStateBlackboard`, `EnemyHealthComponent`, `EnemyRecoil`, `EnemyContactDamage`, `DamageHero`, `EnemyFeedbackController`, `IEnemyBehaviour`, and `MushroomEnemy` patrol behaviour. See `Docs/FeatureSpecs/EnemyAI.md`. (Done — `EnemyMotor`, `EnemyPerception`, and full state machine are still planned)
- [x] Create `EnemyConfig` SO for the first enemy type. (`MushroomConfig.asset` exists)
- [x] Implement `IHeroDownslashResponder` on the first enemy (`EnemyHealthComponent` implements it). (Done)
- [ ] Implement `EnemyMotor` — Rigidbody2D velocity control for enemy movement behaviours.
- [ ] Implement `EnemyPerception` — overlap / raycast detection; writes `alerted` to blackboard.
- [ ] Implement full `EnemyBehaviour` state machine — Idle → Patrol → Chase → Attack → Hurt → Dead.
- [ ] Implement `InteractableBase` base class (used by CheckpointInteractable and future interactables).
- [ ] Implement `CheckpointInteractable` — calls `SaveManager.Save()`, sets active respawn marker.
- [ ] Place a checkpoint and at least one enemy in the test level; validate the full loop: fight → die → respawn → fight.
- [ ] Wire attack SFX through `AudioManager.PlaySFX` in `HeroAttackModule` (remove the AudioSource field).
- [ ] Add basic action SFX (jump, land, dash, hurt) via `AudioManager.PlaySFX` calls in the relevant Action classes.

---

## Milestone 3 — Ability System

**Goal:** Gated traversal abilities can be unlocked and the gate is data-driven.

- [x] Create `PlayerAbilityState` script (`Assets/_Project/Scripts/Hero/Core/PlayerAbilityState.cs`). (Done — asset at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` must be created manually in Unity Editor via CreateAssetMenu and wired into HeroController Inspector field.)
- [x] Create `AbilityId` enum (`Assets/_Project/Scripts/Hero/Core/AbilityId.cs`). (Done — Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast)
- [x] Gate dash behind `PlayerAbilityState.dashUnlocked` (covers ground and air — no separate flags). (Done)
- [x] Gate wall-slide and wall-jump behind shared `PlayerAbilityState.wallClingUnlocked` (no separate flags). (Done)
- [x] Implement `AbilityPickup` MonoBehaviour (`Assets/_Project/Scripts/World/AbilityPickup.cs`). (Done — save/world-state integration is a TODO)
- [x] Implement `AbilityGate` MonoBehaviour (`Assets/_Project/Scripts/World/AbilityGate.cs`). (Done — reacts to `PlayerAbilityState.AbilityChanged` event at runtime)
- [x] Implement wall-jump (`HeroWallJumpAction`). See `Docs/FeatureSpecs/Abilities.md`. (Done — gated behind `wallClingUnlocked`)
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
- `SampleScene` is the only scene; a proper first-level scene should replace it once the test level is authored in Milestone 1.
- `HeroController.ResolveDependencies` falls back to editor-only `AssetDatabase` calls — resolve via Inspector wire-up in Milestone 0.
- `GameManager` holds `_hero` and `_heroHealth` as cached fields and calls `_heroHealth.RestoreFullHealth()` from `BeginRespawnSequence`. This is a temporary coupling; health restoration on respawn should move into `SaveManager.ApplySaveData` once the save system exists. `HitStop` and `BeginRespawnSequence` are also beyond the stated "four responsibilities" boundary — document or relocate when SaveManager is implemented.
- `GameManager.BeginRespawnSequence` uses nearest-marker lookup instead of honouring a persisted active respawn marker — will be correct only after `SaveManager` owns the active marker pointer.

## Current Integration Risks

- RespawnMarker is currently a compile-time stub (`Assets/_Project/Scripts/World/RespawnMarker.cs`) — SaveManager / persistent active-respawn-marker wiring is not implemented yet.
- `SaveManager` and `InteractManager` are not present in the codebase; several systems (checkpoints, hazard respawn, Save persistence) reference them in docs and comments.
- `CheckpointInteractable` and `HazardRespawnMarker` are not implemented — checkpoint activation and hazard-specific respawn persistence are pending.
- Runtime respawn exists via `GameManager.BeginRespawnSequence`, but it uses nearest-marker lookup and does not honour a persisted "active" marker (Save system missing) — this is a functional gap for correct respawn semantics.
- Prefab / Inspector wiring risk: `Bootstrap` expects `GameManager`, `AudioManager`, and `GameCameras` prefabs to be configured in the Boot scene; the prefabs exist at `Assets/_Project/Prefabs/Managers/_GameManager.prefab`, `_AudioManager.prefab`, and `_GameCameras.prefab` but inspector hookups should be verified in-editor.
- Engine/API compatibility: the code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g., `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor.
- HUD / UI wiring is not implemented: `HeroHealthComponent` exposes events, but `HealthDisplay` and HUD subscription are not yet in place.

