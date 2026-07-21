# Implementation Plan

**Last audited:** 2026-06-05
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
| Enemy AI framework | Foundation validated |
| Interactables / checkpoints | Partial |
| HeroHealthComponent / hurt / death / respawn | Partial |
| Ability unlock system | Done |
| Save / load system | Done (Milestone 0 foundation; world-state and UI deferred) |
| Scene transitions | Done (Milestone 1 — single-scene `LoadSceneAsync`; additive loading and world-state deferred) |
| UI (HUD, menus) | Not started |
| Audio system | Partial |

---

## Immediate — No Code Required

These must happen before any milestone work begins. Both are preconditions for the systems they unblock.

- [x] Add `Interact` action (button) to the Player action map in `InputSystem_Actions.inputactions`. Bindings: E (keyboard), North button (gamepad — not South; South is Jump). Expose `InteractPressedThisFrame` on `HeroInputReader` alongside the existing input signals. (Done — action, bindings, property, and enable/disable wiring all verified present)
- [x] Write `Docs/FeatureSpecs/HUD.md` — boundary-setting spec: what the HUD subscribes to, Canvas hierarchy skeleton, vertical-slice scope (health only). (Done)
- [x] Write `Docs/FeatureSpecs/Audio.md` — boundary-setting spec: `AudioManager` API, call site rules, music routing. (Done)

---

## Milestone 0 — Persistent Infrastructure

**Goal:** All persistent singletons exist and boot correctly before any gameplay scene loads.  
**Status: Complete.**

- [x] Create boot scene (Build Index 0) with a `Bootstrap` MonoBehaviour. (Boot.unity + `Bootstrap` exist)
- [x] Implement `GameManager` — `GameState` enum, `SceneInit` event, `BeginSceneTransition`, `Pause` / `Unpause`. (Done; normal transition lifecycle now delegates to `SceneTransitionManager` + `SceneLoader`; also has `HitStop` and respawn / hazard recovery — see Known Technical Debt)
- [x] Implement `AudioManager` — `PlaySFX(AudioClip)` and `PlayMusic(AudioClip, bool loop)` only. DontDestroyOnLoad. (Done)
- [x] Implement `InteractManager` — priority-sorted interactable list, interact input routing. DontDestroyOnLoad. (Done)
- [x] Implement `SaveManager` — persistent singleton; `LoadOrCreate`, `Save`, `CreateFreshSave`, `GetSaveStats`, `HasSave`, `DeleteSave`; `ISaveTarget` pipeline for ScriptableObjects; respawn key storage and resolution. DontDestroyOnLoad. (Done — see `Docs/FeatureSpecs/SaveSystem.md`)
- [x] Wire `Bootstrap`: instantiates five managers in `Awake` (`GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`); calls `SaveManager.LoadOrCreate(0)`, resolves startup scene from save data, requests saved respawn placement, and starts the transition in `Start`. (Done)
- [ ] Resolve `HeroController.ResolveDependencies` AssetDatabase fallback — link `HeroConfig` and `HeroAnimationLibrary` via Inspector instead. This masks missing prefab assignments in the editor; no runtime impact in builds.

---

## Milestone 1 — Camera and Scene Foundation

**Goal:** A functional room the hero can move through, with a working camera and room boundary.

- [x] Implement `CameraController`, `CameraTarget`, `CameraBoundsVolume`, and `CameraLockArea` — smooth follow, room bounds, and lock zones. (Done)
- [x] Create `CameraConfig` SO at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`. (Done)
- [ ] Author first test level: platforms, walls, pits, at least two rooms.
- [x] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Includes auto-trigger (edge gates) and door variant (`DoorTransitionInteractable`, `requireInteract` toggle). Uses explicit `GateSide` enum; direction is never inferred from GameObject name. WGE now supplies graph-backed scene / port GUID data, while Underbrew still owns runtime scene loading, hero placement, and respawn flow. Per-gate entry tuning lives on the destination `TransitionPoint` (not `HeroConfig`). `HeroSceneEntry` owns all per-gate scripted motion (Left/Right run-in, Top gravity-driven drop, Bottom diagonal throw, Door stand). Door and auto-trigger activation share `TransitionPoint` validation, and scene-entry placement routes through `HeroMotor` (`TeleportTo` / collider-aware feet placement) rather than direct transform writes. Missing destination gates log an error and place the hero at a deterministic fallback (`RespawnMarker`, then authored position). Editor validation exists at `Tools/Project/Validate Transition Gate Links` for WGE passage GUIDs and Build Settings scene links. **Design decision:** `TransitionPoint` does NOT call `GameManager.SetActiveRespawnMarker` — death after a gate crossing returns the player to the last activated checkpoint. `linkedRespawnMarker` is serialized and auto-populated from a child `RespawnMarker` but is reserved for a future policy pass. See `Docs/Integrations/WorldGraphEditorIntegration.md`.
- [x] Implement `HazardZone` — supports instant-death and recoverable local hazard recovery modes. (Done)
- [x] Implement `RespawnMarker` — full component with `Key` (string), `RespawnPosition`, and `FacingDirection`. (Done)
- [x] Implement `HazardRespawnMarker` — placed near recoverable hazards and referenced directly by `HazardZone`. (Done; trigger-updated active hazard marker persistence remains deferred.)
- [x] Implement `HeroHealthComponent` — TakeDamage, TakeHazardDamage, TriggerHazardDeath, i-frames, OnHealthChanged, OnDamaged, OnHazardDamaged / OnDeath events. (Done)
- [x] Implement hero hurt response in `HeroController`. (Done)
- [x] Implement basic respawn sequence on OnDeath — `GameManager.BeginRespawnSequence` uses the saved checkpoint scene + marker key as source of truth, supports cross-scene checkpoint respawn, and falls back to an authored marker in the loaded scene when needed. (Done)
- [x] Hazard recovery hardening pass (2026-05-20):
  - `HeroHealthComponent.GrantTemporaryInvincibility(source, duration)` — public method for recovery i-frames; reuses existing i-frame infrastructure with an explicit duration.
  - `HeroBox.HandleHazard` — null GameManager guard moved before `TakeHazardDamage` to prevent stuck Hurt state in test scenes.
  - `GameManager.BeginHazardRecoverySequence` — grants recovery i-frames (default 0.75 s) immediately, before the first coroutine yield.
  - `GameManager.HazardRecoveryRoutine` — hardened with `try/finally` to guarantee control lock removal and `_respawnOrRecoveryInProgress` reset even if a step throws.
  - `GameManager.FindNearestRespawnMarkerPosition` — returns `nearest.RespawnPosition` (was `transform.position`); falls back to cached `_sceneFallbackPosition` if no `RespawnMarker` exists instead of the hero's current (hazard) position.
  - `GameManager._sceneFallbackPosition` — cached on scene load after initial hero placement; prevents infinite hazard loops when no markers exist in the scene.
  - `HazardRecoveryProfile` SO (`Assets/_Project/Scripts/World/HazardRecoveryProfile.cs`) — per-hazard tuning (`ImpactDelay` 0.18 s, `BlackScreenHold` 0.1 s, `RecoveryIFrameDuration` 0.75 s, `FadeOutDuration` −1, `FadeInDuration` −1). `FadeOutDuration` and `FadeInDuration` default to −1, meaning the camera's own defaults are used; set shorter values (0.35–0.45 s) for a snappier local recovery feel. Referenced by `HazardZone` and carried in `HazardContact`; `GameManager` reads values from the contact, keeping hazard tuning off `GameManager`. **Create the asset** at `Assets/_Project/ScriptableObjects/World/HazardRecoveryProfile.asset` and **assign it on each recoverable `HazardZone`**. If unassigned, built-in fallback values are used.
  - `HazardZone` — `Tooltip`/`Header` attributes added; `OnValidate` warns when `RecoverLocal` has no `HazardRespawnMarker` or no `HazardRecoveryProfile` assigned (profile absence uses defaults, not an error).
- [ ] Validate sensor probes against authored geometry; confirm `terrainLayers` is set correctly.

---

## Milestone 2 — Enemy Loop

**Goal:** At least one enemy the player can fight and kill; a checkpoint to respawn from.

- [x] Implement `EnemyController`, `EnemyStateBlackboard`, `EnemyHealthComponent`, `EnemyRecoil`, `EnemyContactDamage`, `DamageHero`, `EnemyFeedbackController`, `IEnemyBehaviour`, and `MushroomEnemy` patrol behaviour. (Done)
- [x] Create `EnemyConfig` SO for the first enemy type. (`MushroomConfig.asset` exists)
- [x] Implement `IHeroDownslashResponder` on the first enemy (`EnemyHealthComponent` implements it). (Done)
- [x] Implement `EnemyMotor` — Rigidbody2D velocity control for enemy movement. (Done)
- [x] Implement `EnemyPerception` — overlap / raycast detection; exposes events and last known positions. (Done)
- [x] Implement Mushroom enemy state loop — Idle → Patrol → Chase → Attack → Hurt → Dead using `EnemyMotor`, `EnemyPerception`, and optional `EnemyAttackController`. (Done; manually validated in Unity 2026-06-05)
- [x] Implement explicit enemy attack-window architecture — Startup → Active → Recovery → Cooldown, enemy attack hitboxes, duplicate-hit prevention per active window, and interrupt cleanup on hurt/death. (Done; manually validated with Mushroom in Unity 2026-06-05)
- [x] Use Mushroom as the first explicit-attacker validation archetype for authored attack timing, telegraphing, hitboxes, recovery, cooldown, and game-feel interactions with hero hurt / pogo. (`MushroomEnemy` + `Mushroom.prefab` + `Tools/Project/Validate Enemy AI Foundation`; manually validated in Unity 2026-06-05)
- [x] Validate Mushroom contact damage and authored attack coexistence. Confirmed contact damage remains intentional, contact+attack do not double-hit unfairly, later contacts/attacks can damage after cooldown/i-frames, death disables both damage paths, downslash pogo remains reliable, and no hero feel values changed. (Manual Unity validation 2026-06-05)
- [x] Implement `InteractableBase` base class. (Done)
- [x] Implement `CheckpointInteractable` — sets active runtime respawn marker via `GameManager.SetActiveRespawnMarker` (which propagates the key to `SaveManager`); calls `SaveManager.Save()` on activation. (Done)
- [ ] Implement trigger-updated active hazard respawn markers — optional follow-up that stores the live active marker on `GameManager`; save-key integration remains deferred.
- [ ] Place a checkpoint and at least one enemy in the test level; validate the full loop: fight → die → respawn → fight.
- [x] Wire attack SFX through `AudioManager.PlaySFX` in `HeroAttackModule`. (Done)
- [x] Add basic action SFX (jump, land, dash, hurt) via `HeroAudioController` in the relevant action classes. (Done — jump/double-jump in `HeroJumpAction`, dash in `HeroDashAction`, land/hurt/death in `HeroController`; also wall-jump, wall-slide, footsteps, attack slash, and terrain impact covered)

**Enemy AI future boundaries:** wake/sleep activation, defeated-enemy persistence, pooling, bosses, and additional enemy archetypes remain planned/not implemented. The validated foundation currently targets Mushroom only.

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
- [x] Implement `SaveSerializer` (JsonUtility), `SaveFileStore` (synchronous + `.bak` backup), `SaveDataMigrator` (version 2 + null normalization + respawn-scene migration), `SaveStats`. (Done)
- [x] Wire `PlayerAbilityState` as `ISaveTarget` — ability flags persist and round-trip correctly. Verified: edit JSON → reload → `PlayerAbilityState` Inspector shows loaded values; `AbilityGate`s refresh to the loaded state. (Done)
- [x] Checkpoint save trigger — `CheckpointInteractable.Interact()` calls `SaveManager.Save()`; `GameManager.SetActiveRespawnMarker` forwards scene + marker key to `SaveManager` via an atomic plain-string seam. (Done)
- [x] Cross-session / cross-scene respawn marker resolution — `GameManager.ResolveActiveRespawnMarkerFromSave()` resolves saved key only in the saved checkpoint scene; normal death can load that scene before placing the hero. (Done)
- [x] Hero placement at saved position on boot/continue — `PlaceHeroAtSavedRespawnIfRequested()` single-use flag set by `Bootstrap`, consumed on first scene load. Camera snaps to correct position automatically via `TransitionRoutine`. (Done)
- [x] Handle missing/corrupt save file gracefully — fresh state, no crash, clear console log. (Done)
- [x] Application quit auto-save — `Application.quitting` callback; configurable `saveOnApplicationQuit` toggle. (Done)
- [ ] Implement `WorldStateRegistry` SO — visited rooms, defeated enemies, open doors, collected pickups; wire as `ISaveTarget`.
- [x] Scene-name-driven boot continue — `Bootstrap` now resolves startup scene from `activeRespawnSceneName`, then `currentScene`, then `firstScene`. (Done; main-menu Continue button still deferred.)
- [ ] `AbilityPickup` persistence — decide autosave policy; wire `collectedPickupIds` round-trip through `WorldStateRegistry`.
- [ ] Multi-slot save UI — slot selection screen on main menu; `LoadOrCreate(chosenSlot)` / `CreateFreshSave(chosenSlot)` routing.

**Future compatibility note:** Death-drop / shade / resource recovery is not part of this pass. When added, it should capture death scene + death position before `GameManager` loads the checkpoint scene; do not reuse `activeRespawnSceneName` for death-drop location.

---

## Milestone 5 — Polish Pass

- [ ] HUD: health display wired to `HeroHealthComponent` events.
- [ ] Pause menu wired through `GameManager.Pause()` / `Unpause()`.
- [ ] Main menu scene — loaded from boot on fresh start; "New Game" calls `SaveManager.CreateFreshSave(0)` and `BeginSceneTransition(firstScene)`; "Continue" calls `SaveManager.LoadOrCreate(0)` and `BeginSceneTransition(SaveManager.GetStartupScene(firstScene))`.
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

- **`GameManager` health coupling.** `BeginRespawnSequence()` calls `_heroHealth.RestoreFullHealth()` and grants default post-respawn i-frames directly. This is a temporary coupling. Long-term, a `PlayerHealthState` ScriptableObject (implementing `ISaveTarget`) should own current health, and `ApplySaveData` should restore health rather than `GameManager` calling into `HeroHealthComponent`. `HitStop` and `BeginRespawnSequence` are also beyond the stated "four responsibilities" boundary — document or relocate when `PlayerHealthState` is implemented.

- **Bootstrap still has no main-menu routing.** Boot now resolves a saved startup scene directly, but once a main menu exists, `Bootstrap` should load the menu; the menu should route to `firstScene` for New Game or the saved startup scene for Continue.

- **`TransitionPoint` does not update active `RespawnMarker`.** By current design decision, dying after crossing a gate returns the hero to the last activated checkpoint, not to the entry door. The `linkedRespawnMarker` field on `TransitionPoint` is reserved for a future policy pass if entry-door respawn is desired. See `Docs/Architecture.md` § Checkpoint and Respawn Markers.

- **`HitStopRoutine` has no `try/finally` cleanup.** If an exception is thrown inside the coroutine, `Time.timeScale` could be left in a bad state. `TransitionRoutine`, `RespawnRoutine`, and `HazardRecoveryRoutine` use `try/finally` cleanup.

---

## Current Integration Risks

- **World Graph Editor upgrade risk.** WGE is integrated and working as the scene graph / port authoring layer, but Underbrew runtime ownership must be preserved during plugin updates. Before importing the next WGE version, protect the current state with a tag or branch such as `wge-working-before-upstream-update`, then update on `upgrade/wge-next-version`. Re-check vendor-file patches, especially relocated-path support under `Assets/Plugins/WorldGraphEditor`, `WGEAssetPathUtility.cs`, editor asset-loading utilities, and `Resources/TransitionManager.prefab` with `_autoLoad` disabled. Evaluate upcoming official custom `TransitionManager` support before doing deeper custom runtime refactors. See `Docs/Integrations/WorldGraphEditorIntegration.md`.

- **Trigger-updated active hazard respawn markers are not implemented.** Direct `HazardZone` → `HazardRespawnMarker` local recovery is implemented. `activeHazardRespawnMarkerKey` is already in `PlayerSaveData`, but the first-pass runtime path intentionally does not read or write it.

- **`AbilityPickup` world-state gap.** `AbilityPickup` unlocks the ability in the current session but the unlocked state only persists if the player reaches a checkpoint before quitting. Once `WorldStateRegistry` tracks `collectedPickupIds`, pickups can be suppressed on scene load if already collected.

- **HUD / UI wiring not implemented.** `HeroHealthComponent` exposes neutral `OnHealthChanged` plus `OnDamaged` / `OnDeath`, but `HealthDisplay` and HUD subscription are not yet in place.

- **Prefab / Inspector wiring.** `Bootstrap` now requires five prefab references (`GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`) and one string field (`firstScene`). `SaveManager` prefab requires the `PlayerAbilityState` asset in its `Ability State` field. Verify all in-editor after any prefab refactor.

- **Engine/API compatibility.** Code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g. `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor only.
