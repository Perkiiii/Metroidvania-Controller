# Project Overview

Verified from `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, Unity project metadata, and the project-owned asset layout on 2026-09-10.

## Purpose

This is a Unity 2.5D side-scrolling metroidvania. Gameplay movement and physics are fully 2D, while layered sprites, lighting, and camera presentation provide depth and a 2.5D visual treatment.

## Scope

The project includes hero movement and combat, gated traversal abilities, enemy and boss foundations,
interactables/checkpoints/hazards, scene transitions, persistent save state, camera presentation,
audio routing, HUD/menu UI, and world calendar/climate/weather state. Development/sample gameplay
scenes and a UI Sandbox are present. A production frontend, some menu data domains, Package 4 room
climate tooling, weather presentation, final art/audio polish, and other roadmap items remain
deferred according to the implementation plan.

## Architecture

The runtime is coordinator-and-subsystem based. `HeroController` wires Hero components; `HeroStateBlackboard` shares runtime state; action objects are plain C# classes; `HeroMotor` owns Rigidbody2D velocity and gravity behavior; and Animancer drives animation directly. ScriptableObjects separate core tuning, ability tuning, unlock flags, and persistent player/world state.

Persistent services are created from `Boot.unity` by `Bootstrap` and carried across gameplay scenes. `GameManager` owns game state, pause, and scene-transition entry. `SaveManager` gathers and applies data only through `ISaveTarget` assets. Camera and UI systems communicate through narrow signals/events, keeping gameplay ownership out of presentation layers.

## Main Workflows

- Boot initializes persistent managers, loads or creates save data, completes world-weather load
  reconciliation (including post-load weather-time binding), and transitions to the saved or
  configured gameplay scene.
- The Hero update flow samples input, evaluates actions, updates sensors and motor physics, and updates visuals; details and ordering are maintained in `Docs/Architecture.md` and the Hero feature specifications.
- Interactions register with `InteractManager`; checkpoints update the active respawn marker and trigger the established save flow.
- Scene transitions enter through `GameManager.BeginSceneTransition`; World Graph Editor data resolves authored connections and destination gates.
- HUD and menu views subscribe to state/events and use UGUI; pause ownership remains with `GameManager`.
- Edit Mode and Play Mode tests are authored under `Assets/_Project/Scripts/Editor/Tests/` and `Assets/_Project/Tests/PlayMode/` and should be run through Unity’s Test Runner.

## Major Decisions

- Keep 2D physics independent from 2.5D visual presentation.
- Keep subsystem ownership explicit: motor writes velocity, actions own action logic, ScriptableObjects own authored/persistent data, and managers own cross-scene lifecycle concerns.
- Use Animancer clip playback without Animator parameter-driven runtime logic.
- Use stable save keys and `ISaveTarget` assets instead of serializing scene-object references.
- Keep third-party/plugin content separate from project-owned systems; project changes belong under `Assets/_Project/` and `Docs/`.
