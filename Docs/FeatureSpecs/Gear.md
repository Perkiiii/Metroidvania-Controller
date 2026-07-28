# Feature Spec — Gear

**Last reviewed:** 2026-07-27  
**Status:** Authoritative player-facing Gear contract. Implemented in Package A2: `GearScreen`,
`GearDisplayDefinition`, `GearDisplayCatalog`, `GearEntryView`, `GearDetailsPanel`, the `GearTab`
and `GearEntryView` prefabs, EditMode/PlayMode coverage, and validator rules.

The production `GearDisplayCatalog.asset` is intentionally **empty**. No physical Gear identity,
name, artwork, category, description, or flavour text has been approved yet, and a raw `AbilityId`
name is not an acceptable substitute. Gear therefore ships as a functional screen presenting its
authored empty state; adding an approved definition asset is all that is needed to populate it.

## Purpose

Gear shows physical permanent progression possessions. It helps the player understand what has been
acquired and provides useful thematic description, functional explanation, and control information.

Gear is separate from Satchel. Satchel will eventually contain seeds, crops, ingredients, farming
resources, enemy drops, processing/crafting materials, and consumables. Recipes are portable
discovered knowledge and do not enable remote production.

## Confirmed starting state

- A new player starts with no unlocked permanent abilities.
- A new player starts with no acquired ability-granting Gear.
- Dash is acquired through progression.
- Wall Cling is acquired through progression.
- The initial Gear collection may legitimately be empty.
- That empty collection is intentional, not an error or missing-data state.
- Undiscovered possessions are not shown as placeholder slots.

**Prerequisite resolved (Package A2 Stage 0).** `PlayerAbilityState`'s field initializers and
`ResetToDefaults`, `AbilitySaveData`'s defaults, and the mutable `PlayerAbilityState.asset` now all
begin with every one of the eight abilities locked. Existing saves are unaffected:
`GatherSaveData` writes all eight booleans explicitly, so deserialization overwrites the new
defaults with the stored values, and a save whose ability section is missing or null receives the
all-locked default. The save schema version was not changed, because no migration behaviour
changed. See `Docs/FeatureSpecs/Abilities.md` and `Docs/FeatureSpecs/SaveSystem.md`.

A true new game therefore shows the Gear empty state, which is the intended presentation. To test
unlocked Gear, use the UI Sandbox fixtures (`UISandboxController` creates isolated runtime ability
state and a fixture catalogue) or acquire abilities through their pickups — never by editing the
production asset back to unlocked.

## Non-goals

Gear is not:

- A skill tree.
- A skill-point or purchasing interface.
- An ability-unlocking interface.
- An ability-equipping interface for permanent traversal abilities.
- An ability-tuning interface.
- The material inventory or Satchel.
- Recipes.
- Generic item ownership.
- A complete weapon/equipment system.
- An excuse to invent ownership or persistence for future permanent items.

## Current data owner

`PlayerAbilityState` is the implemented authority for permanent ability unlocks. Current
`AbilityId` values are:

1. Dash.
2. WallCling.
3. Sprint.
4. WallLatch.
5. DoubleJump.
6. DriftCloak.
7. SpiritCast.
8. Bind.

Package A Gear:

- Reads `PlayerAbilityState.IsUnlocked(AbilityId)`.
- Performs an explicit full refresh on initialize and every screen open.
- Subscribes to genuine `AbilityChanged` while active.
- Never mutates ownership.
- Does not require a new `StateApplied` event for Package A.
- Never treats raw ownership changes as acquisition notification.

Explicit refresh covers startup save application and reopening. A future acquisition toast must use
an explicit progression/presentation event carrying acquisition context. Save loading must never be
inferred as acquisition.

## Planned Gear presentation data

Proposed Package A `GearDisplayDefinition` fields:

- Stable display key.
- Exactly one required `AbilityId`.
- Display name.
- Optional mechanical label.
- Category.
- Icon/artwork.
- Functional description.
- Control-hint metadata.
- Flavour text.
- Display order/group.
- Optional Acquired/Active wording.

A proposed `GearDisplayCatalog` provides ordered definitions, lookup, and validation. These names
and assets are planned, not implemented.

Definitions contain no unlock state, purchase/equip state, velocities, cooldowns,
`HeroAbilityConfig` values, blackboard flags, or layout coordinates. The stable display key supports
selection restoration, view identity, validation, and tests; it is not a second gameplay ownership
key.

Package A does not add:

- Optional ownerless definitions.
- Generic ownership resolvers.
- Multiple ownership providers.
- Weapon ownership without an authoritative owner.
- Speculative permanent-item persistence.

Future non-ability Gear is added only after its gameplay owner and persistence contract exist.

## Visibility

- Only acquired mapped Gear is presented.
- A true new save initially presents zero ability-backed entries.
- A locked ability creates no button, slot, silhouette, question mark, anchor marker, or details
  page.
- No unknown totals or completion percentages are shown.
- An unlocked ability with no approved definition is omitted safely and produces a development
  diagnostic.
- Production definitions are not created merely because an enum member exists.

## Intentional empty state

The production Gear screen must remain usable with zero acquired entries. Its empty state:

- Provides a valid selectable Back/close target.
- Uses wording that does not imply an error.
- Does not reveal the number or identity of future abilities.
- Does not invent a starting possession.
- Works with keyboard, controller, and mouse.
- Transitions naturally to first-item selection when the first Gear object is acquired.

Final empty-state copy and artwork remain UI/content decisions.

## Selection and details

Selection is keyed by stable Gear display key, never current list index. When a refresh changes
visibility:

- Retain the selected key if it remains visible.
- Otherwise select the first visible entry.
- If no entries remain, select the empty-state Back target.
- Rebuild explicit navigation for the visible layout.

The selected details panel may show:

- Thematic physical item name.
- Category.
- Large artwork.
- Functional description.
- Device-appropriate control hint.
- Flavour text.
- Optional approved Acquired/Active wording.

It never displays internal tuning numbers, velocities, cooldowns, or raw enum/member names as final
player copy.

## Ability-backed physical Gear

### Double Jump

Once acquired, Double Jump is represented by a physical Underbrew traversal item. Its final name,
artwork, flavour text, and category presentation remain open design/art decisions. Do not use the
raw label “Double Jump” as the final production item name without separate approval.

### Dash and Wall Cling

Dash and Wall Cling are progression unlocks, not starting abilities. They appear in Gear only after:

1. Progression has unlocked the authoritative ability flag.
2. Their physical Gear identities and display definitions are approved.

Current default-unlocked code/development state does not authorize starting Gear definitions or
early display. Whether they start acquired is resolved: they do not.

### Other ability flags

Sprint, Wall Latch, Drift Cloak, Spirit Cast, and Bind do not automatically receive production Gear
definitions because enum members exist. Their physical identities and player-facing relevance
require separate content approval. Locked entries remain absent.

## Layout

Prototype in the UI Sandbox:

| Approach | Behavior | Strength | Risk |
|---|---|---|---|
| Dynamic authored groups | Acquired entries populate authored category containers; empty groups collapse | Responsive bespoke hierarchy | Requires navigation rebuild and group authoring |
| Authored collection/tableau | Acquired keys appear in authored zones without mystery framing | Strong physical-possession identity | More aspect-ratio and navigation work |
| Simple list/grid fallback | Visible entries in stable order beside details | Fastest and easiest to validate | May read as a generic inventory |

Recommended direction is a bespoke physical-progression presentation using authored groups or an
authored collection, subject to UI/UX review. The list/grid remains the functional comparator and
fallback. The chosen layout must support zero entries without appearing broken and must not encode
coordinates in display definitions.

## Refresh and feedback

- Full refresh on initialize and every open.
- Subscribe to `AbilityChanged` only while active.
- Retain stable-key selection where possible.
- Move correctly from empty state to the first acquired item.
- Missing unlocked definitions omit safely and log once for development.
- Duplicate stable keys or `AbilityId` mappings fail validation.
- Save application and screen reopening reflect the current snapshot neutrally.
- Save loading never triggers acquisition presentation.
- Future acquisition notification requires an explicit gameplay/presentation event.

## Gameplay Menu integration

Gear is the default tab on the first Gameplay Menu open in a play session. It is the only
data-backed Package A2 tab. Later opens remember the last valid tab for that session; if no longer
available, return to Gear. Back at the Gear root closes the Gameplay Menu through the flow in
`PauseAndMenuFlow.md`.

The sibling tabs — Tools, Satchel, Recipes, Tasks, Journal, and Map — are **visible and reachable**
in production, each presenting an authored empty state until its authoritative gameplay system
exists. They are not hidden. See `Docs/FeatureSpecs/GameplayMenu.md`. Fixture-only Sandbox content
is not production data, and `UIFoundationValidator` rejects a production Gear tab that references a
Sandbox catalogue or a Sandbox Gear tab that references the production one.

## Automated tests

Implemented in `GearScreenTests` (EditMode), `GameplayMenuProductionAssetTests` (EditMode), and
`PlayerPersistentStateTests` (Stage 0 defaults). Coverage:

- A new-game state shows zero Gear entries.
- The new-game empty state is valid and navigable.
- Dash remains absent while locked.
- Wall Cling remains absent while locked.
- Unlocking Dash makes only its approved Gear entry appear.
- Unlocking Wall Cling makes only its approved Gear entry appear.
- Other locked abilities produce no entry, placeholder, or silhouette at all.
- Reset-to-new-game returns to an empty collection.
- Existing saves with unlocked abilities display their explicitly saved unlocks.
- Empty-to-first-item selection remains valid.
- Selection is retained by stable key when possible.
- Reopening performs an explicit snapshot refresh.
- Missing unlocked definitions omit safely and report a development diagnostic.
- Duplicate keys and duplicate ability mappings are rejected.
- Save loading does not infer acquisition notification.
- Mutable production/development assets are not used as test defaults.

## Manual validation

Future validation covers true-empty, first-unlock, multi-item, all-approved, and missing-definition
states; keyboard/controller/mouse navigation; device prompt switching; details/Back focus;
selection retention; common aspect ratios and safe areas; save-load neutral refresh; and
empty-to-first-item transition.

Performed in Package A2: the empty state, several-acquired, and live-unlock states were reviewed in
a real Play Mode session through the UI Sandbox's isolated Gear fixtures at 1920x1080. Keyboard and
controller device input, mouse clicking, device prompt switching, other aspect ratios, and the
production Boot path have **not** been interactively validated.

## Required Unity Editor work

Completed in Package A2:

- `GearTab.prefab` (collection + details + empty state) and `GearEntryView.prefab`.
- `GearDisplayCatalog.asset` created and assigned, alongside the production `PlayerAbilityState`.
- Intentional empty state authored; global Close is always a valid focus target.
- Explicit list navigation built at runtime, with Up from the first entry returning to the Gear
  button in the shared top strip.
- Isolated Sandbox fixtures for zero / one / several / live-unlock / missing-definition states.

Still required, pending approval:

- Author approved `GearDisplayDefinition` assets — physical identity, name, category, artwork,
  functional description, flavour text, and optional control hint — one per approved ability.
  Nothing else is blocking a populated Gear screen.

## Provisional decisions to review

- Gear presents as a list plus details panel rather than a bespoke physical tableau. The list
  composition was chosen as the reliable, navigable first pass; the authored-collection option in
  `UIImplementationPlan.md` remains open.
- No ScrollRect: the acquired set is at most eight entries and fits the panel.
- The `"CARRIED"` section label and the empty-state copy are drafts.

## Risks

- Mutable `PlayerAbilityState.asset` values masquerade as intended starting content.
  *Guarded:* `PlayerPersistentStateTests.ProductionAbilityStateAssetShipsFullyLocked` fails if the
  asset is edited back to unlocked.
- The prerequisite changes defaults but overwrites explicit existing-save unlocks.
  *Guarded:* `ExistingSaveWithExplicitUnlocksIsAppliedUnchanged` and a full 256-combination
  round-trip test.
- Layout framing reveals unknown totals or future identities.
- Raw mechanical ability names become final physical item names.
- A definition becomes a second ownership or tuning source.
- Save loading is misrepresented as acquisition.
- Future enum members are exposed before gameplay/content approval.

## Deferred work

- Non-ability permanent Gear after real ownership/persistence exists.
- Production Tools, Satchel, Recipes, Tasks, Journal, and Map.
- Acquisition notifications.
- Complete weapon/equipment systems.
- Final glyph/localization/accessibility support.

## Open decisions

- Final layout after Sandbox prototypes.
- Final empty-state copy and artwork.
- Final physical names, categories, artwork, and flavour text, especially Double Jump.
- Approved physical identities/content for Dash, Wall Cling, and other ability flags.
- Final control-hint/glyph/localization strategy.

Dash and Wall Cling starting ownership is not open: both begin locked and appear only after
progression acquisition and approved physical representation.

## Related specifications

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/PauseAndMenuFlow.md`
- `Docs/FeatureSpecs/UISandbox.md`
- `Docs/FeatureSpecs/Abilities.md`
