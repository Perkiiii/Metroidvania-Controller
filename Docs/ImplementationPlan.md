# Implementation Plan

**Last audited:** 2026-09-13 — Weather presentation Packages 1–3 completion; visual review pending
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
| Save / load system | Done (Milestone 0 foundation + World Persistence Phase 1/2/3 + Packages 1–3 world-time/climate v6; multi-slot UI deferred) |
| Calendar / world time | Package 1 implemented and validated (runtime code/schema/test coverage and serialized asset composition) |
| Climate / weather | Simulation Packages 1–4 implemented; presentation Packages 1–3 implemented in `SampleScene`; focused EditMode 169/169, lifecycle PlayMode 1/1, full EditMode 764/765 with one established unrelated camera assertion; visual review pending |
| Scene transitions | Done (Milestone 1 — single-scene `LoadSceneAsync`; additive loading and world-state deferred) |
| UI (HUD, menus) | Package A1 (flow/Sandbox), A2.1 (five-tab Gameplay Menu/read-only Gear), and A3 (visual foundation/HUD presentation/Sandbox workbench) implemented; production Boot-path interactive manual validation and final art remain pending; Package B not started |
| Audio system | Partial |

---

## Authoritative UI Roadmap

The approved planning record is `Docs/ImplementationPlans/UIImplementationPlan.md`. Authoritative
contracts now live in:

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/GameplayMenu.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/FeatureSpecs/HUD.md`
- `Docs/FeatureSpecs/UIVisualFoundation.md`
- `Docs/FeatureSpecs/Abilities.md`

`Docs/ImplementationPlans/UIA2ImplementationPlan.md` is the approved Package A2 planning record and
supersedes the earlier proposal's Gear-only/hidden-sibling-tab recommendation.

Package A1 (root Pause menu, persistent MenuRoot/input foundation, Sandbox) has been implemented,
corrected, and covered by automated EditMode and PlayMode tests — both confirmed executing via the
real Unity Test Runner — plus a passing `UIFoundationValidator` pass. The Sandbox-only Options
preview and Quit callback flows were live-verified in a real Play Mode session. See the Package A1
entry below for exact scope and what remains unvalidated (interactive manual checks against the
production Boot path).

Package A2 (Stage 0 ability defaults, the original Gameplay Menu shell, and read-only Gear) was
implemented at the historical baseline. Package A2.1 corrects the top-level structure to five tabs
while retaining read-only Gear. Package A3 implements the provisional visual foundation without
adding deferred gameplay domains. Package B has not been performed.

### Documentation/design contract

**Status: Approved planning direction; authoritative documentation updated.**

- [x] High-level UI product decisions confirmed.
- [x] UI implementation proposal reviewed.
- [x] Authoritative UI ownership, pause/menu flow, Gear, and Sandbox specifications created.
- [x] Package A1 implementation and focused automated validation completed; production Boot-path
  interactive validation remains pending.

### Prerequisite — New-game ability defaults

**Status: Done (Package A2 Stage 0, 2026-07-28).**

**Confirmed intended state:** a new player starts with no unlocked permanent abilities. Dash, Wall
Cling, Sprint, Wall Latch, Double Jump, Drift Cloak, Spirit Cast, and Bind are acquired through
progression. The initial Gear collection may legitimately be empty.

- [x] Authoritative new-game and reset defaults updated so all eight abilities begin locked.
- [x] `AbilitySaveData` defaults updated.
- [x] `PlayerAbilityState.ResetToDefaults()` and field initializers updated.
- [x] `Bootstrap`/fresh-save/migration paths audited. `SaveManager.CreateFreshSave` composes
  `new SaveData()` → `Migrate` → apply, which now yields all-locked; no code change was needed
  there.
- [x] `PlayerAbilityState.asset` reset to all-locked.
- [x] Compatibility reviewed: `GatherSaveData` writes all eight booleans explicitly, so existing
  saves round-trip unchanged; a missing/null ability section receives the all-locked default.
  `SaveDataMigrator.CurrentSaveVersion` was **not** bumped — no migration behaviour changed.
- [x] Tests proving new-save and reset-to-defaults states contain no unlocked abilities.
- [x] Tests proving existing saves preserve explicitly stored unlock values (including a
  256-combination round trip) and that the production asset ships fully locked.

**Not done:** existing development save slots were deliberately not inspected or deleted. A slot
that already contains explicit unlocks keeps them.

### Package A — Internal UI foundation vertical slice

**Status: Planned; not yet shippable.**

#### Package A1 — UI foundation

**Status: Implemented, corrected, and automated-tested with confirmed EditMode+PlayMode execution;
Sandbox flows live-verified in Play Mode; production Boot-path interactive manual validation not yet
performed.**

- [x] Development-only UI Sandbox (`Assets/_Project/Scenes/UISandbox.unity`, excluded
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
  fresh-press resume behavior for Jump/Attack/shared Dash-Wildstride/Interact/Bind; continuous movement
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
- [x] Automated coverage: `UIFlowControllerTests`
  (EditMode, 20 tests), `PauseMenuScreenTests` (EditMode, 14 tests), `ConfirmationModalTests` (EditMode,
  4/4), `UISandboxControllerFixtureTests` (EditMode, 4/4) — full EditMode suite 365/365, zero
  PlayMode tests included. `HeroInputSuspensionPlayModeTests` (PlayMode, 14/14 — 9 original plus 5
  added this pass) confirmed executing and passing via the real PlayMode Test Runner after fixing
  `Underbrew.UI.PlayModeTests.asmdef`'s `includePlatforms` (was `["Editor"]`, now `[]` matching the
  working `Underbrew.Camera.PlayModeTests.asmdef` convention) — full PlayMode suite 29/29 (14 UI +
  15 unrelated camera tests, confirming the asmdef fix did not affect other discovery).
  `UIFoundationValidator` (`Tools/Project/Validate UI Foundation`) passed, extended this pass to
  validate input-module action wiring (confirmed to fail when a reference is deliberately cleared)
  and Sandbox-only-component production exclusion.
- [x] Final A1 hardening adds three real Input Action/player-loop Escape arbitration PlayMode tests
  and paused-transition coverage. The 2026-07-27 focused UI EditMode run observed 42/42 passing.
  PlayMode Input System event advancement was blocked by the available automated runner environment,
  so this hardening pass makes no new PlayMode pass claim. Camera lifecycle/transition PlayMode
  regression coverage observed 11/11 passing.
- [x] Sandbox correction pass: Options preview placeholder (`SandboxOptionsPreviewPanel`) and Quit
  callback status label (`SandboxQuitCallbackStatus`) implemented and live-verified in a real Play
  Mode session; Bonus-health and resource fixture presets made deterministic across repeated clicks;
  runtime boss-fixture `EnemyConfig` instances tracked and destroyed when superseded.
- [ ] Interactive manual validation against the **production Boot path** (keyboard/controller/mouse
  Pause+Gameplay Menu open/close in the real gameplay scene, held-input-through-transition, real
  room-transition timing) not performed this session — the Sandbox-only flows above were live-driven
  in Play Mode, but the production Boot scene was not loaded.

### Package A2.1 — Gameplay Menu five-tab correction

**Status: Implemented and automated-tested (EditMode 458/458, PlayMode 45/45, validator passing);
production Boot-path interactive manual validation not yet performed.**

Planning record: `Docs/ImplementationPlans/UIA2ImplementationPlan.md`. Contract:
`Docs/FeatureSpecs/GameplayMenu.md`.

- [x] `GameplayMenuScreen` registered as the second `IUIFlowRootScreen` through the existing
  `UIFlowController.gameplayMenuRootBehaviour` field. **No Package A1 production code changed** —
  only comments/tooltips were refreshed.
- [x] Five always-visible tabs in the confirmed order (Gear, Loadout, Satchel, Field Notes, Map),
  with stable IDs `Gear`, `CombatLoadout`, `Satchel`, `FieldNotes`, `Map` and runtime-only
  last-tab/per-tab selection memory.
- [x] **Superseded:** the earlier "Gear only, siblings hidden" filtering. Ownerless tabs are
  visible with authored empty states; deferral now means *no data*, not *no tab*.
- [x] Narrow `IGameplayMenuTab` contract; one shared presentation-only `GameplayMenuEmptyTabView`
  for the four unfinished tabs, distinguished entirely by authored prefab content.
- [x] Read-only Gear: `GearDisplayDefinition`/`GearDisplayCatalog`/`GearScreen`/`GearEntryView`/
  `GearDetailsPanel`, filtered by `PlayerAbilityState.IsUnlocked` with stable-key selection.
- [x] Acquired-only presentation with no silhouettes, unknown totals, or future placeholders.
- [x] Intentional, selectable true-new-game empty Gear state.
- [x] Details presentation and optional binding-string control hints (no glyph database).
- [x] `UI/PreviousTab` and `UI/NextTab` actions plus a provisional controller `System/GameplayMenu`
  binding; durable `InputActionReference` sub-assets assigned on both `UIFlowController` and
  `GameplayMenuScreen`.
- [x] Sandbox nests the real prefab with isolated runtime ability state and fixture catalogue.
- [x] Validator and EditMode/PlayMode/asset-contract coverage updated for the five-tab contract;
  the Package A3 pass retains and reruns the focused coverage.
- [ ] **Production Gear definitions.** The production catalogue is intentionally empty — no
  physical Gear identity, name, artwork, or copy has been approved. Everything else is in place.
- [ ] Production layout comparison of authored groups vs authored tableau vs list. A list + details
  composition was chosen as a reversible first pass; the alternatives remain open.
- [ ] Interactive manual validation with real keyboard/controller/mouse devices and the production
  Boot path.

### Package A3 — Visual foundation, HUD presentation, and UI workbench

**Status: Implemented; validator passing, full EditMode 473/473, UI PlayMode 34/34. Production
Boot-path interactive review and final art content remain pending.**

Contract: `Docs/FeatureSpecs/UIVisualFoundation.md`.

- [x] Shared palette, spacing, hierarchy, TMP typography, selection/focus, and unscaled local
  transition language applied to the Package A prefabs.
- [x] Production HUD safe-area wrapper, layered health states, masked continuous resource fill,
  and source-neutral boss presentation with main/trailing masked fills.
- [x] Five-tab structure and deferred-domain ownership preserved; no invented Loadout, inventory,
  Field Notes, Map, notification, settings, or save state.
- [x] Gear list/details hierarchy refined while preserving read-only ability-backed filtering,
  stable-key selection, and the intentionally empty production catalogue.
- [x] Sandbox reshaped into a compact visual workbench with collapsible fixtures,
  hidden-by-default diagnostics, background/safe-area checks, and automatic root-preview collapse.
- [x] `UIFoundationValidator` extended for TMP/font assignment, masked-fill wiring, safe-area and
  decorative-raycast checks, and Sandbox-workbench production exclusion.
- [x] Focused masked-fill and presentation regression coverage added/updated; the 2026-07-29 Unity
  Test Runner pass completed 473/473 EditMode and 34/34 UI PlayMode tests.
- [ ] Approved art sprites, bespoke TMP font/fallbacks, icons/glyphs, localization, accessibility,
  and target-device safe-area review.
- [ ] Production Boot-path keyboard/controller/mouse and room-transition validation.

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
- Combat Loadout, Satchel, Field Notes internal sections, and Map domain implementations.
- Dual-access Map: hold the future Map action for a non-pausing Local Quick Map; double-tap the same
  action for the pausing Gameplay Menu Full Map tab. Tab is provisionally reserved for Map on
  keyboard. Map remains outside Package A.
- Notifications and acquisition presentation.
- HUD final illustrated artwork, audio, and accessibility review.
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
- [x] Wire `Bootstrap`: instantiates the existing managers plus `WorldClockDriver` after
  `GameManager` and `SaveManager`; calls `SaveManager.LoadOrCreate(0)`, resolves startup scene
  from save data, requests saved respawn placement, and starts the transition in `Start`. The
  serialized driver prefab reference is present in `Boot.unity`.
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
- [x] Implement always-available static-terrain ledge climb (`HeroLedgeClimbAction`). (Done —
  airborne-only entry; pre-wall-slide arbitration; dash-to-mantle ownership handoff; strict
  Terrain/composite/adjacent-seam validation; target-local result data; minimal authored
  exclusion volume; code-owned Catch/PullUp/Settle completion. Moving platforms, one-way
  platforms, Wall Latch, and indefinite hanging remain excluded. See
  `Docs/FeatureSpecs/LedgeClimb.md`.)
- [ ] Implement wall-latch / aimed wall launch (`HeroWallLatchAction`).
- [x] Correct and harden Wildstride Pass 1/2 movement (`HeroSprintAction`). Ordinary movement now
  explicitly uses Walk/HeroWalk; grounded Wildstride explicitly uses sprintSpeed/the Sprint slot.
  Natural grounded Dash hands off immediately, using current steering or the typed Dash direction
  when steering is neutral, while a natural ordinary air Dash creates a typed/versioned
  authorization and retains its direction for the first landing. Grounded direction changes
  preserve authorization through motor-owned turning and same-step locomotion restoration; active
  grounded neutral input retains the last valid Sprint direction while Dash remains held and armed,
  without allowing an idle Dash hold to synthesize Wildstride.
  Airborne carry captures its launch direction as a private `AirborneCarry` phase. Neutral input
  while Dash remains held preserves forced carry; normal falling, opposite air input, and residual
  decay end only forced carry through the motor. `AirborneAuthorised` keeps first-landing
  permission through arbitrary ordinary airtime and resolves neutral input to remembered-direction
  air steering. Landing consumes it once and restores grounded Wildstride in the same fixed step,
  using current input before the remembered sequence direction. Base Wildstride is resource-free.
  A provisional
  0.08-second private ledge-jump buffer preserves the specialized Jump opportunity when Jump begins
  just after running off a ledge; buffer expiry transitions to ordinary airborne authorization,
  without changing normal coyote or Jump-buffer tuning. Authorized ledge climbs temporarily suspend
  Wildstride and resume on successful completion while Dash remains held and armed. Double Jump is
  the hard-cancellation exception: it clears the sequence and landing authorization and requires
  Dash release before restart. The earlier resource-drain and carry-required landing rules are
  superseded. Dedicated run-slash, down-dash,
  skid/back-Sprint presentation, VFX/audio, swimming, optional enhanced-speed Gear, final tuning,
  and manual feel approval remain outstanding;
  the complete feature is not done. See `Docs/ImplementationPlans/Wildstride.md`.
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

## World Time / Climate / Weather Packages

**Status:** Simulation Packages 1–4 and weather presentation Packages 1–3 are implemented.
Simulation Package 3 includes the
deterministic SplitMix64/XorShift32 generation kernel, independent-season weighted generation,
fixed-slot resolution, immutable three-period-per-region LRU, post-completion `HourChanged`
progression through every crossed slot, actual-weather history recording/querying, exact-slot
runtime overrides, and immutable forecasts. It preserves `WorldWeatherState`/`WorldTimeState`
ownership and save schema v6. Package 4 adds passive room exposure metadata, safe matrix authoring,
the project validator, and one provisional context in each enabled gameplay scene; it changes no
save/schema, prefab, ProjectSettings, Packages, or presentation system. The separate presentation
packages add only the scene-local weather consumer/effects described below.

Observed Unity 6000.3.10f1 EditMode results: validator 9/9; generator 10/10,
progression/persistence 29/29, history 11/11, overrides 12/12, forecast 13/13; existing
WorldClimate 84/84, WorldTime 38/38, and explicit existing climate/time union 122/122. Full
Final focused EditMode (simulation plus presentation) is 169/169 and the weather lifecycle
PlayMode fixture is 1/1. Full EditMode is 764/765; the sole failure is the established unrelated
`CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion. No live screenshot,
hands-on visual/audio-mix validation, or profiling evidence was recorded.

See `Docs/ImplementationPlans/WorldTimeClimateWeather.md` for the authoritative package contract.

### Package 1 — Calendar and World Time

- [x] Add `CalendarConfig`, immutable `WorldTimeSnapshot`, `WorldTimeState`, and
  `WorldClockDriver` under `Assets/_Project/Scripts/World/Time/`.
- [x] Add `WorldTimeSaveData` under `Assets/_Project/Scripts/Save/Data/`, include it in
  `SaveData`, and migrate the save schema from v4 to v5. Pre-v5 data remains uninitialized so the
  state uses its authored fresh date.
- [x] Add focused tests under `Assets/_Project/Scripts/Editor/Tests/WorldTime/` for exact calendar
  conversion, rollover/boundary event ordering, save/migration, overflow/reentrancy, and driver
  gating. Covered by the completed Package 1 baseline/full-suite validation pass.
- [x] Add the `Bootstrap.worldClockDriverPrefab` seam; the driver is instantiated after
  `GameManager` and `SaveManager` and cannot tick until `WorldTimeState.IsLoaded`.
- [x] Create and wire the target assets/prefab:
  `Assets/_Project/ScriptableObjects/World/CalendarConfig.asset`,
  `Assets/_Project/ScriptableObjects/World/WorldTimeState.asset`, and
  `Assets/_Project/Prefabs/Managers/_WorldClockDriver.prefab`. Assign only the state to the
  driver; the driver reads `WorldTimeState.RealSecondsPerGameMinute`. The state is included in
  `Assets/_Project/Prefabs/Managers/_SaveManager.prefab` and the driver prefab is assigned in
  `Assets/_Project/Scenes/Boot.unity`.

The runtime ownership split is fixed: `WorldTimeState` owns canonical minutes, calendar
derivation, advancement, persistence, and events; `WorldClockDriver` owns only the unscaled
accumulator and gating. It advances only while the state is loaded and `GameManager.State` is
`Playing`, and stops for pause, menus, and loading/transition states (hit-stop continues because
the state remains `Playing`). Each crossed hour publishes `HourChanged`, then applicable
`DayChanged`, `SeasonChanged`, `YearChanged`, and `DayPhaseChanged`; one `TimeAdvanced` follows the
final target. `StateApplied` publishes once after fresh/initialized save application.

### Package 2 — Climate Definitions and Regional Persistent State

**Implemented and isolated-Unity validated; primary-Editor/Play Mode validation not performed.** Package 2 adds the
append-only `WeatherType` enum, weather metadata/config, seasonal transition definitions, stable
climate region definitions/catalog, `WorldWeatherState` with current-weather/root-seed queries,
v5→v6 weather DTO/migration, and the post-load `CompleteLoad(WorldTimeState)` reconciliation seam.
It is wired through `SaveData`, `_SaveManager.prefab`, and `Boot.unity`. `ApplySaveData` is
clock-free and `CompleteLoad` runs after all save targets apply; Package 3 extends this seam with
live progression, actual-weather history, exact-slot overrides, and read-only forecast/history
queries.

### Package 3 — Deterministic Weather, Forecast, History, and Overrides

**Implemented and independently validated.** The project-owned SplitMix64/XorShift32 kernel,
independent-season weighted generation, authored fixed-slot behavior, and immutable
three-period-per-region LRU are integrated with lifecycle-safe slot progression. Every crossed slot
is resolved in catalog order through the actual-weather mutation/history/event path. Immutable
half-open history queries, exact-slot runtime overrides, and inclusive deterministic forecasts are
implemented without mutating query state or changing save schema v6. Focused fixtures pass
10/10, 29/29, 11/11, 12/12, and 13/13 respectively; the complete WorldClimate/WorldTime union
passes 122/122. No manual visual/interactive validation or PlayMode run was performed; current
closure results are recorded above.

### Package 4 — Room Context and Authoring Tools

**Implemented and validated.** Adds runtime `EnvironmentExposure` and passive
`RoomClimateContext`, safe `SeasonDefinitionEditor` tooling (FROM/TO labels, row totals,
`Set Uniform`, `Clear Row`, append-safe preservation, and confirmation for destructive shrink/reset;
there is no Normalize-to-100), and `WorldTimeClimateValidator` at
`Tools/Project/Validate World Time & Climate`. Enabled-scene validation requires zero contexts in
`Boot`, exactly one context in every other enabled Build Settings scene, and each context’s nonblank
`regionId` to be present in the validated catalog; shared region IDs are allowed. The menu pass
checked five enabled scenes, four contexts, and zero issues. `SampleScene` through `SampleScene4`
each have one provisional root `region_underbrew` / `Outdoor` context; `UISandbox` is disabled and
excluded. This simulation Package 4 does not add presentation responsibilities; the separate
weather presentation Packages 1–3 are recorded below. Farming/NPC/UI consumers, final art/polish,
and broader weather presentation remain deferred.

### Weather presentation Package 1 — Presentation Foundation

**Implemented and independently validated.** `RoomWeatherPresentation` consumes the actual
regional weather exposed by `WorldWeatherState` through the existing `RoomClimateContext`. It
refreshes on enable, responds only to the matching region's `WeatherChanged`, maps outdoor Rain and
Storm to presentation requests, and resets/unsubscribes safely on disable/destroy. It owns only
transient presentation state; it does not generate or mutate weather, own time, persist timers, or
reference `HeroController`.

### Weather presentation Package 2 — Rain Slice

**Implemented; visually provisional.** `RoomRainPresentation` drives one camera-framed, world-space
`WorldRain` ParticleSystem, three authored upward-facing `GroundSplash` emitters using the copied
six-frame atlas, and short Clear/Rain ramps. Camera geometry is read through `CameraInfoCache`.
Rain ambience uses `AudioManager`'s single owner-scoped looping channel; the prefab has no unmanaged
AudioSource. `SampleScene` is the only authored weather room for this pass.

### Weather presentation Package 3 — Storm Slice

**Implemented; visually provisional.** `RoomStormPresentation` extends the Rain request with a
transient unscaled cadence, a bounded hidden `LightningFlash` SpriteRenderer and restrained
multi-pulse envelope, then routes delayed Thunder one-shots through `AudioManager.PlaySFX`. Storm
exit, disable, destroy, and room unload cancel future strikes and pending thunder. No timer is
persisted and no simulation, camera-follow, hero, GameManager, renderer, package, or save changes
are part of this slice.

Final focused EditMode validation is 169/169; the weather lifecycle PlayMode fixture is 1/1. Full
EditMode is 764/765 with only the established unrelated
`CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion failing. No live screenshot,
hands-on visual/audio-mix validation, or profiling evidence was recorded. Stop here for human
visual review; defer shelter policy, foreground rain, wind, ambient leaves/motes, camera feedback,
wet surfaces, fog, day/night lighting, post-processing profiles, additional weather types, and
broader polish.

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

- [x] Persistent health and resource HUD — UGUI views subscribe directly to `PlayerHealthState` and `PlayerResourceState`; Canvas hierarchy and Inspector wiring are built into `_GameCameras.prefab` (Milestone 6, verified during Milestone 7). Package A3 adds the visual foundation; final illustrated art remains a content pass.
- [ ] Pause menu wired through `GameManager.Pause()` / `Unpause()`.
- [ ] Main menu scene — loaded from boot on fresh start; "New Game" calls `SaveManager.CreateFreshSave(0)` and `BeginSceneTransition(firstScene)`; "Continue" calls `SaveManager.LoadOrCreate(0)` and `BeginSceneTransition(SaveManager.GetStartupScene(firstScene))`.
- [ ] Save slot UI — show `SaveStats` (scene, play time, ability count) per slot; `SaveManager.GetSaveStats(slot)` for read-only previews.
- [ ] Play-time accumulation — wire `MetaSaveData.playTimeSeconds` accumulator in `SaveManager.Update()`.
- [ ] Full SFX pass: all hero actions, all enemy actions, UI sounds.
- [ ] Music routing through `GameManager.BeginSceneTransition`.
- [ ] Game feel: hit-pause, screen shake on landing/hit, particle VFX on impacts.
- [ ] Performance: profile and optimise FixedUpdate sensor raycasts for large levels.

## Milestone 6 — Persistent Health and Resource HUD

**Status:** Implemented and verified (Milestone 7 pass confirmed exactly one HUD instance, Overlay camera configuration, and no duplicate subscriptions). Package A3 adds masked-fill/TMP/safe-area presentation; final illustrated artwork remains an art pass.

- [x] Add `PersistentHudRoot`, `HealthDisplay`, and `ResourceDisplay` under `Assets/_Project/Scripts/UI/`.
- [x] Render dynamic normal/bonus health slots and a single always-visible horizontal resource fill bar (`ResourceBarView`) — the earlier discrete pip/orb presentation (`ResourcePipView`) was removed as obsolete in Milestone 7.
- [x] Subscribe directly to persistent state change events with explicit initial refresh and neutral save/reset handling.
- [x] Create the persistent HUD Canvas hierarchy and Inspector assignments on `_GameCameras.prefab`, including the `HUDCamera` Overlay/stack configuration.
- [x] Automated coverage of persistence, gain/spend, damage/heal, zero-capacity, masked-fill behavior, and duplicate subscriptions (`HudDisplayTests`, `HorizontalMaskedFillViewTests`). Final art and production-path interactive review remain manual work.

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

- **New-game ability defaults now match the confirmed product design.** Resolved in Package A2
  Stage 0: field, save-data, reset, and asset defaults all begin locked, existing saves keep their
  explicit values, and the save version was not bumped. Note the consequence for development: a
  fresh save now has no Dash or Wall Cling. Use the ability pickups or the Sandbox fixtures.

- **Package A1/A2 production-path validation remains incomplete.** Boot-path
  keyboard/controller/mouse checks, controller disconnect/focus loss, and real room-transition
  held-input checks remain manual for both packages.

- **Gameplay data behind the Gameplay Menu remains deferred.** All five tabs are visible and
  navigable, but only Gear is data-backed, and its production catalogue is empty pending approved
  identities. Combat Loadout, Satchel (inventory/quantities), Field Notes internal Recipes/Tasks/
  Journal content, and the functional Map — plus Quick Map, the Full Map double-tap shortcut, and
  map markers — have no owners yet. Functional Options, functional Quit, frontend, and
  notifications remain deferred.

- **External transitions while paused are explicitly rejected.** `GameManager.BeginSceneTransition`
  returns false without starting scene flow while `GameState.Paused`. A future functional Quit must
  close presentation, resume input safely, release UI-owned pause, then request scene flow.

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

- **HUD final illustrated artwork remains art work.** `PersistentHudRoot`, state subscriptions, Package A3 masked-fill/TMP presentation, safe-area hierarchy, and local unscaled feedback are built and validated. Approved sprites, bespoke type, audio, accessibility, and device review remain.

- **Prefab / Inspector wiring.** `Bootstrap` requires six prefab references (`GameManager`,
  `SaveManager`, `WorldClockDriver`, `AudioManager`, `GameCameras`, `InteractManager`) and one
  string field (`firstScene`). Its Package 1 driver and Package 2 weather-state references are
  serialized in `Boot.unity`.
  `SaveManager` prefab's ordered target list contains `PlayerAbilityState`, `PlayerHealthState`,
  `PlayerResourceState`, `WorldStateRegistry`, `WorldTimeState`, and `WorldWeatherState`. The Hero prefab's
  `HeroController.healthState` field must reference the same `PlayerHealthState.asset`. Verify all
  in-editor after any prefab refactor.

- **Engine/API compatibility.** Code uses `FindObjectsByType` / `FindFirstObjectByType` (Unity 2023+). Building outside the Unity Editor (e.g. `dotnet build`) will fail due to missing Unity runtime assemblies — verify compilation inside the Unity Editor only.
