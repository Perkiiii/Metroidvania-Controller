# Feature Spec — Pause and Menu Flow

**Last reviewed:** 2026-07-27  
**Status:** Authoritative contract. Package A1 (root Pause menu, dedicated Pause/GameplayMenu input,
focus flow, input suspension/rearming) is implemented and covered by automated EditMode and PlayMode
tests, both confirmed executing via the real Unity Test Runner (see Automated tests and validators
below). A correction pass fixed production `InputSystemUIInputModule` action wiring, modal-first
Pause-toggle behavior, production Quit/Options gating and dynamic navigation, confirmation-modal
selection fallback, pause-safe `UIFlowController` teardown, and a Dash/Jump/Attack/Interact/Bind
input-leakage defect in `HeroInputReader`'s resume-disarm logic. The Sandbox-only Options preview and
Quit callback status were live-verified in a real Play Mode session. Interactive manual validation of
the full production Boot path (real gameplay scene, keyboard/gamepad device input, real room
transitions) has not been performed — see Manual validation below for the exact remaining checks.
Package A2 (Gameplay Menu tabs, Gear) remains not implemented.

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
- Options is authored and present but non-interactable in production (Package B implements
  functional settings); the Sandbox may enable a preview seam that opens a labelled placeholder
  child, restoring selection to Options on close. See `UISandbox.md`.
- Quit to Main Menu is authored and present but non-interactable in production while its typed
  request seam is disabled — it never opens the confirmation modal in that state. The Sandbox may
  enable the seam to exercise the real shared confirmation modal end to end.
- Pressing the dedicated Pause action while the Quit confirmation modal is open closes only the
  modal (through the same path as Back/Cancel), keeping the root open, gameplay paused, and the Quit
  button selection restored; it does not run the Hero input-resume sequence or call
  `GameManager.Unpause()`. A later Pause press at the bare root closes normally.
- Back at the Pause root performs the same approved resume flow as Continue.
- Pressing the dedicated Pause action while the Pause root is stably open (no modal) toggles it
  closed, subject to close guards and release handling.
- Repeated Pause callbacks during opening/closing do nothing.
- A Gameplay Menu request while Pause or its modal is active is rejected.
- Runtime navigation (`PauseMenuScreen.RefreshAvailabilityAndNavigation`) always includes Continue
  and skips Options/Quit while gated, so keyboard/controller focus never lands on a disabled control;
  disabling a currently-selected gated control moves selection back to Continue.

Quit destination and save policy remain deferred because no frontend scene exists.

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
- The persistent `UIFlowController` owns at most one UI pausing root and therefore one UI pause
  request.
- It calls Unpause only when it owns that paused root.
- Package A does not add a generic pause-token framework.
- Repeated callbacks must not duplicate Pause or Unpause.
- Arbitrary external transitions are not accepted while UI owns an open pausing root.
  `GameManager.BeginSceneTransition` enforces the general state rule: while
  `GameManager.State == GameState.Paused`, it returns false without starting a coroutine, changing
  scene/UI state, or unpausing.

## Input ownership

### Same-frame Pause / Cancel arbitration

`UIFlowController.Update()` snapshots Pause, Gameplay Menu, and UI Cancel edges before changing any
root or action-map state, then arbitrates against the root active at frame start. With no root or
Pause active, Pause owns Escape for that frame and duplicate Cancel is suppressed. With Gameplay
Menu active, Pause remains rejected and Cancel continues through its Back hierarchy. Enabling the UI
map mid-frame is never treated as protection against an Escape resynchronisation edge.

### System action-map direction

A small always-enabled `System` map contains Pause and Gameplay Menu, kept independent from the
scene Hero and Player/UI map switching. `UIFlowController` is its sole persistent owner/enabler; the
scene Hero never touches it. Controller binding for Gameplay Menu and future Map space remain open.

Responsibilities:

| Map/layer | Responsibility |
|---|---|
| Player | Gameplay movement and commands |
| UI | Navigate, Submit, Cancel, pointer, click, scroll, and planned tab navigation |
| System | Persistent root-open actions (Pause, GameplayMenu); future Map space if separately approved |
| `HeroInputReader` | Sampling, direct fallback gating/removal, transient buffers, held-command state, rearming |
| `UIFlowController` | Root availability and action-map mode; no gameplay input interpretation |

Provisional bindings:

- Pause: Escape; controller Start/Menu.
- Gameplay Menu: keyboard I; controller binding open.
- Future Map: keyboard Tab provisionally reserved for hold Quick Map/double-tap Full Map.
- Previous/next tab: approved UI-map actions; exact bindings open.

Tab must not be used as the Gameplay Menu binding. Existing gameplay `Previous`/`Next` actions are
not reused for menu tabs because their D-pad bindings conflict with ordinary navigation.

### EventSystem and focus

Package A1 implements one persistent project-owned EventSystem with `InputSystemUIInputModule`,
whose Point/Move/Submit/Cancel/click/scroll/tracked-device action references are wired to durable
`UI`-map `InputActionReference` sub-assets (not a transient `InputActionReference.Create()`, which
does not survive serialization) — validated by `UIFoundationValidator`. Each
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
Bind/Crouch input requires explicit release handling. Direct keyboard/mouse fallbacks honor
suspension and rearming (Dash's Right Control fallback, previously only checked at the raw-keyboard
edge, now also gates the disarm snapshot).

`HeroInputReader`'s held-state check reads an action's bound controls' raw actuation directly
(`IsActuated()`) rather than `InputAction.IsPressed()`. Disabling then re-enabling an action while
its control is still physically held — exactly what suspend/resume does — does not resynchronize
`IsPressed()` within the same frame; it only reports true again after the Input System processes
another update, one frame too late for held-through-resume disarm gating. This was a confirmed,
previously-undetected leak affecting Jump (Space), Attack (Enter), Interact (E), and Bind (Gamepad
East), not only the Dash fallback — found and fixed via real PlayMode execution (see Automated tests
and validators). Continuous movement (`MoveVector`) has the same resync gap for `ReadValue<Vector2>()`
and falls back to a raw keyboard read only when the action reads exactly zero.

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

The implemented A1 observation seam is the successful falling edge of
`GameManager.IsSceneTransitioning` while `GameManager.State == Playing`, observed by
`UIFlowController.UpdateTransitionLockout()`. The 1.0-second value is UI-flow tuning, not Hero, ability, camera, boss,
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

Implemented and passing (EditMode `UIFlowControllerTests`, 18/18 — includes the correction pass's
modal-first-Pause and pause-safe-teardown coverage): Pause/Unpause occur exactly once per accepted
lifecycle; root exclusivity; repeated-request guards during opening; modal/root Back order; Pause
pressed while a modal is open closes only the modal and keeps gameplay paused with no double
Unpause; valid first selection; unregistered-Gameplay-Menu safe rejection; rejection during scene
transition, respawn/recovery, and depleted health; the 1.0-second unscaled lockout arms on the
transition falling edge and blocks until elapsed; blocked inputs create no queued state; destroying
the active controller while it owns Pause releases ownership exactly once; destroying a duplicate
controller never touches the real one; teardown while already closed is a no-op.

Also implemented and passing (EditMode): `PauseMenuScreenTests` (14/14) — production
Options/Quit default to gated, non-interactable, Continue-only navigation; enabling either rebuilds
navigation to skip the other when still disabled; disabling a currently-selected gated control
restores selection to Continue; navigation never targets a non-interactable control across all
gating combinations; Quit/Options click handlers no-op while gated; Pause-while-modal-open (driven
through the real `PauseMenuScreen`/`ConfirmationModal` pair) closes only the modal and restores Quit
selection. `ConfirmationModalTests` (4/4) — close restores a valid invoker, falls back to the
parent's fallback selection when the invoker is disabled or inactive, and a whole-root teardown close
clears selection instead of restoring it. `UISandboxControllerFixtureTests` (4/4) — Bonus health and
Resource fixture presets are deterministic across repeated applications.

Confirmed executing and passing via the real PlayMode Test Runner (`HeroInputSuspensionPlayModeTests`,
14/14 — 9 original plus 5 added this pass for Right-Control Dash fallback, X-key double-path, Interact,
and Attack J-key/mouse-left fallbacks): buffers clear at time scale zero; every held command
(Jump/Attack/Dash/Sprint/Interact/Bind, including every physical fallback control) remains disarmed
until its own release/fresh press; releasing one command does not rearm another still-held command;
continuous movement resumes without neutral release; Gamepad East/Bind overlap does not leak;
enabling the Player map does not synthesize a press. The `Underbrew.UI.PlayModeTests.asmdef`
`includePlatforms` mismatch that previously blocked discovery is fixed (now empty, matching
`Underbrew.Camera.PlayModeTests.asmdef`'s convention); the full EditMode suite (365/365) does not
execute these tests, and the focused/full PlayMode suite (29/29, including the unrelated 15 camera
PlayMode tests) discovers and passes them all.

Implemented and passing (`UIFoundationValidator`): one persistent EventSystem, no competing
gameplay-scene EventSystem, Canvas/raycaster semantics, modal/panel hidden-by-default raycast
safety, Sandbox build exclusion; production and Sandbox `InputSystemUIInputModule` action references
are all non-null and resolve to the expected `UI`-map actions (confirmed failing when a reference is
deliberately cleared, then passing again once restored); Sandbox-only components
(`UISandboxController`, `SandboxOptionsPreviewPanel`, `SandboxQuitCallbackStatus`) are absent from
`_GameCameras.prefab`.

Not yet covered: `SceneInit`-alone-insufficient as an explicit regression test (the implementation
does not use `SceneInit` for availability at all, so this is structurally satisfied but has no
dedicated test); controller-disconnect and focus-loss auto-resume prevention (no explicit handling
exists yet — Package A1 simply never listens for these events, which already prevents any
auto-resume, but this is not independently tested); the Sandbox boss-fixture `EnemyConfig` cleanup
fix has no dedicated leak-detection test (verified by code inspection only — `Destroy()` outside Play
Mode logs an error in this Editor, making it impractical to assert in an EditMode test).

## Manual validation

Live-driven and confirmed in a real Play Mode session on `UISandbox.unity` this pass (see
`UISandbox.md`): the Options preview opens with Back-button focus and restores Options selection on
close; the Quit confirmation modal opens the real shared prefab, Cancel restores Quit selection
without firing the callback, and Confirm fires `QuitToMainMenuRequested` exactly once with the status
label updating live; aspect-ratio buttons produce the correct frame size for 16:9/16:10/21:9/4:3; no
scene load and no `GameManager` instance existed throughout.

Not performed this session — no interactive Play Mode input control was exercised against the
**production** Boot path. Still required before Package A1 is considered fully validated on real
device input: keyboard/controller/mouse Pause+Gameplay Menu open/close via the actual `System` map
in the real gameplay scene; rapid toggles; modal focus; holding Pause, Gameplay Menu, Cancel/East,
every command, and movement through blocked/close periods; every transition stage; death/respawn/
hazard/boss states; controller disconnect; focus loss; scene-Hero recreation; Boot-driven entry; and
supported aspect ratios/safe areas in the production HUD/Pause composition.

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
