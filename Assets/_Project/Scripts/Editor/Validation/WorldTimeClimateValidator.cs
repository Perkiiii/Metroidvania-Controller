using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only validation for the authored world-time climate graph and scene-local room context
/// seam. This validator only reads assets/scenes; it never saves, rewrites, or creates content.
/// </summary>
public static class WorldTimeClimateValidator
{
    private const string CatalogAssetPath = "Assets/_Project/ScriptableObjects/World/ClimateRegionCatalog.asset";
    private const string WorldTimeAssetPath = "Assets/_Project/ScriptableObjects/World/WorldTimeState.asset";
    private const string BootSceneName = "Boot";

    /// <summary>
    /// Minimal scene-local data used by the pure context rules and EditMode tests.
    /// </summary>
    internal readonly struct ContextInfo
    {
        public readonly string ObjectPath;
        public readonly string RegionId;

        public ContextInfo(string objectPath, string regionId)
        {
            ObjectPath = objectPath ?? string.Empty;
            RegionId = regionId ?? string.Empty;
        }
    }

    [MenuItem("Tools/Project/Validate World Time & Climate")]
    public static void ValidateWorldTimeClimate()
    {
        if (HasDirtyLoadedScene())
        {
            Debug.LogWarning(
                "[WorldTimeClimateValidator] Validation cancelled because a loaded scene has unsaved changes. Save or close the scene and run validation again.");
            return;
        }

        int issues = ValidateEnabledBuildSettingsScenes(out int sceneCount, out int contextCount);
        string summary = $"Checked {sceneCount} enabled Build Settings scene(s), {contextCount} RoomClimateContext instance(s).";
        if (issues == 0)
            Debug.Log($"[WorldTimeClimateValidator] {summary} No issues found.");
        else
            Debug.LogWarning($"[WorldTimeClimateValidator] {summary} Found {issues} issue(s). See earlier logs.");
    }

    /// <summary>
    /// Runs the same non-mutating project pass used by the menu item. Kept internal so the real
    /// EditMode Test Runner can exercise the authored Build Settings scene contract.
    /// </summary>
    internal static int ValidateEnabledBuildSettingsScenesForTests()
    {
        return ValidateEnabledBuildSettingsScenes(out _, out _);
    }

    /// <summary>
    /// Validates the authored catalog/season graph and builds the stable region-ID set used by
    /// scene validation. No runtime database or asset mutation is performed.
    /// </summary>
    internal static bool TryBuildRegionLookup(
        ClimateRegionCatalog catalog,
        WorldTimeState worldTimeState,
        out HashSet<string> regionIds,
        out string error)
    {
        regionIds = new HashSet<string>(StringComparer.Ordinal);
        error = null;

        if (catalog == null)
        {
            error = "ClimateRegionCatalog asset is missing.";
            return false;
        }

        if (worldTimeState == null)
        {
            error = "WorldTimeState asset is missing; the climate season graph cannot be checked against the calendar.";
            return false;
        }

        try
        {
            if (!catalog.TryValidate(worldTimeState, out error))
                return false;

            if (catalog.Count == 0)
            {
                error = "ClimateRegionCatalog contains no regions; room contexts have no valid lookup target.";
                return false;
            }

            for (int i = 0; i < catalog.Regions.Count; i++)
            {
                ClimateRegionDefinition region = catalog.Regions[i];
                if (region == null)
                {
                    error = $"ClimateRegionCatalog entry at index {i} is null.";
                    regionIds.Clear();
                    return false;
                }

                string regionId = region.RegionId;
                if (!regionIds.Add(regionId))
                {
                    error = $"ClimateRegionCatalog contains duplicate region ID '{regionId}'.";
                    regionIds.Clear();
                    return false;
                }

                if (!catalog.TryGetRegion(regionId, out ClimateRegionDefinition resolved)
                    || !ReferenceEquals(region, resolved))
                {
                    error = $"ClimateRegionCatalog lookup could not resolve authored region ID '{regionId}'.";
                    regionIds.Clear();
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            regionIds.Clear();
            error = $"ClimateRegionCatalog validation threw {exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Pure scene rule evaluation. Multiple scenes may pass the same region ID; uniqueness is not
    /// a rule of the climate catalog seam.
    /// </summary>
    internal static List<string> GetContextIssues(
        string sceneName,
        string scenePath,
        bool isBoot,
        IReadOnlyList<ContextInfo> contexts,
        ISet<string> validRegionIds)
    {
        List<string> issues = new List<string>();
        int contextCount = contexts != null ? contexts.Count : 0;
        string location = string.IsNullOrWhiteSpace(scenePath)
            ? $"Scene '{sceneName}'"
            : $"Scene '{sceneName}' at '{scenePath}'";

        if (isBoot)
        {
            if (contextCount > 0)
            {
                issues.Add(
                    $"{location} must contain zero RoomClimateContext components, but found {contextCount}: {DescribeContexts(contexts)}.");
            }

            return issues;
        }

        if (contextCount == 0)
        {
            issues.Add($"{location} is missing its required single RoomClimateContext component.");
            return issues;
        }

        if (contextCount > 1)
        {
            issues.Add(
                $"{location} has duplicate RoomClimateContext components ({contextCount}); exactly one is required: {DescribeContexts(contexts)}.");
        }

        for (int i = 0; i < contextCount; i++)
        {
            ContextInfo context = contexts[i];
            string objectPath = string.IsNullOrWhiteSpace(context.ObjectPath) ? "<unknown object>" : context.ObjectPath;
            if (string.IsNullOrWhiteSpace(context.RegionId))
            {
                issues.Add($"{location} RoomClimateContext '{objectPath}' has a blank regionId.");
            }
            else if (validRegionIds == null || !validRegionIds.Contains(context.RegionId))
            {
                issues.Add(
                    $"{location} RoomClimateContext '{objectPath}' references unknown regionId '{context.RegionId}'. Assign an ID present in ClimateRegionCatalog.");
            }
        }

        return issues;
    }

    private static int ValidateEnabledBuildSettingsScenes(out int sceneCount, out int contextCount)
    {
        sceneCount = 0;
        contextCount = 0;
        int issueCount = 0;

        ClimateRegionCatalog catalog = AssetDatabase.LoadAssetAtPath<ClimateRegionCatalog>(CatalogAssetPath);
        WorldTimeState worldTimeState = AssetDatabase.LoadAssetAtPath<WorldTimeState>(WorldTimeAssetPath);
        bool catalogValid = TryBuildRegionLookup(catalog, worldTimeState, out HashSet<string> regionIds, out string catalogError);
        if (!catalogValid)
        {
            Debug.LogError(
                $"[WorldTimeClimateValidator] Authored climate catalog/graph at '{CatalogAssetPath}' (checked against '{WorldTimeAssetPath}') is invalid: {catalogError}");
            issueCount++;
        }

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[i];
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                continue;

            sceneCount++;
            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            Scene scene = SceneManager.GetSceneByPath(buildScene.path);
            bool openedByValidator = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                    openedByValidator = true;
                }

                if (!scene.IsValid())
                {
                    Debug.LogError(
                        $"[WorldTimeClimateValidator] Enabled scene '{sceneName}' at '{buildScene.path}' did not produce a valid Scene handle.");
                    issueCount++;
                    continue;
                }

                List<ContextInfo> contexts = CollectContextInfo(scene);
                contextCount += contexts.Count;
                List<string> sceneIssues = GetContextIssues(
                    sceneName,
                    buildScene.path,
                    string.Equals(sceneName, BootSceneName, StringComparison.Ordinal),
                    contexts,
                    regionIds);

                for (int issueIndex = 0; issueIndex < sceneIssues.Count; issueIndex++)
                {
                    Debug.LogError($"[WorldTimeClimateValidator] {sceneIssues[issueIndex]}");
                    issueCount++;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[WorldTimeClimateValidator] Could not inspect enabled scene '{sceneName}' at '{buildScene.path}': {exception.Message}");
                issueCount++;
            }
            finally
            {
                if (openedByValidator && scene.IsValid() && scene.isLoaded)
                {
                    try
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"[WorldTimeClimateValidator] Could not close temporary additive scene '{sceneName}' at '{buildScene.path}': {exception.Message}");
                        issueCount++;
                    }
                }
            }
        }

        return issueCount;
    }

    private static List<ContextInfo> CollectContextInfo(Scene scene)
    {
        List<ContextInfo> contexts = new List<ContextInfo>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            RoomClimateContext[] components = roots[rootIndex].GetComponentsInChildren<RoomClimateContext>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                RoomClimateContext context = components[componentIndex];
                if (context == null || context.gameObject.scene != scene)
                    continue;

                contexts.Add(new ContextInfo(GetHierarchyPath(context.transform), context.RegionId));
            }
        }

        contexts.Sort((left, right) => StringComparer.Ordinal.Compare(left.ObjectPath, right.ObjectPath));
        return contexts;
    }

    private static string DescribeContexts(IReadOnlyList<ContextInfo> contexts)
    {
        if (contexts == null || contexts.Count == 0)
            return "<none>";

        List<string> paths = new List<string>(contexts.Count);
        for (int i = 0; i < contexts.Count; i++)
        {
            string path = string.IsNullOrWhiteSpace(contexts[i].ObjectPath) ? "<unknown object>" : contexts[i].ObjectPath;
            paths.Add($"'{path}'");
        }

        return string.Join(", ", paths);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<unknown object>";

        List<string> segments = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static bool HasDirtyLoadedScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
                return true;
        }

        return false;
    }
}
