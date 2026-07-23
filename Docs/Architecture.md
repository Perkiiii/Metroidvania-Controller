# Architecture — Metroidvania Controller

**Last audited:** 2026-07-23

## Overview

A 2.5D side-scrolling metroidvania built in Unity. Movement and physics are fully 2D (Rigidbody2D, Collider2D, Physics2D raycasts). Visuals add depth through layered sprites, URP lighting, and 2.5D presentation.

---

## Runtime Stack

```
HeroController (MonoBehaviour — coordinator)
├── HeroStateBlackboard   shared state bus (MonoBehaviour, inspector-visible)
├── HeroInputReader       raw input → typed signals
├── HeroSensors           physics probes → grounded / wall / ceiling flags
├── HeroMotor             Rigidbody2D velocity and gravity manipulation
├── HeroActionController  action logic orchestrator
│   ├── HeroJumpAction      (plain C# class)
│   ├── HeroDashAction      (plain C# class)
│   ├── HeroAttackAction    (plain C# class)
│   ├── HeroWallSlideAction (plain C# class)
│   └── HeroWallJumpAction  (plain C# class)
├── HeroAnimationController  Animancer playback driven by blackboard state
└── HeroSceneEntry        scripted scene-entry motion coordinator (see Scene Transitions)
```

### Update order

| Loop | Responsibilities |
|---|---|
| `Update` | `HeroInputReader.Tick` → `HeroCameraSignalBridge.Tick` → `HeroActionController.Tick` (dash/attack timers, locomotion intent) → `HeroAudioController.Tick` (footstep/wall-slide state) |
| `FixedUpdate` | `HeroSensors.FixedTick` → `CheckLanding` (fires land SFX on first grounded frame) → `HeroActionController.FixedTick` → `HeroMotor.FixedTick` |
| `LateUpdate` | `HeroAnimationController.TickVisuals` |

---

## Key Data Types

### HeroConfig (ScriptableObject)
Core baseline tuning at `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset`. Contains shared controller parameters: walk/run speeds, base jump, gravity, attack, downslash pogo, sensor probes, health response, and animation fade durations. All subsystems receive a reference at initialization. Its serialized `maxHealth` field is legacy data retained for migration safety and is not read by gameplay; maximum health belongs to `PlayerHealthState`.

### HeroAbilityConfig (ScriptableObject)
Gated traversal ability tuning at `Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset`. Holds numeric parameters for dash, wall-slide, wall-jump, and double-jump. Wired into `HeroController` via a serialized Inspector field alongside `HeroConfig`. Absent from core movement logic — actions and the motor use it only for the ability-specific behaviours it governs. Bind tuning is intentionally held in the separate `PlayerResourceConfig` asset.

- **HeroConfig** = core baseline movement and combat config (always required)
- **HeroAbilityConfig** = gated traversal ability tuning (dash, wall-slide, wall-jump, double-jump); abilities are disabled gracefully if missing
- **PlayerAbilityState** = unlock flags only; no tuning values; wired via Inspector

### HeroStateBlackboard (MonoBehaviour)
Single source of truth for the hero's runtime state. Written by Sensors, Motor, and Action classes; read by everything else, including AnimationController. Keeps subsystems decoupled — no direct references between Motor and ActionController, for example.

### Persistent Player State (ScriptableObjects)
`PlayerHealthState` and `PlayerResourceState` are persistent-value owners implementing `ISaveTarget`. Health is the sole owner of current, maximum, and temporary bonus health; `HeroHealthComponent` delegates value mutations to it while retaining scene-local damage context and i-frames. Resource stores current and maximum integer parts; `HeroAttackAction` receives the injected resource state and awards configured parts only from accepted, resource-eligible attack results. `HeroBindAction` spends resource and heals normal health once after a valid grounded hold; it is a plain C# action owned by `HeroActionController`. Both states enforce invariants, apply fresh-save defaults, and emit a neutral `StateApplied` notification after save application.

### Persistent HUD

`PersistentHudRoot` is a presentation composition root intended as a child of the persistent `_GameCameras` prefab. Its UGUI child views (`HealthDisplay` and `ResourceDisplay`) subscribe directly to the persistent state assets, perform an explicit initial refresh, and unsubscribe safely. They do not read scene-local hero components, poll in `Update`, or rebind through `GameManager.SceneInit`; room transitions therefore preserve the displayed values without HUD-specific lifecycle logic. `GameCameras` remains camera-only. `PlayerResourceConfig.partsPerPip` controls visual grouping and is not saved persistent state. See `Docs/FeatureSpecs/HUD.md` for the Editor hierarchy and placeholder-art setup.

### HeroAnimationLibrary (ScriptableObject)
Maps logical animation names (idle, walk, run, jump, fall, dash, wallSlide, attackSide, attackUp, attackDown, bind) to `AnimationClip` references. Swapping a clip does not require code changes.

### HeroAttackHit (readonly struct)
Value type passed to hit-reaction interfaces. Contains: `Source` (GameObject), `Direction` (HeroAttackDirection), `Damage` (int), `Point` (Vector2), `ForceDirection` (Vector2).

---

## Combat Object Model

```
HeroAttackAction (logic)
└── HeroAttackModule[] (scene GameObjects under an "Attacks" child)
    Each module carries:
    - PolygonCollider2D  damageCollider  (trigger, enabled during hit window)
    - PolygonCollider2D  clashCollider   (optional, for parry detection)
    - AnimancerComponent visualAnimancer (VFX animation)
    - AudioClip          slashClip   (played via AudioManager.PlaySFX on activation, not AudioSource.Play)
    - direction          (Side | Up | Down)
    - mirrorWithFacing   (Side module mirrors on X)
    - resourceGenerationMode (None | PerSuccessfulTarget | FirstSuccessfulHitPerAttack)
    - resourceGainParts  (integer parts awarded by an eligible result)
```

Attack direction is determined at swing start from the vertical component of `MoveVector` against `HeroConfig.attackDirectionThreshold`. Hit detection runs every `FixedUpdate` during the active window using `PolygonCollider2D.Overlap`.

Hit receivers are tracked per-swing in a `HashSet` to prevent multi-hit on the same target in one swing.

---

## Hit-Reaction Interfaces

| Interface | When called |
|---|---|
| `IHeroAttackReceiver` | Collider overlaps the damage collider; returns a `HeroAttackResult` |
| `IHeroAttackClashReceiver` | Collider overlaps the clash collider |
| `IHeroDownslashResponder` | Damage hit occurs and direction is Down |

Implement on any MonoBehaviour in the hit object's hierarchy. `HeroAttackAction` walks up the parent chain to find them.

Resource generation is attacker-owned. `HeroController` passes the Inspector-assigned `PlayerResourceState` through `HeroActionController` into `HeroAttackAction`. No resource controller, singleton, receiver callback, or global combat event bus is used. `HeroAttackAction` applies the active module's generation policy only after an accepted result reports `ResourceEligible`.

---

## Animation

`HeroAnimationController` drives Animancer directly — no Animator parameters. Locomotion uses a `LinearMixerState` keyed on horizontal speed (idle → walk → run thresholds from `HeroConfig`). Action states (attack, dash, wall-slide, Bind, jump, fall) are played as one-shots with configurable fade durations. Bind uses an Animancer end-event as a completion signal and a duration timer as a fail-safe; missing Bind clips disable the action rather than silently falling back.

Attack animation completion is signalled back to `HeroAttackAction.CompleteAttackFromAnimation` via an Animancer end-event. A fail-safe timer forces the attack to end if the event does not fire within the expected duration.

---

## Sensor System

`HeroSensors` uses three-point raycasts (left, center, right / bottom, center, top) against `HeroConfig.terrainLayers`. Results are written to the blackboard each `FixedUpdate` before Motor runs. Probe distances and edge inset are tunable in `HeroConfig`.

---

## Control Lock System

`HeroController` exposes `AddControlLock(object)` / `RemoveControlLock(object)`. Any system can suppress player input by registering a lock token. The lock set is reference-counted; `blackboard.controlLocked` is true whenever any lock is held.

---

## Hero Health, Hurt, Death, and Respawn

`PlayerHealthState` is the authoritative owner of current, maximum, and bonus health. `HeroHealthComponent` (MonoBehaviour) is the scene-side damage facade: it owns combat i-frames and gameplay-context events, but contains no mirrored health values. `HeroController` passes the already-loaded state asset into the facade at initialization and does not expose or mutate health values directly.

```
HeroController
└── HeroHealthComponent
    ├── PlayerHealthState           (authoritative current/max/bonus values)
    ├── IsInvincible                (reference-counted by source object, not a raw timer)
    ├── OnDamaged (event)           → hurt animation, knockback, i-frames
    ├── OnHazardDamaged (event)     → hazard-only flash, audio, shake, hurt pose
    └── OnDeath   (event)           → death sequence

PlayerHealthState.Changed            → neutral value notification with change reason
```

**Normal damage flow:**
1. An enemy attack calls `HeroHealthComponent.TakeDamage(amount, iFrameSource)`.
2. If not invincible: delegate bonus-first damage to `PlayerHealthState`, grant i-frames keyed to `iFrameSource`, and fire `OnDamaged`.
3. `HeroController` subscribes to `OnDamaged` → writes `HeroActorState.Hurt` to the blackboard, applies knockback via `HeroMotor`, adds a control lock for the stun duration.
4. If health ≤ 0: fire `OnDeath`; transition to `HeroActorState.Dead` and begin respawn.

**Hazards:** `HazardZone` supports `InstantDeath` and `RecoverLocal`. Instant-kill hazards call `HeroHealthComponent.TriggerHazardDeath()`, which atomically clears normal and bonus health and emits one `PlayerHealthState.Changed` notification with reason `ForcedDepletion`. Recoverable hazards call `HeroHealthComponent.TakeHazardDamage()`, which ignores normal combat i-frames, delegates damage to the state, skips `OnDamaged`, and only fires `OnDeath` if health reaches zero. Nonfatal recoverable hazards fire `OnHazardDamaged` for feedback, then pass a `HazardContact` to `GameManager.BeginHazardRecoverySequence()` for an impact delay, fade, local reposition, camera snap, and hero reset without restoring health. `HeroBox.HandleHazard` intentionally discards any buffered normal/contact damage before processing the hazard — hazards take priority over same-step enemy hits buffered for `FixedUpdate`. `BeginHazardRecoverySequence` immediately grants temporary invincibility so enemy contact damage cannot reach the hero through a `FixedUpdate` flush during the recovery window. Hazard recovery tuning (impact delay, black-screen hold, fade durations, i-frame duration) lives in `HazardRecoveryProfile` (SO), referenced by `HazardZone` and carried in `HazardContact` — `GameManager` is a sequence coordinator, not a tuning database. Key defaults: `ImpactDelay` 0.18 s (recommended 0.18–0.20 s), `BlackScreenHold` 0.1 s, `RecoveryIFrameDuration` 0.75 s, `FadeOutDuration` −1 (use camera default), `FadeInDuration` −1 (use camera default); if no profile is assigned the code falls back to these values. Set `FadeInDuration` to 0.35–0.45 s on a profile for a snappier local recovery feel relative to the longer scene-transition fade-in. Create a shared `HazardRecoveryProfile.asset` under `Assets/_Project/ScriptableObjects/World/` and assign it to each recoverable `HazardZone`. `GameManager` caches the hero's post-placement position on every scene load (`_sceneFallbackPosition`) as a last-resort fallback if no `RespawnMarker` or `HazardRespawnMarker` is found; scenes with recoverable hazards should always author at least one `RespawnMarker`.

**Respawn markers:**
- `RespawnMarker` — scene object placed at save points. Set as the active normal-death respawn point **only when a checkpoint is activated** (`CheckpointInteractable.Interact`). Crossing a `TransitionPoint` does **not** update the active respawn marker in the current pass — death after a gate crossing returns the hero to the last activated checkpoint, not to the door (see Scene Transitions for the deferred `linkedRespawnMarker` field).
- `HazardRespawnMarker` — scene object placed near recoverable hazards. `HazardZone` can reference one directly as its local recovery point. If unassigned, `GameManager` falls back to the nearest `RespawnMarker`, then the cached scene entry position. Author at least one `RespawnMarker` per scene that contains recoverable hazards. Trigger-updated active hazard markers are deferred; if added later, the live active pointer belongs on `GameManager`, not `HeroController` or `SaveManager`.

---

## Ability Unlock State

Gated abilities are controlled by a `PlayerAbilityState` ScriptableObject at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`. Each action class checks the relevant flag in its `CanStart` condition. Ability gate checks live in the individual action classes — never in `HeroController` or `HeroStateBlackboard`.

```
AbilityId (enum)
  Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast, Bind

PlayerAbilityState (SO)
├── dashUnlocked        bool  (default true — covers ground and air dash; no separate flags)
├── wallClingUnlocked   bool  (default true — shared gate for wall-slide and wall-jump; no separate flags)
├── sprintUnlocked      bool  (default false)
├── wallLatchUnlocked   bool  (default false)
├── doubleJumpUnlocked  bool  (default false)
├── driftCloakUnlocked  bool  (default false)
├── spiritCastUnlocked  bool  (default false)
├── bindUnlocked        bool  (default false — checked in HeroBindAction.CanStart() only)
└── AbilityChanged event — fired by SetUnlocked only when value changes; scene gates subscribe for runtime changes
```

`PlayerAbilityState` is separate from `HeroConfig` (tuning values) and from the save data class (`AbilitySaveData`). It implements `ISaveTarget`: `GatherSaveData` copies the 8 flags into `AbilitySaveData`; `ApplySaveData` calls `SetUnlocked(AbilityId, bool)` for each flag so runtime subscribers receive `AbilityChanged` events when values change. Scene `AbilityGate` objects match already-loaded state because they call `Refresh()` in `OnEnable`. `SaveManager` includes the asset in its serialized, Inspector-ordered save-target collection and calls both methods at the correct points in the save/load lifecycle.

`AbilityPickup` (MonoBehaviour) calls `abilityState.Unlock(ability)` on hero trigger contact. `AbilityGate` (MonoBehaviour) refreshes on enable, then subscribes to `AbilityChanged` and enables/disables a blocker object or collider reactively.

`PlayerAbilityState`, `PlayerHealthState`, `PlayerResourceState`, and `PlayerResourceConfig` are wired into `HeroController` via serialized Inspector fields; `HeroActionController.Initialize` passes them to the relevant action constructors. No `AssetDatabase` lookup is used.

See `Docs/FeatureSpecs/Abilities.md` for the full per-ability spec.

---

## Enemy Architecture

Each enemy is a self-contained scene object. The structure mirrors the hero's blackboard-and-action pattern but is simpler.

**Implemented:**

```
EnemyController (MonoBehaviour — coordinator)
├── EnemyConfig (SO)          movement speed, knockback, stun, health, SFX
├── EnemyStateBlackboard      alerted, hurt, recoiling, attacking, attackWindowActive, dead flags
├── IEnemyBehaviour           interface; current impl: MushroomEnemy
├── EnemyHealthComponent      implements IHeroAttackReceiver and IHeroDownslashResponder
├── EnemyRecoil               hit freeze / knockback / stun recovery
├── EnemyFeedbackController   enemy-local MMF hit, pogo, body-hit, and death players
├── EnemyAttackController     optional authored attack-window runtime
├── EnemyAttackHitbox         optional active-window hero damage collider
├── DamageHero                data marker for a collider that can hurt the hero
└── EnemyContactDamage        persistent body-touch damage behaviour
```

`EnemyMotor` and `EnemyPerception` are implemented and wired by `EnemyController` when present. `MushroomEnemy` implements Idle, Patrol, Chase, Attack, Hurt, and Dead using `EnemyMotor`, `EnemyPerception`, and optional `EnemyAttackController`. It remains the first/basic enemy archetype, retains body-contact damage, and validates explicit authored attacks by entering `EnemyAttackController` from Chase when the hero is in range. Mushroom uses one authored attack clip with animation events for `OpenAttackWindow`, `CloseAttackWindow`, and `CompleteAttack`; timer fallback remains on `EnemyAttackController`. `Assets/_Project/Prefabs/Enemies/Mushroom.prefab` is the foundation validation prefab.

Manual Unity validation on 2026-06-05 confirmed: Enemy AI foundation validator passed; Mushroom patrol, detection/chase, authored attack entry, startup inactive hitbox, active-window damage, duplicate-hit prevention, later-window damage after cooldown/i-frames, contact damage coexistence, contact+attack same-moment safety, hurt/death attack interrupts, death damage shutdown, and downslash pogo all worked. No hero feel values were changed.

`EnemyAttackController` implements reusable authored attack windows (`Startup -> Active -> Recovery -> Cooldown`), animation-event methods (`OpenAttackWindow`, `CloseAttackWindow`, `CompleteAttack`), timer fallbacks, cooldown, and interrupt cleanup. `EnemyAttackHitbox` is disabled by default, uses `DamageHero` metadata, damages through `HeroBox`, and shares duplicate-hit prevention per active window.

`EnemyController` is wiring/coordinator only. Enemy-specific behaviour components own state transitions. `EnemyMotor` owns normal enemy velocity writes; `EnemyRecoil` may temporarily override velocity through `EnemyMotor` during hit reaction.

`EnemyHealthComponent` is the only class in the project that implements `IHeroAttackReceiver`. It returns `Ignored` for invalid or already-dead interactions, `Damaged` for accepted nonlethal damage, and `Killed` when accepted damage causes death. Accepted results include the applied amount and conservative future `ResourceEligible` metadata; no resource state is mutated. When accepted, it subtracts damage, plays hit feedback, and delegates hit reaction to `EnemyRecoil`. `HeroAttackAction` uses accepted results for first-connect hit-stop, camera shake, and attack feedback while preserving its per-swing receiver set. `Blocked` and `Invulnerable` remain reserved outcomes because no such receiver systems exist yet. `EnemyRecoil` applies the knockback or freeze response from `hit.ForceDirection`, exposes its `Ready` / `Frozen` / `Recoiling` state for debugging, and owns the `hurt` / `recoiling` blackboard flags until stun recovery ends. `DamageHero` marks a collider as capable of hurting the hero and stores shared damage metadata. Behaviour-specific scripts such as `EnemyContactDamage` decide when and how that damage is applied, including cooldown and knockback policy. Enemies do not reference `HeroController` or any hero subsystem; they may read the hero's `Transform` for detection targeting.

See `Docs/FeatureSpecs/EnemyAI.md` for the full state machine spec and config schema.

### Enemy World Persistence (World Persistence Phase 1)

`WorldStateRegistry` (SO, `Scripts/World/Persistence/Core/WorldStateRegistry.cs`) implements `ISaveTarget` and owns physical world facts: visited rooms, consumed pickups, permanent object states, and permanent encounter completion (all serialized), plus non-serialized generic until-death state and non-serialized timed death records for ordinary respawnable enemies. `EnemyPersistence` (`Scripts/Enemy/EnemyPersistence.cs`) is the per-instance participant attached alongside `EnemyController`, carrying a stable `worldObjectId`, an `EnemyPersistenceMode` (`RoomRuntime` | `RespawnableTimed` | `PermanentEncounter`), and a registry reference. Ordinary-enemy respawn duration lives on shared `EnemyConfig.respawnDuration`.

`EnemyController.Awake` resolves suppression exactly once, before any AI/perception/movement/combat/feedback initialization: if the persistence participant reports suppression, colliders are disabled, `Rigidbody2D.simulated` is set false, renderers are disabled, and `EnemyMotor`/`EnemyPerception`/`EnemyAttackController`/`EnemyContactDamage`/`IEnemyBehaviour` components are disabled without ever being initialized — `EnemyController` and `EnemyPersistence` remain enabled for diagnostics, and the first pass never calls `gameObject.SetActive(false)`. Timer expiry is resolved lazily only during this one-time initialization path; there is no live respawn scheduler, coroutine, or per-frame check. On the active path, confirmed death (`EnemyHealthComponent.OnDeath`) is subscribed to `EnemyPersistence.RecordDeath`, which records a timed death (RespawnableTimed) or permanent defeat (PermanentEncounter) — RoomRuntime records nothing.

`Tools/Project/Validate World Persistence` scans enabled Build Settings scenes for missing/duplicate `worldObjectId`s (both `EnemyPersistence` and `AbilityPickup`), missing registry/config/`PlayerAbilityState` references, and invalid `respawnDuration` on `RespawnableTimed` instances. See `Docs/ImplementationPlans/WorldPersistence.md` for the full behavioural spec and `Docs/FeatureSpecs/SaveSystem.md` for the registry's save-schema and `ISaveTarget` details.

**Normal-death lifecycle integration (World Persistence Phase 2).** `GameManager.BeginRespawnSequence` — the single entry point for every normal death (including lethal/forced-death hazards, which route through the same `OnDeath` event) — calls `WorldStateRegistry.ResetRespawnableEnemyDeaths()` and `ResetUntilDeathState()` exactly once per death, immediately after the in-progress guard and before attempting either the checkpoint scene transition or the in-place fallback. This ordering is deliberate: the reset must complete before the checkpoint scene begins loading (same-scene or cross-scene) because `SceneLoader.LoadSingle`/`SceneManager.LoadSceneAsync` synchronously fires `Awake` on every object in the newly loaded scene, including `EnemyController`, which must observe cleared state to resolve timed/permanent suppression correctly. `ApplyNormalDeathRespawn` (called later, once a marker has been resolved — either after the checkpoint scene has already loaded, or immediately for the in-place fallback) no longer touches `WorldStateRegistry` at all. Checkpoint activation (`CheckpointInteractable.Interact`) and recoverable-hazard reposition (`HeroBox.HandleHazard` → `GameManager.BeginHazardRecoverySequence` → `HeroController.ResetAfterHazardRecovery`) never reference `WorldStateRegistry` and therefore never clear transient records. Continue, New Game, and slot change need no additional wiring: `WorldStateRegistry.ApplySaveData` already unconditionally clears and repopulates every collection (serialized and non-serialized) on every apply.

`AbilityPickup` (`Scripts/World/Persistence/Participants/AbilityPickup.cs`) carries its own `worldObjectId` (same stable-identity convention as `EnemyPersistence`, kept as a separate field rather than a shared component to avoid touching the 9 already-serialized `EnemyPersistence` scene overrides), a `PlayerAbilityState`/`AbilityId` pair, and a `WorldStateRegistry` reference. On initialization it reconciles the two ownership domains in favor of `PlayerAbilityState` (the sole ability-ownership authority): an already-unlocked ability suppresses the pickup and backfills a missing `collectedPickupIds` record without replaying feedback; a `collectedPickupIds` record with no matching unlocked ability logs a warning, clears the stale record via `WorldStateRegistry.ClearPickupCollectedRecord`, and leaves the pickup active so it can be genuinely reacquired, without unlocking the ability or replaying feedback. Collection calls both `PlayerAbilityState.Unlock` and `WorldStateRegistry.MarkPickupCollected`. This is currently the only physical pickup type in the project; the same stable-id + `IsPickupCollected`/`MarkPickupCollected` pattern is the reusable participant for future non-ability pickups.

### Doors, switches, breakables, and room visitation (World Persistence Phase 3)

**Stable-ID convention (documented, not a shared component).** Following the precedent set by `EnemyPersistence`/`AbilityPickup`, `PersistentDoor`, `PersistentSwitch`, and `PersistentBreakable` each declare their own `[SerializeField] private string worldObjectId` with matching tooltip wording, rather than a shared `WorldObjectIdentity` component. An ID must be unique across every persistent world object type in the project (enemies, pickups, doors, switches, breakables), never reused when duplicating a prefab instance, and is required for any lifetime except `RoomRuntime` (which never reads or writes the registry and therefore needs no ID).

**`PersistenceLifetime` enum** (`Scripts/World/Persistence/Core/PersistenceLifetime.cs`: `RoomRuntime` | `UntilDeath` | `Permanent`) is shared by `PersistentSwitch` and `PersistentBreakable`. `PersistentDoor` has no lifetime field — per the plan's policy table a door/shortcut is always a permanent physical fact, so it always calls `TryGetObjectState`/`SetObjectState` directly. `PersistenceLifetime` describes the fact represented by a given participant *instance*, not every behaviour on its GameObject — `WorldStateRegistry` itself is purely string-ID-keyed with no assumption that one GameObject owns exactly one lifetime. Composing multiple independently-lifetimed facts on one physical object (e.g. a permanent destroyed-obstacle fact alongside a separate until-death resource-availability fact) is done via multiple participant components across a small object hierarchy (e.g. a child GameObject), not by extending one component with multiple lifetime fields; the only current constraint is that `WorldPersistenceValidator.ValidateNoConflictingParticipants` rejects two persistence participants on the *same* GameObject, to catch accidental miswiring rather than to block intentional composition.

**`PersistentDoor`** (`Scripts/World/Persistence/Participants/PersistentDoor.cs`, extends `InteractableBase`) is a permanently-openable shortcut gate — never an ordinary always-usable scene-transition door (those remain `TransitionPoint`/`DoorTransitionInteractable`, untouched and untracked). Its own required trigger `Collider2D` is the interact-range detector; a separate serialized `blockerCollider` (a solid, non-trigger `Collider2D`, typically on a child object) is the physical block, disabled together with the closed-state visual the moment `Awake` resolves stored state — before any physics step can run, so a restored-open door never blocks the hero for even one frame. `Interact()` opens once, calls `SetObjectState(id, "open")`, and then `SetDisabled(true)` (an opened shortcut has nothing left to interact with). Unknown stored values log a warning and fall back to closed.

**`PersistentSwitch`** (`Scripts/World/Persistence/Participants/PersistentSwitch.cs`, extends `InteractableBase`) is a one-shot lever with a configurable `PersistenceLifetime`. It directly toggles a paired `blockerCollider`/visual-root pair (a plain scene object, e.g. a gate) exactly like `AbilityGate`'s blocker pattern — there is no generic mechanism/event framework. `RoomRuntime` never queries or writes the registry; `UntilDeath` writes through `SetUntilDeathState`/`TryGetUntilDeathState`; `Permanent` writes through `SetObjectState`/`TryGetObjectState`. `Interact()` is idempotent (a second call on an already-active switch no-ops) and disables itself once active, so restored state is indistinguishable from a freshly confirmed activation except that no feedback replays.

**`PersistentBreakable`** (`Scripts/World/Persistence/Participants/PersistentBreakable.cs`) implements `IHeroAttackReceiver` directly — the same contract `EnemyHealthComponent` uses — rather than requiring a health/hit component of its own; a single accepted hit destroys it. Same `PersistenceLifetime` options as the switch, defaulting to `RoomRuntime` per the plan's "breakable | room runtime by default" policy. `ApplyDestroyedState` disables its own solid collider and a serialized `intactVisualRoot`/`destroyedVisualRoot` pair; `intactVisualRoot` must be a separate child object rather than the breakable's own root, since toggling the root's own active state from inside its own `Awake` would risk a recursive re-entry. The registry stores only the physical fact of destruction — no rewards, drops, VFX, or audio are wired to it in this milestone.

All three participants follow the same initialization-order contract as `EnemyPersistence`/`AbilityPickup`: query the registry exactly once in `Awake`, apply the result quietly (`playFeedback: false`), configure collider/visual/interactable state before any gameplay input can reach them, and only ever write the registry from a confirmed player action (`Interact()` / `ReceiveHeroAttack`), never from restoration. `PersistentBreakable.ReceiveHeroAttack` returns `HeroAttackResult.Damaged(hit.Damage, resourceEligible: false)` (World Persistence Phase 3.1) — environmental destruction does not award hero combat resource by default. Generic receiver-agnostic resource generation (`HeroAttackAction.TryAwardResource`) would otherwise treat any accepted `IHeroAttackReceiver` hit identically to an enemy kill; this was an unintended consequence of interface reuse, not a decision, until this fix. `HeroAttackResult.Damaged`/`Killed` take an optional `resourceEligible` parameter (default `true`, so `EnemyHealthComponent` is unaffected) for exactly this kind of opt-out.

**Room visitation (World Persistence Phase 3.1).** `RoomVisitReporter` (`Scripts/World/Persistence/Participants/RoomVisitReporter.cs`) is a small participant placed once in each gameplay scene, carrying its own authored `roomId` — a separate ID namespace from `worldObjectId`, since it keys `WorldStateRegistry.visitedRoomIds`, not `objectStates`/`untilDeathStates` — and a `WorldStateRegistry` reference. `Awake()` calls `registry.MarkRoomVisited(roomId)` once. Room identity is therefore authored data, not derived from the Unity scene filename — renaming a `.unity` file never changes saved room identity. `GameManager.OnSceneLoaded` no longer touches `WorldStateRegistry` for this at all; the prior scene-name-as-room-ID mechanism relied on `SceneManager.sceneLoaded` never firing for the Boot scene, which worked but was an incidental side effect of subscription timing rather than an enforced rule. `WorldPersistenceValidator` requires exactly one `RoomVisitReporter` per enabled Build Settings scene except `Boot`, and validates room-ID uniqueness independently of the five `worldObjectId`-keyed participant types. The three gameplay scenes are authored as `room_sample_01` (`SampleScene`), `room_sample_02` (`SampleScene2`), and `room_sample_03` (`SampleScene3`). `IsRoomVisited` remains a write-only fact — nothing in the project currently consumes it (no minimap, no "first visit" popup); it exists purely as the registry-side half of the contract for whenever such a feature is built.

**Editor/config changes.** A new Physics2D layer, `Breakable` (slot 22), was added so `PersistentBreakable` colliders can be distinguished from ordinary terrain. `HeroConfig.attackHitLayers` and `HeroConfig.terrainLayers` were both extended to include it — the hero's melee attack must be able to hit breakables, and `HeroSensors` must treat an intact breakable as solid ground/wall. Neither change touches a numeric hero-feel tuning value (see `Docs/HeroFeelTuning.md`); they only add one layer to two existing collision masks.

**Validator coverage** (`WorldPersistenceValidator`) was extended with per-scene local validation for all three new types (missing ID, missing registry, missing blocker/solid-collider "destruction target", and a warning if a `RoomRuntime` switch/breakable has an ID assigned anyway since it will never be read), a check for more than one persistence-participant component on a single `GameObject`, and — most significantly — the previously-separate enemy-only and pickup-only cross-scene ID-uniqueness passes were unified into one pass covering all five participant types together, so a door can no longer silently collide with an enemy's or pickup's ID. `GetEnabledBuildScenesByName` also reports two enabled Build Settings scenes sharing a filename, as general scene-hygiene. **World Persistence Phase 3.1** added a parallel, independent validation pass for `RoomVisitReporter`: every enabled Build Settings scene except `Boot` must contain exactly one instance with a non-empty `roomId`, and room IDs are checked for cross-scene uniqueness in their own namespace (kept separate from the five-type `worldObjectId` pass above, since `roomId` and `worldObjectId` key different `WorldStateRegistry` collections).

**Vertical slice** (`SampleScene.unity`): one `PersistentDoor` (`door_shortcut_samplescene_a`, Permanent), one `PersistentSwitch` (`switch_samplescene_a`, UntilDeath) paired with a `SimpleGateBlocker` instance, one `PersistentBreakable` (RoomRuntime, no ID), and one `RoomVisitReporter` (`room_sample_01`) — all placed on the ground platform between `Checkpoint_A` and the scene's outer passage. `SampleScene2`/`SampleScene3` each carry their own `RoomVisitReporter` (`room_sample_02`/`room_sample_03`) and no other Phase 3 content yet. Prefabs live under `Assets/_Project/Prefabs/World/`; placeholder materials (flat-colored URP Unlit quads, no art pass) under `Assets/_Project/Materials/World/`.

---

## Interactable System

All world objects the player can interact with (checkpoints, NPCs, doors that require a button press, item pickups) extend `InteractableBase`:

```
InteractableBase (MonoBehaviour)
├── Priority   InteractPriority  — Low / Normal / High; higher wins when multiple are in range
├── IsDisabled bool              — deactivated interactables are skipped by the manager
└── Interact()                   — called by InteractManager when interact input is confirmed
```

`InteractManager` (DontDestroyOnLoad singleton):
- Maintains a priority-sorted list of `InteractableBase` instances in the hero's trigger range.
- Shows / hides the interact prompt UI based on whether any valid interactable is active.
- On interact input: calls `Interact()` on the highest-priority enabled interactable.

Registration: `InteractableBase.OnTriggerEnter2D` registers; `OnTriggerExit2D` deregisters. Do not poll input or manage proximity inside individual interactable MonoBehaviours.

---

## Checkpoint and Respawn Markers

**CheckpointInteractable** (extends `InteractableBase`):
- `Interact()`: calls `GameManager.SetActiveRespawnMarker(respawnMarker)`, which (a) stores the live reference for in-session respawn and (b) forwards the marker's scene name and `Key` to `SaveManager.SetActiveRespawnPoint`. Then calls `SaveManager.Save()` to persist immediately.
- Does not call any method on `HeroController` or `HeroHealthComponent`.

**HazardZone** (MonoBehaviour on a trigger collider):
- `OnTriggerEnter2D` with the hero: sends a `HazardContact` to `HeroBox`.
- `InstantDeath` hazards use the existing full death path.
- `RecoverLocal` hazards subtract hazard damage, preserve reduced health, and ask `GameManager` to recover the hero at the assigned `HazardRespawnMarker` when health remains above zero. A directly assigned `HazardRespawnMarker` is strongly recommended; `OnValidate` warns if missing, and runtime falls back to the nearest `RespawnMarker`, then the cached scene-entry position.
- `activeHazardRespawnMarkerKey` in `PlayerSaveData` remains reserved for future trigger-updated hazard marker persistence. The first-pass runtime path does not depend on `SaveManager`.

**TransitionPoint** (implemented — Milestone 1; WGE-backed): `TransitionPoint` does **not** call `GameManager.SetActiveRespawnMarker`. By design decision, death after crossing a gate sends the hero back to the last activated checkpoint, not to the entry door. A serialized `linkedRespawnMarker` field exists on `TransitionPoint` and is auto-populated from a child `RespawnMarker` in `Awake`, but is not consumed at runtime in this pass. It is reserved for a future policy pass that may opt into entry-door respawn. `ActiveRespawnMarker` (normal death) and `ActiveHazardRespawnMarker` (hazard death) remain independent. World Graph Editor (WGE) supplies scene graph / port GUID data only; Underbrew still owns runtime loading, hero placement, and respawn flow. See `Docs/Integrations/WorldGraphEditorIntegration.md`.

**Respawn marker persistence:** The save file stores the checkpoint scene name plus marker string `Key`, not a live object reference. On scene load, `GameManager.ResolveActiveRespawnMarkerFromSave()` only resolves `_activeRespawnMarker` when the loaded scene matches `activeRespawnSceneName`; it intentionally does not warn when ordinary traversal loads a different scene. On boot/continue, `PlaceHeroAtSavedRespawnIfRequested()` moves the hero to the resolved marker position before the fade-in. On normal death in a different scene, `GameManager` runs a pending normal-death respawn transition: load checkpoint scene, skip `TransitionPoint` entry motion, resolve the saved marker or first available fallback marker, restore health/state, rebind the camera, and fade in.

**Future death-drop note:** normal-death respawn destination (`activeRespawnSceneName + activeRespawnMarkerKey`) must stay separate from the eventual death-drop/shade location (`deathSceneName + deathPosition`). A future death-drop system should capture death scene + death position before loading the checkpoint scene.

---

## Scene Transitions and Loading

**World Graph Editor integration:** WGE is integrated as the scene graph, node, connection, and port authoring layer. `TransitionPoint` stores the selected WGE port GUID and `WorldGraphTransitionResolver` resolves graph data into a target scene and target passage GUID. WGE runtime transition components (`Passage2D`, `Teleport2D`, spawn points, runtime `TransitionManager`) are not used for gameplay; `GameManager.BeginSceneTransition(...)` remains Underbrew's public scene-transition entry point and delegates the lifecycle to `SceneTransitionManager`. Full ownership rules and upgrade steps live in `Docs/Integrations/WorldGraphEditorIntegration.md`.

**TransitionPoint** (implemented — Milestone 1, WGE-backed):

`TransitionPoint` is a `MonoBehaviour` + `Collider2D` that serves as both a *source* gate (triggers a transition when the hero enters) and a *destination* gate (provides spawn position and entry-motion parameters when the hero arrives from another scene).

Key serialized fields:

| Field | Role |
|---|---|
| WGE assigned port (`PassageBase._assignedPort`) | Stable WGE passage GUID for this gate. Selected in the `TransitionPoint` custom Inspector and used for both outgoing graph resolution and destination lookup. |
| `gateSide` | `GateSide` enum (Left, Right, Top, Bottom, Door, Unknown). Explicit; never inferred from GameObject name. |
| `entryOffset` | World-space nudge added to the gate's position to produce the hero spawn point. |
| `entryFacingOverride` | `EntryFacing` enum (None, ForceRight, ForceLeft). `None` means destination-hero default facing; it does NOT carry facing across scenes. |
| Per-gate motion params | `entryRunInDuration`, `entryDropSpeed`, `bottomThrowHorizontal`, `bottomThrowVertical`, `bottomThrowDuration`, `bottomGateSpawnLift`, `entryMaxFallbackTime`. All are transition-entry values only; they do not affect `HeroConfig` movement tuning. |
| `isDoor` / `requireInteract` | `isDoor` routes activation through `DoorTransitionInteractable` (extends `InteractableBase`). `requireInteract = false` makes the door auto-trigger. |
| `linkedRespawnMarker` | Reserved for a future policy pass. Auto-populated from a child `RespawnMarker` in `Awake` if empty. Not consumed at runtime in the current pass. |

Static registry: `TransitionPoint` maintains an `Active` list (populated in `OnEnable`, cleared in `OnDisable`). `FindByPassageGuid(string)` does an O(n) search and warns on duplicate WGE GUIDs. `GameManager` uses this after scene load to locate the destination gate without a `FindObjectsByType` call per transition.

Activation rules:
- Non-door, non-interact: `OnTriggerEnter2D` / `OnTriggerStay2D` route through `TransitionPoint`'s shared activation validation. `OnTriggerStay2D` remains a safety net for heroes already inside a trigger.
- Door (`requireInteract = true`): `DoorTransitionInteractable.Interact()` routes back through `TransitionPoint.TryActivateFromInteract`, so doors share the same target-scene, entry-gate, game-state, local-guard, and hero-state validation as auto gates.
- Door (`requireInteract = false`): `OnTriggerEnter2D` auto-fires; `DoorTransitionInteractable` disables itself in `Awake`.
- Wrong-direction / invalid-state: auto-trigger gates nudge the hero out of the trigger using collider bounds math, zeroing the relevant velocity component, and routing through `HeroMotor.PushOut`. Door-interact gates reject the transition without directional push-back.

**HeroSceneEntry** (implemented — Milestone 1):

`HeroSceneEntry` is a sibling `MonoBehaviour` on the hero, initialized by `HeroController`, that owns all per-gate entry branching. `HeroController` exposes only thin pass-throughs (`BeginSceneEntryPlacement`, `BeginSceneEntryMotion`). No per-gate code lives in `HeroController`.

Entry is split into two phases:
1. **Placement** (synchronous, behind black screen): facing override, motor-owned spawn placement + collider-aware ground snap, control lock, `HeroMotor.BeginScriptedEntry`.
2. **Motion** (coroutine, after fade-in so the player sees it): per-gate scripted movement until grounded or timeout, with `HeroMotor.EndScriptedEntry` + `RemoveControlLock` guaranteed in a `finally` block.

Top entries place the hero at the destination gate, apply the authored downward entry speed once, then lock X while allowing gravity to drive the fall. Bottom entries keep their two-phase diagonal throw, then release gravity while locking X. Top and Bottom entries require observing `blackboard.grounded == false` at least once before a grounded landing can complete the entry, preventing stale sensor data from short-circuiting the motion.

`HeroSceneEntry` subscribes to `HeroHealthComponent.OnDeath` and `OnHazardDamaged` and cancels the motion coroutine via `StopCoroutine` if either fires, so respawn / hazard recovery cannot fight scripted velocity.

`HeroMotor` owns all `Rigidbody2D` velocity writes and scene-entry placement through the `BeginScriptedEntry / EndScriptedEntry / SetScriptedVelocity / SetScriptedVelocityX / PushOut / TeleportTo / GetPositionWithFeetAt` API. The scripted-entry mode re-applies the locked target velocity at the end of every `FixedTick` so locomotion, gravity, wall-slide, and fall-clamp cannot overwrite it.

Editor validation: `Tools/Project/Validate Transition Gate Links` scans enabled Build Settings scenes and reports blank or duplicate WGE passage GUIDs, graph links that cannot resolve, targets outside enabled Build Settings scenes, and target scenes that do not contain exactly one matching destination gate. This is editor-only validation, not a runtime gate database.

**GameManager** (MonoBehaviour, DontDestroyOnLoad) — four responsibilities only. Do not add to these without a documented architectural reason:

1. **`GameState` enum** — `Playing`, `Paused`, `EnteringLevel`, `ExitingLevel`, `Loading`. Written only by GameManager methods, never from outside.
2. **`SceneInit` event** — fired after every scene load, before the fade-in. Scene-local systems subscribe here rather than relying on `Awake` ordering.
3. **`BeginSceneTransition(...)`** — public compatibility facade. It accepts either the legacy `(targetScene, destinationPassageGuid)` pair or a typed `SceneTransitionRequest`, then delegates to `SceneTransitionManager`. `SceneTransitionManager` validates the scene through `SceneLoader`, rejects duplicate requests, resolves the fade profile from request override → kind default → `CameraFade` fallback, records an ordered debug trace, and owns the normal transition coroutine. Actual flow: grant i-frames on outgoing hero → add control lock → freeze camera → fade out → `SceneLoader.LoadSingle` → `OnSceneLoaded` fires `SceneInit`, then caches the new hero and resolves saved respawn placement → if a pending normal-death respawn exists, place at the checkpoint marker and skip gate entry → otherwise resolve destination `TransitionPoint` by WGE passage GUID → place hero at destination gate or deterministic missing-gate fallback (`RespawnMarker`, then authored hero position) → refresh `_sceneFallbackPosition` → rebind camera → 0.1 s wait → fade in **and** per-gate entry motion start simultaneously → wait for both to complete → set `GameState.Playing`. Fade-in and entry motion are concurrent by design so the hero is already walking in from the gate as the screen reveals. Transition state and transition-owned control locks are cleaned up in `try/finally`.
4. **`Pause()` / `Unpause()`** — set `GameState.Paused`, add hero control lock, set `Time.timeScale = 0`. Unpause reverses all three in order. Nothing else in the project touches `Time.timeScale`.

GameManager must not own health, enemies, progression state, UI layout, or save logic.

**Current deviations (tech debt):** `GameManager` also implements `HitStop(float duration)` (used by `HeroAttackAction` on hit-connect), `BeginRespawnSequence()` (normal-death respawn — always reloads the activated checkpoint scene through `SceneTransitionManager`/`SceneLoader`, even when the checkpoint is in the scene already loaded, so ordinary enemies re-run their one-time `EnemyController` initialization against the just-reset `WorldStateRegistry` transient state), and `BeginHazardRecoverySequence()` (recoverable hazard reposition — stays in the current scene, never reloads it, and never touches `WorldStateRegistry`). Normal respawn placement is followed by one `HeroController.ResetAfterRespawn()` call, which routes health restoration through the health facade, explicitly clears bonus health, and clears current resource (`PlayerResourceState.Clear()`) — the single authoritative call site for all three, reached identically by normal death, lethal recoverable hazards, and forced-death hazards; `GameManager` itself still never mutates health or resource values. Additionally, `GameManager` owns `ResolveActiveRespawnMarkerFromSave()` and `PlaceHeroAtSavedRespawnIfRequested()` — these are well-defined seams to the save system, not business logic, and are considered acceptable for the current architecture stage. `GameManager` also holds one direct `PlayerHealthState` reference solely for `ResolveLoadedHealthState()`, called once by `Bootstrap.Start()` right after `SaveManager.LoadOrCreate` and before any scene/Hero exists — it normalizes a save loaded at zero health (see `Docs/FeatureSpecs/PlayerHealthAndResource.md`); this is a narrow read-and-correct seam, not general value ownership. See `Docs/ImplementationPlan.md` Known Technical Debt.

---

## Boot / Startup

Build Index 0 is a dedicated boot scene containing only a `Bootstrap` MonoBehaviour. It initialises all persistent singletons in order, loads the save file, then loads the first gameplay scene.

```
Bootstrap.Awake() — instantiate and DontDestroyOnLoad in order:
  1. GameManager
  2. SaveManager
  3. AudioManager
  4. GameCameras      (null-guarded; optional)
  5. InteractManager  (null-guarded; optional)

Bootstrap.Start():
  6. SaveManager.LoadOrCreate(0)
       — deserialize file; if missing or corrupt: CreateFreshSave
       — ApplySaveData() → Inspector-ordered ISaveTarget assets applied
  7. SaveManager.GetStartupScene(firstScene)
       — activeRespawnSceneName if loadable, else currentScene if loadable, else firstScene
  8. GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()
       — sets a single-use flag; consumed on the next OnSceneLoaded
  9. GameManager.BeginSceneTransition(startupScene)
       — fade out → LoadSceneAsync → OnSceneLoaded:
             ResolveActiveRespawnMarkerFromSave()  (key → live RespawnMarker)
             PlaceHeroAtSavedRespawnIfRequested()  (hero positioned at marker)
       → camera snap → fade in
```

`firstScene` is a serialized string field on `Bootstrap` (currently `"SampleScene"`) and is now the fallback startup scene. When a main menu scene exists, Boot should load the menu instead; the menu routes to `firstScene` on New Game or `SaveManager.GetStartupScene(firstScene)` on Continue.

Persistent singletons live only in the boot scene and carry across all subsequent loads via `DontDestroyOnLoad`. They must not be placed in gameplay or UI scenes.

---

## Save / Persistence

See `Docs/FeatureSpecs/SaveSystem.md` for the full spec.

**Foundation implemented (2026-05-20); checkpoint/boot continuation updated (2026-05-26).** The complete save data layer, manager singleton, ability round-trip, checkpoint save triggers, cross-session / cross-scene respawn marker resolution, saved-scene startup routing, and hero placement on boot are all in place.

**Structural boundary:**

```
SaveManager (DontDestroyOnLoad)
  ↓  GatherSaveData() / ApplySaveData()
  ↓
ISaveTarget (interface, implemented by persistent SOs)
  ├── PlayerAbilityState.asset       7 ability unlock flags   [implemented]
  ├── PlayerHealthState.asset        current/max/bonus health [gameplay ownership implemented]
  ├── PlayerResourceState.asset      current/max parts        [gameplay ownership implemented; generation + Bind spend + death-clear wired]
  └── WorldStateRegistry.asset       rooms, pickups, encounters, object states, enemy timers  [implemented — World Persistence Phase 1/2]
```

Targets are explicitly assigned as `ScriptableObject` assets on `SaveManager`. The Inspector list order is the deterministic gather/apply order. Initialization validates the `ISaveTarget` contract and ignores null, invalid, or duplicate entries with warnings; there is no reflection-based discovery or scene search.

`SaveManager` must not reference any `MonoBehaviour` at save or load time. All live state that needs persisting must be owned by a ScriptableObject implementing `ISaveTarget`. Scene-object identity is stored as string keys (e.g. `RespawnMarker.Key`), never as `UnityEngine.Object` references.

**Save triggers:**
- `CheckpointInteractable.Interact()` — primary in-game save
- `SaveManager.SaveOnQuit` via `Application.quitting` — configurable auto-save on exit

Never call `SaveManager.Save()` from inside a hero or enemy `MonoBehaviour`.

**Respawn key flow:**
```
SetActiveRespawnMarker(marker)           → _activeRespawnMarker (live, in-session)
                                         → SaveManager.SetActiveRespawnPoint(sceneName, key)
SaveManager.Save()                       → key written to save_slot0.json
OnSceneLoaded → ResolveActiveRespawnMarkerFromSave()  → _activeRespawnMarker restored
```

---

## Camera

See `Docs/FeatureSpecs/Camera.md` for the full spec.

`GameCameras` is a persistent singleton prefab containing the perspective main camera, HUD camera, fade canvas, `CameraTarget`, `CameraController`, and `CameraShakeCueService`. `CameraController` and `CameraTarget` read the hero's `Transform` only - no hero component references. Hero-specific camera intent is sent one-way by `HeroCameraSignalBridge`. Room bounds are defined by `CameraBoundsVolume`, temporary hard locks by `CameraLockArea`, and soft framing offsets by `CameraOffsetArea`.

All tuning lives in `CameraConfig` SO at `Assets/_Project/ScriptableObjects/World/CameraConfig.asset`.

---

## UI / HUD

See `Docs/FeatureSpecs/HUD.md` for the full spec.

HUD and menus use UGUI. The persistent `_GameCameras` prefab already contains `HUDCamera`; the HUD Canvas is configured in Screen Space - Camera mode and assigned to that camera. This keeps UI layout independent of world camera settings and prevents z-fighting.

**Canvas hierarchy (vertical slice scope):**

```
_GameCameras (persistent)
└── HUDRoot (PersistentHudRoot)
    └── HUD Canvas (HUDCamera)
        ├── HealthDisplay   — direct PlayerHealthState.Changed subscriber
        └── ResourceDisplay — direct PlayerResourceState.Changed subscriber
└── Menus
    ├── PauseMenu         — shown/hidden by GameManager.Pause() / Unpause()
    └── [reserved slots]  — main menu, game-over screen; populated in later milestones
```

Key separation rules:
- **HUD subscribes to C# events; it never polls component fields.** `HealthDisplay` and `ResourceDisplay` subscribe directly to persistent state assets, refresh explicitly on enable, and do not rebind through `GameManager.SceneInit`. Gameplay-context events remain outside the basic value display.
- **Pause is owned by `GameManager`.** The pause menu calls `GameManager.Pause()` / `Unpause()`; it does not set `Time.timeScale` directly.
- **Save/load UI goes through `UIFlowController`.** No UI MonoBehaviour calls `SaveManager.Save()` or `LoadSceneAsync` directly.
- **Presentation remains separate from state.** Health uses dynamic slot views; resource uses a single always-visible horizontal fill bar (no orb/pip presentation). Both are simple UGUI elements; final artwork and optional menu overlays are Editor work and do not change gameplay ownership.

---

## Audio

See `Docs/FeatureSpecs/Audio.md` for the full spec.

`AudioManager` (MonoBehaviour, DontDestroyOnLoad, initialised in `Bootstrap`) is the global audio router for music, enemy/world/UI one-shots, and shared mix settings. Hero-owned action sounds are the one local exception: they are routed through `HeroAudioController` on the hero prefab.

**Vertical slice API:**

```csharp
AudioManager.Instance.PlaySFX(AudioClip clip)
AudioManager.Instance.PlaySFX(AudioClip clip, float pitchMin, float pitchMax, float volume = 1f)
AudioManager.Instance.PlayMusic(AudioClip clip, bool loop = true)
```

**Call sites:**
- Hero movement, hurt, death, footstep, and terrain-impact sounds call methods on `HeroAudioController`, which owns the `Hero/Sounds/*` child `AudioSource`s and is the only hero subsystem allowed to call `AudioSource.Play()` / `Stop()` directly.
- `HeroAttackModule` calls `AudioManager.PlaySFX(slashClip, pitchMin, pitchMax)` on activation — not `AudioSource.Play()`.
- Enemy, world, UI, and shared one-shots call `AudioManager.PlaySFX`.
- `GameManager.BeginSceneTransition` will call `PlayMusic` for the incoming scene's music clip — **deferred to Milestone 5**. No music routing through transitions is implemented yet.

**Do not** call `AudioSource.Play()` directly from actions, enemies, world objects, or UI. Hero-local source playback belongs only in `HeroAudioController`; all other audio routing goes through `AudioManager` so that volume settings, mix groups, and interrupt logic can be added without touching call sites.

---

## Planned Systems

Status and sequencing: `Docs/ImplementationPlan.md`.

| System | FeatureSpec | Milestone | Status |
|---|---|---|---|
| Camera | `Docs/FeatureSpecs/Camera.md` | 1 | Done |
| Scene Transitions | — (described in this doc) | 1 | Done |
| Enemy AI | `Docs/FeatureSpecs/EnemyAI.md` | 2 | Foundation validated; broader enemy roster planned |
| Abilities / Upgrades | `Docs/FeatureSpecs/Abilities.md` | 3 | Partial |
| Save / Load | `Docs/FeatureSpecs/SaveSystem.md` | Foundation + World Persistence Phase 1/2/3 done (M0/M4); slot UI in M5 | Partial |
| HUD / Menus | `Docs/FeatureSpecs/HUD.md` | 6 | Presentation foundation implemented; Editor wiring pending |
| Audio | `Docs/FeatureSpecs/Audio.md` | 5 | Partial |
