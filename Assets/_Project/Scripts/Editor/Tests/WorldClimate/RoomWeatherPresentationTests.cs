using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoomWeatherPresentationTests
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
    public void OnEnable_RefreshesTheRoomImmediately()
    {
        WeatherFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Rain));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);

        InvokePrivate(presentation.Presenter, "OnEnable");

        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Rain));
    }

    [Test]
    public void RelevantWeatherChanged_RefreshesThePresentation()
    {
        WeatherFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear, WeatherType.Rain, WeatherType.Storm));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);
        List<RoomWeatherPresentationMode> changes = new List<RoomWeatherPresentationMode>();
        presentation.Presenter.PresentationChanged += changes.Add;

        InvokePrivate(presentation.Presenter, "OnEnable");
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));

        fixture.Time.AdvanceMinutes(6L * CalendarConfig.MinutesPerHour);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Rain));

        fixture.Time.AdvanceMinutes(12L * CalendarConfig.MinutesPerHour);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Storm));
        Assert.That(changes, Is.EqualTo(new[]
        {
            RoomWeatherPresentationMode.Rain,
            RoomWeatherPresentationMode.Storm
        }));
    }

    [Test]
    public void WeatherChanged_ForAnotherRegionIsIgnored()
    {
        WeatherFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear, WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Clear, WeatherType.Storm));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);
        int changeCount = 0;
        presentation.Presenter.PresentationChanged += _ => changeCount++;

        InvokePrivate(presentation.Presenter, "OnEnable");
        fixture.Time.AdvanceMinutes(6L * CalendarConfig.MinutesPerHour);

        Assert.That(fixture.Weather.TryGetWeather("region_b", out WeatherSnapshot regionB), Is.True);
        Assert.That(regionB.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));
        Assert.That(changeCount, Is.Zero);
    }

    [TestCase(WeatherType.Clear, RoomWeatherPresentationMode.Clear)]
    [TestCase(WeatherType.Cloudy, RoomWeatherPresentationMode.Clear)]
    [TestCase(WeatherType.Rain, RoomWeatherPresentationMode.Rain)]
    [TestCase(WeatherType.Storm, RoomWeatherPresentationMode.Storm)]
    [TestCase(WeatherType.Fog, RoomWeatherPresentationMode.Clear)]
    public void OutdoorWeatherPolicyMapsOnlyRainAndStorm(
        WeatherType weatherType,
        RoomWeatherPresentationMode expected)
    {
        WeatherFixture fixture = CreateFixture(new RegionSpec("region_a", weatherType));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);

        InvokePrivate(presentation.Presenter, "OnEnable");

        Assert.That(presentation.Presenter.CurrentPresentation, Is.EqualTo(expected));
    }

    [TestCase(EnvironmentExposure.Sheltered)]
    [TestCase(EnvironmentExposure.Indoor)]
    public void CoveredExposureAlwaysUsesClearBaseline(EnvironmentExposure exposure)
    {
        WeatherFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Storm));
        PresentationFixture presentation = CreatePresentation(fixture, "region_a", exposure);

        InvokePrivate(presentation.Presenter, "OnEnable");

        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));
    }

    [Test]
    public void RepeatedEnableDoesNotDuplicateWeatherSubscription()
    {
        WeatherFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear, WeatherType.Rain));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);
        int changeCount = 0;
        presentation.Presenter.PresentationChanged += _ => changeCount++;

        InvokePrivate(presentation.Presenter, "OnEnable");
        InvokePrivate(presentation.Presenter, "OnEnable");
        Assert.That(GetWeatherSubscriberCount(fixture.Weather), Is.EqualTo(1));

        fixture.Time.AdvanceMinutes(6L * CalendarConfig.MinutesPerHour);

        Assert.That(changeCount, Is.EqualTo(1));
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Rain));
    }

    [Test]
    public void DisableResetsToBaselineAndReenableReadsCurrentWeather()
    {
        WeatherFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear, WeatherType.Rain, WeatherType.Storm));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);

        InvokePrivate(presentation.Presenter, "OnEnable");
        fixture.Time.AdvanceMinutes(6L * CalendarConfig.MinutesPerHour);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Rain));

        InvokePrivate(presentation.Presenter, "OnDisable");
        InvokePrivate(presentation.Presenter, "OnDisable");
        Assert.That(GetWeatherSubscriberCount(fixture.Weather), Is.Zero);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));

        fixture.Time.AdvanceMinutes(12L * CalendarConfig.MinutesPerHour);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));

        InvokePrivate(presentation.Presenter, "OnEnable");
        Assert.That(GetWeatherSubscriberCount(fixture.Weather), Is.EqualTo(1));
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Storm));
    }

    [Test]
    public void DestroyCleanupIsSafeWhenLifecycleIsRepeated()
    {
        WeatherFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear, WeatherType.Rain));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);
        InvokePrivate(presentation.Presenter, "OnEnable");

        Assert.DoesNotThrow(() =>
        {
            InvokePrivate(presentation.Presenter, "OnDisable");
            InvokePrivate(presentation.Presenter, "OnDisable");
            InvokePrivate(presentation.Presenter, "OnDestroy");
            InvokePrivate(presentation.Presenter, "OnDestroy");
        });
        Assert.That(GetWeatherSubscriberCount(fixture.Weather), Is.Zero);

        fixture.Time.AdvanceMinutes(6L * CalendarConfig.MinutesPerHour);
        Assert.That(presentation.Presenter.CurrentPresentation,
            Is.EqualTo(RoomWeatherPresentationMode.Clear));
    }

    [Test]
    public void PresentationLifecycleDoesNotMutateWeatherOrTimeState()
    {
        WeatherFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Rain));
        PresentationFixture presentation = CreatePresentation(
            fixture,
            "region_a",
            EnvironmentExposure.Outdoor);
        Assert.That(fixture.Weather.TryGetWeather("region_a", out WeatherSnapshot beforeWeather), Is.True);
        SaveData beforeSave = new SaveData();
        fixture.Weather.GatherSaveData(beforeSave);
        long beforeMinute = fixture.Time.TotalGameMinutes;

        InvokePrivate(presentation.Presenter, "OnEnable");
        InvokePrivate(presentation.Presenter, "OnEnable");
        InvokePrivate(presentation.Presenter, "OnDisable");
        InvokePrivate(presentation.Presenter, "OnDestroy");

        Assert.That(fixture.Time.TotalGameMinutes, Is.EqualTo(beforeMinute));
        Assert.That(fixture.Weather.TryGetWeather("region_a", out WeatherSnapshot afterWeather), Is.True);
        Assert.That(afterWeather, Is.EqualTo(beforeWeather));
        SaveData afterSave = new SaveData();
        fixture.Weather.GatherSaveData(afterSave);
        AssertWeatherSaveEqual(beforeSave, afterSave);
    }

    private WeatherFixture CreateFixture(params RegionSpec[] regionSpecs)
    {
        WeatherSimulationConfig simulationConfig = Track(ScriptableObject.CreateInstance<WeatherSimulationConfig>());
        List<WeatherDefinition> weatherDefinitions = new List<WeatherDefinition>();
        int weatherCount = Enum.GetValues(typeof(WeatherType)).Length;
        for (int i = 0; i < weatherCount; i++)
        {
            WeatherDefinition definition = Track(ScriptableObject.CreateInstance<WeatherDefinition>());
            SetField(definition, "type", (WeatherType)i);
            SetField(definition, "displayName", ((WeatherType)i).ToString());
            weatherDefinitions.Add(definition);
        }

        SetField(simulationConfig, "weatherSlotHours", new List<int> { 6, 18 });
        SetField(simulationConfig, "maxForecastDaysAhead", 28);
        SetField(simulationConfig, "weatherDefinitions", weatherDefinitions);
        SetField(simulationConfig, "lookupBuilt", false);
        SetField(simulationConfig, "lookupValid", false);
        SetField(simulationConfig, "definitionsByType", null);

        CalendarConfig calendar = Track(ScriptableObject.CreateInstance<CalendarConfig>());
        calendar.daysPerSeason = 28;
        calendar.seasonsPerYear = 4;
        calendar.initialYear = 1;
        calendar.initialSeasonOrdinal = 0;
        calendar.initialDayInSeason = 1;
        calendar.initialHour = 0;
        calendar.initialMinute = 0;
        calendar.realSecondsPerGameMinute = 1f;

        List<ClimateRegionDefinition> regions = new List<ClimateRegionDefinition>();
        for (int regionIndex = 0; regionIndex < regionSpecs.Length; regionIndex++)
        {
            RegionSpec spec = regionSpecs[regionIndex];
            SeasonTrackDefinition track = Track(ScriptableObject.CreateInstance<SeasonTrackDefinition>());
            List<SeasonDefinition> seasons = new List<SeasonDefinition>();
            for (int seasonIndex = 0; seasonIndex < calendar.seasonsPerYear; seasonIndex++)
            {
                SeasonDefinition season = Track(ScriptableObject.CreateInstance<SeasonDefinition>());
                SetField(season, "seasonId", $"season_{regionIndex}_{seasonIndex}");
                SetField(season, "displayName", $"Season {regionIndex}_{seasonIndex}");
                SetField(season, "entryWeatherType", WeatherType.Clear);
                SetField(season, "transitionWeights", UniformMatrix());
                List<FixedWeatherSlot> fixedSlots = new List<FixedWeatherSlot>();
                if (spec.FirstSlotWeather.HasValue)
                    fixedSlots.Add(new FixedWeatherSlot(1, 0, spec.FirstSlotWeather.Value));
                if (spec.SecondSlotWeather.HasValue)
                    fixedSlots.Add(new FixedWeatherSlot(1, 1, spec.SecondSlotWeather.Value));
                SetField(season, "fixedWeatherSlots", fixedSlots);
                seasons.Add(season);
            }

            SetField(track, "seasons", seasons);
            ClimateRegionDefinition region = Track(ScriptableObject.CreateInstance<ClimateRegionDefinition>());
            SetField(region, "regionId", spec.Id);
            SetField(region, "displayName", spec.Id);
            SetField(region, "seasonTrack", track);
            SetField(region, "initialWeatherType", spec.InitialWeather);
            regions.Add(region);
        }

        ClimateRegionCatalog catalog = Track(ScriptableObject.CreateInstance<ClimateRegionCatalog>());
        SetField(catalog, "regions", regions);
        SetField(catalog, "lookupBuilt", false);
        SetField(catalog, "lookupValid", false);
        SetField(catalog, "regionsById", null);

        WorldTimeState time = Track(ScriptableObject.CreateInstance<WorldTimeState>());
        SetField(time, "calendarConfig", calendar);
        time.ApplySaveData(new SaveData
        {
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = 0L
            }
        });

        WorldWeatherState weather = Track(ScriptableObject.CreateInstance<WorldWeatherState>());
        SetField(weather, "weatherSimulationConfig", simulationConfig);
        SetField(weather, "climateRegionCatalog", catalog);
        weather.ApplySaveData(new SaveData());
        weather.CompleteLoad(time);

        return new WeatherFixture(time, weather);
    }

    private PresentationFixture CreatePresentation(
        WeatherFixture weather,
        string regionId,
        EnvironmentExposure exposure)
    {
        GameObject room = Track(new GameObject("Room Weather Presentation Test"));
        room.SetActive(false);
        RoomClimateContext context = room.AddComponent<RoomClimateContext>();
        SetField(context, "regionId", regionId);
        SetField(context, "exposure", exposure);
        RoomWeatherPresentation presenter = room.AddComponent<RoomWeatherPresentation>();
        SetField(presenter, "weatherState", weather.Weather);
        SetField(presenter, "roomClimateContext", context);
        return new PresentationFixture(presenter);
    }

    private static int GetWeatherSubscriberCount(WorldWeatherState weather)
    {
        FieldInfo field = typeof(WorldWeatherState).GetField(
            "WeatherChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Delegate callback = field.GetValue(weather) as Delegate;
        return callback == null ? 0 : callback.GetInvocationList().Length;
    }

    private static int[] UniformMatrix()
    {
        int weatherCount = Enum.GetValues(typeof(WeatherType)).Length;
        int[] matrix = new int[weatherCount * weatherCount];
        for (int i = 0; i < matrix.Length; i++)
            matrix[i] = 1;
        return matrix;
    }

    private static void AssertWeatherSaveEqual(SaveData expected, SaveData actual)
    {
        Assert.That(actual.worldWeather.initialized, Is.EqualTo(expected.worldWeather.initialized));
        Assert.That(actual.worldWeather.regions, Has.Count.EqualTo(expected.worldWeather.regions.Count));
        Assert.That(actual.worldWeather.overrides, Has.Count.EqualTo(expected.worldWeather.overrides.Count));
        for (int i = 0; i < expected.worldWeather.regions.Count; i++)
        {
            RegionWeatherSaveEntry left = expected.worldWeather.regions[i];
            RegionWeatherSaveEntry right = actual.worldWeather.regions[i];
            Assert.That(right.regionId, Is.EqualTo(left.regionId));
            Assert.That(right.currentWeatherType, Is.EqualTo(left.currentWeatherType));
            Assert.That(right.currentWeatherStartMinute, Is.EqualTo(left.currentWeatherStartMinute));
            Assert.That(right.historyAvailableFromMinute, Is.EqualTo(left.historyAvailableFromMinute));
            Assert.That(right.rootSeed, Is.EqualTo(left.rootSeed));
            Assert.That(right.history, Has.Count.EqualTo(left.history.Count));
        }
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
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' on '{target.GetType().Name}'.");
        method.Invoke(target, arguments);
    }

    private readonly struct RegionSpec
    {
        public readonly string Id;
        public readonly WeatherType InitialWeather;
        public readonly WeatherType? FirstSlotWeather;
        public readonly WeatherType? SecondSlotWeather;

        public RegionSpec(
            string id,
            WeatherType initialWeather,
            WeatherType? firstSlotWeather = null,
            WeatherType? secondSlotWeather = null)
        {
            Id = id;
            InitialWeather = initialWeather;
            FirstSlotWeather = firstSlotWeather;
            SecondSlotWeather = secondSlotWeather;
        }
    }

    private readonly struct WeatherFixture
    {
        public readonly WorldTimeState Time;
        public readonly WorldWeatherState Weather;

        public WeatherFixture(WorldTimeState time, WorldWeatherState weather)
        {
            Time = time;
            Weather = weather;
        }
    }

    private readonly struct PresentationFixture
    {
        public readonly RoomWeatherPresentation Presenter;

        public PresentationFixture(RoomWeatherPresentation presenter)
        {
            Presenter = presenter;
        }
    }
}
