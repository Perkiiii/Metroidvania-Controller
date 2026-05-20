# Implementation Plan

**Last audited:** 2026-05-20  
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
| Boot scene / persistent infrastructure | Done |
| Camera system | Done |
| Enemy AI framework | Partial |
| Interactables / checkpoints | Partial |
| HeroHealthComponent / hurt / death / respawn | Partial |
| Ability unlock system | Done |
| Save / load system | Done (Milestone 0 foundation; world-state and UI deferred) |
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
**Status: Complete.**

- [x] Create boot scene (Build Index 0) with a `Bootstrap` MonoBehaviour. (Boot.unity + `Bootstrap` exist)
- [x] Implement `GameManager` — `GameState` enum, `SceneInit` event, `BeginSceneTransition`, `Pause` / `Unpause`. (Done; also has `HitStop` and `BeginRespawnSequence` — see Known Technical Debt)
- [x] Implement `AudioManager` — `PlaySFX(AudioClip)` and `PlayMusic(AudioClip, bool loop)` only. DontDestroyOnLoad. (Done)
- [x] Implement `InteractManager` — priority-sorted interactable list, interact input routing. DontDestroyOnLoad. (Done)
- [x] Implement `SaveManager` — persistent singleton; `LoadOrCreate`, `Save`, `CreateFreshSave`, `GetSaveStats`, `HasSave`, `DeleteSave`; `ISaveTarget` pipeline for ScriptableObjects; respawn key storage and resolution. DontDestroyOnLoad. (Done — see `Docs/FeatureSpecs/SaveSystem.md`)
- [x] Wire `Bootstrap`: instantiates five managers in `Awake` (`GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`); calls `SaveManager.LoadOrCreate(0)`, `GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()`, and `GameManager.BeginSceneTransition(firstScene)` in `Start`. (Done)
- [ ] Resolve `HeroController.ResolveDependencies` AssetDatabase fallback — link `HeroConfig` and `HeroAnimationLibrary` via Inspector instead. This masks missing prefab assignments in the editor; no runtime impact in builds.

---

## Milestone 1 — Camera and Scene Foundation

**Goal:** A functional room the hero can move through, with a working camera and room boundary.

- [x] Implement `CameraController`, `CameraTarget`, `CameraBoundsVolume`, and `CameraLockArea` — smooth follow, room bounds, and lock zones. (Done)
- [x] Create `CameraConfig` SO at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`. (Done)
- [ ] Author first test level: platforms, walls, pits, at least two rooms.
- [ ] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Include door variant (requires interact) and auto variant (trigger on entry). `TransitionPoint` should call `GameManager.SetActiveRespawnMarker` on entry so the player respawns at the door they came through if they die in the new room.
- [x] Implement `HazardZone` — supports instant-death and recoverable local hazard recovery modes. (Done)
- [x] Implement `RespawnMarker` — full component with `Key` (string), `RespawnPosition`, and `FacingDirection`. (Done)
- [x] Implement `HazardRespawnMarker` — placed near recoverable hazards and referenced directly by `HazardZone`. (Done; trigger-updated active hazard marker persistence remains deferred.)
- [x] Implement `HeroHealthComponent` — TakeDamage, TakeHazardDamage, TriggerHazardDeath, i-frames, OnHealthChanged, OnDamaged / OnDeath events. (Done)
- [x] Implement hero hurt response in `HeroController`. (Done)
- [x] Implement basic respawn sequence on OnDeath — `GameManager.BeginRespawnSequence` uses `_activeRespawnMarker` (resolved from `SaveManager.ActiveRespawnMarkerKey` on scene load) with nearest-marker fallback. (Done)
- [ ] Validate sensor probes against authored geometry; confirm `terrainLayers` is set correctly.

---

## Milestone 2 — Enemy Loop

**Goal:** At least one enemy the player can fight and kill; a checkpoint to respawn from.

- [x] Implement `EnemyController`, `EnemyStateBlackboard`, `EnemyHealthComponent`, `EnemyRecoil`, `EnemyContactDamage`, `DamageHero`, `EnemyFeedbackController`, `IEnemyBehaviour`, and `MushroomEnemy` patrol behaviour. (Done — `EnemyMotor`, `EnemyPerception`, and full state machine are still planned)
- [x] Create `EnemyConfig` SO for the first enemy type. (`MushroomConfig.asset` exists)
- [x] Implement `IHeroDownslashResponder` on the first enemy (`EnemyHealthComponent` implements it). (Done)
- [ ] Implement `EnemyMotor` — Rigidbody2D velocity control for enemy movement.
- [ ] Implement `EnemyPerception` — overlap / raycast detection; writes `alerted` to blackboard.
- [ ] Implement full `EnemyBehaviour` state machine — Idle → Patrol → Chase → Attack → Hurt → Dead.
- [x] Implement `InteractableBase` base class. (Done)
- [x] Implement `CheckpointInteractable` — sets active runtime respawn marker via `GameManager.SetActiveRespawnMarker` (which propagates the key to `SaveManager`); calls `SaveManager.Save()` on activation. (Done)
- [ ] Implement trigger-updated active hazard respawn markers — optional follow-up that stores the live active marker on `GameManager`; save-key integration remains deferred.
- [ ] Place a checkpoint and at least one enemy in the test level; validate the full loop: fight → die → respawn → fight.
- [x] Wire attack SFX through `AudioManager.PlaySFX` in `HeroAttackModule`. (Done)
- [ ] Add basic action SFX (jump, land, dash, hurt) via `AudioManager.PlaySFX` in the relevant action classes.

---

## Milestone 3 — Ability System

**Goal:** Gated traversal abilities can be unlocked and the gate is data-driven.

- [x] Create `PlayerAbilityState` ScriptableObject. (Done — asset at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`)
- [x] Create `AbilityId` enum. (Done — Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast)
- [x] Create `HeroAbilityConfig` ScriptableObject. (Done)
- [x] Gate dash behind `PlayerAbilityState.dashUnlocked`. (Done)
- [x] Gate wall-slide and wall-jump behind shared `PlayerAbilityState.wallClingUnlocked`. (Done)
- [x] Implement `AbilityPickup` MonoBehaviour. (Done — save/world-state integration is a TODO: persist via `collectedPickupIds` in `WorldSaveData` once `WorldStateRegistry` exists)
- [x] Implement `AbilityGate` MonoBehaviour. (Done — refreshes on enable for loaded state and reacts to `PlayerAbilityState.AbilityChanged` for runtime changes)
- [x] Implement wall-jump (`HeroWallJumpAction`). (Done)
- [x] Implement double-jump first pass (`HeroJumpAction`). (Done)
- [ ] Implement wall-latch / aimed wall launch (`HeroWallLatchAction`).
- [ ] Implement sprint (`HeroSprintAction`).
- [ ] Author a gate in the test level that requires an unlocked ability to pass.

---

## Milestone 4 — Save System

**Goal:** Progress persists across sessions.  
**Status: Foundation complete (Milestone 0 pass). Remaining items are world-state and UX.**

- [x] Implement `SaveManager` and `ISaveTarget` interface. (Done — see `Docs/FeatureSpecs/SaveSystem.md`)
- [x] Implement save data classes: `MetaSaveData`, `PlayerSaveData`, `AbilitySaveData`, `WorldSaveData`, `SaveData`. (Done)
- [x] Implement `SaveSerializer` (JsonUtility), `SaveFileStore` (synchronous + `.bak` backup), `SaveDataMigrator` (version 1 + null normalization), `SaveStats`. (Done)
- [x] Wire `PlayerAbilityState` as `ISaveTarget` — ability flags persist and round-trip correctly. Verified: edit JSON → reload → `PlayerAbilityState` Inspector shows loaded values; `AbilityGate`s refresh to the loaded state. (Done)
- [x] Checkpoint save trigger — `CheckpointInteractable.Interact()` calls `SaveManager.Save()`; `GameManager.SetActiveRespawnMarker` forwards key to `SaveManager` via null-conditional seam. (Done)
- [x] Cross-session respawn marker resolution — `GameManager.ResolveActiveRespawnMarkerFromSave()` resolves saved key to live `RespawnMarker` on every `OnSceneLoaded`. (Done)
- [x] Hero placement at saved position on boot/continue — `PlaceHeroAtSavedRespawnIfRequested()` single-use flag set by `Bootstrap`, consumed on first scene load. Camera snaps to correct position automatically via `TransitionRoutine`. (Done)
- [x] Handle missing/corrupt save file gracefully — fresh state, no crash, clear console log. (Done)
- [x] Application quit auto-save — `Application.quitting` callback; configurable `saveOnApplicationQuit` toggle. (Done)
- [ ] Implement `WorldStateRegistry` SO — visited rooms, defeated enemies, open doors, collected pickups; wire as `ISaveTarget`.
- [ ] Scene-name-driven continue — `BeginSceneTransition(savedScene)` instead of always `firstScene`; route from a "Continue" button on the main menu.
- [ ] `AbilityPickup` persistence — decide autosave policy; wire `collectedPickupIds` round-trip through `WorldStateRegistry`.
- [ ] Multi-slot save UI — slot selection screen on main menu; `LoadOrCreate(chosenSlot)` / `CreateFreshSave(chosenSlot)` routing.

---

## Milestone 5 — Polish Pass

- [ ] HUD: health display wired to `HeroHealthComponent` events.
- [ ] Pause menu wired through `GameManager.Pause()` / `Unpause()`.
- [ ] Main menu scene — loaded from boot on fresh start; "New Game" calls `SaveManager.CreateFreshSave(0)` and `BeginSceneTransition(firstScene)`; "Continue" calls `SaveManager.LoadOrCreate(0)` and `BeginSceneTransition(savedScene)`.
- [ ] Save slot UI — show `SaveStats` (scene, play time, ability count) per slot; `SaveManager.GetSaveStats(slot)` for read-only previews.
- [ ] Play-time accumulation — wire `MetaSaveData.playTimeSeconds` accumulator in `SaveManager.Update()`.
- [ ] Full SFX pass: all hero actions, all enemy actions, UI sounds.
- [ ] Music routing through `GameManager.BeginSceneTransition`.
- [ ] Game feel: hit-pause, screen shake on landing/hit, particle VFX on impacts.
- [ ] Performance: profile and optimise FixedUpdate sensor raycasts for large levels.

---

## Known Technical Debt

- **`SampleScene` is the only scene.** A proper first-level scene should replace it once the test level is authored in Milestone 1. `Bootstrap.firstScene` is hardcoded to `"SampleScene"`.

- **`HeroController.ResolveDependencies` AssetDatabase fallback.** Lines 126–131 and 138–142 fall back to editor-only `AssetDatabase.LoadAssetAtPath<>` calls for `HeroConfig` and `HeroAnimationLibrary`. This masks missing prefab Inspector assignments. Fix: wire both in the Hero prefab Inspector and remove the fallback blocks. Guard: `#if UNITY_EDITOR` ensures no runtime impact in builds, but the silent fallback makes it easy to ship without the prefab correctly wired.

- **`GameManager` health coupling.** `BeginRespawnSequence()` calls `_heroHealth.RestoreFullHealth()` directly. This is a temporary coupling. Long-term, a `PlayerHealthState` ScriptableObject (implementing `ISaveTarget`) should own current health, and `ApplySaveData` should restore health rather than `GameManager` calling into `HeroHealthComponent`. `HitStop` and `BeginRespawnSequence` are also beyond the stated "four responsibilities" boundary — document or relocate when `PlayerHealthState` is implemented.

- **Bootstrap always loads `firstScene`.** Once a main menu scene exists, `Bootstrap` should load the menu, which then routes to `firstScene` (New Game) or `savedScene` (Continue). The save system plumbing for this is already in place (`PlayerSaveData.currentScene` is written on every save).

---

## Current Integration Risks

- **`TransitionPoint` not implemented.** Inter-room navigation requires it. `TransitionPoint` should call `GameManager.SetActiveRespawnMarker` on entry (entry-door respawn) and `GameManager.BeginSceneTransition`. Until it exists, the game is confined to a single scene.

- **Trigger-updated active hazard respawn markers are not implemented.** Direct `HazardZone` → `HazardRespawnMarker` local recovery is implemented. `activeHazardRespawnMarkerKey` is already in `PlayerSaveData`, but the first-pass runtime path intentionally does not read or write it.

- **`AbilityPickup` world-state gap.** `AbilityPickup` unlocks the ability in the current session but the unlocked state only persists if the player reaches a checkpoint before quitting. Once `WorldStateRegistry` tracks `collectedPickupIds`, pickups can be suppressed on scene load if already collected.

- **HUD / UI wiring not implemented.** `HeroHealthComponent` exposes neutral `OnHealthChanged` plus `OnDamaged` / `OnDeath`, but `HealthDisplay` and HUD subscription are not yet in place.

- **Prefab / Inspector wiring.** `Bootstrap` now requires five prefab references (`GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`) and one string field (`firstScene`). `SaveManager` prefab requires the `PlayerAbilityState` asset in its `Ability State` field. Verify all in-editor after any prefab refactor.

- **Engine/API compatibility.** Code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g. `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor only.
