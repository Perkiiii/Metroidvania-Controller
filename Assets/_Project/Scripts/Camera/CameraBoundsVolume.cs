using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraBoundsVolume : MonoBehaviour
{
    private static readonly List<CameraBoundsVolume> activeVolumes = new List<CameraBoundsVolume>();
    private BoxCollider2D box;

    public static IReadOnlyList<CameraBoundsVolume> ActiveVolumes => activeVolumes;

    private void Awake()
    {
        CacheCollider();
    }

    private void OnEnable()
    {
        CacheCollider();
        if (!activeVolumes.Contains(this))
        {
            activeVolumes.Add(this);
        }

        GameCameras.Instance?.RefreshOverlapFor(this);
    }

    public Bounds GetBounds()
    {
        CacheCollider();
        return box.bounds;
    }

    public bool Overlaps(Collider2D other)
    {
        CacheCollider();
        return other != null
            && other.enabled
            && box.enabled
            && Physics2D.Distance(box, other).isOverlapped;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        GameCameras.Instance?.Controller.SetBoundsVolume(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        GameCameras.Instance?.Controller.SetBoundsVolume(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        GameCameras.Instance?.Controller.ClearBoundsVolume(this);
    }

    private void OnDisable()
    {
        activeVolumes.Remove(this);
        GameCameras.Instance?.Controller.ClearBoundsVolume(this);
    }

    private void CacheCollider()
    {
        if (box == null)
        {
            box = GetComponent<BoxCollider2D>();
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other != null
            && (other.CompareTag("Player")
                || (other.transform.root != null && other.transform.root.CompareTag("Player")));
    }
}
