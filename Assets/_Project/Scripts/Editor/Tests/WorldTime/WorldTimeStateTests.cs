using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

public sealed class WorldTimeStateTests
{
    [Test]
    public void LargeSkipEmitsTheSameOrderedBoundaryStreamAsSmallerCalls()
    {
        WorldTimeState large = CreateState(out CalendarConfig largeConfig);
        WorldTimeState small = CreateState(out CalendarConfig smallConfig);
        try
        {
            large.ApplySaveData(new SaveData());
            small.ApplySaveData(new SaveData());

            List<string> largeEvents = CaptureBoundaryEvents(large);
            List<string> smallEvents = CaptureBoundaryEvents(small);

            // Three complete 112-day years plus a partial day makes this a real offline-style
            // skip: it crosses many phases, seasons, and year boundaries while remaining small
            // enough for an EditMode test. Daily chunks preserve the same hour-boundary stream.
            long fullDays = (long)largeConfig.daysPerSeason * largeConfig.seasonsPerYear * 3L;
            long fullDaysInMinutes = fullDays * CalendarConfig.MinutesPerDay;
            const long partialMinutes = 125L;
            long largeSkip = fullDaysInMinutes + partialMinutes;

            large.AdvanceMinutes(largeSkip);
            for (long day = 0L; day < fullDays; day++)
                small.AdvanceMinutes(CalendarConfig.MinutesPerDay);
            small.AdvanceMinutes(partialMinutes);

            Assert.That(large.TotalGameMinutes, Is.EqualTo(small.TotalGameMinutes));
            Assert.That(largeEvents, Is.EqualTo(smallEvents));
        }
        finally
        {
            Destroy(large, largeConfig);
            Destroy(small, smallConfig);
        }
    }

    [Test]
    public void DawnToDawnSkipEmitsEveryCrossedPhaseInChronologicalOrder()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.initialHour = 6;
            state.ApplySaveData(new SaveData());

            List<DayPhase> phases = new List<DayPhase>();
            state.DayPhaseChanged += snapshot => phases.Add(snapshot.Phase);

            state.AdvanceMinutes(CalendarConfig.MinutesPerDay);

            Assert.That(phases, Is.EqualTo(new[]
            {
                DayPhase.Day,
                DayPhase.Dusk,
                DayPhase.Night,
                DayPhase.Dawn
            }));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void OneCrossedHourEmitsOneHourChangedAtTheExactBoundary()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.initialMinute = 30;
            state.ApplySaveData(new SaveData());

            List<long> hourEvents = new List<long>();
            state.HourChanged += snapshot => hourEvents.Add(snapshot.TotalGameMinutes);

            state.AdvanceMinutes(30L);

            Assert.That(hourEvents, Is.EqualTo(new[] { 60L }));
            Assert.That(state.TotalGameMinutes, Is.EqualTo(60L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void MultipleCrossedHoursEmitOrderedHourBoundaries()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.initialMinute = 30;
            state.ApplySaveData(new SaveData());

            List<long> hourEvents = new List<long>();
            state.HourChanged += snapshot => hourEvents.Add(snapshot.TotalGameMinutes);

            state.AdvanceMinutes(150L);

            Assert.That(hourEvents, Is.EqualTo(new[] { 60L, 120L, 180L }));
            Assert.That(state.TotalGameMinutes, Is.EqualTo(180L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void TimeAdvancedPublishesExactlyOnceWithBeforeAndAfterSnapshots()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            state.ApplySaveData(new SaveData());
            int callbackCount = 0;
            WorldTimeSnapshot before = default;
            WorldTimeSnapshot after = default;
            state.TimeAdvanced += (from, to) =>
            {
                callbackCount++;
                before = from;
                after = to;
            };

            state.AdvanceMinutes(125L);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(before.TotalGameMinutes, Is.EqualTo(0L));
            Assert.That(after.TotalGameMinutes, Is.EqualTo(125L));
            Assert.That(state.TotalGameMinutes, Is.EqualTo(125L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void DayRolloverPublishesDayEventAndAdvancesWeekday()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.initialHour = 23;
            config.initialMinute = 59;
            state.ApplySaveData(new SaveData());

            List<WorldTimeSnapshot> dayEvents = new List<WorldTimeSnapshot>();
            state.DayChanged += snapshot => dayEvents.Add(snapshot);

            state.AdvanceMinutes(1L);

            Assert.That(dayEvents, Has.Count.EqualTo(1));
            Assert.That(dayEvents[0].TotalDays, Is.EqualTo(1L));
            Assert.That(dayEvents[0].DayInSeason, Is.EqualTo(2));
            Assert.That(dayEvents[0].WeekDay, Is.EqualTo(WeekDay.Tuesday));
            Assert.That(state.Current.WeekDay, Is.EqualTo(WeekDay.Tuesday));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void WeekdayWrapsFromSundayBackToMonday()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.initialDayInSeason = 7;
            config.initialHour = 23;
            config.initialMinute = 59;
            state.ApplySaveData(new SaveData());

            Assert.That(state.Current.WeekDay, Is.EqualTo(WeekDay.Sunday));
            List<WeekDay> dayEvents = new List<WeekDay>();
            state.DayChanged += snapshot => dayEvents.Add(snapshot.WeekDay);

            state.AdvanceMinutes(1L);

            Assert.That(dayEvents, Is.EqualTo(new[] { WeekDay.Monday }));
            Assert.That(state.Current.WeekDay, Is.EqualTo(WeekDay.Monday));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void SeasonRolloverPublishesSeasonEventAndResetsDayOrdinal()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.daysPerSeason = 1;
            config.initialHour = 23;
            config.initialMinute = 59;
            state.ApplySaveData(new SaveData());

            List<WorldTimeSnapshot> seasonEvents = new List<WorldTimeSnapshot>();
            state.SeasonChanged += snapshot => seasonEvents.Add(snapshot);

            state.AdvanceMinutes(1L);

            Assert.That(seasonEvents, Has.Count.EqualTo(1));
            Assert.That(seasonEvents[0].SeasonInstanceIndex, Is.EqualTo(1L));
            Assert.That(seasonEvents[0].SeasonOrdinal, Is.EqualTo(1));
            Assert.That(seasonEvents[0].DayInSeason, Is.EqualTo(1));
            Assert.That(seasonEvents[0].Year, Is.EqualTo(1L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void YearRolloverPublishesYearEventAndAdvancesYearCounter()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.daysPerSeason = 1;
            config.seasonsPerYear = 1;
            config.initialHour = 23;
            config.initialMinute = 59;
            state.ApplySaveData(new SaveData());

            List<WorldTimeSnapshot> yearEvents = new List<WorldTimeSnapshot>();
            state.YearChanged += snapshot => yearEvents.Add(snapshot);

            state.AdvanceMinutes(1L);

            Assert.That(yearEvents, Has.Count.EqualTo(1));
            Assert.That(yearEvents[0].Year, Is.EqualTo(2L));
            Assert.That(yearEvents[0].SeasonInstanceIndex, Is.EqualTo(1L));
            Assert.That(yearEvents[0].SeasonOrdinal, Is.Zero);
            Assert.That(yearEvents[0].DayInSeason, Is.EqualTo(1));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void CoincidentMidnightSeasonAndYearEventsUseFixedOrder()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            config.daysPerSeason = 1;
            config.seasonsPerYear = 1;
            config.initialYear = 1;
            config.initialSeasonOrdinal = 0;
            config.initialDayInSeason = 1;
            config.initialHour = 23;
            config.initialMinute = 59;
            state.ApplySaveData(new SaveData());

            List<string> events = new List<string>();
            state.HourChanged += _ => events.Add("Hour");
            state.DayChanged += _ => events.Add("Day");
            state.SeasonChanged += _ => events.Add("Season");
            state.YearChanged += _ => events.Add("Year");
            state.DayPhaseChanged += _ => events.Add("Phase");

            state.AdvanceMinutes(1L);

            Assert.That(events, Is.EqualTo(new[] { "Hour", "Day", "Season", "Year" }));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void ZeroIsNoOpAndNegativeAdvanceIsRejected()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            state.ApplySaveData(new SaveData());
            int eventCount = 0;
            state.TimeAdvanced += (_, __) => eventCount++;
            long before = state.TotalGameMinutes;

            state.AdvanceMinutes(0L);

            Assert.That(state.TotalGameMinutes, Is.EqualTo(before));
            Assert.That(eventCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AdvanceMinutes(-1L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void CheckedOverflowIsRejectedWithoutChangingCanonicalTime()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            state.ApplySaveData(new SaveData
            {
                worldTime = new WorldTimeSaveData
                {
                    initialized = true,
                    totalGameMinutes = long.MaxValue
                }
            });

            Assert.Throws<OverflowException>(() => state.AdvanceMinutes(1L));
            Assert.That(state.TotalGameMinutes, Is.EqualTo(long.MaxValue));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void AdvanceFromBoundaryCallbackIsRejectedAsReentrant()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            state.ApplySaveData(new SaveData());
            bool rejected = false;
            state.HourChanged += _ =>
            {
                try
                {
                    state.AdvanceMinutes(1L);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
            };

            state.AdvanceMinutes(60L);

            Assert.That(rejected, Is.True);
            Assert.That(state.TotalGameMinutes, Is.EqualTo(60L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void ThrowingSubscriberDoesNotStopLaterBoundariesOrFinalEvent()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            state.ApplySaveData(new SaveData());
            bool hasThrown = false;
            List<long> laterHourEvents = new List<long>();
            int timeAdvancedCount = 0;
            long finalMinutes = -1L;

            state.HourChanged += _ =>
            {
                if (!hasThrown)
                {
                    hasThrown = true;
                    throw new InvalidOperationException("intentional world-time callback failure");
                }
            };
            state.HourChanged += snapshot => laterHourEvents.Add(snapshot.TotalGameMinutes);
            state.TimeAdvanced += (_, after) =>
            {
                timeAdvancedCount++;
                finalMinutes = after.TotalGameMinutes;
            };

            LogAssert.Expect(LogType.Exception, new Regex("intentional world-time callback failure"));
            state.AdvanceMinutes(120L);

            Assert.That(state.TotalGameMinutes, Is.EqualTo(120L));
            Assert.That(laterHourEvents, Is.EqualTo(new[] { 60L, 120L }));
            Assert.That(timeAdvancedCount, Is.EqualTo(1));
            Assert.That(finalMinutes, Is.EqualTo(120L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void ApplyFreshAndInitializedSaveBothPublishOneStateApplied()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            int appliedCount = 0;
            state.StateApplied += _ => appliedCount++;

            state.ApplySaveData(new SaveData());
            Assert.That(state.IsLoaded, Is.True);
            Assert.That(state.TotalGameMinutes, Is.EqualTo(config.ComputeInitialTotalGameMinutes()));

            state.ApplySaveData(new SaveData
            {
                worldTime = new WorldTimeSaveData
                {
                    initialized = true,
                    totalGameMinutes = 12345L
                }
            });

            Assert.That(appliedCount, Is.EqualTo(2));
            Assert.That(state.TotalGameMinutes, Is.EqualTo(12345L));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    [Test]
    public void GatherWritesCanonicalLongAndDoesNotRequireLoadedFlag()
    {
        WorldTimeState state = CreateState(out CalendarConfig config);
        try
        {
            SaveData data = new SaveData();
            state.GatherSaveData(data);

            Assert.That(data.worldTime, Is.Not.Null);
            Assert.That(data.worldTime.initialized, Is.True);
            Assert.That(data.worldTime.totalGameMinutes, Is.EqualTo(config.ComputeInitialTotalGameMinutes()));
        }
        finally
        {
            Destroy(state, config);
        }
    }

    private static List<string> CaptureBoundaryEvents(WorldTimeState state)
    {
        List<string> events = new List<string>();
        state.HourChanged += snapshot => events.Add($"H:{snapshot.TotalGameMinutes}");
        state.DayChanged += snapshot => events.Add($"D:{snapshot.TotalGameMinutes}");
        state.SeasonChanged += snapshot => events.Add($"S:{snapshot.TotalGameMinutes}");
        state.YearChanged += snapshot => events.Add($"Y:{snapshot.TotalGameMinutes}");
        state.DayPhaseChanged += snapshot => events.Add($"P:{snapshot.TotalGameMinutes}:{snapshot.Phase}");
        return events;
    }

    private static WorldTimeState CreateState(out CalendarConfig config)
    {
        config = ScriptableObject.CreateInstance<CalendarConfig>();
        config.daysPerSeason = 28;
        config.seasonsPerYear = 4;
        config.epochWeekDay = WeekDay.Monday;
        config.dawnHour = 6;
        config.dayHour = 8;
        config.duskHour = 18;
        config.nightHour = 20;
        config.initialYear = 1;
        config.initialSeasonOrdinal = 0;
        config.initialDayInSeason = 1;
        config.initialHour = 0;
        config.initialMinute = 0;
        config.realSecondsPerGameMinute = 1f;

        WorldTimeState state = ScriptableObject.CreateInstance<WorldTimeState>();
        FieldInfo configField = typeof(WorldTimeState).GetField(
            "calendarConfig",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(configField, Is.Not.Null, "WorldTimeState must serialize a CalendarConfig reference.");
        configField.SetValue(state, config);
        return state;
    }

    private static void Destroy(WorldTimeState state, CalendarConfig config)
    {
        if (state != null)
            UnityEngine.Object.DestroyImmediate(state);
        if (config != null)
            UnityEngine.Object.DestroyImmediate(config);
    }
}
