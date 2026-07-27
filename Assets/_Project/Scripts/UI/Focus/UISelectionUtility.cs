using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small shared helper for moving EventSystem selection to a valid Selectable. Hidden or
/// non-active selections are never applied.
/// </summary>
public static class UISelectionUtility
{
    public static bool IsSelectable(Selectable selectable)
    {
        return selectable != null
            && selectable.gameObject.activeInHierarchy
            && selectable.IsInteractable();
    }

    public static void Select(EventSystem eventSystem, Selectable selectable)
    {
        if (eventSystem == null)
        {
            return;
        }

        eventSystem.SetSelectedGameObject(IsSelectable(selectable) ? selectable.gameObject : null);
    }

    /// <summary>
    /// Selects <paramref name="preferred"/> if still valid, otherwise falls back to
    /// <paramref name="fallback"/>.
    /// </summary>
    public static void SelectPreferredOrFallback(EventSystem eventSystem, Selectable preferred, Selectable fallback)
    {
        if (IsSelectable(preferred))
        {
            Select(eventSystem, preferred);
            return;
        }

        Select(eventSystem, fallback);
    }
}
