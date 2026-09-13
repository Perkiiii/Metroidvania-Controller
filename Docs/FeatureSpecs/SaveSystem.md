# Feature Spec — Save System

**Last audited:** 2026-09-13 — Weather presentation Packages 1–3 completion; visual review pending

## Responsibilities

Persist and restore game state across play sessions: player progress, unlocked abilities, active
respawn marker, visited rooms, relevant world state (opened doors, defeated bosses, collected
pickups), the canonical Package 1 world-time timestamp, and Packages 2–3 regional weather facts,
history, and exact-slot overrides.

---

## Current State

**Foundation implemented and verified (2026-05-20, Milestone 0 completion pass). Cross-scene checkpoint respawn and scene-name-driven boot continue were added on 2026-05-26. Simulation Packages 1–4 world time/climate/weather are implemented in code, schema, tests, and existing asset/prefab composition; weather presentation Packages 1–3 use transient scene-local state and do not change the save schema. Observed Unity 6000.3.10f1 EditMode results: generator 10/10, progression/persistence 29/29, history 11/11, overrides 12/12, forecast 13/13, complete WorldClimate/WorldTime 122/122, and WorldTime 38/38. Final focused simulation/presentation EditMode is 169/169; the weather lifecycle PlayMode fixture is 1/1. Full EditMode is 764/765, with the sole unrelated `CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` failure. No live screenshot, hands-on visual/audio-mix validation, or profiling evidence was recorded.**

The complete save data layer, manager singleton, ability round-trip, checkpoint save triggers, cross-session / cross-scene respawn marker resolution, saved-scene startup routing, and hero placement on initial load are all implemented and working.

### What is implemented

| Component | File | Status |
|---|---|---|
| `ISaveTarget` interface | `Scripts/Save/ISaveTarget.cs` | Done |
| `SaveData` root class | `Scripts/Save/Data/SaveData.cs` | Done |
| `MetaSaveData` | `Scripts/Save/Data/MetaSaveData.cs` | Done |
| `PlayerSaveData` | `Scripts/Save/Data/PlayerSaveData.cs` | Done |
| `AbilitySaveData` | `Scripts/Save/Data/AbilitySaveData.cs` | Done |
| `HealthSaveData` | `Scripts/Save/Data/HealthSaveData.cs` | Done |
| `ResourceSaveData` | `Scripts/Save/Data/ResourceSaveData.cs` | Done |
| `WorldSaveData` | `Scripts/Save/Data/WorldSaveData.cs` | Done (stub lists) |
| `SaveStats` | `Scripts/Save/SaveStats.cs` | Done |
| `SaveSerializer` (JsonUtility) | `Scripts/Save/SaveSerializer.cs` | Done |
| `SaveFileStore` (sync + .bak) | `Scripts/Save/SaveFileStore.cs` | Done |
| `WorldTimeSaveData` | `Scripts/Save/Data/WorldTimeSaveData.cs` | Done (Package 1; v5 schema) |
| `WorldTimeState` implements `ISaveTarget` | `Scripts/World/Time/WorldTimeState.cs` | Done (Package 1; asset wired) |
| `WorldWeatherSaveData` and nested region/history/override DTOs | `Scripts/Save/Data/WorldWeatherSaveData.cs`, `RegionWeatherSaveEntry.cs`, `WeatherHistoryRecordSaveEntry.cs`, `WeatherOverrideSaveEntry.cs` | Done (Packages 2–3; v6 schema unchanged) |
| `WorldWeatherState` implements `ISaveTarget` | `Scripts/World/Climate/WorldWeatherState.cs` | Done (Packages 2–3; asset wired) |
| Climate definitions and regional catalog | `Scripts/World/Climate/*.cs` | Done (Packages 2–3; authored assets wired; generator/history/override/forecast behavior) |
| `SaveDataMigrator` (v6) | `Scripts/Save/SaveDataMigrator.cs` | Done |
| `SaveManager` persistent singleton | `Scripts/Save/SaveManager.cs` | Done |
| `WorldStateRegistry` implements `ISaveTarget` | `Scripts/World/Persistence/Core/WorldStateRegistry.cs` | Done (World Persistence Phase 1) |
| Coordinated boss completion (`BossEncounterController` → `WorldStateRegistry`) | `Scripts/Boss/BossEncounterController.cs` | Done; Undead Executioner authored in `SampleScene4` |
| `EnemyPersistence` / `EnemyPersistenceMode` | `Scripts/Enemy/EnemyPersistence.cs`, `EnemyPersistenceMode.cs` | Done (World Persistence Phase 1) |
| `PersistenceLifetime`, `PersistentDoor`, `PersistentSwitch`, `PersistentBreakable` | `Scripts/World/Persistence/Core/PersistenceLifetime.cs`, `Scripts/World/Persistence/Participants/PersistentDoor.cs`, `PersistentSwitch.cs`, `PersistentBreakable.cs` | Done (World Persistence Phase 3) |
| Room visitation (`RoomVisitReporter.Awake` → `WorldStateRegistry.MarkRoomVisited`, authored `roomId` per gameplay scene) | `Scripts/World/Persistence/Participants/RoomVisitReporter.cs` | Done (World Persistence Phase 3.1) |
| `PlayerAbilityState` implements `ISaveTarget` | `Scripts/Hero/Core/PlayerAbilityState.cs` | Done |
| `PlayerHealthState` implements `ISaveTarget` | `Scripts/Hero/Core/PlayerHealthState.cs` | Done; authoritative gameplay ownership wired |
| `PlayerResourceState` implements `ISaveTarget` | `Scripts/Hero/Core/PlayerResourceState.cs` | Done; authoritative gameplay ownership wired |
| Checkpoint save trigger | `Scripts/World/Interactables/CheckpointInteractable.cs` | Done |
| GameManager → SaveManager respawn key seam | `Scripts/Managers/GameManager.cs` | Done |
| Cross-session respawn marker resolution | `Scripts/Managers/GameManager.cs` | Done |
| Hero placement at saved position on boot | `Scripts/Managers/GameManager.cs` | Done |
| Application quit auto-save | `Scripts/Save/SaveManager.cs` | Done |
| Fresh save on missing/corrupt file | `Scripts/Save/SaveManager.cs` | Done |
| `.bak` backup before overwrite | `Scripts/Save/SaveFileStore.cs` | Done |

### What is not yet implemented

| Feature | Milestone |
|---|---|
| Multi-slot save selection UI | Milestone 5 |
| `HazardRespawnMarker` direct local recovery | Done |
| `HazardRespawnMarker` save key integration | Future |
| Scene-name-driven boot continue (`activeRespawnSceneName` / `currentScene` instead of always `firstScene`) | Done |
| Play-time accumulation (`playTimeSeconds` stub exists, not yet wired) | Milestone 5 |
| Save slot UI (multi-slot selection, delete, stats display) | Milestone 5 |
| Automatic climate/weather progression, forecasts, history queries, runtime overrides (Package 3) | Done; automated-tested; no Play Mode/manual primary-Editor validation |
| Room climate context and authoring tools (simulation Package 4) | Done; passive metadata/tooling only; no save-schema change |
| Weather presentation transient state (presentation Packages 1–3) | Not persisted; initialized from actual weather on room enable; visual review pending |

See `Docs/ImplementationPlans/WorldPersistence.md` for the full World Persistence plan. Phase 1 (registry foundation + ordinary placed enemy persistence), Phase 2 (normal-death lifecycle + pickup reconciliation), and Phase 3 (doors/switches/breakables + room visitation) are implemented; see the `WorldStateRegistry`, `EnemyPersistence`, and Phase 3 sections below and in `Docs/Architecture.md`.

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
          → PlayerHealthState.ApplySaveData()     [ISaveTarget]
          → PlayerResourceState.ApplySaveData()   [ISaveTarget]
          → WorldStateRegistry.ApplySaveData()    [ISaveTarget]
          → WorldTimeState.ApplySaveData()         [ISaveTarget; Package 1, wired]
          → WorldWeatherState.ApplySaveData()      [ISaveTarget; Packages 2–3, wired]
      → WorldWeatherState.CompleteLoad(WorldTimeState)
          [post-target reconciliation; clock-dependent, not target-order dependent]
  → SaveManager.GetStartupScene(firstScene)
      → activeRespawnSceneName if set and loadable
      → currentScene if set and loadable
      → firstScene fallback
  → GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()
  → GameManager.BeginSceneTransition(startupScene)
      → [scene loads] → OnSceneLoaded()
          → ResolveActiveRespawnMarkerFromSave()  (key → live RespawnMarker)
          → PlaceHeroAtSavedRespawnIfRequested()  (move hero to marker)
          → [TransitionRoutine continues] → camera snap → fade in

WorldClockDriver.Update()
  → no-op until WorldTimeState.IsLoaded && GameManager.Instance != null
      && GameManager.State == GameState.Playing
  → unscaled/clamped real-time sample
  → complete quanta via WorldTimeState.RealSecondsPerGameMinute
  → WorldTimeState.AdvanceMinutes(completeMinutes)
```

### Class diagram

```
SaveManager (MonoBehaviour — DontDestroyOnLoad)
├── SaveData (serializable root)
│   ├── MetaSaveData        saveVersion, lastSavedUtc, playTimeSeconds
│   ├── PlayerSaveData      currentScene, activeRespawnSceneName, activeRespawnMarkerKey, activeHazardRespawnMarkerKey
│   ├── AbilitySaveData     8 bool flags (mirrors PlayerAbilityState; all default locked)
│   ├── HealthSaveData      initialized marker, current/max/bonus health
│   ├── ResourceSaveData    initialized marker, current/max integer parts
│   ├── WorldSaveData       collectedPickupIds, visitedRoomIds, defeatedEncounterIds, objectStates
│   ├── WorldTimeSaveData   initialized marker, canonical totalGameMinutes [Package 1]
│   └── WorldWeatherSaveData initialized marker, regional weather/history/override DTOs [Packages 2–3; v6]
├── ISaveTarget (interface — implemented by persistent SOs)
│   ├── PlayerAbilityState  [implemented]
│   ├── PlayerHealthState   [implemented; authoritative gameplay owner]
│   ├── PlayerResourceState [implemented; authoritative gameplay owner]
│   ├── WorldStateRegistry  [implemented — World Persistence Phase 1]
│   ├── WorldTimeState      [implemented — Package 1; wired]
│   └── WorldWeatherState   [implemented — Packages 2–3; wired]
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
    ├── HealthSaveData.cs
    ├── ResourceSaveData.cs
    ├── WorldSaveData.cs
    ├── WorldTimeSaveData.cs
    ├── WorldWeatherSaveData.cs
    ├── RegionWeatherSaveEntry.cs
    ├── WeatherHistoryRecordSaveEntry.cs
    └── WeatherOverrideSaveEntry.cs
```

### Package 1 world-time composition

The Package 1 target assets are:

```text
Assets/_Project/ScriptableObjects/World/CalendarConfig.asset
Assets/_Project/ScriptableObjects/World/WorldTimeState.asset
Assets/_Project/Prefabs/Managers/_WorldClockDriver.prefab
```

`WorldTimeState.asset` owns the serialized `CalendarConfig` reference and is the save target.
`_WorldClockDriver.prefab` serializes only the `WorldTimeState` reference and reads pacing through
`WorldTimeState.RealSecondsPerGameMinute`. The state asset is included in `_SaveManager.prefab`'s
`saveTargets`, and the driver prefab is assigned to `Bootstrap.worldClockDriverPrefab` in
`Assets/_Project/Scenes/Boot.unity`. The current checkout contains the runtime code, generated
assets/prefab, and these serialized assignments. Package 1 baseline/full-suite validation is
complete. Package 2 asset/composition generation and validation were performed through Unity APIs
in an isolated copy; this does not claim Play Mode or manual primary-Editor validation, and the
open primary Editor may require refresh/reimport.

### Package 2 climate/weather composition

Package 2 adds the following ScriptableObject assets under
`Assets/_Project/ScriptableObjects/World/`:

```text
WeatherSimulationConfig.asset
WorldWeatherState.asset
ClimateRegionCatalog.asset
WeatherDefinitions/Clear.asset
WeatherDefinitions/Cloudy.asset
WeatherDefinitions/Rain.asset
WeatherDefinitions/Storm.asset
WeatherDefinitions/Fog.asset
Seasons/Spring.asset
Seasons/Summer.asset
Seasons/Autumn.asset
Seasons/Winter.asset
Seasons/UnderbrewSeasonTrack.asset
Seasons/Underbrew.asset
```

The simulation config contains one weather definition per enum value and the authored 06:00/18:00
slot policy. `WorldWeatherState.asset` references the simulation config and region catalog. The
catalog currently contains the single provisional `region_underbrew` region, whose track contains
the four authored seasons and whose initial weather is `Clear`. `WorldWeatherState.asset` is in
`_SaveManager.prefab`'s target list and `Bootstrap.worldWeatherState` is assigned in
`Assets/_Project/Scenes/Boot.unity`.

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
    public HealthSaveData  health     = new HealthSaveData();
    public ResourceSaveData resource  = new ResourceSaveData();
    public WorldSaveData   world      = new WorldSaveData();
    public WorldTimeSaveData worldTime = new WorldTimeSaveData();
    public WorldWeatherSaveData worldWeather = new WorldWeatherSaveData();
}

// MetaSaveData.cs
[Serializable]
public class MetaSaveData
{
    public int    saveVersion     = 3;   // constructor default; Migrate() stamps current schema v6
    public string lastSavedUtc    = "";  // DateTime.UtcNow.ToString("o") on every Save()
    public float  playTimeSeconds = 0f;  // stub — not yet accumulated; wire in Milestone 5
}

// PlayerSaveData.cs
[Serializable]
public class PlayerSaveData
{
    public string currentScene                = "";  // stamped by SaveManager.Save()
    public string activeRespawnSceneName      = "";  // scene name containing the last activated checkpoint marker
    public string activeRespawnMarkerKey      = "";  // RespawnMarker.Key of last activated checkpoint
    public string activeHazardRespawnMarkerKey = ""; // reserved for future trigger-updated hazard marker persistence
}

// AbilitySaveData.cs — defaults MUST match PlayerAbilityState.ResetToDefaults()
// A true new game owns no permanent ability, so every flag defaults to locked. These initializers
// are also what a save with a missing/null "abilities" section falls back to after migration.
// Existing saves are unaffected: GatherSaveData writes all eight booleans explicitly, so
// deserialization overwrites every initializer with the stored value.
[Serializable]
public class AbilitySaveData
{
    public bool dashUnlocked       = false;  // progression unlock, not a starting capability
    public bool wallClingUnlocked  = false;  // progression unlock, not a starting capability
    public bool sprintUnlocked     = false;
    public bool wallLatchUnlocked  = false;
    public bool doubleJumpUnlocked = false;
    public bool driftCloakUnlocked = false;
    public bool spiritCastUnlocked = false;
    public bool bindUnlocked       = false;
}

// HealthSaveData.cs
[Serializable]
public class HealthSaveData
{
    public bool initialized;
    public int currentHealth;
    public int maximumHealth;
    public int bonusHealth;
}

// ResourceSaveData.cs
[Serializable]
public class ResourceSaveData
{
    public bool initialized;
    public int currentParts;
    public int maximumParts;
}

// WorldSaveData.cs
[Serializable]
public class WorldSaveData
{
    public List<string> collectedPickupIds = new List<string>();
    public List<string> visitedRoomIds = new List<string>();
    public List<string> defeatedEncounterIds = new List<string>();
    public List<WorldObjectStateEntry> objectStates = new List<WorldObjectStateEntry>();
}

// WorldObjectStateEntry.cs — JsonUtility cannot serialize Dictionary<,> directly, so permanent
// per-object state round-trips through a flat, sorted list of these entries.
[Serializable]
public class WorldObjectStateEntry
{
    public string id = "";
    public string state = "";
}

// WorldTimeSaveData.cs — Package 1 canonical timestamp. Calendar conversion remains owned by
// WorldTimeState and CalendarConfig; this DTO carries no Unity/Object references or derived fields.
[Serializable]
public class WorldTimeSaveData
{
    public bool initialized;
    public long totalGameMinutes;
}

// WorldWeatherSaveData.cs — Package 2 regional weather persistence. JsonUtility uses flat lists
// rather than dictionaries; runtime reconciliation remains owned by WorldWeatherState.
[Serializable]
public class WorldWeatherSaveData
{
    public bool initialized;
    public List<RegionWeatherSaveEntry> regions = new List<RegionWeatherSaveEntry>();
    public List<WeatherOverrideSaveEntry> overrides = new List<WeatherOverrideSaveEntry>();
}

[Serializable]
public class RegionWeatherSaveEntry
{
    public string regionId = "";
    public int currentWeatherType;
    public long currentWeatherStartMinute;
    public long historyAvailableFromMinute;
    public uint rootSeed;
    public List<WeatherHistoryRecordSaveEntry> history = new List<WeatherHistoryRecordSaveEntry>();
}

[Serializable]
public class WeatherHistoryRecordSaveEntry
{
    public int weatherType;
    public long startMinute;
    public long endMinuteExclusive;
}

[Serializable]
public class WeatherOverrideSaveEntry
{
    public string regionId = "";
    public long absoluteDayIndex;
    public int slotIndex;
    public int weatherType;
    public string sourceTag = "";
}
```

Respawnable-enemy death records and generic until-death state are intentionally NOT part of `WorldSaveData` — they are runtime-only collections owned by `WorldStateRegistry` (see below) and are cleared by `ApplySaveData` (fresh game / Continue / slot change) and by explicit reset calls on normal death.

### Example save_slot0.json

```json
{
  "meta": { "saveVersion": 6, "lastSavedUtc": "2026-07-21T08:37:37Z", "playTimeSeconds": 0.0 },
  "player": { "currentScene": "SampleScene", "activeRespawnSceneName": "SampleScene", "activeRespawnMarkerKey": "checkpoint_a", "activeHazardRespawnMarkerKey": "" },
  "abilities": { "dashUnlocked": true, "wallClingUnlocked": true, "sprintUnlocked": false, "wallLatchUnlocked": false, "doubleJumpUnlocked": false, "driftCloakUnlocked": false, "spiritCastUnlocked": false, "bindUnlocked": false },
  "health": { "initialized": true, "currentHealth": 5, "maximumHealth": 5, "bonusHealth": 0 },
  "resource": { "initialized": true, "currentParts": 0, "maximumParts": 0 },
  "world": { "collectedPickupIds": [], "visitedRoomIds": [], "defeatedEncounterIds": [], "objectStates": [] },
  "worldTime": { "initialized": true, "totalGameMinutes": 480 },
  "worldWeather": { "initialized": true, "regions": [], "overrides": [] }
}
```

---

## Serialization

- **Format:** JSON via `JsonUtility.ToJson` / `JsonUtility.FromJson<SaveData>`
- **Location:** `Application.persistentDataPath/save_slot{n}.json` (e.g. `save_slot0.json`)
- **Backup:** `save_slot{n}.bak` written before every overwrite — one generation of rollback
- **Newtonsoft.Json:** not installed (`com.unity.nuget.newtonsoft-json` absent from `Packages/manifest.json`). Upgrade path is available if richer null handling or polymorphism is later needed.
- **Null normalization:** `SaveDataMigrator.Migrate()` always runs after deserialization. It normalizes all null sub-objects, `worldWeather.regions`/`overrides`, each region history list, and region/source-tag strings. This means downstream code receives a stable non-null weather DTO shape; malformed entries still undergo WorldWeatherState completion validation.

---

## ISaveTarget Interface

```csharp
public interface ISaveTarget
{
    void GatherSaveData(SaveData data);   // copy live runtime state → data fields
    void ApplySaveData(SaveData data);    // copy data fields → live runtime state, firing events
}
```

`SaveManager` holds an Inspector-ordered `List<ScriptableObject>` because Unity cannot serialize interface fields. During initialization it validates and caches the entries as `ISaveTarget`s. Null entries, assets that do not implement the interface, and duplicate asset assignments are warned and skipped. No reflection, service locator, or scene search is used; list order is the deterministic gather/apply order.

```csharp
[SerializeField] private List<ScriptableObject> saveTargets;
```

The `_SaveManager.prefab` list must contain `PlayerAbilityState`, `PlayerHealthState`, and
`PlayerResourceState`; it also contains `WorldStateRegistry`, the Package 1 `WorldTimeState`, and
the Package 2 `WorldWeatherState` assets in the current serialized prefab. SaveManager remains a
generic Inspector-ordered target pipeline and does not reference either world state by concrete
type. Weather completion is a Bootstrap post-load step because target ordering is not a lifecycle
contract.

### PlayerAbilityState (implemented)

`GatherSaveData`: copies 8 bool fields from the ScriptableObject into `data.abilities`.

`ApplySaveData`: calls `SetUnlocked(AbilityId, bool)` for each of the 8 flags. Uses `SetUnlocked` (not direct field assignment) so runtime subscribers receive `AbilityChanged` events when values change.

Fresh save and loaded save go through the identical `ApplySaveData` code path. Scene `AbilityGate` objects are still correct on initial load because `AbilityGate.OnEnable()` calls `Refresh()` after the scene is instantiated; `AbilityChanged` covers runtime changes after gates have subscribed.

### PlayerHealthState and PlayerResourceState

Their save sections include an `initialized` marker so an old save that lacks the section is distinguishable from legitimate zero values. Applying an uninitialized section resets the asset to its authored fresh-save defaults. Applying initialized but malformed values clamps them safely. Each state emits one neutral `StateApplied` change notification rather than damage/heal/gain/spend presentation reasons. `PlayerHealthState` is the authoritative gameplay owner and is injected into `HeroHealthComponent` only after save application; scene-facade initialization never resets it. `PlayerResourceState` is consumed by `HeroAttackAction` for accepted attack-result generation and by `HeroBindAction` for spend-then-heal completion; both current health and current resource are cleared/restored exactly once by `HeroController.ResetAfterRespawn()` on death (see `Docs/FeatureSpecs/PlayerHealthAndResource.md`).

A loaded save can, in rare cases (a quit-save or checkpoint-save racing a death sequence), capture `health.currentHealth == 0`. `SaveDataMigrator` does not correct this — normalization instead happens once at `Bootstrap.Start()`, immediately after `SaveManager.LoadOrCreate(0)` and before any gameplay scene loads, via `GameManager.ResolveLoadedHealthState()` → `PlayerHealthState.NormalizeDepletedContinue()`. This runs before any Hero exists, so it cannot trigger a second death/respawn, and it fires the neutral `StateApplied` reason (not `Heal`/`FullRestore`) so the HUD never presents it as player healing. See `Docs/FeatureSpecs/PlayerHealthAndResource.md` for the full lifecycle policy table.

### WorldStateRegistry (implemented — World Persistence Phase 1)

`Scripts/World/Persistence/Core/WorldStateRegistry.cs`. Owns physical world facts only: visited rooms, consumed pickups, permanent object states (arbitrary string payload per object ID, e.g. door open/closed), and permanent encounter completion — all serialized into `WorldSaveData`. It also owns two runtime-only collections that are never serialized: generic until-death physical state, and timed death records for ordinary respawnable enemies.

`GatherSaveData` writes sorted, deduplicated lists (`StringComparer.Ordinal`) so save-target order never affects output. `ApplySaveData` clears and repopulates every serialized collection *and* clears both non-serialized collections — every apply represents a fresh game, a Continue, or a slot change, all of which must present ordinary enemies alive with no stale until-death state.

Restricted enemy-timer API (see `Docs/ImplementationPlans/WorldPersistence.md` for the full behavioural spec):
- `RecordRespawnableEnemyDeath(string enemyId, float respawnDuration)` — public, called by `EnemyPersistence.RecordDeath()` on confirmed death.
- `ResetRespawnableEnemyDeaths()` / `ResetUntilDeathState()` — public, called exactly once per normal death from `GameManager.BeginRespawnSequence` (World Persistence Phase 2), before the checkpoint scene begins loading (or before the in-place fallback if it can't load at all) — never from `ApplyNormalDeathRespawn`, which runs later and would already be too late for newly loaded scene objects to observe the cleared state. Never called from checkpoint activation or recoverable hazard reposition.
- `internal bool ShouldSuppressEnemyOnInitialization(string enemyId)` — internal; only `EnemyPersistence` (same assembly) may call it, and only from `EnemyController`'s one-time initialization path. There is no live respawn scheduler, coroutine, or per-frame timer — expiry is resolved lazily, only when a scene next initializes that enemy.

Keyed notifications use `WorldStateKey` (category + string id) and `WorldStateChange` (bool flag + optional string payload) via `Subscribe`/`Unsubscribe` — there is no unqualified global `Changed` event. Enemy timer expiry never dispatches a notification.

### WorldTimeState (implemented — Package 1)

`Assets/_Project/Scripts/World/Time/WorldTimeState.cs` is the canonical world-time
`ScriptableObject` and an `ISaveTarget`. It owns the nonserialized `long totalGameMinutes`, derives
the immutable `WorldTimeSnapshot` from its serialized `CalendarConfig`, and exposes
`RealSecondsPerGameMinute` for the driver. `WorldTimeSaveData` stores only an `initialized` marker
and the canonical long; a missing or pre-v5 section leaves the marker false so the state applies
the authored fresh date. Initialized negative timestamps are rejected.

`WorldTimeState` publishes `StateApplied` once after save application. `AdvanceMinutes` rejects
negative values and checked overflow, treats zero as a no-op, rejects callback reentrancy, emits
crossed hour boundaries in fixed order (`HourChanged`, applicable `DayChanged`, `SeasonChanged`,
`YearChanged`, `DayPhaseChanged`), and emits one `TimeAdvanced(before, after)` after the final
target. Boundary subscribers are isolated so one exception does not strand canonical time or stop
later subscribers.

`WorldClockDriver` is a separate persistent `MonoBehaviour` sampler. Its only serialized gameplay
reference is `WorldTimeState`; it reads pacing through `WorldTimeState.RealSecondsPerGameMinute`
and never holds a direct `CalendarConfig` reference. It advances only when the state is loaded and
`GameManager.State == GameState.Playing`; pause/menu/loading/transition states stop it, while
hit-stop continues because the game state remains `Playing`. The generated time assets, driver
prefab, and SaveManager/Boot assignments are serialized at the paths listed in
`Docs/ImplementationPlans/WorldTimeClimateWeather.md` §25. Package 1 baseline/full-suite
validation is complete. Observed Unity 6000.3.10f1 EditMode results are generator 10/10,
progression/persistence 29/29, history 11/11, overrides 12/12, forecast 13/13, complete
WorldClimate/WorldTime 122/122, and WorldTime 38/38. Final focused simulation/presentation
EditMode is 169/169; the weather lifecycle PlayMode fixture is 1/1. Full EditMode is 764/765
because of the unrelated pre-existing `CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis`
failure. No live screenshot, hands-on visual/audio-mix validation, or profiling evidence was
recorded.

### WorldWeatherState (implemented — Packages 2–3)

`Assets/_Project/Scripts/World/Climate/WorldWeatherState.cs` is the persistent regional-weather
`ScriptableObject` and `ISaveTarget`. It is a sibling of `WorldTimeState`, not a concern of
`SaveManager`, `GameManager`, or `WorldStateRegistry`. The asset references
`WeatherSimulationConfig` and `ClimateRegionCatalog`. Its runtime records own each catalog
region’s current `WeatherType`, current-start minute, history-availability minute, nonzero root
seed, actual-weather history, and exact-slot override payloads.

`GatherSaveData` writes `SaveData.worldWeather` only after completion and serializes the current
regional records plus their payload lists. `ApplySaveData` deep-copies only the weather section to
a private pending DTO, clears the old runtime view, and does not read `WorldTimeState`, subscribe
to events, or depend on the Inspector order of save targets. `Bootstrap` calls
`CompleteLoad(worldTimeState)` after `SaveManager.LoadOrCreate` has applied every target.

`CompleteLoad` requires a loaded `WorldTimeState`, validates the simulation config, catalog, season
tracks, matrices, fixed slot keys, and calendar/slot cardinality, then reconciles the pending save
against the authored catalog. Missing configured regions start at the loaded minute with the
active deterministic slot result, a truthful history boundary, and a generated nonzero seed.
Saved regions removed from the catalog are dropped; duplicate saved IDs use the first entry;
invalid weather values fall back to the region’s authored initial weather; malformed
histories/overrides and invalid timestamps are warned and normalized/reset according to the climate
implementation contract. Completion is idempotent for an unchanged catalog and snapshots the
latest runtime state before catalog-changing recompletion.

After completion, `WorldWeatherState` binds once to `WorldTimeState.HourChanged` and resolves every
crossed configured slot in catalog order with runtime exact-slot override > authored fixed slot >
generated weather precedence. One atomic mutation path updates actual weather, closes/merges
history, and emits `WeatherChanged`; same-type resolutions do nothing. `TryQueryHistory` and
`TryGetForecast` return immutable read-only results, and `SetOverride`/`ClearOverride` validate
exact keys and never rewrite past actual history. Package 3 changes no save version; v6 persists
the current regional records, closed history, and override payloads.

Weather presentation Packages 1–3 are not save targets. `RoomWeatherPresentation`,
`RoomRainPresentation`, and `RoomStormPresentation` initialize from the current actual weather
when a room is enabled and own only transient requests, particle state, ambience fades, and storm
timers. They do not write `WorldWeatherState`, add save fields, or persist pending thunder/flash
activity. Room unload/disable clears that transient state; re-entry reads the current saved/live
weather again.

### EnemyPersistence and enemy persistence modes (implemented — World Persistence Phase 1)

`Scripts/Enemy/EnemyPersistence.cs` + `EnemyPersistenceMode.cs` (`RoomRuntime` | `RespawnableTimed` | `PermanentEncounter`). Attached alongside `EnemyController` on a placed enemy prefab instance; carries a per-instance `worldObjectId` (must be unique — left blank on the shared prefab asset and set uniquely per scene instance), a `mode`, and a `WorldStateRegistry` reference. Ordinary-enemy respawn duration lives on shared `EnemyConfig.respawnDuration`, not on `EnemyPersistence` itself.

`EnemyController.Awake` resolves suppression exactly once, before any AI/perception/movement/combat/feedback initialization: if `EnemyPersistence.ShouldSuppressOnInitialization()` returns true, colliders are disabled, `Rigidbody2D.simulated` is set false, renderers are disabled, and `EnemyMotor`/`EnemyPerception`/`EnemyAttackController`/`EnemyContactDamage`/`IEnemyBehaviour` components are disabled without ever being initialized. `EnemyController` and `EnemyPersistence` remain enabled for diagnostics; the first pass never calls `gameObject.SetActive(false)`. On the active path, `EnemyPersistence.RecordDeath` is subscribed to `EnemyHealthComponent.OnDeath`.

`Mushroom.prefab` and all 9 placed instances across `SampleScene`/`SampleScene2`/`SampleScene3` are wired as `RespawnableTimed` against the single `WorldStateRegistry.asset` (`Assets/_Project/ScriptableObjects/World/WorldStateRegistry.asset`), registered on `_SaveManager.prefab`'s save target list. `Tools/Project/Validate World Persistence` scans enabled Build Settings scenes for missing/duplicate `worldObjectId`s, missing registry/config references, and `RespawnableTimed` instances with an invalid `EnemyConfig.respawnDuration`.

### Adding a new ISaveTarget implementor (future)

1. Implement `ISaveTarget` on the ScriptableObject.
2. Add the corresponding fields to the relevant save data class (`WorldSaveData`, or a new sub-data class).
3. Add null-normalization for the new fields in `SaveDataMigrator.Migrate()`.
4. Wire the asset into the ordered target list on the SaveManager prefab Inspector.

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

2. WorldWeatherState.CompleteLoad(WorldTimeState)
   → requires WorldTimeState.IsLoaded
   → validates authored dependencies and reconciles all catalog regions
   → runs after every ISaveTarget.ApplySaveData; binds weather progression only after reconciliation

3. GameManager.ResolveLoadedHealthState()
   → PlayerHealthState.NormalizeDepletedContinue() (no-op unless CurrentHealth <= 0)
   → if it fired, also PlayerResourceState.Clear()
   → runs before any gameplay scene/Hero exists — zero-health save protection (see PlayerHealthAndResource.md)

4. startupScene = SaveManager.GetStartupScene(firstScene)
   ├─ activeRespawnSceneName if present + loadable
   ├─ currentScene if present + loadable
   └─ firstScene fallback

5. GameManager.RequestSavedRespawnPlacementOnNextSceneLoad()
   → sets _placeHeroAtSavedRespawnOnNextSceneLoad = true (single-use flag)

6. GameManager.BeginSceneTransition(startupScene)
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

The save file stores a **scene name + string key**, not a live scene object reference. `activeRespawnSceneName + activeRespawnMarkerKey` is the normal-death respawn destination. It is intentionally separate from any future death-drop/shade data, which should capture the death scene and death position before loading the checkpoint scene.

### On checkpoint activation

```
CheckpointInteractable.Interact()
  → GameManager.SetActiveRespawnMarker(marker)
      → _activeRespawnMarker = marker           (live ref — in-session respawn)
      → SaveManager.SetActiveRespawnPoint(marker.SceneName, marker.Key)
  → SaveManager.Save()                          (writes full save to disk)
```

### On next session start

```
SaveManager.LoadOrCreate()     reads scene + key into CurrentSave.player
GameManager.OnSceneLoaded()
  → ResolveActiveRespawnMarkerFromSave()
      key = SaveManager.ActiveRespawnMarkerKey  ("checkpoint_a")
      if loaded scene matches SaveManager.ActiveRespawnSceneName, scan scene RespawnMarkers by Key
      assign _activeRespawnMarker = matched marker
  → PlaceHeroAtSavedRespawnIfRequested()
      move hero to _activeRespawnMarker.RespawnPosition
```

On normal death, `GameManager.BeginRespawnSequence()` treats the saved scene + marker strings as source of truth. If the checkpoint scene is different from the death scene, `GameManager` loads the checkpoint scene, skips transition-gate entry motion, resolves the marker after the new hero is cached, restores health/state, rebinds the camera, and fades in. If no checkpoint was ever activated, same-scene fallback marker placement is used.

### activeHazardRespawnMarkerKey

Present in `PlayerSaveData` but not wired into runtime recovery yet. `HazardRespawnMarker` now exists for direct scene authoring from `HazardZone`, but first-pass local hazard recovery does not read or write this save key. The field remains reserved for a future trigger-updated active hazard marker path.

---

## SaveManager Public API

```csharp
// Properties
SaveData CurrentSave { get; }
int      CurrentSlot { get; }
string   ActiveRespawnMarkerKey { get; }
string   ActiveRespawnSceneName { get; }
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
string    GetStartupScene(string fallbackScene)

// Respawn keys (do not auto-save)
void SetActiveRespawnPoint(string sceneName, string markerKey)
void SetCurrentScene(string sceneName)
void SetActiveRespawnMarkerKey(string key) // deprecated compatibility wrapper
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
| Save-target list empty | Log warning; save/load of manager-owned metadata continues |
| Save-target entry missing, invalid, or duplicated | Log warning and skip that entry; remaining targets continue in Inspector order |
| No matching checkpoint `RespawnMarker` in the checkpoint scene | Log warning, use the first available `RespawnMarker`; if none exists, fall back to cached scene-entry position |
| Multiple `RespawnMarker`s with same key | Use first match, log warning about duplicate keys |
| A target receives a null data section | Migrator normalizes sections; individual targets also guard defensively |
| Missing/removed saved weather region | `WorldWeatherState.CompleteLoad` discards it with a warning; re-adding the region creates fresh state |
| Duplicate saved weather region ID | Completion keeps the first entry and warns |
| Invalid weather/history/override payload | Completion falls back, resets, or discards the malformed entry according to the Package 2 weather contract and warns |
| Weather completion before loaded world time | `CompleteLoad` throws; Bootstrap calls it only after time target application |

---

## SaveDataMigrator

```csharp
public static class SaveDataMigrator
{
    public const int CurrentSaveVersion = 6;

    public static void Migrate(SaveData data)
    {
        int originalVersion = data.meta?.saveVersion ?? 0;

        // Null-normalize sub-objects
        data.meta       ??= new MetaSaveData();
        data.player     ??= new PlayerSaveData();
        data.abilities  ??= new AbilitySaveData();
        data.health     ??= new HealthSaveData();
        data.resource   ??= new ResourceSaveData();
        data.world      ??= new WorldSaveData();
        data.worldTime  ??= new WorldTimeSaveData();
        data.worldWeather ??= new WorldWeatherSaveData();
        data.world.collectedPickupIds   ??= new List<string>();
        data.world.visitedRoomIds       ??= new List<string>();
        data.world.defeatedEncounterIds ??= new List<string>();
        data.world.objectStates         ??= new List<WorldObjectStateEntry>();

        data.worldWeather.regions  ??= new List<RegionWeatherSaveEntry>();
        data.worldWeather.overrides ??= new List<WeatherOverrideSaveEntry>();
        for (int regionIndex = 0; regionIndex < data.worldWeather.regions.Count; regionIndex++)
        {
            RegionWeatherSaveEntry region = data.worldWeather.regions[regionIndex];
            if (region == null)
                continue;

            region.regionId ??= "";
            region.history ??= new List<WeatherHistoryRecordSaveEntry>();
        }

        for (int overrideIndex = 0; overrideIndex < data.worldWeather.overrides.Count; overrideIndex++)
        {
            WeatherOverrideSaveEntry weatherOverride = data.worldWeather.overrides[overrideIndex];
            if (weatherOverride == null)
                continue;

            weatherOverride.regionId ??= "";
            weatherOverride.sourceTag ??= "";
        }

        // Null-normalize string fields
        data.player.currentScene                ??= "";
        data.player.activeRespawnSceneName      ??= "";
        data.player.activeRespawnMarkerKey      ??= "";
        data.player.activeHazardRespawnMarkerKey ??= "";
        data.meta.lastSavedUtc                  ??= "";

        if (originalVersion < 2
            && string.IsNullOrEmpty(data.player.activeRespawnSceneName)
            && !string.IsNullOrEmpty(data.player.activeRespawnMarkerKey))
        {
            // Best-effort v1 migration: testers should re-activate checkpoints
            // if currentScene no longer matches the checkpoint scene.
            data.player.activeRespawnSceneName = data.player.currentScene;
        }

        if (originalVersion < 3)
        {
            data.health.initialized = false;
            data.resource.initialized = false;
        }

        if (originalVersion < 5)
        {
            // Time was not persisted before v5. WorldTimeState supplies its authored fresh date.
            data.worldTime.initialized = false;
        }

        if (originalVersion < 6)
        {
            // Weather was not persisted before v6. WorldWeatherState supplies authored regional
            // initial weather and begins the history boundary at the loaded world minute.
            data.worldWeather.initialized = false;
        }

        // Version 3 and earlier had no room/encounter/object-state world sections. No transform
        // is needed beyond the null-coalescing above — an absent section is correctly empty.

        data.meta.saveVersion = CurrentSaveVersion;

        // Versions 3, 5, and 6 add initialized state sections. Their markers intentionally remain
        // false when absent so the owning state assets supply authored fresh-save defaults.
    }
}
```

When a schema change breaks backward compatibility:
1. Increment `CurrentSaveVersion`.
2. Add a `if (data.meta.saveVersion < N)` block to patch old data.
3. Existing saves load and migrate in place. Only truly incompatible saves fall back to a fresh state.

Version 5 is the Package 1 world-time addition. Version 6 is the Package 2 weather addition; it
does not reinterpret existing player/world/time state. It adds the canonical regional weather
payload and leaves `worldWeather.initialized == false` for pre-v6 saves so
`WorldWeatherState.CompleteLoad` creates authored initial regional state at the loaded minute.
Unsupported future-version rejection remains a separate technical-debt decision.

---

## SaveStats

`SaveStats` is a plain C# class built from `SaveData` via `SaveStats.FromSaveData(data)`. Fields: `isEmpty`, `saveVersion`, `currentScene`, `lastSavedUtc`, `playTimeSeconds`, `unlockedAbilityCount`.

`SaveManager.GetSaveStats(int slot)` reads and deserializes a slot's file without applying any state to runtime objects. Intended for a future save-slot selection screen that needs to show per-slot summaries before the player commits to loading.

---

## Future Expansion

### World Persistence Phase 3 / 3.1 (implemented; boss integration implemented)
Phase 1 covers the registry foundation, save schema, stable world object IDs, and the full ordinary-placed-enemy vertical slice. Phase 2 adds normal-death lifecycle integration (`GameManager.BeginRespawnSequence` calls `ResetRespawnableEnemyDeaths()`/`ResetUntilDeathState()` exactly once per death, before the checkpoint scene begins loading) and `AbilityPickup` reconciliation against `WorldStateRegistry.collectedPickupIds`, in favor of `PlayerAbilityState`. Phase 3 adds `PersistentDoor`/`PersistentSwitch`/`PersistentBreakable` (consuming `SetObjectState`/`SetUntilDeathState` via a shared `PersistenceLifetime` enum) and room-visitation. Phase 3.1 replaced the original scene-name-as-room-ID mechanism with `RoomVisitReporter`, an authored-`roomId` participant placed once per gameplay scene (so a `.unity` filename rename never changes saved room identity), and made `PersistentBreakable` non-resource-eligible by default (`HeroAttackResult.Damaged`/`Killed` gained an optional `resourceEligible` parameter for this). The coordinated boss path is now exercised by `boss_sample_04_executioner`: only `BossEncounterController` marks the permanent encounter fact, and disk persistence still occurs only at the ordinary checkpoint/quit save boundaries. See `Docs/Architecture.md`, `Docs/FeatureSpecs/BossEncounters.md`, and the historical plan below.

### AbilityPickup persistence (resolved)
`AbilityPickup.cs` now marks `WorldStateRegistry.MarkPickupCollected` alongside `PlayerAbilityState.Unlock` on collection, and reconciles the two on initialization in favor of `PlayerAbilityState`. This does not force an immediate disk save — per the `Save()` call-site rule below, collection only updates in-memory registry state; it is durably persisted at the next checkpoint or quit-save, same as any other mid-session progress.

### Multi-slot saves
The API is already slot-aware. `Save(int slot)`, `HasSave(int slot)`, `GetSaveStats(int slot)`, and `SaveFileStore.GetPath(int slot)` all accept any slot index. `save_slot{n}.json` naming is already in place. Adding multi-slot support requires only: a slot-selection UI that calls `LoadOrCreate(chosenSlot)` and `CreateFreshSave(chosenSlot)` at the right times.

### Scene-name-driven boot continue
`Bootstrap.Start()` calls `SaveManager.GetStartupScene(firstScene)` after `LoadOrCreate()`. Startup prefers `activeRespawnSceneName` when present and loadable so checkpoint placement starts in the correct scene, then falls back to `currentScene`, then the serialized `firstScene`. A future main menu can reuse this query for Continue, while New Game should still call `CreateFreshSave()` and route to `firstScene`.

### Play-time accumulation
`MetaSaveData.playTimeSeconds` is a stub at `0f`. Wire it by adding a `Time.unscaledDeltaTime` accumulator to `SaveManager.Update()` that increments while `GameManager.State == GameState.Playing`. Write the accumulated total into `CurrentSave.meta.playTimeSeconds` in `GatherSaveData()`.

### Save migration versioning
When a schema change needs data patching, increment `SaveDataMigrator.CurrentSaveVersion` and add a version-gated block. Version 2 adds `activeRespawnSceneName`; old saves migrate by copying `currentScene` only when a respawn marker key exists, which is best-effort and may require checkpoint re-activation if the old save's `currentScene` is not the checkpoint scene. Version 3 adds initialized health and resource sections; missing sections retain `initialized == false` so the state assets apply authored defaults. Version 4 adds `visitedRoomIds`, `defeatedEncounterIds`, and `objectStates` to `WorldSaveData` for `WorldStateRegistry`; no version-gated transform was needed since the null-coalescing normalization already leaves absent sections correctly empty. Unsupported future-version rejection is not implemented: the current migrator stamps any deserialized data to the current version. Treat a forward-version policy as separate technical debt.

### Package 1 schema note

Version 5 adds `WorldTimeSaveData`/`SaveData.worldTime`; pre-v5 data keeps `initialized == false`
so `WorldTimeState` applies its authored fresh date. Unsupported future-version rejection remains
a separate technical-debt decision.

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
