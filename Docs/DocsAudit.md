# Documentation Audit — Metroidvania Controller

**Audited:** 2026-05-19  
**Auditor:** Documentation audit pass (pre-Phase 2 implementation)

---

## Executive Summary

The core architectural intent across all docs is sound and internally consistent. The main category of error is **stale status** — several systems that have been implemented are still marked as "Planned" or "Not started." The second category is **overstated enemy architecture** — `Architecture.md` describes three enemy subsystems (`EnemyMotor`, `EnemyPerception`, `EnemyBehaviour`) that do not yet exist as classes. The third is a **minor architectural deviation** — `GameManager` has grown two methods (`HitStop`, `BeginRespawnSequence`) beyond its stated "four responsibilities only" boundary and holds a direct reference to `HeroHealthComponent`.

No docs need to be deleted. No gameplay code changes are needed. The research docs are already in a separate `Research/` folder and are correctly treated as reference-only.

---

## High-Priority Inconsistencies

| # | Severity | Location | Issue |
|---|---|---|---|
| 1 | **Critical** | `Abilities.md` | Wall-Jump listed as "Planned" but `HeroWallJumpAction.cs` is fully implemented and wired into `HeroActionController` |
| 2 | **Critical** | `ImplementationPlan.md` | Status table shows "Enemy AI framework — Not started" but 11+ enemy scripts exist |
| 3 | **High** | `Architecture.md` | Runtime stack tree omits `HeroWallJumpAction` |
| 4 | **High** | `Architecture.md` | Enemy architecture section lists `EnemyMotor`, `EnemyPerception`, `EnemyBehaviour` as peer components — none of these classes exist yet |
| 5 | **High** | `ImplementationPlan.md` | Milestone 3 wall-jump task (`HeroWallJumpAction`) should be marked done |
| 6 | **High** | `ImplementationPlan.md` | Milestone 0 duplicate checklist rows (same tasks appear unchecked then re-stated below) |
| 7 | **Medium** | `Architecture.md` | GameManager is described with "four responsibilities only" but has two additional implemented methods (`HitStop`, `BeginRespawnSequence`) and holds a `_heroHealth` reference — architectural deviation, not flagged anywhere |
| 8 | **Medium** | `ImplementationPlan.md` | "Immediate — No Code Required" tasks for writing HUD.md and Audio.md are not marked done, but both files exist |
| 9 | **Low** | `ImplementationPlan.md` | Date at top says 2026-05-13 (stale) |

---

## Per-File Findings

### `Architecture.md`

**Accurate:**
- HeroController coordinator pattern and subsystem split
- HeroStateBlackboard as shared state bus
- HeroMotor owns all Rigidbody2D writes
- HeroHealthComponent structure and event flow
- HeroAttackHit struct and hit-receiver interfaces
- Combat object model (HeroAttackModule)
- Sensor system description
- Control lock reference-counting
- Interactable system
- Checkpoint/respawn marker flow
- Scene transition flow (TransitionPoint → GameManager)
- Boot/startup sequence
- Save/persistence architecture
- Camera, HUD, Audio sections (all correct cross-references)

**Inaccurate / Stale:**
- **Runtime stack tree:** Missing `HeroWallJumpAction` — it is instantiated in `HeroActionController.Initialize()` and ticked in `FixedTick()`.
- **Enemy architecture diagram:** Lists `EnemyMotor`, `EnemyPerception`, and `EnemyBehaviour` as siblings in the hierarchy. None of these classes exist. What exists is `IEnemyBehaviour` (interface) and `MushroomEnemy` (concrete implementation). `EnemyController.cs` has an explicit comment: *"EnemyMotor, EnemyPerception, and EnemyBehaviour will be wired here in Milestone 2."*
- **GameManager "four responsibilities only":** `GameManager.cs` also implements `HitStop(float)` and `BeginRespawnSequence()`, and caches `_hero` and `_heroHealth` as fields. `BeginRespawnSequence` calls `_heroHealth.RestoreFullHealth()`, which is a direct hero-health coupling not acknowledged in the architecture rules. This is a **tech debt item** that should be flagged in `ImplementationPlan.md`.

**No change needed:**
- Planned Systems table at the bottom is correct in mapping systems to milestone numbers.

---

### `ImplementationPlan.md`

**Accurate:**
- Camera system: marked Done — correct
- Hero movement: marked Done — correct
- Directional melee combat: marked Done — correct
- HeroHealthComponent: marked Partial — correct
- Scene transitions: marked Partial — correct
- Audio system: marked Partial — correct
- Save / load system: marked Not started — correct
- UI (HUD, menus): marked Not started — correct
- Ability unlock system: marked Not started — correct (PlayerAbilityState SO not yet created)

**Inaccurate / Stale:**
- **Status table:** "Enemy AI framework — Not started" is wrong. Multiple enemy classes exist: `EnemyController`, `EnemyStateBlackboard`, `EnemyConfig`, `EnemyHealthComponent`, `EnemyRecoil`, `EnemyRecoilState`, `EnemyContactDamage`, `DamageHero`, `IEnemyBehaviour`, `MushroomEnemy`, `EnemyFeedbackController`, `EnemyDeathType`. Status should be **Partial**.
- **"Immediate — No Code Required":** Two tasks (write HUD.md, write Audio.md) are not marked done. Both files exist and are well-written.
- **Milestone 0 duplicate rows:** Six tasks are listed twice — once as the original spec and once as status-annotated updates below. This is confusing. The duplicates should be collapsed into a single clean list.
- **Milestone 3:** "Implement wall-jump (`HeroWallJumpAction`)" — the script exists, is instantiated in `HeroActionController`, and is ticked in `FixedUpdate`. Should be marked `[x]`.
- **Date:** Top says 2026-05-13, now 2026-05-19.

**Tech debt gap:**
- GameManager's `HitStop` method and hero health coupling are not mentioned in Known Technical Debt or Current Integration Risks. Should be added.

---

### `Abilities.md`

**Accurate:**
- Dash: "Implemented (always available)" — correct
- Wall-Slide: "Implemented (always available)" — correct
- Sprint: "Planned" — correct, no `HeroSprintAction.cs` exists
- Wall Latch: "Planned" — correct, no `HeroWallLatchAction.cs` exists
- Spirit Cast: "Planned" — correct
- Intended architecture section (gate pattern, PlayerAbilityState, etc.) — all correct

**Inaccurate:**
- **Wall-Jump: "Planned"** — WRONG. `HeroWallJumpAction.cs` is fully implemented. It is instantiated in `HeroActionController.Initialize()` and ticked in `FixedTick()`. Features: relatch lockout timer, `CanStartWallJump` check (wallSliding, not controlLocked, not dashing, not attackRecovering, HasBufferedJump), `StartWallJump()` calling `motor.StartWallJump(wallDirection)` and `audio.PlayWallJump()`.
- **Dependencies block:** `PlayerAbilityState (TODO)` — still correct, the SO does not exist yet. Abilities.md does not need to say wall-jump is gated, only that the system is implemented.

---

### `EnemyAI.md`

**Accurate:** This is the best-maintained feature spec. The "Current State" section accurately describes what is implemented. It correctly distinguishes implemented behaviour from planned states.

**Minor gaps:**
- State machine table lists all states (Idle, Patrol, Chase, Attack, Hurt, Dead) without a "Planned" label on Chase and Attack — readers might think all states are implemented. A note would help.
- `EnemyController.cs` shows `IEnemyBehaviour` is initialized via interface scan (not a named field) — the implemented architecture diagram in EnemyAI.md shows `IEnemyBehaviour` as a direct field, which is slightly inaccurate but acceptable for a spec doc.

---

### `Camera.md`

**Accurate:** Thorough and matches the codebase. All classes described exist.

**Minor:**
- The `HeroCameraAnimancerBridge` is listed as a script in the project (`Assets/_Project/Scripts/Hero/Animation/HeroCameraAnimancerBridge.cs`). The doc correctly marks it as optional.
- TODOs at the bottom are still valid deferred items.

---

### `CameraImplementationChecklist.md`

**Status:** All items are checked [x]. This document is **complete**. It serves as a historical record of the camera implementation pass.

**Recommendation:** No changes needed. It can be treated as reference-only — it does not need a "Last audited" date since it is a completed checklist, not a living spec.

---

### `PlayerController.md`

**Accurate:** Matches actual implementation. `HeroBox`, `HeroHealthComponent` boundary, blackboard fields, movement rules, control lock, extension points — all accurate.

**Minor:**
- `inputBlocked` field has "(TODO: cutscene/menu system)" — still correct; no cutscene system exists.

---

### `Combat.md`

**Accurate:** Matches implementation. The Stage 2 VFX section correctly identifies the VFX prefabs under `Assets/_Project/Prefabs/VFX/Combat/` (HitSpark, PogoSpark, EnemyDeathBurst, TerrainImpact — all exist). The Stage 3 slash arc section correctly describes the `SlashArcVisual` child pattern.

**No changes needed.**

---

### `Audio.md`

**Accurate:** Call sites table is accurate. `HeroWallJumpAction` entry references `HeroAudioController.PlayWallJump()` — now correct since wall jump is implemented.

**No changes needed.**

---

### `HUD.md`

**Accurate:** "Current State: No HUD exists" is correct. No HUD scripts exist in the codebase.

**No changes needed.**

---

### `SaveSystem.md`

**Accurate:** "Current State: No save system exists" is correct. No `SaveManager.cs` exists.

**No changes needed.**

---

### Research Docs (`Docs/Research/`)

Seven research/reference documents exist:
- `ReferenceReviewHandoff.md`
- `Pogo_DownslashBounce_Reference.md`
- `AttackFeelResearch.md`
- `Combat Impact Research.md`
- `Enemy Hit VFX Responsibility and Hollow Knight Style Slash VFX in Unity.md`
- `Hollow Knight Slash VFX Integration for Metroidvania-Controller.md`
- `Audio Architecture Deep Research.md`

These are all correctly isolated in `Docs/Research/` and are not referenced as implementation truth. Leave as-is.

---

## Recommended Edits

| Doc | Change | Priority |
|---|---|---|
| `Architecture.md` | Add `HeroWallJumpAction` to the runtime stack tree | High |
| `Architecture.md` | Clarify enemy architecture — mark `EnemyMotor`/`EnemyPerception`/`EnemyBehaviour` as planned; add `IEnemyBehaviour` / `MushroomEnemy` as what's implemented | High |
| `Architecture.md` | Note `HitStop` and `BeginRespawnSequence` as additional implemented GameManager methods; flag hero health coupling as known tech debt | Medium |
| `Abilities.md` | Change Wall-Jump status from "Planned" to "Implemented" | High |
| `ImplementationPlan.md` | Update date to 2026-05-19 | Low |
| `ImplementationPlan.md` | Change Enemy AI framework status from "Not started" to "Partial" | High |
| `ImplementationPlan.md` | Mark Immediate doc tasks (HUD.md, Audio.md) as done | Medium |
| `ImplementationPlan.md` | Consolidate duplicate Milestone 0 checklist rows | Medium |
| `ImplementationPlan.md` | Mark Milestone 3 wall-jump task as done | High |
| `ImplementationPlan.md` | Add GameManager HitStop + hero health coupling to Known Technical Debt | Medium |
| All active specs | Add "Last audited: 2026-05-19" header line | Low |

---

## Proposed Source-of-Truth Hierarchy

```
Docs/Architecture.md           ← authoritative: system boundaries, class responsibilities, rules
Docs/ImplementationPlan.md     ← authoritative: what is done, what is next, known debt
Docs/FeatureSpecs/[System].md  ← authoritative: detailed design and rules per system
Docs/FeatureSpecs/CameraImplementationChecklist.md  ← historical/reference (completed)
Docs/Research/                 ← reference only; not implementation truth
```

When a spec and Architecture.md conflict, Architecture.md wins for boundary/coupling rules. When a spec and ImplementationPlan.md conflict on status, trust the code.

---

## Safe Documentation Change Checklist

- [x] Write DocsAudit.md (this file)
- [ ] `Architecture.md` — add HeroWallJumpAction to runtime stack tree
- [ ] `Architecture.md` — fix enemy architecture section (implemented vs planned)
- [ ] `Architecture.md` — note GameManager additional methods and tech debt
- [ ] `Abilities.md` — update Wall-Jump status to Implemented
- [ ] `ImplementationPlan.md` — update date
- [ ] `ImplementationPlan.md` — fix Enemy AI status to Partial
- [ ] `ImplementationPlan.md` — mark Immediate doc tasks done
- [ ] `ImplementationPlan.md` — consolidate Milestone 0 duplicates
- [ ] `ImplementationPlan.md` — mark Milestone 3 wall-jump done
- [ ] `ImplementationPlan.md` — add GameManager tech debt entries
- [ ] Add "Last audited" to all actively maintained docs

---

## Code / Project State That Could Not Be Verified

The following could not be confirmed without opening Unity:

- Whether `HeroController.ResolveDependencies` still uses the `AssetDatabase` fallback (flagged as tech debt in ImplementationPlan.md, but the HeroController.cs file was not read during this audit).
- Whether `PlayerAbilityState.asset` at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` is wired into the HeroController Inspector field (asset exists; Inspector wiring must be verified in-editor).
- Inspector wiring in Boot scene, Hero prefab, and Enemy prefab (correct prefab structures exist but cannot be verified without in-editor inspection).
- Whether `HeroAnimationLibrary.wallJump` clip slot has been filled.

---

## Next Recommended Implementation Prompt

**After doc cleanup:** The project is at the boundary of Milestone 2 (Enemy Loop). The hero, camera, and basic enemy framework (Mushroom patrol) are in place. The natural next prompt is:

> Implement the complete Milestone 2 enemy loop: add InteractableBase and CheckpointInteractable, wire SaveManager (stub implementation sufficient), connect HazardRespawnMarker to GameManager, implement hero hurt response in HeroController (subscribe to OnDamaged → Hurt state + knockback + control lock), and validate the full loop: fight → die → respawn → fight → reach checkpoint.

This requires: `InteractableBase.cs`, `CheckpointInteractable.cs`, `SaveManager.cs` (stub), `HazardRespawnMarker.cs`, and hero hurt wiring in `HeroController.cs`. No combat changes needed — the combat system is complete.
