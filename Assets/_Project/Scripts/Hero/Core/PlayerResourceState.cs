using System;
using UnityEngine;

public enum PlayerResourceChangeReason
{
    Gain,
    Spend,
    MaximumChanged,
    StateApplied,
    Reset,
    Cleared
}

public readonly struct PlayerResourceChangeInfo
{
    public PlayerResourceChangeInfo(int currentParts, int maximumParts, PlayerResourceChangeReason reason)
    {
        CurrentParts = currentParts;
        MaximumParts = maximumParts;
        Reason = reason;
    }

    public int CurrentParts { get; }
    public int MaximumParts { get; }
    public PlayerResourceChangeReason Reason { get; }
}

[CreateAssetMenu(menuName = "Project/Hero/Player Resource State", fileName = "PlayerResourceState")]
public sealed class PlayerResourceState : ScriptableObject, ISaveTarget
{
    [Header("Fresh Save Defaults")]
    [Tooltip("Authored fresh-save capacity in integer parts. Final resource tuning is deferred; the current asset intentionally remains at zero until manually configured for validation.")]
    [SerializeField, Min(0)] private int initialMaximumParts;

    [Header("Runtime State")]
    [SerializeField, Min(0)] private int currentParts;
    [SerializeField, Min(0)] private int maximumParts;

    public event Action<PlayerResourceChangeInfo> Changed;

    public int CurrentParts => currentParts;
    public int MaximumParts => maximumParts;

    private void OnEnable()
    {
        NormalizeRuntimeState();
    }

    private void OnValidate()
    {
        initialMaximumParts = Mathf.Max(0, initialMaximumParts);
        NormalizeRuntimeState();
    }

    public int Gain(int parts)
    {
        if (parts <= 0 || currentParts >= maximumParts)
            return 0;

        int previousParts = currentParts;
        currentParts = (int)Math.Min((long)maximumParts, (long)currentParts + parts);
        NotifyChanged(PlayerResourceChangeReason.Gain);
        return currentParts - previousParts;
    }

    public bool CanAfford(int parts)
    {
        return parts >= 0 && currentParts >= parts;
    }

    public bool TrySpend(int parts)
    {
        if (parts < 0 || !CanAfford(parts))
            return false;

        if (parts == 0)
            return true;

        currentParts -= parts;
        NotifyChanged(PlayerResourceChangeReason.Spend);
        return true;
    }

    public void SetMaximumParts(int value)
    {
        int nextMaximum = Mathf.Max(0, value);
        int nextCurrent = Mathf.Clamp(currentParts, 0, nextMaximum);
        ApplyState(nextCurrent, nextMaximum, PlayerResourceChangeReason.MaximumChanged);
    }

    public void ResetToDefaults()
    {
        ApplyState(0, Mathf.Max(0, initialMaximumParts), PlayerResourceChangeReason.Reset, forceNotify: true);
    }

    /// <summary>
    /// Forfeits current resource on death (normal, lethal-hazard, or forced-death) while
    /// preserving maximum capacity. No-op if already empty, so repeated death handling stays safe.
    /// </summary>
    public void Clear()
    {
        if (currentParts == 0)
            return;

        currentParts = 0;
        NotifyChanged(PlayerResourceChangeReason.Cleared);
    }

    public void GatherSaveData(SaveData data)
    {
        if (data == null)
            return;

        data.resource ??= new ResourceSaveData();
        data.resource.initialized = true;
        data.resource.currentParts = currentParts;
        data.resource.maximumParts = maximumParts;
    }

    public void ApplySaveData(SaveData data)
    {
        ResourceSaveData saved = data?.resource;
        if (saved == null || !saved.initialized)
        {
            ApplyState(0, Mathf.Max(0, initialMaximumParts), PlayerResourceChangeReason.StateApplied, forceNotify: true);
            return;
        }

        int loadedMaximum = Mathf.Max(0, saved.maximumParts);
        int loadedCurrent = Mathf.Clamp(saved.currentParts, 0, loadedMaximum);
        ApplyState(loadedCurrent, loadedMaximum, PlayerResourceChangeReason.StateApplied, forceNotify: true);
    }

    private void NormalizeRuntimeState()
    {
        maximumParts = Mathf.Max(0, maximumParts);
        currentParts = Mathf.Clamp(currentParts, 0, maximumParts);
    }

    private void ApplyState(
        int nextCurrent,
        int nextMaximum,
        PlayerResourceChangeReason reason,
        bool forceNotify = false)
    {
        nextMaximum = Mathf.Max(0, nextMaximum);
        nextCurrent = Mathf.Clamp(nextCurrent, 0, nextMaximum);
        bool changed = currentParts != nextCurrent || maximumParts != nextMaximum;

        currentParts = nextCurrent;
        maximumParts = nextMaximum;

        if (changed || forceNotify)
            NotifyChanged(reason);
    }

    private void NotifyChanged(PlayerResourceChangeReason reason)
    {
        Changed?.Invoke(new PlayerResourceChangeInfo(currentParts, maximumParts, reason));
    }
}
