using System;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldTimeSnapshotTests
{
    [Test]
    public void EpochUsesZeroBasedAbsoluteCountersAndEpochWeekday()
    {
        CalendarConfig config = CreateConfig();
        try
        {
            WorldTimeSnapshot snapshot = WorldTimeSnapshot.From(0L, config);

            Assert.That(snapshot.TotalGameMinutes, Is.Zero);
            Assert.That(snapshot.TotalDays, Is.Zero);
            Assert.That(snapshot.SeasonInstanceIndex, Is.Zero);
            Assert.That(snapshot.Year, Is.EqualTo(1L));
            Assert.That(snapshot.SeasonOrdinal, Is.Zero);
            Assert.That(snapshot.DayInSeason, Is.EqualTo(1));
            Assert.That(snapshot.WeekDay, Is.EqualTo(WeekDay.Monday));
            Assert.That(snapshot.Hour, Is.Zero);
            Assert.That(snapshot.Minute, Is.Zero);
            Assert.That(snapshot.Day01, Is.EqualTo(0f));
            Assert.That(snapshot.Phase, Is.EqualTo(DayPhase.Night));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void WorkedInitialDateMatchesExactEpochArithmetic()
    {
        CalendarConfig config = CreateConfig();
        try
        {
            long totalMinutes = config.ComputeInitialTotalGameMinutes();
            WorldTimeSnapshot snapshot = WorldTimeSnapshot.From(totalMinutes, config);

            Assert.That(totalMinutes, Is.EqualTo(204975L));
            Assert.That(snapshot.TotalDays, Is.EqualTo(142L));
            Assert.That(snapshot.Year, Is.EqualTo(2L));
            Assert.That(snapshot.SeasonOrdinal, Is.EqualTo(1));
            Assert.That(snapshot.DayInSeason, Is.EqualTo(3));
            Assert.That(snapshot.WeekDay, Is.EqualTo(WeekDay.Wednesday));
            Assert.That(snapshot.Hour, Is.EqualTo(8));
            Assert.That(snapshot.Minute, Is.EqualTo(15));
            Assert.That(snapshot.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(snapshot.Day01, Is.EqualTo(495f / 1440f).Within(0.000001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void PhaseRangesWrapAtMidnightAndUseHalfOpenBoundaries()
    {
        CalendarConfig config = CreateConfig();
        try
        {
            Assert.That(WorldTimeSnapshot.From(5L * 60L + 59L, config).Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(WorldTimeSnapshot.From(6L * 60L, config).Phase, Is.EqualTo(DayPhase.Dawn));
            Assert.That(WorldTimeSnapshot.From(8L * 60L, config).Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(WorldTimeSnapshot.From(18L * 60L, config).Phase, Is.EqualTo(DayPhase.Dusk));
            Assert.That(WorldTimeSnapshot.From(20L * 60L, config).Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(WorldTimeSnapshot.From(23L * 60L + 59L, config).Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(WorldTimeSnapshot.From(24L * 60L, config).Phase, Is.EqualTo(DayPhase.Night));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void LargeTimestampKeepsAbsoluteCountersWide()
    {
        CalendarConfig config = CreateConfig();
        try
        {
            long totalMinutes = ((long)int.MaxValue + 1L) * CalendarConfig.MinutesPerDay + 17L;
            WorldTimeSnapshot snapshot = WorldTimeSnapshot.From(totalMinutes, config);

            Assert.That(snapshot.TotalDays, Is.GreaterThan((long)int.MaxValue));
            Assert.That(snapshot.TotalGameMinutes, Is.EqualTo(totalMinutes));
            Assert.That(snapshot.Hour, Is.Zero);
            Assert.That(snapshot.Minute, Is.EqualTo(17));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void NegativeTimestampIsRejected()
    {
        CalendarConfig config = CreateConfig();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldTimeSnapshot.From(-1L, config));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static CalendarConfig CreateConfig()
    {
        CalendarConfig config = ScriptableObject.CreateInstance<CalendarConfig>();
        config.daysPerSeason = 28;
        config.seasonsPerYear = 4;
        config.epochWeekDay = WeekDay.Monday;
        config.dawnHour = 6;
        config.dayHour = 8;
        config.duskHour = 18;
        config.nightHour = 20;
        config.initialYear = 2;
        config.initialSeasonOrdinal = 1;
        config.initialDayInSeason = 3;
        config.initialHour = 8;
        config.initialMinute = 15;
        config.realSecondsPerGameMinute = 1f;
        return config;
    }
}
