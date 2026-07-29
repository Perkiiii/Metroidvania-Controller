# Underbrew Gameplay Menu Five-Tab Correction
## Implementation-ready plan for Codex

**Repository:** `Perkiiii/Metroidvania-Controller`  
**Authoritative inspected baseline:** `9ef4e5255eeb9583c072d80372b557f821810372`  
**Previous baseline:** `1e233fbf40d3e8796f778c5957661e9b79734d34`  
**Pass type:** Post-Package-A2 information-architecture correction  
**Execution model:** One uninterrupted implementation pass  
**Validation model:** Codex updates test coverage but does **not** run tests. Sam runs the validator, tests, and manual checks after implementation.

---

# 1. Decision summary

Replace the current seven top-level Gameplay Menu tabs:

`Gear → Tools → Satchel → Recipes → Tasks → Journal → Map`

with five top-level tabs:

`Gear → Loadout → Satchel → Field Notes → Map`

Use the following distinction:

- **Gear**: permanent physical progression possessions and major practical capabilities. The current screen remains read-only and ability-backed. Literal gathering tools such as a sickle attachment, axe, or mining capability are not implemented in this pass; they may later be represented here once their real gameplay owner, acquisition rules, and persistence exist.
- **Loadout**: the future crest/charm-style combat-build customisation destination. Its internal stable ID is `CombatLoadout` so the code cannot be confused with literal harvesting tools. Its player-facing label is provisionally `Loadout`.
- **Satchel**: gathered materials, ingredients, seeds, drops, consumables, and quantities once an inventory owner exists.
- **Field Notes**: one top-level knowledge destination that will eventually contain Recipes, Tasks, and Journal/discovery content as internal sections.
- **Map**: the full player map destination. Quick Map remains deferred and keeps its existing input reservation.

This pass changes menu structure and presentation only. It does not implement Loadout rules, inventory, recipes, quests, journal data, map data, practical tool ownership, crafting, or saves.

---

# 2. Why this is the correct implementation point

The seven-tab structure is currently embedded in:

- `GameplayMenuTabId`
- `GameplayMenuScreen.FixedTabOrder`
- serialized tab registrations
- the tab-strip hierarchy
- the content-host hierarchy
- individual empty-state tab prefabs
- `_GameCameras.prefab`
- `UISandbox.unity`
- `UIFoundationValidator`
- EditMode tests
- PlayMode tests
- authoritative documentation

However, only Gear is data-backed. The other six tabs all use the shared presentation-only `GameplayMenuEmptyTabView`. This means the information architecture can still be corrected without migrating real Recipes, Tasks, Journal, Tools, Satchel, or Map state.

The existing runtime implementation is already mostly count-driven:

- tab switching iterates the `tabs` list;
- cycling uses `tabs.Count`;
- strip navigation builds from the authored registrations;
- compact/expanded density is calculated from the current tab buttons;
- selection memory is keyed by `GameplayMenuTabId`;
- root ownership stays in `UIFlowController`.

Therefore, do not rewrite the menu architecture. Change the fixed contract, serialized composition, validation, tests, and docs.

---

# 3. Locked product and naming decisions

## 3.1 Stable code identities

Use:

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

Reasons:

- `CombatLoadout` cannot be mistaken for the physical sickle, axe, or mining tools.
- The player-facing name can later change from `Loadout` to something thematic such as `Infusions` without another enum or serialized-data migration.
- `FieldNotes` remains stable even if its final player-facing label later becomes Notebook, Almanac, Journal, or another thematic term.
- Values remain contiguous, preserving the current test and Inspector conventions after the serialized migration is complete.

## 3.2 Player-facing display names

Author these on the five `GameplayMenuTabRegistration` entries:

| Stable ID | Display name |
|---|---|
| `Gear` | `Gear` |
| `CombatLoadout` | `Loadout` |
| `Satchel` | `Satchel` |
| `FieldNotes` | `Field Notes` |
| `Map` | `Map` |

Do not rely on `id.ToString()` for the two multi-purpose labels.

## 3.3 Provisional empty-state copy

Keep the existing Gear, Satchel, and Map copy unless the current prefab differs from the authoritative spec.

Use:

### Combat Loadout

- **Title:** `Nothing fitted`
- **Body:** `Anything you recover that can alter your fighting style will be kept here.`

### Field Notes

- **Title:** `Blank pages`
- **Body:** `Recipes, promises, and discoveries will gather here as you learn more about the world.`

These are player-facing empty states, not developer placeholders. Do not use “coming soon”, “not implemented”, “TODO”, “placeholder”, “WIP”, or similar wording.

## 3.4 Visual reuse

This is an information-architecture correction, not a visual redesign.

- Reuse the current Tools tab accent and decorative motif for Combat Loadout.
- Reuse the current Recipes tab accent and decorative motif for Field Notes.
- Preserve Gear, Satchel, and Map accents.
- Preserve the top-centred horizontal tab strip.
- Preserve `stripCellSpacing`, `stripReservedWidth`, button dimensions, active-tab accent behaviour, and compact-title behaviour unless a broken layout is directly observed.
- Do not introduce final icon art, animation, TextMeshPro, new fonts, or a second navigation system.

---

# 4. Architecture constraints

The following ownership boundaries must remain unchanged:

- `UIFlowController` remains the sole root-flow coordinator.
- `GameManager` remains the pause/time-scale owner.
- `GameplayMenuScreen` owns only active-tab state, runtime selection memory, tab cycling, strip presentation, and Back delegation.
- `GameplayMenuScreen` must not reference `SaveManager`, gameplay state owners, scene loaders, `HeroInputReader`, or Sandbox controllers.
- `IGameplayMenuTab` remains narrow and unchanged.
- `GameplayMenuEmptyTabView` remains a presentation-only placeholder presenter.
- Gear remains read-only and backed by `PlayerAbilityState` plus `GearDisplayCatalog`.
- No save schema or save version changes.
- No `PlayerPrefs`.
- No new global manager.
- No changes to hero movement, combat, ability tuning, Animancer, camera, or scene-transition architecture.
- No change to Input Actions or current bindings.
- No practical harvesting-tool system.
- No Combat Loadout domain owner.
- No Field Notes domain owner.
- No nested Field Notes section navigation in this pass.
- No map implementation.

---

# 5. Important enum-serialization warning

Unity serializes enum fields as integers. The old values are:

- Gear `0`
- Tools `1`
- Satchel `2`
- Recipes `3`
- Tasks `4`
- Journal `5`
- Map `6`

The new values make Map `4`.

After changing the enum, any unsaved old Map registration still contains integer `6` and is invalid until the prefab is re-authored. The old Tasks entry temporarily appears to have the new Map integer value `4`.

Therefore:

1. Preserve or rename source prefab assets first.
2. Change the enum.
3. Force a genuine Unity script recompile/domain reload.
4. Re-author all five serialized registrations in the same pass.
5. Save the production Gameplay Menu prefab.
6. Verify the nested production and Sandbox instances.
7. Search for undefined/stale enum values before finishing.

Do not rely only on Hot Reload for this enum change. A proper Unity compile/domain reload is required before trusting Inspector enum values.

No save migration is required because last-viewed-tab and per-tab selection memory are runtime-only and are not written to SaveData or PlayerPrefs.

---

# 6. Asset migration strategy

Use Unity Editor APIs, Unity MCP, or Prefab Mode. Do **not** hand-edit Unity YAML unless no safe Editor route exists.

Preserve `.meta` GUIDs for the two repurposed prefabs.

## 6.1 Rename, do not recreate

Move through `AssetDatabase.MoveAsset` or an equivalent Unity-aware operation:

```text
Assets/_Project/Prefabs/UI/GameplayMenu/ToolsTab.prefab
→ Assets/_Project/Prefabs/UI/GameplayMenu/CombatLoadoutTab.prefab
```

```text
Assets/_Project/Prefabs/UI/GameplayMenu/RecipesTab.prefab
→ Assets/_Project/Prefabs/UI/GameplayMenu/FieldNotesTab.prefab
```

Move their `.meta` files with them automatically. Do not delete and recreate these two prefabs.

## 6.2 Remove obsolete prefab assets only after references are removed

Delete after the Gameplay Menu prefab has been migrated:

```text
Assets/_Project/Prefabs/UI/GameplayMenu/TasksTab.prefab
Assets/_Project/Prefabs/UI/GameplayMenu/TasksTab.prefab.meta
Assets/_Project/Prefabs/UI/GameplayMenu/JournalTab.prefab
Assets/_Project/Prefabs/UI/GameplayMenu/JournalTab.prefab.meta
```

## 6.3 Repurposed prefab contents

### `CombatLoadoutTab.prefab`

- Rename root GameObject from `ToolsTab` to `CombatLoadoutTab`.
- Keep `GameplayMenuEmptyTabView`.
- Keep `Content` inactive by default.
- Keep `firstSelection` null.
- Change title/body to the approved copy.
- Preserve the existing Tools accent/motif.

### `FieldNotesTab.prefab`

- Rename root GameObject from `RecipesTab` to `FieldNotesTab`.
- Keep `GameplayMenuEmptyTabView`.
- Keep `Content` inactive by default.
- Keep `firstSelection` null.
- Change title/body to the approved copy.
- Preserve the existing Recipes accent/motif.
- Do not add Recipes/Tasks/Journal sub-buttons yet.

---

# 7. Production prefab migration

## 7.1 `GameplayMenuScreen.prefab`

Target final hierarchy:

```text
GameplayMenuScreen
└── Panel                         inactive by default
    └── Frame
        ├── HeaderBar
        │   ├── PreviousTabHint
        │   ├── StripViewport
        │   │   └── TabStrip
        │   │       ├── Tab_Gear
        │   │       ├── Tab_CombatLoadout
        │   │       ├── Tab_Satchel
        │   │       ├── Tab_FieldNotes
        │   │       └── Tab_Map
        │   ├── NextTabHint
        │   └── active title / Close
        ├── AccentLine
        └── ContentHost
            ├── GearTab
            ├── CombatLoadoutTab
            ├── SatchelTab
            ├── FieldNotesTab
            └── MapTab
```

Required operations:

1. Keep the Gear, Tools/CombatLoadout, Satchel, Recipes/FieldNotes, and Map nested instances.
2. Remove `Tab_Tasks` and `Tab_Journal`.
3. Remove `TasksTab` and `JournalTab`.
4. Rename `Tab_Tools` to `Tab_CombatLoadout`.
5. Change its visible label to `Loadout`.
6. Rename `Tab_Recipes` to `Tab_FieldNotes`.
7. Change its visible label to `Field Notes`.
8. Ensure strip sibling order is exactly Gear, CombatLoadout, Satchel, FieldNotes, Map.
9. Ensure content sibling order is exactly Gear, CombatLoadout, Satchel, FieldNotes, Map.
10. Keep every tab button active and interactable.
11. Keep every tab content root inactive by default.
12. Keep the outer `Panel` inactive by default.
13. Keep `PreviousTabHint`, `NextTabHint`, Close, active title label, action references, EventSystem reference seam, and input asset reference unchanged.
14. Keep `wrapTabCycling = true`.
15. Keep Gear’s production `PlayerAbilityState` and `GearDisplayCatalog` references unchanged.
16. Rebuild the serialized `tabs` list to exactly five elements.

Target serialized registrations:

```text
0:
  id: Gear
  displayName: Gear
  tabButton: Tab_Gear / GameplayMenuTabButton
  tabViewBehaviour: GearTab / GearScreen
  firstSelectionFallback: preserve current Gear value

1:
  id: CombatLoadout
  displayName: Loadout
  tabButton: Tab_CombatLoadout / GameplayMenuTabButton
  tabViewBehaviour: CombatLoadoutTab / GameplayMenuEmptyTabView
  firstSelectionFallback: null

2:
  id: Satchel
  displayName: Satchel
  tabButton: Tab_Satchel / GameplayMenuTabButton
  tabViewBehaviour: SatchelTab / GameplayMenuEmptyTabView
  firstSelectionFallback: null

3:
  id: FieldNotes
  displayName: Field Notes
  tabButton: Tab_FieldNotes / GameplayMenuTabButton
  tabViewBehaviour: FieldNotesTab / GameplayMenuEmptyTabView
  firstSelectionFallback: null

4:
  id: Map
  displayName: Map
  tabButton: Tab_Map / GameplayMenuTabButton
  tabViewBehaviour: MapTab / GameplayMenuEmptyTabView
  firstSelectionFallback: null
```

Do not reconstruct the entire screen prefab. Preserve existing object references and shared prefab instances wherever possible.

## 7.2 `_GameCameras.prefab`

The persistent production root nests `GameplayMenuScreen.prefab`.

After saving the standalone Gameplay Menu prefab:

- Open `_GameCameras.prefab`.
- Confirm there is still exactly one `GameplayMenuScreen`.
- Confirm it remains under `MenuRoot/RootInterfaceLayer`.
- Confirm `UIFlowController.gameplayMenuRootBehaviour` still references it.
- Confirm the production EventSystem/action-reference overrides remain valid.
- Confirm Gear still references production state/catalogue assets.
- Confirm there are exactly five registrations.
- Confirm no stale overrides target removed Tasks or Journal objects.
- Confirm no missing prefab references or missing scripts.
- Do not modify Pause, Modal, HUD, Fade, sorting, or input ownership.

## 7.3 `UISandbox.unity`

The Sandbox uses the real Gameplay Menu prefab but runtime-isolated Gear fixtures.

After prefab propagation:

- Open `Assets/_Project/Scenes/UISandbox.unity`.
- Confirm its nested Gameplay Menu has five tabs.
- Confirm `UISandboxController.gameplayMenuScreen` is still assigned.
- Confirm `UISandboxController.gearScreen` is still assigned.
- Confirm Sandbox-local EventSystem and flow references remain valid.
- Confirm the Gear fixture buttons still drive the same nested `GearScreen`.
- Confirm no serialized button/event references point to removed Tasks or Journal controls.
- Preserve isolated runtime state and no-production-save behaviour.
- Keep the Sandbox excluded from enabled Build Settings scenes.
- Do not add Loadout or Field Notes fixture data.

---

# 8. Runtime code changes

## 8.1 `GameplayMenuTabId.cs`

Replace the enum and rewrite its summary to describe five confirmed top-level destinations.

Use the exact enum shown in section 3.1.

## 8.2 `GameplayMenuScreen.cs`

Change `FixedTabOrder` to:

```csharp
public static readonly GameplayMenuTabId[] FixedTabOrder =
{
    GameplayMenuTabId.Gear,
    GameplayMenuTabId.CombatLoadout,
    GameplayMenuTabId.Satchel,
    GameplayMenuTabId.FieldNotes,
    GameplayMenuTabId.Map
};
```

Keep:

```csharp
public const GameplayMenuTabId DefaultTab = GameplayMenuTabId.Gear;
```

Update only number/name-specific comments, tooltips, headers, and diagnostics.

Recommended cleanup:

- Change `[Header("Tabs — exactly seven, in fixed order")]` to `[Header("Tabs — fixed order")]`.
- Remove number-specific wording from general runtime comments where `FixedTabOrder.Length` already defines the contract.
- Build the wrong-count error text from `FixedTabOrder` rather than a manually typed tab list.

Example:

```csharp
private static string DescribeFixedTabOrder()
{
    return string.Join(", ", FixedTabOrder);
}
```

Then:

```csharp
Debug.LogError(
    $"[GameplayMenuScreen] '{name}' has {tabs.Count} tab registration(s); exactly " +
    $"{FixedTabOrder.Length} are required ({DescribeFixedTabOrder()}).",
    this);
```

Do not change:

- `Show`
- `Hide`
- `SelectTab`
- `CycleTab`
- selection memory
- Back routing
- action subscription ownership
- `BuildStripNavigation` algorithm
- `WireActiveTabNavigation`
- compact-strip calculation
- root close event

These already operate from the authored list/count.

## 8.3 `GameplayMenuTabRegistration.cs`

Code behaviour remains unchanged.

Update comments from fixed seven-element/all-seven wording to fixed confirmed top-level tabs.

Keep `displayName` as the player-facing rename seam.

## 8.4 `GameplayMenuEmptyTabView.cs`

Code behaviour remains unchanged.

Update class comments to list:

- Combat Loadout
- Satchel
- Field Notes
- Map

Change “tab rail” wording to “tab strip” where present.

## 8.5 `UISandboxController.cs`

No behavioural changes are required.

Only update terminology/comments if they describe seven tabs, Tools, Recipes, Tasks, or Journal as separate top-level destinations.

Do not add fixture state for Combat Loadout or Field Notes.

---

# 9. Validator changes

File:

`Assets/_Project/Scripts/Editor/Validation/UIFoundationValidator.cs`

Required updates:

1. Change package summary from seven-tab to five-tab Gameplay Menu.
2. Update `ValidateGameplayMenuScreen` comments and diagnostics.
3. Continue using `GameplayMenuScreen.FixedTabOrder.Length`; do not hardcode `5` in production validator logic.
4. Update “all seven tabs” messages to “all confirmed tabs” or current count.
5. Update `ValidateTopTabStrip` comments and messages.
6. Continue requiring:
   - exactly one Gameplay Menu root;
   - correct layer;
   - inactive visual root;
   - five registrations in fixed order;
   - unique IDs;
   - active/interactable buttons;
   - valid `IGameplayMenuTab` presenters;
   - valid player-facing empty copy;
   - no gameplay-owner/save references;
   - valid action references;
   - correct Gear production references;
   - no Sandbox leakage.
7. Keep Map’s World Graph/visitation boundary unchanged in tests/specs.
8. Prefer reading enum serialized values with `SerializedProperty.intValue` rather than `enumValueIndex`.

Example:

```csharp
GameplayMenuTabId id =
    (GameplayMenuTabId)element.FindPropertyRelative("id").intValue;
```

This is more robust than assuming enum declaration index always equals underlying integer value.

9. Add or retain diagnostics that make a stale old ID obvious, for example:

```csharp
if (!System.Enum.IsDefined(typeof(GameplayMenuTabId), id))
{
    Debug.LogError(
        $"[UIFoundationValidator] Gameplay Menu tab {i} contains undefined serialized ID value {(int)id}.");
    issues++;
    continue;
}
```

10. `ValidateTopTabStrip` should expect `FixedTabOrder.Length` cells. Preserve horizontal layout, hints, active cells, and positive expanded widths.

Do not run the validator during Codex’s implementation session. Update it and leave execution to Sam.

---

# 10. EditMode test updates

Codex must update tests so they compile and assert the new contract, but must not execute them.

## 10.1 `GameplayMenuScreenTests.cs`

Required replacements:

- `FixedTabOrderIsTheSevenConfirmedTabs`
  → `FixedTabOrderIsTheFiveConfirmedTabs`
- expected order:
  - Gear
  - CombatLoadout
  - Satchel
  - FieldNotes
  - Map
- `Journal` test target
  → `FieldNotes`
- `Tools` test target
  → `CombatLoadout`
- `Recipes` click target
  → `FieldNotes`
- `Tasks` duplicate-subscription target
  → `FieldNotes` or `CombatLoadout`
- `StripNavigationChainsAllSevenTabsHorizontallyWithWraparound`
  → `StripNavigationChainsAllFiveTabsHorizontallyWithWraparound`
- wrong-count expected message:
  - `exactly 7 are required`
  → `exactly 5 are required`

Keep all behavioural coverage:

- one active tab;
- all tabs selectable;
- Gear default;
- runtime last-tab memory;
- missing remembered tab falls back to Gear;
- previous/next order and wrap;
- hidden cycling rejected;
- modal cycling rejected;
- tab button clicks;
- non-null selection;
- content selection preference;
- per-tab content memory;
- empty-tab strip fallback;
- strip/content focus policy;
- explicit navigation;
- idempotent hide;
- no duplicate subscriptions;
- Back delegation;
- modal ownership;
- close request seam;
- wrong order;
- duplicate ID;
- invalid presenter;
- unregistered tab rejection.

Use `FieldNotes` as the second fake content-bearing tab in tests that need content on two tabs.

## 10.2 `GameplayMenuProductionAssetTests.cs`

Update `TabPrefabPaths` to:

```csharp
private static readonly string[] TabPrefabPaths =
{
    "Assets/_Project/Prefabs/UI/GameplayMenu/GearTab.prefab",
    "Assets/_Project/Prefabs/UI/GameplayMenu/CombatLoadoutTab.prefab",
    "Assets/_Project/Prefabs/UI/GameplayMenu/SatchelTab.prefab",
    "Assets/_Project/Prefabs/UI/GameplayMenu/FieldNotesTab.prefab",
    "Assets/_Project/Prefabs/UI/GameplayMenu/MapTab.prefab",
};
```

Required test changes:

- `ExactlySevenTabsAreRegisteredInTheConfirmedOrder`
  → `ExactlyFiveTabsAreRegisteredInTheConfirmedOrder`
- assert `tabs.arraySize == GameplayMenuScreen.FixedTabOrder.Length`
- update order diagnostic text.
- `SixSiblingTabsUseTheSharedEmptyPresenterAndGearIsTheOnlyDataBackedTab`
  → `FourSiblingTabsUseTheSharedEmptyPresenterAndGearIsTheOnlyDataBackedTab`
- assert four `GameplayMenuEmptyTabView` components.
- use `intValue` for serialized enum reads.
- preserve all input, layer, Gear, Sandbox, empty-copy, and Map-boundary tests.

Add a focused display-name contract test:

```text
Gear          → Gear
CombatLoadout → Loadout
Satchel       → Satchel
FieldNotes    → Field Notes
Map           → Map
```

Add a superseded-asset cleanup test asserting the old paths no longer load:

```text
ToolsTab.prefab
RecipesTab.prefab
TasksTab.prefab
JournalTab.prefab
```

This prevents the old seven-tab asset set silently returning.

## 10.3 `GameplayMenuPlayModeTests.cs`

Required updates:

- `AllSevenTabsAreVisibleAndReachableWhileOpen`
  → `AllFiveTabsAreVisibleAndReachableWhileOpen`
- internal expected IDs:
  - Gear
  - CombatLoadout
  - Satchel
  - FieldNotes
  - Map
- also assert player display names:
  - Gear
  - Loadout
  - Satchel
  - Field Notes
  - Map
- `NextTabActionAdvancesThroughEveryTabInOrder` expected sequence:
  - CombatLoadout
  - Satchel
  - FieldNotes
  - Map
- replace hardcoded `for (int i = 0; i < 7; i++)` with the expected registration count.
- preserve wrap Gear ↔ Map.
- preserve root flow, pause ownership, close, Cancel, selection, last-viewed tab, and repeated-cycle coverage.

## 10.4 Other tests

`UISandboxControllerFixtureTests` currently covers health/resource fixture controls and does not require structural changes.

`GearScreenTests`, `UIFlowControllerTests`, `PlayerPersistentStateTests`, and hero-input tests should not need behavioural changes. Do not edit them merely to increase scope.

---

# 11. Documentation updates

## 11.1 Authoritative documents

Update:

- `Docs/FeatureSpecs/GameplayMenu.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/Architecture.md`
- `Docs/ImplementationPlan.md`
- `Docs/ImplementationPlans/UIImplementationPlan.md`

## 11.2 Historical planning record

Do not rewrite the completed 1,600-line Package A2 plan as though it originally specified five tabs.

At the top of:

`Docs/ImplementationPlans/UIA2ImplementationPlan.md`

add a clear historical notice:

```markdown
> **Historical implementation record.**
> Package A2 was implemented with seven top-level Gameplay Menu tabs.
> That product decision was superseded on 2026-07-29 by the post-A2
> five-tab information-architecture correction documented in
> `Docs/FeatureSpecs/GameplayMenu.md`.
```

Update only plainly misleading current-status wording elsewhere in that historical file if essential.

## 11.3 `GameplayMenu.md` target contract

Rewrite the fixed top-level order to:

```text
Gear · Loadout · Satchel · Field Notes · Map
```

Document stable IDs separately:

```text
Gear · CombatLoadout · Satchel · FieldNotes · Map
```

Document:

- all five are visible and reachable;
- Gear is currently the only data-backed tab;
- the other four use authored empty states;
- Loadout is the future combat-build destination, not literal gathering tools;
- Field Notes will eventually contain Recipes, Tasks, and Journal as internal sections;
- no internal Field Notes section navigation exists yet;
- no domain owners are invented in this pass;
- top strip remains horizontal;
- runtime memory remains unsaved;
- Quick Map remains deferred;
- current input bindings remain provisional and unchanged.

Update the deferred table to:

| Tab | Missing dependency |
|---|---|
| Combat Loadout | product rules, slot/capacity model, modifiers, equip state, owner, persistence |
| Satchel | inventory definitions, quantities, categories, owner, persistence |
| Field Notes | recipe knowledge, task progression, journal/discovery owners, internal navigation |
| Map | stable room catalogue, visited-room snapshot, current-room resolver, authored geometry, markers, pan/zoom |

## 11.4 `Gear.md` clarification

Keep Gear’s implemented behaviour unchanged.

Add the distinction:

- Gear is permanent physical progression.
- Combat Loadout is player-configurable combat build composition.
- Literal sickle/axe/mining capabilities are not automatically Combat Loadout items.
- Their eventual Gear presentation depends on their real owner and persistence.
- This correction does not implement or promise an equipment-swapping system.

Update sibling-tab references from six/seven to four/five.

## 11.5 `ImplementationPlan.md`

Add a post-A2 correction entry, with status accurately reflecting the implementation result.

Replace current future references to separate production Tools, Recipes, Tasks, and Journal tabs with:

- production Combat Loadout;
- production Satchel;
- production Field Notes internal sections;
- production Map.

Do not claim tests, validator, Sandbox Play Mode, or production Boot-path validation passed during this pass. State explicitly that Codex updated coverage but Sam will execute validation afterward.

---

# 12. Recommended implementation order

Perform as one pass. Do not stop for approval between stages unless a hard repository contradiction is discovered.

## Stage 1 — Baseline and safe asset moves

1. Confirm branch and clean/known working tree.
2. Record current HEAD.
3. Do not discard unrelated user changes.
4. Move Tools prefab to CombatLoadout, preserving GUID.
5. Move Recipes prefab to FieldNotes, preserving GUID.
6. Edit the two renamed source prefabs.

## Stage 2 — Runtime contract

1. Update `GameplayMenuTabId`.
2. Update `GameplayMenuScreen.FixedTabOrder`.
3. Update diagnostics/comments.
4. Update registration/empty-presenter comments.
5. Force Unity compile/domain reload.
6. Resolve compiler errors caused by removed enum names.
7. Do not run tests.

## Stage 3 — Serialized production and Sandbox composition

1. Migrate `GameplayMenuScreen.prefab` from seven to five.
2. Save it.
3. Remove obsolete Tasks/Journal nested instances.
4. Delete obsolete Tasks/Journal source prefabs.
5. Open and verify `_GameCameras.prefab`.
6. Open and verify `UISandbox.unity`.
7. Save only intentional changes.
8. Refresh AssetDatabase.
9. Re-open the Gameplay Menu prefab and confirm all references.

## Stage 4 — Validator, tests, and docs

1. Update validator.
2. Update EditMode tests.
3. Update PlayMode tests.
4. Update authoritative docs.
5. Add the historical notice to the A2 plan.
6. Do not execute validator or tests.

## Stage 5 — Static completion checks only

Codex may:

- wait for one final Unity compilation;
- inspect Console for compiler errors;
- inspect missing-script/missing-reference errors if Unity reports them;
- run repository text searches;
- run `git diff --check`;
- inspect `git status --short`;
- inspect `git diff --stat`;
- review the final diff.

Codex must not:

- run EditMode tests;
- run PlayMode tests;
- invoke Unity with `-runTests`;
- repeatedly enter Play Mode;
- run the complete validator unless Sam explicitly asks later;
- claim validation passed;
- commit or push unless separately instructed.

---

# 13. Required repository searches before Codex finishes

Run token-light searches and resolve every relevant result:

```bash
rg -n \
  "GameplayMenuTabId\.(Tools|Recipes|Tasks|Journal)|\
ToolsTab\.prefab|RecipesTab\.prefab|TasksTab\.prefab|JournalTab\.prefab|\
seven-tab|seven tabs|exactly seven|AllSeven|SixSibling|\
Gear, Tools, Satchel, Recipes, Tasks, Journal, Map" \
  Assets/_Project Docs
```

Then search potentially stale player-facing labels in UI-specific paths:

```bash
rg -n \
  "\bTools\b|\bRecipes\b|\bTasks\b|\bJournal\b" \
  Assets/_Project/Scripts/UI \
  Assets/_Project/Scripts/Editor/Tests/UI \
  Assets/_Project/Tests/PlayMode/UI \
  Docs/FeatureSpecs \
  Docs/ImplementationPlan.md \
  Docs/ImplementationPlans/UIImplementationPlan.md
```

Do not blindly delete legitimate references to:

- literal harvesting tools;
- Recipes, Tasks, and Journal as future Field Notes internal sections;
- historical seven-tab implementation records clearly labelled historical.

Also search for serialized stale values and missing asset paths through Unity/AssetDatabase where possible.

---

# 14. Acceptance criteria before handing back to Sam

Codex should hand back only when static inspection indicates:

- enum has exactly five IDs;
- `FixedTabOrder` has exactly five IDs in the correct order;
- no production code references removed enum members;
- five production tab registrations exist;
- internal IDs and player display names are correct;
- four empty presenters plus one Gear presenter exist;
- old Tasks and Journal instances are gone;
- old tab prefab paths are gone;
- renamed prefabs retained their GUIDs;
- Gear references remain intact;
- `_GameCameras.prefab` remains structurally valid;
- Sandbox references remain intact;
- no input action changes occurred;
- no save changes occurred;
- no gameplay-owner references were introduced;
- validator code asserts the new contract;
- tests compile against the new contract;
- docs describe five tabs;
- historical A2 plan is labelled as superseded rather than rewritten;
- Unity reports no compiler errors after a full compile;
- tests and validation are explicitly reported as not run.

---

# 15. Sam’s validation pass after Codex finishes

## 15.1 Unity compile

1. Open the project.
2. Allow a full domain reload.
3. Confirm Console has no compiler errors.
4. Clear unrelated old logs before validating.

## 15.2 Validator

Run:

`Tools → Project → Validate UI Foundation`

Expected outcome: zero reported issues.

## 15.3 Focused EditMode tests

Run at least:

- `GameplayMenuScreenTests`
- `GameplayMenuProductionAssetTests`
- `GearScreenTests`
- `UIFlowControllerTests`
- `UISandboxControllerFixtureTests`

Then run the full UI-related EditMode assembly/suite if convenient.

## 15.4 Focused PlayMode tests

Run at least:

- `GameplayMenuPlayModeTests`
- existing UI flow/input-suspension PlayMode coverage

Do not accept skipped discovery as a pass. Confirm the intended test cases actually execute.

## 15.5 Sandbox manual checks

In `Assets/_Project/Scenes/UISandbox.unity`:

1. Open Gameplay Menu.
2. Verify order:
   - Gear
   - Loadout
   - Satchel
   - Field Notes
   - Map
3. Verify Q/E and LB/RB cycle in that order.
4. Verify wrap Gear ↔ Map.
5. Verify mouse clicks select every tab.
6. Verify active title label uses `Loadout` and `Field Notes`, not enum names.
7. Verify only one content root is active.
8. Verify empty-state copy.
9. Verify Gear None/Single/Multiple/Live Unlock/Missing Definition fixtures still work.
10. Verify Close and Cancel.
11. Verify 16:9, 16:10, 21:9, and 4:3 previews.
12. Check that five expanded labels fit cleanly at common aspect ratios.

## 15.6 Production Boot-path checks

1. Start through Boot.
2. Open Gameplay Menu with keyboard.
3. Open with controller.
4. Cycle all tabs.
5. Close with the menu action.
6. Close with Cancel.
7. Confirm Pause cannot open behind Gameplay Menu.
8. Confirm Gameplay Menu cannot replace an already-open Pause root.
9. Confirm time scale returns to 1 after close.
10. Confirm hero input does not leak through open/close.
11. Cross a room transition and re-open the menu.
12. Confirm last-viewed tab persists across room transition but resets on application restart as intended.

---

# 16. Codex-ready implementation prompt

Copy the prompt below into Codex.

---

## Prompt

Implement the post-Package-A2 Gameplay Menu information-architecture correction in the Underbrew Rebuild Unity project.

### Repository baseline

- Repository: `Perkiiii/Metroidvania-Controller`
- Branch: `main`
- Intended starting baseline: `9ef4e5255eeb9583c072d80372b557f821810372`
- Inspect the actual current working tree before editing.
- Preserve unrelated user changes.
- Do not commit or push.

### Goal

Replace the current seven top-level Gameplay Menu tabs:

`Gear → Tools → Satchel → Recipes → Tasks → Journal → Map`

with:

`Gear → Loadout → Satchel → Field Notes → Map`

This is one uninterrupted implementation pass. Do not stop between stages for confirmation unless a hard repository contradiction makes the supplied plan impossible.

### Locked meanings

- Gear: permanent physical progression possessions. Keep the current read-only ability-backed implementation unchanged.
- Loadout: future charm/crest-style combat build customisation. It is not the sickle, axe, mining, or farming-tool inventory.
- Satchel: future material/ingredient/seed/consumable inventory.
- Field Notes: future top-level container for Recipes, Tasks, and Journal/discovery as internal sections.
- Map: unchanged deferred player map destination.

Do not implement any of those deferred domain systems in this pass.

### Stable IDs and labels

Replace `GameplayMenuTabId` with:

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

Player-facing registration display names:

- Gear → `Gear`
- CombatLoadout → `Loadout`
- Satchel → `Satchel`
- FieldNotes → `Field Notes`
- Map → `Map`

`CombatLoadout` and `FieldNotes` are stable internal identities. Their display names may be rethemed later without another enum migration.

### Empty-state copy

Combat Loadout:

- Title: `Nothing fitted`
- Body: `Anything you recover that can alter your fighting style will be kept here.`

Field Notes:

- Title: `Blank pages`
- Body: `Recipes, promises, and discoveries will gather here as you learn more about the world.`

Keep existing Gear, Satchel, and Map copy unless the authoritative current spec says otherwise.

### Architecture constraints

Preserve all existing architecture:

- `UIFlowController` remains sole root-flow coordinator.
- `GameManager` remains pause/time-scale owner.
- `GameplayMenuScreen` owns only active tab, runtime selection memory, strip/cycling, and Back delegation.
- `IGameplayMenuTab` remains unchanged.
- `GameplayMenuEmptyTabView` remains presentation-only.
- Gear remains read-only and backed by `PlayerAbilityState` plus `GearDisplayCatalog`.
- No save schema/version changes.
- No PlayerPrefs.
- No new manager.
- No Input Action or binding changes.
- No hero, combat, movement, Animancer, camera, scene-flow, or game-feel changes.
- No new data owners or speculative persistence.
- No Field Notes internal navigation yet.
- No Loadout implementation.
- No practical harvesting-tool implementation.
- No Map implementation.

### Required asset moves

Use Unity-aware moves and preserve `.meta` GUIDs:

```text
ToolsTab.prefab → CombatLoadoutTab.prefab
RecipesTab.prefab → FieldNotesTab.prefab
```

Rename their root GameObjects and update their authored copy.

Reuse the current Tools accent/motif for Combat Loadout and the current Recipes accent/motif for Field Notes.

After production references are removed, delete:

```text
TasksTab.prefab (+ meta)
JournalTab.prefab (+ meta)
```

Do not recreate the renamed prefabs with new GUIDs.

### Enum migration safety

Unity serializes enum integers. Old Map is `6`; new Map is `4`.

Do not rely only on Hot Reload.

1. Move/prepare assets.
2. Change the enum and runtime references.
3. Force a real Unity compile/domain reload.
4. Re-author every serialized tab registration.
5. Save the prefab.
6. Verify production and Sandbox nested instances.

No save migration is needed because menu tab memory is runtime-only.

### Runtime code

Update:

- `Assets/_Project/Scripts/UI/GameplayMenu/GameplayMenuTabId.cs`
- `Assets/_Project/Scripts/UI/Screens/GameplayMenuScreen.cs`
- comments in `GameplayMenuTabRegistration.cs`
- comments in `GameplayMenuEmptyTabView.cs`
- stale terminology in `UISandboxController.cs` only if present

Set `GameplayMenuScreen.FixedTabOrder` to:

```csharp
Gear,
CombatLoadout,
Satchel,
FieldNotes,
Map
```

Keep Gear as default.

Do not rewrite tab switching, cycling, selection memory, Back routing, input subscription, explicit navigation, or compact-strip algorithms. They already use the authored list/count.

Remove brittle hard-coded seven-tab diagnostic wording. Use `FixedTabOrder.Length` and build the diagnostic list from `FixedTabOrder`.

### GameplayMenuScreen prefab

Migrate the production prefab to:

Tab strip:

```text
Tab_Gear
Tab_CombatLoadout
Tab_Satchel
Tab_FieldNotes
Tab_Map
```

Content host:

```text
GearTab
CombatLoadoutTab
SatchelTab
FieldNotesTab
MapTab
```

Remove Tasks and Journal buttons/content.

Set exactly five serialized registrations with the correct IDs, display names, buttons, presenters, and order.

Keep:

- Panel inactive by default
- all buttons active/interactable
- all tab content roots inactive by default
- `wrapTabCycling = true`
- action references
- active title label
- Close control
- Previous/Next hints
- production Gear state/catalog references
- existing top-centred horizontal strip and layout values

Do not reconstruct unrelated parts of the prefab.

### Production and Sandbox integration

Verify and save intentional changes in:

- `Assets/_Project/Prefabs/Managers/_GameCameras.prefab`
- `Assets/_Project/Scenes/UISandbox.unity`

Production requirements:

- exactly one Gameplay Menu root;
- still under `MenuRoot/RootInterfaceLayer`;
- `UIFlowController.gameplayMenuRootBehaviour` intact;
- production EventSystem/action refs intact;
- Gear production refs intact;
- no stale Tasks/Journal overrides;
- no Sandbox leakage.

Sandbox requirements:

- real five-tab prefab;
- `UISandboxController.gameplayMenuScreen` intact;
- `UISandboxController.gearScreen` intact;
- isolated runtime Gear fixtures intact;
- no production state/save access;
- no Loadout/Field Notes fixture domain data;
- Sandbox remains excluded from enabled Build Settings.

### Validator

Update `UIFoundationValidator.cs` to enforce the five-tab contract while continuing to derive count from `GameplayMenuScreen.FixedTabOrder.Length`.

Replace enum `enumValueIndex` reads with `intValue`.

Add an undefined-enum-value diagnostic so stale old Map value `6` is reported clearly.

Keep all existing production/Sandbox, input, layering, Gear, empty-state, and ownership checks.

### Tests — update but DO NOT RUN

Update:

- `GameplayMenuScreenTests.cs`
- `GameplayMenuProductionAssetTests.cs`
- `GameplayMenuPlayModeTests.cs`

Required new expected order:

```text
Gear
CombatLoadout
Satchel
FieldNotes
Map
```

Player-facing labels:

```text
Gear
Loadout
Satchel
Field Notes
Map
```

Update seven/five and six/four names/counts, replace old enum references, and remove hardcoded cycling loop `7`.

Keep all existing behavioural coverage.

Add production-asset coverage that:

- registration display names are correct;
- old Tools/Recipes/Tasks/Journal prefab paths no longer exist;
- exactly four empty presenters plus one Gear presenter ship.

Do not run EditMode or PlayMode tests. Do not invoke `-runTests`. Do not repeatedly enter Play Mode. Sam will run all validation afterward.

### Documentation

Update current authoritative docs:

- `Docs/FeatureSpecs/GameplayMenu.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/Architecture.md`
- `Docs/ImplementationPlan.md`
- `Docs/ImplementationPlans/UIImplementationPlan.md`

Add a historical supersession notice to the top of:

- `Docs/ImplementationPlans/UIA2ImplementationPlan.md`

Do not rewrite that historical plan as though it originally specified five tabs.

Document Loadout versus literal gathering tools, and Field Notes containing future Recipes/Tasks/Journal sections.

Do not claim tests, validator, Play Mode, Sandbox live verification, or production Boot-path validation passed.

### Final static checks only

You may:

- force one final Unity compile/domain reload;
- inspect Console for compiler errors;
- inspect missing references reported by Unity;
- run `rg` searches for stale names;
- run `git diff --check`;
- inspect diff/status.

Do not run tests or the validator.

Before finishing, resolve relevant results from:

```bash
rg -n \
  "GameplayMenuTabId\.(Tools|Recipes|Tasks|Journal)|\
ToolsTab\.prefab|RecipesTab\.prefab|TasksTab\.prefab|JournalTab\.prefab|\
seven-tab|seven tabs|exactly seven|AllSeven|SixSibling|\
Gear, Tools, Satchel, Recipes, Tasks, Journal, Map" \
  Assets/_Project Docs
```

Preserve legitimate references to literal tools, future Field Notes sections, and clearly-labelled historical records.

### Final response format

Return:

1. starting branch/HEAD and working-tree state;
2. implementation summary;
3. exact code files changed;
4. exact prefab/scene/assets changed, moved, and deleted;
5. documentation changed;
6. tests updated;
7. explicit statement: tests were not run;
8. explicit statement: validator was not run;
9. compile/static inspection performed and any limitations;
10. remaining manual Unity work for Sam;
11. `git status --short`;
12. no commit/push confirmation.

Do not claim anything passed that was not executed.

---

# 17. Recommended pass name

Use one of:

- `Gameplay Menu Five-Tab Correction`
- `Post-A2 Gameplay Menu IA Correction`
- `UI Package A2.1 — Five-Tab Information Architecture`

Recommended internal milestone label:

**UI Package A2.1 — Five-Tab Information Architecture Correction**

This communicates that the work corrects the already-implemented Package A2 structure without pretending to implement Package B or any future tab domain.
