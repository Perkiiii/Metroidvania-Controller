using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent regional weather facts. WorldTimeState remains the canonical timestamp owner; this
// state subscribes only after post-load reconciliation so every crossed configured slot can resolve
// actual weather and close compact history segments.
[CreateAssetMenu(menuName = "Project/World/World Weather State", fileName = "WorldWeatherState")]
public sealed class WorldWeatherState : ScriptableObject, ISaveTarget
{
    [Header("Climate Dependencies")]
    [SerializeField] private WeatherSimulationConfig weatherSimulationConfig;
    [SerializeField] private ClimateRegionCatalog climateRegionCatalog;

    private readonly Dictionary<string, RuntimeRegionState> activeRegions =
        new Dictionary<string, RuntimeRegionState>(StringComparer.Ordinal);
    private readonly List<string> activeRegionOrder = new List<string>();
    private readonly List<WeatherOverrideSaveEntry> activeOverrides =
        new List<WeatherOverrideSaveEntry>();

    [NonSerialized] private WorldWeatherSaveData pendingSave;
    [NonSerialized] private WorldTimeState loadedWorldTime;
    [NonSerialized] private WorldTimeState subscribedWorldTime;
    [NonSerialized] private WeatherGenerator weatherGenerator;
    [NonSerialized] private bool hasAppliedSave;
    [NonSerialized] private bool isComplete;

    public event Action<WeatherSnapshot> WeatherChanged;

    public WeatherSimulationConfig WeatherSimulationConfig => weatherSimulationConfig;
    public ClimateRegionCatalog ClimateRegionCatalog => climateRegionCatalog;
    public bool IsLoaded => isComplete;
    public int ActiveRegionCount => activeRegionOrder.Count;

    private void OnEnable()
    {
        DetachFromWorldTime();
        activeRegions.Clear();
        activeRegionOrder.Clear();
        activeOverrides.Clear();
        pendingSave = CreateEmptySave();
        loadedWorldTime = null;
        weatherGenerator = null;
        hasAppliedSave = false;
        isComplete = false;
    }

    private void OnDisable()
    {
        DetachFromWorldTime();
    }

    // -------------------------------------------------------------------------
    // ISaveTarget
    // -------------------------------------------------------------------------

    public void GatherSaveData(SaveData data)
    {
        if (data == null)
            return;

        data.worldWeather ??= new WorldWeatherSaveData();
        WorldWeatherSaveData destination = data.worldWeather;
        destination.initialized = isComplete;
        destination.regions = new List<RegionWeatherSaveEntry>();
        destination.overrides = new List<WeatherOverrideSaveEntry>();

        if (!isComplete)
            return;

        CopyActiveStateTo(destination);
    }

    private void CopyActiveStateTo(WorldWeatherSaveData destination)
    {
        destination.initialized = true;
        destination.regions = new List<RegionWeatherSaveEntry>();
        destination.overrides = new List<WeatherOverrideSaveEntry>();

        for (int i = 0; i < activeRegionOrder.Count; i++)
        {
            RuntimeRegionState source = activeRegions[activeRegionOrder[i]];
            RegionWeatherSaveEntry region = new RegionWeatherSaveEntry
            {
                regionId = source.RegionId,
                currentWeatherType = (int)source.CurrentWeatherType,
                currentWeatherStartMinute = source.CurrentWeatherStartMinute,
                historyAvailableFromMinute = source.HistoryAvailableFromMinute,
                rootSeed = source.RootSeed,
                history = new List<WeatherHistoryRecordSaveEntry>(source.History.Count)
            };

            for (int historyIndex = 0; historyIndex < source.History.Count; historyIndex++)
            {
                WeatherHistoryRecordSaveEntry record = source.History[historyIndex];
                region.history.Add(record == null
                    ? null
                    : new WeatherHistoryRecordSaveEntry
                    {
                        weatherType = record.weatherType,
                        startMinute = record.startMinute,
                        endMinuteExclusive = record.endMinuteExclusive
                    });
            }

            destination.regions.Add(region);
        }

        for (int i = 0; i < activeOverrides.Count; i++)
        {
            WeatherOverrideSaveEntry source = activeOverrides[i];
            destination.overrides.Add(source == null
                ? null
                : new WeatherOverrideSaveEntry
                {
                    regionId = source.regionId,
                    absoluteDayIndex = source.absoluteDayIndex,
                    slotIndex = source.slotIndex,
                    weatherType = source.weatherType,
                    sourceTag = source.sourceTag
                });
        }
    }

    // Apply is intentionally clock-free. SaveManager's target order is not a contract, so this
    // method only copies the serialized section into a private pending DTO and invalidates the
    // previously reconciled runtime view. CompleteLoad performs the later clock-dependent pass.
    public void ApplySaveData(SaveData data)
    {
        DetachFromWorldTime();
        pendingSave = DeepCopy(data?.worldWeather);
        hasAppliedSave = true;
        isComplete = false;
        loadedWorldTime = null;
        weatherGenerator = null;
        activeRegions.Clear();
        activeRegionOrder.Clear();
        activeOverrides.Clear();
    }

    // -------------------------------------------------------------------------
    // Post-load completion and queries
    // -------------------------------------------------------------------------

    public void CompleteLoad(WorldTimeState worldTimeState)
    {
        if (worldTimeState == null)
            throw new ArgumentNullException(nameof(worldTimeState));
        if (!worldTimeState.IsLoaded)
            throw new InvalidOperationException("WorldWeatherState.CompleteLoad requires a loaded WorldTimeState.");
        if (isComplete && CatalogMatchesActiveOrder())
        {
            ValidateDependencies(worldTimeState);
            weatherGenerator = new WeatherGenerator(
                worldTimeState.DaysPerSeason,
                weatherSimulationConfig.SlotCount);
            // A caller may repeat completion with an equivalent loaded clock after a bootstrap
            // retry. Rebinding this non-serialized query dependency does not mutate weather.
            loadedWorldTime = worldTimeState;
            BindToWorldTime(worldTimeState);
            return;
        }

        ValidateDependencies(worldTimeState);
        DetachFromWorldTime();
        weatherGenerator = new WeatherGenerator(
            worldTimeState.DaysPerSeason,
            weatherSimulationConfig.SlotCount);

        WorldWeatherSaveData source = hasAppliedSave && pendingSave != null
            ? pendingSave
            : CreateEmptySave();
        long loadedMinute = worldTimeState.TotalGameMinutes;

        if (isComplete)
        {
            // A catalog change causes reconciliation to rebuild all runtime records. Capture the
            // post-progression state first; ApplySaveData keeps isComplete false so a newly loaded
            // DTO remains authoritative on the initial completion pass.
            pendingSave = new WorldWeatherSaveData();
            CopyActiveStateTo(pendingSave);
            source = pendingSave;
        }

        Dictionary<string, RegionWeatherSaveEntry> savedById = CollectSavedRegions(source);
        activeRegions.Clear();
        activeRegionOrder.Clear();
        activeOverrides.Clear();
        if (source.initialized)
            NormalizeOverrides(source.overrides);

        WorldTimeSnapshot loadedSnapshot = worldTimeState.Current;
        HashSet<uint> reservedSeeds = CollectReservedSeeds(savedById, source.initialized);

        IReadOnlyList<ClimateRegionDefinition> definitions = climateRegionCatalog.Regions;
        for (int i = 0; i < definitions.Count; i++)
        {
            ClimateRegionDefinition definition = definitions[i];
            if (definition == null)
                continue; // Validation above should make this unreachable.

            string regionId = definition.RegionId;
            savedById.TryGetValue(regionId, out RegionWeatherSaveEntry saved);
            RuntimeRegionState state = BuildRegionState(
                definition,
                saved,
                source.initialized,
                reservedSeeds,
                loadedMinute,
                loadedSnapshot,
                worldTimeState.DaysPerSeason);
            activeRegions.Add(regionId, state);
            activeRegionOrder.Add(regionId);
        }

        loadedWorldTime = worldTimeState;
        isComplete = true;

        // Commit the reconciled set as the new pending baseline. This drops orphaned save entries
        // permanently, so a region removed from the catalog and later re-added receives fresh
        // state instead of resurrecting data that was intentionally discarded.
        pendingSave = new WorldWeatherSaveData();
        CopyActiveStateTo(pendingSave);
        hasAppliedSave = true;
        BindToWorldTime(worldTimeState);
    }

    private bool CatalogMatchesActiveOrder()
    {
        if (climateRegionCatalog == null || activeRegionOrder.Count != climateRegionCatalog.Regions.Count)
            return false;

        for (int i = 0; i < activeRegionOrder.Count; i++)
        {
            ClimateRegionDefinition definition = climateRegionCatalog.Regions[i];
            if (definition == null
                || !string.Equals(activeRegionOrder[i], definition.RegionId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryGetWeather(string regionId, out WeatherSnapshot snapshot)
    {
        if (!isComplete || loadedWorldTime == null || string.IsNullOrEmpty(regionId)
            || !activeRegions.TryGetValue(regionId, out RuntimeRegionState state))
        {
            snapshot = default;
            return false;
        }

        long now = loadedWorldTime.TotalGameMinutes;
        long duration = now >= state.CurrentWeatherStartMinute
            ? now - state.CurrentWeatherStartMinute
            : 0L;
        snapshot = new WeatherSnapshot(
            state.RegionId,
            state.CurrentWeatherType,
            state.CurrentWeatherStartMinute,
            duration);
        return true;
    }

    public bool TryGetRootSeed(string regionId, out uint rootSeed)
    {
        if (isComplete && !string.IsNullOrEmpty(regionId)
            && activeRegions.TryGetValue(regionId, out RuntimeRegionState state))
        {
            rootSeed = state.RootSeed;
            return true;
        }

        rootSeed = 0u;
        return false;
    }

    /// <summary>
    /// Resolves a read-only regional forecast without advancing or otherwise mutating the live
    /// weather state. The current value is clipped to the query minute, while every later slot is
    /// emitted as its own entry even when its value repeats the preceding slot.
    /// </summary>
    public bool TryGetForecast(string regionId, int daysAhead, out WeatherForecast forecast)
    {
        forecast = default;
        if (!isComplete
            || loadedWorldTime == null
            || weatherGenerator == null
            || weatherSimulationConfig == null
            || string.IsNullOrEmpty(regionId)
            || !activeRegions.TryGetValue(regionId, out RuntimeRegionState state)
            || !climateRegionCatalog.TryGetRegion(regionId, out ClimateRegionDefinition definition))
        {
            return false;
        }

        if (daysAhead < 0 || daysAhead > weatherSimulationConfig.MaxForecastDaysAhead)
            return false;

        long requestedAtMinute = loadedWorldTime.TotalGameMinutes;
        long totalDays = requestedAtMinute / CalendarConfig.MinutesPerDay;
        long endDayExclusive;
        long endMinuteExclusive;
        try
        {
            checked
            {
                endDayExclusive = totalDays + (long)daysAhead + 1L;
                endMinuteExclusive = endDayExclusive * CalendarConfig.MinutesPerDay;
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        if (endMinuteExclusive <= requestedAtMinute)
            return false;

        IReadOnlyList<int> slotHours = weatherSimulationConfig.WeatherSlotHours;
        List<WeatherForecastEntry> entries = new List<WeatherForecastEntry>();
        WeatherType currentWeather = state.CurrentWeatherType;
        long cursor = requestedAtMinute;

        // The end bound has already been checked above, so each day and slot boundary in this
        // range is representable. A boundary exactly at the query minute is intentionally skipped:
        // the synchronous live HourChanged callback has already resolved that slot.
        for (long day = totalDays; day < endDayExclusive; day++)
        {
            long dayStart = checked(day * (long)CalendarConfig.MinutesPerDay);
            for (int slotIndex = 0; slotIndex < slotHours.Count; slotIndex++)
            {
                long boundaryMinute = dayStart + (long)slotHours[slotIndex] * CalendarConfig.MinutesPerHour;
                if (boundaryMinute <= requestedAtMinute)
                    continue;
                if (boundaryMinute >= endMinuteExclusive)
                    break;

                entries.Add(new WeatherForecastEntry(
                    currentWeather,
                    cursor,
                    boundaryMinute));
                currentWeather = ResolveWeatherAtSlot(
                    definition,
                    state.RootSeed,
                    day,
                    day / loadedWorldTime.DaysPerSeason,
                    (int)(day % loadedWorldTime.DaysPerSeason) + 1,
                    slotIndex);
                cursor = boundaryMinute;
            }
        }

        if (cursor < endMinuteExclusive)
        {
            entries.Add(new WeatherForecastEntry(
                currentWeather,
                cursor,
                endMinuteExclusive));
        }

        forecast = new WeatherForecast(
            regionId,
            requestedAtMinute,
            endMinuteExclusive,
            entries);
        return true;
    }

    /// <summary>
    /// Adds or replaces one exact-slot override. The command only changes the resolution payload;
    /// future and past keys therefore cannot rewrite the current weather or recorded history.
    /// </summary>
    public bool SetOverride(WeatherOverrideEntry entry)
    {
        if (!CanAcceptOverrideKey(entry.RegionId, entry.AbsoluteDayIndex, entry.SlotIndex)
            || !entry.IsValid
            || !WeatherTypeUtility.IsDefined(entry.WeatherType)
            || !climateRegionCatalog.TryGetRegion(entry.RegionId, out ClimateRegionDefinition definition))
        {
            return false;
        }

        WeatherOverrideSaveEntry replacement = new WeatherOverrideSaveEntry
        {
            regionId = entry.RegionId,
            absoluteDayIndex = entry.AbsoluteDayIndex,
            slotIndex = entry.SlotIndex,
            weatherType = (int)entry.WeatherType,
            sourceTag = entry.SourceTag
        };
        OverrideKey key = new OverrideKey(
            entry.RegionId,
            entry.AbsoluteDayIndex,
            entry.SlotIndex);

        for (int i = 0; i < activeOverrides.Count; i++)
        {
            WeatherOverrideSaveEntry existing = activeOverrides[i];
            if (existing != null
                && new OverrideKey(existing.regionId, existing.absoluteDayIndex, existing.slotIndex).Equals(key))
            {
                // Keep the existing ordinal so save output remains deterministic for callers that
                // replace a command after another key has already been appended.
                activeOverrides[i] = replacement;
                ReResolveActiveSlotIfNeeded(definition, key);
                return true;
            }
        }

        activeOverrides.Add(replacement);
        ReResolveActiveSlotIfNeeded(definition, key);
        return true;
    }

    /// <summary>
    /// Removes one exact-slot override. A missing key is not an error and does not affect weather.
    /// </summary>
    public bool ClearOverride(string regionId, long absoluteDayIndex, int slotIndex)
    {
        if (!CanAcceptOverrideKey(regionId, absoluteDayIndex, slotIndex)
            || !climateRegionCatalog.TryGetRegion(regionId, out ClimateRegionDefinition definition))
        {
            return false;
        }

        OverrideKey key = new OverrideKey(regionId, absoluteDayIndex, slotIndex);
        for (int i = 0; i < activeOverrides.Count; i++)
        {
            WeatherOverrideSaveEntry existing = activeOverrides[i];
            if (existing == null
                || !new OverrideKey(existing.regionId, existing.absoluteDayIndex, existing.slotIndex).Equals(key))
            {
                continue;
            }

            activeOverrides.RemoveAt(i);
            ReResolveActiveSlotIfNeeded(definition, key);
            return true;
        }

        return false;
    }

    private bool CanAcceptOverrideKey(string regionId, long absoluteDayIndex, int slotIndex)
    {
        return isComplete
            && loadedWorldTime != null
            && weatherSimulationConfig != null
            && !string.IsNullOrWhiteSpace(regionId)
            && absoluteDayIndex >= 0L
            && IsValidSlotIndex(slotIndex)
            && climateRegionCatalog != null
            && activeRegions.ContainsKey(regionId);
    }

    private void ReResolveActiveSlotIfNeeded(
        ClimateRegionDefinition definition,
        OverrideKey changedKey)
    {
        if (definition == null
            || loadedWorldTime == null
            || !activeRegions.TryGetValue(definition.RegionId, out RuntimeRegionState state))
        {
            return;
        }

        WorldTimeSnapshot snapshot = loadedWorldTime.Current;
        if (!TryGetActiveSlot(
            snapshot,
            loadedWorldTime.DaysPerSeason,
            out long absoluteDayIndex,
            out long seasonInstanceIndex,
            out int dayInSeason,
            out int slotIndex)
            || absoluteDayIndex != changedKey.AbsoluteDayIndex
            || slotIndex != changedKey.SlotIndex)
        {
            return;
        }

        WeatherType nextWeather = ResolveWeatherAtSlot(
            definition,
            state.RootSeed,
            absoluteDayIndex,
            seasonInstanceIndex,
            dayInSeason,
            slotIndex);
        // Keep active command changes on the same mutation path as clock-driven progression so
        // events and history boundaries remain identical at the current world minute.
        ApplyWeatherChange(state, nextWeather, snapshot.TotalGameMinutes);
    }

    public bool TryQueryHistory(
        string regionId,
        long fromInclusive,
        long toExclusive,
        out WeatherHistoryQueryResult result)
    {
        result = default;
        if (!isComplete
            || loadedWorldTime == null
            || string.IsNullOrEmpty(regionId)
            || !activeRegions.TryGetValue(regionId, out RuntimeRegionState state))
        {
            return false;
        }

        long now = loadedWorldTime.TotalGameMinutes;
        if (fromInclusive < 0L || fromInclusive >= toExclusive || toExclusive > now)
            return false;

        List<WeatherHistoryRecord> records = new List<WeatherHistoryRecord>();
        for (int i = 0; i < state.History.Count; i++)
        {
            WeatherHistoryRecordSaveEntry source = state.History[i];
            if (source == null || !WeatherTypeUtility.TryFromInt(source.weatherType, out WeatherType weatherType))
                continue;

            long start = Math.Max(fromInclusive, state.HistoryAvailableFromMinute);
            start = Math.Max(start, source.startMinute);
            long end = Math.Min(toExclusive, now);
            end = Math.Min(end, source.endMinuteExclusive);
            if (end <= start)
                continue;

            AddHistoryRecord(records, weatherType, start, end);
        }

        long openStart = Math.Max(fromInclusive, state.HistoryAvailableFromMinute);
        openStart = Math.Max(openStart, state.CurrentWeatherStartMinute);
        long openEnd = Math.Min(toExclusive, now);
        if (openEnd > openStart && WeatherTypeUtility.IsDefined(state.CurrentWeatherType))
            AddHistoryRecord(records, state.CurrentWeatherType, openStart, openEnd);

        result = new WeatherHistoryQueryResult(
            records,
            fromInclusive,
            toExclusive,
            state.HistoryAvailableFromMinute);
        return true;
    }

    private static void AddHistoryRecord(
        List<WeatherHistoryRecord> records,
        WeatherType weatherType,
        long startMinute,
        long endMinuteExclusive)
    {
        if (records.Count > 0)
        {
            WeatherHistoryRecord previous = records[records.Count - 1];
            if (previous.WeatherType == weatherType && previous.EndMinuteExclusive == startMinute)
            {
                records[records.Count - 1] = new WeatherHistoryRecord(
                    weatherType,
                    previous.StartMinute,
                    endMinuteExclusive);
                return;
            }
        }

        records.Add(new WeatherHistoryRecord(weatherType, startMinute, endMinuteExclusive));
    }

    private void BindToWorldTime(WorldTimeState worldTimeState)
    {
        if (ReferenceEquals(subscribedWorldTime, worldTimeState))
            return;

        DetachFromWorldTime();
        subscribedWorldTime = worldTimeState;
        subscribedWorldTime.HourChanged += HandleHourChanged;
    }

    private void DetachFromWorldTime()
    {
        if (subscribedWorldTime != null)
            subscribedWorldTime.HourChanged -= HandleHourChanged;

        subscribedWorldTime = null;
    }

    private void HandleHourChanged(WorldTimeSnapshot boundary)
    {
        if (!isComplete || weatherGenerator == null || climateRegionCatalog == null
            || subscribedWorldTime == null)
        {
            return;
        }

        int slotIndex = FindSlotIndex(boundary.Hour);
        if (slotIndex < 0 || boundary.Minute != 0)
            return;

        // activeRegionOrder is copied from the authored catalog and is the stable resolution order
        // used for both weather mutations and WeatherChanged delivery.
        for (int regionIndex = 0; regionIndex < activeRegionOrder.Count; regionIndex++)
        {
            string regionId = activeRegionOrder[regionIndex];
            if (!activeRegions.TryGetValue(regionId, out RuntimeRegionState state)
                || !climateRegionCatalog.TryGetRegion(regionId, out ClimateRegionDefinition definition))
            {
                continue;
            }

            WeatherType nextWeather = ResolveWeatherAtSlot(
                definition,
                state.RootSeed,
                boundary.TotalDays,
                boundary.SeasonInstanceIndex,
                boundary.DayInSeason,
                slotIndex);
            ApplyWeatherChange(state, nextWeather, boundary.TotalGameMinutes);
        }
    }

    private int FindSlotIndex(int hour)
    {
        IReadOnlyList<int> slotHours = weatherSimulationConfig.WeatherSlotHours;
        for (int i = 0; i < slotHours.Count; i++)
        {
            if (slotHours[i] == hour)
                return i;
        }

        return -1;
    }

    private WeatherType ResolveWeatherAtSlot(
        ClimateRegionDefinition definition,
        uint rootSeed,
        long absoluteDayIndex,
        long seasonInstanceIndex,
        int dayInSeason,
        int slotIndex)
    {
        for (int i = 0; i < activeOverrides.Count; i++)
        {
            WeatherOverrideSaveEntry weatherOverride = activeOverrides[i];
            if (weatherOverride != null
                && string.Equals(weatherOverride.regionId, definition.RegionId, StringComparison.Ordinal)
                && weatherOverride.absoluteDayIndex == absoluteDayIndex
                && weatherOverride.slotIndex == slotIndex)
            {
                return (WeatherType)weatherOverride.weatherType;
            }
        }

        return weatherGenerator.ResolveSlot(
            definition,
            rootSeed,
            seasonInstanceIndex,
            dayInSeason,
            slotIndex);
    }

    private bool TryGetActiveSlot(
        WorldTimeSnapshot snapshot,
        int daysPerSeason,
        out long absoluteDayIndex,
        out long seasonInstanceIndex,
        out int dayInSeason,
        out int slotIndex)
    {
        IReadOnlyList<int> slotHours = weatherSimulationConfig.WeatherSlotHours;
        for (int i = slotHours.Count - 1; i >= 0; i--)
        {
            if (slotHours[i] <= snapshot.Hour)
            {
                absoluteDayIndex = snapshot.TotalDays;
                seasonInstanceIndex = snapshot.SeasonInstanceIndex;
                dayInSeason = snapshot.DayInSeason;
                slotIndex = i;
                return true;
            }
        }

        // Before the first slot of day zero there is no deterministic weather value yet. On later
        // days, the final slot of the prior day is the active value (including across seasons).
        if (snapshot.TotalDays <= 0L)
        {
            absoluteDayIndex = 0L;
            seasonInstanceIndex = 0L;
            dayInSeason = 1;
            slotIndex = -1;
            return false;
        }

        absoluteDayIndex = snapshot.TotalDays - 1L;
        seasonInstanceIndex = absoluteDayIndex / daysPerSeason;
        dayInSeason = (int)(absoluteDayIndex % daysPerSeason) + 1;
        slotIndex = slotHours.Count - 1;
        return true;
    }

    private void ApplyWeatherChange(
        RuntimeRegionState state,
        WeatherType nextWeather,
        long boundaryMinute)
    {
        if (state.CurrentWeatherType == nextWeather)
            return;

        if (boundaryMinute < state.CurrentWeatherStartMinute)
        {
            throw new InvalidOperationException(
                $"Weather boundary {boundaryMinute} precedes region '{state.RegionId}' current start "
                + $"{state.CurrentWeatherStartMinute}.");
        }

        CloseCurrentHistorySegment(state, boundaryMinute);
        state.CurrentWeatherType = nextWeather;
        state.CurrentWeatherStartMinute = boundaryMinute;

        InvokeWeatherChanged(new WeatherSnapshot(
            state.RegionId,
            state.CurrentWeatherType,
            boundaryMinute,
            0L));
    }

    private static void CloseCurrentHistorySegment(RuntimeRegionState state, long boundaryMinute)
    {
        if (boundaryMinute == state.CurrentWeatherStartMinute)
            return;

        int currentWeatherType = (int)state.CurrentWeatherType;
        if (state.History.Count > 0)
        {
            WeatherHistoryRecordSaveEntry previous = state.History[state.History.Count - 1];
            if (previous != null
                && previous.weatherType == currentWeatherType
                && previous.endMinuteExclusive == state.CurrentWeatherStartMinute)
            {
                previous.endMinuteExclusive = boundaryMinute;
                return;
            }
        }

        state.History.Add(new WeatherHistoryRecordSaveEntry
        {
            weatherType = currentWeatherType,
            startMinute = state.CurrentWeatherStartMinute,
            endMinuteExclusive = boundaryMinute
        });
    }

    private void InvokeWeatherChanged(WeatherSnapshot snapshot)
    {
        if (WeatherChanged == null)
            return;

        Delegate[] invocationList = WeatherChanged.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<WeatherSnapshot>)invocationList[i]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                // One observer must not strand later region resolution at the same time boundary.
                Debug.LogException(exception, this);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Reconciliation
    // -------------------------------------------------------------------------

    private RuntimeRegionState BuildRegionState(
        ClimateRegionDefinition definition,
        RegionWeatherSaveEntry saved,
        bool saveInitialized,
        HashSet<uint> reservedSeeds,
        long loadedMinute,
        WorldTimeSnapshot loadedSnapshot,
        int daysPerSeason)
    {
        WeatherType initialWeather = definition.InitialWeatherType;
        bool hasSavedState = saveInitialized && saved != null;

        if (!hasSavedState)
        {
            uint freshRootSeed = CreateRootSeed(reservedSeeds);
            WeatherType freshWeather = initialWeather;
            if (TryGetActiveSlot(
                loadedSnapshot,
                daysPerSeason,
                out long absoluteDayIndex,
                out long seasonInstanceIndex,
                out int dayInSeason,
                out int slotIndex))
            {
                freshWeather = ResolveWeatherAtSlot(
                    definition,
                    freshRootSeed,
                    absoluteDayIndex,
                    seasonInstanceIndex,
                    dayInSeason,
                    slotIndex);
            }

            return new RuntimeRegionState(
                definition.RegionId,
                freshWeather,
                loadedMinute,
                loadedMinute,
                freshRootSeed,
                new List<WeatherHistoryRecordSaveEntry>());
        }

        WeatherType currentWeather = IsValidWeatherType(saved.currentWeatherType)
            ? (WeatherType)saved.currentWeatherType
            : InvalidWeatherFallback(definition, saved.currentWeatherType);
        uint rootSeed = saved.rootSeed;
        if (rootSeed == 0u)
        {
            Debug.LogWarning(
                $"[WorldWeatherState] Saved region '{definition.RegionId}' had a zero root seed; creating a new seed.",
                this);
            rootSeed = CreateRootSeed(reservedSeeds);
        }
        else
        {
            // Valid saved seeds are authoritative. Reserving them only prevents a newly added
            // region from receiving the same generated seed; it never rewrites the saved value.
            reservedSeeds.Add(rootSeed);
        }

        if (HasNegativeTimestamp(saved))
        {
            Debug.LogWarning(
                $"[WorldWeatherState] Saved region '{definition.RegionId}' contained a negative timestamp; resetting its history and weather boundary to the loaded world minute.",
                this);
            return new RuntimeRegionState(
                definition.RegionId,
                currentWeather,
                loadedMinute,
                loadedMinute,
                rootSeed,
                new List<WeatherHistoryRecordSaveEntry>());
        }

        long currentStart = NormalizeTimestamp(
            saved.currentWeatherStartMinute,
            loadedMinute,
            $"region '{definition.RegionId}' currentWeatherStartMinute");
        long availableFrom = NormalizeTimestamp(
            saved.historyAvailableFromMinute,
            loadedMinute,
            $"region '{definition.RegionId}' historyAvailableFromMinute");

        List<WeatherHistoryRecordSaveEntry> history;
        if (!TryNormalizeHistory(
            saved.history,
            definition.RegionId,
            loadedMinute,
            availableFrom,
            currentStart,
            out history))
        {
            Debug.LogWarning(
                $"[WorldWeatherState] Saved region '{definition.RegionId}' had invalid or overlapping history; resetting its known history boundary.",
                this);
            history = new List<WeatherHistoryRecordSaveEntry>();
            availableFrom = loadedMinute;
            currentStart = loadedMinute;
        }
        else if (currentStart < availableFrom)
        {
            Debug.LogWarning(
                $"[WorldWeatherState] Saved region '{definition.RegionId}' started before its history boundary; resetting both timestamps to the loaded minute.",
                this);
            history.Clear();
            availableFrom = loadedMinute;
            currentStart = loadedMinute;
        }

        return new RuntimeRegionState(
            definition.RegionId,
            currentWeather,
            currentStart,
            availableFrom,
            rootSeed,
            history);
    }

    private static bool HasNegativeTimestamp(RegionWeatherSaveEntry saved)
    {
        if (saved.currentWeatherStartMinute < 0L || saved.historyAvailableFromMinute < 0L)
            return true;

        if (saved.history == null)
            return false;

        for (int i = 0; i < saved.history.Count; i++)
        {
            WeatherHistoryRecordSaveEntry record = saved.history[i];
            if (record != null && (record.startMinute < 0L || record.endMinuteExclusive < 0L))
                return true;
        }

        return false;
    }

    private Dictionary<string, RegionWeatherSaveEntry> CollectSavedRegions(WorldWeatherSaveData source)
    {
        Dictionary<string, RegionWeatherSaveEntry> result =
            new Dictionary<string, RegionWeatherSaveEntry>(StringComparer.Ordinal);
        if (source == null || !source.initialized || source.regions == null)
            return result;

        for (int i = 0; i < source.regions.Count; i++)
        {
            RegionWeatherSaveEntry entry = source.regions[i];
            if (entry == null || string.IsNullOrEmpty(entry.regionId))
            {
                Debug.LogWarning("[WorldWeatherState] Ignoring a saved weather region with an empty ID.", this);
                continue;
            }

            if (result.ContainsKey(entry.regionId))
            {
                Debug.LogWarning(
                    $"[WorldWeatherState] Duplicate saved weather region '{entry.regionId}' ignored; first entry wins.",
                    this);
                continue;
            }

            result.Add(entry.regionId, entry);
        }

        foreach (KeyValuePair<string, RegionWeatherSaveEntry> pair in result)
        {
            if (!climateRegionCatalog.TryGetRegion(pair.Key, out _))
            {
                Debug.LogWarning(
                    $"[WorldWeatherState] Saved weather region '{pair.Key}' is not in the current catalog and was discarded.",
                    this);
            }
        }

        return result;
    }

    private HashSet<uint> CollectReservedSeeds(
        Dictionary<string, RegionWeatherSaveEntry> savedById,
        bool saveInitialized)
    {
        HashSet<uint> reserved = new HashSet<uint>();
        if (!saveInitialized)
            return reserved;

        foreach (KeyValuePair<string, RegionWeatherSaveEntry> pair in savedById)
        {
            if (pair.Value != null
                && pair.Value.rootSeed != 0u
                && climateRegionCatalog.TryGetRegion(pair.Key, out _))
            {
                reserved.Add(pair.Value.rootSeed);
            }
        }

        return reserved;
    }

    private void NormalizeOverrides(List<WeatherOverrideSaveEntry> source)
    {
        if (source == null)
            return;

        HashSet<OverrideKey> seen = new HashSet<OverrideKey>();
        for (int i = 0; i < source.Count; i++)
        {
            WeatherOverrideSaveEntry entry = source[i];
            if (entry == null || string.IsNullOrEmpty(entry.regionId)
                || !climateRegionCatalog.TryGetRegion(entry.regionId, out _)
                || entry.absoluteDayIndex < 0L
                || !IsValidSlotIndex(entry.slotIndex)
                || !IsValidWeatherType(entry.weatherType))
            {
                Debug.LogWarning("[WorldWeatherState] Ignoring a malformed saved weather override.", this);
                continue;
            }

            OverrideKey key = new OverrideKey(entry.regionId, entry.absoluteDayIndex, entry.slotIndex);
            if (!seen.Add(key))
            {
                Debug.LogWarning(
                    $"[WorldWeatherState] Duplicate weather override for '{entry.regionId}' day {entry.absoluteDayIndex}, slot {entry.slotIndex} ignored; first entry wins.",
                    this);
                continue;
            }

            activeOverrides.Add(new WeatherOverrideSaveEntry
            {
                regionId = entry.regionId,
                absoluteDayIndex = entry.absoluteDayIndex,
                slotIndex = entry.slotIndex,
                weatherType = entry.weatherType,
                sourceTag = entry.sourceTag ?? string.Empty
            });
        }
    }

    private void ValidateDependencies(WorldTimeState worldTimeState)
    {
        if (weatherSimulationConfig == null)
            throw new InvalidOperationException("WorldWeatherState requires a WeatherSimulationConfig reference.");
        if (climateRegionCatalog == null)
            throw new InvalidOperationException("WorldWeatherState requires a ClimateRegionCatalog reference.");

        weatherSimulationConfig.ValidateOrThrow();
        if (!climateRegionCatalog.TryValidate(worldTimeState.SeasonsPerYear, out string catalogError))
        {
            throw new InvalidOperationException($"Invalid ClimateRegionCatalog: {catalogError}");
        }

        for (int regionIndex = 0; regionIndex < climateRegionCatalog.Regions.Count; regionIndex++)
        {
            ClimateRegionDefinition region = climateRegionCatalog.Regions[regionIndex];
            SeasonTrackDefinition track = region.SeasonTrack;
            for (int seasonIndex = 0; seasonIndex < track.Seasons.Count; seasonIndex++)
            {
                if (!track.Seasons[seasonIndex].TryValidate(
                    worldTimeState.DaysPerSeason,
                    weatherSimulationConfig.WeatherSlotHours,
                    out string seasonError))
                {
                    throw new InvalidOperationException($"Invalid ClimateRegionCatalog: {seasonError}");
                }
            }
        }
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < weatherSimulationConfig.WeatherSlotHours.Count;
    }

    private static bool IsValidWeatherType(int raw)
    {
        return WeatherTypeUtility.TryFromInt(raw, out _);
    }

    private WeatherType InvalidWeatherFallback(ClimateRegionDefinition definition, int raw)
    {
        Debug.LogWarning(
            $"[WorldWeatherState] Saved region '{definition.RegionId}' contained invalid weather value {raw}; using its authored initial weather.",
            this);
        return definition.InitialWeatherType;
    }

    private long NormalizeTimestamp(long value, long loadedMinute, string label)
    {
        if (value < 0L)
        {
            Debug.LogWarning($"[WorldWeatherState] {label} was negative and was clamped to zero.", this);
            return 0L;
        }
        if (value > loadedMinute)
        {
            Debug.LogWarning(
                $"[WorldWeatherState] {label} was after the loaded world minute and was clamped.",
                this);
            return loadedMinute;
        }

        return value;
    }

    private bool TryNormalizeHistory(
        List<WeatherHistoryRecordSaveEntry> source,
        string regionId,
        long loadedMinute,
        long availableFrom,
        long currentStart,
        out List<WeatherHistoryRecordSaveEntry> normalized)
    {
        normalized = new List<WeatherHistoryRecordSaveEntry>();
        if (source == null)
            return true;

        long previousEnd = availableFrom;
        for (int i = 0; i < source.Count; i++)
        {
            WeatherHistoryRecordSaveEntry entry = source[i];
            if (entry == null || !IsValidWeatherType(entry.weatherType))
                return false;

            long start = entry.startMinute;
            long end = entry.endMinuteExclusive;
            if (start < 0L || end < 0L)
                return false;
            if (start > loadedMinute)
            {
                Debug.LogWarning(
                    $"[WorldWeatherState] Saved region '{regionId}' history start was after the loaded world minute and was clamped.",
                    this);
                start = loadedMinute;
            }
            if (end > loadedMinute)
            {
                Debug.LogWarning(
                    $"[WorldWeatherState] Saved region '{regionId}' history end was after the loaded world minute and was clamped.",
                    this);
                end = loadedMinute;
            }

            if (end <= start || start < previousEnd || end > currentStart)
                return false;

            normalized.Add(new WeatherHistoryRecordSaveEntry
            {
                weatherType = entry.weatherType,
                startMinute = start,
                endMinuteExclusive = end
            });
            previousEnd = end;
        }

        return true;
    }

    private static uint CreateRootSeed(HashSet<uint> reservedSeeds = null)
    {
        // Randomness is allowed only at one-time region creation. The resulting seed is saved and
        // all future Package-3 generation will consume the persisted bits deterministically.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            uint high = (uint)UnityEngine.Random.Range(0, 1 << 16);
            uint low = (uint)UnityEngine.Random.Range(0, 1 << 16);
            uint seed = (high << 16) | low;
            if (seed == 0u)
                seed = 1u;

            if (reservedSeeds == null || reservedSeeds.Add(seed))
                return seed;
        }

        // This is practically unreachable, but keeps fresh-region creation total even if a test
        // replaces Unity's random source with a constant value.
        uint fallback = 0xA341316Cu;
        if (reservedSeeds == null)
            return fallback;

        while (!reservedSeeds.Add(fallback))
        {
            fallback++;
            if (fallback == 0u)
                fallback = 1u;
        }

        return fallback;
    }

    private static WorldWeatherSaveData CreateEmptySave()
    {
        return new WorldWeatherSaveData
        {
            initialized = false,
            regions = new List<RegionWeatherSaveEntry>(),
            overrides = new List<WeatherOverrideSaveEntry>()
        };
    }

    private static WorldWeatherSaveData DeepCopy(WorldWeatherSaveData source)
    {
        WorldWeatherSaveData copy = CreateEmptySave();
        if (source == null)
            return copy;

        copy.initialized = source.initialized;
        if (source.regions != null)
        {
            for (int i = 0; i < source.regions.Count; i++)
            {
                RegionWeatherSaveEntry entry = source.regions[i];
                if (entry == null)
                {
                    copy.regions.Add(null);
                    continue;
                }

                RegionWeatherSaveEntry region = new RegionWeatherSaveEntry
                {
                    regionId = entry.regionId ?? string.Empty,
                    currentWeatherType = entry.currentWeatherType,
                    currentWeatherStartMinute = entry.currentWeatherStartMinute,
                    historyAvailableFromMinute = entry.historyAvailableFromMinute,
                    rootSeed = entry.rootSeed,
                    history = new List<WeatherHistoryRecordSaveEntry>()
                };

                if (entry.history != null)
                {
                    for (int historyIndex = 0; historyIndex < entry.history.Count; historyIndex++)
                    {
                        WeatherHistoryRecordSaveEntry record = entry.history[historyIndex];
                        region.history.Add(record == null
                            ? null
                            : new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = record.weatherType,
                                startMinute = record.startMinute,
                                endMinuteExclusive = record.endMinuteExclusive
                            });
                    }
                }

                copy.regions.Add(region);
            }
        }

        if (source.overrides != null)
        {
            for (int i = 0; i < source.overrides.Count; i++)
            {
                WeatherOverrideSaveEntry entry = source.overrides[i];
                copy.overrides.Add(entry == null
                    ? null
                    : new WeatherOverrideSaveEntry
                    {
                        regionId = entry.regionId ?? string.Empty,
                        absoluteDayIndex = entry.absoluteDayIndex,
                        slotIndex = entry.slotIndex,
                        weatherType = entry.weatherType,
                        sourceTag = entry.sourceTag ?? string.Empty
                    });
            }
        }

        return copy;
    }

    private sealed class RuntimeRegionState
    {
        public readonly string RegionId;
        public WeatherType CurrentWeatherType;
        public long CurrentWeatherStartMinute;
        public readonly long HistoryAvailableFromMinute;
        public readonly uint RootSeed;
        public readonly List<WeatherHistoryRecordSaveEntry> History;

        public RuntimeRegionState(
            string regionId,
            WeatherType currentWeatherType,
            long currentWeatherStartMinute,
            long historyAvailableFromMinute,
            uint rootSeed,
            List<WeatherHistoryRecordSaveEntry> history)
        {
            RegionId = regionId;
            CurrentWeatherType = currentWeatherType;
            CurrentWeatherStartMinute = currentWeatherStartMinute;
            HistoryAvailableFromMinute = historyAvailableFromMinute;
            RootSeed = rootSeed;
            History = history;
        }
    }

    private readonly struct OverrideKey : IEquatable<OverrideKey>
    {
        private readonly string regionId;
        private readonly long absoluteDayIndex;
        private readonly int slotIndex;

        public OverrideKey(string regionId, long absoluteDayIndex, int slotIndex)
        {
            this.regionId = regionId;
            this.absoluteDayIndex = absoluteDayIndex;
            this.slotIndex = slotIndex;
        }

        public long AbsoluteDayIndex => absoluteDayIndex;
        public int SlotIndex => slotIndex;

        public bool Equals(OverrideKey other)
        {
            return string.Equals(regionId, other.regionId, StringComparison.Ordinal)
                && absoluteDayIndex == other.absoluteDayIndex
                && slotIndex == other.slotIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is OverrideKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = regionId == null ? 0 : StringComparer.Ordinal.GetHashCode(regionId);
                hash = (hash * 397) ^ absoluteDayIndex.GetHashCode();
                return (hash * 397) ^ slotIndex;
            }
        }
    }
}
