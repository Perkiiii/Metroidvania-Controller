using UnityEngine;

// Bind tuning is kept separate from traversal tuning and from persistent resource values.
[CreateAssetMenu(menuName = "Project/Hero/Player Resource Config", fileName = "PlayerResourceConfig")]
public sealed class PlayerResourceConfig : ScriptableObject
{
    [Header("Bind - Temporary Validation Defaults")]
    [Tooltip("Temporary validation value. Final Bind balance is intentionally not decided in Milestone 5.")]
    [Min(1)] public int bindCostParts = 3;

    [Tooltip("Temporary validation value. Bind heals normal health only.")]
    [Min(1)] public int bindHealAmount = 1;

    [Tooltip("Temporary validation value. The action completes after this uninterrupted hold duration.")]
    [Min(0.01f)] public float bindDuration = 1f;

    [Header("HUD Presentation")]
    [Tooltip("Temporary development presentation value. This is not persistent state or final Silksong tuning.")]
    [Min(1)] public int partsPerPip = 3;
}
