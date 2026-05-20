using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private PlayerAbilityState abilityState;
    [SerializeField] private bool saveOnApplicationQuit = true;

    public SaveData CurrentSave { get; private set; }
    public int CurrentSlot { get; private set; }

    public string ActiveRespawnMarkerKey =>
        CurrentSave?.player?.activeRespawnMarkerKey ?? "";

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

        if (saveOnApplicationQuit)
            Application.quitting += SaveOnQuit;
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
        CurrentSave.player.currentScene = SceneManager.GetActiveScene().name;
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

    // -------------------------------------------------------------------------
    // Respawn keys
    // -------------------------------------------------------------------------

    public void SetActiveRespawnMarkerKey(string key)
    {
        if (CurrentSave?.player == null) return;
        CurrentSave.player.activeRespawnMarkerKey = key ?? "";
    }

    public void SetActiveHazardRespawnMarkerKey(string key)
    {
        if (CurrentSave?.player == null) return;
        CurrentSave.player.activeHazardRespawnMarkerKey = key ?? "";
    }

    // -------------------------------------------------------------------------
    // Ability state gather / apply
    // -------------------------------------------------------------------------

    private void GatherSaveData()
    {
        if (abilityState == null)
        {
            Debug.LogWarning("[SaveManager] PlayerAbilityState not assigned — ability data will not be saved.");
            return;
        }
        ((ISaveTarget)abilityState).GatherSaveData(CurrentSave);
    }

    private void ApplySaveData()
    {
        if (abilityState == null)
        {
            Debug.LogWarning("[SaveManager] PlayerAbilityState not assigned — ability data will not be applied.");
            return;
        }
        ((ISaveTarget)abilityState).ApplySaveData(CurrentSave);
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
