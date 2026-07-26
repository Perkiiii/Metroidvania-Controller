using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraLockArea : MonoBehaviour
{
    [SerializeField] private int priority;
    [SerializeField] private bool lockX = true;
    [SerializeField] private bool lockY = true;

    [Header("Look Clamp")]
    [SerializeField] private bool preventLookUp;
    [SerializeField] private bool overrideLookYMax;
    [SerializeField] private float lookYMax;
    [SerializeField] private bool preventLookDown;
    [SerializeField] private bool overrideLookYMin;
    [SerializeField] private float lookYMin;

    [Header("Transition Override")]
    [Tooltip("Use the settings below instead of the shared CameraConfig defaults when this area becomes or stops being the active lock.")]
    [SerializeField] private bool useTransitionOverride;
    [SerializeField] private CameraTransitionSettings entryTransitionOverride = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [SerializeField] private CameraTransitionSettings exitTransitionOverride = CameraTransitionSettings.Live(0.15f, 0.35f, false);

    private static readonly List<CameraLockArea> activeAreas = new List<CameraLockArea>();
    private BoxCollider2D box;

    public static IReadOnlyList<CameraLockArea> ActiveAreas => activeAreas;
    public int Priority => priority;
    public bool LockX => lockX;
    public bool LockY => lockY;
    public bool PreventLookUp => preventLookUp;
    public bool PreventLookDown => preventLookDown;
    public bool OverrideLookYMax => overrideLookYMax;
    public bool OverrideLookYMin => overrideLookYMin;
    public bool HasLookYMax => preventLookUp && overrideLookYMax;
    public bool HasLookYMin => preventLookDown && overrideLookYMin;
    public float LookYMax => lookYMax;
    public float LookYMin => lookYMin;
    public bool UseTransitionOverride => useTransitionOverride;
    public CameraTransitionSettings EntryTransitionOverride => entryTransitionOverride;
    public CameraTransitionSettings ExitTransitionOverride => exitTransitionOverride;

    private void Awake()
    {
        CacheCollider();
    }

    private void OnEnable()
    {
        CacheCollider();
        if (!activeAreas.Contains(this))
        {
            activeAreas.Add(this);
        }

        GameCameras.Instance?.RefreshOverlapFor(this);
    }

    private void Reset()
    {
        CacheCollider();
        box.isTrigger = true;
    }

    public Rect GetLockRect()
    {
        CacheCollider();

        Bounds b = box.bounds;
        return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
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
        if (!IsPlayer(other))
        {
            return;
        }

        CameraEventService.RaiseLockEntered(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        CameraEventService.RaiseLockEntered(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        CameraEventService.RaiseLockExited(this);
    }

    private void OnDisable()
    {
        activeAreas.Remove(this);
        CameraEventService.RaiseLockExited(this);
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
    private void OnValidate()
    {
        CacheCollider();
        if (box != null && !box.isTrigger)
        {
            Debug.LogWarning($"[{nameof(CameraLockArea)}] '{name}' activation collider must be a trigger.", this);
        }

        if (!lockX && !lockY)
        {
            Debug.LogWarning($"[{nameof(CameraLockArea)}] '{name}' does not lock either axis.", this);
        }

        if (HasLookYMin && HasLookYMax && lookYMin > lookYMax)
        {
            Debug.LogWarning($"[{nameof(CameraLockArea)}] '{name}' has lookYMin greater than lookYMax.", this);
        }
    }

    private void OnDrawGizmos()
    {
        CacheCollider();
        if (box == null)
        {
            return;
        }

        bool isActiveLock = Application.isPlaying
            && GameCameras.Instance != null
            && GameCameras.Instance.Controller != null
            && GameCameras.Instance.Controller.CurrentLockArea == this;

        Gizmos.color = isActiveLock ? new Color(1f, 0.55f, 0f, 1f) : new Color(1f, 0.85f, 0.1f, 0.85f);
        Bounds activation = box.bounds;
        Gizmos.DrawWireCube(activation.center, activation.size);
    }

    private void OnDrawGizmosSelected()
    {
        CacheCollider();
        if (box == null)
        {
            return;
        }

        Bounds activation = box.bounds;
        float z = activation.center.z;

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(activation.center, activation.size);

        Rect framed = GetLockRect();
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
        DrawWireRect(framed, z);

        CameraGizmoUtility.GetApproximateFrustumHalfExtents(out float halfW, out float halfH, out bool isLive);
        CameraGizmoUtility.GetInsetInterval(framed.xMin, framed.xMax, halfW, out float centerMinX, out float centerMaxX, out bool collapsedX);
        CameraGizmoUtility.GetInsetInterval(framed.yMin, framed.yMax, halfH, out float centerMinY, out float centerMaxY, out bool collapsedY);

        float regionMinX = lockX ? centerMinX : framed.xMin;
        float regionMaxX = lockX ? centerMaxX : framed.xMax;
        float regionMinY = lockY ? centerMinY : framed.yMin;
        float regionMaxY = lockY ? centerMaxY : framed.yMax;
        bool xCollapsed = lockX && collapsedX;
        bool yCollapsed = lockY && collapsedY;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        if (xCollapsed && yCollapsed)
        {
            Gizmos.DrawSphere(new Vector3((regionMinX + regionMaxX) * 0.5f, (regionMinY + regionMaxY) * 0.5f, z), 0.15f);
        }
        else if (xCollapsed)
        {
            Gizmos.DrawLine(new Vector3(regionMinX, regionMinY, z), new Vector3(regionMinX, regionMaxY, z));
        }
        else if (yCollapsed)
        {
            Gizmos.DrawLine(new Vector3(regionMinX, regionMinY, z), new Vector3(regionMaxX, regionMinY, z));
        }
        else
        {
            DrawWireRect(new Rect(regionMinX, regionMinY, regionMaxX - regionMinX, regionMaxY - regionMinY), z);
        }

#if UNITY_EDITOR
        string axesLabel = lockX && lockY ? "XY" : lockX ? "X" : lockY ? "Y" : "none";
        string overrideLabel = useTransitionOverride ? " | transition override" : string.Empty;
        string liveLabel = isLive ? string.Empty : " (approx. viewport)";
        string warnLabel = xCollapsed || yCollapsed ? " | SMALLER THAN VIEWPORT" : string.Empty;
        Handles.Label(
            activation.center + Vector3.up * (activation.extents.y + 0.3f),
            $"CameraLockArea '{name}' P{priority} [{axesLabel}]{overrideLabel}{liveLabel}{warnLabel}");
#endif
    }

    private static void DrawWireRect(Rect rect, float z)
    {
        Vector3 bl = new Vector3(rect.xMin, rect.yMin, z);
        Vector3 br = new Vector3(rect.xMax, rect.yMin, z);
        Vector3 tr = new Vector3(rect.xMax, rect.yMax, z);
        Vector3 tl = new Vector3(rect.xMin, rect.yMax, z);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
#endif
}
