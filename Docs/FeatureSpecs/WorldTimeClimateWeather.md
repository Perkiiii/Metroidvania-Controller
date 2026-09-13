# Feature Spec — World Time, Climate, and Weather

**Last audited:** 2026-09-13 — Weather presentation Packages 1–3 completion; visual review pending

This spec records the implemented simulation Package 1–4 contract and the implemented bounded
weather presentation Packages 1–3. Expanded presentation and downstream consumers remain deferred.
The detailed roadmap remains in
`Docs/ImplementationPlans/WorldTimeClimateWeather.md`.

## Status and scope

Packages 1 (Calendar and World Time) and 2 (Climate Definitions and Regional Persistent State) are
implemented in runtime code, save schema, tests, and authored asset composition.
Package 3 adds the project-owned SplitMix64/XorShift32 kernel, independent-season weighted
generation, authored fixed-slot behavior, immutable three-period-per-region LRU, live progression,
actual history, exact-slot overrides, and immutable forecasts. Observed Unity 6000.3.10f1 EditMode
results: generator 10/10, progression/persistence 29/29, history 11/11, overrides 12/12,
forecast 13/13, existing WorldClimate 84/84, WorldTime 38/38, and explicit existing
climate/time union 122/122. Package 4 validator tests pass 9/9. Final focused EditMode for
simulation plus weather presentation is 169/169; the weather presentation lifecycle PlayMode
fixture is 1/1. Full EditMode is 764/765; the sole failure is the established unrelated
`CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion. No live screenshot,
hands-on visual/audio-mix validation, or profiling evidence was recorded.

Package 2 establishes:

- immutable calendar projections and the persistent canonical world-time state from Package 1;
- the append-only weather enum and metadata lookup;
- authored seasonal transition data and stable region definitions;
- persistent current weather, history/override DTO payloads, and nonzero regional root seeds;
- clock-independent save application followed by explicit post-load reconciliation; and
- narrow current-weather and root-seed queries.

Package 3 supplies deterministic generation, post-load live slot progression, actual-weather history
recording and read-only queries, exact-slot runtime overrides, and immutable forecasts. Simulation
Package 4 adds passive room climate metadata, safe matrix authoring, and project validation. Weather
presentation Packages 1–3 consume this state without changing the state contract; downstream
consumers remain later work.

## Ownership

| Owner | Responsibility | Boundary |
|---|---|---|
| `CalendarConfig` | Calendar shape, phases, fresh date, pacing | Does not own mutable time |
| `WorldTimeState` | Canonical minutes, snapshots, advancement, time events, time save | Does not own weather |
| `WorldClockDriver` | Unscaled real-time sampling and gameplay-state gating | Does not perform calendar math or persistence |
| `WeatherSimulationConfig` | Slot-hour policy and one-to-one weather metadata lookup | Does not own regional state |
| `WeatherDefinition` | Type/display/precipitation/severity metadata | No Unity presentation references |
| `SeasonDefinition` | Entry weather, 5×5 row-major transition weights, fixed slot records | Does not roll weights or own runtime history |
| `SeasonTrackDefinition` | Maps global season ordinal to a regional season | Does not own calendar time |
| `ClimateRegionDefinition` | Stable region ID, display name, season track, initial weather | Does not own mutable weather |
| `ClimateRegionCatalog` | Authored set and ordinal ID lookup | Does not own current weather |
| `WorldWeatherState` | Current regional weather, deterministic resolution, actual-weather history, exact-slot overrides, immutable forecast/history queries, serialized payloads, save/reconciliation | Does not own the clock, scenes, or presentation |
| `EnvironmentExposure` | Scene-authored `Outdoor`, `Sheltered`, or `Indoor` exposure enum | Does not drive weather or presentation |
| `RoomClimateContext` | Passive scene-local region ID and exposure metadata | No clock, weather, catalog, save, or lifecycle work |
| `RoomWeatherPresentation` | Maps actual room weather/exposure to a scene-local presentation request | Does not own simulation, clock, save, hero, or camera |
| `RoomRainPresentation` | World-space rain, authored splashes, emission ramp, owner-scoped ambience request | Does not own weather or unmanaged audio sources |
| `RoomStormPresentation` | Transient strike/flash cadence and delayed thunder request | Does not own weather, world time, persistence, gameplay, or camera shake |
| `Bootstrap` | Calls weather completion after all save targets apply | Does not implement weather rules |

`WorldWeatherState` is a persistent `ScriptableObject`/`ISaveTarget` sibling to `WorldTimeState`.
`SaveManager` remains a generic Inspector-ordered save pipeline and does not reference concrete
weather types. Regional weather is not stored in `WorldStateRegistry`, `GameManager`, or a scene
object.

## Authored climate model

The current weather enum is an append-only persistence/content contract:

```text
Clear = 0
Cloudy = 1
Rain = 2
Storm = 3
Fog = 4
```

There is no `None` value. Unknown region IDs and invalid weather values use fail-closed query or
fallback behavior rather than an ambiguous empty weather. New weather values must append and all
season matrices/assets must be updated before a save/content contract is shipped.

`WeatherSimulationConfig` authors sorted unique slot hours `{6, 18}` and a maximum forecast
allocation guard of `28`; it contains exactly one `WeatherDefinition` for each enum value. These
values are consumed by the implemented Package 3 simulation and do not move ownership into room
metadata.

Each `SeasonDefinition` contains:

- an authored ID/display name and `entryWeatherType`;
- a complete 5×5 row-major `transitionWeights` array, indexed as
  `(int)from * weatherTypeCount + (int)to`; and
- optional concrete fixed records using one-based `dayInSeason` and nonnegative `slotIndex`.

Every matrix row must contain nonnegative values, have a positive total, and remain within the
`uint` cumulative-weight range. Fixed keys must be valid for the calendar/slot configuration and
must not duplicate. `SeasonTrackDefinition` contains exactly `CalendarConfig.SeasonsPerYear`
season references. `ClimateRegionCatalog` rejects null definitions, empty IDs, duplicate IDs, and
invalid region dependencies; IDs are case-sensitive and are not derived from scene names.

The authored Package 2 catalog currently contains one provisional region, `region_underbrew`,
with display name `Underbrew`, initial weather `Clear`, and a four-entry Spring/Summer/Autumn/
Winter track. The content is a minimal implementation slice and is not final world design.

## Regional state and queries

`WorldWeatherState` maintains one runtime record per valid catalog region. A record contains the
current weather type, the minute at which that current value began, a history-availability minute,
a nonzero `uint` root seed, and closed actual-weather history entries. Exact-slot overrides remain
separate resolution payloads; they never mutate generated cache data or rewrite past history.

Current queries are:

```csharp
bool TryGetWeather(string regionId, out WeatherSnapshot snapshot);
bool TryGetRootSeed(string regionId, out uint rootSeed);
bool TryGetForecast(string regionId, int daysAhead, out WeatherForecast forecast);
bool TryQueryHistory(string regionId, long fromInclusive, long toExclusive,
    out WeatherHistoryQueryResult result);
bool SetOverride(WeatherOverrideEntry entry);
bool ClearOverride(string regionId, long absoluteDayIndex, int slotIndex);
```

`WeatherSnapshot` is immutable and reports the stable region ID, current type, start minute, and
elapsed duration relative to the loaded `WorldTimeState`. Unknown IDs, empty IDs, and queries made
before completion return `false`.

The deterministic generator derives each season from the persisted root seed and absolute season
instance, so generation is independent across seasons and independent of query order. It uses the
approved SplitMix64 seed derivation and owned XorShift32/`NextBelow` stream, honors authored fixed
slots without consuming a roll, and retains an immutable three-period-per-region LRU. It never
uses `UnityEngine.Random` for generation and never mutates global weather state.

After completion, `WorldWeatherState` binds exactly once to `WorldTimeState.HourChanged`. Every
crossed configured slot is resolved in catalog order with precedence
runtime exact-slot override > authored fixed slot > generated weather. Actual type changes use one
atomic mutation path that closes/merges history, updates the current segment, and emits
`WeatherChanged`; same-type resolutions are no-ops. Repeated completion, save apply/rebind, and
catalog recompletion preserve the latest runtime state and do not duplicate subscriptions.

`TryQueryHistory` is read-only and returns clipped half-open actual-weather records plus the
synthetic open segment, with truthful `IsComplete` reporting when a region was initialized after
the requested start. `TryGetForecast` is read-only, inclusive in `daysAhead`, emits the remainder
of today plus strictly future slots through the requested end, and crosses season/year instances
without changing current state, history, overrides, seeds, cache state, or subscriptions.

`SetOverride` and `ClearOverride` validate exact region/day/slot keys. Future changes affect later
resolution, past changes do not rewrite history, and active-slot changes re-resolve immediately
through the same mutation path at the current world minute. Replacing an override preserves its
key position for deterministic save output. The root seed is generated only when a region needs
initial state and is then persisted.

## Load lifecycle and reconciliation

Save-target Inspector order is not a weather lifecycle contract. The implemented flow is:

```text
SaveManager.LoadOrCreate()
  → migrate/null-normalize SaveData v6
  → WorldTimeState.ApplySaveData()
  → WorldWeatherState.ApplySaveData()
  → all other assigned ISaveTarget.ApplySaveData() calls

Bootstrap.Start()
  → WorldWeatherState.CompleteLoad(WorldTimeState)
  → health normalization and startup transition
```

`ApplySaveData` only deep-copies `SaveData.worldWeather` into a private pending DTO and clears the
previous runtime view. It does not read the clock, validate against the current timestamp, bind
events, or mutate the authored definitions. `CompleteLoad` requires a loaded `WorldTimeState`,
validates the simulation config/catalog/season graph against calendar and slot cardinality, then
reconciles the pending DTO against the current authored catalog. Repeating completion for an
unchanged catalog is idempotent.

Reconciliation rules:

- A configured region missing from the save is created at the loaded minute with authored initial
  weather, history availability at that minute, and a new nonzero root seed.
- A saved region removed from the catalog is discarded with a warning. Re-adding it later creates
  fresh state rather than resurrecting the discarded entry.
- Duplicate saved region IDs use the first entry and warn.
- Invalid saved weather values fall back to that region’s authored initial weather and warn.
- Negative or future timestamps are rejected, clamped, or reset to a truthful loaded-minute
  boundary according to the malformed-entry rules; malformed/overlapping history is discarded for
  that region and malformed overrides are ignored, with warnings.
- Valid saved root seeds remain authoritative. Newly generated seeds are reserved against existing
  valid seeds so newly created regions do not collide in the same completion pass.

Completion does not advance the clock or publish a weather-changed event. It validates/reconciles
the state, rebuilds the nonpersisted generator cache, then binds the one time subscriber. On catalog
change it snapshots current runtime weather, history, and overrides before rebuilding so active
runtime behavior is not lost.

## Save contract

The current save schema is v6. Package 2 adds `SaveData.worldWeather`:

```csharp
[Serializable] public class WorldWeatherSaveData {
    public bool initialized;
    public List<RegionWeatherSaveEntry> regions = new List<RegionWeatherSaveEntry>();
    public List<WeatherOverrideSaveEntry> overrides = new List<WeatherOverrideSaveEntry>();
}

[Serializable] public class RegionWeatherSaveEntry {
    public string regionId = "";
    public int currentWeatherType;
    public long currentWeatherStartMinute;
    public long historyAvailableFromMinute;
    public uint rootSeed;
    public List<WeatherHistoryRecordSaveEntry> history = new List<WeatherHistoryRecordSaveEntry>();
}

[Serializable] public class WeatherHistoryRecordSaveEntry {
    public int weatherType;
    public long startMinute;
    public long endMinuteExclusive;
}

[Serializable] public class WeatherOverrideSaveEntry {
    public string regionId = "";
    public long absoluteDayIndex;
    public int slotIndex;
    public int weatherType;
    public string sourceTag = "";
}
```

`JsonUtility` receives flattened lists rather than dictionaries. `SaveDataMigrator` always
null-normalizes the weather section, region/override lists, region history lists, region IDs, and
override source tags. Saves with a version below 6 retain `worldWeather.initialized == false`;
completion then creates authored regional state at the loaded world minute. The existing Package 1
v4→v5 time migration remains unchanged. The current implementation verifies the intended `uint`
root-seed round-trip in its focused test coverage; no fallback representation is used in the DTO.

`GatherSaveData` serializes the reconciled current records and payload lists. Before completion,
the weather DTO is marked uninitialized and no stale runtime state is written.

## Composition and validation boundary

Package 2 assets are under `Assets/_Project/ScriptableObjects/World/`:

```text
WeatherSimulationConfig.asset
WorldWeatherState.asset
ClimateRegionCatalog.asset
WeatherDefinitions/{Clear,Cloudy,Rain,Storm,Fog}.asset
Seasons/{Spring,Summer,Autumn,Winter}.asset
Seasons/UnderbrewSeasonTrack.asset
Seasons/Underbrew.asset
```

`WorldWeatherState.asset` references the simulation config and catalog, is included in
`Assets/_Project/Prefabs/Managers/_SaveManager.prefab`, and is assigned to
`Bootstrap.worldWeatherState` in `Assets/_Project/Scenes/Boot.unity`. Simulation Package 4 authors
exactly one root `RoomClimateContext` in each enabled `SampleScene` through `SampleScene4`, all
provisionally `region_underbrew` / `Outdoor`; `Boot` has none. `UISandbox` is disabled and excluded.

Focused definition, generator, progression, history, override, and forecast tests are present under
`Assets/_Project/Scripts/Editor/Tests/WorldClimate/`. The Package 2 definition/persistence
fixtures pass 28/28 in the historical isolated Unity 6.0.3.10f1 EditMode run (9 definition, 19
persistence). Current focused Package 3 fixtures pass generator 10/10, progression/persistence
29/29, history 11/11, overrides 12/12, and forecast 13/13; existing WorldClimate and WorldTime
pass 84/84 and 38/38, with the explicit union at 122/122. Package 4 validator tests pass 9/9.
Final focused EditMode for simulation plus weather presentation is 169/169; the weather lifecycle
PlayMode fixture is 1/1. Full EditMode is 764/765; the sole failure is the established unrelated
`CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion. No live screenshot, hands-on
visual/audio-mix validation, or profiling evidence was recorded. Simulation Package 4 changed
scenes and docs only; presentation Packages 1–3 add only the separate SampleScene composition and
production weather assets described below, with no save/schema change.

## Room context and authoring tools (Package 4)

Package 3A–3E is implemented without a save-version change: the v6 DTO now carries the populated
history and override payloads, while generator cache and query-result collections remain
nonpersisted/immutable. Weather binds to time only after the existing post-load completion seam and
does not move canonical time ownership.

Package 4 is implemented as runtime metadata and Editor tooling only:

- `EnvironmentExposure` is `Outdoor`, `Sheltered`, or `Indoor`.
- `RoomClimateContext` is `[DisallowMultipleComponent]` passive scene metadata with private
  serialized `regionId`/`exposure` and read-only properties. It has no simulation, save, catalog,
  clock, or lifecycle responsibility.
- `SeasonDefinitionEditor` presents rows as FROM and columns as TO, shows row totals, provides
  `Set Uniform` and `Clear Row`, preserves existing cells when the append-only enum grows, and
  confirms destructive shrink/reset operations. It has no “Normalize to 100” operation.
- `WorldTimeClimateValidator` is available at `Tools/Project/Validate World Time & Climate`.
  It validates the catalog/season graph against `WorldTimeState`, then checks enabled Build
  Settings scenes: zero contexts in `Boot`, exactly one context in every other enabled scene, and
  a nonblank region ID present in the catalog. Shared region IDs are allowed; disabled scenes are
  skipped. The authored pass covers five enabled scenes, four contexts, and zero issues.

The four scene assignments are provisional content. The state contract remains simulation-only;
the bounded weather presentation consumer is documented separately below.

## Weather presentation vertical slice (Packages 1–3)

This first production presentation pass stops at `Clear → Rain → Storm → Clear` for human visual
review. It consumes actual `WorldWeatherState` output and never chooses/generates weather.

- `RoomWeatherPresentation` reads the matching scene `RoomClimateContext`, refreshes on enable,
  responds to that region's `WeatherChanged`, maps outdoor Rain/Storm to presentation requests, and
  resets/unsubscribes safely on disable, destroy, or room unload.
- `RoomRainPresentation` owns one world-space, gameplay-camera-framed `WorldRain` ParticleSystem,
  three authored upward-facing `GroundSplash` emitters using the six-frame atlas, and short
  emission ramps. Rain ambience starts/fades/stops through the owner-scoped `AudioManager`
  ambience channel; the weather prefab owns no AudioSource.
- `RoomStormPresentation` owns a transient unscaled strike cadence, a hidden view-bounded
  `LightningFlash` SpriteRenderer with a restrained multi-pulse envelope, and delayed thunder via
  `AudioManager.PlaySFX`. Storm exit and lifecycle teardown cancel future strikes and pending
  thunder; transient timers are not persisted.

Only `Assets/_Project/Scenes/SampleScene.unity` is authored for this slice, using
`Assets/_Project/Prefabs/Weather/Rain/RoomRainPresentation.prefab` and project-owned rain/splash
materials, `Assets/_Project/Audio/Weather/Rain.wav`, `Thunder.wav`, and the copied
`Assets/_Project/Art/Weather/Rain/RainSplashFlipbook.png`; the flash uses the existing project-owned
`Assets/_Project/Lights/white_fader.png` and `Assets/_Project/Shaders/SpriteFlash.mat`. Focused
EditMode validation is 169/169;
the weather lifecycle PlayMode fixture is 1/1. Full EditMode is 764/765 with only the established
unrelated `CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion failing. No live
screenshot, hands-on visual/audio-mix validation, or profiling evidence is recorded.

### Editor-only weather presentation testing

`WeatherDebugWindow` is available at `Tools/Underbrew/Weather Debug` and lives under
`Assets/_Project/Scripts/Editor/`, so it is excluded from player builds. It resolves the existing
`WorldTimeState` and `WorldWeatherState` assets, plus exactly one enabled `RoomClimateContext` in
the active loaded scene. The window shows the room region ID, current absolute clock day, active
weather slot, the override-key day/slot used by the exact-slot commands, and the current actual
weather returned by `WorldWeatherState`.

Controls are enabled only in Play Mode when both states are loaded, the context region and weather
configuration are valid, an active slot can be resolved, and an actual-weather query succeeds.
`Force Clear`, `Force Rain`, and `Force Storm` call the existing
`SetOverride(new WeatherOverrideEntry(...))` for that active context/slot; `Clear Current Override`
calls the existing `ClearOverride(...)`. The tool does not touch presentation effects or keep
persistent debug state, so the normal `WeatherChanged` flow drives presentation.

Focused weather/presentation/debug validation is 54/54. Live GUI/menu smoke and hands-on visual
validation of this tool were not performed.

Shelter policy, foreground rain, wind, ambient leaves/motes, camera feedback, wet surfaces, fog,
day/night presentation, post-processing profiles, additional weather types, and broader polish
are deferred until this slice receives human review.
