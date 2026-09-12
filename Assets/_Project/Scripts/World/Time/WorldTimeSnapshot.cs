using System;

// Immutable calendar projection of one canonical, non-negative game-minute timestamp.
public readonly struct WorldTimeSnapshot : IEquatable<WorldTimeSnapshot>
{
    private WorldTimeSnapshot(
        long totalGameMinutes,
        long totalDays,
        long seasonInstanceIndex,
        long year,
        int seasonOrdinal,
        int dayInSeason,
        WeekDay weekDay,
        int hour,
        int minute,
        float day01,
        DayPhase phase)
    {
        TotalGameMinutes = totalGameMinutes;
        TotalDays = totalDays;
        SeasonInstanceIndex = seasonInstanceIndex;
        Year = year;
        SeasonOrdinal = seasonOrdinal;
        DayInSeason = dayInSeason;
        WeekDay = weekDay;
        Hour = hour;
        Minute = minute;
        Day01 = day01;
        Phase = phase;
    }

    public long TotalGameMinutes { get; }
    public long TotalDays { get; }
    public long SeasonInstanceIndex { get; }
    public long Year { get; }
    public int SeasonOrdinal { get; }
    public int DayInSeason { get; }
    public WeekDay WeekDay { get; }
    public int Hour { get; }
    public int Minute { get; }
    public float Day01 { get; }
    public DayPhase Phase { get; }

    public static WorldTimeSnapshot From(long totalGameMinutes, CalendarConfig config)
    {
        if (totalGameMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(totalGameMinutes), totalGameMinutes,
                "World time cannot be negative.");
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        config.ValidateOrThrow();

        long minuteOfDay = totalGameMinutes % CalendarConfig.MinutesPerDay;
        long totalDays = totalGameMinutes / CalendarConfig.MinutesPerDay;
        long seasonInstanceIndex = totalDays / config.DaysPerSeason;
        int seasonOrdinal = (int)(seasonInstanceIndex % config.SeasonsPerYear);
        int dayInSeason = (int)(totalDays % config.DaysPerSeason) + 1;
        long year = 1L + seasonInstanceIndex / config.SeasonsPerYear;
        WeekDay weekDay = (WeekDay)(((int)config.EpochWeekDay + (int)(totalDays % 7L)) % 7);
        int hour = (int)(minuteOfDay / CalendarConfig.MinutesPerHour);
        int minute = (int)(minuteOfDay % CalendarConfig.MinutesPerHour);
        float day01 = minuteOfDay / (float)CalendarConfig.MinutesPerDay;
        DayPhase phase = config.GetPhaseForMinuteOfDay((int)minuteOfDay);

        return new WorldTimeSnapshot(
            totalGameMinutes,
            totalDays,
            seasonInstanceIndex,
            year,
            seasonOrdinal,
            dayInSeason,
            weekDay,
            hour,
            minute,
            day01,
            phase);
    }

    public bool Equals(WorldTimeSnapshot other)
    {
        return TotalGameMinutes == other.TotalGameMinutes
            && TotalDays == other.TotalDays
            && SeasonInstanceIndex == other.SeasonInstanceIndex
            && Year == other.Year
            && SeasonOrdinal == other.SeasonOrdinal
            && DayInSeason == other.DayInSeason
            && WeekDay == other.WeekDay
            && Hour == other.Hour
            && Minute == other.Minute
            && Day01.Equals(other.Day01)
            && Phase == other.Phase;
    }

    public override bool Equals(object obj)
    {
        return obj is WorldTimeSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TotalGameMinutes.GetHashCode();
            hash = (hash * 397) ^ TotalDays.GetHashCode();
            hash = (hash * 397) ^ SeasonInstanceIndex.GetHashCode();
            hash = (hash * 397) ^ Year.GetHashCode();
            hash = (hash * 397) ^ SeasonOrdinal;
            hash = (hash * 397) ^ DayInSeason;
            hash = (hash * 397) ^ (int)WeekDay;
            hash = (hash * 397) ^ Hour;
            hash = (hash * 397) ^ Minute;
            hash = (hash * 397) ^ Day01.GetHashCode();
            return (hash * 397) ^ (int)Phase;
        }
    }

    public static bool operator ==(WorldTimeSnapshot left, WorldTimeSnapshot right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WorldTimeSnapshot left, WorldTimeSnapshot right)
    {
        return !left.Equals(right);
    }
}
