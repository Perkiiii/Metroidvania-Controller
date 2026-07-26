# Underbrew — Shop and Alchemy Design

**Status:** Working system design  
**Version:** 0.1  
**Created:** 2026-07-26  
**Parent document:** `Docs/GameDesign/UnderbrewGameDesign.md`

> This document defines the player-facing purpose, operation, progression, and economy of the Underbrew apothecary and its alchemy systems. It is not an implementation-status document.
>
> - `Docs/Architecture.md` remains authoritative for technical ownership and system boundaries.
> - `Docs/ImplementationPlan.md` remains authoritative for what currently exists and what should be implemented next.
> - `Docs/GameDesign/WorldAndProgression.md` owns the wider reward and regional progression structure.
> - `Docs/GameDesign/HomeAndFarming.md` owns cultivation, Hearthseed symbiosis, crop progression, and harvested-material use.

---

## 1. Core Direction

The protagonist is a displaced red panda herbalist and developing alchemist who establishes an apothecary at the restored Hearthstead.

The working title connection is:

> **Underbrew is the name of the apothecary created around the lower roots of the Hearthstead, where cultivated plants and discoveries from the wider world are transformed into remedies, practical goods, restoration compounds, and specialist commissions.**

This is a **direction-set** fiction decision rather than a final naming lock. The exact shop architecture, logo, founder story, and moment at which the protagonist chooses the name remain open.

The shop should connect the game's major systems:

```text
Explore
→ discover ingredients, people, problems, and knowledge
→ cultivate renewable plants at the Hearthstead
→ process and brew useful products
→ stock the apothecary or fulfil specific commissions
→ earn money, trust, plans, relationships, and new objectives
→ improve the shop, Hearthstead, and community
→ gain reasons and means to explore farther
```

---

## 2. Design Goals

The Underbrew apothecary should:

- make the protagonist's herbalist and alchemist identity central to play;
- give repeated cultivation a clear, renewable use;
- provide a grounded source of money without relying on raw-crop shipping;
- create relationships and story through the people who need its products;
- attract displaced residents and travellers to the Hearthstead;
- visibly grow from a tiny improvised counter into a recognised sanctuary service;
- remain low-friction enough that it does not interrupt metroidvania exploration;
- support player expression through product choice, furniture, displays, and decoration;
- make the title `Underbrew` something the player actively helps create.

It should not become:

- a daily retail shift simulator;
- a strict opening-hours system;
- a queue-management minigame;
- an endless series of random timed orders;
- the place where traversal abilities or critical story access are simply purchased;
- a survival economy that requires food, stamina items, or healing stock before exploration;
- a single-product profit optimisation puzzle.

---

## 3. Role of the Shop in the Story

### 3.1 From refuge to useful place

The Hearthseed initially makes the abandoned Hearthstead safe enough for the protagonist to remain there.

The apothecary gives other people a practical reason to visit and eventually stay.

```text
Hearthseed
→ makes the land safe and alive

Underbrew apothecary
→ makes the place helpful, known, and socially connected

Together
→ transform an abandoned refuge into a community
```

News spreads that the Hearthstead offers:

- remedies for extraction-related illness or injury;
- plant knowledge unavailable in avian-controlled settlements;
- practical goods made from restored native species;
- help cultivating damaged land;
- a safe place for displaced travellers;
- someone willing to investigate unusual regional problems.

### 3.2 Found-family progression

Residents should not appear only because the Hearthseed magically summons them.

A stronger chain is:

```text
The Hearthseed becomes visible in the world
→ travellers discover that the land is recovering
→ the apothecary helps them with a real need
→ trust develops
→ some characters return, contribute, or settle permanently
```

The shop therefore becomes one of the main engines of the found-family story.

### 3.3 Relationship to the avian civilisation

Underbrew demonstrates an alternative to avian extraction.

The avian system:

- standardises resources;
- extracts at industrial scale;
- values output over ecosystem health;
- replaces local knowledge with controlled production;
- abandons depleted places.

The apothecary:

- studies regional plants;
- cultivates renewable sources;
- adapts products to individual and ecological needs;
- shares value with the Hearthstead and its residents;
- becomes stronger because the surrounding world recovers.

The shop is commercial, but it is not extractive. Money is one outcome of useful work, not the sole measure of value.

---

## 4. Product Families

The shop should use a manageable number of memorable product families rather than hundreds of nearly identical recipes.

### 4.1 Everyday remedies

Repeatable products that create steady demand for common cultivated ingredients.

Possible examples:

- soothing salves;
- antidotes;
- breathing remedies for extraction dust or spores;
- treatment for damaged bark or skin;
- restorative teas used by residents rather than as the hero's combat-healing system;
- animal or creature remedies;
- simple disinfecting and cleansing mixtures.

These products support the fiction of an apothecary without replacing Bind or checkpoints.

### 4.2 Practical alchemy

Products with household, workshop, cultivation, or decorative uses.

Possible examples:

- lamp oils;
- pigments and dyes;
- adhesives and resins;
- wood or bark treatments;
- preserving solutions;
- soaps and plant-safe cleaning mixtures;
- incense and fragrances;
- polish, wax, and waterproofing compounds.

These products can generate ordinary shop income and support Hearthstead customisation.

### 4.3 Cultivation products

Products that improve the farm or help others establish healthy cultivation.

Possible examples:

- rooting mixtures;
- grafting compounds;
- fungal cultures;
- specialist-bed substrates;
- propagation tonics;
- cleansing agents for damaged soil;
- water-balancing mixtures;
- pollinator attractants.

Cultivation products should unlock authored capabilities or convenience rather than hidden percentage optimisation.

### 4.4 Restoration compounds

Higher-value authored products tied to world and regional problems.

Possible examples:

- a compound that neutralises avian runoff;
- a paste that heals tapped tree bark;
- a catalyst that awakens a dormant root node;
- treatment that allows a native species to survive in damaged soil;
- a solvent that safely removes extraction residue;
- a mixture used to restore a living bridge or water system.

These are usually commissions, quests, or restoration projects rather than endlessly sold shelf stock.

### 4.5 Prestige and cultural products

Optional products connected to resident stories, mastery, decoration, and completion.

Possible examples:

- fragrances associated with a resident's lost home;
- ceremonial inks or dyes;
- rare incense;
- luminous botanical displays;
- mastered crop blends;
- gifts or commemorative products after major story milestones.

These create emotional and cosmetic value beyond currency.

---

## 5. Shop Demand Structure

The shop should use three complementary forms of demand.

### 5.1 Everyday stock

The player chooses a small number of known products to place on shelves or make available for ordinary customers.

Everyday stock should:

- use renewable ingredients;
- sell gradually while the player explores;
- create a modest, dependable reason to cultivate common crops;
- have readable demand rather than volatile market simulation;
- avoid requiring the player to manually serve every customer.

Examples:

- common salves;
- lamp oil;
- simple dyes;
- incense;
- basic cultivation compounds;
- practical cleaning or preserving products.

### 5.2 Personal commissions

Non-expiring requests tied to named characters.

A commission should reveal character, place, or need.

Examples:

- medicine for an injured traveller;
- a familiar fragrance from a displaced resident's homeland;
- pigment needed by a craftsperson;
- a remedy requested by a bird who has defected from the extraction system;
- treatment for a local creature;
- a product required to establish a resident's workshop.

Commissions should reward more than money where appropriate:

- trust;
- recipes;
- furniture plans;
- resident recruitment;
- story information;
- maps or regional leads;
- a new shop service;
- a visible community change.

### 5.3 Regional orders and restoration work

Larger authored needs connected to a settlement, ecosystem, or regional objective.

Examples:

- produce bark treatment for a damaged grove;
- develop an antidote for polluted water;
- supply rooting compound for a restoration site;
- create cultivation materials for displaced residents;
- adapt an avian process into a non-destructive alternative.

These orders connect the apothecary directly to the metroidvania world without turning every route into a repeated consumable check.

---

## 6. Shop Operation

### 6.1 Low-friction rule

The player should not need to stand behind the counter during routine operation.

A recommended flow is:

```text
Choose products to stock
→ place or assign them to a small number of shelf slots
→ leave on an expedition
→ ordinary sales occur while away
→ return to a concise sales and request summary
→ collect money and see customer or resident responses
```

Important story customers may be served directly through authored dialogue, but this should be the exception rather than the daily routine.

### 6.2 No punitive schedule

The shop should not require:

- fixed opening hours;
- daily attendance;
- timed order failure;
- customer anger because the player explored for too long;
- perishable stock loss as a normal punishment;
- repeated cleaning or maintenance chores without strategic value.

### 6.3 Shopkeeper progression

An early or midgame resident may eventually help manage ordinary sales.

This creates a meaningful progression beat:

```text
Protagonist runs an improvised counter
→ first regular visitors appear
→ trusted resident joins
→ resident manages routine sales
→ protagonist focuses on recipes, stock choice, commissions, and exploration
```

The player remains the alchemist and owner. Delegation removes friction rather than removing authorship.

---

## 7. Currency and Economy

### 7.1 Money has a valid role

The apothecary provides a grounded source of ordinary currency.

Money may support:

- shop furniture and display upgrades;
- decorative objects and colour variants;
- cultivation beds, planters, and storage;
- crafting plans;
- selected tool construction costs;
- resident services;
- convenience improvements;
- repairs and Hearthstead presentation;
- specialist ingredients that are not critical-path random gates.

### 7.2 Money must not replace exploration

The following should normally come from authored world progress rather than purchase:

- major traversal abilities;
- Hearth Blooms;
- core story access;
- regional boss resolutions;
- the primary map structure;
- major Hearthseed stages;
- essential combat verbs.

Money improves and personalises the player's growing home. It should not let the player buy past the metroidvania.

### 7.3 Avoid universal crop pricing

Raw crops should not be defined mainly by a universal sale value.

The game should avoid:

- a shipping bin that converts every plant directly into money;
- one fast crop becoming the dominant strategy;
- repeated market-price fluctuation;
- requiring large profit totals for story progression;
- making rare ecological plants valuable only because they sell for more.

The more meaningful value chain is:

```text
Cultivated plant
→ processed ingredient
→ deliberate alchemical product
→ relevant customer, commission, project, or shop category
```

### 7.4 Limited raw-material exchange

Specific residents or travellers may barter for raw or processed plants when it makes sense.

This is a secondary surplus outlet, not the main economy.

---

## 8. Trust, Reputation, and Community Growth

Money measures commercial value. Trust or reputation measures what Underbrew means to other people.

The exact name of this progression track remains open. Possible framings include:

- trust;
- renown;
- community standing;
- apothecary reputation;
- Hearthstead welcome;
- word of mouth represented through resident milestones rather than a visible bar.

### 8.1 Sources of trust

Trust may grow through:

- completing personal commissions;
- helping a regional community;
- discovering safe uses for native plants;
- restoring damaged habitats;
- helping birds who oppose the extraction system;
- providing products during resident story arcs;
- maintaining a varied and useful shop;
- completing major Hearthseed transformations.

### 8.2 Trust rewards

Trust may unlock:

- new regular visitors;
- resident candidates;
- specialist recipes;
- rare seeds or cuttings;
- commissions;
- furniture and display plans;
- information about regional problems;
- community scenes and celebrations;
- a shopkeeper or additional service;
- story access based on relationships rather than payment.

### 8.3 Avoid reputation grinding

Trust should be driven primarily by authored milestones and meaningful variety.

Selling the same common salve one hundred times should not replace helping a named character or restoring a region.

Routine shop stock may contribute small background progress, but major trust changes should come from what the player did and who they helped.

---

## 9. Alchemy Design

### 9.1 Alchemy is transformation

Alchemy should make cultivated and discovered materials more meaningful by transforming them into purposeful products.

The player should understand:

- what they are making;
- who or what it helps;
- which ingredients define its function;
- how the recipe was learned;
- whether it is ordinary stock, a commission, crafting material, or restoration tool.

### 9.2 Recipe acquisition

Recipes may be learned through:

- the protagonist's starting herbal knowledge;
- resident teaching;
- experimentation with guidance;
- studying regional plants;
- journals or abandoned workspaces;
- personal commissions;
- avian processes adapted into safer alternatives;
- boss or restoration materials;
- cultivation mastery.

Important recipes should not depend on blind random combinations.

### 9.3 Processing depth

A practical first-release alchemy chain should be compact.

Possible actions include:

- drying;
- grinding;
- pressing;
- steeping;
- distilling;
- fermenting;
- combining with mineral or creature components;
- bottling, wrapping, or preparing for display.

Not every product should require every step. The player should make a planning decision, not perform a long sequence of identical animations.

### 9.4 Recipe readability

The interface should show:

- known ingredients;
- required quantities;
- missing sources;
- product category;
- whether the result is stockable, commissioned, placeable, or world-facing;
- known customers or uses;
- mastery or quality requirements only when clearly justified.

---

## 10. Relationship to Hero Health and Combat

The apothecary must not undermine the established metroidvania health economy.

The normal loop remains:

```text
Fight successfully
→ gain hero resource
→ spend resource on Bind
→ recover health

Reach checkpoint
→ rest and restore
```

### Explicit design boundaries

- No hunger system.
- No stamina food required for exploration.
- No assumption that the player brings meals into every region.
- No routine healing-pot economy that makes Bind optional or inferior.
- No boss balance based on stockpiled healing items.
- No farming chores required before ordinary combat attempts.

The protagonist may sell remedies to other people because their needs are not identical to the hero's gameplay health model.

A rare alchemical combat utility may exist if it creates a distinct tactical choice, but it should be limited, clearly bounded, and tested against the existing resource system.

---

## 11. Shop Progression Stages

### Stage 0 — Abandoned brewing space

- old counter or underground workspace visible;
- no active customers;
- damaged shelves and equipment;
- title `Underbrew` not yet established in fiction;
- only basic personal processing is possible.

### Stage 1 — Improvised apothecary

- one basic work surface;
- one or two stock slots;
- starter remedies or practical products;
- first visitor or commission;
- handwritten sign or provisional counter;
- ordinary sales remain very small.

### Stage 2 — Recognised local shop

- drying and grinding stations;
- additional shelf space;
- several repeatable stock products;
- first regular visitors;
- product journal or order board;
- money begins supporting customisation and practical upgrades;
- the name `Underbrew` may be chosen or established here.

### Stage 3 — Community apothecary

- improved brewing and processing;
- multiple product families;
- resident workspace or shop assistance;
- personal and regional commissions;
- player-controlled furniture, display, and colour choices;
- shop reputation visibly attracts people to the Hearthstead.

### Stage 4 — Sanctuary service

- routine sales delegated to a trusted resident;
- advanced cultivation and restoration compounds;
- specialist visitors and regional orders;
- adapted avian equipment may support safe processing;
- the shop connects several resident professions;
- broader customisation and prestige products.

### Stage 5 — Underbrew in full form

- mature apothecary identity;
- all major product categories represented;
- final resident and community scenes;
- high-level optional commissions and mastery products;
- strong visual connection to the mature Hearthseed;
- post-game operation remains optional and non-punitive.

---

## 12. Opening-Chapter Relationship

The opening chapter should establish the existence and potential of the apothecary without teaching the full shop system immediately after the combat tutorial.

Recommended opening sequence:

```text
Plant Hearthseed
→ first land and shelter restoration
→ discover or uncover the abandoned brewing room / counter
→ use one basic processing surface or inspect the old equipment
→ establish that this space could become something useful
→ receive two exploration directions
```

The first full shop transaction may occur:

- at the very end of the opening through one simple visitor need; or
- early in the first regional phase after the player discovers a second ingredient or first resident.

The preferable implementation order is to avoid overloading the first hour. The exact onboarding point remains open for playtesting.

---

## 13. Product Design Template

Every proposed shop product should answer:

| Field | Question |
|---|---|
| Product family | Remedy, practical good, cultivation product, restoration compound, or prestige item? |
| Ingredients | Which cultivated and discovered materials define it? |
| Repeatability | Is it ordinary stock, a recurring commission type, or one-time authored content? |
| Customer | Who wants or needs it? |
| Player reward | Money, trust, plan, recipe, resident progress, or world change? |
| System connection | Does it support farming, crafting, residents, restoration, or story? |
| Health-loop risk | Could it undermine Bind, checkpoints, or combat balance? |
| Presentation cost | Does it need unique bottle art, animation, VFX, UI, or dialogue? |
| Dominance risk | Could mass-producing it invalidate product variety? |

A product should not exist only to increase the recipe count.

---

## 14. Example Product Chain

### Lumen Reed Oil — illustrative only

```text
Discover Lumen Reed near polluted water
→ establish it in a shallow-water bed
→ harvest reeds while their growth strengthens the Hearthseed's water branch
→ press and refine their luminous oil
```

Possible outputs:

- **Everyday stock:** practical lamp oil sold to travellers and residents;
- **Crafting:** ingredient for glowing Hearthstead lights;
- **Commission:** requested for a displaced resident's waterside workspace;
- **Restoration:** combined into a water-cleansing compound;
- **Trust:** demonstrates a safe renewable alternative to an avian extraction product.

This is the intended integration level: one regional plant supports farming, Hearthseed growth, alchemy, shop economy, customisation, residents, and restoration.

---

## 15. Decisions Register

| Decision | Status | Current direction |
|---|---|---|
| Protagonist profession | Decided | Herbalist and developing alchemist |
| Shop role | Decided | Central home system connecting crops, alchemy, economy, residents, and story |
| `Underbrew` title meaning | Direction set | Name of the apothecary built around the lower Hearthstead roots |
| Raw-crop selling | Direction set | Secondary and limited; the main commercial output is transformed products |
| Money | Decided to include | Supports customisation, shop/home upgrades, plans, services, and convenience—not core ability or story purchase |
| Shop operation | Direction set | Low-friction stock assignment and sales while exploring |
| Mandatory retail shifts | Decided against | Important customers may be served directly; routine sales are asynchronous |
| Shop reputation | Direction set | Meaningful commissions and restoration attract residents, recipes, and stories |
| Healing consumables | Decided against as core economy | Bind, combat resource, and checkpoints remain the normal health loop |
| Timed orders | Decided against by default | Commissions are non-expiring unless a rare authored sequence strongly justifies timing |
| Dominant cash crop/product | Decided against | Demand and rewards should encourage useful variety |

---

## 16. Open Questions

- At what exact point does the protagonist choose or inherit the name `Underbrew`?
- Is the shop physically underground, built into a root cellar, or simply positioned beneath the Hearthseed canopy?
- Does the player manually place stock on shelves or assign products through a concise shop interface?
- How many active stock slots create meaningful choice without micromanagement?
- Is ordinary demand fixed, region-responsive, resident-responsive, or a simple blend?
- Does money use one universal currency, local tokens, barter, or a hybrid?
- Should trust be a visible meter or communicated through visitors and authored milestones?
- Who becomes the first shopkeeper or assistant?
- Is the first transaction part of the opening chapter or the first early-region return?
- Which product families belong to the smallest integrated vertical slice?
- Can bird-controlled settlements become customers or commission sources later in the story?
- How much visual customisation should the shop counter, shelves, signs, and product displays support?

---

## 17. Smallest Representative Prototype

Do not begin with a full shop catalogue.

The smallest integrated prototype should contain:

- two cultivable crop species;
- one basic processing action;
- two alchemical products;
- one repeatable everyday stock product;
- one named, non-expiring commission;
- two shop shelf or stock slots;
- sales resolving while the player completes an expedition;
- one currency reward;
- one trust or resident consequence;
- one purchasable or craftable Hearthstead customisation;
- no healing-food dependency;
- save/load coverage for stock, sales, commission state, money, and shop progression.

This prototype should be evaluated for:

- whether farming has a satisfying repeated purpose;
- whether shop setup is quick enough;
- whether returning home feels rewarding;
- whether money has useful but non-dominating value;
- whether commissions create attachment to residents;
- whether the shop distracts from exploration;
- whether the system creates pressure to mass-produce one product.

---

## 18. Implementation Handoff Notes

This design is not implemented by this document.

Before implementation:

1. inspect existing inventory, item, save, interaction, journal, UI, and ScriptableObject patterns;
2. define product, recipe, stock-slot, commission, customer, currency, trust, and shop-stage contracts in a focused feature spec;
3. keep persistent progression through `SaveManager` and ScriptableObject `ISaveTarget` state rather than direct scene references;
4. keep the shop decoupled from hero internals and do not alter the hero health/resource architecture to accommodate products;
5. define stable IDs for commissions, stock slots, shop upgrades, transactions, and resident milestones;
6. establish deterministic offline/away sales rules based on game progress rather than real-world elapsed-time exploitation;
7. implement the smallest representative prototype before expanding recipes or customers.

### Future Unity Editor work

A representative prototype will eventually require:

- an authored Hearthstead brewing room or shop area;
- shop counter, shelf, stock-display, and processing-station prefabs;
- ScriptableObject assignments for products, recipes, commissions, and shop stages;
- inventory and storage hookups;
- UI for recipes, stock choice, sales summary, and commissions;
- interaction prompts and input actions;
- save IDs and persistence registration;
- resident/customer spawn and dialogue hookups;
- placeable furniture footprints and valid placement zones;
- item icons, product presentation, audio, animation, and feedback;
- testing scenes for shop flow without requiring full world traversal.

Do not claim shop, farming, economy, or Editor validation until those systems are actually implemented and tested.
