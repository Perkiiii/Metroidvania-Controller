using System;

[Serializable]
public class MetaSaveData
{
    public int saveVersion = 2;
    public string lastSavedUtc = "";
    public float playTimeSeconds = 0f;
}
