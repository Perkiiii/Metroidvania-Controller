# Package A2 — Full Gameplay Menu Interface Implementation Plan

> **Historical implementation record.**
> Package A2 was implemented with seven top-level Gameplay Menu tabs.
> That product decision was superseded on 2026-07-29 by the post-A2
> five-tab information-architecture correction documented in
> `Docs/FeatureSpecs/GameplayMenu.md`.

**Plan date:** 2026-07-27  
**Repository audited:** `E:\GameDev\Projects\Final_Project\Metroidvania Controller`  
**Status:** Implementation-ready plan; Package A2 is not implemented by this document.  
**Supersedes for Package A2:** the earlier Gear-only/hidden-tab production recommendation. All
seven confirmed tabs are visible and reachable in production.

---

## A. Executive summary

Package A2 delivers the persistent, pausing Gameplay Menu opened by `System/GameplayMenu` and its
seven fixed tabs, in this exact order:

1. Gear
2. Tools
3. Satchel
4. Recipes
5. Tasks
6. Journal
7. Map

The package extends the Package A1 `UIFlowController`; it does not replace it. The Gameplay Menu is
the second registered `IUIFlowRootScreen`, pauses only through `GameManager.Pause()`, uses the
existing persistent `EventSystem` and `UI` action map, and preserves A1 transition blocking,
same-frame Pause/Cancel arbitration, input suspension, held-command disarming, and continuous
movement resume policy.

Only Gear can become data-functional immediately. Its authoritative source is
`PlayerAbilityState.IsUnlocked(AbilityId)`, read-only. Production Gear is nevertheless blocked for
valid new-game verification until the still-present ability-default mismatch is corrected: code and
fresh-save defaults currently unlock Dash and Wall Cling, while the mutable production asset also
has Double Jump and Bind unlocked. That correction is Stage 0 and belongs to the ability/save
foundation, not the Gear view.

Tools, Satchel, Recipes, Tasks, and Journal have no authoritative gameplay owner in the current
repository. Map has persisted room-visitation facts and stable authored room IDs, but lacks an
approved map model, room-ID-to-map presentation catalogue, current-room resolution, authored player
map geometry, pan/zoom rules, and discovery snapshot API. A2 therefore implements all six of those
tabs and Map as polished, intentional, navigable empty states. They are not errors, development
messages, fake inventories, fake quests, or fake maps. Their focused tab components are ready to
bind to future domain owners without making the UI own those domains.

Recommended sequence:

- **Stage 0 — ability/save prerequisite:** make all eight true-new-game ability defaults locked,
  preserve explicit values from existing saves, and verify the mutable asset and tests.
- **Stage 1 — seven-tab foundation:** build `GameplayMenuScreen`, the narrow tab contract, seven
  authored registrations, input/navigation, runtime-only memory, and honest empty states.
- **Stage 2 — first functional integration:** add Gear display definitions/catalogue and read-only
  `PlayerAbilityState` refresh/subscription behavior. Do not add speculative owners for other tabs.
- **Stage 3 — production integration and validation:** wire `_GameCameras.prefab`, the Input Action
  asset and durable references, the current UISandbox path, validators, automated regression,
  Boot-path/manual checks, and documentation.

Main risks are destabilising A1 input arbitration, allowing Sandbox fixture data into production,
mistaking mutable ability values for design defaults, turning tab presentation into gameplay
ownership, and letting the visible Map tab expand into an unapproved full map/Quick Map project.

Main approvals are Gear identities/content, tab wraparound and bindings, controller Gameplay Menu
binding, layout/HUD treatment, empty-state copy, each deferred tab's eventual product meaning, and
whether a functional first-pass Map becomes a separately approved follow-up rather than part of A2.

---

## B. Repository state

### Audited Git state

| Item | Verified value |
|---|---|
| Branch | `main` |
| HEAD | `1e233fbf40d3e8796f778c5957661e9b79734d34` |
| HEAD subject | `feat(ui): implement scene transition rejection while paused and enhance input arbitration` |
| Reported older baseline | `38244ec fix(ui): complete Package A1 correction and validation pass` |
| Working tree | Dirty |

Exact `git status --short` before this plan was created:

```text
 D Assets/_Project/Scenes/Development.meta
 D Assets/_Project/Scenes/Development/UISandbox.unity
 D Assets/_Project/Scenes/Development/UISandbox.unity.meta
?? Assets/_Project/Scenes/UISandbox.unity
?? Assets/_Project/Scenes/UISandbox.unity.meta
?? Assets/_Recovery.meta
```

The tracked diff is the deletion side of a local UISandbox relocation. The untracked
`Assets/_Project/Scenes/UISandbox.unity` has the same scene GUID as the deleted tracked scene and is
the scene currently open in Unity. `Assets/_Recovery.meta` accompanies an untracked recovery
folder. These are user-owned changes and must not be reset, cleaned, stashed, restored, overwritten,
or silently included in unrelated work.

### Package A1 hardening

The A1 hardening listed in the request is present in the current **committed** HEAD, not as a local
uncommitted source/doc diff. HEAD adds paused-transition rejection, final same-frame input
arbitration, `UIFlowTestRootScreen`, three real-input PlayMode tests, camera lifecycle regression
coverage, and matching documentation. The current authoritative UI documents reflect that
committed hardening.

The local UISandbox relocation is not reflected consistently:

- `Docs/ImplementationPlan.md`, `Docs/FeatureSpecs/UISandbox.md`, and related UI docs still name
  `Assets/_Project/Scenes/Development/UISandbox.unity`.
- `UIFoundationValidator.SandboxScenePath` still uses that old path.
- The live, clean Unity scene is `Assets/_Project/Scenes/UISandbox.unity`, build index `-1`.

This path disagreement is a concrete Stage 3 integration issue. Before editing, the implementer must
confirm that the local move is intentional. If it is, update consumers to the new path without
recreating or overwriting the scene. If it is not, stop and ask Sam; do not decide by moving files.

### Unity MCP and serialized inspection

Unity MCP was available and connected to the correct project. Read-only queries confirmed:

- Active scene: `UISandbox`, path `Assets/_Project/Scenes/UISandbox.unity`.
- Scene is loaded, not dirty, and excluded from Build Settings (`buildIndex = -1`).
- It has one local `EventSystem`/`InputSystemUIInputModule`, a `SandboxCanvas`, nested shared Pause
  and Confirmation prefabs, `RootInterfaceLayer`, `ModalLayer`, HUD fixtures, aspect/safe-area
  fixtures, and `UISandboxController`.
- The nested Pause `Panel` and Confirmation `Panel` are inactive by default.

Production prefab details were inspected from serialized YAML rather than changing the active
prefab stage. No Unity scene/prefab was loaded, saved, or modified for this audit.

### Ability-default prerequisite status

The mismatch still exists:

- `PlayerAbilityState` field initializers: Dash and Wall Cling are `true`.
- `PlayerAbilityState.ResetToDefaults()`: Dash and Wall Cling become `true`.
- `AbilitySaveData` defaults: Dash and Wall Cling are `true`.
- `SaveManager.CreateFreshSave()` constructs `new SaveData()`, whose `abilities` is a new
  `AbilitySaveData`, migrates it, applies it, and saves it.
- `PlayerAbilityState.asset`: Dash, Wall Cling, Double Jump, and Bind are currently `true`.
- Intended true-new-game state: all eight flags locked.

`SaveDataMigrator.CurrentSaveVersion` is 4 and already creates missing ability data through
`new AbilitySaveData()`. Changing boolean defaults alone does not inherently require a schema
version bump. Existing saves containing explicit booleans must keep those values. Missing/null
ability data must receive the new all-locked default. Verify these semantics with tests before
deciding whether migration code needs any change.

No Unity compilation, tests, validator command, or manual playtest was run for this planning task.

---

## C. Verified current implementation

### A1 root flow

`Assets/_Project/Scripts/UI/Flow/UIFlowController.cs` is the single persistent pausing-root
coordinator.

- `UIRootKind` contains `Pause` and `GameplayMenu`.
- `UIFlowState` guards `Closed`, `Opening`, `Open`, and `Closing`.
- `pauseRootBehaviour` is assigned and resolved as `IUIFlowRootScreen`.
- `gameplayMenuRootBehaviour` is serialized but currently null in `_GameCameras.prefab`.
- `ConfigureRoots(IUIFlowRootScreen pause, IUIFlowRootScreen gameplayMenu)` is the test/Sandbox
  seam.
- Root `CloseRequested` events route back through `RequestCloseActiveRoot()`.
- The controller enables the `System` map once, enables the `UI` map only while a root is open,
  and never gives System-map ownership to a scene Hero.
- Gameplay Menu input already routes through `HandleGameplayMenuPressed()`. With no registered
  screen it rejects safely without pausing, changing selection, or opening a blank interface.
- A stable open Gameplay Menu toggles closed when Gameplay Menu is freshly pressed again.
- A request for the other root while one root is open fails `CanAcceptNewRootRequest()`; roots do
  not switch directly.
- `OpenRoot` suspends gameplay input through `HeroController`, calls `GameManager.Pause()`, enables
  UI navigation, calls the screen's `Show()`, then selects `screen.FirstSelection`.
- Close hides the screen, clears EventSystem selection, clears transient Hero input, disables the
  UI map, begins Hero input resume/rearming, and releases only UI-owned pause.
- `OnDestroy()` releases a legitimately owned open root unless application shutdown is already in
  progress. A duplicate singleton self-destruct does not affect the real controller.

`IUIFlowRootScreen` already provides exactly the root behavior A2 needs:

```csharp
event Action CloseRequested;
void Show();
void Hide();
Selectable FirstSelection { get; }
bool HandleBackInternally();
bool HasOpenModal { get; }
```

No additive change to this interface is required for A2. Tab lifecycle belongs inside
`GameplayMenuScreen`; placing tab APIs on the shared root interface would pollute Pause with
Gameplay-Menu-specific concerns.

### Back, modal, and selection behavior

- `UIFlowController.Update()` snapshots Pause, Gameplay Menu, and Cancel edges before any map/root
  change.
- With no root or Pause active, a Pause edge owns Escape for that frame and suppresses the
  resynchronised Cancel edge.
- With Gameplay Menu active, Pause is rejected and Cancel still enters Gameplay Menu Back routing.
- `HandleCancelPressed()` asks the active root to handle Back internally, then closes the root only
  if it returns `false`.
- `PauseMenuScreen.HandleBackInternally()` closes its Confirmation modal first.
- `ConfirmationModal` selects Cancel first and restores the invoker, or a valid parent fallback.
- `UISelectionUtility` refuses inactive/non-interactable selections and supports
  preferred-or-fallback restoration.
- `PauseMenuScreen.FirstSelection` is its Continue button. `ConfirmationModal.FirstSelection` is
  Cancel when assigned, otherwise Confirm. The persistent EventSystem itself has no authored
  `m_FirstSelected`; roots establish selection when they open.

`GameplayMenuScreen` must own active-tab selection, per-tab remembered selection, nested-page Back,
and tab switching. `UIFlowController` must continue to own only root availability, pause, map mode,
Hero suspension/resume, and root-level selection entry.

### Pause and input ownership

`GameManager.Pause()` sets `GameState.Paused`, writes `Time.timeScale = 0`, and adds its Hero control
lock. `Unpause()` restores `Playing`, `Time.timeScale = 1`, and removes that lock.
`GameManager.BeginSceneTransition(...)` rejects while paused. Views must never write time scale or
load a scene.

`HeroController` exposes only thin UI forwarding methods:

```csharp
public void SuspendGameplayInput();
public void ClearTransientGameplayInput();
public void BeginGameplayInputResume();
```

`HeroInputReader` owns action sampling, buffer clearing, physical-held detection, per-command
disarming, and rearming. Jump, Attack, Dash, Sprint, Interact, and Bind are suppressed when held
through resume until individually released; continuous Move resumes without neutral release.
Gameplay Menu views must not access `HeroInputReader`.

Both roots are rejected during transitions, before the first completed transition, during the
one-second unscaled post-transition lockout, outside `Playing`, during respawn/hazard recovery, or
while persistent health is depleted. Blocked requests are discarded, not queued.

### Input maps

`Assets/_Project/Input/InputSystem_Actions.inputactions` contains:

- `Player`: Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint, Dash, Bind.
- `UI`: Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel,
  TrackedDevicePosition, TrackedDeviceOrientation.
- `System`: Pause and GameplayMenu.

Current System bindings:

- Pause: Keyboard Escape; Gamepad Start.
- GameplayMenu: Keyboard I; no controller binding.

There are no `UI/PreviousTab` or `UI/NextTab` actions. Gameplay `Player/Previous` and `Player/Next`
must not be reused. Keyboard Tab must not be used; it remains reserved for future Quick Map.

The production and Sandbox `InputSystemUIInputModule` instances serialize the shared action asset
and all ten durable UI action-reference sub-assets. In contrast, `_GameCameras.prefab` currently
leaves `UIFlowController.pauseActionRef`, `gameplayMenuActionRef`, and `uiCancelActionRef` null; the
controller resolves those actions by map/name fallback. A2 should assign durable references to
those existing fields while retaining fallback compatibility, and assign durable new
PreviousTab/NextTab references directly to `GameplayMenuScreen`.

### Persistent prefab hierarchy

Serialized `_GameCameras.prefab` contains:

```text
_GameCameras (persistent)
├── MainCamera
├── HUDCamera (URP Overlay)
├── HUDRoot
│   └── HUD Canvas (Screen Space - Camera, HUDCamera, sorting 100)
├── MenuRoot (UIFlowController)
│   ├── EventSystem (InputSystemUIInputModule)
│   ├── RootInterfaceLayer
│   │   └── PauseMenuScreen prefab instance
│   └── ModalLayer
│       └── ConfirmationModal prefab instance
└── FadeCanvas (Screen Space - Overlay, sorting 999)
```

`RootInterfaceLayer` is Screen Space - Camera on `HUDCamera`, sorting 150, with a
`GraphicRaycaster`. `ModalLayer` uses the same camera, sorting 180, with a `GraphicRaycaster`.
Semantic order is Fade > Modal > Root > HUD. The Gameplay Menu must be a sibling of Pause under
`RootInterfaceLayer`; it must not move under `HUDRoot` or `ModalLayer`.

The Pause prefab root is active but its `Panel`/visual root is inactive by default. Confirmation
uses the same pattern. Gameplay Menu should follow it so a hidden root cannot block HUD raycasts.
No persistent menu reference to a gameplay-scene object was observed in serialized YAML; production
references resolve within `_GameCameras.prefab` or to persistent project assets. A2 must preserve
that property.

### Sandbox

The current local Sandbox is `Assets/_Project/Scenes/UISandbox.unity`, not the still-documented
Development path. It has:

- A local camera and exactly one local EventSystem/input module.
- `SandboxCanvas` with `AspectFrame`, `SafeAreaGuide`, isolated HUD preview, and fixture controls.
- `RootInterfaceLayer` with a nested shared Pause prefab.
- `ModalLayer` with a nested shared Confirmation prefab and Sandbox-only Options panel.
- Runtime-created isolated health/resource/boss fixture state.
- No `GameManager`/Boot production flow and no Build Settings entry.

No Gameplay Menu or Gear fixtures exist.

### Gameplay and persistence state

- `AbilityId` has eight stable enum values: Dash, WallCling, Sprint, WallLatch, DoubleJump,
  DriftCloak, SpiritCast, and Bind.
- `PlayerAbilityState` is the sole permanent ability-unlock owner, implements `ISaveTarget`, and
  fires `AbilityChanged(AbilityId, bool)` through `SetUnlocked`.
- Gear can read this state; it must never call `Unlock`, `Lock`, `SetUnlocked`, or
  `ResetToDefaults`.
- No inventory, item definition, quantity, recipe, crafting, brewing, cooking, task, quest,
  objective, journal, lore, bestiary, character-discovery, or player-map presentation owner exists
  in project scripts or ScriptableObjects.
- `WorldStateRegistry` persists `visitedRoomIds`, collected pickup IDs, defeated encounters, and
  object states. It explicitly excludes quest, dialogue/NPC, inventory, and currency ownership.
- `RoomVisitReporter` marks one authored stable `roomId` visited in each gameplay scene. Current IDs
  are `room_sample_01` through `room_sample_04`.
- `WorldStateRegistry` exposes `IsRoomVisited(roomId)` and keyed notifications, but no visited-room
  enumeration/snapshot suitable for a map UI.
- `UnderbrewWorldGraph.asset` contains scene nodes, editor positions, port GUIDs, and connections.
  Its scene names/build indexes and editor graph coordinates are not approved player-map identity
  or geometry. There is no mapping from stable `roomId` to a player-facing map definition.

### Existing tests and validator

Existing EditMode coverage includes `UIFlowControllerTests`, `PauseMenuScreenTests`,
`ConfirmationModalTests`, `UISandboxControllerFixtureTests`, HUD tests, and persistent-state/world
visitation tests. Existing PlayMode UI coverage includes `HeroInputSuspensionPlayModeTests` and
`UIFlowInputArbitrationPlayModeTests`. The latter sends real Input System events through the player
loop for three Escape arbitration scenarios.

Documentation records prior successful runs, but the latest hardening notes that the available
automation environment could not advance real Input System PlayMode events for a fresh PlayMode
claim. A2 must use the real Unity PlayMode runner with the existing test asmdef convention and
report discovery/execution separately from direct handler tests.

`UIFoundationValidator` currently validates persistent EventSystem count, module action references,
Canvas/raycaster/layering, hidden panels, no competing gameplay-scene EventSystem, Sandbox
isolation, Sandbox Build Settings exclusion, and absence of Sandbox-only components in production.
Its hard-coded Sandbox path is stale against the local working tree. It currently warns if a
Gameplay Menu root is assigned because that was the A1 expectation; A2 must replace that warning
with required A2 validation.

---

## D. Product-decision corrections

The following current sections retain the superseded Gear-only/hidden-tab assumption:

| Document | Exact section/statement to reconcile during implementation |
|---|---|
| `Docs/ImplementationPlan.md` | `Authoritative UI Roadmap` → `Package A2 — Gameplay Menu and Gear`: “Production tab filtering: Gear only”; `Deferred UI and adjacent systems`: production Tools/Satchel/Recipes/Tasks/Journal/Map deferred. |
| `Docs/ImplementationPlans/UIImplementationPlan.md` | `8. Gameplay Menu shell`: unavailable tabs hidden and explicit Gear-only recommendation; `Phase 4`; `Recommended first implementation packages` → `Package A2`; tests for hidden unavailable tabs. |
| `Docs/FeatureSpecs/PauseAndMenuFlow.md` | `Gameplay Menu`: “Package A2 exposes only production-backed Gear” and hides ownerless tabs; `Quick Map relationship` wording that Package A does not implement Map must distinguish visible full-map tab shell from functional map data. |
| `Docs/FeatureSpecs/Gear.md` | `Gameplay Menu integration`: Gear is the only production-backed A2 tab and siblings remain hidden; `Deferred work`: production sibling tabs. |
| `Docs/FeatureSpecs/UISandbox.md` | `Deferred-system fixtures`, `Responsibility split`, `Package A1 relationship`, `Explicit exclusions`, and validator gaps treat sibling tabs as fixture-only/non-production. |
| `Docs/FeatureSpecs/UIArchitecture.md` | `Current implemented state`, `Lifetime boundaries`, `Quick Map versus Full Map`, `Sandbox and Gear relationships`, and open decisions describe Gameplay Menu/Gear only and functional Map as deferred. The functional Map deferral remains valid; the visible Map tab deferral does not. |
| `Docs/Architecture.md` | `UI / HUD` and `Planned Systems`/system-status row describe Gameplay Menu, Gear, tab memory, and Full Map collectively as deferred. |

The new confirmed direction is:

```text
All seven tabs are visible and reachable in the production Gameplay Menu.
```

Visibility does not claim data functionality. Gear has a current authoritative state integration
after Stage 0. The other tabs use player-facing empty states until their gameplay owners and content
contracts exist. Functional Map, Quick Map, inventory quantities, recipe discovery, tasks, and
journal discovery remain future gameplay-system work.

---

## E. Tab ownership matrix

| Tab | Player-facing purpose | Current authoritative owner | Existing state/data | Existing persistence | Existing producer | A2 presentation status | Empty state | Missing dependency | Future owner | Risk |
|---|---|---|---|---|---|---|---|---|---|---|
| Gear | Physical permanent progression possessions | `PlayerAbilityState` for ability-backed ownership | Eight `AbilityId` flags; no approved display catalogue | `AbilitySaveData` via `PlayerAbilityState` | `AbilityPickup`/progression unlocks | Functional after Stage 0 and approved definitions | Required for true new game and zero approved mappings | Correct defaults; approved physical identities/copy/art | Existing state owner; future non-ability Gear needs its own domain owner | High: UI could mutate abilities or expose enum values as content |
| Tools | Meaning not yet confirmed | None | No tool definitions, ownership, equip/upgrade state, or events | None | None | Real tab shell only | Required, intentional | Product meaning; identity; ownership; interaction/equip rules | Future dedicated Tools domain | High: easy to invent equipment or conflate farming/combat tools |
| Satchel | Future carried quantities such as seeds, crops, ingredients, drops, materials, consumables | None | No item IDs/definitions/quantity owner/category contract | None | No general item pickup/quantity producer | Real tab shell with category-ready composition | Required; no totals/capacity | Inventory domain, item definitions, quantities/events/save target, approved categories | Future Satchel/inventory state SO implementing `ISaveTarget` | High: UI could become inventory/save owner or merge with Gear |
| Recipes | Portable discovered knowledge; not remote production | None | No recipe IDs/definitions/discovery state/stations | None | None | Real tab shell | Required | Recipe catalogue, discovery owner/events/save; approved categories/details | Future recipe-knowledge state; stations remain separate | High: accidental crafting buttons or duplicated Satchel quantities |
| Tasks | Active/completed commitments and objectives, once designed | None | No task IDs, objective/progress/completion model | None | None | Real tab shell | Required | Task contract, stable IDs, progress events, dialogue/world dependencies, save owner | Future task/quest progression state | High: speculative quest framework or UI-owned progress |
| Journal | Discovered reference/lore content, exact categories undecided | None | No lore/character/bestiary/location/note discovery owner | None | None | Real flexible tab shell | Required | Approved content categories, entries, discovery/events/save | Future Journal/discovery state | High: invented categories or duplication of Tasks/Recipes/Map |
| Map | Pausing full-map presentation within Gameplay Menu | No player-map owner. `WorldStateRegistry` owns visitation facts only | Stable room IDs, four visited-room facts, WGE scene graph/ports; no map model/geometry/current-room mapping | `WorldSaveData.visitedRoomIds` | `RoomVisitReporter` | Visible Map shell; functional map staged separately | Required until map model approved | Room presentation catalogue keyed by stable room ID, discovery snapshot, current-room seam, map geometry/links, pan/zoom rules | Future Map model/presenter reading world facts; WGE remains graph authoring adapter | Very high: scene-name identity, WGE editor coordinates, fake map art, or A2 scope explosion |

---

## F. Recommended architecture

```text
System/GameplayMenu
    -> UIFlowController
        -> existing availability / pause / Hero input suspension / UI map
        -> GameplayMenuScreen : IUIFlowRootScreen
            -> GameplayMenuTabRegistration[7] (fixed authored order)
            -> tab bar + global Close control
            -> IGameplayMenuTab lifecycle
                -> GearScreen
                    -> GearDisplayCatalog
                    -> PlayerAbilityState (read-only)
                    -> GearEntryView / GearDetailsPanel
                -> ToolsTabView -> intentional empty state
                -> SatchelTabView -> intentional empty state
                -> RecipesTabView -> intentional empty state
                -> TasksTabView -> intentional empty state
                -> JournalTabView -> intentional empty state
                -> MapTabView -> intentional empty state
            -> persistent EventSystem selection
            -> UI/PreviousTab and UI/NextTab
            -> runtime-only last-tab + per-tab selection memory
```

Ownership:

- `UIFlowController` owns root flow, not tab content or remembered per-tab gameplay data.
- Persistent `GameplayMenuScreen` owns runtime-only active/last tab and selection memory because
  those are presentation state specific to this root and naturally survive room transitions.
- Each tab component owns its presentation lifecycle only.
- Gear definitions own display metadata only; `PlayerAbilityState` owns acquisition.
- Empty tabs own no gameplay state. Future integrations replace/extend their focused tab presenter
  against a real domain API rather than changing the root contract.
- `GameManager` remains pause/time authority.
- `HeroInputReader`, `SaveManager`, scene loading, HUD state, camera, and WGE runtime remain outside
  the tab hierarchy.

Non-owners:

- No second flow manager, EventSystem, pause system, generic `UIManager`, generic inventory
  framework, modal stack, or window framework.
- No per-tab `Time.timeScale`, Hero input, save, or scene-loading access.
- No ScriptableObject owner for an empty tab merely to make it appear populated.

---

## G. Gameplay Menu public API plan

### Root and flow APIs

| Proposed signature | File / owner | Caller | Lifecycle and need | Alternative / compatibility |
|---|---|---|---|---|
| `public event Action CloseRequested;` | `GameplayMenuScreen.cs` / screen | Close button; `UIFlowController` subscribes | Raised only for a full root close. Back itself remains routed through `UIFlowController`. | Matches existing interface and Pause pattern; no compatibility impact. |
| `public void Show();` | `GameplayMenuScreen` | `UIFlowController.OpenRoot` | Activate visual root, resolve requested/remembered/default tab, subscribe tab actions, show/refresh active tab, establish valid `FirstSelection`. | Required by existing interface; no interface change. |
| `public void Hide();` | `GameplayMenuScreen` | `UIFlowController` | Snapshot active selection, unsubscribe actions, hide active tab/child, deactivate visuals. Must be idempotent. | Required by existing interface. |
| `public Selectable FirstSelection { get; }` | `GameplayMenuScreen` | `UIFlowController` | Returns active tab's restored/first valid selection, then active tab button, then global Close fallback. Valid after `Show()`. | Do not make UIFlow inspect tab registrations. |
| `public bool HandleBackInternally();` | `GameplayMenuScreen` | `UIFlowController` | Delegates to active tab/nested child. Returns false at bare tab root so UIFlow closes root. | Existing interface already models this. |
| `public bool HasOpenModal { get; }` | `GameplayMenuScreen` | `UIFlowController` | True only if a real Gameplay Menu-owned modal/child blocks root toggle. Initial A2 can return active tab's nested-layer state; empty tabs return false. | Do not introduce a generic modal stack. |
| `public bool TryOpenGameplayMenu(GameplayMenuTabId? requestedTab = null);` | additive method in `UIFlowController.cs` | System input handler; future Full Map owner | Atomic root request. Checks the same A1 availability predicate; rejected calls return false and store no pending request. If accepted, passes the optional route to the concrete screen immediately before `OpenRoot`. | Preferred over a future caller directly invoking `GameplayMenuScreen.Show()` and bypassing pause. Existing input behavior remains; method is additive. If team rejects A2-time future routing, defer this method but retain `GameplayMenuScreen.SelectTab`; do not add queued state. |

`IUIFlowRootScreen` is unchanged. `UIFlowController` may validate/cast its Gameplay Menu root to
`GameplayMenuScreen` only for the optional initial-tab route; Pause remains unaware of tab types.

### Tab APIs

| Proposed signature | File / owner | Caller | Lifecycle and need | Alternative / compatibility |
|---|---|---|---|---|
| `public GameplayMenuTabId ActiveTabId { get; }` | `GameplayMenuScreen` | Tests, diagnostics, future prompt view | Read-only presentation state. Valid while shown; retained last value while hidden. | Avoid exposing mutable registration lists. |
| `public bool SelectTab(GameplayMenuTabId tabId);` | `GameplayMenuScreen` | Tab-button listener; tests | Capture old selection, hide old tab, show new tab, update active visuals/runtime last tab, restore target selection. Returns false and logs concise diagnostics for an invalid registration. | Direct tab selection should not be implemented in each tab. |
| `public bool SelectPreviousTab();` | `GameplayMenuScreen` | `UI/PreviousTab.performed` | Select previous fixed registration according to approved boundary/wrap policy. Does nothing when hidden or nested modal blocks switching. | Do not reuse `Player/Previous`. |
| `public bool SelectNextTab();` | `GameplayMenuScreen` | `UI/NextTab.performed` | Symmetric next-tab behavior. | Do not use EventSystem horizontal movement as shoulder input. |
| `public void RequestClose();` | `GameplayMenuScreen` | Authored global Close button | Raises `CloseRequested`; never unpauses directly. | Avoid wiring a view to `UIFlowController.Instance`. |

### Internal immediate-route seam

Use an internal, one-shot method such as:

```csharp
internal void PrepareImmediateOpen(GameplayMenuTabId? requestedTab);
```

`UIFlowController.TryOpenGameplayMenu` calls it only after the request is accepted and immediately
before `OpenRoot`. `Show()` consumes and clears it. This is not a queued menu request and cannot
survive a blocked transition/death/recovery window. An invalid requested tab falls back to Gear.

### Runtime memory and selection restoration

- `GameplayMenuScreen` stores `GameplayMenuTabId lastValidTab = Gear` as a normal runtime field.
- It is not serialized to disk, not static, not in `PlayerPrefs`, not in `SaveData`, and not in a
  settings profile.
- Store per-tab remembered `Selectable` references only for the current persistent screen instance.
  On switch/hide, accept the current EventSystem selection only if it belongs to that tab's authored
  content or tab button.
- On restore, use `UISelectionUtility.SelectPreferredOrFallback` semantics:
  remembered valid selection -> tab `FirstSelection` -> active tab button -> global Close.
- Gear additionally retains its selected **stable key**, because dynamic entries can be rebuilt and
  Unity object references can become invalid. The root remembers the resulting active Selectable;
  Gear owns the key.

### Empty states and diagnostics

- Empty tabs return no fake content selection. Their `FirstSelection` may resolve to the global
  Close control through registration fallback; tab buttons remain directly reachable.
- Missing, duplicate, out-of-order, null-view, null-button, invalid-first-selection, or non-castable
  registrations produce concise development errors with tab ID and screen context.
- Production initialization should fail closed to a valid Gear/Close selection, not throw repeatedly
  per frame or hide the entire root.
- No public diagnostics framework is needed. Validator errors plus one initialization log are
  sufficient.

---

## H. Shared tab contract

Use this narrow runtime contract:

```csharp
public interface IGameplayMenuTab
{
    Selectable FirstSelection { get; }
    void Show();
    void Hide();
    bool HandleBackInternally();
    bool HasOpenModal { get; }
}
```

`GameplayMenuTabId` belongs to the authored `GameplayMenuTabRegistration`, not the view contract.
That avoids duplicating identity in both prefab registration and component state. The registration
contains:

```csharp
[Serializable]
public sealed class GameplayMenuTabRegistration
{
    [SerializeField] private GameplayMenuTabId id;
    [SerializeField] private GameplayMenuTabButton tabButton;
    [SerializeField] private MonoBehaviour tabViewBehaviour; // must implement IGameplayMenuTab
    [SerializeField] private Selectable firstSelectionFallback;
}
```

The fixed array/list is authored on `GameplayMenuScreen` and validated as exactly Gear, Tools,
Satchel, Recipes, Tasks, Journal, Map in that order. No availability flag exists in production A2,
because confirmed tabs may not be hidden. Missing content is represented by the view's empty state,
not by registration filtering.

The contract contains only lifecycle, Back/modal routing, and first selection. It deliberately does
not expose gameplay owners, save APIs, item/recipe/task/map data, input-reader access, generic
details, generic inventory entries, or generic modal/window behavior.

Implementation arrangement:

- `GameplayMenuScreen.prefab` owns the serialized seven registrations and tab bar.
- Each tab is a dedicated nested prefab child with a focused component.
- Six empty-state tabs may compose one reusable presentation-only `GameplayMenuEmptyStatePanel`
  prefab/component, but retain dedicated `ToolsTabView`, `SatchelTabView`, `RecipesTabView`,
  `TasksTabView`, `JournalTabView`, and `MapTabView` owners.
- Gear has its own data/view components and does not inherit from an inventory base class.
- Sandbox nests the same production Gameplay Menu/tab prefabs. Fixture adapters are Sandbox-only
  siblings/components and are never serialized into production registrations.

This is a seven-tab lifecycle contract, not a generic window framework.

---

## I. Individual tab plans

### Per-tab artifact and wiring summary

| Tab | Runtime presentation owner | Editor script | Prefab | ScriptableObject/data asset | A2 view interface | Required Inspector assignments |
|---|---|---|---|---|---|---|
| Gear | `GearScreen`, `GearEntryView`, `GearDetailsPanel` | None new; shared validator extension | `GearTab.prefab`, `GearEntryView.prefab` | `GearDisplayCatalog.asset` plus approved `GearDisplayDefinition` assets | `IGameplayMenuTab`; direct read-only `PlayerAbilityState` (no generic owner interface) | State, catalog, entry prefab/container, details, empty root, first/global fallback |
| Tools | `ToolsTabView` | None | `ToolsTab.prefab` | None | `IGameplayMenuTab` only | Empty panel, local root, optional first selection; registration supplies global fallback |
| Satchel | `SatchelTabView` | None | `SatchelTab.prefab` | None in production A2 | `IGameplayMenuTab` only; future domain interface deferred | Empty panel/root/fallback; no quantity or registry reference |
| Recipes | `RecipesTabView` | None | `RecipesTab.prefab` | None in production A2 | `IGameplayMenuTab` only; future recipe read model deferred | Empty panel/root/fallback; no crafting callback |
| Tasks | `TasksTabView` | None | `TasksTab.prefab` | None in production A2 | `IGameplayMenuTab` only; future task read model deferred | Empty panel/root/fallback; no world/save reference |
| Journal | `JournalTabView` | None | `JournalTab.prefab` | None in production A2 | `IGameplayMenuTab` only; future discovery read model deferred | Empty panel/root/fallback; no speculative category asset |
| Map | `MapTabView` | None | `MapTab.prefab` | None in production A2 | `IGameplayMenuTab` only; future Map model deferred | Empty panel/root/fallback; no WGE graph, scene-name, or registry reference on the view |

Every prefab is nested and registered by `GameplayMenuScreen.prefab`. Empty-tab presentation copy
and art are serialized presentation references only. No tab requires a custom Inspector in A2.
Editor validation is centralized in `UIFoundationValidator`.

### Gear

**Purpose:** acquired physical permanent progression possessions. Not a skill tree, equipment
screen, purchasing screen, Satchel, recipe list, or ability editor.

**Current implementation:** no Gear runtime scripts, catalogue, definitions, screen, prefabs, or
tests exist. `PlayerAbilityState` and its save integration exist.

**Proposed scripts:**

- `UI/Gear/GearDisplayDefinition.cs` — display-only ScriptableObject; stable key, exactly one
  required `AbilityId`, approved physical content, optional Input Action/control-hint metadata.
- `UI/Gear/GearDisplayCatalog.cs` — ordered definitions, stable-key/ability lookup, deterministic
  validation.
- `UI/Gear/GearScreen.cs` — `IGameplayMenuTab`; filters definitions through
  `PlayerAbilityState.IsUnlocked`, builds entries, subscribes only while shown, and owns stable-key
  selection.
- `UI/Gear/GearEntryView.cs` — one visible acquired entry; emits selection/click events only.
- `UI/Gear/GearDetailsPanel.cs` — renders approved display metadata; no tuning values.
- Optional `UI/GameplayMenu/InputBindingDisplay.cs` — narrow text binding resolver using
  `InputActionReference.GetBindingDisplayString()` with neutral text fallback. No glyph database.

**Prefab/assets:** `GearTab.prefab`, `GearEntryView.prefab`, one
`GearDisplayCatalog.asset`, and definition assets only for Sam-approved physical Gear identities.

**Data/save dependency:** direct serialized read-only reference to the production
`PlayerAbilityState.asset`; persistence remains owned by the existing save target. Definitions
contain no unlock/save/equip state.

**Empty state:** intentional true-new-game presentation, no unknown total/slots/silhouettes, with
valid global Close/tab navigation. Final wording/art requires approval.

**Selection/details:** stable display key, deterministic display order, retain key after refresh,
fall back to first visible entry, otherwise global Close. Details clear in empty state and never
show raw enum names/tuning.

**Lifecycle:** full refresh during initialize/Awake-safe setup and every `Show`; subscribe to
`AbilityChanged` after the open refresh; unsubscribe in `Hide` and `OnDestroy`; no Update polling.
An unlock while open refreshes neutrally. Save application/reopen is state restoration, never an
acquisition notification.

**Automated tests:** acquired-only filtering, zero entries, first unlock, multiple unlocks,
stable-key retention/fallback, reopen refresh, live `AbilityChanged`, no duplicate subscriptions,
missing unlocked definition log/omission, duplicate/null catalogue validation, and no calls to
mutating ability methods.

**Validator:** assigned state/catalogue; unique non-empty stable keys; unique ability mapping;
non-null definitions; deterministic order; only approved content assets; no Sandbox references.

**Editor wiring:** assign catalog/state/list container/entry prefab/details/empty panel/global
fallback; author only approved definitions; test zero/one/multiple with cloned Sandbox state.

**Deferred:** acquisition notifications, non-ability Gear ownership, equipment/loadout, final
glyph/localization/accessibility, and unapproved physical identities.

**Approval:** physical identity/name/category/art/copy for each mapped ability, especially Double
Jump; which other ability flags receive Gear definitions; final layout and empty copy.

### Tools

**Purpose:** not confirmed. Repository evidence does not establish combat, utility, farming,
equipable, or upgradeable Tools.

**Current implementation/owner/persistence/producer:** none.

**A2 scripts/prefab:** dedicated `ToolsTabView.cs` and `ToolsTab.prefab`, implementing the tab
contract and composing the shared empty-state panel. Do not introduce a Tools data interface before
the product meaning is approved.

**Empty/selection/details:** intentional player-facing absence, global Close as first fallback,
tab bar remains reachable. No item cards, loadout slots, levels, capacity, or details panel data.

**Lifecycle:** Show/Hide only; no subscriptions or polling.

**Tests/validator:** real registration and first-selection fallback; visible/reachable; no
production fixture/data reference; Back closes root; missing view is diagnosed.

**Editor wiring:** authored empty-state title/body/art placeholder, Tools tab button, navigation,
and screen registration.

**Deferred/integration seam:** after approval, add a Tools-domain owner and a narrow read model/event
contract defined by that owner; replace the tab's internal empty body without changing
`IGameplayMenuTab`.

**Approval required:** what Tools are, whether they are owned/equipped/upgraded, stable identity,
categories, quantities/durability, gameplay producers, and save owner.

### Satchel

**Purpose:** distinct from Gear; future home for carried quantities such as seeds, crops,
ingredients, farming resources, enemy drops, processing/crafting materials, and consumables.
These categories are direction, not implemented contracts.

**Current implementation/owner/persistence/producer:** none. World collected-pickup IDs record
physical consumption and are not an inventory quantity owner.

**A2 scripts/prefab:** `SatchelTabView.cs` and `SatchelTab.prefab`. Author a category-ready visual
region, but show only one intentional empty state until categories and quantity owner are approved.
Do not create item IDs, quantity dictionaries, capacity, or a save target.

**Empty/selection/details:** no zero-count rows, fake quantities, mystery slots, totals, or capacity.
First fallback is global Close; category controls should not be interactive if no approved category
contract exists. A visual category treatment may be a non-interactive review mock in Sandbox only.

**Lifecycle:** no production subscriptions. Future owner should expose a snapshot plus change event;
the tab subscribes while shown and renders definition metadata separately from quantities.

**Tests:** production empty state; isolated fixture layout with explicit fake *test* definitions and
quantities kept outside production; selection/details retention as a future integration test.

**Validator:** no Sandbox fixture assets in production; no production Satchel registration may
reference `WorldStateRegistry` as a quantity owner.

**Editor wiring:** empty panel, tab button, registration, navigation. Sandbox fixture controls are
optional and clearly labelled non-production.

**Deferred:** item definitions/IDs, quantities, pickup rewards, categories/filters, details,
consumption, capacity, events, save target.

**Approval:** final categories, stacking/capacity policy, consumable behavior, stable IDs,
definition/state split, acquisition producers, save ownership.

### Recipes

**Purpose:** portable discovered knowledge. It does not execute crafting, cooking, brewing,
farming, or processing remotely.

**Current implementation/owner/persistence/producer:** none; no recipe/station system exists.

**A2 scripts/prefab:** `RecipesTabView.cs` and `RecipesTab.prefab`, with an intentional
discovery-empty state. No Craft/Brew/Cook button.

**Empty/selection/details:** no fake recipes, unknown totals, undiscovered silhouettes, ingredient
quantities, or station availability. Global Close/tab navigation remains valid.

**Future boundary:** a recipe-knowledge owner supplies stable recipe IDs/discovered set/events/save;
display definitions supply approved output/ingredient knowledge; Satchel remains the quantity
authority; stations own production execution.

**Lifecycle:** no A2 production subscription. Future tab subscribes to discovery changes only while
shown and refreshes snapshot on each open.

**Tests/validator:** shell visibility/navigation/Back; isolated discovered/undiscovered fixture
tests only when such fixtures are added; validator rejects production crafting actions and Sandbox
fixture references.

**Editor wiring:** empty state, registration, tab button, navigation; details region may be authored
as inactive/layout-ready presentation.

**Deferred:** recipe catalogue/discovery, categories/filters, ingredient display, station
integration, crafting execution.

**Approval:** recipe categories, discovery semantics, ingredient disclosure rules, station types,
save owner, and final empty copy.

### Tasks

**Purpose:** future active/completed commitments and objectives; exact quest model is absent.

**Current implementation/owner/persistence/producer:** none. Boss/world-state facts are not a task
system and must not be repurposed as one.

**A2 scripts/prefab:** `TasksTabView.cs` and `TasksTab.prefab`, with a focused empty state and
layout-ready active/details regions. Do not create task IDs or progress state in UI.

**Empty/selection/details:** no fake objectives, percentages, completion counts, or “failed to
load” language. Global Close/tab navigation is valid.

**Future boundary:** a task-domain save owner defines stable task identity, active/completed state,
objective snapshots, ordering, and change events. Dialogue, encounters, and world facts may produce
progress through that domain, never through the view.

**Lifecycle:** Show/Hide only in A2. Future refresh on open and subscribe while visible.

**Tests/validator:** visible registration, empty-state focus/Back, fixture isolation, no direct
`WorldStateRegistry` mutation or `SaveManager` reference.

**Editor wiring:** empty panel, button, registration, navigation.

**Deferred:** active/completed structure, objective display, tracking, dialogue/encounter adapters,
save persistence.

**Approval:** active/completed UX, tracking/pinning, failure/expiry, objective disclosure, stable
IDs, producers, save owner.

### Journal

**Purpose:** future discovered reference/lore content. Repository evidence does not approve
characters, bestiary, locations, documents, tutorials, notes, or other categories.

**Current implementation/owner/persistence/producer:** none.

**A2 scripts/prefab:** `JournalTabView.cs` and `JournalTab.prefab`, using a flexible two-region
shell only if it remains visually useful while empty. Do not serialize speculative category IDs.

**Empty/selection/details:** honest discovery-empty presentation, no category totals, silhouettes,
or fake lore. Global Close/tab navigation valid.

**Future boundary:** approved Journal definitions remain display/content data; a separate discovery
state/save owner exposes discovered stable IDs and events.

**Lifecycle/tests/validator/editor wiring:** same presentation-only pattern as other empty tabs;
validate no production fixture/discovery assets and test navigation/Back.

**Deferred:** all categories, entries, discovery producers, persistence, search/filter, bestiary
statistics.

**Approval:** Journal purpose/categories, relationship to NPCs/enemies/locations/tutorials,
discovery rules, ordering, save owner, content pipeline.

### Map

**Purpose:** pausing full-map tab inside the Gameplay Menu. Quick Map is not part of A2.

**Current implementation:** stable room IDs and persisted visitation exist; WGE stores
scene/port/connection graph data. No player-map model or view exists.

**A2 scripts/prefab:** `MapTabView.cs` and `MapTab.prefab` with a deliberate foundation/empty state.
Do not fake a map image or display WGE editor graph coordinates.

**Existing facts that may support a future map:** `RoomVisitReporter.roomId`,
`WorldStateRegistry.IsRoomVisited`, `WorldSaveData.visitedRoomIds`, and WGE port connections.
These facts are insufficient for a functional screen because no public snapshot, room presentation
catalogue, stable room-to-graph mapping, current-room identity, authored player map geometry,
discovery rules, pan/zoom policy, or marker model exists.

**Future functional architecture:** a separate approved Map FeatureSpec should define:

- `MapRoomDefinition` keyed by authored stable `roomId`, with player-facing geometry/position and
  explicit graph linkage where needed.
- A read-only Map model/presenter that combines definitions with visitation facts.
- A neutral visited-room snapshot/event seam from the world-state owner.
- A current-room resolver that uses authored identity, never Unity scene names.
- Pan, zoom, bounds, focus, selected-room details, and optional marker contracts.
- Full Map and later Quick Map sharing one model/presentation source without sharing root behavior.

**Empty/selection/details:** Map tab remains fully reachable, presents an intentional exploration
foundation message, and focuses global Close/tab navigation. It must not say “not implemented” or
show internal IDs.

**Tests/validator:** A2 shell and empty-state tests; stable-ID/map-definition tests only in a
separately approved functional Map stage. Validator must reject scene-name identity and Sandbox map
fixtures in production.

**Editor wiring:** tab registration/button/navigation/empty state. No map asset or WGE-to-map
conversion is created in A2.

**Deferred:** functional map model, authored geometry, current position/room, pan/zoom, markers,
Quick Map hold/double-tap, transition restoration.

**Approval:** whether functional Map is a follow-up; first-pass room/zone scope; presentation
geometry; discovery rules; pan/zoom; marker scope; WGE adapter boundary; current-room seam.

---

## J. Visual and UX direction

### Recommended first-pass composition

Use a full-screen, authored Gameplay Menu panel under `RootInterfaceLayer`. Recommended reviewable
default:

- A left vertical tab rail containing all seven labels/icons in fixed order. Seven tabs fit 4:3
  and localized layouts more reliably than a single compressed top row.
- A large content field to the right with a clear tab title, primary list/collection region, and
  optional details region.
- A persistent, visible Close/Back control/hint in the frame.
- Gear uses a collection/list plus details composition; empty tabs use distinct illustration,
  title, restrained body copy, and the same navigation grammar.
- Tabs may have distinct accent treatments, silhouettes, or subtle motifs, but share one frame,
  type scale, focus language, and motion system.

The left-rail choice is a practical default, not an architecture requirement. Claude Opus may
propose a superior top/bottom treatment if all seven tabs, 4:3 behavior, explicit navigation, and
future iteration remain strong. Sam approves the final placement.

### Information and focus

- Current tab identity must be unmistakable independently of hover/focus.
- Selected UI focus needs at least two channels (for example shape/outline plus color), with strong
  controller readability.
- Hover, selected, pressed, active-tab, and disabled states must not be visually conflated.
- Entry lists prioritize name/category; details prioritize physical identity, functional
  explanation, control hint, then flavor.
- Empty states should feel authored and calm, not broken: no warning icon, debug wording, unknown
  totals, grey mystery grids, or “coming soon.”

### HUD treatment

Recommended default is an opaque or near-opaque menu background with a subtle view of the game/HUD
behind it. Do not disable HUD components or unsubscribe them. A dim/blur/mask is presentation only;
avoid runtime blur if it creates avoidable URP/performance complexity. The Gameplay Menu full-screen
raycast surface blocks HUD interaction while visible. Sam approves whether HUD is fully covered,
dimly visible, or compositionally masked.

### Motion and transitions

- Use short unscaled-time fades/slides for root and tab transitions.
- Input and selection become valid immediately; motion must not create a second opening state or
  delay A1 pause ownership unless explicitly guarded.
- Rapid tab changes should cancel/replace presentation tweens cleanly, never queue a long animation
  chain.
- Support reduced-motion-friendly implementation later by keeping motion presentation-only.
- No copied motifs, layout, typography, icons, or animation from reference games.

### Aspect ratio and iteration

- Anchor the rail/frame to safe area and let the content field expand from 4:3 through 21:9.
- Cap readable text/entry widths at ultrawide; use extra space for breathing room/details, not
  stretched text.
- At 4:3, preserve all seven tabs and focus order; collapse optional art/details before labels or
  navigation.
- Use nested shared prefabs and presentation-only serialized fields so the UI/UX partner can alter
  proportions, typography, decoration, states, and animation without changing tab IDs, gameplay
  references, or root flow.

Fixed contracts are tab count/order/visibility, ownership, root flow, input map, semantic layering,
valid selection, and no fake data. Reviewable visual choices include composition, tab placement,
panel proportions, typography, empty-state illustration, focus treatment, HUD coverage, and motion.
Final copy, Gear identities, icons/art, and meaningful tab category labels require Sam's approval.

---

## K. Input and navigation plan

### Actions and recommended bindings

| Purpose | Action | Recommended keyboard | Recommended gamepad | Notes |
|---|---|---|---|---|
| Open/close Gameplay Menu | `System/GameplayMenu` | I (fixed current decision) | Select/View/Back button | Add controller binding only after approval; not Tab. |
| Previous tab | new `UI/PreviousTab` | Q | Left Shoulder | UI map only; fresh press; no `Player/Previous`. |
| Next tab | new `UI/NextTab` | E | Right Shoulder | E overlap with gameplay Interact is safe only because A1 resume-disarm is preserved; approval required. PageUp/PageDown are lower-conflict fallback bindings. |
| Navigate UI | existing `UI/Navigate` | WASD/arrows | Left stick/D-pad | Content and tab-rail navigation. |
| Activate/click | existing Submit/Click | Input System submit controls/mouse | South button | Direct tab button selection requires submit/click; focus alone should not unexpectedly switch tabs. |
| Back | existing `UI/Cancel` | Escape | East button | Nested child/modal, then root. |

Create PreviousTab/NextTab in the existing `UI` map, with durable `InputActionReference` sub-assets.
Do not add an action asset or Gameplay Menu-specific map. Do not bind keyboard Tab.

### Navigation behavior

- Default first open selects Gear and its remembered/first content; later opens use runtime last
  tab.
- Mouse click on a tab switches directly and leaves a valid selection on that tab button or first
  content based on the approved pointer policy.
- Shoulder/Q/E cycling changes the active tab immediately. Recommended wraparound is Map -> Gear
  and Gear -> Map, subject to approval.
- If focus is in the left tab rail, Right enters restored/first content; Left from content returns
  to the active tab button. Up/Down moves within the rail or authored content. If a top bar is
  chosen, adapt this to Down/Up.
- On shoulder cycling while focus is in content, move focus to the new tab's restored/first
  selection. While focus is on the rail, keep focus on the newly active tab button.
- Each empty tab falls back to global Close or its active tab button; EventSystem selection must
  never become null while the root is open.
- Dynamic Gear navigation is rebuilt after entry changes and retains stable-key selection.
- Back hierarchy: active tab child/modal -> Gameplay Menu root -> A1 close sequence.
- Gameplay Menu input at stable bare root toggles closed. If a future nested modal is open, the
  root's `HasOpenModal`/Back policy must be explicitly defined; initial recommendation mirrors Pause:
  close the top layer first.

### Held input and A1 compatibility

- `GameplayMenuScreen` subscribes to tab actions only while shown and never enables/disables the
  entire UI map itself.
- UIFlow snapshots Pause/Menu/Cancel before state changes exactly as A1.
- Closing while Q/E, shoulder, Cancel/East, Submit, or gameplay-overlapping controls are held must
  not leak Hero commands. `HeroInputReader.BeginResumeGameplayInput()` remains authoritative.
- Continuous movement resumes under the unchanged A1 policy.
- No blocked root or tab request is queued. Tab input received while root is closed is ignored
  because the UI map and screen subscriptions are inactive.

### Durable references

Assign:

- Existing `System/Pause`, `System/GameplayMenu`, and `UI/Cancel` sub-assets to the corresponding
  `UIFlowController` fields.
- New `UI/PreviousTab` and `UI/NextTab` sub-assets to `GameplayMenuScreen`.
- Keep all existing production/Sandbox input-module references intact.
- Add the new references to validator expectations without requiring the input module to consume
  them (they are screen actions, not module navigation actions).

---

## L. File-by-file implementation plan

Paths below are relative to the repository. `.meta` files are created by Unity as required during
implementation even though this planning task does not touch them.

### 1. Existing files definitely modified

| Path | Purpose / symbols / fields / call sites | Risk and validation |
|---|---|---|
| `Assets/_Project/Scripts/UI/Flow/UIFlowController.cs` | Register real Gameplay Menu through existing field; optionally add atomic `TryOpenGameplayMenu(GameplayMenuTabId?)`; preserve all A1 handlers/order. | Highest A1 regression risk. Run all UIFlow/input arbitration tests and paused-transition checks. |
| `Assets/_Project/Input/InputSystem_Actions.inputactions` | Add `UI/PreviousTab`, `UI/NextTab`, approved bindings, durable reference sub-assets; add approved controller binding to `System/GameplayMenu`. | Binding conflicts and regenerated IDs. Validate exact action paths and real-device input. |
| `Assets/_Project/Prefabs/Managers/_GameCameras.prefab` | Nest `GameplayMenuScreen.prefab` under `MenuRoot/RootInterfaceLayer`; assign `gameplayMenuRootBehaviour`, current root action refs, previous/next refs through screen, PlayerAbilityState/catalogue, HUDCamera inheritance/layering. | Persistent duplication/stale refs. Validator and Boot transitions. |
| `Assets/_Project/Scripts/UI/Sandbox/UISandboxController.cs` | Focused A2 fixture controls: real seven-tab screen, isolated cloned ability states, Gear zero/one/multiple/live unlock/missing-definition, open/close/aspect cases. Main symbols remain Sandbox-only. | Never mutate production assets or save; cleanup runtime SOs. |
| `Assets/_Project/Scenes/UISandbox.unity` **if the local move is approved** | Nest the real Gameplay Menu and A2 fixture controls; keep one local EventSystem and no production managers. | User-owned uncommitted path move. Confirm before edit; remain out of Build Settings. |
| `Assets/_Project/Scripts/Editor/Validation/UIFoundationValidator.cs` | Update approved Sandbox path; replace A1 unassigned-root warning with A2 requirements; validate seven registrations/order, tab views/first fallbacks, catalogue/state, actions, layering, no fixture leakage. | Avoid overlapping diagnostics; add validator tests. |
| `Assets/_Project/Scripts/Editor/Tests/Hero/PlayerPersistentStateTests.cs` | Stage 0 tests for all-locked fresh/reset/default state and existing explicit unlock round-trip. | Avoid production asset mutation; use transient SOs/data. |
| `Assets/_Project/Scripts/Hero/Core/PlayerAbilityState.cs` | Stage 0 only: all eight field/reset defaults locked. No Gear APIs added. | Gameplay-state change; preserve saved values and ability mechanics. |
| `Assets/_Project/Scripts/Save/Data/AbilitySaveData.cs` | Stage 0 only: all eight new-instance defaults locked. | Existing save booleans must remain explicit. |
| `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` | Stage 0 cleanup to all locked after compatibility review. | Mutable dev state; coordinate with test scenes/saves; never delete saves blindly. |
| `Assets/_Project/Tests/PlayMode/UI/UIFlowInputArbitrationPlayModeTests.cs` | Retain and, where efficient, extend real-input regression for Gameplay Menu root and same-frame Pause/Cancel behavior. | Must execute via real Input System player loop, not private handler calls only. |

The implementer must re-run `git status` first. If the UISandbox relocation has changed, adjust the
single approved path without overwriting user work.

### 2. New runtime scripts

| Path | Main symbols / serialized fields / call sites | Validation |
|---|---|---|
| `Assets/_Project/Scripts/UI/GameplayMenu/GameplayMenuTabId.cs` | Fixed enum: Gear, Tools, Satchel, Recipes, Tasks, Journal, Map. | Ordering tests and validator. |
| `Assets/_Project/Scripts/UI/GameplayMenu/IGameplayMenuTab.cs` | Narrow lifecycle/navigation contract in Section H. | Contract/fixture tests. |
| `Assets/_Project/Scripts/UI/GameplayMenu/GameplayMenuTabRegistration.cs` | Serializable ID/button/view/fallback registration; resolves `IGameplayMenuTab`. | Null/duplicate/order diagnostics. |
| `Assets/_Project/Scripts/UI/GameplayMenu/GameplayMenuTabButton.cs` | Serialized `Button`, active/focus presentation targets; event or listener seam; `SetActiveTab(bool)`. | Active state independent from selection; mouse tests. |
| `Assets/_Project/Scripts/UI/GameplayMenu/GameplayMenuEmptyStatePanel.cs` | Presentation-only title/body/art references; no gameplay state. | No debug/future totals; valid raycast/navigation. |
| `Assets/_Project/Scripts/UI/Screens/GameplayMenuScreen.cs` | Implements root interface; visual root, EventSystem, registrations, Close, Previous/Next refs, memory dictionaries; APIs in Section G. | Core EditMode/PlayMode tests. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/ToolsTabView.cs` | Focused empty-state tab owner. | Visibility/Back/first fallback. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/SatchelTabView.cs` | Focused Satchel presentation owner; empty in A2. | No quantity owner. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/RecipesTabView.cs` | Focused recipe-knowledge presentation owner; empty in A2. | No crafting action. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/TasksTabView.cs` | Focused task presentation owner; empty in A2. | No progress state. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/JournalTabView.cs` | Focused Journal presentation owner; empty in A2. | No speculative categories. |
| `Assets/_Project/Scripts/UI/GameplayMenu/Tabs/MapTabView.cs` | Focused full-map tab owner; empty/foundation in A2. | No scene-name/WGE-coordinate map. |
| `Assets/_Project/Scripts/UI/Gear/GearDisplayDefinition.cs` | SO display metadata; stable key, one ability, approved content/hint metadata. | No state/tuning/layout coordinates. |
| `Assets/_Project/Scripts/UI/Gear/GearDisplayCatalog.cs` | Ordered definitions/lookups/validation helpers. | Unique keys/mappings/nulls. |
| `Assets/_Project/Scripts/UI/Gear/GearScreen.cs` | Gear tab lifecycle, read-only state, entry rebuild, key selection, event subscription. | Mutation prohibition and subscription tests. |
| `Assets/_Project/Scripts/UI/Gear/GearEntryView.cs` | Entry rendering and selection/click event. | Reuse/pooling cleanup and navigation. |
| `Assets/_Project/Scripts/UI/Gear/GearDetailsPanel.cs` | Approved metadata details and clear state. | No raw enum/tuning. |
| `Assets/_Project/Scripts/UI/GameplayMenu/InputBindingDisplay.cs` *(optional, only if Gear hints need it)* | Narrow `InputActionReference` binding display string + neutral fallback. | Keyboard/controller text; no full glyph system. |

If the six focused empty tab classes are identical beyond references, keep them extremely small;
do not introduce a class hierarchy or generic inventory presenter to remove a few lines.

### 3. New Editor scripts

No new Editor script is required if `UIFoundationValidator` is extended as recommended. Add a
separate validator only if the existing file becomes unmaintainable during implementation; if so,
move—not duplicate—all Gameplay Menu/Gear checks into
`Assets/_Project/Scripts/Editor/Validation/GameplayMenuValidator.cs` and have one menu command call
both once.

### 4. New tests

| Path | Purpose |
|---|---|
| `Assets/_Project/Scripts/Editor/Tests/UI/GameplayMenuScreenTests.cs` | Registration/order/default/memory/cycling/direct selection/Back/selection/diagnostics. |
| `Assets/_Project/Scripts/Editor/Tests/UI/GearScreenTests.cs` | Read-only filtering, empty state, stable keys, refresh/subscription/catalogue cases. |
| `Assets/_Project/Scripts/Editor/Tests/UI/UIFoundationValidatorTests.cs` | A2 prefab/input/Sandbox/catalogue validator failure cases without relying only on the menu command. |
| `Assets/_Project/Tests/PlayMode/UI/GameplayMenuPlayModeTests.cs` | Real registered root, pause/input, visible seven tabs, real actions, mouse/controller navigation, transitions, subscription lifecycle. |

### 5. New prefabs

| Path | Purpose / serialized contract |
|---|---|
| `Assets/_Project/Prefabs/UI/GameplayMenuScreen.prefab` | Root component active; `Panel` inactive by default; seven buttons/registrations, global Close, content host, Previous/Next refs. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/GameplayMenuTabButton.prefab` | Reusable active/focus/press presentation. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/GameplayMenuEmptyStatePanel.prefab` | Reusable presentation-only empty composition. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/GearTab.prefab` | Gear collection/details/empty layout and `GearScreen`. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/ToolsTab.prefab` | Focused Tools shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/SatchelTab.prefab` | Focused Satchel shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/RecipesTab.prefab` | Focused Recipes shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/TasksTab.prefab` | Focused Tasks shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/JournalTab.prefab` | Focused Journal shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/MapTab.prefab` | Focused pausing Map shell/empty state. |
| `Assets/_Project/Prefabs/UI/GameplayMenu/GearEntryView.prefab` | Reusable acquired Gear entry. |

### 6. New ScriptableObjects

| Path | Purpose |
|---|---|
| `Assets/_Project/ScriptableObjects/UI/Gear/GearDisplayCatalog.asset` | Production ordered catalogue. May be empty until physical identities are approved; empty is valid. |
| `Assets/_Project/ScriptableObjects/UI/Gear/Definitions/<ApprovedStableGearKey>.asset` | One asset per approved physical Gear identity only. Exact filenames are content decisions and must not be invented by implementation. |

No ScriptableObject types/assets are created for Tools, Satchel, Recipes, Tasks, Journal, or Map in
A2. If no Gear identity is approved, ship the functional empty catalogue/screen and keep missing
unlocked mappings diagnostic-only; do not invent definitions.

### 7. Existing assets modified

- `_GameCameras.prefab`, `InputSystem_Actions.inputactions`, and `PlayerAbilityState.asset` as above.
- Generated durable `InputActionReference` sub-assets remain inside the `.inputactions` asset, not
  transient runtime references.
- No existing HUD, Hero config, ability tuning, WGE graph, save-manager prefab, or gameplay prefab
  changes are expected.

### 8. Scenes modified

- Only the approved current UISandbox path for focused fixture authoring.
- No Boot or gameplay scene changes.
- Build Settings remains unchanged; Sandbox remains excluded.

### 9. Documents updated after implementation

Update the exact documents/sections in Section R. Documentation changes occur after implementation
and validation, not during this planning task.

### 10. Files explicitly untouched

- Hero movement/actions/motor/blackboard/config/ability tuning and hero prefab.
- HUD runtime scripts and health/resource/boss state ownership.
- `GameManager` unless a discovered A1 bug is separately approved; A2 needs no new manager concern.
- `SaveManager`, `SaveData`, `SaveDataMigrator`, save version, and `_SaveManager.prefab` unless
  Stage 0 tests prove a missing/null compatibility defect. Defaults alone do not justify a version
  bump.
- `WorldStateRegistry`, `RoomVisitReporter`, WGE integration/graph, transitions, cameras, bosses,
  enemies, hazards, audio settings, Package B/frontend/notifications.
- `Assets/Plugins/`, `Assets/ThirdParty/`, project physics/layers, Hero feel assets/docs.

---

## M. Unity Editor work

### Input Actions

1. Open `InputSystem_Actions.inputactions`.
2. Add Button actions `PreviousTab` and `NextTab` to `UI`.
3. Author approved Q/E and left/right shoulder bindings, or approved alternatives.
4. Add the approved Gamepad Select/View binding to `System/GameplayMenu`.
5. Preserve keyboard I and all A1 action IDs/bindings.
6. Save/import and confirm durable `InputActionReference` sub-assets resolve to:
   `System/Pause`, `System/GameplayMenu`, `UI/Cancel`, `UI/PreviousTab`, `UI/NextTab`.
7. Do not bind keyboard Tab or reuse Player Previous/Next.

### `_GameCameras.prefab`

1. Under `MenuRoot/RootInterfaceLayer`, add a nested `GameplayMenuScreen.prefab` sibling of Pause.
2. Keep `RootInterfaceLayer` on `HUDCamera`, Screen Space - Camera, sorting 150, with its existing
   raycaster.
3. Assign the screen component to `UIFlowController.gameplayMenuRootBehaviour`.
4. Assign durable Pause, GameplayMenu, and Cancel references to existing UIFlow fields.
5. Assign production `PlayerAbilityState.asset` and `GearDisplayCatalog.asset` to Gear.
6. Verify one EventSystem, one input module, no scene-local refs, and no Sandbox components/assets.
7. Verify Gameplay Menu `Panel` is inactive by default and full-screen raycast blocker is active
   only with that panel.
8. Preserve Modal 180, HUD 100, Fade Overlay 999.

### Gameplay Menu and tab prefabs

- Author all seven tab buttons in fixed order and seven nested tab prefabs.
- Register exact IDs/buttons/views/fallbacks.
- Wire global Close to `GameplayMenuScreen.RequestClose`.
- Assign Previous/Next references and EventSystem.
- Author explicit rail/content navigation and active-tab presentation.
- Give every tab a valid first selection/fallback.
- Give Gear zero-entry state a valid fallback and details-clear state.
- Do not add per-tab Canvas/EventSystem unless a reviewed need appears; inherit the root Canvas.
- Do not add raycasters to decorative nested canvases.

### Gear catalogue

- Create the catalog asset.
- Create definition assets only after identity/content approval.
- Assign one unique ability per definition and unique stable keys.
- Use placeholder artwork only when visibly marked as provisional in the content workflow; do not
  encode final identity from raw enum names.

### Sandbox

- First resolve the local path move with Sam/current tree.
- Nest the same Gameplay Menu prefab under Sandbox `RootInterfaceLayer`.
- Use the Sandbox-local EventSystem and input module.
- Extend `UISandboxController` with runtime-created/cloned ability state and fixture-only catalogue
  data where required.
- Provide zero/one/multiple/live-unlock/missing-definition Gear controls and direct seven-tab
  open/switch/close checks.
- Optional Satchel/Recipe/Task/Journal/Map layout fixtures must be explicit test data, not
  production assets or registrations.
- Destroy all runtime-created SOs/objects and never touch saves.
- Keep scene excluded from Build Settings.

### Validator and authoring workflow

- Run `Tools/Project/Validate UI Foundation` after every production/Sandbox prefab wiring change.
- Extend the command to A2 rather than creating overlapping warnings.
- Claude may safely automate deterministic prefab nesting, serialized references, Input Actions,
  catalogue creation, navigation scaffolding, and validator execution through Unity MCP when
  connected.
- Claude may author reusable prefabs/assets and focused Sandbox fixtures.
- Human UI/UX review is required for composition, focus clarity, copy, Gear identities, artwork,
  aspect-ratio compromises, and animation feel.
- Final artwork, glyph library, localization, accessibility, and advanced motion remain later.
- Use nested prefab variants/instances; do not unpack shared prefabs merely for preview styling.

---

## N. Automated tests

No tests were run for this plan. During implementation, report test discovery, execution, and
results; do not repeat historical claims as a new pass.

### EditMode tests

`GameplayMenuScreenTests`:

- Exactly seven fixed registrations in Gear/Tools/Satchel/Recipes/Tasks/Journal/Map order.
- First-ever show defaults to Gear.
- Runtime last tab is retained across Hide/Show and is not serialized.
- Invalid remembered/requested tab falls back to Gear.
- Previous and next select the expected tab, including approved wrap/boundary behavior.
- Direct button/API selection.
- Each empty tab resolves a valid first selection/fallback.
- Per-tab selection is restored when still valid; invalid selection falls back safely.
- Tab switching never leaves EventSystem selection null.
- Missing tab/view/button/fallback diagnostics are concise and fail closed.
- Duplicate ID and incorrect-order diagnostics.
- Hide/Show and repeated switching do not duplicate action/button subscriptions.
- Back handled by a child stays in root; bare Back returns false.

`GearScreenTests`:

- Acquired mapped Gear only.
- Locked abilities create no view.
- Zero-entry empty state and details clear.
- First unlock moves selection to the first Gear entry when appropriate.
- Multiple entries use deterministic catalogue order.
- Stable-key selection survives rebuild/reorder.
- Removed selected key falls back to first visible or empty fallback.
- Refresh on initialize and every reopen.
- Live `AbilityChanged` refresh while shown.
- No refresh after Hide/unsubscribe; no duplicate subscription after repeated opens.
- Missing definition for an unlocked ability omits safely and logs once.
- Null definitions, duplicate stable keys, duplicate ability mappings.
- `PlayerAbilityState` mutation methods are never called by Gear.
- Save/load snapshot refresh is neutral and emits no acquisition UI.

Stage 0 persistent-state tests:

- New `AbilitySaveData` has all eight false.
- New/reset `PlayerAbilityState` has all eight false.
- `SaveManager.CreateFreshSave` applies all eight locked using isolated storage/test seams.
- Missing/null ability data receives all-locked defaults.
- A save with every explicit true/false combination applies exactly those values.
- Existing unlocked save values are not overwritten by new defaults.
- No save-version increase unless an actual migration behavior changes.
- True-new-game Gear has zero acquired entries.

Sandbox/validator tests:

- Sandbox fixtures use cloned/runtime state and clean it up.
- Production prefab references no fixture state/assets/components.
- Current approved Sandbox path is excluded from Build Settings.
- Validator catches wrong root count, EventSystem duplication, missing root, wrong tab count/order,
  duplicate IDs, invalid first selections, missing catalogue/state/action refs, bad sorting/camera/
  raycaster, Sandbox leakage, and invalid Gear catalogue.
- Validator test helpers should inspect prefab contents directly and restore any temporary fixture;
  do not mutate shared production assets without guaranteed cleanup.

### PlayMode tests

Use real `InputSystem` devices/events, the `Underbrew.UI.PlayModeTests.asmdef`, and the actual
player-loop/Unity Test Runner:

- `System/GameplayMenu` opens the real registered root after A1 availability is armed.
- `GameManager.State` becomes Paused and time scale changes only through GameManager.
- All seven tab buttons/roots are visible and reachable.
- Real PreviousTab/NextTab actions switch tabs; approved wrap/boundary behavior.
- Mouse click selects a tab.
- Gameplay Menu fresh press toggles the stable root closed.
- Cancel closes tab child first and bare root second.
- Pause while Gameplay Menu is open is rejected; Gameplay Menu while Pause is open is rejected.
- No Jump/Attack/Dash/Sprint/Interact/Bind leakage after close, including overlapping controls.
- Continuous movement resumes according to A1.
- EventSystem has a valid selected GameObject throughout keyboard/controller switching.
- Repeated open/close does not duplicate button/action/ability subscriptions.
- Gear updates while open after `PlayerAbilityState.SetUnlocked` is invoked by the test/progression
  owner, not by the view.
- Persistent root survives real room transitions without duplication or stale Hero refs.
- Empty tabs remain navigable.
- Controller navigation retains/restores valid selection.
- Existing same-frame Pause/Cancel Escape arbitration scenarios still pass.
- Transition/death/respawn/hazard/post-transition requests remain discarded, never queued.

Do not claim real input behavior from only invoking private handlers or `Button.onClick`. Direct
method tests remain useful for deterministic state-machine coverage, but at least one focused
PlayMode suite must send actual keyboard/gamepad/mouse events.

The previous hardening pass encountered an automation environment that could not advance real Input
System events. The implementation handoff should first run the existing working PlayMode runner in
Unity. If MCP cannot drive it reliably, run via the Unity Test Runner UI or project-appropriate
batch invocation and record the limitation. Do not create a broad custom runner.

---

## O. Validator plan

Extend `Assets/_Project/Scripts/Editor/Validation/UIFoundationValidator.cs`.

Rationale: this command already owns persistent MenuRoot/EventSystem/input-module/layering/Sandbox
composition. A second A2 validator would duplicate prefab loading, scene traversal, Build Settings,
and production/Sandbox diagnostics. Keep Gear catalogue checks in small reusable validation methods
called once by the foundation validator. If file size later forces extraction, retain one menu
command and one diagnostic owner per rule.

Required A2 checks:

- Exactly one persistent `MenuRoot`.
- Exactly one production EventSystem/input module and no gameplay-scene competitor.
- `gameplayMenuRootBehaviour` assigned and implements `IUIFlowRootScreen`.
- Gameplay Menu visual panel inactive by default.
- Exactly seven production registrations in fixed order.
- No duplicate IDs; no availability/hide flag suppressing confirmed tabs.
- Every view implements `IGameplayMenuTab`.
- Every tab resolves a valid first selection or authored global fallback.
- Every empty tab has a valid presentation root and focus target.
- Tab buttons are present/interactable and navigation does not point outside the root or to
  non-interactable controls.
- `GearDisplayCatalog` and production `PlayerAbilityState` assigned.
- Gear definitions non-null; stable keys non-empty/unique; ability mappings unique.
- Missing approved-definition policy reports intentionally; do not require unapproved enum members.
- Required Pause/GameplayMenu/Cancel/PreviousTab/NextTab action refs assigned and resolve to exact
  maps/actions.
- Input module retains all ten required action refs.
- Root/Modal raycasters present; HUDCamera assigned; RenderMode/sorting preserve
  Fade > Modal > Root > HUD.
- No Sandbox fixture assets/components/references in `_GameCameras.prefab` or production catalog.
- Current approved Sandbox path exists and is excluded from enabled Build Settings.
- Sandbox has exactly one local EventSystem/input module and no production `PersistentHudRoot`.
- No production UI component references `UISandboxController` fixture assets.
- No tab view serializes `SaveManager`, `HeroInputReader`, `SceneLoader`,
  `SceneTransitionManager`, or gameplay-scene objects.
- No second EventSystem or per-tab flow/pause manager.

Update the stale `SandboxScenePath` only after the local relocation is confirmed. Validator
diagnostics should include asset path, object/component, field, expected value, and actual value
where practical. Do not emit the same error from both the screen's initialization and multiple
validator methods.

---

## P. Manual validation matrix

Do not mark any row passed until a person actually performs it.

| Area | Required checks |
|---|---|
| Keyboard | I open/toggle; Escape Back; Q/E or approved previous/next; WASD/arrows; Submit; no Tab use; held-overlap leakage. |
| Controller | Approved Gameplay Menu button; shoulders; D-pad/stick; Submit/Cancel; focus retention; disconnect/reconnect. |
| Mouse | Direct tab clicks, entry clicks, Close, hover/active distinction, background/raycast blocking. |
| Root flow | Open/close, repeated rapid toggles, Pause rejection, Gameplay Menu rejection while Pause open, no double pause/unpause. |
| Seven tabs | Every label visible, fixed order, direct and cyclic reachability, active state, no disabled/hidden confirmed tab. |
| Rail/content | Enter content, return to tab rail, remembered content focus, active tab focus, empty fallback. |
| Empty states | Tools, Satchel, Recipes, Tasks, Journal, Map and zero-Gear all look intentional, contain no debug/fake totals/data, and remain navigable. |
| Gear | True empty; first approved unlock; multiple unlocks; locked omission; missing definition; stable selection; details refresh; reopen refresh; live unlock while open. |
| Rapid switching | Shoulder/key spam, mouse/key alternation, animation cancellation, no queued transitions or null selection. |
| Held controls | I, Escape/Cancel, shoulders, Q/E, Submit, Gamepad East, Jump/Attack/Dash/Sprint/Interact/Bind, movement through close. |
| Room transition | Requests rejected during every transition phase and one-second lockout; persistent root/HUD/EventSystem not duplicated; no stale Hero refs. |
| Death/recovery | Depleted health, death, respawn, hazard recovery reject/discard requests; no delayed open. |
| Focus lifecycle | Application focus loss/return, controller disconnect, pointer-to-controller switch, domain reload, prefab disable/destroy with open root. |
| Sandbox | Direct entry without Boot/SaveManager; isolated fixtures; cleanup; real shared prefabs; scene remains out of Build Settings. |
| Boot path | Start at Boot, fresh/continue state, real gameplay scene, menu across all four sample rooms. |
| Aspect/safe area | 16:9, 16:10, 21:9, 4:3, safe-area preview; all seven tabs readable; no clipped focus/details/copy. |
| HUD/layering | HUD remains subscribed; chosen cover/dim treatment; Modal above root; Fade above Modal; hidden menu does not block HUD raycasts. |
| Content review | Empty copy, physical Gear identity/copy/art, control hints, no copied reference-game identity. |

Manual validation reports should distinguish direct Sandbox review, production Boot-path review, and
automated test results.

---

## Q. Risks and architecture checks

| Risk | Classification | Guard / resolution |
|---|---|---|
| Old Gear-only docs drive hidden production tabs | Must fix before implementation completion | Treat this plan as A2 contract; reconcile exact sections in R. |
| Ability defaults make new-game Gear non-empty | Must fix before functional Gear validation | Complete Stage 0 and compatibility tests; do not use mutable asset as design. |
| Existing explicit unlock saves are overwritten | Must fix before implementation completion | Apply saved booleans unchanged; test combinations; no blind save deletion. |
| Local UISandbox move is overwritten or validator uses stale path | Must fix before editing that scene | Confirm intent from current working tree/Sam; update one canonical path. |
| Scope grows into six gameplay systems | Must guard during implementation | Shell/empty states only; no speculative owners/SOs. |
| Empty states look broken or like errors | Manual review item | Authored player copy/art/focus; no debug language/totals. |
| Fake Sandbox data becomes production data | Must guard during implementation | Runtime clones/test assets only; validator production-reference rejection. |
| Generic tab/inventory framework overengineering | Must guard during implementation | Narrow `IGameplayMenuTab`; focused tab owners; no generic item APIs. |
| Q/E or shoulder conflicts | Manual review item | UI map only; real-device tests; approve alternatives. |
| Tab cycling fights EventSystem navigation | Must guard during implementation | Separate actions; explicit zone/focus policy; do not reuse D-pad Previous/Next. |
| Input command leaks on close | Must fix before implementation completion | Preserve A1 close order/disarming; real input tests. |
| Same-frame Pause/Cancel regression | Must fix before implementation completion | Do not reorder UIFlow snapshot/arbitration; retain PlayMode tests. |
| Persistent UI/EventSystem duplication | Must fix before implementation completion | `_GameCameras` only; validator and room-transition test. |
| Persistent screen retains scene-local refs | Must guard during implementation | Only persistent assets/EventSystem/prefab refs; Hero resolved by UIFlow just in time. |
| Gear mutates or tunes abilities | Must guard during implementation | Read only `IsUnlocked`/event; no HeroAbilityConfig; tests/code review. |
| Satchel becomes speculative inventory/save owner | Must guard during implementation | Empty presenter only; future domain owns quantities and save. |
| Recipes becomes remote crafting owner | Must guard during implementation | Knowledge-only boundary; no production buttons. |
| Tasks becomes quest-state owner | Must guard during implementation | Empty presenter; future state SO/domain owns progress. |
| Journal categories are invented | Manual review item | Flexible empty shell; approve categories before data assets. |
| Map uses scene names or WGE editor positions | Must guard during implementation | No functional map in A2; future stable room-ID catalogue. |
| Functional Map overwhelms A2 | Deferred | Visible empty Map now; separately approve Map FeatureSpec/stage. |
| Existing visitation facts are mistaken for complete map state | Must guard during implementation | Document missing snapshot/model/geometry/current-room seams. |
| Prefab merge conflict with local A1/Sandbox work | Must guard during implementation | Re-audit status; minimal nested prefab changes; do not reserialize unrelated assets. |
| Placeholder art becomes hard to replace | Manual review item | Reusable presentation prefabs and asset refs; no layout coordinates in definitions. |
| Creative UX changes gameplay rules | Must guard during implementation | Fixed technical contracts; route gameplay/persistence/input decisions to approval. |
| Another game's UI is copied too closely | Manual review item | Use genre conventions only; original Underbrew composition/assets/terminology. |
| Final glyph/localization/accessibility scope expands A2 | Deferred | Narrow binding strings and neutral text only. |
| A2 changes Hero/camera/HUD/game feel | Must guard during implementation | Explicit untouched list; visual cover only; no tuning. |

---

## R. Documentation updates after implementation

Update only after code/assets are implemented and validation is honestly reported.

| Document | Exact sections to update |
|---|---|
| `Docs/Architecture.md` | `Persistent Player State`; `UI / HUD` persistent composition, ownership, input/root flow, Full Map boundary; `Planned Systems` status row. Add new Gear display SO type to the architecture map because AGENTS requires documenting new SO types. |
| `Docs/ImplementationPlan.md` | `Authoritative UI Roadmap`; mark Stage 0/A2 items accurately; replace Gear-only filtering with seven visible tabs and empty-state distinction; update deferred systems to mean functional data, not tab visibility; record test/manual results. |
| `Docs/FeatureSpecs/UIArchitecture.md` | `Current implemented state`; `Persistent composition`; `Ownership table`; `UIFlowController boundary`; `Back, focus, and selection`; `Input-map`; `Quick Map versus Full Map`; `Sandbox and Gear relationships`; Editor/validator/test/manual sections. |
| `Docs/FeatureSpecs/PauseAndMenuFlow.md` | `Current state`; `Gameplay Menu`; `Input ownership/System action-map`; EventSystem/focus; automated/manual sections; preserve A1 arbitration and clarify visible Map tab vs deferred Quick/functional Map. |
| `Docs/FeatureSpecs/Gear.md` | `Status`; ability-default prerequisite outcome; `Planned Gear presentation data` -> implemented details; `Gameplay Menu integration` remove hidden siblings; refresh/tests/Editor work; approved physical identities only. |
| `Docs/FeatureSpecs/UISandbox.md` | Canonical scene path; preview controls/states; seven production tabs; Gear fixtures; production/Sandbox boundary; validator/manual results. |
| `Docs/FeatureSpecs/Abilities.md` | `PlayerAbilityState` defaults contradiction and authoritative new-game contract; planned validation -> observed Stage 0 implementation/results. Do not add UI presentation details beyond Gear boundary. |
| `Docs/FeatureSpecs/SaveSystem.md` | `AbilitySaveData` schema/default semantics; fresh-save/missing-data behavior; compatibility/test result. Do not add Gameplay Menu memory to saves. |
| `Docs/ImplementationPlans/UIImplementationPlan.md` | `Authoritative-decision note`; `Gear vertical slice`; `Gameplay Menu shell`; Sandbox; file/test/validator proposal; Package A2 package summary. Clearly mark this A2 plan as the newer product decision. |

Recommended new specification:

- Add `Docs/FeatureSpecs/GameplayMenu.md` because the seven-tab shell, fixed order, runtime memory,
  input/navigation, empty-state policy, and root API are now a durable cross-tab contract.

Do not create empty FeatureSpecs for Tools, Satchel, Recipes, Tasks, Journal, or Map merely for
symmetry:

- Add a focused `Map.md` only when the functional Map model/identity/presentation scope is approved.
- Add Satchel/Recipes/Tasks/Journal specs only when their gameplay owners and minimum contracts are
  approved.
- Record the confirmed current boundary/empty-state behavior in `GameplayMenu.md` meanwhile.
- Tools specifically needs a product-purpose decision before a useful FeatureSpec can exist.

---

## S. Decisions requiring Sam's approval

| Decision | Recommended practical default | May Claude decide reversibly? |
|---|---|---|
| Default tab | Gear | No; approve as product behavior (recommended). |
| Tab wraparound | Wrap Gear <-> Map for shoulder/Q/E cycling | No; input behavior approval. |
| Keyboard previous/next | Q / E; PageUp/PageDown fallback if overlap feels poor | No; binding approval. |
| Controller Gameplay Menu | Select/View/Back button | No; binding approval. |
| Controller previous/next | Left/Right Shoulder | No; binding approval. |
| Tab-bar position | **Approved correction:** top-centred horizontal seven-tab strip; preserve all glyphs at narrow widths and show the selected title separately | Implemented correction direction; do not restore the left rail. |
| Tab visual direction | Cohesive Underbrew frame with distinct restrained accent per tab | Yes, original work only. |
| HUD behind Gameplay Menu | Near-opaque full-screen cover; HUD remains active/subscribed | Yes, performance-safe presentation choice. |
| Empty-state wording | Calm discovery/collection wording with no “coming soon,” errors, totals, or developer language | Claude may draft; Sam approves final player copy. |
| Meaning of Tools | Leave function unclaimed in A2 | No; product decision required before functionality. |
| Satchel categories | Do not make production category controls until approved; use the listed set only as future direction | No. |
| Recipe categories | None in production A2; knowledge-only boundary | No. |
| Tasks structure | Empty shell now; future Active/Completed split is recommended starting hypothesis | No. |
| Journal categories | None until content direction is approved | No. |
| Map first-pass scope | Visible intentional shell in A2; functional map is separate approved follow-up | No. |
| Approved Gear identities | Author no definition without approved physical name/content; prioritize the first actual progression pickups | No. |
| Control hints | Binding display strings plus authored neutral phrasing; no glyph database | Yes for layout/format; binding identity/copy reviewed. |
| Placeholder art boundary | Replaceable UI-only assets, clearly provisional; never establish physical Gear identity accidentally | Yes within approved identities. |
| Animation scope | Short unscaled root/tab/focus transitions, cancel-safe; no large VFX system | Yes, reversible and performance-safe. |
| Functional Map now or later | Later, behind the visible Map tab, unless separately approved with map-model scope | No; major scope/data ownership decision. |
| Reversible visual discretion | Claude may decide layout proportions, focus visuals, empty composition, and responsive behavior without stopping | Yes; document decisions. |

---

## Implementation stages and stopping points

### Stage 0 — Required ability/save prerequisite

Correct all eight code/fresh/reset defaults to locked, verify missing/null ability data, preserve
explicit existing save values, clean the mutable asset deliberately, and add focused tests. Do not
change schema version unless tested migration behavior requires it. Safe stopping point: gameplay
starts with intended abilities and all pre-existing explicit save unlocks still apply; no UI work
depends on mutable dev values.

### Stage 1 — Seven-tab Gameplay Menu foundation

Create the root, narrow tab contract, fixed registrations, tab bar, Previous/Next UI actions,
direct click, runtime memory, selection/Back hierarchy, six focused empty presenters plus Map empty
presenter, prefabs, and core tests. Wire first in Sandbox with isolated state. Safe stopping point:
all seven tabs are visible/reachable and root/navigation behavior works without any invented
gameplay data.

### Stage 2 — First functional integration

Implement read-only Gear definitions/catalogue/screen/entries/details, empty state, stable-key
selection, open refresh, active-only `AbilityChanged` subscription, isolated fixtures, tests, and
catalogue validation. Author only approved physical identities. Safe stopping point: Gear reflects
authoritative acquisition neutrally, while every other tab remains an intentional shell.

### Stage 3 — Production integration and validation

Wire `_GameCameras.prefab`, durable Input Action references, production catalogue/state, current
approved Sandbox path, extended validator, EditMode/PlayMode regressions, Boot-path/manual matrix,
UI/UX review, and documentation reconciliation. Safe stopping point: one persistent production
root works across rooms and devices without A1 regressions, validator/test results are recorded, and
remaining functional gameplay systems are explicitly deferred.

---

## Claude Opus review and implementation handoff prompt

```text
You are working in the Unity repository:

E:\GameDev\Projects\Final_Project\Metroidvania Controller

Review and, only after the plan is approved, implement Package A2 — Full Gameplay Menu Interface.

Your primary contract is:

Docs/ImplementationPlans/UIA2ImplementationPlan.md

Before editing:

1. Read that plan completely.
2. Read AGENTS.md, Docs/Architecture.md, Docs/ImplementationPlan.md, and all authoritative UI,
   Gear, Abilities, Save, HUD, input, camera, audio, Sandbox, world-persistence, and WGE documents
   referenced by the plan.
3. Run a fresh read-only working-tree audit:
   git branch --show-current
   git rev-parse HEAD
   git status --short
   git diff --stat
   git diff
4. Treat the entire current local tree as ground truth. Preserve all Package A1 and user-owned
   uncommitted work. In particular, resolve the current UISandbox relocation status before editing
   either scene path or the validator. Do not reset, clean, stash, restore, discard, or overwrite.
5. Inspect the current scripts, prefabs, scenes, Input Actions, ScriptableObjects, tests, validator,
   and Build Settings. Use Unity MCP read-only inspection where helpful and connected.
6. Review the Codex plan critically. Identify contradictions, missing dependencies,
   overengineering, architecture drift, weak UX choices, unsafe assumptions, or changes made in the
   repository since the audit.
7. Recommend necessary corrections before editing. Distinguish technical/product blockers from
   reversible visual choices you can resolve responsibly.
8. Preserve every Package A1 contract: root exclusivity, same-frame Pause/Cancel arbitration,
   paused-transition rejection, transition/post-transition blocking, discarded blocked requests,
   UI-owned pause, Hero input suspension/clearing/rearming, continuous movement policy, teardown,
   persistent EventSystem, layering, and Sandbox isolation.

Confirmed product decisions:

- The production Gameplay Menu has seven visible, reachable tabs in this fixed order:
  Gear, Tools, Satchel, Recipes, Tasks, Journal, Map.
- Do not hide ownerless tabs. Give them intentional, polished, navigable empty states.
- Do not invent player data, totals, capacity, ownership, persistence, or gameplay systems.
- Gear is physical permanent progression and reads PlayerAbilityState.IsUnlocked(AbilityId)
  read-only. It never calls Unlock, Lock, SetUnlocked, or ResetToDefaults.
- Tools has no confirmed meaning yet.
- Satchel is separate from Gear and has no current inventory/quantity owner.
- Recipes are discovered knowledge and do not enable remote production.
- Tasks and Journal have no current authoritative owners.
- Map is the visible pausing full-map tab, but the current room-visitation/WGE facts are not a
  complete player-map model. Do not fake a map or use scene names/WGE editor coordinates as map
  identity.
- Quick Map, markers, full functional Map data, inventory, recipe discovery, tasks, Journal
  discovery, Package B settings, frontend, functional Quit, notifications, final glyphs,
  localization, accessibility, and final artwork are deferred unless Sam separately approves them.

Implementation sequencing:

- Implement only approved stages from the plan.
- Stage 0 is required if the all-locked new-game mismatch still exists. Preserve explicitly saved
  existing unlock values; do not delete saves blindly or bump save version without a real migration
  need.
- Stage 1 builds the seven-tab shell and empty states.
- Stage 2 integrates read-only Gear and only approved display definitions.
- Stage 3 performs production/Sandbox wiring, tests, validator, manual review, and docs.
- Do not expand A2 by creating gameplay owners solely to populate empty tabs.

Architecture constraints:

- Extend the existing UIFlowController and IUIFlowRootScreen architecture. Do not create another
  root manager, EventSystem, pause owner, action asset, menu-specific action map, giant UIManager,
  generic inventory/window/item framework, or parallel save/scene flow.
- UI never writes Time.timeScale, never directly accesses HeroInputReader, never loads scenes,
  never saves, and never owns gameplay state.
- GameManager remains pause/time authority.
- HeroController remains thin; HeroMotor/blackboard/actions/tuning remain unchanged.
- HUD remains persistent and subscribed; Gameplay Menu may cover/dim it visually only.
- Preserve Fade > Modal > Root > HUD layering.
- Use the existing UI map. Add focused PreviousTab/NextTab actions only if still missing. Do not use
  Player Previous/Next and do not bind keyboard Tab.
- Runtime last-tab memory belongs to the persistent GameplayMenuScreen and is not saved or stored in
  PlayerPrefs/settings.
- Keep the shared tab contract narrow and each tab presentation owner focused.

UI/UX freedom:

- Use your strengths to improve composition, tab presentation, focus flow, layout, empty states,
  information hierarchy, interaction feedback, aspect-ratio handling, and reusable prefab
  structure.
- You may choose strong reversible visual defaults without stopping for approval. Keep them easy
  for the UI/UX partner to revise and document each provisional choice.
- You may draw on high-quality metroidvania UI conventions, but do not copy another game's art,
  icons, exact layout, typography, motifs, animation, terminology, or visual identity.
- Do not use visual creativity to change tab purpose, input behavior, data ownership, persistence,
  map scope, crafting access, or gameplay.
- Flag/request approval for gameplay behavior, persistence, tab purpose, input bindings, functional
  Map scope, final content, physical Gear identities, and major scope growth.

Unity work:

- Complete all approved Input Actions, durable InputActionReference assignments,
  GameplayMenuScreen/tab/Gear prefabs, _GameCameras wiring, Sandbox fixtures, catalogue/assets,
  explicit navigation, camera/sorting/raycaster setup, and validator work.
- Use Unity MCP and Hot Reload where appropriate. If MCP is unavailable/revoked, inspect YAML/source
  and work normally; do not change MCP settings or repeatedly retry workarounds.
- Never let Sandbox fixture assets/components enter production.
- Keep the Sandbox excluded from Build Settings.
- Do not modify Assets/Plugins or Assets/ThirdParty.
- Avoid unrelated reserialization/refactoring.

Validation:

1. Run focused EditMode tests, including Stage 0 defaults, Gameplay Menu, Gear, and validator cases.
2. Run focused PlayMode tests through the real Unity runner with actual Input System events.
3. Run Tools/Project/Validate UI Foundation after its approved A2 extension.
4. Run relevant A1 input/root/camera lifecycle regressions.
5. Perform the approved manual matrix where possible: keyboard, controller, mouse, all seven tabs,
   empty/Gear states, rapid/held input, transitions/death/recovery, direct Sandbox, real Boot path,
   focus/disconnect, aspect ratios, and safe area.
6. Report automated and manual validation separately.
7. Never claim tests were discovered/executed/passed or manual checks were performed unless directly
   observed. Record runner limitations honestly; do not create a broad custom test runner.

Documentation and delivery:

- Update the authoritative documents and exact sections listed in Section R after implementation.
- Add a GameplayMenu FeatureSpec if the implemented cross-tab contract warrants it. Do not create
  empty symmetric FeatureSpecs for undecided domains.
- Do not implement Package B or deferred gameplay systems.
- Do not alter Hero feel, movement, combat, ability tuning, camera, bosses, hazards, transitions,
  world persistence, or WGE ownership.
- Do not commit or push unless explicitly requested.
- Finish with:
  1. a file-by-file implementation report,
  2. automated test results,
  3. manual validation actually performed,
  4. remaining manual Unity checks,
  5. Inspector/prefab/scene work completed or still required,
  6. provisional UI/UX decisions you made,
  7. deferred dependencies and approval items,
  8. confirmation that the pre-existing working tree was preserved.
```
