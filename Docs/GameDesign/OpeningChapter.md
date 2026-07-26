# Underbrew — Opening Chapter

**Status:** Working chapter design  
**Version:** 0.1  
**Created:** 2026-07-26  
**Parent document:** `Docs/GameDesign/UnderbrewGameDesign.md`

> This document defines the intended player experience, progression, and content of Underbrew's opening chapter. It is not an implementation-status document.
>
> - `Docs/Architecture.md` remains authoritative for technical ownership and system boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what currently exists and what should be implemented next.
> - `Docs/HeroFeelTuning.md` remains authoritative for validated hero movement and combat values.
> - `Docs/GameDesign/WorldAndProgression.md` owns the wider progression framework that begins after this chapter.

---

## 1. Chapter Purpose

The opening chapter must establish both halves of Underbrew without teaching them simultaneously.

The player first experiences a focused metroidvania introduction that teaches movement, combat, danger, death, and checkpoint value. Only after proving themselves through that route and defeating the introductory boss do they discover the Hearthstead, plant the Hearthseed, and learn that the game is also about cultivation, restoration, personal expression, and community.

The intended emotional progression is:

```text
Loss
→ isolation
→ survival
→ movement mastery
→ relief at the first checkpoint
→ victory over a damaged guardian
→ discovery of a possible refuge
→ first act of restoration
→ ownership and hope
```

The chapter should leave the player thinking:

> “The world outside is dangerous, but I now have somewhere worth returning to.”

---

## 2. Chapter Boundaries

### Chapter begins

The opening cutscene starts in the protagonist's woodland settlement as the avian civilisation evacuates its inhabitants and taps the living trees.

### Chapter ends

The chapter ends once the player has:

- planted the Hearthseed;
- restored the first small area of the Hearthstead;
- placed the first cultivation bed;
- planted the first crop or ingredient;
- used or inspected the first basic home facility;
- received at least two visible directions for the first proper expedition phase.

### Target duration

| Player experience | Target duration |
|---|---:|
| Opening cutscene | 2–4 minutes |
| Tutorial route, first attempt | 15–25 minutes |
| Tutorial route after learning it | 4–8 minutes |
| First boss and retries | 5–20 minutes |
| Hearthseed and Hearthstead introduction | 15–25 minutes |
| Full opening chapter | 45–75 minutes |

These are pacing targets, not production promises.

---

## 3. Opening Cutscene

### 3.1 Narrative purpose

The cutscene should communicate:

- the protagonist had a place within a real community;
- the settlement is adapted around living trees rather than built over them;
- the avian civilisation is organised, quiet, mechanical, and intimidating;
- the birds are draining the land through visible extraction infrastructure;
- the protagonist is displaced and isolated;
- the entire ecological explanation is not yet known.

The cutscene should not frontload:

- the full history of the avian civilisation;
- the complete nature of the Hearthroot network;
- the names of every faction or region;
- a prophecy or chosen-hero explanation;
- a lengthy family-rescue motivation.

### 3.2 Provisional sequence

```text
Woodland settlement built around living trees
→ protagonist performs a small herbalist task or moves through daily life
→ quiet avian machines and brutalist structures are revealed
→ extraction taps pierce one or more living trees
→ light, sap, colour, or vitality moves through pipes away from the settlement
→ armed birds escort residents out
→ the protagonist is carried along with the evacuation
→ extraction damage destabilises a living bridge, root path, or riverbank
→ the protagonist falls into the river
→ he is swept away before anyone can reach him
→ short river and wilderness montage
→ fade into gameplay at the tutorial start
```

### 3.3 Protagonist state

The protagonist awakens alone with only the final starting kit.

He does not know where the rest of the settlement has gone, but the story does not frame this as an immediate rescue timer. His immediate objective is survival:

> Find safety and keep moving.

---

## 4. Tutorial Region

### 4.1 Working identity

The final region name and biome are open. The route should feel like a contained natural corridor that plausibly lies downstream from the settlement and leads toward the abandoned Hearthstead.

Strong candidate identities include:

- a root-choked ravine;
- a dried or damaged watercourse;
- an overgrown gorge beneath tapped trees;
- an abandoned cultivation or irrigation route;
- a natural basin partially affected by avian extraction.

The region should contrast with both:

- the warmth of the opening settlement; and
- the open, customisable Hearthstead revealed after the boss.

### 4.2 Core layout principle

The route should use the same broad lesson that makes Moss Grotto effective:

> The route itself is the tutorial examination.

The player begins physically near the upper destination but must descend, travel through the lower region, climb back through a folded route, and earn the first checkpoint through repeated movement practice.

```text
START                                      CHECKPOINT → BOSS
  ● ───── visible but inaccessible ─────────── ● ────────►
  │                                             ▲
  ▼                                             │
  │      folded lower traversal route           │
  └─────────────────────────────────────────────┘
```

There is no shortcut that bypasses the tutorial route before the first checkpoint.

### 4.3 Why there is no early shortcut

The opening route intentionally asks the player to repeat its movement fundamentals after a death.

Repeated traversal should allow the player to notice:

- cleaner jumps;
- better use of variable jump height;
- more confident enemy positioning;
- fewer unnecessary stops;
- faster route completion;
- growing familiarity with the hero's movement.

The chapter should create challenge through consistency and learning, not punishment or attrition.

---

## 5. Starting Player Kit

The exact final starting kit must remain compatible with the wider ability plan in `WorldAndProgression.md`.

### Working starting verbs

- walk and run;
- variable-height jump;
- normal aerial control;
- side, up, and down melee attacks;
- downslash pogo on valid targets;
- basic resource generation through combat;
- Bind or the project's core healing action, provided the opening teaches it clearly.

### Not assumed during tutorial greyboxing

Unless deliberately revised, the tutorial route should not require:

- Dash;
- Double Jump;
- Drift Cloak;
- Wall Latch;
- Spirit Cast;
- a farming tool;
- a consumable brew;
- a temporary environmental buff.

Wall-slide and wall-jump remain an open starting-kit decision. The first greybox should not depend on them until their final progression position is settled.

---

## 6. Route Structure

The tutorial should contain approximately ten functional beats. These may be spread across several scenes or rooms, but the player should experience them as one continuous route.

### Beat 1 — Riverbank awakening

Purpose:

- hand control to the player quickly;
- establish direction and atmosphere;
- provide the deterministic pre-checkpoint respawn location;
- allow safe movement without combat.

Content:

- clear route forward;
- a small safe jump;
- visual evidence that the river carried the protagonist here;
- optional environmental detail from the evacuation.

### Beat 2 — Controlled descent

Purpose:

- teach dropping, landing, and variable jump height;
- move the player away from the visible destination;
- establish the folded route.

Content:

- forgiving platforms;
- no lethal punishment for the first missed jumps;
- one route that visually closes behind or above the player without feeling arbitrary.

### Beat 3 — First precision sequence

Purpose:

- require several clean but forgiving jumps;
- teach the player to observe landing space;
- introduce a recoverable hazard or safe fall loop.

Content:

- two or three connected jumps;
- a safe reset or short local recovery;
- no enemy interference yet.

### Beat 4 — First enemy

Purpose:

- teach attack range, enemy tells, and recovery;
- provide a controlled resource-generation opportunity;
- keep the environment simple during the first fight.

Content:

- one low-threat native creature;
- enough space to reposition;
- no pit directly behind the player.

### Beat 5 — Movement under pressure

Purpose:

- combine traversal and an enemy;
- teach that combat positioning is spatial;
- introduce the risk of being knocked from a platform without making it unfair.

Content:

- one or two enemies at most;
- uneven but readable terrain;
- a quick return if the player falls without dying.

### Beat 6 — Hazard lesson

Purpose:

- teach the game's local hazard recovery or damage language;
- communicate that careless movement has a cost;
- avoid surprising instant-death rules.

Content:

- one clearly signposted hazard type;
- a safe observation point;
- no enemy forcing the player into it on first contact.

### Beat 7 — Optional discovery pocket

Purpose:

- reward curiosity without granting a mandatory upgrade;
- establish that routes may hide lore, ingredients, currency, or future hooks;
- show one inaccessible feature worth remembering.

Possible rewards:

- a journal entry;
- a small permanent currency or material reward;
- an image or clue related to extraction machinery;
- an unreachable ledge or root anchor for later revisiting.

### Beat 8 — Vertical return and final movement test

Purpose:

- climb back toward the upper level;
- combine previously taught movement patterns;
- make the checkpoint feel earned.

Content:

- a longer sequence using familiar jumps;
- one restrained enemy placement;
- no new mechanic introduced in the final challenge;
- visual proximity to the opening area or checkpoint approach.

### Beat 9 — First checkpoint chamber

Purpose:

- deliver relief;
- teach checkpoint interaction and save meaning;
- prepare the player emotionally for the boss.

Content:

- a quiet, enemy-free chamber;
- a clear and memorable checkpoint silhouette;
- a glimpse, sound, shadow, or environmental clue suggesting the boss ahead;
- no combat or platforming between the checkpoint and the arena trigger.

### Beat 10 — Introductory boss

Purpose:

- test basic movement, attack timing, positioning, healing windows, and observation;
- show the ecological consequences of Hearthroot failure;
- create the transition into the Hearthseed and Hearthstead systems.

---

## 7. Death and Checkpoint Rules

### Before the first checkpoint

```text
Death
→ fade / death sequence
→ restore the tutorial route's normal reset state
→ respawn at the riverbank opening marker
```

The opening position is the authoritative respawn point until the first checkpoint is activated.

### After activating the checkpoint

```text
Death
→ respawn at the checkpoint immediately before the boss
→ short, safe return to the arena
```

### Required design rules

- no activatable checkpoint appears earlier in the tutorial;
- no shortcut bypasses the route before the checkpoint;
- no enemy stands between checkpoint and boss;
- no lengthy dialogue or cutscene repeats on boss retries;
- the route must be short once mastered;
- early hazards should teach rather than repeatedly erase long progress;
- the opening respawn marker must be deterministic and never depend on an arbitrary “first marker found” fallback.

---

## 8. Introductory Boss Design Brief

### 8.1 Narrative role

The first boss should be a native creature or former local guardian affected by the failing Hearthroot network.

It should not be the main avian villain or a senior bird commander. The player should encounter the consequences of extraction before fully understanding the system causing them.

### 8.2 Relationship to the Hearthseed

Working direction:

- the creature has been drawn to, tangled around, guarding, or corrupted by the dormant Hearthseed;
- damaged roots, invasive growth, or extraction residue have made it aggressive;
- defeating it releases the Hearthseed and restores the route beyond the arena;
- the fight should feel necessary but not celebratory cruelty toward an innocent animal.

The exact species, silhouette, attack set, and history remain open.

### 8.3 Mechanical role

The boss may test:

- reading a clear startup tell;
- moving through or around one main attack;
- attacking during recovery;
- jumping over a grounded threat;
- repositioning without overcommitting;
- healing during a safe window;
- using downslash/pogo if the final encounter supports it naturally.

The boss should not require:

- an unexplained parry;
- a traversal ability not yet acquired;
- a consumable preparation;
- hidden elemental weakness;
- long damage phases;
- excessive health intended to extend the fight artificially.

### 8.4 Defeat transformation

Boss defeat should visibly change the room:

- hostile growth loosens or retracts;
- a blocked spring, root, or source begins moving again;
- the arena becomes calm;
- the dormant Hearthseed becomes accessible;
- a passage toward the Hearthstead opens.

The arena may later remain useful as:

- a restored spring or Hearthwell;
- a rare wild ingredient location;
- a visual source feeding the Hearthstead;
- a future shortcut or root-travel point;
- a small sanctuary rather than the main farm.

---

## 9. Hearthseed Acquisition

The Hearthseed is the chapter's central reward.

### Working form

The precise design is open, but it should read as a living object rather than a conventional key. Strong forms include:

- a dormant flower bulb containing a glowing root-heart;
- a compact seed with visible internal light;
- a closed Hearth Bloom capable of rooting into an old network.

### Player understanding at acquisition

The player does not need to understand the entire Hearthroot system immediately.

They should understand:

- the object is alive;
- the boss or corruption was preventing it from recovering;
- it responds to the route beyond the arena;
- carrying it toward the abandoned Hearthstead is the obvious next action.

---

## 10. Hearthstead Reveal

### 10.1 Spatial reveal

The Hearthstead should be immediately beyond, above, or clearly connected to the boss arena.

The reveal should contrast with the tutorial region:

- broader composition;
- warmer or more hopeful lighting;
- visible but damaged structures;
- open space suitable for future placement;
- quiet rather than threatening ambience;
- several unusable areas hinting at long-term growth.

The Hearthstead should initially feel abandoned, not already cosy.

### 10.2 Initial state

The first visit should show:

- one central dormant planting or Hearthseed location;
- one damaged shelter or workshop;
- a small initial placement zone;
- blocked or corrupted extensions;
- one basic storage or interaction point;
- several visual hooks for future residents and facilities;
- at least two visible exits or future route directions.

---

## 11. Planting the Hearthseed

### 11.1 Interaction

The Hearthseed is planted at an authored central location or chosen from a very small set of prominent anchors.

The main narrative seed should not support unrestricted placement because story presentation, roots, world restoration, camera composition, and save logic need a stable anchor.

The act should still feel owned by the player:

- the player carries it to the location;
- confirms the planting;
- sees the protagonist physically interact with the soil or root cradle;
- watches the transformation begin from the chosen action.

### 11.2 First transformation

```text
Plant Hearthseed
→ roots spread through visible channels beneath the area
→ a spring or source begins flowing
→ local corruption withdraws
→ colour and ambient life return to one small zone
→ one cultivation area becomes usable
→ one basic home facility powers or opens
→ inaccessible outer sections remain visibly dormant
```

The transformation should be impressive enough to communicate the game's premise, but limited enough that future Hearth Blooms can produce substantial growth.

### 11.3 First major progression payoff

The Hearthseed should grant or enable at least three immediate benefits:

1. **Home:** a usable Hearthstead and first placement zone.
2. **Cultivation:** the ability to place and use the first growing bed.
3. **World:** an initial cleansing or living structure that reveals the next routes.

A personal movement ability may also be tied to the planting, but this remains a progression decision rather than a locked chapter requirement.

---

## 12. First Placement and Farming Tutorial

### 12.1 Tutorial principle

The first home tutorial should be tactile and brief. It should prove player ownership without opening a large catalogue or management interface.

### Required first actions

```text
Receive or uncover one basic cultivation bed
→ enter placement mode
→ move the preview within the initial placement zone
→ confirm a valid location
→ plant one starter ingredient
→ inspect its expected growth and use
```

### First placeable object

The first cultivation bed should:

- support grid-assisted free placement within the valid zone;
- be movable again without loss;
- clearly show valid and invalid placement;
- avoid blocking exits and critical interactions;
- contain several planting slots rather than requiring many individual soil tiles;
- save its position and state.

### First crop

The starter crop should:

- have a short and understandable growth cycle;
- not die if the player leaves;
- require no repeated daily watering;
- have a clear early use in healing, brewing, a quest, or the first expedition;
- provide a journal entry explaining growth and purpose.

### First farming boundary

The opening should not introduce:

- many crop types;
- fertiliser tiers;
- irrigation management;
- crop quality;
- seasons;
- animals;
- a complex economy;
- large-scale furniture catalogues;
- repeated maintenance chores.

---

## 13. First Hearthstead Facilities

The exact set remains open, but the chapter should expose only a minimal functional loop.

### Available or partially usable

- one storage interaction;
- one cultivation bed;
- one basic herbalist or processing surface;
- the central Hearthseed;
- the checkpoint or rest function associated with the Hearthstead.

### Visible but unavailable

- expanded cultivation ground;
- a proper brewing station or upgrade;
- resident spaces;
- specialist growing environments;
- furniture crafting;
- root travel or fast travel;
- advanced processing.

Locked facilities should create curiosity without filling the screen with upgrade prompts.

---

## 14. Chapter-End Direction Choice

The opening should end by giving the player more than one meaningful direction.

The Hearthseed's first roots may:

- retract corruption from two exits;
- grow a bridge toward one region while exposing a second lower route;
- reveal two weak signals from damaged Hearthroot sites;
- allow an arriving traveller, map clue, or environmental landmark to suggest multiple objectives.

The player should not receive a rigid quest list that says:

```text
Complete Region A
then Region B
then Region C
```

Instead, the chapter should communicate:

> Several parts of the land are in trouble. Pick the direction that interests you.

The wider rules for that freedom are defined in `WorldAndProgression.md`.

---

## 15. Opening Chapter Reward Summary

By the end of the chapter, the player has gained:

### Personal progression

- mastery of the starting movement and combat kit;
- confidence navigating hazards and enemies;
- understanding of Bind/healing if included;
- possibly one Hearthseed-linked movement unlock, pending the final ability sequence.

### Home progression

- the Hearthstead;
- the planted Hearthseed;
- an initial placement zone;
- one placeable growing bed;
- one basic crop;
- minimal storage and processing capability.

### World progression

- the first restored Hearthroot connection;
- a cleansed local area;
- an opened path beyond the tutorial;
- at least two candidate expedition directions.

### Narrative progression

- personal loss and displacement established;
- the avian faction established as an occupying force;
- extraction damage shown but not fully explained;
- a new purpose established through protecting and growing the Hearthseed.

---

## 16. Implementation Dependencies

This chapter is a game-design target. Before implementation, inspect the actual repository and create focused implementation plans as required.

### Existing foundations likely involved

- hero movement, combat, damage, Bind, and resource systems;
- checkpoints, respawn markers, and save flow;
- camera bounds, locks, presentation, and scene transitions;
- enemy and boss encounter foundations;
- world persistence and encounter completion;
- HUD and boss health presentation;
- World Graph Editor scene connections.

### New systems not yet assumed implemented

- opening cutscene flow;
- dialogue or narrative presentation;
- Hearthseed planting and restoration presentation;
- homestead placement mode;
- cultivation beds, crops, and growth;
- home facility progression;
- resident arrival and home population;
- living world restoration structures.

---

## 17. Unity Editor Work Required Later

The eventual greybox and vertical slice will require:

- a new opening scene or connected scene group;
- an authored opening `RespawnMarker` used before any checkpoint activation;
- no `CheckpointInteractable` until the pre-boss chamber;
- the first checkpoint directly before the boss;
- camera bounds, lock areas, and reveal framing;
- terrain, hazard, enemy, and breakable layers as required;
- ordinary enemy placements that teach rather than overwhelm;
- a boss encounter definition, participant, arena barriers, camera presenter, and HUD request;
- a post-boss transition into the Hearthstead;
- stable world and encounter IDs;
- one `RoomVisitReporter` per gameplay scene;
- Build Settings and World Graph Editor links;
- Hearthseed and Hearthstead placeholder presentation;
- one initial placement zone and cultivation-bed placeholder;
- Inspector wiring for every referenced config and persistent state asset.

No claim should be made that the chapter works until it has been greyboxed and manually played through in Unity.

---

## 18. Validation Checklist

### Opening route

- [ ] A first-time player understands where to go without a large navigation marker.
- [ ] Basic movement is introduced before combat pressure.
- [ ] The final route tests previously taught actions rather than introducing new ones.
- [ ] Dying before the checkpoint returns to the opening spawn.
- [ ] The route takes only a few minutes once learned.
- [ ] The route contains no shortcut that bypasses its core movement lesson.
- [ ] The player can recognise personal improvement after repeated attempts.

### Checkpoint and boss

- [ ] The first checkpoint creates visible and emotional relief.
- [ ] Boss retries contain no enemy or traversal runback.
- [ ] The boss tests the starting kit only.
- [ ] Attacks have readable tells, active windows, and recovery.
- [ ] The defeat transformation communicates ecological restoration.
- [ ] The Hearthseed reward is visually and mechanically clear.

### Hearthstead

- [ ] The reveal contrasts strongly with the tutorial region.
- [ ] The initial area is visibly incomplete but not visually confusing.
- [ ] Planting the Hearthseed changes the space immediately.
- [ ] The player places the first cultivation bed themselves.
- [ ] The first crop has an obvious purpose.
- [ ] At least two future directions are visible by chapter end.
- [ ] The home feels like the beginning of something, not a completed town.

---

## 19. Open Decisions

### High priority

- final tutorial-region identity and name;
- final starting movement kit;
- whether planting the Hearthseed grants Dash or another personal ability;
- first enemy species and behaviour;
- first boss species, attacks, and relationship to the Hearthseed;
- Hearthseed visual form;
- exact Hearthstead layout and initial placement-zone size;
- first crop identity and gameplay use;
- how the chapter presents the two initial region choices.

### Later

- final opening cutscene style and production method;
- dialogue or internal narration during the first Hearthstead sequence;
- exact music transitions;
- optional tutorial accessibility assists;
- whether the player can revisit the original riverbank immediately after gaining later movement.

---

## 20. Next Design Pass

The next revision should create a room-by-room map and content table containing:

- room purpose;
- movement lesson;
- enemy and hazard content;
- checkpoint/respawn behaviour;
- camera requirement;
- optional discovery;
- expected first-attempt and mastered traversal time.

It should also define the first boss at a concept level before any final art or detailed attack implementation begins.
