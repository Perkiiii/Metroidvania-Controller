# Architecture — Metroidvania Controller

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
│   └── HeroWallSlideAction (plain C# class)
└── HeroAnimationController  Animancer playback driven by blackboard state
```

### Update order

| Loop | Responsibilities |
|---|---|
| `Update` | `HeroInputReader.Tick` → `HeroActionController.Tick` (timers, attack/dash intent) |
| `FixedUpdate` | `HeroSensors.FixedTick` → `HeroActionController.FixedTick` → `HeroMotor.FixedTick` |
| `LateUpdate` | `HeroAnimationController.TickVisuals` |

---

## Key Data Types

### HeroConfig (ScriptableObject)
Central tuning asset at `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset`. Contains all numeric parameters for movement, jump, gravity, dash, attack, wall-slide, sensors, and animation fade durations. All subsystems receive a reference at initialization.

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
    - AudioClip          slashSfx    (played via AudioManager.PlaySFX on activation, not AudioSource.Play)
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
    ├── OnDamaged (event)           → hurt animation, knockback, i-frames
    └── OnDeath   (event)           → death sequence
```

**Normal damage flow:**
1. An enemy attack or hazard calls `HeroHealthComponent.TakeDamage(amount, iFrameSource)`.
2. If not invincible: subtract health, grant i-frames keyed to `iFrameSource`, fire `OnDamaged`.
3. `HeroController` subscribes to `OnDamaged` → writes `HeroActorState.Hurt` to the blackboard, applies knockback via `HeroMotor`, adds a control lock for the stun duration.
4. If health ≤ 0: fire `OnDeath`; transition to `HeroActorState.Dead` and begin respawn.

**Hazard death:** instant-kill hazards (pits, kill zones) call `HeroHealthComponent.TriggerHazardDeath()`, which fires `OnDeath` immediately regardless of current health or invincibility.

**Respawn markers:**
- `RespawnMarker` — scene object placed at save points and room entries. Set as the active normal-death respawn point when a checkpoint is activated or when a room with a default marker is entered.
- `HazardRespawnMarker` — scene object placed near pits or kill zones. `HazardZone` specifies which marker to use. The active pointer is stored on `SaveManager`, not on `HeroController`.

---

## Ability Unlock State

Gated abilities are controlled by a `PlayerAbilityState` ScriptableObject at `Assets/_Project/ScriptableObjects/World/PlayerAbilityState.asset`. Each action class checks the relevant flag in its `CanStart` condition:

```
PlayerAbilityState (SO)
├── wallJumpUnlocked   bool
├── wallLatchUnlocked  bool
├── spiritCastUnlocked bool
└── ...
```

`PlayerAbilityState` is separate from `HeroConfig` (tuning values) and from the save data class (`AbilitySaveData`). At load time, `SaveManager` calls `ApplySaveData` on the SO to restore unlock flags. Always-available abilities (dash, wall-slide in the current build) default their flags to `true` so they can be gated later if scope changes.

See `Docs/FeatureSpecs/Abilities.md` for the full per-ability spec.

---

## Enemy Architecture

Each enemy is a self-contained scene object. The structure mirrors the hero's blackboard-and-action pattern but is simpler:

```
EnemyController (MonoBehaviour — coordinator)
├── EnemyConfig (SO)          movement speed, detection range, attack data, health
├── EnemyStateBlackboard      alerted, attacking, hurt, recoiling, dead flags
├── EnemyMotor                Rigidbody2D velocity control
├── EnemyPerception           overlap / raycast detection; writes to blackboard
├── EnemyBehaviour            C# state machine: Idle → Patrol → Chase → Attack → Hurt → Dead
├── EnemyHealthComponent      implements IHeroAttackReceiver (and IHeroDownslashResponder if applicable)
├── EnemyRecoil               hit freeze / knockback / stun recovery
├── DamageHero                data marker for a collider that can hurt the hero
└── EnemyContactDamage        persistent body-touch damage behaviour
```

`EnemyHealthComponent` is the only class in the project that implements `IHeroAttackReceiver`. When called, it subtracts damage, plays hit feedback, and delegates hit reaction to `EnemyRecoil`. `EnemyRecoil` applies the knockback or freeze response from `hit.ForceDirection`, exposes its `Ready` / `Frozen` / `Recoiling` state for debugging, and owns the `hurt` / `recoiling` blackboard flags until stun recovery ends. `DamageHero` marks a collider as capable of hurting the hero and stores shared damage metadata. Behaviour-specific scripts such as `EnemyContactDamage` decide when and how that damage is applied, including cooldown and knockback policy. Enemies do not reference `HeroController` or any hero subsystem; they may read the hero's `Transform` for detection targeting.

See `Docs/FeatureSpecs/EnemyAI.md` for the full state machine spec and config schema.

---

## Interactable System

All world objects the player can interact with (checkpoints, NPCs, doors that require a button press, item pickups) extend `InteractableBase`:

```
InteractableBase (MonoBehaviour)
├── Priority   int     — higher value wins when multiple are in range
├── IsDisabled bool    — deactivated interactables are skipped by the manager
└── OnInteract()       — called by InteractManager when interact input is confirmed
```

`InteractManager` (DontDestroyOnLoad singleton):
- Maintains a priority-sorted list of `InteractableBase` instances in the hero's trigger range.
- Shows / hides the interact prompt UI based on whether any valid interactable is active.
- On interact input: calls `OnInteract()` on the highest-priority enabled interactable.

Registration: `InteractableBase.OnTriggerEnter2D` registers; `OnTriggerExit2D` deregisters. Do not poll input or manage proximity inside individual interactable MonoBehaviours.

---

## Checkpoint and Respawn Markers

**CheckpointInteractable** (extends `InteractableBase`):
- `OnInteract()`: calls `SaveManager.Save()`, sets `SaveManager.ActiveRespawnMarker` to this checkpoint's linked `RespawnMarker`, plays an activation effect.
- Does not call any method on `HeroController` or `HeroHealthComponent`.

**HazardZone** (MonoBehaviour on a trigger collider):
- `OnTriggerEnter2D` with the hero: calls `HeroHealthComponent.TriggerHazardDeath()` and sets `SaveManager.ActiveHazardRespawnMarker` to the zone's linked `HazardRespawnMarker`.

**TransitionPoint also sets the normal-death respawn marker on entry.** Before initiating the load, `TransitionPoint` sets `SaveManager.ActiveRespawnMarker` to its linked `RespawnMarker`. If the player dies immediately after crossing into a new room, they respawn at the door they just used rather than the last checkpoint in the previous room. The `ActiveRespawnMarker` (normal death) and `ActiveHazardRespawnMarker` (hazard death) are fully independent — both can be set simultaneously without conflict.

Respawn marker string keys are what the save file persists. Each marker registers itself by key on scene load; `SaveManager` resolves the key to the live object after `SceneInit`.

---

## Scene Transitions and Loading

**TransitionPoint** (MonoBehaviour, extends `InteractableBase`):
- Fields: `targetScene` (string), `isADoor` (bool), `entryMarkerTag` (string), linked `HazardRespawnMarker`.
- On activation: calls `GameManager.BeginSceneTransition(targetScene, entryMarkerTag)`. Never calls `SceneManager.LoadSceneAsync` directly.
- If `isADoor` is true, activation requires the interact input; otherwise a trigger `OnTriggerEnter2D` fires automatically.

**GameManager** (MonoBehaviour, DontDestroyOnLoad) — four responsibilities only. Do not add to these without a documented architectural reason:

1. **`GameState` enum** — `Playing`, `Paused`, `EnteringLevel`, `ExitingLevel`, `Loading`. Written only by GameManager methods, never from outside.
2. **`SceneInit` event** — fired after every scene load, before the fade-in. Scene-local systems subscribe here rather than relying on `Awake` ordering.
3. **`BeginSceneTransition(targetScene, entryTag)`** — add control lock → screen fade out → `LoadSceneAsync` → fire `SceneInit` → position hero at entry marker → screen fade in → remove control lock.
4. **`Pause()` / `Unpause()`** — set `GameState.Paused`, add hero control lock, set `Time.timeScale = 0`. Unpause reverses all three in order. Nothing else in the project touches `Time.timeScale`.

GameManager must not own health, enemies, progression state, UI layout, or save logic.

---

## Boot / Startup

Build Index 0 is a dedicated boot scene containing only a `Bootstrap` MonoBehaviour. It initialises all persistent singletons in order, then loads the main menu:

```
Bootstrap (MonoBehaviour in Boot scene, Build Index 0)
  1. Instantiate and DontDestroyOnLoad: GameManager, SaveManager, AudioManager, InteractManager
  2. SaveManager.LoadOrCreate()     — load save file; create fresh state if file is missing or corrupt
  3. GameManager.LoadScene("MainMenu")
```

Persistent singletons live only in the boot scene and carry across all subsequent loads via `DontDestroyOnLoad`. They must not be placed in gameplay or UI scenes.

---

## Save / Persistence

See `Docs/FeatureSpecs/SaveSystem.md` for the full spec.

**Structural boundary:**

```
SaveManager (DontDestroyOnLoad)
  ↓  CollectSaveData() / ApplySaveData()
  ↓
ISaveTarget (interface, implemented by SOs)
  ├── PlayerAbilityState.asset       unlock flags
  ├── WorldStateRegistry.asset       room flags, defeated enemies, open doors (planned)
  └── (additional domain SOs as needed)
```

`SaveManager` must not reference any MonoBehaviour at save or load time. All live state that needs persisting must be mirrored into a registered SO implementing `ISaveTarget`.

**Save triggers:** only `CheckpointInteractable.OnInteract` and the application quit handler on `SaveManager` itself call `SaveManager.Save()`. Never call it from inside a hero or enemy MonoBehaviour.

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
│   ├── HealthDisplay     — subscribes to HeroHealthComponent.OnDamaged / OnDeath
│   └── [reserved slots]  — ability indicators, resource bars; empty GameObjects, filled later
└── Menus
    ├── PauseMenu         — shown/hidden by GameManager.Pause() / Unpause()
    └── [reserved slots]  — main menu, game-over screen; populated in later milestones
```

Key separation rules:
- **HUD subscribes to C# events; it never polls component fields.** `HealthDisplay` subscribes to `HeroHealthComponent.OnDamaged` and `OnDeath` at scene init via `GameManager.SceneInit`. It must not call `GetComponent<HeroHealthComponent>()` per-frame or hold a direct MonoBehaviour reference to query each frame.
- **Pause is owned by `GameManager`.** The pause menu calls `GameManager.Pause()` / `Unpause()`; it does not set `Time.timeScale` directly.
- **Save/load UI goes through `UIFlowController`.** No UI MonoBehaviour calls `SaveManager.Save()` or `LoadSceneAsync` directly.
- **Reserve slots, populate later.** Author the full Canvas hierarchy in the first HUD pass; leave placeholder GameObjects for elements not yet implemented. Adding new HUD elements later must not require structural Canvas changes.

---

## Audio

See `Docs/FeatureSpecs/Audio.md` for the full spec.

`AudioManager` (MonoBehaviour, DontDestroyOnLoad, initialised in `Bootstrap`) is the single point of contact for all audio output.

**Vertical slice API:**

```csharp
AudioManager.Instance.PlaySFX(AudioClip clip)
AudioManager.Instance.PlayMusic(AudioClip clip, bool loop = true)
```

**Call sites:**
- Hero and enemy action classes call `PlaySFX` for action sounds (jump, dash, attack, hurt). The clip is stored as a field on the relevant SO or module and passed at call time.
- `HeroAttackModule` calls `AudioManager.PlaySFX(slashSfx)` on activation — not `AudioSource.Play()`.
- `GameManager.BeginSceneTransition` calls `PlayMusic` for the incoming scene's music clip.

**Do not** call `AudioSource.Play()` directly on hero or enemy prefabs, or manage music from any gameplay MonoBehaviour. All audio routing goes through `AudioManager` so that volume settings, mix groups, and interrupt logic can be added without touching call sites.

---

## Planned Systems

Status and sequencing: `Docs/ImplementationPlan.md`.

| System | FeatureSpec | Milestone |
|---|---|---|
| Camera | `Docs/FeatureSpecs/Camera.md` | 1 |
| Enemy AI | `Docs/FeatureSpecs/EnemyAI.md` | 2 |
| Abilities / Upgrades | `Docs/FeatureSpecs/Abilities.md` | 3 |
| Save / Load | `Docs/FeatureSpecs/SaveSystem.md` | 4 |
| HUD / Menus | `Docs/FeatureSpecs/HUD.md` | 5 |
| Audio | `Docs/FeatureSpecs/Audio.md` | 5 |
