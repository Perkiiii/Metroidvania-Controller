using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldWeatherPersistenceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        createdObjects.Clear();
    }

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
    public void FreshCompletionCreatesEveryRegionWithAuthoredWeatherAndPersistentSeeds()
    {
        ClimateFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(123L);
        WorldWeatherState state = CreateWeatherState(fixture);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.ActiveRegionCount, Is.EqualTo(2));
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot regionA), Is.True);
        Assert.That(regionA.WeatherType, Is.EqualTo(WeatherType.Clear));
        Assert.That(regionA.SinceMinute, Is.EqualTo(123L));
        Assert.That(regionA.DurationSoFarMinutes, Is.Zero);
        Assert.That(state.TryGetWeather("region_b", out WeatherSnapshot regionB), Is.True);
        Assert.That(regionB.WeatherType, Is.EqualTo(WeatherType.Rain));

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        Assert.That(gathered.worldWeather.initialized, Is.True);
        Assert.That(gathered.worldWeather.regions, Has.Count.EqualTo(2));
        Assert.That(gathered.worldWeather.regions[0].rootSeed, Is.Not.EqualTo(0u));
        Assert.That(gathered.worldWeather.regions[1].rootSeed, Is.Not.EqualTo(0u));
        Assert.That(gathered.worldWeather.regions[0].rootSeed,
            Is.Not.EqualTo(gathered.worldWeather.regions[1].rootSeed));
        Assert.That(gathered.worldWeather.regions[0].currentWeatherType, Is.EqualTo((int)WeatherType.Clear));
        Assert.That(gathered.worldWeather.regions[1].currentWeatherType, Is.EqualTo((int)WeatherType.Rain));

        WorldWeatherState destination = CreateWeatherState(fixture);
        WorldTimeState destinationTime = CreateWorldTime(123L);
        destination.ApplySaveData(gathered);
        destination.CompleteLoad(destinationTime);

        SaveData roundTripped = new SaveData();
        destination.GatherSaveData(roundTripped);
        Assert.That(roundTripped.worldWeather.regions, Has.Count.EqualTo(2));
        Assert.That(roundTripped.worldWeather.regions[0].rootSeed,
            Is.EqualTo(gathered.worldWeather.regions[0].rootSeed));
        Assert.That(roundTripped.worldWeather.regions[1].rootSeed,
            Is.EqualTo(gathered.worldWeather.regions[1].rootSeed));
    }

    [Test]
    public void CurrentWeatherSurvivesTimeAdvancementAndOnlyDurationChanges()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Cloudy));
        SetAllSeasonTransitionMatrix(fixture, MatrixThatAlwaysSelects(WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(240L);
        WorldWeatherState state = CreateWeatherState(fixture);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        Assert.That(state.TryGetWeather("region_a", out _), Is.True);

        time.AdvanceMinutes(180L);

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot after), Is.True);
        Assert.That(after.WeatherType, Is.EqualTo(WeatherType.Clear));
        Assert.That(after.SinceMinute, Is.EqualTo(360L));
        Assert.That(after.DurationSoFarMinutes, Is.EqualTo(60L));
    }

    [Test]
    public void ProgressionResolvesSixAndEighteenAndClosesHistoryAtExactBoundaries()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(
            fixture,
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        List<WeatherSnapshot> changes = new List<WeatherSnapshot>();
        state.WeatherChanged += changes.Add;
        time.AdvanceMinutes(840L);

        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[0].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[0].SinceMinute, Is.EqualTo(360L));
        Assert.That(changes[1].WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(changes[1].SinceMinute, Is.EqualTo(1080L));

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Storm));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(1080L));
        Assert.That(region.history, Has.Count.EqualTo(2));
        Assert.That(region.history[0].weatherType, Is.EqualTo((int)WeatherType.Clear));
        Assert.That(region.history[0].startMinute, Is.EqualTo(300L));
        Assert.That(region.history[0].endMinuteExclusive, Is.EqualTo(360L));
        Assert.That(region.history[1].weatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(region.history[1].startMinute, Is.EqualTo(360L));
        Assert.That(region.history[1].endMinuteExclusive, Is.EqualTo(1080L));
    }

    [Test]
    public void SkipResolvesEveryConfiguredSlotForEachRegionInCatalogOrder()
    {
        ClimateFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Storm));
        SetSlotHours(fixture, 6, 12, 18);
        SetAllSeasonFixedSlots(
            fixture,
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Fog),
            new FixedWeatherSlot(1, 2, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(0L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        List<string> order = new List<string>();
        state.WeatherChanged += snapshot => order.Add(snapshot.RegionId + ":" + snapshot.SinceMinute);
        time.AdvanceMinutes(1140L);

        Assert.That(order, Is.EqualTo(new[]
        {
            "region_a:360", "region_b:360",
            "region_a:720", "region_b:720",
            "region_a:1080", "region_b:1080"
        }));
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot regionA), Is.True);
        Assert.That(regionA.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(state.TryGetWeather("region_b", out WeatherSnapshot regionB), Is.True);
        Assert.That(regionB.WeatherType, Is.EqualTo(WeatherType.Storm));
    }

    [Test]
    public void FreshRegionUsesActiveSlotAtLoadAndKeepsAvailabilityAtLoadedMinute()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 0, WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(390L);
        WorldWeatherState state = CreateWeatherState(fixture);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot snapshot), Is.True);
        Assert.That(snapshot.WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(snapshot.SinceMinute, Is.EqualTo(390L));
        Assert.That(snapshot.DurationSoFarMinutes, Is.Zero);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(390L));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(390L));
        Assert.That(region.history, Is.Empty);
    }

    [Test]
    public void FreshRegionUsesPriorDayActiveSlotBeforeFirstSlotOfLoadedDay()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 1, WeatherType.Storm));
        WorldTimeState time = CreateWorldTime(CalendarConfig.MinutesPerDay);
        WorldWeatherState state = CreateWeatherState(fixture);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot snapshot), Is.True);
        Assert.That(snapshot.WeatherType, Is.EqualTo(WeatherType.Storm));
        Assert.That(snapshot.SinceMinute, Is.EqualTo(CalendarConfig.MinutesPerDay));
    }

    [Test]
    public void SavedOverrideBeatsFixedSlotWhichBeatsGeneratedWeather()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonTransitionMatrix(fixture, MatrixThatAlwaysSelects(WeatherType.Storm));
        SetAllSeasonFixedSlots(
            fixture,
            new FixedWeatherSlot(1, 0, WeatherType.Rain),
            new FixedWeatherSlot(1, 1, WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(0L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>(),
                overrides = new List<WeatherOverrideSaveEntry>
                {
                    new WeatherOverrideSaveEntry
                    {
                        regionId = "region_a",
                        absoluteDayIndex = 0L,
                        slotIndex = 0,
                        weatherType = (int)WeatherType.Fog
                    }
                }
            }
        });
        state.CompleteLoad(time);

        List<WeatherSnapshot> changes = new List<WeatherSnapshot>();
        state.WeatherChanged += changes.Add;
        time.AdvanceMinutes(1080L);

        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[0].WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(changes[1].WeatherType, Is.EqualTo(WeatherType.Rain));
        Assert.That(changes[0].SinceMinute, Is.EqualTo(360L));
        Assert.That(changes[1].SinceMinute, Is.EqualTo(1080L));
    }

    [Test]
    public void SameWeatherSlotIsNoOpWithoutEventOrHistoryMutation()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 0, WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        int eventCount = 0;
        state.WeatherChanged += _ => eventCount++;
        time.AdvanceMinutes(60L);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(eventCount, Is.Zero);
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(300L));
        Assert.That(region.history, Is.Empty);
    }

    [Test]
    public void ClosingHistoryMergesAdjacentCompatibleSavedSegment()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Rain));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 1, WeatherType.Fog));
        WorldTimeState time = CreateWorldTime(600L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Rain,
                        currentWeatherStartMinute = 360L,
                        historyAvailableFromMinute = 0L,
                        rootSeed = 0x12345678u,
                        history = new List<WeatherHistoryRecordSaveEntry>
                        {
                            new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = (int)WeatherType.Rain,
                                startMinute = 0L,
                                endMinuteExclusive = 360L
                            }
                        }
                    }
                }
            }
        });
        state.CompleteLoad(time);
        time.AdvanceMinutes(480L);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.history, Has.Count.EqualTo(1));
        Assert.That(region.history[0].weatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(region.history[0].startMinute, Is.EqualTo(0L));
        Assert.That(region.history[0].endMinuteExclusive, Is.EqualTo(1080L));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(1080L));
    }

    [Test]
    public void ProgressedWeatherHistoryAndSeedSurviveSaveReload()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 0, WeatherType.Rain));
        WorldTimeState time = CreateWorldTime(300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);
        time.AdvanceMinutes(180L);

        SaveData saved = new SaveData();
        state.GatherSaveData(saved);
        uint seed = saved.worldWeather.regions[0].rootSeed;

        WorldTimeState destinationTime = CreateWorldTime(480L);
        WorldWeatherState destination = CreateWeatherState(fixture);
        destination.ApplySaveData(saved);
        destination.CompleteLoad(destinationTime);

        SaveData reloaded = new SaveData();
        destination.GatherSaveData(reloaded);
        RegionWeatherSaveEntry region = reloaded.worldWeather.regions[0];
        Assert.That(region.rootSeed, Is.EqualTo(seed));
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(360L));
        Assert.That(region.history, Has.Count.EqualTo(1));
        Assert.That(region.history[0].endMinuteExclusive, Is.EqualTo(360L));
    }

    [Test]
    public void ApplyDetachesOldClockAndRepeatedCompletionDoesNotDuplicateBinding()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 0, WeatherType.Rain));
        WorldTimeState oldTime = CreateWorldTime(0L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(oldTime);

        state.ApplySaveData(new SaveData());
        oldTime.AdvanceMinutes(360L);
        Assert.That(state.IsLoaded, Is.False);

        WorldTimeState newTime = CreateWorldTime(300L);
        state.CompleteLoad(newTime);
        state.CompleteLoad(newTime);
        state.CompleteLoad(newTime);
        int eventCount = 0;
        state.WeatherChanged += _ => eventCount++;
        newTime.AdvanceMinutes(60L);

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot snapshot), Is.True);
        Assert.That(snapshot.WeatherType, Is.EqualTo(WeatherType.Rain));
    }

    [Test]
    public void CatalogRecompletionUsesLatestRuntimeStateAndAcceptedOverrides()
    {
        ClimateFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Clear));
        SetAllSeasonFixedSlots(fixture, new FixedWeatherSlot(1, 0, WeatherType.Rain));
        ClimateRegionDefinition regionA = fixture.Catalog.Regions[0];
        ClimateRegionDefinition regionB = fixture.Catalog.Regions[1];
        SetCatalogRegions(fixture.Catalog, new[] { regionA });

        WorldTimeState time = CreateWorldTime(300L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Clear,
                        currentWeatherStartMinute = 300L,
                        historyAvailableFromMinute = 300L,
                        rootSeed = 0x12345678u,
                        history = new List<WeatherHistoryRecordSaveEntry>()
                    }
                },
                overrides = new List<WeatherOverrideSaveEntry>
                {
                    new WeatherOverrideSaveEntry
                    {
                        regionId = "region_a",
                        absoluteDayIndex = 0L,
                        slotIndex = 0,
                        weatherType = (int)WeatherType.Fog,
                        sourceTag = "accepted"
                    }
                }
            }
        });
        state.CompleteLoad(time);

        time.AdvanceMinutes(60L);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot progressed), Is.True);
        Assert.That(progressed.WeatherType, Is.EqualTo(WeatherType.Fog));
        Assert.That(progressed.SinceMinute, Is.EqualTo(360L));

        SetCatalogRegions(fixture.Catalog, new[] { regionA, regionB });
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        Assert.That(gathered.worldWeather.regions, Has.Count.EqualTo(2));
        RegionWeatherSaveEntry persistedA = gathered.worldWeather.regions[0];
        Assert.That(persistedA.regionId, Is.EqualTo("region_a"));
        Assert.That(persistedA.currentWeatherType, Is.EqualTo((int)WeatherType.Fog));
        Assert.That(persistedA.currentWeatherStartMinute, Is.EqualTo(360L));
        Assert.That(persistedA.history, Has.Count.EqualTo(1));
        Assert.That(persistedA.history[0].weatherType, Is.EqualTo((int)WeatherType.Clear));
        Assert.That(persistedA.history[0].startMinute, Is.EqualTo(300L));
        Assert.That(persistedA.history[0].endMinuteExclusive, Is.EqualTo(360L));
        Assert.That(persistedA.rootSeed, Is.EqualTo(0x12345678u));
        Assert.That(gathered.worldWeather.overrides, Has.Count.EqualTo(1));
        Assert.That(gathered.worldWeather.overrides[0].sourceTag, Is.EqualTo("accepted"));

        RegionWeatherSaveEntry persistedB = gathered.worldWeather.regions[1];
        Assert.That(persistedB.regionId, Is.EqualTo("region_b"));
        Assert.That(persistedB.currentWeatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(persistedB.currentWeatherStartMinute, Is.EqualTo(360L));
    }

    [Test]
    public void ApplyAndGatherDeepCopyCurrentStateHistoryAndOverrides()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        WorldWeatherSaveData sourceWeather = new WorldWeatherSaveData
        {
            initialized = true,
            regions = new List<RegionWeatherSaveEntry>
            {
                new RegionWeatherSaveEntry
                {
                    regionId = "region_a",
                    currentWeatherType = (int)WeatherType.Storm,
                    currentWeatherStartMinute = 80L,
                    historyAvailableFromMinute = 20L,
                    rootSeed = 0xF1234567u,
                    history = new List<WeatherHistoryRecordSaveEntry>
                    {
                        new WeatherHistoryRecordSaveEntry
                        {
                            weatherType = (int)WeatherType.Rain,
                            startMinute = 20L,
                            endMinuteExclusive = 80L
                        }
                    }
                }
            },
            overrides = new List<WeatherOverrideSaveEntry>
            {
                new WeatherOverrideSaveEntry
                {
                    regionId = "region_a",
                    absoluteDayIndex = 1L,
                    slotIndex = 0,
                    weatherType = (int)WeatherType.Fog,
                    sourceTag = "test"
                }
            }
        };
        SaveData source = new SaveData { worldWeather = sourceWeather };

        state.ApplySaveData(source);
        sourceWeather.regions[0].currentWeatherType = (int)WeatherType.Clear;
        sourceWeather.regions[0].history[0].endMinuteExclusive = 25L;
        sourceWeather.overrides[0].sourceTag = "mutated";
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Storm));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(80L));
        Assert.That(region.history, Has.Count.EqualTo(1));
        Assert.That(region.history[0].endMinuteExclusive, Is.EqualTo(80L));
        Assert.That(region.rootSeed, Is.EqualTo(0xF1234567u));
        Assert.That(gathered.worldWeather.overrides, Has.Count.EqualTo(1));
        Assert.That(gathered.worldWeather.overrides[0].sourceTag, Is.EqualTo("test"));
    }

    [Test]
    public void CompleteLoadRequiresLoadedWorldTime()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTimeWithoutApplying();
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());

        Assert.Throws<InvalidOperationException>(() => state.CompleteLoad(time));
        Assert.That(state.IsLoaded, Is.False);
    }

    [Test]
    public void CompleteLoadIsIdempotentAndDoesNotRegenerateSeed()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(50L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        SaveData first = new SaveData();
        state.GatherSaveData(first);
        uint seed = first.worldWeather.regions[0].rootSeed;

        time.AdvanceMinutes(75L);
        state.CompleteLoad(time);

        SaveData second = new SaveData();
        state.GatherSaveData(second);
        Assert.That(second.worldWeather.regions[0].rootSeed, Is.EqualTo(seed));
        Assert.That(second.worldWeather.regions[0].currentWeatherStartMinute, Is.EqualTo(50L));
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot snapshot), Is.True);
        Assert.That(snapshot.DurationSoFarMinutes, Is.EqualTo(75L));
    }

    [Test]
    public void AddedCatalogRegionSelfHealsAndUnknownSavedRegionIsDiscarded()
    {
        ClimateFixture destinationFixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Fog));
        WorldTimeState time = CreateWorldTime(300L);
        WorldWeatherState state = CreateWeatherState(destinationFixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Storm,
                        currentWeatherStartMinute = 200L,
                        historyAvailableFromMinute = 200L,
                        rootSeed = 0x12345678u,
                        history = new List<WeatherHistoryRecordSaveEntry>()
                    },
                    new RegionWeatherSaveEntry
                    {
                        regionId = "removed_region",
                        currentWeatherType = (int)WeatherType.Rain,
                        rootSeed = 0xABCDEF01u
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("removed_region.*not in the current catalog"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        Assert.That(gathered.worldWeather.regions, Has.Count.EqualTo(2));
        Assert.That(gathered.worldWeather.regions[0].regionId, Is.EqualTo("region_a"));
        Assert.That(gathered.worldWeather.regions[0].rootSeed, Is.EqualTo(0x12345678u));
        Assert.That(gathered.worldWeather.regions[1].regionId, Is.EqualTo("region_b"));
        Assert.That(gathered.worldWeather.regions[1].currentWeatherType, Is.EqualTo((int)WeatherType.Fog));
        Assert.That(gathered.worldWeather.regions[1].currentWeatherStartMinute, Is.EqualTo(300L));
        Assert.That(state.TryGetWeather("removed_region", out _), Is.False);
    }

    [Test]
    public void RemovedRegionIsNotResurrectedWhenCatalogAddsItBack()
    {
        ClimateFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Fog));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);

        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        ClimateRegionDefinition regionA = fixture.Catalog.Regions[0];
        ClimateRegionDefinition regionB = fixture.Catalog.Regions[1];
        SetCatalogRegions(fixture.Catalog, new[] { regionA });
        LogAssert.Expect(LogType.Warning, new Regex("region_b.*not in the current catalog"));
        state.CompleteLoad(time);
        Assert.That(state.ActiveRegionCount, Is.EqualTo(1));

        time.AdvanceMinutes(50L);
        SetCatalogRegions(fixture.Catalog, new[] { regionA, regionB });
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry readded = gathered.worldWeather.regions[1];
        Assert.That(readded.regionId, Is.EqualTo("region_b"));
        Assert.That(readded.currentWeatherStartMinute, Is.EqualTo(150L));
        Assert.That(readded.historyAvailableFromMinute, Is.EqualTo(150L));
    }

    [Test]
    public void DuplicateSavedRegionUsesFirstEntryDeterministically()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Rain,
                        currentWeatherStartMinute = 40L,
                        historyAvailableFromMinute = 40L,
                        rootSeed = 0x11111111u
                    },
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Storm,
                        currentWeatherStartMinute = 70L,
                        historyAvailableFromMinute = 70L,
                        rootSeed = 0x22222222u
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("Duplicate saved weather region 'region_a'.*first entry wins"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        Assert.That(gathered.worldWeather.regions, Has.Count.EqualTo(1));
        Assert.That(gathered.worldWeather.regions[0].currentWeatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(gathered.worldWeather.regions[0].rootSeed, Is.EqualTo(0x11111111u));
    }

    [Test]
    public void InvalidWeatherAndFutureTimestampsNormalizeSafely()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Cloudy));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = 99,
                        currentWeatherStartMinute = 200L,
                        historyAvailableFromMinute = 300L,
                        rootSeed = 0x01020304u
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("invalid weather value 99"));
        LogAssert.Expect(LogType.Warning, new Regex("currentWeatherStartMinute.*clamped"));
        LogAssert.Expect(LogType.Warning, new Regex("historyAvailableFromMinute.*clamped"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Cloudy));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(100L));
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(100L));
    }

    [Test]
    public void NegativeRegionTimestampsResetWeatherBoundaryToLoadedMinute()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Storm,
                        currentWeatherStartMinute = -1L,
                        historyAvailableFromMinute = -2L,
                        rootSeed = 0x10203040u,
                        history = new List<WeatherHistoryRecordSaveEntry>
                        {
                            new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = (int)WeatherType.Rain,
                                startMinute = 10L,
                                endMinuteExclusive = 90L
                            }
                        }
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("negative timestamp.*resetting"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Storm));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(100L));
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(100L));
        Assert.That(region.history, Is.Empty);
        Assert.That(region.rootSeed, Is.EqualTo(0x10203040u));
    }

    [Test]
    public void NegativeHistoryRecordTimestampResetsWeatherBoundaryToLoadedMinute()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Rain,
                        currentWeatherStartMinute = 90L,
                        historyAvailableFromMinute = 10L,
                        rootSeed = 0x50607080u,
                        history = new List<WeatherHistoryRecordSaveEntry>
                        {
                            new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = (int)WeatherType.Clear,
                                startMinute = -1L,
                                endMinuteExclusive = 90L
                            }
                        }
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("negative timestamp.*resetting"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.currentWeatherType, Is.EqualTo((int)WeatherType.Rain));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(100L));
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(100L));
        Assert.That(region.history, Is.Empty);
        Assert.That(region.rootSeed, Is.EqualTo(0x50607080u));
    }

    [Test]
    public void InvalidOverlappingHistoryResetsKnownBoundaryToLoadedMinute()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Rain,
                        currentWeatherStartMinute = 90L,
                        historyAvailableFromMinute = 10L,
                        rootSeed = 0xABCDEF01u,
                        history = new List<WeatherHistoryRecordSaveEntry>
                        {
                            new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = (int)WeatherType.Clear,
                                startMinute = 10L,
                                endMinuteExclusive = 60L
                            },
                            new WeatherHistoryRecordSaveEntry
                            {
                                weatherType = (int)WeatherType.Storm,
                                startMinute = 50L,
                                endMinuteExclusive = 90L
                            }
                        }
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("invalid or overlapping history"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        RegionWeatherSaveEntry region = gathered.worldWeather.regions[0];
        Assert.That(region.history, Is.Empty);
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(100L));
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(100L));
    }

    [Test]
    public void InvalidOverridesAreDiscardedWithoutAffectingCurrentWeather()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldTimeState time = CreateWorldTime(100L);
        WorldWeatherState state = CreateWeatherState(fixture);
        state.ApplySaveData(new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>(),
                overrides = new List<WeatherOverrideSaveEntry>
                {
                    new WeatherOverrideSaveEntry
                    {
                        regionId = "region_a",
                        absoluteDayIndex = -1L,
                        slotIndex = 0,
                        weatherType = (int)WeatherType.Rain
                    },
                    new WeatherOverrideSaveEntry
                    {
                        regionId = "unknown",
                        absoluteDayIndex = 1L,
                        slotIndex = 0,
                        weatherType = (int)WeatherType.Rain
                    },
                    new WeatherOverrideSaveEntry
                    {
                        regionId = "region_a",
                        absoluteDayIndex = 1L,
                        slotIndex = 0,
                        weatherType = 99
                    }
                }
            }
        });

        LogAssert.Expect(LogType.Warning, new Regex("malformed saved weather override"));
        LogAssert.Expect(LogType.Warning, new Regex("malformed saved weather override"));
        LogAssert.Expect(LogType.Warning, new Regex("malformed saved weather override"));
        state.CompleteLoad(time);

        SaveData gathered = new SaveData();
        state.GatherSaveData(gathered);
        Assert.That(gathered.worldWeather.overrides, Is.Empty);
        Assert.That(state.TryGetWeather("region_a", out WeatherSnapshot snapshot), Is.True);
        Assert.That(snapshot.WeatherType, Is.EqualTo(WeatherType.Clear));
    }

    [Test]
    public void ApplyCanRunBeforeOrAfterWorldTimeAndCompletionProducesEquivalentState()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Rain));
        WorldTimeState firstTime = CreateWorldTimeWithoutApplying();
        WorldTimeState secondTime = CreateWorldTimeWithoutApplying();

        WorldWeatherSaveData weather = new WorldWeatherSaveData
        {
            initialized = true,
            regions = new List<RegionWeatherSaveEntry>
            {
                new RegionWeatherSaveEntry
                {
                    regionId = "region_a",
                    currentWeatherType = (int)WeatherType.Storm,
                    currentWeatherStartMinute = 400L,
                    historyAvailableFromMinute = 400L,
                    rootSeed = 0xDEADBEEFu
                }
            }
        };
        WorldWeatherState first = CreateWeatherState(fixture);
        WorldWeatherState second = CreateWeatherState(fixture);

        first.ApplySaveData(new SaveData { worldWeather = weather });
        Assert.That(first.IsLoaded, Is.False,
            "ApplySaveData must only stage a deep copy; completion owns clock-dependent state.");
        firstTime.ApplySaveData(CreateTimeSave(500L));
        secondTime.ApplySaveData(CreateTimeSave(500L));
        second.ApplySaveData(new SaveData { worldWeather = DeepCopyWeather(weather) });
        first.CompleteLoad(firstTime);
        second.CompleteLoad(secondTime);

        SaveData firstGathered = new SaveData();
        SaveData secondGathered = new SaveData();
        first.GatherSaveData(firstGathered);
        second.GatherSaveData(secondGathered);
        Assert.That(secondGathered.worldWeather.regions[0].currentWeatherType,
            Is.EqualTo(firstGathered.worldWeather.regions[0].currentWeatherType));
        Assert.That(secondGathered.worldWeather.regions[0].rootSeed,
            Is.EqualTo(firstGathered.worldWeather.regions[0].rootSeed));
    }

    [Test]
    public void SaveManagerTargetOrderProducesEquivalentCompletedWeatherState()
    {
        ClimateFixture fixture = CreateFixture(
            new RegionSpec("region_a", WeatherType.Clear),
            new RegionSpec("region_b", WeatherType.Fog));
        WorldTimeState firstTime = CreateWorldTimeWithoutApplying();
        WorldTimeState secondTime = CreateWorldTimeWithoutApplying();
        WorldWeatherState firstWeather = CreateWeatherState(fixture);
        WorldWeatherState secondWeather = CreateWeatherState(fixture);
        SaveManager firstManager = CreateSaveManager(firstTime, firstWeather);
        SaveManager secondManager = CreateSaveManager(secondWeather, secondTime);

        ApplyThroughSaveManager(firstManager, CreateTargetOrderSaveData());
        ApplyThroughSaveManager(secondManager, CreateTargetOrderSaveData());

        Assert.That(firstTime.IsLoaded, Is.True);
        Assert.That(secondTime.IsLoaded, Is.True);
        Assert.That(firstWeather.IsLoaded, Is.False,
            "SaveManager.ApplySaveData must not complete weather before the explicit post-load seam.");
        Assert.That(secondWeather.IsLoaded, Is.False,
            "Weather-first target application must remain clock-order independent.");

        firstWeather.CompleteLoad(firstTime);
        secondWeather.CompleteLoad(secondTime);

        SaveData firstGathered = new SaveData();
        SaveData secondGathered = new SaveData();
        firstWeather.GatherSaveData(firstGathered);
        secondWeather.GatherSaveData(secondGathered);

        Assert.That(secondGathered.worldWeather.initialized, Is.EqualTo(firstGathered.worldWeather.initialized));
        Assert.That(secondGathered.worldWeather.regions, Has.Count.EqualTo(firstGathered.worldWeather.regions.Count));
        for (int i = 0; i < firstGathered.worldWeather.regions.Count; i++)
        {
            RegionWeatherSaveEntry firstRegion = firstGathered.worldWeather.regions[i];
            RegionWeatherSaveEntry secondRegion = secondGathered.worldWeather.regions[i];
            Assert.That(secondRegion.regionId, Is.EqualTo(firstRegion.regionId));
            Assert.That(secondRegion.currentWeatherType, Is.EqualTo(firstRegion.currentWeatherType));
            Assert.That(secondRegion.currentWeatherStartMinute, Is.EqualTo(firstRegion.currentWeatherStartMinute));
            Assert.That(secondRegion.historyAvailableFromMinute, Is.EqualTo(firstRegion.historyAvailableFromMinute));
            Assert.That(secondRegion.rootSeed, Is.EqualTo(firstRegion.rootSeed));
            Assert.That(secondRegion.history, Has.Count.EqualTo(firstRegion.history.Count));
        }
    }

    [Test]
    public void JsonUtilityPreservesAllRootSeedBits()
    {
        const uint expectedSeed = 0xF1234567u;
        SaveData source = new SaveData
        {
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        rootSeed = expectedSeed
                    }
                }
            }
        };

        Assert.That(SaveSerializer.TrySerialize(source, out string json), Is.True);
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData destination), Is.True);
        Assert.That(destination.worldWeather, Is.Not.Null);
        Assert.That(destination.worldWeather.regions, Has.Count.EqualTo(1));
        Assert.That(destination.worldWeather.regions[0].rootSeed, Is.EqualTo(expectedSeed));
    }

    [Test]
    public void PreWeatherSaveMigratesToUninitializedClimateStateWithoutHistory()
    {
        SaveData old = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 5 },
            worldWeather = null
        };

        SaveDataMigrator.Migrate(old);

        Assert.That(SaveDataMigrator.CurrentSaveVersion, Is.EqualTo(6));
        Assert.That(old.worldWeather, Is.Not.Null);
        Assert.That(old.worldWeather.initialized, Is.False);
        Assert.That(old.worldWeather.regions, Is.Not.Null);
        Assert.That(old.worldWeather.regions, Is.Empty);
        Assert.That(old.worldWeather.overrides, Is.Not.Null);
        Assert.That(old.worldWeather.overrides, Is.Empty);
    }

    [Test]
    public void CurrentWeatherSaveMigrationNormalizesNullCollectionsAndNestedFields()
    {
        SaveData missingCollections = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 6 },
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = null,
                overrides = null
            }
        };

        SaveDataMigrator.Migrate(missingCollections);

        Assert.That(missingCollections.worldWeather.initialized, Is.True);
        Assert.That(missingCollections.worldWeather.regions, Is.Not.Null);
        Assert.That(missingCollections.worldWeather.regions, Is.Empty);
        Assert.That(missingCollections.worldWeather.overrides, Is.Not.Null);
        Assert.That(missingCollections.worldWeather.overrides, Is.Empty);

        const int savedWeatherType = (int)WeatherType.Storm;
        const long savedStartMinute = 1234L;
        const long savedHistoryBoundary = 1200L;
        const uint savedRootSeed = 0xF1234567u;
        const long savedOverrideDay = 7L;
        const int savedOverrideSlot = 1;
        SaveData nestedNulls = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 6 },
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = null,
                        currentWeatherType = savedWeatherType,
                        currentWeatherStartMinute = savedStartMinute,
                        historyAvailableFromMinute = savedHistoryBoundary,
                        rootSeed = savedRootSeed,
                        history = null
                    }
                },
                overrides = new List<WeatherOverrideSaveEntry>
                {
                    new WeatherOverrideSaveEntry
                    {
                        regionId = null,
                        absoluteDayIndex = savedOverrideDay,
                        slotIndex = savedOverrideSlot,
                        weatherType = (int)WeatherType.Rain,
                        sourceTag = null
                    }
                }
            }
        };

        SaveDataMigrator.Migrate(nestedNulls);

        Assert.That(nestedNulls.worldWeather.initialized, Is.True);
        Assert.That(nestedNulls.worldWeather.regions, Has.Count.EqualTo(1));
        RegionWeatherSaveEntry region = nestedNulls.worldWeather.regions[0];
        Assert.That(region.regionId, Is.EqualTo(string.Empty));
        Assert.That(region.history, Is.Not.Null);
        Assert.That(region.history, Is.Empty);
        Assert.That(region.currentWeatherType, Is.EqualTo(savedWeatherType));
        Assert.That(region.currentWeatherStartMinute, Is.EqualTo(savedStartMinute));
        Assert.That(region.historyAvailableFromMinute, Is.EqualTo(savedHistoryBoundary));
        Assert.That(region.rootSeed, Is.EqualTo(savedRootSeed));

        Assert.That(nestedNulls.worldWeather.overrides, Has.Count.EqualTo(1));
        WeatherOverrideSaveEntry weatherOverride = nestedNulls.worldWeather.overrides[0];
        Assert.That(weatherOverride.regionId, Is.EqualTo(string.Empty));
        Assert.That(weatherOverride.sourceTag, Is.EqualTo(string.Empty));
        Assert.That(weatherOverride.absoluteDayIndex, Is.EqualTo(savedOverrideDay));
        Assert.That(weatherOverride.slotIndex, Is.EqualTo(savedOverrideSlot));
        Assert.That(weatherOverride.weatherType, Is.EqualTo((int)WeatherType.Rain));
    }

    [Test]
    public void UnknownRegionQueryReturnsFalse()
    {
        ClimateFixture fixture = CreateFixture(new RegionSpec("region_a", WeatherType.Clear));
        WorldWeatherState state = CreateWeatherState(fixture);
        WorldTimeState time = CreateWorldTime(0L);
        state.ApplySaveData(new SaveData());
        state.CompleteLoad(time);

        Assert.That(state.TryGetWeather("not_catalogued", out _), Is.False);
        Assert.That(state.TryGetRootSeed("not_catalogued", out _), Is.False);
    }

    private ClimateFixture CreateFixture(params RegionSpec[] regionSpecs)
    {
        WeatherSimulationConfig simulationConfig = Track(Create<WeatherSimulationConfig>());
        List<WeatherDefinition> weatherDefinitions = new List<WeatherDefinition>();
        int weatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
        for (int i = 0; i < weatherTypeCount; i++)
        {
            WeatherDefinition definition = Track(Create<WeatherDefinition>());
            WeatherType type = (WeatherType)i;
            SetWeatherDefinitionContent(definition, type, type.ToString());
            weatherDefinitions.Add(definition);
        }

        SetWeatherSimulationConfigData(simulationConfig, new[] { 6, 18 }, 28, weatherDefinitions);
        CalendarConfig calendarConfig = Track(Create<CalendarConfig>());
        calendarConfig.daysPerSeason = 28;
        calendarConfig.seasonsPerYear = 4;
        calendarConfig.initialYear = 1;
        calendarConfig.initialSeasonOrdinal = 0;
        calendarConfig.initialDayInSeason = 1;
        calendarConfig.initialHour = 0;
        calendarConfig.initialMinute = 0;
        calendarConfig.realSecondsPerGameMinute = 1f;

        SeasonTrackDefinition track = Track(Create<SeasonTrackDefinition>());
        List<SeasonDefinition> seasons = new List<SeasonDefinition>();
        for (int seasonIndex = 0; seasonIndex < calendarConfig.seasonsPerYear; seasonIndex++)
        {
            SeasonDefinition season = Track(Create<SeasonDefinition>());
            SetSeasonData(
                season,
                $"season_{seasonIndex}",
                $"Season {seasonIndex}",
                WeatherType.Clear,
                UniformMatrix(),
                new List<FixedWeatherSlot>());
            seasons.Add(season);
        }

        SetSeasonTrackSeasons(track, seasons);
        List<ClimateRegionDefinition> regions = new List<ClimateRegionDefinition>();
        for (int i = 0; i < regionSpecs.Length; i++)
        {
            ClimateRegionDefinition region = Track(Create<ClimateRegionDefinition>());
            SetRegionData(
                region,
                regionSpecs[i].Id,
                regionSpecs[i].Id,
                track,
                regionSpecs[i].InitialWeather);
            regions.Add(region);
        }

        ClimateRegionCatalog catalog = Track(Create<ClimateRegionCatalog>());
        SetCatalogRegions(catalog, regions);
        return new ClimateFixture(simulationConfig, catalog, calendarConfig);
    }

    private WorldWeatherState CreateWeatherState(ClimateFixture fixture)
    {
        WorldWeatherState state = Track(Create<WorldWeatherState>());
        SetField(state, "weatherSimulationConfig", fixture.SimulationConfig);
        SetField(state, "climateRegionCatalog", fixture.Catalog);
        return state;
    }

    private SaveManager CreateSaveManager(params ScriptableObject[] targets)
    {
        GameObject gameObject = Track(new GameObject("Weather SaveManager Test"));
        gameObject.SetActive(false);
        SaveManager manager = gameObject.AddComponent<SaveManager>();
        SetField(manager, "saveTargets", new List<ScriptableObject>(targets));
        InvokePrivate(manager, "CacheSaveTargets", false);
        return manager;
    }

    private static void ApplyThroughSaveManager(SaveManager manager, SaveData data)
    {
        SetField(manager, "<CurrentSave>k__BackingField", data);
        InvokePrivate(manager, "ApplySaveData");
    }

    private static SaveData CreateTargetOrderSaveData()
    {
        return new SaveData
        {
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = 500L
            },
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_a",
                        currentWeatherType = (int)WeatherType.Storm,
                        currentWeatherStartMinute = 400L,
                        historyAvailableFromMinute = 400L,
                        rootSeed = 0xDEADBEEFu,
                        history = new List<WeatherHistoryRecordSaveEntry>()
                    },
                    new RegionWeatherSaveEntry
                    {
                        regionId = "region_b",
                        currentWeatherType = (int)WeatherType.Rain,
                        currentWeatherStartMinute = 350L,
                        historyAvailableFromMinute = 300L,
                        rootSeed = 0xCAFEBABEu,
                        history = new List<WeatherHistoryRecordSaveEntry>()
                    }
                },
                overrides = new List<WeatherOverrideSaveEntry>()
            }
        };
    }

    private WorldTimeState CreateWorldTime(long minute)
    {
        WorldTimeState state = CreateWorldTimeWithoutApplying();
        state.ApplySaveData(CreateTimeSave(minute));
        return state;
    }

    private WorldTimeState CreateWorldTimeWithoutApplying()
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

        WorldTimeState state = Track(Create<WorldTimeState>());
        SetField(state, "calendarConfig", calendar);
        return state;
    }

    private static SaveData CreateTimeSave(long minute)
    {
        return new SaveData
        {
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = minute
            }
        };
    }

    private static void SetSlotHours(ClimateFixture fixture, params int[] slotHours)
    {
        SetField(fixture.SimulationConfig, "weatherSlotHours", new List<int>(slotHours));
    }

    private static void SetAllSeasonFixedSlots(
        ClimateFixture fixture,
        params FixedWeatherSlot[] fixedSlots)
    {
        SeasonTrackDefinition track = fixture.Catalog.Regions[0].SeasonTrack;
        for (int i = 0; i < track.Seasons.Count; i++)
            SetField(track.Seasons[i], "fixedWeatherSlots", new List<FixedWeatherSlot>(fixedSlots));
    }

    private static void SetAllSeasonTransitionMatrix(ClimateFixture fixture, List<int> matrix)
    {
        SeasonTrackDefinition track = fixture.Catalog.Regions[0].SeasonTrack;
        for (int i = 0; i < track.Seasons.Count; i++)
            SetField(track.Seasons[i], "transitionWeights", matrix.ToArray());
    }

    private static List<int> MatrixThatAlwaysSelects(WeatherType target)
    {
        int weatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
        List<int> matrix = new List<int>(weatherTypeCount * weatherTypeCount);
        for (int i = 0; i < weatherTypeCount * weatherTypeCount; i++)
            matrix.Add(0);

        for (int from = 0; from < weatherTypeCount; from++)
            matrix[from * weatherTypeCount + (int)target] = 1;

        return matrix;
    }

    private static List<int> UniformMatrix()
    {
        List<int> matrix = new List<int>();
        int weatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
        for (int i = 0; i < weatherTypeCount * weatherTypeCount; i++)
            matrix.Add(1);
        return matrix;
    }

    private static WorldWeatherSaveData DeepCopyWeather(WorldWeatherSaveData source)
    {
        return new WorldWeatherSaveData
        {
            initialized = source.initialized,
            regions = new List<RegionWeatherSaveEntry>(source.regions),
            overrides = new List<WeatherOverrideSaveEntry>(source.overrides)
        };
    }

    private static void SetWeatherDefinitionContent(
        WeatherDefinition definition,
        WeatherType weatherType,
        string displayName)
    {
        SetField(definition, "type", weatherType);
        SetField(definition, "displayName", displayName);
        SetField(definition, "isPrecipitation", false);
        SetField(definition, "isSevereWeather", false);
    }

    private static void SetWeatherSimulationConfigData(
        WeatherSimulationConfig config,
        IEnumerable<int> slotHours,
        int forecastDays,
        IEnumerable<WeatherDefinition> definitions)
    {
        SetField(config, "weatherSlotHours", new List<int>(slotHours));
        SetField(config, "maxForecastDaysAhead", forecastDays);
        SetField(config, "weatherDefinitions", new List<WeatherDefinition>(definitions));
        // SetFixture* seams are intentionally not part of production authoring. Keep the lazy
        // lookup invalidated after reflection composes this in-memory fixture.
        SetField(config, "lookupBuilt", false);
        SetField(config, "lookupValid", false);
        SetField(config, "definitionsByType", null);
    }

    private static void SetSeasonData(
        SeasonDefinition season,
        string id,
        string displayName,
        WeatherType entryWeather,
        IEnumerable<int> weights,
        IEnumerable<FixedWeatherSlot> fixedSlots)
    {
        SetField(season, "seasonId", id);
        SetField(season, "displayName", displayName);
        SetField(season, "entryWeatherType", entryWeather);
        SetField(season, "transitionWeights", new List<int>(weights).ToArray());
        SetField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>(fixedSlots));
    }

    private static void SetSeasonTrackSeasons(
        SeasonTrackDefinition track,
        IEnumerable<SeasonDefinition> seasons)
    {
        SetField(track, "seasons", new List<SeasonDefinition>(seasons));
    }

    private static void SetRegionData(
        ClimateRegionDefinition region,
        string id,
        string displayName,
        SeasonTrackDefinition track,
        WeatherType initialWeather)
    {
        SetField(region, "regionId", id);
        SetField(region, "displayName", displayName);
        SetField(region, "seasonTrack", track);
        SetField(region, "initialWeatherType", initialWeather);
    }

    private static void SetCatalogRegions(
        ClimateRegionCatalog catalog,
        IEnumerable<ClimateRegionDefinition> regions)
    {
        SetField(catalog, "regions", new List<ClimateRegionDefinition>(regions));
        // The catalog builds its dictionary lazily. Reflection-composed fixtures must invalidate
        // all three cache fields whenever the authored list is replaced.
        SetField(catalog, "lookupBuilt", false);
        SetField(catalog, "lookupValid", false);
        SetField(catalog, "regionsById", null);
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
        Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}'.");
        method.Invoke(target, arguments);
    }

    private readonly struct RegionSpec
    {
        public readonly string Id;
        public readonly WeatherType InitialWeather;

        public RegionSpec(string id, WeatherType initialWeather)
        {
            Id = id;
            InitialWeather = initialWeather;
        }
    }

    private readonly struct ClimateFixture
    {
        public readonly WeatherSimulationConfig SimulationConfig;
        public readonly ClimateRegionCatalog Catalog;
        public readonly CalendarConfig CalendarConfig;

        public ClimateFixture(
            WeatherSimulationConfig simulationConfig,
            ClimateRegionCatalog catalog,
            CalendarConfig calendarConfig)
        {
            SimulationConfig = simulationConfig;
            Catalog = catalog;
            CalendarConfig = calendarConfig;
        }
    }
}
