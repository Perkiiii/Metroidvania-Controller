using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One regional seasonal climate definition. Package 2 validates the data graph but does not roll
/// these weights or mutate weather at time boundaries.
/// </summary>
[CreateAssetMenu(menuName = "Project/World/Climate/Season Definition", fileName = "SeasonDefinition")]
public sealed class SeasonDefinition : ScriptableObject
{
    [SerializeField] private string seasonId;
    [SerializeField] private string displayName;
    [SerializeField] private WeatherType entryWeatherType;
    [SerializeField] private int[] transitionWeights =
        new int[WeatherTypeUtility.Count * WeatherTypeUtility.Count];
    [SerializeField] private List<FixedWeatherSlot> fixedWeatherSlots = new List<FixedWeatherSlot>();

    public string SeasonId => seasonId;
    public string DisplayName => displayName;
    public WeatherType EntryWeatherType => entryWeatherType;
    public IReadOnlyList<int> TransitionWeights => transitionWeights ?? Array.Empty<int>();
    public IReadOnlyList<FixedWeatherSlot> FixedWeatherSlots => fixedWeatherSlots != null
        ? fixedWeatherSlots
        : Array.Empty<FixedWeatherSlot>();

    public bool TryGetTransitionWeight(
        WeatherType from,
        WeatherType to,
        out int weight)
    {
        weight = 0;
        if (!WeatherTypeUtility.IsDefined(from)
            || !WeatherTypeUtility.IsDefined(to)
            || transitionWeights == null
            || transitionWeights.Length != WeatherTypeUtility.Count * WeatherTypeUtility.Count)
        {
            return false;
        }

        int index = (int)from * WeatherTypeUtility.Count + (int)to;
        weight = transitionWeights[index];
        return weight >= 0;
    }

    public int GetTransitionWeight(WeatherType from, WeatherType to)
    {
        if (!TryGetTransitionWeight(from, to, out int weight))
            throw new InvalidOperationException("SeasonDefinition has no valid transition weight for the requested FROM/TO weather values.");

        return weight;
    }

    public bool TryGetFixedWeather(
        int dayInSeason,
        int slotIndex,
        out WeatherType weatherType)
    {
        weatherType = default;
        if (fixedWeatherSlots == null || dayInSeason < 1 || slotIndex < 0)
            return false;

        for (int i = 0; i < fixedWeatherSlots.Count; i++)
        {
            FixedWeatherSlot fixedSlot = fixedWeatherSlots[i];
            if (fixedSlot != null
                && fixedSlot.DayInSeason == dayInSeason
                && fixedSlot.SlotIndex == slotIndex
                && WeatherTypeUtility.IsDefined(fixedSlot.WeatherType))
            {
                weatherType = fixedSlot.WeatherType;
                return true;
            }
        }

        return false;
    }

    public bool TryValidate(out string error)
    {
        return TryValidateInternal(null, 0, out error);
    }

    public bool TryValidate(int daysPerSeason, int weatherSlotCount, out string error)
    {
        if (daysPerSeason <= 0)
        {
            error = "daysPerSeason must be greater than zero when validating fixed weather slots.";
            return false;
        }

        if (weatherSlotCount <= 0)
        {
            error = "weatherSlotCount must be greater than zero when validating fixed weather slots.";
            return false;
        }

        return TryValidateInternal(daysPerSeason, weatherSlotCount, out error);
    }

    public bool TryValidate(int daysPerSeason, IReadOnlyList<int> weatherSlotHours, out string error)
    {
        if (weatherSlotHours == null)
        {
            error = "weatherSlotHours cannot be null when validating fixed weather slots.";
            return false;
        }

        return TryValidate(daysPerSeason, weatherSlotHours.Count, out error);
    }

    public bool TryValidate(CalendarConfig calendarConfig, WeatherSimulationConfig simulationConfig, out string error)
    {
        if (calendarConfig == null)
        {
            error = "SeasonDefinition validation requires a CalendarConfig.";
            return false;
        }

        if (simulationConfig == null)
        {
            error = "SeasonDefinition validation requires a WeatherSimulationConfig.";
            return false;
        }

        if (!calendarConfig.TryValidate(out string calendarError))
        {
            error = calendarError;
            return false;
        }

        if (!simulationConfig.TryValidate(out string simulationError))
        {
            error = simulationError;
            return false;
        }

        return TryValidate(calendarConfig.DaysPerSeason, simulationConfig.SlotCount, out error);
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException($"Invalid SeasonDefinition: {error}");
    }

    private bool TryValidateInternal(int? daysPerSeason, int weatherSlotCount, out string error)
    {
        if (!WeatherTypeUtility.IsDefined(entryWeatherType))
        {
            error = $"SeasonDefinition '{name}' has an invalid entry weather value {(int)entryWeatherType}.";
            return false;
        }

        int expectedLength = WeatherTypeUtility.Count * WeatherTypeUtility.Count;
        if (transitionWeights == null || transitionWeights.Length != expectedLength)
        {
            int actualLength = transitionWeights != null ? transitionWeights.Length : 0;
            error = $"SeasonDefinition '{name}' transition matrix length must be {expectedLength}, but was {actualLength}.";
            return false;
        }

        for (int fromIndex = 0; fromIndex < WeatherTypeUtility.Count; fromIndex++)
        {
            long rowTotal = 0L;
            for (int toIndex = 0; toIndex < WeatherTypeUtility.Count; toIndex++)
            {
                int weight = transitionWeights[fromIndex * WeatherTypeUtility.Count + toIndex];
                if (weight < 0)
                {
                    error = $"SeasonDefinition '{name}' transition weight at FROM {(WeatherType)fromIndex} / TO {(WeatherType)toIndex} cannot be negative.";
                    return false;
                }

                rowTotal += (long)weight;
            }

            if (rowTotal <= 0L)
            {
                error = $"SeasonDefinition '{name}' transition row FROM {(WeatherType)fromIndex} must have a total greater than zero.";
                return false;
            }

            if (rowTotal > uint.MaxValue)
            {
                error = $"SeasonDefinition '{name}' transition row FROM {(WeatherType)fromIndex} exceeds UInt32 weight total.";
                return false;
            }
        }

        if (fixedWeatherSlots == null)
        {
            error = $"SeasonDefinition '{name}' fixed weather slots list cannot be null.";
            return false;
        }

        HashSet<FixedWeatherSlotKey> keys = new HashSet<FixedWeatherSlotKey>();
        for (int i = 0; i < fixedWeatherSlots.Count; i++)
        {
            FixedWeatherSlot fixedSlot = fixedWeatherSlots[i];
            if (fixedSlot == null)
            {
                error = $"SeasonDefinition '{name}' fixed weather slot at index {i} is null.";
                return false;
            }

            if (!fixedSlot.TryValidate(daysPerSeason, weatherSlotCount, out string fixedSlotError))
            {
                error = $"SeasonDefinition '{name}' fixed weather slot at index {i}: {fixedSlotError}";
                return false;
            }

            if (!keys.Add(new FixedWeatherSlotKey(fixedSlot.DayInSeason, fixedSlot.SlotIndex)))
            {
                error = $"SeasonDefinition '{name}' contains duplicate fixed weather slot key day {fixedSlot.DayInSeason}, slot {fixedSlot.SlotIndex}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private readonly struct FixedWeatherSlotKey : IEquatable<FixedWeatherSlotKey>
    {
        public FixedWeatherSlotKey(int dayInSeason, int slotIndex)
        {
            DayInSeason = dayInSeason;
            SlotIndex = slotIndex;
        }

        private int DayInSeason { get; }
        private int SlotIndex { get; }

        public bool Equals(FixedWeatherSlotKey other)
        {
            return DayInSeason == other.DayInSeason && SlotIndex == other.SlotIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FixedWeatherSlotKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DayInSeason * 397) ^ SlotIndex;
            }
        }
    }
}

/// <summary>
/// One authored weather value at a concrete day/slot key. The class is serializable so Unity can
/// show a list of entries; null entries are rejected by <see cref="SeasonDefinition"/> validation.
/// </summary>
[Serializable]
public sealed class FixedWeatherSlot
{
    [SerializeField] private int dayInSeason = 1;
    [SerializeField] private int slotIndex;
    [SerializeField] private WeatherType weatherType;

    public int DayInSeason => dayInSeason;
    public int SlotIndex => slotIndex;
    public WeatherType WeatherType => weatherType;

    public FixedWeatherSlot()
    {
    }

    public FixedWeatherSlot(int day, int slot, WeatherType type)
    {
        dayInSeason = day;
        slotIndex = slot;
        weatherType = type;
    }

    public bool TryValidate(out string error)
    {
        return TryValidate(null, 0, out error);
    }

    public bool TryValidate(int daysPerSeason, int weatherSlotCount, out string error)
    {
        if (daysPerSeason <= 0)
        {
            error = "daysPerSeason must be greater than zero.";
            return false;
        }

        if (weatherSlotCount <= 0)
        {
            error = "weatherSlotCount must be greater than zero.";
            return false;
        }

        return TryValidate((int?)daysPerSeason, weatherSlotCount, out error);
    }

    public bool TryValidate(int daysPerSeason, IReadOnlyList<int> weatherSlotHours, out string error)
    {
        if (weatherSlotHours == null)
        {
            error = "weatherSlotHours cannot be null.";
            return false;
        }

        return TryValidate(daysPerSeason, weatherSlotHours.Count, out error);
    }

    internal bool TryValidate(int? daysPerSeason, int weatherSlotCount, out string error)
    {
        if (dayInSeason < 1)
        {
            error = "dayInSeason must be one-based and at least 1.";
            return false;
        }

        if (daysPerSeason.HasValue && dayInSeason > daysPerSeason.Value)
        {
            error = $"dayInSeason must be in the range 1..{daysPerSeason.Value}.";
            return false;
        }

        if (slotIndex < 0)
        {
            error = "slotIndex must identify a concrete non-negative slot.";
            return false;
        }

        if (weatherSlotCount > 0 && slotIndex >= weatherSlotCount)
        {
            error = $"slotIndex must be in the range 0..{weatherSlotCount - 1}.";
            return false;
        }

        if (!WeatherTypeUtility.IsDefined(weatherType))
        {
            error = $"weatherType value {(int)weatherType} is invalid.";
            return false;
        }

        error = null;
        return true;
    }

}
