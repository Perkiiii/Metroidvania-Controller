# Feature Spec — UI Architecture

**Last reviewed:** 2026-07-27  
**Status:** Authoritative architecture. Package A1 (persistent `MenuRoot`, `UIFlowController`, root
Pause menu, Sandbox) is implemented and covered by automated EditMode and PlayMode tests, both
confirmed executing via the real Unity Test Runner, plus a passing `UIFoundationValidator` pass. A
correction pass fixed production `InputSystemUIInputModule` action wiring (durable `UI`-map
`InputActionReference` sub-assets, not a transient `InputActionReference.Create()`), modal-first
Pause-toggle behavior, production Quit/Options gating and dynamic navigation, confirmation-modal
selection fallback, pause-safe `UIFlowController` teardown, and an input-leakage defect in
`HeroInputReader`'s resume-disarm logic affecting Jump/Attack/Interact/Bind/Dash/Move. The
Sandbox-only Options preview and Quit callback status were live-verified in a real Play Mode
session. Package A2 (Gameplay Menu, Gear) and Package B (functional Options) have not been performed
or validated. Interactive manual validation of the full production Boot path (keyboard/controller/
mouse in the real gameplay scene, real room transitions, held-input timing) has not been performed —
see `Docs/ImplementationPlan.md` Package A1 entry for exact scope.

## Purpose

Define Underbrew's overall UI ownership, lifetime, layering, input, focus, scene-flow, and
presentation boundaries. Specialized behavior belongs in:

- `Docs/FeatureSpecs/HUD.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/FeatureSpecs/Abilities.md`

## Goals

- Preserve the implemented persistent HUD and transition fade.
- Add a focused persistent pausing-menu composition without creating an all-owning manager.
- Keep gameplay state authoritative outside views.
- Provide deterministic root, modal, Back, focus, input-suspension, and scene-transition behavior.
- Support keyboard, controller, and mouse.
- Allow an honestly empty Gear screen because a new player begins with no ability-granting Gear.
- Leave clean boundaries for future frontend, contextual interfaces, notifications, and dual-access
  Map.

## Non-goals

- A giant all-owning `UIManager`.
- Gameplay-state ownership in UI.
- A generic inventory or window framework.
- Map discovery ownership in `UIFlowController`.
- Direct scene loading from views.
- Direct `SaveManager` calls from views.
- Direct `Time.timeScale` writes.
- Direct `HeroInputReader` access from UI.
- Animator-parameter state-machine architecture; UI animation must not introduce gameplay Animator
  parameters.
- Implementing deferred gameplay systems merely to populate tabs.
- Using current mutable development-state asset values as product-design defaults.
- Building a generic limited-input framework in Package A solely for deferred Map behavior.

## Current implemented state

| Area | Status | Current owner |
|---|---|---|
| Persistent gameplay HUD | Implemented | `PersistentHudRoot` under `_GameCameras` |
| Health presentation | Implemented | `HealthDisplay` reading `PlayerHealthState` |
| Resource presentation | Implemented | Continuous `ResourceDisplay` reading `PlayerResourceState` |
| Boss HUD | Implemented | `BossHealthDisplay` + stateless `BossHudEventService` |
| HUD camera | Implemented | Dedicated URP Overlay `HUDCamera` |
| Transition fade | Implemented | `FadeCanvas`, transition-owned |
| Pause authority | Implemented seam | `GameManager.Pause()` / `Unpause()` |
| UI navigation actions | Implemented | Existing `UI` Input Action map, wired to `InputSystemUIInputModule` |
| Root Pause menu | Implemented (Package A1) | `PauseMenuScreen` + `ConfirmationModal` under `MenuRoot`; Options authored but production-gated non-functional; Quit is a development-gated typed request seam only |
| Gameplay Menu / Gear | Missing/planned | Package A2 |
| Persistent MenuRoot/EventSystem | Implemented (Package A1) | `_GameCameras.prefab` → `MenuRoot` (`EventSystem`, `InputSystemUIInputModule`, `UIFlowController`, `RootInterfaceLayer`, `ModalLayer`) |
| Notifications | Deferred | Future `NotificationRoot` or equivalent |
| Quick Map / Full Map | Deferred | Future Map system plus menu integration |
| Frontend | Missing/deferred | Future scene-local frontend |

The Input Actions asset now has `Player`, `UI`, and `System` maps. `System` contains `Pause`
(Keyboard Escape, Gamepad Start) and `GameplayMenu` (Keyboard I; controller binding deferred).
`UIFlowController` is the System map's sole persistent owner; the scene Hero never enables/disables
it. A project-owned production menu `EventSystem`/`InputSystemUIInputModule`/`UIFlowController`/
`MenuRoot` and a UI Sandbox now exist (Package A1). Gear/Gameplay Menu views do not exist yet
(Package A2).

## Persistent composition

```text
_GameCameras
├── MainCamera / HUDCamera
├── HUDRoot                 implemented
├── NotificationRoot        future/deferred
├── MenuRoot                implemented (Package A1)
└── FadeCanvas              implemented
```

### HUDRoot

`HUDRoot` remains the persistent presentation composition described in `HUD.md`. It subscribes
directly to persistent state and is not recreated on room load. Menus may visually cover or fade it
without changing those subscriptions.

### MenuRoot

`MenuRoot` is implemented (Package A1) as a persistent sibling under `_GameCameras.prefab`, not a
child of `HUDRoot`. It contains `UIFlowController`, one project-owned `EventSystem`/
`InputSystemUIInputModule`, a `RootInterfaceLayer` (nested `PauseMenuScreen` prefab instance), and a
`ModalLayer` (nested `ConfirmationModal` prefab instance). `GameplayMenuScreen` does not exist yet;
`UIFlowController.gameplayMenuRootBehaviour` is intentionally left unassigned until Package A2.

### ModalLayer

The modal layer is part of the planned menu composition. A modal covers and blocks its parent root,
owns the current selection, and closes before the parent can close. Package A needs only a shallow
modal hierarchy; it does not establish a generic stacked-window framework.

### NotificationRoot

`NotificationRoot` is a future/deferred sibling for acquisition, area-title, save, and tutorial
presentation. It is not owned by HUD or `UIFlowController`. Package A may preview static fixtures in
the Sandbox but does not create production notification queues, content ownership, or inferred
acquisition events.

### FadeCanvas

`FadeCanvas` remains transition-owned and covers every gameplay UI layer. Menus, HUD, contextual
interfaces, notifications, and future Map presentation do not control transition fade lifecycle.

## Lifetime boundaries

| Interface | Lifetime | Pauses? | Authority |
|---|---|---:|---|
| Gameplay HUD | Persistent | No | Persistent state views |
| Root Pause menu | Persistent composition, transient visibility | Yes | Implemented `UIFlowController` via `GameManager` |
| Gameplay Menu / Full Map | Deferred persistent composition | Yes | Future A2/later `UIFlowController` routing |
| Notifications | Future persistent sibling | No by default | Future presentation owner |
| Frontend | Scene-local | Policy-specific | Future frontend flow |
| Dialogue/shop/station | Scene-local/contextual | No by default | Context owner + approved control lock/replacement |
| Local Quick Map | Future contextual overlay | No | Future Map owner + Hero limited-input request |

Persistent UI must not retain a stale scene-local Hero reference. `GameManager.SceneInit` can
invalidate the previous binding, but it fires before the destination Hero cache is ready. A future
implementation must clear the old reference immediately and bind later or resolve the established
current-Hero seam just in time.

## Ownership table

| Concern | Owner | Explicit non-owner |
|---|---|---|
| Health/resource/ability values | Persistent gameplay ScriptableObjects | Views, `UIFlowController` |
| Pause state and time scale | `GameManager` | UI views, input reader |
| Active pausing root, modal/back routing | Implemented `UIFlowController` | `GameManager`, views |
| Gameplay input sampling/buffers/rearm | `HeroInputReader` | `GameManager`, UI |
| UI-to-Hero suspension request boundary | `HeroController` | Direct view/input-reader coupling |
| UI focus/selection | Implemented UI flow/focus helpers | Gameplay systems |
| Save data and save execution | `SaveManager` + `ISaveTarget` owners | UI views |
| Scene transition execution | Existing game/scene-flow systems | UI views |
| Map model/discovery/stable room resolution | Future Map/world-state owner | `UIFlowController` |
| Gear unlock ownership | `PlayerAbilityState` | Gear definitions/views |
| Audio playback/routing/settings | `AudioManager` + planned settings foundation | Options sliders/views |

## UIFlowController boundary

`UIFlowController` currently owns in A1:

- Root open/close requests and one-active-pausing-root enforcement.
- `Closed`/opening/open/closing guards or equivalent.
- Shallow modal and Back/Cancel routing.
- Requests to `GameManager.Pause()` / `Unpause()`.
- Requests through `HeroController` for input suspension/resume.
- Player/UI/System action-map mode once the binding design is approved.
- Full transition availability, post-transition lockout, and per-root-action arming.
- EventSystem first selection, remembered selection, and restoration.

Future A2 or later responsibilities are Gameplay Menu tab state, session-only last-valid-tab
memory, Full Map routing, and future scene-flow request routing after policy approval. None is
currently active at runtime.

It must not own gameplay state, inventory quantities, recipes, tasks, journal content, map
discovery, save data, scene loading, audio mix state, notifications, or artwork.

## Screen lifecycle and root exclusivity

Only one UI-owned pausing root may be active. Repeated callbacks during opening/closing are ignored.
A request for the other root while one is active is rejected rather than switching behind a modal.
Root toggle behavior is defined in `PauseAndMenuFlow.md`.

```text
valid root request
  -> enter busy/opening guard
  -> suspend gameplay command input
  -> acquire pause through GameManager
  -> enable UI navigation
  -> show root
  -> establish valid first selection

Back / close
  -> close modal or child first
  -> close root
  -> clear UI selection
  -> clear transient gameplay input
  -> resume through the approved input flow
  -> release UI-owned pause exactly once
```

All menu animation, release-gating, and UI-flow lockout timers use unscaled time.

## Back, focus, and selection

- Back/Cancel closes the top modal or child first.
- At a root screen with no child, Back closes that root.
- Modal close restores its invoking control if still valid; otherwise use the parent's authored
  fallback.
- Each root, tab, child, modal, and intentional empty state authors a valid first selectable.
- Tab switching remembers a valid selection within the current open session.
- Dynamic Gear refresh retains the selected stable Gear key where possible; otherwise select the
  first visible entry or the empty-state Back target.
- Mouse clicks may move selection. Keyboard/controller navigation continues from a valid current
  selection.

## Device switching

A planned device-mode helper may update prompts and cursor presentation based on the latest
meaningful UI action. Device changes must not trigger menu requests or mutate gameplay input.
Final glyph assets, localization, binding override support, and cursor policy remain open.

## Input-map and Hero boundaries

A small always-enabled `System` map holds Pause and Gameplay Menu, with `UIFlowController` as its
sole persistent owner/enabler; the scene Hero never touches it. The existing `UI` map supplies
Navigate, Submit, Cancel, pointer, click, and scroll primitives.

```text
UIFlowController
  ├── requests GameManager.Pause() / Unpause()
  └── requests input suspension/resume through HeroController
        └── HeroInputReader owns sampling, buffer clearing,
            fallback gating, held-command tracking, and rearming
```

Buffer clearing removes previous intent; fresh-press rearming prevents a physically held command
from becoming new intent. Jump, Attack, Dash, Bind, Crouch, Interact, Sprint activation, and future
one-shot commands held through resume remain disarmed until individually released and freshly
pressed. Continuous movement keys/sticks may resume immediately.

## Scene-transition lifecycle

Pause and Gameplay Menu cannot open during scene exit, fade-out, loading, destination
initialization, placement, camera readiness, fade/reveal, scripted destination entry, remaining
transition-owned control lock, or the initial post-transition lockout. `SceneInit` is not
completion.

After the full transition finishes and state returns to Playing, begin a configurable unscaled
lockout with 1.0 seconds as the initial value. The successful falling edge of
`GameManager.IsSceneTransitioning` while Playing is the implemented A1 observation seam in
`UIFlowController.UpdateTransitionLockout()`.

Blocked requests are discarded. There is no pending root, automatic delayed open, or held-input
conversion. Pause and Gameplay Menu remain independently disarmed while their own controls are held;
releasing one rearms only that action.

## Quick Map versus Full Map

Map remains functionally deferred.

- Holding the future Map action shows a Local Quick Map while held.
- Local Quick Map does not pause and is not a `UIFlowController` root.
- It initially allows horizontal movement and normal gravity/falling while suppressing command
  actions through an approved limited-input request to `HeroController`.
- A proposed `QuickMapOverlay` never accesses Hero actions, action maps, buffers, or
  `HeroInputReader` directly.
- Double-tapping the same Map action closes Quick Map and requests the Gameplay Menu directly on the
  Full Map tab.
- Full Map is the ordinary pausing Gameplay Menu root and obeys all transition, lockout, focus, Back,
  and fresh-input rules.
- Tab is provisionally reserved for Map on keyboard. Keyboard I is the provisional Gameplay Menu
  candidate. Controller bindings remain open.

Quick Map restoration across transition is continuous-state restoration, not request queueing. It
is eligible only if Quick Map was already open from an uninterrupted Map hold when transition
began. Release clears eligibility; new transition-time presses cannot create it; tap history is
discarded. After transition completion and destination map-context readiness, only the destination
Local Quick Map may restore while that original hold remains physically down. The root one-second
lockout does not delay restoration by default.

Map identity uses stable authored room IDs compatible with `RoomVisitReporter`,
`WorldStateRegistry`, and World Graph Editor boundaries. Unity scene names are never map/save
identity.

## Save, scene-flow, and audio routing

Views do not call `SaveManager.Save()`, apply save data, or load scenes. A future quit flow resolves
save/discard policy, closes UI, releases UI-owned pause, completes input cleanup, then requests the
transition through the established scene-flow API.

Future Options controls call a narrow audio/settings service. `AudioManager` and the planned
profile-independent settings foundation own mixer groups, normalized-value conversion, persistence,
and startup application. UI one-shots route through `AudioManager.PlaySFX`.

## Semantic layering

Behavioral order, independent of specific sorting numbers:

1. Transition fade covers every gameplay UI layer.
2. Modal covers and blocks its parent root.
3. Active pausing root covers HUD interaction and receives navigation.
4. Future notifications are a sibling above HUD; their visibility beneath a covering root remains
   an open presentation policy.
5. HUD remains below covering roots and notifications.

Concrete Canvas sorting, camera assignment, raycasters, and override-sorting values are Unity
authoring details validated against the existing prefab.

## Sandbox and Gear relationships

`UISandbox.md` owns the direct-open, isolated preview workflow. It reuses shared production prefabs
without production managers, save files, or mutable state assets. Fixture-only future tabs never
create production gameplay systems.

`Gear.md` owns the physical-progression collection. Package A reads `PlayerAbilityState` and shows
only acquired approved definitions. The true-new-game collection is intentionally empty. Current
default-unlocked development state is a known ability/save contradiction and cannot be used to
populate starting Gear.

## Unity Editor authoring

Package A1 requires persistent `MenuRoot` composition, one EventSystem/input module, approved Input
Action references, Canvas/camera/raycaster setup, semantic sorting, first selections, explicit
navigation, modal raycast blocking, 1.0-second lockout tuning, and cross-scene Hero rebinding
validation. Package A2 adds Gear catalogue/definition references, layout, details, empty state, and
tab registrations.

Package A1 Unity Editor authoring performed: `System` map added to `InputSystem_Actions.inputactions`;
`MenuRoot`/`EventSystem`/`InputSystemUIInputModule`/`UIFlowController` authored under
`_GameCameras.prefab`; shared `PauseMenuScreen.prefab`/`ConfirmationModal.prefab` created and nested;
`UISandbox.unity` authored and confirmed excluded from Build Settings.

## Automated validation

Implemented Package A1 EditMode coverage: `UIFlowControllerTests` (20 tests) covering root
exclusivity, pause acquisition/release exactly once, repeated-callback guards, modal/root Back
order, Pause-while-modal-open closing only the modal, first selection, unregistered-Gameplay-Menu
safe rejection, transition/lockout rejection, no queued requests, and pause-safe teardown (owned
pause released exactly once, duplicates never interfere, idempotent when already closed).
`PauseMenuScreenTests` (14 tests) and `ConfirmationModalTests` (4 tests) covering production Options/Quit
gating and dynamic navigation, and confirmation-modal selection fallback. `UISandboxControllerFixtureTests`
(4 tests) covering deterministic Sandbox fixture presets. `HeroInputSuspensionPlayModeTests`
(PlayMode, 14 tests) cover the full input-leakage matrix. `UIFlowInputArbitrationPlayModeTests` adds
three real Input Action/player-loop Escape scenarios. During the 2026-07-27 hardening pass, focused
UI EditMode observed 42/42 passing; PlayMode Input System event advancement was blocked by the
available automated runner environment, so no new PlayMode pass is claimed. `UIFoundationValidator` (`Tools/Project/Validate UI
Foundation`) passed: single persistent EventSystem with a fully-wired `InputSystemUIInputModule`
(validated to fail when a reference is missing), no competing gameplay-scene EventSystem, Sandbox
build exclusion, production-asset isolation, Sandbox-only-component production exclusion, Canvas/
raycaster semantics, hidden-panel raycast safety. Tab filtering, Gear visibility/selection, and
future stable-ID Map validation remain Package A2+ work.

## Manual validation

Live-driven and confirmed in a real Play Mode session on `UISandbox.unity` this pass: the Sandbox
Options preview and Quit callback flows (open/close, focus restoration, confirm/cancel, callback
firing) and aspect-ratio presets — see `UISandbox.md` for the exact sequence observed.

Not performed this session — no interactive Play Mode input control was exercised against the
**production** Boot path. Planned validation still covers keyboard/controller/mouse in the real
gameplay scene, device switching, rapid and held root inputs, held command inputs through close,
movement through resume, every transition stage, death/respawn/hazard/boss restrictions, modal
selection restoration, scene-Hero recreation, Boot-driven entry, common aspect ratios, and safe
areas in the production HUD/Pause composition.

## Risks

- Input fallbacks bypassing action-map suppression.
- Scaled command buffers persisting at time scale zero.
- A stale Hero reference surviving scene load.
- Mistaking `SceneInit` for transition completion.
- External transition while UI owns pause leaving time scale or focus inconsistent.
- Duplicate persistent UI/EventSystem composition.
- UI inventing gameplay ownership for deferred tabs.
- Mutable development assets redefining product defaults.

## Open decisions

- Final System-map versus duplicated-open-action implementation.
- Exact controller and previous/next-tab bindings; keyboard I remains provisional for Gameplay Menu
  and Tab remains reserved for Map.
- Exact boss-introduction availability seam and whether active boss combat is pausable.
- Focus-loss auto-pause policy; auto-resume is prohibited.
- Final glyph, binding override, localization, cursor, artwork, typography, animation, motion,
  accessibility, aspect-ratio, safe-area, and display targets.
- Whether notifications remain visible beneath a covering root.
- Dedicated Map allowed-input list, gesture timing/arbitration, destination-context readiness, and
  restoration timing.
- Final transition-completion observation seam and post-playtest lockout tuning.
