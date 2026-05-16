# Reference Review Handoff — Metroidvania Controller

Date: 2026-05-15

This document captures the outcome of a reference-code review and transfers the useful findings into the Metroidvania-Controller project.

## Summary
- Goal: Map Silksong-derived reference systems to the project's architecture and produce a Phase‑1 plan that preserves the project's rules (single-authority `HeroMotor`, `HeroStateBlackboard`, Animancer, ScriptableObjects, interface-based combat receivers, GameManager owning time scale, no PlayMaker or tk2d).
- Outcome: Read core reference systems (damage, camera, freeze/hit-stop, save, audio, hurtboxes) and extracted patterns we should adopt (attacker-side buffered damage, HitInstance DTO, FreezeMoment facade, CameraShakeManager behavior, save/restore abstraction). A handoff plan and prioritized implementation steps are below.

## Architecture Constraints (must preserve)
1. `HeroMotor` is the only writer of Rigidbody2D velocity for the hero.
2. `HeroStateBlackboard` is the shared bus for actor states; controllers and sensors coordinate through it.
3. Animations via Animancer; no tk2d/PlayMaker for new code.
4. ScriptableObjects for tunables and config where appropriate.
5. Combat receivers implement interfaces (e.g. `IHeroAttackReceiver`).
6. `GameManager` owns `Time.timeScale` (hitstop/pause). Use `GameManager` helpers for hit stop.
7. Scene transitions go through `GameManager.BeginSceneTransition`.

## Key Reference Patterns to Adopt
- Attacker-side buffered ordered damage (DamageEnemies): per-frame collection of collided targets, ordering/aggregation of hits, and a damage buffer that is swapped and processed in LateFixedUpdate to keep deterministic ordering.
- Target-side `IHitResponder` (HealthManager): central target contract to receive `HitInstance`, check invincibility/blocked-by-direction, apply scaling, recoil, and death plumbing.
- `HitInstance` DTO: carries damage amount, source, force direction, flags (parry, crit), and hit metadata — useful to keep decoupling between attacker and receiver.
- FreezeMoment facade (GameManager): central presets for hit-stop / time-ramp behavior invoked by gameplay code and by CameraShakeManager for frame freezes.
- CameraShakeManager: accumulation of multiple shakes, per-shake weight/priority, and optional freeze-frame support. Good to reproduce as a lightweight manager and expose a simple API for polish systems.
- SaveRestoreHandler: abstract save backend, JSON <-> bytes conversion, optional encryption, and restore point trimming. Keep `SaveRestoreHandler` principles but not necessarily the exact implementation until SaveManager milestone.

## Notable Files Read (high-level takeaways)
- `CameraController.cs` — separate X/Y damping, look offsets tied to hero state, LockToArea/Release lock logic. Keep camera decoupled from hero internals and accept intent signals (look up/down, start-locked, enter/exit areas).
- `CameraShakeManager.cs` — multi-shake accumulation, freeze frames via coroutine. Implement a simpler variant first and keep API to accept presets and intensities.
- `GameManager.cs` — `FreezeMoment` coroutine mapping enum presets to ramp-down/wait/ramp-up sequences. Central time-control is owned here.
- `DamageEnemies.cs` — attacker-side buffer, `enteredColliders` per-frame queue, `processingDamageBuffer` swapping, `IHitResponder.Hit` invocation. Use an adapted pattern for clean ordering and multi-hit pacing.
- `HealthManager.cs` — target-side checks for invincibility, directional blocking, damage scaling, recoil, and death flow. Implements `IHitResponder.Hit` and demonstrates how to keep hit logic local to the damage receiver.
- `HeroBox.cs` — player hurtbox, hit buffering and LateFixedUpdate application, parry hooks. Useful for implementing buffered hurt responses and parries later.
- `Recoil.cs` — sweep-based recoil and optional freeze behavior. Useful inspiration for knockback behavior that respects physics sweeps and ground collision.
- `SaveRestoreHandler.cs` — versioned save names, optional encryption; shows a robust save/trimming design.

## Reference Files Inspected — Detailed Notes

For each inspected file/class below:
- Responsibility
- Important behaviours
- Important timers / buffers / state flags
- Comparable system in the target project
- What to learn from it
- What not to copy directly

- `HeroController.cs`
   - Responsibility: central coordinator for hero input, actions, sensors, motor and animation glue in the reference.
   - Important behaviours: state transitions, input-to-action mapping, animation event routing, subscribes to health and respawn events.
   - Timers/buffers/flags: input buffer windows, attack/cast timers, hurt/stun windows, state booleans used by many subsystems.
   - Comparable in target: `HeroController` coordinator — Audit Needed.
   - What to learn: keep explicit separation of intent -> action delegation; centralise subscriptions to sensors and state changes.
   - What not to copy: monolithic method bodies that own low-level physics, animation, save logic or PlayMaker hooks.

- `HeroControllerStates.cs`
   - Responsibility: enumerates and documents canonical hero states used across systems.
   - Important behaviours: state names and transitions are consumed by camera, audio, hit detection, and save systems.
   - Timers/buffers/flags: state expiration timers and transient flags (e.g., `isHurt`, `isInvulnerable`).
   - Comparable in target: `HeroStateBlackboard` usage — Audit Needed.
   - What to learn: unify state names into a single authoritative source to avoid string mismatches.
   - What not to copy: scattered string-based state checks across unrelated systems.

- `HeroControllerConfig.cs` / `HeroControllerConfigWarrior.cs`
   - Responsibility: ScriptableObject configs for movement, attack windows, timings and tunables per character variant.
   - Important behaviours: hold tuning values used by controller and animation timing.
   - Timers/buffers/flags: animation window timings, attack frame windows, stun durations.
   - Comparable in target: `HeroConfig` ScriptableObjects — Audit Needed.
   - What to learn: use SOs for per-character variants and expose only safe fields.
   - What not to copy: mutate SOs at runtime or embed scene-only references in SOs.

- `HeroAnimationController.cs`
   - Responsibility: play animations, raise animation events, and route callbacks to gameplay systems.
   - Important behaviours: event hooks for attack/hit windows, fallback timers when events are missed, blend handling.
   - Timers/buffers/flags: per-clip event windows, fail-safe timers for missed events.
   - Comparable in target: `HeroAnimationController` (Animancer) — Audit Needed.
   - What to learn: implement time-based fallbacks for critical events so missed event frames don't break logic.
   - What not to copy: rely exclusively on PlayMaker or tk2d animation event wiring.

- `InputHandler.cs`
   - Responsibility: raw input sampling, buffering, and mapping to high-level intents.
   - Important behaviours: input buffering for jumps/attacks, multi-source support, repeat/hold detection.
   - Timers/buffers/flags: `inputBuffer` durations, last-input timestamps per action.
   - Comparable in target: `HeroInputReader` / Input buffer helper — Audit Needed.
   - What to learn: centralise buffering, expose a tiny API (Consume, Peek, Clear) for actions.
   - What not to copy: duplicating input reads across components or embedding buffering logic in actions.

- `CameraController.cs`
   - Responsibility: follow target with configurable smoothing and apply look offsets and bounds.
   - Important behaviours: separate X/Y damping, look offsets tied to hero state, start-locked timers, bounds clamping.
   - Timers/buffers/flags: `startLockedTimer`, lock area priority flags, lookOffset smoothing.
   - Comparable in target: `CameraController` — Audit Needed.
   - What to learn: accept intent signals from blackboard rather than probing hero internals.
   - What not to copy: direct reads of private hero fields or tight coupling to the hero update order.

- `CameraLockArea.cs`
   - Responsibility: define camera lock regions and manage enter/exit signals and priorities.
   - Important behaviours: OnEnter/OnExit callbacks, priority handling and bounds calculation.
   - Timers/buffers/flags: priority integer, entered/exited flags, world bounds cache.
   - Comparable in target: camera lock area system — Audit Needed.
   - What to learn: keep lock geometry and priority small and data-oriented; use persistent identifiers for critical markers.
   - What not to copy: baking scene-only references as runtime singletons.

- `DamageHero.cs`
   - Responsibility: source-side construction of hits targeted at the player (used by enemies/hazards).
   - Important behaviours: assemble `HitInstance` metadata (damage, force, flags), parry/clash interactions.
   - Timers/buffers/flags: parry windows, lastHit timestamps per source.
   - Comparable in target: `HeroHealthComponent` target API and source-side weapons — Audit Needed.
   - What to learn: a consistent `HitInstance` shape and explicit parry metadata.
   - What not to copy: tight coupling to PlayMaker or scene-only event names for parry handling.

- `DamageEnemies.cs`
   - Responsibility: attacker-side detection and buffering of hits produced by the player's attacks.
   - Important behaviours: per-frame `enteredColliders` queue, de-dup/hit-once-per-swing bookkeeping, processing buffer swap in LateFixedUpdate.
   - Timers/buffers/flags: `frameQueue`, `currentDamageBuffer`, `processingDamageBuffer`, stepsToNextHit pacing.
   - Comparable in target: `HeroAttackModule` / weapon collision — Audit Needed.
   - What to learn: ordered buffering to prevent duplicates and to control multi-hit pacing.
   - What not to copy: PlayMaker FSM callbacks; prefer explicit C# processing loops.

- `HealthManager.cs`
   - Responsibility: target-side receive-and-respond for hits, invincibility, blocking and death flow.
   - Important behaviours: `IHitResponder.Hit` checks, directional block checks, damage scaling, recoil and death handling.
   - Timers/buffers/flags: invincibility frames, block flags, dying flag, damage history.
   - Comparable in target: `EnemyHealthComponent` / `HeroHealthComponent` — Audit Needed.
   - What to learn: keep target-side checks authoritative and local; expose clean events (`OnDamaged`, `OnDeath`).
   - What not to copy: centralised logic that ignores target blocking or per-source i-frames.

- `HeroBox.cs`
   - Responsibility: player hurtbox that buffers incoming hits and applies them in a safe order.
   - Important behaviours: buffer incoming Hit data, parry integration, applying buffered hit in LateFixedUpdate.
   - Timers/buffers/flags: `isHitBuffered`, `bufferedHit`, parry window flags.
   - Comparable in target: `HeroBox` — Audit Needed.
   - What to learn: per-target buffering avoids race conditions and keeps physics-consistent application points.
   - What not to copy: mixing buffered logic into arbitrary Update calls rather than LateFixedUpdate.

- `Recoil.cs`
   - Responsibility: apply knockback/recoil while performing sweep/collision checks to avoid clipping.
   - Important behaviours: RecoilByDirection/Damage, optional freeze-on-recoil, sweep corrections.
   - Timers/buffers/flags: currentRecoil vector, freezeInPlace toggle, OnCancel/OnHandleFreeze events.
   - Comparable in target: `HeroMotor` knockback API — Audit Needed.
   - What to learn: motor should execute recoil after receiving a request; perform sweep correction to respect collisions.
   - What not to copy: directly modifying rigidbody without motor mediation.

- `CharacterBumpCheck.cs`
   - Responsibility: micro-collision adjustment to prevent getting stuck after small displacements.
   - Important behaviours: iterative bump tests, tolerance thresholds, corrective offsets.
   - Timers/buffers/flags: small counters for corrective attempts, debug flags.
   - Comparable in target: micro-collision helpers in `HeroMotor` — Audit Needed.
   - What to learn: add small, contained bump-correction utilities.
   - What not to copy: heavy-weight correction logic that hides root collision bugs.

- `HeroAudioController.cs` / `AudioManager.cs`
   - Responsibility: local audio routing and global audio management (SFX, music, channels).
   - Important behaviours: call sites use `AudioManager.Instance.PlaySFX`, music control, channel grouping.
   - Timers/buffers/flags: fading timers for music, one-shot SFX triggers.
   - Comparable in target: `AudioManager` — Partial / Audit Needed.
   - What to learn: centralise SFX and music playback via a singleton manager; enforce play-via-manager policy.
   - What not to copy: direct `AudioSource.Play()` calls scattered across gameplay prefabs.

- `CameraShakeManager.cs`
   - Responsibility: aggregate multiple shake requests and apply an offset to the camera; optionally trigger freeze frames.
   - Important behaviours: list of active shakes, weight evaluation, coroutine-based frame freeze support.
   - Timers/buffers/flags: per-shake lifetime, FreezeFrames coroutine state.
   - Comparable in target: `CameraShakeManager` placeholder — Missing / Audit Needed.
   - What to learn: design a small API to accept shake presets and priorities; keep freeze scheduling separate and routed through `GameManager`.
   - What not to copy: heavy dependency on PlayMaker or direct Time.timeScale changes inside multiple callers.

- `GameManager.FreezeMoment` (in `GameManager.cs`)
   - Responsibility: centralised facade for hit-stop/time-slow presets and ramp behaviour.
   - Important behaviours: map enum presets to ramp-down/wait/ramp-up sequences; manage time-control stack.
   - Timers/buffers/flags: ramp durations, wait times, active TimeControl instances.
   - Comparable in target: `GameManager` time-control — Partial / Audit Needed.
   - What to learn: centralise freeze/hit-stop in a small, testable API rather than ad-hoc timeScale changes.
   - What not to copy: exposing direct raw Time.timeScale changes in many places.

- `RestBench.cs`
   - Responsibility: bench/rest interaction that notifies hero of rest availability.
   - Important behaviours: OnTriggerEnter/Exit calls to `HeroController.NearBench`, animation/bench VFX hooks.
   - Timers/buffers/flags: proximity flags, rest cooldowns.
   - Comparable in target: rest point handling — Partial / Audit Needed.
   - What to learn: simple trigger-based notification with clear API to hero controller.
   - What not to copy: embedding save logic into the bench component itself.

- `RespawnMarker.cs`
   - Responsibility: world-placed respawn anchors with persistent keys for save/restore.
   - Important behaviours: persistent ID, respawn position, activation state.
   - Timers/buffers/flags: activation flags, last-activated timestamp.
   - Comparable in target: `RespawnMarker` — Partial / Audit Needed.
   - What to learn: use persistent keys and lightweight data for respawn anchors; expose restore hooks.
   - What not to copy: storing scene object references directly in save blobs.

- `TransitionPoint.cs`
   - Responsibility: scene transition trigger (door/portal) with optional fade and respawn mapping.
   - Important behaviours: start transition sequence, pass params to `GameManager.BeginSceneTransition`.
   - Timers/buffers/flags: transition lock flags, destination metadata.
   - Comparable in target: scene transition system — Audit Needed.
   - What to learn: delegate actual load/fade to `GameManager`; keep triggers data-only.
   - What not to copy: calling `LoadSceneAsync` directly from gameplay triggers.

- `SaveRestoreHandler.cs`
   - Responsibility: abstract file-backed save/restore layer with versioning and optional encryption.
   - Important behaviours: JSON <-> bytes translation, backup naming with timestamps/versions, restore-point trimming.
   - Timers/buffers/flags: max restore points, trimming thresholds.
   - Comparable in target: `SaveManager` — Audit Needed.
   - What to learn: implement versioned backups and corrupt-save fallbacks; keep serialization of gameplay objects out of core save files.
   - What not to copy: serialising live MonoBehaviours or scene references directly.

- `SaveGame.cs`
   - Responsibility: small PlayMaker action that calls `GameManager.SaveGame(null)` in reference.
   - Important behaviours: bridge for PlayMaker flows, triggers save with optional metadata.
   - Timers/buffers/flags: none intrinsic — acts as a wrapper.
   - Comparable in target: save triggers — Audit Needed.
   - What to learn: provide small glue entry-points for UI/flow, but prefer direct method calls in code.
   - What not to copy: relying on PlayMaker wrappers as primary save triggers.

- `PlayerData.cs`
   - Responsibility: container for persistent player data used by reference (large, many concerns).
   - Important behaviours: stores many fields (progress, flags, unlocks) and is used across systems.
   - Timers/buffers/flags: version field, lastSave timestamps, many feature flags.
   - Comparable in target: `PlayerData` style containers — Audit Needed.
   - What to learn: version and migration fields are useful; keep data small and focused per feature.
   - What not to copy: a single monolithic god-object; prefer splitting by concern and explicit migration.


## Gap Analysis Table

| Area | What the reference handles | What the target project may already have | Risk if ignored | Recommended audit / adaptation |
|---|---|---|---|---|
| Input buffering | Per-action input buffering, buffer windows, multi-source handling | Audit Needed | Missed or dropped inputs; poor responsiveness | Verify `HeroInputReader` for buffer API; add small `InputBuffer` utility if missing |
| Coyote time / jump grace | Coyote timers for forgiving jumps | Audit Needed | Unforgiving jumps, poor player feel | Check `HeroJumpAction` and `HeroMotor` for coyote implementation and tuning fields |
| Fall shaping / jump cut | Variable gravity, jump-cut behavior, fall damping | Audit Needed | Floaty or stiff aerial control | Audit vertical physics parameters and confirm jump cut hooks in `HeroMotor` |
| Landing events | Reliable land detection and landing event hooks | Audit Needed | Missed landing combos and sound/VFX cues | Verify landing detection in sensors and `HeroController` and add fallbacks if needed |
| Animation event fallbacks | Time-based fallbacks when animation events fail | Audit Needed | Missed hit windows or broken combos | Ensure `HeroAnimationController` supports time-based fallbacks for critical events |
| Ordered attack hit buffering | Attacker-side per-frame ordered hit buffers, de-dup logic | Likely Missing / Audit Needed | Duplicate hits, inconsistent multi-hit ordering | Inspect `HeroAttackModule` for buffering; adapt `DamageEnemies` pattern if absent |
| Hit-once-per-swing tracking | Swing IDs, per-swing hit lists | Audit Needed | Multiple applications of same hit per swing | Add swing-id bookkeeping in attack module if missing |
| Multi-hit pacing | Steps/pacing between multi-hits and hit delays | Audit Needed | Unfair rapid multi-hits or skipped hits | Audit attack timing and implement pacing controls in attack module if needed |
| Player hurtbox buffering | Target-side buffering (LateFixedUpdate) for incoming hits | Audit Needed | Race conditions between physics and hit application | Verify `HeroBox` buffering and ensure LateFixedUpdate application point |
| Parry / clash windows | Explicit parry windows and clash resolution | Likely Missing / Audit Needed | Unclear parry behavior, exploitable timing | Add parry window hooks and consistent clash resolution strategy in combat module |
| Damage source metadata | Rich HitInstance with source, flags, force, stagger | Partial / Audit Needed | Loss of context for reactions and VFX | Standardize `HitInstance` DTO and ensure fields used by target health systems |
| Per-source i-frames | Reference-counted i-frames per damage source | Likely Missing / Audit Needed | Premature invincibility removal or incorrect stacking | Implement reference-counted i-frame helper and audit `HeroHealthComponent` usage |
| Directional invulnerability / blocking | Directional block checks, facing tests | Partial / Audit Needed | Blocked hits still apply damage or wrong reactions | Audit health/block checks and standardize directional tests |
| Recoil ownership | Recoil requests vs direct velocity changes; sweep correction | Partial / Audit Needed | Teleporting or clipping during knockback | Ensure `HeroMotor` exposes knockback API and performs sweep corrections |
| Micro collision / bump correction | Small bump tests and iterative correction | Likely Missing / Audit Needed | Player stuck on tiny geometry | Add `CharacterBumpCheck` utilities in motor if needed |
| Camera lock priority | Priority system for overlapping camera locks | Partial / Audit Needed | Camera pop/jitter in complex rooms | Audit `CameraLockArea` priorities and ensure deterministic resolution |
| Camera intent signals | Look offsets, start-locked timers, intent flags | Partial / Audit Needed | Camera desync or poor look behavior | Use `HeroStateBlackboard` signals for camera intent; avoid probing internals |
| Camera shake | Centralized shake manager with optional freeze | Missing / Audit Needed | Inconsistent shake or duplicated code | Implement `CameraShakeManager` placeholder and check for Feel plugin before advanced integration |
| Freeze / hit-stop | Central `GameManager` FreezeMoment / time-control facade | Partial / Audit Needed | Inconsistent time-slow effects, conflicting timescale changes | Audit `GameManager` time-control and centralize small API for hit-stop presets |
| Scene transition control locks | Transition locks and action gating during loads | Partial / Audit Needed | Actions during transitions causing state corruption | Verify `BeginSceneTransition` usage and ensure action locks are honored during transitions |
| Active respawn marker handling | Persistent respawn IDs and restore hooks | Partial / Audit Needed | Wrong respawn positions after reload | Audit `RespawnMarker` activation persistence and restore implementation |
| Hazard respawn marker handling | Hazards bypassing health and forcing respawn | Partial / Audit Needed | Hazards not reliably causing respawn | Verify `HazardZone` behavior and integration with respawn markers |
| Save backend versioning / corrupt-save fallback | Versioned backups, trimming and encryption options | Partial / Audit Needed | Corrupt saves can break players' progress | Audit `SaveManager`/`SaveRestoreHandler` for backups and fallback handling |
| Local hero audio controller | Hero-local SFX routing and channeling | Partial / Audit Needed | Scattered audio calls and inconsistent SFX | Ensure `HeroAudioController` uses `AudioManager` and centralizes hero SFX calls |
| Global audio routing | Central music and SFX channels, fade control | Partial / Audit Needed | Music/SFX desync, duplicated sources | Audit `AudioManager` for channel/fade APIs and enforce usage patterns |

## What Not To Copy Directly

- Monolithic `HeroController` implementations that mix physics, input, animation, and persistence.
- PlayMaker FSM string events and PlayMaker-specific glue.
- tk2d animation dependencies and tk2d-specific data.
- Huge `PlayerData` god objects with tight coupling to gameplay behaviors.
- `GameManager` implementations that own too many unrelated systems; keep time-control and transition orchestration only.
- Direct scene object persistence patterns that serialize MonoBehaviours rather than discrete data objects.
- Relying solely on animation events as the only source of truth for gameplay timing.
- Directly reading hero internals from enemies, camera, UI, or save subsystems.

## Recommended Phased Plan

Phase A — Stabilise Existing Foundation
- Goal: Audit and stabilise core foundation (input, time-control, audio routing) before feature work.
- Why: further combat and camera work depend on accurate foundation and partial implementations already present in the target project.
- Reference systems informing it: `InputHandler`, `GameManager.FreezeMoment`, `AudioManager`, `SaveRestoreHandler`.
- Files likely inspected/edited in target: `HeroInputReader`, `HeroMotor`, `GameManager`, `AudioManager`, `HeroConfig`.
- New classes likely needed: `InputBuffer` helper, `IFramesRefCounter` utility, a small `TimeControl` facade if missing.
- ScriptableObject fields likely needed: global hit-stop presets, input buffer duration, audio channel ids.
- Manual Unity setup: verify manager prefabs in bootstrap scene, ensure SampleScene index, confirm AudioManager singleton in scene.
- Validation checklist: input buffering exists and is consistent; `GameManager` owns timeScale operations; audio routed via `AudioManager`.
- Risks / edge cases: over-editing stable code; do not replace working motor code without tests.
- What not to change: existing working `HeroMotor` internals unless defects proven by audit.

Phase B — Player Feel Polish
- Goal: Harden input and movement feel (coyote, jump cut, fall shaping).
- Why: feel issues block iterative playtesting and make combat tuning noisy.
- Reference systems informing it: `HeroControllerConfig`, `InputHandler`, jump/fall shaping in reference controller.
- Files likely inspected/edited: `HeroJumpAction`, `HeroMotor`, `HeroAnimationController`, `HeroConfig`.
- New classes likely needed: small `JumpTuning` SO, adjustments in `HeroMotor` for variable gravity.
- ScriptableObject fields likely needed: coyoteDuration, inputBufferTime, variableGravity factors.
- Manual Unity setup: tune values with live playtesting scenes; add debug toggles for coyote and buffers.
- Validation checklist: jump feels responsive; coyote window present; landing events fire reliably.
- Risks / edge cases: physics parameter changes affecting other systems; validate across scenes.
- What not to change: asset import settings or collider geometry when tuning.

Phase C — Damage / Hurt / Death / Respawn Loop
- Goal: Verify and stabilise the target-side damage loop and respawn handling without adding attacker buffering yet.
- Why: core gameplay loop must be reliable before adding combat complexity.
- Reference systems informing it: `HealthManager`, `HeroBox`, `DamageHero`, `RespawnMarker`.
- Files likely inspected/edited: `HeroHealthComponent`, `HeroBox`, `RespawnMarker`, `HeroController` hooks.
- New classes likely needed: `HitInstance` DTO standardization, health event interfaces if missing.
- ScriptableObject fields likely needed: default damage, invincibility durations, respawn offsets.
- Manual Unity setup: place respawn markers, bench objects, and hazard zones in test scenes.
- Validation checklist: damage applies correctly, i-frames per-source validated, hazard respawn works, death/respawn transitions consistent.
- Risks / edge cases: inconsistent invulnerability handling; avoid changing save flow during this phase.
- What not to change: scene transition orchestration; keep `GameManager.BeginSceneTransition` as authority.

Phase D — Combat Hit Buffering / Parry / Clash
- Goal: Add attacker-side ordered hit buffering, parry windows and clash resolution.
- Why: deterministic combat requires attacker-side ordering and swing bookkeeping.
- Reference systems informing it: `DamageEnemies`, `HeroBox` parry hooks.
- Files likely inspected/edited: `HeroAttackModule`, `Weapon` components, `HeroBox`.
- New classes likely needed: `DamageDispatcher`, `SwingId` bookkeeping, per-attack `HitList` tracking.
- ScriptableObject fields likely needed: swing IDs, multi-hit pacing config, parry window durations.
- Manual Unity setup: test scenes with multiple enemies and swing types.
- Validation checklist: each swing hits once per intended target, parry windows behave, multi-hit pacing is correct.
- Risks / edge cases: race conditions with physics; ensure LateFixedUpdate ordering and buffer swaps.
- What not to change: hero animation clips or attack timings without consult with designers.

Phase E — Camera Parity Pass
- Goal: Bring camera features to parity with reference (lock areas, look offsets, shake) using intent signals.
- Why: camera affects navigation and feel; do after core loop is stable.
- Reference systems informing it: `CameraController`, `CameraLockArea`, `CameraShakeManager`.
- Files likely inspected/edited: `CameraController`, `CameraLockArea`, camera rigs.
- New classes likely needed: `CameraShakeManager` placeholder, camera lock priority resolver.
- ScriptableObject fields likely needed: camera damping X/Y, look offset magnitudes, shake presets.
- Manual Unity setup: mark lock areas, test overlapping priority scenarios.
- Validation checklist: lock priority deterministic, look offsets responsive, default shake present (placeholder if Feel not installed).
- Risks / edge cases: camera jitter from conflicting lock areas; test edge overlap cases.
- What not to change: hero blackboard signals or motor timings; camera only listens to intent.

Phase F — Enemy Loop
- Goal: Implement enemy hurt/death flows and basic life-cycle (no attack AI yet).
- Why: populate world and iterate on combat feel with non-attacking enemies.
- Reference systems informing it: `EnemyHealthComponent`, `Recoil`, death/pool patterns.
- Files likely inspected/edited: `EnemyHealthComponent`, enemy state machines, pooling scripts.
- New classes likely needed: enemy health helpers, simple AI stubs for hurt/death responses.
- ScriptableObject fields likely needed: enemy health, knockback, death delays.
- Manual Unity setup: spawn/test enemies in test scenes, verify pooling or destroy timing.
- Validation checklist: enemies enter hurt state, recoil correctly, death triggers VFX and cleanup.
- Risks / edge cases: pooled enemies retaining state; ensure reset on respawn.
- What not to change: global spawn systems unless proven buggy.

Phase G — Ability Unlocks
- Goal: Wire ability unlocks (double jump, dash upgrades) and gating.
- Why: progression features depend on stable core loops.
- Reference systems informing it: `PlayerData` patterns for unlocks and migration.
- Files likely inspected/edited: ability unlock handlers, `PlayerData` storage.
- New classes likely needed: unlock registry, unlock SOs.
- ScriptableObject fields likely needed: ability flags, unlock conditions.
- Manual Unity setup: test unlock flows in scenes and verify persistence.
- Validation checklist: abilities unlock correctly and persist across saves.
- Risks / edge cases: migration issues when `PlayerData` shapes change; include versioning.
- What not to change: core movement behaviors while wiring new abilities.

Phase H — Save / Checkpoint Integration
- Goal: Implement robust versioned save, restore hooks, and checkpoint persistence.
- Why: persistent progress depends on safe save/restore behavior.
- Reference systems informing it: `SaveRestoreHandler`, `SaveGame`, `RespawnMarker`.
- Files likely inspected/edited: `SaveManager`, `RespawnMarker` persistence hooks, save UI.
- New classes likely needed: save migration utilities, structured save DTOs.
- ScriptableObject fields likely needed: max restore points, backup retention settings.
- Manual Unity setup: test saves across versions and simulate corrupt save recovery.
- Validation checklist: save/restore works, corrupt save fallback triggers, respawn markers restore correctly.
- Risks / edge cases: breaking migration scripts; always include migration + rollback testing.
- What not to change: runtime save triggers during earlier phases; keep save integration concentrated here.

Phase I — HUD / Audio / Polish
- Goal: Final polish for HUD feedback, SFX, music cues, hit flash and VFX.
- Why: last-mile experience improvements after all systems stable.
- Reference systems informing it: `HeroAudioController`, `AudioManager`, `CameraShakeManager`, hit flash options.
- Files likely inspected/edited: HUD, `HeroAudioController`, audio mixer snapshots.
- New classes likely needed: polish VFX helpers, hit-flash component (sprite color lerp fallback).
- ScriptableObject fields likely needed: SFX mappings, hit flash color and durations, shake intensities.
- Manual Unity setup: link audio mixer groups, assign SFX clips, tune hit flash in scenes.
- Validation checklist: all SFX via `AudioManager`, HUD updates, hit flash visually acceptable.
- Risks / edge cases: mixing audio sources causing clipping; test on target hardware.
- What not to change: gameplay logic to satisfy cosmetic polish.

## First Prompt To Run In The Target Project

Use the following Ask-mode prompt to audit the Metroidvania-Controller repository. Paste it verbatim into the auditing assistant that has read access to the target repo and run it read-only. Do NOT modify files; produce a file-by-file audit report answering the checklist.

--- Audit Prompt (paste as one message) ---

Read `Docs/ReferenceReviewHandoff.md` in the repository root. Then, perform a read-only audit of the codebase focusing on the list below. For each inspected file or system, answer the checklist questions precisely and include file paths and line references where relevant.

Files/systems to inspect (in order):
- `HeroConfig` (all matching files)
- `HeroInputReader` / `InputHandler`
- `HeroActionController`
- `HeroJumpAction`
- `HeroDashAction`
- `HeroSensors`
- `HeroMotor`
- `HeroStateBlackboard`
- `HeroAnimationController`
- `HeroController`
- `HeroAttackAction`
- `HeroAttackModule`
- `HeroHealthComponent`
- `HeroBox`
- `GameManager`
- `AudioManager`
- `CameraController` / `GameCameras` (if present)

Checklist questions (answer per-file/system):
1. Which recommendations from the handoff are already implemented? Include exact class/method/field names.
2. Which recommendations are partially implemented? Describe missing parts and exact file locations.
3. Which recommendations are missing entirely?
4. Which recommendations are not relevant to this project (explain why)?
5. Which `HeroConfig` fields exist already (list names and types) and which fields needed for Phase work are missing?
6. Does input buffering exist? If so, where, what window durations, and how is it implemented?
7. Is coyote time implemented? Where and how?
8. Is `HeroMotor` the only writer of hero velocity? Search for `Rigidbody2D.velocity =` and similar; report findings with file/line refs.
9. Are animation event fallbacks present (time-based windows) if animation events fail? Where?
10. Does attacker-side ordered hit buffering exist (e.g., DamageEnemies pattern)? Where and how?
11. Does `HeroBox` or equivalent buffer incoming damage? Where and how?
12. Does hit-stop / freeze exist, and if so how is it exposed? (e.g. `GameManager.FreezeMoment`)
13. Does camera shake exist and is it centralized? Where?
14. Are respawn markers implemented (stub/partial/full)? Provide file refs and indicate whether persistent keys exist.
15. What is the single safest first implementation task to improve stability for Phase work? Provide a one-line task and a tiny PR plan (files to change and basic tests/manual checks).

Deliverables:
- A file-by-file audit report answering all checklist questions with file/line references.
- A prioritized list of missing pieces and `Audit Needed` flags.
- The single safest-first PR plan with exact minimal edits and a testing checklist.

Final next step: run the audit prompt in the target project (read-only) and return the generated audit report.

--- End Audit Prompt ---

Do not implement changes before running the audit prompt.


---
If you want this saved elsewhere, or prefer the full raw readouts for each reference file I inspected, tell me which files to include and I will append them.

Contact: I reviewed files under `e:\Silksong\Assembly-CSharp` on 2026-05-15 and produced this adaptation for your Motor/Sensors/Actions architecture.
