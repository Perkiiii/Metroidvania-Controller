#if UNITY_INCLUDE_TESTS
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Test-only root fixture compiled with Assembly-CSharp so custom PlayMode test assemblies can
/// supply an <see cref="IUIFlowRootScreen"/> through reflection without creating a production
/// screen or weakening the runtime interface boundary.
/// </summary>
public sealed class UIFlowTestRootScreen : MonoBehaviour, IUIFlowRootScreen
{
    private EventSystem eventSystem;
    private Selectable parentSelection;

    public event Action CloseRequested;
    public int ShowCount { get; private set; }
    public int HideCount { get; private set; }
    public int BackCount { get; private set; }
    public bool HandleBackResult { get; set; }
    public bool HasOpenModal { get; set; }
    public Selectable FirstSelection => parentSelection;

    public void Configure(EventSystem targetEventSystem, Selectable targetParentSelection)
    {
        eventSystem = targetEventSystem;
        parentSelection = targetParentSelection;
    }

    public void Show() => ShowCount++;
    public void Hide() => HideCount++;
    public void RaiseCloseRequested() => CloseRequested?.Invoke();

    public bool HandleBackInternally()
    {
        BackCount++;
        if (HasOpenModal)
        {
            HasOpenModal = false;
            eventSystem.SetSelectedGameObject(parentSelection.gameObject);
            return true;
        }

        return HandleBackResult;
    }
}
#endif
