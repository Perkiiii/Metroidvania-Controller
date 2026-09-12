using System;
using UnityEngine;

/// <summary>
/// Stable authored identity and climate mapping for one simulated world region.
/// </summary>
[CreateAssetMenu(menuName = "Project/World/Climate/Climate Region Definition", fileName = "ClimateRegionDefinition")]
public sealed class ClimateRegionDefinition : ScriptableObject
{
    [SerializeField] private string regionId;
    [SerializeField] private string displayName;
    [SerializeField] private SeasonTrackDefinition seasonTrack;
    [SerializeField] private WeatherType initialWeatherType;

    public string RegionId => regionId;
    public string DisplayName => displayName;
    public SeasonTrackDefinition SeasonTrack => seasonTrack;
    public WeatherType InitialWeatherType => initialWeatherType;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(regionId))
        {
            error = $"Climate region '{name}' must have a nonempty stable region ID.";
            return false;
        }

        if (seasonTrack == null)
        {
            error = $"Climate region '{regionId}' requires a SeasonTrackDefinition.";
            return false;
        }

        if (!WeatherTypeUtility.IsDefined(initialWeatherType))
        {
            error = $"Climate region '{regionId}' has an invalid initial weather value {(int)initialWeatherType}.";
            return false;
        }

        error = null;
        return true;
    }

    public bool TryValidate(int seasonsPerYear, out string error)
    {
        if (!TryValidate(out error))
            return false;

        return seasonTrack.TryValidate(seasonsPerYear, out error);
    }

    public bool TryValidate(CalendarConfig calendarConfig, out string error)
    {
        if (!TryValidate(out error))
            return false;

        return seasonTrack.TryValidate(calendarConfig, out error);
    }

    public bool TryValidate(WorldTimeState worldTimeState, out string error)
    {
        if (!TryValidate(out error))
            return false;

        return seasonTrack.TryValidate(worldTimeState, out error);
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid ClimateRegionDefinition: {error}");
    }

}
