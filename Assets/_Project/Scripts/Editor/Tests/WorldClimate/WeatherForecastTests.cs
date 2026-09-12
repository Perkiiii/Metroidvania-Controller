using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WeatherForecastTests
{
    private const int WeatherCount = 5;
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
    public void ForecastEntryIsImmutableAndValidatesItsHalfOpenInterval()
    {
        WeatherForecastEntry entry = new WeatherForecastEntry(WeatherType.Rain, 120L, 240L);

        Assert.That(entry.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(entry.StartMinute, Is.EqualTo(120L));
        Assert.That(entry.EndMinuteExclusive, Is.EqualTo(240L));
        Assert.That(new WeatherForecastEntry(WeatherType.Rain, 120L, 240L), Is.EqualTo(entry));
        Assert.That(entry.GetHashCode(), Is.EqualTo(
            new WeatherForecastEntry(WeatherType.Rain, 120L, 240L).GetHashCode()));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherForecastEntry((WeatherType)999, 0L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherForecastEntry(WeatherType.Clear, -1L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherForecastEntry(WeatherType.Clear, 1L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherForecastEntry(WeatherType.Clear, 2L, 1L));
    }

    [Test]
    public void ForecastDefensivelyCopiesEntriesAndValidatesEnvelope()
    {
        List<WeatherForecastEntry> source = new List<WeatherForecastEntry>
        {
            new WeatherForecastEntry(WeatherType.Rain, 10L, 20L)
        };
        WeatherForecast forecast = new WeatherForecast("region_a", 10L, 20L, source);

        source[0] = new WeatherForecastEntry(WeatherType.Storm, 10L, 20L);
        Assert.That(forecast.Entries[0].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(forecast.RegionId, Is.EqualTo("region_a"));
        Assert.That(forecast.RequestedAtMinute, Is.EqualTo(10L));
        Assert.That(forecast.EndMinuteExclusive, Is.EqualTo(20L));

        IList<WeatherForecastEntry> readOnly = (IList<WeatherForecastEntry>)forecast.Entries;
        Assert.That(readOnly.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => readOnly.Add(
            new WeatherForecastEntry(WeatherType.Clear, 20L, 30L)));
        Assert.Throws<NotSupportedException>(() => readOnly[0] =
            new WeatherForecastEntry(WeatherType.Clear, 10L, 20L));

        Assert.Throws<ArgumentException>(() => new WeatherForecast(" ", 10L, 20L, source));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherForecast("region_a", -1L, 20L, source));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherForecast("region_a", 20L, 20L, source));
        Assert.Throws<ArgumentNullException>(() => new WeatherForecast("region_a", 10L, 20L, null));
    }

    [Test]
    public void ForecastRejectsUnloadedUnknownAndOutOfRangeRequests()
    {
        ClimateFixture fixture = CreateFixture(28, 4, 2, 0, 0);
        WorldWeatherState state = CreateWeatherState(fixture);
        Assert.That(state.TryGetForecast("region_a", 0, out _), Is.False);

        WorldTimeState time = CreateWorldTime(fixture.Calendar, 0L);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.TryGetForecast(null, 0, out _), Is.False);
        Assert.That(state.TryGetForecast(string.Empty, 0, out _), Is.False);
        Assert.That(state.TryGetForecast("missing", 0, out _), Is.False);
        Assert.That(state.TryGetForecast("region_a", -1, out _), Is.False);
        Assert.That(state.TryGetForecast("region_a", 3, out _), Is.False);
        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast zero), Is.True);
        Assert.That(state.TryGetForecast("region_a", 2, out WeatherForecast maximum), Is.True);
        Assert.That(zero.RequestedAtMinute, Is.Zero);
        Assert.That(maximum.EndMinuteExclusive, Is.EqualTo(3L * CalendarConfig.MinutesPerDay));
    }

    [Test]
    public void ZeroDaysAheadEndsAtTheNextDayAndMeansRemainderOfToday()
    {
        ClimateFixture fixture = CreateFixture(
            28,
            4,
            28,
            0,
            0,
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Rain),
                new FixedWeatherSlot(1, 1, WeatherType.Storm)
            });
        WorldWeatherState state = LoadState(fixture, 7L * 60L + 15L, out _);

        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast forecast), Is.True);
        Assert.That(forecast.RequestedAtMinute, Is.EqualTo(435L));
        Assert.That(forecast.EndMinuteExclusive, Is.EqualTo(CalendarConfig.MinutesPerDay));
        Assert.That(forecast.Entries, Has.Count.EqualTo(2));
        AssertEntry(forecast.Entries[0], WeatherType.Rain, 435L, 1080L);
        AssertEntry(forecast.Entries[1], WeatherType.Storm, 1080L, 1440L);
    }

    [Test]
    public void ExactCurrentSlotIsResolvedLiveAndNotDuplicatedInForecast()
    {
        ClimateFixture fixture = CreateFixture(
            28,
            4,
            28,
            0,
            0,
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Rain),
                new FixedWeatherSlot(1, 1, WeatherType.Storm)
            });
        WorldWeatherState state = LoadState(fixture, 6L * 60L, out _);

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot current), Is.True);
        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast forecast), Is.True);
        Assert.That(forecast.Entries, Has.Count.EqualTo(2));
        AssertEntry(forecast.Entries[0], current.WeatherType, 360L, 1080L);
        AssertEntry(forecast.Entries[1], WeatherType.Storm, 1080L, 1440L);

        int entriesStartingAtNow = 0;
        for (int i = 0; i < forecast.Entries.Count; i++)
        {
            if (forecast.Entries[i].StartMinute == 360L)
                entriesStartingAtNow++;
        }

        Assert.That(entriesStartingAtNow, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedFutureTypesStillProduceOneEntryPerSlot()
    {
        ClimateFixture fixture = CreateFixture(
            28,
            4,
            28,
            0,
            0,
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Rain),
                new FixedWeatherSlot(1, 1, WeatherType.Rain)
            });
        WorldWeatherState state = LoadState(fixture, 5L * 60L, out _);

        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast forecast), Is.True);
        Assert.That(forecast.Entries, Has.Count.EqualTo(3));
        AssertEntry(forecast.Entries[0], WeatherType.Clear, 300L, 360L);
        AssertEntry(forecast.Entries[1], WeatherType.Rain, 360L, 1080L);
        AssertEntry(forecast.Entries[2], WeatherType.Rain, 1080L, 1440L);
        Assert.That(forecast.Entries[1].WeatherType, Is.EqualTo(forecast.Entries[2].WeatherType));
    }

    [Test]
    public void ForecastEntriesAreChronologicalContiguousAndHalfOpen()
    {
        ClimateFixture fixture = CreateFixture(28, 4, 2, 0, 0);
        WorldWeatherState state = LoadState(fixture, 300L, out _);

        Assert.That(state.TryGetForecast("region_a", 2, out WeatherForecast forecast), Is.True);
        Assert.That(forecast.Entries, Is.Not.Empty);
        Assert.That(forecast.Entries[0].StartMinute, Is.EqualTo(forecast.RequestedAtMinute));
        for (int i = 0; i < forecast.Entries.Count; i++)
        {
            WeatherForecastEntry entry = forecast.Entries[i];
            Assert.That(entry.StartMinute, Is.LessThan(entry.EndMinuteExclusive));
            if (i > 0)
                Assert.That(entry.StartMinute, Is.EqualTo(forecast.Entries[i - 1].EndMinuteExclusive));
        }

        Assert.That(forecast.Entries[forecast.Entries.Count - 1].EndMinuteExclusive,
            Is.EqualTo(forecast.EndMinuteExclusive));
    }

    [Test]
    public void ForecastUsesOverrideThenAuthoredFixedThenGeneratedPrecedence()
    {
        ClimateFixture fixture = CreateCustomFixture(
            28,
            4,
            28,
            0,
            0,
            null,
            null,
            new[] { MatrixThatAlwaysSelects(WeatherType.Storm) });
        SetField(fixture.Catalog.Regions[0].SeasonTrack.Seasons[0], "fixedWeatherSlots",
            new List<FixedWeatherSlot> { new FixedWeatherSlot(1, 0, WeatherType.Rain) });
        WorldWeatherState state = LoadState(fixture, 300L, out _);

        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 0, WeatherType.Fog, "fixed-override")), Is.True);
        Assert.That(state.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Cloudy, "generated-override")), Is.True);
        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast overridden), Is.True);
        AssertEntry(overridden.Entries[0], WeatherType.Clear, 300L, 360L);
        AssertEntry(overridden.Entries[1], WeatherType.Fog, 360L, 1080L);
        AssertEntry(overridden.Entries[2], WeatherType.Cloudy, 1080L, 1440L);

        Assert.That(state.ClearOverride("region_a", 0L, 1), Is.True);
        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast generated), Is.True);
        Assert.That(generated.Entries[2].WeatherType, Is.EqualTo(WeatherType.Storm));

        Assert.That(state.ClearOverride("region_a", 0L, 0), Is.True);
        Assert.That(state.TryGetForecast("region_a", 0, out WeatherForecast fixedWeather), Is.True);
        Assert.That(fixedWeather.Entries[1].WeatherType, Is.EqualTo(WeatherType.Rain));
    }

    [Test]
    public void ForecastUsesAbsoluteDaySeasonAndYearMathAcrossCalendarTransitions()
    {
        WeatherType[] entries = { WeatherType.Clear, WeatherType.Fog };
        FixedWeatherSlot[][] fixedSlots =
        {
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Clear),
                new FixedWeatherSlot(1, 1, WeatherType.Fog)
            },
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Rain),
                new FixedWeatherSlot(1, 1, WeatherType.Storm)
            }
        };
        ClimateFixture fixture = CreateCustomFixture(
            1,
            2,
            2,
            0,
            0,
            entries,
            fixedSlots,
            null);
        WorldWeatherState state = LoadState(fixture, 23L * 60L, out _);

        Assert.That(state.TryGetForecast("region_a", 2, out WeatherForecast forecast), Is.True);
        Assert.That(forecast.EndMinuteExclusive, Is.EqualTo(3L * CalendarConfig.MinutesPerDay));
        Assert.That(forecast.Entries, Has.Count.EqualTo(5));
        AssertEntry(forecast.Entries[0], WeatherType.Fog, 1380L, 1800L);
        AssertEntry(forecast.Entries[1], WeatherType.Rain, 1800L, 2520L);
        AssertEntry(forecast.Entries[2], WeatherType.Storm, 2520L, 3240L);
        AssertEntry(forecast.Entries[3], WeatherType.Clear, 3240L, 3960L);
        AssertEntry(forecast.Entries[4], WeatherType.Fog, 3960L, 4320L);
    }

    [Test]
    public void ForecastReturnsFalseWhenExclusiveEndOverflowsLong()
    {
        ClimateFixture fixture = CreateFixture(28, 4, 28, 0, 0);
        WorldWeatherState state = LoadState(fixture, long.MaxValue, out _);

        Assert.That(state.TryGetForecast("region_a", 0, out _), Is.False);
    }

    [Test]
    public void RepeatedAndReorderedForecastQueriesAreDeterministicAndNonMutating()
    {
        ClimateFixture fixture = CreateFixture(28, 4, 3, 0, 0);
        WorldWeatherState first = LoadState(fixture, 300L, out WorldTimeState firstTime);
        SaveData before = Gather(first);
        WorldTimeSnapshot beforeTime = firstTime.Current;
        WeatherSnapshot beforeWeather;
        Assert.That(first.TryGetWeather("region_a", out beforeWeather), Is.True);
        int eventCount = 0;
        first.WeatherChanged += _ => eventCount++;

        Assert.That(first.TryGetForecast("region_a", 1, out WeatherForecast firstOne), Is.True);
        Assert.That(first.TryGetForecast("region_a", 1, out WeatherForecast repeated), Is.True);
        AssertForecastEqual(firstOne, repeated);
        Assert.That(eventCount, Is.Zero);
        Assert.That(firstTime.Current, Is.EqualTo(beforeTime));
        Assert.That(first.TryGetWeather("region_a", out WeatherSnapshot afterWeather), Is.True);
        Assert.That(afterWeather, Is.EqualTo(beforeWeather));
        AssertWeatherSaveEqual(before, Gather(first));

        WorldTimeState secondTime = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState second = CreateWeatherState(fixture);
        second.ApplySaveData(before);
        second.CompleteLoad(secondTime);
        Assert.That(second.TryGetForecast("region_a", 1, out WeatherForecast secondOne), Is.True);
        Assert.That(second.TryGetForecast("region_a", 0, out WeatherForecast secondZero), Is.True);

        WorldTimeState thirdTime = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState third = CreateWeatherState(fixture);
        third.ApplySaveData(before);
        third.CompleteLoad(thirdTime);
        Assert.That(third.TryGetForecast("region_a", 0, out WeatherForecast thirdZero), Is.True);
        Assert.That(third.TryGetForecast("region_a", 1, out WeatherForecast thirdOne), Is.True);
        AssertForecastEqual(secondZero, thirdZero);
        AssertForecastEqual(secondOne, thirdOne);
    }

    [Test]
    public void ForecastSurvivesSaveAndReloadWithEquivalentOutput()
    {
        ClimateFixture fixture = CreateFixture(
            2,
            2,
            3,
            0,
            0,
            new[]
            {
                new FixedWeatherSlot(1, 0, WeatherType.Rain),
                new FixedWeatherSlot(1, 1, WeatherType.Storm)
            });
        WorldWeatherState source = LoadState(fixture, 300L, out _);
        Assert.That(source.SetOverride(new WeatherOverrideEntry(
            "region_a", 0L, 1, WeatherType.Fog, "save-test")), Is.True);
        Assert.That(source.TryGetForecast("region_a", 2, out WeatherForecast before), Is.True);
        SaveData saved = Gather(source);

        WorldTimeState destinationTime = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState destination = CreateWeatherState(fixture);
        destination.ApplySaveData(saved);
        destination.CompleteLoad(destinationTime);
        Assert.That(destination.TryGetForecast("region_a", 2, out WeatherForecast after), Is.True);
        AssertForecastEqual(before, after);
    }

    [Test]
    public void ForecastBeforeLiveProgressionMatchesLiveProgressionWithoutForecast()
    {
        ClimateFixture fixture = CreateCustomFixture(
            28,
            4,
            2,
            0,
            0,
            null,
            null,
            new[] { MatrixThatAlwaysSelects(WeatherType.Storm) });
        WorldWeatherState withForecast = LoadState(fixture, 300L, out WorldTimeState withForecastTime);
        SaveData initialSave = Gather(withForecast);
        WorldTimeState withoutForecastTime = CreateWorldTime(fixture.Calendar, 300L);
        WorldWeatherState withoutForecast = CreateWeatherState(fixture);
        withoutForecast.ApplySaveData(initialSave);
        withoutForecast.CompleteLoad(withoutForecastTime);

        Assert.That(withForecast.TryGetForecast("region_a", 1, out _), Is.True);
        withForecastTime.AdvanceMinutes(1800L);
        withoutForecastTime.AdvanceMinutes(1800L);

        Assert.That(withForecast.TryGetWeather("region_a", out WeatherSnapshot forecastWeather), Is.True);
        Assert.That(withoutForecast.TryGetWeather("region_a", out WeatherSnapshot liveWeather), Is.True);
        Assert.That(forecastWeather, Is.EqualTo(liveWeather));
        AssertWeatherSaveEqual(Gather(withoutForecast), Gather(withForecast));
    }

    private ClimateFixture CreateFixture(
        int daysPerSeason,
        int seasonsPerYear,
        int maxForecastDays,
        int initialHour,
        int initialMinute,
        params FixedWeatherSlot[] fixedSlots)
    {
        WeatherType[] entryWeatherTypes = new WeatherType[seasonsPerYear];
        FixedWeatherSlot[][] fixedSlotsBySeason = new FixedWeatherSlot[seasonsPerYear][];
        for (int i = 0; i < seasonsPerYear; i++)
        {
            entryWeatherTypes[i] = WeatherType.Clear;
            fixedSlotsBySeason[i] = fixedSlots;
        }

        return CreateCustomFixture(
            daysPerSeason,
            seasonsPerYear,
            maxForecastDays,
            initialHour,
            initialMinute,
            entryWeatherTypes,
            fixedSlotsBySeason,
            null);
    }

    private ClimateFixture CreateCustomFixture(
        int daysPerSeason,
        int seasonsPerYear,
        int maxForecastDays,
        int initialHour,
        int initialMinute,
        WeatherType[] entryWeatherTypes,
        FixedWeatherSlot[][] fixedSlotsBySeason,
        int[][] transitionMatrices)
    {
        CalendarConfig calendar = Track(ScriptableObject.CreateInstance<CalendarConfig>());
        calendar.daysPerSeason = daysPerSeason;
        calendar.seasonsPerYear = seasonsPerYear;
        calendar.initialYear = 1;
        calendar.initialSeasonOrdinal = 0;
        calendar.initialDayInSeason = 1;
        calendar.initialHour = initialHour;
        calendar.initialMinute = initialMinute;
        calendar.realSecondsPerGameMinute = 1f;

        WeatherSimulationConfig simulation = Track(ScriptableObject.CreateInstance<WeatherSimulationConfig>());
        List<WeatherDefinition> definitions = new List<WeatherDefinition>();
        for (int i = 0; i < WeatherCount; i++)
        {
            WeatherDefinition definition = Track(ScriptableObject.CreateInstance<WeatherDefinition>());
            SetField(definition, "type", (WeatherType)i);
            SetField(definition, "displayName", ((WeatherType)i).ToString());
            definitions.Add(definition);
        }

        SetField(simulation, "weatherSlotHours", new List<int> { 6, 18 });
        SetField(simulation, "maxForecastDaysAhead", maxForecastDays);
        SetField(simulation, "weatherDefinitions", definitions);
        SetField(simulation, "lookupBuilt", false);
        SetField(simulation, "lookupValid", false);
        SetField(simulation, "definitionsByType", null);

        List<SeasonDefinition> seasons = new List<SeasonDefinition>();
        for (int i = 0; i < seasonsPerYear; i++)
        {
            SeasonDefinition season = Track(ScriptableObject.CreateInstance<SeasonDefinition>());
            WeatherType entryWeather = entryWeatherTypes != null && i < entryWeatherTypes.Length
                ? entryWeatherTypes[i]
                : WeatherType.Clear;
            int[] matrix = transitionMatrices != null
                && i < transitionMatrices.Length
                && transitionMatrices[i] != null
                ? transitionMatrices[i]
                : IdentityMatrix();
            FixedWeatherSlot[] slots = fixedSlotsBySeason != null
                && i < fixedSlotsBySeason.Length
                ? fixedSlotsBySeason[i]
                : null;
            SetField(season, "seasonId", "season_" + i);
            SetField(season, "displayName", "Season " + i);
            SetField(season, "entryWeatherType", entryWeather);
            SetField(season, "transitionWeights", (int[])matrix.Clone());
            SetField(season, "fixedWeatherSlots", slots == null
                ? new List<FixedWeatherSlot>()
                : new List<FixedWeatherSlot>(slots));
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

    private WorldWeatherState LoadState(
        ClimateFixture fixture,
        long minute,
        out WorldTimeState time)
    {
        time = CreateWorldTime(fixture.Calendar, minute);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        return state;
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

    private static void AssertEntry(
        WeatherForecastEntry entry,
        WeatherType weatherType,
        long startMinute,
        long endMinuteExclusive)
    {
        Assert.That(entry.WeatherType, Is.EqualTo(weatherType));
        Assert.That(entry.StartMinute, Is.EqualTo(startMinute));
        Assert.That(entry.EndMinuteExclusive, Is.EqualTo(endMinuteExclusive));
    }

    private static void AssertForecastEqual(WeatherForecast actual, WeatherForecast expected)
    {
        Assert.That(actual.RegionId, Is.EqualTo(expected.RegionId));
        Assert.That(actual.RequestedAtMinute, Is.EqualTo(expected.RequestedAtMinute));
        Assert.That(actual.EndMinuteExclusive, Is.EqualTo(expected.EndMinuteExclusive));
        Assert.That(actual.Entries.Count, Is.EqualTo(expected.Entries.Count));
        for (int i = 0; i < expected.Entries.Count; i++)
            Assert.That(actual.Entries[i], Is.EqualTo(expected.Entries[i]));
    }

    private static void AssertWeatherSaveEqual(SaveData actual, SaveData expected)
    {
        Assert.That(actual.worldWeather.initialized, Is.EqualTo(expected.worldWeather.initialized));
        Assert.That(actual.worldWeather.regions, Has.Count.EqualTo(expected.worldWeather.regions.Count));
        for (int i = 0; i < expected.worldWeather.regions.Count; i++)
        {
            RegionWeatherSaveEntry left = actual.worldWeather.regions[i];
            RegionWeatherSaveEntry right = expected.worldWeather.regions[i];
            Assert.That(left.regionId, Is.EqualTo(right.regionId));
            Assert.That(left.currentWeatherType, Is.EqualTo(right.currentWeatherType));
            Assert.That(left.currentWeatherStartMinute, Is.EqualTo(right.currentWeatherStartMinute));
            Assert.That(left.historyAvailableFromMinute, Is.EqualTo(right.historyAvailableFromMinute));
            Assert.That(left.rootSeed, Is.EqualTo(right.rootSeed));
            Assert.That(left.history, Has.Count.EqualTo(right.history.Count));
            for (int historyIndex = 0; historyIndex < right.history.Count; historyIndex++)
            {
                Assert.That(left.history[historyIndex].weatherType,
                    Is.EqualTo(right.history[historyIndex].weatherType));
                Assert.That(left.history[historyIndex].startMinute,
                    Is.EqualTo(right.history[historyIndex].startMinute));
                Assert.That(left.history[historyIndex].endMinuteExclusive,
                    Is.EqualTo(right.history[historyIndex].endMinuteExclusive));
            }
        }

        Assert.That(actual.worldWeather.overrides, Has.Count.EqualTo(expected.worldWeather.overrides.Count));
        for (int i = 0; i < expected.worldWeather.overrides.Count; i++)
        {
            WeatherOverrideSaveEntry left = actual.worldWeather.overrides[i];
            WeatherOverrideSaveEntry right = expected.worldWeather.overrides[i];
            Assert.That(left.regionId, Is.EqualTo(right.regionId));
            Assert.That(left.absoluteDayIndex, Is.EqualTo(right.absoluteDayIndex));
            Assert.That(left.slotIndex, Is.EqualTo(right.slotIndex));
            Assert.That(left.weatherType, Is.EqualTo(right.weatherType));
            Assert.That(left.sourceTag, Is.EqualTo(right.sourceTag));
        }
    }

    private static int[] IdentityMatrix()
    {
        int[] matrix = new int[WeatherCount * WeatherCount];
        for (int i = 0; i < WeatherCount; i++)
            matrix[i * WeatherCount + i] = 1;
        return matrix;
    }

    private static int[] MatrixThatAlwaysSelects(WeatherType target)
    {
        int[] matrix = new int[WeatherCount * WeatherCount];
        for (int from = 0; from < WeatherCount; from++)
            matrix[from * WeatherCount + (int)target] = 1;
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
