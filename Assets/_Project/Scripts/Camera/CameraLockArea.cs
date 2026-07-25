using System.Collections.Generic;
using UnityEngine;

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
        return other != null
            && (other.CompareTag("Player")
                || (other.transform.root != null && other.transform.root.CompareTag("Player")));
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
#endif
}
