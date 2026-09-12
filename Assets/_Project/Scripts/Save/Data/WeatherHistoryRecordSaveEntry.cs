using System;

[Serializable]
public class WeatherHistoryRecordSaveEntry
{
    public int weatherType;
    public long startMinute;
    public long endMinuteExclusive;
}
