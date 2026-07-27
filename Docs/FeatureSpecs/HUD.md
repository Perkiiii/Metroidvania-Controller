# Feature Spec — HUD

**Last audited:** 2026-07-27

## Responsibilities

Display player-facing runtime values without owning gameplay state. The persistent HUD displays
normal/bonus hero health, current resource, and an encounter-scoped aggregate boss-health bar.
`HUDRoot` remains separate from the planned persistent `MenuRoot`, the future sibling
`NotificationRoot`, and scene-local/non-pausing contextual interfaces.

## Current State

Implemented and verified under `Assets/_Project/Scripts/UI/`, living under the persistent `_GameCameras` prefab (verified as the only HUD instance in the project — no gameplay scene defines a second `PersistentHudRoot`/`HealthDisplay`/`ResourceDisplay`):

- `PersistentHudRoot` is the composition root under `_GameCameras` (`DontDestroyOnLoad`), self-destructs any duplicate instance in `Awake`.
- `HealthDisplay` subscribes directly to `PlayerHealthState.Changed` and renders dynamic normal/bonus slots.
- `ResourceDisplay` subscribes directly to `PlayerResourceState.Changed` and renders a single always-visible horizontal fill bar (`ResourceBarView`), fill = `CurrentParts / MaximumParts`, clamped to `[0,1]`, zero when `MaximumParts == 0`.
- `BossHealthDisplay` is always enabled but hidden by `CanvasGroup` until a stateless `BossHudEventService` show request supplies a source token, display metadata, and explicit initialized `EnemyHealthComponent` roster.
- The HUD camera (`HUDCamera`) is a URP Overlay camera (`ClearFlags = Nothing`) stacked onto the gameplay `MainCamera` — it does not clear the gameplay view.

`ResourcePipView` (the pre-bar-refactor discrete pip/orb presentation) has been removed; no orb or pip presentation remains anywhere in the project.

The implemented HUD foundation is not a stub. Final artwork, animation, audio, and additional
feedback remain planned presentation work.

## Ownership and lifecycle

The HUD is presentation-only. It reads the persistent ScriptableObject states directly and never routes values through `HeroController`, `HeroHealthComponent`, or `GameManager`. It does not poll in `Update`, search scenes, mutate state, or rebind on `GameManager.SceneInit`.

Each view subscribes once in `OnEnable`, performs an explicit initial refresh, and unsubscribes in `OnDisable`/`OnDestroy`. Hero-state room transitions require no rebinding because the state assets persist. Boss bindings are deliberately scene-scoped: the display stores the active source token and health subscriptions, ignores mismatched hide requests, and clears all scene references on matching hide or disable.

`BossHudEventService` stores no current request, participant, health component, scene object, or other Unity reference. `BossHealthDisplay` performs no polling or scene search. `PersistentHudRoot` does not wire or retain the boss display.

`GameCameras` remains responsible for camera lifetime and camera initialization only. The HUD hierarchy is a child of its persistent prefab; `PersistentHudRoot` protects against an accidental second instance.

Permanent ability ownership is reviewed through the planned read-only Gear screen, not shown as
permanent combat-HUD indicators. The confirmed true-new-game state has no unlocked abilities; this
does not require placeholder, locked, or undiscovered ability indicators on the HUD. Menus may
visually cover or fade the HUD without changing its subscriptions or persistent state. The future
Local Quick Map is a separate non-pausing overlay and is outside the health/resource/boss HUD
contract.

## Canvas architecture

UGUI with the existing `HUDCamera` (Overlay, stacked on `MainCamera`):

```
_GameCameras (persistent, DontDestroyOnLoad)
└── HUDRoot (PersistentHudRoot)
    └── HUD Canvas (Screen Space - Camera, HUDCamera, sortingOrder 100)
        ├── Player HUD
        │   ├── Health Display (HealthDisplay)
        │   │   ├── Normal Health Container
        │   │   └── Bonus Health Container
        │   └── Resource Display (ResourceDisplay + CanvasGroup)
        │       └── Frame/Background
        │           └── Fill (ResourceBarView)
        └── Boss Health Display (BossHealthDisplay + CanvasGroup)
            ├── Background
            │   └── Fill
            └── Boss Name
```

`FadeCanvas` remains a sibling of `HUDRoot` under `_GameCameras` for transition fades. Final layout groups, placeholder sprites, and feedback polish remain Editor/art work — no visual redesign is in scope for this milestone.

## Health display

`HealthDisplay` renders one dynamic normal slot per `MaximumHealth`, fills slots through `CurrentHealth`, and renders `BonusHealth` in a separate container. It does not hardcode five slots. `HealthSlotView` is an artwork-agnostic element with empty, filled, and bonus states.

`PlayerHealthChangeReason.StateApplied` and `Reset` cause an immediate neutral refresh with no damage/heal/bonus feedback. Gameplay reasons (`Damage`, `Heal`, `FullRestore`, `MaximumChanged`, `BonusGranted`, `BonusCleared`, and `ForcedDepletion`) update the view and may drive local presentation hooks. HUD presentation never triggers death or other gameplay orchestration.

## Resource display

`ResourceDisplay` renders current resource as one horizontal fill bar, not discrete pips or an orb:

```
fillAmount01 = maximumParts > 0 ? currentParts / maximumParts : 0
```

The bar's `CanvasGroup` is always forced to `alpha = 1` (`IsVisible` is hardcoded `true`) — the bar remains visible at zero resource, matching the "resource is a visible-but-empty bar, not a hidden element" requirement. Division by zero is guarded; zero maximum capacity yields `fillAmount01 = 0`, not `NaN`.

`StateApplied` and `Reset` refresh without gain/spend feedback (`OnResourceChanged` excludes both from `gameplayChange`). `Gain`, `Spend`, `MaximumChanged`, and `Cleared` (fired when resource is forfeited on death) update the display and count toward `GameplayFeedbackCount`.

`ResourceDisplay.Configure(PlayerResourceState, PlayerResourceConfig)` — the two-argument overload — is retained only for backward compatibility with callers authored before the bar refactor; the `PlayerResourceConfig` argument is ignored by the bar presentation. `PlayerResourceConfig.partsPerPip` itself is retained as inert configuration (still exercised by `HudDisplayTests` fixture setup) but no longer read by any presentation code.

## Bind affordability

No Bind-readiness indicator is added in this pass. A HUD affordance may later derive “missing health” and “enough resource” from the two persistent states plus `PlayerResourceConfig.bindCostParts`, but it must not claim full Bind eligibility because grounding, control locks, and active actions belong to the hero action system.

## Boss health display

The encounter controller shows the HUD only after every participant actor has initialized. The display performs an explicit initial property read, removes null/uninitialized/duplicate sources, sums all configured current and maximum values, and updates on `EnemyHealthComponent.OnHealthChanged`. Lethal health events arrive before `OnDeath`, so the aggregate can visibly reach zero before encounter death orchestration continues.

A matching hide request clears visibility, source identity, roster references, and subscriptions. A request from any other source is ignored. Completion, failed startup, hero-death interruption, and controller disable/unload all send source-scoped hide requests.

## Rules

- No HUD component calls `GetComponent` or performs scene searches per frame.
- No HUD component writes `Time.timeScale`, controls, health, resource, or save data.
- Gameplay-context events remain separate from neutral state display events.
- State application never appears as damage, healing, resource gain, or resource spending.
- Boss HUD artwork and animation remain placeholder presentation; per-ordinary-enemy health bars are not implemented.
- `HUDRoot` does not own `MenuRoot`, future `NotificationRoot`, Quick Map, or frontend UI.
- Ability flags and Gear ownership never become combat-HUD state merely to populate presentation.

## Planned additions

The following are planned or deferred and must not be described as implemented:

- Interaction prompts.
- Area titles.
- Save indicators.
- Acquisition notifications.
- Tutorial prompts.
- Possible equipped consumable/brew slots.
- Temporary status indicators.
- Final health/resource/boss visual, animation, and audio feedback.

`NotificationRoot` is the future sibling presentation composition for acquisition, area-title,
save, and tutorial notifications. Its queue/event/content ownership requires a separate approved
implementation; it is not folded into `PersistentHudRoot` or the planned menu coordinator.

## Non-goals

- Owning or mutating health, resource, abilities, boss state, saves, or input.
- Reintroducing resource pips or deriving presentation grouping from `partsPerPip`.
- Permanently displaying undiscovered abilities or the Gear collection.
- Owning menu, modal, Map, notification, or scene-transition flow.
- Replacing encounter-local boss orchestration.

## Unity Editor work

The implemented hierarchy and references already exist on
`Assets/_Project/Prefabs/Managers/_GameCameras.prefab`. Future HUD presentation work must:

- Preserve one `PersistentHudRoot`, the dedicated URP Overlay `HUDCamera`, and existing direct state
  references.
- Author final art/animation/audio without changing gameplay ownership.
- Verify menus/future notifications use sibling layering and do not block HUD raycasts while hidden.
- Validate supported aspect ratios and safe areas once target platforms are approved.

No Unity Editor work was performed by this documentation update.

## Automated and manual regression validation

Future implementation passes must retain automated coverage for initial snapshot refresh,
neutral save/reset application, duplicate subscription prevention, health/bonus slot changes,
continuous resource fill including zero capacity, and source-scoped aggregate boss show/hide.

Manual regression should verify one persistent HUD across Boot-driven room transitions; no feedback
on save/reset application; health damage/heal/bonus/death presentation; resource gain/spend/clear;
boss initialization, aggregation, lethal zero, interruption, and unload cleanup; menu visual
coverage without subscription loss; and common input/aspect-ratio combinations. No Unity
compilation, tests, Editor validation, visual validation, or playtesting was run for this
documentation update.

## Risks

- Accidentally adding a second HUD or EventSystem in a gameplay scene.
- Treating save/reset application as gameplay gain, heal, or damage feedback.
- Letting menu visibility disconnect persistent state subscriptions.
- Reintroducing obsolete pip grouping or using mutable ability-state assets as display defaults.
- Coupling Quick Map, Gear, or notifications to combat-HUD ownership.

## Open decisions

- Final health/resource/boss artwork, typography, animation, and audio feedback.
- Which planned prompts, indicators, temporary statuses, and possible consumable/brew slots are
  approved for the combat HUD.
- Final supported aspect-ratio, safe-area, localization, and accessibility requirements.
- Whether future notifications remain visible beneath a covering pausing root; that policy belongs
  to the future notification/UI architecture, not persistent HUD state.

## Related specifications

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/Abilities.md`
