using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private List<ScriptableObject> saveTargets = new List<ScriptableObject>();
    [SerializeField] private bool saveOnApplicationQuit = true;

    private readonly List<ISaveTarget> registeredSaveTargets = new List<ISaveTarget>();

    public SaveData CurrentSave { get; private set; }
    public int CurrentSlot { get; private set; }

    public string ActiveRespawnMarkerKey =>
        CurrentSave?.player?.activeRespawnMarkerKey ?? "";

    public string ActiveRespawnSceneName =>
        CurrentSave?.player?.activeRespawnSceneName ?? "";

    public string ActiveHazardRespawnMarkerKey =>
        CurrentSave?.player?.activeHazardRespawnMarkerKey ?? "";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheSaveTargets(logWarnings: true);

        if (saveOnApplicationQuit)
            Application.quitting += SaveOnQuit;
    }

    private void OnValidate()
    {
        CacheSaveTargets(logWarnings: true);
    }

    private void OnDestroy()
    {
        Application.quitting -= SaveOnQuit;
    }

    // -------------------------------------------------------------------------
    // Load / Create
    // -------------------------------------------------------------------------

    public void LoadOrCreate(int slot = 0)
    {
        CurrentSlot = slot;

        if (SaveFileStore.Exists(slot))
        {
            if (SaveFileStore.Read(slot, out string json) &&
                SaveSerializer.TryDeserialize(json, out SaveData data))
            {
                SaveDataMigrator.Migrate(data);
                CurrentSave = data;
                ApplySaveData();
                Debug.Log($"[SaveManager] Save loaded from slot {slot} at '{SaveFileStore.GetPath(slot)}'.");
                return;
            }

            Debug.LogWarning($"[SaveManager] Save at slot {slot} was unreadable or corrupt. Creating fresh save.");
        }
        else
        {
            Debug.Log($"[SaveManager] No save found at slot {slot}. Creating fresh save.");
        }

        CreateFreshSave(slot);
    }

    public void CreateFreshSave(int slot = 0)
    {
        CurrentSlot = slot;
        SaveData fresh = new SaveData();
        SaveDataMigrator.Migrate(fresh);
        CurrentSave = fresh;
        ApplySaveData();
        Save(slot);
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    public void Save()
    {
        Save(CurrentSlot);
    }

    public void Save(int slot)
    {
        if (CurrentSave == null)
        {
            Debug.LogWarning("[SaveManager] Cannot save — CurrentSave is null.");
            return;
        }

        GatherSaveData();
        CurrentSave.meta.lastSavedUtc = DateTime.UtcNow.ToString("o");
        // Boot is a composition root, not a playable scene. Do not overwrite the
        // last playable location when Unity exits while the bootstrap scene is active.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex != 0)
            CurrentSave.player.currentScene = activeScene.name;
        SaveDataMigrator.Migrate(CurrentSave);

        if (!SaveSerializer.TrySerialize(CurrentSave, out string json))
        {
            Debug.LogError("[SaveManager] Save failed — could not serialize save data.");
            return;
        }

        if (SaveFileStore.Write(slot, json))
        {
            CurrentSlot = slot;
            Debug.Log($"[SaveManager] Save written to slot {slot} at '{SaveFileStore.GetPath(slot)}'.");
        }
        else
        {
            Debug.LogError($"[SaveManager] Save failed — could not write to slot {slot}.");
        }
    }

    // -------------------------------------------------------------------------
    // Query / Delete
    // -------------------------------------------------------------------------

    public bool HasSave(int slot)
    {
        return SaveFileStore.Exists(slot);
    }

    public void DeleteSave(int slot)
    {
        SaveFileStore.Delete(slot);
    }

    public SaveStats GetSaveStats(int slot)
    {
        if (!SaveFileStore.Exists(slot))
            return SaveStats.Empty();

        if (!SaveFileStore.Read(slot, out string json))
            return SaveStats.Empty();

        if (!SaveSerializer.TryDeserialize(json, out SaveData data))
            return SaveStats.Empty();

        SaveDataMigrator.Migrate(data);
        return SaveStats.FromSaveData(data);
    }

    public string GetStartupScene(string fallbackScene)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string respawnScene = CurrentSave?.player?.activeRespawnSceneName ?? "";
        if (activeScene.buildIndex == 0 && respawnScene == activeScene.name)
            respawnScene = "";

        if (!string.IsNullOrEmpty(respawnScene) && Application.CanStreamedLevelBeLoaded(respawnScene))
        {
            Debug.Log($"[SaveManager] Startup scene resolved from active respawn scene: {respawnScene}");
            return respawnScene;
        }

        string currentScene = CurrentSave?.player?.currentScene ?? "";
        if (activeScene.buildIndex == 0 && currentScene == activeScene.name)
        {
            Debug.LogWarning($"[SaveManager] Ignoring saved bootstrap scene '{currentScene}' and using fallback '{fallbackScene}'.");
            currentScene = "";
        }

        if (!string.IsNullOrEmpty(currentScene) && Application.CanStreamedLevelBeLoaded(currentScene))
        {
            Debug.Log($"[SaveManager] Startup scene resolved from current scene: {currentScene}");
            return currentScene;
        }

        Debug.Log($"[SaveManager] Startup scene using fallback: {fallbackScene}");
        return fallbackScene;
    }

    // -------------------------------------------------------------------------
    // Respawn keys
    // -------------------------------------------------------------------------

    public void SetCurrentScene(string sceneName)
    {
        if (CurrentSave?.player == null) return;
        CurrentSave.player.currentScene = sceneName ?? "";
    }

    public void SetActiveRespawnPoint(string sceneName, string markerKey)
    {
        if (CurrentSave?.player == null) return;
        CurrentSave.player.activeRespawnSceneName = sceneName ?? "";
        CurrentSave.player.activeRespawnMarkerKey = markerKey ?? "";
    }

    public void SetActiveRespawnMarkerKey(string key)
    {
        // Compatibility only. This assumes the active scene is the marker's scene,
        // so normal checkpoint code should use SetActiveRespawnPoint instead.
        Debug.LogWarning("[SaveManager] SetActiveRespawnMarkerKey is deprecated. Use SetActiveRespawnPoint so scene and marker stay in sync.");
        SetActiveRespawnPoint(SceneManager.GetActiveScene().name, key);
    }

    public void SetActiveHazardRespawnMarkerKey(string key)
    {
        if (CurrentSave?.player == null) return;
        CurrentSave.player.activeHazardRespawnMarkerKey = key ?? "";
    }

    // -------------------------------------------------------------------------
    // Save targets
    // -------------------------------------------------------------------------

    private void GatherSaveData()
    {
        for (int i = 0; i < registeredSaveTargets.Count; i++)
            registeredSaveTargets[i].GatherSaveData(CurrentSave);
    }

    private void ApplySaveData()
    {
        for (int i = 0; i < registeredSaveTargets.Count; i++)
            registeredSaveTargets[i].ApplySaveData(CurrentSave);
    }

    private void CacheSaveTargets(bool logWarnings)
    {
        registeredSaveTargets.Clear();

        if (saveTargets == null || saveTargets.Count == 0)
        {
            if (logWarnings)
                Debug.LogWarning("[SaveManager] No save targets assigned — persistent ScriptableObject state will not be gathered or applied.", this);
            return;
        }

        HashSet<ScriptableObject> seenAssets = new HashSet<ScriptableObject>();
        for (int i = 0; i < saveTargets.Count; i++)
        {
            ScriptableObject asset = saveTargets[i];
            if (asset == null)
            {
                if (logWarnings)
                    Debug.LogWarning($"[SaveManager] Save target at index {i} is missing and will be ignored.", this);
                continue;
            }

            if (!seenAssets.Add(asset))
            {
                if (logWarnings)
                    Debug.LogWarning($"[SaveManager] Duplicate save target '{asset.name}' at index {i} will be ignored.", asset);
                continue;
            }

            if (!(asset is ISaveTarget saveTarget))
            {
                if (logWarnings)
                    Debug.LogWarning($"[SaveManager] Assigned asset '{asset.name}' at index {i} does not implement ISaveTarget and will be ignored.", asset);
                continue;
            }

            registeredSaveTargets.Add(saveTarget);
        }
    }

    // -------------------------------------------------------------------------
    // Quit
    // -------------------------------------------------------------------------

    private void SaveOnQuit()
    {
        if (Instance != this) return;
        Save();
    }
}
