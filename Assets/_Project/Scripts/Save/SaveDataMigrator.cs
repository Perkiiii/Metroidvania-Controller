using System.Collections.Generic;

public static class SaveDataMigrator
{
    public const int CurrentSaveVersion = 3;

    public static void Migrate(SaveData data)
    {
        int originalVersion = data.meta?.saveVersion ?? 0;

        data.meta       ??= new MetaSaveData();
        data.player     ??= new PlayerSaveData();
        data.abilities  ??= new AbilitySaveData();
        data.health     ??= new HealthSaveData();
        data.resource   ??= new ResourceSaveData();
        data.world      ??= new WorldSaveData();

        data.world.collectedPickupIds          ??= new List<string>();
        data.player.currentScene               ??= "";
        data.player.activeRespawnSceneName     ??= "";
        data.player.activeRespawnMarkerKey     ??= "";
        data.player.activeHazardRespawnMarkerKey ??= "";
        data.meta.lastSavedUtc                 ??= "";

        if (originalVersion < 2
            && string.IsNullOrEmpty(data.player.activeRespawnSceneName)
            && !string.IsNullOrEmpty(data.player.activeRespawnMarkerKey))
        {
            // Best-effort v1 migration: currentScene may not be the checkpoint scene
            // if the player saved after leaving it. Re-activate checkpoints after
            // migration to write an authoritative scene + marker pair.
            data.player.activeRespawnSceneName = data.player.currentScene;
        }

        if (originalVersion < 3)
        {
            // Version 2 and earlier had no health/resource sections. Leave their
            // markers clear so each state asset supplies its authored fresh default.
            data.health.initialized = false;
            data.resource.initialized = false;
        }

        data.meta.saveVersion = CurrentSaveVersion;
    }
}
