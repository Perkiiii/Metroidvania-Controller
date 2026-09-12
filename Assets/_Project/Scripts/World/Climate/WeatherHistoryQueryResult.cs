using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Immutable result of a regional actual-weather history query.
/// </summary>
public readonly struct WeatherHistoryQueryResult
{
    private readonly ReadOnlyCollection<WeatherHistoryRecord> records;

    public WeatherHistoryQueryResult(
        IReadOnlyList<WeatherHistoryRecord> records,
        long requestedFrom,
        long requestedTo,
        long availableFrom)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));
        if (requestedFrom < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedFrom),
                requestedFrom,
                "History query start minute cannot be negative.");
        }
        if (requestedTo <= requestedFrom)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedTo),
                requestedTo,
                "History query end minute must be greater than its start minute.");
        }
        if (availableFrom < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableFrom),
                availableFrom,
                "History availability minute cannot be negative.");
        }

        this.records = new List<WeatherHistoryRecord>(records).AsReadOnly();
        RequestedFrom = requestedFrom;
        RequestedTo = requestedTo;
        AvailableFrom = availableFrom;
    }

    public IReadOnlyList<WeatherHistoryRecord> Records => records;
    public long RequestedFrom { get; }
    public long RequestedTo { get; }
    public long AvailableFrom { get; }
    public bool IsComplete => RequestedFrom >= AvailableFrom;
}
