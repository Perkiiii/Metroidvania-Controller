using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        return GameCameras.IsCanonicalPlayerCollider(other);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        CacheCollider();
        if (box == null)
        {
            return;
        }

        bool isActive = Application.isPlaying
            && GameCameras.Instance != null
            && GameCameras.Instance.Controller != null
            && GameCameras.Instance.Controller.CurrentBoundsVolume == this;

        Gizmos.color = isActive ? new Color(0.1f, 0.9f, 0.3f, 1f) : new Color(0.1f, 0.7f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
    }

    private void OnDrawGizmosSelected()
    {
        CacheCollider();
        if (box == null)
        {
            return;
        }

        Bounds room = box.bounds;
        float z = room.center.z;

        Gizmos.color = new Color(0.1f, 0.7f, 0.9f, 0.9f);
        Gizmos.DrawWireCube(room.center, room.size);

        CameraGizmoUtility.GetApproximateFrustumHalfExtents(out float halfW, out float halfH, out bool isLive);
        CameraGizmoUtility.GetInsetInterval(room.min.x, room.max.x, halfW, out float minX, out float maxX, out bool collapsedX);
        CameraGizmoUtility.GetInsetInterval(room.min.y, room.max.y, halfH, out float minY, out float maxY, out bool collapsedY);

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        if (collapsedX && collapsedY)
        {
            Gizmos.DrawSphere(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, z), 0.15f);
        }
        else if (collapsedX)
        {
            Gizmos.DrawLine(new Vector3(minX, minY, z), new Vector3(minX, maxY, z));
        }
        else if (collapsedY)
        {
            Gizmos.DrawLine(new Vector3(minX, minY, z), new Vector3(maxX, minY, z));
        }
        else
        {
            Vector3 bl = new Vector3(minX, minY, z);
            Vector3 br = new Vector3(maxX, minY, z);
            Vector3 tr = new Vector3(maxX, maxY, z);
            Vector3 tl = new Vector3(minX, maxY, z);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }

        string liveLabel = isLive ? string.Empty : " (approx. viewport)";
        string warnLabel = collapsedX || collapsedY ? " | SMALLER THAN VIEWPORT" : string.Empty;
        Handles.Label(
            room.center + Vector3.up * (room.extents.y + 0.3f),
            $"CameraBoundsVolume '{name}'{liveLabel}{warnLabel}");
    }
#endif
}
