using System;

[Serializable]
public class SaveData
{
    public MetaSaveData meta       = new MetaSaveData();
    public PlayerSaveData player   = new PlayerSaveData();
    public AbilitySaveData abilities = new AbilitySaveData();
    public WorldSaveData world     = new WorldSaveData();
}
