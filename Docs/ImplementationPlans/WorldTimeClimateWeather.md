# Review Outcome

Status: **Packages 1–4 implemented.**
Package 1 (Calendar and World Time) and Package 2 (Climate Definitions and Regional Persistent
State) runtime code, save DTO/migration, focused EditMode coverage, generated assets, and
serialized `SaveManager`/`Boot` assignments are present in the current checkout. Package 4 adds
passive room metadata, safe matrix authoring, the project validator, and authored scene contexts.
Observed Unity 6000.3.10f1 EditMode results are validator 9/9; generator 10/10,
progression/persistence 29/29, history 11/11, overrides 12/12, forecast 13/13; existing
WorldClimate 84/84, WorldTime 38/38, and explicit existing climate/time union 122/122. Full
EditMode is 727/727. The historical camera failure did not reproduce and its fixture passed
23/23. No PlayMode was applicable; no manual visual or interactive matrix-Inspector/Undo
validation was claimed.

The original proposal's central shape is approved: `WorldTimeState` owns canonical calendar state, `WorldClockDriver` converts real time into game minutes, `WorldWeatherState` owns persistent regional weather, authored climate assets describe region/season behavior, and `RoomClimateContext` is a passive scene-local lookup seam. Time/weather simulation remains independent of presentation, farming, NPCs, audio, and the hero.

The weather shape and binding rules below are implemented through Package 4. Climate definitions,
regional persistent state, deterministic generation, temporal behavior, room contexts, and safe
authoring/validation tools are complete. Presentation remains a later consumer.

Binding corrections made by this review:

- Save application no longer depends on Inspector ordering. `WorldWeatherState.ApplySaveData` only
  copies weather data; after all save targets apply, `Bootstrap` calls
  `WorldWeatherState.CompleteLoad(worldTimeState)` to validate and reconcile regions. Package 2
  binds time only after completion; Package 3 owns temporal weather behavior.
- The calendar has a fixed epoch and exact formulas. There is no implementer-defined offset arithmetic.
- Time skips advance between published boundaries, not minute by minute. Every crossed hour, day, season, year, and phase boundary is emitted in chronological order.
- Both Pause and Gameplay Menu are verified to call `GameManager.Pause`; transitions also stop the clock. Hit-stop leaves `GameState.Playing`, so the unscaled clock intentionally continues through its brief presentation freeze.
- Package 3 keeps generation and query state deterministic and non-global. `WorldWeatherState` is
  the sole actual-weather owner and binds the existing clock only after post-load reconciliation.
- Forecast determinism uses a project-owned, exactly specified mixer and PRNG, not `System.Random` output that could vary with runtime changes.
- Each season forecast intentionally has an authored entry weather rather than accidentally starting from `Clear`.
- History is not trimmed initially. Queries still report completeness because migrated saves and newly added regions cannot describe weather before their initialization minute.
- `WeatherSimulationConfig` owns the small `WeatherType -> WeatherDefinition` mapping. No extra catalog is introduced.
- `WeatherType` has no `None`; unknown regions use `Try...` APIs.
- Room exposure is one small enum, not overlapping `isIndoor` and `exteriorWeatherVisible` booleans.

Remaining decisions are content decisions: final season/region names, transition weights, fresh-save
date, phase hours, region assignments, and clock pace. The current Package 2 content is a minimal
Underbrew slice and is provisional. Calendar shape, weather slot layout, matrices, and weather
ordinals must be frozen before production saves/content; changing either later requires an explicit
migration.

---

## 1. Executive recommendation

The roadmap comprises four additive packages; all four are implemented:

1. **Calendar and World Time** — complete clock, math, events, persistence, and runtime driver. **Implemented.**
2. **Climate Definitions and Regional Persistent State** — final authored data graph, current weather per region, root seeds, and final weather DTOs.
3. **Deterministic Weather, Forecast, History, and Overrides** — final generator and all temporal weather behavior. **Implemented.**
4. **Room Context and Authoring Tools** — scene climate tags, validators, and transition-matrix inspector.

Each package must compile and be useful alone. Packages 1–4 are implemented and EditMode-tested;
no PlayMode run was applicable, and no manual visual or interactive matrix-Inspector/Undo
validation was claimed. Package 2 authored assets and the four Package 4 room assignments remain
provisional content until production freeze.

Out of scope: rendering, particles, lighting, post-processing, weather audio, farming, NPC schedules, shops, calendar/forecast UI, festivals, fishing, and enemy behavior.

---

## 2. Sources and current Underbrew state

Authority order used:

1. Current Underbrew code.
2. `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, `Docs/FeatureSpecs/SaveSystem.md`, and `Docs/ImplementationPlans/WorldPersistence.md`.
3. Original decompiled Doloc Town C# under `C:\Users\samue\Desktop\Mods\Doloc Town`.
4. Supplied research reports as navigation/presentation context only.

Verified repository facts:

- `SaveManager` stores a serialized `List<ScriptableObject>`, filters it to `ISaveTarget`, and invokes targets in Inspector order. Its prefab is `Assets/_Project/Prefabs/Managers/_SaveManager.prefab` and currently has six targets, including `WorldTimeState.asset` and `WorldWeatherState.asset`.
- `SaveDataMigrator.CurrentSaveVersion` is **6**. `SaveData.worldTime` and `SaveData.worldWeather`
  are plain `[Serializable]` sections; weather regions/history/overrides are flattened lists for
  `JsonUtility`.
- `Bootstrap.Awake` has `WorldClockDriver` and weather-state seams and instantiates the driver after
  `GameManager` and `SaveManager`. `Bootstrap.Start` loads/creates the save, applies all assigned
  save targets, calls `WorldWeatherState.CompleteLoad(worldTimeState)` after target application,
  normalizes loaded health, configures the world graph, and begins startup transition.
- Package 1 runtime files are present under `Assets/_Project/Scripts/World/Time/`, with focused
  tests under `Assets/_Project/Scripts/Editor/Tests/WorldTime/`. The serialized
  `CalendarConfig.asset`, `WorldTimeState.asset`, `_WorldClockDriver.prefab`, `_SaveManager.prefab`
  target entry, and `Boot.unity` driver reference are present at the paths in §25. This is asset
  composition evidence only, not a claim of Editor import or runtime validation.
- `GameManager.State` defaults to enum value `Playing`; therefore the clock also needs a state-level load gate before `Bootstrap.Start` applies the save.
- `UIFlowController.OpenRoot` calls `GameManager.Pause()` for both Pause and Gameplay Menu. The old proposal's menu uncertainty was incorrect.
- `GameManager.HitStop` changes only `Time.timeScale`, waits in real time, and leaves `GameState` unchanged.
- Stable room identity already uses `RoomVisitReporter.roomId`; the validator is `Assets/_Project/Scripts/Editor/Validation/WorldPersistenceValidator.cs`.
- Dedicated `ISaveTarget` ScriptableObjects own persistent state. Time/weather must not enter `WorldStateRegistry` or `GameManager`.
- Package 1 supplies the calendar/time implementation. Package 2 supplies first-party climate
  definitions, regional persistent state, weather DTOs/migration, tests, and authored assets.
  Package 3 supplies deterministic generation and temporal weather behavior. Package 4 room/tool
  integration is implemented; presentation and downstream consumers remain future work.
- Production scripts use default `Assembly-CSharp` and no namespaces.

No hero, camera, audio, or validated tuning file is modified.

---

## 3. Verified Doloc findings

Direct source inspection established:

- `TimeArchiveData` persists both `totalSeconds` and mutable `DateInfo`, advances a time unit at a time, and embeds `ClimateManager`.
- `ClimateManager` owns one `WeatherSystem` per season-group string, removes saved groups missing from config, and creates newly configured groups.
- `WeatherSystem` persists current weather, seed, history, and patches. A change records the prior segment before replacing current weather.
- Weather resolves at hardcoded 06:00/18:00. `SeasonInfo.GenWeatherMap` hardcodes 28 days independently of configured month length.
- `SeasonInfo.GenWeatherMap` initializes Markov state to integer `1` for every month. Doloc therefore does not chain one month's final weather into the next.
- Doloc derives month seeds by sequential `System.Random.Next()` replay and mutates cached forecast dictionaries when applying patches.
- Doloc matrix validation checks row count but only whether *any* row has correct length. Underbrew must validate every row and non-negative weights.
- Doloc history is compact timed segments and supports unloaded-room catch-up.

Reuse: one world clock, regional authorities, fixed weather slots, Markov rows, deterministic periods, authored fixed weather, compact history, and off-screen queries.

Improve: one canonical timestamp; exact derivation; configured season length; owned RNG; non-destructive overrides; explicit history completeness; no initial trimming; no second-by-second room replay; no presentation dependencies.

---

## 4. Final architecture

```text
CalendarConfig
      |
      v
WorldTimeState : ScriptableObject, ISaveTarget <--- WorldClockDriver : MonoBehaviour
      |                                              serialized state reference only;
      |                                              real-time sampler reads state pacing
      | sparse ordered boundary events
      v
WorldWeatherState : ScriptableObject, ISaveTarget (Packages 2–3)
      |-- current weather + root seed per region
      |-- actual-weather history + immutable history queries
      |-- exact-slot overrides + immutable forecasts
      `-- persisted history/override payloads (v6)
             ^
             |
ClimateRegionCatalog
      `-- ClimateRegionDefinition
             `-- SeasonTrackDefinition
                    `-- SeasonDefinition

WeatherSimulationConfig
      |-- slot hours / forecast limit
      `-- WeatherDefinition list

Loaded scene -> RoomClimateContext (region ID + exposure only; Package 4)

Future readers: presentation | farming | NPCs | shops | festivals | fishing | resources | audio
```

No service locator is added. The driver is persistent because it is the process-wide real-time sampler; state lives in persistent SO assets and remains available with no gameplay scene loaded.

---

## 5. Ownership

| Owner | Owns | Must not own |
|---|---|---|
| `CalendarConfig` | Calendar shape, epoch weekday, phases, fresh date, pace | Mutable time |
| `WorldTimeState` | Canonical minutes, derivation, advancement, events, save | Unity delta time, weather |
| `WorldClockDriver` | Serialized `WorldTimeState` reference, unscaled accumulator, and gameplay-state gating; reads `WorldTimeState.RealSecondsPerGameMinute` | `CalendarConfig` reference, calendar math, second clock, persistence |
| `WeatherSimulationConfig` | Slot policy, forecast guard, weather metadata lookup | Regional runtime state |
| `ClimateRegionCatalog` | Complete region set and stable-ID lookup | Current weather |
| `ClimateRegionDefinition` | ID, name, season track, initial weather | History |
| `SeasonTrackDefinition` | Global season ordinal -> regional season | Calendar time |
| `SeasonDefinition` | Entry weather, weights, authored fixed slots | Runtime overrides/history |
| `WorldWeatherState` | Current weather/seeds, persisted history/override payloads, save, post-load reconciliation | Clock ownership, progression, scene/presentation objects |
| `RoomClimateContext` | Loaded room region and exposure | Simulation authority |
| Future presentation owner | Final Unity visual/audio writes | Physical time/weather state |

---

## 6. Calendar model and exact epoch

Fixed clock units:

```text
MinutesPerHour = 60
HoursPerDay    = 24
MinutesPerDay  = 1440
```

`CalendarConfig` authors `daysPerSeason > 0`, `seasonsPerYear > 0`, `epochWeekDay` (recommended Monday), strictly ordered phase hours `0 <= dawn < day < dusk < night < 24`, a valid fresh-save year/season/day/hour/minute, and `realSecondsPerGameMinute > 0`.

Binding epoch:

```text
TotalGameMinutes = 0
= Year 1, SeasonOrdinal 0, DayInSeason 1, 00:00, epochWeekDay
```

Negative timestamps are forbidden. For `M = TotalGameMinutes`:

```text
minuteOfDay         = M % 1440
TotalDays           = M / 1440                         // zero-based absolute day
Hour                = minuteOfDay / 60
Minute              = minuteOfDay % 60
DayInSeason         = (TotalDays % daysPerSeason) + 1 // one-based
SeasonInstanceIndex = TotalDays / daysPerSeason       // zero-based absolute season
SeasonOrdinal       = SeasonInstanceIndex % seasonsPerYear
Year                = 1 + SeasonInstanceIndex / seasonsPerYear
WeekDay             = (WeekDay)(((int)epochWeekDay + (TotalDays % 7)) % 7)
Day01               = minuteOfDay / 1440f             // [0,1)
```

Phase ranges are `[dawn,day)=Dawn`, `[day,dusk)=Day`, `[dusk,night)=Dusk`, and `[night,24) U [0,dawn)=Night`.

Fresh-save conversion:

```text
initialTotalDays =
    (((long)initialYear - 1) * seasonsPerYear + initialSeasonOrdinal)
    * daysPerSeason + (initialDayInSeason - 1)

ComputeInitialTotalGameMinutes() =
    initialTotalDays * 1440L + initialHour * 60L + initialMinute
```

Example: with 28-day seasons, four seasons, Monday epoch, Year 2 / Season 1 / Day 3 / 08:15 is absolute day 142, minute 204975, Wednesday.

Calendar shape (`daysPerSeason`, `seasonsPerYear`) reinterprets all timestamps/forecast keys. Before shipped content it may change only with deliberate save resets and validation. Afterwards it is immutable unless a versioned migration converts old values with the old constants. Pace, fresh start, and phase boundaries do not reinterpret existing timestamps.

---

## 7. Core time types

```csharp
public enum WeekDay { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }
public enum DayPhase { Dawn, Day, Dusk, Night }
```

`WorldTimeSnapshot` is immutable and contains `long TotalGameMinutes`, `long TotalDays`, `long SeasonInstanceIndex`, `long Year`, `int SeasonOrdinal`, `int DayInSeason`, `WeekDay`, `Hour`, `Minute`, `Day01`, and `Phase`. Do not narrow absolute counters to `int`. `From(long, CalendarConfig)` is pure and uses only §6.

---

## 8. `WorldTimeState` contract

`WorldTimeState : ScriptableObject, ISaveTarget` serializes the `CalendarConfig` reference and owns
nonserialized `long totalGameMinutes` plus loaded state. It exposes the config's
`RealSecondsPerGameMinute` to the driver; the driver does not serialize or retain a direct
`CalendarConfig` reference.

```csharp
long TotalGameMinutes { get; }
WorldTimeSnapshot Current { get; }
bool IsLoaded { get; }
float RealSecondsPerGameMinute { get; }
void AdvanceMinutes(long minutes);
void GatherSaveData(SaveData data);
void ApplySaveData(SaveData data);

event Action<WorldTimeSnapshot> HourChanged;
event Action<WorldTimeSnapshot> DayChanged;
event Action<WorldTimeSnapshot> SeasonChanged;
event Action<WorldTimeSnapshot> YearChanged;
event Action<WorldTimeSnapshot> DayPhaseChanged;
event Action<WorldTimeSnapshot, WorldTimeSnapshot> TimeAdvanced;
event Action<WorldTimeSnapshot> StateApplied;
```

- Apply saved minutes only when `worldTime.initialized`; otherwise use the authored fresh value. Validate non-negative values and raise `StateApplied` once.
- Gather allocates the DTO, writes `initialized=true`, and copies the canonical long.
- `AdvanceMinutes(0)` is no-op; negative throws `ArgumentOutOfRangeException`; checked overflow throws. Backward time and public absolute setters are absent.
- Only the driver and explicit future sleep/debug commands advance time.
- Guard against callback reentrancy into `AdvanceMinutes`.

---

## 9. Advancement and event semantics

Do not loop per minute. Since phases begin on whole hours, iterate crossed **hour boundaries**:

1. Capture `before` and checked `target`.
2. Find the next multiple of 60 strictly greater than current.
3. At every boundary `<= target`, set canonical time to it, derive one snapshot, then emit in fixed order: `HourChanged`, `DayChanged`, `SeasonChanged`, `YearChanged`, `DayPhaseChanged` when applicable.
4. Set canonical time to `target` after the last boundary.
5. Emit `TimeAdvanced(before, Current)` once.

At midnight, hour fires before day/season/year so subscribers see the new day. Phase fires at **every crossed phase boundary**. A Dawn-to-Dawn 24-hour skip emits Day, Dusk, Night, Dawn. Complexity is O(crossed hours), not minutes. Future multi-year offline systems should use bulk `[from,to)` queries rather than scene-object replay.

---

## 10. `WorldClockDriver` contract

Persistent prefab MonoBehaviour with exactly one serialized world-time reference
(`WorldTimeState`), a hitch clamp, and a private fractional `double` accumulator. It reads
`WorldTimeState.RealSecondsPerGameMinute`; it does not serialize/retain `CalendarConfig`, expose
`Instance`, do calendar math, persist its fraction, or touch `Time.timeScale`. A private static
duplicate guard is allowed only to destroy accidental duplicates.

Tick only when `WorldTimeState.IsLoaded`, `GameManager.Instance` exists, and state is `Playing`. Accumulate `min(Time.unscaledDeltaTime,maxRealSecondsPerFrame) * debugMultiplier`; convert complete quanta using `RealSecondsPerGameMinute`, retain remainder, call `AdvanceMinutes` once.

Explicit behavior:

- Pause and Gameplay Menu: stopped.
- Loading/EnteringLevel/ExitingLevel: stopped.
- Before save application: stopped.
- Hit-stop: continues, because game state remains Playing and unscaled time is intentional.
- Excess hitch/alt-tab duration: discarded by clamp.

`Bootstrap.Awake` instantiates driver after GameManager and SaveManager. Its root uses `DontDestroyOnLoad`.

---

## 11. Weather definition model and slot policy (Package 2 implemented)

```csharp
public enum WeatherType { Clear = 0, Cloudy = 1, Rain = 2, Storm = 3, Fog = 4 }
```

Append-only; no `None`. Unknown-region APIs return false. Invalid serialized enums normalize to region initial weather with warning.

`WeatherDefinition` contains type, display name, `isPrecipitation`, and `isSevereWeather`; no presentation references.

`WeatherSimulationConfig` contains sorted unique nonempty `weatherSlotHours` within 0..23 (the
authored asset uses `{6,18}`), `maxForecastDaysAhead` (the authored asset uses 28 as the future
allocation guard), exactly one definition per enum, and `TryGetDefinition`. It is the smallest
coherent lookup owner; do not add `WeatherDefinitionCatalog`.

Two slots/day is approved for a 30–40-real-minute day. Presentation may blend while state changes at slots. Slot layout is save/content shape; changing it after overrides exist requires migration.

---

## 12. Regional climate model (Package 2 implemented)

`ClimateRegionDefinition`: stable case-sensitive nonempty ID, display name, `SeasonTrackDefinition`, valid initial weather. IDs are never scene-name derived.

`ClimateRegionCatalog` is the full simulated set and builds a dictionary once. Nulls/duplicates are
validation errors; an invalid catalog fails completion closed rather than silently choosing one.
`WorldWeatherState.CompleteLoad` validates the catalog against the loaded calendar’s season count
and slot count before it creates runtime region records.

`SeasonTrackDefinition` maps each global season ordinal to a regional `SeasonDefinition`; list length equals `SeasonsPerYear`. Tracks remain separate assets because several regions may reuse one annual climate while others map the same global ordinal differently.

Reconciliation implemented in Package 2:

- Missing catalog region: create at loaded minute, assign/persist nonzero seed, use region initial weather in Package 2 and active deterministic slot in Package 3, set history availability to loaded minute.
- Saved region absent from catalog: discard with warning during completion. Re-adding later does not restore old state.
- Unknown query: false, never an ambiguous empty result.

Package 3 extends the current-weather/root-seed surface with deterministic slot resolution,
post-completion `HourChanged` progression, actual-weather history recording and queries, exact-slot
overrides, and immutable forecasts. `WorldWeatherState.ApplySaveData` still only deep-copies the
weather DTO into a pending buffer; `CompleteLoad(WorldTimeState)` remains the explicit
clock-dependent reconciliation step and is idempotent for an unchanged catalog. Catalog-changing
recompletion snapshots current runtime weather/history/overrides before rebuilding.

---

## 13. Season transition data

`SeasonDefinition` has ID/name, valid `entryWeatherType`, flattened row-major `int[] transitionWeights`, and fixed slots.

For `N = WeatherType count`: length is `N*N`; index is `(int)from*N+(int)to`; all values nonnegative. Rows are FROM and columns TO. Totals need not equal 100. Every row must total >0; runtime defensively holds `from` and logs once if invalid. Sum in `long` and reject totals above `uint.MaxValue`.

`FixedWeatherSlot` uses one-based `dayInSeason`, concrete `slotIndex`, and type. Do not use `-1`; editor convenience may create all daily slots. Duplicate day/slot keys are errors. A fixed value becomes current for the next Markov roll.

Enum ordinals deliberately index matrices. New types append only; expand every matrix while preserving the old top-left cells.

---

## 14. Exact deterministic algorithm (Package 3A implemented)

Generation never calls `UnityEngine.Random` or `System.Random`. Region creation may choose a root seed once using `UnityEngine.Random.Range`; reproducibility starts after persisting the nonzero `uint`.

Derive `(rootSeed, SeasonInstanceIndex)` with SplitMix64:

```text
x = ((ulong)rootSeed << 32) ^ (ulong)seasonInstanceIndex
z = x + 0x9E3779B97F4A7C15
z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9
z = (z ^ (z >> 27)) * 0x94D049BB133111EB
z = z ^ (z >> 31)
seed32 = low32(z) XOR high32(z)
if seed32 == 0: seed32 = 0xA341316C
```

Use owned XorShift32:

```text
state ^= state << 13
state ^= state >> 17
state ^= state << 5
```

For unbiased `NextBelow(bound)`, `bound>0`: `threshold = unchecked(0u-bound)%bound`; draw until `r>=threshold`; return `r%bound`. All operations are explicitly unchecked unsigned arithmetic and require golden tests.

Generate one season independently:

1. Resolve regional season by absolute instance modulo seasons/year.
2. Seed RNG from above.
3. `current = season.EntryWeatherType` (not Clear and not prior season's final result).
4. Iterate days 1..DaysPerSeason, then slots ascending.
5. Exact fixed slot emits its value and updates current without consuming RNG.
6. Otherwise draw from current row; first cumulative weight greater than draw wins and becomes current.

Independent seasons intentionally preserve O(one season) arbitrary lookup and query-order independence. Current weather persists across the calendar boundary until the first slot of the new season. This follows Doloc's independent periods while replacing its magic starting integer.

Cache immutable arrays by `(regionId,seasonInstanceIndex)` with a small nonpersisted LRU (three periods/region).

---

## 15. Slot resolution and live advancement

Slot key: `(absoluteDayIndex,slotIndex)`. Absolute minute is `day*1440 + slotHour*60`.

Package 3 binds `WorldWeatherState` to `HourChanged` during `CompleteLoad`. At a slot it resolves every catalog region in stable catalog order with precedence:

```text
runtime exact-slot override > authored fixed slot > generated Markov slot
```

If type differs, close current history at boundary, set new current/start through the event-firing setter, and raise `WeatherChanged`. If identical, leave open-segment start and raise nothing. Ordered time boundary replay makes sleep/debug skips resolve all crossed slots even with no room loaded.

---

## 16. Forecast contract (Package 3E implemented)

```csharp
bool TryGetForecast(string regionId, int daysAhead, out WeatherForecast forecast);
```

`daysAhead` is inclusive, 0..configured maximum. Zero means remainder of today. N means `[now,startOfDay(TotalDays+N+1))`. First entry is current actual weather clipped to now; later entries start at exact slot boundaries strictly after now. At an exact slot, synchronous `HourChanged` has already resolved current, so do not duplicate it.

Queries cross seasons/years, resolve region-specific seasons, include fixed slots/runtime overrides, and never mutate state, history, seeds, RNG stream, cache arrays, or subscriptions. `WeatherForecast` includes region, requested-at minute, exclusive end, and ordered half-open entries. Backend periods are arbitrarily calculable; public cap prevents huge allocations and future UI may display a shorter horizon.

---

## 17. Weather history (Package 3C implemented; recording in 3B)

Use half-open `[start,end)` segments. Per region store `historyAvailableFromMinute`, closed ordered nonoverlapping records, plus current type/start as the open segment. Merge adjacent identical closed segments.

Do **not** trim initially. At two slots/day, even an unmerged ten-year history is about 7,300 records/region. Add retention only after profiling and consumer requirements.

```csharp
bool TryQueryHistory(string regionId, long fromInclusive, long toExclusive,
    out WeatherHistoryQueryResult result);
```

Require `0 <= from < to <= now`. Unknown region/invalid range returns false. Result contains clipped records, `RequestedFrom`, `RequestedTo`, `AvailableFrom`, and `IsComplete = RequestedFrom >= AvailableFrom`. Intersect the open segment synthetically without mutation. If request begins before availability, return only known intersection with `IsComplete=false`; never invent Clear weather. Thus migrated/new regions truthfully expose unavailable past.

The unloaded-farm example is one query over `[09:00,17:00)`; rain segments prove occurrence/duration without a farm object having existed.

---

## 18. Overrides (Package 3D implemented)

Minimal exact-slot entry: region ID, `long absoluteDayIndex`, slot index, type, source tag.

```csharp
bool SetOverride(WeatherOverrideEntry entry); // replace same region/day/slot
bool ClearOverride(string regionId, long absoluteDayIndex, int slotIndex);
```

Validate all keys. Source tag is diagnostic metadata, not a quest system. Persist overrides separately and apply at read time; never mutate generated cache. Setting/clearing the active slot re-resolves immediately and records a current-minute history boundary only if actual type changes. Past resolved weather remains history. No ranges, recurrence, priorities, conditions, or quest framework yet.

---

## 19. Save schema and migration

**Packages 1–4 are implemented; Package 4 does not change persistence.** The current save schema
remains **v6**. Package 1 adds
`WorldTimeSaveData { bool initialized; long totalGameMinutes; }` as `SaveData.worldTime` and
updates `SaveDataMigrator.CurrentSaveVersion` from **4 to 5**. Package 2 adds
`WorldWeatherSaveData` as `SaveData.worldWeather` and updates the version from **5 to 6**.
Migration null-normalizes both nested save graphs and leaves `initialized == false` for pre-v5
time or pre-v6 weather, so the owning state assets use authored fresh values rather than
fabricating timestamps or weather history.

Package 2 implements the following final weather shape:

```csharp
[Serializable] public class WorldWeatherSaveData {
    public bool initialized;
    public List<RegionWeatherSaveEntry> regions = new();
    public List<WeatherOverrideSaveEntry> overrides = new();
}
[Serializable] public class RegionWeatherSaveEntry {
    public string regionId = "";
    public int currentWeatherType;
    public long currentWeatherStartMinute;
    public long historyAvailableFromMinute;
    public uint rootSeed;
    public List<WeatherHistoryRecordSaveEntry> history = new();
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

`SaveDataMigrator` always null-normalizes sections, nested lists, IDs, and override source tags.
Package 2 established seeds and availability/current-start fields in the persisted shape. Package 3
populates closed actual-weather history and runtime exact-slot overrides without another version
bump; generator caches and immutable query results remain nonpersisted.

Verify `JsonUtility` uint round trip in EditMode. If unsupported, store identical 32 bits in `long` and checked-cast; do not alter RNG bits.

Normalization: first duplicate saved region wins; unknown catalog IDs are discarded during completion; invalid enum uses region initial; invalid/overlapping history discards that region's closed list and resets availability/start to loaded now; invalid overrides discard; timestamps beyond loaded now clamp during completion. All cases warn.

---

## 20. Save/load/event-binding order

### Package 1–2 implemented lifecycle

The following is the implemented time lifecycle. It is independent of save-target Inspector
ordering; the current serialized composition assigns `WorldTimeState.asset` to the target list:

```text
Bootstrap.Awake
  instantiate GameManager
  instantiate SaveManager
  instantiate wired WorldClockDriver prefab
    (cannot tick: WorldTimeState.IsLoaded is false)
  instantiate existing persistent managers

Bootstrap.Start
  SaveManager.LoadOrCreate(0)
    migrate
    each target ApplySaveData in Inspector order
WorldTimeState.ApplySaveData()
        use initialized saved minutes, or CalendarConfig's authored fresh date
        validate non-negative canonical minutes
        publish StateApplied once
      WorldWeatherState.ApplySaveData()
        deep-copy worldWeather into a pending DTO
        do not read WorldTimeState or bind events

  WorldWeatherState.CompleteLoad(WorldTimeState)
    require loaded WorldTimeState
    validate WeatherSimulationConfig, catalog, tracks, seasons, and slot keys
    reconcile saved regions with authored catalog at loaded minute
    create missing regions with active deterministic slot weather, history boundary, and nonzero seed
    discard orphaned/duplicate/malformed saved entries with warnings
    preserve current runtime state on catalog-changing recompletion
    bind exactly once to WorldTimeState.HourChanged

  ResolveLoadedHealthState
  begin startup transition
```

On every `WorldClockDriver.Update`, the driver returns without advancing unless its
`WorldTimeState` exists and is loaded, `GameManager.Instance` exists, and
`GameManager.State == GameState.Playing`. It samples unscaled delta time, applies the authored
hitch clamp and debug multiplier, converts complete quanta using
`WorldTimeState.RealSecondsPerGameMinute`, retains the fractional remainder, and calls
`WorldTimeState.AdvanceMinutes` once. Pause, menus, and loading/entering/exiting states therefore
stop the clock; hit-stop intentionally continues while the game state remains `Playing`.

`WorldTimeState.AdvanceMinutes` is the sole canonical mutation path. For each crossed hour
boundary it publishes `HourChanged`, then `DayChanged`, `SeasonChanged`, `YearChanged`, and
`DayPhaseChanged` when applicable; midnight therefore reports the new hour before date/season/year
events. It publishes one `TimeAdvanced(before, after)` after the final target is installed. Zero is
a no-op, negative/backward values are rejected, checked overflow is rejected, and callbacks may
not re-enter advancement. See §9 for the boundary algorithm.

`WorldWeatherState.ApplySaveData` only copies its own DTO, then `Bootstrap` calls
`CompleteLoad(WorldTimeState)` after all save targets have applied. Completion validates dependencies,
reconciles current regional state, preserves runtime state on catalog changes, and binds exactly one
`HourChanged` subscriber. Package 3 resolves every crossed configured slot in catalog order with
runtime exact-slot override > authored fixed slot > generated weather precedence. Actual changes
use one atomic current-weather/history/event path; immutable history and forecast queries never
mutate live state. `SaveManager` remains unaware of concrete gameplay types. Package 4 adds only
passive room context/tooling; the save contract and schema remain unchanged.

---

## 21. Room context

```csharp
public enum EnvironmentExposure { Outdoor, Sheltered, Indoor }

[DisallowMultipleComponent]
public sealed class RoomClimateContext : MonoBehaviour {
    [SerializeField] private string regionId;
    [SerializeField] private EnvironmentExposure exposure;
}
```

Scene-local query seam only. `EnvironmentExposure` is `Outdoor`, `Sheltered`, or `Indoor`; the
component has private serialized `regionId`/`exposure` fields and read-only runtime properties. It
has no clock, weather, catalog, save, or lifecycle work. Future presentation may refine
windows/caves without changing simulation. Exactly one context is required per non-Boot enabled
Build Settings scene, none in Boot; IDs must be nonblank catalog members, and many scenes may share
a region.

---

## 22. Future presentation and gameplay boundaries

Presentation combines independent day/night, weather, room/biome, and exposure profiles into one resolved visual state written by one owner. Simulation never references `Light2D`, particles, `Volume`, materials, SpriteRenderers, or audio. Gameplay reads physical state/metadata, never VFX activity. Indoor scenes do not stop the clock or regional weather.

Future examples:

- Crops bulk-process elapsed minutes and query rain history; incomplete history requires a farming-owned migration/default policy.
- NPCs/shops/festivals query date, weekday, and time ranges; they never own clocks.
- Fishing/resources query regional season plus current/history weather.
- Unloaded rooms need no ticking MonoBehaviour.

---

## 23. Exact files to create

Package 1:

```text
Assets/_Project/Scripts/World/Time/WeekDay.cs
Assets/_Project/Scripts/World/Time/DayPhase.cs
Assets/_Project/Scripts/World/Time/CalendarConfig.cs
Assets/_Project/Scripts/World/Time/WorldTimeSnapshot.cs
Assets/_Project/Scripts/World/Time/WorldTimeState.cs
Assets/_Project/Scripts/World/Time/WorldClockDriver.cs
Assets/_Project/Scripts/Save/Data/WorldTimeSaveData.cs
Assets/_Project/Scripts/Editor/Tests/WorldTime/WorldTimeSnapshotTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldTime/WorldTimeStateTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldTime/WorldTimeSaveTests.cs
```

The Package 1 files above are present in the current checkout. The Package 2 files below and the
complete Package 3 runtime/test files are also present. Package 4 files are present as implemented.

Package 2 (implemented):

```text
Assets/_Project/Scripts/World/Climate/WeatherType.cs
Assets/_Project/Scripts/World/Climate/WeatherDefinition.cs
Assets/_Project/Scripts/World/Climate/WeatherSimulationConfig.cs
Assets/_Project/Scripts/World/Climate/SeasonDefinition.cs
Assets/_Project/Scripts/World/Climate/SeasonTrackDefinition.cs
Assets/_Project/Scripts/World/Climate/ClimateRegionDefinition.cs
Assets/_Project/Scripts/World/Climate/ClimateRegionCatalog.cs
Assets/_Project/Scripts/World/Climate/WeatherSnapshot.cs
Assets/_Project/Scripts/World/Climate/WorldWeatherState.cs
Assets/_Project/Scripts/Save/Data/WorldWeatherSaveData.cs
Assets/_Project/Scripts/Save/Data/RegionWeatherSaveEntry.cs
Assets/_Project/Scripts/Save/Data/WeatherHistoryRecordSaveEntry.cs
Assets/_Project/Scripts/Save/Data/WeatherOverrideSaveEntry.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/ClimateDefinitionTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WorldWeatherPersistenceTests.cs
```

Package 3 (3A–3E implemented):

```text
Assets/_Project/Scripts/World/Climate/DeterministicWeatherRng.cs
Assets/_Project/Scripts/World/Climate/WeatherGenerator.cs
Assets/_Project/Scripts/World/Climate/WeatherForecast.cs
Assets/_Project/Scripts/World/Climate/WeatherForecastEntry.cs
Assets/_Project/Scripts/World/Climate/WeatherHistoryRecord.cs
Assets/_Project/Scripts/World/Climate/WeatherHistoryQueryResult.cs
Assets/_Project/Scripts/World/Climate/WeatherOverrideEntry.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WeatherGeneratorTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WeatherHistoryTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WeatherOverrideTests.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WeatherForecastTests.cs
```

Package 3 also extends `WorldWeatherState.cs` and the existing
`WorldWeatherPersistenceTests.cs` with progression, history, override, and recompletion coverage.

Package 4 (implemented):

```text
Assets/_Project/Scripts/World/Climate/EnvironmentExposure.cs
Assets/_Project/Scripts/World/Climate/RoomClimateContext.cs
Assets/_Project/Scripts/Editor/Climate/SeasonDefinitionEditor.cs
Assets/_Project/Scripts/Editor/Validation/WorldTimeClimateValidator.cs
Assets/_Project/Scripts/Editor/Tests/WorldClimate/WorldTimeClimateValidatorTests.cs
```

Unity generates `.meta` files normally.

---

## 24. Existing files to modify

Package 1:

```text
Assets/_Project/Scripts/Save/Data/SaveData.cs
Assets/_Project/Scripts/Save/SaveDataMigrator.cs
Assets/_Project/Scripts/Managers/Bootstrap.cs
```

The code and serialized composition changes above are present. These composition files contain the
Package 1 assignments:

```text
Assets/_Project/Prefabs/Managers/_SaveManager.prefab
Assets/_Project/Scenes/Boot.unity
```

Package 2 modifies the same save/bootstrap/prefab/Boot composition files for weather. Package 3
extends `WorldWeatherState.cs` and does not bump save version; it adds no composition changes.
Package 4 adds one root context to each of the
currently enabled gameplay scenes `SampleScene.unity`, `SampleScene2.unity`, `SampleScene3.unity`,
and `SampleScene4.unity`, each provisionally `region_underbrew` / `Outdoor`; `Boot.unity` remains
excluded. `UISandbox.unity` exists but is disabled in Build Settings and is excluded from the
required scene pass.

---

## 25. Assets, prefabs, and Editor work

Package 1 target assets (created and serialized in the repository):

```text
Assets/_Project/ScriptableObjects/World/CalendarConfig.asset
Assets/_Project/ScriptableObjects/World/WorldTimeState.asset
Assets/_Project/Prefabs/Managers/_WorldClockDriver.prefab
```

The current checkout contains these assets and their serialized references. The assignments are:

- `CalendarConfig.asset` is assigned to `WorldTimeState.calendarConfig`.
- `WorldTimeState.asset` is assigned to `WorldClockDriver.worldTimeState` (the driver has no
  direct `CalendarConfig` field; it reads `WorldTimeState.RealSecondsPerGameMinute`).
- `WorldTimeState.asset` is included in `_SaveManager.prefab`'s `saveTargets` list.
- `_WorldClockDriver.prefab` is assigned to `Bootstrap.worldClockDriverPrefab` in
  `Assets/_Project/Scenes/Boot.unity`.

Save-target order is not semantically important for Package 1, but the state assignment is
required so `ApplySaveData` runs before the driver can advance. Package 1 is covered by the
completed baseline/full-suite validation pass. Package 2 serialized climate assets/composition
were generated and validated through Unity APIs in an isolated copy; the primary Editor was not
manually validated and may require refresh/reimport, and Play Mode is not claimed.

Package 2 assets are present and serialized:

```text
Assets/_Project/ScriptableObjects/World/WeatherSimulationConfig.asset
Assets/_Project/ScriptableObjects/World/WorldWeatherState.asset
Assets/_Project/ScriptableObjects/World/ClimateRegionCatalog.asset
Assets/_Project/ScriptableObjects/World/WeatherDefinitions/Clear.asset
Assets/_Project/ScriptableObjects/World/WeatherDefinitions/Cloudy.asset
Assets/_Project/ScriptableObjects/World/WeatherDefinitions/Rain.asset
Assets/_Project/ScriptableObjects/World/WeatherDefinitions/Storm.asset
Assets/_Project/ScriptableObjects/World/WeatherDefinitions/Fog.asset
Assets/_Project/ScriptableObjects/World/Seasons/Spring.asset
Assets/_Project/ScriptableObjects/World/Seasons/Summer.asset
Assets/_Project/ScriptableObjects/World/Seasons/Autumn.asset
Assets/_Project/ScriptableObjects/World/Seasons/Winter.asset
Assets/_Project/ScriptableObjects/World/Seasons/UnderbrewSeasonTrack.asset
Assets/_Project/ScriptableObjects/World/Seasons/Underbrew.asset
```

`WeatherSimulationConfig.asset` contains exactly one definition for each `WeatherType` and uses
the authored two-slot policy at 06:00/18:00. `WorldWeatherState.asset` references the simulation
config and catalog. `ClimateRegionCatalog.asset` currently contains the single provisional
`region_underbrew` region, mapped to the four-season `UnderbrewSeasonTrack.asset` with authored
initial weather `Clear`. The state asset is included in `_SaveManager.prefab` and assigned to
`Bootstrap.worldWeatherState` in `Boot.unity`.

Package 3 requires no new assets, prefabs, scenes, Inspector assignments, or manual Editor setup.

Package 4 is implemented without save/schema, prefab, ProjectSettings, Packages, or presentation
changes. `Tools/Project/Validate World Time & Climate` first validates the catalog/season graph
against `WorldTimeState`, then inspects enabled Build Settings scenes: `Boot` must contain zero
contexts; every other enabled scene must contain exactly one; every context must have a nonblank
catalog-member region ID; shared region IDs are allowed. Disabled scenes are skipped. The safe
`SeasonDefinitionEditor` labels rows FROM and columns TO, shows row totals, offers `Set Uniform`
and `Clear Row`, preserves existing cells on enum append, confirms destructive shrink/reset, and
has no “Normalize to 100”.

---

## 26. Automated tests

Package 1:

**Coverage added and included in the completed Package 1 baseline/full-suite validation pass.**

- Epoch/worked initial conversion; all exact formulas and phase wrap.
- Minute/hour/day/weekday/season/year rollover.
- Large skip final state and ordered boundary stream equals smaller calls.
- Dawn + 24 hours emits Day, Dusk, Night, Dawn.
- Coincident midnight/season/year event order.
- Zero/negative/overflow/reentrant advance.
- Fresh/missing/uninitialized DTO, v4->v5 migration, gather/apply, StateApplied.
- Accumulator/gating logic testable without flaky wall clock; lifecycle remains PlayMode/manual.

Package 2 landing validation (historical isolated Unity 6.0.3.10f1 EditMode result):

- Catalog/track/definition lookup and all validation failures.
- Every matrix row/dimension/weight/overflow and duplicate fixed slots.
- Two independent regions/seeds; added/removed/invalid saved regions.
- Apply does not read clock; completion requires loaded time and is idempotent.
- Save-target order permutations complete identically.
- v6 null normalization and JSON/root-seed round trip.
- The Package 2 contract intentionally had no time-driven weather; this was superseded by Package 3B.

The Package 2 landing fixtures reported 28/28 climate and 33/33 WorldTime tests; those historical
counts are retained only to identify that landing. Current Package 3 validation is recorded below.

Package 3A–3E (Unity 6000.3.10f1 EditMode validation complete):

- Golden SplitMix64/XorShift32 outputs and bound edge cases.
- Exact weighted sequences; fixed precedence and no RNG consumption on fixed slots.
- Same inputs independent of query order; differing seeds/periods differ.
- Configured day/slot count, cross-season query, authored entry state.
- Every crossed slot in catalog order, same-result no-op, history close/merge/open query behavior,
  incomplete prefixes, exact-slot override add/replace/clear, and catalog-recompletion preservation.
- Forecast remainder-of-today, inclusive day cap, exact-boundary de-duplication, season/year
  boundaries, override/fixed/generated precedence, save/reload determinism, and strict query
  non-mutation.

Focused fixtures pass: `WeatherGeneratorTests` 10/10, `WorldWeatherPersistenceTests` 29/29,
`WeatherHistoryTests` 11/11, `WeatherOverrideTests` 12/12, and `WeatherForecastTests` 13/13.
Existing WorldClimate passes 84/84; WorldTime passes 38/38; the explicit existing climate/time
union passes 122/122. The Package 4 validator passes 9/9. Full EditMode is 727/727; the
historical camera failure did not reproduce and its fixture passed 23/23.

Package 4 validator tests cover missing/duplicate contexts, blank/unknown region IDs, Boot
exclusion, valid shared regions, read-only serialized context properties, catalog lookup, and the
enabled-scene contract. They pass 9/9; the validator menu pass checks five enabled scenes, four
contexts, and zero issues.

No PlayMode was applicable. No manual visual or interactive matrix-Inspector/Undo validation was
claimed; the menu validator is a non-mutating automated/editor pass.

---

## 27. Manual validation

- Fresh date starts correctly; exact minute survives reload.
- Clock advances only in Playing, stops in both menus/transitions, and continues through a deliberately lengthened debug hit-stop.
- Hitch clamp prevents an alt-tab/breakpoint jump.
No PlayMode was applicable. No manual visual or interactive matrix-Inspector/Undo validation was
claimed. Automated fixtures cover forward sleep/debug slot replay, independent region seeds,
reload preservation, history/override payloads, forecast query-order determinism/nonmutation, and
the Package 4 validator/context contract.

---

## 28. Package order and definition of done

### Package 1 — Calendar and World Time

**Implemented and validated.**
The canonical calendar state, exact snapshot conversion, hour-boundary event stream, real-time
driver gating, `WorldTimeSaveData`, v4→v5 migration, and Bootstrap seam are in the repository.
The target assets/prefab and their `_SaveManager.prefab`/`Boot.unity` assignments are present as
listed in §25.

### Package 2 — Definitions and Regional State

**Implemented; landing validation recorded historically.** The
authored weather definitions, regional catalog/track graph, `WorldWeatherState` current-weather
and root-seed queries, v6 DTOs/migration, post-load reconciliation, and SaveManager/Bootstrap
composition are in the repository. The historical isolated Unity 6.0.3.10f1 EditMode landing
result was 28/28 climate and 33/33 WorldTime tests. Current Package 3 results are recorded below.
`ApplySaveData` is clock-free; `CompleteLoad(WorldTimeState)` runs after all save targets apply,
is idempotent, and reconciles missing/orphaned/malformed state. Package 3 adds temporal behavior
without changing save version or ownership. Package 2's no-progression behavior is retained as
historical contract evidence only.

### Package 3 — Deterministic Weather, Forecast, History, Overrides

**Implemented and validated.** The specified RNG/generator, lifecycle-safe slot replay, actual
history recording/querying, exact-slot overrides, and immutable forecasts are present. Focused
fixtures pass 10/10, 29/29, 11/11, 12/12, and 13/13; existing WorldClimate/WorldTime pass
84/84 and 38/38, with the explicit union at 122/122. At Package 4 closure, Full EditMode is
727/727; the historical camera failure did not reproduce and its fixture passed 23/23. No
version bump or save/prefab/ProjectSettings/Packages/presentation change was made.

### Package 4 — Room Context and Authoring Tools

**Implemented and validated.** Runtime `EnvironmentExposure` and passive `RoomClimateContext`,
safe matrix authoring, the `Tools/Project/Validate World Time & Climate` menu, and four provisional
scene contexts are present. Validation requires zero contexts in Boot, one in each other enabled
scene, and a nonblank catalog-member region ID; shared IDs are valid. Presentation, particles,
lighting, post-processing, audio, farming/NPC/UI consumers, final art/polish, and later stages
remain deferred.

Each package compiles standalone, keeps existing tests green, and includes required Inspector/prefab/scene wiring.

---

## 29. Risks, constraints, and deferred work

- Calendar shape, weather ordinals, slot layout, matrices, and RNG are persistence/content contracts.
- Unbounded history is accepted initially. Profile before retention; any later retention preserves `IsComplete`.
- Config changes intentionally alter future forecast for the same seed. Shipped forecast assets need content-version discipline; no generic framework now.
- Seed creation is nondeterministic once, then persisted. New/migrated regions begin known history at migration time.
- No rewind API. A real rewind needs coordinated history/override clipping.
- Driver's private duplicate guard is not a public service singleton.
- Existing lack of forward-version save rejection remains out of scope.

---

## 30. Documentation updates when implementation lands

- For the Package 1 landing, update `Docs/Architecture.md` with ownership and the
  simulation/presentation boundary, `Docs/ImplementationPlan.md` with the completed package
  status, and `Docs/FeatureSpecs/SaveSystem.md` with v5 and the time save target.
- For the Package 2 landing, keep `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, and
  `Docs/FeatureSpecs/SaveSystem.md` synchronized with the v6 weather state/DTO and post-load
  completion seam. This plan and `Docs/FeatureSpecs/WorldTimeClimateWeather.md` are the detailed
  climate contract.
- Optionally cross-link historical `Docs/ImplementationPlans/WorldPersistence.md`.

Keep Packages 2–4 marked implemented for definitions/regional state, deterministic generation and
temporal weather behavior, and room context/tools respectively. Keep presentation and downstream
consumers marked deferred until their production code/assets land.

---

## Luna Implementation Handoff

### Package 1 implementation record

Package 1 is implemented, composed, and validated by the completed baseline/full-suite pass.

### Package 2 implementation record

Package 2 is implemented, composed, and isolated-Unity validated. Its final focused climate and
WorldTime fixture results are 28/28 and 33/33 respectively at that historical landing; current
WorldTime fixtures are 38/38 and the Package 4 closure full EditMode result is 727/727. The
historical camera failure did not reproduce and its fixture passed 23/23. Runtime/editor dotnet
assemblies compile with 0 errors and 0 warnings. Package 2 is
limited to definitions, regional persistent state, save v6, and post-load reconciliation; it does
not include automatic progression or temporal weather behavior. Play Mode and manual primary-
Editor validation remain unclaimed; refresh/reimport may be required in the open primary Editor.

### Files to create

Every Package-1 through Package-4 runtime/editor/test path in §23 is present.

### Files to modify

The code and composition paths in §24 are updated, plus the documentation files updated for this
landing:

```text
Docs/Architecture.md
Docs/ImplementationPlan.md
Docs/FeatureSpecs/SaveSystem.md
Docs/FeatureSpecs/WorldTimeClimateWeather.md
```

The verified Boot scene path is `Assets/_Project/Scenes/Boot.unity`.

### Assets/prefabs

Package 1 assets (`CalendarConfig.asset`, `WorldTimeState.asset`, and `_WorldClockDriver.prefab`)
and Package 2 assets (`WeatherSimulationConfig.asset`, `WorldWeatherState.asset`,
`ClimateRegionCatalog.asset`, five weather definitions, and the four-season Underbrew climate
graph) are present at the §25 paths, with config/state/catalog/driver/SaveManager/Bootstrap
references serialized as specified. Treat initial climate names, matrices, and region membership
as provisional content until product-approved.

### Tests

Package-1 tests in §26 are present and included in the completed baseline/full-suite validation
pass. Package-2 definition/persistence tests in §26 passed in the historical isolated Unity
6.0.3.10f1 EditMode run (28/28); the historical WorldTime result was 33/33. Current Package-3
fixtures pass generator 10/10, progression/persistence 29/29, history 11/11, overrides 12/12,
forecast 13/13, and current WorldTime 38/38. Existing WorldClimate/WorldTime pass 84/84 and
38/38, with the explicit union at 122/122. Package 4 validator tests pass 9/9. Full EditMode is
727/727; the historical camera failure did not reproduce and its fixture passed 23/23. No
PlayMode was applicable, and no manual visual or interactive matrix-Inspector/Undo validation was
claimed.

### Explicit exclusions

Package 2 climate definitions/assets and regional save state plus complete Package 3A–3E
deterministic/temporal weather behavior and Package 4 room context/tools are implemented. All
weather presentation remains excluded. Do not modify
`WorldStateRegistry`, `GameManager`,
hero/tuning, camera, audio, scene-transition code, UI flow, or presentation. Do not add
backward/absolute time setters or a minute loop.

### Stop conditions

If repository state materially contradicts this plan—especially save version, Bootstrap lifecycle, prefab paths, or existing time code—stop that portion, report exact evidence, and do not invent a substitute. Preserve unrelated dirty changes.

### Validation claims

Report the isolated Unity 6.0.3.10f1 fixture/full-suite and dotnet results separately from primary
Editor and Play Mode work. Do not claim manual primary-Editor or Play Mode success unless actually
performed; the open primary Editor may require refresh/reimport. Report changed files and
remaining manual Unity work.
