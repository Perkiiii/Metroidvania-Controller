using System.Collections.Generic;

public static class SaveDataMigrator
{
    public const int CurrentSaveVersion = 1;

    public static void Migrate(SaveData data)
    {
        int originalVersion = data.meta?.saveVersion ?? 0;

        data.meta       ??= new MetaSaveData();
        data.player     ??= new PlayerSaveData();
        data.abilities  ??= new AbilitySaveData();
        data.world      ??= new WorldSaveData();

        data.world.collectedPickupIds          ??= new List<string>();
        data.player.currentScene               ??= "";
        data.player.activeRespawnMarkerKey     ??= "";
        data.player.activeHazardRespawnMarkerKey ??= "";
        data.meta.lastSavedUtc                 ??= "";

        // TODO: Add version-gated migration blocks here as schema evolves.
        // Example:
        // if (originalVersion < 2)
        // {
        //     // migration logic for version 1 → 2
        // }

        data.meta.saveVersion = CurrentSaveVersion;
    }
}
