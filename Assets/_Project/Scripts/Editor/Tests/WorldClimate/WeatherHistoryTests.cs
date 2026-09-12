using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WeatherHistoryTests
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
    public void HistoryRecordValidatesItsHalfOpenIntervalAndIsImmutable()
    {
        WeatherHistoryRecord record = new WeatherHistoryRecord(WeatherType.Rain, 120L, 240L);

        Assert.That(record.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(record.StartMinute, Is.EqualTo(120L));
        Assert.That(record.EndMinuteExclusive, Is.EqualTo(240L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryRecord((WeatherType)999, 0L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryRecord(WeatherType.Clear, -1L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryRecord(WeatherType.Clear, 1L, 1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryRecord(WeatherType.Clear, 2L, 1L));
    }

    [Test]
    public void QueryResultDefensivelyOwnsReadOnlyRecordsAndComputesCompleteness()
    {
        List<WeatherHistoryRecord> source = new List<WeatherHistoryRecord>
        {
            new WeatherHistoryRecord(WeatherType.Rain, 10L, 20L)
        };
        WeatherHistoryQueryResult result = new WeatherHistoryQueryResult(source, 0L, 30L, 10L);
        source[0] = new WeatherHistoryRecord(WeatherType.Storm, 10L, 20L);
        source.Add(new WeatherHistoryRecord(WeatherType.Clear, 20L, 30L));

        Assert.That(result.RequestedFrom, Is.EqualTo(0L));
        Assert.That(result.RequestedTo, Is.EqualTo(30L));
        Assert.That(result.AvailableFrom, Is.EqualTo(10L));
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.Records, Has.Count.EqualTo(1));
        Assert.That(result.Records[0].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(result.Records, Is.Not.SameAs(source));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<WeatherHistoryRecord>)result.Records).Add(
                new WeatherHistoryRecord(WeatherType.Clear, 30L, 40L)));

        WeatherHistoryQueryResult complete = new WeatherHistoryQueryResult(
            Array.Empty<WeatherHistoryRecord>(),
            10L,
            20L,
            10L);
        Assert.That(complete.IsComplete, Is.True);
        Assert.Throws<ArgumentNullException>(() =>
            new WeatherHistoryQueryResult(null, 0L, 1L, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryQueryResult(Array.Empty<WeatherHistoryRecord>(), -1L, 1L, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryQueryResult(Array.Empty<WeatherHistoryRecord>(), 1L, 1L, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherHistoryQueryResult(Array.Empty<WeatherHistoryRecord>(), 0L, 1L, -1L));
    }

    [Test]
    public void InvalidRangesUnknownRegionsAndPreLoadQueriesFailClosed()
    {
        ClimateFixture fixture = CreateFixture();
        WorldTimeState time = CreateWorldTime(fixture.CalendarConfig, 600L);
        WorldWeatherState state = CreateWeatherState(fixture);

        Assert.That(state.TryQueryHistory("region_a", 0L, 1L, out _), Is.False);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.TryQueryHistory("missing", 0L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory(string.Empty, 0L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory(null, 0L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", -1L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", 1L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", 2L, 1L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", 0L, 601L, out _), Is.False);
    }

    [Test]
    public void QueryReturnsClosedRecordsAndSyntheticCurrentOpenSegment()
    {
        WorldWeatherState state = CreateLoadedState(
            600L,
            100L,
            WeatherType.Storm,
            300L,
            new HistorySpec(WeatherType.Rain, 100L, 300L));

        Assert.That(state.TryQueryHistory("region_a", 100L, 600L, out WeatherHistoryQueryResult result), Is.True);
        AssertRecords(result, new RecordSpec(WeatherType.Rain, 100L, 300L),
            new RecordSpec(WeatherType.Storm, 300L, 600L));
        Assert.That(result.IsComplete, Is.True);
    }

    [Test]
    public void QueryClipsRecordsToRequestAndAvailabilityPrefix()
    {
        WorldWeatherState state = CreateLoadedState(
            600L,
            200L,
            WeatherType.Storm,
            300L,
            new HistorySpec(WeatherType.Rain, 200L, 300L));

        Assert.That(state.TryQueryHistory("region_a", 100L, 350L, out WeatherHistoryQueryResult result), Is.True);
        Assert.That(result.RequestedFrom, Is.EqualTo(100L));
        Assert.That(result.RequestedTo, Is.EqualTo(350L));
        Assert.That(result.AvailableFrom, Is.EqualTo(200L));
        Assert.That(result.IsComplete, Is.False);
        AssertRecords(result, new RecordSpec(WeatherType.Rain, 200L, 300L),
            new RecordSpec(WeatherType.Storm, 300L, 350L));

        Assert.That(state.TryQueryHistory("region_a", 250L, 350L, out WeatherHistoryQueryResult complete), Is.True);
        Assert.That(complete.IsComplete, Is.True);
        AssertRecords(complete, new RecordSpec(WeatherType.Rain, 250L, 300L),
            new RecordSpec(WeatherType.Storm, 300L, 350L));
    }

    [Test]
    public void QueryCoalescesAdjacentCompatibleClosedAndOpenSegments()
    {
        WorldWeatherState state = CreateLoadedState(
            400L,
            100L,
            WeatherType.Rain,
            300L,
            new HistorySpec(WeatherType.Rain, 100L, 200L),
            new HistorySpec(WeatherType.Rain, 200L, 300L));

        Assert.That(state.TryQueryHistory("region_a", 100L, 400L, out WeatherHistoryQueryResult result), Is.True);
        AssertRecords(result, new RecordSpec(WeatherType.Rain, 100L, 400L));
    }

    [Test]
    public void EntirelyUnavailableRangeReturnsTrueEmptyAndIncomplete()
    {
        WorldWeatherState state = CreateLoadedState(
            600L,
            300L,
            WeatherType.Clear,
            300L);

        Assert.That(state.TryQueryHistory("region_a", 0L, 200L, out WeatherHistoryQueryResult unavailable), Is.True);
        Assert.That(unavailable.Records, Is.Empty);
        Assert.That(unavailable.AvailableFrom, Is.EqualTo(300L));
        Assert.That(unavailable.IsComplete, Is.False);

        Assert.That(state.TryQueryHistory("region_a", 0L, 400L, out WeatherHistoryQueryResult partial), Is.True);
        Assert.That(partial.IsComplete, Is.False);
        AssertRecords(partial, new RecordSpec(WeatherType.Clear, 300L, 400L));
    }

    [Test]
    public void QueryHonorsCurrentTimeAndHalfOpenRangeLimits()
    {
        WorldWeatherState state = CreateLoadedState(
            600L,
            0L,
            WeatherType.Rain,
            300L,
            new HistorySpec(WeatherType.Clear, 0L, 300L));

        Assert.That(state.TryQueryHistory("region_a", 0L, 600L, out WeatherHistoryQueryResult atNow), Is.True);
        Assert.That(atNow.Records[atNow.Records.Count - 1].EndMinuteExclusive, Is.EqualTo(600L));
        Assert.That(state.TryQueryHistory("region_a", 0L, 601L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", 600L, 600L, out _), Is.False);
        Assert.That(state.TryQueryHistory("region_a", 601L, 602L, out _), Is.False);
    }

    [Test]
    public void UnloadedFarmQueryReportsRainOccurrenceAndDuration()
    {
        WorldWeatherState state = CreateLoadedState(
            17L * 60L,
            0L,
            WeatherType.Rain,
            15L * 60L,
            new HistorySpec(WeatherType.Clear, 0L, 9L * 60L),
            new HistorySpec(WeatherType.Rain, 9L * 60L, 12L * 60L),
            new HistorySpec(WeatherType.Clear, 12L * 60L, 15L * 60L));

        Assert.That(state.TryQueryHistory(
            "region_a",
            9L * 60L,
            17L * 60L,
            out WeatherHistoryQueryResult result), Is.True);
        AssertRecords(result,
            new RecordSpec(WeatherType.Rain, 9L * 60L, 12L * 60L),
            new RecordSpec(WeatherType.Clear, 12L * 60L, 15L * 60L),
            new RecordSpec(WeatherType.Rain, 15L * 60L, 17L * 60L));

        long rainMinutes = 0L;
        for (int i = 0; i < result.Records.Count; i++)
        {
            if (result.Records[i].WeatherType == WeatherType.Rain)
                rainMinutes += result.Records[i].EndMinuteExclusive - result.Records[i].StartMinute;
        }

        Assert.That(rainMinutes, Is.EqualTo(5L * 60L));
    }

    [Test]
    public void HistoryQuerySurvivesSaveReload()
    {
        ClimateFixture fixture = CreateFixture(
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(fixture.CalendarConfig, 300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        time.AdvanceMinutes(780L);

        Assert.That(state.TryQueryHistory("region_a", 300L, 1080L, out WeatherHistoryQueryResult before), Is.True);
        SaveData saved = new SaveData();
        state.GatherSaveData(saved);

        WorldTimeState destinationTime = CreateWorldTime(fixture.CalendarConfig, 1080L);
        WorldWeatherState destination = CreateWeatherState(fixture);
        destination.ApplySaveData(saved);
        destination.CompleteLoad(destinationTime);

        Assert.That(destination.TryQueryHistory("region_a", 300L, 1080L, out WeatherHistoryQueryResult after), Is.True);
        AssertResultEqual(before, after);
    }

    [Test]
    public void RepeatedQueriesDoNotMutateStateHistorySubscriptionsOrSeeds()
    {
        WorldWeatherState state = CreateLoadedState(
            600L,
            100L,
            WeatherType.Storm,
            300L,
            new HistorySpec(WeatherType.Rain, 100L, 300L));
        SaveData beforeSave = new SaveData();
        state.GatherSaveData(beforeSave);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot beforeSnapshot), Is.True);
        Assert.That(state.TryGetRootSeed("region_a", out uint beforeSeed), Is.True);
        int eventCount = 0;
        state.WeatherChanged += _ => eventCount++;

        Assert.That(state.TryQueryHistory("region_a", 100L, 600L, out WeatherHistoryQueryResult first), Is.True);
        Assert.That(state.TryQueryHistory("region_a", 100L, 600L, out WeatherHistoryQueryResult second), Is.True);
        AssertResultEqual(first, second);

        SaveData afterSave = new SaveData();
        state.GatherSaveData(afterSave);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot afterSnapshot), Is.True);
        Assert.That(state.TryGetRootSeed("region_a", out uint afterSeed), Is.True);
        Assert.That(afterSnapshot, Is.EqualTo(beforeSnapshot));
        Assert.That(afterSeed, Is.EqualTo(beforeSeed));
        Assert.That(eventCount, Is.Zero);
        AssertWeatherSaveEqual(beforeSave.worldWeather, afterSave.worldWeather);
    }

    private WorldWeatherState CreateLoadedState(
        long now,
        long availableFrom,
        WeatherType currentWeather,
        long currentStart,
        params HistorySpec[] history)
    {
        ClimateFixture fixture = CreateFixture();
        WorldTimeState time = CreateWorldTime(fixture.CalendarConfig, now);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = CreateWeatherSave(
                availableFrom,
                currentWeather,
                currentStart,
                history)
        });
        state.CompleteLoad(time);
        return state;
    }

    private ClimateFixture CreateFixture(params FixedWeatherSlot[] fixedSlots)
    {
        CalendarConfig calendar = Track(Create<CalendarConfig>());
        calendar.daysPerSeason = 28;
        calendar.seasonsPerYear = 4;
        calendar.initialYear = 1;
        calendar.initialSeasonOrdinal = 0;
        calendar.initialDayInSeason = 1;
        calendar.initialHour = 0;
        calendar.initialMinute = 0;
        calendar.realSecondsPerGameMinute = 1f;

        WeatherSimulationConfig config = Track(Create<WeatherSimulationConfig>());
        List<WeatherDefinition> definitions = new List<WeatherDefinition>();
        int weatherCount = Enum.GetValues(typeof(WeatherType)).Length;
        for (int i = 0; i < weatherCount; i++)
        {
            WeatherDefinition definition = Track(Create<WeatherDefinition>());
            SetField(definition, "type", (WeatherType)i);
            SetField(definition, "displayName", ((WeatherType)i).ToString());
            definitions.Add(definition);
        }

        SetField(config, "weatherSlotHours", new List<int> { 6, 18 });
        SetField(config, "maxForecastDaysAhead", 28);
        SetField(config, "weatherDefinitions", definitions);
        SetField(config, "lookupBuilt", false);
        SetField(config, "lookupValid", false);
        SetField(config, "definitionsByType", null);

        List<SeasonDefinition> seasons = new List<SeasonDefinition>();
        for (int i = 0; i < calendar.seasonsPerYear; i++)
        {
            SeasonDefinition season = Track(Create<SeasonDefinition>());
            SetField(season, "seasonId", "season_" + i);
            SetField(season, "displayName", "Season " + i);
            SetField(season, "entryWeatherType", WeatherType.Clear);
            SetField(season, "transitionWeights", UniformMatrix());
            SetField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>(fixedSlots));
            seasons.Add(season);
        }

        SeasonTrackDefinition track = Track(Create<SeasonTrackDefinition>());
        SetField(track, "seasons", seasons);
        ClimateRegionDefinition region = Track(Create<ClimateRegionDefinition>());
        SetField(region, "regionId", "region_a");
        SetField(region, "displayName", "Region A");
        SetField(region, "seasonTrack", track);
        SetField(region, "initialWeatherType", WeatherType.Clear);

        ClimateRegionCatalog catalog = Track(Create<ClimateRegionCatalog>());
        SetField(catalog, "regions", new List<ClimateRegionDefinition> { region });
        SetField(catalog, "lookupBuilt", false);
        SetField(catalog, "lookupValid", false);
        SetField(catalog, "regionsById", null);

        return new ClimateFixture(calendar, config, catalog);
    }

    private WorldWeatherState CreateWeatherState(ClimateFixture fixture)
    {
        WorldWeatherState state = Track(Create<WorldWeatherState>());
        SetField(state, "weatherSimulationConfig", fixture.SimulationConfig);
        SetField(state, "climateRegionCatalog", fixture.Catalog);
        return state;
    }

    private WorldTimeState CreateWorldTime(CalendarConfig calendar, long minute)
    {
        WorldTimeState state = Track(Create<WorldTimeState>());
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

    private static WorldWeatherSaveData CreateWeatherSave(
        long availableFrom,
        WeatherType currentWeather,
        long currentStart,
        IEnumerable<HistorySpec> history)
    {
        List<WeatherHistoryRecordSaveEntry> entries = new List<WeatherHistoryRecordSaveEntry>();
        foreach (HistorySpec item in history)
        {
            entries.Add(new WeatherHistoryRecordSaveEntry
            {
                weatherType = (int)item.WeatherType,
                startMinute = item.StartMinute,
                endMinuteExclusive = item.EndMinuteExclusive
            });
        }

        return new WorldWeatherSaveData
        {
            initialized = true,
            regions = new List<RegionWeatherSaveEntry>
            {
                new RegionWeatherSaveEntry
                {
                    regionId = "region_a",
                    currentWeatherType = (int)currentWeather,
                    currentWeatherStartMinute = currentStart,
                    historyAvailableFromMinute = availableFrom,
                    rootSeed = 0x12345678u,
                    history = entries
                }
            },
            overrides = new List<WeatherOverrideSaveEntry>()
        };
    }

    private static void AssertRecords(
        WeatherHistoryQueryResult result,
        params RecordSpec[] expected)
    {
        Assert.That(result.Records, Has.Count.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(result.Records[i].WeatherType, Is.EqualTo(expected[i].WeatherType));
            Assert.That(result.Records[i].StartMinute, Is.EqualTo(expected[i].StartMinute));
            Assert.That(result.Records[i].EndMinuteExclusive, Is.EqualTo(expected[i].EndMinuteExclusive));
        }
    }

    private static void AssertResultEqual(
        WeatherHistoryQueryResult left,
        WeatherHistoryQueryResult right)
    {
        Assert.That(right.RequestedFrom, Is.EqualTo(left.RequestedFrom));
        Assert.That(right.RequestedTo, Is.EqualTo(left.RequestedTo));
        Assert.That(right.AvailableFrom, Is.EqualTo(left.AvailableFrom));
        Assert.That(right.IsComplete, Is.EqualTo(left.IsComplete));
        Assert.That(right.Records, Is.EqualTo(left.Records));
    }

    private static void AssertWeatherSaveEqual(
        WorldWeatherSaveData left,
        WorldWeatherSaveData right)
    {
        Assert.That(right.initialized, Is.EqualTo(left.initialized));
        Assert.That(right.regions, Has.Count.EqualTo(left.regions.Count));
        for (int i = 0; i < left.regions.Count; i++)
        {
            RegionWeatherSaveEntry leftRegion = left.regions[i];
            RegionWeatherSaveEntry rightRegion = right.regions[i];
            Assert.That(rightRegion.regionId, Is.EqualTo(leftRegion.regionId));
            Assert.That(rightRegion.currentWeatherType, Is.EqualTo(leftRegion.currentWeatherType));
            Assert.That(rightRegion.currentWeatherStartMinute, Is.EqualTo(leftRegion.currentWeatherStartMinute));
            Assert.That(rightRegion.historyAvailableFromMinute, Is.EqualTo(leftRegion.historyAvailableFromMinute));
            Assert.That(rightRegion.rootSeed, Is.EqualTo(leftRegion.rootSeed));
            Assert.That(rightRegion.history, Has.Count.EqualTo(leftRegion.history.Count));
            for (int j = 0; j < leftRegion.history.Count; j++)
            {
                Assert.That(rightRegion.history[j].weatherType, Is.EqualTo(leftRegion.history[j].weatherType));
                Assert.That(rightRegion.history[j].startMinute, Is.EqualTo(leftRegion.history[j].startMinute));
                Assert.That(rightRegion.history[j].endMinuteExclusive, Is.EqualTo(leftRegion.history[j].endMinuteExclusive));
            }
        }
    }

    private static int[] UniformMatrix()
    {
        int weatherCount = Enum.GetValues(typeof(WeatherType)).Length;
        int[] matrix = new int[weatherCount * weatherCount];
        for (int i = 0; i < matrix.Length; i++)
            matrix[i] = 1;
        return matrix;
    }

    private T Create<T>() where T : ScriptableObject
    {
        return ScriptableObject.CreateInstance<T>();
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected serialized field '" + fieldName + "'.");
        field.SetValue(target, value);
    }

    private readonly struct HistorySpec
    {
        public readonly WeatherType WeatherType;
        public readonly long StartMinute;
        public readonly long EndMinuteExclusive;

        public HistorySpec(WeatherType weatherType, long startMinute, long endMinuteExclusive)
        {
            WeatherType = weatherType;
            StartMinute = startMinute;
            EndMinuteExclusive = endMinuteExclusive;
        }
    }

    private readonly struct RecordSpec
    {
        public readonly WeatherType WeatherType;
        public readonly long StartMinute;
        public readonly long EndMinuteExclusive;

        public RecordSpec(WeatherType weatherType, long startMinute, long endMinuteExclusive)
        {
            WeatherType = weatherType;
            StartMinute = startMinute;
            EndMinuteExclusive = endMinuteExclusive;
        }
    }

    private readonly struct ClimateFixture
    {
        public readonly CalendarConfig CalendarConfig;
        public readonly WeatherSimulationConfig SimulationConfig;
        public readonly ClimateRegionCatalog Catalog;

        public ClimateFixture(
            CalendarConfig calendarConfig,
            WeatherSimulationConfig simulationConfig,
            ClimateRegionCatalog catalog)
        {
            CalendarConfig = calendarConfig;
            SimulationConfig = simulationConfig;
            Catalog = catalog;
        }
    }
}
