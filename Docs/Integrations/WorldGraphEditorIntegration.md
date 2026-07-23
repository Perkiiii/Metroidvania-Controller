# World Graph Editor Integration

**Last audited:** 2026-06-04

This document is the canonical Underbrew record for the current World Graph Editor (WGE) integration. Older notes in `Docs/WGE Integration Strategy.md` and `Docs/WGE Integration Outcome.md` remain useful history, but this file is the upgrade and ownership checklist.

---

## 1. Overview

World Graph Editor is used in Underbrew primarily as an editor-time world / scene graph authoring and validation tool. It provides scene nodes, port GUIDs, connections, graph-backed port selection, and graph resolution data.

Underbrew still owns runtime scene flow. WGE may resolve "source port -> target scene / target port" data, but gameplay transitions continue through `TransitionPoint` and `GameManager.BeginSceneTransition(...)`. WGE must not become the runtime scene-loading authority unless a future architecture decision explicitly changes that boundary.

The ideal direction is an Underbrew adapter around official WGE extension points, not deeper ownership transfer to WGE runtime systems.

---

## 2. Current Integration Status

Verified from current project source:

| Concern | Current status |
|---|---|
| Graph asset | `Assets/_Project/ScriptableObjects/World/UnderbrewWorldGraph.asset` exists and serializes WGE scene nodes, ports, and connections. |
| Graph window / graph opens | Existing docs record successful graph authoring and validation, but this audit did not open Unity or the WGE window. |
| Scene nodes / ports | Scene nodes and port GUIDs are serialized in `UnderbrewWorldGraph.asset`; `TransitionPoint` components serialize WGE `_assignedPort` data in gameplay scenes. |
| Underbrew bridge | `TransitionPoint` derives from WGE `PassageBase` and implements `ITransitionComponent`, then routes activation through Underbrew transition flow. |
| Runtime graph resolution | `WorldGraphTransitionResolver` resolves a source WGE port GUID to target scene name and target port GUID. |
| Runtime scene loading | Still owned by `GameManager.BeginSceneTransition(...)`; WGE `TransitionManager.GoFrom`, `GoTo`, and `LoadScene` are bypassed. |
| Runtime WGE managers | `Bootstrap.Start()` asserts that no WGE `TransitionManager` exists at runtime. WGE `TransitionManager.prefab` must keep `_autoLoad` disabled. |
| WGE runtime components | `Passage2D`, `Teleport2D`, WGE spawn points, and WGE player spawning are not part of Underbrew gameplay scenes. |
| Editor validation | `TransitionGateLinkValidator` validates WGE passage GUIDs and graph links for enabled Build Settings scenes. |

Observed validation and play behavior are recorded in `Docs/WGE Integration Outcome.md`; this audit did not rerun Unity validation.

---

## 3. Modified WGE Files

Git currently reports the tracked WGE tree under `Assets/WorldGraphEditor/` as deleted and the current tree under `Assets/Plugins/WorldGraphEditor/` as untracked. That means this audit can verify relocation and content differences against the previous tracked tree, but it cannot prove which changes differ from the plugin developer's pristine package unless a clean upstream import is available.

| Path | Purpose | What changed / why | Need | Upgrade risk | Move to Underbrew adapter later? |
|---|---|---|---|---|---|
| `Assets/Plugins/WorldGraphEditor/` | Vendor plugin root | Current WGE files live under `Assets/Plugins/WorldGraphEditor` instead of the previously tracked `Assets/WorldGraphEditor`. This keeps the paid plugin under the project plugin area. | Essential in current repo layout | Medium | No; keep vendor root stable during upgrades. |
| `Assets/Plugins/WorldGraphEditor/Scripts/Editor/Utilities/WGEAssetPathUtility.cs` | Editor asset path resolution | Current file dynamically discovers the WGE root via `WGEEditor.asmdef`, normalizes legacy `Assets/WorldGraphEditor/` paths, and defaults to `Assets/Plugins/WorldGraphEditor`. This appears to be an Underbrew adaptation for relocated plugin assets. | Essential while WGE lives under `Assets/Plugins` | High | Ideally unnecessary if upstream supports relocatable plugin roots. |
| `Assets/Plugins/WorldGraphEditor/Scripts/Editor/Utilities/GraphUtility.cs` and related editor utility files | Load WGE styles/assets and graph editor UI resources | Current files rely on `WGEAssetPathUtility` rather than hardcoded old-root paths. This is likely part of the relocation adaptation. | Essential for editor UI after relocation | Medium | Prefer upstream path utility support if available. |
| `Assets/Plugins/WorldGraphEditor/Scripts/Editor/EditorWindows/WorldBuilderGraph*.cs`, edge/node/inspector utility files | WGE graph window, node, edge, and inspector rendering | Git indicates several editor files differ from the prior tracked tree. Based on current source, many differences appear related to relocatable asset loading and editor behavior, but exact upstream delta is uncertain. | Likely essential for current graph editor behavior | Medium | Prefer replacing custom edits with upstream fixes where possible. |
| `Assets/Plugins/WorldGraphEditor/Resources/TransitionManager.prefab` | WGE runtime manager prefab and editor tooling source | Must have `_autoLoad` disabled so WGE's `Bootstrapper` does not create a runtime `TransitionManager` before Underbrew boot. Existing docs state this prefab override was set to false; verify after every WGE import. | Essential | High | No; if future WGE exposes a project setting, use that instead. |
| WGE docs/screenshots/settings assets | Plugin documentation and editor state | Git reports content differences in PDFs, screenshots, and undo/redo/settings assets. These may be imported version differences or editor-generated artifacts, not necessarily deliberate integration patches. | Optional / tooling | Low to Medium | No. |

Files that are important to understand but not known to be modified from upstream:

| Path | Why it matters |
|---|---|
| `Scripts/Passages/Abstract/PassageBase.cs` | `TransitionPoint` inherits from this. It stores `_assignedPort`, exposes `GetGuid()`, and has editor-only `Refresh(...)`. |
| `Scripts/Data/PortsDropdown.cs` | Stores selected port GUID. In editor, refresh can replace stale selections; Underbrew guards against this in `TransitionPointEditor`. |
| `Scripts/Managers/Bootstrapper.cs` | Runs before scene load and creates WGE `TransitionManager` if `_autoLoad` is true. |
| `Scripts/Managers/TransitionManager.cs` | WGE runtime scene-loading path. Underbrew must not call it for gameplay transitions. |

---

## 4. Underbrew Adapter / Integration Files

| Path | Role | Kind | WGE dependency | Underbrew systems touched | Inspector setup |
|---|---|---|---|---|---|
| `Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionResolver.cs` | Static adapter that resolves source port GUIDs into target scene names and target port GUIDs. | Runtime | `WorldGraphContainer`, `RuntimeTransitionData`, `TransitionPassStatusType` | Used by `TransitionPoint`; does not load scenes. | None directly; container is supplied by `Bootstrap`. |
| `Assets/_Project/Scripts/WorldGraph/WorldGraphTransitionRequest.cs` | Data result for resolved or failed graph transition lookup. | Runtime data | `TransitionPassStatusType` | Used by `TransitionPoint`. | None. |
| `Assets/_Project/Scripts/Scene/TransitionPoint.cs` | Underbrew trigger/door bridge. Reads WGE GUID, asks resolver for destination, then calls `GameManager.BeginSceneTransition(...)`. Provides destination spawn data to `HeroSceneEntry`. | Runtime | `PassageBase`, `ITransitionComponent` | `GameManager`, `HeroController`, `HeroSceneEntry`, `HeroMotor`, `RespawnMarker` reserved field. | Assign a WGE port in the custom Inspector; set gate side, entry offset/facing, door settings, and entry tuning. |
| `Assets/_Project/Scripts/Managers/Bootstrap.cs` | Owns the `WorldGraphContainer` reference for runtime resolver setup and asserts no WGE runtime manager exists. | Runtime boot | `WorldGraphContainer`, `TransitionManager` for assert only | `GameManager`, `SaveManager`, `AudioManager`, `GameCameras`, `InteractManager`. | Boot scene `Bootstrap` must assign `UnderbrewWorldGraph.asset`. |
| `Assets/_Project/Scripts/Editor/Inspectors/TransitionPointEditor.cs` | Custom Inspector for WGE port assignment plus Underbrew gate fields. Protects serialized GUIDs from WGE refresh mutation. | Editor-only | `TransitionManager.LoadFromResources()`, `RefreshContext`, WGE serialized fields on `PassageBase` | `TransitionPoint` authoring. | Use "Refresh World Graph Data" after graph edits; re-select stale ports when warned. |
| `Assets/_Project/Scripts/Editor/Validation/TransitionGateLinkValidator.cs` | Build Settings scene scanner for blank/duplicate WGE passage GUIDs and graph target mismatches. | Editor-only | `WorldGraphContainer`, `TransitionManager.LoadFromResources()`, `RuntimeTransitionData`, `TransitionPassStatusType` | Transition gate authoring and Build Settings scene list. | Run `Tools/Project/Validate Transition Gate Links` after graph changes or plugin updates. |
| `Assets/_Project/ScriptableObjects/World/UnderbrewWorldGraph.asset` | WGE graph asset containing scene, port, and connection data. | Data-only | `WorldGraphContainer` | Transition graph data consumed by the resolver. | Assign to Boot `Bootstrap`; also assign to WGE `TransitionManager.prefab` for editor tooling. |

---

## 5. Runtime Ownership Rules

| Concern | Owner | WGE role | Notes |
|---|---|---|---|
| Scene graph authoring | WGE | Primary authoring tool | Use WGE window and `UnderbrewWorldGraph.asset`. |
| Scene node data | WGE | Stores scene nodes/build indices | Build Settings must stay valid. |
| Port / exit data | WGE | Stores port names and GUIDs | `TransitionPoint` serializes selected WGE port GUIDs. |
| Scene transition trigger | Underbrew | Supplies selected source port GUID | `TransitionPoint` remains the trigger/door bridge. |
| Scene loading | Underbrew | Provides resolved target data only | `GameManager.BeginSceneTransition(...)` remains the loading authority. |
| Hero placement after transition | Underbrew | None | `HeroSceneEntry`, `HeroMotor`, and destination `TransitionPoint` fields own placement/motion. |
| Respawn marker assignment | Underbrew | None | Checkpoints and `RespawnMarker` flow remain separate from WGE. |
| Save/load | Underbrew | None | `SaveManager` and `ISaveTarget` ScriptableObjects own persistence. |
| Bootstrapping / persistent managers | Underbrew | WGE runtime manager disabled | No WGE persistent singleton in gameplay runtime. |
| Camera snap/follow after scene load | Underbrew | None | `GameCameras` / camera systems stay decoupled from hero internals. |
| Addressables, future support | Undecided | Potential graph/runtime feature | Evaluate only through an adapter that preserves Underbrew ownership. |
| Editor validation | Shared | WGE graph data + Underbrew validator | WGE validates graph; Underbrew validates scene gates and Build Settings links. |

---

## 6. Upgrade Plan for Upcoming WGE Version

1. Commit the current working integration before importing a new WGE package.
2. Create a tag or branch named `wge-working-before-upstream-update`.
3. Create a dedicated upgrade branch, e.g. `upgrade/wge-next-version`.
4. Import the new WGE version into a separate clean Unity project first, if possible.
5. Compare the clean new plugin against current `Assets/Plugins/WorldGraphEditor`.
6. Compare current Underbrew-modified WGE files against the new plugin files, especially `WGEAssetPathUtility.cs`, editor asset-loading utilities, and `Resources/TransitionManager.prefab`.
7. Prefer replacing local vendor patches with official APIs, especially if custom `TransitionManager` support can delegate scene loading cleanly.
8. Preserve Underbrew ownership of `GameManager`, `TransitionPoint`, save/load, respawn, hero placement, camera flow, and bootstrapping.
9. Confirm `_autoLoad` remains false before entering Play Mode.
10. Run the manual validation checklist below.
11. Merge only after validation passes and any remaining vendor patches are documented in this file.

Do not use the WGE update as a reason to add deeper runtime refactors unless the new API directly removes a current adapter risk.

---

## 7. Future Custom TransitionManager Migration Notes

The WGE developer has announced upcoming custom `TransitionManager` support. If that API lands, the preferred Underbrew direction is:

1. WGE resolves graph node / source port / target scene / target port data.
2. A custom Underbrew transition manager or adapter receives the resolved request.
3. The adapter calls `GameManager.BeginSceneTransition(...)`.
4. `GameManager` remains the runtime loading authority unless architecture is deliberately changed.
5. Respawn marker, entry marker, save/load, hero placement, and bootstrapping remain Underbrew-owned.

Questions to answer when the WGE update arrives:

- Can the custom `TransitionManager` delegate loading instead of loading scenes itself?
- Can it pass source port GUID, target scene, and target port GUID?
- Can it support Underbrew's entry marker / respawn marker flow?
- Does it work with Build Settings scene loading as well as Addressables?
- Does it require WGE runtime managers in gameplay scenes?
- Can it be implemented without modifying WGE core plugin files?

---

## 8. Manual Validation Checklist

Run this after any WGE graph change, plugin update, or adapter change:

- [ ] Unity opens without WGE console errors.
- [ ] WGE graph window opens.
- [ ] Existing graph asset opens.
- [ ] Existing scene nodes remain intact.
- [ ] Existing ports remain intact.
- [ ] Selected port GUIDs remain serialized on `TransitionPoint` components.
- [ ] Connections remain intact.
- [ ] Node previews still display.
- [ ] Node previews can be removed/reset if supported by the installed WGE version.
- [ ] `Tools/Project/Validate Transition Gate Links` reports no issues.
- [ ] `TransitionPoint` / Underbrew bridge resolves the correct target scene.
- [ ] `TransitionPoint` / Underbrew bridge resolves the correct target entry port or marker.
- [ ] Transitions still route through Underbrew `GameManager`.
- [ ] No direct WGE runtime scene load bypasses Underbrew.
- [ ] Hero appears at the correct entry point after transition.
- [ ] Active respawn marker is updated correctly after checkpoint activation.
- [ ] Death respawn still works after entering a new scene.
- [ ] Save/load still works.
- [ ] Bootstrap still owns persistent manager startup.
- [ ] No duplicate WGE runtime manager appears in gameplay scenes.
- [ ] Build Settings / scene references remain valid.
- [ ] Addressables path is tested separately if adopted later.

Do not report Unity tests, editor validation, or playtests as passing unless they were actually run.

---

## 9. Known Risks / Tech Debt

- Vendor/plugin files are modified or relocated and may be overwritten by WGE updates.
- Current git state reports the old `Assets/WorldGraphEditor/` tree as deleted and current `Assets/Plugins/WorldGraphEditor/` tree as untracked; this makes upgrade diffs harder until the relocation is committed cleanly.
- Underbrew runtime files directly reference WGE types (`PassageBase`, `WorldGraphContainer`, `TransitionPassStatusType`). This is intentional but means WGE API changes can break compile.
- `TransitionPointEditor` depends on WGE private serialized field names such as `_assignedPort`, `_assignedGuid`, `_targetScene`, `_selectedGuid`, and `_selectedName`.
- Several current warning strings still mention `Assets/WorldGraphEditor/Resources/TransitionManager.prefab`; if the plugin root remains `Assets/Plugins/WorldGraphEditor`, those messages should be updated in a future code pass.
- WGE `PortsDropdown` editor refresh can replace stale selections with the first available port. Underbrew currently guards this in `TransitionPointEditor`, but WGE updates may change that internal behavior.
- `_autoLoad` on WGE `TransitionManager.prefab` is load-bearing. If it resets to true, WGE can instantiate a persistent runtime manager before Underbrew boot.
- Build-index-based graph data depends on Build Settings staying stable.
- Addressables support is not adopted yet; it must be evaluated separately before changing scene loading.

---

## 10. Developer Roadmap Notes

These are external WGE roadmap notes, not Underbrew implementation commitments:

- Upcoming: Addressables support.
- Upcoming: custom `TransitionManager` support.
- Possible/current: remove/reset node preview images.
- Future roadmap interest: scene grouping / regions / sub-regions.
- Possible future investigation: editable connection paths / reroute nodes.

Each feature should be evaluated through the ownership rules above before adoption.
