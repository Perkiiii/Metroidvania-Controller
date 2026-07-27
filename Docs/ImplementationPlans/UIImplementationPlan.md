# Underbrew UI Systems — Implementation Proposal

**Status:** Proposal for review. This document is not authoritative architecture and does not
change the contracts in `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, or any FeatureSpec.

**Repository audit date:** 2026-07-27  
**Unity version inspected:** 6000.3.10f1  
**Implementation status:** No UI implementation described here has been performed or validated.

## Confirmed product direction

This proposal treats the player-experience decisions supplied with the task as confirmed product
direction:

- The root Pause menu and the tabbed Gameplay Menu are separate interfaces.
- Both freeze gameplay only through `GameManager.Pause()` / `GameManager.Unpause()`.
- Neither root can open during the full scene-transition lifecycle or the initial 1.0-second
  unscaled post-transition lockout. Blocked presses are discarded; controls must be released and
  freshly pressed after availability returns.
- Gear is a collection of physical progression objects, not a skill tree.
- Gear and Satchel remain separate.
- Gear reads ownership from authoritative gameplay state and never grants, purchases, tunes, or
  equips permanent abilities.
- Undiscovered Gear is absent: no silhouettes, mystery slots, unknown totals, or completion
  percentages.
- Map is not part of the first functional milestone.
- The future Map has two access modes over one shared map model/authored presentation: hold Map for
  a non-pausing Local Quick Map, and double-tap Map for the pausing Full Map tab.
- Local Quick Map is a hold-driven overlay, not a `UIFlowController` root. Full Map is the Gameplay
  Menu opened directly on Map and obeys every pausing-root availability rule.
- Future tabs may use isolated Sandbox fixtures, but fixture data must never become production
  gameplay ownership.
- Contextual interfaces such as dialogue, shops, and stations normally keep world time running and
  use control suppression or replacement instead of pause.

The external HUD, Inventory, Map, Pause Menu, and UI Overview research notes are design references
only. Their PlayMaker event model, all-owning `UIManager`, scene-name map identity, and reference
game inventory structures are not Underbrew implementation proposals.

---

## 1. Repository audit

### Implemented

| Area | Verified repository evidence | Consequence for this proposal |
|---|---|---|
| Persistent HUD lifetime | `Assets/_Project/Prefabs/Managers/_GameCameras.prefab` contains one `HUDRoot` with `PersistentHudRoot`. `GameCameras` and `PersistentHudRoot` both reject duplicate instances. | Preserve this composition. Do not create a replacement HUD or general UI owner. |
| HUD rendering | `_GameCameras.prefab` contains `HUDCamera`, configured as a URP Overlay camera and stacked on `MainCamera`. `HUD Canvas` is Screen Space - Camera and targets `HUDCamera`. | New persistent gameplay UI should use the established UI camera/layering contract unless a reviewed scene-local exception is needed. |
| Health HUD | `HealthDisplay`, `HealthSlotView`, and `PlayerHealthState` implement event-driven normal/bonus health presentation with neutral `StateApplied` and `Reset` handling. | Keep health presentation subscribed directly to `PlayerHealthState`; do not route through the scene Hero. |
| Resource HUD | `ResourceDisplay`, `ResourceBarView`, and `PlayerResourceState` implement one continuous, always-visible resource bar. | Do not reintroduce `ResourcePipView` or any pip/orb presentation. |
| Boss HUD | `BossHudEventService` is stateless. `BossHealthDisplay` owns the active source token and explicit aggregate `EnemyHealthComponent` roster. | Preserve the existing request and source-scoping architecture. |
| Persistent player state | `PlayerAbilityState`, `PlayerHealthState`, and `PlayerResourceState` are ScriptableObjects registered on `_SaveManager.prefab`. | UI reads these assets but never owns or mutates their gameplay values. |
| Ability ownership | `AbilityId` contains `Dash`, `WallCling`, `Sprint`, `WallLatch`, `DoubleJump`, `DriftCloak`, `SpiritCast`, and `Bind`. `PlayerAbilityState.SetUnlocked` raises `AbilityChanged` only for genuine value changes. | Gear can explicitly refresh from the current snapshot and react to genuine ownership changes without a new state event in the first pass. |
| Save/load | `SaveManager` applies Inspector-ordered `ISaveTarget` assets. `SaveStats` can inspect a slot without applying it. | Do not put Gear display data or user settings into gameplay save ownership. |
| Pause authority | `GameManager.Pause()` sets `GameState.Paused`, sets `Time.timeScale` to zero, and adds its Hero control lock. `Unpause()` reverses that work. | UI must call these seams and must never write `Time.timeScale`. |
| Scene flow | `SceneTransitionManager` owns `LoadSceneAsync` and transition state. `GameManager.IsSceneTransitioning` exposes that state. `Bootstrap` loads slot 0 and immediately routes to saved/fallback gameplay. | UI requests scene flow through established APIs; it never loads a scene directly. `SceneInit` is not a completion signal. |
| Input navigation primitives | `InputSystem_Actions.inputactions` contains `Player` and `UI` maps. `UI` already has Navigate, Submit, Cancel, pointer, click, right/middle click, scroll, and tracked-device actions. | Reuse the UI map instead of creating a parallel navigation implementation. |
| Existing UI tests | `HudDisplayTests` and `BossHealthDisplayTests` cover snapshot refresh, neutral changes, duplicate subscriptions, aggregation, and source-scoped hide behavior. | Extend coverage without rewriting these tests or their production targets. |

The current serialized `PlayerAbilityState.asset` has Dash, WallCling, Double Jump, and Bind set
true. Fresh-save defaults in `AbilitySaveData` and `PlayerAbilityState.ResetToDefaults()` are Dash
and WallCling true with the other six false. The serialized asset is therefore not a safe Sandbox
fixture or a reliable statement of starting presentation policy.

### Partially implemented

- `AudioManager` has persistent Music and SFX sources plus playback APIs, but no project AudioMixer,
  exposed mix parameters, settings API, profile-independent persistence, or startup settings
  application. The sources on `_AudioManager.prefab` are not routed to project mixer groups.
- Project Player Settings specify a default resolution and fullscreen mode, but there is no runtime
  display-settings model, resolution enumeration, apply/revert flow, or settings persistence.
- `GameManager` owns `GameState`, but exposes no state-changed event and has no source-aware pause
  ownership. A single `UIFlowController` can safely own the first UI pause request without adding a
  general pause-token framework, provided repeated requests and interruption paths are guarded.
- No transition-completed event exists. On success, `SceneTransitionManager` finishes destination
  placement, camera readiness, reveal/fade, scripted entry motion, and gameplay restoration before
  clearing `IsTransitioning` in its `finally` block. The falling edge of the existing
  `GameManager.IsSceneTransitioning` property is therefore the narrowest current observable
  completion seam. `GameManager.SceneInit` fires before the destination Hero is cached and before
  reveal/entry work, so it cannot start menu availability.
- Boss encounters expose instance events such as `EncounterStarting`, `EncounterActivated`, and
  `EncounterInterrupted`, but there is no global “menus currently allowed” signal.
- `SaveManager` is slot-aware and `SaveStats` is ready for a future frontend, while play-time
  accumulation and all slot UI remain deferred.

### Missing

- Root Pause menu, Gameplay Menu, Gear screen, modal/confirmation layer, notifications, Options UI,
  frontend UI, and UI Sandbox.
- A focused menu-flow coordinator.
- Project-owned `EventSystem` and `InputSystemUIInputModule` authoring.
- Dedicated Pause and Gameplay Menu input actions.
- UI-specific previous/next-tab input.
- Explicit gameplay-input suspension and transient-buffer clearing.
- Release gating when a UI close control overlaps a gameplay control.
- Menu focus restoration, device-change presentation, control-glyph resolution, safe-area tooling,
  menu tests, and a project UI validator.

### Planned but not implemented

- `Docs/Architecture.md` illustrates a `Menus` branch under `_GameCameras`, but no such branch exists
  in the prefab.
- `Docs/ImplementationPlan.md` lists Pause menu, main menu, save slots, play time, full SFX, and
  transition-owned music as future work.
- Map discovery, a general item inventory, Recipes, Tasks, Journal, and production data for their
  tabs are not implemented gameplay systems.
- The dual Quick/Full Map behavior is confirmed product direction only; no Map Input Action,
  controller, overlay, double-tap recognizer, map model, or Map tab currently exists.

### Stale documentation and contradictions

These findings should be corrected only when the relevant authoritative document is deliberately
updated after implementation approval:

1. `Docs/Architecture.md` says `PlayerResourceConfig.partsPerPip` controls visual grouping. Current
   `ResourceDisplay` explicitly ignores it and renders one continuous bar.
2. `Docs/Architecture.md` describes `PlayerAbilityState` as seven flags; code and save data contain
   eight.
3. `Docs/FeatureSpecs/Abilities.md` still lists `PlayerResourceState` as TODO even though it is
   implemented, saved, displayed, and used by Bind.
4. `Docs/FeatureSpecs/Audio.md` lists scene music as a `GameManager.BeginSceneTransition` call site,
   but repository search finds no `PlayMusic` call outside `AudioManager`. `Architecture.md`
   correctly calls the routing deferred.
5. `Docs/ImplementationPlan.md` says `SampleScene` is the only scene. Build Settings currently
   enables `Boot`, `SampleScene`, `SampleScene2`, `SampleScene3`, and `SampleScene4`.
6. `Docs/DocsAudit.md` is historical and still reports that no HUD and no `PlayerAbilityState`
   exist.
7. Two `HudDisplayTests` method names still mention full pips and a hidden zero-capacity display,
   although their assertions correctly validate a visible continuous bar.
8. `AGENTS.md` folder notes still label `TransitionPoint` and `WorldStateRegistry.asset` planned even
   though both are implemented.

### Relevant technical debt and risks

- `HeroInputReader.Tick()` continues while the game is paused. Its jump and attack buffer timers
  use scaled `Time.deltaTime`, so a buffer created at time scale zero does not expire.
- Disabling the `Player` action map alone is insufficient: the reader contains direct
  keyboard/mouse fallbacks for attack, dash, and sprint which are evaluated even when their Input
  Actions exist.
- UI Cancel commonly resolves to Gamepad East. East is also the current Bind/Crouch gameplay input.
  Re-enabling gameplay while East is still held can immediately start or hold a gameplay action.
- Clearing buffers is insufficient when a command remains physically held through menu closure.
  Jump, Attack, Dash, Bind, Interact, Sprint activation, and other commands require release followed
  by a fresh press after resume; continuous movement is intentionally exempt.
- `GameManager.Unpause()` always restores time scale to 1. This matches current project behavior,
  but UI flow must not call it unless the UI actually owns the active paused state.
- Starting a scene transition while a pausing root remains open can leave time scale at zero because
  transition state changes do not themselves restore it. Package A therefore rejects external
  transitions while it owns a pausing root. A future approved frontend flow must close UI, clean
  input, and release that pause before requesting a transition.
- Pause and Gameplay Menu presses during a transition or post-transition grace period must be
  discarded. A held menu control cannot become a delayed open after availability returns.
- Future Map handling must distinguish uninterrupted continuous state from tap history: Local Quick
  Map may restore after transition/context readiness only when it was already open from the Map hold
  at transition start and that original hold was never released. A new transition-time hold is
  ineligible, while all Full Map double-tap history and requests are discarded.
- No project-owned focus-loss or controller-disconnect policy exists.

---

## 2. Architecture proposal

### Persistent runtime HUD

Keep `PersistentHudRoot`, `HealthDisplay`, `ResourceDisplay`, and `BossHealthDisplay` in their
current ownership and lifecycle. Menu work may visually cover or locally fade the HUD, but must not
disconnect it from persistent state, rebuild it on room load, or make it a child of a menu screen.

### Persistent menu composition

Propose a separate sibling composition under the persistent `_GameCameras` prefab:

```text
_GameCameras
├── MainCamera / HUDCamera
├── HUDRoot                            existing, PersistentHudRoot
├── NotificationRoot                   future/deferred presentation composition
├── MenuRoot                           proposed
│   ├── UIFlowController
│   ├── EventSystem
│   ├── RootInterfaceLayer
│   │   ├── PauseMenuScreen
│   │   └── GameplayMenuScreen
│   └── ModalLayer
└── FadeCanvas                         existing, transition-owned
```

`UIFlowController` is a focused coordinator, not an all-owning `UIManager`. It may own:

- Which root interface is active.
- Open, close, and root-switch requests.
- Gameplay Menu active-tab and current-session last-tab memory.
- A shallow modal/back hierarchy.
- Calls to `GameManager.Pause()` / `Unpause()`.
- Input-map mode and release-gated resume sequencing.
- Root-menu availability across the full transition lifecycle and its post-transition lockout.
- EventSystem first selection and selection restoration.
- Requests to established game-flow APIs.

It must not own health, resource, abilities, save data, inventory quantities, recipes, tasks,
journal state, map discovery, scene loading, gameplay tuning, or artwork.

`NotificationRoot` is not owned by `UIFlowController`. It is a future presentation composition for
ability/item acquisition, area titles, save indicators, and tutorial prompts. `UIFlowController`
may expose whether a root currently covers the screen, but it does not own notification queues,
acquisition/save/area-title events, or notification content. Package A may show static notification
fixtures in the Sandbox only.

Low-level Hero input remains outside `GameManager`:

```text
UIFlowController
  ├── requests GameManager.Pause() / Unpause()
  └── requests input suspension/resume through HeroController
        └── HeroInputReader owns sampling, buffer clearing,
            fallback gating, held-command tracking, and fresh-press rearming
```

`HeroController` is only a thin forwarding boundary. The persistent flow must not retain a Hero
from an unloaded scene. The repository already exposes `GameManager.SceneInit` as an invalidation
point and an internal `GameManager.CurrentHero` cache after scene load. Because `SceneInit` fires
before that cache is assigned, implementation must clear an old reference immediately and
query/rebind only after the cache is ready, or query the existing current-Hero seam just in time
instead of storing it. No `GameManager` input logic is required.

Use small screen-specific components rather than a generic window framework. A minimal shared
screen contract may expose `Show`, `Hide`, `FirstSelection`, and `HandleBack`; it should not force
Gear, Options, future Map, and modal content into one layout model.

### Semantic layering

Layering rules are behavioral, not magic-number contracts:

1. Transition fade covers every gameplay UI layer.
2. Modal confirmation covers and blocks its parent root.
3. The active root menu covers gameplay HUD interaction and receives navigation.
4. Notification presentation is a sibling of MenuRoot. It appears above the HUD during gameplay,
   while visibility under a covering root remains a presentation policy.
5. HUD remains below root interfaces and notification presentation.

The current prefab has concrete sorting values for the HUD and fade. Implementation authoring must
inspect those values and select compatible numbers, camera assignments, layers, raycasters, and
override-sorting settings. Only the semantic ordering above is architectural.

### Scene-local frontend

A future frontend remains scene-local. It may use `GameCameras.HudCamera` through an explicit
binder or its own reviewed scene canvas, but it must not become another persistent singleton. It
requests New Game, Continue, slot selection, or transitions from established services once those
features are separately approved.

### Future contextual interfaces

Dialogue, shops, crafting/brewing stations, physical recipe books, construction, and base
management remain outside the pausing root-menu state. The architecture must leave space for a
scene-local contextual-interface coordinator that:

- Uses `HeroController.AddControlLock` / `RemoveControlLock` or a future explicit input replacement
  seam.
- Keeps `Time.timeScale` unchanged.
- Reuses UI navigation/focus utilities where appropriate.
- Does not register itself as a Gameplay Menu tab merely to share UI code.

### Future dual Map architecture

Exact names remain proposals pending a dedicated Map specification:

- `MapController` or equivalent owns map content, stable authored room/zone resolution, discovery
  presentation, and shared local/full viewing state.
- `QuickMapOverlay` or equivalent owns the hold-driven, non-pausing local presentation.
- `QuickMapOverlay` does not manipulate Hero actions, action maps, input buffers, or
  `HeroInputReader`. It may request an approved limited-input mode only through the thin
  `HeroController` boundary.
- The Hero input layer remains responsible for allowing horizontal movement while suppressing the
  approved command actions.
- `UIFlowController` may route a valid Full Map shortcut into the Gameplay Menu's Map tab, but does
  not own map discovery or map data.
- Quick Map is not a root menu and never calls `GameManager.Pause()`.
- Local Quick Map and Full Map reuse the same underlying map model and authored map visual.
- Map identity continues to use stable authored room IDs, never Unity scene names.

The first recommended Quick Map control policy is horizontal movement plus normal gravity/falling.
Jump, Attack, Dash, Bind, Interact, Sprint activation, and other command actions remain suppressed
unless later approved. The exact allowed-input list is a product decision for the dedicated Map
proposal. Package A must not introduce a generic limited-input framework solely for this deferred
feature; implementation waits for the dedicated Map specification.

---

## 3. Pause and input flow

### Input Action options

The current asset has no Pause or Gameplay Menu action. Two practical designs are:

| Option | Benefits | Costs |
|---|---|---|
| Add a small always-enabled `System` action map containing Pause and Gameplay Menu, with room for a future Map action | Open/close actions survive Player/UI map switching; one binding source; independent of scene Hero lifetime; future persistent held-state Map observation remains possible. | Adds a third map and requires clear ownership so only the focused input/UI routing layer enables it. Package A still does not add or implement Map. |
| Put open actions in Player and duplicate close bindings in UI | Uses only existing maps. | Duplicates bindings and callbacks, makes repeated requests easier, and complicates opening/closing when one map is disabled. |

**Recommendation requiring approval:** use a small `System` map. `HeroInputReader` currently enables
and disables specific actions by name from the `Player` map; it never enables an entire asset.
Therefore a System map will not be accidentally toggled by the Hero if `UIFlowController` is its
sole enabler. This is a justification, not a confirmed rule.

Candidate bindings, all still open for approval:

- Pause: Keyboard Escape; controller Start/Menu.
- Gameplay Menu: Keyboard I; controller binding remains open.
- Future Map: Keyboard Tab is provisionally reserved for hold Quick Map / double-tap Full Map;
  controller binding remains open.
- UI Cancel: existing UI Cancel bindings.
- Previous/Next tab: controller shoulders plus approved keyboard keys on the UI map.

Do not reuse Player `Previous`/`Next` because they are gameplay actions and currently use D-pad
left/right, which conflicts with ordinary menu navigation.

The future input architecture leaves room for three distinct actions: Pause, Gameplay Menu, and
Map. Tab must not be assigned to Gameplay Menu. Package A implements only the approved Pause and
Gameplay Menu strategy; it does not add the Map action, hold/double-tap recognition, or Map
interaction logic without separate approval.

### Confirmed transition availability and request rejection

Both root interfaces are unavailable throughout the complete transition-owned lifecycle:

- Scene exit and any exit-owned control lock.
- Fade-out.
- Loading.
- Destination scene initialization.
- Destination placement.
- Camera readiness.
- Fade/reveal.
- Scripted scene-entry motion.
- Any remaining transition-owned control lock.
- The post-transition menu lockout.

`SceneInit` is explicitly not completion: `GameManager.OnSceneLoaded` raises it before caching the
destination Hero, and `SceneTransitionManager` still performs placement, camera readiness,
fade/reveal, and entry motion afterward.

The repository has no completion event. The narrow existing observation is the successful falling
edge of `GameManager.IsSceneTransitioning` with `GameManager.State == GameState.Playing`, which
occurs when `SceneTransitionManager` reaches its final cleanup after gameplay restoration.
`UIFlowController` can observe that transition state without adding menu business logic to
`SceneTransitionManager`. If implementation proves the falling-edge observation ambiguous, the
only proposed addition is a narrow transition-completed notification owned by the existing
transition/game-flow system.

When completion is observed:

1. Start an Inspector-authored unscaled lockout on `UIFlowController`.
2. Use `1.0f` seconds as the initial proposed value.
3. Reject Pause and Gameplay Menu for the entire lockout.
4. Track Pause and Gameplay Menu arming independently. Any action held during transition or
   lockout remains disarmed until that same action is released.
5. Releasing Pause rearms only Pause; releasing Gameplay Menu rearms only Gameplay Menu.
6. After the timer, a subsequent fresh press may open its root when that action is armed and every
   other availability condition passes, even if the other root action remains held.

The value is UI-flow/input presentation tuning. It does not belong in `HeroConfig`,
`HeroAbilityConfig`, `CameraConfig`, a `TransitionPoint`, Hero movement tuning, boss tuning, or save
data.

Every unavailable root-menu request is rejected immediately. Do not store a pending request, set a
queued-open flag, open automatically when a block ends, or convert a held control into a delayed
open. This no-queue rule also applies to death, respawn, hazard recovery, non-interruptible boss
presentation, and every other root-menu availability block. A rejected request produces no later
state change.

### Future Quick Map transition lifecycle

The Local Quick Map follows a distinct continuous held-state contract:

- Holding the dedicated Map control displays the current local-area overlay only while held.
- It does not pause, call `GameManager.Pause()`, or become a pausing root.
- It allows horizontal movement and normal gravity/falling under the initial recommended policy.
- It suppresses command actions unless the dedicated Map specification later approves them.
- Double-tapping the same Map control closes Quick Map and requests the pausing Gameplay Menu
  directly on Map. The same Full Map is also reachable through ordinary Gameplay Menu navigation.

At transition start, record restoration eligibility only if Local Quick Map is currently open
because the Map control is held, then close it immediately. The previous room's map must not render
over exit/fade, loading, destination reveal, or scripted entry.

During transition:

- Releasing Map clears restoration eligibility.
- A new Map hold begun during transition does not create restoration eligibility.
- New presses, taps, and double-taps display nothing and are discarded.
- Do not preserve Full Map tap history, a pending Map request, or any other queued open state.

After the destination transition fully completes and destination map context is ready:

- Restore Local Quick Map only if it was open when transition began, the original hold remained
  uninterrupted, restoration eligibility was never cleared, and Map is still physically held.
- If Map was released at any point, it stays closed. A new hold begun during transition does not
  restore it automatically.
- Eligible held restoration requires no new press because it restores uninterrupted continuous
  state; it is not a stored request or queued past press.
- Held restoration can only open the non-pausing Quick Map. It never opens Full Map.
- The root-menu 1.0-second post-transition lockout does not apply to Quick Map restoration by
  default. Initial timing may change after Underbrew playtesting.
- Full Map remains blocked by the root grace/release/fresh-press contract. Repeated transition taps
  cannot create a later Full Map open.

The future Map input path must observe release of an eligible original hold across transition and
the final physical held state wherever the approved System/input architecture permits. It must not
treat a new transition-time hold as eligible or preserve tap history or a pending open.
Destination map-context readiness belongs to the future Map system, not `SceneInit`.

### Gameplay-input suspension and fresh-press resume

Action-map switching and the Hero control lock serve different purposes:

- `GameManager.Pause()` remains the authoritative gameplay pause and adds the approved Hero control
  lock.
- Disabling the Player map prevents normal Input System actions from producing new gameplay input.
- A new low-level suspension seam prevents `HeroInputReader` from sampling legacy fallbacks and
  clears already-buffered transient input.
- A resume-disarmed state prevents commands already held when Player actions are re-enabled from
  creating new intent.

Input has two behavioral groups:

- **Continuous gameplay input:** movement direction and analogue movement may resume immediately
  when gameplay control is restored. Movement keys/sticks do not require a neutral release.
- **One-shot/command input:** Jump, Attack, Dash, Bind, Crouch, Interact, Sprint activation where
  applicable, and future actions that should start from a new press. Each command already held at
  resume remains ignored until that control is released, then accepts only a subsequent fresh
  press.

Proposed minimal behavior:

```text
Pause request accepted
  -> mark UI transition busy
  -> disable Player action map
  -> suspend Hero input sampling and clear transient input
  -> GameManager.Pause()
  -> enable UI map / InputSystemUIInputModule
  -> show requested root
  -> establish first selection

Root close accepted
  -> close modal/child first when applicable
  -> clear EventSystem selection
  -> keep gameplay paused
  -> suspend Hero command sampling
  -> clear gameplay buffers and transient snapshots
  -> wait for overlapping UI close controls to release
  -> disable UI navigation mode
  -> re-enable Player actions
  -> record command controls currently held
  -> place Hero command input into resume-disarmed state
  -> release the UI-owned pause through GameManager
  -> restore continuous movement sampling
  -> ignore each held command until released
  -> accept only a later fresh press
```

The exact method names are implementation details. The seam belongs behind `HeroController` so UI
does not reach into `HeroInputReader`, preserving the rule that action code and the coordinator own
reader access. It must reset:

- Jump/attack buffer timers.
- Queued jump release.
- Pressed/released/held snapshots.
- Movement vector.
- Bind/Interact transient snapshots.

Buffer clearing removes old buffered intent. Resume disarming prevents still-held controls from
creating new intent after the clear. Both are required. Explicit release gating remains necessary
where the UI close action itself overlaps gameplay, especially UI Cancel/Gamepad East and
Bind/Crouch. Enabling the Player map must not itself create a fresh buffer or synthetic press.

Legacy direct fallbacks should preferably be removed after all production bindings are verified.
If retained for development safety, every fallback read must honor input suspension. Map disabling
alone is not accepted as leakage prevention. `GameManager` does not learn about action maps,
buffers, fallback reads, held controls, resume disarming, UI Cancel, or EventSystem focus.

### Focus and selection

- Each root, tab, child, and modal authors a valid first selectable.
- Opening selects the remembered valid control or falls back to the authored first control.
- Closing a modal restores its invoking control if it is still active and interactable.
- Switching tabs restores that tab's last valid selection for the current open session.
- Rebuilding Gear retains the selected Gear key when still visible; otherwise it selects the first
  visible entry or the empty-state Back control.
- Pointer click may move selection; controller/keyboard navigation then continues from that item.
- A device-mode tracker may switch prompts and cursor presentation from the control that performed
  the latest meaningful UI action. It must not change gameplay ownership.

### Repeated request safety

`UIFlowController` maintains explicit `Closed`, `Opening`, `Open`, and `Closing` behavior or
equivalent guards. Repeated Pause/Menu callbacks during an opening/closing frame are ignored.
Requesting the already-open root closes it only through that root's approved toggle behavior.
Requesting the other root while one is open is rejected rather than switching roots behind a
modal. Rejected inputs during any availability block are discarded with no pending state.

All menu animation, post-transition lockout, and release-gating timers use unscaled time.

---

## 4. Pause restrictions and lifecycle

### Initial open predicate

Root menus may open only when all currently verifiable conditions pass:

- `GameManager.Instance` exists.
- `GameManager.State == GameState.Playing`.
- No scene transition is in progress.
- The full post-transition unscaled lockout has elapsed.
- The specific requested root action is armed. Pause and Gameplay Menu maintain independent
  armed/disarmed state; the other action need not be released for this request to pass.
- No respawn or hazard recovery is in progress.
- Persistent health is not depleted.
- No modal/root is already transitioning.

Failure rejects the current request and records nothing for later. This predicate prevents
transition, grace-period, death, respawn, recovery, and repeated-request cases without a new
generic lock system.

### Lifecycle rules

| Situation | Proposed rule |
|---|---|
| Scene transition and post-transition lockout | Reject both roots from exit through final transition cleanup and for 1.0 unscaled second afterward. Never queue. Each root action remains independently disarmed until its own release; a fresh press of an armed root action may open even while the other remains held. Package A must not accept an external transition while UIFlow owns an open pausing root; ordinary traversal cannot begin while gameplay is frozen and Package A has no scene-loading menu action. |
| Future Local Quick Map | If Quick Map is open from a held Map control at transition start, record restoration eligibility and close it immediately; otherwise record no eligibility. Render nothing during transition. Release clears eligibility, and a new hold during transition cannot create it. After completion plus destination-map readiness, restore only when the original hold was uninterrupted and Map remains held. This continuous-state restoration is not queued and does not use the root grace period by default. |
| Future Full Map | Treat as a pausing Gameplay Menu root shortcut. Reject double-taps during transition/grace with no queue. After grace, require release and a fresh approved gesture before opening Map. |
| Hero death | Reject opens while health is depleted. Never queue. If a future death can occur during a non-freezing UI, close it without resuming or changing death flow. |
| Respawn / hazard recovery | Reject opens while `IsRespawnOrRecoveryInProgress`. Never queue or interfere with GameManager control locks/fade. After the owning lifecycle finishes, each root action rearms independently on its own release. |
| Boss introduction/presentation | Product rule: reject and discard requests during non-interruptible intro/presentation. Current repository lacks a global signal. Before implementation, choose a narrow adapter from boss lifecycle events or a small menu-availability seam; do not add a broad token framework without reviewing concrete callers. Normal active boss combat may remain pausable unless design decides otherwise. |
| Repeated Pause/Menu request | Ignore while opening/closing; never issue duplicate Pause or Unpause calls. |
| Another root is open | Modal back closes modal first. Root back closes its root. A request for the other root is rejected until closed. |
| Focus loss | Never auto-resume. Whether ordinary gameplay automatically opens Pause on focus loss is an open platform/design decision. If adopted, apply it only in eligible Playing state; do not open over transitions, death, recovery, boss presentation, frontend, or an existing interface. |
| Controller disconnect | Keep current state and time scale. Preserve selection, expose keyboard/mouse fallback, and show an optional non-owning prompt. Never auto-close or auto-resume. |

The first pass does not need source-counted pause tokens because one persistent flow controller
owns at most one pausing root. If future systems independently pause, pause ownership must be
revisited before adding callers.

A future UI-initiated return-to-main-menu transition requires a separately approved sequence:

```text
confirm return
  -> resolve save/discard policy
  -> close modal and root presentation
  -> release the UI-owned pause
  -> complete input cleanup
  -> request transition through the established scene-flow API
```

That sequence is deferred until a real frontend destination and save policy are approved. Package A
does not implement a generic paused-transition interruption path.

---

## 5. Quit-to-main-menu dependency

The confirmed Pause menu contains Quit to Main Menu with confirmation, but there is no main-menu
scene. `Bootstrap.Start()` immediately loads slot 0 and enters gameplay. Loading `Boot` as a
destination would simply re-enter gameplay and is not a main menu.

### Options

1. **Flow-request-only first pass.** Implement the button, confirmation hierarchy, cancel/back
   behavior, and a typed `QuitToMainMenuRequested` seam. The internal vertical slice verifies the
   request, but no production scene transition occurs. This is not a shippable functional Quit
   endpoint; release UI must hide or clearly gate the final action until a destination is approved.
2. **Separately approved minimal frontend dependency.** Add a narrow scene and transition
   integration expressly for the destination. This necessarily introduces decisions about what
   the landing screen can do, whether unsaved state is discarded/reloaded, and how the player
   leaves it. It must not incidentally add New Game, Continue, slots, or general Boot rerouting.
3. **Move functional Quit to the later frontend milestone.** Keep the confirmed structure in
   design/prefabs but do not expose the production button until the frontend exists.

**Recommendation:** Package A implements option 1 for flow and modal validation, while functional
transition remains deferred. Option 2 requires separate approval. Do not silently create a
frontend scene.

Open dependency: decide whether returning to a future main menu saves, discards to the last
checkpoint, reloads the current slot, or preserves in-memory unsaved state. Existing save rules do
not authorize a save from this UI flow.

---

## 6. Options vertical slice and settings foundation

### Current support

- `AudioManager` can play Music and SFX but exposes no volume controls.
- `_AudioManager.prefab` sources have ordinary source volume values and no project mixer routing.
- Hero-local sources live under `Hero/Sounds/*` and bypass `AudioManager` playback by design.
- No project code uses `PlayerPrefs`, a settings file, `Screen.SetResolution`, or
  `Screen.fullScreenMode`.
- No runtime settings are applied during startup.

### Smallest genuinely functional subset

Recommend Master, Music, and SFX volume as Package B. Display mode and resolution remain deferred
until supported platforms, aspect ratios, window behavior, confirmation/revert UX, and persistence
are approved.

Audio settings are a distinct foundation, not UI view behavior:

1. Create a project AudioMixer with Master, Music, and SFX groups.
2. Expose reviewed Master/Music/SFX attenuation parameters.
3. Route `_AudioManager.prefab` Music and SFX sources to the matching groups.
4. Route every Hero-local action source to SFX while preserving `HeroAudioController` playback
   ownership.
5. Confirm enemy/world/UI one-shots inherit SFX routing, including pooled sources copied by
   `AudioManager`.
6. Add a small settings model/store independent of `SaveManager` and save slots.
7. Load and apply settings during persistent audio startup before normal playback.
8. Expose narrow audio-setting APIs to Options; sliders never touch AudioMixer directly.
9. Convert normalized slider values to decibels in the audio/settings layer, for example
   `20 * log10(max(linear, epsilon))`, with an approved mute floor such as -80 dB.
10. Route UI navigation/confirm/cancel sounds through `AudioManager.PlaySFX`.

Compare delivery:

- Including this foundation in Package A creates additional mixer, Hero prefab, AudioManager,
  persistence, and startup risk while the menu/input spine is still settling.
- Delivering it immediately after Package A lets the Pause/Options navigation contract be tested
  first and gives audio routing its own review gate.

**Recommendation:** Package B immediately follows Package A. Package A may exercise the Options
route with a Sandbox-only placeholder, but production Options is not considered functional until
Package B. Do not ship a fake settings screen.

---

## 7. Gear vertical slice

### Ownership contract

`PlayerAbilityState` remains the authority for ability unlocks. Gear:

- Reads `IsUnlocked(AbilityId)`.
- Performs an explicit full refresh when initialized or opened.
- Subscribes to `AbilityChanged` while active and refreshes ownership presentation only when values
  genuinely change.
- Never treats `AbilityChanged` as an acquisition-notification event.
- Does not require a new `PlayerAbilityState.StateApplied` event in this proposal.

Explicit refresh covers initialization, opening after boot load, and reopening after any state
application. While Gear is already open, changed values already raise `AbilityChanged`; an
identical save application cannot change visible ownership. No concrete current lifecycle requires
another event. Reconsider a neutral state-applied event only if future slot switching or live load
can alter an open screen without either a value change or an explicit screen refresh.

Future acquisition toasts must consume an explicit pickup/presentation event carrying acquisition
context. They must not infer acquisition from save loading or from a raw ownership flag changing.

### Display data

Proposed display-only data:

- `GearDisplayDefinition`: stable Gear display key, one required `AbilityId`, display name, optional
  mechanical label, category, icon/artwork, functional description, control-hint metadata, flavor
  text, display order/group, and optional Acquired/Active wording.
- `GearDisplayCatalog`: ordered collection and lookup/validation surface.

Display definitions do not contain unlock state, purchase/equip state, tuning, velocities,
cooldowns, blackboard flags, or `HeroAbilityConfig` values. Keep Unity layout references and
coordinates out of the definition so the same data can drive multiple Sandbox layouts.

For Package A every production definition maps to exactly one required `AbilityId`. The stable key
exists for selection restoration, view/layout identity, validation, and tests; it is not a second
ownership key. Do not add ownerless production definitions, generic ownership resolvers, multiple
provider interfaces, speculative permanent-item state, or weapon/artefact ownership without an
authoritative gameplay owner. Future non-ability Gear is added only after its owner exists.

### Layout comparison

Prototype these lightweight alternatives in the Sandbox:

| Approach | Behavior | Strengths | Risks |
|---|---|---|---|
| Dynamic authored-group layout | Authored category/group containers; acquired entries instantiate naturally inside visible groups. Empty groups collapse. | Bespoke hierarchy with responsive density; no undiscovered holes; data remains layout-independent. | Needs group/order authoring and navigation rebuilding. |
| Authored collection layout | A bespoke physical collection/tableau maps acquired Gear keys to authored presentation anchors or zones; absent entries instantiate nothing and leave no mystery framing. | Strong physical-progression identity and art direction. | Must ensure empty space does not reveal totals; more aspect-ratio and navigation work. |
| Simple list/grid | Visible acquired definitions in order with a details panel. | Fastest, easiest to navigate and test. | Risks reading as a conventional inventory and may undersell Gear’s physical-progression identity. |

**Recommendation requiring UI/UX approval:** prefer a bespoke physical-progression presentation,
using either dynamic authored groups or an authored collection that adds only acquired objects.
Use the simple list as a functional Sandbox comparator and fallback, not a locked production
decision. Final layout is chosen after keyboard/controller/mouse and aspect-ratio prototypes.

### Visibility and content

- Iterate catalog definitions and create presentation only when the mapped ability is unlocked.
- A definition for a locked ability creates no button, placeholder, anchor marker, silhouette, or
  count.
- An unlocked ability with no definition is omitted safely and logged once for development.
- A validator reports duplicate Gear keys, duplicate `AbilityId` mappings, null artwork where
  required, invalid groups, and missing definitions for the explicitly approved production Gear
  set.
- Do not author production definitions merely because an enum member exists. Sprint, WallLatch,
  DriftCloak, and SpiritCast do not gain screens or ownership models through this work.

Double Jump must use an approved Underbrew physical traversal-object name, artwork, and flavor
text. A raw “Double Jump” production icon is not acceptable. The exact identity remains an
art/design decision.

Starting Dash and WallCling are true in fresh saves, but whether their physical objects appear in
Gear is open. Options are:

- Show them as acquired starting possessions once their physical identities are approved.
- Treat them as baseline capabilities and omit them from the Gear catalog.

Do not infer the answer from the current mutable ScriptableObject asset.

### Selection and details

- Select by stable Gear key, not list index.
- Rebuild layout/navigation after ownership changes and retain selection when possible.
- A selected item may show item name, category, large art, functional description, resolved
  control hint, flavor, and optional Acquired/Active wording.
- Empty state contains no unknown totals. It provides approved copy and a valid Back selection.
- Dynamic layouts explicitly wire navigation after rebuild and scroll/focus the selected item.
- Mouse click selects and opens details without changing ownership.

### Control hints and glyphs

Definitions may reference an Input Action identity plus authored hint text/template. A small
resolver can use Input System binding display strings for the latest active device, with text
fallback when no sprite glyph exists. Multi-input behaviors such as wall movement may need
authored phrasing around resolved action names.

The final glyph library, platform icon assets, binding override support, and localization strategy
remain open. Do not hardcode internal action names as final player-facing copy.

---

## 8. Gameplay Menu shell

The shell supports eventual Gear, Tools, Satchel, Recipes, Tasks, Journal, and Map without assuming
their content layouts match.

Propose:

- A `GameplayMenuTabId` enum or equivalent stable UI identity.
- A small serialized tab registration containing ID, label/icon presentation, root content object,
  tab control, availability source, and first selectable.
- Independent tab screen components. Shared details-panel helpers are optional, not mandatory.
- Production availability filtering before navigation links are built.
- A separate Sandbox override that can expose fixture-only tabs.

Behavior:

- First open in a play session selects Gear.
- Later opens restore the last viewed tab if it remains available; otherwise fall back to Gear.
- Memory is runtime-only on the persistent flow root and is never saved.
- Back from a tab root closes the Gameplay Menu. Back from a child/modal closes that child first.
- Tabs with no production ownership are hidden, not disabled or filled with fake data.
- A production-backed tab may define its own honest empty state.
- Adding Map later requires a new registration and independent Map screen, not rebuilding the shell
  or forcing Map into the Gear layout.
- A valid future Full Map shortcut first closes Local Quick Map, then asks `UIFlowController` to
  open this same shell directly on the available Map tab. It uses ordinary UI navigation/back and
  `GameManager.Pause()` ownership; it is not a separate pausing screen.

**Recommendation requiring approval:** Package A exposes only Gear in production. Tools, Satchel,
Recipes, Tasks, Journal, and Map remain hidden until their authoritative systems exist.

---

## 9. UI Sandbox

Create a development-only `UISandbox.unity` or equivalent direct-open workflow. It is excluded from
Build Settings and does not run the production Boot/save flow.

### Isolation

- Create runtime-only `PlayerHealthState`, `PlayerResourceState`, and `PlayerAbilityState`
  instances or clones that are never saved.
- Apply explicit fixture values; never mutate the production state assets.
- Do not read or write save files.
- Do not instantiate production manager singletons solely to make a preview work.
- Do not instantiate a second full production `PersistentHudRoot` solely for preview.
- Shared visual/menu prefabs are nested into Sandbox-owned presentation roots so UI/UX changes can
  be applied back intentionally.

Preferred composition:

```text
UISandbox
└── SandboxPreviewRoot
    ├── HealthDisplay configured with isolated PlayerHealthState
    ├── ResourceDisplay configured with isolated PlayerResourceState
    ├── BossHealthDisplay fixture adapter
    ├── Pause/Menu shared prefabs
    └── Gear shared prefabs
```

This previews real reusable views with isolated runtime state. Production `_GameCameras` and
`PersistentHudRoot` wiring are validated separately rather than duplicated inside the Sandbox.

### Preview matrix

- Health: full, damaged, bonus health, capacity changes, and empty/death frame.
- Resource: empty, partial, full, and zero-capacity safety.
- Boss: hidden, one source, aggregate sources, long/short names.
- Gear: starting-only, several acquired, all approved definitions, no acquired entries, missing
  definition warning, and locked/unlocked combinations.
- Pause: root, Options route placeholder for Package A, confirmation modal, back restoration.
- Gameplay shell: Gear production availability plus lightweight fixture-only tabs.
- Notifications/prompts: representative layout fixtures only; no acquisition gameplay.
- Keyboard, controller, and mouse navigation/device switching.
- Common 16:9, 16:10, 21:9, and 4:3 development previews until final targets are approved.
- Safe-area guides and deliberate edge/cutout visualization.

Future tabs need only enough fixture content to test shell layout, tab filtering, focus, and broad
art direction. They do not require polished screens, comprehensive data, or production view
models. Map remains a non-functional layout fixture only.

---

## 10. Expected files and Unity Editor work

This is a proposal inventory, not authorization to create every item.

### Existing code likely modified in Package A

- `Assets/_Project/Scripts/Hero/HeroController.cs` — thin forwarding seam to its input reader.
- `Assets/_Project/Scripts/Hero/Input/HeroInputReader.cs` — suspend sampling, clear transient
  buffers, track held commands, fresh-press rearm, and gate/remove legacy fallbacks.
- `Assets/_Project/Input/InputSystem_Actions.inputactions` — approved open actions and tab actions.

`PlayerAbilityState.cs` is not expected to change for the first Gear slice.
`GameManager.cs` is not a Package A low-level input change: it remains responsible for `GameState`,
Pause/Unpause, time scale, its existing control lock, and existing transition/respawn/recovery
coordination. It does not learn about action maps, buffers, UI Cancel, held controls, rearming,
fallback reads, or EventSystem focus. A narrow transition-completed notification would be reviewed
separately only if observing the existing `IsSceneTransitioning` falling edge proves insufficient.

### Proposed Package A scripts

- `UIFlowController`
- `PauseMenuScreen`
- `ConfirmationModal`
- `GameplayMenuScreen`
- `GameplayMenuTabId` / serialized tab registration
- `UIFocusController` or small focus/selection utility
- `UIDeviceModeTracker`
- `ControlHintResolver`
- `GearScreen`
- `GearEntryView`
- `GearDetailsPanel`
- `GearDisplayDefinition`
- `GearDisplayCatalog`
- `UISandboxController` and isolated fixture helpers
- `UIProjectValidator`

Names are proposals. Combine tiny helpers where that keeps the implementation focused.

### Proposed ScriptableObjects and assets

- `GearDisplayDefinition` assets only for approved production Gear.
- One `GearDisplayCatalog.asset`.
- Optional layout-specific presentation profile only if the selected Sandbox prototype needs one;
  layout data remains separate from Gear definitions.
- Placeholder art may live in a clearly identified UI development folder and must not be presented
  as final art.

### Proposed prefabs

- Persistent `MenuRoot` or nested menu composition prefab.
- Future/deferred sibling `NotificationRoot` composition; Package A creates no runtime notification
  queue or event ownership.
- Pause Menu screen.
- Confirmation modal.
- Gameplay Menu shell.
- Gear entry/details/shared controls.
- Focusable button/tab/prompt visual prefabs.
- Sandbox-only fixture panels.

### Proposed development scene

- `Assets/_Project/Scenes/Development/UISandbox.unity`, excluded from Build Settings.

### Input and EventSystem authoring

- Add approved Pause and Gameplay Menu actions, with the System-map option reviewed first.
- Reserve Tab for the future Map action; do not bind Package A Gameplay Menu to Tab.
- Leave architectural room for distinct Pause, Gameplay Menu, and Map actions, but do not add Map
  or its hold/double-tap interaction in Package A without separate approval.
- Add approved UI previous/next-tab actions.
- Configure one persistent project-owned EventSystem and `InputSystemUIInputModule`.
- Assign existing UI Navigate/Submit/Cancel/Pointer/Click/Scroll actions.
- Verify no gameplay scene contains a competing EventSystem.
- Verify Player/UI/System map activation behavior with the Hero being destroyed/recreated across
  scene loads.

### Canvas and camera authoring

- Place MenuRoot under the persistent camera composition without modifying `PersistentHudRoot`.
- Target `HUDCamera` where using Screen Space - Camera.
- Author GraphicRaycasters only on interactive canvases.
- Verify actual current sorting values and establish semantic Fade > Modal > Root, with
  NotificationRoot as a sibling presentation layer above HUD according to the approved visibility
  policy.
- Confirm the HUD camera culling mask includes every new UI GameObject layer.
- Keep the existing FadeCanvas above menus.

### Inspector references

- Assign UIFlow screens, EventSystem/module, action asset/maps/actions, default selections, and
  modal parents.
- Author the initial `1.0f` unscaled post-transition menu lockout on `UIFlowController`.
- Verify scene-lifecycle Hero invalidation/rebinding and that no destroyed Hero reference persists.
- Assign Gear state/catalog references and layout bindings.
- Assign each tab registration and first selectable.
- Assign Sandbox fixtures to runtime clones only.

### Package B files/assets

- Existing `AudioManager.cs`, `_AudioManager.prefab`, and `Hero.prefab` source routing.
- Proposed AudioMixer and Master/Music/SFX groups.
- Proposed profile-independent audio settings data/store/application service.
- Proposed Options screen and volume controls.
- UI navigation/confirm/cancel clips.

### Tests and validators

- New EditMode UI flow, Gear catalog, Gear refresh, and selection tests.
- New PlayMode pause/time-scale/input leakage/EventSystem tests.
- Validator coverage for persistent roots, EventSystem, Input Actions, canvases/cameras, tab
  registrations, catalog duplicates/missing definitions, Sandbox build exclusion, and production
  asset isolation.

### Documentation after implementation approval

- Update `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, and the relevant FeatureSpecs only
  after a phase is implemented and verified.
- Correct the independently confirmed stale statements listed in this audit.
- Record manual validation honestly; do not mark Unity checks complete unless performed.

---

## 11. Milestone breakdown

### Phase 1 — UI Sandbox and shared presentation prefabs

- **Goal:** Establish an isolated UI/UX workspace and reusable visual primitives.
- **Included:** Development scene, isolated fixture states, shared buttons/tabs/panels, safe-area and
  aspect guides, HUD/boss/Gear/menu preview states, lightweight future-tab fixtures.
- **Excluded:** Save files, production item systems, functional Map, final art, gameplay input
  switching.
- **Existing files affected:** None required beyond optional shared prefab nesting.
- **Proposed files:** Sandbox scene/controller/fixtures and shared UI prefabs.
- **Editor work:** Scene camera/canvas, EventSystem for direct preview, prefab nesting, build
  exclusion, resolution presets.
- **Automated tests:** Fixture isolation and Build Settings exclusion validator.
- **Manual validation:** Common aspect ratios, safe area, keyboard/controller/mouse focus.
- **Risks:** Accidentally referencing/mutating production ScriptableObjects; Sandbox divergence from
  production prefabs.
- **Documentation after completion:** Record Sandbox workflow in the future UI FeatureSpec.
- **Review gate:** Approve visual language and Gear prototype candidates before production layout.

### Phase 2 — Input and menu-flow foundation

- **Goal:** Establish one safe pausing-root coordinator with no resume leakage.
- **Included:** UIFlow, approved open-action design, UI map activation, Player suppression,
  full-transition blocking, 1.0-second unscaled post-transition lockout, request rejection without
  queueing, independent Pause/Gameplay Menu release rearming, input-reader suspension/clear,
  held-command detection, fresh-press rearming, overlap release gating, EventSystem, focus memory,
  and repeated-request guards.
- **Excluded:** Full Pause visuals, Gear data, Options, frontend, contextual interfaces.
- **Existing files affected:** Input Actions, `HeroInputReader`, and the thin `HeroController` seam.
  `GameManager` receives no low-level input responsibility.
- **Proposed files:** UIFlow and focus/device utilities; persistent MenuRoot prefab.
- **Editor work:** Action bindings, module references, camera/canvas authoring, duplicate
  EventSystem audit, and lockout tuning.
- **Automated tests:** Full transition/grace blocking, no queueing, independent per-root-action
  release/new-press rearming, pause ownership, time scale, buffer clear, command rearm, overlap
  release gate, map activation, repeated toggles, Hero rebinding, and first selection.
- **Manual validation:** Hold Pause, Gameplay Menu, East/Cancel, Jump, Attack, Dash, Bind, Interact,
  Sprint, movement, and any additional command through blocked periods and close. Commands wait for
  release/fresh press; movement may resume immediately.
- **Risks:** Legacy fallback bypass, synthetic presses on map enable, action-map state after Hero
  recreation, stale Hero references, and mistaking `SceneInit` for completion.
- **Documentation after completion:** UI/input boundaries and verified bindings.
- **Review gate:** No screen work proceeds until transition lockout, no-queue, leakage, fresh-press,
  rebinding, and ownership tests pass.

### Phase 3 — Root Pause menu flow

- **Goal:** Deliver the root Pause hierarchy and modal behavior.
- **Included:** Continue, Options route contract, Quit confirmation, cancel/back hierarchy,
  selection restoration, internal flow-request-only Quit behavior.
- **Excluded:** Functional Options, frontend transition, New Game, Continue frontend action, slots,
  save-on-return policy, arbitrary external scene transitions, and generic paused-transition
  interruption.
- **Existing files affected:** Persistent menu prefab composition.
- **Proposed files:** Pause screen and confirmation modal.
- **Editor work:** Layout, navigation, modal raycast blocking, initial/return selections.
- **Automated tests:** Continue resumes once, modal Back returns to Pause, root Back resumes,
  duplicate requests, request-only Quit, and scene-flow requests rejected while UI owns pause.
- **Manual validation:** Controller-first flow, mouse clicks, keyboard Escape, rapid toggling.
- **Risks:** Shipping non-functional Options/Quit endpoints. Package A is an internal vertical
  slice until Package B and frontend scope decisions resolve production exposure.
- **Documentation after completion:** Pause lifecycle and known deferred destinations.
- **Review gate:** Approve Pause UX and modal hierarchy.

### Phase 4 — Gear vertical slice and Gameplay Menu shell

- **Goal:** Deliver production-backed Gear inside a future-compatible shell.
- **Included:** Tab filtering, session-only last tab, Gear default, display catalog, explicit open
  refresh, AbilityChanged refresh, acquired-only presentation, details, control hints, empty state,
  layout selected after Sandbox review, and exactly one required `AbilityId` per Package A
  production definition.
- **Excluded:** Ability mutations, acquisition toasts, general inventory, Satchel, recipes,
  crafting, fake production tabs, Map.
- **Existing files affected:** None expected in gameplay ownership; `PlayerAbilityState` remains
  read-only to UI.
- **Proposed files:** Shell, tab registration, Gear definitions/catalog/views and validator.
- **Editor work:** Approved definitions, physical item copy/art references, layout authoring,
  navigation, tab availability.
- **Automated tests:** Visibility, changes, explicit reopen refresh, duplicate/missing definitions,
  selection retention, last-tab fallback, hidden unavailable tabs.
- **Manual validation:** Starting/empty/multi-item states across input devices and aspect ratios.
- **Risks:** Prematurely exposing enum members, using raw ability names, layout revealing unknown
  capacity, treating serialized asset values as defaults, or adding speculative ownership sources.
- **Documentation after completion:** Gear ownership/presentation contract and approved layout.
- **Review gate:** Product/art approval for starting Gear and Double Jump identity.

### Phase 5 — Audio settings foundation and functional Options

- **Goal:** Add honest, persistent Master/Music/SFX controls through an audio-owned foundation.
- **Included:** AudioMixer/groups/parameters, source routing, dB conversion, settings storage,
  startup application, Options sliders, UI audio.
- **Excluded:** Display mode, resolution, key rebinding, accessibility suite, music-selection
  architecture.
- **Existing files affected:** `AudioManager`, `_AudioManager.prefab`, Hero audio-source routing.
- **Proposed files:** Mixer, settings data/store/service, Options view components, UI clips.
- **Editor work:** Expose parameters, assign groups to global/hero sources, slider defaults,
  navigation, clip authoring.
- **Automated tests:** Conversion/clamping, persistence round trip, startup application, mute,
  slider-to-service calls.
- **Manual validation:** Every hero/global/UI/music path responds to the correct bus without
  double attenuation.
- **Risks:** Missing a local Hero source, mixer naming drift, settings applied after audio begins.
- **Documentation after completion:** Audio settings ownership and routing.
- **Review gate:** Audio review independent of Package A.

### Phase 6 — HUD artwork and feedback

- **Goal:** Replace placeholder presentation without changing HUD state ownership.
- **Included:** Health/resource/boss artwork, local neutral-versus-gameplay feedback, layout polish.
- **Excluded:** Resource pips, new gameplay resources, ordinary enemy bars, state mutation.
- **Existing files affected:** `_GameCameras.prefab`, `HealthSlotView.prefab`, HUD presentation
  assets.
- **Proposed files:** Artwork/animation presentation assets only as approved.
- **Editor work:** Sprite/material/animation setup and aspect checks.
- **Automated tests:** Existing behavior plus no-feedback-on-state-application regressions.
- **Manual validation:** Damage/heal/bonus/resource/death/boss lifecycle.
- **Risks:** Feedback accidentally becoming gameplay orchestration or using scaled time.
- **Documentation after completion:** Final HUD presentation behavior.
- **Review gate:** Art/feel approval.

### Phase 7 — Future production-backed tabs

- **Goal:** Add tabs only when authoritative gameplay systems exist.
- **Included:** One independently approved tab at a time, its real reader/commands, honest empty
  state, shell registration.
- **Excluded:** Generic inventory framework and speculative persistence schemas.
- **Existing files affected:** Gameplay Menu registration and the owning system’s approved API.
- **Proposed files:** Tab-specific screens/adapters.
- **Editor work:** Per-tab layout and navigation.
- **Automated tests:** Ownership mapping, availability, empty/populated state.
- **Manual validation:** System-specific flows.
- **Risks:** UI inventing stack limits, rarity, crafting, tasks, or journal ownership.
- **Documentation after completion:** New owning FeatureSpec plus UI boundary.
- **Review gate:** Gameplay-system contract approval before UI production work.

### Phase 8 — Dual-access Map

- **Goal:** Add one Underbrew map model/presentation with non-pausing Local Quick Map and pausing
  Full Map access after identity/discovery design is approved.
- **Included:** Dedicated Map specification; stable authored world/room identity; discovery;
  shared authored map visual/model; hold-to-view Quick Map; approved limited gameplay input while
  quick view is held; double-tap Full Map shortcut; Gameplay Menu Map-tab insertion; transition
  close/held restoration; destination context readiness; and no-queue behavior.
- **Excluded:** Reference-game scene-name identity, Package A functional work, speculative map
  ownership, and command permissions beyond the approved Quick Map list.
- **Existing files affected:** Future world graph/persistence APIs and the approved input/menu seams
  only after dedicated design.
- **Proposed files:** `MapController`, `QuickMapOverlay`, Map tab/presentation, data/assets, and input
  recognition are conceptual names determined by the dedicated proposal.
- **Editor work:** Stable room/zone authoring, shared local/full visual, Map tab, local framing,
  input binding, transition/destination fixtures, and validation.
- **Automated tests:** Identity, discovery, save/load, bounds/navigation, hold/release, double-tap
  routing, input restrictions, transition close, held restoration, and Full Map root restrictions.
- **Manual validation:** Exploration, falling/moving under Quick Map, command suppression, gesture
  timing, transition restoration, controller/keyboard behavior, and supported aspect ratios.
- **Risks:** Coupling identity to `.unity` scene names, duplicating `WorldStateRegistry` facts,
  confusing continuous held restoration with queued input, stale-room map rendering, or gesture
  arbitration causing unintended Full Map opens.
- **Documentation after completion:** Dedicated Map FeatureSpec and implementation proposal.
- **Review gate:** Separate map architecture, allowed-input, gesture, content-readiness, and UX
  approval.

### Phase 9 — Main menu and save slots

- **Goal:** Add a real frontend and resolve Quit destination.
- **Included:** Separately approved Boot routing, New Game/Continue policy, slots, `SaveStats`,
  return-to-main-menu save policy.
- **Excluded:** Incidental implementation during Package A.
- **Existing files affected:** `Bootstrap`, `SaveManager` only through approved existing APIs or
  narrowly reviewed additions.
- **Proposed files:** Frontend scene/screens and flow adapter.
- **Editor work:** Build Settings, scene canvas, persistent-camera binding, navigation.
- **Automated tests:** Boot routes, slot inspection/application separation, return policy.
- **Manual validation:** Fresh/corrupt/existing saves and cross-scene checkpoint continuation.
- **Risks:** Mutating runtime state while previewing slots, saving from unauthorized UI paths,
  paused transition leaving time scale zero.
- **Documentation after completion:** Boot and save flow updates.
- **Review gate:** Save/product approval before implementation.

### Phase 10 — Accessibility and polish

- **Goal:** Apply approved accessibility, localization, glyph, motion, and resolution standards.
- **Included:** Approved remapping, text scaling, contrast, reduced motion, hold/toggle alternatives,
  localization, final glyph library, supported display targets.
- **Excluded:** Assumptions made before requirements are approved.
- **Existing files affected:** Shared UI presentation, settings foundation, Input Actions as
  approved.
- **Proposed files:** Accessibility/settings assets and tests.
- **Editor work:** Full matrix authoring.
- **Automated tests:** Navigation reachability, settings persistence, layout overflow.
- **Manual validation:** Accessibility and platform compliance passes.
- **Risks:** Retrofitting layouts after art lock.
- **Documentation after completion:** Supported accessibility/platform matrix.
- **Review gate:** Product/platform approval.

---

## 12. Tests and validation proposal

No tests listed here were run during this planning task.

### EditMode

- Duplicate `PersistentHudRoot`, `GameCameras`, MenuRoot, and EventSystem detection.
- Repeated view configuration does not duplicate subscriptions.
- UIFlow open/close and root exclusivity.
- UI-owned pause is acquired/released exactly once.
- An external scene-flow request is not accepted while UIFlow owns an open pausing root.
- Back hierarchy: modal, child, tab root, root close.
- First-selection fallback and invalid remembered-selection recovery.
- Pause and Gameplay Menu requests reject independently during transition and grace states.
- Rejected requests create no pending/queued state and do not change selection.
- Transition completion begins a `1.0f` unscaled lockout.
- Completing the lockout does not arm either root action while that action remains held.
- Pause and Gameplay Menu track armed/disarmed state independently.
- Releasing Pause rearms only Pause; releasing Gameplay Menu rearms only Gameplay Menu.
- A fresh press of one armed root action produces its open request even if the other root action
  remains held, provided every other open predicate passes.
- Resume-disarmed command tracking is per command; releasing one does not arm another still-held
  command.
- Continuous movement is not part of command-release rearming.
- Tab availability filtering and navigation-link rebuilding.
- First Gameplay Menu open selects Gear.
- Later opens remember the last still-available tab for the session.
- Gear explicit open/initialize refresh.
- Gear reacts to genuine `AbilityChanged`.
- Gear never emits acquisition feedback from ownership changes.
- Locked Gear definitions produce no visible placeholder.
- Missing unlocked definition omits safely and reports once.
- Duplicate Gear key and duplicate `AbilityId` validation.
- Starting/empty state does not reveal unknown counts.
- Save-state application followed by open shows the current snapshot.
- Runtime unlock while open refreshes selection/details safely.
- Sandbox fixtures do not reference production state assets.

### PlayMode

- `GameManager.Pause()` and `Unpause()` remain the only time-scale writers in UI flow.
- Time scale reaches zero on open and returns once on valid close.
- Player map disabled and UI map enabled while a root is open.
- Pause and Gameplay Menu are both rejected during scene exit, fade-out, load, destination
  `SceneInit`, placement, camera readiness, reveal/fade, entry motion, remaining transition-owned
  control lock, and the full post-transition grace period.
- Availability begins only after real transition completion plus the unscaled lockout.
- A press during transition or grace is discarded with no pending request.
- Holding Pause or Gameplay Menu through transition and grace does not open either root.
- Releasing and freshly pressing Pause opens Pause even if Gameplay Menu remains held; releasing
  and freshly pressing Gameplay Menu opens Gameplay Menu even if Pause remains held.
- Releasing either root action rearms only that action.
- Repeated blocked presses do not alter root state, modal state, or selection.
- Attack/jump buffers clear even when created at time scale zero.
- Holding Gamepad East/Cancel through close delays gameplay reactivation until release.
- Holding Jump, Attack, Dash, Bind, Crouch, Interact, Sprint, or any approved command through close
  starts no command until that specific control is released and freshly pressed.
- Movement keys/stick may resume continuous movement immediately after control restoration.
- Re-enabling Player actions does not synthesize a command press or create a new buffer.
- Legacy keyboard/mouse attack, dash, and sprint fallbacks honor suspension and fresh-press rearm.
- The same held Pause/Menu press cannot reopen or immediately close a root.
- Rapid/repeated Pause and Gameplay Menu inputs do not double-toggle.
- Controller/keyboard selection starts on a valid control.
- Mouse click and controller navigation can alternate without losing focus.
- Device prompt changes do not trigger menu actions.
- Death, respawn, hazard recovery, and non-interruptible boss-presentation requests reject with no
  queued open.
- Controller disconnect never resumes or closes the menu.
- Focus loss never auto-resumes; any future auto-pause policy is tested only after approval.

### Ownership and lifecycle assertions

- `UIFlowController` never writes `Time.timeScale`.
- `GameManager` remains the UI path's only time-scale authority and contains no action-map,
  input-buffer, held-control, resume-disarm, fallback, UI Cancel, or EventSystem logic.
- UI requests low-level suspension/resume only through the thin `HeroController` boundary and does
  not access `HeroInputReader` directly.
- Scene initialization invalidates any previous Hero binding.
- Deferred or just-in-time binding uses the current cached Hero and never retains a destroyed
  scene-Hero reference.
- `SceneInit` alone never starts the post-transition lockout.

### Future dual Map tests

These belong to the dedicated Map phase, not Package A:

- Holding Map opens the non-pausing Local Quick Map without calling `GameManager.Pause()`.
- Releasing Map closes Local Quick Map.
- Horizontal movement remains available and normal gravity/falling continues.
- Jump, Attack, Dash, Bind, Crouch, Interact, Sprint activation, and every other unapproved command
  do not execute while Quick Map is open.
- Double-tapping Map closes Quick Map and opens the pausing Gameplay Menu directly on its Map tab.
- Ordinary Gameplay Menu navigation reaches the same Full Map.
- Quick Map closes immediately when transition begins.
- The previous room's Quick Map stays hidden through exit, fade, load, `SceneInit`, destination
  reveal, and scripted entry.
- A Quick Map that is open because Map is held at transition start records restoration eligibility
  before closing.
- Beginning a transition while Quick Map is not open records no restoration eligibility.
- Releasing the original Map hold during transition clears restoration eligibility permanently for
  that transition.
- A new Map press or hold begun during transition displays nothing and cannot create restoration
  eligibility.
- Only an uninterrupted original hold restores the destination Local Quick Map, and only after
  transition completion and destination map-context readiness while Map remains physically held.
- Releasing and then holding Map again during transition does not restore Quick Map automatically.
- Held restoration does not require a new press and never opens Full Map.
- The root-menu one-second grace does not delay Quick Map restoration by default.
- Taps and double-taps during transition are discarded, retain no tap history, create no pending
  Map request, and never queue a later Full Map.
- Full Map continues to obey root transition blocking, post-transition lockout, release gating, and
  fresh-press/gesture rules.
- `QuickMapOverlay` never manipulates Hero actions, action maps, buffers, or `HeroInputReader`;
  limited input is requested through `HeroController` and enforced by the Hero input layer.

### Editor validator

- Exactly one persistent HUD/Menu/EventSystem composition in enabled production scenes/prefabs.
- UI Sandbox excluded from Build Settings.
- Required action maps/actions/bindings and module references exist.
- No duplicate EventSystem in gameplay scenes.
- HUD/menu/modal/fade semantic sorting and camera assignments are valid.
- Interactive canvases have raycasters; non-interactive presentation does not block raycasts.
- Tab IDs are unique and Gear is available in Package A production configuration.
- Fixture-only tabs cannot be enabled in production configuration.
- Gear catalog keys/mappings are unique and approved production coverage is present.
- Production UI does not reference Sandbox fixture assets.
- Future Map validation confirms stable authored room IDs, one shared map model/presentation source,
  Quick Map outside pausing roots, and Full Map registered as the Gameplay Menu Map tab.

### Manual matrix

- Keyboard, common gamepad, and mouse.
- 16:9, 16:10, 21:9, and 4:3 during development; replace with approved targets later.
- Repeated open/close at grounded, airborne, moving, attacking, and near interactables.
- Attempt both roots throughout exit/load/`SceneInit`/reveal/entry/grace. Verify blocked presses are
  discarded and held menu controls do not open. Hold both root controls, release and freshly press
  Pause while Gameplay Menu stays held, then perform the inverse; verify each action rearms
  independently and opens only its own root.
- Hold Jump, Attack, Dash, Bind, Crouch, Interact, Sprint, East/Cancel, Pause, and Gameplay Menu
  individually through closure; verify fresh-press behavior. Hold movement and verify it resumes
  without a neutral-stick/key requirement.
- Open/close before and after room transitions, death/respawn, hazards, and boss introduction.
- Modal selection restoration and all available Gear counts.
- Unity Editor domain reload, direct Sandbox entry, and Boot-driven gameplay entry.

Do not claim Unity compilation, EditMode, PlayMode, visual, or Editor validation passed until each
was actually run.

---

## 13. Open decisions

Confirmed and no longer open: neither root can open during the full transition lifecycle;
`SceneInit` is too early; completion starts an initial 1.0-second unscaled lockout; requests made
during transition/lockout are discarded; a held control cannot open a root; release and a new press
are required.

Also confirmed: hold Map is a non-pausing Local Quick Map; double-tap Map routes to the pausing Full
Map tab; Tab is the provisional keyboard Map control; Quick Map closes for transition and may
restore after completion/context readiness only when it was open at transition start and its
original hold remained uninterrupted; transition-time holds do not become eligible. Full Map never
queues and keeps all root restrictions.

1. Whether and when Quit to Main Menu receives a functional destination.
2. Save/discard/reload policy when returning to a future main menu.
3. Exact repository implementation seam used to observe transition completion; the existing
   successful `IsSceneTransitioning` falling edge while state is Playing is the narrowest current
   candidate.
4. Whether the initial 1.0-second value changes after Underbrew playtesting.
5. Whether starting Dash/WallCling appear as physical Gear possessions.
6. Final Gear presentation: dynamic authored groups, authored collection, or list fallback.
7. Underbrew names, artwork, categories, and flavor text, especially Double Jump’s physical item.
8. Exact Pause, Gameplay Menu (provisionally I on keyboard), previous-tab, and next-tab bindings;
   Tab remains reserved for Map.
9. Whether the always-enabled System map is approved.
10. Whether legacy direct Hero input fallbacks are removed or retained behind suspension/rearming.
11. Exact boss-introduction menu restriction integration.
12. Whether active boss combat is pausable outside non-interruptible presentation.
13. Focus-loss auto-pause policy per platform; auto-resume is prohibited.
14. Final input-glyph and binding-override solution.
15. Final UI artwork, typography, animation, and motion policy.
16. Accessibility requirements.
17. Supported resolution, aspect-ratio, safe-area, and window-mode targets.
18. Whether notifications remain visible while a root menu is open.
19. Final controller Map binding.
20. Final Quick Map allowed-input list; the first recommendation is horizontal movement only.
21. Map hold/double-tap arbitration and double-tap timing.
22. Exact destination map-context readiness seam.
23. Whether Quick Map restoration timing changes after Underbrew playtesting.

---

## 14. Recommended first implementation packages

### Package A — Internal UI foundation vertical slice

Include:

- UI Sandbox foundation.
- Shared visual prefabs.
- Persistent MenuRoot composition and EventSystem/Input System UI module.
- Menu/input-flow foundation.
- Full-transition and one-second unscaled post-transition menu blocking.
- Immediate request rejection with no queued root opens.
- Independent Pause and Gameplay Menu release rearming after availability blocks.
- Gameplay-input suspension, buffer clearing, held-command detection, fresh-press rearming,
  fallback gating/removal, and overlap release gating.
- Focus, selection-memory, device-mode, and control-hint utilities.
- Root Pause menu.
- Quit confirmation and flow request without assuming a frontend destination.
- Gameplay Menu shell.
- Production-backed Gear vertical slice using the layout approved from Sandbox prototypes.
- Production tab availability filtering with only Gear exposed.
- EditMode/PlayMode tests and Editor validators.

Package A is internally testable and useful for ongoing UI development. It is not the complete
shippable player-facing Pause experience: production Options is not functional until Package B,
Quit to Main Menu has no approved destination, and some presentation remains placeholder.

Package A explicitly defers:

- Functional Quit transition.
- Frontend scene, New Game, Continue, save slots, and Boot rerouting.
- Functional Options settings.
- Production Tools, Satchel, Recipes, Tasks, Journal, and Map.
- Map Input Action, hold/double-tap recognition, Quick Map overlay, Full Map route, and map data.
- Acquisition gameplay/notifications.
- Generic inventory, recipe, crafting, task, journal, or map ownership.

#### Package A1 — Sandbox, input, and Pause foundation

Include:

- UI Sandbox and shared visual prefabs.
- Persistent menu composition.
- EventSystem and Input System UI module.
- Approved Pause/Gameplay Menu action-map strategy.
- `UIFlowController`.
- Full transition and post-transition availability handling.
- Initial 1.0-second unscaled lockout.
- Request rejection with no queueing.
- Independent Pause and Gameplay Menu armed/disarmed state and release rearming.
- Hero input suspension, buffer clearing, resume disarming, and fresh-press rearming.
- Focus/selection restoration.
- Root Pause menu and confirmation modal.
- Leakage, pause, transition-lockout, lifecycle-binding, and navigation tests.

Review gate:

- Neither root opens during transition or post-transition grace.
- Blocked presses never queue.
- Holding a menu control through grace does not open a root.
- Releasing and freshly pressing either root action opens its root even if the other root action
  remains held, provided all other availability conditions pass.
- No gameplay command fires immediately on menu close, while movement can resume continuously.

#### Package A2 — Gameplay Menu and Gear

Include:

- Gameplay Menu shell.
- Production tab availability filtering.
- Current-session last-tab memory.
- Gear catalog and Package A definitions, each with one required `AbilityId`.
- Gear presentation selected after Sandbox review.
- Gear details and control hints.
- Gear tests and validator expansion.

This A1/A2 sequence reduces stop/start overhead while retaining meaningful review gates.

### Package B — First complete player-facing settings integration

Include:

- Audio settings foundation.
- Project AudioMixer, exposed parameters, source routing, decibel conversion, persistence, and
  startup application.
- Functional Master/Music/SFX Options subset.
- UI audio integration through `AudioManager`.
- A decision on whether to approve return-to-main-menu integration or continue deferring it.

Package B remains distinct because it changes audio routing and persistent settings behavior beyond
the UI view layer and retains its own implementation/audio review gate.

Package A is the internal UI foundation vertical slice. Package A plus Package B complete the first
full player-facing Pause/settings milestone, except for Quit to Main Menu if its destination remains
deferred. Package A alone must not be described as shippable.

This sequence establishes a useful, production-data-backed UI slice without inventing unrelated
gameplay systems or replacing Underbrew’s existing HUD, state, save, pause, scene, or audio
ownership.
