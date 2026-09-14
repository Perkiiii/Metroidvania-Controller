# Underbrew — Living Game Design Document

**Status:** Working vision document  
**Version:** 0.2  
**Created:** 2026-07-26  
**Last updated:** 2026-07-26  
**Project:** Underbrew / Metroidvania Controller  

> This document describes what the complete game is intended to become. It is a design and planning source of truth, not an implementation-status document.
>
> - `Docs/Architecture.md` remains authoritative for technical ownership and system boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what is implemented and what should be built next.
> - `Docs/FeatureSpecs/` remains authoritative for detailed implemented-system contracts.
> - This document owns the game vision, player experience, fiction, world structure, progression, content targets, and scope boundaries.
>
> When a design decision becomes implementation-ready, convert it into a focused feature spec or implementation plan rather than implementing directly from this document.

---

## 1. High Concept

**Underbrew is a 2.5D action-exploration metroidvania about a lone red panda herbalist who creates a new home in a damaged land, cultivates unusual plants, brews alchemical creations, restores a living root network, and gradually builds a sanctuary for others displaced by an extractive avian civilisation.**

The moment-to-moment play should have the responsiveness, traversal expression, readable combat, exploration, and boss encounters associated with games such as *Hollow Knight*, *Silksong*, *Nine Sols*, and *MIO*. Its long-term rhythm should contain a lighter form of the cultivation, collection, crafting, customisation, and home improvement found in games such as *Stardew Valley*, *Doloc Town*, and *Terraria*.

The game is not intended to place two complete genres beside one another. Cultivation, alchemy, home customisation, world restoration, and metroidvania progression should form one reinforcing experience.

### Working player fantasy

> Lose a home, survive an unfamiliar world, nurture a living seed, shape a place that truly belongs to you, welcome others into it, and grow strong enough to heal the land that surrounds it.

---

## 2. Vision Statement

Underbrew should feel like a handcrafted metroidvania where returning home is emotionally and mechanically meaningful rather than merely administrative.

The world outside the Hearthstead provides danger, mystery, traversal challenges, enemies, bosses, ingredients, knowledge, displaced travellers, and evidence of the avian civilisation's extraction. The Hearthstead converts those discoveries into visible recovery and new possibilities: crops grow, facilities improve, furniture and workspaces are placed by the player, residents arrive, recipes are learned, and the Hearthseed sends restorative roots back into the wider world.

The two halves should create one reinforcing progression loop:

```text
Explore the damaged world
        ↓
Discover routes, ingredients, people, knowledge, and Hearth Blooms
        ↓
Return home and cultivate / brew / arrange / restore
        ↓
Strengthen the Hearthseed and the community around it
        ↓
Create new preparation, traversal routes, and world changes
        ↓
Re-enter the world with greater capability and purpose
```

The player should never feel that they have left the metroidvania to play an unrelated farming minigame. Home progression should deepen the adventure, and adventure progression should visibly transform the home.

---

## 3. Core Story Premise

### 3.1 The protagonist

The protagonist is a young red panda herbalist who lives alone within a woodland settlement grown around ancient living trees.

He is not beginning the game as a chosen hero, military leader, or famous warrior. He knows plants, remedies, gathering, and practical survival. His ability to fight is a necessary extension of living in a dangerous world rather than his defining identity.

The protagonist does not begin with an urgent missing-family rescue objective. His loss is the displacement of his community and the destruction of the place where he lived. This allows the Hearthstead to become the emotional centre of the game rather than a temporary camp that delays the “real” story.

His personal name, exact age, prior relationships, and the details of his herbalist training remain open.

### 3.2 The woodland settlement

The opening settlement is built around and among living trees connected to the ancient Hearthroot network.

It should feel:

- inhabited rather than pristine;
- warm, handmade, and adapted to the trees instead of imposed over them;
- cultivated without being industrialised;
- like a real community the protagonist has lost;
- visibly different from the later Hearthstead while establishing the kind of life that can return there.

The opening cutscene should establish this place efficiently before showing its occupation and evacuation.

### 3.3 The avian civilisation

The principal antagonist faction is an avian civilisation that fled a damaged homeland and expanded into this region.

Their culture is quiet, mechanical, controlled, and brutalist. They value order, predictability, output, and survival. Their leadership believes unmanaged nature is wasteful and that land becomes valuable only when measured, controlled, and made productive.

They install extraction machinery into ancient trees and the Hearthroot network. Living energy, sap, water, heat, or another regional resource is drawn into pipes, reservoirs, processing sites, and avian infrastructure.

Their expansion causes:

- trees to lose colour and die around extraction taps;
- the Hearthroot network to starve or withdraw;
- native ecosystems to become unstable;
- local creatures to become displaced, aggressive, or corrupted;
- invasive cultivation to replace local biodiversity;
- spikes, thorns, rot, stagnant water, or other region-specific manifestations to block routes;
- settlements to be evacuated or absorbed by avian expansion.

The birds are not inherently evil as a species. The central conflict is with an extractive system and the leadership enforcing it. Individual bird characters may support it, depend upon it, question it, resist it, or defect from it.

### 3.4 Cultivation versus extraction

The thematic contrast is not “wild nature good, orderly farming bad.” Players must be free to build a neat, efficient, symmetrical farm without the game implying that they have adopted the antagonist's ideology.

The contrast is:

```text
The Hearthstead
care, reciprocity, biodiversity, adaptation, repair, community

Avian extraction
uniformity, depletion, forced conversion, expansion, control, abandonment
```

The player cultivates a place. The avian regime converts places into production.

A carefully organised player farm remains healthy because its underlying relationship with the land is restorative. It grows compatible plants, strengthens the Hearthseed, supports its residents, and returns vitality to the Hearthroot network.

### 3.5 The inciting incident

The birds occupy the woodland settlement and begin tapping its living trees. Residents are forced to leave as the settlement becomes unsafe.

During the evacuation, extraction damage causes a living bridge, root path, or riverbank to collapse. The protagonist falls into the river and is swept far away from the scattered community.

The cutscene then transitions through a brief journey montage before the protagonist awakens at the beginning of the tutorial region with little more than basic equipment.

The immediate objective is simple:

> Find safety and keep moving.

### 3.6 The abandoned Hearthstead

After surviving the tutorial region and defeating its first boss, the protagonist discovers a dormant Hearthseed and an abandoned Hearthstead beyond the arena.

With nowhere else to go, he plants the Hearthseed and begins cultivating the surrounding land.

The restored Hearthseed:

- pushes back the first local corruption;
- makes a small part of the Hearthstead habitable and cultivable;
- awakens part of the damaged Hearthroot network;
- becomes the living centre of the player's home;
- acts as a beacon that gradually attracts displaced travellers;
- eventually becomes a threat to the avian extraction system because it proves the land can recover.

What begins as one lonely herbalist creating somewhere to survive becomes a new community, a sanctuary for people who have lost their homes, and eventually the centre of resistance against the extraction of the land.

### 3.7 Emotional progression

```text
Opening:
“I need somewhere safe.”

Early game:
“I need to protect this seed and make this place liveable.”

Midgame:
“Other people now depend on what we are building here.”

Late game:
“If the extraction continues, every community will eventually lose its home.”

Final act:
“Restoring one refuge is not enough; the system draining the land must be stopped.”
```

The central emotional theme is:

> **The protagonist loses a home, creates a new one, and gradually makes room within it for others.**

---

## 4. Design Pillars

### 4.1 Responsive traversal and readable combat

The hero must remain satisfying to control even when no reward is present. Movement, attacks, damage response, abilities, camera behaviour, and animation readability are the foundation of the game.

- Traversal abilities should expand expression, not merely function as coloured keys.
- Rooms should support movement mastery as well as basic navigation.
- Combat should reward positioning, timing, directional attacks, aerial control, and learning enemy behaviour.
- Bosses should test understood mechanics rather than rely on visual noise or unavoidable damage.
- The validated hero-feel baseline should not be casually changed to accommodate level-design problems.

### 4.2 Exploration that feeds the home

Every major expedition should have the potential to produce more than map completion.

The player may return with:

- a traversal ability;
- a new ingredient or seed;
- an alchemical recipe;
- a functional or decorative placeable object;
- a tool or facility upgrade;
- a displaced traveller or future resident;
- knowledge that changes an existing location;
- a shortcut or persistent world change;
- a Hearth Bloom or other boss reward;
- story information or a new objective.

This gives exploration multiple forms of value without filling the world with disposable loot.

### 4.3 A player-shaped home with visible growth

The Hearthstead should begin empty, damaged, and lonely. It should become warmer, safer, busier, more useful, and more personal over the course of the game.

The player should decide how much of the Hearthstead is arranged. Meaningful home rewards should often provide objects that can be placed and moved rather than only unlocking predetermined scene upgrades.

Progress should be visible through:

- expanded cultivable and buildable zones;
- player-placed garden plots and planters;
- an improved brewing workspace;
- storage, furniture, lights, trophies, paths, and decoration;
- new residents and visitors;
- repaired fixed structures and routes;
- returning wildlife and ambient life;
- the growth of the Hearthseed and its Blooms;
- story changes caused by world restoration.

The Hearthstead should feel personal and restorative, but it must not become a full settlement-management simulation.

### 4.4 Farming and alchemy with low friction

The cultivation layer should be satisfying without demanding constant maintenance.

- Small numbers of meaningful crops are preferable to huge identical fields.
- Every ingredient should have a clear gameplay, progression, quest, ecological, or economic purpose.
- Routine work should become easier rather than expand indefinitely.
- The player should be able to spend several expeditions away from home without being punished.
- There should be no expectation that the player follows an optimal daily schedule.
- Alchemy should create interesting preparation choices, not an inventory full of minor percentage buffs.
- Cultivation should strengthen the Hearthstead and support restoration rather than existing only for profit.

### 4.5 Restoration that changes traversal

Healing the world should do more than remove a coloured barrier.

Hearthseed growth and recovered Hearth Blooms can cause authored environmental changes such as:

- spikes or thorn walls withdrawing;
- root bridges growing across gaps;
- flowers opening into platforms or bounce surfaces;
- climbable vines appearing;
- poisoned water becoming safe;
- dormant lifts, springs, or waterways reactivating;
- hollow roots becoming tunnels;
- wildlife and ingredients returning;
- old rooms gaining new traversal routes.

These transformations should be authored before-and-after states, not procedural geometry generation.

### 4.6 A compact, authored world

Underbrew should favour deliberate rooms, memorable landmarks, layered shortcuts, and meaningful revisits over raw map size.

The world should feel interconnected and gradually understood. New abilities, knowledge, persistent objects, Hearth Blooms, and story progress should reveal additional uses for familiar spaces.

---

## 5. What Underbrew Is

Underbrew is intended to be:

- a traversal-led action metroidvania;
- a handcrafted single-player adventure;
- a story about displacement, restoration, belonging, and found community;
- a world with authored progression and deliberate sequence flexibility;
- a game where combat and exploration remain the primary moment-to-moment activities;
- a game with a recurring Hearthstead-and-expedition rhythm;
- a farming-lite game with compact cultivation and low maintenance;
- an alchemy game where ingredients connect exploration to preparation and progression;
- a home-customisation game within carefully authored placement boundaries;
- a game where major victories visibly change both home and world;
- a game with bosses, NPCs, quests, environmental storytelling, and persistent world changes;
- a project that can be built incrementally through complete vertical slices.

---

## 6. What Underbrew Is Not

Underbrew is not intended to be:

- a full farming simulator;
- a crop-profit optimisation game;
- a daily-calendar or strict time-management game;
- a life simulator with dozens of romance schedules and social statistics;
- a survival game driven by hunger, thirst, temperature, or equipment degradation;
- a procedurally generated sandbox;
- a loot-driven action RPG with constant equipment replacement;
- a crafting game with hundreds of low-value recipes;
- unrestricted structural base building or terrain deformation;
- an open-world game measured by map scale;
- a boss-rush game with exploration acting only as connective tissue;
- a story where every bird is inherently evil;
- a story that treats organised cultivation itself as immoral;
- a direct copy of any individual reference game.

### Explicit scope boundaries

Unless the design is deliberately revised later, the first full release should avoid:

- multiplayer;
- player-built houses or terrain deformation;
- moveable objects becoming arbitrary player-created platforming geometry;
- dozens of farm animals;
- complex irrigation simulation;
- crop disease, soil chemistry, or weather damage;
- real-time deadlines that punish exploration;
- large-scale procedural item generation;
- fully simulated NPC daily routines across the entire world;
- separate combat gear tiers that invalidate earlier equipment;
- multiple large towns with independent economies.

---

## 7. Target Experience and Scope

### Target playtime

The aspirational target is **20–30 hours for a thorough first playthrough**, rather than 20–30 mandatory hours on the critical path.

| Player path | Target |
|---|---:|
| Critical path | 12–15 hours |
| Typical exploratory playthrough | 18–24 hours |
| Thorough completion-focused playthrough | 20–30 hours |

This allows optional exploration, resident stories, cultivation, recipe discovery, decoration, challenge rooms, and upgrades to provide much of the extended playtime.

### Scope principle

A shorter polished game with one strongly integrated home loop is preferable to a longer game made from disconnected or thin systems.

The intended scope should only be committed to after completing and measuring one integrated chapter containing:

- a representative region;
- a traversal unlock;
- an ordinary-enemy set;
- a miniboss or boss;
- a crop/ingredient loop;
- one useful brew;
- one meaningful placeable object;
- one Hearthseed or world-restoration change;
- an NPC or quest thread;
- a return visit using the new capability.

---

## 8. Player Experience Goals

The player should regularly feel:

- **curiosity** — “What is beyond that route?”
- **mastery** — “I can move through this space better now.”
- **recognition** — “I remember this place, and now I understand how to reach that path.”
- **preparation** — “I know what I want to bring on the next expedition.”
- **ownership** — “This place looks and functions this way because I shaped it.”
- **care** — “This home and its people are changing because of what I do.”
- **relief** — “I made it back with something valuable.”
- **discovery** — “This material, recipe, creature, or ruin has a use I did not expect.”
- **restoration** — “A place that was dying is visibly alive again.”
- **momentum** — “There is always one clear, exciting thing I could pursue next.”

The player should rarely feel:

- obligated to perform repetitive chores before they are allowed to explore;
- punished for spending time away from the garden;
- punished for arranging the farm neatly or inefficiently;
- overwhelmed by minor crafting materials;
- unsure whether a reward has any meaningful use;
- required to grind ordinary enemies for basic progression;
- trapped in lengthy menus between expeditions.

---

## 9. Core Game Loops

### 9.1 Expedition loop

```text
Choose an objective or direction
→ travel through known routes
→ fight, traverse, investigate, and gather
→ open shortcuts and reach a milestone
→ decide whether to push farther or return safely
→ bring discoveries home
```

The expedition loop is the primary play loop. Farming and alchemy must not delay the player from entering it.

### 9.2 Hearthstead loop

```text
Return home
→ see Hearthseed, world, and resident changes
→ collect mature crops / process discoveries
→ brew or prepare a small number of useful items
→ place or rearrange functional and decorative objects
→ improve one facility, relationship, restoration goal, or objective
→ choose the next expedition
```

A normal home visit should be useful in a few minutes. Players may spend longer when they deliberately want to organise, experiment, talk to residents, or decorate.

### 9.3 Restoration loop

```text
Defeat a major threat or resolve a regional problem
→ recover a Hearth Bloom, root fragment, or equivalent restorative reward
→ plant or graft it at the Hearthstead
→ strengthen the central Hearthseed
→ roots spread into authored locations
→ corruption withdraws and living traversal structures appear
→ old and new areas become reachable
```

### 9.4 Long-term progression loop

```text
Gain personal traversal capability
+
Heal part of the world
+
Grow the Hearthstead and its community
→ reach a new region or layer of an old region
→ uncover the larger extraction system
→ resolve a regional conflict
→ create persistent change
→ move closer to stopping the source
```

---

## 10. World Structure

### Recommended structure

A practical full-game structure is a **central Hearthstead connected to five or six major adventure regions**, with smaller transitional spaces and optional sub-areas between them.

The Hearthstead should not act as a menu hub that teleports the player everywhere. It should remain physically connected to the world, while later root routes and shortcuts reduce repetitive return travel.

Each major region should ideally contain:

- one strong visual and mechanical identity;
- one primary traversal concept;
- two to four ordinary enemy families or meaningful variants;
- one local ingredient family;
- evidence of how avian extraction affects that ecosystem;
- one persistent change or shortcut chain;
- one displaced character, resident candidate, quest, or story thread;
- one major reward;
- one boss or major encounter, though not every region requires both a miniboss and boss;
- at least one reason to revisit after gaining a later ability or Hearth Bloom.

### World topology principle

The world should begin relatively legible, then develop complexity through vertical connections, loops, and cross-region shortcuts. It should avoid becoming a straight sequence of isolated biomes.

```text
Opening settlement cutscene
        ↓
Tutorial region → first boss
        ↓
Hearthstead / central home
├── Early Region A
├── Early Region B
│    └── connection into Mid Region C
├── corrupted or extraction-blocked route into Mid Region D
└── late root / shortcut network
     ├── returns through restored early regions
     └── leads toward the avian extraction centre / final region
```

The player should gain some choice in their second and third major objectives, while the overall progression remains authorable and testable.

---

## 11. Opening Chapter — Decided Direction

### 11.1 Opening cutscene

The opening cutscene should communicate the protagonist's loss without explaining the entire history of the world.

Provisional sequence:

```text
Woodland settlement built around living trees
→ quiet avian machinery and brutalist structures appear
→ extraction taps are installed into the trees
→ colour and vitality begin draining from the settlement
→ armed birds escort residents away
→ extraction damage collapses a living bridge or riverbank
→ the protagonist falls into the river
→ he is swept far downstream
→ brief journey montage
→ he awakens alone in the tutorial region
```

The player should initially understand the birds as an occupying and displacing force. The deeper ecological consequences of extraction should be discovered during play.

### 11.2 Tutorial-region purpose

The tutorial region is a dedicated metroidvania introduction before the Hearthstead and farming systems are introduced.

It should teach:

- walking, running, jumping, and air control;
- variable jump height and movement precision;
- basic directional combat;
- hazards and recovery;
- ordinary enemy tells;
- checkpoints and the meaning of securing progress;
- the expectation that movement mastery is part of the game.

It should not simultaneously teach farming, brewing, furniture placement, shops, quests, and multiple currencies.

### 11.3 Folded route and first checkpoint

The tutorial route should follow the structural principle admired in Moss Grotto:

- the player begins near the upper or outer edge of a folded space;
- the route descends, crosses a lower section, and climbs back toward an upper destination;
- the checkpoint may be spatially close to the start but cannot be reached directly;
- there is no shortcut that bypasses the route before the first checkpoint;
- repeated attempts teach precision, route knowledge, and movement confidence.

Before the checkpoint is activated, death returns the player to the opening spawn.

The first activatable checkpoint sits immediately before the introductory boss. Once reached, it provides a short, safe boss runback with no enemies or repeated tutorial gauntlet between checkpoint and arena.

Target traversal timing:

| Experience | Approximate time to checkpoint |
|---|---:|
| First cautious attempt | 10–15 minutes |
| After one or two deaths | 6–8 minutes |
| Familiar route | 3–5 minutes |

The route should challenge consistency without relying on unfair damage, lengthy dialogue, or instant-death traps during the earliest teaching rooms.

### 11.4 Suggested room progression

```text
Opening spawn / sheltered arrival
→ basic movement descent
→ first precision-jump room
→ first low-threat enemy
→ hazard introduction
→ movement under combat pressure
→ longer vertical climb
→ final combined traversal test
→ quiet checkpoint chamber
→ short atmospheric boss approach
→ introductory boss
→ environmental transformation
→ abandoned Hearthstead reveal
```

### 11.5 Introductory boss

The first boss should be a native creature harmed or destabilised by the failing Hearthroot network, not an avian commander.

Its design should communicate:

- this land is already suffering;
- local creatures are victims or consequences as well as threats;
- something has disrupted the ecosystem;
- the protagonist is capable of surviving, but does not yet understand the full cause.

The exact creature, moveset, and arena identity remain open.

The boss should test only mechanics already taught by the tutorial route. It should not require a newly introduced parry, spell, farm item, or advanced traversal ability.

### 11.6 Hearthseed discovery and first restoration

After the boss is defeated:

- the local corruption loosens or withdraws;
- a blocked spring, root chamber, or passage reawakens;
- the dormant Hearthseed becomes accessible;
- the route into the abandoned Hearthstead opens;
- the protagonist plants the Hearthseed because it offers the first real chance of safety and renewal.

The first planting should produce a visible transformation:

- roots travel beneath the ground;
- colour returns to a small area;
- water, light, or warmth reactivates;
- one authored cultivation and placement zone becomes usable;
- the wider damaged Hearthstead remains visible as a long-term promise.

### 11.7 First Hearthstead interaction

The player should customise something almost immediately.

The first home sequence should include only:

1. planting the Hearthseed at its authored narrative location;
2. awakening one small part of the Hearthstead;
3. receiving or uncovering one growing plot;
4. choosing where to place that plot inside a valid zone;
5. planting the first ingredient;
6. placing one simple storage or decorative object;
7. receiving a clear reason to begin the first proper expedition.

The player should not receive a crowded catalogue or lengthy management tutorial at this point.

### 11.8 End-of-opening target

By the end of the opening chapter, the player should have:

- understood the movement and combat language;
- experienced death before and after securing a checkpoint;
- defeated the introductory boss;
- discovered the Hearthseed and abandoned Hearthstead;
- caused the first visible restoration;
- placed the first farm object themselves;
- understood that cultivation strengthens something larger than personal profit;
- seen at least one major corrupted route or distant landmark that motivates future exploration.

---

## 12. Hearthseed, Hearth Blooms, and World Restoration

### Central Hearthseed

The Hearthseed is the living centre of the Hearthstead and the visual representation of long-term restoration.

Its exact final form remains open. It may grow into a tree, great flower, interwoven root-and-bloom structure, or another distinctive living landmark.

### Regional boss rewards

Major regional bosses or objectives can provide **Hearth Blooms**, root hearts, grafts, or equivalent living fragments.

The preferred term is currently **Hearth Bloom**, while the central object remains the **Hearthseed**.

```text
Defeat regional threat
→ recover its Hearth Bloom
→ return to the Hearthstead
→ choose where it is displayed or planted within the home garden
→ graft its power into the central Hearthseed
→ restorative roots reach the associated world locations
```

The player may choose the Bloom's physical arrangement at home, while its progression effect remains authored and deterministic.

### Restoration categories

Different Blooms may restore different natural functions:

| Working Bloom family | Possible world effect |
|---|---|
| Root / Thorn Bloom | Withdraws hostile thorns and grows bridges or climbable roots |
| Tide / Reed Bloom | Purifies water and creates lily, reed, or current-based routes |
| Gale / Spore Bloom | Creates wind currents, floating seeds, or aerial platforms |
| Ember / Sun Bloom | Restores warmth, dormant flora, furnaces, or heat-dependent ecosystems |
| Lantern / Night Bloom | Pushes back darkness and awakens luminous plants |

These names and categories are exploratory, not a locked boss roster.

### Progression balance

Not every blocked route should depend on Hearth Blooms.

World access should combine:

- personal traversal abilities;
- restorative environmental changes;
- alchemical or knowledge-based solutions;
- persistent switches, doors, and shortcuts;
- occasional keys or authored quest gates.

The progression fantasy is:

```text
The protagonist becomes more capable
+
The world becomes healthier
+
The Hearthstead becomes stronger and more populated
```

---

## 13. Home Placement and Customisation

### Purpose

Placement is a core part of the Hearthstead fantasy, not a final cosmetic extra.

The enjoyment should come partly from deciding where functional and decorative objects belong and watching residents inhabit a space the player genuinely shaped.

### Recommended placement model

Use grid-assisted free placement inside authored Hearthstead zones.

```text
Enter placement mode
→ select a stored object
→ move a transparent preview
→ snap to a fine grid or valid anchor
→ display valid / invalid footprint
→ rotate, flip, or choose a variant where supported
→ confirm or cancel
```

### Placeable categories

Functional objects may include:

- growing plots and planters;
- brewing and processing stations;
- storage;
- drying racks;
- cooking or preparation stations;
- later cultivation utilities.

Decorative objects may include:

- furniture;
- lights;
- shelves;
- rugs;
- wall decoration;
- boss trophies;
- signs, plants, and garden ornaments.

### Fixed authored elements

The following should generally remain fixed:

- scene exits and transition routes;
- the main structure shells;
- critical quest locations;
- major Hearthseed installation points;
- camera and traversal boundaries;
- permanent world shortcuts;
- any structure that must support tightly authored narrative staging.

### Placement rules

Placed objects should generally:

- be movable again without permanent loss;
- return to storage when removed;
- not charge the player simply for repositioning them;
- clearly preview footprint and collision;
- never block required exits or interactions;
- save position, orientation, variant, and relevant functional state;
- not become arbitrary platforming geometry in the first version.

### Player expression

The player may create a wild garden, a dense workshop, a minimalist space, or an extremely orderly farm.

Farm health and story morality must not depend on whether the arrangement looks natural or symmetrical. The thematic distinction from avian cultivation comes from reciprocity and ecological effect, not visual neatness.

---

## 14. Progression Model

Underbrew should use several connected progression tracks with distinct rewards.

### 14.1 Traversal progression

Permanent abilities change movement possibilities and route access.

### 14.2 Combat progression

New attacks, cast options, healing utility, resource use, or modifiers increase expression without turning the game into gear-score progression.

### 14.3 Hearthstead progression

Facilities, placement zones, residents, and repaired structures visibly expand what can be cultivated, processed, learned, placed, or prepared.

### 14.4 Restoration progression

Hearthseed growth and Hearth Blooms cleanse authored world sites, return biodiversity, and create new traversal routes.

### 14.5 Knowledge progression

Recipes, journal discoveries, maps, creature information, avian technology, lore interpretation, and environmental understanding allow the player to recognise opportunities that were always present.

### 14.6 Community progression

Displaced travellers become visitors, friends, or residents. Their arrival changes dialogue, services, quests, ambience, and the social meaning of the Hearthstead.

### 14.7 World-state progression

Boss defeats, opened shortcuts, repaired mechanisms, deactivated extraction taps, activated switches, removed obstacles, and completed quests permanently alter the world.

### 14.8 Optional mastery progression

Optional health, resource, inventory, recipe, challenge, movement, decoration, and narrative rewards support completion-focused play without blocking the critical path.

---

## 15. Ability Framework

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

Aim for approximately **six to eight major player abilities**, including healing or combat utility.

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
| Sprint | Fast grounded traversal | Chase or timing utility | Planned; must justify itself against run and dash |

### Ability questions still open

- Which traversal abilities does the player begin with in the final game?
- Is Wall Cling a basic movement verb, an early unlock, or split between basic slide and advanced latch?
- Does Spirit Cast consume the same resource used by Bind?
- Can alchemical preparation temporarily imitate an ability, or must permanent traversal access always come from permanent unlocks?
- Is Drift Cloak a true glide, a slow-fall mantle, a wind interaction, or another form entirely?
- Does Sprint provide enough unique level-design value to justify an input and animation set?

### Temporary brews

Temporary alchemical effects should make an expedition safer, easier, faster, or more rewarding. They should rarely be required to preserve access to a permanent critical-path route after the player has discovered it.

A brew may provide the first solution to a local obstacle, but permanent progression or a nearby renewable supply should prevent frustrating return trips.

---

## 16. Farming-Lite Design

### Purpose

Farming exists to create anticipation, attachment to the Hearthstead, ingredient planning, visual growth, ecological restoration, and a reason to return. It is not intended to become the player's primary source of money or a parallel full-length campaign.

### Recommended first-release scope

- a small number of unlockable placement zones rather than a giant field;
- approximately 12–20 meaningful growable ingredient types across the full game;
- no more than a few readable growth stages per crop;
- simple planting and harvesting;
- forgiving growth that continues while the player explores;
- limited or automated maintenance after initial onboarding;
- no crop death from player absence;
- a clear journal entry showing source, growth conditions, uses, and ecological role;
- region-linked seeds or spores that make exploration feed cultivation;
- a few special growth conditions that create interesting goals without becoming a simulation.

### Possible special conditions

Use sparingly:

- grows only in shade or light;
- responds to a brew or processed soil additive;
- grows on walls, water, fungus beds, or ruins rather than standard soil;
- changes after a regional boss or restoration event;
- attracts a creature or resident;
- produces a different result when combined with another nearby plant;
- strengthens a particular Hearth Bloom or restoration function.

### Friction limits

The player should not need to:

- water every individual tile before every expedition;
- return before a short deadline;
- clear weeds that continuously respawn without strategic value;
- maintain large quantities of identical crops;
- memorise hidden profitability tables;
- carry a large separate toolbelt for basic interactions.

### Expansion rule

Do not add animals, seasons, weather-dependent crops, cooking, fishing, soil quality, fertiliser tiers, or farm automation merely because farming games commonly contain them. Each system must strengthen Underbrew's expedition, home, restoration, or alchemy loop.

---

## 17. Alchemy and Brewing

### Purpose

Alchemy is the bridge between exploration and preparation. It converts the world's plants, creatures, minerals, and knowledge into a small number of memorable effects.

### Ingredient sources

- cultivated plants;
- wild forage;
- enemy or creature materials;
- minerals and environmental deposits;
- quest or boss materials;
- processed ingredients created at home;
- rare regional discoveries.

### Brew categories

#### Expedition brews

Limited-use preparation that changes risk or approach.

Examples:

- temporary light or hazard resistance;
- protection from a regional environmental effect;
- improved recovery;
- temporary movement forgiveness;
- revealing hidden traces, roots, or machinery.

#### Combat brews

Clear tactical tools rather than stacks of small modifiers.

Examples:

- a thrown or placed concoction;
- a temporary alteration to Spirit Cast;
- resistance to a boss mechanic;
- a resource-generation or healing trade-off.

#### World and restoration brews

Brews used to grow, repair, awaken, cleanse, feed, deactivate extraction, or transform something in the world.

These are especially valuable because they connect the Hearthstead loop directly to authored exploration and quests.

### Recipe design rules

- A recipe should have an understandable purpose.
- Important recipes should be learned through discovery, quests, guided experimentation, residents, or major rewards—not random grinding.
- Ingredients should have multiple uses only when the choice is interesting and readable.
- Required critical-path recipes should avoid rare random drops.
- The interface should show known results, missing ingredients, and likely sources without demanding external notes.
- Brewing should be quick once the player has made the planning decision.

### Relationship to the original Underbrew prototype

The earlier prototype's Lumen Draught / light-potion concept remains a strong example: gather and process ingredients, learn or complete a recipe, then use the result to pass a meaningful exploration obstacle. The final version may rename or redesign this content, but the structure is worth preserving.

---

## 18. Resources, Inventory, and Economy

### Inventory principle

The inventory should support exploration decisions without becoming a constant capacity-management problem.

Recommended separation:

- **Key progression:** permanent and never consumes ordinary inventory space.
- **Ingredients/materials:** clear categories with generous capacity or stack limits.
- **Prepared brews:** deliberately limited enough to make loadout choices meaningful.
- **Seeds/growing items:** clearly connected to the cultivation and placement interface.
- **Placeable objects:** held in a dedicated home-storage catalogue rather than ordinary combat inventory.
- **Quest items:** tracked separately and never accidentally sold or consumed.

### Economy principle

Currency can support services, convenience, decoration, and selected upgrades, but should not replace exploration rewards.

Avoid making repetitive enemy farming the best way to progress. Important abilities, recipes, facilities, Blooms, and residents should come from authored objectives, discoveries, bosses, quests, or world milestones.

The game may not need a broad sell-everything economy. Specialist traders, resident requests, or exchanges for particular materials may fit better than universal item prices.

---

## 19. Residents, Found Family, and Quests

### Community purpose

The Hearthstead begins with one lonely protagonist and gradually becomes a small community.

Residents should arrive because the Hearthseed's restoration makes the place visible, viable, or safe. Their presence should feel like a direct result of the player's work.

Possible resident roles include:

- a displaced gardener who expands cultivation;
- a brewer, cook, or herbalist who develops recipes;
- an injured explorer who supports mapping;
- a craftsperson who creates furniture and functional placeables;
- a creature specialist who helps interpret corruption;
- a bird defector who reveals the extraction system and complicates the conflict;
- someone from the original woodland community who confirms that others survived.

Not every character must permanently move in. The Hearthstead may also become a crossroads for visitors.

### Cast scope

A small cast with changing dialogue, visible relationships, and clear roles is preferable to a large town of shallow characters.

NPCs should not require fully simulated daily schedules unless a specific design need justifies them.

### Quest purpose

Quests should direct attention toward the world, introduce systems, reveal character, create persistent changes, and provide reasons to revisit areas.

Prefer quests involving:

- discovering a location;
- helping a displaced character reach the Hearthstead;
- finding a particular ingredient, seed, object, or keepsake;
- brewing something with a world-facing use;
- resolving an environmental problem;
- defeating or understanding a creature;
- repairing or disabling extraction infrastructure;
- improving a resident's part of the Hearthstead;
- making a meaningful choice about a limited resource.

Avoid large numbers of generic quantity requests.

---

## 20. Mapping, Journal, and Information Design

The player needs enough information to plan exploration and cultivation without turning the game into checklist management.

### Map responsibilities

- show discovered rooms and major connections;
- communicate incomplete exploration without revealing every secret;
- mark player-selected objectives and useful Hearthstead facilities;
- support revisiting ability- and restoration-gated routes;
- distinguish incomplete, locked, corrupted, restored, and unexplored routes where appropriate.

### Journal responsibilities

- record ingredients, crops, creatures, recipes, residents, and important discoveries;
- show known sources, uses, growth conditions, and restoration links;
- identify missing knowledge clearly;
- surface quest and progression clues;
- record avian machinery and regional extraction effects;
- celebrate meaningful discoveries and completion milestones.

The earlier prototype's journal is a useful foundation, especially its role in making newly discovered items and recipes legible.

---

## 21. Combat, Enemies, and Boss Direction

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

### Enemy categories

The wider enemy roster should not consist entirely of birds.

Useful categories include:

1. **Avian personnel** — commanders, engineers, hunters, cultivators, or guards directly enforcing extraction.
2. **Affected native wildlife** — displaced, starving, territorial, or corrupted by Hearthroot failure.
3. **Avian constructs and cultivated organisms** — machinery, plant constructs, imported creatures, or controlled hybrids.
4. **Unaffiliated regional threats** — creatures and hazards that belong to the world independently of the central conflict.

### Boss direction

Bosses should serve at least two purposes:

- a mechanical test or culmination;
- a world, story, restoration, or progression consequence.

A boss defeat should often cause something tangible: a route opens, extraction machinery stops, a region changes, a resident is freed, a Hearth Bloom is recovered, or a major ability becomes available.

Not every boss should be an avian officer. A varied boss roster will make the ecological consequences of the conflict more credible.

---

## 22. Narrative and Tone

### Tone

Underbrew should support:

- melancholy places with warmth still present;
- recovery rather than simple conquest;
- displacement without constant despair;
- a home that becomes a source of belonging;
- quiet mechanical menace in avian spaces;
- strange humour and character warmth between difficult expeditions;
- environmental storytelling that rewards attention;
- visible before-and-after restoration;
- sympathetic individuals within an antagonistic system.

### Narrative delivery

The story should be communicated through a combination of:

- concise opening and milestone cutscenes;
- environmental evidence;
- changed world states;
- resident dialogue;
- boss and region consequences;
- journal discoveries;
- avian infrastructure and documents;
- the physical growth of the Hearthstead.

The story should explain why cultivation and brewing matter to the world rather than treating them as optional hobbies attached to an unrelated hero.

### Story questions still open

- What is the protagonist's name and exact personality?
- What is the formal name of the avian civilisation and its ruling institution?
- What destroyed or damaged the birds' original homeland?
- How much does avian leadership understand about the long-term effects of extraction?
- What does the extraction resource power?
- What is the ultimate source or centre of the Hearthroot network?
- What does restoration cost, and can every part of the world be restored?
- What does “Underbrew” refer to within the fiction?
- Which residents form the core found-family cast?
- What is the final relationship between the rebuilt Hearthstead and the displaced woodland community?

---

## 23. Working Full-Game Progression Framework

This is a structure for planning, not a locked sequence or set of biome names.

### Stage 0 — Displacement, survival, and first restoration

- establish the woodland settlement, avian occupation, and river separation;
- teach core movement and combat through the dedicated tutorial region;
- require the player to master the route before reaching the first checkpoint;
- defeat the corrupted native guardian;
- discover and plant the Hearthseed;
- restore the first cultivation and placement zone;
- introduce the Hearthstead as the central home;
- reveal the first proper expedition objective.

### Stage 1 — Two early regional objectives

- allow partial choice between two nearby regions;
- introduce the first major traversal unlock beyond the final starting kit;
- attract or recruit the first important Hearthstead resident;
- expand cultivation beyond the tutorial crop;
- introduce region-specific extraction effects and ingredients;
- recover the first major Hearth Bloom;
- open a meaningful cross-region or home shortcut.

### Stage 2 — Integrated midgame

- require combinations of learned movement rather than single-ability locks;
- make alchemy useful for preparation and world interaction without hard-gating every route;
- introduce more complex enemy combinations and a major avian-controlled site;
- reveal the birds' displaced origins and the ideology behind extraction;
- visibly transform the Hearthstead from survival space into a small community.

### Stage 3 — World reconnection

- use restored roots, persistent switches, doors, repaired systems, and regional outcomes to connect previously separate routes;
- grant one of the most expressive traversal abilities;
- return the player through significantly changed early spaces;
- complete major resident and facility arcs;
- unlock advanced but compact cultivation and brewing options.

### Stage 4 — Late-game convergence

- combine traversal, combat, preparation, restoration, and world knowledge;
- resolve remaining regional bosses or extraction sites in a flexible order where feasible;
- expose the final region through accumulated persistent changes rather than a single arbitrary key;
- force the avian leadership to respond directly to the growing Hearthseed;
- ensure optional upgrades remain useful but are not mandatory.

### Stage 5 — Finale and aftermath

- final region and boss sequence;
- confront the avian extraction system and its leadership;
- reflect important regional and resident outcomes;
- return to the Hearthstead after the climax;
- provide a satisfying visible state for the completed community and restoration;
- support post-game cleanup and optional challenges without requiring an endless simulation mode.

---

## 24. Preliminary Content Budget

These figures are planning ceilings, not promises.

| Content | Initial planning range |
|---|---:|
| Major adventure regions | 5–6 plus tutorial and Hearthstead |
| Major bosses | 5–7 |
| Minibosses / major encounters | 4–8 |
| Major permanent abilities | 6–8 total |
| Ordinary enemy families | 12–18, with meaningful variants/combinations |
| Growable ingredients | 12–20 |
| Major useful brew recipes | 12–18 |
| World/quest recipes and special processes | 8–15 |
| Core Hearthstead residents | 4–7 |
| Major quest lines | 5–8 |
| Optional challenge spaces | 6–12 |
| Hearth Blooms / equivalent regional restorations | Approximately 4–6 |

The budget should shrink if production quality, animation burden, environment art, placement persistence, or integration costs are higher than expected.

---

## 25. Production Strategy

The project should continue building the metroidvania foundation first, but later work must validate the combined identity before producing a large world.

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

### Phase B — Opening metroidvania vertical slice

Build the decided tutorial structure:

- folded route with no pre-checkpoint shortcut;
- opening respawn until checkpoint activation;
- movement and combat teaching rooms;
- first checkpoint immediately before the boss;
- introductory native boss;
- complete death, retry, save, and presentation flow;
- transition into a greybox Hearthstead.

This validates the opening without requiring the full farming layer.

### Phase C — Hearthseed and placement prototype

Build the smallest complete home loop:

- one Hearthseed activation;
- one authored placement zone;
- one moveable growing plot;
- one seed type;
- one growth cycle;
- one harvest;
- one simple placeable storage or decorative object;
- one visible environmental restoration;
- journal support.

Do not begin with a general furniture catalogue, dozens of crops, irrigation, shops, or full decoration support.

### Phase D — Farming and alchemy prototype

Add only what is required to prove the expedition connection:

- two or three plots;
- two growable ingredients;
- growth across expeditions;
- one processing action;
- one brew;
- one world interaction enabled by that brew;
- one visible Hearthstead improvement.

### Phase E — Integrated chapter

Create the first representative post-Hearthstead chapter:

- home visit and resident interaction;
- expedition objective;
- ingredient or seed discovery;
- cultivation or processing;
- meaningful brew or restoration use;
- traversal or boss progression;
- first Hearth Bloom;
- NPC/quest consequence;
- return route and visible home/world change.

Measure actual development cost and player time before locking the full content budget.

### Phase F — Production framework

Only after the integrated chapter works:

- finalise region list and progression graph;
- lock the major ability and Bloom sequence;
- define content templates and authoring tools;
- establish enemy, boss, crop, placeable, recipe, quest, resident, and room pipelines;
- set final scope based on measured throughput.

### Phase G — Full production and polish

Produce content region by region while continuously validating:

- navigation and sequence logic;
- revisit value;
- Hearthstead/exploration balance;
- placement usability and save safety;
- farming friction;
- recipe usefulness;
- restoration readability;
- narrative pacing;
- performance and accessibility;
- controller feel and game feedback.

---

## 26. Immediate Design Work

The broad premise is now decided. The next design work should focus on the opening chapter and first integrated region.

1. Name and visually define the tutorial region.
2. Design its room map around the validated hero movement.
3. Define the introductory native boss, its corruption, and its arena.
4. Decide the exact visual form of the Hearthseed and its first transformation.
5. Greybox the abandoned Hearthstead and its first placement zone.
6. Decide the final starting movement kit versus development defaults.
7. Define the first post-Hearthstead region and its traversal reward.
8. Define the first resident and why they remain at the Hearthstead.
9. Choose the smallest farming and placement loops that can validate the game's identity.
10. Define approximately six example recipes across expedition, combat, and restoration categories.
11. Establish the Hearthstead's major visual growth stages.
12. Create a rough world graph with the Hearthstead and five or six candidate regions.

---

## 27. Decisions Register

Use this table to stop settled decisions from being repeatedly reopened without a reason.

| Decision | Status | Current direction | Reason / evidence |
|---|---|---|---|
| Primary genre | Decided | Action-exploration metroidvania | Current controller, combat, world, boss, camera, and persistence foundations |
| Secondary genre layer | Decided | Farming/cultivation-lite, alchemy, and home customisation | Core creative direction |
| Protagonist | Decided direction | Lone young red panda herbalist | Strong visual identity and natural connection to agility, cultivation, and home |
| Opening settlement | Decided direction | Woodland community built around living trees | Establishes what has been lost and contrasts avian infrastructure |
| Inciting incident | Decided direction | Avian occupation and extraction force evacuation; protagonist is swept away by a river | Creates displacement without an urgent family-rescue conflict |
| Antagonist culture | Decided direction | Quiet, mechanical, brutalist avian civilisation using extractive cultivation | Connects visual identity, ecology, farming theme, and central conflict |
| Antagonist nuance | Decided | The system and leadership are antagonistic; birds are not inherently evil | Supports sympathetic individuals and avoids species-essentialist storytelling |
| Central home | Decided direction | Abandoned Hearthstead restored around a Hearthseed | Gives farming, story, placement, and world restoration one centre |
| Emotional arc | Decided | Lose a home → create a home → build a found community → protect the wider land | Keeps the Hearthstead central rather than temporary |
| Opening structure | Decided direction | Dedicated folded tutorial route before the Hearthstead | Cleanly teaches metroidvania fundamentals first |
| First checkpoint | Decided | No shortcut; first checkpoint immediately before introductory boss | Repetition teaches precision while boss retries remain respectful |
| First boss type | Decided direction | Native creature affected by Hearthroot failure, not an avian commander | Introduces consequence and mystery before direct faction conflict |
| First major reward | Decided direction | Hearthseed and access to the abandoned Hearthstead | Immediately establishes Underbrew's unique loop |
| Regional boss rewards | Direction set | Hearth Blooms strengthen the central seed and restore authored world routes | Connects bosses, home growth, and metroidvania traversal |
| Home placement | Decided direction | Grid-assisted placement within authored Hearthstead zones | Captures player expression without turning the game into an unrestricted level editor |
| Farming morality | Decided | Orderly player farms are valid; extraction and depletion are the problem | Preserves farming-game enjoyment and thematic clarity |
| System priority | Decided | Complete metroidvania implementation foundation before broad farming implementation | Reduces integration risk and matches current work |
| Farming depth | Direction set | Small, low-maintenance, expedition- and restoration-supporting | Avoids building two complete games |
| Target length | Direction set | 12–15 hour critical path; 20–30 thorough playthrough | More feasible than a mandatory 20–30 hour campaign |
| World structure | Proposed | Central physical Hearthstead plus 5–6 major interconnected regions | Supports return loop and authored metroidvania structure |
| Major ability count | Proposed | 6–8 total | Limits animation, level, input, and testing burden |
| Final starting kit | Open | Development defaults are not final progression | Requires opening level/progression testing |
| Crop count | Proposed ceiling | 12–20 meaningful growables | Enough variety without simulation sprawl |
| Mandatory temporary brews | Open | Use cautiously; avoid repeated critical-path consumable friction | Requires integrated chapter testing |

---

## 28. Open Design Backlog

### Highest priority

- tutorial-region identity and room map;
- introductory boss species, moveset, visual corruption, and reward staging;
- Hearthseed visual design and first planting sequence;
- Hearthstead greybox, placement boundaries, and first restoration state;
- final starting abilities;
- first post-Hearthstead region;
- first resident and found-family introduction;
- first integrated chapter;
- exact relationship between resource, Bind, Spirit Cast, and brews.

### Narrative priority

- protagonist name, voice, personality, and herbalist history;
- formal avian faction name and hierarchy;
- cause of the avian homeland's decline;
- extraction resource and what it powers;
- core resident cast and interpersonal arcs;
- bird defector or internal opposition possibilities;
- meaning of “Underbrew” in the fiction;
- final-act conflict and resolution boundaries.

### Medium priority

- map acquisition and annotation rules;
- inventory and prepared-brew capacity;
- death consequences beyond checkpoint respawn;
- currency and trade model;
- resident roles and services;
- quest structure and tracking;
- farming growth timing and maintenance model;
- recipe discovery method;
- placeable catalogue and storage interaction;
- optional challenge and completion structure.

### Later

- difficulty and accessibility modes;
- achievements;
- post-game state;
- localisation scope;
- platform targets;
- final content count and release roadmap.

---

## 29. Document Maintenance Rules

This is a living document. Update it when a design decision changes, but do not use it as a changelog.

### Status language

- **Decided** — plan and implement around this unless deliberately revised.
- **Direction set** — preferred approach, but still requires validation.
- **Proposed** — a concrete option worth testing.
- **Open** — not yet decided.
- **Cut / deferred** — intentionally outside the current release scope.

### When a section becomes too detailed

Create a focused design document under `Docs/GameDesign/`, for example:

```text
Docs/GameDesign/
├── UnderbrewGameDesign.md
├── OpeningChapter.md
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

## 30. Next Revision Goal

Version 0.3 should focus on **the complete opening chapter and the first Hearthstead state**.

The next revision should answer:

1. What is the tutorial region called, and what is its visual identity?
2. What is the room-by-room route from river arrival to first checkpoint?
3. What native creature is the introductory boss?
4. How has Hearthroot failure changed that creature?
5. Where and how is the Hearthseed revealed after the fight?
6. What does the abandoned Hearthstead look like before planting?
7. What exactly changes during the first Hearthseed transformation?
8. What is the player's first placeable plot, crop, and object?
9. What objective leads them into the first full region?
10. Which first resident or visitor demonstrates that the Hearthstead is becoming a beacon?

Once these are clear, the tutorial and Hearthstead can be greyboxed as one coherent opening rather than as disconnected system tests.
