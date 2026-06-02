# WGE Integration Strategy for Underbrew

*Revised 2026-06-02. Updated after checking the actual World Graph Editor source. Earlier
strategy notes treated WGE API details as unverified and recommended a companion binding
component. The verified API changes that recommendation: `TransitionPoint` can directly inherit
from `WorldGraphEditor.PassageBase` while Underbrew keeps ownership of runtime scene loading,
spawn placement, save flow, camera, audio, and hero physics.*

---

## Executive Summary

Underbrew's scene-transition architecture is deliberately narrow. Every transition funnels through
`GameManager.BeginSceneTransition(string targetScene, string entryGateKey)`. The destination gate is
resolved by `TransitionPoint.FindByGateKey(key)`, and `HeroSceneEntry` owns spawn placement and entry
motion by reading the resolved `TransitionPoint`.

World Graph Editor (WGE) provides a useful graph-authoring layer through `WorldGraphContainer` and
`PassageBase`. After checking the actual WGE source, `PassageBase` is safe enough for direct
integration:

- It does **not** require `Traverse()`.
- It does **not** call `TransitionManager`.
- It does **not** load scenes or move/spawn the hero.
- Its runtime surface is effectively serialized port state plus `GetGuid()`.
- The only abstract method left for `TransitionPoint` is `GetSpawnPosition()`.

The unsafe WGE runtime path lives in `Passage2D`, `TeleportBase` derivatives, and
`TransitionManager`. Those should not be used by Underbrew gates.

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

## What WGE Provides

| Category | Key systems | Notes |
|---|---|---|
| Graph authoring | `WorldGraphContainer` holds scene nodes and connection data. | Authoring happens in the WGE editor window. |
| Port selection | `PassageBase` owns the selected port data, assigned GUID, target scene, and `GetGuid()`. | This is the part Underbrew should reuse directly. |
| Runtime graph resolution | `WorldGraphContainer.CanPassTransition(sourceGuid, ignoreShortcuts, out status)` and `GetTransitionData(sourceGuid, false)`. | Use these to resolve source port GUID to target scene build index and target port GUID. |
| Runtime transitions | `Passage2D`, `TeleportBase` derivatives, and `TransitionManager`. | Do **not** use for Underbrew runtime transitions. They conflict with `GameManager.BeginSceneTransition`. |
| Spawn points | WGE spawn point components. | Do **not** use. Underbrew already owns spawn placement through `HeroSceneEntry`, `RespawnMarker`, and `TransitionPoint` entry data. |
| Editor validation | WGE graph consistency tooling. | Useful alongside `TransitionGateLinkValidator`. |

---

## What Underbrew Keeps

| Category | Owner | Rule |
|---|---|---|
| Scene loading | `GameManager.BeginSceneTransition` | The only code path that loads gameplay scenes. |
| Destination lookup | `TransitionPoint.FindByGateKey` | Must compare against `candidate.GateKey` so GUID-backed gates can be found. |
| Spawn placement and entry motion | `HeroSceneEntry`, `TransitionPoint` entry fields | WGE never moves or spawns the hero. |
| Hero physics | `HeroMotor` | WGE never writes hero velocity. |
| Save and respawn | `SaveManager`, `RespawnMarker`, checkpoint and hazard flows | WGE data must not appear in save data in this first integration pass. |
| Camera and audio | Existing Underbrew camera/audio systems | No WGE camera/audio integration. |

---

## Direct Refactor Plan

### Phase 0 — Read-Only Safety Pass

1. Import WGE under `Assets/WorldGraphEditor/`.
2. Confirm Unity compiles.
3. Do not add WGE `TransitionManager` prefabs to Boot or gameplay scenes.
4. Do not add `Passage2D`, `Teleport2D`, or WGE spawn point components to Underbrew gates.
5. Create or confirm a `WorldGraphContainer` asset for the Underbrew scenes.
6. Add both scenes to Build Settings and graph them in WGE.
7. Add ports matching the existing Underbrew gates.

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
7. Convert build index to scene name with `SceneUtility.GetScenePathByBuildIndex(buildIndex)`.
8. Validate the scene name with `Application.CanStreamedLevelBeLoaded`.
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

The editor must provide a `RefreshContext`. Since `RefreshContext` only needs an
`ITransitionManager` with a `WorldGraphContainer`, create a small editor-only adapter instead of
depending on WGE `TransitionManager`.

The editor still needs a source for the graph asset. Recommended first pass: create a small
Underbrew settings asset for the `WorldGraphContainer`. This avoids scene scanning and keeps the
editor independent of whether the Boot scene is loaded.

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
- Do not place WGE `TransitionManager` prefabs in Boot or gameplay scenes.
- Do not put `Passage2D`, `Teleport2D`, or WGE spawn point components on Underbrew gates.
- Do not call `SceneManager.LoadSceneAsync` from WGE adapters.
- Do not move or spawn the hero from WGE code.
- Do not write hero physics outside `HeroMotor`.
- Do not add WGE data to save files in the first integration pass.
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
9. No `TransitionManager` prefab exists in Boot or gameplay scenes.
10. No `Passage2D` or `Teleport2D` components are on Underbrew gates.
11. Standalone build completes with all WGE-backed and legacy transitions functional.

---

## Open Questions Before Implementation

1. **Container settings location.** Should the editor read the `WorldGraphContainer` from a new
   Underbrew settings asset, from `Bootstrap`, or from an existing WGE global setting?
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

Keep Underbrew runtime ownership by resolving WGE graph data into:

```csharp
GameManager.Instance.BeginSceneTransition(request.TargetSceneName, request.TargetPortGuid);
```

and make the essential lookup fix:

```csharp
candidate.GateKey == key
```

instead of comparing against the private legacy `gateKey` field.
