# Feature Spec — UI Sandbox

**Last reviewed:** 2026-07-27  
**Canonical scene path:** `Assets/_Project/Scenes/UISandbox.unity` (relocated out of
`Scenes/Development/`; the scene GUID was preserved by the move). `UIFoundationValidator` and this
spec both use the new path.

**Status:** Implemented (Package A1), including a correction pass, and extended in Package A2 with
the real Gameplay Menu and isolated Gear fixtures. `UISandboxController`, HUD/Pause/modal fixtures, the Options preview
placeholder (`SandboxOptionsPreviewPanel`), the Quit callback status label (`SandboxQuitCallbackStatus`),
and `UIFoundationValidator`'s Sandbox checks exist and pass. Health/resource fixture presets are now
deterministic across repeated clicks, and runtime boss-fixture `EnemyConfig` instances are tracked
and destroyed when superseded. A real Play Mode session live-drove the Options preview, Quit
callback, and aspect-ratio flows this pass — see Manual validation below.

Package A2 nested the real production `GameplayMenuScreen.prefab` under the Sandbox's
`RootInterfaceLayer` and added Gameplay Menu open/close plus five Gear fixture presets. The Gear
tab's production `PlayerAbilityState` and `GearDisplayCatalog` references are cleared on the
Sandbox instance; `UISandboxController` supplies runtime-created isolated substitutes instead, and
the validator now fails if either boundary is crossed in either direction.

Two pre-existing composition defects in this scene were corrected while adding the A2 fixtures:

- `RootInterfaceLayer` and `ModalLayer` were left at Unity's default 100x100 child rect, which
  silently collapsed any edge-anchored nested screen. Both are now full-screen stretched, matching
  the production layers (which are Canvases and therefore always fill the screen). Existing
  centre-anchored Pause/Confirmation content is unaffected.
- `FixtureControlPanel` was not scrollable, so its content — already ~2.7x the canvas height before
  A2, and 3704 px against a 1040 px viewport after — put most presets permanently off-screen.
  The panel is now a clamped vertical `ScrollRect` with a `RectMask2D` over the same `Content`
  object; mouse-wheel scrolling reaches every preset. There is deliberately no visible scrollbar
  (development tool, mouse-driven); add one if keyboard/controller access to the fixture list is
  ever wanted.

Notification fixtures remain out of scope per the exclusions below. Interactive review of
keyboard/controller device navigation and visual safe-area guides across all four aspect presets has
not been performed.

## Purpose

Provide a direct-open, development-only workspace where UI/UX collaborators can compare layouts,
navigation, device prompts, safe areas, and representative states without running Boot, loading
saves, mutating production state, or duplicating persistent runtime managers.

The UI Sandbox is Package A1 work and supports later production authoring. It is not a playable
frontend, gameplay scene, or fake implementation of deferred systems.

## Non-production scope

- Scene: `Assets/_Project/Scenes/UISandbox.unity`.
- Opened directly from the Unity Editor.
- Excluded from Build Settings.
- Does not run the production Boot or save flow.
- Does not read, create, overwrite, or delete production save files.
- Does not mutate production ScriptableObject state.
- Does not instantiate duplicate `GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, or
  other persistent production managers solely for preview.
- Does not create a second production `PersistentHudRoot`.
- Does not claim gameplay validation.

## Preferred composition

```text
UISandbox
└── SandboxPreviewRoot
    ├── HealthDisplay with isolated PlayerHealthState
    ├── ResourceDisplay with isolated PlayerResourceState
    ├── BossHealthDisplay fixture adapter
    ├── Pause/Menu shared prefabs
    └── Gear shared prefabs
```

The Sandbox owns preview composition and fixture controls. Shared reusable visual/menu/Gear
prefabs are nested beneath it so approved changes can be applied back intentionally. Production
`_GameCameras` and persistent lifecycle wiring are validated separately.

## State isolation

Health, resource, and ability preview state uses runtime-created ScriptableObject instances or
isolated clones that are never registered with `SaveManager`. The health/resource controls and the
shared displays are configured against those same instances; fixture presets apply explicit values.

Ability fixtures must not clone mutable unlock values from
`Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`. `UISandboxController` creates a
fresh runtime `PlayerAbilityState` and calls `ResetToDefaults()`, so the fixture always starts from
the confirmed all-locked new-game state and never reads the production asset.

Gear display data is likewise runtime-only: `UISandboxController` builds a `GearDisplayCatalog`
instance and three `GearDisplayDefinition` instances through `SetFixtureContent`, labels their
category "Sandbox fixture", and destroys all of them in `OnDestroy`. None of them is a project
asset, and the production catalogue stays empty.

No fixture can call production save/load, unlock a production ability state, mark world persistence,
or trigger acquisition gameplay.

## Preview controls and states

### Implemented-system presentation

- Health: Damage -1, Heal +1, full, damaged, empty/death frame, changing capacity. Incremental
  actions clamp through `PlayerHealthState`; no production health rules are changed.
- Bonus health: none, one/multiple bonus slots, clear.
- Resource: Empty, Add +1, Partial, Full, Spend -1, Clear, and zero-capacity safety. Every action
  refreshes the real shared `ResourceDisplay` from the isolated `PlayerResourceState`.
- Boss HUD: hidden, one source, aggregate sources, short/long names, zero state.

### Menu presentation

Implemented (Package A1):

- Root Pause menu opened through the Sandbox-local `UIFlowController`.
- Options preview: a Sandbox-only toggle enables the production `PauseMenuScreen`'s Options button;
  clicking it opens `SandboxOptionsPreviewPanel`, a labelled placeholder ("Options Preview / Package B
  will implement functional settings. / Back") that owns first selection on open, closes via its Back
  button or `UI/Cancel`, and restores selection to the Options button on close. It creates no
  settings data, audio mixer, or persistence, and exists only in this scene.
- Quit confirmation modal: a Sandbox-only toggle enables the typed Quit request seam and lets Quit
  open the real shared `ConfirmationModal`. `SandboxQuitCallbackStatus` subscribes to
  `PauseMenuScreen.QuitToMainMenuRequested` and displays a visible fired/not-fired counter proving
  the callback actually reaches a subscriber, without loading a scene, saving, or running Boot.

Implemented (Package A2):

- Gameplay Menu: `OpenGameplayMenuPreview` drives the real production prefab through the
  Sandbox-local `UIFlowController`. The Sandbox injects `IUIFlowHost` because it has no
  `GameManager`; root exclusivity, Cancel/toggle handling, `CloseRequested`, action-map mode, and
  selection still run through the production flow owner.
  `SelectGameplayMenuTab(GameplayMenuTabId)` switches tabs programmatically for review.
- Gear "None acquired" — the true-new-game empty state.
- Gear "One acquired" — first-unlock presentation and selection.
- Gear "Several acquired" — deterministic catalogue order and details.
- Gear "Live unlock" — unlocks one more ability with no explicit rebuild, exercising the
  `AbilityChanged` path while the tab is open.
- Gear "Unlocked, no definition" — the omit-and-log-once content-gap path.

The scene also contains a separate `DeveloperUtilityLayer` Canvas above Root and Modal. Its
emergency close uses the normal `UIFlowController.RequestCloseRoot` path. Its last-resort recovery
uses the host-gated `RequestHostRecoveryClose` seam; the developer component never hides a root
screen directly. Validator and asset tests reject this layer from `_GameCameras.prefab` and the
standalone production Gameplay Menu prefab.

### Gear fixture presets

- New Game — no abilities unlocked.
- First Ability Acquired.
- Several Abilities Acquired.
- All approved definitions unlocked.
- Missing Definition Diagnostic.

Do not label Dash or Wall Cling “starting-only” content. Fixture unlocks are explicitly authored for
the selected preview and do not imply production starting ownership.

### Deferred-system fixtures

- Lightweight Tools, Satchel, Recipes, Tasks, Journal, and Map tab fixtures for shell/layout only.
- Static acquisition notification, area-title, save-indicator, tutorial, and interaction-prompt
  fixtures.
- Map remains a non-functional layout fixture with no identity, discovery, input, save, or
  transition behavior.

Fixture-only future tabs never become production registrations and do not create gameplay data
models.

### Input and layout matrix

- Keyboard navigation.
- Controller navigation.
- Mouse interaction.
- Device-prompt switching.
- Safe-area visualization.
- 16:9.
- 16:10.
- 21:9.
- 4:3.

Supported shipping targets remain open; these are development comparison presets.

## Direct-open workflow

1. Open the Sandbox scene directly, without Boot.
2. Choose a named fixture preset.
3. The Sandbox creates or resets isolated runtime state.
4. Select a shared screen/prefab and input mode.
5. Compare layout, focus, prompts, safe area, and aspect ratio.
6. Apply approved nested-prefab changes intentionally.
7. Validate production prefab composition separately through the Boot/gameplay path.

Closing or reloading the Sandbox discards its runtime fixture state.

## Nested-prefab workflow

- Reusable screen, panel, button, tab, Gear entry/details, and prompt presentation lives in shared
  prefabs where reuse is real.
- Sandbox-only controls, labels, fixtures, and adapters remain outside shared production prefabs.
- Apply changes from the nested shared instance only after UI/UX review.
- Avoid unpacking shared prefabs merely to tune a preview.
- Do not duplicate `_GameCameras`, production managers, or the entire persistent HUD to make the
  preview convenient.

## UI/UX collaborator workflow

UI/UX collaborators may:

- Compare the three Gear layout candidates.
- Tune shared visual prefabs and presentation-only animation.
- Author explicit navigation, selected/hover/disabled states, empty-state presentation, and device
  prompts.
- Inspect safe-area/aspect behavior.
- Produce reviewed artwork/copy proposals.

They do not need to run the production save/Boot flow for every visual iteration. Production
integration remains an engineering/review step.

## Responsibility split

| Concern | Sandbox/UI-UX | Production gameplay/engineering |
|---|---|---|
| Fixture state | Explicit isolated presets | Authoritative state remains unchanged |
| Shared visual prefabs | Preview and reviewed presentation edits | Production wiring/lifecycle validation |
| Gear layout comparison | Prototype and recommendation | Ownership mapping, refresh, tests, catalogue validation |
| Menu focus/navigation | Author/test in preview | Persistent EventSystem/action-map/input-resume integration |
| Future tabs | Static shell fixtures only | Implement only after owning gameplay system approval |
| Save/Boot/world state | Excluded | Existing production owners |

## Package A1 relationship

Package A1 establishes the Sandbox, shared presentation prefabs, persistent menu/input foundation,
Pause/modal flow, focus, and validators. Sandbox preview work may begin before production menu
wiring, but it does not mark that wiring complete. Package A2 adds the production Gameplay Menu and
Gear slice after the new-game-ability-default prerequisite is corrected for validation.

## Explicit exclusions

- Build Settings inclusion.
- Boot, frontend, New Game, Continue, or slot flow.
- Production save files or mutable production state.
- Duplicate persistent manager/HUD composition.
- Functional Options settings in Package A.
- Functional Quit transition.
- Gameplay inventory, Satchel, Recipes, Tasks, Journal, or Map systems.
- Map input, discovery, identity, transition continuity, or persistence.
- Acquisition notification gameplay.
- Final art, localization, accessibility, display, and platform certification.

## Required Unity Editor setup

- Create the direct-open development scene outside enabled Build Settings.
- Create `SandboxPreviewRoot` and Sandbox-only fixture controller/adapters.
- Nest shared reusable HUD/menu/Gear prefabs without duplicating production persistence roots.
- Configure a Sandbox-local preview EventSystem only if required for direct operation; validator
  must ensure it cannot enter production composition.
- Assign runtime-created/isolated state factories and explicit fixture values.
- Add aspect-ratio and safe-area preview controls.
- Add clear visual labeling for fixture-only/non-functional content.

Package A1 Unity Editor work performed: created `UISandbox.unity` (camera, local `EventSystem`/
`InputSystemUIInputModule`, HUD preview using `HealthDisplay`/`ResourceDisplay`/`BossHealthDisplay`
with isolated state, nested `PauseMenuScreen.prefab`/`ConfirmationModal.prefab` instances, fixture
control panel, aspect-frame/safe-area guide RectTransforms); created two Sandbox-only
`BossEncounterDefinition` fixture assets under `Assets/_Project/ScriptableObjects/Sandbox/`; left
the scene out of Build Settings. Correction-pass work performed: wired the local
`InputSystemUIInputModule`'s action references to durable `UI`-map `InputActionReference` sub-assets
(previously all null); authored the `OptionsPreviewPanel` (dim background, message, Back button)
under `ModalLayer` with `SandboxOptionsPreviewPanel` wired to the Sandbox EventSystem and the
persistent `UI/Cancel` action reference; authored the `QuitCallbackStatus` label in the fixture
control panel with `SandboxQuitCallbackStatus` wired to the shared `PauseMenuScreen`.

## Automated validation

Implemented and passing (`UIFoundationValidator`, `Tools/Project/Validate UI Foundation`):

- Sandbox scene is excluded from Build Settings.
- No `HealthDisplay`/`ResourceDisplay` component in the Sandbox scene references a production
  `PlayerHealthState`/`PlayerResourceState` asset path.
- No second production `PersistentHudRoot` is instantiated in the Sandbox scene.
- `UISandboxController` is present.
- The Sandbox-local `InputSystemUIInputModule` has the correct actions asset and all ten required
  action references resolving to the expected `UI`-map actions.
- `UISandboxController`, `SandboxOptionsPreviewPanel`, `SandboxQuitCallbackStatus`, and
  `SandboxDeveloperUtilityLayer` are absent from `_GameCameras.prefab`.
- Exactly one Sandbox-local `UIFlowController` owns both root prefabs.
- Sandbox Canvas < Root < Modal < Developer Utility sorting is enforced; Root and Modal override
  nested sorting and each interactive layer retains its `GraphicRaycaster`.

Also implemented and passing (EditMode `UISandboxControllerFixtureTests`, 6 tests): Damage/Heal
mutate one health per actual button click and clamp, every resource action updates both isolated
state and displayed current/capacity/fill, repeated fixture configuration/enable cycles do not
duplicate listeners, and the deterministic presets remain stable.

Not yet covered by the validator (Package A2+ scope, since Gear/Map/notification fixtures do not
exist yet): fixture-only-tab production-enable prevention, true-new-game Gear fixture lock/empty
check, missing-definition diagnostic. The boss-fixture `EnemyConfig` tracking/destroy fix has no
dedicated automated leak-detection test (`Destroy()` outside Play Mode logs an error in this Editor,
making it impractical to assert in an EditMode test) — verified by code inspection only.

## Manual validation

Live-driven and confirmed in a real Play Mode session this pass (`EditorApplication.isPlaying`,
direct `Button.onClick.Invoke()` calls on the actual running Sandbox scene, no Boot, single scene
loaded throughout, `GameManager.Instance` confirmed null throughout):

- Enabling the Options preview toggle makes the production Options button interactable; clicking it
  opens `SandboxOptionsPreviewPanel` and selects its Back button; clicking Back closes the panel and
  restores selection to the Options button.
- Enabling the Quit request-seam toggle makes the production Quit button interactable; clicking it
  opens the real shared `ConfirmationModal` (Cancel selected first); clicking Cancel closes it,
  restores Quit selection, and leaves the status label at "not fired yet"; reopening and clicking
  Confirm closes the modal and updates the status label to "Quit callback fired (1)".
- All four aspect-ratio buttons (16:9, 16:10, 21:9, 4:3) set the expected distinct `AspectFrame`
  size.
- No console errors were logged during the sequence; the active scene remained `UISandbox.unity`
  throughout.

Still required (a genuine visual/input-device review, not exercisable by driving `onClick` directly):
keyboard/controller/mouse navigation through the fixture panel and Options/Quit flows; focus
retention across device switches; modal raycast blocking; safe-area guide visual check across 16:9,
16:10, 21:9, and 4:3; and confirmation that production save files/state assets are
unchanged after a Sandbox session.

## Risks

- A fixture accidentally references and mutates a production state asset.
- Direct Sandbox entry creates or changes a production save.
- A duplicate persistent manager, EventSystem, camera, or HUD makes preview behavior misleading.
- Sandbox-only tabs or controls leak into production configuration.
- Shared prefabs diverge because previews use copies instead of nested production assets.
- Mutable development ability values masquerade as the new-game fixture.
- Non-functional Map/Options/Quit fixtures are mistaken for implemented behavior.

## Open decisions

- Final Sandbox scene path/name and fixture-control UI.
- Final Gear layout selected after prototypes.
- Final shipping aspect-ratio/safe-area targets.
- Final shared-prefab boundaries and presentation animation approach.
- Final artwork, typography, glyph, localization, accessibility, and motion direction.

## Related specifications

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/HUD.md`
