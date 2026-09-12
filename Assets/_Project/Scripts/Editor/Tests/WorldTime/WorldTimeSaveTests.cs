using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldTimeSaveTests
{
    [Test]
    public void CurrentSaveVersionIsSix()
    {
        Assert.That(SaveDataMigrator.CurrentSaveVersion, Is.EqualTo(6));
    }

    [Test]
    public void WeatherMigrationDeepNormalizesNestedSaveShapeAndKeepsPreV6Uninitialized()
    {
        SaveData data = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 5 },
            worldWeather = new WorldWeatherSaveData
            {
                initialized = true,
                regions = new System.Collections.Generic.List<RegionWeatherSaveEntry>
                {
                    new RegionWeatherSaveEntry
                    {
                        regionId = null,
                        history = null
                    }
                },
                overrides = new System.Collections.Generic.List<WeatherOverrideSaveEntry>
                {
                    new WeatherOverrideSaveEntry
                    {
                        regionId = null,
                        sourceTag = null
                    }
                }
            }
        };

        SaveDataMigrator.Migrate(data);

        Assert.That(data.meta.saveVersion, Is.EqualTo(6));
        Assert.That(data.worldWeather, Is.Not.Null);
        Assert.That(data.worldWeather.initialized, Is.False);
        Assert.That(data.worldWeather.regions, Is.Not.Null);
        Assert.That(data.worldWeather.regions[0].regionId, Is.EqualTo(string.Empty));
        Assert.That(data.worldWeather.regions[0].history, Is.Not.Null);
        Assert.That(data.worldWeather.overrides, Is.Not.Null);
        Assert.That(data.worldWeather.overrides[0].regionId, Is.EqualTo(string.Empty));
        Assert.That(data.worldWeather.overrides[0].sourceTag, Is.EqualTo(string.Empty));
    }

    [Test]
    public void MissingWorldTimeSectionIsNormalizedWithoutInventingTime()
    {
        SaveData data = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 4 },
            worldTime = null
        };

        SaveDataMigrator.Migrate(data);

        Assert.That(data.worldTime, Is.Not.Null);
        Assert.That(data.worldTime.initialized, Is.False);
        Assert.That(data.worldTime.totalGameMinutes, Is.Zero);
        Assert.That(data.meta.saveVersion, Is.EqualTo(SaveDataMigrator.CurrentSaveVersion));
    }

    [Test]
    public void PreV5WorldTimeSectionRemainsUninitializedDuringMigration()
    {
        SaveData data = new SaveData
        {
            meta = new MetaSaveData { saveVersion = 4 },
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = 123456L
            }
        };

        SaveDataMigrator.Migrate(data);

        Assert.That(data.worldTime.initialized, Is.False,
            "Pre-v5 saves have no authoritative world-time value and must use the authored fresh date.");
    }

    [Test]
    public void WorldTimeSaveDataRoundTripsThroughJson()
    {
        const long expectedMinutes = 9876543210123L;
        SaveData source = new SaveData
        {
            meta = new MetaSaveData { saveVersion = SaveDataMigrator.CurrentSaveVersion },
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = expectedMinutes
            }
        };

        Assert.That(SaveSerializer.TrySerialize(source, out string json), Is.True);
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData destination), Is.True);

        SaveDataMigrator.Migrate(destination);

        Assert.That(destination.worldTime, Is.Not.Null);
        Assert.That(destination.worldTime.initialized, Is.True);
        Assert.That(destination.worldTime.totalGameMinutes, Is.EqualTo(expectedMinutes));
    }

    [Test]
    public void WorldTimeStateGatherAndApplyRoundTripCanonicalMinutes()
    {
        WorldTimeState source = CreateState(out UnityEngine.Object sourceConfig);
        WorldTimeState destination = CreateState(out UnityEngine.Object destinationConfig);
        try
        {
            const long expectedMinutes = 123456789L;
            source.ApplySaveData(CreateInitializedTimeSave(expectedMinutes));

            SaveData gathered = new SaveData();
            source.GatherSaveData(gathered);

            Assert.That(gathered.worldTime, Is.Not.Null);
            Assert.That(gathered.worldTime.initialized, Is.True);
            Assert.That(gathered.worldTime.totalGameMinutes, Is.EqualTo(expectedMinutes));

            destination.ApplySaveData(gathered);

            Assert.That(destination.TotalGameMinutes, Is.EqualTo(expectedMinutes));
        }
        finally
        {
            DestroyState(source, sourceConfig);
            DestroyState(destination, destinationConfig);
        }
    }

    [Test]
    public void ApplyingWorldTimeSaveRaisesStateAppliedExactlyOnce()
    {
        WorldTimeState state = CreateState(out UnityEngine.Object config);
        try
        {
            int eventCount = 0;
            long appliedMinutes = -1L;
            state.StateApplied += snapshot =>
            {
                eventCount++;
                appliedMinutes = snapshot.TotalGameMinutes;
            };

            const long expectedMinutes = 43210L;
            state.ApplySaveData(CreateInitializedTimeSave(expectedMinutes));

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(appliedMinutes, Is.EqualTo(expectedMinutes));
        }
        finally
        {
            DestroyState(state, config);
        }
    }

    [Test]
    public void ApplyingNegativeSavedMinutesThrowsAndDoesNotApplyState()
    {
        WorldTimeState state = CreateState(out UnityEngine.Object config);
        try
        {
            int eventCount = 0;
            state.StateApplied += _ => eventCount++;

            const long previousMinutes = 24680L;
            state.ApplySaveData(CreateInitializedTimeSave(previousMinutes));
            Assert.That(state.IsLoaded, Is.True);
            Assert.That(state.TotalGameMinutes, Is.EqualTo(previousMinutes));
            Assert.That(eventCount, Is.EqualTo(1));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                state.ApplySaveData(CreateInitializedTimeSave(-1L)));

            Assert.That(state.TotalGameMinutes, Is.EqualTo(previousMinutes));
            Assert.That(state.IsLoaded, Is.True);
            Assert.That(eventCount, Is.EqualTo(1),
                "Rejecting a negative saved timestamp must not raise StateApplied again.");
        }
        finally
        {
            DestroyState(state, config);
        }
    }

    private static SaveData CreateInitializedTimeSave(long totalGameMinutes)
    {
        return new SaveData
        {
            meta = new MetaSaveData { saveVersion = SaveDataMigrator.CurrentSaveVersion },
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = totalGameMinutes
            }
        };
    }

    private static WorldTimeState CreateState(out UnityEngine.Object config)
    {
        WorldTimeState state = ScriptableObject.CreateInstance<WorldTimeState>();
        config = null;

        // Keep the test independent of a production asset while respecting the state
        // object's serialized CalendarConfig dependency. The exact field name is an
        // implementation detail; the contract only requires the dependency itself.
        FieldInfo configField = FindCalendarConfigField(typeof(WorldTimeState));
        if (configField != null && typeof(ScriptableObject).IsAssignableFrom(configField.FieldType))
        {
            config = ScriptableObject.CreateInstance(configField.FieldType);
            configField.SetValue(state, config);
            InitializeCommonCalendarDefaults(config);
        }

        return state;
    }

    private static FieldInfo FindCalendarConfigField(Type stateType)
    {
        FieldInfo[] fields = stateType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType.Name == "CalendarConfig")
                return fields[i];
        }

        return null;
    }

    private static void InitializeCommonCalendarDefaults(UnityEngine.Object config)
    {
        // Agent A's authored defaults are normally valid. These assignments only fill
        // zero-value test instances when the corresponding serialized names exist.
        SetMemberIfPresent(config, 28, "daysPerSeason");
        SetMemberIfPresent(config, 4, "seasonsPerYear");
        SetMemberIfPresent(config, 5, "dawnHour", "phaseDawnHour");
        SetMemberIfPresent(config, 8, "dayHour", "phaseDayHour");
        SetMemberIfPresent(config, 18, "duskHour", "phaseDuskHour");
        SetMemberIfPresent(config, 21, "nightHour", "phaseNightHour");
        SetMemberIfPresent(config, 1, "initialYear");
        SetMemberIfPresent(config, 0, "initialSeasonOrdinal");
        SetMemberIfPresent(config, 1, "initialDayInSeason");
        SetMemberIfPresent(config, 8, "initialHour");
        SetMemberIfPresent(config, 0, "initialMinute");
        SetMemberIfPresent(config, 1f, "realSecondsPerGameMinute");
    }

    private static void SetMemberIfPresent(object target, object value, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = target.GetType();
        for (int i = 0; i < names.Length; i++)
        {
            FieldInfo field = type.GetField(names[i], flags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(names[i], flags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value, null);
                return;
            }
        }
    }

    private static void DestroyState(WorldTimeState state, UnityEngine.Object config)
    {
        if (state != null)
            UnityEngine.Object.DestroyImmediate(state);
        if (config != null)
            UnityEngine.Object.DestroyImmediate(config);
    }
}
