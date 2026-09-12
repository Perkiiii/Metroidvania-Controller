using System;

/// <summary>
/// Immutable, query-facing current weather value. The mutable regional runtime record remains an
/// implementation detail of WorldWeatherState.
/// </summary>
public readonly struct WeatherSnapshot : IEquatable<WeatherSnapshot>
{
    public WeatherSnapshot(
        string regionId,
        WeatherType weatherType,
        long sinceMinute,
        long durationSoFarMinutes)
    {
        if (string.IsNullOrWhiteSpace(regionId))
            throw new ArgumentException("A weather snapshot requires a nonempty region ID.", nameof(regionId));
        if (!WeatherTypeUtility.IsDefined(weatherType))
            throw new ArgumentOutOfRangeException(nameof(weatherType), weatherType, "Weather type must be defined.");
        if (sinceMinute < 0L)
            throw new ArgumentOutOfRangeException(nameof(sinceMinute), sinceMinute, "Weather start minute cannot be negative.");
        if (durationSoFarMinutes < 0L)
            throw new ArgumentOutOfRangeException(nameof(durationSoFarMinutes), durationSoFarMinutes, "Weather duration cannot be negative.");

        RegionId = regionId;
        WeatherType = weatherType;
        SinceMinute = sinceMinute;
        DurationSoFarMinutes = durationSoFarMinutes;
    }

    public string RegionId { get; }
    public WeatherType WeatherType { get; }
    public long SinceMinute { get; }
    public long DurationSoFarMinutes { get; }

    public static WeatherSnapshot FromCurrentMinute(
        string regionId,
        WeatherType weatherType,
        long sinceMinute,
        long currentMinute)
    {
        if (currentMinute < sinceMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(currentMinute), currentMinute,
                "Current minute cannot precede the weather start minute.");
        }

        return new WeatherSnapshot(
            regionId,
            weatherType,
            sinceMinute,
            checked(currentMinute - sinceMinute));
    }

    public bool Equals(WeatherSnapshot other)
    {
        return string.Equals(RegionId, other.RegionId, StringComparison.Ordinal)
            && WeatherType == other.WeatherType
            && SinceMinute == other.SinceMinute
            && DurationSoFarMinutes == other.DurationSoFarMinutes;
    }

    public override bool Equals(object obj)
    {
        return obj is WeatherSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = RegionId != null ? StringComparer.Ordinal.GetHashCode(RegionId) : 0;
            hash = (hash * 397) ^ (int)WeatherType;
            hash = (hash * 397) ^ SinceMinute.GetHashCode();
            return (hash * 397) ^ DurationSoFarMinutes.GetHashCode();
        }
    }

    public static bool operator ==(WeatherSnapshot left, WeatherSnapshot right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WeatherSnapshot left, WeatherSnapshot right)
    {
        return !left.Equals(right);
    }
}
