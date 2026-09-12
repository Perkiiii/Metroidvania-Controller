using System;

[Serializable]
public class SaveData
{
    public MetaSaveData meta       = new MetaSaveData();
    public PlayerSaveData player   = new PlayerSaveData();
    public AbilitySaveData abilities = new AbilitySaveData();
    public HealthSaveData health   = new HealthSaveData();
    public ResourceSaveData resource = new ResourceSaveData();
    public WorldSaveData world     = new WorldSaveData();
    public WorldTimeSaveData worldTime = new WorldTimeSaveData();
    public WorldWeatherSaveData worldWeather = new WorldWeatherSaveData();
}
