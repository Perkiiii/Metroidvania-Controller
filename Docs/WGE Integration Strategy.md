# WGE Integration Strategy for Underbrew

*Revised 2026-06-02. Audited against actual project source (`TransitionPoint.cs`, `GameManager.cs`,
`Bootstrap.cs`, `HeroSceneEntry.cs`, `TransitionPointEditor.cs`, `TransitionGateLinkValidator.cs`)
and actual WGE source (`PassageBase.cs`, `Passage2D.cs`, `TransitionManager.cs`,
`PortsDropdown.cs`, `Bootstrapper.cs`, `GraphContainerBase.cs`, `RefreshContext.cs`,
`WorldGraphEditor.asmdef`). All API facts in this document are verified from source, not assumed.*

---

## Executive Summary

Underbrew's scene-transition architecture funnels through one entry point:
`GameManager.BeginSceneTransition(string targetScene, string destinationPassageGuid)`. The WGE
integration does not change that entry point. It replaces how `TransitionPoint` populates the two
arguments.

Previously: authors typed a `gateKey`, `targetScene`, and `entryGateKey` string into each gate
Inspector. After integration: authors select a WGE port from a graph-backed dropdown. WGE stores the
GUID, and `WorldGraphTransitionResolver` reads the graph to supply the target scene name and the
destination passage GUID automatically.

`TransitionPoint` inherits directly from `WorldGraphEditor.PassageBase`. `PassageBase` has been
verified to have zero runtime side effects: no `Start`, `Awake`, `OnEnable`, or collision callbacks.
Its only abstract method is `GetSpawnPosition()`. Its `Refresh` method is entirely inside
`#if UNITY_EDITOR`. It does not reference `TransitionManager`.

The three legacy serialized fields (`gateKey`, `targetScene`, `entryGateKey`) are **removed
entirely**. There is no legacy fallback path. `GetGuid()` from `PassageBase` is the identity
source. The old `GateKey` property is removed. The old `FindByGateKey` method is renamed
`FindByPassageGuid` and compares `candidate.GetGuid()` directly.

WGE's unsafe runtime path — `Passage2D`, `TeleportBase` derivatives, and `TransitionManager` — is
not used. WGE never loads scenes, never moves the hero, and never appears at runtime. Its
`TransitionManager` prefab is kept for editor tooling only, with `_autoLoad` disabled.

---

## Verified WGE Source Facts

These facts are verified from the actual WGE source files. Do not re-derive them from WGE
documentation or samples.

### PassageBase (`PassageBase.cs`)

- `PassageBase : MonoBehaviour, ITransitionComponent`.
- `[DisallowMultipleComponent]` — same attribute is already on `TransitionPoint`. Both having it is
  harmless; Unity treats it as one constraint.
- Serialized field: `private PortsDropdown _assignedPort`. This holds the selected GUID.
- Editor-only (`#if UNITY_EDITOR`) fields: `private string _assignedGuid`, `private string _targetScene`.
  These are read-only display fields. They do not exist in builds.
- `Refresh(RefreshContext context)` is `virtual` and entirely inside `#if UNITY_EDITOR`. It is never
  called at runtime. `TransitionPoint` does not need to override it.
- `GetGuid()` calls `_assignedPort.GetSelectedValue()`. It is available at runtime.
- `GetSpawnPosition()` is the only abstract method. `TransitionPoint` must implement it.
- **Zero runtime side effects.** No Unity message methods, no `TransitionManager` reference.

### ITransitionComponent (`ITransitionComponent.cs`)

- Three members: `Refresh(RefreshContext)` (editor-only), `Vector3 GetSpawnPosition()`, `string GetGuid()`.
- `TransitionPoint : PassageBase` satisfies this interface automatically.

### PortsDropdown (`PortsDropdown.cs`) — Build Safety

- `_selectedGuid` is a serialized `string`. It is written when the author selects a port and
  `serializedObject.ApplyModifiedProperties()` is called.
- **In builds:** `GetSelectedValue()` returns `_selectedGuid` directly. No other logic runs.
- **In the Editor:** `GetSelectedValue()` returns `null` if `_data` (a runtime `List<>` populated
  by `SetData()`) has not been populated in this session. `_data` is populated by `Refresh`. This
  means `GetGuid()` returns `null` in Editor Play Mode unless a WGE Refresh has been triggered
  after opening Unity. See the Manual Test Checklist for the required pre-test step.
- **Conclusion:** builds are safe provided `_selectedGuid` was serialized before the build. Editor
  play tests require a WGE Refresh each session before testing transitions.

### Passage2D (`Passage2D.cs`)

- `Traverse()` calls `TransitionManager.Instance.GoFrom(GetGuid(), true)`. Directly bypasses
  `GameManager.BeginSceneTransition`. Do not place `Passage2D` on Underbrew gates.
- `OnEnable` subscribes to `TransitionManager.OnSceneLoaded`. If `TransitionManager.Instance` is
  null (which it will be in Underbrew), this is a `NullReferenceException` waiting to fire.
- `OnTriggerEnter2D` calls `Traverse()` unconditionally when `_canBeUsed`.

### TransitionManager (`TransitionManager.cs`)

- `PersistentSingleton<TransitionManager>` — calls `DontDestroyOnLoad` in `Awake`.
- `Start()` initializes the container, calls `FindAnyObjectByType<SpawnPointBase>()`, and optionally
  instantiates a player prefab.
- `GoFrom` / `GoInternal` call `AwaitUtility.LoadSceneAsync` directly.
- Must never exist at runtime in Underbrew.

### WGE Bootstrapper — CRITICAL TIMING RISK (`Bootstrapper.cs`)

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
public static void Execute()
{
    var manager = TransitionManager.LoadFromResources();
    if (manager != null && manager.AutoLoad)
        TransitionManager.CreateInstance();
}
```

`BeforeSceneLoad` runs before any scene loads — before the Boot scene, before `Bootstrap.Awake()`,
before `GameManager` is instantiated. If `_autoLoad` is `true` on the `Resources/TransitionManager`
prefab, WGE creates a `PersistentSingleton` `TransitionManager` with `DontDestroyOnLoad` before
Underbrew's startup sequence begins. This will conflict with the rest of the boot sequence.

**Disabling `_autoLoad` is the first thing done in this integration, not a later cleanup step.**

### WorldGraphContainer (`GraphContainerBase.cs`, `WorldGraphContainer.cs`)

- `Initialize()` fills four internal dictionaries. Calling it multiple times is safe (idempotent).
- `IsInitialized()` returns `true` when all four dictionaries are non-null. Call this before
  `CanPassTransition` or `GetTransitionData`; both will throw `NullReferenceException` if
  called before `Initialize()`.
- `CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status)`
  returns `false` with `BlockedByAdditionalPort` if the GUID is not in the graph.
- `GetTransitionData(string guid, false)` returns a `RuntimeTransitionData` struct with
  `CurrentPassageGuid`, `TargetPassageGuid`, and `TargetSceneBuildIndex`. With `isTargetPassage:
  false` this is the correct call for looking up a source port.
- `GetTransitionData` throws `KeyNotFoundException` if the GUID is missing. The resolver must
  guard against this; `CanPassTransition` returning true is not sufficient alone because the
  two dictionaries are filled separately.

### RefreshContext (`RefreshContext.cs`)

- The entire struct is `#if UNITY_EDITOR`. It cannot be constructed at runtime.
- Constructor: `RefreshContext(ITransitionManager manager)`.
- `FillPortsDropdownData(PortsDropdown)` fills the dropdown with ports for the currently active
  scene via `SceneManager.GetActiveScene().path`.
- The concrete call to use in `TransitionPointEditor`:
  ```csharp
  #if UNITY_EDITOR
  ((TransitionPoint)target).Refresh(new RefreshContext(TransitionManager.LoadFromResources()));
  #endif
  ```
  `TransitionManager.LoadFromResources()` reads `Resources/TransitionManager.prefab`. The same
  prefab WGE's own tooling uses — no forking needed.

### Assembly Definition

- `WorldGraphEditor.asmdef` has `"autoReferenced": true`. Underbrew scripts can reference
  `WorldGraphEditor` types without any `.asmdef` changes. This is resolved; no investigation needed.

---

## Verified Underbrew Source Facts

These facts are verified from the actual project source. Treat them as ground truth.

### `FindByGateKey` compares the private field

`TransitionPoint.cs` line 102:
```csharp
if (candidate == null || candidate.gateKey != key) continue;
```
`FindByGateKey` is a static method inside `TransitionPoint` and can access the private `gateKey`
field. `GateKey` (public property) currently returns `gateKey`. After the refactor, the correct
implementation calls `candidate.GetGuid()` directly and the method is renamed `FindByPassageGuid`.

### `TryActivate` hard-exits for blank `targetScene` / `entryGateKey`

`TransitionPoint.cs` lines 175–185. Both checks return `false` before any WGE lookup is attempted.
WGE-backed gates intentionally have both fields empty. **These guards must be removed entirely.**
There is no legacy fallback path. A gate with no WGE port assigned logs a warning and rejects.

### `OnValidate` warns for blank `gateKey`

`TransitionPoint.cs` lines 271–274. After removing the `gateKey` field, this warning is removed
entirely with the field.

### `GameManager.BeginSceneTransition` signature

`public bool BeginSceneTransition(string targetScene, string entryGateKey = "")`.

Rename the second parameter to `destinationPassageGuid` in the same pass as the TransitionPoint
refactor. The `Bootstrap.Start()` call `GameManager.Instance.BeginSceneTransition(startupScene)`
passes one argument and continues to work because the second parameter defaults to `""`. When
`destinationPassageGuid` is empty, `TransitionRoutine` correctly skips gate lookup and uses the
saved respawn placement. No behaviour change.

### `TransitionRoutine` destination lookup

`GameManager.cs` line 153:
```csharp
TransitionPoint dest = !string.IsNullOrEmpty(entryGateKey)
    ? TransitionPoint.FindByGateKey(entryGateKey)
    : null;
```
After the refactor:
```csharp
TransitionPoint dest = !string.IsNullOrEmpty(destinationPassageGuid)
    ? TransitionPoint.FindByPassageGuid(destinationPassageGuid)
    : null;
```
And the error log on line 159 should say "passage GUID" not "key".

### `TransitionGateLinkValidator` reads legacy fields

`TransitionGateLinkValidator.cs` uses `gate.Gate.GateKey` (line 105), `gate.Gate.TargetScene`
(line 136), and `gate.Gate.EntryGateKey` (line 147). After removing those properties, the validator
must read `gate.Gate.GetGuid()` for identity validation. Phase 6 rewrites this.

### `HeroSceneEntry` requires no WGE changes

`HeroSceneEntry` reads `dest.GateSide`, `dest.EntrySpawnPosition`, `dest.FacingOverride`,
`dest.EntryRunInDuration`, and the other entry motion fields. All are non-WGE properties that
survive the refactor. The `dest.GateKey` reference in the `GateSide.Unknown` error log at line 97
must be changed to `dest.name` or `dest.GetGuid()`.

### `Bootstrap` as container owner

`Bootstrap.cs` currently holds five prefab `[SerializeField]` references. Adding
`[SerializeField] private WorldGraphContainer worldGraphContainer` is consistent with its pattern.
This does not expand `GameManager`.

---

## What WGE Provides

| Category | System | Usage |
|---|---|---|
| Graph authoring | `WorldGraphContainer` | Holds scene nodes, connections, and port GUIDs. Authoring in the WGE editor window. |
| Port selection and GUID storage | `PassageBase`, `PortsDropdown` | Underbrew inherits from `PassageBase`. GUID is serialized in `_selectedGuid`. |
| Runtime graph resolution | `container.CanPassTransition`, `container.GetTransitionData` | Used by `WorldGraphTransitionResolver` only. |
| Editor refresh | `RefreshContext`, `TransitionManager.LoadFromResources()` | Used by `TransitionPointEditor.Refresh` only. |
| Runtime transitions | `Passage2D`, `TeleportBase`, `TransitionManager` | **Do not use.** Conflict with `GameManager`. |
| Spawn points | WGE spawn point components | **Do not use.** Underbrew owns spawn placement. |

---

## What Underbrew Keeps

| Category | Owner | Rule |
|---|---|---|
| Scene loading | `GameManager.BeginSceneTransition` | The only code path that loads gameplay scenes. |
| Destination lookup | `TransitionPoint.FindByPassageGuid` | Compares `candidate.GetGuid()`. Called from `GameManager.TransitionRoutine`. |
| Spawn placement and entry motion | `HeroSceneEntry`, `TransitionPoint` entry fields | WGE never moves or spawns the hero. |
| Hero physics | `HeroMotor` | WGE never writes hero velocity. |
| Save and respawn | `SaveManager`, `RespawnMarker`, checkpoint and hazard flows | WGE data must not appear in save files. `linkedRespawnMarker` remains reserved and unused. |
| Camera and audio | Existing Underbrew systems | No WGE camera or audio integration. |

---

## Pre-Implementation Safety Step

**Before writing a single line of code:**

Open `Assets/WorldGraphEditor/Resources/TransitionManager.prefab` in the Inspector. Set `_autoLoad`
to `false`. Save. Enter Play Mode and confirm the Console shows no WGE-related output and no
`TransitionManager` instance is created.

This must be done first because `WorldGraphEditor.Bootstrapper` runs
`RuntimeInitializeLoadType.BeforeSceneLoad` — before the Boot scene, before `Bootstrap.Awake()`.
If `_autoLoad` is ever `true`, a WGE `PersistentSingleton` `TransitionManager` is created before
Underbrew's startup sequence, conflicting with the entire boot flow.

---

## Implementation Phases

### Phase 0 — WGE Tooling Setup (no code changes)

1. Confirm WGE is imported under `Assets/WorldGraphEditor/` and the project compiles.
2. Confirm `_autoLoad` is `false` on `Assets/WorldGraphEditor/Resources/TransitionManager.prefab`
   (see Pre-Implementation Safety Step above).
3. Create a `WorldGraphContainer` asset at
   `Assets/_Project/ScriptableObjects/World/UnderbrewWorldGraph.asset`.
4. Assign it to the WGE `TransitionManager.prefab` `_container` field.
5. Open the WGE editor window. Add scene nodes for all gameplay scenes. Add ports matching the
   existing gate structure. Connect them with edges.
6. Do not add `Passage2D`, `Teleport2D`, or WGE spawn point components to any scene objects.
7. Do not add `TransitionManager` instances to the Boot scene or any gameplay scene.

### Phase 1 — WorldGraphTransitionResolver

Create these two files:

```
Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionRequest.cs
Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionResolver.cs
```

**`WorldGraphTransitionRequest`** is a pure data struct:

```csharp
using WorldGraphEditor;

public readonly struct WorldGraphTransitionRequest
{
    public readonly bool IsValid;
    public readonly string SourcePortGuid;
    public readonly string TargetPortGuid;
    public readonly int TargetSceneBuildIndex;
    public readonly string TargetSceneName;
    public readonly TransitionPassStatusType PassStatus;
    public readonly string FailureReason;

    public WorldGraphTransitionRequest(
        string sourcePortGuid,
        string targetPortGuid,
        int targetSceneBuildIndex,
        string targetSceneName,
        TransitionPassStatusType passStatus)
    {
        IsValid = true;
        SourcePortGuid = sourcePortGuid;
        TargetPortGuid = targetPortGuid;
        TargetSceneBuildIndex = targetSceneBuildIndex;
        TargetSceneName = targetSceneName;
        PassStatus = passStatus;
        FailureReason = null;
    }

    public WorldGraphTransitionRequest(string sourcePortGuid, string failureReason)
    {
        IsValid = false;
        SourcePortGuid = sourcePortGuid;
        TargetPortGuid = null;
        TargetSceneBuildIndex = -1;
        TargetSceneName = null;
        PassStatus = TransitionPassStatusType.BlockedByAdditionalPort;
        FailureReason = failureReason;
    }
}
```

**`WorldGraphTransitionResolver`** is a static class:

```csharp
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldGraphEditor;

public static class WorldGraphTransitionResolver
{
    private static WorldGraphContainer _container;

    public static void SetContainer(WorldGraphContainer container)
    {
        _container = container;
    }

    public static bool TryResolve(
        string sourcePortGuid,
        bool ignoreShortcuts,
        out WorldGraphTransitionRequest request)
    {
        if (_container == null)
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, "WorldGraphContainer is null.");
            return false;
        }

        if (string.IsNullOrEmpty(sourcePortGuid))
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, "Source port GUID is empty.");
            return false;
        }

        if (!_container.IsInitialized())
            _container.Initialize();

        if (!_container.CanPassTransition(sourcePortGuid, ignoreShortcuts, out var status))
        {
            request = new WorldGraphTransitionRequest(
                sourcePortGuid,
                $"CanPassTransition returned false. Status: {status}");
            return false;
        }

        RuntimeTransitionData data;
        try
        {
            data = _container.GetTransitionData(sourcePortGuid, false);
        }
        catch (System.Exception e)
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, $"GetTransitionData threw: {e.Message}");
            return false;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(data.TargetSceneBuildIndex);
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            request = new WorldGraphTransitionRequest(
                sourcePortGuid,
                $"Build index {data.TargetSceneBuildIndex} does not map to a loadable scene.");
            return false;
        }

        request = new WorldGraphTransitionRequest(
            sourcePortGuid,
            data.TargetPassageGuid,
            data.TargetSceneBuildIndex,
            sceneName,
            status);
        return true;
    }
}
```

The resolver must never call `SceneManager.LoadSceneAsync`, touch `TransitionManager`, instantiate
a player, or write hero velocity.

### Phase 2 — Bootstrap Container Reference

In `Bootstrap.cs`, add one serialized field:

```csharp
[SerializeField] private WorldGraphContainer worldGraphContainer;
```

In `Bootstrap.Start()`, call the resolver initializer before `BeginSceneTransition`:

```csharp
private void Start()
{
    SaveManager.Instance.LoadOrCreate(0);
    WorldGraphTransitionResolver.SetContainer(worldGraphContainer);   // ← add this line
    string startupScene = SaveManager.Instance.GetStartupScene(firstScene);
    GameManager.Instance.RequestSavedRespawnPlacementOnNextSceneLoad();
    GameManager.Instance.BeginSceneTransition(startupScene);
}
```

In the Boot scene Inspector, assign `UnderbrewWorldGraph.asset` to both the `Bootstrap` component
and the WGE `TransitionManager.prefab`. They are both readers of the same asset; there is no
ownership conflict.

### Phase 3 — Refactor `TransitionPoint`

**Base class change:**

```csharp
// Before
public sealed class TransitionPoint : MonoBehaviour

// After
using WorldGraphEditor;
public sealed class TransitionPoint : PassageBase
```

**Remove these serialized fields entirely:**

```csharp
// Remove:
[SerializeField] private string gateKey;
[SerializeField] private string targetScene;
[SerializeField] private string entryGateKey;
```

These fields are removed, not marked obsolete, not kept in a debug foldout. There is no legacy
authoring path. Any existing scene data referencing these fields is superseded by the WGE port
selection. After migration in Phase 7, every gate will have a WGE GUID as its identity.

**Remove these public properties:**

```csharp
// Remove:
public string GateKey => gateKey;
public string TargetScene => targetScene;
public string EntryGateKey => entryGateKey;
```

`GetGuid()` is the identity. Any code that called `transitionPoint.GateKey` must call
`transitionPoint.GetGuid()` instead. Update all call sites.

**Implement the required abstract method:**

```csharp
public override Vector3 GetSpawnPosition()
{
    return EntrySpawnPosition;
}
```

`EntrySpawnPosition` is `transform.position + (Vector3)entryOffset`. This satisfies the WGE
`ITransitionComponent` contract without changing how `HeroSceneEntry` places the hero.

**Replace `FindByGateKey` with `FindByPassageGuid`:**

```csharp
public static TransitionPoint FindByPassageGuid(string guid)
{
    if (string.IsNullOrEmpty(guid)) return null;

    TransitionPoint match = null;
    for (int i = 0; i < Active.Count; i++)
    {
        TransitionPoint candidate = Active[i];
        if (candidate == null || candidate.GetGuid() != guid) continue;

        if (match != null)
        {
            Debug.LogWarning($"[TransitionPoint] Multiple active gates with GUID '{guid}' — using first match.");
            break;
        }

        match = candidate;
    }

    return match;
}
```

**Update `OnValidate`:**

Remove the warnings for blank `gateKey`, blank `targetScene`, and blank `entryGateKey` entirely
(the fields are gone). Add a warning for the new failure case:

```csharp
if (string.IsNullOrEmpty(GetGuid()))
    Debug.LogWarning($"[TransitionPoint] '{name}' has no WGE port assigned. Select a port in the Inspector.", this);
```

**Update `HeroSceneEntry.cs` line 97:**

The `GateSide.Unknown` error log references `dest.GateKey`. Change to `dest.name` or `dest.GetGuid()`:

```csharp
// Before
Debug.LogError($"[HeroSceneEntry] Gate '{dest.GateKey}' has GateSide.Unknown — skipping entry motion.");

// After
Debug.LogError($"[HeroSceneEntry] Gate '{dest.name}' ({dest.GetGuid()}) has GateSide.Unknown — skipping entry motion.");
```

### Phase 4 — Update `TryActivate`

**Remove the early-exit guards for legacy blank fields:**

```csharp
// Remove entirely:
if (string.IsNullOrEmpty(targetScene)) { ... return false; }
if (string.IsNullOrEmpty(entryGateKey)) { ... return false; }
```

These guards checked the now-removed fields. A WGE gate always has empty legacy strings; these
blocks would reject every transition.

**Keep all other existing guards unchanged:**

- `if (localTransitionGuard) return false;`
- `if (hero == null) return false;`
- `if (GameManager.Instance == null) return false;`
- Door/interact mode checks
- `ShouldRejectActivation` / push-back
- `if (GameManager.Instance.State != GameState.Playing) return false;`
- `localTransitionGuard = true` only after `BeginSceneTransition` returns true

**Replace the destination resolution block with WGE resolution:**

```csharp
string graphGuid = GetGuid();

if (string.IsNullOrEmpty(graphGuid))
{
    Debug.LogWarning($"[TransitionPoint] '{name}' has no WGE port assigned; transition rejected.", this);
    return false;
}

if (!WorldGraphTransitionResolver.TryResolve(graphGuid, ignoreShortcuts: false, out WorldGraphTransitionRequest request))
{
    Debug.LogWarning($"[TransitionPoint] WGE resolve failed for '{name}': {request.FailureReason}", this);

    if (mode == TransitionActivationMode.AutoTrigger && heroCollider != null)
        ApplyPushBack(hero, heroCollider);

    return false;
}

if (!GameManager.Instance.BeginSceneTransition(request.TargetSceneName, request.TargetPortGuid))
    return false;

localTransitionGuard = true;
return true;
```

**Do not add** `GameManager.SetActiveRespawnMarker(linkedRespawnMarker)` here. Crossing a gate does
not update the respawn marker in this integration pass. `linkedRespawnMarker` remains reserved.

### Phase 5 — Update `TransitionPointEditor`

The editor is fully custom and draws all fields manually. Do not call `base.OnInspectorGUI()` and
do not replace it with WGE's editor.

**Add PassageBase serialized properties:**

```csharp
private SerializedProperty assignedPort;
private SerializedProperty assignedGuid;
private SerializedProperty targetSceneFromGraph;
```

**Find them in `OnEnable()`:**

```csharp
assignedPort          = serializedObject.FindProperty("_assignedPort");
assignedGuid          = serializedObject.FindProperty("_assignedGuid");
targetSceneFromGraph  = serializedObject.FindProperty("_targetScene");
```

`_assignedGuid` and `_targetScene` are editor-only fields on `PassageBase`. They exist in the
serialized object in the Editor but not in builds. `FindProperty` is safe for them in the Editor.

**Update `HasAllExpectedProperties()`:**

The existing method checks all serialized properties. Add the three new ones or the editor will
silently fall back to `DrawDefaultInspector()` when any one is missing:

```csharp
private bool HasAllExpectedProperties()
{
    return assignedPort != null
        && assignedGuid != null
        && targetSceneFromGraph != null
        && gateSide != null
        && entryOffset != null
        && entryFacingOverride != null
        && entryRunInDuration != null
        && entryDropSpeed != null
        && bottomThrowHorizontal != null
        && bottomThrowVertical != null
        && bottomThrowDuration != null
        && bottomGateSpawnLift != null
        && entryMaxFallbackTime != null
        && isDoor != null
        && requireInteract != null
        && linkedRespawnMarker != null;
}
```

Note: `gateKey`, `targetScene`, and `entryGateKey` are removed from `HasAllExpectedProperties()`
along with their `FindProperty` calls in `OnEnable`.

**Draw the WGE port section at the top of `OnInspectorGUI`, before gate-side and entry tuning:**

```csharp
DrawSection("World Graph Port");

#if UNITY_EDITOR
((TransitionPoint)target).Refresh(new RefreshContext(TransitionManager.LoadFromResources()));
#endif

EditorGUILayout.PropertyField(assignedPort, new GUIContent("Assigned Port"));

using (new EditorGUI.DisabledScope(true))
{
    EditorGUILayout.PropertyField(assignedGuid,         new GUIContent("GUID (read-only)"));
    EditorGUILayout.PropertyField(targetSceneFromGraph, new GUIContent("Target Scene (read-only)"));
}

if (string.IsNullOrEmpty(((TransitionPoint)target).GetGuid()))
{
    EditorGUILayout.HelpBox(
        "No WGE port assigned. Select a port from the dropdown to enable transitions.",
        MessageType.Warning);
}
```

**Apply modified properties and mark scene dirty after every change:**

```csharp
bool changed = serializedObject.ApplyModifiedProperties();
if (changed)
{
    EditorUtility.SetDirty(target);
    EditorSceneManager.MarkSceneDirty(((TransitionPoint)target).gameObject.scene);
}
```

This is required. The Inspector may display a selected port name, but `_selectedGuid` inside the
`PortsDropdown` struct is what gets baked into the build. Without `ApplyModifiedProperties` and
`SetDirty`, that value may not be saved to the scene file.

**Update `DrawValidationMessages()`:**

Remove any warnings or errors that reference the now-deleted `gateKey`, `targetScene`, or
`entryGateKey` properties. The GUID-empty warning is now drawn in the WGE port section above.

**Remove `DrawOutgoingTransition()` section entirely** (was drawing `targetScene` and `entryGateKey`).

**Update `DrawButtons()`:**

Remove the "Copy Gate Key" button. Replace with "Copy Passage GUID":

```csharp
if (GUILayout.Button("Copy Passage GUID"))
{
    EditorGUIUtility.systemCopyBuffer = ((TransitionPoint)target).GetGuid();
}
```

### Phase 6 — Update `TransitionGateLinkValidator`

**This phase must be completed before Phase 7 (scene migration).** Running the validator after
migration but before this update will report every migrated gate as an error.

**Replace the local gate key blank check:**

```csharp
// Before — errors on blank gateKey:
string key = gate.Gate.GateKey;
if (string.IsNullOrWhiteSpace(key)) { ... error ... }

// After — errors on blank GUID:
string guid = gate.Gate.GetGuid();
if (string.IsNullOrWhiteSpace(guid))
{
    Debug.LogError($"[Validator] '{sceneName}' gate '{gate.ObjectPath}' has no WGE port assigned.", gate.Gate);
    issues++;
    continue;
}
```

**Replace the duplicate key check with a duplicate GUID check:**

Two `TransitionPoint` objects in the same scene with the same WGE GUID is always an authoring
error. Check for duplicate `GetGuid()` values per scene.

**Replace source link validation:**

```csharp
// For each gate with a non-empty GetGuid():
// 1. Confirm container.CanPassTransition(guid, false, out status) returns true.
// 2. Resolve GetTransitionData(guid, false).
// 3. Confirm TargetSceneBuildIndex maps to an enabled Build Settings scene.
// 4. Open the target scene.
// 5. Confirm exactly one TransitionPoint in the target scene has GetGuid() == data.TargetPassageGuid.
```

Step 5 is important: a destination-only gate with no WGE port assigned will not be found by
`FindByPassageGuid`. The validator must report this as an error, not just a warning.

**Remove all legacy validation for `GateKey`, `TargetScene`, and `EntryGateKey`.** There is no
legacy path; those properties no longer exist.

The validator still needs to read the container. Provide the container to the validator through a
serialized `WorldGraphContainer` field on a `TransitionValidationSettings` ScriptableObject, or
load the container the same way the resolver does. Do not use `TransitionManager.Instance` (absent
at editor tool time when not in Play Mode); use `TransitionManager.LoadFromResources()` to get
the prefab reference and then read its `Container` property.

### Phase 7 — Scene Migration

**Run Phase 6 first.** The validator must be GUID-aware before migration begins or it will report
false errors for every gate.

With two gameplay scenes, migrate manually. For each `TransitionPoint` in each scene:

1. Select the GameObject in the scene.
2. In the Inspector "World Graph Port" section, choose the matching port from the dropdown.
3. Verify "GUID (read-only)" shows a non-empty value.
4. Verify "Target Scene (read-only)" shows the correct destination scene.
5. Save the scene.

Every `TransitionPoint` in every scene needs a WGE port — including destination-only gates (gates
with no outgoing transition). `FindByPassageGuid(targetPortGuid)` resolves arrivals; a destination
gate with no WGE GUID will never be found.

After all scenes are migrated, run `Tools/Project/Validate Transition Gate Links`. Expect zero
issues. If any gate reports a missing GUID, return to step 2 for that gate.

### Phase 8 — Update `GameManager`

Rename the `entryGateKey` parameter and update the `FindByGateKey` call:

```csharp
// Signature
public bool BeginSceneTransition(string targetScene, string destinationPassageGuid = "")

// Inside TransitionRoutine, replace:
TransitionPoint dest = !string.IsNullOrEmpty(entryGateKey)
    ? TransitionPoint.FindByGateKey(entryGateKey)
    : null;

if (dest == null && !string.IsNullOrEmpty(entryGateKey))
{
    Debug.LogError($"[GameManager] No TransitionPoint with key '{entryGateKey}' found ...");
    ...
}

// With:
TransitionPoint dest = !string.IsNullOrEmpty(destinationPassageGuid)
    ? TransitionPoint.FindByPassageGuid(destinationPassageGuid)
    : null;

if (dest == null && !string.IsNullOrEmpty(destinationPassageGuid))
{
    Debug.LogError($"[GameManager] No TransitionPoint with passage GUID '{destinationPassageGuid}' found ...");
    ...
}
```

No other changes to `GameManager`. Scene loading, respawn flow, hazard recovery, pause, and
hit-stop are unchanged.

---

## Runtime Rules

- Do not use WGE `TransitionManager` for gameplay scene loading.
- Keep WGE `Resources/TransitionManager.prefab` for editor tooling only. `_autoLoad` must be
  `false`. It must not be placed in Boot or gameplay scenes.
- Confirm no `WorldGraphEditor.TransitionManager` instance exists at runtime. Add a smoke-check
  assertion in `GameManager.OnSceneLoaded` or `Bootstrap.Start()`:
  ```csharp
  Debug.Assert(
      Object.FindAnyObjectByType<WorldGraphEditor.TransitionManager>() == null,
      "[Underbrew] WGE TransitionManager found at runtime. Disable _autoLoad on the WGE prefab.");
  ```
- Do not place `Passage2D`, `Teleport2D`, or WGE spawn point components on Underbrew gates.
- Do not call `SceneManager.LoadSceneAsync` from WGE adapters or the resolver.
- Do not move or spawn the hero from WGE code.
- Do not write hero velocity outside `HeroMotor`.
- Do not add WGE data to save files. WGE passage GUIDs must not appear in `PlayerSaveData` or
  `WorldSaveData`.
- Do not call `GameManager.SetActiveRespawnMarker` from `TransitionPoint`. `linkedRespawnMarker`
  remains reserved.
- `ignoreShortcuts: false` is the correct value for the first integration pass. Revisit when
  ability-gating or locked shortcuts are implemented.

---

## Manual Test Checklist

No Unity tests or playtests have been run as part of this strategy document. Validate in Editor in
this order.

**Step 0 (required before any play test):** Open the WGE editor window, or click into a
`TransitionPoint` Inspector to trigger a `Refresh`. Verify the "GUID (read-only)" field shows a
non-empty value on every gate before entering Play Mode. Without this, `GetGuid()` returns `null`
in the Editor and every transition will silently reject. This is an Editor-only session issue;
builds are not affected.

1. `FindAnyObjectByType<WorldGraphEditor.TransitionManager>()` returns `null` at startup. Check
   this before any transition test.
2. Every active `TransitionPoint` has a non-empty GUID visible in the "World Graph Port" Inspector section.
3. The Inspector does not show `gateKey`, `targetScene`, or `entryGateKey` fields anywhere.
4. One WGE-backed gate transitions to the correct target scene.
5. Destination hero placement uses `HeroSceneEntry` and `TransitionPoint.EntrySpawnPosition`.
6. Left, right, top, bottom, and door entry motion match the pre-WGE baseline.
7. `FindByPassageGuid(targetPassageGuid)` finds the destination `TransitionPoint` in the loaded scene.
8. A gate with no WGE port assigned logs the expected warning and rejects the transition.
9. Wrong-direction or invalid state still rejects with push-back correctly.
10. Door interact gates still require interact input.
11. Death and respawn use Underbrew `SaveManager` / `RespawnMarker`, not WGE spawn points.
12. No `Passage2D`, `Teleport2D`, or WGE spawn point components exist on Underbrew gates.
13. `TransitionGateLinkValidator` reports zero issues after Phase 7 migration.
14. Standalone build completes with WGE-backed transitions functional.
15. No `[TransitionPoint] ... has no WGE port assigned` warnings appear in a build log when all
    gates are correctly authored.

---

## Open Questions

1. **Container synchronization.** The same `WorldGraphContainer` is assigned to WGE's
   `Resources/TransitionManager.prefab` and Underbrew `Bootstrap`. If a future settings asset is
   introduced, define one owner and one explicit sync step in this document.
2. **Shortcut gating.** `ignoreShortcuts: false` is the first-pass default. When ability-gating
   or locked passage shortcuts are implemented, this value needs revisiting. Document the policy
   decision when it changes.
3. **Save migration.** No save data contains legacy gate keys in the current implementation (saves
   store respawn marker keys, not transition gate keys). No migration work required for existing
   save files.
4. **Assembly definitions.** Resolved: `WorldGraphEditor.asmdef` is `autoReferenced: true`. No
   `.asmdef` changes are needed for any Underbrew script to reference WGE types.

---

## Do-Not-Touch List

- **Hero feel tuning.** Do not modify movement, dash, jump, pogo, wall-slide, or attack values.
- **Scene loading flow.** Do not call `SceneManager.LoadSceneAsync` outside
  `GameManager.BeginSceneTransition`.
- **Physics ownership.** Do not write hero velocity except through `HeroMotor`.
- **Save/load timing.** Do not add new save triggers or call `SaveManager.Save()` outside
  checkpoint interactions.
- **Checkpoint respawn policy.** Do not wire `TransitionPoint.linkedRespawnMarker` or call
  `GameManager.SetActiveRespawnMarker` from gate traversal in this integration pass.
- **Audio and camera systems.** Do not integrate WGE audio or camera logic.
- **`HeroSceneEntry.cs`.** No WGE logic required. Only the `dest.GateKey` reference in the
  `GateSide.Unknown` log message must be updated (Phase 3).
- **`HeroMotor.cs`.** No changes required.
- **`GameManager` responsibilities.** The container reference goes to `Bootstrap`. Do not add
  WGE graph ownership to `GameManager`.

---

## Unity Editor Setup Checklist

Ordered by dependency. Do not skip ahead.

1. Open `Assets/WorldGraphEditor/Resources/TransitionManager.prefab`. Set `_autoLoad` to `false`.
   Save. Confirm no WGE output on Play.
2. Create `Assets/_Project/ScriptableObjects/World/UnderbrewWorldGraph.asset`
   (`WorldGraphContainer`).
3. Assign it to the WGE `TransitionManager.prefab` `_container` field.
4. Open the WGE editor window. Add scene nodes for all gameplay scenes. Connect them with edges.
   Add ports matching the existing gate names and directions.
5. Assign `UnderbrewWorldGraph.asset` to the Boot scene `Bootstrap` component
   `worldGraphContainer` field.
6. On each `TransitionPoint` in each scene: select the matching port. Verify GUID and Target Scene
   populate in the Inspector.
7. Save all modified scenes.
8. Confirm no Underbrew gate has `Passage2D`, `Teleport2D`, or WGE spawn point components.
9. Run `Tools/Project/Validate Transition Gate Links`. Expect zero issues.
10. Make a standalone build. Test all transitions. Confirm the Console is clean.
