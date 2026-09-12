# Project Core Technologies

Verified from `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, installed plugin roots, source layout, and the authoritative architecture document on 2026-09-10.

## Languages and Runtimes

- C# is used for gameplay, editor tooling, and tests.
- Unity Editor version: `6000.3.10f1` (Unity 6.0.3).
- Unity’s 2D physics stack is used through `Rigidbody2D`, colliders, and `Physics2D` queries.

## Frameworks and Libraries

- Universal Render Pipeline `17.3.0` provides rendering; layered sprites and URP lighting support the 2.5D presentation.
- Unity Input System `1.18.0` drives keyboard/mouse and gamepad input through `Assets/_Project/Input/InputSystem_Actions.inputactions`.
- Animancer is the runtime animation playback system used by the Hero architecture.
- Unity UI/UGUI `2.0.0` is used for HUD and menu presentation.
- Unity Timeline `1.8.10` is present for authored camera presentation flows.
- Installed project plugins include Sirenix, WorldGraphEditor, TextMesh Pro, and Feel; plugin files are third-party/integration content, not project-owned gameplay modules.

## Build, Test, and Development Tools

- Unity Test Framework `1.6.0` is declared.
- Edit Mode tests are under `Assets/_Project/Scripts/Editor/Tests/`; Play Mode tests are under `Assets/_Project/Tests/PlayMode/`.
- Unity’s Editor/Test Runner is the authoritative compilation and test environment for project scripts. No Unity build or test run was performed during this bootstrap.

## External Services and Infrastructure

- No runtime cloud service or external service dependency was identified in the inspected project metadata.
- World Graph Editor is an installed authoring integration for scene graph data. Gameplay scene flow remains owned by the project’s `GameManager`/scene systems.

## Important Technical Constraints

- Runtime animation uses Animancer clip playback; Hero behavior must not depend on Animator parameters.
- Hero subsystems communicate through injected dependencies and the blackboard rather than per-frame component discovery.
- `HeroConfig` and `HeroAbilityConfig` have distinct tuning ownership; persistent values belong to ScriptableObjects implementing `ISaveTarget`.
- Camera code consumes the hero Transform and camera signals, not Hero controller/subsystem references.
- Project-owned source belongs under `Assets/_Project/`; `Assets/Plugins/` and `Assets/ThirdParty/` are not to be modified for feature work.
