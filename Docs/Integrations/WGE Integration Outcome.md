# WGE Integration Outcome

Updated: 2026-06-02

## Summary

World Graph Editor is now integrated as the authoring and graph-resolution layer for Underbrew scene transitions. Underbrew still owns all runtime scene loading, save/load, respawn, camera rebinding, and hero placement.

The integration keeps the existing runtime entry point:

```csharp
GameManager.BeginSceneTransition(string targetScene, string destinationPassageGuid = "")
```

WGE supplies the two transition arguments indirectly:

- source gate identity: `TransitionPoint.GetGuid()`
- resolved destination: `WorldGraphTransitionResolver.TryResolve(...)`

WGE runtime transition components are not used for gameplay. Do not add `Passage2D`, `Teleport2D`, WGE spawn points, or a runtime WGE `TransitionManager` to Underbrew scenes.

## What Changed

### Runtime Resolution

Added:

- `Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionRequest.cs`
- `Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionResolver.cs`

`WorldGraphTransitionResolver` stores a `WorldGraphContainer` reference and resolves a source WGE port GUID into:

- target scene build index
- target scene name
- destination passage GUID
- WGE pass status

It does not load scenes, instantiate objects, move the hero, use WGE `TransitionManager.Instance`, or call WGE transition runtime methods.

### Bootstrap Wiring

`Bootstrap` now has:

```csharp
[SerializeField] private WorldGraphContainer worldGraphContainer;
```

In `Start()`, after save load and before the startup scene transition, it calls:

```csharp
WorldGraphTransitionResolver.SetContainer(worldGraphContainer);
```

The Boot scene `Bootstrap` component is assigned to `Assets/_Project/ScriptableObjects/World/UnderbrewWorldGraph.asset`.

### TransitionPoint Refactor

`TransitionPoint` now derives from WGE `PassageBase`:

```csharp
public sealed class TransitionPoint : PassageBase, ITransitionComponent
```

Removed legacy serialized fields:

- `gateKey`
- `targetScene`
- `entryGateKey`

Removed legacy properties:

- `GateKey`
- `TargetScene`
- `EntryGateKey`

Replaced destination lookup:

```csharp
FindByGateKey(...)
```

with:

```csharp
FindByPassageGuid(...)
```

`TransitionPoint.TryActivate` now:

1. keeps existing Underbrew activation guards;
2. gets the source WGE GUID from `GetGuid()`;
3. rejects blank GUIDs;
4. calls `WorldGraphTransitionResolver.TryResolve`;
5. calls `GameManager.BeginSceneTransition(request.TargetSceneName, request.TargetPortGuid)`.

It still does not call `GameManager.SetActiveRespawnMarker`; `linkedRespawnMarker` remains reserved.

### Editor GUID Hardening

`PortsDropdown.GetSelectedValue()` has two Editor failure modes:

1. **No Refresh this session:** `_data` is null or empty, so `GetSelectedValue()` returns `null`.
2. **Port renamed or deleted after assignment:** `_data` is populated (Refresh ran) but the assigned port no longer exists in the graph. In this case `GetSelectedValue()` silently returns `_guidData[0]` — the first available port in the scene — rather than null, so a simple null-fallback cannot catch it.

To close both cases, `TransitionPoint.GetGuid()` in the Editor now reads `_assignedPort._selectedGuid` directly via `SerializedObject` rather than calling `PortsDropdown.GetSelectedValue()` at all. This matches exactly what is baked into builds and removes any dependency on WGE's in-memory `_data` state. `ITransitionComponent.GetGuid()` is routed through the same method.

Builds use `base.GetGuid()` unchanged, which returns `_selectedGuid` directly from the `PortsDropdown` struct.

**Full mutation protection in `TryRefreshFromWorldGraph`:** `TransitionPoint.GetGuid()` reads the serialized `_selectedGuid` directly. However, the Inspector's `Refresh` call goes through `PassageBase.Refresh()` → `PortsDropdown.SetData()` → `RefreshSelectedData()`, which is the same internal mutation path (it also writes `_selectedGuid`). `TransitionPointEditor` closes this separately:

1. Snapshots `_selectedGuid` and `_selectedName` from the `SerializedProperty` before calling `Refresh()`.
2. After `Refresh()`, reads the live object state via a fresh `SerializedObject` before the main `serializedObject.Update()` runs.
3. If `_selectedGuid` was mutated, restores both fields to the originals via `checkSO.ApplyModifiedProperties()`, then calls the main `serializedObject.Update()` on the restored state.
4. Sets `_guidIsStale = true` and displays a warning in the Inspector.

`Refresh` is also only called when the assigned port GUID changes (not for every property change), which eliminates unnecessary exposure to the mutation path for unrelated edits like entry offset or gate side.

**Stale port warning:** When a port is renamed or deleted in the WGE graph after assignment, the Inspector shows: *"The assigned WGE port no longer exists in the graph. The original GUID has been preserved — re-select the correct port or run the validator."* The serialized GUID is not changed without explicit user action.

Run `Tools/Project/Validate Transition Gate Links` after any WGE graph edit that renames or removes ports.

### GameManager Update

`GameManager.BeginSceneTransition` now names the second parameter `destinationPassageGuid`.

During transition placement it resolves:

```csharp
TransitionPoint.FindByPassageGuid(destinationPassageGuid)
```

Scene loading is unchanged and still happens only inside `GameManager` with `SceneManager.LoadSceneAsync`.

### HeroSceneEntry Update

The one legacy `dest.GateKey` log reference was replaced with `dest.name` and `dest.GetGuid()`.

No WGE logic was added to `HeroSceneEntry`.

### TransitionPoint Inspector

`TransitionPointEditor` now draws WGE port assignment first:

- assigned port dropdown
- GUID read-only field
- target scene read-only field

It no longer draws `gateKey`, `targetScene`, or `entryGateKey`.

It still draws Underbrew-specific fields:

- gate side
- door/interact settings
- entry offset and facing
- per-gate entry motion tuning
- reserved linked respawn marker

The WGE `Refresh` call (which populates the GUID and Target Scene read-only display fields) is no longer called on every repaint. It runs once in `OnEnable`, once after each Inspector property change, and on demand via the **Refresh World Graph Data** button in the Authoring section. This avoids querying `WGE EditorData` every frame while keeping the display fields accurate during authoring.

### Transition Gate Validator

`TransitionGateLinkValidator` now validates WGE passage GUIDs instead of legacy gate keys.

It:

- loads the WGE container from `TransitionManager.LoadFromResources().Container`;
- initializes the container;
- opens each enabled Build Settings scene;
- snapshots each `TransitionPoint` object path and serialized passage GUID as plain strings while the scene is open;
- checks blank and duplicate GUIDs per scene;
- uses WGE graph data to resolve target scene and target passage;
- checks that the target scene contains exactly one matching serialized passage GUID.

`GateInfo` stores only string fields (`ObjectPath`, `PassageGuid`). It does not hold live `TransitionPoint` references. `OpenSceneMode.Single` destroys all objects from the previous scene, so any stored `MonoBehaviour` reference would be stale in the cross-scene validation pass. Using plain strings avoids that class of bug entirely.

The build index map is produced using `SceneUtility.GetScenePathByBuildIndex` (positional, all-scene indexing) rather than a sequential count of only enabled scenes. This matches the indexing system WGE uses for `SceneRuntimeData.BuildIndex` and correctly handles Build Settings lists that include disabled scenes.

## Unity Authored Data

The following Unity-authored setup was performed after the code pass:

- `UnderbrewWorldGraph.asset` contains scene nodes and connections for `Boot`, `SampleScene`, `SampleScene2`, and `SampleScene3`.
- `ProjectSettings/EditorBuildSettings.asset` includes 4 enabled scenes.
- `SampleScene`, `SampleScene2`, and `SampleScene3` have WGE port GUIDs assigned on their `TransitionPoint` components.
- `SampleScene3` was added and connected successfully.
- `Assets/WorldGraphEditor/Resources/TransitionManager.prefab` remains editor tooling only and has `_autoLoad` disabled.

Some WGE editor assets may change as part of graph editing and validation, such as:

- `Assets/WorldGraphEditor/ProjectValidationResult.asset`
- `Assets/WorldGraphEditor/Editor/Settings/UndoRedoGraphData.asset`

These are editor/tooling artifacts, not runtime dependencies for Underbrew scene loading.

## How It Works

### Outgoing Transition

1. Hero enters or interacts with an Underbrew `TransitionPoint`.
2. `TransitionPoint` checks Underbrew rules: local guard, hero state, door/interact mode, game state, push-back rules.
3. `TransitionPoint.GetGuid()` returns the assigned WGE source passage GUID.
4. `WorldGraphTransitionResolver.TryResolve(sourceGuid, false, out request)` resolves graph data.
5. `TransitionPoint` calls:

   ```csharp
   GameManager.Instance.BeginSceneTransition(request.TargetSceneName, request.TargetPortGuid)
   ```

### Scene Loading

`GameManager` performs the same Underbrew transition sequence as before:

- outgoing control lock
- fade out
- `SceneManager.LoadSceneAsync`
- `SceneInit`
- destination `TransitionPoint` lookup by passage GUID
- hero placement through `HeroSceneEntry` and `HeroMotor`
- camera rebind
- fade in and scripted entry motion

WGE does not load the scene or move the hero.

### Destination Lookup

After the target scene loads, `GameManager` calls:

```csharp
TransitionPoint.FindByPassageGuid(destinationPassageGuid)
```

This searches active `TransitionPoint` instances in the loaded scene and compares `GetGuid()`.

## Validation Results

Observed successful validation:

```text
[TransitionGateLinkValidator] Checked 4 enabled Build Settings scene(s). No transition gate link issues found.
```

Observed successful runtime behavior:

- existing scene transitions still worked;
- a new `SampleScene3` connection worked;
- boot/continue placed the hero at saved checkpoint `checkpoint_d`;
- save-on-quit continued to work.

Observed healthy save/load logs:

```text
[SaveManager] Save loaded from slot 0 ...
[SaveManager] Startup scene resolved from active respawn scene: SampleScene3
[GameManager] Restored active respawn marker: checkpoint_d
[GameManager] Placed hero at saved respawn marker: checkpoint_d
[SaveManager] Save written to slot 0 ...
```

## Issues Found And Fixed

### False "No WGE Port Assigned" Warnings

Problem:

`TransitionPoint.OnValidate()` used WGE `GetGuid()`, which can return `null` in the Editor before WGE dropdown data is refreshed. A secondary issue: after Refresh populates `_data`, `PortsDropdown.GetSelectedValue()` silently returns `_guidData[0]` (the first available port) when the assigned port no longer exists in the graph, producing a wrong non-null GUID that bypasses a simple null-fallback.

Fix:

`TransitionPoint.GetGuid()` now reads `_assignedPort._selectedGuid` directly via `SerializedObject` in the Editor, bypassing `PortsDropdown.GetSelectedValue()` entirely. This closes the null case and the silent `_guidData[0]` case for all callers (`GetGuid`, `FindByPassageGuid`, `OnValidate`). The build path (`base.GetGuid()`) is unchanged.

Additionally, `TransitionPointEditor.TryRefreshFromWorldGraph` snapshots `_selectedGuid` and `_selectedName` before every `PassageBase.Refresh()` call, detects mutation via a temporary `SerializedObject`, and restores the originals to the live object before `serializedObject.Update()`. This prevents the `PassageBase.Refresh()` → `RefreshSelectedData()` internal mutation from ever reaching `ApplyModifiedProperties()`. A stale-port warning is shown in the Inspector when restoration was necessary.

### Validator Reported Missing Target Gate Despite Runtime Working

Problem:

The validator stored live scene object references across `OpenSceneMode.Single` scene opens. Target scene gate references could become stale before cross-scene matching.

Fix:

`GateInfo` now contains only `ObjectPath` and `PassageGuid` as plain strings. No live `TransitionPoint` reference is stored. All cross-scene comparisons use the string fields.

### Unity AI Assistant Relay Warning

Observed warning:

```text
[RelayService] Bus validation failed: TaskCanceledException: A task was canceled.
```

This came from Unity's AI Assistant package under `Library/PackageCache/com.unity.ai.assistant...` and was unrelated to WGE or Underbrew transitions.

## Current Known Notes

- `Refresh Build Settings` in WGE being greyed out is expected when WGE scene nodes already match enabled Build Settings scenes and indices. The button only becomes clickable when WGE can add or repair missing Build Settings entries from the graph.
- Standalone build smoke testing is still recommended because builds rely on serialized `_selectedGuid`. Run `Tools/Project/Validate Transition Gate Links` immediately before making a standalone build to confirm all GUIDs are assigned and resolvable.
- WGE graph authoring should continue to use Underbrew `TransitionPoint` components only. Do not use WGE runtime passage or teleport components.

### _autoLoad must stay false — it defaults to true in source

`WorldGraphEditor.TransitionManager` has `[SerializeField] private bool _autoLoad = true` as its C# default. The `Assets/WorldGraphEditor/Resources/TransitionManager.prefab` overrides this to `false` (`_autoLoad: 0` in the YAML). **This prefab override must survive every WGE upgrade.** If WGE regenerates or resets the Resources prefab, `_autoLoad` reverts to `true`. After any WGE update, check that `_autoLoad` is still `false` on the prefab before entering Play Mode.

`WorldGraphEditor.Bootstrapper.Execute()` runs at `RuntimeInitializeLoadType.BeforeSceneLoad` — before any scene, before `Bootstrap.Awake()`. If `_autoLoad` is `true`, a `PersistentSingleton<TransitionManager>` is created before Underbrew's boot sequence begins, conflicting with `GameManager`'s scene-loading flow.

`Bootstrap.Start()` contains a `Debug.Assert` that will fire immediately if a WGE `TransitionManager` exists at runtime, pointing directly at the prefab setting.

### GUID hardening and validation workflow

`TransitionPoint.GetGuid()` returns the serialized `_selectedGuid` in the Editor. This is safe and consistent with build behavior but means a stale GUID (from a renamed or deleted WGE port) will not be detected at the gate level alone — `TryResolve` will fail at runtime with a logged warning. Run the validator after any WGE graph edit that renames or removes ports:

1. `Tools/Project/Validate Transition Gate Links` — reports blank GUIDs, duplicate GUIDs, GUIDs that cannot pass transition, and target-scene mismatches.
2. If the validator reports a GUID that cannot pass transition, re-select the correct port on that `TransitionPoint` in the Inspector and save the scene.
3. Re-run the validator until it reports zero issues.

## Review Checklist

- Confirm `TransitionPoint` does not contain legacy `gateKey`, `targetScene`, or `entryGateKey`.
- Confirm only `GameManager` calls `SceneManager.LoadSceneAsync` for gameplay transitions.
- Confirm `WorldGraphTransitionResolver` only resolves data and never loads scenes or moves the hero.
- Confirm `Bootstrap` owns the `WorldGraphContainer` reference, not `GameManager`.
- Confirm `Bootstrap` logs an error if `worldGraphContainer` is unassigned.
- Confirm `Bootstrap` asserts that no WGE `TransitionManager` exists at runtime.
- Confirm `TransitionGateLinkValidator` compares serialized passage GUIDs.
- Confirm `TransitionGateLinkValidator` uses `SceneUtility.GetScenePathByBuildIndex` for build index mapping (not a sequential count of enabled scenes).
- Confirm `TransitionManager.prefab` has `_autoLoad: 0` — check after every WGE upgrade.
- Confirm no Underbrew gameplay scene contains WGE `Passage2D`, `Teleport2D`, WGE spawn points, or a runtime WGE `TransitionManager`.
- Run `Tools/Project/Validate Transition Gate Links` after any WGE graph edit that renames or deletes ports.
