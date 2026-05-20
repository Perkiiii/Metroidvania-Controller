using System;
using UnityEngine;

public static class SaveSerializer
{
    public static bool TrySerialize(SaveData data, out string json)
    {
        if (data == null)
        {
            Debug.LogWarning("[SaveSerializer] Cannot serialize null SaveData.");
            json = null;
            return false;
        }

        try
        {
            json = JsonUtility.ToJson(data, prettyPrint: false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSerializer] Serialization failed: {ex.Message}");
            json = null;
            return false;
        }
    }

    public static bool TryDeserialize(string json, out SaveData data)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[SaveSerializer] Cannot deserialize null or empty JSON.");
            data = null;
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogWarning("[SaveSerializer] Deserialized SaveData was null.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveSerializer] Deserialization failed: {ex.Message}");
            data = null;
            return false;
        }
    }
}
