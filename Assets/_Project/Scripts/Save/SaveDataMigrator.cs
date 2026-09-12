using System.Collections.Generic;

public static class SaveDataMigrator
{
    public const int CurrentSaveVersion = 6;

    public static void Migrate(SaveData data)
    {
        int originalVersion = data.meta?.saveVersion ?? 0;

        data.meta       ??= new MetaSaveData();
        data.player     ??= new PlayerSaveData();
        data.abilities  ??= new AbilitySaveData();
        data.health     ??= new HealthSaveData();
        data.resource   ??= new ResourceSaveData();
        data.world      ??= new WorldSaveData();
        data.worldTime  ??= new WorldTimeSaveData();
        data.worldWeather ??= new WorldWeatherSaveData();

        data.world.collectedPickupIds          ??= new List<string>();
        data.world.visitedRoomIds              ??= new List<string>();
        data.world.defeatedEncounterIds        ??= new List<string>();
        data.world.objectStates                ??= new List<WorldObjectStateEntry>();
        data.player.currentScene               ??= "";
        data.player.activeRespawnSceneName     ??= "";
        data.player.activeRespawnMarkerKey     ??= "";
        data.player.activeHazardRespawnMarkerKey ??= "";
        data.meta.lastSavedUtc                 ??= "";

        // Weather is a nested save graph. Normalize every collection and diagnostic string on
        // every migration pass so freshly deserialized, partially-authored, and future saves all
        // present the same non-null shape to WorldWeatherState.
        data.worldWeather.regions  ??= new List<RegionWeatherSaveEntry>();
        data.worldWeather.overrides ??= new List<WeatherOverrideSaveEntry>();
        for (int regionIndex = 0; regionIndex < data.worldWeather.regions.Count; regionIndex++)
        {
            RegionWeatherSaveEntry region = data.worldWeather.regions[regionIndex];
            if (region == null)
                continue;

            region.regionId ??= "";
            region.history ??= new List<WeatherHistoryRecordSaveEntry>();
        }

        for (int overrideIndex = 0; overrideIndex < data.worldWeather.overrides.Count; overrideIndex++)
        {
            WeatherOverrideSaveEntry weatherOverride = data.worldWeather.overrides[overrideIndex];
            if (weatherOverride == null)
                continue;

            weatherOverride.regionId ??= "";
            weatherOverride.sourceTag ??= "";
        }

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

        if (originalVersion < 5)
        {
            // Time was not persisted before version 5. Keep the marker clear so the
            // WorldTimeState asset supplies its authored fresh-save value rather than
            // fabricating a timestamp from an older save shape.
            data.worldTime.initialized = false;
        }

        if (originalVersion < 6)
        {
            // Weather did not exist before version 6. Keep its marker clear so completion uses
            // authored regional initial weather and starts a truthful history boundary.
            data.worldWeather.initialized = false;
        }

        // Version 3 and earlier had no room/encounter/object-state world sections.
        // No transform is needed: the null-coalescing above already leaves them empty,
        // which is the correct fresh state for saves that predate WorldStateRegistry.

        data.meta.saveVersion = CurrentSaveVersion;
    }
}
