# Feature Spec — UI Sandbox

**Last reviewed:** 2026-07-27  
**Status:** Authoritative planned development workflow. The Sandbox scene, controller, fixtures,
and validators are not implemented.

## Purpose

Provide a direct-open, development-only workspace where UI/UX collaborators can compare layouts,
navigation, device prompts, safe areas, and representative states without running Boot, loading
saves, mutating production state, or duplicating persistent runtime managers.

The UI Sandbox is Package A1 work and supports later production authoring. It is not a playable
frontend, gameplay scene, or fake implementation of deferred systems.

## Non-production scope

- Proposed scene: `Assets/_Project/Scenes/Development/UISandbox.unity` or equivalent.
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
isolated clones that are never registered with `SaveManager`. Fixture presets apply explicit values.

Ability fixtures must not clone mutable unlock values from
`Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`. The confirmed new-game fixture
explicitly locks every ability. This prevents the current development asset's unlocked Dash, Wall
Cling, Double Jump, and Bind values from redefining product behavior.

No fixture can call production save/load, unlock a production ability state, mark world persistence,
or trigger acquisition gameplay.

## Preview controls and states

### Implemented-system presentation

- Health: full, damaged, empty/death frame, changing capacity.
- Bonus health: none, one/multiple bonus slots, clear.
- Resource: empty, partial, full, and zero-capacity safety.
- Boss HUD: hidden, one source, aggregate sources, short/long names, zero state.

### Planned menu presentation

- Root Pause menu.
- Options route placeholder clearly marked non-functional for Package A.
- Quit confirmation modal.
- Gameplay Menu shell.
- Gear true-new-game empty state.
- Gear first-unlock state.
- Gear multi-item state.
- Gear missing-definition diagnostic.

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

No Unity Editor work was performed by this documentation update.

## Automated validation

Planned checks:

- Sandbox scene is excluded from Build Settings.
- No fixture references production `PlayerHealthState`, `PlayerResourceState`, or
  `PlayerAbilityState` assets.
- No production manager or save-file access is present.
- No second production `PersistentHudRoot` is instantiated.
- Fixture-only tabs cannot be enabled in production configuration.
- Shared prefabs remain the same assets used by production composition where intended.
- True-new-game fixture explicitly locks all eight abilities and yields zero Gear entries.
- Missing-definition fixture reports the intended development diagnostic.

## Manual validation

Future manual review covers every named fixture; keyboard/controller/mouse; switching devices;
focus retention; modal raycast blocking; Gear empty-to-first-item behavior; shared-prefab changes;
safe-area guides; 16:9, 16:10, 21:9, and 4:3; direct scene entry; domain reload; and confirmation
that production state/save files are unchanged.

No Unity compilation, EditMode tests, PlayMode tests, Editor validation, visual validation, or
playtesting was run for this documentation update.

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
