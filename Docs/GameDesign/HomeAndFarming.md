# Underbrew — Home and Farming Design

**Status:** Working system design  
**Version:** 0.1  
**Created:** 2026-07-26  
**Parent document:** `Docs/GameDesign/UnderbrewGameDesign.md`

> This document defines the player-facing purpose and progression of cultivation, the Hearthseed, home customisation, and the use of harvested plants. It is not an implementation-status document.
>
> - `Docs/Architecture.md` remains authoritative for technical ownership and system boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what currently exists and what should be implemented next.
> - `Docs/GameDesign/WorldAndProgression.md` owns the wider reward and regional progression structure.
> - `Docs/GameDesign/ShopAndAlchemy.md` owns apothecary operation, alchemical products, commissions, reputation, and shop economy.

---

## 1. Purpose

Farming in Underbrew must provide an ongoing reason to plant, grow, harvest, reorganise, and discover new crops without becoming:

- a crop-profit optimisation game;
- a stamina or hunger maintenance system;
- the primary source of combat healing;
- a daily-calendar obligation;
- a system where the Hearthseed consumes harvested crops as fuel;
- an endless grind for generic farming experience.

The core cultivation fantasy is:

> **The player establishes a living garden whose successful growth strengthens the Hearthseed, supplies an apothecary, restores damaged ecosystems, supports a growing community, and gives the player materials to shape the Hearthstead.**

The player should feel that growing plants creates life and possibility. They should not feel that they are raising crops only to feed them into another resource bar.

---

## 2. Core Farming Loop

```text
Explore the world
→ discover a seed, cutting, spore, plant use, or ecological problem
→ establish the species at the Hearthstead
→ cultivate it through one or more growth cycles
→ harvest useful produce while the completed cycle strengthens the Hearthseed
→ use the produce in alchemy, the shop, resident projects, crafting, or restoration
→ unlock new plants, growing conditions, facilities, routes, and community changes
→ return to exploration with new goals
```

This loop gives every harvest two connected outputs:

```text
Completed growth cycle
→ restorative vitality and knowledge for the Hearthseed

Harvested produce
→ useful material retained by the player
```

The crop is not sacrificed to the Hearthseed. The Hearthseed benefits because cultivation has restored living activity to the soil and connected another healthy organism to the Hearthroot network.

---

## 3. Hearthseed Symbiosis

### 3.1 Fictional rule

Plants grown within the Hearthstead become temporarily connected to the Hearthseed through the shared soil and root network.

As a plant develops, it can:

- return nutrients and living activity to depleted soil;
- carry moisture, warmth, light, or cleansing properties through the roots;
- attract insects, wildlife, spores, or pollinators;
- help stabilise an ecosystem damaged by extraction;
- teach the Hearthseed how a regional species survives;
- strengthen a corresponding restorative branch of the Hearthseed.

When the plant reaches maturity and is harvested, its completed growth cycle leaves a restorative imprint in the Hearthroots. The player keeps the harvested material.

### 3.2 Required presentation

The system should be communicated through the world rather than only through numbers.

Possible feedback includes:

- a pulse travelling from the cultivation bed into nearby roots;
- light or sap moving beneath the soil;
- subtle Hearthseed leaf, bud, or branch changes;
- healthier soil and ambient plant movement;
- insects or small creatures returning;
- corruption retracting from a future placement zone;
- a journal note explaining what the Hearthseed learned from the plant.

A generic `+5 Hearthseed XP` presentation should not be the primary feedback.

### 3.3 No crop cannibalisation

The Hearthseed should not normally accept stacks of crops through a deposit menu.

Avoid:

```text
Harvest vegetables
→ put vegetables into Hearthseed inventory
→ convert vegetables into tree currency
```

This would make the Hearthseed feel extractive and would weaken the thematic contrast with the avian civilisation.

The Hearthseed grows because the garden is healthy, diverse, and successfully cultivated—not because it consumes the garden's output.

---

## 4. How Cultivation Advances the Hearthseed

### 4.1 Diversity before bulk

Hearthseed progression should reward:

- successfully establishing new species;
- completing a manageable number of harvest cycles;
- cultivating plants with different ecological traits;
- growing regional species connected to recovered Hearth Blooms;
- satisfying visible ecosystem combinations;
- completing authored restoration projects.

It should not reward endlessly repeating the fastest-growing crop.

### 4.2 Example stage requirement

Illustrative only:

```text
Rooted Hearthseed stage

✓ Recover and plant one regional Hearth Bloom
✓ Successfully cultivate three different crop species
✓ Complete six total harvest cycles
✓ Grow one cleansing or water-binding species
✓ Restore the first damaged root node
```

The exact counts must be validated through the farming prototype. The important rule is that exploration, diversity, cultivation, and authored world progress all contribute.

### 4.3 Limited contribution per species

A crop may contribute strongly when:

- grown for the first time;
- used to complete a new ecological combination;
- connected to a new Hearth Bloom branch;
- successfully cultivated under a specialist condition;
- used in a major Hearthstead or world-restoration project.

Further harvests remain useful through alchemy, the shop, crafting, residents, and propagation, but should not allow one crop to grind through every Hearthseed stage.

---

## 5. Ecological Crop Traits

Crop families may have one or two clear ecological traits. These are design tags, not hidden simulation statistics.

| Trait | Hearthstead and world relationship |
|---|---|
| Nourishing | Rebuilds ordinary soil and supports basic cultivation |
| Water-binding | Restores moisture, ponds, streams, reeds, or wetland roots |
| Cleansing | Reduces poison, rot, invasive growth, or extraction residue |
| Structural | Produces strong fibres, stabilising roots, bridges, or supports |
| Warming | Restores heat to soil, facilities, or dormant ecosystems |
| Luminous | Awakens light-sensitive plants, roots, and crafted lighting |
| Pollinating | Supports biodiversity, variants, fruiting, and ambient life |
| Fungal | Breaks down waste, supports shade beds, and reconnects buried systems |
| Aromatic | Supports apothecary products, residents, and wildlife interactions |

Traits should make plant purpose legible without becoming a complicated soil-stat spreadsheet.

---

## 6. Bosses, Hearth Blooms, and Farming

Major regional progression should use a three-part relationship:

```text
Resolve regional threat
→ recover a Hearth Bloom or equivalent restorative catalyst
→ plant or graft it at the Hearthstead
→ unlock a dormant Hearthseed branch
→ cultivate compatible regional crops
→ branch reaches maturity
→ permanent Hearthstead and world transformations occur
```

The boss or regional objective unlocks restorative potential. Farming fulfils that potential.

This prevents either side of the game from replacing the other:

- exploration is required to discover the Bloom and regional plants;
- cultivation is required to establish and mature the restored ecosystem;
- the world change creates new exploration routes and objectives.

### Example: Tide branch

```text
Resolve the damaged wetland
→ recover the Tide Bloom
→ graft it onto the Hearthseed
→ discover and cultivate water-loving regional plants
→ complete the branch's cultivation goals
→ Hearthstead pond and shallow-water beds awaken
→ polluted pools elsewhere are cleansed
→ reeds, lilies, and root bridges create new traversal routes
```

---

## 7. Primary Uses for Harvested Crops

### 7.1 Apothecary and alchemy

The most consistent repeatable use of crops is as renewable material for products made and sold through the Underbrew apothecary.

Crops may provide:

- medicinal leaves, roots, petals, spores, or sap;
- fibres, oils, waxes, resins, pigments, and fragrances;
- growing compounds and grafting mixtures;
- lamp oils and practical household products;
- cleansing or restoration compounds;
- ingredients for resident and regional commissions.

The player sells transformed products more often than raw crops. See `ShopAndAlchemy.md`.

### 7.2 Hearthstead projects

Cultivated materials support visible permanent projects such as:

- specialist cultivation beds;
- a pond, trellis wall, shade garden, or fungal space;
- shop shelves, counters, signs, storage, or displays;
- resident homes and workspaces;
- lights, paths, communal areas, and decorations;
- adapting recovered avian equipment safely;
- repairing structures without replacing the Hearthstead's living character.

Projects should use curated combinations and regional components rather than huge stacks of one crop.

### 7.3 Resident stories and community needs

Residents may request plants or products to:

- establish a profession or workspace;
- recover a cultural practice from a lost home;
- help another resident;
- complete a personal project;
- research a regional ecosystem;
- hold a celebration or community event;
- create a new shop service or product family.

Requests should be non-expiring and positive. Residents should not starve, leave, or become unhappy because the player spends a long time exploring.

### 7.4 Crafting and customisation

Harvested plants can become renewable materials for repeated placeable crafting.

Examples:

- fibres for rugs, baskets, screens, awnings, and trellises;
- pigments for paint, ceramics, fabrics, and furniture variants;
- reeds and stalks for fences, shelves, and planters;
- resin and wax for lights, repairs, and polished objects;
- luminous petals for lanterns and decorative lighting;
- dried flowers for displays and resident spaces;
- fragrant plants for incense, hanging bundles, and shop presentation.

This gives decoration-focused players a strong optional reason to farm repeatedly.

### 7.5 Propagation and authored world restoration

Some harvested crops produce viable cuttings, spores, bulbs, or saplings used at predetermined restoration sites.

```text
Find damaged habitat
→ identify the missing native species
→ establish it at the Hearthstead
→ harvest a viable cutting or propagation material
→ return to the authored site
→ reintroduce the plant
→ permanently restore the ecosystem or route
```

Possible outcomes include:

- vines creating bridges;
- roots stabilising tunnels;
- flowers producing bounce platforms;
- reeds cleansing water;
- fungal growth breaking down extraction waste;
- restored trees becoming climbable;
- wildlife returning and altering the room.

These are authored persistent changes, not procedural plant geometry.

### 7.6 Limited barter and surplus use

Surplus crops or products may be exchanged through specific character desires.

Examples:

- pigments for fabric patterns;
- fibres for furniture plans;
- herbs for ceramic planters;
- flowers for rare seeds;
- alchemical products for specialist materials.

There should not be a universal raw-crop shipping box or one mathematically dominant cash crop.

---

## 8. Farming and the Health Economy

Ordinary health recovery must remain metroidvania-first.

```text
Fight successfully
→ gain hero resource
→ spend resource on Bind
→ restore health

Reach checkpoint
→ rest and restore
```

Cultivation and shop products should not create a parallel requirement to eat food for health or stamina before normal exploration.

### Explicit boundaries

- No hunger system.
- No stamina drain for ordinary movement, attacks, farming, or exploration.
- No requirement to carry meals before entering a region.
- No routine crop-based healing that makes Bind irrelevant.
- No boss balance that assumes a stockpile of healing consumables.

Rare or highly constrained alchemical utility may exist, but it should not replace the project's core resource-and-Bind loop.

---

## 9. Cultivation Mastery

Repeated growth can deepen knowledge without requiring long experience bars.

### First successful harvest

May unlock:

- full journal identification;
- common alchemical uses;
- a basic processed material;
- shop product eligibility;
- a clear ecological trait.

### Established cultivation

May unlock:

- improved seed or cutting recovery;
- one crafting or shop recipe;
- a plant compatibility discovery;
- a resident interaction;
- a propagation use.

### Mastered cultivation

May unlock:

- an ornamental or colour variant;
- advanced grafting;
- a prestige shop product;
- a specialist ecological use;
- a final journal or resident story entry.

The required number of cycles should remain small. The player is learning a species, not grinding it indefinitely.

---

## 10. Farming Progression Stages

These stages refine the cultivation stages defined in `WorldAndProgression.md`.

### Stage 0 — Dormant

- no active Hearthseed;
- no usable beds;
- abandoned garden and apothecary space;
- corruption blocks most placement zones.

### Stage 1 — Seeded

- first Hearthseed connection;
- two basic multi-slot beds;
- starter Hearth crops;
- simple manual planting and harvesting;
- first basic processing surface;
- first visible root pulses from completed harvests;
- future shop space is visible but incomplete.

### Stage 2 — Rooted

- first regional Hearth Bloom branch;
- additional beds and placement space;
- Tier 2 regional crops;
- drying and grinding;
- first active apothecary stock;
- first resident or regular visitor;
- first authored propagation site.

### Stage 3 — Flourishing

- specialist bed types;
- improved brewing and product families;
- player-crafted shop and home customisation;
- several residents and commissions;
- visible Hearthseed branch specialisation;
- first major world changes completed through Bloom plus cultivation.

### Stage 4 — Sanctuary

- grafting and advanced propagation;
- broader specialist crop support;
- multiple shop shelves or product categories;
- resident-managed ordinary sales;
- expanded customisation zones;
- long-distance root restoration or shortcuts.

### Stage 5 — Hearthbloom

- mature Hearthseed presentation;
- Hearth-attuned crops;
- final apothecary form;
- mature resident community;
- advanced optional restoration, mastery, and decorative goals;
- no endless mandatory farm upkeep after the story is complete.

---

## 11. Crop Design Template

Every planned crop should answer the following before production:

| Field | Question |
|---|---|
| Regional identity | Where is it discovered and what ecosystem does it represent? |
| Growing condition | Which bed or visible condition does it need? |
| Ecological trait | How does it interact with the Hearthseed or world? |
| Alchemical use | Which product or process uses it repeatedly? |
| Shop demand | Is it common stock, a commission ingredient, or a prestige product? |
| Project use | Which Hearthstead or resident project needs it? |
| Crafting use | Can it become a meaningful placeable material or variant? |
| Restoration use | Can it be propagated at an authored world site? |
| Mastery reward | What does repeated successful cultivation reveal? |
| Production cost | Does its art/UI/animation burden justify its distinct role? |

A crop should normally have at least two meaningful uses. Major regional crops should aim for three or four.

---

## 12. Example Crop

### Lumen Reed — illustrative only

**Regional identity:** A pale reed surviving near extraction-polluted water.  
**Growing condition:** Shallow-water bed.  
**Traits:** Water-binding and luminous.

Possible uses:

- completes a Hearthseed water-branch cultivation requirement;
- provides oil for practical Underbrew lamps;
- supports a water-cleansing alchemical product;
- supplies a resident's waterside workspace project;
- produces fibres for woven glowing decorations;
- propagates at two polluted pools to create safe traversal platforms;
- unlocks a decorative luminous variant through mastery.

This is the intended level of integration: one plant connects exploration, cultivation, the Hearthseed, the shop, residents, customisation, and world restoration.

---

## 13. Decisions Register

| Decision | Status | Current direction |
|---|---|---|
| Core farming motivation | Decided | Cultivation strengthens Hearthseed ecology and supplies the Underbrew apothecary, projects, residents, crafting, and restoration |
| Hearthseed crop consumption | Decided against | The Hearthseed benefits from completed growth cycles and biodiversity; it does not eat deposited crops |
| Raw-crop selling | Direction set | Limited and secondary; transformed apothecary products are the primary commercial output |
| Healing food | Decided against as core loop | Checkpoints, combat resource, and Bind remain the normal health economy |
| Farming maintenance | Direction set | Low-friction growth during exploration; no crop death from absence or daily schedule |
| Cultivation progression | Direction set | Diversity, regional discovery, Hearth Blooms, small cycle goals, and authored projects |
| Repeated harvest value | Decided | Alchemy/shop stock, commissions, crafting, resident projects, propagation, and mastery |
| Crop profit optimisation | Decided against | No universal shipping economy or dominant cash crop as the main farming goal |

---

## 14. Open Questions

- Does the Hearthseed respond when a plant reaches maturity, when it is harvested, or through a combined maturity-and-harvest sequence?
- How explicitly should ecological traits be shown in the journal and farming UI?
- How many harvest cycles constitute established and mastered cultivation?
- Which crop families become ordinary repeatable shop stock?
- How much produce should crafting require without making decoration grind-heavy?
- Are seeds automatically recovered, purchased, propagated, or produced through plant-specific rules?
- Can a player leave a mature crop planted decoratively without blocking Hearthseed progression?
- How many authored world propagation sites can production support?
- Which Hearthseed branches and crop traits belong to the first three regional threads?

---

## 15. Implementation Handoff Notes

This design is not implemented by this document.

Before implementation:

1. inspect the repository for existing inventory, item, interaction, save, and ScriptableObject patterns;
2. define crop, bed, growth, harvest, mastery, and ecological-trait data contracts in a focused feature spec;
3. keep long-term state in the project's established ScriptableObject `ISaveTarget` model through `SaveManager`;
4. keep ability unlocks in `PlayerAbilityState` and tuning in the appropriate hero configuration assets;
5. define stable IDs for placed beds, crops, restoration sites, Hearthseed branches, and completed projects;
6. prototype only two beds, two crops, one Hearthseed response, one shop product, and one restoration use before expanding.

### Future Unity Editor work

A representative prototype will eventually require:

- Hearthstead placement zones and authored fixed structures;
- cultivation-bed prefabs with clear planting slots;
- crop ScriptableObject assignments and growth-stage visuals;
- Hearthseed visual-state references;
- interaction prompts and input hookups;
- save IDs and persistence registration;
- sorting layers, collision rules, and placement footprints;
- one authored world restoration site;
- one processing or brewing station;
- shop shelf or stock display setup;
- journal and UI data bindings.

Do not change validated hero movement or combat values to accommodate the farming prototype.
