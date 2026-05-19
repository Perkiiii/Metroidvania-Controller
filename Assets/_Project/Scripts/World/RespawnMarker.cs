using UnityEngine;

// Marks a position where the hero respawns after normal death.
// The key field is the stable string ID that SaveManager will persist (Milestone 4).
// At runtime, GameManager.SetActiveRespawnMarker() tracks which marker is current.
public sealed class RespawnMarker : MonoBehaviour
{
    [SerializeField] private string key;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool facingRight = true;

    public string Key => key;
    public Vector3 RespawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public int FacingDirection => facingRight ? 1 : -1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(key))
            Debug.LogWarning($"[RespawnMarker] '{name}' has no key set. Assign a unique key for save system compatibility.", this);
    }
#endif
}
