using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ClimateDefinitionTests
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
    public void WeatherTypeOrdinalsAreAppendOnlyApprovedValues()
    {
        Array values = Enum.GetValues(typeof(WeatherType));
        Assert.That(values.Length, Is.EqualTo(WeatherCount));
        for (int i = 0; i < values.Length; i++)
            Assert.That((int)(WeatherType)values.GetValue(i), Is.EqualTo(i));
        Assert.That((int)WeatherType.Clear, Is.EqualTo(0));
        Assert.That((int)WeatherType.Cloudy, Is.EqualTo(1));
        Assert.That((int)WeatherType.Rain, Is.EqualTo(2));
        Assert.That((int)WeatherType.Storm, Is.EqualTo(3));
        Assert.That((int)WeatherType.Fog, Is.EqualTo(4));
        Assert.That(Enum.IsDefined(typeof(WeatherType), (WeatherType)999), Is.False);
        Assert.That(Enum.IsDefined(typeof(WeatherType), -1), Is.False);
    }

    [Test]
    public void WeatherConfigRequiresNonemptySortedUniqueInRangeSlots()
    {
        WeatherSimulationConfig config = CreateWeatherConfig();
        Assert.That(config.TryValidate(out string error), Is.True, error);

        SetPrivateField(config, "weatherSlotHours", new List<int> { 6, 6 });
        Assert.That(config.TryValidate(out _), Is.False);

        SetPrivateField(config, "weatherSlotHours", new List<int> { 18, 6 });
        Assert.That(config.TryValidate(out _), Is.False);

        SetPrivateField(config, "weatherSlotHours", new List<int> { -1, 6 });
        Assert.That(config.TryValidate(out _), Is.False);

        SetPrivateField(config, "weatherSlotHours", new List<int> { 6, 24 });
        Assert.That(config.TryValidate(out _), Is.False);

        SetPrivateField(config, "weatherSlotHours", new List<int>());
        Assert.That(config.TryValidate(out _), Is.False);

        SetPrivateField(config, "weatherSlotHours", null);
        Assert.That(config.TryValidate(out _), Is.False);
    }

    [Test]
    public void WeatherConfigRequiresExactlyOneDefinitionPerEnumAndLooksUpByType()
    {
        WeatherSimulationConfig config = CreateWeatherConfig();

        Assert.That(config.TryGetDefinition(WeatherType.Rain, out WeatherDefinition rain), Is.True);
        Assert.That(rain.Type, Is.EqualTo(WeatherType.Rain));
        Assert.That(config.TryGetDefinition((WeatherType)999, out _), Is.False);

        List<WeatherDefinition> duplicate = new List<WeatherDefinition>(config.WeatherDefinitions);
        duplicate.Add(config.WeatherDefinitions[0]);
        SetPrivateField(config, "weatherDefinitions", duplicate);
        Assert.That(config.TryValidate(out _), Is.False, "Duplicate weather definitions must fail closed.");
        Assert.That(config.TryGetDefinition(WeatherType.Clear, out _), Is.False);

        List<WeatherDefinition> missing = new List<WeatherDefinition>(config.WeatherDefinitions);
        missing.RemoveAll(definition => definition.Type == WeatherType.Clear);
        SetPrivateField(config, "weatherDefinitions", missing);
        Assert.That(config.TryValidate(out _), Is.False, "Missing weather definitions must fail closed.");
        Assert.That(config.TryGetDefinition(WeatherType.Clear, out _), Is.False);
    }

    [Test]
    public void SeasonMatrixRequiresExactDimensionsAndEveryPositiveBoundedRow()
    {
        SeasonDefinition season = CreateSeason(WeatherType.Cloudy);
        int n = WeatherCount;
        Assert.That(season.TryValidate(out string validError), Is.True, validError);

        SetPrivateField(season, "transitionWeights", new int[n * n - 1]);
        Assert.That(season.TryValidate(out _), Is.False);

        int[] negative = UniformWeights();
        negative[(2 * n) + 3] = -1;
        SetPrivateField(season, "transitionWeights", negative);
        Assert.That(season.TryValidate(out _), Is.False);

        int[] zeroRow = UniformWeights();
        for (int to = 0; to < n; to++)
            zeroRow[(int)WeatherType.Storm * n + to] = 0;
        SetPrivateField(season, "transitionWeights", zeroRow);
        Assert.That(season.TryValidate(out _), Is.False);

        int[] excessiveRow = UniformWeights();
        excessiveRow[0] = int.MaxValue;
        excessiveRow[1] = int.MaxValue;
        excessiveRow[2] = int.MaxValue;
        SetPrivateField(season, "transitionWeights", excessiveRow);
        Assert.That(season.TryValidate(out _), Is.False, "Row totals above UInt32 must be rejected using wide arithmetic.");

        SetPrivateField(season, "transitionWeights", UniformWeights());
        SetPrivateField(season, "entryWeatherType", (WeatherType)100);
        Assert.That(season.TryValidate(out _), Is.False);
    }

    [Test]
    public void SeasonMatrixUsesFromRowToColumnIndexing()
    {
        SeasonDefinition season = CreateSeason(WeatherType.Clear);
        int n = WeatherCount;
        int[] weights = new int[n * n];
        for (int from = 0; from < n; from++)
        {
            for (int to = 0; to < n; to++)
                weights[from * n + to] = from * 100 + to + 1;
        }

        SetPrivateField(season, "transitionWeights", weights);
        Assert.That(season.TryValidate(out string error), Is.True, error);
        Assert.That(season.TryGetTransitionWeight(WeatherType.Rain, WeatherType.Fog, out int weight), Is.True);
        Assert.That(weight, Is.EqualTo((int)WeatherType.Rain * 100 + (int)WeatherType.Fog + 1));
        Assert.That(season.TryGetTransitionWeight((WeatherType)100, WeatherType.Fog, out _), Is.False);
        Assert.That(season.TryGetTransitionWeight(WeatherType.Fog, (WeatherType)100, out _), Is.False);
    }

    [Test]
    public void FixedWeatherSlotsRequireOneBasedDaysConcreteSlotsValidTypesAndUniqueKeys()
    {
        SeasonDefinition season = CreateSeason(WeatherType.Clear);
        FixedWeatherSlot valid = new FixedWeatherSlot(1, 0, WeatherType.Rain);
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot> { valid });
        Assert.That(season.TryValidate(28, 2, out string validError), Is.True, validError);
        Assert.That(season.TryGetFixedWeather(1, 0, out WeatherType fixedType), Is.True);
        Assert.That(fixedType, Is.EqualTo(WeatherType.Rain));

        FixedWeatherSlot zeroDay = new FixedWeatherSlot(0, 0, WeatherType.Rain);
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot> { zeroDay });
        Assert.That(season.TryValidate(28, 2, out _), Is.False);

        FixedWeatherSlot tooLate = new FixedWeatherSlot(29, 0, WeatherType.Rain);
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot> { tooLate });
        Assert.That(season.TryValidate(28, 2, out _), Is.False);

        FixedWeatherSlot magicAllSlots = new FixedWeatherSlot(1, -1, WeatherType.Rain);
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot> { magicAllSlots });
        Assert.That(season.TryValidate(28, 2, out _), Is.False);

        FixedWeatherSlot invalidType = new FixedWeatherSlot(1, 0, (WeatherType)100);
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot> { invalidType });
        Assert.That(season.TryValidate(28, 2, out _), Is.False);

        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>
        {
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 0, WeatherType.Storm)
        });
        Assert.That(season.TryValidate(28, 2, out _), Is.False, "Duplicate day/slot keys must fail closed.");

        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>
        {
            new FixedWeatherSlot(1, 2, WeatherType.Rain)
        });
        Assert.That(season.TryValidate(28, 2, out _), Is.False);
    }

    [Test]
    public void SeasonTrackMatchesCalendarSeasonCountAndHasSafeOrdinalLookup()
    {
        SeasonTrackDefinition track = CreateTrack(4);
        SeasonDefinition season0 = track.Seasons[0];
        SeasonDefinition season1 = track.Seasons[1];
        SeasonDefinition season2 = track.Seasons[2];
        SeasonDefinition season3 = track.Seasons[3];
        Assert.That(track.TryValidate(4, out string error), Is.True, error);
        Assert.That(track.TryGetSeason(0, out SeasonDefinition first), Is.True);
        Assert.That(first.EntryWeatherType, Is.EqualTo(WeatherType.Clear));
        Assert.That(track.TryGetSeason(3, out _), Is.True);
        Assert.That(track.TryGetSeason(-1, out _), Is.False);
        Assert.That(track.TryGetSeason(4, out _), Is.False);

        SetPrivateField(track, "seasons", new List<SeasonDefinition> { season0, season1 });
        Assert.That(track.TryValidate(4, out _), Is.False);

        SetPrivateField(track, "seasons", new List<SeasonDefinition> { season0, null, season2, season3 });
        Assert.That(track.TryValidate(4, out _), Is.False);
        Assert.That(track.TryGetSeason(1, out _), Is.False);

        SetPrivateField(track, "seasons", null);
        Assert.That(track.TryValidate(4, out _), Is.False);
        Assert.That(track.TryGetSeason(0, out _), Is.False);
    }

    [Test]
    public void RegionIdsAreNonemptyAndCatalogLookupIsCaseSensitiveStableAndFailClosedOnDuplicates()
    {
        SeasonTrackDefinition track = CreateTrack(2);
        ClimateRegionDefinition first = CreateRegion("region_A", track, WeatherType.Clear);
        ClimateRegionDefinition second = CreateRegion("region_B", track, WeatherType.Rain);
        ClimateRegionCatalog catalog = Create<ClimateRegionCatalog>();
        SetPrivateField(catalog, "regions", new List<ClimateRegionDefinition> { first, second });

        Assert.That(catalog.TryValidate(2, out string validError), Is.True, validError);
        Assert.That(catalog.TryValidate(0, out _), Is.False);
        Assert.That(catalog.TryGetRegion("region_A", out ClimateRegionDefinition found), Is.True);
        Assert.That(found, Is.SameAs(first));
        Assert.That(catalog.TryGetRegion("REGION_A", out _), Is.False);
        Assert.That(catalog.TryGetRegion("missing", out _), Is.False);
        Assert.That(catalog.Regions[0], Is.SameAs(first));
        Assert.That(catalog.Regions[1], Is.SameAs(second));

        ClimateRegionDefinition duplicate = CreateRegion("region_A", track, WeatherType.Storm);
        SetPrivateField(catalog, "regions", new List<ClimateRegionDefinition> { first, duplicate });
        Assert.That(catalog.TryValidate(out _), Is.False);
        Assert.That(catalog.TryGetRegion("region_A", out _), Is.False, "Duplicate IDs must fail closed.");

        ClimateRegionDefinition nullRegion = null;
        SetPrivateField(catalog, "regions", new List<ClimateRegionDefinition> { first, nullRegion });
        Assert.That(catalog.TryValidate(out _), Is.False);
        Assert.That(catalog.TryGetRegion("region_A", out _), Is.False);

        ClimateRegionDefinition emptyId = CreateRegion("", track, WeatherType.Clear);
        SetPrivateField(catalog, "regions", new List<ClimateRegionDefinition> { emptyId });
        Assert.That(catalog.TryValidate(out _), Is.False);
    }

    [Test]
    public void WeatherSnapshotIsImmutableAndComputesDurationFromCurrentMinute()
    {
        WeatherSnapshot snapshot = WeatherSnapshot.FromCurrentMinute(
            "region_A",
            WeatherType.Rain,
            100L,
            145L);

        Assert.That(snapshot.RegionId, Is.EqualTo("region_A"));
        Assert.That(snapshot.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(snapshot.SinceMinute, Is.EqualTo(100L));
        Assert.That(snapshot.DurationSoFarMinutes, Is.EqualTo(45L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeatherSnapshot.FromCurrentMinute("region_A", WeatherType.Rain, 100L, 99L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherSnapshot("region_A", (WeatherType)999, 0L, 0L));
        Assert.Throws<ArgumentException>(() =>
            new WeatherSnapshot(" ", WeatherType.Clear, 0L, 0L));
    }

    private WeatherSimulationConfig CreateWeatherConfig()
    {
        WeatherSimulationConfig config = Create<WeatherSimulationConfig>();
        List<WeatherDefinition> definitions = new List<WeatherDefinition>();
        for (int i = 0; i < WeatherCount; i++)
        {
            WeatherDefinition definition = Create<WeatherDefinition>();
            SetPrivateField(definition, "type", (WeatherType)i);
            SetPrivateField(definition, "displayName", ((WeatherType)i).ToString());
            definitions.Add(definition);
        }

        SetPrivateField(config, "weatherSlotHours", new List<int> { 6, 18 });
        SetPrivateField(config, "maxForecastDaysAhead", 28);
        SetPrivateField(config, "weatherDefinitions", definitions);
        return config;
    }

    private SeasonDefinition CreateSeason(WeatherType entryWeather)
    {
        SeasonDefinition season = Create<SeasonDefinition>();
        SetPrivateField(season, "seasonId", "season_fixture");
        SetPrivateField(season, "displayName", "Fixture Season");
        SetPrivateField(season, "entryWeatherType", entryWeather);
        SetPrivateField(season, "transitionWeights", UniformWeights());
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>());
        return season;
    }

    private SeasonTrackDefinition CreateTrack(int count)
    {
        SeasonTrackDefinition track = Create<SeasonTrackDefinition>();
        List<SeasonDefinition> seasons = new List<SeasonDefinition>();
        for (int i = 0; i < count; i++)
            seasons.Add(CreateSeason((WeatherType)(i % WeatherCount)));
        SetPrivateField(track, "seasons", seasons);
        return track;
    }

    private ClimateRegionDefinition CreateRegion(
        string id,
        SeasonTrackDefinition track,
        WeatherType initialWeather)
    {
        ClimateRegionDefinition region = Create<ClimateRegionDefinition>();
        SetPrivateField(region, "regionId", id);
        SetPrivateField(region, "displayName", id);
        SetPrivateField(region, "seasonTrack", track);
        SetPrivateField(region, "initialWeatherType", initialWeather);
        return region;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(instance);
        return instance;
    }

    private static int[] UniformWeights()
    {
        int[] weights = new int[WeatherCount * WeatherCount];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = 1;
        return weights;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
        field.SetValue(target, value);

        MethodInfo invalidateLookup = target.GetType().GetMethod(
            "InvalidateLookup",
            BindingFlags.Instance | BindingFlags.NonPublic);
        invalidateLookup?.Invoke(target, null);
    }
}
