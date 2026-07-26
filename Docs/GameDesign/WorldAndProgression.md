# Underbrew — World and Progression Framework

**Status:** Working progression design  
**Version:** 0.1  
**Created:** 2026-07-26  
**Parent document:** `Docs/GameDesign/UnderbrewGameDesign.md`

> This document defines the intended progression structure for the complete game. It separates progression design from implementation status.
>
> - `Docs/Architecture.md` remains authoritative for system ownership and technical boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what currently exists and what should be implemented next.
> - `Docs/FeatureSpecs/Abilities.md` remains authoritative for the contracts of implemented ability systems.
> - `Docs/HeroFeelTuning.md` remains authoritative for validated movement and combat values.
> - `Docs/GameDesign/OpeningChapter.md` owns the fixed opening sequence that leads into this framework.

---

## 1. Purpose

Underbrew needs enough concrete progression planning to distribute meaningful rewards across its regions without turning the game into a fixed linear chain.

This document defines:

- the player's starting kit;
- major ability progression;
- farming and crop tiers;
- Hearthstead growth stages;
- tool unlocks;
- crafting and processing unlocks;
- alchemy progression;
- health, resource, inventory, and loadout progression;
- resident and service progression;
- Hearthseed and world-restoration progression;
- rules for combining those rewards into regions;
- a macro progression structure that supports player-directed exploration.

The specific final region names, bosses, and reward locations will be assigned in a later world-map pass.

---

## 2. Progression Philosophy

### 2.1 Progress through capability, not level numbers

Underbrew should not use a broad character-level or gear-score system as its primary progression.

The player progresses because:

- the hero gains new verbs;
- the player masters existing verbs;
- the Hearthstead gains new functions;
- crops and alchemy create new preparation options;
- residents bring knowledge and services;
- the Hearthseed restores the world;
- shortcuts and persistent changes reshape travel;
- knowledge makes previously confusing opportunities legible.

### 2.2 Several progression tracks should move together

A major region should rarely reward only one isolated item.

A satisfying regional arc should usually advance several of these tracks:

1. **Personal capability** — movement, combat, health, resource, or loadout.
2. **Cultivation** — crop, bed type, capacity, or growth option.
3. **Home capability** — station, placement zone, resident, or facility.
4. **World restoration** — Hearth Bloom, cleansed route, bridge, platform, or root connection.
5. **Knowledge** — recipe, map information, creature knowledge, or story truth.

These rewards should be distributed through the region rather than all dropping from one boss.

### 2.3 Player direction should matter

After the fixed opening chapter, the player should normally have more than one useful destination.

The world should communicate opportunities through:

- visible paths;
- map shapes and landmarks;
- resident suggestions;
- root signals;
- environmental clues;
- remembered gates;
- optional quest guidance.

The game should not present the main experience as a numbered checklist of regions.

### 2.4 Non-linearity must remain production-feasible

Underbrew should not attempt unrestricted open-world sequencing.

The intended model is:

- fixed opening;
- broad early choice;
- partial convergence gates;
- another open midgame;
- late-game combination requirements;
- a final convergence.

This creates freedom while allowing authored difficulty, story, and ability dependencies.

---

## 3. Progression Categories

| Category | What changes | Typical source |
|---|---|---|
| Hero abilities | New movement, combat, or utility verbs | Major discoveries, mentors, regional milestones |
| Hero capacity | Health, resource, brew slots, inventory | Optional exploration, residents, upgrades |
| Farming | Crop tiers, bed types, capacity, growth convenience | Hearthseed stages, regional seeds, residents |
| Tools | New contextual gathering and cultivation interactions | Regional objectives, residents, crafted upgrades |
| Crafting | New placeables, stations, processing, furniture | Residents, plans, materials, Hearthstead stages |
| Alchemy | New recipe categories and stronger world uses | Ingredients, knowledge, boss materials, residents |
| Hearthstead | Build zones, facilities, residents, visual life | Hearth Blooms and home objectives |
| World restoration | Cleansed routes, living bridges, root travel | Planted Hearth Blooms and major regional outcomes |
| Knowledge | Map, journal, recipes, language, ecosystem understanding | Exploration, dialogue, study, quests |
| Story | Access to deeper avian infrastructure and final conflict | Regional outcomes and restoration threshold |

---

## 4. Macro Progression Structure

### 4.1 Phase 0 — Fixed opening

```text
Woodland settlement cutscene
→ tutorial region
→ first checkpoint
→ introductory boss
→ Hearthseed
→ abandoned Hearthstead
→ first cultivation and placement
```

This phase is intentionally linear so the game can teach its baseline language.

### 4.2 Phase 1 — Early open exploration

After planting the Hearthseed, the player should see or learn about approximately three viable regional directions.

```text
Hearthstead
├── Early Region Thread A
├── Early Region Thread B
└── Early Region Thread C
```

The player may enter, leave, and switch between these threads.

Working rule:

> Meaningful progress in any two early threads should be sufficient to trigger the first major story convergence.

The third thread remains valuable and fully completable. It may grant an ability, resident, crop family, Hearth Bloom, optional route, or later advantage.

### 4.3 Phase 2 — First convergence

Planting enough early restorative rewards strengthens the Hearthseed and reveals a deeper problem.

Possible convergence forms:

- a central root gate opens;
- an avian extraction route becomes reachable;
- a displaced resident identifies a shared source of damage;
- restored roots reveal a path beneath several regions;
- the birds respond to the Hearthstead becoming active.

The gate should be expressed through the world and Hearthseed rather than an abstract “2/3 objectives complete” interface.

### 4.4 Phase 3 — Open midgame

The first convergence opens two or three larger regions, some of which may have multiple entry routes depending on the player's abilities and restorations.

The player should be able to:

- pursue a traversal ability;
- pursue a farming or resident improvement;
- investigate the avian civilisation;
- revisit skipped early content;
- follow optional character arcs.

### 4.5 Phase 4 — World reconnection

The Hearthseed becomes strong enough to establish long-distance living routes.

This phase should:

- create major shortcuts through earlier regions;
- reveal substantially changed spaces;
- connect areas that previously felt separate;
- allow several late objectives to be approached in flexible order;
- bring the Hearthstead close to its full community form.

### 4.6 Phase 5 — Late convergence and finale

The final region should require accumulated restoration and story understanding, not a single arbitrary key.

Working requirement model:

- a minimum total number of major Hearth Blooms or equivalent regional restorations;
- one required story discovery explaining the core extraction system;
- a set of baseline hero capabilities available through clearly signposted routes;
- no requirement for every optional crop, resident, recipe, or challenge.

A thorough player may complete every region first. A critical-path player should be able to reach the finale while leaving meaningful optional content behind.

---

## 5. Gate Types

The world should use several gate types so progression does not feel like repeated coloured locks.

### 5.1 Personal movement gates

Examples:

- horizontal distance;
- vertical surfaces;
- controlled descent;
- mid-air correction;
- aimed wall launch;
- advanced movement combinations.

### 5.2 World-restoration gates

Examples:

- corruption retracts;
- roots form a bridge;
- flowers create platforms;
- water is cleansed;
- a dead tree becomes climbable;
- living lifts or seed pods awaken.

### 5.3 Tool gates

Examples:

- a root cutting requires pruning shears;
- compacted soil requires a root spade;
- a rare plant requires a grafting knife;
- a damaged object requires a resident craft service.

Tool gates should often reward ingredients, side routes, or home improvements. They should be used cautiously for the main critical path.

### 5.4 Knowledge and alchemy gates

Examples:

- reveal hidden root traces;
- brew temporary light or resistance;
- identify a safe reaction with an ecosystem;
- understand how to awaken a dormant structure.

A critical-path alchemy gate should never depend on a rare random ingredient.

### 5.5 Story and restoration thresholds

Examples:

- the Hearthseed has grown enough to reach a region;
- multiple regional roots must be restored;
- a resident can interpret avian machinery;
- the player has discovered the extraction source.

### 5.6 Soft challenge gates

Some routes may be possible earlier through strong movement or combat execution.

Sequence breaks are welcome when they:

- use legitimate movement mastery;
- do not corrupt save or story state;
- do not bypass essential tutorials;
- do not require glitches;
- produce a useful reward rather than a broken quest.

---

## 6. Reward Distribution Rule

To avoid bosses becoming overloaded reward containers, a region should distribute progression across several milestones.

### Recommended regional reward pattern

| Regional point | Typical reward |
|---|---|
| Entry or early discovery | Map clue, crop, ingredient family, or basic recipe |
| Mid-region milestone | Tool, resident encounter, station plan, or personal ability |
| Optional branch | Health/resource upgrade, placeable, recipe, lore, or challenge reward |
| Major boss or regional resolution | Hearth Bloom / world restoration / story consequence |
| Return to Hearthstead | Plant/graft reward, unlock new build zone, facility, route, or resident outcome |

A boss can still grant a personal ability when fiction and pacing support it, but this should not be the default for every region.

---

## 7. Hero Ability Progression

The current project recognises Dash, Wall Cling, Sprint, Wall Latch, Double Jump, Drift Cloak, Spirit Cast, and Bind. The final design should use the existing architecture but does not need to retain the current development unlock defaults.

### 7.1 Working starting kit

**Direction set:**

- walk and run;
- variable-height jump;
- standard aerial control;
- side, up, and down melee;
- downslash/pogo;
- combat resource generation;
- Bind or equivalent resource-to-health action.

**Open:**

- whether basic wall-slide/wall-jump are part of the starting kit;
- whether the Hearthseed grants Dash at the end of the opening chapter.

### 7.2 Recommended major progression set

| Ability | Intended role | Recommended progression tier |
|---|---|---|
| Dash | Horizontal gap crossing, evasion, faster backtracking | Opening reward or first post-Hearthstead unlock |
| Wall Cling | Vertical routes and controlled wall interaction | Early game |
| Spirit Cast | Ranged combat, remote activation, selected world interactions | Early or midgame |
| Double Jump | Mid-air correction and vertical combination routes | Midgame |
| Drift Cloak | Controlled descent, distance, wind and hazard routes | Midgame |
| Wall Latch | Precision aiming, advanced wall launch, mastery routes | Mid-to-late game |
| Sprint | Fast grounded travel or chase utility | Optional/cut candidate unless levels justify it |
| Bind | Expedition endurance and resource decision | Starting kit or very early unlock |

### 7.3 Recommended ability-tier structure

#### Tier A — Core verbs

Available in the opening or immediately after it:

- jump;
- directional melee;
- pogo;
- Bind;
- possibly basic wall interaction.

#### Tier B — Route-opening abilities

Obtained across the early regional phase:

- Dash;
- Wall Cling;
- Spirit Cast or another world-interaction ability.

These should make earlier routes faster or richer, not only open one matching gate.

#### Tier C — Combination abilities

Obtained during the midgame:

- Double Jump;
- Drift Cloak;
- advanced Spirit Cast use if split into stages.

These should combine with Tier B abilities to create multiple route solutions.

#### Tier D — Mastery ability

Obtained in the later midgame or early late game:

- Wall Latch / aimed wall launch;
- another highly expressive movement technique if Wall Latch is cut.

This tier should deepen existing movement rather than invalidate earlier abilities.

### 7.4 Ability assignment rules

- No required region should need the ability it awards to reach its own main objective.
- Optional challenge rooms may preview or test the reward after acquisition.
- A major critical-path gate should normally have only one required permanent ability or a readable combination of already-established abilities.
- Skipped early regions must remain reachable later.
- If the story can progress after completing any two of three early regions, the skipped region's ability cannot suddenly become mandatory without a clear opportunity to return and acquire it.
- Movement tuning must remain in `HeroAbilityConfig` or `HeroConfig` according to existing architecture; this document does not define values.

---

## 8. Farming and Crop Progression

### 8.1 Farming purpose

Farming provides:

- visible growth at home;
- ingredients for alchemy and quests;
- anticipation between expeditions;
- player-controlled organisation and decoration;
- a direct way to strengthen and diversify the Hearthstead;
- ecological knowledge rather than pure profit optimisation.

### 8.2 Growth model

**Direction set:**

- crops grow while the player explores;
- there is no strict daily schedule;
- crops do not die because the player stays away;
- daily tile-by-tile watering is not required;
- planting may include one initial settling, watering, or soil-preparation interaction;
- later Hearthseed and irrigation upgrades reduce routine actions further;
- harvesting and replanting remain deliberate player actions.

### 8.3 Crop tiers

#### Tier 0 — Wild forage

Not ordinary farm crops.

Purpose:

- teach gathering;
- provide immediate basic ingredients;
- reveal plants that may later become cultivable;
- populate regions without requiring every plant to become a seed.

Examples of functions:

- basic healing ingredient;
- starter dye or decoration material;
- simple processing material;
- regional clue or journal discovery.

#### Tier 1 — Hearth crops

Unlocked when the Hearthseed is planted.

Properties:

- grow in basic cultivation beds;
- short and forgiving growth cycles;
- clear early uses;
- no specialist conditions;
- form the foundation of basic brewing and healing.

Planning target:

- 3–5 crop types across the early game.

#### Tier 2 — Regional crops

Found through early and midgame exploration.

Properties:

- connected to a region's ecology;
- require ordinary beds or one simple specialist condition;
- support regional brews, residents, and world interactions;
- encourage returning home after discovery.

Planning target:

- 5–8 crop types across the full game.

#### Tier 3 — Specialist crops

Require a new bed type, tool, resident, or Hearthstead upgrade.

Possible conditions:

- shade;
- shallow water;
- wall trellis;
- fungal bed;
- warm stone;
- restored avian growing equipment adapted safely;
- proximity to a compatible plant.

These conditions should be visible and easy to understand. They should not become hidden soil-stat simulation.

Planning target:

- 3–5 crop types.

#### Tier 4 — Hearth-attuned crops

Rare late-game or optional crops that respond directly to restored Hearth Blooms or advanced grafting.

Purpose:

- powerful world-use brews;
- major resident quests;
- late-game preparation;
- distinctive visual customisation;
- optional completion goals.

Planning target:

- 2–4 crop types.

### 8.4 Hearth Blooms are not ordinary crops

Boss or regional restorative plants should be tracked separately from harvestable crops.

A Hearth Bloom:

- represents a major regional recovery;
- is planted or grafted once;
- permanently changes the Hearthseed and world;
- cannot be repeatedly harvested for profit;
- may unlock a related crop family or specialist condition.

### 8.5 Preliminary crop budget

| Category | Planning range |
|---|---:|
| Tier 1 Hearth crops | 3–5 |
| Tier 2 regional crops | 5–8 |
| Tier 3 specialist crops | 3–5 |
| Tier 4 Hearth-attuned crops | 2–4 |
| Total growable crops | 13–22 |

The final number should be reduced if each crop requires expensive art, animation, VFX, bespoke UI, or quest support.

---

## 9. Hearthstead Cultivation Stages

The Hearthstead grows through major restoration stages. Exact bed counts are test targets and may change after the placement prototype.

### Stage 0 — Dormant

State:

- Hearthseed not planted;
- no cultivation;
- abandoned structures;
- corruption blocks most buildable ground.

### Stage 1 — Seeded

Unlocked in the opening chapter.

Capabilities:

- initial outdoor placement zone;
- 2 basic cultivation beds;
- approximately 4 planting slots per bed;
- Tier 1 crops;
- basic storage;
- one simple processing surface;
- move and rotate supported objects.

### Stage 2 — Rooted

Unlocked after the first meaningful regional restoration.

Capabilities:

- expanded placement zone;
- up to 4 cultivation beds;
- basic water or root-moisture support;
- drying and grinding;
- simple paths, lights, and outdoor decoration;
- first permanent resident space;
- Tier 2 crops.

### Stage 3 — Flourishing

Unlocked during the early-to-midgame transition.

Capabilities:

- up to 6 cultivation beds;
- first specialist bed types;
- indoor and outdoor functional placement;
- improved brewing;
- resident-crafted furniture;
- expanded storage and seed handling;
- Hearthseed visibly branches into restored regional forms.

### Stage 4 — Sanctuary

Unlocked during the midgame.

Capabilities:

- up to 8 cultivation beds;
- broader specialist-crop support;
- grafting and propagation;
- additional resident spaces;
- multiple distinct customisation zones;
- root shortcuts or travel support;
- advanced processing and community facilities.

### Stage 5 — Hearthbloom

Late-game full form.

Capabilities:

- up to 10 cultivation beds as an initial ceiling;
- all supported bed types;
- Tier 4 Hearth-attuned crops;
- final Hearthseed presentation;
- the Hearthstead functions as a mature sanctuary;
- optional aesthetic and convenience upgrades continue without being required for the finale.

### Capacity principle

A cultivation bed should contain several planting slots. The player expands by placing meaningful bed objects, not by tilling hundreds of individual tiles.

This supports:

- neat farm layouts;
- visible expansion;
- lower interaction friction;
- simpler side-view collision and placement;
- manageable save data and UI.

---

## 10. Placement and Customisation Progression

### 10.1 Stage 1 placement

- basic cultivation beds;
- one storage object;
- a few simple decorations;
- small authored placement zone;
- move, rotate, flip, store, and replace where supported.

### 10.2 Stage 2 placement

- paths and lights;
- outdoor workstations;
- more storage and display objects;
- first crafted furniture set;
- expanded outdoor zone.

### 10.3 Stage 3 placement

- indoor functional objects;
- wall decoration anchors;
- specialist cultivation objects;
- resident-provided furniture styles;
- trophies from bosses or quests.

### 10.4 Stage 4–5 placement

- several Hearthstead sub-zones;
- resident-themed decoration;
- advanced lighting and plant displays;
- optional cosmetic prestige rewards;
- larger layout freedom without arbitrary structural building.

### Placement boundaries

- fixed scene exits remain unobstructed;
- the Hearthseed and major story structures use authored anchors;
- residents must retain valid interaction positions;
- placeables do not become arbitrary platforming geometry;
- repositioning does not consume resources repeatedly;
- removing an object returns it to storage unless the design explicitly says otherwise.

---

## 11. Tool Progression

Tools should be contextual and automatically selected where possible. Underbrew should avoid a large toolbelt that requires constant manual switching.

### 11.1 Herbalist Satchel

**Availability:** Starting kit.

Functions:

- stores forage, seeds, and ordinary ingredients;
- supports contextual gathering;
- links discoveries to the journal.

Progression:

- capacity upgrades;
- clearer categorisation;
- optional rare-material pouch.

### 11.2 Cultivation Trowel

**Availability:** Hearthseed planting / Stage 1.

Functions:

- place and interact with basic beds;
- plant and remove ordinary crops;
- contextually prepare a planting slot.

The player should not manually equip it for every crop interaction.

### 11.3 Pruning Shears

**Availability:** Early regional progression.

Functions:

- collect viable cuttings from selected wild plants;
- remove soft invasive growth;
- harvest vine, leaf, fibre, and branch ingredients;
- create selected optional shortcuts.

### 11.4 Root Spade

**Availability:** Early-to-midgame.

Functions:

- open compacted soil pockets;
- recover buried bulbs, roots, and materials;
- create specialist deep-soil beds;
- interact with damaged Hearthroot nodes.

It should not enable free terrain deformation.

### 11.5 Grafting Knife

**Availability:** Midgame.

Functions:

- propagate specialist crops;
- attach or manage Hearth-attuned plants;
- support selected Hearth Bloom interactions;
- unlock grafting recipes and advanced beds.

### 11.6 Survey or study tool

**Status:** Proposed.

Possible functions:

- reveal extraction flow;
- identify ecosystem conditions;
- annotate root signals on the map;
- inspect avian machinery;
- support knowledge-based progression.

This may be a journal capability or resident service rather than a physical tool.

### Tool design rules

- tools should unlock new interactions, not higher numerical harvesting power;
- old tools should remain useful;
- tools should not require durability repair;
- critical tools must come from authored progression, not random drops;
- a tool gate should be visually recognisable before the player owns the tool.

---

## 12. Crafting and Processing Progression

Crafting should use a small number of broad stations rather than one station per recipe category.

### Tier 1 — Basic handwork

Available at the first Hearthstead stage.

Can create:

- basic cultivation beds;
- simple storage;
- basic paths, lights, signs, and decoration;
- simple ingredient preparation.

### Tier 2 — Herbalist processing

Unlocked early.

Functions:

- drying;
- grinding;
- crushing;
- extracting simple sap, powder, or fibre;
- preparing ingredients for basic brews.

Recommended station model:

- one Herbalist Bench that visually gains modules rather than several tiny separate stations.

### Tier 3 — Brewing

Unlocked in the early game or restored from an unusable opening facility.

Functions:

- expedition brews;
- combat brews;
- world-use preparations;
- recipe journal and loadout management.

### Tier 4 — Cultivation construction

Unlocked through a resident, regional plans, or Hearthstead stage.

Can create:

- specialist beds;
- root irrigation or passive moisture support;
- trellises;
- water planters;
- fungal beds;
- improved storage and seed handling.

### Tier 5 — Grafting and advanced alchemy

Unlocked midgame.

Functions:

- rare plant propagation;
- Hearth-attuned ingredients;
- multi-stage processing;
- major restoration preparations;
- late-game resident and world quests.

### Furniture and decoration

Furniture crafting should probably be associated with a resident craftsperson or recovered plans rather than the protagonist personally mastering every trade.

This supports found-family progression:

```text
Help or recruit resident
→ resident establishes workshop or service
→ new placeable category becomes available
→ player chooses how to use it
```

---

## 13. Alchemy Progression

### 13.1 Tier 0 — Field remedies

Starting or opening-chapter knowledge.

Purpose:

- establish the protagonist as an herbalist;
- support simple healing or tutorial interactions;
- avoid a full brewing menu before the Hearthstead.

### 13.2 Tier 1 — Basic Hearth brews

Unlocked at the first functional brewing station.

Examples of roles:

- basic recovery support;
- temporary light;
- simple resistance;
- reveal nearby root traces;
- one first world-facing use.

### 13.3 Tier 2 — Regional preparations

Unlocked through regional ingredients and knowledge.

Roles:

- counter one environmental pressure;
- interact with a regional ecosystem;
- support a resident quest;
- alter risk or route choice;
- provide combat utility without invalidating melee.

### 13.4 Tier 3 — Advanced infusions

Midgame.

Roles:

- modify Spirit Cast or another combat system;
- combine cultivated and rare wild ingredients;
- support more advanced restoration;
- provide strong but limited preparation choices.

### 13.5 Tier 4 — Hearth restoration brews

Late or major quest progression.

Roles:

- cleanse major root damage;
- stabilise a Hearth Bloom;
- restore an extraction-damaged ecosystem;
- prepare for late-game hazards or bosses;
- resolve a resident or regional storyline.

### Alchemy unlock model

Recipes may come from:

- journal discovery;
- resident teaching;
- experimentation with readable hints;
- boss or regional materials;
- recovered texts;
- observing ecological relationships.

Critical-path recipes must show:

- what they do;
- what ingredients are missing;
- where known ingredients can be found or grown;
- whether the result is consumed or permanent.

---

## 14. Hero Capacity Progression

These upgrades reward optional exploration without replacing major abilities.

### Health

- small number of permanent normal-health upgrades;
- possible temporary bonus health from selected systems;
- no excessive health inflation that removes enemy threat;
- health upgrades primarily from authored exploration and challenge rewards.

### Resource

- maximum resource capacity upgrades;
- possibly improved generation options through techniques or equipment;
- resource remains shared by Bind and any retained cast systems according to final design.

### Brew loadout

Working progression:

- begin with 1 prepared-brew slot once brewing is introduced;
- expand to 2 slots in the early or midgame;
- cap around 3 slots unless testing supports more.

A small loadout creates meaningful preparation without turning the player into a walking inventory menu.

### Ingredient and seed storage

- generous ordinary material stacking;
- upgrades should reduce friction rather than create early punishment;
- key items and quest items remain separate;
- seeds and placeables should be clearly categorised.

### Map and journal

Progression may unlock:

- regional map detail;
- custom markers;
- remembered ability gates;
- crop-condition information;
- known ingredient sources;
- extraction-flow or Hearthroot overlays;
- resident and quest notes.

---

## 15. Hearthseed and World-Restoration Progression

### 15.1 Central Hearthseed stages

The Hearthseed should visibly change as major regional restorations are planted or grafted.

Possible visual progression:

```text
Dormant seed
→ first shoot
→ rooted sapling / bloom
→ branching regional forms
→ large sanctuary plant
→ full Hearth Bloom connected across the world
```

### 15.2 Major restorative rewards

Working categories:

- Hearth Blooms;
- root hearts;
- living grafts;
- purified regional seeds;
- restored Hearthwell connections.

The final fiction can use one consistent term or a small hierarchy.

### 15.3 World effects

A planted major reward may cause authored changes such as:

- thorn barriers retracting;
- root bridges growing;
- leaf or flower platforms appearing;
- poisoned water becoming safe;
- wind-borne seeds creating movement routes;
- dead trunks becoming climbable;
- roots breaking weakened avian structures;
- safe rooms and checkpoints awakening;
- new wild ingredients appearing;
- residents or wildlife returning.

These changes should be authored before/after states, not procedural world geometry.

### 15.4 Regional affinity

Each Hearth Bloom may carry a regional affinity that affects both home and world.

Example functional affinities:

| Affinity | World effect | Hearthstead effect |
|---|---|---|
| Root / Thorn | bridges, retracting spikes, climbable roots | trellises, deep-soil crops |
| Water / Reed | cleansed water, lily routes, restored flow | water beds, passive moisture |
| Wind / Spore | currents, floating seed platforms | hanging or airborne crops |
| Warmth / Ember | thawed or awakened spaces | warm beds, advanced processing |
| Light / Bloom | darkness retreat, luminous paths | night crops, lighting and ambience |

These are examples for later region assignment, not final biome promises.

---

## 16. Resident and Community Progression

Residents should be meaningful progression, not only dialogue decoration.

### Resident roles

Potential core roles include:

- craftsperson and furniture;
- cultivation specialist;
- brewer, cook, or ingredient processor;
- cartographer or explorer;
- creature/ecology researcher;
- combat or movement mentor;
- avian defector or machinery specialist;
- trader or request coordinator.

### Resident unlock pattern

```text
Encounter resident or their problem in the world
→ help resolve a meaningful objective
→ resident visits or moves to the Hearthstead
→ a service, station, quest chain, or placeable category becomes available
→ their area and dialogue develop as the Hearthstead grows
```

### Community stages

| Hearthstead state | Community feel |
|---|---|
| Seeded | Protagonist alone |
| Rooted | First visitor or resident |
| Flourishing | Small recognisable household/community |
| Sanctuary | Multiple services and relationships |
| Hearthbloom | Mature refuge and centre of resistance |

The exact residents and arrival order should remain partly non-linear.

---

## 17. Region Progression Package Template

When designing each region, fill out this package.

### Region identity

- working name;
- visual identity;
- ecosystem;
- avian extraction impact;
- traversal concept;
- primary emotional tone.

### Entry state

- accessible from which routes;
- minimum required capability;
- optional earlier sequence-break route;
- what the player can see but not yet reach.

### Progression rewards

- crop or ingredient family;
- tool or tool use;
- personal ability or technique;
- resident or service;
- recipe or knowledge;
- health/resource/slot upgrade;
- Hearth Bloom or regional restoration;
- major shortcut or world connection.

### Boss and outcome

- boss type: avian, corrupted native, or constructed creation;
- mechanic tested;
- environmental consequence;
- story information;
- Hearthstead consequence;
- revisit value.

### Optional content

- challenge room;
- resident quest;
- crop condition;
- boss trophy or furniture;
- lore or avian perspective;
- sequence break or mastery route.

---

## 18. Non-Linear Region Rules

### 18.1 Every available direction must be worthwhile

A player who chooses a region that is not the immediate critical path should still gain something useful.

Avoid regions that end with:

> “Come back later; nothing here matters yet.”

An early visit may still provide:

- a crop;
- a shortcut;
- a resident encounter;
- map knowledge;
- a tool;
- an optional upgrade;
- a clear view of the later main gate.

### 18.2 Story progression should use partial completion

Preferred patterns:

- complete any 2 of 3 early regional restorations;
- reach a total Hearthseed stage through several possible objectives;
- obtain one of several clues that point to the same convergence;
- approach a region from different sides based on ability order.

Avoid requiring one exact region order unless story or difficulty genuinely needs it.

### 18.3 Ability dependencies must be audited

For every region and major room, record:

- required ability;
- optional ability;
- restoration state;
- tool requirement;
- story requirement;
- alternate route;
- sequence-break risk.

A world graph should be validated against every meaningful ability-order combination the design claims to support.

### 18.4 Difficulty can guide without hard-locking

A region may signal that it is harder through:

- enemy complexity;
- longer checkpoint spacing;
- environmental pressure;
- presentation;
- NPC warnings;
- reward expectations.

The player may still choose to continue if the route is technically accessible.

### 18.5 The journal guides but does not command

The journal may show:

- known regional problems;
- rumours;
- root signals;
- resident requests;
- remembered gates.

It should avoid converting exploration into a mandatory ordered task list.

---

## 19. Working Progression Table

This table is a first concrete spine to test. Exact assignments remain adjustable until regions are defined.

| Game phase | Hero | Farming/home | Tools/crafting | World/story |
|---|---|---|---|---|
| Opening | Starting kit, Bind | Hearthseed planted, 2 beds, first crop | Trowel, storage, basic handwork | First local cleansing, two paths revealed |
| Early open phase | Dash/Wall Cling/Spirit Cast distributed across early progression | Tier 1–2 crops, 4 beds, first resident | Shears, herbalist processing, brewing | Any 2 early restorations trigger convergence |
| Early midgame | First combination ability | 6 beds, specialist bed, expanded placement | Root Spade, cultivation construction | Central root gate / avian response |
| Midgame | Double Jump and/or Drift Cloak | Tier 3 crops, residents, grafting preparation | Advanced brewing, better storage | Several regions and changed early routes |
| World reconnection | Wall Latch or mastery ability | 8 beds, multiple specialist zones | Grafting Knife, root travel support | Major shortcuts and flexible late objectives |
| Late game | Final combat/utility refinements | Tier 4 crops, mature sanctuary | Final station modules and cosmetics | Required restoration threshold and story key |
| Finale/aftermath | No mandatory grind | Hearthbloom form, optional completion | Convenience and presentation rewards | Extraction system resolved; restored-world state |

---

## 20. Decisions Register

| Decision | Status | Current direction |
|---|---|---|
| Overall structure | Direction set | Fixed opening, early open phase, partial convergence, open midgame, late convergence |
| Early story gate | Proposed | Meaningful progress in any 2 of 3 early regional threads |
| Progression style | Decided | Capability, restoration, knowledge, and home growth; no broad character levels |
| Farming growth | Direction set | Grows during expeditions; no daily punishment or crop death |
| Crop tiers | Direction set | Wild forage, Hearth crops, regional crops, specialist crops, Hearth-attuned crops |
| Farming capacity | Proposed | 2 → 4 → 6 → 8 → 10 multi-slot beds |
| Hearth Blooms | Decided direction | Permanent regional restoration rewards, separate from ordinary crops |
| Tool model | Direction set | Small contextual tool set, no durability, minimal manual switching |
| Placement | Decided direction | Grid-assisted free arrangement inside authored Hearthstead zones |
| Boss rewards | Direction set | Boss primarily resolves region and grants restoration; other rewards distributed through region |
| Resident progression | Direction set | Found-family residents unlock services, stations, quests, and placeables |
| Sequence flexibility | Decided direction | Several viable directions; partial completion gates; authored convergence |
| Starting kit | Partially open | Run, jump, directional melee, pogo, Bind; wall interaction and Dash placement open |
| Sprint | Open / cut candidate | Retain only if level design justifies unique value |

---

## 21. Highest-Priority Decisions Still Needed

Before assigning rewards to regions, decide:

1. Does the Hearthseed grant Dash at the end of the opening chapter?
2. Are basic wall-slide and wall-jump part of the starting kit or an early unlock?
3. Is Spirit Cast a required core progression verb or optional combat/world utility?
4. Does the first early convergence truly require any 2 of 3 regional outcomes?
5. What are the three first-region gameplay identities?
6. Which early region rewards a resident rather than a major movement ability?
7. How many Hearth Blooms are required for the critical path versus full restoration?
8. Is root travel a fast-travel network, a physical shortcut system, or both?
9. What is the first crop, and what immediate brew or purpose does it support?
10. Which crafting functions belong to the protagonist and which belong to residents?

---

## 22. Next Design Pass

The next revision should create a provisional region matrix with approximately five or six major regions.

For each region, assign:

- traversal concept;
- entry requirements;
- ability or technique;
- crop family;
- tool/crafting unlock;
- resident or character thread;
- avian extraction effect;
- boss category;
- Hearth Bloom affinity;
- world transformation;
- Hearthstead transformation;
- optional revisit and mastery content.

Only after that matrix works should the project lock the final world graph and exact ability order.
