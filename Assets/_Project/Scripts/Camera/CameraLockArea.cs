using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraLockArea : MonoBehaviour
{
    [SerializeField] private int priority;
    [SerializeField] private bool lockX = true;
    [SerializeField] private bool lockY = true;

    [Header("Look Clamp")]
    [SerializeField] private bool preventLookUp;
    [SerializeField] private float lookYMax;
    [SerializeField] private bool preventLookDown;
    [SerializeField] private float lookYMin;

    private BoxCollider2D box;

    public int Priority => priority;
    public bool LockX => lockX;
    public bool LockY => lockY;
    public bool PreventLookUp => preventLookUp;
    public bool PreventLookDown => preventLookDown;
    public bool HasLookYMax => preventLookUp && !Mathf.Approximately(lookYMax, 0f);
    public bool HasLookYMin => preventLookDown && !Mathf.Approximately(lookYMin, 0f);
    public float LookYMax => lookYMax;
    public float LookYMin => lookYMin;

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

    public Rect GetLockRect()
    {
        if (box == null)
        {
            box = GetComponent<BoxCollider2D>();
        }

        Bounds b = box.bounds;
        return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        CameraEventService.RaiseLockEntered(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        CameraEventService.RaiseLockEntered(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        CameraEventService.RaiseLockExited(this);
    }

    private void OnDisable()
    {
        CameraEventService.RaiseLockExited(this);
    }
}
