using System;
using System.Collections.Generic;

[Serializable]
public class RegionWeatherSaveEntry
{
    public string regionId = string.Empty;
    public int currentWeatherType;
    public long currentWeatherStartMinute;
    public long historyAvailableFromMinute;
    public uint rootSeed;
    public List<WeatherHistoryRecordSaveEntry> history = new List<WeatherHistoryRecordSaveEntry>();
}
