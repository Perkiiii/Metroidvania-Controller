# AGENTS.md — Metroidvania Controller

## Project Overview

2.5D side-scrolling metroidvania in Unity. Movement and physics are fully 2D (Rigidbody2D / Physics2D); visuals use layered sprites under URP for depth. The hero controller and core melee combat are implemented. Camera, enemy AI, interactables, scene management, and save systems are pending.

Full architecture: `Docs/Architecture.md`. Feature details: `Docs/FeatureSpecs/`.

---

## Tech Stack

| Concern | Solution |
|---|---|
| Engine | Unity, URP |
| Physics | 2D — Rigidbody2D, Collider2D, Physics2D raycasts |
| Input | Unity Input System 1.x — `Assets/_Project/Input/InputSystem_Actions.inputactions` |
| Animation | Animancer — clips driven directly in code; no Animator parameters at runtime |
| Tuning | ScriptableObjects (`HeroConfig`, `HeroAnimationLibrary`) |
| Inspector | Odin Inspector (available; use where it reduces boilerplate, not by default) |
| Language | C# |

---

## Folder Structure

```
Assets/_Project/
  Scripts/Hero/             all hero C# — the implemented core
    Core/                   HeroStateBlackboard, HeroConfig, HeroActorState, HeroAttackDirection
    Actions/                HeroActionController + plain-C# action objects
    Animation/              HeroAnimationController, HeroAnimationLibrary
    Combat/                 HeroAttackModule, HeroAttackHit, hit-reaction interfaces
    Input/                  HeroInputReader
    Movement/               HeroMotor
    Sensors/                HeroSensors
  Scripts/Enemy/            enemy controller, health, perception, behaviour (Milestone 2)
  Scripts/World/            CameraController, CameraZone, TransitionPoint, InteractableBase,
                            InteractManager, CheckpointInteractable, RespawnMarker,
                            HazardZone, HazardRespawnMarker, GameManager, Bootstrap (Milestone 1+)
  Scripts/Persistence/      SaveManager, ISaveTarget, save data types (Milestone 4)
  Scripts/UI/               HUD components, menus, UIFlowController (Milestone 5)
  Scripts/Audio/            AudioManager (Boot + Milestone 1)
  ScriptableObjects/Hero/   HeroConfig.asset, HeroAnimationLibrary.asset
  ScriptableObjects/Enemy/  EnemyConfig assets (Milestone 2)
  ScriptableObjects/World/  CameraConfig.asset, PlayerAbilityState.asset,
                            WorldStateRegistry.asset (Milestone 1+)
  Input/                    InputSystem_Actions.inputactions
  Animations/               .anim clips, Animator controllers
Assets/Plugins/             Sirenix / Odin — do not modify
Assets/ThirdParty/          other third-party assets — do not modify
Docs/                       architecture and feature documentation
```

New scripts go under `Assets/_Project/Scripts/<SystemName>/`. Do not place scripts at the `Assets/` root or inside `Assets/Plugins/`.

---

## Architecture Guardrails

- **Blackboard is the state bus.** Subsystems (Motor, ActionController, AnimationController, etc.) share runtime state through `HeroStateBlackboard`. Avoid passing state between subsystems by direct reference.
- **`HeroController.InitializeSystems` is the established wiring point.** New subsystems should receive dependencies there via `Initialize(config, blackboard, ...)`. Avoid `GetComponent` calls inside subsystem `Awake` or `Start`; avoid `FindObjectOfType` and static fields for runtime coordination.
- **Animancer only — no Animator parameters.** `HeroAnimationController` calls Animancer API directly. Do not add parameters to `HeroAnimator.controller` or call `Animator.SetBool` / `SetFloat` / `SetTrigger`.
- **Interfaces for hit contacts.** `IHeroAttackReceiver`, `IHeroAttackClashReceiver`, and `IHeroDownslashResponder` are the established contracts. New contact types get a new interface; special-case receiver logic does not belong inside `HeroAttackAction`.
- **Keep hitbox control inside `HeroAttackAction`.** Enabling and disabling `PolygonCollider2D` on attack modules should stay in `HeroAttackAction` unless there is a clear reason to move it.
- **Camera and save systems must not reference hero subsystems.** They may read the hero's `Transform`. See `Docs/FeatureSpecs/Camera.md` and `Docs/FeatureSpecs/SaveSystem.md`.
- **Hero health lives in `HeroHealthComponent`.** Health, invincibility tracking, `OnDamaged`, and `OnDeath` are owned by a dedicated `HeroHealthComponent` MonoBehaviour. `HeroController` receives a reference at init but does not own health fields directly. Invincibility is reference-counted by source object, not by a raw timer, so multiple systems can independently grant it without racing.
- **Enemy structure mirrors the hero blackboard-and-action pattern.** Each enemy has an `EnemyController` coordinator, an `EnemyStateBlackboard`, and an `EnemyHealthComponent` that implements `IHeroAttackReceiver`. Configuration lives in an `EnemyConfig` ScriptableObject. Do not use C# inheritance for enemy variants — prefer composition and distinct config assets. See `Docs/FeatureSpecs/EnemyAI.md`.
- **All interactable world objects derive from `InteractableBase` and register with `InteractManager`.** Do not write bespoke proximity detection or input polling inside individual world objects. `InteractManager` (persistent singleton) selects the highest-priority nearby interactable and routes the interact input to it.
- **Scene transitions go through `GameManager`, not from trigger callbacks.** `TransitionPoint` (MonoBehaviour) carries the target scene name and conditions but does not call `LoadSceneAsync` directly. `GameManager.BeginSceneTransition` owns the full sequence: control lock → screen fade → async load → `SceneInit` event → hero position → fade in.
- **`AddControlLock` / `RemoveControlLock` is the only approved input-suppression channel.** Scene transitions, cutscenes, and interactions hold a lock token for their duration. Do not write `blackboard.controlLocked` directly from world or UI systems.
- **Save and load never touch gameplay MonoBehaviours.** `SaveManager` calls only `ISaveTarget.CollectSaveData()` and `ISaveTarget.ApplySaveData()` on registered ScriptableObjects. It must not call into `HeroController`, `HeroStateBlackboard`, or any enemy or world MonoBehaviour. See `Docs/FeatureSpecs/SaveSystem.md`.
- **`GameManager` has exactly four responsibilities.** `GameState` enum, `SceneInit` event, `BeginSceneTransition`, and `Pause` / `Unpause`. Do not add health, enemy tracking, progression, UI, or save logic to it. If a new cross-scene concern doesn't fit these four, it belongs in a dedicated system.
- **Audio routing is explicit.** Hero-owned movement, hurt, death, footstep, and terrain-impact sounds go through `HeroAudioController`, which is the only hero subsystem allowed to call `AudioSource.Play()` / `Stop()` directly on the `Hero/Sounds/*` sources. Enemy, world, UI, music, and shared/module one-shots still go through `AudioManager.PlaySFX` or `PlayMusic`. `Time.timeScale` changes are owned exclusively by `GameManager.Pause()` / `Unpause()` — nothing else sets it.

---

## Coding Rules

- **All gameplay numbers live in ScriptableObjects.** Values that affect timing, speed, feel, or damage belong in `HeroConfig` or a new domain-specific SO. Do not hard-code them in MonoBehaviours. Use `HeroConfig` first; create a new SO only when the concern is clearly distinct.
- **Cache component references — no `GetComponent` in hot paths.** Resolve in `Initialize` or `Awake` and store in a field. Never call `GetComponent` inside `Update`, `FixedUpdate`, or any method called per-frame.
- **Action objects are plain C# classes, not MonoBehaviours.** `HeroJumpAction`, `HeroAttackAction`, etc. use constructor injection and expose `Tick` / `FixedTick`. New actions follow this pattern and are instantiated in `HeroActionController.Initialize`.
- **Avoid `FindObjectOfType`, `GameObject.Find`, and `Object.FindObjectsOfType`.** Prefer injected references.
- **`UnityEditor` APIs are editor-only.** Wrap in `#if UNITY_EDITOR`.
- **Comments explain *why*, not *what*.** Only add a comment when the reasoning is not obvious from the code itself.

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

Make only the changes required by the task:

- Do not refactor surrounding code unless the task explicitly asks for it.
- Do not rename symbols that are not part of the change, even if a better name exists.
- Do not reformat unrelated files, reorder class members, or reorganise imports in untouched areas.
- If you spot a genuine problem outside the task scope, report it in your response rather than silently fixing it.

---

## Validation Before Finishing

Before reporting a task done, verify each of the following:

- [ ] **No new compile errors or warnings** — reason through any new or changed API usage.
- [ ] **Blackboard coherence** — any new state flag has a clear owner that writes it; reads from multiple places are fine.
- [ ] **SO fields accounted for** — if a new serialised field was added to a ScriptableObject or MonoBehaviour, note which `.asset` or prefab needs to be updated in the editor.
- [ ] **Docs in sync** — if this change alters behaviour described in `Docs/`, update the relevant file.

---

## Reporting Manual Unity Work

Code changes often require follow-up work in the Unity Editor. Always list this explicitly at the end of your response using these formats:

**Inspector hookup** — component, GameObject, field, value:
> On the `Hero` GameObject → `HeroController` → assign `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset` to `config`.

**Prefab change** — prefab path, what to add or change:
> Open `Assets/_Project/Prefabs/Hero.prefab` → add `HeroAttackModule` to the `Attacks/SlashSide` child; set `direction` to `Side`, `mirrorWithFacing` to true.

**Scene change** — scene name, what to create or configure:
> In `SampleScene`, add a child GameObject `Attacks/SlashDown` under the hero; attach `HeroAttackModule` and a trigger `PolygonCollider2D`.

**Layer or Physics 2D setup** — what to add and which objects need it:
> Add a `Terrain` layer in Project Settings → Tags and Layers; assign it to all ground and wall colliders; confirm it is included in `HeroConfig.terrainLayers`.

If the task requires no editor work, state: **No Unity Editor work required.**

---

## What Not To Do

- Do not modify anything under `Assets/Plugins/` or `Assets/ThirdParty/`.
- Do not restructure `Assets/_Project/` folders without updating this file and `Docs/Architecture.md`.
- Do not create a new ScriptableObject type without adding it to `Docs/Architecture.md`.
- Do not add feature-specific implementation details to this file — they belong in `Docs/FeatureSpecs/`.
- Do not introduce a new persistent singleton (`GameManager`, `SceneLoader`, etc.) without adding it to `Docs/Architecture.md` and deciding its initialization order in the boot sequence.
- Do not trigger saves from inside a gameplay MonoBehaviour (`HeroController`, `EnemyController`, etc.). Save triggers belong in checkpoint and save-point world objects that call `SaveManager`.
