using UnityEngine;

// Local recovery point for recoverable hazards such as pits, spikes, acid, or lava.
public sealed class HazardRespawnMarker : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool facingRight = true;

    public Vector3 RespawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public int FacingDirection => facingRight ? 1 : -1;
}
