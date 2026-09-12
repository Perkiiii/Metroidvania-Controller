using System;

/// <summary>
/// Immutable half-open interval describing one forecast weather value.
/// </summary>
public readonly struct WeatherForecastEntry : IEquatable<WeatherForecastEntry>
{
    public WeatherForecastEntry(
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
                "Weather forecast start minute cannot be negative.");
        }
        if (endMinuteExclusive <= startMinute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMinuteExclusive),
                endMinuteExclusive,
                "Weather forecast end minute must be greater than its start minute.");
        }

        WeatherType = weatherType;
        StartMinute = startMinute;
        EndMinuteExclusive = endMinuteExclusive;
    }

    public WeatherType WeatherType { get; }
    public long StartMinute { get; }
    public long EndMinuteExclusive { get; }

    public bool Equals(WeatherForecastEntry other)
    {
        return WeatherType == other.WeatherType
            && StartMinute == other.StartMinute
            && EndMinuteExclusive == other.EndMinuteExclusive;
    }

    public override bool Equals(object obj)
    {
        return obj is WeatherForecastEntry other && Equals(other);
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

    public static bool operator ==(WeatherForecastEntry left, WeatherForecastEntry right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WeatherForecastEntry left, WeatherForecastEntry right)
    {
        return !left.Equals(right);
    }
}
