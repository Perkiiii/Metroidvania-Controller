using System;
using System.Collections.Generic;
using UnityEngine;

// Global weather slot policy and the one-to-one weather metadata lookup. This asset carries no
// regional runtime state and, in Package 2, its slots are configuration only.
[CreateAssetMenu(menuName = "Project/World/Climate/Weather Simulation Config", fileName = "WeatherSimulationConfig")]
public sealed class WeatherSimulationConfig : ScriptableObject
{
    [SerializeField] private List<int> weatherSlotHours = new List<int> { 6, 18 };
    [Min(0)] [SerializeField] private int maxForecastDaysAhead = 28;
    [SerializeField] private List<WeatherDefinition> weatherDefinitions = new List<WeatherDefinition>();

    [NonSerialized] private Dictionary<WeatherType, WeatherDefinition> definitionsByType;
    [NonSerialized] private bool lookupBuilt;
    [NonSerialized] private bool lookupValid;

    public IReadOnlyList<int> WeatherSlotHours => weatherSlotHours != null
        ? weatherSlotHours
        : Array.Empty<int>();
    public int MaxForecastDaysAhead => maxForecastDaysAhead;
    public IReadOnlyList<WeatherDefinition> WeatherDefinitions => weatherDefinitions != null
        ? weatherDefinitions
        : Array.Empty<WeatherDefinition>();

    public int SlotCount => weatherSlotHours != null ? weatherSlotHours.Count : 0;

    private void OnEnable()
    {
        InvalidateLookup();
    }

    private void OnValidate()
    {
        InvalidateLookup();
    }

    public bool TryGetDefinition(WeatherType weatherType, out WeatherDefinition definition)
    {
        definition = null;
        if (!WeatherTypeUtility.IsDefined(weatherType) || !EnsureLookup())
            return false;

        return definitionsByType.TryGetValue(weatherType, out definition) && definition != null;
    }

    public bool TryValidate(out string error)
    {
        if (weatherSlotHours == null || weatherSlotHours.Count == 0)
        {
            error = "Weather slot hours must be nonempty.";
            return false;
        }

        int previous = -1;
        for (int i = 0; i < weatherSlotHours.Count; i++)
        {
            int hour = weatherSlotHours[i];
            if (hour < 0 || hour >= CalendarConfig.HoursPerDay)
            {
                error = $"Weather slot hour at index {i} must be in the range 0..23.";
                return false;
            }

            if (hour <= previous)
            {
                error = $"Weather slot hours must be sorted strictly ascending and unique; index {i} is {hour}.";
                return false;
            }

            previous = hour;
        }

        if (maxForecastDaysAhead < 0)
        {
            error = "maxForecastDaysAhead cannot be negative.";
            return false;
        }

        if (weatherDefinitions == null)
        {
            error = "Weather definitions must be non-null and contain exactly one definition per WeatherType.";
            return false;
        }

        bool[] seen = new bool[WeatherTypeUtility.Count];
        for (int i = 0; i < weatherDefinitions.Count; i++)
        {
            WeatherDefinition definition = weatherDefinitions[i];
            if (definition == null)
            {
                error = $"Weather definition at index {i} is null.";
                return false;
            }

            if (!definition.TryValidate(out string definitionError))
            {
                error = definitionError;
                return false;
            }

            int weatherIndex = (int)definition.Type;
            if (seen[weatherIndex])
            {
                error = $"Duplicate WeatherDefinition for {definition.Type}.";
                return false;
            }

            seen[weatherIndex] = true;
        }

        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i])
            {
                WeatherType missing = (WeatherType)i;
                error = $"Missing WeatherDefinition for {missing}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid WeatherSimulationConfig: {error}");
    }

    private bool EnsureLookup()
    {
        if (lookupBuilt)
            return lookupValid;

        lookupBuilt = true;
        lookupValid = TryValidate(out _);
        if (!lookupValid)
        {
            definitionsByType = null;
            return false;
        }

        definitionsByType = new Dictionary<WeatherType, WeatherDefinition>(WeatherTypeUtility.Count);
        for (int i = 0; i < weatherDefinitions.Count; i++)
        {
            WeatherDefinition definition = weatherDefinitions[i];
            definitionsByType.Add(definition.Type, definition);
        }

        return true;
    }

    private void InvalidateLookup()
    {
        lookupBuilt = false;
        lookupValid = false;
        definitionsByType = null;
    }
}
