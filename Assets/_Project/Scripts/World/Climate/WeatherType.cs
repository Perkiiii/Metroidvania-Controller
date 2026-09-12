/// <summary>
/// Weather values are persistence/content ordinals. Append new values only; never reorder or
/// remove an existing value because season matrices and saved weather state use these ordinals.
/// </summary>
public enum WeatherType
{
    Clear = 0,
    Cloudy = 1,
    Rain = 2,
    Storm = 3,
    Fog = 4
}

/// <summary>
/// Shared validation helpers for the append-only <see cref="WeatherType"/> enum.
/// </summary>
internal static class WeatherTypeUtility
{
    internal const int Count = (int)WeatherType.Fog + 1;

    internal static bool IsDefined(WeatherType type)
    {
        return (int)type >= 0 && (int)type < Count;
    }

    internal static bool TryFromInt(int value, out WeatherType type)
    {
        if (value >= 0 && value < Count)
        {
            type = (WeatherType)value;
            return true;
        }

        type = default;
        return false;
    }
}
