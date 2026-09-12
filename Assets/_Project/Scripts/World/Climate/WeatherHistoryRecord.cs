using System;

/// <summary>
/// Immutable half-open interval describing one actual weather segment.
/// </summary>
public readonly struct WeatherHistoryRecord : IEquatable<WeatherHistoryRecord>
{
    public WeatherHistoryRecord(
        WeatherType weatherType,
        long startMinute,
        long endMinuteExclusive)
    {
        if (!WeatherTypeUtility.IsDefined(weatherType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weatherType),
                weatherType,
                "Weather type must be defined.");
        }
        if (startMinute < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startMinute),
                startMinute,
                "Weather history start minute cannot be negative.");
        }
        if (endMinuteExclusive <= startMinute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMinuteExclusive),
                endMinuteExclusive,
                "Weather history end minute must be greater than its start minute.");
        }

        WeatherType = weatherType;
        StartMinute = startMinute;
        EndMinuteExclusive = endMinuteExclusive;
    }

    public WeatherType WeatherType { get; }
    public long StartMinute { get; }
    public long EndMinuteExclusive { get; }

    public bool Equals(WeatherHistoryRecord other)
    {
        return WeatherType == other.WeatherType
            && StartMinute == other.StartMinute
            && EndMinuteExclusive == other.EndMinuteExclusive;
    }

    public override bool Equals(object obj)
    {
        return obj is WeatherHistoryRecord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)WeatherType;
            hash = (hash * 397) ^ StartMinute.GetHashCode();
            return (hash * 397) ^ EndMinuteExclusive.GetHashCode();
        }
    }

    public static bool operator ==(WeatherHistoryRecord left, WeatherHistoryRecord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WeatherHistoryRecord left, WeatherHistoryRecord right)
    {
        return !left.Equals(right);
    }
}
