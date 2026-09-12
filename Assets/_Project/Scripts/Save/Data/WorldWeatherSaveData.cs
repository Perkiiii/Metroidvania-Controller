using System;
using System.Collections.Generic;

[Serializable]
public class WorldWeatherSaveData
{
    public bool initialized;
    public List<RegionWeatherSaveEntry> regions = new List<RegionWeatherSaveEntry>();
    public List<WeatherOverrideSaveEntry> overrides = new List<WeatherOverrideSaveEntry>();
}
