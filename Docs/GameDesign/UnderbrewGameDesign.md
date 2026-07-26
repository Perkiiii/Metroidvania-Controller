# Underbrew — Living Game Design Document

**Status:** Working vision document  
**Version:** 0.1  
**Created:** 2026-07-26  
**Project:** Underbrew / Metroidvania Controller  

> This document describes what the complete game is intended to become. It is a design and planning source of truth, not an implementation-status document.
>
> - `Docs/Architecture.md` remains authoritative for technical ownership and system boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what is implemented and what should be built next.
> - `Docs/FeatureSpecs/` remains authoritative for detailed implemented-system contracts.
> - This document owns the game vision, player experience, world structure, progression, content targets, and scope boundaries.
>
> When a design decision becomes implementation-ready, it should be converted into a focused feature spec or implementation plan rather than implemented directly from this document.

---

## 1. High Concept

**Underbrew is a 2.5D action-exploration metroidvania in which the player restores a neglected home and its surrounding land, cultivates unusual ingredients, brews alchemical creations, and uses what they discover to push deeper into a dangerous interconnected world.**

The moment-to-moment play should have the responsiveness, traversal expression, readable combat, exploration, and boss encounters associated with games such as *Hollow Knight*, *Silksong*, *Nine Sols*, and *MIO*. Its long-term rhythm should also contain a lighter form of the cultivation, collection, crafting, and home improvement found in farming and survival games such as *Stardew Valley*, *Doloc Town*, and *Terraria*.

The game is not intended to place two complete genres beside one another. The farming and alchemy systems should support the metroidvania adventure rather than compete with it.

### Working player fantasy

> Leave home with a purpose, survive a dangerous expedition, return with strange discoveries, improve the place and people you care about, and become capable of reaching somewhere that previously felt impossible.

---

## 2. Vision Statement

Underbrew should feel like a handcrafted metroidvania where returning home is meaningful rather than merely administrative.

The world outside the home provides danger, mystery, traversal challenges, enemies, bosses, ingredients, knowledge, and lost tools. The home converts those discoveries into visible recovery and new possibilities: crops grow, facilities improve, recipes are learned, characters settle in, and the player prepares for their next expedition.

The two halves should create one reinforcing progression loop:

```text
Explore dangerous world
        ↓
Discover routes, materials, people, and knowledge
        ↓
Return home and cultivate / brew / improve
        ↓
Gain preparation, utility, and new objectives
        ↓
Re-enter the world with greater capability
```

The player should never feel that they have left the metroidvania to play an unrelated farming minigame. Home progression should deepen the adventure.

---

## 3. Design Pillars

### 3.1 Responsive traversal and readable combat

The hero must remain satisfying to control even when no reward is present. Movement, attacks, damage response, abilities, camera behavior, and animation readability are the foundation of the game.

- Traversal abilities should expand expression, not merely function as coloured keys.
- Rooms should support movement mastery as well as basic navigation.
- Combat should reward positioning, timing, directional attacks, aerial control, and learning enemy behavior.
- Bosses should test understood mechanics rather than rely on visual noise or unavoidable damage.
- The validated hero-feel baseline should not be casually changed to accommodate level-design problems.

### 3.2 Exploration that feeds the home

Every major expedition should have the potential to produce more than map completion.

The player may return with:

- a traversal ability;
- a new ingredient or seed;
- an alchemical recipe;
- a tool or facility upgrade;
- a rescued or recruited character;
- knowledge that changes an existing location;
- a shortcut or persistent world change;
- a boss reward;
- story information or a new objective.

This gives exploration multiple forms of value without filling the world with disposable loot.

### 3.3 A small home with visible growth

The home area should begin incomplete and become warmer, safer, busier, and more useful over the course of the game.

Progress should be visible through a manageable number of meaningful changes:

- restored garden plots;
- an improved brewing workspace;
- new residents or visitors;
- repaired paths and structures;
- new ambient life and decoration;
- new services or interactions;
- story changes caused by world progress.

The home should feel personal and restorative, but it must not become a full settlement-management simulation.

### 3.4 Farming and alchemy with low friction

The cultivation layer should be satisfying without demanding constant maintenance.

- Small numbers of plots and crop types are preferable to large fields.
- Every ingredient should have a clear gameplay, progression, quest, or economic purpose.
- Routine work should become easier rather than expand indefinitely.
- The player should be able to spend several expeditions away from home without being punished.
- There should be no expectation that the player follows an optimal daily schedule.
- Alchemy should create interesting preparation choices, not an inventory full of minor percentage buffs.

### 3.5 A compact, authored world

Underbrew should favour deliberate rooms, memorable landmarks, layered shortcuts, and meaningful revisits over raw map size.

The world should feel interconnected and gradually understood. New abilities, knowledge, persistent objects, and story progress should reveal additional uses for familiar spaces.

---

## 4. What Underbrew Is

Underbrew is intended to be:

- a traversal-led action metroidvania;
- a handcrafted single-player adventure;
- a world with authored progression and deliberate sequence flexibility;
- a game where combat and exploration remain the primary moment-to-moment activities;
- a game with a recurring home-and-expedition rhythm;
- a farming-lite game with compact cultivation and low maintenance;
- an alchemy game where ingredients connect exploration to preparation and progression;
- a game with bosses, NPCs, quests, environmental storytelling, and persistent world changes;
- a game with a complete save flow, menus, mapping, UI, accessibility options, audio, and polish;
- a project that can be built incrementally through complete vertical slices.

---

## 5. What Underbrew Is Not

Underbrew is not intended to be:

- a full farming simulator;
- a crop-profit optimisation game;
- a daily-calendar or strict time-management game;
- a life simulator with dozens of romance schedules and social statistics;
- a survival game driven by hunger, thirst, temperature, or equipment degradation;
- a procedurally generated sandbox;
- a loot-driven action RPG with constant equipment replacement;
- a crafting game with hundreds of low-value recipes;
- a base-building game with freeform structural construction;
- an open-world game measured by map scale;
- a boss-rush game with exploration acting only as connective tissue;
- a direct copy of any individual reference game.

### Explicit scope boundaries

Unless the design is deliberately revised later, the first full release should avoid:

- multiplayer;
- player-built houses or terrain deformation;
- dozens of farm animals;
- complex irrigation simulation;
- crop disease, soil chemistry, or weather damage;
- real-time deadlines that punish exploration;
- large-scale procedural item generation;
- fully simulated NPC daily routines across the entire world;
- separate combat gear tiers that invalidate earlier equipment;
- multiple large towns with independent economies.

---

## 6. Target Experience and Scope

### Target playtime

The aspirational target is **20–30 hours for a thorough first playthrough**, rather than 20–30 mandatory hours on the critical path.

Recommended content target:

| Player path | Target |
|---|---:|
| Critical path | 12–15 hours |
| Typical exploratory playthrough | 18–24 hours |
| Thorough completion-focused playthrough | 20–30 hours |

This keeps the full vision substantial while allowing optional exploration, quests, cultivation, recipe discovery, challenge rooms, and upgrades to provide much of the extended playtime.

### Scope principle

A shorter polished game with one strongly integrated home loop is preferable to a longer game made from disconnected or thin systems.

The intended 20–30 hour scope should only be committed to after the team has completed and measured one integrated chapter containing:

- a representative region;
- a traversal unlock;
- an ordinary-enemy set;
- a miniboss or boss;
- a crop/ingredient loop;
- one useful brew;
- a home improvement;
- an NPC or quest thread;
- a return visit using the new capability.

---

## 7. Player Experience Goals

The player should regularly feel:

- **curiosity** — “What is beyond that route?”
- **mastery** — “I can move through this space better now.”
- **recognition** — “I remember this place, and now I understand how to reach that path.”
- **preparation** — “I know what I want to bring on the next expedition.”
- **care** — “This home and its people are changing because of what I do.”
- **relief** — “I made it back with something valuable.”
- **discovery** — “This material, recipe, creature, or ruin has a use I did not expect.”
- **momentum** — “There is always one clear, exciting thing I could pursue next.”

The player should rarely feel:

- obligated to perform repetitive chores before they are allowed to explore;
- punished for spending time away from the garden;
- overwhelmed by minor crafting materials;
- unsure whether a reward has any meaningful use;
- required to grind ordinary enemies for basic progression;
- trapped in lengthy menus between expeditions.

---

## 8. Core Game Loops

### 8.1 Expedition loop

```text
Choose an objective or direction
→ travel through known routes
→ fight, traverse, investigate, and gather
→ open shortcuts and reach a milestone
→ decide whether to push farther or return safely
→ bring discoveries home
```

The expedition loop is the primary play loop. Farming and alchemy must not delay the player from entering it.

### 8.2 Home loop

```text
Return home
→ see visible changes and character reactions
→ collect mature crops / process discoveries
→ brew or prepare a small number of useful items
→ improve one facility, plot, relationship, or objective
→ choose the next expedition
```

A normal home visit should be useful in a few minutes. Players may spend longer when they deliberately want to organise, experiment, talk to characters, or decorate within the supported limits.

### 8.3 Long-term progression loop

```text
Gain traversal capability
→ reach a new region or layer of an old region
→ gain world knowledge and new ingredients
→ improve home capability
→ resolve a regional conflict or boss
→ create persistent change
→ uncover the next larger problem
```

---

## 9. World Structure

### Recommended structure

A practical full-game structure is a **central home region connected to five or six major adventure regions**, with smaller transitional spaces and optional sub-areas between them.

The home should not act as a menu hub that teleports the player everywhere. It should remain physically connected to the world, while later shortcuts reduce repetitive return travel.

Each major region should ideally contain:

- one strong visual and mechanical identity;
- one primary traversal concept;
- two to four ordinary enemy families or meaningful variants;
- one local ingredient family;
- one persistent change or shortcut chain;
- one NPC, quest, or story thread;
- one major reward;
- one boss or major encounter, though not every region requires both a miniboss and boss;
- at least one reason to revisit after gaining a later ability.

### World topology principle

The world should begin relatively legible, then develop complexity through vertical connections, loops, and cross-region shortcuts. It should avoid becoming a straight sequence of isolated biomes.

Recommended progression shape:

```text
Home / starting region
├── Early Region A
├── Early Region B
│    └── connection into Mid Region C
├── locked or hazardous route into Mid Region D
└── late shortcut network
     ├── returns through earlier regions
     └── leads toward final region
```

The player should gain some choice in their second and third major objectives, while the overall progression remains authorable and testable.

---

## 10. Starting Area — First Working Plan

**Names, lore, and final biome identity remain open. This section defines the required gameplay structure rather than final fiction.**

### Starting premise

The player arrives at, inherits, returns to, or awakens near a neglected smallholding and brewing space on the edge of a damaged or overgrown region. The site is safe enough to become a home, but incomplete enough that restoring it can carry the game’s long-term visual progression.

The earlier Underbrew prototype established a useful foundation worth carrying forward in revised form:

- foraging for ingredients;
- processing and brewing;
- a journal that records discoveries;
- quests that introduce recipes and world interaction;
- a light-producing brew used to pass an environmental obstacle;
- a cavern or deeper area unlocked through alchemical preparation.

These are inherited design seeds, not confirmed final content.

### Starting-area goals

The first area should teach:

1. responsive movement and basic attacks;
2. environmental interaction and a checkpoint;
3. the difference between the safe home and the dangerous outer world;
4. gathering one or two clearly useful ingredient types;
5. returning home rather than progressing forever in one direction;
6. one simple cultivation action;
7. one simple processing or brewing action;
8. using a prepared result to change what the player can do in the world;
9. reaching and overcoming the first meaningful encounter;
10. receiving a clear lead toward at least two possible next objectives.

### Suggested starting-area layout

```text
Home / smallholding
├── garden plots
├── basic brewing station
├── save/rest point
├── one initially unusable or damaged facility
└── route into nearby outskirts
     ├── safe forage pocket
     ├── first enemy encounter
     ├── movement tutorial rooms
     ├── first shortcut back toward home
     ├── blocked dark / hazardous route
     └── first small dungeon or cavern
          ├── recipe or ingredient requirement
          ├── introductory miniboss / boss
          └── first major progression reward
```

### First-hour target

By the end of the first hour, the player should have:

- understood the game’s movement and combat language;
- visited the home at least twice;
- planted or activated a very small cultivation process;
- brewed or received one meaningful alchemical item;
- used that item or knowledge in exploration;
- opened a shortcut;
- met at least one character or discovered a strong narrative hook;
- seen one clearly inaccessible route that motivates future progression.

The opening should not require a lengthy farming tutorial before combat and exploration begin.

---

## 11. Progression Model

Underbrew should use several connected progression tracks, each with distinct ownership and rewards.

### 11.1 Traversal progression

Permanent abilities change movement possibilities and route access.

### 11.2 Combat progression

New attacks, cast options, healing utility, resource use, or modifiers increase expression without turning the game into gear-score progression.

### 11.3 Home progression

Facilities, plots, residents, and repaired structures visibly expand what can be cultivated, processed, learned, or prepared.

### 11.4 Knowledge progression

Recipes, journal discoveries, maps, creature information, language/lore interpretation, and environmental understanding allow the player to recognise opportunities that were always present.

### 11.5 World-state progression

Boss defeats, opened shortcuts, repaired mechanisms, activated switches, removed obstacles, and completed quests permanently alter the world.

### 11.6 Optional mastery progression

Optional health, resource, inventory, recipe, challenge, movement, and narrative rewards support completion-focused play without blocking the critical path.

---

## 12. Ability Framework

The current project already contains an implemented ability-unlock spine and currently recognises:

- Dash;
- Wall Cling, shared by wall-slide and wall-jump;
- Sprint;
- Wall Latch;
- Double Jump;
- Drift Cloak;
- Spirit Cast;
- Bind.

The current technical defaults are not automatically the final game unlock order. Dash and Wall Cling are presently enabled by default for development and controller validation.

### Ability design rules

Every major traversal ability should satisfy at least three of these four roles:

1. unlock new routes;
2. make old routes faster or more expressive;
3. create meaningful combat utility;
4. combine with another ability to produce advanced movement.

Abilities should not exist only to open a matching lock.

### Recommended major-ability budget

Aim for approximately **six to eight major player abilities**, including healing or combat utility, rather than continually adding movement mechanics.

A working ability set could include:

| Ability | Primary role | Secondary value | Status |
|---|---|---|---|
| Basic jump / directional melee / downslash | Starting verb set | Pogo and combat traversal | Implemented foundation |
| Dash | Gap crossing and evasion | Faster traversal and attack repositioning | Implemented, currently default-unlocked |
| Wall Cling | Vertical navigation | Deliberate wall interaction | Implemented, currently default-unlocked |
| Double Jump | Mid-air correction and vertical reach | Advanced route chaining | Implemented first pass |
| Bind | Grounded resource-to-health conversion | Expedition endurance decision | Implemented, gated |
| Spirit Cast | Ranged combat and remote interaction | Environmental/alchemical utility potential | Planned |
| Drift Cloak | Aerial distance or controlled descent | Hazard navigation and route expression | Planned |
| Wall Latch / aimed launch | Precision wall traversal | Advanced combat repositioning | Planned |
| Sprint | Fast grounded traversal | Chase or timing utility | Planned; must justify itself against normal run and dash |

### Ability questions still open

- Which traversal abilities does the player begin with in the final game?
- Is Wall Cling a basic movement verb, an early unlock, or split between basic slide and advanced latch?
- Does Spirit Cast consume the same resource used by Bind?
- Can alchemical preparation temporarily imitate an ability, or must permanent traversal access always come from permanent unlocks?
- Is Drift Cloak a true glide, a slow-fall mantle, a wind interaction, or another form entirely?
- Does Sprint provide enough unique level-design value to justify an input and animation set?

### Recommended principle for temporary brews

Temporary alchemical effects should make an expedition safer, easier, faster, or more rewarding. They should rarely be required to preserve access to a permanent critical-path route after the player has already discovered it.

A brew may provide the first solution to a local obstacle, but permanent progression or a nearby renewable supply should prevent frustrating return trips.

---

## 13. Farming-Lite Design

### Purpose

Farming exists to create anticipation, attachment to home, ingredient planning, visual growth, and a reason to return. It is not intended to become the player’s primary source of money or a parallel full-length campaign.

### Recommended first-release scope

- a small number of unlockable plots;
- approximately 12–20 meaningful growable ingredient types across the full game;
- no more than a few active growth stages per crop;
- simple planting and harvesting;
- forgiving growth that continues while the player explores;
- limited or automated maintenance after initial onboarding;
- no crop death from player absence;
- a clear journal entry showing source, growth conditions, and uses;
- region-linked seeds or spores that make exploration feed cultivation;
- a few special growth conditions that create interesting goals without turning into simulation complexity.

### Possible special conditions

Use sparingly:

- grows only in shade or light;
- responds to a brew or processed soil additive;
- grows on walls, water, fungus beds, or ruins rather than standard soil;
- changes after a regional boss or world event;
- attracts a creature or NPC;
- produces a different result when combined with another nearby plant.

### Friction limits

The player should not need to:

- water every individual tile before every expedition;
- return before a short real-time or in-game deadline;
- clear weeds that continuously respawn without strategic value;
- maintain large quantities of identical crops;
- memorise hidden profitability tables;
- carry a large separate toolbelt for basic interactions.

### Expansion rule

Do not add animals, seasons, weather-dependent crops, cooking, fishing, soil quality, fertiliser tiers, or farm automation merely because farming games commonly contain them. Each system must strengthen Underbrew’s expedition and alchemy loop.

---

## 14. Alchemy and Brewing

### Purpose

Alchemy is the bridge between exploration and preparation. It should convert the world’s plants, creatures, minerals, and knowledge into a small number of memorable effects.

### Ingredient sources

- cultivated plants;
- wild forage;
- enemy or creature materials;
- minerals and environmental deposits;
- quest or boss materials;
- processed ingredients created at home;
- rare regional discoveries.

### Recommended brew categories

#### Expedition brews

Limited-use preparation that changes risk or approach.

Examples:

- temporary light or hazard resistance;
- protection from a regional environmental effect;
- improved recovery;
- temporary movement forgiveness;
- revealing hidden traces or interactables.

#### Combat brews

Clear tactical tools rather than stacks of small modifiers.

Examples:

- a thrown or placed concoction;
- a temporary alteration to Spirit Cast;
- resistance to a boss mechanic;
- a resource-generation or healing trade-off.

#### World and progression brews

Brews used to grow, repair, awaken, cleanse, feed, or transform something in the world.

These are especially valuable because they connect the home loop directly to authored exploration and quests.

### Recipe design rules

- A recipe should have an understandable purpose.
- Important recipes should be learned through discovery, quests, experimentation with guidance, or major rewards—not random grinding.
- Ingredients should have multiple uses only when the choice is interesting and readable.
- Required critical-path recipes should avoid rare random drops.
- The interface should show known results, missing ingredients, and likely sources without demanding external notes.
- Brewing should be quick once the player has made the planning decision.

### Relationship to the original Underbrew prototype

The earlier prototype’s Lumen Draught / light-potion concept is a strong example of the intended integration: gather and process ingredients, learn or complete a recipe, then use the resulting alchemical capability to pass a meaningful exploration obstacle. The final version may rename or redesign this content, but the structure is worth preserving.

---

## 15. Resources, Inventory, and Economy

### Inventory principle

The inventory should support exploration decisions without becoming a constant capacity-management problem.

Recommended separation:

- **Key progression:** permanent, never consumes ordinary inventory space.
- **Ingredients/materials:** collected into clear categories, with generous capacity or stack limits.
- **Prepared brews:** deliberately limited enough to make loadout choices meaningful.
- **Seeds/growing items:** stored clearly and connected to the garden interface.
- **Quest items:** tracked separately and never accidentally sold or consumed.

### Economy principle

Currency can support services, convenience, and selected upgrades, but should not replace exploration rewards.

Avoid making repetitive enemy farming the best way to progress. Important abilities, recipes, and facilities should come from authored objectives, discoveries, bosses, quests, or world milestones.

The game may not need a broad sell-everything economy. A narrow exchange system, specialist traders, or requests for particular materials may fit better than universal item prices.

---

## 16. Quests and NPCs

### Quest purpose

Quests should direct attention toward the world, introduce systems, reveal character, create persistent changes, and provide reasons to revisit areas.

Prefer quests that involve:

- discovering a location;
- finding a particular ingredient or lost object;
- brewing something with a world-facing use;
- resolving an environmental problem;
- defeating or understanding a creature;
- repairing or activating a structure;
- helping an NPC move to or improve the home;
- making a meaningful choice about a limited resource.

Avoid large numbers of generic quantity requests.

### NPC scope

A small cast with changing dialogue and visible roles is preferable to a large town of shallow characters.

Possible home roles include:

- alchemy or botanical knowledge;
- map and exploration support;
- repairs and facility progression;
- combat or movement training;
- trade or specialist requests;
- story and regional interpretation.

NPCs should not require fully simulated daily schedules unless a specific design need justifies them.

---

## 17. Mapping, Journal, and Information Design

The player needs enough information to plan both exploration and cultivation without turning the game into checklist management.

### Map responsibilities

- show discovered rooms and major connections;
- communicate incomplete exploration without revealing every secret;
- mark player-selected objectives and useful home facilities;
- support revisiting ability-gated routes;
- distinguish incomplete, locked, and unexplored routes where appropriate.

### Journal responsibilities

- record ingredients, crops, creatures, recipes, and important discoveries;
- show known sources and uses;
- identify missing knowledge clearly;
- surface quest and progression clues;
- celebrate meaningful discoveries and completion milestones.

The earlier prototype’s journal is a useful foundation, especially its role in making newly discovered items and recipes legible.

---

## 18. Combat and Enemy Direction

Combat should remain close-range, movement-led, and readable.

### Combat principles

- directional melee remains the reliable core;
- aerial attacks and downslash/pogo should matter in enemy and room design;
- resource generation should reward successful engagement;
- Bind creates a meaningful safe-window decision rather than instant healing;
- Spirit Cast, if retained, should complement melee rather than replace it;
- ordinary encounters should usually be short but spatially interesting;
- enemies should have clear silhouettes, tells, attack windows, and recovery;
- enemy combinations should create complexity through interaction rather than excessive health.

### Enemy content approach

Build a small set of reusable behavioral components and create region-specific enemies through composition, presentation, movement patterns, attacks, and arena context. Avoid creating every enemy as a unique architecture.

### Boss direction

Bosses should serve at least two purposes:

- a mechanical test or culmination;
- a world, story, or progression consequence.

A boss defeat should often cause something tangible: a route opens, a region changes, a character is freed, a facility becomes possible, or a major ingredient/ability is obtained.

---

## 19. Narrative and Tone — Working Direction

The final story premise remains open, but the design supports a tone built around:

- melancholy places with warmth still present;
- recovery rather than simple conquest;
- forgotten practices, plants, creatures, and ruins;
- a home that becomes a source of belonging;
- danger and loss without constant hopelessness;
- strange humour and character warmth between difficult expeditions;
- environmental storytelling that rewards attention.

The story should explain why cultivation and brewing matter to the world rather than treating them as optional hobbies attached to an unrelated hero.

### Narrative questions still open

- Who is the protagonist, and why can they fight, cultivate, and brew?
- Is the home inherited, discovered, reclaimed, or built around an existing community?
- What damaged or changed the world?
- Why are important ingredients and alchemical knowledge scattered across dangerous regions?
- What is the central conflict?
- What does restoration cost, and can every part of the world be restored?
- What does “Underbrew” refer to in the fiction?

---

## 20. Working Full-Game Progression Framework

This is a structure for planning, not a locked sequence or set of biome names.

### Stage 0 — Arrival and first restoration

- establish the protagonist and neglected home;
- teach core movement, combat, interaction, checkpoint, gathering, and return flow;
- restore the basic garden and brewing function;
- create the first world-facing brew;
- overcome the first major local obstacle or encounter;
- reveal the larger problem and multiple directions.

### Stage 1 — Two early regional objectives

- allow partial choice between two nearby regions;
- introduce the first major permanent traversal unlock beyond the final starting kit;
- recruit or establish the first important home resident/service;
- expand cultivation beyond the tutorial crop;
- introduce region-specific hazards and ingredients;
- open a meaningful cross-region or home shortcut.

### Stage 2 — Integrated midgame

- require combinations of learned movement rather than single-ability locks;
- make alchemy useful for preparation and world interaction without hard-gating every route;
- introduce more complex enemy combinations and a major boss;
- reveal deeper history and change the player’s understanding of the central conflict;
- visibly transform the home from survival space into a small community.

### Stage 3 — World reconnection

- use persistent switches, doors, repaired systems, and regional outcomes to connect previously separate routes;
- grant one of the most expressive traversal abilities;
- return the player through significantly changed early spaces;
- complete major NPC and facility arcs;
- unlock advanced but compact cultivation/brewing options.

### Stage 4 — Late-game convergence

- combine traversal, combat, preparation, and world knowledge;
- resolve remaining regional bosses or critical objectives in a flexible order where feasible;
- expose the final region through accumulated persistent changes rather than a single arbitrary key;
- ensure optional upgrades remain useful but are not mandatory.

### Stage 5 — Finale and aftermath

- final region and boss sequence;
- consequences reflect important world and character outcomes;
- return to the home after the climax;
- provide a satisfying visible state for completed restoration;
- support post-game cleanup and optional challenges without requiring an endless simulation mode.

---

## 21. Preliminary Content Budget

These figures are planning ceilings, not promises.

| Content | Initial planning range |
|---|---:|
| Major adventure regions | 5–6 plus home/start region |
| Major bosses | 5–7 |
| Minibosses / major encounters | 4–8 |
| Major permanent abilities | 6–8 total |
| Ordinary enemy families | 12–18, with meaningful variants/combinations |
| Growable ingredients | 12–20 |
| Major useful brew recipes | 12–18 |
| World/quest recipes and special processes | 8–15 |
| Core home residents | 4–7 |
| Major quest lines | 5–8 |
| Optional challenge spaces | 6–12 |

The budget should shrink if production quality, animation burden, environment art, or integration costs are higher than expected.

---

## 22. Production Strategy

The project should continue to build the metroidvania foundation first, but later work must validate the combined identity before producing a large world.

### Phase A — Metroidvania foundation

**Current focus.**

Complete and validate:

- hero movement and abilities;
- combat and damage loop;
- enemies and boss framework;
- camera and scene flow;
- checkpoints, save/load, and persistence;
- HUD and essential UI;
- first test rooms and progression gates;
- game-feel baseline.

### Phase B — Metroidvania vertical slice

Build a coherent small area containing:

- several connected rooms;
- an enemy set;
- a checkpoint and shortcuts;
- a traversal gate and unlock;
- a boss or major encounter;
- basic map support;
- complete death, retry, and save flow.

This validates the game without the farming layer.

### Phase C — Farming and alchemy prototype

Build the smallest complete home loop:

- two or three plots;
- two growable ingredients;
- growth across expeditions;
- one processing action;
- one brew;
- one world interaction enabled by that brew;
- one visible home improvement;
- journal support.

Do not build a broad crop catalogue yet.

### Phase D — Integrated chapter

Create the first representative chapter joining both halves:

- home visit;
- expedition objective;
- ingredient or seed discovery;
- cultivation or processing;
- meaningful brew/use;
- traversal or boss progression;
- NPC/quest consequence;
- return route and visible home change.

Measure actual development cost and player time here before locking the full content budget.

### Phase E — Production framework

Only after the integrated chapter works:

- finalise region list and progression graph;
- lock the major ability sequence;
- define content templates and authoring tools;
- establish enemy, boss, crop, recipe, quest, and room pipelines;
- set final scope based on measured throughput.

### Phase F — Full production and polish

Produce content region by region while continuously validating:

- navigation and sequence logic;
- revisit value;
- home/exploration balance;
- farming friction;
- recipe usefulness;
- narrative pacing;
- save and persistence safety;
- performance and accessibility;
- controller feel and game feedback.

---

## 23. Immediate Design Work Before Farming Implementation

The farming code does not need to be implemented yet. The following design work can happen alongside the current metroidvania foundation:

1. Define the protagonist, home premise, and central conflict.
2. Decide the final starting movement kit versus development defaults.
3. Create a rough world graph with the home and five or six candidate regions.
4. Assign a traversal concept, ingredient family, persistent change, and major reward to each candidate region.
5. Define the first integrated chapter in detail.
6. Choose the smallest farming loop that can validate the game’s identity.
7. Define approximately six example recipes across expedition, combat, and world-use categories.
8. Decide whether temporary brews can ever be mandatory for critical-path exploration.
9. Establish the home’s visible restoration stages.
10. Determine the likely critical-path and completionist scope before adding optional systems.

---

## 24. Decisions Register

Use this table to stop settled decisions from being repeatedly reopened without a reason.

| Decision | Status | Current direction | Reason / evidence |
|---|---|---|---|
| Primary genre | Decided | Action-exploration metroidvania | Current controller, combat, world, boss, camera, and persistence foundations |
| Secondary genre layer | Decided | Farming/cultivation-lite with alchemy | Core creative direction |
| System priority | Decided | Complete metroidvania implementation foundation before farming implementation | Reduces integration risk and matches current work |
| Farming depth | Direction set | Small, low-maintenance, expedition-supporting | Avoid building two complete games |
| Target length | Direction set | 12–15 hour critical path; 20–30 thorough playthrough | More production-feasible than a mandatory 20–30 hour campaign |
| World structure | Proposed | Central physical home plus 5–6 major interconnected regions | Supports return loop and authored metroidvania structure |
| Major ability count | Proposed | 6–8 total | Limits animation, level, input, and testing burden |
| Final starting kit | Open | Development defaults are not final progression | Requires level/progression decision |
| Final narrative premise | Open | Restoration, belonging, and alchemical world connection | Requires dedicated narrative pass |
| Crop count | Proposed ceiling | 12–20 meaningful growables | Enough variety without simulation sprawl |
| Mandatory temporary brews | Open | Use cautiously; avoid repeated critical-path consumable friction | Requires integrated chapter testing |

---

## 25. Open Design Backlog

### Highest priority

- final player fantasy and protagonist identity;
- central conflict and story premise;
- home premise and visual transformation stages;
- starting-area fiction and first boss/reward;
- final starting abilities;
- rough world-region list and topology;
- first integrated chapter;
- exact relationship between resource, Bind, Spirit Cast, and brews.

### Medium priority

- map acquisition and annotation rules;
- inventory and prepared-brew capacity;
- death consequences beyond checkpoint respawn;
- currency and trade model;
- home resident roles;
- quest structure and tracking;
- farming growth timing and maintenance model;
- recipe discovery method;
- optional challenge and completion structure.

### Later

- difficulty and accessibility modes;
- achievements;
- post-game state;
- localisation scope;
- platform targets;
- final content count and release roadmap.

---

## 26. Document Maintenance Rules

This is a living document. Update it when a design decision changes, but do not use it as a changelog.

### Status language

Use these labels for uncertain content:

- **Decided** — the project should plan and implement around this unless deliberately revised.
- **Direction set** — preferred approach, but still requires validation.
- **Proposed** — a concrete option worth testing.
- **Open** — not yet decided.
- **Cut / deferred** — intentionally outside the current release scope.

### When a section becomes too detailed

Create a focused design document under `Docs/GameDesign/`, for example:

```text
Docs/GameDesign/
├── UnderbrewGameDesign.md
├── WorldAndProgression.md
├── HomeAndFarming.md
├── AlchemyAndRecipes.md
├── NarrativeAndCharacters.md
├── CombatAndAbilities.md
└── ContentBudget.md
```

Do not duplicate architecture or implementation contracts in those documents. Link to the relevant `Architecture`, `ImplementationPlan`, or `FeatureSpecs` document instead.

### Design-to-implementation handoff

Before implementing a proposed system:

1. settle the player-facing rules in the relevant game-design document;
2. inspect the repository for existing or planned equivalents;
3. define ownership and interfaces in a feature spec or implementation plan;
4. identify required Unity Editor work;
5. implement the smallest representative vertical slice;
6. playtest before expanding content.

---

## 27. Next Revision Goal

Version 0.2 should focus on **the game’s premise, the home, the first region, and the first integrated chapter**.

The next revision should answer:

1. Who is the protagonist?
2. Why do they establish or restore this home?
3. What is wrong with the surrounding world?
4. What does the first region contain?
5. What is the first boss or major encounter?
6. What ability, ingredient, recipe, character, or world change is earned there?
7. How does that reward visibly change both exploration and home life?

Once these are clear, the rough world graph and progression sequence can be designed without inventing disconnected biomes or abilities.
