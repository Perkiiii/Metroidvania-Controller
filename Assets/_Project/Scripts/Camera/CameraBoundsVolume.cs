using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraBoundsVolume : MonoBehaviour
{
    private BoxCollider2D box;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    public Bounds GetBounds() => box.bounds;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        GameCameras.Instance?.Controller.SetBoundsVolume(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        GameCameras.Instance?.Controller.ClearBoundsVolume(this);
    }
}
