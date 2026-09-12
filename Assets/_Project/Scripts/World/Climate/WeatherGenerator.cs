using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Deterministically generates independent regional weather seasons. This kernel owns only an
/// in-memory LRU of immutable results; live world state and temporal behavior remain external.
/// </summary>
public sealed class WeatherGenerator
{
    public const int CachedPeriodsPerRegion = 3;

    private readonly int daysPerSeason;
    private readonly int slotsPerDay;
    private readonly Dictionary<string, RegionCache> cachesByRegion =
        new Dictionary<string, RegionCache>(StringComparer.Ordinal);
    private readonly HashSet<InvalidTransitionRowKey> loggedInvalidRows =
        new HashSet<InvalidTransitionRowKey>();

    public WeatherGenerator(int daysPerSeason, int slotsPerDay)
    {
        if (daysPerSeason <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysPerSeason),
                daysPerSeason,
                "Days per season must be greater than zero.");
        }
        if (slotsPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotsPerDay),
                slotsPerDay,
                "Weather slots per day must be greater than zero.");
        }

        this.daysPerSeason = daysPerSeason;
        this.slotsPerDay = slotsPerDay;
    }

    public IReadOnlyList<WeatherType> GenerateSeason(
        ClimateRegionDefinition region,
        uint rootSeed,
        long seasonInstanceIndex)
    {
        SeasonDefinition season = ResolveSeason(region, rootSeed, seasonInstanceIndex);
        RegionCache regionCache = GetOrCreateRegionCache(region.RegionId);

        if (regionCache.Periods.TryGetValue(seasonInstanceIndex, out CachedPeriod cached))
        {
            if (cached.Matches(region, season, rootSeed))
            {
                Touch(regionCache, cached);
                return cached.Values;
            }

            Remove(regionCache, cached);
        }

        ReadOnlyCollection<WeatherType> generated = Generate(season, rootSeed, seasonInstanceIndex);
        LinkedListNode<long> recencyNode = regionCache.Recency.AddFirst(seasonInstanceIndex);
        CachedPeriod period = new CachedPeriod(region, season, rootSeed, generated, recencyNode);
        regionCache.Periods.Add(seasonInstanceIndex, period);
        Trim(regionCache);
        return generated;
    }

    public WeatherType ResolveSlot(
        ClimateRegionDefinition region,
        uint rootSeed,
        long seasonInstanceIndex,
        int dayInSeason,
        int slotIndex)
    {
        if (dayInSeason < 1 || dayInSeason > daysPerSeason)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayInSeason),
                dayInSeason,
                $"Day in season must be in the range 1..{daysPerSeason}.");
        }
        if (slotIndex < 0 || slotIndex >= slotsPerDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                $"Weather slot index must be in the range 0..{slotsPerDay - 1}.");
        }

        IReadOnlyList<WeatherType> season = GenerateSeason(region, rootSeed, seasonInstanceIndex);
        int index = checked((dayInSeason - 1) * slotsPerDay + slotIndex);
        return season[index];
    }

    private ReadOnlyCollection<WeatherType> Generate(
        SeasonDefinition season,
        uint rootSeed,
        long seasonInstanceIndex)
    {
        int valueCount = checked(daysPerSeason * slotsPerDay);
        WeatherType[] values = new WeatherType[valueCount];
        WeatherType current = season.EntryWeatherType;
        if (!WeatherTypeUtility.IsDefined(current))
        {
            throw new InvalidOperationException(
                $"SeasonDefinition '{season.name}' has an invalid entry weather value {(int)current}.");
        }

        DeterministicWeatherRng rng = new DeterministicWeatherRng(
            DeterministicWeatherRng.DeriveSeasonSeed(rootSeed, seasonInstanceIndex));
        int valueIndex = 0;
        for (int day = 1; day <= daysPerSeason; day++)
        {
            for (int slot = 0; slot < slotsPerDay; slot++)
            {
                if (season.TryGetFixedWeather(day, slot, out WeatherType fixedWeather))
                    current = fixedWeather;
                else
                    current = RollTransition(season, current, ref rng);

                values[valueIndex++] = current;
            }
        }

        return Array.AsReadOnly(values);
    }

    private WeatherType RollTransition(
        SeasonDefinition season,
        WeatherType from,
        ref DeterministicWeatherRng rng)
    {
        if (!WeatherTypeUtility.IsDefined(from))
        {
            WarnInvalidRowOnce(season, from);
            return from;
        }

        long total = 0L;
        for (int toIndex = 0; toIndex < WeatherTypeUtility.Count; toIndex++)
        {
            if (!season.TryGetTransitionWeight(from, (WeatherType)toIndex, out int weight)
                || weight < 0)
            {
                WarnInvalidRowOnce(season, from);
                return from;
            }

            total += weight;
        }

        if (total <= 0L || total > uint.MaxValue)
        {
            WarnInvalidRowOnce(season, from);
            return from;
        }

        uint draw = rng.NextBelow((uint)total);
        long cumulative = 0L;
        for (int toIndex = 0; toIndex < WeatherTypeUtility.Count; toIndex++)
        {
            cumulative += season.GetTransitionWeight(from, (WeatherType)toIndex);
            if (cumulative > draw)
                return (WeatherType)toIndex;
        }

        WarnInvalidRowOnce(season, from);
        return from;
    }

    private SeasonDefinition ResolveSeason(
        ClimateRegionDefinition region,
        uint rootSeed,
        long seasonInstanceIndex)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));
        if (string.IsNullOrWhiteSpace(region.RegionId))
            throw new ArgumentException("A climate region requires a nonempty stable ID.", nameof(region));
        if (rootSeed == 0u)
            throw new ArgumentOutOfRangeException(nameof(rootSeed), "A weather root seed must be nonzero.");
        if (seasonInstanceIndex < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seasonInstanceIndex),
                seasonInstanceIndex,
                "A season instance index cannot be negative.");
        }

        SeasonTrackDefinition track = region.SeasonTrack;
        if (track == null || track.Count <= 0)
            throw new InvalidOperationException($"Climate region '{region.RegionId}' requires a nonempty season track.");

        int seasonOrdinal = (int)(seasonInstanceIndex % track.Count);
        if (!track.TryGetSeason(seasonOrdinal, out SeasonDefinition season) || season == null)
        {
            throw new InvalidOperationException(
                $"Climate region '{region.RegionId}' has no season definition at ordinal {seasonOrdinal}.");
        }

        return season;
    }

    private RegionCache GetOrCreateRegionCache(string regionId)
    {
        if (!cachesByRegion.TryGetValue(regionId, out RegionCache cache))
        {
            cache = new RegionCache();
            cachesByRegion.Add(regionId, cache);
        }

        return cache;
    }

    private static void Touch(RegionCache cache, CachedPeriod period)
    {
        cache.Recency.Remove(period.RecencyNode);
        cache.Recency.AddFirst(period.RecencyNode);
    }

    private static void Remove(RegionCache cache, CachedPeriod period)
    {
        cache.Periods.Remove(period.RecencyNode.Value);
        cache.Recency.Remove(period.RecencyNode);
    }

    private static void Trim(RegionCache cache)
    {
        while (cache.Periods.Count > CachedPeriodsPerRegion)
        {
            LinkedListNode<long> leastRecent = cache.Recency.Last;
            cache.Recency.RemoveLast();
            cache.Periods.Remove(leastRecent.Value);
        }
    }

    private void WarnInvalidRowOnce(SeasonDefinition season, WeatherType from)
    {
        InvalidTransitionRowKey key = new InvalidTransitionRowKey(season, from);
        if (loggedInvalidRows.Add(key))
        {
            Debug.LogWarning(
                $"[WeatherGenerator] Season '{season.name}' has an invalid transition row for {from}; retaining the current weather.",
                season);
        }
    }

    private sealed class RegionCache
    {
        public readonly Dictionary<long, CachedPeriod> Periods = new Dictionary<long, CachedPeriod>();
        public readonly LinkedList<long> Recency = new LinkedList<long>();
    }

    private sealed class CachedPeriod
    {
        private readonly ClimateRegionDefinition region;
        private readonly SeasonDefinition season;
        private readonly uint rootSeed;

        public CachedPeriod(
            ClimateRegionDefinition region,
            SeasonDefinition season,
            uint rootSeed,
            ReadOnlyCollection<WeatherType> values,
            LinkedListNode<long> recencyNode)
        {
            this.region = region;
            this.season = season;
            this.rootSeed = rootSeed;
            Values = values;
            RecencyNode = recencyNode;
        }

        public ReadOnlyCollection<WeatherType> Values { get; }
        public LinkedListNode<long> RecencyNode { get; }

        public bool Matches(
            ClimateRegionDefinition candidateRegion,
            SeasonDefinition candidateSeason,
            uint candidateRootSeed)
        {
            return ReferenceEquals(region, candidateRegion)
                && ReferenceEquals(season, candidateSeason)
                && rootSeed == candidateRootSeed;
        }
    }

    private readonly struct InvalidTransitionRowKey : IEquatable<InvalidTransitionRowKey>
    {
        private readonly SeasonDefinition season;
        private readonly WeatherType from;

        public InvalidTransitionRowKey(SeasonDefinition season, WeatherType from)
        {
            this.season = season;
            this.from = from;
        }

        public bool Equals(InvalidTransitionRowKey other)
        {
            return ReferenceEquals(season, other.season) && from == other.from;
        }

        public override bool Equals(object obj)
        {
            return obj is InvalidTransitionRowKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((season != null ? season.GetInstanceID() : 0) * 397) ^ (int)from;
            }
        }
    }
}
