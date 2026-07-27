# Feature Spec — Pause and Menu Flow

**Last reviewed:** 2026-07-27  
**Status:** Authoritative contract. Package A1 (root Pause menu, dedicated Pause/GameplayMenu input,
focus flow, input suspension/rearming) is implemented and covered by automated EditMode/PlayMode
tests. Interactive manual validation has not been performed. Package A2 (Gameplay Menu tabs, Gear)
remains not implemented.

## Purpose

Define the two pausing root interfaces, their Back/modal hierarchy, pause ownership, input
suspension/resume, focus, and availability across scene and gameplay lifecycles.

## Current state

Implemented (Package A1):

- `GameManager.Pause()` / `Unpause()` own `GameState`, the Hero control lock, and
  `Time.timeScale`. `UIFlowController` is the only caller for UI-owned pauses.
- The Input Actions asset contains `Player`, `UI`, and `System` maps. `System` holds `Pause`
  (Keyboard Escape, Gamepad Start) and `GameplayMenu` (Keyboard I; controller binding deferred).
  `UI` contains navigation, submit, cancel, pointer, click, and scroll primitives.
- Root Pause screen (`PauseMenuScreen`: Continue, gated Options, Quit confirmation modal).
- Dedicated Pause and Gameplay Menu actions, owned solely by `UIFlowController`.
- Project-owned production `EventSystem` and `InputSystemUIInputModule` under `MenuRoot`.
- Persistent `MenuRoot` and `UIFlowController`.
- Gameplay-input suspension, buffer clearing, held-command tracking, and fresh-press resume
  (`HeroInputReader.SuspendGameplayInput`/`ClearTransientInput`/`BeginResumeGameplayInput`, forwarded
  through a thin `HeroController` facade).
- Root focus/selection and modal hierarchy; EditMode/PlayMode tests; `UIFoundationValidator`.

Missing/planned (Package A2+):

- Gameplay Menu tabs and Gear (no `GameplayMenuScreen` exists; `UIFlowController` safely rejects
  Gameplay Menu requests since none is registered).
- Device-prompt/glyph switching.
- Interactive manual validation (see the Package A1 completion report for what specifically has
  not been performed).

## Root Pause menu

The root Pause menu is separate from the tabbed Gameplay Menu. It contains:

1. Continue.
2. Options.
3. Quit to Main Menu.

Behavior:

- Continue closes the root and resumes through the approved close/input flow.
- Options opens as a child flow. Back/close returns to the Pause menu and restores the invoking
  selection.
- Quit to Main Menu opens a confirmation modal.
- Back at the Pause root performs the same approved resume flow as Continue.
- Pressing the dedicated Pause action while the Pause root is stably open toggles it closed, subject
  to close guards and release handling.
- Repeated Pause callbacks during opening/closing do nothing.
- A Gameplay Menu request while Pause or its modal is active is rejected.

The Package A Options route may be exercised internally, but functional audio Options belongs to
Package B. Quit destination and save policy remain deferred because no frontend scene exists.

## Gameplay Menu

The Gameplay Menu uses its own dedicated action. Planned final tabs are:

- Gear.
- Tools.
- Satchel.
- Recipes.
- Tasks.
- Journal.
- Map.

Package A2 exposes only production-backed Gear. Tabs with no authoritative gameplay owner are
hidden in production rather than disabled or populated with fake data. Sandbox fixtures may preview
their shell layout.

The first Gameplay Menu open in a play session defaults to Gear. Later opens remember the last
valid viewed tab for that session only; this is never saved. If the remembered tab is unavailable,
fall back to Gear.

Back from the active tab root closes the Gameplay Menu. Back from a modal or child closes that layer
first. Pressing the Gameplay Menu action while its root is stably open toggles it closed, subject to
the same close guards and release handling. A Pause request while Gameplay Menu is active is
rejected.

Future Full Map access closes Local Quick Map and routes into this same Gameplay Menu directly on
the Map tab. It is not a separate pausing root.

## Pause ownership

- `GameManager.Pause()` and `GameManager.Unpause()` are the only approved pause/time-scale seams.
- UI never writes `Time.timeScale`.
- One proposed `UIFlowController` owns at most one UI pausing root and therefore one UI pause
  request.
- It calls Unpause only when it owns that paused root.
- Package A does not add a generic pause-token framework.
- Repeated callbacks must not duplicate Pause or Unpause.
- Arbitrary external transitions are not accepted while UI owns an open pausing root.

## Input ownership

### Recommended action-map direction

The approved proposal recommends reviewing a small always-enabled `System` map containing Pause and
Gameplay Menu first. It can keep open/close controls independent from the scene Hero and Player/UI
map switching. The final action-map design remains open until implementation review.

Responsibilities:

| Map/layer | Responsibility |
|---|---|
| Player | Gameplay movement and commands |
| UI | Navigate, Submit, Cancel, pointer, click, scroll, and planned tab navigation |
| Proposed System | Persistent root-open actions; future Map space if separately approved |
| `HeroInputReader` | Sampling, direct fallback gating/removal, transient buffers, held-command state, rearming |
| Proposed `UIFlowController` | Root availability and action-map mode; no gameplay input interpretation |

Provisional bindings:

- Pause: Escape; controller Start/Menu.
- Gameplay Menu: keyboard I; controller binding open.
- Future Map: keyboard Tab provisionally reserved for hold Quick Map/double-tap Full Map.
- Previous/next tab: approved UI-map actions; exact bindings open.

Tab must not be used as the Gameplay Menu binding. Existing gameplay `Previous`/`Next` actions are
not reused for menu tabs because their D-pad bindings conflict with ordinary navigation.

### EventSystem and focus

Package A1 plans one persistent project-owned EventSystem with `InputSystemUIInputModule`. Each
root, child, tab, modal, and empty state authors a valid first selection. A modal restores its
invoker where possible. Pointer clicks may move selection, and later keyboard/controller navigation
continues from a valid selected control.

### Suspension and resume

```text
UIFlowController
  ├── requests GameManager.Pause() / Unpause()
  └── requests input suspension/resume through HeroController
        └── HeroInputReader owns sampling, buffer clearing,
            fallback gating, held-command tracking, and rearming
```

On close:

- Clear existing command buffers and transient pressed/released/held snapshots.
- Re-enable actions only through the approved input flow.
- Record command controls still physically held.
- Keep each held command disarmed until that command is released.
- Require a later fresh press.

At minimum this applies to Jump, Attack, Dash, Bind, Crouch, Interact, Sprint activation where
applicable, and future one-shot commands. Continuous movement keys and analogue movement may resume
immediately; they do not require neutral release.

Buffer clearing removes old intent. Held-command rearming prevents a held physical control from
creating new intent after the clear. Both are required. UI Cancel/Gamepad East overlap with current
Bind/Crouch input requires explicit release handling. Direct keyboard/mouse fallbacks must honor
suspension and rearming or be removed after bindings are verified.

`GameManager` does not own action maps, buffers, held-button detection, resume disarming, UI Cancel
handling, or EventSystem focus. UI never accesses `HeroInputReader` directly. Persistent menu flow
must not retain a stale Hero reference across scene loads.

## Transition contract

Both pausing roots are unavailable throughout:

- Scene exit and exit-owned control lock.
- Fade-out.
- Loading.
- Destination scene initialization.
- Destination placement.
- Camera readiness.
- Fade/reveal.
- Scripted destination-entry movement.
- Remaining transition-owned control lock.
- Initial post-transition menu lockout.

`SceneInit` is not completion. It fires before the destination Hero cache, reveal, and scripted entry
work are complete.

When the full transition lifecycle has completed and `GameManager.State` has returned to Playing:

1. Start a configurable unscaled post-transition lockout.
2. Use 1.0 seconds as the initial value.
3. Reject Pause and Gameplay Menu for the entire lockout.
4. Keep Pause disarmed while Pause is held.
5. Keep Gameplay Menu disarmed while Gameplay Menu is held.
6. Releasing one action rearms only that action.
7. After the lockout, a later fresh press of an armed action may open its root even if the other root
   action remains held.

The current narrow candidate for observing completion is the successful falling edge of
`GameManager.IsSceneTransitioning` while `GameManager.State == Playing`. The exact implementation
seam remains open. The 1.0-second value is UI-flow tuning, not Hero, ability, camera, boss,
transition-point, or save data.

Every blocked request is discarded. Do not queue a Pause request, queue a Gameplay Menu request,
store a pending root, open automatically after the block, or convert a held input into a delayed
open.

## Availability restrictions

| Situation | Contract |
|---|---|
| Death | Reject while persistent health is depleted; never queue |
| Respawn/hazard recovery | Reject for the full owned lifecycle; never queue or alter its locks/fade |
| Non-interruptible boss presentation | Reject; exact integration seam remains open |
| Active boss combat | Pausability remains an open design decision |
| Another root open | Reject the other root; close modal/child/root in hierarchy |
| External scene transition while UI owns pause | Unsupported in Package A; do not claim arbitrary interruption is safe |
| Focus loss | Never auto-resume; optional eligible-state auto-pause remains open |
| Controller disconnect | Keep current state/time scale, preserve selection, allow fallback input, never auto-close/resume |
| Repeated callback | Ignore during opening/closing; never duplicate pause calls |

The initial open predicate also requires a live `GameManager`, Playing state, no transition, elapsed
post-transition lockout, requested action armed, no respawn/recovery, living persistent health, and
no root/modal transition already in progress. Failure records nothing for later.

## Quit flow

The confirmed Pause menu contains Quit to Main Menu with a confirmation modal, but no frontend
destination currently exists and existing save rules do not authorize saving from this view.

Future required sequence:

```text
confirm return
  -> resolve save/discard policy
  -> close modal/root
  -> release UI-owned pause
  -> finish input cleanup
  -> request transition through established scene flow
```

Functional destination, save/discard/reload behavior, and production exposure remain deferred.
Package A may validate a typed internal flow request only; it does not create a frontend or generic
paused-transition path.

## Quick Map relationship

Local Quick Map is future/deferred, non-pausing, and not a root. Full Map is a future direct route
to the Gameplay Menu Map tab and obeys every rule in this document. Map transition continuity and
stable-ID ownership are specified in `UIArchitecture.md`; Package A does not implement Map.

## Required Unity Editor work

Package A1 requires:

- Add approved Pause and Gameplay Menu actions/bindings; reserve Tab for future Map.
- Configure one persistent EventSystem and `InputSystemUIInputModule`.
- Assign UI Navigate/Submit/Cancel/pointer/click/scroll references.
- Build the persistent `MenuRoot` sibling composition and Canvas references.
- Author first selections and explicit navigation for roots, children, tabs, modal, and empty state.
- Ensure modal Canvas/raycast blocking prevents parent interaction.
- Assign the initial 1.0-second unscaled lockout value.
- Verify semantic Fade > Modal > Root > HUD layering against existing sorting values.
- Verify gameplay scenes contain no competing EventSystem.
- Validate scene-Hero invalidation/rebinding across every room load.

Package A1 Unity Editor work performed: `System` action map added; `MenuRoot`/`EventSystem`/
`InputSystemUIInputModule` authored and wired; `PauseMenuScreen.prefab`/`ConfirmationModal.prefab`
created and nested under `MenuRoot`; 1.0-second lockout value authored on `UIFlowController`;
semantic Fade(Overlay, always-on-top) > Modal(180) > Root(150) > HUD(100) sorting verified via
`UIFoundationValidator`.

## Automated tests and validators

Implemented and passing (EditMode `UIFlowControllerTests`, 14/14): Pause/Unpause occur exactly once
per accepted lifecycle; root exclusivity; repeated-request guards during opening; modal/root Back
order; valid first selection; unregistered-Gameplay-Menu safe rejection; rejection during scene
transition, respawn/recovery, and depleted health; the 1.0-second unscaled lockout arms on the
transition falling edge and blocks until elapsed; blocked inputs create no queued state.

Implemented but not confirmed executing via Unity MCP this session (PlayMode
`HeroInputSuspensionPlayModeTests` — see completion report for the specific tooling issue
encountered): buffers clear at time scale zero; every held command (Jump/Attack/Dash/Sprint/Bind)
remains disarmed until its own release/fresh press; releasing one command does not rearm another
still-held command; continuous movement resumes without neutral release; Gamepad East/Bind overlap
does not leak; enabling the Player map does not synthesize a press. Recommend running this suite via
the Editor's Test Runner window (PlayMode tab) to confirm.

Implemented and passing (`UIFoundationValidator`): one persistent EventSystem, no competing
gameplay-scene EventSystem, Canvas/raycaster semantics, modal/panel hidden-by-default raycast
safety, Sandbox build exclusion.

Not yet covered: `SceneInit`-alone-insufficient as an explicit regression test (the implementation
does not use `SceneInit` for availability at all, so this is structurally satisfied but has no
dedicated test); controller-disconnect and focus-loss auto-resume prevention (no explicit handling
exists yet — Package A1 simply never listens for these events, which already prevents any
auto-resume, but this is not independently tested).

## Manual validation

Not performed this session — no interactive Play Mode input control was available via Unity MCP.
Still required before Package A1 is considered fully validated: keyboard/controller/mouse; rapid
toggles; modal focus; holding Pause, Gameplay Menu, Cancel/East, every command, and movement through
blocked/close periods; every transition stage; death/respawn/hazard/boss states; controller
disconnect; focus loss; scene-Hero recreation; Boot and direct-Sandbox entry; and supported aspect
ratios/safe areas.

## Risks

- A scaled command buffer persists indefinitely while paused.
- Direct input fallback reads bypass disabled maps.
- UI Cancel overlaps a gameplay command.
- A held root action becomes a delayed open.
- `SceneInit` is mistaken for completion.
- Persistent UI retains a destroyed Hero.
- Repeated callbacks double-pause or double-unpause.
- An external transition begins while UI-owned pause remains active.
- A non-functional Options/Quit endpoint is exposed as shippable.

## Open decisions

- Final System-map approval and exact Pause/Gameplay Menu/tab bindings.
- Controller binding for Gameplay Menu; Tab remains reserved for Map.
- Exact transition-completion observation seam and whether 1.0 seconds changes after playtesting.
- Exact boss-presentation restriction integration and active-combat pause policy.
- Focus-loss auto-pause policy; auto-resume remains prohibited.
- Final device prompt, cursor, glyph, localization, and accessibility policy.
- Functional main-menu destination and save/discard/reload policy.
