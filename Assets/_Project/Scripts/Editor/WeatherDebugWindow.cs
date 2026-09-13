using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small Play Mode-only weather inspection and override window for the active room context.
/// </summary>
public sealed class WeatherDebugWindow : EditorWindow
{
    private const string WorldTimeAssetPath =
        "Assets/_Project/ScriptableObjects/World/WorldTimeState.asset";
    private const string WorldWeatherAssetPath =
        "Assets/_Project/ScriptableObjects/World/WorldWeatherState.asset";
    private WorldTimeState worldTimeState;
    private WorldWeatherState worldWeatherState;

    [MenuItem("Tools/Underbrew/Weather Debug")]
    private static void ShowWindow()
    {
        GetWindow<WeatherDebugWindow>("Weather Debug");
    }

    private void OnEnable()
    {
        LoadStateAssets();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        LoadStateAssets();

        EditorGUILayout.LabelField("Underbrew Weather Debug", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        WorldTimeSnapshot timeSnapshot = default;
        bool hasTimeSnapshot = false;
        bool timeIsLoaded = worldTimeState != null && worldTimeState.IsLoaded;
        if (timeIsLoaded)
        {
            // WorldTimeState.Current initializes runtime state when queried, so keep this behind
            // the explicit load check. A malformed asset should leave the debug controls disabled.
            try
            {
                timeSnapshot = worldTimeState.Current;
                hasTimeSnapshot = true;
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(
                    $"WorldTimeState could not provide a snapshot: {exception.Message}",
                    MessageType.Error);
            }
        }

        bool weatherIsLoaded = worldWeatherState != null && worldWeatherState.IsLoaded;
        RoomClimateContext activeContext = null;
        bool hasOneEnabledContext = TryResolveActiveContext(
            SceneManager.GetActiveScene(),
            out activeContext);
        bool hasValidRegion = hasOneEnabledContext
            && activeContext != null
            && !string.IsNullOrWhiteSpace(activeContext.RegionId)
            && worldWeatherState != null
            && worldWeatherState.ClimateRegionCatalog != null
            && worldWeatherState.ClimateRegionCatalog.TryGetRegion(
                activeContext.RegionId,
                out _);

        bool configIsValid = false;
        string configStatus = "<missing>";
        WeatherSimulationConfig weatherConfig = worldWeatherState != null
            ? worldWeatherState.WeatherSimulationConfig
            : null;
        if (weatherConfig != null)
        {
            configIsValid = weatherConfig.TryValidate(out string configError);
            configStatus = configIsValid ? "Valid" : configError;
        }

        ActiveSlotResolution activeSlot = default;
        bool hasActiveSlot = hasTimeSnapshot
            && configIsValid
            && TryResolveActiveSlot(
                timeSnapshot,
                weatherConfig.WeatherSlotHours,
                out activeSlot);

        WeatherSnapshot actualWeather = default;
        bool hasActualWeather = weatherIsLoaded
            && hasValidRegion
            && worldWeatherState.TryGetWeather(activeContext.RegionId, out actualWeather);

        DrawStateSummary(
            timeIsLoaded,
            hasTimeSnapshot,
            timeSnapshot,
            weatherIsLoaded,
            activeContext,
            hasOneEnabledContext,
            hasValidRegion,
            configStatus,
            configIsValid,
            hasActiveSlot,
            activeSlot,
            hasActualWeather,
            actualWeather);

        bool canUseActions = CanUseActions(
            Application.isPlaying,
            timeIsLoaded,
            weatherIsLoaded,
            hasTimeSnapshot,
            hasValidRegion,
            configIsValid,
            hasActiveSlot,
            hasActualWeather);

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledGroupScope(!canUseActions))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Clear"))
            {
                string regionId = activeContext.RegionId;
                long activeSlotDay = activeSlot.AbsoluteDayIndex;
                int slotIndex = activeSlot.SlotIndex;
                worldWeatherState.SetOverride(new WeatherOverrideEntry(
                    regionId,
                    activeSlotDay,
                    slotIndex,
                    WeatherType.Clear,
                    "editor_weather_debug"));
            }

            if (GUILayout.Button("Force Rain"))
            {
                string regionId = activeContext.RegionId;
                long activeSlotDay = activeSlot.AbsoluteDayIndex;
                int slotIndex = activeSlot.SlotIndex;
                worldWeatherState.SetOverride(new WeatherOverrideEntry(
                    regionId,
                    activeSlotDay,
                    slotIndex,
                    WeatherType.Rain,
                    "editor_weather_debug"));
            }

            if (GUILayout.Button("Force Storm"))
            {
                string regionId = activeContext.RegionId;
                long activeSlotDay = activeSlot.AbsoluteDayIndex;
                int slotIndex = activeSlot.SlotIndex;
                worldWeatherState.SetOverride(new WeatherOverrideEntry(
                    regionId,
                    activeSlotDay,
                    slotIndex,
                    WeatherType.Storm,
                    "editor_weather_debug"));
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Clear Current Override"))
            {
                string regionId = activeContext.RegionId;
                long activeSlotDay = activeSlot.AbsoluteDayIndex;
                int slotIndex = activeSlot.SlotIndex;
                worldWeatherState.ClearOverride(regionId, activeSlotDay, slotIndex);
            }
        }

        if (!canUseActions)
        {
            EditorGUILayout.HelpBox(
                "Controls require Play Mode, loaded world states, one valid enabled room context, a valid active weather slot, and an available actual-weather query.",
                MessageType.Info);
        }
    }

    private void LoadStateAssets()
    {
        worldTimeState = AssetDatabase.LoadAssetAtPath<WorldTimeState>(WorldTimeAssetPath);
        worldWeatherState = AssetDatabase.LoadAssetAtPath<WorldWeatherState>(WorldWeatherAssetPath);
    }

    private static void DrawStateSummary(
        bool timeIsLoaded,
        bool hasTimeSnapshot,
        WorldTimeSnapshot timeSnapshot,
        bool weatherIsLoaded,
        RoomClimateContext activeContext,
        bool hasOneEnabledContext,
        bool hasValidRegion,
        string configStatus,
        bool configIsValid,
        bool hasActiveSlot,
        ActiveSlotResolution activeSlot,
        bool hasActualWeather,
        WeatherSnapshot actualWeather)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("World Time", timeIsLoaded ? "Loaded" : "Not loaded");
        EditorGUILayout.LabelField(
            "Absolute Day",
            hasTimeSnapshot ? timeSnapshot.TotalDays.ToString() : "<unavailable>");
        EditorGUILayout.LabelField("World Weather", weatherIsLoaded ? "Loaded" : "Not loaded");
        EditorGUILayout.LabelField(
            "Room Region ID",
            activeContext != null ? activeContext.RegionId : "<requires one enabled context>");
        EditorGUILayout.LabelField(
            "Context",
            !hasOneEnabledContext
                ? "Invalid (need exactly one enabled context)"
                : hasValidRegion
                    ? "Valid"
                    : "Invalid region ID");
        EditorGUILayout.LabelField("Weather Config", configStatus);

        if (!configIsValid)
        {
            EditorGUILayout.LabelField("Active Slot Index", "<invalid slot configuration>");
            EditorGUILayout.LabelField("Active Slot Hour", "<invalid slot configuration>");
            EditorGUILayout.LabelField("Override Key Day", "<invalid slot configuration>");
        }
        else if (!hasActiveSlot)
        {
            EditorGUILayout.LabelField("Active Slot Index", "<none>");
            EditorGUILayout.LabelField("Active Slot Hour", "<none>");
            EditorGUILayout.LabelField(
                "Override Key Day",
                hasTimeSnapshot
                    ? "<invalid: day 0 before first daily slot>"
                    : "<unavailable>");
        }
        else
        {
            EditorGUILayout.LabelField("Active Slot Index", activeSlot.SlotIndex.ToString());
            EditorGUILayout.LabelField("Active Slot Hour", $"{activeSlot.SlotHour:00}:00");
            EditorGUILayout.LabelField("Override Key Day", activeSlot.AbsoluteDayIndex.ToString());
            EditorGUILayout.LabelField(
                "Slot Day",
                activeSlot.IsPreviousDay ? "Prior day final slot" : "Current day slot");
        }

        EditorGUILayout.LabelField(
            "Actual Weather",
            hasActualWeather ? actualWeather.WeatherType.ToString() : "<unavailable>");
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Resolves only enabled contexts beneath the active scene roots. A disabled context does not
    /// become a second active room, while two enabled contexts make the room identity ambiguous.
    /// </summary>
    internal static bool TryResolveActiveContext(
        Scene scene,
        out RoomClimateContext context)
    {
        context = null;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            RoomClimateContext[] contexts = roots[rootIndex]
                .GetComponentsInChildren<RoomClimateContext>(true);
            for (int contextIndex = 0; contextIndex < contexts.Length; contextIndex++)
            {
                RoomClimateContext candidate = contexts[contextIndex];
                if (candidate == null
                    || candidate.gameObject.scene != scene
                    || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (context != null)
                {
                    context = null;
                    return false;
                }

                context = candidate;
            }
        }

        return context != null;
    }

    /// <summary>
    /// Mirrors WorldWeatherState's active-slot policy: the latest slot at or before the current
    /// hour wins; before the first slot, the prior day's final slot is used when one exists.
    /// </summary>
    internal static bool TryResolveActiveSlot(
        WorldTimeSnapshot snapshot,
        IReadOnlyList<int> slotHours,
        out ActiveSlotResolution resolution)
    {
        resolution = default;
        if (!AreValidSlotHours(slotHours))
            return false;

        for (int slotIndex = slotHours.Count - 1; slotIndex >= 0; slotIndex--)
        {
            if (slotHours[slotIndex] <= snapshot.Hour)
            {
                resolution = new ActiveSlotResolution(
                    snapshot.TotalDays,
                    slotIndex,
                    slotHours[slotIndex],
                    false);
                return true;
            }
        }

        if (snapshot.TotalDays <= 0L)
            return false;

        int priorDaySlotIndex = slotHours.Count - 1;
        resolution = new ActiveSlotResolution(
            snapshot.TotalDays - 1L,
            priorDaySlotIndex,
            slotHours[priorDaySlotIndex],
            true);
        return true;
    }

    internal static bool CanUseActions(
        bool isPlaying,
        bool timeIsLoaded,
        bool weatherIsLoaded,
        bool hasTimeSnapshot,
        bool hasValidRegion,
        bool configIsValid,
        bool hasActiveSlot,
        bool hasActualWeather)
    {
        return isPlaying
            && timeIsLoaded
            && weatherIsLoaded
            && hasTimeSnapshot
            && hasValidRegion
            && configIsValid
            && hasActiveSlot
            && hasActualWeather;
    }

    private static bool AreValidSlotHours(IReadOnlyList<int> slotHours)
    {
        if (slotHours == null || slotHours.Count == 0)
            return false;

        int previous = -1;
        for (int i = 0; i < slotHours.Count; i++)
        {
            int hour = slotHours[i];
            if (hour < 0 || hour >= CalendarConfig.HoursPerDay || hour <= previous)
                return false;

            previous = hour;
        }

        return true;
    }

    internal readonly struct ActiveSlotResolution
    {
        public ActiveSlotResolution(
            long absoluteDayIndex,
            int slotIndex,
            int slotHour,
            bool isPreviousDay)
        {
            AbsoluteDayIndex = absoluteDayIndex;
            SlotIndex = slotIndex;
            SlotHour = slotHour;
            IsPreviousDay = isPreviousDay;
        }

        public long AbsoluteDayIndex { get; }
        public int SlotIndex { get; }
        public int SlotHour { get; }
        public bool IsPreviousDay { get; }
    }
}
