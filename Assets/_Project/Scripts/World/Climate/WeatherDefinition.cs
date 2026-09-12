using UnityEngine;

// Authored simulation/gameplay metadata for one weather type. Presentation assets belong to a
// future environment presentation layer and intentionally do not appear here.
[CreateAssetMenu(menuName = "Project/World/Climate/Weather Definition", fileName = "WeatherDefinition")]
public sealed class WeatherDefinition : ScriptableObject
{
    [SerializeField] private WeatherType type;
    [SerializeField] private string displayName;
    [SerializeField] private bool isPrecipitation;
    [SerializeField] private bool isSevereWeather;

    public WeatherType Type => type;
    public string DisplayName => displayName;
    public bool IsPrecipitation => isPrecipitation;
    public bool IsSevereWeather => isSevereWeather;

    public bool TryValidate(out string error)
    {
        if (!WeatherTypeUtility.IsDefined(type))
        {
            error = $"Weather definition '{name}' has an invalid weather type value {(int)type}.";
            return false;
        }

        error = null;
        return true;
    }

    public void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new System.InvalidOperationException($"Invalid WeatherDefinition: {error}");
    }

}
