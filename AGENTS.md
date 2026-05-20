# AGENTS.md — Metroidvania Controller

## Project Overview

2.5D side-scrolling metroidvania in Unity. Physics and movement are fully 2D (Rigidbody2D / Physics2D); visuals use layered sprites under URP.

**Source of truth:**
- High-level architecture and ownership: `Docs/Architecture.md`
- Implementation status and sequencing: `Docs/ImplementationPlan.md`
- Feature-level rules and contracts: `Docs/FeatureSpecs/<System>.md`
- Validated hero feel values and regression checklist: `Docs/HeroFeelTuning.md`

Before editing any system, read its relevant doc. `Docs/Architecture.md` and `Docs/ImplementationPlan.md`
are the high-level source of truth for ownership boundaries and what is or is not implemented.

---

## Tech Stack

| Concern | Solution |
|---|---|
| Engine | Unity, URP |
| Physics | 2D — Rigidbody2D, Collider2D, Physics2D raycasts |
| Input | Unity Input System 1.x — `Assets/_Project/Input/InputSystem_Actions.inputactions` |
| Animation | Animancer — clips driven in code; no Animator parameters at runtime |
| Core tuning | `HeroConfig` SO — movement baselines, combat, gravity, sensor probes, health/hurt |
| Ability tuning | `HeroAbilityConfig` SO — dash, wall-slide, wall-jump, double-jump |
| Unlock flags | `PlayerAbilityState` SO — ability unlock booleans only; no tuning values |
| Inspector | Odin Inspector (available; use where it reduces boilerplate, not by default) |
| Language | C# |

---

## Folder Structure

```
Assets/_Project/
  Scripts/Hero/
    Core/         HeroStateBlackboard, HeroConfig, HeroAbilityConfig, HeroActorState,
                  AbilityId, PlayerAbilityState
    Actions/      HeroActionController + plain-C# action objects
    Animation/    HeroAnimationController, HeroAnimationLibrary, HeroCameraSignalBridge
    Audio/        HeroAudioController
    Combat/       HeroAttackModule, HeroAttackHit, hit-reaction interfaces
    Input/        HeroInputReader
    Movement/     HeroMotor
    Sensors/      HeroSensors, HeroBox
  Scripts/Enemy/  EnemyController, EnemyConfig, EnemyStateBlackboard, EnemyHealthComponent,
                  EnemyRecoil, EnemyFeedbackController, DamageHero, EnemyContactDamage
  Scripts/World/  GameManager, Bootstrap, InteractManager, CheckpointInteractable,
                  AbilityPickup, AbilityGate, RespawnMarker, HazardZone,
                  HazardRespawnMarker, HazardRecoveryProfile, TransitionPoint (planned),
                  CameraController and related camera components
  Scripts/Save/   SaveManager, ISaveTarget, save data classes, SaveSerializer,
                  SaveFileStore, SaveDataMigrator, SaveStats
  Scripts/UI/     HUD components, menus, UIFlowController
  ScriptableObjects/Hero/   HeroConfig.asset, HeroAnimationLibrary.asset,
                            HeroAbilityConfig.asset, PlayerAbilityState.asset
  ScriptableObjects/Enemy/  EnemyConfig assets
  ScriptableObjects/World/  CameraConfig.asset, HazardRecoveryProfile.asset,
                            WorldStateRegistry.asset (planned)
  Input/          InputSystem_Actions.inputactions
  Animations/     .anim clips
Assets/Plugins/    Sirenix / Odin — do not modify
Assets/ThirdParty/ third-party assets — do not modify
Docs/              architecture and feature documentation
```

New scripts go under `Assets/_Project/Scripts/<SystemName>/`. Do not place scripts at the `Assets/`
root or inside `Assets/Plugins/`.

---

## Agent Workflow

- **Before a risky or multi-file change, write a short plan** and confirm the approach before
  making edits.
- **Prefer minimal, targeted edits.** Do not rewrite working systems unless explicitly asked.
- **Do not implement planned systems** just because they appear in `Docs/ImplementationPlan.md` —
  implement only what the task requests.
- **Verify implementation status against actual code** before making claims. Docs describe the
  intended architecture; what exists in `Scripts/` is the ground truth.
- **Before creating a new system, search for an existing equivalent.** Check
  `Docs/ImplementationPlan.md` for planned systems and `Scripts/` for existing implementations.
- Prefer a small, working first-pass over copying a complex reference design verbatim.
- Preserve existing naming, folder structure, namespaces, and access modifiers in code you
  are not directly changing.
- At the end of every task, **list which files changed** and **list required manual Unity
  Editor steps** (see Reporting Manual Unity Work).
- **Do not claim Unity tests passed unless they were actually run in the Editor.**

---

## Architecture Rules

### Hero Subsystem Rules

- **`HeroController` is a thin coordinator.** Logic lives in subsystems; `HeroController` owns
  nothing except its config references and the wiring of subsystems.
- **`HeroMotor` is the sole authority for `Rigidbody2D` velocity writes.** Do not write
  `rb.linearVelocity` from actions or any other class. Read velocity from `blackboard.velocity`
  or `HeroMotor.Velocity`.
- **`HeroStateBlackboard` is the runtime state bus.** Subsystems share state through the
  blackboard; do not pass state between subsystems by direct reference.
- **`HeroController.InitializeSystems` is the wiring point.** New subsystems receive dependencies
  there via `Initialize(config, blackboard, ...)`. Avoid `GetComponent` in subsystem `Awake`/`Start`;
  never use `FindObjectOfType` or static fields for runtime coordination.
- **Animancer only — no Animator parameters.** Do not add parameters to the Animator controller
  or call `Animator.SetBool` / `SetFloat` / `SetTrigger`.
- **Action objects are plain C# classes, not MonoBehaviours.** Instantiated in
  `HeroActionController.Initialize`; ticked via `Tick` / `FixedTick`.
- **`HeroBox` is the hurtbox.** It lives on the `Hero/Herobox` child and routes enemy contact
  damage, instant-death hazards, and recoverable local hazard damage. `HeroHealthComponent`
  stays on the Hero root.
- **`HeroInputReader` is accessed only from `HeroActionController` and the action objects.**

See `Docs/FeatureSpecs/PlayerController.md`.

### Ability System Rules

- **`PlayerAbilityState` owns unlock flags only.** No tuning values, no runtime state.
- **`HeroAbilityConfig` owns gated traversal ability tuning** (dash, wall-slide, wall-jump,
  double-jump). **`HeroConfig` owns core baseline tuning** (movement, combat, gravity,
  health/hurt). Do not mix tuning between them.
- **Ability gate checks live in action classes.** Each action checks the relevant
  `PlayerAbilityState` flag in its `CanStart` condition. Never gate abilities inside
  `HeroController` or `HeroStateBlackboard`.
- Adding a new ability: add a flag to `PlayerAbilityState`, add tuning to `HeroAbilityConfig`,
  create a `Hero<Name>Action` plain C# class, instantiate it in `HeroActionController.Initialize`.

See `Docs/FeatureSpecs/Abilities.md`.

### Combat Rules

- **Hit delivery uses established interfaces.** `IHeroAttackReceiver`, `IHeroAttackClashReceiver`,
  and `IHeroDownslashResponder` are the contracts. New contact types get a new interface;
  do not add special-case receiver logic inside `HeroAttackAction`.
- **`HeroAttackAction` owns global attack-connect feel.** On first confirmed hit or clash per
  swing: hit-stop via `GameManager.HitStop`, camera shake via `CameraEventService`, and
  `HeroAttackImpactFeedbackController`. Enemy implementations of `IHeroAttackReceiver` must
  not trigger hit-stop or camera shake.
- **Enemies own their local feedback** — damage subtraction, hit flash, hurt/death audio,
  recoil, `EnemyFeedbackController` MMF players, and death handling.
- **Hitbox control stays in `HeroAttackAction`.** Enable/disable `PolygonCollider2D` on attack
  modules from there only.

See `Docs/FeatureSpecs/Combat.md`.

### Camera Rules

- **Camera systems may read the hero `Transform` only.** They must not reference `HeroController`,
  `HeroStateBlackboard`, or any other hero subsystem.
- **`HeroCameraSignalBridge`** is the one-way hero→camera adapter. It reads hero/input state and
  sends signals (facing, dash, look) into `GameCameras`. Use it for all hero-originated camera
  communication.
- Room bounds: `CameraBoundsVolume`. Temporary hard locks: `CameraLockArea`. Soft framing:
  `CameraOffsetArea`. All tuning in `CameraConfig.asset`.

See `Docs/FeatureSpecs/Camera.md`.

### Audio Rules

- **Hero movement, hurt, death, footstep, and terrain-impact sounds go through `HeroAudioController`.**
  It is the only component allowed to call `AudioSource.Play()` / `Stop()` on `Hero/Sounds/*`
  sources. Action classes call named methods on it (e.g. `PlayJump`, `PlayDash`).
- **`HeroAttackModule` calls `AudioManager.PlaySFX`** on activation — not `HeroAudioController`
  and not `AudioSource.Play()` directly.
- **All other audio** (enemy, world, UI, music) goes through `AudioManager.PlaySFX` or `PlayMusic`.
- **Music is `GameManager`'s responsibility** via `BeginSceneTransition`. Do not call `PlayMusic`
  from `Awake`, `Start`, or scene-init handlers in gameplay scenes.
- **Time control belongs to `GameManager`.** Do not set `Time.timeScale` outside `GameManager`
  flows such as pause, hit-stop, transitions, respawn, or hazard recovery.

See `Docs/FeatureSpecs/Audio.md`.

### Save System Rules

- **`SaveManager` must not reference gameplay MonoBehaviours** — no `HeroController`,
  `HeroStateBlackboard`, `Rigidbody2D`, or enemy classes at save or load time.
- **All save-eligible state is owned by a ScriptableObject implementing `ISaveTarget`.**
  Scene-object identity is stored as string keys, never as `UnityEngine.Object` references.
- **Only the checkpoint interaction flow and `SaveManager.SaveOnQuit` call `Save()`.**
  Never from hero or enemy MonoBehaviours.

See `Docs/FeatureSpecs/SaveSystem.md`.

### Scene, World, and Manager Rules

- **Scene transitions go through `GameManager.BeginSceneTransition`.** `TransitionPoint` calls
  it; it must never call `LoadSceneAsync` directly.
- **`AddControlLock` / `RemoveControlLock` is the only approved input-suppression channel.**
  Do not write `blackboard.controlLocked` directly from world or UI systems.
- **`GameManager` has four core responsibilities:** `GameState` enum, `SceneInit` event,
  `BeginSceneTransition`, `Pause`/`Unpause`. Note: `HitStop`, `BeginRespawnSequence`, and
  `BeginHazardRecoverySequence` are known tech debt; see `Docs/ImplementationPlan.md`. Do not
  add further concerns without documenting the architectural reason.
- **All interactable world objects extend `InteractableBase` and register with `InteractManager`.**
  Do not write bespoke proximity detection or input polling inside individual world objects.
- **Persistent singletons exist only in the boot scene,** created by `Bootstrap` and carried via
  `DontDestroyOnLoad`. Do not place them in gameplay or UI scenes.
- **HUD subscribes to C# events; it never polls component fields.** Health UI should subscribe
  to `HeroHealthComponent` health/death events at `SceneInit`.

See `Docs/FeatureSpecs/HUD.md`.

### Enemy Rules

- **Enemy structure mirrors the hero pattern:** `EnemyController` coordinator,
  `EnemyStateBlackboard`, `EnemyHealthComponent` implementing `IHeroAttackReceiver`, config in
  `EnemyConfig` SO.
- **Do not use C# inheritance for enemy variants** — prefer composition and distinct config assets.
- **Enemies must not reference `HeroController` or any hero subsystem.** They may read the hero
  `Transform` for detection targeting.

See `Docs/FeatureSpecs/EnemyAI.md`.

---

## Tuning Safety

- **Do not casually change validated hero feel values.** Core movement and combat values are
  recorded in `Docs/HeroFeelTuning.md` with notes on load-bearing interactions.
- If a tuning change is made and kept, **run the regression checklist in `Docs/HeroFeelTuning.md`**
  and update the recorded values before committing.
- `HeroConfig` owns core baseline tuning. `HeroAbilityConfig` owns gated ability tuning. Do not
  move values between them without updating both assets and both docs.

---

## Coding Standards

- **All gameplay numbers live in ScriptableObjects.** Use `HeroConfig` first; create a new SO only
  when the concern is clearly distinct.
- **No `GetComponent` in hot paths.** Resolve in `Initialize` or `Awake`; never per-frame.
- **Avoid `FindObjectOfType`, `GameObject.Find`, `FindObjectsOfType`.** Use injected references.
- **`UnityEditor` APIs are editor-only.** Wrap in `#if UNITY_EDITOR`.
- **Comments explain *why*, not *what*.** Add one only when the reasoning is non-obvious.

**Naming:**

| Type | Convention | Example |
|---|---|---|
| MonoBehaviour / plain class | PascalCase | `HeroMotor.cs` |
| Interface | `I` prefix | `IHeroAttackReceiver.cs` |
| Enum | PascalCase | `HeroActorState.cs` |
| SO asset file | PascalCase | `HeroConfig.asset` |
| Animation clip | SystemClipName | `HeroIdle.anim` |

---

## Scope Preservation

- Do not refactor surrounding code unless the task explicitly requires it.
- Do not rename symbols that are not part of the change, even if a better name exists.
- Do not reformat unrelated files, reorder class members, or reorganise imports in untouched areas.
- If you spot a genuine problem outside task scope, report it rather than silently fixing it.

---

## Validation Before Finishing

- [ ] **No new compile errors or warnings** — reason through any new or changed API usage.
- [ ] **Blackboard coherence** — any new state flag has a clear owner that writes it.
- [ ] **SO fields accounted for** — if a new serialised field was added to an SO or MonoBehaviour,
  note which `.asset` or prefab needs to be updated in the Editor.
- [ ] **Docs in sync** — if this change alters behaviour described in `Docs/`, update the relevant
  file and note it in your response.
- [ ] **Tuning unchanged or documented** — if a `HeroConfig` or `HeroAbilityConfig` value changed,
  update `Docs/HeroFeelTuning.md` and confirm the regression checklist passes.

---

## Reporting Manual Unity Work

Code changes often require follow-up work in the Unity Editor. Always list this explicitly at
the end of your response:

**Inspector hookup** — component, GameObject, field, value:
> On the `Hero` GameObject → `HeroController` → assign `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset` to `config`.

**Prefab change** — prefab path, what to add or change:
> Open `Assets/_Project/Prefabs/Hero.prefab` → add `HeroAttackModule` to the `Attacks/SlashSide` child; set `direction` to `Side`, `mirrorWithFacing` to true.

**Scene change** — scene name, what to create or configure:
> In `SampleScene`, add a child GameObject `Attacks/SlashDown` under the hero; attach `HeroAttackModule` and a trigger `PolygonCollider2D`.

**Layer or Physics 2D setup:**
> Add a `Terrain` layer in Project Settings → Tags and Layers; assign it to all ground and wall colliders; confirm it is included in `HeroConfig.terrainLayers`.

If the task requires no editor work, state: **No Unity Editor work required.**

---

## What Not To Do

- Do not modify anything under `Assets/Plugins/` or `Assets/ThirdParty/`.
- Do not restructure `Assets/_Project/` folders without updating this file and `Docs/Architecture.md`.
- Do not introduce a new persistent singleton without adding it to `Docs/Architecture.md` and
  deciding its initialization order in `Bootstrap`.
- Do not create a new ScriptableObject type without adding it to `Docs/Architecture.md`.
- Do not add feature-specific implementation details to this file — they belong in `Docs/FeatureSpecs/`.
- Do not trigger saves from inside a hero or enemy MonoBehaviour.
