using UnityEngine;

/// <summary>
/// Applies the current normalized screen safe area to a full-screen RectTransform. Presentation
/// only; it allocates nothing per frame and reapplies only when the screen or safe area changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaInset : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void OnEnable()
    {
        ApplyIfChanged(force: true);
    }

    private void Update()
    {
        ApplyIfChanged(force: false);
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyIfChanged(force: true);
    }

    public void ApplyNow()
    {
        ApplyIfChanged(force: true);
    }

    private void ApplyIfChanged(bool force)
    {
        Rect safe = Screen.safeArea;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == lastSafeArea && screen == lastScreenSize)
        {
            return;
        }

        lastSafeArea = safe;
        lastScreenSize = screen;

        RectTransform rect = target != null ? target : transform as RectTransform;
        if (rect == null || screen.x <= 0 || screen.y <= 0)
        {
            return;
        }

        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;
        anchorMin.x /= screen.x;
        anchorMin.y /= screen.y;
        anchorMax.x /= screen.x;
        anchorMax.y /= screen.y;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
