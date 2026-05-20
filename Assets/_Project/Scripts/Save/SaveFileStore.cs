using System;
using System.IO;
using UnityEngine;

public static class SaveFileStore
{
    public static string GetPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot{slot}.json");
    }

    public static string GetBackupPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot{slot}.bak");
    }

    public static bool Exists(int slot)
    {
        return File.Exists(GetPath(slot));
    }

    public static bool Read(int slot, out string json)
    {
        string path = GetPath(slot);
        try
        {
            json = File.ReadAllText(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveFileStore] Failed to read save at '{path}': {ex.Message}");
            json = null;
            return false;
        }
    }

    public static bool Write(int slot, string json)
    {
        string path = GetPath(slot);
        string backupPath = GetBackupPath(slot);
        try
        {
            if (File.Exists(path))
                File.Copy(path, backupPath, overwrite: true);

            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveFileStore] Failed to write save at '{path}': {ex.Message}");
            return false;
        }
    }

    public static bool Delete(int slot)
    {
        string path = GetPath(slot);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveFileStore] Failed to delete save at '{path}': {ex.Message}");
            return false;
        }
    }
}
