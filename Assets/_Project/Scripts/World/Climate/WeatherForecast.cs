using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Immutable read-only forecast for one regional weather timeline.
/// </summary>
public readonly struct WeatherForecast
{
    private readonly ReadOnlyCollection<WeatherForecastEntry> entries;

    public WeatherForecast(
        string regionId,
        long requestedAtMinute,
        long endMinuteExclusive,
        IReadOnlyList<WeatherForecastEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(regionId))
            throw new ArgumentException("A weather forecast requires a nonempty region ID.", nameof(regionId));
        if (requestedAtMinute < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedAtMinute),
                requestedAtMinute,
                "Weather forecast requested-at minute cannot be negative.");
        }
        if (endMinuteExclusive <= requestedAtMinute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMinuteExclusive),
                endMinuteExclusive,
                "Weather forecast end minute must be greater than its requested-at minute.");
        }
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        this.entries = new List<WeatherForecastEntry>(entries).AsReadOnly();
        RegionId = regionId;
        RequestedAtMinute = requestedAtMinute;
        EndMinuteExclusive = endMinuteExclusive;
    }

    public string RegionId { get; }
    public long RequestedAtMinute { get; }
    public long EndMinuteExclusive { get; }
    public IReadOnlyList<WeatherForecastEntry> Entries => entries;
}
