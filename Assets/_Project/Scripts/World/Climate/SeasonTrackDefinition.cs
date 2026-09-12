using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps each global calendar season ordinal to one regional seasonal climate definition.
/// </summary>
[CreateAssetMenu(menuName = "Project/World/Climate/Season Track Definition", fileName = "SeasonTrackDefinition")]
public sealed class SeasonTrackDefinition : ScriptableObject
{
    [SerializeField] private List<SeasonDefinition> seasons = new List<SeasonDefinition>();

    public IReadOnlyList<SeasonDefinition> Seasons => seasons != null
        ? seasons
        : Array.Empty<SeasonDefinition>();
    public int Count => seasons != null ? seasons.Count : 0;

    public bool TryGetSeason(int seasonOrdinal, out SeasonDefinition season)
    {
        season = null;
        if (seasons == null || seasonOrdinal < 0 || seasonOrdinal >= seasons.Count)
            return false;

        season = seasons[seasonOrdinal];
        return season != null;
    }

    public bool TryValidate(out string error)
    {
        if (seasons == null)
        {
            error = "Season track entries cannot be null; the season list itself is null.";
            return false;
        }

        for (int i = 0; i < seasons.Count; i++)
        {
            if (seasons[i] == null)
            {
                error = $"Season track entry at ordinal {i} is null.";
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

        if (seasons.Count != seasonsPerYear)
        {
            error = $"Season track length must match seasonsPerYear ({seasonsPerYear}), but was {seasons.Count}.";
            return false;
        }

        return true;
    }

    public bool TryValidate(CalendarConfig calendarConfig, out string error)
    {
        if (calendarConfig == null)
        {
            error = "Season track validation requires a CalendarConfig.";
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
            error = "Season track validation requires a WorldTimeState.";
            return false;
        }

        return TryValidate(worldTimeState.SeasonsPerYear, out error);
    }

    public void ValidateOrThrow(int seasonsPerYear)
    {
        if (!TryValidate(seasonsPerYear, out string error))
            throw new InvalidOperationException($"Invalid SeasonTrackDefinition: {error}");
    }

}
