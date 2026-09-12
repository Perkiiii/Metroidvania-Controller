using System;

[Serializable]
public class WeatherOverrideSaveEntry
{
    public string regionId = string.Empty;
    public long absoluteDayIndex;
    public int slotIndex;
    public int weatherType;
    public string sourceTag = string.Empty;
}
