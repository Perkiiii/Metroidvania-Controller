# Underbrew World Persistence Implementation Plan

**Status:** Phase 1 (registry foundation, enemy persistence vertical slice), Phase 2 (normal-death lifecycle integration, ability-pickup reconciliation), and Phase 3 (doors/switches/breakables, room-visitation calls) are implemented. Real boss content using `PermanentEncounter` remains not yet authored (the mode itself has been implemented and tested since Phase 2) — see `Docs/FeatureSpecs/SaveSystem.md` "Future Expansion". Note on naming: this "Phase 3" is independent of `Docs/ImplementationPlan.md`'s own "Milestone 3 — Ability System" numbering; the two are unrelated despite the shared number.

## 1. Behavioural model and research boundary

Extend the existing save pipeline with a focused `WorldStateRegistry` ScriptableObject implementing `ISaveTarget`.

It owns physical world facts only:

- Visited rooms.
- Consumed physical pickups.
- Permanent object states.
- Permanent boss/one-time encounter completion.
- Generic non-serialized until-death state.
- Non-serialized timed death records for ordinary respawnable enemies.

Ability, quest, dialogue/NPC, inventory, currency, and item ownership remain in dedicated domain ScriptableObjects implementing `ISaveTarget`.

### Ordinary-enemy respawn rule

Ordinary enemies may respawn only during scene initialization.

For this system, "scene initialization" means the one-time initialization path executed by `EnemyController` after the registry has loaded and before that enemy's gameplay systems are enabled. It does not mean `OnEnable`, pool reuse, hierarchy reactivation, an arbitrary scene callback, or a second initialization pass during gameplay.

Timer expiry never spawns, instantiates, reactivates, unsuppresses, or resurrects an enemy in the currently loaded scene. It only determines whether a transient death record remains valid when that enemy's scene next loads.

There will be:

- No live respawn scheduler.
- No coroutine-based enemy respawning.
- No per-frame timer checks.
- No timer-expiry notification that reactivates enemies.
- No in-place resurrection after expiry.
- No respawn while the player remains in the same scene.

Dynamically pooled, summoned, or disposable enemies use `RoomRuntime` and do not normally query timed persistence.

### State categories

| Category | One-time scene initialization | Normal death | Timer | Quit/continue |
|---|---|---|---|---|
| Genuine room runtime | Initializes from authored default | Resets through scene load | None | Resets |
| Respawnable enemy death | Suppressed while its transient record is valid | Records cleared before checkpoint scene loads | Evaluated once during `EnemyController` initialization | Not serialized; enemy initializes alive |
| Generic until death | Reapplied across transitions | Cleared | None | Not serialized; resets |
| Permanent world fact | Reapplied | Preserved | None | Serialized |
| Permanent encounter | Remains defeated | Preserved | Ignored | Serialized |

Remaining in a room beyond expiry has no effect on the existing dead enemy.

### Research distinction

The supplied Silksong research directly confirms that, in the reviewed C# code:

- Enemies without persistence components write no death record and return on scene reload.
- Semi-persistent records are non-serialized and cleared by normal death.
- Checkpoint/bench interaction does not directly reset enemies.
- Recoverable hazard handling does not use the normal-death reset.
- No timed respawn system was found in the reviewed C# sources.

It does not establish whether unreviewed FSM or spawn behaviour supplies timers. Underbrew's timed transient enemy records are an Underbrew design decision, not a confirmed Silksong behaviour.

## 2. Registry data and APIs

### Serialized data

Extend `WorldSaveData` with:

- `visitedRoomIds`
- Existing `collectedPickupIds`
- `defeatedEncounterIds`
- `objectStates`

Do not serialize:

- Respawnable-enemy death records.
- Generic until-death state.
- Active combat state.
- Ability, quest, NPC/dialogue, or inventory progression.

Increment the save version, normalize collections, retain existing pickup IDs, reject empty keys, resolve duplicates deterministically, and sort gathered entries.

### Restricted enemy API

Maintain enemy deaths in a separate non-serialized collection.

Public or externally consumed operations:

- `RecordRespawnableEnemyDeath(string enemyId, float respawnDuration)`
- `ResetRespawnableEnemyDeaths()`

Initialization resolution:

- `ShouldSuppressEnemyOnInitialization(string enemyId)`

Make `ShouldSuppressEnemyOnInitialization` `internal` when the persistence participant remains in the same assembly. Only the enemy persistence participant may call it, and only from the one-time `EnemyController` initialization path.

Do not expose a generally named method such as `ShouldRespawnableEnemyBeSuppressed`, which could encourage live gameplay polling.

Each transient record stores:

- Stable enemy identity.
- Death time.
- Expiry time calculated from the approved enemy policy.

Rules:

- Use `Time.timeAsDouble`; pause and zero-timescale states do not advance expiry.
- Recording an existing identity replaces its timestamp and expiry.
- The initialization query removes an expired record and returns false.
- An unexpired record returns true.
- Removing an expired record never locates or alters scene objects.
- Optional bulk pruning may run only at scene-initialization boundaries and has no scene-object effects.
- Normal death, fresh game, Continue, and slot changes clear the collection.
- Save gathering never writes the collection.

No expiry notifications are dispatched.

### Duration ownership

Store ordinary-enemy respawn duration in shared `EnemyConfig`, not individual prefab instances.

Permanent encounters, room-runtime enemies, and generic until-death objects ignore this value.

### Other APIs and notifications

Expose focused methods for room visitation, pickup consumption, encounter completion, permanent physical states, and generic until-death physical states.

Use keyed subscriptions based on `WorldStateKey` and `WorldStateChange`. Do not expose an unqualified global `Changed` event.

Enemy timer expiry does not participate in notifications.

### Save-target independence

Every `ISaveTarget` reads and writes only its own save-data section. Target order must not affect results.

Cross-domain reconciliation occurs during the one-time scene initialization path after all targets have been applied.

## 3. Identity and enemy initialization

### Identity and modes

Relevant enemy instances receive a stable `WorldObjectId` and one mode:

- `RoomRuntime`
- `RespawnableTimed`
- `PermanentEncounter`

Validation checks missing/duplicate IDs, prefab-instance ID reuse, missing configs, invalid durations, and permanent encounters incorrectly configured as timed.

### One-time initialization sequence

`EnemyController` remains the coordinator:

1. Cache dependencies while AI, attacks, movement, contact damage, collision, registration, and feedback remain gated.
2. Initialize the persistence participant with its ID, mode, registry, and `EnemyConfig`.
3. Resolve state once:
   - `RoomRuntime`: active.
   - `RespawnableTimed`: persistence participant calls the internal `ShouldSuppressEnemyOnInitialization`.
   - `PermanentEncounter`: query permanent encounter completion.
4. If suppressed:
   - Mark the blackboard suppressed/dead.
   - Disable all colliders.
   - Disable Rigidbody2D simulation and velocity.
   - Disable contact damage and attack execution.
   - Disable renderers or the dedicated presentation root.
   - Do not initialize AI, perception, movement, combat behaviour, or feedback.
   - Keep the root coordinator/persistence components available for diagnostics.
5. If active:
   - Initialize motor, perception, recoil, health, attacks, and contact systems.
   - Subscribe persistence to confirmed death.
   - Initialize `IEnemyBehaviour` implementations last.
   - Release the initialization gate.

The first pass will not call `gameObject.SetActive(false)` from the early initialization path. Whole-root destruction or deactivation may be added later only after confirming it cannot interrupt initialization, diagnostics, or event cleanup.

Enemy behaviour components no-op until explicitly initialized. The suppression query is never repeated for that instance, including after `OnEnable` or timer expiry.

### Confirmed death

On `EnemyHealthComponent.OnDeath`:

- `RoomRuntime`: record nothing.
- `RespawnableTimed`: record ID and expiry using `EnemyConfig`.
- `PermanentEncounter`: record permanent encounter completion.

Recording affects only a future scene initialization.

## 4. World objects and domain ownership

Each persistent world object:

1. Closes its initialization gate.
2. Validates dependencies and identity.
3. Queries the registry or dedicated domain owner.
4. Applies state silently.
5. Configures collision, blockers, renderers, children, and interaction.
6. Subscribes to its exact key only when live changes are required.
7. Opens its initialization gate.

Load restoration never plays completion feedback.

`PlayerAbilityState` remains the only authority for ability ownership. The registry records only physical pickup consumption:

1. Unlock through `PlayerAbilityState`.
2. Confirm ownership.
3. Mark the pickup consumed.
4. Disable it.

Inconsistent pickup/ability data is reconciled in favor of `PlayerAbilityState`.

Quest, NPC/dialogue, and inventory progression remain in dedicated state assets. Physical objects wholly derived from those domains read the authoritative domain directly rather than duplicating state.

## 5. Scene, checkpoint, death, and timer lifecycle

### Checkpoint authority

Normal death uses only:

- `ActiveRespawnSceneName`
- `ActiveRespawnMarkerKey`

The only normal checkpoint writer remains:

`CheckpointInteractable.Interact`
→ `GameManager.SetActiveRespawnMarker`
→ `SaveManager.SetActiveRespawnPoint`

Room transitions do not replace it. `TransitionPoint.linkedRespawnMarker` remains unused, and fallback placement does not update the checkpoint.

### Re-entry before expiry

1. Enemy dies in Room A.
2. Player enters Room B.
3. Player returns before expiry.
4. The new Room A instance reaches its one-time `EnemyController` initialization.
5. Its transient record remains valid.
6. The instance is quietly suppressed for that entire scene visit.

It cannot reactivate when the timer later expires.

### Re-entry after expiry

1. Enemy dies in Room A.
2. Player leaves.
3. The duration elapses.
4. Player returns.
5. The new instance queries during its one-time initialization.
6. The expired record is removed.
7. That newly loading instance initializes normally.

### Remaining in the room beyond expiry

- The dead enemy stays dead.
- No query is made.
- No polling occurs.
- No notification is emitted.
- No prefab is instantiated.
- No component or hierarchy is reactivated.

### Normal death

1. Read the last activated checkpoint.
2. Publish the normal-death lifecycle signal.
3. Call `ResetRespawnableEnemyDeaths()`.
4. Call `ResetUntilDeathState()`.
5. Load the checkpoint scene.
6. Run each enemy's one-time initialization.
7. Restore permanent world and encounter state.
8. Resolve the saved checkpoint and restore the hero.

Ordinary enemies in the checkpoint scene return as that scene initializes. Enemies in other scenes return when those scenes are later loaded.

### Checkpoint, hazard, and Continue

Checkpoint activation saves but does not clear records, reset timers, reload enemies, or repeat enemy initialization.

Recoverable hazard repositioning does not clear records, query timers, reload the scene, or reactivate enemies.

Continue clears transient collections before loading gameplay. Ordinary enemies initialize alive; permanent encounters remain defeated.

## 6. Object policies

| Object | Policy |
|---|---|
| Disposable/summoned/pooled enemy | Genuine room runtime |
| Ordinary placed enemy | Timed transient record evaluated once during `EnemyController` initialization |
| One-time enemy/miniboss | Permanent encounter completion |
| Boss | Permanent encounter completion |
| Repeatable arena | Runtime or explicitly authored participant policy |
| Ability pickup | Consumption in registry; ownership in `PlayerAbilityState` |
| Other pickup | Consumption in registry; reward in its dedicated domain |
| Door/shortcut | Permanent physical fact unless domain-derived |
| Switch/lever | Room runtime, generic until death, or permanent |
| Breakable | Room runtime by default |
| NPC/quest object | Dedicated domain progression |
| Hazard | Existing hazard system |
| Visited room | Permanent registry fact |

## 7. Roadmap and acceptance tests

1. Add registry schema, transient collections, restricted APIs, keyed notifications, migration, and target-order tests.
2. Add stable identity, enemy modes, duration validation, and room visitation.
3. Integrate `EnemyController`'s one-time suppression path and safe component-level suppression.
4. Add normal-death clearing while preserving records across transitions, checkpoints, and hazards.
5. Add permanent encounters and ability-pickup reconciliation.
6. Integrate only existing physical world objects.
7. Validate scenes, large collections, deterministic serialization, and the absence of live respawn systems.

### Required tests

- Remain in the room beyond expiry: enemy stays dead.
- Leave and return before expiry: newly loaded enemy remains suppressed.
- Leave and return after expiry: newly loaded enemy initializes alive.
- Normal death clears records; enemies return as their scenes next initialize.
- Checkpoint interaction does not restore enemies.
- Hazard recovery does not restore enemies.
- Quit/continue restores ordinary enemies because records are not serialized.
- No enemy reactivates during gameplay because its timer elapsed.
- `OnEnable`, hierarchy reactivation, and a second runtime initialization attempt do not re-query timed persistence.
- No respawn coroutine, scheduler, per-frame check, or expiry-reactivation notification exists.
- Bosses and one-time encounters remain defeated regardless of timer or death.
- Room-runtime objects reset immediately on reload.
- Generic until-death objects have no timer.
- Room transitions never replace the activated checkpoint.
- Save-target order permutations produce identical state.
- Suppressed enemies never activate AI, physics, collision, damage, registration, or feedback.
- Suppression leaves coordinator/persistence diagnostics accessible.

## Assumptions

- Respawn duration is shared through `EnemyConfig`.
- Timer expiry affects only record validity during the one-time `EnemyController` scene-initialization path.
- Dynamically pooled or summoned enemies default to `RoomRuntime`.
- No ordinary enemy respawns until its scene loads again.
- Normal death clears all ordinary-enemy transient records before the checkpoint scene initializes.
- Checkpoints and recoverable hazards never reset ordinary enemies.
- World Graph Editor remains graph/port authoring only.
