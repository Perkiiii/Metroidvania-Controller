# Gameplay Menu

**Status:** Implemented (Package A2). Seven visible tabs; Gear is the only data-backed tab. The
six sibling tabs present authored empty states until their gameplay owners exist.

**Owner scripts:** `Assets/_Project/Scripts/UI/Screens/GameplayMenuScreen.cs`,
`Assets/_Project/Scripts/UI/GameplayMenu/*`, `Assets/_Project/Scripts/UI/Gear/*`.

This document is the durable cross-tab contract. Gear's own content rules live in
`Docs/FeatureSpecs/Gear.md`; pausing-root behaviour lives in
`Docs/FeatureSpecs/PauseAndMenuFlow.md`; persistent composition lives in
`Docs/FeatureSpecs/UIArchitecture.md`.

---

## 1. What it is

The Gameplay Menu is the second pausing `IUIFlowRootScreen`, opened by `System/GameplayMenu`. It
extends Package A1 rather than replacing any part of it: `UIFlowController` still owns root
availability, pause ownership, input-map mode, Hero input suspension/resume, and root-level
selection entry. `GameplayMenuScreen` is registered through the existing
`UIFlowController.gameplayMenuRootBehaviour` field — no A1 production code changed for A2.

## 2. Fixed tab contract

`GameplayMenuTabId` declares the seven confirmed tabs, and its declaration order **is** the
presentation and cycling order:

```text
Gear · Tools · Satchel · Recipes · Tasks · Journal · Map
```

Rules that are not negotiable without a product decision:

- All seven tabs are always visible and reachable in production. There is deliberately no
  availability or hidden flag on `GameplayMenuTabRegistration`.
- A tab with no gameplay owner shows an authored empty state. It is never hidden, never disabled,
  and never populated with invented data, totals, capacity, silhouettes, or mystery slots.
- Empty-state copy is player-facing. Developer wording ("coming soon", "not implemented", "TODO",
  "placeholder") is rejected by `UIFoundationValidator` and by
  `GameplayMenuProductionAssetTests`.
- Exactly one tab's content is active at a time.

`GameplayMenuScreen` logs a concise authoring error and fails closed to a usable Gear/Close
selection for a wrong tab count, wrong order, duplicate ID, missing button, missing view, or a view
that does not implement `IGameplayMenuTab`.

## 3. Tab contract

```csharp
public interface IGameplayMenuTab
{
    Selectable FirstSelection { get; }   // null is valid — empty tabs have no content selection
    void Show();
    void Hide();
    bool HandleBackInternally();
    bool HasOpenModal { get; }
}
```

The contract is lifecycle, Back routing, and first selection only. It exposes no gameplay owner,
save API, item/recipe/task/map data, input-reader access, or generic window behaviour. A future
domain owner is bound inside its concrete tab presenter; the interface does not widen.

`GameplayMenuTabId` lives on the authored registration rather than on the view, so identity is not
duplicated between prefab data and component state.

### Presenters

| Tab | Presenter | Data |
|---|---|---|
| Gear | `GearScreen` | `PlayerAbilityState` (read-only) + `GearDisplayCatalog` |
| Tools, Satchel, Recipes, Tasks, Journal, Map | `GameplayMenuEmptyTabView` | none |

The six sibling tabs share one presentation-only component. Everything that distinguishes them —
copy, motif, accent, layout — is authored in their own prefabs, not duplicated across six
behaviourally identical scripts. When a tab's authoritative owner is approved it gets its own
focused presenter (as Gear has) and that component is swapped in its prefab; `IGameplayMenuTab`
does not change.

## 4. Ownership boundary

`GameplayMenuScreen` owns only: the active tab, runtime-only last-tab and per-tab selection memory,
tab-rail presentation and explicit navigation, Previous/Next cycling, and Back delegation.

It never writes `Time.timeScale`, never touches `HeroInputReader`, never loads a scene, never
saves, and owns no gameplay state. `UIFoundationValidator` and
`GameplayMenuProductionAssetTests` both reject a tab presenter that serializes a `SaveManager`,
`HeroInputReader`, `SceneTransitionManager`, `WorldStateRegistry`, `GameManager`, or
`UISandboxController` reference.

### Runtime memory

Last-viewed tab and per-tab selection are ordinary runtime fields on the persistent screen. They
are **not** serialized, **not** static, **not** in `PlayerPrefs`, and **not** in `SaveData`. They
survive room transitions only because the screen itself is persistent, and reset on application
restart. First open of a session selects Gear.

## 5. Input and navigation

| Purpose | Action | Keyboard | Gamepad |
|---|---|---|---|
| Open / close | `System/GameplayMenu` | `I` | `Select` (View/Back) — provisional |
| Previous tab | `UI/PreviousTab` (new) | `Q` — provisional | Left Shoulder — provisional |
| Next tab | `UI/NextTab` (new) | `E` — provisional | Right Shoulder — provisional |
| Navigate | `UI/Navigate` | WASD / arrows | Left stick / D-pad |
| Activate | `UI/Submit`, `UI/Click` | Enter / mouse | South button |
| Back | `UI/Cancel` | `Escape` | East button |

- Tab cycling lives on the existing `UI` map. `Player/Previous` and `Player/Next` are **not**
  reused, and keyboard `Tab` is **not** bound — it stays reserved for the future Quick Map.
- `GameplayMenuScreen` subscribes to the two tab actions only while shown, and never enables or
  disables the UI action map itself. In a direct-preview context with no flow controller (the UI
  Sandbox) it enables only the two actions it owns, and disables exactly those again on hide.
- Tab input while the root is closed is ignored, never queued.
- Durable `InputActionReference` sub-assets are assigned for `System/Pause`,
  `System/GameplayMenu`, `UI/Cancel` (on `UIFlowController`) and `UI/PreviousTab`, `UI/NextTab`
  (on `GameplayMenuScreen`). Resolve-by-map/name fallback is retained on both.

### Focus model

The rail is a single vertical chain — seven tab buttons then the global Close control — built in
code at initialisation so it can never drift from the authored tab order. It wraps at both ends.

- `Right` from the active tab button enters that tab's content entry point. An empty tab has none,
  so `Right` simply does nothing there rather than jumping into another tab's content.
- `Left` from Gear content returns to the Gear tab button.
- Switching tabs **while focus is on the rail** keeps focus on the newly active tab button.
  Switching **while focus is in content** moves to the new tab's remembered-or-first selection.
  Leaving content for an empty tab therefore parks focus on the rail, and it stays there until the
  player deliberately enters content again.
- Selection resolution order: remembered valid selection → the tab's `FirstSelection` → the
  registration's authored fallback → the active tab button → the global Close control. EventSystem
  selection is never null while the root is open.
- Cycling wraps Map → Gear and Gear → Map (provisional; `wrapTabCycling` is serialized).

### Back hierarchy

active tab child/modal → Gameplay Menu root → the Package A1 close sequence. No tab owns a nested
layer in Package A2, so `HandleBackInternally` always returns false and Cancel closes the root.

## 6. Composition and visual direction

```text
GameplayMenuScreen
└── Panel                      full-screen scrim, inactive by default
    └── Frame                  inset 96/56 at 1920x1080 reference
        ├── HeaderBar
        │   ├── PreviousTabHint
        │   ├── StripViewport
        │   │   └── TabStrip  seven GameplayMenuTabButton instances
        │   ├── NextTabHint
        │   └── active tab title + global Close
        ├── AccentLine
        └── ContentHost        seven nested tab prefabs
```

- `Panel` defaults to **inactive**, matching Pause and Confirmation, so a closed menu cannot block
  HUD raycasts. The validator and both test suites assert this.
- The screen is a sibling of `PauseMenuScreen` under `MenuRoot/RootInterfaceLayer`. Semantic
  layering Fade > Modal > Root > HUD is unchanged.
- The HUD stays active and subscribed behind the menu; it is covered visually only.
- Active-tab identity uses two channels independent of focus: an accent bar and a warm label
  colour. UGUI highlighted/selected/pressed states remain separate so focus and "which tab am I
  on" are never conflated.
- Each tab carries a restrained accent colour used by its strip glyph, active bar, and empty-state
  motif, over one shared frame, type scale, and focus language.

### Provisional presentation choices

These are reversible defaults chosen during implementation. They are documented here so the UI/UX
partner can revise them without touching tab IDs, ownership, or flow:

- Top-centred horizontal strip in the fixed Gear → Map order, flanked by Previous/Next hints.
- At narrow widths all seven glyphs remain visible; per-cell titles collapse and the selected tab
  remains named separately.
- Left/Right traverses the strip, Down enters the active tab's content, and Up returns from the
  first content selection to the active strip button. The global Close remains an explicit fallback.
- Legacy `UnityEngine.UI.Text` and the built-in font, matching the existing A1 prefabs. No
  TextMeshPro is used anywhere in the project yet.
- Motifs and strip glyphs are flat coloured shapes, not artwork.
- Gear uses a list + details composition without a ScrollRect (the acquired set is small).
- No animation. Motion was deliberately deferred rather than half-built.

## 7. Empty-state copy (provisional, pending approval)

| Tab | Title | Body |
|---|---|---|
| Gear (zero acquired) | Nothing carried yet | Gear you recover on your travels is kept here, ready to hand. |
| Tools | The workbench is bare | Implements of the trade will hang here once you have some to hang. |
| Satchel | An empty satchel | Seeds, cuttings, and whatever else you gather along the way will settle in here. |
| Recipes | Nothing written down | What you learn to brew gets noted here, in your own hand. |
| Tasks | Nothing promised | Errands and obligations you take on will be kept here, so none of them slip. |
| Journal | Blank pages | What you notice about this place, and the people in it, collects here. |
| Map | Uncharted | The ground you cover will be drawn here as you learn its shape. |

## 8. Deferred, with the seam each one plugs into

| Tab | Missing dependency | Where it lands |
|---|---|---|
| Tools | Product meaning; identity; ownership/equip rules | A Tools domain owner + focused presenter replacing `GameplayMenuEmptyTabView` |
| Satchel | Inventory domain, item definitions, quantities, categories, save target | A Satchel state SO implementing `ISaveTarget`; the tab renders a snapshot |
| Recipes | Recipe catalogue, discovery owner/events/save | A recipe-knowledge owner. Stations own execution; the tab is knowledge-only |
| Tasks | Task contract, stable IDs, progress events, save owner | A task/quest progression state owner |
| Journal | Approved content categories, entries, discovery owner | A Journal/discovery state owner |
| Map | Room presentation catalogue keyed by stable `roomId`, visited-room snapshot API, current-room resolver, authored geometry, pan/zoom, markers | A separately approved Map FeatureSpec |

**Map boundary.** `WorldStateRegistry` owns visitation facts (`visitedRoomIds`, `IsRoomVisited`)
and `RoomVisitReporter` marks one authored stable `roomId` per scene, but there is no visited-room
enumeration, no room-to-map catalogue, no current-room resolver, and no authored geometry. Unity
scene names and `UnderbrewWorldGraph` editor coordinates are **not** player-map identity or
geometry and must never be used as such. Quick Map (hold) and the double-tap Full Map shortcut are
also deferred; keyboard `Tab` stays reserved for them.

## 9. Validation

`Tools/Project/Validate UI Foundation` covers: exactly one Gameplay Menu root under
`MenuRoot/RootInterfaceLayer`; `gameplayMenuRootBehaviour` assigned and implementing
`IUIFlowRootScreen`; visual root inactive by default; exactly seven registrations in the fixed
order with no duplicates; every view implementing `IGameplayMenuTab`; every tab button present and
interactable; authored empty-state title/body free of developer wording; Gear catalogue and
production `PlayerAbilityState` assigned; all five required action references resolving to their
exact map/action; no gameplay-owner reference on any tab presenter; no Sandbox leakage into
production; and the Sandbox Gear tab using runtime fixtures rather than production assets.

Automated coverage:

- `GameplayMenuScreenTests` (EditMode) — order, memory, cycling, selection, Back, diagnostics.
- `GearScreenTests` (EditMode) — filtering, empty state, stable keys, subscription lifecycle,
  read-only ownership.
- `GameplayMenuProductionAssetTests` (EditMode) — the same contract asserted against the shipped
  prefabs, Input Actions, and catalogue.
- `GameplayMenuPlayModeTests` (PlayMode) — the shipped prefab driven by real Input System events.
