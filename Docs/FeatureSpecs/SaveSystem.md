# Feature Spec — Save System

**Last audited:** 2026-05-20

## Responsibilities

Persist and restore game state across play sessions: player progress, unlocked abilities, active respawn marker, visited rooms, and relevant world state (opened doors, defeated bosses, collected pickups).

---

## Current State

**Foundation implemented and verified (2026-05-20, Milestone 0 completion pass).**

The complete save data layer, manager singleton, ability round-trip, checkpoint save triggers, cross-session respawn marker resolution, and hero placement on initial load are all implemented and working.

### What is implemented

| Component | File | Status |
|---|---|---|
| `ISaveTarget` interface | `Scripts/Save/ISaveTarget.cs` | Done |
| `SaveData` root class | `Scripts/Save/Data/SaveData.cs` | Done |
| `MetaSaveData` | `Scripts/Save/Data/MetaSaveData.cs` | Done |
| `PlayerSaveData` | `Scripts/Save/Data/PlayerSaveData.cs` | Done |
| `AbilitySaveData` | `Scripts/Save/Data/AbilitySaveData.cs` | Done |
| `WorldSaveData` | `Scripts/Save/Data/WorldSaveData.cs` | Done (stub lists) |
| `SaveStats` | `Scripts/Save/SaveStats.cs` | Done |
| `SaveSerializer` (JsonUtility) | `Scripts/Save/SaveSerializer.cs` | Done |
| `SaveFileStore` (sync + .bak) | `Scripts/Save/SaveFileStore.cs` | Done |
| `SaveDataMigrator` (v1) | `Scripts/Save/SaveDataMigrator.cs` | Done |
| `SaveManager` persistent singleton | `Scripts/Save/SaveManager.cs` | Done |
| `PlayerAbilityState` implements `ISaveTarget` | `Scripts/Hero/Core/PlayerAbilityState.cs` | Done |
| Checkpoint save trigger | `Scripts/World/Interactables/CheckpointInteractable.cs` | Done |
| GameManager → SaveManager respawn key seam | `Scripts/World/GameManager.cs` | Done |
| Cross-session respawn marker resolution | `Scripts/World/GameManager.cs` | Done |
| Hero placement at saved position on boot | `Scripts/World/GameManager.cs` | Done |
| Application quit auto-save | `Scripts/Save/SaveManager.cs` | Done |
| Fresh save on missing/corrupt file | `Scripts/Save/SaveManager.cs` | Done |
| `.bak` backup before overwrite | `Scripts/Save/SaveFileStore.cs` | Done |

### What is not yet implemented

| Feature | Milestone |
|---|---|
| `WorldStateRegistry` SO (visited rooms, defeated enemies, open doors) | Milestone 4 |
| Multi-slot save selection UI | Milestone 5 |
| `HazardRespawnMarker` direct local recovery | Done |
| `HazardRespawnMarker` save key integration | Future |
| Scene-name-driven continue (`savedScene` instead of always `firstScene`) | Milestone 4 |
| Play-time accumulation (`playTimeSeconds` stub exists, not yet wired) | Milestone 5 |
| `AbilityPickup` immediate autosave (design decision not yet made) | Milestone 4 |
| Full world-state persistence (item flags, room flags, door flags) | Milestone 4 |
| Save slot UI (multi-slot selection, delete, stats display) | Milestone 5 |

---

## Design Goals

- **Fully decoupled from all gameplay MonoBehaviours** — `SaveManager` never calls into `HeroController`, `HeroMotor`, `HeroStateBlackboard`, Rigidbody2D, enemies, or arbitrary scene MonoBehaviours
- **Data-driven** — what gets saved is defined by the data classes and `ISaveTarget` implementors, not by scattered field references
- **Fail-safe** — a missing or corrupt save file starts a fresh game without crashing, with a clear console log
- **Extensible** — adding a new saveable domain requires only: add fields to the relevant data class, implement `ISaveTarget` on the owning SO, and wire it into `SaveManager`
- **Single authority path** — the active respawn key is always written through `GameManager.SetActiveRespawnMarker`, never directly from `CheckpointInteractable` or any other caller

---

## Architecture

### System diagram

```
Bootstrap.Start()
  → SaveManager.LoadOrCreate(slot)
      → SaveFileStore.Read()
      → SaveSerializer.TryDeserialize()
      → SaveDataMigrator.Migrate()
      → ApplySaveData()
          → PlayerAbilityState.ApplySaveData()    [ISaveTarget]
          → WorldStateRegistry.ApplySaveData()    [planned]
  → GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()
  → GameManager.BeginSceneTransition(firstScene)
      → [scene loads] → OnSceneLoaded()
          → ResolveActiveRespawnMarkerFromSave()  (key → live RespawnMarker)
          → PlaceHeroAtSavedRespawnIfRequested()  (move hero to marker)
          → [TransitionRoutine continues] → camera snap → fade in
```

### Class diagram

```
SaveManager (MonoBehaviour — DontDestroyOnLoad)
├── SaveData (serializable root)
│   ├── MetaSaveData        saveVersion, lastSavedUtc, playTimeSeconds
│   ├── PlayerSaveData      currentScene, activeRespawnMarkerKey, activeHazardRespawnMarkerKey
│   ├── AbilitySaveData     7 bool flags (mirrors PlayerAbilityState)
│   └── WorldSaveData       collectedPickupIds list (stub; ready for expansion)
├── ISaveTarget (interface — implemented by persistent SOs)
│   ├── PlayerAbilityState  [implemented]
│   └── WorldStateRegistry  [planned]
├── SaveSerializer          JsonUtility wrapper with null/exception guards
├── SaveFileStore           persistentDataPath I/O; .bak before overwrite; synchronous
└── SaveDataMigrator        null normalization + version stamp
```

---

## File Layout

```
Assets/_Project/Scripts/Save/
├── ISaveTarget.cs
├── SaveManager.cs
├── SaveStats.cs
├── SaveSerializer.cs
├── SaveFileStore.cs
├── SaveDataMigrator.cs
└── Data/
    ├── SaveData.cs
    ├── MetaSaveData.cs
    ├── PlayerSaveData.cs
    ├── AbilitySaveData.cs
    └── WorldSaveData.cs
```

---

## Save Data Schema

```csharp
// Root — SaveData.cs
[Serializable]
public class SaveData
{
    public MetaSaveData    meta       = new MetaSaveData();
    public PlayerSaveData  player     = new PlayerSaveData();
    public AbilitySaveData abilities  = new AbilitySaveData();
    public WorldSaveData   world      = new WorldSaveData();
}

// MetaSaveData.cs
[Serializable]
public class MetaSaveData
{
    public int    saveVersion     = 1;   // bumped by SaveDataMigrator on schema change
    public string lastSavedUtc    = "";  // DateTime.UtcNow.ToString("o") on every Save()
    public float  playTimeSeconds = 0f;  // stub — not yet accumulated; wire in Milestone 5
}

// PlayerSaveData.cs
[Serializable]
public class PlayerSaveData
{
    public string currentScene                = "";  // stamped by SaveManager.Save()
    public string activeRespawnMarkerKey      = "";  // RespawnMarker.Key of last activated checkpoint
    public string activeHazardRespawnMarkerKey = ""; // reserved for future trigger-updated hazard marker persistence
}

// AbilitySaveData.cs — defaults MUST match PlayerAbilityState.ResetToDefaults()
[Serializable]
public class AbilitySaveData
{
    public bool dashUnlocked       = true;   // core — starts unlocked
    public bool wallClingUnlocked  = true;   // core — starts unlocked
    public bool sprintUnlocked     = false;
    public bool wallLatchUnlocked  = false;
    public bool doubleJumpUnlocked = false;
    public bool driftCloakUnlocked = false;
    public bool spiritCastUnlocked = false;
}

// WorldSaveData.cs
[Serializable]
public class WorldSaveData
{
    public List<string> collectedPickupIds = new List<string>();
    // Future: visitedRoomKeys, openedDoorIds, defeatedEnemyIds
}
```

### Example save_slot0.json

```json
{
  "meta": { "saveVersion": 1, "lastSavedUtc": "2026-05-20T08:37:37Z", "playTimeSeconds": 0.0 },
  "player": { "currentScene": "SampleScene", "activeRespawnMarkerKey": "checkpoint_a", "activeHazardRespawnMarkerKey": "" },
  "abilities": { "dashUnlocked": true, "wallClingUnlocked": true, "sprintUnlocked": false, "wallLatchUnlocked": false, "doubleJumpUnlocked": false, "driftCloakUnlocked": false, "spiritCastUnlocked": false },
  "world": { "collectedPickupIds": [] }
}
```

---

## Serialization

- **Format:** JSON via `JsonUtility.ToJson` / `JsonUtility.FromJson<SaveData>`
- **Location:** `Application.persistentDataPath/save_slot{n}.json` (e.g. `save_slot0.json`)
- **Backup:** `save_slot{n}.bak` written before every overwrite — one generation of rollback
- **Newtonsoft.Json:** not installed (`com.unity.nuget.newtonsoft-json` absent from `Packages/manifest.json`). Upgrade path is available if richer null handling or polymorphism is later needed.
- **Null normalization:** `SaveDataMigrator.Migrate()` always runs after deserialization. It normalizes all null sub-objects to fresh instances and all null strings to `""`. This means downstream code never needs to null-check save data fields.

---

## ISaveTarget Interface

```csharp
public interface ISaveTarget
{
    void GatherSaveData(SaveData data);   // copy live runtime state → data fields
    void ApplySaveData(SaveData data);    // copy data fields → live runtime state, firing events
}
```

`SaveManager` holds serialized references to `ISaveTarget` implementors (as their concrete types, since Unity cannot serialize interface fields). Currently:

```csharp
[SerializeField] private PlayerAbilityState abilityState;
```

At call time, `SaveManager` casts to `ISaveTarget` and calls `GatherSaveData` / `ApplySaveData`.

### PlayerAbilityState (implemented)

`GatherSaveData`: copies 7 bool fields from the ScriptableObject into `data.abilities`.

`ApplySaveData`: calls `SetUnlocked(AbilityId, bool)` for each of the 7 flags. Uses `SetUnlocked` (not direct field assignment) so runtime subscribers receive `AbilityChanged` events when values change.

Fresh save and loaded save go through the identical `ApplySaveData` code path. Scene `AbilityGate` objects are still correct on initial load because `AbilityGate.OnEnable()` calls `Refresh()` after the scene is instantiated; `AbilityChanged` covers runtime changes after gates have subscribed.

### Adding a new ISaveTarget implementor (future)

1. Implement `ISaveTarget` on the ScriptableObject.
2. Add the corresponding fields to the relevant save data class (`WorldSaveData`, or a new sub-data class).
3. Add null-normalization for the new fields in `SaveDataMigrator.Migrate()`.
4. Add a `[SerializeField]` reference to `SaveManager` and call the new target in `GatherSaveData()` / `ApplySaveData()`.
5. Wire the asset in the SaveManager prefab Inspector.

---

## Save Triggers

| Trigger | Caller | Notes |
|---|---|---|
| Checkpoint activation | `CheckpointInteractable.Interact()` → `SaveManager.Save()` | Primary in-game save |
| Application quit | `Application.quitting` callback on `SaveManager` | Toggle via `saveOnApplicationQuit` Inspector field |
| Future: scene transition | `GameManager.BeginSceneTransition` (not yet wired) | Auto-save before unloading current scene |
| Future: rest bench / save room | Dedicated interactable (not yet implemented) | Explicit player-initiated save |

**Rule:** Only `CheckpointInteractable.Interact` and `SaveManager.SaveOnQuit` call `Save()`. Never call it from inside a hero or enemy MonoBehaviour.

---

## Boot and Continue Flow

Every application start runs through `Bootstrap.Start()`:

```
1. SaveManager.LoadOrCreate(0)
   ├─ File exists + valid JSON
   │   → Read → Deserialize → Migrate → CurrentSave = data → ApplySaveData()
   └─ File missing or corrupt
       → CreateFreshSave(0)
           → new SaveData() → Migrate → CurrentSave = fresh → ApplySaveData() → Write file

2. GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()
   → sets _placeHeroAtSavedRespawnOnNextSceneLoad = true (single-use flag)

3. GameManager.BeginSceneTransition(firstScene)
   → [fade out] → [LoadSceneAsync] → OnSceneLoaded():
       → ResolveActiveRespawnMarkerFromSave()
           scan FindObjectsByType<RespawnMarker>
           match Key == SaveManager.ActiveRespawnMarkerKey
           assign _activeRespawnMarker
       → PlaceHeroAtSavedRespawnIfRequested()
           clear flag (single-use)
           _hero.transform.position = _activeRespawnMarker.RespawnPosition
           _hero.ForceFacingDirection(_activeRespawnMarker.FacingDirection)
   → [TransitionRoutine continues] → camera SnapToHero → fade in
```

The single-use flag `_placeHeroAtSavedRespawnOnNextSceneLoad` is consumed on the first `OnSceneLoaded` call and never set again during the session. All subsequent scene transitions (door transitions, room changes) are unaffected.

---

## Respawn Marker Persistence

The save file stores a **string key**, not a live scene object reference.

### On checkpoint activation

```
CheckpointInteractable.Interact()
  → GameManager.SetActiveRespawnMarker(marker)
      → _activeRespawnMarker = marker           (live ref — in-session respawn)
      → SaveManager.SetActiveRespawnMarkerKey(marker.Key)  (persists to CurrentSave.player)
  → SaveManager.Save()                          (writes full save to disk)
```

### On next session start

```
SaveManager.LoadOrCreate()     reads key into CurrentSave.player.activeRespawnMarkerKey
GameManager.OnSceneLoaded()
  → ResolveActiveRespawnMarkerFromSave()
      key = SaveManager.ActiveRespawnMarkerKey  ("checkpoint_a")
      scan scene RespawnMarkers by Key
      assign _activeRespawnMarker = matched marker
  → PlaceHeroAtSavedRespawnIfRequested()
      move hero to _activeRespawnMarker.RespawnPosition
```

### activeHazardRespawnMarkerKey

Present in `PlayerSaveData` but not wired into runtime recovery yet. `HazardRespawnMarker` now exists for direct scene authoring from `HazardZone`, but first-pass local hazard recovery does not read or write this save key. The field remains reserved for a future trigger-updated active hazard marker path.

---

## SaveManager Public API

```csharp
// Properties
SaveData CurrentSave { get; }
int      CurrentSlot { get; }
string   ActiveRespawnMarkerKey { get; }
string   ActiveHazardRespawnMarkerKey { get; }

// Load / Create
void LoadOrCreate(int slot = 0)
void CreateFreshSave(int slot = 0)    // public — used by New Game / debug reset

// Save
void Save()                           // saves CurrentSlot
void Save(int slot)

// Query / Delete
bool      HasSave(int slot)
void      DeleteSave(int slot)
SaveStats GetSaveStats(int slot)      // reads file without applying state — for slot UI

// Respawn keys (do not auto-save)
void SetActiveRespawnMarkerKey(string key)
void SetActiveHazardRespawnMarkerKey(string key)
```

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| File missing | `CreateFreshSave`, log `[SaveManager] No save found... Creating fresh save.` |
| Corrupt / invalid JSON | `CreateFreshSave`, log warning |
| Null sub-object after deserialization | `SaveDataMigrator.Migrate()` normalizes before use |
| Null string fields | Migrator normalizes to `""` |
| Write failure (IOException) | `SaveFileStore` logs error and returns false; `SaveManager` logs error, does not crash |
| Duplicate `SaveManager` | `Destroy(gameObject)` in `Awake`, same as all other managers |
| `abilityState` reference missing | Log warning, skip ability gather/apply, save/load of other data continues |
| No matching `RespawnMarker` in scene | Log warning, hero stays at authored scene position |
| Multiple `RespawnMarker`s with same key | Use first match, log warning about duplicate keys |
| `abilityState.ApplySaveData` gets null abilities | Guarded — migrator ensures this never reaches the call site, but the method checks defensively |

---

## SaveDataMigrator

```csharp
public static class SaveDataMigrator
{
    public const int CurrentSaveVersion = 1;

    public static void Migrate(SaveData data)
    {
        // Null-normalize sub-objects
        data.meta       ??= new MetaSaveData();
        data.player     ??= new PlayerSaveData();
        data.abilities  ??= new AbilitySaveData();
        data.world      ??= new WorldSaveData();
        data.world.collectedPickupIds ??= new List<string>();

        // Null-normalize string fields
        data.player.currentScene                ??= "";
        data.player.activeRespawnMarkerKey      ??= "";
        data.player.activeHazardRespawnMarkerKey ??= "";
        data.meta.lastSavedUtc                  ??= "";

        data.meta.saveVersion = CurrentSaveVersion;

        // TODO: Add version-gated migration blocks here as schema evolves
        // if (data.meta.saveVersion < 2) { ... }
    }
}
```

When a schema change breaks backward compatibility:
1. Increment `CurrentSaveVersion`.
2. Add a `if (data.meta.saveVersion < N)` block to patch old data.
3. Existing saves load and migrate in place. Only truly incompatible saves fall back to a fresh state.

---

## SaveStats

`SaveStats` is a plain C# class built from `SaveData` via `SaveStats.FromSaveData(data)`. Fields: `isEmpty`, `saveVersion`, `currentScene`, `lastSavedUtc`, `playTimeSeconds`, `unlockedAbilityCount`.

`SaveManager.GetSaveStats(int slot)` reads and deserializes a slot's file without applying any state to runtime objects. Intended for a future save-slot selection screen that needs to show per-slot summaries before the player commits to loading.

---

## Future Expansion

### WorldStateRegistry (Milestone 4)
A `WorldStateRegistry` ScriptableObject implementing `ISaveTarget`. Tracks:
- `visitedRoomKeys` — rooms the player has entered; drives map reveal
- `defeatedEnemyIds` — enemies that should not respawn on scene reload
- `openedDoorIds` — doors in their opened/unlocked persistent state
- `collectedPickupIds` — already in `WorldSaveData`, just needs `AbilityPickup` to write to it and `WorldStateRegistry` to consume it on load

### AbilityPickup persistence (design decision needed)
`AbilityPickup.cs` currently has a TODO comment for save integration. The design question: does picking up an ability immediately force a save (making it permanent before the next checkpoint), or does it rely on the next checkpoint/quit save? This matters for retry-loop design. Until decided, pickups are only persisted if the player reaches a checkpoint after picking them up.

### Multi-slot saves
The API is already slot-aware. `Save(int slot)`, `HasSave(int slot)`, `GetSaveStats(int slot)`, and `SaveFileStore.GetPath(int slot)` all accept any slot index. `save_slot{n}.json` naming is already in place. Adding multi-slot support requires only: a slot-selection UI that calls `LoadOrCreate(chosenSlot)` and `CreateFreshSave(chosenSlot)` at the right times.

### Scene-name-driven continue
`PlayerSaveData.currentScene` is written on every `Save()`. A "Continue" button on the main menu can call `GameManager.BeginSceneTransition(SaveManager.Instance.CurrentSave.player.currentScene)` instead of always loading `firstScene`. The existing respawn marker resolution and hero placement code will handle positioning correctly.

### Play-time accumulation
`MetaSaveData.playTimeSeconds` is a stub at `0f`. Wire it by adding a `Time.unscaledDeltaTime` accumulator to `SaveManager.Update()` that increments while `GameManager.State == GameState.Playing`. Write the accumulated total into `CurrentSave.meta.playTimeSeconds` in `GatherSaveData()`.

### Save migration versioning
When a schema change needs data patching, increment `SaveDataMigrator.CurrentSaveVersion` and add a version-gated block. Old saves migrate forward silently; saves from a future version that `Migrate()` does not understand are treated as corrupt and reset to fresh state.

### Auto-save on scene transition
Wire `SaveManager.Save()` into `GameManager.TransitionRoutine` before the `LoadSceneAsync` call. This creates a checkpoint-independent auto-save whenever the player moves between scenes, at the cost of slightly longer transition times.

---

## Rules

- `SaveManager` must not call any method on `HeroController`, `HeroMotor`, `HeroStateBlackboard`, `Rigidbody2D`, enemies, or arbitrary scene `MonoBehaviour`s.
- All save data types must be plain C# classes with no `UnityEngine.Object` references — use string keys for scene-object identity.
- Loading must be possible before any hero scene object exists (loading from a main menu is valid).
- `ApplySaveData` implementations must use event-firing setters (`SetUnlocked`, etc.), never direct field assignment, so scene subscribers react correctly.
- Only `CheckpointInteractable.Interact` and `SaveManager.SaveOnQuit` call `Save()`. Never from hero or enemy `MonoBehaviour`s.
- `SaveManager` must not be placed in gameplay scenes — it lives exclusively in `DontDestroyOnLoad`, instantiated from `Bootstrap`.
- `CreateFreshSave` is the canonical path for a new-game start. It goes through `ApplySaveData` so the same event-firing path is used regardless of whether data came from a file or was freshly constructed.
