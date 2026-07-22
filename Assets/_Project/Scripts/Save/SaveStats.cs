public class SaveStats
{
    public bool isEmpty;
    public int saveVersion;
    public string currentScene;
    public string lastSavedUtc;
    public float playTimeSeconds;
    public int unlockedAbilityCount;

    public static SaveStats FromSaveData(SaveData data)
    {
        if (data == null)
            return Empty();

        AbilitySaveData a = data.abilities ?? new AbilitySaveData();
        int count = 0;
        if (a.dashUnlocked)       count++;
        if (a.wallClingUnlocked)  count++;
        if (a.sprintUnlocked)     count++;
        if (a.wallLatchUnlocked)  count++;
        if (a.doubleJumpUnlocked) count++;
        if (a.driftCloakUnlocked) count++;
        if (a.spiritCastUnlocked) count++;
        if (a.bindUnlocked)       count++;

        return new SaveStats
        {
            isEmpty             = false,
            saveVersion         = data.meta?.saveVersion ?? 0,
            currentScene        = data.player?.currentScene ?? "",
            lastSavedUtc        = data.meta?.lastSavedUtc ?? "",
            playTimeSeconds     = data.meta?.playTimeSeconds ?? 0f,
            unlockedAbilityCount = count
        };
    }

    public static SaveStats Empty()
    {
        return new SaveStats { isEmpty = true };
    }
}
