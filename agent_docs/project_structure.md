# Project Structure

Verified from the checkout on 2026-09-10. This document records stable layout and ownership; detailed feature contracts remain in `Docs/`.

## Directory Layout

- `Assets/_Project/` contains project-owned Unity content.
  - `Scripts/` contains runtime systems and editor tooling, organized into `Hero`, `Enemy`, `Boss`, `Camera`, `World`, `Save`, `UI`, `Scene`, `Hazard`, `Managers`, `Audio`, `WorldGraph`, and `Utilities` areas.
  - `ScriptableObjects/` contains authored Hero, Enemy, Boss, UI, Sandbox, and World data, including Seasons and WeatherDefinitions.
  - `Prefabs/` contains the Hero, manager singletons, enemies/bosses, UI, world persistence objects, particles, and combat VFX.
  - `Scenes/` contains `Boot`, `SampleScene` through `SampleScene4`, and `UISandbox`.
  - `Input/`, `Animations/`, `Art/`, `Audio/`, `Lights/`, `Materials/`, `Shaders/`, and `Timelines/` contain supporting authored assets.
  - `Tests/PlayMode/` contains Play Mode tests; editor tests are kept with the editor test scripts under `Scripts/Editor/Tests/`.
- `Docs/` is the project knowledge base: architecture, feature specifications, implementation plans, integration notes, and research.
- `Packages/` and `ProjectSettings/` hold Unity package and project configuration.
- `Assets/Plugins/` contains installed third-party/editor integrations and is outside project-system ownership.

## Modules and Responsibilities

- Hero mechanics are coordinated by `HeroController`. Input, sensors, motor, actions, animation, audio, combat, health, and camera signaling are separate components/classes.
- Enemy and boss systems use coordinators plus configuration/state objects. Boss-specific behavior is kept in the Boss area and communicates with shared systems through explicit contracts.
- World systems own interactables, checkpoints, hazards, transitions, world persistence, calendar/time, and climate/weather definitions/state.
- Save systems serialize persistent ScriptableObject owners through `ISaveTarget`; scene identity is represented by stable string keys.
- Camera, audio, managers, scene flow, and UI are separate integration areas. Persistent manager prefabs are initialized from the Boot scene.

## Main Interfaces and Integration Boundaries

- `HeroStateBlackboard` is the shared runtime state bus. `HeroMotor` is the authority for `Rigidbody2D` velocity writes, while plain-C# action objects request behavior through the established controller boundary.
- `HeroConfig` owns core movement/combat/health tuning; `HeroAbilityConfig` owns gated traversal tuning; `PlayerAbilityState` owns unlock flags.
- `GameManager.BeginSceneTransition` owns scene-transition entry. World Graph Editor data is used for authoring/resolution, not as the gameplay transition owner.
- `InteractableBase` and `InteractManager` centralize proximity and interaction selection.
- `GameCameras` and camera components consume the hero Transform and neutral camera signals rather than hero subsystem references.
- Persistent HUD views subscribe to persistent state events; UI does not own saves, scene loading, or pause time-scale changes.

## Tests and Supporting Assets

The checkout contains focused Edit Mode coverage for Hero, Camera, Enemy, Boss, UI, World Time, World Climate, and World Persistence, plus Play Mode coverage for camera, Hero, and UI flows. Unity test execution was not performed during this documentation bootstrap.
