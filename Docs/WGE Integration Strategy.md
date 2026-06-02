# WGE Integration Strategy for Underbrew

*Revised 2026-06-02. Updated after checking the actual World Graph Editor source from
`Perkiiii/World-Graph-Editor-v1.2` on GitHub. Earlier strategy notes treated WGE API details as
unverified and recommended a companion binding component. The verified API changes that
recommendation: `TransitionPoint` can directly inherit from `WorldGraphEditor.PassageBase` while
Underbrew keeps ownership of runtime scene loading, spawn placement, save flow, camera, audio, and
hero physics.*

---

## Executive Summary

Underbrew's scene-transition architecture is deliberately narrow. Every transition funnels through
`GameManager.BeginSceneTransition(string targetScene, string entryGateKey)`. The destination gate is
resolved by `TransitionPoint.FindByGateKey(key)`, and `HeroSceneEntry` owns spawn placement and entry
motion by reading the resolved `TransitionPoint`.

World Graph Editor (WGE) provides the authoring layer Underbrew wants: a graph-backed port dropdown
that automatically stores an assigned GUID and displays the target scene. In WGE's own `Passage2D`,
that good authoring UX comes from `PassageBase`, not from `Passage2D`'s runtime traversal logic.
After checking the actual WGE source, `PassageBase` is safe enough for direct integration:

- It does **not** require `Traverse()`.
- It does **not** call `TransitionManager`.
- It does **not** load scenes or move/spawn the hero.
- Its runtime surface is effectively serialized port state plus `GetGuid()`.
- The only abstract method left for `TransitionPoint` is `GetSpawnPosition()`.

The unsafe WGE runtime path lives in `Passage2D`, `TeleportBase` derivatives, and
`TransitionManager`. Those should not be used by Underbrew gates. The goal is to reuse the
`PassageBase` authoring workflow that makes `Passage2D` convenient, while rejecting
`Passage2D`'s trigger-driven runtime transition behaviour.

**Recommended integration:** refactor `TransitionPoint` directly to inherit `PassageBase`, use the
selected WGE port GUID as the gate key when present, resolve graph data into
`GameManager.BeginSceneTransition(targetScene, targetPortGuid)`, and keep the existing legacy string
fields as fallback during migration.

---

## Key Corrections From The Earlier Strategy

| Earlier assumption | Verified finding | Strategy update |
|---|---|---|
| Composition is safer because `PassageBase` may have runtime side effects. | `PassageBase` has no meaningful runtime transition side effects. | Use direct inheritance: `TransitionPoint : PassageBase`. |
| `PassageBase` may require `Traverse()`. | `Traverse()` is not part of `PassageBase` or `ITransitionComponent`; `Passage2D` defines its own runtime `Traverse()`. | No no-op `Traverse()` adapter is needed. |
| `TransitionPoint` should avoid any WGE compile dependency. | This project is intentionally integrating WGE as part of the workflow. | Accept the compile dependency in `TransitionPoint`; keep WGE runtime scene-loading components out of Underbrew scenes. |
| `FindByGateKey` already works if `GateKey` returns a GUID. | Current code compares against the private `candidate.gateKey`, not the public `GateKey` property. | Change `FindByGateKey` to compare `candidate.GateKey`. This is required for GUID destination lookup. |
| A sibling `WorldGraphPortBinding : PassageBase` is needed. | `TransitionPoint` can safely satisfy `PassageBase` directly with `GetSpawnPosition()`. | Do not add companion components unless direct inheritance becomes blocked by a future WGE API change. |

`TransitionPoint` is currently `sealed`, but this does not block direct inheritance from
`PassageBase`. It only prevents other classes from inheriting from `TransitionPoint`. Keep
`TransitionPoint` sealed.

---

## GitHub Audit Findings

The following details were verified against `Perkiiii/World-Graph-Editor-v1.2` on GitHub:

- **`PassageBase` is the useful integration surface.** It owns `_assignedPort`, editor-only
  `_assignedGuid`, editor-only `_targetScene`, `Refresh(RefreshContext)`, `GetGuid()`, and abstract
  `GetSpawnPosition()`.
- **`PortsDropdown` is build-safe after refresh/selection.** It serializes `_selectedGuid`; in builds,
  `GetSelectedValue()` returns that serialized GUID directly. In the Editor, refresh/selection keeps
  `_selectedGuid` synchronized with the selected display name.
- **`Passage2D` is runtime-incompatible with Underbrew.** `OnTriggerEnter2D` calls `Traverse()`, and
  `Traverse()` calls `TransitionManager.Instance.GoFrom(GetGuid(), true)`. That bypasses
  `GameManager.BeginSceneTransition`.
- **`TransitionManager` conflicts with Underbrew runtime ownership.** It loads scenes, finds WGE
  transition components, calculates spawn positions, may instantiate a player, and invokes WGE
  transition events.
- **WGE editor tooling expects its manager prefab.** `TransitionComponentRefresher`, WGE validation,
  overlays, and build preprocessing read the container through `Resources/TransitionManager.prefab`.
  Keep that prefab for editor tooling, but disable runtime auto-load.

---

## What WGE Provides

| Category | Key systems | Notes |
|---|---|---|
| Graph authoring | `WorldGraphContainer` holds scene nodes and connection data. | Authoring happens in the WGE editor window. |
| Port selection | `PassageBase` owns the selected port data, assigned GUID, target scene, and `GetGuid()`. | This is the part Underbrew should reuse directly. |
| Port dropdown persistence | `PortsDropdown` serializes `_selectedGuid`. | After refresh/selection, GUID lookup works in builds. |
| Runtime graph resolution | `WorldGraphContainer.CanPassTransition(sourceGuid, ignoreShortcuts, out status)` and `GetTransitionData(sourceGuid, false)`. | Use these to resolve source port GUID to target scene build index and target port GUID. |
| Runtime transitions | `Passage2D`, `TeleportBase` derivatives, and `TransitionManager`. | Do **not** use for Underbrew runtime transitions. They conflict with `GameManager.BeginSceneTransition`. |
| Spawn points | WGE spawn point components. | Do **not** use. Underbrew already owns spawn placement through `HeroSceneEntry`, `RespawnMarker`, and `TransitionPoint` entry data. |
| Editor refresh/validation | WGE graph consistency tooling, `TransitionComponentRefresher`, and build preprocessing. | Useful alongside `TransitionGateLinkValidator`; expects WGE `Resources/TransitionManager.prefab` to exist with a container assigned. |

---

## What Underbrew Keeps

| Category | Owner | Rule |
|---|---|---|
| Scene loading | `GameManager.BeginSceneTransition` | The only code path that loads gameplay scenes. |
| Destination lookup | `TransitionPoint.FindByGateKey` | Must compare against `candidate.GateKey` so GUID-backed gates can be found. |
| Spawn placement and entry motion | `HeroSceneEntry`, `TransitionPoint` entry fields | WGE never moves or spawns the hero. |
| Hero physics | `HeroMotor` | WGE never writes hero velocity. |
| Save and respawn | `SaveManager`, `RespawnMarker`, checkpoint and hazard flows | WGE data must not appear in save data in this first integration pass. `TransitionPoint.linkedRespawnMarker` remains reserved and is not consumed. |
| Camera and audio | Existing Underbrew camera/audio systems | No WGE camera/audio integration. |

---

## Direct Refactor Plan

### Phase 0 — Read-Only Safety Pass

1. Import WGE under `Assets/WorldGraphEditor/`.
2. Confirm Unity compiles.
3. Keep WGE `Assets/WorldGraphEditor/Resources/TransitionManager.prefab` for editor tooling.
4. Assign the Underbrew `WorldGraphContainer` asset to that WGE manager prefab.
5. Set the WGE manager prefab's `_autoLoad` to `false` so WGE `Bootstrapper` does not create a
   runtime `TransitionManager`.
6. Do not add WGE `TransitionManager` instances to Boot or gameplay scenes.
7. Do not add `Passage2D`, `Teleport2D`, or WGE spawn point components to Underbrew gates.
8. Create or confirm a `WorldGraphContainer` asset for the Underbrew scenes.
9. Add both scenes to Build Settings and graph them in WGE.
10. Add ports matching the existing Underbrew gates.

### Phase 1 — Add A Resolver, No Scene Loading

Create:

```text
Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionRequest.cs
Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionResolver.cs
```

`WorldGraphTransitionResolver.TryResolve(...)` should:

1. Reject null container.
2. Reject empty source port GUID.
3. Call `container.Initialize()` if needed.
4. Call `container.CanPassTransition(sourcePortGuid, ignoreShortcuts, out status)`.
5. Call `container.GetTransitionData(sourcePortGuid, false)` when passage is allowed.
6. Read `RuntimeTransitionData.TargetPassageGuid` and `RuntimeTransitionData.TargetSceneBuildIndex`.
7. Convert build index to a scene name explicitly:

```csharp
string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
string sceneName = Path.GetFileNameWithoutExtension(path);
```

8. Validate `sceneName`, not the `.unity` path, with `Application.CanStreamedLevelBeLoaded`.
9. Return pure data only.

The resolver must never call `SceneManager.LoadSceneAsync`, instantiate a player, move the hero, or
touch WGE `TransitionManager`.

Recommended request shape:

```csharp
public readonly struct WorldGraphTransitionRequest
{
    public readonly bool IsValid;
    public readonly string SourcePortGuid;
    public readonly string TargetPortGuid;
    public readonly int TargetSceneBuildIndex;
    public readonly string TargetSceneName;
    public readonly TransitionPassStatusType PassStatus;
    public readonly string FailureReason;
}
```

### Phase 2 — Give Underbrew A Container Reference

Use `Bootstrap`, not `GameManager`, as the first-pass owner of the WGE container reference.
`Bootstrap` already owns startup initialization before the first scene transition, so it is the
cleanest place to assign startup graph configuration without expanding `GameManager`.

Add:

```csharp
[SerializeField] private WorldGraphContainer worldGraphContainer;
```

Then set the resolver reference during startup:

```csharp
WorldGraphTransitionResolver.SetContainer(worldGraphContainer);
```

Do not put WGE transition ownership into `GameManager`.

Assign the same `WorldGraphContainer` asset to:

- Underbrew `Bootstrap`, for runtime resolver access.
- WGE `Assets/WorldGraphEditor/Resources/TransitionManager.prefab`, for WGE editor refresh,
  validation, overlays, and build preprocessing.

The WGE manager prefab must remain an editor-tooling container source only. Its `_autoLoad` field
must be disabled so `WorldGraphEditor.Bootstrapper` does not instantiate `TransitionManager` before
scene load.

### Phase 3 — Refactor `TransitionPoint`

Change:

```csharp
public sealed class TransitionPoint : MonoBehaviour
```

to:

```csharp
using WorldGraphEditor;

public sealed class TransitionPoint : PassageBase
```

Implement:

```csharp
public override Vector3 GetSpawnPosition()
{
    return EntrySpawnPosition;
}
```

Add a graph-aware key:

```csharp
public string GraphPortGuid => GetGuid();

public string GateKey
{
    get
    {
        string graphGuid = GetGuid();
        return !string.IsNullOrEmpty(graphGuid) ? graphGuid : gateKey;
    }
}
```

Then fix destination lookup:

```csharp
if (candidate == null || candidate.GateKey != key)
    continue;
```

That `FindByGateKey` change is essential. Without it, `GameManager` will pass the target WGE GUID,
but destination lookup will still compare against the old private string field.

### Phase 4 — Prefer WGE In `TryActivate` When Assigned

Preserve existing activation guards:

- local transition guard
- hero null checks
- door/interact checks
- `ShouldRejectActivation`
- wrong-direction push-back
- `GameManager.Instance.State == GameState.Playing`
- `localTransitionGuard = true` only after transition starts

Do **not** add `GameManager.SetActiveRespawnMarker(linkedRespawnMarker)` in this WGE pass.
Underbrew's current design explicitly keeps gate traversal separate from checkpoint respawn policy:
death after crossing a gate returns to the last activated checkpoint, not to the entry door.
`linkedRespawnMarker` remains reserved for a future policy pass.

Replace only the destination resolution section:

```csharp
string graphGuid = GetGuid();

if (!string.IsNullOrEmpty(graphGuid))
{
    if (!WorldGraphTransitionResolver.TryResolve(
            graphGuid,
            ignoreShortcuts: false,
            out WorldGraphTransitionRequest request))
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
}
```

Then keep the existing legacy path beneath it:

```csharp
if (string.IsNullOrEmpty(targetScene)) { ... }
if (string.IsNullOrEmpty(entryGateKey)) { ... }
GameManager.Instance.BeginSceneTransition(targetScene, entryGateKey);
```

This supports partial migration: WGE-backed gates use graph data, and unassigned gates continue to
use legacy `targetScene` / `entryGateKey`.

### Phase 5 — Update `TransitionPointEditor`

The current `TransitionPointEditor` is fully custom and manually draws fields. Do not call
`base.OnInspectorGUI()` and do not replace it with WGE's inspector.

Add inherited `PassageBase` serialized properties:

```csharp
private SerializedProperty assignedPort;
private SerializedProperty assignedGuid;
private SerializedProperty targetSceneFromGraph;
```

Find them in `OnEnable()`:

```csharp
assignedPort = serializedObject.FindProperty("_assignedPort");
assignedGuid = serializedObject.FindProperty("_assignedGuid");
targetSceneFromGraph = serializedObject.FindProperty("_targetScene");
```

Draw a WGE section near the top or after the outgoing transition section:

```csharp
DrawSection("World Graph Editor");

RefreshPassageBaseData();

EditorGUILayout.PropertyField(assignedPort, new GUIContent("Assigned Port"));

using (new EditorGUI.DisabledScope(true))
{
    EditorGUILayout.PropertyField(assignedGuid, new GUIContent("Assigned GUID"));
    EditorGUILayout.PropertyField(targetSceneFromGraph, new GUIContent("Target Scene"));
}
```

After `RefreshPassageBaseData()` and any dropdown change, persist scene-object data:

```csharp
bool changed = serializedObject.ApplyModifiedProperties();
if (changed)
{
    EditorUtility.SetDirty(target);
    EditorSceneManager.MarkSceneDirty(((TransitionPoint)target).gameObject.scene);
}
```

This is important because the inspector may display a valid selected port, assigned GUID, and target
scene, but builds rely on the serialized `_selectedGuid` stored by `PortsDropdown`.

The editor must provide a `RefreshContext`. Since `RefreshContext` only needs an
`ITransitionManager` with a `WorldGraphContainer`, the simplest first pass is to use WGE's existing
`Resources/TransitionManager.prefab` as the container source. WGE's own
`TransitionComponentRefresher` and validation path already depend on that prefab, so this avoids
forking the refresh model.

Only create a separate Underbrew editor settings asset if WGE's refresh tooling is intentionally
customized later. If that happens, keep it synchronized with the container assigned to `Bootstrap`
and document the reason.

### Phase 6 — Validator Pass

Update `TransitionGateLinkValidator` to support both modes.

Legacy validation remains:

- `gateKey` uniqueness per scene.
- `targetScene` exists.
- `entryGateKey` exists in target scene.

WGE validation for every `TransitionPoint` with a non-empty `GetGuid()`:

- Confirm the GUID exists in the `WorldGraphContainer`.
- Confirm `CanPassTransition(guid, false, out status)` passes, unless intentionally validating blocked shortcuts.
- Resolve `GetTransitionData(guid, false)`.
- Confirm `TargetSceneBuildIndex` maps to a Build Settings scene.
- Open or load the target scene in editor validation context.
- Confirm exactly one `TransitionPoint` in the target scene has `GateKey == targetGuid`.

This matters because `PortsDropdown` is build-safe only when `_selectedGuid` has been populated
before build.

### Phase 7 — Scene Migration

Because there are currently only two gameplay scenes, migrate manually first instead of writing a
migration tool.

For each gate:

1. Select the `TransitionPoint`.
2. In the WGE inspector section, choose the matching graph port.
3. Verify Assigned GUID is populated.
4. Verify Target Scene is populated.
5. Keep old `gateKey`, `targetScene`, and `entryGateKey` fields for one or two test passes.
6. Once both scenes work through WGE, mark legacy fields as fallback/legacy in the inspector, but do not delete them yet.

Destination-only gates still need selected WGE ports because `GameManager` resolves arrivals by the
target port GUID.

---

## Runtime Rules

- Do not use WGE `TransitionManager` for gameplay scene loading.
- Keep WGE `Resources/TransitionManager.prefab` for editor tooling, with the graph container
  assigned and `_autoLoad` set to `false`.
- Do not place WGE `TransitionManager` instances in Boot or gameplay scenes.
- Confirm no WGE `TransitionManager` instance is created at runtime.
- Do not put `Passage2D`, `Teleport2D`, or WGE spawn point components on Underbrew gates.
- Do not call `SceneManager.LoadSceneAsync` from WGE adapters.
- Do not move or spawn the hero from WGE code.
- Do not write hero physics outside `HeroMotor`.
- Do not add WGE data to save files in the first integration pass.
- Do not call `GameManager.SetActiveRespawnMarker` from `TransitionPoint`; `linkedRespawnMarker`
  remains reserved and unused at runtime.
- Keep legacy gate fields until every scene has been migrated and tested.

---

## Manual Test Checklist

No Unity tests or playtests have been run for this strategy. Validate in Editor in this order:

1. Legacy transition still works with no WGE port assigned.
2. One WGE-backed gate transitions to the correct target scene.
3. Destination hero placement still uses `HeroSceneEntry` and `TransitionPoint.EntrySpawnPosition`.
4. Left, right, top, bottom, and door entry motion match the pre-WGE baseline.
5. `FindByGateKey(targetGuid)` finds the destination WGE-backed `TransitionPoint`.
6. Wrong-direction or invalid activation still rejects and applies push-back correctly.
7. Door interact gates still require interact.
8. Death and respawn still use Underbrew `SaveManager` / `RespawnMarker`, not WGE spawn points.
9. WGE `Resources/TransitionManager.prefab` exists for editor tooling and has `_autoLoad` disabled.
10. Add a temporary integration smoke check that logs or asserts if
    `FindAnyObjectByType<WorldGraphEditor.TransitionManager>()` returns a runtime instance.
11. No WGE `TransitionManager` instance is created at runtime.
12. No `Passage2D`, `Teleport2D`, or WGE spawn point components are on Underbrew gates.
13. Standalone build completes with all WGE-backed and legacy transitions functional.

---

## Open Questions Before Implementation

1. **Container synchronization.** The same `WorldGraphContainer` must be assigned to WGE's
   `Resources/TransitionManager.prefab` and Underbrew `Bootstrap`. If a future settings asset is
   introduced, define one owner and one synchronization path.
2. **Shortcut gating.** WGE distinguishes normal edges, one-way edges, and shortcuts. For now use
   `ignoreShortcuts = false`; revisit when ability gating or locked shortcuts are implemented.
3. **Scene name format.** Confirm whether `GameManager.BeginSceneTransition` expects the scene name
   without `.unity`; prefer stripping the extension after `SceneUtility.GetScenePathByBuildIndex`.
4. **Save migration.** Existing save files may contain legacy gate keys. Keep legacy fallback fields
   until save/load behavior is verified after full gate migration.
5. **Assembly definitions.** Determine whether WGE already ships in its own assembly. If not, plan
   assembly boundaries carefully before excluding WGE runtime examples or demo scripts.

---

## Do-Not-Touch List

- **Hero feel tuning.** Do not modify movement, dash, jump, pogo, wall-slide, or attack values.
- **Scene loading flow.** Do not call `SceneManager.LoadSceneAsync` outside `GameManager.BeginSceneTransition`.
- **Physics ownership.** Do not write hero velocity except through `HeroMotor`.
- **Save/load timing.** Do not add new save points or call `SaveManager.Save()` outside checkpoint interactions.
- **Checkpoint respawn policy.** Do not wire `TransitionPoint.linkedRespawnMarker` or call
  `GameManager.SetActiveRespawnMarker` from gate traversal in this integration pass.
- **Audio and camera systems.** Do not integrate WGE audio or camera logic.
- **`HeroSceneEntry.cs`.** No WGE changes required.
- **`HeroMotor.cs`.** No WGE changes required.

---

## Summary Of Final Recommendation

Refactor from:

```csharp
public sealed class TransitionPoint : MonoBehaviour
```

to:

```csharp
public sealed class TransitionPoint : PassageBase
```

because the verified WGE API is safe for direct authoring integration: only
`GetSpawnPosition()` is abstract, `PassageBase` has no runtime scene-loading behavior, and the
dangerous WGE runtime path lives outside `PassageBase`.

Do **not** use `Passage2D` as the gate component even though its inspector has the desired Assigned
GUID / Target Scene workflow. That workflow comes from `PassageBase`; `Passage2D` adds the forbidden
runtime trigger path through WGE `TransitionManager`.

Keep Underbrew runtime ownership by resolving WGE graph data into:

```csharp
GameManager.Instance.BeginSceneTransition(request.TargetSceneName, request.TargetPortGuid);
```

and make the essential lookup fix:

```csharp
candidate.GateKey == key
```

instead of comparing against the private legacy `gateKey` field.

---

## Unity Editor Setup Required

1. Import WGE under `Assets/WorldGraphEditor/`.
2. Create or confirm the Underbrew `WorldGraphContainer` asset.
3. Assign that container to `Assets/WorldGraphEditor/Resources/TransitionManager.prefab`.
4. Disable `_autoLoad` on the WGE `TransitionManager` prefab.
5. Assign the same container to the Boot scene's `Bootstrap` component.
6. Select WGE ports on each `TransitionPoint`; verify Assigned GUID and Target Scene populate.
7. Confirm no Underbrew gate uses `Passage2D`, `Teleport2D`, or WGE spawn point components.
