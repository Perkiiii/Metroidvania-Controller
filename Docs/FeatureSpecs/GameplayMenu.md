# Gameplay Menu

**Status:** Implemented (Package A2.1), with Package A3 visual foundation. Five visible top-level tabs; Gear is the only
data-backed tab. The four sibling tabs present authored empty states until their domain owners
exist.

**Owner scripts:** `Assets/_Project/Scripts/UI/Screens/GameplayMenuScreen.cs`,
`Assets/_Project/Scripts/UI/GameplayMenu/*`, and `Assets/_Project/Scripts/UI/Gear/*`.

This document is the durable cross-tab contract. Gear content rules live in
`Docs/FeatureSpecs/Gear.md`; pausing-root behaviour lives in
`Docs/FeatureSpecs/PauseAndMenuFlow.md`; persistent composition lives in
`Docs/FeatureSpecs/UIArchitecture.md`.

## Fixed tab contract

The player-facing order is:

```text
Gear · Loadout · Satchel · Field Notes · Map
```

The stable internal IDs are:

```text
Gear · CombatLoadout · Satchel · FieldNotes · Map
```

The enum values are contiguous and serialized as integers:

```csharp
public enum GameplayMenuTabId
{
    Gear = 0,
    CombatLoadout = 1,
    Satchel = 2,
    FieldNotes = 3,
    Map = 4
}
```

All five tabs are visible, interactable, and reachable in production. There is no availability
or hidden flag on `GameplayMenuTabRegistration`. A tab with no gameplay owner shows an authored
empty state; it is never disabled or filled with invented data. Exactly one tab content root is
active while the menu is open.

`CombatLoadout` is the future combat-build destination for configurable crest/charm-style rules.
Its label is `Loadout`; it must not be confused with literal sickle, axe, mining, or farming-tool
ownership. `FieldNotes` is the future knowledge destination for Recipes, Tasks, and Journal/
discovery content as internal sections. No Field Notes section navigation exists yet.

## Architecture and ownership

`UIFlowController` owns root availability, pause ownership, input-map mode, hero input
suspension/resume, and root-level selection entry. `GameManager` owns pause and time scale.
`GameplayMenuScreen` owns only active-tab state, runtime-only last-tab and per-tab selection
memory, the horizontal tab strip, cycling, explicit navigation, and Back delegation.

`GameplayMenuScreen` never writes `Time.timeScale`, touches `HeroInputReader`, loads scenes, saves,
or owns gameplay state. `IGameplayMenuTab` exposes lifecycle, Back routing, modal state, and first
selection only. Tab presenters do not reference gameplay owners or save services.

Gear remains read-only and is backed by `PlayerAbilityState` plus `GearDisplayCatalog`. The other
four tabs use `GameplayMenuEmptyTabView`, which owns presentation only. No Loadout rules, practical
tool ownership, Satchel inventory, Recipes, Tasks, Journal data, Map data, save migration,
`PlayerPrefs`, or new manager is part of this correction.

Last-viewed tab and per-tab selection memory are runtime-only fields. They are not serialized, not
static, not in `PlayerPrefs`, and not in `SaveData`. They survive room transitions because the
screen is persistent and reset on application restart. The first open selects Gear.

## Input and navigation

| Purpose | Action | Keyboard | Gamepad |
|---|---|---|---|
| Open / close | `System/GameplayMenu` | `I` | `Select` (provisional) |
| Previous tab | `UI/PreviousTab` | `Q` (provisional) | Left Shoulder (provisional) |
| Next tab | `UI/NextTab` | `E` (provisional) | Right Shoulder (provisional) |
| Navigate | `UI/Navigate` | WASD / arrows | Left stick / D-pad |
| Activate | `UI/Submit`, `UI/Click` | Enter / mouse | South button |
| Back | `UI/Cancel` | Escape | East button |

Existing bindings remain unchanged. Keyboard `Tab` remains reserved for deferred Quick Map.
`GameplayMenuScreen` subscribes to the previous/next actions while shown and leaves action-map
ownership to the flow controller except for its direct-preview fallback seam.

The strip is a horizontal explicit-navigation chain in the fixed order. Left/Right cycle through
all five cells and wrap Map to Gear and Gear to Map. Up/Down connect the strip to the active tab's
content entry point; empty tabs keep focus on their strip button. Back first delegates to an active
child/modal, then closes the Gameplay Menu through the normal root flow.

## Composition and presentation

```text
GameplayMenuScreen
└── Panel                      full-screen scrim, inactive by default
    └── Frame
        ├── HeaderBar
        │   ├── PreviousTabHint
        │   ├── StripViewport
        │   │   └── TabStrip  five GameplayMenuTabButton instances
        │   ├── NextTabHint
        │   └── active tab title + global Close
        ├── AccentLine
        └── ContentHost        five nested tab prefabs
```

The production strip and content host both use this sibling order:

```text
Gear, CombatLoadout, Satchel, FieldNotes, Map
```

The top-centred horizontal strip, spacing, reserved width, cell dimensions, compact-title
behaviour, hints, Close control, and runtime navigation architecture remain unchanged. At narrow
widths every glyph remains visible; per-cell titles collapse and the selected title remains visible
separately. Package A3 migrates the presentation to TMP, separates persistent active-tab accents
from transient focus/hover surfaces, and adds the shared dark-frame/brass-accent hierarchy and
unscaled root transition. Final bespoke font, icon, and illustrated motif content is deferred.

## Empty-state copy

| Tab | Title | Body |
|---|---|---|
| Gear (zero acquired) | Nothing carried yet | Gear you recover on your travels is kept here, ready to hand. |
| Combat Loadout | Nothing fitted | Anything you recover that can alter your fighting style will be kept here. |
| Satchel | An empty satchel | Seeds, cuttings, and whatever else you gather along the way will settle in here. |
| Field Notes | Blank pages | Recipes, promises, and discoveries will gather here as you learn more about the world. |
| Map | Uncharted | The ground you cover will be drawn here as you learn its shape. |

These are intentional player-facing states. Developer-facing placeholder copy is not permitted.

## Deferred domain seams

| Tab | Missing dependency |
|---|---|
| Combat Loadout | product rules, slot/capacity model, modifiers, equip state, owner, persistence |
| Satchel | inventory definitions, quantities, categories, owner, persistence |
| Field Notes | recipe knowledge, task progression, journal/discovery owners, internal navigation |
| Map | stable room catalogue, visited-room snapshot, current-room resolver, authored geometry, markers, pan/zoom |

The Map boundary remains unchanged: `WorldStateRegistry` visitation facts and scene editor graph
coordinates are not player-map identity or presentation. Quick Map, the Full Map shortcut, and map
markers remain deferred.

## Validation and test coverage

`Tools/Project/Validate UI Foundation` validates the persistent root composition, inactive visual
root, five serialized registrations in fixed order, unique and defined IDs, visible/interactable
buttons, valid presenters, empty-state copy, Gear production references, action references,
presentation-only ownership, TMP/font assignment, and Sandbox isolation. Serialized enum values are read through their
underlying integer so stale values such as the former Map value `6` are diagnosed clearly.

Coverage is maintained in `GameplayMenuScreenTests`, `GameplayMenuProductionAssetTests`, and
`GameplayMenuPlayModeTests`. It includes order, stable IDs, display names, visibility,
interactability, one active content root, default Gear, cycling and wraparound, selection and
runtime memory, Back/close routing, production wiring, Sandbox isolation, obsolete asset cleanup,
and presenter ownership boundaries.

Visual anatomy and art-replacement seams are specified in
`Docs/FeatureSpecs/UIVisualFoundation.md`.
