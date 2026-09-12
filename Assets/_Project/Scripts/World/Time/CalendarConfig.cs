using System;
using UnityEngine;

// Authoring-only calendar shape and fresh-save defaults. Runtime time is owned by WorldTimeState.
[CreateAssetMenu(menuName = "Project/World/Calendar Config", fileName = "CalendarConfig")]
public sealed class CalendarConfig : ScriptableObject
{
    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 24;
    public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

    [Header("Calendar Shape")]
    [Min(1)] public int daysPerSeason = 28;
    [Min(1)] public int seasonsPerYear = 4;
    public WeekDay epochWeekDay = WeekDay.Monday;

    [Header("Day Phases (hours)")]
    [Range(0, 23)] public int dawnHour = 5;
    [Range(0, 23)] public int dayHour = 8;
    [Range(0, 23)] public int duskHour = 18;
    [Range(0, 23)] public int nightHour = 21;

    [Header("Fresh Save Date")]
    [Min(1)] public int initialYear = 1;
    [Min(0)] public int initialSeasonOrdinal;
    [Min(1)] public int initialDayInSeason = 1;
    [Range(0, 23)] public int initialHour = 8;
    [Range(0, 59)] public int initialMinute;

    [Header("Clock Pace")]
    [Min(0.0001f)] public float realSecondsPerGameMinute = 1.5f;

    public int DaysPerSeason => daysPerSeason;
    public int SeasonsPerYear => seasonsPerYear;
    public WeekDay EpochWeekDay => epochWeekDay;
    public int DawnHour => dawnHour;
    public int DayHour => dayHour;
    public int DuskHour => duskHour;
    public int NightHour => nightHour;
    public int InitialYear => initialYear;
    public int InitialSeasonOrdinal => initialSeasonOrdinal;
    public int InitialDayInSeason => initialDayInSeason;
    public int InitialHour => initialHour;
    public int InitialMinute => initialMinute;
    public float RealSecondsPerGameMinute => realSecondsPerGameMinute;

    public bool TryValidate(out string error)
    {
        if (daysPerSeason <= 0)
        {
            error = "daysPerSeason must be greater than zero.";
            return false;
        }

        if (seasonsPerYear <= 0)
        {
            error = "seasonsPerYear must be greater than zero.";
            return false;
        }

        if (!Enum.IsDefined(typeof(WeekDay), epochWeekDay))
        {
            error = "epochWeekDay must be a defined WeekDay value.";
            return false;
        }

        if (dawnHour < 0 || dawnHour >= HoursPerDay
            || dayHour < 0 || dayHour >= HoursPerDay
            || duskHour < 0 || duskHour >= HoursPerDay
            || nightHour < 0 || nightHour >= HoursPerDay
            || !(dawnHour < dayHour && dayHour < duskHour && duskHour < nightHour))
        {
            error = "Phase hours must satisfy 0 <= dawn < day < dusk < night < 24.";
            return false;
        }

        if (initialYear < 1)
        {
            error = "initialYear must be at least one.";
            return false;
        }

        if (initialSeasonOrdinal < 0 || initialSeasonOrdinal >= seasonsPerYear)
        {
            error = "initialSeasonOrdinal must be within the configured year.";
            return false;
        }

        if (initialDayInSeason < 1 || initialDayInSeason > daysPerSeason)
        {
            error = "initialDayInSeason must be within the configured season.";
            return false;
        }

        if (initialHour < 0 || initialHour >= HoursPerDay)
        {
            error = "initialHour must be in the range 0..23.";
            return false;
        }

        if (initialMinute < 0 || initialMinute >= MinutesPerHour)
        {
            error = "initialMinute must be in the range 0..59.";
            return false;
        }

        if (realSecondsPerGameMinute <= 0f
            || float.IsNaN(realSecondsPerGameMinute)
            || float.IsInfinity(realSecondsPerGameMinute))
        {
            error = "realSecondsPerGameMinute must be finite and greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid CalendarConfig: {error}");
    }

    public long ComputeInitialTotalDays()
    {
        ValidateOrThrow();

        checked
        {
            long seasonInstanceIndex = ((long)initialYear - 1L) * seasonsPerYear
                + initialSeasonOrdinal;
            return seasonInstanceIndex * daysPerSeason + (initialDayInSeason - 1L);
        }
    }

    public long ComputeInitialTotalGameMinutes()
    {
        checked
        {
            return ComputeInitialTotalDays() * MinutesPerDay
                + (long)initialHour * MinutesPerHour
                + initialMinute;
        }
    }

    // Alias kept intentionally small for callers that phrase the operation as a getter.
    public long GetInitialTotalGameMinutes()
    {
        return ComputeInitialTotalGameMinutes();
    }

    public DayPhase GetPhaseForHour(int hour)
    {
        ValidateOrThrow();

        if (hour < 0 || hour >= HoursPerDay)
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be in the range 0..23.");

        if (hour < dawnHour || hour >= nightHour)
            return DayPhase.Night;
        if (hour < dayHour)
            return DayPhase.Dawn;
        if (hour < duskHour)
            return DayPhase.Day;
        return DayPhase.Dusk;
    }

    public DayPhase GetPhaseForMinuteOfDay(int minuteOfDay)
    {
        if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
            throw new ArgumentOutOfRangeException(nameof(minuteOfDay), minuteOfDay,
                "Minute of day must be in the range 0..1439.");

        return GetPhaseForHour(minuteOfDay / MinutesPerHour);
    }
}
