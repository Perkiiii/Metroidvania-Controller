using System;

/// <summary>
/// Immutable command value for one exact-slot weather override.
/// </summary>
public readonly struct WeatherOverrideEntry : IEquatable<WeatherOverrideEntry>
{
    private readonly string sourceTag;

    public WeatherOverrideEntry(
        string regionId,
        long absoluteDayIndex,
        int slotIndex,
        WeatherType weatherType,
        string sourceTag = null)
    {
        RegionId = regionId;
        AbsoluteDayIndex = absoluteDayIndex;
        SlotIndex = slotIndex;
        WeatherType = weatherType;
        this.sourceTag = sourceTag ?? string.Empty;
    }

    public string RegionId { get; }
    public long AbsoluteDayIndex { get; }
    public int SlotIndex { get; }
    public WeatherType WeatherType { get; }
    public string SourceTag => sourceTag ?? string.Empty;

    /// <summary>
    /// Gets whether the value has a complete nonnegative key and a defined weather ordinal.
    /// Slot-count validation remains a WorldWeatherState concern because it is configuration-specific.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(RegionId)
        && AbsoluteDayIndex >= 0L
        && SlotIndex >= 0
        && WeatherTypeUtility.IsDefined(WeatherType);

    public bool Equals(WeatherOverrideEntry other)
    {
        return string.Equals(RegionId, other.RegionId, StringComparison.Ordinal)
            && AbsoluteDayIndex == other.AbsoluteDayIndex
            && SlotIndex == other.SlotIndex
            && WeatherType == other.WeatherType
            && string.Equals(SourceTag, other.SourceTag, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is WeatherOverrideEntry other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = RegionId == null ? 0 : StringComparer.Ordinal.GetHashCode(RegionId);
            hash = (hash * 397) ^ AbsoluteDayIndex.GetHashCode();
            hash = (hash * 397) ^ SlotIndex;
            hash = (hash * 397) ^ (int)WeatherType;
            return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SourceTag);
        }
    }

    public static bool operator ==(WeatherOverrideEntry left, WeatherOverrideEntry right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WeatherOverrideEntry left, WeatherOverrideEntry right)
    {
        return !left.Equals(right);
    }
}
