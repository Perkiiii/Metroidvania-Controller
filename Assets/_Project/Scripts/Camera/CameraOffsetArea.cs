using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraOffsetArea : MonoBehaviour
{
    [SerializeField] private Vector2 offset;

    private BoxCollider2D box;

    public Vector2 Offset => offset;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    private void Reset()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)offset;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.18f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameCameras.IsCanonicalPlayerCollider(other))
        {
            return;
        }

        CameraEventService.RaiseOffsetEntered(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!GameCameras.IsCanonicalPlayerCollider(other))
        {
            return;
        }

        CameraEventService.RaiseOffsetEntered(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!GameCameras.IsCanonicalPlayerCollider(other))
        {
            return;
        }

        CameraEventService.RaiseOffsetExited(this);
    }

    private void OnDisable()
    {
        CameraEventService.RaiseOffsetExited(this);
    }
}
