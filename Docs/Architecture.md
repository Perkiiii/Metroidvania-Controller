# Architecture — Metroidvania Controller

**Last audited:** 2026-06-05

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
Core baseline tuning at `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset`. Contains shared controller parameters: walk/run speeds, base jump, gravity, attack, downslash pogo, sensor probes, health/hurt, and animation fade durations. All subsystems receive a reference at initialization.

### HeroAbilityConfig (ScriptableObject)
Gated traversal ability tuning at `Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset`. Holds numeric parameters for dash, wall-slide, wall-jump, and double-jump. Wired into `HeroController` via a serialized Inspector field alongside `HeroConfig`. Absent from core movement logic — actions and the motor use it only for the ability-specific behaviours it governs.

- **HeroConfig** = core baseline movement and combat config (always required)
- **HeroAbilityConfig** = gated traversal ability tuning (dash, wall-slide, wall-jump, double-jump); abilities are disabled gracefully if missing
- **PlayerAbilityState** = unlock flags only; no tuning values; wired via Inspector

### HeroStateBlackboard (MonoBehaviour)
Single source of truth for the hero's runtime state. Written by Sensors, Motor, and Action classes; read by everything else, including AnimationController. Keeps subsystems decoupled — no direct references between Motor and ActionController, for example.

### HeroAnimationLibrary (ScriptableObject)
Maps logical animation names (idle, walk, run, jump, fall, dash, wallSlide, attackSide, attackUp, attackDown) to `AnimationClip` references. Swapping a clip does not require code changes.

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
```

Attack direction is determined at swing start from the vertical component of `MoveVector` against `HeroConfig.attackDirectionThreshold`. Hit detection runs every `FixedUpdate` during the active window using `PolygonCollider2D.Overlap`.

Hit receivers are tracked per-swing in a `HashSet` to prevent multi-hit on the same target in one swing.

---

## Hit-Reaction Interfaces

| Interface | When called |
|---|---|
| `IHeroAttackReceiver` | Collider overlaps the damage collider |
| `IHeroAttackClashReceiver` | Collider overlaps the clash collider |
| `IHeroDownslashResponder` | Damage hit occurs and direction is Down |

Implement on any MonoBehaviour in the hit object's hierarchy. `HeroAttackAction` walks up the parent chain to find them.

---

## Animation

`HeroAnimationController` drives Animancer directly — no Animator parameters. Locomotion uses a `LinearMixerState` keyed on horizontal speed (idle → walk → run thresholds from `HeroConfig`). Action states (attack, dash, wall-slide, jump, fall) are played as one-shots with configurable fade durations.

Attack animation completion is signalled back to `HeroAttackAction.CompleteAttackFromAnimation` via an Animancer end-event. A fail-safe timer forces the attack to end if the event does not fire within the expected duration.

---

## Sensor System

`HeroSensors` uses three-point raycasts (left, center, right / bottom, center, top) against `HeroConfig.terrainLayers`. Results are written to the blackboard each `FixedUpdate` before Motor runs. Probe distances and edge inset are tunable in `HeroConfig`.

---

## Control Lock System

`HeroController` exposes `AddControlLock(object)` / `RemoveControlLock(object)`. Any system can suppress player input by registering a lock token. The lock set is reference-counted; `blackboard.controlLocked` is true whenever any lock is held.

---

## Hero Health, Hurt, Death, and Respawn

`HeroHealthComponent` (MonoBehaviour) owns the player-side damage intake. `HeroController` receives a reference at init; it does not own any health fields directly.

```
HeroController
└── HeroHealthComponent
    ├── maxHealth, currentHealth    (tuned in HeroConfig)
    ├── IsInvincible                (reference-counted by source object, not a raw timer)
    ├── OnHealthChanged (event)     → neutral UI/state notification
    ├── OnDamaged (event)           → hurt animation, knockback, i-frames
    ├── OnHazardDamaged (event)     → hazard-only flash, audio, shake, hurt pose
    └── OnDeath   (event)           → death sequence
```

**Normal damage flow:**
1. An enemy attack calls `HeroHealthComponent.TakeDamage(amount, iFrameSource)`.
2. If not invincible: subtract health, grant i-frames keyed to `iFrameSource`, fire `OnDamaged`.
3. `HeroController` subscribes to `OnDamaged` → writes `HeroActorState.Hurt` to the blackboard, applies knockback via `HeroMotor`, adds a control lock for the stun duration.
4. If health ≤ 0: fire `OnDeath`; transition to `HeroActorState.Dead` and begin respawn.

**Hazards:** `HazardZone` supports `InstantDeath` and `RecoverLocal`. Instant-kill hazards call `HeroHealthComponent.TriggerHazardDeath()`. Recoverable hazards call `HeroHealthComponent.TakeHazardDamage()`, which ignores normal combat i-frames, fires `OnHealthChanged`, skips `OnDamaged`, and only fires `OnDeath` if health reaches zero. Nonfatal recoverable hazards fire `OnHazardDamaged` for feedback, then pass a `HazardContact` to `GameManager.BeginHazardRecoverySequence()` for an impact delay, fade, local reposition, camera snap, and hero reset without restoring health. `HeroBox.HandleHazard` intentionally discards any buffered normal/contact damage before processing the hazard — hazards take priority over same-step enemy hits buffered for `FixedUpdate`. `BeginHazardRecoverySequence` immediately grants temporary invincibility so enemy contact damage cannot reach the hero through a `FixedUpdate` flush during the recovery window. Hazard recovery tuning (impact delay, black-screen hold, fade durations, i-frame duration) lives in `HazardRecoveryProfile` (SO), referenced by `HazardZone` and carried in `HazardContact` — `GameManager` is a sequence coordinator, not a tuning database. Key defaults: `ImpactDelay` 0.18 s (recommended 0.18–0.20 s), `BlackScreenHold` 0.1 s, `RecoveryIFrameDuration` 0.75 s, `FadeOutDuration` −1 (use camera default), `FadeInDuration` −1 (use camera default); if no profile is assigned the code falls back to these values. Set `FadeInDuration` to 0.35–0.45 s on a profile for a snappier local recovery feel relative to the longer scene-transition fade-in. Create a shared `HazardRecoveryProfile.asset` under `Assets/_Project/ScriptableObjects/World/` and assign it to each recoverable `HazardZone`. `GameManager` caches the hero's post-placement position on every scene load (`_sceneFallbackPosition`) as a last-resort fallback if no `RespawnMarker` or `HazardRespawnMarker` is found; scenes with recoverable hazards should always author at least one `RespawnMarker`.

**Respawn markers:**
- `RespawnMarker` — scene object placed at save points. Set as the active normal-death respawn point **only when a checkpoint is activated** (`CheckpointInteractable.Interact`). Crossing a `TransitionPoint` does **not** update the active respawn marker in the current pass — death after a gate crossing returns the hero to the last activated checkpoint, not to the door (see Scene Transitions for the deferred `linkedRespawnMarker` field).
- `HazardRespawnMarker` — scene object placed near recoverable hazards. `HazardZone` can reference one directly as its local recovery point. If unassigned, `GameManager` falls back to the nearest `RespawnMarker`, then the cached scene entry position. Author at least one `RespawnMarker` per scene that contains recoverable hazards. Trigger-updated active hazard markers are deferred; if added later, the live active pointer belongs on `GameManager`, not `HeroController` or `SaveManager`.

---

## Ability Unlock State

Gated abilities are controlled by a `PlayerAbilityState` ScriptableObject at `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset`. Each action class checks the relevant flag in its `CanStart` condition. Ability gate checks live in the individual action classes — never in `HeroController` or `HeroStateBlackboard`.

```
AbilityId (enum)
  Dash, WallCling, Sprint, WallLatch, DoubleJump, DriftCloak, SpiritCast

PlayerAbilityState (SO)
├── dashUnlocked        bool  (default true — covers ground and air dash; no separate flags)
├── wallClingUnlocked   bool  (default true — shared gate for wall-slide and wall-jump; no separate flags)
├── sprintUnlocked      bool  (default false)
├── wallLatchUnlocked   bool  (default false)
├── doubleJumpUnlocked  bool  (default false)
├── driftCloakUnlocked  bool  (default false)
├── spiritCastUnlocked  bool  (default false)
└── AbilityChanged event — fired by SetUnlocked only when value changes; scene gates subscribe for runtime changes
```

`PlayerAbilityState` is separate from `HeroConfig` (tuning values) and from the save data class (`AbilitySaveData`). It implements `ISaveTarget`: `GatherSaveData` copies the 7 flags into `AbilitySaveData`; `ApplySaveData` calls `SetUnlocked(AbilityId, bool)` for each flag so runtime subscribers receive `AbilityChanged` events when values change. Scene `AbilityGate` objects match already-loaded state because they call `Refresh()` in `OnEnable`. `SaveManager` holds a serialized reference to the asset and calls both methods at the correct points in the save/load lifecycle.

`AbilityPickup` (MonoBehaviour) calls `abilityState.Unlock(ability)` on hero trigger contact. `AbilityGate` (MonoBehaviour) refreshes on enable, then subscribes to `AbilityChanged` and enables/disables a blocker object or collider reactively.

`PlayerAbilityState` is wired into `HeroController` via a serialized Inspector field; `HeroActionController.Initialize` passes it to the action constructors. No `AssetDatabase` lookup is used.

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

`EnemyHealthComponent` is the only class in the project that implements `IHeroAttackReceiver`. When called, it subtracts damage, plays hit feedback, and delegates hit reaction to `EnemyRecoil`. `EnemyRecoil` applies the knockback or freeze response from `hit.ForceDirection`, exposes its `Ready` / `Frozen` / `Recoiling` state for debugging, and owns the `hurt` / `recoiling` blackboard flags until stun recovery ends. `DamageHero` marks a collider as capable of hurting the hero and stores shared damage metadata. Behaviour-specific scripts such as `EnemyContactDamage` decide when and how that damage is applied, including cooldown and knockback policy. Enemies do not reference `HeroController` or any hero subsystem; they may read the hero's `Transform` for detection targeting.

See `Docs/FeatureSpecs/EnemyAI.md` for the full state machine spec and config schema.

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

**Current deviations (tech debt):** `GameManager` also implements `HitStop(float duration)` (used by `HeroAttackAction` on hit-connect), `BeginRespawnSequence()` (normal-death respawn, including cross-scene checkpoint reload), and `BeginHazardRecoverySequence()` (same-scene local hazard recovery). `BeginRespawnSequence` calls `_heroHealth.RestoreFullHealth()` directly — a temporary coupling that will be replaced when a `PlayerHealthState` ScriptableObject (implementing `ISaveTarget`) owns current health and restores it via `ApplySaveData`. Additionally, `GameManager` now owns `ResolveActiveRespawnMarkerFromSave()` and `PlaceHeroAtSavedRespawnIfRequested()` — these are well-defined seams to the save system, not business logic, and are considered acceptable for the current architecture stage. See `Docs/ImplementationPlan.md` Known Technical Debt.

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
       — ApplySaveData() → PlayerAbilityState flags applied via ISaveTarget
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
  └── WorldStateRegistry.asset       room flags, defeated enemies, open doors  [planned]
```

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

HUD and menus run on a dedicated Canvas with a separate `UICamera` (orthographic, Clear Flags: Depth Only) that renders on top of the gameplay camera. This keeps UI layout independent of world camera settings and prevents z-fighting.

**Canvas hierarchy (vertical slice scope):**

```
UIRoot (Canvas, UICamera)
├── HUD
│   ├── HealthDisplay     — subscribes to HeroHealthComponent.OnHealthChanged / OnDeath
│   └── [reserved slots]  — ability indicators, resource bars; empty GameObjects, filled later
└── Menus
    ├── PauseMenu         — shown/hidden by GameManager.Pause() / Unpause()
    └── [reserved slots]  — main menu, game-over screen; populated in later milestones
```

Key separation rules:
- **HUD subscribes to C# events; it never polls component fields.** `HealthDisplay` subscribes to `HeroHealthComponent.OnHealthChanged` and `OnDeath` at scene init via `GameManager.SceneInit`. It must not call `GetComponent<HeroHealthComponent>()` per-frame or hold a direct MonoBehaviour reference to query each frame.
- **Pause is owned by `GameManager`.** The pause menu calls `GameManager.Pause()` / `Unpause()`; it does not set `Time.timeScale` directly.
- **Save/load UI goes through `UIFlowController`.** No UI MonoBehaviour calls `SaveManager.Save()` or `LoadSceneAsync` directly.
- **Reserve slots, populate later.** Author the full Canvas hierarchy in the first HUD pass; leave placeholder GameObjects for elements not yet implemented. Adding new HUD elements later must not require structural Canvas changes.

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
| Save / Load | `Docs/FeatureSpecs/SaveSystem.md` | Foundation done (M0); world-state and UI in M4–5 | Partial |
| HUD / Menus | `Docs/FeatureSpecs/HUD.md` | 5 | Not started |
| Audio | `Docs/FeatureSpecs/Audio.md` | 5 | Partial |
