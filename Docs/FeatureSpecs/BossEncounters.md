# Feature Spec — Boss Encounters

**Last audited:** 2026-07-24

## Implementation status

The reusable Phase 1 encounter lifecycle, shared enemy prerequisites, persistent boss-health HUD,
and foundation validator are implemented. Phase 2A adds the first playable encounter in
`SampleScene4`: the **Undead Executioner**, stable encounter ID
`boss_sample_04_executioner`.

The Phase 2A fight has one coordinated participant, two boss-owned attacks, one local phase
transition, Animancer presentation, a non-targetable spirit-pressure helper, and retained-root
death presentation. It uses a neutral visual-only `RewardRoot`; no progression reward has been
selected. Music overrides, Timeline, final VFX/SFX, and validated feel tuning remain deferred.

All numeric boss values are provisional first-pass tuning. Mushroom derivation, `SampleScene3`,
Double Jump, and any other progression-critical reward are not part of this implementation.

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

## Undead Executioner vertical slice

`UndeadExecutionerBehaviour` owns the concrete actor state machine and implements both
`IEnemyBehaviour` and `IBossEncounterBehaviour`. `UndeadExecutionerConfig` owns provisional
presentation and gameplay values. The encounter controller remains unaware of attack names,
phase rules, clips, the spirit, and actor-local cleanup.

The actor is a floating spectral body:

- Dynamic `Rigidbody2D`, zero gravity, frozen Y position, and frozen rotation.
- The authored hover Y is recorded during preparation and remains stable during combat.
- Short horizontal glides use `EnemyMotor`; the behavior never writes Rigidbody2D velocity and
  never depends on `EnemyMotor.IsGrounded`.
- Glides occur only from neutral, stay inside explicit scene limits, stop against terrain, do not
  cross through the hero, and stop before attacks, phase work, interruption, or death.
- `PresentationRoot` carries the visuals. The stable body collider covers the hood, central torso,
  and substantial shroud only; weapon/effect padding is excluded. There is no
  `EnemyContactDamage`.

The two core attacks are:

1. `ExecutionerCombo` — one authored 13-frame animation with two boss-owned
   `EnemyAttackController` windows (`ComboFirst`, then `ComboSecond`). Each window has independent
   duplicate-hit tracking, and the shared blackboard prevents overlap.
2. `ShadowBurst` — a distinct close-range radial pressure window driven by the supplied `skill1`
   animation. Close overlap alternates between burst pressure and a deliberate escape glide, so
   the wide arena cannot make this attack unreachable.

At the provisional 50% health threshold, the behavior plays the supplied summon animation and
enters phase two. A single `UndeadExecutionerSpiritPressure` samples the hero's recent horizontal
position, clamps it to the arena with wall/hero clearance, telegraphs through its appearing
animation, performs one temporary pressure window, then disappears. It is non-targetable,
non-persistent, not an encounter participant, and is removed on interruption, hero death, boss
death, completion, disable, or unload.

Lethal damage performs the shared same-frame terminal shutdown, then the actor plays its retained
death clip and emits `DefeatPresentationCompleted` once. Only the encounter controller commits
the registry fact. `NotifyEncounterCompleted` then deactivates the retained actor root.

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

`Tools/Project/Validate Undead Executioner` validates the concrete prefab and loaded scene
instances: floating Rigidbody2D setup, `EnemyMotor`, retained-root cleanup, actor references,
required clips/events and loop settings, attack controllers/hitboxes/damage metadata, absence of
contact damage and persistence, spirit ownership, and explicit scene arena limits. It complements
the generic encounter validator; it does not attempt static movement-authority proof.

## Deferred

- Final boss name/content approval beyond the asset-facing `Undead Executioner` identity.
- Validated tuning, final art integration, VFX, SFX, and reward content.
- Hands-on pogo, collision/platforming, attack-readability, and fight-duration validation.
- Timeline, which may be presentation-only.
- Boss-music override and room-music restoration, pending a separate audio-ownership decision.
- Refights, statues, sequences, tiers, bindings, no-hit records, achievements, and a boss catalogue.
