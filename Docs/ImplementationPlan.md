# Implementation Plan

**Last audited:** 2026-07-27
This is a living document. Update when milestones complete or priorities shift.

---

## Status

| Layer | Status |
|---|---|
| Hero movement (walk, run, jump, coyote, dash, wall-slide) | Done |
| Directional melee combat (Side / Up / Down swings) | Done (result contract, attacker-side resource generation, and Bind spending migration) |
| Animancer-driven animation (locomotion, action states) | Done |
| Input System integration (KB+Mouse, Gamepad) | Done |
| ScriptableObject tuning pipeline | Done |
| Boot scene / persistent infrastructure | Done |
| Camera system | Done |
| Enemy AI framework | Foundation validated |
| Boss encounters | Phase 2A playable Undead Executioner vertical slice implemented; Phase 2A.5 hardening pass done; polish/feel review pending |
| Interactables / checkpoints | Partial |
| HeroHealthComponent / hurt / death / respawn | Partial |
| Ability unlock system | Done |
| Save / load system | Done (Milestone 0 foundation + World Persistence Phase 1/2/3; multi-slot UI deferred) |
| Scene transitions | Done (Milestone 1 — single-scene `LoadSceneAsync`; additive loading and world-state deferred) |
| UI (HUD, menus) | HUD presentation foundation implemented; Package A1 (Pause menu, MenuRoot, input foundation, Sandbox) implemented, corrected, and automated-tested with confirmed EditMode+PlayMode execution; Sandbox flows live-verified in Play Mode; production Boot-path interactive manual validation pending; Package A2/B not started |
| Audio system | Partial |

---

## Authoritative UI Roadmap

The approved planning record is `Docs/ImplementationPlans/UIImplementationPlan.md`. Authoritative
contracts now live in:

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/FeatureSpecs/HUD.md`
- `Docs/FeatureSpecs/Abilities.md`

Package A1 (root Pause menu, persistent MenuRoot/input foundation, Sandbox) has been implemented,
corrected, and covered by automated EditMode and PlayMode tests — both confirmed executing via the
real Unity Test Runner — plus a passing `UIFoundationValidator` pass. The Sandbox-only Options
preview and Quit callback flows were live-verified in a real Play Mode session. See the Package A1
entry below for exact scope and what remains unvalidated (interactive manual checks against the
production Boot path). Package A2 and Package B have not been performed or validated. The
implemented persistent HUD foundation is retained as an existing dependency, not counted as new menu
work.

### Documentation/design contract

**Status: Approved planning direction; authoritative documentation updated.**

- [x] High-level UI product decisions confirmed.
- [x] UI implementation proposal reviewed.
- [x] Authoritative UI ownership, pause/menu flow, Gear, and Sandbox specifications created.
- [ ] No new UI implementation phase has been validated.

### Prerequisite — New-game ability defaults

**Status: Planned gameplay-state correction before Package A2 production validation.**

#### New-game ability defaults mismatch

**Current state:** `PlayerAbilityState` field defaults, `AbilitySaveData`, and
`PlayerAbilityState.ResetToDefaults()` currently begin with Dash and Wall Cling unlocked. The
mutable `PlayerAbilityState.asset` also has Double Jump and Bind unlocked. These values are current
implementation/development state, not the intended product design.

**Confirmed intended state:** a new player starts with no unlocked permanent abilities. Dash, Wall
Cling, Sprint, Wall Latch, Double Jump, Drift Cloak, Spirit Cast, and Bind are acquired through
progression. The initial Gear collection may legitimately be empty.

**Required future work:**

- [ ] Update authoritative new-game and reset defaults so all abilities begin locked.
- [ ] Update `AbilitySaveData` defaults where required.
- [ ] Update `PlayerAbilityState.ResetToDefaults()` and field initializers where required.
- [ ] Audit `Bootstrap`, fresh-save creation, migration, and other new-game initialization paths.
- [ ] Reset or recreate mutable development-state assets without treating them as design defaults.
- [ ] Review compatibility and migration behavior for existing development saves.
- [ ] Add tests proving a new save and reset-to-defaults state contain no unlocked abilities.
- [ ] Add tests proving existing saves preserve their explicitly stored unlock values.

This prerequisite is owned by the ability/save foundation, not Gear UI. It must be completed before
the Package A2 production Gear vertical slice is considered valid. No correction is performed by
this documentation update.

### Package A — Internal UI foundation vertical slice

**Status: Planned; not yet shippable.**

#### Package A1 — UI foundation

**Status: Implemented, corrected, and automated-tested with confirmed EditMode+PlayMode execution;
Sandbox flows live-verified in Play Mode; production Boot-path interactive manual validation not yet
performed.**

- [x] Development-only UI Sandbox (`Assets/_Project/Scenes/Development/UISandbox.unity`, excluded
  from Build Settings) and shared reusable UI prefabs (`Assets/_Project/Prefabs/UI/PauseMenuScreen.prefab`,
  `ConfirmationModal.prefab`, reusing the existing `HealthSlotView.prefab`). Sandbox uses isolated
  runtime `PlayerHealthState`/`PlayerResourceState` instances and fixture `EnemyHealthComponent`
  sources for boss HUD preview; no production save/manager access.
- [x] Persistent `MenuRoot` composition under `_GameCameras.prefab` (`EventSystem` +
  `InputSystemUIInputModule`, `RootInterfaceLayer`, `ModalLayer`, `UIFlowController`).
- [x] One project-owned `EventSystem` and `InputSystemUIInputModule`, wired to the existing `UI`
  action map.
- [x] Approved dedicated `System` action map (`Pause`, `GameplayMenu`) added to
  `InputSystem_Actions.inputactions`; `UIFlowController` is its sole owner/enabler.
- [x] Full transition blocking plus an initial 1.0-second unscaled post-transition lockout
  (`UIFlowController.UpdateTransitionLockout`, observing the falling edge of
  `GameManager.IsSceneTransitioning` while `State == Playing`).
- [x] Immediate rejection of blocked requests; no queued or pending root opens (edge-triggered
  `WasPressedThisFrame` polling — a request is either accepted this frame or discarded).
- [x] Independent Pause/Gameplay Menu release rearming (structural: each action's press-edge is
  independent of the other; verified by input-leakage tests).
- [x] Hero command-input suspension through `HeroController` (`SuspendGameplayInput`,
  `ClearTransientGameplayInput`, `BeginGameplayInputResume` forwarding facade).
- [x] `HeroInputReader` sampling suspension, buffer clearing, per-command held-state tracking, and
  fresh-press resume behavior for Jump/Attack/Dash/Sprint/Interact/Bind; continuous movement
  (`MoveVector`) exempt from release gating and resumes live.
- [x] Root Pause menu: functional Continue, authored-but-gated Options (non-interactable in
  production; Sandbox can preview it enabled via a labelled Sandbox-only placeholder child), and
  Quit-to-Main-Menu confirmation modal, non-interactable in production while its typed request seam
  is disabled (`PauseMenuScreen.QuitToMainMenuRequested`) — no save, no scene load, no frontend.
  Pressing Pause while the Quit modal is open closes only the modal (approved modal-first behavior);
  a later Pause press at the bare root closes normally.
- [x] Correction pass: production/Sandbox `InputSystemUIInputModule` action references wired via
  durable `UI`-map `InputActionReference` sub-assets (previously all null despite an assigned
  actions asset); runtime navigation rebuild so disabled Options/Quit are skipped and never leave
  focus stuck; `ConfirmationModal` selection fallback to the parent root when the invoker becomes
  invalid; pause-safe `UIFlowController.OnDestroy` teardown; `HeroInputReader` held-state checks
  switched from `InputAction.IsPressed()` to raw control actuation (`IsActuated()`), fixing a
  confirmed input-leakage defect where Jump/Attack/Interact/Bind/Dash could re-fire a synthetic
  press immediately on resume if held through the close (`InputAction.IsPressed()` does not
  resynchronize within the same frame after `Enable()` following `Disable()` while a control is
  still held — only `WasPressedThisFrame()` does, one frame too late for disarm gating).
- [x] Automated coverage, all confirmed via the real Unity Test Runner this pass: `UIFlowControllerTests`
  (EditMode, 18/18), `PauseMenuScreenTests` (EditMode, 14/14), `ConfirmationModalTests` (EditMode,
  4/4), `UISandboxControllerFixtureTests` (EditMode, 4/4) — full EditMode suite 365/365, zero
  PlayMode tests included. `HeroInputSuspensionPlayModeTests` (PlayMode, 14/14 — 9 original plus 5
  added this pass) confirmed executing and passing via the real PlayMode Test Runner after fixing
  `Underbrew.UI.PlayModeTests.asmdef`'s `includePlatforms` (was `["Editor"]`, now `[]` matching the
  working `Underbrew.Camera.PlayModeTests.asmdef` convention) — full PlayMode suite 29/29 (14 UI +
  15 unrelated camera tests, confirming the asmdef fix did not affect other discovery).
  `UIFoundationValidator` (`Tools/Project/Validate UI Foundation`) passed, extended this pass to
  validate input-module action wiring (confirmed to fail when a reference is deliberately cleared)
  and Sandbox-only-component production exclusion.
- [x] Sandbox correction pass: Options preview placeholder (`SandboxOptionsPreviewPanel`) and Quit
  callback status label (`SandboxQuitCallbackStatus`) implemented and live-verified in a real Play
  Mode session; Bonus-health and resource fixture presets made deterministic across repeated clicks;
  runtime boss-fixture `EnemyConfig` instances tracked and destroyed when superseded.
- [ ] Interactive manual validation against the **production Boot path** (keyboard/controller/mouse
  Pause+Gameplay Menu open/close in the real gameplay scene, held-input-through-transition, real
  room-transition timing) not performed this session — the Sandbox-only flows above were live-driven
  in Play Mode, but the production Boot scene was not loaded.

### Package A2 — Gameplay Menu and Gear

**Status: Planned; blocked for production validation by the new-game-default prerequisite.**

- [ ] Gameplay Menu shell with session-only last-valid-tab memory.
- [ ] Production tab filtering: Gear only in the first production-backed slice.
- [ ] Planned Gear display definitions/catalogue backed read-only by `PlayerAbilityState`.
- [ ] Acquired-only presentation with no silhouettes, unknown totals, or future placeholders.
- [ ] Intentional, selectable true-new-game empty Gear state.
- [ ] Details presentation and device-aware control hints.
- [ ] Production layout selected after Sandbox comparison of authored groups, authored tableau, and
  list/grid fallback.
- [ ] Gear visibility, empty-state, selection, save-load, catalogue, and validator coverage.

### Package B — First functional settings integration

**Status: Planned.**

- [ ] Project AudioMixer and Master/Music/SFX routing.
- [ ] Profile-independent settings persistence and startup application.
- [ ] Functional audio Options screen.
- [ ] UI navigation/confirm/cancel audio through `AudioManager`.
- [ ] Decide whether return-to-main-menu integration is approved or remains deferred.

Package A plus Package B form the first full player-facing Pause/settings milestone, except that
Quit to Main Menu remains incomplete until a frontend destination and save policy are approved.

### Deferred UI and adjacent systems

- Functional Quit to Main Menu and its save/discard/reload policy.
- Main menu, New Game/Continue frontend routing, and save slots.
- Production Tools, Satchel, Recipes, Tasks, Journal, and Map tabs.
- Dual-access Map: hold the future Map action for a non-pausing Local Quick Map; double-tap the same
  action for the pausing Gameplay Menu Full Map tab. Tab is provisionally reserved for Map on
  keyboard. Map remains outside Package A.
- Notifications and acquisition presentation.
- HUD final artwork and feedback.
- Complete accessibility, localization, input rebinding, glyph, display, and safe-area support.

---

## Immediate — No Code Required

These must happen before any milestone work begins. Both are preconditions for the systems they unblock.

- [x] Add `Interact` action (button) to the Player action map in `InputSystem_Actions.inputactions`. Bindings: E (keyboard), North button (gamepad — not South; South is Jump). Expose `InteractPressedThisFrame` on `HeroInputReader` alongside the existing input signals. (Done — action, bindings, property, and enable/disable wiring all verified present)
- [x] Write `Docs/FeatureSpecs/HUD.md` — state ownership, direct event subscriptions, persistent Canvas boundary, and health/resource presentation rules. (Done)
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
- [x] Camera Phase 1 — deterministic highest-priority/newest-entry lock registration, correct
  per-axis room/lock legal-region intersection, explicit world-zero look overrides, layered
  underlying framing, source-owned timed freeze handles, immediate already-inside refresh,
  scene-lifetime cleanup, focused validation, and discoverable EditMode/PlayMode coverage.
  (Done 2026-07-25.)
- [x] Camera Phase 2 — data-driven lock-transition causes (`SceneStart`/`FollowToLock`/
  `LockToLock`/`LockToFollow`/`OverrideReleased`) owned by `CameraConfig` with optional
  per-`CameraLockArea` override, Scene-view gizmos on `CameraLockArea`/`CameraBoundsVolume`,
  a read-only `CameraControllerEditor` runtime diagnostics Inspector, and an extended
  Camera Phase 1 validator. (Done 2026-07-25; human camera-feel approval and the
  `SampleScene4` fixed-centre-framing decision remain outstanding — see
  `Docs/FeatureSpecs/Camera.md`.)
- [x] Scene-entry camera readiness and reveal sequencing — removed the arbitrary post-rebind
  wait, added bounded `GameCameras` readiness with immediate target/rendered-camera positioning,
  bounds/ordinary-lock overlap refresh, stale smoothing/transition reset, actionable direct-snap
  fallback, and transition-specific freeze-release handoff. Fade-in and `HeroSceneEntry` motion now
  begin only after readiness, preventing the deferred scene-start/`OverrideReleased` camera snap
  after entry motion. (Done 2026-07-25.)
- [x] Scene-entry reveal — render-valid framing + live-follow walk-in. Fixed the still-visible reveal
  defect after the item above: (1) `HeroMotor.TeleportTo` now writes the hero `Transform`, not only
  `Rigidbody2D.position`, so scene-entry readiness frames the true gate instead of the stale
  pre-teleport position (the previous readiness verify was self-consistent against a stale transform,
  so it passed while the camera framed the wrong spot and then catch-glided onto the hero on reveal);
  (2) the transition freeze is now released at the reveal seam — after fade-in starts revealing and
  before entry motion — so the camera follows the directional walk-in live with no post-entry
  correction; (3) entry motion begins once the fade crosses a small visibility threshold rather than
  under a fully black screen. Verified frame-by-frame through the real Boot path
  (`SampleScene ⇄ SampleScene2`, both directions). (Done 2026-07-25.)
- [x] Camera Phase 3 — source-owned presentation requests on `GameCameras` (unique ID, owning
  source, priority + newest-sequence selection, scene/persistent lifetime, optional duration,
  idempotent release, destroyed-source/destroyed-target/scene-unload pruning, in-place re-authoring),
  `FocusTarget`/`FocusWorldPoint`/`FrameTargets` framing, field-of-view automatic zoom with padding,
  shared and per-request limits, a room-derived zoom cap, asymmetric damping and hysteresis, three
  new presentation transition causes plus per-request blend overrides and clip-weight blending, a
  Timeline adapter (`CameraPresentationTrack`/`Clip`/`Behaviour`/`MixerBehaviour`/`Receiver`), the
  `BossEncounterCameraPresenter` vertical slice in `SampleScene4`, read-only diagnostics, Scene-view
  gizmos, `Tools/Project/Validate Camera Phase 3`, and EditMode + PlayMode coverage.
  (Done 2026-07-26. **Human camera-feel review is still outstanding** — see the manual checklist in
  `Docs/FeatureSpecs/Camera.md`. The `SampleScene4` arena-lock/room sizing decision from Phase 2
  remains open and currently limits boss presentation to zoom rather than pans.)
- [ ] Author first test level: platforms, walls, pits, at least two rooms.
- [x] Implement `TransitionPoint` — wired to `GameManager.BeginSceneTransition`. Includes auto-trigger (edge gates) and door variant (`DoorTransitionInteractable`, `requireInteract` toggle). Uses explicit `GateSide` enum; direction is never inferred from GameObject name. WGE now supplies graph-backed scene / port GUID data, while Underbrew still owns runtime scene loading, hero placement, and respawn flow. Per-gate entry tuning lives on the destination `TransitionPoint` (not `HeroConfig`). `HeroSceneEntry` owns all per-gate scripted motion (Left/Right run-in, Top gravity-driven drop, Bottom diagonal throw, Door stand). Door and auto-trigger activation share `TransitionPoint` validation, and scene-entry placement routes through `HeroMotor` (`TeleportTo` / collider-aware feet placement) rather than direct transform writes. Missing destination gates log an error and place the hero at a deterministic fallback (`RespawnMarker`, then authored position). Editor validation exists at `Tools/Project/Validate Transition Gate Links` for WGE passage GUIDs and Build Settings scene links. **Design decision:** `TransitionPoint` does NOT call `GameManager.SetActiveRespawnMarker` — death after a gate crossing returns the player to the last activated checkpoint. `linkedRespawnMarker` is serialized and auto-populated from a child `RespawnMarker` but is reserved for a future policy pass. See `Docs/Integrations/WorldGraphEditorIntegration.md`.
- [x] Implement `HazardZone` — supports instant-death and recoverable local hazard recovery modes. (Done)
- [x] Implement `RespawnMarker` — full component with `Key` (string), `RespawnPosition`, and `FacingDirection`. (Done)
- [x] Implement `HazardRespawnMarker` — placed near recoverable hazards and referenced directly by `HazardZone`. (Done; trigger-updated active hazard marker persistence remains deferred.)
- [x] Implement `HeroHealthComponent` — scene-side facade for TakeDamage, TakeHazardDamage, TriggerHazardDeath, i-frames, OnDamaged, OnHazardDamaged, and OnDeath; authoritative values and neutral change notifications live in `PlayerHealthState`. (Done; ownership migrated.)
- [x] Implement hero hurt response in `HeroController`. (Done)
- [x] Implement basic respawn sequence on OnDeath — `GameManager.BeginRespawnSequence` uses the saved checkpoint scene + marker key as source of truth, supports cross-scene checkpoint respawn, and falls back to an authored marker in the loaded scene when needed. (Done)
- [x] Hazard recovery hardening pass (2026-05-20):
  - `HeroHealthComponent.GrantTemporaryInvincibility(source, duration)` — public method for recovery i-frames; reuses existing i-frame infrastructure with an explicit duration.
  - `HeroBox.HandleHazard` — null GameManager guard moved before `TakeHazardDamage` to prevent stuck Hurt state in test scenes.
  - `GameManager.BeginHazardRecoverySequence` — grants recovery i-frames (default 0.75 s) immediately, before the first coroutine yield.
  - `GameManager.HazardRecoveryRoutine` — hardened with `try/finally` to guarantee control lock removal and `_respawnOrRecoveryInProgress` reset even if a step throws.
  - `GameManager.FindNearestRespawnMarkerPosition` — returns `nearest.RespawnPosition` (was `transform.position`); falls back to cached `_sceneFallbackPosition` if no `RespawnMarker` exists instead of the hero's current (hazard) position.
  - `GameManager._sceneFallbackPosition` — cached on scene load after initial hero placement; prevents infinite hazard loops when no markers exist in the scene.
  - `HazardRecoveryProfile` SO (`Assets/_Project/Scripts/Hazard/HazardRecoveryProfile.cs`) — per-hazard tuning (`ImpactDelay` 0.18 s, `BlackScreenHold` 0.1 s, `RecoveryIFrameDuration` 0.75 s, `FadeOutDuration` −1, `FadeInDuration` −1). `FadeOutDuration` and `FadeInDuration` default to −1, meaning the camera's own defaults are used; set shorter values (0.35–0.45 s) for a snappier local recovery feel. Referenced by `HazardZone` and carried in `HazardContact`; `GameManager` reads values from the contact, keeping hazard tuning off `GameManager`. **Create the asset** at `Assets/_Project/ScriptableObjects/World/HazardRecoveryProfile.asset` and **assign it on each recoverable `HazardZone`**. If unassigned, built-in fallback values are used.
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

**Enemy AI future boundaries:** wake/sleep activation, pooling, a broader boss roster, and additional
ordinary enemy archetypes remain planned. The reusable boss encounter foundation and first
Undead Executioner actor are implemented outside the Mushroom foundation validator. Ordinary-enemy
world persistence (timed suppression on scene re-initialization) is implemented — see
`EnemyPersistence` in Architecture.md.

---

## Milestone 3 — Ability System

**Goal:** Gated traversal abilities can be unlocked and the gate is data-driven.

**Naming note:** this is unrelated to "World Persistence Phase 3" (doors/switches/breakables/room visitation — see `Docs/ImplementationPlans/WorldPersistence.md` and Milestone 4 below). The two share a number by coincidence only.

- [x] Create `PlayerAbilityState` ScriptableObject. (Done — asset at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`)
- [x] Create `AbilityId` enum. (Done — Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast, Bind)
- [x] Create `HeroAbilityConfig` ScriptableObject. (Done)
- [x] Gate dash behind `PlayerAbilityState.dashUnlocked`. (Done)
- [x] Gate wall-slide and wall-jump behind shared `PlayerAbilityState.wallClingUnlocked`. (Done)
- [x] Implement `AbilityPickup` MonoBehaviour. (Done — world-state integration done in World Persistence Phase 2: reconciles `PlayerAbilityState` against `WorldStateRegistry.collectedPickupIds` on initialization and records collection through both. See `Docs/ImplementationPlans/WorldPersistence.md`.)
- [x] Implement `AbilityGate` MonoBehaviour. (Done — refreshes on enable for loaded state and reacts to `PlayerAbilityState.AbilityChanged` for runtime changes)
- [x] Implement wall-jump (`HeroWallJumpAction`). (Done)
- [x] Implement double-jump first pass (`HeroJumpAction`). (Done)
- [ ] Implement wall-latch / aimed wall launch (`HeroWallLatchAction`).
- [ ] Implement sprint (`HeroSprintAction`).
- [ ] Author a gate in the test level that requires an unlocked ability to pass.

---

## Milestone 4 — Save System

**Goal:** Progress persists across sessions.  
**Status: Foundation and World Persistence Phase 1/2/3 complete. Remaining item is multi-slot save UI.**

- [x] Implement `SaveManager` and `ISaveTarget` interface. (Done — see `Docs/FeatureSpecs/SaveSystem.md`)
- [x] Implement save data classes: `MetaSaveData`, `PlayerSaveData`, `AbilitySaveData`, `WorldSaveData`, `SaveData`. (Done)
- [x] Implement `SaveSerializer` (JsonUtility), `SaveFileStore` (synchronous + `.bak` backup), `SaveDataMigrator` (version 3 + null normalization + respawn-scene migration), `SaveStats`. (Done)
- [x] Wire `PlayerAbilityState` as `ISaveTarget` — ability flags persist and round-trip correctly. Verified: edit JSON → reload → `PlayerAbilityState` Inspector shows loaded values; `AbilityGate`s refresh to the loaded state. (Done)
- [x] Add `PlayerHealthState` and `PlayerResourceState` save foundations — version-3 schema sections, asset-defined fresh defaults, loaded-value normalization, and neutral state-application notifications. Health gameplay ownership, attacker-side resource generation, Bind spending, and the HUD presentation foundation are wired. Lifecycle policy (New Game reset, zero-health Continue normalization, death/hazard/checkpoint/transition resource and bonus-health rules) is implemented in Milestone 7 — see `Docs/FeatureSpecs/PlayerHealthAndResource.md`. (Done.)
- [x] Migrate gameplay health ownership to `PlayerHealthState` — `HeroHealthComponent` retains i-frames and contextual damage/hazard/death events without mirrored value storage; scene initialization preserves loaded health; normal respawn restores once and clears bonus health through the facade. (Done in health ownership Milestone 2; Hero prefab assignment verified.)
- [x] Add attacker-side resource generation — `HeroAttackAction` reads accepted, resource-eligible `HeroAttackResult`s and applies each module's `None`, `PerSuccessfulTarget`, or `FirstSuccessfulHitPerAttack` policy to the injected `PlayerResourceState`. (Done in resource generation Milestone 4; Bind spending and HUD presentation are implemented. Partial-resource decay remains an intentional scope exclusion, not pending work — see `Docs/FeatureSpecs/PlayerHealthAndResource.md` Deferred Work.)
- [x] Add grounded hold-to-heal Bind — `HeroBindAction` uses `PlayerResourceConfig`, consumes the configured cost only after the uninterrupted hold completes, heals normal health once, and cancels safely on input/state interruptions, including on death, hazard, scene transition, and control-lock cancellation triggers. (Done in Milestone 5; the temporary direct-keyboard Bind fallback in `HeroInputReader` was removed in Milestone 7 once the real Input System Bind action was verified wired.)
- [x] Generalize `SaveManager` registration to an Inspector-ordered `ScriptableObject` target list. Null, invalid, and duplicate entries are warned and skipped; future `WorldStateRegistry` registration requires only an Inspector assignment. (Done)
- [x] Checkpoint save trigger — `CheckpointInteractable.Interact()` calls `SaveManager.Save()`; `GameManager.SetActiveRespawnMarker` forwards scene + marker key to `SaveManager` via an atomic plain-string seam. (Done)
- [x] Cross-session / cross-scene respawn marker resolution — `GameManager.ResolveActiveRespawnMarkerFromSave()` resolves saved key only in the saved checkpoint scene; normal death can load that scene before placing the hero. (Done)
- [x] Hero placement at saved position on boot/continue — `PlaceHeroAtSavedRespawnIfRequested()` single-use flag set by `Bootstrap`, consumed on first scene load. Camera snaps to correct position automatically via `TransitionRoutine`. (Done)
- [x] Handle missing/corrupt save file gracefully — fresh state, no crash, clear console log. (Done)
- [x] Application quit auto-save — `Application.quitting` callback; configurable `saveOnApplicationQuit` toggle. (Done)
- [x] Implement `WorldStateRegistry` SO — visited rooms, defeated encounters, permanent object states, collected pickups; wired as `ISaveTarget` on `_SaveManager.prefab`. (Done — World Persistence Phase 1, 2026-07-23. Ordinary placed enemy persistence is the complete vertical slice: `EnemyPersistence` + `EnemyPersistenceMode` + `EnemyConfig.respawnDuration`, wired onto `Mushroom.prefab` and all 9 placed instances across `SampleScene`/`SampleScene2`/`SampleScene3`.)
- [x] World Persistence Phase 2 — normal-death lifecycle integration and pickup reconciliation. (Done. `GameManager.BeginRespawnSequence` clears `ResetRespawnableEnemyDeaths()`/`ResetUntilDeathState()` exactly once per normal death, before the checkpoint scene begins loading; checkpoint activation and recoverable-hazard reposition are unchanged and never clear transient records; Continue/New Game/slot change already worked for free via `WorldStateRegistry.ApplySaveData`. `AbilityPickup` reconciles against `WorldStateRegistry.collectedPickupIds` in favor of `PlayerAbilityState`. `WorldPersistenceValidator` extended to cover `AbilityPickup`.)
- [x] World Persistence Phase 3 — doors, switches, breakables, and room visitation (2026-07-23). `PersistentDoor` (permanent shortcut gate), `PersistentSwitch` (one-shot lever, configurable `PersistenceLifetime`), and `PersistentBreakable` (implements `IHeroAttackReceiver` directly) are implemented and validated with a vertical slice in `SampleScene`. `WorldPersistenceValidator` extended with per-type local checks, a conflicting-participants check, and a single unified cross-type global-ID-uniqueness pass (previously separate for enemies vs. pickups). New `Breakable` Physics2D layer added to `HeroConfig.attackHitLayers`/`terrainLayers`. Coordinated encounter persistence is implemented by the Boss Encounter foundation and exercised by the Undead Executioner in `SampleScene4`.
- [x] World Persistence Phase 3.1 — Silksong-comparison refinement pass, pre-boss-milestone (see the Silksong Persistent World Objects research report). Replaced `GameManager.OnSceneLoaded`'s `MarkRoomVisited(scene.name)` with `RoomVisitReporter`, an authored-`roomId` participant placed once per gameplay scene (`room_sample_01`/`02`/`03`), so room identity no longer depends on `.unity` filenames; `GameManager` no longer touches `WorldStateRegistry` for room visitation. `WorldPersistenceValidator` extended with a parallel room-reporter pass (missing-reporter detection — exactly one per enabled Build Settings scene except `Boot` — plus an independent room-ID cross-scene uniqueness check). `HeroAttackResult.Damaged`/`Killed` gained an optional `resourceEligible` parameter (default `true`); `PersistentBreakable` now passes `false`, so breaking environmental objects no longer awards hero combat resource by default. `PersistentDoor`'s scope-clarifying header comment converted to an XML doc comment. See `Docs/ImplementationPlans/WorldPersistence.md` for the full rationale.
- [x] Scene-name-driven boot continue — `Bootstrap` now resolves startup scene from `activeRespawnSceneName`, then `currentScene`, then `firstScene`. (Done; main-menu Continue button still deferred.)
- [ ] Multi-slot save UI — slot selection screen on main menu; `LoadOrCreate(chosenSlot)` / `CreateFreshSave(chosenSlot)` routing.

### Boss Encounter Phase 1 — Foundation

**Status:** Milestones A–C implemented 2026-07-24. Phase 2A first playable implemented in
`SampleScene4`; Phase 2A.5 and Boss Framework Phase A production hardening implemented
2026-07-25; review and hands-on feel
validation remain before Phase 2B.

- [x] Shared enemy prerequisites: additive health snapshots/events, default-preserving retained-root cleanup option, all-child attack initialization/root-health resolution, and shared-blackboard sibling attack exclusion with Mushroom regressions.
- [x] Encounter foundation: stable definition, active participant wrappers with inactive actor roots, trigger/barrier/camera/control-lock integration, separate defeat/presentation gates, synchronous idempotent registry commit, optional reward root, hero-death/unload interruption cleanup, and `BossEncounterValidator`.
- [x] Persistent boss HUD: stateless requests, source-token filtering, explicit multi-participant aggregate roster, lifecycle cleanup, and `_GameCameras.prefab` authoring without changing `PersistentHudRoot`.
- [x] Phase 2A playable vertical slice: asset-facing **Undead Executioner** identity,
  `boss_sample_04_executioner`, one coordinated participant, zero-gravity EnemyMotor glides,
  two boss-owned attacks (`ExecutionerCombo` and `ShadowBurst`), one 50% summon transition,
  temporary non-participant spirit pressure, Animancer intro/combat/death presentation,
  `SampleScene4` trigger/barrier/camera/HUD/completion wiring, normalized checkpoint and
  `room_sample_04`, neutral visual-only reward root, concrete validation, and focused tests.
  Numeric values remain provisional.
- [x] Phase 2A.5 hardening pass (2026-07-25): documented the full-scene-reload retry contract
  (`BossEncounterController` does not self-rearm after ordinary hero death; same-instance death
  retry without a scene reload is unsupported) in code comments and `BossEncounters.md`; added a read-only debug Inspector
  (`UndeadExecutionerBehaviourEditor`) surfacing effective attack timings and live state; added
  arena/hover/range gizmos to `UndeadExecutionerBehaviour` and a shared hitbox-bounds gizmo to
  `EnemyAttackHitbox`; removed the `SampleScene4`-name-coupled walkable check from
  `UndeadExecutionerValidator`; wired the existing `SpriteFlash` component/material onto the
  boss's `PresentationRoot` (auto-discovered by `EnemyHealthComponent`); added targeted EditMode
  coverage for glide-tolerance completion, spirit interruption, combo second-window handoff
  failure, and hero-death camera/control-lock release; removed the accidentally-committed
  `Assets/_Recovery/` Editor crash-recovery scene and ignored the path going forward. No
  encounter/actor architecture changed.
- [x] Boss Framework Phase A hardening (2026-07-25): fail-closed silent participant preparation
  before barriers/camera/HUD/control/intro, safe partial-roster cleanup and corrected same-instance
  failed-start retry, duplicate controller-placement validation across enabled build scenes,
  barrier and Executioner spirit-hierarchy validation, and Editor-owned clear-one-defeat
  save/reload tooling. Normal hero-death retry remains scene-reconstruction-owned; the existing
  in-place respawn fallback is documented as degraded recovery, not a retry guarantee. No camera,
  music, Timeline, boss attack, tuning, presentation, or second-boss work was included.
- [ ] Phase 2A review gate: hands-on player-input pogo, collider/platform behavior, attack
  readability, checkpoint death/retry through the full Boot/save flow, transition framing, and
  fight-duration/tuning approval.
- [ ] Phase 2B polish: tuning, final VFX/SFX/art integration, refined intro/outro, arena
  readability, and reward content after progression approval.
- [x] Camera presentation hooks (delivered by Camera Phase 3, 2026-07-26): `BossEncounterCameraPresenter`
  subscribes to the existing lifecycle events plus the new presentation-only
  `BossEncounterController.EncounterInterrupted` and `IBossPresentationPhaseSource`, and owns the
  intro/combat/phase/defeat/reward camera requests. No boss attack, tuning, health, persistence, or
  reward ownership changed.
- [ ] Music override/restore architecture — still deferred, pending a separate audio-ownership decision.

**Future compatibility note:** Death-drop / shade / resource recovery is not part of this pass. When added, it should capture death scene + death position before `GameManager` loads the checkpoint scene; do not reuse `activeRespawnSceneName` for death-drop location.

---

## Milestone 5 — Polish Pass

- [x] Persistent health and resource HUD — UGUI views subscribe directly to `PlayerHealthState` and `PlayerResourceState`; Canvas hierarchy and Inspector wiring are built into `_GameCameras.prefab` (Milestone 6, verified during Milestone 7). Final art polish remains explicitly out of scope.
- [ ] Pause menu wired through `GameManager.Pause()` / `Unpause()`.
- [ ] Main menu scene — loaded from boot on fresh start; "New Game" calls `SaveManager.CreateFreshSave(0)` and `BeginSceneTransition(firstScene)`; "Continue" calls `SaveManager.LoadOrCreate(0)` and `BeginSceneTransition(SaveManager.GetStartupScene(firstScene))`.
- [ ] Save slot UI — show `SaveStats` (scene, play time, ability count) per slot; `SaveManager.GetSaveStats(slot)` for read-only previews.
- [ ] Play-time accumulation — wire `MetaSaveData.playTimeSeconds` accumulator in `SaveManager.Update()`.
- [ ] Full SFX pass: all hero actions, all enemy actions, UI sounds.
- [ ] Music routing through `GameManager.BeginSceneTransition`.
- [ ] Game feel: hit-pause, screen shake on landing/hit, particle VFX on impacts.
- [ ] Performance: profile and optimise FixedUpdate sensor raycasts for large levels.

## Milestone 6 — Persistent Health and Resource HUD

**Status:** Implemented and verified (Milestone 7 pass confirmed exactly one HUD instance, Overlay camera configuration, and no duplicate subscriptions). Placeholder artwork remains Editor/art work, out of scope.

- [x] Add `PersistentHudRoot`, `HealthDisplay`, and `ResourceDisplay` under `Assets/_Project/Scripts/UI/`.
- [x] Render dynamic normal/bonus health slots and a single always-visible horizontal resource fill bar (`ResourceBarView`) — the earlier discrete pip/orb presentation (`ResourcePipView`) was removed as obsolete in Milestone 7.
- [x] Subscribe directly to persistent state change events with explicit initial refresh and neutral save/reset handling.
- [x] Create the persistent HUD Canvas hierarchy and Inspector assignments on `_GameCameras.prefab`, including the `HUDCamera` Overlay/stack configuration.
- [x] Automated coverage of persistence, gain/spend, damage/heal, zero-capacity, and duplicate-subscription behavior (`HudDisplayTests`). Placeholder artwork and full in-Editor Play Mode visual validation remain manual/Editor work.

---

## Milestone 7 — Lifecycle Integration, Regression Validation, Cleanup, and Documentation

**Status:** Done. See `Docs/FeatureSpecs/PlayerHealthAndResource.md` for the full lifecycle policy table and validation checklist.

- [x] Resource now clears to zero exactly once on normal death, lethal recoverable hazards, and forced-death hazards, via `PlayerResourceState.Clear()` called from the single authoritative `HeroController.ResetAfterRespawn()` call site (previously resource was never cleared on death — a genuine gap fixed this milestone).
- [x] Zero-health save protection — `GameManager.ResolveLoadedHealthState()` (called once by `Bootstrap.Start()` right after `SaveManager.LoadOrCreate`, before any scene/Hero exists) normalizes a loaded save captured at zero health via `PlayerHealthState.NormalizeDepletedContinue()`, using the neutral `StateApplied` reason so the HUD never presents it as healing, and clears `PlayerResourceState` alongside it so a Continue never resumes with a stale pre-death resource amount.
- [x] Validated (already correctly implemented, no code changes needed): New Game reset, ordinary save/load restore + clamping, room-transition preservation, checkpoint marker-before-save ordering and no-heal/no-refill behavior, recoverable-hazard preservation, lethal/forced-hazard funneling into the single death path, and the full Bind cancellation matrix.
- [x] Removed the temporary direct-keyboard Bind fallback from `HeroInputReader` (the real Input System Bind action was verified wired and functional).
- [x] Removed obsolete `ResourcePipView.cs`/`.prefab` (superseded by the resource bar refactor; confirmed zero references).
- [x] Fixed two pre-existing flaky HUD tests (`HudDisplayTests`) whose "stops when disabled" assertions relied on `OnDisable` firing synchronously from `SetActive`/`enabled` outside Play Mode — not guaranteed by the Editor. Tests now invoke the lifecycle method directly; production code was already correct.
- [x] Added focused tests: `PlayerResourceState.Clear()`, `PlayerHealthState.NormalizeDepletedContinue()`, and `GameManager.ResolveLoadedHealthState()`.

---

## Known Technical Debt

- **Gameplay scenes remain development/sample content.** Current Build Settings enable `Boot`,
  `SampleScene`, `SampleScene2`, `SampleScene3`, and `SampleScene4`; the repository is no longer a
  single-gameplay-scene project. `Bootstrap.firstScene` still falls back to `"SampleScene"`, and a
  proper frontend/first-level routing policy remains planned.

- **New-game ability defaults contradict the confirmed product design.** Current field/save/reset
  defaults unlock Dash and Wall Cling, and the mutable development asset also contains Double Jump
  and Bind unlocked. A true new game must start with all eight abilities locked. Complete the
  prerequisite above, including development-asset cleanup, existing-save compatibility review, and
  automated coverage, before Package A2 production validation.

- **Paused gameplay input is not yet suspended.** `HeroInputReader.Tick()` continues sampling while
  paused. Jump and attack buffers use scaled `Time.deltaTime`, so buffers created at time scale zero
  do not expire.

- **Legacy input fallbacks bypass action-map-only suppression.** Direct keyboard/mouse fallbacks for
  attack, dash, and sprint are still sampled. Package A1 must gate or remove them through the
  input-reader suspension/rearming implementation.

- **Fresh-press resume handling is missing.** Clearing command buffers alone cannot prevent held
  Jump, Attack, Dash, Bind, Crouch, Interact, Sprint activation, or future one-shot commands from
  triggering after resume. Held commands require per-command release and a later fresh press;
  continuous movement is intentionally exempt.

- **UI Cancel overlaps gameplay input.** Gamepad East is currently used by UI Cancel and
  Bind/Crouch gameplay input. Re-enabling gameplay while it remains held can leak an action unless
  Package A1 performs overlap release gating and per-command resume disarming.

- **Root-menu transition availability has no implementation.** No transition-completed event exists;
  the approved plan currently identifies the successful falling edge of
  `GameManager.IsSceneTransitioning` while `GameManager.State == Playing` as the narrowest existing
  observation seam. `SceneInit` is too early. The 1.0-second unscaled UI lockout, request rejection
  without queueing, and independent root-action rearming remain planned.

- **External transition interruption is not safe while UI owns pause.** Current transition flow does
  not restore a menu-owned paused time scale. Package A rejects external transitions while a
  pausing root is open; the deferred Quit flow must close presentation, clean input, release pause,
  and then request scene flow in that order.

- **UI focus and persistent Hero lifecycle are not implemented.** The project has UI navigation
  actions but no project-owned menu `EventSystem`/`InputSystemUIInputModule`, root focus flow, or
  persistent menu coordinator. Persistent UI must not retain a destroyed scene-local Hero reference.

- **`HeroController.ResolveDependencies` AssetDatabase fallback.** Lines 126–131 and 138–142 fall back to editor-only `AssetDatabase.LoadAssetAtPath<>` calls for `HeroConfig` and `HeroAnimationLibrary`. This masks missing prefab Inspector assignments. Fix: wire both in the Hero prefab Inspector and remove the fallback blocks. Guard: `#if UNITY_EDITOR` ensures no runtime impact in builds, but the silent fallback makes it easy to ship without the prefab correctly wired.

- **`GameManager` respawn sequencing.** Direct health restoration coupling has been removed: `GameManager` places the hero, grants scene-local post-respawn i-frames, then calls `HeroController.ResetAfterRespawn()` once; that coordinator routes restoration through `HeroHealthComponent` to `PlayerHealthState`, clears bonus health, and clears current resource through `PlayerResourceState.Clear()` — the same single call site reached by normal death, lethal recoverable hazards, and forced-death hazards. `HitStop`, the respawn/recovery sequence methods, and the narrow `ResolveLoadedHealthState()` zero-health-save seam remain beyond the stated "four responsibilities" boundary.

- **Unsupported future save versions are not rejected.** `SaveDataMigrator` stamps any deserialized save to the current version. `Docs/FeatureSpecs/SaveSystem.md` previously described future versions as corrupt, but the implementation has no such guard. This Milestone 1 schema addition does not require changing that behavior; decide and implement a forward-version policy separately.

- **Bootstrap still has no main-menu routing.** Boot now resolves a saved startup scene directly, but once a main menu exists, `Bootstrap` should load the menu; the menu should route to `firstScene` for New Game or the saved startup scene for Continue.

- **`TransitionPoint` does not update active `RespawnMarker`.** By current design decision, dying after crossing a gate returns the hero to the last activated checkpoint, not to the entry door. The `linkedRespawnMarker` field on `TransitionPoint` is reserved for a future policy pass if entry-door respawn is desired. See `Docs/Architecture.md` § Checkpoint and Respawn Markers.

- **`HitStopRoutine` has no `try/finally` cleanup.** If an exception is thrown inside the coroutine, `Time.timeScale` could be left in a bad state. `TransitionRoutine`, `RespawnRoutine`, and `HazardRecoveryRoutine` use `try/finally` cleanup.

---

## Current Integration Risks

- **World Graph Editor upgrade risk.** WGE is integrated and working as the scene graph / port authoring layer, but Underbrew runtime ownership must be preserved during plugin updates. Before importing the next WGE version, protect the current state with a tag or branch such as `wge-working-before-upstream-update`, then update on `upgrade/wge-next-version`. Re-check vendor-file patches, especially relocated-path support under `Assets/Plugins/WorldGraphEditor`, `WGEAssetPathUtility.cs`, editor asset-loading utilities, and `Resources/TransitionManager.prefab` with `_autoLoad` disabled. Evaluate upcoming official custom `TransitionManager` support before doing deeper custom runtime refactors. See `Docs/Integrations/WorldGraphEditorIntegration.md`.

- **Trigger-updated active hazard respawn markers are not implemented.** Direct `HazardZone` → `HazardRespawnMarker` local recovery is implemented. `activeHazardRespawnMarkerKey` is already in `PlayerSaveData`, but the first-pass runtime path intentionally does not read or write it.

- ~~**`AbilityPickup` world-state gap.**~~ Resolved in World Persistence Phase 2 — collection now records `WorldStateRegistry.MarkPickupCollected` alongside `PlayerAbilityState.Unlock`, and initialization reconciles the two in favor of `PlayerAbilityState`.

- **HUD placeholder artwork remains Editor/art work.** `PersistentHudRoot`, `HealthDisplay`, and `ResourceDisplay` provide direct persistent-state subscriptions and neutral initial/state-applied refreshes; the `_GameCameras` Canvas hierarchy and Inspector assignments are already built and verified (Milestone 7). Only final visual artwork/animation polish remains, and it is explicitly out of scope.

- **Prefab / Inspector wiring.** `Bootstrap` requires five prefab references (`GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`) and one string field (`firstScene`). `SaveManager` prefab requires its ordered target list to contain `PlayerAbilityState`, `PlayerHealthState`, and `PlayerResourceState`. The Hero prefab's `HeroController.healthState` field must reference the same `PlayerHealthState.asset`. Verify all in-editor after any prefab refactor.

- **Engine/API compatibility.** Code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g. `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor only.
