using System;

[Serializable]
public class MetaSaveData
{
    public int saveVersion = 3;
    public string lastSavedUtc = "";
    public float playTimeSeconds = 0f;
}
