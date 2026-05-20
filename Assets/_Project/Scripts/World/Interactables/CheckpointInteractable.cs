using UnityEngine;

public sealed class CheckpointInteractable : InteractableBase
{
    [SerializeField] private RespawnMarker respawnMarker;

    public override void Interact()
    {
        if (respawnMarker == null)
        {
            Debug.LogWarning($"[CheckpointInteractable] '{name}' has no RespawnMarker assigned.", this);
            return;
        }

        GameManager.Instance?.SetActiveRespawnMarker(respawnMarker);
        SaveManager.Instance?.Save();
        Debug.Log($"[CheckpointInteractable] Activated: {respawnMarker.Key}");
    }
}
