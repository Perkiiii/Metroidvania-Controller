using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete authored set of simulated climate regions. Authored list order is stable and is the
/// order used by future deterministic weather resolution; lookup never relies on dictionary order.
/// </summary>
[CreateAssetMenu(menuName = "Project/World/Climate/Climate Region Catalog", fileName = "ClimateRegionCatalog")]
public sealed class ClimateRegionCatalog : ScriptableObject
{
    [SerializeField] private List<ClimateRegionDefinition> regions = new List<ClimateRegionDefinition>();

    [NonSerialized] private Dictionary<string, ClimateRegionDefinition> regionsById;
    [NonSerialized] private bool lookupBuilt;
    [NonSerialized] private bool lookupValid;

    public IReadOnlyList<ClimateRegionDefinition> Regions => regions != null
        ? regions
        : Array.Empty<ClimateRegionDefinition>();
    public int Count => regions != null ? regions.Count : 0;

    private void OnEnable()
    {
        InvalidateLookup();
    }

    private void OnValidate()
    {
        InvalidateLookup();
    }

    public bool TryGetRegion(string id, out ClimateRegionDefinition region)
    {
        region = null;
        if (string.IsNullOrEmpty(id) || !EnsureLookup())
            return false;

        return regionsById.TryGetValue(id, out region) && region != null;
    }

    public bool TryValidate(out string error)
    {
        if (regions == null)
        {
            error = "Climate region catalog entries cannot be null; the region list itself is null.";
            return false;
        }

        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < regions.Count; i++)
        {
            ClimateRegionDefinition region = regions[i];
            if (region == null)
            {
                error = $"Climate region catalog entry at index {i} is null.";
                return false;
            }

            if (!region.TryValidate(out string regionError))
            {
                error = regionError;
                return false;
            }

            if (!seenIds.Add(region.RegionId))
            {
                error = $"Duplicate climate region ID '{region.RegionId}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public bool TryValidate(int seasonsPerYear, out string error)
    {
        if (seasonsPerYear <= 0)
        {
            error = "seasonsPerYear must be greater than zero.";
            return false;
        }

        if (!TryValidate(out error))
            return false;

        for (int i = 0; i < regions.Count; i++)
        {
            if (!regions[i].TryValidate(seasonsPerYear, out string regionError))
            {
                error = regionError;
                return false;
            }
        }

        return true;
    }

    public bool TryValidate(CalendarConfig calendarConfig, out string error)
    {
        if (calendarConfig == null)
        {
            error = "Climate region catalog validation requires a CalendarConfig.";
            return false;
        }

        if (!calendarConfig.TryValidate(out string calendarError))
        {
            error = calendarError;
            return false;
        }

        return TryValidate(calendarConfig.SeasonsPerYear, out error);
    }

    public bool TryValidate(WorldTimeState worldTimeState, out string error)
    {
        if (worldTimeState == null)
        {
            error = "Climate region catalog validation requires a WorldTimeState.";
            return false;
        }

        return TryValidate(worldTimeState.SeasonsPerYear, out error);
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid ClimateRegionCatalog: {error}");
    }

    private bool EnsureLookup()
    {
        if (lookupBuilt)
            return lookupValid;

        lookupBuilt = true;
        lookupValid = TryValidate(out _);
        if (!lookupValid)
        {
            regionsById = null;
            return false;
        }

        regionsById = new Dictionary<string, ClimateRegionDefinition>(regions.Count, StringComparer.Ordinal);
        for (int i = 0; i < regions.Count; i++)
            regionsById.Add(regions[i].RegionId, regions[i]);

        return true;
    }

    private void InvalidateLookup()
    {
        lookupBuilt = false;
        lookupValid = false;
        regionsById = null;
    }
}
