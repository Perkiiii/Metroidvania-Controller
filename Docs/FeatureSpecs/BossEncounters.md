# Feature Spec — Boss Encounters

**Last audited:** 2026-07-24

## Phase 1 status

The reusable encounter lifecycle, shared enemy prerequisites, persistent boss-health HUD, and foundation validator are implemented. No production boss, arena, reward, attacks, phases, presentation, music override, or Timeline sequence is authored yet.

Boss identity, arena choice, reward content, moveset, phase thresholds, and tuning remain content decisions for Phase 2. In particular, Mushroom derivation, `SampleScene3`, and Double Jump are not approved assumptions.

## Ownership

`BossEncounterController` is the scene-level coordinator and the sole writer of coordinated permanent completion. It owns lifecycle state, its explicit participant roster, trigger/barrier/camera requests, short hero control locks, HUD requests, the two completion gates, optional reward-root visibility, and interruption/unload cleanup.

It does not select attacks, run phases, move actors, play actor animation, time hitboxes, own rewards, initiate respawn, call `SaveManager.Save()`, or write ability ownership. Coordinated actors must not have `EnemyPersistence`; only the controller calls `WorldStateRegistry.MarkEncounterDefeated(definition.EncounterId)`.

`IBossEncounterBehaviour` is the actor-owned lifecycle seam:

- `PrepareForEncounter`, `PlayIntro`, and `BeginCombat` start actor-local behavior.
- `IntroCompleted` ends the optional starting gate.
- `DefeatPresentationCompleted` ends actor-owned death presentation.
- `InterruptEncounter` stops actor-local work without applying a completed visual state.
- `NotifyEncounterCompleted` selects the actor's final authored visual state.

Normal boss locomotion must go through `EnemyMotor`. An `IEnemyBehaviour` implementation may still receive the existing `Rigidbody2D` dependency, but boss behavior must not write velocity directly. Terminal physics shutdown remains in `EnemyHealthComponent`.

## Authored hierarchy

```text
BossParticipant                         active
├── BossEncounterParticipant            active adapter
└── ActorRoot                            inactive until encounter start
    ├── EnemyController
    ├── EnemyHealthComponent
    ├── EnemyMotor
    ├── concrete/dummy boss behaviour
    └── authored attack modules
```

The active adapter subscribes before activating `ActorRoot`, verifies health initialized through the actor's normal Unity initialization, and then calls `PrepareForEncounter`. Startup failure returns control to the encounter, which releases its arena presentation without writing persistence.

`EnemyHealthComponent` uses `RetainRoot` only for actors that need a death presentation. Death still performs the normal same-frame terminal shutdown; retaining skips only automatic root destruction. After presentation completes, the actor remains retained until `NotifyEncounterCompleted` lets its concrete behavior choose a corpse, fade, hidden renderer, or deactivated root.

## Lifecycle and persistence boundary

First-time initialization is quiet: participant actor roots and optional reward are inactive, barriers are open, camera lock is released, and the trigger is available. An already-defeated encounter queries `WorldStateRegistry.IsEncounterDefeated` once during initialization, keeps actors inactive, opens barriers, reveals the optional reward root, disables the trigger, and enters `Completed`.

The active path is:

```text
Dormant -> Starting -> Active -> BossesDefeated -> Completing -> Completed
                      \------------------------------> Interrupted (hero death/unload before commit)
```

`BossesDefeated` means every required health source has died. It does not reveal the reward or write permanent state. Completion waits independently for every required `DefeatPresentationCompleted`.

The final commit is synchronous, non-yielding, null-safe, and guarded by `completionCommitted`: unsubscribe hero death, enter `Completing`, mark the registry exactly once, reconcile reward/barriers/camera/HUD/control lock, notify participants, enter `Completed`, then emit `EncounterCompleted`. Source-scoped disable cleanup may still release control, HUD, camera, subscriptions, and routines after commit, but cannot interrupt the encounter, hide its reward, close barriers, reactivate actors, repeat persistence, or replay completion.

Hero death during `Starting`, `Active`, or `BossesDefeated` enters `Interrupted`, stops participants, opens barriers, releases only this encounter's camera/control/HUD requests, unsubscribes callbacks, and writes no completion. `GameManager` remains the only respawn coordinator.

## Camera and HUD

Enabling a `CameraLockArea` around a hero already inside is supported by explicitly raising `CameraEventService.RaiseLockEntered`; cleanup raises the matching exit and disables the area. Camera code receives no hero-internal references.

`BossHudEventService` is a stateless request dispatcher. It retains no active request, health roster, participants, or scene objects. The always-enabled `BossHealthDisplay` under `_GameCameras` owns the active source token, filters hide requests by reference identity, subscribes to the explicit `EnemyHealthComponent` roster, performs an initial property read, aggregates current/maximum health, and clears all scene references on matching hide or disable. It never polls or searches scenes. `PersistentHudRoot` is unchanged.

## Validation

`Tools/Project/Validate Boss Encounters` checks definitions and enabled Build Settings scenes. It reports empty/duplicate encounter IDs, collisions with `EnemyPersistenceMode.PermanentEncounter`, missing registry/roster/required arena references, invalid wrapper/actor hierarchy, invalid health/behavior references, and `EnemyPersistence` beneath coordinated participants. It never generates IDs or attempts static proof of movement authority. Validation cancels rather than opening scenes when any loaded scene is dirty.

Boss-content validation for motor, retained cleanup choice, Animancer clips, attacks, hitboxes, damage masks, and layers belongs to Phase 2.

## Deferred

- Concrete boss identity, arena, reward, attacks, phases, tuning, art, animation, VFX, and SFX.
- Production scene/prefab authoring and PlayMode encounter validation.
- Timeline, which may be presentation-only.
- Boss-music override and room-music restoration, pending a separate audio-ownership decision.
- Refights, statues, sequences, tiers, bindings, no-hit records, achievements, and a boss catalogue.
