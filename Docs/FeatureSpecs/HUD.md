# Feature Spec — HUD

**Last audited:** 2026-07-22

## Responsibilities

Display player-facing runtime values without owning gameplay state. The persistent HUD displays normal/bonus health (slot-based) and current resource (a single horizontal fill bar). Menus and other overlays remain separate concerns.

## Current State

Implemented and verified under `Assets/_Project/Scripts/UI/`, living under the persistent `_GameCameras` prefab (verified as the only HUD instance in the project — no gameplay scene defines a second `PersistentHudRoot`/`HealthDisplay`/`ResourceDisplay`):

- `PersistentHudRoot` is the composition root under `_GameCameras` (`DontDestroyOnLoad`), self-destructs any duplicate instance in `Awake`.
- `HealthDisplay` subscribes directly to `PlayerHealthState.Changed` and renders dynamic normal/bonus slots.
- `ResourceDisplay` subscribes directly to `PlayerResourceState.Changed` and renders a single always-visible horizontal fill bar (`ResourceBarView`), fill = `CurrentParts / MaximumParts`, clamped to `[0,1]`, zero when `MaximumParts == 0`.
- The HUD camera (`HUDCamera`) is a URP Overlay camera (`ClearFlags = Nothing`) stacked onto the gameplay `MainCamera` — it does not clear the gameplay view.

`ResourcePipView` (the pre-bar-refactor discrete pip/orb presentation) has been removed; no orb or pip presentation remains anywhere in the project.

## Ownership and lifecycle

The HUD is presentation-only. It reads the persistent ScriptableObject states directly and never routes values through `HeroController`, `HeroHealthComponent`, or `GameManager`. It does not poll in `Update`, search scenes, mutate state, or rebind on `GameManager.SceneInit`.

Each view subscribes once in `OnEnable`, performs an explicit initial refresh, and unsubscribes in `OnDisable`/`OnDestroy`. This supports both startup orders: state application before the HUD is enabled and state application after it has subscribed. Room transitions do not require value rebinding because the state assets persist.

`GameCameras` remains responsible for camera lifetime and camera initialization only. The HUD hierarchy is a child of its persistent prefab; `PersistentHudRoot` protects against an accidental second instance.

## Canvas architecture

UGUI with the existing `HUDCamera` (Overlay, stacked on `MainCamera`):

```
_GameCameras (persistent, DontDestroyOnLoad)
└── HUDRoot (PersistentHudRoot)
    └── HUD Canvas (Screen Space - Camera, HUDCamera, sortingOrder 100)
        └── Player HUD
            ├── Health Display (HealthDisplay)
            │   ├── Normal Health Container
            │   └── Bonus Health Container
            └── Resource Display (ResourceDisplay + CanvasGroup)
                └── Frame/Background
                    └── Fill (ResourceBarView)
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

## Rules

- No HUD component calls `GetComponent` or performs scene searches per frame.
- No HUD component writes `Time.timeScale`, controls, health, resource, or save data.
- Gameplay-context events remain separate from neutral state display events.
- State application never appears as damage, healing, resource gain, or resource spending.
- No final art, menu HUD, enemy health bars, or lifecycle policy is part of this milestone.
