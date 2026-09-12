using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WeatherOverrideTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void OverrideEntryIsImmutableValidatedAndNullNormalizesSourceTag()
    {
        WeatherOverrideEntry entry = new WeatherOverrideEntry(
            "region_a",
            7L,
            1,
            WeatherType.Rain,
            null);

        Assert.That(entry.IsValid, Is.True);
        Assert.That(entry.RegionId, Is.EqualTo("region_a"));
        Assert.That(entry.AbsoluteDayIndex, Is.EqualTo(7L));
        Assert.That(entry.SlotIndex, Is.EqualTo(1));
        Assert.That(entry.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(entry.SourceTag, Is.EqualTo(string.Empty));

        WeatherOverrideEntry defaultEntry = default(WeatherOverrideEntry);
        Assert.That(defaultEntry.IsValid, Is.False);
        Assert.That(defaultEntry.SourceTag, Is.EqualTo(string.Empty));
        Assert.That(new WeatherOverrideEntry(" ", 0L, 0, WeatherType.Clear).IsValid, Is.False);
        Assert.That(new WeatherOverrideEntry("region_a", -1L, 0, WeatherType.Clear).IsValid, Is.False);
        Assert.That(new WeatherOverrideEntry("region_a", 0L, -1, WeatherType.Clear).IsValid, Is.False);
        Assert.That(new WeatherOverrideEntry("region_a", 0L, 0, (WeatherType)999).IsValid, Is.False);

        WeatherOverrideEntry equivalent = new WeatherOverrideEntry(
            "region_a", 7L, 1, WeatherType.Rain, string.Empty);
        Assert.That(entry, Is.EqualTo(equivalent));
        Assert.That(entry.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
    }

    [Test]
    public void InvalidCommandsAndUnknownKeysFailClosed()
    {
        ClimateFixture fixture = CreateFixture();
        WorldWeatherState state = CreateWeatherState(fixture);

        WeatherOverrideEntry valid = new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Rain, "test");
        Assert.That(state.SetOverride(valid), Is.False);
        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.False);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(CreateWorldTime(fixture.Calendar, 0L));

        Assert.That(state.SetOverride(default(WeatherOverrideEntry)), Is.False);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", -1L, 0, WeatherType.Rain)), Is.False);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, -1, WeatherType.Rain)), Is.False);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 2, WeatherType.Rain)), Is.False);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, (WeatherType)999)), Is.False);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "missing", 0L, 0, WeatherType.Rain)), Is.False);
        Assert.That(state.ClearOverride(null, 0L, 0), Is.False);
        Assert.That(state.ClearOverride(string.Empty, 0L, 0), Is.False);
        Assert.That(state.ClearOverride("region_a", -1L, 0), Is.False);
        Assert.That(state.ClearOverride("region_a", 0L, -1), Is.False);
        Assert.That(state.ClearOverride("region_a", 0L, 2), Is.False);
        Assert.That(state.ClearOverride("missing", 0L, 0), Is.False);
    }

    [Test]
    public void SetReplacesInPlaceAndClearRemovesExactOrdinalKey()
    {
        ClimateFixture fixture = CreateFixture();
        WorldWeatherState state = CreateLoadedState(fixture, 0L);

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 1L, 0, WeatherType.Rain, "first")), Is.True);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Storm, "second")), Is.True);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "third")), Is.True);

        SaveData beforeReplace = Gather(state);
        Assert.That(beforeReplace.worldWeather.overrides, Has.Count.EqualTo(3));
        AssertOverride(beforeReplace.worldWeather.overrides[0], "region_a", 1L, 0,
            WeatherType.Rain, "first");
        AssertOverride(beforeReplace.worldWeather.overrides[1], "region_a", 0L, 1,
            WeatherType.Storm, "second");
        AssertOverride(beforeReplace.worldWeather.overrides[2], "region_a", 0L, 0,
            WeatherType.Fog, "third");

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Clear, "replacement")), Is.True);
        SaveData afterReplace = Gather(state);
        Assert.That(afterReplace.worldWeather.overrides, Has.Count.EqualTo(3));
        AssertOverride(afterReplace.worldWeather.overrides[0], "region_a", 1L, 0,
            WeatherType.Rain, "first");
        AssertOverride(afterReplace.worldWeather.overrides[1], "region_a", 0L, 1,
            WeatherType.Clear, "replacement");
        AssertOverride(afterReplace.worldWeather.overrides[2], "region_a", 0L, 0,
            WeatherType.Fog, "third");

        Assert.That(state.ClearOverride("region_a", 1L, 0), Is.True);
        SaveData afterClear = Gather(state);
        Assert.That(afterClear.worldWeather.overrides, Has.Count.EqualTo(2));
        AssertOverride(afterClear.worldWeather.overrides[0], "region_a", 0L, 1,
            WeatherType.Clear, "replacement");
        AssertOverride(afterClear.worldWeather.overrides[1], "region_a", 0L, 0,
            WeatherType.Fog, "third");
        Assert.That(state.ClearOverride("region_a", 1L, 0), Is.False);
    }

    [Test]
    public void DayZeroBeforeFirstSlotHasNoActiveKeyAndFutureOverrideAppliesAtBoundary()
    {
        ClimateFixture fixture = CreateFixture();
        WorldWeatherState state = CreateLoadedState(fixture, 0L);
        int eventCount = 0;
        state.WeatherChanged += _ => eventCount++;

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot before), Is.True);
        Assert.That(before.WeatherType, Is.EqualTo(WeatherType.Clear));
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "future")), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot stillBefore), Is.True);
        Assert.That(stillBefore, Is.EqualTo(before));
        Assert.That(eventCount, Is.Zero);

        // The state uses the same loaded clock; advance through the exact first slot.
        WorldTimeState time = GetLoadedWorldTime(state);
        time.AdvanceMinutes(360L);

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot after), Is.True);
        Assert.That(after.WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(after.SinceMinute, Is.EqualTo(360L));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void FutureAndPastOverridesDoNotMutateCurrentWeatherOrHistory()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "future")), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot before), Is.True);
        Assert.That(before.WeatherType, Is.EqualTo(WeatherType.Clear));

        time.AdvanceMinutes(780L); // 300 -> 1080; both authored slots are crossed.
        SaveData beforePast = Gather(state);
        RegionWeatherSaveEntry beforeRegion = beforePast.worldWeather.regions[0];
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot currentBeforePast), Is.True);
        Assert.That(currentBeforePast.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(currentBeforePast.SinceMinute, Is.EqualTo(1080L));

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Clear, "past")), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot currentAfterPast), Is.True);
        Assert.That(currentAfterPast, Is.EqualTo(currentBeforePast));
        RegionWeatherSaveEntry afterRegion = Gather(state).worldWeather.regions[0];
        AssertRegionHistoryEqual(afterRegion, beforeRegion);
    }

    [Test]
    public void ActiveOverrideCorrectionUsesCurrentMinuteHistoryAndEventPath()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        List<WeatherSnapshot> changes = new List<WeatherSnapshot>();
        state.WeatherChanged += changes.Add;
        time.AdvanceMinutes(60L);
        Assert.That(changes, Has.Count.EqualTo(1));
        Assert.That(changes[0].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[0].SinceMinute, Is.EqualTo(360L));

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Storm, "active")), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot corrected), Is.True);
        Assert.That(corrected.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(corrected.SinceMinute, Is.EqualTo(360L));
        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[1].WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(changes[1].SinceMinute, Is.EqualTo(360L));

        RegionWeatherSaveEntry afterCorrection = Gather(state).worldWeather.regions[0];
        Assert.That(afterCorrection.history, Has.Count.EqualTo(1));
        Assert.That(afterCorrection.history[0].weatherType, Is.EqualTo((int)WeatherType.Clear));
        Assert.That(afterCorrection.history[0].startMinute, Is.EqualTo(300L));
        Assert.That(afterCorrection.history[0].endMinuteExclusive, Is.EqualTo(360L));

        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot fallback), Is.True);
        Assert.That(fallback.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(fallback.SinceMinute, Is.EqualTo(360L));
        Assert.That(changes, Has.Count.EqualTo(3));
        Assert.That(changes[2].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[2].SinceMinute, Is.EqualTo(360L));
    }

    [Test]
    public void MidSlotActiveSetReplaceAndClearUseCurrentMinuteWithoutSameResultChurn()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        List<WeatherSnapshot> changes = new List<WeatherSnapshot>();
        state.WeatherChanged += changes.Add;
        time.AdvanceMinutes(75L); // 375: 15 minutes inside the 06:00 slot.
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot baseline), Is.True);
        Assert.That(baseline.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(baseline.SinceMinute, Is.EqualTo(360L));
        Assert.That(changes, Has.Count.EqualTo(1));

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Storm, "mid-slot")), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot overridden), Is.True);
        Assert.That(overridden.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(overridden.SinceMinute, Is.EqualTo(375L));
        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[1].WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(changes[1].SinceMinute, Is.EqualTo(375L));

        SaveData afterSet = Gather(state);
        Assert.That(afterSet.worldWeather.regions[0].history, Has.Count.EqualTo(2));
        Assert.That(afterSet.worldWeather.regions[0].history[1].weatherType,
            Is.EqualTo((int)WeatherType.Rain));
        Assert.That(afterSet.worldWeather.regions[0].history[1].startMinute,
            Is.EqualTo(360L));
        Assert.That(afterSet.worldWeather.regions[0].history[1].endMinuteExclusive,
            Is.EqualTo(375L));

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Storm, "mid-slot-replaced")), Is.True);
        Assert.That(changes, Has.Count.EqualTo(2));
        SaveData afterSameResultReplace = Gather(state);
        Assert.That(afterSameResultReplace.worldWeather.regions[0].history, Has.Count.EqualTo(2));
        Assert.That(afterSameResultReplace.worldWeather.regions[0].currentWeatherStartMinute,
            Is.EqualTo(375L));
        AssertOverride(afterSameResultReplace.worldWeather.overrides[0], "region_a", 0L, 0,
            WeatherType.Storm, "mid-slot-replaced");

        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot cleared), Is.True);
        Assert.That(cleared.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(cleared.SinceMinute, Is.EqualTo(375L));
        Assert.That(changes, Has.Count.EqualTo(3));
        Assert.That(changes[2].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[2].SinceMinute, Is.EqualTo(375L));
        RegionWeatherSaveEntry afterClear = Gather(state).worldWeather.regions[0];
        AssertRegionHistoryEqual(afterClear, afterSet.worldWeather.regions[0]);
    }

    [Test]
    public void SameResultOverrideAndClearAreNoOpsForEventsAndHistory()
    {
        ClimateFixture fixture = CreateFixture(new FixedWeatherSlot(1, 0, WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        time.AdvanceMinutes(60L);

        int eventCount = 0;
        state.WeatherChanged += _ => eventCount++;
        SaveData before = Gather(state);

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Rain, "same")), Is.True);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Rain, "same-replaced")), Is.True);
        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);

        SaveData after = Gather(state);
        Assert.That(eventCount, Is.Zero);
        Assert.That(after.worldWeather.regions[0].currentWeatherType,
            Is.EqualTo(before.worldWeather.regions[0].currentWeatherType));
        Assert.That(after.worldWeather.regions[0].currentWeatherStartMinute,
            Is.EqualTo(before.worldWeather.regions[0].currentWeatherStartMinute));
        AssertRegionHistoryEqual(after.worldWeather.regions[0], before.worldWeather.regions[0]);
        Assert.That(after.worldWeather.overrides, Is.Empty);
    }

    [Test]
    public void ClearFallsThroughAuthoredFixedThenGeneratedWeather()
    {
        ClimateFixture fixture = CreateFixture(new FixedWeatherSlot(1, 0, WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        List<WeatherSnapshot> changes = new List<WeatherSnapshot>();
        state.WeatherChanged += changes.Add;
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "fixed-test")), Is.True);
        time.AdvanceMinutes(60L);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot fixedOverride), Is.True);
        Assert.That(fixedOverride.WeatherType, Is.EqualTo(WeatherType.Fog));

        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot fixedFallback), Is.True);
        Assert.That(fixedFallback.WeatherType, Is.EqualTo(WeatherType.Rain));

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Fog, "generated-test")), Is.True);
        time.AdvanceMinutes(720L);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot generatedOverride), Is.True);
        Assert.That(generatedOverride.WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(state.ClearOverride("region_a", 0L, 1), Is.True);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot generatedFallback), Is.True);
        Assert.That(generatedFallback.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(changes, Has.Count.EqualTo(4));
        Assert.That(changes[0].WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(changes[1].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[2].WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(changes[3].WeatherType, Is.EqualTo(WeatherType.Storm));
    }

    [Test]
    public void PastOverrideLeavesExperiencedHistoryImmutable()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        time.AdvanceMinutes(780L);

        RegionWeatherSaveEntry before = Gather(state).worldWeather.regions[0];
        Assert.That(before.history, Has.Count.EqualTo(2));
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "past-history")), Is.True);
        RegionWeatherSaveEntry after = Gather(state).worldWeather.regions[0];

        AssertRegionHistoryEqual(after, before);
        Assert.That(after.currentWeatherType, Is.EqualTo(before.currentWeatherType));
        Assert.That(after.currentWeatherStartMinute, Is.EqualTo(before.currentWeatherStartMinute));
    }

    [Test]
    public void OverridesPreserveSourceTagsAndDeterministicV6SaveReload()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "quest")), Is.True);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Cloudy, null)), Is.True);
        time.AdvanceMinutes(60L);

        SaveData saved = Gather(state);
        WorldTimeState destinationTime = CreateWorldTime(fixture.Calendar, 360L);
        WorldWeatherState destination = CreateWeatherState(fixture);
        destination.ApplySaveData(saved);
        destination.CompleteLoad(destinationTime);

        SaveData reloaded = Gather(destination);
        Assert.That(reloaded.worldWeather.initialized, Is.True);
        Assert.That(reloaded.worldWeather.overrides, Has.Count.EqualTo(2));
        AssertOverride(reloaded.worldWeather.overrides[0], "region_a", 0L, 0,
            WeatherType.Fog, "quest");
        AssertOverride(reloaded.worldWeather.overrides[1], "region_a", 0L, 1,
            WeatherType.Cloudy, string.Empty);
        Assert.That(destination.TryGetWeather("region_a", out WeatherSnapshot reloadedCurrent), Is.True);
        Assert.That(reloadedCurrent.WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(reloadedCurrent.SinceMinute, Is.EqualTo(360L));

        time.AdvanceMinutes(720L);
        destinationTime.AdvanceMinutes(720L);
        SaveData originalAfter = Gather(state);
        SaveData destinationAfter = Gather(destination);
        AssertWeatherRegionEqual(destinationAfter.worldWeather.regions[0], originalAfter.worldWeather.regions[0]);
        Assert.That(destinationAfter.worldWeather.overrides, Has.Count.EqualTo(2));
    }

    [Test]
    public void OverrideCommandsDoNotInvalidateGeneratorCache()
    {
        ClimateFixture fixture = CreateFixture();
        WorldTimeState time = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(GetGeneratorCacheCount(state), Is.Zero);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "future")), Is.True);
        Assert.That(GetGeneratorCacheCount(state), Is.Zero);

        time.AdvanceMinutes(60L);
        Assert.That(GetGeneratorCacheCount(state), Is.Zero);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Storm, "replace")), Is.True);
        Assert.That(GetGeneratorCacheCount(state), Is.Zero);

        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(GetGeneratorCacheCount(state), Is.EqualTo(1));
        object cachedRegion = GetGeneratorCacheRegion(state);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "cached")), Is.True);
        Assert.That(GetGeneratorCacheRegion(state), Is.SameAs(cachedRegion));
        Assert.That(GetGeneratorCacheCount(state), Is.EqualTo(1));
        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(GetGeneratorCacheRegion(state), Is.SameAs(cachedRegion));
        Assert.That(GetGeneratorCacheCount(state), Is.EqualTo(1));
    }

    private WorldWeatherState CreateLoadedState(ClimateFixture fixture, long minute)
    {
        WorldTimeState time = CreateWorldTime(fixture.Calendar, minute);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        return state;
    }

    private ClimateFixture CreateFixture(params FixedWeatherSlot[] fixedSlots)
    {
        CalendarConfig calendar = Track(ScriptableObject.CreateInstance<CalendarConfig>());
        calendar.daysPerSeason = 28;
        calendar.seasonsPerYear = 4;
        calendar.initialYear = 1;
        calendar.initialSeasonOrdinal = 0;
        calendar.initialDayInSeason = 1;
        calendar.initialHour = 0;
        calendar.initialMinute = 0;
        calendar.realSecondsPerGameMinute = 1f;

        WeatherSimulationConfig simulation = Track(ScriptableObject.CreateInstance<WeatherSimulationConfig>());
        List<WeatherDefinition> definitions = new List<WeatherDefinition>();
        int weatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
        for (int i = 0; i < weatherTypeCount; i++)
        {
            WeatherDefinition definition = Track(ScriptableObject.CreateInstance<WeatherDefinition>());
            SetField(definition, "type", (WeatherType)i);
            SetField(definition, "displayName", ((WeatherType)i).ToString());
            definitions.Add(definition);
        }

        SetField(simulation, "weatherSlotHours", new List<int> { 6, 18 });
        SetField(simulation, "maxForecastDaysAhead", 28);
        SetField(simulation, "weatherDefinitions", definitions);
        SetField(simulation, "lookupBuilt", false);
        SetField(simulation, "lookupValid", false);
        SetField(simulation, "definitionsByType", null);

        List<SeasonDefinition> seasons = new List<SeasonDefinition>();
        for (int i = 0; i < calendar.seasonsPerYear; i++)
        {
            SeasonDefinition season = Track(ScriptableObject.CreateInstance<SeasonDefinition>());
            SetField(season, "seasonId", "season_" + i);
            SetField(season, "displayName", "Season " + i);
            SetField(season, "entryWeatherType", WeatherType.Clear);
            SetField(season, "transitionWeights", MatrixThatAlwaysSelects(WeatherType.Storm));
            SetField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>(fixedSlots));
            seasons.Add(season);
        }

        SeasonTrackDefinition track = Track(ScriptableObject.CreateInstance<SeasonTrackDefinition>());
        SetField(track, "seasons", seasons);
        ClimateRegionDefinition region = Track(ScriptableObject.CreateInstance<ClimateRegionDefinition>());
        SetField(region, "regionId", "region_a");
        SetField(region, "displayName", "Region A");
        SetField(region, "seasonTrack", track);
        SetField(region, "initialWeatherType", WeatherType.Clear);
        ClimateRegionCatalog catalog = Track(ScriptableObject.CreateInstance<ClimateRegionCatalog>());
        SetField(catalog, "regions", new List<ClimateRegionDefinition> { region });
        SetField(catalog, "lookupBuilt", false);
        SetField(catalog, "lookupValid", false);
        SetField(catalog, "regionsById", null);

        return new ClimateFixture(calendar, simulation, catalog);
    }

    private WorldWeatherState CreateWeatherState(ClimateFixture fixture)
    {
        WorldWeatherState state = Track(ScriptableObject.CreateInstance<WorldWeatherState>());
        SetField(state, "weatherSimulationConfig", fixture.Simulation);
        SetField(state, "climateRegionCatalog", fixture.Catalog);
        return state;
    }

    private WorldTimeState CreateWorldTime(CalendarConfig calendar, long minute)
    {
        WorldTimeState state = Track(ScriptableObject.CreateInstance<WorldTimeState>());
        SetField(state, "calendarConfig", calendar);
        state.ApplySaveData(new SaveData
        {
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = minute
            }
        });
        return state;
    }

    private static SaveData Gather(WorldWeatherState state)
    {
        SaveData data = new SaveData();
        state.GatherSaveData(data);
        return data;
    }

    private static WorldTimeState GetLoadedWorldTime(WorldWeatherState state)
    {
        FieldInfo field = typeof(WorldWeatherState).GetField(
            "loadedWorldTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        WorldTimeState result = field.GetValue(state) as WorldTimeState;
        Assert.That(result, Is.Not.Null);
        return result;
    }

    private static int GetGeneratorCacheCount(WorldWeatherState state)
    {
        object generator = GetPrivateField(state, "weatherGenerator");
        object caches = GetPrivateField(generator, "cachesByRegion");
        return ((IDictionary)caches).Count;
    }

    private static object GetGeneratorCacheRegion(WorldWeatherState state)
    {
        object generator = GetPrivateField(state, "weatherGenerator");
        object caches = GetPrivateField(generator, "cachesByRegion");
        return ((IDictionary)caches)["region_a"];
    }

    private static object GetPrivateField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected private field '" + name + "'.");
        return field.GetValue(target);
    }

    private static void AssertOverride(
        WeatherOverrideSaveEntry entry,
        string regionId,
        long day,
        int slot,
        WeatherType type,
        string sourceTag)
    {
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.regionId, Is.EqualTo(regionId));
        Assert.That(entry.absoluteDayIndex, Is.EqualTo(day));
        Assert.That(entry.slotIndex, Is.EqualTo(slot));
        Assert.That(entry.weatherType, Is.EqualTo((int)type));
        Assert.That(entry.sourceTag, Is.EqualTo(sourceTag));
    }

    private static void AssertRegionHistoryEqual(
        RegionWeatherSaveEntry actual,
        RegionWeatherSaveEntry expected)
    {
        Assert.That(actual.history, Has.Count.EqualTo(expected.history.Count));
        for (int i = 0; i < expected.history.Count; i++)
        {
            Assert.That(actual.history[i].weatherType, Is.EqualTo(expected.history[i].weatherType));
            Assert.That(actual.history[i].startMinute, Is.EqualTo(expected.history[i].startMinute));
            Assert.That(actual.history[i].endMinuteExclusive,
                Is.EqualTo(expected.history[i].endMinuteExclusive));
        }
    }

    private static void AssertWeatherRegionEqual(
        RegionWeatherSaveEntry actual,
        RegionWeatherSaveEntry expected)
    {
        Assert.That(actual.regionId, Is.EqualTo(expected.regionId));
        Assert.That(actual.currentWeatherType, Is.EqualTo(expected.currentWeatherType));
        Assert.That(actual.currentWeatherStartMinute, Is.EqualTo(expected.currentWeatherStartMinute));
        Assert.That(actual.historyAvailableFromMinute, Is.EqualTo(expected.historyAvailableFromMinute));
        Assert.That(actual.rootSeed, Is.EqualTo(expected.rootSeed));
        AssertRegionHistoryEqual(actual, expected);
    }

    private static int[] MatrixThatAlwaysSelects(WeatherType target)
    {
        int weatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
        int[] matrix = new int[weatherTypeCount * weatherTypeCount];
        for (int from = 0; from < weatherTypeCount; from++)
            matrix[from * weatherTypeCount + (int)target] = 1;
        return matrix;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected serialized field '" + fieldName + "'.");
        field.SetValue(target, value);
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
    }

    private readonly struct ClimateFixture
    {
        public readonly CalendarConfig Calendar;
        public readonly WeatherSimulationConfig Simulation;
        public readonly ClimateRegionCatalog Catalog;

        public ClimateFixture(
            CalendarConfig calendar,
            WeatherSimulationConfig simulation,
            ClimateRegionCatalog catalog)
        {
            Calendar = calendar;
            Simulation = simulation;
            Catalog = catalog;
        }
    }
}
