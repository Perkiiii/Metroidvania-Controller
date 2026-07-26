# Feature Spec — Boss Encounters

**Last audited:** 2026-07-25 — Phase A production hardening

## Implementation status

The reusable Phase 1 encounter lifecycle, shared enemy prerequisites, persistent boss-health HUD,
and foundation validator are implemented. Phase 2A adds the first playable encounter in
`SampleScene4`: the **Undead Executioner**, stable encounter ID
`boss_sample_04_executioner`. Phase 2A.5 hardened authoring/debugging around that same slice
(retry-contract documentation, a read-only debug Inspector, authoring gizmos, a basic hit flash,
and validator scope cleanup) without changing encounter or actor architecture.
Phase A production hardening adds fail-closed silent preparation, enabled-build-scene placement
uniqueness, stronger barrier/spirit validation, and Editor-owned defeated-record reset tooling.

The Phase 2A fight has one coordinated participant, two boss-owned attacks, one local phase
transition, Animancer presentation, a non-targetable spirit-pressure helper, and retained-root
death presentation. It uses a neutral visual-only `RewardRoot`; no progression reward has been
selected. Camera Phase 3 (2026-07-26) added an optional scene-side camera presentation adapter and
a Timeline-driven intro for this slice — see "Camera presentation adapter" below. Music overrides,
final VFX/SFX, and validated feel tuning remain deferred.

All numeric boss values are provisional first-pass tuning. Mushroom derivation, `SampleScene3`,
Double Jump, and any other progression-critical reward are not part of this implementation.

## Ownership

`BossEncounterController` is the scene-level coordinator and the sole writer of coordinated permanent completion. It owns lifecycle state, its explicit participant roster, trigger/barrier/camera requests, short hero control locks, HUD requests, the two completion gates, optional reward-root visibility, and interruption/unload cleanup.

It does not select attacks, run phases, move actors, play actor animation, time hitboxes, own rewards, initiate respawn, call `SaveManager.Save()`, or write ability ownership. Coordinated actors must not have `EnemyPersistence`; only the controller calls `WorldStateRegistry.MarkEncounterDefeated(definition.EncounterId)`.

`IBossEncounterBehaviour` is the actor-owned lifecycle seam:

- `TryPrepareForEncounter` performs silent readiness/setup and reports explicit success.
- `PlayIntro` is the first intentional visible actor presentation; `BeginCombat` starts decisions.
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

The active adapter subscribes before activating `ActorRoot`, temporarily suppresses every renderer
and collider, verifies health initialized through the actor's normal Unity initialization, and then
calls `TryPrepareForEncounter`. The entire roster must succeed before the controller closes
barriers, activates the camera lock, shows HUD, acquires an intro control lock, emits
`EncounterStarting`, or invokes any intro. `PlayIntro` restores each actor's authored
renderer/collider states immediately before actor-owned presentation begins.

Preparation failure is defensive authoring protection, not a gameplay result. The controller logs
the failed roster index and participant, interrupts/deactivates every participant activated by the
attempt, keeps or restores the quiet arena state, releases attempt-scoped hero-death/resources,
writes no completion, keeps the reward hidden, and returns to retryable `Dormant`. Participant
lifecycle subscriptions remain installed across this retryable failure; callbacks received after
the abort cannot advance because the controller is no longer in `Starting`. A corrected participant
may therefore succeed on a later `TryBeginEncounter` call on the same controller without duplicate
subscriptions.

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
- `PresentationRoot` also carries a `SpriteFlash` (shared `SpriteFlash.mat`/`Custom/SpriteFlash`
  shader, same asset Mushroom uses), auto-discovered by `EnemyHealthComponent.Initialize`'s
  `GetComponentInChildren<SpriteFlash>(true)` — no additional wiring beyond the component and
  material assignment. `EnemyFeedbackController` (MMF hit/death sparks) is not yet assigned; doing
  so requires authoring new MMF feedback content and remains deferred.

### Editor authoring support

`UndeadExecutionerBehaviourEditor` (a small `CustomEditor`, not a full boss editor) appends a
read-only debug section below the default Inspector: the effective `ComboFirst`/`ComboSecond`/
`ShadowBurst`/spirit timings actually read from `UndeadExecutionerConfig` (the serialized
Startup/Active/Recovery/Cooldown fields shown directly on each `EnemyAttackController` are prefab
defaults that `TryConfigureTimings` overwrites at `TryPrepareForEncounter` time and must not be tuned
directly), plus live `State`/`CurrentAttack`/`IsPhaseTwo`/`Prepared`/hover-Y/arena-limit values
while in Play Mode. `UndeadExecutionerBehaviour.OnDrawGizmosSelected` draws the arena span, hover
height, and neutral-decision distance thresholds; the shared `EnemyAttackHitbox.OnDrawGizmosSelected`
draws any authored hitbox's bounds (green when enabled, grey when authored-disabled) so hitbox
geometry can be inspected without toggling colliders on by hand. All of this is Editor/debug-only
and changes no runtime behavior.

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

`Starting` is entered internally while the controller performs synchronous silent preparation.
No player-visible encounter state is committed until every participant reports ready. A failed
preflight returns to `Dormant`; it does not traverse the combat/completion path.

The final commit is synchronous, non-yielding, null-safe, and guarded by `completionCommitted`: unsubscribe hero death, enter `Completing`, mark the registry exactly once, reconcile reward/barriers/camera/HUD/control lock, notify participants, enter `Completed`, then emit `EncounterCompleted`. Source-scoped disable cleanup may still release control, HUD, camera, subscriptions, and routines after commit, but cannot interrupt the encounter, hide its reward, close barriers, reactivate actors, repeat persistence, or replay completion.

Hero death during `Starting`, `Active`, or `BossesDefeated` enters `Interrupted`, stops participants, opens barriers, releases only this encounter's camera/control/HUD requests, unsubscribes callbacks, and writes no completion. `GameManager` remains the only respawn coordinator.

**Retry contract.** Failed preparation is retryable on the same instance as described above.
Ordinary hero death is different: `HandleHeroDeath` and the disable/destroy cleanup path deliberately do not
restore `State` to `Dormant` or re-enable the trigger themselves — the only way this encounter
re-arms is a fresh `InitializeEncounter()` run (i.e. a new `Awake()`). The normal production path
through `GameManager.BeginRespawnSequence` performs a full `SceneManager.LoadSceneAsync` reload of
the checkpoint scene, including same-scene checkpoints, and reconstructs the encounter. If the
saved scene is not loadable or the transition cannot start, `GameManager` deliberately falls back
to degraded in-place respawn; an interrupted boss does not rearm in that emergency path.
**Same-instance retry after ordinary hero death is unsupported.** Checkpoint/build validation must
keep the full-reconstruction path available.

## Camera and HUD

The encounter remains only a source for a normal scene-authored `CameraLockArea`; concrete boss
behaviours have no camera dependency.

### Camera presentation adapter (Camera Phase 3)

`BossEncounterCameraPresenter` is an optional scene-side component and the **only** place that knows
both a specific encounter and the camera. `BossEncounterController` and `UndeadExecutionerBehaviour`
gained no camera knowledge, and the camera system gained no boss knowledge; they meet through
neutral events and generic camera presentation requests carrying only `Transform`s and authored data.

Two small neutral seams were added for it:

- `BossEncounterController.EncounterInterrupted` — raised from hero death, failed start, and
  disable/unload interruption. It is presentation-only and grants no lifecycle, persistence, or
  retry authority; the retry contract above is unchanged.
- `IBossPresentationPhaseSource` (`PresentationPhaseChanged`, `CurrentPresentationPhase`),
  implemented by `UndeadExecutionerBehaviour`. It advances when the *visible* phase beat begins
  (`BeginPhaseTransition`, i.e. the summon animation), which is deliberately distinct from the
  gameplay `IsPhaseTwo` flag. Phase thresholds and phase logic are unchanged and remain actor-owned.

Flow, all through scene-local requests that the arena `CameraLockArea` still clamps:

| Encounter event | Presentation |
|---|---|
| `EncounterStarting` | Plays the assigned intro `PlayableDirector` if present (Timeline then owns the request), otherwise acquires a boss focus itself |
| `EncounterActivated` | Releases the intro, acquires dynamic hero + boss `FrameTargets` framing with automatic zoom |
| `PresentationPhaseChanged` | Acquires a short, self-expiring higher-priority boss focus; on expiry selection falls back to the still-registered combat framing |
| `BossesDefeated` | Releases the phase focus, acquires a boss defeat focus, releases combat framing |
| `EncounterCompleted` | Releases everything, then acquires a self-expiring reward focus (explicit `rewardFocus`, else the encounter's reward root) |
| `EncounterInterrupted` | Stops the intro director and releases every handle |
| `OnDisable` / `OnDestroy` | Releases every handle |

The presenter never commits persistence, never touches reward ownership or collection, never changes
barriers, control locks, HUD, or the arena lock, and never blocks participant defeat-completion
signalling. Scene unload prunes its scene-lifetime requests independently.

`SampleScene4` authoring: `UndeadExecutionerEncounter` carries the presenter (boss focus =
`ActorRoot`, reward focus = `RewardRoot_NeutralPlaceholder`, phase source = `UndeadExecutionerBehaviour`),
and its child `CameraPresentation` carries the intro `PlayableDirector`
(`Assets/_Project/Timelines/UndeadExecutionerIntroCamera.playable`) plus a `CameraPresentationReceiver`
bound to the timeline's `CameraPresentationTrack`.

`Tools/Project/Validate Camera Phase 3` covers presenter/receiver/Timeline authoring for this slice.
Camera feel for the intro, combat zoom, phase focus, death focus, and reward reveal has **not** been
reviewed by a human; see the manual checklist in `Docs/FeatureSpecs/Camera.md`. Enabling the inactive arena lock performs an immediate
ordinary overlap refresh, and the encounter's explicit `CameraEventService.RaiseLockEntered`
remains safe because duplicate entry is idempotent and does not refresh entry order. Cleanup
releases only that area registration and disables the area; unrelated locks, bounds, free mode,
and freeze handles remain untouched. Failed preparation never enables/acquires the lock, and quiet
completed-room restoration keeps it inactive.

`BossHudEventService` is a stateless request dispatcher. It retains no active request, health roster, participants, or scene objects. The always-enabled `BossHealthDisplay` under `_GameCameras` owns the active source token, filters hide requests by reference identity, subscribes to the explicit `EnemyHealthComponent` roster, performs an initial property read, aggregates current/maximum health, and clears all scene references on matching hide or disable. It never polls or searches scenes. `PersistentHudRoot` is unchanged.

## Validation

`Tools/Project/Validate Boss Encounters` checks definitions and enabled Build Settings scenes. It
reports empty/duplicate definition IDs, multiple controller placements sharing one encounter ID
(including exact scene and hierarchy paths), collisions with
`EnemyPersistenceMode.PermanentEncounter`, missing registry/roster/required arena references,
invalid wrapper/actor hierarchy, invalid health/behavior references, and `EnemyPersistence`
beneath coordinated participants. Required barriers need at least one non-null, non-trigger solid
blocker, but that blocker may be authored disabled while the arena is open. Encounter-trigger and
camera-lock trigger colliders cannot be reused as blockers. Optional open/closed presentation roots
may both be absent, but cannot reference the same object. The validator never generates IDs or
mutates scenes and cancels rather than opening build scenes when any loaded scene is dirty.

`Tools/Project/Validate Undead Executioner` validates the concrete prefab and loaded scene
instances: floating Rigidbody2D setup, `EnemyMotor`, retained-root cleanup, actor references,
required clips/events and loop settings, attack controllers/hitboxes/damage metadata, absence of
contact damage and persistence, spirit ownership, and explicit scene arena limits. It complements
the generic encounter validator; it does not attempt static movement-authority proof. It no longer
checks literal `SampleScene4` walkable-collider names (`"Platform"`, `"Platform (2)"`, `"Platform
(9)"`) — that was a scene-terrain-authoring concern coupled to default Unity object names by
string match, unrelated to boss combat authoring, and fragile to renames. That floor is instead
covered by the generic `HeroSensorsTests` ground-probe regression coverage and hands-on room
checks.

The Executioner validator also rejects any `BossEncounterParticipant` inside the spirit hierarchy;
the spirit remains a boss-owned auxiliary actor rather than a coordinated encounter participant.

In Play Mode, the component context command
`Debug: Clear Defeated Record And Reload Scene` is implemented by Editor-only tooling. It requires
the Boot-created initialized `SaveManager`, clears only the selected encounter fact, gathers and
saves all ordinary targets so unrelated state remains intact, then uses the normal scene transition
with direct same-scene reload only as an Editor fallback. Runtime `BossEncounterController` never
calls `SaveManager.Save()`.

## Deferred

- Final boss name/content approval beyond the asset-facing `Undead Executioner` identity.
- Validated tuning, final art integration, VFX, SFX, and reward content.
- Hands-on pogo, collision/platforming, attack-readability, and fight-duration validation.
- Hands-on review of the Camera Phase 3 boss presentation (intro, combat zoom, phase focus, death
  focus, reward reveal). The arena-lock/room sizing decision currently limits it to zoom rather than
  pans — see `Docs/FeatureSpecs/Camera.md`.
- Boss-music override and room-music restoration, pending a separate audio-ownership decision.
- Refights, statues, sequences, tiers, bindings, no-hit records, achievements, and a boss catalogue.
